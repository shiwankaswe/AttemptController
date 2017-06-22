using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using AttemptController;
using AttemptController.AccountStorage.Memory;
using AttemptController.Controllers;
using AttemptController.Models;
using AttemptController.EncryptionPrimitives;


namespace Tester
{
    public class SimulatedLoginAttemptGenerator
    {
        private readonly SimulatedAccounts _simAccounts;
        private readonly ExperimentalConfiguration _experimentalConfiguration;
        private readonly IpPool _ipPool;
        private readonly SimulatedPasswords _simPasswords;
        private readonly MemoryUserAccountController _userAccountController = new MemoryUserAccountController();

        public readonly SortedSet<SimulatedLoginAttempt> ScheduledBenignAttempts = new SortedSet<SimulatedLoginAttempt>(
            Comparer<SimulatedLoginAttempt>.Create( (a, b) => 
                a.TimeOfAttemptUtc.CompareTo(b.TimeOfAttemptUtc)));


        public SimulatedLoginAttemptGenerator(ExperimentalConfiguration experimentalConfiguration, SimulatedAccounts simAccounts, IpPool ipPool, SimulatedPasswords simPasswords)
        {
            _simAccounts = simAccounts;
            _experimentalConfiguration = experimentalConfiguration;
            _ipPool = ipPool;
            _simPasswords = simPasswords;
        }

        public static string AddTypoToPassword(string originalPassword)
        {
            const string typoAlphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ./";
            return originalPassword + typoAlphabet[(int) StrongRandomNumberGenerator.Get32Bits(typoAlphabet.Length)];
        }

        public SimulatedLoginAttempt BenignLoginAttempt(DateTime eventTimeUtc)
        {
            lock (ScheduledBenignAttempts)
            {
                if (ScheduledBenignAttempts.Count > 0 &&
                    ScheduledBenignAttempts.First().TimeOfAttemptUtc < eventTimeUtc)
                {
                    SimulatedLoginAttempt result = ScheduledBenignAttempts.First();
                    ScheduledBenignAttempts.Remove(result);
                    return result;
                }
            }

            string mistake = "";
            SimulatedUserAccount account = _simAccounts.BenignAccountSelector.GetItemByWeightedRandom();

            string cookie;
            if (account.Cookies.Count == 0 ||
                (account.Cookies.Count < _experimentalConfiguration.MaxCookiesPerUserAccount && StrongRandomNumberGenerator.GetFraction() > _experimentalConfiguration.ChanceOfCoookieReUse))
            {
                cookie = StrongRandomNumberGenerator.Get64Bits().ToString();
                account.Cookies.Add(cookie);
            }
            else
            {
                cookie = account.Cookies.ToArray()[(int)StrongRandomNumberGenerator.Get32Bits(account.Cookies.Count)];
            }

            IPAddress clientIp;
            if (account.ClientAddresses.Count == 0 ||
                (account.ClientAddresses.Count < _experimentalConfiguration.MaxIpPerUserAccount && StrongRandomNumberGenerator.GetFraction() < _experimentalConfiguration.ChanceOfIpReUse))
            {
                account.ClientAddresses.Add(clientIp = _ipPool.GetNewRandomBenignIp());
            }
            else
            {
                clientIp = account.ClientAddresses.ToArray()[(int)StrongRandomNumberGenerator.Get32Bits(account.ClientAddresses.Count)];
            }
                        
            string password = account.Password;

            if (StrongRandomNumberGenerator.GetFraction() < _experimentalConfiguration.ChanceOfLongRepeatOfStalePassword)
            {
                string newPassword = _simPasswords.GetPasswordFromWeightedDistribution();
                _userAccountController.SetPassword(account, newPassword, account.Password);
                mistake += "StalePassword";

                lock (ScheduledBenignAttempts)
                {
                    double additionalMistakes = 0;
                    DateTime currentTimeUtc = eventTimeUtc;
                    for (additionalMistakes = 1; additionalMistakes < _experimentalConfiguration.LengthOfLongRepeatOfOldPassword; additionalMistakes++)
                    {
                        currentTimeUtc = currentTimeUtc.AddSeconds(_experimentalConfiguration.MinutesBetweenLongRepeatOfOldPassword);
                        ScheduledBenignAttempts.Add(new SimulatedLoginAttempt(
                            account, password, false, false, clientIp, cookie, mistake, currentTimeUtc));
                    }
                    for (uint correctLogins = 1; correctLogins < _experimentalConfiguration.LengthOfLongRepeatOfOldPassword; correctLogins++)
                    {
                        currentTimeUtc = currentTimeUtc.AddSeconds(_experimentalConfiguration.MinutesBetweenLongRepeatOfOldPassword);
                        ScheduledBenignAttempts.Add(new SimulatedLoginAttempt(
                            account, newPassword, false, false, clientIp, cookie, mistake,currentTimeUtc));
                    }
                }
            }

            if (StrongRandomNumberGenerator.GetFraction() < _experimentalConfiguration.ChanceOfBenignPasswordTypo)
            {
                mistake += "Typo";
                lock (ScheduledBenignAttempts)
                {
                    double additionalMistakes = 0;
                    while (StrongRandomNumberGenerator.GetFraction() < _experimentalConfiguration.ChanceOfRepeatTypo)
                    {
                        ScheduledBenignAttempts.Add(new SimulatedLoginAttempt(
                            account, AddTypoToPassword(password), false, false, clientIp, cookie, mistake,
                            eventTimeUtc.AddSeconds(_experimentalConfiguration.DelayBetweenRepeatBenignErrorsInSeconds * ++additionalMistakes)));
                    }
                    ScheduledBenignAttempts.Add(new SimulatedLoginAttempt(
                        account, password, false, false, clientIp, cookie, "", eventTimeUtc.AddSeconds(
                            _experimentalConfiguration.DelayBetweenRepeatBenignErrorsInSeconds*(1 + additionalMistakes))));

                }
                password = AddTypoToPassword(password);
            }

            if (StrongRandomNumberGenerator.GetFraction() < _experimentalConfiguration.ChanceOfAccidentallyUsingAnotherAccountPassword)
            {
                mistake += "WrongPassword";

                lock (ScheduledBenignAttempts)
                {
                    double additionalMistakes = 0;
                    while(StrongRandomNumberGenerator.GetFraction() < _experimentalConfiguration.ChanceOfRepeatUseOfPasswordFromAnotherAccount) {
                        ScheduledBenignAttempts.Add(new SimulatedLoginAttempt(
                            account, _simPasswords.GetPasswordFromWeightedDistribution(), false, false, clientIp, cookie,
                            mistake, eventTimeUtc.AddSeconds(_experimentalConfiguration.DelayBetweenRepeatBenignErrorsInSeconds*++additionalMistakes)));
                    }
                    ScheduledBenignAttempts.Add(new SimulatedLoginAttempt(
                        account, password, false, false, clientIp, cookie, "", eventTimeUtc.AddSeconds(
                        _experimentalConfiguration.DelayBetweenRepeatBenignErrorsInSeconds * (additionalMistakes+1))));
                }

                password = _simPasswords.GetPasswordFromWeightedDistribution();
            }

            if (StrongRandomNumberGenerator.GetFraction() < _experimentalConfiguration.ChanceOfBenignAccountNameTypoResultingInAValidUserName)
            {
                mistake += "WrongAccountName";

                lock (ScheduledBenignAttempts)
                {
                    double additionalMistakes = 0;
                    while (StrongRandomNumberGenerator.GetFraction() < _experimentalConfiguration.ChanceOfRepeatWrongAccountName)
                    {
                        ScheduledBenignAttempts.Add(new SimulatedLoginAttempt(
                            _simAccounts.GetBenignAccountAtRandomUniform(), password, false, false, clientIp, cookie, mistake, eventTimeUtc.AddSeconds(
                            _experimentalConfiguration.DelayBetweenRepeatBenignErrorsInSeconds * ++additionalMistakes)));
                    }
                    ScheduledBenignAttempts.Add(new SimulatedLoginAttempt(
                        account, password, false, false, clientIp, cookie, "", eventTimeUtc.AddSeconds(
                        _experimentalConfiguration.DelayBetweenRepeatBenignErrorsInSeconds * (additionalMistakes + 1))));

                    account = _simAccounts.GetBenignAccountAtRandomUniform();
                }
            }

            return new SimulatedLoginAttempt(account, password, false, false, clientIp, cookie, mistake, eventTimeUtc);

        }

        public SimulatedLoginAttempt MaliciousLoginAttemptWeighted(DateTime eventTimeUtc)
        {
            SimulatedUserAccount targetBenignAccount =
                (StrongRandomNumberGenerator.GetFraction() <  _experimentalConfiguration.ProbabilityThatAttackerChoosesAnInvalidAccount)
                    ? null : _simAccounts.GetBenignAccountAtRandomUniform();

            return new SimulatedLoginAttempt(
                targetBenignAccount,
                _simPasswords.GetPasswordFromWeightedDistribution(),
                true, true,
                _ipPool.GetRandomMaliciousIp(),
                StrongRandomNumberGenerator.Get64Bits().ToString(),
                "",
                eventTimeUtc);
        }

        private readonly Object _breadthFirstLock = new object();
        private ulong _breadthFirstAttemptCounter;

        public SimulatedLoginAttempt MaliciousLoginAttemptBreadthFirst(DateTime eventTimeUtc)
        {
            bool invalidAccount = (StrongRandomNumberGenerator.GetFraction() <
                                   _experimentalConfiguration.ProbabilityThatAttackerChoosesAnInvalidAccount);

            ulong breadthFirstAttemptCount;
            lock (_breadthFirstLock)
            {
                breadthFirstAttemptCount = _breadthFirstAttemptCounter;
                if (!invalidAccount)
                    _breadthFirstAttemptCounter++;
            }

            int passwordIndex = (int)(breadthFirstAttemptCount / (ulong)_simAccounts.BenignAccounts.Count);
            int accountIndex = (int)(breadthFirstAttemptCount % (ulong)_simAccounts.BenignAccounts.Count);

            string mistake = invalidAccount ? "BadAccount" : "";
            SimulatedUserAccount targetBenignAccount = invalidAccount ? null : _simAccounts.BenignAccounts[accountIndex];
            string password = _simPasswords.OrderedListOfMostCommonPasswords[passwordIndex];

            return new SimulatedLoginAttempt(targetBenignAccount, password,
                true, true,
                _ipPool.GetRandomMaliciousIp(),
                StrongRandomNumberGenerator.Get64Bits().ToString(),
                mistake,
                eventTimeUtc);
        }


        public SimulatedLoginAttempt MaliciousLoginAttemptBreadthFirstAvoidMakingPopular(DateTime eventTimeUtc)
        {
            bool invalidAccount = (StrongRandomNumberGenerator.GetFraction() <
                                   _experimentalConfiguration.ProbabilityThatAttackerChoosesAnInvalidAccount);

            ulong breadthFirstAttemptCount;
            lock (_breadthFirstLock)
            {
                breadthFirstAttemptCount = _breadthFirstAttemptCounter;
                if (!invalidAccount)
                    _breadthFirstAttemptCounter++;
            }

            int passwordIndex = (int)((breadthFirstAttemptCount / (ulong)_experimentalConfiguration.MaxAttackerGuessesPerPassword)) % 
                _simPasswords.OrderedListOfMostCommonPasswords.Count;
            int accountIndex = (int)(breadthFirstAttemptCount % (ulong)_simAccounts.BenignAccounts.Count);

            string mistake = invalidAccount ? "BadAccount" : "";
            SimulatedUserAccount targetBenignAccount = invalidAccount ? null : _simAccounts.BenignAccounts[accountIndex];
            string password = _simPasswords.OrderedListOfMostCommonPasswords[passwordIndex];

            return new SimulatedLoginAttempt(targetBenignAccount, password,
                true, true,
                _ipPool.GetRandomMaliciousIp(),
                StrongRandomNumberGenerator.Get64Bits().ToString(),
                mistake,
                eventTimeUtc);
        }

        public SimulatedLoginAttempt MaliciousAttemptToSantiizeIpViaAValidLogin(IPAddress ipAddressToSanitizeThroughLogin)
        {
            SimulatedUserAccount simAccount = _simAccounts.GetMaliciousAccountAtRandomUniform();

            return new SimulatedLoginAttempt(simAccount, simAccount.Password,
                true, false,
                ipAddressToSanitizeThroughLogin, StrongRandomNumberGenerator.Get64Bits().ToString(), "",
                DateTime.UtcNow);
        }



    }
}
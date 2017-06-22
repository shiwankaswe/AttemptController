using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using AttemptController;
using AttemptController.DataStructures;
using AttemptController.EncryptionPrimitives;
using AttemptController.Models;

namespace Tester
{
    public class SimulatedLoginAttempt
    {
        public SimulatedUserAccount SimAccount;
        public string UserNameOrAccountId;
        public IPAddress AddressOfClientInitiatingRequest { get; set; }
        public DateTime TimeOfAttemptUtc { get; set; }
        public string CookieProvidedByBrowser { get; set; }
        public bool DeviceCookieHadPriorSuccessfulLoginForThisAccount { get; set; }
        public bool IsFrequentlyGuessedPassword = false;
        public bool IsRepeatFailure = false;
        public string Password;
        public bool IsPasswordValid;
        public bool IsFromAttacker;
        public bool IsGuess;
        public string MistakeType;

        public SimulatedLoginAttempt(SimulatedUserAccount account,
            string password,
            bool isFromAttacker,
            bool isGuess,
            IPAddress clientAddress,
            string cookieProvidedByBrowser,
            string mistakeType,
            DateTime eventTimeUtc
            )
        {
            SimAccount = account;
            UserNameOrAccountId = account != null ? account.UsernameOrAccountId : StrongRandomNumberGenerator.Get64Bits().ToString();
            IsPasswordValid = account != null && account.Password == password;
            AddressOfClientInitiatingRequest = clientAddress;
            TimeOfAttemptUtc = eventTimeUtc;
            CookieProvidedByBrowser = cookieProvidedByBrowser;
            Password = password;
            IsFromAttacker = isFromAttacker;
            IsGuess = isGuess;
            MistakeType = mistakeType;
        }

        public void UpdateSimulatorState(Simulator simulator, SimIpHistory ipHistory)
        {
            IsRepeatFailure = !IsPasswordValid && (
                (SimAccount == null)
                    ? simulator._recentIncorrectPasswords.AddMember(UserNameOrAccountId + "\n" + Password)
                    : simulator._userAccountController.AddIncorrectPhaseTwoHash(SimAccount, Password, TimeOfAttemptUtc)
            );

            int passwordsHeightOnBinomialLadder = (IsPasswordValid || IsRepeatFailure)
                ? simulator._binomialLadderFilter.GetHeight(Password)
                : simulator._binomialLadderFilter.Step(Password);

            IsFrequentlyGuessedPassword = passwordsHeightOnBinomialLadder >=
                                          simulator._experimentalConfiguration.BlockingOptions.BinomialLadderFrequencyThreshdold_T;

            DeviceCookieHadPriorSuccessfulLoginForThisAccount = SimAccount != null &&
                simulator._userAccountController.HasClientWithThisHashedCookieSuccessfullyLoggedInBefore(SimAccount, CookieProvidedByBrowser);

            if (SimAccount != null && IsPasswordValid)
            {
                ipHistory.AdjustBlockingScoreForPastTyposTreatedAsFullFailures(simulator, SimAccount, TimeOfAttemptUtc,
                    Password);
                simulator._userAccountController.RecordHashOfDeviceCookieUsedDuringSuccessfulLoginBackground(
                    SimAccount, CookieProvidedByBrowser, TimeOfAttemptUtc);
                SimAccount.ConsecutiveIncorrectAttempts.SetValue(0, this.TimeOfAttemptUtc);
            }
            else if (SimAccount != null && !IsRepeatFailure)
            {
                SimAccount.ConsecutiveIncorrectAttempts.AddInPlace(
                    simulator._experimentalConfiguration.BlockingOptions.BlockScoreHalfLife, 1d,
                    this.TimeOfAttemptUtc);
                if (SimAccount.ConsecutiveIncorrectAttempts.GetValue(
                        simulator._experimentalConfiguration.BlockingOptions.BlockScoreHalfLife)
                    >
                    SimAccount.MaxConsecutiveIncorrectAttempts.GetValue(
                        simulator._experimentalConfiguration.BlockingOptions.BlockScoreHalfLife))
                    SimAccount.MaxConsecutiveIncorrectAttempts.SetValue(SimAccount.ConsecutiveIncorrectAttempts);
            }

            if (!IsPasswordValid && !IsRepeatFailure && SimAccount != null)
            {
                ipHistory.RecentPotentialTypos.Add(new SimLoginAttemptSummaryForTypoAnalysis()
                {
                    WhenUtc = TimeOfAttemptUtc,
                    Password = Password,
                    UsernameOrAccountId = UserNameOrAccountId,
                    WasPasswordFrequent = IsFrequentlyGuessedPassword
                });
            }


            DecayingDouble decayingOneFromThisInstant = new DecayingDouble(1, TimeOfAttemptUtc);
            TimeSpan halfLife = simulator._experimentalConfiguration.BlockingOptions.BlockScoreHalfLife;
            if (IsPasswordValid)
            {
                ipHistory.SuccessfulLogins.AddInPlace(halfLife, decayingOneFromThisInstant);
            } else if (SimAccount == null)
            {
                if (IsRepeatFailure)
                {
                    if (IsFrequentlyGuessedPassword)
                        ipHistory.RepeatAccountFailuresFrequentPassword.AddInPlace(halfLife, decayingOneFromThisInstant);
                    else
                        ipHistory.RepeatAccountFailuresInfrequentPassword.AddInPlace(halfLife, decayingOneFromThisInstant);
                }
                else
                {
                    if (IsFrequentlyGuessedPassword)
                        ipHistory.AccountFailuresFrequentPassword.AddInPlace(halfLife, decayingOneFromThisInstant);
                    else
                        ipHistory.AccountFailuresInfrequentPassword.AddInPlace(halfLife, decayingOneFromThisInstant);
                }
            }
            else
            {
                if (IsRepeatFailure)
                {
                    if (IsFrequentlyGuessedPassword)
                        ipHistory.RepeatPasswordFailuresNoTypoFrequentPassword.AddInPlace(halfLife, decayingOneFromThisInstant);
                    else
                        ipHistory.RepeatPasswordFailuresNoTypoInfrequentPassword.AddInPlace(halfLife, decayingOneFromThisInstant);
                }
                else
                {
                    if (IsFrequentlyGuessedPassword)
                        ipHistory.PasswordFailuresNoTypoFrequentPassword.AddInPlace(halfLife, decayingOneFromThisInstant);
                    else
                        ipHistory.PasswordFailuresNoTypoInfrequentPassword.AddInPlace(halfLife, decayingOneFromThisInstant);
                }
            }

        }

        
    }
}

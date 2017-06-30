﻿using System.Collections.Generic;
using System.Net;
using AttemptController.EncryptionPrimitives;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AttemptController;
using AttemptController.AccountStorage.Memory;
using AttemptController.DataStructures;
using AttemptController.Models;

namespace Tester
{
    public class SimulatedAccounts
    {
        public List<SimulatedUserAccount> BenignAccounts = new List<SimulatedUserAccount>();
        public List<SimulatedUserAccount> AttackerAccounts = new List<SimulatedUserAccount>();
        public WeightedSelector<SimulatedUserAccount> BenignAccountSelector = new WeightedSelector<SimulatedUserAccount>();
        private readonly IpPool _ipPool;
        private readonly DebugLogger _logger;
        private readonly SimulatedPasswords _simPasswords;

        public SimulatedAccounts(IpPool ipPool, SimulatedPasswords simPasswords, DebugLogger logger)
        {
            _ipPool = ipPool;
            _logger = logger;
            _simPasswords = simPasswords;
        }


        public SimulatedUserAccount GetBenignAccountWeightedByLoginFrequency()
        {
            lock (BenignAccountSelector)
            {
                return BenignAccountSelector.GetItemByWeightedRandom();
            }
        }

        public SimulatedUserAccount GetBenignAccountAtRandomUniform()
        {
            return BenignAccounts[(int) StrongRandomNumberGenerator.Get32Bits(BenignAccounts.Count)];
        }

        public SimulatedUserAccount GetMaliciousAccountAtRandomUniform()
        {
            return AttackerAccounts[(int) StrongRandomNumberGenerator.Get32Bits(AttackerAccounts.Count)];
        }



        public void Generate(ExperimentalConfiguration experimentalConfiguration)
        {
            SimulatedUserAccountController simUserAccountController = new SimulatedUserAccountController();
            _logger.WriteStatus("Creating accounts");        
            ConcurrentBag<SimulatedUserAccount> benignSimulatedAccountBag = new ConcurrentBag<SimulatedUserAccount>();
            Parallel.For(0, (int) experimentalConfiguration.NumberOfBenignAccounts, (index) =>
            {
                //if (index > 0 && index % 10000 == 0)
                //    _logger.WriteStatus("Created {0:N0} benign accounts", index);
                SimulatedUserAccount userAccount = simUserAccountController.Create(
                    "user_" + index.ToString(),
                    _simPasswords.GetPasswordFromWeightedDistribution()
                );
                userAccount.ClientAddresses.Add(_ipPool.GetNewRandomBenignIp());
                string initialCookie = StrongRandomNumberGenerator.Get64Bits().ToString();
                userAccount.Cookies.Add(initialCookie);
                userAccount.HashesOfCookiesOfClientsThatHaveSuccessfullyLoggedIntoThisAccount[initialCookie] = true;

                benignSimulatedAccountBag.Add(userAccount);

                double inverseFrequency = Distributions.GetLogNormal(0, 1);
                if (inverseFrequency < 0.01d)
                    inverseFrequency = 0.01d;
                if (inverseFrequency > 50d)
                    inverseFrequency = 50d;
                double frequency = 1 / inverseFrequency;
                lock (BenignAccountSelector)
                {
                    BenignAccountSelector.AddItem(userAccount, frequency);
                }
            });
            BenignAccounts = benignSimulatedAccountBag.ToList();
           // _logger.WriteStatus("Finished creating {0:N0} benign accounts",
           //     experimentalConfiguration.NumberOfBenignAccounts);

            //_logger.WriteStatus("Creating attacker IPs");            
            _ipPool.GenerateAttackersIps();

            //_logger.WriteStatus("Creating {0:N0} attacker accounts",
            //    experimentalConfiguration.NumberOfAttackerControlledAccounts);
            ConcurrentBag<SimulatedUserAccount> maliciousSimulatedAccountBag = new ConcurrentBag<SimulatedUserAccount>();
            
            Parallel.For(0, (int) experimentalConfiguration.NumberOfAttackerControlledAccounts, (index) =>
            {
                SimulatedUserAccount userAccount = simUserAccountController.Create(
                    "attacker_" + index.ToString(),
                    _simPasswords.GetPasswordFromWeightedDistribution());

                userAccount.ClientAddresses.Add(_ipPool.GetRandomMaliciousIp());
                maliciousSimulatedAccountBag.Add(userAccount);
            });
            AttackerAccounts = maliciousSimulatedAccountBag.ToList();
            _logger.WriteStatus("Finished creating accounts",
                experimentalConfiguration.NumberOfAttackerControlledAccounts);
            
            Parallel.ForEach(BenignAccounts.Union(AttackerAccounts),
                (simAccount, loopState) =>
                {
                    simAccount.CreditHalfLife = experimentalConfiguration.BlockingOptions.AccountCreditLimitHalfLife;
                    simAccount.CreditLimit = experimentalConfiguration.BlockingOptions.AccountCreditLimit;

                    foreach (string cookie in simAccount.Cookies)
                        simUserAccountController.HasClientWithThisHashedCookieSuccessfullyLoggedInBefore(
                            simAccount,
                            LoginAttempt.HashCookie(cookie));
                });
            //_logger.WriteStatus("Finished creating user accounts for each simluated account record");
        }
    }
}
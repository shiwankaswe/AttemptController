using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AttemptController.Controllers;
using AttemptController.DataStructures;
using AttemptController.EncryptionPrimitives;
using AttemptController.AccountStorage.Memory;
using AttemptController.Interfaces;
using AttemptController.Models;
using AttemptController.Utilities;

namespace Tester
{
    public class SimulatedUserAccountControllerFactory : IFactory<SimulatedUserAccountController>
    {

        public SimulatedUserAccountController Create()
        {
            return new SimulatedUserAccountController();
        }
    }

    public class SimulatedUserAccountController : IUserAccountController<SimulatedUserAccount> 
    {
        public SimulatedUserAccountController()
        {
        }

        public SimulatedUserAccount Create(
            string usernameOrAccountId,
            string password = null,
            int? maxNumberOfCookiesToTrack = null,
            int? maxFailedPhase2HashesToTrack = null,
            DateTime? currentDateTimeUtc = null)
        {
            SimulatedUserAccount account = new SimulatedUserAccount
            {
                UsernameOrAccountId = usernameOrAccountId,
                Password = password,
                HashesOfCookiesOfClientsThatHaveSuccessfullyLoggedIntoThisAccount =
                    new ConcurrentDictionary<string,bool>(),
                RecentIncorrectPhase2Hashes =
                    new SmallCapacityConstrainedSet<string>(maxFailedPhase2HashesToTrack ??
                                                            UserAccountController<SimulatedUserAccount>
                                                                .DefaultMaxFailedPhase2HashesToTrack),
                ConsumedCredits = new DecayingDouble(0, currentDateTimeUtc),
                NumberOfIterationsToUseForPhase1Hash = 1
            };



            return account;
        }


        public byte[] ComputePhase1Hash(SimulatedUserAccount userAccount, string password)
        {
            return Encoding.UTF8.GetBytes(password);
        }

        public string ComputePhase2HashFromPhase1Hash(SimulatedUserAccount account, byte[] phase1Hash)
        {
            return Encoding.UTF8.GetString(phase1Hash);
        }

        public void SetPassword(
            SimulatedUserAccount userAccount,
            string newPassword,
            string oldPassword = null,
            string nameOfExpensiveHashFunctionToUse = null,
            int? numberOfIterationsToUseForPhase1Hash = null)
        {
            byte[] newPasswordHashPhase1 = ComputePhase1Hash(userAccount, newPassword);

            userAccount.PasswordHashPhase2 = ComputePhase2HashFromPhase1Hash(userAccount, newPasswordHashPhase1);            
        }


        public virtual void SetAccountLogKey(
            SimulatedUserAccount userAccount,
            Encryption.IPrivateKey accountLogKey,
            byte[] phase1HashOfCorrectPassword)
        {            
        }

        public Encryption.IPrivateKey DecryptPrivateAccountLogKey(
            SimulatedUserAccount userAccount,
            byte[] phase1HashOfCorrectPassword)
        {
            return null;
        }

#pragma warning disable CS1998          
        public async Task<bool> AddIncorrectPhaseTwoHashAsync(SimulatedUserAccount userAccount, string phase2Hash,
#pragma warning restore CS1998          
            DateTime? whenSeenUtc = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return userAccount.RecentIncorrectPhase2Hashes.Add(phase2Hash);
        }

        public bool AddIncorrectPhaseTwoHash(SimulatedUserAccount userAccount, string phase2Hash,
            DateTime? whenSeenUtc = null)
        {
            return userAccount.RecentIncorrectPhase2Hashes.Add(phase2Hash);
        }

#pragma warning disable CS1998          
        public async Task<bool> HasClientWithThisHashedCookieSuccessfullyLoggedInBeforeAsync(
#pragma warning restore CS1998          
            SimulatedUserAccount userAccount,
            string hashOfCookie,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return userAccount.HashesOfCookiesOfClientsThatHaveSuccessfullyLoggedIntoThisAccount.ContainsKey(hashOfCookie);
        }

        public bool HasClientWithThisHashedCookieSuccessfullyLoggedInBefore(
            SimulatedUserAccount userAccount,
            string hashOfCookie)
        {
            return userAccount.HashesOfCookiesOfClientsThatHaveSuccessfullyLoggedIntoThisAccount.ContainsKey(hashOfCookie);
        }

#pragma warning disable 1998
        public async Task RecordHashOfDeviceCookieUsedDuringSuccessfulLoginAsync(
            SimulatedUserAccount account, 
            string hashOfCookie,
#pragma warning restore 1998
            DateTime? whenSeenUtc = null,
            CancellationToken cancellationToken = new CancellationToken())
        {
            account.HashesOfCookiesOfClientsThatHaveSuccessfullyLoggedIntoThisAccount[hashOfCookie] = true;
        }

        public virtual void RecordHashOfDeviceCookieUsedDuringSuccessfulLoginBackground(
    SimulatedUserAccount userAccount,
    string hashOfCookie,
    DateTime? whenSeenUtc = null)
        {
            userAccount.HashesOfCookiesOfClientsThatHaveSuccessfullyLoggedIntoThisAccount[hashOfCookie] = true;
        }


#pragma warning disable 1998
        public async Task<double> TryGetCreditAsync(SimulatedUserAccount userAccount,
            double amountRequested,
            DateTime? timeOfRequestUtc = null,
            CancellationToken cancellationToken = default(CancellationToken))
#pragma warning restore 1998
        {
            DateTime timeOfRequestOrNowUtc = timeOfRequestUtc ?? DateTime.UtcNow;
            double amountAvailable = Math.Max(0, userAccount.CreditLimit - userAccount.ConsumedCredits.GetValue(userAccount.CreditHalfLife, timeOfRequestOrNowUtc));
            double amountConsumed = Math.Min(amountRequested, amountAvailable);
            userAccount.ConsumedCredits.SubtractInPlace(userAccount.CreditHalfLife, amountConsumed, timeOfRequestOrNowUtc);
            return amountConsumed;
        }
    }
}

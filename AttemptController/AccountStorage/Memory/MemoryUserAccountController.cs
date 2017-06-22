using AttemptController.Controllers;
using AttemptController.DataStructures;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace AttemptController.AccountStorage.Memory
{
    public class MemoryUserAccountControllerFactory : IUserAccountControllerFactory<MemoryUserAccount>
    {

        public IUserAccountController<MemoryUserAccount> Create()
        {
            return new MemoryUserAccountController();
        }
    }

    public class MemoryUserAccountController : UserAccountController<MemoryUserAccount>
    {
        public MemoryUserAccountController()
        {
        }

        public MemoryUserAccount Create(
            string usernameOrAccountId,
            string password = null,
            int numberOfIterationsToUseForHash = 0,
            string passwordHashFunctionName = null,
            int? maxNumberOfCookiesToTrack = null,
            int? maxFailedPhase2HashesToTrack = null,
            DateTime? currentDateTimeUtc = null)
        {
            MemoryUserAccount account = new MemoryUserAccount {UsernameOrAccountId = usernameOrAccountId};

            Initialize(account, password, numberOfIterationsToUseForHash, passwordHashFunctionName);

            account.HashesOfCookiesOfClientsThatHaveSuccessfullyLoggedIntoThisAccount =
                new ConcurrentDictionary<string,bool>();
            account.RecentIncorrectPhase2Hashes = new SmallCapacityConstrainedSet<string>(maxFailedPhase2HashesToTrack ?? DefaultMaxFailedPhase2HashesToTrack);
            account.ConsumedCredits = new DecayingDouble(0, currentDateTimeUtc);

            return account;
        }

        public override async Task<bool> AddIncorrectPhaseTwoHashAsync(MemoryUserAccount userAccount, string phase2Hash,
            DateTime? whenSeenUtc = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return userAccount.RecentIncorrectPhase2Hashes.Add(phase2Hash);
        }

        public override async Task<bool> HasClientWithThisHashedCookieSuccessfullyLoggedInBeforeAsync(
            MemoryUserAccount userAccount,
            string hashOfCookie,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (hashOfCookie == null)
                return false;

            return userAccount.HashesOfCookiesOfClientsThatHaveSuccessfullyLoggedIntoThisAccount.ContainsKey(hashOfCookie);
        }

        public override async Task RecordHashOfDeviceCookieUsedDuringSuccessfulLoginAsync(MemoryUserAccount account, string hashOfCookie,
            DateTime? whenSeenUtc = null, CancellationToken cancellationToken = new CancellationToken())
        {
            account.HashesOfCookiesOfClientsThatHaveSuccessfullyLoggedIntoThisAccount[hashOfCookie] = true;
        }


        public override async Task<double> TryGetCreditAsync(MemoryUserAccount userAccount, 
            double amountRequested,
            DateTime? timeOfRequestUtc = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            DateTime timeOfRequestOrNowUtc = timeOfRequestUtc ?? DateTime.UtcNow;
            double amountAvailable = Math.Max(0, userAccount.CreditLimit - userAccount.ConsumedCredits.GetValue(userAccount.CreditHalfLife, timeOfRequestOrNowUtc));
            double amountConsumed = Math.Min(amountRequested, amountAvailable);
            userAccount.ConsumedCredits.SubtractInPlace(userAccount.CreditHalfLife, amountConsumed, timeOfRequestOrNowUtc);
            return amountConsumed;
        }
    }
}

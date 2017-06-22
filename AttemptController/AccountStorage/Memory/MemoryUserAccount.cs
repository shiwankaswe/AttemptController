using AttemptController.Controllers;
using AttemptController.DataStructures;
using AttemptController.EncryptionPrimitives;
using AttemptController.Interfaces;
using System;
using System.Collections.Concurrent;

namespace AttemptController.AccountStorage.Memory
{
    public class MemoryUserAccount : IUserAccount
    {
        public string UsernameOrAccountId { get; set; }

        public byte[] SaltUniqueToThisAccount { get; set; } =
            StrongRandomNumberGenerator.GetBytes(UserAccountController<MemoryUserAccount>.DefaultSaltLength);

        public string PasswordHashPhase1FunctionName { get; set; } =
            ExpensiveHashFunctionFactory.DefaultFunctionName;


        public int NumberOfIterationsToUseForPhase1Hash { get; set; } =
            UserAccountController<MemoryUserAccount>.DefaultIterationsForPasswordHash;
        
        public byte[] EcPublicAccountLogKey { get; set; }
     
        public byte[] EcPrivateAccountLogKeyEncryptedWithPasswordHashPhase1 { get; set; }

        public string PasswordHashPhase2 { get; set; }

        public double CreditLimit { get; set; } = 
            UserAccountController<MemoryUserAccount>.DefaultCreditLimit;

        public TimeSpan CreditHalfLife { get; set; } =
            new TimeSpan( TimeSpan.TicksPerHour * UserAccountController<MemoryUserAccount>.DefaultCreditHalfLifeInHours);

        public DecayingDouble ConsumedCredits { get; set; }

        public ConcurrentDictionary<string,bool> HashesOfCookiesOfClientsThatHaveSuccessfullyLoggedIntoThisAccount { get; set; }

        public SmallCapacityConstrainedSet<string> RecentIncorrectPhase2Hashes { get; set; }



        public void Dispose()
        {

        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using AttemptController.EncryptionPrimitives;
using AttemptController.Interfaces;
using AttemptController.Models;
using AttemptController.Utilities;

namespace AttemptController.Controllers
{
    public abstract class UserAccountController<TAccount> : IUserAccountController<TAccount> where TAccount : IUserAccount
    {
        public const int DefaultIterationsForPasswordHash = 1000;
        public const int DefaultSaltLength = 8;
        public const int DefaultMaxFailedPhase2HashesToTrack = 8;
        public const int DefaultMaxNumberOfCookiesToTrack = 24;
        public const int DefaultCreditHalfLifeInHours = 12;
        public const double DefaultCreditLimit = 50;


        public void Initialize(TAccount userAccount,
            string password = null,
            int numberOfIterationsToUseForHash = 0,
            string passwordHashFunctionName = null)
        {
            if (numberOfIterationsToUseForHash > 0)
            {
                userAccount.NumberOfIterationsToUseForPhase1Hash = numberOfIterationsToUseForHash;
            }
            if (passwordHashFunctionName != null)
            {
                userAccount.PasswordHashPhase1FunctionName = passwordHashFunctionName;
            }

            if (password != null)
            {
                SetPassword(userAccount, password);
            }
        }


        public virtual byte[] ComputePhase1Hash(TAccount userAccount, string password)
        {
            return ExpensiveHashFunctionFactory.Get(userAccount.PasswordHashPhase1FunctionName)(
                password, userAccount.SaltUniqueToThisAccount, userAccount.NumberOfIterationsToUseForPhase1Hash);
        }

        public virtual string ComputePhase2HashFromPhase1Hash(TAccount account, byte[] phase1Hash)
        {
            return Convert.ToBase64String(ManagedSHA256.Hash(phase1Hash));
        }


        public virtual void SetPassword(
            TAccount userAccount,
            string newPassword,
            string oldPassword = null,
            string nameOfExpensiveHashFunctionToUse = null,
            int? numberOfIterationsToUseForPhase1Hash = null)
        {
            byte[] oldPasswordHashPhase1 = oldPassword == null ? null : ComputePhase1Hash(userAccount, oldPassword);


            if (nameOfExpensiveHashFunctionToUse != null)
            {
                userAccount.PasswordHashPhase1FunctionName = nameOfExpensiveHashFunctionToUse;
            }

            if (numberOfIterationsToUseForPhase1Hash.HasValue)
            {
                userAccount.NumberOfIterationsToUseForPhase1Hash = numberOfIterationsToUseForPhase1Hash.Value;
            }

            byte[] newPasswordHashPhase1 = ComputePhase1Hash(userAccount, newPassword);


            userAccount.PasswordHashPhase2 = ComputePhase2HashFromPhase1Hash(userAccount, newPasswordHashPhase1);


            if (oldPassword != null &&
                ComputePhase2HashFromPhase1Hash(userAccount, oldPasswordHashPhase1) == userAccount.PasswordHashPhase2)
            {

                try
                {
                    if (userAccount.EcPrivateAccountLogKeyEncryptedWithPasswordHashPhase1 == null)
                    {
                        using (Encryption.IPrivateKey accountLogKey =
                            Encryption.DecryptAesCbcEncryptedPrivateKey(
                                userAccount.EcPrivateAccountLogKeyEncryptedWithPasswordHashPhase1,
                                oldPasswordHashPhase1))
                        {
                            if (accountLogKey != null)
                            {
                                SetAccountLogKey(userAccount, accountLogKey, newPasswordHashPhase1);
                            }
                            return;
                        }
                    }
                }
                catch (Exception)
                {
                }
            }


            using (Encryption.IPrivateKey newPrivateKey = Encryption.GenerateNewPrivateKey())
            {
                SetAccountLogKey(userAccount, newPrivateKey, newPasswordHashPhase1);
            }
        }


        public virtual void SetAccountLogKey(
            TAccount userAccount,
            Encryption.IPrivateKey accountLogKey,
            byte[] phase1HashOfCorrectPassword)
        {

        }


        public virtual Encryption.IPrivateKey DecryptPrivateAccountLogKey(
            TAccount userAccount,
            byte[] phase1HashOfCorrectPassword)
        {
            return Encryption.DecryptAesCbcEncryptedPrivateKey(userAccount.EcPrivateAccountLogKeyEncryptedWithPasswordHashPhase1, phase1HashOfCorrectPassword);
        }

        public virtual void RecordHashOfDeviceCookieUsedDuringSuccessfulLoginBackground(
            TAccount userAccount,
            string hashOfCookie,
            DateTime? whenSeenUtc = null)
        {
            TaskHelper.RunInBackground(
                RecordHashOfDeviceCookieUsedDuringSuccessfulLoginAsync(userAccount, hashOfCookie, whenSeenUtc));
        }
        public abstract Task<bool> HasClientWithThisHashedCookieSuccessfullyLoggedInBeforeAsync(
            TAccount userAccount,
            string hashOfCookie,
            CancellationToken cancellationToken = new CancellationToken());

        public abstract Task RecordHashOfDeviceCookieUsedDuringSuccessfulLoginAsync(
            TAccount userAccount,
            string hashOfCookie,
            DateTime? whenSeenUtc = null, CancellationToken cancellationToken = new CancellationToken());

        public abstract Task<bool> AddIncorrectPhaseTwoHashAsync(
            TAccount userAccount,
            string phase2Hash,
            DateTime? whenSeenUtc = null,
            CancellationToken cancellationToken = new CancellationToken());

        public abstract Task<double> TryGetCreditAsync(
            TAccount userAccount,
            double amountRequested,
            DateTime? timeOfRequestUtc = null,
            CancellationToken cancellationToken = new CancellationToken());


    }
}

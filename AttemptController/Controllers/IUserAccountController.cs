using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using AttemptController.Interfaces;
using AttemptController.Models;

namespace AttemptController.Controllers
{
    public interface IUserAccountControllerFactory<TAccount> : IFactory<IUserAccountController<TAccount>>
        where TAccount : IUserAccount
    {
    };


    public interface IUserAccountController<in TAccount> where TAccount : IUserAccount
    {

        void SetPassword(
            TAccount userAccount,
            string newPassword,
            string oldPassword = null,
            string nameOfExpensiveHashFunctionToUse = null,
            int? numberOfIterationsToUseForPhase1Hash = null);


        byte[] ComputePhase1Hash(
            TAccount userAccount,
            string password);


        string ComputePhase2HashFromPhase1Hash(
            TAccount account,
            byte[] phase1Hash);


        void SetAccountLogKey(
            TAccount userAccount,
            EncryptionPrimitives.Encryption.IPrivateKey accountLogKey,
            byte[] phase1HashOfCorrectPassword);


        EncryptionPrimitives.Encryption.IPrivateKey DecryptPrivateAccountLogKey(
            TAccount userAccount,
            byte[] phase1HashOfCorrectPassword);


        void RecordHashOfDeviceCookieUsedDuringSuccessfulLoginBackground(
            TAccount userAccount,
            string hashOfCookie,
            DateTime? whenSeenUtc = null);


        Task RecordHashOfDeviceCookieUsedDuringSuccessfulLoginAsync(
            TAccount userAccount,
            string hashOfCookie,
            DateTime? whenSeenUtc = null,
            CancellationToken cancellationToken = default(CancellationToken));


        Task<bool> HasClientWithThisHashedCookieSuccessfullyLoggedInBeforeAsync(
            TAccount userAccount,
            string hashOfCookie,
            CancellationToken cancellationToken = default(CancellationToken));


        Task<bool> AddIncorrectPhaseTwoHashAsync(
            TAccount userAccount,
            string phase2Hash,
            DateTime? whenSeenUtc = null,
            CancellationToken cancellationToken = default(CancellationToken));



        Task<double> TryGetCreditAsync(
            TAccount userAccount,
            double amountRequested,
            DateTime? timeOfRequestUtc = null,
            CancellationToken cancellationToken = default(CancellationToken));

    }
}

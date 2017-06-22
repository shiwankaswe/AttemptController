using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using AttemptController.DataStructures;
using AttemptController.Models;
using System.Text;
using System.Threading;
using AttemptController.EncryptionPrimitives;
using AttemptController.Interfaces;
using AttemptController.Utilities;


namespace AttemptController.Controllers
{
    public interface ILoginAttemptController
    {
        Task<LoginAttempt> PutAsync(LoginAttempt loginAttempt,
            string passwordProvidedByClient = null,
            CancellationToken cancellationToken = default(CancellationToken));
    }
    

    [Route("api/[controller]")]
    public class LoginAttemptController<TUserAccount> :
        Controller, 
        ILoginAttemptController where TUserAccount : IUserAccount
    {

        private readonly BlockingAlgorithmOptions _options;

        private readonly IBinomialLadderFilter _binomialLadderFilter;

        private readonly IUserAccountRepositoryFactory<TUserAccount> _userAccountRepositoryFactory;

        private readonly IUserAccountControllerFactory<TUserAccount> _userAccountControllerFactory;

        private readonly AgingMembershipSketch _recentIncorrectPasswords;


        private readonly SelfLoadingCache<IPAddress, IpHistory> _ipHistoryCache;


        public LoginAttemptController(
            IUserAccountControllerFactory<TUserAccount> userAccountControllerFactory,
            IUserAccountRepositoryFactory<TUserAccount> userAccountRepositoryFactory,
            IBinomialLadderFilter binomialLadderFilter,
            MemoryUsageLimiter memoryUsageLimiter,
            BlockingAlgorithmOptions blockingOptions
            )
        {
            _options = blockingOptions;
            _binomialLadderFilter = binomialLadderFilter;
            _userAccountRepositoryFactory = userAccountRepositoryFactory;
            _userAccountControllerFactory = userAccountControllerFactory;


            _recentIncorrectPasswords = new AgingMembershipSketch(blockingOptions.AgingMembershipSketchTables, blockingOptions.AgingMembershipSketchTableSize);

            _ipHistoryCache = new SelfLoadingCache<IPAddress, IpHistory>(address => new IpHistory(address, _options));

            memoryUsageLimiter.OnReduceMemoryUsageEventHandler += ReduceMemoryUsage;
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> PutAsync([FromRoute] string id,
            [FromBody] LoginAttempt loginAttempt,
            [FromBody] string passwordProvidedByClient = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (id != loginAttempt.UniqueKey)
            {
                throw new Exception("The id assigned to the login does not match it's unique key.");
            }

            return new ObjectResult(await PutAsync(loginAttempt,
                passwordProvidedByClient,
                cancellationToken: cancellationToken));
        }


        public async Task<LoginAttempt> PutAsync(
            LoginAttempt loginAttempt,
            string passwordProvidedByClient = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await DetermineLoginAttemptOutcomeAsync(
                loginAttempt,
                passwordProvidedByClient,
                cancellationToken: cancellationToken);
        }


        public async Task PrimeCommonPasswordAsync(string passwordToTreatAsFrequent,
            int numberOfSteps,
            CancellationToken cancellationToken = default(CancellationToken))
        {   
            for (int i = 0; i < numberOfSteps; i++)
            {
                await _binomialLadderFilter.StepAsync(passwordToTreatAsFrequent, cancellationToken: cancellationToken);
            }
        }
        

        [HttpDelete("{id}")]
        public IActionResult Delete(string id)
        {
            return new NotFoundResult();
        }



        protected void AdjustBlockingScoreForPastTyposTreatedAsFullFailures(
            IpHistory clientsIpHistory,
            IUserAccountController<TUserAccount> accountController,
            TUserAccount account,
            DateTime whenUtc,
            string correctPassword,
            byte[] phase1HashOfCorrectPassword)
        {
            double credit = 0d;

            if (clientsIpHistory == null)
                return;
        
            LoginAttemptSummaryForTypoAnalysis[] recentPotentialTypos = clientsIpHistory.RecentPotentialTypos.MostRecentFirst.ToArray();
            Encryption.IPrivateKey privateAccountLogKey = null;
            try
            {
                foreach (LoginAttemptSummaryForTypoAnalysis potentialTypo in recentPotentialTypos)
                {
                    bool usernameCorrect = potentialTypo.UsernameOrAccountId == account.UsernameOrAccountId;

                    if (usernameCorrect) 
                    {
                        string passwordSubmittedInFailedAttempt = null;
                        if (account.GetType().Name == "SimulatedUserAccount")
                        {
                            passwordSubmittedInFailedAttempt = potentialTypo.EncryptedIncorrectPassword.Ciphertext;
                        }
                        else
                        {
                            if (privateAccountLogKey == null)
                            {
                                try
                                {
                                    privateAccountLogKey = accountController.DecryptPrivateAccountLogKey(account,
                                        phase1HashOfCorrectPassword);
                                }
                                catch (Exception)
                                {                           
                                    return;
                                }
                            }
                            try
                            {
                                passwordSubmittedInFailedAttempt =
                                    potentialTypo.EncryptedIncorrectPassword.Read(privateAccountLogKey);
                            }
                            catch (Exception)
                            {

                            }
                        }

                        if (passwordSubmittedInFailedAttempt != null)
                        {
                            bool passwordHadTypo = 
                                EditDistance.Calculate(passwordSubmittedInFailedAttempt, correctPassword) <=
                                _options.MaxEditDistanceConsideredATypo;

                            if (passwordHadTypo)
                            {
                                credit += potentialTypo.Penalty.GetValue(_options.AccountCreditLimitHalfLife, whenUtc)*
                                          (1d - _options.PenaltyMulitiplierForTypo);
                            }
                        }
                    }

                    clientsIpHistory.RecentPotentialTypos.Remove(potentialTypo);
                }

                clientsIpHistory.CurrentBlockScore.SubtractInPlace(account.CreditHalfLife, credit, whenUtc);
            }
            finally
            {
                privateAccountLogKey?.Dispose();
            }
        }

        public async Task<LoginAttempt> DetermineLoginAttemptOutcomeAsync(
            LoginAttempt loginAttempt,
            string passwordProvidedByClient,
            byte[] phase1HashOfProvidedPassword = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {

            Task<IpHistory> ipHistoryGetTask = _ipHistoryCache.GetAsync(loginAttempt.AddressOfClientInitiatingRequest,
                cancellationToken);

            IRepository<string, TUserAccount> userAccountRepository = _userAccountRepositoryFactory.Create();
            Task<TUserAccount> userAccountRequestTask = userAccountRepository.LoadAsync(loginAttempt.UsernameOrAccountId, cancellationToken);

            Task<int> passwordsHeightOnBinomialLadderTask =
                _binomialLadderFilter.GetHeightAsync(passwordProvidedByClient, cancellationToken: cancellationToken);


            IpHistory ip = await ipHistoryGetTask;


            TUserAccount account = await userAccountRequestTask;
            if (account != null)
            {
                try
                {
                    IUserAccountController<TUserAccount> userAccountController = _userAccountControllerFactory.Create();

                    loginAttempt.DeviceCookieHadPriorSuccessfulLoginForThisAccount = await
                        userAccountController.HasClientWithThisHashedCookieSuccessfullyLoggedInBeforeAsync(
                            account,
                            loginAttempt.HashOfCookieProvidedByBrowser,
                            cancellationToken);

                    if (phase1HashOfProvidedPassword == null)
                    {
                        phase1HashOfProvidedPassword =
                            userAccountController.ComputePhase1Hash(account, passwordProvidedByClient);
                    }


                    string phase2HashOfProvidedPassword =
                            userAccountController.ComputePhase2HashFromPhase1Hash(account, phase1HashOfProvidedPassword);
 
                    bool isSubmittedPasswordCorrect = phase2HashOfProvidedPassword == account.PasswordHashPhase2;


                    loginAttempt.PasswordsHeightOnBinomialLadder = await passwordsHeightOnBinomialLadderTask;

                    if (isSubmittedPasswordCorrect)
                    {

                        AdjustBlockingScoreForPastTyposTreatedAsFullFailures(
                            ip, userAccountController, account, loginAttempt.TimeOfAttemptUtc, passwordProvidedByClient,
                            phase1HashOfProvidedPassword);

                        double blockingThreshold = _options.BlockThresholdPopularPassword_T_base*
                                                   _options.PopularityBasedThresholdMultiplier_T_multiplier(
                                                       loginAttempt);
                        double blockScore = ip.CurrentBlockScore.GetValue(_options.BlockScoreHalfLife,
                            loginAttempt.TimeOfAttemptUtc);

                        if (loginAttempt.DeviceCookieHadPriorSuccessfulLoginForThisAccount)
                            blockScore *= _options.MultiplierIfClientCookieIndicatesPriorSuccessfulLogin_Kappa;

                        if (blockScore > blockingThreshold)
                        {

                            loginAttempt.Outcome = AuthenticationOutcome.CredentialsValidButBlocked;
                        }
                        else
                        {

                            loginAttempt.Outcome = AuthenticationOutcome.CredentialsValid;
                            userAccountController.RecordHashOfDeviceCookieUsedDuringSuccessfulLoginBackground(
                                account,
                                loginAttempt.HashOfCookieProvidedByBrowser);

                            if (
                                ip.CurrentBlockScore.GetValue(_options.AccountCreditLimitHalfLife,
                                    loginAttempt.TimeOfAttemptUtc) > 0)
                            {

                                TaskHelper.RunInBackground(Task.Run( async () =>
                                {
                                    double credit = await userAccountController.TryGetCreditAsync(
                                        account,
                                        _options.RewardForCorrectPasswordPerAccount_Sigma,
                                        loginAttempt.TimeOfAttemptUtc, cancellationToken);
                                    ip.CurrentBlockScore.SubtractInPlace(_options.AccountCreditLimitHalfLife, credit,
                                        loginAttempt.TimeOfAttemptUtc);
                                }, cancellationToken));
                            }
                        }

                    }
                    else
                    {

                        loginAttempt.Phase2HashOfIncorrectPassword = phase2HashOfProvidedPassword;
                        if (account.GetType().Name == "SimulatedUserAccount")
                        {
                            loginAttempt.EncryptedIncorrectPassword.Ciphertext = passwordProvidedByClient;
                        }
                        else
                        {
                            loginAttempt.EncryptedIncorrectPassword.Write(passwordProvidedByClient,
                                account.EcPublicAccountLogKey);
                        }

                        if (await userAccountController.AddIncorrectPhaseTwoHashAsync(account, phase2HashOfProvidedPassword, cancellationToken: cancellationToken))
                        {

                            loginAttempt.Outcome = AuthenticationOutcome.CredentialsInvalidRepeatedIncorrectPassword;
                        }
                        else
                        {

                            loginAttempt.Outcome = AuthenticationOutcome.CredentialsInvalidIncorrectPassword;


                            double invalidPasswordPenalty = _options.PenaltyForInvalidPassword_Beta*
                                                            _options.PopularityBasedPenaltyMultiplier_phi(
                                                                loginAttempt);
                            ip.CurrentBlockScore.AddInPlace(_options.AccountCreditLimitHalfLife,
                                invalidPasswordPenalty,
                                loginAttempt.TimeOfAttemptUtc);

                            ip.RecentPotentialTypos.Add(new LoginAttemptSummaryForTypoAnalysis()
                            {
                                EncryptedIncorrectPassword = loginAttempt.EncryptedIncorrectPassword,
                                Penalty = new DecayingDouble(invalidPasswordPenalty, loginAttempt.TimeOfAttemptUtc),
                                UsernameOrAccountId = loginAttempt.UsernameOrAccountId
                            });
                        }

                    }
                }
                finally
                {

                    TaskHelper.RunInBackground(userAccountRepository.SaveChangesAsync(cancellationToken));
                }
            }
            else
            {

                if (phase1HashOfProvidedPassword == null)
                {
                    phase1HashOfProvidedPassword =
                        ExpensiveHashFunctionFactory.Get(_options.DefaultExpensiveHashingFunction)(
                            passwordProvidedByClient,
                            ManagedSHA256.Hash(Encoding.UTF8.GetBytes(loginAttempt.UsernameOrAccountId)),
                            _options.ExpensiveHashingFunctionIterations);
                }

                loginAttempt.PasswordsHeightOnBinomialLadder = await passwordsHeightOnBinomialLadderTask;



                if (_recentIncorrectPasswords.AddMember(Convert.ToBase64String(phase1HashOfProvidedPassword)))
                {
                    loginAttempt.Outcome = AuthenticationOutcome.CredentialsInvalidRepeatedNoSuchAccount;
                }
                else
                {
                    loginAttempt.Outcome = AuthenticationOutcome.CredentialsInvalidNoSuchAccount;
                    double invalidAccontPenalty = _options.PenaltyForInvalidAccount_Alpha*
                                                  _options.PopularityBasedPenaltyMultiplier_phi(loginAttempt);
                    ip.CurrentBlockScore.AddInPlace(_options.BlockScoreHalfLife, invalidAccontPenalty,
                        loginAttempt.TimeOfAttemptUtc);
                }
            }


            if (loginAttempt.Outcome == AuthenticationOutcome.CredentialsInvalidNoSuchAccount ||
                loginAttempt.Outcome == AuthenticationOutcome.CredentialsInvalidIncorrectPassword)
            {
                TaskHelper.RunInBackground(_binomialLadderFilter.StepAsync(passwordProvidedByClient, cancellationToken: cancellationToken));
            }

            return loginAttempt;
        }

        public void ReduceMemoryUsage(object sender, MemoryUsageLimiter.ReduceMemoryUsageEventParameters parameters)
        {
            _ipHistoryCache.RecoverSpace(parameters.FractionOfMemoryToTryToRemove);
        }
    }
}

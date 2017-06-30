using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AttemptController.AccountStorage.Memory;
using AttemptController.Controllers;
using AttemptController.DataStructures;
using AttemptController.Interfaces;
using AttemptController.Models;
using Xunit;

namespace UnitTests
{
    public class TestConfiguration
    {
        public IDistributedResponsibilitySet<RemoteHost> MyResponsibleHosts;
        public BlockingAlgorithmOptions MyBlockingAlgorithmOptions;
        public MemoryUserAccountController MemUserAccountController;
        public MemoryOnlyUserAccountFactory MyAccountFactory;
        public ILoginAttemptController MyLoginAttemptClient;
    }

    public class FunctionalTests
    {
        public static TestConfiguration InitTest(BlockingAlgorithmOptions options = default(BlockingAlgorithmOptions))
        {
            if (options == null)
                options = new BlockingAlgorithmOptions();

            TestConfiguration configuration = new TestConfiguration();
            configuration.MyBlockingAlgorithmOptions = options ?? new BlockingAlgorithmOptions();

            MemoryUsageLimiter memoryUsageLimiter = new MemoryUsageLimiter();

            BinomialLadderFilter localPasswordBinomialLadderFilter =
            new BinomialLadderFilter(options.NumberOfBitsInBinomialLadderFilter_N, options.HeightOfBinomialLadder_H);

            configuration.MyAccountFactory = new MemoryOnlyUserAccountFactory();
            configuration.MemUserAccountController = new MemoryUserAccountController();

            LoginAttemptController<MemoryUserAccount> myLoginAttemptController = 
                new LoginAttemptController<MemoryUserAccount>(
                new MemoryUserAccountControllerFactory(),
                configuration.MyAccountFactory,
                localPasswordBinomialLadderFilter,
                memoryUsageLimiter, configuration.MyBlockingAlgorithmOptions);

            configuration.MyLoginAttemptClient = myLoginAttemptController;

            return configuration;
        }

        public static IUserAccount CreateTestAccount(TestConfiguration configuration, string usernameOrAccountId, string password,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            MemoryUserAccount account = configuration.MemUserAccountController.Create(usernameOrAccountId, password, 1);
            account.CreditLimit = configuration.MyBlockingAlgorithmOptions.AccountCreditLimit;
            account.CreditHalfLife = configuration.MyBlockingAlgorithmOptions.AccountCreditLimitHalfLife;

            configuration.MyAccountFactory.Add(account);
            return account;
        }

        public static string[] CreateUserAccounts(TestConfiguration configuration, int numberOfAccounts)
        {
            string[] usernames = Enumerable.Range(1, numberOfAccounts).Select(x => "testuser" + x.ToString()).ToArray();
            Parallel.ForEach(usernames,
                (username) => CreateTestAccount(configuration, username, "passwordfor" + username)
                );


            return usernames;
        }

        public async Task<LoginAttempt> AuthenticateAsync(TestConfiguration configuration, string username, string password,
            IPAddress clientAddress = null,
            IPAddress serverAddress = null,
            string api = "web",
            string cookieProvidedByBrowser = null,
            DateTime? eventTimeUtc = null,
            CancellationToken cancellationToken = default(CancellationToken)
            )
        {
            clientAddress = clientAddress ?? new IPAddress(new byte[] {42, 42, 42, 42});
            serverAddress = serverAddress ?? new IPAddress(new byte[] {127, 1, 1, 1});


            LoginAttempt attempt = new LoginAttempt
            {
                UsernameOrAccountId = username,
                AddressOfClientInitiatingRequest = clientAddress,
                TimeOfAttemptUtc = eventTimeUtc ?? DateTime.UtcNow,
                Api = api,
                CookieProvidedByClient = cookieProvidedByBrowser
            };

            return await configuration.MyLoginAttemptClient.PutAsync(attempt, password,
                cancellationToken: cancellationToken);                
        }


        const string Username1 = "user1";
        const string Password1 = "123456";
        private const string PopularPassword = "p@ssword";
        protected IPAddress ClientsIp = new IPAddress(new byte[] { 42, 42, 42, 42 });
        protected IPAddress AttackersIp = new IPAddress(new byte[] { 66, 66, 66, 66 });
        protected IPAddress AnotherAttackersIp = new IPAddress(new byte[] { 166, 66, 66, 66 });

        [Fact]
        public async Task LoginTestTryCorrectPassword()
        {
            TestConfiguration configuration = InitTest();

            CreateTestAccount(configuration, Username1, Password1);

            LoginAttempt attempt = await AuthenticateAsync(configuration, Username1, Password1);
            
            Assert.Equal(AuthenticationOutcome.CredentialsValid, attempt.Outcome);
        }

        [Fact]
        public async Task LoginWithInvalidPassword()
        {
            TestConfiguration configuration = InitTest();
            CreateTestAccount(configuration, Username1, Password1);

            LoginAttempt attempt = await AuthenticateAsync(configuration, Username1, "wrong", cookieProvidedByBrowser: "GimmeCookie");

            Assert.Equal(AuthenticationOutcome.CredentialsInvalidIncorrectPassword, attempt.Outcome);

            LoginAttempt secondAttempt = await AuthenticateAsync(configuration, Username1, "wrong", cookieProvidedByBrowser: "GimmeCookie");

            Assert.Equal(AuthenticationOutcome.CredentialsInvalidRepeatedIncorrectPassword, secondAttempt.Outcome);            
        }

        [Fact]
        public async Task LoginWithInvalidAccount()
        {
            TestConfiguration configuration = InitTest();
            CreateTestAccount(configuration, Username1, Password1);
            
            LoginAttempt firstAttempt = await AuthenticateAsync(configuration,"KeyzerSoze", Password1, cookieProvidedByBrowser: "GimmeCookie");
            
            Assert.Equal(AuthenticationOutcome.CredentialsInvalidNoSuchAccount, firstAttempt.Outcome);

            LoginAttempt secondAttempt = await AuthenticateAsync(configuration, "KeyzerSoze", Password1, cookieProvidedByBrowser: "GimmeCookie");
            
            Assert.Equal(AuthenticationOutcome.CredentialsInvalidRepeatedNoSuchAccount, secondAttempt.Outcome);
        }

        [Fact]
        public async Task LoginWithIpWithBadReputationAsync()
        {
            TestConfiguration configuration = InitTest();
            string[] usernames = CreateUserAccounts(configuration, 200);
            CreateTestAccount(configuration, Username1, Password1);

            foreach (string username in usernames.Skip(10))
                await AuthenticateAsync(configuration, username, Password1, clientAddress: AttackersIp);

            LoginAttempt firstAttackersAttempt = await AuthenticateAsync(configuration, Username1, Password1, clientAddress: AttackersIp);

            Assert.Equal(AuthenticationOutcome.CredentialsValidButBlocked, firstAttackersAttempt.Outcome);

        }

        [Fact]
        public async Task LoginWithIpWithBadReputationParallelLoadAsync()
        {
            TestConfiguration configuration = InitTest();
            string[] usernames = CreateUserAccounts(configuration, 250);
            CreateTestAccount(configuration, Username1, Password1);

            await TaskParalllel.ForEachWithWorkers(usernames.Skip(20), async (username, itemNumber, cancelToken) =>
                await AuthenticateAsync(configuration, username, Password1, clientAddress: AttackersIp, cancellationToken: cancelToken));

            Thread.Sleep(2000);
            
            LoginAttempt firstAttackersAttempt = await AuthenticateAsync(configuration, Username1, Password1, clientAddress: AttackersIp);

            Assert.Equal(AuthenticationOutcome.CredentialsValidButBlocked, firstAttackersAttempt.Outcome);

            foreach (string username in usernames.Skip(1).Take(19))
                await AuthenticateAsync(configuration, username, Password1, AnotherAttackersIp);

            await AuthenticateAsync(configuration, usernames[0], Password1, AnotherAttackersIp);

            LoginAttempt anotherAttackersAttempt = await AuthenticateAsync(configuration, Username1, Password1, clientAddress: AnotherAttackersIp);

            Assert.Equal(AuthenticationOutcome.CredentialsValidButBlocked, anotherAttackersAttempt.Outcome);
        }


        [Fact]
        public async Task LoginWithIpWithMixedReputationAsync()
        {
            TestConfiguration configuration = InitTest();
            string[] usernames = CreateUserAccounts(configuration, 500);
            CreateTestAccount(configuration, Username1, Password1);

            foreach (string username in usernames.Skip(100))
                await AuthenticateAsync(configuration, username, PopularPassword, clientAddress: AttackersIp);

            bool shouldGuessPopular = true;
            foreach (string username in usernames.Take(50))
            {
                await AuthenticateAsync(configuration, username, shouldGuessPopular ? PopularPassword : "passwordfor" + username, ClientsIp);
                shouldGuessPopular = !shouldGuessPopular;
            }
            
            LoginAttempt anotherAttackersAttempt = await AuthenticateAsync(configuration, Username1, Password1, clientAddress: AnotherAttackersIp);
            Assert.Equal(AuthenticationOutcome.CredentialsValid, anotherAttackersAttempt.Outcome);
        }


    }
}

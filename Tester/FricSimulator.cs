using AttemptController.AccountStorage.Memory;
using AttemptController.Controllers;
using AttemptController.DataStructures;
using AttemptController.Interfaces;
using AttemptController.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tester
{
    public class TestConfiguration
    {
        public IDistributedResponsibilitySet<RemoteHost> MyResponsibleHosts;
        public BlockingAlgorithmOptions MyBlockingAlgorithmOptions;
        public MemoryUserAccountController MemUserAccountController;
        public MemoryOnlyUserAccountFactory MyAccountFactory;
        public ILoginAttemptController MyLoginAttemptClient;
    }

    public class FricSimulator
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
            clientAddress = clientAddress ?? new IPAddress(new byte[] { 42, 42, 42, 42 });
            serverAddress = serverAddress ?? new IPAddress(new byte[] { 127, 1, 1, 1 });


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
        const string Password1 = "alexandra";
        private const string PopularPassword = "p@ssword";
        protected IPAddress Ip1 = new IPAddress(new byte[] { 42, 42, 42, 42 });
        protected IPAddress Ip2 = new IPAddress(new byte[] { 66, 66, 66, 66 });
        protected IPAddress Ip3 = new IPAddress(new byte[] { 166, 66, 66, 66 });

        public async Task RunAsync(DebugLogger _logger, IpPool _ipPool)
        {

            TestConfiguration configuration = InitTest();

            _logger.WriteStatus("   "); _logger.WriteStatus("   "); _logger.WriteStatus("   ");


            _logger.WriteStatus("01)--------------------------------------------------------------------------");
            _logger.WriteStatus("Test With Correct User Account");
            CreateTestAccount(configuration, Username1, Password1);
            for (int i = 0; i < 1; i++)
            {
                LoginAttempt attempt = await AuthenticateAsync(configuration, Username1, Password1, Ip1);
                _logger.WriteStatus("Result - "+ attempt.Outcome);
            }
            _logger.WriteStatus("-----------------------------------------------------------------------------");

            _logger.WriteStatus("   ");
            _logger.WriteStatus("02)--------------------------------------------------------------------------");
            _logger.WriteStatus("Test With Correct User Account with multiple login attempts ");
            string[] usernames = CreateUserAccounts(configuration, 200);
            foreach (string username in usernames.Skip(10))
            {
                await AuthenticateAsync(configuration, username, Password1, clientAddress: Ip1);
            }
            for (int i = 0; i < 1; i++)
            {
                LoginAttempt attempt = await AuthenticateAsync(configuration, Username1, Password1, Ip1);
                _logger.WriteStatus("Result - " + attempt.Outcome);
            }
            _logger.WriteStatus("-----------------------------------------------------------------------------");

            _logger.WriteStatus("   ");
            _logger.WriteStatus("03)--------------------------------------------------------------------------");
            _logger.WriteStatus("Test With InCorrect Username ");
            for (int i = 0; i < 1; i++)
            {
                LoginAttempt attempt = await AuthenticateAsync(configuration, "Invalid Account", Password1, Ip2, cookieProvidedByBrowser: "GimmeCookie");
                _logger.WriteStatus("Result - " + attempt.Outcome);
            }
            _logger.WriteStatus("-----------------------------------------------------------------------------");

            _logger.WriteStatus("   ");
            _logger.WriteStatus("04)--------------------------------------------------------------------------");
            _logger.WriteStatus("Test With InCorrect Username Multiple Attempts ");
            for (int i = 0; i < 1; i++)
            {
                LoginAttempt attempt = await AuthenticateAsync(configuration, "Invalid Account", Password1, Ip2, cookieProvidedByBrowser: "GimmeCookie");
                _logger.WriteStatus("Result - " + attempt.Outcome);
            }
            _logger.WriteStatus("-----------------------------------------------------------------------------");

            _logger.WriteStatus("   ");
            _logger.WriteStatus("05)--------------------------------------------------------------------------");
            _logger.WriteStatus("Test With InCorrect Password ");
            for (int i = 0; i < 1; i++)
            {
                LoginAttempt attempt = await AuthenticateAsync(configuration, Username1, "Incorrect Password", Ip3, cookieProvidedByBrowser: "GimmeCookie");
                _logger.WriteStatus("Result - " + attempt.Outcome);
            }
            _logger.WriteStatus("-----------------------------------------------------------------------------");

            _logger.WriteStatus("   ");
            _logger.WriteStatus("06)--------------------------------------------------------------------------");
            _logger.WriteStatus("Test With InCorrect Password Multiple Attempts ");
            for (int i = 0; i < 1; i++)
            {
                LoginAttempt attempt = await AuthenticateAsync(configuration, Username1, "Incorrect Password", Ip3, cookieProvidedByBrowser: "GimmeCookie");
                _logger.WriteStatus("Result - " + attempt.Outcome);
            }
            _logger.WriteStatus("-----------------------------------------------------------------------------");



        }

    }
}

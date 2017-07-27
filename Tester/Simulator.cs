using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Formatters;
using AttemptController.AccountStorage.Memory;
using AttemptController.DataStructures;
using AttemptController.EncryptionPrimitives;
using AttemptController.Models;
using AttemptController.Utilities;
using System.Collections.Concurrent;

namespace Tester
{
    public partial class Simulator
    {
        public BinomialLadderFilter _binomialLadderFilter;
        public AgingMembershipSketch _recentIncorrectPasswords;
        public ConcurrentDictionary<IPAddress, SimIpHistory> _ipHistoryCache;
        public readonly ExperimentalConfiguration _experimentalConfiguration;
        public readonly SimulatedUserAccountController _userAccountController;
        public static TextWriter errorWriter;

        private readonly ConcurrentStreamWriter _Attempts;
        private readonly DebugLogger _logger;
        private readonly SimulatedPasswords _simPasswords;
        private IpPool _ipPool;
        private SimulatedAccounts _simAccounts;
        private SimulatedLoginAttemptGenerator _attemptGenerator;

        protected readonly DateTime StartTimeUtc = new DateTime(2016, 01, 01, 0, 0, 0, DateTimeKind.Utc);

        ConcurrentDictionary<string, DecayingDouble> _incorrectPasswordCounts = new ConcurrentDictionary<string, DecayingDouble>();


        public delegate void ExperimentalConfigurationFunction(ExperimentalConfiguration config);

        private static string Fraction(ulong numerator, ulong denominmator)
        {
            if (denominmator == 0)
                return "NaN";
            else
                return (((double)numerator) / (double)denominmator).ToString(CultureInfo.InvariantCulture);
        }


        public static void RunExperimentalSweep(ExperimentalConfiguration[] configurations)
        {
            foreach (ExperimentalConfiguration config in configurations)
            {
                DateTime now = DateTime.Now;
                string dirName = config.OutputPath;// + config.OutputDirectoryName;
                Directory.CreateDirectory(dirName);
                string path = dirName;// + @"\";

                if (errorWriter == null)
                    errorWriter = (new StreamWriter(new FileStream(path + "error.txt", FileMode.OpenOrCreate, FileAccess.ReadWrite)));

                DebugLogger logger = new DebugLogger();

                try
                {
                    SimulatedPasswords simPasswords = new SimulatedPasswords(logger, config);
                    Simulator simulator = new Simulator(logger, path, config, simPasswords);
                    simulator.RunAsync();
                }
                catch (Exception e)
                {
                    lock (errorWriter)
                    {
                        while (e != null)
                        {
                            errorWriter.WriteLine(e.Message);
                            errorWriter.WriteLine(e.StackTrace);
                            errorWriter.WriteLine(e);
                            e = e.InnerException;
                        }
                        errorWriter.Flush();
                    }
                }
            }
        }

        public Simulator(DebugLogger logger, string path, ExperimentalConfiguration myExperimentalConfiguration, SimulatedPasswords simPasswords)
        {

            _simPasswords = simPasswords;
            _logger = logger;
            if (_Attempts == null)
                _Attempts = new ConcurrentStreamWriter(path + "Attempts.txt");

            _logger.WriteStatus("Entered Simulator constructor");
            _experimentalConfiguration = myExperimentalConfiguration;
            BlockingAlgorithmOptions options = _experimentalConfiguration.BlockingOptions;

            //_logger.WriteStatus("Creating binomial ladder");
            _binomialLadderFilter =
                new BinomialLadderFilter(options.NumberOfBitsInBinomialLadderFilter_N, options.HeightOfBinomialLadder_H);
            _ipHistoryCache = new ConcurrentDictionary<IPAddress, SimIpHistory>();
            _userAccountController = new SimulatedUserAccountController();

            _recentIncorrectPasswords = new AgingMembershipSketch(16, 128 * 1024);

            //_logger.WriteStatus("Exiting Simulator constructor");
        }


        public async Task RunAsync()
        {
            _logger.WriteStatus("In RunInBackground");

            //_logger.WriteStatus("Priming password-tracking with known common passwords");
            _simPasswords.PrimeWithKnownPasswordsAsync(_binomialLadderFilter, 40);
            //_logger.WriteStatus("Finished priming password-tracking with known common passwords");

            //_logger.WriteStatus("Creating IP Pool");
            _ipPool = new IpPool(_experimentalConfiguration);
            //_logger.WriteStatus("Generating simualted account records");
            _simAccounts = new SimulatedAccounts(_ipPool, _simPasswords, _logger);
            _simAccounts.Generate(_experimentalConfiguration);

            //_logger.WriteStatus("Creating login-attempt generator");
            _attemptGenerator = new SimulatedLoginAttemptGenerator(_experimentalConfiguration, _simAccounts, _ipPool,
                _simPasswords);
            _logger.WriteStatus("Finiished creating login-attempt generator");

            FricSimulator fri = new FricSimulator();
            _logger.WriteStatus("   ");
            _logger.WriteStatus("   ");
            _logger.WriteStatus("Click Enter To First Testing Step");
            _logger.WriteStatus("   ");
            _logger.WriteStatus("   ");
            Console.Read();

            await fri.RunAsync(_logger, _ipPool);

            _logger.WriteStatus("   ");
            _logger.WriteStatus("   ");
            _logger.WriteStatus("Click Enter To Second Testing Step");
            _logger.WriteStatus("   ");
            _logger.WriteStatus("   ");
            Console.Read();
            Console.Read();

            _logger.WriteStatus("Running Password File to check");
            _logger.WriteStatus("   ");

            foreach (
                ConcurrentStreamWriter writer in
                    new[]
                    {_Attempts})
            {
                lock (writer)
                {

                    writer.WriteLine(string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}",
                        "UserID",
                        "IP",
                        "IsFrequentlyGuessedPw",
                        "IsPasswordCorrect",
                        "IsFromAttackAttacker",
                        "IsAGuess"
                        ));
                }
            }

            TimeSpan testTimeSpan = _experimentalConfiguration.TestTimeSpan;
            double ticksBetweenLogins = ((double)testTimeSpan.Ticks) /
                                        (double)_experimentalConfiguration.TotalLoginAttemptsToIssue;
            _experimentalConfiguration.TotalLoginAttemptsToIssue = 30000;
            int interlockedCount = 0;

            Parallel.For(0L, (long)_experimentalConfiguration.TotalLoginAttemptsToIssue, (count, pls) =>
           {
               interlockedCount = Interlocked.Add(ref interlockedCount, 1);
               if (interlockedCount % 10000 == 0)
                   _logger.WriteStatus("Login Attempt {0:N0}", interlockedCount);
               DateTime eventTimeUtc = StartTimeUtc.AddTicks((long)(ticksBetweenLogins * interlockedCount));
               SimulatedLoginAttempt simAttempt;
               if (StrongRandomNumberGenerator.GetFraction() <
                   _experimentalConfiguration.FractionOfLoginAttemptsFromAttacker)
               {
                   switch (_experimentalConfiguration.AttackersStrategy)
                   {
                       case ExperimentalConfiguration.AttackStrategy.UseUntilLikelyPopular:
                           simAttempt =
                               _attemptGenerator.MaliciousLoginAttemptBreadthFirstAvoidMakingPopular(eventTimeUtc);
                           break;
                       case ExperimentalConfiguration.AttackStrategy.Weighted:
                           simAttempt = _attemptGenerator.MaliciousLoginAttemptWeighted(eventTimeUtc);
                           break;
                       case ExperimentalConfiguration.AttackStrategy.BreadthFirst:
                       default:
                           simAttempt = _attemptGenerator.MaliciousLoginAttemptBreadthFirst(eventTimeUtc);
                           break;
                   }
               }
               else
               {
                   simAttempt = _attemptGenerator.BenignLoginAttempt(eventTimeUtc);
               }


               SimIpHistory ipHistory = _ipHistoryCache.GetOrAdd(simAttempt.AddressOfClientInitiatingRequest,
                   (ip) => new SimIpHistory(
                           _experimentalConfiguration.BlockingOptions
                               .NumberOfFailuresToTrackForGoingBackInTimeToIdentifyTypos));
               double[] scores = ipHistory.GetAllScores(_experimentalConfiguration.BlockingOptions.BlockScoreHalfLife,
                   simAttempt.TimeOfAttemptUtc);

               simAttempt.UpdateSimulatorState(this, ipHistory);

               double decayingInvalidPasswordAttempts = 0d;
               if (simAttempt.IsPasswordValid)
               {
                   DecayingDouble incorrectPasswordAttempts;
                   if (_incorrectPasswordCounts.TryGetValue(simAttempt.Password, out incorrectPasswordAttempts))
                       decayingInvalidPasswordAttempts = incorrectPasswordAttempts.GetValue(_experimentalConfiguration.BlockingOptions.BlockScoreHalfLife, simAttempt.TimeOfAttemptUtc);
               }
               else
               {
                   decayingInvalidPasswordAttempts = 1d;
                   DecayingDouble incorrectPasswordAttempts;
                   if (_incorrectPasswordCounts.TryGetValue(simAttempt.Password, out incorrectPasswordAttempts))
                       decayingInvalidPasswordAttempts += incorrectPasswordAttempts.GetValue(_experimentalConfiguration.BlockingOptions.BlockScoreHalfLife, simAttempt.TimeOfAttemptUtc);
                   _incorrectPasswordCounts[simAttempt.Password] = new DecayingDouble(decayingInvalidPasswordAttempts, simAttempt.TimeOfAttemptUtc);
               }

               var ipInfo = _ipPool.GetIpAddressDebugInfo(simAttempt.AddressOfClientInitiatingRequest);
               string outputString = string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}",
                   simAttempt.SimAccount?.UsernameOrAccountId ?? "<null>",
                   simAttempt.AddressOfClientInitiatingRequest,
                   simAttempt.IsFrequentlyGuessedPassword ? "Frequent" : "Infrequent",
                   simAttempt.IsPasswordValid ? "Correct" : "Incorrect",
                   simAttempt.IsFromAttacker ? "FromAttacker" : "FromUser",
                   simAttempt.IsGuess ? "IsGuess" : "NotGuess",
                   simAttempt.IsFromAttacker
                       ? (ipInfo.UsedByBenignUsers ? "IsInBenignPool" : "NotUsedByBenign")
                       : (ipInfo.UsedByAttackers ? "IsInAttackersIpPool" : "NotUsedByAttacker"),
                   ipInfo.IsPartOfProxy ? "ProxyIP" : "NotAProxy",
                   string.IsNullOrEmpty(simAttempt.MistakeType) ? "-" : simAttempt.MistakeType,
                   decayingInvalidPasswordAttempts,
                   simAttempt.SimAccount?.MaxConsecutiveIncorrectAttempts.GetValue(_experimentalConfiguration.BlockingOptions.BlockScoreHalfLife, simAttempt.TimeOfAttemptUtc) ?? 0d,
                   string.Join("\t", scores.Select(s => s.ToString(CultureInfo.InvariantCulture)).ToArray())
                   );


               _Attempts.WriteLine(outputString);
               _logger.WriteStatus(outputString);
               Thread.Sleep(1300);

           });
            foreach (
                ConcurrentStreamWriter writer in
                    new[]
                    {_Attempts})
            {
                writer.Close();
            }
        }

    }
}

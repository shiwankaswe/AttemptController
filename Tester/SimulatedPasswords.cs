using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AttemptController.Controllers;
using AttemptController.DataStructures;
using AttemptController.EncryptionPrimitives;

namespace Tester
{
    public class SimulatedPasswords
    {
        private WeightedSelector<string> _passwordSelector;
        private List<string> _passwordsAlreadyKnownToBePopular;
        public List<string> OrderedListOfMostCommonPasswords;
        private WeightedSelector<string> _commonPasswordSelector;
        private DebugLogger _logger;

        public SimulatedPasswords(DebugLogger logger, ExperimentalConfiguration config)
        {
            _logger = logger;
            _logger.WriteStatus("Configuring...");
            LoadPasswordSelector(config.PasswordFrequencyFile);
            if (config.PopularPasswordsToRemoveFromDistribution > 0)
            {
                _passwordSelector = _passwordSelector.TrimToRemoveInitialItems(config.PopularPasswordsToRemoveFromDistribution);
            }

            //_logger.WriteStatus("Loading passwords known to be common by the algorithm before the attack");
            LoadKnownPopularPasswords(config.PreviouslyKnownPopularPasswordFile);
           // _logger.WriteStatus("Creating common password selector");
            _commonPasswordSelector = _passwordSelector.TrimToInitialItems(
                    (int)config.NumberOfPopularPasswordsForAttackerToExploit);
           // _logger.WriteStatus("Finished creating common password selector");

           // _logger.WriteStatus("Creating list of most common passwords");
            OrderedListOfMostCommonPasswords =
                _passwordSelector.GetItems();
            //_logger.WriteStatus("Finished creating list of most common passwords");
        }


        public string GetPasswordFromWeightedDistribution()
        {
            return _passwordSelector.GetItemByWeightedRandom();
        }

        public void LoadKnownPopularPasswords(string pathToPreviouslyKnownPopularPasswordFile)
        {
            _passwordsAlreadyKnownToBePopular = new List<string>();
            using (System.IO.StreamReader file =
                new System.IO.StreamReader(new FileStream(pathToPreviouslyKnownPopularPasswordFile, FileMode.Open, FileAccess.Read)))
            {

                string line;
                while ((line = file.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (!string.IsNullOrEmpty(line))
                        _passwordsAlreadyKnownToBePopular.Add(line);
                }
            }
        }

        public void PrimeWithKnownPasswordsAsync(BinomialLadderFilter freqFilter, int numberOfTimesToPrime)
        {

            for (int i = 0; i < numberOfTimesToPrime; i++)
            {
                Parallel.ForEach(_passwordsAlreadyKnownToBePopular,
                    (password) => freqFilter.Step(password));
            }
        }

        private void LoadPasswordSelector(string pathToWeightedFrequencyFile)
        {
            _passwordSelector = new WeightedSelector<string>();
            using (System.IO.StreamReader file =
                new System.IO.StreamReader(new FileStream(pathToWeightedFrequencyFile, FileMode.Open, FileAccess.Read)))
            {
                string lineWithCountFollowedBySpaceFollowedByPassword;
                while ((lineWithCountFollowedBySpaceFollowedByPassword = file.ReadLine()) != null)
                {
                    lineWithCountFollowedBySpaceFollowedByPassword =
                        lineWithCountFollowedBySpaceFollowedByPassword.Trim();
                    int indexOfFirstSpace = lineWithCountFollowedBySpaceFollowedByPassword.IndexOf(' ');
                    if (indexOfFirstSpace < 0 ||
                        indexOfFirstSpace + 1 >= lineWithCountFollowedBySpaceFollowedByPassword.Length)
                        continue;                
                    string countAsString = lineWithCountFollowedBySpaceFollowedByPassword.Substring(0, indexOfFirstSpace);
                    ulong count;
                    if (!ulong.TryParse(countAsString, out count))
                        continue;              
                    string password = lineWithCountFollowedBySpaceFollowedByPassword.Substring(indexOfFirstSpace + 1);
                    _passwordSelector.AddItem(password, count);
                }
            }
        }



    }
}

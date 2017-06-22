using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AttemptController.DataStructures;
using AttemptController.Models;

namespace Tester
{

    public class SimLoginAttemptSummaryForTypoAnalysis
    {
        public DateTime WhenUtc { get; set; }

        public string UsernameOrAccountId { get; set; }

        public bool WasPasswordFrequent { get; set; }

        public string Password { get; set; }
    }

    public class SimIpHistory
    {
        public DecayingDouble SuccessfulLogins;

        public DecayingDouble AccountFailuresInfrequentPassword;
        public DecayingDouble AccountFailuresFrequentPassword;
        public DecayingDouble RepeatAccountFailuresInfrequentPassword;
        public DecayingDouble RepeatAccountFailuresFrequentPassword;

        public DecayingDouble PasswordFailuresNoTypoInfrequentPassword;
        public DecayingDouble PasswordFailuresNoTypoFrequentPassword;
        public DecayingDouble PasswordFailuresTypoInfrequentPassword;
        public DecayingDouble PasswordFailuresTypoFrequentPassword;
        public DecayingDouble RepeatPasswordFailuresNoTypoInfrequentPassword;
        public DecayingDouble RepeatPasswordFailuresNoTypoFrequentPassword;
        public DecayingDouble RepeatPasswordFailuresTypoInfrequentPassword;
        public DecayingDouble RepeatPasswordFailuresTypoFrequentPassword;

        public double[] GetAllScores(TimeSpan halfLife, DateTime whenUtc)
        {
            return new double[]
            {
                SuccessfulLogins.GetValue(halfLife, whenUtc),
                AccountFailuresInfrequentPassword.GetValue(halfLife, whenUtc),
                AccountFailuresFrequentPassword.GetValue(halfLife, whenUtc),
                RepeatAccountFailuresInfrequentPassword.GetValue(halfLife, whenUtc),
                RepeatAccountFailuresFrequentPassword.GetValue(halfLife, whenUtc),
                PasswordFailuresNoTypoInfrequentPassword.GetValue(halfLife, whenUtc),
                PasswordFailuresNoTypoFrequentPassword.GetValue(halfLife, whenUtc),
                PasswordFailuresTypoInfrequentPassword.GetValue(halfLife, whenUtc),
                PasswordFailuresTypoFrequentPassword.GetValue(halfLife, whenUtc),
                RepeatPasswordFailuresNoTypoInfrequentPassword.GetValue(halfLife, whenUtc),
                RepeatPasswordFailuresNoTypoFrequentPassword.GetValue(halfLife, whenUtc),
                RepeatPasswordFailuresTypoInfrequentPassword.GetValue(halfLife, whenUtc),
                RepeatPasswordFailuresTypoFrequentPassword.GetValue(halfLife, whenUtc)
            };
        }

        public SmallCapacityConstrainedSet<SimLoginAttemptSummaryForTypoAnalysis> RecentPotentialTypos;

        public SimIpHistory(int numberOfPastLoginsToKeepForTypoAnalysis)
        {
            RecentPotentialTypos =
                new SmallCapacityConstrainedSet<SimLoginAttemptSummaryForTypoAnalysis>(
                    numberOfPastLoginsToKeepForTypoAnalysis);
        }

        public void AdjustBlockingScoreForPastTyposTreatedAsFullFailures(
            Simulator simulator,
            SimulatedUserAccount account,
            DateTime whenUtc,
            string correctPassword)
        {
            SimLoginAttemptSummaryForTypoAnalysis[] recentPotentialTypos =
                RecentPotentialTypos.MostRecentFirst.ToArray();
            foreach (SimLoginAttemptSummaryForTypoAnalysis potentialTypo in recentPotentialTypos)
            {
                if (account == null || potentialTypo.UsernameOrAccountId != account.UsernameOrAccountId)
                    continue;

                bool likelyTypo =
                    EditDistance.Calculate(potentialTypo.Password, correctPassword) <=
                    simulator._experimentalConfiguration.BlockingOptions.MaxEditDistanceConsideredATypo;

                TimeSpan halfLife = simulator._experimentalConfiguration.BlockingOptions.BlockScoreHalfLife;
                DecayingDouble value = new DecayingDouble(1d, potentialTypo.WhenUtc);
                if (potentialTypo.WasPasswordFrequent)
                {
                    PasswordFailuresNoTypoFrequentPassword.SubtractInPlace(halfLife, value);
                    PasswordFailuresTypoFrequentPassword.AddInPlace(halfLife, value);
                }
                RecentPotentialTypos.Remove(potentialTypo);
            }
        }

    }
}

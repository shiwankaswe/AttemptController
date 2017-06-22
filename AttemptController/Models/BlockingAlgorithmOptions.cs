using System;
using System.Collections.Generic;
using System.Linq;
using AttemptController.DataStructures;
using AttemptController.EncryptionPrimitives;


namespace AttemptController.Models
{

    public class ThresholdAndValue
    {
        public double Threshold { get; set; }
        public double Value { get; set; }

        public static double EvaluteAbove(IEnumerable<ThresholdAndValue> thresholdValuePairs, double point,  double defaultResult)
        {
            return thresholdValuePairs.Where(tvp => point >= tvp.Threshold).Select(thresholdValuePair => thresholdValuePair.Value).Concat(new[] {defaultResult}).Max();
        }

        public static double EvaluteBelow(IEnumerable<ThresholdAndValue> thresholdValuePairs, double point, double defaultResult)
        {
            return thresholdValuePairs.Where(tvp => point <= tvp.Threshold).Select(thresholdValuePair => thresholdValuePair.Value).Concat(new[] { defaultResult }).Max();
        }

    }

    public delegate double PasswordPopularityFunction(LoginAttempt attempt);

    public class BlockingAlgorithmOptions
    {
        public int NumberOfRedundantHostsToCacheIPs = 1;
        public int NumberOfRedundantHostsToCachePasswordPopularity = 1;
        public int HeightOfBinomialLadder_H = 48;
        public int BinomialLadderFrequencyThreshdold_T = 44;
        public int NumberOfBitsInBinomialLadderFilter_N = 1 << 29;
        public int NumberOfVirtualNodesForDistributedBinomialLadder = 1 << 10;

        public TimeSpan MinimumBinomialLadderFilterCacheFreshness = new TimeSpan(0,5,0);   

        public string PrivateConfigurationKey = "ShiwankaChathurangaBionomialLadderFilterProcess";

        public int NumberOfBitsPerShardInBinomialLadderFilter
            => NumberOfBitsInBinomialLadderFilter_N/NumberOfVirtualNodesForDistributedBinomialLadder;

        public int NumberOfFailuresToTrackForGoingBackInTimeToIdentifyTypos = 8;

        public float MaxEditDistanceConsideredATypo { get; set; } = 2f;
        public double PenaltyMulitiplierForTypo { get; set; } = .1d;
        public double PenaltyForInvalidPassword_Beta { get; set; } = 1d;
        public double PenaltyForInvalidAccount_Alpha { get; set; } = 2d;    

        public double RewardForCorrectPasswordPerAccount_Sigma { get; set; } = 30d;

        public double BlockThresholdPopularPassword_T_base { get; set; } = 50d;

        public double MultiplierIfClientCookieIndicatesPriorSuccessfulLogin_Kappa = 0d;

        public TimeSpan BlockScoreHalfLife = new TimeSpan(12,0,0);   

        public uint AgingMembershipSketchTables = 16;
        public uint AgingMembershipSketchTableSize = 128*1024;


        public uint LengthOfShortestPopularityMeasurementPeriod = 10 * 1000;
        public  uint FactorOfGrowthBetweenPopularityMeasurementPeriods = 10;
        public  int NumberOfPopularityMeasurementPeriods = 4;

        public string DefaultExpensiveHashingFunction { get; set; } = ExpensiveHashFunctionFactory.DefaultFunctionName;
        public int ExpensiveHashingFunctionIterations { get; set; } = ExpensiveHashFunctionFactory.DefaultNumberOfIterations;

        public double AccountCreditLimit { get; set; } = 50d;
        public TimeSpan AccountCreditLimitHalfLife = new TimeSpan(12,0,0);   

        public double PopularityConfidenceLevel { get; set; } = 0.001d;    

        public double PhiIfFrequent = 5;
        public double PopularityBasedPenaltyMultiplier_phi(LoginAttempt loginAttempt)
        {
            return loginAttempt.PasswordsHeightOnBinomialLadder >= BinomialLadderFrequencyThreshdold_T ? 5 : 1;
        }


        public double PopularityBasedThresholdMultiplier_T_multiplier(LoginAttempt loginAttempt)
        {
            return loginAttempt.PasswordsHeightOnBinomialLadder >= BinomialLadderFrequencyThreshdold_T ? 1 : 100;
        }
       
    }
}

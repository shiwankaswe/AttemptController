using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AttemptController.DataStructures
{
    public interface IUpdatableFrequency
    {
        Proportion Proportion { get; }

        Task RecordObservationAsync(
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default(CancellationToken));
    }

    public interface IFrequenciesProvider<in TKey>
    {
        Task<IUpdatableFrequency> GetFrequencyAsync(
            TKey key,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default(CancellationToken));
    }

    public class MultiperiodFrequencyTracker<TKey> : IFrequenciesProvider<TKey>
    {
        protected List<FrequencyTracker<TKey>> PasswordFrequencyEstimatesForDifferentPeriods;

        public MultiperiodFrequencyTracker(int numberOfPopularityMeasurementPeriods,
            uint lengthOfShortestPopularityMeasurementPeriod,
            uint factorOfGrowthBetweenPopularityMeasurementPeriods
            )
        {
            PasswordFrequencyEstimatesForDifferentPeriods =
                new List<FrequencyTracker<TKey>>(numberOfPopularityMeasurementPeriods);
            uint currentPeriodLength = lengthOfShortestPopularityMeasurementPeriod;
            for (int period = 0; period < numberOfPopularityMeasurementPeriods; period++)
            {
                PasswordFrequencyEstimatesForDifferentPeriods.Add(
                    new FrequencyTracker<TKey>((int) currentPeriodLength));
                currentPeriodLength *= factorOfGrowthBetweenPopularityMeasurementPeriods;
            }
            PasswordFrequencyEstimatesForDifferentPeriods.Reverse();

        }

        public Proportion Get(TKey key)
        {
            return Proportion.GetLargest(
                PasswordFrequencyEstimatesForDifferentPeriods.Select(
                    (ft) => ft.Get(key)));
        }

        public void RecordObservation(TKey key)
        {
            foreach (FrequencyTracker<TKey> ft in PasswordFrequencyEstimatesForDifferentPeriods)
                ft.Observe(key);
        }

#pragma warning disable 1998
        public async Task<IUpdatableFrequency> GetFrequencyAsync(TKey key,
#pragma warning restore 1998
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new FrequencyTrackerResult(this, key, Get(key));
        }
    


    public class FrequencyTrackerResult : IUpdatableFrequency
        {
            protected MultiperiodFrequencyTracker<TKey> Tracker;
            protected TKey Key;

            public Proportion Proportion { get; protected set; }

#pragma warning disable CS1998          
            public async Task RecordObservationAsync(
#pragma warning restore CS1998          
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default(CancellationToken))
            {
                Tracker.RecordObservation(Key);
            }

            public FrequencyTrackerResult(MultiperiodFrequencyTracker<TKey> tracker, TKey key, IEnumerable<Proportion> proportions = null)
            {
                Tracker = tracker;
                Key = key;
                this.Proportion = new Proportion(0, ulong.MaxValue);
                if (proportions == null) return;
                foreach(Proportion proportion in proportions)
                    if (proportion.AsDouble > Proportion.AsDouble)
                        this.Proportion = proportion;
            }

            public FrequencyTrackerResult(MultiperiodFrequencyTracker<TKey> tracker, TKey key, Proportion proportion)
            {
                Tracker = tracker;
                Key = key;
                this.Proportion = proportion;
            }

        }

    }
}

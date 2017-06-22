using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace AttemptController.DataStructures
{

    public class FrequencyTracker<TKey>
    {
   
        public int MaxCapacity { get; }

        public int CapacityToTargetWhenRecoveringSpace { get; }

        public uint Generations { get; }

        private long _sumOfAmountsOverAllKeys;
        public long SumOfAmountsOverAllKeys {
            get { return _sumOfAmountsOverAllKeys;}
            private set {_sumOfAmountsOverAllKeys = value; } }

        private int _numberOfElements = 0;
        public int Count
        {
            get { return _numberOfElements; }
            private set { _numberOfElements = value; }
        }


        private readonly ConcurrentDictionary<TKey, uint> _keyCounts;

        protected long TotalCountMaxThreshold { get; }

        protected long ReducedAmountToTargetWhenTotalAmountReached { get; }

        private readonly int _periodBetweenIncrementGrowth;
        private int _capacityAtWhichToIncreaseTheIncrement;

        private uint _increment;

        private readonly object _recoveryTaskLock;
        private Task _recoveryTask;
        private Queue<TKey> _cleanupQueue;


        public FrequencyTracker(
            int approximateObservationLifetime,
            int? maxCapacity = null,
            double capacityToTargetWhenRecoveringSpace = 0.95f,
            uint generations = 4)
        {
            Generations = generations;
            TotalCountMaxThreshold = ((long)approximateObservationLifetime) * Generations;
            MaxCapacity = maxCapacity ?? approximateObservationLifetime;
            CapacityToTargetWhenRecoveringSpace = (int) (MaxCapacity*capacityToTargetWhenRecoveringSpace);
            ReducedAmountToTargetWhenTotalAmountReached = ((TotalCountMaxThreshold*95U)/100U);

            _increment = 1;
            _periodBetweenIncrementGrowth = approximateObservationLifetime / (int) Generations;
            _capacityAtWhichToIncreaseTheIncrement = _periodBetweenIncrementGrowth;

            _keyCounts = new ConcurrentDictionary<TKey, uint>();

            _cleanupQueue = new Queue<TKey>();
            _recoveryTaskLock = new object();
        }

        public Proportion Get(TKey key)
        {
            uint count;
            _keyCounts.TryGetValue(key, out count);
            return new Proportion(count, (ulong) SumOfAmountsOverAllKeys);
        }


        private readonly object _capacityLockObj = new object();
        public Proportion Observe(TKey key)
        {
            uint increment = _increment;
            uint countForThisKeyAfter = _keyCounts.AddOrUpdate(key, (k) => increment, (k, priorValue) => priorValue + increment);
            uint countForThisKeyBefore = countForThisKeyAfter - increment;
            if (countForThisKeyBefore > 0)
                Interlocked.Add(ref _numberOfElements, 1);
            Interlocked.Add(ref _sumOfAmountsOverAllKeys, _increment);

            if (_capacityAtWhichToIncreaseTheIncrement >= 0 && Count >= _capacityAtWhichToIncreaseTheIncrement)
            {
                lock (_capacityLockObj)
                {
                    if (_capacityAtWhichToIncreaseTheIncrement >= 0 &&
                        _numberOfElements >= _capacityAtWhichToIncreaseTheIncrement)
                    {
                        if (++_increment >= Generations)
                        {
                            _capacityAtWhichToIncreaseTheIncrement = -1;
                        }
                        else
                        {
                            _capacityAtWhichToIncreaseTheIncrement += _periodBetweenIncrementGrowth;
                        }
                    }
                }
            }
            if (_recoveryTask == null)
            {
                if (_numberOfElements > MaxCapacity)
                {
                    StartRecoveringSpace();
                }
                else if (SumOfAmountsOverAllKeys > TotalCountMaxThreshold)
                {
                    StartReducingTotal();
                }
            }

            return new Proportion(countForThisKeyBefore, (ulong) SumOfAmountsOverAllKeys);
        }

        private void StartRecoveringSpace()
        {
            lock (_recoveryTaskLock)
            {
                if (_recoveryTask != null)
                    return;
                _recoveryTask = Task.Run(() => RecoverSpace( () =>_keyCounts.Count <= CapacityToTargetWhenRecoveringSpace ));
            }
        }

        private void StartReducingTotal()
        {
            lock (_recoveryTaskLock)
            {
                if (_recoveryTask != null)
                    return;
                _recoveryTask = Task.Run(() => RecoverSpace(() => SumOfAmountsOverAllKeys <= ReducedAmountToTargetWhenTotalAmountReached));
            }
        }

        private delegate bool RecoveryFinishedCondition();

        private void RecoverSpace(RecoveryFinishedCondition finishCondition)
        {
            bool finished = false;
            while (!finished)
            {
                if (_cleanupQueue.Count == 0)
                {
                    _cleanupQueue = new Queue<TKey>(_keyCounts.ToArray().Select(k => k.Key));
                }
                TKey key = _cleanupQueue.Dequeue();
                uint count;
                if (_keyCounts.TryGetValue(key, out count))
                {
                    if (count <= 1)
                    {
                        _keyCounts.TryRemove(key, out count);
                        Interlocked.Add(ref _sumOfAmountsOverAllKeys, -count);
                        Interlocked.Add(ref _numberOfElements, -1);
                    }
                    else
                    {
                        _keyCounts.AddOrUpdate(key, k => 0, (k, priorValue) => priorValue - 1);
                        Interlocked.Add(ref _sumOfAmountsOverAllKeys, -1);
                    }
                }
                finished = finishCondition();
            }
            lock (_recoveryTaskLock)
            {
                _recoveryTask = null;
            }
        }

    }
}

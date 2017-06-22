using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AttemptController.Models;

namespace AttemptController.DataStructures
{
    public class SelfLoadingCache<TKey, TValue> : Cache<TKey, TValue>
    {
        public delegate Task<TValue> FunctionToConstructAValueFromItsKeyAsync(TKey key, CancellationToken cancellationToken);
        public delegate TValue FunctionToConstructAValueFromItsKey(TKey key);

        protected FunctionToConstructAValueFromItsKeyAsync ConstructAValueFromItsKeyAsync;
        protected FunctionToConstructAValueFromItsKey ConstructAValueFromItsKey;

        protected readonly Dictionary<TKey, Task<TValue>> ValuesUnderConstruction;


        public SelfLoadingCache(FunctionToConstructAValueFromItsKeyAsync constructAValueFromItsKeyAsync = null)
        {
            ConstructAValueFromItsKeyAsync = constructAValueFromItsKeyAsync;
            ConstructAValueFromItsKey = null;
            ValuesUnderConstruction = new Dictionary<TKey, Task<TValue>>();
        }
        public SelfLoadingCache(FunctionToConstructAValueFromItsKey constructAValueFromItsKey = null)
        {
            ConstructAValueFromItsKeyAsync = null;
            ConstructAValueFromItsKey = constructAValueFromItsKey;
            ValuesUnderConstruction = new Dictionary<TKey, Task<TValue>>();
        }

        public virtual async Task<TValue> GetAsync(TKey key, CancellationToken cancellationToken = default(CancellationToken))
        {
            TValue resultValue;
            Task<TValue> taskToConstructValueAsync;
            bool justConstructedTheCacheEntry = false;
            lock (KeysOrderedFromMostToLeastRecentlyUsed)
            {
                if (TryGetValueWithinLock(key, out resultValue))
                {
                    return resultValue;
                }
                else
                {
                    if (ValuesUnderConstruction.TryGetValue(key, out taskToConstructValueAsync))
                    {
                    }
                    else if (ConstructAValueFromItsKeyAsync != null)
                    {
                        justConstructedTheCacheEntry = true;
                        taskToConstructValueAsync = ConstructAValueFromItsKeyAsync(key, cancellationToken);
                    }

                    else if (ConstructAValueFromItsKey != null)
                    {
                        justConstructedTheCacheEntry = true;
                        return this[key] = ConstructAValueFromItsKey(key);
                    }
                    else {
                        return default(TValue);
                    }
                }
            }
            resultValue = await taskToConstructValueAsync;
            if (justConstructedTheCacheEntry)
            {
                this[key] = resultValue;
            }
            return resultValue;
        }


        protected override TValue Get(TKey key)
        {
            return GetAsync(key).Result;       
        }


    }
}

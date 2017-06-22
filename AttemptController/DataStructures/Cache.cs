using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AttemptController.DataStructures
{
    public interface IAsyncDisposable
    {
        Task DisposeAsync();
    }



    public class Cache<TKey, TValue> : DictionaryThatTracksAccessRecency<TKey, TValue>
    {
        public virtual TValue ConstructDefaultValueForMissingCacheEntry(TKey key)
        {
            return default(TValue);
        }


        public void RecoverSpace(int numberOfItemsToRemove)
        {
            KeyValuePair<TKey, TValue>[] entriesToRemove = RemoveAndGetLeastRecentlyAccessed(numberOfItemsToRemove).ToArray();

            ConcurrentBag<Task> asyncDisposalTasks = new ConcurrentBag<Task>();

            Parallel.For(0, entriesToRemove.Length, (counter, loop) =>
            {
                KeyValuePair<TKey, TValue> entryToRemove = entriesToRemove[counter];
                TValue valueToRemove = entryToRemove.Value;
                
                if (valueToRemove is IAsyncDisposable)
                {
                    asyncDisposalTasks.Add(((IAsyncDisposable)valueToRemove).DisposeAsync());
                }
                else if (valueToRemove is IDisposable)
                {
                    ((IDisposable)valueToRemove).Dispose();
                }

                entriesToRemove[counter] = default(KeyValuePair<TKey, TValue>);
            });

            Task.WaitAll(asyncDisposalTasks.ToArray());
        }

        public void RecoverSpace(double fractionOfItemsToRemove)
        {
            int numberOfItemsToRemove = (int)(Count * fractionOfItemsToRemove);
            RecoverSpace(numberOfItemsToRemove);
        }
    }


}
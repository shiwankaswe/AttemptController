using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace AttemptController.DataStructures
{

    public class DictionaryThatTracksAccessRecency<TKey, TValue> : IDictionary<TKey, TValue>
    {
        protected Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> KeyToLinkedListNode;
        protected LinkedList<KeyValuePair<TKey, TValue>> KeysOrderedFromMostToLeastRecentlyUsed;

        public DictionaryThatTracksAccessRecency()
        {
            KeyToLinkedListNode = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>();
            KeysOrderedFromMostToLeastRecentlyUsed = new LinkedList<KeyValuePair<TKey, TValue>>();
        }

        public KeyValuePair<TKey, TValue> MostRecentlyAccessed => KeysOrderedFromMostToLeastRecentlyUsed.First.Value;

        public KeyValuePair<TKey, TValue> LeastRecentlyAccessed => KeysOrderedFromMostToLeastRecentlyUsed.Last.Value;

        public IList<KeyValuePair<TKey, TValue>> GetMostRecentlyAccessed(int numberToGet)
        {
            List<KeyValuePair<TKey, TValue>> mostRecentlyAccessedItems = new List<KeyValuePair<TKey, TValue>>(numberToGet);
            lock (KeysOrderedFromMostToLeastRecentlyUsed)
            {
                LinkedListNode<KeyValuePair<TKey, TValue>> node = KeysOrderedFromMostToLeastRecentlyUsed.First;
                for (int i = 0; i < numberToGet && node != null; i++)
                {
                    mostRecentlyAccessedItems.Add(node.Value);
                    node = node.Next;
                }
            }
            return mostRecentlyAccessedItems;
        }

        public List<KeyValuePair<TKey, TValue>> GetLeastRecentlyAccessed(int numberToGet)
        {
            List<KeyValuePair<TKey, TValue>> leastRecentlyAccessedItems = new List<KeyValuePair<TKey, TValue>>(numberToGet);
            lock(KeysOrderedFromMostToLeastRecentlyUsed)
            {
                LinkedListNode<KeyValuePair<TKey, TValue>> node = KeysOrderedFromMostToLeastRecentlyUsed.Last;
                for (int i = 0; i < numberToGet && node != null; i++)
                {
                    leastRecentlyAccessedItems.Add(node.Value);
                    node = node.Previous;
                }
            }
            return leastRecentlyAccessedItems;
        }

        public IList<KeyValuePair<TKey, TValue>> RemoveAndGetLeastRecentlyAccessed(int numberToGet)
        {
            List<KeyValuePair<TKey, TValue>> leastRecentlyAccessedItems = new List<KeyValuePair<TKey, TValue>>(numberToGet);
            lock(KeysOrderedFromMostToLeastRecentlyUsed)
            {
                numberToGet = Math.Min(numberToGet, KeysOrderedFromMostToLeastRecentlyUsed.Count);
                for (int i = 0; i < numberToGet; i++)
                {
                    leastRecentlyAccessedItems.Add(KeysOrderedFromMostToLeastRecentlyUsed.Last.Value);
                    KeyToLinkedListNode.Remove(KeysOrderedFromMostToLeastRecentlyUsed.Last.Value.Key);
                    KeysOrderedFromMostToLeastRecentlyUsed.RemoveLast();
                }
            }
            return leastRecentlyAccessedItems;
        }


        public int Count => KeyToLinkedListNode.Count;


        public ICollection<TKey> Keys
        {
            get
            {
                lock(KeysOrderedFromMostToLeastRecentlyUsed)
                {
                    return KeysOrderedFromMostToLeastRecentlyUsed.Select(keyValue => keyValue.Key).ToList();
                }
            }
        }

        public ICollection<TValue> Values
        {
            get
            {
                lock (KeysOrderedFromMostToLeastRecentlyUsed)
                {
                    return KeysOrderedFromMostToLeastRecentlyUsed.Select(keyValue => keyValue.Value).ToList();
                }
            }
        }
        
        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

        public TValue this[TKey key]
        {
            get
            {
                return Get(key);
            }
            set
            {
                Add(key, value);
            }
        }


        public virtual void Add(TKey key, TValue value)
        {
            lock (KeysOrderedFromMostToLeastRecentlyUsed)
            {
                LinkedListNode<KeyValuePair<TKey, TValue>> nodeWithSameKey;
                if (KeyToLinkedListNode.TryGetValue(key, out nodeWithSameKey))
                {
                    KeysOrderedFromMostToLeastRecentlyUsed.Remove(nodeWithSameKey);
                    KeyToLinkedListNode.Remove(key);
                }
                LinkedListNode<KeyValuePair<TKey, TValue>> newNode =
                    new LinkedListNode<KeyValuePair<TKey, TValue>>(
                        new KeyValuePair<TKey, TValue>(key, value));
                KeysOrderedFromMostToLeastRecentlyUsed.AddFirst(newNode);
                KeyToLinkedListNode[key] = newNode;

            }
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        protected virtual TValue GetWithinLock(TKey key)
        {
            LinkedListNode<KeyValuePair<TKey, TValue>> node = KeyToLinkedListNode[key];
            KeysOrderedFromMostToLeastRecentlyUsed.Remove(node);
            KeysOrderedFromMostToLeastRecentlyUsed.AddFirst(node);
            return node.Value.Value;
        }

        protected virtual TValue Get(TKey key)
        {
            lock (KeysOrderedFromMostToLeastRecentlyUsed)
            {
                return GetWithinLock(key);
            }
        }


        public void Clear()
        {
            lock (KeysOrderedFromMostToLeastRecentlyUsed)
            {
                KeyToLinkedListNode.Clear();
                KeysOrderedFromMostToLeastRecentlyUsed.Clear();
            }
        }


        public bool ContainsKey(TKey key)
        {
            lock (KeysOrderedFromMostToLeastRecentlyUsed)
            {
                return KeyToLinkedListNode.ContainsKey(key);
            }
        }

        public bool Contains(TValue value)
        {
            lock (KeysOrderedFromMostToLeastRecentlyUsed)
            {
                return KeyToLinkedListNode.Values.Any(node => node.Value.Value.Equals(value));
            }
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return ContainsKey(item.Key);
        }

        internal bool RemoveWithinLock(TKey key)
        {
            LinkedListNode<KeyValuePair<TKey, TValue>> node;
            if (KeyToLinkedListNode.TryGetValue(key, out node))
            {
                KeysOrderedFromMostToLeastRecentlyUsed.Remove(node);
                return KeyToLinkedListNode.Remove(key);
            }
            else
            {
                return false;
            }
        }

        public bool Remove(TKey key)
        {
            lock (KeysOrderedFromMostToLeastRecentlyUsed)
            {
                return RemoveWithinLock(key);
            }
        }


        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return Remove(item.Key);
        }

        internal bool TryGetValueWithinLock(TKey key, out TValue value)
        {
            LinkedListNode<KeyValuePair<TKey, TValue>> kvp;
            if (KeyToLinkedListNode.TryGetValue(key, out kvp))
            {
                value = kvp.Value.Value;
                return true;
            }
            else
            {
                value = default(TValue);
                return false;
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            lock (KeysOrderedFromMostToLeastRecentlyUsed)
            {
                return TryGetValueWithinLock(key, out value);
            }
        }


        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            lock (KeysOrderedFromMostToLeastRecentlyUsed)
            {
                KeysOrderedFromMostToLeastRecentlyUsed.CopyTo(array, arrayIndex);
            }
        }
        

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            lock (KeysOrderedFromMostToLeastRecentlyUsed)
            {
                return KeysOrderedFromMostToLeastRecentlyUsed.ToList().GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            lock (KeysOrderedFromMostToLeastRecentlyUsed)
            {
                return KeysOrderedFromMostToLeastRecentlyUsed.ToList().GetEnumerator();
            }
        }
        

           


    }
}

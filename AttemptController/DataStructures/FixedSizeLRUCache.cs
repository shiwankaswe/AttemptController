using System.Collections.Generic;

namespace AttemptController.DataStructures
{
    public class FixedSizeLruCache<TKey, TValue> : DictionaryThatTracksAccessRecency<TKey, TValue>
    {
        public int Capacity { get; }

        public FixedSizeLruCache(int capacity)
        {
            Capacity = capacity;
        }


        public override void Add(TKey key, TValue value)
        {
            lock (KeysOrderedFromMostToLeastRecentlyUsed)
            {
                LinkedListNode<KeyValuePair<TKey, TValue>> nodeWithSameKey;
                if (KeyToLinkedListNode.TryGetValue(key, out nodeWithSameKey))
                {
                    KeysOrderedFromMostToLeastRecentlyUsed.Remove(nodeWithSameKey);
                    KeyToLinkedListNode.Remove(key);
                }
                if (KeyToLinkedListNode.Count == Capacity)
                {
                    LinkedListNode<KeyValuePair<TKey, TValue>> oldestNode = KeysOrderedFromMostToLeastRecentlyUsed.Last;
                    KeysOrderedFromMostToLeastRecentlyUsed.Remove(oldestNode);
                    KeyToLinkedListNode.Remove(oldestNode.Value.Key);
                }
                LinkedListNode<KeyValuePair<TKey, TValue>> newNode = 
                    new LinkedListNode<KeyValuePair<TKey, TValue>>(
                        new KeyValuePair<TKey, TValue>(key, value));
                KeysOrderedFromMostToLeastRecentlyUsed.AddFirst(newNode);
                KeyToLinkedListNode[key] = newNode;
            }
        }

    }
}

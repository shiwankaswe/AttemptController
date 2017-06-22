using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace AttemptController.DataStructures
{

    [DataContract]
    [JsonConverter(typeof(SequenceConverter))]
    public class Sequence<T> : ICollection<T>, IEquatable<Sequence<T>>      
    {
        [IgnoreDataMember]
        protected int CurrentItemIndex;
        [IgnoreDataMember]
        public int Count { get; protected set; }
        [IgnoreDataMember]
        protected internal long ItemsAlreadyObserved { get; set; }
        [IgnoreDataMember]
        protected T[] SequenceArray;

        public IEnumerable<T> OldestToMostRecent => this;
        public IEnumerable<T> MostRecentToOldest => this.Reverse();

        public int Capacity
        {
            get { return SequenceArray.Length; }
            set
            {
                if (SequenceArray != null )
                    throw new Exception("Once the capacity of a seqeunce has been set it cannot be changed.");
                SequenceArray = new T[value];
            }
        }

        public long CountOfAllObservedItems => ItemsAlreadyObserved;


        public Sequence(int capacity, long itemsAlreadyObserved)
        {
            SequenceArray = new T[capacity];
            CurrentItemIndex = 0;
            Count = 0;
            ItemsAlreadyObserved = itemsAlreadyObserved;
        }


        public Sequence(int capacity) : this(capacity, 0)
        {
        }

        public T this[int index]
        {
            get
            {
                lock (SequenceArray)
                {
                    return SequenceArray[SequenceIndexToArrayIndex(index)];
                }
            }
            set
            {
                if (index != 0)
                    throw new IndexOutOfRangeException("You can only set the current value of a sequence");
                Add(value);
            }
        }

        public virtual void Add(T value)
        {
            lock (SequenceArray)
            {
                if (++CurrentItemIndex >= SequenceArray.Length)
                    CurrentItemIndex = 0;
                SequenceArray[CurrentItemIndex] = value;
                if (Count < Capacity)
                {
                    Count++;
                }
                ItemsAlreadyObserved++;
            }
        }

        public virtual void AddRange(IEnumerable<T> values)
        {
            lock (SequenceArray)
            {
                foreach (T value in values)
                {
                    if (++CurrentItemIndex >= SequenceArray.Length)
                        CurrentItemIndex = 0;
                    SequenceArray[CurrentItemIndex] = value;
                    if (Count < Capacity)
                    {
                        Count++;
                    }
                    ItemsAlreadyObserved++;
                }
            }
        }

        public virtual bool Contains(T value)
        {
            lock (SequenceArray)
            {
                return SequenceArray.Contains(value);
            }
        }

        public void Clear()
        {
            lock (SequenceArray)
            {
                CurrentItemIndex = 0;
                Count = 0;
                ItemsAlreadyObserved = 0;
            }
        }

        public void CopyTo(T[] subsequenceBuffer, int index)
        {
            lock (SequenceArray)
            {

                int numItemsToCopy = subsequenceBuffer.Length - index;

                if (numItemsToCopy > Capacity)
                    throw new IndexOutOfRangeException(
                        "Attempt to Get a subsequence using items beyond the sequence's capacity.");

                int howFarBackToStart = numItemsToCopy;
                while (index < subsequenceBuffer.Length)
                    subsequenceBuffer[index++] = this[--howFarBackToStart];
            }
        }

        public T[] GetSubsequence(int howFarBackToStart, int howManyItemsToTake)
        {
            T[] subsequenceBuffer = new T[howManyItemsToTake];
            lock (SequenceArray)
            {
                if (howFarBackToStart < 0)
                    howFarBackToStart = -howFarBackToStart;
                if (howFarBackToStart >= Count)
                    throw new IndexOutOfRangeException("Attempt to Get a subsequence using items older than are tracked, or the number of observed items.");
                if (howFarBackToStart > Capacity)
                    throw new IndexOutOfRangeException("Attempt to request a subsequence further back in time than the sequence has capacity to remember.");

                for (int i = 0; i < subsequenceBuffer.Length; i++)
                    subsequenceBuffer[i] = this[howFarBackToStart - i];
            }
            return subsequenceBuffer;
        }

        public T[] GetSubsequence(int howFarBackToStart)
        {
            if (howFarBackToStart < 0)
                howFarBackToStart = -howFarBackToStart;
            return GetSubsequence(howFarBackToStart, howFarBackToStart + 1);
        }

        public T[] GetSubsequence()
        {
            return GetSubsequence(Count - 1, Count);
        }



        protected int SequenceIndexToArrayIndex(int sequenceIndex)
        {
            if (sequenceIndex == int.MinValue)
                throw new IndexOutOfRangeException("Cannot index int.MinValue.");
            if (sequenceIndex < 0)
                sequenceIndex = -sequenceIndex;
            if (sequenceIndex > Count)
                throw new IndexOutOfRangeException("Index is out of range of valid sequence values.");

            int arrayIndex = CurrentItemIndex - sequenceIndex;
            if (arrayIndex < 0)
                arrayIndex += SequenceArray.Length;

            return arrayIndex;
        }

        public T Current
        {
            get
            {
                lock (SequenceArray)
                {
                    return SequenceArray[CurrentItemIndex];
                }
            }
            set { Add(value); }
        }



        public bool Remove(T item)
        {
            lock (SequenceArray)
            {
                for (int i = 0; i < Count; i++)
                {
                    if (EqualityComparer<T>.Default.Equals(item, this[i]))
           
                    {
                        RemoveAt(i);
                        return true;
                    }
                }
                return false;
            }
        }

        public virtual void RemoveAt(int index)
        {
            lock (SequenceArray)
            {
                int currentIndex = SequenceIndexToArrayIndex(index);
                for (int i = index; i < Count; i++)
                {
                    int nextIndex = currentIndex - 1;
                    if (nextIndex < 0)
                        nextIndex += SequenceArray.Length;
                    SequenceArray[currentIndex] = SequenceArray[nextIndex];
                }
                Count--;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new SequenceEnumerator(this);
        }

        public bool IsReadOnly => false;

        public bool Equals(Sequence<T> other)
        {
            return other.Capacity == Capacity &&
                   other.Count == Count &&
                   other.GetSubsequence().SequenceEqual(GetSubsequence());
        }

        public override bool Equals(object other)
        {
            if (!(other is Sequence<T>)) return false;
            return Equals((Sequence<T>) other);
        }

        public override int GetHashCode()
        {
            return GetSubsequence().GetHashCode();
        }

        public class SequenceEnumerator : IEnumerator<T>
        {
            readonly Sequence<T> _sequence;
            private int _count;

            public SequenceEnumerator(Sequence<T> sequence)
            {
                _sequence = sequence;
                Reset();
            }

            public T Current => _sequence[_count];

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                return (--_count >= 0);
            }

            public void Reset()
            {
                _count = _sequence.Count;
            }

            public void Dispose()
            { }
        }
    }


    public class SequenceConverter : JsonConverter
    {
        private const string CapacityName = "Capacity";
        private const string AdditionalItemsObservedName = "AdditionalItemsObserved";
        private const string ValuesName = "Values";


        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer
            )
        {
            JObject jObject = JObject.Load(reader);

            int capacity = jObject[CapacityName].Value<int>();

            long additionalItemsObserved = 0;
            JToken additionalItemsObservedToken;
            if (jObject.TryGetValue(AdditionalItemsObservedName, out additionalItemsObservedToken))
            {
                additionalItemsObserved = additionalItemsObservedToken.ToObject<long>();
            }

            dynamic target = Activator.CreateInstance(objectType, capacity, additionalItemsObserved);

            Type typeArrayOfT = typeof(List<>).MakeGenericType(objectType.GenericTypeArguments);
            dynamic values = jObject[ValuesName].ToObject(typeArrayOfT);
            target.AddRange(values);

            return target;
        }


        public override void WriteJson(
            JsonWriter writer,
            Object value,
            JsonSerializer serializer
            )
        {
            dynamic sequence = value;
            writer.WriteStartObject();
            writer.WritePropertyName(CapacityName);
            serializer.Serialize(writer, sequence.Capacity);
            long additionalItemsObserved = sequence.ItemsAlreadyObserved - sequence.Count;
            if (additionalItemsObserved > 0)
            {
                writer.WritePropertyName(AdditionalItemsObservedName);
                serializer.Serialize(writer, additionalItemsObserved);
            }
            writer.WritePropertyName(ValuesName);
            serializer.Serialize(writer, sequence.GetSubsequence(sequence.Count - 1, sequence.Count));
            writer.WriteEndObject();
        }


        public override bool CanConvert(Type objectType)
        {
            bool canConvert = objectType.GetGenericTypeDefinition() == typeof(Sequence<>);
            return canConvert;
        }
    }
    
}

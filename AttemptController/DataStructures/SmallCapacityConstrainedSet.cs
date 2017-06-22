using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AttemptController.DataStructures
{

    [DataContract]
    [JsonConverter(typeof(SmallCapacityConstrainedSetConverter))]
    public class SmallCapacityConstrainedSet<T> : IEquatable<SmallCapacityConstrainedSet<T>>
    {
        public int Capacity { get; protected set; }

        protected LinkedList<T> AsList;
        protected ReaderWriterLockSlim RwLock;

        public IEnumerable<T> LeastRecentFirst => MostRecentFirst.Reverse();

        public bool Contains(T item)
        {
            RwLock.EnterReadLock();
            try
            {
                return AsList.Contains(item);
            }
            finally
            {
                RwLock.ExitReadLock();
            }            
        } 

        public IEnumerable<T> MostRecentFirst
        {
            get
            {
                {
                    RwLock.EnterReadLock();
                    try
                    {
                        return AsList.ToArray();
                    }
                    finally
                    {
                        RwLock.ExitReadLock();
                    }
                }
            }
        }

        public SmallCapacityConstrainedSet(int capacity)
        {
            AsList = new LinkedList<T>();
            Capacity = capacity;
            RwLock = new ReaderWriterLockSlim();
        }
       

        public bool Add(T item)
        {
            RwLock.EnterWriteLock();
            try
            {
                bool itemIsAlreadyPresent = AsList.Contains(item);
                if (itemIsAlreadyPresent)
                {
                    if (!item.Equals(AsList.First))
                    {
                        AsList.Remove(item);
                        AsList.AddFirst(item);
                    }
                }
                else
                {
                    if (AsList.Count >= Capacity)
                    {
                        AsList.RemoveLast();
                    }
                    AsList.AddFirst(item);

                }
                return itemIsAlreadyPresent;
            }
            finally
            {
                RwLock.ExitWriteLock();
            }
        }

        public bool Remove(T item)
        {
            RwLock.EnterWriteLock();
            try
            {
                return AsList.Remove(item);
            }
            finally
            {
                RwLock.ExitWriteLock();
            }
        }

        public void UnionWith(IEnumerable<T> newMembers)
        {
            foreach (T newMember in newMembers)
                Add(newMember);
        }
        
        public bool Equals(SmallCapacityConstrainedSet<T> other)
        {
            return other.Capacity == Capacity &&
                   other.LeastRecentFirst.SequenceEqual(LeastRecentFirst);
        }
    }




    public class SmallCapacityConstrainedSetConverter : JsonConverter
    {
        private const string CapacityName = "Capacity";
        private const string MembersName = "Members";


        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);

            int capacity = jObject[CapacityName].Value<int>();

            dynamic target = Activator.CreateInstance(objectType, capacity);

            Type typeArrayOfT = typeof (List<>).MakeGenericType(objectType.GenericTypeArguments);
            dynamic values = jObject[MembersName].ToObject(typeArrayOfT);
            target.UnionWith(values);

            return target;
        }


        public override void WriteJson(JsonWriter writer, Object value, JsonSerializer serializer)
        {
            dynamic set = value;
            writer.WriteStartObject();
            writer.WritePropertyName(CapacityName);
            serializer.Serialize(writer, set.Capacity);
            writer.WritePropertyName(MembersName);
            serializer.Serialize(writer, set.LeastRecentFirst);
            writer.WriteEndObject();
        }

        public override bool CanConvert(Type objectType)
        {
            bool canConvert = objectType.GetGenericTypeDefinition() == typeof(SmallCapacityConstrainedSet<>);
            return canConvert;
        }
    }

}
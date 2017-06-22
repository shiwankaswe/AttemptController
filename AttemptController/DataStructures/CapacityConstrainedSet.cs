using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AttemptController.DataStructures
{

    [DataContract]
    [JsonConverter(typeof(CapacityConstrainedSetConverter))]
    public class CapacityConstrainedSet<T> : HashSet<T>, IEquatable<CapacityConstrainedSet<T>>
    {
        public int Capacity { get; protected set; }

        private readonly LinkedList<T> _recency;

        public List<T> InOrderAdded
        {
            get
            {
                lock (_recency)
                {
                    return _recency.Reverse().ToList();
                }
            }
        }

        public CapacityConstrainedSet(int capacity)
        {
            Capacity = capacity;
            _recency = new LinkedList<T>();
        }
       

        public new bool Add(T item)
        {
            lock (_recency)
            {
                bool itemIsAlreadyPresent = Contains(item);
                if (itemIsAlreadyPresent)
                {
                    if (!item.Equals(_recency.First()))
                    {
                        _recency.Remove(item);
                        _recency.AddFirst(item);
                    }
                }
                else
                {
                    if (Count >= Capacity)
                    {
                        base.Remove(_recency.Last());
                        _recency.RemoveLast();
                    }
                    base.Add(item);
                    _recency.AddFirst(item);

                }
                return itemIsAlreadyPresent;
            }
        }

        public new bool Remove(T item)
        {
            lock (_recency)
            {
                _recency.Remove(item);
                return base.Remove(item);
            }
        }

        public new void UnionWith(IEnumerable<T> newMembers)
        {
            foreach (T newMember in newMembers)
                Add(newMember);
        }
        
        public bool Equals(CapacityConstrainedSet<T> other)
        {
            return other.Capacity == Capacity &&
                   other.InOrderAdded.SequenceEqual(InOrderAdded);
        }
    }




    public class CapacityConstrainedSetConverter : JsonConverter
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
            serializer.Serialize(writer, set.InOrderAdded);
            writer.WriteEndObject();
        }

        public override bool CanConvert(Type objectType)
        {
            bool canConvert = objectType.GetGenericTypeDefinition() == typeof (CapacityConstrainedSet<>);
            return canConvert;
        }
    }

}
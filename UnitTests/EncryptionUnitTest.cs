using System;
using AttemptController.EncryptionPrimitives;
using AttemptController.Models;
using Xunit;

namespace UnitTests
{
    public class EncryptionUnitTest
    {

        [Fact]
        public void Testserilization()
        {
            DateTime utcNow = DateTime.UtcNow;
            
            LoginAttempt attempt = new LoginAttempt()
            {
                TimeOfAttemptUtc = utcNow
            };
            string serialized = Newtonsoft.Json.JsonConvert.SerializeObject(attempt);
            LoginAttempt deserialized = Newtonsoft.Json.JsonConvert.DeserializeObject<LoginAttempt>(serialized);
            DateTimeOffset deserializedTimeOfAttempt = deserialized.TimeOfAttemptUtc;
            Assert.Equal(utcNow, deserializedTimeOfAttempt);
        }

    }
}

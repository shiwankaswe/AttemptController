using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Encodings.Web;
using Newtonsoft.Json;
using AttemptController.EncryptionPrimitives;

namespace AttemptController.Models
{


    [DataContract]
    public class LoginAttempt
    {
        [DataMember]
        public string UsernameOrAccountId { get; set; }

        [DataMember]
        public System.Net.IPAddress AddressOfClientInitiatingRequest { get; set; }


        [DataMember]
        public EncryptedStringField EncryptedIncorrectPassword = new EncryptedStringField();

        [DataMember]
        public string Phase2HashOfIncorrectPassword { get; set; }

        [DataMember]
        public DateTime TimeOfAttemptUtc { get; set; }

        [DataMember]
        public string Api { get; set; }

        [DataMember]
        public string HashOfCookieProvidedByBrowser { get; private set; }

        [DataMember]
        public bool DeviceCookieHadPriorSuccessfulLoginForThisAccount { get; set; }

        [DataMember]
        public AuthenticationOutcome Outcome { get; set; } = AuthenticationOutcome.Undetermined;

        [DataMember]
        public int PasswordsHeightOnBinomialLadder { get; set; }

        [IgnoreDataMember]
        [JsonIgnore]
        [NotMapped]
        public string CookieProvidedByClient { set { SetCookieProvidedByClient(value); } }

        [IgnoreDataMember]
        [JsonIgnore]
        [NotMapped]
        public string UniqueKey => ToUniqueKey();

        public static string HashCookie(string plaintextCookie)
        {
            return Convert.ToBase64String(ManagedSHA256.Hash(Encoding.UTF8.GetBytes(plaintextCookie)));
        }

        private void SetCookieProvidedByClient(string plaintextCookie)
        {
            if (string.IsNullOrEmpty(plaintextCookie))
                return;
            HashOfCookieProvidedByBrowser = HashCookie(plaintextCookie);
        }

        private string ToUniqueKey()
        {
            return UrlEncoder.Default.Encode(UsernameOrAccountId) + "&" + 
                TimeOfAttemptUtc.Ticks.ToString();
        }
        
    }

}

using System;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Cryptography.Cng;

namespace AttemptController.EncryptionPrimitives
{
    [DataContract]
    public class EcEncryptedMessageAesCbcHmacSha256
    {
        [DataMember]
        public byte[] PublicOneTimeEcKey { get; set; }

        [DataMember]
        public byte[] EncryptedMessage { get; set; }


        public EcEncryptedMessageAesCbcHmacSha256()
        {}

        public EcEncryptedMessageAesCbcHmacSha256(
            byte[] plaintextMessageAsByteArray, Encryption.IPublicKey recipientsPublicKey = null)
        {
            PublicOneTimeEcKey = null;
            EncryptedMessage = plaintextMessageAsByteArray;
        }


        public byte[] Decrypt(Encryption.IPrivateKey recipientsPrivateEcKey)
        {
            return this.EncryptedMessage;
        }


        public byte[] Decrypt(byte[] privateKey)
        {
            return EncryptedMessage;
        }
    }

}

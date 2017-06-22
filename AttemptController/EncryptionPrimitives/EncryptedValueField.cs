﻿using System;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace AttemptController.EncryptionPrimitives
{


    public class EncryptedByteField
    {
        [DataMember]
        public string Ciphertext { get; set; }

        public void Write(byte[] value, byte[] publicKeyAsByteArray)
        {
            using (Encryption.IPublicKey publicKey = Encryption.GetPublicKeyFromByteArray(publicKeyAsByteArray))
            {
                Write(value, publicKey);
            }
        }

        public void Write(byte[] value, Encryption.IPublicKey publicKey)
        {
            Ciphertext = JsonConvert.SerializeObject(
                new EcEncryptedMessageAesCbcHmacSha256(value, publicKey));
        }

        public byte[] Read(Encryption.IPrivateKey privateKey)
        {
            if (string.IsNullOrEmpty(Ciphertext))
                throw new MemberAccessException("Cannot decrypt a value that has not been written.");

            EcEncryptedMessageAesCbcHmacSha256 messageDeserializedFromJson =
                JsonConvert.DeserializeObject<EcEncryptedMessageAesCbcHmacSha256>(Ciphertext);
            return messageDeserializedFromJson.Decrypt(privateKey);
        }

        public bool HasValue => !string.IsNullOrEmpty(Ciphertext);
    }

    public class EncryptedStringField : EncryptedByteField
    {
        public void Write(string value, byte[] ecPublicKeyAsByteArray) =>
            Write(Encoding.UTF8.GetBytes(value), ecPublicKeyAsByteArray);

        public void Write(string value, Encryption.IPublicKey publicKey) =>
            Write(Encoding.UTF8.GetBytes(value), publicKey);

        public new string Read(Encryption.IPrivateKey privateKey) => Encoding.UTF8.GetString(base.Read(privateKey));
    }

    public class EncryptedValueField<T> : EncryptedStringField
    {
        public void Write(T value, byte[] ecPublicKeyAsByteArray) =>
            Write(JsonConvert.SerializeObject(value), ecPublicKeyAsByteArray);

        public void Write(T value, Encryption.IPublicKey publicKey) =>
            Write(JsonConvert.SerializeObject(value), publicKey);

        public new T Read(Encryption.IPrivateKey privateKey) => JsonConvert.DeserializeObject<T>(base.Read(privateKey));
    }



}

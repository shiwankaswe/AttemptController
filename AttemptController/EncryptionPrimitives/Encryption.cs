using System;
using System.Linq;
using System.Security.Cryptography;

namespace AttemptController.EncryptionPrimitives
{
    public class Encryption
    {
        public interface IPublicKey : IDisposable
        {
        }

        public interface IPrivateKey : IDisposable
        {
        }

        static readonly byte[] NullIv = new byte[16];

        const int Sha256HmacLength = 32;

        public static IPublicKey GetPublicKeyFromByteArray(byte[] publicKeyAsByteArray)
        {
            return null;
        }
 
        public static byte[] EncryptAesCbc(byte[] plaintext, byte[] key, byte[] iv = null, bool addHmac = false)
        {
            using (Aes aes =Aes.Create())
            {
                aes.Key = key;
                if (iv == null)
                    iv = NullIv;
                aes.Mode = CipherMode.CBC;
                aes.IV = iv;

                byte[] cipherText;
                using (System.IO.MemoryStream ciphertext = new System.IO.MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ciphertext, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(plaintext, 0, plaintext.Length);
                        if (addHmac)
                        {
                            byte[] hmac = new HMACSHA256(key).ComputeHash(plaintext);
                            cs.Write(hmac, 0, hmac.Length);
                        }
                        cs.Flush();
                    }
                    cipherText = ciphertext.ToArray();
                }

                return cipherText;
            }
        }

        public static byte[] EncryptAesCbc(string plainText, byte[] key, byte[] iv = null, bool addHmac = false)
        {
            return EncryptAesCbc(System.Text.Encoding.UTF8.GetBytes(plainText), key, iv, addHmac);
        }


        public static byte[] DecryptAesCbc(byte[] ciphertext, byte[] key, byte[] iv = null, bool checkAndRemoveHmac = false)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                if (iv == null)
                    iv = NullIv;

                aes.IV = iv;
                aes.Mode = CipherMode.CBC;

                using (System.IO.MemoryStream plaintextStream = new System.IO.MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(plaintextStream, aes.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(ciphertext, 0, ciphertext.Length);
                    }
                    byte[] plaintext = plaintextStream.ToArray();
                    if (checkAndRemoveHmac)
                    {
                        byte[] hmacProvided = plaintext.Skip(plaintext.Length - Sha256HmacLength).ToArray();
                        plaintext = plaintext.Take(plaintext.Length - Sha256HmacLength).ToArray();
                        byte[] hmacCalculated = new HMACSHA256(key).ComputeHash(plaintext);
                        if (!hmacProvided.SequenceEqual(hmacCalculated))
                            throw new CryptographicException("Message authentication code validation failed.");
                    }
                    return plaintext;
                }
            }
        }

        public static string DecryptAescbcutf8(byte[] ciphertext, byte[] key, byte[] iv = null, bool checkAndRemoveHmac = false)
        {
            return System.Text.Encoding.UTF8.GetString(DecryptAesCbc(ciphertext, key, iv, checkAndRemoveHmac));
        }

        public static IPrivateKey GenerateNewPrivateKey()
        {
            return null;            
        }

        public static byte[] EncryptPrivateKeyWithAesCbc(IPrivateKey privateKey, byte[] symmetricKey)  
        {
            return new byte[0];
        }

        public static IPrivateKey DecryptAesCbcEncryptedPrivateKey(
            byte[] privateKeyEncryptedWithAesCbc,
            byte[] symmetricKey)
        {
            byte[] ecPrivateAccountLogKeyAsBytes = DecryptAesCbc(
                                privateKeyEncryptedWithAesCbc,
                                symmetricKey.Take(16).ToArray(),
                                checkAndRemoveHmac: true);
            return null;
        }


        public static byte[] KeyGenFromPwd(string password, byte[] salt)
        {
            Rfc2898DeriveBytes pwdGen = new Rfc2898DeriveBytes(password, salt, 10000);
            return pwdGen.GetBytes(32);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AttemptController.EncryptionPrimitives
{

    public class UniversalHashFunction
    {
        private readonly ulong _initialRandomKey;
        private readonly ulong[] _randomKeyVector;
        private int _randomKeyVectorLengthInBytes;

        public const int MaximumNumberOfResultBitsGuaranteedToBeUnbiased = 32;

        public const int MaximumNumberOfResultBitsAllowing32BiasedBits = 64;


        private void SetVectorLength(int randomKeyVectorLengthInBytes)
        {
            if (randomKeyVectorLengthInBytes < 16)
                randomKeyVectorLengthInBytes = 16;

            int mod8;
            if ((mod8 = randomKeyVectorLengthInBytes % 8) != 0)
            {
                randomKeyVectorLengthInBytes += (8 - mod8);
            }
            _randomKeyVectorLengthInBytes = randomKeyVectorLengthInBytes;
        }


        public UniversalHashFunction(int randomKeyVectorLengthInBytes)
        {
            int numberOfPseudoRandomBytesNeeded = SetVectorLengthAndGetNumberOfRandomBytesNeeded(randomKeyVectorLengthInBytes);
            byte[] pseudoRandomBytes = new byte[numberOfPseudoRandomBytesNeeded];

            StrongRandomNumberGenerator.GetBytes(pseudoRandomBytes);

            _randomKeyVector = new ulong[randomKeyVectorLengthInBytes/8];
            for (int i = 0; i < _randomKeyVector.Length; i++)
                _randomKeyVector[i] = BitConverter.ToUInt64(pseudoRandomBytes, i*8);
            _initialRandomKey = BitConverter.ToUInt64(pseudoRandomBytes, randomKeyVectorLengthInBytes);
        }



        public UniversalHashFunction(byte[] keyOf16Or24Or32Bytes, int randomKeyVectorLengthInBytes = 256)
        {
            int numberOfRandomBytesToGenerate = SetVectorLengthAndGetNumberOfRandomBytesNeeded(randomKeyVectorLengthInBytes);

            using (System.Security.Cryptography.Aes aes = System.Security.Cryptography.Aes.Create())
            {
                aes.Key = keyOf16Or24Or32Bytes;
                aes.IV = new byte[16];
                aes.Mode = System.Security.Cryptography.CipherMode.CBC;

                byte[] pseudoRandomBytes;
                using (System.IO.MemoryStream ciphertext = new System.IO.MemoryStream())
                {
                    using (System.Security.Cryptography.CryptoStream cs = new System.Security.Cryptography.CryptoStream(ciphertext, aes.CreateEncryptor(), System.Security.Cryptography.CryptoStreamMode.Write))
                    {
                        if (numberOfRandomBytesToGenerate % 16 != 0)
                        {
                            numberOfRandomBytesToGenerate += 16 - (numberOfRandomBytesToGenerate % 16);
                        }
                        cs.Write(new byte[numberOfRandomBytesToGenerate], 0, numberOfRandomBytesToGenerate);
                    }
                    pseudoRandomBytes = ciphertext.ToArray();
                }

                _randomKeyVector = new ulong[randomKeyVectorLengthInBytes / 8];
                for (int i = 0; i < _randomKeyVector.Length; i++)
                    _randomKeyVector[i] = BitConverter.ToUInt64(pseudoRandomBytes, i * 8);
                _initialRandomKey = BitConverter.ToUInt64(pseudoRandomBytes, randomKeyVectorLengthInBytes);
            }
        }

        
        public UniversalHashFunction(string key, int randomKeyVectorLengthInBytes = 256)
            : this(ManagedSHA256.Hash(Encoding.UTF8.GetBytes(key)), randomKeyVectorLengthInBytes)
        {
        }


        public ulong Hash(IList<UInt32> messageToBeHashed, int numberOfBitsToReturn = MaximumNumberOfResultBitsGuaranteedToBeUnbiased)
        {
            if (messageToBeHashed.Count > _randomKeyVector.Length)
                return HashArrayThatExceedsLengthOfRandomKeyVector(messageToBeHashed, numberOfBitsToReturn);
            ulong sum = _initialRandomKey;
            for (int i = 0; i < messageToBeHashed.Count; i++) {
                sum += messageToBeHashed[i] * _randomKeyVector[i];
            }
            return sum >> (64 - numberOfBitsToReturn);
        }


        private int SetVectorLengthAndGetNumberOfRandomBytesNeeded(int randomKeyVectorLengthInBytes)
        {
            SetVectorLength(randomKeyVectorLengthInBytes);
            if (randomKeyVectorLengthInBytes < 16)
                randomKeyVectorLengthInBytes = 16;

            int mod8;
            if ((mod8 = randomKeyVectorLengthInBytes % 8) != 0)
            {
                randomKeyVectorLengthInBytes += (8 - mod8);
            }
            _randomKeyVectorLengthInBytes = randomKeyVectorLengthInBytes;

            return 8 + _randomKeyVectorLengthInBytes;
        }

        public ulong Hash(IList<byte> messageToBeHashed, int numberOfBitsToReturn = MaximumNumberOfResultBitsGuaranteedToBeUnbiased)
        {
            if (messageToBeHashed.Count > (_randomKeyVector.Length * 4))
                return HashArrayThatExceedsLengthOfRandomKeyVector(messageToBeHashed, numberOfBitsToReturn);
            ulong sum = _initialRandomKey;
            int indexIntoRandomKeyVector = 0;
            for (int i = 0; i < messageToBeHashed.Count; i++)
            {
                ulong value = (((UInt32)messageToBeHashed[i]) << 24);
                if (++i < messageToBeHashed.Count)
                    value |= (((UInt32)messageToBeHashed[i]) << 16);
                if (++i < messageToBeHashed.Count)
                    value |= (((UInt32)messageToBeHashed[i]) << 8);
                if (++i < messageToBeHashed.Count)
                    value |= messageToBeHashed[i];

                value *= _randomKeyVector[indexIntoRandomKeyVector++];
                sum += value;
            }
            return sum >> (64 - numberOfBitsToReturn);
        }

        internal ulong HashArrayThatExceedsLengthOfRandomKeyVector(IList<UInt32> messageToBeHashed, int numberOfBitsToReturn)
        {
            List<UInt32> hashesForEachBlock = new List<uint>(2 + 2 * (messageToBeHashed.Count / _randomKeyVector.Length));

            int valuesConsumed = messageToBeHashed.Count;
            while (valuesConsumed < messageToBeHashed.Count)
            {
                List<UInt32> block = messageToBeHashed.Skip(valuesConsumed).Take(_randomKeyVector.Length).ToList();
                ulong blockHash = Hash(block);
                hashesForEachBlock.Add((UInt32)(blockHash >> 32));
                hashesForEachBlock.Add((UInt32)(blockHash & 0xFFFFFFFF));
                valuesConsumed += block.Count;
            }

            return Hash(hashesForEachBlock, numberOfBitsToReturn);
        }


        internal ulong HashArrayThatExceedsLengthOfRandomKeyVector(IList<byte> messageToBeHashed, int numberOfBitsToReturn)
        {
            List<UInt32> hashesForEachBlock = new List<uint>(2 + 2 * (messageToBeHashed.Count / (_randomKeyVectorLengthInBytes)));

            int valuesConsumed = messageToBeHashed.Count;
            while (valuesConsumed < messageToBeHashed.Count)
            {
                List<byte> block = messageToBeHashed.Skip(valuesConsumed).Take(_randomKeyVectorLengthInBytes).ToList();
                ulong blockHash = Hash(block);
                hashesForEachBlock.Add((UInt32)(blockHash >> 32));
                hashesForEachBlock.Add((UInt32)(blockHash & 0xFFFFFFFF));
                valuesConsumed += block.Count;
            }

            return Hash(hashesForEachBlock, numberOfBitsToReturn);
        }


        public ulong Hash(string stringToHash, int numberOfBitsToReturn = MaximumNumberOfResultBitsGuaranteedToBeUnbiased) {
            return Hash(Encoding.UTF8.GetBytes(stringToHash), numberOfBitsToReturn);
        }

        public ulong Hash(UInt32 valueToHash, int numberOfBitsToReturn = MaximumNumberOfResultBitsGuaranteedToBeUnbiased)
        {
            ulong hashValue = _initialRandomKey + (valueToHash * _randomKeyVector[0]);
            return hashValue >> (64 - numberOfBitsToReturn);
        }

        public ulong Hash(Int32 valueToHash, int numberOfBitsToReturn = MaximumNumberOfResultBitsGuaranteedToBeUnbiased)
        {
            ulong hashValue = _initialRandomKey + (((ulong)valueToHash) * _randomKeyVector[0]);
            return hashValue >> (64 - numberOfBitsToReturn);
        }

        public ulong Hash(ulong valueToHash, int numberOfBitsToReturn = MaximumNumberOfResultBitsGuaranteedToBeUnbiased)
        {
            ulong hashValue = _initialRandomKey +
                ((valueToHash >> 32) * _randomKeyVector[0]) +
                ((valueToHash & 0xFFFFFFFF) * _randomKeyVector[1]);
            return hashValue >> (64 - numberOfBitsToReturn);
        }

        public ulong Hash(Int64 valueToHash, int numberOfBitsToReturn = MaximumNumberOfResultBitsGuaranteedToBeUnbiased)
        {
            ulong hashValue = _initialRandomKey +
                (((ulong)valueToHash >> 32) * _randomKeyVector[0]) +
                (((ulong)valueToHash & 0xFFFFFFFF) * _randomKeyVector[1]);
            return hashValue >> (64 - numberOfBitsToReturn);
        }


    }

}

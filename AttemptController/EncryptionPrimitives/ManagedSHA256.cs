using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace AttemptController.EncryptionPrimitives
{
    public static class ManagedSHA256
    {
        [ThreadStatic] static SHA256 Sha256;


        public static byte[] Hash(byte[] buffer)
        {
            if (Sha256 == null)
            {
                Sha256 = SHA256.Create();
            }
            return Sha256.ComputeHash(buffer);
        }

    }
}

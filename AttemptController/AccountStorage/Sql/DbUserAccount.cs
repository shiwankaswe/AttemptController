﻿using System;
using System.ComponentModel.DataAnnotations.Schema;
using StopGuessing.Controllers;
using StopGuessing.EncryptionPrimitives;
using StopGuessing.Interfaces;

namespace StopGuessing.AccountStorage.Sql
{
    /// <summary>
    /// An implementation of IUserAccount that stores account information in an SQL database.
    /// </summary>
    public class DbUserAccount : IUserAccount
    {
        public string DbUserAccountId { get; set; }

        [NotMapped]
        public string UsernameOrAccountId => DbUserAccountId;

        /// <summary>
        /// The salt is a random unique sequence of bytes that is included when the password is hashed (phase 1 of hashing)
        /// to ensure that attackers who might obtain the set of account hashes cannot hash a password once and then compare
        /// the hash against every account.
        /// </summary>
        public byte[] SaltUniqueToThisAccount { get; set; } =
            StrongRandomNumberGenerator.GetBytes(UserAccountController<DbUserAccount>.DefaultSaltLength);

        /// <summary>
        /// The name of the (hopefully) expensive hash function used for the first phase of password hashing.
        /// </summary>
        public string PasswordHashPhase1FunctionName { get; set; } =
            ExpensiveHashFunctionFactory.DefaultFunctionName;

        /// <summary>
        /// The number of iterations to use for the phase 1 hash to make it more expensive.
        /// </summary>
        public int NumberOfIterationsToUseForPhase1Hash { get; set; } =
            UserAccountController<DbUserAccount>.DefaultIterationsForPasswordHash;

        /// <summary>
        /// An EC public encryption symmetricKey used to store log about password failures, which can can only be decrypted when the user 
        /// enters her correct password, the expensive (phase1) hash of which is used to symmetrically encrypt the matching EC private symmetricKey.
        /// </summary>
        public byte[] EcPublicAccountLogKey { get; set; }

        /// <summary>
        /// The EC private symmetricKey encrypted with phase 1 (expensive) hash of the password
        /// </summary>        
        public byte[] EcPrivateAccountLogKeyEncryptedWithPasswordHashPhase1 { get; set; }

        /// <summary>
        /// The Phase2 password hash is the result of hasing the password (and salt) first with the expensive hash function to create a Phase1 hash,
        /// then hasing that Phase1 hash (this time without the salt) using SHA256 so as to make it unnecessary to store the
        /// phase1 hash in this record.  Doing so allows the Phase1 hash to be used as a symmetric encryption symmetricKey for the log. 
        /// </summary>
        public string PasswordHashPhase2 { get; set; }

        /// <summary>
        /// The account's credit limit for offsetting penalties for IP addresses from which
        /// the account has logged in successfully.
        /// </summary>
        public double CreditLimit { get; set; } =
            UserAccountController<DbUserAccount>.DefaultCreditLimit;

        /// <summary>
        /// The half life with which used credits are removed from the system freeing up new credit
        /// </summary>
        public TimeSpan CreditHalfLife { get; set; } =
            new TimeSpan(TimeSpan.TicksPerHour * UserAccountController<DbUserAccount>.DefaultCreditHalfLifeInHours);


        public DbUserAccount()
        {
        }

        
    }

}

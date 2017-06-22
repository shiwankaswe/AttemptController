using System;

namespace AttemptController.Interfaces
{
    public interface IUserAccount
    {
        string UsernameOrAccountId { get; }

        byte[] SaltUniqueToThisAccount { get; }

        string PasswordHashPhase1FunctionName { get; set; }

        int NumberOfIterationsToUseForPhase1Hash { get; set; }

        byte[] EcPublicAccountLogKey { get; set; }

        byte[] EcPrivateAccountLogKeyEncryptedWithPasswordHashPhase1 { get; set; }

        string PasswordHashPhase2 { get; set; }

        double CreditLimit { get; set; }

        TimeSpan CreditHalfLife { get; set; }
    }


}

namespace Dpm.Identity.Domain;

public enum TokenPurpose : byte
{
    EmailVerification = 1,
    PasswordReset = 2,
}

namespace Dpm.Marketplace.Domain;

public enum KycStatus : byte
{
    None = 0,
    Submitted = 1,
    Verified = 2,
    Rejected = 3,
}

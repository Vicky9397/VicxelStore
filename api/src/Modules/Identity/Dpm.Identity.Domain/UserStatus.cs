namespace Dpm.Identity.Domain;

public enum UserStatus : byte
{
    Active = 1,
    Locked = 2,
    Suspended = 3,
    Deleted = 4,
}

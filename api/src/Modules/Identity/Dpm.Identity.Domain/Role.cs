namespace Dpm.Identity.Domain;

public sealed class Role
{
    public const string Buyer = "Buyer";
    public const string Seller = "Seller";
    public const string Admin = "Admin";
    public const string Moderator = "Moderator";
    public const string Support = "Support";

    public int Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public bool IsStaffRole { get; private set; }

    private Role()
    {
    }
}

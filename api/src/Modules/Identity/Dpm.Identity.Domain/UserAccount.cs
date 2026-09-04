using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;
using Dpm.Identity.Domain.Events;

namespace Dpm.Identity.Domain;

/// <summary>
/// Identity aggregate root (identity.Users). Owns credential state, lockout and
/// email-verification invariants. Auth module rules (spec 02 section 2.4):
/// lockout after 5 failed logins in 15 minutes with progressive backoff;
/// unverified accounts cannot checkout, download or publish.
/// </summary>
public sealed class UserAccount : AggregateRoot
{
    public const int MaxFailedLogins = 5;
    private static readonly TimeSpan BaseLockout = TimeSpan.FromMinutes(15);

    private readonly List<Role> _roles = [];

    public string Email { get; private set; } = string.Empty;

    public string EmailNormalized { get; private set; } = string.Empty;

    public bool EmailVerified { get; private set; }

    public string? PasswordHash { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public UserStatus Status { get; private set; } = UserStatus.Active;

    public bool MfaEnabled { get; private set; }

    public int FailedLogins { get; private set; }

    public DateTime? LockoutEndUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public bool IsDeleted { get; private set; }

    public IReadOnlyCollection<Role> Roles => _roles.AsReadOnly();

    private UserAccount()
    {
    }

    public static UserAccount Register(string email, string displayName, string passwordHash, IClock clock)
    {
        Guard.AgainstNullOrWhiteSpace(email, nameof(email));
        Guard.AgainstNullOrWhiteSpace(displayName, nameof(displayName));
        Guard.AgainstNullOrWhiteSpace(passwordHash, nameof(passwordHash));

        var now = clock.UtcNow;
        var user = new UserAccount
        {
            PublicId = Guid.NewGuid(),
            Email = email.Trim(),
            EmailNormalized = NormalizeEmail(email),
            DisplayName = displayName.Trim(),
            PasswordHash = passwordHash,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        user.Raise(new UserRegistered(user.PublicId, user.Email));
        user.Raise(new EmailVerificationRequested(user.PublicId, user.Email));
        return user;
    }

    public static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    public void AddRole(Role role)
    {
        if (_roles.All(r => r.Name != role.Name))
        {
            _roles.Add(role);
        }
    }

    public Result VerifyEmail(IClock clock)
    {
        if (EmailVerified)
        {
            return Result.Success();
        }

        EmailVerified = true;
        UpdatedAtUtc = clock.UtcNow;
        Raise(new EmailVerified(PublicId));
        return Result.Success();
    }

    public bool IsLockedOut(IClock clock) =>
        LockoutEndUtc.HasValue && LockoutEndUtc.Value > clock.UtcNow;

    /// <summary>
    /// Records a failed credential check. On reaching the threshold the account
    /// locks with progressive backoff: 15 min doubling per threshold breach.
    /// </summary>
    public void RecordFailedLogin(IClock clock)
    {
        FailedLogins++;
        UpdatedAtUtc = clock.UtcNow;

        if (FailedLogins < MaxFailedLogins)
        {
            return;
        }

        var breaches = FailedLogins - MaxFailedLogins;
        var multiplier = Math.Min(breaches, 5);
        var lockout = TimeSpan.FromTicks(BaseLockout.Ticks * (1L << multiplier));
        LockoutEndUtc = clock.UtcNow.Add(lockout);
        Raise(new UserLockedOut(PublicId, LockoutEndUtc.Value));
    }

    public void RecordSuccessfulLogin(IClock clock)
    {
        FailedLogins = 0;
        LockoutEndUtc = null;
        UpdatedAtUtc = clock.UtcNow;
    }

    public void ChangePasswordHash(string passwordHash, IClock clock)
    {
        Guard.AgainstNullOrWhiteSpace(passwordHash, nameof(passwordHash));
        PasswordHash = passwordHash;
        UpdatedAtUtc = clock.UtcNow;
    }
}

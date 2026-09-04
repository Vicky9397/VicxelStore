using Dpm.BuildingBlocks.Domain;

namespace Dpm.Identity.Domain;

/// <summary>
/// Rotating refresh token (identity.RefreshTokens). Only the SHA-256 hash is
/// stored. Tokens belong to a family; presenting an already-rotated or revoked
/// token is theft evidence and revokes the whole family (spec 08 section 8.2).
/// </summary>
public sealed class RefreshToken : Entity
{
    public long UserId { get; private set; }

    public Guid FamilyId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public string? ReplacedByHash { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private RefreshToken()
    {
    }

    public static RefreshToken Issue(long userId, Guid familyId, string tokenHash, DateTime expiresAtUtc, IClock clock)
    {
        Guard.AgainstNullOrWhiteSpace(tokenHash, nameof(tokenHash));
        return new RefreshToken
        {
            UserId = userId,
            FamilyId = familyId,
            TokenHash = tokenHash,
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = clock.UtcNow,
        };
    }

    public bool IsActive(IClock clock) =>
        RevokedAtUtc is null && ReplacedByHash is null && ExpiresAtUtc > clock.UtcNow;

    /// <summary>A token that was already rotated or revoked but is presented again.</summary>
    public bool IsReused(IClock clock) =>
        (RevokedAtUtc is not null || ReplacedByHash is not null) && ExpiresAtUtc > clock.UtcNow;

    public void MarkRotated(string replacedByHash, IClock clock)
    {
        ReplacedByHash = replacedByHash;
        RevokedAtUtc = clock.UtcNow;
    }

    public void Revoke(IClock clock)
    {
        RevokedAtUtc ??= clock.UtcNow;
    }
}

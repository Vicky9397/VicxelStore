using Dpm.BuildingBlocks.Domain;

namespace Dpm.Identity.Domain;

/// <summary>
/// Single-use purpose-bound token (identity.UserTokens): email verification
/// (24h TTL) and password reset (30 min TTL). Hash-only storage.
/// </summary>
public sealed class UserToken : Entity
{
    public long UserId { get; private set; }

    public TokenPurpose Purpose { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? ConsumedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private UserToken()
    {
    }

    public static UserToken Issue(long userId, TokenPurpose purpose, string tokenHash, TimeSpan ttl, IClock clock)
    {
        Guard.AgainstNullOrWhiteSpace(tokenHash, nameof(tokenHash));
        return new UserToken
        {
            UserId = userId,
            Purpose = purpose,
            TokenHash = tokenHash,
            ExpiresAtUtc = clock.UtcNow.Add(ttl),
            CreatedAtUtc = clock.UtcNow,
        };
    }

    public bool IsUsable(IClock clock) => ConsumedAtUtc is null && ExpiresAtUtc > clock.UtcNow;

    public void Consume(IClock clock) => ConsumedAtUtc = clock.UtcNow;
}

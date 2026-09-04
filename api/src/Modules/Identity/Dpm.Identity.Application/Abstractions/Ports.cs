using Dpm.Identity.Domain;

namespace Dpm.Identity.Application.Abstractions;

public interface IUserRepository
{
    Task<UserAccount?> FindByEmailAsync(string emailNormalized, CancellationToken ct);

    Task<UserAccount?> FindByPublicIdAsync(Guid publicId, CancellationToken ct);

    Task<UserAccount?> FindByIdAsync(long id, CancellationToken ct);

    Task<bool> EmailExistsAsync(string emailNormalized, CancellationToken ct);

    void Add(UserAccount user);

    Task<Role?> FindRoleAsync(string name, CancellationToken ct);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct);

    Task<IReadOnlyList<RefreshToken>> FindFamilyAsync(Guid familyId, CancellationToken ct);

    void Add(RefreshToken token);
}

public interface IUserTokenRepository
{
    Task<UserToken?> FindUsableByHashAsync(string tokenHash, TokenPurpose purpose, CancellationToken ct);

    Task<int> CountIssuedSinceAsync(long userId, TokenPurpose purpose, DateTime sinceUtc, CancellationToken ct);

    void Add(UserToken token);
}

public interface IIdentityUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}

public interface ITokenService
{
    AccessToken IssueAccessToken(UserAccount user);

    /// <summary>Generates a cryptographically random opaque token and its SHA-256 hex hash.</summary>
    (string Token, string Hash) GenerateOpaqueToken();

    string HashOpaqueToken(string token);

    TimeSpan RefreshTokenLifetime { get; }
}

public sealed record AccessToken(string Token, int ExpiresInSeconds);

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct);
}

/// <summary>Resolves the authenticated user from the current request context.</summary>
public interface ICurrentUser
{
    Guid? PublicId { get; }
}

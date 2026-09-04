using System.Security.Cryptography;
using System.Text;
using Dpm.Identity.Application.Abstractions;
using Dpm.Identity.Domain;

namespace Dpm.UnitTests.Application;

/// <summary>
/// In-memory doubles for the Identity ports. Ids are assigned on save so handler
/// logic that depends on a persisted id behaves as it does against EF.
/// </summary>
public sealed class FakeIdentityStore : IUserRepository, IRefreshTokenRepository, IUserTokenRepository, IIdentityUnitOfWork
{
    private readonly List<UserAccount> _users = [];
    private readonly List<RefreshToken> _refreshTokens = [];
    private readonly List<UserToken> _userTokens = [];
    private readonly List<UserAccount> _pendingUsers = [];
    private readonly List<RefreshToken> _pendingRefreshTokens = [];
    private readonly List<UserToken> _pendingUserTokens = [];
    private long _nextId = 1;

    public IReadOnlyList<UserAccount> Users => _users;

    public IReadOnlyList<RefreshToken> RefreshTokens => _refreshTokens;

    public IReadOnlyList<UserToken> UserTokens => _userTokens;

    public Task<UserAccount?> FindByEmailAsync(string emailNormalized, CancellationToken ct) =>
        Task.FromResult(_users.FirstOrDefault(u => u.EmailNormalized == emailNormalized));

    public Task<UserAccount?> FindByPublicIdAsync(Guid publicId, CancellationToken ct) =>
        Task.FromResult(_users.FirstOrDefault(u => u.PublicId == publicId));

    public Task<UserAccount?> FindByIdAsync(long id, CancellationToken ct) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

    public Task<bool> EmailExistsAsync(string emailNormalized, CancellationToken ct) =>
        Task.FromResult(_users.Any(u => u.EmailNormalized == emailNormalized));

    public void Add(UserAccount user) => _pendingUsers.Add(user);

    public Task<Role?> FindRoleAsync(string name, CancellationToken ct) => Task.FromResult<Role?>(null);

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct) =>
        Task.FromResult(_refreshTokens.FirstOrDefault(t => t.TokenHash == tokenHash));

    public Task<IReadOnlyList<RefreshToken>> FindFamilyAsync(Guid familyId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<RefreshToken>>(
            _refreshTokens.Where(t => t.FamilyId == familyId).ToList());

    public void Add(RefreshToken token) => _pendingRefreshTokens.Add(token);

    public Task<UserToken?> FindUsableByHashAsync(string tokenHash, TokenPurpose purpose, CancellationToken ct) =>
        Task.FromResult(_userTokens.FirstOrDefault(
            t => t.TokenHash == tokenHash && t.Purpose == purpose && t.ConsumedAtUtc is null));

    public Task<int> CountIssuedSinceAsync(long userId, TokenPurpose purpose, DateTime sinceUtc, CancellationToken ct) =>
        Task.FromResult(_userTokens.Count(
            t => t.UserId == userId && t.Purpose == purpose && t.CreatedAtUtc >= sinceUtc));

    public void Add(UserToken token) => _pendingUserTokens.Add(token);

    public Task SaveChangesAsync(CancellationToken ct)
    {
        foreach (var user in _pendingUsers)
        {
            AssignId(user, _nextId++);
            _users.Add(user);
        }

        foreach (var token in _pendingRefreshTokens)
        {
            AssignId(token, _nextId++);
            _refreshTokens.Add(token);
        }

        foreach (var token in _pendingUserTokens)
        {
            AssignId(token, _nextId++);
            _userTokens.Add(token);
        }

        _pendingUsers.Clear();
        _pendingRefreshTokens.Clear();
        _pendingUserTokens.Clear();
        return Task.CompletedTask;
    }

    private static void AssignId(object entity, long id) =>
        entity.GetType()
            .GetProperty("Id")!
            .SetValue(entity, id);
}

public sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => "hashed:" + password;

    public bool Verify(string password, string passwordHash) => passwordHash == "hashed:" + password;
}

public sealed class FakeTokenService : ITokenService
{
    private int _counter;

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(14);

    public AccessToken IssueAccessToken(UserAccount user) => new($"access-{user.PublicId}", 900);

    public (string Token, string Hash) GenerateOpaqueToken()
    {
        var token = $"token-{++_counter}";
        return (token, HashOpaqueToken(token));
    }

    public string HashOpaqueToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}

public sealed class RecordingEmailComposer : IVerificationEmailComposer
{
    public List<(string Email, string Token)> Sent { get; } = [];

    public Task SendVerificationAsync(string email, string displayName, string token, CancellationToken ct)
    {
        Sent.Add((email, token));
        return Task.CompletedTask;
    }
}

public sealed class StubCurrentUser(Guid? publicId) : ICurrentUser
{
    public Guid? PublicId { get; } = publicId;
}

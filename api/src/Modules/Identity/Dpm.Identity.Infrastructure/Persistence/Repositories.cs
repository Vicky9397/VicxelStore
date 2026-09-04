using Dpm.BuildingBlocks.Domain;
using Dpm.Identity.Application.Abstractions;
using Dpm.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dpm.Identity.Infrastructure.Persistence;

public sealed class UserRepository(IdentityDbContext dbContext) : IUserRepository
{
    public Task<UserAccount?> FindByEmailAsync(string emailNormalized, CancellationToken ct) =>
        dbContext.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.EmailNormalized == emailNormalized, ct);

    public Task<UserAccount?> FindByPublicIdAsync(Guid publicId, CancellationToken ct) =>
        dbContext.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.PublicId == publicId, ct);

    public Task<UserAccount?> FindByIdAsync(long id, CancellationToken ct) =>
        dbContext.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<bool> EmailExistsAsync(string emailNormalized, CancellationToken ct) =>
        dbContext.Users.IgnoreQueryFilters().AnyAsync(u => u.EmailNormalized == emailNormalized, ct);

    public void Add(UserAccount user) => dbContext.Users.Add(user);

    public Task<Role?> FindRoleAsync(string name, CancellationToken ct) =>
        dbContext.Roles.FirstOrDefaultAsync(r => r.Name == name, ct);
}

public sealed class RefreshTokenRepository(IdentityDbContext dbContext) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct) =>
        dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task<IReadOnlyList<RefreshToken>> FindFamilyAsync(Guid familyId, CancellationToken ct) =>
        await dbContext.RefreshTokens.Where(t => t.FamilyId == familyId).ToListAsync(ct);

    public void Add(RefreshToken token) => dbContext.RefreshTokens.Add(token);
}

public sealed class UserTokenRepository(IdentityDbContext dbContext, IClock clock) : IUserTokenRepository
{
    public Task<UserToken?> FindUsableByHashAsync(string tokenHash, TokenPurpose purpose, CancellationToken ct)
    {
        var now = clock.UtcNow;
        return dbContext.UserTokens.FirstOrDefaultAsync(
            t => t.TokenHash == tokenHash
                && t.Purpose == purpose
                && t.ConsumedAtUtc == null
                && t.ExpiresAtUtc > now,
            ct);
    }

    public Task<int> CountIssuedSinceAsync(long userId, TokenPurpose purpose, DateTime sinceUtc, CancellationToken ct) =>
        dbContext.UserTokens.CountAsync(
            t => t.UserId == userId && t.Purpose == purpose && t.CreatedAtUtc >= sinceUtc,
            ct);

    public void Add(UserToken token) => dbContext.UserTokens.Add(token);
}

public sealed class IdentityUnitOfWork(IdentityDbContext dbContext) : IIdentityUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}

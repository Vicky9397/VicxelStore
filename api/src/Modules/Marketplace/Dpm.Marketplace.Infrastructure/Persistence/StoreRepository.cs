using Dpm.Marketplace.Application.Abstractions;
using Dpm.Marketplace.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dpm.Marketplace.Infrastructure.Persistence;

public sealed class StoreRepository(MarketplaceDbContext dbContext) : IStoreRepository
{
    public Task<Store?> FindByOwnerUserIdAsync(long ownerUserId, CancellationToken ct) =>
        dbContext.Stores.FirstOrDefaultAsync(s => s.OwnerUserId == ownerUserId, ct);

    public Task<Store?> FindBySlugAsync(string slug, CancellationToken ct) =>
        dbContext.Stores.FirstOrDefaultAsync(s => s.Slug == slug, ct);

    public Task<Store?> FindByPublicIdAsync(Guid publicId, CancellationToken ct) =>
        dbContext.Stores.FirstOrDefaultAsync(s => s.PublicId == publicId, ct);

    public Task<Store?> FindByIdAsync(long storeId, CancellationToken ct) =>
        dbContext.Stores.FirstOrDefaultAsync(s => s.Id == storeId, ct);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct) =>
        dbContext.Stores.AnyAsync(s => s.Slug == slug, ct);

    public Task<bool> OwnerHasStoreAsync(long ownerUserId, CancellationToken ct) =>
        dbContext.Stores.AnyAsync(s => s.OwnerUserId == ownerUserId, ct);

    public void Add(Store store) => dbContext.Stores.Add(store);
}

public sealed class MarketplaceUnitOfWork(MarketplaceDbContext dbContext) : IMarketplaceUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}

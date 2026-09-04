using Dpm.Marketplace.Domain;

namespace Dpm.Marketplace.Application.Abstractions;

public interface IStoreRepository
{
    Task<Store?> FindByOwnerUserIdAsync(long ownerUserId, CancellationToken ct);

    Task<Store?> FindBySlugAsync(string slug, CancellationToken ct);

    Task<Store?> FindByPublicIdAsync(Guid publicId, CancellationToken ct);

    Task<Store?> FindByIdAsync(long storeId, CancellationToken ct);

    Task<bool> SlugExistsAsync(string slug, CancellationToken ct);

    Task<bool> OwnerHasStoreAsync(long ownerUserId, CancellationToken ct);

    void Add(Store store);
}

public interface IMarketplaceUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>
/// Resolves the caller's identity. Marketplace stores the internal user id on a
/// store, so it needs both forms; Identity remains the owner of the user record.
/// </summary>
public interface ICurrentSeller
{
    Guid? UserPublicId { get; }

    Task<long?> ResolveUserIdAsync(CancellationToken ct);
}

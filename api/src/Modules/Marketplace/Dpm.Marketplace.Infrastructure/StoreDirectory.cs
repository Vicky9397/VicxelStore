using Dpm.Marketplace.Application.Abstractions;
using Dpm.Marketplace.Contracts;

namespace Dpm.Marketplace.Infrastructure;

/// <summary>
/// Implements the module's public surface for downstream modules. Everything
/// they may know about a store passes through this projection.
/// </summary>
public sealed class StoreDirectory(IStoreRepository stores, ICurrentSeller currentSeller) : IStoreDirectory
{
    public async Task<StoreSummary?> FindForCurrentUserAsync(CancellationToken ct)
    {
        if (await currentSeller.ResolveUserIdAsync(ct) is not { } userId)
        {
            return null;
        }

        var store = await stores.FindByOwnerUserIdAsync(userId, ct);
        return store is null ? null : ToSummary(store);
    }

    public async Task<StoreSummary?> FindByIdAsync(long storeId, CancellationToken ct)
    {
        var store = await stores.FindByIdAsync(storeId, ct);
        return store is null ? null : ToSummary(store);
    }

    public async Task<decimal?> FindCommissionOverrideAsync(long storeId, CancellationToken ct)
    {
        var store = await stores.FindByIdAsync(storeId, ct);
        return store?.Profile.CommissionOverridePct;
    }

    private static StoreSummary ToSummary(Domain.Store store) =>
        new(store.Id, store.PublicId, store.Slug, store.Name, store.IsActive, store.CanPublish);
}

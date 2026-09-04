using Dpm.BuildingBlocks.Application;
using Dpm.Marketplace.Application.Abstractions;
using Dpm.Marketplace.Domain;

namespace Dpm.Marketplace.Application;

/// <summary>
/// Resource-based authorization for store commands: the caller must be signed in
/// and own the store they name (spec 08 section 8.3). Enforced in the
/// Application layer so no controller can bypass it.
/// </summary>
public static class OwnedStore
{
    public static async Task<Result<Store>> ResolveAsync(
        IStoreRepository stores,
        ICurrentSeller currentSeller,
        Guid storePublicId,
        CancellationToken ct)
    {
        if (await currentSeller.ResolveUserIdAsync(ct) is not { } userId)
        {
            return Result.Failure<Store>(MarketplaceErrors.NotAuthenticated);
        }

        var store = await stores.FindByPublicIdAsync(storePublicId, ct);
        if (store is null)
        {
            return Result.Failure<Store>(MarketplaceErrors.StoreNotFound);
        }

        return store.OwnerUserId == userId
            ? Result.Success(store)
            : Result.Failure<Store>(MarketplaceErrors.NotStoreOwner);
    }
}

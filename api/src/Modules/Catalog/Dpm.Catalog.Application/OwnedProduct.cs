using Dpm.BuildingBlocks.Application;
using Dpm.Catalog.Application.Abstractions;
using Dpm.Catalog.Domain;
using Dpm.Marketplace.Contracts;

namespace Dpm.Catalog.Application;

/// <summary>
/// Resource-based authorization for product commands: the caller must own an
/// active store and that store must own the product. Enforced in the Application
/// layer so no controller can bypass it (spec 08 section 8.3).
/// </summary>
public static class OwnedProduct
{
    public static async Task<Result<StoreSummary>> ResolveStoreAsync(
        IStoreDirectory stores,
        CancellationToken ct)
    {
        var store = await stores.FindForCurrentUserAsync(ct);
        if (store is null)
        {
            return Result.Failure<StoreSummary>(CatalogErrors.NoStore);
        }

        return store.IsActive
            ? Result.Success(store)
            : Result.Failure<StoreSummary>(CatalogErrors.StoreSuspended);
    }

    public static async Task<Result<(Product Product, StoreSummary Store)>> ResolveAsync(
        IProductRepository products,
        IStoreDirectory stores,
        Guid productPublicId,
        CancellationToken ct)
    {
        var store = await ResolveStoreAsync(stores, ct);
        if (store.IsFailure)
        {
            return Result.Failure<(Product, StoreSummary)>(store.Error);
        }

        var product = await products.FindByPublicIdAsync(productPublicId, ct);
        if (product is null)
        {
            return Result.Failure<(Product, StoreSummary)>(CatalogErrors.ProductNotFound);
        }

        return product.StoreId == store.Value.StoreId
            ? Result.Success((product, store.Value))
            : Result.Failure<(Product, StoreSummary)>(CatalogErrors.NotProductOwner);
    }
}

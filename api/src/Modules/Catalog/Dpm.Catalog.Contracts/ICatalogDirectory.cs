namespace Dpm.Catalog.Contracts;

/// <summary>
/// The Catalog module's public surface. The Files module uses it to resolve and
/// authorize an upload target without reading catalog.* directly.
/// </summary>
public interface ICatalogDirectory
{
    Task<VariantRef?> FindVariantAsync(Guid variantPublicId, CancellationToken ct);

    Task<VariantRef?> FindVariantByIdAsync(long variantId, CancellationToken ct);

    /// <summary>The product's latest version, which a new file attaches to.</summary>
    Task<long?> FindLatestVersionIdAsync(long productId, CancellationToken ct);

    /// <summary>
    /// Everything checkout needs to price and license one variant, resolved in a
    /// single call. Returns null unless the product is published and the variant
    /// is active, so an unavailable item cannot be charged for.
    /// </summary>
    Task<SellableVariant?> FindSellableVariantAsync(long variantId, CancellationToken ct);

    /// <summary>The same lookup addressed by the public id the API exposes.</summary>
    Task<SellableVariant?> FindSellableVariantByPublicIdAsync(Guid variantPublicId, CancellationToken ct);

    /// <summary>The category's commission rate, which the platform default backs up.</summary>
    Task<decimal?> FindCategoryCommissionAsync(int categoryId, CancellationToken ct);
}

public sealed record SellableVariant(
    long VariantId,
    Guid VariantPublicId,
    string VariantName,
    long ProductId,
    string ProductSlug,
    string ProductTitle,
    long StoreId,
    int CategoryId,
    long VersionId,
    decimal PriceAmount,
    string PriceCurrency,
    byte LicenseType,
    int DownloadLimit);

/// <param name="OwnerStoreId">The store that owns the product this variant belongs to.</param>
public sealed record VariantRef(
    long VariantId,
    Guid PublicId,
    long ProductId,
    long OwnerStoreId,
    string ProductTitle);

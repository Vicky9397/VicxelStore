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
}

/// <param name="OwnerStoreId">The store that owns the product this variant belongs to.</param>
public sealed record VariantRef(
    long VariantId,
    Guid PublicId,
    long ProductId,
    long OwnerStoreId,
    string ProductTitle);

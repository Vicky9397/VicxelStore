using Dpm.Catalog.Application.Abstractions;
using Dpm.Catalog.Contracts;

namespace Dpm.Catalog.Infrastructure;

/// <summary>
/// Implements the module's public surface. The Files module resolves an upload
/// target through this rather than reading catalog.* directly.
/// </summary>
public sealed class CatalogDirectory(IProductRepository products, ICategoryRepository categories)
    : ICatalogDirectory
{
    public async Task<VariantRef?> FindVariantAsync(Guid variantPublicId, CancellationToken ct)
    {
        var product = await products.FindByVariantPublicIdAsync(variantPublicId, ct);
        var variant = product?.Variants.FirstOrDefault(v => v.PublicId == variantPublicId);
        return product is null || variant is null
            ? null
            : new VariantRef(variant.Id, variant.PublicId, product.Id, product.StoreId, product.Title);
    }

    public async Task<VariantRef?> FindVariantByIdAsync(long variantId, CancellationToken ct)
    {
        var product = await products.FindByVariantIdAsync(variantId, ct);
        var variant = product?.Variants.FirstOrDefault(v => v.Id == variantId);
        return product is null || variant is null
            ? null
            : new VariantRef(variant.Id, variant.PublicId, product.Id, product.StoreId, product.Title);
    }

    public async Task<SellableVariant?> FindSellableVariantAsync(long variantId, CancellationToken ct)
    {
        var product = await products.FindByVariantIdAsync(variantId, ct);
        var variant = product?.Variants.FirstOrDefault(v => v.Id == variantId);
        if (product is null || variant is null || !variant.IsActive || !product.IsPubliclyVisible)
        {
            return null;
        }

        var version = product.Versions.OrderByDescending(v => v.ReleasedAtUtc).FirstOrDefault();
        if (version is null)
        {
            return null;
        }

        return new SellableVariant(
            variant.Id,
            variant.PublicId,
            variant.Name,
            product.Id,
            product.Slug,
            product.Title,
            product.StoreId,
            product.CategoryId,
            version.Id,
            variant.PriceAmount,
            variant.PriceCurrency,
            (byte)variant.LicenseType,
            variant.DownloadLimit);
    }

    public async Task<SellableVariant?> FindSellableVariantByPublicIdAsync(
        Guid variantPublicId,
        CancellationToken ct)
    {
        var product = await products.FindByVariantPublicIdAsync(variantPublicId, ct);
        var variant = product?.Variants.FirstOrDefault(v => v.PublicId == variantPublicId);
        return variant is null ? null : await FindSellableVariantAsync(variant.Id, ct);
    }

    public async Task<decimal?> FindCategoryCommissionAsync(int categoryId, CancellationToken ct)
    {
        var category = await categories.FindByIdAsync(categoryId, ct);
        return category?.CommissionPct;
    }

    public async Task<long?> FindLatestVersionIdAsync(long productId, CancellationToken ct)
    {
        var product = await products.FindByIdAsync(productId, ct);
        var latest = product?.Versions.OrderByDescending(v => v.ReleasedAtUtc).FirstOrDefault();
        return latest?.Id;
    }
}

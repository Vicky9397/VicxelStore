using Dpm.Catalog.Domain;

namespace Dpm.Catalog.Application.Contracts;

public static class ProductMapping
{
    public static VariantDto ToDto(this ProductVariant variant) => new(
        variant.PublicId,
        variant.Name,
        new MoneyDto(variant.PriceAmount, variant.PriceCurrency),
        variant.LicenseType.ToString(),
        variant.DownloadLimit,
        variant.IsActive,
        variant.HasCleanFile);

    public static SellerProductDetailDto ToSellerDetailDto(this Product product, string categorySlug) => new(
        product.PublicId,
        product.Slug,
        product.Title,
        product.Description,
        categorySlug,
        product.Status.ToString(),
        product.CheckPublishReadiness().IsSuccess,
        product.Variants.Select(v => v.ToDto()).ToList(),
        product.Versions
            .OrderByDescending(v => v.ReleasedAtUtc)
            .Select(v => new VersionDto(v.VersionNumber, v.Changelog, v.ReleasedAtUtc))
            .ToList());

    public static SellerProductDto ToSellerDto(this Product product) => new(
        product.PublicId,
        product.Slug,
        product.Title,
        product.Status.ToString(),
        product.Variants.Count,
        product.CheckPublishReadiness().IsSuccess,
        product.UpdatedAtUtc);
}

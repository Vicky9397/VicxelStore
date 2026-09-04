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

    public static SellerProductDto ToSellerDto(this Product product) => new(
        product.PublicId,
        product.Slug,
        product.Title,
        product.Status.ToString(),
        product.Variants.Count,
        product.CheckPublishReadiness().IsSuccess,
        product.UpdatedAtUtc);
}

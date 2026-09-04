using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;

namespace Dpm.Catalog.Domain;

/// <summary>
/// A priced edition of a product (catalog.ProductVariants). Part of the Product
/// aggregate. CleanFileCount is projected from the Files module's FileScanned
/// event and is what the publish invariant reads.
/// </summary>
public sealed class ProductVariant : Entity
{
    public Guid PublicId { get; private set; }

    public long ProductId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public decimal PriceAmount { get; private set; }

    public string PriceCurrency { get; private set; } = string.Empty;

    public LicenseType LicenseType { get; private set; }

    public int DownloadLimit { get; private set; }

    public bool IsActive { get; private set; } = true;

    public int CleanFileCount { get; private set; }

    private ProductVariant()
    {
    }

    internal static Result<ProductVariant> Create(
        string name,
        Money price,
        LicenseType licenseType,
        int downloadLimit)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<ProductVariant>(Error.Validation("VALIDATION_ERROR", "Variant name is required."));
        }

        if (price.Amount <= 0)
        {
            return Result.Failure<ProductVariant>(
                Error.Validation("VALIDATION_ERROR", "Variant price must be greater than zero."));
        }

        if (downloadLimit < 1)
        {
            return Result.Failure<ProductVariant>(
                Error.Validation("VALIDATION_ERROR", "Download limit must be at least 1."));
        }

        return Result.Success(new ProductVariant
        {
            PublicId = Guid.NewGuid(),
            Name = name.Trim(),
            PriceAmount = price.Amount,
            PriceCurrency = price.Currency,
            LicenseType = licenseType,
            DownloadLimit = downloadLimit,
        });
    }

    internal Result Update(string name, Money price, LicenseType licenseType, int downloadLimit, bool isActive)
    {
        var replacement = Create(name, price, licenseType, downloadLimit);
        if (replacement.IsFailure)
        {
            return Result.Failure(replacement.Error);
        }

        Name = replacement.Value.Name;
        PriceAmount = replacement.Value.PriceAmount;
        PriceCurrency = replacement.Value.PriceCurrency;
        LicenseType = replacement.Value.LicenseType;
        DownloadLimit = replacement.Value.DownloadLimit;
        IsActive = isActive;
        return Result.Success();
    }

    public Money Price => Money.Create(PriceAmount, PriceCurrency).Value;

    public bool HasCleanFile => CleanFileCount > 0;

    /// <summary>Applied from the Files module's FileScanned event; never set by a command.</summary>
    internal void ApplyFileReadiness(int cleanFileCount) => CleanFileCount = Math.Max(0, cleanFileCount);
}

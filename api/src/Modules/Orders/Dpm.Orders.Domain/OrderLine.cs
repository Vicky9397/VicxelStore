using Dpm.BuildingBlocks.Domain;

namespace Dpm.Orders.Domain;

public sealed class OrderLine : Entity
{
    public long OrderId { get; private set; }

    public long ProductId { get; private set; }

    public long VariantId { get; private set; }

    public long VersionId { get; private set; }

    public long StoreId { get; private set; }

    public decimal UnitAmount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public decimal CommissionPct { get; private set; }

    public byte LicenseType { get; private set; }

    /// <summary>
    /// Carried from the variant so the license issued for this line keeps the
    /// quota the buyer actually paid for, even if the seller changes it later.
    /// </summary>
    public int DownloadLimit { get; private set; }

    private OrderLine()
    {
    }

    internal static OrderLine Create(
        long productId,
        long variantId,
        long versionId,
        long storeId,
        Money unitPrice,
        decimal commissionPct,
        byte licenseType,
        int downloadLimit) => new()
        {
            ProductId = productId,
            VariantId = variantId,
            VersionId = versionId,
            StoreId = storeId,
            UnitAmount = unitPrice.Amount,
            Currency = unitPrice.Currency,
            CommissionPct = commissionPct,
            LicenseType = licenseType,
            DownloadLimit = downloadLimit,
        };

    public Money UnitPrice => Money.Create(UnitAmount, Currency).Value;

    /// <summary>Platform commission on this line, rounded to the money scale.</summary>
    public Money Commission => UnitPrice.MultiplyPercent(CommissionPct);

    /// <summary>What the seller earns from this line, before the payout hold.</summary>
    public Money SellerEarning => UnitPrice.Subtract(Commission);
}

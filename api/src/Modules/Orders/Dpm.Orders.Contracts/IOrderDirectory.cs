namespace Dpm.Orders.Contracts;

/// <summary>
/// The Orders module's public surface. Ledger reads an order to post its journal
/// and Downloads resolves a license through this, rather than reading orders.*.
/// </summary>
public interface IOrderDirectory
{
    Task<OrderSnapshot?> FindOrderAsync(long orderId, CancellationToken ct);

    Task<LicenseSnapshot?> FindLicenseAsync(Guid licensePublicId, CancellationToken ct);

    /// <summary>Records a completed download against the license's quota.</summary>
    Task<bool> TryConsumeDownloadAsync(Guid licensePublicId, CancellationToken ct);
}

/// <param name="CommissionPct">
/// The commission rate resolved and frozen at checkout. The ledger uses this
/// snapshot so a later rate change never rewrites a settled sale.
/// </param>
public sealed record OrderLineSnapshot(
    long OrderLineId,
    long StoreId,
    long VariantId,
    decimal UnitAmount,
    decimal CommissionPct);

public sealed record OrderSnapshot(
    long OrderId,
    Guid PublicId,
    string Currency,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal GrandTotal,
    string Status,
    IReadOnlyList<OrderLineSnapshot> Lines);

public sealed record LicenseSnapshot(
    long LicenseId,
    Guid PublicId,
    long BuyerUserId,
    long VariantId,
    long VersionId,
    int DownloadLimit,
    int DownloadsUsed,
    DateTime? ExpiresAtUtc);

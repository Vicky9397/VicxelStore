using Dpm.Orders.Application.Abstractions;
using Dpm.Orders.Contracts;

namespace Dpm.Orders.Infrastructure;

/// <summary>
/// The Orders module's public surface. Ledger reads a settled order through this
/// to post its journal, and Downloads resolves and spends a license through it.
/// </summary>
public sealed class OrderDirectory(
    IOrderRepository orders,
    IOrdersUnitOfWork unitOfWork,
    BuildingBlocks.Domain.IClock clock)
    : IOrderDirectory
{
    public async Task<OrderSnapshot?> FindOrderAsync(long orderId, CancellationToken ct)
    {
        var order = await orders.FindByIdAsync(orderId, ct);
        if (order is null)
        {
            return null;
        }

        return new OrderSnapshot(
            order.Id,
            order.PublicId,
            order.Currency,
            order.Subtotal,
            order.DiscountTotal,
            order.TaxTotal,
            order.GrandTotal,
            order.Status.ToString(),
            order.Lines
                .Select(l => new OrderLineSnapshot(l.Id, l.StoreId, l.VariantId, l.UnitAmount, l.CommissionPct))
                .ToList());
    }

    public async Task<LicenseSnapshot?> FindLicenseAsync(Guid licensePublicId, CancellationToken ct)
    {
        var order = await orders.FindByLicensePublicIdAsync(licensePublicId, ct);
        var license = order?.Licenses.FirstOrDefault(l => l.PublicId == licensePublicId);
        return license is null
            ? null
            : new LicenseSnapshot(
                license.Id,
                license.PublicId,
                license.BuyerUserId,
                license.VariantId,
                license.VersionId,
                license.DownloadLimit,
                license.DownloadsUsed,
                license.ExpiresAtUtc);
    }

    /// <summary>
    /// Spends one download from the license quota. The License aggregate owns the
    /// rule, so quota can never be bypassed by a caller that forgets to check.
    /// </summary>
    public async Task<bool> TryConsumeDownloadAsync(Guid licensePublicId, CancellationToken ct)
    {
        var order = await orders.FindByLicensePublicIdAsync(licensePublicId, ct);
        var license = order?.Licenses.FirstOrDefault(l => l.PublicId == licensePublicId);
        if (license is null)
        {
            return false;
        }

        var consumed = license.ConsumeDownload(clock);
        if (consumed.IsFailure)
        {
            return false;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

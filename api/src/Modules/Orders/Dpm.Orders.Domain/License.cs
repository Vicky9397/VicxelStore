using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;

namespace Dpm.Orders.Domain;

/// <summary>
/// A buyer's entitlement to download a purchased variant (orders.Licenses).
/// Issued only when payment is captured, and it is what the Downloads module
/// checks before ever issuing a URL (spec 02 DL-02).
/// </summary>
public sealed class License : Entity
{
    public Guid PublicId { get; private set; }

    public long OrderLineId { get; private set; }

    public long BuyerUserId { get; private set; }

    public long VariantId { get; private set; }

    public long VersionId { get; private set; }

    public int DownloadLimit { get; private set; }

    public int DownloadsUsed { get; private set; }

    public DateTime? ExpiresAtUtc { get; private set; }

    public DateTime IssuedAtUtc { get; private set; }

    private License()
    {
    }

    internal static License Issue(OrderLine line, long buyerUserId, IClock clock) => new()
    {
        PublicId = Guid.NewGuid(),
        OrderLineId = line.Id,
        BuyerUserId = buyerUserId,
        VariantId = line.VariantId,
        VersionId = line.VersionId,
        DownloadLimit = line.DownloadLimit,
        IssuedAtUtc = clock.UtcNow,
    };

    public bool IsExpired(IClock clock) => ExpiresAtUtc is { } expiry && expiry <= clock.UtcNow;

    public bool HasQuotaRemaining => DownloadsUsed < DownloadLimit;

    /// <summary>
    /// Spends one download from the quota. Refused once the quota is exhausted or
    /// the license has expired, which is what turns into 429 DOWNLOAD_LIMIT and
    /// 403 NO_ENTITLEMENT at the edge.
    /// </summary>
    public Result ConsumeDownload(IClock clock)
    {
        if (IsExpired(clock))
        {
            return Result.Failure(Error.Forbidden("NO_ENTITLEMENT", "This license has expired."));
        }

        if (!HasQuotaRemaining)
        {
            return Result.Failure(Error.RateLimited(
                "DOWNLOAD_LIMIT",
                "You have used every download this license allows."));
        }

        DownloadsUsed++;
        return Result.Success();
    }
}

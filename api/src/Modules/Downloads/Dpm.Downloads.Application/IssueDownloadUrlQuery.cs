using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;
using Dpm.Downloads.Application.Abstractions;
using Dpm.Files.Contracts;
using Dpm.Orders.Contracts;

namespace Dpm.Downloads.Application;

public sealed record IssueDownloadUrlQuery(Guid LicenseId, Guid FileId) : IQuery<DownloadUrlDto>;

public sealed record DownloadUrlDto(string Url, int ExpiresInSeconds);

public static class DownloadErrors
{
    public static readonly Error NotAuthenticated =
        Error.Unauthorized("UNAUTHENTICATED", "Sign in to download.");

    /// <summary>
    /// Used both when no license exists and when it belongs to someone else, so
    /// the response cannot be used to discover other buyers' licenses.
    /// </summary>
    public static readonly Error NoEntitlement =
        Error.Forbidden("NO_ENTITLEMENT", "You do not have a license for this file.");

    public static readonly Error DownloadLimit =
        Error.RateLimited("DOWNLOAD_LIMIT", "You have used every download this license allows.");

    public static readonly Error FileNotReady =
        Error.Conflict("FILE_NOT_READY", "This file is still being scanned. Try again shortly.");

    public static readonly Error FileQuarantined =
        Error.Gone("FILE_QUARANTINED", "This file failed a virus scan and cannot be downloaded.");
}

/// <summary>
/// Issues a download URL for a purchased file (spec 02 DL-02).
///
/// Four gates run in order, and every one of them is server-side: the caller
/// must own an unexpired license, the file must belong to that license's
/// variant, the file must have passed its virus scan, and the license must have
/// quota left. Only then is a short-lived signed URL minted, and the quota is
/// spent as part of issuing it.
/// </summary>
public sealed class IssueDownloadUrlQueryHandler(
    IOrderDirectory orders,
    IFileDirectory files,
    ISignedUrlFactory signedUrls,
    IDownloadLog downloadLog,
    ICurrentDownloader downloader,
    IClock clock)
    : IQueryHandler<IssueDownloadUrlQuery, DownloadUrlDto>
{
    /// <summary>Signed URLs live 15 minutes (spec 02 DL-02).</summary>
    private static readonly TimeSpan UrlLifetime = TimeSpan.FromMinutes(15);

    public async Task<Result<DownloadUrlDto>> Handle(
        IssueDownloadUrlQuery query,
        CancellationToken cancellationToken)
    {
        if (await downloader.ResolveUserIdAsync(cancellationToken) is not { } userId)
        {
            return Result.Failure<DownloadUrlDto>(DownloadErrors.NotAuthenticated);
        }

        var license = await orders.FindLicenseAsync(query.LicenseId, cancellationToken);
        if (license is null || license.BuyerUserId != userId)
        {
            return Result.Failure<DownloadUrlDto>(DownloadErrors.NoEntitlement);
        }

        if (license.ExpiresAtUtc is { } expiry && expiry <= clock.UtcNow)
        {
            return Result.Failure<DownloadUrlDto>(DownloadErrors.NoEntitlement);
        }

        var file = await files.FindFileAsync(query.FileId, cancellationToken);
        if (file is null || file.VariantId != license.VariantId)
        {
            return Result.Failure<DownloadUrlDto>(DownloadErrors.NoEntitlement);
        }

        // A file that has not cleared its scan is never served, whatever the
        // buyer's entitlement (spec 08 section 8.6).
        if (file.ScanStatus == "Infected")
        {
            return Result.Failure<DownloadUrlDto>(DownloadErrors.FileQuarantined);
        }

        if (!file.IsDownloadable)
        {
            return Result.Failure<DownloadUrlDto>(DownloadErrors.FileNotReady);
        }

        if (license.DownloadsUsed >= license.DownloadLimit)
        {
            return Result.Failure<DownloadUrlDto>(DownloadErrors.DownloadLimit);
        }

        // Spending the quota is what makes the issue atomic: if this loses a race
        // the URL is not minted.
        if (!await orders.TryConsumeDownloadAsync(query.LicenseId, cancellationToken))
        {
            return Result.Failure<DownloadUrlDto>(DownloadErrors.DownloadLimit);
        }

        var signed = signedUrls.Create(file.StorageKey, file.FileName, UrlLifetime);
        await downloadLog.RecordAsync(
            license.LicenseId, file.FileId, userId, downloader.ClientIpHash, cancellationToken);

        return Result.Success(new DownloadUrlDto(signed.Url, signed.ExpiresInSeconds));
    }
}

namespace Dpm.Downloads.Application.Abstractions;

/// <summary>
/// Mints a short-lived, signed URL for one stored object. The storage key is
/// never handed to the client; only the signed URL is, and it expires
/// (spec 08 section 8.6).
/// </summary>
public interface ISignedUrlFactory
{
    SignedUrl Create(string storageKey, string fileName, TimeSpan lifetime);
}

public sealed record SignedUrl(string Url, int ExpiresInSeconds);

/// <summary>Records that a download was issued, for audit and analytics.</summary>
public interface IDownloadLog
{
    Task RecordAsync(long licenseId, long fileId, long userId, string? ipHash, CancellationToken ct);
}

public interface ICurrentDownloader
{
    Task<long?> ResolveUserIdAsync(CancellationToken ct);

    string? ClientIpHash { get; }
}

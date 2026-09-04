using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;

namespace Dpm.Files.Domain;

/// <summary>
/// A stored product file (files.ProductFiles). It becomes downloadable only
/// after the virus scan reports Clean; an infected file is quarantined and never
/// serves a download URL (spec 08 section 8.6).
/// </summary>
public sealed class ProductFile : AggregateRoot
{
    public long VariantId { get; private set; }

    public long VersionId { get; private set; }

    public string StorageKey { get; private set; } = string.Empty;

    public string FileName { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public string Checksum { get; private set; } = string.Empty;

    public ScanStatus ScanStatus { get; private set; } = ScanStatus.Pending;

    public DateTime CreatedAtUtc { get; private set; }

    private ProductFile()
    {
    }

    public static ProductFile CreatePending(
        long variantId,
        long versionId,
        string storageKey,
        string fileName,
        long sizeBytes,
        string checksum,
        IClock clock) => new()
        {
            PublicId = Guid.NewGuid(),
            VariantId = variantId,
            VersionId = versionId,
            StorageKey = storageKey,
            FileName = fileName,
            SizeBytes = sizeBytes,
            Checksum = checksum.ToLowerInvariant(),
            CreatedAtUtc = clock.UtcNow,
        };

    public Result MarkScanned(bool isClean, string cleanStorageKey)
    {
        if (ScanStatus != ScanStatus.Pending)
        {
            // Scans are delivered at least once; re-reporting the same outcome is a no-op.
            return Result.Success();
        }

        if (isClean)
        {
            ScanStatus = ScanStatus.Clean;
            StorageKey = cleanStorageKey;
        }
        else
        {
            ScanStatus = ScanStatus.Infected;
        }

        return Result.Success();
    }

    /// <summary>Only a scanned-clean file may ever be served to a buyer.</summary>
    public bool IsDownloadable => ScanStatus == ScanStatus.Clean;
}

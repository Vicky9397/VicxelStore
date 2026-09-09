namespace Dpm.Files.Contracts;

/// <summary>
/// The Files module's public surface. Downloads resolves a stored file through
/// this rather than reading files.* directly.
/// </summary>
public interface IFileDirectory
{
    Task<StoredFile?> FindFileAsync(Guid filePublicId, CancellationToken ct);
}

/// <param name="IsDownloadable">True only once the virus scan has reported Clean.</param>
public sealed record StoredFile(
    long FileId,
    Guid PublicId,
    long VariantId,
    string StorageKey,
    string FileName,
    long SizeBytes,
    string ScanStatus,
    bool IsDownloadable);

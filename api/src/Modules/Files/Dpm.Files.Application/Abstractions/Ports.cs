using Dpm.Files.Domain;

namespace Dpm.Files.Application.Abstractions;

public interface IFileUploadRepository
{
    Task<FileUpload?> FindByPublicIdAsync(Guid publicId, CancellationToken ct);

    void Add(FileUpload upload);
}

public interface IProductFileRepository
{
    Task<ProductFile?> FindByPublicIdAsync(Guid publicId, CancellationToken ct);

    Task<int> CountCleanForVariantAsync(long variantId, CancellationToken ct);

    Task<IReadOnlyList<ProductFile>> ListCleanForVariantAsync(long variantId, CancellationToken ct);

    void Add(ProductFile file);
}

public interface IFilesUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>
/// Object storage behind an abstraction so the bucket implementation (MinIO, S3,
/// Azure Blob) can change without touching the domain (spec README section 1).
/// Uploads land in the quarantine bucket and move to the clean bucket only after
/// a passing scan.
/// </summary>
public interface IFileStorage
{
    Task WritePartAsync(string quarantineKey, int partNumber, Stream content, CancellationToken ct);

    /// <summary>Concatenates the received parts and returns the assembled size and SHA-256.</summary>
    Task<AssembledFile> AssembleAsync(string quarantineKey, int totalParts, CancellationToken ct);

    Task<Stream> OpenQuarantinedAsync(string quarantineKey, CancellationToken ct);

    /// <summary>Moves a scanned-clean object out of quarantine and returns its clean key.</summary>
    Task<string> PromoteToCleanAsync(string quarantineKey, CancellationToken ct);

    Task DeleteQuarantinedAsync(string quarantineKey, CancellationToken ct);
}

public sealed record AssembledFile(long SizeBytes, string Sha256Hex);

/// <summary>Anti-virus scanning; asynchronous so a large file does not block the request.</summary>
public interface IVirusScanner
{
    Task<bool> IsCleanAsync(Stream content, CancellationToken ct);
}

/// <summary>Queues the scan so the upload response returns without waiting for it.</summary>
public interface IScanDispatcher
{
    void Enqueue(Guid productFilePublicId);
}

public interface ICurrentUploader
{
    Task<long?> ResolveUserIdAsync(CancellationToken ct);
}

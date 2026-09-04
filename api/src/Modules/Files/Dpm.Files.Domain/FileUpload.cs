using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;

namespace Dpm.Files.Domain;

/// <summary>
/// A resumable chunked upload session (files.FileUploads). Parts are written to
/// the quarantine bucket and assembled on completion, where the assembled
/// checksum must match what the client declared (spec 08 section 8.6).
/// </summary>
public sealed class FileUpload : AggregateRoot
{
    /// <summary>Ceiling from the Assumption Register (A9): 20 GB per product file.</summary>
    public const long MaxSizeBytes = 20L * 1024 * 1024 * 1024;

    public const int DefaultPartSizeBytes = 8 * 1024 * 1024;

    private readonly List<FileUploadPart> _parts = [];

    public long OwnerUserId { get; private set; }

    public long VariantId { get; private set; }

    public long VersionId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public long DeclaredSizeBytes { get; private set; }

    public string DeclaredChecksum { get; private set; } = string.Empty;

    public int PartSizeBytes { get; private set; }

    public int TotalParts { get; private set; }

    public string QuarantineKey { get; private set; } = string.Empty;

    public UploadStatus Status { get; private set; } = UploadStatus.InProgress;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public IReadOnlyList<FileUploadPart> Parts => _parts.AsReadOnly();

    private FileUpload()
    {
    }

    public static Result<FileUpload> Start(
        long ownerUserId,
        long variantId,
        long versionId,
        string fileName,
        long declaredSizeBytes,
        string declaredChecksum,
        string quarantineKey,
        IClock clock,
        int partSizeBytes = DefaultPartSizeBytes)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return Result.Failure<FileUpload>(Error.Validation("VALIDATION_ERROR", "A file name is required."));
        }

        if (declaredSizeBytes <= 0)
        {
            return Result.Failure<FileUpload>(
                Error.Validation("VALIDATION_ERROR", "File size must be greater than zero."));
        }

        if (declaredSizeBytes > MaxSizeBytes)
        {
            return Result.Failure<FileUpload>(
                Error.Validation("VALIDATION_ERROR", "File exceeds the 20 GB ceiling."));
        }

        if (!IsSha256Hex(declaredChecksum))
        {
            return Result.Failure<FileUpload>(
                Error.Validation("VALIDATION_ERROR", "Checksum must be a 64-character SHA-256 hex digest."));
        }

        var totalParts = (int)((declaredSizeBytes + partSizeBytes - 1) / partSizeBytes);
        return Result.Success(new FileUpload
        {
            PublicId = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            VariantId = variantId,
            VersionId = versionId,
            FileName = fileName.Trim(),
            DeclaredSizeBytes = declaredSizeBytes,
            DeclaredChecksum = declaredChecksum.ToLowerInvariant(),
            PartSizeBytes = partSizeBytes,
            TotalParts = totalParts,
            QuarantineKey = quarantineKey,
            CreatedAtUtc = clock.UtcNow,
        });
    }

    public Result AcceptPart(int partNumber, int sizeBytes, string checksum, IClock clock)
    {
        if (Status != UploadStatus.InProgress)
        {
            return Result.Failure(Error.Conflict("CONFLICT", "This upload is no longer accepting parts."));
        }

        if (partNumber < 1 || partNumber > TotalParts)
        {
            return Result.Failure(Error.Validation(
                "VALIDATION_ERROR",
                $"Part number must be between 1 and {TotalParts}."));
        }

        if (sizeBytes <= 0 || sizeBytes > PartSizeBytes)
        {
            return Result.Failure(Error.Validation(
                "VALIDATION_ERROR",
                $"Part size must be between 1 and {PartSizeBytes} bytes."));
        }

        // Re-sending a part is how a resumed upload recovers, so it replaces
        // rather than conflicts.
        _parts.RemoveAll(p => p.PartNumber == partNumber);
        _parts.Add(FileUploadPart.Create(partNumber, sizeBytes, checksum, clock));
        return Result.Success();
    }

    public bool HasAllParts => _parts.Select(p => p.PartNumber).Distinct().Count() == TotalParts;

    public IReadOnlyList<int> MissingParts =>
        Enumerable.Range(1, TotalParts).Except(_parts.Select(p => p.PartNumber)).ToList();

    /// <summary>
    /// Closes the session once every part has arrived and the assembled bytes
    /// hash to the checksum the client declared.
    /// </summary>
    public Result Complete(string assembledChecksum, long assembledSizeBytes, IClock clock)
    {
        if (Status == UploadStatus.Completed)
        {
            return Result.Success();
        }

        if (Status != UploadStatus.InProgress)
        {
            return Result.Failure(Error.Conflict("CONFLICT", "This upload was aborted."));
        }

        if (!HasAllParts)
        {
            return Result.Failure(Error.Conflict(
                "CONFLICT",
                $"Upload is incomplete; missing part(s): {string.Join(", ", MissingParts)}."));
        }

        if (assembledSizeBytes != DeclaredSizeBytes)
        {
            return Result.Failure(Error.Validation(
                "VALIDATION_ERROR",
                "Assembled size does not match the declared size."));
        }

        if (!string.Equals(assembledChecksum, DeclaredChecksum, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(Error.Validation(
                "VALIDATION_ERROR",
                "Assembled checksum does not match the declared checksum."));
        }

        Status = UploadStatus.Completed;
        CompletedAtUtc = clock.UtcNow;
        return Result.Success();
    }

    public void Abort() => Status = UploadStatus.Aborted;

    private static bool IsSha256Hex(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);
}

public sealed class FileUploadPart : Entity
{
    public long UploadId { get; private set; }

    public int PartNumber { get; private set; }

    public int SizeBytes { get; private set; }

    public string Checksum { get; private set; } = string.Empty;

    public DateTime ReceivedAtUtc { get; private set; }

    private FileUploadPart()
    {
    }

    internal static FileUploadPart Create(int partNumber, int sizeBytes, string checksum, IClock clock) => new()
    {
        PartNumber = partNumber,
        SizeBytes = sizeBytes,
        Checksum = checksum.ToLowerInvariant(),
        ReceivedAtUtc = clock.UtcNow,
    };
}

namespace Dpm.Files.Application.Contracts;

public sealed record UploadSessionDto(
    Guid UploadId,
    int PartSizeBytes,
    int TotalParts,
    IReadOnlyList<int> MissingParts);

public sealed record ProductFileDto(
    Guid Id,
    string FileName,
    long SizeBytes,
    string ScanStatus,
    bool IsDownloadable);

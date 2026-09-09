using Dpm.Files.Application.Abstractions;
using Dpm.Files.Contracts;

namespace Dpm.Files.Infrastructure;

public sealed class FileDirectory(IProductFileRepository files) : IFileDirectory
{
    public async Task<IReadOnlyList<StoredFile>> ListDownloadableAsync(long variantId, CancellationToken ct)
    {
        var found = await files.ListCleanForVariantAsync(variantId, ct);
        return found
            .Select(f => new StoredFile(
                f.Id, f.PublicId, f.VariantId, f.StorageKey, f.FileName, f.SizeBytes,
                f.ScanStatus.ToString(), f.IsDownloadable))
            .ToList();
    }

    public async Task<StoredFile?> FindFileAsync(Guid filePublicId, CancellationToken ct)
    {
        var file = await files.FindByPublicIdAsync(filePublicId, ct);
        return file is null
            ? null
            : new StoredFile(
                file.Id,
                file.PublicId,
                file.VariantId,
                file.StorageKey,
                file.FileName,
                file.SizeBytes,
                file.ScanStatus.ToString(),
                file.IsDownloadable);
    }
}

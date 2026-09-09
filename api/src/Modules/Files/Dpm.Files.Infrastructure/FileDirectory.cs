using Dpm.Files.Application.Abstractions;
using Dpm.Files.Contracts;

namespace Dpm.Files.Infrastructure;

public sealed class FileDirectory(IProductFileRepository files) : IFileDirectory
{
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

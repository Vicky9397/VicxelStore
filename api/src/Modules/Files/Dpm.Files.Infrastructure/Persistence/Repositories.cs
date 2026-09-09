using Dpm.Files.Application.Abstractions;
using Dpm.Files.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dpm.Files.Infrastructure.Persistence;

public sealed class FileUploadRepository(FilesDbContext dbContext) : IFileUploadRepository
{
    public Task<FileUpload?> FindByPublicIdAsync(Guid publicId, CancellationToken ct) =>
        dbContext.FileUploads.Include(u => u.Parts).FirstOrDefaultAsync(u => u.PublicId == publicId, ct);

    public void Add(FileUpload upload) => dbContext.FileUploads.Add(upload);
}

public sealed class ProductFileRepository(FilesDbContext dbContext) : IProductFileRepository
{
    public Task<ProductFile?> FindByPublicIdAsync(Guid publicId, CancellationToken ct) =>
        dbContext.ProductFiles.FirstOrDefaultAsync(f => f.PublicId == publicId, ct);

    public Task<int> CountCleanForVariantAsync(long variantId, CancellationToken ct) =>
        dbContext.ProductFiles.CountAsync(
            f => f.VariantId == variantId && f.ScanStatus == ScanStatus.Clean, ct);

    public async Task<IReadOnlyList<ProductFile>> ListCleanForVariantAsync(
        long variantId,
        CancellationToken ct) =>
        await dbContext.ProductFiles
            .Where(f => f.VariantId == variantId && f.ScanStatus == ScanStatus.Clean)
            .OrderBy(f => f.FileName)
            .ToListAsync(ct);

    public void Add(ProductFile file) => dbContext.ProductFiles.Add(file);
}

public sealed class FilesUnitOfWork(FilesDbContext dbContext) : IFilesUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}

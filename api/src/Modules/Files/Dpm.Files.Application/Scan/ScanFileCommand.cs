using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.IntegrationEvents;
using Dpm.Catalog.Contracts;
using Dpm.Files.Application.Abstractions;
using Dpm.Files.Application.Contracts;
using MediatR;

namespace Dpm.Files.Application.Scan;

public sealed record ScanFileCommand(Guid FileId) : ICommand<ProductFileDto>;

/// <summary>
/// Runs the anti-virus scan for an uploaded file and records the outcome. A
/// clean file is promoted out of the quarantine bucket and becomes downloadable;
/// an infected one stays quarantined and is never served.
///
/// Publishes <see cref="FileScanned"/> carrying the variant's resulting clean
/// file count, which is how Catalog learns the variant is deliverable.
/// </summary>
public sealed class ScanFileCommandHandler(
    IProductFileRepository files,
    IFilesUnitOfWork unitOfWork,
    IFileStorage storage,
    IVirusScanner scanner,
    ICatalogDirectory catalog,
    IPublisher publisher)
    : ICommandHandler<ScanFileCommand, ProductFileDto>
{
    public async Task<Result<ProductFileDto>> Handle(ScanFileCommand command, CancellationToken cancellationToken)
    {
        var file = await files.FindByPublicIdAsync(command.FileId, cancellationToken);
        if (file is null)
        {
            return Result.Failure<ProductFileDto>(FilesErrors.FileNotFound);
        }

        if (file.ScanStatus != Domain.ScanStatus.Pending)
        {
            return Result.Success(ToDto(file));
        }

        bool isClean;
        await using (var content = await storage.OpenQuarantinedAsync(file.StorageKey, cancellationToken))
        {
            isClean = await scanner.IsCleanAsync(content, cancellationToken);
        }

        var quarantineKey = file.StorageKey;
        var cleanKey = isClean
            ? await storage.PromoteToCleanAsync(quarantineKey, cancellationToken)
            : quarantineKey;

        file.MarkScanned(isClean, cleanKey);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var cleanCount = await files.CountCleanForVariantAsync(file.VariantId, cancellationToken);
        var variantPublicId = await ResolveVariantPublicIdAsync(file.VariantId, cancellationToken);
        if (variantPublicId is { } variantId)
        {
            await publisher.Publish(
                new FileScanned(file.PublicId, variantId, isClean, cleanCount), cancellationToken);
        }

        return Result.Success(ToDto(file));
    }

    private async Task<Guid?> ResolveVariantPublicIdAsync(long variantId, CancellationToken ct)
    {
        var variant = await catalog.FindVariantByIdAsync(variantId, ct);
        return variant?.PublicId;
    }

    private static ProductFileDto ToDto(Domain.ProductFile file) =>
        new(file.PublicId, file.FileName, file.SizeBytes, file.ScanStatus.ToString(), file.IsDownloadable);
}

public sealed record GetScanStatusQuery(Guid FileId) : IQuery<ProductFileDto>;

public sealed class GetScanStatusQueryHandler(IProductFileRepository files)
    : IQueryHandler<GetScanStatusQuery, ProductFileDto>
{
    public async Task<Result<ProductFileDto>> Handle(GetScanStatusQuery query, CancellationToken cancellationToken)
    {
        var file = await files.FindByPublicIdAsync(query.FileId, cancellationToken);
        return file is null
            ? Result.Failure<ProductFileDto>(FilesErrors.FileNotFound)
            : Result.Success(new ProductFileDto(
                file.PublicId, file.FileName, file.SizeBytes, file.ScanStatus.ToString(), file.IsDownloadable));
    }
}

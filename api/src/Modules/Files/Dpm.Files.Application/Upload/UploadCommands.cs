using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;
using Dpm.Catalog.Contracts;
using Dpm.Files.Application.Abstractions;
using Dpm.Files.Application.Contracts;
using Dpm.Files.Domain;
using Dpm.Marketplace.Contracts;
using FluentValidation;

namespace Dpm.Files.Application.Upload;

public sealed record InitUploadCommand(
    Guid VariantId,
    string FileName,
    long SizeBytes,
    string Checksum) : ICommand<UploadSessionDto>;

public sealed class InitUploadCommandValidator : AbstractValidator<InitUploadCommand>
{
    public InitUploadCommandValidator()
    {
        RuleFor(c => c.FileName).NotEmpty().MaximumLength(260);
        RuleFor(c => c.SizeBytes).GreaterThan(0).LessThanOrEqualTo(FileUpload.MaxSizeBytes);
        RuleFor(c => c.Checksum).NotEmpty().Length(64);
    }
}

/// <summary>
/// Opens a resumable upload session against a variant the caller owns. Nothing
/// is stored against the catalog until the upload completes and passes scanning.
/// </summary>
public sealed class InitUploadCommandHandler(
    IFileUploadRepository uploads,
    IFilesUnitOfWork unitOfWork,
    ICatalogDirectory catalog,
    IStoreDirectory stores,
    ICurrentUploader uploader,
    IClock clock)
    : ICommandHandler<InitUploadCommand, UploadSessionDto>
{
    public async Task<Result<UploadSessionDto>> Handle(InitUploadCommand command, CancellationToken cancellationToken)
    {
        var target = await UploadTarget.ResolveAsync(catalog, stores, command.VariantId, cancellationToken);
        if (target.IsFailure)
        {
            return Result.Failure<UploadSessionDto>(target.Error);
        }

        if (await uploader.ResolveUserIdAsync(cancellationToken) is not { } userId)
        {
            return Result.Failure<UploadSessionDto>(FilesErrors.NotAuthenticated);
        }

        var versionId = await catalog.FindLatestVersionIdAsync(target.Value.ProductId, cancellationToken);
        if (versionId is null)
        {
            return Result.Failure<UploadSessionDto>(FilesErrors.NoVersion);
        }

        var quarantineKey = $"quarantine/{target.Value.PublicId:N}/{Guid.NewGuid():N}";
        var session = FileUpload.Start(
            userId,
            target.Value.VariantId,
            versionId.Value,
            command.FileName,
            command.SizeBytes,
            command.Checksum,
            quarantineKey,
            clock);
        if (session.IsFailure)
        {
            return Result.Failure<UploadSessionDto>(session.Error);
        }

        uploads.Add(session.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new UploadSessionDto(
            session.Value.PublicId,
            session.Value.PartSizeBytes,
            session.Value.TotalParts,
            session.Value.MissingParts));
    }
}

public sealed record UploadPartCommand(
    Guid UploadId,
    int PartNumber,
    Stream Content,
    int SizeBytes,
    string Checksum) : ICommand<UploadSessionDto>;

public sealed class UploadPartCommandHandler(
    IFileUploadRepository uploads,
    IFilesUnitOfWork unitOfWork,
    IFileStorage storage,
    ICurrentUploader uploader,
    IClock clock)
    : ICommandHandler<UploadPartCommand, UploadSessionDto>
{
    public async Task<Result<UploadSessionDto>> Handle(UploadPartCommand command, CancellationToken cancellationToken)
    {
        var session = await uploads.FindByPublicIdAsync(command.UploadId, cancellationToken);
        if (session is null)
        {
            return Result.Failure<UploadSessionDto>(FilesErrors.UploadNotFound);
        }

        if (await uploader.ResolveUserIdAsync(cancellationToken) is not { } userId
            || session.OwnerUserId != userId)
        {
            return Result.Failure<UploadSessionDto>(FilesErrors.NotVariantOwner);
        }

        var accept = session.AcceptPart(command.PartNumber, command.SizeBytes, command.Checksum, clock);
        if (accept.IsFailure)
        {
            return Result.Failure<UploadSessionDto>(accept.Error);
        }

        await storage.WritePartAsync(session.QuarantineKey, command.PartNumber, command.Content, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new UploadSessionDto(
            session.PublicId, session.PartSizeBytes, session.TotalParts, session.MissingParts));
    }
}

public sealed record CompleteUploadCommand(Guid UploadId) : ICommand<ProductFileDto>;

/// <summary>
/// Assembles the parts, verifies the checksum the client declared, records the
/// file as Pending, and queues the virus scan. The file is not downloadable
/// until that scan reports Clean.
/// </summary>
public sealed class CompleteUploadCommandHandler(
    IFileUploadRepository uploads,
    IProductFileRepository files,
    IFilesUnitOfWork unitOfWork,
    IFileStorage storage,
    IScanDispatcher scanDispatcher,
    ICurrentUploader uploader,
    IClock clock)
    : ICommandHandler<CompleteUploadCommand, ProductFileDto>
{
    public async Task<Result<ProductFileDto>> Handle(CompleteUploadCommand command, CancellationToken cancellationToken)
    {
        var session = await uploads.FindByPublicIdAsync(command.UploadId, cancellationToken);
        if (session is null)
        {
            return Result.Failure<ProductFileDto>(FilesErrors.UploadNotFound);
        }

        if (await uploader.ResolveUserIdAsync(cancellationToken) is not { } userId
            || session.OwnerUserId != userId)
        {
            return Result.Failure<ProductFileDto>(FilesErrors.NotVariantOwner);
        }

        var assembled = await storage.AssembleAsync(session.QuarantineKey, session.TotalParts, cancellationToken);
        var complete = session.Complete(assembled.Sha256Hex, assembled.SizeBytes, clock);
        if (complete.IsFailure)
        {
            return Result.Failure<ProductFileDto>(complete.Error);
        }

        var file = ProductFile.CreatePending(
            session.VariantId,
            session.VersionId,
            session.QuarantineKey,
            session.FileName,
            assembled.SizeBytes,
            assembled.Sha256Hex,
            clock);

        files.Add(file);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        scanDispatcher.Enqueue(file.PublicId);

        return Result.Success(new ProductFileDto(
            file.PublicId, file.FileName, file.SizeBytes, file.ScanStatus.ToString(), file.IsDownloadable));
    }
}

/// <summary>
/// Resolves the variant an upload targets and checks the caller's store owns it.
/// Catalog answers the variant question; Marketplace answers the store question.
/// </summary>
internal static class UploadTarget
{
    public static async Task<Result<VariantRef>> ResolveAsync(
        ICatalogDirectory catalog,
        IStoreDirectory stores,
        Guid variantPublicId,
        CancellationToken ct)
    {
        var store = await stores.FindForCurrentUserAsync(ct);
        if (store is null)
        {
            return Result.Failure<VariantRef>(FilesErrors.NoStore);
        }

        var variant = await catalog.FindVariantAsync(variantPublicId, ct);
        if (variant is null)
        {
            return Result.Failure<VariantRef>(FilesErrors.VariantNotFound);
        }

        return variant.OwnerStoreId == store.StoreId
            ? Result.Success(variant)
            : Result.Failure<VariantRef>(FilesErrors.NotVariantOwner);
    }
}

using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;
using Dpm.Catalog.Application.Abstractions;
using Dpm.Catalog.Application.Contracts;
using Dpm.Marketplace.Contracts;
using FluentValidation;

namespace Dpm.Catalog.Application.Lifecycle;

public sealed record SubmitProductCommand(Guid ProductId) : ICommand<SellerProductDto>;

/// <summary>
/// Sends a draft to the moderation queue. The deliverability invariant is
/// checked here so a product that cannot be delivered never reaches a moderator.
/// </summary>
public sealed class SubmitProductCommandHandler(
    IProductRepository products,
    ICatalogUnitOfWork unitOfWork,
    IStoreDirectory stores,
    IClock clock)
    : ICommandHandler<SubmitProductCommand, SellerProductDto>
{
    public async Task<Result<SellerProductDto>> Handle(SubmitProductCommand command, CancellationToken cancellationToken)
    {
        var owned = await OwnedProduct.ResolveAsync(products, stores, command.ProductId, cancellationToken);
        if (owned.IsFailure)
        {
            return Result.Failure<SellerProductDto>(owned.Error);
        }

        var (product, store) = owned.Value;
        var submit = product.SubmitForReview(store.PublicId, clock);
        if (submit.IsFailure)
        {
            return Result.Failure<SellerProductDto>(submit.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(product.ToSellerDto());
    }
}

public sealed record PublishProductCommand(Guid ProductId) : ICommand<SellerProductDto>;

/// <summary>
/// Publishes an approved product. Requires the store to have completed
/// verification, tax and payout setup, which the Marketplace module decides.
/// </summary>
public sealed class PublishProductCommandHandler(
    IProductRepository products,
    ICatalogUnitOfWork unitOfWork,
    IStoreDirectory stores,
    IClock clock)
    : ICommandHandler<PublishProductCommand, SellerProductDto>
{
    public async Task<Result<SellerProductDto>> Handle(PublishProductCommand command, CancellationToken cancellationToken)
    {
        var owned = await OwnedProduct.ResolveAsync(products, stores, command.ProductId, cancellationToken);
        if (owned.IsFailure)
        {
            return Result.Failure<SellerProductDto>(owned.Error);
        }

        var (product, store) = owned.Value;
        var publish = product.Publish(store.CanPublish, clock);
        if (publish.IsFailure)
        {
            return Result.Failure<SellerProductDto>(publish.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(product.ToSellerDto());
    }
}

public sealed record UnpublishProductCommand(Guid ProductId) : ICommand<SellerProductDto>;

public sealed class UnpublishProductCommandHandler(
    IProductRepository products,
    ICatalogUnitOfWork unitOfWork,
    IStoreDirectory stores,
    IClock clock)
    : ICommandHandler<UnpublishProductCommand, SellerProductDto>
{
    public async Task<Result<SellerProductDto>> Handle(UnpublishProductCommand command, CancellationToken cancellationToken)
    {
        var owned = await OwnedProduct.ResolveAsync(products, stores, command.ProductId, cancellationToken);
        if (owned.IsFailure)
        {
            return Result.Failure<SellerProductDto>(owned.Error);
        }

        var unpublish = owned.Value.Product.Unpublish(clock);
        if (unpublish.IsFailure)
        {
            return Result.Failure<SellerProductDto>(unpublish.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(owned.Value.Product.ToSellerDto());
    }
}

public sealed record ScheduleProductCommand(Guid ProductId, DateTime PublishAtUtc) : ICommand<SellerProductDto>;

public sealed class ScheduleProductCommandValidator : AbstractValidator<ScheduleProductCommand>
{
    public ScheduleProductCommandValidator()
    {
        RuleFor(c => c.PublishAtUtc).NotEmpty();
    }
}

public sealed class ScheduleProductCommandHandler(
    IProductRepository products,
    ICatalogUnitOfWork unitOfWork,
    IStoreDirectory stores,
    IClock clock)
    : ICommandHandler<ScheduleProductCommand, SellerProductDto>
{
    public async Task<Result<SellerProductDto>> Handle(ScheduleProductCommand command, CancellationToken cancellationToken)
    {
        var owned = await OwnedProduct.ResolveAsync(products, stores, command.ProductId, cancellationToken);
        if (owned.IsFailure)
        {
            return Result.Failure<SellerProductDto>(owned.Error);
        }

        var schedule = owned.Value.Product.Schedule(command.PublishAtUtc, clock);
        if (schedule.IsFailure)
        {
            return Result.Failure<SellerProductDto>(schedule.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(owned.Value.Product.ToSellerDto());
    }
}

public sealed record ArchiveProductCommand(Guid ProductId) : ICommand<SellerProductDto>;

public sealed class ArchiveProductCommandHandler(
    IProductRepository products,
    ICatalogUnitOfWork unitOfWork,
    IStoreDirectory stores,
    IClock clock)
    : ICommandHandler<ArchiveProductCommand, SellerProductDto>
{
    public async Task<Result<SellerProductDto>> Handle(ArchiveProductCommand command, CancellationToken cancellationToken)
    {
        var owned = await OwnedProduct.ResolveAsync(products, stores, command.ProductId, cancellationToken);
        if (owned.IsFailure)
        {
            return Result.Failure<SellerProductDto>(owned.Error);
        }

        owned.Value.Product.Archive(clock);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(owned.Value.Product.ToSellerDto());
    }
}

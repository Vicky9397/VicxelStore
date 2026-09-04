using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;
using Dpm.Catalog.Application.Abstractions;
using Dpm.Catalog.Application.Contracts;
using FluentValidation;

namespace Dpm.Catalog.Application.Moderation;

/// <summary>
/// Moderator decisions. These deliberately do not go through the ownership
/// guard: a moderator acts on other sellers' products. Endpoint authorization
/// restricts them to the moderator policy, and every decision is logged with its
/// reason (spec 02 section 2.4 Product Management).
/// </summary>
public sealed record ApproveProductCommand(Guid ProductId) : ICommand<SellerProductDto>;

public sealed class ApproveProductCommandHandler(
    IProductRepository products,
    ICatalogUnitOfWork unitOfWork,
    IModerationLog moderationLog,
    ICurrentModerator moderator,
    IClock clock)
    : ICommandHandler<ApproveProductCommand, SellerProductDto>
{
    public async Task<Result<SellerProductDto>> Handle(ApproveProductCommand command, CancellationToken cancellationToken)
    {
        if (await moderator.ResolveUserIdAsync(cancellationToken) is not { } moderatorId)
        {
            return Result.Failure<SellerProductDto>(CatalogErrors.NotAuthenticated);
        }

        var product = await products.FindByPublicIdAsync(command.ProductId, cancellationToken);
        if (product is null)
        {
            return Result.Failure<SellerProductDto>(CatalogErrors.ProductNotFound);
        }

        var approve = product.Approve(clock);
        if (approve.IsFailure)
        {
            return Result.Failure<SellerProductDto>(approve.Error);
        }

        moderationLog.Record(product.Id, moderatorId, approved: true, reason: null);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(product.ToSellerDto());
    }
}

public sealed record RejectProductCommand(Guid ProductId, string Reason) : ICommand<SellerProductDto>;

public sealed class RejectProductCommandValidator : AbstractValidator<RejectProductCommand>
{
    public RejectProductCommandValidator()
    {
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(1000)
            .WithMessage("Tell the seller why the product was rejected.");
    }
}

public sealed class RejectProductCommandHandler(
    IProductRepository products,
    ICatalogUnitOfWork unitOfWork,
    IModerationLog moderationLog,
    ICurrentModerator moderator,
    IClock clock)
    : ICommandHandler<RejectProductCommand, SellerProductDto>
{
    public async Task<Result<SellerProductDto>> Handle(RejectProductCommand command, CancellationToken cancellationToken)
    {
        if (await moderator.ResolveUserIdAsync(cancellationToken) is not { } moderatorId)
        {
            return Result.Failure<SellerProductDto>(CatalogErrors.NotAuthenticated);
        }

        var product = await products.FindByPublicIdAsync(command.ProductId, cancellationToken);
        if (product is null)
        {
            return Result.Failure<SellerProductDto>(CatalogErrors.ProductNotFound);
        }

        var reject = product.Reject(command.Reason, clock);
        if (reject.IsFailure)
        {
            return Result.Failure<SellerProductDto>(reject.Error);
        }

        moderationLog.Record(product.Id, moderatorId, approved: false, command.Reason.Trim());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(product.ToSellerDto());
    }
}

public sealed record BeginReviewCommand(Guid ProductId) : ICommand<SellerProductDto>;

public sealed class BeginReviewCommandHandler(
    IProductRepository products,
    ICatalogUnitOfWork unitOfWork,
    IClock clock)
    : ICommandHandler<BeginReviewCommand, SellerProductDto>
{
    public async Task<Result<SellerProductDto>> Handle(BeginReviewCommand command, CancellationToken cancellationToken)
    {
        var product = await products.FindByPublicIdAsync(command.ProductId, cancellationToken);
        if (product is null)
        {
            return Result.Failure<SellerProductDto>(CatalogErrors.ProductNotFound);
        }

        var begin = product.BeginReview(clock);
        if (begin.IsFailure)
        {
            return Result.Failure<SellerProductDto>(begin.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(product.ToSellerDto());
    }
}

/// <summary>Resolves the acting staff member so decisions are attributable.</summary>
public interface ICurrentModerator
{
    Task<long?> ResolveUserIdAsync(CancellationToken ct);
}

using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;
using Dpm.Marketplace.Application.Abstractions;
using Dpm.Marketplace.Application.Contracts;
using FluentValidation;

namespace Dpm.Marketplace.Application.Administration;

/// <summary>
/// Staff decisions on a seller's verification. These are the counterpart to the
/// seller's own submissions: a seller can never mark themselves verified.
/// Endpoint authorization restricts these to staff roles.
/// </summary>
public sealed record ApproveKycCommand(Guid StoreId) : ICommand<SellerProfileDto>;

public sealed class ApproveKycCommandHandler(
    IStoreRepository stores,
    IMarketplaceUnitOfWork unitOfWork,
    IClock clock)
    : ICommandHandler<ApproveKycCommand, SellerProfileDto>
{
    public async Task<Result<SellerProfileDto>> Handle(ApproveKycCommand command, CancellationToken cancellationToken)
    {
        var store = await stores.FindByPublicIdAsync(command.StoreId, cancellationToken);
        if (store is null)
        {
            return Result.Failure<SellerProfileDto>(MarketplaceErrors.StoreNotFound);
        }

        store.ApproveKyc(clock);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(store.ToProfileDto());
    }
}

public sealed record RejectKycCommand(Guid StoreId) : ICommand<SellerProfileDto>;

public sealed class RejectKycCommandHandler(
    IStoreRepository stores,
    IMarketplaceUnitOfWork unitOfWork,
    IClock clock)
    : ICommandHandler<RejectKycCommand, SellerProfileDto>
{
    public async Task<Result<SellerProfileDto>> Handle(RejectKycCommand command, CancellationToken cancellationToken)
    {
        var store = await stores.FindByPublicIdAsync(command.StoreId, cancellationToken);
        if (store is null)
        {
            return Result.Failure<SellerProfileDto>(MarketplaceErrors.StoreNotFound);
        }

        store.RejectKyc(clock);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(store.ToProfileDto());
    }
}

public sealed record VerifyBankCommand(Guid StoreId) : ICommand<SellerProfileDto>;

public sealed class VerifyBankCommandHandler(
    IStoreRepository stores,
    IMarketplaceUnitOfWork unitOfWork,
    IClock clock)
    : ICommandHandler<VerifyBankCommand, SellerProfileDto>
{
    public async Task<Result<SellerProfileDto>> Handle(VerifyBankCommand command, CancellationToken cancellationToken)
    {
        var store = await stores.FindByPublicIdAsync(command.StoreId, cancellationToken);
        if (store is null)
        {
            return Result.Failure<SellerProfileDto>(MarketplaceErrors.StoreNotFound);
        }

        store.VerifyBank(clock);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(store.ToProfileDto());
    }
}

public sealed record SuspendStoreCommand(Guid StoreId, string Reason) : ICommand<StoreDto>;

public sealed class SuspendStoreCommandValidator : AbstractValidator<SuspendStoreCommand>
{
    public SuspendStoreCommandValidator()
    {
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class SuspendStoreCommandHandler(
    IStoreRepository stores,
    IMarketplaceUnitOfWork unitOfWork)
    : ICommandHandler<SuspendStoreCommand, StoreDto>
{
    public async Task<Result<StoreDto>> Handle(SuspendStoreCommand command, CancellationToken cancellationToken)
    {
        var store = await stores.FindByPublicIdAsync(command.StoreId, cancellationToken);
        if (store is null)
        {
            return Result.Failure<StoreDto>(MarketplaceErrors.StoreNotFound);
        }

        store.Suspend(command.Reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(store.ToDto());
    }
}

public sealed record ReinstateStoreCommand(Guid StoreId) : ICommand<StoreDto>;

public sealed class ReinstateStoreCommandHandler(
    IStoreRepository stores,
    IMarketplaceUnitOfWork unitOfWork)
    : ICommandHandler<ReinstateStoreCommand, StoreDto>
{
    public async Task<Result<StoreDto>> Handle(ReinstateStoreCommand command, CancellationToken cancellationToken)
    {
        var store = await stores.FindByPublicIdAsync(command.StoreId, cancellationToken);
        if (store is null)
        {
            return Result.Failure<StoreDto>(MarketplaceErrors.StoreNotFound);
        }

        store.Reinstate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(store.ToDto());
    }
}

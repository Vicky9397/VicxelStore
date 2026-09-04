using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;
using Dpm.Marketplace.Application.Abstractions;
using Dpm.Marketplace.Application.Contracts;
using FluentValidation;

namespace Dpm.Marketplace.Application.Kyc;

public sealed record SubmitKycCommand(Guid StoreId, string LegalName) : ICommand<SellerProfileDto>;

public sealed class SubmitKycCommandValidator : AbstractValidator<SubmitKycCommand>
{
    public SubmitKycCommandValidator()
    {
        RuleFor(c => c.LegalName).NotEmpty().MinimumLength(2).MaximumLength(200);
    }
}

public sealed class SubmitKycCommandHandler(
    IStoreRepository stores,
    IMarketplaceUnitOfWork unitOfWork,
    ICurrentSeller currentSeller,
    IClock clock)
    : ICommandHandler<SubmitKycCommand, SellerProfileDto>
{
    public async Task<Result<SellerProfileDto>> Handle(SubmitKycCommand command, CancellationToken cancellationToken)
    {
        var owned = await OwnedStore.ResolveAsync(stores, currentSeller, command.StoreId, cancellationToken);
        if (owned.IsFailure)
        {
            return Result.Failure<SellerProfileDto>(owned.Error);
        }

        var submit = owned.Value.SubmitKyc(command.LegalName, clock);
        if (submit.IsFailure)
        {
            return Result.Failure<SellerProfileDto>(submit.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(owned.Value.ToProfileDto());
    }
}

public sealed record UpdateTaxInfoCommand(Guid StoreId, string TaxIdType, string TaxId) : ICommand<SellerProfileDto>;

public sealed class UpdateTaxInfoCommandValidator : AbstractValidator<UpdateTaxInfoCommand>
{
    public UpdateTaxInfoCommandValidator()
    {
        RuleFor(c => c.TaxIdType).NotEmpty().Must(t => t is "GSTIN" or "VAT")
            .WithMessage("Tax id type must be GSTIN or VAT.");
        RuleFor(c => c.TaxId).NotEmpty().MaximumLength(50);
    }
}

public sealed class UpdateTaxInfoCommandHandler(
    IStoreRepository stores,
    IMarketplaceUnitOfWork unitOfWork,
    ICurrentSeller currentSeller,
    IClock clock)
    : ICommandHandler<UpdateTaxInfoCommand, SellerProfileDto>
{
    public async Task<Result<SellerProfileDto>> Handle(UpdateTaxInfoCommand command, CancellationToken cancellationToken)
    {
        var owned = await OwnedStore.ResolveAsync(stores, currentSeller, command.StoreId, cancellationToken);
        if (owned.IsFailure)
        {
            return Result.Failure<SellerProfileDto>(owned.Error);
        }

        owned.Value.SetTaxInfo(command.TaxIdType, command.TaxId, clock);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(owned.Value.ToProfileDto());
    }
}

public sealed record UpdateBankInfoCommand(Guid StoreId, string BankRef) : ICommand<SellerProfileDto>;

public sealed class UpdateBankInfoCommandValidator : AbstractValidator<UpdateBankInfoCommand>
{
    public UpdateBankInfoCommandValidator()
    {
        RuleFor(c => c.BankRef).NotEmpty().MaximumLength(200);
    }
}

/// <summary>
/// Stores the payment provider's token for the payout destination. Raw bank
/// details never reach this system, and setting a new destination clears the
/// verified flag until the provider confirms it again.
/// </summary>
public sealed class UpdateBankInfoCommandHandler(
    IStoreRepository stores,
    IMarketplaceUnitOfWork unitOfWork,
    ICurrentSeller currentSeller,
    IClock clock)
    : ICommandHandler<UpdateBankInfoCommand, SellerProfileDto>
{
    public async Task<Result<SellerProfileDto>> Handle(UpdateBankInfoCommand command, CancellationToken cancellationToken)
    {
        var owned = await OwnedStore.ResolveAsync(stores, currentSeller, command.StoreId, cancellationToken);
        if (owned.IsFailure)
        {
            return Result.Failure<SellerProfileDto>(owned.Error);
        }

        owned.Value.SetBankInfo(command.BankRef, clock);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(owned.Value.ToProfileDto());
    }
}

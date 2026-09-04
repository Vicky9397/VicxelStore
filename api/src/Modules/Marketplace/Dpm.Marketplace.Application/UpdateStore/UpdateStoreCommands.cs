using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;
using Dpm.Marketplace.Application.Abstractions;
using Dpm.Marketplace.Application.Contracts;
using FluentValidation;

namespace Dpm.Marketplace.Application.UpdateStore;

public sealed record UpdateStoreCommand(Guid StoreId, string Name, string? About) : ICommand<StoreDto>;

public sealed class UpdateStoreCommandValidator : AbstractValidator<UpdateStoreCommand>
{
    public UpdateStoreCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MinimumLength(2).MaximumLength(120);
        RuleFor(c => c.About).MaximumLength(4000);
    }
}

public sealed class UpdateStoreCommandHandler(
    IStoreRepository stores,
    IMarketplaceUnitOfWork unitOfWork,
    ICurrentSeller currentSeller)
    : ICommandHandler<UpdateStoreCommand, StoreDto>
{
    public async Task<Result<StoreDto>> Handle(UpdateStoreCommand command, CancellationToken cancellationToken)
    {
        var owned = await OwnedStore.ResolveAsync(stores, currentSeller, command.StoreId, cancellationToken);
        if (owned.IsFailure)
        {
            return Result.Failure<StoreDto>(owned.Error);
        }

        var store = owned.Value;
        var update = store.UpdateDetails(command.Name, command.About);
        if (update.IsFailure)
        {
            return Result.Failure<StoreDto>(update.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(store.ToDto());
    }
}

public sealed record UpdateBrandingCommand(
    Guid StoreId,
    string? LogoUrl,
    string? BannerUrl,
    string? ThemeJson) : ICommand<StoreDto>;

public sealed class UpdateBrandingCommandValidator : AbstractValidator<UpdateBrandingCommand>
{
    public UpdateBrandingCommandValidator()
    {
        RuleFor(c => c.LogoUrl).MaximumLength(500);
        RuleFor(c => c.BannerUrl).MaximumLength(500);
    }
}

public sealed class UpdateBrandingCommandHandler(
    IStoreRepository stores,
    IMarketplaceUnitOfWork unitOfWork,
    ICurrentSeller currentSeller)
    : ICommandHandler<UpdateBrandingCommand, StoreDto>
{
    public async Task<Result<StoreDto>> Handle(UpdateBrandingCommand command, CancellationToken cancellationToken)
    {
        var owned = await OwnedStore.ResolveAsync(stores, currentSeller, command.StoreId, cancellationToken);
        if (owned.IsFailure)
        {
            return Result.Failure<StoreDto>(owned.Error);
        }

        owned.Value.UpdateBranding(command.LogoUrl, command.BannerUrl, command.ThemeJson);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(owned.Value.ToDto());
    }
}

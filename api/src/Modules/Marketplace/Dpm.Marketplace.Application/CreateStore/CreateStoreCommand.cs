using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;
using Dpm.Marketplace.Application.Abstractions;
using Dpm.Marketplace.Application.Contracts;
using Dpm.Marketplace.Domain;
using FluentValidation;

namespace Dpm.Marketplace.Application.CreateStore;

public sealed record CreateStoreCommand(string Slug, string Name, string? About) : ICommand<StoreDto>;

public sealed class CreateStoreCommandValidator : AbstractValidator<CreateStoreCommand>
{
    public CreateStoreCommandValidator()
    {
        RuleFor(c => c.Slug)
            .NotEmpty()
            .MinimumLength(Store.SlugMinLength)
            .MaximumLength(Store.SlugMaxLength)
            .Must(slug => Store.IsValidSlug(Store.NormalizeSlug(slug)))
            .WithMessage("Use lowercase letters, digits and single hyphens.");
        RuleFor(c => c.Name).NotEmpty().MinimumLength(2).MaximumLength(120);
        RuleFor(c => c.About).MaximumLength(4000);
    }
}

/// <summary>
/// Turns a buyer into a seller by opening their store. One store per user, and
/// the slug is claimed first-come (spec 02 section 2.4 Seller Management).
/// </summary>
public sealed class CreateStoreCommandHandler(
    IStoreRepository stores,
    IMarketplaceUnitOfWork unitOfWork,
    ICurrentSeller currentSeller,
    IClock clock)
    : ICommandHandler<CreateStoreCommand, StoreDto>
{
    public async Task<Result<StoreDto>> Handle(CreateStoreCommand command, CancellationToken cancellationToken)
    {
        if (currentSeller.UserPublicId is not { } userPublicId)
        {
            return Result.Failure<StoreDto>(MarketplaceErrors.NotAuthenticated);
        }

        if (await currentSeller.ResolveUserIdAsync(cancellationToken) is not { } userId)
        {
            return Result.Failure<StoreDto>(MarketplaceErrors.NotAuthenticated);
        }

        if (await stores.OwnerHasStoreAsync(userId, cancellationToken))
        {
            return Result.Failure<StoreDto>(MarketplaceErrors.AlreadyOwnsStore);
        }

        var slug = Store.NormalizeSlug(command.Slug);
        if (await stores.SlugExistsAsync(slug, cancellationToken))
        {
            return Result.Failure<StoreDto>(MarketplaceErrors.SlugTaken);
        }

        var creation = Store.Create(userId, userPublicId, slug, command.Name, clock);
        if (creation.IsFailure)
        {
            return Result.Failure<StoreDto>(creation.Error);
        }

        var store = creation.Value;
        var details = store.UpdateDetails(command.Name, command.About);
        if (details.IsFailure)
        {
            return Result.Failure<StoreDto>(details.Error);
        }

        stores.Add(store);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(store.ToDto());
    }
}

using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;
using Dpm.Catalog.Application.Abstractions;
using Dpm.Catalog.Application.Contracts;
using Dpm.Marketplace.Contracts;
using FluentValidation;

namespace Dpm.Catalog.Application.Versions;

public sealed record ReleaseVersionCommand(Guid ProductId, string VersionNumber, string? Changelog)
    : ICommand<VersionDto>;

public sealed class ReleaseVersionCommandValidator : AbstractValidator<ReleaseVersionCommand>
{
    public ReleaseVersionCommandValidator()
    {
        RuleFor(c => c.VersionNumber).NotEmpty().MaximumLength(20);
        RuleFor(c => c.Changelog).MaximumLength(10000);
    }
}

public sealed class ReleaseVersionCommandHandler(
    IProductRepository products,
    ICatalogUnitOfWork unitOfWork,
    IStoreDirectory stores,
    IClock clock)
    : ICommandHandler<ReleaseVersionCommand, VersionDto>
{
    public async Task<Result<VersionDto>> Handle(ReleaseVersionCommand command, CancellationToken cancellationToken)
    {
        var owned = await OwnedProduct.ResolveAsync(products, stores, command.ProductId, cancellationToken);
        if (owned.IsFailure)
        {
            return Result.Failure<VersionDto>(owned.Error);
        }

        var version = owned.Value.Product.ReleaseVersion(command.VersionNumber, command.Changelog, clock);
        if (version.IsFailure)
        {
            return Result.Failure<VersionDto>(version.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new VersionDto(
            version.Value.VersionNumber, version.Value.Changelog, version.Value.ReleasedAtUtc));
    }
}

using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;
using Dpm.Catalog.Application.Abstractions;
using Dpm.Catalog.Application.Contracts;
using Dpm.Catalog.Domain;
using Dpm.Marketplace.Contracts;
using FluentValidation;

namespace Dpm.Catalog.Application.Variants;

public sealed record AddVariantCommand(
    Guid ProductId,
    string Name,
    decimal Price,
    string Currency,
    string LicenseType,
    int DownloadLimit) : ICommand<VariantDto>;

public sealed class AddVariantCommandValidator : AbstractValidator<AddVariantCommand>
{
    public AddVariantCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(120);
        RuleFor(c => c.Price).GreaterThan(0);
        RuleFor(c => c.Currency).NotEmpty().Length(3);
        RuleFor(c => c.DownloadLimit).GreaterThan(0);
        RuleFor(c => c.LicenseType)
            .Must(t => Enum.TryParse<LicenseType>(t, ignoreCase: true, out _))
            .WithMessage("License type must be Personal, Commercial or Extended.");
    }
}

public sealed class AddVariantCommandHandler(
    IProductRepository products,
    ICatalogUnitOfWork unitOfWork,
    IStoreDirectory stores,
    IClock clock)
    : ICommandHandler<AddVariantCommand, VariantDto>
{
    public async Task<Result<VariantDto>> Handle(AddVariantCommand command, CancellationToken cancellationToken)
    {
        var owned = await OwnedProduct.ResolveAsync(products, stores, command.ProductId, cancellationToken);
        if (owned.IsFailure)
        {
            return Result.Failure<VariantDto>(owned.Error);
        }

        var price = Money.Create(command.Price, command.Currency);
        if (price.IsFailure)
        {
            return Result.Failure<VariantDto>(price.Error);
        }

        var variant = owned.Value.Product.AddVariant(
            command.Name,
            price.Value,
            Enum.Parse<LicenseType>(command.LicenseType, ignoreCase: true),
            command.DownloadLimit,
            clock);
        if (variant.IsFailure)
        {
            return Result.Failure<VariantDto>(variant.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(variant.Value.ToDto());
    }
}

public sealed record UpdateVariantCommand(
    Guid ProductId,
    Guid VariantId,
    string Name,
    decimal Price,
    string Currency,
    string LicenseType,
    int DownloadLimit,
    bool IsActive) : ICommand<VariantDto>;

public sealed class UpdateVariantCommandValidator : AbstractValidator<UpdateVariantCommand>
{
    public UpdateVariantCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(120);
        RuleFor(c => c.Price).GreaterThan(0);
        RuleFor(c => c.Currency).NotEmpty().Length(3);
        RuleFor(c => c.DownloadLimit).GreaterThan(0);
        RuleFor(c => c.LicenseType)
            .Must(t => Enum.TryParse<LicenseType>(t, ignoreCase: true, out _))
            .WithMessage("License type must be Personal, Commercial or Extended.");
    }
}

public sealed class UpdateVariantCommandHandler(
    IProductRepository products,
    ICatalogUnitOfWork unitOfWork,
    IStoreDirectory stores,
    IClock clock)
    : ICommandHandler<UpdateVariantCommand, VariantDto>
{
    public async Task<Result<VariantDto>> Handle(UpdateVariantCommand command, CancellationToken cancellationToken)
    {
        var owned = await OwnedProduct.ResolveAsync(products, stores, command.ProductId, cancellationToken);
        if (owned.IsFailure)
        {
            return Result.Failure<VariantDto>(owned.Error);
        }

        var price = Money.Create(command.Price, command.Currency);
        if (price.IsFailure)
        {
            return Result.Failure<VariantDto>(price.Error);
        }

        var product = owned.Value.Product;
        var update = product.UpdateVariant(
            command.VariantId,
            command.Name,
            price.Value,
            Enum.Parse<LicenseType>(command.LicenseType, ignoreCase: true),
            command.DownloadLimit,
            command.IsActive,
            clock);
        if (update.IsFailure)
        {
            return Result.Failure<VariantDto>(update.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        var variant = product.Variants.First(v => v.PublicId == command.VariantId);
        return Result.Success(variant.ToDto());
    }
}

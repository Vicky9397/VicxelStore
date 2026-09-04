using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;
using Dpm.Catalog.Application.Abstractions;
using Dpm.Catalog.Application.Contracts;
using Dpm.Marketplace.Contracts;
using FluentValidation;

namespace Dpm.Catalog.Application.UpdateProduct;

public sealed record UpdateProductCommand(
    Guid ProductId,
    string Title,
    string? Description,
    string CategorySlug,
    string? SeoTitle,
    string? SeoDescription) : ICommand<SellerProductDto>;

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MinimumLength(3).MaximumLength(200);
        RuleFor(c => c.CategorySlug).NotEmpty();
        RuleFor(c => c.Description).MaximumLength(20000);
        RuleFor(c => c.SeoTitle).MaximumLength(200);
        RuleFor(c => c.SeoDescription).MaximumLength(400);
    }
}

public sealed class UpdateProductCommandHandler(
    IProductRepository products,
    ICategoryRepository categories,
    ICatalogUnitOfWork unitOfWork,
    IStoreDirectory stores,
    IClock clock)
    : ICommandHandler<UpdateProductCommand, SellerProductDto>
{
    public async Task<Result<SellerProductDto>> Handle(UpdateProductCommand command, CancellationToken cancellationToken)
    {
        var owned = await OwnedProduct.ResolveAsync(products, stores, command.ProductId, cancellationToken);
        if (owned.IsFailure)
        {
            return Result.Failure<SellerProductDto>(owned.Error);
        }

        var category = await categories.FindBySlugAsync(
            command.CategorySlug.Trim().ToLowerInvariant(), cancellationToken);
        if (category is null)
        {
            return Result.Failure<SellerProductDto>(CatalogErrors.CategoryNotFound);
        }

        var product = owned.Value.Product;
        var update = product.UpdateDetails(
            command.Title, command.Description, category.Id, command.SeoTitle, command.SeoDescription, clock);
        if (update.IsFailure)
        {
            return Result.Failure<SellerProductDto>(update.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(product.ToSellerDto());
    }
}

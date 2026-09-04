using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;
using Dpm.Catalog.Application.Abstractions;
using Dpm.Catalog.Application.Contracts;
using Dpm.Catalog.Domain;
using Dpm.Marketplace.Contracts;
using FluentValidation;

namespace Dpm.Catalog.Application.CreateProduct;

public sealed record CreateVariantInput(
    string Name,
    decimal Price,
    string Currency,
    string LicenseType,
    int DownloadLimit);

public sealed record CreateProductCommand(
    string Title,
    string CategorySlug,
    string? Description,
    IReadOnlyList<string> Tags,
    IReadOnlyList<CreateVariantInput> Variants) : ICommand<SellerProductDto>;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MinimumLength(3).MaximumLength(200);
        RuleFor(c => c.CategorySlug).NotEmpty();
        RuleFor(c => c.Description).MaximumLength(20000);
        RuleFor(c => c.Variants).NotEmpty().WithMessage("Add at least one variant.");
        RuleForEach(c => c.Variants).ChildRules(variant =>
        {
            variant.RuleFor(v => v.Name).NotEmpty().MaximumLength(120);
            variant.RuleFor(v => v.Price).GreaterThan(0);
            variant.RuleFor(v => v.Currency).NotEmpty().Length(3);
            variant.RuleFor(v => v.DownloadLimit).GreaterThan(0);
            variant.RuleFor(v => v.LicenseType)
                .Must(t => Enum.TryParse<LicenseType>(t, ignoreCase: true, out _))
                .WithMessage("License type must be Personal, Commercial or Extended.");
        });
    }
}

/// <summary>
/// Creates a product in Draft with its initial variants and an initial version.
/// The product is not deliverable until files are uploaded and scanned clean.
/// </summary>
public sealed class CreateProductCommandHandler(
    IProductRepository products,
    ICategoryRepository categories,
    ITagRepository tags,
    ICatalogUnitOfWork unitOfWork,
    IStoreDirectory stores,
    IClock clock)
    : ICommandHandler<CreateProductCommand, SellerProductDto>
{
    public async Task<Result<SellerProductDto>> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var store = await OwnedProduct.ResolveStoreAsync(stores, cancellationToken);
        if (store.IsFailure)
        {
            return Result.Failure<SellerProductDto>(store.Error);
        }

        var category = await categories.FindBySlugAsync(command.CategorySlug.Trim().ToLowerInvariant(), cancellationToken);
        if (category is null)
        {
            return Result.Failure<SellerProductDto>(CatalogErrors.CategoryNotFound);
        }

        var slug = await ReserveSlugAsync(command.Title, cancellationToken);
        var draft = Product.CreateDraft(
            store.Value.StoreId, category.Id, command.Title, slug, command.Description, clock);
        if (draft.IsFailure)
        {
            return Result.Failure<SellerProductDto>(draft.Error);
        }

        var product = draft.Value;
        foreach (var input in command.Variants)
        {
            var price = Money.Create(input.Price, input.Currency);
            if (price.IsFailure)
            {
                return Result.Failure<SellerProductDto>(price.Error);
            }

            var licenseType = Enum.Parse<LicenseType>(input.LicenseType, ignoreCase: true);
            var variant = product.AddVariant(input.Name, price.Value, licenseType, input.DownloadLimit, clock);
            if (variant.IsFailure)
            {
                return Result.Failure<SellerProductDto>(variant.Error);
            }
        }

        product.ReleaseVersion("1.0.0", "Initial release.", clock);

        foreach (var tagName in command.Tags.Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0).Distinct())
        {
            var tag = await tags.FindByNameAsync(tagName, cancellationToken);
            if (tag is null)
            {
                tag = Tag.Create(tagName);
                tags.Add(tag);
            }

            product.AddTag(tag);
        }

        products.Add(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(product.ToSellerDto());
    }

    /// <summary>
    /// Slugs are derived from the title and disambiguated with a short suffix,
    /// so two sellers can use the same product name.
    /// </summary>
    private async Task<string> ReserveSlugAsync(string title, CancellationToken ct)
    {
        var baseSlug = Slugify(title);
        if (!await products.SlugExistsAsync(baseSlug, ct))
        {
            return baseSlug;
        }

        for (var attempt = 2; attempt <= 50; attempt++)
        {
            var candidate = $"{baseSlug}-{attempt}";
            if (!await products.SlugExistsAsync(candidate, ct))
            {
                return candidate;
            }
        }

        return $"{baseSlug}-{Guid.NewGuid().ToString("n")[..6]}";
    }

    private static string Slugify(string title)
    {
        var lowered = title.Trim().ToLowerInvariant();
        var builder = new System.Text.StringBuilder(lowered.Length);
        var lastWasHyphen = false;
        foreach (var character in lowered)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen && builder.Length > 0)
            {
                builder.Append('-');
                lastWasHyphen = true;
            }
        }

        var slug = builder.ToString().Trim('-');
        if (slug.Length > 200)
        {
            slug = slug[..200].Trim('-');
        }

        return slug.Length >= 3 ? slug : $"product-{Guid.NewGuid().ToString("n")[..8]}";
    }
}

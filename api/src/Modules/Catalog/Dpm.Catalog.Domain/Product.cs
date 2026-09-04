using System.Text.RegularExpressions;
using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;
using Dpm.Catalog.Domain.Events;

namespace Dpm.Catalog.Domain;

/// <summary>
/// Catalog aggregate root (catalog.Products). Owns its variants and versions and
/// enforces the moderation lifecycle plus the publish invariant: a product may
/// not be published without at least one active variant that has at least one
/// Clean file (spec 04 section 4.7, 02 section 2.4 Product Management).
/// </summary>
public sealed partial class Product : AggregateRoot
{
    private readonly List<ProductVariant> _variants = [];
    private readonly List<ProductVersion> _versions = [];
    private readonly List<Tag> _tags = [];

    public long StoreId { get; private set; }

    public int CategoryId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public ProductStatus Status { get; private set; } = ProductStatus.Draft;

    public string? SeoTitle { get; private set; }

    public string? SeoDescription { get; private set; }

    public decimal RatingAvg { get; private set; }

    public int RatingCount { get; private set; }

    public DateTime? PublishedAtUtc { get; private set; }

    public DateTime? ScheduledPublishUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public bool IsDeleted { get; private set; }

    public IReadOnlyList<ProductVariant> Variants => _variants.AsReadOnly();

    public IReadOnlyList<ProductVersion> Versions => _versions.AsReadOnly();

    public IReadOnlyList<Tag> Tags => _tags.AsReadOnly();

    private Product()
    {
    }

    public static Result<Product> CreateDraft(
        long storeId,
        int categoryId,
        string title,
        string slug,
        string? description,
        IClock clock)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length < 3 || title.Trim().Length > 200)
        {
            return Result.Failure<Product>(
                Error.Validation("VALIDATION_ERROR", "Title must be between 3 and 200 characters."));
        }

        var normalizedSlug = NormalizeSlug(slug);
        if (!IsValidSlug(normalizedSlug))
        {
            return Result.Failure<Product>(Error.Validation(
                "VALIDATION_ERROR",
                "Slug must be 3 to 220 characters of lowercase letters, digits and single hyphens."));
        }

        var now = clock.UtcNow;
        return Result.Success(new Product
        {
            PublicId = Guid.NewGuid(),
            StoreId = storeId,
            CategoryId = categoryId,
            Title = title.Trim(),
            Slug = normalizedSlug,
            Description = description?.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
    }

    public static string NormalizeSlug(string slug) => slug.Trim().ToLowerInvariant();

    public static bool IsValidSlug(string slug) =>
        slug.Length is >= 3 and <= 220 && SlugPattern().IsMatch(slug);

    public Result UpdateDetails(
        string title,
        string? description,
        int categoryId,
        string? seoTitle,
        string? seoDescription,
        IClock clock)
    {
        if (Status is ProductStatus.Archived)
        {
            return Result.Failure(Error.Conflict("CONFLICT", "An archived product cannot be edited."));
        }

        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length is < 3 or > 200)
        {
            return Result.Failure(Error.Validation("VALIDATION_ERROR", "Title must be between 3 and 200 characters."));
        }

        Title = title.Trim();
        Description = description?.Trim();
        CategoryId = categoryId;
        SeoTitle = seoTitle?.Trim();
        SeoDescription = seoDescription?.Trim();
        UpdatedAtUtc = clock.UtcNow;
        return Result.Success();
    }

    public Result<ProductVariant> AddVariant(
        string name,
        Money price,
        LicenseType licenseType,
        int downloadLimit,
        IClock clock)
    {
        if (Status is ProductStatus.Archived)
        {
            return Result.Failure<ProductVariant>(Error.Conflict("CONFLICT", "An archived product cannot be edited."));
        }

        if (_variants.Count > 0 && _variants[0].PriceCurrency != price.Currency)
        {
            return Result.Failure<ProductVariant>(Error.Validation(
                "VALIDATION_ERROR",
                "All variants of a product must share one currency."));
        }

        var variant = ProductVariant.Create(name, price, licenseType, downloadLimit);
        if (variant.IsFailure)
        {
            return variant;
        }

        _variants.Add(variant.Value);
        UpdatedAtUtc = clock.UtcNow;
        return variant;
    }

    public Result UpdateVariant(
        Guid variantPublicId,
        string name,
        Money price,
        LicenseType licenseType,
        int downloadLimit,
        bool isActive,
        IClock clock)
    {
        var variant = _variants.FirstOrDefault(v => v.PublicId == variantPublicId);
        if (variant is null)
        {
            return Result.Failure(Error.NotFound("NOT_FOUND", "Variant not found."));
        }

        if (_variants.Any(v => v.PublicId != variantPublicId && v.PriceCurrency != price.Currency))
        {
            return Result.Failure(Error.Validation(
                "VALIDATION_ERROR",
                "All variants of a product must share one currency."));
        }

        var update = variant.Update(name, price, licenseType, downloadLimit, isActive);
        if (update.IsFailure)
        {
            return update;
        }

        UpdatedAtUtc = clock.UtcNow;
        return Result.Success();
    }

    public Result<ProductVersion> ReleaseVersion(string versionNumber, string? changelog, IClock clock)
    {
        if (_versions.Any(v => v.VersionNumber == versionNumber.Trim()))
        {
            return Result.Failure<ProductVersion>(
                Error.Conflict("CONFLICT", "That version number already exists for this product."));
        }

        var version = ProductVersion.Create(versionNumber, changelog, clock);
        _versions.Add(version);
        UpdatedAtUtc = clock.UtcNow;
        Raise(new ProductVersionReleased(PublicId, version.VersionNumber));
        return Result.Success(version);
    }

    public void AddTag(Tag tag)
    {
        if (_tags.All(t => t.Name != tag.Name))
        {
            _tags.Add(tag);
        }
    }

    public Result SubmitForReview(Guid storePublicId, IClock clock)
    {
        if (Status is not (ProductStatus.Draft or ProductStatus.Rejected))
        {
            return Result.Failure(Error.Conflict(
                "CONFLICT",
                $"A product in {Status} cannot be submitted for review."));
        }

        var readiness = CheckPublishReadiness();
        if (readiness.IsFailure)
        {
            return readiness;
        }

        Status = ProductStatus.Submitted;
        UpdatedAtUtc = clock.UtcNow;
        Raise(new ProductSubmitted(PublicId, storePublicId));
        return Result.Success();
    }

    public Result BeginReview(IClock clock)
    {
        if (Status != ProductStatus.Submitted)
        {
            return Result.Failure(Error.Conflict("CONFLICT", "Only a submitted product can enter review."));
        }

        Status = ProductStatus.InReview;
        UpdatedAtUtc = clock.UtcNow;
        return Result.Success();
    }

    public Result Approve(IClock clock)
    {
        if (Status is not (ProductStatus.Submitted or ProductStatus.InReview))
        {
            return Result.Failure(Error.Conflict("CONFLICT", "Only a submitted product can be approved."));
        }

        Status = ProductStatus.Approved;
        UpdatedAtUtc = clock.UtcNow;
        Raise(new ProductApproved(PublicId));
        return Result.Success();
    }

    public Result Reject(string reason, IClock clock)
    {
        if (Status is not (ProductStatus.Submitted or ProductStatus.InReview))
        {
            return Result.Failure(Error.Conflict("CONFLICT", "Only a submitted product can be rejected."));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(Error.Validation("VALIDATION_ERROR", "A rejection reason is required."));
        }

        Status = ProductStatus.Rejected;
        UpdatedAtUtc = clock.UtcNow;
        Raise(new ProductRejected(PublicId, reason.Trim()));
        return Result.Success();
    }

    /// <summary>
    /// Publishing requires moderator approval and a deliverable product. Both
    /// conditions are checked here so no caller can publish around them.
    /// </summary>
    public Result Publish(bool storeCanPublish, IClock clock)
    {
        if (Status != ProductStatus.Approved)
        {
            return Result.Failure(Error.Conflict("CONFLICT", "Only an approved product can be published."));
        }

        if (!storeCanPublish)
        {
            return Result.Failure(Error.Forbidden(
                "KYC_REQUIRED",
                "Complete store verification, tax and payout setup before publishing."));
        }

        var readiness = CheckPublishReadiness();
        if (readiness.IsFailure)
        {
            return readiness;
        }

        Status = ProductStatus.Published;
        PublishedAtUtc = clock.UtcNow;
        ScheduledPublishUtc = null;
        UpdatedAtUtc = clock.UtcNow;
        Raise(new ProductPublished(PublicId, Slug));
        return Result.Success();
    }

    public Result Unpublish(IClock clock)
    {
        if (Status != ProductStatus.Published)
        {
            return Result.Failure(Error.Conflict("CONFLICT", "Only a published product can be unpublished."));
        }

        Status = ProductStatus.Approved;
        UpdatedAtUtc = clock.UtcNow;
        Raise(new ProductUnpublished(PublicId));
        return Result.Success();
    }

    public Result Schedule(DateTime publishAtUtc, IClock clock)
    {
        if (Status != ProductStatus.Approved)
        {
            return Result.Failure(Error.Conflict("CONFLICT", "Only an approved product can be scheduled."));
        }

        if (publishAtUtc <= clock.UtcNow)
        {
            return Result.Failure(Error.Validation("VALIDATION_ERROR", "Scheduled publish time must be in the future."));
        }

        ScheduledPublishUtc = publishAtUtc;
        UpdatedAtUtc = clock.UtcNow;
        return Result.Success();
    }

    public Result Archive(IClock clock)
    {
        if (Status == ProductStatus.Archived)
        {
            return Result.Success();
        }

        Status = ProductStatus.Archived;
        PublishedAtUtc = null;
        UpdatedAtUtc = clock.UtcNow;
        return Result.Success();
    }

    /// <summary>
    /// The deliverability invariant: at least one active variant carrying at
    /// least one file that has passed the virus scan.
    /// </summary>
    public Result CheckPublishReadiness()
    {
        if (_variants.Count == 0)
        {
            return Result.Failure(Error.Conflict("CONFLICT", "Add at least one variant before submitting."));
        }

        return _variants.Any(v => v.IsActive && v.HasCleanFile)
            ? Result.Success()
            : Result.Failure(Error.Conflict(
                "CONFLICT",
                "At least one active variant needs a file that has passed the virus scan."));
    }

    public bool IsPubliclyVisible => Status == ProductStatus.Published && !IsDeleted;

    /// <summary>Projects a scan outcome from the Files module onto the owning variant.</summary>
    public void ApplyVariantFileReadiness(Guid variantPublicId, int cleanFileCount, IClock clock)
    {
        var variant = _variants.FirstOrDefault(v => v.PublicId == variantPublicId);
        if (variant is null)
        {
            return;
        }

        variant.ApplyFileReadiness(cleanFileCount);
        UpdatedAtUtc = clock.UtcNow;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}

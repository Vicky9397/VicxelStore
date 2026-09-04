namespace Dpm.Catalog.Application.Contracts;

public sealed record MoneyDto(decimal Amount, string Currency);

public sealed record VariantDto(
    Guid Id,
    string Name,
    MoneyDto Price,
    string LicenseType,
    int DownloadLimit,
    bool IsActive,
    bool HasCleanFile);

public sealed record VersionDto(string VersionNumber, string? Changelog, DateTime ReleasedAtUtc);

public sealed record CategoryDto(int Id, string Slug, string Name, int? ParentId);

public sealed record ProductSummaryDto(
    Guid Id,
    string Slug,
    string Title,
    string StoreSlug,
    string StoreName,
    string CategorySlug,
    MoneyDto FromPrice,
    decimal RatingAvg,
    int RatingCount,
    DateTime? PublishedAtUtc);

public sealed record ProductDetailDto(
    Guid Id,
    string Slug,
    string Title,
    string? Description,
    string StoreSlug,
    string StoreName,
    string CategorySlug,
    decimal RatingAvg,
    int RatingCount,
    DateTime? PublishedAtUtc,
    IReadOnlyList<VariantDto> Variants,
    IReadOnlyList<VersionDto> Versions,
    IReadOnlyList<string> Tags);

public sealed record SellerProductDto(
    Guid Id,
    string Slug,
    string Title,
    string Status,
    int VariantCount,
    bool IsPublishReady,
    DateTime UpdatedAtUtc);

public sealed record ModerationQueueItemDto(
    Guid Id,
    string Title,
    string StoreName,
    string Status,
    DateTime SubmittedAtUtc);

public sealed record PageInfo(int Page, int PageSize, int Total);

public sealed record ProductPage(IReadOnlyList<ProductSummaryDto> Data, PageInfo Page);

/// <param name="Sort">Allow-listed sort key; anything else falls back to newest first.</param>
public sealed record ProductSearch(
    string? Query,
    string? CategorySlug,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? LicenseType,
    string? Sort,
    int Page,
    int PageSize);

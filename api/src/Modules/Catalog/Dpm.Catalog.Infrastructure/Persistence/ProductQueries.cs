using System.Data;
using Dapper;
using Dpm.Catalog.Application.Abstractions;
using Dpm.Catalog.Application.Contracts;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Dpm.Catalog.Infrastructure.Persistence;

/// <summary>
/// Read-side storefront queries. These bypass the aggregate and project
/// straight to DTOs (spec 10.1 CQRS structure). Every filter is parameterized
/// and every sort key is allow-listed, so no user input reaches the SQL text.
/// </summary>
public sealed class ProductQueries(IConfiguration configuration) : IProductQueries
{
    private const byte PublishedStatus = 5;
    private const byte SubmittedStatus = 2;
    private const byte InReviewStatus = 3;

    private SqlConnection OpenConnection() =>
        new(configuration.GetConnectionString("Database"));

    public async Task<ProductPage> SearchAsync(ProductSearch search, CancellationToken ct)
    {
        var filters = new List<string>
        {
            "P.Status = @PublishedStatus",
            "P.IsDeleted = 0",
        };

        var parameters = new DynamicParameters();
        parameters.Add("PublishedStatus", PublishedStatus, DbType.Byte);

        if (!string.IsNullOrWhiteSpace(search.Query))
        {
            filters.Add("(P.Title LIKE @Query OR P.Description LIKE @Query)");
            parameters.Add("Query", $"%{search.Query.Trim()}%", DbType.String);
        }

        if (!string.IsNullOrWhiteSpace(search.CategorySlug))
        {
            filters.Add("C.Slug = @CategorySlug");
            parameters.Add("CategorySlug", search.CategorySlug.Trim().ToLowerInvariant(), DbType.String);
        }

        if (search.MinPrice is { } minPrice)
        {
            filters.Add("V.MinPrice >= @MinPrice");
            parameters.Add("MinPrice", minPrice, DbType.Decimal);
        }

        if (search.MaxPrice is { } maxPrice)
        {
            filters.Add("V.MinPrice <= @MaxPrice");
            parameters.Add("MaxPrice", maxPrice, DbType.Decimal);
        }

        if (!string.IsNullOrWhiteSpace(search.LicenseType)
            && Enum.TryParse<Domain.LicenseType>(search.LicenseType, ignoreCase: true, out var licenseType))
        {
            filters.Add("V.LicenseTypes LIKE @LicenseType");
            parameters.Add("LicenseType", $"%|{(byte)licenseType}|%", DbType.String);
        }

        parameters.Add("Offset", (search.Page - 1) * search.PageSize, DbType.Int32);
        parameters.Add("PageSize", search.PageSize, DbType.Int32);

        var where = string.Join(" AND ", filters);
        var orderBy = ResolveSort(search.Sort);

        var sql = $"""
            WITH VariantRollup AS (
                SELECT
                    PV.ProductId,
                    MIN(PV.PriceAmount) AS MinPrice,
                    MIN(PV.PriceCurrency) AS Currency,
                    '|' + STRING_AGG(CAST(PV.LicenseType AS VARCHAR(3)), '|') + '|' AS LicenseTypes
                FROM catalog.ProductVariants AS PV
                WHERE PV.IsActive = 1
                GROUP BY PV.ProductId
            )
            SELECT
                P.PublicId, P.Slug, P.Title, S.Slug AS StoreSlug, S.Name AS StoreName,
                C.Slug AS CategorySlug, V.MinPrice, V.Currency,
                P.RatingAvg, P.RatingCount, P.PublishedAtUtc
            FROM catalog.Products AS P
            INNER JOIN market.Stores AS S ON S.Id = P.StoreId
            INNER JOIN catalog.Categories AS C ON C.Id = P.CategoryId
            INNER JOIN VariantRollup AS V ON V.ProductId = P.Id
            WHERE {where}
            ORDER BY {orderBy}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*)
            FROM catalog.Products AS P
            INNER JOIN market.Stores AS S ON S.Id = P.StoreId
            INNER JOIN catalog.Categories AS C ON C.Id = P.CategoryId
            INNER JOIN VariantRollup AS V ON V.ProductId = P.Id
            WHERE {where};
            """;

        using var connection = OpenConnection();
        using var reader = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, parameters, cancellationToken: ct));

        var rows = (await reader.ReadAsync<ProductSummaryRow>()).ToList();
        var total = await reader.ReadSingleAsync<int>();

        IReadOnlyList<ProductSummaryDto> data = rows
            .Select(r => new ProductSummaryDto(
                r.PublicId,
                r.Slug,
                r.Title,
                r.StoreSlug,
                r.StoreName,
                r.CategorySlug,
                new MoneyDto(r.MinPrice, r.Currency),
                r.RatingAvg,
                r.RatingCount,
                r.PublishedAtUtc))
            .ToList();

        return new ProductPage(data, new PageInfo(search.Page, search.PageSize, total));
    }

    /// <summary>Sort keys are allow-listed; anything unrecognized falls back to newest first.</summary>
    private static string ResolveSort(string? sort) => sort switch
    {
        "price" => "V.MinPrice ASC, P.Id DESC",
        "-price" => "V.MinPrice DESC, P.Id DESC",
        "rating" => "P.RatingAvg DESC, P.RatingCount DESC, P.Id DESC",
        "title" => "P.Title ASC, P.Id DESC",
        _ => "P.PublishedAtUtc DESC, P.Id DESC",
    };

    public async Task<ProductDetailDto?> FindPublishedBySlugAsync(string slug, CancellationToken ct)
    {
        const string sql = """
            SELECT
                P.Id, P.PublicId, P.Slug, P.Title, P.Description,
                S.Slug AS StoreSlug, S.Name AS StoreName, C.Slug AS CategorySlug,
                P.RatingAvg, P.RatingCount, P.PublishedAtUtc
            FROM catalog.Products AS P
            INNER JOIN market.Stores AS S ON S.Id = P.StoreId
            INNER JOIN catalog.Categories AS C ON C.Id = P.CategoryId
            WHERE P.Slug = @Slug AND P.Status = @PublishedStatus AND P.IsDeleted = 0;

            SELECT PV.PublicId, PV.Name, PV.PriceAmount, PV.PriceCurrency, PV.LicenseType,
                   PV.DownloadLimit, PV.IsActive,
                   COALESCE(R.CleanFileCount, 0) AS CleanFileCount
            FROM catalog.ProductVariants AS PV
            INNER JOIN catalog.Products AS P ON P.Id = PV.ProductId
            LEFT JOIN catalog.VariantFileReadiness AS R ON R.VariantId = PV.Id
            WHERE P.Slug = @Slug AND P.Status = @PublishedStatus AND PV.IsActive = 1
            ORDER BY PV.PriceAmount ASC;

            SELECT PVer.VersionNumber, PVer.Changelog, PVer.ReleasedAtUtc
            FROM catalog.ProductVersions AS PVer
            INNER JOIN catalog.Products AS P ON P.Id = PVer.ProductId
            WHERE P.Slug = @Slug
            ORDER BY PVer.ReleasedAtUtc DESC;

            SELECT T.Name
            FROM catalog.Tags AS T
            INNER JOIN catalog.ProductTags AS PT ON PT.TagId = T.Id
            INNER JOIN catalog.Products AS P ON P.Id = PT.ProductId
            WHERE P.Slug = @Slug;
            """;

        using var connection = OpenConnection();
        using var reader = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { Slug = slug, PublishedStatus },
            cancellationToken: ct));

        var product = await reader.ReadSingleOrDefaultAsync<ProductDetailRow>();
        if (product is null)
        {
            return null;
        }

        var variants = (await reader.ReadAsync<VariantRow>())
            .Select(v => new VariantDto(
                v.PublicId,
                v.Name,
                new MoneyDto(v.PriceAmount, v.PriceCurrency),
                ((Domain.LicenseType)v.LicenseType).ToString(),
                v.DownloadLimit,
                v.IsActive,
                v.CleanFileCount > 0))
            .ToList();

        var versions = (await reader.ReadAsync<VersionRow>())
            .Select(v => new VersionDto(v.VersionNumber, v.Changelog, v.ReleasedAtUtc))
            .ToList();

        var tags = (await reader.ReadAsync<string>()).ToList();

        return new ProductDetailDto(
            product.PublicId,
            product.Slug,
            product.Title,
            product.Description,
            product.StoreSlug,
            product.StoreName,
            product.CategorySlug,
            product.RatingAvg,
            product.RatingCount,
            product.PublishedAtUtc,
            variants,
            versions,
            tags);
    }

    public async Task<IReadOnlyList<SellerProductDto>> ListForStoreAsync(long storeId, CancellationToken ct)
    {
        const string sql = """
            SELECT
                P.PublicId, P.Slug, P.Title, P.Status,
                COUNT(PV.Id) AS VariantCount,
                CASE WHEN SUM(CASE WHEN PV.IsActive = 1 AND COALESCE(R.CleanFileCount, 0) > 0
                                   THEN 1 ELSE 0 END) > 0
                     THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS IsPublishReady,
                P.UpdatedAtUtc
            FROM catalog.Products AS P
            LEFT JOIN catalog.ProductVariants AS PV ON PV.ProductId = P.Id
            LEFT JOIN catalog.VariantFileReadiness AS R ON R.VariantId = PV.Id
            WHERE P.StoreId = @StoreId AND P.IsDeleted = 0
            GROUP BY P.PublicId, P.Slug, P.Title, P.Status, P.UpdatedAtUtc
            ORDER BY P.UpdatedAtUtc DESC;
            """;

        using var connection = OpenConnection();
        var rows = await connection.QueryAsync<SellerProductRow>(
            new CommandDefinition(sql, new { StoreId = storeId }, cancellationToken: ct));

        return rows
            .Select(r => new SellerProductDto(
                r.PublicId,
                r.Slug,
                r.Title,
                ((Domain.ProductStatus)r.Status).ToString(),
                r.VariantCount,
                r.IsPublishReady,
                r.UpdatedAtUtc))
            .ToList();
    }

    public async Task<IReadOnlyList<ModerationQueueItemDto>> ListModerationQueueAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT P.PublicId, P.Title, S.Name AS StoreName, P.Status, P.UpdatedAtUtc AS SubmittedAtUtc
            FROM catalog.Products AS P
            INNER JOIN market.Stores AS S ON S.Id = P.StoreId
            WHERE P.Status IN (@Submitted, @InReview) AND P.IsDeleted = 0
            ORDER BY P.UpdatedAtUtc ASC;
            """;

        using var connection = OpenConnection();
        var rows = await connection.QueryAsync<ModerationRow>(new CommandDefinition(
            sql,
            new { Submitted = SubmittedStatus, InReview = InReviewStatus },
            cancellationToken: ct));

        return rows
            .Select(r => new ModerationQueueItemDto(
                r.PublicId,
                r.Title,
                r.StoreName,
                ((Domain.ProductStatus)r.Status).ToString(),
                r.SubmittedAtUtc))
            .ToList();
    }

    private sealed record ProductSummaryRow(
        Guid PublicId, string Slug, string Title, string StoreSlug, string StoreName,
        string CategorySlug, decimal MinPrice, string Currency, decimal RatingAvg,
        int RatingCount, DateTime? PublishedAtUtc);

    private sealed record ProductDetailRow(
        long Id, Guid PublicId, string Slug, string Title, string? Description,
        string StoreSlug, string StoreName, string CategorySlug, decimal RatingAvg,
        int RatingCount, DateTime? PublishedAtUtc);

    private sealed record VariantRow(
        Guid PublicId, string Name, decimal PriceAmount, string PriceCurrency,
        byte LicenseType, int DownloadLimit, bool IsActive, int CleanFileCount);

    private sealed record VersionRow(string VersionNumber, string? Changelog, DateTime ReleasedAtUtc);

    private sealed record SellerProductRow(
        Guid PublicId, string Slug, string Title, byte Status, int VariantCount,
        bool IsPublishReady, DateTime UpdatedAtUtc);

    private sealed record ModerationRow(
        Guid PublicId, string Title, string StoreName, byte Status, DateTime SubmittedAtUtc);
}

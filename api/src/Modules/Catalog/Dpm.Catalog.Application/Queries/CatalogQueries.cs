using Dpm.BuildingBlocks.Application;
using Dpm.Catalog.Application.Abstractions;
using Dpm.Catalog.Application.Contracts;
using Dpm.Marketplace.Contracts;

namespace Dpm.Catalog.Application.Queries;

public sealed record SearchProductsQuery(
    string? Query,
    string? CategorySlug,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? LicenseType,
    string? Sort,
    int Page,
    int PageSize) : IQuery<ProductPage>;

/// <summary>Public storefront search. Only published, non-deleted products are visible.</summary>
public sealed class SearchProductsQueryHandler(IProductQueries queries)
    : IQueryHandler<SearchProductsQuery, ProductPage>
{
    private const int MaxPageSize = 60;

    public async Task<Result<ProductPage>> Handle(SearchProductsQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, MaxPageSize);

        var result = await queries.SearchAsync(
            new ProductSearch(
                query.Query,
                query.CategorySlug,
                query.MinPrice,
                query.MaxPrice,
                query.LicenseType,
                query.Sort,
                page,
                pageSize),
            cancellationToken);

        return Result.Success(result);
    }
}

public sealed record GetProductBySlugQuery(string Slug) : IQuery<ProductDetailDto>;

public sealed class GetProductBySlugQueryHandler(IProductQueries queries)
    : IQueryHandler<GetProductBySlugQuery, ProductDetailDto>
{
    public async Task<Result<ProductDetailDto>> Handle(GetProductBySlugQuery query, CancellationToken cancellationToken)
    {
        var product = await queries.FindPublishedBySlugAsync(
            query.Slug.Trim().ToLowerInvariant(), cancellationToken);
        return product is null
            ? Result.Failure<ProductDetailDto>(CatalogErrors.ProductNotFound)
            : Result.Success(product);
    }
}

public sealed record ListMyProductsQuery : IQuery<IReadOnlyList<SellerProductDto>>;

public sealed class ListMyProductsQueryHandler(IProductQueries queries, IStoreDirectory stores)
    : IQueryHandler<ListMyProductsQuery, IReadOnlyList<SellerProductDto>>
{
    public async Task<Result<IReadOnlyList<SellerProductDto>>> Handle(
        ListMyProductsQuery query,
        CancellationToken cancellationToken)
    {
        var store = await OwnedProduct.ResolveStoreAsync(stores, cancellationToken);
        if (store.IsFailure)
        {
            return Result.Failure<IReadOnlyList<SellerProductDto>>(store.Error);
        }

        var products = await queries.ListForStoreAsync(store.Value.StoreId, cancellationToken);
        return Result.Success(products);
    }
}

public sealed record ListCategoriesQuery : IQuery<IReadOnlyList<CategoryDto>>;

public sealed class ListCategoriesQueryHandler(ICategoryRepository categories)
    : IQueryHandler<ListCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    public async Task<Result<IReadOnlyList<CategoryDto>>> Handle(
        ListCategoriesQuery query,
        CancellationToken cancellationToken)
    {
        var all = await categories.ListAsync(cancellationToken);
        IReadOnlyList<CategoryDto> dtos = all
            .Select(c => new CategoryDto(c.Id, c.Slug, c.Name, c.ParentId))
            .ToList();
        return Result.Success(dtos);
    }
}

public sealed record ListModerationQueueQuery : IQuery<IReadOnlyList<ModerationQueueItemDto>>;

public sealed class ListModerationQueueQueryHandler(IProductQueries queries)
    : IQueryHandler<ListModerationQueueQuery, IReadOnlyList<ModerationQueueItemDto>>
{
    public async Task<Result<IReadOnlyList<ModerationQueueItemDto>>> Handle(
        ListModerationQueueQuery query,
        CancellationToken cancellationToken)
    {
        var items = await queries.ListModerationQueueAsync(cancellationToken);
        return Result.Success(items);
    }
}

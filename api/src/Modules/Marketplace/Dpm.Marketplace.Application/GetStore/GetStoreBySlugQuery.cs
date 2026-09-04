using Dpm.BuildingBlocks.Application;
using Dpm.Marketplace.Application.Abstractions;
using Dpm.Marketplace.Application.Contracts;
using Dpm.Marketplace.Domain;

namespace Dpm.Marketplace.Application.GetStore;

public sealed record GetStoreBySlugQuery(string Slug) : IQuery<StoreDto>;

/// <summary>Public storefront lookup. Suspended stores are not visible.</summary>
public sealed class GetStoreBySlugQueryHandler(IStoreRepository stores)
    : IQueryHandler<GetStoreBySlugQuery, StoreDto>
{
    public async Task<Result<StoreDto>> Handle(GetStoreBySlugQuery query, CancellationToken cancellationToken)
    {
        var store = await stores.FindBySlugAsync(Store.NormalizeSlug(query.Slug), cancellationToken);
        return store is null || store.Status == StoreStatus.Suspended
            ? Result.Failure<StoreDto>(MarketplaceErrors.StoreNotFound)
            : Result.Success(store.ToDto());
    }
}

using Dpm.BuildingBlocks.Application;
using Dpm.Marketplace.Application.Abstractions;
using Dpm.Marketplace.Application.Contracts;

namespace Dpm.Marketplace.Application.GetStore;

public sealed record GetMyStoreQuery : IQuery<MyStoreDto>;

/// <summary>The owner's own view, including the verification state they must complete.</summary>
public sealed class GetMyStoreQueryHandler(IStoreRepository stores, ICurrentSeller currentSeller)
    : IQueryHandler<GetMyStoreQuery, MyStoreDto>
{
    public async Task<Result<MyStoreDto>> Handle(GetMyStoreQuery query, CancellationToken cancellationToken)
    {
        if (await currentSeller.ResolveUserIdAsync(cancellationToken) is not { } userId)
        {
            return Result.Failure<MyStoreDto>(MarketplaceErrors.NotAuthenticated);
        }

        var store = await stores.FindByOwnerUserIdAsync(userId, cancellationToken);
        return store is null
            ? Result.Failure<MyStoreDto>(MarketplaceErrors.StoreNotFound)
            : Result.Success(store.ToMyStoreDto());
    }
}

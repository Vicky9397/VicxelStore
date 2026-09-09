using Dpm.BuildingBlocks.Application;
using Dpm.Ledger.Application.Abstractions;
using Dpm.Ledger.Application.Contracts;
using Dpm.Marketplace.Contracts;

namespace Dpm.Ledger.Application.Queries;

public sealed record GetMyWalletQuery(string Currency) : IQuery<WalletDto>;

public sealed record GetMyStatementQuery(int Limit) : IQuery<IReadOnlyList<LedgerLineDto>>;

public sealed record RunReconciliationQuery : IQuery<ReconciliationDto>;

public static class LedgerErrors
{
    public static readonly Error NoStore =
        Error.Forbidden("FORBIDDEN", "Open a store to see earnings.");
}

/// <summary>
/// The seller's wallet, derived from the ledger on every read. There is no
/// stored balance to drift out of step (spec 03 section 3.3).
/// </summary>
public sealed class GetMyWalletQueryHandler(IBalanceQueries balances, IStoreDirectory stores)
    : IQueryHandler<GetMyWalletQuery, WalletDto>
{
    public async Task<Result<WalletDto>> Handle(GetMyWalletQuery query, CancellationToken cancellationToken)
    {
        var store = await stores.FindForCurrentUserAsync(cancellationToken);
        if (store is null)
        {
            return Result.Failure<WalletDto>(LedgerErrors.NoStore);
        }

        var wallet = await balances.GetWalletAsync(
            store.StoreId, query.Currency.ToUpperInvariant(), cancellationToken);
        return Result.Success(wallet);
    }
}

public sealed class GetMyStatementQueryHandler(IBalanceQueries balances, IStoreDirectory stores)
    : IQueryHandler<GetMyStatementQuery, IReadOnlyList<LedgerLineDto>>
{
    public async Task<Result<IReadOnlyList<LedgerLineDto>>> Handle(
        GetMyStatementQuery query,
        CancellationToken cancellationToken)
    {
        var store = await stores.FindForCurrentUserAsync(cancellationToken);
        if (store is null)
        {
            return Result.Failure<IReadOnlyList<LedgerLineDto>>(LedgerErrors.NoStore);
        }

        var limit = Math.Clamp(query.Limit <= 0 ? 50 : query.Limit, 1, 500);
        var lines = await balances.ListStatementAsync(store.StoreId, limit, cancellationToken);
        return Result.Success(lines);
    }
}

/// <summary>
/// The release gate from spec 09C: every posted transaction must balance. A
/// non-empty result is a finance alert, not a warning.
/// </summary>
public sealed class RunReconciliationQueryHandler(IBalanceQueries balances)
    : IQueryHandler<RunReconciliationQuery, ReconciliationDto>
{
    public async Task<Result<ReconciliationDto>> Handle(
        RunReconciliationQuery query,
        CancellationToken cancellationToken)
    {
        var discrepancies = await balances.FindUnbalancedAsync(cancellationToken);
        return Result.Success(new ReconciliationDto(discrepancies.Count == 0, discrepancies));
    }
}

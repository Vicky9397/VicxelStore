using Dpm.Ledger.Domain;

namespace Dpm.Ledger.Application.Abstractions;

public interface ILedgerRepository
{
    /// <summary>
    /// Whether a transaction already exists for this reference. Posting is driven
    /// by an at-least-once event, so this is what keeps a replay from writing the
    /// journal twice.
    /// </summary>
    Task<bool> ExistsForReferenceAsync(string refType, long refId, CancellationToken ct);

    void Add(LedgerTransaction transaction);
}

public interface ILedgerUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>Balances derived from the ledger; no cached figure is authoritative.</summary>
public interface IBalanceQueries
{
    Task<Contracts.WalletDto> GetWalletAsync(long storeId, string currency, CancellationToken ct);

    Task<IReadOnlyList<Contracts.LedgerLineDto>> ListStatementAsync(
        long storeId, int limit, CancellationToken ct);

    /// <summary>Transactions whose debits and credits disagree. Must always be empty.</summary>
    Task<IReadOnlyList<Contracts.UnbalancedTransactionDto>> FindUnbalancedAsync(CancellationToken ct);
}

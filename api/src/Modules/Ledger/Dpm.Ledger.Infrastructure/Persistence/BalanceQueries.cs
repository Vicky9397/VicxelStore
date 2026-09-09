using Dapper;
using Dpm.Ledger.Application.Abstractions;
using Dpm.Ledger.Application.Contracts;
using Dpm.Ledger.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Dpm.Ledger.Infrastructure.Persistence;

/// <summary>
/// Balances read straight from the journal. Every figure is a sum over
/// ledger.LedgerLines, so nothing can drift from the source of truth
/// (spec 03 section 3.3).
/// </summary>
public sealed class BalanceQueries(IConfiguration configuration) : IBalanceQueries
{
    private SqlConnection OpenConnection() => new(configuration.GetConnectionString("Database"));

    public async Task<WalletDto> GetWalletAsync(long storeId, string currency, CancellationToken ct)
    {
        const string sql = """
            SELECT
                COALESCE(SUM(CASE WHEN Account = @Pending
                                  THEN Credit - Debit ELSE 0 END), 0) AS Pending,
                COALESCE(SUM(CASE WHEN Account = @Available
                                  THEN Credit - Debit ELSE 0 END), 0) AS Available,
                COALESCE(SUM(CASE WHEN Account = @InTransit
                                  THEN Debit - Credit ELSE 0 END), 0) AS InTransit
            FROM ledger.LedgerLines
            WHERE SellerStoreId = @StoreId AND Currency = @Currency;
            """;

        using var connection = OpenConnection();
        var row = await connection.QuerySingleAsync<WalletRow>(new CommandDefinition(
            sql,
            new
            {
                StoreId = storeId,
                Currency = currency,
                Pending = LedgerAccount.SellerPayablePending,
                Available = LedgerAccount.SellerPayableAvailable,
                InTransit = LedgerAccount.PayoutClearingInTransit,
            },
            cancellationToken: ct));

        return new WalletDto(row.Pending, row.Available, row.InTransit, currency);
    }

    public async Task<IReadOnlyList<LedgerLineDto>> ListStatementAsync(
        long storeId,
        int limit,
        CancellationToken ct)
    {
        const string sql = """
            SELECT TOP (@Limit)
                LT.PublicId AS TransactionPublicId, LT.RefType, LL.Account,
                LL.Debit, LL.Credit, LL.Currency, LT.OccurredAtUtc, LT.Description
            FROM ledger.LedgerLines AS LL
            INNER JOIN ledger.LedgerTransactions AS LT ON LT.Id = LL.TransactionId
            WHERE LL.SellerStoreId = @StoreId
            ORDER BY LT.OccurredAtUtc DESC, LL.Id DESC;
            """;

        using var connection = OpenConnection();
        var rows = await connection.QueryAsync<LedgerLineDto>(new CommandDefinition(
            sql, new { StoreId = storeId, Limit = limit }, cancellationToken: ct));
        return rows.ToList();
    }

    /// <summary>
    /// The reconciliation gate: any transaction whose debits and credits disagree
    /// within a currency. A clean run returns nothing.
    /// </summary>
    public async Task<IReadOnlyList<UnbalancedTransactionDto>> FindUnbalancedAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT LT.PublicId AS TransactionPublicId, LT.RefType, LT.RefId, LL.Currency,
                   SUM(LL.Debit) AS TotalDebit, SUM(LL.Credit) AS TotalCredit
            FROM ledger.LedgerTransactions AS LT
            INNER JOIN ledger.LedgerLines AS LL ON LL.TransactionId = LT.Id
            GROUP BY LT.PublicId, LT.RefType, LT.RefId, LL.Currency
            HAVING SUM(LL.Debit) <> SUM(LL.Credit);
            """;

        using var connection = OpenConnection();
        var rows = await connection.QueryAsync<UnbalancedTransactionDto>(
            new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    private sealed record WalletRow(decimal Pending, decimal Available, decimal InTransit);
}

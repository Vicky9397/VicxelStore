using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;

namespace Dpm.Ledger.Domain;

/// <summary>
/// One side of a journal entry (ledger.LedgerLines). A line is either a debit or
/// a credit, never both, and it is never modified after it is written: the
/// ledger is append-only and corrections are new reversing entries
/// (spec 03 section 3.3).
/// </summary>
public sealed class LedgerLine : Entity
{
    public long TransactionId { get; private set; }

    public string Account { get; private set; } = string.Empty;

    /// <summary>Set on seller-scoped accounts so a wallet balance can be derived per store.</summary>
    public long? SellerStoreId { get; private set; }

    public decimal Debit { get; private set; }

    public decimal Credit { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    private LedgerLine()
    {
    }

    public static Result<LedgerLine> Debited(string account, Money amount, long? sellerStoreId = null) =>
        Create(account, amount, isDebit: true, sellerStoreId);

    public static Result<LedgerLine> Credited(string account, Money amount, long? sellerStoreId = null) =>
        Create(account, amount, isDebit: false, sellerStoreId);

    private static Result<LedgerLine> Create(string account, Money amount, bool isDebit, long? sellerStoreId)
    {
        if (!LedgerAccount.IsKnown(account))
        {
            return Result.Failure<LedgerLine>(Error.Failure(
                "LEDGER_UNKNOWN_ACCOUNT",
                $"'{account}' is not in the chart of accounts."));
        }

        if (amount.Amount < 0)
        {
            return Result.Failure<LedgerLine>(Error.Failure(
                "LEDGER_NEGATIVE_AMOUNT",
                "A ledger line amount cannot be negative; post the opposite side instead."));
        }

        return Result.Success(new LedgerLine
        {
            Account = account,
            SellerStoreId = sellerStoreId,
            Debit = isDebit ? amount.Amount : 0m,
            Credit = isDebit ? 0m : amount.Amount,
            Currency = amount.Currency,
        });
    }
}

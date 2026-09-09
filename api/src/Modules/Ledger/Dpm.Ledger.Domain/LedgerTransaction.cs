using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;

namespace Dpm.Ledger.Domain;

/// <summary>
/// One balanced journal entry (ledger.LedgerTransactions). The aggregate refuses
/// to exist unless its lines sum to zero, which is the ledger's central
/// invariant: total debits equal total credits, per currency
/// (spec 03 section 3.6 invariant 1).
///
/// Append-only. There is deliberately no method to change or remove a posted
/// line; a correction is a new reversing transaction.
/// </summary>
public sealed class LedgerTransaction : AggregateRoot
{
    private readonly List<LedgerLine> _lines = [];

    public string RefType { get; private set; } = string.Empty;

    public long RefId { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public string? Description { get; private set; }

    public IReadOnlyList<LedgerLine> Lines => _lines.AsReadOnly();

    private LedgerTransaction()
    {
    }

    public static Result<LedgerTransaction> Post(
        string refType,
        long refId,
        string? description,
        IReadOnlyList<LedgerLine> lines,
        IClock clock)
    {
        if (lines.Count == 0)
        {
            return Result.Failure<LedgerTransaction>(Error.Failure(
                "LEDGER_EMPTY", "A ledger transaction must have at least one line."));
        }

        var imbalance = FindImbalance(lines);
        if (imbalance is not null)
        {
            return Result.Failure<LedgerTransaction>(Error.Failure(
                "LEDGER_UNBALANCED",
                $"Journal does not balance in {imbalance.Currency}: " +
                $"debits {imbalance.Debits:0.0000}, credits {imbalance.Credits:0.0000}."));
        }

        var transaction = new LedgerTransaction
        {
            PublicId = Guid.NewGuid(),
            RefType = refType,
            RefId = refId,
            Description = description,
            OccurredAtUtc = clock.UtcNow,
        };
        transaction._lines.AddRange(lines);
        return Result.Success(transaction);
    }

    /// <summary>Debits and credits must match within every currency the entry touches.</summary>
    private static Imbalance? FindImbalance(IReadOnlyList<LedgerLine> lines)
    {
        foreach (var group in lines.GroupBy(l => l.Currency, StringComparer.Ordinal))
        {
            var debits = group.Sum(l => l.Debit);
            var credits = group.Sum(l => l.Credit);
            if (debits != credits)
            {
                return new Imbalance(group.Key, debits, credits);
            }
        }

        return null;
    }

    public bool IsBalanced => FindImbalance(_lines) is null;

    private sealed record Imbalance(string Currency, decimal Debits, decimal Credits);
}

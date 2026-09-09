namespace Dpm.Ledger.Application.Contracts;

public sealed record WalletDto(
    decimal Pending,
    decimal Available,
    decimal InTransit,
    string Currency);

public sealed record LedgerLineDto(
    Guid TransactionPublicId,
    string RefType,
    string Account,
    decimal Debit,
    decimal Credit,
    string Currency,
    DateTime OccurredAtUtc,
    string? Description);

public sealed record UnbalancedTransactionDto(
    Guid TransactionPublicId,
    string RefType,
    long RefId,
    string Currency,
    decimal TotalDebit,
    decimal TotalCredit);

public sealed record ReconciliationDto(bool IsClean, IReadOnlyList<UnbalancedTransactionDto> Discrepancies);

namespace Dpm.Ledger.Contracts;

/// <summary>The Ledger module's public surface for balance questions.</summary>
public interface ILedgerDirectory
{
    Task<WalletBalance> GetWalletAsync(long storeId, string currency, CancellationToken ct);
}

/// <param name="Pending">Earnings still inside the payout hold window.</param>
/// <param name="Available">Earnings cleared for withdrawal.</param>
public sealed record WalletBalance(decimal Pending, decimal Available, decimal InTransit, string Currency);

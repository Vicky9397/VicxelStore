using Dpm.Ledger.Application.Abstractions;
using Dpm.Ledger.Contracts;

namespace Dpm.Ledger.Infrastructure;

public sealed class LedgerDirectory(IBalanceQueries balances) : ILedgerDirectory
{
    public async Task<WalletBalance> GetWalletAsync(long storeId, string currency, CancellationToken ct)
    {
        var wallet = await balances.GetWalletAsync(storeId, currency, ct);
        return new WalletBalance(wallet.Pending, wallet.Available, wallet.InTransit, wallet.Currency);
    }
}

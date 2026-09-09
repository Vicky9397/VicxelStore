using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;

namespace Dpm.Ledger.Domain;

/// <param name="SellerStoreId">Which seller's payable the earning is credited to.</param>
public sealed record SaleLine(long SellerStoreId, Money UnitPrice, decimal CommissionPct);

/// <summary>
/// Builds the journal for a captured sale (spec 03 section 3.3).
///
/// Worked example from the spec: a 1,000 INR sale at 15% commission with 180 INR
/// GST and a 25 INR gateway fee posts cash 1,180 debit, tax 180 credit, seller
/// payable 850 credit, platform revenue 150 credit, gateway fees 25 debit and
/// cash 25 credit. Totals 1,205 on each side.
///
/// The gateway fee is modelled as an expense with a matching cash reduction
/// rather than netted out of the seller's earning, so the seller is credited the
/// full amount their price earned and the platform absorbs its own cost of
/// processing.
/// </summary>
public static class SaleJournal
{
    public static Result<IReadOnlyList<LedgerLine>> Build(
        IReadOnlyList<SaleLine> saleLines,
        Money taxTotal,
        Money gatewayFee,
        Money grandTotal)
    {
        if (saleLines.Count == 0)
        {
            return Result.Failure<IReadOnlyList<LedgerLine>>(Error.Failure(
                "LEDGER_EMPTY", "A sale journal needs at least one line."));
        }

        var currency = grandTotal.Currency;
        if (saleLines.Any(l => l.UnitPrice.Currency != currency)
            || taxTotal.Currency != currency
            || gatewayFee.Currency != currency)
        {
            return Result.Failure<IReadOnlyList<LedgerLine>>(Error.Failure(
                "LEDGER_CURRENCY_MISMATCH", "Every amount in one journal must share a currency."));
        }

        var lines = new List<LedgerLine>();
        var results = new List<Result<LedgerLine>>();

        // What the buyer actually paid arrives as cash at the provider.
        results.Add(LedgerLine.Debited(LedgerAccount.CashGateway, grandTotal));

        if (taxTotal.Amount > 0)
        {
            results.Add(LedgerLine.Credited(LedgerAccount.TaxPayable, taxTotal));
        }

        // Each seller is credited their earning; the platform keeps the commission.
        var platformRevenue = Money.Zero(currency);
        foreach (var group in saleLines.GroupBy(l => l.SellerStoreId))
        {
            var sellerEarning = Money.Zero(currency);
            foreach (var line in group)
            {
                var commission = line.UnitPrice.MultiplyPercent(line.CommissionPct);
                platformRevenue = platformRevenue.Add(commission);
                sellerEarning = sellerEarning.Add(line.UnitPrice.Subtract(commission));
            }

            if (sellerEarning.Amount > 0)
            {
                results.Add(LedgerLine.Credited(
                    LedgerAccount.SellerPayablePending, sellerEarning, group.Key));
            }
        }

        if (platformRevenue.Amount > 0)
        {
            results.Add(LedgerLine.Credited(LedgerAccount.PlatformRevenue, platformRevenue));
        }

        // The processing fee is an expense, and the cash it consumes never reached us.
        if (gatewayFee.Amount > 0)
        {
            results.Add(LedgerLine.Debited(LedgerAccount.GatewayFees, gatewayFee));
            results.Add(LedgerLine.Credited(LedgerAccount.CashGateway, gatewayFee));
        }

        foreach (var result in results)
        {
            if (result.IsFailure)
            {
                return Result.Failure<IReadOnlyList<LedgerLine>>(result.Error);
            }

            lines.Add(result.Value);
        }

        return Result.Success<IReadOnlyList<LedgerLine>>(lines);
    }

    /// <summary>
    /// Moves a seller's earning from Pending to Available once the hold expires
    /// (spec 03 section 3.3 balance derivation).
    /// </summary>
    public static Result<IReadOnlyList<LedgerLine>> BuildHoldRelease(long sellerStoreId, Money amount)
    {
        var debit = LedgerLine.Debited(LedgerAccount.SellerPayablePending, amount, sellerStoreId);
        var credit = LedgerLine.Credited(LedgerAccount.SellerPayableAvailable, amount, sellerStoreId);

        if (debit.IsFailure)
        {
            return Result.Failure<IReadOnlyList<LedgerLine>>(debit.Error);
        }

        if (credit.IsFailure)
        {
            return Result.Failure<IReadOnlyList<LedgerLine>>(credit.Error);
        }

        return Result.Success<IReadOnlyList<LedgerLine>>([debit.Value, credit.Value]);
    }
}

namespace Dpm.Ledger.Domain;

/// <summary>
/// The chart of accounts (spec 03 section 3.3). These strings are the ledger's
/// vocabulary and appear in every posted line, so they are fixed here rather
/// than spelled out at each call site.
/// </summary>
public static class LedgerAccount
{
    /// <summary>Funds held at the payment provider. Debit-normal.</summary>
    public const string CashGateway = "Cash/Gateway";

    /// <summary>Seller earnings inside the payout hold window. Credit-normal.</summary>
    public const string SellerPayablePending = "SellerPayable:Pending";

    /// <summary>Seller earnings cleared for withdrawal. Credit-normal.</summary>
    public const string SellerPayableAvailable = "SellerPayable:Available";

    /// <summary>Commission the platform earned. Credit-normal.</summary>
    public const string PlatformRevenue = "PlatformRevenue";

    /// <summary>GST or VAT collected and owed to the authority. Credit-normal.</summary>
    public const string TaxPayable = "TaxPayable";

    /// <summary>Processing fees charged by the gateway. Debit-normal expense.</summary>
    public const string GatewayFees = "GatewayFees";

    /// <summary>Reserve held against expected refunds.</summary>
    public const string RefundsReserve = "RefundsReserve";

    /// <summary>Payout initiated but not yet confirmed by the provider. Debit-normal.</summary>
    public const string PayoutClearingInTransit = "PayoutClearing:InTransit";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        CashGateway,
        SellerPayablePending,
        SellerPayableAvailable,
        PlatformRevenue,
        TaxPayable,
        GatewayFees,
        RefundsReserve,
        PayoutClearingInTransit,
    };

    public static bool IsKnown(string account) => All.Contains(account);
}

/// <summary>What a ledger transaction refers back to.</summary>
public static class LedgerRefType
{
    public const string Order = "Order";
    public const string Refund = "Refund";
    public const string Payout = "Payout";
    public const string Adjustment = "Adjustment";
    public const string HoldRelease = "HoldRelease";
}

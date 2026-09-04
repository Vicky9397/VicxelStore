-- Wallet read model: per-seller pending/available balances derived from the ledger.
-- The ledger is the source of truth; this view is a convenience projection
-- (snapshot table + reconciliation arrive with milestone M4).
CREATE OR ALTER VIEW ledger.vw_SellerWalletBalances
AS
SELECT
    LL.SellerStoreId AS StoreId,
    LL.Currency,
    SUM(CASE WHEN LL.Account = 'SellerPayable:Pending'   THEN LL.Credit - LL.Debit ELSE 0 END) AS PendingBalance,
    SUM(CASE WHEN LL.Account = 'SellerPayable:Available' THEN LL.Credit - LL.Debit ELSE 0 END) AS AvailableBalance,
    SUM(CASE WHEN LL.Account = 'PayoutClearing:InTransit' THEN LL.Debit - LL.Credit ELSE 0 END) AS InTransitAmount
FROM ledger.LedgerLines AS LL
WHERE LL.SellerStoreId IS NOT NULL
GROUP BY LL.SellerStoreId, LL.Currency;
GO

-- Pending (within hold period) balance for a seller store, derived from the ledger.
CREATE OR ALTER FUNCTION ledger.fn_SellerPendingBalance
(
    @StoreId BIGINT,
    @Currency CHAR(3)
)
RETURNS DECIMAL(19,4)
AS
BEGIN
    DECLARE @Balance DECIMAL(19,4);

    SELECT @Balance = COALESCE(SUM(LL.Credit) - SUM(LL.Debit), 0)
    FROM ledger.LedgerLines AS LL
    WHERE LL.SellerStoreId = @StoreId
      AND LL.Account = 'SellerPayable:Pending'
      AND LL.Currency = @Currency;

    RETURN @Balance;
END
GO

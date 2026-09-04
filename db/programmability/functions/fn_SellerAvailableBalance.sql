-- Available balance for a seller store, derived from the ledger (spec 03 section 3.3).
-- Credit-normal account: balance = Sum(credits) - Sum(debits).
CREATE OR ALTER FUNCTION ledger.fn_SellerAvailableBalance
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
      AND LL.Account = 'SellerPayable:Available'
      AND LL.Currency = @Currency;

    RETURN @Balance;
END
GO

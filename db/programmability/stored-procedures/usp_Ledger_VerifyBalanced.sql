-- Reconciliation gate (spec 03 section 3.6 invariant 1): every ledger transaction's
-- lines must sum to zero (Sum(debit) = Sum(credit)) per currency.
-- Returns offending transactions; @ThrowOnMismatch = 1 raises for use as a job gate.
CREATE OR ALTER PROCEDURE ledger.usp_Ledger_VerifyBalanced
    @ThrowOnMismatch BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        LT.Id AS TransactionId,
        LT.PublicId,
        LT.RefType,
        LT.RefId,
        LL.Currency,
        SUM(LL.Debit) AS TotalDebit,
        SUM(LL.Credit) AS TotalCredit
    INTO #Unbalanced
    FROM ledger.LedgerTransactions AS LT
    INNER JOIN ledger.LedgerLines AS LL ON LL.TransactionId = LT.Id
    GROUP BY LT.Id, LT.PublicId, LT.RefType, LT.RefId, LL.Currency
    HAVING SUM(LL.Debit) <> SUM(LL.Credit);

    SELECT TransactionId, PublicId, RefType, RefId, Currency, TotalDebit, TotalCredit
    FROM #Unbalanced
    ORDER BY TransactionId;

    IF @ThrowOnMismatch = 1 AND EXISTS (SELECT 1 FROM #Unbalanced)
    BEGIN
        DECLARE @Count INT = (SELECT COUNT(*) FROM #Unbalanced);
        DECLARE @Msg NVARCHAR(200) =
            CONCAT('Ledger reconciliation failed: ', @Count, ' unbalanced transaction(s).');
        THROW 50001, @Msg, 1;
    END
END
GO

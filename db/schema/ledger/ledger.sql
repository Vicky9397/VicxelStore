-- Module: ledger (spec 05 section 5.3). APPEND ONLY: no UPDATE/DELETE ever ships
-- against ledger.LedgerTransactions / ledger.LedgerLines. Corrections are new
-- reversing entries. Enforced by deploy lint (deploy/lint-append-only.sh).
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'ledger')
    EXEC('CREATE SCHEMA ledger');
GO

IF OBJECT_ID('ledger.LedgerTransactions', 'U') IS NULL
BEGIN
    CREATE TABLE ledger.LedgerTransactions (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        PublicId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        RefType NVARCHAR(30) NOT NULL,               -- Order/Refund/Payout/Adjustment/HoldRelease
        RefId BIGINT NOT NULL,
        OccurredAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        Description NVARCHAR(300) NULL,
        CONSTRAINT UQ_LT_PublicId UNIQUE (PublicId),
        INDEX IX_LT_Ref (RefType, RefId)
    );
END
GO

IF OBJECT_ID('ledger.LedgerLines', 'U') IS NULL
BEGIN
    CREATE TABLE ledger.LedgerLines (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        TransactionId BIGINT NOT NULL REFERENCES ledger.LedgerTransactions(Id),
        Account NVARCHAR(60) NOT NULL,               -- CashGateway/SellerPayablePending/etc.
        SellerStoreId BIGINT NULL,
        Debit DECIMAL(19,4) NOT NULL DEFAULT 0,
        Credit DECIMAL(19,4) NOT NULL DEFAULT 0,
        Currency CHAR(3) NOT NULL,
        CONSTRAINT CK_LL_OneSided CHECK (Debit = 0 OR Credit = 0),
        INDEX IX_LL_Txn (TransactionId),
        INDEX IX_LL_SellerAcct (SellerStoreId, Account)
    );  -- Sum(debit) = Sum(credit) per txn enforced in app + usp_Ledger_VerifyBalanced
END
GO

IF OBJECT_ID('ledger.Payouts', 'U') IS NULL
BEGIN
    CREATE TABLE ledger.Payouts (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        PublicId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        StoreId BIGINT NOT NULL REFERENCES market.Stores(Id),
        Amount DECIMAL(19,4) NOT NULL,
        Currency CHAR(3) NOT NULL,
        Status TINYINT NOT NULL DEFAULT 1,           -- 1 Requested,2 Processing,3 Paid,4 Failed,5 Cancelled
        Provider NVARCHAR(20) NULL,
        ProviderRef NVARCHAR(100) NULL,
        RequestedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        SettledAtUtc DATETIME2(3) NULL,
        FailureReason NVARCHAR(300) NULL,
        RowVersion ROWVERSION,
        CONSTRAINT UQ_Payouts_PublicId UNIQUE (PublicId),
        INDEX IX_Payouts_Store (StoreId),
        INDEX IX_Payouts_Status (Status)
    );
END
GO

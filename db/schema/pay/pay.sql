-- Module: pay (spec 05 section 5.3; Refunds per ER model 5.2)
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'pay')
    EXEC('CREATE SCHEMA pay');
GO

IF OBJECT_ID('pay.Payments', 'U') IS NULL
BEGIN
    CREATE TABLE pay.Payments (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        PublicId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        OrderId BIGINT NOT NULL REFERENCES orders.Orders(Id),
        Provider NVARCHAR(20) NOT NULL,
        ProviderIntentId NVARCHAR(100) NOT NULL,
        ProviderChargeId NVARCHAR(100) NULL,
        Amount DECIMAL(19,4) NOT NULL,
        Currency CHAR(3) NOT NULL,
        FxRate DECIMAL(19,8) NULL,
        SettlementCurrency CHAR(3) NULL,
        Status TINYINT NOT NULL DEFAULT 1,           -- 1 Created,2 Captured,3 Failed,4 Refunded,5 Disputed
        CreatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_Payments_PublicId UNIQUE (PublicId),
        INDEX IX_Pay_Order (OrderId),
        INDEX IX_Pay_Intent (ProviderIntentId)
    );
END
GO

IF OBJECT_ID('pay.Refunds', 'U') IS NULL
BEGIN
    CREATE TABLE pay.Refunds (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        PublicId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        PaymentId BIGINT NOT NULL REFERENCES pay.Payments(Id),
        Amount DECIMAL(19,4) NOT NULL,
        Currency CHAR(3) NOT NULL,
        Reason NVARCHAR(300) NULL,
        Status TINYINT NOT NULL DEFAULT 1,           -- 1 Requested,2 Approved,3 Denied,4 Settled,5 Failed
        ProviderRefundId NVARCHAR(100) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        SettledAtUtc DATETIME2(3) NULL,
        CONSTRAINT UQ_Refunds_PublicId UNIQUE (PublicId),
        INDEX IX_Refunds_Payment (PaymentId)
    );
END
GO

IF OBJECT_ID('pay.Idempotency', 'U') IS NULL
BEGIN
    CREATE TABLE pay.Idempotency (
        KeyHash CHAR(64) NOT NULL,
        Endpoint NVARCHAR(120) NOT NULL,
        ResponseJson NVARCHAR(MAX) NOT NULL,
        StatusCode INT NOT NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        PRIMARY KEY (KeyHash, Endpoint)
    );
END
GO

IF OBJECT_ID('pay.WebhookInbox', 'U') IS NULL
BEGIN
    CREATE TABLE pay.WebhookInbox (
        ProviderEventId NVARCHAR(120) PRIMARY KEY,
        Provider NVARCHAR(20) NOT NULL,
        Type NVARCHAR(60) NOT NULL,
        PayloadJson NVARCHAR(MAX) NOT NULL,
        ProcessedAtUtc DATETIME2(3) NULL,
        ReceivedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

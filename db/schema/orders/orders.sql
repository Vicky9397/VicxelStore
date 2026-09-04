-- Module: orders (spec 05 section 5.3; Carts per ER model 5.2 and module rules 02 section 2.4)
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'orders')
    EXEC('CREATE SCHEMA orders');
GO

IF OBJECT_ID('orders.Carts', 'U') IS NULL
BEGIN
    CREATE TABLE orders.Carts (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        PublicId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        UserId BIGINT NOT NULL REFERENCES [identity].Users(Id),
        UpdatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_Carts_PublicId UNIQUE (PublicId),
        CONSTRAINT UQ_Carts_User UNIQUE (UserId)     -- one persistent cart per user; guest carts live in a signed cookie
    );
END
GO

IF OBJECT_ID('orders.CartItems', 'U') IS NULL
BEGIN
    CREATE TABLE orders.CartItems (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        CartId BIGINT NOT NULL REFERENCES orders.Carts(Id),
        ProductId BIGINT NOT NULL REFERENCES catalog.Products(Id),
        VariantId BIGINT NOT NULL REFERENCES catalog.ProductVariants(Id),
        PriceSnapshotAmount DECIMAL(19,4) NOT NULL,  -- re-priced + confirmed at checkout
        PriceSnapshotCurrency CHAR(3) NOT NULL,
        SavedForLater BIT NOT NULL DEFAULT 0,
        AddedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_CartItems_CartVariant UNIQUE (CartId, VariantId),
        INDEX IX_CartItems_Cart (CartId)
    );
END
GO

IF OBJECT_ID('orders.Orders', 'U') IS NULL
BEGIN
    CREATE TABLE orders.Orders (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        PublicId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        BuyerUserId BIGINT NOT NULL REFERENCES [identity].Users(Id),
        Currency CHAR(3) NOT NULL,
        Subtotal DECIMAL(19,4) NOT NULL,
        DiscountTotal DECIMAL(19,4) NOT NULL DEFAULT 0,
        TaxTotal DECIMAL(19,4) NOT NULL DEFAULT 0,
        GrandTotal DECIMAL(19,4) NOT NULL,
        Status TINYINT NOT NULL DEFAULT 1,           -- 1 Pending,2 Paid,3 Completed,4 Refunded,5 Disputed,6 Cancelled
        InvoiceNo NVARCHAR(40) NULL,
        PlacedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CompletedAtUtc DATETIME2(3) NULL,
        RowVersion ROWVERSION,
        CONSTRAINT UQ_Orders_PublicId UNIQUE (PublicId),
        INDEX IX_Orders_Buyer (BuyerUserId),
        INDEX IX_Orders_Status (Status)
    );  -- partition by PlacedAtUtc (monthly range) at scale
END
GO

IF OBJECT_ID('orders.OrderLines', 'U') IS NULL
BEGIN
    CREATE TABLE orders.OrderLines (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        OrderId BIGINT NOT NULL REFERENCES orders.Orders(Id),
        ProductId BIGINT NOT NULL,
        VariantId BIGINT NOT NULL,
        VersionId BIGINT NOT NULL,
        StoreId BIGINT NOT NULL,
        UnitAmount DECIMAL(19,4) NOT NULL,
        Currency CHAR(3) NOT NULL,
        CommissionPct DECIMAL(5,2) NOT NULL,
        LicenseType TINYINT NOT NULL,
        INDEX IX_Lines_Order (OrderId),
        INDEX IX_Lines_Store (StoreId)
    );
END
GO

IF OBJECT_ID('orders.Licenses', 'U') IS NULL
BEGIN
    CREATE TABLE orders.Licenses (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        PublicId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        OrderLineId BIGINT NOT NULL REFERENCES orders.OrderLines(Id),
        BuyerUserId BIGINT NOT NULL,
        VariantId BIGINT NOT NULL,
        VersionId BIGINT NOT NULL,
        DownloadLimit INT NOT NULL,
        DownloadsUsed INT NOT NULL DEFAULT 0,
        ExpiresAtUtc DATETIME2(3) NULL,
        IssuedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_Licenses_PublicId UNIQUE (PublicId),
        INDEX IX_Licenses_Buyer (BuyerUserId)
    );
END
GO

-- FK from files.DownloadLogs to orders.Licenses (deferred; files deploys before orders)
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DownloadLogs_License')
    AND OBJECT_ID('files.DownloadLogs', 'U') IS NOT NULL
BEGIN
    ALTER TABLE files.DownloadLogs
        ADD CONSTRAINT FK_DownloadLogs_License FOREIGN KEY (LicenseId) REFERENCES orders.Licenses(Id);
END
GO

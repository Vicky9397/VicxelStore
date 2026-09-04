-- V001__init.sql
-- Baseline schema for all module schemas (idempotent; mirrors db/schema/*).
-- Order respects FKs: identity -> market -> catalog -> files -> orders -> pay -> ledger -> reviews -> notify -> admin -> cms -> analytics -> dbo(outbox).

-- Module: identity (spec 05 section 5.3)
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'identity')
    EXEC('CREATE SCHEMA [identity]');
GO

IF OBJECT_ID('[identity].Users', 'U') IS NULL
BEGIN
    CREATE TABLE [identity].Users (
        Id              BIGINT IDENTITY(1,1) PRIMARY KEY,
        PublicId        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        Email           NVARCHAR(256) NOT NULL,
        EmailNormalized NVARCHAR(256) NOT NULL,
        EmailVerified   BIT NOT NULL DEFAULT 0,
        PasswordHash    NVARCHAR(500) NULL,
        DisplayName     NVARCHAR(120) NOT NULL,
        Status          TINYINT NOT NULL DEFAULT 1,  -- 1 Active,2 Locked,3 Suspended,4 Deleted
        MfaEnabled      BIT NOT NULL DEFAULT 0,
        MfaSecret       VARBINARY(256) NULL,
        FailedLogins    INT NOT NULL DEFAULT 0,
        LockoutEndUtc   DATETIME2(3) NULL,
        CreatedAtUtc    DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc    DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        IsDeleted       BIT NOT NULL DEFAULT 0,
        RowVersion      ROWVERSION,
        CONSTRAINT UQ_Users_PublicId UNIQUE (PublicId),
        CONSTRAINT UQ_Users_EmailNorm UNIQUE (EmailNormalized)
    );
END
GO

IF OBJECT_ID('[identity].Roles', 'U') IS NULL
BEGIN
    CREATE TABLE [identity].Roles (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(64) NOT NULL UNIQUE,           -- Buyer, Seller, Admin, Moderator, Support
        IsStaffRole BIT NOT NULL DEFAULT 0
    );
END
GO

IF OBJECT_ID('[identity].Permissions', 'U') IS NULL
BEGIN
    CREATE TABLE [identity].Permissions (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Code NVARCHAR(128) NOT NULL UNIQUE
    );
END
GO

IF OBJECT_ID('[identity].RolePermissions', 'U') IS NULL
BEGIN
    CREATE TABLE [identity].RolePermissions (
        RoleId INT NOT NULL REFERENCES [identity].Roles(Id),
        PermissionId INT NOT NULL REFERENCES [identity].Permissions(Id),
        PRIMARY KEY (RoleId, PermissionId)
    );
END
GO

IF OBJECT_ID('[identity].UserRoles', 'U') IS NULL
BEGIN
    CREATE TABLE [identity].UserRoles (
        UserId BIGINT NOT NULL REFERENCES [identity].Users(Id),
        RoleId INT NOT NULL REFERENCES [identity].Roles(Id),
        PRIMARY KEY (UserId, RoleId)
    );
END
GO

IF OBJECT_ID('[identity].RefreshTokens', 'U') IS NULL
BEGIN
    CREATE TABLE [identity].RefreshTokens (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserId BIGINT NOT NULL REFERENCES [identity].Users(Id),
        FamilyId UNIQUEIDENTIFIER NOT NULL,
        TokenHash CHAR(64) NOT NULL,                 -- SHA-256, never store raw
        ExpiresAtUtc DATETIME2(3) NOT NULL,
        RevokedAtUtc DATETIME2(3) NULL,
        ReplacedByHash CHAR(64) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        INDEX IX_RT_User (UserId),
        INDEX IX_RT_Hash (TokenHash)
    );
END
GO

IF OBJECT_ID('[identity].UserDevices', 'U') IS NULL
BEGIN
    CREATE TABLE [identity].UserDevices (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        PublicId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        UserId BIGINT NOT NULL REFERENCES [identity].Users(Id),
        DeviceName NVARCHAR(200) NOT NULL,
        FingerprintHash CHAR(64) NOT NULL,
        LastSeenAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_UserDevices_PublicId UNIQUE (PublicId),
        INDEX IX_UD_User (UserId)
    );
END
GO

-- Single-use verification/reset tokens. Not in spec DDL; required by AUTH module
-- rules (verification link 24h TTL, reset token single-use 30-min TTL).
-- Recorded assumption: purpose-coded token table, hash-only storage.
IF OBJECT_ID('[identity].UserTokens', 'U') IS NULL
BEGIN
    CREATE TABLE [identity].UserTokens (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserId BIGINT NOT NULL REFERENCES [identity].Users(Id),
        Purpose TINYINT NOT NULL,                    -- 1 EmailVerify, 2 PasswordReset
        TokenHash CHAR(64) NOT NULL,
        ExpiresAtUtc DATETIME2(3) NOT NULL,
        ConsumedAtUtc DATETIME2(3) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        INDEX IX_UT_User_Purpose (UserId, Purpose),
        INDEX IX_UT_Hash (TokenHash)
    );
END
GO

-- Module: market (spec 05 section 5.3)
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'market')
    EXEC('CREATE SCHEMA market');
GO

IF OBJECT_ID('market.Stores', 'U') IS NULL
BEGIN
    CREATE TABLE market.Stores (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        PublicId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        OwnerUserId BIGINT NOT NULL REFERENCES [identity].Users(Id),
        Slug NVARCHAR(80) NOT NULL,
        Name NVARCHAR(120) NOT NULL,
        About NVARCHAR(MAX) NULL,
        LogoUrl NVARCHAR(500) NULL,
        BannerUrl NVARCHAR(500) NULL,
        ThemeJson NVARCHAR(MAX) NULL,
        Status TINYINT NOT NULL DEFAULT 1,           -- 1 Active,2 Pending,3 Suspended
        CreatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion ROWVERSION,
        CONSTRAINT UQ_Stores_PublicId UNIQUE (PublicId),
        CONSTRAINT UQ_Stores_Slug UNIQUE (Slug),
        CONSTRAINT UQ_Stores_Owner UNIQUE (OwnerUserId)
    );
END
GO

IF OBJECT_ID('market.SellerProfiles', 'U') IS NULL
BEGIN
    CREATE TABLE market.SellerProfiles (
        StoreId BIGINT PRIMARY KEY REFERENCES market.Stores(Id),
        KycStatus TINYINT NOT NULL DEFAULT 0,        -- 0 None,1 Submitted,2 Verified,3 Rejected
        LegalName NVARCHAR(200) NULL,
        TaxIdType NVARCHAR(20) NULL,                 -- GSTIN / VAT
        TaxId NVARCHAR(50) NULL,
        BankVerified BIT NOT NULL DEFAULT 0,
        BankRef NVARCHAR(200) NULL,                  -- provider token, not raw account
        CommissionOverridePct DECIMAL(5,2) NULL,
        PayoutMinorityFrozen BIT NOT NULL DEFAULT 0,
        UpdatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

IF OBJECT_ID('market.Follows', 'U') IS NULL
BEGIN
    CREATE TABLE market.Follows (
        UserId BIGINT NOT NULL REFERENCES [identity].Users(Id),
        StoreId BIGINT NOT NULL REFERENCES market.Stores(Id),
        CreatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        PRIMARY KEY (UserId, StoreId),
        INDEX IX_Follows_Store (StoreId)
    );
END
GO

-- Module: catalog (spec 05 section 5.3)
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'catalog')
    EXEC('CREATE SCHEMA catalog');
GO

IF OBJECT_ID('catalog.Categories', 'U') IS NULL
BEGIN
    CREATE TABLE catalog.Categories (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ParentId INT NULL REFERENCES catalog.Categories(Id),
        Slug NVARCHAR(80) NOT NULL UNIQUE,
        Name NVARCHAR(120) NOT NULL,
        CommissionPct DECIMAL(5,2) NOT NULL DEFAULT 15.00
    );
END
GO

IF OBJECT_ID('catalog.Products', 'U') IS NULL
BEGIN
    CREATE TABLE catalog.Products (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        PublicId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        StoreId BIGINT NOT NULL REFERENCES market.Stores(Id),
        CategoryId INT NOT NULL REFERENCES catalog.Categories(Id),
        Title NVARCHAR(200) NOT NULL,
        Slug NVARCHAR(220) NOT NULL,
        Description NVARCHAR(MAX) NULL,
        Status TINYINT NOT NULL DEFAULT 1,           -- 1 Draft,2 Submitted,3 InReview,4 Approved,5 Published,6 Rejected,7 Archived
        SeoTitle NVARCHAR(200) NULL,
        SeoDescription NVARCHAR(400) NULL,
        RatingAvg DECIMAL(3,2) NOT NULL DEFAULT 0,
        RatingCount INT NOT NULL DEFAULT 0,
        PublishedAtUtc DATETIME2(3) NULL,
        ScheduledPublishUtc DATETIME2(3) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        IsDeleted BIT NOT NULL DEFAULT 0,
        RowVersion ROWVERSION,
        CONSTRAINT UQ_Products_PublicId UNIQUE (PublicId),
        INDEX IX_Products_Store (StoreId) WHERE IsDeleted = 0,
        INDEX IX_Products_Status_Cat (Status, CategoryId)
    );
END
GO

IF OBJECT_ID('catalog.ProductVariants', 'U') IS NULL
BEGIN
    CREATE TABLE catalog.ProductVariants (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        PublicId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        ProductId BIGINT NOT NULL REFERENCES catalog.Products(Id),
        Name NVARCHAR(120) NOT NULL,
        PriceAmount DECIMAL(19,4) NOT NULL,
        PriceCurrency CHAR(3) NOT NULL,
        LicenseType TINYINT NOT NULL,                -- 1 Personal,2 Commercial,3 Extended
        DownloadLimit INT NOT NULL DEFAULT 5,
        IsActive BIT NOT NULL DEFAULT 1,
        CONSTRAINT UQ_Variants_PublicId UNIQUE (PublicId),
        INDEX IX_Variants_Product (ProductId)
    );
END
GO

IF OBJECT_ID('catalog.ProductVersions', 'U') IS NULL
BEGIN
    CREATE TABLE catalog.ProductVersions (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        ProductId BIGINT NOT NULL REFERENCES catalog.Products(Id),
        VersionNumber NVARCHAR(20) NOT NULL,
        Changelog NVARCHAR(MAX) NULL,
        ReleasedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        INDEX IX_Versions_Product (ProductId)
    );
END
GO

IF OBJECT_ID('catalog.Tags', 'U') IS NULL
BEGIN
    CREATE TABLE catalog.Tags (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(60) NOT NULL UNIQUE
    );
END
GO

IF OBJECT_ID('catalog.ProductTags', 'U') IS NULL
BEGIN
    CREATE TABLE catalog.ProductTags (
        ProductId BIGINT NOT NULL REFERENCES catalog.Products(Id),
        TagId INT NOT NULL REFERENCES catalog.Tags(Id),
        PRIMARY KEY (ProductId, TagId)
    );
END
GO

-- Module: files (spec 05 section 5.3)
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'files')
    EXEC('CREATE SCHEMA files');
GO

IF OBJECT_ID('files.ProductFiles', 'U') IS NULL
BEGIN
    CREATE TABLE files.ProductFiles (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        PublicId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        VariantId BIGINT NOT NULL REFERENCES catalog.ProductVariants(Id),
        VersionId BIGINT NOT NULL REFERENCES catalog.ProductVersions(Id),
        StorageKey NVARCHAR(500) NOT NULL,           -- object storage key (clean bucket)
        FileName NVARCHAR(260) NOT NULL,
        SizeBytes BIGINT NOT NULL,
        Checksum CHAR(64) NOT NULL,                  -- SHA-256
        ScanStatus TINYINT NOT NULL DEFAULT 0,       -- 0 Pending,1 Clean,2 Infected
        CreatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_ProductFiles_PublicId UNIQUE (PublicId),
        INDEX IX_Files_Variant (VariantId)
    );
END
GO

IF OBJECT_ID('files.DownloadLogs', 'U') IS NULL
BEGIN
    CREATE TABLE files.DownloadLogs (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        LicenseId BIGINT NOT NULL,                   -- FK orders.Licenses (added in orders module)
        FileId BIGINT NOT NULL REFERENCES files.ProductFiles(Id),
        UserId BIGINT NOT NULL,
        IpHash CHAR(64) NULL,
        StartedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        Completed BIT NOT NULL DEFAULT 0
    );  -- candidate for monthly partitioning
END
GO

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

-- Module: reviews (spec 05 section 5.3)
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'reviews')
    EXEC('CREATE SCHEMA reviews');
GO

IF OBJECT_ID('reviews.Reviews', 'U') IS NULL
BEGIN
    CREATE TABLE reviews.Reviews (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        PublicId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        ProductId BIGINT NOT NULL REFERENCES catalog.Products(Id),
        BuyerUserId BIGINT NOT NULL REFERENCES [identity].Users(Id),
        OrderLineId BIGINT NOT NULL REFERENCES orders.OrderLines(Id),
        Rating TINYINT NOT NULL CHECK (Rating BETWEEN 1 AND 5),
        Body NVARCHAR(4000) NULL,
        Status TINYINT NOT NULL DEFAULT 1,           -- 1 Visible,2 Hidden,3 Removed
        SellerReply NVARCHAR(2000) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_Reviews_PublicId UNIQUE (PublicId),
        CONSTRAINT UQ_Review_BuyerProduct UNIQUE (ProductId, BuyerUserId),
        INDEX IX_Reviews_Product (ProductId, Status)
    );
END
GO

-- Module: notify (spec 05 section 5.3)
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'notify')
    EXEC('CREATE SCHEMA notify');
GO

IF OBJECT_ID('notify.Notifications', 'U') IS NULL
BEGIN
    CREATE TABLE notify.Notifications (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserId BIGINT NOT NULL REFERENCES [identity].Users(Id),
        Type NVARCHAR(60) NOT NULL,
        Channel TINYINT NOT NULL,                    -- 1 Email,2 InApp,3 Push
        Title NVARCHAR(200) NOT NULL,
        Body NVARCHAR(MAX) NULL,
        ReadAtUtc DATETIME2(3) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        INDEX IX_Notif_User_Unread (UserId, ReadAtUtc)
    );
END
GO

-- Module: admin (spec 05 section 5.3). admin.AuditLogs is APPEND ONLY.
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'admin')
    EXEC('CREATE SCHEMA admin');
GO

IF OBJECT_ID('admin.AuditLogs', 'U') IS NULL
BEGIN
    CREATE TABLE admin.AuditLogs (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        ActorUserId BIGINT NULL,
        Action NVARCHAR(120) NOT NULL,
        EntityType NVARCHAR(80) NOT NULL,
        EntityId NVARCHAR(80) NOT NULL,
        BeforeJson NVARCHAR(MAX) NULL,
        AfterJson NVARCHAR(MAX) NULL,
        Reason NVARCHAR(500) NULL,
        IpHash CHAR(64) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        INDEX IX_Audit_Entity (EntityType, EntityId),
        INDEX IX_Audit_Time (CreatedAtUtc)
    );  -- append-only, monthly partition candidate
END
GO

IF OBJECT_ID('admin.Settings', 'U') IS NULL
BEGIN
    CREATE TABLE admin.Settings (
        [Key] NVARCHAR(120) PRIMARY KEY,
        ValueJson NVARCHAR(MAX) NOT NULL,
        UpdatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedBy BIGINT NULL
    );
END
GO

-- Module: cms (spec 05 section 5.3)
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'cms')
    EXEC('CREATE SCHEMA cms');
GO

IF OBJECT_ID('cms.Pages', 'U') IS NULL
BEGIN
    CREATE TABLE cms.Pages (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        Slug NVARCHAR(160) NOT NULL UNIQUE,
        Locale CHAR(5) NOT NULL DEFAULT 'en',
        Title NVARCHAR(200) NOT NULL,
        BodyHtml NVARCHAR(MAX) NULL,
        Status TINYINT NOT NULL DEFAULT 1,           -- 1 Draft,2 Published
        UpdatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

-- Module: analytics (spec 05 section 5.3)
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'analytics')
    EXEC('CREATE SCHEMA analytics');
GO

IF OBJECT_ID('analytics.Events', 'U') IS NULL
BEGIN
    CREATE TABLE analytics.Events (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        EventType NVARCHAR(40) NOT NULL,
        ProductId BIGINT NULL,
        UserId BIGINT NULL,
        StoreId BIGINT NULL,
        MetaJson NVARCHAR(MAX) NULL,
        OccurredAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );  -- write-heavy, monthly partition; roll up into analytics.Daily* aggregates
END
GO

-- Cross-cutting: transactional outbox (spec 04 section 4.7 event flow).
-- Owned by BuildingBlocks infrastructure; lives in dbo because it spans modules.
IF OBJECT_ID('dbo.OutboxMessages', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OutboxMessages (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        EventId UNIQUEIDENTIFIER NOT NULL,
        Type NVARCHAR(200) NOT NULL,
        PayloadJson NVARCHAR(MAX) NOT NULL,
        OccurredAtUtc DATETIME2(3) NOT NULL,
        ProcessedAtUtc DATETIME2(3) NULL,
        Error NVARCHAR(MAX) NULL,
        CONSTRAINT UQ_Outbox_EventId UNIQUE (EventId),
        INDEX IX_Outbox_Unprocessed (ProcessedAtUtc) WHERE ProcessedAtUtc IS NULL
    );
END
GO

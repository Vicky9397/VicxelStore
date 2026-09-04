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

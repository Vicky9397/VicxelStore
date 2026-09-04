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

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

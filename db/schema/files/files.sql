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

-- Chunked resumable upload session (spec 02 section 2.4 File Management, 06 section 6.5 /files).
-- Parts land in the quarantine bucket and are assembled on complete; the file
-- becomes downloadable only after the scan reports Clean.
IF OBJECT_ID('files.FileUploads', 'U') IS NULL
BEGIN
    CREATE TABLE files.FileUploads (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        PublicId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        OwnerUserId BIGINT NOT NULL REFERENCES [identity].Users(Id),
        VariantId BIGINT NOT NULL REFERENCES catalog.ProductVariants(Id),
        VersionId BIGINT NOT NULL REFERENCES catalog.ProductVersions(Id),
        FileName NVARCHAR(260) NOT NULL,
        DeclaredSizeBytes BIGINT NOT NULL,
        DeclaredChecksum CHAR(64) NOT NULL,          -- SHA-256 declared by the client
        PartSizeBytes INT NOT NULL,
        TotalParts INT NOT NULL,
        QuarantineKey NVARCHAR(500) NOT NULL,
        Status TINYINT NOT NULL DEFAULT 1,           -- 1 InProgress,2 Completed,3 Aborted
        CreatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CompletedAtUtc DATETIME2(3) NULL,
        RowVersion ROWVERSION,
        CONSTRAINT UQ_FileUploads_PublicId UNIQUE (PublicId),
        INDEX IX_FileUploads_Owner (OwnerUserId),
        INDEX IX_FileUploads_Variant (VariantId)
    );
END
GO

IF OBJECT_ID('files.FileUploadParts', 'U') IS NULL
BEGIN
    CREATE TABLE files.FileUploadParts (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        UploadId BIGINT NOT NULL REFERENCES files.FileUploads(Id),
        PartNumber INT NOT NULL,
        SizeBytes INT NOT NULL,
        Checksum CHAR(64) NOT NULL,
        ReceivedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_UploadParts_Number UNIQUE (UploadId, PartNumber)
    );
END
GO

-- V002__catalog_files_workflow.sql
-- Additive migration for milestone M2: chunked upload sessions, the Catalog-owned
-- variant file readiness projection, and moderation decision history.
-- Idempotent; creates nothing that V001 already created and alters no existing column.

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

-- Read projection owned by Catalog, maintained from the Files module's
-- FileScanned integration event. It exists so the publish invariant ("at least
-- one variant with at least one Clean file", spec 04 section 4.7) can be checked
-- without Catalog reaching into files.* -- Files is downstream of Catalog in the
-- module dependency graph, so the arrow may not be reversed.
IF OBJECT_ID('catalog.VariantFileReadiness', 'U') IS NULL
BEGIN
    CREATE TABLE catalog.VariantFileReadiness (
        VariantId BIGINT NOT NULL PRIMARY KEY REFERENCES catalog.ProductVariants(Id),
        CleanFileCount INT NOT NULL DEFAULT 0,
        UpdatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

-- Moderation decision history. Rejected products carry the moderator's reason
-- (spec 02 section 2.4 Product Management).
IF OBJECT_ID('catalog.ProductModerations', 'U') IS NULL
BEGIN
    CREATE TABLE catalog.ProductModerations (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        ProductId BIGINT NOT NULL REFERENCES catalog.Products(Id),
        ModeratorUserId BIGINT NOT NULL REFERENCES [identity].Users(Id),
        Decision TINYINT NOT NULL,                   -- 1 Approved,2 Rejected
        Reason NVARCHAR(1000) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        INDEX IX_ProductModerations_Product (ProductId, CreatedAtUtc)
    );
END
GO

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

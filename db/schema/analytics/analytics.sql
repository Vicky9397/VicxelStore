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

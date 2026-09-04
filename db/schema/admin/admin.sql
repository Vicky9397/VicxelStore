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

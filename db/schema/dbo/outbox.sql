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

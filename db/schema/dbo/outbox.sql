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

-- Delivery bookkeeping for the dispatcher. A message that exhausts its attempts
-- is parked (left unprocessed with its error visible) rather than dropped: a
-- money event that never lands is a reconciliation break, not a discard.
IF COL_LENGTH('dbo.OutboxMessages', 'Attempts') IS NULL
BEGIN
    ALTER TABLE dbo.OutboxMessages ADD Attempts INT NOT NULL DEFAULT 0;
END
GO

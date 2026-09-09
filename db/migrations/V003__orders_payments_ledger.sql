-- V003__orders_payments_ledger.sql
-- Milestone M3 additions. Idempotent; alters no existing column and adds no
-- write path against the append-only ledger or audit tables.

-- Outbox delivery bookkeeping (see db/schema/dbo/outbox.sql).
IF COL_LENGTH('dbo.OutboxMessages', 'Attempts') IS NULL
BEGIN
    ALTER TABLE dbo.OutboxMessages ADD Attempts INT NOT NULL DEFAULT 0;
END
GO

-- Invoice numbering. A SEQUENCE gives gap-tolerant, contiguous-ish numbers
-- without the contention of a counter row (spec 05 section 5.7).
IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = 'InvoiceNumbers' AND SCHEMA_NAME(schema_id) = 'orders')
BEGIN
    CREATE SEQUENCE orders.InvoiceNumbers AS BIGINT START WITH 1 INCREMENT BY 1;
END
GO

-- Orders carries the buyer's billing country so the tax applied to an order can
-- be audited against the rule that produced it.
IF COL_LENGTH('orders.Orders', 'BillingCountry') IS NULL
BEGIN
    ALTER TABLE orders.Orders ADD BillingCountry CHAR(2) NULL;
END
GO

-- The hold clock starts at order completion (spec 03 section 3.4). Storing it on
-- the order keeps the hold-expiry job from re-deriving it per line.
IF COL_LENGTH('orders.Orders', 'HoldReleaseAtUtc') IS NULL
BEGIN
    ALTER TABLE orders.Orders ADD HoldReleaseAtUtc DATETIME2(3) NULL;
END
GO

-- The download quota the buyer actually paid for, carried alongside the line so
-- a later change to the variant cannot shrink an issued license. Kept in its own
-- additive table so the spec-defined OrderLines columns stay exactly as specified.
IF OBJECT_ID('orders.OrderLineTerms', 'U') IS NULL
BEGIN
    CREATE TABLE orders.OrderLineTerms (
        Id BIGINT NOT NULL PRIMARY KEY REFERENCES orders.OrderLines(Id),
        DownloadLimit INT NOT NULL DEFAULT 5
    );
END
GO

-- Idempotency records expire after 24 hours (spec 03 section 3.2); the index
-- supports the sweep that removes them.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Idempotency_Created' AND object_id = OBJECT_ID('pay.Idempotency'))
BEGIN
    CREATE INDEX IX_Idempotency_Created ON pay.Idempotency (CreatedAtUtc);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WebhookInbox_Unprocessed' AND object_id = OBJECT_ID('pay.WebhookInbox'))
BEGIN
    CREATE INDEX IX_WebhookInbox_Unprocessed ON pay.WebhookInbox (ProcessedAtUtc) WHERE ProcessedAtUtc IS NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DownloadLogs_License' AND object_id = OBJECT_ID('files.DownloadLogs'))
BEGIN
    CREATE INDEX IX_DownloadLogs_License ON files.DownloadLogs (LicenseId, StartedAtUtc);
END
GO

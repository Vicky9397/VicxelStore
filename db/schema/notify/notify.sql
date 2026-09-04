-- Module: notify (spec 05 section 5.3)
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'notify')
    EXEC('CREATE SCHEMA notify');
GO

IF OBJECT_ID('notify.Notifications', 'U') IS NULL
BEGIN
    CREATE TABLE notify.Notifications (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserId BIGINT NOT NULL REFERENCES [identity].Users(Id),
        Type NVARCHAR(60) NOT NULL,
        Channel TINYINT NOT NULL,                    -- 1 Email,2 InApp,3 Push
        Title NVARCHAR(200) NOT NULL,
        Body NVARCHAR(MAX) NULL,
        ReadAtUtc DATETIME2(3) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        INDEX IX_Notif_User_Unread (UserId, ReadAtUtc)
    );
END
GO

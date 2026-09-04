-- Module: identity (spec 05 section 5.3)
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'identity')
    EXEC('CREATE SCHEMA [identity]');
GO

IF OBJECT_ID('[identity].Users', 'U') IS NULL
BEGIN
    CREATE TABLE [identity].Users (
        Id              BIGINT IDENTITY(1,1) PRIMARY KEY,
        PublicId        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        Email           NVARCHAR(256) NOT NULL,
        EmailNormalized NVARCHAR(256) NOT NULL,
        EmailVerified   BIT NOT NULL DEFAULT 0,
        PasswordHash    NVARCHAR(500) NULL,
        DisplayName     NVARCHAR(120) NOT NULL,
        Status          TINYINT NOT NULL DEFAULT 1,  -- 1 Active,2 Locked,3 Suspended,4 Deleted
        MfaEnabled      BIT NOT NULL DEFAULT 0,
        MfaSecret       VARBINARY(256) NULL,
        FailedLogins    INT NOT NULL DEFAULT 0,
        LockoutEndUtc   DATETIME2(3) NULL,
        CreatedAtUtc    DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc    DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        IsDeleted       BIT NOT NULL DEFAULT 0,
        RowVersion      ROWVERSION,
        CONSTRAINT UQ_Users_PublicId UNIQUE (PublicId),
        CONSTRAINT UQ_Users_EmailNorm UNIQUE (EmailNormalized)
    );
END
GO

IF OBJECT_ID('[identity].Roles', 'U') IS NULL
BEGIN
    CREATE TABLE [identity].Roles (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(64) NOT NULL UNIQUE,           -- Buyer, Seller, Admin, Moderator, Support
        IsStaffRole BIT NOT NULL DEFAULT 0
    );
END
GO

IF OBJECT_ID('[identity].Permissions', 'U') IS NULL
BEGIN
    CREATE TABLE [identity].Permissions (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Code NVARCHAR(128) NOT NULL UNIQUE
    );
END
GO

IF OBJECT_ID('[identity].RolePermissions', 'U') IS NULL
BEGIN
    CREATE TABLE [identity].RolePermissions (
        RoleId INT NOT NULL REFERENCES [identity].Roles(Id),
        PermissionId INT NOT NULL REFERENCES [identity].Permissions(Id),
        PRIMARY KEY (RoleId, PermissionId)
    );
END
GO

IF OBJECT_ID('[identity].UserRoles', 'U') IS NULL
BEGIN
    CREATE TABLE [identity].UserRoles (
        UserId BIGINT NOT NULL REFERENCES [identity].Users(Id),
        RoleId INT NOT NULL REFERENCES [identity].Roles(Id),
        PRIMARY KEY (UserId, RoleId)
    );
END
GO

IF OBJECT_ID('[identity].RefreshTokens', 'U') IS NULL
BEGIN
    CREATE TABLE [identity].RefreshTokens (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserId BIGINT NOT NULL REFERENCES [identity].Users(Id),
        FamilyId UNIQUEIDENTIFIER NOT NULL,
        TokenHash CHAR(64) NOT NULL,                 -- SHA-256, never store raw
        ExpiresAtUtc DATETIME2(3) NOT NULL,
        RevokedAtUtc DATETIME2(3) NULL,
        ReplacedByHash CHAR(64) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        INDEX IX_RT_User (UserId),
        INDEX IX_RT_Hash (TokenHash)
    );
END
GO

IF OBJECT_ID('[identity].UserDevices', 'U') IS NULL
BEGIN
    CREATE TABLE [identity].UserDevices (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        PublicId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        UserId BIGINT NOT NULL REFERENCES [identity].Users(Id),
        DeviceName NVARCHAR(200) NOT NULL,
        FingerprintHash CHAR(64) NOT NULL,
        LastSeenAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_UserDevices_PublicId UNIQUE (PublicId),
        INDEX IX_UD_User (UserId)
    );
END
GO

-- Single-use verification/reset tokens. Not in spec DDL; required by AUTH module
-- rules (verification link 24h TTL, reset token single-use 30-min TTL).
-- Recorded assumption: purpose-coded token table, hash-only storage.
IF OBJECT_ID('[identity].UserTokens', 'U') IS NULL
BEGIN
    CREATE TABLE [identity].UserTokens (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserId BIGINT NOT NULL REFERENCES [identity].Users(Id),
        Purpose TINYINT NOT NULL,                    -- 1 EmailVerify, 2 PasswordReset
        TokenHash CHAR(64) NOT NULL,
        ExpiresAtUtc DATETIME2(3) NOT NULL,
        ConsumedAtUtc DATETIME2(3) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        INDEX IX_UT_User_Purpose (UserId, Purpose),
        INDEX IX_UT_Hash (TokenHash)
    );
END
GO

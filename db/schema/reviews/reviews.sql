-- Module: reviews (spec 05 section 5.3)
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'reviews')
    EXEC('CREATE SCHEMA reviews');
GO

IF OBJECT_ID('reviews.Reviews', 'U') IS NULL
BEGIN
    CREATE TABLE reviews.Reviews (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        PublicId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        ProductId BIGINT NOT NULL REFERENCES catalog.Products(Id),
        BuyerUserId BIGINT NOT NULL REFERENCES [identity].Users(Id),
        OrderLineId BIGINT NOT NULL REFERENCES orders.OrderLines(Id),
        Rating TINYINT NOT NULL CHECK (Rating BETWEEN 1 AND 5),
        Body NVARCHAR(4000) NULL,
        Status TINYINT NOT NULL DEFAULT 1,           -- 1 Visible,2 Hidden,3 Removed
        SellerReply NVARCHAR(2000) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_Reviews_PublicId UNIQUE (PublicId),
        CONSTRAINT UQ_Review_BuyerProduct UNIQUE (ProductId, BuyerUserId),
        INDEX IX_Reviews_Product (ProductId, Status)
    );
END
GO

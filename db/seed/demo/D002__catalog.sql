-- Demo seed: a published product with variants and a released version, so the
-- catalog and checkout milestones have something deterministic to work against.
SET NOCOUNT ON;

MERGE catalog.Products AS T
USING (
    SELECT
        St.Id AS StoreId,
        C.Id AS CategoryId,
        N'Sci-fi UI Kit' AS Title,
        N'sci-fi-ui-kit' AS Slug,
        N'A dark, high-contrast interface kit with 180 components and 12 screen templates.' AS Description
    FROM market.Stores AS St
    CROSS JOIN catalog.Categories AS C
    WHERE St.Slug = N'demo-studio' AND C.Slug = N'ui-kits'
) AS S
ON T.Slug = S.Slug
WHEN NOT MATCHED THEN
    INSERT (StoreId, CategoryId, Title, Slug, Description, Status, PublishedAtUtc)
    VALUES (S.StoreId, S.CategoryId, S.Title, S.Slug, S.Description, 5, SYSUTCDATETIME());
GO

MERGE catalog.ProductVariants AS T
USING (
    SELECT P.Id AS ProductId, V.Name, V.PriceAmount, V.PriceCurrency, V.LicenseType, V.DownloadLimit
    FROM catalog.Products AS P
    CROSS JOIN (VALUES
        (N'Personal',   900.0000,  'INR', CAST(1 AS TINYINT), 5),
        (N'Commercial', 2400.0000, 'INR', CAST(2 AS TINYINT), 10)
    ) AS V (Name, PriceAmount, PriceCurrency, LicenseType, DownloadLimit)
    WHERE P.Slug = N'sci-fi-ui-kit'
) AS S
ON T.ProductId = S.ProductId AND T.Name = S.Name
WHEN NOT MATCHED THEN
    INSERT (ProductId, Name, PriceAmount, PriceCurrency, LicenseType, DownloadLimit)
    VALUES (S.ProductId, S.Name, S.PriceAmount, S.PriceCurrency, S.LicenseType, S.DownloadLimit);
GO

MERGE catalog.ProductVersions AS T
USING (
    SELECT P.Id AS ProductId, N'1.0.0' AS VersionNumber, N'First public release.' AS Changelog
    FROM catalog.Products AS P
    WHERE P.Slug = N'sci-fi-ui-kit'
) AS S
ON T.ProductId = S.ProductId AND T.VersionNumber = S.VersionNumber
WHEN NOT MATCHED THEN
    INSERT (ProductId, VersionNumber, Changelog)
    VALUES (S.ProductId, S.VersionNumber, S.Changelog);
GO

MERGE catalog.Tags AS T
USING (VALUES (N'figma'), (N'dark'), (N'ui-kit')) AS S (Name)
ON T.Name = S.Name
WHEN NOT MATCHED THEN INSERT (Name) VALUES (S.Name);
GO

WITH ProductTagLinks AS (
    SELECT P.Id AS ProductId, Tg.Id AS TagId
    FROM catalog.Products AS P
    CROSS JOIN catalog.Tags AS Tg
    WHERE P.Slug = N'sci-fi-ui-kit' AND Tg.Name IN (N'figma', N'dark', N'ui-kit')
)
MERGE catalog.ProductTags AS T
USING ProductTagLinks AS S
ON T.ProductId = S.ProductId AND T.TagId = S.TagId
WHEN NOT MATCHED THEN INSERT (ProductId, TagId) VALUES (S.ProductId, S.TagId);
GO

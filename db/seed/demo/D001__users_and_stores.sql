-- Demo seed: deterministic dataset for dev, staging and E2E only. Never applied
-- to production (deploy.sh applies it solely under --demo).
--
-- Every demo account uses the password DemoPassword123!. The hash below is
-- produced by the API's Pbkdf2PasswordHasher with a fixed salt so the dataset
-- reproduces byte-for-byte; Dpm.UnitTests pins it to that hasher.
SET NOCOUNT ON;

DECLARE @DemoPasswordHash NVARCHAR(500) =
    N'pbkdf2-sha256.210000.NH3eeLY8GdLiUMgLNZwv5w==.WEY6pYGm3du70l/QUgTS5br8Ijb9eGndf0xxtO26Ogw=';

MERGE [identity].Users AS T
USING (VALUES
    (N'buyer@demo.vicxelstore.local',     N'BUYER@DEMO.VICXELSTORE.LOCAL',     N'Demo Buyer',     1),
    (N'seller@demo.vicxelstore.local',    N'SELLER@DEMO.VICXELSTORE.LOCAL',    N'Demo Seller',    1),
    (N'admin@demo.vicxelstore.local',     N'ADMIN@DEMO.VICXELSTORE.LOCAL',     N'Demo Admin',     1),
    (N'moderator@demo.vicxelstore.local', N'MODERATOR@DEMO.VICXELSTORE.LOCAL', N'Demo Moderator', 1),
    (N'unverified@demo.vicxelstore.local', N'UNVERIFIED@DEMO.VICXELSTORE.LOCAL', N'Demo Unverified', 0)
) AS S (Email, EmailNormalized, DisplayName, EmailVerified)
ON T.EmailNormalized = S.EmailNormalized
WHEN NOT MATCHED THEN
    INSERT (Email, EmailNormalized, DisplayName, EmailVerified, PasswordHash)
    VALUES (S.Email, S.EmailNormalized, S.DisplayName, S.EmailVerified, @DemoPasswordHash);
GO

-- Role assignments. Buyer is additive: the seller and staff accounts buy too.
WITH Assignments AS (
    SELECT U.Id AS UserId, R.Id AS RoleId
    FROM (VALUES
        (N'BUYER@DEMO.VICXELSTORE.LOCAL',      N'Buyer'),
        (N'SELLER@DEMO.VICXELSTORE.LOCAL',     N'Buyer'),
        (N'SELLER@DEMO.VICXELSTORE.LOCAL',     N'Seller'),
        (N'ADMIN@DEMO.VICXELSTORE.LOCAL',      N'Buyer'),
        (N'ADMIN@DEMO.VICXELSTORE.LOCAL',      N'Admin'),
        (N'MODERATOR@DEMO.VICXELSTORE.LOCAL',  N'Buyer'),
        (N'MODERATOR@DEMO.VICXELSTORE.LOCAL',  N'Moderator'),
        (N'UNVERIFIED@DEMO.VICXELSTORE.LOCAL', N'Buyer')
    ) AS A (EmailNormalized, RoleName)
    INNER JOIN [identity].Users AS U ON U.EmailNormalized = A.EmailNormalized
    INNER JOIN [identity].Roles AS R ON R.Name = A.RoleName
)
MERGE [identity].UserRoles AS T
USING Assignments AS S
ON T.UserId = S.UserId AND T.RoleId = S.RoleId
WHEN NOT MATCHED THEN INSERT (UserId, RoleId) VALUES (S.UserId, S.RoleId);
GO

MERGE market.Stores AS T
USING (
    SELECT U.Id AS OwnerUserId, N'demo-studio' AS Slug, N'Demo Studio' AS Name,
           N'A demo storefront used by local development and end-to-end tests.' AS About
    FROM [identity].Users AS U
    WHERE U.EmailNormalized = N'SELLER@DEMO.VICXELSTORE.LOCAL'
) AS S
ON T.Slug = S.Slug
WHEN NOT MATCHED THEN
    INSERT (OwnerUserId, Slug, Name, About, Status)
    VALUES (S.OwnerUserId, S.Slug, S.Name, S.About, 1);
GO

MERGE market.SellerProfiles AS T
USING (
    SELECT St.Id AS StoreId
    FROM market.Stores AS St
    WHERE St.Slug = N'demo-studio'
) AS S
ON T.StoreId = S.StoreId
WHEN NOT MATCHED THEN
    INSERT (StoreId, KycStatus, LegalName, TaxIdType, TaxId, BankVerified)
    VALUES (S.StoreId, 2, N'Demo Studio Private Limited', N'GSTIN', N'29ABCDE1234F1Z5', 1);
GO

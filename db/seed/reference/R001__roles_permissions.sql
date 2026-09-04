-- Reference seed: roles and permissions (spec 02 section 2.1 personas; idempotent).
SET NOCOUNT ON;

MERGE [identity].Roles AS T
USING (VALUES
    ('Buyer', 0),
    ('Seller', 0),
    ('Admin', 1),
    ('Moderator', 1),
    ('Support', 1)
) AS S (Name, IsStaffRole)
ON T.Name = S.Name
WHEN NOT MATCHED THEN INSERT (Name, IsStaffRole) VALUES (S.Name, S.IsStaffRole);
GO

MERGE [identity].Permissions AS T
USING (VALUES
    ('profile.edit'),
    ('cart.checkout'),
    ('order.view.own'),
    ('order.refund.request'),
    ('license.download'),
    ('review.create'),
    ('review.reply'),
    ('store.create'),
    ('store.edit.own'),
    ('product.create'),
    ('product.edit.own'),
    ('product.submit'),
    ('product.publish.own'),
    ('product.approve'),
    ('product.reject'),
    ('file.upload'),
    ('wallet.view.own'),
    ('payout.request'),
    ('payout.approve'),
    ('user.manage'),
    ('seller.moderate'),
    ('review.moderate'),
    ('settings.manage'),
    ('audit.view'),
    ('reports.view'),
    ('cms.manage'),
    ('ticket.respond'),
    ('order.refund.issue')
) AS S (Code)
ON T.Code = S.Code
WHEN NOT MATCHED THEN INSERT (Code) VALUES (S.Code);
GO

-- Role -> permission grants (default-deny elsewhere; staff roles never self-grantable).
WITH Grants AS (
    SELECT R.Id AS RoleId, P.Id AS PermissionId
    FROM (VALUES
        ('Buyer',     'profile.edit'),
        ('Buyer',     'cart.checkout'),
        ('Buyer',     'order.view.own'),
        ('Buyer',     'order.refund.request'),
        ('Buyer',     'license.download'),
        ('Buyer',     'review.create'),
        ('Buyer',     'store.create'),
        ('Seller',    'store.edit.own'),
        ('Seller',    'product.create'),
        ('Seller',    'product.edit.own'),
        ('Seller',    'product.submit'),
        ('Seller',    'product.publish.own'),
        ('Seller',    'file.upload'),
        ('Seller',    'wallet.view.own'),
        ('Seller',    'payout.request'),
        ('Seller',    'review.reply'),
        ('Moderator', 'product.approve'),
        ('Moderator', 'product.reject'),
        ('Moderator', 'review.moderate'),
        ('Moderator', 'seller.moderate'),
        ('Support',   'order.view.own'),
        ('Support',   'order.refund.issue'),
        ('Support',   'ticket.respond'),
        ('Admin',     'user.manage'),
        ('Admin',     'seller.moderate'),
        ('Admin',     'product.approve'),
        ('Admin',     'product.reject'),
        ('Admin',     'review.moderate'),
        ('Admin',     'payout.approve'),
        ('Admin',     'settings.manage'),
        ('Admin',     'audit.view'),
        ('Admin',     'reports.view'),
        ('Admin',     'cms.manage')
    ) AS G (RoleName, PermissionCode)
    INNER JOIN [identity].Roles AS R ON R.Name = G.RoleName
    INNER JOIN [identity].Permissions AS P ON P.Code = G.PermissionCode
)
MERGE [identity].RolePermissions AS T
USING Grants AS S
ON T.RoleId = S.RoleId AND T.PermissionId = S.PermissionId
WHEN NOT MATCHED THEN INSERT (RoleId, PermissionId) VALUES (S.RoleId, S.PermissionId);
GO

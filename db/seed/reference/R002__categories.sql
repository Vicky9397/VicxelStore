-- Reference seed: category tree (idempotent). Default commission 15.00 per spec A4 / 01.3.
SET NOCOUNT ON;

MERGE catalog.Categories AS T
USING (VALUES
    ('software',       'Software',            15.00),
    ('templates',      'Templates',           15.00),
    ('ui-kits',        'UI Kits',             15.00),
    ('3d-models',      '3D Models',           15.00),
    ('plugins',        'Plugins',             15.00),
    ('ebooks',         'Ebooks',              15.00),
    ('audio',          'Audio',               15.00),
    ('design-assets',  'Design Assets',       15.00),
    ('fonts',          'Fonts',               15.00),
    ('video',          'Video',               15.00)
) AS S (Slug, Name, CommissionPct)
ON T.Slug = S.Slug
WHEN NOT MATCHED THEN INSERT (Slug, Name, CommissionPct) VALUES (S.Slug, S.Name, S.CommissionPct);
GO

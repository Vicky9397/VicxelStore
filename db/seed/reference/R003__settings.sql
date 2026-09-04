-- Reference seed: platform settings (idempotent). Values from the Assumption
-- Register (spec README section 2) and module rules; financial policy values
-- must not change without human approval (spec 11B section 13.5).
SET NOCOUNT ON;

MERGE admin.Settings AS T
USING (VALUES
    ('commission.defaultPct',        N'{"value": 15.00}'),
    ('payout.minimumAmount',         N'{"amount": 1000.00, "currency": "INR"}'),
    ('payout.holdDays',              N'{"value": 7}'),
    ('refund.windowDays',            N'{"value": 14}'),
    ('auth.accessTokenMinutes',      N'{"value": 15}'),
    ('auth.refreshTokenDays',        N'{"value": 14}'),
    ('auth.lockoutThreshold',        N'{"failedLogins": 5, "windowMinutes": 15}'),
    ('auth.emailVerifyTtlHours',     N'{"value": 24}'),
    ('auth.passwordResetTtlMinutes', N'{"value": 30}'),
    ('files.maxSizeGb',              N'{"value": 20}'),
    ('downloads.signedUrlTtlSeconds',N'{"value": 900}'),
    ('downloads.defaultLimit',       N'{"value": 5}'),
    ('tax.rules',                    N'{"IN": {"kind": "GST", "ratePct": 18.0}, "EU": {"kind": "VAT", "ratePct": 20.0}, "GB": {"kind": "VAT", "ratePct": 20.0}}'),
    ('currency.primary',             N'{"value": "INR"}'),
    ('currency.supported',           N'{"values": ["INR", "USD", "EUR", "GBP"]}')
) AS S ([Key], ValueJson)
ON T.[Key] = S.[Key]
WHEN NOT MATCHED THEN INSERT ([Key], ValueJson) VALUES (S.[Key], S.ValueJson);
GO

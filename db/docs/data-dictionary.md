# Data dictionary (through V002)

Full column definitions live in `db/schema/<module>/<module>.sql`; this is the
table-level index. Conventions: PK `Id BIGINT IDENTITY` (internal), external id
`PublicId UNIQUEIDENTIFIER`, money `DECIMAL(19,4)` + `CHAR(3)` currency,
timestamps UTC `DATETIME2(3)`.

| Schema | Table | Purpose |
|--------|-------|---------|
| identity | Users | Accounts; status, lockout, MFA flags |
| identity | Roles / Permissions / RolePermissions / UserRoles | RBAC |
| identity | RefreshTokens | Rotating refresh tokens, hash-only, family-based reuse detection |
| identity | UserDevices | Known devices for new-device security email |
| identity | UserTokens | Single-use email-verify / password-reset tokens (hash-only) |
| market | Stores | One store per owner; unique slug |
| market | SellerProfiles | KYC, tax id, bank ref, commission override |
| market | Follows | Buyer follows store |
| catalog | Categories | Tree; per-category commission pct |
| catalog | Products | Draft-to-published lifecycle, moderation status |
| catalog | ProductVariants | Priced editions with license type + download limit |
| catalog | ProductVersions | Version number + changelog |
| catalog | Tags / ProductTags | Tagging |
| catalog | VariantFileReadiness | Clean-file counts per variant, projected from the Files module's FileScanned event |
| catalog | ProductModerations | Moderation decision history with the moderator's reason |
| files | ProductFiles | Object-storage keys, checksum, AV scan status |
| files | FileUploads | Chunked upload sessions: declared size and checksum, part size, quarantine key |
| files | FileUploadParts | Received parts of an upload session |
| files | DownloadLogs | Per-download audit; partition candidate |
| orders | Carts / CartItems | Persistent per-user cart (guest cart is a signed cookie) |
| orders | Orders / OrderLines | Money totals, status lifecycle, commission snapshot per line |
| orders | Licenses | Entitlements: download limit/used, expiry |
| pay | Payments / Refunds | Provider-agnostic payment + refund records |
| pay | Idempotency | Replay store keyed by (key hash, endpoint) |
| pay | WebhookInbox | Exactly-once webhook processing |
| ledger | LedgerTransactions / LedgerLines | Append-only double-entry journal |
| ledger | Payouts | Payout lifecycle with provider ref |
| reviews | Reviews | Verified-purchase reviews, one per buyer per product |
| notify | Notifications | Per-user notifications (email/in-app/push) |
| admin | AuditLogs | Append-only privileged/financial action audit |
| admin | Settings | Key/JSON platform configuration |
| cms | Pages | CMS pages |
| analytics | Events | Raw event ingest; partition candidate |
| dbo | OutboxMessages | Transactional outbox for domain events |

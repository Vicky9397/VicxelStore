# db — SQL Server assets (spec repo plan 10.3)

EF Core migrations in `api/` own the schema going forward; this folder owns
everything EF should not: the canonical schema mirror for review, stored
procedures/functions/views, ordered idempotent SQL migrations as the deploy
vehicle, reference + demo seed, and deployment orchestration.

## Layout

| Folder | Contents |
|--------|----------|
| `schema/` | Canonical schema mirror, one file per module schema |
| `migrations/` | Ordered idempotent `Vxxx__desc.sql` runner scripts |
| `programmability/` | `functions/`, `views/`, `stored-procedures/` (all `CREATE OR ALTER`) |
| `seed/reference/` | Required in every environment: roles, permissions, categories, settings |
| `seed/demo/` | Deterministic demo dataset for dev/staging/E2E only |
| `deploy/` | `deploy.sh` (ordered apply) + `lint-append-only.sh` + rollback docs |
| `maintenance/` | Index/stats maintenance job registration |
| `docs/` | Data dictionary |

## Deploy

```bash
# infrastructure first: docker compose up -d (repo root)
./deploy/deploy.sh          # migrations -> programmability -> reference seed
./deploy/deploy.sh --demo   # additionally applies the demo dataset
```

Connection settings come from `DPM_DB_SERVER`, `DPM_DB_NAME`, `DPM_DB_USER`,
`DPM_DB_PASSWORD` (defaults target the docker-compose SQL Server).

## Demo dataset

`./deploy/deploy.sh --demo` additionally applies `seed/demo`: five accounts
(buyer, seller, admin, moderator and an unverified buyer), a verified store, and
a published product with two priced variants and a released version. Every demo
account uses the password `DemoPassword123!`. The stored hash is precomputed
with a fixed salt so the dataset reproduces byte-for-byte across environments,
and `Dpm.UnitTests` pins that hash to the API's real password hasher so a change
to the hashing parameters cannot silently strand the demo logins.

The demo seed is for `dev`, `staging` and E2E only; `deploy.sh` never applies it
without the explicit `--demo` flag.

## Verification status

CI runs two static gates over these scripts: `deploy/lint-append-only.sh` and
`deploy/lint-sql-parse.sh`, which parses every script as T-SQL. Both pass. They
prove the SQL is syntactically valid and free of writes against the append-only
tables; they do **not** prove the scripts apply cleanly against a live SQL
Server. A first run of `deploy.sh` against a real instance is still needed to
confirm the deploy end to end.

## Binding rules

- Money columns are `DECIMAL(19,4)` with a separate `CHAR(3)` ISO-4217 currency.
- Every externally referenced row carries `PublicId UNIQUEIDENTIFIER`
  (`NEWSEQUENTIALID()`); internal `BIGINT` ids never leave the API.
- All timestamps are UTC `DATETIME2(3)`.
- `ledger.LedgerTransactions`, `ledger.LedgerLines` and `admin.AuditLogs` are
  append-only. `deploy/lint-append-only.sh` fails the deploy (and CI) if any
  script issues UPDATE/DELETE against them.

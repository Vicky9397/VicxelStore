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

## Binding rules

- Money columns are `DECIMAL(19,4)` with a separate `CHAR(3)` ISO-4217 currency.
- Every externally referenced row carries `PublicId UNIQUEIDENTIFIER`
  (`NEWSEQUENTIALID()`); internal `BIGINT` ids never leave the API.
- All timestamps are UTC `DATETIME2(3)`.
- `ledger.LedgerTransactions`, `ledger.LedgerLines` and `admin.AuditLogs` are
  append-only. `deploy/lint-append-only.sh` fails the deploy (and CI) if any
  script issues UPDATE/DELETE against them.

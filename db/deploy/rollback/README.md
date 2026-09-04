# Rollback scripts

One script per migration that needs a rollback path, named `Vxxx__rollback.sql`.

Rules:

- Rollbacks must be reviewed and applied manually; they are never run by
  `deploy.sh`.
- Destructive rollbacks (dropping tables/columns with data) require explicit
  human approval per spec `11B section 13.4`.
- `ledger.*` and `admin.AuditLogs` are append-only: there is no rollback that
  removes rows from them. Corrections are new reversing ledger entries.
- `V001__init.sql` is the baseline; its only rollback is dropping the database,
  which is intentionally not scripted.

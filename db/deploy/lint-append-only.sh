#!/usr/bin/env bash
# Append-only lint (spec 10.3): no deploy/seed/migration script may UPDATE or
# DELETE against ledger.LedgerTransactions, ledger.LedgerLines or admin.AuditLogs.
set -euo pipefail

DB_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

violations=$(grep -rniE '(update|delete)[[:space:]]+(from[[:space:]]+)?(\[?ledger\]?\.\[?(LedgerTransactions|LedgerLines)\]?|\[?admin\]?\.\[?AuditLogs\]?)' \
  "$DB_DIR/migrations" "$DB_DIR/seed" "$DB_DIR/programmability" "$DB_DIR/schema" 2>/dev/null || true)

if [ -n "$violations" ]; then
  echo "Append-only violation: UPDATE/DELETE found against ledger or audit tables:" >&2
  echo "$violations" >&2
  exit 1
fi

echo "Append-only lint passed."

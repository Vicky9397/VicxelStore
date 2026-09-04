#!/usr/bin/env bash
# Parses every deployable SQL script as T-SQL so a syntax error is caught before
# it reaches a database. This is a parse check, not an execution check: it does
# not prove the scripts apply cleanly against a live SQL Server.
set -euo pipefail

DB_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

failed=0
while IFS= read -r -d '' file; do
  if sqlfluff parse --dialect tsql "$file" 2>&1 | grep -qi 'unparsable'; then
    echo "Unparsable T-SQL: $file" >&2
    failed=1
  fi
done < <(find "$DB_DIR/migrations" "$DB_DIR/programmability" "$DB_DIR/seed" "$DB_DIR/schema" \
  -name '*.sql' -type f -print0 2>/dev/null)

if [ "$failed" -ne 0 ]; then
  exit 1
fi

echo "SQL parse check passed."

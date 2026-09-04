#!/usr/bin/env bash
# Applies db assets in the spec-enforced order (spec 10.3):
#   migrations -> programmability (functions -> views -> procs) -> reference seed -> [demo seed]
# Idempotent: every script guards its own existence checks and can be re-run.
set -euo pipefail

SERVER="${DPM_DB_SERVER:-localhost,1433}"
DATABASE="${DPM_DB_NAME:-DpmMarketplace}"
USER="${DPM_DB_USER:-sa}"
PASSWORD="${DPM_DB_PASSWORD:-${MSSQL_SA_PASSWORD:-Dpm_Local_1!}}"
WITH_DEMO=0

for arg in "$@"; do
  case "$arg" in
    --demo) WITH_DEMO=1 ;;
    *) echo "Unknown argument: $arg" >&2; exit 2 ;;
  esac
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DB_DIR="$(dirname "$SCRIPT_DIR")"

SQLCMD=(sqlcmd -C -b -S "$SERVER" -U "$USER" -P "$PASSWORD")

run_file() {
  local file="$1" db="$2"
  echo ">> $file"
  "${SQLCMD[@]}" -d "$db" -i "$file"
}

"$SCRIPT_DIR/lint-append-only.sh"

echo "== Ensuring database $DATABASE exists"
"${SQLCMD[@]}" -d master -Q "IF DB_ID(N'$DATABASE') IS NULL CREATE DATABASE [$DATABASE];"
"${SQLCMD[@]}" -d master -Q "ALTER DATABASE [$DATABASE] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;"

echo "== Migrations"
for f in "$DB_DIR"/migrations/V*.sql; do
  [ -e "$f" ] || continue
  run_file "$f" "$DATABASE"
done

echo "== Programmability"
for dir in functions views stored-procedures; do
  for f in "$DB_DIR"/programmability/$dir/*.sql; do
    [ -e "$f" ] || continue
    run_file "$f" "$DATABASE"
  done
done

echo "== Reference seed"
for f in "$DB_DIR"/seed/reference/R*.sql; do
  [ -e "$f" ] || continue
  run_file "$f" "$DATABASE"
done

if [ "$WITH_DEMO" -eq 1 ]; then
  echo "== Demo seed"
  for f in "$DB_DIR"/seed/demo/D*.sql; do
    [ -e "$f" ] || continue
    run_file "$f" "$DATABASE"
  done
fi

echo "== Done"

#!/usr/bin/env bash
# scripts/reset-db.sh
#
# Drops and recreates the development database, reapplies migrations,
# and lets the API's idempotent seeder repopulate RBAC + localization +
# dev identities on next startup. Development/Codespaces use only -
# never point this at a production connection string.

set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
REPO_ROOT="$(pwd)"

DB_HOST="${BARD_DB_HOST:-sqlserver}"
DB_PORT="${BARD_DB_PORT:-1433}"
DB_PASSWORD="${BARD_DB_PASSWORD:-BardDev!Passw0rd2026}"
DB_NAME="${BARD_DB_NAME:-BardDb.Dev}"
export PATH="$PATH:/opt/mssql-tools18/bin"

echo "This will DROP the development database '${DB_NAME}' on ${DB_HOST}:${DB_PORT}."
read -r -p "Type 'yes' to continue: " CONFIRM
if [ "${CONFIRM}" != "yes" ]; then
    echo "Cancelled."
    exit 1
fi

echo "Stopping any running BARD API..."
pkill -f "dotnet.*BARD.API.dll" 2>/dev/null || true
sleep 1

echo "Dropping database (if it exists)..."
sqlcmd -S "${DB_HOST},${DB_PORT}" -U sa -P "${DB_PASSWORD}" -C -Q \
    "IF DB_ID('${DB_NAME}') IS NOT NULL BEGIN ALTER DATABASE [${DB_NAME}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [${DB_NAME}]; END"

echo "Reapplying migrations..."
dotnet ef database update \
    --project "${REPO_ROOT}/src/BARD.Infrastructure/BARD.Infrastructure.csproj" \
    --startup-project "${REPO_ROOT}/src/BARD.API/BARD.API.csproj"

echo ""
echo "Database reset complete. RBAC/localization/dev-identity seeding"
echo "will run automatically the next time you start the API"
echo "(./scripts/start-dev.sh)."

#!/usr/bin/env bash
# scripts/setup.sh
#
# Runs automatically when the GitHub Codespace is created
# (devcontainer.json's postCreateCommand). Prepares the full BARD
# development environment: waits for SQL Server, restores/builds/tests
# the backend, generates+applies EF Core migrations, seeds the
# database, and installs+builds the frontend.
#
# Every step reports success/failure explicitly and the script stops
# (non-zero exit) on the first failure — nothing is hidden.

set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
REPO_ROOT="$(pwd)"

STEP=0
FAILED=0

step() {
    STEP=$((STEP + 1))
    echo ""
    echo "== Step ${STEP}: $1 =="
}

run() {
    local description="$1"; shift
    if "$@"; then
        echo "  ✓ ${description}"
        return 0
    else
        local code=$?
        echo "  ✗ ${description} FAILED (exit code ${code})"
        FAILED=1
        return "$code"
    fi
}

step "Wait for SQL Server readiness"
DB_HOST="${BARD_DB_HOST:-sqlserver}"
DB_PORT="${BARD_DB_PORT:-1433}"
DB_PASSWORD="${BARD_DB_PASSWORD:-BardDev!Passw0rd2026}"
export PATH="$PATH:/opt/mssql-tools18/bin"

READY=0
for i in $(seq 1 60); do
    if sqlcmd -S "${DB_HOST},${DB_PORT}" -U sa -P "${DB_PASSWORD}" -C -Q "SELECT 1" >/dev/null 2>&1; then
        READY=1
        break
    fi
    echo "  ...waiting for SQL Server (${i}/60)"
    sleep 2
done
if [ "${READY}" -eq 1 ]; then
    echo "  ✓ SQL Server is ready"
else
    echo "  ✗ SQL Server did not become ready in time"
    FAILED=1
fi

step ".NET dependency restore"
run "dotnet restore" dotnet restore "${REPO_ROOT}/BARD.sln"

step "Backend build"
run "dotnet build" dotnet build "${REPO_ROOT}/BARD.sln" --no-restore -c Debug

step "EF Core migrations (generate if missing, then apply)"
MIGRATIONS_DIR="${REPO_ROOT}/src/BARD.Infrastructure/Persistence/Migrations"
if ! ls "${MIGRATIONS_DIR}"/*.cs >/dev/null 2>&1; then
    echo "  No migrations found — generating InitialCreate."
    run "dotnet ef migrations add InitialCreate" dotnet ef migrations add InitialCreate \
        --project "${REPO_ROOT}/src/BARD.Infrastructure/BARD.Infrastructure.csproj" \
        --startup-project "${REPO_ROOT}/src/BARD.API/BARD.API.csproj" \
        --output-dir Persistence/Migrations
else
    echo "  ✓ Migrations already present, skipping generation."
fi
run "dotnet ef database update" dotnet ef database update \
    --project "${REPO_ROOT}/src/BARD.Infrastructure/BARD.Infrastructure.csproj" \
    --startup-project "${REPO_ROOT}/src/BARD.API/BARD.API.csproj"

step "Backend tests"
run "dotnet test" dotnet test "${REPO_ROOT}/BARD.sln" --no-build

step "Frontend dependency install"
run "npm install" npm --prefix "${REPO_ROOT}/frontend" install

step "Frontend type check"
run "npm run typecheck" npm --prefix "${REPO_ROOT}/frontend" run typecheck

step "Frontend production build"
run "npm run build" npm --prefix "${REPO_ROOT}/frontend" run build

step "Database seeding"
echo "  RBAC + localization seeding runs automatically on API startup (DatabaseSeeder)."
echo "  It will run the first time you execute ./scripts/start-dev.sh."

echo ""
if [ "${FAILED}" -eq 0 ]; then
    echo "=================================================="
    echo " Setup complete. Run ./scripts/start-dev.sh to start BARD."
    echo "=================================================="
    exit 0
else
    echo "=================================================="
    echo " Setup finished with FAILURES — see steps marked ✗ above."
    echo "=================================================="
    exit 1
fi

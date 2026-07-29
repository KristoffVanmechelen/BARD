#!/usr/bin/env bash
# scripts/start-dev.sh
#
# Starts the complete BARD development environment: verifies SQL Server
# is reachable, starts the ASP.NET Core API (HTTP, dev-only, port 5080),
# and starts the React frontend dev server (port 5173). Both run in the
# background; logs are written to ./logs/.

set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
REPO_ROOT="$(pwd)"
mkdir -p logs

DB_HOST="${BARD_DB_HOST:-sqlserver}"
DB_PORT="${BARD_DB_PORT:-1433}"
DB_PASSWORD="${BARD_DB_PASSWORD:-BardDev!Passw0rd2026}"
export PATH="$PATH:/opt/mssql-tools18/bin"

echo "Checking SQL Server..."
if ! sqlcmd -S "${DB_HOST},${DB_PORT}" -U sa -P "${DB_PASSWORD}" -C -Q "SELECT 1" >/dev/null 2>&1; then
    echo "SQL Server not reachable at ${DB_HOST}:${DB_PORT}."
    echo "If this is the first run, wait a few seconds for the container to finish starting and try again."
    exit 1
fi
echo "  OK: SQL Server is reachable."

pkill -f "dotnet.*BARD.API.dll" 2>/dev/null || true
pkill -f "vite" 2>/dev/null || true
sleep 1

echo "Starting BARD API (http://localhost:5080)..."
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://+:5080
( cd "${REPO_ROOT}/src/BARD.API" && dotnet run --no-launch-profile > "${REPO_ROOT}/logs/api.log" 2>&1 & )

echo "Waiting for the API to come up..."
API_UP=0
for i in $(seq 1 30); do
    if curl -fsS "http://localhost:5080/api/v1/terminology/bundle/nl-BE" >/dev/null 2>&1; then
        API_UP=1
        break
    fi
    sleep 1
done
if [ "${API_UP}" -eq 1 ]; then
    echo "  OK: API is responding."
else
    echo "  WARNING: API did not respond in time - check logs/api.log"
fi

echo "Starting BARD frontend (http://localhost:5173)..."
( cd "${REPO_ROOT}/frontend" && npm run dev -- --host > "${REPO_ROOT}/logs/frontend.log" 2>&1 & )
sleep 2

echo ""
echo "=================================================="
echo " BARD development environment is starting."
echo ""
echo " In GitHub Codespaces: open the 'Ports' tab and click the globe"
echo " icon next to port 5173 (it should also open automatically)."
echo ""
echo " Frontend: http://localhost:5173  (forwarded automatically by Codespaces)"
echo " API:      http://localhost:5080/swagger  (Swagger UI, Development only)"
echo ""
echo " Logs: logs/api.log, logs/frontend.log"
echo " Stop: pkill -f 'BARD.API.dll'; pkill -f vite"
echo "=================================================="

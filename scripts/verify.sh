#!/usr/bin/env bash
# scripts/verify.sh
#
# Full verification pass: restore, build, test, frontend typecheck/build,
# migration validation, basic repository consistency checks. Exits
# non-zero if ANY step fails. Intended to be run after setup.sh, or in
# CI, or any time you want to confirm the repository is in a known-good
# state.

set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
REPO_ROOT="$(pwd)"

FAILED=0
STEP=0

step() {
    STEP=$((STEP + 1))
    echo ""
    echo "== Verify step ${STEP}: $1 =="
}

run() {
    local description="$1"; shift
    if "$@"; then
        echo "  PASS: ${description}"
        return 0
    else
        echo "  FAIL: ${description}"
        FAILED=1
        return 1
    fi
}

step "dotnet restore"
run "dotnet restore" dotnet restore "${REPO_ROOT}/BARD.sln"

step "dotnet build"
run "dotnet build" dotnet build "${REPO_ROOT}/BARD.sln" --no-restore -c Debug

step "dotnet test"
run "dotnet test" dotnet test "${REPO_ROOT}/BARD.sln" --no-build

step "Migration validation (does a migration exist and apply cleanly?)"
MIGRATIONS_DIR="${REPO_ROOT}/src/BARD.Infrastructure/Persistence/Migrations"
if ls "${MIGRATIONS_DIR}"/*.cs >/dev/null 2>&1; then
    echo "  PASS: migration files present"
    run "dotnet ef database update (idempotent)" dotnet ef database update \
        --project "${REPO_ROOT}/src/BARD.Infrastructure/BARD.Infrastructure.csproj" \
        --startup-project "${REPO_ROOT}/src/BARD.API/BARD.API.csproj"
else
    echo "  FAIL: no migration files found - run ./scripts/setup.sh first"
    FAILED=1
fi

step "Frontend dependency install"
run "npm install" npm --prefix "${REPO_ROOT}/frontend" install

step "Frontend type check"
run "npm run typecheck" npm --prefix "${REPO_ROOT}/frontend" run typecheck

step "Frontend production build"
run "npm run build" npm --prefix "${REPO_ROOT}/frontend" run build

step "Repository consistency checks"
# Every controller referenced from a permission policy must have that
# policy registered (catches copy-paste drift between PermissionCodes
# and AuthorizationExtensions).
MISSING_POLICIES=0
for code in $(grep -ohP 'PermissionCodes\.\K\w+' -r "${REPO_ROOT}/src/BARD.API/Controllers" | sort -u); do
    if ! grep -q "PermissionCodes.${code}" "${REPO_ROOT}/src/BARD.API/Extensions/AuthorizationExtensions.cs"; then
        echo "  FAIL: PermissionCodes.${code} used in a controller but not registered as a policy"
        MISSING_POLICIES=1
    fi
done
if [ "${MISSING_POLICIES}" -eq 0 ]; then
    echo "  PASS: every controller permission code has a registered policy"
else
    FAILED=1
fi

# Every DI-registered interface should resolve to exactly one implementation.
DUPLICATE_DI=$(grep -oP 'services\.Add\w+<\K[A-Za-z0-9_.]+(?=,)' "${REPO_ROOT}/src/BARD.Infrastructure/DependencyInjection.cs" | sort | uniq -d)
if [ -z "${DUPLICATE_DI}" ]; then
    echo "  PASS: no duplicate DI registrations"
else
    echo "  FAIL: duplicate DI registrations found: ${DUPLICATE_DI}"
    FAILED=1
fi

echo ""
if [ "${FAILED}" -eq 0 ]; then
    echo "=================================================="
    echo " VERIFICATION PASSED"
    echo "=================================================="
    exit 0
else
    echo "=================================================="
    echo " VERIFICATION FAILED - see FAIL lines above"
    echo "=================================================="
    exit 1
fi

# BARD — Belgian Accise Refund & Document Analyzer

**Version 1.0 (production-v1 scope)**

Enterprise implementation per the frozen BARD architecture decisions and
the BERDS functional specification. Automates the objective work of
validating Belgian excise duty refund dossiers while keeping every
legal decision with the customs officer (Core Philosophy).

## Stack (authoritative, frozen)

- **Backend:** ASP.NET Core 8, Clean Architecture (Domain / Application / Infrastructure / API / Contracts), MediatR (CQRS), EF Core, SQL Server, Azure Blob Storage, Microsoft Entra ID + enterprise RBAC.
- **Frontend:** React 18 + TypeScript + Material UI, MSAL for Entra ID auth, react-i18next for localization (nl-BE default, fr-BE, de-BE, en), TanStack Query.

## What's implemented (Production v1)

**Domain & pipeline** (ported from the original Python prototype, business rules preserved exactly):
- Dossier / DossierLine / DossierDocument / Ac4Declaration aggregate with full match/export/MRN/AC4/calculation/review trail.
- Document classification (content-based, never filename), invoice & AC4 parsing (PdfPig + Tesseract OCR fallback), Excel claim reading (ClosedXML).
- Matching engine: 35/20/20/15/10 weighted scoring, 95/80 thresholds, **exact-quantity matching (no tolerance)**, missing-excise-code hard block, alias-based product synonym resolution.
- Export/destination validation, MRN cumulative-quantity validation (no tolerance), AC4-date-vs-**application**-date deadline check (never "today").
- Refund calculation (quantity × rate against the historically-correct `ExciseRateVersion`); Plato/alcohol-conversion units deliberately refused rather than guessed (no such data exists in the model).
- On-demand-only AI-assist fallback, structurally unreachable from the automatic pipeline.

**Company** (Phase 5, integrated into the upload workflow): explicit name + enterprise/VAT number captured on upload, normalized deduplication with a concurrency-safe resolve-or-create, Excel-derived company data never used as the source of truth.

**Workflow UI:**
- **Upload/process** (`/dossiers/new`): Excel + PDF upload, company identification, refund application date, progress/error feedback, result summary.
- **Officer review** (`/dossiers/:id`): per-line findings (match/export/MRN/AC4/calculation), approve/reject with remarks, reviewer identity + timestamp, dossier status auto-recomputes.
- **Export** (`/dossiers/:id` → download button): starts from the **original preserved Excel workbook** in Blob Storage, appends BARD's validation/status/calculation/decision columns with conditional formatting — never reconstructs from parsed entities.
- **Settings > Terminology** and **Settings > Excise Rates**: full admin CRUD, inline "Edit texts" mode, versioned non-destructive rate history.

**Platform:**
- RBAC: `Officer` and `Administrator` roles, seeded idempotently with a documented permission mapping (`RbacSeedData.cs`). Server-side `[Authorize(Policy=...)]` on every endpoint; frontend UI gates the same way via `GET /api/v1/users/me`.
- Localization: all 92 UI strings currently referenced by the frontend seeded in nl-BE (default)/fr-BE/de-BE/en (`LocalizationSeedData.cs`). Backend error messages route through the same mechanism where a stable key exists, falling back safely to English otherwise.
- Development-only test identity seeding, gated behind explicit configuration, never present in production settings.

## Known, explicitly out-of-scope for v1

Postponed to v1.1 per the accepted execution decisions: configuration export/import, configurable branding/colours/icons, terminology/excise-rate history UI, AI-assist frontend trigger, distinct eAD/eVAD parser refinement, doubtful-authenticity/manual-exclusion structured flags, audit-log read UI, statistics dashboard, bulk alias-dictionary management, Excise Rates pagination, pluggable rules-registry restructuring. Underlying functionality for all of these remains intact (nothing was removed) — only their dedicated UI/admin surface is deferred.

## Verification status

This repository was built in a sandbox with no NuGet/npm registry access, no .NET SDK, and no SQL Server. Every file has been:
- **C#:** hand-written, reviewed, and mechanically brace/paren-balance-checked across all 74 source + 7 test files. **Not yet compiled** — see "Run BARD in GitHub Codespaces" below for a real, working build/test environment.
- **TypeScript/React:** parsed with the real TypeScript compiler's parser (0 syntax errors across all 19 files) but not type-checked against installed dependencies.

**Nothing in this repository should be assumed to compile or pass tests until actually run in a real environment** — Codespaces (below) is the fastest way to do that from a browser alone.

## Setup (local, if you already have the tooling)

```bash
# Backend
dotnet restore
dotnet build
dotnet ef migrations add InitialCreate --project src/BARD.Infrastructure --startup-project src/BARD.API
dotnet ef database update --project src/BARD.Infrastructure --startup-project src/BARD.API
dotnet test
dotnet run --project src/BARD.API

# Frontend
cd frontend
npm install
npm run typecheck
npm run build
npm run dev
```


## Run BARD in GitHub Codespaces

You do **not** need to install anything on your computer — a web browser is enough.

**1. Create or open the private GitHub repository.** On github.com, click **New repository**, mark it **Private**, and create it (an empty repository is fine).

**2. Upload the complete repository contents.** On the repository page, click **Add file > Upload files**, drag in everything from this delivery (or use `git push` if you're comfortable with Git — either works).

**3. Create a Codespace.** Click the green **Code** button > **Codespaces** tab > **Create codespace on main**.

**4. Wait for initialization.** The Codespace automatically builds a container with .NET 8, Node.js, and a SQL Server database, then runs `scripts/setup.sh` (restore, build, migrate, seed, test, install frontend deps, typecheck, build). This takes several minutes the first time — watch the terminal that opens automatically.

**5. Start BARD.** If it isn't already running, open a terminal (**Terminal > New Terminal**) and run:
```
./scripts/start-dev.sh
```

**6. Open the frontend.** A notification should pop up offering to open port 5173 in your browser — click it. If not, open the **Ports** tab at the bottom of the screen and click the globe icon next to port **5173**.

**7. Choose a development identity.** The app shows a yellow bar at the top — click **Dev Officer** or **Dev Administrator** to continue. This never appears in a real production deployment; it exists only so you can test BARD without a real Entra ID tenant.

**8. Test the application.** Upload a dossier, review lines, download a report — exactly as described earlier in this README.

**9. Download generated exports.** Files downloaded through the browser (e.g. the Excel report) save to your computer normally, the same as any other website download — Codespaces runs in the cloud, but your browser's downloads still land locally.

**10. Stop or delete the Codespace when finished.** Go to github.com/codespaces, and either **Stop** (keeps it for later, still counts toward storage quota) or **Delete** (frees everything) your Codespace to conserve your free monthly usage.

### Rerunning verification
```
./scripts/verify.sh
```
Runs `dotnet restore/build/test`, frontend install/typecheck/build, migration validation, and a few repository consistency checks. Exits non-zero if anything fails — nothing is hidden.

### Resetting the development database
```
./scripts/reset-db.sh
```
Drops and recreates the dev database, reapplies migrations. RBAC/localization/dev-identity seeding runs automatically the next time you start the API.

### Inspecting failures
- Setup/verify output prints `PASS`/`FAIL` per step.
- Runtime logs: `logs/api.log`, `logs/frontend.log` (created by `start-dev.sh`).
- The **ms-mssql.mssql** VS Code extension (pre-installed in the Codespace) can connect directly to the dev SQL Server for manual inspection.

### What's a production deployment concern, not a Codespaces concern
- Real Microsoft Entra ID app registration (client id, tenant id, API scope) — Codespaces uses the dev-identity bypass instead.
- Real Azure Blob Storage account — Codespaces runs the official Azurite emulator instead (`.devcontainer/compose.yaml`), so document upload/export work end-to-end without any Azure subscription.
- Azure OpenAI endpoint/key for AI-assist — not required for the core workflow, only for the on-demand extraction feature (which also has no frontend trigger yet — postponed to v1.1).
- HTTPS/TLS certificates and `UseHttpsRedirection` — Codespaces' own port forwarding handles TLS; a real deployment needs its own certificate.

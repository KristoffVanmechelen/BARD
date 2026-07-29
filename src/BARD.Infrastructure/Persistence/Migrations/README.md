# EF Core Migrations

This folder is intentionally empty in this delivery. Migrations could
not be generated in the sandbox this project was built in (no NuGet
access, no `dotnet` SDK installed, no SQL Server instance available).

## Generate the initial migration

Run this from the repository root, in an environment with the .NET 8
SDK and NuGet access:

```bash
dotnet tool install --global dotnet-ef   # if not already installed
cd src/BARD.API
dotnet ef migrations add InitialCreate \
  --project ../BARD.Infrastructure/BARD.Infrastructure.csproj \
  --startup-project BARD.API.csproj \
  --output-dir Persistence/Migrations
```

## Apply it to a database

```bash
dotnet ef database update \
  --project ../BARD.Infrastructure/BARD.Infrastructure.csproj \
  --startup-project BARD.API.csproj
```

## After first run

On startup, `DatabaseSeeder` (see
`BARD.Infrastructure/Persistence/Seed/`) idempotently seeds:
- RBAC roles (`Officer`, `Administrator`) and all `PermissionCodes.*`
  permissions, with the mapping defined in `RbacSeedData.cs`.
- All 91 localization keys currently referenced by the frontend, in
  nl-BE (default), fr-BE, de-BE, and en — see `LocalizationSeedData.cs`.

Re-running the app (and therefore the seeder) is always safe: it only
inserts rows that don't already exist and never touches administrator
overrides made afterward through the UI.

Do NOT manually hand-write a migration file to replace this step —
migrations must be generated from the actual compiled model to be
trustworthy; a hand-written one could silently drift from the real
entity configuration.

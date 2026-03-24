# Session Summary - 2026-03-23 CosmosDB Migration

**Session Duration:** ~8 hours (across multiple sub-sessions)
**Build Status:** All projects building (1 warning: CS8604 nullable in GameApiController)
**Test Status:** 27/27 CatanDb contract tests passing, 76 shared tests passing
**Branch:** cosmos-migration (merged to staging via PRs #74, #76)

## Work Completed

### CosmosDB Data Layer

- **ICatanDb abstraction** — New database interface replacing EF Core `CatanDbContext`
  - 5 containers: players, games, completed-games, templates, recordings
  - All CRUD operations, image storage, game archival
  - Key file: `Catan3.GameService/Abstractions/ICatanDb.cs`

- **CosmosCatanDb implementation** — Full CosmosDB implementation using .NET SDK
  - Flattened document model (no embedded JSON strings)
  - `PlayerDoc` has first-class `name`, `colors`, `imageUri`, `imageData` fields
  - `DefaultAzureCredential` for Azure, emulator key for local dev
  - Auto-detects local vs Azure via `WEBSITE_SITE_NAME` env var
  - Key file: `Catan3.GameService/Abstractions/CosmosCatanDb.cs`

- **27 contract tests** — Full coverage of ICatanDb contract
  - Run against both local Cosmos emulator and Azure CosmosDB
  - Key files: `Tests/GameService/CatanDb/CatanDbContractTests.cs`,
    `Tests/GameService/CatanDb/CosmosCatanDbTests.cs`

### Seed Pipeline

- **Flattened player JSON documents** — 7 player files are exact CosmosDB documents
  - No transformation at insert time; seed script loops and inserts verbatim
  - `encode-images.ps1` embeds base64 images into JSON files
  - Key directory: `Catan3.GameService/Default Data/Players/`

- **Recording seed data** — 5 `.catan_test` files converted to JSON and seeded
  - Key directory: `Catan3.GameService/Default Data/Recordings/`

- **Switched from REST API to .NET SDK** — Eliminated partition key header issues
  - REST API `x-ms-partitionkey` header returned "fewer components" error on every write
  - Root cause never fully determined; SDK handles partition keys internally
  - Load Cosmos SDK DLLs from GameService build output into PowerShell

### database.ps1 Script (1858 lines)

- **Full CosmosDB lifecycle management:**
  - `install` — Pull emulator image, create containers, seed data (local)
  - `install -Azure` — Create account, database, containers, RBAC, firewall, seed
  - `deploy -Azure` — Grant Managed Identity RBAC roles
  - `seed-data` / `seed-data -Azure` — Insert default data via SDK
  - `doctor` / `doctor -Azure` — Health checks with status reporting
  - `test` / `test -Azure` — Run 27 contract tests
  - `nuke-containers` — Delete and recreate all containers

- **Doctor-driven install:** `Get-AzureDoctorResult` returns hashtable driving all decisions
  - Firewall check: `publicNetworkAccess=Enabled` + empty ipRules
  - RBAC probe via SDK (not REST) to verify write access before seeding

### GameService Wiring

- **All controllers/services rewired to ICatanDb:**
  - `GameApiController` — player CRUD, game save/load, image upload/download
  - `RecordingController` — recording list/load/delete
  - `StatsController` — completed games stats
  - `GameTemplateService` — template CRUD
  - `DatabasePersistenceService` — game auto-save
  - `DatabaseSeedingService` — startup initialization

- **Dead code removed:**
  - `CatanDbContext.cs`, `DatabaseProviderDetector.cs`, `AzureSqlDiagnosticService.cs` deleted
  - Stale EF Core NuGet packages removed from .csproj
  - Dead `using` statements cleaned from Program.cs, GameApiController.cs
  - SQLite functions removed from catan.ps1

### CI/CD Updates

- **deploy-azure.yml / deploy-staging.yml** — Use `database.ps1` instead of old `catan-azure.ps1`
- **CI test filter** — Skip CatanDb contract tests in CI (require Cosmos emulator)
- **.code-reviews to .gitignore** — Review artifacts not tracked in git

## Key Decisions

| Decision | Rationale |
|----------|-----------|
| SDK over REST API | REST partition key header broken; SDK handles internally |
| Flattened documents | CosmosDB best practice; queryable/indexable fields |
| JSON files = CosmosDB docs | No transformation at seed time; single source of truth |
| encode-images.ps1 separate | Image encoding is a build step, not a runtime step |
| DefaultAzureCredential | No secrets in config; Managed Identity on Azure |
| disableLocalAuth=true | AAD-only auth on CosmosDB account |
| No DatabaseSeedingService work | Templates will become seed data JSON files (future) |
| Config-as-code over env vars | Detect environment, read config; avoid env var dependencies |

## Bugs Found During Firewall/Partition Key Investigation

- **HTTP 403 firewall:** `publicNetworkAccess: "Disabled"` overrides all ipRules
  - Fix: `az cosmosdb update --public-network-access Enabled --ip-range-filter ""`
  - Corporate proxy egress IPs differ from `api.ipify.org` — IP allow-lists unreliable

- **HTTP 400 "fewer components":** Every REST write to CosmosDB failed
  - All partition key header formats tried (`["value"]`, `"value"`, bare, empty)
  - Management plane showed correct single-path `/id` definition
  - Root cause undetermined; switching to .NET SDK resolved it completely

## Code Reviews

- **Claude review:** 3 critical, 5 important, 5 suggestions across 73 files
- **Copilot review:** File-by-file reviews in `.code-reviews/cosmos-migration/cp/`
- **Cross-verified:** Issues agreed by both reviewers filed as GitHub issues
- Review output: `.code-reviews/cosmos-migration/claude/` and `.code-reviews/cosmos-migration/cp/`

## Open Issues Filed

| # | Title | Priority |
|---|-------|----------|
| 57 | Remove SQLite functions from catan.ps1 | Must fix |
| 58 | Remove dead using from GameApiController | Must fix |
| 60 | Guard debug console.log in React | Soon |
| 61 | Replace Console.WriteLine with ILogger | Soon |
| 62 | SELECT projection to exclude imageData | Soon |
| 63 | Health endpoint lightweight query | Soon |
| 64 | GameTemplateService point read | Soon |
| 65 | Document DeletePlayerAsync cascade | Soon |
| 67 | SaveTemplateAsync mutates caller data | Soon |
| 77 | Staging missing Cosmos app settings | CI/CD — CLOSED |
| 78 | Staging identity missing RBAC role | CI/CD — CLOSED |
| 79 | Deploy skips GameService on workflow changes | CI/CD — CLOSED |
| 80 | RBAC assignments not idempotent | CI/CD — CLOSED |
| 81 | React renders raw HTML error page | UX |
| 82 | One-time SQL Server data migration | CLOSED |
| 84 | Health check circular dependency | CI/CD — CLOSED |
| 85 | Ops disabling publicNetworkAccess | CI/CD — CLOSED |

## PRs

| # | Title | Status |
|---|-------|--------|
| 74 | CosmosDB migration (cosmos-migration → staging) | Merged |
| 75 | CosmosDB migration (staging → main) | Open |
| 76 | CI/CD fixes for CosmosDB (cosmos-migration → staging) | Merged |
| 83 | CI/CD pipeline fixes for slot support (#77-80) | Merged |
| 86 | catan-cicd.ps1 orchestrator (#84, #85) | Merged |
| 87 | Unicode fix for catan-cicd.ps1 | Merged |

## Architecture Diagram

```text
React UI (Next.js)
    ↓ REST + SignalR
GameApiController / RecordingController / StatsController
    ↓ ICatanDb
CosmosCatanDb (.NET SDK)
    ↓ DefaultAzureCredential (Azure) / Emulator Key (local)
Azure CosmosDB / Local Emulator
```

### CI/CD Orchestrator

- **`catan-cicd.ps1`** — Unified CI/CD script solving circular dependency
  - Infrastructure runs BEFORE app deployment (firewall → app settings → RBAC → deploy → verify)
  - Both deploy-staging.yml and deploy-azure.yml use it
  - Fixes ops team disabling `publicNetworkAccess` between deploys

### SQL Server Data Migration (#82)

- **`export-sql.ps1`** — Exported all 6 SQL tables via AAD token auth
  - Fixed `Invoke-Sqlcmd` truncation with `-MaxBinaryLength 10485760`
  - Raw exports preserved in `Default Data/sql-export/` (gitignored)
- **`transform-to-cosmos.ps1`** — Transformed to CosmosDB document format
  - Players: flattened with embedded base64 images
  - Games: merged metadata + data tables into single documents
  - Recordings: extracted gameId as first-class field
  - Templates: extracted summary fields (minPlayers, maxPlayers, etc.)
- **Verified on staging:** 9 players, 29 games, 4 completed games, 3 templates, 5 recordings — all loading correctly

## Next Steps

1. Merge PR #75 to main (staging validated, all blocking issues closed)
2. Run `workflow_dispatch` on production deploy to force full deploy
3. Fix remaining non-blocking issues (#60-65, #67, #69-72, #81)
4. Remove Azure SQL from resource group and clean up sqlServer config
5. Convert templates from C# code to seed JSON files
6. Remove DatabaseSeedingService entirely

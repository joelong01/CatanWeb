# CosmosDB Migration Design

**Status:** Draft — 2026-03-18

## Overview

The project currently uses SQLite (local development) and Azure SQL Serverless (production), both
accessed via Entity Framework Core. This document describes the migration to Azure Cosmos DB for
NoSQL, which is a better architectural fit for the data: three of six entity types already store
JSON blobs, and there are no complex joins or aggregation queries in the codebase.

### Goals

- Replace EF Core / SQL with a clean `ICatanDb` abstraction layer
- Eliminate SQL semantics from application code (controllers, services, tests)
- Support both inner loop (offline, local Docker emulator) and outer loop (Azure CosmosDB account)
- Phase the migration so production continuity is preserved at each step

### Non-Goals

- Changes to the React UI or SignalR protocol
- Changes to the `GameStateMachine` or game rules engine
- Moving images to Azure Blob Storage (images remain embedded in player documents)

---

## Current Data Layer Audit

### Entities and Storage Patterns

Six entity types exist in `Catan3.GameService/Data/CatanDbContext.cs`:

| Entity | Key | Storage | SQL-Specific Concerns |
|--------|-----|---------|----------------------|
| `PlayerEntity` | `Id` (string) | JSON blob in `Data` column | None — already document-style |
| `ImageEntity` | `Id` (string) | Binary `Data` column | None — will embed in player doc |
| `GameSaveMetadataEntity` | `Id` (int, auto) | Relational columns + FK | Two-table pattern, cascade delete, `Include()` |
| `GameSaveDataEntity` | `Id` (int, auto) | Binary blob | FK parent of metadata |
| `CompletedGameEntity` | `Id` (int, auto) | Relational columns + binary | Auto-increment int key |
| `RecordingEntity` | `Id` (string, GUID) | JSON blob in `Data` column | `String.Contains()` on JSON field |
| `GameTemplateEntity` | `Id` (string) | JSON blob in `Data` column | None — already document-style |

### Where SQL Semantics Have Leaked

The following code directly injects or resolves `CatanDbContext` and must be refactored:

- `GameApiController` (`Controllers/GameApiController.cs`) — players, images, metadata, completed
  games; **also contains raw SQL DDL** at lines 2130–2341: dual-dialect `CREATE TABLE` statements
  for every table (SQL Server and SQLite variants), executed via `ExecuteSqlRawAsync`. This is a
  separate category of SQL coupling beyond normal DbContext queries — the entire schema-bootstrap
  endpoint must be deleted or replaced with `ICatanDb.InitializeAsync()`
- `StatsController` (`Controllers/StatsController.cs`) — `Players.ToListAsync()`
- `GameTemplateService` (`Services/GameTemplateService.cs`) — template CRUD via `_context.GameTemplates`
- `RecordingService` (`Services/RecordingService.cs`) — scope factory → DbContext for every persist
- `DatabaseSeedingService` (`Services/DatabaseSeedingService.cs`) — resolves `CatanDbContext` from
  scope factory on startup (line 36)
- `AzureSqlDiagnosticService` (`Services/AzureSqlDiagnosticService.cs`) — entirely Azure SQL-specific:
  resolves `CatanDbContext` at two locations (lines 416, 468); imports `Azure.ResourceManager.Sql`
  and `Microsoft.Data.SqlClient`; performs connection warmup, firewall diagnostics, auto-pause
  detection, and managed identity checks. This service must be redesigned as a
  `CosmosDbDiagnosticService` or deleted outright.
- `Program.cs` — three inline endpoint/health-check handlers resolve `CatanDbContext` directly
  at lines 221, 316, and 404; these must be rewritten against `ICatanDb`

### SQL-Specific Patterns to Eliminate

**Two-table game save pattern** (`GameSaveMetadataEntity` → `GameSaveDataEntity`):

- Joined via FK with `OnDelete(DeleteBehavior.Cascade)`
- Loaded with `Include(m => m.GameData)` (EF Core eager loading, relational-only)
- Will be flattened into a single CosmosDB document

**JSON field string search** (`RecordingService.DeleteRecordingsByGameIdAsync`):

```csharp
// Current — SQL-specific: searches raw JSON column
var searchPattern = $"\"gameId\":\"{gameId}\"";
var matches = await dbContext.Recordings
    .Where(r => r.Data.Contains(searchPattern))
    .ToListAsync();
```

Fix: add an explicit `GameId` property to `RecordingEntity` (indexed in CosmosDB).

**Recording `gameId` read path** (`RecordingController.GetRecordings`):

`RecordingController` also derives `GameId` by parsing the raw `data` JSON blob on every list
request (`ExtractGameIdFromData`, lines 168–199). Once `gameId` is a first-class field on the
stored document, this parsing path must be removed. `RecordingController` and any summary DTO
(`RecordingSummary`) must read `gameId` directly from the stored record.

**Auto-increment integer keys** (`GameSaveMetadata`, `GameSaveData`, `CompletedGame`):

CosmosDB requires string partition keys and string document ids. All int keys become GUIDs.

**EF Core schema management** (`DatabaseSeeder.cs`):

```csharp
await context.Database.MigrateAsync();      // SQL Server
await context.Database.EnsureCreatedAsync(); // SQLite
```

Both are replaced by `ICatanDb.InitializeAsync()` which creates CosmosDB containers if absent.

### Existing Abstractions (Keep and Extend)

- `IGamePersistence` / `GamePersistenceService` — game save/load, already abstract
- `IPersistenceService` / `DatabaseBackedPersistenceService` — platform-neutral file ops

These are superseded by `ICatanDb` in the new design but can delegate to it during Phase 1.

---

## ICatanDb Interface

The interface is **domain-specific**: callers express intent, not database operations.
No SQL terminology, no entity objects, no EF Core types cross this boundary.

**Location:** `Catan3.GameService/Abstractions/ICatanDb.cs`

### Lifecycle

```csharp
Task InitializeAsync(); // Create containers/schema and seed default data if absent
```

### Players

```csharp
Task<IReadOnlyList<PlayerProfile>> LoadPlayersAsync();
Task<PlayerProfile?> LoadPlayerAsync(string id);
Task SavePlayerAsync(PlayerProfile player);
Task DeletePlayerAsync(string id);
```

### Images (embedded in player document)

```csharp
Task<(byte[] Data, string ContentType)?> LoadImageAsync(string playerId);
Task SaveImageAsync(string playerId, byte[] data, string contentType);
Task DeleteImageAsync(string playerId);
```

### Game Saves

```csharp
Task<IReadOnlyList<GameSummary>> ListGamesAsync(string? startedBy = null);
Task<GameSaveData?> LoadGameAsync(string gameId);
Task SaveGameAsync(GameSaveData game);
Task DeleteGameAsync(string gameId);
Task<int> CountGamesAsync();
```

### Templates

```csharp
// GameTemplateSummary: existing type in Catan3.Shared/Models/GameTemplateData.cs
// GameTemplateData:    existing type in Catan3.Shared/Models/GameTemplateData.cs
Task<IReadOnlyList<GameTemplateSummary>> ListTemplatesAsync(string? category = null);
Task<GameTemplateData?> LoadTemplateAsync(string id);
Task SaveTemplateAsync(string id, string name, string category, bool isSystemTemplate, GameTemplateData data);
Task DeleteTemplateAsync(string id);
```

### Recordings

```csharp
// RecordingSummary: existing type in Catan3.Shared/Services/GameServiceProxy.cs
//                  GameId property must be added to this type
// RecordingEntity:  persistence model; callers receive RecordingSummary or raw data byte[]
Task<IReadOnlyList<RecordingSummary>> ListRecordingsAsync();
Task<(RecordingSummary Summary, string Data)?> LoadRecordingAsync(string id);
Task<(RecordingSummary Summary, string Data)?> FindRecordingByGameIdAsync(string gameId);
Task SaveRecordingAsync(RecordingSummary summary, string data);
Task DeleteRecordingAsync(string id);
Task DeleteRecordingsByGameIdAsync(string gameId);
```

### Completed Games

```csharp
// CompletedGameRecord: new persistence DTO in Catan3.GameService/Abstractions/
Task SaveCompletedGameAsync(CompletedGameRecord game);
Task<IReadOnlyList<CompletedGameRecord>> ListCompletedGamesAsync();
```

---

## CosmosDB Container Design

**Database name:** `catan`

### Containers

| Container | Partition Key | Notes |
|-----------|---------------|-------|
| `players` | `/id` | Player profile + base64 image embedded |
| `games` | `/id` | `gameId` IS the document `id`; merged metadata + data |
| `completed-games` | `/id` | `gameId` IS the document `id` |
| `templates` | `/id` | System and user templates |
| `recordings` | `/id` | Test recordings with explicit `gameId` field |

### Document Shapes

**`players` container item:**

```json
{
  "id": "Joe-001",
  "displayName": "Joe",
  "colors": { "primary": "#...", "secondary": "#..." },
  "lifetimeStats": { "gamesPlayed": 42, "gamesWon": 7 },
  "image": { "contentType": "image/png", "data": "<base64>" }
}
```

Images embedded as base64 are acceptable: there are at most ~10 players, each avatar is small
(thumbnails), and the document store avoids the operational overhead of a separate blob service.

**`games` container item (flattened from two-table pattern):**

```json
{
  "id": "game-abc-123",
  "gameName": "Friday Night Game",
  "gameState": "WaitingForRoll",
  "gameType": "Regular",
  "startedBy": "Joe-001",
  "playerCount": 4,
  "playerNames": "Joe, Doug, Ryan, Adrian",
  "turnCount": 23,
  "savedAt": "2026-03-18T20:00:00Z",
  "createdAt": "2026-03-18T18:00:00Z",
  "compressedData": "<base64 of compressed game log>",
  "size": 12345
}
```

The `gameId` from SQL becomes the CosmosDB document `id` directly — it already carries a UNIQUE
index and is the only identifier ever used to load or update a game. Generating a separate GUID
`id` would create a dual-identity problem: callers would need to track which identifier to pass
in each context.

The separation of metadata and data (two-table pattern) existed to allow listing games without
loading blobs. In CosmosDB this concern is handled by reading only specific properties in list
queries (projection), or by keeping `compressedData` large and accepting that list queries do not
return it (CosmosDB charges per RU, not per column like SQL).

**`recordings` container item:**

```json
{
  "id": "guid-...",
  "gameId": "game-abc-123",
  "name": "Friday Night Replay",
  "gameType": "Regular",
  "playerCount": 4,
  "playerIds": ["Joe-001", "Doug-002"],
  "actionCount": 187,
  "createdAt": "2026-03-18T20:00:00Z",
  "data": "<JSON: initialGameModel + actions array>"
}
```

The explicit `gameId` property enables a direct equality query (`WHERE c.gameId = @id`),
replacing the fragile `String.Contains()` on the raw JSON blob.

### Partition Key Rationale

- **`players/id`**: Tiny collection (~10 documents). Unique per player. All queries are by id
  or list-all (cross-partition, acceptable for small collections).
- **`games/id`**: Every game operation (save, load, delete) passes `gameId`, which is now the
  document `id`. List-all queries cross partitions — acceptable since active game counts are small.
- **`completed-games/id`**: Same rationale as games.
- **`templates/id`**: `ListTemplatesAsync(category)` filters by category and crosses partitions,
  but the collection is tiny (~5 templates) so this is operationally free. `LoadTemplateAsync(id)`
  resolves directly without a cross-partition query. Using `/id` is consistent with other
  containers and avoids the hidden issue of `/category` where `LoadTemplateAsync` would always
  cross partitions because the caller does not know the category at call time.
- **`recordings/id`**: `FindRecordingByGameIdAsync` crosses partitions (queries on `gameId` field,
  not on the partition key). The alternative — partitioning by `/gameId` — would make that query
  fast but make `LoadRecordingAsync(id)` cross-partition instead. Since list-all and find-by-gameId
  are the hot paths and recording counts are small, `/id` is the better trade-off.

---

## Inner Loop: Local Offline Story

### Technology: CosmosDB Emulator (Docker)

The Azure CosmosDB Emulator provides exact API parity with the cloud service. Running it locally
eliminates an entire class of bugs caused by semantic differences between a local surrogate and
the production database.

**Start the emulator:**

```bash
docker run -d -p 8081:8081 -p 10251-10254:10251-10254 \
  --name cosmos-emulator \
  mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview
```

**Emulator endpoint:** `https://localhost:8081`

**Emulator key:** Well-known fixed key published in Microsoft documentation. Safe for local use;
never used in production. Store in `appsettings.Development.json` (already `.gitignore`d) —
never in `appsettings.json` or committed to source control.

**TLS note:** The emulator uses a self-signed certificate. The CosmosDB SDK accepts this via
`CosmosClientOptions.HttpClientFactory` with certificate validation disabled for localhost only.

**Apple Silicon (ARM64) note:** The standard emulator image has compatibility issues on M-series
Macs. Use the `vnext-preview` tag shown above, which has improved Linux/ARM64 support. If issues
persist, add `--platform linux/amd64` to run under Rosetta 2 (slower but functional). The
`catan.ps1` emulator start command must handle both cases and surface a clear error if the
emulator fails the health check at `https://localhost:8081/_explorer/index.html`.

### Changes to `catan.ps1`

| Verb | Current behavior | New behavior |
|------|-----------------|--------------|
| `database install` | Seed SQLite via `dotnet run --seed-database` | Pull emulator image, start container, create containers, seed data |
| `database doctor` | Check SQLite file health | Check emulator running, containers present, player/template counts |
| `database clean` | Delete `catan.db` file | Stop and remove emulator container (data wiped) |
| `database stop` | (none) | Stop emulator container without removing data |
| `install` | Check dotnet, sqlite3 | Add Docker prereq check |
| `run` | Start GameService + React | Also start emulator if not running |

### Provider Detection (Local)

`DatabaseProviderDetector` (or its replacement `ICatanDbFactory`) detects:

- `COSMOS_ENDPOINT` env var set → use that endpoint (real CosmosDB or custom)
- `COSMOS_EMULATOR=true` env var or default local → emulator at `https://localhost:8081`
- `WEBSITE_SITE_NAME` env var set → Azure CosmosDB via Managed Identity

---

## Outer Loop: Azure Story

### Azure Resource

**Type:** Azure Cosmos DB for NoSQL account

**Name:** `cosmos-catan` (consistent with `catan-azure.json` naming convention)

**Region:** `westus2` (same as other resources)

**Authentication:** Managed Identity (same pattern as current Azure SQL Managed Identity auth).
No connection strings with passwords are stored anywhere.

**Cost:** The free tier provides 1000 RU/s and 25 GB storage at no charge — sufficient for
this application's workload.

### Changes to `.scripts/catan-azure.ps1`

| Command | Current | New |
|---------|---------|-----|
| `database install` | Create Azure SQL Server + Serverless DB | Create CosmosDB account, database, and containers |
| `database deploy` | Set SQL connection string; grant managed identity SQL roles | Set CosmosDB endpoint in App Service settings; assign CosmosDB Data Contributor RBAC role |
| `database doctor` | Check SQL Server, DB, firewall, managed identity | Check CosmosDB account, database, containers, RBAC assignment, App Service config |
| `database clean` | Delete SQL Server and DB | Delete CosmosDB account |
| `database deploy-staging-access` | Grant staging slot SQL access | Grant staging slot CosmosDB RBAC |

### Changes to `.azure/catan-azure.json`

Add a `cosmosDb` block and remove the `sqlServer` block:

```json
{
  "cosmosDb": {
    "accountName": "cosmos-catan",
    "databaseName": "catan",
    "endpoint": "https://cosmos-catan.documents.azure.com:443/"
  }
}
```

### Changes to GitHub Actions

`deploy-azure.yml` database steps replace `az sql ...` with `az cosmosdb ...` commands.
The `database deploy` step assigns the `Cosmos DB Built-in Data Contributor` role to the
App Service managed identity instead of executing SQL DDL via `Invoke-Sqlcmd`.

---

## Migration Phases

### Phase 1 — Abstraction Layer (No Behavior Change)

**Goal:** Introduce `ICatanDb` over the existing EF Core stack. All tests pass; SQLite still works
locally; Azure SQL still works in production. Nothing changes at runtime.

**Steps:**

1. Define `ICatanDb` interface (`Catan3.GameService/Abstractions/ICatanDb.cs`)
2. Identify DTO types for the interface boundary — reuse existing shared types where the shape
   matches; only create new persistence-facing types where there is a real mismatch, and give them
   names that do not collide with existing types in `Catan3.Shared`:
   - `GameTemplateSummary` — already exists in `Catan3.Shared/Models/GameTemplateData.cs`; reuse it
   - `RecordingSummary` — exists in `Catan3.Shared/Services/GameServiceProxy.cs` but **does not
     have a `GameId` property**; must add `GameId` to the existing type (this is an additive,
     backward-compatible change — existing callers ignore unknown properties)
   - `GameSummary`, `GameSaveData` — no shared equivalents; create in
     `Catan3.GameService/Abstractions/` with names that do not shadow anything in `Catan3.Shared`
   - `CompletedGame` — no shared equivalent; create as `CompletedGameRecord` to avoid any future
     collision
3. Implement `EfCoreCatanDb : ICatanDb` wrapping `CatanDbContext`
   (`Catan3.GameService/Abstractions/EfCoreCatanDb.cs`)
4. Refactor `GameApiController` to inject `ICatanDb` instead of `CatanDbContext`
5. Refactor `StatsController` to inject `ICatanDb`
6. Refactor `GameTemplateService` to use `ICatanDb`
7. Refactor `RecordingService` to use `ICatanDb`; remove the `IServiceScopeFactory` pattern
   (scope factory was only needed to work around DbContext lifetime — ICatanDb removes that need)
8. Refactor the two persistence services:
   - `GamePersistenceService` (scoped) — constructor-inject `ICatanDb` directly; straightforward swap
   - `DatabaseBackedPersistenceService` (singleton, `AddSingleton<IPersistenceService>`) —
     because it is singleton it cannot directly inject scoped `ICatanDb`; it must keep its
     `IServiceScopeFactory` permanently. In Phase 1 resolve `ICatanDb` from the scope instead
     of `CatanDbContext`. In Phase 2 this does not change — the scope factory stays regardless
     of `CosmosClient` being singleton, because `DatabaseBackedPersistenceService` itself remains
     singleton. The only way to remove the scope factory is to change `IPersistenceService` to
     scoped, which is a separate decision (see Open Questions).
9. Refactor `DatabaseSeedingService` to resolve `ICatanDb` from scope instead of `CatanDbContext`
10. Rewrite the three inline endpoint handlers in `Program.cs` (lines 221, 316, 404) to use `ICatanDb`
11. Register `ICatanDb → EfCoreCatanDb` as scoped in `Program.cs`
12. Verify: `pwsh ./catan.ps1 test` — all tests pass

**Gate before Phase 2:** Deploy to production; verify no regressions.

### Phase 2 — CosmosDB Implementation

**Goal:** Write `CosmosCatanDb`, switch the provider, update scripts.

**Steps:**

1. Add NuGet: `Microsoft.Azure.Cosmos` to `Catan3.GameService.csproj`
2. Implement `CosmosCatanDb : ICatanDb` (`Catan3.GameService/Abstractions/CosmosCatanDb.cs`);
   register `CosmosClient` as singleton (thread-safe, designed as singleton) and `CosmosCatanDb`
   as scoped (lightweight wrapper over the singleton client; consistent with `EfCoreCatanDb` lifetime)
3. Update `DatabaseProviderDetector` (or create `ICatanDbFactory`) to return
   `EfCoreCatanDb` or `CosmosCatanDb` based on environment
4. Move seeding logic into `CosmosCatanDb.InitializeAsync()` — all four private seeder methods
   (`SeedPlayersAsync`, `SeedGamesAsync`, `SeedRecordingsAsync`, `SeedTemplatesAsync`) currently
   take `CatanDbContext` directly and must be rewritten against `ICatanDb`. The cleanest approach
   is to move them inside the `CosmosCatanDb` implementation so seeding is co-located with
   container creation. `DatabaseSeeder` becomes a thin shell that calls `ICatanDb.InitializeAsync()`.
5. Redesign or delete `AzureSqlDiagnosticService` — replace with `CosmosDbDiagnosticService`
   that checks endpoint reachability, RBAC assignment, and container health; or delete entirely
   if the `catan-azure.ps1` doctor command covers those checks externally.
6. Update `Program.cs` — remove `AddDbContextPool`, add `CosmosClient` singleton
7. Write data migration utility (`database migrate` verb in `catan.ps1`): export SQLite → CosmosDB
   and Azure SQL → CosmosDB (see Production Migration Track below)
8. Update `catan.ps1` (emulator start/stop/install/doctor/clean verbs)
9. Update `.scripts/catan-azure.ps1` (CosmosDB account management)
10. Update `.azure/catan-azure.json` (add cosmosDb block, remove sqlServer block)
11. Update GitHub Actions workflows
12. Verify: local emulator + `pwsh ./catan.ps1 test`; then Azure deploy + smoke test

**Gate:** Production migration complete (see below); run parallel for one session; cut over;
remove EF Core SQL provider.

### Production Migration Track

The authoritative production data lives in **Azure SQL Serverless**, not SQLite. The
`database migrate` utility must handle both sources. The production cutover sequence is:

1. **Export** — dump all Azure SQL tables to JSON files using the `database migrate export-sql`
   subcommand (connects to Azure SQL, serializes each entity to the CosmosDB document shape)
2. **Transform** — map SQL rows to CosmosDB documents: flatten the two-table game save, embed
   images in player documents, add explicit `gameId` field to recordings, convert int keys to
   string ids
3. **Import into staging** — load transformed documents into a staging CosmosDB account or a
   separate database (`catan-migration`) using `database migrate import-cosmos --target staging`
4. **Validate** — compare row/document counts per container; spot-check representative payload
   hashes; run `pwsh ./catan.ps1 database doctor` against the staging CosmosDB
5. **Cutover rehearsal** — run a full session against the staging CosmosDB; verify game play,
   template loading, player management end-to-end
6. **Production cutover** — stop writes to Azure SQL (take app offline briefly or put in
   maintenance mode); re-run export/import against production CosmosDB; verify; switch
   `COSMOS_ENDPOINT` App Service setting; restart; run doctor

**Rollback criteria:** if doctor fails or any data validation check fails at step 4 or 6,
restore the previous Azure SQL configuration and restart:

1. Restore `DATABASE_PROVIDER` App Service setting to `SqlServer` (or remove it if it was
   absent and Azure detection was the trigger)
2. Restore `ConnectionStrings:AzureSql` App Service setting to the original SQL connection string
3. Remove or ignore `COSMOS_ENDPOINT` / `COSMOS_KEY` settings (harmless if present but not read)
4. Restart the App Service and run `pwsh ./catan.ps1 azure doctor`

Azure SQL remains intact and unmodified throughout the migration. Do not drop or truncate Azure
SQL data until after a successful parallel-run period and explicit sign-off.

---

## Start/Launch Model Changes

### Current Flow

```text
Program.cs startup:
  DatabaseProviderDetector → detects SQLite or SQL Server
  → AddDbContext<CatanDbContext> (SQLite) or AddDbContextPool (SQL Server)
  → DatabaseSeedingService (IHostedService) → DatabaseSeeder.SeedAsync()
     → context.Database.EnsureCreatedAsync() or MigrateAsync()
     → seed players, games, templates, recordings
```

### Proposed Flow

```text
Program.cs startup:
  ICatanDbFactory.Create() → detects emulator, Azure CosmosDB, or EfCore fallback
  → CosmosClient registered as singleton; ICatanDb (CosmosCatanDb or EfCoreCatanDb) as scoped
  → DatabaseSeedingService (IHostedService) → ICatanDb.InitializeAsync()
     → create containers if absent (CosmosDB) or EnsureCreated (EF Core fallback)
     → seed players, templates, recordings if counts are zero

catan.ps1 run:
  1. Check/start CosmosDB Emulator (new step)
  2. pwsh ./catan.ps1 build
  3. Start GameService (emulator endpoint in env vars)
  4. Start React UI
```

### Environment Variables

| Variable | Value | Meaning |
|----------|-------|---------|
| `COSMOS_ENDPOINT` | `https://localhost:8081` | Emulator (local) |
| `COSMOS_ENDPOINT` | `https://cosmos-catan.documents.azure.com:443/` | Real account |
| `COSMOS_KEY` | emulator fixed key | Local only; absent in Azure (uses Managed Identity) |
| `WEBSITE_SITE_NAME` | set by Azure App Service | Triggers Managed Identity auth |

### Local Secret Management

`COSMOS_KEY` for the local emulator must go in `appsettings.Development.json` (already
`.gitignore`d via `*.Development.json`). It must never appear in `appsettings.json` or any
committed file. The well-known emulator key is not a secret in the traditional sense — it is
published in Microsoft documentation and is identical on every developer machine — but keeping
it out of source control preserves the habit and prevents the pattern from being copied to
production configuration by accident.

---

## Files to Create or Modify

### New Files

| File | Purpose |
|------|---------|
| `.design/cosmos-migration.md` | This document |
| `Catan3.GameService/Abstractions/ICatanDb.cs` | Domain-specific DB interface |
| `Catan3.GameService/Abstractions/EfCoreCatanDb.cs` | Phase 1: EF Core implementation |
| `Catan3.GameService/Abstractions/CosmosCatanDb.cs` | Phase 2: CosmosDB implementation |
| `Catan3.GameService/Abstractions/ICatanDbFactory.cs` | Selects implementation by environment |

### Modified Files

| File | Change |
|------|--------|
| `Catan3.GameService/Controllers/GameApiController.cs` | Inject `ICatanDb`; remove `CatanDbContext` |
| `Catan3.GameService/Controllers/StatsController.cs` | Inject `ICatanDb`; remove `CatanDbContext` |
| `Catan3.GameService/Services/GameTemplateService.cs` | Use `ICatanDb`; remove `CatanDbContext` |
| `Catan3.GameService/Services/RecordingService.cs` | Use `ICatanDb`; remove scope factory pattern |
| `Catan3.GameService/Services/DatabasePersistenceService.cs` | Delegate to `ICatanDb` |
| `Catan3.GameService/Services/DatabaseSeedingService.cs` | Resolve `ICatanDb` instead of `CatanDbContext` |
| `Catan3.GameService/Services/AzureSqlDiagnosticService.cs` | Redesign as `CosmosDbDiagnosticService` or delete |
| `Catan3.GameService/Data/DatabaseProviderDetector.cs` | Add CosmosDB / emulator detection |
| `Catan3.GameService/Data/DatabaseSeeder.cs` | Thin shell calling `ICatanDb.InitializeAsync()`; seeding moves inside impl |
| `Catan3.GameService/Program.cs` | Register `ICatanDb`; rewrite inline handlers (lines 221, 316, 404); add `CosmosClient` singleton (Phase 2) |
| `catan.ps1` | Emulator start/stop/install/doctor verbs; Docker prereq |
| `.scripts/catan-azure.ps1` | Replace SQL commands with CosmosDB commands |
| `.azure/catan-azure.json` | Add `cosmosDb` block; remove `sqlServer` block (Phase 2) |
| `.github/workflows/deploy-azure.yml` | Update database steps |
| `.github/workflows/deploy-staging.yml` | Update database steps |
| `Catan3.GameService/Catan3.GameService.csproj` | Add `Microsoft.Azure.Cosmos` (Phase 2) |

---

## Open Questions

1. **Data migration utility** — implemented as `pwsh ./catan.ps1 database migrate` (decided:
   `catan.ps1` owns the database lifecycle end-to-end; a standalone script is harder to discover).

2. **EF Core fallback after Phase 2** — given the ARM64 emulator constraints, keep `EfCoreCatanDb`
   registered when neither `COSMOS_ENDPOINT` nor `COSMOS_EMULATOR` is set. This preserves a
   zero-Docker inner loop for developers who cannot run the emulator. Mark it deprecated but
   functional; remove only after ARM64 emulator support is confirmed stable.

3. **RU capacity and seeding bursts** — game play at ~5 concurrent players generates well under
   100 RU/s. The risk is the `database install` seeding step: bulk inserting recordings and games
   can exhaust free-tier RU/s (1000 RU/s) transiently. Add inter-document delays or batching in
   the seeder. Note: only **one free-tier CosmosDB account is permitted per Azure subscription**.
   If the subscription already has a free-tier account for another project, this deployment
   requires a paid account (~$25/month minimum for provisioned throughput).

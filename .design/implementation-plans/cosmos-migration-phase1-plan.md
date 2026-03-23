# Implementation Plan: CosmosDB Migration

## Goal

Implement `ICatanDb` directly against Azure Cosmos DB for NoSQL, validate it with contract
tests against both the local emulator and the real Azure account, then wire up the GameService
in a single cutover. The GameService is **not touched** until all contract tests pass.

**Revised strategy vs. original two-phase plan:**

- Skip `EfCoreCatanDb` as a production implementation — no throwaway wrapper code
- `CosmosCatanDb` is the only implementation; contract tests validate it end-to-end
- One production deploy instead of two
- GameService stays on EF Core / SQLite throughout development; nothing breaks

---

## Pre-Flight

```bash
git checkout -b feat/cosmos-migration
pwsh ./catan.ps1 build
pwsh ./catan.ps1 test    # baseline: 76 passing — must stay green throughout
docker --version         # Docker required for local emulator
```

---

## Changes

### 1. Add `GameId` to `RecordingSummary` in `Catan3.Shared`

**File:** `Catan3.Shared/Services/GameServiceProxy.cs`

Add one property to the existing nested `RecordingSummary` class (around line 1041):

```csharp
public string GameId { get; set; } = string.Empty;
```

Additive and backward-compatible. Existing JSON deserializers ignore unknown properties;
existing callers that do not use `GameId` are unaffected.

---

### 2. Create `ICatanDb` Interface and DTOs

**File:** `Catan3.GameService/Abstractions/ICatanDb.cs` *(new)*

```csharp
using Catan3.Shared.Models;
using Catan3.Shared.Services;

namespace Catan3.GameService.Abstractions;

public interface ICatanDb
{
    Task InitializeAsync();

    Task<IReadOnlyList<PlayerProfile>> LoadPlayersAsync();
    Task<PlayerProfile?> LoadPlayerAsync(string id);
    Task SavePlayerAsync(PlayerProfile player);
    Task DeletePlayerAsync(string id);

    Task<(byte[] Data, string ContentType)?> LoadImageAsync(string playerId);
    Task SaveImageAsync(string playerId, byte[] data, string contentType);
    Task DeleteImageAsync(string playerId);

    Task<IReadOnlyList<GameSummary>> ListGamesAsync(string? startedBy = null);
    Task<GameSaveData?> LoadGameAsync(string gameId);
    Task SaveGameAsync(GameSaveData game);
    Task DeleteGameAsync(string gameId);
    Task<int> CountGamesAsync();

    Task SaveCompletedGameAsync(CompletedGameRecord game);
    Task<IReadOnlyList<CompletedGameRecord>> ListCompletedGamesAsync();

    Task<IReadOnlyList<GameTemplateSummary>> ListTemplatesAsync(string? category = null);
    Task<GameTemplateData?> LoadTemplateAsync(string id);
    Task SaveTemplateAsync(
        string id, string name, string category,
        bool isSystemTemplate, GameTemplateData data);
    Task DeleteTemplateAsync(string id);

    Task<IReadOnlyList<GameServiceProxy.RecordingSummary>> ListRecordingsAsync();
    Task<(GameServiceProxy.RecordingSummary Summary, string Data)?> LoadRecordingAsync(string id);
    Task<(GameServiceProxy.RecordingSummary Summary, string Data)?> FindRecordingByGameIdAsync(
        string gameId);
    Task SaveRecordingAsync(GameServiceProxy.RecordingSummary summary, string data);
    Task DeleteRecordingAsync(string id);
    Task DeleteRecordingsByGameIdAsync(string gameId);
}
```

**File:** `Catan3.GameService/Abstractions/GameSaveData.cs` *(new)*

```csharp
namespace Catan3.GameService.Abstractions;

public class GameSaveData
{
    public string GameId { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string GameState { get; set; } = string.Empty;
    public string GameType { get; set; } = string.Empty;
    public string StartedBy { get; set; } = string.Empty;
    public int PlayerCount { get; set; }
    public string PlayerNames { get; set; } = string.Empty;
    public int TurnCount { get; set; }
    public DateTime SavedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] CompressedData { get; set; } = [];
    public int Size { get; set; }
}

public class GameSummary
{
    public string GameId { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string GameState { get; set; } = string.Empty;
    public string GameType { get; set; } = string.Empty;
    public string StartedBy { get; set; } = string.Empty;
    public int PlayerCount { get; set; }
    public string PlayerNames { get; set; } = string.Empty;
    public int TurnCount { get; set; }
    public DateTime SavedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Size { get; set; }
}
```

**File:** `Catan3.GameService/Abstractions/CompletedGameRecord.cs` *(new)*

```csharp
namespace Catan3.GameService.Abstractions;

public class CompletedGameRecord
{
    public string GameId { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string WinnerId { get; set; } = string.Empty;
    public string WinnerName { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; }
    public DateTime StartedAt { get; set; }
    public int PlayerCount { get; set; }
    public int TurnCount { get; set; }
    public string PlayerNames { get; set; } = string.Empty;
    public byte[] CompressedData { get; set; } = [];
    public int Size { get; set; }
}
```

---

### 3. Add NuGet Package

**File:** `Catan3.GameService/Catan3.GameService.csproj`

```xml
<PackageReference Include="Microsoft.Azure.Cosmos" Version="3.*" />
```

`CosmosClient` is thread-safe and designed as a singleton. Register it in DI as singleton;
`CosmosCatanDb` itself registers as scoped (lightweight wrapper).

---

### 4. Implement `CosmosCatanDb`

**File:** `Catan3.GameService/Abstractions/CosmosCatanDb.cs` *(new)*

Constructor injects `CosmosClient` (singleton) and `IConfiguration`. All `ICatanDb` methods
are implemented here. Key design points:

**`InitializeAsync()`:**

- Creates database `catan` if absent
- Creates all 5 containers if absent: `players`, `games`, `completed-games`,
  `templates`, `recordings`
- Seeds default players, templates, and recordings if their containers are empty
- Seeding logic migrated from `DatabaseSeeder` private static methods, rewritten
  to call `ICatanDb` save methods rather than `CatanDbContext` directly

**Container / document mapping:**

| Container | Partition key | Document `id` | Notes |
|-----------|---------------|---------------|-------|
| `players` | `/id` | Player ID (e.g. `"Joe-001"`) | Profile + base64 image embedded |
| `games` | `/id` | `gameId` | Metadata + `CompressedData` as base64 |
| `completed-games` | `/id` | `gameId` | All archive fields |
| `templates` | `/id` | Template ID (e.g. `"regular"`) | `GameTemplateData` JSON embedded |
| `recordings` | `/id` | GUID | `gameId` stored as first-class field |

**Authentication (detected at runtime):**

```csharp
// Local emulator: key from config
// Azure (WEBSITE_SITE_NAME set): DefaultAzureCredential (Managed Identity)
CosmosClient client = isAzure
    ? new CosmosClient(endpoint, new DefaultAzureCredential())
    : new CosmosClient(endpoint, key, new CosmosClientOptions
      {
          HttpClientFactory = () => new HttpClient(
              new HttpClientHandler { ServerCertificateCustomValidationCallback =
                  (_, _, _, _) => true }),  // emulator self-signed cert
          ConnectionMode = ConnectionMode.Gateway
      });
```

**Image storage:** base64-encoded, embedded in the player document under an `image` property.
Not a separate container — player + image are one atomic document.

**`gameId` for recordings:** stored as a top-level `gameId` property on the recording
document. `FindRecordingByGameIdAsync` queries `SELECT * FROM c WHERE c.gameId = @id`.

---

### 5. Update `DatabaseProviderDetector` (or replace with `ICatanDbFactory`)

**File:** `Catan3.GameService/Data/DatabaseProviderDetector.cs`

Extend to detect CosmosDB configuration:

- `COSMOS_ENDPOINT` env var set → CosmosDB mode
- `WEBSITE_SITE_NAME` set (Azure App Service) → Azure CosmosDB with Managed Identity
- Neither → remain on SQLite / EF Core (existing behaviour, unchanged for now)

Alternatively, create `ICatanDbFactory` as a new class that encapsulates this logic and
returns either `CosmosCatanDb` (when Cosmos is configured) or falls through to EF Core
(existing path).

---

### 6. Register `CosmosCatanDb` in `Program.cs`

**File:** `Catan3.GameService/Program.cs`

```csharp
if (/* cosmos endpoint configured */)
{
    builder.Services.AddSingleton<CosmosClient>(sp => /* build client */);
    builder.Services.AddScoped<ICatanDb, CosmosCatanDb>();
}
// else: ICatanDb not yet registered — GameService still uses CatanDbContext directly
```

`ICatanDb` registration is additive in Phase 1 of wiring — it does not yet replace
`CatanDbContext` in controllers. That happens as a separate step once tests pass.

---

### 7. Add Contract Test Suite

**New files:**

- `Tests/GameService/CatanDb/CatanDbContractTests.cs` — abstract base (26 tests)
- `Tests/GameService/CatanDb/CosmosCatanDbTests.cs` — concrete runner (reads from env)

#### How test targeting works

xunit has no native `TestContext.Properties` mechanism (that is MSTest-only). Instead,
`catan.ps1` writes a small JSON params file immediately before calling `dotnet test` and
deletes it after. A static helper class reads the file once per test session. No environment
variables are touched at any point.

**Params file location:** `Tests/GameService/CatanDb/.cosmos-test-params.json` (gitignored)

**File shape written by `catan.ps1` (local emulator):**

```json
{
  "endpoint": "https://localhost:8081",
  "key": "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw=="
}
```

**File shape written by `catan.ps1` (Azure — no key, uses `DefaultAzureCredential`):**

```json
{
  "endpoint": "https://cosmos-catan.documents.azure.com:443/"
}
```

**`CosmosTestParams.cs`** — static helper, read once per process:

```csharp
// File: Tests/GameService/CatanDb/CosmosTestParams.cs
internal static class CosmosTestParams
{
    private static readonly Lazy<(string Endpoint, string? Key)> _params =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static string Endpoint => _params.Value.Endpoint;
    public static string? Key     => _params.Value.Key;

    private static (string Endpoint, string? Key) Load()
    {
        // Params file sits next to the test assembly after build
        var dir  = Path.GetDirectoryName(typeof(CosmosTestParams).Assembly.Location)!;
        var path = Path.Combine(dir, ".cosmos-test-params.json");

        if (!File.Exists(path))
        {
            // No file → default to local emulator with well-known key
            return ("https://localhost:8081",
                "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw==");
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        var endpoint = root.GetProperty("endpoint").GetString()!;
        var key = root.TryGetProperty("key", out var k) ? k.GetString() : null;
        return (endpoint, key);
    }
}
```

**`CosmosCatanDbTests.cs`** — reads params, creates a unique database per test run:

```csharp
public class CosmosCatanDbTests : CatanDbContractTests
{
    // Unique database name per test run prevents cross-run interference
    private readonly string _dbName = $"catan-test-{Guid.NewGuid():N}";
    private CosmosClient? _client;

    protected override async Task<ICatanDb> CreateDbAsync()
    {
        var endpoint = CosmosTestParams.Endpoint;
        var key      = CosmosTestParams.Key;

        _client = string.IsNullOrEmpty(key)
            ? new CosmosClient(endpoint, new DefaultAzureCredential())
            : new CosmosClient(endpoint, key, new CosmosClientOptions
              {
                  // Accept emulator self-signed certificate
                  HttpClientFactory = () => new HttpClient(
                      new HttpClientHandler
                      {
                          ServerCertificateCustomValidationCallback =
                              HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                      }),
                  ConnectionMode = ConnectionMode.Gateway
              });

        var db = new CosmosCatanDb(_client, _dbName);
        await db.InitializeAsync();
        return db;
    }

    // Called by IAsyncLifetime after every test class
    public override async Task DisposeAsync()
    {
        if (_client is not null)
        {
            // Delete the ephemeral test database to leave emulator/Azure clean
            await _client.GetDatabase(_dbName).DeleteAsync();
            _client.Dispose();
        }
    }
}
```

**`catan.ps1` — write params file, run tests, delete params file:**

```powershell
$EmulatorKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw=="
$ParamsFile  = "Tests/GameService/CatanDb/.cosmos-test-params.json"

function Invoke-CatanDbTests {
    param([switch]$Azure)

    $params = if ($Azure) {
        @{ endpoint = $config.cosmosDb.endpoint }   # no key → DefaultAzureCredential
    } else {
        Start-CosmosEmulator
        @{ endpoint = "https://localhost:8081"; key = $EmulatorKey }
    }

    # Write params file next to source; dotnet copies it to output dir on build
    $params | ConvertTo-Json | Set-Content $ParamsFile

    try {
        dotnet test Tests/GameService --filter "FullyQualifiedName~CatanDb" `
            --logger "console;verbosity=normal"
    } finally {
        Remove-Item $ParamsFile -ErrorAction SilentlyContinue
    }
}
```

The `finally` block ensures the params file is always deleted even if tests fail.
`dotnet test` copies the file to the build output directory via an `<EmbeddedResource>`
or `<Content CopyToOutputDirectory="PreserveNewest">` entry in the test `.csproj`.

**`.gitignore` entry:**

```text
Tests/GameService/CatanDb/.cosmos-test-params.json
```

The same compiled binary runs against either target — no recompilation, no separate
test projects, no persistent state between runs.

Each test run uses a unique database name (`catan-test-<guid>`). After the test run, the
database is deleted in `DisposeAsync()`. This ensures test isolation without shared state
between parallel runs, and avoids polluting the emulator or Azure account with leftover data.

#### `CatanDbContractTests` — abstract base

```csharp
public abstract class CatanDbContractTests : IAsyncLifetime
{
    protected ICatanDb Db { get; private set; } = null!;
    protected abstract Task<ICatanDb> CreateDbAsync();

    public async Task InitializeAsync() => Db = await CreateDbAsync();
    public async Task DisposeAsync() => await Db.DeleteDatabaseAsync();
    // (DeleteDatabaseAsync added to ICatanDb or handled by the concrete class)

    // Players
    [Fact] public async Task SaveAndLoadPlayer_RoundTrips();
    [Fact] public async Task LoadPlayersAsync_ReturnsAll();
    [Fact] public async Task DeletePlayerAsync_RemovesRecord();
    [Fact] public async Task LoadPlayerAsync_UnknownId_ReturnsNull();

    // Images
    [Fact] public async Task SaveAndLoadImage_RoundTrips();
    [Fact] public async Task DeleteImageAsync_RemovesRecord();
    [Fact] public async Task LoadImageAsync_UnknownId_ReturnsNull();

    // Game saves
    [Fact] public async Task SaveAndLoadGame_RoundTrips();
    [Fact] public async Task ListGamesAsync_NoFilter_ReturnsAll();
    [Fact] public async Task ListGamesAsync_WithFilter_FiltersCorrectly();
    [Fact] public async Task ListGamesAsync_IncludesGameOverGames();
    [Fact] public async Task SaveGameAsync_UpdatesExisting();
    [Fact] public async Task DeleteGameAsync_RemovesRecord();
    [Fact] public async Task CountGamesAsync_ReturnsCorrectCount();
    [Fact] public async Task ListGamesAsync_StartsWith_SupportsCopyNaming();
    // Verifies ListGamesAsync can be filtered client-side for copy-name generation.

    // Completed games
    [Fact] public async Task SaveAndListCompletedGames_RoundTrips();

    // Templates
    [Fact] public async Task SaveAndLoadTemplate_RoundTrips();
    [Fact] public async Task ListTemplatesAsync_NoFilter_ReturnsAll();
    [Fact] public async Task ListTemplatesAsync_WithCategory_Filters();
    [Fact] public async Task DeleteTemplateAsync_RemovesRecord();
    [Fact] public async Task LoadTemplateAsync_UnknownId_ReturnsNull();

    // Recordings
    [Fact] public async Task SaveAndLoadRecording_RoundTrips();
    [Fact] public async Task FindRecordingByGameIdAsync_FindsCorrectRecord();
    [Fact] public async Task FindRecordingByGameIdAsync_UnknownGameId_ReturnsNull();
    [Fact] public async Task DeleteRecordingsByGameIdAsync_RemovesAllMatching();
    [Fact] public async Task ListRecordingsAsync_ReturnsAll();
    [Fact] public async Task RecordingSummary_GameId_IsStoredField_NotParsedFromJson();
}
```

---

### 8. Update `catan.ps1` — Database and Test Commands

#### Database commands

| Command | Behavior |
|---------|----------|
| `./catan.ps1 database install` | Pull emulator image, start container, call `InitializeAsync()` to create containers + seed |
| `./catan.ps1 database install -Local` | Same (explicit) |
| `./catan.ps1 database doctor` | Check emulator running, all 5 containers exist, player/template counts non-zero |
| `./catan.ps1 database doctor -Local` | Same (explicit) |
| `./catan.ps1 database clean` | Stop and remove emulator container (data wiped) |
| `./catan.ps1 database stop` | Stop emulator container without removing data |
| `./catan.ps1 azure database install` | Create CosmosDB account, database, containers |
| `./catan.ps1 azure database deploy` | Set endpoint in App Service; assign Cosmos RBAC to managed identity |
| `./catan.ps1 azure database doctor` | Check account, containers, RBAC, App Service config |

**Emulator start snippet (in `catan.ps1`):**

```powershell
function Start-CosmosEmulator {
    $running = docker ps --filter "name=cosmos-emulator" --format "{{.Names}}"
    if ($running -ne "cosmos-emulator") {
        Write-Host "Starting CosmosDB emulator..."
        $platform = if ($IsLinux -or $IsMacOS) { "--platform linux/amd64" } else { "" }
        docker run -d $platform -p 8081:8081 -p 10251-10254:10251-10254 `
            --name cosmos-emulator `
            mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview
        # Wait for emulator ready
        $timeout = 60
        do {
            Start-Sleep 2; $timeout -= 2
            $ready = try { Invoke-WebRequest -Uri "https://localhost:8081/_explorer/index.html" `
                -SkipCertificateCheck -TimeoutSec 2 -UseBasicParsing; $true } catch { $false }
        } while (-not $ready -and $timeout -gt 0)
        if (-not $ready) { throw "CosmosDB emulator failed to start" }
    }
}
```

#### Test commands

| Command | Endpoint passed to tests | Key passed | Auth |
|---------|--------------------------|------------|------|
| `./catan.ps1 test` | `https://localhost:8081` | Emulator fixed key | Emulator (default) |
| `./catan.ps1 test -Local` | `https://localhost:8081` | Emulator fixed key | Emulator (explicit) |
| `./catan.ps1 test -Azure` | Value from `catan-azure.json` | none | `DefaultAzureCredential` |
| `./catan.ps1 azure test` | Value from `catan-azure.json` | none | `DefaultAzureCredential` (alias) |

Both endpoint and key are passed as `dotnet test` run parameters — not environment variables.

**Test invocation in `catan.ps1`:**

```powershell
$EmulatorKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw=="

function Invoke-Tests {
    param([switch]$Azure, [switch]$Local)
    if ($Azure) {
        $endpoint = $config.cosmosDb.endpoint
        # Azure uses Managed Identity — no key parameter passed
        dotnet test Tests/GameService `
            "--" "TestRunParameters.Parameter(name=`"CosmosEndpoint`",value=`"$endpoint`")"
    } else {
        Start-CosmosEmulator
        dotnet test Tests/GameService `
            "--" "TestRunParameters.Parameter(name=`"CosmosEndpoint`",value=`"https://localhost:8081`")" `
                 "TestRunParameters.Parameter(name=`"CosmosKey`",value=`"$EmulatorKey`")"
    }
}
```

No environment variables are touched. Parameters are explicit, per-invocation, and
visible in the shell history.

The existing 76 tests keep running unchanged. The CosmosDB contract tests are additive.

---

### 9. Wire Up GameService (after all contract tests pass)

This step happens only after `./catan.ps1 test -Local` and `./catan.ps1 azure test` both
show all contract tests green.

**Files to update (13 files — same list as before, now a single cutover):**

| File | Change |
|------|--------|
| `Catan3.GameService/Controllers/GameApiController.cs` | Inject `ICatanDb`; update `EnsureGameLoadedAsync` + `GenerateCopyNameAsync`; remove SQL DDL endpoint (lines 2249–2572); add `POST /api/database/initialize` |
| `Catan3.GameService/Controllers/StatsController.cs` | Inject `ICatanDb` |
| `Catan3.GameService/Controllers/RecordingController.cs` | Inject `ICatanDb`; remove `ExtractGameIdFromData` |
| `Catan3.GameService/Services/GameTemplateService.cs` | Inject `ICatanDb` |
| `Catan3.GameService/Services/RecordingService.cs` | Resolve `ICatanDb` from scope; remove `String.Contains` query |
| `Catan3.GameService/Services/DatabasePersistenceService.cs` | Inject/resolve `ICatanDb` |
| `Catan3.GameService/Services/DatabaseSeedingService.cs` | Resolve `ICatanDb`; call `InitializeAsync()` |
| `Catan3.GameService/Services/AzureSqlDiagnosticService.cs` | Delete or replace with `CosmosDbDiagnosticService` |
| `Catan3.GameService/Program.cs` | Register `ICatanDb → CosmosCatanDb` as default; rewrite three inline handlers (lines 221, 316, 404); remove `AddDbContextPool` |
| `catan.ps1` | Remove SQLite doctor/install logic; update `run` verb to start emulator first |
| `.scripts/catan-azure.ps1` | Replace `az sql` commands with `az cosmosdb` commands |
| `.azure/catan-azure.json` | Add `cosmosDb` block; remove `sqlServer` block |
| `.github/workflows/deploy-azure.yml` | Update database steps |

**`CopyGame` and `ReplayGame` already use `IGamePersistence.SaveAsync()` — no changes needed.**
`CloseGame` has no DB operations. `EnsureGameLoadedAsync` maps to `_db.LoadGameAsync(gameId)`.
`GenerateCopyNameAsync` uses `(await _db.ListGamesAsync()).Where(g => g.GameName.StartsWith(...))`.

Note: `Catan3.CLI/Commands/DbExportCommand.cs` uses raw `SqliteConnection` — separate concern,
addressed after wiring (provide a `db export --cosmos` path or rewrite against `ICatanDb`).

---

## Files Modified

### Now (steps 1–8 — GameService untouched)

| File | Change |
|------|--------|
| `Catan3.Shared/Services/GameServiceProxy.cs` | Add `GameId` to `RecordingSummary` |
| `Catan3.GameService/Abstractions/ICatanDb.cs` | **New** — domain interface |
| `Catan3.GameService/Abstractions/GameSaveData.cs` | **New** — `GameSaveData` + `GameSummary` DTOs |
| `Catan3.GameService/Abstractions/CompletedGameRecord.cs` | **New** — `CompletedGameRecord` DTO |
| `Catan3.GameService/Abstractions/CosmosCatanDb.cs` | **New** — CosmosDB implementation |
| `Catan3.GameService/Catan3.GameService.csproj` | Add `Microsoft.Azure.Cosmos` NuGet |
| `Tests/GameService/CatanDb/CatanDbContractTests.cs` | **New** — abstract contract tests (26 tests) |
| `Tests/GameService/CatanDb/CosmosCatanDbTests.cs` | **New** — emulator + Azure runner |
| `catan.ps1` | Add emulator start/stop/doctor; add `-Local`/`-Azure` test flags |

### Later (step 9 — GameService cutover, after tests pass)

13 files listed in step 9 above.

---

## Verification

```bash
# 1. Baseline — existing tests must stay green throughout
pwsh ./catan.ps1 test -Local

# 2. Contract tests against emulator (primary dev loop)
dotnet test Tests/GameService --filter "FullyQualifiedName~CatanDb" --logger "console;verbosity=normal"

# 3. Contract tests against real Azure (before production cutover)
pwsh ./catan.ps1 test -Azure

# 4. After step 9 (GameService wiring) — full smoke test
pwsh ./catan.ps1 run
# - Load players, create game, play turns, save
# - Copy game, replay game, close game
# - Load game list (verify GameOver games visible)
# - Start/stop recording
# - Load templates

# 5. Deploy and verify
pwsh ./catan.ps1 azure deploy
pwsh ./catan.ps1 azure doctor
```

**Gate for step 9:** `./catan.ps1 test -Local` and `./catan.ps1 azure test` both pass all
contract tests with zero failures.

**Gate for production deploy:** step 9 wiring complete, `pwsh ./catan.ps1 test -Local` still
shows all 76 + 26 tests passing, smoke test clean.

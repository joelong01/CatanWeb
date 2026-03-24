# Implementation Plan: CosmosDB Migration (Updated 2026-03-23)

## Goal

Implement `ICatanDb` against Azure Cosmos DB for NoSQL, validate with contract tests, then
wire into GameService in a single cutover. GameService stays on EF Core / SQLite until all
contract tests pass.

---

## Completed Work

### Infrastructure (done)

- `ICatanDb` interface + DTOs (`GameSaveData`, `GameSummary`, `CompletedGameRecord`)
- `CosmosCatanDb` implementation with all 5 containers
- 26 contract tests (`CatanDbContractTests` + `CosmosCatanDbTests`)
- `.scripts/database.ps1` — full lifecycle: install, deploy, seed, doctor, test, nuke-containers
- Azure account provisioned: `cosmos-catan` with AAD auth (`disableLocalAuth=true`)
- Docker compose for local emulator (`vnext-preview`)
- All 7 players + 5 recordings successfully seeded to Azure

### Key Decisions Made During Implementation

1. **Flattened document model for players.** Profile fields (`name`, `colors`, `imageUri`,
   `imageData`, `imageContentType`) are first-class CosmosDB properties — not wrapped in a
   `profileJson` string. JSON files in `Default Data/Players/` ARE the exact CosmosDB documents.

2. **Cosmos .NET SDK for data plane, not REST API.** The REST API's `x-ms-partitionkey` header
   caused persistent HTTP 400 errors ("fewer components than defined in the collection").
   Loading `Microsoft.Azure.Cosmos.Client.dll` from the GameService build output into PowerShell
   solved it completely. The SDK handles partition keys, AAD auth, and serialization internally.

3. **`encode-images.ps1` generates complete documents.** Run once to base64-encode player images
   into the JSON files. After that, the seed script just loops and upserts verbatim.

4. **Templates are generated at startup, not seeded.** `DatabaseSeeder.UpsertSystemTemplatesAsync()`
   builds "regular" and "expansion" templates from `RegularBoardInfo.Default` /
   `ExpansionBoardInfo.Default` C# code on every app startup. No seed JSON files needed.
   Game creation also has a hardcoded fallback if templates are missing.

5. **`games` and `completed-games` are correctly empty.** They're populated at runtime when
   users create and finish games. No seed data required.

---

## Remaining Work

### Phase A: Update CosmosCatanDb to Flattened Model

**File:** `Catan3.GameService/Abstractions/CosmosCatanDb.cs`

The current `PlayerDoc` uses the old embedded-JSON pattern:

```csharp
// OLD — remove this
private class PlayerDoc
{
    public string Id { get; set; }
    public string? ProfileJson { get; set; }  // serialized PlayerProfile string
    public string? ImageData { get; set; }
    public string? ImageContentType { get; set; }
}
```

Replace with flattened shape matching the seed JSON files:

```csharp
// NEW — matches Default Data/Players/*.json exactly
private class PlayerDoc
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("colors")]
    public PlayerColorsDoc Colors { get; set; } = new();

    [JsonPropertyName("imageUri")]
    public string? ImageUri { get; set; }

    [JsonPropertyName("imageData")]
    public string? ImageData { get; set; }

    [JsonPropertyName("imageContentType")]
    public string? ImageContentType { get; set; }

    [JsonPropertyName("lifetimeStats")]
    public LifetimeStats? LifetimeStats { get; set; }
}

private class PlayerColorsDoc
{
    [JsonPropertyName("primary")]
    public string Primary { get; set; } = "#808080";

    [JsonPropertyName("secondary")]
    public string Secondary { get; set; } = "#606060";

    [JsonPropertyName("foreground")]
    public string Foreground { get; set; } = "#FFFFFF";
}
```

Update all methods that read/write `PlayerDoc`:

- `LoadPlayersAsync` / `LoadPlayerAsync` — construct `PlayerProfile` from flat fields
- `SavePlayerAsync` — deconstruct `PlayerProfile` into flat fields
- `SaveImageAsync` / `LoadImageAsync` — read/write `ImageData` + `ImageContentType` directly
- `DeleteImageAsync` — null out `ImageData` + `ImageContentType` on existing doc

**Also consider flattening `TemplateDoc.dataJson`** — currently stores `GameTemplateData` as a
serialized string. For now, keep as-is since templates are generated from C# code and the
embedded pattern works fine for write-once/read-by-id access patterns.

### Phase B: Run Contract Tests

```bash
# Against Azure
pwsh .scripts/database.ps1 test -Azure

# Against local emulator (if available)
pwsh .scripts/database.ps1 test
```

All 26 contract tests must pass. The tests create an isolated `catan-test-{guid}` database,
run all operations, and clean up.

### Phase C: Wire into GameService

**Gate:** All contract tests pass on Azure.

Update these files to inject `ICatanDb` instead of `CatanDbContext`:

| File | Change |
|------|--------|
| `Program.cs` | Register `CosmosClient` (singleton) + `CosmosCatanDb` (scoped); remove `AddDbContextPool` when Cosmos endpoint configured |
| `GameApiController.cs` | Inject `ICatanDb`; replace EF Core calls |
| `StatsController.cs` | Inject `ICatanDb` |
| `RecordingController.cs` | Inject `ICatanDb` |
| `GameTemplateService.cs` | Inject `ICatanDb` |
| `RecordingService.cs` | Resolve `ICatanDb` from scope |
| `DatabasePersistenceService.cs` | Inject `ICatanDb` |
| `DatabaseSeedingService.cs` | Resolve `ICatanDb`; call `InitializeAsync()` |

**Detection logic:** `COSMOS_ENDPOINT` env var → Cosmos mode; otherwise → SQLite (unchanged).

### Phase D: Smoke Test and Deploy

```bash
pwsh ./catan.ps1 run
# Create game, play turns, save, load, replay
# Verify player images display correctly
# Verify templates load in UI

pwsh ./catan.ps1 azure deploy
pwsh .scripts/database.ps1 doctor -Azure
```

---

## Container Summary

| Container | Partition Key | Seed Source | Document Model |
|-----------|---------------|-------------|----------------|
| `players` | `/id` | `Default Data/Players/*.json` (verbatim) | Flattened: id, name, colors, imageUri, imageData, imageContentType |
| `games` | `/id` | Empty (runtime) | id, gameName, gameState, compressedData (base64), metadata |
| `completed-games` | `/id` | Empty (runtime) | id, gameName, winnerId, compressedData, metadata |
| `templates` | `/id` | Generated at startup from C# code | id, name, category, isSystemTemplate, dataJson (serialized) |
| `recordings` | `/id` | `Default Data/Recordings/*.json` (verbatim) | id, name, gameType, playerCount, actionCount, gameId, data |

---

## Files Modified (cumulative)

| File | Status |
|------|--------|
| `Catan3.Shared/Services/GameServiceProxy.cs` | Done — added `GameId` to `RecordingSummary` |
| `Catan3.GameService/Abstractions/ICatanDb.cs` | Done |
| `Catan3.GameService/Abstractions/GameSaveData.cs` | Done |
| `Catan3.GameService/Abstractions/CompletedGameRecord.cs` | Done |
| `Catan3.GameService/Abstractions/CosmosCatanDb.cs` | **Phase A** — flatten PlayerDoc |
| `Catan3.GameService/Catan3.GameService.csproj` | Done — Cosmos NuGet added |
| `Catan3.GameService/Default Data/Players/*.json` | Done — flattened documents with base64 images |
| `Catan3.GameService/Default Data/Players/encode-images.ps1` | Done |
| `Tests/GameService/CatanDb/*` | Done — 26 contract tests |
| `.scripts/database.ps1` | Done — SDK-based seed, doctor, firewall, JMESPath fix |
| `catan.ps1` | Done — database verbs |
| `.docker/cosmos-emulator.yml` | Done |
| `Catan3.GameService/Program.cs` | **Phase C** — register CosmosCatanDb |
| `GameApiController.cs` + 6 more | **Phase C** — inject ICatanDb |

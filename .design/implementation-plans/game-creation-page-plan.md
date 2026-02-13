# Implementation Plan: Game Creation Page -- Phase 1

**Design:** `.design/game-creation-page.md`
**Date:** 2026-02-13
**Scope:** Phase 1 only (template engine). Phase 2 (CRUD editor) is a
separate plan after Phase 1 ships.

## Overview

Seven milestones, ordered by dependency. Each milestone has a verification
step that must pass before proceeding.

## Files Modified

| File | Action | Milestone |
|------|--------|-----------|
| `Catan.sln` | Modify | M1 |
| `catan.ps1` | Modify | M1 |
| `.scripts/build_worker.ps1` | Modify | M1 |
| `.vscode/tasks.json` | Modify | M1 |
| `Catan3.Shared/Models/GameTemplateData.cs` | **New** | M2 |
| `Catan3.GameService/Data/CatanDbContext.cs` | Modify | M2 |
| `Catan3.GameService/Data/DatabaseSeeder.cs` | Modify | M2 |
| `Catan3.GameService/Factory/BoardInfoJsonAdapter.cs` | **New** | M3 |
| `Catan3.GameService/Services/GameTemplateService.cs` | **New** | M3 |
| `Catan3.GameService/Controllers/GameApiController.cs` | Modify | M3, M4 |
| `Catan3.GameService/Program.cs` | Modify | M3 |
| `Catan3.Shared/Models/MessageObjects.cs` | Modify | M4 |
| `Catan3.Shared/TypeScript/CatanTypeGenSpec.cs` | Modify | M4 |
| `react-ui/lib/api/gameApi.ts` | Modify | M4 |
| `react-ui/app/new-game/page.tsx` | Modify | M4 |
| `react-ui/types/generated/models/` | Regenerated | M4 |
| `Tests/Shared/Serialization/SharedSerializationTests.cs` | Modify | M5 |
| `Tests/Shared/Serialization/BidirectionalSerializationTests.cs` | Modify | M5 |
| `Tests/Shared/Templates/BoardInfoJsonAdapterTests.cs` | **New** | M5 |
| `Tests/GameService/Templates/GameTemplateApiTests.cs` | **New** | M5 |

---

## Milestone 1: Deprecate Blazor and Desktop from Build

**Goal:** `./catan.ps1 build && ./catan.ps1 test` succeeds with only
Shared, GameService, CLI, Tests, and React.

### 1.1 `Catan.sln`

Remove these project entries and their build configuration blocks:

- `Catan3.WebUI` (line 30, GUID `{601F34F3-...}`)
- `WebUI.Server` (line 32, GUID `{16503443-...}`)
- `Catan Desktop` (line 14, GUID `{DBED3ED0-...}`)
- `Tests.DesktopApp.UI` (line 16, GUID `{8C9B1C2E-...}`)
- `WebUI` solution folder (line 28, GUID `{7E46470A-...}`)

Remove corresponding `GlobalSection(ProjectConfigurationPlatforms)` entries
for each removed GUID.

**Keep:** Catan3.Shared, Catan3.GameService, Tests.GameService, Tests.Shared,
Catan3.CLI.

### 1.2 `catan.ps1`

Remove or simplify these sections:

- **Parameters** (lines 96, 99): Remove `[switch]$Razor` and
  `[switch]$Desktop` parameters and their `.PARAMETER` documentation
  (lines 16-20).
- **`Start-WebUI` function** (lines 257-293): Delete entirely. The only UI
  is React (started via `npm run dev`).
- **`$WebUIPort` and `$WebUIUrl` variables** (lines 132, 135): Delete.
- **Build command** (lines 834-843): Remove `$Desktop` conditional. Always
  call `build.ps1 -NoTest -NoDesktop`.
- **Run command** (lines 1781-1879): Remove `$Razor`/`$Desktop`
  conditionals. Always build with `-NoDesktop`. Remove `Start-WebUI` call
  and Blazor UI status reporting.
- **Update command** (lines 2033-2070): Remove `$Razor` rebuild path.
- **Restart command** (lines 1991-2003): Remove `Start-WebUI` call.
- **Stop command** (lines 700-821): Remove `$WebUIPort` from port list.
  Remove Blazor-specific process killing.
- **Clean command** (line 1949): Remove `WebUI/Catan3.WebUI.csproj` and
  `DesktopApp/Catan Desktop.csproj` from clean targets.
- **Azure deploy** (lines 2239-2370): Remove Blazor deploy status messages.
  Leave `catan-azure.ps1` unchanged (already unused).
- **Help text**: Update all examples to remove `-Razor` and `-Desktop`.

### 1.3 `.scripts/build_worker.ps1`

- **Parameters** (lines 11-12): Remove `[switch]$NoFontRegister` and
  `[switch]$NoDesktop`.
- **`$CrossPlatformProjects`** (lines 475-480): Remove
  `WebUI/Catan3.WebUI.csproj`. Keep Shared, GameService, CLI.
- **Build logic** (lines 590-632): Remove Desktop conditional. Always build
  cross-platform projects only.
- **Font registration** (lines 624-631): Remove `Register-Font` call and
  `Register-Font` function (lines 375-424).
- **Test filtering** (lines 648-662): Remove Desktop test skip logic.
- **MSIX installation** (lines 710-862): Remove entire MSIX section.
- **Help text** (lines 134-184): Update to remove Desktop/font flags.

### 1.4 `.vscode/tasks.json`

Remove these task definitions:

- `build-webui` (lines 244-256)
- `publish-desktop` (lines 27-41)
- `build-and-publish` (lines 43-49)
- `rebuild-desktopapp` (lines 67-82)
- `rebuild-desktopapp-quoted` (lines 84-99)
- `run-webui` (lines 212-242)
- `test-ui` (lines 51-65)
- `test-ui-2` (lines 101-115)
- `run-ui-tests` (lines 117-131)
- `run-ui-tests-fixed` (lines 133-147)

**Keep:** `build`, `build-tests`, `run-gameservice`, `Expansion-Replay-Test`.

### Verification M1

```bash
pwsh ./catan.ps1 build
pwsh ./catan.ps1 test
# Both succeed. No references to WebUI or Desktop in build output.
```

---

## Milestone 2: Database Entity + Migration + Seed Data

**Goal:** `GameTemplates` table exists with `regular` and `expansion`
seeded. `catan.ps1 database install` creates and seeds the table.

### 2.1 `Catan3.Shared/Models/GameTemplateData.cs` (New)

Create the DTO that maps to the JSON schema. This lives in Shared because
both GameService and tests need it.

```csharp
namespace Catan3.Shared.Models;

public class GameTemplateData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public string Description { get; set; } = string.Empty;
    public string Engine { get; set; } = "base";
    public string GameType { get; set; } = "Regular";
    public ResourceRules ResourceRules { get; set; } = new();
    public HouseRules HouseRules { get; set; } = new();
    public bool HasSupplemental { get; set; }
    public List<TemplateTile> Tiles { get; set; } = [];
    public List<TemplateHarbor> Harbors { get; set; } = [];
    public List<TemplateEntitlement> Entitlements { get; set; } = [];
}

public class TemplateTile
{
    public int Q { get; set; }
    public int R { get; set; }
    public string Resource { get; set; } = "Desert";
    public int Number { get; set; }
}

public class TemplateHarbor
{
    public HexCoordinates HexCoordinates { get; set; } = new();
    public string Side { get; set; } = "Right";
    public string Type { get; set; } = "ThreeForOne";
}

public class TemplateEntitlement
{
    public string Entitlement { get; set; } = "Road";
}
```

Also create a summary DTO:

```csharp
public class GameTemplateSummary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsSystemTemplate { get; set; }
    public string Description { get; set; } = string.Empty;
    public int MinPlayers { get; set; }
    public int MaxPlayers { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

### 2.2 `CatanDbContext.cs`

Add after line 12 (existing DbSets):

```csharp
public DbSet<GameTemplateEntity> GameTemplates { get; set; } = null!;
```

Add entity class (after `RecordingEntity` at end of file):

```csharp
public class GameTemplateEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsSystemTemplate { get; set; }
    public int Version { get; set; } = 1;
    public string Data { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

Add to `OnModelCreating` (after RecordingEntity configuration):

```csharp
modelBuilder.Entity<GameTemplateEntity>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).HasMaxLength(100);
    entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
    entity.Property(e => e.Category).HasMaxLength(50);
    entity.HasIndex(e => e.Category);
    entity.Property(e => e.Data).IsRequired();
    entity.Property(e => e.CreatedAt).IsRequired();
    entity.Property(e => e.UpdatedAt).IsRequired();
});
```

### 2.3 EF Migration

```bash
cd Catan3.GameService
dotnet ef migrations add AddGameTemplatesTable
```

### 2.4 `DatabaseSeeder.cs`

Add a `SeedTemplatesAsync` method following the existing pattern. Call it
from `SeedAsync` after the existing seeds (around line 95).

```csharp
// In SeedAsync, after SeedRecordingsAsync:
if (!await context.GameTemplates.AnyAsync())
{
    await SeedTemplatesAsync(context);
}
```

`SeedTemplatesAsync` implementation:

1. Create a `GameTemplateData` from `RegularBoardInfo.Default`:
   - Iterate `TileKeys`, `Resources`, `Numbers` in parallel to build
     `TemplateTile` objects (preserving original order)
   - Map `Harbors` to `TemplateHarbor` objects
   - Map `PurchaseableEntitlements` to `TemplateEntitlement` objects
   - Copy `ResourceRules`, `HouseRules`, `GameType`, `Description`,
     `HasSupplemental`
   - Set `Id = "regular"`, `Name = "Regular Game"`, `Category = "Base"`,
     `Engine = "base"`
2. Serialize via `JsonHelper.Serialize(templateData)`
3. Create `GameTemplateEntity` with `IsSystemTemplate = true`,
   `CreatedAt = DateTime.UtcNow`, `UpdatedAt = DateTime.UtcNow`
4. Repeat for `ExpansionBoardInfo.Default` with `Id = "expansion"`,
   `Name = "Expansion Game"`, `Category = "Expansion"`
5. `context.GameTemplates.AddRange(...)` + `SaveChangesAsync`

### Verification M2

```bash
pwsh ./catan.ps1 database install
# Verify: SELECT * FROM GameTemplates returns 2 rows
# Verify: Both rows have IsSystemTemplate = 1
# Verify: JSON in Data column is valid and has camelCase keys
pwsh ./catan.ps1 test
# All existing tests still pass
```

---

## Milestone 3: Adapter + Service + API Endpoints

**Goal:** `BoardInfoJsonAdapter` produces identical output to the hardcoded
singletons. Template CRUD API works. GameService DI is wired.

### 3.1 `BoardInfoJsonAdapter.cs` (New)

**Location:** `Catan3.GameService/Factory/BoardInfoJsonAdapter.cs`

Implements `IGameMetadata`. Constructor takes `GameTemplateData`.

**Critical:** The `Tiles` list order in `GameTemplateData` defines the
parallel array order. The seeding in M2 preserves the original order from
`RegularBoardInfo`/`ExpansionBoardInfo`. The adapter does NOT re-sort --
it uses the tile list order as-is. This means the seed order IS the
contract.

Properties:

- `GameType` -- `Enum.Parse<GameType>(_template.GameType)`
- `Description` -- `_template.Description`
- `ResourceRules` -- `_template.ResourceRules` (direct reference)
- `HouseRules` -- `_template.HouseRules` (direct reference)
- `HasSupplemental` -- `_template.HasSupplemental`
- `TileKeys` -- `_template.Tiles.Select(t => new HexCoordinates(t.Q, t.R)).ToList()`
- `Resources` -- `_template.Tiles.Select(t => Enum.Parse<ResourceType>(t.Resource)).ToList()`
- `Numbers` -- `_template.Tiles.Select(t => t.Number).ToList()`
- `Harbors` -- map each `TemplateHarbor` to `HarborModel`:

  ```csharp
  new HarborModel
  {
      HarborKey = new HarborKey
      {
          HexCoordinates = h.HexCoordinates,
          HexSide = Enum.Parse<HexSide>(h.Side)
      },
      Type = Enum.Parse<HarborType>(h.Type)
  }
  ```

- `PurchaseableEntitlements` -- map each `TemplateEntitlement` to
  `EntitlementPurchaseModel` with `Entitlement` parsed from string

### 3.2 `GameTemplateService.cs` (New)

**Location:** `Catan3.GameService/Services/GameTemplateService.cs`

Constructor: `GameTemplateService(CatanDbContext context)`

Methods:

- `ListAsync(string? category)` -- query `GameTemplates`, project to
  `GameTemplateSummary` (extract `MinPlayers`/`MaxPlayers` from
  deserialized `ResourceRules`). Filter by category if provided.
- `GetAsync(string id)` -- find by ID, deserialize `Data` to
  `GameTemplateData` via `JsonHelper.Deserialize<GameTemplateData>()`.
  Return null if not found.
- `SaveAsync(GameTemplateData template)` -- find existing entity by ID.
  If `IsSystemTemplate`, throw `InvalidOperationException`. Serialize
  template to JSON, update `Data`, `Name`, `Category`, `UpdatedAt`.
- `SaveAsAsync(GameTemplateData template, string newId, string newName)` --
  create new entity. ID must not exist already. Set
  `IsSystemTemplate = false`.
- `DeleteAsync(string id)` -- find by ID. If `IsSystemTemplate`, throw
  `InvalidOperationException`. Remove and save.

No in-memory cache in Phase 1. Templates are small and rarely queried.
Add caching later if needed.

### 3.3 Template API Endpoints

**In `GameApiController.cs`**, add new endpoints. These can go in a new
controller `TemplateApiController.cs` or in the existing controller.
Prefer a new controller to keep concerns separate:

**`Catan3.GameService/Controllers/TemplateApiController.cs`** (New)

```csharp
[ApiController]
[Route("api/game")]
public class TemplateApiController : ControllerBase
{
    private readonly GameTemplateService _templateService;

    [HttpGet("templates")]
    public async Task<IActionResult> ListTemplates([FromQuery] string? category)

    [HttpGet("templates/{id}")]
    public async Task<IActionResult> GetTemplate(string id)

    [HttpPut("templates/{id}")]
    public async Task<IActionResult> UpdateTemplate(string id, [FromBody] GameTemplateData template)

    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplate([FromBody] GameTemplateData template)

    [HttpDelete("templates/{id}")]
    public async Task<IActionResult> DeleteTemplate(string id)
}
```

Delete returns 403 for system templates. Update returns 403 for system
templates.

### 3.4 DI Registration

**In `Program.cs`**, after existing service registrations:

```csharp
builder.Services.AddScoped<GameTemplateService>();
```

### Verification M3

```bash
pwsh ./catan.ps1 build
# Start service manually or via catan.ps1 run
curl http://localhost:8080/api/game/templates
# Returns JSON array with 2 templates
curl http://localhost:8080/api/game/templates/regular
# Returns full regular template JSON
curl -X DELETE http://localhost:8080/api/game/templates/regular
# Returns 403 (system template)
pwsh ./catan.ps1 test
# All tests pass
```

---

## Milestone 4: Wire `/api/game/new` to Templates + React Update

**Goal:** Game creation uses templates. React sends `templateId`. Existing
`GameType`-only path still works (backward compat for CLI/replay).

### 4.1 `MessageObjects.cs`

Add optional `TemplateId` to `NewGameMessage` (line 109):

```csharp
public class NewGameMessage(
    GameType GameType,
    IList<string> PlayerIds,
    string GameName,
    HouseRules? HouseRules = null,
    bool SaveLifetimeStats = true,
    string? TemplateId = null)
{
    // ... existing properties ...
    public string? TemplateId { get; set; } = TemplateId;
}
```

### 4.2 `GameApiController.cs` -- NewGame method

Replace the `IGameMetadata` selection logic (lines 288-291):

```csharp
// Resolve template: prefer TemplateId, fall back to GameType mapping
IGameMetadata gameInfo;
if (!string.IsNullOrEmpty(newGameMessage.TemplateId))
{
    var templateData = await _templateService.GetAsync(newGameMessage.TemplateId);
    if (templateData is null)
        return BadRequest($"Template '{newGameMessage.TemplateId}' not found");
    gameInfo = new BoardInfoJsonAdapter(templateData);
}
else
{
    // Backward compatibility: map GameType to default template
    var templateId = newGameMessage.GameType == GameType.Regular
        ? "regular" : "expansion";
    var templateData = await _templateService.GetAsync(templateId);
    if (templateData is null)
    {
        // Fallback to hardcoded singletons if templates not seeded
        gameInfo = newGameMessage.GameType == GameType.Regular
            ? RegularBoardInfo.Default
            : ExpansionBoardInfo.Default;
    }
    else
    {
        gameInfo = new BoardInfoJsonAdapter(templateData);
    }
}
```

Add `GameTemplateService` to controller constructor injection.

### 4.3 `CatanTypeGenSpec.cs`

Add after existing interface registrations (around line 92):

```csharp
AddInterface<GameTemplateData>();
AddInterface<GameTemplateSummary>();
AddInterface<TemplateTile>();
AddInterface<TemplateHarbor>();
AddInterface<TemplateEntitlement>();
```

Run TypeGen to regenerate:

```bash
dotnet run --project Catan3.Shared/TypeScript/TypeGenRunner
```

### 4.4 `react-ui/lib/api/gameApi.ts`

Update `createGame` (line 181) to include `templateId`:

```typescript
async createGame(
  gameType: GameType,
  playerIds: string[],
  gameName?: string,
  houseRules?: Partial<HouseRules>,
  saveLifetimeStats: boolean = true
): Promise<ApiResponse<string>> {
  // Map gameType to templateId
  const templateId = gameType === 'Regular' ? 'regular' : 'expansion';

  const message: NewGameMessage = {
    gameType,
    playerIds,
    gameName: gameName ?? 'Untitled Game',
    houseRules: houseRules as HouseRules,
    saveLifetimeStats,
    templateId,
  };
  // ... rest unchanged
}
```

### 4.5 `react-ui/app/new-game/page.tsx`

No changes needed in Phase 1. The `gameApi.createGame()` call already
passes `gameType`, and the API change in 4.4 adds `templateId`
transparently.

### Verification M4 -- Replay Test Gate

**This is the critical correctness gate for the entire template system.**

After M4, the replay tests exercise the full template path: each
`.catan_test` recording sends `GameType` (no `templateId`) →
`GameApiController` maps `GameType` → template ID → loads template from
DB → creates `BoardInfoJsonAdapter` → passes `IGameMetadata` to
`HandleNewGameAsync`. Every subsequent action's `GameHash` is verified
against the recorded hash. If the adapter produces arrays in a different
order, or any field differs from the original hardcoded singleton, the
hash will mismatch and the replay test will fail.

**Passing replay tests proves that games created from templates are
bit-for-bit identical to games created from the old hardcoded singletons.**

```bash
pwsh ./catan.ps1 build
pwsh ./catan.ps1 test
# CRITICAL: All replay tests in Tests/GameService/ReplayTests/ must pass.
# These tests verify GameHash at every state transition. A single ordering
# difference in the adapter will cause hash mismatch failures here.
# If replay tests fail, the template system has a bug -- do not proceed.

# Also verify manually:
# Start services, create a Regular game from React UI
# Game loads and plays correctly
```

---

## Milestone 5: Tests

**Goal:** Full test coverage for templates, adapter parity, serialization
round-trips, and API integration.

### 5.1 `Tests/Shared/Templates/BoardInfoJsonAdapterTests.cs` (New)

**Critical parity tests:**

```csharp
[Fact]
public void RegularTemplate_ProducesIdenticalMetadata()
{
    // 1. Build GameTemplateData from RegularBoardInfo.Default
    //    (same logic as DatabaseSeeder.SeedTemplatesAsync)
    // 2. Create BoardInfoJsonAdapter
    // 3. Compare every IGameMetadata property field-by-field:
    //    - TileKeys: exact order, exact coordinates
    //    - Resources: exact order, exact types
    //    - Numbers: exact order, exact values
    //    - Harbors: exact count, exact coordinates/sides/types
    //    - PurchaseableEntitlements: exact count, exact entitlements
    //    - ResourceRules: all fields match
    //    - HouseRules: all fields match
    //    - GameType, Description, HasSupplemental
}

[Fact]
public void ExpansionTemplate_ProducesIdenticalMetadata()
{
    // Same as above for ExpansionBoardInfo.Default
}

[Fact]
public void RoundTrip_SerializeDeserialize_PreservesOrder()
{
    // 1. Build GameTemplateData from RegularBoardInfo.Default
    // 2. Serialize to JSON via JsonHelper
    // 3. Deserialize back to GameTemplateData
    // 4. Create BoardInfoJsonAdapter from deserialized data
    // 5. Compare field-by-field against RegularBoardInfo.Default
}
```

### 5.2 `Tests/Shared/Serialization/SharedSerializationTests.cs`

Add test following the existing pattern (like `HouseRules` test at
line 522):

```csharp
[Fact]
public async Task GameTemplateData_ShouldSerializeAndDeserialize_AllFields()
{
    // Build a GameTemplateData with representative data
    // Serialize via JsonHelper.Serialize
    // Deserialize via JsonHelper.Deserialize<GameTemplateData>
    // Assert all fields match including nested Tiles, Harbors, Entitlements
}
```

### 5.3 `Tests/Shared/Serialization/BidirectionalSerializationTests.cs`

Add C# -> TS -> C# round-trip test following existing pattern:

```csharp
[Fact]
public async Task GameTemplateData_RoundTrip()
{
    var template = BuildRegularTemplate(); // helper
    await TestRoundTrip(template, "GameTemplateData");
}
```

### 5.4 `Tests/GameService/Templates/GameTemplateApiTests.cs` (New)

Integration tests using `TestWebApplicationFactory`:

- `GET /api/game/templates` returns seeded templates
- `GET /api/game/templates/regular` returns full template
- `GET /api/game/templates/nonexistent` returns 404
- `DELETE /api/game/templates/regular` returns 403 (system)
- `POST /api/game/templates` creates new template
- `DELETE /api/game/templates/{newId}` succeeds (non-system)
- `POST /api/game/new` with `templateId: "regular"` creates a game
- `POST /api/game/new` with `gameType: "Regular"` (no templateId) still
  creates a game via fallback

### Verification M5

```bash
pwsh ./catan.ps1 test
# All tests pass, including:
# - Parity tests (adapter vs singleton)
# - Serialization round-trip tests
# - API integration tests
# - Existing replay tests (unchanged)
cd react-ui && npm run test:run
# React tests pass
```

---

## Milestone 6: Staging Deploy + Smoke Test

**Goal:** Staging environment works with templates.

### 6.1 Deploy

```bash
# Push to staging branch to trigger deploy-staging.yml
git push origin game-creation-page:staging
```

### 6.2 Smoke Test

1. Verify `/api/game/templates` returns 2 templates on staging
2. Create a Regular game from the React UI on staging
3. Play through a few turns (roll, build, next)
4. Create an Expansion game and verify it works
5. Verify `/health?checkDatabase=true` shows database connected

### Verification M6

All staging smoke tests pass. Games created from templates are
indistinguishable from games created from hardcoded singletons.

---

## Milestone 7: Cleanup + Documentation

### 7.1 Update `.design/README.md`

Add `game-creation-page.md` and `game-state-machine.md` to the document
index if not already present.

### 7.2 Update `.design/known-issues.md`

Remove any items related to hardcoded board info or Blazor build issues
that are now resolved.

### 7.3 Update `CLAUDE.md`

If any build commands changed (e.g., removed flags), update the
documentation.

### Verification M7

```bash
pwsh ./catan.ps1 test
# Final check: everything passes
```

---

## Dependency Order

```text
M1 (Deprecate Blazor/Desktop)
 └─→ M2 (Entity + Migration + Seed)
      └─→ M3 (Adapter + Service + API)
           └─→ M4 (Wire /api/game/new + React)
                └─→ M5 (Tests)
                     └─→ M6 (Staging Deploy)
                          └─→ M7 (Cleanup)
```

M1 is independent and can be done first. M2-M4 are sequential. M5 can
partially overlap with M3-M4 (write tests as code is written). M6
depends on all code being merged. M7 is last.

## What This Plan Does NOT Cover

- **Phase 2 (CRUD editor)** -- separate plan after Phase 1 ships
- **IGameRules extraction** -- deferred to Seafarers
- **GameMode enum** -- separate implementation
- **Template versioning/history** -- not in MVP
- **Auth for template editing** -- dev-only for now

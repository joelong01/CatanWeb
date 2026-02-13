# Game Creation Page

**Status:** Draft
**Date:** 2026-02-12

## Summary

Replace hardcoded `RegularBoardInfo` / `ExpansionBoardInfo` singletons with
**JSON templates stored in the database**. Deprecate Blazor and Desktop UIs
(leave source, remove from build/test). Then build a React **Game Creation**
page for browsing, editing, and saving templates with full CRUD.

Two phases:

1. **Template engine** -- database-backed templates that drive game creation
   for Regular and Expansion. No UI changes to the new-game page yet; the
   existing game-type dropdown works against templates instead of singletons.
2. **Game Creation page** -- full CRUD editor for templates (browse, edit,
   save, save-as, delete). Prerequisite for Seafarers, which needs new
   templates that don't exist as hardcoded classes.

## Goals

- **Metadata-driven boards** -- single code path for all game types.
- **Deprecate Blazor/Desktop** -- stop building/testing them; React is the UI.
- **Database templates** -- document-style JSON in a `GameTemplates` table.
- **Backward compatible** -- Regular and Expansion games work identically to
  today; templates are seeded from the existing hardcoded data.
- **Foundation for Seafarers** -- the template schema supports islands, sea
  tiles, ships, and pirate (see `seafarers.md`).
- **Full CRUD** -- browse, create, edit, save, save-as, delete templates from
  the React UI.

## Non-Goals

- Seafarers gameplay implementation (separate design).
- Electron/desktop packaging (future).
- User-created templates in production (future; no auth story yet).
- Rule engine decomposition (deferred to Seafarers phase; see architecture
  section below for the design direction).

## Current State

- `IGameMetadata` implemented by `RegularBoardInfo` and `ExpansionBoardInfo`
  (hardcoded singletons in `Catan3.Shared`).
- `/api/game/new` selects `RegularBoardInfo.Default` or
  `ExpansionBoardInfo.Default` based on `GameType` enum.
- `HandleNewGameAsync` reads parallel arrays (`TileKeys[i]`, `Resources[i]`,
  `Numbers[i]`) plus `Harbors`, `ResourceRules`, `HouseRules`,
  `PurchaseableEntitlements`.
- React new-game page sends `GameType` + `PlayerIds` + `GameName`.
- Build system (`catan.ps1`) builds Blazor WebUI on all platforms and Desktop
  on Windows. Tests run for GameService and Shared.

## Phase 1: Template Engine

### 1.1 Deprecate Blazor and Desktop

**Leave source in repo.** Remove from build/test/run:

- **`catan.ps1`**: Remove Blazor WebUI from `build`, `test`, `run` commands.
  Remove `-Desktop` and `-Razor` flags. The only UI is React.
- **`build_worker.ps1`**: Remove `WebUI/Catan3.WebUI.csproj` from
  `$CrossPlatformProjects`. Remove Desktop/font-registration paths.
- **`Catan.sln`**: Remove `Catan3.WebUI`, `WebUI.Server`,
  `Catan Desktop`, `Tests.DesktopApp.UI` projects.
- **`.vscode/tasks.json`**: Remove any build/launch tasks referencing WebUI
  or Desktop projects.
- **Azure deploy**: `catan-azure.ps1` `Deploy-WebUI` function (Blazor deploy)
  is already unused; leave it but don't call it.
- **CI workflows**: No changes needed -- staging/production already deploy
  React only.

**What remains in the build:**

| Project | Purpose |
|---------|---------|
| `Catan3.Shared` | Core models, GameStateMachine, IGameMetadata |
| `Catan3.GameService` | REST API, SignalR hub, database |
| `Tests.GameService` | GameService tests |
| `Tests.Shared` | Shared model tests |
| `Catan3.CLI` | Automation harness |
| `react-ui` | React UI (Next.js) |

### 1.2 GameTemplate JSON Schema

A `GameTemplate` is a self-contained JSON document that carries everything
`IGameMetadata` needs, plus metadata for the template browser.

```json
{
  "id": "regular",
  "name": "Regular Game",
  "category": "Base",
  "version": 1,
  "description": "Standard 19-tile board for 3-4 players",
  "engine": "base",
  "gameType": "Regular",
  "resourceRules": {
    "maxCities": 4,
    "maxSettlements": 5,
    "maxRoads": 15,
    "minPlayers": 3,
    "maxPlayers": 4
  },
  "houseRules": {
    "goldTiles": 1,
    "wallsProtectCities": true,
    "hideBaronBeforeInvasion": false,
    "knightMovesBaronBeforeRoll": true,
    "supplementalMinPlayers": 5,
    "griefDodgy": true
  },
  "hasSupplemental": false,
  "tiles": [
    { "q": 0, "r": 0, "resource": "Desert", "number": 7 },
    { "q": 1, "r": -1, "resource": "Wheat", "number": 9 }
  ],
  "harbors": [
    { "hexCoordinates": { "q": 3, "r": -3 }, "side": "SouthWest", "type": "ThreeForOne" }
  ],
  "entitlements": [
    { "entitlement": "Road" },
    { "entitlement": "Settlement" },
    { "entitlement": "City" },
    { "entitlement": "DevCard" }
  ]
}
```

**Key design decisions:**

- **Tiles as objects, not parallel arrays.** Each tile carries its own `q`,
  `r`, `resource`, and `number`. No more index-aligned arrays. This is easier
  to read, edit, and validate. The adapter reconstructs the parallel arrays
  that `HandleNewGameAsync` expects.
- **Deterministic tile ordering.** The adapter MUST produce parallel arrays in
  the exact same order as the existing hardcoded singletons. Define sort order
  as axial coordinates `(q, r)` for tiles, `(TileKey.Q, TileKey.R, HexSide)`
  for roads, `(TileKey.Q, TileKey.R, HexPosition)` for buildings, and
  `(HexCoordinates.Q, HexCoordinates.R, HexSide)` for harbors. This matters
  because `UpdateGameHash()` consumes ordered lists (replay hash verification
  fails on reorder), `MarkBuildableRoads`/`MarkBuildableBuildings` assign
  `BuildIndex` in enumeration order, and `BalancedShuffle`/`ValidateGame`
  iterate tiles in list order.
- **Schema matches IGameMetadata semantics.** Every field in `IGameMetadata`
  has a corresponding JSON field.
- **Serialization uses `JsonHelper.StandardOptions`.** The project has a
  centralized JSON policy in `Catan3.Shared/Utility/JsonHelper.cs` that
  enforces `CamelCase` naming, `JsonStringEnumConverter`, and
  `ReferenceHandler.IgnoreCycles`. All template serialization and
  deserialization MUST use `JsonHelper.Serialize<T>()` /
  `JsonHelper.Deserialize<T>()` — not raw `JsonSerializer` with custom
  options. This ensures consistency with the existing C# ↔ TypeScript
  interop pipeline.
- **`engine` field** selects the rule component bundle (see Rule Engine
  Architecture below). Defaults to `"base"` for Regular and Expansion.
  Seafarers templates will use `"seafarers"`.
- **Seafarers-ready fields** (optional, not used in Phase 1): `islandId`,
  `type` (land/sea), `seaKind`, `faceDown`. Omitted fields default to
  land tiles with no island grouping.

### 1.3 Database Entity

```text
GameTemplateEntity
├── Id               string(100)  PK      Template slug ("regular", "expansion")
├── Name             string(255)  required Display name
├── Category         string(50)   indexed  "Base", "Expansion", "Seafarers"
├── IsSystemTemplate bool         required Seeded templates (regular, expansion)
├── Version          int          required Schema version for migration
├── Data             TEXT         required Full JSON document
├── CreatedAt        DateTime     required
├── UpdatedAt        DateTime     required
```

Document-style: the `Data` column holds the complete JSON. `Name` and
`Category` are denormalized for fast listing queries.

**System templates** (`IsSystemTemplate = true`) cannot be deleted or
overwritten via the API. The service layer enforces this. Seed data sets
`IsSystemTemplate = true` for `regular` and `expansion`. Add
`IsSystemTemplate` to TypeGen so the React editor can disable delete/rename
for seeded templates.

### 1.4 BoardInfoJsonAdapter

New class implementing `IGameMetadata` that wraps a deserialized
`GameTemplate` JSON:

```csharp
public class BoardInfoJsonAdapter : IGameMetadata
{
    private readonly GameTemplateData _template;

    public BoardInfoJsonAdapter(GameTemplateData template) { ... }

    public GameType GameType => Enum.Parse<GameType>(_template.GameType);
    public string Description => _template.Description;
    public ResourceRules ResourceRules => _template.ResourceRules;
    public HouseRules HouseRules => _template.HouseRules;
    public bool HasSupplemental => _template.HasSupplemental;

    // Reconstruct parallel arrays from tile objects
    public List<HexCoordinates> TileKeys =>
        _template.Tiles.Select(t => new HexCoordinates(t.Q, t.R)).ToList();
    public List<ResourceType> Resources =>
        _template.Tiles.Select(t => Enum.Parse<ResourceType>(t.Resource)).ToList();
    public List<int> Numbers =>
        _template.Tiles.Select(t => t.Number).ToList();

    public List<HarborModel> Harbors => /* map from template harbors */;
    public List<EntitlementPurchaseModel> PurchaseableEntitlements => /* map */;
}
```

**Location:** `Catan3.GameService/Factory/BoardInfoJsonAdapter.cs`
(alongside existing `IGameMetadata.cs`).

### 1.5 Template Service

```csharp
public class GameTemplateService
{
    Task<List<GameTemplateSummary>> ListAsync(string? category = null);
    Task<GameTemplateData?> GetAsync(string id);
    Task<GameTemplateData> SaveAsync(GameTemplateData template);
    Task<GameTemplateData> SaveAsAsync(GameTemplateData template, string newId, string newName);
    Task DeleteAsync(string id);
}
```

Caches templates in memory; invalidates on save/delete.

### 1.6 API Changes

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/game/templates` | GET | List template summaries |
| `/api/game/templates/{id}` | GET | Get full template JSON |
| `/api/game/templates/{id}` | PUT | Update template |
| `/api/game/templates` | POST | Create new template (save-as) |
| `/api/game/templates/{id}` | DELETE | Delete template |
| `/api/game/new` | POST | **Modified**: accepts `templateId` instead of `GameType` |

The `/api/game/new` change:

```csharp
// Before: hardcoded selection
IGameMetadata gameInfo = newGameMessage.GameType == GameType.Regular
    ? RegularBoardInfo.Default
    : ExpansionBoardInfo.Default;

// After: load from database
var templateData = await _templateService.GetAsync(newGameMessage.TemplateId);
IGameMetadata gameInfo = new BoardInfoJsonAdapter(templateData);
```

**Backward compatibility:** Add optional `TemplateId` field to
`NewGameMessage` (C# record in `MessageObjects.cs`; also update
`CatanTypeGenSpec` for TypeGen). If `TemplateId` is provided, use it. If
only `GameType` is provided, map server-side:
`GameType.Regular` → `"regular"`, `GameType.Expansion` → `"expansion"`.
Both paths must work. CLI and replay tests continue to send `GameType` only.

### 1.7 Migration and Seed Data

**EF migration:** Add `DbSet<GameTemplateEntity>` to `CatanDbContext` (which
currently has 6 DbSets: Players, Images, GameSaveData, GameSaveMetadata,
CompletedGames, Recordings). Create a migration named
`AddGameTemplatesTable`.

**Seeding:** Extend `DatabaseSeeder` (which already seeds players, games,
and recordings) with idempotent seeding of `regular` and `expansion`
templates. Seed data is generated at migration time by serializing
`RegularBoardInfo.Default` and `ExpansionBoardInfo.Default` through
`JsonHelper.Serialize<GameTemplateData>()` — not from embedded JSON files.
This ensures the seed data exactly matches the existing hardcoded singletons
and uses the project's centralized JSON policy.

Both seeded templates set `IsSystemTemplate = true`.

### 1.8 React New-Game Page Updates

Minimal changes in Phase 1:

- The game-type dropdown still shows "Regular" and "Expansion".
- Under the hood, `gameApi.createGame()` sends `templateId: "regular"` or
  `templateId: "expansion"` instead of (or in addition to) `GameType`.
- No new UI for template browsing yet.

### 1.9 TypeGen Updates

Add to `CatanTypeGenSpec`:

- `GameTemplateData` (full template for editor)
- `GameTemplateSummary` (for list endpoint; includes `isSystemTemplate`)
- Updated `NewGameMessage` with optional `templateId` field

The 5-step TypeGen pipeline (`TypeGenRunner/Program.cs`) handles camelCase
conversion, enum-to-union conversion, and `[JsonIgnore]` removal
automatically. New types will need entries in the `[JsonIgnore]` removal
step if any properties should be excluded from the TypeScript interface.

## Phase 2: Game Creation Page

### 2.1 Template Browser

New React page at `/templates` with:

- **List view** -- cards/rows showing template name, category, description,
  player count range. Grouped by category (Base, Expansion, Seafarers).
- **Actions** -- New, Edit, Duplicate (save-as), Delete with confirmation.
- **Search/filter** by category and name.

### 2.2 Template Editor

New React page at `/templates/[id]/edit` with:

- **Metadata panel** -- name, description, category, player count, rules.
- **Board preview** -- read-only hex grid rendered from template data.
  Shows tiles with resources, numbers, and harbors.
- **JSON editor** -- raw JSON view with syntax highlighting and validation.
  Changes here update the board preview in real-time.
- **Tile list** -- tabular view of all tiles (q, r, resource, number) with
  inline editing.
- **Harbor list** -- tabular view of harbors with inline editing.
- **Validation** -- real-time checks: resource counts match expectations,
  no duplicate coordinates, number distribution is valid, harbors are on
  valid edges.
- **Save** -- PUT to update existing template.
- **Save As** -- POST to create a copy with a new ID and name.

### 2.3 Validation Rules

- Tile coordinates are unique (no duplicates).
- Resource counts match expected totals per game type.
- Number distribution: no 6/8 adjacent (warning, not error).
- Harbors reference valid hex edges on sea-adjacent tiles.
- Player count range is valid (min <= max, min >= 2).
- All required fields present.

## Rule Engine Architecture (Future Direction)

This section documents the extensibility strategy for adding new game types
(Seafarers, Cities & Knights, custom scenarios) without polluting
`GameStateMachine` with per-game-type if-statements.

**Not implemented in Phase 1 or Phase 2.** This is the architectural direction
that templates are designed to support. Implementation happens when Seafarers
begins.

For a comprehensive analysis of the current `GameStateMachine` architecture
(~2460 lines), state flow, handler methods, and extensibility boundaries, see
[game-state-machine.md](game-state-machine.md).

### Approaches Considered

Three approaches were evaluated against prior art from boardgame.io, JSettlers2,
Game Programming Patterns, and other turn-based game frameworks:

**A. Composable Rule Components** — decompose rules into pluggable interfaces
(`IRouteCalculator`, `IArmyTracker`, `IRobberHandler`, etc.) bundled into a
`GameEngine` per game type. **Rejected:** still codifies which mechanics exist
in the base interfaces. Adding pirate requires a new `IPirateHandler`, and
every existing game type needs a stub. Doesn't scale when entirely new
mechanisms appear.

**B. State-Based Handler Dispatch** — thin state machine switch that delegates
each `GameState` to a per-game-type handler (`HandleWaitingForNext()`).
**Rejected:** interface balloons to 30+ methods. Every game type must implement
every method. Massive refactoring for minimal benefit. No clean way to handle
combined expansions.

**C. Hybrid State Dispatch + Shared Extensions** — per-game-type handler with
shared logic as extension methods. Better than B, but hits the inheritance wall
with combined expansions (Seafarers + Cities & Knights on the same board).

### Recommended: IGameRules Injection (Approach D)

The key insight from analyzing boardgame.io's plugin architecture and the
actual `GameStateMachine` code: the right extensibility boundary is **not** the
state dispatch but the **derived-state calculation pipeline** — the methods
called by `LogGameModel()` — plus a hook for game-type-specific state
transitions.

#### The LogGameModel Pipeline

Every game action flows through `LogGameModel()` (line 1490), which calls:

```text
LogGameModel(gameModel)
├── UpdateScore()              ← longest road, largest army, VP calc
├── UpdatePlayerStars()        ← probability-weighted holdings
├── MarkBuildableRoads()       ← which roads can be built
├── MarkBuildableBuildings()   ← which buildings can be placed/upgraded
├── SetActionFlags()           ← undo/next/roll enabled
├── UpdatePurchaseUi()         ← purchase button states
├── SetPlaySoldierAccess()     ← soldier availability
├── SetDevCardAccess()         ← devcard availability
├── UpdateGameHash()           ← state hash for validation
└── _gameLog.Done()            ← commit to undo/redo stack
```

**This pipeline is where game types actually differ.** Seafarers needs a
different route calculator, buildable-ship marking, island discovery scoring,
and gold-field resource handling. The state transitions (`NextState()`) and
handler patterns are mostly shared.

#### IGameRules Interface

Extract only the methods that diverge per game type:

```csharp
public interface IGameRules
{
    // Derived-state pipeline (called by LogGameModel)
    void CalculateLongestRoute(GameModel game);
    void UpdateScore(GameModel game);
    void MarkBuildableLocations(GameModel game);

    // State transition hook
    GameState? ResolveNextState(GameModel game, GameState currentState);

    // New action support (additive)
    // Seafarers adds: HandleShipMovement, HandlePirateMovement, etc.
    // These are new methods on IGameStateMachine, not overrides.
}
```

#### How It Works

1. **GameStateMachine keeps its structure.** The handler pattern, undo/redo,
   recording, and `LogGameModel` flow all stay the same.
2. **LogGameModel calls IGameRules** instead of inline methods:

   ```csharp
   // Before:
   CalculateLongestRoad(gameModel);
   MarkBuildableRoads(gameModel);

   // After:
   _rules.CalculateLongestRoute(gameModel);
   _rules.MarkBuildableLocations(gameModel);
   ```

3. **NextState calls the hook** for game-type-specific transitions:

   ```csharp
   var overrideState = _rules.ResolveNextState(gameModel, gameModel.GameState);
   if (overrideState.HasValue) {
       gameModel.GameState = overrideState.Value;
       return gameModel;
   }
   // ... existing switch statement
   ```

4. **New mechanics are additive.** Seafarers adds `HandleShipMovementAsync`
   and `HandleMovePirateAsync` as new methods on `IGameStateMachine`. These
   are entirely new handler methods, not overrides of existing ones. The base
   game simply doesn't have them.

5. **Shared logic lives as static helpers or extension methods.** Code that's
   common across game types (standard scoring formula, army tracking, buildable
   road adjacency) stays as reusable methods that `IGameRules` implementations
   call internally.

#### Concrete Implementations

**BaseGameRules** (Regular + Expansion):

- `CalculateLongestRoute` — current `CalculateLongestRoad` extracted verbatim
- `UpdateScore` — current scoring logic extracted verbatim
- `MarkBuildableLocations` — current road + building marking
- `ResolveNextState` — returns `null` (use default switch)

**SeafarersGameRules** (delegates to base, overrides where needed):

- `CalculateLongestRoute` — existing algorithm already works (ships are
  `RoadState.Ship` on `RoadModel`); only adds pirate-blocking logic
- `UpdateScore` — base scoring + island discovery VP bonuses
- `MarkBuildableLocations` — base + ship placement on buildable sea
- `ResolveNextState` — handles new states (gold resource choice, pirate move)

#### Why This Approach

- **Minimal interface surface** — `IGameRules` has ~4 methods, not 30+.
  New mechanics are additive (new handler methods), not overrides.
- **No stub explosion** — base game doesn't need to know about pirate.
  Pirate is a new `HandleMovePirateAsync` on `IGameStateMachine`, with a new
  `SeafarersGameRules.ResolveNextState` that handles the pirate states.
- **Combined expansions work** — `SeafarersPlusCKGameRules` delegates to both
  `SeafarersGameRules` and `CitiesAndKnightsGameRules` via composition.
- **Minimal refactoring** — only extract methods from `LogGameModel` pipeline.
  The ~2460 line state machine structure stays intact.
- **Undo/redo unaffected** — snapshot-based, orthogonal to rule logic.
- **Recording/replay unaffected** — captures handler inputs, not internal logic.
- **Testable** — `IGameRules` implementations can be unit tested with mock
  `GameModel` objects. Existing replay tests verify base behavior unchanged.

#### Template Integration

Templates declare their rule set via the `"engine"` field:

```json
{ "engine": "base" }        // Regular, Expansion
{ "engine": "seafarers" }   // Seafarers scenarios
```

A factory resolves `IGameRules` from the engine field:

```csharp
public static IGameRules CreateRules(string engine) => engine switch
{
    "base" => new BaseGameRules(),
    "seafarers" => new SeafarersGameRules(),
    _ => throw new ArgumentException($"Unknown engine: {engine}")
};
```

### Phase 1 Impact

None. Phase 1 templates use `"engine": "base"` (or omit the field, defaulting
to base). The current monolithic `GameStateMachine` continues to work
unchanged. The `IGameRules` extraction happens when Seafarers begins —
`BaseGameRules` is a mechanical extraction of existing code, verified by
replay tests.

The template schema includes the `"engine"` field now so that existing
templates are forward-compatible.

## Testing

### Phase 1

- **Parity**: `BoardInfoJsonAdapter` produces identical `IGameMetadata`
  output as `RegularBoardInfo.Default` and `ExpansionBoardInfo.Default`.
  Field-by-field comparison of `TileKeys`, `Resources`, `Numbers`, `Harbors`,
  `PurchaseableEntitlements`, `ResourceRules`, `HouseRules`. This is the
  critical test — ordering must match exactly.
- **Serialization round-trip**: Extend `Tests/Shared/Serialization/` test
  suites to cover `GameTemplateData`. Add to `SharedSerializationTests`
  (model round-trip), `JavaScriptCompatibilityTests` (camelCase, string
  enums, no C# artifacts), and `BidirectionalSerializationTests` (C# ↔ TS ↔
  C# round-trip). All serialization uses `JsonHelper.StandardOptions`.
- **Integration**: `/api/game/new` with `templateId` creates a valid game.
  `/api/game/templates` CRUD endpoints work correctly. System template
  delete returns 403.
- **Replay**: existing `.catan_test` replay tests pass unchanged (games
  created from templates behave identically to hardcoded boards).
- **React**: `npm run test:run` passes.
- **Build**: `./catan.ps1 build` succeeds without Blazor/Desktop.
  `./catan.ps1 test` passes all .NET and React tests.

### Phase 2

- **React**: template browser loads and displays templates, CRUD operations
  work, editor validates and saves.
- **Integration**: round-trip test -- create template via API, load in editor,
  modify, save, create game from modified template.

## Risks

- **Parallel array reconstruction** -- the adapter must produce arrays in the
  exact same order as the hardcoded classes. Hash validation in replay tests
  will catch any ordering mismatch. Mitigate with field-by-field parity
  tests comparing adapter output vs. singleton output for both Regular and
  Expansion. Define deterministic sort order (axial coordinates) and test it.
- **JSON schema drift** -- if `IGameMetadata` changes, the JSON schema must
  change too. Mitigate with adapter tests that verify all fields.
- **Large templates** -- Seafarers scenarios may have 60+ tiles. The document
  approach handles this fine (JSON text in SQL).

## Open Questions

- Auth for template editing (dev-only for now; no production editing).
- Should templates be versioned (keep history of edits)? (Not in MVP.)

## Milestones

1. Deprecate Blazor/Desktop from build system.
2. `GameTemplateEntity` + migration + seed data.
3. `BoardInfoJsonAdapter` + template service + API endpoints.
4. `/api/game/new` uses templates; all existing tests pass.
5. Deploy to staging; verify Regular and Expansion games work.
6. Template browser page (list, delete).
7. Template editor page (edit, save, save-as, validate).
8. Full CRUD verified end-to-end.

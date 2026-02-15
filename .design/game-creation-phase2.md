# Game Creation Phase 2: Template Editor

**Date:** February 13, 2026
**Prerequisite:** Phase 1 (template engine) -- merged via PR #20
**Design reference:** `.design/game-creation-page.md` sections 2.1-2.3,
`.design/game-state-machine.md`, `.design/seafarers.md`

## Goal

Build a WYSIWYG template editor where the board is the primary editing
surface. Click tiles to edit resources and numbers. Place and drag harbors.
Shuffle the board and see results visually. Edit entitlements with full
cost/icon/description metadata. Define islands for Seafarers. The form
panel stays in bidirectional sync with the board.

This is the prerequisite for Seafarers -- templates must be able to
express islands, sea tiles, ships, and enriched entitlements before the
GameStateMachine can consume them.

## Non-Goals

- GameStateMachine refactor to read entitlement costs from templates
  (Seafarers task -- the state machine currently hardcodes costs)
- PurchasePanel data-driven rendering from template metadata (Seafarers)
- Template versioning or history
- Authentication or per-user template ownership
- Modifying `/new-game` page to select templates (separate task)

## Architecture Decisions

### Routing

| Route | Purpose |
|-------|---------|
| `/templates` | Template browser -- list, filter, delete, clone |
| `/templates/[id]` | Template editor -- WYSIWYG board + form panel |

The `/templates` route is a developer/admin tool, not in the main nav
initially.

### State Management

**Local component state only.** No Zustand store.

The editor page loads the template via API on mount, holds it in
`useState<GameTemplateData>`, and sends it back via API on save. This
matches the pattern in `/new-game/page.tsx`.

Rationale: template editing is self-contained (load → edit → save). No
other component observes editing state. Dirty tracking and validation are
local concerns.

### Interactive Board (Not Passive Preview)

The board is the **primary editing surface**, not a read-only preview.
The form panel is the secondary view that stays in sync.

Build an `EditorBoard` component that uses the existing `HexGrid` and
`GameTile` rendering primitives but adds editor-specific interactions:

- **Click tile** → select it, highlight in form panel
- **Right-click tile** → context menu: change resource, change number,
  set as desert, assign to island
- **Click harbor** → select it, highlight in form panel
- **Drag harbor** → reposition to a different hex edge
- **Hover** → show coordinate tooltip

**Why not extend `GameBoard`?** `GameBoard` is 1660 lines, tightly coupled
to the game Zustand store (`useBoardData`, `useBoardPlayers`, etc.), and
handles game-specific interactions (hit-testing buildings, roads, robber).
The editor needs different state (local `GameTemplateData`), different
interactions (right-click to edit tiles), and no game-state dependencies.
Sharing the `HexGrid` + `GameTile` primitives is the right layer to reuse.

**Why not reuse `ReplayBoardPreview`?** That expects a full `GameModel`
with buildings, roads, and players. Templates have none of those.

### Template Schema Enrichment

Phase 2 enriches the template schema to carry data that the
GameStateMachine and PurchasePanel will consume in Seafarers. The schema
changes are **additive** -- existing `regular` and `expansion` templates
continue to work unchanged.

#### Enriched Entitlements

Current `TemplateEntitlement` is minimal:

```typescript
{ entitlement: string }  // e.g., "Road"
```

Enrich to match the seafarers.md design:

```typescript
{
  entitlement: string;      // "Road", "Settlement", "City", "Ship", etc.
  title: string;            // Display name
  description: string;      // Tooltip text
  cost: ResourceCost;       // { wood: 1, brick: 1 } etc.
  icon: string;             // Catan font glyph or icon identifier
  purchaseType: string;     // Maps to GameStateMachine handler
}
```

The GameStateMachine **ignores** the enriched fields today (it reads
entitlement type from the enum, costs are hardcoded). Seafarers will
wire these fields to the PurchasePanel and state machine.

#### Tile Extensions

Add optional fields for Seafarers (ignored by current base game):

```typescript
{
  q: number;
  r: number;
  resource: string;
  number: number;
  // New optional fields:
  type?: 'land' | 'sea';        // Default: 'land'
  seaKind?: 'buildable' | 'blocked';
  islandId?: string;
  faceDown?: boolean;
  tags?: string[];               // 'gold', 'starter', etc.
}
```

#### Islands

New top-level field on `GameTemplateData`:

```typescript
islands?: TemplateIsland[];
```

Where:

```typescript
{
  id: string;
  name: string;
  shuffleGroup: string;     // Tiles in same group shuffle together
  discoveryVp?: number;     // VP for first settlement on island
}
```

### API Layer

Add template CRUD functions to `gameApi.ts`:

```typescript
getTemplates(category?: string): Promise<ApiResponse<GameTemplateSummary[]>>
getTemplate(id: string): Promise<ApiResponse<GameTemplateData>>
updateTemplate(id: string, data: GameTemplateData): Promise<ApiResponse<void>>
createTemplate(data: GameTemplateData): Promise<ApiResponse<GameTemplateData>>
deleteTemplate(id: string): Promise<ApiResponse<void>>
```

### Backend Schema Changes

The C# `GameTemplateData`, `TemplateTile`, and `TemplateEntitlement` models
need the new optional fields. The database column (`Data` JSON) is
schema-flexible -- new fields serialize without migration. Existing
templates deserialize with defaults (null/empty for optional fields).

Add `TemplateIsland` model and `Islands` property to `GameTemplateData`.

Update `BoardInfoJsonAdapter` to pass through the new fields (they're
ignored by the current state machine but must round-trip correctly).

Update `CatanTypeGenSpec` to regenerate TypeScript types with new fields.

## Template Browser (`/templates`)

### Layout

Card grid, responsive (1 column on mobile, 2 on desktop).

```text
┌──────────────────────────────────────────┐
│  Game Templates            [+ New]       │
│  [All ▼] [Search________]               │
├──────────────────────────────────────────┤
│  ┌─────────────────┐ ┌─────────────────┐ │
│  │ Regular Game  🔒 │ │ Expansion    🔒 │ │
│  │ Base · 19 tiles  │ │ Expansion · 30  │ │
│  │ 3-4 players      │ │ tiles · 5-6     │ │
│  │ [Edit] [Clone]   │ │ [Edit] [Clone]  │ │
│  └─────────────────┘ └─────────────────┘ │
│  ┌─────────────────┐                     │
│  │ My Custom Board  │                     │
│  │ Base · 19 tiles  │                     │
│  │ [Edit] [Clone]   │                     │
│  │ [Delete]         │                     │
│  └─────────────────┘                     │
└──────────────────────────────────────────┘
```

### Behaviors

- **Category filter** -- dropdown populated from distinct categories
- **Search** -- client-side filter by name
- **New** -- navigates to `/templates/new` (editor with empty defaults)
- **Edit** -- navigates to `/templates/{id}`
- **Clone** -- POST copy with `-copy` suffix, navigate to new editor
- **Delete** -- confirmation dialog; disabled for system templates
- System templates: lock icon, no delete, can edit and clone

## Template Editor (`/templates/[id]`)

### Layout2

Two-column: form panel (left, scrollable) + interactive board (right,
fixed). On narrow screens, board stacks above the form.

```text
┌──────────────────────────────────────────────────────┐
│  ← Back    Regular Game           [Shuffle] [Save] ▾ │
├──────────────────────┬───────────────────────────────┤
│  Metadata            │                               │
│  Name: [________]    │    ┌───┐ ┌───┐ ┌───┐         │
│  Category: [Base ▼]  │   │   │ │   │ │   │          │
│  Game Type: [Reg ▼]  │  ┌───┐ ┌───┐ ┌───┐ ┌───┐    │
│                      │ │   │ │ 6 │ │ 8 │ │   │     │
│  ─── Tiles ───────── │  ┌───┐ ┌───┐ ┌───┐ ┌───┐    │
│  ▸ (0,0) Desert  -   │ │   │ │   │ │   │ │   │     │
│  ▸ (1,0) Wood    6 ← │  ┌───┐ ┌───┐ ┌───┐ ┌───┐    │
│  ▸ (2,0) Brick   8   │   │   │ │   │ │   │          │
│  ...                 │    ┌───┐ ┌───┐ ┌───┐         │
│  [+ Add Tile]        │     harbor indicators         │
│                      │     on edges                   │
│  ─── Harbors ─────── │                               │
│  ▸ (3,0) Top 3:1     │   Right-click any tile:       │
│  ...                 │   ┌──────────────────┐        │
│  [+ Add Harbor]      │   │ Resource: [Ore ▼]│        │
│                      │   │ Number:   [10  ] │        │
│  ─── Entitlements ── │   │ Island:   [main] │        │
│  Road     1W+1B      │   │ Tags:     [    ] │        │
│  Settlement 1W+1B+   │   └──────────────────┘        │
│  ...                 │                               │
│                      │                               │
│  ─── Islands ─────── │                               │
│  main: "Main Island" │                               │
│  [+ Add Island]      │                               │
│                      │                               │
│  ▸ Rules (collapsed) │                               │
├──────────────────────┴───────────────────────────────┤
│  ✓ 19 tiles · ✓ No dup coords · ⚠ 6/8 adjacent (1,0)│
└──────────────────────────────────────────────────────┘
```

### Board Interactions

| Action | Result |
|--------|--------|
| Click tile | Select tile; scroll form panel to that tile's row; highlight row |
| Right-click tile | Context menu: resource dropdown, number input, island assignment, tags |
| Click harbor indicator | Select harbor; highlight in form panel |
| Drag harbor | Move to different hex edge; update form |
| Click empty edge | Add harbor at that position (if no harbor exists) |
| Hover tile | Show coordinate tooltip `(Q, R)` |
| Mouse wheel | Zoom board in/out |
| Drag background | Pan board |

### Board → Form Sync (Bidirectional)

- Editing a tile in the context menu updates the form row instantly
- Editing a form row updates the board tile instantly
- Selecting a tile on the board scrolls and highlights the form row
- Clicking a form row highlights the tile on the board
- Adding/removing tiles in either place syncs both views

### Shuffle

The "Shuffle" button in the toolbar calls the existing balance algorithm
(client-side port or server endpoint):

1. Reads current resource distribution from template tiles
2. Shuffles resources and numbers across tile coordinates
3. Applies no-adjacent-6/8 constraint
4. Respects shuffle groups (tiles in the same `shuffleGroup` shuffle
   together, not across groups)
5. Updates template state → board and form both reflect new layout

If the balance algorithm is only available server-side (C#), add a
`POST /api/game/templates/shuffle` endpoint that accepts a
`GameTemplateData` and returns the shuffled version.

### Resource Distribution Spec

Above the tiles table, a summary bar shows:

```text
Resources: 4×Wood  3×Brick  4×Wheat  4×Sheep  3×Ore  1×Desert  [+ Gold]
```

Clicking a resource count opens a stepper (increment/decrement). Changing
the count auto-adds or removes tiles of that resource type (with default
numbers). This is a convenience shortcut -- the tile table is still the
source of truth.

### Sections

#### Metadata

- **Name** -- text input, required
- **Category** -- dropdown: Base, Expansion, Seafarers, Custom
- **Description** -- textarea
- **Game Type** -- dropdown: Regular, Expansion

#### Tiles Table

Each row shows: coordinates, resource, number, island (if any), tags.
Clicking a row highlights the tile on the board.

| Column | Type | Notes |
|--------|------|-------|
| Q, R | number | Hex coordinate (editable) |
| Resource | dropdown | Desert, Wood, Brick, Wheat, Sheep, Ore, Gold |
| Number | number | 0 for Desert, 2-12 for others |
| Type | dropdown | land, sea (default: land) |
| Island | dropdown | From islands list, or "none" |

Delete button per row. "Add Tile" appends defaults.

#### Harbors Table

| Column | Type | Notes |
|--------|------|-------|
| Q, R | number | Hex coordinate |
| Side | dropdown | Top, TopRight, BottomRight, Bottom, BottomLeft, TopLeft |
| Type | dropdown | ThreeForOne, Wood, Brick, Wheat, Sheep, Ore |

Harbors are also visualized on the board and can be dragged to new positions.

#### Entitlements Table (Enriched)

| Column | Type | Notes |
|--------|------|-------|
| Entitlement | dropdown | Road, Settlement, City, DevelopmentCard, Ship, etc. |
| Title | text | Display name |
| Cost | resource editor | Inline cost: wood, brick, wheat, sheep, ore counts |
| Icon | text/picker | Catan font glyph identifier |
| Description | text | Tooltip |

The enriched fields are forward-looking -- the current GameStateMachine
ignores them (costs are hardcoded). Seafarers will wire them to the
PurchasePanel and state machine. But the editor lets you define them now
so templates are complete.

#### Islands Section

| Column | Type | Notes |
|--------|------|-------|
| ID | text | Unique identifier |
| Name | text | Display name |
| Shuffle Group | text | Tiles with same group shuffle together |
| Discovery VP | number | VP for first settlement (0 = none) |

Tiles reference islands by ID in the tile table / context menu.

#### Rules Section (Collapsible)

`ResourceRules`: min/max players, VP to win, starting resources.
`HouseRules`: ShowPipCounts, GriefDodgy, etc.

Collapsed by default since most users won't change them.

### Behaviors2

- **Save** -- PUT to `/api/game/templates/{id}`. Disabled if validation
  errors exist. Shows success/error toast.
- **Save As** (dropdown on Save button) -- modal for new ID + name,
  POST to create copy.
- **Back** -- navigate to `/templates`. Confirmation if dirty.
- **Dirty tracking** -- `JSON.stringify` comparison. Dot on Save button.
- **New template** (`/templates/new`) -- 19 desert tiles in standard hex
  layout, default entitlements (Road, Settlement, City, DevelopmentCard
  with standard costs), no harbors, default rules.

### Validation

Continuous validation, displayed in footer bar:

| Rule | Severity |
|------|----------|
| Duplicate tile coordinates | Error |
| Non-desert tile with no number | Error |
| Desert tile with nonzero number | Warning |
| Adjacent 6 and 8 | Warning |
| Min players > max players | Error |
| No tiles | Error |
| Empty name | Error |
| Island referenced by tile but not defined | Error |
| Entitlement with zero-cost (non-standard) | Warning |
| Harbor on non-edge position | Warning |

Errors block save. Warnings are informational.

## Component Structure

```text
react-ui/
  app/
    templates/
      page.tsx                  -- Template browser
      [id]/
        page.tsx                -- Template editor
  components/
    templates/
      EditorBoard.tsx           -- Interactive hex board (click, right-click, drag)
      TileContextMenu.tsx       -- Right-click menu for tile editing
      HarborOverlay.tsx         -- Harbor indicators + drag handles
      TemplateCard.tsx          -- Card for browser list
      TileTable.tsx             -- Editable tile list with board sync
      HarborTable.tsx           -- Editable harbor list
      EntitlementTable.tsx      -- Enriched entitlement editor
      IslandTable.tsx           -- Island definitions
      RulesEditor.tsx           -- ResourceRules + HouseRules form
      ResourceDistribution.tsx  -- Summary bar with stepper controls
      ValidationBar.tsx         -- Footer validation summary
      SaveAsDialog.tsx          -- Modal for save-as
  lib/
    api/
      gameApi.ts                -- Add template CRUD functions
    templates/
      validation.ts             -- Validation logic (pure functions)
      defaults.ts               -- Default template for "new" flow
      shuffle.ts                -- Client-side shuffle (or API call wrapper)
```

## Backend Changes

### Schema (C#)

- **`TemplateEntitlement`** -- add `Title`, `Description`, `Cost`
  (dictionary), `Icon`, `PurchaseType` properties (all optional,
  defaults to null/empty)
- **`TemplateTile`** -- add `Type`, `SeaKind`, `IslandId`, `FaceDown`,
  `Tags` properties (all optional)
- **`GameTemplateData`** -- add `Islands` property (`List<TemplateIsland>`,
  defaults to empty)
- **`TemplateIsland`** -- new model: `Id`, `Name`, `ShuffleGroup`,
  `DiscoveryVp`
- **`BoardInfoJsonAdapter`** -- pass through new fields (ignored by
  current state machine)
- **`CatanTypeGenSpec`** -- add `TemplateIsland` to type generation

### Shuffle Endpoint (if needed)

If the balance algorithm can't be ported to TypeScript easily:

```text
POST /api/game/templates/shuffle
Body: GameTemplateData
Returns: GameTemplateData (shuffled)
```

Uses existing `BalancedShuffle` logic, respecting `shuffleGroup` on tiles.

### Seeding

Update `DatabaseSeeder.SeedTemplatesAsync` to populate the enriched
entitlement fields (cost, title, icon) for `regular` and `expansion`
templates. This ensures the editor shows complete data for system
templates.

## Testing Strategy

- **Validation logic** -- unit tests for all validation rules (pure
  functions, no React)
- **Schema round-trip** -- update `BoardInfoJsonAdapterTests` to verify
  new optional fields survive serialize → deserialize → adapter
- **API integration** -- extend `GameTemplateApiTests` for enriched
  entitlement CRUD
- **Manual** -- visual board interactions (click, right-click, drag,
  shuffle) tested in browser

## Risks

- **Context menu UX** -- right-click menus in web apps can conflict with
  browser context menu. Use `preventDefault` and test across browsers.
- **Harbor drag-and-drop** -- mapping screen coordinates to hex edges
  requires the same geometry math used in `GameBoard` hit-testing. The
  `hex-geometry.ts` module has the primitives.
- **Shuffle algorithm port** -- if client-side shuffle is needed, porting
  the C# `BalancedShuffle` to TypeScript is non-trivial. Server endpoint
  is the simpler path.
- **Schema migration** -- adding optional fields to `GameTemplateData` is
  safe (JSON deserialization handles missing fields), but existing seed
  data won't have enriched entitlements until re-seeded.

## What This Design Does NOT Cover

- GameStateMachine reading entitlement costs from templates (Seafarers)
- PurchasePanel data-driven from template entitlements (Seafarers)
- Integration with `/new-game` page (template picker)
- Template import/export (JSON file upload/download)
- Template sharing between users
- Play-testing a template (creating a game and running state machine
  to verify the board works) -- could be a "Test" button in a future
  iteration

---

## Addendum: Islands-Own-Tiles Model and Entitlement Max

**Date:** February 13, 2026
**Motivation:** The original design had tiles referencing islands via
`islandId`. After working through the editor UI, a better model emerged:
islands **own** their tiles, harbors, and numbers. This makes shuffle,
validation, and the editor UI all simpler. Additionally, entitlements
need a `Max` field since the game enforces per-entitlement limits.

### Problem: Entitlement Max Lives in the Wrong Place

Currently, max counts for entitlements are buried in `ResourceRules`:

```csharp
ResourceRules { MaxCities: 4, MaxSettlements: 5, MaxRoads: 15 }
```

This has two issues:

1. **Not extensible** -- adding Ship, Knight, Wall, etc. requires new
   properties on `ResourceRules` for each entitlement type
2. **Inconsistent** -- Soldier and DevCard have no max (unlimited), but
   this is implicit (they just return 0 from `MaxEntitlementCount()`)

The fix: move max to the entitlement itself.

### Problem: Tiles-Reference-Islands Is Inside-Out

The original design put `islandId` on each tile. This works but creates
friction:

- Shuffle logic must filter tiles by `islandId`, group them, shuffle
  each group separately, then reassemble
- The editor UI must maintain a parallel list of island IDs and keep
  tile `islandId` references in sync
- Deleting an island leaves orphaned `islandId` references on tiles

A better model: **islands own their tiles**. Each island is a
self-contained board fragment with its own tiles, harbors, and shuffle
semantics. Regular and Expansion games have exactly 1 island. Seafarers
has 2+. It is conceptually "two boards linked together."

### Revised C# Models

#### TemplateEntitlement (add Max)

```csharp
public class TemplateEntitlement
{
    public string Entitlement { get; set; } = "Road";
    public int? Max { get; set; }  // null = unlimited (Soldier, DevCard)
}
```

Seed values for Regular/Expansion:

| Entitlement     | Max  |
|-----------------|------|
| Road            | 15   |
| Settlement      | 5    |
| City            | 4    |
| DevelopmentCard  | null |
| Soldier         | null |

This replaces `ResourceRules.MaxCities/MaxSettlements/MaxRoads` as the
source of truth for templates. The `ResourceRules` fields remain for
backward compatibility with `IGameMetadata` and the GameStateMachine,
but templates define max per-entitlement.

#### TemplateIsland (new -- islands own tiles)

```csharp
public class TemplateIsland
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<TemplateTile> Tiles { get; set; } = [];
    public List<TemplateHarbor> Harbors { get; set; } = [];
}
```

#### GameTemplateData (revised)

```csharp
public class GameTemplateData
{
    // ... existing metadata fields unchanged ...

    public List<TemplateIsland> Islands { get; set; } = [];
    public List<TemplateEntitlement> Entitlements { get; set; } = [];

    // REMOVED: top-level Tiles and Harbors
    // These now live inside each island.
}
```

#### Backward Compatibility

Existing templates in the database have top-level `Tiles` and `Harbors`
arrays with no `Islands`. The migration strategy:

1. **Deserialization** -- keep `Tiles` and `Harbors` as optional
   properties on `GameTemplateData` with `[JsonIgnore(Condition =
   WhenWritingDefault)]` or similar. When deserializing old templates,
   these fields populate.

2. **Normalization** -- add a static method
   `GameTemplateData.Normalize()` that checks: if `Islands` is empty
   but `Tiles` is populated, wrap the flat tiles/harbors into a single
   default island:

   ```csharp
   public void Normalize()
   {
       if (Islands.Count == 0 && Tiles.Count > 0)
       {
           Islands.Add(new TemplateIsland
           {
               Id = "main",
               Name = "Main Island",
               Tiles = Tiles,
               Harbors = Harbors,
           });
           Tiles = [];
           Harbors = [];
       }
   }
   ```

3. **BoardInfoJsonAdapter** -- update to read tiles/harbors from
   `Islands` (flattened across all islands) instead of the top-level
   lists.

4. **DatabaseSeeder** -- update `BuildTemplateFromMetadata` to wrap
   tiles and harbors in a single island.

5. **Re-seeding** -- run `./catan.ps1 database install` after schema
   changes to re-seed with the new format.

### Per-Island Shuffle

Shuffle operates **per island**, not globally:

1. For each island, collect its tiles
2. Separate the resource list and number list from the tile coordinates
3. Shuffle resources across coordinates (Desert stays fixed or floats,
   depending on rules)
4. Assign numbers with the no-adjacent-6/8 constraint, evaluated
   **within that island only**
5. Repeat for each island independently

This means a Seafarers board with 2 islands shuffles each island's
resources and numbers independently. The two islands never exchange
tiles.

### Editor UI Changes

#### Island Selector (top of form panel)

```text
┌──────────────────────────────────────┐
│  Islands  [+ Add Island]             │
│  ┌──────────────────────────────┐    │
│  │ ● Main Island          [×]  │    │
│  │ ○ Far Island            [×]  │    │
│  └──────────────────────────────┘    │
└──────────────────────────────────────┘
```

- Radio buttons select the "active" island
- **Clicking a tile on the board** also selects its owning island --
  the editor finds which island contains the clicked tile and switches
  the active island automatically
- The Resource Distribution, Number Distribution, and Harbors sections
  below show data for the **active island only**
- Adding/removing tiles operates on the active island
- The board highlights tiles belonging to the active island (others
  dimmed)
- For Regular/Expansion (1 island), the selector is collapsed or hidden
- Delete button disabled when only 1 island remains

#### Right-Click Context Menus (board interactions)

Three distinct menus depending on what was right-clicked:

##### 1. Right-click a land tile (tile belongs to an island)

The active island switches to the tile's island. Menu shows:

```text
┌──────────────────────────┐
│  Resource: [Ore      ▼]  │
│  Number:   [10       ▼]  │
│  ────────────────────────│
│  Delete Tile             │
└──────────────────────────┘
```

- Resource dropdown: Desert, Wood, Brick, Wheat, Sheep, Ore, GoldMine
- Number dropdown: 2-12 (excluding 7); selecting Desert auto-sets
  number to 0
- Delete removes the tile from its island

##### 2. Right-click a sea hex adjacent to at least one land tile

This is an "expand island" action. The sea hex neighbors are checked
to find which island(s) it borders.

```text
┌──────────────────────────┐
│  Add Tile (Desert)       │
│  Add Harbor (3:1)        │
└──────────────────────────┘
```

- **Add Tile** -- adds a Desert tile at this coordinate to the
  bordering island, with number 0. The user can then right-click it
  to change resource/number.
- **Add Harbor** -- adds a ThreeForOne harbor at this coordinate to
  the bordering island, on the side facing the adjacent land tile.
  User can edit type/side in the harbors panel.
- If the sea hex borders multiple islands, the menu adds the tile to
  the **active** island (or shows a sub-menu to pick).

##### 3. Right-click a sea hex surrounded entirely by sea

This creates a brand new island.

```text
┌──────────────────────────┐
│  New Island (Desert)     │
└──────────────────────────┘
```

- Creates a new `TemplateIsland` with auto-generated ID and name
  (e.g., "Island 2", "Island 3")
- Adds a single Desert tile at this coordinate
- Switches active island to the new island
- The user then expands the island by right-clicking adjacent sea
  hexes (which will now border the new island's tile)

#### Entitlements Section (add Max column)

```text
┌────────────────────────────────────────┐
│  Entitlements (5)                      │
│  Road             Max: [15]            │
│  Settlement       Max: [5 ]            │
│  City             Max: [4 ]            │
│  DevelopmentCard   Max: [∞ ]            │
│  Soldier          Max: [∞ ]            │
│  [+ Add Entitlement]                   │
└────────────────────────────────────────┘
```

- Numeric input for Max, with a toggle or special value for "unlimited"
- Empty/null = unlimited, displayed as `∞`

### BoardInfoJsonAdapter Changes

The adapter must flatten islands back into the flat lists that
`IGameMetadata` expects:

```csharp
public List<HexCoordinates> TileKeys =>
    _template.Islands.SelectMany(i => i.Tiles)
        .Select(t => new HexCoordinates(t.Q, t.R, -t.Q - t.R))
        .ToList();

public List<ResourceType> Resources =>
    _template.Islands.SelectMany(i => i.Tiles)
        .Select(t => Enum.Parse<ResourceType>(t.Resource))
        .ToList();

public List<int> Numbers =>
    _template.Islands.SelectMany(i => i.Tiles)
        .Select(t => t.Number)
        .ToList();

public List<HarborModel> Harbors =>
    _template.Islands.SelectMany(i => i.Harbors)
        .Select(h => new HarborModel(
            new HexCoordinates(h.Q, h.R, -h.Q - h.R),
            Enum.Parse<HarborType>(h.Type),
            Enum.Parse<HexSide>(h.Side)
        )).ToList();
```

For `MaxEntitlementCount`, the adapter can read from
`TemplateEntitlement.Max`:

```csharp
// In ResourceRules or a new adapter method:
// Look up max from the entitlement list, fall back to ResourceRules
```

This is a **read path change only** -- the GameStateMachine continues
to call `ResourceRules.MaxEntitlementCount()`. A future task wires the
state machine to read from the template entitlements directly.

*Implementation order superseded by Addendum 2 below.*

### Addendum 2: Separate Board Shape from Tile Placement

> **Superseded by Addendum 4.** The algorithmic template approach
> (TileCount + distributions + LayoutType) has been replaced by
> concrete templates where each tile specifies its coordinate, resource,
> and number explicitly. Layout algorithms remain as editor tools.
> Kept here for historical reference.

**Date:** February 13, 2026
**Motivation:** The stepper UI exposed a fundamental flaw. Adding a Brick
tile via the +/- control creates `{ q: 0, r: 0, resource: "Brick" }`,
which collides with every other tile at (0,0). The root cause: the
template conflates **board shape** (where hexes are) with **tile
placement** (what resource and number go on each hex). In physical
Catan, these are two different things -- the cardboard frame vs. the
shuffled resource tiles and number tokens.

#### Two Data Structures, Two Concerns

| Concern | Data Structure | What It Defines |
|---------|---------------|-----------------|
| Board frame | Template | Shape (hex positions), distributions (resource/number pools), harbor positions, rules |
| Tile placement | GameModel | Coordinate → resource + number mapping (result of shuffle) |

The template is the **board frame**. It says "19 hexes in a spiral,
with 4 Wood, 3 Brick, 4 Wheat, 4 Sheep, 3 Ore, 1 Desert; numbers
pool is 1x2, 2x3, 2x4, ...; 9 harbors at these edges." It does NOT
say which specific hex gets which resource.

The GameModel is the **tile placement**. After shuffle, it says
"hex (0,0) has Wheat with number 6." This is what HexGrid renders.

#### Revised TemplateIsland (breaking change)

```csharp
public class TemplateIsland
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    // Board shape
    public int TileCount { get; set; }   // generates spiral coordinates

    // Distribution pools (what goes INTO the shape)
    public Dictionary<string, int> ResourceCounts { get; set; } = new();
    // e.g., { "Wood": 4, "Brick": 3, "Wheat": 4, "Sheep": 4,
    //         "Ore": 3, "Desert": 1 }

    public Dictionary<int, int> NumberCounts { get; set; } = new();
    // e.g., { 2: 1, 3: 2, 4: 2, 5: 2, 6: 2, 8: 2, 9: 2,
    //         10: 2, 11: 2, 12: 1 }

    // Harbors have explicit coordinates (printed on the frame)
    public List<TemplateHarbor> Harbors { get; set; } = [];
}
```

`TemplateTile` (with Q, R, Resource, Number) is **removed** from the
template schema. It only exists as a transient result of shuffle.

`TileCount` drives `generateSpiral(n)` which is a pure, deterministic
function that produces hex coordinates in spiral order. Since the
spiral is deterministic, harbor coordinates can reference positions
from it.

#### Spiral Layout Algorithm

```text
generateSpiral(n) → HexCoordinate[]
```

Produces coordinates in ring order:

- Ring 0: (0,0) -- 1 hex
- Ring 1: 6 hexes in clockwise order
- Ring 2: 12 hexes
- Ring N: 6N hexes

Cumulative: 1, 7, 19, 37, 61, 91...

For counts that don't fill a complete ring (e.g., 30 for Expansion),
the algorithm fills rings in order and partially fills the last ring
in spiral order.

This algorithm must produce identical results in C# and TypeScript.
It is a small pure function (~30 lines) that can be implemented in
both languages and verified with a shared test case.

#### Validation

The template enforces:

- `sum(ResourceCounts) == TileCount` -- resource pool must fill exactly
  every hex position
- `sum(NumberCounts) == TileCount - desertCount` -- number pool must
  cover every non-desert hex
- Harbors reference valid coordinates (within the spiral's output)

#### Editor Workflow

1. User adjusts resource distribution via steppers (+/- Brick, etc.)
2. `TileCount = sum(ResourceCounts)` -- auto-computed
3. `generateSpiral(TileCount)` produces coordinates
4. Board renders shape with a **default assignment**: resources
   assigned to spiral positions in distribution order (all Wood first,
   then Brick, etc.) with numbers similarly distributed
5. User clicks **Shuffle** → randomizes the assignment
6. Right-click a tile → changes that tile's resource/number in the
   current assignment. The distribution tables auto-adjust to match.
7. **Save** stores the template (distributions + shape). The current
   visual assignment is not persisted -- it's regenerated on load.

#### Game Creation (BoardInfoJsonAdapter)

At game creation time, the adapter materializes tiles:

```csharp
public List<HexCoordinates> TileKeys =>
    HexSpiralGenerator.Generate(_island.TileCount)
        .Select(c => new HexCoordinates(c.Q, c.R, -c.Q - c.R))
        .ToList();
```

The shuffle algorithm (existing C# `BalancedShuffle`) assigns resources
and numbers from the distribution pools to the spiral coordinates.
The result is a `List<TemplateTile>` that feeds the GameModel.

#### What About Custom Shapes?

Some Seafarers scenarios have non-spiral layouts (L-shapes, separated
clusters). For these, `TemplateIsland` gains an optional field:

```csharp
public List<HexCoordinates>? CustomCoordinates { get; set; }
```

If `CustomCoordinates` is non-null, it overrides the spiral. The
right-click "add tile to sea hex" interaction populates this list.
When `CustomCoordinates` is null, the spiral algorithm is used.

This is a future extension -- all current boards (Regular, Expansion)
are spirals. We can add custom coordinate support in the Seafarers
phase.

#### Breaking Change

This replaces the entire template data format. The `Tiles` and
`Harbors` top-level arrays and the `TemplateTile` type with per-tile
coordinates are removed. The database must be re-seeded:

```bash
pwsh ./catan.ps1 database install
```

No backward compatibility shim -- the old format was only in dev
databases. The seeder builds templates from `IGameMetadata` which has
the distribution data we need to populate the new format.

### Addendum 3: LayoutType Enum — Explicit Coordinate Generation

**Date:** February 13, 2026
**Motivation:** Addendum 2 assumed all islands use spiral coordinates
(`TileCount` drives `generateSpiral(n)`). But the Expansion board (30
tiles) uses a **square** column layout `[3,4,5,6,5,4,3]`, not a
spiral. `generateSpiral(30)` produces different coordinates than
ExpansionBoardInfo. Both C# (GameStateMachine) and TypeScript (HexGrid)
must use the **same** algorithm to generate coordinates from a tile
count — otherwise the visual layout won't match the game's logical
tile positions.

#### LayoutType Enum

```csharp
public enum LayoutType
{
    Spiral,   // GenerateSpiralCoordinates(TileCount)
    Square,   // GenerateSquareCoordinates(TileCount)
    Custom    // Use explicit coordinate list (future: Seafarers)
}
```

**Spiral** — ring-by-ring from center outward. Produces hexagonal
boards: 1, 7, 19, 37, 61... Partial rings fill in spiral order.
Used by Regular game (19 tiles).

**Square** — column-based compact layout. Adjacent columns differ by
at most 1 in height. Deterministically computes column heights from
tile count using hill/valley pattern search with squareness scoring.
Used by Expansion game (30 tiles → `[3,4,5,6,5,4,3]`) and RollRing
(11 tiles → `[4,3,4]`).

**Custom** — explicit coordinate list stored on the island. For
Seafarers scenarios with non-algorithmic shapes (L-shapes, separated
clusters). Future extension.

#### TemplateIsland (revised)

```csharp
public class TemplateIsland
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public LayoutType Layout { get; set; } = LayoutType.Spiral;
    public int TileCount { get; set; }
    public Dictionary<string, int> ResourceCounts { get; set; } = new();
    public Dictionary<int, int> NumberCounts { get; set; } = new();
    public List<TemplateHarbor> Harbors { get; set; } = [];
    public List<TemplateCoordinate>? CustomCoordinates { get; set; }
}
```

When `Layout` is `Spiral` or `Square`, coordinates are generated
deterministically from `TileCount`. When `Layout` is `Custom`,
`CustomCoordinates` provides the explicit list.

#### Coordinate Generation Dispatch

Both C# and TypeScript use the same dispatch:

```csharp
coordinates = layout switch {
    Spiral => GenerateSpiralCoordinates(tileCount),
    Square => GenerateSquareCoordinates(tileCount),
    Custom => customCoordinates
};
```

The algorithms are implemented identically in both languages:

- C#: `HexCoordinates.GenerateSpiralCoordinates()`,
  `HexCoordinates.GenerateSquareCoordinates()`
- TS: `getSpiralCoordinates()`, `getSquareCoordinates()`

Cross-language consistency is verified by tests:

- `SpiralCoordinates19_MatchesRegularBoardSet`
- `SquareCoordinates30_MatchesExpansionBoardSet`

#### Template Editor: Layout Dropdown

The template editor gains a **Layout** dropdown in the metadata section
(next to Game Type). Changing the layout regenerates the board preview
using the selected algorithm with the current tile count. This lets the
user see how the same tiles look in different arrangements.

#### Implementation Status

The coordinate generation algorithms (`GenerateSpiralCoordinates`,
`GenerateSquareCoordinates`, `ComputeSquareColumnHeights`) are
implemented and tested in both C# and TypeScript. The `LayoutType`
enum and `TemplateIsland.Layout` property will be added when the
template schema is implemented.

### What This Does NOT Change

- `ResourceRules` keeps `MaxCities/MaxSettlements/MaxRoads` for now
  (GameStateMachine reads these)
- `IGameMetadata` interface unchanged (adapter materializes tiles)
- No GameStateMachine changes
- No changes to game creation flow (`/new-game`)

### Addendum 4: Concrete Templates

**Date:** February 13, 2026
**Motivation:** Addenda 2 and 3 proposed algorithmic templates where
board shape is derived from `TileCount + LayoutType` at game creation
time. This creates a sync problem: both the server (GameStateMachine)
and client (HexGrid) must run the same algorithm to produce identical
coordinates. It also prevents arbitrary board shapes -- only shapes
producible by Spiral or Square algorithms are supported.

**Decision: Templates are concrete.** Each tile in a template explicitly
specifies its coordinate, resource, and number. The `GameTemplateData`
schema already supports this via `tiles: TemplateTile[]` where each
tile has `{q, r, resource, number}`. The `GameStateMachine` only
operates on concrete templates -- no algorithmic generation at game
creation time.

**Layout algorithms are editor tools.** `getSpiralCoordinates(count)`
and `getSquareCoordinates(count)` remain as utility functions for:

- The template editor's "Board Layout" dropdown (regenerates tile
  coordinates using the selected algorithm)
- UI controls like RollRing that need hex layouts
- The resource stepper (+/- buttons) which bulk-adds tiles and needs
  to place them somewhere

**What this means:**

- `Regular` and `Expansion` are stored in the database as concrete
  templates with explicit per-tile coordinates (already seeded by
  `DatabaseSeeder`)
- Custom templates created in the editor are concrete by definition
  -- the user clicks water hexes to place tiles at specific coordinates
- The `BoardInfoJsonAdapter` reads concrete tiles directly into
  `IGameMetadata` (already implemented, no changes needed)
- Arbitrary board shapes (L-shapes, islands, etc.) are now possible

**Interactive template editor features:**

- Right-click any land tile to edit resource, number, or remove it
- Click any water hex adjacent to a land tile to add a new tile
  (with flip animation)
- Tile table in left pane shows all tiles with inline editing
- Layout dropdown bulk-regenerates coordinates (editor tool)
- Flip animation (CSS 3D Y-axis rotation) when adding/removing tiles

# Implementation Plan — Seafarers Step 1: template + editor renders it

**Design:** [`.design/seafarers.md`](../seafarers.md) (Arc A, step 1).
**Goal:** **Seafarers exists as a game family** (`GameType.Seafarers`) and its
**"Heading for New Shores" board renders in the template editor** — main island,
≥1 small island, sea tiles as water, harbors (including on sea), correct layout,
pan/zoom. Creating/playing a Seafarers game is **gated "coming soon"** (steps 2–11).
Run everything **locally**. **STOP for approval before any code.**

**Verify (acceptance):** `./catan.ps1 run` → open the template browser → open
"Seafarers - Heading for New Shores" in the editor → the board renders the
land/sea/island layout with numbers and harbors; `(0,0,0)` is land (main island);
Regular/Expansion templates are unchanged.

---

## 1. Model & template changes review (the per-step gate)

Per the design's per-step gate — *these are all the underlying template + GameModel
changes this step needs*, each with its GameHash treatment:

| Area | Change this step | Notes |
|---|---|---|
| **`GameModel`** | **NONE** | Step 1 touches templates + editor only; no gameplay state. |
| **`GameHash`** | **NONE** | The hash is computed over `GameModel`, not templates. Nothing to classify this step. |
| **Enums** | **add `GameType.Seafarers`** (the *family* — one value) | Not hashed differently; `(int)GameType` isn't in the hash. Ripples into `switch (GameType)` sites → add a **defensive `default` ("not yet supported")**. Individual scenarios (New Shores, Four Islands, …) are **`Scenario` data, not `GameType` values**. `ResourceType.Sea`/`GoldMine` already exist. |
| **Template schema** (`GameTemplateData`/`TemplateTile`/`TemplateHarbor`) | **NONE** | `ShuffleGroup`/`Fixed` (D5) land in **step 3** (shuffle); `Scenario`/`PirateStart` (D2/D11) land in their steps. The New Shores board renders using only existing fields (`Q,R,Resource,Number`, harbors). |
| **New data (Seafarers-specific)** | `Default Data/Templates/seafarers.json` — a `GameTemplateData` document | Pure data, not code. The board's tiles/resources/numbers/harbors. |
| **Backend (reusable framework)** | a JSON template loader in `DatabaseSeeder` | Built-in templates as **data files**; future expansions drop a JSON. |
| **Editor (reusable framework)** | Sea-tile authorability + harbor-on-sea rendering | Fixes that benefit *any* sea-bearing template, not just Seafarers. |

**Result: Step 1 adds no `GameModel` field, no template-schema field, and nothing to
the hash** (`GameType` isn't hashed, and appending `Seafarers` leaves every `(int)`
ordinal unchanged). The persistent additions are one **data file** (Seafarers), one
**enum value** (`GameType.Seafarers`, family), and small **framework** seams (a JSON
template loader + editor sea-support + defensive `GameType` defaults). Low-risk entry.

**Decision — pure data, not a C# `BoardInfo` class.** The built-in
`RegularBoardInfo`/`ExpansionBoardInfo` are a **UWP-port remnant**; the modern
architecture stores/serves templates as `GameTemplateData` JSON and adapts them via
`BoardInfoJsonAdapter` at game-create. So Seafarers ships as **`seafarers.json`**
(exactly what the design says), seeded by a small file loader — consistent with the
epic's "expansion is data" thesis, and *simpler* (no `IGameMetadata` impl, no
`BuildTemplateFromMetadata` conversion). Regular/Expansion stay code-seeded for now;
migrating them to JSON and deleting the remnant is a clean **follow-up, out of scope
here**.

## 2. Framework vs Seafarers split (Prime rule)

- **Reusable framework:** (1) a **JSON template loader** — built-in templates become
  data files; (2) **editor** sea-support — harbors on `Sea` tiles + `Sea` as a
  first-class resource; (3) the **`GameType.Seafarers` family** + a defensive
  `default` ("not yet supported") on `GameType` switches. Any water expansion
  benefits from all three. The **scenario** layer (New Shores vs Four Islands …)
  stays data — no `GameType` value per scenario.
- **Seafarers config/test:** `seafarers.json` (the New Shores board data). It is the
  acceptance test that exercises the framework seams.

## 3. Backend changes

### 3a. `Catan3.GameService/Default Data/Templates/seafarers.json` — NEW (Seafarers data)

A hand-authored **`GameTemplateData`** document (the exact stored shape — `Id, Name,
Category, Version, Description, Engine, GameType, ResourceRules, HouseRules,
HasSupplemental, Tiles[], Harbors[], Entitlements[]`). `Default Data\**` is already
copied to output by the csproj (`Catan3.GameService.csproj:15`), so no project
change. The **New Shores board** on a cube-coord layout:

- `Id:"seafarers"`, `Name:"Seafarers - Heading for New Shores"`,
  `Category:"Seafarers"`, **`GameType:"Seafarers"`** (the new family enum, added in
  3c). `Scenario`/`ShuffleGroup`/etc. are omitted (later steps).
- **Board:** a radius-3 hex field (37 cells, `Q,R ∈ [-3,3]`, `S=-Q-R`):
  - **Main island** — a central connected land mass that **includes `(0,0,0)`**
    (~13–15 land tiles), with **1 `Desert`** and **2 `GoldMine`** tiles (D12 —
    New Shores uses fixed gold), the rest the five standard resources.
  - **≥1 small island** — a separate connected land cluster (3–5 land tiles) that is
    **fully surrounded by `Sea`** (so `ComputeIslands`, later, sees it as a distinct
    scoring island).
  - **Sea tiles** — every remaining cell is `Resource:"Sea"`, `Number:0`.
  - **Harbors** — ~9, including **at least one anchored on a `Sea` tile** adjacent to
    a small island (the case the editor currently drops — see 4a).
- **Numbers:** `0` for `Sea`/`Desert`; standard chits on land (no-adjacent-6/8 holds
  by construction — step 1 doesn't shuffle).
- **`ResourceRules`:** expansion-style limits (no `MaxShips` field yet — added step
  6/7). `HasSupplemental:false`. Standard `Entitlements`.

*(The board is only rendered in step 1 — it need not pass shuffle/balance validation
yet. The exact per-tile table is authored in the file.)*

### 3b. `Catan3.GameService/Data/DatabaseSeeder.cs` — add a JSON template loader (framework)

After the existing code-seeded candidates (`:19-28`), add a **reusable file loader**:
enumerate `Default Data/Templates/*.json` (path resolved relative to
`AppContext.BaseDirectory`), deserialize each with `JsonHelper` to `GameTemplateData`,
and `db.SaveTemplateAsync(..., isSystemTemplate:true, ...)` — the same upsert the
metadata path uses (`:28`). Idempotent (upsert by `Id`), and future expansions need
only drop a JSON file. Guard for a missing folder / malformed file with a logged
warning (don't crash seeding). Regular/Expansion remain code-seeded this step.

*(`Sea` tiles serialize as `Resource:"Sea"`, `Number:0` and round-trip through
`BoardInfoJsonAdapter` unchanged; that adapter runs only at game-create — step 2.)*

### 3c. `GameType.Seafarers` — add the family value + defensive defaults

- **`Catan3.Shared/Models/GameEnums.cs`** — add `Seafarers` to `enum GameType`
  (append at the end so existing `(int)` ordinals — and therefore all hashes and
  serialized values — are unchanged).
- **Regenerate types:** `pwsh ./catan.ps1 generate-types` (`GameType` is already
  registered in `CatanTypeGenSpec`, so the TS enum picks up `Seafarers`).
- **Ripple — add a `default` "not yet supported" arm** to each `switch (GameType)`
  in **live** code (not `DesktopApp`/`WebUI`, which are out of scope). Candidates to
  audit: `GameApiController`, `Log.cs`, `GameModelExtensions.cs`, and the react-ui
  new-game files. Most `GameType` uses are equality comparisons that don't need a
  case; only exhaustive switch expressions need the `default`. The `default` should
  fail clearly ("GameType Seafarers is not yet playable") rather than silently
  mis-handle.
- **New-Game selector** (`react-ui/components/new-game/…` + the create flow): show a
  **Seafarers** option, but **gate actual game-creation as "coming soon / not yet
  supported"** — create+play is steps 2–11. Step 1's job is that Seafarers *appears*
  as a family and its template *renders in the editor*; it must not create a broken
  game.

*(Scenario-level "supported vs not-supported" resolution — a scenario needing an
unbuilt mechanic — lands with the module registry in step 6; step 1 only needs the
family value + the defensive default.)*

## 4. Editor changes (reusable framework) — `react-ui`

### 4a. `react-ui/components/templates/EditorBoard.tsx` — render harbors on sea

The harbor list is filtered so any harbor whose computed water coord coincides with
an existing tile is **dropped** (`~:576-578`,
`harborItems.filter(item => !tileCoordSet.has(coordKey(item.coord)))`). In Seafarers
the harbor's water position **is** a real `Sea` tile, so island harbors vanish.
**Fix:** allow a harbor whose target coord is a **`Sea`** tile (only drop it when the
coord is a **land** tile). Also review the water-adjacency harbor-placement helper
(`~:523-536`) so `Sea` tiles count as water for harbor anchoring.

### 4b. `react-ui/app/templates/[id]/page.tsx` — make `Sea` first-class

- Add `'Sea'` to `RESOURCE_OPTIONS` (`~:25`) so a loaded `Sea` tile's per-tile
  `<select>` (`~:502-518`) shows a valid value and a user *can* set a tile to Sea.
  (Consider `'None'` too, but `Sea` is what Step 1 needs.)
- Force `number: 0` for `Sea` exactly like `Desert` (`~:160`, `~:507`), and exclude
  `Sea` from the number-distribution counting (`~:549-571`) — a sea hex has no chit.
- Add **`Seafarers`** to the editor's `gameType` `<select>` (`~:404-413`) so the
  seeded template's `GameType:"Seafarers"` shows correctly (the enum exists per 3c).

*(No change to `GameTile`, `HexGrid`, `WaterHex`, or `hex-geometry.ts` — `Sea` already
maps to the `TileSea` texture in `board-assets.ts:26`, and loaded coords are
preserved because the spiral/square generators only run on explicit layout change.)*

## 5. Files-modified table

| File | Change | Kind |
|---|---|---|
| `Catan3.GameService/Default Data/Templates/seafarers.json` | **new** — New Shores board as a `GameTemplateData` document | Seafarers data |
| `Catan3.GameService/Data/DatabaseSeeder.cs` | add a JSON template loader (scan `Default Data/Templates/*.json` → upsert as system) | Framework |
| `Catan3.Shared/Models/GameEnums.cs` | add `GameType.Seafarers` (append) | Framework |
| `react-ui/types/generated/models/**` | regenerated (`GameType` gains `Seafarers`) | Generated |
| `switch (GameType)` sites (live C#/TS — audit) | add `default` "not yet supported" | Framework |
| `react-ui/components/new-game/…` (selector + create flow) | show Seafarers, gate creation "coming soon" | Framework |
| `react-ui/components/templates/EditorBoard.tsx` | render harbors on `Sea` tiles; treat `Sea` as water for anchoring | Framework |
| `react-ui/app/templates/[id]/page.tsx` | `Sea` in resource options; force `number:0` for `Sea`; exclude from chit counts; add `Seafarers` to gameType select | Framework |

Generated-types **do** change this step (`GameType` enum) — run
`pwsh ./catan.ps1 generate-types`. No csproj change (`Default Data\**` is already
copied to output). `(int)GameType` ordinals are unchanged (append-only), so no hash
or serialization impact.

## 6. Verification (local)

1. `./catan.ps1 build` — Shared + GameService + react-ui compile.
2. `./catan.ps1 database install` — (re)create Cosmos-emulator containers so
   `DatabaseSeedingService` can upsert; then `./catan.ps1 run`.
3. **Seed check:** GET `http://localhost:8080/api/game/templates` (or the template
   browser at `http://localhost:3000/templates`) lists
   **"Seafarers - Heading for New Shores"** alongside Regular/Expansion.
4. **Editor render (the acceptance test):** open it in the editor
   (`/templates/<id>`):
   - main island + small island render as land tiles with numbers;
   - sea cells render as **water** (`TileSea`), no chit;
   - **harbors render, including the one(s) on a sea tile** (4a);
   - `(0,0,0)` is a land tile on the main island;
   - pan/zoom work; the `(q,r)` labels match the authored coords.
5. **Seafarers appears as a family (gated):** the New Game selector shows
   **Seafarers**; attempting to create is a clear **"coming soon / not yet
   supported"** — no broken game is created.
6. **No regression:** Regular and Expansion render/create exactly as before; the
   `GameType.Seafarers` append leaves `(int)` ordinals unchanged, so existing
   `.catan_test` replay hashes and serialized games are unaffected; the C#/TS build
   is clean (all `switch (GameType)` sites compile with the new `default`).
7. **Sea authorability (smoke):** in the editor, set a land tile's resource to `Sea`
   via the dropdown → it renders as water and its number clears.

## 7. Out of scope (explicitly deferred)

- **Creating and playing** a Seafarers game (**steps 2–11**). Step 1 adds the
  `GameType.Seafarers` family + a selector entry, but game-creation is **gated
  "coming soon."** (`BoardInfoJsonAdapter` will parse `GameType:"Seafarers"` fine
  once the enum exists, but the create/play path isn't wired.)
- `ShuffleGroup`/`Fixed` fields + sea-safe shuffle (**step 3**).
- `Scenario`/`PirateStart`/`ComputeIslands`/scenario flags (later steps).
- Full editor *authoring from scratch* (create islands/groups, prevent invalid
  combos, a "manual/None" layout mode so generators don't reshuffle hand-placed
  coords) — the design already parks this until after step 3.
- Any `GameModel`/hash/rules-engine change.

## 8. Risks / watch-items

- **Layout generators overwrite manual coords** (`getSpiralCoordinates` /
  `getSquareCoordinates`, applied on layout change). Step 1 only *loads and views*,
  so authored coords are preserved; but do **not** trigger a layout change on the
  Seafarers template while testing (that would reshuffle). Flag the "manual layout
  mode" as the first follow-up if authoring feels fragile.
- **`GameType.Expansion` placeholder** on the Seafarers template is intentional and
  temporary; step 2 retags it. Confirm nothing in step 1 creates a *game* from it.
- **Cosmos emulator required** locally; if `database install` hasn't run, the seed
  silently no-ops (containers missing). Verification step 2 covers this.
- **JSON loader hygiene:** resolve the folder from `AppContext.BaseDirectory`
  (not CWD); upsert by `Id`, so keep file `Id`s distinct from `regular`/`expansion`;
  a missing folder or malformed file logs a warning and is skipped (never crashes
  seeding). The loader runs after the code-seeded candidates.

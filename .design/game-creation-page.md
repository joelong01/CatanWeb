# Game Creation Page

**Status:** Draft  
**Date:** 2026-02-12

## Summary

- React **Game Creation** page to author metadata-driven boards (base, expansion, Seafarers).
- Store templates in **EF `GameTemplates`** table as JSON documents.
- Load templates on New Game; adapt to `IGameMetadata` via **`BoardInfoJsonAdapter`**.
- Pan/zoom hex editor with infinite sea; right-click to edit tiles/harbors; floating panels for template browser and JSON editor.
- Plan for **Electron** desktop packaging (react-ui bundled for desktop).

## Goals

- Metadata-driven boards (no hardcoded `RegularBoardInfo` / `ExpansionBoardInfo`).
- Browse, view, edit, and save templates (base, expansion, Seafarers core/full).
- Render board from template; allow pan/zoom and tile/harbor editing.
- Validate templates (resource/number counts, per-island constraints, harbors, buildable seas).
- Keep GameModel the single source of truth; typed messages; minimal changes.

## Non-Goals

- No server-side arbitrary user persistence beyond templates table.
- No DesktopApp UI changes (React only for builder).
- No Blazor board panning retrofits (React page implements pan/zoom itself).

## Current State

- `IGameMetadata` implemented by `RegularBoardInfo` and `ExpansionBoardInfo` (hardcoded lists).
- `/api/game/new` chooses `RegularBoardInfo.Default` or `ExpansionBoardInfo.Default`.
- WebUI NewGame (Blazor) posts `GameType` only; React has disabled Seafarers.
- GameModel shuffle/balance operates on single island; no sea tiles; no ships.

## Proposed Design

### Data Model & JSON Schema

Define a **`GameTemplate`** JSON document:

```json
{
  "id": "seafarers-core",
  "name": "Seafarers – Core",
  "version": 1,
  "category": "Seafarers",
  "description": "Two islands, ships, pirate",
  "minPlayers": 3,
  "maxPlayers": 6,
  "victoryPoints": 10,
  "rules": {
    "resourceRules": { "maxCities": 4, "maxSettlements": 5, "maxRoads": 15, "maxShips": 15, "minPlayers": 3, "maxPlayers": 6 },
    "entitlements": { "ship": { "lumber": 1, "wool": 1 } },
    "longestRouteIncludesShips": true,
    "pirateEnabled": true,
    "islandDiscoveryBonus": { "vp": 1 }
  },
  "islands": [
    { "id": "north", "name": "North Island", "shuffleGroup": "north" },
    { "id": "south", "name": "South Island", "shuffleGroup": "south" }
  ],
  "tiles": [
    { "q": 0, "r": 0, "type": "land", "resource": "Desert", "number": 7, "islandId": "south" },
    { "q": 1, "r": -1, "type": "sea", "seaKind": "buildable", "faceDown": false }
  ],
  "harbors": [
    { "q": 2, "r": -2, "edge": 3, "tradeType": "Any3" }
  ]
}
```

**Key types**:

- `tiles`: axial coords `q`,`r`; `type`: `land|sea`; `resource` optional for sea; `number` optional; `islandId`; `seaKind`: `buildable|blocked`; `faceDown` for exploration; `harborRef` optional.
- `islands`: `id`, `name`, `shuffleGroup` (group for per-island shuffle/balance; multiple islands can share group for combined shuffle).
- `harbors`: `q`,`r`,`edge` (0-5), `tradeType` (`Any3`, `Brick2`, etc.).
- `rules`: `resourceRules`, `entitlements` (map keyed by entitlement id; each has `cost`, `icon` {font/image, front/back glyphs or URLs}, `title`, `description`, `purchaseType`), flags for `pirateEnabled`, `longestRouteIncludesShips`, `islandDiscoveryBonus`.

### Storage

- **EF entity `GameTemplate`**: `Id` (string/slug), `Name`, `Category`, `Version`, `Json` (text), `CreatedUtc`, `UpdatedUtc`.
- **Migration** to create `GameTemplates` table (SQLite JSON text).
- **Seeding**: `regular`, `expansion`, `seafarers-core`, `seafarers-full` JSON.

### Adapter (`BoardInfoJsonAdapter`)

- Implements `IGameMetadata` by loading `GameTemplate` JSON.
- Maps `tiles` to `TileKeys`, `Resources`, `Numbers`; maps `harbors` to `HarborModel`.
- Provides island-aware grouping for shuffle/balance (extend `IGameMetadata` if needed with `IslandGroups`).

#### Compatibility (Desktop/Blazor)

- Extend `IGameMetadata` with `IslandGroups` (and sea metadata if needed); adapter supplies from JSON.
- `Regular`/`Expansion` implement a single island group for backward compatibility.
- Desktop can load templates via adapter or call GameService; GameModel carries islands/sea/ships unchanged; UI updates for ships/sea can follow later.

### React-Only Variant (Optional)

- Promote `GameTemplate` as source of truth; deprecate `RegularBoardInfo`/`ExpansionBoardInfo` classes (seed as templates).
- Replace adapter indirection with direct template consumption (`GameTemplateBoardBuilder`).
- Simplify `/api/game/new` to `templateId` only; no `GameType`.
- Remove Desktop/Blazor build/test flows; keep Shared/GameService + React tests.
- Update docs/TypeGen to reflect template-first flows.

### GameService Integration

- `/api/game/templates` (list metadata) and `/api/game/templates/{id}` (full JSON).
- `/api/game/templates/{id}` `PUT` to update JSON (dev-only; add auth guard later).
- `/api/game/new` accepts `templateId`; loads JSON → `BoardInfoJsonAdapter` → `HandleNewGameAsync`.
- Cache templates in memory; invalidate on update.

### React UI (react-ui)

- Use existing hex utils (`components/hex-grid/hex-geometry.ts`).
- Full-screen **pan/zoom** (SVG or canvas) with infinite sea grid; default tiles rendered as `sea` until template loaded.
- **Floating panels**:
  - **Templates** list (categories: Base, Expansion, Seafarers Core, Seafarers Full)
  - **Details** (name, description, rules)
  - **JSON** view/editor with validation
- **Editing**:
  - Right-click tile → context menu to set `type`, `resource`, `number`, `islandId`, `seaKind`, `faceDown`.
  - Harbor placement tool (select edge on tile).
  - Validators: resource/number counts match rules; per-island constraints; harbors on sea edges only; buildable seas.
- **Save**: `PUT /api/game/templates/{id}` with current JSON.

#### PurchasePanel (data-driven)

- Render entitlements as a **HexRing** of buttons sourced from `rules.entitlements`.
- Each button supports front/back meta display (icon/title on front, cost/details on back), using font icons (CatanFont glyph) or images (front/back URLs).
- Clicking sends `PurchaseMessage` with entitlement id; disable when resources insufficient; highlight active/selected.

### Electron Support

- Package `react-ui` with Electron (main process + preload for IPC; load local build).
- Configure GameService endpoint via env/args (default to remote service; optional local service if needed).
- Add build scripts (e.g., `pnpm electron:dev`, `pnpm electron:build`); sign/notarize if distributing.
- Reuse React components; ensure file protocol compatibility and auth flows.
- **Auto-update**: use `electron-updater` with GitHub Releases (or Azure Blob/S3) as provider; **do not commit binaries to branch**. CI publishes artifacts to Releases; app checks updates via `autoUpdater` feed URL (GH_TOKEN required for private repos); semver for versions; code-sign/notarize for macOS/Windows.

### Validation & Tooling

- Client-side schema validation (e.g., `zod` or generated TS types from Shared `TypeGen`).
- Server-side validation on save (counts, islands, harbors, entitlements).
- Optional preview of **shuffle** (simulate once) for quick feedback.

### Testing

- Unit: `BoardInfoJsonAdapter` mapping; per-island shuffle grouping.
- Integration: `/api/game/new` for each seeded template.
- UI: Vitest/Playwright for load/edit/save flow; PurchasePanel renders entitlements and button states.
- Replay: ensure Seafarers templates run through GameStateMachine without errors.

### Telemetry

- Track template load/save, validation failures, and shuffle previews (console or OTEL).

### Risks

- Large templates → render performance; mitigate with viewport culling and memoized hex layers.
- Schema drift between Shared and React; mitigate via TypeGen.

### Open Questions

- Auth for template editing (dev-only for now).
- Do we allow arbitrary user-defined templates in prod? (future)

### Milestones

1. Schema + EF + API + adapter + seeds.
2. `/api/game/new` accepts `templateId`.
3. React UI page with load/render/edit/save.
4. Island-aware shuffle/balance, tests.
5. React-only cleanup (if chosen): template-first flows, remove adapter indirection.
6. Electron packaging for `react-ui` (build/run scripts, endpoint config).

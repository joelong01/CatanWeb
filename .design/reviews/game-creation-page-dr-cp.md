# Design Review: game-creation-page

**Design:** `.design/game-creation-page.md`
**Reviewed:** 2026-02-13
**Reviewer:** GitHub Copilot
**Stage:** Design Doc (Phase 1 + Phase 2)

## Summary

Strong proposal to move board metadata into JSON templates stored in the DB, with a clean adapter to `IGameMetadata` and CRUD APIs plus a React editor. Deprecating Blazor/Desktop from the build/test matrix is called out. The plan is achievable with minimal disruption, but a few critical details need to be nailed down: JSON contract naming, deterministic tile ordering for adapter parity, migration/seeding specifics, and protecting system templates. TypeGen and client/server validation sharing also need explicit steps.

## Critical Issues

### 1) JSON contract needs explicit naming policy

**Section:** 1.2 GameTemplate JSON Schema / 1.3 Database Entity
**Issue:** The schema uses camelCase keys (`id`, `resourceRules`, `tiles[q,r]`), while `GameTemplateData` in C# will have PascalCase properties by default. `System.Text.Json` defaults to Pascal unless `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` is configured, and EF storage of the raw JSON needs a stable contract.
**Recommendation:**

- Define a `GameTemplateData` DTO with `[JsonPropertyName]` attributes or enforce `JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase` globally (and document it).
- Add a regression test that round-trips a template JSON document through `BoardInfoJsonAdapter` and back to JSON.

### 2) Deterministic tile ordering for adapter parity

**Section:** 1.4 BoardInfoJsonAdapter / Risks
**Issue:** `HandleNewGameAsync` expects parallel arrays (`TileKeys`, `Resources`, `Numbers`). The adapter currently plans to `Select` over `Tiles`, but ordering is undefined. Any reordering breaks replay tests and balance logic.
**Recommendation:**

- Define the ordering contract: sort by cube coordinates `(q,r,s)` (or axial `(q,r)` with derived `s = -q-r`) for **all** spatial collections. Apply the same stable ordering to roads (`RoadKey.TileKey` coordinate order, then `HexSide` enum order), buildings (`BuildingKey.TileKey` then `HexPosition`), and harbors (`HexCoordinates` then `HexSide`). Document the comparer.
- Add tests comparing `RegularBoardInfo.Default` vs adapter output for tile arrays, numbers, harbors, entitlements (road/building ordering too).
- Seed serialization must emit the same coordinate-ordered lists to populate the DB.

**Where & why order matters:**

- 📦 `HandleNewGameAsync` builds `GameModel` by iterating **parallel arrays** (`TileKeys`, `Resources`, `Numbers`). Index alignment must be deterministic or setup breaks.
- 🔐 `GameModel.UpdateGameHash()` consumes ordered lists; replay tests compare hashes. Any list reorder causes `.catan_test` failures (see `Tests.GameService` replay suite).
- 🔁 `BalancedShuffle`/`ValidateGame` iterate tiles in list order; unstable order can make balance checks nondeterministic.
- 🛣️ `MarkBuildableRoads`/`MarkBuildableBuildings` assign `BuildIndex` in enumeration order; deterministic ordering keeps UI highlighting and CLI expectations stable.
- 🧪 React hex geometry tests (`react-ui/components/hex-grid/hex-geometry.test.ts`) assert coordinates match C# ordering. TypeGen outputs also assume stable order when generating truth sets.
- 🗄️ Seeding/EF idempotency: JSON comparisons for `GameTemplates` should be stable to avoid drift; coordinate-ordered lists make diffs meaningful.

## Important Issues

### 3) Migration/seeding details need to be specified

**Section:** 1.3 Database Entity / 1.7 Seed Data
**Issue:** EF migrations and seeding conventions aren’t spelled out. `CatanDbContext` currently has six DbSets. `DatabaseSeeder` needs explicit insert logic and idempotency.
**Recommendation:**

- Add `DbSet<GameTemplateEntity>` to `CatanDbContext` and define the migration name in the plan.
- Extend `DatabaseSeeder` with idempotent seed for `regular` and `expansion` using serialized `RegularBoardInfo.Default` / `ExpansionBoardInfo.Default`.
- Decide where seed JSON lives (embedded resource vs generated at migration time).

### 4) Protect system templates from delete/save-as collisions

**Section:** 1.6 API Changes / Template Service
**Issue:** Deleting or overwriting `regular`/`expansion` would break default game creation and replay tests.
**Recommendation:**

- Add `IsSystemTemplate` or `IsReadOnly` to the entity and enforce in service/controller.
- Enforce unique `Id` with lowercasing and a slug validator.

### 5) Backcompat contract for `/api/game/new`

**Section:** 1.6 API Changes
**Issue:** React, CLI, and tests currently send `GameType`. Plan says `templateId` is preferred.
**Recommendation:**

- Implement mapping logic server-side: if `TemplateId` provided, use it; else map `GameType → default template id`.
- Update `NewGameMessage` (C#) and TypeGen spec (`CatanTypeGenSpec`) to include optional `templateId`.
- Add integration tests for both forms.

### 6) Build/test deprecation scope needs clarity

**Section:** 1.1 Deprecate Blazor and Desktop
**Issue:** Removing projects from `Catan.sln` and scripts affects `.vscode/tasks.json`, `catan.ps1 -Razor`, and `publish-desktop` tasks. React tests and hex geometry tests rely on `RegularBoardInfo` from Shared, not WebUI, so removal is fine but tooling must be updated.
**Recommendation:**

- Update `catan.ps1`, `build_worker.ps1`, `.vscode/tasks.json`, and CI configs in tandem.
- Consider keeping projects in the solution but exclude from default build to minimize VS/JetBrains friction.

### 7) Cache invalidation and concurrency for Template Service

**Section:** 1.5 Template Service
**Issue:** Cache must be thread-safe and invalidated on save/delete. Concurrency tokens (rowversion) are not mentioned.
**Recommendation:**

- Use `ConcurrentDictionary`/`IMemoryCache` with clear invalidation.
- Add `UpdatedAt` and optimistic concurrency token (`[Timestamp]`) to the entity.

### 8) Validation logic duplication risk

**Section:** 2.3 Validation Rules / Template Editor
**Issue:** Validation rules are listed for React. Server-side validation is required to prevent bad templates in DB.
**Recommendation:**

- Create a shared validator in `Catan3.Shared` (or a `FluentValidation`-style service) consumed by both the API and React via TypeGen.

## Suggestions

- Add `engine`→`IGameRules` factory mapping (tie into the `IGameRules` seam from `.design/game-state-machine.md`). Document default engines (`"base"`, `"seafarers"`) and where factories live.
- Add `IsSystemTemplate` to TypeGen so the UI can disable delete/rename for seeded templates.
- Consider `Version` semantics: semantic version for schema, separate for user edits; plan migrations accordingly.

## Questions

1. Should `GameType` remain on `GameTemplateData`, or be derived from categories/engine? If both exist, which wins for routing `NewGameMessage`?
2. Do we want a `GameTemplateSummary` projection that includes `UpdatedAt` for cache validation headers (ETag/If-None-Match)?
3. How will `GameTemplateService` be wired in GameService DI (singleton vs scoped) given caching concerns?

## Verification

### 1. IGameMetadata location and shape

**Design says:** Implemented by Regular/Expansion board infos.
**Actual code:** `Catan3.Shared/Models/GameModels.cs:94-108` defines `IGameMetadata`.
**Status:** Verified

### 2. Regular/Expansion board info are hardcoded singletons

**Design says:** `RegularBoardInfo.Default` / `ExpansionBoardInfo.Default` exist today.
**Actual code:** `Catan3.Shared/Models/RegularBoardInfo.cs` and `.../ExpansionBoardInfo.cs` define singletons with parallel arrays.
**Status:** Verified

### 3. `HandleNewGameAsync` consumes parallel arrays

**Design says:** Reads `TileKeys`, `Resources`, `Numbers`, `Harbors`, `PurchaseableEntitlements`.
**Actual code:** `Catan3.Shared/GameLogic/GameStateMachine.cs:64-134` (loop populating tiles), adds harbors, entitlements.
**Status:** Verified

### 4. GameService `/api/game/new` chooses Regular/Expansion

**Design says:** Controller maps `GameType` → `RegularBoardInfo.Default` or `ExpansionBoardInfo.Default`.
**Actual code:** `Catan3.GameService/Controllers/GameApiController.cs:280-292`.
**Status:** Verified

### 5. React new-game flow sends `GameType`

**Design says:** Current React UI sends `GameType` + player ids + game name.
**Actual code:** `react-ui/lib/api/gameApi.ts:191-214`, `react-ui/lib/services/GameServiceProxy.ts:279-304`, and `react-ui/app/new-game/page.tsx:156`.
**Status:** Verified

### 6. Build scripts still include Blazor/Desktop

**Design says:** catan.ps1 builds WebUI/Desktop (to be deprecated).
**Actual code:** `catan.ps1:17-2070` contains `-Razor`, `Start-WebUI`, `publish-desktop` tasks.
**Status:** Verified

### 7. TypeGen spec location

**Design says:** Add types to `CatanTypeGenSpec`.
**Actual code:** `Catan3.Shared/TypeScript/CatanTypeGenSpec.cs` exists and is used by `TypeGenRunner`.
**Status:** Verified

## Praise

- Phase breakdown is clear (template engine first, UI second), with backward compatibility explicitly called out.
- Schema aligns tightly with `IGameMetadata` (tiles, harbors, entitlements, house/resource rules) and anticipates Seafarers fields.
- Adapter design keeps `GameStateMachine` unchanged—excellent minimal-change approach.

## Follow-Up Actions

- [ ] Decide and document JSON naming policy; add tests for round-trip.
- [ ] Define tile ordering strategy and add parity tests vs `RegularBoardInfo`/`ExpansionBoardInfo`.
- [ ] Draft EF migration + `DatabaseSeeder` changes for `GameTemplates` table.
- [ ] Add `IsSystemTemplate` and guard delete/overwrite; expose via TypeGen.
- [ ] Update `NewGameMessage` (C# + TypeGen) to include optional `TemplateId`; add API integration tests.
- [ ] Update `catan.ps1`, `build_worker.ps1`, `.vscode/tasks.json`, CI to de-scope Blazor/Desktop.
- [ ] Implement shared validation library for templates (server + React).
- [ ] Define caching + concurrency approach for `GameTemplateService` and wire DI lifetime.

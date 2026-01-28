# React Codebase Refactoring Audit

**Audit Date:** 2026-01-28
**Auditor:** Claude (Opus 4.5)
**Scope:** Phase 2 of `react-refactoring-plan.md` - identify code for refactoring

## Summary

This audit identifies code that should be refactored in Phase 3 to:
1. Use the new extensions library (Phase 1 complete)
2. Follow server-driven UI architecture
3. Standardize component props to use player IDs instead of colors

---

## Files Audited

| File | Status |
|------|--------|
| [page.tsx](react-ui/app/game/[id]/page.tsx) | Audited |
| [GameBoard.tsx](react-ui/components/game/board/GameBoard.tsx) | Audited |
| [Building.tsx](react-ui/components/game/tiles/Building.tsx) | Audited |
| [Road.tsx](react-ui/components/game/tiles/Road.tsx) | Audited |

---

## Finding Categories

| Category | Count | Priority |
|----------|-------|----------|
| Props passing colors instead of IDs | 4 | High |
| Duplicate logic | 3 | High |
| Manual lookups that should use extensions | 5 | Medium |
| Entitlement checks that duplicate server logic | 3 | Medium |

---

## Detailed Findings

### 1. Building.tsx - Props Passing Colors

**Severity:** High
**Location:** [Building.tsx:44-46](react-ui/components/game/tiles/Building.tsx#L44-L46)

```typescript
/** Player colors for owned buildings */
ownerColors?: PlayerColors | null;
/** Current player colors (used for highlighting/placement/stars) */
currentPlayerColors?: PlayerColors | null;
```

**Issue:** Component receives pre-computed `PlayerColors` objects. Per the refactoring plan (Part 3.2), this should change to:

```typescript
/** Owner player ID (null if unowned) - component looks up colors via selector */
ownerId: string | null;
/** Current player ID - component looks up colors via selector for buildable state */
currentPlayerId: string;
```

**Action:** Work Unit 3.1 - Update Building.tsx props

---

### 2. Road.tsx - Props Passing Colors

**Severity:** High
**Location:** [Road.tsx:24-27](react-ui/components/game/tiles/Road.tsx#L24-L27)

```typescript
/** Player colors for owned roads */
ownerColors?: PlayerColors | null;
/** Current player colors (used for Buildable state) */
currentPlayerColors?: PlayerColors | null;
```

**Issue:** Same as Building.tsx - receives colors instead of player IDs.

**Action:** Work Unit 3.2 - Update Road.tsx props

---

### 3. GameBoard.tsx - Color Lookups Passed as Props

**Severity:** High
**Location:** GameBoard.tsx (multiple locations where Building/Road are instantiated)

The GameBoard currently looks up colors from `playerProfiles` and passes them to Building/Road components. After Work Units 3.1 and 3.2, these lookups should be removed and `ownerId`/`currentPlayerId` passed instead.

**Action:** Work Unit 3.3 - Update GameBoard.tsx

---

### 4. Duplicate neighborDirections Mapping

**Severity:** High
**Locations:**
- page.tsx (star calculation)
- GameBoard.tsx (star calculation)
- GameBoard.tsx (building adjacency)

```typescript
const neighborDirections = [
  'North', 'NorthEast', 'SouthEast', 'South', 'SouthWest', 'NorthWest'
] as Direction[];
```

**Issue:** The `HEX_DIRECTIONS` constant is already exported from `tileExtensions.ts`:

```typescript
export const HEX_DIRECTIONS: HexDirection[] = [
  'North', 'NorthEast', 'SouthEast', 'South', 'SouthWest', 'NorthWest'
];
```

**Action:** Replace all inline `neighborDirections` arrays with `HEX_DIRECTIONS` import.

---

### 5. Duplicate Star Calculation Logic

**Severity:** High
**Locations:**
- page.tsx: `starCounts` computation using `totalStars` extension
- GameBoard.tsx: `calculateStars` function duplicates same logic

**Issue:** Star calculation for building positions is implemented twice with identical logic.

**Current (GameBoard.tsx):**
```typescript
function calculateStars(buildingKey: BuildingKey): number {
  const tiles = gameModel?.tiles ?? [];
  // ... manual implementation
}
```

**Action:**
1. Create `starsForBuilding(gameModel, buildingKey)` extension in `gameModelExtensions.ts`
2. Both page.tsx and GameBoard.tsx should use this single function

---

### 6. Manual Building/Road Lookups

**Severity:** Medium
**Location:** GameBoard.tsx - buildingMap/roadMap construction

```typescript
const buildingMap = useMemo(() => {
  const map = new Map<string, BuildingModel>();
  for (const building of buildings) {
    const key = makeBuildingKeyString(building.buildingKey);
    map.set(key, building);
  }
  return map;
}, [buildings]);
```

**Issue:** Manual map construction. The `findBuilding()` extension handles aliases correctly; direct map lookup may miss alias positions.

**Action:** Use `findBuilding(buildings, key)` from extensions instead of map lookup, OR ensure map includes all alias keys.

---

### 7. Manual currentPlayer Lookup

**Severity:** Medium
**Location:** page.tsx

**Issue:** The extensions library provides `currentPlayer(gameModel)` but the game page may still have inline lookups.

**Action:** Verify all uses of `gameModel.players.find(p => p.id === gameModel.currentPlayerId)` are replaced with `currentPlayer(gameModel)`.

---

### 8. Entitlement Checks for Visibility

**Severity:** Medium
**Locations:** page.tsx

**Issue:** Code checks `currentPlayer?.unspentEntitlements?.includes('Settlement')` to determine if settlement indexes should be shown.

**Server-Driven Principle:** The server only sets `BuildingState.PossibleSettlement` when the player has the entitlement. Client should trust `buildingState`, not recompute entitlement logic.

**Action:** Work Unit 3.4 - Remove redundant entitlement checks

---

### 9. settlementIndexMap / cityUpgradeIndexMap Construction

**Severity:** Medium
**Location:** GameBoard.tsx

```typescript
const settlementIndexMap = useMemo(() => {
  // Manual construction of 1, 2, 3... indexes
}, [...]);

const cityUpgradeIndexMap = useMemo(() => {
  // Manual construction of A, B, C... indexes
}, [...]);
```

**Issue:** Index assignment logic is spread across GameBoard. This could be centralized.

**Consideration:** This is legitimate UI-only logic (server doesn't need to know display indexes). However, it should be extracted to a utility function for:
1. Testability
2. Reuse if other components need these indexes

**Action:** Extract to `buildingIndexMaps(buildings): { settlementIndexes, cityUpgradeIndexes }` utility function.

---

## Phase 1 Verification

Phase 1 (Extensions Library) is **complete** and ready for use:

| Extension File | Status | Tests |
|----------------|--------|-------|
| playerExtensions.ts | Created | Passing |
| tileExtensions.ts | Created | Passing |
| buildingExtensions.ts | Created | Passing |
| roadExtensions.ts | Created | Passing (geometry bug fixed) |
| gameModelExtensions.ts | Created | Passing |
| gameStoreHooks.ts | Created | Passing |

**Total Tests:** 413 passing

---

## Code Review Integration

The following code reviews have been completed and their findings are incorporated:

| Review | File | Status |
|--------|------|--------|
| [Phase1-Extensions-CR-Gemini.md](.code-reviews/Phase1-Extensions-CR-Gemini.md) | All extension files | Approved |
| [gameStoreHooks-cr-gemini.md](.code-reviews/gameStoreHooks-cr-gemini.md) | gameStoreHooks.ts | Approved |
| [gameStoreHooks-test-cr-gemini.md](.code-reviews/gameStoreHooks-test-cr-gemini.md) | gameStoreHooks.test.ts | Approved |

**Key Review Findings (Already Addressed):**
- `roadExtensions.ts` geometry bug: `NorthWest.BottomLeft` → `NorthWest.Bottom` (fixed)
- Missing extensions import dependency: Extensions library now exists (resolved)
- Zustand v5 shallow import path: Verified correct (`zustand/shallow`)

---

## Recommended Work Unit Order

Based on dependencies and impact:

### Phase 1.5: Post-Audit Extension Port (NEW)

Complete the extension library before refactoring client code.

| Order | Work Unit | Description | Priority |
|-------|-----------|-------------|----------|
| 1.5.1 | Fix `totalStars` | Use `tile.stars` instead of recalculating | High |
| 1.5.2 | Add `starsForBuilding` | UI utility for building star count | High |
| 1.5.3 | Port `FindAdjacentHarbor` | Harbor trade options | High |
| 1.5.4 | Port `OwnedAdjacentRoadsNotCounted` | Longest road calculation | High |
| 1.5.5 | Port `PurchaseModel` | Purchase requirements | Medium |
| 1.5.6 | Port `ResourcesForBuilding` | Resources a building produces | Medium |
| 1.5.7 | Port `Resources(BuildingModel)` | Building resource production | Medium |
| 1.5.8 | Port `ToResourceCardType` | Tile to card resource conversion | Low |

### Phase 3 Work Units

| Order | Work Unit | Description | Dependencies |
|-------|-----------|-------------|--------------|
| 1 | 3.1 | Update Building.tsx props | gameStoreHooks.ts (done) |
| 2 | 3.2 | Update Road.tsx props | gameStoreHooks.ts (done) |
| 3 | 3.3 | Update GameBoard.tsx | Work Units 3.1, 3.2 |
| 4 | 3.4 | Remove redundant entitlement checks | None |
| 5 | New | Extract star calculation utility | Phase 1.5.2 |
| 6 | New | Extract building index map utilities | None |
| 7 | New | Replace neighborDirections with HEX_DIRECTIONS | tileExtensions.ts (done) |

---

## Files to Modify (Summary)

| File | Changes |
|------|---------|
| Building.tsx | Replace color props with ID props, add usePlayerColors |
| Road.tsx | Replace color props with ID props, add usePlayerColors |
| GameBoard.tsx | Pass IDs not colors, use extensions, remove duplicate logic |
| page.tsx | Use extensions, remove entitlement checks, use HEX_DIRECTIONS |
| gameModelExtensions.ts | Add starsForBuilding() |
| gameStoreHooks.ts | Ensure usePlayerColors is exported (already done) |

---

## Verification After Phase 3

After each work unit:
- [ ] `npm run build` passes
- [ ] `npm run test:run` passes (413+ tests)
- [ ] `npm run lint` passes
- [ ] Manual: Start game, verify building/road rendering
- [ ] Manual: Verify colors update correctly during gameplay

---

## C# Extension Port Completeness Audit

Phase 1 ported a subset of C# extensions. This section documents what was ported vs what's missing.

### BuildingModelExtensions.cs

| C# Method | TypeScript | Status |
|-----------|------------|--------|
| `GetAutomationId` | - | Skip (testing) |
| `Aliases(BuildingKey)` | `buildingKeyAliases` | ✅ Ported |
| `AdjacentBuildings` | `adjacentBuildings` | ✅ Ported |
| `FindBuildingModel` | `findBuilding` | ✅ Ported |
| `GetBuildingOrThrow` | - | Skip (throws, not React-idiomatic) |
| `BuildingsInTile` | `buildingsInTile` | ✅ Ported |
| `OwnedBuildings` | `ownedBuildings` | ✅ Ported |
| **`Resources(BuildingModel, ResourceType)`** | - | ❌ **MISSING** |

### TileModelExtensions.cs

| C# Method | TypeScript | Status |
|-----------|------------|--------|
| `GetTileAutomationId` | - | Skip (testing) |
| `GetTileNumberAutomationId` | - | Skip (testing) |
| `GetTileResourceAutomationId` | - | Skip (testing) |
| `TileFromCoords` | `tileFromCoords` | ✅ Ported |
| `AdjacentTiles` | `adjacentTiles` | ✅ Ported |
| `Stars(IEnumerable)` | `totalStars` | ⚠️ Bug: recalculates instead of using `tile.stars` |
| `TilesWithNumber` | `tilesWithNumber` | ✅ Ported |
| `TilesWithResource` | `tilesWithResource` | ✅ Ported |
| `TilesWithSixOrEight` | `tilesWithSixOrEight` | ✅ Ported |
| **`ToResourceCardType`** | - | ❌ **MISSING** |
| `GetRobberTargetAutomationId` | - | Skip (testing) |

### RoadModelExtensions.cs

| C# Method | TypeScript | Status |
|-----------|------------|--------|
| `GetAutomationId` | - | Skip (testing) |
| **`OwnedAdjacentRoadsNotCounted`** | - | ❌ **MISSING** (longest road calc) |
| `AdjacentRoads` | `adjacentRoads` | ✅ Ported |
| `FindRoad` | `findRoad` | ✅ Ported |
| `Aliases(RoadKey)` | `roadKeyAlias` | ✅ Ported |
| `InsertSorted` | - | Skip (C# collection utility) |

### PlayerModelExtensions.cs

| C# Method | TypeScript | Status |
|-----------|------------|--------|
| `PlayerFromId` | `playerFromId` | ✅ Ported |

### GameModelExtensions.cs

| C# Method | TypeScript | Status |
|-----------|------------|--------|
| `CreateNew` | - | Skip (server-only) |
| `AllocationPhase` | `isAllocationPhase` | ✅ Ported |
| `NextRandom` | - | Skip (server-only) |
| `Phase` | `gamePhase` | ✅ Ported |
| **`PurchaseModel(Entitlement)`** | - | ❌ **MISSING** |
| `ComputeGameHash` | - | Skip (server-only) |
| `UpdateGameHash` | - | Skip (server-only, mutates) |
| `AdjacentRoads(BuildingKey)` | `adjacentRoadsForBuilding` | ✅ Ported |
| `AdjacentBuildings(RoadKey)` | `adjacentBuildingsForRoad` | ✅ Ported |
| `BuildingBetweenRoads` | `buildingBetweenRoads` | ✅ Ported |
| `NextPlayerId` | `nextPlayerId` | ✅ Ported |
| `ChangePlayer` | - | Skip (server-only, mutates) |
| `ChangePlayerTo` | - | Skip (server-only, mutates) |
| `TilesForBuildings` | `tilesForBuilding` | ✅ Ported |
| `CurrentPlayer` | `currentPlayer` | ✅ Ported |
| **`FindAdjacentHarbor`** | - | ❌ **MISSING** |
| **`ResourcesForBuilding`** | - | ❌ **MISSING** |
| `Validate` | - | Skip (server-only) |
| `SaveFileName` | - | Skip (server-only) |
| `Shuffle` | - | Skip (server-only) |
| `ValidateGame` | - | Skip (server-only) |
| `ValidateBalance` | - | Skip (server-only) |
| `ValidateNoClumps` | - | Skip (server-only) |
| `BalancedShuffle` | - | Skip (server-only) |

---

## Post-Audit: Missing Extensions to Port

These extensions should be ported in a post-audit pass before Phase 3 refactoring:

| Extension | Source File | Target File | Purpose | Priority |
|-----------|-------------|-------------|---------|----------|
| `Resources(BuildingModel, ResourceType)` | BuildingModelExtensions.cs | buildingExtensions.ts | Resources produced by building type | Medium |
| `ToResourceCardType` | TileModelExtensions.cs | tileExtensions.ts | Convert tile resource to card type | Low |
| `OwnedAdjacentRoadsNotCounted` | RoadModelExtensions.cs | roadExtensions.ts | Longest road calculation | High |
| `PurchaseModel(Entitlement)` | GameModelExtensions.cs | gameModelExtensions.ts | Purchase requirements for entitlements | Medium |
| `FindAdjacentHarbor` | GameModelExtensions.cs | gameModelExtensions.ts | Find harbor adjacent to building | High |
| `ResourcesForBuilding` | GameModelExtensions.cs | gameModelExtensions.ts | Resources a building produces | Medium |

### Bug Fix Required

| Issue | File | Fix |
|-------|------|-----|
| `totalStars` recalculates pips | tileExtensions.ts | Use `tile.stars` property instead of `pipsForNumber(tile.number)` |

### New UI Utility (Not from C#)

| Function | File | Purpose |
|----------|------|---------|
| `starsForBuilding(gameModel, buildingKey)` | gameModelExtensions.ts | Sum stars for tiles adjacent to building (UI convenience) |

---

## Notes

1. **Building.tsx is well-structured** - Component is properly memoized with `React.memo`, handles visual states correctly, and has good JSDoc. Only the props interface needs updating.

2. **Road.tsx is well-implemented** - SVG polygon geometry is correct, properly handles `roadState === 'Unowned'` by returning null, gradient IDs are correctly unique per color combination.

3. **No Harbor.tsx found** - Harbor component may not exist yet or may be rendered differently. Not in scope for this audit.

4. **NEUTRAL_COLORS pattern is good** - Both Building and Road define fallback neutral colors. This should be moved to a shared location during refactoring.

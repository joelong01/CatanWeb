# React Codebase Refactoring Plan

## Overview

This document captures two major refactoring efforts for the React UI:

1. **Common Extensions Library** - Port C# extension methods to TypeScript for consistent, reusable code
2. **Server-Driven UI Architecture** - Remove client-side business logic, trust GameModel state

Both efforts share a common goal: **reduce duplication, improve consistency, and make the codebase easier to maintain**.

## Related Documents

| Document | Purpose |
|----------|---------|
| [react-game-page.md](react-game-page.md) | Design doc with Server-Driven UI Architecture section |
| [react-game-page.md#server-driven-ui-architecture](react-game-page.md#server-driven-ui-architecture) | Architecture principles this plan implements |
| [react-game-page.md#building-rendering-data-requirements](react-game-page.md#building-rendering-data-requirements) | Definitive Building props specification |
| [react-game-page.md#road-rendering-data-requirements](react-game-page.md#road-rendering-data-requirements) | Definitive Road props specification |
| [react-game-page.md#player-color-selectors](react-game-page.md#player-color-selectors) | Selector pattern for player colors |

**This plan implements the architecture defined in the design doc. The design doc is the source of truth for "what"; this plan is the source of truth for "how" and "when".**

---

## Code Quality Standards

All code written as part of this refactoring must meet best practice standards:

### Documentation Requirements

- **JSDoc comments** on all exported functions with:
  - `@param` for each parameter with type and description
  - `@returns` describing the return value
  - `@throws` if the function can throw
  - `@example` for non-trivial functions
- **Inline comments** explaining "why" for non-obvious logic
- **Type annotations** on all function signatures (no implicit `any`)

### Example: Well-Documented Extension Function

```typescript
/**
 * Finds a building by its key, handling alias positions.
 *
 * Buildings can exist at multiple equivalent positions (aliases) due to
 * hex geometry. This function checks both the primary key and all aliases.
 *
 * @param buildings - Array of building models to search
 * @param key - The building key to find
 * @returns The matching BuildingModel, or undefined if not found
 *
 * @example
 * const building = findBuilding(gameModel.buildings, {
 *   hexCoordinates: { q: 0, r: 0, s: 0 },
 *   position: 'TopRight'
 * });
 */
export function findBuilding(
  buildings: BuildingModel[],
  key: BuildingKey
): BuildingModel | undefined {
  // First try direct match
  const direct = buildings.find(b => buildingKeysEqual(b.buildingKey, key));
  if (direct) return direct;

  // Check alias positions (same vertex, different hex reference)
  for (const alias of buildingKeyAliases(key)) {
    const aliasMatch = buildings.find(b =>
      buildingKeysEqual(b.buildingKey, alias)
    );
    if (aliasMatch) return aliasMatch;
  }

  return undefined;
}
```

### Testing Requirements

- **Every extension function MUST have tests**
- Test files located at `react-ui/lib/extensions/__tests__/`
- Minimum test coverage:
  - Happy path (normal operation)
  - Edge cases (empty arrays, null values)
  - Alias handling (critical for building/road lookups)
- Use descriptive test names: `"findBuilding returns undefined for empty array"`

### Example: Test File Structure

```typescript
// react-ui/lib/extensions/__tests__/buildingExtensions.test.ts
import { describe, it, expect } from 'vitest';
import {
  findBuilding,
  buildingKeyAliases,
  adjacentBuildings,
} from '../buildingExtensions';

describe('buildingExtensions', () => {
  describe('findBuilding', () => {
    it('returns undefined for empty buildings array', () => {
      const result = findBuilding([], mockBuildingKey);
      expect(result).toBeUndefined();
    });

    it('finds building by direct key match', () => {
      const buildings = [mockBuilding];
      const result = findBuilding(buildings, mockBuilding.buildingKey);
      expect(result).toBe(mockBuilding);
    });

    it('finds building via alias position', () => {
      // TopRight at (0,0,0) is same vertex as BottomRight at neighbor
      const buildings = [mockBuildingAtAlias];
      const result = findBuilding(buildings, primaryKey);
      expect(result).toBe(mockBuildingAtAlias);
    });
  });

  describe('buildingKeyAliases', () => {
    it('returns correct aliases for TopRight position', () => {
      const aliases = buildingKeyAliases({
        hexCoordinates: { q: 0, r: 0, s: 0 },
        position: 'TopRight',
      });
      expect(aliases).toHaveLength(2);
      // Verify specific alias positions...
    });
  });
});
```

### Hooks Testing

- Store hooks tested with mock Zustand store
- Test that hooks return correct values from store state
- Test that hooks handle missing/null data gracefully

### C# to TypeScript Patterns

When porting C# extension methods, follow these patterns:

#### `out` Parameters → Return Objects

C# uses `out` parameters for multiple return values. In TypeScript, return an object instead:

```csharp
// C# pattern
public static List<RoadModel> OwnedAdjacentRoadsNotCounted(
    this GameModel gameModel,
    RoadModel road,
    List<RoadModel> owned,
    RoadModel? blockedFork,
    out bool adjacentFork)  // <-- out parameter
```

```typescript
// TypeScript equivalent - return an object
interface AdjacentRoadsResult {
  roads: RoadModel[];
  adjacentFork: boolean;
}

export function ownedAdjacentRoadsNotCounted(
  gameModel: GameModel,
  road: RoadModel,
  owned: RoadModel[],
  blockedFork: RoadModel | null
): AdjacentRoadsResult {
  // ... implementation
  return { roads: result, adjacentFork };
}
```

#### LINQ → Native Array Methods

Use native JavaScript array methods, not lodash:

| C# LINQ | TypeScript |
|---------|------------|
| `.Where(x => ...)` | `.filter(x => ...)` |
| `.Select(x => ...)` | `.map(x => ...)` |
| `.FirstOrDefault(x => ...)` | `.find(x => ...)` |
| `.Any(x => ...)` | `.some(x => ...)` |
| `.All(x => ...)` | `.every(x => ...)` |
| `.Count()` | `.length` |

**Important:** `.find()` returns `undefined` (not `null`) when no match. Handle accordingly.

### Error Handling Pattern

**React-idiomatic: Return `undefined`, let caller handle it.**

```typescript
// CORRECT: Return undefined for missing items
export function findBuilding(
  buildings: BuildingModel[],
  key: BuildingKey
): BuildingModel | undefined {
  // ... search logic
  return undefined;  // Not found
}

// Usage: Caller handles undefined
const building = findBuilding(buildings, key);
if (!building) {
  // Handle missing case - render nothing, show error, etc.
  return null;
}
```

**Do NOT throw exceptions for "not found" cases.** In React, a thrown exception in a component crashes the component tree. Return `undefined` and handle gracefully.

**Exception:** For true logic errors that should never happen in production (and indicate a bug), you MAY throw. But prefer `console.error` + graceful degradation over crashes.

### Zustand Selector Performance

When selectors return objects, use a custom equality function to prevent unnecessary re-renders.

**Problem:** Returning a new object every render breaks `React.memo`:

```typescript
// BAD: Creates new object reference every time
export function usePlayerColors(playerId: string | null): PlayerColors {
  return useGameStore((state) => {
    // This object is NEW every call, even if colors unchanged
    return { primary: '...', secondary: '...', foreground: '...' };
  });
}
```

**Solution:** Use a custom equality function that compares the actual values:

```typescript
// GOOD: Custom equality prevents unnecessary re-renders
function colorsEqual(a: PlayerColors, b: PlayerColors): boolean {
  return a.primary === b.primary &&
         a.secondary === b.secondary &&
         a.foreground === b.foreground;
}

export function usePlayerColors(playerId: string | null): PlayerColors {
  return useGameStore(
    (state) => {
      if (!playerId) return NEUTRAL_COLORS;
      const profile = state.playerProfiles.get(playerId);
      if (!profile) return NEUTRAL_COLORS;
      return {
        primary: profile.primaryColor,
        secondary: profile.secondaryColor,
        foreground: profile.foregroundColor,
      };
    },
    colorsEqual  // Custom equality function
  );
}
```

**Alternative:** Return a stable reference by caching:

```typescript
// ALSO GOOD: Memoize the color object
const colorCache = new Map<string, PlayerColors>();

export function usePlayerColors(playerId: string | null): PlayerColors {
  return useGameStore((state) => {
    if (!playerId) return NEUTRAL_COLORS;
    const profile = state.playerProfiles.get(playerId);
    if (!profile) return NEUTRAL_COLORS;

    // Return cached object if colors unchanged
    const cacheKey = `${profile.primaryColor}|${profile.secondaryColor}|${profile.foregroundColor}`;
    if (!colorCache.has(cacheKey)) {
      colorCache.set(cacheKey, {
        primary: profile.primaryColor,
        secondary: profile.secondaryColor,
        foreground: profile.foregroundColor,
      });
    }
    return colorCache.get(cacheKey)!;
  });
}
```

---

## Part 1: Common Extensions Library

### 1.1 Goal

Port the C# extension pattern to TypeScript, providing reusable utility functions that match the server-side API. This ensures:

- Consistent behavior between Blazor and React
- Single source of truth for algorithms (alias handling, adjacency, etc.)
- Discoverable, documented utilities instead of ad-hoc inline code

### 1.2 Files to Create

```text
react-ui/lib/extensions/
├── index.ts                    // Re-exports all extensions
├── playerExtensions.ts         // Player lookup utilities
├── buildingExtensions.ts       // Building lookup, adjacency, aliases
├── roadExtensions.ts           // Road lookup, adjacency, aliases
├── tileExtensions.ts           // Tile lookup, filtering, star calculation
└── gameModelExtensions.ts      // High-level game queries
```

### 1.3 Extension Methods to Port

#### playerExtensions.ts

| C# Method | TypeScript Function | Purpose |
|-----------|---------------------|---------|
| `PlayerFromId(players, id)` | `playerFromId(players, id)` | Find player by ID |

#### buildingExtensions.ts

| C# Method | TypeScript Function | Purpose |
|-----------|---------------------|---------|
| `FindBuildingModel(buildings, key)` | `findBuilding(buildings, key)` | Find building, handling aliases |
| `GetBuildingOrThrow(buildings, key)` | `getBuildingOrThrow(buildings, key)` | Find or throw |
| `AdjacentBuildings(buildings, key)` | `adjacentBuildings(buildings, key)` | Buildings within one position |
| `BuildingsInTile(buildings, coords)` | `buildingsInTile(buildings, coords)` | All buildings in a hex |
| `OwnedBuildings(buildings, coords)` | `ownedBuildings(buildings, coords)` | Owned buildings in a hex |
| `Aliases(key)` | `buildingKeyAliases(key)` | Get alias positions for a building key |
| `Resources(model, resource)` | `buildingResources(model, resource)` | Resources generated (1 for settlement, 2 for city) |

#### roadExtensions.ts

| C# Method | TypeScript Function | Purpose |
|-----------|---------------------|---------|
| `FindRoad(roads, key)` | `findRoad(roads, key)` | Find road, handling aliases |
| `AdjacentRoads(roads, key)` | `adjacentRoads(roads, key)` | Roads sharing a vertex with this road |
| `Aliases(key)` | `roadKeyAliases(key)` | Get alias positions for a road key |

#### tileExtensions.ts

| C# Method | TypeScript Function | Purpose |
|-----------|---------------------|---------|
| `TileFromCoords(tiles, coords)` | `tileFromCoords(tiles, coords)` | Find tile by coordinates |
| `AdjacentTiles(tiles, tile)` | `adjacentTiles(tiles, tile)` | Get 6 neighboring tiles |
| `Stars(tiles)` | `totalStars(tiles)` | Sum of pip values |
| `TilesWithNumber(tiles, n)` | `tilesWithNumber(tiles, n)` | Filter by roll number |
| `TilesWithResource(tiles, r)` | `tilesWithResource(tiles, r)` | Filter by resource type |
| `TilesWithSixOrEight(tiles)` | `tilesWithSixOrEight(tiles)` | Filter for high-probability tiles |

#### gameModelExtensions.ts

| C# Method | TypeScript Function | Purpose |
|-----------|---------------------|---------|
| `CurrentPlayer(game)` | `currentPlayer(game)` | Get current player model |
| `AllocationPhase(game)` | `isAllocationPhase(game)` | Check if in allocation phase |
| `Phase(game)` | `gamePhase(game)` | Get current GamePhase enum |
| `AdjacentRoads(game, buildingKey)` | `roadsAdjacentToBuilding(game, key)` | Roads touching a building |
| `AdjacentBuildings(game, roadKey)` | `buildingsAdjacentToRoad(game, key)` | Buildings at road endpoints |
| `TilesForBuildings(game, key)` | `tilesForBuilding(game, key)` | Tiles a building touches |
| `FindAdjacentHarbor(game, key)` | `findAdjacentHarbor(game, key)` | Harbor touching a building |
| `BuildingBetweenRoads(game, r1, r2)` | `buildingBetweenRoads(game, r1, r2)` | Building at road junction |

### 1.4 Zustand Hooks (Store Extensions)

Separate from pure functions, create hooks that access the store:

```text
react-ui/lib/stores/
├── gameStore.ts               // Core store (existing)
├── gameStoreHooks.ts          // Derived hooks (new)
└── index.ts                   // Re-exports
```

#### gameStoreHooks.ts

| Hook | Purpose |
|------|---------|
| `usePlayerColors(playerId)` | Get PlayerColors for a player |
| `usePlayerGradient(playerId)` | Get CSS gradient string |
| `usePlayerForeground(playerId)` | Get foreground color |
| `useCurrentPlayer()` | Get current player model |
| `useIsMyTurn()` | Check if local player's turn |
| `useGamePhase()` | Get current GamePhase |
| `useIsAllocationPhase()` | Check if in allocation phase |

### 1.5 Analysis Phase

Before implementing, analyze existing code to find:

1. **Duplicate implementations** - Same logic in multiple places
2. **Ad-hoc alias handling** - Building/road lookups that don't handle aliases
3. **Inline star calculations** - Should use centralized function
4. **Player lookups** - `.find(p => p.id === ...)` patterns

**Files to analyze:**

- [ ] `react-ui/app/game/[id]/page.tsx` - Main game page
- [ ] `react-ui/components/game/board/GameBoard.tsx` - Board rendering
- [ ] `react-ui/components/game/tiles/Building.tsx` - Building component
- [ ] `react-ui/components/game/tiles/Road.tsx` - Road component
- [ ] `react-ui/components/game/panels/*.tsx` - All panel components
- [ ] `react-ui/components/game/controls/*.tsx` - All control components

---

## Part 2: Server-Driven UI Architecture

### 2.1 Goal

The React UI should **render GameModel state**, not compute business logic. The GameStateMachine is the single source of truth.

### 2.2 Current Problems

| Problem | Location | Impact |
|---------|----------|--------|
| `showSettlementIndexes` computed from entitlements | page.tsx | Duplicates server logic |
| Settlement index map (1, 2, 3...) built client-side | GameBoard.tsx | Logic scattered, should be centralized |
| City upgrade index map (A, B, C...) built client-side | GameBoard.tsx | Logic scattered, should be centralized |
| Star calculation duplicated | GameBoard.tsx, page.tsx | Should use centralized extension method |
| Entitlement checks for visibility | Multiple files | Server already encodes this in state |

### 2.3 Server Model Changes Required

**None.** The server is already authoritative for `BuildingState` and `RoadState`. Visual helpers like `Stars` and `BuildIndex` (1, 2, 3 / A, B, C) are purely for UI convenience and will be computed client-side.

### 2.4 Client Code to Refactor

| Current Code | New Approach |
|--------------|--------------|
| `showSettlementIndexes` computation | Check if any `buildingState === 'PossibleSettlement'` |
| `settlementIndexMap` construction | Use centralized client hook/utility |
| `cityUpgradeIndexMap` construction | Use centralized client hook/utility |
| `calculateStars()` function | Use `tileExtensions.totalStars` |
| Entitlement checks for visibility | Trust `buildingState`/`roadState` values |

### 2.5 Legitimate Client-Only State

These should remain client-side:

| State | Reason |
|-------|--------|
| `starFilter` | User preference |
| `resourceFilter` | User preference |
| `lastRolledNumber` + 5s timer | Transient visual effect |
| `isHovered`, `isPressed` | UI interaction |
| Panel positions/sizes | Layout preference |
| `pendingRobberCoords` | Optimistic UI before server confirms |

---

## Part 3: Visual Component Props Standardization

### 3.1 Goal

Define **comprehensive, well-designed props interfaces** for visual components (Building, Road, Harbor). Props should be:

1. **Complete** - All state needed to render the component
2. **Minimal** - No redundant or derived values that can be computed internally
3. **ID-based** - Pass player IDs, not pre-computed colors; component looks up via selectors
4. **Documented** - Props interface serves as the contract for what the component needs

This replaces the current ad-hoc approach where props accumulated organically as features were added.

### 3.2 Building Component Props

**Definitive `BuildingProps` interface:**

```typescript
interface BuildingProps {
  // === Server State (from BuildingModel) ===
  /** Building state determines glyph and behavior */
  buildingState: BuildingState;
  /** Owner player ID (null if unowned) - component looks up colors via selector */
  ownerId: string | null;

  // === Derived State (computed by parent or server) ===
  /** Visual state for rendering mode */
  visualState: BuildingVisualState;
  /** Star count for this position (sum of adjacent tile pips) */
  stars: number;
  /** Build index label ("1", "2"... or "A", "B"...) when buildable */
  buildIndex?: string;

  // === Context (from GameModel) ===
  /** Current player ID - component looks up colors via selector for buildable state */
  currentPlayerId: string;

  // === Layout ===
  /** Size in pixels (diameter) */
  size: number;

  // === Interaction ===
  /** Click handler for buildable spots */
  onClick?: () => void;

  // === Styling ===
  /** Additional CSS classes */
  className?: string;
}
```

**What the component does internally:**

```typescript
function Building({ ownerId, currentPlayerId, visualState, ... }: BuildingProps) {
  // Look up colors via selectors - component subscribes to store changes
  const ownerColors = usePlayerColors(ownerId);
  const currentColors = usePlayerColors(currentPlayerId);

  // Determine which colors to use based on visual state
  const colors = (visualState === 'Highlighted' || visualState === 'Stars')
    ? currentColors
    : ownerColors;

  // Track local UI state
  const [isHovered, setIsHovered] = useState(false);

  // Render based on props + internal state
}
```

### 3.3 Road Component Props

**Definitive `RoadProps` interface:**

```typescript
interface RoadProps {
  // === Server State (from RoadModel) ===
  /** Road state determines visibility and styling */
  roadState: RoadState;
  /** Owner player ID (null if unowned) - component looks up colors via selector */
  ownerId: string | null;
  /** Build index (1, 2, 3...) when roadState is 'Buildable' */
  buildIndex: number;

  // === Context (from GameModel) ===
  /** Current player ID - component looks up colors via selector for buildable state */
  currentPlayerId: string;

  // === Geometry ===
  /** Which edge of the hex this road is on */
  side: HexSide;
  /** Hex size (circumradius) for scaling */
  hexSize: number;

  // === Interaction ===
  /** Click handler for buildable roads */
  onClick?: () => void;

  // === Styling ===
  /** Additional CSS classes */
  className?: string;
}
```

**What the component does internally:**

```typescript
function Road({ ownerId, currentPlayerId, roadState, ... }: RoadProps) {
  // Look up colors via selectors
  const ownerColors = usePlayerColors(ownerId);
  const currentColors = usePlayerColors(currentPlayerId);

  // Determine which colors to use based on road state
  const colors = roadState === 'Buildable' ? currentColors : ownerColors;

  // Unowned roads don't render
  if (roadState === 'Unowned') return null;

  // Render polygon with appropriate colors and opacity
}
```

### 3.4 Harbor Component Props

**Definitive `HarborProps` interface:**

```typescript
interface HarborProps {
  // === Server State (from HarborModel) ===
  /** Harbor type determines icon and trade ratio */
  harborType: HarborType;
  /** Position on the hex edge */
  side: HexSide;

  // === Geometry ===
  /** Hex coordinates for positioning */
  coordinates: HexCoordinates;
  /** Hex size for scaling */
  hexSize: number;

  // === Interaction ===
  /** Click handler (if harbors are interactive) */
  onClick?: () => void;

  // === Styling ===
  /** Additional CSS classes */
  className?: string;
}
```

### 3.5 Props Design Principles

| Principle | Rationale |
|-----------|-----------|
| Pass `playerId` not `PlayerColors` | Single source of truth in store; automatic updates when colors change |
| Pass `buildingState` not derived booleans | Server is authoritative; component derives visuals from state |
| Pass `visualState` explicitly | Parent/container determines rendering mode based on game context |
| Include all geometry props | Component should be self-contained for positioning |
| Keep interaction handlers optional | Not all contexts require interactivity |

### 3.6 Migration: Current vs Target Props

#### Building.tsx

| Current Prop | Target Prop | Change |
|--------------|-------------|--------|
| `ownerColors?: PlayerColors` | `ownerId: string \| null` | Pass ID, use selector |
| `currentPlayerColors?: PlayerColors` | `currentPlayerId: string` | Pass ID, use selector |
| `buildingState` | `buildingState` | Keep (server state) |
| `visualState` | `visualState` | Keep (derived by parent) |
| `stars` | `stars` | Keep (derived or server-provided) |
| `buildIndex` | `buildIndex` | Keep (derived or server-provided) |
| `size` | `size` | Keep (layout) |
| `onClick` | `onClick` | Keep (interaction) |
| `className` | `className` | Keep (styling) |

#### Road.tsx

| Current Prop | Target Prop | Change |
|--------------|-------------|--------|
| `ownerColors?: PlayerColors` | `ownerId: string \| null` | Pass ID, use selector |
| `currentPlayerColors?: PlayerColors` | `currentPlayerId: string` | Pass ID, use selector |
| `roadState` | `roadState` | Keep (server state) |
| `side` | `side` | Keep (geometry) |
| `hexSize` | `hexSize` | Keep (geometry) |
| `buildIndex` | `buildIndex` | Keep (server-provided) |
| `onClick` | `onClick` | Keep (interaction) |
| `className` | `className` | Keep (styling) |

### 3.7 Files to Update

| File | Changes Required |
|------|------------------|
| `Building.tsx` | Replace `ownerColors`/`currentPlayerColors` with IDs; add `usePlayerColors` hook calls |
| `Road.tsx` | Replace `ownerColors`/`currentPlayerColors` with IDs; add `usePlayerColors` hook calls |
| `GameBoard.tsx` | Pass `ownerId`/`currentPlayerId` instead of looking up and passing colors |
| `gameStoreHooks.ts` | Create `usePlayerColors` hook (if not exists) |
| `player-profile.ts` | Ensure `DEFAULT_PLAYER_COLORS` is exported |

### 3.8 Verification

After updating each component:

- [ ] Component renders correctly with owner colors
- [ ] Component renders correctly with current player colors (buildable state)
- [ ] Component updates when player profile colors change in store
- [ ] No TypeScript errors
- [ ] No runtime errors in console

---

## Execution Plan

Each work unit follows the pattern: **implement → write tests → pass tests → commit**.

**Test Data:** Use `react-ui/lib/test-data/expansion-game.ts` which provides a full GameModel with tiles, buildings, roads, and harbors.

---

### Phase 1: Extensions Library

Each extension file is a separate commit. This allows incremental progress and easy rollback.

#### Work Unit 1.1: playerExtensions

**Implement:**

```text
react-ui/lib/extensions/
├── index.ts                    // Re-exports (start here)
└── playerExtensions.ts         // playerFromId
```

**Test:** `__tests__/playerExtensions.test.ts`

- `playerFromId` returns undefined for empty array
- `playerFromId` returns player when found
- `playerFromId` returns undefined when not found

**Verify:** `cd react-ui && npm run test -- playerExtensions`

**Commit:** `feat(extensions): add playerExtensions with playerFromId`

---

#### Work Unit 1.2: tileExtensions

**Implement:** `tileExtensions.ts`

- `tileFromCoords(tiles, coords)` - find tile by coordinates
- `totalStars(tiles)` - sum of pip values (use `NUMBER_PIPS` from test-data)
- `tilesWithNumber(tiles, n)` - filter by roll number
- `tilesWithResource(tiles, r)` - filter by resource type
- `tilesWithSixOrEight(tiles)` - high-probability tiles
- `adjacentTiles(tiles, tile)` - 6 neighbors

**Test:** `__tests__/tileExtensions.test.ts`

- Use `EXPANSION_GAME_DATA.tiles` for test data
- Star calculation: tile with number 6 = 5 pips, number 2 = 1 pip
- Filter tests: verify correct tile counts

**Verify:** `cd react-ui && npm run test -- tileExtensions`

**Commit:** `feat(extensions): add tileExtensions with star calc and filters`

---

#### Work Unit 1.3: buildingExtensions (CRITICAL - aliases)

**Implement:** `buildingExtensions.ts`

- `buildingKeyAliases(key)` - returns 2 alias positions for each vertex
- `buildingKeysEqual(a, b)` - compare keys accounting for aliases
- `findBuilding(buildings, key)` - find with alias handling
- `adjacentBuildings(buildings, key)` - 3 adjacent vertices
- `buildingsInTile(buildings, coords)` - all 6 vertices of a tile
- `ownedBuildings(buildings, coords)` - owned buildings in tile

**Test:** `__tests__/buildingExtensions.test.ts`

- **CRITICAL:** Test alias handling thoroughly
  - TopRight at (0,0,0) = BottomRight at North neighbor = Left at NorthEast neighbor
  - Verify `findBuilding` finds via any alias
- Empty array returns undefined
- Adjacency returns correct 3 neighbors

**Verify:** `cd react-ui && npm run test -- buildingExtensions`

**Commit:** `feat(extensions): add buildingExtensions with alias handling`

---

#### Work Unit 1.4: roadExtensions (CRITICAL - aliases)

**Implement:** `roadExtensions.ts`

- `roadKeyAliases(key)` - returns 1 alias position for each edge
- `roadKeysEqual(a, b)` - compare keys accounting for aliases
- `findRoad(roads, key)` - find with alias handling
- `adjacentRoads(roads, key)` - 4 adjacent edges

**Test:** `__tests__/roadExtensions.test.ts`

- **CRITICAL:** Test alias handling
  - TopRight at (0,0,0) = BottomLeft at NorthEast neighbor
- Use `EXPANSION_GAME_DATA.roads` or `generateRoadsForTile()` for test data

**Verify:** `cd react-ui && npm run test -- roadExtensions`

**Commit:** `feat(extensions): add roadExtensions with alias handling`

---

#### Work Unit 1.5: gameModelExtensions

**Implement:** `gameModelExtensions.ts`

- `currentPlayer(game)` - get current player model
- `isAllocationPhase(game)` - check game state
- `gamePhase(game)` - get GamePhase enum
- `roadsAdjacentToBuilding(game, buildingKey)` - roads touching a building
- `buildingsAdjacentToRoad(game, roadKey)` - buildings at road endpoints
- `tilesForBuilding(game, buildingKey)` - up to 3 tiles touching a building
- `buildingBetweenRoads(game, r1, r2)` - building at road junction

**Test:** `__tests__/gameModelExtensions.test.ts`

- Create mock GameModel with players, tiles, buildings, roads
- Test cross-model queries

**Verify:** `cd react-ui && npm run test -- gameModelExtensions`

**Commit:** `feat(extensions): add gameModelExtensions for cross-model queries`

---

#### Work Unit 1.6: gameStoreHooks

**Implement:** `react-ui/lib/stores/gameStoreHooks.ts`

- `usePlayerColors(playerId)` - with custom `colorsEqual` equality
- `usePlayerGradient(playerId)` - CSS gradient string
- `usePlayerForeground(playerId)` - foreground color
- `useCurrentPlayer()` - current player from store
- `useIsMyTurn()` - check if local player's turn
- `useGamePhase()` - current game phase

**Test:** `__tests__/gameStoreHooks.test.ts`

- Mock Zustand store with test data
- Verify hooks return correct values
- Verify null/missing player returns NEUTRAL_COLORS

**Verify:** `cd react-ui && npm run test -- gameStoreHooks`

**Commit:** `feat(stores): add gameStoreHooks with player color selectors`

---

### Phase 2: Code Audit (No Commits)

This phase identifies code to refactor. Document findings in this file or separate notes.

#### Step 2.1: Audit game page

- [ ] Document inline logic that should use extensions
- [ ] Document entitlement checks
- [ ] Document star calculations

#### Step 2.2: Audit GameBoard

- [ ] Document building/road position calculations
- [ ] Document index map constructions

#### Step 2.3: Audit components

- [ ] List components passing colors as props
- [ ] List components with inline player lookups

---

### Phase 3: Refactor Client Code

#### Work Unit 3.1: Update Building.tsx props

**Implement:**

- Change `ownerColors?: PlayerColors` → `ownerId: string | null`
- Change `currentPlayerColors?: PlayerColors` → `currentPlayerId: string`
- Add `usePlayerColors(ownerId)` and `usePlayerColors(currentPlayerId)` inside component

**Test:** Manual - verify building renders correctly in game

**Verify:** `cd react-ui && npm run build`

**Commit:** `refactor(Building): use playerId props with color selectors`

---

#### Work Unit 3.2: Update Road.tsx props

**Implement:**

- Change `ownerColors?: PlayerColors` → `ownerId: string | null`
- Change `currentPlayerColors?: PlayerColors` → `currentPlayerId: string`
- Add `usePlayerColors` hook calls inside component

**Test:** Manual - verify road renders correctly in game

**Verify:** `cd react-ui && npm run build`

**Commit:** `refactor(Road): use playerId props with color selectors`

---

#### Work Unit 3.3: Update GameBoard.tsx

**Implement:**

- Pass `ownerId` and `currentPlayerId` to Building/Road instead of looking up colors
- Replace inline star calculations with `totalStars()` or extension functions
- Replace inline player lookups with `playerFromId()`

**Test:** Manual - verify board renders correctly

**Verify:** `cd react-ui && npm run build`

**Commit:** `refactor(GameBoard): pass playerIds, use extensions for lookups`

---

#### Work Unit 3.4: Remove redundant entitlement checks

**Implement:**

- Remove `showSettlementIndexes` entitlement check
- Derive from `buildings.some(b => b.buildingState === 'PossibleSettlement')`
- Document any remaining legitimate entitlement uses

**Test:** Manual - verify settlement indexes appear when they should

**Verify:** `cd react-ui && npm run build`

**Commit:** `refactor(game): trust server buildingState, remove redundant checks`

---

## Verification Checklist

After each phase:

- [ ] `npm run build` passes in react-ui
- [ ] `npm run lint` passes
- [ ] Manual testing: Create game, play through allocation, build roads/settlements

---

## Key Files Reference

| File | Purpose |
|------|---------|
| [GameBoard.tsx](react-ui/components/game/board/GameBoard.tsx) | Main board rendering, index maps |
| [page.tsx](react-ui/app/game/[id]/page.tsx) | Game page, entitlement checks |
| [Building.tsx](react-ui/components/game/tiles/Building.tsx) | Building component |
| [Road.tsx](react-ui/components/game/tiles/Road.tsx) | Road component |
| [gameStore.ts](react-ui/lib/stores/gameStore.ts) | Zustand store |
| [BuildingModelExtensions.cs](Catan3.Shared/Extensions/BuildingModelExtensions.cs) | C# source for building utils |
| [RoadModelExtensions.cs](Catan3.Shared/Extensions/RoadModelExtensions.cs) | C# source for road utils |
| [TileModelExtensions.cs](Catan3.Shared/Extensions/TileModelExtensions.cs) | C# source for tile utils |
| [GameModelExtensions.cs](Catan3.Shared/Extensions/GameModelExtensions.cs) | C# source for game utils |

---

## Decision Log

| Date | Decision | Rationale |
|------|----------|-----------|
| 2026-01-28 | Keep stars/buildIndex client-side permanently | UI convenience only - no game semantic, not state logic |
| 2026-01-28 | Pass playerId not colors | Single source of truth, fewer props |
| 2026-01-28 | Port C# extensions to TypeScript | Consistent behavior, discoverable code |
| 2026-01-28 | Create gameStoreHooks.ts | Mirrors C# extensions pattern for React |
| 2026-01-28 | Return `undefined` not throw for "not found" | React-idiomatic; thrown exceptions crash component tree |
| 2026-01-28 | Use custom equality for Zustand object selectors | Prevents unnecessary re-renders when colors unchanged |

---

## Notes

- The C# extension pattern maps well to TypeScript: static methods → exported functions
- Zustand hooks provide the React-specific equivalent for store-derived values
- Alias handling is critical for correctness - buildings/roads share vertices/edges
- C# `out` parameters become TypeScript return objects (e.g., `{ roads, adjacentFork }`)

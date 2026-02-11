# TypeScript Test Strategy

**Status:** Proposed — awaiting approval

## Core Requirement: Serialization Round-Trip

The original and primary purpose of TypeScript tests is to verify that the
C# GameService and the TypeScript client can communicate:

> **Serialize in C# → Deserialize in TypeScript → Serialize in TypeScript →
> Deserialize in C#**

If this round-trip works, the wire contract is sound. Everything else (extension
function tests, store tests, UI tests) builds on top of this foundation.

The `.catan_test` files are C#-serialized JSON — they represent exactly what
the GameService produces. The generated TypeScript types (from TypeGen) represent
how the client expects to consume that JSON. **If the generated types can
correctly deserialize `.catan_test` JSON, the contract holds.**

Hand-written types completely defeat this purpose. They test whether hand-written
types match hand-written data — which proves nothing about the actual wire
format. This is why the parallel type system in `expansion-game.ts` is
destructive: it makes tests pass while the real serialization contract is broken.

## Problem Statement

The test suite has lost the round-trip verification guarantee:

1. **Parallel type system.** `lib/test-data/expansion-game.ts` defines 14 types
   that duplicate (and diverge from) the generated types. Tests pass against
   these hand-written types while the real wire format has drifted.

2. **Generated types include phantom fields.** TypeGen produces `TileModel` with
   `stars` and `default` — fields that don't exist in the JSON wire format:
   - `stars` is `[JsonIgnore]` in C# (computed from `Number`, never serialized)
   - `default` is a `static` property (never serialized)

   Any code that reads `tile.stars` from a deserialized model gets `undefined`
   at runtime. The hand-written test data masked this by including `stars`.

3. **Extension functions read phantom fields.** `totalStars()` reads
   `tile.stars` which doesn't exist on real server data. This is a latent
   runtime bug hidden by hand-written test data.

4. **Invalid enum values.** Tests used `"PossibleCity"` and `"NoSettlement"`
   which don't exist in the C# `BuildingState` enum. TypeScript accepted them
   because the test data used its own `BuildingState` type.

## Design Principles

1. **Round-trip first.** The foundational test is: can TypeScript deserialize
   C#-serialized JSON into the generated types? Everything else builds on this.
2. **One type system.** All TypeScript code (app and test) imports from
   `@/types/generated/models/`. Zero hand-written model type definitions.
3. **Wire format is truth.** Generated types must exactly match the JSON the
   server sends. If C# has `[JsonIgnore]` or `static`, the field must not
   appear in the generated TypeScript interface.
4. **Computed properties are client logic.** Values like `stars` (derived from
   tile number) are computed by extension functions, not stored on the model.
5. **`.catan_test` files are the C# serialization oracle.** They are produced
   by the C# GameStateMachine and represent the authoritative wire format.
   Tests deserialize them with the generated types to verify the contract.

## Test Architecture

### Layer 0: Serialization Contract (foundation)

**What it tests:** Can the generated TypeScript types correctly deserialize
JSON produced by C#?

**How:** Load a `.catan_test` file (C#-serialized JSON) and parse it as
`GameModel` using the generated type. If `JSON.parse()` + type assignment
works and downstream code can access fields correctly, the contract holds.

```typescript
import { readFileSync } from 'fs';
import type { GameModel } from '@/types/generated/models/game-model';

const json = readFileSync('../../Tests/Data/Expansion.catan_test', 'utf-8');
const data = JSON.parse(json) as { gameModel: GameModel; actionStack: unknown[] };
const game = data.gameModel;

// These verify the type contract — if generated types are wrong,
// these will be undefined or the wrong type at runtime
expect(game.tiles.length).toBe(30);
expect(game.tiles[0].tileKey.q).toBe(-3);
expect(game.tiles[0].resourceTileType).toBe('Ore');
expect(game.players.length).toBe(4);
expect(game.players[0].name).toBe('Joe');
```

**Critical property:** No hand-written types are involved. The generated
`GameModel` type is the only interface between the JSON and the test code.
If the generated type has a field that doesn't exist in the JSON (like `stars`),
the test will catch it when it reads `undefined` instead of a number.

### Layer 1: Truth Set Verification

**What it tests:** Do extension functions compute correct values from real
C#-serialized data?

**How:** Define a truth set of independently verified facts about a
`.catan_test` game state. Load the game, run extension functions, compare
against truth values.

```typescript
/** Known facts about Tests/Data/Expansion.catan_test initial board. */
export const EXPANSION_BOARD_TRUTH = {
  tileCount: 30,
  desertCount: 2,
  resourceCounts: {
    Ore: 5, Wheat: 5, Sheep: 5, Wood: 6, Brick: 5, Desert: 2,
  },
  tilesWithSix: 3,
  tilesWithEight: 3,
  tileAt_0_0: { resource: 'Ore', number: 9, computedStars: 4 },
  totalComputedStars: 88,
} as const;
```

This catches two classes of bugs:

- **Serialization bugs:** If the generated type is wrong, the data loads
  incorrectly and truth checks fail.
- **Computation bugs:** If an extension function has a logic error, the
  computed value won't match the truth.

### Layer 2: Pure Function Unit Tests (mock factories)

**What it tests:** Algorithmic correctness of pure functions.

**How:** Lightweight mock objects constructed from generated types. These
don't need realistic game data — they test coordinate math, key equality,
alias resolution, etc.

```typescript
function createMockTile(q: number, r: number, number: number): TileModel {
  return {
    tileKey: { q, r, s: -q - r },
    number,
    resourceTileType: 'Ore' as ResourceType,
    highlighted: false,
    temporarilyGold: false,
  };
}
```

**Key rule:** Mock factories import types from `@/types/generated/models/`.
If a generated type gains or loses a field, the mock factory gets a compile
error — which is exactly what we want.

### Layer 3: Store Tests (minimal objects)

**What it tests:** Zustand state management (not game logic).

**How:** Construct minimal valid objects from generated types. Already works
in `gameStoreHooks.test.ts`.

### Layer 4: Integration Tests (existing)

**What it tests:** Full round-trip through live GameService.

**How:** `RecordingPlayer.integration.test.ts` replays recordings through the
actual server with hash verification at every action. This is the strongest
validation — it exercises the real C# serialization, SignalR transport,
TypeScript deserialization, and re-serialization path.

No structural changes needed. Already uses generated types exclusively.

## Required Changes

### Phase 1: Fix TypeGen Pipeline (wire format alignment)

The generated types must match the JSON wire format exactly.

| Issue | Root Cause | Fix |
|-------|-----------|-----|
| `TileModel.stars` | `[JsonIgnore]` not stripped | Add `TileModel` to `GetJsonIgnoredPropertiesMap()` |
| `TileModel.default` | `static` property not stripped | Strip `static` properties in TypeGen post-processing |
| `BuildingModel.default` | Same | Same |
| `BuildingKey.default` | Same | Same |
| `HarborModel.default` | Same | Same |

After fixing, generated `TileModel` becomes:

```typescript
export interface TileModel {
    tileKey: HexCoordinates;
    number: number;
    resourceTileType: ResourceType;
    highlighted: boolean;
    temporarilyGold: boolean;
}
```

This matches what C# actually serializes to JSON.

### Phase 2: Fix Extension Functions

`totalStars()` reads `tile.stars` — a field that doesn't exist on the wire.
Fix it (and audit all extension functions) to compute from actual model data:

```typescript
// Before (reads phantom field):
export function totalStars(tiles: TileModel[]): number {
  return tiles.reduce((sum, tile) => sum + tile.stars, 0);
}

// After (computes from actual data):
export function totalStars(tiles: TileModel[]): number {
  return tiles.reduce((sum, tile) => sum + pipsForNumber(tile.number), 0);
}
```

### Phase 3: Eliminate Parallel Type System

Rewrite `lib/test-data/expansion-game.ts`:

1. **Delete** all 14 hand-written type definitions
2. **Import** types from `@/types/generated/models/`
3. **Keep** data constants and factory functions
4. **Remove** `stars` and `default` from tile data (not in wire format)
5. **Fix** `HarborHex.tsx` to import types from generated models

### Phase 4: Add Serialization Contract Tests

New files:

| File | Purpose |
|------|---------|
| `lib/test-data/load-catan-test.ts` | Load `.catan_test` JSON, parse as typed `GameModel` |
| `lib/test-data/expansion-board-truth.ts` | Known facts about the Expansion board |
| `lib/extensions/__tests__/serialization-contract.test.ts` | Layer 0 + Layer 1 tests |

The contract test is the most important addition. It:

1. Loads `Tests/Data/Expansion.catan_test` (C#-serialized JSON)
2. Parses it as `GameModel` using only generated types
3. Verifies field access works correctly (catches phantom fields)
4. Runs extension functions and compares against truth set
5. Optionally: re-serializes to JSON and compares structure

### Phase 5: Export 169-Move Recording (future)

Export the database recording to `Tests/Data/FullGame.catan_test`. Define
truth sets at key action indices to test game-state extension functions
(`currentPlayer`, `gamePhase`, `buildableBuildings`, `starsForBuilding`)
against real mid-game and end-game states.

## File Changes Summary

| File | Change |
|------|--------|
| `Catan3.Shared/TypeScript/TypeGenRunner/Program.cs` | Strip `[JsonIgnore]` and `static` properties from all types |
| `react-ui/types/generated/models/tile-model.ts` | Regenerated — no `stars`, no `default` |
| `react-ui/types/generated/models/building-model.ts` | Regenerated — no `default` |
| `react-ui/types/generated/models/building-key.ts` | Regenerated — no `default` |
| `react-ui/types/generated/models/harbor-model.ts` | Regenerated — no `default` |
| `react-ui/lib/extensions/tileExtensions.ts` | `totalStars` computes from `pipsForNumber` |
| `react-ui/lib/test-data/expansion-game.ts` | Delete types, import from generated |
| `react-ui/lib/test-data/load-catan-test.ts` | New — `.catan_test` JSON loader |
| `react-ui/lib/test-data/expansion-board-truth.ts` | New — truth set for Expansion board |
| `react-ui/lib/extensions/__tests__/serialization-contract.test.ts` | New — contract + truth set tests |
| `react-ui/lib/extensions/__tests__/tileExtensions.test.ts` | Use loaded board data |
| `react-ui/lib/extensions/__tests__/buildingExtensions.test.ts` | Fix invalid enum values |
| `react-ui/lib/stores/stores.test.ts` | Fix `color` → `colors` |
| `react-ui/components/game/tiles/HarborHex.tsx` | Import types from generated models |

## Verification

1. `pwsh ./catan.ps1 generate-types` — regenerated types match wire format
2. `pwsh ./catan.ps1 lint ts` — zero TypeScript errors, zero ESLint errors
3. `npm run test:run` — all unit tests pass, including serialization contract
4. `npm run test:integration` — recording replays pass (if server running)
5. Manually verify: load `.catan_test` JSON and confirm generated `TileModel`
   has no `stars` or `default` fields

## Open Questions

1. **How to handle `default` fields long-term?** The `static Default` pattern
   is pervasive in C# models. Options: (a) strip all `static` properties in
   TypeGen, (b) strip only `Default`, (c) make them optional. Recommendation:
   option (a) — static properties are never serialized so they should never
   appear in generated types.

2. **Should we load `.catan_test` files in vitest?** These are ~100KB JSON
   files. Loading from disk in unit tests is fast and avoids hardcoding large
   data structures. vitest runs in Node so `fs.readFileSync` works. The
   alternative is importing a pre-parsed constant, but that couples the test
   data to the build pipeline.

3. **TS → C# direction.** The current plan validates C# → TS deserialization.
   The reverse (TS → C# serialization) is implicitly validated by the
   integration tests (TypeScript sends actions through SignalR, C# processes
   them). Should we add explicit TS → JSON → C# structure comparison tests?
   The integration test hash verification may be sufficient.

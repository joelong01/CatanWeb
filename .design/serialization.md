# TypeScript Code Generation

**Last verified:** January 30, 2026

## Overview

C# models are automatically converted to TypeScript interfaces via
a custom `TypeGenRunner` tool. The pipeline handles MVVM artifact
removal, `[JsonIgnore]` property stripping, and enum-to-union
conversion.

**Entry point:** `dotnet run --project Catan3.Shared/TypeScript/TypeGenRunner`

**Output:** `react-ui/types/generated/models/`

## Pipeline Steps

### Step 1: TypeGen Generation

TypeGen generates raw TypeScript from C# models annotated with
`[ExportTs*]` attributes. Output includes MVVM artifacts (observable
base classes) that are not needed in React.

### Step 2: MVVM Artifact Removal

Removes desktop-specific artifacts from generated TypeScript:

- Deletes files: `observable-object.ts`,
  `i-notify-property-changed.ts`, `i-notify-property-changing.ts`
- Removes imports and `extends` clauses referencing deleted types
- Converts generated classes to interfaces
- Removes default values from interface properties

### Step 2a: Enum to String Literal Union Conversion

Converts TypeScript enums to string literal union types for better
ergonomics:

**Before:**

```typescript
export enum Direction { North = 'North', South = 'South' }
```

**After:**

```typescript
export type Direction = 'North' | 'South';
export const Direction = { North: 'North', South: 'South' } as const;
```

### Step 2b: JsonIgnore Property Removal

Removes properties marked with `[JsonIgnore]` in C# from generated
TypeScript interfaces:

1. Reflects over C# types to find `[JsonIgnoreAttribute]` properties
2. Skips static properties
3. Converts property names PascalCase -> camelCase
4. Removes matching lines via regex
5. Cleans up unused imports

**Currently configured for:** `HexCoordinates` type

**Rationale:** `HexCoordinates` has navigation properties (`.North`,
`.South`, etc.) that are useful in C# for ergonomic board traversal
(~30 call sites) but should not be serialized. The `[JsonIgnore]`
approach is preferred over extension methods because:

- Better C# ergonomics (property syntax vs method calls)
- Post-processing is a one-time setup cost
- No breaking changes to ~30 call sites

### Step 3: Enum Description Generation

Extracts `[Description]` attributes from C# enums and generates
TypeScript description maps:

- `GameState` -> display text
- `Entitlement` -> display text
- `ActionType` -> display text

## Build Integration

TypeGenRunner runs as part of the standard build process:

```powershell
# During React build (catan.ps1)
dotnet run --project Catan3.Shared\TypeScript\TypeGenRunner --no-build

# Standalone regeneration
dotnet run --project Catan3.Shared\TypeScript\TypeGenRunner
```

Invoked by `catan.ps1` during both `build` and `run` verbs.

## Generated Type Examples

### GameModel (simplified)

```typescript
export interface GameModel {
    gameId: string;
    gameState: GameState;
    currentPlayerId: string;
    players: PlayerModel[];
    tiles: TileModel[];
    buildings: BuildingModel[];
    roads: RoadModel[];
    harbors: HarborModel[];
    robber: RobberModel;
    actionFlags: ActionFlags;
    gameHash: string;
    // ... ~40 properties total
}
```

### HexCoordinates (after JsonIgnore removal)

```typescript
export interface HexCoordinates {
    q: number;
    r: number;
    s: number;
}
```

Navigation properties (`north`, `south`, `east`, `west`, etc.) are
removed by Step 2b since they are computed in C# and not serialized.

## Files

| File | Purpose |
|------|---------|
| `Catan3.Shared/TypeScript/TypeGenRunner/Program.cs` | Pipeline orchestrator (~535 lines) |
| `react-ui/types/generated/models/` | Output directory |
| `catan.ps1` (lines 833-835, 919-924) | Build integration |

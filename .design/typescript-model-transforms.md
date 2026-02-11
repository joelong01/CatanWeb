# TypeScript Model Transforms

**Last verified:** February 6, 2026

## Overview

C# models are auto-generated to TypeScript via a custom `TypeGenRunner`
console app that wraps the TypeGen 7.0.0 library. The pipeline handles
MVVM artifact removal, `[JsonIgnore]` property stripping, enum-to-union
conversion, and enum description extraction.

## Why a Custom Runner?

The TypeGen CLI tool (`dotnet-typegen`) targets .NET 10 but this project
pins .NET 9 via `global.json`. The runner invokes the TypeGen library
API directly as a NuGet package, which works with any .NET version.

## File Locations

| File | Purpose |
|------|---------|
| `Catan3.Shared/TypeScript/CatanTypeGenSpec.cs` | Declares which C# types to export |
| `Catan3.Shared/TypeScript/TypeGenRunner/Program.cs` | Pipeline orchestrator (~535 lines) |
| `Catan3.Shared/TypeScript/TypeGenRunner/TypeGenRunner.csproj` | Console app project |
| `react-ui/types/generated/models/` | Output directory (~54 generated files) |
| `react-ui/types/generated/models/index.ts` | Barrel export for all generated types |

## Running the Generator

```bash
# Via catan.ps1 (preferred)
pwsh ./catan.ps1 generate-types

# Direct invocation
dotnet run --project Catan3.Shared/TypeScript/TypeGenRunner/TypeGenRunner.csproj
```

The `generate-types` verb is also invoked automatically during `build`
and `run` verbs.

## Pipeline Steps

### Step 1: TypeGen Generation

TypeGen reads `CatanTypeGenSpec` and generates raw TypeScript from C#
types. Configuration:

- `PascalCaseToCamelCaseConverter` for property names
- PascalCase preserved for type names
- String initializers for enums
- Single quotes, barrel `index.ts`

### Step 2: MVVM Artifact Removal

Removes desktop-specific artifacts:

- Deletes `observable-object.ts`, `i-notify-property-changed.ts`,
  `i-notify-property-changing.ts`
- Strips imports and `extends` clauses referencing those types
- Converts generated classes to interfaces
- Removes default values from interface properties

### Step 2a: Enum to String Literal Union Conversion

Converts TypeScript enums to `type` + `const` pairs for ergonomics:

```typescript
// Before:  export enum Direction { North = 'North' }
// After:
export type Direction = 'North' | 'South';
export const Direction = { North: 'North', South: 'South' } as const;
```

### Step 2b: JsonIgnore Property Removal

Reflects over C# types to find `[JsonIgnore]` and static properties,
then removes them from generated TypeScript via regex. Currently
configured for `HexCoordinates` (navigation properties).

### Step 3: Enum Description Generation

Extracts `[Description]` attributes from C# enums (`GameState`,
`Entitlement`, `ActionType`) and generates `enum-descriptions.ts`
with `Record<EnumType, string>` maps.

## What Types Are Covered

### Currently Generated (via CatanTypeGenSpec)

All types from `Catan3.Shared.Models` and `Catan3.Shared.Utility`:

- **Core:** `GameModel`, `ActionFlags`, `PlayerModel`
- **Board:** `TileModel`, `BuildingModel`, `RoadModel`, `HarborModel`,
  `RobberModel`, `HexCoordinates`
- **Resources:** `ResourcesModel`, `ResourceCounterModel`
- **Config:** `HouseRules`, `ResourceRules`, `EntitlementPurchaseModel`
- **Rolls:** `RollModel`, `GameRollModel`, `TurnRollModel`
- **Enums:** `GameState`, `ResourceType`, `BuildingState`, etc. (17 enums)
- **Messages:** `RollMessage`, `UndoMessage`, etc. (15 message types)

### NOT Generated (hand-written, currently wrong)

Types from `Catan3.Shared.Profiles` are **missing** from the spec:

| C# Type | React File | Status |
|---------|-----------|--------|
| `PlayerProfile` | `react-ui/types/player-profile.ts` | Hand-written, out of sync |
| `PlayerColors` | `react-ui/types/player-profile.ts` | Hand-written, correct |
| `LifetimeStats` | `react-ui/types/player-profile.ts` | Hand-written, **wrong** |
| `GameStats` (Profiles) | `react-ui/types/player-profile.ts` | Hand-written, **wrong** |

The hand-written `LifetimeStats` interface is missing ~14 fields
(gamesPlayed, wins, all max/min records). The hand-written `GameStats`
has completely different fields than the C# record.

### Root Cause

`CatanTypeGenSpec.cs` only imports `Catan3.Shared.Models` and
`Catan3.Shared.Utility`. The profile types live in
`Catan3.Shared.Profiles` and were never added to the spec. Someone
wrote the TypeScript types by hand and they drifted.

## Adding New Types

1. Add using directive and `AddInterface<T>()` / `AddEnum<T>()` call
   to `CatanTypeGenSpec.cs`
2. Run `pwsh ./catan.ps1 generate-types`
3. Types appear in `react-ui/types/generated/models/`
4. If the type has `[JsonIgnore]` properties, add it to
   `GetJsonIgnoredPropertiesMap()` in `Program.cs`

## Profile Types: Special Considerations

When adding Profile types to the generator, note:

- **`PlayerProfile`** has 3 `[JsonIgnore]` backward-compat properties
  (`PrimaryBackgroundColor`, `SecondaryBackgroundColor`,
  `ForegroundColor`) that must be stripped
- **`PlayerColors`** has computed properties (`SvgGradientStops`,
  `CssGradient`) that are not `[JsonIgnore]` but also not serialized
  (they're `get`-only with no backing field). TypeGen may or may not
  include these - verify after generation
- **`LifetimeStats`** has calculated properties (`WinRate`,
  `AverageStars`, etc.) that are `get`-only and won't serialize.
  The `Empty` static property needs stripping
- **`GameStats`** has a static `Empty` property and `operator+` that
  should not appear in TypeScript

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| TypeGen | 7.0.0 | Core generation library |
| Newtonsoft.Json | 13.0.3 | Required by TypeGen |
| Namotion.Reflection | 2.1.1 | Required by TypeGen |
| Catan3.Shared | (project ref) | Access to model types |

## Related Documentation

- [serialization.md](serialization.md) - Pipeline overview (abbreviated)
- `Catan3.Shared/TypeScript/TypeGenRunner/typegen-design.md` - Detailed
  in-code design doc

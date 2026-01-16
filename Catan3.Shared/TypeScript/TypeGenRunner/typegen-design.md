# TypeGen Design Document

**Location:** `Catan3.Shared/TypeScript/TypeGenRunner/`
**Last Updated:** 2026-01-16

## Overview

TypeGenRunner is a custom console application that programmatically invokes TypeGen 7.0.0 to generate
TypeScript interfaces from C# model classes. This approach bypasses the `dotnet-typegen` CLI tool
which requires .NET 10 (incompatible with this project's .NET 9 SDK constraint in `global.json`).

## Why TypeGenRunner?

### Problem

- TypeGen 7.0.0 CLI tool (`dotnet-typegen`) targets .NET 10
- This project pins .NET SDK to 9.0 via `global.json` for stability
- Running `dotnet tool install -g dotnet-typegen` fails with misleading "DotnetToolSettings.xml not found"

### Solution

Create a console app that:

1. References TypeGen 7.0.0 as a NuGet package (works with any .NET version)
2. Programmatically invokes the TypeGen Generator API
3. Uses `CatanTypeGenSpec` to define which types to export

## Architecture

```text
Catan3.Shared/
├── TypeScript/
│   ├── CatanTypeGenSpec.cs      # Defines types to export
│   └── TypeGenRunner/
│       ├── TypeGenRunner.csproj  # Console app project
│       ├── Program.cs            # Entry point, runs generator
│       └── typegen-design.md     # This document
```

### Output

Generated TypeScript files are written to:

```text
react-ui/types/generated/models/
├── game-model.ts
├── player-model.ts
├── game-state.ts
├── ... (57 total files)
└── index.ts (barrel export)
```

## Type Generation Configuration

### Generator Options

```csharp
var options = new GeneratorOptions
{
    BaseOutputDirectory = outputPath,
    CreateIndexFile = true,           // Creates index.ts barrel export
    SingleQuotes = true,              // Use 'string' not "string"
    EnumStringInitializers = true,    // GameState.WaitingForRoll = 'WaitingForRoll'
    PropertyNameConverters = [new PascalCaseToCamelCaseConverter()],
    TypeNameConverters = []           // Keep PascalCase for type names
};
```

### CatanTypeGenSpec

The `CatanTypeGenSpec` class (in `Catan3.Shared/TypeScript/CatanTypeGenSpec.cs`) defines all types
to export using `AddInterface<T>()` and `AddEnum<T>()` methods.

**Categories:**

- **Core Models:** GameModel, ActionFlags, PlayerModel
- **Board Models:** TileModel, BuildingModel, RoadModel, HarborModel, RobberModel
- **Resource Models:** ResourcesModel, ResourceCounterModel
- **Configuration:** HouseRules, ResourceRules, EntitlementPurchaseModel
- **Keys:** BuildingKey, RoadKey, HarborKey, HexCoordinates
- **Enums:** GameState, GameType, ResourceType, BuildingState, etc.
- **Messages:** RollMessage, UndoMessage, RedoMessage, etc. (SignalR communication)

## Enum Description Attributes

### The Problem

C# enums use `[Description("...")]` attributes to provide UI-friendly display text:

```csharp
public enum GameState
{
    [Description("Select Roll...")]
    WaitingForRoll,

    [Description("Build or click Next.")]
    WaitingForNext,
    // ...
}
```

TypeGen does NOT export these descriptions. The Blazor app uses reflection to read descriptions
at runtime, but TypeScript cannot do this.

### Solution: Generated Description Maps

TypeGenRunner exports enum descriptions to a separate TypeScript file that mirrors the C# attributes.
This file is auto-generated alongside the model files.

**Output:** `react-ui/types/generated/models/enum-descriptions.ts`

```typescript
import { GameState } from './game-state';
import { Entitlement } from './entitlement';

export const GameStateDescriptions: Record<GameState, string> = {
  [GameState.Uninitialized]: 'Uninitialized',
  [GameState.WaitingForNewGame]: 'New Game',
  [GameState.WaitingForRoll]: 'Select Roll...',
  [GameState.WaitingForNext]: 'Build or click Next.',
  // ... all values
};

export const EntitlementDescriptions: Record<Entitlement, string> = {
  [Entitlement.Undefined]: 'Undefined',
  [Entitlement.DevCard]: 'Dev Card',
  [Entitlement.Settlement]: 'Settlement',
  // ... all values
};

// Helper function to get description for any enum value
export function getEnumDescription<T extends string>(
  descriptions: Record<T, string>,
  value: T
): string {
  return descriptions[value] ?? value;
}
```

### Enums With Descriptions

The following enums have `[Description]` attributes that are exported:

| Enum | Purpose |
|------|---------|
| `GameState` | Status bar text showing current game phase |
| `Entitlement` | Button labels for purchase actions |
| `ActionType` | Test script action descriptions |

### Keeping Descriptions in Sync

When C# enum descriptions change:

1. Run `pwsh ./catan.ps1 generate-types`
2. `enum-descriptions.ts` is regenerated automatically
3. TypeScript compiler will catch any mismatches at build time

## Usage

### Generate Types

```bash
pwsh ./catan.ps1 generate-types
```

This command:

1. Builds TypeGenRunner if needed
2. Runs the console app
3. Outputs generated files to `react-ui/types/generated/models/`
4. Reports file count on success

### Manual Execution

```bash
dotnet run --project Catan3.Shared/TypeScript/TypeGenRunner/TypeGenRunner.csproj
```

## Dependencies

### TypeGenRunner.csproj

```xml
<PackageReference Include="TypeGen" Version="7.0.0" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
<PackageReference Include="Namotion.Reflection" Version="2.1.1" />
<ProjectReference Include="..\..\Catan3.Shared.csproj" />
```

- **TypeGen 7.0.0:** Core generation library
- **Newtonsoft.Json:** Required by TypeGen for JSON schema handling
- **Namotion.Reflection:** Required by TypeGen for type analysis
- **Catan3.Shared:** Access to model types and CatanTypeGenSpec

## Exclusion from Parent Project

TypeGenRunner must be excluded from `Catan3.Shared.csproj` to prevent build conflicts:

```xml
<ItemGroup>
  <!-- Exclude TypeGenRunner from this project - it's a separate console app -->
  <Compile Remove="TypeScript\TypeGenRunner\**" />
  <None Remove="TypeScript\TypeGenRunner\**" />
</ItemGroup>
```

## Extending TypeGenRunner

### Adding New Types

1. Add the type to `CatanTypeGenSpec.OnBeforeGeneration()`:

   ```csharp
   AddInterface<NewModel>();  // For classes/interfaces
   AddEnum<NewEnum>();        // For enums
   ```

2. Run `pwsh ./catan.ps1 generate-types`

### Adding Enum Descriptions

1. Add `[Description("...")]` attributes to the C# enum
2. Register the enum in TypeGenRunner's description extractor (if not already)
3. Run `pwsh ./catan.ps1 generate-types`

## Post-Processing

TypeGen generates types directly from C#, but some .NET-specific artifacts need to be removed
for clean TypeScript. TypeGenRunner performs post-processing after TypeGen runs.

### MVVM Artifacts Removed

The following C# MVVM types have no TypeScript equivalent and are removed:

| File | Reason |
|------|--------|
| `observable-object.ts` | Empty class, C# MVVM base class |
| `i-notify-property-changed.ts` | Empty interface, C# change notification |
| `i-notify-property-changing.ts` | Empty interface, C# change notification |

### Post-Processing Steps

1. **Delete MVVM files** - Remove the three files listed above
2. **Clean imports** - Remove imports of deleted types from other files
3. **Clean extends clauses** - Remove `extends INotifyPropertyChanged, INotifyPropertyChanging`
4. **Update index.ts** - Remove exports for deleted files

### Types Preserved

| Type | Reason |
|------|--------|
| `ReplayableRandom` | Data flows through client for serialization; server uses for determinism |

The `ReplayableRandom` class contains `seed` and `iterations` properties that are part of
`GameModel`. The client doesn't call `Next()` - all randomness happens server-side - but
the data must flow through for save/load round-trips.

## Testing

Serialization tests in `react-ui/lib/serialization.test.ts` verify that generated types correctly
deserialize JSON from C# (using `.catan_test` files as test data).

Run tests:

```bash
pwsh ./catan.ps1 test
# or
cd react-ui && npm run test:run
```

## Related Documentation

- [TypeScript Port Implementation Plan](../../../.design/ts-port-impl-plan.md)
- [TypeGen Official Documentation](https://typegen.net/)

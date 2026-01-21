# HexCoordinates: JsonIgnore vs Extension Methods Analysis

## Context

During the TypeScript React port, we needed to decide how to handle C# model properties
that shouldn't be serialized to JSON (and thus shouldn't appear in generated TypeScript types).

Specifically, `HexCoordinates` has computed navigation properties (`North`, `South`, etc.)
marked with `[JsonIgnore]` that provide convenient hex grid traversal in C# but shouldn't
be part of the serialized DTO.

## The Question

Should we:

1. **Keep `[JsonIgnore]`** on instance properties + post-process TypeGen output
2. **Refactor to extension methods** to keep the model pure

## Analysis Results

### Usage Locations Found

Grep search found **extensive usage** of `HexCoordinates` computed properties:

| Location | Usages | Properties Used |
|----------|--------|-----------------|
| `Catan3.Shared/Extensions/RoadModelExtensions.cs` | 12 | `.North`, `.NorthEast`, `.SouthEast`, `.South`, `.SouthWest`, `.NorthWest` |
| `Catan3.Shared/Extensions/GameModelExtensions.cs` | 6 | `.NorthEast`, `.South`, `.NorthWest`, `.North` |
| `Catan3.Shared/Extensions/BuildingModelExtensions.cs` | 6 | `.NorthEast`, `.South`, `.NorthWest`, `.North` |
| `Catan3.Shared/Extensions/TileModelExtensions.cs` | 1 | `HexCoordinates.Directions` (static) |
| `DesktopApp/Game/GameModel/GameModelExtensions.cs` | 6 | `.NorthEast`, `.South`, `.NorthWest`, `.North` |
| `DesktopApp/Roads/RoadViewModel.cs` | 4 | Uses `Direction` enum, not direct properties |

### Key Findings

1. **No Razor/WebUI usages** - The Blazor WebUI does not directly use `HexCoordinates`
   computed properties (rendering is done server-side)

2. **DesktopApp has its own copy** - `DesktopApp/Game/GameModel/GameModelExtensions.cs`
   duplicates the shared logic (6 usages of `.North`, `.South`, etc.)

3. **Catan3.Shared is the core** - 25+ usages in extension methods that implement
   adjacency logic for roads, buildings, and tiles

### Impact of Extension Method Refactoring

If refactored to extension methods:

```csharp
// Before (instance property)
key.TileKey.North

// After (extension method)
key.TileKey.North()
```

**Files requiring changes:**

- `Catan3.Shared/Extensions/RoadModelExtensions.cs` - 12 changes
- `Catan3.Shared/Extensions/GameModelExtensions.cs` - 6 changes
- `Catan3.Shared/Extensions/BuildingModelExtensions.cs` - 6 changes
- `Catan3.Shared/Utility/HexCoordinates.cs` - Move properties to extension class
- `DesktopApp/Game/GameModel/GameModelExtensions.cs` - 6 changes

**Total: ~30 call sites need `()` added**

## Tradeoff Comparison

| Aspect | `[JsonIgnore]` + Post-Processing | Extension Methods |
|--------|----------------------------------|-------------------|
| C# API ergonomics | `hex.North` (property) | `hex.North()` (method) |
| Model purity | Properties on model | Pure DTO |
| TypeGen complexity | Post-processing step | None needed |
| Codebase changes | None | ~30 call sites |
| Discoverability | IntelliSense shows on type | Requires `using` |
| Performance | Identical | Identical |

## Decision

**Keep the current `[JsonIgnore]` approach.**

### Rationale

1. **Works correctly** - TypeGen post-processing successfully removes `[JsonIgnore]`
   properties from generated TypeScript

2. **Better C# ergonomics** - Property syntax is more natural for navigation:
   `hex.North` reads better than `hex.North()`

3. **No breaking changes** - Avoids touching 30+ call sites across 5 files

4. **Isolated complexity** - The TypeGenRunner post-processing is a one-time setup
   cost, not ongoing maintenance

5. **No functional benefit** - Extension methods would achieve the same result with
   more code changes

## Implementation

The TypeGenRunner (`Catan3.Shared/TypeScript/TypeGenRunner/Program.cs`) includes:

```csharp
// Step 2b: Remove properties marked with [JsonIgnore] in C#
RemoveJsonIgnoredProperties(outputPath);
```

This function:

1. Reflects over C# types to find `[JsonIgnore]` properties
2. Uses regex to remove those properties from generated TypeScript
3. Cleans up any unused imports

See `Catan3.Shared/TypeScript/TypeGenRunner/Program.cs` for implementation details.

## When to Reconsider

Consider extension methods if:

- Many more types need `[JsonIgnore]` handling (increases post-processing complexity)
- The team prefers "pure DTO" architecture
- A major refactoring effort is already planned

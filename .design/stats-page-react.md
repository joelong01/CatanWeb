# Stats Page - React Port

## Overview

Port the Blazor Stats page to React with a simplified UX. Instead of the Blazor approach
(4-value stat cards with Total/Max/Min/Ave crammed into each cell), show a clean single-value
table with a mode selector at the top to switch between Count, Min, Max, and Average views.

## Data Source

Uses the existing `/api/players` endpoint via `gameApi.getPlayers()`. Each player profile
includes `lifetimeStats` with counters, max/min records, and aggregated `totals`.

## Type Alignment Issue

The React TypeScript types in `react-ui/types/player-profile.ts` are **wrong** - they don't
match the C# `LifetimeStats` and `GameStats` models. The hand-written `LifetimeStats` is
missing ~14 fields and the hand-written `GameStats` has completely different fields than C#.

### Root Cause

The project has a TypeGenRunner pipeline (see
[typescript-model-transforms.md](typescript-model-transforms.md)) that auto-generates
TypeScript from C# models. However, `CatanTypeGenSpec.cs` only includes types from
`Catan3.Shared.Models` — the profile types in `Catan3.Shared.Profiles` (`PlayerProfile`,
`PlayerColors`, `LifetimeStats`, `GameStats`) were never added to the spec. Someone wrote
the TypeScript types by hand and they drifted out of sync.

### Fix: Add Profile Types to TypeGenRunner

1. Add `using Catan3.Shared.Profiles;` to `CatanTypeGenSpec.cs`
2. Add profile types to the spec:

   ```csharp
   // Player profile models
   AddInterface<PlayerProfile>();
   AddInterface<PlayerColors>();
   AddInterface<LifetimeStats>();
   AddInterface<GameStats>("ProfileGameStats"); // avoid name collision if needed
   ```

3. Add `PlayerProfile` and `LifetimeStats` to `GetJsonIgnoredPropertiesMap()` in
   `Program.cs` to strip `[JsonIgnore]` backward-compat properties and static/computed
   properties
4. Run `pwsh ./catan.ps1 generate-types`
5. Update `react-ui/types/player-profile.ts` to re-export from generated types (or
   delete it and import from `react-ui/types/generated/models/` directly)
6. Fix all import sites that reference the old hand-written types

## Score Column Limitation

The user wants Score with min/max/average. The C# model only stores `HighestScoreRecord` (max).
There is no min score record and no total score for computing average. Options:

1. **Show what we have** - Display max score only, show "-" for min/average
2. **Add fields to C# model** - Requires backend changes (add `MinScoreRecord` to
   `LifetimeStats`, add `Score` to `GameStats` for totals/average). This breaks existing
   stored data.

Recommendation: Option 1 for now. Score column shows max in Max mode, "-" in Min/Average modes.

## UX Design

### Mode Selector

A segmented button group (pill tabs) at the top-right of the table, above the header row:

```text
[ Count | Min | Max | Average ]
```

- Default: **Count** (shows totals, same as current Blazor "Total" values)
- Switching modes changes only the stat columns, not the count-only columns

### Column Categories

**Always-count columns** (unaffected by mode selector):

| Column | Source |
|---|---|
| Player | name, avatar, colors |
| Games | `lifetimeStats.gamesPlayed` |
| Wins | `lifetimeStats.wins` (gold text) |
| LR Wins | `lifetimeStats.longestRoadWins` |
| LA Wins | `lifetimeStats.largestArmyWins` |

**Mode-sensitive columns** (change based on selector):

| Column | Icon | Count | Min | Max | Average |
|---|---|---|---|---|---|
| Score | trophy | - | - | `highestScoreRecord` | - |
| Soldiers | CatanGlyph.LargestArmy | `totals.soldiersPlayed` | `minSoldiersRecord` | `mostSoldiersRecord` | total/games |
| Stars | CatanGlyph.Star | `totals.starsEarned` | `minStarsRecord` | `mostStarsRecord` | total/games |
| Targeted | CatanGlyph.Target | `totals.timesTargeted` | `minTargetedRecord` | `mostTargetedRecord` | total/games |
| Robber | CatanGlyph.Robber | `totals.resourcesLostToRobber` | `minRobberRecord` | `mostRobberRecord` | total/games |

### Sorting

Default sort: Wins descending, then win rate (wins/gamesPlayed), then name alphabetically.
Same as Blazor.

### Player Row Styling

- Row background: player's CSS gradient (`colors.primary` to `colors.secondary`)
- Text color: `colors.foreground`
- Avatar with image or initials fallback
- Trophy icon next to name if `wins > 0`
- Player column is sticky-left on horizontal scroll

### Display Formatting

- Integers: no decimal
- Averages: one decimal place (e.g., "3.2")
- Min values showing `int.MaxValue` (2147483647): display as "-"
- Score in Count/Min/Average modes: display as "-" (data unavailable)

## Files to Modify

| File | Change |
|---|---|
| `Catan3.Shared/TypeScript/CatanTypeGenSpec.cs` | Add Profile types to generation spec |
| `Catan3.Shared/TypeScript/TypeGenRunner/Program.cs` | Add Profile types to JsonIgnore map |
| `react-ui/types/player-profile.ts` | Replace hand-written types with re-exports from generated |
| `react-ui/app/stats/page.tsx` | Replace placeholder with full stats page |
| Import sites referencing old types | Update imports if paths change |

## Verification

1. `pwsh ./catan.ps1 build` passes
2. Stats page loads and displays player data from API
3. Mode selector switches between Count/Min/Max/Average views
4. Player rows show correct colors and avatars
5. Sorting works (wins descending)
6. Horizontal scroll works with sticky player column

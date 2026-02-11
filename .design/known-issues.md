# Known Issues & TODOs

**Last verified:** January 30, 2026

## Tailwind v4 Migration

### @utility vs @layer utilities

Tailwind v4 treats `@layer utilities` as a standard CSS cascade layer, not
a Tailwind instruction. Any class name matching a Tailwind pattern (e.g.,
`animate-*`) defined in `@layer utilities` gets silently dropped because
Tailwind tries to resolve it through the theme system.

**Fix applied:** All custom utilities in `globals.css` now use the `@utility`
directive instead. This includes hex clip paths, 3D transforms, and all
animation utilities.

**Risk:** New custom utilities must use `@utility`, not `@layer utilities`.
This is a recurring trap since most Tailwind v3 documentation and examples
still show the old pattern.

## Outdated Design Documents

The following documents in `.design/old/` are outdated or inaccurate.
The docs in `.design/` root are the verified source of truth.

| Document | Issue |
|----------|-------|
| `old/systems/game-service-api.md` | Labels `/api/game/action` as "legacy desktop path" -- it is actually the PRIMARY command endpoint for React. See [game-service-api.md](game-service-api.md) for corrected reference. |
| `old/systems/board-rendering.md` | Describes Blazor SVG string generation pipeline. React uses DOM-based HexGrid components instead. See [board-rendering.md](board-rendering.md). |
| `old/systems/mvvm-messaging.md` | Describes Desktop MVVM pattern only. No coverage of React POST-based command flow. |
| `old/systems/database.md` | Lists 4 entity tables. Actual schema has 6 (missing CompletedGameEntity, RecordingEntity). See [database.md](database.md). |
| `old/systems/settings.md` | Missing `GriefDodgy` house rule property. See [settings.md](settings.md). |
| `old/ui/board-measurement.md` | Blazor-specific board sizing. React uses HexGrid auto-sizing. |
| `old/ui/player-viewmodel.md` | Blazor ViewModel pattern. React uses Zustand stores. |
| `old/ui/uiscale-design.md` | Blazor scaling architecture. React uses CSS/Tailwind responsive. |
| `old/ui/game-play-design.md` | Partially accurate for state transitions but UI details are Blazor-specific. |

## Proposed But Not Implemented

See [proposals.md](proposals.md) for full details on all 7 proposals.

Key proposals:

- **Pane Visibility System** -- centralizes scattered `@if` conditions
  in Blazor `Game.razor`. Ready to implement but less relevant with
  React migration.
- **API/Data Versioning** -- no versioning exists; schema changes
  require manual migration.
- **Single-Table Schema** -- proposed but rejected in favor of current
  6-table design.
- **Stats Management CLI** -- `stats` verb not yet implemented in
  `catan.ps1`.

## Known Bugs

### GriefDodgy Default Value

`HouseRules.GriefDodgy` defaults to `true` in `HouseRules.cs` (line 50).
Should default to `false` to avoid unwanted animations when creating
new games with "Use House Rules" checked.

### Duplicate Font File

`Catan.ttf` exists in two React locations:

- `react-ui/public/fonts/Catan.ttf` (loaded by Next.js)
- `react-ui/public/themes/base/fonts/Catan.ttf` (referenced by theme.json)

Only the first is actively loaded. The duplicate should be removed or
theme.json should reference the canonical location.

### Glyph Constant Duplication

`Building.tsx` duplicates glyph constants locally instead of importing
from `lib/constants/catanGlyphs.ts`.

## Known Gaps in React Port

### Not Yet Implemented

- **GriefDodgy animations** -- Tile flip, fake-out robber, and celebration
  are Blazor-only. See [grief-dodgy.md](grief-dodgy.md).
- **Portrait/mobile layout** -- Store infrastructure exists but orientation
  detection, tab UI, and auto-layout switching are not wired up.
  See [portrait-mode.md](portrait-mode.md).
- **Settings page** -- No React equivalent of Blazor Settings.razor.
- **Edit Players page** -- No React equivalent.
- **Statistics page** -- No React equivalent.
- **TooManyCards discard UI** -- Not implemented in React.
- **Board viewport pan/zoom** -- `BoardViewport.tsx` exists but is not
  wired into the game page.
- **Trade UI** -- By design: trading uses physical cards (hybrid model).
- **No authentication** -- All endpoints trust caller-supplied `playerId`.
  See [game-service-api.md](game-service-api.md).

### Resolved in Recent Sessions

- Winner flow refactor -- replaced WinnerDialog, WinnerCelebration,
  VictoryPointsOverlay with unified WinnerOverlay (Jan 30, 2026)
- Roll stats array indexing (off-by-one) -- fixed Jan 30, 2026
- Purchase count badges showing wrong counts -- fixed Jan 30, 2026
- Road/city keyboard label overlap -- fixed Jan 28, 2026
- Enter key not triggering Next -- fixed Jan 30, 2026
- Turbopack dev server crashes -- fixed (flag spelling)

## Session History (React Port Timeline)

| Date | Milestone |
|------|-----------|
| Jan 21 | Home page, NavMenu, MainLayout, TypeScript enum conversion |
| Jan 23 | HexGrid component system (flat-top geometry, responsive) |
| Jan 25 | HexGrid accessibility, constants cleanup, CSS variables |
| Jan 28 AM | Server-driven UI refactoring, composite hooks, GameBoard simplification |
| Jan 28 PM | Road/city labeling, winner celebration, VP overlay |
| Jan 30 AM | Roll stats fix, purchase badges, Enter key, WinnerOverlay fireworks |
| Jan 30 PM | As-built documentation audit (19 docs), Gemini comparison |
| Jan 30 PM | Expanded to 30 docs: all design files verified and absorbed |

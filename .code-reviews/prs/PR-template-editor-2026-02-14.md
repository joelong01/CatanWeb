# PR Code Review: template-editor

**Branch:** template-editor
**Base:** main
**Reviewed:** 2026-02-14
**Reviewer:** Claude Opus 4.6

## Summary

This PR adds the interactive template editor (Phase 2), hex layout algorithms
with parity tests, SideToDirection/DirectionToSide shared mappings, home page
Coming Soon hexes, New Game page component refactors, and DiceCluster removal.
23 files changed, ~4100 insertions, ~900 deletions across 7 commits.

## Changes Overview

| Commit | Purpose |
|--------|---------|
| f3b2340 | Hex layout algorithms + SideToDirection/DirectionToSide mappings |
| 9c620d4 | Interactive template editor (Phase 2) |
| d64437b | Simplify GameTypeSelector and PlayerSelector |
| 117410d | Home page Coming Soon hexes |
| e84b291 | Remove unused DiceCluster component |
| e96b910 | Session summary |
| e6b56a2 | Fix bugs found during code review |

## Files Changed

| File | Changes | Risk |
|------|---------|------|
| Catan3.Shared/Utility/HexCoordinates.cs | Spiral/square generators, SideToDirection | Low |
| Tests/Shared/HexCoordinatesLayoutTests.cs | New parity tests | Low |
| react-ui/components/hex-grid/hex-geometry.ts | TS layout algorithms, mappings | Low |
| react-ui/components/hex-grid/HexGrid.tsx | fitToParent prop | Low |
| react-ui/components/hex-grid/index.ts | Barrel exports | Low |
| react-ui/components/game/board/GameBoard.tsx | Import consolidation, None filter | Low |
| react-ui/app/templates/[id]/page.tsx | Template editor page (new) | Medium |
| react-ui/app/templates/page.tsx | Template list page (new) | Medium |
| react-ui/components/templates/EditorBoard.tsx | Editor board (new) | Medium |
| react-ui/components/templates/TileContextMenu.tsx | Tile context menu (new) | Low |
| react-ui/components/templates/WaterContextMenu.tsx | Water context menu (new) | Low |
| react-ui/components/templates/HarborContextMenu.tsx | Harbor context menu (new) | Low |
| react-ui/lib/api/gameApi.ts | Template CRUD endpoints, 204 fix | Medium |
| react-ui/app/page.tsx | Coming Soon hexes | Low |
| react-ui/components/new-game/GameTypeSelector.tsx | Simplification | Low |
| react-ui/components/new-game/PlayerSelector.tsx | Simplification | Low |
| react-ui/components/game/controls/DiceCluster.tsx | Deleted | Low |
| react-ui/app/controls-test/page.tsx | DiceCluster cleanup | Low |

## Critical Issues

### 1. apiFetch crashes on 204 No Content (FIXED)

**Location:** `react-ui/lib/api/gameApi.ts:164`
**Status:** Fixed in commit e6b56a2

`apiFetch` unconditionally called `response.json()` on all successful responses.
Delete endpoint returns 204 No Content, causing `SyntaxError` on JSON parse.
Fixed by checking for 204 status before parsing.

## Important Issues

### 1. SVG ID collisions across harbors (FIXED)

**Location:** `react-ui/components/templates/EditorBoard.tsx:110,158,163`
**Status:** Fixed in commit e6b56a2

`EditorHarborHex` used `editor-harbor-clip-${side}` for SVG IDs. Two harbors
with the same side value would collide. Fixed by using unique harbor identity
(tile coords + side) in IDs.

### 2. Duplicate React keys for harbors (FIXED)

**Location:** `react-ui/components/templates/EditorBoard.tsx:362`
**Status:** Fixed in commit e6b56a2

Harbor items used `harbor-${coordKey(waterCoord)}` as keys. Two harbors
pointing to the same water hex would produce duplicate keys. Fixed by using
harbor identity in keys.

### 3. Clone template ID collision (FIXED)

**Location:** `react-ui/app/templates/page.tsx:72`
**Status:** Fixed in commit e6b56a2

Clone produced deterministic ID `${id}-copy`. Repeated clones would collide.
Fixed by appending timestamp.

## Suggestions

- The passive wheel listener concern in EditorBoard is low risk with React 19's
  synthetic event handling, but could be revisited if zoom-while-scrolling issues
  are reported.
- HexSide type mismatch (hex-geometry excludes None, generated type includes it)
  causes casting at usage sites. A future cleanup could reconcile these types.

## Security Review

- No hardcoded credentials or secrets
- No SQL injection vectors (all API calls use fetch with JSON body)
- No XSS vulnerabilities (React auto-escapes, no dangerouslySetInnerHTML)
- Context menus use React portals safely
- Template data validated on server side

## Testing Verification

- .NET tests: 80 total, 76 passed, 2 skipped (deprecated), 2 passed (GameService)
- TypeScript tests: All passed
- New xunit tests for spiral/square coordinate generation parity
- Manual testing needed: template editor interaction (right-click menus, flip
  animation, tile table sync)

## Approval Status

- [x] No critical issues (all fixed)
- [x] Build passes
- [x] Tests pass
- [x] Ready for PR

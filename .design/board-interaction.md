# Board Interaction Model

**Status:** Reviewed — incorporating feedback

## Problem

The game board has scattered click handlers on individual DOM elements
(GameTile, Building, Road) passed as prop callbacks through `GameBoardProps`.
This causes four problems:

1. **MustMoveRobber fails on overlays.** Buildings and roads render in a
   separate DOM overlay layer from tiles. Right-clicking a building never
   reaches the tile underneath, so robber placement only works if you click
   on exposed tile surface between overlays.

2. **Browser context menu leaks.** Buildings and roads lack `onContextMenu`
   handlers. Right-clicking them shows the system context menu instead of
   triggering game actions.

3. **Pan requires ctrl/cmd+drag.** On Mac, ctrl+click opens the system
   context menu, making ctrl-based panning unreliable. There is no way to
   pan by dragging on water tiles.

4. **No touch support.** `GameBoard.tsx` has zero touch event handlers.
   Mobile users cannot pan or trigger right-click actions (robber placement).

## Solution

Replace the scattered DOM click handlers with a **single unified interaction
handler** on the board container. The handler uses hex math to determine what
the user clicked, then checks game state and dispatches the appropriate action.

The core insight: we already have the math infrastructure to answer "what is
at this pixel?" without relying on DOM event targets:

- `pixelToHex()` converts a pixel position to a hex coordinate
- `getVertexPosition()` returns the pixel position of any building vertex
- `getEdgeMidpoint()` returns the pixel position of any road edge
- `TileModel.resourceTileType` tells us whether a hex is Sea, Desert,
  Wheat, etc.

## Hit-Test API

A single function answers "what is under this pixel?" and returns a typed
result. No special-casing of water — Sea is a tile with
`resourceTileType === 'Sea'`.

```typescript
HitTarget =
  | { type: 'tile';     tile: TileModel }
  | { type: 'road';     key: RoadKey;     model: RoadModel;     tile: TileModel }
  | { type: 'building'; key: BuildingKey; model: BuildingModel; tile: TileModel }
  | { type: 'none' }
```

Every road and building result also carries the **underlying tile** (resolved
via `pixelToHex`). This lets dispatch fall through to tile-level behavior
when the road or building isn't actionable.

**Priority order:** building > road > tile > none. Buildings are smallest
targets and get priority. Roads are next. If neither matches, we check for
a tile. If no tile matches, we return `none` (clicked outside the board).

**Existing infrastructure used:**

| What | File | Purpose |
| ---- | ---- | ------- |
| `pixelToHex()` | `hex-geometry.ts` | Pixel → hex coordinate |
| `getVertexPosition()` | `hex-geometry.ts` | Hex coord + position → vertex pixel pos |
| `getEdgeMidpoint()` | `hex-geometry.ts` | Hex coord + side → edge midpoint pixel pos |
| `tileCoordSet` | `GameBoard.tsx` | Set of tile hex coord strings for lookup |
| `buildingPositions` | `GameBoard.tsx` | All unique vertex positions |
| `roadPositions` | `GameBoard.tsx` | All unique edge positions |
| `buildingMap` / `roadMap` | `GameBoard.tsx` | Position key → model lookup |
| `TileModel.resourceTileType` | `tile-model.ts` | `ResourceType` of the tile |

## Dispatch Model

The dispatch function takes a `HitTarget`, a button (`left` or `right`),
and reads the current `gameState`. The logic:

1. If `type === 'none'` → no action.
2. Resolve the underlying tile (every `HitTarget` variant carries one).
3. If `tile.resourceTileType === 'Sea'` → no game action. Sea tiles are
   pan-only surfaces. No left-click, no right-click. Return early.
4. **Right-click** (non-Sea only) → dispatch to the tile right-click
   handler (`onTileRightClick`) using the underlying tile, regardless of
   whether the user clicked a building, road, or tile surface. The game
   page handler validates game state, tile type, and robber position.
5. **Left-click on building** → if actionable (e.g., upgradable), call
   `onBuildingClick`. Otherwise fall through to tile.
6. **Left-click on road** → if buildable (`RoadState.Buildable`), call
   `onRoadClick`. Otherwise fall through to tile.
7. **Left-click on tile** (or fallthrough) → call `onTileClick`.

The game page callbacks (`handleBuildingClick`, `handleRoadClick`,
`handleTileRightClick`) already validate game state internally. The dispatch
function routes to them; it doesn't duplicate validation.

## Pan and Zoom

**Current:** Ctrl/cmd+drag to pan, mouse wheel to zoom. Broken on Mac
(ctrl+click = context menu).

**New model — drag-threshold panning:**

- On **pointer down**, hit-test the position and record it.
- On **pointer move**, if distance from start exceeds a threshold (5px),
  check whether panning is allowed:
  - **Sea tile or `none`**: pan without modifier keys (water = pan surface)
  - **Modifier key held** (ctrl/cmd): pan on any surface (backward
    compatible)
  - **Resource tile without modifier**: no pan (preserves click behavior)
- On **pointer up**, if no panning occurred, dispatch as a click.

This gives maps-like behavior: click and drag on water to pan, click on
game elements to interact. Mouse wheel zoom is unchanged.

**Context menu suppression:** A single `onContextMenu` handler with
`e.preventDefault()` on the board container prevents the browser context
menu everywhere. Right-clicks are routed through the dispatch function
instead.

## Touch Support

**PointerEvents** (`onPointerDown/Move/Up`) unify mouse and touch into one
code path. This avoids duplicating logic across separate mouse and touch
handlers.

Touch-specific behavior:

- **Tap** = left-click (handled naturally by PointerEvents)
- **Drag** = pan on **any** surface (not just Sea tiles). This is a
  deliberate divergence from mouse behavior. On desktop, resource tiles
  without modifier don't pan because click-vs-drag is ambiguous with
  hover. On touch there is no hover — the drag threshold (5px)
  unambiguously separates tap (click) from drag (pan). This matches
  standard mobile map behavior (Google Maps pans from any surface).
- **Long-press** (500ms hold without movement) = right-click. This is how
  mobile users place the robber. Optional haptic feedback via
  `navigator.vibrate`.

**Long-press state machine:**

1. On pointer down, start a 500ms timer.
2. If pointer moves past the drag threshold before 500ms → cancel timer,
   begin panning. No right-click fires.
3. If 500ms elapses without movement → fire right-click dispatch, set a
   `longPressFired` flag, optionally vibrate.
4. On pointer up, check `longPressFired` — if true, suppress the
   left-click dispatch. If false and no panning occurred, dispatch as
   left-click (tap).

Container CSS: `touch-action: none` prevents browser scroll/zoom interference
with custom gesture handling.

## Hover

Hover stays **DOM-based** — it is not routed through the unified handler.

Current hover behavior:

| Component | Mechanism | Visual feedback |
| --------- | --------- | --------------- |
| GameTile | CSS-only (`hover:scale-[1.02]`) | 2% scale-up |
| Building | JS `onMouseEnter/Leave` + `isHovered` state | 15% scale-up; hidden buildings reveal star count |
| Road | JS `onMouseEnter/Leave` (tracked, barely used) | Cursor change only |

Building hover has real game logic: hidden building spots switch from
`'Hidden'` to `'Stars'` visual state on hover, revealing star counts.
This is not just cosmetic — it communicates information.

**Why not unify hover?** Hover doesn't need game-state dispatch. Running
hit-testing on every mousemove (60fps) to replicate what CSS and
`onMouseEnter` already do would be pure overhead. The overlay keeps
`pointer-events` enabled so DOM hover works naturally. Click events from
overlay elements bubble up to the container where the unified handler
lives — it ignores the DOM target and does its own math-based hit-testing.

**Consequence:** The overlay div does **not** get `pointer-events: none`.
Both hover (DOM-based) and clicks (math-based unified handler) coexist.

## FloatingPanel Touch Interaction

FloatingPanel already has separate mouse and touch handlers for drag-to-move
and corner resize. This works but has touch-target problems.

**Current state:**

| Action | Desktop | Mobile | Touch target |
| ------ | ------- | ------ | ------------ |
| Drag to move | Ctrl+click or background click | 400ms long-press + drag | Whole panel |
| Resize | Corner drag | Corner touch-drag | 16px (w-4 h-4) |
| Minimize | Click "─" button | Tap | 20px (w-5 h-5) |
| Restore from tray | Click minimized bar item | Tap | 100px+ wide |
| Context menu (tray) | Right-click | 400ms long-press | 100px+ wide |

**Problem: resize handle is too small for touch.** Apple's HIG recommends
44px minimum touch targets. The current 16px corner handle is nearly
impossible to hit with a finger. The minimize button at 20px is also
borderline.

**Fixes needed:**

1. **Resize handle**: Increase touch target to at least 44px. The visual
   indicator can stay small (the diagonal lines), but the invisible hit
   area should extend further into the panel corner. Use padding or an
   oversized transparent hit area.
2. **Minimize button**: Increase to at least 44px touch target. Same
   approach — visual stays compact, hit area extends.
3. **No PointerEvents migration needed.** FloatingPanel's separate
   mouse+touch handlers work correctly. The panel sits above the board
   in z-order and uses `stopPropagation`, so its events don't conflict
   with the board's unified handler.

**Event isolation:** FloatingPanel events and board events don't
interfere. Panels are rendered above the board with higher z-index.
Touch on a panel is captured by the panel's handlers and never reaches
the board container. Touch on the board (outside panels) is captured by
the board's unified handler.

## Scope

**In scope:**

- Unified hit-test and dispatch
- Drag-threshold panning on Sea tiles
- Context menu suppression
- Touch pan and long-press
- Stop passing click handler props to GameTile, Building, Road (in
  GameBoard.tsx only — the component files themselves are not modified)
- FloatingPanel: enlarge resize handle and minimize button touch targets

**Out of scope:**

- Pinch-to-zoom (two-finger gesture tracking — separate feature)
- Changes to component files: GameTile.tsx, Building.tsx, Road.tsx,
  WaterHex.tsx, HexGrid.tsx, or game page (prop interfaces stay the same,
  we just stop passing click callbacks in GameBoard.tsx)
- FloatingPanel PointerEvents migration (current mouse+touch handlers work)

## Files Affected

- `react-ui/components/game/board/GameBoard.tsx` — unified handler,
  hit-testing, pan/zoom, touch support
- `react-ui/components/game/panels/FloatingPanel.tsx` — enlarge resize
  handle and minimize button touch targets

The existing prop callbacks (`onTileClick`, `onTileRightClick`,
`onBuildingClick`, `onRoadClick`) remain unchanged — the unified handler
calls them.

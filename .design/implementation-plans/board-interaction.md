# Implementation Plan: Board Unified Interaction Handler

**Design doc:** [board-interaction.md](../board-interaction.md)

## Overview

Implements the unified board interaction handler described in the design doc.
Two files modified: `GameBoard.tsx` (main work) and `FloatingPanel.tsx`
(touch target fixes).

## Step 1 — Add `pixelToHex` import and `layoutInfoRef`

**File:** `GameBoard.tsx`

Add `pixelToHex` to the existing hex-geometry import (line 6):

```typescript
import {
  cubicCoord,
  getNeighbor,
  getVertexPosition,
  getEdgeMidpoint,
  hexToPixel,
  pixelToHex,       // ← add
  Direction,
  // ... existing types
} from '@/components/hex-grid/hex-geometry';
```

Add refs for layout info and hex grid content element after the existing
`containerRef` (around line 490):

```typescript
const layoutInfoRef = useRef<HexGridLayoutInfo | null>(null);
const hexGridContentRef = useRef<HTMLDivElement>(null);
```

Store layoutInfo from the overlay callback (line 1296):

```typescript
overlay={players.length > 0 ? (layoutInfo) => {
  layoutInfoRef.current = layoutInfo;
  return renderOverlay(layoutInfo);
} : undefined}
```

Add ref to the transform wrapper div (line 1284):

```tsx
<div
  ref={hexGridContentRef}
  className="absolute inset-0 flex items-center justify-center"
  style={{ transform: `translate(${panOffset.x}px, ${panOffset.y}px)` }}
>
```

## Step 2 — Add tile lookup map

**File:** `GameBoard.tsx`

Add a `tileMap` memo (after `tileCoordSet`, line 587) that maps coord key
strings to `TileModel` for O(1) lookup:

```typescript
const tileMap = useMemo(() => {
  const map = new Map<string, TileModel>();
  tiles.forEach((tile) => {
    const coord = cubicCoord(tile.tileKey.q, tile.tileKey.r);
    map.set(coordKeyString(coord), tile);
  });
  return map;
}, [tiles]);
```

## Step 3 — HitTarget type and hitTest function

**File:** `GameBoard.tsx`

Add the `HitTarget` type and `hitTest` function. Place after the lookup
maps section (after line 783).

```typescript
type HitTarget =
  | { type: 'tile'; tile: TileModel }
  | { type: 'road'; key: string; model: RoadModel; tile: TileModel }
  | { type: 'building'; key: string; model: BuildingModel; tile: TileModel }
  | { type: 'none' };
```

The `hitTest` function:

```typescript
function hitTest(clientX: number, clientY: number): HitTarget {
  const layout = layoutInfoRef.current;
  const contentEl = hexGridContentRef.current;
  if (!layout || !contentEl) return { type: 'none' };

  // Convert client coords → hex-grid-local coords
  const rect = contentEl.getBoundingClientRect();
  const localX = clientX - rect.left;
  const localY = clientY - rect.top;

  const { origin, hexSize: hSize } = layout;

  // 1. Resolve underlying tile
  const hexCoord = pixelToHex({ x: localX, y: localY }, hSize, origin);
  const tileKey = coordKeyString(hexCoord);
  const tile = tileMap.get(tileKey);
  if (!tile) return { type: 'none' };

  // 2. Building check — search clicked hex + 6 neighbors
  const buildingHitRadius = hSize * 0.3;
  let closestBuilding: { key: string; model: BuildingModel; dist: number } | null = null;

  const hexesToCheck = [hexCoord, ...Array.from({ length: 6 }, (_, i) => getNeighbor(hexCoord, i))];
  const hexKeySet = new Set(hexesToCheck.map(coordKeyString));

  for (const bp of buildingPositions) {
    if (!hexKeySet.has(coordKeyString(bp.coord))) continue;
    const model = buildingMap.get(bp.key);
    if (!model) continue;
    const pos = getVertexPosition(bp.coord, bp.position, hSize, origin);
    const dx = localX - pos.x;
    const dy = localY - pos.y;
    const dist = Math.sqrt(dx * dx + dy * dy);
    if (dist < buildingHitRadius && (!closestBuilding || dist < closestBuilding.dist)) {
      closestBuilding = { key: bp.key, model, dist };
    }
  }

  if (closestBuilding) {
    return { type: 'building', key: closestBuilding.key, model: closestBuilding.model, tile };
  }

  // 3. Road check — same neighbor set
  const roadHitRadius = hSize * 0.25;
  let closestRoad: { key: string; model: RoadModel; dist: number } | null = null;

  for (const rp of roadPositions) {
    if (!hexKeySet.has(coordKeyString(rp.coord))) continue;
    const model = roadMap.get(rp.key);
    if (!model) continue;
    const pos = getEdgeMidpoint(rp.coord, rp.side, hSize, origin);
    const dx = localX - pos.x;
    const dy = localY - pos.y;
    const dist = Math.sqrt(dx * dx + dy * dy);
    if (dist < roadHitRadius && (!closestRoad || dist < closestRoad.dist)) {
      closestRoad = { key: rp.key, model, dist };
    }
  }

  if (closestRoad) {
    return { type: 'road', key: closestRoad.key, model: closestRoad.model, tile };
  }

  // 4. Tile
  return { type: 'tile', tile };
}
```

Wrap in `useCallback` with deps: `[tileMap, buildingPositions, roadPositions,
buildingMap, roadMap]`.

## Step 4 — Dispatch function

**File:** `GameBoard.tsx`

Add `dispatchInteraction` after `hitTest`:

```typescript
function dispatchInteraction(target: HitTarget, button: 'left' | 'right') {
  if (target.type === 'none') return;

  const tile = target.tile;

  // Sea tiles are pan-only — no game actions
  if (tile.resourceTileType === 'Sea') return;

  // Right-click always goes to the tile (robber placement, etc.)
  if (button === 'right') {
    // Create a synthetic event for the handler signature
    onTileRightClick?.(tile, {} as React.MouseEvent);
    return;
  }

  // Left-click: dispatch based on target type with fallthrough
  switch (target.type) {
    case 'building': {
      if (onBuildingClick) {
        const bk = target.model.buildingKey;
        onBuildingClick(bk);
        return;
      }
      break;
    }
    case 'road': {
      if (target.model.roadState === 'Buildable' && onRoadClick) {
        onRoadClick(target.model.roadKey);
        return;
      }
      break; // Non-buildable road → fall through to tile
    }
  }

  // Tile-level left-click (or fallthrough)
  onTileClick?.(tile);
}
```

## Step 5 — Replace pan handlers with unified pointer handlers

**File:** `GameBoard.tsx`

Remove the existing pan state and 4 handlers (lines 505-577):

```typescript
// REMOVE:
const [isPanning, setIsPanning] = useState(false);
const [panStart, setPanStart] = useState<PixelPosition>({ x: 0, y: 0 });
// ... handleMouseDown, handleMouseMove, handleMouseUp, handleMouseLeave
```

Replace with:

```typescript
const PAN_DRAG_THRESHOLD = 5;
const LONG_PRESS_MS = 500;

const dragStateRef = useRef<{
  panning: boolean;
  startClient: PixelPosition;
  startPan: PixelPosition;
  hitTarget: HitTarget;
  isModifier: boolean;
  isTouch: boolean;
  longPressFired: boolean;
} | null>(null);

const recentPanRef = useRef(false);
const longPressTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
const [isPanningForCursor, setIsPanningForCursor] = useState(false);

const canPan = (target: HitTarget, isModifier: boolean, isTouch: boolean) => {
  if (isModifier) return true;
  if (target.type === 'none') return true;
  if (isTouch) return true; // Touch always pans (drag threshold disambiguates)
  if (target.type === 'tile' && target.tile.resourceTileType === 'Sea') return true;
  return false;
};
```

**handlePointerDown:**

```typescript
const handlePointerDown = useCallback((e: React.PointerEvent) => {
  if (e.button !== 0) return; // Left button only for pan/click
  const target = hitTest(e.clientX, e.clientY);
  const isModifier = e.ctrlKey || e.metaKey;
  const isTouch = e.pointerType === 'touch';

  dragStateRef.current = {
    panning: false,
    startClient: { x: e.clientX, y: e.clientY },
    startPan: { ...panOffset },
    hitTarget: target,
    isModifier,
    isTouch,
    longPressFired: false,
  };

  // Pointer capture so we get move/up even if cursor leaves
  (e.target as HTMLElement).setPointerCapture(e.pointerId);

  // Start long-press timer for touch
  if (isTouch) {
    longPressTimerRef.current = setTimeout(() => {
      const state = dragStateRef.current;
      if (state && !state.panning) {
        state.longPressFired = true;
        navigator.vibrate?.(50);
        dispatchInteraction(state.hitTarget, 'right');
      }
    }, LONG_PRESS_MS);
  }

  // Prevent text selection if we might pan
  if (canPan(target, isModifier, isTouch)) {
    e.preventDefault();
  }
}, [hitTest, panOffset, dispatchInteraction]);
```

**handlePointerMove:**

```typescript
const handlePointerMove = useCallback((e: React.PointerEvent) => {
  const state = dragStateRef.current;
  if (!state) return;

  const dx = e.clientX - state.startClient.x;
  const dy = e.clientY - state.startClient.y;
  const dist = Math.sqrt(dx * dx + dy * dy);

  if (!state.panning) {
    if (dist >= PAN_DRAG_THRESHOLD) {
      // Cancel long-press
      if (longPressTimerRef.current) {
        clearTimeout(longPressTimerRef.current);
        longPressTimerRef.current = null;
      }

      if (canPan(state.hitTarget, state.isModifier, state.isTouch)) {
        state.panning = true;
        setIsPanningForCursor(true);
      }
    }
  }

  if (state.panning) {
    setViewport({
      pan: {
        x: state.startPan.x + dx,
        y: state.startPan.y + dy,
      },
    });
  }
}, [setViewport]);
```

**handlePointerUp:**

```typescript
const handlePointerUp = useCallback((e: React.PointerEvent) => {
  const state = dragStateRef.current;
  if (!state) return;

  // Cancel long-press timer
  if (longPressTimerRef.current) {
    clearTimeout(longPressTimerRef.current);
    longPressTimerRef.current = null;
  }

  if (state.panning) {
    // Suppress click after pan
    recentPanRef.current = true;
    setTimeout(() => { recentPanRef.current = false; }, 0);
    setIsPanningForCursor(false);
  } else if (!state.longPressFired) {
    // Not panning, not long-press → dispatch as click
    dispatchInteraction(state.hitTarget, 'left');
  }

  dragStateRef.current = null;
}, [dispatchInteraction]);
```

**handleContextMenu:**

```typescript
const handleContextMenu = useCallback((e: React.MouseEvent) => {
  e.preventDefault();
  if (recentPanRef.current) return;
  const target = hitTest(e.clientX, e.clientY);
  dispatchInteraction(target, 'right');
}, [hitTest, dispatchInteraction]);
```

## Step 6 — Remove DOM-level click handlers

**File:** `GameBoard.tsx`

### 6a. tileItems memo (lines 610-618)

Remove `onClick` and `onRightClick` props from GameTile:

```tsx
<GameTile
  tile={tile}
  hexSize={hexSize}
  isHighlighted={isHighlighted}
  isDimmed={isDimmed}
  // onClick and onRightClick REMOVED
  tileIndex={showTileIndexes ? index + 1 : undefined}
/>
```

Remove `onTileClick` and `onTileRightClick` from the dependency array
(line 622).

### 6b. renderOverlay — Road onClick (lines 974-978)

Remove the onClick prop from Road:

```tsx
<Road
  roadState={roadState}
  side={side}
  ownerId={ownerId}
  currentPlayerId={currentPlayerId}
  hexSize={hSize}
  buildIndex={roadModel.buildIndex}
  // onClick REMOVED
/>
```

### 6c. renderOverlay — Building onClick, Loop 1 (lines 1031-1035)

Remove onClick from owned buildings:

```tsx
<Building
  buildingState={buildingState}
  visualState={isCityUpgradeable ? 'Highlighted' : 'Normal'}
  ownerId={ownerId}
  currentPlayerId={currentPlayerId}
  size={ownedBuildingSize}
  buildIndex={cityUpgradeIndex}
  // onClick REMOVED
/>
```

### 6d. renderOverlay — Building onClick, Loop 2 (lines 1134-1136)

Remove onClick from building spots:

```tsx
<Building
  buildingState="PossibleSettlement"
  visualState={visualState}
  stars={stars}
  currentPlayerId={currentPlayerId}
  size={buildableBuildingSize}
  buildIndex={settlementBuildIndex}
  // onClick REMOVED
/>
```

Remove `onBuildingClick` and `onRoadClick` from renderOverlay's
dependency array (lines 1251-1252).

## Step 7 — Wire container element

**File:** `GameBoard.tsx`

Update the container div (lines 1268-1278):

```tsx
<div
  ref={containerRef}
  className="relative w-full h-full overflow-hidden select-none"
  onWheel={handleWheel}
  onPointerDown={handlePointerDown}
  onPointerMove={handlePointerMove}
  onPointerUp={handlePointerUp}
  onContextMenu={handleContextMenu}
  style={{
    cursor: isPanningForCursor ? 'grabbing' : 'default',
    touchAction: 'none',
  }}
>
```

Remove `onMouseDown`, `onMouseMove`, `onMouseUp`, `onMouseLeave`.

## Step 8 — FloatingPanel touch targets

**File:** `FloatingPanel.tsx`

### 8a. Resize handle (line 451-463)

Increase touch target from 16px to 44px. Keep visual indicator small:

```tsx
<div
  className="absolute bottom-0 right-0 w-11 h-11 cursor-se-resize z-10"
  onMouseDown={handleResizeStart}
  onTouchStart={handleResizeStart}
>
  {/* Visual indicator stays small, positioned in corner */}
  <svg
    className="absolute bottom-0 right-0 w-4 h-4 text-gray-600 hover:text-gray-400 transition-colors"
    viewBox="0 0 16 16"
    fill="currentColor"
  >
    <path d="M14 14H12V12H14V14ZM14 10H12V8H14V10ZM10 14H8V12H10V14Z" />
  </svg>
</div>
```

### 8b. Minimize button (line 438-448)

Increase touch target from 20px to 44px. Keep visual compact:

```tsx
<button
  onClick={(e) => {
    e.stopPropagation();
    toggleMinimize(panelId);
  }}
  className="absolute top-0 right-0 w-11 h-11 flex items-center justify-center z-10"
  title="Minimize"
>
  <span className="w-5 h-5 flex items-center justify-center bg-gray-900/70 text-gray-400 hover:text-white hover:bg-gray-700 rounded text-xs transition-colors backdrop-blur-sm">
    ─
  </span>
</button>
```

## Verification

1. `pwsh ./catan.ps1 build` — no TypeScript errors
2. Manual testing (desktop):
   - **Water pan**: Click-drag on Sea tiles pans without modifier
   - **Ctrl/cmd pan**: Still works on any surface (backward compatible)
   - **Tile click**: Left-click on resource tile fires `onTileClick`
   - **Building click**: Left-click on building fires `onBuildingClick`
   - **Road click**: Left-click on buildable road fires `onRoadClick`
   - **Road fallthrough**: Left-click on non-buildable road fires `onTileClick`
   - **No browser context menu**: Right-click anywhere → no system menu
   - **MustMoveRobber**: Right-click on building/road/tile → robber placement
   - **Hover preserved**: Building scale-up and hidden→stars reveal still work
3. Manual testing (mobile/touch):
   - **Tap**: Tap on building → building click fires
   - **Long-press**: Long-press on tile → right-click dispatch (robber menu)
   - **Long-press cancel**: Start long-press, move finger → pans instead
   - **Drag-to-pan**: Single-finger drag on any surface → pans
   - **FloatingPanel resize**: Corner resize handle hittable with finger
   - **FloatingPanel minimize**: Minimize button hittable with finger

# HexGrid ItemsControl Refactor + Call Site Migration

## Context

HexGrid currently requires callers to manually specify coordinates for
every item (`HexGridItem[]` with explicit `coord` on each). This forces
all call sites to wire `cubicCoord(q, r)` per item, which is error-prone
(Guest bug at `cubicCoord(0, 2)` in PlayerSelector) and prevents
data-driven layouts (template editor stepper creates tiles at (0,0)
causing collisions).

The fix: make HexGrid work like XAML's ItemsControl -- give it a count
and layout algorithm, it generates coordinates, the caller provides a
render function for each position. `getSpiralCoordinates(n)` and other
layout generators already exist in `hex-geometry.ts` and `layouts.ts`.

## HexGrid API Changes

**File:** `react-ui/components/hex-grid/HexGrid.tsx`

Add a new rendering mode alongside the existing `items` prop:

```tsx
interface HexGridProps {
  // EXISTING (backward compat - unchanged)
  items?: HexGridItem[];

  // NEW: Layout-driven rendering (ItemsControl pattern)
  coordinates?: HexCoordinate[];     // from getSpiralCoordinates, LAYOUTS, etc.
  renderItem?: (coord: HexCoordinate, index: number) => ReactNode;
  itemIds?: string[];                // optional explicit ids (default: index)
  itemClassName?: string;            // shared className for all items
  excludeFromBounds?: boolean[];     // per-item bounds exclusion

  // EXISTING (unchanged)
  hexSize: number;
  gap?: number;
  borderColor?: string;
  fitToParent?: boolean;
  // ... rest unchanged
}
```

**Internal:** When `coordinates + renderItem` are provided, HexGrid
builds `HexGridItem[]` internally:

```tsx
const derivedItems: HexGridItem[] = coordinates.map((coord, i) => ({
  id: itemIds?.[i] ?? `item-${i}`,
  coord,
  content: renderItem(coord, i),
  className: itemClassName,
  excludeFromBounds: excludeFromBounds?.[i],
}));
```

Validation: error if both `items` and `coordinates` are provided.

**Barrel export:** `getSpiralCoordinates` is already exported from
`hex-grid/index.ts`.

## Migration Plan

### Phase 1: HexGrid API Extension

1. Add `coordinates` + `renderItem` props to `HexGridProps`
2. Add internal derivation logic
3. No existing call sites change -- pure additive

### Phase 2: HomePage (`react-ui/app/page.tsx`)

**Current:** Two clusters with manual `cubicCoord()` per item.

**Game cluster** (5 items: center + 4 menus):

```tsx
const gameButtons = [
  <CenterHex icon={faDice} title="Catan" ... />,
  activeGameId
    ? <MenuHex icon={faPlay} title="Return to" ... />
    : <MenuHex icon={faUsers} title="Edit Players" ... />,
  <MenuHex icon={faGamepad} title="New Game" ... />,
  <MenuHex icon={faFolderOpen} title="Open Game" ... />,
  <MenuHex icon={faChartBar} title="Stats" ... />,
];
<HexGrid
  hexSize={140}
  coordinates={getSpiralCoordinates(gameButtons.length)}
  renderItem={(_, i) => gameButtons[i]}
  gap={4}
  scale={hexScale}
/>
```

**Dev cluster** (7 items: center + 6 menus):
Same pattern. No more `cubicCoord(1, 0)` / `cubicCoord(-1, 1)` wiring.

### Phase 3: NewGamePage (`react-ui/app/new-game/page.tsx`)

Two sub-components:

**GameTypeSelector** (`react-ui/components/new-game/GameTypeSelector.tsx`):

- 7 items in CLUSTER_7 layout (center + 6 game types/water)
- Replace `HEX_LAYOUTS.CLUSTER_7[idx]` indexing with `coordinates` prop
- Water hexes become items in the render function (index 3, 4 are water)

**PlayerSelector** (`react-ui/components/new-game/PlayerSelector.tsx`):

- **Fixes Guest bug.** Currently Guest is at `cubicCoord(0, 2)` (detached).
- New: all players (including Guest) are just items in the list:

  ```tsx
  const allPlayers = [...visiblePlayers, ...(includeGuest ? [guestPlayer] : [])];
  <HexGrid
    hexSize={hexSize}
    coordinates={getSpiralCoordinates(allPlayers.length + 1)}
    renderItem={(coord, index) => {
      if (index === 0) return <CenterHex ... />;
      return <PlayerCardContent player={allPlayers[index - 1]} ... />;
    }}
  />
  ```

- Guest naturally occupies the next spiral position. No special coordinate.

### Phase 4: GamePage (`react-ui/components/game/board/GameBoard.tsx`)

GameBoard is multi-layer (water + tiles + harbors + overlay). It
composes heterogeneous item types, so the explicit `items` API is
appropriate here. The refactor is lighter:

- **Keep** the `items` prop for the main HexGrid render
- **Use** `getSpiralCoordinates` or layout helpers for water hex
  generation (replace the manual nested loop at lines 711-747)
- **Clean up** the `allItems = [...waterItems, ...tileItems, ...harborItems]`
  composition to use the existing layout utilities where possible

Control clusters within GamePage:

- **DiceCluster, ActionCluster, RollRing, MeasurementCluster**: evaluate
  for migration to `coordinates + renderItem` where beneficial
- These are small and self-contained; migrate if the code gets simpler

### Phase 5: Return to Template Editor

With HexGrid supporting layout-driven rendering, the template editor
can use the distribution-based model from Addendum 2:

- `TemplateIsland` stores `ResourceCounts` + `NumberCounts`
- `tileCount = sum(ResourceCounts)`
- EditorBoard: `coordinates={getSpiralCoordinates(tileCount)}`
- `renderItem` maps spiral positions to current assignment state
- Steppers modify distribution counts -> tileCount changes -> spiral
  regenerates -> board updates

This is Phase 5 and will be planned in detail after Phases 1-4.

## Files Modified

| File | Change |
|------|--------|
| `react-ui/components/hex-grid/HexGrid.tsx` | Add `coordinates` + `renderItem` props |
| `react-ui/app/page.tsx` | Replace manual coords with spiral layout |
| `react-ui/components/new-game/GameTypeSelector.tsx` | Replace HEX_LAYOUTS indexing |
| `react-ui/components/new-game/PlayerSelector.tsx` | Fix Guest bug, use spiral |
| `react-ui/components/game/board/GameBoard.tsx` | Clean up water generation |
| `react-ui/components/game/controls/DiceCluster.tsx` | Evaluate for migration |
| `react-ui/components/game/controls/ActionCluster.tsx` | Evaluate for migration |

## Verification

1. `pwsh ./catan.ps1 build` -- all projects compile
2. `pwsh ./catan.ps1 test` -- all 59+ tests pass
3. Manual: HomePage renders both clusters correctly
4. Manual: NewGamePage -- Guest player appears in cluster (not detached)
5. Manual: GameBoard renders identically to before
6. Manual: All overlays (GoFirst, Supplemental, Winner) still work
7. Verify spiral coordinates match existing Regular (19) and Expansion
   (30) board layouts before using for template editor

## Risks

- **Spiral order matters:** The spiral determines which position each
  item gets. If the order doesn't match the visual intent (e.g., "Stats"
  should be at bottom), we may need to reorder the data array. This is
  a feature, not a bug -- the array order IS the layout order.
- **GameBoard complexity:** Multi-layer composition doesn't fit the
  ItemsControl pattern cleanly. Keep explicit items there.
- **Expansion board (30 tiles):** Need to verify `getSpiralCoordinates(30)`
  matches the actual expansion tile coordinates. If not, may need a
  custom layout for expansion (3 full rings = 19, then 11 of ring 4).

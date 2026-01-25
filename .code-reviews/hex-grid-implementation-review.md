# Code Review: Hex Grid Component System

**Reviewer:** Claude Opus 4.5
**Date:** 2026-01-23
**Branch:** typescript-react-port
**Files Changed:** 4 modified, 4 new files
**Design Doc:** `.design/ui/react/hex-grid-component-design.md`

## Summary

This changeset replaces card-based game type and player selectors with a reusable hex grid layout system. The implementation closely follows the design document's architecture (hex-geometry.ts, HexTile.tsx, HexGrid.tsx) and correctly ports Red Blob Games formulas from the C# codebase.

## Design Compliance

### Matches Design

| Aspect | Status |
|--------|--------|
| File structure (hex-grid/, 4 files) | Compliant |
| `calculateHexDimensions` formula | Compliant |
| `hexToPixel` formula | Compliant |
| `HEX_LAYOUTS.CLUSTER_7` coordinates | Compliant |
| Two-hex polygon border approach | Compliant |
| HexSize = 80 for both selectors | Compliant |
| Scale prop support | Compliant |
| Player gradient always visible | Compliant |
| Center hex as label (disabled) | Compliant |

### Intentional Divergences (implementation is correct, design needs update)

1. **Selection indicator**: Design says outer hex border changes to `player.colors.primary` on selection. Implementation uses a checkmark overlay at `top: 16%, left: 68%` instead. User explicitly approved this approach.

2. **Circular selection**: Design says `disabled: !canAddMore && !selectedPlayerIds.includes(player.id)`. Implementation uses circular FIFO removal (remove oldest, add new). User explicitly requested this.

3. **No section headers**: Design shows `<h2>` headers above grids. Implementation relies on center hex content ("Choose Game" / "Choose Players") as the label. User removed headers.

4. **Gradient direction**: Design says `linear-gradient(135deg, primary, secondary)`. Implementation uses `linear-gradient(160deg, primary 0%, secondary 70%, rgba(0,0,0,0.3) 100%)` adding a dark edge for depth.

5. **Guest hex at (-2, 1)**: Not in original design. Added per user request to show Guest player as a separate hex controlled by checkbox.

6. **Sitting Order moved to page.tsx**: Drag-drop reordering is now in the parent page, not inside PlayerSelector. Clean separation of concerns.

7. **Water placeholder hexes**: GameTypeSelector fills unused CLUSTER_7 positions with water tile images. Not mentioned in design but sensible.

8. **Origin formula**: Design originally said `containerWidth / 2 - minX` but implementation correctly uses `dims.width / 2 - minX`. The design was already updated to match.

## Bugs

### B1: `CLUSTER_30` has 29 entries (not 30)

**File:** `react-ui/components/hex-grid/hex-geometry.ts:144-157`

```typescript
CLUSTER_30: [
  // Row -2: 4 hexes
  // Row -1: 5 hexes
  // Row 0:  6 hexes
  // Row 1:  5 hexes
  // Row 2:  5 hexes
  // Row 3:  4 hexes
  // Total: 29, not 30
]
```

**Impact:** Low (not currently used). Should be verified against actual Catan expansion board layout before use.

### B2: `gap` parameter has no effect on layout

**File:** `react-ui/components/hex-grid/hex-geometry.ts:37`, `HexGrid.tsx:81`

The `gap` parameter is stored in `HexDimensions` but never used in `hexToPixel` calculations. Hex positions are computed without any gap adjustment. The visual "gap" comes entirely from the two-hex polygon approach (outer/inner hex at 91% scale).

```typescript
// gap is calculated and stored...
const dims = calculateHexDimensions(hexSize, gap);
// ...but never used in positioning
const pos = hexToPixel(item.coord, hexSize, origin);
```

**Impact:** Medium. The `gap` prop on `<HexGrid gap={2}>` is misleading - it doesn't actually create spacing between hexes. Either:

- Remove the `gap` parameter entirely (since spacing comes from scale(0.91))
- Implement gap by reducing the effective hex size in `hexToPixel` calculations

**Recommendation:** Remove `gap` from the public API since the two-hex border approach inherently provides spacing. Document that inter-hex spacing is controlled by the inner hex scale factor.

## Code Quality Issues

### C1: Indentation error in GameTypeContent

**File:** `react-ui/components/new-game/GameTypeSelector.tsx:142-178`

The "Coming Soon" banner and content div are indented as if they're siblings of the inner hex div, but they're actually children of it. The JSX structure is correct but the visual indentation is misleading:

```tsx
      <div style={{ transform: 'scale(0.91)' }}>
      {/* These are children but look like siblings due to indent */}
      {isDisabled && (
        <div>Coming Soon banner</div>
      )}
      <div className="flex flex-col ...">
        {/* content */}
      </div>
      </div>
```

**Fix:** Indent the banner and content div one level deeper.

### C2: Redundant `'use client'` in HexTile.tsx

**File:** `react-ui/components/hex-grid/HexTile.tsx:1`

HexTile is only imported by HexGrid.tsx (which has `'use client'`). The directive is harmless but redundant. However, since HexTile is exported from the barrel (index.ts), keeping it is defensively correct for direct imports.

**Verdict:** Keep as-is. No action needed.

### C3: `handleTogglePlayer` recreated every render

**File:** `react-ui/components/new-game/PlayerSelector.tsx:181-189`

Previously wrapped in `useCallback`. Now a plain function. Since `HexGridItem.onClick` uses inline arrow functions anyway (`onClick: () => handleTogglePlayer(player.id)`), wrapping in useCallback wouldn't help - the items array is rebuilt every render regardless.

**Verdict:** Current approach is fine. No unnecessary optimization needed.

### C4: PlayerSelector limited to 6 players

**File:** `react-ui/components/new-game/PlayerSelector.tsx:237`

```typescript
const visiblePlayers = sortedPlayers.slice(0, 6);
```

If there are more than 6 non-guest players in the database, only the first 6 (alphabetically) are shown. There's no scroll, pagination, or indication that more exist.

**Impact:** Low for current use case (seems like there are ~5-6 players), but could be a problem if player count grows.

**Recommendation:** Add a comment documenting this limitation. Consider a future overflow strategy (second ring of hexes, or a scroll indicator).

### C5: Type assertion for disabled game types

**File:** `react-ui/components/new-game/GameTypeSelector.tsx:79,91`

```typescript
type: 'Unset' as GameType,      // Cities & Knights
type: 'SavedGame' as GameType,  // Seafarers
```

Using real GameType enum values for placeholder/disabled game types is fragile. If someone selects these (bypassing disabled checks), it would create a game with type 'Unset' or 'SavedGame'.

**Impact:** Low (disabled at HexGridItem level AND in onClick handler), but defense-in-depth is lacking.

### C6: Empty `<div />` as flexbox spacer

**File:** `react-ui/components/new-game/PlayerSelector.tsx:317`

```tsx
) : <div />}
```

When there's no guest player or no `onIncludeGuestChange` callback, an empty div is rendered as a flex spacer to keep the validation message right-aligned. This works but is semantically unclear.

## Architectural Observations

### Positive

1. **Clean separation**: HexGrid is pure layout math. Content components own all visual behavior (borders, hover, selection).
2. **Correct hex math**: The Red Blob Games formulas are correctly ported and match BoardGeometry.cs.
3. **Reusable**: The hex-grid components have no knowledge of game types or players - they work with any content.
4. **Type-safe**: Good TypeScript interfaces throughout.
5. **Consistent visual approach**: Both selectors use identical patterns (center label hex, surrounding content hexes, two-hex border).

### Concerns

1. **Fixed pixel dimensions**: The HexGrid container uses fixed pixel widths/heights. On small viewports, this could overflow its parent card container. The `scale` prop helps but doesn't resize the container box itself (it's a CSS transform).

2. **No keyboard accessibility**: Hex tiles are clickable divs, not buttons. They lack `role="button"`, `tabIndex`, and `onKeyDown` handlers. Screen readers won't announce them as interactive elements.

3. **No ARIA labels**: Selected state is only communicated visually (checkmark/green border). `aria-selected` or `aria-pressed` attributes are missing.

## Recommendations

### Must Fix

None - the implementation is functional and visually correct.

### Should Fix (before merge to main)

1. **Update design doc** to reflect the 7 intentional divergences listed above
2. **Fix CLUSTER_30 count** or rename to CLUSTER_29
3. **Remove or document `gap` parameter** to avoid confusion

### Nice to Have

1. Fix GameTypeContent indentation
2. Add keyboard/ARIA accessibility to hex tiles
3. Document the 6-player limit in PlayerSelector
4. Consider `role="button"` + `tabIndex={0}` in HexTile for accessibility

## Files Reviewed

| File | Lines | Verdict |
|------|-------|---------|
| `react-ui/components/hex-grid/hex-geometry.ts` | 159 | Good - correct math, clean interfaces |
| `react-ui/components/hex-grid/HexTile.tsx` | 63 | Good - minimal, focused |
| `react-ui/components/hex-grid/HexGrid.tsx` | 133 | Good - clean layout engine |
| `react-ui/components/hex-grid/index.ts` | 26 | Good - proper barrel exports |
| `react-ui/components/new-game/GameTypeSelector.tsx` | 330 | Good - minor indent issue |
| `react-ui/components/new-game/PlayerSelector.tsx` | 327 | Good - clean refactor |
| `react-ui/app/new-game/page.tsx` | 258 | Good - proper separation |
| `react-ui/app/globals.css` | 651 | Good - hex-clip utilities correct |

## Conclusion

The implementation is solid and closely follows the design document. The intentional divergences are all user-directed improvements. The main issues are the misleading `gap` parameter, the incorrect CLUSTER_30 count, and the design document being stale relative to implementation decisions. No blocking issues for the current feature scope.

# BoardContainer.razor.css Review

Analysis of each CSS property for necessity.

## `.board-svg-container`

| Property | Value | Status | Reason |
|----------|-------|--------|--------|
| `display` | `grid` | **KEPT** | Required for grid-based layer stacking |
| `grid-template-columns` | `1fr` | **REMOVED** | User confirmed: default for single-column grid |
| `grid-template-rows` | `1fr` | **REMOVED** | User confirmed: default for single-row grid |
| `width` | `100%` | **REMOVED** | User confirmed: block elements fill width by default |
| `height` | `100%` | **KEPT** | Required - user confirmed removing breaks layout |
| `isolation` | `isolate` | **REMOVED** | User confirmed: not needed |
| `position` | `relative` | **KEPT** | Required for absolute positioned `.building-overlay` |

## `.board-static-layer, .board-static-canvas, .board-interactive-layer` (shared)

| Property | Value | Status | Reason |
|----------|-------|--------|--------|
| `grid-column` | `1` | **KEPT** | Required to stack layers in same cell |
| `grid-row` | `1` | **KEPT** | Required to stack layers in same cell |
| `width` | `100%` | **CHECK** | Grid items stretch by default - toggle off to verify |
| `height` | `100%` | **CHECK** | Grid items stretch by default - toggle off to verify |

## `::deep .building-overlay`

| Property | Value | Status | Reason |
|----------|-------|--------|--------|
| `grid-column` | `1` | **KEPT** | Required for grid positioning |
| `grid-row` | `1` | **KEPT** | Required for grid positioning |
| `position` | `absolute` | **KEPT** | Required for overlay positioning |
| `top` | `0` | **KEPT** | Required for positioning |
| `left` | `0` | **KEPT** | Required for positioning |
| `width` | `100%` | **KEPT** | Required to cover full area |
| `height` | `100%` | **KEPT** | Required to cover full area |
| `z-index` | `3` | **KEPT** | Consolidated from second occurrence |

## `.board-static-layer`

| Property | Value | Status | Reason |
|----------|-------|--------|--------|
| `z-index` | `1` | **KEPT** | Controls layer stacking order |
| `pointer-events` | `none` | **KEPT** | Allows clicks to pass through |

## `.board-static-canvas`

| Property | Value | Status | Reason |
|----------|-------|--------|--------|
| `z-index` | `1` | **KEPT** | Same layer as static SVG |
| `pointer-events` | `none` | **KEPT** | Allows clicks to pass through |
| `justify-self` | `center` | **REMOVED** | Only needed with offscreen rendering (disabled) |
| `align-self` | `center` | **REMOVED** | Only needed with offscreen rendering (disabled) |

## `.board-interactive-layer`

| Property | Value | Status | Reason |
|----------|-------|--------|--------|
| `z-index` | `2` | **KEPT** | Must be above static layer |
| `will-change` | `contents` | **REMOVED** | Premature optimization hint |
| `pointer-events` | `none` | **KEPT** | Disabled on container, enabled on children |
| `isolation` | `isolate` | **REMOVED** | Not needed |
| `justify-self` | `center` | **REMOVED** | Only needed with offscreen rendering (disabled) |
| `align-self` | `center` | **REMOVED** | Only needed with offscreen rendering (disabled) |

## `.board-interactive-layer :deep(...)` pointer events

| Property | Value | Status | Reason |
|----------|-------|--------|--------|
| `pointer-events` | `auto` | **KEPT** | Required for road/building interactions |

## `.board-loading-overlay`

| Property | Value | Status | Reason |
|----------|-------|--------|--------|
| All properties | - | **KEPT** | Loading state styling - all necessary |

## `.loading-spinner` and `@keyframes spin`

| Property | Value | Status | Reason |
|----------|-------|--------|--------|
| All properties | - | **KEPT** | Spinner animation - all necessary |

---

## Summary of Changes Made

1. **REMOVED** from `.board-svg-container`: `grid-template-columns`, `grid-template-rows`, `width`, `isolation`
2. **REMOVED** from `.board-static-canvas`: `justify-self`, `align-self`
3. **REMOVED** from `.board-interactive-layer`: `will-change`, `isolation`, `justify-self`, `align-self`
4. **CONSOLIDATED** duplicate `::deep .building-overlay` blocks into one

## Items Still Requiring Manual Check

Toggle off in browser DevTools to verify these don't affect layout:

1. `width: 100%` / `height: 100%` on shared layer rule (lines 22-23) - grid items may stretch by default

# Portrait Mode

**Last verified:** January 30, 2026

## Overview

Portrait mode provides a mobile-friendly layout for devices held
in portrait orientation (aspect ratio < 4:3). The layout switches
from the multi-panel landscape view to a tabbed interface with
Board, Controls, and Players tabs.

The outer app shell (nav column + header bar + content area) is
identical in both orientations. Portrait mode only changes what
renders inside the content area. See [app-shell.md](app-shell.md).

## Implementation Status

| Component | Blazor | React |
|-----------|--------|-------|
| Orientation detection | Implemented (JS callback) | **Infrastructure only** |
| Tabbed interface | Implemented (3 tabs) | **Not implemented** |
| Panel layout presets | Implemented | Store methods exist |
| Tab persistence | Implemented (sessionStorage) | Store property exists |
| CSS variants | Implemented | Defined but unused |
| Viewport scaler | Implemented (viewportScaler.js) | **Not implemented** |

## Detection

### Blazor

`viewportScaler.js` detects orientation changes and calls back to
Blazor via `OnOrientationChanged(bool isPortrait)`. Trigger
threshold: aspect ratio < 1.333 (4:3).

### React

`uiStore.ts` has the infrastructure:

```typescript
interface UIState {
    isPortrait: boolean;
    isMobile: boolean;         // detects coarse pointer (touch)
    activePortraitTab: 'board' | 'controls' | 'players';
}
```

`layoutStore.ts` provides:

- `isPortraitViewport()` function
- `PORTRAIT_PANELS` preset with vertical stacking
- `resetToPortrait()` and `resetToLandscape()` actions

**Gap:** `setOrientation()` is never called. No component wires up
the resize/orientation listener.

## Layout

### Landscape (Default)

Three-column layout with floating panels:

```
+--Left--+-----Center------+--Right--+
| Dice   |                  | Players |
| Actions|     Game Board   |         |
| Measure|                  |         |
+---------+------------------+---------+
```

### Portrait (Tabbed)

Single column with 60px tab bar:

```
+--Tab Bar: [Board] [Controls] [Players]--+
|                                           |
|          (Selected tab content)           |
|                                           |
+-------------------------------------------+
```

Only one tab's content is visible at a time. Board tab shows the
game board at full width. Controls tab shows dice, actions, and
measurement panels stacked vertically. Players tab shows the
players panel.

## CSS Support

### Blazor

Uses `data-layout-mode` attribute:

```css
.game-container[data-layout-mode="portrait"] { ... }
.game-container[data-layout-mode="portrait"][data-game-active="true"] { ... }
```

Portrait-specific purchase grid maintains 3x2 layout:

```css
.portrait-purchase .purchase-grid {
    grid-template-columns: repeat(3, 1fr);
}
```

### React

CSS custom variants defined in `globals.css`:

```css
@custom-variant portrait { ... }
@custom-variant landscape { ... }
```

CSS variables:

- `--portrait-width: 1080px`
- `--portrait-height: 1920px`
- `--portrait-tab-height: 60px`

Mobile touch targets via `@media (pointer: coarse)`.

## Tab Persistence

### Blazor

Selected tab stored in `sessionStorage` as `"portraitTab"`. Restored
on page init. Default tab: `"board"`.

### React

`uiStore.activePortraitTab` property exists but is not wired to
any persistence mechanism.

## Base Dimensions

Both orientations use the same pixel dimensions for game elements.
Scaling is uniform (not per-element):

| Mode | Base Resolution |
|------|-----------------|
| Landscape | 1920 x 1080 |
| Portrait | 1080 x 1920 |

Scale factor: `min(viewport / base, 1.0)` -- never scales above 1x.

## What Changes in Portrait

- Player tiles align center instead of right
- Roll grid changes from 3 to 4 columns
- Tab visibility replaces panel visibility
- Floating panels collapse to tab content areas

## What Stays the Same

- All pixel dimensions within components
- Colors and styling
- Game logic and state handling
- Font sizes relative to their containers

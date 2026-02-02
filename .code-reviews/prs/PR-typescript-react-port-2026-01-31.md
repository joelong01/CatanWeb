# PR Code Review: typescript-react-port (Arrange Layout)

**Branch:** typescript-react-port
**Base:** main
**Reviewed:** 2026-01-31
**Reviewer:** Claude Opus 4.5
**Scope:** Commits bf91718..55174f4 (2 commits, 10 files, +2399/-98 lines)

## Summary

Adds a game-state-aware "Arrange" layout action that computes optimal panel
positions based on browser dimensions, orientation, and current game phase.
Also fixes viewport save/load (pan/zoom persistence) and adds ContextMenu
and SaveLayoutDialog components.

## Commits

| Hash | Message |
|------|---------|
| bf91718 | feat(react-ui): Add game-state-aware Arrange layout and viewport save/load |
| 55174f4 | docs: Add layout management design doc and session summary |

## Files Changed

| File | Changes | Risk |
|------|---------|------|
| react-ui/lib/stores/layoutStore.ts | +466 lines: arrange algorithm, viewport save/load, phase classification | Medium |
| react-ui/lib/stores/\_\_tests\_\_/layoutStore.test.ts | +690 lines: 28 new tests | Low |
| react-ui/components/layout/NavMenu.tsx | +221 lines: Arrange menu item, Save As dialog, expanded layout section | Low |
| react-ui/components/game/panels/MinimizedBar.tsx | +108 lines: Arrange context menu, gameState hook | Low |
| react-ui/components/game/panels/ContextMenu.tsx | +103 lines: New reusable context menu | Low |
| react-ui/components/game/panels/SaveLayoutDialog.tsx | +119 lines: New save dialog | Low |
| react-ui/components/game/panels/index.ts | +2 lines: barrel exports | Low |
| .design/layout-management.md | +378 lines: New design doc | Low |
| .design/floating-panel.md | +95 lines: Updated design doc | Low |
| .ai/sessions/SESSION\_SUMMARY-2026-01-31-1715.md | +217 lines: Session summary | Low |

## Critical Issues

None.

## Important Issues

### 1. MinimizedBar: Missing timer cleanup on unmount

**Location:** `react-ui/components/game/panels/MinimizedBar.tsx:103-130`
**Severity:** Important

The long-press timer (`longPressTimerRef`) is cleared on touchEnd/touchMove,
but not on component unmount. If the component unmounts during a pending
long-press, the setTimeout callback fires after unmount, calling
`setContextMenu` on an unmounted component.

**Recommendation:** Add a cleanup effect:

```tsx
useEffect(() => {
  return () => {
    if (longPressTimerRef.current) {
      clearTimeout(longPressTimerRef.current);
    }
  };
}, []);
```

**Impact:** Low in practice (400ms timer, component rarely unmounts during
long-press), but is a correctness issue.

### 2. Small viewport edge case in computeArrangedLayout

**Location:** `react-ui/lib/stores/layoutStore.ts:472-566`
**Severity:** Important

No minimum size guards for very small viewports (< 400px). Panel widths
computed as viewport percentages (e.g., `fullW * 0.22`) could produce
panels < 50px wide on mobile browsers, making content unreadable.

**Recommendation:** Add minimum width/height clamps or consider falling
back to a mobile-specific layout (all panels minimized except board).

**Impact:** Medium -- affects mobile browser use. Not blocking for desktop.

## Suggestions

### 1. Accessibility: Missing ARIA attributes on ContextMenu and SaveLayoutDialog

Both new components lack semantic ARIA roles:

- **ContextMenu:** Missing `role="menu"`, `role="menuitem"`, keyboard navigation
- **SaveLayoutDialog:** Missing `role="dialog"`, `aria-modal="true"`,
  `aria-labelledby`, focus trap

These are standard accessibility requirements per WCAG 2.1. Not blocking
for this PR but should be addressed in a follow-up.

### 2. ContextMenu hardcoded colors

**Location:** `react-ui/components/game/panels/ContextMenu.tsx:78-80`

Uses inline `backgroundColor: '#2a2a2a'` instead of Tailwind classes or
CSS custom properties. Minor consistency issue with the project's theming
approach.

### 3. Test constants for layout margins

**Location:** `react-ui/lib/stores/__tests__/layoutStore.test.ts:459-460`

Tests use magic numbers `8` and `56` matching implementation constants
`M` and `TOP`. Consider extracting and exporting these as named constants
from the store for test readability.

## Security Review

No security concerns identified. Changes are purely client-side layout
logic with no external data handling, network requests, or user input
processing (SaveLayoutDialog input is used only as a localStorage key name).

## Testing Verification

- **Build:** Clean (0 errors, 0 warnings)
- **Tests:** 534/534 passing (15 test files)
- **New tests:** 28 tests covering classifyGameState (17), computeArrangedLayout
  (10), arrangeLayout action (4), viewport save/load (3)
- **Coverage gaps:** No tests for viewports < 400px, no integration test
  for arrange+save+load round-trip

## Approval Status

- [x] No critical issues
- [x] Build passes
- [x] Tests pass (534/534)
- [x] Ready for PR

# Code Review: board-interaction.md

**File:** `/Users/joelong/GitHub/CatanWeb/.design/board-interaction.md`
**Reviewed:** 2026-02-10
**Reviewer:** GitHub Copilot

## Summary

Design doc for a unified board interaction model, including hit-testing, dispatch, pan/zoom, and touch behavior.
Review focused on internal consistency, scope clarity, and behavioral edge cases.

## Critical Issues

None.

## Important Issues

### 1. Scope contradicts component changes

**Location:** `.design/board-interaction.md:211-223`

The doc lists removal of DOM-level click handlers from GameTile/Building/Road as in scope, but also says changes to
those components are out of scope. Removing click handlers implies edits in those components or their props wiring.
This conflict will block implementation decisions and could cause scope creep or rework.

**Recommendation:** Clarify scope. Either allow the minimal component changes needed to remove handlers, or remove
that in-scope item and document how the unified handler coexists with existing handlers.

### 2. Sea tile right-click behavior is inconsistent

**Location:** `.design/board-interaction.md:82-89`

Step 3 says Sea tiles are pan-only with no game action, while step 4 says right-click always dispatches to
`onTileRightClick`. These two rules are incompatible unless Sea is an explicit exception to right-click dispatch.

**Recommendation:** Specify the precedence: either Sea suppresses right-click dispatch, or right-click on Sea tiles
is allowed and handled by the game page. Explicitly define the rule order.

### 3. Touch drag rules conflict with pan eligibility

**Location:** `.design/board-interaction.md:107-135`

The mouse panning rules restrict drag on resource tiles without modifiers, but touch behavior says drag on any
surface pans. This is a functional divergence that can change click behavior on touch devices.

**Recommendation:** State whether touch overrides the surface eligibility rules or follows the same eligibility
with an explicit exception. If touch always pans, document how taps still trigger clicks on resource tiles.

## Suggestions

### 1. Define hit-test tolerances for roads and buildings

**Location:** `.design/board-interaction.md:42-75`

The hit-test API lists positions but does not specify a hit radius or tolerance for matching roads/buildings.
Without a defined tolerance, implementations may diverge or produce inconsistent targeting between zoom levels.

**Recommendation:** Add explicit hit-test thresholds (e.g., pixels or scale-adjusted values) for road and building
targets, and note whether they scale with zoom.

## Questions

### 1. Long-press cancelation and click suppression

**Location:** `.design/board-interaction.md:130-136`

How should long-press interact with drag threshold and pointer up? The doc says long-press triggers right-click,
but does not state whether the subsequent pointer up should suppress the left-click dispatch, or whether movement
before the timeout cancels long-press.

## Desktop App Comparison

Not applicable. The document does not reference a Desktop implementation or behavior to compare against.

## Follow-Up Actions

- [ ] Resolve the in-scope vs out-of-scope contradiction for component changes.
- [ ] Clarify the precedence between Sea tile pan-only behavior and right-click dispatch.
- [ ] Specify touch drag eligibility rules and how taps map to clicks on resource tiles.
- [ ] Define hit-test tolerances for roads/buildings (including zoom behavior).
- [ ] Document long-press cancelation and click suppression behavior.

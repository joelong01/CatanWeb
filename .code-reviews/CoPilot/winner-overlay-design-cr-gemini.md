# Code Review: Winner Overlay Design

**File:** `.design/ui/react/winner-overlay-design.md`
**Reviewed:** 2026-01-30
**Reviewer:** Gemini

## Summary

This design document proposes a unified `WinnerOverlay` component to replace the fragmented `WinnerDialog`, `WinnerCelebration`, and `VictoryPointsOverlay`. The architecture uses a three-phase state machine ('ready', 'celebrating', 'scoring') within a single component, leveraging the existing `HexGrid` for layout consistency. The approach is sound and significantly simplifies the winner flow. However, there are some open questions regarding the "Scoring" UX and how the overlay behaves for non-winning players (observers or losers).

## Critical Issues

### 1. Calculation of Winner State

**Location:** `Props Interface`
**Severity:** Critical

The `WinnerOverlayProps` interface includes `players` and `currentPlayerColors`, but does not explicitly identify *who* the winner is. Use of `currentPlayerColors` for the center hex implies the local user is always the winner, or the overlay is only shown to the winner.

If this component is intended to be shown to *all* players when a game ends (triggered by a server broadcast), it needs to know who the winner is to display the correct name/avatar in the "Winner!" context. If it is only for the claimant, this should be explicitly stated, as it impacts how other clients perceive the game end.

**Recommendation:**
Add a `winnerId` prop to `WinnerOverlayProps`.

- If `winnerId` matches the local user, show "You Won!" or similar.
- If `winnerId` differs, the center hex should probably display the winner's info, not the local user's colors, or at least distinguish between "Victory" and "Defeat".

### 2. Scoring "Up-Only" Limitation

**Location:** `Phase 3: Scoring` -> `Ring hexes`
**Severity:** Critical

The design states "Scores only go UP, never down" with a single "+" button. While logically VP totals shouldn't decrease (you don't lose VPs usually), accidental clicks are a common user error. If a user accidentally clicks "+" twice, they are stuck with an incorrect score that they will then broadcast to the server. This is a severe usability trap.

**Recommendation:**
Include a "-" button or an "Undo" mechanism. Even if the game logic implies scores only grow, the UI must account for input errors before the final payload is sent.

## Important Issues

### 1. Trusted Scoring Model

**Location:** `Backend (future)` / `Phase 3: Scoring`
**Severity:** Important

The design implies the client (the "Claimant") sends a `Record<string, number>` adjusting *all* players' scores via `onEndGame`.

1. Does the claimant know other players' hidden VPs?
2. Why is the claimant adjusting other players' scores?
3. If this payload is authoritative, a malicious or mistaken client could corrupt the game state for everyone.

**Recommendation:**
Clarify if each player submits their *own* score, or if the winner submits everyone's. If the winner submits everyone's, the UI needs to make it clear *why* they are editing others' scores. Ideally, the server should validate these scores against known state (e.g. strict limits on hidden VPs).

### 2. Accessibility & Keyboard Navigation

**Location:** `Missing considerations`
**Severity:** Important

The design does not mention accessibility. `HexGrid` items needs to be keyboard navigable.

- Can users TAB to the center "Winner" button?
- Can users TAB to the "+" buttons in the scoring phase?
- Are there `aria-label` attributes for the icon-only buttons?

**Recommendation:**
Explicitly mandate `tabIndex` and `onKeyDown` handlers for accessibility compliance, especially since this flow blocks the game.

### 3. Reduced Motion Support

**Location:** `CSS Animations`
**Severity:** Important

The spinning animation and confetti should respect the user's `prefers-reduced-motion` system setting. A 5-second continuous rotation can be triggering for users with vestibular disorders.

**Recommendation:**
Wrap the animation CSS in a media query:

```css
@media (prefers-reduced-motion: no-preference) {
  animation: winner-spin <duration>ms linear infinite;
}
```

Or allow disabling via a prop.

## Suggestions

### 1. Confetti Toggle

**Location:** `Props Interface`
**Severity:** Suggestion

Consider adding a `showConfetti?: boolean` prop or user setting. While fun, some users may find it distracting or performance-intensive on low-end devices.

### 2. Animation Clipping Check

**Location:** `Phase 2: Celebrating`
**Severity:** Suggestion

Verify that rotating the wrapper `div` inside a rectangular `FloatingPanel` doesn't cause visual clipping of the hexes at 45-degree angles. Since `fitToParent` scales the grid, it usually constrains to the smallest dimension, but it's worth verifying that the *diagonal* length of the rotating square doesn't exceed the container's bounds if the container is tight.

## Questions

### 1. Non-Winner Experience

**Location:** `General`

What do the other players see while the winner is celebrating? Do they get a notification? Do they see a read-only version of this overlay? The document focuses heavily on the interactive "Claimant" flow.

### 2. Mobile/Touch Targets

**Location:** `HexGrid`

Are the hex buttons (radius 50) large enough for reliable touch targets on mobile? 50px radius is ~100px width, which is plenty, but the "+" button in Scoring phase might be smaller.

## Praise

### 1. Unified Architecture

**Location:** `Component Architecture`

Replacing three fragmented components with a single internal-state-machine component is an excellent architectural decision. It reduces external state management complexity significantly.

### 2. Layout Consistency

**Location:** `Layout Pattern`

Reusing the `HexGrid` + `CLUSTER_7` layout ensures the winner screen looks and feels like part of the board, fixing the "alien modal" feel of the previous dialog.

### 3. Performance

**Location:** `Design Decisions`

Using CSS animations (`transform: rotate`, `keyframe`) instead of JS-based animation libraries is the right choice for this predictable, continuous motion.

## Follow-Up Actions

- [ ] Add `winnerId` to `WinnerOverlayProps`.
- [ ] Add "-" button (decrement) to `PlayerScoringHex` to handle user error.
- [ ] Add `prefers-reduced-motion` check to styling.
- [ ] Document the expected flow for *non-winning* players (losers/observers).
- [ ] Verify keyboard tab order through the ring of hexes.

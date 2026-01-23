# Architecture Review: React Implementation Plan

**Date:** 2026-01-23
**Reviewer:** Gemini
**Scope:** Store architecture, component patterns, config strategy

## Executive Summary

The proposed architectural changes represent a significant maturity leap for the project. The move to a **multi-game store**,
**infinite ocean rendering**, and **configuration-as-code** aligns well with modern scalable React applications. However, the core premise of
"fine-grained reactivity" is at significant risk due to the nature of immutable data updates via SignalR. Without specific mitigation strategies
(structural sharing or deep comparison), replacing the entire `GameModel` on every update will trigger widespread re-renders, negating the benefits of
selector-based subscriptions.

## Critical Issues

### 1. Reactivity & Reference Stability (The "Everything Re-renders" Risk)

The plan relies on `useGameStore(state => state.games[gameId]?.tiles)` to avoid re-renders.

* **The Problem:** SignalR usually deserializes a fresh JSON object for every update. Even if tiles haven't changed logically,
  the *new* `GameModel` will contain a *new* `tiles` array reference.
* **Consequence:** `state => state.games[gameId]?.tiles` will return a new reference every time `updateGame` is called.
  Zustand's default shallow equality check will fail, causing the component to re-render *every single time* any part of the game state updates.
* **Risk:** Performance degradation on mobile devices during high-frequency updates (e.g., timers, drag events if synced).

### 2. Viewport Key Collisions

Using `viewportId="thumbnail"` for preview components is dangerous.

* **The Problem:** If the "Load Game" page displays a list of 10 saved games, and all use `viewportId="thumbnail"`,
  they will all bind to the same entry in `ViewportStore`. Panning one will pan all of them simultaneously.
* **Risk:** Broken UI in lists/grids of game previews.

## Important Issues

### 3. Store Cleanup Race Conditions

The `useEffect` cleanup pattern (`return () => clearGame(gameId)`) works for simple pages, but:

* **Scenario:** User navigates Game A -> Hub -> Game A quickly.
* **Race:** The `clearGame` from the first visit might fire *after* the `loadGame` of the second visit if batched or async,
  clearing the state the user just loaded.
* **Mitigation:** Ensure `clearGame` checks if the "unmounting" consumer is the current owner, or use reference counting if multiple components
  share state.

### 4. Player Profile Sync Gaps

* **Scenario:** A historical game contains a `playerId` that no longer exists in `PlayerProfilesStore` (deleted profile).
* **Result:** `usePlayerProfilesStore` returns `undefined`.
* **Impact:** `PlayerTile` component returns `null` (per code snippet), causing the player to vanish from the UI entirely.
  Need a fallback visual ("Unknown Player").

## Suggestions

### 1. Optimize SignalR Updates

Instead of `store.games[gameId] = newModel`, consider a diffing strategy or `useShallow` in selectors.

* **Alternative:** Use `useShallow` from `zustand/react/shallow` in components:

    ```typescript
    const tiles = useGameStore(useShallow(state => state.games[gameId]?.tiles ?? []));
    ```

    This forces a deep(er) comparison of the array contents rather than just reference equality.

### 2. Standardize Viewport IDs

Enforce a pattern for viewport IDs to avoid collisions:

* Main Game: `game-${gameId}`
* Thumbnail: `thumb-${gameId}`
This ensures every visual instance has its own pan/zoom state.

### 3. Storybook Decorators

Since components are now coupled to global stores, create a `.storybook/preview.tsx` decorator that wraps stories and allows
seeding the Zustand store with mock data. This restores testability/isolation.

## Questions

1. **Partial vs. Full Updates:** Does the backend send the full `GameModel` on every `GameStateUpdated` event, or just a patch?
   (Assumed full based on "updateGame(model)").
2. **Config Runtime Injection:** The plan removes `lib/services/config.ts` which handled `window.__CATAN_SERVICE_URL__`.
   Does `config/index.ts` retain this runtime injection capability? It is critical for "Build Once, Deploy Anywhere".

## Praise

* **PlayerProfiles Separation:** Decoupling identity from game state is an excellent architectural move.
  It simplifies the game model and enables consistent "Who is who" across the entire app.
* **Tailwind Orientation Variants:** This CSS-first approach for responsive layout is robust and far superior to JS-based listeners.
* **Architecture Layers:** The design of the z-index stack (Ocean -> Board -> UI) is clean and extensible.

## Detailed Analysis

### 1. Store Architecture Soundness

* **Multi-Game Pattern:** `Record<string, GameModel>` is the correct approach for a multi-tenant capability.
  It allows for "spectating" or "previewing" while playing.
* **Memory Management:** Relying on `useEffect` cleanup is standard for React. Given the SPA nature, extensive navigation might
  leave "zombie" games if cleanup fails, but the memory footprint of a text-based Catan game model is negligible (kb, not mb).

### 2. Selector-Based Reactivity

* **Efficiency:** As noted in "Critical Issues", reference stability is the main blocker.
* **Dual-Store:** The pattern `const player = find(...)` + `const profile = profiles[id]` is clean and understandable.
  The O(N) search on a 4-item array is performance-neutral.

### 3. SignalR Integration

* **Flow:** The flow (Event -> Proxy -> Hook -> Store) is clean.
* **Optimistic Updates:** The plan relies on server autority. This is safer for consistency but feels laggy on high-latency networks.
  A future phase might consider local optimistic application of moves (e.g., placing a road immediately).

### 4. Config as Code

* **Feasibility:** "No Env Vars" works IF the config file includes logic to read `window` or `global` at runtime.
  If it's purely static string literals, you cannot promote a build from Staging to Production without rebuilding.

### 5. Color Propagation

* **Performance:** Updating a single color string in `PlayerProfilesStore` will trigger all subscribers.
  React 18's concurrent rendering handles "many small updates" (like changing 100 SVG fill colors) efficiently.
  This should be visually instant and satisfy the requirement.

### 6. Component Pattern

* **Trade-off:** Coupling components to store IDs (`gameId`) lowers boilerplate (prop drilling) but increases coupling.
* **Verdict:** For a complex app like Catan, avoiding prop-drilling through 10 layers of Board/Layer/Hex/Token is worth the coupling cost.

## Recommendations

1. **Mandatory:** Adopt `useShallow` or a structural sharing library (like Immer) for store updates to solve the Reference Stability
   issue. Without this, the "fine-grained" architecture fails.
2. **Mandatory:** Change the "thumbnail" viewport ID strategy to `thumb-${gameId}` to prevent collision in lists.
3. **Mandatory:** Ensure `config/index.ts` implements runtime config detection (`window.__CATAN_...`) for containerization support.

## Confidence Assessment

* **Architecture Soundness:** High (Logic is sound)
* **Performance Feasibility:** Medium (Risk of over-rendering without reference stability)
* **Implementation Clarity:** High (Plan is detailed)
* **Overall Confidence:** High (with fixes)

## Next Steps

* [ ] **Update Plan:** Add specific strategy for Reference Stability (e.g., `useShallow`).
* [ ] **Refine Config:** Explicitly document the runtime injection implementation in `config/index.ts`.
* [ ] **Phase 0.4:** Implement the `Multi-Game` and `PlayerProfiles` store refactor immediately; it is a blocker for the New Game page.

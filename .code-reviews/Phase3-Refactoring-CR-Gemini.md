# Code Review: Phase 3 (Work Units 3.1 & 3.2) Refactoring

**Files Reviewed:**

- `react-ui/components/game/tiles/Road.tsx`
- `react-ui/components/game/tiles/Building.tsx`
- `react-ui/components/game/board/GameBoard.tsx`
- `react-ui/lib/stores/gameStoreHooks.ts`

**Reviewed:** 2026-01-28
**Reviewer:** GitHub Copilot (Gemini)

## Summary

This review covers the refactoring of `Road` and `Building` components to use `ownerId` and `currentPlayerId` props instead of passing color objects directly. This implements "Work Units 3.1 & 3.2" from the refactoring plan, shifting the responsibility of color lookup to the components themselves via the `usePlayerColors` hook. This aligns with the "Server-Driven UI" architectural goal by reducing `GameBoard.tsx`'s responsibility for data formatting.

## Critical Issues

*None found.*

## Important Issues

*None found.*

## Suggestions

### 1. Consistency in `GameBoard.tsx` Prop Passing

**Location:** `react-ui/components/game/board/GameBoard.tsx`

There is a minor inconsistency in how the current player ID is passed to the components:

- **Road:** `currentPlayerId={currentPlayer?.id}`
- **Building:** `currentPlayerId={selectedPlayerId}`

Assuming `currentPlayer` is derived from `selectedPlayerId`, these result in the same value. However, for clarity and consistency, it would be better to use `selectedPlayerId` for both, as it likely comes directly from the props or store, whereas `currentPlayer` requires an extra object lookup.

```tsx
// Current Road usage
<Road
  ...
  currentPlayerId={currentPlayer?.id}
/>

// Suggested
<Road
  ...
  currentPlayerId={selectedPlayerId}
/>
```

## Praise

### 1. Clean Separation of Concerns

Moving the color lookup into `Road.tsx` and `Building.tsx` significantly cleans up `GameBoard.tsx`. Removing the manual `owner` lookup (`const owner = ownerId ? ...`) inside the render loop is a great performance and readability improvement.

### 2. Proper Hook Abstraction

The creation and export of `usePlayerColors` in `gameStoreHooks.ts` is the correct way to share this logic. It encapsulates the store access and error handling (returning undefined if ID is missing) nicely.

### 3. Test Data Updates

Updating `expansion-game.ts` to support multiple player IDs helps ensure these rendering changes can be verified independently of the full game state.

## Follow-Up Actions

- [ ] Consider unifying the `currentPlayerId` prop usage in `GameBoard.tsx` (Suggestion #1).

## Conclusion

**Approved.** The changes are correct, follow the architectural vision, and simplify the parent component. You are clear to commit and proceed to the next Work Items.

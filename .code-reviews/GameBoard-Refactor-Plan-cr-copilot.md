# Code Review: React Component Props Audit Plan

**Subject:** `GameBoard` Refactoring Plan
**Reviewed:** 2026-01-28
**Reviewer:** GitHub Copilot

## Summary

The proposed refactor aims to convert `GameBoard` from a generic presentational component (receiving `BoardGameData` props) into a "connected" component that subscribes directly to the Zustand store. This aligns with modern React performance patterns to minimize re-renders by enforcing granular subscriptions.

## Critical Issues

*None identified in the plan.*

## Important Issues

### 1. Board Player Derivation Logic

**Current:** `page.tsx` derives `boardPlayers` by combining `usePlayers()` and `usePlayerProfiles()`.
**Refactor Risk:** If `GameBoard` calls `usePlayers()` and `usePlayerProfiles()` separately, it must duplicate the mapping logic to merge colors.
**Recommendation:** Create a dedicated hook `useBoardPlayers()` (or include in `useBoardData`) that encapsulates this transformation. This keeps `GameBoard` clean and centralized the logic for coloring players.

### 2. Hook Explosion in `GameBoard`

**Concern:** `GameBoard` will need to call ~7 individual hooks (`useTiles`, `useBuildings`, `useRoads`, `useHarbors`, `useRobber`, `usePlayers`, `usePlayerProfiles`, `useCurrentPlayerEntitlements`).
**Recommendation:** Implement the suggested **Composite Hook** pattern. Create `useBoardData()` in a new file (e.g., `lib/hooks/useBoardData.ts`).

```typescript
export function useBoardData() {
  const tiles = useTiles();
  const buildings = useBuildings();
  // ... other hooks
  const boardPlayers = useBoardPlayers(); // The derived logic mentioned above
  
  return {
    tiles,
    buildings,
    // ...
    players: boardPlayers
  };
}
```

## Suggestions

### 1. Handling Callbacks

**Question:** "Should callbacks (onBuildingClick, onRoadClick) still be passed as props?"
**Answer:** **Yes, keep them as props.**
Rationale:

- **Separation of Concerns:** `GameBoard` should focus on *rendering* the state and detecting interactions. `page.tsx` (or a specific controller/viewmodel) should decide *what to do* with those interactions (e.g., log metrics, check secondary conditions, call the API).
- **Testing:** It's easier to tests `GameBoard` interactions if you can pass a mock `onBuildingClick` jest function without mocking the entire network/proxy layer.

### 2. "Smart" vs "Presentational" Components

The refactor effectively makes `GameBoard` a "Smart" (Connected) component.

- **Pros:** Performance (granular updates), cleaner `page.tsx`.
- **Cons:** Tighter coupling to the specific App Store.
- **Verdict:** Given this is the specific `GameBoard` for this app (and not a generic library component), coupling is acceptable and beneficial.

### 3. Interface Reuse

The existing `BoardGameData` interface in `GameBoard.tsx` is well-structured. You should reuse this interface as the return type for your new `useBoardData()` hook to ensure type consistency during the migration.

## Follow-Up Actions

- [ ] Create `useBoardPlayers` hook to map `PlayerModel` + `PlayerProfile` -> `BoardPlayer`.
- [ ] Create `useBoardData` composite hook returning `BoardGameData`.
- [ ] Refactor `GameBoard.tsx` to remove `gameModel`, `players`, and `robber` props, replacing them with `useBoardData()`.
- [ ] Update `page.tsx` to remove the `boardGameData` derivation and prop passing.

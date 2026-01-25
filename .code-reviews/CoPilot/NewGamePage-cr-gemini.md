# Code Review: New Game Page & Store Architecture

**Reviewer:** GitHub Copilot (Gemini)
**Date:** 2026-01-23
**Scope:** `react-ui/app/new-game/`, `react-ui/components/new-game/`, `react-ui/lib/stores/gameStore.ts`
**Reference:** `.design/ui/react/ts-port-impl-plan.md` (Phases 0.4, 0.4.1, 2)

## 1. Store Architecture Critical Gap

**Severity: Critical**
`react-ui/lib/stores/gameStore.ts` does **not** match the agreed architectural design documented in Phase 0.4 and 0.4.1.

* **Design Requirement:** The store must support **Multiple Games** (`games: Record<string, GameModel>`) to enable spectating, previewing, and seamless switching.
* **Current Implementation:** It uses a singleton pattern `gameModel: GameModel | null`.
* **Impact:** This prevents implementing the "Lobby / Active Games" feature correctly and forces a full state wipe when switching games.
* **Action Required:** Refactor `gameStore.ts` to matching the `ts-port-impl-plan.md` Phase 0.4 spec (`games: { [id]: model }`, `activeGameId: string`).

## 2. Reactivity & Reconciliation Missing

**Severity: High**
The `gameStore.ts` currently implements simple reference replacement:

```typescript
setGameModel: (gameModel) => set({ gameModel })
```

This violates Phase 0.4.1 (Structural Sharing).

* **Issue:** Every SignalR update creates a new object tree. Without the `reconcileArray` / `reconcileObject` logic defined in the plan, **every component in the game will re-render on every tick** (timers, chat, small updates), leading to massive performance degradation on mobile.
* **Action Required:** Implement `lib/utils/reconciliation.ts` and integrate it into a `updateGame(id, model)` action in the store.

## 3. Player Profiles Store Separation

**Severity: Medium**
The implementation partially separates profiles (`playerProfiles: Map<string, PlayerProfile>`), which is good. However:

* It is currently inside `useGameStore`.
* **Design Requirement:** The plan suggests a dedicated `usePlayerProfilesStore` so profiles persist across games/lobbies.
* **Recommendation:** Move `playerProfiles` to its own store so it acts as a global cache (like a relational DB table) rather than per-game state.

## 4. UI/UX: New Game Page Implementation

Status: Excellent - The implementation of `app/new-game/page.tsx` and `PlayerSelector.tsx` is high quality.

* **Strengths:**
  * **Drag & Drop:** Implementation for sitting order is intuitive.
  * **Visual Validation:** The HexGrid usage for player selection is creative and visually consistent with the app's theme.
  * **Component Composition:** `GameTypeSelector` and `PlayerSelector` are clean, isolated components.
* **Minor Fixes:**
  * **Hex Math:** `PlayerSelector.tsx` uses a hardcoded `CLUSTER_7`. Ensure this handles the 5-6 player expansion correctly (it currently slices to 6, which is correct for Expansion, but verify visual crowding).
  * **Config Usage:** It uses `@/lib/services/config`. Ensure this imports the unified config object that resolves `window.__CATAN_SERVICE_URL__` correctly at runtime.

## 5. Directory Structure & Naming

Status: Compliant

* Files are correctly placed in `app/new-game` and `components/new-game`.
* Naming conventions (PascalCase components, camelCase functions) are followed.

## 6. Implementation Plan Updates

The `ts-port-impl-plan.md` needs to be updated to reflect that **Phase 2 (New Game)** is largely implemented code-wise, but is blocked by the **Phase 0.4 (Store Architecture)** underlying refactor.

## Summary of Required Actions

1. **Refactor Store:** Rewrite `gameStore.ts` to use `Record<string, GameModel>` and implement `reconcileGameModel`.
2. **Move Profiles:** Extract `playerProfiles` to `playerProfilesStore.ts`.
3. **Verify Config:** Audit `lib/services/config.ts` for runtime injection support.

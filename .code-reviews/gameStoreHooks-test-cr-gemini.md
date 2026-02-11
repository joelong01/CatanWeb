# Code Review: gameStoreHooks.test.ts

**File:** `react-ui/lib/stores/__tests__/gameStoreHooks.test.ts`
**Reviewed:** 2026-01-28
**Reviewer:** Gemini

## Summary

This test suite provides comprehensive coverage for the `gameStoreHooks`. It validates that derived state is calculated correctly and that selectors behave as expected when the store updates.

## Critical Issues

### 1. Missing Dependencies (Hooks)

**Location:** `gameStoreHooks.test.ts:10`
**Severity:** Critical

This test file imports from `../gameStoreHooks`, which (as noted in the other review) relies on missing `../extensions`. These tests cannot run until the dependencies are resolved.

## Important Issues

### 1. Test Data Mocking

**Location:** `gameStoreHooks.test.ts:38`
**Severity:** Important

The `createMockPlayer` function is manually implementing the `PlayerModel` interface. As the server model evolves (e.g., adding `Stars` or `BuildIndex` in Phase 2), this manual mock will fall out of sync and cause TypeErrors in CI.

**Recommendation:**
Use a central mock factory (e.g., in `react-ui/lib/test-data/factories.ts`) or use `Partial<PlayerModel>` casting more aggressively to avoid listing every single property when only testing a specific hook.

## Praise

### 1. Isolation of Tests

**Location:** `gameStoreHooks.test.ts:100`

The `beforeEach` block calling `clearGameState` results in clean isolation between tests. This prevents "leaky state" which is a common source of flaky tests in global store testing.

### 2. Comprehensive Derived State Testing

**Location:** `gameStoreHooks.test.ts:385`

The derived value hooks (`useMyPlayer`, `useCurrentPlayer`) are tested with various scenarios (undefined model, ID set/unset), covering the "optional chaining" logic thoroughly.

## Follow-Up Actions

- [ ] Create a shared mock factory for `GameModel` and `PlayerModel` to reduce boilerplate in test files.

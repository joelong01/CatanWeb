# Code Review: gameStoreHooks.ts

**File:** `react-ui/lib/stores/gameStoreHooks.ts`
**Reviewed:** 2026-01-28
**Reviewer:** Gemini

## Summary

This file implements a robust set of custom hooks for accessing the `gameStore`. It leverages `zustand/traditional`'s `useStoreWithEqualityFn` to provide optimized selectors that prevent unnecessary re-renders—a critical performance optimization for Catan's complex state updates.

## Critical Issues

### 1. Missing Dependencies

**Location:** `gameStoreHooks.ts:31`
**Severity:** Critical

The file imports from `'../extensions'`, but this directory and its modules (like `currentPlayer`, `isAllocationPhase`) do not exist yet in the codebase. This code will fail to compile until Phase 1 of the refactoring plan is complete.

**Recommendation:**
Ensure Phase 1 (Extensions Port) is completed *before* this file is integrated, or stub the extensions temporarily.

## Important Issues

### 1. Zustand Shallow Import Compatibility

**Location:** `gameStoreHooks.ts:11`
**Severity:** Important

The import `import { shallow } from 'zustand/shallow'` is used with `zustand` v5. In some v5 configurations, the export paths have changed or behavior differs.

**Recommendation:**
Verify that `zustand/shallow` correctly exports the `shallow` comparison function in the installed version (`^5.0.10`). If compliant with v5, this is fine; otherwise, check if it should be `import { shallow } from 'zustand/vanilla/shallow'` or similar.

## Suggestions

### 1. Documentation for `arraysEqual`

**Location:** `gameStoreHooks.ts:46`

The `arraysEqual` function performs a reference check on elements (`a[i] !== b[i]`). This works perfectly for `GameModel` updates because the server/deserializer replaces changed objects with new references. However, it's worth adding a comment explicitly stating this assumption so future developers don't use it for deep objects expecting value comparison.

**Recommendation:**
```typescript
/**
 * Compare two arrays...
 * Relies on reference equality of elements (immutable updates pattern).
 */
```

## Praise

### 1. Stable Empty Array Constants

**Location:** `gameStoreHooks.ts:39`

Defining `EMPTY_PLAYERS`, `EMPTY_TILES` etc. is an excellent pattern. It prevents the common React "infinite re-render" bug where a selector returns a new `[]` reference every time a list is empty.

### 2. Custom Equality Functions

**Location:** `gameStoreHooks.ts:65`

The manual implementation of `actionFlagsEqual` and `profilesEqual` is precise and performant. It avoids the overhead of a generic "deep equal" while ensuring components only render when relevant data actually flips.

## Follow-Up Actions

- [ ] Block integration until `react-ui/lib/extensions/*` are created.
- [ ] Verify `zustand` v5 import paths.

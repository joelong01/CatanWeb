# Code Review: Phase 1.5 Extensions

**Files Reviewed:**

- `react-ui/lib/extensions/resourcesExtensions.ts`
- `react-ui/lib/extensions/buildingExtensions.ts`
- `react-ui/lib/extensions/gameModelExtensions.ts`
- `react-ui/lib/extensions/tileExtensions.ts`
- `react-ui/lib/extensions/index.ts`
- Associated tests

**Reviewed:** 2026-01-28
**Reviewer:** GitHub Copilot (Gemini)

## Summary

This review covers the "Phase 1.5" extension ports identified in the `react-refactoring-audit.md`. These additions bridge the gap between the initial Phase 1 port and the requirements for the UI refactoring in Phase 3. The changes include resource calculation logic, entitlement purchase models, and key game logic helpers like `ownedAdjacentRoadsNotCounted` (essential for longest road).

The implementation faithfully ports the C# logic to TypeScript, correctly handling the conversion from C# mutable patterns to TypeScript where appropriate, while maintaining compatibility with the defined C# models.

## Critical Issues

*None found.*

## Important Issues

*None found.*

## Suggestions

### 1. `resourcesExtensions.ts` Mutability Clarity

**Location:** `react-ui/lib/extensions/resourcesExtensions.ts:46`

The `addResource` function documentation correctly notes that it mutates the model.

```typescript
/**
 * Adds a resource amount to a ResourcesModel.
 * Mutates the model in place (matches C# behavior).
 */
export function addResource(model: ResourcesModel, ...)
```

**Suggestion:** While this matches C# behavior and is safe when used with `createEmptyResourcesModel()` (as seen in `resourcesForBuilding`), be cautious if this is ever used directly on React state or Zustand store objects outside of an Immer producer. Consider adding a JSDoc `@example` showing correct usage (e.g., "Use inside immer producer or on local objects") to prevent accidental state mutation bugs in the future.

## Praise

### 1. Robust `ownedAdjacentRoadsNotCounted` Port

**Location:** `react-ui/lib/extensions/gameModelExtensions.ts:534`
The porting of the longest road logic is accurate and handles the edge case of "road blocked by opponent settlement" correctly (`buildingBetween.ownerId !== r.ownerId`). This is a critical rule in Catan that is often missed.

### 2. Standardized Star Calculation

**Location:** `react-ui/lib/extensions/tileExtensions.ts:152`
Updated `totalStars` to use `tile.stars` instead of calculating from pips. This aligns the client logic with the server's authoritative data and simplifies the implementation.

### 3. Comprehensive Testing

The new test files (`resourcesExtensions.test.ts` and updates to others) provide excellent coverage. Specifically, testing `purchaseModel` with enabled/disabled states ensures the UI will correctly reflect server-side entitlement rules.

## Follow-Up Actions

- [ ] COMPLETE: Matches requirements in `react-refactoring-audit.md`.
- [ ] Ensure `addResource` is only used on transient objects or within Immer producers.

## Conclusion

**Approved.** The code is high quality, well-tested, and ready to support the Phase 2/3 refactoring efforts.

# UI Automation Best Practices for Catan Tests

## Handling FlipperCtrl Purchase Buttons

### Problem
Purchase buttons in the Catan app are inside `FlipperCtrl` controls that can be "face down" (showing the back) or "face up" (showing the front with buttons). When face down, the buttons are not accessible via standard automation searches.

### Best Practice Solution: Dynamic Search with Fallback

Our implementation uses a multi-tier approach:

#### Tier 1: Cache Lookup (Fast)
```csharp
// First try cached elements from initial load
if (_uiControlsCache.TryGetValue(automationId, out var cachedElement))
    return cachedElement;
```

#### Tier 2: Fresh DOM Search (Medium)
```csharp
// Perform fresh search of entire UI tree
var allElements = _mainWindow.FindAllDescendants();
// Search for matching AutomationId
```

#### Tier 3: FlipperCtrl Pattern Search (Thorough)
```csharp
// Look for FlipperCtrl parent containers
// Search children and grandchildren for target element
```

### Implementation Details

**In UIAutomationHelper.cs:**
- `ClickButton()` detects purchase buttons and uses `FindPurchaseButtonDynamically()`
- `FindPurchaseButtonDynamically()` implements the 3-tier search
- `FindByFlipperCtrlPattern()` handles FlipperCtrl-specific searching
- `LoadAutomationObjects()` is lenient about missing purchase buttons

### Why This Approach?

✅ **Performance**: Cache hits are fast, fallback only when needed
✅ **Reliability**: Works whether buttons are face-up or face-down  
✅ **Maintainability**: Centralized logic, doesn't require app changes
✅ **Debugging**: Detailed logging at each search tier
✅ **Robustness**: Multiple fallback strategies

### Alternative Approaches Considered

#### Option A: Set AutomationId on FlipperCtrl Parent
- **Pros**: Could click parent, navigate to button
- **Cons**: Requires app changes, breaks encapsulation

#### Option B: Wait for Flip Animation
- **Pros**: Simple, waits for natural state changes  
- **Cons**: Assumes state will change, adds test time

#### Option C: Force Flip via Game State
- **Pros**: Deterministic, controls when buttons appear
- **Cons**: Requires game logic knowledge, fragile

### Usage Example

```csharp
// Works regardless of FlipperCtrl state
uiHelper.ClickButton("PurchaseRoadButton");    // ✅ Dynamic search
uiHelper.ClickButton("PurchaseSettlementButton"); // ✅ Fallback patterns
uiHelper.ClickButton("UiPumpButton");         // ✅ Fast cache lookup
```

### Debugging

When purchase buttons aren't found, look for these log messages:
- "INFO: Purchase buttons not found in initial cache (may be face-down)"
- "Performing dynamic search for purchase button"  
- "Found via FlipperCtrl pattern search"
- "Purchase button not found via dynamic search"

### Testing Different Scenarios

1. **Face-up buttons**: Should use cache/fast path
2. **Face-down buttons**: Should use dynamic search 
3. **Animation in progress**: Should retry and eventually succeed
4. **Disabled purchase**: Should provide clear error message

This approach follows UI automation best practices by being resilient to UI state changes while maintaining good performance characteristics.
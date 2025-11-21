# Swap Tiles Feature - Design Document

## Overview
Enable players to drag and drop tiles during the **PickingBoard** game state to exchange resource types between tiles. This allows visual tile arrangement during board setup.

---

## Requirements

### Functional Requirements
1. **Drag Initiation**
   - Left-click and drag on a tile (excluding CatanNumber control) initiates drag operation
   - Minimum drag threshold of 10 pixels to avoid accidental triggers
   - Only available during PickingBoard game state

2. **Drag Visual Feedback**
   - Drag cursor shows the source tile's resource type as a semi-transparent visual
   - Visual follows the cursor with slight offset
   - Cursor changes to indicate drag-in-progress

3. **Hover Highlighting**
   - As cursor moves over tiles during drag, target tile highlights
   - Only one tile highlights at a time (the one directly under cursor)
   - Non-hovered tiles return to normal state
   - Accomplished via MVVM message broadcasting

4. **Drop & Swap**
   - Release mouse over a valid tile to swap resources
   - Swap exchanges resource types between source and destination tiles
   - Invalid drops (outside board) cancel the operation
   - Swaps are undoable via existing Undo/Redo system

### Non-Functional Requirements
- Swaps are sent as MVVM messages for consistency with game architecture
- Game service/local game handles actual swap logic
- Client-side state updates automatically via GameModel change propagation
- Swaps are recorded in game history
- Works only during PickingBoard state (enforced server-side)
- New message handler integrates with existing GameMessageService pattern
- GameStateMachine processes swap logic on both client and server
- Swap is recorded in undo/redo stack via GameStateMachine

### Constraints
- Desktop app only (no touch support needed)
- Performance acceptable with 19+ tiles (broadcast highlighting is fine)
- Swap includes both tile IDs and their current resources for validation
- Must follow existing MVVM Toolkit messaging patterns
- Must integrate with existing GameStateMachine message handling
- Swap must be compatible with existing undo/redo recording system

---

## Revised Architecture Overview (Centralized Hit Testing)

Initial approach attempted to rely on `PointerEntered/PointerExited` events per tile. That proved unreliable due to hex overlap, transparency, and potential pointer capture. New approach centralizes pointer ? tile resolution:

1. A board-level hit test function converts current pointer position to `HexCoordinates?`.
2. This function lives in the main board context (temporary: `MainPage` or board root control) to allow experimentation and rapid iteration.
3. A global `PointerMoved` hook invokes the hit test and broadcasts a `HighlightTile` message:
   - Sends the coordinates if over a tile.
   - Sends `null` if outside all tiles.
4. Drag and swap logic will later consume the same hit-test function for destination selection.

Benefits:
- One source of truth for coordinate math.
- Eliminates per-tile hover fragility.
- Simplifies logging and diagnostics.
- Scales if we change visual layering.

---

## New Hit Test API

### Function Signature (Proposed)
```csharp
/// <summary>
/// Returns the HexCoordinates under the given board-relative pointer position, or null if none.
/// </summary>
public HexCoordinates? HitTestBoard(Point boardPoint)
```

### Inputs
- `Point boardPoint`: Pointer position relative to the board container (not screen coordinates).

### Implementation Strategy
1. Convert from raw pointer position to axial cube coordinates using board layout math (already encapsulated in `BoardVisualLayout`).
2. Round/normalize to nearest hex.
3. Validate the resulting coordinates exist in current `GameModel.Tiles` collection.
4. Return `null` if no match.

### Temporary Location
- Implement initially in `MainPage` (or the parent board user control) for quick testing.
- Later refactor into a reusable static helper or extension (e.g., `BoardLayoutExtensions.HitTest(...)`).

---

## Message Types (Unchanged)

### 1. HighlightTile Message
```csharp
public class HighlightTile
{
    public HexCoordinates? TargetTileCoordinates { get; set; }
    // null = no tile highlighted
}
```

### 2. SwapTileResources Message
```csharp
public class SwapTileResources
{
    public HexCoordinates SourceTileCoordinates { get; set; }
    public HexCoordinates DestinationTileCoordinates { get; set; }
    public ResourceType SourceCurrentResource { get; set; }
    public ResourceType DestinationCurrentResource { get; set; }
}
```

---

## Updated Implementation Plan (Phases)

### Phase 0: Preparation / Diagnostic
- Ensure `DebugWindow` stable for high-frequency pointer logging.
- Add temporary logging hooks to observe pointer movement and hit test outcomes.

### Phase 1: Central Hit Test Function
1. Implement `HitTestBoard(Point)` in `MainPage` (or `GameView` container).
2. Wire `PointerMoved` on the board root.
3. On each move:
   - Translate pointer to board coordinates.
   - Call `HitTestBoard`.
   - Send `HighlightTile` with result (null to clear).
4. Log (throttled) hit test results.
5. Verify only one tile highlights at a time via `HighlightedEffective` changes.

### Phase 2: Refine Hit Test Accuracy
- Adjust math for hex boundaries (center vs edge conditions).
- Handle edge cases (near corners or outside bounding box).
- Optimize: early reject if outside board bounding rectangle before expensive hex rounding.

### Phase 3: Integrate Drag Threshold
1. Add drag state: record source tile when mouse pressed.
2. Start “drag” only after pointer moved > threshold (10px).
3. Use existing highlight feed from Phase 1 (no per-tile events).
4. Keep cursor changes and logging.

### Phase 4: Execute Swap
1. On mouse release while dragging, resolve destination via latest highlighted coordinate.
2. If different from source: send `SwapTileResources` capturing both current resource types.
3. Clear highlight (send `HighlightTile(null)`).
4. Log swap intent and result.

### Phase 5: GameStateMachine Validation (Already Present)
- Ensure `HandleSwapResourcesAsync` rejects non-PickingBoard states and mismatches.
- Confirm undo/redo stacks record swap.

### Phase 6: Service Synchronization
- Confirm SignalR hub method `ExecuteSwapTileResources` receives and broadcasts updated state.
- Multi-client test: highlight local only; swap result reflected on all.

### Phase 7: Cleanup & Refactor
- Move `HitTestBoard` out of `MainPage` into a dedicated helper or layout extension.
- Remove temporary verbose logging/throttling.
- Consider accessible keyboard alternative (optional future work).

### Phase 8: Optional Enhancements
- Add ghost drag visual (semi-transparent resource image following cursor).
- Provide cancel (ESC) support.
- Add analytics counters (swap count, invalid attempts).

---

## Hit Test Pseudocode
```csharp
public HexCoordinates? HitTestBoard(Point boardPoint)
{
    // Quick bounding box reject
    if (boardPoint.X < 0 || boardPoint.Y < 0 ||
        boardPoint.X > Layout.ControlWidthTotal ||
        boardPoint.Y > Layout.ControlHeightTotal)
        return null;

    // Convert to fractional axial coordinates using layout scale
    var qf = (boardPoint.X - Layout.OriginX) / Layout.HexHorizontalStride;
    var rf = (boardPoint.Y - Layout.OriginY) / Layout.HexVerticalStride;

    // Cube rounding
    var cube = HexMath.RoundToHex(qf, rf);
    var coords = new HexCoordinates(cube.Q, cube.R, cube.S);

    // Verify tile exists
    return GameModel.Tiles.Any(t => t.TileKey == coords) ? coords : null;
}
```
*(Exact math will depend on existing `BoardVisualLayout` helpers; adapt accordingly.)*

---

## Testing Plan (Incremental)

### Phase 1 Tests
- Move mouse across board: verify highlight changes follow pointer.
- Move outside board: highlight cleared.
- Rapid movement: no exceptions, performance acceptable.

### Phase 3–4 Tests
- Drag threshold: click without moving <10px should not start drag.
- Drag source to destination: swap occurs; resources exchanged.
- Undo ? restores pre-swap; Redo ? reapplies swap.

### Multi-client
- Client A performs swap; Client B receives updated board state.
- Highlight is local-only (no cross-client highlight messages).

---

## Timeline (Updated)
| Phase | Effort | Status |
|-------|--------|--------|
| 0 Prep | 15m | Done (DebugWindow stable) |
| 1 Hit Test API | 45m | Pending |
| 2 Accuracy Refinement | 30–45m | Pending |
| 3 Drag Threshold | 30m | Pending |
| 4 Swap Execution | 30m | Pending |
| 5 Validation | 20m | Existing logic |
| 6 Service Sync | 30m | Partial (hub added) |
| 7 Refactor | 30m | Pending |
| 8 Enhancements | Optional | Future |
| **Total (core)** | **~4–5h** | In progress |

---

## Open Questions
1. Exact placement for `HitTestBoard` – keep temporary in `MainPage` or `GameViewModel`? (Decision: start in `MainPage` for rapid iteration.)
2. Do we need a drag visual immediately? (Deferred.)
3. Should highlight persist after drag end until next move? (Current: cleared.)
4. Need keyboard accessibility for swaps? (Future enhancement.)

---

## Next Immediate Step
Implement `HitTestBoard(Point)` in `MainPage`, wire `PointerMoved` to broadcast `HighlightTile` messages. Log coordinates and tile matches to verify correctness before enabling drag threshold and swaps.


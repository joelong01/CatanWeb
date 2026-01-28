# React Game State UI Design

This document maps all 33 GameStates to their UI requirements and tracks React implementation status.

## Architecture Overview

```text
┌─────────────────┐     REST POST      ┌─────────────────┐
│   React Client  │ ─────────────────► │   GameService   │
│   (page.tsx)    │                    │                 │
│                 │ ◄───────────────── │ GameStateMachine│
│   GameModel     │    SignalR         │                 │
│   (Zustand)     │  GameStateUpdated  │   GameModel     │
└─────────────────┘                    └─────────────────┘
```

**Flow:**

1. User action triggers REST POST to `/api/game/action`
2. GameStateMachine processes message, modifies GameModel
3. Modified GameModel broadcast via SignalR `GameStateUpdated`
4. React client receives update, stores in Zustand, re-renders

**Key principle:** GameModel is the single source of truth. All UI state derives from it.

## ActionFlags Reference

Server-controlled flags that determine UI button states:

```typescript
interface ActionFlags {
  undoEnabled: boolean;   // Can undo last action
  redoEnabled: boolean;   // Can redo undone action
  nextEnabled: boolean;   // Can proceed to next state
  rollsEnabled: boolean;  // Can roll dice (only WaitingForRoll)
}
```

**AllowNext logic** (GameStateMachine.cs:1097-1108):

- Returns `false` during `WaitingForRoll` or `MustMoveRobber`
- Returns `false` if `currentPlayer.UnspentEntitlements.Count > 0`
- Otherwise returns `true`

## Entitlement System

Three-tier tracking for purchases:

| Property | Description | UI Impact |
|----------|-------------|-----------|
| `UnspentEntitlements` | Purchased but not placed | Blocks Next button, shows placement needed |
| `SpentEntitlementsThisTurn` | Placed this turn only | Turn summary |
| `SpentEntitlementsThisGame` | Cumulative for scoring | Purchase stats display |

**Entitlement types:** Road, Settlement, City, DevCard, Ship, Soldier, and expansion-specific types.

## GameState Matrix

### Setup Phase (Pre-Game)

| State | Description | UI Required | React Status |
|-------|-------------|-------------|--------------|
| `Uninitialized` | Initial state | None | N/A |
| `WaitingForNewGame` | Game not created | None (handled by home page) | N/A |
| `WaitingForPlayers` | Players joining | None (handled by home page) | N/A |
| `PickingBoard` | Shuffle/balance/accept board | Shuffle button (enabled), Balance menu item (visible) | DONE |

**PickingBoard controls:**

- **Shuffle button** (center hex of Board Measurement): Enabled only during `PickingBoard`
- **Balance menu item** (burger menu): Visible only during `PickingBoard`
- After board is approved (Next clicked), both controls become disabled/hidden

### Roll Order Phase

| State | Description | UI Required | React Status |
|-------|-------------|-------------|--------------|
| `WaitingForRollForOrder` | Players roll to determine order | Roll controls, display rolls | PARTIAL - rolls work |
| `FinishedRollOrder` | Select who goes first | **Player selection overlay** | **MISSING** |

**FinishedRollOrder implementation needed:**

- Blazor: Flips player cards to show "Go First" button
- React design: HexGrid ring overlay with player avatars
- Command: `proxy.goFirst(playerId)`

### Allocation Phase (Initial Settlement Placement)

| State | Description | UI Required | React Status |
|-------|-------------|-------------|--------------|
| `BeginResourceAllocation` | Start of allocation | Grant entitlements (Settlement + Road) | PARTIAL |
| `AllocateResourceForward` | Forward pass (player 1→N) | Click building → place settlement, click road → place road | **MISSING** |
| `AllocateResourceReverse` | Reverse pass (player N→1) | Same as forward | **MISSING** |
| `DoneResourceAllocation` | Allocation complete | Next button enabled | DONE |

**Allocation phase behavior:**

- Each player granted Settlement + Road entitlements
- `nextEnabled` is `false` until both are spent (placed)
- Forward: players 1, 2, 3, 4 in order
- Reverse: players 4, 3, 2, 1 in order
- Board measurement panel shown (not roll panel)

## Board Overlay UI Specification (Comprehensive)

This section details the **complete** rendering logic for buildings and roads across ALL game states.
Reference implementation: Blazor `BuildingOverlay.razor` and `RoadOverlay.razor`.

---

### Road Rendering Logic

**Source:** `RoadOverlay.razor` (120 lines)

#### Data Model

```typescript
interface RoadModel {
  roadKey: RoadKey;           // { tileKey: HexCoordinates, hexSide: HexSide }
  roadState: RoadState;       // 'Unowned' | 'Buildable' | 'Road' | 'Ship'
  ownerId: string | null;     // null = unowned, else player ID
  buildIndex: number;         // 0 = no index, >0 = numbered for regular gameplay
}
```

#### Visibility Rule (CRITICAL)

```csharp
// RoadOverlay.razor:70-72
private IEnumerable<RoadModel> GetVisibleRoads()
{
    return Roads.Where(r => r.OwnerId != null || r.RoadState == RoadState.Buildable);
}
```

**Only render roads where:**

- `ownerId != null` (owned by a player), OR
- `roadState === 'Buildable'` (server marked as buildable)

**NEVER render roads that are `Unowned` with no buildable flag.** The server controls which roads are buildable based on entitlements and adjacency rules.

#### Color Selection

```csharp
// RoadOverlay.razor:78-85
private PlayerColors? GetRoadColors(RoadModel road)
{
    if (road.OwnerId != null)
    {
        return Players.FirstOrDefault(p => p.Id == road.OwnerId)?.Colors;
    }
    return CurrentPlayer?.Colors;
}
```

| Condition | Colors Used |
|-----------|-------------|
| `ownerId != null` | **Owner's** player colors |
| `roadState === 'Buildable'` | **Current player's** colors |

#### Opacity

```html
<!-- RoadOverlay.razor:34 -->
opacity="@(road.OwnerId != null ? "1" : "0.5")"
```

| Condition | Opacity |
|-----------|---------|
| Owned (`ownerId != null`) | **100%** |
| Buildable (`roadState === 'Buildable'`) | **50%** |

#### Cursor

```html
<!-- RoadOverlay.razor:27 -->
style="cursor: @(road.RoadState == RoadState.Buildable ? "pointer" : "default")"
```

| Condition | Cursor |
|-----------|--------|
| `roadState === 'Buildable'` | `pointer` |
| Otherwise | `default` |

#### Click Handler

```csharp
// RoadOverlay.razor:113-118
private async Task HandleRoadClick(RoadModel road)
{
    if (road.RoadState == RoadState.Buildable && OnRoadClick.HasDelegate)
    {
        await OnRoadClick.InvokeAsync(road.RoadKey);
    }
}
```

**Only fire click callback if `roadState === 'Buildable'`.**

#### Build Index Display

```html
<!-- RoadOverlay.razor:36-49 -->
@if (road.BuildIndex > 0)
{
    <g transform="translate(@midX, @midY)">
        <rect x="-18" y="-14" width="36" height="28" rx="6" fill="black" />
        <text x="0" y="0"
              text-anchor="middle"
              dominant-baseline="central"
              font-family="Segoe UI, sans-serif"
              font-size="24"
              font-weight="bold"
              fill="white">@road.BuildIndex</text>
    </g>
}
```

**When BuildIndex > 0:**

- Black rounded rectangle (36x28px, 6px corner radius) centered on road
- White bold number inside showing the build index
- Allows players to say "click on road number 3"
- Server sets `buildIndex` based on adjacency to player's buildings/roads
- During allocation phase, roads adjacent to just-placed settlement get sequential numbers
- During regular gameplay, roads adjacent to player's network get sequential numbers

**BuildIndex rendering:**

| buildIndex | Render |
|------------|--------|
| 0 | No label (road polygon only) |
| > 0 | Black rect + white number centered on road |

#### Keyboard Shortcuts for Road Building

When buildable roads are displayed with numbered indexes (1-9), the user can press the corresponding number key to build that road instantly.

**Implementation:**

```typescript
// In page.tsx - listen for keydown events
useEffect(() => {
  const handleKeyDown = (e: KeyboardEvent) => {
    // Only handle 1-9 keys
    const num = parseInt(e.key);
    if (isNaN(num) || num < 1 || num > 9) return;

    // Find road with matching buildIndex
    const road = gameModel?.roads?.find(r =>
      r.roadState === 'Buildable' && r.buildIndex === num
    );

    if (road) {
      proxy.purchaseRoad(road.roadKey);
    }
  };

  window.addEventListener('keydown', handleKeyDown);
  return () => window.removeEventListener('keydown', handleKeyDown);
}, [gameModel?.roads, proxy]);
```

**Rules:**

- Only keys 1-9 are supported (avoids multi-key input complexity)
- Only works when roads have `buildIndex > 0` AND `roadState === 'Buildable'`
- Pressing a number with no matching road does nothing
- Works during allocation phase and regular gameplay when road entitlement is active

#### SVG Gradient (2-stop)

```html
<linearGradient id="gradient-{playerId}" x1="0%" y1="0%" x2="100%" y2="100%">
    <stop offset="0%" stopColor={colors.primary} />
    <stop offset="100%" stopColor={colors.secondary} />
</linearGradient>
```

---

### Building Rendering Logic

**Source:** `BuildingOverlay.razor` (290 lines)

Buildings are rendered in **two separate loops**:

1. **Owned buildings** - settlements and cities with an owner
2. **Buildable spots** - complex conditional logic

#### Data Model

```typescript
interface BuildingModel {
  buildingKey: BuildingKey;   // { hexCoordinates: HexCoordinates, position: HexPosition }
  buildingState: BuildingState; // 'PossibleSettlement' | 'NotBuildable' | 'Settlement' | 'City' | 'Metropolis' | 'Knight'
  ownerId: string | null;     // null = unowned, else player ID
}
```

#### Loop 1: Owned Buildings

```csharp
// BuildingOverlay.razor:126-130
private IEnumerable<BuildingModel> GetOwnedBuildings()
{
    return Buildings.Where(b => b.OwnerId != null &&
        (b.BuildingState == BuildingState.Settlement || b.BuildingState == BuildingState.City));
}
```

**Filter:** `ownerId != null` AND (`buildingState === 'Settlement'` OR `buildingState === 'City'`)

**Rendering:**

- Circle radius: **24** SVG units
- Fill: Owner's gradient (`url(#gradient-{ownerId})`)
- Stroke: `ownerColors.Secondary`
- Content: Catan font glyph
  - Settlement: `\uE926`
  - City: `\uE900`
- Font size: `24 * 1.4 = 33.6`

#### Loop 2: Buildable/Star Spots

This is the complex logic in `GetBuildableSpots()` (lines 142-256).

**Intermediate Data Structure:**

```typescript
interface BuildingSpot {
  building: BuildingModel;
  stars: number;              // Sum of pips from adjacent tiles
  x: number;                  // Pixel position
  y: number;
  isBuildable: boolean;       // Can be clicked to build
  isHidden: boolean;          // Hidden by default, reveals on hover
  buildIndex: string | null;  // "1", "2"... for settlements, "A", "B"... for cities
}
```

**Input Variables:**

```csharp
var currentPlayer = GameModel.CurrentPlayer();
var hasSettlementEntitlement = currentPlayer.UnspentEntitlements.Contains(Entitlement.Settlement);
var hasCityEntitlement = currentPlayer.UnspentEntitlements.Contains(Entitlement.City);
var isPickingBoard = GameModel.GameState == GameState.PickingBoard;
var isPickingResources = GameModel.Phase() == GamePhase.PickingResources; // Allocation phase
```

**Decision Tree (per building):**

```
1. CITY UPGRADE CHECK
   IF hasCityEntitlement
      AND buildingState === 'Settlement'
      AND ownerId === currentPlayer.Id
   THEN:
      - isBuildable = true
      - isHidden = false
      - buildIndex = 'A', 'B', 'C'... (letter sequence)
      - CONTINUE to next building (don't fall through)

2. SKIP OWNED BUILDINGS
   IF ownerId != null
   THEN: SKIP (rendered in Loop 1)

3. FILTER BY BUILDING STATE
   IF buildingState === 'NotBuildable':
      IF NOT isPickingBoard: SKIP
      (Only show NotBuildable during PickingBoard for star evaluation)
   ELSE IF buildingState !== 'PossibleSettlement':
      SKIP

4. COMPUTE STARS
   stars = sum of pips from all adjacent tiles (0-3 tiles per vertex)

5. SKIP ZERO-STAR SPOTS DURING PICKING
   IF stars <= 0 AND isPickingBoard: SKIP

6. APPLY RESOURCE FILTER
   IF filteredResources.length > 0 AND (isPickingBoard OR isPickingResources):
      Get adjacent tile resource types
      IF NOT all filteredResources are present: SKIP

7. DETERMINE VISIBILITY

   CASE A: isPickingBoard
      - shouldShow = stars >= ShownStars
      - isBuildable = false (can't build during board picking)
      - isHidden = false

   CASE B: hasSettlementEntitlement AND buildingState === 'PossibleSettlement'
      - shouldShow = true
      - isBuildable = true

      IF NOT isPickingResources (regular gameplay):
         - buildIndex = "1", "2", "3"... (number sequence)
         - isHidden = false

      ELSE (allocation phase):
         - buildIndex = null (show stars instead)
         - isHidden = stars < ShownStars

   CASE C: Otherwise
      - shouldShow = false (don't render)
```

**Rendering:**

- Circle radius: **18** SVG units (smaller than owned buildings)
- Fill: Current player's gradient (`url(#gradient-{currentPlayerId})`)
- Stroke: `currentPlayerColors.Foreground`
- Content: `buildIndex ?? stars.toString()` (priority to build index)
- Font: "Segoe UI, sans-serif", size 20, bold
- Cursor: `pointer` if `isBuildable`, else `default`

**CSS Classes:**

```csharp
if (spot.IsBuildable)
    classes.Add("building-spot-buildable");
else
    classes.Add("building-spot-stars");

if (spot.IsHidden)
    classes.Add("building-spot-hidden");   // CSS: opacity 0 → 1 on hover

if (!string.IsNullOrEmpty(spot.BuildIndex))
    classes.Add("building-spot-indexed");  // CSS: opacity 0.5
```

**Click Handler:**

```csharp
private async Task OnSpotClick(BuildingSpot spot)
{
    if (spot.IsBuildable && OnBuildingClick.HasDelegate)
    {
        await OnBuildingClick.InvokeAsync(spot.Building.BuildingKey);
    }
}
```

---

### Game Phase Summary

| Game Phase | Buildings Shown | Roads Shown |
|------------|-----------------|-------------|
| **PickingBoard** | Owned only (React: no star evaluation mode) | None |
| **Allocation (Forward/Reverse)** | PossibleSettlement spots when hasSettlementEntitlement (stars, clickable) | Server-set Buildable roads (50% opacity, clickable) |
| **WaitingForRoll** | Owned only | Owned only |
| **WaitingForNext** | Owned + buildable spots with stars when hasSettlementEntitlement | Owned + server-set Buildable roads |

**React simplification:** Stars are ONLY shown when `hasSettlementEntitlement` is true. No board evaluation mode during PickingBoard.

---

### Gradient Consistency

**Blazor Pattern:**

| Context | Gradient Type | Definition |
|---------|---------------|------------|
| SVG fills (roads, buildings) | 2-stop | `Primary (0%) → Secondary (100%)` |
| CSS backgrounds (panels, buttons) | 3-stop | `Primary → Secondary → Black (135deg)` |

**React Implementation:**

```typescript
// For SVG <linearGradient>
<linearGradient id={gradientId} x1="0%" y1="0%" x2="100%" y2="100%">
  <stop offset="0%" stopColor={colors.primary} />
  <stop offset="100%" stopColor={colors.secondary} />
</linearGradient>

// For CSS background (use buildCssGradient from playerColors.ts)
import { buildCssGradient } from '@/lib/utils/playerColors';
const cssGradient = buildCssGradient(colors);
// Returns: linear-gradient(135deg, primary, secondary, endColor)
```

---

### Implementation Checklist

#### Roads

- [ ] Only render if `ownerId != null` OR `roadState === 'Buildable'`
- [ ] Colors: owner's if owned, current player's if buildable
- [ ] Opacity: 100% if owned, 50% if buildable
- [ ] Cursor: pointer if buildable, default otherwise
- [ ] Click: only fire if `roadState === 'Buildable'`
- [ ] Gradient: 2-stop SVG gradient

#### Buildings (Owned)

- [ ] Filter: `ownerId != null` AND (`Settlement` OR `City`)
- [ ] Show Catan font glyph
- [ ] Use owner's gradient
- [ ] Larger radius (24 vs 18)

#### Buildings (Buildable Spots)

- [x] City upgrades: current player's settlements when `hasCityEntitlement`
- [x] Settlement spots: `PossibleSettlement` when `hasSettlementEntitlement`
- [x] Stars only shown when `hasSettlementEntitlement` (simplified from Blazor)
- [x] Star filter controls visibility (isHidden) when active
- [x] Current player's gradient (not owner's)
- [x] Smaller radius than owned buildings
- [x] Click: always fires when spot is rendered (entitlement required)

#### State Message

- [ ] During allocation/WaitingForNext: show "[N Unspent Entitlements]"

### Main Game Loop

| State | Description | UI Required | React Status |
|-------|-------------|-------------|--------------|
| `WaitingForRoll` | Must roll dice | Roll controls enabled, Next disabled | DONE |
| `WaitingForNext` | Build/trade phase | Purchase controls, Next when no unspent | PARTIAL |

**WaitingForNext behavior:**

- `rollsEnabled: false`
- `nextEnabled: true` (unless UnspentEntitlements exist)
- Purchase buttons enabled based on resources
- Can place buildings/roads if purchased

### Special States

| State | Description | UI Required | React Status |
|-------|-------------|-------------|--------------|
| `TooManyCards` | Player(s) must discard after 7 | Discard dialog | DEFERRED |
| `MustMoveRobber` | Robber must be moved | Tile click + target selection | DESIGNED (see below) |
| `PickingRandomGoldTiles` | Gold tile resource selection | None (auto-distributed) | N/A (unused) |
| `Supplemental` | Supplemental build phase | Same as WaitingForNext | DONE |
| `PickSupplementalPlayers` | Select who participates | **Player checkboxes + Done** | **MISSING** |
| `GameOver` | Game ended | Winner display, VP entry | PARTIAL |

### Expansion States (Lower Priority)

| State | Description | React Status |
|-------|-------------|--------------|
| `MustDestroyCity` | Expansion: destroy city | NOT IMPLEMENTED |
| `HandlePirates` | Expansion: pirate handling | NOT IMPLEMENTED |
| `DoneDestroyingCities` | After city destruction | NOT IMPLEMENTED |
| `MustMoveMerchant` | Move merchant | NOT IMPLEMENTED |
| `DestroyRoad` | Destroy a road | NOT IMPLEMENTED |
| `SwapNumbers` | Swap tile numbers | NOT IMPLEMENTED |
| `PickDeserter` | Pick deserter | NOT IMPLEMENTED |
| `PlaceDeserterKnight` | Place deserter knight | NOT IMPLEMENTED |
| `DoneWithDeserter` | Done with deserter | NOT IMPLEMENTED |
| `UpgradeToMetro` | Upgrade to metropolis | NOT IMPLEMENTED |
| `DisplaceVictimKnight` | Displace victim knight | NOT IMPLEMENTED |
| `DisplaceKnightMoveVictim` | Move victim knight | NOT IMPLEMENTED |
| `ClickOnKnight` | Click on a knight | NOT IMPLEMENTED |
| `TestCheckpoint` | Test state | N/A |

## Implementation Priority

### Critical (Blocks Basic Gameplay)

1. **FinishedRollOrder** - Cannot start game without selecting first player
   - Create `GoFirstOverlay` component
   - Wire `proxy.goFirst(playerId)`

2. **AllocateResourceForward/Reverse** - Cannot place initial settlements
   - Make buildable building positions clickable
   - Wire `proxy.upgradeBuilding(buildingKey)`
   - Make buildable road positions clickable
   - Wire `proxy.purchaseRoad(roadKey)`

3. **MustMoveRobber** - Cannot continue after rolling 7
   - Make land tiles clickable during this state
   - Show target player selection when robber lands on occupied tile
   - Wire `proxy.moveRobber(coordinates, targetPlayerId)`

### Important (Blocks Full Gameplay)

1. **PickSupplementalPlayers** - Cannot use supplemental build phase
   - Player cards with checkboxes
   - Done button to proceed
   - Wire `proxy.setParticipatingInSupplemental(playerId, participating)`

### Deferred

1. **TooManyCards** - Not implemented in any game version yet
   - Modal dialog showing player's resources
   - Select resources to discard (half, rounded down)
   - Wire discard command

## Commands Available But Not Wired

From `GameServiceProxy.ts`:

| Command | Method | Line | Wired in page.tsx? |
|---------|--------|------|-------------------|
| Go First | `goFirst(playerId)` | 479-481 | NO |
| Purchase Road | `purchaseRoad(roadKey)` | 452-454 | NO |
| Upgrade Building | `upgradeBuilding(buildingKey)` | 459-461 | NO |
| Move Robber | `moveRobber(coords, targetId)` | 466-474 | NO |
| Supplemental | `setParticipatingInSupplemental()` | 486-494 | NO |
| Declare Winner | `declareWinner(winnerId, vps)` | 525-533 | NO |

## FinishedRollOrder Design

### Blazor Implementation (Reference)

- Player cards flip to show "Go First" button on back
- Click button → `Connection.GoFirstAsync(targetPlayerId)`
- State transitions to `BeginResourceAllocation`

### React Implementation (HexGrid-Based)

**Component:** `GoFirstOverlay` (wrapped in `FloatingPanel`)

- Uses standard `FloatingPanel` wrapper (panelId: `"goFirst"`):
  - Draggable/movable
  - Resizable
  - Scale-to-container content
  - Position persistence via `layoutStore`
- HexGrid ring layout (RING_6 for up to 6 players)
- Center hex shows "Pick Who Goes First" text
- Each outer hex contains:
  - Player avatar image
  - Player name
  - Player color border
- Click behavior: button-like with hover/press states
- Click action: `proxy.goFirst(playerId)`

```typescript
interface GoFirstOverlayProps {
  players: PlayerModel[];
  playerProfiles: Map<string, PlayerProfile>;
  onSelectPlayer: (playerId: string) => void;
}
```

## PickSupplementalPlayers Design

### Blazor PickSupplementalPlayers (Reference)

- Player cards flip to show checkbox for supplemental participation
- Current player (who just finished their turn) is excluded
- "Done" button confirms selection and proceeds
- Empty selection is valid (no one participates)

### React PickSupplementalPlayers (HexGrid-Based)

**Component:** `SupplementalOverlay` (wrapped in `FloatingPanel`)

- Uses standard `FloatingPanel` wrapper (panelId: `"supplemental"`):
  - Draggable/movable
  - Resizable
  - Scale-to-container content
  - Position persistence via `layoutStore`
- HexGrid ring layout (RING_6 for up to 6 players)
- Center hex shows "Supplemental Build" title
- **Current player is excluded** from the ring (they just finished their turn)
- Each outer hex contains:
  - Player avatar image
  - Player name
  - Player color border
  - **Toggle state** (selected/unselected) - default OFF
- Multi-select behavior: click toggles participation
- Selected hexes get visual indicator (checkmark, glow, or filled state)
- **Next button** (from ActionCluster) confirms selection
- Click Next → POST `setParticipatingInSupplemental` for each selected player → state transitions

```typescript
interface SupplementalOverlayProps {
  players: PlayerModel[];           // Excludes current player
  playerProfiles: Map<string, PlayerProfile>;
  selectedPlayerIds: Set<string>;   // Multi-select state
  onTogglePlayer: (playerId: string) => void;
}
```

### Key Differences from GoFirstOverlay

| Aspect | GoFirstOverlay | SupplementalOverlay |
|--------|----------------|---------------------|
| Panel ID | `"goFirst"` | `"supplemental"` |
| Selection | Single-click selects and submits | Multi-select toggles, Next submits |
| Default | No default | All OFF by default |
| Validation | Must select one player | Empty selection is valid |
| Submit | Click hex → immediate POST | Click Next → batch POST |
| Players shown | All players | All except current player |

**Shared FloatingPanel behaviors:** Both overlays use the standard FloatingPanel wrapper providing draggable, resizable, scale-to-container, and position persistence via layoutStore.

## MustMoveRobber Design

### Overview

When a player rolls a 7 or plays a Soldier card, the game enters `MustMoveRobber` state. The player must:

1. Select a new tile for the robber (right-click on tile)
2. If the tile has adjacent buildings owned by other players, select a target to steal from
3. Option to target "Nobody" (hatred deferred) if stealing is not desired

### Blazor Implementation (Reference) - EXACT DETAILS

**Robber Menu Structure (Game.razor:351-374):**

```razor
@* Robber target selection menu - outside game-container to avoid transform issues *@
@if (_robberMenuVisible && _robberMenuTile != null)
{
    <div class="robber-menu-backdrop" @onclick="CloseRobberMenu"></div>
    <div class="robber-menu" style="left: @(_robberMenuX)px; top: @(_robberMenuY)px;">
        <div class="robber-menu-header">Move Robber to Tile @GetTileIndex(_robberMenuTile)</div>
        @foreach (var target in _robberMenuTargets)
        {
            <div class="robber-menu-item" @onclick="() => SelectRobberTarget(target.Id)">
                Target @target.Name
            </div>
        }
        @if (_robberMenuTargets.Count > 0)
        {
            <div class="robber-menu-separator"></div>
        }
        <div class="robber-menu-item robber-menu-nobody" @onclick="() => SelectRobberTarget(null)">
            Nobody. Hatred Deferred.
        </div>
        <div class="robber-menu-separator"></div>
        <div class="robber-menu-item robber-menu-cancel" @onclick="CloseRobberMenu">
            Cancel
        </div>
    </div>
}
```

**CSS Styling (Game.razor.css:995-1047):**

```css
.robber-menu-backdrop {
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background: transparent;
    z-index: 999;
}

.robber-menu {
    position: fixed;
    min-width: 200px;
    background: #2a2a2a;
    border: 1px solid #444;
    border-radius: 8px;
    box-shadow: 0 4px 20px rgba(0, 0, 0, 0.5);
    z-index: 1000;
    overflow: hidden;
}

.robber-menu-header {
    padding: 10px 15px;
    font-weight: bold;
    background: #333;
    color: #fff;
    border-bottom: 1px solid #444;
}

.robber-menu-item {
    padding: 10px 15px;
    color: #fff;
    cursor: pointer;
    transition: background 0.15s ease;
}

.robber-menu-item:hover {
    background: #3a3a3a;
}

.robber-menu-nobody {
    color: #aaa;
    font-style: italic;
}

.robber-menu-cancel {
    color: #888;
}

.robber-menu-separator {
    height: 1px;
    background: #444;
    margin: 5px 0;
}
```

**GetRobberTargetsForTile (Game.razor:1303-1327):**

```csharp
private List<RobberTarget> GetRobberTargetsForTile(TileModel tile)
{
    var targets = new List<RobberTarget>();
    if (GameModel == null) return targets;

    // Desert has no targets
    if (tile.ResourceTileType == ResourceType.Desert) return targets;

    // Get buildings on this tile
    var buildings = GameModel.Buildings.OwnedBuildings(tile.TileKey);

    // Get unique owners (excluding current player)
    var ownerIds = buildings
        .Where(b => b.OwnerId != null && b.OwnerId != GameModel.CurrentPlayerId)
        .Select(b => b.OwnerId!)
        .Distinct();

    foreach (var ownerId in ownerIds)
    {
        var player = GameStateService.Players.First(p => p.Id == ownerId);
        targets.Add(new RobberTarget(player.Name, ownerId));
    }

    return targets;
}
```

**GetTileIndex (Game.razor:1332-1339):**

```csharp
private int GetTileIndex(TileModel tile)
{
    if (GameModel == null) return 0;
    var landTiles = GameModel.Tiles
        .Where(t => t.ResourceTileType != ResourceType.Sea)
        .ToList();
    return landTiles.IndexOf(tile) + 1;
}
```

**Robber Rendering with Player Color (RobberLayer.razor):**

The robber uses CatanFont glyphs with the moving player's color gradient:

```razor
<g class="robber"
   style="transform: translate(@(x)px, @(y)px); visibility: @visibility;"
   opacity="0.75">
    @* Background: SolidShield glyph (E925) with player gradient *@
    <text class="robber-shield"
          x="0" y="0"
          text-anchor="middle"
          dominant-baseline="central"
          font-family="Catan"
          font-size="@RobberFontSize"
          fill="url(#gradient-@MovedByPlayerId)">&#xE925;</text>

    @* Foreground: Pirate glyph (E90C) with player foreground color *@
    <text class="robber-pirate"
          x="0" y="0"
          text-anchor="middle"
          dominant-baseline="central"
          font-family="Catan"
          font-size="@(RobberFontSize * 0.8)"
          fill="@colors.Foreground">&#xE90C;</text>

    @* Resources stolen count *@
    @((MarkupString)RenderResourcesStolen(colors.Foreground))
</g>
```

**Key Robber Rendering Details:**

- Font size: 50% of hex height (`BoardSvgConstants.HexHeight * 0.50`)
- Shield glyph (background): `\uE925` - filled with player gradient
- Pirate glyph (foreground): `\uE90C` - filled with player foreground color
- Overall opacity: 75%
- Player gradients defined in SharedDefinitions.razor: `<linearGradient id="gradient-{playerId}">`

**Menu State Variables (Game.razor:484-489):**

```csharp
private bool _robberMenuVisible;
private TileModel? _robberMenuTile;
private double _robberMenuX;
private double _robberMenuY;
private List<RobberTarget> _robberMenuTargets = [];
private record RobberTarget(string Name, string Id);
```

### Data Model

```typescript
// RobberModel from generated types
interface RobberModel {
  coordinates: HexCoordinates;  // Current robber position
  movedByPlayerId?: string;     // Player who last moved the robber
  resourcesStolen: number;      // Count of resources stolen
}

// GameModel includes robber
interface GameModel {
  robber: RobberModel;
  gameState: GameState;
  currentPlayerId: string;
  // ... other properties
}
```

### GameServiceProxy Methods

```typescript
// GameServiceProxy.ts:466-474 - Move robber command
async moveRobber(coordinates: HexCoordinates, targetPlayerId: string | undefined): Promise<void> {
  await this.postCommand('moveRobber', {
    coordinates,
    targetPlayerId
  });
}
```

### React Implementation Plan

#### Step 1: Wire Soldier Button Click ✓

Add case to `handleAction` in page.tsx:

```typescript
case 'soldier':
  proxy.purchase('Soldier');
  break;
```

#### Step 2: Render Robber on GameBoard

**Current (image-based):** Using robber.svg image - needs to change to CatanFont for player color support.

**Target (matches Blazor):** Use CatanFont glyphs with player gradient:

```typescript
// In GameBoard SVG defs section - player gradients
{players.map(player => (
  <linearGradient key={player.id} id={`gradient-${player.id}`} x1="0%" y1="0%" x2="100%" y2="100%">
    <stop offset="0%" stopColor={profile?.colors?.primary || '#666'} />
    <stop offset="100%" stopColor={profile?.colors?.secondary || '#888'} />
  </linearGradient>
))}

// Robber rendering with CatanFont
{robber && robber.coordinates && (
  <g className="robber" transform={`translate(${robberX}, ${robberY})`} opacity={0.75}>
    {/* Background shield with player gradient */}
    <text
      textAnchor="middle"
      dominantBaseline="central"
      fontFamily="Catan"
      fontSize={hexHeight * 0.5}
      fill={`url(#gradient-${robber.movedByPlayerId})`}
    >
      {'\uE925'}
    </text>
    {/* Foreground pirate with player foreground color */}
    <text
      textAnchor="middle"
      dominantBaseline="central"
      fontFamily="Catan"
      fontSize={hexHeight * 0.4}
      fill={movedByColors?.foreground || '#fff'}
    >
      {'\uE90C'}
    </text>
  </g>
)}
```

#### Step 3: Right-Click on Tiles Shows Menu

Wire `onTileRightClick` in page.tsx - menu appears at click position:

```typescript
const handleTileRightClick = useCallback((tile: TileModel, event: React.MouseEvent) => {
  if (gameModel?.gameState !== 'MustMoveRobber') return;
  if (tile.resourceTileType === 'Sea') return;

  // Can't place on current robber location (unless desert)
  if (tile.tileKey === robber.coordinates && tile.resourceTileType !== 'Desert') return;

  // Get target players
  const targets = getPlayersWithBuildingsOnTile(tile);

  // Store menu state
  setRobberMenuTile(tile);
  setRobberMenuTargets(targets);
  setRobberMenuPosition({ x: event.clientX, y: event.clientY });
  setRobberMenuVisible(true);
}, [gameModel, robber]);
```

#### Step 4: RobberTargetMenu Component (Match Blazor Exactly)

```typescript
interface RobberTargetMenuProps {
  tile: TileModel;
  tileIndex: number;              // 1-based index of land tiles
  targetPlayers: { id: string; name: string }[];
  position: { x: number; y: number };
  onSelectTarget: (playerId: string | undefined) => void;
  onCancel: () => void;
}

// Structure matches Blazor exactly:
// - Backdrop (click to cancel)
// - Menu positioned at click location
// - Header: "Move Robber to Tile {index}"
// - Player targets: "Target {name}"
// - Separator (if targets exist)
// - "Nobody. Hatred Deferred." (italic, gray)
// - Separator
// - "Cancel" (gray)
```

#### Step 5: Visual Feedback

During MustMoveRobber:

- Land tiles get hover effect (cursor: pointer)
- Water/harbor tiles NOT clickable
- Currently occupied tile slightly dimmed (can't place on same tile)
- Valid tiles highlight on hover

#### Step 6: GriefDodgy House Rule

If enabled, special animations target the "Dodgy" player. Server handles via `FakeOutCoordinates`.

### Robber Implementation Checklist

- [x] Add 'soldier' case to `handleAction` in page.tsx
- [x] Create robber rendering in GameBoard (basic image version)
- [x] **Upgrade robber to CatanFont glyphs with player gradient** (matches Blazor)
- [x] Wire `onTileRightClick` prop from GameBoard to page.tsx
- [x] Add tile right-click handler that checks for MustMoveRobber state
- [x] Implement `getPlayersWithBuildingsOnTile()` helper function
- [x] **Rewrite `RobberTargetMenu` to match Blazor exactly:**
  - [x] Header: "Move Robber to Tile {index}"
  - [x] Player targets: "Target {name}"
  - [x] "Nobody. Hatred Deferred." (italic, gray)
  - [x] "Cancel" option
  - [x] Dark theme styling (#2a2a2a background)
  - [x] Position at click location
  - [x] Backdrop for click-away dismissal
- [x] Add state for `robberMenuTile`, `robberMenuTargets`, `robberMenuPosition`
- [x] Add `robberMenuVisible` state for menu visibility

### UI Store Integration

The `uiStore` already has robber menu infrastructure:

```typescript
// uiStore.ts - existing methods
isRobberMenuOpen: boolean;
openRobberMenu: () => void;
closeRobberMenu: () => void;
```

### Robber Code References

- **Soldier purchase handling:** `GameStateMachine.cs:770-800`
- **MoveRobber core logic:** `GameStateMachine.cs:1811-1855`
- **PreviousGameState save/restore:** `GameStateMachine.cs:1097-1108`
- **Target validation:** `GameModel.GetOwnedBuildingsInTile()`
- **React proxy method:** `GameServiceProxy.ts:466-474`
- **Blazor tile click:** `WebUI/Components/Board/BoardView.razor`
- **uiStore robber methods:** `react-ui/lib/stores/uiStore.ts`

---

## State Transition Flow

```text
WaitingForNewGame
       │
       ▼ (create game)
WaitingForPlayers
       │
       ▼ (all joined)
PickingBoard ◄──────┐
       │            │ (shuffle/balance)
       ▼            │
WaitingForRollForOrder
       │
       ▼ (all rolled)
FinishedRollOrder
       │
       ▼ (goFirst)
BeginResourceAllocation
       │
       ▼ (next)
AllocateResourceForward ◄──┐
       │                   │ (next, more players)
       │                   │
       ▼ (last player)     │
AllocateResourceReverse ◄──┤
       │                   │
       ▼ (first player)    │
DoneResourceAllocation
       │
       ▼ (next)
WaitingForRoll ◄─────────────┐
       │                     │
       ▼ (roll)              │
   [7 rolled?]───yes───► TooManyCards
       │                     │
       │no                   ▼ (discarded)
       │              MustMoveRobber
       │                     │
       │                     ▼ (moved)
       ▼                     │
WaitingForNext ◄─────────────┘
       │
       ▼ (next)
   [supplemental?]───yes───► PickSupplementalPlayers
       │                           │
       │no                         ▼
       │                     Supplemental
       │                           │
       └───────────────────────────┘
```

## PickingRandomGoldTiles Analysis

**Finding:** This state requires NO client-side UI implementation.

### How Gold Tiles Work in This Implementation

1. **TemporarilyGold House Rule** - `HouseRules.GoldTiles` (0-N) controls how many tiles are randomly marked as gold each turn

2. **Server-side logic** (`SetTempGoldTiles` in GameStateMachine.cs:1937):
   - At the start of each turn, N random tiles are marked `TemporarilyGold = true`
   - These tiles display with a gold texture overlay
   - Previous gold tiles are cleared before new ones are set

3. **Resource distribution** (`HandleRollAsync` in GameStateMachine.cs:1037):
   - When a roll matches a TemporarilyGold tile, the `effectiveType` becomes `ResourceType.GoldMine`
   - GoldMine resources are assigned AUTOMATICALLY - no player choice required

4. **GoldMine as a resource type**:
   - Unlike traditional Catan where gold lets you CHOOSE a resource
   - This implementation tracks GoldMine as its OWN resource type in `ResourcesModel`
   - Displayed alongside Wheat, Wood, Sheep, Brick, Ore, Robber in the UI

5. **PickingRandomGoldTiles state**:
   - Has an empty `break;` in `NextState()` (line 1345-1346)
   - No transitions INTO this state exist in the codebase
   - Appears to be a **placeholder for future "choose your resource" functionality**

### Conclusion

The `PickingRandomGoldTiles` state is NOT used in the current implementation. Gold resources are automatically distributed as `GoldMine` type without player interaction. The state can remain in the "Expansion States (Lower Priority)" category as it represents future functionality, not current behavior.

**React Status:** N/A (state not used in current game flow)

## Harbor Ownership

Harbors can be owned by players when a building is placed at an adjacent vertex.

### Data Model

```typescript
// HarborModel includes owner property
interface HarborModel {
  harborKey: HarborKey;     // { hexCoordinates, harborType, side }
  owner: PlayerModel | null; // null = unowned, else owning player
}
```

### Adjacency Logic

**Source:** `HarborModel.cs:187-250`

A harbor is adjacent to a building when:
1. Building is on the same hex as the harbor
2. Building's vertex position is one of the two vertices of the harbor's side

```csharp
// HarborModel.cs:187-195 - Maps each HexSide to its two adjacent vertices
private static readonly Dictionary<HexSide, (HexPosition, HexPosition)> SideToVertices = new()
{
    { HexSide.Top, (HexPosition.TopLeft, HexPosition.TopRight) },
    { HexSide.TopRight, (HexPosition.TopRight, HexPosition.Right) },
    { HexSide.BottomRight, (HexPosition.Right, HexPosition.BottomRight) },
    { HexSide.Bottom, (HexPosition.BottomRight, HexPosition.BottomLeft) },
    { HexSide.BottomLeft, (HexPosition.BottomLeft, HexPosition.Left) },
    { HexSide.TopLeft, (HexPosition.Left, HexPosition.TopLeft) },
};
```

### Ownership Assignment

**Source:** `GameStateMachine.cs:1615-1622`

When a building is placed, the server checks for adjacent harbors and assigns ownership:

```csharp
if (adjacentHarbor is not null)
{
    var currentPlayer = gameModel.CurrentPlayer();
    // If the building is adjacent to a harbor, set the owner of the harbor to the current player.
    adjacentHarbor.Owner = currentPlayer;
    // set the PlayerModel to have owned this harbor
    ...
}
```

### Visual Representation

| Platform | Unowned Harbor | Owned Harbor |
|----------|----------------|--------------|
| **Blazor** | Circle stroke: `#2a5d8f` (neutral blue) | Circle stroke: player's foreground color |
| **React** | Transparent background (water shows through) | Hex background: player's gradient |

### React Implementation

The `HarborHexContent` component renders the harbor hex. When `harbor.owner` is set:

1. Fill the harbor hex background with the owner's player gradient
2. The gradient uses `primary → secondary` (2-stop, matching other player elements)
3. Dock triangle and harbor circle render on top of the gradient background

```typescript
// In GameBoard.tsx - pass owner colors to HarborHexContent
interface HarborHexContentProps {
  harbor: HarborModel;
  ownerColors?: PlayerColors | null;  // From harbor.owner.colors
}

// Render gradient background when owned
{ownerColors && (
  <polygon
    points="100,0 75,43.3 100,86.6 0,86.6 25,43.3 0,0"  // Full hex
    fill={`url(#owner-gradient-${ownerColors.primary})`}
    opacity={0.6}
  />
)}
```

### Ownership Animation

When a harbor becomes owned (player places a settlement at an adjacent vertex), the ownership background animates with an **expand + fade** effect:

**Animation Properties:**
- **Type:** Scale up from center + fade in
- **Duration:** 600ms
- **Easing:** ease-out (fast start, slow finish)
- **Scale:** 0.3 → 1.0 (expand outward from hex center)
- **Opacity:** 0 → 0.7 (fade in as it expands)

**Implementation:**

```typescript
// CSS keyframes embedded in SVG <style> element
@keyframes harbor-owned {
  from {
    transform: scale(0.3);
    opacity: 0;
  }
  to {
    transform: scale(1);
    opacity: 0.7;
  }
}

.harbor-owned-bg {
  animation: harbor-owned 0.6s ease-out forwards;
  transform-origin: 50px 43.3px;  // Center of viewBox (100x86.6)
}
```

The animation creates a "ripple" visual effect that draws attention to the ownership change while integrating smoothly with the existing dock and harbor circle elements.

### Harbor Implementation Checklist

- [x] HarborModel includes `owner` property (auto-generated TypeScript)
- [x] Pass harbor owner colors to HarborHexContent
- [x] Render player gradient background when harbor is owned
- [x] Gradient uses 2-stop SVG pattern (primary → secondary)
- [x] Animate ownership background with expand + fade effect (600ms)

---

## Key Code References

- **GameState enum:** `Catan3.Shared/Models/GameEnums.cs:34-99`
- **State transitions:** `Catan3.Shared/GameLogic/GameStateMachine.cs:1156-1350`
- **AllowNext logic:** `GameStateMachine.cs:1097-1108`
- **SetActionFlags:** `GameStateMachine.cs:1091-1096`
- **ActionFlags type:** `react-ui/types/generated/models/action-flags.ts`
- **GameServiceProxy:** `react-ui/lib/services/GameServiceProxy.ts`
- **React game page:** `react-ui/app/game/[id]/page.tsx`
- **Blazor GoFirst:** `WebUI/Pages/Game.razor:1182-1195`
- **Blazor PlayerCard flip:** `WebUI/Components/Players/PlayerCard.razor:19-27`
- **Harbor ownership logic:** `Catan3.Shared/Models/HarborModel.cs:187-250`
- **Harbor ownership assignment:** `Catan3.Shared/GameLogic/GameStateMachine.cs:1615-1622`

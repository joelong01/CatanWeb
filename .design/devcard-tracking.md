# Development Card Tracking Design

**Status:** Implemented
**Created:** 2026-01-15
**Implemented:** 2026-01-15

## Overview

Add development card (dev card) purchase tracking to the Catan game. This feature follows the
existing entitlement purchase pattern and adds UI for purchasing dev cards, displaying counts per
player, and entering Victory Point (VP) dev cards at game end.

## Goals

1. **Purchase Dev Cards** - Players can buy dev cards using the existing entitlement system
2. **Track Dev Card Count** - Display how many dev cards each player has purchased (public count)
3. **Enter Victory Points** - At game end, players manually enter their VP dev card count
4. **Maintain Compatibility** - Desktop app continues to work (new properties have safe defaults)
5. **Support Recording** - Dev card purchases are recorded and replay correctly

## User Decisions

| Decision | Choice | Rationale |
| ----------|--------|----------- |  
| Visibility | Public count | Matches physical game (you can count someone's dev cards) |
| VP Tracking | Manual entry at game end | Simpler than tracking card types during game |
| Purchase Layout | 3x2 grid | Accommodates 5th purchaseable item naturally |
| Icon Style | Question mark card | Clear representation of mystery/unknown card |

## Current System Analysis

### Entitlement Purchase Flow

The existing system uses three message types for purchases:

| Message | Purpose | Example |
| ---------|---------|--------- |
| `PurchaseMessage` | Generic purchases (Soldiers, upgrades) | Buy a soldier card |
| `RoadPurchaseMessage` | Place road on specific location | Build road at key X |
| `BuildingUpgradeMessage` | Place/upgrade buildings | Build settlement, upgrade to city |

**Key flow for `PurchaseMessage`:**

1. Client sends `PurchaseMessage(Entitlement.Soldier)` via SignalR
2. `GameStateMachine.HandlePurchaseAsync()` validates and processes
3. `OnPurchase()` checks game state, validates purchase, adds to `UnspentEntitlements`
4. `ConsumeEntitlement()` moves from unspent to spent when used
5. `UpdatePurchaseUi()` enables/disables purchase buttons

### Current Purchase UI

- **Component:** `PurchaseButton.razor` with flip animation
- **Layout:** 2x2 grid (Road, Settlement, City, Soldier)
- **Entitlements:** Defined in `RegularBoardInfo.GetPurchaseableEntitlements()`

### Player Stat Display

- **Component:** `PlayerTile.razor` with 12-stat grid
- **Grid:** `grid-template-columns: repeat(12, 35px)`
- **Stats:** Score, Roads, Cities, Settlements, Soldiers, Robber, Targeted, Total, LongestRoad,
  GoodRolls, BadRolls, Stars

### Winner Declaration Flow

- **State:** `GameState.GameOver` when winner declared
- **UI:** `PlayerCard.razor` supports flip modes (`CardFlipMode.GoFirst`, `CardFlipMode.Supplemental`)
- **Pattern:** Cards flip to show alternate content (buttons, checkboxes)

## Design

### 1. Model Updates

**DevCards Purchased - No new property needed!**

Use the existing entitlement tracking pattern (same as Soldiers):

```csharp
// How soldiers are counted (PlayerTile.razor:269-270)
private int GetSoldiersPlayed() =>
    Player.SpentEntitlementsThisGame.Count(e => e == Entitlement.Soldier);

// DevCards will use identical pattern
private int GetDevCardsPurchased() =>
    Player.SpentEntitlementsThisGame.Count(e => e == Entitlement.DevCard);
```

**PlayerModel.cs** - Add ONE new property for VP entry at game end:

```csharp
/// <summary>
/// Gets or sets the Victory Point card count entered at game end.
/// This is manually entered when the winner is declared, as VP dev cards are kept secret.
/// </summary>
[ObservableProperty]
public partial int VictoryPointCards { get; set; } = 0;
```

**Why this approach:**

- **DevCards**: Tracked via `SpentEntitlementsThisGame` (follows Soldier pattern exactly)
- **VictoryPointCards**: New property required - this is manual entry at game end, not an entitlement

### 2. Entitlement System Integration

**GameEnums.cs** - `Entitlement.DevCard` already exists (line 107-108).

**RegularBoardInfo.cs** - Add `Entitlement.DevCard` to purchaseable list:

```csharp
public static List<Entitlement> GetPurchaseableEntitlements()
{
    return new List<Entitlement>
    {
        Entitlement.Road,
        Entitlement.Settlement,
        Entitlement.City,
        Entitlement.Soldier,
        Entitlement.DevCard  // NEW
    };
}
```

**GameStateMachine.cs** - Handle DevCard in purchase flow:

```csharp
// In ValidatePurchase():
Entitlement.DevCard => true,  // No max limit for dev cards

// In OnPurchase() - follows same pattern as other entitlements:
// 1. Add to UnspentEntitlements (purchased but not "used")
// 2. Immediately consume it (move to SpentEntitlementsThisGame)
// This matches how the count is tracked via SpentEntitlementsThisGame
```

### 3. Purchase UI Update

**Game.razor / Game.razor.css** - Change grid from 2x2 to 3x2:

```css
.purchase-grid {
    display: grid;
    grid-template-columns: repeat(3, 1fr);  /* Was repeat(2, 1fr) */
    grid-template-rows: repeat(2, 1fr);
    gap: 8px;
}
```

**Layout:**

```text
┌──────────┬──────────┬──────────┐
│   Road   │Settlement│   City   │
├──────────┼──────────┼──────────┤
│  Soldier │ Dev Card │  (empty) │
└──────────┴──────────┴──────────┘
```

**PurchaseButton.razor** - Add DevCard support:

- Reuse card-back image with CSS "?" overlay for icon
- Label: "Dev Card"
- Count: Show count from `SpentEntitlementsThisGame` (no "of Max" since unlimited)

### 4. Player Tile Stat Addition

**PlayerTile.razor** - Add DevCards as 13th stat:

```csharp
// Add to CatanGlyph class
public const string DevCard = "?";  // Simple text glyph

// Add helper method (follows GetSoldiersPlayed pattern)
private int GetDevCardsPurchased() =>
    Player.SpentEntitlementsThisGame.Count(e => e == Entitlement.DevCard);

// Add to GetPlayerStats() after Soldiers (index 4)
new() { Name = "DevCards", Glyph = CatanGlyph.DevCard, Count = GetDevCardsPurchased(),
        IsHighlighted = false },
```

**PlayerTile.razor.css** - Expand grid to 13 columns:

```css
.player-stats-grid {
    grid-template-columns: repeat(13, var(--stat-size, 35px));  /* Was 12 */
}
```

**PlayerCard.razor.css** - Increase card width (~36px wider):

```css
/* Adjust from 500px to ~540px to accommodate 13th stat */
```

### 5. Victory Point Entry at Game End

**PlayerCard.razor** - Add new flip mode:

```csharp
public enum CardFlipMode
{
    None,
    GoFirst,
    Supplemental,
    VictoryPoints  // NEW - enter VP count at game end
}
```

**VP Entry UI (card back):**

```html
<div class="card-back card-back-vp">
    <div class="vp-input-area">
        <label>Victory Point Cards:</label>
        <input type="number" min="0" max="5" value="@Player.VictoryPointCards"
               @onchange="OnVPChanged" />
    </div>
    <div class="player-avatar"><!-- avatar --></div>
    <div class="player-name">@Player.Name</div>
</div>
```

**PlayersPanel.razor** - Flip cards during VP entry phase (before GameOver, after winner confirmed):

```csharp
// IsVPEntryPhase parameter from parent controls when to show VP input
if (IsVPEntryPhase)
{
    // Only flip cards for players who have dev cards
    var devCardCount = player.SpentEntitlementsThisGame.Count(e => e == Entitlement.DevCard);
    if (devCardCount > 0)
    {
        return CardFlipMode.VictoryPoints;
    }
}
```

**Local VP Storage** - VPs are stored locally in `_localVPEntries` dictionary until Done clicked:

```csharp
private Dictionary<string, int> _localVPEntries = new();

private void HandleLocalVPChange((string PlayerId, int VictoryPoints) args)
{
    _localVPEntries[args.PlayerId] = args.VictoryPoints;
}
```

**Done Button** - Add "Done" button (like PickSupplementalPlayers) to finalize VP entry and submit all VPs in single DeclareWinner API call.

### 6. VP Entry Flow (Game.razor)

**State Management** - Use `_pendingWinnerId` to track winner before API call:

```csharp
private string? _pendingWinnerId = null;
private bool IsVPEntryPhase => _pendingWinnerId != null;
```

**Flow:**
1. User clicks "Declare Winner" → shows confirmation dialog
2. User confirms → `ConfirmWinner()` sets `_pendingWinnerId`, triggers animation
3. Animation completes → VP entry UI appears (for players with dev cards)
4. User enters VP counts → stored locally in PlayersPanel
5. User clicks "Done" → `OnVictoryPointsDone()` sends DeclareWinner API with all VPs
6. API success → `_pendingWinnerId = null`, game transitions to GameOver

**API Call:**
```csharp
private async Task OnVictoryPointsDone(Dictionary<string, int> victoryPoints)
{
    var request = new DeclareWinnerRequest
    {
        WinnerId = _pendingWinnerId,
        VictoryPoints = victoryPoints
    };
    var response = await Http.PostAsJsonAsync(url, request);
    if (response.IsSuccessStatusCode) _pendingWinnerId = null;
}
```

### 7. Score Calculation Update

**GameStateMachine.cs** - Update `UpdateScore()`:

```csharp
// Current formula:
Score = (CitiesPlayed × 2) + SettlementsPlayed +
        (HasLongestRoad ? 2 : 0) +
        (LargestArmy ? 2 : 0);

// New formula:
Score = (CitiesPlayed × 2) + SettlementsPlayed +
        (HasLongestRoad ? 2 : 0) +
        (LargestArmy ? 2 : 0) +
        VictoryPointCards;  // NEW
```

### 8. Recording Support

Dev card purchases use existing `PurchaseMessage` infrastructure:

- **Recording:** `PurchaseMessage(Entitlement.DevCard)` is recorded like Soldier purchases
- **Replay:** Same message replays correctly
- **VP Entry:** VPs are included in `DeclareWinnerMessage` as a dictionary (no separate message)

## Files to Modify

| File | Changes |
|------|---------|
| `Catan3.Shared/Models/PlayerModel.cs` | Add `VictoryPointCards` property |
| `Catan3.Shared/Models/RegularBoardInfo.cs` | Add DevCard to purchaseable entitlements |
| `Catan3.Shared/Models/ExpansionBoardInfo.cs` | Add DevCard to purchaseable entitlements |
| `Catan3.Shared/Models/MessageObjects.cs` | Update `DeclareWinnerMessage` with VP dictionary |
| `Catan3.Shared/Models/RecordedMessage.cs` | Update `DeclareWinnerRecord` with VP dictionary |
| `Catan3.Shared/GameLogic/GameStateMachine.cs` | DevCard purchase handling, VP processing in HandleDeclareWinnerAsync |
| `Catan3.Shared/Extensions/GameModelExtensions.cs` | Document hash exclusion for SpentEntitlementsThisGame |
| `Catan3.GameService/Controllers/GameApiController.cs` | Pass VPs to DeclareWinnerMessage |
| `Catan3.GameService/Controllers/RecordingController.cs` | Pass VPs during replay |
| `WebUI/Pages/Game.razor` | 3x2 purchase grid, IsVPEntryPhase state, VP submission handler |
| `WebUI/Pages/Game.razor.css` | Grid layout update |
| `WebUI/Components/Shared/PurchaseButton.razor` | DevCard icon and label |
| `WebUI/Components/Players/PlayerTile.razor` | Add DevCards stat (13th column) |
| `WebUI/Components/Players/PlayerTile.razor.css` | 13-column grid |
| `WebUI/Components/Players/PlayerCard.razor` | VictoryPoints flip mode with input UI |
| `WebUI/Components/Players/PlayerCard.razor.css` | Wider card, VP entry styling |
| `WebUI/Components/Players/PlayersPanel.razor` | IsVPEntryPhase logic, local VP storage, Done button |

## Edge Cases

1. **No dev cards purchased** - Don't show VP entry for players with 0 dev cards
2. **VP entry skipped** - If player doesn't enter VP, default is 0 (no score change)
3. **Desktop compatibility** - New properties default to 0, desktop ignores them
4. **Re-entering VP** - Allow changing VP count until "Done" clicked
5. **Winner change** - Recalculate `HighestScore` flag after VP entry

## Testing Plan

1. **Purchase Flow**
   - Buy dev card via purchase button
   - Verify `SpentEntitlementsThisGame` contains `Entitlement.DevCard`
   - Verify stat tile shows correct count

2. **Recording/Replay**
   - Record game with dev card purchases
   - Replay and verify purchases execute correctly

3. **VP Entry**
   - Declare winner with dev cards present
   - Enter VP counts for multiple players
   - Verify scores update correctly
   - Verify winner may change based on VPs

4. **Layout**
   - Test 3x2 purchase grid in landscape and portrait
   - Test 13-stat player tile fits correctly
   - Test VP entry UI appearance

## Open Questions (Resolved)

1. **Resource cost** - The game doesn't track resource spending for purchases (physical cards are
   used). This feature only tracks that a purchase happened, not resource validation.

2. **Dev card icon** - Used `StatDevCards` asset from AssetName enum.

3. **VP entry timing** - Implemented: VP entry appears immediately when winner is declared, for all
   players with dev cards. Cards flip to show VP input field.

## Architecture Notes

- **GameModel is single source of truth** - Dev card count tracked via `SpentEntitlementsThisGame`,
  VictoryPointCards stored in PlayerModel, all synced via SignalR
- **Follows existing patterns** - Uses same entitlement tracking as Soldier (count entries in
  `SpentEntitlementsThisGame`), same flip mode pattern as Supplemental
- **Desktop compatibility** - New `VictoryPointCards` property defaults to 0, desktop ignores it

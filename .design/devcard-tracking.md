# Development Card Tracking

**Last verified:** January 30, 2026

## Overview

Development card purchase tracking extends the entitlement system
to count dev card purchases per player, display them in the player
stats grid, and allow Victory Point card entry at game end.

**Status:** Fully implemented (completed January 15, 2026).

## Model Changes

### PlayerModel

```csharp
// Catan3.Shared/Models/PlayerModel.cs
[ObservableProperty]
public partial int VictoryPointCards { get; set; } = 0;
```

Victory Point cards are manually entered at game end because VP dev
cards are kept secret during play.

### Entitlement.DevCard

```csharp
// Catan3.Shared/Models/GameEnums.cs
[Description("Dev Card")]
DevCard,
```

Added to both `RegularBoardInfo.PurchaseableEntitlements` and
`ExpansionBoardInfo.PurchaseableEntitlements` alongside City,
Settlement, Road, and Soldier.

## Purchase Behavior

Dev card purchases differ from other entitlements:

| Entitlement | Flow |
|-------------|------|
| Road, Settlement, City | Purchase -> UnspentEntitlements -> place on board -> SpentEntitlementsThisGame |
| Soldier | Purchase -> UnspentEntitlements -> play card -> SpentEntitlementsThisGame |
| **DevCard** | Purchase -> **SpentEntitlementsThisGame directly** (no placement phase) |

This means the purchase button's front face count shows
`SpentCount` (total purchased) rather than `UnspentCount`.

## Purchase Grid

The purchase UI changed from 2x2 to 3x2 grid to accommodate
the fifth purchaseable item:

```
[ Road       ] [ Settlement ] [ City    ]
[ Soldier    ] [ Dev Card   ] [         ]
```

**CSS:** `grid-template-columns: repeat(3, 1fr)` in both landscape
and portrait modes.

## Player Stats Display

Dev cards appear as the 6th stat in the 13-column player tile grid:

| Stat | Glyph | Source |
|------|-------|--------|
| Score | Laurel | `Player.Score` |
| Roads | Road | `SpentEntitlementsThisGame.Count(Road)` |
| Cities | City | `SpentEntitlementsThisGame.Count(City)` |
| Settlements | Settlement | `SpentEntitlementsThisGame.Count(Settlement)` |
| Soldiers | Soldier | `SpentEntitlementsThisGame.Count(Soldier)` |
| **DevCards** | **?** | **`SpentEntitlementsThisGame.Count(DevCard)`** |
| Robber | Pirate | `ResourcesLost` |
| Targeted | Target | `TimesTargeted` |
| Total | Sum | Total resources |
| LongestRoad | LongestRoad | `Player.LongestRoad` |
| ... | ... | ... |

The DevCard glyph uses `"?"` as a placeholder since the Catan font
does not include a dev card icon.

## Winner Declaration with VP Entry

### DeclareWinnerMessage

```csharp
public class DeclareWinnerMessage
{
    public string WinnerId { get; set; }
    public Dictionary<string, int> VictoryPoints { get; set; }
}
```

The `VictoryPoints` dictionary maps player IDs to their VP card
counts. Only players who purchased dev cards need VP entry.

### Processing Flow

1. Current player clicks "Declare Winner"
2. If any players have dev cards, VP entry UI appears
3. Players reveal and count VP cards
4. `DeclareWinnerMessage` sent with winner ID and VP counts
5. `HandleDeclareWinnerAsync()` applies VP counts to `PlayerModel`
6. `UpdateScore()` recalculates all scores including VP cards
7. Game transitions to `GameOver`

### Score Formula

```
score = settlements_placed * 1
      + cities_placed * 2
      + (hasLongestRoad ? 2 : 0)
      + (hasLargestArmy ? 2 : 0)
      + victoryPointCards
```

## Hash Exclusion

`SpentEntitlementsThisGame` and `VictoryPointCards` are intentionally
excluded from `GameHash` calculation to maintain backward
compatibility with existing game recordings. These values are
tracked via state transitions and player data serialization.

## Implementation Files

| File | Change |
|------|--------|
| `PlayerModel.cs` | Added `VictoryPointCards` property |
| `GameEnums.cs` | Added `Entitlement.DevCard` |
| `RegularBoardInfo.cs` | Added DevCard to purchaseable entitlements |
| `ExpansionBoardInfo.cs` | Added DevCard to purchaseable entitlements |
| `MessageObjects.cs` | Extended `DeclareWinnerMessage` with VP dictionary |
| `GameStateMachine.cs` | Purchase handling, VP application, score update |
| `PurchaseButton.razor` | DevCard-specific count display logic |
| `PlayerTile.razor` | Added DevCards stat column |
| `PlayerTile.razor.css` | Updated to 13-column grid |
| `Game.razor.css` | 3x2 purchase grid layout |

# Winner Declaration System

## Overview

The winner declaration system allows the current player to declare victory, update lifetime statistics,
archive the game for future review, and display a celebration animation. The game remains playable
(undo/redo available) until explicitly deleted.

## User Flow

1. Current player clicks "Winner!" button in NavMenu
2. Confirmation dialog appears: "Declare {name} as the winner and end the game?"
3. On "Yes":
   - LifetimeStats updated for all players in database
   - Game archived to CompletedGames table
   - GameState set to GameOver
   - Celebration animation displays (5 seconds)
   - Game stays in memory - undo/redo still works
4. On "No": Dialog dismissed, game continues
5. User deletes game via existing Delete mechanism when done

## Catan Rule Constraint

Only the **current player** can declare victory. This is a Catan rule - you can only win on your turn.
No player picker is needed; the winner is implicitly `GameModel.CurrentPlayerId`.

## Architecture

### Component Flow

```text
NavMenu.razor          → "Winner!" button click
    ↓
Game.razor             → Show confirmation dialog
    ↓ (on Yes)
Game.razor             → HTTP POST to API
    ↓
GameApiController      → POST /api/game/{gameId}/winner
    ↓
    ├─→ UpdatePlayerLifetimeStats()  → Update PlayerEntity.Data (JSON)
    ├─→ ArchiveCompletedGame()       → Insert CompletedGameEntity
    └─→ HandleDeclareWinnerAsync()   → Set GameState.GameOver
    ↓
SignalR                → Broadcast GameStateUpdated
    ↓
Game.razor             → Show celebration, update UI
```

### REST API Endpoint

```text
POST /api/game/{gameId}/winner
Content-Type: application/json

Request:
{
  "winnerId": "player-id"
}

Response (success):
{
  "success": true,
  "message": "Winner recorded: Joe",
  "gameId": "xxx-xxx",
  "winnerId": "joe-001",
  "winnerName": "Joe"
}

Response (errors):
- 404: Game not found (GAME_NOT_FOUND)
- 400: Game already over (GAME_ALREADY_OVER)
- 403: Not current player (NOT_CURRENT_PLAYER)
```

## Database Schema

### New Table: CompletedGames

Archives finished games for future review and statistics.

```sql
CREATE TABLE CompletedGames (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    GameId          TEXT NOT NULL,           -- Original game ID
    GameName        TEXT NOT NULL,
    WinnerId        TEXT NOT NULL,           -- Winner player ID
    WinnerName      TEXT NOT NULL,           -- Winner name for display
    CompletedAt     DATETIME NOT NULL,       -- When winner was declared
    StartedAt       DATETIME NOT NULL,       -- Original game start time
    PlayerCount     INTEGER NOT NULL,
    PlayerNames     TEXT NOT NULL,           -- Comma-separated
    TurnCount       INTEGER NOT NULL,        -- Number of turns played
    CompressedData  BLOB NOT NULL,           -- Full .catan format
    Size            INTEGER NOT NULL         -- Size in bytes
);

CREATE INDEX IX_CompletedGames_GameId ON CompletedGames(GameId);
CREATE INDEX IX_CompletedGames_WinnerId ON CompletedGames(WinnerId);
CREATE INDEX IX_CompletedGames_CompletedAt ON CompletedGames(CompletedAt);
```

### Existing Table Updates

**PlayerEntity.Data** (JSON) contains `PlayerProfile.LifetimeStats`:

```json
{
  "Id": "joe-001",
  "Name": "Joe",
  "Colors": { "Primary": "#...", "Secondary": "#...", "Foreground": "#..." },
  "LifetimeStats": {
    "GamesPlayed": 10,
    "Wins": 3,
    "LongestRoadWins": 2,
    "LargestArmyWins": 1,
    "LongestRoadRecord": 12,
    "MostSoldiersRecord": 8,
    "MostStarsRecord": 42,
    "HighestScoreRecord": 14,
    "Totals": {
      "ResourcesCollected": 150,
      "ResourcesLostToRobber": 18,
      "RoadsBuilt": 45,
      "SettlementsBuilt": 20,
      "CitiesBuilt": 12,
      "SoldiersPlayed": 15,
      "TimesTargeted": 22,
      "GoodRolls": 85,
      "BadRolls": 40,
      "StarsEarned": 120
    }
  }
}
```

## Statistics Update Logic

On winner declaration, for **each player** in the game:

### Step 1: Read

Load `PlayerProfile.LifetimeStats` from `PlayerEntity.Data` in database.

### Step 2: Capture GameStats from GameModel

Extract per-game statistics from the current GameModel state:

| GameStats Field | Source | Description |
|-----------------|--------|-------------|
| ResourcesCollected | `player.ResourcesThisGame.Count` | Total resources earned (excluding Robber) |
| ResourcesLostToRobber | `player.ResourcesThisGame.Robber` | Cards discarded to robber |
| RoadsBuilt | `GameModel.Roads.Count(owner == player.Id)` | Roads placed this game |
| SettlementsBuilt | `GameModel.Buildings.Count(Settlement, owner)` | Settlements placed |
| CitiesBuilt | `GameModel.Buildings.Count(City, owner)` | Cities upgraded |
| SoldiersPlayed | `player.SpentEntitlementsThisGame.Count(Soldier)` | Knight cards played |
| TimesTargeted | `player.TimesTargeted` | Robber targets |
| GoodRolls | `player.GoodRolls` | Productive rolls |
| BadRolls | `player.BadRolls` | Unproductive rolls |
| StarsEarned | `player.Stars` | Stars this game |

### Step 3: Update LifetimeStats

Apply aggregation rules to update lifetime statistics:

| LifetimeStats Field | Update Rule | Type |
|---------------------|-------------|------|
| GamesPlayed | `+= 1` | Count |
| Wins | `+= 1` if winner | Count |
| WinRate | `Wins / GamesPlayed * 100` | Calculated |
| LongestRoadWins | `+= 1` if `player.HasLongestRoad` | Count |
| LargestArmyWins | `+= 1` if `player.LargestArmy` | Count |
| LongestRoadRecord | `Max(current, player.LongestRoad)` | Max |
| MostSoldiersRecord | `Max(current, soldiersPlayed)` | Max |
| MostStarsRecord | `Max(current, player.Stars)` | Max |
| HighestScoreRecord | `Max(current, player.Score)` | Max |
| Totals | `+= gameStats` (uses `operator+`) | Aggregate |

### Step 4: Save

Persist updated `PlayerProfile` back to `PlayerEntity.Data` in database.

### AddGame Implementation

```csharp
public LifetimeStats AddGame(
    GameStats gameStats,
    bool won,
    bool hasLongestRoad,
    bool hasLargestArmy,
    int roadLength,
    int soldiersPlayed,
    int stars,
    int score) => this with
{
    GamesPlayed = GamesPlayed + 1,
    Wins = won ? Wins + 1 : Wins,
    LongestRoadWins = hasLongestRoad ? LongestRoadWins + 1 : LongestRoadWins,
    LargestArmyWins = hasLargestArmy ? LargestArmyWins + 1 : LargestArmyWins,
    LongestRoadRecord = Math.Max(LongestRoadRecord, roadLength),
    MostSoldiersRecord = Math.Max(MostSoldiersRecord, soldiersPlayed),
    MostStarsRecord = Math.Max(MostStarsRecord, stars),
    HighestScoreRecord = Math.Max(HighestScoreRecord, score),
    Totals = Totals + gameStats
};
```

## UI Components

### Winner Button (NavMenu)

Location: Within Game context menu

```razor
<button class="nav-menu-item" @onclick="OnWinner">
    <div class="nav-icon">&#x1F3C6;</div>
    <div class="nav-label">Winner!</div>
</button>
```

### Confirmation Dialog

Modal overlay with centered dialog box:

- **Title**: "Declare Winner"
- **Message**: "Declare {CurrentPlayerFirstName} as the winner and end the game?"
- **Buttons**: "Yes" (green, confirms) / "No" (gray, cancels)
- **Backdrop**: Click to cancel

### Celebration Animation

Fullscreen overlay with:

- **Trophy**: Large emoji (&#x1F3C6;) with bounce animation
- **Text**: "{WinnerName} Wins!" in gold with pulse animation
- **Confetti**: 50 randomized falling pieces with varied colors, positions, delays
- **Duration**: 5 seconds, then auto-dismiss

### CSS Animations

```css
@keyframes trophyBounce {
    0%, 100% { transform: translateY(0); }
    50% { transform: translateY(-20px); }
}

@keyframes textPulse {
    0%, 100% { transform: scale(1); }
    50% { transform: scale(1.05); }
}

@keyframes confettiFall {
    0% { transform: translateY(0) rotate(0deg); opacity: 1; }
    100% { transform: translateY(100vh) rotate(720deg); opacity: 0; }
}
```

## Game State After Winner

### What Changes

- `GameState = GameState.GameOver`
- Celebration displayed
- Stats persisted to database
- Game archived to CompletedGames

### What Stays the Same

- Game remains in `GameStateMachineRegistry`
- `ActionFlags.UndoEnabled` remains based on history
- `ActionFlags.RedoEnabled` remains based on history
- User can undo/redo if desired (accepts data inconsistency)
- User deletes game when done via existing mechanism

## Edge Cases

### Already GameOver

If user tries to declare winner on a game already in GameOver state:

- Returns 400 error
- UI should disable Winner button when GameState == GameOver

### Undo After Winner

User can undo after declaring winner:

- Game returns to previous state
- **Stats are NOT rolled back** (user accepts this)
- CompletedGames archive remains

### Multiple Winners

Not possible - only one winner can be declared:

- Once GameState == GameOver, Winner button should be disabled
- API rejects subsequent calls

## Files Modified

| File | Purpose |
| ---- | ------- |
| `Catan3.GameService/Data/CatanDbContext.cs` | Add CompletedGames DbSet |
| `Catan3.GameService/Data/Entities/CompletedGameEntity.cs` | New entity (created) |
| `Catan3.Shared/Models/MessageObjects.cs` | DeclareWinnerMessage class |
| `Catan3.Shared/GameLogic/GameStateMachine.cs` | HandleDeclareWinnerAsync |
| `Catan3.GameService/Controllers/GameApiController.cs` | Endpoint + helpers |
| `WebUI/Layout/NavMenu.razor` | Winner button |
| `WebUI/Pages/Game.razor` | Dialog + celebration + handlers |
| `WebUI/Pages/Game.razor.css` | Modal + animation styles |

## Wins Display in New Game

The New Game player picker shows each player's lifetime wins count in a gold badge positioned in
the lower-right corner of the player card. This provides quick visibility into player experience
and win history.

### UI Element

```razor
@if ((player.LifetimeStats?.Wins ?? 0) > 0)
{
    <div class="wins-badge" title="@player.LifetimeStats?.Wins wins">
        @player.LifetimeStats?.Wins
    </div>
}
```

### Styling

```css
.wins-badge {
    position: absolute;
    bottom: 5px;
    right: 5px;
    background: linear-gradient(135deg, #ffd700 0%, #b8860b 100%);
    color: #1a1a1a;
    min-width: 22px;
    height: 22px;
    padding: 0 6px;
    border-radius: 11px;
    font-size: 12px;
    font-weight: bold;
}
```

### Files for Wins Badge

| File | Purpose |
| ---- | ------- |
| `WebUI/Pages/NewGame.razor` | Add wins badge to player cards |
| `WebUI/Pages/NewGame.razor.css` | Badge styling |

## Stats Page

The Stats page displays lifetime statistics for all players, providing a leaderboard view and
detailed statistics breakdown.

### Navigation

- Accessible from NavMenu → "Stats" button (available on all pages)
- Route: `/stats`

### UI Layout

Table format with players as rows and stats as columns. Each stat has **Max** (best single game)
and **Ave** (average per game) variants where applicable.

```text
┌───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│  Player Statistics                                                                                                     │
├────────────┬───────┬──────┬────────┬────────┬─────────────┬─────────────┬───────────┬───────────┬──────────┬──────────┤
│ Player     │ Games │ Wins │ L.Road │ L.Army │ Longest Road│ Most Soldiers│ Most Stars│ Ave Stars │ Targeted │ Robber   │
├────────────┼───────┼──────┼────────┼────────┼─────────────┼─────────────┼───────────┼───────────┼──────────┼──────────┤
│ [Av] Joe   │  15   │  5   │   3    │   2    │     12      │      8      │    42     │    2.8    │    28    │    15    │
│ [Av] Doug  │  12   │  4   │   2    │   3    │     10      │      9      │    38     │    3.2    │    22    │    12    │
│ [Av] Sarah │  10   │  3   │   1    │   1    │      9      │      6      │    30     │    3.0    │    18    │     8    │
└────────────┴───────┴──────┴────────┴────────┴─────────────┴─────────────┴───────────┴───────────┴──────────┴──────────┘
```

**Column Legend:**

- **L.Road**: Times won Longest Road bonus at game end
- **L.Army**: Times won Largest Army bonus at game end
- **Longest Road**: Longest road ever achieved (Max record)
- **Most Soldiers**: Most soldiers played in a single game (Max record)
- **Most Stars**: Most stars earned in a single game (Max record)
- **Ave Stars**: Average stars per game (Totals.StarsEarned / GamesPlayed)
- **Targeted**: Total times targeted by robber (Aggregate)
- **Robber**: Total resources lost to robber (Aggregate)

### In-Game Statistics (PlayerModel Reference)

The `PlayerModel` (`Catan3.Shared/Models/PlayerModel.cs`) tracks these statistics during gameplay:

| Field | Type | Description | Displayed In |
| ----- | ---- | ----------- | ------------ |
| Score | int | Current victory points | PlayerTile (laurel) |
| LongestRoad | int | Current road length | PlayerTile |
| HasLongestRoad | bool | Currently holds Longest Road bonus | PlayerTile (highlighted) |
| LargestArmy | bool | Currently holds Largest Army bonus | PlayerTile (highlighted) |
| TimesTargeted | int | Times robber targeted this player | PlayerTile |
| GoodRolls | int | Rolls that produced resources | PlayerTile |
| BadRolls | int | Rolls that didn't produce resources | PlayerTile |
| GoldRolls | int | Gold mine rolls | Not displayed |
| Stars | int | Stars earned this game | PlayerTile |
| MaxNoResourceRolls | int | Max consecutive no-resource rolls | Not displayed |
| NoResourceCount | int | Current no-resource streak | Not displayed |
| HighestScore | bool | Currently has highest score | PlayerTile (highlighted) |
| ResourcesThisGame | ResourcesModel | Cumulative resources collected | PlayerTile (Total) |
| ResourcesThisGame.Robber | int | Resources lost to robber | PlayerTile (Robber) |
| SpentEntitlementsThisGame | List | Cards played (Soldiers, etc.) | PlayerTile (Soldiers) |

**Derived Stats (calculated from GameModel):**

| Stat | Calculation | Displayed In |
| ---- | ----------- | ------------ |
| Roads Built | GameModel.Roads.Count(owner) | PlayerTile |
| Cities Built | GameModel.Buildings.Count(City) | PlayerTile |
| Settlements Built | GameModel.Buildings.Count(Settlement) | PlayerTile |
| Soldiers Played | SpentEntitlementsThisGame.Count(Soldier) | PlayerTile |

### Statistics Design Philosophy

**What's fun and interesting to report on?**

1. **Victory Achievements**: Wins, win rate, times won Longest Road, times won Largest Army
2. **Records**: Longest road ever, most soldiers ever, most stars ever, highest score ever
3. **Cumulative Totals**: Resources collected, buildings built, soldiers played
4. **Misfortune Stats**: Times targeted, resources lost to robber, bad rolls
5. **Luck Stats**: Good rolls, gold rolls, stars earned

### Proposed Statistics Model

**GameStats** (`Catan3.Shared/PlayerProfile/GameStats.cs`) - Per-game statistics:

| Field | Type | Source | Description |
| ----- | ---- | ------ | ----------- |
| ResourcesCollected | int | ResourcesThisGame.Count | Total resources earned |
| ResourcesLostToRobber | int | ResourcesThisGame.Robber | Cards discarded to robber |
| RoadsBuilt | int | GameModel.Roads | Roads placed |
| SettlementsBuilt | int | GameModel.Buildings | Settlements placed |
| CitiesBuilt | int | GameModel.Buildings | Cities upgraded |
| SoldiersPlayed | int | SpentEntitlementsThisGame | Knight cards played |
| TimesTargeted | int | PlayerModel.TimesTargeted | Robber targets |
| GoodRolls | int | PlayerModel.GoodRolls | Productive rolls |
| BadRolls | int | PlayerModel.BadRolls | Unproductive rolls |
| GoldRolls | int | PlayerModel.GoldRolls | Gold mine rolls |
| StarsEarned | int | PlayerModel.Stars | Stars this game |
| FinalScore | int | PlayerModel.Score | End-game score |
| RoadLength | int | PlayerModel.LongestRoad | Final road length |
| WonLongestRoad | bool | PlayerModel.HasLongestRoad | Had bonus at game end |
| WonLargestArmy | bool | PlayerModel.LargestArmy | Had bonus at game end |

**LifetimeStats** (`Catan3.Shared/PlayerProfile/LifetimeStats.cs`) - Aggregated across all games:

| Field | Type | Aggregation | Description |
| ----- | ---- | ----------- | ----------- |
| GamesPlayed | int | Count | Total games completed |
| Wins | int | Count | Total victories |
| WinRate | double | Calculated | Wins / GamesPlayed * 100 |
| LongestRoadWins | int | Count | Times won Longest Road bonus |
| LargestArmyWins | int | Count | Times won Largest Army bonus |
| LongestRoadRecord | int | Max | Highest road length ever |
| MostSoldiersRecord | int | Max | Most soldiers in one game |
| MostStarsRecord | int | Max | Most stars in one game |
| HighestScoreRecord | int | Max | Highest final score ever |
| Totals | GameStats | Sum | Aggregated game stats |

### Stats Page Columns (Proposed)

Each stat supports **Max** (best single game record) and **Ave** (average per game) where applicable.

| Column | Source | Type | Description |
| ------ | ------ | ---- | ----------- |
| Player | PlayerProfile | - | Avatar + name |
| Games | GamesPlayed | Count | Total games played |
| Wins | Wins | Count | Total victories |
| L.Road | LongestRoadWins | Count | Times won Longest Road |
| L.Army | LargestArmyWins | Count | Times won Largest Army |
| Longest Road | LongestRoadRecord | Max | Best road length ever |
| Most Soldiers | MostSoldiersRecord | Max | Most soldiers in one game |
| Most Stars | MostStarsRecord | Max | Most stars in one game |
| Ave Stars | Totals.StarsEarned / GamesPlayed | Ave | Average stars per game |
| Targeted | Totals.TimesTargeted | Aggregate | Total robber targets |
| Robber | Totals.ResourcesLostToRobber | Aggregate | Total cards lost |

**Extensible Pattern - Max/Ave for Any Stat:**

| Base Stat | Max Column | Ave Column |
| --------- | ---------- | ---------- |
| Stars | MostStarsRecord | Totals.StarsEarned / GamesPlayed |
| Soldiers | MostSoldiersRecord | Totals.SoldiersPlayed / GamesPlayed |
| Road Length | LongestRoadRecord | (not typically averaged) |
| Resources | (not typically maxed) | Totals.ResourcesCollected / GamesPlayed |
| Targeted | (not typically maxed) | Totals.TimesTargeted / GamesPlayed |

**Type Definitions:**

- **Count**: Simple integer counter, incremented per occurrence
- **Aggregate**: Sum of values across all completed games
- **Max**: Highest value achieved in a single game (record)
- **Ave**: Calculated average (Aggregate / GamesPlayed)

### Sorting

Players are sorted by **wins descending** (most wins first), then by **win rate** as tiebreaker.

### Visual Design

- Player rows use the player's gradient colors as background
- Trophy icon displayed next to players with wins > 0
- Header row has dark background (#333333) with white text
- Wins column highlighted in gold (#ffd700)
- Left padding (60px) for hamburger menu bar
- Player column sticky on horizontal scroll

### Files for Stats Page

| File | Purpose |
| ---- | ------- |
| `WebUI/Pages/Stats.razor` | Stats page component |
| `WebUI/Pages/Stats.razor.css` | Page styling |
| `WebUI/Layout/NavMenu.razor` | Stats button in menu |

## Future Enhancements

### Victory Point Score Adjustment UI

After winner declaration and celebration, display a modal for each player to adjust their final
score to include hidden Victory Point cards. This ensures accurate `HighestScoreRecord` tracking.

**Flow:**

1. Winner declared → Celebration plays (5 seconds)
2. After celebration → Score adjustment modal appears
3. Shows each player with their current visible score
4. Each player can increment their score for hidden VP cards (+1 button per player)
5. "Confirm" button → Stats captured with adjusted scores
6. Save to database

**UI Mockup:**

```text
┌─────────────────────────────────────────┐
│  Adjust Final Scores                    │
│  (Add hidden Victory Point cards)       │
├─────────────────────────────────────────┤
│  🏆 Joe      Score: 10  [+] [-]         │
│     Doug     Score: 8   [+] [-]         │
│     Sarah    Score: 7   [+] [-]         │
├─────────────────────────────────────────┤
│           [ Confirm Scores ]            │
└─────────────────────────────────────────┘
```

### Development Card Purchase Tracking

Add "Buy Dev Card" option to the purchase tile UI, enabling full development card tracking.

**UI Changes:**

- Add dev card purchase button to `EntitlementPurchaseModel` in purchase tile
- Show dev card cost (Ore + Wheat + Sheep)
- Track purchase in `PlayerModel` (new field or use existing entitlements)

**Downstream Dependencies:**

1. `GameStateMachine` - Handle dev card purchase action
2. `PlayerModel` - Track `DevCardsBoughtThisGame` count
3. `GameStats` - Add `DevCardsBought` field
4. `LifetimeStats` - Add `MostDevCardsRecord`, update `Totals`

**New Statistics:**

| Stat | Type | Description |
| ---- | ---- | ----------- |
| DevCardsBought | Aggregate | Total dev cards purchased across all games |
| MostDevCardsRecord | Max | Most dev cards bought in a single game |
| AvgDevCardsPerGame | Calculated | DevCardsBought / GamesPlayed |

**Stats Page Column:**

| Column | Source | Fun Factor |
| ------ | ------ | ---------- |
| Dev Cards | Totals.DevCardsBought | Strategy indicator |
| Best DC | MostDevCardsRecord | Dev card hoarder bragging rights |

### Other Enhancements

- **Game History Page**: Browse CompletedGames, replay moves
- **Achievements**: Track milestones (first win, 10 games, etc.)
- **Game Replay**: Step through archived game turn by turn

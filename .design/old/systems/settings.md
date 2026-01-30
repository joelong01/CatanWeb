# Settings & House Rules

Source: Design document created 2025-12-04

## Overview

Settings control game behavior (gold tiles, supplemental build phase minimum players) and flow from the WebUI client through GameService
to the GameStateMachine. HouseRules are stored per-game in `GameModel.HouseRules`, persisted with game saves, and can be modified mid-game.

## Data Flow

```text
WebUI (NewGame.razor)  ──POST /api/game/new──▶  GameApiController  ──▶  GameStateMachine  ──▶  GameModel.HouseRules
                         { houseRules: {...} }

WebUI (Game.razor)     ──PUT /api/game/{id}/houserules──▶  GameApiController  ──▶  GameModel.HouseRules  ──▶  Broadcast
```

## Key Design Decisions

### 1. HouseRules stored in GameModel (per-game)

- `GameModel.HouseRules` already exists and is persisted with game saves
- GameStateMachine reads `gameModel.HouseRules` for gold tiles, supplemental min players, etc.
- No separate service needed - uses existing `GameStateMachineRegistry` to access games

### 2. House rules are opt-in via New Game page

- "Use House Rules" checkbox on New Game page
- When checked, shows customization options
- When unchecked, uses defaults from `ExpansionBoardInfo.Default` or `RegularBoardInfo.Default`

### 3. House rules can be changed mid-game

- API endpoint: `PUT /api/game/{gameId}/houserules`
- Changes are persisted and broadcast to all clients
- Note: Some settings only affect future turns (gold tiles already placed won't change)

## HouseRules Model

**File: `Catan3.Shared/Models/HouseRules.cs`**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| GoldTiles | int | 1 | Number of gold tiles on the board |
| WallsProtectCities | bool | true | Whether walls protect cities from robber |
| HideBaronBeforeInvasion | bool | false | Hide baron before invasion phase |
| KnightMovesBaronBeforeRoll | bool | true | Knight can move baron before roll |
| HideRobberBeforeInvasion | bool | false | Hide robber before invasion phase |
| KnightMovesRobberBeforeRoll | bool | false | Knight can move robber before roll |
| SupplementalMinPlayers | int | 5 | Minimum players for supplemental build phase |

## API Endpoints

### Create Game with HouseRules

`POST /api/game/new`

```json
{
  "gameType": "Expansion",
  "playerIds": ["player1", "player2", "player3", "player4"],
  "gameName": "My Game",
  "houseRules": {
    "goldTiles": 4,
    "supplementalMinPlayers": 3
  }
}
```

### Update HouseRules Mid-Game

`PUT /api/game/{gameId}/houserules`

```json
{
  "goldTiles": 4,
  "supplementalMinPlayers": 3,
  "knightMovesRobberBeforeRoll": true
}
```

## Client-Side Settings (WebUI)

Settings are stored in browser localStorage as `setting_{settingName}` keys. The Settings page (`/settings`) provides UI to change these values.

**Game-affecting settings** (read when creating new game):

- ExpansionGoldTiles (0-4)
- RegularGoldTiles (0-2)
- SupplementalMinPlayers (3-6)

**Client-only settings** (not sent to server):

- AutoSaveEnabled
- ShowDebugInfo
- AnimationSpeed

## GameStateMachine Integration

The GameStateMachine reads HouseRules from `gameModel.HouseRules`:

- **Gold tiles**: `SetTempGoldTiles()` uses `gameModel.HouseRules.GoldTiles`
- **Supplemental phase**: Check uses `gameModel.HouseRules.SupplementalMinPlayers` instead of hardcoded 5

## Files

| File | Purpose |
|------|---------|
| `Catan3.Shared/Models/HouseRules.cs` | HouseRules model with all game rule settings |
| `Catan3.Shared/Models/MessageObjects.cs` | `NewGameMessage` includes optional `HouseRules` |
| `Catan3.GameService/Controllers/GameApiController.cs` | `NewGame()` and `UpdateHouseRules()` endpoints |
| `Catan3.Shared/GameLogic/GameStateMachine.cs` | Reads HouseRules for game behavior |
| `WebUI/Pages/NewGame.razor` | UI for house rules selection at game creation |
| `WebUI/Pages/Settings.razor` | Global settings page (localStorage) |
| `WebUI/wwwroot/settings.json` | Settings definitions (inputType, options, defaults) |

## TODO / Open Work

- Validation: Should gold tile count be validated against available desert/sea tiles?
- Desktop parity: Desktop app should also pass HouseRules when creating games via service
- In-game UI: Determine where house rules editing UI lives (NavMenu, sidebar, or panel)

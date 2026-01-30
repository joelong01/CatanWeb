# Game Service API Reference

**Status:** As-Built
**Source:** `Catan3.GameService/Controllers/*.cs`

## 1. Overview

The GameService exposes a collection of RESTful endpoints to manage game lifecycle, gameplay commands, recordings, and player statistics.
While real-time updates are pushed via SignalR, all state-mutating actions are performed via REST POST requests to ensure reliable delivery and ordering.

**Base URL**: `http://localhost:8080/api`

## 2. Game Controller (`GameApiController`)

Primary controller for gameplay.

### Lifecycle
| Method | Endpoint | Description | Request Body | Response |
|---|---|---|---|---|
| **POST** | `/game/new` | Create a new game instance. | `NewGameMessage` | `{ success, gameId }` |
| **POST** | `/game/load` | Load a game from a compressed `.catan` blob. | `LoadGameMessage` | `{ success, gameId }` |
| **POST** | `/game/{gameId}/load` | Resume a saved game from the database. | (None) | `{ success, message }` |
| **POST** | `/game/end` | End a game session (cleanup). | `{ gameId }` | `{ success }` |

### Gameplay Commands
| Method | Endpoint | Description | Request Body | Response |
|---|---|---|---|---|
| **POST** | `/game/action` | **Main Command Endpoint**. Executes any gameplay action via `AsyncCommandProcessor`. | `JsonElement` (Action Wrapper) | `{ success, commandId, message }` |
| **GET** | `/gamestate/{gameId}` | Retrieve full current state. | - | `GameModel` |
| **GET** | `/games` | List all saved games in the database. | `?playerId={id}` (Optional) | `List<GameSaveMetadataEntity>` |

*Game Action Body Format:*
```json
{
  "gameId": "GUID",
  "playerId": "Player1",
  "messageType": "PurchaseMessage", // or RollMessage, etc.
  "messageData": { ...payload... }
}
```

### Static Data
| Method | Endpoint | Description |
|---|---|---|
| **GET** | `/players` | List all configured player profiles. |
| **GET** | `/images/{id}` | Retrieve player avatar image binary. |

## 3. Recording Controller (`RecordingController`)

Manages test recordings for regression testing.

| Method | Endpoint | Description |
|---|---|---|
| **POST** | `/recording/start/{gameId}` | Start recording actions for a running game. |
| **POST** | `/recording/stop/{gameId}` | Stop recording. |
| **GET** | `/recordings` | List all available recordings. |
| **GET** | `/recording/{id}` | Get full recording data (JSON). |
| **DELETE** | `/recording/{id}` | Delete a recording. |
| **POST** | `/recording/replay` | Execute a replay test against the running service. |

## 4. Stats Controller (`StatsController`)

Manages lifetime player statistics.

| Method | Endpoint | Description |
|---|---|---|
| **GET** | `/stats` | Get all player statistics. |
| **GET** | `/stats/{playerId}` | Get stats for a specific player. |
| **POST** | `/stats/reset` | **Admin:** Reset all lifetime statistics. |
| **POST** | `/stats/import` | Import stats from JSON backup. |
| **GET** | `/stats/export` | Export stats to JSON backup. |

## 5. SignalR Hub (`GameHub`)

**Hub URL**: `/gameHub`

### Server -> Client Methods
*   `GameStateUpdated(GameModel model)`: Broadcast on *any* state change.
*   `CommandCompleted(Guid commandId, bool success, string message)`: Acknowledge async action.
*   `CommandFailed(Guid commandId, string error)`: Notify async failure.
*   `PlayerPresenceChanged(string playerId, bool isConnected)`: Connectivity tracking.

### Client -> Server Methods
*   `JoinGame(string gameId)`: Subscribe to updates.
*   `LeaveGame(string gameId)`: Unsubscribe.
*   *Legacy Methods*: The Hub supports methods like `ExecutePurchase` but React clients prefer the REST API.

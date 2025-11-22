# GameService REST API Specification

## Overview

This document specifies the REST API for the Catan GameService. Commands are sent via REST endpoints and return success/error responses. Game state updates are broadcast to all clients via SignalR.

## Architecture

- **Commands**: REST POST endpoints with JSON bodies
- **Queries**: REST GET endpoints
- **Real-time Updates**: SignalR broadcasts `GameStateUpdated` event with full `GameModel`

## Base URL

```
http://localhost:8080/api
```

## Authentication

Currently: `playerId` in request body identifies the acting player.

Future: JWT token in `Authorization` header, `playerId` extracted from token claims.

## Common Response Format

### Success Response

```json
{
  "success": true,
  "message": "Command executed successfully",
  "gameId": "9f31dd51-0c99-46c7-83fb-1a57d7339b30"
}
```

### Error Response

```json
{
  "success": false,
  "error": "Player WebUI-Client cannot act - current player is Adrian",
  "errorCode": "INVALID_PLAYER"
}
```

### HTTP Status Codes

| Code | Meaning |
|------|---------|
| 200 | Success |
| 400 | Bad Request (invalid parameters) |
| 403 | Forbidden (not your turn, invalid action) |
| 404 | Game not found |
| 500 | Internal server error |

---

## Game Management Endpoints

### Create New Game

Creates a new game with specified players.

```
POST /api/game/new
```

**Request Body:**
```json
{
  "gameType": "Regular",
  "playerIds": ["Alice", "Bob", "Charlie"],
  "gameName": "Game 14:30"
}
```

**Response:**
```json
{
  "success": true,
  "gameId": "9f31dd51-0c99-46c7-83fb-1a57d7339b30"
}
```

### End Game

Ends and cleans up a game.

```
POST /api/game/{gameId}/end
```

**Request Body:**
```json
{
  "playerId": "Alice"
}
```

---

## Game Command Endpoints

All command endpoints follow the pattern:
```
POST /api/game/{gameId}/{command}
```

### Shuffle Board

Randomizes the tile layout during board setup.

```
POST /api/game/{gameId}/shuffle
```

**Request Body:**
```json
{
  "playerId": "Alice"
}
```

**Allowed States:** `PickingBoard`

### Next

Advances the game to the next state/phase.

```
POST /api/game/{gameId}/next
```

**Request Body:**
```json
{
  "playerId": "Alice"
}
```

**Allowed States:** `WaitingForNewGame`, `PickingBoard`, `WaitingForNext`

### Undo

Undoes the last action.

```
POST /api/game/{gameId}/undo
```

**Request Body:**
```json
{
  "playerId": "Alice"
}
```

### Redo

Redoes a previously undone action.

```
POST /api/game/{gameId}/redo
```

**Request Body:**
```json
{
  "playerId": "Alice"
}
```

### Roll Dice

Rolls the dice for resource distribution.

```
POST /api/game/{gameId}/roll
```

**Request Body:**
```json
{
  "playerId": "Alice",
  "roll1": 3,
  "roll2": 4
}
```

**Allowed States:** `WaitingForRoll`

### Balance Board

Auto-balances the board tile distribution.

```
POST /api/game/{gameId}/balance
```

**Request Body:**
```json
{
  "playerId": "Alice",
  "resourceCounts": {
    "Wheat": 4,
    "Wood": 4,
    "Brick": 3,
    "Sheep": 4,
    "Ore": 3
  }
}
```

**Allowed States:** `PickingBoard`

### Swap Tiles

Swaps two tiles on the board during setup.

```
POST /api/game/{gameId}/swap-tiles
```

**Request Body:**
```json
{
  "playerId": "Alice",
  "tile1": { "q": 0, "r": 0, "s": 0 },
  "tile2": { "q": 1, "r": -1, "s": 0 }
}
```

**Allowed States:** `PickingBoard`

### Set Player Order

Sets the turn order for players.

```
POST /api/game/{gameId}/set-player-order
```

**Request Body:**
```json
{
  "playerId": "Alice",
  "playerOrder": ["Bob", "Alice", "Charlie"]
}
```

**Allowed States:** `AllocateResourcesAndOrder`

### Go First

Indicates which player goes first.

```
POST /api/game/{gameId}/go-first
```

**Request Body:**
```json
{
  "playerId": "Alice",
  "firstPlayerId": "Bob"
}
```

---

## Building & Development Endpoints

### Purchase Building

Places a settlement or city.

```
POST /api/game/{gameId}/purchase
```

**Request Body:**
```json
{
  "playerId": "Alice",
  "buildingIndex": 5,
  "isCity": false
}
```

**Allowed States:** `WaitingForNext`, `Supplemental`

### Upgrade Building

Upgrades a settlement to a city.

```
POST /api/game/{gameId}/upgrade-building
```

**Request Body:**
```json
{
  "playerId": "Alice",
  "buildingIndex": 5
}
```

### Purchase Road

Places a road.

```
POST /api/game/{gameId}/purchase-road
```

**Request Body:**
```json
{
  "playerId": "Alice",
  "roadIndex": 12
}
```

**Allowed States:** `WaitingForNext`, `Supplemental`

---

## Robber Endpoints

### Move Robber

Moves the robber to a new tile.

```
POST /api/game/{gameId}/move-robber
```

**Request Body:**
```json
{
  "playerId": "Alice",
  "tileCoords": { "q": 1, "r": -1, "s": 0 },
  "targetPlayerId": "Bob"
}
```

**Allowed States:** `WaitingToMoveRobber`

---

## Supplemental Phase Endpoints

### Participate in Supplemental

Indicates whether player wants to participate in supplemental building phase.

```
POST /api/game/{gameId}/participate-supplemental
```

**Request Body:**
```json
{
  "playerId": "Alice",
  "participating": true
}
```

**Allowed States:** `Supplemental`

---

## Query Endpoints

### Get Game Types

Returns available game types for creating new games.

```
GET /api/game-types
```

**Response:**
```json
{
  "success": true,
  "gameTypes": [
    {
      "value": "Regular",
      "displayName": "Regular",
      "description": "Standard 3-4 player board (19 tiles)",
      "minPlayers": 3,
      "maxPlayers": 4
    },
    {
      "value": "Expansion",
      "displayName": "Expansion",
      "description": "5-6 player board (37 tiles)",
      "minPlayers": 3,
      "maxPlayers": 6
    }
  ]
}
```

### Get Games

Returns all active games.

```
GET /api/games
```

**Response:**
```json
{
  "success": true,
  "games": [
    {
      "gameId": "9f31dd51-0c99-46c7-83fb-1a57d7339b30",
      "gameName": "Game 14:30",
      "gameType": "Regular",
      "gameState": "PickingBoard",
      "playerCount": 3,
      "players": ["Alice", "Bob", "Charlie"],
      "currentPlayerId": "Alice",
      "createdAt": "2025-11-21T14:30:00Z"
    }
  ],
  "count": 1,
  "timestamp": "2025-11-21T16:00:00Z"
}
```

### Get Players

Returns all registered players.

```
GET /api/players
```

**Response:**
```json
{
  "success": true,
  "players": [
    {
      "id": "joe-001",
      "name": "Joe",
      "backgroundColor": "#0000FF",
      "foregroundColor": "#FFFFFF",
      "imageUri": "/api/images/joe-001"
    }
  ],
  "count": 6,
  "timestamp": "2025-11-21T16:00:00Z"
}
```

### Get Image

Returns a player's avatar image.

```
GET /api/images/{id}
```

**Response:** Binary image data with appropriate `Content-Type` header.

### Health Check

Service health endpoint.

```
GET /health
```

**Response:**
```json
{
  "status": "healthy",
  "timestamp": "2025-11-21T16:00:00Z"
}
```

---

## SignalR Hub

### Connection

```
ws://localhost:8080/gameHub
```

### Client Events (Server → Client)

#### GameStateUpdated

Broadcast after any successful command.

```typescript
connection.on("GameStateUpdated", (gameModel: GameModel) => {
  // Update UI with new game state
});
```

### Hub Methods (Client → Server)

These remain for Desktop app compatibility:

- `JoinGame(gameId, playerId)` - Join a game's SignalR group
- `LeaveGame(gameId, playerId)` - Leave a game's SignalR group

**Note:** All game commands should use REST endpoints. Hub methods for commands are deprecated for new clients.

---

## Request Models

### HexCoordinates

```json
{
  "q": 0,
  "r": 0,
  "s": 0
}
```

### GameType Enum

- `Regular` - Standard 3-4 player board (19 tiles)
- `Expansion` - 5-6 player board (37 tiles)

---

## Implementation Notes

### Server-Side Flow

1. REST endpoint receives command
2. Validate `playerId` matches `CurrentPlayerId`
3. Find `GameStateMachine` by `gameId`
4. Execute command on state machine
5. Get updated `GameModel`
6. Broadcast `GameStateUpdated` via SignalR to game group
7. Return success/error response to caller

### Error Handling

- **Invalid Player**: Return 403 with `INVALID_PLAYER` error code
- **Invalid State**: Return 400 with `INVALID_STATE` error code
- **Game Not Found**: Return 404 with `GAME_NOT_FOUND` error code
- **Invalid Parameters**: Return 400 with `INVALID_PARAMETERS` error code

### Concurrency

Commands are processed synchronously per game. The `GameStateMachine` handles all state transitions atomically.

---

## Migration Path

1. Implement REST endpoints in `GameApiController`
2. Create `CommandResponse` model
3. WebUI uses REST for commands, SignalR for updates
4. Desktop app continues using SignalR hub methods
5. Eventually migrate Desktop to REST (optional)

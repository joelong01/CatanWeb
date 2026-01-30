# Database Schema As-Built

**Status:** As-Built
**Source:** `Catan3.GameService/Data/CatanDbContext.cs`

## 1. Overview

The application uses **Entity Framework Core** with a **Hybrid Document-Relational** pattern. It uses SQL Server (or SQLite for local dev) but stores complex objects as serialized JSON or compressed binary blobs, surfacing only metadata fields for querying.

## 2. Entities

### A. Players (`PlayerEntity`)

Stores player profiles.

* **Id** (`PK, string`): Player ID (e.g., "Joe").
* **Data** (`ntext/nvarchar(max)`): JSON document containing the full `PlayerModel` / `PlayerProfile` data.
* *Usage*: Key-value store for player settings/stats.

### B. Game Saves (`GameSaveDataEntity` & `GameSaveMetadataEntity`)

Split into "Heavy" data and "Light" metadata for performance.

**Metadata (`GameSaveMetadataEntity`)**

* **Id** (`PK, int`): Auto-increment.
* **GameId** (`string`, Indexed): GUID of the game.
* **GameState** (`string`, Indexed): Current enum state (e.g., "WaitingForRoll").
* **PlayerNames**: Comma-separated list for display.
* *Usage*: Populates the "Load Game" list UI efficiently without loading full game logs.

**Data (`GameSaveDataEntity`)**

* **Id** (`PK, int`): Matches Metadata FK.
* **CompressedData** (`varbinary(max)/blob`): GZip compressed JSON of the entire `GameLog` (undo/redo history).
* *Usage*: Loaded only when resuming a specific game.

### C. Completed Games (`CompletedGameEntity`)

Archive for historical stats.

* **WinnerId**, **WinnerName**: For leaderboards.
* **CompletedAt**: Timestamp.
* **CompressedData**: Final game state snapshot.

### D. Images (`ImageEntity`)

Simple binary store for assets.

* **Id**: Image identifier.
* **Data**: Raw byte array.
* **ContentType**: MIME type (e.g., "image/png").

## 3. Key Patterns

1. **Compression**: Game logs are compressed before storage to save space and bandwidth.
2. **Metadata Separation**: Prevents "SELECT *" from fetching megabytes of game history when listing games.
3. **JSON Storage**: Avoids complex EF Core mapping for rapidly changing `GameModel` schemas. The DB treats the game rules/state as an opaque blob.

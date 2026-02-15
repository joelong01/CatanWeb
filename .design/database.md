# Database Architecture

**Last verified:** February 13, 2026

## Overview

The game service uses **Entity Framework Core** with SQLite (local dev) or
Azure SQL (production). The database stores player profiles, game saves,
completed game archives, test recordings, and game templates.

**DbContext:** `Catan3.GameService/Data/CatanDbContext.cs`

## Entity Tables

### 1. PlayerEntity

Player profile storage using document-style JSON.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | string(255) | PK | Player ID (e.g., "Joe-001") |
| `Data` | TEXT | required | JSON-serialized `PlayerProfile` |

### 2. ImageEntity

Binary image storage for player avatars.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | string(255) | PK | Image identifier |
| `ContentType` | string(100) | required | MIME type (image/jpeg, etc.) |
| `Data` | BLOB | required | Binary image bytes |

### 3. GameSaveMetadataEntity

Lightweight queryable metadata for saved games. Separating metadata from
blob data allows fast listing without loading full game histories.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | int | PK, auto-increment | Internal ID |
| `GameId` | string(255) | unique index | Game identifier |
| `GameName` | string(255) | | Display name |
| `GameState` | string(50) | indexed | Current game state |
| `GameType` | string(50) | | "Regular" or "Expansion" |
| `StartedBy` | string(255) | indexed | Creator (e.g., "WebUI") |
| `SavedAt` | DateTime | indexed | Last save time |
| `CreatedAt` | DateTime | | Original creation time |
| `PlayerCount` | int | | Number of players |
| `PlayerNames` | string(500) | | Comma-separated names |
| `TurnCount` | int | | State transitions count |
| `GameDataId` | int | FK (cascade delete) | Reference to data blob |

### 4. GameSaveDataEntity

Compressed game log blob storage.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | int | PK, auto-increment | Internal ID |
| `CompressedData` | BLOB | required | Compressed SerializableLog JSON |
| `Size` | int | | Byte size for display |

### 5. CompletedGameEntity

Archive of finished games with winner information.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | int | PK, auto-increment | Internal ID |
| `GameId` | string(255) | indexed, required | Original game identifier |
| `GameName` | string(255) | required | Display name |
| `WinnerId` | string(255) | indexed, required | Winner's player ID |
| `WinnerName` | string(255) | required | Winner's display name |
| `PlayerNames` | string(500) | required | Comma-separated names |
| `CompletedAt` | DateTime | indexed | When winner was declared |
| `StartedAt` | DateTime | | Original start time |
| `PlayerCount` | int | | Number of players |
| `TurnCount` | int | | Total actions |
| `CompressedData` | BLOB | | Full game history |
| `Size` | int | | Byte size |

### 6. RecordingEntity

Test recordings for replay verification.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | string | PK (GUID) | Recording identifier |
| `Name` | string(255) | required | User-provided name |
| `CreatedAt` | DateTime | indexed | Creation time |
| `GameType` | string(50) | required | "Regular" or "Expansion" |
| `PlayerCount` | int | | Number of players |
| `PlayerIds` | string(500) | required | Comma-separated player IDs |
| `ActionCount` | int | | Number of recorded actions |
| `Data` | TEXT | | JSON: initialGameModel + actions |

### 7. GameTemplateEntity

Board configuration templates for game creation.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | string(100) | PK | Template identifier (e.g., "regular") |
| `Name` | string(255) | required | Display name |
| `Category` | string(50) | indexed | "Base" or "Expansion" |
| `IsSystemTemplate` | bool | | True for built-in templates (cannot be deleted) |
| `Version` | int | | Template version number |
| `Data` | TEXT | required | JSON-serialized `GameTemplateData` |
| `CreatedAt` | DateTime | required | Creation time |
| `UpdatedAt` | DateTime | required | Last modification time |

## Persistence Service

**File:** `Catan3.GameService/Services/DatabasePersistenceService.cs`

The persistence service implements two-table save/load:

| Method | Purpose |
|--------|---------|
| `SaveAsync(gameId, data, metadata)` | Upsert game state (create or update) |
| `LoadAsync(gameId)` | Retrieve compressed game data |
| `GetGamesAsync(startedBy?)` | List saved games (filters out GameOver) |
| `DeleteAsync(gameId)` | Remove saved game |

### Save Flow

1. Serialize game state to `SerializableLog` (includes undo/redo stacks)
2. Compress to byte array
3. Write `GameSaveDataEntity` with compressed blob
4. Write `GameSaveMetadataEntity` with queryable fields and FK to data
5. Upsert: if `GameId` exists, update both entities

### Load Flow

1. Query `GameSaveMetadataEntity` by `GameId`
2. Follow FK to load `GameSaveDataEntity.CompressedData`
3. Decompress to `SerializableLog` JSON
4. Reconstruct `GameStateMachine` from log

## Design Decisions

- **Metadata/data split:** Allows listing saved games without loading
  multi-megabyte compressed blobs
- **Document-style players:** `PlayerEntity.Data` stores full JSON,
  mirroring a CosmosDB document model for future migration
- **Cascading delete:** Deleting metadata cascades to data blob
- **Completed games separate:** Archives preserve winner info and full
  history independently from active save slots

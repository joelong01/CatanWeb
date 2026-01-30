# Database Design

Source: design_docs/database-design.md

## Technology Stack

- SQLite via Entity Framework Core (`CatanDbContext`)
- Database file: `Catan3.GameService/Data/catan.db`
- Migrations not yet enabled; schema maintained via `DatabaseSeeder` and EF model snapshot.

## Entities

### GameSaveMetadataEntity

```csharp
public class GameSaveMetadataEntity
{
    public int Id { get; set; }
    public string GameId { get; set; } = string.Empty;
    public string StartedBy { get; set; } = "WebUI";
    public DateTime SavedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string GameState { get; set; } = "WaitingForRoll";
    public string GameType { get; set; } = "Regular";
    public int PlayerCount { get; set; }
    public string PlayerNames { get; set; } = string.Empty;
    public int TurnCount { get; set; }
    public string GameName { get; set; } = string.Empty;
    public int GameDataId { get; set; }
    public GameSaveDataEntity GameData { get; set; } = null!;
}
```

Indexed columns: `GameId` (unique), `StartedBy`, `SavedAt`. EF query filters exclude `GameState == "GameOver"` for active lists.

### GameSaveDataEntity

Holds the compressed `SerializableLog` blob and size in bytes. Cascade delete is configured from metadata to data.

### Player Entities

- `PlayerEntity` stores serialized `PlayerProfile` JSON plus image id.
- `ImageEntity` contains avatar bytes. `DatabaseSeeder` loads defaults from `Default Data/Players/` at startup when tables empty.

## Seeding & Health Checks

- `DatabaseSeeder.SeedAsync` ensures players and images exist; idempotent.
- `/api/database/health` counts players/games and flags `needsSeeding` or `needsGames` for `webui.ps1` to act on.

## Access Patterns

- `GamePersistenceService` uses scoped `CatanDbContext` per operation. Save operations update metadata timestamp and upsert blob.
- Queries: `GetGamesAsync` orders by `SavedAt` descending and filters on `StartedBy` when provided.
- Import endpoint reads `.catan` files, decompresses to `SerializableLog`, builds metadata, and saves using same service.

## TODO / Future Enhancements

- Introduce EF migrations for schema evolution once authentication lands.
- Add indexes for `GameType` and `PlayerCount` if saved-game list becomes large.
- Consider moving `StartedBy` to normalized user table when identity is implemented.

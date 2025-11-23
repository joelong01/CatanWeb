# Database Design Document

## Overview

This document describes the database schema, use cases, and data management for the Catan3 system. The design uses SQLite for development with a document-style storage pattern that mirrors CosmosDB for future cloud migration.

## Architecture

### Storage Pattern

The database uses a **document-style storage** pattern where complex entities are stored as JSON documents within a single column. This approach:

- Mirrors CosmosDB's document storage model
- Simplifies schema evolution (add fields without migrations)
- Maintains relational indices for query performance
- Enables easy migration to cloud when needed

### Database Location

```
Catan3.GameService/Data/catan.db
```

The database is created during first run or via the `--seed-database` command.

### Technology Stack

- **Current**: SQLite with Entity Framework Core
- **Future**: Azure CosmosDB with same document model
- **ORM**: Entity Framework Core with code-first approach

---

## Tables

### Players

Stores player profile information as JSON documents.

#### Schema

| Column | Type | Description | Constraints |
|--------|------|-------------|-------------|
| `Id` | TEXT | Player identifier (e.g., "joe-001") | PRIMARY KEY |
| `Data` | TEXT | JSON document containing PlayerData | NOT NULL |

#### Indices

- Primary key on `Id` (automatic)

#### PlayerData Document Structure

```json
{
  "Id": "joe-001",
  "Name": "Joe",
  "PrimaryBackgroundColor": "#0000FF",
  "SecondaryBackgroundColor": "#000080",
  "ForegroundColor": "#FFFFFF",
  "ImageUri": "/api/images/joe-001"
}
```

#### Field Descriptions

| Field | Type | Description |
|-------|------|-------------|
| `Id` | string | Unique identifier (format: `name-salt`) |
| `Name` | string | Display name shown in UI |
| `PrimaryBackgroundColor` | string | Primary gradient color (e.g., "#0000FF") |
| `SecondaryBackgroundColor` | string | Secondary gradient color (e.g., "#000080") |
| `ForegroundColor` | string | Text/foreground color (e.g., "#FFFFFF") |
| `ImageUri` | string | Relative URI to player's avatar image |

**Gradient Rendering**: The UI creates a gradient brush from `PrimaryBackgroundColor` to `SecondaryBackgroundColor` for player-themed controls (see `PlayerColorViewModel` in Desktop app).

#### Use Cases

1. **New Game Player Selection**: Load all players for selection dropdown
2. **Game Display**: Show player names, colors, and avatars during gameplay
3. **Player Profile Management**: CRUD operations for player profiles
4. **Color Theming**: Apply player colors to UI elements (controls, highlights)

#### Adding New Fields

To add a new field (e.g., player statistics, preferences):

1. Add property to `Catan3.Shared/ViewData/PlayerData.cs`
2. Update `DatabaseSeeder.cs` with default values for seeded players
3. No database migration needed - JSON document accepts new fields

---

### Images

Stores binary image data for player avatars.

#### Schema

| Column | Type | Description | Constraints |
|--------|------|-------------|-------------|
| `Id` | TEXT | Image identifier (matches player ID) | PRIMARY KEY |
| `ContentType` | TEXT | MIME type (e.g., "image/jpeg") | NOT NULL |
| `Data` | BLOB | Binary image data | NOT NULL |

#### Indices

- Primary key on `Id` (automatic)

#### Use Cases

1. **Player Avatar Display**: Serve avatar images via `/api/images/{id}`
2. **Profile Photo Upload**: Store cropped/resized player photos
3. **Image Format Support**: JPEG, PNG, GIF, WebP

#### Supported Content Types

- `image/jpeg`
- `image/png`
- `image/gif`
- `image/webp`

---

### GameSaves

Stores saved game state as compressed binary data.

#### Schema

| Column | Type | Description | Constraints |
|--------|------|-------------|-------------|
| `Id` | INTEGER | Auto-increment primary key | PRIMARY KEY, AUTO |
| `GameId` | TEXT | Unique game identifier (GUID) | INDEXED |
| `CompressedData` | BLOB | Compressed .catan format data | NOT NULL |
| `SavedAt` | DATETIME | Last save timestamp | NOT NULL |
| `CreatedAt` | DATETIME | Game creation timestamp | NOT NULL, INDEXED |
| `GameName` | TEXT | Display name for game | |
| `GameState` | TEXT | Current state (for filtering) | |
| `StartedBy` | TEXT | Player ID who created game | INDEXED |
| `PlayerCount` | INTEGER | Number of players | |
| `GameType` | TEXT | "Regular" or "Expansion" | |

#### Indices

- Primary key on `Id` (automatic, clustered)
- Secondary index on `GameId` (for game lookups)
- Secondary index on `CreatedAt` (for sorting by date)
- Secondary index on `StartedBy` (for "My games" filter)

#### Use Cases

1. **Game Persistence**: Save game state for later continuation
2. **Game List**: Show available games with metadata
3. **Game Recovery**: Reload game after browser refresh or crash
4. **Player's Games**: Filter games by who started them
5. **Cleanup**: Remove old/abandoned games by creation date

#### CompressedData Format

The `.catan` format is a GZip-compressed JSON representation of the full game state, including:

- Board layout (tiles, harbors)
- Player resources and buildings
- Game history (for undo/redo)
- Current game state

---

## Future Tables

### GameInvitations (Planned)

For multi-device companion mode game invitations.

```sql
CREATE TABLE GameInvitations (
    Id TEXT PRIMARY KEY,
    GameId TEXT NOT NULL,
    PlayerId TEXT NOT NULL,
    InvitedAt DATETIME NOT NULL,
    Status TEXT NOT NULL,  -- 'pending', 'accepted', 'declined'
    RespondedAt DATETIME,
    FOREIGN KEY (GameId) REFERENCES GameSaves(GameId),
    FOREIGN KEY (PlayerId) REFERENCES Players(Id)
);
```

#### Indices

- Index on `PlayerId` (for checking pending invitations)
- Index on `GameId, Status` (for game participant status)

#### Use Cases

1. **Send Invitations**: Host invites players to join game
2. **Check Pending**: Player sees invitations on login
3. **Accept/Decline**: Update invitation status

---

### Statistics Architecture

The system has two distinct types of statistics:

#### Game Statistics (Current - In GameModel)

During gameplay, statistics are stored in the `GameModel` and rendered in the game UI:

- Victory points per player
- Roads/settlements/cities built
- Resources collected
- Development cards played
- Longest road / largest army

These are part of the game state and saved with the game in `GameSaves.CompressedData`.

#### Lifetime Statistics (Future - In PlayerData)

Future enhancement: aggregate statistics across all games, stored in the `PlayerData` document:

```json
{
  "Id": "joe-001",
  "Name": "Joe",
  // ... existing fields ...
  "LifetimeStats": {
    "GamesPlayed": 42,
    "GamesWon": 15,
    "TotalVictoryPoints": 312,
    "TimesTargetedByRobber": 87,
    "LongestRoadCount": 8,
    "LargestArmyCount": 5
  }
}
```

**Implementation**: When a game ends, update each player's `LifetimeStats` in their profile. Not currently implemented - will be added when game completion flow is finalized.

---

## Data Management

### Bootstrap / Seeding

#### Command

```bash
# From Catan3.GameService directory
dotnet run -- --seed-database
```

Or using webui.ps1:

```powershell
./webui.ps1 seed
```

#### What Gets Seeded

**Default Players:**

| ID | Name | Primary | Secondary | Foreground | Image |
|----|------|---------|-----------|------------|-------|
| joe-001 | Joe | #0000FF | #000080 | #FFFFFF | joe.jpg |
| dodgy-001 | Dodgy | #FF0000 | #800000 | #FFFFFF | Dodgy.png |
| doug-001 | Doug | #008000 | #004000 | #FFFFFF | doug.jpg |
| ryan-001 | Ryan | #A9A9A9 | #696969 | #FFFFFF | ryan.jpg |
| adrian-001 | Adrian | #800080 | #400040 | #FFFFFF | adrian.jpg |
| chris-001 | Chris | #000000 | #333333 | #FFFFFF | chris.jpg |
| guest-001 | Guest | #FFA500 | #CC8400 | #000000 | guest.png |

**Image Source:**
Images are loaded from `DesktopApp/Assets/DefaultPlayers/`

#### Seeding Logic

```csharp
// In DatabaseSeeder.cs
public static async Task SeedAsync(CatanDbContext context, string imagesSourcePath)
{
    await context.Database.EnsureCreatedAsync();

    if (context.Players.Any())
    {
        Console.WriteLine("Database already seeded.");
        return;
    }

    // Seed players and images...
}
```

The seeder is idempotent - it only runs if the Players table is empty.

---

### CRUD APIs

#### Player Endpoints

| Endpoint | Method | Description | Request Body | Response |
|----------|--------|-------------|--------------|----------|
| `/api/players` | GET | List all players | - | `{ players: [...], count: n }` |
| `/api/players` | POST | Create new player | PlayerData JSON | `{ success: true, id: "..." }` |
| `/api/players/{id}` | GET | Get player by ID | - | PlayerData JSON |
| `/api/players/{id}` | PUT | Update player | PlayerData JSON | `{ success: true }` |
| `/api/players/{id}` | DELETE | Delete player | - | `{ success: true }` |

#### Image Endpoints

| Endpoint | Method | Description | Request | Response |
|----------|--------|-------------|---------|----------|
| `/api/images/{id}` | GET | Get player image | - | Binary image data |
| `/api/images/{id}` | POST | Upload image | `multipart/form-data` | `{ success: true }` |
| `/api/images/{id}` | DELETE | Delete image | - | `{ success: true }` |

#### Game Save Endpoints

| Endpoint | Method | Description | Response |
|----------|--------|-------------|----------|
| `/api/games` | GET | List all games | `{ games: [...], count: n }` |
| `/api/games/{gameId}` | GET | Get game by ID | GameSave metadata |
| `/api/games/{gameId}` | DELETE | Delete game | `{ success: true }` |

See `GameServiceAPIs.md` for full REST API documentation.

---

### Data Operations

#### Adding a New Player

```csharp
// Example: Add player via API
var playerData = new PlayerData
{
    Id = $"newplayer-{Guid.NewGuid().ToString("N")[..6]}",
    Name = "New Player",
    PrimaryBackgroundColor = "#FF6B6B",
    SecondaryBackgroundColor = "#CC5555",
    ForegroundColor = "#FFFFFF",
    ImageUri = "/api/images/newplayer-abc123"
};

// POST to /api/players
```

#### Updating Player Colors

```csharp
// Example: Update player colors
var player = await GetPlayer("joe-001");
player.PrimaryBackgroundColor = "#4ECDC4";
player.SecondaryBackgroundColor = "#3DBDB4";
player.ForegroundColor = "#000000";

// PUT to /api/players/joe-001
```

#### Querying Games by Player

```csharp
// In GameApiController or service
var myGames = await context.GameSaves
    .Where(g => g.StartedBy == playerId)
    .OrderByDescending(g => g.SavedAt)
    .ToListAsync();
```

---

## Implementation Details

### DbContext Configuration

```csharp
// CatanDbContext.cs
public class CatanDbContext : DbContext
{
    public DbSet<PlayerEntity> Players { get; set; } = null!;
    public DbSet<ImageEntity> Images { get; set; } = null!;
    public DbSet<GameSaveEntity> GameSaves { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure indices and constraints
        modelBuilder.Entity<GameSaveEntity>(entity =>
        {
            entity.HasIndex(e => e.GameId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.StartedBy);
        });
    }
}
```

### Service Registration

```csharp
// Program.cs
var dataDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data");
var dbPath = Path.Combine(dataDir, "catan.db");
builder.Services.AddDbContext<CatanDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));
```

---

## CosmosDB Migration Path

The document-style storage pattern enables straightforward migration to CosmosDB:

### Mapping

| SQLite Table | CosmosDB Container | Partition Key |
|--------------|-------------------|---------------|
| Players | players | /id |
| Images | images | /id |
| GameSaves | games | /gameId |

### Migration Steps

1. Create CosmosDB account and database
2. Create containers with appropriate partition keys
3. Implement `IRepository<T>` pattern abstracting storage
4. Create CosmosDB implementation alongside SQLite
5. Configure DI to switch implementations based on environment
6. Migrate data using export/import tools
7. Update connection strings for production

### Code Changes Required

```csharp
// Abstract repository pattern
public interface IPlayerRepository
{
    Task<PlayerData?> GetAsync(string id);
    Task<IEnumerable<PlayerData>> GetAllAsync();
    Task SaveAsync(PlayerData player);
    Task DeleteAsync(string id);
}

// SQLite implementation (current)
public class SqlitePlayerRepository : IPlayerRepository { ... }

// CosmosDB implementation (future)
public class CosmosPlayerRepository : IPlayerRepository { ... }
```

---

## Maintenance Tasks

### Database Backup

```powershell
# Copy database file
Copy-Item "Catan3.GameService/Data/catan.db" "backup/catan_$(Get-Date -Format 'yyyyMMdd').db"
```

### Cleanup Old Games

```sql
-- Delete games older than 30 days
DELETE FROM GameSaves
WHERE CreatedAt < datetime('now', '-30 days');
```

### Reset Database

```powershell
# Delete and re-seed
Remove-Item "Catan3.GameService/Data/catan.db"
dotnet run --project Catan3.GameService -- --seed-database
```

---

## File References

### Core Files

- `Catan3.GameService/Data/CatanDbContext.cs` - Entity Framework context and entity definitions
- `Catan3.GameService/Data/DatabaseSeeder.cs` - Bootstrap seeding logic
- `Catan3.Shared/ViewData/PlayerData.cs` - Player document model
- `Catan3.GameService/Program.cs` - Database service registration and seed command

### API Controllers

- `Catan3.GameService/Controllers/GameApiController.cs` - Game and player REST endpoints

### Related Design Documents

- `design_docs/GameServiceAPIs.md` - Full REST API specification
- `design_docs/Session-Bootstrap-Design.md` - Session management and bootstrap flow
- `design_docs/WebUI-Design.md` - WebUI architecture and data flow

---

## Adding New Data Types

When adding new data storage requirements:

1. **Define the model** in `Catan3.Shared/ViewData/` or `Catan3.Shared/Models/`
2. **Create entity** in `CatanDbContext.cs` following document-style pattern
3. **Add DbSet** to `CatanDbContext`
4. **Configure indices** in `OnModelCreating`
5. **Update seeder** if default data is needed
6. **Add API endpoints** in appropriate controller
7. **Document** use cases in this file

### Example: Adding Game Statistics

```csharp
// 1. Model
public class GameStatistics
{
    public string GameId { get; set; }
    public string WinnerId { get; set; }
    public Dictionary<string, int> PlayerScores { get; set; }
    // ...
}

// 2. Entity
public class GameStatisticsEntity
{
    public string Id { get; set; }
    public string Data { get; set; } // JSON document
}

// 3. DbSet
public DbSet<GameStatisticsEntity> GameStatistics { get; set; }

// 4. Index
entity.HasIndex(e => e.WinnerId);

// 5. API
[HttpGet("api/stats/{playerId}")]
public async Task<IActionResult> GetPlayerStats(string playerId) { ... }
```

using Catan3.Shared.Utility;
using Catan3.Shared.Profiles;
using Catan3.Shared.Models;
using Catan3.GameService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Catan3.GameService.Data;

public static class DatabaseSeeder
{
    /// <summary>
    /// Seeds the database with default players and games.
    /// </summary>
    /// <param name="context">Database context</param>
    /// <param name="defaultDataPath">Path to "Default Data" folder containing Players and Games subfolders</param>
    /// <param name="gamePersistence">Game persistence service for saving games</param>
    /// <param name="useSqlServer">True if using SQL Server (uses migrations), false for SQLite (uses EnsureCreated)</param>
    /// <param name="logger">Logger instance</param>
    public static async Task SeedAsync(
        CatanDbContext context,
        string defaultDataPath,
        IGamePersistence? gamePersistence = null,
        bool useSqlServer = false,
        ILogger? logger = null)
    {
        logger?.LogInformation("[SEEDER] SeedAsync called - useSqlServer: {UseSqlServer}, defaultDataPath: {DefaultDataPath}", useSqlServer, defaultDataPath);

        // Initialize database schema
        if (useSqlServer)
        {
            // SQL Server: Check if we have pending migrations
            logger?.LogInformation("[SEEDER] SQL Server mode - checking pending migrations...");
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            logger?.LogInformation("[SEEDER] Pending migrations: {Count}", pendingMigrations.Count());
            if (pendingMigrations.Any())
            {
                logger?.LogInformation("[SEEDER] Applying {Count} pending migration(s) (SQL Server)...", pendingMigrations.Count());
                await context.Database.MigrateAsync();
                logger?.LogInformation("[SEEDER] Migrations applied successfully");
            }
            else
            {
                // No migrations yet - use EnsureCreated for initial schema
                // This will be replaced by migrations once they're added
                logger?.LogInformation("[SEEDER] Ensuring database schema exists (SQL Server)...");
                await context.Database.EnsureCreatedAsync();
                logger?.LogInformation("[SEEDER] EnsureCreatedAsync completed");
            }
        }
        else
        {
            // SQLite: Create database if it doesn't exist
            // EnsureCreatedAsync is fine for development with SQLite
            logger?.LogInformation("[SEEDER] SQLite mode - calling EnsureCreatedAsync...");
            await context.Database.EnsureCreatedAsync();
            logger?.LogInformation("[SEEDER] SQLite EnsureCreatedAsync completed");
        }

        var playersPath = Path.Combine(defaultDataPath, "Players");
        var gamesPath = Path.Combine(defaultDataPath, "Games");
        logger?.LogInformation("[SEEDER] Players path: {PlayersPath}", playersPath);
        logger?.LogInformation("[SEEDER] Games path: {GamesPath}", gamesPath);

        // Seed players if not already seeded
        logger?.LogInformation("[SEEDER] Checking if players need to be seeded...");
        var hasPlayers = await context.Players.AnyAsync();
        logger?.LogInformation("[SEEDER] Players.Any() = {HasPlayers}", hasPlayers);

        if (!hasPlayers)
        {
            logger?.LogInformation("[SEEDER] Seeding players from: {PlayersPath}", playersPath);
            await SeedPlayersAsync(context, playersPath, logger);
            logger?.LogInformation("[SEEDER] SeedPlayersAsync completed");
        }
        else
        {
            logger?.LogInformation("[SEEDER] Players already seeded, skipping");
        }

        // Seed games if games folder exists and persistence service available
        logger?.LogInformation("[SEEDER] Checking games - gamePersistence null: {IsNull}, gamesPath exists: {Exists}", gamePersistence == null, Directory.Exists(gamesPath));
        if (gamePersistence != null && Directory.Exists(gamesPath))
        {
            logger?.LogInformation("[SEEDER] Seeding games...");
            await SeedGamesAsync(context, gamesPath, gamePersistence, logger);
            logger?.LogInformation("[SEEDER] SeedGamesAsync completed");
        }

        // Seed recordings if recordings folder exists
        var recordingsPath = Path.Combine(defaultDataPath, "Recordings");
        logger?.LogInformation("[SEEDER] Checking recordings - recordingsPath exists: {Exists}", Directory.Exists(recordingsPath));
        if (Directory.Exists(recordingsPath))
        {
            logger?.LogInformation("[SEEDER] Seeding recordings...");
            await SeedRecordingsAsync(context, recordingsPath, logger);
            logger?.LogInformation("[SEEDER] SeedRecordingsAsync completed");
        }

        // Upsert system game templates on every startup so board data changes take effect on deploy
        logger?.LogInformation("[SEEDER] Upserting system game templates...");
        await UpsertSystemTemplatesAsync(context, logger);
        logger?.LogInformation("[SEEDER] UpsertSystemTemplatesAsync completed");

        logger?.LogInformation("[SEEDER] Database seeding complete");
    }

    private static async Task SeedPlayersAsync(CatanDbContext context, string imagesSourcePath, ILogger? logger)
    {
        // Default players with their colors (primary, secondary for gradient, foreground)
        // IDs match Desktop App (PascalCase): Joe-001, Dodgy-001, etc.
        var players = new[]
        {
            new PlayerProfile("Joe-001", "Joe", "#0000FF", "#000080", "#FFFFFF", "/api/images/Joe-001"),
            new PlayerProfile("Dodgy-001", "Dodgy", "#FF0000", "#800000", "#FFFFFF", "/api/images/Dodgy-001"),
            new PlayerProfile("Doug-001", "Doug", "#008000", "#004000", "#FFFFFF", "/api/images/Doug-001"),
            new PlayerProfile("Ryan-001", "Ryan", "#d0ac35ff", "#000000ff", "#FFFFFF", "/api/images/Ryan-001"),
            new PlayerProfile("Adrian-001", "Adrian", "#800080", "#400040", "#FFFFFF", "/api/images/Adrian-001"),
            new PlayerProfile("Chris-001", "Chris", "#000000", "#333333", "#FFFFFF", "/api/images/Chris-001"),
            new PlayerProfile("Guest-001", "Guest", "#ff008cff", "#CC8400", "#000000", "/api/images/Guest-001")
        };

        // Map player IDs to image file names
        var imageFiles = new Dictionary<string, string>
        {
            ["Joe-001"] = "joe.jpg",
            ["Dodgy-001"] = "Dodgy.jpg",
            ["Doug-001"] = "doug.jpg",
            ["Ryan-001"] = "ryan.jpg",
            ["Adrian-001"] = "adrian.jpg",
            ["Chris-001"] = "chris.jpg",
            ["Guest-001"] = "guest.png"
        };

        // Seed players
        foreach (var player in players)
        {
            var playerEntity = new PlayerEntity
            {
                Id = player.Id,
                Data = JsonHelper.Serialize(player)
            };
            context.Players.Add(playerEntity);
            logger?.LogInformation("  Added player: {Name} ({Id})", player.Name, player.Id);
        }

        // Seed images
        foreach (var (playerId, fileName) in imageFiles)
        {
            var imagePath = Path.Combine(imagesSourcePath, fileName);
            if (File.Exists(imagePath))
            {
                var imageData = await File.ReadAllBytesAsync(imagePath);
                var contentType = GetContentType(fileName);

                var imageEntity = new ImageEntity
                {
                    Id = playerId,
                    ContentType = contentType,
                    Data = imageData
                };
                context.Images.Add(imageEntity);
                logger?.LogInformation("  Added image: {FileName} ({Size} bytes)", fileName, imageData.Length);
            }
            else
            {
                logger?.LogWarning("  Image not found: {ImagePath}", imagePath);
            }
        }

        await context.SaveChangesAsync();
        logger?.LogInformation("Players seeding complete.");
    }

    private static async Task SeedGamesAsync(CatanDbContext context, string gamesPath, IGamePersistence gamePersistence, ILogger? logger)
    {
        var gameFiles = Directory.GetFiles(gamesPath, "*.catan");
        if (gameFiles.Length == 0)
        {
            logger?.LogInformation("No .catan game files found to seed.");
            return;
        }

        logger?.LogInformation("Seeding {Count} game(s) from: {GamesPath}", gameFiles.Length, gamesPath);

        foreach (var gameFile in gameFiles)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(gameFile);

                // Read the compressed .catan file
                var compressedData = await File.ReadAllBytesAsync(gameFile);

                // Decompress to get the SerializableLog JSON
                var json = JsonHelper.Decompress(compressedData);
                var serializableLog = JsonHelper.Deserialize<Catan3.Shared.Interfaces.SerializableLog>(json);

                if (serializableLog == null || serializableLog.DoneCount == 0)
                {
                    logger?.LogWarning("  {FileName} appears empty or invalid, skipping", fileName);
                    continue;
                }

                // Get the current game state from the top of the done stack
                var currentGameJson = serializableLog.DoneStack.LastOrDefault();
                if (currentGameJson == null)
                {
                    logger?.LogWarning("  {FileName} has no game states, skipping", fileName);
                    continue;
                }

                var gameModel = JsonHelper.Deserialize<GameModel>(currentGameJson);
                if (gameModel == null)
                {
                    logger?.LogWarning("  {FileName} could not deserialize game model, skipping", fileName);
                    continue;
                }

                // Use the GameId from the file (preserves Desktop/WebUI compatibility)
                var gameId = gameModel.GameId;

                // Check if game already exists
                var existingGame = context.GameSaveMetadata.FirstOrDefault(m => m.GameId == gameId);
                if (existingGame != null)
                {
                    logger?.LogInformation("  Skipping {FileName} - already seeded as {GameId}", fileName, existingGame.GameId);
                    continue;
                }

                // Create metadata
                var metadata = new GameMetadata
                {
                    GameName = gameModel.GameName ?? fileName,
                    GameState = gameModel.GameState.ToString(),
                    StartedBy = "Import",
                    PlayerCount = gameModel.Players.Count,
                    GameType = gameModel.Tiles.Count > 19 ? "Expansion" : "Regular",
                    PlayerNames = string.Join(", ", gameModel.Players.Select(p => p.Name)),
                    TurnCount = serializableLog.DoneCount
                };

                // Save to database
                await gamePersistence.SaveAsync(gameId, compressedData, metadata);
                logger?.LogInformation("  Seeded game: {FileName} ({PlayerNames}) - {TurnCount} turns", fileName, metadata.PlayerNames, metadata.TurnCount);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "  Error seeding {GameFile}", gameFile);
            }
        }
    }

    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }

    private static async Task SeedRecordingsAsync(CatanDbContext context, string recordingsPath, ILogger? logger)
    {
        var recordingFiles = Directory.GetFiles(recordingsPath, "*.json");
        if (recordingFiles.Length == 0)
        {
            logger?.LogInformation("No .json recording files found to seed.");
            return;
        }

        logger?.LogInformation("Seeding {Count} recording(s) from: {RecordingsPath}", recordingFiles.Length, recordingsPath);

        foreach (var recordingFile in recordingFiles)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(recordingFile);

                // Read the JSON file
                var json = await File.ReadAllTextAsync(recordingFile);
                var recording = JsonHelper.Deserialize<RecordingDto>(json);

                if (recording == null)
                {
                    logger?.LogWarning("  {FileName} could not be deserialized, skipping", fileName);
                    continue;
                }

                // Check if recording already exists
                var existingRecording = await context.Recordings.FindAsync(recording.Id);
                if (existingRecording != null)
                {
                    logger?.LogInformation("  Skipping {FileName} - already seeded as {Name}", fileName, recording.Name);
                    continue;
                }

                // Create recording entity
                var entity = new RecordingEntity
                {
                    Id = recording.Id,
                    Name = recording.Name,
                    CreatedAt = recording.CreatedAt,
                    GameType = recording.GameType,
                    PlayerCount = recording.PlayerCount,
                    PlayerIds = recording.PlayerIds,
                    ActionCount = recording.ActionCount,
                    Data = recording.Data
                };

                context.Recordings.Add(entity);
                logger?.LogInformation("  Seeded recording: {Name} ({ActionCount} actions)", recording.Name, recording.ActionCount);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "  Error seeding {RecordingFile}", recordingFile);
            }
        }

        await context.SaveChangesAsync();
        logger?.LogInformation("Recordings seeding complete.");
    }

    /// <summary>
    /// Upserts system templates via ICatanDb (CosmosDB path).
    /// </summary>
    internal static async Task UpsertSystemTemplatesAsync(Abstractions.ICatanDb db, ILogger? logger = null)
    {
        (string Id, string Name, string Category, IGameMetadata Metadata)[] candidates =
        [
            ("regular",   "Regular Game",   "Base",      RegularBoardInfo.Default),
            ("expansion", "Expansion Game", "Expansion", ExpansionBoardInfo.Default),
        ];

        foreach (var (id, name, category, metadata) in candidates)
        {
            var templateData = BuildTemplateFromMetadata(metadata, id, name, category);
            await db.SaveTemplateAsync(id, name, category, isSystemTemplate: true, templateData);
        }

        logger?.LogInformation("  Upserted {Count} system game templates", candidates.Length);
    }

    internal static async Task UpsertSystemTemplatesAsync(CatanDbContext context, ILogger? logger = null)
    {
        var now = DateTime.UtcNow;

        (string Id, string Name, string Category, IGameMetadata Metadata)[] candidates =
        [
            ("regular",   "Regular Game",   "Base",      RegularBoardInfo.Default),
            ("expansion", "Expansion Game", "Expansion", ExpansionBoardInfo.Default),
        ];

        int count = 0;
        foreach (var (id, name, category, metadata) in candidates)
        {
            var data = JsonHelper.Serialize(BuildTemplateFromMetadata(metadata, id, name, category));
            var existing = await context.GameTemplates.FindAsync(id);
            if (existing is null)
            {
                context.GameTemplates.Add(new GameTemplateEntity
                {
                    Id = id,
                    Name = name,
                    Category = category,
                    IsSystemTemplate = true,
                    Version = 1,
                    Data = data,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            else
            {
                existing.Data = data;
                existing.UpdatedAt = now;
            }
            count++;
        }

        await context.SaveChangesAsync();
        logger?.LogInformation("  Updated {Count} system game templates", count);
    }

    private static GameTemplateData BuildTemplateFromMetadata(
        IGameMetadata metadata, string id, string name, string category)
    {
        var tiles = new List<TemplateTile>();
        for (int i = 0; i < metadata.TileKeys.Count; i++)
        {
            tiles.Add(new TemplateTile
            {
                Q = metadata.TileKeys[i].Q,
                R = metadata.TileKeys[i].R,
                Resource = metadata.Resources[i].ToString(),
                Number = metadata.Numbers[i]
            });
        }

        var harbors = metadata.Harbors.Select(h => new TemplateHarbor
        {
            Q = h.HarborKey.HexCoordinates.Q,
            R = h.HarborKey.HexCoordinates.R,
            Side = h.HarborKey.Side.ToString(),
            Type = h.HarborKey.HarborType.ToString()
        }).ToList();

        var entitlements = metadata.PurchaseableEntitlements.Select(e => new TemplateEntitlement
        {
            Entitlement = e.Entitlement.ToString()
        }).ToList();

        return new GameTemplateData
        {
            Id = id,
            Name = name,
            Category = category,
            Version = 1,
            Description = metadata.Description,
            Engine = "base",
            GameType = metadata.GameType.ToString(),
            ResourceRules = metadata.ResourceRules,
            HouseRules = metadata.HouseRules,
            HasSupplemental = metadata.HasSupplemental,
            Tiles = tiles,
            Harbors = harbors,
            Entitlements = entitlements
        };
    }

    /// <summary>
    /// DTO for deserializing recording JSON files
    /// </summary>
    private class RecordingDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string GameType { get; set; } = string.Empty;
        public int PlayerCount { get; set; }
        public string PlayerIds { get; set; } = string.Empty;
        public int ActionCount { get; set; }
        public string Data { get; set; } = string.Empty;
    }
}

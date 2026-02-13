using Catan3.Shared.Utility;
using Catan3.Shared.Profiles;
using Catan3.Shared.Models;
using Catan3.GameService.Services;
using Microsoft.EntityFrameworkCore;

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
    public static async Task SeedAsync(
        CatanDbContext context,
        string defaultDataPath,
        IGamePersistence? gamePersistence = null,
        bool useSqlServer = false)
    {
        Console.WriteLine($"[SEEDER] SeedAsync called - useSqlServer: {useSqlServer}, defaultDataPath: {defaultDataPath}");

        // Initialize database schema
        if (useSqlServer)
        {
            // SQL Server: Check if we have pending migrations
            Console.WriteLine("[SEEDER] SQL Server mode - checking pending migrations...");
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            Console.WriteLine($"[SEEDER] Pending migrations: {pendingMigrations.Count()}");
            if (pendingMigrations.Any())
            {
                Console.WriteLine($"[SEEDER] Applying {pendingMigrations.Count()} pending migration(s) (SQL Server)...");
                await context.Database.MigrateAsync();
                Console.WriteLine("[SEEDER] Migrations applied successfully");
            }
            else
            {
                // No migrations yet - use EnsureCreated for initial schema
                // This will be replaced by migrations once they're added
                Console.WriteLine("[SEEDER] Ensuring database schema exists (SQL Server)...");
                await context.Database.EnsureCreatedAsync();
                Console.WriteLine("[SEEDER] EnsureCreatedAsync completed");
            }
        }
        else
        {
            // SQLite: Create database if it doesn't exist
            // EnsureCreatedAsync is fine for development with SQLite
            Console.WriteLine("[SEEDER] SQLite mode - calling EnsureCreatedAsync...");
            await context.Database.EnsureCreatedAsync();
            Console.WriteLine("[SEEDER] SQLite EnsureCreatedAsync completed");
        }

        var playersPath = Path.Combine(defaultDataPath, "Players");
        var gamesPath = Path.Combine(defaultDataPath, "Games");
        Console.WriteLine($"[SEEDER] Players path: {playersPath}");
        Console.WriteLine($"[SEEDER] Games path: {gamesPath}");

        // Seed players if not already seeded
        Console.WriteLine("[SEEDER] Checking if players need to be seeded...");
        var hasPlayers = await context.Players.AnyAsync();
        Console.WriteLine($"[SEEDER] Players.Any() = {hasPlayers}");

        if (!hasPlayers)
        {
            Console.WriteLine($"[SEEDER] Seeding players from: {playersPath}");
            await SeedPlayersAsync(context, playersPath);
            Console.WriteLine("[SEEDER] SeedPlayersAsync completed");
        }
        else
        {
            Console.WriteLine("[SEEDER] Players already seeded, skipping");
        }

        // Seed games if games folder exists and persistence service available
        Console.WriteLine($"[SEEDER] Checking games - gamePersistence null: {gamePersistence == null}, gamesPath exists: {Directory.Exists(gamesPath)}");
        if (gamePersistence != null && Directory.Exists(gamesPath))
        {
            Console.WriteLine("[SEEDER] Seeding games...");
            await SeedGamesAsync(context, gamesPath, gamePersistence);
            Console.WriteLine("[SEEDER] SeedGamesAsync completed");
        }

        // Seed recordings if recordings folder exists
        var recordingsPath = Path.Combine(defaultDataPath, "Recordings");
        Console.WriteLine($"[SEEDER] Checking recordings - recordingsPath exists: {Directory.Exists(recordingsPath)}");
        if (Directory.Exists(recordingsPath))
        {
            Console.WriteLine("[SEEDER] Seeding recordings...");
            await SeedRecordingsAsync(context, recordingsPath);
            Console.WriteLine("[SEEDER] SeedRecordingsAsync completed");
        }

        // Seed game templates if not already seeded
        Console.WriteLine("[SEEDER] Checking if game templates need to be seeded...");
        var hasTemplates = await context.GameTemplates.AnyAsync();
        Console.WriteLine($"[SEEDER] GameTemplates.Any() = {hasTemplates}");
        if (!hasTemplates)
        {
            Console.WriteLine("[SEEDER] Seeding game templates...");
            await SeedTemplatesAsync(context);
            Console.WriteLine("[SEEDER] SeedTemplatesAsync completed");
        }
        else
        {
            Console.WriteLine("[SEEDER] Game templates already seeded, skipping");
        }

        Console.WriteLine("[SEEDER] Database seeding complete");
    }

    private static async Task SeedPlayersAsync(CatanDbContext context, string imagesSourcePath)
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
            Console.WriteLine($"  Added player: {player.Name} ({player.Id})");
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
                Console.WriteLine($"  Added image: {fileName} ({imageData.Length} bytes)");
            }
            else
            {
                Console.WriteLine($"  Warning: Image not found: {imagePath}");
            }
        }

        await context.SaveChangesAsync();
        Console.WriteLine("Players seeding complete.");
    }

    private static async Task SeedGamesAsync(CatanDbContext context, string gamesPath, IGamePersistence gamePersistence)
    {
        var gameFiles = Directory.GetFiles(gamesPath, "*.catan");
        if (gameFiles.Length == 0)
        {
            Console.WriteLine("No .catan game files found to seed.");
            return;
        }

        Console.WriteLine($"Seeding {gameFiles.Length} game(s) from: {gamesPath}");

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
                    Console.WriteLine($"  Warning: {fileName} appears empty or invalid, skipping");
                    continue;
                }

                // Get the current game state from the top of the done stack
                var currentGameJson = serializableLog.DoneStack.LastOrDefault();
                if (currentGameJson == null)
                {
                    Console.WriteLine($"  Warning: {fileName} has no game states, skipping");
                    continue;
                }

                var gameModel = JsonHelper.Deserialize<GameModel>(currentGameJson);
                if (gameModel == null)
                {
                    Console.WriteLine($"  Warning: {fileName} could not deserialize game model, skipping");
                    continue;
                }

                // Use the GameId from the file (preserves Desktop/WebUI compatibility)
                var gameId = gameModel.GameId;

                // Check if game already exists
                var existingGame = context.GameSaveMetadata.FirstOrDefault(m => m.GameId == gameId);
                if (existingGame != null)
                {
                    Console.WriteLine($"  Skipping {fileName} - already seeded as {existingGame.GameId}");
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
                Console.WriteLine($"  Seeded game: {fileName} ({metadata.PlayerNames}) - {metadata.TurnCount} turns");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error seeding {gameFile}: {ex.Message}");
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

    private static async Task SeedRecordingsAsync(CatanDbContext context, string recordingsPath)
    {
        var recordingFiles = Directory.GetFiles(recordingsPath, "*.json");
        if (recordingFiles.Length == 0)
        {
            Console.WriteLine("No .json recording files found to seed.");
            return;
        }

        Console.WriteLine($"Seeding {recordingFiles.Length} recording(s) from: {recordingsPath}");

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
                    Console.WriteLine($"  Warning: {fileName} could not be deserialized, skipping");
                    continue;
                }

                // Check if recording already exists
                var existingRecording = await context.Recordings.FindAsync(recording.Id);
                if (existingRecording != null)
                {
                    Console.WriteLine($"  Skipping {fileName} - already seeded as {recording.Name}");
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
                Console.WriteLine($"  Seeded recording: {recording.Name} ({recording.ActionCount} actions)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error seeding {recordingFile}: {ex.Message}");
            }
        }

        await context.SaveChangesAsync();
        Console.WriteLine("Recordings seeding complete.");
    }

    private static async Task SeedTemplatesAsync(CatanDbContext context)
    {
        var now = DateTime.UtcNow;

        var regularTemplate = BuildTemplateFromMetadata(
            RegularBoardInfo.Default, "regular", "Regular Game", "Base");
        var expansionTemplate = BuildTemplateFromMetadata(
            ExpansionBoardInfo.Default, "expansion", "Expansion Game", "Expansion");

        var entities = new[]
        {
            new GameTemplateEntity
            {
                Id = "regular",
                Name = "Regular Game",
                Category = "Base",
                IsSystemTemplate = true,
                Version = 1,
                Data = JsonHelper.Serialize(regularTemplate),
                CreatedAt = now,
                UpdatedAt = now
            },
            new GameTemplateEntity
            {
                Id = "expansion",
                Name = "Expansion Game",
                Category = "Expansion",
                IsSystemTemplate = true,
                Version = 1,
                Data = JsonHelper.Serialize(expansionTemplate),
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        context.GameTemplates.AddRange(entities);
        await context.SaveChangesAsync();
        Console.WriteLine($"  Seeded {entities.Length} game templates (regular, expansion)");
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

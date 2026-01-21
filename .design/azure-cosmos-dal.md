# Azure CosmosDB Data Access Layer Design

**Document Version:** 1.0  
**Last Updated:** 2024-12-08  
**Status:** Draft

## Overview

This document describes the design for a data access layer (DAL) that abstracts database operations to support both SQLite for local development (inner loop) and CosmosDB for Azure cloud deployment. The design enables seamless switching between local and cloud storage while maintaining full functionality in both connected and disconnected scenarios.

## Current State Analysis

### Existing Database Schema

The current SQLite implementation uses Entity Framework with four main entities:

1. **PlayerEntity** - Document-style storage for player profiles
2. **ImageEntity** - Binary image storage for player avatars  
3. **GameSaveDataEntity** - Compressed game log data (heavy blob storage)
4. **GameSaveMetadataEntity** - Lightweight metadata for querying saved games

### Current Data Access Patterns

- **Entity Framework DbContext** - `CatanDbContext` for SQL operations
- **Repository Pattern** - `IGamePersistence` interface with database implementation
- **Two-table Design** - Metadata table for queries + data table for blobs
- **Document Storage** - JSON serialization for complex objects (PlayerProfile)
- **Binary Storage** - Direct byte arrays for images and compressed data

### Azure Deployment Architecture

- **Storage Account** - Blob storage for SQLite database file upload
- **Managed Identity** - Authentication for Azure services
- **App Service** - Hosts GameService with DATABASE_MODE=azure configuration

## Design Goals

1. **Seamless Switching** - Same application code works locally and in Azure
2. **Inner Loop Optimization** - Full functionality when disconnected
3. **Performance** - Optimized queries and minimal overhead
4. **Consistency** - Same behavior across storage backends
5. **Maintainability** - Clean abstractions and testable code
6. **Migration Path** - Easy transition from current SQLite implementation

## Proposed Architecture

### Interface Abstraction Layer

```csharp
/// <summary>
/// Primary data access interface - replaces direct DbContext usage
/// </summary>
public interface IDataRepository
{
    // Player Management
    Task<PlayerProfile?> GetPlayerAsync(string playerId);
    Task<List<PlayerProfile>> GetPlayersAsync();
    Task<bool> CreatePlayerAsync(PlayerProfile player);
    Task<bool> UpdatePlayerAsync(PlayerProfile player);
    Task<bool> DeletePlayerAsync(string playerId);

    // Image Management
    Task<ImageData?> GetImageAsync(string imageId);
    Task<bool> SaveImageAsync(string imageId, ImageData imageData);
    Task<bool> DeleteImageAsync(string imageId);

    // Game Persistence
    Task<bool> SaveGameAsync(string gameId, byte[] compressedData, GameMetadata metadata);
    Task<byte[]?> LoadGameAsync(string gameId);
    Task<List<GameSaveMetadataEntity>> GetGamesAsync(string? startedBy = null);
    Task<List<GameSaveMetadataEntity>> GetGamesByStateAsync(GameStateFilter stateFilter, string? startedBy = null);
    Task<bool> DeleteGameAsync(string gameId);

    // Health and Diagnostics
    Task<DatabaseHealthStatus> GetHealthAsync();
    Task<bool> InitializeAsync();
}

/// <summary>
/// Configuration interface for connection details
/// </summary>
public interface IDataConfiguration
{
    DataStorageType StorageType { get; }
    string ConnectionString { get; }
    string DatabaseName { get; }
    string ContainerPrefix { get; }
    Dictionary<string, object> AdditionalSettings { get; }
}

/// <summary>
/// Health status for diagnostics
/// </summary>
public class DatabaseHealthStatus
{
    public bool Healthy { get; set; }
    public string Status { get; set; } = string.Empty;
    public int PlayerCount { get; set; }
    public int GameCount { get; set; }
    public bool NeedsSeeding { get; set; }
    public string? Error { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Storage type enumeration
/// </summary>
public enum DataStorageType
{
    SQLite,
    CosmosDB
}

/// <summary>
/// Image data wrapper
/// </summary>
public class ImageData
{
    public string ContentType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = [];
}

/// <summary>
/// Game state filter for discovering and filtering games
/// </summary>
public class GameStateFilter
{
    /// <summary>
    /// Specific game states to include (null = no state filter)
    /// </summary>
    public HashSet<string>? IncludeStates { get; set; }
    
    /// <summary>
    /// Specific game states to exclude (null = no exclusions)
    /// </summary>
    public HashSet<string>? ExcludeStates { get; set; }
    
    /// <summary>
    /// Include active games (non-GameOver states)
    /// </summary>
    public bool ActiveGamesOnly { get; set; } = false;
    
    /// <summary>
    /// Include completed games (GameOver state)
    /// </summary>
    public bool CompletedGamesOnly { get; set; } = false;
    
    /// <summary>
    /// Game types to filter by (null = all types)
    /// </summary>
    public HashSet<string>? GameTypes { get; set; }
    
    /// <summary>
    /// Date range filter
    /// </summary>
    public DateTime? SavedAfter { get; set; }
    public DateTime? SavedBefore { get; set; }
    
    /// <summary>
    /// Maximum number of results to return (null = no limit)
    /// </summary>
    public int? MaxResults { get; set; }
    
    /// <summary>
    /// Skip this many results (for pagination)
    /// </summary>
    public int Skip { get; set; } = 0;

    /// <summary>
    /// Predefined filters for common scenarios
    /// </summary>
    public static GameStateFilter ActiveGames => new() { ActiveGamesOnly = true };
    public static GameStateFilter CompletedGames => new() { CompletedGamesOnly = true };
    public static GameStateFilter NotGameOver => new() { ExcludeStates = new HashSet<string> { "GameOver" } };
    public static GameStateFilter WaitingForPlayers => new() { IncludeStates = new HashSet<string> { "WaitingForPlayers", "Setup" } };
    public static GameStateFilter InProgress => new() { IncludeStates = new HashSet<string> { "Playing", "Trading", "Building", "RollingDice" } };
    
    /// <summary>
    /// Create filter for specific states
    /// </summary>
    public static GameStateFilter ForStates(params string[] states) => 
        new() { IncludeStates = new HashSet<string>(states) };
    
    /// <summary>
    /// Create filter excluding specific states  
    /// </summary>
    public static GameStateFilter ExcludingStates(params string[] states) => 
        new() { ExcludeStates = new HashSet<string>(states) };
    
    /// <summary>
    /// Create filter for recent games (last N days)
    /// </summary>
    public static GameStateFilter RecentGames(int days = 7) => 
        new() { SavedAfter = DateTime.UtcNow.AddDays(-days) };
}
```

### Configuration System

```csharp
/// <summary>
/// Configuration provider that detects environment and returns appropriate settings
/// ZERO CONFIG PRINCIPLE: localhost always uses SQLite, Azure uses CosmosDB
/// </summary>
public class DataConfigurationProvider : IDataConfiguration
{
    public DataStorageType StorageType { get; private set; }
    public string ConnectionString { get; private set; } = string.Empty;
    public string DatabaseName { get; private set; } = "catan";
    public string ContainerPrefix { get; private set; } = "catan";
    public Dictionary<string, object> AdditionalSettings { get; private set; } = new();

    public DataConfigurationProvider(IConfiguration configuration, IWebHostEnvironment? environment = null)
    {
        // ZERO CONFIG DETECTION LOGIC:
        // 1. If running on localhost/127.0.0.1 -> SQLite (no config needed)
        // 2. If Azure App Service (WEBSITE_SITE_NAME exists) -> CosmosDB
        // 3. Explicit DATABASE_MODE override if needed
        // 4. Default to SQLite for all other local scenarios

        var databaseMode = configuration["DATABASE_MODE"]?.ToLowerInvariant();
        var isAzureAppService = !string.IsNullOrEmpty(configuration["WEBSITE_SITE_NAME"]);
        var isLocalhost = IsRunningOnLocalhost(configuration, environment);

        if (databaseMode == "azure" || databaseMode == "cosmos")
        {
            StorageType = DataStorageType.CosmosDB;
        }
        else if (databaseMode == "sqlite")
        {
            StorageType = DataStorageType.SQLite;
        }
        else if (isLocalhost)
        {
            // ZERO CONFIG: localhost always uses SQLite
            StorageType = DataStorageType.SQLite;
        }
        else if (isAzureAppService)
        {
            // Azure App Service uses CosmosDB
            StorageType = DataStorageType.CosmosDB;
        }
        else
        {
            // Default to SQLite for all other scenarios (development, docker, etc.)
            StorageType = DataStorageType.SQLite;
        }

        ConfigureForStorageType(configuration);
    }

    private bool IsRunningOnLocalhost(IConfiguration configuration, IWebHostEnvironment? environment)
    {
        // Check common localhost indicators
        var urls = configuration["ASPNETCORE_URLS"] ?? configuration["urls"] ?? "";
        var isDevelopment = environment?.IsDevelopment() ?? false;
        var isLocalhost = urls.Contains("localhost") || urls.Contains("127.0.0.1") || urls.Contains("::1");
        
        // Also check if we're in development environment without explicit URLs
        return isLocalhost || (isDevelopment && !urls.Contains("azurewebsites.net"));
    }

    private void ConfigureForStorageType(IConfiguration configuration)
    {
        switch (StorageType)
        {
            case DataStorageType.SQLite:
                // ZERO CONFIG: Use standard path, create directory if needed
                ConnectionString = configuration.GetConnectionString("DefaultConnection") 
                    ?? "Data Source=Data/catan.db";
                break;

            case DataStorageType.CosmosDB:
                ConnectionString = configuration["COSMOS_CONNECTION_STRING"] 
                    ?? throw new InvalidOperationException("COSMOS_CONNECTION_STRING required for CosmosDB mode");
                DatabaseName = configuration["COSMOS_DATABASE_NAME"] ?? "catan";
                ContainerPrefix = configuration["COSMOS_CONTAINER_PREFIX"] ?? "catan";
                
                AdditionalSettings["PartitionKeyPath"] = configuration["COSMOS_PARTITION_KEY"] ?? "/partitionKey";
                AdditionalSettings["ThroughputMode"] = configuration["COSMOS_THROUGHPUT_MODE"] ?? "serverless";
                break;
        }
    }
}
```

## SQLite Implementation

### SQLiteDataRepository

```csharp
/// <summary>
/// SQLite implementation using Entity Framework - maintains current behavior
/// </summary>
public class SQLiteDataRepository : IDataRepository
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SQLiteDataRepository> _logger;

    public SQLiteDataRepository(IServiceProvider serviceProvider, ILogger<SQLiteDataRepository> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<PlayerProfile?> GetPlayerAsync(string playerId)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatanDbContext>();
        
        var entity = await dbContext.Players.FindAsync(playerId);
        if (entity == null) return null;

        return JsonHelper.Deserialize<PlayerProfile>(entity.Data);
    }

    public async Task<List<PlayerProfile>> GetPlayersAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatanDbContext>();
        
        var entities = await dbContext.Players.ToListAsync();
        return entities
            .Select(e => JsonHelper.Deserialize<PlayerProfile>(e.Data))
            .Where(p => p != null)
            .Cast<PlayerProfile>()
            .ToList();
    }

    public async Task<bool> CreatePlayerAsync(PlayerProfile player)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CatanDbContext>();
            
            var entity = new PlayerEntity
            {
                Id = player.Id,
                Data = JsonHelper.Serialize(player)
            };

            dbContext.Players.Add(entity);
            await dbContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating player {PlayerId}", player.Id);
            return false;
        }
    }

    public async Task<bool> SaveGameAsync(string gameId, byte[] compressedData, GameMetadata metadata)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CatanDbContext>();

            // Use existing two-table upsert logic
            var existingMetadata = await dbContext.GameSaveMetadata
                .Include(m => m.GameData)
                .FirstOrDefaultAsync(m => m.GameId == gameId);

            if (existingMetadata != null)
            {
                // Update existing
                existingMetadata.GameData.CompressedData = compressedData;
                existingMetadata.GameData.Size = compressedData.Length;
                existingMetadata.SavedAt = DateTime.UtcNow;
                existingMetadata.GameName = metadata.GameName;
                // ... update other fields
            }
            else
            {
                // Create new
                var gameData = new GameSaveDataEntity
                {
                    CompressedData = compressedData,
                    Size = compressedData.Length
                };
                dbContext.GameSaveData.Add(gameData);
                await dbContext.SaveChangesAsync();

                var gameMetadata = new GameSaveMetadataEntity
                {
                    GameId = gameId,
                    StartedBy = metadata.StartedBy,
                    SavedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    GameState = metadata.GameState,
                    GameType = metadata.GameType,
                    PlayerCount = metadata.PlayerCount,
                    PlayerNames = metadata.PlayerNames,
                    TurnCount = metadata.TurnCount,
                    GameName = metadata.GameName,
                    GameDataId = gameData.Id
                };
                dbContext.GameSaveMetadata.Add(gameMetadata);
            }

            await dbContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving game {GameId}", gameId);
            return false;
        }
    }

    // ... additional methods following same pattern
}
```

## CosmosDB Implementation

### Document Models for CosmosDB

```csharp
/// <summary>
/// Base document type for CosmosDB with partition key and metadata
/// </summary>
public abstract class CosmosDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("partitionKey")]
    public string PartitionKey { get; set; } = string.Empty;

    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }
}

/// <summary>
/// Player document for CosmosDB
/// </summary>
public class PlayerDocument : CosmosDocument
{
    [JsonPropertyName("profile")]
    public PlayerProfile Profile { get; set; } = new();

    public PlayerDocument()
    {
        DocumentType = "player";
        PartitionKey = "players";
    }

    public PlayerDocument(PlayerProfile profile) : this()
    {
        Id = profile.Id;
        Profile = profile;
    }
}

/// <summary>
/// Game metadata document for CosmosDB queries
/// </summary>
public class GameMetadataDocument : CosmosDocument
{
    [JsonPropertyName("gameId")]
    public string GameId { get; set; } = string.Empty;

    [JsonPropertyName("gameName")]
    public string GameName { get; set; } = string.Empty;

    [JsonPropertyName("gameState")]
    public string GameState { get; set; } = string.Empty;

    [JsonPropertyName("startedBy")]
    public string StartedBy { get; set; } = string.Empty;

    [JsonPropertyName("savedAt")]
    public DateTime SavedAt { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("gameType")]
    public string GameType { get; set; } = string.Empty;

    [JsonPropertyName("playerCount")]
    public int PlayerCount { get; set; }

    [JsonPropertyName("playerNames")]
    public string PlayerNames { get; set; } = string.Empty;

    [JsonPropertyName("turnCount")]
    public int TurnCount { get; set; }

    [JsonPropertyName("dataSize")]
    public int DataSize { get; set; }

    public GameMetadataDocument()
    {
        DocumentType = "game-metadata";
    }
}

/// <summary>
/// Game data document for CosmosDB blob storage
/// </summary>
public class GameDataDocument : CosmosDocument
{
    [JsonPropertyName("gameId")]
    public string GameId { get; set; } = string.Empty;

    [JsonPropertyName("compressedData")]
    public byte[] CompressedData { get; set; } = [];

    [JsonPropertyName("size")]
    public int Size { get; set; }

    public GameDataDocument()
    {
        DocumentType = "game-data";
    }
}

/// <summary>
/// Image document for CosmosDB
/// </summary>
public class ImageDocument : CosmosDocument
{
    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public byte[] Data { get; set; } = [];

    public ImageDocument()
    {
        DocumentType = "image";
        PartitionKey = "images";
    }
}
```

### CosmosDBDataRepository

```csharp
/// <summary>
/// CosmosDB implementation using Azure Cosmos DB SDK
/// </summary>
public class CosmosDBDataRepository : IDataRepository, IDisposable
{
    private readonly CosmosClient _cosmosClient;
    private readonly Database _database;
    private readonly Container _playersContainer;
    private readonly Container _gamesContainer;
    private readonly Container _imagesContainer;
    private readonly ILogger<CosmosDBDataRepository> _logger;

    private const string PLAYERS_CONTAINER = "players";
    private const string GAMES_CONTAINER = "games";  
    private const string IMAGES_CONTAINER = "images";

    public CosmosDBDataRepository(IDataConfiguration config, ILogger<CosmosDBDataRepository> logger)
    {
        _logger = logger;
        
        var cosmosOptions = new CosmosClientOptions
        {
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            },
            ConnectionMode = ConnectionMode.Direct,
            ConsistencyLevel = ConsistencyLevel.Session
        };

        _cosmosClient = new CosmosClient(config.ConnectionString, cosmosOptions);
        _database = _cosmosClient.GetDatabase(config.DatabaseName);
        
        var containerPrefix = config.ContainerPrefix;
        _playersContainer = _database.GetContainer($"{containerPrefix}-{PLAYERS_CONTAINER}");
        _gamesContainer = _database.GetContainer($"{containerPrefix}-{GAMES_CONTAINER}");
        _imagesContainer = _database.GetContainer($"{containerPrefix}-{IMAGES_CONTAINER}");
    }

    public async Task<bool> InitializeAsync()
    {
        try
        {
            // Create database if it doesn't exist (serverless mode)
            var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_database.Id);
            
            // Create containers with appropriate partition keys
            await _database.CreateContainerIfNotExistsAsync(
                $"{_playersContainer.Id}", 
                "/partitionKey", 
                throughput: null); // Serverless

            await _database.CreateContainerIfNotExistsAsync(
                $"{_gamesContainer.Id}", 
                "/gameId",  // Partition by gameId for game documents
                throughput: null);

            await _database.CreateContainerIfNotExistsAsync(
                $"{_imagesContainer.Id}", 
                "/partitionKey", 
                throughput: null);

            _logger.LogInformation("CosmosDB containers initialized");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing CosmosDB containers");
            return false;
        }
    }

    public async Task<PlayerProfile?> GetPlayerAsync(string playerId)
    {
        try
        {
            var response = await _playersContainer.ReadItemAsync<PlayerDocument>(
                playerId, 
                new PartitionKey("players"));
                
            return response.Resource.Profile;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting player {PlayerId}", playerId);
            return null;
        }
    }

    public async Task<List<PlayerProfile>> GetPlayersAsync()
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.documentType = @documentType")
                .WithParameter("@documentType", "player");

            var iterator = _playersContainer.GetItemQueryIterator<PlayerDocument>(query);
            var players = new List<PlayerProfile>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                players.AddRange(response.Select(doc => doc.Profile));
            }

            return players;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting players");
            return new List<PlayerProfile>();
        }
    }

    public async Task<bool> CreatePlayerAsync(PlayerProfile player)
    {
        try
        {
            var document = new PlayerDocument(player);
            await _playersContainer.CreateItemAsync(document, new PartitionKey("players"));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating player {PlayerId}", player.Id);
            return false;
        }
    }

    public async Task<bool> UpdatePlayerAsync(PlayerProfile player)
    {
        try
        {
            var document = new PlayerDocument(player);
            await _playersContainer.ReplaceItemAsync(document, document.Id, new PartitionKey("players"));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating player {PlayerId}", player.Id);
            return false;
        }
    }

    public async Task<bool> SaveGameAsync(string gameId, byte[] compressedData, GameMetadata metadata)
    {
        try
        {
            // Create metadata document for queries
            var metadataDoc = new GameMetadataDocument
            {
                Id = $"metadata-{gameId}",
                GameId = gameId,
                GameName = metadata.GameName,
                GameState = metadata.GameState,
                StartedBy = metadata.StartedBy,
                SavedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                GameType = metadata.GameType,
                PlayerCount = metadata.PlayerCount,
                PlayerNames = metadata.PlayerNames,
                TurnCount = metadata.TurnCount,
                DataSize = compressedData.Length,
                PartitionKey = gameId
            };

            // Create data document for blob storage
            var dataDoc = new GameDataDocument
            {
                Id = $"data-{gameId}",
                GameId = gameId,
                CompressedData = compressedData,
                Size = compressedData.Length,
                PartitionKey = gameId
            };

            // Use transaction for consistency (both documents have same partition key)
            var batch = _gamesContainer.CreateTransactionalBatch(new PartitionKey(gameId));
            
            // Check if documents exist for upsert
            try
            {
                await _gamesContainer.ReadItemAsync<GameMetadataDocument>($"metadata-{gameId}", new PartitionKey(gameId));
                // Exists - replace
                batch.ReplaceItem($"metadata-{gameId}", metadataDoc);
                batch.ReplaceItem($"data-{gameId}", dataDoc);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Doesn't exist - create
                batch.CreateItem(metadataDoc);
                batch.CreateItem(dataDoc);
            }

            var batchResponse = await batch.ExecuteAsync();
            return batchResponse.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving game {GameId}", gameId);
            return false;
        }
    }

    public async Task<byte[]?> LoadGameAsync(string gameId)
    {
        try
        {
            var response = await _gamesContainer.ReadItemAsync<GameDataDocument>(
                $"data-{gameId}", 
                new PartitionKey(gameId));
                
            return response.Resource.CompressedData;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading game {GameId}", gameId);
            return null;
        }
    }

    public async Task<List<GameSaveMetadataEntity>> GetGamesAsync(string? startedBy = null)
    {
        // Use new filter method with legacy behavior (exclude GameOver)
        var filter = GameStateFilter.NotGameOver;
        return await GetGamesByStateAsync(filter, startedBy);
    }

    public async Task<List<GameSaveMetadataEntity>> GetGamesByStateAsync(GameStateFilter stateFilter, string? startedBy = null)
    {
        try
        {
            var queryBuilder = new StringBuilder("SELECT * FROM c WHERE c.documentType = @documentType");
            var query = new QueryDefinition(queryBuilder.ToString())
                .WithParameter("@documentType", "game-metadata");

            var parameterIndex = 0;

            // Apply state filters
            if (stateFilter.ActiveGamesOnly)
            {
                queryBuilder.Append(" AND c.gameState != @gameOverState");
                query = query.WithParameter("@gameOverState", "GameOver");
            }
            else if (stateFilter.CompletedGamesOnly)
            {
                queryBuilder.Append(" AND c.gameState = @gameOverState");
                query = query.WithParameter("@gameOverState", "GameOver");
            }
            else
            {
                // Handle include/exclude states
                if (stateFilter.IncludeStates?.Any() == true)
                {
                    var includeParams = stateFilter.IncludeStates.Select(s => $"@includeState{parameterIndex++}").ToList();
                    queryBuilder.Append($" AND c.gameState IN ({string.Join(", ", includeParams)})");
                    
                    parameterIndex = 0;
                    foreach (var state in stateFilter.IncludeStates)
                    {
                        query = query.WithParameter($"@includeState{parameterIndex++}", state);
                    }
                }

                if (stateFilter.ExcludeStates?.Any() == true)
                {
                    var excludeParams = stateFilter.ExcludeStates.Select(s => $"@excludeState{parameterIndex++}").ToList();
                    queryBuilder.Append($" AND c.gameState NOT IN ({string.Join(", ", excludeParams)})");
                    
                    parameterIndex = 0;
                    foreach (var state in stateFilter.ExcludeStates)
                    {
                        query = query.WithParameter($"@excludeState{parameterIndex++}", state);
                    }
                }
            }

            // Apply game type filter
            if (stateFilter.GameTypes?.Any() == true)
            {
                var typeParams = stateFilter.GameTypes.Select(t => $"@gameType{parameterIndex++}").ToList();
                queryBuilder.Append($" AND c.gameType IN ({string.Join(", ", typeParams)})");
                
                parameterIndex = 0;
                foreach (var type in stateFilter.GameTypes)
                {
                    query = query.WithParameter($"@gameType{parameterIndex++}", type);
                }
            }

            // Apply date range filters
            if (stateFilter.SavedAfter.HasValue)
            {
                queryBuilder.Append(" AND c.savedAt >= @savedAfter");
                query = query.WithParameter("@savedAfter", stateFilter.SavedAfter.Value);
            }

            if (stateFilter.SavedBefore.HasValue)
            {
                queryBuilder.Append(" AND c.savedAt <= @savedBefore");
                query = query.WithParameter("@savedBefore", stateFilter.SavedBefore.Value);
            }

            // Apply startedBy filter
            if (!string.IsNullOrEmpty(startedBy) && startedBy != "*")
            {
                queryBuilder.Append(" AND c.startedBy = @startedBy");
                query = query.WithParameter("@startedBy", startedBy);
            }

            // Order by savedAt descending
            queryBuilder.Append(" ORDER BY c.savedAt DESC");

            // Apply pagination
            if (stateFilter.Skip > 0)
            {
                queryBuilder.Append($" OFFSET {stateFilter.Skip}");
            }

            if (stateFilter.MaxResults.HasValue)
            {
                queryBuilder.Append($" LIMIT {stateFilter.MaxResults.Value}");
            }

            // Update query with final SQL
            query = new QueryDefinition(queryBuilder.ToString());
            
            // Re-add all parameters (CosmosDB requires rebuilding the query)
            query = query.WithParameter("@documentType", "game-metadata");
            
            parameterIndex = 0;
            if (stateFilter.ActiveGamesOnly || stateFilter.CompletedGamesOnly)
            {
                query = query.WithParameter("@gameOverState", "GameOver");
            }
            else
            {
                if (stateFilter.IncludeStates?.Any() == true)
                {
                    parameterIndex = 0;
                    foreach (var state in stateFilter.IncludeStates)
                    {
                        query = query.WithParameter($"@includeState{parameterIndex++}", state);
                    }
                }

                if (stateFilter.ExcludeStates?.Any() == true)
                {
                    parameterIndex = 0;
                    foreach (var state in stateFilter.ExcludeStates)
                    {
                        query = query.WithParameter($"@excludeState{parameterIndex++}", state);
                    }
                }
            }

            if (stateFilter.GameTypes?.Any() == true)
            {
                parameterIndex = 0;
                foreach (var type in stateFilter.GameTypes)
                {
                    query = query.WithParameter($"@gameType{parameterIndex++}", type);
                }
            }

            if (stateFilter.SavedAfter.HasValue)
            {
                query = query.WithParameter("@savedAfter", stateFilter.SavedAfter.Value);
            }

            if (stateFilter.SavedBefore.HasValue)
            {
                query = query.WithParameter("@savedBefore", stateFilter.SavedBefore.Value);
            }

            if (!string.IsNullOrEmpty(startedBy) && startedBy != "*")
            {
                query = query.WithParameter("@startedBy", startedBy);
            }

            var iterator = _gamesContainer.GetItemQueryIterator<GameMetadataDocument>(query);
            var games = new List<GameSaveMetadataEntity>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                
                // Convert CosmosDB documents to EF entities for compatibility
                foreach (var doc in response)
                {
                    games.Add(new GameSaveMetadataEntity
                    {
                        Id = 0, // Not used in Cosmos
                        GameId = doc.GameId,
                        GameName = doc.GameName,
                        GameState = doc.GameState,
                        StartedBy = doc.StartedBy,
                        SavedAt = doc.SavedAt,
                        CreatedAt = doc.CreatedAt,
                        GameType = doc.GameType,
                        PlayerCount = doc.PlayerCount,
                        PlayerNames = doc.PlayerNames,
                        TurnCount = doc.TurnCount,
                        GameDataId = 0, // Not used in Cosmos
                        GameData = new GameSaveDataEntity
                        {
                            Size = doc.DataSize,
                            CompressedData = [] // Not loaded for listing
                        }
                    });
                }
            }

            return games;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting games with filter");
            return new List<GameSaveMetadataEntity>();
        }
    }

    public async Task<DatabaseHealthStatus> GetHealthAsync()
    {
        try
        {
            // Test database connectivity
            await _database.ReadAsync();
            
            // Count players
            var playerCountQuery = new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.documentType = @documentType")
                .WithParameter("@documentType", "player");
            var playerIterator = _playersContainer.GetItemQueryIterator<int>(playerCountQuery);
            var playerResponse = await playerIterator.ReadNextAsync();
            var playerCount = playerResponse.FirstOrDefault();

            // Count games
            var gameCountQuery = new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.documentType = @documentType")
                .WithParameter("@documentType", "game-metadata");
            var gameIterator = _gamesContainer.GetItemQueryIterator<int>(gameCountQuery);
            var gameResponse = await gameIterator.ReadNextAsync();
            var gameCount = gameResponse.FirstOrDefault();

            return new DatabaseHealthStatus
            {
                Healthy = true,
                Status = "healthy",
                PlayerCount = playerCount,
                GameCount = gameCount,
                NeedsSeeding = playerCount == 0,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new DatabaseHealthStatus
            {
                Healthy = false,
                Status = "error",
                Error = ex.Message,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    // Image operations follow similar pattern...
    // ... additional methods
    
    public void Dispose()
    {
        _cosmosClient?.Dispose();
    }
}
```

## Dependency Injection Configuration

### Startup Configuration

```csharp
// Program.cs or ServiceCollectionExtensions.cs
public static class DataAccessServiceCollectionExtensions
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment? environment = null)
    {
        // Register configuration provider
        services.AddSingleton<IDataConfiguration>(provider => 
            new DataConfigurationProvider(configuration, environment));

        // Register appropriate repository based on configuration
        services.AddSingleton<IDataRepository>(provider =>
        {
            var config = provider.GetRequiredService<IDataConfiguration>();
            var logger = provider.GetRequiredService<ILoggerFactory>();

            return config.StorageType switch
            {
                DataStorageType.SQLite => new SQLiteDataRepository(provider, logger.CreateLogger<SQLiteDataRepository>()),
                DataStorageType.CosmosDB => new CosmosDBDataRepository(config, logger.CreateLogger<CosmosDBDataRepository>()),
                _ => throw new NotSupportedException($"Storage type {config.StorageType} is not supported")
            };
        });

        // Register Entity Framework for SQLite (will be no-op for CosmosDB)
        services.AddDbContext<CatanDbContext>((provider, options) =>
        {
            var config = provider.GetRequiredService<IDataConfiguration>();
            if (config.StorageType == DataStorageType.SQLite)
            {
                options.UseSqlite(config.ConnectionString);
            }
        }, ServiceLifetime.Scoped);

        // Keep existing IGamePersistence for backward compatibility
        services.AddScoped<IGamePersistence, DataRepositoryGamePersistenceAdapter>();

        return services;
    }
}

/// <summary>
/// Adapter to maintain backward compatibility with existing IGamePersistence interface
/// </summary>
public class DataRepositoryGamePersistenceAdapter : IGamePersistence
{
    private readonly IDataRepository _dataRepository;

    public DataRepositoryGamePersistenceAdapter(IDataRepository dataRepository)
    {
        _dataRepository = dataRepository;
    }

    public Task<bool> SaveAsync(string gameId, byte[] data, GameMetadata metadata) =>
        _dataRepository.SaveGameAsync(gameId, data, metadata);

    public Task<byte[]?> LoadAsync(string gameId) =>
        _dataRepository.LoadGameAsync(gameId);

    public Task<List<GameSaveMetadataEntity>> GetGamesAsync(string? startedBy = null) =>
        _dataRepository.GetGamesAsync(startedBy);

    public Task<bool> DeleteAsync(string gameId) =>
        _dataRepository.DeleteGameAsync(gameId);
}
```

## Health and Statistics APIs

The design includes comprehensive health check and statistics APIs that are exposed through the existing GameApiController:

### Health Check Endpoint

```csharp
/// <summary>
/// Enhanced health endpoint with comprehensive statistics
/// GET /api/database/health - existing endpoint with expanded functionality
/// GET /api/database/stats - new detailed statistics endpoint
/// </summary>
[HttpGet("database/stats")]
public async Task<IActionResult> GetDatabaseStatistics()
{
    try
    {
        var dataRepository = HttpContext.RequestServices.GetRequiredService<IDataRepository>();
        var health = await dataRepository.GetHealthAsync();
        
        // Get additional statistics
        var players = await dataRepository.GetPlayersAsync();
        var games = await dataRepository.GetGamesAsync();
        
        var stats = new
        {
            healthy = health.Healthy,
            status = health.Status,
            storageType = dataRepository.GetType().Name.Replace("DataRepository", ""),
            players = new
            {
                total = health.PlayerCount,
                withImages = players.Count(p => !string.IsNullOrEmpty(p.ImageUri)),
                recentlyActive = players.Count(p => p.LifetimeStats.GamesPlayed > 0)
            },
            games = new
            {
                total = health.GameCount,
                byState = games.GroupBy(g => g.GameState)
                    .ToDictionary(g => g.Key, g => g.Count()),
                byType = games.GroupBy(g => g.GameType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                averageTurns = games.Any() ? games.Average(g => g.TurnCount) : 0,
                totalDataSize = games.Sum(g => g.GameData?.Size ?? 0)
            },
            timestamp = health.Timestamp,
            needsSeeding = health.NeedsSeeding,
            error = health.Error
        };

        return Ok(stats);
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { error = ex.Message, timestamp = DateTime.UtcNow });
    }
}

/// <summary>
/// Lightweight health check for monitoring systems
/// GET /api/health - simple up/down check
/// </summary>
[HttpGet("health")]
public async Task<IActionResult> GetSimpleHealth()
{
    try
    {
        var dataRepository = HttpContext.RequestServices.GetRequiredService<IDataRepository>();
        var health = await dataRepository.GetHealthAsync();
        
        return Ok(new
        {
            status = health.Healthy ? "healthy" : "unhealthy",
            storage = dataRepository.GetType().Name.Replace("DataRepository", "").ToLowerInvariant(),
            timestamp = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        return StatusCode(503, new { status = "unhealthy", error = ex.Message });
    }
}
```

### Enhanced Health Status Model

```csharp
/// <summary>
/// Extended health status with detailed statistics
/// </summary>
public class DatabaseHealthStatus
{
    public bool Healthy { get; set; }
    public string Status { get; set; } = string.Empty;
    public int PlayerCount { get; set; }
    public int GameCount { get; set; }
    public bool NeedsSeeding { get; set; }
    public string? Error { get; set; }
    public DateTime Timestamp { get; set; }
    
    // Additional statistics
    public long TotalDataSize { get; set; }
    public Dictionary<string, int> GamesByState { get; set; } = new();
    public Dictionary<string, int> GamesByType { get; set; } = new();
    public double AverageTurnsPerGame { get; set; }
    public int PlayersWithImages { get; set; }
    public DateTime? LastGameSaved { get; set; }
    public DateTime? OldestGame { get; set; }
}
```

## Configuration Examples

### Local Development - ZERO CONFIG

No configuration needed! Just run `dotnet run` and it automatically uses SQLite:

```json
{
  // No database configuration needed for localhost!
  // Automatically uses SQLite at Data/catan.db
}
```

### Optional Local Override (appsettings.json)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=MyCustomPath/catan.db"
  }
}
```

### Azure App Service Configuration

```json
{
  "DATABASE_MODE": "azure",
  "COSMOS_CONNECTION_STRING": "AccountEndpoint=https://catan-cosmos.documents.azure.com:443/;AccountKey=...",
  "COSMOS_DATABASE_NAME": "catan-prod",
  "COSMOS_CONTAINER_PREFIX": "catan",
  "COSMOS_PARTITION_KEY": "/partitionKey",
  "COSMOS_THROUGHPUT_MODE": "serverless"
}
```

### Environment Variables for Azure (Auto-detected)

```bash
# Azure App Service automatically detected - no DATABASE_MODE needed!
COSMOS_CONNECTION_STRING=AccountEndpoint=https://...
COSMOS_DATABASE_NAME=catan-prod
COSMOS_CONTAINER_PREFIX=catan

# Optional explicit override if needed
DATABASE_MODE=azure
```

## Migration Strategy

### Phase 1: Interface Implementation

1. **Create abstraction interfaces** - `IDataRepository`, `IDataConfiguration`
2. **Implement SQLite version** - Wrap existing `CatanDbContext` usage
3. **Update DI registration** - Use factory pattern to select implementation
4. **Test thoroughly** - Ensure no behavioral changes for existing SQLite path

### Phase 2: CosmosDB Implementation  

1. **Create CosmosDB documents** - Map existing entities to Cosmos documents
2. **Implement CosmosDB repository** - Following same interface
3. **Add configuration detection** - Auto-detect Azure vs local environment
4. **Test CosmosDB in isolation** - Unit tests with Cosmos emulator

### Phase 3: Integration and Deployment

1. **Update Azure deployment scripts** - Provision CosmosDB resources
2. **Configure Azure App Service** - Environment variables for Cosmos connection
3. **Test full deployment pipeline** - Local → Azure migration testing
4. **Performance testing** - Ensure query performance meets requirements

### Phase 4: Advanced Features (Future)

1. **Connection pooling optimization** - Cosmos client singleton management
2. **Caching layer** - Redis for frequently accessed data
3. **Multi-region support** - Cosmos global distribution
4. **Cost optimization** - Autoscale throughput based on usage

## Performance Considerations

### SQLite Optimizations

- **Connection pooling** - EF Core handles this automatically
- **Batch operations** - Use transactions for multi-entity operations
- **Index optimization** - Ensure proper indexes on query fields
- **File locking** - Handle concurrent access appropriately

### CosmosDB Optimizations

- **Partition key design** - Use gameId for games, logical groupings for others
- **Query optimization** - Use parameterized queries, avoid cross-partition
- **Throughput management** - Start with serverless, scale as needed
- **Connection management** - Singleton CosmosClient with connection pooling
- **Batch operations** - Use transactional batches where possible
- **Point reads** - Prefer item reads over queries when possible

### Cross-Cutting Optimizations

- **Caching** - Add memory cache for frequently accessed players/metadata
- **Async patterns** - All database operations are async
- **Connection resilience** - Retry policies for transient failures
- **Monitoring** - Log performance metrics and query costs
- **Health endpoints** - `/api/health` for monitoring, `/api/database/stats` for detailed statistics
- **Zero configuration** - Localhost automatically uses SQLite, Azure auto-detects CosmosDB

## Testing Strategy

### Unit Testing

```csharp
[TestFixture]
public class DataRepositoryTests
{
    private IDataRepository _sqliteRepository;
    private IDataRepository _cosmosRepository;

    [SetUp]
    public void Setup()
    {
        // SQLite in-memory database for testing
        var sqliteConfig = new TestDataConfiguration(DataStorageType.SQLite, ":memory:");
        _sqliteRepository = new SQLiteDataRepository(/* test service provider */, logger);

        // CosmosDB emulator for testing
        var cosmosConfig = new TestDataConfiguration(DataStorageType.CosmosDB, "https://localhost:8081/...");
        _cosmosRepository = new CosmosDBDataRepository(cosmosConfig, logger);
    }

    [Test]
    [TestCase(typeof(SQLiteDataRepository))]
    [TestCase(typeof(CosmosDBDataRepository))]
    public async Task CreatePlayer_Should_SaveAndRetrieve(Type repositoryType)
    {
        var repository = repositoryType == typeof(SQLiteDataRepository) ? _sqliteRepository : _cosmosRepository;
        
        var player = new PlayerProfile("test-001", "Test Player", new PlayerColors(), "/images/test.jpg", new LifetimeStats());
        
        var created = await repository.CreatePlayerAsync(player);
        Assert.That(created, Is.True);

        var retrieved = await repository.GetPlayerAsync(player.Id);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved.Name, Is.EqualTo(player.Name));
    }

    // Similar tests for all operations ensuring both implementations behave identically
}
```

### Integration Testing

- **Full stack tests** - Test complete game save/load cycle
- **Environment switching** - Test configuration detection logic
- **Azure deployment tests** - Verify behavior in actual Azure environment
- **Performance benchmarks** - Compare SQLite vs CosmosDB performance

## Monitoring and Diagnostics

### Health Checks and Monitoring

```csharp
public class DataRepositoryHealthCheck : IHealthCheck
{
    private readonly IDataRepository _dataRepository;

    public DataRepositoryHealthCheck(IDataRepository dataRepository)
    {
        _dataRepository = dataRepository;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await _dataRepository.GetHealthAsync();
            var storageType = _dataRepository.GetType().Name.Replace("DataRepository", "");
            
            if (health.Healthy)
            {
                var data = new Dictionary<string, object>
                {
                    ["storage"] = storageType,
                    ["players"] = health.PlayerCount,
                    ["games"] = health.GameCount,
                    ["needsSeeding"] = health.NeedsSeeding,
                    ["dataSize"] = health.TotalDataSize
                };
                
                return HealthCheckResult.Healthy(
                    $"{storageType} healthy - {health.PlayerCount} players, {health.GameCount} games", 
                    data);
            }
            else
            {
                return HealthCheckResult.Unhealthy($"{storageType} unhealthy: {health.Error}");
            }
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Health check failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Extension methods for easy health check registration
/// </summary>
public static class HealthCheckExtensions
{
    public static IServiceCollection AddDataRepositoryHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DataRepositoryHealthCheck>("database")
            .AddCheck("storage-type", () =>
            {
                var config = services.BuildServiceProvider().GetRequiredService<IDataConfiguration>();
                return HealthCheckResult.Healthy($"Using {config.StorageType} storage");
            });

        return services;
    }
}
```

### API Endpoints for Monitoring

The following endpoints are automatically available:

| Endpoint | Purpose | Response |
|----------|---------|----------|
| `GET /api/health` | Simple up/down status | `{ "status": "healthy", "storage": "sqlite" }` |
| `GET /api/database/health` | Existing endpoint with player/game counts | `{ "healthy": true, "playerCount": 4, "gameCount": 12 }` |
| `GET /api/database/stats` | Detailed statistics and analytics | Full breakdown by game state, type, sizes, etc. |
| `GET /health` | ASP.NET Core health checks | Built-in health check endpoint |

### WebUI.ps1 Integration

The health endpoints integrate with existing `webui.ps1` commands:

```powershell
# webui.ps1 database doctor uses /api/database/health endpoint
./webui.ps1 database doctor

# Enhanced output shows storage type automatically
Database health: GOOD (SQLite)
  Players: 4 players configured
  Games: 12 game(s) in database
  Storage: SQLite (Data/catan.db)
```

### Logging and Metrics

- **Structured logging** - Use consistent log fields across implementations
- **Performance metrics** - Query duration, RU consumption (Cosmos), record counts
- **Error tracking** - Connection failures, timeout handling
- **Cost monitoring** - Cosmos DB request units, storage costs

## Security Considerations

### SQLite Security

- **File permissions** - Ensure database file is properly protected
- **Connection string security** - Store in secure configuration
- **SQL injection prevention** - Use parameterized queries (EF handles this)

### CosmosDB Security

- **Managed Identity** - Prefer over connection strings in Azure
- **Key rotation** - Support for rotating Cosmos account keys
- **Network security** - VNet integration for production environments
- **RBAC** - Use Cosmos DB built-in roles for fine-grained access
- **Encryption** - Data encrypted at rest and in transit by default

## Cost Optimization

### CosmosDB Cost Management

- **Serverless mode** - Best for development and low-traffic scenarios
- **Provision throughput** - For predictable high-traffic workloads
- **Request unit optimization** - Efficient queries and indexing
- **Time-to-live** - Auto-delete old game saves and logs
- **Archival strategy** - Move old data to cheaper storage tiers

### Resource Management

- **Connection pooling** - Minimize connection overhead
- **Batch operations** - Reduce transaction costs
- **Monitoring** - Track costs and set up alerts
- **Testing** - Use emulator for development to avoid charges

## Conclusion

This design provides a comprehensive data access layer that seamlessly supports both SQLite for local development and CosmosDB for Azure deployment. The interface abstraction ensures that business logic remains unchanged while providing flexibility to optimize for each storage backend.

Key benefits:

- **Seamless switching** between local and cloud storage
- **Full offline capability** for inner loop development
- **Performance optimization** for each storage type
- **Backward compatibility** with existing codebase
- **Comprehensive testing** strategy for reliability
- **Cost-effective** CosmosDB usage patterns

The phased migration approach minimizes risk while enabling rapid deployment to Azure with full cloud-native capabilities.

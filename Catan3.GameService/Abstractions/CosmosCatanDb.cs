using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Identity;
using Catan3.Shared.Models;
using Catan3.Shared.Profiles;
using Catan3.Shared.Services;
using Catan3.Shared.Utility;
using Microsoft.Azure.Cosmos;

namespace Catan3.GameService.Abstractions;

/// <summary>
/// CosmosDB implementation of ICatanDb.
/// CosmosClient is singleton (thread-safe, manages connection pooling).
/// CosmosCatanDb itself is scoped (lightweight, no state beyond container references).
///
/// Document serialization note:
/// CosmosClientOptions.UseSystemTextJsonSerializerWithOptions (JsonNamingPolicy.CamelCase)
/// configures the SDK to use STJ for all document serialization. Doc classes use
/// [JsonPropertyName] attributes. Newtonsoft.Json is still a direct package reference
/// because the SDK requires it for internal metadata operations (see MS best practices).
/// Complex shared types (PlayerProfile, GameTemplateData) are still stored as STJ-serialized
/// strings to decouple the doc schema from the shared model evolution — see profileJson,
/// bag of primitives.
/// </summary>
public sealed class CosmosCatanDb : ICatanDb
{
    private readonly CosmosClient _client;
    private readonly string _databaseName;

    private Container _players = null!;
    private Container _games = null!;
    private Container _completedGames = null!;
    private Container _templates = null!;
    private Container _recordings = null!;

    public CosmosCatanDb(CosmosClient client, string databaseName)
    {
        _client = client;
        _databaseName = databaseName;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    // Shared write options — suppresses returning the full document body on upsert,
    // saving serialization cost and network bandwidth (best practice: write-heavy workloads).
    private static readonly ItemRequestOptions _writeOptions = new()
    {
        EnableContentResponseOnWrite = false,
    };

    public async Task InitializeAsync()
    {
        var db = (await _client.CreateDatabaseIfNotExistsAsync(_databaseName)).Database;
        _players        = (await db.CreateContainerIfNotExistsAsync("players",         "/id")).Container;
        _games          = (await db.CreateContainerIfNotExistsAsync("games",            "/id")).Container;
        _completedGames = (await db.CreateContainerIfNotExistsAsync("completed-games",  "/id")).Container;
        _templates      = (await db.CreateContainerIfNotExistsAsync("templates",        "/id")).Container;
        _recordings     = (await db.CreateContainerIfNotExistsAsync("recordings",       "/id")).Container;
    }

    // ── Players ───────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<PlayerProfile>> LoadPlayersAsync()
    {
        var results = new List<PlayerProfile>();
        var iter = _players.GetItemQueryIterator<PlayerDoc>("SELECT * FROM c");
        while (iter.HasMoreResults)
            foreach (var doc in await iter.ReadNextAsync())
            {
                var p = DeserializeProfile(doc.ProfileJson);
                if (p is not null) results.Add(p);
            }
        return results;
    }

    public async Task<PlayerProfile?> LoadPlayerAsync(string id)
    {
        var doc = await TryReadAsync<PlayerDoc>(_players, id);
        return doc is null ? null : DeserializeProfile(doc.ProfileJson);
    }

    public async Task SavePlayerAsync(PlayerProfile player)
    {
        var doc = await TryReadAsync<PlayerDoc>(_players, player.Id) ?? new PlayerDoc { Id = player.Id };
        doc.ProfileJson = JsonHelper.Serialize(player);
        await _players.UpsertItemAsync(doc, new PartitionKey(player.Id), _writeOptions);
    }

    public async Task DeletePlayerAsync(string id) => await TryDeleteAsync(_players, id);

    // ── Images ────────────────────────────────────────────────────────────────

    public async Task<(byte[] Data, string ContentType)?> LoadImageAsync(string playerId)
    {
        var doc = await TryReadAsync<PlayerDoc>(_players, playerId);
        if (doc?.ImageData is null || doc.ImageContentType is null) return null;
        return (Convert.FromBase64String(doc.ImageData), doc.ImageContentType);
    }

    public async Task SaveImageAsync(string playerId, byte[] data, string contentType)
    {
        var doc = await TryReadAsync<PlayerDoc>(_players, playerId) ?? new PlayerDoc { Id = playerId };
        doc.ImageData = Convert.ToBase64String(data);
        doc.ImageContentType = contentType;
        await _players.UpsertItemAsync(doc, new PartitionKey(playerId), _writeOptions);
    }

    public async Task DeleteImageAsync(string playerId)
    {
        var doc = await TryReadAsync<PlayerDoc>(_players, playerId);
        if (doc is null) return;
        doc.ImageData = null;
        doc.ImageContentType = null;
        await _players.UpsertItemAsync(doc, new PartitionKey(playerId), _writeOptions);
    }

    // ── Game saves ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<GameSummary>> ListGamesAsync(string? startedBy = null)
    {
        // SELECT only summary columns — omit compressedData to keep response small
        const string select = "SELECT c.id, c.gameName, c.gameState, c.gameType, c.startedBy, " +
                              "c.playerCount, c.playerNames, c.turnCount, c.savedAt, c.createdAt, c.size FROM c";

        QueryDefinition query = string.IsNullOrEmpty(startedBy)
            ? new QueryDefinition(select)
            : new QueryDefinition(select + " WHERE c.startedBy = @by").WithParameter("@by", startedBy);

        var results = new List<GameSummary>();
        var iter = _games.GetItemQueryIterator<GameDoc>(query);
        while (iter.HasMoreResults)
            foreach (var doc in await iter.ReadNextAsync())
                results.Add(DocToSummary(doc));
        return results;
    }

    public async Task<GameSaveData?> LoadGameAsync(string gameId)
    {
        var doc = await TryReadAsync<GameDoc>(_games, gameId);
        return doc is null ? null : DocToSaveData(doc);
    }

    public async Task SaveGameAsync(GameSaveData game)
    {
        await _games.UpsertItemAsync(SaveDataToDoc(game), new PartitionKey(game.GameId), _writeOptions);
    }

    public async Task DeleteGameAsync(string gameId) => await TryDeleteAsync(_games, gameId);

    public async Task<int> CountGamesAsync()
    {
        var iter = _games.GetItemQueryIterator<int>("SELECT VALUE COUNT(1) FROM c");
        if (iter.HasMoreResults)
        {
            var page = await iter.ReadNextAsync();
            return page.FirstOrDefault();
        }
        return 0;
    }

    // ── Completed games ───────────────────────────────────────────────────────

    public async Task SaveCompletedGameAsync(CompletedGameRecord game)
    {
        var doc = new CompletedGameDoc
        {
            Id             = game.GameId,
            GameId         = game.GameId,
            GameName       = game.GameName,
            WinnerId       = game.WinnerId,
            WinnerName     = game.WinnerName,
            CompletedAt    = game.CompletedAt,
            StartedAt      = game.StartedAt,
            PlayerCount    = game.PlayerCount,
            TurnCount      = game.TurnCount,
            PlayerNames    = game.PlayerNames,
            CompressedData = game.CompressedData.Length > 0 ? Convert.ToBase64String(game.CompressedData) : null,
            Size           = game.Size,
        };
        await _completedGames.UpsertItemAsync(doc, new PartitionKey(game.GameId), _writeOptions);
    }

    public async Task<IReadOnlyList<CompletedGameRecord>> ListCompletedGamesAsync()
    {
        var results = new List<CompletedGameRecord>();
        var iter = _completedGames.GetItemQueryIterator<CompletedGameDoc>("SELECT * FROM c");
        while (iter.HasMoreResults)
            foreach (var doc in await iter.ReadNextAsync())
                results.Add(DocToCompletedRecord(doc));
        return results;
    }

    // ── Templates ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<GameTemplateSummary>> ListTemplatesAsync(string? category = null)
    {
        // Project only summary columns — omit dataJson to avoid large payload on list
        const string select = "SELECT c.id, c.name, c.category, c.isSystemTemplate, c.description, " +
                              "c.minPlayers, c.maxPlayers, c.updatedAt FROM c";
        QueryDefinition query = string.IsNullOrEmpty(category)
            ? new QueryDefinition(select)
            : new QueryDefinition(select + " WHERE c.category = @cat").WithParameter("@cat", category);

        var results = new List<GameTemplateSummary>();
        var iter = _templates.GetItemQueryIterator<TemplateDoc>(query);
        while (iter.HasMoreResults)
            foreach (var doc in await iter.ReadNextAsync())
                results.Add(DocToTemplateSummary(doc));
        return results;
    }

    public async Task<GameTemplateData?> LoadTemplateAsync(string id)
    {
        var doc = await TryReadAsync<TemplateDoc>(_templates, id);
        if (doc is null) return null;
        // DataJson is the STJ-serialized GameTemplateData
        return doc.DataJson is null ? null : JsonHelper.Deserialize<GameTemplateData>(doc.DataJson);
    }

    public async Task SaveTemplateAsync(
        string id, string name, string category,
        bool isSystemTemplate, GameTemplateData data)
    {
        var existing = await TryReadAsync<TemplateDoc>(_templates, id);
        var now = DateTime.UtcNow;

        // Normalize embedded payload so LoadTemplateAsync and ListTemplatesAsync
        // always agree: the scalar parameters are the authoritative source for
        // identity fields; update data before serializing.
        data.Id       = id;
        data.Name     = name;
        data.Category = category;

        var doc = new TemplateDoc
        {
            Id               = id,
            Name             = name,
            Category         = category,
            IsSystemTemplate = isSystemTemplate,
            Description      = data.Description,
            MinPlayers       = data.ResourceRules.MinPlayers,
            MaxPlayers       = data.ResourceRules.MaxPlayers,
            UpdatedAt        = now,
            CreatedAt        = existing?.CreatedAt ?? now,
            DataJson         = JsonHelper.Serialize(data),
        };
        await _templates.UpsertItemAsync(doc, new PartitionKey(id), _writeOptions);
    }

    public async Task DeleteTemplateAsync(string id) => await TryDeleteAsync(_templates, id);

    // ── Recordings ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<GameServiceProxy.RecordingSummary>> ListRecordingsAsync()
    {
        // Project only summary columns — omit data to avoid large payload on list
        const string select = "SELECT c.id, c.name, c.createdAt, c.gameType, c.playerCount, c.actionCount, c.gameId FROM c";
        var results = new List<GameServiceProxy.RecordingSummary>();
        var iter = _recordings.GetItemQueryIterator<RecordingDoc>(select);
        while (iter.HasMoreResults)
            foreach (var doc in await iter.ReadNextAsync())
                results.Add(DocToRecordingSummary(doc));
        return results;
    }

    public async Task<(GameServiceProxy.RecordingSummary Summary, string Data)?> LoadRecordingAsync(string id)
    {
        var doc = await TryReadAsync<RecordingDoc>(_recordings, id);
        if (doc is null) return null;
        return (DocToRecordingSummary(doc), doc.Data);
    }

    public async Task<(GameServiceProxy.RecordingSummary Summary, string Data)?> FindRecordingByGameIdAsync(string gameId)
    {
        var iter = _recordings.GetItemQueryIterator<RecordingDoc>(
            new QueryDefinition("SELECT * FROM c WHERE c.gameId = @id").WithParameter("@id", gameId));
        while (iter.HasMoreResults)
            foreach (var doc in await iter.ReadNextAsync())
                return (DocToRecordingSummary(doc), doc.Data);
        return null;
    }

    public async Task SaveRecordingAsync(GameServiceProxy.RecordingSummary summary, string data)
    {
        var doc = new RecordingDoc
        {
            Id          = summary.Id,
            Name        = summary.Name,
            CreatedAt   = summary.CreatedAt,
            GameType    = summary.GameType,
            PlayerCount = summary.PlayerCount,
            ActionCount = summary.ActionCount,
            GameId      = summary.GameId,
            Data        = data,
        };
        await _recordings.UpsertItemAsync(doc, new PartitionKey(summary.Id), _writeOptions);
    }

    public async Task DeleteRecordingAsync(string id) => await TryDeleteAsync(_recordings, id);

    public async Task DeleteRecordingsByGameIdAsync(string gameId)
    {
        var ids = new List<string>();
        var iter = _recordings.GetItemQueryIterator<RecordingDoc>(
            new QueryDefinition("SELECT c.id FROM c WHERE c.gameId = @id").WithParameter("@id", gameId));
        while (iter.HasMoreResults)
            foreach (var doc in await iter.ReadNextAsync())
                ids.Add(doc.Id);
        foreach (var id in ids)
            await TryDeleteAsync(_recordings, id);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static PlayerProfile? DeserializeProfile(string? json) =>
        json is null ? null : JsonHelper.Deserialize<PlayerProfile>(json);

    private static async Task<T?> TryReadAsync<T>(Container container, string id)
    {
        try
        {
            return (await container.ReadItemAsync<T>(id, new PartitionKey(id))).Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return default;
        }
    }

    private static async Task TryDeleteAsync(Container container, string id)
    {
        try
        {
            await container.DeleteItemAsync<object>(id, new PartitionKey(id));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Already gone — treat as success
        }
    }

    private static GameSummary DocToSummary(GameDoc doc) => new()
    {
        GameId      = doc.Id,
        GameName    = doc.GameName,
        GameState   = doc.GameState,
        GameType    = doc.GameType,
        StartedBy   = doc.StartedBy,
        PlayerCount = doc.PlayerCount,
        PlayerNames = doc.PlayerNames,
        TurnCount   = doc.TurnCount,
        SavedAt     = doc.SavedAt,
        CreatedAt   = doc.CreatedAt,
        Size        = doc.Size,
    };

    private static GameSaveData DocToSaveData(GameDoc doc) => new()
    {
        GameId         = doc.Id,
        GameName       = doc.GameName,
        GameState      = doc.GameState,
        GameType       = doc.GameType,
        StartedBy      = doc.StartedBy,
        PlayerCount    = doc.PlayerCount,
        PlayerNames    = doc.PlayerNames,
        TurnCount      = doc.TurnCount,
        SavedAt        = doc.SavedAt,
        CreatedAt      = doc.CreatedAt,
        Size           = doc.Size,
        CompressedData = doc.CompressedData is null ? [] : Convert.FromBase64String(doc.CompressedData),
    };

    private static GameDoc SaveDataToDoc(GameSaveData g) => new()
    {
        Id             = g.GameId,
        GameName       = g.GameName,
        GameState      = g.GameState,
        GameType       = g.GameType,
        StartedBy      = g.StartedBy,
        PlayerCount    = g.PlayerCount,
        PlayerNames    = g.PlayerNames,
        TurnCount      = g.TurnCount,
        SavedAt        = g.SavedAt,
        CreatedAt      = g.CreatedAt,
        Size           = g.Size,
        CompressedData = g.CompressedData.Length > 0 ? Convert.ToBase64String(g.CompressedData) : null,
    };

    private static CompletedGameRecord DocToCompletedRecord(CompletedGameDoc doc) => new()
    {
        GameId         = doc.Id,
        GameName       = doc.GameName,
        WinnerId       = doc.WinnerId,
        WinnerName     = doc.WinnerName,
        CompletedAt    = doc.CompletedAt,
        StartedAt      = doc.StartedAt,
        PlayerCount    = doc.PlayerCount,
        TurnCount      = doc.TurnCount,
        PlayerNames    = doc.PlayerNames,
        CompressedData = doc.CompressedData is null ? [] : Convert.FromBase64String(doc.CompressedData),
        Size           = doc.Size,
    };

    private static GameTemplateSummary DocToTemplateSummary(TemplateDoc doc) => new()
    {
        Id               = doc.Id,
        Name             = doc.Name,
        Category         = doc.Category,
        IsSystemTemplate = doc.IsSystemTemplate,
        Description      = doc.Description,
        MinPlayers       = doc.MinPlayers,
        MaxPlayers       = doc.MaxPlayers,
        UpdatedAt        = doc.UpdatedAt,
    };

    private static GameServiceProxy.RecordingSummary DocToRecordingSummary(RecordingDoc doc) => new()
    {
        Id          = doc.Id,
        Name        = doc.Name,
        CreatedAt   = doc.CreatedAt,
        GameType    = doc.GameType,
        PlayerCount = doc.PlayerCount,
        ActionCount = doc.ActionCount,
        GameId      = doc.GameId,
    };

    // ── Cosmos document shapes ────────────────────────────────────────────────
    //
    // The SDK serializes these via STJ (UseSystemTextJsonSerializerWithOptions).
    // [JsonPropertyName] controls the JSON field name in CosmosDB. The "id" field
    // must be lowercase — CosmosDB treats it as the document key.

    private class PlayerDoc
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>STJ-serialized PlayerProfile (avoids Newtonsoft vs STJ attribute conflicts).</summary>
        [JsonPropertyName("profileJson")]
        public string? ProfileJson { get; set; }

        [JsonPropertyName("imageData")]
        public string? ImageData { get; set; }

        [JsonPropertyName("imageContentType")]
        public string? ImageContentType { get; set; }
    }

    private class GameDoc
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("gameName")]
        public string GameName { get; set; } = string.Empty;

        [JsonPropertyName("gameState")]
        public string GameState { get; set; } = string.Empty;

        [JsonPropertyName("gameType")]
        public string GameType { get; set; } = string.Empty;

        [JsonPropertyName("startedBy")]
        public string StartedBy { get; set; } = string.Empty;

        [JsonPropertyName("playerCount")]
        public int PlayerCount { get; set; }

        [JsonPropertyName("playerNames")]
        public string PlayerNames { get; set; } = string.Empty;

        [JsonPropertyName("turnCount")]
        public int TurnCount { get; set; }

        [JsonPropertyName("savedAt")]
        public DateTime SavedAt { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("size")]
        public int Size { get; set; }

        [JsonPropertyName("compressedData")]
        public string? CompressedData { get; set; }
    }

    private class CompletedGameDoc
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("gameName")]
        public string GameName { get; set; } = string.Empty;

        [JsonPropertyName("winnerId")]
        public string WinnerId { get; set; } = string.Empty;

        [JsonPropertyName("winnerName")]
        public string WinnerName { get; set; } = string.Empty;

        [JsonPropertyName("completedAt")]
        public DateTime CompletedAt { get; set; }

        [JsonPropertyName("startedAt")]
        public DateTime StartedAt { get; set; }

        [JsonPropertyName("playerCount")]
        public int PlayerCount { get; set; }

        [JsonPropertyName("playerNames")]
        public string PlayerNames { get; set; } = string.Empty;

        [JsonPropertyName("turnCount")]
        public int TurnCount { get; set; }

        // Explicit gameId mirrors id; enables cross-container queries and future scenarios
        // where the doc id diverges from the originating game id.
        [JsonPropertyName("gameId")]
        public string GameId { get; set; } = string.Empty;

        [JsonPropertyName("compressedData")]
        public string? CompressedData { get; set; }

        [JsonPropertyName("size")]
        public int Size { get; set; }
    }

    private class TemplateDoc
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [JsonPropertyName("isSystemTemplate")]
        public bool IsSystemTemplate { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("minPlayers")]
        public int MinPlayers { get; set; }

        [JsonPropertyName("maxPlayers")]
        public int MaxPlayers { get; set; }

        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        /// <summary>STJ-serialized GameTemplateData (keeps serializer consistent with rest of app).</summary>
        [JsonPropertyName("dataJson")]
        public string? DataJson { get; set; }
    }

    private class RecordingDoc
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("gameType")]
        public string GameType { get; set; } = string.Empty;

        [JsonPropertyName("playerCount")]
        public int PlayerCount { get; set; }

        [JsonPropertyName("actionCount")]
        public int ActionCount { get; set; }

        /// <summary>First-class field; indexed by Cosmos for FindRecordingByGameIdAsync query.</summary>
        [JsonPropertyName("gameId")]
        public string GameId { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public string Data { get; set; } = string.Empty;
    }
}

/// <summary>
/// Builds a CosmosClient for the active environment.
/// Detects local emulator vs. Azure via COSMOS_ENDPOINT / WEBSITE_SITE_NAME config.
/// </summary>
public static class CosmosClientFactory
{
    // STJ options shared by all client instances — camelCase matches [JsonPropertyName] attributes.
    private static readonly JsonSerializerOptions _stjOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // Singleton handler: reused across HttpClient instances inside the SDK.
    // 5-min pooled connection lifetime lets the SDK react to DNS changes on the account
    // (best practice from https://learn.microsoft.com/azure/cosmos-db/best-practice-dotnet).
    private static readonly System.Net.Http.SocketsHttpHandler _socketsHandler = new()
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    };

    public static CosmosClient Create(IConfiguration configuration)
    {
        var endpoint = configuration["COSMOS_ENDPOINT"]
            ?? throw new InvalidOperationException(
                "COSMOS_ENDPOINT is required when running in CosmosDB mode.");

        var isAzure = !string.IsNullOrEmpty(configuration["WEBSITE_SITE_NAME"]);
        if (isAzure)
        {
            return new CosmosClient(endpoint, new DefaultAzureCredential(), new CosmosClientOptions
            {
                // STJ serializer for all document reads/writes
                UseSystemTextJsonSerializerWithOptions = _stjOptions,
                // Direct mode: lower latency than Gateway for data-plane ops
                ConnectionMode = ConnectionMode.Direct,
                // DNS refresh — avoids stale connections after regional failover
                HttpClientFactory = () => new System.Net.Http.HttpClient(_socketsHandler, disposeHandler: false),
            });
        }

        // Local emulator: well-known fixed key; emulator uses HTTP (no TLS)
        var key = configuration["COSMOS_KEY"]
            ?? "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw=";

        return new CosmosClient(endpoint, key, new CosmosClientOptions
        {
            UseSystemTextJsonSerializerWithOptions = _stjOptions,
            ConnectionMode = ConnectionMode.Gateway,
            HttpClientFactory = () => new System.Net.Http.HttpClient(_socketsHandler, disposeHandler: false),
        });
    }
}

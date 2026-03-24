using Catan3.GameService.Abstractions;
using Catan3.Shared.Models;
using Catan3.Shared.Profiles;
using Catan3.Shared.Services;
using Xunit;

namespace Tests.GameService.CatanDb;

/// <summary>
/// Abstract contract test suite for ICatanDb implementations.
/// Every concrete subclass runs all 26 tests against a fresh, isolated database.
/// </summary>
public abstract class CatanDbContractTests : IAsyncLifetime
{
    protected ICatanDb Db { get; private set; } = null!;

    protected abstract Task<ICatanDb> CreateDbAsync();
    protected abstract Task DeleteDatabaseAsync();

    public async Task InitializeAsync() => Db = await CreateDbAsync();
    public async Task DisposeAsync() => await DeleteDatabaseAsync();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PlayerProfile MakePlayer(string id, string name = "Test") =>
        new PlayerProfile(id, name,
            new PlayerColors("#FF0000", "#800000", "#FFFFFF"));

    private static GameSaveData MakeGame(string gameId, string startedBy = "tester") => new()
    {
        GameId = gameId,
        GameName = $"Game {gameId}",
        GameState = "WaitingForRoll",
        GameType = "Regular",
        StartedBy = startedBy,
        PlayerCount = 3,
        PlayerNames = "Alice, Bob, Carol",
        TurnCount = 5,
        SavedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        CompressedData = [1, 2, 3, 4, 5],
        Size = 5,
    };

    private static GameServiceProxy.RecordingSummary MakeRecordingSummary(
        string id, string gameId = "g1") => new()
        {
            Id = id,
            Name = $"Recording {id}",
            CreatedAt = DateTime.UtcNow,
            GameType = "Regular",
            PlayerCount = 3,
            ActionCount = 42,
            GameId = gameId,
        };

    private static GameTemplateData MakeTemplateData(string id) => new()
    {
        Id = id,
        Name = $"Template {id}",
        Category = "Base",
        Description = "Test template",
        ResourceRules = new ResourceRules { MinPlayers = 3, MaxPlayers = 4 },
    };

    // ── Player tests ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAndLoadPlayer_RoundTrips()
    {
        var player = MakePlayer("p-001", "Alice");
        await Db.SavePlayerAsync(player);

        var loaded = await Db.LoadPlayerAsync("p-001");

        Assert.NotNull(loaded);
        Assert.Equal("p-001", loaded.Id);
        Assert.Equal("Alice", loaded.Name);
        Assert.Equal("#FF0000", loaded.Colors.Primary);
    }

    [Fact]
    public async Task LoadPlayersAsync_ReturnsAll()
    {
        await Db.SavePlayerAsync(MakePlayer("p-001", "Alice"));
        await Db.SavePlayerAsync(MakePlayer("p-002", "Bob"));

        var all = await Db.LoadPlayersAsync();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, p => p.Id == "p-001");
        Assert.Contains(all, p => p.Id == "p-002");
    }

    [Fact]
    public async Task DeletePlayerAsync_RemovesRecord()
    {
        await Db.SavePlayerAsync(MakePlayer("p-del"));
        await Db.DeletePlayerAsync("p-del");

        Assert.Null(await Db.LoadPlayerAsync("p-del"));
    }

    [Fact]
    public async Task LoadPlayerAsync_UnknownId_ReturnsNull()
    {
        Assert.Null(await Db.LoadPlayerAsync("does-not-exist"));
    }

    // ── Image tests ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAndLoadImage_RoundTrips()
    {
        await Db.SavePlayerAsync(MakePlayer("p-img"));
        var bytes = new byte[] { 10, 20, 30, 40 };
        await Db.SaveImageAsync("p-img", bytes, "image/jpeg");

        var result = await Db.LoadImageAsync("p-img");

        Assert.NotNull(result);
        Assert.Equal(bytes, result!.Value.Data);
        Assert.Equal("image/jpeg", result.Value.ContentType);
    }

    [Fact]
    public async Task DeleteImageAsync_RemovesRecord()
    {
        await Db.SavePlayerAsync(MakePlayer("p-img2"));
        await Db.SaveImageAsync("p-img2", [1, 2], "image/png");
        await Db.DeleteImageAsync("p-img2");

        Assert.Null(await Db.LoadImageAsync("p-img2"));
    }

    [Fact]
    public async Task LoadImageAsync_UnknownId_ReturnsNull()
    {
        Assert.Null(await Db.LoadImageAsync("no-such-player"));
    }

    // ── Game save tests ───────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAndLoadGame_RoundTrips()
    {
        var game = MakeGame("gid-001");
        await Db.SaveGameAsync(game);

        var loaded = await Db.LoadGameAsync("gid-001");

        Assert.NotNull(loaded);
        Assert.Equal("gid-001", loaded.GameId);
        Assert.Equal(game.GameName, loaded.GameName);
        Assert.Equal(game.CompressedData, loaded.CompressedData);
    }

    [Fact]
    public async Task ListGamesAsync_NoFilter_ReturnsAll()
    {
        await Db.SaveGameAsync(MakeGame("g-list-1", "userA"));
        await Db.SaveGameAsync(MakeGame("g-list-2", "userB"));

        var all = await Db.ListGamesAsync();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task ListGamesAsync_WithFilter_FiltersCorrectly()
    {
        await Db.SaveGameAsync(MakeGame("g-f1", "alice"));
        await Db.SaveGameAsync(MakeGame("g-f2", "bob"));

        var aliceGames = await Db.ListGamesAsync("alice");

        Assert.Single(aliceGames);
        Assert.Equal("g-f1", aliceGames[0].GameId);
    }

    [Fact]
    public async Task ListGamesAsync_IncludesGameOverGames()
    {
        var game = MakeGame("g-gameover");
        game.GameState = "GameOver";
        await Db.SaveGameAsync(game);

        var all = await Db.ListGamesAsync();

        Assert.Contains(all, g => g.GameId == "g-gameover" && g.GameState == "GameOver");
    }

    [Fact]
    public async Task SaveGameAsync_UpdatesExisting()
    {
        var game = MakeGame("g-upd");
        await Db.SaveGameAsync(game);

        game.TurnCount = 99;
        game.GameName = "Updated Name";
        await Db.SaveGameAsync(game);

        var loaded = await Db.LoadGameAsync("g-upd");
        Assert.Equal(99, loaded!.TurnCount);
        Assert.Equal("Updated Name", loaded.GameName);
    }

    [Fact]
    public async Task DeleteGameAsync_RemovesRecord()
    {
        await Db.SaveGameAsync(MakeGame("g-del"));
        await Db.DeleteGameAsync("g-del");

        Assert.Null(await Db.LoadGameAsync("g-del"));
    }

    [Fact]
    public async Task CountGamesAsync_ReturnsCorrectCount()
    {
        await Db.SaveGameAsync(MakeGame("g-cnt-1"));
        await Db.SaveGameAsync(MakeGame("g-cnt-2"));
        await Db.SaveGameAsync(MakeGame("g-cnt-3"));

        Assert.Equal(3, await Db.CountGamesAsync());
    }

    [Fact]
    public async Task ListGamesAsync_StartsWith_SupportsCopyNaming()
    {
        // Verifies that client-side StartsWith filtering works for GenerateCopyNameAsync pattern
        await Db.SaveGameAsync(MakeGame("g-copy-1") with { GameName = "Foo" });
        await Db.SaveGameAsync(MakeGame("g-copy-2") with { GameName = "Foo (Copy)" });
        await Db.SaveGameAsync(MakeGame("g-copy-3") with { GameName = "Bar" });

        var all = await Db.ListGamesAsync();
        var copies = all.Where(g => g.GameName.StartsWith("Foo")).ToList();

        Assert.Equal(2, copies.Count);
    }

    // ── Completed game tests ──────────────────────────────────────────────────

    [Fact]
    public async Task SaveAndListCompletedGames_RoundTrips()
    {
        var record = new CompletedGameRecord
        {
            GameId = "cg-001",
            GameName = "Finished Game",
            WinnerId = "p-001",
            WinnerName = "Alice",
            CompletedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            PlayerCount = 3,
            TurnCount = 20,
            PlayerNames = "Alice, Bob, Carol",
            CompressedData = [9, 8, 7],
            Size = 3,
        };
        await Db.SaveCompletedGameAsync(record);

        var all = await Db.ListCompletedGamesAsync();

        Assert.Single(all);
        Assert.Equal("cg-001", all[0].GameId);
        Assert.Equal("Alice", all[0].WinnerName);
        Assert.Equal(new byte[] { 9, 8, 7 }, all[0].CompressedData);
    }

    // ── Template tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAndLoadTemplate_RoundTrips()
    {
        var data = MakeTemplateData("tmpl-001");
        await Db.SaveTemplateAsync("tmpl-001", "Template 001", "Base", true, data);

        var loaded = await Db.LoadTemplateAsync("tmpl-001");

        Assert.NotNull(loaded);
        Assert.Equal("tmpl-001", loaded.Id);
        Assert.Equal("Test template", loaded.Description);
    }

    [Fact]
    public async Task ListTemplatesAsync_NoFilter_ReturnsAll()
    {
        await Db.SaveTemplateAsync("t1", "T1", "Base", true, MakeTemplateData("t1"));
        await Db.SaveTemplateAsync("t2", "T2", "Expansion", false, MakeTemplateData("t2"));

        var all = await Db.ListTemplatesAsync();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task ListTemplatesAsync_WithCategory_Filters()
    {
        await Db.SaveTemplateAsync("t1", "T1", "Base", true, MakeTemplateData("t1"));
        await Db.SaveTemplateAsync("t2", "T2", "Expansion", false, MakeTemplateData("t2"));

        var base_ = await Db.ListTemplatesAsync("Base");

        Assert.Single(base_);
        Assert.Equal("t1", base_[0].Id);
    }

    [Fact]
    public async Task DeleteTemplateAsync_RemovesRecord()
    {
        await Db.SaveTemplateAsync("t-del", "TDel", "Base", false, MakeTemplateData("t-del"));
        await Db.DeleteTemplateAsync("t-del");

        Assert.Null(await Db.LoadTemplateAsync("t-del"));
    }

    [Fact]
    public async Task LoadTemplateAsync_UnknownId_ReturnsNull()
    {
        Assert.Null(await Db.LoadTemplateAsync("no-such-template"));
    }

    // ── Recording tests ───────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAndLoadRecording_RoundTrips()
    {
        var summary = MakeRecordingSummary("r-001", "game-abc");
        const string data = """{"actions":[]}""";
        await Db.SaveRecordingAsync(summary, data);

        var result = await Db.LoadRecordingAsync("r-001");

        Assert.NotNull(result);
        Assert.Equal("r-001", result!.Value.Summary.Id);
        Assert.Equal("game-abc", result.Value.Summary.GameId);
        Assert.Equal(data, result.Value.Data);
    }

    [Fact]
    public async Task FindRecordingByGameIdAsync_FindsCorrectRecord()
    {
        await Db.SaveRecordingAsync(MakeRecordingSummary("r-f1", "game-x"), "{}");
        await Db.SaveRecordingAsync(MakeRecordingSummary("r-f2", "game-y"), "{}");

        var result = await Db.FindRecordingByGameIdAsync("game-x");

        Assert.NotNull(result);
        Assert.Equal("r-f1", result!.Value.Summary.Id);
    }

    [Fact]
    public async Task FindRecordingByGameIdAsync_UnknownGameId_ReturnsNull()
    {
        Assert.Null(await Db.FindRecordingByGameIdAsync("no-such-game"));
    }

    [Fact]
    public async Task DeleteRecordingsByGameIdAsync_RemovesAllMatching()
    {
        await Db.SaveRecordingAsync(MakeRecordingSummary("r-d1", "game-del"), "{}");
        await Db.SaveRecordingAsync(MakeRecordingSummary("r-d2", "game-del"), "{}");
        await Db.SaveRecordingAsync(MakeRecordingSummary("r-d3", "game-keep"), "{}");

        await Db.DeleteRecordingsByGameIdAsync("game-del");

        var all = await Db.ListRecordingsAsync();
        Assert.Single(all);
        Assert.Equal("r-d3", all[0].Id);
    }

    [Fact]
    public async Task ListRecordingsAsync_ReturnsAll()
    {
        await Db.SaveRecordingAsync(MakeRecordingSummary("r-l1", "g1"), "{}");
        await Db.SaveRecordingAsync(MakeRecordingSummary("r-l2", "g2"), "{}");

        var all = await Db.ListRecordingsAsync();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task RecordingSummary_GameId_IsStoredField_NotParsedFromJson()
    {
        // Verifies that GameId is a first-class stored field (not extracted from Data JSON)
        var summary = MakeRecordingSummary("r-gid", "explicit-game-id");
        // Data intentionally does NOT contain gameId — it must come from the summary field
        const string data = """{"actions":[], "note": "no gameId here"}""";
        await Db.SaveRecordingAsync(summary, data);

        var result = await Db.FindRecordingByGameIdAsync("explicit-game-id");

        Assert.NotNull(result);
        Assert.Equal("explicit-game-id", result!.Value.Summary.GameId);
    }
}

using System.Net;
using System.Text;
using System.Text.Json;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace Tests.GameService
{
    /// <summary>
    /// Integration tests for POST /api/game/{gameId}/replay (issue #145).
    ///
    /// The positive test reuses the longest already-seeded game (the standard
    /// workflow always seeds the DB via `./catan.ps1 database install`), replays
    /// it, and verifies the new game is a clean reset to WaitingForRollForOrder
    /// with the same board/players, fresh randomness, and a single-entry history.
    ///
    /// The negative test verifies the eligibility guard: a game that was never
    /// rolled cannot be replayed (HTTP 422).
    /// </summary>
    public class ReplayEndpointTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly ITestOutputHelper _output;

        public ReplayEndpointTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
        {
            _output = output;
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["GameApi:HangingGetTimeoutSeconds"] = "5",
                    });
                });
            });
        }

        private (Uri uri, HttpMessageHandler handler) Conn()
        {
            var uri = _factory.Server.BaseAddress ?? new Uri("http://localhost");
            return (uri, _factory.Server.CreateHandler());
        }

        private static async Task<GameModel> GetGameModelAsync(HttpClient http, string gameId)
        {
            var resp = await http.GetAsync($"/api/gamestate/{gameId}");
            var body = await resp.Content.ReadAsStringAsync();
            Assert.True(resp.IsSuccessStatusCode, $"GET /api/gamestate/{gameId} failed: {(int)resp.StatusCode} {body}");
            return JsonHelper.Deserialize<GameModel>(body)
                ?? throw new InvalidOperationException($"Could not deserialize GameModel for {gameId}");
        }

        private static string TileSig(IEnumerable<TileModel> tiles) => string.Join("|",
            tiles.Select(t => $"{t.TileKey.Q},{t.TileKey.R},{t.TileKey.S}:{t.ResourceTileType}:{t.Number}")
                 .OrderBy(s => s, StringComparer.Ordinal));

        private static string HarborSig(IEnumerable<HarborModel> harbors) => string.Join("|",
            harbors.Select(h => h.HarborKey.ToString()).OrderBy(s => s, StringComparer.Ordinal));

        [Fact]
        public async Task Replay_FromLongestSeedGame_ResetsToRollForOrder()
        {
            var (uri, handler) = Conn();
            using var http = new HttpClient(handler) { BaseAddress = uri };

            // 1. Pick the longest already-seeded game.
            var listResp = await http.GetAsync("/api/games?playerId=*");
            var listBody = await listResp.Content.ReadAsStringAsync();
            Assert.True(listResp.IsSuccessStatusCode, $"GET /api/games failed: {(int)listResp.StatusCode} {listBody}");

            using var listDoc = JsonDocument.Parse(listBody);
            var games = listDoc.RootElement.GetProperty("games");
            if (games.GetArrayLength() == 0)
            {
                Assert.Fail("No seeded games found — run `./catan.ps1 database install` to seed the database.");
            }

            string originalId = "";
            int bestTurns = -1;
            foreach (var g in games.EnumerateArray())
            {
                var turns = g.TryGetProperty("turnCount", out var tc) ? tc.GetInt32() : 0;
                if (turns > bestTurns)
                {
                    bestTurns = turns;
                    originalId = g.GetProperty("gameId").GetString() ?? "";
                }
            }
            Assert.False(string.IsNullOrEmpty(originalId), "Could not select a seed game id.");
            _output.WriteLine($"Selected longest seed game {originalId} ({bestTurns} turns)");

            // 2. Replay it.
            var replayResp = await http.PostAsync($"/api/game/{originalId}/replay", null);
            var replayBody = await replayResp.Content.ReadAsStringAsync();
            Assert.True(replayResp.IsSuccessStatusCode,
                $"Replay failed ({(int)replayResp.StatusCode}). The longest seed game may not be a played game " +
                $"(needs >=1 roll and a WaitingForRollForOrder state). Body: {replayBody}");

            using var replayDoc = JsonDocument.Parse(replayBody);
            var newGameId = replayDoc.RootElement.GetProperty("newGameId").GetString() ?? "";
            Assert.False(string.IsNullOrEmpty(newGameId), "Replay did not return a newGameId.");
            Assert.NotEqual(originalId, newGameId);

            // 3. Capture original (current) and replay models. Both are now in the
            //    registry (replay's EnsureGameLoadedAsync loaded the original).
            var original = await GetGameModelAsync(http, originalId);
            var replay = await GetGameModelAsync(http, newGameId);

            // 4a. Bug 1 — GameId rewritten; registry key and model agree.
            Assert.Equal(newGameId, replay.GameId);
            Assert.NotEqual(originalId, replay.GameId);

            // 4b. Reset to the correct start state.
            Assert.Equal(GameState.WaitingForRollForOrder, replay.GameState);

            // 4c. Same board (tiles + numbers + harbors) and same players.
            Assert.Equal(TileSig(original.Tiles), TileSig(replay.Tiles));
            Assert.Equal(HarborSig(original.Harbors), HarborSig(replay.Harbors));
            Assert.Equal(
                original.Players.Select(p => p.Id).OrderBy(s => s, StringComparer.Ordinal),
                replay.Players.Select(p => p.Id).OrderBy(s => s, StringComparer.Ordinal));

            // 4d. Clean reset — no placed buildings/roads, no player stats.
            Assert.DoesNotContain(replay.Buildings, b => !string.IsNullOrEmpty(b.OwnerId));
            Assert.DoesNotContain(replay.Roads, r => !string.IsNullOrEmpty(r.OwnerId) || r.RoadState != RoadState.Unowned);
            foreach (var p in replay.Players)
            {
                Assert.Equal(0, p.Score);
                Assert.Equal(0, p.ResourcesThisGame.Count);
                Assert.Equal(0, p.ResourcesThisTurn.Count);
                Assert.Empty(p.UnspentEntitlements);
                Assert.Empty(p.SpentEntitlementsThisGame);
            }

            // 4e. Bug 2 — fresh randomness.
            Assert.Equal(0, replay.Random.Iterations);
            Assert.NotEqual(original.Random.Seed, replay.Random.Seed);

            // 4f. Fresh reset — TurnCount is now sourced from RollModel.TotalRolls
            //     (epic #197). A replay reset to WaitingForRollForOrder has not rolled
            //     yet, so turnCount == 0. (Pre-#197 this asserted == 1, when TurnCount
            //     tracked DoneCount/log-entry count.)
            var listResp2 = await http.GetAsync("/api/games?playerId=*");
            var listBody2 = await listResp2.Content.ReadAsStringAsync();
            Assert.True(listResp2.IsSuccessStatusCode,
                $"GET /api/games failed: {(int)listResp2.StatusCode} {listBody2}");
            using var listDoc2 = JsonDocument.Parse(listBody2);
            int replayTurns = -1;
            foreach (var g in listDoc2.RootElement.GetProperty("games").EnumerateArray())
            {
                if (g.GetProperty("gameId").GetString() == newGameId)
                {
                    replayTurns = g.TryGetProperty("turnCount", out var tc) ? tc.GetInt32() : -1;
                    break;
                }
            }
            Assert.Equal(0, replayTurns);
        }

        [Fact]
        public async Task Replay_NeverRolledGame_Returns422()
        {
            var (uri, handler) = Conn();
            using var http = new HttpClient(handler) { BaseAddress = uri };

            // A brand-new game sits in PickingBoard with TotalRolls == 0.
            var newGameJson = JsonSerializer.Serialize(new
            {
                playerIds = new[] { "p1", "p2", "p3" },
                gameType = "Regular",
                gameName = "replay-neg-test",
            });
            var createResp = await http.PostAsync("/api/game/new",
                new StringContent(newGameJson, Encoding.UTF8, "application/json"));
            var createBody = await createResp.Content.ReadAsStringAsync();
            Assert.True(createResp.IsSuccessStatusCode, $"POST /api/game/new failed: {(int)createResp.StatusCode} {createBody}");

            using var createDoc = JsonDocument.Parse(createBody);
            var gameId = createDoc.RootElement.GetProperty("gameId").GetString() ?? "";
            Assert.False(string.IsNullOrEmpty(gameId));

            var replayResp = await http.PostAsync($"/api/game/{gameId}/replay", null);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, replayResp.StatusCode);
        }
    }
}

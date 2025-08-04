using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;

namespace Tests.Shared.Serialization
{
    /// <summary>
    /// Performance benchmarks for serialization operations.
    /// These tests measure serialization/deserialization performance to ensure
    /// the companion app can handle real-time updates efficiently.
    /// </summary>
    public class SerializationPerformanceTests
    {
        private readonly JsonSerializerOptions _jsonOptions;

        public SerializationPerformanceTests()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false, // Compact for performance
                Converters = { new JsonStringEnumConverter() },
                PropertyNameCaseInsensitive = true
            };
        }

        [Fact]
        public void GameModel_SerializationPerformance_ShouldBeFast()
        {
            using (new FunctionTimer("GameModel_Serialization_Performance", enableOverride: true, writeToConsole: true))
            {
                const int iterations = 1000;
                var gameModel = CreateLargeGameModel();
                var results = new ConcurrentBag<TimeSpan>();
                
                // Warmup
                for (int i = 0; i < 10; i++)
                {
                    JsonSerializer.Serialize(gameModel, _jsonOptions);
                }

                // Parallel performance test
                Parallel.For(0, iterations, i =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    var json = JsonSerializer.Serialize(gameModel, _jsonOptions);
                    stopwatch.Stop();
                    
                    results.Add(stopwatch.Elapsed);
                    
                    // Verify we got valid JSON
                    Assert.True(json.Length > 0);
                });

                // Analyze results
                var times = results.ToList();
                var avgTime = TimeSpan.FromTicks((long)times.Average(t => t.Ticks));
                var maxTime = times.Max();
                var minTime = times.Min();

                Console.WriteLine($"Serialization Performance (n={iterations}):");
                Console.WriteLine($"  Average: {avgTime.TotalMilliseconds:F2}ms");
                Console.WriteLine($"  Min: {minTime.TotalMilliseconds:F2}ms");
                Console.WriteLine($"  Max: {maxTime.TotalMilliseconds:F2}ms");

                // Performance assertions
                Assert.True(avgTime.TotalMilliseconds < 50, $"Average serialization should be under 50ms, was {avgTime.TotalMilliseconds:F2}ms");
                Assert.True(maxTime.TotalMilliseconds < 200, $"Max serialization should be under 200ms, was {maxTime.TotalMilliseconds:F2}ms");
            }
        }

        [Fact]
        public void GameModel_DeserializationPerformance_ShouldBeFast()
        {
            using (new FunctionTimer("GameModel_Deserialization_Performance", enableOverride: true, writeToConsole: true))
            {
                const int iterations = 1000;
                var gameModel = CreateLargeGameModel();
                var json = JsonSerializer.Serialize(gameModel, _jsonOptions);
                var results = new ConcurrentBag<TimeSpan>();
                
                // Warmup
                for (int i = 0; i < 10; i++)
                {
                    JsonSerializer.Deserialize<GameModel>(json, _jsonOptions);
                }

                // Parallel performance test
                Parallel.For(0, iterations, i =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    var deserialized = JsonSerializer.Deserialize<GameModel>(json, _jsonOptions);
                    stopwatch.Stop();
                    
                    results.Add(stopwatch.Elapsed);
                    
                    // Verify we got valid object
                    Assert.NotNull(deserialized);
                    Assert.Equal(gameModel.GameId, deserialized.GameId);
                });

                // Analyze results
                var times = results.ToList();
                var avgTime = TimeSpan.FromTicks((long)times.Average(t => t.Ticks));
                var maxTime = times.Max();
                var minTime = times.Min();

                Console.WriteLine($"Deserialization Performance (n={iterations}):");
                Console.WriteLine($"  Average: {avgTime.TotalMilliseconds:F2}ms");
                Console.WriteLine($"  Min: {minTime.TotalMilliseconds:F2}ms");
                Console.WriteLine($"  Max: {maxTime.TotalMilliseconds:F2}ms");

                // Performance assertions
                Assert.True(avgTime.TotalMilliseconds < 100, $"Average deserialization should be under 100ms, was {avgTime.TotalMilliseconds:F2}ms");
                Assert.True(maxTime.TotalMilliseconds < 300, $"Max deserialization should be under 300ms, was {maxTime.TotalMilliseconds:F2}ms");
            }
        }

        [Fact]
        public void RealTimeUpdate_SimulationPerformance_ShouldHandleLoad()
        {
            using (new FunctionTimer("RealTime_Update_Simulation", enableOverride: true, writeToConsole: true))
            {
                const int simultaneousClients = 10;
                const int updatesPerClient = 100;
                
                var gameModel = CreateLargeGameModel();
                var results = new ConcurrentBag<(int ClientId, TimeSpan SerializeTime, TimeSpan DeserializeTime)>();

                // Simulate multiple clients receiving real-time updates
                Parallel.For(0, simultaneousClients, clientId =>
                {
                    for (int update = 0; update < updatesPerClient; update++)
                    {
                        // Simulate server sending update (serialization)
                        var serializeStart = Stopwatch.StartNew();
                        var json = JsonSerializer.Serialize(gameModel, _jsonOptions);
                        serializeStart.Stop();

                        // Simulate client receiving update (deserialization) 
                        var deserializeStart = Stopwatch.StartNew();
                        var deserialized = JsonSerializer.Deserialize<GameModel>(json, _jsonOptions);
                        deserializeStart.Stop();

                        results.Add((clientId, serializeStart.Elapsed, deserializeStart.Elapsed));

                        // Verify correctness
                        Assert.NotNull(deserialized);
                        Assert.Equal(gameModel.GameId, deserialized.GameId);

                        // Simulate small delay between updates
                        Thread.Sleep(1);
                    }
                });

                // Analyze results
                var allResults = results.ToList();
                var totalOperations = allResults.Count * 2; // serialize + deserialize
                
                var avgSerialize = TimeSpan.FromTicks((long)allResults.Average(r => r.SerializeTime.Ticks));
                var avgDeserialize = TimeSpan.FromTicks((long)allResults.Average(r => r.DeserializeTime.Ticks));
                var avgTotal = TimeSpan.FromTicks(avgSerialize.Ticks + avgDeserialize.Ticks);

                Console.WriteLine($"Real-time Update Simulation ({simultaneousClients} clients, {updatesPerClient} updates each):");
                Console.WriteLine($"  Total operations: {totalOperations}");
                Console.WriteLine($"  Avg serialize: {avgSerialize.TotalMilliseconds:F2}ms");
                Console.WriteLine($"  Avg deserialize: {avgDeserialize.TotalMilliseconds:F2}ms");
                Console.WriteLine($"  Avg total per update: {avgTotal.TotalMilliseconds:F2}ms");

                // Performance assertions for real-time scenarios
                Assert.True(avgTotal.TotalMilliseconds < 150, 
                    $"Total update time should be under 150ms for real-time feel, was {avgTotal.TotalMilliseconds:F2}ms");
                Assert.Equal(simultaneousClients * updatesPerClient, allResults.Count);
            }
        }

        [Fact]
        public void JsonSize_ShouldBeReasonable_ForNetworkTransfer()
        {
            using (new FunctionTimer("JSON_Size_Analysis", enableOverride: true, writeToConsole: true))
            {
                var gameModel = CreateLargeGameModel();
                
                // Test different serialization options
                var compactOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false,
                    Converters = { new JsonStringEnumConverter() }
                };

                var prettyOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter() }
                };

                var compactJson = JsonSerializer.Serialize(gameModel, compactOptions);
                var prettyJson = JsonSerializer.Serialize(gameModel, prettyOptions);

                var compactSize = System.Text.Encoding.UTF8.GetByteCount(compactJson);
                var prettySize = System.Text.Encoding.UTF8.GetByteCount(prettyJson);
                var compressionRatio = (double)compactSize / prettySize * 100;

                Console.WriteLine($"JSON Size Analysis:");
                Console.WriteLine($"  Compact: {compactSize:N0} bytes ({compactSize / 1024.0:F1} KB)");
                Console.WriteLine($"  Pretty: {prettySize:N0} bytes ({prettySize / 1024.0:F1} KB)");
                Console.WriteLine($"  Compression: {compressionRatio:F1}% of pretty size");

                // Size assertions for network efficiency
                Assert.True(compactSize < 100_000, $"Compact JSON should be under 100KB, was {compactSize:N0} bytes");
                Assert.True(compactSize < prettySize, "Compact JSON should be smaller than pretty JSON");

                // Test that both deserialize correctly
                var compactDeserialized = JsonSerializer.Deserialize<GameModel>(compactJson, compactOptions);
                var prettyDeserialized = JsonSerializer.Deserialize<GameModel>(prettyJson, prettyOptions);

                Assert.NotNull(compactDeserialized);
                Assert.NotNull(prettyDeserialized);
                Assert.Equal(gameModel.GameId, compactDeserialized.GameId);
                Assert.Equal(gameModel.GameId, prettyDeserialized.GameId);
            }
        }

        private GameModel CreateLargeGameModel()
        {
            var players = new List<PlayerModel>();
            for (int i = 1; i <= 6; i++)
            {
                players.Add(new PlayerModel($"Player{i}-{i:000}"));
            }

            var tiles = new List<TileModel>();
            var buildings = new List<BuildingModel>();
            var roads = new List<RoadModel>();
            var harbors = new List<HarborModel>();

            // Create a full-sized game board
            var resourceTypes = new[] { ResourceType.Wood, ResourceType.Brick, ResourceType.Sheep, ResourceType.Wheat, ResourceType.Ore };
            var numbers = new[] { 2, 3, 3, 4, 4, 5, 5, 6, 6, 8, 8, 9, 9, 10, 10, 11, 11, 12 };
            var positions = new[] { HexPosition.TopLeft, HexPosition.TopRight, HexPosition.Right, HexPosition.BottomRight, HexPosition.BottomLeft, HexPosition.Left };
            var sides = new[] { HexSide.Top, HexSide.TopRight, HexSide.BottomRight, HexSide.Bottom, HexSide.BottomLeft, HexSide.TopLeft };

            // Generate hex grid
            for (int q = -3; q <= 3; q++)
            {
                for (int r = Math.Max(-3, -q - 3); r <= Math.Min(3, -q + 3); r++)
                {
                    var s = -q - r;
                    var coords = new HexCoordinates(q, r, s);
                    
                    // Add tile
                    tiles.Add(new TileModel
                    {
                        TileKey = coords,
                        ResourceTileType = resourceTypes[Math.Abs(q + r) % resourceTypes.Length],
                        Number = numbers[Math.Abs(q * r) % numbers.Length],
                        Highlighted = false,
                        TemporarilyGold = false
                    });

                    // Add some buildings
                    for (int p = 0; p < positions.Length; p++)
                    {
                        if ((q + r + s + p) % 3 == 0) // Sparse placement
                        {
                            buildings.Add(new BuildingModel
                            {
                                BuildingKey = new BuildingKey(coords, positions[p]),
                                BuildingState = (BuildingState)((q + r + s + p) % 4),
                                Wall = false,
                                Metropolis = false,
                                OwnerId = players[(q + r + s + p) % players.Count].Id
                            });
                        }
                    }

                    // Add some roads
                    for (int side = 0; side < sides.Length; side++)
                    {
                        if ((q + r + s + side) % 4 == 0) // Sparse placement
                        {
                            roads.Add(new RoadModel
                            {
                                RoadKey = new RoadKey(coords, sides[side]),
                                RoadState = (q + r + s + side) % 3 == 0 ? RoadState.Road : RoadState.Buildable,
                                OwnerId = players[(q + r + s + side) % players.Count].Id,
                                BuildIndex = side
                            });
                        }
                    }

                    // Add some harbors
                    if (Math.Abs(q) == 3 || Math.Abs(r) == 3 || Math.Abs(s) == 3)
                    {
                        harbors.Add(new HarborModel
                        {
                            HarborKey = new HarborKey(coords, (HarborType)((q + r + s) % Enum.GetValues<HarborType>().Length), sides[Math.Abs(q + r + s) % sides.Length]),
                            Owner = null
                        });
                    }
                }
            }

            return new GameModel
            {
                GameId = "performance-test-" + Guid.NewGuid().ToString(),
                GameState = GameState.WaitingForNext,
                GameType = GameType.Expansion,
                CurrentPlayerId = players[0].Id,
                CreatedTime = DateTime.UtcNow,
                Players = players,
                Tiles = tiles,
                Buildings = buildings,
                Roads = roads,
                Harbors = harbors,
                Robber = new RobberModel { Coordinates = new HexCoordinates(0, 0, 0) },
                ActionFlags = new ActionFlags
                {
                    NextEnabled = true,
                    UndoEnabled = true,
                    RedoEnabled = false,
                    RollsEnabled = true
                },
                EntitlementPurchaseModel = new List<EntitlementPurchaseModel>
                {
                    new() { Entitlement = Entitlement.Settlement, Enabled = true },
                    new() { Entitlement = Entitlement.City, Enabled = true },
                    new() { Entitlement = Entitlement.Road, Enabled = true },
                    new() { Entitlement = Entitlement.Soldier, Enabled = true },
                    new() { Entitlement = Entitlement.DevCard, Enabled = false },
                    new() { Entitlement = Entitlement.BuyKnight, Enabled = true },
                    new() { Entitlement = Entitlement.UpgradeKnight, Enabled = false }
                },
                HouseRules = new HouseRules
                {
                    GoldTiles = 2,
                    WallsProtectCities = true,
                    KnightMovesRobberBeforeRoll = true
                },
                ResourceRules = new ResourceRules(4, 5, 15, 3, 6),
                RollModel = new RollModel
                {
                    TurnRollModel = new TurnRollModel(4, 6),
                    GameRollModel = new GameRollModel
                    {
                        RollCounts = new int[] { 5, 8, 12, 15, 18, 20, 25, 20, 18, 15, 12 },
                        TotalRolls = 168
                    }
                },
                GameResourcesModel = new ResourcesModel
                {
                    Wood = 45,
                    Brick = 38,
                    Sheep = 42,
                    Wheat = 51,
                    Ore = 29
                },
                NextPlayerToRollAfterSupplemental = players[1].Id,
                PreviousGameState = GameState.WaitingForRoll
            };
        }
    }
}
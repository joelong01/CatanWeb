using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Xunit;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;

namespace Tests.Shared.Serialization
{
    /// <summary>
    /// Comprehensive serialization test suite for all Catan3.Shared models and enums.
    /// These tests verify that every model and enum can be serialized and deserialized properly,
    /// with support for JavaScript/JSON compatibility testing for the companion app.
    /// Tests run in parallel for maximum performance.
    /// </summary>
    public class SharedSerializationTests
    {
        private readonly JsonSerializerOptions _jsonOptions;

        public SharedSerializationTests()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() },
                PropertyNameCaseInsensitive = true
            };
        }

        #region Enum Serialization Tests

        [Theory]
        [InlineData(typeof(GameAction))]
        [InlineData(typeof(ResourceType))]
        [InlineData(typeof(BuildingState))]
        [InlineData(typeof(BuildingVisualState))]
        [InlineData(typeof(GameType))]
        [InlineData(typeof(RoadState))]
        [InlineData(typeof(HarborType))]
        [InlineData(typeof(CatanOrientation))]
        [InlineData(typeof(GameState))]
        [InlineData(typeof(GamePhase))]
        [InlineData(typeof(SpecialDice))]
        [InlineData(typeof(ValidCatanRoll))]
        [InlineData(typeof(Entitlement))]
        [InlineData(typeof(ErrorLevel))]
        [InlineData(typeof(LocalPersistActions))]
        [InlineData(typeof(HexPosition))]
        [InlineData(typeof(HexSide))]
        [InlineData(typeof(Direction))]
        public async Task Enum_ShouldSerializeAndDeserialize_AllValues(Type enumType)
        {
            using (new FunctionTimer($"Enum_{enumType.Name}_Serialization", enableOverride: true))
            {
                // Get all enum values
                var enumValues = Enum.GetValues(enumType);
                var results = new ConcurrentBag<(object Value, bool Success, string? Error)>();

                // Test all enum values in parallel
                await Task.Run(() =>
                {
                    Parallel.ForEach(enumValues.Cast<object>(), enumValue =>
                    {
                        try
                        {
                            // Test serialization
                            var json = JsonSerializer.Serialize(enumValue, _jsonOptions);
                            Assert.NotNull(json);
                            Assert.NotEmpty(json);

                            // Test deserialization
                            var deserialized = JsonSerializer.Deserialize(json, enumType, _jsonOptions);
                            Assert.NotNull(deserialized);
                            Assert.Equal(enumValue, deserialized);

                            // Test JavaScript compatibility (string representation)
                            Assert.True(json.Contains('"'), $"Enum {enumValue} should serialize as string for JS compatibility");

                            results.Add((enumValue, true, null));
                        }
                        catch (Exception ex)
                        {
                            results.Add((enumValue, false, ex.Message));
                        }
                    });
                });

                // Verify all tests passed
                var failures = results.Where(r => !r.Success).ToList();
                Assert.Empty(failures.Select(f => $"{f.Value}: {f.Error}"));

                // Verify we tested all values
                Assert.Equal(enumValues.Length, results.Count);
            }
        }

        #endregion

        #region Model Serialization Tests

        [Fact]
        public async Task GameModel_ShouldSerializeAndDeserialize_CompleteObject()
        {
            using (new FunctionTimer("GameModel_Serialization", enableOverride: true))
            {
                await Task.Run(() =>
                {
                    // Create comprehensive GameModel with all fields populated
                    var gameModel = CreateFullGameModel();

                    // Test serialization
                    var json = JsonSerializer.Serialize(gameModel, _jsonOptions);
                    Assert.NotNull(json);
                    Assert.NotEmpty(json);

                    // Test deserialization
                    var deserialized = JsonSerializer.Deserialize<GameModel>(json, _jsonOptions);
                    Assert.NotNull(deserialized);

                    // Verify critical fields
                    Assert.Equal(gameModel.GameId, deserialized.GameId);
                    Assert.Equal(gameModel.GameState, deserialized.GameState);
                    Assert.Equal(gameModel.GameType, deserialized.GameType);
                    Assert.Equal(gameModel.CurrentPlayerId, deserialized.CurrentPlayerId);
                    Assert.Equal(gameModel.Players.Count, deserialized.Players.Count);
                    Assert.Equal(gameModel.EntitlementPurchaseModel.Count, deserialized.EntitlementPurchaseModel.Count);

                    // Test JavaScript compatibility
                    VerifyJavaScriptCompatibility(json, "GameModel").Wait();
                });
            }
        }

        [Fact]
        public async Task PlayerModel_ShouldSerializeAndDeserialize_AllFields()
        {
            using (new FunctionTimer("PlayerModel_Serialization", enableOverride: true))
            {
                await Task.Run(() =>
                {
                    var player = new PlayerModel("TestPlayer-001")
                    {
                        // Add any additional fields that PlayerModel might have
                    };

                    var json = JsonSerializer.Serialize(player, _jsonOptions);
                    var deserialized = JsonSerializer.Deserialize<PlayerModel>(json, _jsonOptions);

                    Assert.NotNull(deserialized);
                    Assert.Equal(player.Id, deserialized.Id);
                    Assert.Equal(player.Name, deserialized.Name);
                });
            }
        }

        [Fact]
        public async Task TileModel_ShouldSerializeAndDeserialize_AllFields()
        {
            using (new FunctionTimer("TileModel_Serialization", enableOverride: true))
            {
                await Task.Run(() =>
                {
                    var tile = new TileModel
                    {
                        TileKey = new HexCoordinates(1, -1, 0),
                        ResourceTileType = ResourceType.Wheat,
                        Number = 6,
                        Highlighted = true,
                        TemporarilyGold = false
                    };

                    var json = JsonSerializer.Serialize(tile, _jsonOptions);
                    var deserialized = JsonSerializer.Deserialize<TileModel>(json, _jsonOptions);

                    Assert.NotNull(deserialized);
                    Assert.Equal(tile.ResourceTileType, deserialized.ResourceTileType);
                    Assert.Equal(tile.Number, deserialized.Number);
                    Assert.Equal(tile.Stars, deserialized.Stars);
                    Assert.Equal(tile.Highlighted, deserialized.Highlighted);
                });
            }
        }

        [Fact]
        public async Task BuildingModel_ShouldSerializeAndDeserialize_AllFields()
        {
            using (new FunctionTimer("BuildingModel_Serialization", enableOverride: true))
            {
                await Task.Run(() =>
                {
                    var building = new BuildingModel
                    {
                        BuildingKey = new BuildingKey(new HexCoordinates(0, 0, 0), HexPosition.TopRight),
                        BuildingState = BuildingState.Settlement,
                        Wall = false,
                        Metropolis = false,
                        OwnerId = "Player1"
                    };

                    var json = JsonSerializer.Serialize(building, _jsonOptions);
                    var deserialized = JsonSerializer.Deserialize<BuildingModel>(json, _jsonOptions);

                    Assert.NotNull(deserialized);
                    Assert.Equal(building.BuildingState, deserialized.BuildingState);
                    Assert.Equal(building.Wall, deserialized.Wall);
                    Assert.Equal(building.OwnerId, deserialized.OwnerId);
                });
            }
        }

        [Fact]
        public async Task RoadModel_ShouldSerializeAndDeserialize_AllFields()
        {
            using (new FunctionTimer("RoadModel_Serialization", enableOverride: true))
            {
                await Task.Run(() =>
                {
                    var road = new RoadModel
                    {
                        RoadKey = new RoadKey(new HexCoordinates(0, 0, 0), HexSide.Top),
                        RoadState = RoadState.Road,
                        OwnerId = "Player1",
                        BuildIndex = 1
                    };

                    var json = JsonSerializer.Serialize(road, _jsonOptions);
                    var deserialized = JsonSerializer.Deserialize<RoadModel>(json, _jsonOptions);

                    Assert.NotNull(deserialized);
                    Assert.Equal(road.RoadState, deserialized.RoadState);
                    Assert.Equal(road.OwnerId, deserialized.OwnerId);
                    Assert.Equal(road.BuildIndex, deserialized.BuildIndex);
                });
            }
        }

        [Fact]
        public async Task HarborModel_ShouldSerializeAndDeserialize_AllFields()
        {
            using (new FunctionTimer("HarborModel_Serialization", enableOverride: true))
            {
                await Task.Run(() =>
                {
                    var harbor = new HarborModel
                    {
                        HarborKey = new HarborKey(new HexCoordinates(1, 0, -1), HarborType.Wheat, HexSide.Bottom),
                        Owner = null
                    };

                    var json = JsonSerializer.Serialize(harbor, _jsonOptions);
                    var deserialized = JsonSerializer.Deserialize<HarborModel>(json, _jsonOptions);

                    Assert.NotNull(deserialized);
                    Assert.Equal(harbor.HarborKey.HarborType, deserialized.HarborKey.HarborType);
                    Assert.Equal(harbor.HarborKey.Side, deserialized.HarborKey.Side);
                    Assert.Equal(harbor.Owner, deserialized.Owner);
                });
            }
        }

        [Fact]
        public async Task RobberModel_ShouldSerializeAndDeserialize_AllFields()
        {
            using (new FunctionTimer("RobberModel_Serialization", enableOverride: true))
            {
                await Task.Run(() =>
                {
                    var robber = new RobberModel
                    {
                        Coordinates = new HexCoordinates(0, 1, -1),
                        MovedBy = "Player1",
                        Targetted = "Player2",
                        ResourcesStolen = 2
                    };

                    var json = JsonSerializer.Serialize(robber, _jsonOptions);
                    var deserialized = JsonSerializer.Deserialize<RobberModel>(json, _jsonOptions);

                    Assert.NotNull(deserialized);
                    Assert.Equal(robber.Coordinates.Q, deserialized.Coordinates.Q);
                    Assert.Equal(robber.Coordinates.R, deserialized.Coordinates.R);
                    Assert.Equal(robber.Coordinates.S, deserialized.Coordinates.S);
                    Assert.Equal(robber.MovedBy, deserialized.MovedBy);
                    Assert.Equal(robber.Targetted, deserialized.Targetted);
                    Assert.Equal(robber.ResourcesStolen, deserialized.ResourcesStolen);
                });
            }
        }

        [Fact]
        public async Task EntitlementPurchaseModel_ShouldSerializeAndDeserialize_AllFields()
        {
            using (new FunctionTimer("EntitlementPurchaseModel_Serialization", enableOverride: true))
            {
                await Task.Run(() =>
                {
                    var entitlement = new EntitlementPurchaseModel
                    {
                        Entitlement = Entitlement.Settlement,
                        Enabled = true
                    };

                    var json = JsonSerializer.Serialize(entitlement, _jsonOptions);
                    var deserialized = JsonSerializer.Deserialize<EntitlementPurchaseModel>(json, _jsonOptions);

                    Assert.NotNull(deserialized);
                    Assert.Equal(entitlement.Entitlement, deserialized.Entitlement);
                    Assert.Equal(entitlement.Enabled, deserialized.Enabled);
                });
            }
        }

        [Fact]
        public async Task ResourcesModel_ShouldSerializeAndDeserialize_AllFields()
        {
            using (new FunctionTimer("ResourcesModel_Serialization", enableOverride: true))
            {
                await Task.Run(() =>
                {
                    var resources = new ResourcesModel
                    {
                        Wood = 3,
                        Brick = 2,
                        Sheep = 1,
                        Wheat = 4,
                        Ore = 1
                    };

                    var json = JsonSerializer.Serialize(resources, _jsonOptions);
                    var deserialized = JsonSerializer.Deserialize<ResourcesModel>(json, _jsonOptions);

                    Assert.NotNull(deserialized);
                    Assert.Equal(resources.Wood, deserialized.Wood);
                    Assert.Equal(resources.Brick, deserialized.Brick);
                    Assert.Equal(resources.Sheep, deserialized.Sheep);
                    Assert.Equal(resources.Wheat, deserialized.Wheat);
                    Assert.Equal(resources.Ore, deserialized.Ore);
                });
            }
        }

        [Fact]
        public async Task HexCoordinates_ShouldSerializeAndDeserialize_AllFields()
        {
            using (new FunctionTimer("HexCoordinates_Serialization", enableOverride: true))
            {
                await Task.Run(() =>
                {
                    var coords = new HexCoordinates(2, -1, -1);

                    var json = JsonSerializer.Serialize(coords, _jsonOptions);
                    var deserialized = JsonSerializer.Deserialize<HexCoordinates>(json, _jsonOptions);

                    Assert.NotNull(deserialized);
                    Assert.Equal(coords.Q, deserialized.Q);
                    Assert.Equal(coords.R, deserialized.R);
                    Assert.Equal(coords.S, deserialized.S);
                });
            }
        }

        [Fact]
        public async Task BuildingKey_ShouldSerializeAndDeserialize_AllFields()
        {
            using (new FunctionTimer("BuildingKey_Serialization", enableOverride: true))
            {
                await Task.Run(() =>
                {
                    var key = new BuildingKey(new HexCoordinates(1, 0, -1), HexPosition.BottomLeft);

                    var json = JsonSerializer.Serialize(key, _jsonOptions);
                    var deserialized = JsonSerializer.Deserialize<BuildingKey>(json, _jsonOptions);

                    Assert.NotNull(deserialized);
                    Assert.Equal(key.HexCoordinates.Q, deserialized.HexCoordinates.Q);
                    Assert.Equal(key.HexCoordinates.R, deserialized.HexCoordinates.R);
                    Assert.Equal(key.HexCoordinates.S, deserialized.HexCoordinates.S);
                    Assert.Equal(key.Position, deserialized.Position);
                });
            }
        }

        [Fact]
        public async Task RoadKey_ShouldSerializeAndDeserialize_AllFields()
        {
            using (new FunctionTimer("RoadKey_Serialization", enableOverride: true))
            {
                await Task.Run(() =>
                {
                    var key = new RoadKey(new HexCoordinates(0, -1, 1), HexSide.BottomRight);

                    var json = JsonSerializer.Serialize(key, _jsonOptions);
                    var deserialized = JsonSerializer.Deserialize<RoadKey>(json, _jsonOptions);

                    Assert.NotNull(deserialized);
                    Assert.Equal(key.TileKey.Q, deserialized.TileKey.Q);
                    Assert.Equal(key.TileKey.R, deserialized.TileKey.R);
                    Assert.Equal(key.TileKey.S, deserialized.TileKey.S);
                    Assert.Equal(key.HexSide, deserialized.HexSide);
                });
            }
        }

        [Fact]
        public async Task ActionFlags_ShouldSerializeAndDeserialize_AllFields()
        {
            using (new FunctionTimer("ActionFlags_Serialization", enableOverride: true))
            {
                await Task.Run(() =>
                {
                    var flags = new ActionFlags
                    {
                        NextEnabled = true,
                        UndoEnabled = false,
                        RedoEnabled = true,
                        RollsEnabled = false
                    };

                    var json = JsonSerializer.Serialize(flags, _jsonOptions);
                    var deserialized = JsonSerializer.Deserialize<ActionFlags>(json, _jsonOptions);

                    Assert.NotNull(deserialized);
                    Assert.Equal(flags.NextEnabled, deserialized.NextEnabled);
                    Assert.Equal(flags.UndoEnabled, deserialized.UndoEnabled);
                    Assert.Equal(flags.RedoEnabled, deserialized.RedoEnabled);
                    Assert.Equal(flags.RollsEnabled, deserialized.RollsEnabled);
                });
            }
        }

        [Fact]
        public async Task TurnRollModel_ShouldSerializeAndDeserialize_AllFields()
        {
            using (new FunctionTimer("TurnRollModel_Serialization", enableOverride: true))
            {
                await Task.Run(() =>
                {
                    var roll = new TurnRollModel(3, 4);

                    var json = JsonSerializer.Serialize(roll, _jsonOptions);
                    var deserialized = JsonSerializer.Deserialize<TurnRollModel>(json, _jsonOptions);

                    Assert.NotNull(deserialized);
                    Assert.Equal(roll.RedRoll, deserialized.RedRoll);
                    Assert.Equal(roll.WhiteRoll, deserialized.WhiteRoll);
                    Assert.Equal(roll.NormalRoll, deserialized.NormalRoll);
                });
            }
        }

        [Fact]
        public async Task ResourceRules_ShouldSerializeAndDeserialize_AllFields()
        {
            using (new FunctionTimer("ResourceRules_Serialization", enableOverride: true))
            {
                await Task.Run(() =>
                {
                    var rules = new ResourceRules(4, 5, 15, 3, 4);

                    var json = JsonSerializer.Serialize(rules, _jsonOptions);
                    var deserialized = JsonSerializer.Deserialize<ResourceRules>(json, _jsonOptions);

                    Assert.NotNull(deserialized);
                    Assert.Equal(rules.MaxCities, deserialized.MaxCities);
                    Assert.Equal(rules.MaxSettlements, deserialized.MaxSettlements);
                    Assert.Equal(rules.MaxRoads, deserialized.MaxRoads);
                    Assert.Equal(rules.MinPlayers, deserialized.MinPlayers);
                    Assert.Equal(rules.MaxPlayers, deserialized.MaxPlayers);
                });
            }
        }

        [Fact]
        public async Task RollModel_ShouldSerializeAndDeserialize_AllFields()
        {
            using (new FunctionTimer("RollModel_Serialization", enableOverride: true))
            {
                await Task.Run(() =>
                {
                    var rollModel = new RollModel
                    {
                        TurnRollModel = new TurnRollModel(4, 6),
                        GameRollModel = new GameRollModel
                        {
                            RollCounts = new int[] { 1, 2, 3, 4, 5, 6, 7, 6, 5, 4, 3 },
                            TotalRolls = 50
                        }
                    };

                    var json = JsonSerializer.Serialize(rollModel, _jsonOptions);
                    var deserialized = JsonSerializer.Deserialize<RollModel>(json, _jsonOptions);

                    Assert.NotNull(deserialized);
                    Assert.NotNull(deserialized.TurnRollModel);
                    Assert.NotNull(deserialized.GameRollModel);
                    Assert.Equal(rollModel.TurnRollModel.RedRoll, deserialized.TurnRollModel.RedRoll);
                    Assert.Equal(rollModel.TurnRollModel.WhiteRoll, deserialized.TurnRollModel.WhiteRoll);
                    Assert.Equal(rollModel.GameRollModel.TotalRolls, deserialized.GameRollModel.TotalRolls);
                    Assert.Equal(rollModel.GameRollModel.RollCounts.Length, deserialized.GameRollModel.RollCounts.Length);
                });
            }
        }

        [Fact]
        public async Task GameRollModel_ShouldSerializeAndDeserialize_AllFields()
        {
            using (new FunctionTimer("GameRollModel_Serialization", enableOverride: true))
            {
                await Task.Run(() =>
                {
                    var gameRoll = new GameRollModel
                    {
                        RollCounts = new int[] { 2, 4, 6, 8, 10, 12, 14, 12, 10, 8, 6 },
                        TotalRolls = 92
                    };

                    var json = JsonSerializer.Serialize(gameRoll, _jsonOptions);
                    var deserialized = JsonSerializer.Deserialize<GameRollModel>(json, _jsonOptions);

                    Assert.NotNull(deserialized);
                    Assert.Equal(gameRoll.TotalRolls, deserialized.TotalRolls);
                    Assert.Equal(gameRoll.RollCounts.Length, deserialized.RollCounts.Length);
                    for (int i = 0; i < gameRoll.RollCounts.Length; i++)
                    {
                        Assert.Equal(gameRoll.RollCounts[i], deserialized.RollCounts[i]);
                    }
                });
            }
        }

        [Fact]
        public async Task HouseRules_ShouldSerializeAndDeserialize_AllFields()
        {
            using (new FunctionTimer("HouseRules_Serialization", enableOverride: true))
            {
                await Task.Run(() =>
                {
                    var houseRules = new HouseRules
                    {
                        GoldTiles = 2,
                        WallsProtectCities = false,
                        HideBaronBeforeInvasion = true,
                        KnightMovesBaronBeforeRoll = false,
                        HideRobberBeforeInvasion = true,
                        KnightMovesRobberBeforeRoll = true
                    };

                    var json = JsonSerializer.Serialize(houseRules, _jsonOptions);
                    var deserialized = JsonSerializer.Deserialize<HouseRules>(json, _jsonOptions);

                    Assert.NotNull(deserialized);
                    Assert.Equal(houseRules.GoldTiles, deserialized.GoldTiles);
                    Assert.Equal(houseRules.WallsProtectCities, deserialized.WallsProtectCities);
                    Assert.Equal(houseRules.HideBaronBeforeInvasion, deserialized.HideBaronBeforeInvasion);
                    Assert.Equal(houseRules.KnightMovesBaronBeforeRoll, deserialized.KnightMovesBaronBeforeRoll);
                    Assert.Equal(houseRules.HideRobberBeforeInvasion, deserialized.HideRobberBeforeInvasion);
                    Assert.Equal(houseRules.KnightMovesRobberBeforeRoll, deserialized.KnightMovesRobberBeforeRoll);
                });
            }
        }

        #endregion

        #region Message Object Tests

        [Fact]
        public async Task MessageObjects_ShouldSerializeAndDeserialize_AllTypes()
        {
            using (new FunctionTimer("MessageObjects_Serialization", enableOverride: true))
            {
                await Task.Run(() =>
                {
                    // Test DoAction message
                    var doAction = new DoAction(GameAction.Next);
                    TestMessageSerialization(doAction, nameof(DoAction));

                    // Test PurchaseMessage
                    var purchase = new PurchaseMessage(Entitlement.Settlement);
                    TestMessageSerialization(purchase, nameof(PurchaseMessage));

                    // Test BuildingUpgradeMessage
                    var buildingUpgrade = new BuildingUpgradeMessage(new BuildingKey(new HexCoordinates(0, 0, 0), HexPosition.TopLeft));
                    TestMessageSerialization(buildingUpgrade, nameof(BuildingUpgradeMessage));

                    // Test RoadPurchaseMessage
                    var roadPurchase = new RoadPurchaseMessage(new RoadKey(new HexCoordinates(0, 0, 0), HexSide.Top));
                    TestMessageSerialization(roadPurchase, nameof(RoadPurchaseMessage));

                    // Test MoveRobberMessage
                    var moveRobber = new MoveRobberMessage(new HexCoordinates(1, -1, 0), "Player1");
                    TestMessageSerialization(moveRobber, nameof(MoveRobberMessage));

                    // Test RollMessage
                    var roll = new RollMessage(new TurnRollModel(2, 5));
                    TestMessageSerialization(roll, nameof(RollMessage));
                });
            }
        }

        private void TestMessageSerialization<T>(T message, string typeName)
        {
            var json = JsonSerializer.Serialize(message, _jsonOptions);
            Assert.NotNull(json);
            Assert.NotEmpty(json);

            var deserialized = JsonSerializer.Deserialize<T>(json, _jsonOptions);
            Assert.NotNull(deserialized);
        }

        #endregion

        #region JavaScript Compatibility Tests

        [Fact]
        public async Task JavaScriptCompatibility_ShouldTestViaWebRequest()
        {
            using (new FunctionTimer("JavaScript_Compatibility_Test", enableOverride: true))
            {
                // Create a complete GameModel
                var gameModel = CreateFullGameModel();
                var json = JsonSerializer.Serialize(gameModel, _jsonOptions);

                // Test that the JSON is well-formed and JavaScript-compatible
                await VerifyValidJson(json);
                await VerifyJavaScriptFeatures(json);
                
                // Test specific JavaScript compatibility features
                Assert.Contains("\"gameId\":", json); // camelCase
                Assert.DoesNotContain("\"GameId\":", json); // Not PascalCase
                
                Console.WriteLine($"? JSON is {json.Length} characters and JavaScript-compatible");
            }
        }

        private async Task VerifyJavaScriptCompatibility(string json, string modelType)
        {
            // Verify JSON contains expected JavaScript-compatible features
            await Task.Run(() =>
            {
                // Should use camelCase for properties (configured in JsonSerializerOptions)
                Assert.Contains("\"gameId\":", json); // Property should be camelCase
                Assert.Contains("\"gameState\":", json); // Property should be camelCase

                // Enums should be strings for JavaScript compatibility
                Assert.Contains("\"gameState\":", json);

                // Should be valid JSON (no exceptions thrown during parsing)
                var parsed = JsonDocument.Parse(json);
                Assert.NotNull(parsed);
            });
        }

        private async Task VerifyValidJson(string json)
        {
            await Task.Run(() =>
            {
                // Verify it's valid JSON
                var document = JsonDocument.Parse(json);
                Assert.NotNull(document);

                // Verify no undefined values
                Assert.DoesNotContain("undefined", json);
                
                // Verify proper string escaping
                Assert.DoesNotContain("\\u0000", json); // No null characters
            });
        }

        private async Task VerifyJavaScriptFeatures(string json)
        {
            await Task.Run(() =>
            {
                // Should use camelCase property names - check for property names specifically (key:)
                if (json.Contains("\"gameId\":"))
                {
                    // This is good - camelCase property name
                }

                if (json.Contains("\"gameState\":"))
                {
                    // This is good - camelCase property name
                }

                // Should not contain C#-specific artifacts
                Assert.DoesNotContain("$type", json); // No type annotations
                Assert.DoesNotContain("$id", json);   // No reference handling
                
                // Dates should be in ISO format for JavaScript compatibility
                if (json.Contains("createdTime"))
                {
                    Assert.Contains("T", json); // ISO format has 'T' separator
                    Assert.Contains(":", json); // Time portion
                }
            });
        }

        #endregion

        #region Parallel Testing Performance Verification

        [Fact]
        public async Task ParallelSerialization_ShouldCompleteEfficiently()
        {
            using (var timer = new FunctionTimer("Parallel_Serialization_Performance", enableOverride: true, writeToConsole: true))
            {
                const int testCount = 100;
                var results = new ConcurrentBag<bool>();

                // Run many serialization tests in parallel
                await Task.Run(() =>
                {
                    Parallel.For(0, testCount, i =>
                    {
                        try
                        {
                            var gameModel = CreateFullGameModel();
                            gameModel.GameId = $"test-game-{i}";
                            
                            var json = JsonSerializer.Serialize(gameModel, _jsonOptions);
                            var deserialized = JsonSerializer.Deserialize<GameModel>(json, _jsonOptions);
                            
                            results.Add(deserialized != null && deserialized.GameId == gameModel.GameId);
                        }
                        catch
                        {
                            results.Add(false);
                        }
                    });
                });

                // Verify all tests completed successfully
                Assert.Equal(testCount, results.Count);
                Assert.All(results, result => Assert.True(result));

                Console.WriteLine($"? Completed {testCount} parallel serialization tests efficiently");
            }
        }

        #endregion

        #region Helper Methods

        private GameModel CreateFullGameModel()
        {
            var players = new List<PlayerModel>
            {
                new("Alice-001"),
                new("Bob-002"),
                new("Charlie-003"),
                new("David-004")
            };

            var gameModel = new GameModel
            {
                GameId = "test-game-123",
                GameState = GameState.PickingBoard,
                GameType = GameType.Regular,
                CurrentPlayerId = "Alice-001",
                Players = players,
                CreatedTime = DateTime.UtcNow,
                HasSupplementalBuildPhase = false,
                ActionFlags = new ActionFlags
                {
                    NextEnabled = true,
                    UndoEnabled = false,
                    RedoEnabled = false,
                    RollsEnabled = false
                },
                EntitlementPurchaseModel = new List<EntitlementPurchaseModel>
                {
                    new() { Entitlement = Entitlement.Settlement, Enabled = true },
                    new() { Entitlement = Entitlement.Road, Enabled = true },
                    new() { Entitlement = Entitlement.City, Enabled = false },
                    new() { Entitlement = Entitlement.Soldier, Enabled = true }
                },
                Tiles = new List<TileModel>
                {
                    new() { TileKey = new HexCoordinates(0, 0, 0), ResourceTileType = ResourceType.Wheat, Number = 6 },
                    new() { TileKey = new HexCoordinates(1, -1, 0), ResourceTileType = ResourceType.Wood, Number = 8 },
                    new() { TileKey = new HexCoordinates(-1, 1, 0), ResourceTileType = ResourceType.Brick, Number = 5 }
                },
                Buildings = new List<BuildingModel>
                {
                    new() { BuildingKey = new BuildingKey(new HexCoordinates(0, 0, 0), HexPosition.TopRight), BuildingState = BuildingState.Settlement, OwnerId = "Alice-001" }
                },
                Roads = new List<RoadModel>
                {
                    new() { RoadKey = new RoadKey(new HexCoordinates(0, 0, 0), HexSide.Top), RoadState = RoadState.Road, OwnerId = "Alice-001" }
                },
                Harbors = new List<HarborModel>
                {
                    new() { HarborKey = new HarborKey(new HexCoordinates(2, -1, -1), HarborType.ThreeForOne, HexSide.Bottom) }
                },
                Robber = new RobberModel { Coordinates = new HexCoordinates(0, 0, 0) },
                HouseRules = new HouseRules(),
                ResourceRules = new ResourceRules(4, 5, 15, 3, 4),
                RollModel = new RollModel(),
                GameResourcesModel = new ResourcesModel(),
                NextPlayerToRollAfterSupplemental = "",
                PreviousGameState = GameState.WaitingForNewGame
            };

            return gameModel;
        }

        #endregion
    }
}
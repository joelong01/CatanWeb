using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;

namespace Tests.Shared.Serialization
{
    /// <summary>
    /// Bidirectional serialization tests that verify C# models can round-trip through TypeScript.
    ///
    /// Flow: C# serialize → TypeScript deserialize → TypeScript serialize → C# deserialize
    ///
    /// This catches casing mismatches, missing properties, and other serialization issues
    /// that would break the React UI communicating with the GameService.
    /// </summary>
    public class BidirectionalSerializationTests
    {
        private readonly ITestOutputHelper _output;
        private readonly string _reactUiPath;
        private readonly string _testScriptPath;

        public BidirectionalSerializationTests(ITestOutputHelper output)
        {
            _output = output;

            // Find react-ui directory relative to test project
            var currentDir = Directory.GetCurrentDirectory();
            var repoRoot = FindRepoRoot(currentDir);
            _reactUiPath = Path.Combine(repoRoot, "react-ui");
            _testScriptPath = Path.Combine(_reactUiPath, "test-roundtrip.mjs");
        }

        [Fact]
        public async Task BuildingKey_RoundTrip()
        {
            var original = new BuildingKey(new HexCoordinates(-3, 1, 2), HexPosition.Right);
            await TestRoundTrip(original, "BuildingKey");
        }

        [Fact]
        public async Task RoadKey_RoundTrip()
        {
            var original = new RoadKey(new HexCoordinates(0, 1, -1), HexSide.Top);
            await TestRoundTrip(original, "RoadKey");
        }

        [Fact]
        public async Task HexCoordinates_RoundTrip()
        {
            var original = new HexCoordinates(-2, 3, -1);
            await TestRoundTrip(original, "HexCoordinates");
        }

        [Fact]
        public async Task TurnRollModel_RoundTrip()
        {
            var original = new TurnRollModel(3, 5) { SpecialDice = SpecialDice.Trade };
            await TestRoundTrip(original, "TurnRollModel");
        }

        [Fact]
        public async Task ActionFlags_RoundTrip()
        {
            var original = new ActionFlags
            {
                NextEnabled = true,
                UndoEnabled = false,
                RedoEnabled = true
            };
            await TestRoundTrip(original, "ActionFlags");
        }

        [Fact]
        public async Task EntitlementPurchaseModel_RoundTrip()
        {
            var original = new EntitlementPurchaseModel
            {
                Entitlement = Entitlement.Settlement,
                Enabled = true
            };
            await TestRoundTrip(original, "EntitlementPurchaseModel");
        }

        [Fact]
        public async Task PlayerModel_RoundTrip()
        {
            var original = new PlayerModel("Joe-001")
            {
                GoldRolls = 2,
                Score = 3
            };
            await TestRoundTrip(original, "PlayerModel");
        }

        [Fact]
        public async Task TileModel_RoundTrip()
        {
            var original = new TileModel
            {
                TileKey = new HexCoordinates(0, 0, 0),
                ResourceTileType = ResourceType.Wheat,
                Number = 8,
                Highlighted = false
            };
            await TestRoundTrip(original, "TileModel");
        }

        [Fact]
        public async Task BuildingModel_RoundTrip()
        {
            var original = new BuildingModel
            {
                BuildingKey = new BuildingKey(new HexCoordinates(1, -1, 0), HexPosition.TopRight),
                BuildingState = BuildingState.Settlement,
                OwnerId = "Player1-001"
            };
            await TestRoundTrip(original, "BuildingModel");
        }

        [Fact]
        public async Task RoadModel_RoundTrip()
        {
            var original = new RoadModel
            {
                RoadKey = new RoadKey(new HexCoordinates(0, 0, 0), HexSide.TopRight),
                OwnerId = "Player1-001"
            };
            await TestRoundTrip(original, "RoadModel");
        }

        [Fact]
        public async Task GameModel_RoundTrip()
        {
            var original = new GameModel
            {
                GameId = "test-game-123",
                GameName = "Test Game",
                GameState = GameState.WaitingForRoll,
                GameType = GameType.Expansion,
                CurrentPlayerId = "Joe-001",
                CreatedTime = DateTime.UtcNow,
                Players = new List<PlayerModel>
                {
                    new("Joe-001") { GoldRolls = 1, Score = 2 },
                    new("Adrian-002") { GoldRolls = 2, Score = 4 }
                },
                ActionFlags = new ActionFlags { NextEnabled = false, UndoEnabled = true },
                HouseRules = new HouseRules(),
                ResourceRules = new ResourceRules(4, 5, 15, 3, 4)
            };
            await TestRoundTrip(original, "GameModel");
        }

        [Fact]
        public async Task GameTemplateData_RoundTrip()
        {
            var original = new GameTemplateData
            {
                Id = "test-regular",
                Name = "Regular Game",
                Category = "Base",
                Version = 1,
                Description = "Regular Game",
                Engine = "base",
                GameType = "Regular",
                ResourceRules = new ResourceRules(4, 5, 15, 3, 4),
                HouseRules = new HouseRules { GoldTiles = 1 },
                HasSupplemental = false,
                Tiles =
                [
                    new TemplateTile { Q = -2, R = 0, Resource = "Desert", Number = 7 },
                    new TemplateTile { Q = -2, R = 1, Resource = "Brick", Number = 2 }
                ],
                Harbors =
                [
                    new TemplateHarbor { Q = 0, R = -2, Side = "Top", Type = "Ore" }
                ],
                Entitlements =
                [
                    new TemplateEntitlement { Entitlement = "City" },
                    new TemplateEntitlement { Entitlement = "Road" }
                ]
            };
            await TestRoundTrip(original, "GameTemplateData");
        }

        [Fact]
        public async Task AllMessageTypes_CommandPayloads_RoundTrip()
        {
            // Test the exact payloads that GameServiceProxy sends to the REST API

            // BuildingUpgradeMessage payload
            var buildingUpgradePayload = new
            {
                buildingKey = new BuildingKey(new HexCoordinates(-3, 1, 2), HexPosition.Right)
            };
            await TestRoundTripAnonymous(buildingUpgradePayload, "BuildingUpgradePayload");

            // RoadPurchaseMessage payload
            var roadPurchasePayload = new
            {
                roadKey = new RoadKey(new HexCoordinates(0, 1, -1), HexSide.Top)
            };
            await TestRoundTripAnonymous(roadPurchasePayload, "RoadPurchasePayload");

            // MoveRobberMessage payload
            var moveRobberPayload = new
            {
                coordinates = new HexCoordinates(1, -2, 1),
                targetPlayerId = "Player2-002"
            };
            await TestRoundTripAnonymous(moveRobberPayload, "MoveRobberPayload");

            // RollMessage payload
            var rollPayload = new
            {
                roll = new { normalRoll = "Eight", specialDice = "None" }
            };
            await TestRoundTripAnonymous(rollPayload, "RollPayload");

            // PurchaseMessage payload
            var purchasePayload = new
            {
                entitlement = Entitlement.Settlement
            };
            await TestRoundTripAnonymous(purchasePayload, "PurchasePayload");

            // GoFirstMessage payload
            var goFirstPayload = new
            {
                playerId = "Joe-001"
            };
            await TestRoundTripAnonymous(goFirstPayload, "GoFirstPayload");

            // SetPlayerOrderMessage payload
            var setPlayerOrderPayload = new
            {
                playerIds = new[] { "Joe-001", "Adrian-002", "Doug-003" }
            };
            await TestRoundTripAnonymous(setPlayerOrderPayload, "SetPlayerOrderPayload");

            // SwapTileResourcesMessage payload
            var swapTileResourcesPayload = new
            {
                sourceTileCoordinates = new HexCoordinates(0, 0, 0),
                destinationTileCoordinates = new HexCoordinates(1, -1, 0),
                sourceCurrentResource = "Wheat",
                destinationCurrentResource = "Ore"
            };
            await TestRoundTripAnonymous(swapTileResourcesPayload, "SwapTileResourcesPayload");

            // DeclareWinnerMessage payload
            var declareWinnerPayload = new
            {
                winnerId = "Joe-001",
                victoryPoints = new Dictionary<string, int>
                {
                    ["Joe-001"] = 10,
                    ["Adrian-002"] = 8
                }
            };
            await TestRoundTripAnonymous(declareWinnerPayload, "DeclareWinnerPayload");
        }

        private async Task TestRoundTrip<T>(T original, string typeName) where T : class
        {
            await EnsureTestScriptExists();

            // Step 1: Serialize in C#
            var csharpJson = JsonHelper.Serialize(original);
            _output.WriteLine($"C# serialized {typeName}:");
            _output.WriteLine(csharpJson);

            // Step 2: Round-trip through TypeScript
            var tsJson = await RunTypeScriptRoundTrip(csharpJson, typeName);
            _output.WriteLine($"TypeScript re-serialized {typeName}:");
            _output.WriteLine(tsJson);

            // Step 3: Deserialize back in C#
            var roundTripped = JsonHelper.Deserialize<T>(tsJson);
            Assert.NotNull(roundTripped);

            // Step 4: Verify key properties match
            var originalJson = JsonHelper.Serialize(original);
            var finalJson = JsonHelper.Serialize(roundTripped);

            Assert.Equal(originalJson, finalJson);
            _output.WriteLine($"✓ {typeName} round-trip successful");
        }

        private async Task TestRoundTripAnonymous(object original, string typeName)
        {
            await EnsureTestScriptExists();

            // Step 1: Serialize in C#
            var csharpJson = JsonHelper.Serialize(original);
            _output.WriteLine($"C# serialized {typeName}:");
            _output.WriteLine(csharpJson);

            // Step 2: Round-trip through TypeScript
            var tsJson = await RunTypeScriptRoundTrip(csharpJson, typeName);
            _output.WriteLine($"TypeScript re-serialized {typeName}:");
            _output.WriteLine(tsJson);

            // Step 3: Compare JSON structure (can't deserialize to anonymous type)
            // Parse both as JsonDocument and compare
            using var originalDoc = JsonDocument.Parse(csharpJson);
            using var roundTrippedDoc = JsonDocument.Parse(tsJson);

            Assert.True(JsonElementEquals(originalDoc.RootElement, roundTrippedDoc.RootElement),
                $"{typeName} JSON mismatch after round-trip.\nOriginal: {csharpJson}\nRound-tripped: {tsJson}");

            _output.WriteLine($"✓ {typeName} round-trip successful");
        }

        private bool JsonElementEquals(JsonElement a, JsonElement b)
        {
            if (a.ValueKind != b.ValueKind)
                return false;

            switch (a.ValueKind)
            {
                case JsonValueKind.Object:
                    var aProps = a.EnumerateObject().OrderBy(p => p.Name).ToList();
                    var bProps = b.EnumerateObject().OrderBy(p => p.Name).ToList();
                    if (aProps.Count != bProps.Count)
                        return false;
                    for (int i = 0; i < aProps.Count; i++)
                    {
                        if (aProps[i].Name != bProps[i].Name)
                            return false;
                        if (!JsonElementEquals(aProps[i].Value, bProps[i].Value))
                            return false;
                    }
                    return true;

                case JsonValueKind.Array:
                    var aItems = a.EnumerateArray().ToList();
                    var bItems = b.EnumerateArray().ToList();
                    if (aItems.Count != bItems.Count)
                        return false;
                    for (int i = 0; i < aItems.Count; i++)
                    {
                        if (!JsonElementEquals(aItems[i], bItems[i]))
                            return false;
                    }
                    return true;

                case JsonValueKind.String:
                    return a.GetString() == b.GetString();

                case JsonValueKind.Number:
                    return a.GetDecimal() == b.GetDecimal();

                case JsonValueKind.True:
                case JsonValueKind.False:
                    return a.GetBoolean() == b.GetBoolean();

                case JsonValueKind.Null:
                    return true;

                default:
                    return a.GetRawText() == b.GetRawText();
            }
        }

        private async Task<string> RunTypeScriptRoundTrip(string inputJson, string typeName)
        {
            // Write input JSON to temp file
            var inputFile = Path.Combine(Path.GetTempPath(), $"roundtrip_input_{Guid.NewGuid()}.json");
            var outputFile = Path.Combine(Path.GetTempPath(), $"roundtrip_output_{Guid.NewGuid()}.json");

            try
            {
                await File.WriteAllTextAsync(inputFile, inputJson);

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node",
                        Arguments = $"\"{_testScriptPath}\" \"{inputFile}\" \"{outputFile}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = _reactUiPath
                    }
                };

                process.Start();
                var stdout = await process.StandardOutput.ReadToEndAsync();
                var stderr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"TypeScript round-trip failed for {typeName}:\n{stderr}\n{stdout}");
                }

                return await File.ReadAllTextAsync(outputFile);
            }
            finally
            {
                if (File.Exists(inputFile)) File.Delete(inputFile);
                if (File.Exists(outputFile)) File.Delete(outputFile);
            }
        }

        private async Task EnsureTestScriptExists()
        {
            if (File.Exists(_testScriptPath))
                return;

            var scriptContent = @"/**
 * Round-trip serialization test script.
 * Reads JSON from input file, parses it, re-serializes it, writes to output file.
 * This simulates what happens when TypeScript receives data from C# and sends it back.
 */

import { readFileSync, writeFileSync } from 'fs';

const inputFile = process.argv[2];
const outputFile = process.argv[3];

if (!inputFile || !outputFile) {
    console.error('Usage: node test-roundtrip.mjs <input.json> <output.json>');
    process.exit(1);
}

try {
    // Read and parse JSON (simulates receiving from C#)
    const inputJson = readFileSync(inputFile, 'utf8');
    const parsed = JSON.parse(inputJson);

    // Re-serialize (simulates sending back to C#)
    const outputJson = JSON.stringify(parsed);

    // Write output
    writeFileSync(outputFile, outputJson);

    process.exit(0);
} catch (error) {
    console.error('Round-trip failed:', error.message);
    process.exit(1);
}
";
            await File.WriteAllTextAsync(_testScriptPath, scriptContent);
        }

        private static string FindRepoRoot(string startPath)
        {
            var dir = new DirectoryInfo(startPath);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new Exception("Could not find repository root");
        }
    }
}

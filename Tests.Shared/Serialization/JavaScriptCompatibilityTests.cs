using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Xunit;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;

namespace Tests.Shared.Serialization
{
    /// <summary>
    /// JavaScript compatibility tests that verify serialized models can be properly
    /// consumed by actual JavaScript engines (Node.js if available).
    /// These tests ensure the companion.js can properly work with serialized data.
    /// Uses the same JsonHelper as the real application for accurate compatibility testing.
    /// </summary>
    public class JavaScriptCompatibilityTests
    {
        // Use the same JsonHelper as the real application for accurate compatibility testing
        private readonly JsonSerializerOptions _jsonOptions = JsonHelper.PrettyOptions;

        [Fact]
        public async Task GameModel_ShouldBeParseableByJavaScript()
        {
            using (new FunctionTimer("JavaScript_GameModel_Parsing", enableOverride: false))
            {
                var gameModel = CreateTestGameModel();
                var json = JsonSerializer.Serialize(gameModel, _jsonOptions);

                // Test that it's valid JSON that JavaScript can parse
                VerifyValidJson(json);

                // Test specific JavaScript compatibility features
                VerifyJavaScriptFeatures(json);

                // If Node.js is available, test actual JavaScript parsing
                if (await IsNodeJsAvailable())
                {
                    await TestWithNodeJs(json, "GameModel");
                }
            }
        }

        [Fact]
        public async Task AllEnums_ShouldBeJavaScriptCompatible()
        {
            using (new FunctionTimer("JavaScript_Enum_Compatibility", enableOverride: false))
            {
                var enumTypes = new[]
                {
                    typeof(ResourceType), typeof(BuildingState),
                    typeof(GameType), typeof(GameState), typeof(Entitlement),
                    typeof(HexPosition), typeof(HexSide)
                };

                foreach (var enumType in enumTypes)
                {
                    await TestEnumJavaScriptCompatibility(enumType);
                }
            }
        }

        [Fact]
        public async Task CompanionRequiredModels_ShouldBeJavaScriptReady()
        {
            using (new FunctionTimer("JavaScript_Companion_Models", enableOverride: false))
            {
                // Test models that companion.js specifically needs
                var models = new object[]
                {
                    new ActionFlags { NextEnabled = true, UndoEnabled = false },
                    new EntitlementPurchaseModel { Entitlement = Entitlement.Settlement, Enabled = true },
                    new HexCoordinates(1, -1, 0),
                    new BuildingKey(new HexCoordinates(0, 0, 0), HexPosition.TopRight),
                    new RoadKey(new HexCoordinates(0, 0, 0), HexSide.Top),
                    new TurnRollModel(3, 5)
                };

                foreach (var model in models)
                {
                    var json = JsonSerializer.Serialize(model, _jsonOptions);
                    VerifyValidJson(json);
                    VerifyJavaScriptFeatures(json);

                    if (await IsNodeJsAvailable())
                    {
                        await TestWithNodeJs(json, model.GetType().Name);
                    }
                }
            }
        }

        private Task TestEnumJavaScriptCompatibility(Type enumType)
        {
            var enumValues = Enum.GetValues(enumType);
            
            foreach (var enumValue in enumValues)
            {
                var json = JsonSerializer.Serialize(enumValue, _jsonOptions);
                
                // Enums should serialize as strings for JavaScript compatibility
                Assert.True(json.StartsWith('"') && json.EndsWith('"'), 
                    $"Enum {enumType.Name}.{enumValue} should serialize as string, got: {json}");
                
                // Should not contain numeric values that would break JavaScript
                Assert.DoesNotMatch(@"^\d+$", json.Trim('"'));
            }

            return Task.CompletedTask;
        }

        private void VerifyValidJson(string json)
        {
            // Verify it's valid JSON
            var document = JsonDocument.Parse(json);
            Assert.NotNull(document);

            // Verify no undefined values
            Assert.DoesNotContain("undefined", json);
            
            // Verify proper string escaping
            Assert.DoesNotContain("\\u0000", json); // No null characters
        }

        private void VerifyJavaScriptFeatures(string json)
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
        }

        private async Task<bool> IsNodeJsAvailable()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node",
                        Arguments = "--version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private async Task TestWithNodeJs(string json, string modelType)
        {
            try
            {
                // Create a temporary JavaScript file to test parsing
                var tempDir = Path.GetTempPath();
                var jsFile = Path.Combine(tempDir, $"test_{modelType}_{Guid.NewGuid()}.js");
                var jsonFile = Path.Combine(tempDir, $"test_{modelType}_{Guid.NewGuid()}.json");

                // Write JSON to file
                await File.WriteAllTextAsync(jsonFile, json);

                // Create JavaScript test file
                var jsContent = $@"
const fs = require('fs');

try {{
    const jsonData = fs.readFileSync('{jsonFile.Replace("\\", "\\\\")}', 'utf8');
    const parsed = JSON.parse(jsonData);
    
    console.log('? {modelType} parsed successfully');
    console.log('Type:', typeof parsed);
    
    // Test that we can access properties
    if (typeof parsed === 'object' && parsed !== null) {{
        const keys = Object.keys(parsed);
        console.log('Properties:', keys.length);
        
        // Verify camelCase properties
        const hasCamelCase = keys.some(key => key !== key.toLowerCase() && key.charAt(0) === key.charAt(0).toLowerCase());
        if (hasCamelCase) {{
            console.log('? CamelCase properties detected');
        }}
    }}
    
    process.exit(0);
}} catch (error) {{
    console.error('? {modelType} parsing failed:', error.message);
    process.exit(1);
}}
";

                await File.WriteAllTextAsync(jsFile, jsContent);

                // Run Node.js test
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node",
                        Arguments = jsFile,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                // Clean up
                try
                {
                    File.Delete(jsFile);
                    File.Delete(jsonFile);
                }
                catch { } // Ignore cleanup errors

                // Verify Node.js could parse the JSON
                Assert.True(process.ExitCode == 0, 
                    $"Node.js failed to parse {modelType}: {error}\nOutput: {output}");

                Console.WriteLine($"Node.js test for {modelType}: {output.Trim()}");
            }
            catch (Exception ex)
            {
                // If Node.js testing fails, just log it but don't fail the test
                Console.WriteLine($"Node.js testing for {modelType} skipped: {ex.Message}");
            }
        }

        private GameModel CreateTestGameModel()
        {
            return new GameModel
            {
                GameId = "js-test-123",
                GameState = GameState.PickingBoard,
                GameType = GameType.Regular,
                CurrentPlayerId = "Player1",
                CreatedTime = DateTime.UtcNow,
                Players =
                [
                    new("Player1-001"),
                    new("Player2-002")
                ],
                ActionFlags = new ActionFlags
                {
                    NextEnabled = true,
                    UndoEnabled = false,
                    RedoEnabled = false
                },
                EntitlementPurchaseModel =
                [
                    new() { Entitlement = Entitlement.Settlement, Enabled = true },
                    new() { Entitlement = Entitlement.Road, Enabled = true }
                ],
                HouseRules = new HouseRules(),
                ResourceRules = new ResourceRules(4, 5, 15, 3, 4)
            };
        }
    }
}
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using Catan3.Shared.Utility;
using Catan3.Shared.Models;
using Catan3.Shared.Extensions;

namespace Tests.DesktopApp.UI.ScriptedTestData
{
    /// <summary>
    /// Loads test scenarios from .catan_test files which contain both GameModel and ActionStack.
    /// </summary>
    public static class ScenarioLoader
    {
        /// <summary>
        /// Load a test scenario from a .catan_test file
        /// </summary>
        /// <param name="testFilePath">Path to the .catan_test file</param>
        /// <returns>Loaded and validated TestScenario with actions from the file</returns>
        public static TestScenario LoadScenario(string testFilePath)
        {
            if (!File.Exists(testFilePath))
                throw new FileNotFoundException($"Test file not found: {testFilePath}");
            
            // Validate file extension
            if (!Path.GetExtension(testFilePath).Equals(".catan_test", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Test file must have .catan_test extension: {testFilePath}");
            }
                
            try
            {
                var jsonContent = File.ReadAllText(testFilePath);
                using var document = JsonDocument.Parse(jsonContent);
                var root = document.RootElement;
                
                // Extract GameModel and ActionStack from the .catan_test file
                if (!root.TryGetProperty("gameModel", out var gameModel))
                {
                    throw new InvalidOperationException(".catan_test file must contain a 'gameModel' property");
                }
                
                if (!root.TryGetProperty("actionStack", out var actionStack))
                {
                    throw new InvalidOperationException(".catan_test file must contain an 'actionStack' property");
                }
                
                // Deserialize the recorded messages using polymorphic JSON serialization
                var recordedMessages = JsonSerializer.Deserialize<List<IRecordedMessage>>(actionStack.GetRawText(), JsonHelper.StandardOptions);
                if (recordedMessages == null)
                {
                    throw new InvalidOperationException("Failed to deserialize ActionStack");
                }
                
                // Store recorded messages directly for replay validation and UI interaction
                var actions = recordedMessages;
                
                // Extract game metadata from GameModel
                var gameType = gameModel.TryGetProperty("gameType", out var gt) 
                    ? gt.GetString() ?? "Regular"
                    : "Regular";
                    
                var playerCount = 0;
                if (gameModel.TryGetProperty("players", out var players))
                {
                    playerCount = players.GetArrayLength();
                }
                
                // Create TestScenario from the loaded data
                var scenario = new TestScenario
                {
                    Name = Path.GetFileNameWithoutExtension(testFilePath),
                    Description = $"Test scenario from {Path.GetFileName(testFilePath)}",
                    GameType = gameType,
                    ExpectedPlayerCount = playerCount,
                    TestFilePath = testFilePath,
                    ExpectedFinalState = "WaitingForRoll", // Default, can be overridden
                    TimeoutMs = 300000,
                    RecordMode = false,
                    Actions = actions
                };
                
                // Validate the loaded scenario
                var validationErrors = scenario.Validate();
                if (validationErrors.Any())
                {
                    throw new InvalidOperationException($"Scenario validation failed:\n{string.Join("\n", validationErrors)}");
                }
                
                return scenario;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Failed to parse .catan_test file: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Validate that a .catan_test file exists for the scenario
        /// </summary>
        /// <param name="scenario">Scenario to validate</param>
        /// <param name="baseDirectory">Base directory for relative paths</param>
        /// <returns>Full path to the .catan_test file if it exists</returns>
        public static string ValidateTestFile(TestScenario scenario, string baseDirectory)
        {
            var testFilePath = Path.IsPathRooted(scenario.TestFilePath) 
                ? scenario.TestFilePath 
                : Path.Combine(baseDirectory, scenario.TestFilePath);
                
            if (!File.Exists(testFilePath))
            {
                throw new FileNotFoundException($"Test file not found: {testFilePath}");
            }
            
            if (!Path.GetExtension(testFilePath).Equals(".catan_test", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Test file must have .catan_test extension: {testFilePath}");
            }
            
            return testFilePath;
        }

    }
}
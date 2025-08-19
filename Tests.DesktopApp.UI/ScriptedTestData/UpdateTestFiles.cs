using System;
using System.IO;
using System.Text.Json;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;

namespace Tests.DesktopApp.UI.ScriptedTestData
{
    /// <summary>
    /// Utility to update .catan_test files to use the correct JsonSerializerOptions
    /// </summary>
    public static class UpdateTestFiles
    {
        public static void UpdateAllTestFiles()
        {
            var testDataDir = Path.GetDirectoryName(typeof(UpdateTestFiles).Assembly.Location) 
                ?? throw new InvalidOperationException("Unable to determine test data directory");
            var catanTestFiles = Directory.GetFiles(testDataDir, "*.catan_test", SearchOption.AllDirectories);
            
            foreach (var file in catanTestFiles)
            {
                try
                {
                    Console.WriteLine($"Updating {file}...");
                    UpdateTestFile(file);
                    Console.WriteLine($"✓ Updated {file}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Failed to update {file}: {ex.Message}");
                }
            }
        }
        
        private static void UpdateTestFile(string filePath)
        {
            // Read the file with flexible options first
            var flexibleOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
            
            var jsonContent = File.ReadAllText(filePath);
            using var document = JsonDocument.Parse(jsonContent);
            var root = document.RootElement;
            
            // Extract and convert GameModel
            if (root.TryGetProperty("GameModel", out var gameModelElement))
            {
                var gameModel = gameModelElement.Deserialize<GameModel>(flexibleOptions);
                if (gameModel == null)
                {
                    throw new InvalidOperationException("Failed to deserialize GameModel");
                }
                
                // Extract ActionStack as raw JSON (it doesn't need conversion)
                JsonElement actionStackElement = default;
                root.TryGetProperty("ActionStack", out actionStackElement);
                
                // Create the updated structure
                var updatedData = new
                {
                    GameModel = gameModel,
                    ActionStack = JsonSerializer.Deserialize<object[]>(actionStackElement.GetRawText(), flexibleOptions)
                };
                
                // Serialize with correct JsonHelper options
                var updatedJson = JsonSerializer.Serialize(updatedData, JsonHelper.PrettyOptions);
                
                // Write back to file
                File.WriteAllText(filePath, updatedJson);
            }
        }
    }
}
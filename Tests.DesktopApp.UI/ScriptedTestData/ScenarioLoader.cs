using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Catan3.Shared.Utility;

namespace Tests.DesktopApp.UI.ScriptedTestData
{
    /// <summary>
    /// Loads test scenarios from JSON files and validates their structure.
    /// Handles deserialization and provides helper methods for scenario management.
    /// </summary>
    public static class ScenarioLoader
    {
        /// <summary>
        /// Load a test scenario from a JSON file
        /// </summary>
        /// <param name="scenarioFilePath">Path to the scenario JSON file</param>
        /// <returns>Loaded and validated TestScenario</returns>
        public static TestScenario LoadScenario(string scenarioFilePath)
        {
            if (!File.Exists(scenarioFilePath))
                throw new FileNotFoundException($"Scenario file not found: {scenarioFilePath}");
                
            try
            {
                var jsonContent = File.ReadAllText(scenarioFilePath);
                var scenario = JsonSerializer.Deserialize<TestScenario>(jsonContent, JsonHelper.StandardOptions);
                
                if (scenario == null)
                    throw new InvalidOperationException("Failed to deserialize scenario - result was null");
                
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
                throw new InvalidOperationException($"Failed to parse scenario JSON: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Validate that a .catan file exists for the scenario
        /// </summary>
        /// <param name="scenario">Scenario to validate</param>
        /// <param name="baseDirectory">Base directory for relative paths</param>
        /// <returns>Full path to the .catan file if it exists</returns>
        public static string ValidateCatanFile(TestScenario scenario, string baseDirectory)
        {
            var catanFilePath = Path.IsPathRooted(scenario.TestFilePath) 
                ? scenario.TestFilePath 
                : Path.Combine(baseDirectory, scenario.TestFilePath);
                
            if (!File.Exists(catanFilePath))
            {
                throw new FileNotFoundException($"Catan test file not found: {catanFilePath}");
            }
            
            if (!Path.GetExtension(catanFilePath).Equals(".catan", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Test file must have .catan extension: {catanFilePath}");
            }
            
            return catanFilePath;
        }
    }
}
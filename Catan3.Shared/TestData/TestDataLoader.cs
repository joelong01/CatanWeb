using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Catan3.Shared.Models;

namespace Catan3.Shared.TestData
{
    /// <summary>
    /// Provides access to shared test data files embedded in the assembly.
    /// These test files can be used by both Desktop and GameService tests to ensure
    /// consistent game logic behavior across different implementations.
    /// </summary>
    public static class TestDataLoader
    {
        /// <summary>
        /// Loads a test scenario from an embedded resource file.
        /// </summary>
        /// <param name="testFileName">The name of the test file (e.g., "Expansion.catan_test")</param>
        /// <returns>The loaded test scenario with GameModel and action stack</returns>
        public static async Task<TestScenario> LoadTestScenarioAsync(string testFileName)
        {
            // Get the Shared assembly where the test data is actually embedded
            var assembly = typeof(TestDataLoader).Assembly;
            var resourceName = $"Catan3.Shared.TestData.{testFileName}";

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException($"Test file '{testFileName}' not found as embedded resource");

            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty("gameModel", out var gameModelElement))
            {
                throw new InvalidOperationException($"Test file '{testFileName}' is missing 'gameModel' property");
            }

            if (!root.TryGetProperty("actionStack", out var actionStackElement))
            {
                throw new InvalidOperationException($"Test file '{testFileName}' is missing 'actionStack' property");
            }

            var gameModel = gameModelElement.Deserialize<GameModel>(Utility.JsonHelper.StandardOptions)
                ?? throw new InvalidOperationException($"Failed to deserialize GameModel from '{testFileName}'");

            var actions = actionStackElement.Deserialize<IRecordedMessage[]>(Utility.JsonHelper.StandardOptions)
                ?? Array.Empty<IRecordedMessage>();

            return new TestScenario
            {
                TestFileName = testFileName,
                InitialGameModel = gameModel,
                RecordedActions = actions
            };
        }

        /// <summary>
        /// Gets the stream for a test file embedded resource.
        /// </summary>
        /// <param name="testFileName">The name of the test file</param>
        /// <returns>Stream containing the test file data</returns>
        public static Stream GetTestFileStream(string testFileName)
        {
            // Get the Shared assembly where the test data is actually embedded
            var assembly = typeof(TestDataLoader).Assembly;
            var resourceName = $"Catan3.Shared.TestData.{testFileName}";

            return assembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException($"Test file '{testFileName}' not found as embedded resource");
        }

        /// <summary>
        /// Lists all available test files.
        /// </summary>
        /// <returns>Array of test file names</returns>
        public static string[] GetAvailableTestFiles()
        {
            // Get the Shared assembly where the test data is actually embedded
            var assembly = typeof(TestDataLoader).Assembly;
            var prefix = "Catan3.Shared.TestData.";
            var suffix = ".catan_test";
            
            var resources = assembly.GetManifestResourceNames();
            var testFiles = new System.Collections.Generic.List<string>();

            foreach (var resource in resources)
            {
                if (resource.StartsWith(prefix) && resource.EndsWith(suffix))
                {
                    var fileName = resource.Substring(prefix.Length);
                    testFiles.Add(fileName);
                }
            }

            return testFiles.ToArray();
        }
    }

    /// <summary>
    /// Represents a test scenario loaded from a .catan_test file.
    /// </summary>
    public class TestScenario
    {
        /// <summary>
        /// The name of the test file this scenario was loaded from.
        /// </summary>
        public string TestFileName { get; set; } = string.Empty;

        /// <summary>
        /// The initial GameModel state at the start of the test.
        /// </summary>
        public GameModel InitialGameModel { get; set; } = new GameModel();

        /// <summary>
        /// The sequence of recorded actions to replay.
        /// </summary>
        public IRecordedMessage[] RecordedActions { get; set; } = Array.Empty<IRecordedMessage>();

        /// <summary>
        /// Gets the expected final game hash after all actions are replayed.
        /// This is typically the gameHash from the last action's expectedGameHash property.
        /// </summary>
        public string GetExpectedFinalHash()
        {
            if (RecordedActions.Length == 0)
                return InitialGameModel.GameHash;

            var lastAction = RecordedActions[RecordedActions.Length - 1];
            return lastAction.ExpectedGameHash ?? InitialGameModel.GameHash;
        }
    }
}
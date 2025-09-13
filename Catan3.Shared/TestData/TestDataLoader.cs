using System.Text.Json;
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
        /// Loads a test scenario from the Tests/Data directory.
        /// </summary>
        /// <param name="testFileName">The name of the test file (e.g., "Expansion.catan_test")</param>
        /// <returns>The loaded test scenario with GameModel and action stack</returns>
        public static async Task<TestScenario> LoadTestScenarioAsync(string testFileName)
        {
            var testDataPath = Path.Combine(GetSolutionRoot(), "Tests", "Data", testFileName);
            
            if (!File.Exists(testDataPath))
            {
                throw new FileNotFoundException($"Test file '{testFileName}' not found at: {testDataPath}");
            }

            var json = await File.ReadAllTextAsync(testDataPath);

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
        /// Gets the stream for a test file from Tests/Data directory.
        /// </summary>
        /// <param name="testFileName">The name of the test file</param>
        /// <returns>Stream containing the test file data</returns>
        public static Stream GetTestFileStream(string testFileName)
        {
            var testDataPath = Path.Combine(GetSolutionRoot(), "Tests", "Data", testFileName);
            
            if (!File.Exists(testDataPath))
            {
                throw new FileNotFoundException($"Test file '{testFileName}' not found at: {testDataPath}");
            }

            return File.OpenRead(testDataPath);
        }

        /// <summary>
        /// Lists all available test files in Tests/Data directory.
        /// </summary>
        /// <returns>Array of test file names</returns>
        public static string[] GetAvailableTestFiles()
        {
            var testDataDir = Path.Combine(GetSolutionRoot(), "Tests", "Data");
            
            if (!Directory.Exists(testDataDir))
            {
                return Array.Empty<string>();
            }

            return Directory.GetFiles(testDataDir, "*.catan_test")
                .Select(Path.GetFileName)
                .Where(name => name != null)
                .Cast<string>()
                .ToArray();
        }

        /// <summary>
        /// Gets the solution root directory by walking up from the current assembly location.
        /// </summary>
        /// <returns>Path to the solution root directory</returns>
        private static string GetSolutionRoot()
        {
            var assemblyLocation = typeof(TestDataLoader).Assembly.Location;
            var currentDir = Path.GetDirectoryName(assemblyLocation);
            
            while (currentDir != null)
            {
                if (File.Exists(Path.Combine(currentDir, "Catan.sln")))
                {
                    return currentDir;
                }
                currentDir = Path.GetDirectoryName(currentDir);
            }
            
            throw new InvalidOperationException("Could not find solution root directory (Catan.sln not found)");
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
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;

namespace Catan3.CLI.Services;

/// <summary>
/// Service to test serialization and deserialization of all MVVM message objects
/// Ensures that constructor parameters work correctly with JSON serialization/deserialization
/// </summary>
public class MvvmObjectTester
{
    private readonly ILogger<MvvmObjectTester> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public MvvmObjectTester(ILogger<MvvmObjectTester> logger)
    {
        _logger = logger;
        _jsonOptions = JsonHelper.PrettyOptions;
    }

    /// <summary>
    /// Tests serialization and deserialization of all MVVM message objects
    /// </summary>
    public async Task TestAllMvvmObjectsAsync()
    {
        var timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
        Console.WriteLine($"[{timestamp}] [?? MVVM TEST] Starting comprehensive MVVM object serialization tests");
        Console.WriteLine();

        var testResults = new TestResults();

        // Test each MVVM message object
        await TestObject(() => new ExecuteGameActionMessage(GameAction.Shuffle), "ExecuteGameActionMessage", testResults);
        await TestObject(() => new PurchaseMessage(Entitlement.Settlement), "PurchaseMessage", testResults);
        await TestObject(() => new RoadPurchaseMessage(new RoadKey(new HexCoordinates(0, 0, 0), HexSide.TopRight)), "RoadPurchaseMessage", testResults);
        await TestObject(() => new BuildingUpgradeMessage(new BuildingKey(new HexCoordinates(1, -1, 0), HexPosition.Right)), "BuildingUpgradeMessage", testResults);
        await TestObject(() => new MoveRobberMessage(new HexCoordinates(2, -1, -1), "TestPlayer"), "MoveRobberMessage", testResults);
        await TestObject(() => new RollMessage(new TurnRollModel(3, 4)), "RollMessage", testResults);
        await TestObject(() => new NewGameMessage(GameType.Regular, ["Alice", "Bob", "Charlie"]), "NewGameMessage", testResults);
        await TestObject(() => new SetPlayerOrderMessage(["Alice", "Bob", "Charlie"]), "SetPlayerOrderMessage", testResults);
        await TestObject(() => new PlayersDoingSupplemental(["Alice", "Bob"]), "PlayersDoingSupplemental", testResults);
        await TestObject(() => new GoFirstMessage("Alice"), "GoFirstMessage", testResults);
        await TestObject(() => new BalanceBoardMessage(), "BalanceBoardMessage", testResults);
        await TestObject(() => new EndGame(), "EndGame", testResults);
        await TestObject(() => new ErrorMessage("Test error", ErrorLevel.Information), "ErrorMessage", testResults);
        await TestObject(() => new PersistGameMessage(LocalPersistActions.Save, "/test/path"), "PersistGameMessage", testResults);
        await TestObject(() => new LoadGameMessage("/test/game.json"), "LoadGameMessage", testResults);

        // Test with nullable parameters
        await TestObject(() => new MoveRobberMessage(new HexCoordinates(0, 0, 0), null), "MoveRobberMessage (null target)", testResults);

        // Test UpdateGameModel with a basic GameModel
        await TestUpdateGameModel(testResults);

        Console.WriteLine();
        var finalTimestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
        
        if (testResults.FailedTests == 0)
        {
            Console.WriteLine($"[{finalTimestamp}] [? SUCCESS] All {testResults.TotalTests} MVVM object tests passed!");
            Console.WriteLine($"[{finalTimestamp}] [?? SUMMARY] {testResults.PassedTests} passed, {testResults.FailedTests} failed");
        }
        else
        {
            Console.WriteLine($"[{finalTimestamp}] [? FAILURE] {testResults.FailedTests} out of {testResults.TotalTests} MVVM object tests failed!");
            Console.WriteLine($"[{finalTimestamp}] [?? SUMMARY] {testResults.PassedTests} passed, {testResults.FailedTests} failed");
            throw new InvalidOperationException($"MVVM object serialization tests failed: {testResults.FailedTests} failures out of {testResults.TotalTests} tests");
        }
    }

    /// <summary>
    /// Tests a specific MVVM object type
    /// </summary>
    private async Task TestObject<T>(Func<T> objectFactory, string objectName, TestResults testResults)
    {
        testResults.TotalTests++;
        var timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
        
        try
        {
            // Create original object
            var original = objectFactory();
            LogEvent("?? CREATING", $"Testing {objectName}: {original?.GetType().Name}");

            // Serialize to JSON
            var json = JsonSerializer.Serialize(original, _jsonOptions);
            LogEvent("?? SERIALIZE", $"{objectName} serialized to JSON ({json.Length} characters)");
            LogEvent("?? JSON", $"{objectName} JSON: {json}");

            // Deserialize back to object
            var deserialized = JsonSerializer.Deserialize<T>(json, _jsonOptions);
            LogEvent("?? DESERIALIZE", $"{objectName} deserialized from JSON");

            // Verify deserialization succeeded
            if (deserialized == null)
            {
                throw new InvalidOperationException($"Deserialization returned null for {objectName}");
            }

            // Verify properties match (basic verification)
            await VerifyObjectProperties(original, deserialized, objectName);

            testResults.PassedTests++;
            LogEvent("? PASS", $"{objectName} serialization/deserialization successful");
        }
        catch (Exception ex)
        {
            testResults.FailedTests++;
            LogEvent("? FAIL", $"{objectName} test failed: {ex.Message}");
            _logger.LogError(ex, "MVVM object test failed for {ObjectName}", objectName);
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Tests UpdateGameModel which requires a GameModel parameter
    /// </summary>
    private async Task TestUpdateGameModel(TestResults testResults)
    {
        testResults.TotalTests++;
        await Task.Delay(0);
        try
        {
            // Create a basic GameModel for testing
            var gameModel = CreateTestGameModel();
            var original = new UpdateGameModel(gameModel);
            
            LogEvent("?? CREATING", "Testing UpdateGameModel with test GameModel");

            // Serialize to JSON
            var json = JsonSerializer.Serialize(original, _jsonOptions);
            LogEvent("?? SERIALIZE", $"UpdateGameModel serialized to JSON ({json.Length} characters)");

            // Deserialize back to object
            var deserialized = JsonSerializer.Deserialize<UpdateGameModel>(json, _jsonOptions);
            LogEvent("?? DESERIALIZE", "UpdateGameModel deserialized from JSON");

            // Verify deserialization succeeded
            if (deserialized?.GameModel == null)
            {
                throw new InvalidOperationException("UpdateGameModel deserialization failed - GameModel is null");
            }

            // Basic verification that GameModel properties are preserved
            if (deserialized.GameModel.GameType != original.GameModel.GameType)
            {
                throw new InvalidOperationException($"GameType mismatch: {deserialized.GameModel.GameType} != {original.GameModel.GameType}");
            }

            if (deserialized.GameModel.GameState != original.GameModel.GameState)
            {
                throw new InvalidOperationException($"GameState mismatch: {deserialized.GameModel.GameState} != {original.GameModel.GameState}");
            }

            testResults.PassedTests++;
            LogEvent("? PASS", "UpdateGameModel serialization/deserialization successful");
        }
        catch (Exception ex)
        {
            testResults.FailedTests++;
            LogEvent("? FAIL", $"UpdateGameModel test failed: {ex.Message}");
            _logger.LogError(ex, "UpdateGameModel test failed");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Creates a minimal test GameModel for UpdateGameModel testing
    /// </summary>
    private GameModel CreateTestGameModel()
    {
        var players = new List<PlayerModel>
        {
            new() { Id = "TestPlayer1" },
            new() { Id = "TestPlayer2" }
        };

        return new GameModel
        {
            GameType = GameType.Regular,
            GameState = GameState.PickingBoard,
            Players = players,
            GameId = "TestGame"
            // Note: GameStateMachineVersion is read-only property = 1, so we don't set it
        };
    }

    /// <summary>
    /// Verifies that key properties match between original and deserialized objects
    /// </summary>
    private async Task VerifyObjectProperties<T>(T original, T deserialized, string objectName)
    {
        await Task.CompletedTask;
        if (original == null || deserialized == null)
        {
            throw new InvalidOperationException($"Cannot verify null objects for {objectName}");
        }

        var type = typeof(T);
        
        // Use reflection to check key properties
        foreach (var property in type.GetProperties())
        {
            try
            {
                var originalValue = property.GetValue(original);
                var deserializedValue = property.GetValue(deserialized);

                // Handle different comparison types
                if (originalValue != null && deserializedValue != null)
                {
                    // For IList properties, compare counts
                    if (originalValue is System.Collections.IList originalList && deserializedValue is System.Collections.IList deserializedList)
                    {
                        if (originalList.Count != deserializedList.Count)
                        {
                            throw new InvalidOperationException($"Property {property.Name} list count mismatch: {originalList.Count} != {deserializedList.Count}");
                        }
                    }
                    // For simple properties, compare values
                    else if (!originalValue.Equals(deserializedValue))
                    {
                        throw new InvalidOperationException($"Property {property.Name} value mismatch: {originalValue} != {deserializedValue}");
                    }
                }
                else if (originalValue != deserializedValue) // Handle null mismatches
                {
                    throw new InvalidOperationException($"Property {property.Name} null mismatch: {originalValue} != {deserializedValue}");
                }
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                // Skip properties that can't be compared (like complex objects)
                LogEvent("?? WARN", $"Could not verify property {property.Name}: {ex.Message}");
            }
        }

        LogEvent("?? VERIFY", $"{objectName} property verification completed");
    }

    /// <summary>
    /// Logs events with consistent formatting
    /// </summary>
    private void LogEvent(string eventType, string message)
    {
        var timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
        Console.WriteLine($"[{timestamp}] [{eventType}] {message}");
    }
}

/// <summary>
/// Helper class to track test results since async methods can't use ref parameters
/// </summary>
public class TestResults
{
    public int TotalTests { get; set; } = 0;
    public int PassedTests { get; set; } = 0;
    public int FailedTests { get; set; } = 0;
}
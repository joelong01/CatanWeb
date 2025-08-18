using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Tests.DesktopApp.UI.ScriptedTestData
{
    /// <summary>
    /// Represents a single scripted action that can be executed during UI testing.
    /// Actions are executed in sequence for each player during their turn.
    /// </summary>
    public class TestAction
    {
        /// <summary>
        /// The type of action to execute
        /// </summary>
        public ActionType Type { get; set; }
        
        /// <summary>
        /// The player ID who should execute this action.
        /// If null, action applies to current player.
        /// </summary>
        public string? PlayerId { get; set; }
        
        /// <summary>
        /// Parameters specific to the action type.
        /// Contents depend on ActionType - see documentation for each action.
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new();
        
        /// <summary>
        /// Expected game state after this action completes.
        /// Used for validation - test will assert this state is reached.
        /// </summary>
        public string? ExpectedState { get; set; }
        
        /// <summary>
        /// Human-readable description of what this action does.
        /// Used for logging and debugging test failures.
        /// </summary>
        public string? Description { get; set; }
        
        /// <summary>
        /// Optional delay in milliseconds to wait after executing this action.
        /// Useful for allowing UI updates to complete.
        /// </summary>
        public int DelayMs { get; set; } = 0;
        
        /// <summary>
        /// Whether this action is optional - if it fails, continue with next action.
        /// Useful for actions that may not be available in all game states.
        /// </summary>
        public bool Optional { get; set; } = false;
        
        /// <summary>
        /// Sequence number for this action within the player's turn.
        /// Used for ordering actions when multiple actions occur in same turn.
        /// </summary>
        public int Sequence { get; set; } = 0;
    }
    
    /// <summary>
    /// Constants for TestAction parameter names to ensure consistency and provide IntelliSense support.
    /// 
    /// Parameter Categories:
    /// - UI Element Parameters: AutomationId (primary), BuildingKey/RoadKey (legacy)
    /// - Game Action Parameters: Roll values, player selections, coordinates
    /// - Test Control Parameters: Wait times, validation criteria
    /// 
    /// AutomationId Format:
    /// - Buildings: "Building-(-3,3,0)-Right"
    /// - Roads: "Road-(-2,2,0)-Bottom" 
    /// - Tiles: "Tile-(-1,2,1)"
    /// - Rolls: "Roll-6"
    /// 
    /// Usage: Reference these constants instead of hardcoded strings in action parameter dictionaries.
    /// </summary>
    public static class ActionParameters
    {
        // Building placement parameters
        public const string BuildingKey = "buildingKey";
        public const string RoadKey = "roadKey";
        public const string AutomationId = "automationId";
        
        // Dice roll parameters
        public const string Roll = "roll";
        public const string Die1 = "die1";
        public const string Die2 = "die2";
        
        // Robber movement parameters
        public const string Coordinates = "coordinates";
        public const string TargetPlayer = "targetPlayer";
        
        // Player selection parameters
        public const string PlayerIds = "playerIds";
        public const string PlayerId = "playerId";
        
        // Resource parameters
        public const string ResourceType = "resourceType";
        public const string ResourceCount = "resourceCount";
        
        // File path parameters
        public const string FilePath = "filePath";
        
        // Game setup parameters
        public const string GameType = "gameType";
        public const string PlayerCount = "playerCount";
        
        // Wait parameters
        public const string WaitMs = "waitMs";
        
        // Validation parameters
        public const string ExpectedValue = "expectedValue";
        public const string PropertyName = "propertyName";
    }
}
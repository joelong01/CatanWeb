using Catan3.Shared.Models;
using System.Collections.Generic;
using System.Linq;

namespace Tests.DesktopApp.UI.ScriptedTestData
{
    /// <summary>
    /// Represents a complete test scenario with a predefined game file and sequence of scripted actions.
    /// 
    /// Structure:
    /// - Combined .catan_test file containing both GameModel and ActionStack
    /// - Ordered sequence of TestActions that represent user interactions
    /// - Expected states for validation at each step
    /// - Metadata for test configuration and validation
    /// 
    /// Deterministic Testing:
    /// - Uses fixed GameModel to ensure consistent starting conditions
    /// - Actions contain explicit parameters (AutomationIds) for predictable behavior
    /// - Expected states allow verification that actions have correct effects
    /// 
    /// Data Sources:
    /// - .catan_test files which contain both GameModel and ActionStack
    /// - Recorded during manual gameplay or constructed for specific test cases
    /// 
    /// Execution: Loaded by ScenarioLoader and executed by ActionExecutor via FullCyclePackagedUiTests.
    /// </summary>
    public class TestScenario
    {
        /// <summary>
        /// Name/description of this test scenario
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Path to the predefined .catan game file to load.
        /// Relative to the test project root.
        /// </summary>
        public string TestFilePath { get; set; } = string.Empty;
        
        /// <summary>
        /// Description of what this scenario tests
        /// </summary>
        public string? Description { get; set; }
        
        /// <summary>
        /// Game type expected in the test file (Regular, Expansion, etc.)
        /// </summary>
        public string? GameType { get; set; }
        
        /// <summary>
        /// Expected number of players in the loaded game
        /// </summary>
        public int ExpectedPlayerCount { get; set; }
        
        /// <summary>
        /// Chronological list of recorded messages to replay in sequence
        /// </summary>
        public List<IRecordedMessage> Actions { get; set; } = [];
        
        /// <summary>
        /// Expected final game state when all actions are completed
        /// </summary>
        public string? ExpectedFinalState { get; set; }
        
        /// <summary>
        /// Maximum time in milliseconds this scenario should take to execute
        /// </summary>
        public int TimeoutMs { get; set; } = 300000; // 5 minutes default
        
        /// <summary>
        /// Whether to record actions during execution for future scenarios
        /// </summary>
        public bool RecordMode { get; set; } = false;
        
        /// <summary>
        /// Get total number of actions in the scenario
        /// </summary>
        /// <returns>Total action count</returns>
        public int GetTotalActionCount()
        {
            return Actions.Count;
        }
        
        /// <summary>
        /// Get summary of scenario 
        /// </summary>
        /// <returns>Human-readable scenario description</returns>
        public string GetSummary()
        {
            return $"Scenario '{Name}': {Actions.Count} actions, Expected players: {ExpectedPlayerCount}";
        }
        
        /// <summary>
        /// Validate that the scenario is properly configured
        /// </summary>
        /// <returns>List of validation errors, empty if valid</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();
            
            if (string.IsNullOrEmpty(Name))
                errors.Add("Scenario name is required");
                
            if (string.IsNullOrEmpty(TestFilePath))
                errors.Add("Test file path is required");
                
            if (ExpectedPlayerCount <= 0)
                errors.Add("Expected player count must be greater than 0");
                
            if (Actions.Count == 0)
                errors.Add("Scenario must have at least one action defined");
            
            // Validation for recorded messages - just check that we have valid game hashes
            if (Actions.Any(a => string.IsNullOrEmpty(a.ExpectedGameHash)))
                errors.Add("All recorded messages must have a valid ExpectedGameHash");
            
            return errors;
        }
    }
}
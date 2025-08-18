using System.ComponentModel;

namespace Tests.DesktopApp.UI.ScriptedTestData
{
    /// <summary>
    /// Represents the types of actions that can be scripted in UI tests.
    /// These actions abstract the MVVM messages used by the game controller.
    /// Physical cards/dice are used for resources and development cards - this tracks entitlements only.
    /// </summary>
    public enum ActionType
    {
        // Game flow actions
        [Description("Roll dice with specified value (physical dice input)")]
        RollDice,
        
        [Description("Click Next button to advance game state")]
        AdvanceNext,
        
        [Description("Select players to go first during roll order")]
        GoFirst,
        
        // Building placement actions (using purchased entitlements)
        [Description("Place settlement at specified location")]
        PlaceSettlement,
        
        [Description("Place road at specified location")]
        PlaceRoad,
        
        [Description("Upgrade settlement to city")]
        UpgradeToCity,
        
        // Entitlement purchase actions (what players actually buy)
        [Description("Purchase road entitlement")]
        PurchaseRoad,
        
        [Description("Purchase settlement entitlement")]
        PurchaseSettlement,
        
        [Description("Purchase city entitlement")]
        PurchaseCity,
        
        [Description("Purchase soldier entitlement (ability to move robber)")]
        PurchaseSoldier,
        
        // Robber and special actions
        [Description("Move robber to specified tile and optionally target player")]
        MoveRobber,
        
        [Description("Select which players participate in supplemental building")]
        SelectSupplementalPlayers,
        
        // Board setup actions
        [Description("Shuffle board layout")]
        ShuffleBoard,
        
        [Description("Use Previous Board (undo)")]
        PreviousBoard,
        
        [Description("Use Redo Board")]
        RedoBoard,
        
        // Game setup actions
        [Description("Load predefined game file")]
        LoadGame,
        
        [Description("Start new game with specified settings")]
        StartNewGame,
        
        // Validation actions (for testing)
        [Description("Verify current game state matches expected")]
        VerifyGameState,
        
        [Description("Verify player turn resources match expected")]
        VerifyTurnResources,
        
        [Description("Verify player game total resources match expected")]
        VerifyGameResources,
        
        [Description("Wait for specified time (for UI updates)")]
        Wait
    }
}
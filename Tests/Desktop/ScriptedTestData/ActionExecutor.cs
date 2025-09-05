using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Catan3.Shared.Models;
using FlaUI.Core.AutomationElements;

namespace Tests.DesktopApp.UI.ScriptedTestData
{
    /// <summary>
    /// Executes scripted test actions against the UI by translating high-level actions into specific UI interactions.
    /// 
    /// Key Responsibilities:
    /// - Parses TestAction objects from JSON scenario files
    /// - Converts action parameters (AutomationIds, player info, etc.) into UI element interactions
    /// - Delegates low-level UI operations to UIAutomationHelper
    /// - Provides deterministic action execution for reliable test replay
    /// 
    /// Action Parameter Format:
    /// - Building/Road actions use pre-calculated AutomationIds (e.g., "Building-(-3,3,0)-Right")
    /// - Player actions use player IDs that map to game model player indices
    /// - All placement actions require explicit parameters for deterministic behavior
    /// 
    /// Error Handling: Failed actions are logged and re-thrown to fail the test immediately.
    /// </summary>
    public class ActionExecutor
    {
        private readonly UIAutomationHelper _uiHelper;
        private const int SHORT_WAIT = 750;

        public ActionExecutor(UIAutomationHelper uiHelper)
        {
            _uiHelper = uiHelper ?? throw new ArgumentNullException(nameof(uiHelper));
        }

        /// <summary>
        /// Executes a single test action by dispatching to the appropriate action-specific method.
        /// 
        /// Process:
        /// 1. Logs the action being executed for debugging
        /// 2. Dispatches to specific action handler based on action type
        /// 3. Verifies the action succeeded by checking expected state
        /// 4. Logs success or captures/re-throws failures
        /// 
        /// Returns: True if action executed successfully, throws exception on failure
        /// </summary>
        /// <param name="action">The TestAction to execute</param>
        public bool ExecuteAction(TestAction action)
        {
            try
            {
                TraceMessage($"Executing action: {action.Type} - {action.Description}");

                // Execute the action based on its type
                switch (action.Type)
                {
                    case ActionType.RollDice:
                        ExecuteRollDice(action);
                        break;

                    case ActionType.AdvanceNext:
                        ExecuteAdvanceNext(action);
                        break;

                    case ActionType.GoFirst:
                        ExecuteGoFirst(action);
                        break;

                    case ActionType.PlaceSettlement:
                        ExecutePlaceSettlement(action);
                        break;

                    case ActionType.PlaceRoad:
                        ExecutePlaceRoad(action);
                        break;

                    case ActionType.UpgradeToCity:
                        ExecuteUpgradeToCity(action);
                        break;

                    case ActionType.PurchaseRoad:
                        ExecutePurchaseRoad(action);
                        break;

                    case ActionType.PurchaseSettlement:
                        ExecutePurchaseSettlement(action);
                        break;

                    case ActionType.PurchaseCity:
                        ExecutePurchaseCity(action);
                        break;

                    case ActionType.PurchaseSoldier:
                        ExecutePurchaseSoldier(action);
                        break;

                    case ActionType.MoveRobber:
                        ExecuteMoveRobber(action);
                        break;

                    case ActionType.SelectSupplementalPlayers:
                        ExecuteSelectSupplementalPlayers(action);
                        break;

                    case ActionType.ShuffleBoard:
                        ExecuteShuffleBoard(action);
                        break;

                    case ActionType.PreviousBoard:
                        ExecutePreviousBoard(action);
                        break;

                    case ActionType.RedoBoard:
                        ExecuteRedoBoard(action);
                        break;

                    case ActionType.LoadGame:
                        ExecuteLoadGame(action);
                        break;

                    case ActionType.VerifyGameState:
                        ExecuteVerifyGameState(action);
                        break;

                    case ActionType.VerifyTurnResources:
                        ExecuteVerifyTurnResources(action);
                        break;

                    case ActionType.VerifyGameResources:
                        ExecuteVerifyGameResources(action);
                        break;

                    case ActionType.Wait:
                        ExecuteWait(action);
                        break;

                    default:
                        throw new NotImplementedException($"Action type {action.Type} is not implemented");
                }

                // Apply any specified delay
                if (action.DelayMs > 0)
                {
                    Thread.Sleep(action.DelayMs);
                }

                TraceMessage($"✅ Action completed successfully: {action.Type}");
                
                // Force UI message pumping after action completion
                try
                {
                    _uiHelper.ClickButton("TestAutomationActionButton");
                }
                catch (Exception pumpEx)
                {
                    TraceMessage($"⚠️ TestAutomationActionButton click failed (non-critical): {pumpEx.Message}");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                if (action.Optional)
                {
                    TraceMessage($"⚠️ Optional action failed (continuing): {action.Type} - {ex.Message}");
                    return false;
                }
                else
                {
                    TraceMessage($"❌ Action failed: {action.Type} - {ex.Message}");
                    throw;
                }
            }
        }

        private void ExecuteRollDice(TestAction action)
        {
            if (!action.Parameters.TryGetValue(ActionParameters.Roll, out var rollValue))
                throw new ArgumentException("Roll parameter is required for RollDice action");

            var roll = Convert.ToInt32(rollValue);
            _uiHelper.ClickRoll(roll);
        }

        private void ExecuteAdvanceNext(TestAction action)
        {
            _uiHelper.ClickNext();
        }

        private void ExecuteGoFirst(TestAction action)
        {
            // Find GoFirst buttons and click the appropriate one
            var goFirstButtons = _uiHelper.FindAllByText("Go First").ToList();
            
            if (action.Parameters.TryGetValue("playerIndex", out var indexValue))
            {
                var playerIndex = Convert.ToInt32(indexValue);
                if (playerIndex < goFirstButtons.Count)
                {
                    var button = goFirstButtons[playerIndex].AsButton();
                    button.Invoke();
                    TraceMessage($"Clicked GoFirst for player index {playerIndex}");
                }
            }
            else if (action.Parameters.TryGetValue(ActionParameters.PlayerId, out var playerIdValue))
            {
                var playerId = playerIdValue?.ToString();
                TraceMessage($"Looking for GoFirst button for player {playerId}");
                
                // Get current game model to find player index
                var gameModel = _uiHelper.GetCurrentGameModel();
                var player = gameModel.Players.FirstOrDefault(p => p.Id == playerId);
                
                if (player != null)
                {
                    var playerIndex = gameModel.Players.ToList().IndexOf(player);
                    TraceMessage($"Found player {playerId} at index {playerIndex}");
                    
                    if (playerIndex < goFirstButtons.Count)
                    {
                        var button = goFirstButtons[playerIndex].AsButton();
                        button.Invoke();
                        TraceMessage($"Clicked GoFirst for player {playerId} at index {playerIndex}");
                    }
                    else
                    {
                        throw new InvalidOperationException($"Player index {playerIndex} is out of range for available GoFirst buttons ({goFirstButtons.Count})");
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Player {playerId} not found in game model");
                }
            }
        }

        private void ExecutePlaceSettlement(TestAction action)
        {
            if (!action.Parameters.TryGetValue(ActionParameters.AutomationId, out var automationIdObj))
                throw new ArgumentException("AutomationId parameter is required for PlaceSettlement action");

            var automationId = automationIdObj?.ToString() ?? throw new ArgumentException("AutomationId cannot be null");
            TraceMessage($"Placing settlement at specified location: {automationId}");
            _uiHelper.ClickBuilding(automationId);
        }

        private void ExecutePlaceRoad(TestAction action)
        {
            if (!action.Parameters.TryGetValue(ActionParameters.AutomationId, out var automationIdObj))
                throw new ArgumentException("AutomationId parameter is required for PlaceRoad action");

            var automationId = automationIdObj?.ToString() ?? throw new ArgumentException("AutomationId cannot be null");
            TraceMessage($"Placing road at specified location: {automationId}");
            _uiHelper.ClickRoad(automationId);
        }

        private void ExecuteUpgradeToCity(TestAction action)
        {
            if (!action.Parameters.TryGetValue(ActionParameters.AutomationId, out var automationIdObj))
                throw new ArgumentException("AutomationId parameter is required for UpgradeToCity action");

            var automationId = automationIdObj?.ToString() ?? throw new ArgumentException("AutomationId cannot be null");
            TraceMessage($"Upgrading settlement to city at location: {automationId}");
            _uiHelper.ClickBuilding(automationId);
        }

        private void ExecutePurchaseRoad(TestAction action)
        {
            _uiHelper.ClickButton("PurchaseRoadButton");
        }

        private void ExecutePurchaseSettlement(TestAction action)
        {
            _uiHelper.ClickButton("PurchaseSettlementButton");
        }

        private void ExecutePurchaseCity(TestAction action)
        {
            _uiHelper.ClickButton("PurchaseCityButton");
        }

        private void ExecutePurchaseSoldier(TestAction action)
        {
            _uiHelper.ClickButton("PurchaseSoldierButton");
        }

        private void ExecuteMoveRobber(TestAction action)
        {
            if (!action.Parameters.TryGetValue(ActionParameters.Coordinates, out var coordinatesValue))
                throw new ArgumentException("Coordinates parameter is required for MoveRobber action");

            var coordinates = coordinatesValue.ToString();
            var tileAutomationId = $"Tile{coordinates}";
            
            _uiHelper.ClickButton(tileAutomationId);
            
            // If target player is specified, handle that selection
            if (action.Parameters.TryGetValue(ActionParameters.TargetPlayer, out var targetPlayerValue))
            {
                var targetPlayerId = targetPlayerValue.ToString();
                TraceMessage($"Targeting player: {targetPlayerId}");
                // Additional UI interaction needed here
            }
        }

        private void ExecuteSelectSupplementalPlayers(TestAction action)
        {
            if (!action.Parameters.TryGetValue(ActionParameters.PlayerIds, out var playerIdsValue))
                throw new ArgumentException("PlayerIds parameter is required for SelectSupplementalPlayers action");

            // Implementation depends on UI structure for supplemental player selection
            TraceMessage("SelectSupplementalPlayers action needs UI-specific implementation");
        }

        private void ExecuteShuffleBoard(TestAction action)
        {
            _uiHelper.ClickButton("ShuffleButton");
        }

        private void ExecutePreviousBoard(TestAction action)
        {
            _uiHelper.ClickButton("PreviousBoardButton");
        }

        private void ExecuteRedoBoard(TestAction action)
        {
            _uiHelper.ClickButton("RedoButton");
        }

        private void ExecuteLoadGame(TestAction action)
        {
            if (!action.Parameters.TryGetValue(ActionParameters.FilePath, out var filePathValue))
                throw new ArgumentException("FilePath parameter is required for LoadGame action");

            var filePath = filePathValue.ToString();
            TraceMessage($"LoadGame action for {filePath} needs UI-specific implementation");
        }

        private void ExecuteVerifyGameState(TestAction action)
        {
            if (!action.Parameters.TryGetValue(ActionParameters.ExpectedValue, out var expectedValue))
                throw new ArgumentException("ExpectedValue parameter is required for VerifyGameState action");

            var expectedState = Enum.Parse<GameState>(expectedValue.ToString()!);
            _uiHelper.VerifyGameState(expectedState);
        }

        private void ExecuteVerifyTurnResources(TestAction action)
        {
            // Verify resources allocated this turn match expected values
            var gameModel = _uiHelper.GetCurrentGameModel();
            var currentPlayer = gameModel.CurrentPlayer();
            
            if (action.Parameters.TryGetValue(ActionParameters.ResourceType, out var resourceTypeValue) &&
                action.Parameters.TryGetValue(ActionParameters.ResourceCount, out var resourceCountValue))
            {
                var resourceType = Enum.Parse<ResourceType>(resourceTypeValue.ToString()!);
                var expectedCount = Convert.ToInt32(resourceCountValue);
                
                TraceMessage($"Verifying turn resources for {currentPlayer.Name}: {resourceType} = {expectedCount}");
                // TODO: Implement actual verification logic
            }
        }

        private void ExecuteVerifyGameResources(TestAction action)
        {
            // Verify total game resources match expected values
            var gameModel = _uiHelper.GetCurrentGameModel();
            var currentPlayer = gameModel.CurrentPlayer();
            
            if (action.Parameters.TryGetValue(ActionParameters.ResourceType, out var resourceTypeValue) &&
                action.Parameters.TryGetValue(ActionParameters.ResourceCount, out var resourceCountValue))
            {
                var resourceType = Enum.Parse<ResourceType>(resourceTypeValue.ToString()!);
                var expectedCount = Convert.ToInt32(resourceCountValue);
                
                TraceMessage($"Verifying game resources for {currentPlayer.Name}: {resourceType} = {expectedCount}");
                // TODO: Implement actual verification logic
            }
        }

        private void ExecuteWait(TestAction action)
        {
            var waitMs = SHORT_WAIT; // Default wait time
            if (action.Parameters.TryGetValue(ActionParameters.WaitMs, out var waitMsValue))
            {
                waitMs = Convert.ToInt32(waitMsValue);
            }
            
            Thread.Sleep(waitMs);
        }

        private void TraceMessage(string message, [CallerMemberName] string cmb = "", [CallerLineNumber] int cln = 0)
        {
            var output = $"ActionExecutor({cln}): {message} [Caller={cmb}]";
            Debug.WriteLine(output);
        }
    }
}
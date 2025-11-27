using System.Collections.Generic;
using System.Linq;
using Catan3.Shared.Models;

namespace Tests.DesktopApp.UI.ScriptedTestData
{

    /// <summary>
    /// Contains a queue of scripted actions for a specific player.
    /// Actions are executed in FIFO order when it's the player's turn.
    /// </summary>
    public class PlayerActionStack
    {
        /// <summary>
        /// The player ID this action stack belongs to
        /// </summary>
        public string PlayerId { get; set; } = string.Empty;

        /// <summary>
        /// Display name for the player (for logging/debugging)
        /// </summary>
        public string? PlayerName { get; set; }

        /// <summary>
        /// Queue of actions to execute for this player.
        /// Actions are dequeued and executed in order.
        /// </summary>
        public Queue<TestAction> Actions { get; set; } = new();

        /// <summary>
        /// List of actions that have been executed for this player.
        /// Used for debugging and validation.
        /// </summary>
        public List<TestAction> ExecutedActions { get; set; } = [];

        /// <summary>
        /// Whether this player has any remaining actions to execute
        /// </summary>
        public bool HasPendingActions => Actions.Count > 0;

        /// <summary>
        /// Total number of actions originally loaded for this player
        /// </summary>
        public int TotalActions => Actions.Count + ExecutedActions.Count;

        /// <summary>
        /// Number of actions completed by this player
        /// </summary>
        public int CompletedActions => ExecutedActions.Count;

        /// <summary>
        /// Dequeue the next action for this player to execute.
        /// Moves the action from Actions queue to ExecutedActions list.
        /// </summary>
        /// <returns>Next action to execute, or null if no actions remaining</returns>
        public TestAction? GetNextAction()
        {
            if (!Actions.TryDequeue(out var action))
                return null;

            ExecutedActions.Add(action);
            return action;
        }

        /// <summary>
        /// Peek at the next action without removing it from the queue
        /// </summary>
        /// <returns>Next action to execute, or null if no actions remaining</returns>
        public TestAction? PeekNextAction()
        {
            return Actions.TryPeek(out var action) ? action : null;
        }

        /// <summary>
        /// Add an action to the end of this player's action queue
        /// </summary>
        /// <param name="action">Action to add</param>
        public void EnqueueAction(TestAction action)
        {
            action.PlayerId = PlayerId; // Ensure action is associated with this player
            Actions.Enqueue(action);
        }

        /// <summary>
        /// Add multiple actions to the end of this player's action queue
        /// </summary>
        /// <param name="actions">Actions to add</param>
        public void EnqueueActions(IEnumerable<TestAction> actions)
        {
            foreach (var action in actions.OrderBy(a => a.Sequence))
            {
                EnqueueAction(action);
            }
        }

        /// <summary>
        /// Clear all pending actions for this player
        /// </summary>
        public void ClearPendingActions()
        {
            Actions.Clear();
        }

        /// <summary>
        /// Get summary of this player's action stack status
        /// </summary>
        /// <returns>Human-readable status string</returns>
        public string GetStatusSummary()
        {
            return $"Player {PlayerName ?? PlayerId}: {CompletedActions}/{TotalActions} actions completed, {Actions.Count} pending";
        }
    }
}
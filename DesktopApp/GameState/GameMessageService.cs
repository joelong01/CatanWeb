/*
 * GameMessageService.cs
 * 
 * OVERVIEW:
 * This class serves as the MVVM messaging layer bridge between the UI and the core game logic.
 * It registers for all MVVM messages from the UI and delegates the actual game operations to
 * the GameStateMachine through the IGameStateMachine interface.
 * 
 * PURPOSE AND ARCHITECTURE:
 * - Separation of Concerns: This class was created to isolate MVVM messaging concerns from
 *   core game logic. The GameStateMachine contains pure game logic while this class handles
 *   all UI framework dependencies and messaging patterns.
 * - Message Handling: Registers for all CommunityToolkit.Mvvm messages that represent user
 *   actions and game events from the UI layer.
 * - Delegation Pattern: Each message handler receives the MVVM message, calls the corresponding
 *   method on IGameStateMachine, and sends the result back to the UI via UpdateGameModel messages.
 * - Error Handling: Catches GameExceptions from the game logic and converts them to ErrorMessage
 *   objects that the UI can display to users.
 * 
 * KEY RESPONSIBILITIES:
 * 1. MVVM Message Registration: Subscribes to all game-related message types from the UI
 * 2. Message-to-Method Translation: Converts UI messages into GameStateMachine method calls
 * 3. Response Handling: Sends UpdateGameModel messages back to the UI with updated game state
 * 4. Error Propagation: Converts exceptions into user-friendly error messages for the UI
 * 5. Async Coordination: Handles async operations from GameStateMachine and coordinates responses
 * 
 * DESIGN PATTERNS:
 * - Mediator Pattern: Acts as mediator between UI messages and game logic
 * - Adapter Pattern: Adapts MVVM messages to IGameStateMachine interface calls
 * - Observer Pattern: Uses MVVM Messenger for decoupled communication with UI
 * - Dependency Injection: Takes IGameStateMachine as constructor dependency for loose coupling
 * 
 * MESSAGE FLOW:
 * UI Action → MVVM Message → GameMessageService Handler → IGameStateMachine Method →
 * GameModel Result → UpdateGameModel Message → UI Update
 * 
 * ERROR FLOW:
 * GameStateMachine Exception → GameMessageService Catch → ErrorMessage → UI Error Display
 */

using Catan3.Shared.Interfaces;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;


namespace Catan3.GameState
{
    /// <summary>
    /// MVVM messaging service that bridges UI messages to game logic operations.
    /// This class handles all MVVM communication between the UI layer and the GameStateMachine,
    /// providing a clean separation between presentation concerns and game logic.
    /// </summary>
    internal class GameMessageService : ObservableRecipient
    {
        /// <summary>
        /// The game state machine that contains the core game logic.
        /// All game operations are delegated to this instance.
        /// </summary>
        private readonly IGameStateMachine _gameStateMachine;

        /// <summary>
        /// Initializes a new GameMessageService with the specified game state machine.
        /// Automatically registers for all MVVM messages upon construction.
        /// </summary>
        /// <param name="gameStateMachine">The game state machine to delegate operations to.</param>
        public GameMessageService(IGameStateMachine gameStateMachine)
        {
            _gameStateMachine = gameStateMachine;
            RegisterMessages();
        }

        /// <summary>
        /// Registers this service to receive all game-related MVVM messages.
        /// Each message type is mapped to a specific handler method that delegates to the GameStateMachine.
        /// </summary>
        private void RegisterMessages()
        {
            Debug.Assert(Messenger is not null);
            IsActive = true;
            
            Messenger.Register<ExecuteGameActionMessage>(this, HandleExecuteGameActionAsync);
            Messenger.Register<ShuffleMessage>(this, HandleShuffleAsync);
            Messenger.Register<BuildingUpgradeMessage>(this, HandleBuildingUpgradeAsync);
            Messenger.Register<SetPlayerOrderMessage>(this, HandleSetPlayerOrderAsync);
            Messenger.Register<RoadPurchaseMessage>(this, HandleRoadPurchaseAsync);
            Messenger.Register<MoveRobberMessage>(this, HandleMoveRobberAsync);
            Messenger.Register<Catan3.Shared.Models.NewGameMessage>(this, HandleNewGameAsync);
            Messenger.Register<LoadGameMessage>(this, HandleLoadGameAsync);
            Messenger.Register<StartRecordingMessage>(this, HandleStartRecordingAsync);
            Messenger.Register<StopRecordingMessage>(this, HandleStopRecordingAsync);
            Messenger.Register<RollMessage>(this, HandleRollAsync);
            Messenger.Register<PurchaseMessage>(this, HandlePurchaseAsync);
            Messenger.Register<ParticipatingInSupplementalMessage>(this, HandleParticipatingInSupplementalAsync);
            Messenger.Register<BalanceBoardMessage>(this, HandleBalanceBoardAsync);
            Messenger.Register<EndGame>(this, HandleEndGameAsync);
            Messenger.Register<GoFirstMessage>(this, HandleGoFirstAsync);
            Messenger.Register<PersistGameMessage>(this, HandlePersistGameAsync);
        }

        /// <summary>
        /// Handles ExecuteGameActionMessage from the UI to perform game actions (Next, Undo, Redo).
        /// Delegates to GameStateMachine and sends the result back to the UI.
        /// </summary>
        /// <param name="recipient">The message recipient (this service).</param>
        /// <param name="message">The game action message from the UI.</param>
        private async void HandleExecuteGameActionAsync(object recipient, ExecuteGameActionMessage message)
        {
            try
            {
                var gameModel = await _gameStateMachine.ExecuteGameActionAsync(message);
                Messenger.Send(new UpdateGameModel(gameModel));
            }
            catch (GameException e)
            {
                SendErrorMessage(e.Message, e.ErrorLevel);
            }
        }

        /// <summary>
        /// Handles ShuffleMessage from the UI to shuffle game content.
        /// Delegates to GameStateMachine and sends the result back to the UI.
        /// </summary>
        /// <param name="recipient">The message recipient (this service).</param>
        /// <param name="message">The shuffle message from the UI.</param>
        private async void HandleShuffleAsync(object recipient, ShuffleMessage message)
        {
            try
            {
                var gameModel = await _gameStateMachine.HandleShuffleAsync(message);
                Messenger.Send(new UpdateGameModel(gameModel));
            }
            catch (GameException e)
            {
                SendErrorMessage(e.Message, e.ErrorLevel);
            }
        }

        /// <summary>
        /// Handles BuildingUpgradeMessage from the UI to upgrade buildings (settlement to city).
        /// Delegates to GameStateMachine and sends the result back to the UI.
        /// </summary>
        /// <param name="recipient">The message recipient (this service).</param>
        /// <param name="message">The building upgrade message from the UI.</param>
        private async void HandleBuildingUpgradeAsync(object recipient, BuildingUpgradeMessage message)
        {
            try
            {
                var gameModel = await _gameStateMachine.HandleBuildingUpgradeAsync(message);
                Messenger.Send(new UpdateGameModel(gameModel));
            }
            catch (GameException e)
            {
                SendErrorMessage(e.Message, e.ErrorLevel);
            }
        }

        /// <summary>
        /// Handles SetPlayerOrderMessage from the UI to reorder players.
        /// Delegates to GameStateMachine and sends the result back to the UI.
        /// </summary>
        /// <param name="recipient">The message recipient (this service).</param>
        /// <param name="message">The player order message from the UI.</param>
        private async void HandleSetPlayerOrderAsync(object recipient, SetPlayerOrderMessage message)
        {
            try
            {
                var gameModel = await _gameStateMachine.HandleSetPlayerOrderAsync(message);
                Messenger.Send(new UpdateGameModel(gameModel));
            }
            catch (GameException e)
            {
                SendErrorMessage(e.Message, e.ErrorLevel);
            }
        }

        /// <summary>
        /// Handles RoadPurchaseMessage from the UI to place roads.
        /// Delegates to GameStateMachine and sends the result back to the UI.
        /// </summary>
        /// <param name="recipient">The message recipient (this service).</param>
        /// <param name="message">The road purchase message from the UI.</param>
        private async void HandleRoadPurchaseAsync(object recipient, RoadPurchaseMessage message)
        {
            try
            {
                var gameModel = await _gameStateMachine.HandleRoadPurchaseAsync(message);
                Messenger.Send(new UpdateGameModel(gameModel));
            }
            catch (GameException e)
            {
                SendErrorMessage(e.Message, e.ErrorLevel);
            }
        }

        /// <summary>
        /// Handles MoveRobberMessage from the UI to move the robber.
        /// Delegates to GameStateMachine and sends the result back to the UI.
        /// </summary>
        /// <param name="recipient">The message recipient (this service).</param>
        /// <param name="message">The robber movement message from the UI.</param>
        private async void HandleMoveRobberAsync(object recipient, MoveRobberMessage message)
        {
            try
            {
                var gameModel = await _gameStateMachine.HandleMoveRobberAsync(message);
                Messenger.Send(new UpdateGameModel(gameModel));
            }
            catch (GameException e)
            {
                SendErrorMessage(e.Message, e.ErrorLevel);
            }
        }

        /// <summary>
        /// Handles NewGameMessage from the UI to create a new game.
        /// Delegates to GameStateMachine and sends the result back to the UI.
        /// </summary>
        /// <param name="recipient">The message recipient (this service).</param>
        /// <param name="message">The new game message from the UI.</param>
        private async void HandleNewGameAsync(object recipient, Catan3.Shared.Models.NewGameMessage message)
        {
            try
            {
                var gameModel = await _gameStateMachine.HandleNewGameAsync(message);
                Messenger.Send(new UpdateGameModel(gameModel));
            }
            catch (GameException e)
            {
                SendErrorMessage(e.Message, e.ErrorLevel);
            }
        }

        /// <summary>
        /// Handles LoadGameMessage from the UI to load a saved game.
        /// Delegates to GameStateMachine and sends the result back to the UI.
        /// </summary>
        /// <param name="recipient">The message recipient (this service).</param>
        /// <param name="message">The load game message from the UI.</param>
        private async void HandleLoadGameAsync(object recipient, LoadGameMessage message)
        {
            try
            {
                var gameModel = await _gameStateMachine.HandleLoadGameAsync(message);
                Messenger.Send(new UpdateGameModel(gameModel));
            }
            catch (GameException e)
            {
                SendErrorMessage(e.Message, e.ErrorLevel);
            }
        }

        /// <summary>
        /// Handles StartRecordingMessage from the UI to begin recording game actions.
        /// Delegates to GameStateMachine for recording setup.
        /// </summary>
        /// <param name="recipient">The message recipient (this service).</param>
        /// <param name="message">The start recording message from the UI.</param>
        private async void HandleStartRecordingAsync(object recipient, StartRecordingMessage message)
        {
            try
            {
                await _gameStateMachine.HandleStartRecordingAsync(message);
            }
            catch (GameException e)
            {
                SendErrorMessage(e.Message, e.ErrorLevel);
            }
        }

        /// <summary>
        /// Handles StopRecordingMessage from the UI to stop recording game actions.
        /// Delegates to GameStateMachine to finalize recording.
        /// </summary>
        /// <param name="recipient">The message recipient (this service).</param>
        /// <param name="message">The stop recording message from the UI.</param>
        private async void HandleStopRecordingAsync(object recipient, StopRecordingMessage message)
        {
            try
            {
                await _gameStateMachine.HandleStopRecordingAsync(message);
            }
            catch (GameException e)
            {
                SendErrorMessage(e.Message, e.ErrorLevel);
            }
        }

        /// <summary>
        /// Handles RollMessage from the UI to process dice rolls.
        /// Delegates to GameStateMachine and sends the result back to the UI.
        /// </summary>
        /// <param name="recipient">The message recipient (this service).</param>
        /// <param name="message">The dice roll message from the UI.</param>
        private async void HandleRollAsync(object recipient, RollMessage message)
        {
            try
            {
                var gameModel = await _gameStateMachine.HandleRollAsync(message);
                Messenger.Send(new UpdateGameModel(gameModel));
            }
            catch (GameException e)
            {
                SendErrorMessage(e.Message, e.ErrorLevel);
            }
        }

        /// <summary>
        /// Handles PurchaseMessage from the UI to buy entitlements (roads, settlements, cities, soldiers).
        /// Delegates to GameStateMachine and sends the result back to the UI.
        /// </summary>
        /// <param name="recipient">The message recipient (this service).</param>
        /// <param name="message">The purchase message from the UI.</param>
        private async void HandlePurchaseAsync(object recipient, PurchaseMessage message)
        {
            try
            {
                var gameModel = await _gameStateMachine.HandlePurchaseAsync(message);
                Messenger.Send(new UpdateGameModel(gameModel));
            }
            catch (GameException e)
            {
                SendErrorMessage(e.Message, e.ErrorLevel);
            }
        }

        /// <summary>
        /// Handles ParticipatingInSupplementalMessage from the UI to set player participation in supplemental rounds.
        /// Delegates to GameStateMachine and sends the result back to the UI.
        /// </summary>
        /// <param name="recipient">The message recipient (this service).</param>
        /// <param name="message">The supplemental participation message from the UI.</param>
        private async void HandleParticipatingInSupplementalAsync(object recipient, ParticipatingInSupplementalMessage message)
        {
            try
            {
                var gameModel = await _gameStateMachine.HandleParticipatingInSupplementalAsync(message);
                Messenger.Send(new UpdateGameModel(gameModel));
            }
            catch (GameException e)
            {
                SendErrorMessage(e.Message, e.ErrorLevel);
            }
        }

        /// <summary>
        /// Handles BalanceBoardMessage from the UI to balance board resources.
        /// Delegates to GameStateMachine and sends the result back to the UI.
        /// </summary>
        /// <param name="recipient">The message recipient (this service).</param>
        /// <param name="message">The balance board message from the UI.</param>
        private async void HandleBalanceBoardAsync(object recipient, BalanceBoardMessage message)
        {
            try
            {
                var gameModel = await _gameStateMachine.HandleBalanceBoardAsync(message);
                Messenger.Send(new UpdateGameModel(gameModel));
            }
            catch (GameException e)
            {
                SendErrorMessage(e.Message, e.ErrorLevel);
            }
        }

        /// <summary>
        /// Handles EndGame message from the UI to end the current game.
        /// Delegates to GameStateMachine for cleanup operations.
        /// </summary>
        /// <param name="recipient">The message recipient (this service).</param>
        /// <param name="message">The end game message from the UI.</param>
        private async void HandleEndGameAsync(object recipient, EndGame message)
        {
            try
            {
                await _gameStateMachine.HandleEndGameAsync(message);
            }
            catch (GameException e)
            {
                SendErrorMessage(e.Message, e.ErrorLevel);
            }
        }

        /// <summary>
        /// Handles GoFirstMessage from the UI to set which player goes first.
        /// Delegates to GameStateMachine and sends the result back to the UI.
        /// </summary>
        /// <param name="recipient">The message recipient (this service).</param>
        /// <param name="message">The go first message from the UI.</param>
        private async void HandleGoFirstAsync(object recipient, GoFirstMessage message)
        {
            try
            {
                var gameModel = await _gameStateMachine.HandleGoFirstAsync(message);
                Messenger.Send(new UpdateGameModel(gameModel));
            }
            catch (GameException e)
            {
                SendErrorMessage(e.Message, e.ErrorLevel);
            }
        }

        /// <summary>
        /// Handles PersistGameMessage from the UI to save or load game state.
        /// Delegates to GameStateMachine for persistence operations.
        /// </summary>
        /// <param name="recipient">The message recipient (this service).</param>
        /// <param name="message">The persistence message from the UI.</param>
        private async void HandlePersistGameAsync(object recipient, PersistGameMessage message)
        {
            try
            {
                await _gameStateMachine.HandlePersistGameAsync(message);
            }
            catch (GameException e)
            {
                SendErrorMessage(e.Message, e.ErrorLevel);
            }
        }

        /// <summary>
        /// Sends an error message to the UI when game operations fail.
        /// Logs the error for debugging and sends an ErrorMessage to notify the user.
        /// </summary>
        /// <param name="message">The error message to display.</param>
        /// <param name="errorLevel">The severity level of the error.</param>
        /// <param name="indentLevel">The indentation level for logging.</param>
        /// <param name="cmb">The calling member name (auto-filled).</param>
        /// <param name="cln">The calling line number (auto-filled).</param>
        /// <param name="cfp">The calling file path (auto-filled).</param>
        private void SendErrorMessage(string message, ErrorLevel errorLevel, int indentLevel = 0, [CallerMemberName] string cmb = "", [CallerLineNumber] int cln = 0, [CallerFilePath] string cfp = "")
        {
            this.TraceMessage(errorLevel.ToString() + ": " + message, indentLevel, cmb, cln, cfp);
            Messenger.Send(new ErrorMessage(message, errorLevel, cmb, cln, cfp));
        }
    }
}

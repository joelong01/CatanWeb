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

using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Catan3.Shared.Extensions;
using Catan3.Models;
using Catan3.Shared.GameLogic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;


namespace Catan3.GameState
{
    /// <summary>
    /// MVVM messaging service that bridges UI messages to game logic operations.
    /// This class handles all MVVM communication and manages the GameStateMachine lifecycle internally.
    /// Receives MVVM messages and creates/manages GameStateMachine instances as needed.
    /// Service handlers are implemented in GameMessageServiceProxy.cs as a partial class.
    /// </summary>
    internal partial class GameMessageService : ObservableRecipient
    {
        /// <summary>
        /// The current game state machine that contains the core game logic.
        /// Created internally when game messages are received.
        /// </summary>
        private GameStateMachine? _gameStateMachine;

        /// <summary>
        /// The GameService proxy used when ServiceGame setting is enabled.
        /// Delegates game operations to remote GameService instead of local GameStateMachine.
        /// </summary>
        private Catan3.Shared.Services.GameServiceProxy? _gameServiceProxy;

        /// <summary>
        /// The persistence service used for game operations.
        /// </summary>
        private readonly Catan3.Shared.Interfaces.IPersistenceService _persistenceService;

        /// <summary>
        /// Current application settings received from UpdateSettings messages.
        /// </summary>
        private SettingsModel? _currentSettings;

        /// <summary>
        /// Tracks whether game message handlers are currently registered to prevent duplicate registration.
        /// </summary>
        private bool _gameHandlersRegistered = false;

        /// <summary>
        /// Initializes a new GameMessageService that handles MVVM messages.
        /// Creates GameStateMachine instances internally based on received messages.
        /// </summary>
        /// <param name="persistenceService">The persistence service for file operations.</param>
        public GameMessageService(Catan3.Shared.Interfaces.IPersistenceService persistenceService)
        {
            _persistenceService = persistenceService;
            RegisterMessages();
        }

        /// <summary>
        /// Registers this service to receive all game-related MVVM messages.
        /// Checks ServiceGame setting to register either local or service handlers.
        /// </summary>
        private void RegisterMessages()
        {
            Debug.Assert(Messenger is not null);
            IsActive = true;
            
            // Always register settings handler first
            Messenger.Register<UpdateSettings>(this, HandleUpdateSettingsAsync);
            
            // Initialize settings - handler will load if null
            HandleUpdateSettingsAsync(this, null);
            
            // Now initialize handlers based on current settings
            InitializeHandlersBasedOnSettings();
        }

        /// <summary>
        /// Initializes message handlers based on current settings
        /// </summary>
        private async void InitializeHandlersBasedOnSettings()
        {
            // Unregister existing handlers if any
            if (_gameHandlersRegistered)
            {
                UnregisterGameMessages();
            }

            // Get current settings via messaging
            var currentSettings = await SettingsModel.GetAsync();
            _currentSettings = currentSettings;

            // Check ServiceGame setting and register appropriate handlers
            bool useServiceGame = currentSettings.GetBoolValue("ServiceGame");
            
            if (useServiceGame)
            {
                RegisterServiceGameMessages();
            }
            else
            {
                RegisterLocalGameMessages();
            }

            _gameHandlersRegistered = true;
        }
        
        /// <summary>
        /// Registers message handlers for local GameStateMachine operations.
        /// Each handler delegates to the local GameStateMachine instance.
        /// </summary>
        private void RegisterLocalGameMessages()
        {
            Messenger.Register<UndoMessage>(this, HandleUndoAsync);
            Messenger.Register<RedoMessage>(this, HandleRedoAsync);
            Messenger.Register<NextMessage>(this, HandleNextAsync);
            Messenger.Register<ShuffleMessage>(this, HandleShuffleAsync);
            Messenger.Register<BuildingUpgradeMessage>(this, HandleBuildingUpgradeAsync);
            Messenger.Register<SetPlayerOrderMessage>(this, HandleSetPlayerOrderAsync);
            Messenger.Register<RoadPurchaseMessage>(this, HandleRoadPurchaseAsync);
            Messenger.Register<MoveRobberMessage>(this, HandleMoveRobberAsync);
            Messenger.Register<NewGameMessage>(this, HandleNewGameAsync);
            Messenger.Register<Catan3.Models.LoadLocalCatanGameMessage>(this, HandleLoadLocalCatanGameAsync);
            Messenger.Register<StartRecordingMessage>(this, HandleStartRecordingAsync);
            Messenger.Register<StopRecordingMessage>(this, HandleStopRecordingAsync);
            Messenger.Register<RollMessage>(this, HandleRollAsync);
            Messenger.Register<PurchaseMessage>(this, HandlePurchaseAsync);
            Messenger.Register<ParticipatingInSupplementalMessage>(this, HandleParticipatingInSupplementalAsync);
            Messenger.Register<BalanceBoardMessage>(this, HandleBalanceBoardAsync);
            Messenger.Register<EndGame>(this, HandleEndGameAsync);
            Messenger.Register<GoFirstMessage>(this, HandleGoFirstAsync);
            Messenger.Register<PersistGameMessage>(this, HandlePersistGameAsync);
            Messenger.Register<SaveAsRequestMessage>(this, HandleSaveAsRequestAsync);
        }
        
        /// <summary>
        /// Registers message handlers for GameService proxy operations.
        /// Each handler delegates to the GameServiceProxy instance.
        /// </summary>
        private void RegisterServiceGameMessages()
        {
            // Initialize the GameServiceProxy when service handlers are registered
            InitializeGameServiceProxy();
            Messenger.Register<UndoMessage>(this, HandleUndoServiceAsync);
            Messenger.Register<RedoMessage>(this, HandleRedoServiceAsync);
            Messenger.Register<NextMessage>(this, HandleNextServiceAsync);
            Messenger.Register<ShuffleMessage>(this, HandleShuffleServiceAsync);
            Messenger.Register<BuildingUpgradeMessage>(this, HandleBuildingUpgradeServiceAsync);
            Messenger.Register<SetPlayerOrderMessage>(this, HandleSetPlayerOrderServiceAsync);
            Messenger.Register<RoadPurchaseMessage>(this, HandleRoadPurchaseServiceAsync);
            Messenger.Register<MoveRobberMessage>(this, HandleMoveRobberServiceAsync);
            Messenger.Register<NewGameMessage>(this, HandleNewGameServiceAsync);
            Messenger.Register<Catan3.Models.LoadLocalCatanGameMessage>(this, HandleLoadLocalCatanGameServiceAsync);
            Messenger.Register<StartRecordingMessage>(this, HandleStartRecordingAsync);
            Messenger.Register<StopRecordingMessage>(this, HandleStopRecordingAsync);
            Messenger.Register<RollMessage>(this, HandleRollServiceAsync);
            Messenger.Register<PurchaseMessage>(this, HandlePurchaseServiceAsync);
            Messenger.Register<ParticipatingInSupplementalMessage>(this, HandleParticipatingInSupplementalServiceAsync);
            Messenger.Register<BalanceBoardMessage>(this, HandleBalanceBoardServiceAsync);
            Messenger.Register<EndGame>(this, HandleEndGameServiceAsync);
            Messenger.Register<GoFirstMessage>(this, HandleGoFirstServiceAsync);
            Messenger.Register<PersistGameMessage>(this, HandlePersistGameServiceAsync);
            Messenger.Register<SaveAsRequestMessage>(this, HandleSaveAsRequestServiceAsync);
        }

        /// <summary>
        /// Unregisters all game message handlers to prevent duplicate registration.
        /// </summary>
        private void UnregisterGameMessages()
        {
            Messenger.Unregister<UndoMessage>(this);
            Messenger.Unregister<RedoMessage>(this);
            Messenger.Unregister<NextMessage>(this);
            Messenger.Unregister<ShuffleMessage>(this);
            Messenger.Unregister<BuildingUpgradeMessage>(this);
            Messenger.Unregister<SetPlayerOrderMessage>(this);
            Messenger.Unregister<RoadPurchaseMessage>(this);
            Messenger.Unregister<MoveRobberMessage>(this);
            Messenger.Unregister<NewGameMessage>(this);
            Messenger.Unregister<Catan3.Models.LoadLocalCatanGameMessage>(this);
            Messenger.Unregister<StartRecordingMessage>(this);
            Messenger.Unregister<StopRecordingMessage>(this);
            Messenger.Unregister<RollMessage>(this);
            Messenger.Unregister<PurchaseMessage>(this);
            Messenger.Unregister<ParticipatingInSupplementalMessage>(this);
            Messenger.Unregister<BalanceBoardMessage>(this);
            Messenger.Unregister<EndGame>(this);
            Messenger.Unregister<GoFirstMessage>(this);
            Messenger.Unregister<PersistGameMessage>(this);
            Messenger.Unregister<SaveAsRequestMessage>(this);
        }

        /// <summary>
        /// Handles UndoMessage from the UI to revert the last action.
        /// Delegates to GameStateMachine and sends the result back to the UI.
        /// </summary>
        /// <param name="recipient">The message recipient (this service).</param>
        /// <param name="message">The undo message from the UI.</param>
        private async void HandleUndoAsync(object recipient, UndoMessage message)
        {
            if (_gameStateMachine == null)
            {
                SendErrorMessage("No active game. Please start a new game or load an existing one.", ErrorLevel.Critical);
                return;
            }

            try
            {
                var gameModel = await _gameStateMachine.HandleUndoAsync(message);
                Messenger.Send(new UpdateGameModel(gameModel));
            }
            catch (GameException e)
            {
                SendErrorMessage(e.Message, e.ErrorLevel);
            }
        }

        /// <summary>
        /// Handles RedoMessage from the UI to reapply a previously undone action.
        /// Delegates to GameStateMachine and sends the result back to the UI.
        /// </summary>
        /// <param name="recipient">The message recipient (this service).</param>
        /// <param name="message">The redo message from the UI.</param>
        private async void HandleRedoAsync(object recipient, RedoMessage message)
        {
            if (_gameStateMachine == null)
            {
                SendErrorMessage("No active game. Please start a new game or load an existing one.", ErrorLevel.Critical);
                return;
            }

            try
            {
                var gameModel = await _gameStateMachine.HandleRedoAsync(message);
                Messenger.Send(new UpdateGameModel(gameModel));
            }
            catch (GameException e)
            {
                SendErrorMessage(e.Message, e.ErrorLevel);
            }
        }

        /// <summary>
        /// Handles NextMessage from the UI to advance to the next game state.
        /// Delegates to GameStateMachine and sends the result back to the UI.
        /// </summary>
        /// <param name="recipient">The message recipient (this service).</param>
        /// <param name="message">The next message from the UI.</param>
        private async void HandleNextAsync(object recipient, NextMessage message)
        {
            if (_gameStateMachine == null)
            {
                SendErrorMessage("No active game. Please start a new game or load an existing one.", ErrorLevel.Critical);
                return;
            }

            try
            {
                var gameModel = await _gameStateMachine.HandleNextAsync(message);
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
            if (_gameStateMachine == null)
            {
                SendErrorMessage("No active game. Please start a new game or load an existing one.", ErrorLevel.Critical);
                return;
            }

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
            if (_gameStateMachine == null)
            {
                SendErrorMessage("No active game. Please start a new game or load an existing one.", ErrorLevel.Critical);
                return;
            }

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
            if (_gameStateMachine == null)
            {
                SendErrorMessage("No active game. Please start a new game or load an existing one.", ErrorLevel.Critical);
                return;
            }

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
            if (_gameStateMachine == null)
            {
                SendErrorMessage("No active game. Please start a new game or load an existing one.", ErrorLevel.Critical);
                return;
            }

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
            if (_gameStateMachine == null)
            {
                SendErrorMessage("No active game. Please start a new game or load an existing one.", ErrorLevel.Critical);
                return;
            }

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
        /// Creates a fresh GameStateMachine with a new game and sends the result back to the UI.
        /// </summary>
        /// <param name="recipient">The message recipient (this service).</param>
        /// <param name="message">The new game message from the UI.</param>
        private async void HandleNewGameAsync(object recipient, Catan3.Shared.Models.NewGameMessage message)
        {
            try
            {
                // Get the appropriate game metadata based on game type
                IGameMetadata gameInfo = message.GameType == GameType.Regular 
                    ? RegularBoardInfo.Default 
                    : ExpansionBoardInfo.Default;
                
                // Use the helper function to create the new game
                var gameModel = await CreateNewGameAsync(gameInfo, message.PlayerIds, message.GameName ?? "Untitled Game");
                
                // Send the current game state to the UI
                Messenger.Send(new UpdateGameModel(gameModel));
            }
            catch (GameException e)
            {
                SendErrorMessage(e.Message, e.ErrorLevel);
            }
        }


        /// <summary>
        /// Handles LoadLocalCatanGameMessage from the UI to load a saved game.
        /// Creates a fresh GameStateMachine with the loaded game data and sends the result back to the UI.
        /// Supports both .catan (compressed) and .catan_test (uncompressed) file formats.
        /// </summary>
        /// <param name="recipient">The message recipient (this service).</param>
        /// <param name="message">The load game message from the UI containing file path.</param>
        private async void HandleLoadLocalCatanGameAsync(object recipient, Catan3.Models.LoadLocalCatanGameMessage message)
        {
            try
            {
                var filePath = message.LocalFile;
                if (!System.IO.File.Exists(filePath))
                {
                    throw new GameException($"File not found: {filePath}");
                }

                var extension = System.IO.Path.GetExtension(filePath);
                GameModel gameModel;

                switch (extension.ToLowerInvariant())
                {
                    case ".catan":
                        // Use helper function for compressed games
                        gameModel = await LoadCompressedGameAsync(filePath);
                        break;
                        
                    case ".catan_test":
                        // Use helper function for test scenarios
                        gameModel = await LoadTestScenarioAsync(filePath);
                        break;
                        
                    default:
                        throw new GameException($"Unsupported file extension: {extension}. Only .catan and .catan_test files are supported.");
                }
                
                // Send the loaded game state to the UI
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
            if (_gameStateMachine == null)
            {
                SendErrorMessage("No active game. Please start a new game or load an existing one.", ErrorLevel.Critical);
                return;
            }

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
            if (_gameStateMachine == null)
            {
                SendErrorMessage("No active game. Please start a new game or load an existing one.", ErrorLevel.Critical);
                return;
            }

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
            if (_gameStateMachine == null)
            {
                SendErrorMessage("No active game. Please start a new game or load an existing one.", ErrorLevel.Critical);
                return;
            }

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
            if (_gameStateMachine == null)
            {
                SendErrorMessage("No active game. Please start a new game or load an existing one.", ErrorLevel.Critical);
                return;
            }

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
            if (_gameStateMachine == null)
            {
                SendErrorMessage("No active game. Please start a new game or load an existing one.", ErrorLevel.Critical);
                return;
            }

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
            if (_gameStateMachine == null)
            {
                SendErrorMessage("No active game. Please start a new game or load an existing one.", ErrorLevel.Critical);
                return;
            }

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
            if (_gameStateMachine == null)
            {
                SendErrorMessage("No active game. Please start a new game or load an existing one.", ErrorLevel.Critical);
                return;
            }

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
            if (_gameStateMachine == null)
            {
                SendErrorMessage("No active game. Please start a new game or load an existing one.", ErrorLevel.Critical);
                return;
            }

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
            if (_gameStateMachine == null)
            {
                SendErrorMessage("No active game. Please start a new game or load an existing one.", ErrorLevel.Critical);
                return;
            }

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
        /// Creates a GameStateMachine with Desktop-specific dependencies.
        /// </summary>
        /// <param name="localSaveFile">The local save file path.</param>
        /// <returns>A configured GameStateMachine using Desktop services.</returns>
        private GameStateMachine CreateGameStateMachineWithDesktopDependencies(string localSaveFile)
        {
            // Create shared implementations 
            var gameLogger = new Catan3.Services.DesktopGameLogger();
            var gameLog = new Catan3.Shared.Utility.Log<string>(_persistenceService, localSaveFile, gameLogger);

            // Create and return shared GameStateMachine with Desktop dependencies  
            return new GameStateMachine(gameLog, gameLogger, _persistenceService);
        }

        /// <summary>
        /// Creates a brand new game with the specified parameters.
        /// Creates a fresh GameStateMachine instance for the new game.
        /// </summary>
        /// <param name="gameInfo">The game metadata containing board configuration.</param>
        /// <param name="playerIds">The list of player IDs.</param>
        /// <param name="gameName">The name of the game.</param>
        /// <returns>The newly created GameModel.</returns>
        private async Task<GameModel> CreateNewGameAsync(IGameMetadata gameInfo, IList<string> playerIds, string gameName)
        {
            // Create the new game model
            var gameModel = GameModelExtensions.CreateNew(gameInfo, playerIds, gameName);
            
            // Generate save file path for the new game
            var saveFilePath = Catan.Services.FileService.GenerateNewGameSaveFilePath(gameModel);
            
            // Create the GameStateMachine with Desktop dependencies
            _gameStateMachine = CreateGameStateMachineWithDesktopDependencies(saveFilePath);
            
            // Initialize with the new game
            _gameStateMachine.InitializeLoggingState(gameModel);
            
            // Return the current game state - wrapped in Task for async compatibility
            return await Task.FromResult(_gameStateMachine.GetCurrentState());
        }

        /// <summary>
        /// Loads a game from a compressed .catan file.
        /// Creates a fresh GameStateMachine and restores the full game history including undo/redo stacks.
        /// </summary>
        /// <param name="filePath">The path to the .catan file.</param>
        /// <returns>The loaded GameModel with full history.</returns>
        private async Task<GameModel> LoadCompressedGameAsync(string filePath)
        {
            // Create a fresh GameStateMachine for the loaded game
            _gameStateMachine = CreateGameStateMachineWithDesktopDependencies(filePath);
            
            // Read and convert to Base64 for compatibility with existing code
            var compressedBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var base64CompressedLog = Convert.ToBase64String(compressedBytes);
            
            // Load the compressed log and return the game state
            return await _gameStateMachine.HandleLoadCompressedLogAsync(base64CompressedLog);
        }

        /// <summary>
        /// Loads a test scenario from a .catan_test file.
        /// Creates a fresh GameStateMachine and extracts the GameModel from the test file.
        /// </summary>
        /// <param name="filePath">The path to the .catan_test file.</param>
        /// <returns>The loaded GameModel for testing.</returns>
        private async Task<GameModel> LoadTestScenarioAsync(string filePath)
        {
            // Extract GameModel from the test file
            var fileContent = await System.IO.File.ReadAllTextAsync(filePath);
            using var document = System.Text.Json.JsonDocument.Parse(fileContent);
            var root = document.RootElement;
            
            if (!root.TryGetProperty("gameModel", out var gameModelElement))
                throw new GameException(".catan_test file must contain a 'gameModel' property");
            
            var extractedGameModel = JsonHelper.Deserialize<GameModel>(gameModelElement.GetRawText());
            if (extractedGameModel == null)
                throw new GameException("Failed to deserialize GameModel from .catan_test file");
            
            // Create GameStateMachine with Desktop dependencies
            _gameStateMachine = CreateGameStateMachineWithDesktopDependencies(filePath);
            
            // Initialize the log with the GameModel
            _gameStateMachine.InitializeLoggingState(extractedGameModel);
            
            return extractedGameModel;
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
        
        /// <summary>
        /// Handles UpdateSettings message from the UI to receive current application settings.
        /// Stores the settings for use in game creation and file path generation.
        /// </summary>
        /// <param name="recipient">The message recipient (this service).</param>
        /// <param name="message">The settings update message containing current settings.</param>
        private async void HandleUpdateSettingsAsync(object recipient, UpdateSettings? message)
        {
            // Load settings if not provided and not already loaded
            if (message?.Settings == null && _currentSettings == null)
            {
                // Load settings from storage (this will be handled by the settings system)
                // For now, just create empty settings as a fallback
                _currentSettings = new SettingsModel();
                this.TraceMessage("Settings initialized with defaults");
                return;
            }
            else if (message?.Settings != null)
            {
                _currentSettings = message.Settings;
                this.TraceMessage($"Settings updated: {message.Settings.Settings.Count} settings received");
                
                // Update persistence service with save directory from settings
                var saveDirectory = message.Settings.GetStringValue("SaveDirectory");
                if (!string.IsNullOrEmpty(saveDirectory))
                {
                    try
                    {
                        _persistenceService.SaveDirectory = saveDirectory;
                        this.TraceMessage($"✅ Game save directory configured: {saveDirectory}");
                    }
                    catch (Exception ex)
                    {
                        this.TraceMessage($"❌ Failed to set save directory '{saveDirectory}': {ex.Message}");
                        // Don't throw - log the error and continue, but saves will fail until directory is fixed
                    }
                }
                else
                {
                    this.TraceMessage("⚠️ No SaveDirectory setting found - file saves will require absolute paths");
                }
                
                // Send settings to GameService if in service mode
                bool useServiceGame = message.Settings.GetBoolValue("ServiceGame");
                if (useServiceGame && _gameServiceProxy != null)
                {
                    try
                    {
                        await _gameServiceProxy.UpdateSettingsAsync(message.Settings.Settings);
                        this.TraceMessage("✅ Settings sent to GameService");
                    }
                    catch (Exception ex)
                    {
                        this.TraceMessage($"⚠️ Failed to send settings to GameService: {ex.Message}");
                    }
                }
                
                // If this is a runtime update (not initialization), 
                // we may need to re-initialize handlers for dynamic changes
                InitializeHandlersBasedOnSettings();
            }
        }

        /// <summary>
        /// Handles SaveAsRequestMessage from the UI to show file dialog and save game.
        /// </summary>
        private async void HandleSaveAsRequestAsync(object recipient, SaveAsRequestMessage message)
        {
            try
            {
                // Cast to Desktop FileService to access UI-specific methods
                if (_persistenceService is Catan.Services.FileService desktopFileService)
                {
                    var path = await desktopFileService.PickSaveFileAsync("");
                    if (!string.IsNullOrEmpty(path))
                    {
                        // Handle the save operation via existing PersistGameMessage handler
                        HandlePersistGameAsync(this, new PersistGameMessage(LocalPersistActions.SaveAs, path));
                    }
                }
                else
                {
                    this.TraceMessage("SaveAs failed: PersistenceService is not Desktop FileService");
                }
            }
            catch (Exception ex)
            {
                this.TraceMessage($"SaveAs failed: {ex.Message}");
            }
        }

        private void SendErrorMessage(string message, ErrorLevel errorLevel, int indentLevel = 0, [CallerMemberName] string cmb = "", [CallerLineNumber] int cln = 0, [CallerFilePath] string cfp = "")
        {
            this.TraceMessage(errorLevel.ToString() + ": " + message, indentLevel, cmb, cln, cfp);
            Messenger.Send(new ErrorMessage(message, errorLevel, cmb, cln, cfp));
        }
    }
}

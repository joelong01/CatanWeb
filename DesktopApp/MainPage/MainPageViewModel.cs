using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Catan.Services;
using Catan3.Controller;
using Catan3.Utility;
using Catan3.Shared.Models;
using Catan3.Shared.Extensions;
using Catan3.Shared.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
namespace Catan3.Models
{
    /// <summary>
    /// Represents the view model for the main page, including game view model, commands visibility, and game controller.
    /// </summary>
    public partial class MainPageViewModel : ObservableRecipient
    {
        /// <summary>
        /// Gets or sets the game view model.
        /// </summary>
        [ObservableProperty]
        public partial GameViewModel GameViewModel { get; set; }

        /// <summary>
        /// Data that has been set by the UI test automation framework to drive the automated Ui tests
        /// </summary>
        /// 
        [ObservableProperty]
        public partial string SmuggledTestData { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether to show commands.
        /// </summary>
        [ObservableProperty]
        public partial bool ShowCommands { get; set; } = false;

        /// <summary>
        /// Gets the current GameModel serialized as JSON for UI automation access.
        /// This property is bound to AutomationProperties.ItemStatus for test automation.
        /// </summary>
        [ObservableProperty]
        public partial string GameModelJson { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets a value indicating whether recording mode is enabled.
        /// When true, all game actions are recorded for test scenario generation.
        /// </summary>
        [ObservableProperty]
        public partial bool IsRecordMode { get; set; } = App.RecordMode;

        partial void OnGameModelJsonChanged(string value)
        {
            // This is used for UI automation to get the current game state as JSON
            if (GameViewModel.GameModel is not null)
            {
                this.TraceMessage($"GameModelJson changed.  [GameState={GameViewModel.GameModel.GameState}][ExpectedGameHash={GameViewModel.GameModel.GameHash}]");
            }
        }

        partial void OnIsRecordModeChanged(bool value)
        {
            App.RecordMode = value;
            
            if (value)
            {
                // Show debug window when recording starts so user can see recording messages
                DebugWindow.Show();
                
                // Send message to start recording from current game state
                try
                {
                    Messenger.Send(new StartRecordingMessage());
                    this.TraceMessage("🎬 Recording STARTED - Game actions will be captured to .catan_test file");
                    this.TraceMessage("📁 Recording will be saved to Desktop when recording stops");
                }
                catch (Exception ex)
                {
                    this.TraceMessage($"❌ Failed to start recording: {ex.Message}");
                    // Reset the toggle if recording failed to start
                    IsRecordMode = false;
                }
            }
            else
            {
                // Send message to stop recording and save the .catan_test file
                try
                {
                    Messenger.Send(new StopRecordingMessage());
                    this.TraceMessage("🎬 Recording STOPPED - .catan_test file saved");
                }
                catch (Exception ex)
                {
                    this.TraceMessage($"❌ Failed to stop recording: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Gets the game controller.
        /// </summary>
        private GameController GameController { get; set; }
        public bool IsTest { get; private set; }

        /// <summary>
        /// Gets the file service.
        /// </summary>
        private readonly IPersistenceService _fileService;

        /// <summary>
        /// Gets the message service.
        /// </summary>
        public IMessenger MessageService => this.Messenger;

        /// <summary>
        /// Gets the player database.
        /// </summary>
        private readonly IPlayerDatabase _playerDatabase;

        /// <summary>
        /// Gets the player database.
        /// </summary>
        public IPlayerDatabase PlayerDatabase => _playerDatabase;

        /// <summary>
        /// Initializes a new instance of the MainPageViewModel class with the specified file service, player database, selected game, playing player IDs, and file path.
        /// </summary>
        /// <param name="fileService">The file service.</param>
        /// <param name="playerDatabase">The player database.</param>
        /// <param name="selectedGame">The selected game type.</param>
        /// <param name="playingPlayerIds">The list of playing player IDs.</param>
        /// <param name="filePath">The file path.</param>
        public MainPageViewModel(IPersistenceService fileService, IPlayerDatabase playerDatabase, GameType selectedGame, IList<string> playingPlayerIds, string filePath, bool isTest = false)
        {
            FunctionTimer.Enabled = false;
            WeakReferenceMessenger.Default.Send(new EndGame());
            _fileService = fileService;
            _playerDatabase = playerDatabase;
            RegisterMessages();
            GameViewModel = new GameViewModel(playerDatabase);
            GameController = new GameController(_fileService, filePath);
            this.IsTest = isTest;
            if (selectedGame == GameType.SavedGame)
            {
                Messenger.Send(new LoadGameMessage(filePath));
            }
            else
            {
                Messenger.Send(new NewGameMessage(selectedGame, playingPlayerIds, filePath));
            }
        }

        /// <summary>
        /// Registers messages for the view model.
        /// </summary>
        private void RegisterMessages()
        {
            Debug.Assert(Messenger is not null);
            IsActive = true;
            Messenger.Register<EndGame>(this, (recipient, message) =>
            {
                Messenger.UnregisterAll(this);
            });
            Messenger.Register<OpenFileRequestMessage>(this, async (recipient, message) =>
            {
                if (_fileService is null) throw new GameException("File Service is null and it should not be");
                var result = await _fileService.OpenFileAsync(message.Parent, message.Filters);
                Messenger.Send(new OpenFileResponseMessage(result));
            });
        }

        /// <summary>
        /// Ends the game by sending an EndGame message.
        /// </summary>
        public void EndGame()
        {
            Messenger.Send(new EndGame());
        }

        /// <summary>
        /// Called when the users are reordered with drag and drop.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void SetPlayerOrder()
        {
            Debug.Assert(GameViewModel.GameModel is not null);
            List<string> viewModelPlayerIds = GameViewModel.Players.Select(player => player.Id).ToList();
            List<string> gameModelPlayerIds = GameViewModel.GameModel.Players.Select(player => player.Id).ToList();
            if (!viewModelPlayerIds.SequenceEqual(gameModelPlayerIds))
            {
                if (GameViewModel.GameModel.GameState == GameState.FinishedRollOrder)
                {
                    // in this state, make the GameModel match the GameViewModel, but we need to tell
                    // the GameController that there is a new order -- it will be logged, etc.
                    Messenger.Send(new SetPlayerOrderMessage(viewModelPlayerIds));
                }
                else
                {
                    // we are in the wrong state, set the GameViewModel to match what is in the GameModel
                    GameViewModel.SetPlayerOrder(GameViewModel.GameModel);
                }
            }
        }

        public Visibility BIND_ShowBoardMeasurements(GameState currentState)
        {
            if (GameViewModel.GameModel is null) return Visibility.Collapsed;
            return this.GameViewModel.GameModel.AllocationPhase() ? Visibility.Visible : Visibility.Collapsed;
            //return currentState switch
            //{
            //    GameState.PickingBoard or
            //    GameState.AllocateResourceReverse or
            //    GameState.BeginResourceAllocation or
            //    GameState.DoneResourceAllocation or
            //    GameState.FinishedRollOrder or
            //    GameState.WaitingForRollForOrder 
            //      => Visibility.Visible,
            //    _ => Visibility.Collapsed,
            //};
        }
    }
}

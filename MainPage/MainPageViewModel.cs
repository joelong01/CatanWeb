using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Catan.Services;
using Catan3.Controller;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
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
        /// Gets or sets a value indicating whether to show commands.
        /// </summary>
        [ObservableProperty]
        public partial bool ShowCommands { get; set; } = false;

        /// <summary>
        /// Gets the game controller.
        /// </summary>
        private GameController GameController { get; set; }

        /// <summary>
        /// Gets the file service.
        /// </summary>
        private readonly IPersistanceService _fileService;

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
        public MainPageViewModel(IPersistanceService fileService, IPlayerDatabase playerDatabase, GameType selectedGame, IList<string> playingPlayerIds, string filePath)
        {
            FunctionTimer.Enabled = false;
            WeakReferenceMessenger.Default.Send(new EndGame());
            _fileService = fileService;
            _playerDatabase = playerDatabase;
            RegisterMessages();
            GameViewModel = new GameViewModel(playerDatabase);
            GameController = new GameController(_fileService, filePath);
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
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using Catan.Services;
using Catan3.Controller;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
namespace Catan3.Models
{
    public partial class MainPageViewModel : ObservableRecipient
    {
        [ObservableProperty]
        GameViewModel _gameViewModel;
        /// <summary>
        ///     Bound to the SplitView.IsPaneOpen flag in MainPage.xaml
        /// </summary>
        [ObservableProperty]
        bool _showCommands = false;
        private GameController GameController { get; set; }
        private readonly IFileService _fileService;
        public IMessenger MessageService => this.Messenger;
        private readonly IPlayerDatabase _playerDatabase;

        public IPlayerDatabase PlayerDatabase => _playerDatabase;

        public MainPageViewModel(IFileService fileService, IPlayerDatabase playerDatabase, GameType selectedGame, IList<PlayerViewModel> playingPlayers)
        {
            FunctionTimer.Enabled = false;
            _fileService = fileService;
            _playerDatabase = playerDatabase;
            GameController = new GameController();
            RegisterMessages();
            // create a new GameModel - this would usually come from the service
            List<string> playerIds = playingPlayers.Select( p => p.Id ).ToList();
            var gameModel = GameController.NewGame(selectedGame, playerIds);
            var gvm = new GameViewModel(gameModel, playerDatabase);
            this.GameViewModel = gvm;
            GameViewModel.UpdateLayout();
            GameViewModel.SetGameStars();
        }
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
                var result =  await _fileService.GetFileAsync(message.Parent, message.Filters);
                Messenger.Send(new OpenFileResponseMessage(result));
            });

        }
        public void EndGame()
        {
            Messenger.Send(new EndGame());
        }
        /// <summary>
        ///     called when the users are reordered with drag and drop.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void SetPlayerOrder()
        {
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
                    //
                    //  we are in the wrong state, set the GameViewModel to match what is in the GameModel
                    GameViewModel.SetPlayerOrder(GameViewModel.GameModel);
                }
            }

        }
    }
}

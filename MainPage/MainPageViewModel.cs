using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Catan.Utility;
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
        private GameController GameController { get; set; }
        private readonly IFileService _fileService;
        public IMessenger MessageService => this.Messenger;
        public MainPageViewModel(IFileService fileService, GameType selectedGame, List<PlayerViewModel> playingPlayers)
        {
            FunctionTimer.Enabled = false;
            _fileService = fileService;
            GameController = new GameController();
            RegisterMessages();
            // create a new GameModel - this would usually come from the service
            List<string> playerIds = playingPlayers.Select( p => p.Id ).ToList();
            var gameModel = GameController.NewGame(selectedGame, playerIds);
            var gvm = new GameViewModel(gameModel);
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
        }
     
        public void EndGame()
        {
            Messenger.Send(new EndGame());
        }
    }
   
}

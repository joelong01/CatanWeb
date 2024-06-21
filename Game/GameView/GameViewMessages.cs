using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
namespace Catan3.Models
{
    public partial class GameViewModel : ObservableRecipient
    {
        public GameViewModel()
        {
            IsActive = true;
            Id = GetHashCode().ToString();
           
            Messenger.Register<EndGame>(this, (recipient, message) =>
            {
                Messenger.UnregisterAll(this);
            });
            Messenger.Register<ErrorMessage>(this, (recipient, message) =>
            {
                this.ErrorMessage = message;
            });
            Messenger.Register<UpdateGameModel>(this, (recipient, message) => 
            {
                this.GameModel = message.GameModel; // OnGameModelChanged is triggered.
              //  MergeGameModel(message.GameModel); 
            });
            Messenger.Register<RequestTileOwners>(this, (recipient, message) =>
            {
                OnRequestTileOwners(message.TileViewModel);
            });
            Messenger.Register<QueryResourcesMessage>(this, (recipient, message) =>
            {
                ExecuteQuery(message.Resources);
            });
         
        }
        private void OnRequestTileOwners(TileViewModel tileViewModel)
        {
            var buildings = GameModel.Buildings.BuildingsInTile(tileViewModel.Tile.TileKey);
            List<PlayerViewModel> owners = [];
            foreach (var building in buildings)
            {
                if (building.OwnerId is not null)
                {
                    var p = Players.First( player => player.Id == building.OwnerId );
                    Debug.Assert(p is not null);
                    if (p.Id != CurrentPlayer.Id)
                    {
                        owners.Add(p);
                    }
                }
            }
            Messenger.Send(new TileOwnersResponse(owners));
        }
       
    }
}

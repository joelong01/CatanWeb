using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;
namespace Catan3.Models
{
    public partial class GameViewModel
    {
        public string StateMessage(GameModel _, GameState gameState)
        {
            if (this.GameModel is null) throw new Exception("can't have a game without a game model");
            var player = GameModel.CurrentPlayer();
            if (player.UnspentEntitlements.Count > 0)
            {
                return $"{gameState.Description()} [{player.UnspentEntitlements.Count} Entitlement(s)]";
            }
            else
            {
                return $"{gameState.Description()}";
            }

        }
        public string BIND_StarCount(int stars, ObservableCollection<TileModel> _tiles)
        {
            Debug.Assert(GameModel is not null);
            int count = 0;
            foreach (var building in GameModel.Buildings)
            {
                var tiles = TilesForBuildings(building.BuildingKey);
                if (tiles.Stars() == stars) count++;
            }
            return count.ToString();
        }


    }
}

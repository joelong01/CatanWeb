using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;
namespace Catan3.Models
{
    public partial class GameViewModel
    {
        public string StateMessage(GameModel gameModel, GameState gameState)
        {
          
            var player = gameModel.CurrentPlayer();
            int count = player.UnspentEntitlements.Count;
            if (count == 0)
            {
                return $"{gameState.Description()}";
            }
            else if (count == 1)
            {
                return $"[{count} Unspent Entitlement]";
            }
            else
            {
                return $"[{count} Unspent Entitlements]";
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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Catan3.Models
{
    public partial class GameViewModel
    {

        public string StateMessage(GameModel _, GameState gameState)
        {
            return gameState.Description();
        }

        public string BIND_StarCount(int stars, ObservableCollection<TileModel> _tiles)
        {
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

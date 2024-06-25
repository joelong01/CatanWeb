using System.Collections.ObjectModel;
using System.Diagnostics;
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


using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Catan3.Models
{
    public partial class GameModel 
    {
        [JsonConstructor]
        public GameModel()
        {
            _players = [];
            _gameType = CatanGame.Regular;
            _hasSupplementalBuildPhase = false;
        }

        public int StarCount(ResourceTileType tileType)
        {
            var total = this.Tiles.Where(tile => tile.ResourceTileType == tileType)
                .Sum(tile => tile.Stars);

            return total;
        }
    }
}

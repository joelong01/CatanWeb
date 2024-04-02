
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
    }
}

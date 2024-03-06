using System.Collections.ObjectModel;
using System.ComponentModel;
namespace Catan3.Models
{
    /// <summary>
    ///     This class should be Serializable/Deserializable from some future WebService that implements all the Catan rules.
    ///     so *no view data* in here.
    /// </summary>
    public partial class GameModel : INotifyPropertyChanged
    {
        public ObservableCollection<TileModel> Tiles { get; } = [];
        public ObservableCollection<BuildingModel> Buildings { get; } = [];
        public BoardSize BoardSize { get; set; } = BoardSize.Regular;
        public ObservableCollection<PlayerModel> Players { get; } = [];
        public ObservableCollection<RoadModel> Roads { get; } = [];
        public TileKey BaronTile { get; set; } = new TileKey(0, 0, 0);
    }
}

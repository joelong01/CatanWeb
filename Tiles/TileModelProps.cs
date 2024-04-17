using System.Text.Json.Serialization;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
namespace Catan3.Models
{
    public partial class TileModel : ObservableObject
    {
        [ObservableProperty]
        private HexCoordinates _tileKey = HexCoordinates.Default;

        [ObservableProperty]
        private ResourceTileType _resourceTileType = ResourceTileType.None;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Stars))]
        private int _number = 0;

        [ObservableProperty]
        private bool _temporarilyGold = false;
       
    }
}

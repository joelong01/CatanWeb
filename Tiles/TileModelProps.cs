using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
namespace Catan3.Models
{
    public partial class TileModel : ObservableObject
    {
        [ObservableProperty]
        private HexCoordinates _tileKey = HexCoordinates.Default;
        [ObservableProperty]
        private ResourceType _resourceTileType = ResourceType.None;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Stars))]
        private int _number = 0;
        [ObservableProperty]
        private bool _temporarilyGold = false;
        /// <summary>
        ///     this is a little strange to have here instead of the view model because all it does 
        ///     is change the way the tile is displayed -- but only the data models are logged and
        ///     we want highlighting to be part of the logged (and saved) state.
        /// </summary>
        [ObservableProperty]
        private bool _highlighted = false;

        

    }
}

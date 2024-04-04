using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Models
{
    public partial class BuildingKey(HexCoordinates hexcoordinates, HexPosition position) : ObservableObject
    {
        [ObservableProperty]
        private HexCoordinates _hexCoordinates = hexcoordinates;

        [ObservableProperty]
        private HexPosition _position = position;
    }
}

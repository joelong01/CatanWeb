using System;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Models
{
    public partial class BuildingKey(HexCoordinates hexcoordinates, HexPosition position) : ObservableObject, IComparable<BuildingKey>
    {
        [ObservableProperty]
        private HexCoordinates _hexCoordinates = hexcoordinates;

        [ObservableProperty]
        private HexPosition _position = position;

        public int CompareTo(BuildingKey? other)
        {
            if (other is null) return 1;

            // First, compare by HexCoordinates
            int hexCompare = HexCoordinates.CompareTo(other.HexCoordinates);
            if (hexCompare != 0)
            {
                return hexCompare;
            }

            // If HexCoordinates are the same, then compare by HexPosition
            // Since HexPosition is an enum, we can directly compare their underlying integer values
            return Position.CompareTo(other.Position);
        }
    }
}

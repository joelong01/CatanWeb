
using System;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
namespace Catan3.Models
{
    /// <summary>
    /// Represents a harbor model in the game, including its coordinates, type, side, and ownership information.
    /// Implements IComparable for sorting and comparison purposes.
    /// </summary>
    public partial class HarborModel : ObservableObject, IComparable<HarborModel>
    {
        /// <summary>
        /// Gets or sets the coordinates of the harbor.
        /// </summary>
        [ObservableProperty]
        public partial HexCoordinates HexCoordinates { get; set; } = new(0, 0, 0);

        /// <summary>
        /// Gets or sets the type of the harbor.
        /// </summary>
        [ObservableProperty]
        public partial HarborType HarborType { get; set; } = HarborType.ThreeForOne;

        /// <summary>
        /// Gets or sets the side of the hexagon where the harbor is located.
        /// </summary>
        [ObservableProperty]
        public partial HexSide Side { get; set; } = HexSide.Bottom;

        /// <summary>
        /// Gets or sets the owner of the harbor.
        /// </summary>
        [ObservableProperty]
        public partial PlayerModel? Owner { get; set; } = null;

        /// <summary>
        /// Gets the default instance of the HarborModel class.
        /// </summary>
        public static HarborModel Default => new();

        /// <summary>
        /// Initializes a new instance of the HarborModel class with the specified coordinates, type, and side.
        /// </summary>
        /// <param name="tilekey">The coordinates of the harbor.</param>
        /// <param name="harbortype">The type of the harbor.</param>
        /// <param name="position">The side of the hexagon where the harbor is located.</param>
        public HarborModel(HexCoordinates tilekey, HarborType harbortype, HexSide position)
        {
            this.HexCoordinates = tilekey;
            this.HarborType = harbortype;
            this.Side = position;
        }

        /// <summary>
        /// Initializes a new instance of the HarborModel class with default values.
        /// </summary>
        public HarborModel()
        {
        }

        /// <summary>
        /// Compares the current HarborModel with another HarborModel.
        /// </summary>
        /// <param name="other">The HarborModel to compare with the current HarborModel.</param>
        /// <returns>A value that indicates the relative order of the HarborModels being compared.</returns>
        public int CompareTo(HarborModel? other)
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
            return Side.CompareTo(other.Side);
        }
    }
}

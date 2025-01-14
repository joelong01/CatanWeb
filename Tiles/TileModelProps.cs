using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
namespace Catan3.Models
{
    /// <summary>
    /// Represents the model for a tile, including its key, resource type, number, and other properties.
    /// </summary>
    public partial class TileModel : ObservableObject
    {
        /// <summary>
        /// Gets or sets the tile key.
        /// </summary>
        [ObservableProperty]
        public partial HexCoordinates TileKey { get; set; } = HexCoordinates.Default;

        /// <summary>
        /// Gets or sets the resource type of the tile.
        /// </summary>
        [ObservableProperty]
        public partial ResourceType ResourceTileType { get; set; } = ResourceType.None;

        /// <summary>
        /// Gets or sets the number on the tile.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Stars))]
        public partial int Number { get; set; } = 0;

        /// <summary>
        /// Gets or sets a value indicating whether the tile is temporarily gold.
        /// </summary>
        [ObservableProperty]
        public partial bool TemporarilyGold { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether the tile is highlighted.
        /// </summary>
        [ObservableProperty]
        public partial bool Highlighted { get; set; } = false;
    }
}

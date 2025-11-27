using System.Text.Json.Serialization;
using Catan3.Shared.Utility;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Shared.Models
{
    /// <summary>
    /// Represents a tile model in the game.
    /// Supports both plain object usage (for JSON/API) and MVVM usage (for UI data binding).
    /// </summary>
    public partial class TileModel : ObservableObject, IComparable<TileModel>
    {
        [ObservableProperty]
        public partial HexCoordinates TileKey { get; set; } = HexCoordinates.Default;

        [ObservableProperty]
        public partial int Number { get; set; }
        [ObservableProperty]
        public partial ResourceType ResourceTileType { get; set; }

        [ObservableProperty]
        public partial bool Highlighted { get; set; }
        [ObservableProperty]
        public partial bool TemporarilyGold { get; set; }
        public static TileModel Default { get; } = new TileModel();

        public TileModel() { }

        public override string ToString()
        {
            return $"({ResourceTileType}, {Number}, {TileKey}, [Gold={TemporarilyGold})][Highlighted={Highlighted}])";
        }

        public int CompareTo(TileModel? other)
        {
            if (other is null) return 1;
            return TileKey.CompareTo(other.TileKey);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(TileKey, ResourceTileType, Number, TemporarilyGold, Highlighted);
        }

        [JsonIgnore]
        public int Stars
        {
            get
            {
                return Number switch
                {
                    2 or 12 => 1,
                    3 or 11 => 2,
                    4 or 10 => 3,
                    5 or 9 => 4,
                    6 or 8 => 5,
                    7 => 0,
                    _ => throw new System.Exception("Invalid Number"),
                };
            }
        }
    }
}
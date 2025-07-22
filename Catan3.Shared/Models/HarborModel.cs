using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Catan3.Shared.Utility;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Shared.Models
{
    public partial class HarborKey : IComparable<HarborKey>
    {

        public HexCoordinates HexCoordinates { get; set; } = new(0, 0, 0);

        /// <summary>
        /// Gets or sets the type of the harbor.
        /// </summary>

        public HarborType HarborType { get; set; } = HarborType.ThreeForOne;

        /// <summary>
        /// Gets or sets the side of the hexagon where the harbor is located.
        /// </summary>

        public HexSide Side { get; set; } = HexSide.Bottom;

        /// <summary>
        /// Initializes a new instance of the HarborKey class with the specified coordinates, type, and side.
        /// </summary>
        /// <param name="tilekey">The coordinates of the harbor.</param>
        /// <param name="harbortype">The type of the harbor.</param>
        /// <param name="position">The side of the hexagon where the harbor is located.</param>
        public HarborKey(HexCoordinates tilekey, HarborType harbortype, HexSide position)
        {
            this.HexCoordinates = tilekey;
            this.HarborType = harbortype;
            this.Side = position;
        }

        /// <summary>
        /// Initializes a new instance of the HarborKey class with default values.
        /// </summary>
        public HarborKey()
        {
        }

        public override string ToString()
        {
            return $"HarborKey: {HexCoordinates} {HarborType} {Side}";
        }

        /// <summary>
        /// Compares the current HarborKey with another HarborKey.
        /// </summary>
        /// <param name="other">The HarborKey to compare with the current HarborKey.</param>
        /// <returns>A value that indicates the relative order of the HarborKeys being compared.</returns>
        public int CompareTo(HarborKey? other)
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

        public override bool Equals(object? obj)
        {
            if (obj is null || GetType() != obj.GetType())
            {
                return false;
            }

            HarborKey? other = obj as HarborKey;
            if (other is null) return false; // Should not happen due to GetType check

            return HexCoordinates.Equals(other.HexCoordinates) &&
                   HarborType == other.HarborType &&
                   Side == other.Side;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(HexCoordinates, HarborType, Side);
        }

        public static bool operator ==(HarborKey? left, HarborKey? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }
            if (left is null || right is null)
            {
                return false;
            }
            return left.Equals(right);
        }

        public static bool operator !=(HarborKey? left, HarborKey? right)
        {
            return !(left == right);
        }
    }

    /// <summary>
    /// Represents a harbor model in the game, including its key and ownership information.
    /// Implements IComparable for sorting and comparison purposes.
    /// </summary>
    public class HarborModel : IComparable<HarborModel>
    {
        /// <summary>
        /// Gets or sets the key of the harbor.
        /// </summary>

        public HarborKey HarborKey { get; set; } = new();

        /// <summary>
        /// Gets or sets the owner of the harbor.
        /// </summary>

        public PlayerModel? Owner { get; set; } = null;

        /// <summary>
        /// Gets the default instance of the HarborModel class.
        /// </summary>
        public static HarborModel Default => new();

        /// <summary>
        /// Initializes a new instance of the HarborModel class with the specified key.
        /// </summary>
        /// <param name="key">The key of the harbor.</param>
        public HarborModel(HarborKey key)
        {
            this.HarborKey = key;
        }

        /// <summary>
        /// Initializes a new instance of the HarborModel class with the specified coordinates, type, and side.
        /// </summary>
        /// <param name="tilekey">The coordinates of the harbor.</param>
        /// <param name="harbortype">The type of the harbor.</param>
        /// <param name="position">The side of the hexagon where the harbor is located.</param>
        public HarborModel(HexCoordinates tilekey, HarborType harbortype, HexSide position)
        {
            this.HarborKey = new HarborKey(tilekey, harbortype, position);
        }

        /// <summary>
        /// Initializes a new instance of the HarborModel class with default values.
        /// </summary>
        public HarborModel()
        {
        }

        public override string ToString()
        {
            return $"HarborModel: {HarborKey.HexCoordinates} {HarborKey.HarborType} {HarborKey.Side} Owner={Owner?.Id ?? "None"}";
        }

        /// <summary>
        /// Compares the current HarborModel with another HarborModel.
        /// </summary>
        /// <param name="other">The HarborModel to compare with the current HarborModel.</param>
        /// <returns>A value that indicates the relative order of the HarborModels being compared.</returns>
        public int CompareTo(HarborModel? other)
        {
            if (other is null) return 1;
            // Compare by HarborKey
            int keyCompare = HarborKey.CompareTo(other.HarborKey);
            if (keyCompare != 0)
            {
                return keyCompare;
            }
            return 0;
        }

        /// <summary>
        /// Maps each HexSide to the two adjacent HexPositions (vertices).
        /// </summary>
        private static readonly Dictionary<HexSide, (HexPosition, HexPosition)> SideToVertices = new()
        {
            { HexSide.Top, (HexPosition.TopLeft, HexPosition.TopRight) },
            { HexSide.TopRight, (HexPosition.TopRight, HexPosition.Right) },
            { HexSide.BottomRight, (HexPosition.Right, HexPosition.BottomRight) },
            { HexSide.Bottom, (HexPosition.BottomRight, HexPosition.BottomLeft) },
            { HexSide.BottomLeft, (HexPosition.BottomLeft, HexPosition.Left) },
            { HexSide.TopLeft, (HexPosition.Left, HexPosition.TopLeft) },
        };

        /// <summary>
        /// Maps each HexPosition (vertex) to the two adjacent HexSides.
        /// </summary>
        private static readonly Dictionary<HexPosition, List<HexSide>> VertexToSides = new()
        {
            { HexPosition.TopLeft,    new() { HexSide.Top, HexSide.TopLeft } },
            { HexPosition.TopRight,   new() { HexSide.Top, HexSide.TopRight } },
            { HexPosition.Right,      new() { HexSide.TopRight, HexSide.BottomRight } },
            { HexPosition.BottomRight,new() { HexSide.BottomRight, HexSide.Bottom } },
            { HexPosition.BottomLeft, new() { HexSide.Bottom, HexSide.BottomLeft } },
            { HexPosition.Left,       new() { HexSide.BottomLeft, HexSide.TopLeft } },
        };

        /// <summary>
        /// Determines if this harbor is adjacent to the given building.
        /// </summary>
        public bool IsAdjacentToBuilding(BuildingKey buildingKey)
        {
            if (!HarborKey.HexCoordinates.Equals(buildingKey.HexCoordinates))
                return false;

            if (!SideToVertices.TryGetValue(HarborKey.Side, out var vertices))
                return false;

            return buildingKey.Position == vertices.Item1 || buildingKey.Position == vertices.Item2;
        }

        /// <summary>
        /// Gets all (hex, side) pairs for harbors adjacent to the given building key, including all aliases.
        /// </summary>
        public static IEnumerable<(HexCoordinates hex, HexSide side)> GetAdjacentHarborLocations(BuildingKey buildingKey)
        {
            // Helper to yield all (hex, side) pairs for a given (hex, vertex)
            static IEnumerable<(HexCoordinates, HexSide)> HarborLocationsForVertex(HexCoordinates hex, HexPosition pos)
            {
                if (VertexToSides.TryGetValue(pos, out var sides))
                {
                    foreach (var side in sides)
                        yield return (hex, side);
                }
            }

            // Include the original key
            foreach (var loc in HarborLocationsForVertex(buildingKey.HexCoordinates, buildingKey.Position))
                yield return loc;

            // Include all aliases
            foreach (var (aliasPos, dir) in buildingKey.Aliases())
            {
                var aliasHex = buildingKey.HexCoordinates.GetAdjacentTile(dir);
                foreach (var loc in HarborLocationsForVertex(aliasHex, aliasPos))
                    yield return loc;
            }
        }
    }
}

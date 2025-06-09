using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

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

        public override string ToString()
        {
            return $"HarborModel: {HexCoordinates} {HarborType} {Side} Owner={Owner?.Id ?? "None"}";
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
            if (!HexCoordinates.Equals(buildingKey.HexCoordinates))
                return false;

            if (!SideToVertices.TryGetValue(Side, out var vertices))
                return false;

            return buildingKey.Position == vertices.Item1 || buildingKey.Position == vertices.Item2;
        }

        /// <summary>
        /// Sets the owner of any harbor in the collection that is adjacent to the given building.
        /// </summary>
        public static bool SetOwnerIfAdjacent(IEnumerable<HarborModel> harbors, BuildingKey buildingKey, PlayerModel owner)
        {
            foreach (var (hex, side) in GetAdjacentHarborLocations(buildingKey))
            {
                foreach (var harbor in harbors)
                {
                    if (harbor.HexCoordinates.Equals(hex) && harbor.Side == side)
                    {
                        harbor.TraceMessage($"Setting Harbor ownership for {harbor} to {owner} because of buildingKey {buildingKey}");
                        harbor.Owner = owner;
                        return true;
                    }
                }
            }
            harbors.TraceMessage($"{buildingKey} has no harbor");
            return false;
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

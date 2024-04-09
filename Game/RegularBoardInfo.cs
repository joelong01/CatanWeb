using System.Collections.Generic;
using Catan3.Utility;
using Windows.Foundation.Metadata;

namespace Catan3.Models
{

    /// <summary>
    ///     Static data about a game Board
    /// </summary>
    public partial class RegularBoardInfo : IBoardInfo
    {

        public static RegularBoardInfo Default { get; } = new RegularBoardInfo();
        private RegularBoardInfo() => Layout = BoardLayout.Default;
        public bool HasSupplemental => false;
        
        public BoardLayout Layout { get; private set; }
        public List<HexCoordinates> TileKeys { get; } =
             [
                 new(-2, 0, 2),
                 new(-2, 1, 1),
                 new(-2, 2, 0),
                 new(-1, -1, 2),
                 new(-1, 0, 1),
                 new(-1, 1, 0),
                 new(-1, 2, -1),
                 new(0, -2, 2),
                 new(0, -1, 1),
                 new(0, 0, 0),
                 new(0, 1, -1),
                 new(0, 2, -2),
                 new(1, -2, 1),
                 new(1, -1, 0),
                 new(1, 0, -1),
                 new(1, 1, -2),
                 new(2, -2, 0),
                 new(2, -1, -1),
                 new(2, 0, -2)
             ];
        public List<ResourceTileType> Resources { get; } = [
            ResourceTileType.Desert,
            ResourceTileType.Brick,
            ResourceTileType.Brick,
            ResourceTileType.Brick,
            ResourceTileType.Ore,
            ResourceTileType.Ore,
            ResourceTileType.Ore,
            ResourceTileType.Sheep,
            ResourceTileType.Sheep,
            ResourceTileType.Sheep,
            ResourceTileType.Sheep,
            ResourceTileType.Wheat,
            ResourceTileType.Wheat,
            ResourceTileType.Wheat,
            ResourceTileType.Wheat,
            ResourceTileType.Wood,
            ResourceTileType.Wood,
            ResourceTileType.Wood,
            ResourceTileType.Wood
            ];
        public List<HarborModel> Harbors { get; } = [
            new HarborModel(new HexCoordinates(0, -2, 2), HarborType.Ore, HexSide.Top),
            new HarborModel(new HexCoordinates(1, -2, 1), HarborType.Wheat, HexSide.TopRight),
            new HarborModel(new HexCoordinates(2, -1, -1), HarborType.Wood, HexSide.TopRight),
            new HarborModel(new HexCoordinates(2, 0, -2), HarborType.Brick, HexSide.BottomRight),
            new HarborModel(new HexCoordinates(1, 1, -2), HarborType.Sheep, HexSide.Bottom),
            new HarborModel(new HexCoordinates(-1, 2, -1), HarborType.ThreeForOne, HexSide.BottomLeft),
            new HarborModel(new HexCoordinates(-2, 2, 0), HarborType.ThreeForOne, HexSide.BottomLeft),
            new HarborModel(new HexCoordinates(-2, 1, 1), HarborType.ThreeForOne, HexSide.TopLeft),
            new HarborModel(new HexCoordinates(-1, -1, 2), HarborType.ThreeForOne, HexSide.TopLeft)

            ];
        public List<int> Numbers { get; } = [7, 2, 3, 3, 4, 4, 5, 5, 6, 6, 8, 8, 9, 9, 10, 10, 11, 11, 12];
    }

}

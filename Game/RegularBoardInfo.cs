using System.Collections.Generic;
using Catan3.Utility;

namespace Catan3.Models
{

    /// <summary>
    ///     Static data about a game Board
    /// </summary>
    public partial class RegularBoardInfo : IBoardInfo
    {
        public RegularBoardInfo() => Layout = new BoardLayout(ColumnCount, ColumnOffset);
        public bool HasSupplemental => false;
        public static int ColumnCount => 5;
        public static double ColumnOffset => 1.5;
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
        public List<ResourceType> Resources { get; } = [
            ResourceType.Desert,
            ResourceType.Brick,
            ResourceType.Brick,
            ResourceType.Brick,
            ResourceType.Ore,
            ResourceType.Ore,
            ResourceType.Ore,
            ResourceType.Sheep,
            ResourceType.Sheep,
            ResourceType.Sheep,
            ResourceType.Sheep,
            ResourceType.Wheat,
            ResourceType.Wheat,
            ResourceType.Wheat,
            ResourceType.Wheat,
            ResourceType.Wood,
            ResourceType.Wood,
            ResourceType.Wood,
            ResourceType.Wood
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

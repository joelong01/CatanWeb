namespace Catan3.Models
{
    public enum ResourceType
    {
        Sheep, Wood, Ore, Wheat, Brick, GoldMine, Desert,
        Back, None, Sea,
        Coin, Cloth, Paper, Politics, Trade, Science, AnyDevCard, VictoryPoint, Invasion
    };
    public enum Direction
    {
        North,
        NorthEast,
        SouthEast,
        South,
        SouthWest,
        NorthWest
    }

    public enum BuildingState
    {
        Empty,
        Settlement,
        City,
        Pips,
        Knight,
    }
    public enum BoardSize { Regular, Expansion }
    ///     Note that unlike BuidlingPosition, this has a Top and Bottom instead of Left and Right
    public enum HexSide { None = -1, Top = 0, TopRight = 1, BottomRight = 2, Bottom = 3, BottomLeft = 4, TopLeft = 5 };
    public enum RoadState { Unowned, Road, Ship };

    public enum HarborType { Sheep, Wood, Ore, Wheat, Brick, ThreeForOne, None };

}

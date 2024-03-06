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
    /// <summary>
    ///     this order needs to match the CalculateHexGeometry PointCollection order
    /// </summary>
    public enum BuildingPosition
    {
        TopLeft = 0,
        TopRight = 1,
        Right = 2,
        BottomRight = 3,
        BottomLeft = 4,
        Left = 5,
        None,
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
    public enum RoadPosition { None = -1, Top = 0, TopRight = 1, BottomRight = 2, Bottom = 3, BottomLeft = 4, TopLeft = 5 };
    public enum RoadState { Unowned, Road, Ship };
}

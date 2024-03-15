namespace Catan3.Models
{
    public enum ResourceType
    {
        Sheep, Wood, Ore, Wheat, Brick, GoldMine, Desert,
        Back, None, Sea,
        Coin, Cloth, Paper, Politics, Trade, Science, AnyDevCard, VictoryPoint, Invasion
    };
   

    public enum BuildingState
    {
        Empty,
        Settlement,
        City,
        Pips,
        Knight,
    }
    public enum BoardSize { Regular, Expansion }
   
    public enum RoadState { Unowned, Road, Ship };

    public enum HarborType { Sheep, Wood, Ore, Wheat, Brick, ThreeForOne, None };

}

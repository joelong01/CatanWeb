namespace Catan3.Models
{
    public enum ResourceTileType
    {
        Sheep, Wood, Ore, Wheat, Brick, GoldMine, Desert,
        Back, None, Sea,

    };

    public enum ResourceCardType
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
        Stars,
        Knight,
    }
    public enum GameType { Regular, Expansion, Unset }

    public enum RoadState { Unowned, Road, Ship, Highlighted };

    public enum HarborType { Sheep, Wood, Ore, Wheat, Brick, ThreeForOne, None };

    public enum CatanOrientation { FaceUp, FaceDown }

}

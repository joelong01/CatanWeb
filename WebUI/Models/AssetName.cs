namespace Catan3.WebUI.Models
{
    /// <summary>
    /// Strongly-typed identifiers for all game assets.
    /// Used by IAssetService to resolve asset paths for the current theme.
    /// </summary>
    public enum AssetName
    {
        // === Tiles (hex backgrounds) ===
        TileBrick,
        TileWheat,
        TileWood,
        TileOre,
        TileSheep,
        TileDesert,
        TileGoldMine,
        TileSea,
        TileInvasion,

        // === Harbors (trade port images) ===
        HarborBrick,
        HarborOre,
        HarborSheep,
        HarborWheat,
        HarborWood,
        HarborThreeForOne,

        // === Resource Cards (player hand) ===
        CardBrick,
        CardWheat,
        CardWood,
        CardOre,
        CardSheep,
        CardGoldMine,
        CardCloth,
        CardPaper,
        CardCoin,
        CardTrade,
        CardPolitics,
        CardScience,
        CardVictoryPoint,
        CardBack,
        CardRobber,
        CardAnyDev,

        // === Stats (player statistics icons) ===
        StatScore,
        StatRoads,
        StatKnights,
        StatCities,
        StatSettlements,
        StatShips,
        StatDevCards,
        StatResourceCards,
        StatHarbors,
        StatLongestRoad,
        StatLargestArmy,
        StatMetropolis,
        StatGoodRoll,
        StatBadRoll,
        StatTargetted,
        StatRobber,
        StatSkulls,
        StatCheck,
        StatStar,
        StatPirateShip,

        // === Buildings (board pieces) ===
        BuildingCity,
        BuildingSettlement,
        BuildingRoad,
        BuildingShip,
        BuildingKnight,

        // === Backgrounds ===
        BackgroundWater,
        BackgroundBorderFill,   // Maple wood texture for hex border fill
        BackgroundBorderStroke, // Cherry wood texture for hex border stroke

        // === Fonts ===
        FontCatan
    }
}

using Catan3.Shared.Utility;

namespace Catan3.Shared.Models
{
    public partial class ExpansionBoardInfo : IGameMetadata
    {
        public GameType GameType { get; } = GameType.Expansion;
        public string Description { get; } = "Expansion Game";
        public HouseRules HouseRules { get; } = new HouseRules() { GoldTiles = 2, HideBaronBeforeInvasion = false, KnightMovesBaronBeforeRoll = true, WallsProtectCities = true };
        public static ExpansionBoardInfo Default { get; } = new ExpansionBoardInfo();
        public ResourceRules ResourceRules { get; } = new ResourceRules(4, 5, 15, 3, 6);
        private ExpansionBoardInfo() { }
        public bool HasSupplemental => true;
        public List<HexCoordinates> TileKeys { get; } =
             [
                // column 1
            
                new(-3, 1, 2),
                new(-3, 2, 1),
                new(-3, 3, 0),
                //column 2
            
                new(-2, 0, 2),
                new(-2, 1, 1),
                new(-2, 2, 0),
                new(-2, 3, -1),
                //column 3
                new(-1, -1, 2),
                new(-1, 0, 1),
                new(-1, 1, 0),
                new(-1, 2, -1),
                new(-1, 3, -2),
                
                // mid column (4)
                new(0, -2, 2),
                new(0, -1, 1),
                new(0, 0, 0),
                new(0, 1, -1),
                new(0, 2, -2),
                new(0, 3, -3),
                // column 5
              
                new(1, -2, 1),
                new(1, -1, 0),
                new(1, 0, -1),
                new(1, 1, -2),
                new(1, 2, -3),
                //column 6
               
                new(2, -2, 0),
                new(2, -1, -1),
                new(2, 0, -2),
                new(2, 1, -3),
                // column 7
            
                new(3, -2, -1),
                new(3, -1, -2),
                new(3, 0, -3),
             ];
        public List<ResourceType> Resources { get; } = [
                ResourceType.Desert,
                ResourceType.Desert,
                ResourceType.Brick,
                ResourceType.Brick,
                ResourceType.Brick,
                ResourceType.Brick,
                ResourceType.Brick,
                ResourceType.Brick,
                ResourceType.Ore,
                ResourceType.Ore,
                ResourceType.Ore,
                ResourceType.Ore,
                ResourceType.Ore,
                ResourceType.Sheep,
                ResourceType.Sheep,
                ResourceType.Sheep,
                ResourceType.Sheep,
                ResourceType.Sheep,
                ResourceType.Sheep,
                ResourceType.Wheat,
                ResourceType.Wheat,
                ResourceType.Wheat,
                ResourceType.Wheat,
                ResourceType.Wheat,
                ResourceType.Wood,
                ResourceType.Wood,
                ResourceType.Wood,
                ResourceType.Wood,
                ResourceType.Wood,
                ResourceType.Wood
            ];
        public List<HarborModel> Harbors { get; } = [
            new HarborModel(new HexCoordinates(-2, 0, 2), HarborType.Ore, HexSide.TopLeft),
            new HarborModel(new HexCoordinates(0, -2, 2), HarborType.Wheat, HexSide.Top),
            new HarborModel(new HexCoordinates(1, -2, 1), HarborType.Wood, HexSide.TopRight),
            new HarborModel(new HexCoordinates(2, -2, 0), HarborType.Brick, HexSide.TopRight),
            new HarborModel(new HexCoordinates(3, 0, -3), HarborType.Sheep, HexSide.BottomRight),
            new HarborModel(new HexCoordinates(2, 1, -3), HarborType.ThreeForOne, HexSide.Bottom),
            new HarborModel(new HexCoordinates(1, 2, -3), HarborType.ThreeForOne, HexSide.Bottom),
            new HarborModel(new HexCoordinates(-1,3,-2), HarborType.ThreeForOne, HexSide.Bottom),
            new HarborModel(new HexCoordinates(-3, 3, 0), HarborType.ThreeForOne, HexSide.BottomLeft),
            new HarborModel(new HexCoordinates(-3, 2, 1), HarborType.ThreeForOne, HexSide.TopLeft),
            new HarborModel(new HexCoordinates(3, -1, -2), HarborType.ThreeForOne, HexSide.TopRight),
            ];
        public List<int> Numbers { get; } = [7, 7, 2, 2, 3, 3, 3, 4, 4, 4, 5, 5, 5, 6, 6, 6, 8, 8, 8, 9, 9, 9, 10, 10, 10, 11, 11, 11, 12, 12];
        public List<EntitlementPurchaseModel> PurchaseableEntitlements { get; } = [
                new EntitlementPurchaseModel(Entitlement.City),
                new EntitlementPurchaseModel(Entitlement.Settlement),
                new EntitlementPurchaseModel(Entitlement.Road),
                new EntitlementPurchaseModel(Entitlement.Soldier),
            ];
    }
}

using System.Collections.Generic;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;

namespace Catan3.GameService.Factory
{
    /// <summary>
    ///     Static data about a game Board
    /// </summary>
    public partial class RegularBoardInfo : IGameMetadata
    {
        public GameType GameType => GameType.Regular;
        public string Description => "Regular 4-player board";
        public HouseRules HouseRules => HouseRules.Default;
        public ResourceRules ResourceRules => new ResourceRules(5, 5, 15, 3, 4);
        public static RegularBoardInfo Default { get; } = new RegularBoardInfo();
        public bool HasSupplemental => false;
        public BoardLayout BoardLayout => BoardLayout.Default;
        public List<HexCoordinates> TileKeys => new List<HexCoordinates>
             {
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
             };
        public List<ResourceType> Resources => new List<ResourceType>
        {
            ResourceType.Desert, ResourceType.Ore, ResourceType.Sheep,
            ResourceType.Wheat, ResourceType.Brick, ResourceType.Wood, ResourceType.Sheep,
            ResourceType.Wheat, ResourceType.Wood, ResourceType.Wheat, ResourceType.Ore, ResourceType.Sheep,
            ResourceType.Brick, ResourceType.Ore, ResourceType.Wheat, ResourceType.Wood,
            ResourceType.Brick, ResourceType.Wood, ResourceType.Sheep
        };
        public List<int> Numbers => new List<int>
        {
            7, 10, 2,
            9, 12, 6, 4,
            10, 9, 11, 3, 8,
            8, 3, 4, 5,
            5, 6, 11
        };
        public List<HarborModel> Harbors => new List<HarborModel>
        {
            new HarborModel(new HexCoordinates(0, -2, 2), HarborType.Ore, HexSide.Top),
            new HarborModel(new HexCoordinates(1, -2, 1), HarborType.Wheat, HexSide.TopRight),
            new HarborModel(new HexCoordinates(2, -1, -1), HarborType.Wood, HexSide.TopRight),
            new HarborModel(new HexCoordinates(2, 0, -2), HarborType.Brick, HexSide.BottomRight),
            new HarborModel(new HexCoordinates(1, 1, -2), HarborType.Sheep, HexSide.Bottom),
            new HarborModel(new HexCoordinates(-1, 2, -1), HarborType.ThreeForOne, HexSide.BottomLeft),
            new HarborModel(new HexCoordinates(-2, 2, 0), HarborType.ThreeForOne, HexSide.BottomLeft),
            new HarborModel(new HexCoordinates(-2, 1, 1), HarborType.ThreeForOne, HexSide.TopLeft),
            new HarborModel(new HexCoordinates(-1, -1, 2), HarborType.ThreeForOne, HexSide.TopLeft)
        };
        public List<EntitlementPurchaseModel> PurchaseableEntitlements => new List<EntitlementPurchaseModel>
        {
            new EntitlementPurchaseModel(Entitlement.Settlement),
            new EntitlementPurchaseModel(Entitlement.City),
            new EntitlementPurchaseModel(Entitlement.Road),
            new EntitlementPurchaseModel(Entitlement.Soldier)
        };
    }
}

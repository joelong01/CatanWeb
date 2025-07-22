using System.Collections.Generic;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;

namespace Catan3.GameService.Factory
{
    public class ExpansionBoardInfo : IGameMetadata
    {
        public static ExpansionBoardInfo Default { get; } = new ExpansionBoardInfo();
        public GameType GameType => GameType.Expansion;
        public string Description => "Expansion 4-player board with Cities & Knights";
        public HouseRules HouseRules => HouseRules.Default;
        public ResourceRules ResourceRules => new ResourceRules(4, 5, 15, 3, 4);
        public bool HasSupplemental => true;
        public BoardLayout BoardLayout => BoardLayout.Default;
        
        public List<HexCoordinates> TileKeys => new List<HexCoordinates>
        {
            new HexCoordinates(-2, 0, 2), new HexCoordinates(-2, 1, 1), new HexCoordinates(-2, 2, 0),
            new HexCoordinates(-1, -1, 2), new HexCoordinates(-1, 0, 1), new HexCoordinates(-1, 1, 0), new HexCoordinates(-1, 2, -1),
            new HexCoordinates(0, -2, 2), new HexCoordinates(0, -1, 1), new HexCoordinates(0, 0, 0), new HexCoordinates(0, 1, -1), new HexCoordinates(0, 2, -2),
            new HexCoordinates(1, -2, 1), new HexCoordinates(1, -1, 0), new HexCoordinates(1, 0, -1), new HexCoordinates(1, 1, -2),
            new HexCoordinates(2, -2, 0), new HexCoordinates(2, -1, -1), new HexCoordinates(2, 0, -2)
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
            // Add harbor definitions
        };

        public List<EntitlementPurchaseModel> PurchaseableEntitlements => new List<EntitlementPurchaseModel>
        {
            new EntitlementPurchaseModel(Entitlement.Settlement),
            new EntitlementPurchaseModel(Entitlement.City),
            new EntitlementPurchaseModel(Entitlement.Road),
            new EntitlementPurchaseModel(Entitlement.Soldier),
            new EntitlementPurchaseModel(Entitlement.BuyKnight),
            new EntitlementPurchaseModel(Entitlement.UpgradeKnight),
            new EntitlementPurchaseModel(Entitlement.ActivateKnight),
            new EntitlementPurchaseModel(Entitlement.PoliticsUpgrade),
            new EntitlementPurchaseModel(Entitlement.ScienceUpgrade),
            new EntitlementPurchaseModel(Entitlement.TradeUpgrade)
        };
    }
}
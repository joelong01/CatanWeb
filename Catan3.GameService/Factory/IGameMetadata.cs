using Catan3.Shared.Utility;

namespace Catan3.GameService.Factory
{
    public interface IGameMetadata
    {
        Catan3.Shared.Models.GameType GameType { get; }
        string Description { get; }
        Catan3.Shared.Models.HouseRules HouseRules { get; }
        Catan3.Shared.Models.ResourceRules ResourceRules { get; }
        bool HasSupplemental { get; }
        List<HexCoordinates> TileKeys { get; }
        List<Catan3.Shared.Models.ResourceType> Resources { get; }
        List<Catan3.Shared.Models.HarborModel> Harbors { get; }
        List<int> Numbers { get; }
        List<Catan3.Shared.Models.EntitlementPurchaseModel> PurchaseableEntitlements { get; }
    }
}
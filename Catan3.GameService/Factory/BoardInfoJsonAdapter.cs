using Catan3.Shared.Models;
using Catan3.Shared.Utility;

namespace Catan3.GameService.Factory;

/// <summary>
/// Adapts a GameTemplateData JSON document to the IGameMetadata interface
/// used by the GameStateMachine. This allows templates stored in the database
/// to produce identical behavior to the hardcoded RegularBoardInfo/ExpansionBoardInfo singletons.
/// </summary>
public class BoardInfoJsonAdapter : Catan3.Shared.Models.IGameMetadata
{
    private readonly GameTemplateData _template;

    public BoardInfoJsonAdapter(GameTemplateData template)
    {
        _template = template;
    }

    public GameType GameType => Enum.Parse<GameType>(_template.GameType);
    public string Description => _template.Description;
    public ResourceRules ResourceRules => _template.ResourceRules;
    public HouseRules HouseRules => _template.HouseRules;
    public bool HasSupplemental => _template.HasSupplemental;

    public List<HexCoordinates> TileKeys =>
        _template.Tiles.Select(t => new HexCoordinates(t.Q, t.R, -t.Q - t.R)).ToList();

    public List<ResourceType> Resources =>
        _template.Tiles.Select(t => Enum.Parse<ResourceType>(t.Resource)).ToList();

    public List<int> Numbers =>
        _template.Tiles.Select(t => t.Number).ToList();

    public List<HarborModel> Harbors =>
        _template.Harbors.Select(h => new HarborModel(
            new HexCoordinates(h.Q, h.R, -h.Q - h.R),
            Enum.Parse<HarborType>(h.Type),
            Enum.Parse<HexSide>(h.Side)
        )).ToList();

    public List<EntitlementPurchaseModel> PurchaseableEntitlements =>
        _template.Entitlements.Select(e => new EntitlementPurchaseModel(
            Enum.Parse<Entitlement>(e.Entitlement)
        )).ToList();
}

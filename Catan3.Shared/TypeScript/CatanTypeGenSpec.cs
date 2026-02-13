using TypeGen.Core.SpecGeneration;
using Catan3.Shared.Models;
using Catan3.Shared.Profiles;
using Catan3.Shared.Utility;

namespace Catan3.Shared.TypeScript;

/// <summary>
/// TypeGen generation spec that exports all game models to TypeScript interfaces.
/// Output is written to react-ui/types/generated/models/
/// </summary>
public class CatanTypeGenSpec : GenerationSpec
{
    public override void OnBeforeGeneration(OnBeforeGenerationArgs args)
    {
        // Core game model
        AddInterface<GameModel>();
        AddInterface<ActionFlags>();

        // Player models
        AddInterface<PlayerModel>();

        // Player profile models (persistent identity, colors, statistics)
        AddInterface<PlayerProfile>();
        AddInterface<PlayerColors>();
        AddInterface<LifetimeStats>();
        AddInterface<GameStats>();

        // Board models
        AddInterface<TileModel>();
        AddInterface<BuildingModel>();
        AddInterface<RoadModel>();
        AddInterface<HarborModel>();
        AddInterface<HarborKey>();
        AddInterface<RobberModel>();

        // Resource models
        AddInterface<ResourcesModel>();
        AddInterface<ResourceCounterModel>();

        // Game configuration
        AddInterface<HouseRules>();
        AddInterface<ResourceRules>();
        AddInterface<EntitlementPurchaseModel>();

        // Roll/dice models
        AddInterface<RollModel>();
        AddInterface<GameRollModel>();
        AddInterface<TurnRollModel>();

        // Keys for buildings/roads
        AddInterface<BuildingKey>();
        AddInterface<RoadKey>();

        // Coordinates
        AddInterface<HexCoordinates>();

        // Enums
        AddEnum<GameState>();
        AddEnum<GameType>();
        AddEnum<ResourceType>();
        AddEnum<BuildingState>();
        AddEnum<BuildingVisualState>();
        AddEnum<RoadState>();
        AddEnum<HarborType>();
        AddEnum<HexPosition>();
        AddEnum<HexSide>();
        AddEnum<Direction>();
        AddEnum<ActionType>();
        AddEnum<Entitlement>();
        AddEnum<GamePhase>();
        AddEnum<CatanOrientation>();
        AddEnum<SpecialDice>();
        AddEnum<ValidCatanRoll>();

        // Message objects (for SignalR communication)
        AddInterface<RollMessage>();
        AddInterface<UndoMessage>();
        AddInterface<RedoMessage>();
        AddInterface<NextMessage>();
        AddInterface<PurchaseMessage>();
        AddInterface<NewGameMessage>();
        AddInterface<MoveRobberMessage>();
        AddInterface<RoadPurchaseMessage>();
        AddInterface<BuildingUpgradeMessage>();
        AddInterface<GoFirstMessage>();
        AddInterface<ParticipatingInSupplementalMessage>();
        AddInterface<DeclareWinnerMessage>();
        AddInterface<ShuffleMessage>();
        AddInterface<BalanceBoardMessage>();
        AddInterface<LoadGameMessage>();
        AddInterface<UpdateHouseRulesMessage>();

        // Game template models (for template CRUD API)
        AddInterface<GameTemplateData>();
        AddInterface<GameTemplateSummary>();
        AddInterface<TemplateTile>();
        AddInterface<TemplateHarbor>();
        AddInterface<TemplateEntitlement>();
    }
}

using System.ComponentModel;
namespace Catan3.Models
{
    public enum GameAction
    {
        Shuffle, Undo, Redo,
        Next, Balance
    }
    public enum ResourceType
    {
        Sheep, Wood, Ore, Wheat, Brick, GoldMine, Desert,
        Back, None, Sea, Coin, Cloth, Paper, Politics, Trade, Science, AnyDevCard, VictoryPoint, Invasion, Robber
    };
    public enum BuildingState
    {
        PossibleSettlement,
        NotBuildable,
        Settlement,
        City,
        Metropolis,
        Knight,
    }
    public enum BuildingVisualState
    {
        Highlighted,
        Hidden,
        Stars, 
        Normal
    }
    public enum GameType { Regular, Expansion, Unset,
        SavedGame
    }
    public enum RoadState { Unowned, Road, Ship, Buildable };
    public enum HarborType { Sheep, Wood, Ore, Wheat, Brick, ThreeForOne, None };
    public enum CatanOrientation { FaceUp, FaceDown }
    public enum GameState
    {
        [Description("Uninitialized")]
        Uninitialized,
        [Description("New Game")]
        WaitingForNewGame,
        [Description("Start Pick Resources")]
        BeginResourceAllocation,
        [Description("Pick Board...")]
        WaitingForPlayers,
        [Description("Accept Board")]
        PickingBoard,
        [Description("Roll For Order...")]
        WaitingForRollForOrder,
        [Description("Order Done")]
        FinishedRollOrder,
        [Description("Next")]
        AllocateResourceForward,
        [Description("Next")]
        AllocateResourceReverse,
        [Description("Start Game...")]
        DoneResourceAllocation,
        [Description("Select Roll...")]
        WaitingForRoll,
        [Description("Turn Over.  Next Player.")]
        WaitingForNext,
        [Description("Supplemental")]
        Supplemental,
        [Description("Discard Cards")]
        TooManyCards,
        [Description("Destroy City")]
        MustDestroyCity,
        [Description("Picking Random Gold Cards")]
        PickingRandomGoldTiles,
        [Description("Handling Pirate")]
        HandlePirates,
        [Description("Done")]
        DoneDestroyingCities,
        [Description("Move Merchant")]
        MustMoveMerchant,
        [Description("Destroy Road")]
        DestroyRoad,
        [Description("Swap Numbers")]
        SwapNumbers,
        [Description("Pick a Deserter")]
        PickDeserter,
        [Description("Place Deserter")]
        PlaceDeserterKnight,
        DoneWithDeserter,
        [Description("Pick City")]
        UpgradeToMetro,
        [Description("Test Checkpoint")]
        TestCheckpoint,
        [Description("Move Robber")]
        MustMoveRobber,
        [Description("DnD Agressor on Victim")]
        DisplaceVictimKnight,
        [Description("Move Target Knight")]
        DisplaceKnightMoveVictim,
        [Description("Select Knight")]
        ClickOnKnight,
        [Description("Pick Supplemental Players")]
        PickSupplementalPlayers
    };
    public enum GamePhase { Starting, PickingBoard, PickingResources, Rolling, Purchase, ActionRequired, Unspecified }
    public enum SpecialDice { Trade=0, Politics=1, Science=2, Pirate=3, None=-1 };
    public enum ValidCatanRoll { Two=2, Three=3, Four=4, Five=5, Six=6, Seven=7, Eight=8, Nine=9, Ten=10, Eleven=11, Twelve=12, None=-1 };
    public enum Entitlement
    {
        [Description("Undefined")]
        Undefined,
        [Description("Dev Card")]
        DevCard,
        [Description("Settlement")]
        Settlement,
        [Description("City")]
        City,
        [Description("Road")]
        Road,
        [Description("Ship")]
        Ship,
        [Description("Buy")]
        BuyKnight,
        [Description("Upgrade")]
        UpgradeKnight,
        [Description("Activate")]
        ActivateKnight,
        [Description("Play Soldier")]
        Soldier,
        [Description("Politics")]
        PoliticsUpgrade,
        [Description("Science")]
        ScienceUpgrade,
        [Description("Trade")]
        TradeUpgrade,
        [Description("Wall")]
        Wall,
        [Description("Destroy City")]
        DestroyCity,
        [Description("Bishop")]
        Bishop,
        [Description("PickDeserter")]
        Deserter,
        [Description("Inventor")]
        Inventor,
        [Description("Intrigue")]
        Intrigue,
        [Description("Diplomat")]
        Diplomat,
        [Description("Merchant")]
        Merchant,
        [Description("Displace")]
        KnightDisplacement,
        [Description("Metropolis")]
        UpgradeToMetro,
        KnightDisplacementMoveKnightOutOfTheWay,
        [Description("Rolled Seven")]
        RolledSeven,
    }
}

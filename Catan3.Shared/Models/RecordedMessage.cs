using System.Text.Json.Serialization;
using Catan3.Shared.Utility;
// --------------------------------------------------------------------------------------------
// RecordedMessage.cs
//
// This file defines the pattern for recording MVVM messages during gameplay and replaying them
// later in automated UI tests. The design uses System.Text.Json's built-in polymorphic 
// serialization so a heterogeneous list of different message types can be persisted and restored.
//
// Pattern to follow when adding a new message type:
//
//   1. Identify the MVVM message type you want to record (e.g. ExecuteGameActionMessage).
//
//   2. Create a corresponding sealed record class named <prefix>Record 
//      (e.g. ExecuteGameActionRecord) that implements IRecordedMessage.
//         � Include the properties you want to capture for replay.
//         � Add a constructor that takes the MVVM message so you can build the record at runtime.
//         � Add a [JsonConstructor] constructor with init properties so it can deserialize cleanly.
//
//   3. Register the record class as a derived type on IRecordedMessage using 
//      [JsonDerivedType(typeof(ExecuteGameActionRecord), "executeGameAction")].
//         � The string discriminator ("executeGameAction") must match what will appear under "type" in JSON.
//         � Keep discriminator casing consistent with existing entries.
//
//   4. Implement an extension method in MessageConverters (e.g. ToRecord) to easily convert 
//      the MVVM message into its record form while the game is running.
//
//   5. During replay, deserialize JSON into List<IRecordedMessage> and either:
//         � Pattern match on the runtime type (case � when ExecuteGameActionRecord), or
//         � Switch on the RecordType property, then cast to the concrete type.
//
// This pattern ensures that gameplay can be recorded as a sequence of strongly typed records 
// and later faithfully replayed in automated tests.
// --------------------------------------------------------------------------------------------


namespace Catan3.Shared.Models
{
    /// <summary>
    /// Represents a snapshot of a runtime MVVM message that is appended to a recording
    /// during gameplay and later deserialized for UI test replay. The interface is the
    /// common contract that enables polymorphic JSON serialization: each concrete record
    /// type is registered with a discriminator so a heterogeneous list can be written
    /// and read back as the correct runtime types.
    ///
    /// Recording flow:
    ///   � As MVVM messages are raised, convert them to concrete record objects and append
    ///     to a List&lt;IRecordedMessage&gt;.
    ///   � Serialize the list with System.Text.Json; each element includes "type" plus its data.
    ///
    /// Replay flow:
    ///   � Deserialize the JSON back to List&lt;IRecordedMessage&gt;.
    ///   � Branch by concrete type (pattern matching) or via <see cref="RecordType"/> to access data.
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(UndoRecord), UndoRecord.Discriminator)]
    [JsonDerivedType(typeof(RedoRecord), RedoRecord.Discriminator)]
    [JsonDerivedType(typeof(NextRecord), NextRecord.Discriminator)]
    [JsonDerivedType(typeof(ShuffleRecord), ShuffleRecord.Discriminator)]
    [JsonDerivedType(typeof(PurchaseRecord), PurchaseRecord.Discriminator)]
    [JsonDerivedType(typeof(BuildingUpgradeRecord), BuildingUpgradeRecord.Discriminator)]
    [JsonDerivedType(typeof(RoadPurchaseRecord), RoadPurchaseRecord.Discriminator)]
    [JsonDerivedType(typeof(MoveRobberRecord), MoveRobberRecord.Discriminator)]
    [JsonDerivedType(typeof(RollRecord), RollRecord.Discriminator)]
    [JsonDerivedType(typeof(SetPlayerOrderRecord), SetPlayerOrderRecord.Discriminator)]
    [JsonDerivedType(typeof(GoFirstRecord), GoFirstRecord.Discriminator)]
    [JsonDerivedType(typeof(ParticipatingInSupplementalRecord), ParticipatingInSupplementalRecord.Discriminator)]
    [JsonDerivedType(typeof(BalanceBoardRecord), BalanceBoardRecord.Discriminator)]
    [JsonDerivedType(typeof(SwapTileResourcesRecord), SwapTileResourcesRecord.Discriminator)]
    public interface IRecordedMessage
    {
        /// <summary>
        /// Stable identifier for the game state associated with this record.
        /// </summary>
        string ExpectedGameHash { get; }

        /// <summary>
        /// The game state when this message was recorded.
        /// Used for validation during test replay.
        /// </summary>
        GameState ExpectedGameState { get; }

        /// <summary>
        /// The discriminator for this record as it appears in JSON under "type".
        /// This is provided for convenient branching during replay.
        /// </summary>
        string RecordType { get; }
    }

    /// <summary>
    /// Snapshot of an <c>UndoMessage</c> suitable for recording and replay.
    /// </summary>
    public sealed class UndoRecord : IRecordedMessage
    {
        /// <summary>
        /// Discriminator value written to/expected from JSON: <c>"undoRecord"</c>.
        /// </summary>
        public const string Discriminator = "undoRecord";

        /// <inheritdoc />
        public string ExpectedGameHash { get; init; } = string.Empty;
        /// <inheritdoc />
        public GameState ExpectedGameState { get; init; } = GameState.Uninitialized;

        /// <inheritdoc />
        [JsonIgnore]
        public string RecordType => Discriminator;

        /// <summary>
        /// Constructor used during deserialization and for programmatic creation.
        /// </summary>
        [JsonConstructor]
        public UndoRecord(string expectedGameHash, GameState expectedGameState)
        {
            ExpectedGameHash = expectedGameHash;
            ExpectedGameState = expectedGameState;
        }

        /// <summary>
        /// Convenience constructor to capture an <see cref="UndoMessage"/> at runtime.
        /// </summary>
        public UndoRecord(GameModel gameModel, UndoMessage message)
        {
            ExpectedGameHash = gameModel.GameHash;
            ExpectedGameState = gameModel.GameState;
        }
    }

    /// <summary>
    /// Snapshot of a <c>RedoMessage</c> suitable for recording and replay.
    /// </summary>
    public sealed class RedoRecord : IRecordedMessage
    {
        /// <summary>
        /// Discriminator value written to/expected from JSON: <c>"redoRecord"</c>.
        /// </summary>
        public const string Discriminator = "redoRecord";

        /// <inheritdoc />
        public string ExpectedGameHash { get; init; } = string.Empty;
        /// <inheritdoc />
        public GameState ExpectedGameState { get; init; } = GameState.Uninitialized;

        /// <inheritdoc />
        [JsonIgnore]
        public string RecordType => Discriminator;

        /// <summary>
        /// Constructor used during deserialization and for programmatic creation.
        /// </summary>
        [JsonConstructor]
        public RedoRecord(string expectedGameHash, GameState expectedGameState)
        {
            ExpectedGameHash = expectedGameHash;
            ExpectedGameState = expectedGameState;
        }

        /// <summary>
        /// Convenience constructor to capture a <see cref="RedoMessage"/> at runtime.
        /// </summary>
        public RedoRecord(GameModel gameModel, RedoMessage message)
        {
            ExpectedGameHash = gameModel.GameHash;
            ExpectedGameState = gameModel.GameState;
        }
    }

    /// <summary>
    /// Snapshot of a <c>NextMessage</c> suitable for recording and replay.
    /// </summary>
    public sealed class NextRecord : IRecordedMessage
    {
        /// <summary>
        /// Discriminator value written to/expected from JSON: <c>"nextRecord"</c>.
        /// </summary>
        public const string Discriminator = "nextRecord";

        /// <inheritdoc />
        public string ExpectedGameHash { get; init; } = string.Empty;
        /// <inheritdoc />
        public GameState ExpectedGameState { get; init; } = GameState.Uninitialized;

        /// <inheritdoc />
        [JsonIgnore]
        public string RecordType => Discriminator;

        /// <summary>
        /// Constructor used during deserialization and for programmatic creation.
        /// </summary>
        [JsonConstructor]
        public NextRecord(string expectedGameHash, GameState expectedGameState)
        {
            ExpectedGameHash = expectedGameHash;
            ExpectedGameState = expectedGameState;
        }

        /// <summary>
        /// Convenience constructor to capture a <see cref="NextMessage"/> at runtime.
        /// </summary>
        public NextRecord(GameModel gameModel, NextMessage message)
        {
            ExpectedGameHash = gameModel.GameHash;
            ExpectedGameState = gameModel.GameState;
        }
    }

    /// <summary>
    /// Snapshot of a <c>ShuffleMessage</c> suitable for recording and replay.
    /// </summary>
    public sealed class ShuffleRecord : IRecordedMessage
    {
        /// <summary>
        /// Discriminator value written to/expected from JSON: <c>"shuffleRecord"</c>.
        /// </summary>
        public const string Discriminator = "shuffleRecord";

        /// <inheritdoc />
        public string ExpectedGameHash { get; init; } = string.Empty;
        /// <inheritdoc />
        public GameState ExpectedGameState { get; init; } = GameState.Uninitialized;

        /// <inheritdoc />


        /// <inheritdoc />
        [JsonIgnore]
        public string RecordType => Discriminator;

        /// <summary>
        /// Constructor used during deserialization and for programmatic creation.
        /// </summary>
        [JsonConstructor]
        public ShuffleRecord(string expectedGameHash, GameState expectedGameState)
        {
            ExpectedGameHash = expectedGameHash;
            ExpectedGameState = expectedGameState;
        }

        /// <summary>
        /// Convenience constructor to capture a <see cref="ShuffleMessage"/> at runtime.
        /// </summary>
        public ShuffleRecord(GameModel gameModel, ShuffleMessage message)
        {
            ExpectedGameHash = gameModel.GameHash;
            ExpectedGameState = gameModel.GameState;
        }
    }

    /// <summary>
    /// Snapshot of a <c>PurchaseMessage</c> suitable for recording and replay.
    /// </summary>
    public sealed class PurchaseRecord : IRecordedMessage
    {
        /// <summary>
        /// Discriminator value written to/expected from JSON: <c>"purchase"</c>.
        /// </summary>
        public const string Discriminator = "purchase";

        /// <inheritdoc />
        public string ExpectedGameHash { get; init; } = string.Empty;
        /// <inheritdoc />
        public GameState ExpectedGameState { get; init; } = GameState.Uninitialized;



        /// <summary>
        /// The entitlement purchased.
        /// </summary>
        public Entitlement Entitlement { get; init; } = default!;

        /// <inheritdoc />
        [JsonIgnore]
        public string RecordType => Discriminator;

        /// <summary>
        /// Constructor used during deserialization and for programmatic creation.
        /// </summary>
        [JsonConstructor]
        public PurchaseRecord(string expectedGameHash, GameState expectedGameState, Entitlement entitlement)
        {
            ExpectedGameHash = expectedGameHash;
            ExpectedGameState = expectedGameState;
            Entitlement = entitlement;
        }

        /// <summary>
        /// Convenience constructor to capture a <see cref="PurchaseMessage"/> at runtime.
        /// </summary>
        public PurchaseRecord(GameModel gameModel, PurchaseMessage message)
        {
            ExpectedGameHash = gameModel.GameHash;
            ExpectedGameState = gameModel.GameState;
            Entitlement = message.Entitlement;
        }
    }

    /// <summary>
    /// Snapshot of a <c>BuildingUpgradeMessage</c> suitable for recording and replay.
    /// </summary>
    public sealed class BuildingUpgradeRecord : IRecordedMessage
    {
        public const string Discriminator = "buildingUpgrade";

        public string ExpectedGameHash { get; init; } = string.Empty;

        /// <inheritdoc />
        public GameState ExpectedGameState { get; init; } = GameState.Uninitialized;
        public BuildingKey BuildingKey { get; init; } = default!;

        [JsonIgnore]
        public string RecordType => Discriminator;

        [JsonConstructor]
        public BuildingUpgradeRecord(string expectedGameHash, GameState expectedGameState, BuildingKey buildingKey)
        {
            ExpectedGameHash = expectedGameHash;
            ExpectedGameState = expectedGameState;
            BuildingKey = buildingKey;
        }

        public BuildingUpgradeRecord(GameModel gameModel, BuildingUpgradeMessage message)
        {
            ExpectedGameHash = gameModel.GameHash;
            ExpectedGameState = gameModel.GameState;
            BuildingKey = message.BuildingKey;
        }
    }

    /// <summary>
    /// Snapshot of a <c>RoadPurchaseMessage</c> suitable for recording and replay.
    /// </summary>
    public sealed class RoadPurchaseRecord : IRecordedMessage
    {
        public const string Discriminator = "roadPurchase";

        public string ExpectedGameHash { get; init; } = string.Empty;
        /// <inheritdoc />
        public GameState ExpectedGameState { get; init; } = GameState.Uninitialized;
        public RoadKey RoadKey { get; init; } = default!;

        [JsonIgnore]
        public string RecordType => Discriminator;

        [JsonConstructor]
        public RoadPurchaseRecord(string expectedGameHash, GameState expectedGameState, RoadKey roadKey)
        {
            ExpectedGameHash = expectedGameHash;
            ExpectedGameState = expectedGameState;
            RoadKey = roadKey;
        }

        public RoadPurchaseRecord(GameModel gameModel, RoadPurchaseMessage message)
        {
            ExpectedGameHash = gameModel.GameHash;
            ExpectedGameState = gameModel.GameState;
            RoadKey = message.RoadKey;
        }
    }

    /// <summary>
    /// Snapshot of a <c>MoveRobberMessage</c> suitable for recording and replay.
    /// </summary>
    public sealed class MoveRobberRecord : IRecordedMessage
    {
        public const string Discriminator = "moveRobber";

        public string ExpectedGameHash { get; init; } = string.Empty;
        /// <inheritdoc />
        public GameState ExpectedGameState { get; init; } = GameState.Uninitialized;
        public HexCoordinates Coordinates { get; init; } = default!;
        public string? TargetPlayerId { get; init; }

        [JsonIgnore]
        public string RecordType => Discriminator;

        [JsonConstructor]
        public MoveRobberRecord(string expectedGameHash, GameState expectedGameState, HexCoordinates coordinates, string? targetPlayerId)
        {
            ExpectedGameHash = expectedGameHash;
            ExpectedGameState = expectedGameState;
            Coordinates = coordinates;
            TargetPlayerId = targetPlayerId;
        }

        public MoveRobberRecord(GameModel gameModel, MoveRobberMessage message)
        {
            ExpectedGameHash = gameModel.GameHash;
            ExpectedGameState = gameModel.GameState;
            Coordinates = message.Coordinates;
            TargetPlayerId = message.TargetPlayerId;
        }
    }

    /// <summary>
    /// Snapshot of a <c>RollMessage</c> suitable for recording and replay.
    /// </summary>
    public sealed class RollRecord : IRecordedMessage
    {
        public const string Discriminator = "roll";

        public string ExpectedGameHash { get; init; } = string.Empty;
        /// <inheritdoc />
        public GameState ExpectedGameState { get; init; } = GameState.Uninitialized;
        public TurnRollModel Roll { get; init; } = default!;

        [JsonIgnore]
        public string RecordType => Discriminator;

        [JsonConstructor]
        public RollRecord(string expectedGameHash, GameState expectedGameState, TurnRollModel roll)
        {
            ExpectedGameHash = expectedGameHash;
            ExpectedGameState = expectedGameState;
            Roll = roll;
        }

        public RollRecord(GameModel gameModel, RollMessage message)
        {
            ExpectedGameHash = gameModel.GameHash;
            ExpectedGameState = gameModel.GameState;
            Roll = message.Roll;
        }
    }

    /// <summary>
    /// Snapshot of a <c>SetPlayerOrderMessage</c> suitable for recording and replay.
    /// </summary>
    public sealed class SetPlayerOrderRecord : IRecordedMessage
    {
        public const string Discriminator = "setPlayerOrder";

        public string ExpectedGameHash { get; init; } = string.Empty;
        /// <inheritdoc />
        public GameState ExpectedGameState { get; init; } = GameState.Uninitialized;
        public IList<string> PlayerIds { get; init; } = default!;

        [JsonIgnore]
        public string RecordType => Discriminator;

        [JsonConstructor]
        public SetPlayerOrderRecord(string expectedGameHash, GameState expectedGameState, IList<string> playerIds)
        {
            ExpectedGameHash = expectedGameHash;
            ExpectedGameState = expectedGameState;
            PlayerIds = playerIds;
        }

        public SetPlayerOrderRecord(GameModel gameModel, SetPlayerOrderMessage message)
        {
            ExpectedGameHash = gameModel.GameHash;
            ExpectedGameState = gameModel.GameState;
            PlayerIds = message.PlayerIds;
        }
    }

    /// <summary>
    /// Snapshot of a <c>GoFirstMessage</c> suitable for recording and replay.
    /// </summary>
    public sealed class GoFirstRecord : IRecordedMessage
    {
        public const string Discriminator = "goFirst";

        public string ExpectedGameHash { get; init; } = string.Empty;
        /// <inheritdoc />
        public GameState ExpectedGameState { get; init; } = GameState.Uninitialized;
        public string PlayerId { get; init; } = string.Empty;

        [JsonIgnore]
        public string RecordType => Discriminator;

        [JsonConstructor]
        public GoFirstRecord(string expectedGameHash, GameState expectedGameState, string playerId)
        {
            ExpectedGameHash = expectedGameHash;
            ExpectedGameState = expectedGameState;
            PlayerId = playerId;
        }

        public GoFirstRecord(GameModel gameModel, GoFirstMessage message)
        {
            ExpectedGameHash = gameModel.GameHash;
            ExpectedGameState = gameModel.GameState;
            PlayerId = message.PlayerId;
        }
    }

    /// <summary>
    /// Snapshot of a <c>ParticipatingInSupplementalMessage</c> suitable for recording and replay.
    /// </summary>
    public sealed class ParticipatingInSupplementalRecord : IRecordedMessage
    {
        public const string Discriminator = "participatingInSupplemental";

        public string ExpectedGameHash { get; init; } = string.Empty;
        /// <inheritdoc />
        public GameState ExpectedGameState { get; init; } = GameState.Uninitialized;
        public string PlayerId { get; init; } = string.Empty;
        public bool Participating { get; init; }

        [JsonIgnore]
        public string RecordType => Discriminator;

        [JsonConstructor]
        public ParticipatingInSupplementalRecord(string expectedGameHash, GameState expectedGameState, string playerId, bool participating)
        {
            ExpectedGameHash = expectedGameHash;
            ExpectedGameState = expectedGameState;
            PlayerId = playerId;
            Participating = participating;
        }

        public ParticipatingInSupplementalRecord(GameModel gameModel, ParticipatingInSupplementalMessage message)
        {
            ExpectedGameHash = gameModel.GameHash;
            ExpectedGameState = gameModel.GameState;
            PlayerId = message.PlayerId;
            Participating = message.Participating;
        }
    }

    /// <summary>
    /// Snapshot of a <c>BalanceBoardMessage</c> suitable for recording and replay.
    /// </summary>
    public sealed class BalanceBoardRecord : IRecordedMessage
    {
        public const string Discriminator = "balanceBoard";

        public string ExpectedGameHash { get; init; } = string.Empty;
        /// <inheritdoc />
        public GameState ExpectedGameState { get; init; } = GameState.Uninitialized;
        [JsonIgnore]
        public string RecordType => Discriminator;

        [JsonConstructor]
        public BalanceBoardRecord(string expectedGameHash, GameState expectedGameState)
        {
            ExpectedGameHash = expectedGameHash;
            ExpectedGameState = expectedGameState;
        }

        public BalanceBoardRecord(GameModel gameModel, BalanceBoardMessage message)
        {
            ExpectedGameHash = gameModel.GameHash;
            ExpectedGameState = gameModel.GameState;
        }
    }

    /// <summary>
    /// Snapshot of a <c>SwapTileResources</c> message suitable for recording and replay.
    /// </summary>
    public sealed class SwapTileResourcesRecord : IRecordedMessage
    {
        public const string Discriminator = "swapTileResources";

        public string ExpectedGameHash { get; init; } = string.Empty;
        /// <inheritdoc />
        public GameState ExpectedGameState { get; init; } = GameState.Uninitialized;
        public HexCoordinates SourceTileCoordinates { get; init; } = default!;
        public HexCoordinates DestinationTileCoordinates { get; init; } = default!;
        public ResourceType SourceCurrentResource { get; init; }
        public ResourceType DestinationCurrentResource { get; init; }

        [JsonIgnore]
        public string RecordType => Discriminator;

        [JsonConstructor]
        public SwapTileResourcesRecord(string expectedGameHash, GameState expectedGameState, HexCoordinates sourceTileCoordinates, HexCoordinates destinationTileCoordinates, ResourceType sourceCurrentResource, ResourceType destinationCurrentResource)
        {
            ExpectedGameHash = expectedGameHash;
            ExpectedGameState = expectedGameState;
            SourceTileCoordinates = sourceTileCoordinates;
            DestinationTileCoordinates = destinationTileCoordinates;
            SourceCurrentResource = sourceCurrentResource;
            DestinationCurrentResource = destinationCurrentResource;
        }

        public SwapTileResourcesRecord(GameModel gameModel, SwapTileResources message)
        {
            ExpectedGameHash = gameModel.GameHash;
            ExpectedGameState = gameModel.GameState;
            SourceTileCoordinates = message.SourceTileCoordinates;
            DestinationTileCoordinates = message.DestinationTileCoordinates;
            SourceCurrentResource = message.SourceCurrentResource;
            DestinationCurrentResource = message.DestinationCurrentResource;
        }
    }

    /// <summary>
    /// Builders that convert live MVVM messages into their recorded snapshots.
    /// Used while the game runs to append to a recording.
    /// </summary>
    public static class MessageConverters
    {
        /// <summary>
        /// Capture an <see cref="UndoMessage"/> as an <see cref="UndoRecord"/>.
        /// </summary>
        public static IRecordedMessage ToRecord(this UndoMessage msg, GameModel gameModel)
            => new UndoRecord(gameModel, msg);

        /// <summary>
        /// Capture a <see cref="RedoMessage"/> as a <see cref="RedoRecord"/>.
        /// </summary>
        public static IRecordedMessage ToRecord(this RedoMessage msg, GameModel gameModel)
            => new RedoRecord(gameModel, msg);

        /// <summary>
        /// Capture a <see cref="NextMessage"/> as a <see cref="NextRecord"/>.
        /// </summary>
        public static IRecordedMessage ToRecord(this NextMessage msg, GameModel gameModel)
            => new NextRecord(gameModel, msg);

        /// <summary>
        /// Capture a <see cref="ShuffleMessage"/> as a <see cref="ShuffleRecord"/>.
        /// </summary>
        public static IRecordedMessage ToRecord(this ShuffleMessage msg, GameModel gameModel)
            => new ShuffleRecord(gameModel, msg);

        /// <summary>
        /// Capture a <see cref="PurchaseMessage"/> as a <see cref="PurchaseRecord"/>.
        /// </summary>
        public static IRecordedMessage ToRecord(this PurchaseMessage msg, GameModel gameModel)
            => new PurchaseRecord(gameModel, msg);

        /// <summary>
        /// Capture a <see cref="BuildingUpgradeMessage"/> as a <see cref="BuildingUpgradeRecord"/>.
        /// </summary>
        public static IRecordedMessage ToRecord(this BuildingUpgradeMessage msg, GameModel gameModel)
            => new BuildingUpgradeRecord(gameModel, msg);

        /// <summary>
        /// Capture a <see cref="RoadPurchaseMessage"/> as a <see cref="RoadPurchaseRecord"/>.
        /// </summary>
        public static IRecordedMessage ToRecord(this RoadPurchaseMessage msg, GameModel gameModel)
            => new RoadPurchaseRecord(gameModel, msg);

        /// <summary>
        /// Capture a <see cref="MoveRobberMessage"/> as a <see cref="MoveRobberRecord"/>.
        /// </summary>
        public static IRecordedMessage ToRecord(this MoveRobberMessage msg, GameModel gameModel)
            => new MoveRobberRecord(gameModel, msg);

        /// <summary>
        /// Capture a <see cref="RollMessage"/> as a <see cref="RollRecord"/>.
        /// </summary>
        public static IRecordedMessage ToRecord(this RollMessage msg, GameModel gameModel)
            => new RollRecord(gameModel, msg);

        /// <summary>
        /// Capture a <see cref="SetPlayerOrderMessage"/> as a <see cref="SetPlayerOrderRecord"/>.
        /// </summary>
        public static IRecordedMessage ToRecord(this SetPlayerOrderMessage msg, GameModel gameModel)
            => new SetPlayerOrderRecord(gameModel, msg);

        /// <summary>
        /// Capture a <see cref="GoFirstMessage"/> as a <see cref="GoFirstRecord"/>.
        /// </summary>
        public static IRecordedMessage ToRecord(this GoFirstMessage msg, GameModel gameModel)
            => new GoFirstRecord(gameModel, msg);

        /// <summary>
        /// Capture a <see cref="ParticipatingInSupplementalMessage"/> as a <see cref="ParticipatingInSupplementalRecord"/>.
        /// </summary>
        public static IRecordedMessage ToRecord(this ParticipatingInSupplementalMessage msg, GameModel gameModel)
            => new ParticipatingInSupplementalRecord(gameModel, msg);

        /// <summary>
        /// Capture a <see cref="BalanceBoardMessage"/> as a <see cref="BalanceBoardRecord"/>.
        /// </summary>
        public static IRecordedMessage ToRecord(this BalanceBoardMessage msg, GameModel gameModel)
            => new BalanceBoardRecord(gameModel, msg);

        /// <summary>
        /// Capture a <see cref="SwapTileResources"/> message as a <see cref="SwapTileResourcesRecord"/>.
        /// </summary>
        public static IRecordedMessage ToRecord(this SwapTileResources msg, GameModel gameModel)
            => new SwapTileResourcesRecord(gameModel, msg);
    }



}

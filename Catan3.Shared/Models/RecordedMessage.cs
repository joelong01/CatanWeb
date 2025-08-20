using System;
using System.Collections.Generic;
using System.Text.Json;
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
    [JsonDerivedType(typeof(ExecuteGameActionRecord), ExecuteGameActionRecord.Discriminator)]
    [JsonDerivedType(typeof(ShuffleRecord), ShuffleRecord.Discriminator)]
    [JsonDerivedType(typeof(PurchaseRecord), PurchaseRecord.Discriminator)]
    [JsonDerivedType(typeof(BuildingUpgradeRecord), BuildingUpgradeRecord.Discriminator)]
    [JsonDerivedType(typeof(RoadPurchaseRecord), RoadPurchaseRecord.Discriminator)]
    [JsonDerivedType(typeof(MoveRobberRecord), MoveRobberRecord.Discriminator)]
    [JsonDerivedType(typeof(RollRecord), RollRecord.Discriminator)]
    [JsonDerivedType(typeof(SetPlayerOrderRecord), SetPlayerOrderRecord.Discriminator)]
    [JsonDerivedType(typeof(GoFirstRecord), GoFirstRecord.Discriminator)]
    [JsonDerivedType(typeof(PlayersDoingSupplementalRecord), PlayersDoingSupplementalRecord.Discriminator)]
    [JsonDerivedType(typeof(BalanceBoardRecord), BalanceBoardRecord.Discriminator)]
    public interface IRecordedMessage
    {
        /// <summary>
        /// Stable identifier for the game state associated with this record.
        /// </summary>
        string GameHash { get; }

        /// <summary>
        /// The discriminator for this record as it appears in JSON under "type".
        /// This is provided for convenient branching during replay.
        /// </summary>
        string RecordType { get; }
    }

    /// <summary>
    /// Snapshot of an <c>ExecuteGameActionMessage</c> suitable for recording and replay.
    /// </summary>
    public sealed class ExecuteGameActionRecord : IRecordedMessage
    {
        /// <summary>
        /// Discriminator value written to/expected from JSON: <c>"executeGameActionRecord"</c>.
        /// </summary>
        public const string Discriminator = "executeGameActionRecord";

        /// <inheritdoc />
        public string GameHash { get; init; } = string.Empty;

        /// <summary>
        /// The action that was executed.
        /// </summary>
        public GameAction Action { get; init; } = default!;

        /// <inheritdoc />
        [JsonIgnore]
        public string RecordType => Discriminator;

        /// <summary>
        /// Constructor used during deserialization and for programmatic creation.
        /// </summary>
        [JsonConstructor]
        public ExecuteGameActionRecord(string gameHash, GameAction action)
        {
            GameHash = gameHash;
            Action = action;
        }

        /// <summary>
        /// Convenience constructor to capture an <see cref="ExecuteGameActionMessage"/> at runtime.
        /// </summary>
        public ExecuteGameActionRecord(string gameHash, ExecuteGameActionMessage message)
        {
            GameHash = gameHash;
            Action = message.Action;
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
        public string GameHash { get; init; } = string.Empty;

        /// <summary>
        /// The seed used for deterministic randomization.
        /// </summary>
        public int Seed { get; init; }

        /// <inheritdoc />
        [JsonIgnore]
        public string RecordType => Discriminator;

        /// <summary>
        /// Constructor used during deserialization and for programmatic creation.
        /// </summary>
        [JsonConstructor]
        public ShuffleRecord(string gameHash, int seed)
        {
            GameHash = gameHash;
            Seed = seed;
        }

        /// <summary>
        /// Convenience constructor to capture a <see cref="ShuffleMessage"/> at runtime.
        /// </summary>
        public ShuffleRecord(string gameHash, ShuffleMessage message)
        {
            GameHash = gameHash;
            Seed = message.Seed;
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
        public string GameHash { get; init; } = string.Empty;

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
        public PurchaseRecord(string gameHash, Entitlement entitlement)
        {
            GameHash = gameHash;
            Entitlement = entitlement;
        }

        /// <summary>
        /// Convenience constructor to capture a <see cref="PurchaseMessage"/> at runtime.
        /// </summary>
        public PurchaseRecord(string gameHash, PurchaseMessage message)
        {
            GameHash = gameHash;
            Entitlement = message.Entitlement;
        }
    }

    /// <summary>
    /// Snapshot of a <c>BuildingUpgradeMessage</c> suitable for recording and replay.
    /// </summary>
    public sealed class BuildingUpgradeRecord : IRecordedMessage
    {
        public const string Discriminator = "buildingUpgrade";

        public string GameHash { get; init; } = string.Empty;
        public BuildingKey BuildingKey { get; init; } = default!;

        [JsonIgnore]
        public string RecordType => Discriminator;

        [JsonConstructor]
        public BuildingUpgradeRecord(string gameHash, BuildingKey buildingKey)
        {
            GameHash = gameHash;
            BuildingKey = buildingKey;
        }

        public BuildingUpgradeRecord(string gameHash, BuildingUpgradeMessage message)
        {
            GameHash = gameHash;
            BuildingKey = message.BuildingKey;
        }
    }

    /// <summary>
    /// Snapshot of a <c>RoadPurchaseMessage</c> suitable for recording and replay.
    /// </summary>
    public sealed class RoadPurchaseRecord : IRecordedMessage
    {
        public const string Discriminator = "roadPurchase";

        public string GameHash { get; init; } = string.Empty;
        public RoadKey RoadKey { get; init; } = default!;

        [JsonIgnore]
        public string RecordType => Discriminator;

        [JsonConstructor]
        public RoadPurchaseRecord(string gameHash, RoadKey roadKey)
        {
            GameHash = gameHash;
            RoadKey = roadKey;
        }

        public RoadPurchaseRecord(string gameHash, RoadPurchaseMessage message)
        {
            GameHash = gameHash;
            RoadKey = message.RoadKey;
        }
    }

    /// <summary>
    /// Snapshot of a <c>MoveRobberMessage</c> suitable for recording and replay.
    /// </summary>
    public sealed class MoveRobberRecord : IRecordedMessage
    {
        public const string Discriminator = "moveRobber";

        public string GameHash { get; init; } = string.Empty;
        public HexCoordinates Coordinates { get; init; } = default!;
        public string? TargetPlayerId { get; init; }

        [JsonIgnore]
        public string RecordType => Discriminator;

        [JsonConstructor]
        public MoveRobberRecord(string gameHash, HexCoordinates coordinates, string? targetPlayerId)
        {
            GameHash = gameHash;
            Coordinates = coordinates;
            TargetPlayerId = targetPlayerId;
        }

        public MoveRobberRecord(string gameHash, MoveRobberMessage message)
        {
            GameHash = gameHash;
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

        public string GameHash { get; init; } = string.Empty;
        public TurnRollModel Roll { get; init; } = default!;

        [JsonIgnore]
        public string RecordType => Discriminator;

        [JsonConstructor]
        public RollRecord(string gameHash, TurnRollModel roll)
        {
            GameHash = gameHash;
            Roll = roll;
        }

        public RollRecord(string gameHash, RollMessage message)
        {
            GameHash = gameHash;
            Roll = message.Roll;
        }
    }

    /// <summary>
    /// Snapshot of a <c>SetPlayerOrderMessage</c> suitable for recording and replay.
    /// </summary>
    public sealed class SetPlayerOrderRecord : IRecordedMessage
    {
        public const string Discriminator = "setPlayerOrder";

        public string GameHash { get; init; } = string.Empty;
        public IList<string> PlayerIds { get; init; } = default!;

        [JsonIgnore]
        public string RecordType => Discriminator;

        [JsonConstructor]
        public SetPlayerOrderRecord(string gameHash, IList<string> playerIds)
        {
            GameHash = gameHash;
            PlayerIds = playerIds;
        }

        public SetPlayerOrderRecord(string gameHash, SetPlayerOrderMessage message)
        {
            GameHash = gameHash;
            PlayerIds = message.PlayerIds;
        }
    }

    /// <summary>
    /// Snapshot of a <c>GoFirstMessage</c> suitable for recording and replay.
    /// </summary>
    public sealed class GoFirstRecord : IRecordedMessage
    {
        public const string Discriminator = "goFirst";

        public string GameHash { get; init; } = string.Empty;
        public string PlayerId { get; init; } = string.Empty;

        [JsonIgnore]
        public string RecordType => Discriminator;

        [JsonConstructor]
        public GoFirstRecord(string gameHash, string playerId)
        {
            GameHash = gameHash;
            PlayerId = playerId;
        }

        public GoFirstRecord(string gameHash, GoFirstMessage message)
        {
            GameHash = gameHash;
            PlayerId = message.PlayerId;
        }
    }

    /// <summary>
    /// Snapshot of a <c>PlayersDoingSupplemental</c> suitable for recording and replay.
    /// </summary>
    public sealed class PlayersDoingSupplementalRecord : IRecordedMessage
    {
        public const string Discriminator = "playersDoingSupplemental";

        public string GameHash { get; init; } = string.Empty;
        public IList<string> PlayerIds { get; init; } = default!;

        [JsonIgnore]
        public string RecordType => Discriminator;

        [JsonConstructor]
        public PlayersDoingSupplementalRecord(string gameHash, IList<string> playerIds)
        {
            GameHash = gameHash;
            PlayerIds = playerIds;
        }

        public PlayersDoingSupplementalRecord(string gameHash, PlayersDoingSupplemental message)
        {
            GameHash = gameHash;
            PlayerIds = message.PlayerIds;
        }
    }

    /// <summary>
    /// Snapshot of a <c>BalanceBoardMessage</c> suitable for recording and replay.
    /// </summary>
    public sealed class BalanceBoardRecord : IRecordedMessage
    {
        public const string Discriminator = "balanceBoard";

        public string GameHash { get; init; } = string.Empty;

        [JsonIgnore]
        public string RecordType => Discriminator;

        [JsonConstructor]
        public BalanceBoardRecord(string gameHash)
        {
            GameHash = gameHash;
        }

        public BalanceBoardRecord(string gameHash, BalanceBoardMessage message)
        {
            GameHash = gameHash;
        }
    }

    /// <summary>
    /// Builders that convert live MVVM messages into their recorded snapshots.
    /// Used while the game runs to append to a recording.
    /// </summary>
    public static class MessageConverters
    {
        /// <summary>
        /// Capture an <see cref="ExecuteGameActionMessage"/> as an <see cref="ExecuteGameActionRecord"/>.
        /// </summary>
        public static IRecordedMessage ToRecord(this ExecuteGameActionMessage msg, string gameHash)
            => new ExecuteGameActionRecord(gameHash, msg);

        /// <summary>
        /// Capture a <see cref="ShuffleMessage"/> as a <see cref="ShuffleRecord"/>.
        /// </summary>
        public static IRecordedMessage ToRecord(this ShuffleMessage msg, string gameHash)
            => new ShuffleRecord(gameHash, msg);

        /// <summary>
        /// Capture a <see cref="PurchaseMessage"/> as a <see cref="PurchaseRecord"/>.
        /// </summary>
        public static IRecordedMessage ToRecord(this PurchaseMessage msg, string gameHash)
            => new PurchaseRecord(gameHash, msg);

        /// <summary>
        /// Capture a <see cref="BuildingUpgradeMessage"/> as a <see cref="BuildingUpgradeRecord"/>.
        /// </summary>
        public static IRecordedMessage ToRecord(this BuildingUpgradeMessage msg, string gameHash)
            => new BuildingUpgradeRecord(gameHash, msg);

        /// <summary>
        /// Capture a <see cref="RoadPurchaseMessage"/> as a <see cref="RoadPurchaseRecord"/>.
        /// </summary>
        public static IRecordedMessage ToRecord(this RoadPurchaseMessage msg, string gameHash)
            => new RoadPurchaseRecord(gameHash, msg);

        /// <summary>
        /// Capture a <see cref="MoveRobberMessage"/> as a <see cref="MoveRobberRecord"/>.
        /// </summary>
        public static IRecordedMessage ToRecord(this MoveRobberMessage msg, string gameHash)
            => new MoveRobberRecord(gameHash, msg);

        /// <summary>
        /// Capture a <see cref="RollMessage"/> as a <see cref="RollRecord"/>.
        /// </summary>
        public static IRecordedMessage ToRecord(this RollMessage msg, string gameHash)
            => new RollRecord(gameHash, msg);

        /// <summary>
        /// Capture a <see cref="SetPlayerOrderMessage"/> as a <see cref="SetPlayerOrderRecord"/>.
        /// </summary>
        public static IRecordedMessage ToRecord(this SetPlayerOrderMessage msg, string gameHash)
            => new SetPlayerOrderRecord(gameHash, msg);

        /// <summary>
        /// Capture a <see cref="GoFirstMessage"/> as a <see cref="GoFirstRecord"/>.
        /// </summary>
        public static IRecordedMessage ToRecord(this GoFirstMessage msg, string gameHash)
            => new GoFirstRecord(gameHash, msg);

        /// <summary>
        /// Capture a <see cref="PlayersDoingSupplemental"/> as a <see cref="PlayersDoingSupplementalRecord"/>.
        /// </summary>
        public static IRecordedMessage ToRecord(this PlayersDoingSupplemental msg, string gameHash)
            => new PlayersDoingSupplementalRecord(gameHash, msg);

        /// <summary>
        /// Capture a <see cref="BalanceBoardMessage"/> as a <see cref="BalanceBoardRecord"/>.
        /// </summary>
        public static IRecordedMessage ToRecord(this BalanceBoardMessage msg, string gameHash)
            => new BalanceBoardRecord(gameHash, msg);
    }

    /// <summary>
    /// Helpers for working with heterogeneous recordings during replay.
    /// </summary>
    public static class RecordedMessageReplay
    {
        /// <summary>
        /// Downcast to a specific record type, throwing a clear exception on mismatch.
        /// Useful when branching by <see cref="IRecordedMessage.RecordType"/>.
        /// </summary>
        public static T As<T>(this IRecordedMessage msg) where T : class, IRecordedMessage =>
            msg as T ?? throw new InvalidCastException(
                $"Expected {typeof(T).Name} but got {msg.GetType().Name}");

        /// <summary>
        /// Pattern-match and invoke the appropriate callback for the underlying record.
        /// </summary>
        public static void Match(
            this IRecordedMessage msg,
            Action<ExecuteGameActionRecord>? onExecute = null,
            Action<ShuffleRecord>? onShuffle = null,
            Action<PurchaseRecord>? onPurchase = null,
            Action<BuildingUpgradeRecord>? onBuildingUpgrade = null,
            Action<RoadPurchaseRecord>? onRoadPurchase = null,
            Action<MoveRobberRecord>? onMoveRobber = null,
            Action<RollRecord>? onRoll = null,
            Action<SetPlayerOrderRecord>? onSetPlayerOrder = null,
            Action<GoFirstRecord>? onGoFirst = null,
            Action<PlayersDoingSupplementalRecord>? onPlayersDoingSupplemental = null,
            Action<BalanceBoardRecord>? onBalanceBoard = null,
            Action<IRecordedMessage>? onUnknown = null)
        {
            switch (msg)
            {
                case ExecuteGameActionRecord e: onExecute?.Invoke(e); break;
                case ShuffleRecord s: onShuffle?.Invoke(s); break;
                case PurchaseRecord p: onPurchase?.Invoke(p); break;
                case BuildingUpgradeRecord b: onBuildingUpgrade?.Invoke(b); break;
                case RoadPurchaseRecord r: onRoadPurchase?.Invoke(r); break;
                case MoveRobberRecord m: onMoveRobber?.Invoke(m); break;
                case RollRecord ro: onRoll?.Invoke(ro); break;
                case SetPlayerOrderRecord so: onSetPlayerOrder?.Invoke(so); break;
                case GoFirstRecord g: onGoFirst?.Invoke(g); break;
                case PlayersDoingSupplementalRecord ps: onPlayersDoingSupplemental?.Invoke(ps); break;
                case BalanceBoardRecord bb: onBalanceBoard?.Invoke(bb); break;
                default: onUnknown?.Invoke(msg); break;
            }
        }
    }

}

using System.Runtime.CompilerServices;
using Catan3.Shared.Utility;

namespace Catan3.Shared.Models
{
    /// <summary>
    /// Wrapper for SignalR messages that contains MVVM message as JSON string.
    /// Solves polymorphic deserialization issues in SignalR Hub methods.
    /// </summary>
    public class SignalRMessage
    {
        /// <summary>
        /// The type name of the message (e.g., "ShuffleMessage", "NextMessage")
        /// </summary>
        public string MessageType { get; set; } = string.Empty;
        
        /// <summary>
        /// The MVVM message serialized as JSON string using JsonHelper.StandardOptions
        /// </summary>
        public string MessageJson { get; set; } = string.Empty;
        
        /// <summary>
        /// Creates a SignalR message wrapper from an MVVM message object
        /// </summary>
        public static SignalRMessage Create<T>(T message)
        {
            return new SignalRMessage
            {
                MessageType = typeof(T).Name,
                MessageJson = JsonHelper.Serialize(message)
            };
        }
        
        /// <summary>
        /// Deserializes the JSON back to the original message type
        /// </summary>
        public T Deserialize<T>()
        {
            return JsonHelper.Deserialize<T>(MessageJson) ?? throw new InvalidOperationException($"Failed to deserialize {MessageType}");
        }
        
        /// <summary>
        /// Deserializes the JSON back to a specific type
        /// </summary>
        public object Deserialize(Type messageType)
        {
            return JsonHelper.Deserialize(MessageJson, messageType) ?? throw new InvalidOperationException($"Failed to deserialize {MessageType}");
        }
    }

    // Core message types needed for the web API
    public class UndoMessage
    {
        public override string ToString() => "UndoMessage";
    }

    public class RedoMessage
    {
        public override string ToString() => "RedoMessage";
    }

    public class NextMessage
    {
        public override string ToString() => "NextMessage";
    }

    public class PurchaseMessage(Entitlement entitlement)
    {
        public Entitlement Entitlement { get; } = entitlement;
        public override string ToString() => $"PurchaseMessage: {Entitlement}";
    }

    public class RoadPurchaseMessage(RoadKey roadKey)
    {
        public RoadKey RoadKey { get; } = roadKey;
        public override string ToString() => $"RoadPurchaseMessage: {RoadKey}";
    }

    public class BuildingUpgradeMessage(BuildingKey buildingKey)
    {
        public BuildingKey BuildingKey { get; } = buildingKey;
        public override string ToString() => $"BuildingUpgradeMessage: {BuildingKey}";
    }

    public class MoveRobberMessage(HexCoordinates coordinates, string? targetPlayerId)
    {
        public HexCoordinates Coordinates { get; } = coordinates;
        public string? TargetPlayerId { get; } = targetPlayerId;
        public override string ToString() => $"MoveRobberMessage: {Coordinates}, Target: {TargetPlayerId ?? "None"}";
    }

    public class RollMessage(TurnRollModel Roll)
    {
        public TurnRollModel Roll { get; } = Roll;
        public override string ToString() => $"RollMessage: {Roll}";
    }

    public class ShuffleMessage()
    {
        public override string ToString() => $"ShuffleMessage";
    }

    public class NewGameMessage(GameType GameType, IList<string> PlayerIds, string GameName)
    {
        public GameType GameType { get; } = GameType;
        public IList<string> PlayerIds { get; set; } = PlayerIds;
        public string GameName { get; } = GameName;
        public override string ToString() => $"NewGameMessage: {GameName} ({GameType}), Players: {PlayerIds.Count}";
    }

    public class SetPlayerOrderMessage(IList<string> playerIds)
    {
        public IList<string> PlayerIds { get; set; } = playerIds;
        public override string ToString() => $"SetPlayerOrderMessage: {PlayerIds.Count} players";
    }

    public class UpdateGameModel(GameModel gameModel)
    {
        public GameModel GameModel { get; set; } = gameModel;
        public override string ToString() => $"UpdateGameModel: {GameModel?.GameState}, Players: {GameModel?.Players?.Count ?? 0}";
    }

    public class ParticipatingInSupplementalMessage(string playerId, bool participating)
    {
        public string PlayerId { get; } = playerId;
        public bool Participating { get; } = participating;
        public override string ToString() => $"ParticipatingInSupplementalMessage: {PlayerId} -> {(Participating ? "Yes" : "No")}";
    }

    public class GoFirstMessage(string playerId)
    {
        public string PlayerId { get; } = playerId;
        public override string ToString() => $"GoFirstMessage: {PlayerId}";
    }

    public class BalanceBoardMessage
    {
        public override string ToString() => "BalanceBoardMessage";
    }

    public class EndGame
    {
        public override string ToString() => "EndGame";
    }

    public class ErrorMessage(string Message, ErrorLevel ErrorLevel, [CallerMemberName] string CallerMemberName = "", [CallerLineNumber] int CallerLineNumber = 0, [CallerFilePath] string CallerFilePath = "")
    {
        public string Message { get; } = Message;
        public string CallerMemberName { get; } = CallerMemberName;
        public int CallerLineNumber { get; } = CallerLineNumber;
        public string CallerFilePath { get; } = CallerFilePath;
        public ErrorLevel ErrorLevel { get; } = ErrorLevel;
        public override string ToString() => $"ErrorMessage [{ErrorLevel}]: {Message} ({CallerMemberName}:{CallerLineNumber})";
    }

    public class PersistGameMessage(LocalPersistActions action, string location)
    {
        public LocalPersistActions Action { get; set; } = action;
        public string Location { get; set; } = location;
        public override string ToString() => $"PersistGameMessage: {Action} -> {Location}";
    }

    /// <summary>
    /// Shared message for loading games from compressed log data (.catan files).
    /// Contains Base64-encoded compressed SerializableLog data.
    /// Used by GameService APIs to load production game files.
    /// </summary>
    public class LoadGameMessage(string compressedLog)
    {
        public string CompressedLog { get; } = compressedLog;
        public override string ToString() => $"LoadGameMessage: {CompressedLog.Length} chars";
    }

    /// <summary>
    /// Shared message for loading games from deserialized GameModel (.catan_test files).
    /// Used by GameService APIs and tests to load game state directly.
    /// </summary>
    public class LoadGameModelMessage
    {
        public string GameModelJson { get; set; } = string.Empty;
        public bool IsTest { get; set; } = false;
        
        public LoadGameModelMessage() { }
        
        public LoadGameModelMessage(string gameModelJson, bool isTest = false)
        {
            GameModelJson = gameModelJson;
            IsTest = isTest;
        }
        
        public override string ToString() => $"LoadGameModelMessage: {GameModelJson.Length} characters";
    }

    public class StartRecordingMessage(string? outputPath = null)
    {
        public string? OutputPath { get; } = outputPath;
        public override string ToString() => $"StartRecordingMessage: {OutputPath ?? "default path"}";
    }

    public class StopRecordingMessage
    {
        public override string ToString() => "StopRecordingMessage";
    }

    public class UpdateSettings(SettingsModel settings)
    {
        public SettingsModel Settings { get; } = settings;
        public override string ToString() => $"UpdateSettings: {Settings.Settings.Count} settings";
    }
}
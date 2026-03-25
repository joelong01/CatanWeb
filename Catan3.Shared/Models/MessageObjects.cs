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

    public class UpdateHouseRulesMessage(HouseRules houseRules)
    {
        public HouseRules HouseRules { get; } = houseRules;
        public override string ToString() => $"UpdateHouseRulesMessage: GoldTiles={HouseRules.GoldTiles}, SupplementalMinPlayers={HouseRules.SupplementalMinPlayers}";
    }

    public class NewGameMessage(GameType GameType, IList<string> PlayerIds, string GameName, HouseRules? HouseRules = null, bool SaveLifetimeStats = true, string? TemplateId = null, bool RecordGame = false)
    {
        public GameType GameType { get; } = GameType;
        public IList<string> PlayerIds { get; set; } = PlayerIds;
        public string GameName { get; } = GameName;
        /// <summary>
        /// Optional house rules to override defaults. If null, uses default house rules for the game type.
        /// </summary>
        public HouseRules? HouseRules { get; set; } = HouseRules;
        /// <summary>
        /// Whether to save lifetime statistics when the game ends.
        /// Default is true. Set to false for test games to avoid polluting stats.
        /// </summary>
        public bool SaveLifetimeStats { get; set; } = SaveLifetimeStats;
        /// <summary>
        /// Optional template ID. If provided, loads the board configuration from the database.
        /// If null, falls back to GameType-based template lookup.
        /// </summary>
        public string? TemplateId { get; set; } = TemplateId;
        /// <summary>
        /// Whether to record the game for later replay.
        /// </summary>
        public bool RecordGame { get; set; } = RecordGame;
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

    /// <summary>
    /// Message to declare a player as the winner and transition to GameOver state.
    /// Includes Victory Point card counts for all players who have dev cards.
    /// </summary>
    public class DeclareWinnerMessage
    {
        /// <summary>
        /// The player ID of the winner (must be current player per Catan rules).
        /// </summary>
        public string WinnerId { get; set; } = string.Empty;

        /// <summary>
        /// Dictionary mapping player IDs to their Victory Point card counts.
        /// Only includes players who have dev cards.
        /// </summary>
        public Dictionary<string, int> VictoryPoints { get; set; } = new();

        /// <summary>
        /// Default constructor for deserialization.
        /// </summary>
        public DeclareWinnerMessage() { }

        /// <summary>
        /// Creates a DeclareWinnerMessage with just the winner ID (no VP cards).
        /// </summary>
        public DeclareWinnerMessage(string winnerId)
        {
            WinnerId = winnerId;
        }

        /// <summary>
        /// Creates a DeclareWinnerMessage with winner ID and VP card counts.
        /// </summary>
        public DeclareWinnerMessage(string winnerId, Dictionary<string, int> victoryPoints)
        {
            WinnerId = winnerId;
            VictoryPoints = victoryPoints;
        }

        public override string ToString()
        {
            if (VictoryPoints.Count == 0)
                return $"DeclareWinnerMessage: {WinnerId}";
            var vpEntries = string.Join(", ", VictoryPoints.Select(kv => $"{kv.Key}={kv.Value}"));
            return $"DeclareWinnerMessage: {WinnerId} [VP: {vpEntries}]";
        }
    }

    /// <summary>
    /// Request body for the POST /api/game/{gameId}/winner endpoint.
    /// </summary>
    public class DeclareWinnerRequest
    {
        /// <summary>
        /// The player ID to declare as winner.
        /// </summary>
        public string WinnerId { get; set; } = string.Empty;

        /// <summary>
        /// Dictionary mapping player IDs to their Victory Point card counts.
        /// Only includes players who have dev cards.
        /// </summary>
        public Dictionary<string, int> VictoryPoints { get; set; } = new();
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

    /// <summary>
    /// MVVM message to request current settings
    /// </summary>
    public class GetSettingsMessage
    {
        public override string ToString() => "GetSettingsMessage";
    }

    /// <summary>
    /// MVVM Messenger message to highlight a specific tile during drag-and-drop operations.
    /// This is a client-side only message (not sent to server).
    /// When TargetTileCoordinates is null, all highlighting is cleared.
    /// </summary>
    public class HighlightTile
    {
        /// <summary>
        /// The coordinates of the tile to highlight.
        /// Set to null to clear all highlighting.
        /// </summary>
        public HexCoordinates? TargetTileCoordinates { get; set; }

        /// <summary>
        /// Creates a new HighlightTile message.
        /// </summary>
        /// <param name="targetTileCoordinates">The tile to highlight, or null to clear highlighting</param>
        public HighlightTile(HexCoordinates? targetTileCoordinates)
        {
            TargetTileCoordinates = targetTileCoordinates;
        }

        public override string ToString() => $"HighlightTile: {TargetTileCoordinates?.ToString() ?? "None"}";
    }

    /// <summary>
    /// MVVM Messenger message to swap resource types between two tiles.
    /// This message is sent from the client during drag-and-drop, routed through GameMessageService
    /// to the GameStateMachine (local or remote via GameService), and processed as a game action.
    /// 
    /// The message includes both current resource types for validation:
    /// - Prevents race conditions if resources change during drag
    /// - Server can verify the swap is valid before executing
    /// </summary>
    public class SwapTileResources
    {
        /// <summary>
        /// Hex coordinates of the source tile (the one being dragged from)
        /// </summary>
        public HexCoordinates SourceTileCoordinates { get; set; }

        /// <summary>
        /// Hex coordinates of the destination tile (the one being dropped onto)
        /// </summary>
        public HexCoordinates DestinationTileCoordinates { get; set; }

        /// <summary>
        /// The resource type currently on the source tile (for validation)
        /// </summary>
        public ResourceType SourceCurrentResource { get; set; }

        /// <summary>
        /// The resource type currently on the destination tile (for validation)
        /// </summary>
        public ResourceType DestinationCurrentResource { get; set; }

        /// <summary>
        /// Creates a new SwapTileResources message.
        /// </summary>
        /// <param name="sourceTileCoordinates">Hex coordinates of source tile</param>
        /// <param name="destinationTileCoordinates">Hex coordinates of destination tile</param>
        /// <param name="sourceCurrentResource">Current resource type on source tile</param>
        /// <param name="destinationCurrentResource">Current resource type on destination tile</param>
        public SwapTileResources(
            HexCoordinates sourceTileCoordinates,
            HexCoordinates destinationTileCoordinates,
            ResourceType sourceCurrentResource,
            ResourceType destinationCurrentResource)
        {
            SourceTileCoordinates = sourceTileCoordinates;
            DestinationTileCoordinates = destinationTileCoordinates;
            SourceCurrentResource = sourceCurrentResource;
            DestinationCurrentResource = destinationCurrentResource;
        }

        public override string ToString() => $"SwapTileResources: {SourceTileCoordinates} <-> {DestinationTileCoordinates}";
    }
}
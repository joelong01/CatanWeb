using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Catan3.Shared.Utility;

namespace Catan3.Shared.Models
{
    // Core message types needed for the web API
    public class ExecuteGameActionMessage(GameAction action)
    {
        public GameAction Action { get; } = action;
        public override string ToString() => $"ExecuteGameActionMessage: {Action}";
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

    public class NewGameMessage(GameType GameType, IList<string> PlayerIds)
    {
        public GameType GameType { get; } = GameType;
        public IList<string> PlayerIds { get; set; } = PlayerIds;
        public override string ToString() => $"NewGameMessage: {GameType}, Players: {PlayerIds.Count}";
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

    public class LoadGameMessage(string localFile)
    {
        public string LocalFile { get; } = localFile;
        public override string ToString() => $"LoadGameMessage: {LocalFile}";
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
}
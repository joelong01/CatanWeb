using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Catan3.Shared.Utility;

namespace Catan3.Shared.Models
{
    // Core message types needed for the web API
    public class DoAction(GameAction action)
    {
        public GameAction Action { get; } = action;
    }

    public class PurchaseMessage(Entitlement entitlement)
    {
        public Entitlement Entitlement { get; } = entitlement;
    }

    public class RoadPurchaseMessage(RoadKey roadKey)
    {
        public RoadKey RoadKey { get; } = roadKey;
    }

    public class BuildingUpgradeMessage(BuildingKey buildingKey)
    {
        public BuildingKey BuildingKey { get; } = buildingKey;
    }

    public class MoveRobberMessage(HexCoordinates coordinates, string? targetPlayerId)
    {
        public HexCoordinates Coordinates { get; } = coordinates;
        public string? TargetPlayerId { get; } = targetPlayerId;
    }

    public class RollMessage(TurnRollModel roll)
    {
        public TurnRollModel Roll { get; } = roll;
    }

    public class NewGameMessage(GameType gameType, IList<string> playerIds)
    {
        public GameType GameType = gameType;
        public IList<string> PlayerIds { get; set; } = playerIds;
    }

    public class SetPlayerOrderMessage(IList<string> playerIds)
    {
        public IList<string> PlayerIds { get; set; } = playerIds;
    }

    public class UpdateGameModel(GameModel gameModel)
    {
        public GameModel GameModel { get; set; } = gameModel;
    }

    public class PlayersDoingSupplemental(IList<string> playerIds)
    {
        public IList<string> PlayerIds { get; } = playerIds;
    }

    public class GoFirstMessage(string playerId)
    {
        public string PlayerId { get; } = playerId;
    }

    public class BalanceBoardMessage
    {
    }

    public class EndGame
    {
    }

    public class ErrorMessage(string message, ErrorLevel errorLevel, [CallerMemberName] string cmb = "", [CallerLineNumber] int cln = 0, [CallerFilePath] string cfp = "")
    {
        public string Message { get; } = message;
        public string CallerMemberName { get; } = cmb;
        public string CallerLineNumber { get; } = cln.ToString();
        public string CallerFilePath { get; } = cfp;
        public ErrorLevel ErrorLevel { get; } = errorLevel;
    }

    public class PersistGameMessage(LocalPersistActions action, string location)
    {
        public LocalPersistActions Action { get; set; } = action;
        public string Location { get; set; } = location;
    }

    public class LoadGameMessage(string localFile)
    {
        public string LocalFile { get; } = localFile;
    }
}
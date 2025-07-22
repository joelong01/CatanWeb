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

    public class RoadPurchaseMessage(RoadKey key)
    {
        public RoadKey RoadKey { get; } = key;
    }

    public class BuildingUpgradeMessage(BuildingKey key)
    {
        public BuildingKey BuildingKey { get; } = key;
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

    public class NewGameMessage(GameType selectedGame, IList<string> playerIds)
    {
        public GameType GameType = selectedGame;
        public IList<string> PlayerIds { get; set; } = playerIds;
    }

    public class SetPlayerOrderMessage(IList<string> playerIds)
    {
        public IList<string> PlayerIds { get; set; } = playerIds;
    }

    // Placeholder for GameModel - will be added once we copy it
    public class GameModel
    {
        public string? CurrentPlayerId { get; set; }
        public GameState GameState { get; set; }
        public List<object> Players { get; set; } = new();
        public object? ActionFlags { get; set; }
    }

    public class UpdateGameModel(GameModel model)
    {
        public GameModel GameModel { get; set; } = model;
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
}
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Catan3.Utility;

namespace Catan3.Models

{ 
    public class SetPlayerOrderMessage(IList<string> playerIds)
    {
        public IList<string> PlayerIds { get; set; } = playerIds;
    }

    public class UpdateGameModel(GameModel model)
    {
        public GameModel GameModel { get; set; } = model;
    }

    public class NewGameMessage(GameType selectedGame, List<string> playerIds)
    {
        public GameType GameType= selectedGame;
        public List<string> PlayerIds { get; set; } = playerIds;
    }

    public class DoAction(GameAction action)
    {
        public GameAction Action { get; } = action;
    }


    public class RoadPurchaseMessage(RoadKey key)
    {
        public RoadKey RoadKey { get; } = key;
    }

    public class BuildingMouseEntered(BuildingViewModel buildingViewModel)
    {
        public BuildingViewModel BuildingViewModel { get; } = buildingViewModel;
    }

    public class BuildingMouseExit(BuildingViewModel buildingViewModel)
    {
        public BuildingViewModel BuildingViewModel { get; } = buildingViewModel;
    }
    public class BuildingUpgradeMessage(BuildingKey key)
    {
        public BuildingKey BuildingKey { get; } = key;
    }


    public class CurrentPlayerChanged(PlayerViewModel currentPlayer)
    {
        public PlayerViewModel CurrentPlayer { get; } = currentPlayer;
    }

    public class RequestTileOwners(TileViewModel tileViewModel)
    {
        public TileViewModel TileViewModel { get; } = tileViewModel;
    }

    public class TileOwnersResponse(IList<PlayerViewModel> players)
    {
        public IList<PlayerViewModel> Owners { get; } = players;
    }

    public class MoveRobberMessage(HexCoordinates coordinates, string targetPlayerId)
    {
        public HexCoordinates Coordinates { get; } = coordinates;
        public string TargetPlayerId { get; } = targetPlayerId;

    }

    public class UpdateOrientation(CatanOrientation newOrientation)
    {
        public CatanOrientation Orientation { get; } = newOrientation;
    }

    public class RollMessage(TurnRollModel roll)
    {
        public TurnRollModel Roll { get; } = roll;
    }

    public class TurnEnding(string playerId)
    {
        public string PlayerId { get; } = playerId;
    }

    public class TurnStarting(string playerId)
    {
        public string PlayerId { get; } = playerId;
    }

    public class EndGame
    {

    }

    /// <summary>
    ///     we pass the resources tracked from the GameViewModel to whatever ViewModel needs to know (e.g. PlayerviewModel
    /// </summary>
    /// <param name="list"></param>
    public class TrackedResourceTypes(ObservableCollection<ResourceType> list)
    {
        public ObservableCollection<ResourceType> TrackedResources { get; set; } = list;
    }

}

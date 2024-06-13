using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Catan3.Utility;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Windows.Storage;
using WinUIEx;
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
    public class MoveRobberMessage(HexCoordinates coordinates, string? targetPlayerId)
    {
        public HexCoordinates Coordinates { get; } = coordinates;
        public string? TargetPlayerId { get; } = targetPlayerId;
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
    public class PurchaseMessage(Entitlement entitlement)
    {
        public Entitlement Entitlement { get; } = entitlement;
    }
    public enum ErrorLevel { Information, Protection, Critical }
    public class ErrorMessage(string message, ErrorLevel errorLevel, [CallerMemberName] string cmb = "", [CallerLineNumber] int cln = 0, [CallerFilePath] string cfp = "")
    {
        public string Message { get; } = message;
        public string CallerMemberName { get; } = cmb;
        public string CallerLineNumber { get; } = cln.ToString();
        public string CallerFilePath { get; } = cfp;
        public ErrorLevel ErrorLevel { get; } = errorLevel;
    }
    // 
    // we can update the player colors "on the fly".  Normally, we would subscribe to the change notification event an update the bindings
    // e.g. the the BuildingViewModel, we'd subscribe to changes tothe PlayerColorViewModel and update the color of buildings that are 
    // owned by the player whose colors have changed. The problem is that if you create a building, subscribe to change notification events,
    // and then Undo, the event remains subscribed.  that is ok (the function binding knows to check for the owner)...but we will have 
    // leaked the event because we will resubscribe the next time the building is bought).  To work around this, we'll use the MVVM messaging
    // system and publish an event that says the players colors changed.
    public class PlayerColorChanged(PlayerColorViewModel colors)
    {
        public PlayerColorViewModel PlayerColors { get; } = colors;
    }
    // Message to request opening a file with specified filters
    public class OpenFileRequestMessage(WindowEx parent, IList<string> filters)
    {
        public IList<string> Filters { get;  } = filters;
        public WindowEx Parent { get; } = parent;
    }
    // Message to respond with the opened file
    public class OpenFileResponseMessage(StorageFile? file)
    {
        public StorageFile? File { get; } = file;
    }
    //
    // message to make a player go first
    public class GoFirstMessage(string playerId)
    {
        public string PlayerId { get; } = playerId;
    }

}

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Windows.Services.Maps.LocalSearch;
using Windows.Storage;
using WinUIEx;

namespace Catan3.Models
{
    // UI-specific messages that reference ViewModels - these stay in DesktopApp
    
    public class BuildingMouseEntered(BuildingViewModel buildingViewModel)
    {
        public BuildingViewModel BuildingViewModel { get; } = buildingViewModel;
    }
    
    public class BuildingMouseExit(BuildingViewModel buildingViewModel)
    {
        public BuildingViewModel BuildingViewModel { get; } = buildingViewModel;
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
    
    public class UpdateOrientation(CatanOrientation newOrientation)
    {
        public CatanOrientation Orientation { get; } = newOrientation;
    }
    
    public class GameStateChanged(GameState newState)
    {
        public GameState State { get; } = newState;
    }
    
    public class TurnEnding(string playerId)
    {
        public string PlayerId { get; } = playerId;
    }
    
    public class TurnStarting(string playerId)
    {
        public string PlayerId { get; } = playerId;
    }
    
    /// <summary>
    /// We pass the resources tracked from the GameViewModel to whatever ViewModel needs to know (e.g. PlayerViewModel)
    /// </summary>
    /// <param name="list"></param>
    public class TrackedResourceTypes(ObservableCollection<ResourceType> list)
    {
        public ObservableCollection<ResourceType> TrackedResources { get; set; } = list;
    }
    
    // 
    // We can update the player colors "on the fly". Normally, we would subscribe to the change notification event and update the bindings
    // e.g. the BuildingViewModel, we'd subscribe to changes to the PlayerColorViewModel and update the color of buildings that are 
    // owned by the player whose colors have changed. The problem is that if you create a building, subscribe to change notification events,
    // and then Undo, the event remains subscribed. That is ok (the function binding knows to check for the owner)...but we will have 
    // leaked the event because we will resubscribe the next time the building is bought). To work around this, we'll use the MVVM messaging
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
    public class OpenFileResponseMessage(string? file)
    {
        public string? FilePath { get; } = file;
    }

    public class GetPlayerMessage(string playerId)
    {
        public string PlayerId { get; } = playerId;
    }

    public class GetPlayerResponse(PlayerViewModel? model)
    {
        public PlayerViewModel? Player { get; } = model;
    }

    public class QueryResourcesMessage(IList<ResourceType> resources)
    {
        public IList<ResourceType> Resources { get; } = resources;
    }

    // Extended NewGameMessage that includes LocalFile for DesktopApp specific functionality
    public class NewGameMessage(GameType selectedGame, IList<string> playerIds, string localFile)
    {
        public GameType GameType = selectedGame;
        public IList<string> PlayerIds { get; set; } = playerIds;
        public string LocalFile { get; set; } = localFile;
    }
}

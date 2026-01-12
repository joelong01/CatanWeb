using Catan3.Shared.Models;
using Catan3.Shared.Profiles;
using Catan3.Shared.Extensions;
using Catan3.WebUI.Models;

namespace Catan3.WebUI.Services;

/// <summary>
/// Singleton service managing game state and UI state for the thick client architecture.
/// Holds GameModel received from GameService via SignalR and provides state change notifications
/// to UI components. Coordinates client-side SVG rendering and animations.
/// </summary>
public class GameStateService
{
    private GameModel? _gameModel;
    private List<PlayerViewModel> _playerViewModels = new();
    private int _shownStars = 0;

    /// <summary>
    /// Event raised when game state or UI state changes.
    /// UI components subscribe to this event to re-render when state updates.
    /// </summary>
    public event EventHandler? OnStateChanged;

    /// <summary>
    /// Event raised when the Winner button is clicked in NavMenu.
    /// Game.razor subscribes to show the winner confirmation dialog.
    /// </summary>
    public event EventHandler? WinnerDialogRequested;

    /// <summary>
    /// Gets the current game model containing all game state (tiles, buildings, roads, players, etc.).
    /// Returns null if no game is currently loaded.
    /// </summary>
    public GameModel? GameModel => _gameModel;

    /// <summary>
    /// Gets the player view models in game order.
    /// Contains display colors, names, and image URIs for all players.
    /// Players are ordered according to GameModel.Players order.
    /// </summary>
    public IReadOnlyList<PlayerViewModel> Players => _playerViewModels;

    /// <summary>
    /// Gets or sets the current star threshold for building visibility (0-14).
    /// Buildings with star counts below this threshold are hidden on the board.
    /// </summary>
    public int ShownStars
    {
        get => _shownStars;
        set
        {
            if (_shownStars != value)
            {
                _shownStars = value;
                NotifyStateChanged();
            }
        }
    }

    /// <summary>
    /// Updates the game model with new data received from the server via SignalR.
    /// Triggers state change notification to update all subscribed UI components.
    /// </summary>
    /// <param name="gameModel">The new game model to apply.</param>
    public void UpdateGameModel(GameModel gameModel)
    {
        _gameModel = gameModel;
        NotifyStateChanged();
    }

    /// <summary>
    /// Updates the player view models from server profile data.
    /// Called when player data is loaded from the server or modified.
    /// Preserves player order from GameModel.Players if available.
    /// </summary>
    /// <param name="playerProfiles">Dictionary of player profiles from server keyed by player ID.</param>
    /// <param name="baseUrl">Optional base URL for image resolution.</param>
    public void UpdatePlayerData(Dictionary<string, PlayerProfile> playerProfiles, string? baseUrl = null)
    {
        // If we have a game model, preserve player order; otherwise use dictionary order
        if (_gameModel != null)
        {
            // Create PlayerViewModels in game player order
            _playerViewModels = _gameModel.Players
                .Where(p => playerProfiles.ContainsKey(p.Id))
                .Select(p => PlayerViewModel.FromProfile(playerProfiles[p.Id], baseUrl))
                .ToList();
        }
        else
        {
            // No game model yet - use dictionary order
            _playerViewModels = playerProfiles.Values
                .Select(p => PlayerViewModel.FromProfile(p, baseUrl))
                .ToList();
        }

        NotifyStateChanged();
    }

    /// <summary>
    /// Clears all game state (game model, player data, UI state).
    /// Used when leaving a game or starting a new game.
    /// </summary>
    public void ClearState()
    {
        _gameModel = null;
        _playerViewModels.Clear();
        _shownStars = 0;
        NotifyStateChanged();
    }

    /// <summary>
    /// Gets the player view model for a specific player ID.
    /// </summary>
    /// <param name="playerId">The player ID to look up.</param>
    /// <returns>PlayerViewModel if found, null otherwise.</returns>
    public PlayerViewModel? GetPlayerViewModel(string playerId)
    {
        return _playerViewModels.FirstOrDefault(p => p.Id == playerId);
    }

    /// <summary>
    /// Calculates the star value for a building based on adjacent tiles.
    /// Mirrors Desktop GameViewModel.SetGameStars() logic.
    /// </summary>
    /// <param name="buildingKey">The building key to calculate stars for.</param>
    /// <returns>Total star count (0-15) for the building location.</returns>
    public int CalculateStars(BuildingKey buildingKey)
    {
        if (_gameModel == null)
            return 0;

        var tiles = _gameModel.TilesForBuildings(buildingKey);
        return tiles.Stars();
    }

    /// <summary>
    /// Notifies all subscribed components that state has changed.
    /// Triggers re-rendering and animation updates.
    /// </summary>
    private void NotifyStateChanged()
    {
        OnStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests the winner confirmation dialog to be shown.
    /// Called from NavMenu when Winner button is clicked.
    /// </summary>
    public void RequestWinnerDialog()
    {
        WinnerDialogRequested?.Invoke(this, EventArgs.Empty);
    }
}

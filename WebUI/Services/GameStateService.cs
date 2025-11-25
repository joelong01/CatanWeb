using Catan3.Shared.Models;
using Catan3.Shared.ViewData;
using Catan3.Shared.Extensions;

namespace Catan3.WebUI.Services;

/// <summary>
/// Singleton service managing game state and UI state for the thick client architecture.
/// Holds GameModel received from GameService via SignalR and provides state change notifications
/// to UI components. Coordinates client-side SVG rendering and animations.
/// </summary>
public class GameStateService
{
    private GameModel? _gameModel;
    private Dictionary<string, PlayerData> _playerData = new();
    private int _shownStars = 0;

    /// <summary>
    /// Event raised when game state or UI state changes.
    /// UI components subscribe to this event to re-render when state updates.
    /// </summary>
    public event EventHandler? OnStateChanged;

    /// <summary>
    /// Gets the current game model containing all game state (tiles, buildings, roads, players, etc.).
    /// Returns null if no game is currently loaded.
    /// </summary>
    public GameModel? GameModel => _gameModel;

    /// <summary>
    /// Gets the player profile data dictionary keyed by player ID.
    /// Contains display colors, names, and image URIs for all players.
    /// </summary>
    public IReadOnlyDictionary<string, PlayerData> PlayerData => _playerData;

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
    /// Updates the player profile data dictionary.
    /// Called when player data is loaded from the server or modified.
    /// </summary>
    /// <param name="playerData">Dictionary of player data keyed by player ID.</param>
    public void UpdatePlayerData(Dictionary<string, PlayerData> playerData)
    {
        _playerData = playerData;
        NotifyStateChanged();
    }

    /// <summary>
    /// Clears all game state (game model, player data, UI state).
    /// Used when leaving a game or starting a new game.
    /// </summary>
    public void ClearState()
    {
        _gameModel = null;
        _playerData.Clear();
        _shownStars = 0;
        NotifyStateChanged();
    }

    /// <summary>
    /// Gets the PlayerData for a specific player ID.
    /// </summary>
    /// <param name="playerId">The player ID to look up.</param>
    /// <returns>PlayerData if found, null otherwise.</returns>
    public PlayerData? GetPlayerData(string playerId)
    {
        return _playerData.TryGetValue(playerId, out var data) ? data : null;
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
}

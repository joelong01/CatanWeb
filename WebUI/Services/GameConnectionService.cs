using Catan3.Shared.Models;
using Catan3.Shared.Services;
using Catan3.Shared.Utility;
using CommandResult = Catan3.Shared.Services.CommandResult;

namespace Catan3.WebUI.Services;

/// <summary>
/// Connection states for the WebUI game connection.
/// </summary>
public enum ConnectionState
{
    /// <summary>Initial state - no connection established.</summary>
    Disconnected,
    /// <summary>Currently connecting to SignalR hub.</summary>
    Connecting,
    /// <summary>Connected to hub but not joined to a game.</summary>
    Connected,
    /// <summary>Joined to a game and receiving updates.</summary>
    InGame
}

/// <summary>
/// Singleton service that manages the connection to the GameService.
/// Wraps GameServiceProxy and provides state management for the WebUI.
/// </summary>
public class GameConnectionService : IAsyncDisposable
{
    private readonly GameServiceConfig _config;
    private GameServiceProxy? _proxy;

    /// <summary>
    /// Current connection state.
    /// </summary>
    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    /// <summary>
    /// Current game ID (null if not in a game).
    /// </summary>
    public string? GameId => _proxy?.GameId;

    /// <summary>
    /// Current player ID.
    /// </summary>
    public string? PlayerId => _proxy?.PlayerId;

    /// <summary>
    /// Cached GameModel from last update.
    /// </summary>
    public GameModel? GameModel => _proxy?.GameModel;

    /// <summary>
    /// Event fired when game state is updated from the server.
    /// </summary>
    public event Action<GameModel>? GameStateUpdated;

    /// <summary>
    /// Event fired when a command completes.
    /// </summary>
    public event Action<string, bool, string>? CommandCompleted;

    /// <summary>
    /// Event fired when a command fails.
    /// </summary>
    public event Action<string, string>? CommandFailed;

    /// <summary>
    /// Event fired when connection state changes.
    /// </summary>
    public event Action<ConnectionState>? StateChanged;

    public GameConnectionService(GameServiceConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Connects to the GameService SignalR hub.
    /// </summary>
    /// <param name="playerId">The player ID for this connection.</param>
    public async Task ConnectAsync(string playerId)
    {
        if (State != ConnectionState.Disconnected)
        {
            // Already connected or connecting
            return;
        }

        SetState(ConnectionState.Connecting);

        try
        {
            _proxy = new GameServiceProxy(_config.HubUrl, _config.BaseUrl, playerId);

            // Subscribe to proxy events
            _proxy.GameStateUpdated += OnGameStateUpdated;
            _proxy.CommandCompleted += OnCommandCompleted;
            _proxy.CommandFailed += OnCommandFailed;

            await _proxy.ConnectAsync();
            SetState(ConnectionState.Connected);
        }
        catch
        {
            SetState(ConnectionState.Disconnected);
            _proxy = null;
            throw;
        }
    }

    /// <summary>
    /// Joins a specific game.
    /// </summary>
    /// <param name="gameId">The game ID to join.</param>
    public async Task JoinGameAsync(string gameId)
    {
        if (_proxy == null)
        {
            throw new InvalidOperationException("Not connected. Call ConnectAsync first.");
        }

        await _proxy.JoinGameAsync(gameId);
        SetState(ConnectionState.InGame);
    }

    /// <summary>
    /// Connects and joins a game in one call.
    /// </summary>
    /// <param name="gameId">The game ID to join.</param>
    /// <param name="playerId">The player ID for this connection.</param>
    public async Task ConnectAndJoinAsync(string gameId, string playerId)
    {
        if (State == ConnectionState.Disconnected)
        {
            await ConnectAsync(playerId);
        }

        if (State == ConnectionState.Connected || (State == ConnectionState.InGame && GameId != gameId))
        {
            await JoinGameAsync(gameId);
        }
    }

    /// <summary>
    /// Leaves the current game but stays connected.
    /// </summary>
    public async Task LeaveGameAsync()
    {
        if (_proxy == null || string.IsNullOrEmpty(GameId))
        {
            return;
        }

        await _proxy.LeaveGameAsync(GameId);
        SetState(ConnectionState.Connected);
    }

    /// <summary>
    /// Disconnects from the GameService.
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_proxy != null)
        {
            _proxy.GameStateUpdated -= OnGameStateUpdated;
            _proxy.CommandCompleted -= OnCommandCompleted;
            _proxy.CommandFailed -= OnCommandFailed;

            await _proxy.DisposeAsync();
            _proxy = null;
        }

        SetState(ConnectionState.Disconnected);
    }

    #region REST API Methods

    /// <summary>
    /// Loads a game from database into memory via REST API.
    /// Call this before ConnectAndJoinAsync for saved games.
    /// </summary>
    public async Task<bool> LoadGameFromDatabaseAsync(string gameId)
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri(_config.BaseUrl) };
        try
        {
            var response = await httpClient.PostAsync($"/api/game/{gameId}/load", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Game Commands

    /// <summary>
    /// Executes an Undo command.
    /// </summary>
    public async Task<CommandResult> UndoAsync()
    {
        EnsureInGame();
        return await _proxy!.ExecuteUndoAsync();
    }

    /// <summary>
    /// Executes a Redo command.
    /// </summary>
    public async Task<CommandResult> RedoAsync()
    {
        EnsureInGame();
        return await _proxy!.ExecuteRedoAsync();
    }

    /// <summary>
    /// Executes a Next command.
    /// </summary>
    public async Task<CommandResult> NextAsync()
    {
        EnsureInGame();
        return await _proxy!.ExecuteNextAsync();
    }

    /// <summary>
    /// Executes a Shuffle command.
    /// </summary>
    public async Task<CommandResult> ShuffleAsync()
    {
        EnsureInGame();
        return await _proxy!.ExecuteShuffleAsync();
    }

    /// <summary>
    /// Executes a Roll command.
    /// </summary>
    public async Task<CommandResult> RollAsync(int die1, int die2)
    {
        EnsureInGame();
        return await _proxy!.ExecuteRollAsync(die1, die2);
    }

    /// <summary>
    /// Executes a GoFirst command.
    /// </summary>
    public async Task<CommandResult> GoFirstAsync(string firstPlayerId)
    {
        EnsureInGame();
        return await _proxy!.ExecuteGoFirstAsync(firstPlayerId);
    }

    /// <summary>
    /// Executes a Building Upgrade command (place settlement or upgrade to city).
    /// </summary>
    public async Task<CommandResult> BuildingUpgradeAsync(BuildingKey buildingKey)
    {
        EnsureInGame();
        return await _proxy!.ExecuteBuildingUpgradeAsync(GameId!, buildingKey);
    }

    /// <summary>
    /// Executes a Road Purchase command.
    /// </summary>
    public async Task<CommandResult> RoadPurchaseAsync(RoadKey roadKey)
    {
        EnsureInGame();
        return await _proxy!.ExecuteRoadPurchaseAsync(GameId!, roadKey);
    }

    /// <summary>
    /// Executes a Move Robber command.
    /// </summary>
    public async Task<CommandResult> MoveRobberAsync(HexCoordinates coordinates, string? targetPlayerId = null)
    {
        EnsureInGame();
        return await _proxy!.ExecuteMoveRobberAsync(GameId!, coordinates, targetPlayerId);
    }

    /// <summary>
    /// Executes a Purchase command (buy entitlement).
    /// </summary>
    public async Task<CommandResult> PurchaseAsync(Entitlement entitlement)
    {
        EnsureInGame();
        return await _proxy!.ExecutePurchaseAsync(entitlement);
    }

    #endregion

    #region Private Methods

    private void EnsureInGame()
    {
        if (State != ConnectionState.InGame || _proxy == null)
        {
            throw new InvalidOperationException("Not in a game. Call JoinGameAsync first.");
        }
    }

    private void SetState(ConnectionState newState)
    {
        if (State != newState)
        {
            State = newState;
            StateChanged?.Invoke(newState);
        }
    }

    private void OnGameStateUpdated(GameModel gameModel)
    {
        // Update proxy's PlayerId to match current player so commands are authorized
        if (_proxy != null && !string.IsNullOrEmpty(gameModel.CurrentPlayerId))
        {
            _proxy.PlayerId = gameModel.CurrentPlayerId;
        }

        GameStateUpdated?.Invoke(gameModel);
    }

    private void OnCommandCompleted(string commandId, bool success, string message)
    {
        CommandCompleted?.Invoke(commandId, success, message);
    }

    private void OnCommandFailed(string commandId, string error)
    {
        CommandFailed?.Invoke(commandId, error);
    }

    #endregion

    #region IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }

    #endregion
}

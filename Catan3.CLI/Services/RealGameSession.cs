using Microsoft.Extensions.Logging;
using Catan3.CLI.Commands;
using Catan3.Shared.Models;
using Catan3.Shared.Services;
using Catan3.Shared.Utility;
using System.Text.Json;

namespace Catan3.CLI.Services;

/// <summary>
/// Represents a real game session connected to a running GameService
/// Uses SignalRProxy to interact with the game, following the same patterns as EndToEndStatefulTest
/// </summary>
public class RealGameSession : IAsyncDisposable
{
    private readonly GameRunOptions _options;
    private readonly ILogger _logger;
    private readonly Dictionary<string, SignalRProxy> _proxies = new();
    private readonly HttpClient _httpClient;

    public string GameId { get; private set; } = "";
    public int PlayerCount => _options.PlayerCount;

    /// <summary>
    /// Gets all connected proxies (for verification purposes)
    /// </summary>
    public IReadOnlyDictionary<string, SignalRProxy> Proxies => _proxies;

    public RealGameSession(GameRunOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;
        _httpClient = new HttpClient { BaseAddress = new Uri(_options.GetRestApiUrl()) };
    }

    public GameModel? CurrentGameModel
    {
        get
        {
            // Return the GameModel from any proxy - they should all be consistent
            return _proxies.Values.FirstOrDefault()?.GameModel;
        }
    }

    /// <summary>
    /// Initializes the session by creating a game and connecting all players via SignalR
    /// </summary>
    public async Task InitializeAsync()
    {
        _logger.LogDebug("Initializing game session with {PlayerCount} players", _options.PlayerCount);

        // Step 1: Create game via REST API
        GameId = await CreateGameViaRest();
        _logger.LogInformation("Game created with ID: {GameId}", GameId);

        // Step 2: Connect all players via SignalR - but don't wait for individual states
        var playerNames = _options.GetPlayerNames();
        var playerIds = _options.GetPlayerIds();

        for (int i = 0; i < playerNames.Count; i++)
        {
            var playerId = playerIds[i];
            var playerName = playerNames[i];

            _logger.LogDebug("Connecting player {PlayerId} ({PlayerName}) via SignalR", playerId, playerName);

            // Create SignalR proxy for real service (not test factory)
            var hubUrl = _options.GetSignalRHubUrl();
            var proxy = new SignalRProxy(hubUrl, playerId, GameId);

            // Connect to SignalR hub - this starts the continuous listening
            await proxy.ConnectAsync();

            _proxies[playerId] = proxy;
            _logger.LogDebug("Player {PlayerId} connected successfully", playerId);
        }

        _logger.LogInformation("All {PlayerCount} players connected to game {GameId}", PlayerCount, GameId);

        // Step 3: Wait for all proxies to receive their initial GameStateUpdated notification
        // This implements the pattern you described - wait for all notification threads to signal completion
        await WaitForAllProxiesToReceiveInitialUpdate();

        // Step 4: Verify initial game state consistency
        await VerifyGameConsistency();
        var currentState = GetCurrentGameState();
        _logger.LogInformation("Game initialized in state: {GameState}", currentState);
    }

    /// <summary>
    /// Waits for all proxies to receive their initial GameStateUpdated notification
    /// Implements the async notification pattern with Task.WaitAll equivalent
    /// </summary>
    private async Task WaitForAllProxiesToReceiveInitialUpdate()
    {
        var timeout = TimeSpan.FromSeconds(10);
        var waitStart = DateTime.UtcNow;

        _logger.LogDebug("Waiting for all {ProxyCount} proxies to receive initial GameStateUpdated", _proxies.Count);

        while (DateTime.UtcNow - waitStart < timeout)
        {
            // Check if all proxies have received a GameStateUpdated notification
            var proxiesWithGameState = _proxies.Values
                .Where(proxy => proxy.GameModel != null)
                .ToList();

            _logger.LogDebug("Proxies with GameState: {Count}/{Total}", proxiesWithGameState.Count, _proxies.Count);

            if (proxiesWithGameState.Count == _proxies.Count)
            {
                _logger.LogDebug("All proxies have received initial GameStateUpdated notification");
                return;
            }

            // Brief delay before checking again
            await Task.Delay(100);
        }

        throw new TimeoutException($"Timeout waiting for all proxies to receive initial GameStateUpdated after {timeout.TotalSeconds} seconds");
    }

    /// <summary>
    /// Creates a game via REST API call to the real GameService
    /// </summary>
    private async Task<string> CreateGameViaRest()
    {
        var playerNames = _options.GetPlayerNames();
        var playerIds = _options.GetPlayerIds();

        // Create request using the shared model pattern
        var request = new
        {
            gameType = _options.GameType.ToString(),
            players = playerNames.Zip(playerIds, (name, id) => new { id, name }).ToList()
        };

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        _logger.LogDebug("Creating {GameType} game via REST API at {BaseAddress}",
            _options.GameType, _httpClient.BaseAddress);

        var response = await _httpClient.PostAsync("/api/game/new", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to create game: {response.StatusCode}. Error: {errorContent}");
        }

        var responseBody = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(responseBody);

        if (!result.TryGetProperty("gameId", out var gameIdElement))
        {
            throw new InvalidOperationException("Game creation did not return gameId");
        }

        return gameIdElement.GetString() ??
            throw new InvalidOperationException("Game creation returned null gameId");
    }

    /// <summary>
    /// Gets a specific proxy by player ID
    /// </summary>
    public SignalRProxy GetProxy(string playerId)
    {
        if (!_proxies.TryGetValue(playerId, out var proxy))
        {
            throw new InvalidOperationException($"Proxy for player {playerId} not found");
        }
        return proxy;
    }

    /// <summary>
    /// Gets the current player ID from the game state
    /// </summary>
    public string GetCurrentPlayerId()
    {
        var anyProxy = _proxies.Values.First();
        var currentPlayerId = anyProxy.GameModel?.CurrentPlayerId;

        if (string.IsNullOrEmpty(currentPlayerId))
        {
            // Default to first player if no current player set yet
            return _options.GetPlayerIds()[0];
        }

        return currentPlayerId;
    }

    /// <summary>
    /// Gets the current game state
    /// </summary>
    public GameState GetCurrentGameState()
    {
        var anyProxy = _proxies.Values.First();
        return anyProxy.GameModel?.GameState ?? GameState.Uninitialized;
    }

    /// <summary>
    /// Gets the player names for this session
    /// </summary>
    public List<string> GetPlayerNames()
    {
        return _options.GetPlayerNames();
    }

    /// <summary>
    /// Gets the player IDs for this session
    /// </summary>
    public List<string> GetPlayerIds()
    {
        return _options.GetPlayerIds();
    }

    /// <summary>
    /// Executes a DoAction command using the current player and waits for all proxies to receive updates
    /// Implements the async notification pattern where action triggers GameModel updates to all clients
    /// </summary>
    public async Task ExecuteAction(GameAction action)
    {
        var currentPlayerId = GetCurrentPlayerId();
        var proxy = GetProxy(currentPlayerId);

        _logger.LogDebug("Executing {Action} for player {PlayerId}", action, currentPlayerId);

        // Capture the current GameHash from all proxies before the action
        var preActionHashes = _proxies.Values
            .Where(p => p.GameModel != null)
            .ToDictionary(p => p.PlayerId, p => p.GameModel!.GameHash);

        // Execute the action
        var result = await proxy.ExecuteDoActionAsync(GameId, action);

        if (!result.Success)
        {
            throw new InvalidOperationException($"Action {action} failed: {result.Message}");
        }

        // Wait for all proxies to receive the updated GameModel
        // This implements your described pattern: action triggers updates, wait for all notification threads
        await WaitForAllProxiesToReceiveActionUpdate(preActionHashes, action);

        _logger.LogDebug("Action {Action} completed successfully with all proxies updated", action);
    }

    /// <summary>
    /// Waits for all proxies to receive GameStateUpdated after an action
    /// Implements the notification pattern: main thread waits while notification threads signal completion
    /// </summary>
    private async Task WaitForAllProxiesToReceiveActionUpdate(Dictionary<string, string> preActionHashes, GameAction action)
    {
        var timeout = TimeSpan.FromSeconds(10);
        var waitStart = DateTime.UtcNow;

        _logger.LogDebug("Waiting for all proxies to receive GameStateUpdated after {Action}", action);

        while (DateTime.UtcNow - waitStart < timeout)
        {
            // Check if all proxies have received updated GameModel (hash changed or action completed)
            var proxiesWithUpdatedState = 0;

            foreach (var proxy in _proxies.Values)
            {
                if (proxy.GameModel != null)
                {
                    // Check if this proxy's GameHash has changed from before the action
                    if (preActionHashes.TryGetValue(proxy.PlayerId, out var preActionHash))
                    {
                        if (proxy.GameModel.GameHash != preActionHash)
                        {
                            proxiesWithUpdatedState++;
                        }
                    }
                    else
                    {
                        // Proxy didn't have a hash before, but has one now - counts as updated
                        proxiesWithUpdatedState++;
                    }
                }
            }

            _logger.LogDebug("Proxies with updated state after {Action}: {Count}/{Total}",
                action, proxiesWithUpdatedState, _proxies.Count);

            if (proxiesWithUpdatedState == _proxies.Count)
            {
                _logger.LogDebug("All proxies received updated GameModel after {Action}", action);
                return;
            }

            // Brief delay before checking again - this allows notification threads to process
            await Task.Delay(50);
        }

        throw new TimeoutException($"Timeout waiting for all proxies to receive GameStateUpdated after {action} - {timeout.TotalSeconds} seconds");
    }

    /// <summary>
    /// Executes a Next action specifically
    /// </summary>
    public async Task ExecuteNextAction()
    {
        await ExecuteAction(GameAction.Next);
    }

    /// <summary>
    /// Verifies all proxies have received recent updates (consistent game state)
    /// </summary>
    public async Task VerifyAllProxiesReceivedUpdate()
    {
        // Brief delay to allow for state propagation
        await Task.Delay(50);

        // Check that all proxies have consistent GameModel and GameHash
        var gameStates = _proxies.Values
            .Select(p => new { Proxy = p.PlayerId, State = p.GameModel?.GameState, Hash = p.GameModel?.GameHash })
            .Where(x => x.State.HasValue)
            .ToList();

        if (gameStates.Count > 1)
        {
            var reference = gameStates[0];
            var inconsistencies = gameStates.Where(g => g.State != reference.State || g.Hash != reference.Hash).ToList();

            if (inconsistencies.Any())
            {
                var errorMessage = $"Game state inconsistency detected: {string.Join(", ", inconsistencies.Select(i => $"{i.Proxy}:{i.State}"))}";
                throw new InvalidOperationException(errorMessage);
            }
        }
    }

    /// <summary>
    /// Verifies game consistency across all proxies using GameHash
    /// </summary>
    public async Task VerifyGameConsistency()
    {
        await Task.Delay(50); // Brief delay for state propagation

        var proxyStates = _proxies.Values
            .Select(p => new { Proxy = p.PlayerId, GameState = p.GameModel })
            .Where(x => x.GameState != null)
            .ToList();

        if (proxyStates.Count <= 1) return;

        var referenceProxy = proxyStates[0];
        var referenceState = referenceProxy.GameState!;
        var inconsistencies = new List<string>();

        foreach (var proxyState in proxyStates.Skip(1))
        {
            var state = proxyState.GameState!;

            if (state.GameState != referenceState.GameState)
                inconsistencies.Add($"{proxyState.Proxy}: GameState {state.GameState} vs {referenceState.GameState}");

            if (state.CurrentPlayerId != referenceState.CurrentPlayerId)
                inconsistencies.Add($"{proxyState.Proxy}: CurrentPlayer {state.CurrentPlayerId} vs {referenceState.CurrentPlayerId}");

            if (state.GameStateMachineVersion != referenceState.GameStateMachineVersion)
                inconsistencies.Add($"{proxyState.Proxy}: GameStateMachineVersion {state.GameStateMachineVersion} vs {referenceState.GameStateMachineVersion}");

            // GameHash verification for board consistency - this is the key check
            if (!string.IsNullOrEmpty(state.GameHash) && !string.IsNullOrEmpty(referenceState.GameHash))
            {
                if (state.GameHash != referenceState.GameHash)
                {
                    inconsistencies.Add($"{proxyState.Proxy}: GameHash {state.GameHash} vs {referenceState.GameHash} (BOARD MISMATCH!)");
                }
            }
        }

        if (inconsistencies.Any())
        {
            var errorMessage = $"Game consistency check failed:\n  " + string.Join("\n  ", inconsistencies);
            throw new InvalidOperationException(errorMessage);
        }
    }

    /// <summary>
    /// Properly disposes all proxies and cleans up resources
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _logger.LogDebug("Disposing game session {GameId}", GameId);

        foreach (var proxy in _proxies.Values)
        {
            try
            {
                await proxy.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing proxy for player {PlayerId}", proxy.PlayerId);
            }
        }

        _proxies.Clear();
        _httpClient.Dispose();

        _logger.LogInformation("Game session {GameId} disposed", GameId);
    }
}
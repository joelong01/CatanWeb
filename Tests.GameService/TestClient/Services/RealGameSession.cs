using Microsoft.Extensions.Logging;
using TestClient.Commands;
using Catan3.Shared.Models;
using Catan3.Shared.Services;
using System.Text.Json;

namespace TestClient.Services;

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

    public RealGameSession(GameRunOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;
        _httpClient = new HttpClient { BaseAddress = new Uri(_options.GetRestApiUrl()) };
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

        // Step 2: Connect all players via SignalR
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
            await proxy.ConnectAsync();
            
            _proxies[playerId] = proxy;
            _logger.LogDebug("Player {PlayerId} connected successfully", playerId);
        }

        _logger.LogInformation("All {PlayerCount} players connected to game {GameId}", PlayerCount, GameId);

        // Step 3: Verify initial game state
        await VerifyGameConsistency();
        var currentState = GetCurrentState();
        _logger.LogInformation("Game initialized in state: {GameState}", currentState);
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
        var currentPlayerId = anyProxy.LastGameState?.CurrentPlayerId;
        
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
    public GameState GetCurrentState()
    {
        var anyProxy = _proxies.Values.First();
        return anyProxy.LastGameState?.GameState ?? GameState.Uninitialized;
    }

    /// <summary>
    /// Gets the player names for this session
    /// </summary>
    public List<string> GetPlayerNames()
    {
        return _options.GetPlayerNames();
    }

    /// <summary>
    /// Executes a DoAction command using the current player
    /// </summary>
    public async Task ExecuteAction(GameAction action)
    {
        var currentPlayerId = GetCurrentPlayerId();
        var proxy = GetProxy(currentPlayerId);
        
        _logger.LogDebug("Executing {Action} for player {PlayerId}", action, currentPlayerId);
        
        var result = await proxy.ExecuteDoActionAsync(GameId, action);
        
        if (!result.Success)
        {
            throw new InvalidOperationException($"Action {action} failed: {result.Message}");
        }
        
        // Verify all proxies received updates
        await VerifyAllProxiesReceivedUpdate();
        
        _logger.LogDebug("Action {Action} completed successfully", action);
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
        
        // Check that all proxies have consistent LastGameState and GameHash
        var gameStates = _proxies.Values
            .Select(p => new { Proxy = p.PlayerId, State = p.LastGameState?.GameState, Hash = p.LastGameState?.GameHash })
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
            .Select(p => new { Proxy = p.PlayerId, GameState = p.LastGameState })
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
                
            if (state.Version != referenceState.Version)
                inconsistencies.Add($"{proxyState.Proxy}: Version {state.Version} vs {referenceState.Version}");
            
            // GameHash verification for board consistency
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
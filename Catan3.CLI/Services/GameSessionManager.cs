using Microsoft.Extensions.Logging;
using Catan3.CLI.Commands;

namespace Catan3.CLI.Services;

/// <summary>
/// Manages multiple game sessions and provides session lifecycle management
/// </summary>
public class GameSessionManager
{
    private readonly ILogger<GameSessionManager> _logger;
    private readonly Dictionary<string, RealGameSession> _activeSessions = [];

    public GameSessionManager(ILogger<GameSessionManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates a new game session
    /// </summary>
    public async Task<RealGameSession> CreateSessionAsync(GameRunOptions options)
    {
        var session = new RealGameSession(options, _logger);
        await session.InitializeAsync();
        
        _activeSessions[session.GameId] = session;
        _logger.LogInformation("Created session {GameId} with {PlayerCount} players", 
            session.GameId, session.PlayerCount);
        
        return session;
    }

    /// <summary>
    /// Gets an existing session by GameId
    /// </summary>
    public RealGameSession? GetSession(string gameId)
    {
        return _activeSessions.TryGetValue(gameId, out var session) ? session : null;
    }

    /// <summary>
    /// Disposes a session and removes it from active sessions
    /// </summary>
    public async Task DisposeSessionAsync(string gameId)
    {
        if (_activeSessions.TryGetValue(gameId, out var session))
        {
            await session.DisposeAsync();
            _activeSessions.Remove(gameId);
            _logger.LogInformation("Disposed session {GameId}", gameId);
        }
    }

    /// <summary>
    /// Gets all active session IDs
    /// </summary>
    public IEnumerable<string> GetActiveSessionIds()
    {
        return _activeSessions.Keys;
    }

    /// <summary>
    /// Disposes all active sessions
    /// </summary>
    public async Task DisposeAllAsync()
    {
        var disposeTasks = _activeSessions.Values.Select(session => session.DisposeAsync().AsTask());
        await Task.WhenAll(disposeTasks);
        
        _activeSessions.Clear();
        _logger.LogInformation("Disposed all active sessions");
    }
}
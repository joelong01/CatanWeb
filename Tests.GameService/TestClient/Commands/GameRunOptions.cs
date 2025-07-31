using Catan3.Shared.Models;
using Microsoft.Extensions.Logging;

namespace TestClient.Commands;

/// <summary>
/// Configuration options for running a game session
/// </summary>
public class GameRunOptions
{
    /// <summary>
    /// Type of game to create (Regular or Expansion)
    /// </summary>
    public GameType GameType { get; set; } = GameType.Regular;

    /// <summary>
    /// Number of players (default: 3 for Regular, 5 for Expansion)
    /// </summary>
    public int PlayerCount { get; set; } = 3;

    /// <summary>
    /// GameState to stop execution at (null = don't stop)
    /// </summary>
    public string? RunToState { get; set; }

    /// <summary>
    /// Run complete end-to-end test (ignores RunToState)
    /// </summary>
    public bool Complete { get; set; } = false;

    /// <summary>
    /// Keep game alive after completion
    /// </summary>
    public bool NoExit { get; set; } = false;

    /// <summary>
    /// Logging level for console output
    /// </summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Error;

    /// <summary>
    /// GameService server URI (enables debugging against running service or production testing)
    /// Examples: http://localhost:8080, https://production-server.com
    /// </summary>
    public string ServerUri { get; set; } = "http://localhost:8080";

    /// <summary>
    /// Gets the player names to use for this game type and count
    /// Uses the 5 specified names: Joe, Adrian, Dodgy, Doug, Ryan
    /// </summary>
    public List<string> GetPlayerNames()
    {
        var allNames = new[] { "Joe", "Adrian", "Dodgy", "Doug", "Ryan" };
        return allNames.Take(PlayerCount).ToList();
    }

    /// <summary>
    /// Gets the player IDs in the Desktop app format (Name-001, Name-002, etc.)
    /// </summary>
    public List<string> GetPlayerIds()
    {
        return GetPlayerNames()
            .Select((name, index) => $"{name}-{index + 1:D3}")
            .ToList();
    }

    /// <summary>
    /// Gets the SignalR hub URL for the configured server
    /// </summary>
    public string GetSignalRHubUrl()
    {
        var uri = new Uri(ServerUri);
        return new Uri(uri, "/gameHub").ToString();
    }

    /// <summary>
    /// Gets the REST API base URL for the configured server
    /// </summary>
    public string GetRestApiUrl()
    {
        return ServerUri.TrimEnd('/');
    }

    /// <summary>
    /// Validates the options for consistency
    /// </summary>
    public void Validate()
    {
        if (PlayerCount < 1 || PlayerCount > 5)
        {
            throw new ArgumentException("Player count must be between 1 and 5");
        }

        if (GameType == GameType.Expansion && PlayerCount < 4)
        {
            throw new ArgumentException("Expansion games require at least 4 players");
        }

        if (GameType == GameType.Regular && PlayerCount > 4)
        {
            throw new ArgumentException("Regular games support maximum 4 players");
        }

        if (!string.IsNullOrEmpty(RunToState) && Complete)
        {
            throw new ArgumentException("Cannot specify both --run-to and --complete");
        }

        if (!Uri.TryCreate(ServerUri, UriKind.Absolute, out var uri) || 
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            throw new ArgumentException($"Invalid server URI: {ServerUri}. Must be a valid HTTP/HTTPS URL.");
        }

        // Validate RunToState if provided
        if (!string.IsNullOrEmpty(RunToState))
        {
            if (!Enum.TryParse<GameState>(RunToState, ignoreCase: true, out _))
            {
                throw new ArgumentException($"Invalid GameState: {RunToState}. Valid states: {string.Join(", ", Enum.GetNames<GameState>())}");
            }
        }
    }

    /// <summary>
    /// Returns a summary of the configuration for logging
    /// </summary>
    public override string ToString()
    {
        return $"GameType: {GameType}, Players: {PlayerCount} ({string.Join(", ", GetPlayerNames())}), " +
               $"Server: {ServerUri}, Complete: {Complete}, RunTo: {RunToState ?? "None"}, LogLevel: {LogLevel}";
    }
}
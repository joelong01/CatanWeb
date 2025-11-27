namespace Catan3.Shared.Profiles;

/// <summary>
/// Player lifetime statistics across all games.
/// Stored in database as part of Profiles.
/// Maintains composition relationship with GameStats for aggregation.
/// </summary>
public record LifetimeStats
{
    /// <summary>
    /// Total number of games played.
    /// </summary>
    public int GamesPlayed { get; init; }

    /// <summary>
    /// Total number of games won.
    /// </summary>
    public int Wins { get; init; }

    /// <summary>
    /// Aggregated game statistics (sum of all games).
    /// Uses composition to avoid property duplication.
    /// </summary>
    public GameStats Totals { get; init; } = GameStats.Empty;

    /// <summary>
    /// Longest road ever achieved across all games.
    /// </summary>
    public int LongestRoadRecord { get; init; }

    /// <summary>
    /// Highest score ever achieved across all games.
    /// </summary>
    public int HighestScoreRecord { get; init; }

    /// <summary>
    /// Win rate as percentage (calculated property).
    /// </summary>
    public double WinRate => GamesPlayed > 0 ? (Wins * 100.0 / GamesPlayed) : 0.0;

    /// <summary>
    /// Empty lifetime stats for initialization.
    /// </summary>
    public static LifetimeStats Empty { get; } = new();

    /// <summary>
    /// Adds a completed game to lifetime stats.
    /// </summary>
    /// <param name="gameStats">Statistics from the completed game.</param>
    /// <param name="won">Whether the player won this game.</param>
    /// <param name="longestRoad">Longest road achieved in this game.</param>
    /// <param name="score">Final score in this game.</param>
    /// <returns>Updated lifetime stats.</returns>
    public LifetimeStats AddGame(GameStats gameStats, bool won, int longestRoad, int score) => this with
    {
        GamesPlayed = GamesPlayed + 1,
        Wins = won ? Wins + 1 : Wins,
        Totals = Totals + gameStats,  // Uses GameStats operator+
        LongestRoadRecord = Math.Max(LongestRoadRecord, longestRoad),
        HighestScoreRecord = Math.Max(HighestScoreRecord, score)
    };
}

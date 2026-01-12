namespace Catan3.Shared.Profiles;

/// <summary>
/// Player lifetime statistics across all games.
/// Stored in database as part of PlayerProfile.
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
    /// Times won Longest Road bonus at game end.
    /// </summary>
    public int LongestRoadWins { get; init; }

    /// <summary>
    /// Times won Largest Army bonus at game end.
    /// </summary>
    public int LargestArmyWins { get; init; }

    /// <summary>
    /// Longest road ever achieved across all games (Max record).
    /// </summary>
    public int LongestRoadRecord { get; init; }

    /// <summary>
    /// Most soldiers played in a single game (Max record).
    /// </summary>
    public int MostSoldiersRecord { get; init; }

    /// <summary>
    /// Most stars earned in a single game (Max record).
    /// </summary>
    public int MostStarsRecord { get; init; }

    /// <summary>
    /// Highest score ever achieved across all games (Max record).
    /// </summary>
    public int HighestScoreRecord { get; init; }

    /// <summary>
    /// Aggregated game statistics (sum of all games).
    /// Uses composition to avoid property duplication.
    /// </summary>
    public GameStats Totals { get; init; } = GameStats.Empty;

    /// <summary>
    /// Win rate as percentage (calculated property).
    /// </summary>
    public double WinRate => GamesPlayed > 0 ? (Wins * 100.0 / GamesPlayed) : 0.0;

    /// <summary>
    /// Average stars per game (calculated property).
    /// </summary>
    public double AverageStars => GamesPlayed > 0 ? ((double)Totals.StarsEarned / GamesPlayed) : 0.0;

    /// <summary>
    /// Average soldiers per game (calculated property).
    /// </summary>
    public double AverageSoldiers => GamesPlayed > 0 ? ((double)Totals.SoldiersPlayed / GamesPlayed) : 0.0;

    /// <summary>
    /// Average times targeted per game (calculated property).
    /// </summary>
    public double AverageTargeted => GamesPlayed > 0 ? ((double)Totals.TimesTargeted / GamesPlayed) : 0.0;

    /// <summary>
    /// Empty lifetime stats for initialization.
    /// </summary>
    public static LifetimeStats Empty { get; } = new();

    /// <summary>
    /// Adds a completed game to lifetime stats.
    /// </summary>
    /// <param name="gameStats">Statistics from the completed game.</param>
    /// <param name="won">Whether the player won this game.</param>
    /// <param name="hasLongestRoad">Whether player had Longest Road bonus at game end.</param>
    /// <param name="hasLargestArmy">Whether player had Largest Army bonus at game end.</param>
    /// <param name="roadLength">Final road length in this game.</param>
    /// <param name="soldiersPlayed">Soldiers played in this game.</param>
    /// <param name="stars">Stars earned in this game.</param>
    /// <param name="score">Final score in this game.</param>
    /// <returns>Updated lifetime stats.</returns>
    public LifetimeStats AddGame(
        GameStats gameStats,
        bool won,
        bool hasLongestRoad,
        bool hasLargestArmy,
        int roadLength,
        int soldiersPlayed,
        int stars,
        int score) => this with
        {
            GamesPlayed = GamesPlayed + 1,
            Wins = won ? Wins + 1 : Wins,
            LongestRoadWins = hasLongestRoad ? LongestRoadWins + 1 : LongestRoadWins,
            LargestArmyWins = hasLargestArmy ? LargestArmyWins + 1 : LargestArmyWins,
            LongestRoadRecord = Math.Max(LongestRoadRecord, roadLength),
            MostSoldiersRecord = Math.Max(MostSoldiersRecord, soldiersPlayed),
            MostStarsRecord = Math.Max(MostStarsRecord, stars),
            HighestScoreRecord = Math.Max(HighestScoreRecord, score),
            Totals = Totals + gameStats
        };
}

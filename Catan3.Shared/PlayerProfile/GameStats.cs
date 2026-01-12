namespace Catan3.Shared.Profiles;

/// <summary>
/// Per-game statistics captured at game end.
/// Used as component of LifetimeStats for aggregation via operator+.
/// </summary>
public record GameStats(
    int ResourcesCollected,
    int ResourcesLostToRobber,
    int RoadsBuilt,
    int SettlementsBuilt,
    int CitiesBuilt,
    int SoldiersPlayed,
    int TimesTargeted,
    int GoodRolls,
    int BadRolls,
    int StarsEarned
)
{
    /// <summary>
    /// Empty stats for initialization.
    /// </summary>
    public static GameStats Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>
    /// Adds two GameStats together (for aggregation).
    /// </summary>
    public static GameStats operator +(GameStats a, GameStats b) => new(
        a.ResourcesCollected + b.ResourcesCollected,
        a.ResourcesLostToRobber + b.ResourcesLostToRobber,
        a.RoadsBuilt + b.RoadsBuilt,
        a.SettlementsBuilt + b.SettlementsBuilt,
        a.CitiesBuilt + b.CitiesBuilt,
        a.SoldiersPlayed + b.SoldiersPlayed,
        a.TimesTargeted + b.TimesTargeted,
        a.GoodRolls + b.GoodRolls,
        a.BadRolls + b.BadRolls,
        a.StarsEarned + b.StarsEarned
    );
}

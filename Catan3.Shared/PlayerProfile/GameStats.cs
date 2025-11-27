namespace Catan3.Shared.Profiles;

/// <summary>
/// Per-game statistics tracked during a single game (transient, not persisted).
/// Calculated during gameplay and displayed in game UI.
/// Used as component of LifetimeStats for aggregation.
/// </summary>
public record GameStats(
    int ResourcesCollected,
    int TradesMade,
    int RoadsBuilt,
    int SettlementsBuilt,
    int CitiesBuilt,
    int RobberLosses,
    int GoodRolls,
    int BadRolls
)
{
    /// <summary>
    /// Empty stats for initialization.
    /// </summary>
    public static GameStats Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>
    /// Adds two GameStats together (for aggregation).
    /// </summary>
    public static GameStats operator +(GameStats a, GameStats b) => new(
        a.ResourcesCollected + b.ResourcesCollected,
        a.TradesMade + b.TradesMade,
        a.RoadsBuilt + b.RoadsBuilt,
        a.SettlementsBuilt + b.SettlementsBuilt,
        a.CitiesBuilt + b.CitiesBuilt,
        a.RobberLosses + b.RobberLosses,
        a.GoodRolls + b.GoodRolls,
        a.BadRolls + b.BadRolls
    );
}

namespace Catan3.Shared.Profiles;

/// <summary>
/// Player profile data for persistent storage.
/// Separate from PlayerModel (game state) - this is for identity, colors, statistics.
/// Stored as JSON document in database (SQLite → CosmosDB).
/// Maintains hierarchical structure matching document model.
/// </summary>
public class PlayerProfile
{
    /// <summary>
    /// Unique identifier for the player.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display name of the player.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Player color scheme (nested).
    /// </summary>
    public required PlayerColors Colors { get; init; }

    /// <summary>
    /// Relative URI to player image (e.g., "/images/players/joe.jpg").
    /// Client combines with service base URL.
    /// </summary>
    public string? ImageUri { get; init; }

    /// <summary>
    /// Lifetime statistics (nested, optional).
    /// </summary>
    public LifetimeStats? LifetimeStats { get; init; }

    // ==================== Backward Compatibility ====================
    // These properties support existing database/API until migration complete

    /// <summary>
    /// Primary background color - backward compatibility.
    /// Maps to Colors.Primary.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string PrimaryBackgroundColor
    {
        get => Colors.Primary;
        init => Colors = new PlayerColors(value, Colors?.Secondary ?? "#CCCCCC", Colors?.Foreground ?? "#000000");
    }

    /// <summary>
    /// Secondary background color - backward compatibility.
    /// Maps to Colors.Secondary.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string SecondaryBackgroundColor
    {
        get => Colors.Secondary;
        init => Colors = new PlayerColors(Colors?.Primary ?? "#FFFFFF", value, Colors?.Foreground ?? "#000000");
    }

    /// <summary>
    /// Foreground color - backward compatibility.
    /// Maps to Colors.Foreground.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string ForegroundColor
    {
        get => Colors.Foreground;
        init => Colors = new PlayerColors(Colors?.Primary ?? "#FFFFFF", Colors?.Secondary ?? "#CCCCCC", value);
    }

    // ==================== Constructors ====================

    /// <summary>
    /// Parameterless constructor required for deserialization.
    /// </summary>
    public PlayerProfile()
    {
        Id = string.Empty;
        Name = string.Empty;
        Colors = PlayerColors.Default;
    }

    /// <summary>
    /// Constructor using nested PlayerColors (preferred).
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public PlayerProfile(string id, string name, PlayerColors colors, string? imageUri = null, LifetimeStats? lifetimeStats = null)
    {
        Id = id;
        Name = name;
        Colors = colors;
        ImageUri = imageUri;
        LifetimeStats = lifetimeStats;
    }

    /// <summary>
    /// Backward-compatible constructor using flat color properties.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public PlayerProfile(string id, string name, string primaryBackgroundColor, string secondaryBackgroundColor, string foregroundColor, string imageUri)
    {
        Id = id;
        Name = name;
        Colors = new PlayerColors(primaryBackgroundColor, secondaryBackgroundColor, foregroundColor);
        ImageUri = imageUri;
    }
}

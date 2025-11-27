using Catan3.Shared.Profiles;

namespace Catan3.WebUI.Models;

/// <summary>
/// View model for player display in WebUI.
/// Extends PlayerProfile with web-specific presentation logic.
/// Maintains same hierarchical structure as PlayerProfile for consistency.
/// </summary>
public class PlayerViewModel
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
    /// Player color scheme (shared with PlayerProfile).
    /// </summary>
    public required PlayerColors Colors { get; init; }

    /// <summary>
    /// Relative URI to player image (e.g., "/api/images/joe-001").
    /// </summary>
    public string? ImageUri { get; init; }

    /// <summary>
    /// Lifetime statistics (optional, may be null if not loaded).
    /// </summary>
    public LifetimeStats? LifetimeStats { get; init; }

    // ==================== Web-Specific Properties ====================

    /// <summary>
    /// CSS linear gradient string for player card backgrounds.
    /// Format: "linear-gradient(135deg, {Primary} 0%, {Secondary} 100%)"
    /// </summary>
    public string CssGradient =>
        $"linear-gradient(135deg, {Colors.Primary} 0%, {Colors.Secondary} 100%)";

    /// <summary>
    /// Full image URL combining base URL with relative URI.
    /// Returns null if ImageUri is null.
    /// </summary>
    public string? FullImageUrl { get; init; }

    // ==================== Constructors ====================

    /// <summary>
    /// Parameterless constructor for object initializer syntax.
    /// </summary>
    public PlayerViewModel()
    {
        Id = string.Empty;
        Name = string.Empty;
        Colors = PlayerColors.Default;
    }

    /// <summary>
    /// Constructor with all required parameters.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public PlayerViewModel(string id, string name, PlayerColors colors, string? imageUri = null, string? fullImageUrl = null, LifetimeStats? lifetimeStats = null)
    {
        Id = id;
        Name = name;
        Colors = colors;
        ImageUri = imageUri;
        FullImageUrl = fullImageUrl;
        LifetimeStats = lifetimeStats;
    }

    // ==================== Factory Methods ====================

    /// <summary>
    /// Creates a PlayerViewModel from a PlayerProfile.
    /// </summary>
    /// <param name="profile">The player profile to convert.</param>
    /// <param name="baseUrl">Optional base URL for image resolution (e.g., "https://localhost:5001").</param>
    /// <returns>A new PlayerViewModel instance.</returns>
    public static PlayerViewModel FromProfile(PlayerProfile profile, string? baseUrl = null)
    {
        string? fullImageUrl = null;
        if (profile.ImageUri != null && baseUrl != null)
        {
            fullImageUrl = new Uri(new Uri(baseUrl), profile.ImageUri).ToString();
        }

        return new PlayerViewModel(
            profile.Id,
            profile.Name,
            profile.Colors,
            profile.ImageUri,
            fullImageUrl,
            profile.LifetimeStats
        );
    }

    // ==================== Helper Methods ====================

    /// <summary>
    /// Gets color tuple for renderer consumption (principle of least privilege).
    /// Returns (Primary, Secondary, Foreground) for SVG rendering.
    /// </summary>
    /// <returns>Tuple of (primary, secondary, foreground) color hex strings.</returns>
    public (string primary, string secondary, string foreground) GetRenderColors()
    {
        return (Colors.Primary, Colors.Secondary, Colors.Foreground);
    }
}

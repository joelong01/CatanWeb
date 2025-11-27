namespace Catan3.Shared.Profiles;

/// <summary>
/// Player color scheme used for rendering.
/// Shared between Profiles (storage) and PlayerViewModel (rendering).
/// Maintains document model hierarchy for CosmosDB storage.
/// </summary>
public record PlayerColors(
    string Primary,      // Primary background color (hex: #RRGGBB)
    string Secondary,    // Secondary background/gradient color (hex: #RRGGBB)
    string Foreground    // Foreground/text color (hex: #RRGGBB)
)
{
    /// <summary>
    /// Default gray color scheme.
    /// </summary>
    public static PlayerColors Default { get; } = new("#CCCCCC", "#999999", "#000000");
}

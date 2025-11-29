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

    /// <summary>
    /// SVG linearGradient stop elements (cached, computed once at construction).
    /// Use inside &lt;linearGradient&gt; element.
    /// </summary>
    public string SvgGradientStops { get; } =
        $@"<stop offset=""0%"" style=""stop-color:{Primary};stop-opacity:1"" />" +
        $@"<stop offset=""100%"" style=""stop-color:{Secondary};stop-opacity:1"" />";

    /// <summary>
    /// CSS linear-gradient value (cached, computed once at construction).
    /// Use in style attributes.
    /// </summary>
    public string CssGradient { get; } = $"linear-gradient(135deg, {Primary}, {Secondary})";
}

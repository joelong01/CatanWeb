namespace Catan3.WebUI.Models
{
    /// <summary>
    /// Provides theme-aware asset path resolution.
    /// All game code requests assets through this service rather than hardcoding paths.
    /// This service is format-agnostic - it returns paths, not rendered output.
    /// </summary>
    public interface IAssetService
    {
        /// <summary>
        /// Gets the current theme name (e.g., "classic", "startrek").
        /// </summary>
        string CurrentTheme { get; }

        /// <summary>
        /// Gets the URL path to an asset for use in HTML/CSS (e.g., "/themes/classic/tiles/brick.png").
        /// </summary>
        /// <param name="asset">The asset to retrieve.</param>
        /// <returns>URL path relative to wwwroot.</returns>
        string GetAssetPath(AssetName asset);

        /// <summary>
        /// Gets the MIME type for an asset based on file extension.
        /// </summary>
        /// <param name="asset">The asset to query.</param>
        /// <returns>MIME type string (e.g., "image/png", "image/svg+xml").</returns>
        string GetMimeType(AssetName asset);

        /// <summary>
        /// Gets all available theme names (internal identifiers), excluding "base".
        /// </summary>
        IReadOnlyList<string> AvailableThemes { get; }

        /// <summary>
        /// Gets metadata for a specific theme (display name, description, preview path).
        /// </summary>
        /// <param name="themeName">Theme name (case-insensitive).</param>
        ThemeMetadata GetThemeMetadata(string themeName);

        /// <summary>
        /// Gets metadata for the current theme.
        /// </summary>
        ThemeMetadata GetCurrentThemeMetadata();

        /// <summary>
        /// Sets the current theme.
        /// </summary>
        /// <param name="themeName">Theme name (case-insensitive).</param>
        void SetTheme(string themeName);

        /// <summary>
        /// Event raised when theme changes.
        /// </summary>
        event Action<string>? ThemeChanged;
    }

    /// <summary>
    /// Theme metadata extracted from theme.json for UI display.
    /// </summary>
    public record ThemeMetadata
    {
        public required string Name { get; init; }
        public required string DisplayName { get; init; }
        public string? Description { get; init; }
        public string Preview { get; init; } = "preview.png";
    }

    /// <summary>
    /// Theme definition loaded from theme.json, including asset mappings.
    /// </summary>
    public record ThemeDefinition
    {
        public required string Name { get; init; }
        public required string DisplayName { get; init; }
        public string? Description { get; init; }
        public string Preview { get; init; } = "preview.png";
        public Dictionary<string, string> Assets { get; init; } = new();
    }
}

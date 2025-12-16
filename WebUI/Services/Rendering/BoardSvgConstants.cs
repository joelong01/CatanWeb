namespace Catan3.WebUI.Services.Rendering;

/// <summary>
/// Constants for SVG board rendering - using Desktop app's exact values.
/// SVG scales via viewBox, so we use the same sizes for easy comparison.
/// </summary>
public static class BoardSvgConstants
{
    // Debug flag - set to true to show tile coordinates on the board
    public static bool ShowTileCoordinates = false;

    // Base hex geometry - SAME AS DESKTOP APP
    public const double HexSize = 100;  // OuterHexSize from BoardLayoutProps.cs
    public const double HexWidth = HexSize * 2;
    public static readonly double HexHeight = Math.Sqrt(3) * HexSize;

    // Board positioning
    public const double CenterX = 800;
    public const double CenterY = 700;
    public const double Padding = 5;  // Minimal padding - harbors already extend to edges

    // Inner/Outer hex geometry - MATCHES DESKTOP APP (BoardLayoutProps.cs)
    // From BoardVisualLayout.cs: InnerHexSize = OuterHexSize - TileGap - InnerHexStrokeThickness * 0.5
    /// <summary>
    /// Tile gap creates the thin wood border between hexes.
    /// Used as StrokeThickness for outer hex polygon (matches Desktop TileCtrl.xaml line 54).
    /// The gap becomes visible when roads render in the space between tiles.
    /// </summary>
    public const double TileGap = 2;  // Desktop default: 2
    // Inner hex stroke creates the gap where maple border shows and roads render
    // Gap should be wide enough for road visibility (road-width space)
    public const double InnerHexStrokeThickness = 14;  // Desktop default: 16, reduced for narrower roads
    public const double InnerHexSize = HexSize - TileGap - InnerHexStrokeThickness * 0.5;  // = 91 (slightly narrower roads)

    // Road rendering
    public const double RoadStrokeThickness = 2.0;  // Desktop default: 2.0

    // Settlement/City - SAME AS DESKTOP APP
    public const double BuildingSize = 40;
    public const double SettlementRadius = BuildingSize / 2;  // = 20

    // Number token - matches original working server-side rendering
    // Note: XAML uses 65x65 grid, but we render with radius 30 (not 32.5) for proper visual balance
    public const double NumberTokenRadius = 30;  // From original working code
    public const double NumberTokenOffsetY = 40;  // Circle center offset from tile center (moved down 10px from 50)
    public const double NumberTokenOpacity = 0.85;  // From original working code

    // Font sizes - matches Desktop CatanNumber.xaml
    public const double NumberFontSize = 24;  // Number text
    public const double PipsFontSize = 12;    // Star/pip text

    // Text positioning within the number token circle
    // Number: VerticalAlignment="Top" in XAML, slightly offset from circle center for visual balance
    public const double NumberOffsetY = -10;  // Small adjustment above circle center
    // Pips: Below the number, matching XAML Margin="0,10,0,0" + VerticalAlignment="Center"
    public const double PipsOffsetY = 10;  // Below circle center

    // Stroke widths
    // Note: Outer hex stroke uses TileGap (2px). Inner hex stroke uses InnerHexStrokeThickness (16px, transparent).
    // Two-polygon rendering matches Desktop TileCtrl.xaml (lines 53-60).
    public const double NumberTokenStrokeWidth = 0.5;

    // Colors and patterns
    public const string HexBorderFillPattern = "pattern-maple";  // Maple wood texture for border fill (matches Desktop bmMaple)
    public const string HexBorderStrokePattern = "pattern-cherry";  // Cherry wood texture for border stroke (matches Desktop bmCherry)
    public const string HexHighlightColor = "#FFD700";  // Gold/yellow for highlighted tiles (robber placement)
    public const string NumberTokenFill = "#2F6999";
    public const string NumberTokenStroke = "white";
    public const string HighProbColor = "#c00";
    public const string NormalNumberColor = "#fff";
    public const string BackgroundColor = "transparent";  // Black/transparent like Desktop app

    // Harbor rendering - based on Desktop app HarborCtrl.xaml
    public const double HarborCircleRadius = 35;  // Similar to Desktop's 60x60 ellipse scaled by 1.5
    public const double HarborOffset = 70;  // Distance from edge midpoint toward water
    public const double WaterTriangleSize = 60;  // Size of water background triangle
    public const string WaterColor = "#4169e1";  // Royal blue for water triangle

    // Animation settings
    /// <summary>
    /// Duration in seconds for tile dimming animation after a dice roll.
    /// Non-matching tiles dim for this duration, then return to normal.
    /// </summary>
    public const int TileDimDurationSeconds = 5;
}

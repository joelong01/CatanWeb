using Catan3.Shared.Models;

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

    /// <summary>
    /// Canonical road polygon points centered at origin, aligned for HexSide.BottomRight (angle 0°).
    /// All roads use this same shape, just translated and rotated via SVG transform.
    ///
    /// Geometry: 6-point polygon with triangular tips at vertices.
    /// - Tips are at the outer hex vertices (distance HexSize from tile center)
    /// - Inner points are at InnerHexSize, creating the road "body" width
    ///
    /// For BottomRight edge (vertices 0 and 1, angles 0° and 60°):
    /// - Edge midpoint to vertex distance = HexSize (100)
    /// - Inner inset = HexSize - InnerHexSize (9)
    /// - Perpendicular inward distance = apothem difference
    /// </summary>
    public static readonly string CanonicalRoadPolygon = CalculateCanonicalRoadPolygon();

    /// <summary>
    /// Edge rotation angles for each HexSide, used with SVG rotate transform.
    /// This is the angle of the edge itself (direction from v1 to v2), not the midpoint direction.
    /// Canonical polygon is horizontal (tips at left/right), so we rotate to match edge orientation.
    /// </summary>
    public static readonly Dictionary<HexSide, double> RoadEdgeAngles = new()
    {
        // For flat-top hex, edges run perpendicular to midpoint direction (midpoint angle + 90°)
        // But we need to match v1→v2 direction for consistent polygon orientation
        { HexSide.Top, 0 },            // Edge runs horizontally (v4 to v5, left to right)
        { HexSide.TopRight, 60 },      // Edge runs at 60° (v5 to v0)
        { HexSide.BottomRight, 120 },  // Edge runs at 120° (v0 to v1)
        { HexSide.Bottom, 180 },       // Edge runs horizontally reversed (v1 to v2, but same as 0°)
        { HexSide.BottomLeft, 240 },   // Edge runs at 240° (v2 to v3)
        { HexSide.TopLeft, 300 },      // Edge runs at 300° (v3 to v4)
    };

    /// <summary>
    /// Edge midpoint offsets from tile center for each HexSide.
    /// Distance from center to edge midpoint = apothem = HexSize * sqrt(3) / 2
    /// </summary>
    public static readonly Dictionary<HexSide, (double dx, double dy)> RoadEdgeMidpointOffsets = CalculateEdgeMidpointOffsets();

    private static string CalculateCanonicalRoadPolygon()
    {
        // Canonical polygon for a HORIZONTAL edge (HexSide.Top), centered at edge midpoint.
        // The edge runs left-to-right, tips point left and right.
        //
        // For a flat-top regular hexagon with circumradius R:
        // - Edge length = R
        // - Apothem (center to edge midpoint) = R * sqrt(3) / 2
        // - Distance from edge midpoint to vertex (along edge) = R / 2
        //
        // The road polygon has 6 points:
        // - 2 tips at the outer hex vertices (where roads meet)
        // - 4 inner points at the inner hex vertices (road body width)

        double R = HexSize;  // 100 - outer hex circumradius
        double r = InnerHexSize;  // 91 - inner hex circumradius
        double apothemOuter = R * Math.Sqrt(3) / 2;  // ~86.6
        double apothemInner = r * Math.Sqrt(3) / 2;  // ~78.8

        // For a horizontal edge centered at origin:
        // - Tips are at (±R/2, 0) - the outer hex vertices relative to edge midpoint
        // - Inner points are at the inner hex vertices relative to edge midpoint
        //
        // Inner hex vertices for a horizontal edge:
        // The inner hex has the same shape but scaled by r/R.
        // Inner vertex X = (r/R) * (R/2) = r/2
        // Inner vertex perpendicular distance from edge = apothemOuter - apothemInner

        double tipX = R / 2;  // 50 - tip distance from midpoint along edge
        double innerX = r / 2;  // 45.5 - inner vertex distance from midpoint along edge
        double perpDist = apothemOuter - apothemInner;  // ~7.8 - perpendicular offset for inner vertices

        // 6 points forming the bow-tie shape:
        // tip1 (left) -> innerA1 (left-top) -> innerA2 (right-top) -> tip2 (right) -> innerB2 (right-bottom) -> innerB1 (left-bottom)
        var points = new[]
        {
            (-tipX, 0.0),           // tip1: left vertex (outer hex)
            (-innerX, -perpDist),   // innerA1: left inner vertex, tile A side (above edge for horizontal)
            (innerX, -perpDist),    // innerA2: right inner vertex, tile A side
            (tipX, 0.0),            // tip2: right vertex (outer hex)
            (innerX, perpDist),     // innerB2: right inner vertex, tile B side (below edge for horizontal)
            (-innerX, perpDist)     // innerB1: left inner vertex, tile B side
        };

        return string.Join(" ", points.Select(p => $"{p.Item1:F1},{p.Item2:F1}"));
    }

    private static Dictionary<HexSide, (double dx, double dy)> CalculateEdgeMidpointOffsets()
    {
        double apothem = HexSize * Math.Sqrt(3) / 2;  // Distance from center to edge midpoint
        var result = new Dictionary<HexSide, (double, double)>();

        // Midpoint direction angles (perpendicular to edge direction, pointing outward from center)
        // These are edge rotation angle + 90° (or equivalently, the direction TO the midpoint FROM center)
        var midpointAngles = new Dictionary<HexSide, double>
        {
            { HexSide.Top, 270 },          // Midpoint is directly above center
            { HexSide.TopRight, 330 },     // Midpoint at 330° (upper-right)
            { HexSide.BottomRight, 30 },   // Midpoint at 30° (lower-right)
            { HexSide.Bottom, 90 },        // Midpoint is directly below center
            { HexSide.BottomLeft, 150 },   // Midpoint at 150° (lower-left)
            { HexSide.TopLeft, 210 },      // Midpoint at 210° (upper-left)
        };

        foreach (var kvp in midpointAngles)
        {
            double angleRad = kvp.Value * Math.PI / 180;
            double dx = apothem * Math.Cos(angleRad);
            double dy = apothem * Math.Sin(angleRad);
            result[kvp.Key] = (dx, dy);
        }

        return result;
    }

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

    // Pattern ID helpers - centralized to avoid duplication across renderers

    /// <summary>
    /// Gets the SVG pattern ID for a tile's resource type.
    /// </summary>
    public static string GetPatternId(ResourceType resourceType) => resourceType switch
    {
        ResourceType.Brick => "tile-brick",
        ResourceType.Wheat => "tile-wheat",
        ResourceType.Wood => "tile-wood",
        ResourceType.Ore => "tile-ore",
        ResourceType.Sheep => "tile-sheep",
        ResourceType.Desert => "tile-desert",
        ResourceType.GoldMine => "tile-goldmine",
        ResourceType.Sea => "tile-sea",
        _ => "tile-default"
    };

    /// <summary>
    /// Gets the SVG pattern ID for a harbor's type.
    /// </summary>
    public static string GetHarborPatternId(HarborType harborType) => harborType switch
    {
        HarborType.Brick => "harbor-brick",
        HarborType.Ore => "harbor-ore",
        HarborType.Sheep => "harbor-sheep",
        HarborType.Wheat => "harbor-wheat",
        HarborType.Wood => "harbor-wood",
        HarborType.ThreeForOne => "harbor-3for1",
        _ => "harbor-3for1"
    };
}

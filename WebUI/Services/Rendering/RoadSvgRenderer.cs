using System.Text;
using Catan3.Shared.Models;
using Catan3.Shared.ViewData;

namespace Catan3.WebUI.Services.Rendering;

/// <summary>
/// Extension methods for rendering RoadModel as SVG elements with support for player colors,
/// stroke thickness, opacity, and build indices. Mirrors DesktopApp RoadCtrl.xaml functionality.
/// </summary>
public static class RoadSvgRenderer
{
    private const double HexSize = BoardSvgConstants.HexSize;
    private const double CenterX = BoardSvgConstants.CenterX;
    private const double CenterY = BoardSvgConstants.CenterY;
    private const double InnerHexSize = BoardSvgConstants.InnerHexSize;

    /// <summary>
    /// Renders this road as SVG markup.
    /// </summary>
    /// <param name="road">The road model to render.</param>
    /// <param name="playerData">Player profile data for colors.</param>
    /// <param name="buildIndex">Build order index to display (0 = don't show).</param>
    /// <param name="opacity">Optional opacity override (default 0.0 for debugging - shows on hover).</param>
    /// <returns>SVG markup string for the road.</returns>
    public static string RenderSvg(
        this RoadModel road,
        PlayerProfile? playerData,
        int buildIndex = 0,
        double opacity = 0.0)
    {
        var sb = new StringBuilder();
        var (v1, v2) = GetEdgeVertices(road.RoadKey);

        // Road group with CSS class and hover support
        sb.AppendLine($@"  <g class=""road road-hover"" data-player=""{road.OwnerId}"">");

        // Render road polygon with player colors
        RenderRoadPolygon(sb, v1, v2, playerData, opacity);

        // Render build index if provided
        if (buildIndex > 0)
        {
            RenderBuildIndex(sb, v1, v2, buildIndex, playerData);
        }

        sb.AppendLine("  </g>");
        return sb.ToString();
    }

    /// <summary>
    /// Renders the road polygon with player colors.
    /// Uses 6-point polygon: tips at outer vertices, body at inner hex boundaries.
    /// </summary>
    private static void RenderRoadPolygon(StringBuilder sb, (double x, double y) v1, (double x, double y) v2, PlayerProfile? playerData, double opacity)
    {
        var roadPolygon = GenerateRoadPolygon(v1, v2);

        // Use player colors if available, otherwise fallback to gray
        var fillColor = playerData?.PrimaryBackgroundColor ?? "#999999";
        var strokeColor = playerData?.SecondaryBackgroundColor ?? "#666666";
        var strokeWidth = 2;

        sb.AppendLine($@"    <polygon points=""{roadPolygon}"" fill=""{fillColor}"" stroke=""{strokeColor}"" stroke-width=""{strokeWidth}"" opacity=""{opacity}""/>");
    }

    /// <summary>
    /// Renders build index number on the road, rotated to match road orientation.
    /// </summary>
    private static void RenderBuildIndex(StringBuilder sb, (double x, double y) v1, (double x, double y) v2, int buildIndex, PlayerProfile? playerData)
    {
        // Calculate road midpoint
        var midX = (v1.x + v2.x) / 2;
        var midY = (v1.y + v2.y) / 2;

        // Calculate rotation angle to align text with road
        var dx = v2.x - v1.x;
        var dy = v2.y - v1.y;
        var angle = Math.Atan2(dy, dx) * 180 / Math.PI;

        var textColor = playerData?.ForegroundColor ?? "#FFFFFF";

        // Render text with rotation transform
        sb.AppendLine($@"    <text x=""{midX}"" y=""{midY}"" text-anchor=""middle"" dominant-baseline=""middle"" font-family=""sans-serif"" font-size=""14"" font-weight=""bold"" fill=""{textColor}"" stroke=""black"" stroke-width=""0.5"" transform=""rotate({angle:F1} {midX} {midY})"">{buildIndex}</text>");
    }

    /// <summary>
    /// Gets the two vertex positions for a road edge.
    /// </summary>
    private static ((double x, double y) v1, (double x, double y) v2) GetEdgeVertices(RoadKey roadKey)
    {
        // Convert tile axial coordinates to pixel position
        var (tileX, tileY) = BoardGeometry.AxialToPixel(roadKey.TileKey.Q, roadKey.TileKey.R);

        // Get hex vertices
        var vertices = BoardGeometry.GetHexVertices(tileX, tileY);

        // Map HexSide to vertex indices
        var (v1Idx, v2Idx) = roadKey.HexSide switch
        {
            HexSide.Top => (4, 5),           // TopLeft, TopRight
            HexSide.TopRight => (5, 0),      // TopRight, Right
            HexSide.BottomRight => (0, 1),   // Right, BottomRight
            HexSide.Bottom => (1, 2),        // BottomRight, BottomLeft
            HexSide.BottomLeft => (2, 3),    // BottomLeft, Left
            HexSide.TopLeft => (3, 4),       // Left, TopLeft
            _ => (0, 1)
        };

        return (vertices[v1Idx], vertices[v2Idx]);
    }

    /// <summary>
    /// Generates 6-point polygon for road using inner/outer hex geometry.
    /// Tips at outer vertices, body at inner hex boundaries.
    /// </summary>
    private static string GenerateRoadPolygon((double x, double y) v1, (double x, double y) v2)
    {
        // Edge midpoint and perpendicular direction
        var midX = (v1.x + v2.x) / 2;
        var midY = (v1.y + v2.y) / 2;
        var dx = v2.x - v1.x;
        var dy = v2.y - v1.y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        var perpX = -dy / length;  // Perpendicular (90° CCW)
        var perpY = dx / length;

        // Calculate tile centers on both sides of the edge
        double apothem = HexSize * Math.Sqrt(3) / 2;
        var centerAx = midX + perpX * apothem;
        var centerAy = midY + perpY * apothem;
        var centerBx = midX - perpX * apothem;
        var centerBy = midY - perpY * apothem;

        // Inner/outer ratio for scaling vertices toward center
        double ratio = InnerHexSize / HexSize;
        double oneMinusRatio = 1 - ratio;

        // Calculate inner vertices by scaling outer vertices toward their respective tile centers
        var i1Ax = v1.x * ratio + centerAx * oneMinusRatio;
        var i1Ay = v1.y * ratio + centerAy * oneMinusRatio;
        var i2Ax = v2.x * ratio + centerAx * oneMinusRatio;
        var i2Ay = v2.y * ratio + centerAy * oneMinusRatio;

        var i1Bx = v1.x * ratio + centerBx * oneMinusRatio;
        var i1By = v1.y * ratio + centerBy * oneMinusRatio;
        var i2Bx = v2.x * ratio + centerBx * oneMinusRatio;
        var i2By = v2.y * ratio + centerBy * oneMinusRatio;

        // 6 points: outer tips and inner body edges
        var points = new List<string>
        {
            $"{v1.x:F1},{v1.y:F1}",      // O1 (outer vertex 1 - tip)
            $"{i1Ax:F1},{i1Ay:F1}",      // I1_A (tile A's inner vertex at v1)
            $"{i2Ax:F1},{i2Ay:F1}",      // I2_A (tile A's inner vertex at v2)
            $"{v2.x:F1},{v2.y:F1}",      // O2 (outer vertex 2 - tip)
            $"{i2Bx:F1},{i2By:F1}",      // I2_B (tile B's inner vertex at v2)
            $"{i1Bx:F1},{i1By:F1}"       // I1_B (tile B's inner vertex at v1)
        };

        return string.Join(" ", points);
    }

}

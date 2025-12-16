using System.Text;
using Catan3.Shared.Models;
using Catan3.Shared.Profiles;
using Catan3.WebUI.Models;

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
    /// <param name="playerViewModel">Player view model for colors.</param>
    /// <param name="buildIndex">Build order index to display (0 = don't show).</param>
    /// <param name="opacity">Optional opacity override (default 0.0 for debugging - shows on hover).</param>
    /// <returns>SVG markup string for the road.</returns>
    public static string RenderSvg(
        this RoadModel road,
        PlayerViewModel? playerViewModel,
        int buildIndex = 0,
        double opacity = 0.0)
    {
        var sb = new StringBuilder();
        var (tileX, tileY) = BoardGeometry.AxialToPixel(road.RoadKey.TileKey.Q, road.RoadKey.TileKey.R);
        var (v1, v2) = GetEdgeVertices(road.RoadKey);

        // Road group with CSS class and hover support
        sb.AppendLine($@"  <g class=""road road-hover"" data-player=""{road.OwnerId}"">");

        // Render road polygon with player colors
        RenderRoadPolygon(sb, v1, v2, tileX, tileY, playerViewModel, opacity);

        // Render build index if provided
        if (buildIndex > 0)
        {
            RenderBuildIndex(sb, v1, v2, buildIndex, playerViewModel);
        }

        sb.AppendLine("  </g>");
        return sb.ToString();
    }

    /// <summary>
    /// Renders the road polygon with player colors.
    /// Uses 6-point polygon: tips at outer vertices, body at inner hex boundaries.
    /// </summary>
    private static void RenderRoadPolygon(StringBuilder sb, (double x, double y) v1, (double x, double y) v2, double tileX, double tileY, PlayerViewModel? playerViewModel, double opacity)
    {
        var roadPolygon = GenerateRoadPolygon(v1, v2, tileX, tileY);

        // Use player colors if available, otherwise fallback to gray
        var fillColor = playerViewModel?.Colors.Primary ?? "#999999";
        var strokeColor = playerViewModel?.Colors.Secondary ?? "#666666";
        var strokeWidth = BoardSvgConstants.RoadStrokeThickness;

        // Use CSS class for opacity so hover can override it
        var opacityClass = opacity < 0.5 ? "road-hidden" : "";

        sb.AppendLine($@"    <polygon class=""{opacityClass}"" points=""{roadPolygon}"" fill=""{fillColor}"" stroke=""{strokeColor}"" stroke-width=""{strokeWidth}""/>");
    }

    /// <summary>
    /// Renders build index number on the road.
    /// Text is always horizontal (not rotated) for readability, matching Desktop RoadCtrl.xaml.
    /// </summary>
    private static void RenderBuildIndex(StringBuilder sb, (double x, double y) v1, (double x, double y) v2, int buildIndex, PlayerViewModel? playerViewModel)
    {
        // Calculate road midpoint
        var midX = (v1.x + v2.x) / 2;
        var midY = (v1.y + v2.y) / 2;

        // Render black rounded rect background with white text (always horizontal, no rotation)
        var rectSize = 20;
        var rectX = midX - rectSize / 2;
        var rectY = midY - rectSize / 2;

        sb.AppendLine($@"    <rect x=""{rectX:F1}"" y=""{rectY:F1}"" width=""{rectSize}"" height=""{rectSize}"" rx=""5"" fill=""black""/>");
        sb.AppendLine($@"    <text x=""{midX:F1}"" y=""{midY:F1}"" text-anchor=""middle"" dominant-baseline=""central"" font-family=""sans-serif"" font-size=""14"" font-weight=""bold"" fill=""white"">{buildIndex}</text>");
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
    /// Creates a hexagonal road shape with triangular tips at the vertices.
    /// The road fills the gap between two adjacent tiles' inner hexagons.
    /// </summary>
    private static string GenerateRoadPolygon((double x, double y) v1, (double x, double y) v2, double tileX, double tileY)
    {
        // Get inner hex vertices for this tile (side A)
        var innerVerticesA = BoardGeometry.GetHexVertices(tileX, tileY, InnerHexSize);

        // Find which vertex indices correspond to v1 and v2
        var outerVertices = BoardGeometry.GetHexVertices(tileX, tileY, HexSize);
        int idx1 = -1, idx2 = -1;
        for (int i = 0; i < 6; i++)
        {
            if (Math.Abs(outerVertices[i].x - v1.x) < 0.1 && Math.Abs(outerVertices[i].y - v1.y) < 0.1)
                idx1 = i;
            if (Math.Abs(outerVertices[i].x - v2.x) < 0.1 && Math.Abs(outerVertices[i].y - v2.y) < 0.1)
                idx2 = i;
        }

        // Get inner vertices on side A (this tile)
        var innerA1 = innerVerticesA[idx1 >= 0 ? idx1 : 0];
        var innerA2 = innerVerticesA[idx2 >= 0 ? idx2 : 1];

        // Calculate adjacent tile center (side B)
        // The adjacent tile is on the opposite side of the edge
        var midX = (v1.x + v2.x) / 2;
        var midY = (v1.y + v2.y) / 2;
        var dx = v2.x - v1.x;
        var dy = v2.y - v1.y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        var perpX = -dy / length;  // Perpendicular unit vector
        var perpY = dx / length;

        // Distance from edge midpoint to tile center (apothem)
        double apothem = HexSize * Math.Sqrt(3) / 2;

        // Adjacent tile center is on the opposite side of the edge from this tile
        // Determine which direction by checking if tile center is on positive or negative side
        var toTileCenterX = tileX - midX;
        var toTileCenterY = tileY - midY;
        var dotProduct = toTileCenterX * perpX + toTileCenterY * perpY;

        // Adjacent tile center is in the opposite direction
        var adjTileX = midX - Math.Sign(dotProduct) * perpX * apothem;
        var adjTileY = midY - Math.Sign(dotProduct) * perpY * apothem;

        // Get inner hex vertices for adjacent tile (side B)
        var innerVerticesB = BoardGeometry.GetHexVertices(adjTileX, adjTileY, InnerHexSize);

        // Find matching inner vertices on side B
        // They should be at the same outer vertex positions
        (double x, double y) innerB1 = innerVerticesB[0], innerB2 = innerVerticesB[0];
        double minDist1 = double.MaxValue, minDist2 = double.MaxValue;
        foreach (var iv in innerVerticesB)
        {
            var d1 = Math.Sqrt(Math.Pow(iv.x - v1.x, 2) + Math.Pow(iv.y - v1.y, 2));
            var d2 = Math.Sqrt(Math.Pow(iv.x - v2.x, 2) + Math.Pow(iv.y - v2.y, 2));
            if (d1 < minDist1) { minDist1 = d1; innerB1 = iv; }
            if (d2 < minDist2) { minDist2 = d2; innerB2 = iv; }
        }

        // 6 points forming a bow-tie/hexagonal shape:
        // Outer tip 1 -> Inner A1 -> Inner A2 -> Outer tip 2 -> Inner B2 -> Inner B1
        var points = new List<string>
        {
            $"{v1.x:F1},{v1.y:F1}",            // Outer tip at vertex 1
            $"{innerA1.x:F1},{innerA1.y:F1}",  // Inner vertex 1 (tile A)
            $"{innerA2.x:F1},{innerA2.y:F1}",  // Inner vertex 2 (tile A)
            $"{v2.x:F1},{v2.y:F1}",            // Outer tip at vertex 2
            $"{innerB2.x:F1},{innerB2.y:F1}",  // Inner vertex 2 (tile B)
            $"{innerB1.x:F1},{innerB1.y:F1}"   // Inner vertex 1 (tile B)
        };

        return string.Join(" ", points);
    }

}

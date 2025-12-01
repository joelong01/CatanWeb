using Catan3.Shared.Models;

namespace Catan3.WebUI.Services.Rendering;

/// <summary>
/// Shared geometry helper methods for SVG board rendering.
/// Centralizes coordinate conversion and hex vertex calculations to avoid duplication.
/// </summary>
public static class BoardGeometry
{
    private const double HexSize = BoardSvgConstants.HexSize;
    private const double CenterX = BoardSvgConstants.CenterX;
    private const double CenterY = BoardSvgConstants.CenterY;

    /// <summary>
    /// Converts axial coordinates to pixel position.
    /// </summary>
    public static (double x, double y) AxialToPixel(int q, int r)
    {
        double x = HexSize * (3.0 / 2 * q);
        double y = HexSize * (Math.Sqrt(3) / 2 * q + Math.Sqrt(3) * r);
        return (x + CenterX, y + CenterY);
    }

    /// <summary>
    /// Gets hex vertices for a tile at the given center position and size.
    /// </summary>
    /// <param name="cx">Center X coordinate</param>
    /// <param name="cy">Center Y coordinate</param>
    /// <param name="size">Hex size (defaults to standard HexSize)</param>
    public static List<(double x, double y)> GetHexVertices(double cx, double cy, double? size = null)
    {
        var hexSize = size ?? HexSize;
        var vertices = new List<(double x, double y)>();
        for (int i = 0; i < 6; i++)
        {
            double angle = Math.PI / 180 * (60 * i);
            double x = cx + hexSize * Math.Cos(angle);
            double y = cy + hexSize * Math.Sin(angle);
            vertices.Add((x, y));
        }
        return vertices;
    }

    /// <summary>
    /// Generates SVG path for hexagon at given center position and size.
    /// </summary>
    /// <param name="cx">Center X coordinate</param>
    /// <param name="cy">Center Y coordinate</param>
    /// <param name="size">Hex size</param>
    public static string GenerateHexPath(double cx, double cy, double size)
    {
        var vertices = GetHexVertices(cx, cy, size);
        var points = vertices.Select(v => $"{v.x:F1},{v.y:F1}").ToList();
        return $"M {points[0]} L {string.Join(" L ", points.Skip(1))} Z";
    }

    /// <summary>
    /// Maps HexSide to the two vertex indices for flat-top hex orientation.
    /// </summary>
    public static (int v1Idx, int v2Idx) GetEdgeVerticesForSide(HexSide side)
    {
        return side switch
        {
            HexSide.Top => (4, 5),
            HexSide.TopRight => (5, 0),
            HexSide.BottomRight => (0, 1),
            HexSide.Bottom => (1, 2),
            HexSide.BottomLeft => (2, 3),
            HexSide.TopLeft => (3, 4),
            _ => (0, 1)
        };
    }

    /// <summary>
    /// Gets pixel position for a building vertex in SVG coordinate space.
    /// </summary>
    public static (double x, double y) GetVertexPosition(BuildingKey buildingKey)
    {
        // Convert tile axial coordinates to pixel position
        var (tileX, tileY) = AxialToPixel(buildingKey.HexCoordinates.Q, buildingKey.HexCoordinates.R);

        // Get hex vertices
        var vertices = GetHexVertices(tileX, tileY);

        // Map HexPosition to vertex index (0-5)
        var vertexIndex = buildingKey.Position switch
        {
            HexPosition.Right => 0,
            HexPosition.BottomRight => 1,
            HexPosition.BottomLeft => 2,
            HexPosition.Left => 3,
            HexPosition.TopLeft => 4,
            HexPosition.TopRight => 5,
            _ => 0
        };

        return vertices[vertexIndex];
    }
}

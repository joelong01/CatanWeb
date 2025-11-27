using System.Text;
using Catan3.Shared.Models;

namespace Catan3.WebUI.Services.Rendering;

/// <summary>
/// Extension methods for rendering HarborModel as SVG elements positioned at board edges
/// with water triangle backgrounds and circular pattern fills. Mirrors DesktopApp HarborCtrl.xaml functionality.
/// </summary>
public static class HarborSvgRenderer
{
    private const double HexSize = BoardSvgConstants.HexSize;
    private const double CenterX = BoardSvgConstants.CenterX;
    private const double CenterY = BoardSvgConstants.CenterY;

    /// <summary>
    /// Renders this harbor as SVG markup.
    /// </summary>
    /// <param name="harbor">The harbor model to render.</param>
    /// <returns>SVG markup string for the harbor.</returns>
    public static string RenderSvg(this HarborModel harbor)
    {
        if (harbor.HarborKey.HarborType == HarborType.None)
            return string.Empty;

        var sb = new StringBuilder();

        // Get tile center position
        var (cx, cy) = BoardGeometry.AxialToPixel(harbor.HarborKey.HexCoordinates.Q, harbor.HarborKey.HexCoordinates.R);
        var hexVertices = BoardGeometry.GetHexVertices(cx, cy);

        // Get the two vertices for this edge based on HexSide
        var (v1Idx, v2Idx) = BoardGeometry.GetEdgeVerticesForSide(harbor.HarborKey.Side);
        var v1 = hexVertices[v1Idx];
        var v2 = hexVertices[v2Idx];

        // Calculate edge midpoint
        var midX = (v1.x + v2.x) / 2;
        var midY = (v1.y + v2.y) / 2;

        // Calculate direction from tile center to edge midpoint (outward direction)
        var dx = midX - cx;
        var dy = midY - cy;
        var length = Math.Sqrt(dx * dx + dy * dy);
        var normX = dx / length;
        var normY = dy / length;

        // Harbor position is offset outward from edge midpoint
        var harborX = midX + normX * BoardSvgConstants.HarborOffset;
        var harborY = midY + normY * BoardSvgConstants.HarborOffset;

        // Harbor group
        sb.AppendLine($@"  <g class=""harbor"" data-type=""{harbor.HarborKey.HarborType}"">");

        // Draw water triangle background connecting tile edge to harbor circle
        var trianglePoints = $"{v1.x:F1},{v1.y:F1} {v2.x:F1},{v2.y:F1} {harborX:F1},{harborY:F1}";
        sb.AppendLine($@"    <polygon points=""{trianglePoints}"" fill=""{BoardSvgConstants.WaterColor}""/>");

        // Draw harbor circle with pattern
        var patternId = GetHarborPatternId(harbor.HarborKey.HarborType);
        sb.AppendLine($@"    <circle cx=""{harborX:F1}"" cy=""{harborY:F1}"" r=""{BoardSvgConstants.HarborCircleRadius}"" fill=""url(#{patternId})"" stroke=""#2a5d8f"" stroke-width=""3""/>");

        sb.AppendLine("  </g>");
        return sb.ToString();
    }

    /// <summary>
    /// Gets SVG pattern ID for a harbor type.
    /// </summary>
    private static string GetHarborPatternId(HarborType harborType)
    {
        return harborType switch
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

}

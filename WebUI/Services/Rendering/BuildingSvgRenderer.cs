using System.Text;
using Catan3.Shared.Models;
using Catan3.Shared.ViewData;

namespace Catan3.WebUI.Services.Rendering;

/// <summary>
/// Visual state for building rendering.
/// Maps to Desktop BuildingCtrl.xaml BuildingVisualState.
/// </summary>
public enum BuildingVisualState
{
    /// <summary>Normal visible building.</summary>
    Normal,
    /// <summary>Highlighted building (e.g., during placement).</summary>
    Highlighted,
    /// <summary>Hidden building (below star threshold).</summary>
    Hidden,
    /// <summary>Show stars only (no settlement/city SVG).</summary>
    Stars
}

/// <summary>
/// Extension methods for rendering BuildingModel as SVG elements with support for player colors,
/// visual states, and build indices. Mirrors DesktopApp BuildingCtrl.xaml functionality.
/// </summary>
public static class BuildingSvgRenderer
{
    private const double HexSize = BoardSvgConstants.HexSize;
    private const double CenterX = BoardSvgConstants.CenterX;
    private const double CenterY = BoardSvgConstants.CenterY;
    private const double BuildingSize = BoardSvgConstants.BuildingSize;

    /// <summary>
    /// Renders this building as SVG markup.
    /// </summary>
    /// <param name="building">The building model to render.</param>
    /// <param name="playerData">Player profile data for colors.</param>
    /// <param name="visualState">Visual state controlling visibility and rendering mode.</param>
    /// <param name="stars">Number of stars for this building (-1 = not calculated/not needed).</param>
    /// <param name="buildIndex">Build order index to display (0 = don't show).</param>
    /// <returns>SVG markup string for the building.</returns>
    public static string RenderSvg(
        this BuildingModel building,
        PlayerProfile? playerData,
        BuildingVisualState visualState,
        int stars = -1,
        int buildIndex = 0)
    {
        if (visualState == BuildingVisualState.Hidden)
            return string.Empty;

        var sb = new StringBuilder();
        var (x, y) = GetVertexPosition(building.BuildingKey);

        // Building group with CSS class for animations
        var cssClass = visualState == BuildingVisualState.Highlighted ? "building building-highlighted" : "building";
        sb.AppendLine($@"  <g class=""{cssClass}"" data-player=""{building.OwnerId}"">");

        if (visualState == BuildingVisualState.Stars)
        {
            // Render stars only (no building glyph)
            RenderStars(sb, stars, x, y);
        }
        else
        {
            // Render full building with player colors
            RenderBuildingGlyph(sb, building, playerData, x, y);

            // Render build index if provided
            if (buildIndex > 0)
            {
                RenderBuildIndex(sb, x, y, buildIndex, playerData);
            }
        }

        sb.AppendLine("  </g>");
        return sb.ToString();
    }

    /// <summary>
    /// Renders the building glyph (settlement or city SVG) with player gradient background.
    /// </summary>
    private static void RenderBuildingGlyph(StringBuilder sb, BuildingModel building, PlayerProfile? playerData, double x, double y)
    {
        var radius = BuildingSize / 2;
        var gradientId = $"gradient-{building.OwnerId}";

        // Render circular gradient background
        if (playerData != null)
        {
            sb.AppendLine($@"    <circle cx=""{x}"" cy=""{y}"" r=""{radius}"" fill=""url(#{gradientId})"" stroke=""{playerData.ForegroundColor}"" stroke-width=""2""/>");
        }
        else
        {
            // Fallback: gray circle if no player data
            sb.AppendLine($@"    <circle cx=""{x}"" cy=""{y}"" r=""{radius}"" fill=""#cccccc"" stroke=""#333333"" stroke-width=""2""/>");
        }

        // Render settlement or city SVG inside circle
        var svgFile = building.BuildingState == BuildingState.City ? "city.svg" : "settlement.svg";
        var svgSize = BuildingSize * 0.6;  // SVG takes 60% of circle
        var svgX = x - svgSize / 2;
        var svgY = y - svgSize / 2;

        // Embed SVG as image - loaded from WebUI wwwroot/images/svg
        sb.AppendLine($@"    <image href=""/images/svg/{svgFile}"" x=""{svgX}"" y=""{svgY}"" width=""{svgSize}"" height=""{svgSize}""/>");
    }

    /// <summary>
    /// Renders stars as numeric count (for Stars visual state).
    /// Matches Desktop BuildingViewModel.BIND_StateGlyph (line 110): glyph = stars.ToString()
    /// </summary>
    private static void RenderStars(StringBuilder sb, int stars, double x, double y)
    {
        if (stars <= 0)
            return;

        // Desktop renders numeric star count, not star symbols
        sb.AppendLine($@"    <text x=""{x}"" y=""{y}"" text-anchor=""middle"" dominant-baseline=""middle"" font-size=""20"" font-weight=""bold"" fill=""gold"" stroke=""black"" stroke-width=""0.8"">{stars}</text>");
    }

    /// <summary>
    /// Renders build index number inside or near the building.
    /// </summary>
    private static void RenderBuildIndex(StringBuilder sb, double x, double y, int buildIndex, PlayerProfile? playerData)
    {
        var textColor = playerData?.ForegroundColor ?? "#FFFFFF";
        sb.AppendLine($@"    <text x=""{x}"" y=""{y}"" text-anchor=""middle"" dominant-baseline=""middle"" font-family=""sans-serif"" font-size=""14"" font-weight=""bold"" fill=""{textColor}"" stroke=""black"" stroke-width=""0.5"">{buildIndex}</text>");
    }

    /// <summary>
    /// Gets pixel position for a building vertex.
    /// </summary>
    private static (double x, double y)GetVertexPosition(BuildingKey buildingKey)
    {
        // Convert tile axial coordinates to pixel position
        var (tileX, tileY) = BoardGeometry.AxialToPixel(buildingKey.HexCoordinates.Q, buildingKey.HexCoordinates.R);

        // Get hex vertices
        var vertices = BoardGeometry.GetHexVertices(tileX, tileY);

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

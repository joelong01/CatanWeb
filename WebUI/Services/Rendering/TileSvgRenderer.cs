using System.Text;
using Catan3.Shared.Models;

namespace Catan3.WebUI.Services.Rendering;

/// <summary>
/// Extension methods for rendering TileModel as SVG elements with support for animations,
/// coordinates display, and temporary gold tiles feature. Mirrors DesktopApp TileCtrl.xaml functionality.
/// </summary>
public static class TileSvgRenderer
{
    private const double HexSize = BoardSvgConstants.HexSize;
    private static readonly double HexHeight = BoardSvgConstants.HexHeight;
    private const double CenterX = BoardSvgConstants.CenterX;
    private const double CenterY = BoardSvgConstants.CenterY;

    /// <summary>
    /// Renders this tile as SVG markup.
    /// </summary>
    /// <param name="tile">The tile model to render.</param>
    /// <param name="isDimmed">If true, applies dim CSS class for dim animation.</param>
    /// <param name="index">Tile index for verbal reference (always rendered, visibility controlled by CSS).</param>
    /// <param name="isFlipped">If true, applies flip animation (Grief Dodgy feature).</param>
    /// <returns>SVG markup string for the tile.</returns>
    public static string RenderSvg(this TileModel tile, bool isDimmed = false, int index = 0, bool isFlipped = false)
    {
        var sb = new StringBuilder();
        var (x, y) = BoardGeometry.AxialToPixel(tile.TileKey.Q, tile.TileKey.R);

        // Tile group with CSS class for animations
        var cssClasses = new List<string> { "tile" };
        if (isDimmed) cssClasses.Add("tile-dimmed");
        if (isFlipped) cssClasses.Add("grief-flip");
        var cssClass = string.Join(" ", cssClasses);

        sb.AppendLine($@"  <g class=""{cssClass}"" data-q=""{tile.TileKey.Q}"" data-r=""{tile.TileKey.R}"" transform-origin=""{x} {y}"">");

        // For flip animation, wrap front content in a group
        if (isFlipped)
        {
            sb.AppendLine($@"    <g class=""tile-front"" transform-origin=""{x} {y}"">");
        }

        // Render hex background (gold if TemporarilyGold is true)
        RenderHexBackground(sb, tile, x, y);

        // Render number token (if applicable)
        if (tile.ResourceTileType != ResourceType.Desert &&
            tile.ResourceTileType != ResourceType.Sea &&
            tile.Number > 0)
        {
            RenderNumberToken(sb, tile, x, y);
        }

        // Render tile coordinates (if debug flag is enabled)
        if (BoardSvgConstants.ShowTileCoordinates)
        {
            RenderCoordinates(sb, tile, x, y);
        }

        // Always render tile index (visibility controlled by CSS class on parent)
        // Only for resource tiles (not sea/desert)
        if (tile.ResourceTileType != ResourceType.Sea)
        {
            RenderTileIndex(sb, x, y, index);
        }

        if (isFlipped)
        {
            sb.AppendLine("    </g>"); // Close tile-front

            // Render tile back (water pattern)
            RenderTileBack(sb, x, y);
        }

        // Note: Gold indicator card is rendered by GoldTilesLayer, not here,
        // to avoid duplication and ensure proper image rendering

        sb.AppendLine("  </g>");
        return sb.ToString();
    }

    /// <summary>
    /// Renders the back of a tile (water pattern) for flip animation.
    /// </summary>
    private static void RenderTileBack(StringBuilder sb, double x, double y)
    {
        sb.AppendLine($@"    <g class=""tile-back"" transform-origin=""{x} {y}"" style=""opacity: 0"">");

        // Outer hex border (same as front)
        var outerPath = BoardGeometry.GenerateHexPath(x, y, HexSize);
        sb.AppendLine($@"      <path d=""{outerPath}"" fill=""url(#{BoardSvgConstants.HexBorderFillPattern})"" stroke=""url(#{BoardSvgConstants.HexBorderStrokePattern})"" stroke-width=""{BoardSvgConstants.TileGap}""/>");

        // Inner hex - water pattern
        var innerPath = BoardGeometry.GenerateHexPath(x, y, BoardSvgConstants.InnerHexSize);
        sb.AppendLine($@"      <path d=""{innerPath}"" fill=""url(#pattern-water)"" stroke=""transparent"" stroke-width=""{BoardSvgConstants.InnerHexStrokeThickness}""/>");

        sb.AppendLine("    </g>");
    }

    /// <summary>
    /// Renders the hexagonal tile background with two polygons:
    /// 1. Outer hex (wood border, can be highlighted yellow)
    /// 2. Inner hex (resource fill, creates clean spacing)
    /// Matches Desktop TileCtrl.xaml two-polygon rendering (lines 53-60).
    /// </summary>
    private static void RenderHexBackground(StringBuilder sb, TileModel tile, double x, double y)
    {
        // Use gold pattern if this is a temporary gold tile, otherwise use tile's resource type
        var displayResource = tile.TemporarilyGold ? ResourceType.GoldMine : tile.ResourceTileType;
        var patternId = GetPatternId(displayResource);
        var resourceFill = patternId != "tile-default"
            ? $"url(#{patternId})"
            : GetResourceColor(displayResource);

        // Outer hex border (maple fill + cherry stroke, or highlight color)
        var borderFill = tile.Highlighted
            ? BoardSvgConstants.HexHighlightColor  // Highlight color for selected/valid tiles
            : $"url(#{BoardSvgConstants.HexBorderFillPattern})";  // Maple wood texture fill

        var borderStroke = tile.Highlighted
            ? BoardSvgConstants.HexHighlightColor  // Highlight color for selected/valid tiles
            : $"url(#{BoardSvgConstants.HexBorderStrokePattern})";  // Cherry wood texture stroke

        var outerPath = BoardGeometry.GenerateHexPath(x, y, HexSize);
        sb.AppendLine($@"    <path d=""{outerPath}"" fill=""{borderFill}"" stroke=""{borderStroke}"" stroke-width=""{BoardSvgConstants.TileGap}""/>");

        // Inner hex - resource fill with transparent stroke creating gap
        var innerPath = BoardGeometry.GenerateHexPath(x, y, BoardSvgConstants.InnerHexSize);
        sb.AppendLine($@"    <path d=""{innerPath}"" fill=""{resourceFill}"" stroke=""transparent"" stroke-width=""{BoardSvgConstants.InnerHexStrokeThickness}""/>");
    }

    /// <summary>
    /// Renders the number token with roll number and probability stars.
    /// Uses CatanNumberSvg for consistent rendering across the app.
    /// </summary>
    private static void RenderNumberToken(StringBuilder sb, TileModel tile, double x, double y)
    {
        var numberY = y - BoardSvgConstants.NumberTokenOffsetY;

        // Render as a positioned group using the shared CatanNumberSvg helper
        sb.AppendLine($@"    <g transform=""translate({x},{numberY})"">");
        sb.Append(CatanNumberSvg.Render(tile.Number, BoardSvgConstants.NumberTokenRadius));
        sb.AppendLine("    </g>");
    }

    /// <summary>
    /// Renders tile coordinates (Q,R) at the bottom of the tile for debugging/reference.
    /// </summary>
    private static void RenderCoordinates(StringBuilder sb, TileModel tile, double x, double y)
    {
        var coordText = $"({tile.TileKey.Q},{tile.TileKey.R})";
        var coordY = y + HexHeight / 2 - 10;  // Bottom of tile, 10px up
        sb.AppendLine($@"    <text x=""{x}"" y=""{coordY}"" text-anchor=""middle"" font-family=""sans-serif"" font-size=""12"" fill=""white"" stroke=""black"" stroke-width=""0.5"">{coordText}</text>");
    }

    /// <summary>
    /// Renders tile index label at bottom of tile (white text on black rounded rect).
    /// Hidden by default; visibility controlled by CSS class "show-tile-indexes" on parent.
    /// </summary>
    private static void RenderTileIndex(StringBuilder sb, double x, double y, int index)
    {
        // Position at bottom of tile
        var indexY = y + HexHeight / 2 - 15;
        const double rectWidth = 28;
        const double rectHeight = 22;
        const double rectRadius = 5;

        // Group with class for CSS visibility control
        sb.AppendLine($@"    <g class=""tile-index"">");

        // Black rounded rect background
        sb.AppendLine($@"      <rect x=""{x - rectWidth / 2}"" y=""{indexY - rectHeight / 2 - 2}"" width=""{rectWidth}"" height=""{rectHeight}"" rx=""{rectRadius}"" fill=""black"" opacity=""0.8""/>");

        // White text
        sb.AppendLine($@"      <text x=""{x}"" y=""{indexY + 5}"" text-anchor=""middle"" font-family=""sans-serif"" font-size=""18"" font-weight=""bold"" fill=""white"">{index}</text>");

        sb.AppendLine("    </g>");
    }

    /// <summary>
    /// Gets SVG pattern ID for a resource type.
    /// </summary>
    private static string GetPatternId(ResourceType resourceType)
    {
        return resourceType switch
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
    }

    /// <summary>
    /// Gets fallback color for a resource type when pattern is not available.
    /// </summary>
    private static string GetResourceColor(ResourceType resourceType)
    {
        return resourceType switch
        {
            ResourceType.Wheat => "#f4d03f",
            ResourceType.Wood => "#27ae60",
            ResourceType.Ore => "#7f8c8d",
            ResourceType.Brick => "#c0392b",
            ResourceType.Sheep => "#a8e6cf",
            ResourceType.Desert => "#f5deb3",
            ResourceType.Sea => "#3498db",
            ResourceType.GoldMine => "#f39c12",
            _ => "#ecf0f1"
        };
    }
}

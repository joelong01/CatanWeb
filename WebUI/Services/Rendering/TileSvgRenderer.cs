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
    /// <returns>SVG markup string for the tile.</returns>
    public static string RenderSvg(this TileModel tile, bool isDimmed = false)
    {
        var sb = new StringBuilder();
        var (x, y) = AxialToPixel(tile.TileKey.Q, tile.TileKey.R);

        // Tile group with CSS class for animations
        var cssClass = isDimmed ? "tile tile-dimmed" : "tile";
        sb.AppendLine($@"  <g class=""{cssClass}"" data-q=""{tile.TileKey.Q}"" data-r=""{tile.TileKey.R}"">");

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

        // Render temporary gold resource indicator (if TemporarilyGold is true)
        if (tile.TemporarilyGold)
        {
            RenderTemporaryGoldIndicator(sb, x, y, tile.ResourceTileType);
        }

        sb.AppendLine("  </g>");
        return sb.ToString();
    }

    /// <summary>
    /// Renders the hexagonal tile background with resource pattern/color fill.
    /// Uses GoldMine pattern if TemporarilyGold is true, otherwise uses tile's ResourceTileType.
    /// </summary>
    private static void RenderHexBackground(StringBuilder sb, TileModel tile, double x, double y)
    {
        // Use gold pattern if this is a temporary gold tile, otherwise use tile's resource type
        var displayResource = tile.TemporarilyGold ? ResourceType.GoldMine : tile.ResourceTileType;
        var patternId = GetPatternId(displayResource);
        var fill = patternId != "tile-default"
            ? $"url(#{patternId})"
            : GetResourceColor(displayResource);

        var hexPath = GenerateHexPath(x, y);
        sb.AppendLine($@"    <path d=""{hexPath}"" fill=""{fill}"" stroke=""{BoardSvgConstants.HexStrokeColor}"" stroke-width=""{BoardSvgConstants.HexStrokeWidth}""/>");
    }

    /// <summary>
    /// Renders the number token with roll number and probability stars.
    /// </summary>
    private static void RenderNumberToken(StringBuilder sb, TileModel tile, double x, double y)
    {
        var isHighProb = tile.Number == 6 || tile.Number == 8;
        var numberColor = isHighProb ? BoardSvgConstants.HighProbColor : BoardSvgConstants.NormalNumberColor;
        var pips = GetPips(tile.Number);

        var numberY = y - BoardSvgConstants.NumberTokenOffsetY;

        // Number token background circle
        sb.AppendLine($@"    <circle cx=""{x}"" cy=""{numberY}"" r=""{BoardSvgConstants.NumberTokenRadius}"" fill=""{BoardSvgConstants.NumberTokenFill}"" opacity=""{BoardSvgConstants.NumberTokenOpacity}"" stroke=""{BoardSvgConstants.NumberTokenStroke}"" stroke-width=""{BoardSvgConstants.NumberTokenStrokeWidth}""/>");

        // Roll number
        sb.AppendLine($@"    <text x=""{x}"" y=""{numberY + BoardSvgConstants.NumberOffsetY}"" text-anchor=""middle"" dominant-baseline=""middle"" font-family=""sans-serif"" font-size=""{BoardSvgConstants.NumberFontSize}"" font-weight=""bold"" fill=""{numberColor}"">{tile.Number}</text>");

        // Probability pips (stars)
        if (!string.IsNullOrEmpty(pips))
        {
            sb.AppendLine($@"    <text x=""{x}"" y=""{numberY + BoardSvgConstants.PipsOffsetY}"" text-anchor=""middle"" font-size=""{BoardSvgConstants.PipsFontSize}"" fill=""{numberColor}"">{pips}</text>");
        }
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
    /// Renders the temporary gold tile indicator showing the original resource type.
    /// Displays as a small flipped card showing the original resource.
    /// </summary>
    private static void RenderTemporaryGoldIndicator(StringBuilder sb, double x, double y, ResourceType originalResource)
    {
        // Small card at bottom of tile showing original resource
        const double cardWidth = 40;
        const double cardHeight = 60;
        var cardX = x - cardWidth / 2;
        var cardY = y + 30;  // Positioned below number token

        // Card background
        sb.AppendLine($@"    <rect x=""{cardX}"" y=""{cardY}"" width=""{cardWidth}"" height=""{cardHeight}"" fill=""white"" stroke=""black"" stroke-width=""2"" rx=""4"" class=""gold-indicator""/>");

        // Original resource pattern inside card
        var patternId = GetPatternId(originalResource);
        var cardPatternX = cardX + 5;
        var cardPatternY = cardY + 5;
        var cardPatternSize = cardWidth - 10;
        sb.AppendLine($@"    <rect x=""{cardPatternX}"" y=""{cardPatternY}"" width=""{cardPatternSize}"" height=""{cardPatternSize}"" fill=""url(#{patternId})"" rx=""2""/>");
    }

    /// <summary>
    /// Gets probability pips (stars) for a given roll number.
    /// Uses Unicode star character to match Desktop app.
    /// </summary>
    private static string GetPips(int number)
    {
        return number switch
        {
            2 or 12 => "★",
            3 or 11 => "★★",
            4 or 10 => "★★★",
            5 or 9 => "★★★★",
            6 or 8 => "★★★★★",
            _ => ""
        };
    }

    /// <summary>
    /// Converts axial coordinates to pixel position.
    /// </summary>
    private static (double x, double y) AxialToPixel(int q, int r)
    {
        double x = HexSize * (3.0 / 2 * q);
        double y = HexSize * (Math.Sqrt(3) / 2 * q + Math.Sqrt(3) * r);
        return (x + CenterX, y + CenterY);
    }

    /// <summary>
    /// Gets hex vertices for a tile at the given center position.
    /// </summary>
    private static List<(double x, double y)> GetHexVertices(double cx, double cy)
    {
        var vertices = new List<(double x, double y)>();
        for (int i = 0; i < 6; i++)
        {
            double angle = Math.PI / 180 * (60 * i);
            double x = cx + HexSize * Math.Cos(angle);
            double y = cy + HexSize * Math.Sin(angle);
            vertices.Add((x, y));
        }
        return vertices;
    }

    /// <summary>
    /// Generates SVG path for hexagon at given center position.
    /// </summary>
    private static string GenerateHexPath(double cx, double cy)
    {
        var vertices = GetHexVertices(cx, cy);
        var points = vertices.Select(v => $"{v.x:F1},{v.y:F1}").ToList();
        return $"M {points[0]} L {string.Join(" L ", points.Skip(1))} Z";
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

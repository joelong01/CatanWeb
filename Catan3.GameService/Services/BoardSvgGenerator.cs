using System.Text;
using Catan3.Shared.Models;

namespace Catan3.GameService.Services;

/// <summary>
/// Constants for SVG board rendering - using Desktop app's exact values
/// SVG scales via viewBox, so we use the same sizes for easy comparison
/// </summary>
public static class BoardSvgConstants
{
    // Base hex geometry - SAME AS DESKTOP APP
    public const double HexSize = 100;  // OuterHexSize from BoardLayoutProps.cs
    public const double HexWidth = HexSize * 2;
    public static readonly double HexHeight = Math.Sqrt(3) * HexSize;

    // Board positioning
    public const double CenterX = 800;
    public const double CenterY = 700;
    public const double Padding = 40;

    // Inner/Outer hex geometry - SAME AS DESKTOP APP
    // From BoardVisualLayout.cs: InnerHexSize = OuterHexSize - TileGap - InnerHexStrokeThickness * 0.5
    public const double TileGap = 2;
    public const double InnerHexStrokeThickness = 16;
    public const double InnerHexSize = HexSize - TileGap - InnerHexStrokeThickness * 0.5;  // = 90

    // Settlement/City - SAME AS DESKTOP APP
    public const double BuildingSize = 40;
    public const double SettlementRadius = BuildingSize / 2;  // = 20

    // Number token
    public const double NumberTokenRadius = 30;
    public const double NumberTokenOffsetY = 50;
    public const double NumberTokenOpacity = 0.85;

    // Font sizes
    public const double NumberFontSize = 24;
    public const double PipsFontSize = 24;
    public const double PipsOffsetY = 20;

    // Stroke widths
    public const double HexStrokeWidth = 6;
    public const double NumberTokenStrokeWidth = 0.5;

    // Colors
    public const string HexStrokeColor = "#8B4513";  // Reddish-brown like bmCherry
    public const string NumberTokenFill = "#2F6999";
    public const string NumberTokenStroke = "white";
    public const string HighProbColor = "#c00";
    public const string NormalNumberColor = "#fff";
    public const string BackgroundColor = "#1e90ff";
}

/// <summary>
/// Generates SVG representation of the Catan game board
/// </summary>
public class BoardSvgGenerator
{
    // Use constants from BoardSvgConstants
    private const double HexSize = BoardSvgConstants.HexSize;
    private const double HexWidth = BoardSvgConstants.HexWidth;
    private static readonly double HexHeight = BoardSvgConstants.HexHeight;
    private const double CenterX = BoardSvgConstants.CenterX;
    private const double CenterY = BoardSvgConstants.CenterY;

    /// <summary>
    /// Generate SVG for the game board
    /// </summary>
    public string GenerateBoardSvg(GameModel gameModel)
    {
        var sb = new StringBuilder();

        // Calculate actual bounds of all tiles
        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;

        foreach (var tile in gameModel.Tiles)
        {
            var (cx, cy) = AxialToPixel(tile.TileKey.Q, tile.TileKey.R);
            var tileVertices = GetHexVertices(cx, cy);
            foreach (var v in tileVertices)
            {
                minX = Math.Min(minX, v.x);
                maxX = Math.Max(maxX, v.x);
                minY = Math.Min(minY, v.y);
                maxY = Math.Max(maxY, v.y);
            }
        }

        // Add padding
        minX -= BoardSvgConstants.Padding;
        minY -= BoardSvgConstants.Padding;
        maxX += BoardSvgConstants.Padding;
        maxY += BoardSvgConstants.Padding;

        double width = maxX - minX;
        double height = maxY - minY;

        // SVG header - responsive, fills container with calculated viewBox
        sb.AppendLine($@"<svg xmlns=""http://www.w3.org/2000/svg"" xmlns:xlink=""http://www.w3.org/1999/xlink"" viewBox=""{minX:F0} {minY:F0} {width:F0} {height:F0}"" preserveAspectRatio=""xMidYMid meet"" style=""width: 100%; height: 100%;"">");

        // Defs section with patterns and styles
        sb.AppendLine("  <defs>");

        // Add tile patterns
        GenerateTilePatterns(sb);

        sb.AppendLine("  </defs>");

        // CSS for hover effects and selected state
        sb.AppendLine($@"  <style>
    .road {{ fill: transparent; stroke: transparent; cursor: pointer; }}
    .road:hover {{ fill: rgba(255, 255, 255, 0.8); stroke: #333; stroke-width: 1; }}
    .road.selected {{ fill: rgba(255, 255, 0, 0.9); stroke: #333; stroke-width: 1; }}
    .settlement {{ fill: transparent; stroke: transparent; cursor: pointer; }}
    .settlement:hover {{ fill: rgba(255, 255, 255, 0.9); stroke: #333; stroke-width: 2; }}
    .settlement.selected {{ fill: rgba(255, 255, 0, 0.9); stroke: #333; stroke-width: 2; }}
  </style>");

        // JavaScript for click-to-select behavior
        sb.AppendLine(@"  <script type=""text/ecmascript""><![CDATA[
    document.addEventListener('DOMContentLoaded', function() {
      // Handle road clicks
      document.querySelectorAll('.road').forEach(function(road) {
        road.addEventListener('click', function() {
          this.classList.toggle('selected');
        });
      });
      // Handle settlement clicks
      document.querySelectorAll('.settlement').forEach(function(settlement) {
        settlement.addEventListener('click', function() {
          this.classList.toggle('selected');
        });
      });
    });
  ]]></script>");

        // Background
        sb.AppendLine($@"  <rect width=""100%"" height=""100%"" fill=""{BoardSvgConstants.BackgroundColor}""/>");

        // Render each tile
        foreach (var tile in gameModel.Tiles)
        {
            RenderTile(sb, tile);
        }

        // Collect roads and settlements separately for proper z-order
        var edges = new HashSet<string>();
        var vertices = new HashSet<string>();
        var roadData = new List<((double x, double y) v1, (double x, double y) v2)>();
        var settlementData = new List<(double x, double y)>();

        foreach (var tile in gameModel.Tiles)
        {
            // Skip sea tiles for roads/settlements
            if (tile.ResourceTileType == ResourceType.Sea)
                continue;

            var (cx, cy) = AxialToPixel(tile.TileKey.Q, tile.TileKey.R);
            var hexVertices = GetHexVertices(cx, cy);

            // Collect edges (roads)
            for (int i = 0; i < 6; i++)
            {
                var v1 = hexVertices[i];
                var v2 = hexVertices[(i + 1) % 6];
                var edgeKey = GetEdgeKey(v1, v2);
                if (!edges.Contains(edgeKey))
                {
                    edges.Add(edgeKey);
                    roadData.Add((v1, v2));
                }
            }

            // Collect vertices (settlements)
            foreach (var v in hexVertices)
            {
                var vertexKey = $"{v.x:F0},{v.y:F0}";
                if (!vertices.Contains(vertexKey))
                {
                    vertices.Add(vertexKey);
                    settlementData.Add(v);
                }
            }
        }

        // Render roads FIRST (so they're below settlements)
        foreach (var (v1, v2) in roadData)
        {
            var roadPolygon = GenerateRoadPolygon(v1, v2);
            sb.AppendLine($@"  <polygon points=""{roadPolygon}"" class=""road""/>");
        }

        // Render settlements AFTER roads (so they're on top)
        foreach (var v in settlementData)
        {
            sb.AppendLine($@"  <circle cx=""{v.x:F1}"" cy=""{v.y:F1}"" r=""{BoardSvgConstants.SettlementRadius}"" class=""settlement""/>");
        }

        // Close SVG
        sb.AppendLine("</svg>");

        return sb.ToString();
    }

    private void GenerateTilePatterns(StringBuilder sb)
    {
        // Pattern size matches hex bounding box
        var patternWidth = HexSize * 2;
        var patternHeight = HexHeight;

        var tileTypes = new[]
        {
            (ResourceType.Brick, "brick.png"),
            (ResourceType.Wheat, "wheat.png"),
            (ResourceType.Wood, "wood.png"),
            (ResourceType.Ore, "ore.png"),
            (ResourceType.Sheep, "sheep.png"),
            (ResourceType.Desert, "desert.png"),
            (ResourceType.GoldMine, "goldMine.png"),
            (ResourceType.Sea, "back.jpg"),
        };

        foreach (var (resourceType, filename) in tileTypes)
        {
            var patternId = GetPatternId(resourceType);
            sb.AppendLine($@"    <pattern id=""{patternId}"" patternUnits=""objectBoundingBox"" width=""1"" height=""1"">");
            sb.AppendLine($@"      <image href=""/images/tiles/{filename}"" width=""{patternWidth:F0}"" height=""{patternHeight:F0}"" preserveAspectRatio=""xMidYMid slice""/>");
            sb.AppendLine("    </pattern>");
        }
    }

    private string GetPatternId(ResourceType resourceType)
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

    private void RenderTile(StringBuilder sb, TileModel tile)
    {
        // Convert axial coordinates to pixel position
        var (x, y) = AxialToPixel(tile.TileKey.Q, tile.TileKey.R);

        // Get fill - use pattern for known types, fallback to color
        var patternId = GetPatternId(tile.ResourceTileType);
        var fill = patternId != "tile-default"
            ? $"url(#{patternId})"
            : GetResourceColor(tile.ResourceTileType);

        // Generate hex path
        var hexPath = GenerateHexPath(x, y);

        // Render hex with pattern fill
        sb.AppendLine($@"  <path d=""{hexPath}"" fill=""{fill}"" stroke=""{BoardSvgConstants.HexStrokeColor}"" stroke-width=""{BoardSvgConstants.HexStrokeWidth}""/>");

        // Add roll number with background circle (if not desert or sea)
        // Position at top of tile like Desktop app (VerticalAlignment="Top")
        if (tile.ResourceTileType != ResourceType.Desert &&
            tile.ResourceTileType != ResourceType.Sea &&
            tile.Number > 0)
        {
            var isHighProb = tile.Number == 6 || tile.Number == 8;
            var numberColor = isHighProb ? BoardSvgConstants.HighProbColor : BoardSvgConstants.NormalNumberColor;
            var pips = GetPips(tile.Number);

            // Number token positioned at top of hex
            var numberY = y - BoardSvgConstants.NumberTokenOffsetY;

            // Number token background
            sb.AppendLine($@"  <circle cx=""{x}"" cy=""{numberY}"" r=""{BoardSvgConstants.NumberTokenRadius}"" fill=""{BoardSvgConstants.NumberTokenFill}"" opacity=""{BoardSvgConstants.NumberTokenOpacity}"" stroke=""{BoardSvgConstants.NumberTokenStroke}"" stroke-width=""{BoardSvgConstants.NumberTokenStrokeWidth}""/>");

            // Number
            sb.AppendLine($@"  <text x=""{x}"" y=""{numberY - 1}"" text-anchor=""middle"" dominant-baseline=""middle"" font-family=""sans-serif"" font-size=""{BoardSvgConstants.NumberFontSize}"" font-weight=""bold"" fill=""{numberColor}"">{tile.Number}</text>");

            // Probability pips
            if (!string.IsNullOrEmpty(pips))
            {
                sb.AppendLine($@"  <text x=""{x}"" y=""{numberY + BoardSvgConstants.PipsOffsetY}"" text-anchor=""middle"" font-size=""{BoardSvgConstants.PipsFontSize}"" fill=""{numberColor}"">{pips}</text>");
            }
        }
    }

    private string GetPips(int number)
    {
        return number switch
        {
            2 or 12 => "•",
            3 or 11 => "••",
            4 or 10 => "•••",
            5 or 9 => "••••",
            6 or 8 => "•••••",
            _ => ""
        };
    }

    private (double x, double y) AxialToPixel(int q, int r)
    {
        // Convert axial coordinates to pixel position
        // Using flat-top hex orientation
        double x = HexSize * (3.0 / 2 * q);
        double y = HexSize * (Math.Sqrt(3) / 2 * q + Math.Sqrt(3) * r);

        return (x + CenterX, y + CenterY);
    }

    private List<(double x, double y)> GetHexVertices(double cx, double cy)
    {
        var vertices = new List<(double x, double y)>();

        for (int i = 0; i < 6; i++)
        {
            // Flat-top hex: start at right side
            double angle = Math.PI / 180 * (60 * i);
            double x = cx + HexSize * Math.Cos(angle);
            double y = cy + HexSize * Math.Sin(angle);
            vertices.Add((x, y));
        }

        return vertices;
    }

    private string GenerateHexPath(double cx, double cy)
    {
        var vertices = GetHexVertices(cx, cy);
        var points = vertices.Select(v => $"{v.x:F1},{v.y:F1}").ToList();
        return $"M {points[0]} L {string.Join(" L ", points.Skip(1))} Z";
    }

    private string GenerateRoadPolygon((double x, double y) v1, (double x, double y) v2)
    {
        // 6-point polygon using inner/outer hex geometry
        // Tips at outer vertices, body at inner hex boundaries
        // Inner vertices are scaled toward tile center, not perpendicular to edge

        // Edge midpoint and perpendicular direction
        var midX = (v1.x + v2.x) / 2;
        var midY = (v1.y + v2.y) / 2;
        var dx = v2.x - v1.x;
        var dy = v2.y - v1.y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        var perpX = -dy / length;  // Perpendicular (90° CCW)
        var perpY = dx / length;

        // Calculate tile centers on both sides of the edge
        // Apothem = HexSize * sqrt(3) / 2 (distance from center to edge midpoint)
        double apothem = HexSize * Math.Sqrt(3) / 2;
        var centerAx = midX + perpX * apothem;
        var centerAy = midY + perpY * apothem;
        var centerBx = midX - perpX * apothem;
        var centerBy = midY - perpY * apothem;

        // Inner/outer ratio for scaling vertices toward center
        double ratio = BoardSvgConstants.InnerHexSize / HexSize;  // = 0.9

        // Calculate inner vertices by scaling outer vertices toward their respective tile centers
        // I = Center + (V - Center) * ratio = V * ratio + Center * (1 - ratio)
        double oneMinusRatio = 1 - ratio;  // = 0.1

        // Inner vertices for tile A (positive perpendicular side)
        var i1Ax = v1.x * ratio + centerAx * oneMinusRatio;
        var i1Ay = v1.y * ratio + centerAy * oneMinusRatio;
        var i2Ax = v2.x * ratio + centerAx * oneMinusRatio;
        var i2Ay = v2.y * ratio + centerAy * oneMinusRatio;

        // Inner vertices for tile B (negative perpendicular side)
        var i1Bx = v1.x * ratio + centerBx * oneMinusRatio;
        var i1By = v1.y * ratio + centerBy * oneMinusRatio;
        var i2Bx = v2.x * ratio + centerBx * oneMinusRatio;
        var i2By = v2.y * ratio + centerBy * oneMinusRatio;

        // 6 points: outer tips and inner body edges
        var points = new List<string>
        {
            // Point 1: O1 (outer vertex 1 - tip)
            $"{v1.x:F1},{v1.y:F1}",
            // Point 2: I1_A (tile A's inner vertex at v1)
            $"{i1Ax:F1},{i1Ay:F1}",
            // Point 3: I2_A (tile A's inner vertex at v2)
            $"{i2Ax:F1},{i2Ay:F1}",
            // Point 4: O2 (outer vertex 2 - tip)
            $"{v2.x:F1},{v2.y:F1}",
            // Point 5: I2_B (tile B's inner vertex at v2)
            $"{i2Bx:F1},{i2By:F1}",
            // Point 6: I1_B (tile B's inner vertex at v1)
            $"{i1Bx:F1},{i1By:F1}"
        };

        return string.Join(" ", points);
    }

    private string GetEdgeKey((double x, double y) v1, (double x, double y) v2)
    {
        // Create a consistent key for an edge regardless of direction
        var key1 = $"{v1.x:F0},{v1.y:F0}";
        var key2 = $"{v2.x:F0},{v2.y:F0}";
        return string.Compare(key1, key2) < 0 ? $"{key1}-{key2}" : $"{key2}-{key1}";
    }

    private string GetResourceColor(ResourceType resourceType)
    {
        return resourceType switch
        {
            ResourceType.Wheat => "#f4d03f",      // Yellow
            ResourceType.Wood => "#27ae60",       // Green
            ResourceType.Ore => "#7f8c8d",        // Gray
            ResourceType.Brick => "#c0392b",      // Red/Brown
            ResourceType.Sheep => "#a8e6cf",      // Light Green
            ResourceType.Desert => "#f5deb3",     // Tan
            ResourceType.Sea => "#3498db",        // Blue
            ResourceType.GoldMine => "#f39c12",   // Gold
            _ => "#ecf0f1"                        // Light gray
        };
    }
}

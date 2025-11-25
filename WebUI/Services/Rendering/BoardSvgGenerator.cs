using System.Text;
using Catan3.Shared.Models;
using Catan3.Shared.ViewData;
using Catan3.Shared.Utility;

namespace Catan3.WebUI.Services.Rendering;

/// <summary>
/// Extension methods for GameModel to generate SVG board markup.
/// Provides client-side SVG rendering using compositional renderers.
/// </summary>
public static class BoardSvgGenerator
{
    private const double HexSize = BoardSvgConstants.HexSize;
    private static readonly double HexHeight = BoardSvgConstants.HexHeight;

    /// <summary>
    /// Generates complete SVG markup for the game board.
    /// </summary>
    /// <param name="gameModel">The game model containing all board state.</param>
    /// <param name="playerData">Dictionary of player profile data keyed by player ID.</param>
    /// <param name="shownStars">Star threshold for building visibility (0-14).</param>
    /// <param name="dimmedTiles">Set of tile keys that should be dimmed.</param>
    /// <returns>Complete SVG markup string.</returns>
    public static string GenerateSvg(
        this GameModel gameModel,
        IReadOnlyDictionary<string, PlayerData> playerData,
        int shownStars = 0,
        HashSet<HexCoordinates>? dimmedTiles = null)
    {
        dimmedTiles ??= new HashSet<HexCoordinates>();
        var sb = new StringBuilder();

        // Calculate viewBox bounds
        var (minX, minY, maxX, maxY) = CalculateBounds(gameModel);

        // Add padding
        minX -= BoardSvgConstants.Padding;
        minY -= BoardSvgConstants.Padding;
        maxX += BoardSvgConstants.Padding;
        maxY += BoardSvgConstants.Padding;

        double width = maxX - minX;
        double height = maxY - minY;

        // SVG header - responsive, fills container
        sb.AppendLine($@"<svg xmlns=""http://www.w3.org/2000/svg"" xmlns:xlink=""http://www.w3.org/1999/xlink"" viewBox=""{minX:F0} {minY:F0} {width:F0} {height:F0}"" preserveAspectRatio=""xMidYMid meet"" style=""width: 100%; height: 100%;"">");

        // Defs section with patterns and gradients
        sb.AppendLine("  <defs>");
        GenerateTilePatterns(sb);
        GenerateHarborPatterns(sb);
        GeneratePlayerGradients(sb, playerData);
        sb.AppendLine("  </defs>");

        // CSS styles for animations and states
        GenerateStyles(sb);

        // Background
        sb.AppendLine($@"  <rect width=""100%"" height=""100%"" fill=""{BoardSvgConstants.BackgroundColor}""/>");

        // Render tiles
        foreach (var tile in gameModel.Tiles)
        {
            var isDimmed = dimmedTiles.Contains(tile.TileKey);
            sb.Append(tile.RenderSvg(isDimmed));
        }

        // Render harbors (below roads/buildings)
        foreach (var harbor in gameModel.Harbors)
        {
            sb.Append(harbor.RenderSvg());
        }

        // Render roads (below buildings for proper z-order)
        foreach (var road in gameModel.Roads)
        {
            var player = playerData.TryGetValue(road.OwnerId ?? "", out var pd) ? pd : null;
            sb.Append(road.RenderSvg(player, road.BuildIndex));
        }

        // Render buildings (on top)
        // TODO: Future optimization - filter buildings to only render those that are built or buildable in current state
        foreach (var building in gameModel.Buildings)
        {
            var player = playerData.TryGetValue(building.OwnerId ?? "", out var pd) ? pd : null;
            var visualState = GetBuildingVisualState(building, shownStars);
            sb.Append(building.RenderSvg(player, visualState));
        }

        // Close SVG
        sb.AppendLine("</svg>");

        return sb.ToString();
    }

    /// <summary>
    /// Determines building visual state based on building state and star threshold.
    /// Simplified version - full highlighting logic requires game state/entitlements.
    /// </summary>
    private static BuildingVisualState GetBuildingVisualState(BuildingModel building, int shownStars)
    {
        // Show buildings that are actually built (Settlement or City)
        if (building.BuildingState == BuildingState.Settlement ||
            building.BuildingState == BuildingState.City)
        {
            return BuildingVisualState.Normal;
        }

        // Show possible settlement locations during allocation phases
        if (building.BuildingState == BuildingState.PossibleSettlement)
        {
            return BuildingVisualState.Normal;
        }

        // Hide unbuilt buildings
        return BuildingVisualState.Hidden;
    }

    /// <summary>
    /// Calculates the bounding box for all board elements.
    /// </summary>
    private static (double minX, double minY, double maxX, double maxY) CalculateBounds(GameModel gameModel)
    {
        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;

        // Include tiles
        foreach (var tile in gameModel.Tiles)
        {
            var (cx, cy) = AxialToPixel(tile.TileKey.Q, tile.TileKey.R);
            var vertices = GetHexVertices(cx, cy);
            foreach (var v in vertices)
            {
                minX = Math.Min(minX, v.x);
                maxX = Math.Max(maxX, v.x);
                minY = Math.Min(minY, v.y);
                maxY = Math.Max(maxY, v.y);
            }
        }

        // Include harbors
        foreach (var harbor in gameModel.Harbors)
        {
            if (harbor.HarborKey.HarborType == HarborType.None)
                continue;

            var (cx, cy) = AxialToPixel(harbor.HarborKey.HexCoordinates.Q, harbor.HarborKey.HexCoordinates.R);
            var hexVertices = GetHexVertices(cx, cy);
            var (v1Idx, v2Idx) = GetEdgeVerticesForSide(harbor.HarborKey.Side);
            var v1 = hexVertices[v1Idx];
            var v2 = hexVertices[v2Idx];

            var midX = (v1.x + v2.x) / 2;
            var midY = (v1.y + v2.y) / 2;
            var dx = midX - cx;
            var dy = midY - cy;
            var length = Math.Sqrt(dx * dx + dy * dy);
            var normX = dx / length;
            var normY = dy / length;

            var harborX = midX + normX * BoardSvgConstants.HarborOffset;
            var harborY = midY + normY * BoardSvgConstants.HarborOffset;

            minX = Math.Min(minX, harborX - BoardSvgConstants.HarborCircleRadius);
            maxX = Math.Max(maxX, harborX + BoardSvgConstants.HarborCircleRadius);
            minY = Math.Min(minY, harborY - BoardSvgConstants.HarborCircleRadius);
            maxY = Math.Max(maxY, harborY + BoardSvgConstants.HarborCircleRadius);
        }

        return (minX, minY, maxX, maxY);
    }

    /// <summary>
    /// Generates SVG patterns for tile resource images.
    /// </summary>
    private static void GenerateTilePatterns(StringBuilder sb)
    {
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

    /// <summary>
    /// Generates SVG patterns for harbor images.
    /// </summary>
    private static void GenerateHarborPatterns(StringBuilder sb)
    {
        var harborTypes = new[]
        {
            (HarborType.Brick, "2 for 1 brick.png"),
            (HarborType.Ore, "2 for 1 ore.png"),
            (HarborType.Sheep, "2 for 1 sheep.png"),
            (HarborType.Wheat, "2 for 1 wheat.png"),
            (HarborType.Wood, "2 for 1 wood.png"),
            (HarborType.ThreeForOne, "3 for 1.png"),
        };

        var harborSize = BoardSvgConstants.HarborCircleRadius * 2;
        foreach (var (harborType, filename) in harborTypes)
        {
            var patternId = GetHarborPatternId(harborType);
            sb.AppendLine($@"    <pattern id=""{patternId}"" patternUnits=""objectBoundingBox"" width=""1"" height=""1"">");
            sb.AppendLine($@"      <image href=""/images/harbors/{filename}"" width=""{harborSize:F0}"" height=""{harborSize:F0}"" preserveAspectRatio=""xMidYMid slice""/>");
            sb.AppendLine("    </pattern>");
        }
    }

    /// <summary>
    /// Generates linear gradients for player backgrounds.
    /// </summary>
    private static void GeneratePlayerGradients(StringBuilder sb, IReadOnlyDictionary<string, PlayerData> playerData)
    {
        foreach (var (playerId, player) in playerData)
        {
            var gradientId = $"gradient-{playerId}";
            sb.AppendLine($@"    <linearGradient id=""{gradientId}"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""100%"">");
            sb.AppendLine($@"      <stop offset=""0%"" style=""stop-color:{player.PrimaryBackgroundColor};stop-opacity:1"" />");
            sb.AppendLine($@"      <stop offset=""100%"" style=""stop-color:{player.SecondaryBackgroundColor};stop-opacity:1"" />");
            sb.AppendLine("    </linearGradient>");
        }
    }

    /// <summary>
    /// Generates CSS styles for animations and interactive states.
    /// </summary>
    private static void GenerateStyles(StringBuilder sb)
    {
        sb.AppendLine(@"  <style>
    .tile { transition: opacity 0.3s ease; }
    .tile-dimmed { opacity: 0.5; }
    .building-highlighted { filter: brightness(1.5); }
    .gold-indicator { animation: flip-card 0.5s ease; }
    .road { transition: opacity 0.2s ease; }
    .road:hover { opacity: 1.0 !important; }
    @keyframes flip-card {
      0% { transform: rotateY(0deg); }
      100% { transform: rotateY(180deg); }
    }
  </style>");
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

    /// <summary>
    /// Converts axial coordinates to pixel position.
    /// </summary>
    private static (double x, double y) AxialToPixel(int q, int r)
    {
        double x = HexSize * (3.0 / 2 * q);
        double y = HexSize * (Math.Sqrt(3) / 2 * q + Math.Sqrt(3) * r);
        return (x + BoardSvgConstants.CenterX, y + BoardSvgConstants.CenterY);
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
    /// Maps HexSide to the two vertex indices for flat-top hex orientation.
    /// </summary>
    private static (int, int) GetEdgeVerticesForSide(HexSide side)
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
}

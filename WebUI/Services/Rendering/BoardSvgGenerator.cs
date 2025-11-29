using System.Text;
using Catan3.Shared.Extensions;
using Catan3.Shared.Models;
using Catan3.Shared.Profiles;
using Catan3.Shared.Utility;
using Catan3.WebUI.Models;

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
    /// <param name="players">List of player view models (already in game order).</param>
    /// <param name="shownStars">Star threshold for building visibility (0-14).</param>
    /// <param name="dimmedTiles">Set of tile keys that should be dimmed.</param>
    /// <param name="filteredResources">Resources selected for filtering buildings (max 3, AND logic).</param>
    /// <returns>Complete SVG markup string.</returns>
    public static string GenerateSvg(
        this GameModel gameModel,
        IReadOnlyList<PlayerViewModel> players,
        int shownStars = 0,
        HashSet<HexCoordinates>? dimmedTiles = null,
        HashSet<ResourceType>? filteredResources = null)
    {
        dimmedTiles ??= new HashSet<HexCoordinates>();
        filteredResources ??= new HashSet<ResourceType>();

        // Convert player list to dictionary for efficient lookup
        var playerLookup = players.ToDictionary(p => p.Id);

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

        // Get current player for gradient generation and building rendering
        var currentPlayer = gameModel.CurrentPlayer();
        var currentPlayerViewModel = playerLookup.TryGetValue(currentPlayer.Id, out var cpvm) ? cpvm : null;

        // Defs section with patterns and gradients
        sb.AppendLine("  <defs>");
        GenerateCherryPattern(sb);  // Cherry wood border texture
        GenerateTilePatterns(sb);
        GenerateHarborPatterns(sb);
        GeneratePlayerGradients(sb, playerLookup, currentPlayerViewModel);
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
            // Owned roads use owner colors, non-owned roads use current player colors (matches Desktop RoadViewModel)
            PlayerViewModel? player;
            if (road.OwnerId != null && playerLookup.TryGetValue(road.OwnerId, out var ownerViewModel))
            {
                player = ownerViewModel;  // Owned road - use owner's colors
            }
            else
            {
                player = currentPlayerViewModel;  // Non-owned road (buildable, etc) - use current player's colors
            }

            // Calculate opacity based on road state (matches Desktop RoadViewModel.Opacity)
            var opacity = road.RoadState switch
            {
                RoadState.Road => 1.0,      // Owned road - fully visible
                RoadState.Ship => 1.0,      // Owned ship - fully visible
                RoadState.Buildable => 0.5, // Buildable location - semi-transparent
                _ => 0.0                     // Unowned/hidden - transparent (shows on hover via CSS)
            };

            sb.Append(road.RenderSvg(player, road.BuildIndex, opacity));
        }

        // Render buildings (on top)
        int buildingIndex = 1;  // Build index counter for highlighted buildings (matches Desktop logic)
        foreach (var building in gameModel.Buildings)
        {
            // Calculate stars using extension methods (sum of pips from adjacent tiles)
            var stars = gameModel.TilesForBuildings(building.BuildingKey).Stars();

            // Determine visual state based on entitlements, phase, star threshold, and resource filter
            var visualState = GetBuildingVisualState(building, gameModel, stars, shownStars, filteredResources);

            // Get current player colors and owner colors (null if unowned)
            var currentPlayerColors = currentPlayerViewModel?.Colors;
            var ownerColors = playerLookup.TryGetValue(building.OwnerId ?? "", out var owner) ? owner.Colors : null;

            // Assign build index only for highlighted buildings (matches Desktop GameViewModel.MergeBuildings:416, 434)
            var buildIndex = visualState == BuildingVisualState.Highlighted ? buildingIndex++ : 0;

            sb.Append(building.RenderSvg(currentPlayerColors, ownerColors, visualState, stars, buildIndex));
        }

        // Close SVG
        sb.AppendLine("</svg>");

        return sb.ToString();
    }

    /// <summary>
    /// Determines building visual state based on building state, entitlements, star threshold, and resource filter.
    /// Matches Desktop GameViewModel.MergeBuildings logic (DesktopApp/Game/GameView/GameViewModel.cs:410-444)
    /// and ExecuteQuery logic (DesktopApp/Game/GameView/GameViewModel.cs:634-665).
    /// </summary>
    /// <param name="building">The building model.</param>
    /// <param name="gameModel">The game model (for current player and phase).</param>
    /// <param name="stars">Stars value for this building (pip sum of adjacent tiles).</param>
    /// <param name="shownStars">Star threshold from board measurement slider.</param>
    /// <param name="filteredResources">Resources selected for filtering (AND logic, max 3).</param>
    /// <returns>Visual state for rendering.</returns>
    private static BuildingVisualState GetBuildingVisualState(
        BuildingModel building,
        GameModel gameModel,
        int stars,
        int shownStars,
        HashSet<ResourceType> filteredResources)
    {
        var currentPlayer = gameModel.CurrentPlayer();
        var hasCityEntitlement = currentPlayer.UnspentEntitlements.Contains(Entitlement.City);
        var hasSettlementEntitlement = currentPlayer.UnspentEntitlements.Contains(Entitlement.Settlement);
        var isPickingBoard = gameModel.GameState == GameState.PickingBoard;

        // Apply resource filter (AND logic) - only for unowned buildings
        // Desktop: GameViewModel.cs:634-665 (ExecuteQuery method)
        if (filteredResources.Count > 0 && building.OwnerId == null)
        {
            var adjacentTiles = gameModel.TilesForBuildings(building.BuildingKey);
            var tileResources = adjacentTiles
                .Select(t => t.ResourceTileType)
                .Where(rt => rt != ResourceType.Desert && rt != ResourceType.Sea)
                .ToHashSet();

            // Building must have ALL filtered resources (AND logic)
            bool hasAllResources = filteredResources.All(resource => tileResources.Contains(resource));

            if (!hasAllResources)
            {
                return BuildingVisualState.Hidden;  // Filter out buildings without all resources
            }
        }

        return building.BuildingState switch
        {
            BuildingState.PossibleSettlement => hasSettlementEntitlement && gameModel.Phase() != GamePhase.PickingResources
                ? BuildingVisualState.Highlighted
                : stars >= shownStars && (hasSettlementEntitlement || isPickingBoard)
                    ? BuildingVisualState.Stars
                    : BuildingVisualState.Hidden,

            // During PickingBoard, even NotBuildable buildings can show stars (Desktop: GameViewProps.cs:201)
            BuildingState.NotBuildable => isPickingBoard && stars >= shownStars
                ? BuildingVisualState.Stars
                : BuildingVisualState.Hidden,

            BuildingState.Settlement => hasCityEntitlement && building.OwnerId == currentPlayer.Id
                ? BuildingVisualState.Highlighted
                : BuildingVisualState.Normal,

            BuildingState.City => BuildingVisualState.Normal,
            BuildingState.Metropolis => BuildingVisualState.Normal,
            BuildingState.Knight => BuildingVisualState.Normal,

            _ => BuildingVisualState.Hidden
        };
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
            var (cx, cy) = BoardGeometry.AxialToPixel(tile.TileKey.Q, tile.TileKey.R);
            var vertices = BoardGeometry.GetHexVertices(cx, cy);
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

            var (cx, cy) = BoardGeometry.AxialToPixel(harbor.HarborKey.HexCoordinates.Q, harbor.HarborKey.HexCoordinates.R);
            var hexVertices = BoardGeometry.GetHexVertices(cx, cy);
            var (v1Idx, v2Idx) = BoardGeometry.GetEdgeVerticesForSide(harbor.HarborKey.Side);
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
    /// Generates SVG patterns for wood border textures.
    /// Matches Desktop's bmMaple and bmCherry ImageBrush resources.
    /// </summary>
    private static void GenerateCherryPattern(StringBuilder sb)
    {
        // Maple - used as FILL for outer hex border
        sb.AppendLine($@"    <pattern id=""{BoardSvgConstants.HexBorderFillPattern}"" patternUnits=""userSpaceOnUse"" width=""100"" height=""100"">");
        sb.AppendLine($@"      <image href=""/images/maple.jpg"" width=""100"" height=""100"" preserveAspectRatio=""xMidYMid slice""/>");
        sb.AppendLine("    </pattern>");

        // Cherry - used as STROKE for outer hex border
        sb.AppendLine($@"    <pattern id=""{BoardSvgConstants.HexBorderStrokePattern}"" patternUnits=""userSpaceOnUse"" width=""100"" height=""100"">");
        sb.AppendLine($@"      <image href=""/images/cherry.jpg"" width=""100"" height=""100"" preserveAspectRatio=""xMidYMid slice""/>");
        sb.AppendLine("    </pattern>");
    }

    /// <summary>
    /// Generates SVG patterns for tile resource images.
    /// </summary>
    private static void GenerateTilePatterns(StringBuilder sb)
    {
        // Pattern sized for INNER hex to reveal maple border
        var innerHexSize = BoardSvgConstants.InnerHexSize;
        var patternWidth = innerHexSize * 2;
        var patternHeight = Math.Sqrt(3) * innerHexSize;

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
            // Match Desktop's ImageBrush Stretch="UniformToFill"
            // Whole image, centered, scaled to fill, clipped at edges
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
    /// Includes gradient-current-player for Stars state buildings.
    /// </summary>
    private static void GeneratePlayerGradients(
        StringBuilder sb,
        IReadOnlyDictionary<string, PlayerViewModel> playerLookup,
        PlayerViewModel? currentPlayerViewModel)
    {
        // Generate current player gradient (for Stars state buildings)
        if (currentPlayerViewModel != null)
        {
            sb.AppendLine($@"    <linearGradient id=""gradient-current-player"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""100%"">");
            sb.AppendLine($@"      {currentPlayerViewModel.Colors.SvgGradientStops}");
            sb.AppendLine("    </linearGradient>");
        }

        // Generate per-player gradients (for owned buildings)
        foreach (var (playerId, player) in playerLookup)
        {
            var gradientId = $"gradient-{playerId}";
            sb.AppendLine($@"    <linearGradient id=""{gradientId}"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""100%"">");
            sb.AppendLine($@"      {player.Colors.SvgGradientStops}");
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

}

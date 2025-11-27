# Player Profile and View Model Architecture

**Status:** Draft
**Created:** 2025-11-27
**Related:** `database-design.md`, `WebUI-Design.md`, `mvvm-pattern.md`

## Overview

This document defines the architecture for player data representation across the Catan system, establishing clear separation between storage (PlayerProfile), game state (PlayerModel), and rendering (WebPlayerViewModel).

## Constraints

**CRITICAL:** Shared models in `Catan3.Shared/Models/` cannot be changed:
- ❌ `PlayerModel` - game state (resources, score, entitlements)
- ❌ `GameModel` - complete game state

These are shared between Desktop and WebUI and must remain unchanged.

**What CAN change:**
- ✅ `PlayerData` → `PlayerProfile` (move from ViewData to new PlayerProfile namespace)
- ✅ `PlayerViewModel` (platform-specific - Desktop and WebUI each have their own)
- ✅ Add nested types to PlayerProfile namespace (PlayerColors, LifetimeStats, GameStats)

## Architecture Decisions

### Decision 1: Remove "View" Terminology from Shared

**Rationale:** "View" is UI-specific terminology and doesn't belong in Shared code. Shared contains data models, game logic, and profiles - not view concerns.

**Before:**
- `Catan3.Shared/ViewData/PlayerData.cs` ❌ ("ViewData" implies UI concern)

**After:**
- `Catan3.Shared/PlayerProfile/PlayerProfile.cs` ✅ (profile storage is a top-level concern)

### Decision 2: PlayerProfile as Top-Level Namespace

**Rationale:** Player profiles are a significant domain concept - persistent player identity, colors, statistics. This warrants a top-level namespace, not nesting under "ViewData" or "Models".

**Structure:**
```
Catan3.Shared/
  Models/              (game state - PlayerModel, GameModel)
  PlayerProfile/       (persistent player profiles - NEW)
  Utility/
  Extensions/
```

**Benefits:**
- Clear separation: Models = game state, PlayerProfile = persistent identity
- Room for growth (stats, achievements, preferences)
- Easy to locate all profile-related code

### Decision 3: PlayerStats Hierarchy (Composition)

**Rationale:** LifetimeStats and GameStats have significant overlap (both track resources, trades, etc.). Using composition makes the aggregation relationship explicit and provides clear APIs for updating lifetime stats from game stats.

**Structure:**
```csharp
// Core stats for one game
public record GameStats(int ResourcesCollected, int TradesMade, ...);

// Lifetime stats across all games
public record LifetimeStats
{
    public int GamesPlayed { get; init; }
    public int Wins { get; init; }

    public GameStats Totals { get; init; }  // Aggregated sum of all games

    public int LongestRoadRecord { get; init; }  // Max ever achieved

    public LifetimeStats AddGame(GameStats game, bool won, ...) => ...
}
```

**Benefits:**
- Avoids duplicate property definitions
- Makes aggregation relationship explicit
- Clear API: `lifetime.AddGame(gameStats, won, ...)`
- Single source of truth for stat definitions

**Alternative considered:** Shared interface (IPlayerStats) - rejected due to property duplication.

### Decision 4: WebUI PlayerViewModel Location

**Rationale:** Following Blazor WASM conventions, client-side data structures belong in `Models/` namespace. "ViewModels" is less common in Blazor (more of a WPF/XAML pattern).

**Structure:**
```
WebUI/
  Models/              (client-side data structures - NEW)
    PlayerViewModel.cs
  Services/
    GameStateService.cs
  Pages/
```

**Benefits:**
- Follows Blazor community conventions
- Parallel to Desktop's `DesktopApp/Player/PlayerViewModel.cs`
- Clear separation from injectable services

## Problem Statement

### Current State Issues

1. **Naming Confusion**: `PlayerData` (in `Catan3.Shared/ViewData/PlayerData.cs`) represents stored player profile information, but the name suggests transient data rather than persistent profile storage.

2. **Tight Coupling**: Renderers receive full `PlayerData` objects when they only need colors, violating principle of least privilege.

3. **Unordered Collections**: Client uses `Dictionary<string, PlayerData>` for game players, but player order matters for turn sequence and UI display. Dictionary iteration order is not guaranteed.

4. **Mixed Concerns**: Profile data (name, colors, image) is conflated with rendering concerns, making it unclear what properties are for storage vs. display.

5. **Extensibility Friction**: Adding new player statistics (game stats, lifetime stats) requires touching multiple layers without clear guidance on where data belongs.

### Architectural Differences: Desktop vs WebUI

**Desktop (XAML/MVVM):**
- Collection of peer view models (TileViewModel, RoadViewModel, etc.)
- Each view model has limited scope - only knows about its own state
- Communication via MVVM messaging for cross-cutting concerns
- PlayerViewModel holds reference to PlayerData and exposes XAML-bindable properties

**WebUI (Controller/Compositional):**
- Top-level service (GameStateService) has global view of game state
- Single rendering point (BoardSvgGenerator.GenerateSvg) with full context
- Can pass any needed information down to renderers
- No MVVM messaging overhead - direct control flow

This architectural difference allows WebUI to use a cleaner information hierarchy without the constraints of Desktop's peer-to-peer view model pattern.

## Desktop Implementation (Reference)

### Desktop Architecture

Desktop uses a two-class pattern for player representation:

**PlayerViewModel** (`DesktopApp/Player/PlayerViewModel.cs`):
```csharp
public partial class PlayerViewModel : ObservableRecipient
{
    public string Id { get; set; }
    public string Name { get; set; }
    public PlayerColorViewModel PlayerColors { get; set; }  // Nested colors
    public string ImageUri { get; set; }
    public string CroppedImageUri { get; set; }

    // Game state reference
    public PlayerModel Player { get; set; }  // References game state

    // View-specific stats (calculated from PlayerModel)
    public ResourcesViewModel ResourcesThisTurn { get; set; }
    public ResourcesViewModel ResourcesThisGame { get; set; }
    public ObservableCollection<PlayerStatsViewModel> PlayerStats { get; set; }
    public Dictionary<StatName, PlayerStatsViewModel> StatDictionary { get; }

    // UI state
    public bool IsCurrentPlayer { get; set; }
    public bool Selected { get; set; }
    public BitmapImage CroppedBitmapImage { get; set; }  // XAML-specific
}
```

**PlayerColorViewModel** (`DesktopApp/Player/PlayerColorViewModel.cs`):
```csharp
public partial class PlayerColorViewModel : ObservableRecipient
{
    public string PlayerId { get; init; }
    public Color PrimaryBackground { get; set; }    // WinUI Color struct
    public Color SecondaryBackground { get; set; }
    public Color Foreground { get; set; }

    // XAML-specific brushes (derived from colors)
    public Brush ForegroundBrush { get; }
    public Brush BackgroundBrush { get; }
    public Brush GradientBrush { get; }
}
```

### Desktop Data Sources

Desktop **does not** load from PlayerData/PlayerProfile directly. Instead:

1. **PlayerDatabase singleton** (`DesktopApp/Services/PlayerDatabase.cs`):
   - Loads player profiles from JSON files in `Assets/DefaultPlayers/`
   - Each profile has: Id, Name, Colors (hex strings), ImageUri
   - Provides `FromId(playerId)` lookup method

2. **PlayerViewModel creation**:
   ```csharp
   var playerData = PlayerDatabase.Instance.FromId(playerId);
   var playerVm = new PlayerViewModel(
       playerData.Id,
       playerData.Name,
       playerData.ImageUri,
       playerData.CroppedImageUri,
       new PlayerColorViewModel(playerId, foreground, primary, secondary)
   );
   ```

3. **Game state binding**:
   ```csharp
   // In GameViewModel.MergePlayers()
   playerVm.Player = gameModel.Players[i];  // Bind to game state
   ```

### Key Desktop Patterns

1. **Separation**: PlayerViewModel (view) separate from PlayerModel (game state)
2. **Nested Colors**: Colors are a separate view model, not direct properties
3. **Stats Calculated**: Stats (ResourcesThisTurn, etc.) are derived from PlayerModel
4. **MVVM Messaging**: View models communicate via WeakReferenceMessenger
5. **Observable Properties**: CommunityToolkit.Mvvm for change notification

## Proposed Architecture

### WebUI to Desktop Mapping

| Desktop | WebUI | Notes |
|---------|-------|-------|
| `PlayerViewModel` | `PlayerViewModel` | Main view model (same name) |
| `PlayerColorViewModel` | `PlayerColors` | Simplified (record vs class, strings vs Color) |
| `PlayerDatabase.Instance` | `GameStateService.Players` | Singleton vs service instance |
| `BitmapImage` | `string ImageUri` | No platform-specific image types |
| `ObservableRecipient` | Plain C# class | No MVVM observability needed |
| `PlayerModel Player` | Via `GameStateService` | WebUI has global view of game state |

### Final Information Hierarchy

```
┌─────────────────────────────────────────────────────────────┐
│ Storage Layer (Catan3.Shared/PlayerProfile)                 │
│ PlayerProfile - Persistent player profile (CAN CHANGE)      │
│ - Id, Name, ImageUri                                        │
│ - Colors (nested PlayerColors)                              │
│ - LifetimeStats (nested, contains GameStats Totals)         │
│ - Stored as JSON document in database                       │
│                                                              │
│ Supporting types:                                            │
│ - PlayerColors (record: Primary, Secondary, Foreground)     │
│ - GameStats (record: per-game statistics)                   │
│ - LifetimeStats (record: aggregated + lifetime-only stats)  │
└─────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────┐
│ Game State Layer (Catan3.Shared/Models)                     │
│ PlayerModel - In-game state (CANNOT CHANGE - SHARED)        │
│ - Id (references PlayerProfile)                             │
│ - Resources, Score, Entitlements                            │
│ - Owned buildings, roads, etc.                              │
│                                                              │
│ GameModel - Complete game state (CANNOT CHANGE - SHARED)    │
│ - Players (List<PlayerModel>)                               │
│ - CurrentPlayerId                                            │
│ - Tiles, Roads, Buildings                                   │
└─────────────────────────────────────────────────────────────┘
                          ↓
         ┌────────────────────────────────────┐
         │                                    │
         ▼                                    ▼
┌────────────────────────┐      ┌────────────────────────┐
│ Desktop                │      │ WebUI                  │
│ DesktopApp/Player/     │      │ WebUI/Models/          │
│ PlayerViewModel        │      │ PlayerViewModel        │
│ - PlayerProfile data   │      │ - PlayerProfile data   │
│ - Player (PlayerModel) │      │ - Access PlayerModel   │
│ - XAML-specific props  │      │   via GameStateService │
│ - PlayerColorViewModel │      │ - Web-specific helpers │
│ - BitmapImage          │      │ - CssGradient, etc.    │
│ - MVVM observability   │      │ - No observability     │
└────────────────────────┘      └────────────────────────┘
```

### Type Definitions

**Key Principles:**
1. **PlayerProfile and PlayerViewModel share the same hierarchical structure** (document model), but expose different APIs for different concerns
2. **PlayerProfile lives in Catan3.Shared/ViewData** (not Models) - it's profile/storage data, not game state
3. **Each platform has its own PlayerViewModel** - Desktop has XAML-specific features, WebUI has web-specific features
4. **PlayerModel (game state) is untouchable** - shared between platforms, cannot be modified

#### Shared Nested Types

**Location:** `Catan3.Shared/ViewData/PlayerColors.cs`

```csharp
namespace Catan3.Shared.ViewData;

/// <summary>
/// Player color scheme used for rendering.
/// Shared between PlayerProfile (storage) and PlayerViewModel (rendering).
/// </summary>
public record PlayerColors(
    string Primary,      // Primary background color (hex: #RRGGBB)
    string Secondary,    // Secondary background/gradient color (hex: #RRGGBB)
    string Foreground    // Foreground/text color (hex: #RRGGBB)
);
```

**Location:** `Catan3.Shared/ViewData/LifetimeStats.cs`

```csharp
namespace Catan3.Shared.ViewData;

/// <summary>
/// Player lifetime statistics across all games.
/// Stored in database and displayed in player profile views.
/// </summary>
public record LifetimeStats(
    int GamesPlayed,
    int Wins,
    int LongestRoadRecord,
    int HighestScoreRecord
)
{
    public static LifetimeStats Default { get; } = new(0, 0, 0, 0);
}
```

**Location:** `Catan3.Shared/Models/GameStats.cs`

```csharp
namespace Catan3.Shared.Models;

/// <summary>
/// Per-game statistics (transient, not persisted).
/// Calculated during gameplay and displayed in game UI.
/// </summary>
public record GameStats(
    int ResourcesCollected,
    int TradesMade,
    int RoadsBuilt,
    int SettlementsBuilt,
    int CitiesBuilt
)
{
    public static GameStats Default { get; } = new(0, 0, 0, 0, 0);
}
```

#### PlayerProfile (Storage Layer)

**Location:** `Catan3.Shared/ViewData/PlayerProfile.cs` (renamed from PlayerData.cs)

```csharp
namespace Catan3.Shared.ViewData;

/// <summary>
/// Represents a player's persistent profile information.
/// Stored as JSON document in database (SQLite → CosmosDB).
/// Maintains hierarchical structure matching document model.
/// </summary>
public class PlayerProfile
{
    /// <summary>Player unique identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Player display name.</summary>
    public required string Name { get; init; }

    /// <summary>Player color scheme (nested).</summary>
    public required PlayerColors Colors { get; init; }

    /// <summary>URI to player avatar image.</summary>
    public string? ImageUri { get; init; }

    /// <summary>Lifetime statistics (nested, optional).</summary>
    public LifetimeStats? LifetimeStats { get; init; }

    // ==================== Persistence APIs ====================

    /// <summary>
    /// Validates that all required fields are present and valid.
    /// </summary>
    public bool IsValid() =>
        !string.IsNullOrEmpty(Id) &&
        !string.IsNullOrEmpty(Name) &&
        Colors != null &&
        IsValidHexColor(Colors.Primary) &&
        IsValidHexColor(Colors.Secondary) &&
        IsValidHexColor(Colors.Foreground);

    /// <summary>
    /// Creates a deep copy with updated lifetime stats.
    /// Used when persisting stats after game completion.
    /// </summary>
    public PlayerProfile WithUpdatedStats(LifetimeStats newStats) =>
        this with { LifetimeStats = newStats };

    /// <summary>
    /// Serializes to JSON for database storage.
    /// </summary>
    public string ToJson() => JsonSerializer.Serialize(this);

    /// <summary>
    /// Deserializes from JSON database document.
    /// </summary>
    public static PlayerProfile FromJson(string json) =>
        JsonSerializer.Deserialize<PlayerProfile>(json)
            ?? throw new InvalidOperationException("Failed to deserialize PlayerProfile");

    private static bool IsValidHexColor(string color) =>
        !string.IsNullOrEmpty(color) &&
        color.StartsWith('#') &&
        color.Length == 7;
}
```

#### PlayerViewModel (View Layer)

**Location:** `Catan3.WebUI/Services/PlayerViewModel.cs`

```csharp
namespace Catan3.WebUI.Services;

/// <summary>
/// Player view model for WebUI rendering.
/// Maintains same hierarchical structure as PlayerProfile but exposes view-specific APIs.
/// Follows Desktop's PlayerViewModel pattern (without MVVM observability).
/// </summary>
public class PlayerViewModel
{
    /// <summary>Player unique identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Player display name.</summary>
    public required string Name { get; init; }

    /// <summary>Player color scheme (nested - same structure as PlayerProfile).</summary>
    public required PlayerColors Colors { get; init; }

    /// <summary>Player avatar image URI.</summary>
    public string? ImageUri { get; init; }

    /// <summary>Lifetime statistics (may not be used in all views).</summary>
    public LifetimeStats? LifetimeStats { get; init; }

    /// <summary>Current game statistics (transient, not in PlayerProfile).</summary>
    public GameStats? GameStats { get; set; }  // Mutable - updated during game

    // ==================== View/Rendering APIs ====================

    /// <summary>
    /// Gets CSS gradient string for player background.
    /// </summary>
    public string CssGradient =>
        $"linear-gradient(135deg, {Colors.Primary}, {Colors.Secondary})";

    /// <summary>
    /// Gets colors as tuple for minimal privilege rendering (roads, buildings).
    /// Follows principle of least privilege - only passes what's needed.
    /// </summary>
    public (string primary, string secondary, string foreground) GetRenderColors() =>
        (Colors.Primary, Colors.Secondary, Colors.Foreground);

    /// <summary>
    /// Gets formatted win rate for display.
    /// </summary>
    public string WinRateDisplay =>
        LifetimeStats?.GamesPlayed > 0
            ? $"{(LifetimeStats.Wins * 100.0 / LifetimeStats.GamesPlayed):F1}%"
            : "N/A";

    /// <summary>
    /// Creates PlayerViewModel from PlayerProfile (simple deserialization).
    /// No transformation needed - same structure.
    /// </summary>
    public static PlayerViewModel FromProfile(PlayerProfile profile) => new()
    {
        Id = profile.Id,
        Name = profile.Name,
        Colors = profile.Colors,           // Same nested object
        ImageUri = profile.ImageUri,
        LifetimeStats = profile.LifetimeStats  // Same nested object
    };

    /// <summary>
    /// Updates game stats during gameplay.
    /// </summary>
    public void UpdateGameStats(GameStats stats)
    {
        GameStats = stats;
    }
}
```

## Data Flow

### Current Flow (Before Refactoring)

```
Database (Players table)
    ↓ (PlayerEntity.Data JSON → PlayerData)
GameService (/api/players)
    ↓ (Dictionary<string, PlayerData>)
WebUI (GameStateService)
    ↓ (PlayerData passed to renderers)
Renderers (extract colors from PlayerData)
```

**Problems:**
- Renderers receive full PlayerData (over-privileged)
- Dictionary doesn't preserve player order
- Unclear separation between storage and rendering

### Proposed Flow (After Refactoring)

```
Database (Players table)
    ↓ (PlayerEntity.Data JSON → PlayerProfile)
GameService (/api/players)
    ↓ (List<PlayerProfile> - ordered by game join sequence)
WebUI (GameStateService)
    ↓ (List<WebPlayerViewModel> - converted on load)
    ↓ (Fast lookup API: GetPlayerViewModel(id))
BoardSvgGenerator.GenerateSvg()
    ↓ (PlayerColors only - extracted from WebPlayerViewModel)
Renderers (use minimal PlayerColors)
```

**Benefits:**
- Clear separation of concerns at each layer
- Minimal privilege for renderers
- Guaranteed player order preservation
- Fast lookups via indexed API

## API Design

### PlayerProfile Extensions

**Location:** `Catan3.Shared/Extensions/PlayerProfileExtensions.cs`

```csharp
namespace Catan3.Shared.Extensions;

public static class PlayerProfileExtensions
{
    /// <summary>
    /// Gets player colors for rendering (minimal privilege).
    /// </summary>
    public static (string primary, string secondary, string foreground) GetColors(
        this PlayerProfile profile) =>
        (profile.PrimaryBackgroundColor, profile.SecondaryBackgroundColor, profile.ForegroundColor);

    /// <summary>
    /// Creates CSS gradient string from player colors.
    /// </summary>
    public static string ToCssGradient(this PlayerProfile profile) =>
        $"linear-gradient(135deg, {profile.PrimaryBackgroundColor}, {profile.SecondaryBackgroundColor})";
}
```

### GameStateService Player Management

**Location:** `Catan3.WebUI/Services/GameStateService.cs`

```csharp
public class GameStateService
{
    private GameModel? _gameModel;
    private List<WebPlayerViewModel> _players = new();
    private Dictionary<string, int> _playerIndex = new();  // Fast lookup: playerId → index

    /// <summary>
    /// Gets all players in turn order.
    /// </summary>
    public IReadOnlyList<WebPlayerViewModel> Players => _players.AsReadOnly();

    /// <summary>
    /// Gets current player view model.
    /// </summary>
    public WebPlayerViewModel? CurrentPlayer =>
        _gameModel != null ? GetPlayerViewModel(_gameModel.CurrentPlayerId) : null;

    /// <summary>
    /// Gets player view model by ID with O(1) lookup.
    /// </summary>
    public WebPlayerViewModel? GetPlayerViewModel(string playerId)
    {
        if (_playerIndex.TryGetValue(playerId, out var index))
            return _players[index];
        return null;
    }

    /// <summary>
    /// Loads player profiles and converts to view models.
    /// Preserves order from API response.
    /// </summary>
    public async Task LoadPlayersAsync(List<PlayerProfile> profiles)
    {
        _players = profiles.Select(WebPlayerViewModel.FromProfile).ToList();

        // Build index for O(1) lookups
        _playerIndex.Clear();
        for (int i = 0; i < _players.Count; i++)
        {
            _playerIndex[_players[i].Id] = i;
        }

        NotifyStateChanged();
    }
}
```

### BoardSvgGenerator Color Extraction

**Location:** `Catan3.WebUI/Services/Rendering/BoardSvgGenerator.cs`

```csharp
public static string GenerateSvg(
    this GameModel gameModel,
    IReadOnlyList<WebPlayerViewModel> players,  // Changed from Dictionary
    int shownStars = 0,
    HashSet<HexCoordinates>? dimmedTiles = null)
{
    // Build quick lookup dictionary
    var playerLookup = players.ToDictionary(p => p.Id);

    // Current player colors
    var currentPlayer = gameModel.CurrentPlayer();
    var currentPlayerVm = playerLookup.TryGetValue(currentPlayer.Id, out var cp) ? cp : null;
    var currentPlayerColors = currentPlayerVm?.Colors;

    // Render roads with appropriate colors
    foreach (var road in gameModel.Roads)
    {
        // Get colors (owner's colors or current player's colors)
        PlayerColors? colors = road.OwnerId != null && playerLookup.TryGetValue(road.OwnerId, out var owner)
            ? owner.Colors
            : currentPlayerColors;

        sb.Append(road.RenderSvg(colors, road.BuildIndex, opacity));
    }

    // ... similar for buildings
}
```

### Updated Renderer Signatures

```csharp
// RoadSvgRenderer.cs
public static string RenderSvg(
    this RoadModel road,
    PlayerColors? colors,      // Minimal privilege - only colors needed
    int buildIndex = 0,
    double opacity = 0.0)

// BuildingSvgRenderer.cs
public static string RenderSvg(
    this BuildingModel building,
    PlayerColors? colors,      // Minimal privilege
    BuildingVisualState visualState,
    int stars = -1,
    int buildIndex = 0)
```

## Migration Plan

### Phase 1: Rename PlayerData → PlayerProfile

1. **Use Visual Studio Rename Symbol** (recommended):
   - Right-click `PlayerData` class → Rename
   - Enter `PlayerProfile`
   - Let VS update all references automatically

2. **Alternative: Manual sed replacement** (if needed):
   ```bash
   # Windows PowerShell
   Get-ChildItem -Recurse -Include *.cs | ForEach-Object {
       (Get-Content $_) -replace '\bPlayerData\b', 'PlayerProfile' | Set-Content $_
   }
   ```

3. **Files to update**:
   - `Catan3.Shared/ViewData/PlayerData.cs` → `PlayerProfile.cs`
   - Database seeder, API controllers, GameService
   - WebUI components and services
   - All using statements

### Phase 2: Update GameService API

1. **Update API response type**:
   ```csharp
   // GameApiController.cs
   [HttpGet("api/players")]
   public async Task<ActionResult<List<PlayerProfile>>> GetPlayers()
   {
       var profiles = await _dbContext.Players
           .OrderBy(p => p.Id)  // Ensure consistent ordering
           .Select(e => JsonSerializer.Deserialize<PlayerProfile>(e.Data))
           .ToListAsync();
       return Ok(profiles);
   }
   ```

2. **Update game join/create to return ordered list**:
   ```csharp
   [HttpPost("api/game/new")]
   public async Task<ActionResult<GameResponse>> CreateGame(
       List<string> playerIds,  // Order matters!
       GameType gameType)
   {
       // Preserve player order in response
       var orderedProfiles = new List<PlayerProfile>();
       foreach (var id in playerIds)
       {
           var profile = await GetPlayerProfileAsync(id);
           orderedProfiles.Add(profile);
       }

       return Ok(new GameResponse
       {
           GameModel = game,
           Players = orderedProfiles
       });
   }
   ```

### Phase 3: Create WebPlayerViewModel

1. Create new files:
   - `WebUI/Services/Rendering/PlayerColors.cs`
   - `WebUI/Services/WebPlayerViewModel.cs`

2. Update `GameStateService`:
   - Change `Dictionary<string, PlayerProfile>` → `List<WebPlayerViewModel>`
   - Add `_playerIndex` for fast lookups
   - Add `GetPlayerViewModel(id)` method
   - Update `LoadPlayersAsync()` to convert and index

### Phase 4: Update Renderers

1. Update renderer signatures to accept `PlayerColors?`
2. Update `BoardSvgGenerator.GenerateSvg()` to extract colors
3. Update all call sites

### Phase 5: Testing

1. **Build verification**: Ensure all projects build
2. **Runtime testing**:
   - Player order preserved in UI
   - Colors render correctly
   - Build indices display correctly
3. **Performance**: Verify O(1) player lookups work

## Extensibility Scenarios

### Scenario 1: Adding Game Statistics

**Requirement:** Display per-game statistics (resources collected, trades made, etc.)

**Implementation:**

```csharp
// Add to PlayerModel (game state)
public class PlayerModel
{
    // ... existing properties
    public GameStats? Stats { get; set; }
}

public class GameStats
{
    public int ResourcesCollected { get; set; } = 0;
    public int TradesMade { get; set; } = 0;
    public int RoadsBuilt { get; set; } = 0;
    // etc.
}

// Add to WebPlayerViewModel (rendering)
public class GameStatsViewModel
{
    public string ResourcesCollected { get; init; } = "0";
    public string TradesMade { get; init; } = "0";

    public static GameStatsViewModel FromStats(GameStats? stats) => new()
    {
        ResourcesCollected = stats?.ResourcesCollected.ToString() ?? "0",
        TradesMade = stats?.TradesMade.ToString() ?? "0"
    };
}

public class WebPlayerViewModel
{
    // ... existing properties
    public GameStatsViewModel? GameStats { get; init; }
}
```

**UI Usage:**
```razor
@* Game.razor *@
<div class="player-stats">
    <span>Resources: @playerVm.GameStats?.ResourcesCollected</span>
    <span>Trades: @playerVm.GameStats?.TradesMade</span>
</div>
```

**Changes Required:**
- ✅ Add `GameStats` class to `Catan3.Shared/Models/`
- ✅ Add `GameStatsViewModel` to WebUI
- ✅ Update `WebPlayerViewModel.FromProfile()` to map stats
- ❌ No database changes (game stats are transient)
- ❌ No API changes (stats flow through GameModel)

### Scenario 2: Adding Lifetime Statistics

**Requirement:** Display lifetime player statistics (total wins, games played, etc.)

**Implementation:**

```csharp
// Add to PlayerProfile (storage)
public class PlayerProfile
{
    // ... existing properties
    public LifetimeStats? LifetimeStats { get; init; }
}

public class LifetimeStats
{
    public int GamesPlayed { get; init; } = 0;
    public int Wins { get; init; } = 0;
    public int LongestRoad { get; init; } = 0;
}

// Add to WebPlayerViewModel (rendering)
public class LifetimeStatsViewModel
{
    public string GamesPlayed { get; init; } = "0";
    public string WinRate { get; init; } = "0%";

    public static LifetimeStatsViewModel FromStats(LifetimeStats? stats)
    {
        if (stats == null) return new();

        var winRate = stats.GamesPlayed > 0
            ? (stats.Wins * 100.0 / stats.GamesPlayed).ToString("F1")
            : "0";

        return new()
        {
            GamesPlayed = stats.GamesPlayed.ToString(),
            WinRate = $"{winRate}%"
        };
    }
}

public class WebPlayerViewModel
{
    // ... existing properties
    public LifetimeStatsViewModel? LifetimeStats { get; init; }
}
```

**Changes Required:**
- ✅ Add `LifetimeStats` class to PlayerProfile
- ✅ Update database schema (add to PlayerEntity.Data JSON)
- ✅ Update `WebPlayerViewModel.FromProfile()` to map lifetime stats
- ✅ UI components can immediately use `playerVm.LifetimeStats?.WinRate`
- ❌ No GameModel changes (lifetime stats not part of game state)

### Scenario 3: Adding Player Avatar Display

**Requirement:** Show player avatar images in game UI

**Current State:** PlayerProfile already has `ImageUri` property

**Implementation:**

```csharp
// Already supported in WebPlayerViewModel
public class WebPlayerViewModel
{
    public string? ImageUri { get; init; }  // ✅ Already present
}

// Usage in Razor component
<img src="@playerVm.ImageUri" alt="@playerVm.Name" class="player-avatar" />
```

**Changes Required:**
- ❌ No model changes needed
- ❌ No API changes needed
- ✅ Just use existing `ImageUri` property in UI

## Benefits of This Architecture

### Separation of Concerns
- **Storage (PlayerProfile):** Persistent profile data, database schema
- **Game State (PlayerModel):** In-game transient state
- **Rendering (WebPlayerViewModel):** UI-optimized view models

### Minimal Privilege
- Renderers only receive `PlayerColors`, not full profile data
- Clear contracts about what data flows where

### Extensibility
- Adding game statistics: Extend PlayerModel + WebPlayerViewModel
- Adding lifetime statistics: Extend PlayerProfile + WebPlayerViewModel
- Adding UI display fields: Extend WebPlayerViewModel only
- Minimal touch points for each scenario

### Performance
- `List<WebPlayerViewModel>` preserves player order (turn sequence)
- `Dictionary<string, int>` index enables O(1) player lookups
- Color extraction happens once during view model creation

### Type Safety
- Compile-time verification of color usage
- Clear types prevent string/object confusion
- Record types for immutability guarantees

## Desktop Compatibility

This refactoring is WebUI-specific and does not affect Desktop architecture:

- Desktop keeps `PlayerData` name (not renamed)
- Desktop keeps `PlayerViewModel` with XAML bindings
- `PlayerProfile` exists in `Catan3.Shared` but Desktop doesn't use it
- GameService serves both Desktop (PlayerData) and WebUI (PlayerProfile) via different endpoints if needed

Alternative: Rename globally and update Desktop to use PlayerProfile. This provides consistency but requires Desktop changes.

## Open Questions

1. **Global rename vs WebUI-only?**
   - Option A: Rename PlayerData → PlayerProfile everywhere (Desktop + WebUI)
   - Option B: Keep PlayerData in Desktop, use PlayerProfile only in WebUI/Shared

2. **API versioning?**
   - Do we need `/api/v2/players` or can we break existing API?
   - Desktop compatibility implications?

3. **Player ordering authority?**
   - Who determines player order: GameService or client?
   - Current: Player order = order of IDs in CreateGame request

4. **Statistics storage location?**
   - Lifetime stats: PlayerProfile (database)
   - Game stats: PlayerModel (in-memory)
   - Do we need both immediately or defer?

## Implementation Plan

### Phase 1: Restructure Catan3.Shared ✅ COMPLETED

1. ✅ Rename `PlayerData` → `PlayerProfile` (Visual Studio symbol rename)
2. ✅ Stage rename in git (`git add` delete + add)

### Phase 2: Create PlayerProfile Namespace (CURRENT)

**Step 1:** Create directory structure
```bash
mkdir Catan3.Shared/PlayerProfile
```

**Step 2:** Move PlayerProfile.cs
```bash
git mv Catan3.Shared/ViewData/PlayerProfile.cs Catan3.Shared/PlayerProfile/PlayerProfile.cs
```

**Step 3:** Update namespace in PlayerProfile.cs
- Change: `namespace Catan3.Shared.ViewData` → `namespace Catan3.Shared.PlayerProfile`
- Update all using statements across solution (Visual Studio will prompt)

**Step 4:** Create nested types in `Catan3.Shared/PlayerProfile/`:
- `PlayerColors.cs` - Color scheme record
- `GameStats.cs` - Per-game statistics record
- `LifetimeStats.cs` - Lifetime statistics (contains GameStats Totals)

**Step 5:** Update PlayerProfile to use nested PlayerColors
- Change flat color properties → `public PlayerColors Colors { get; init; }`
- Add backward-compatible constructor
- Update JSON serialization

### Phase 3: Create WebUI.Models.PlayerViewModel

**Step 1:** Create directory
```bash
mkdir WebUI/Models
```

**Step 2:** Create `WebUI/Models/PlayerViewModel.cs`
- Namespace: `Catan3.WebUI.Models`
- Same hierarchical structure as PlayerProfile
- Web-specific APIs (CssGradient, GetRenderColors)
- Static `FromProfile()` factory method

**Step 3:** Update GameStateService
- Change: `Dictionary<string, PlayerProfile>` → `List<PlayerViewModel>`
- Add: `Dictionary<string, int> _playerIndex` for O(1) lookups
- Add: `GetPlayerViewModel(id)` method
- Update: `LoadPlayersAsync()` to convert profiles → view models

### Phase 4: Update Renderers

**Step 1:** Update renderer signatures to accept color tuples:
```csharp
// RoadSvgRenderer.cs
public static string RenderSvg(
    this RoadModel road,
    (string primary, string secondary, string foreground)? colors,
    int buildIndex = 0,
    double opacity = 0.0)

// BuildingSvgRenderer.cs
public static string RenderSvg(
    this BuildingModel building,
    (string primary, string secondary, string foreground)? colors,
    BuildingVisualState visualState,
    int stars = -1,
    int buildIndex = 0)
```

**Step 2:** Update BoardSvgGenerator.GenerateSvg()
- Change parameter: `IReadOnlyDictionary<string, PlayerProfile>` → `IReadOnlyList<PlayerViewModel>`
- Extract colors using `playerVm.GetRenderColors()`
- Pass color tuples to renderers

**Step 3:** Update all call sites (Pages, Components)

### Phase 5: Build and Test

1. Build solution - verify all projects compile
2. Run tests - verify existing tests pass
3. Manual testing:
   - Player colors render correctly
   - Build indices display correctly
   - Player order preserved in UI
4. Performance verification - O(1) player lookups

### Phase 6: Cleanup

1. Remove old `ViewData` directory if empty
2. Update code review documentation
3. Create session summary
4. Commit changes with descriptive message

## What Requires Visual Studio Symbol Rename

- ❌ **None currently** - PlayerData → PlayerProfile already completed

## What Can Be Automated (Scripts/Claude)

- ✅ Creating new files (PlayerColors.cs, GameStats.cs, etc.)
- ✅ Moving files (`git mv`)
- ✅ Updating existing code
- ✅ Adding using statements

## Next Steps

1. ✅ Design document approved
2. **→ Execute Phase 2: Create PlayerProfile namespace**
3. Execute Phase 3: Create WebUI PlayerViewModel
4. Execute Phase 4: Update renderers
5. Execute Phase 5: Build and test
6. Execute Phase 6: Cleanup and commit

## References

- `database-design.md` - PlayerProfile storage in database
- `WebUI-Design.md` - WebUI thick client architecture
- `mvvm-pattern.md` - Desktop MVVM pattern for comparison

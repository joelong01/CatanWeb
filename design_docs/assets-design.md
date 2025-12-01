# Assets Design Document

## Overview

This document describes the asset architecture for the Catan game, covering the existing Desktop app's resource system and the planned approach for WebUI asset delivery. The goal is to reuse the high-resolution tile images from the Desktop app in the WebUI with efficient caching and rendering.

**Update 2025-11-30**: Added theme system design to support multiple visual themes (e.g., "Classic", "StarTrek").

## Theme System Architecture

### Design Goals

1. **Theme Switching**: Allow users to select different visual themes (e.g., "Classic", "StarTrek", "Minimal")
2. **Strongly Typed**: All asset references use enums - no magic strings
3. **Fallback Support**: Themes can inherit from a base theme, only overriding specific assets
4. **Consistent Interface**: Single `IAssetService` provides all asset paths
5. **Organized Directory Structure**: Assets organized by usage, not file type

### Asset Name Enum

All assets are referenced via a strongly-typed enum:

```csharp
/// <summary>
/// Strongly-typed identifiers for all game assets.
/// Used by IAssetService to resolve asset paths for the current theme.
/// </summary>
public enum AssetName
{
    // === Tiles (hex backgrounds) ===
    TileBrick,
    TileWheat,
    TileWood,
    TileOre,
    TileSheep,
    TileDesert,
    TileGoldMine,
    TileSea,
    TileInvasion,

    // === Harbors (trade port images) ===
    HarborBrick,
    HarborOre,
    HarborSheep,
    HarborWheat,
    HarborWood,
    HarborThreeForOne,

    // === Resource Cards (player hand) ===
    CardBrick,
    CardWheat,
    CardWood,
    CardOre,
    CardSheep,
    CardGoldMine,
    CardCloth,
    CardPaper,
    CardCoin,
    CardTrade,
    CardPolitics,
    CardScience,
    CardVictoryPoint,
    CardBack,
    CardRobber,
    CardAnyDev,

    // === Stats (player statistics icons) ===
    StatScore,
    StatRoads,
    StatKnights,
    StatCities,
    StatSettlements,
    StatShips,
    StatDevCards,
    StatResourceCards,
    StatHarbors,
    StatLongestRoad,
    StatLargestArmy,
    StatMetropolis,
    StatGoodRoll,
    StatBadRoll,
    StatTargetted,
    StatRobber,

    // === Buildings (board pieces) ===
    BuildingCity,
    BuildingSettlement,
    BuildingRoad,
    BuildingShip,

    // === Backgrounds ===
    BackgroundWater,
    BackgroundBorder,

    // === Miscellaneous ===
    IconCheck,
    IconStar,
    FontCatan
}
```

### IAssetService Interface

The `AssetService` is **format-agnostic**. It knows about themes, file paths, and metadata - but nothing
about how assets are rendered (SVG, CSS, XAML, etc.). Consumers take the path and wrap it in whatever
format they need.

```csharp
/// <summary>
/// Provides theme-aware asset path resolution.
/// All game code requests assets through this service rather than hardcoding paths.
/// This service is format-agnostic - it returns paths, not rendered output.
/// </summary>
public interface IAssetService
{
    /// <summary>
    /// Gets the current theme name (e.g., "classic", "startrek").
    /// </summary>
    string CurrentTheme { get; }

    /// <summary>
    /// Gets the URL path to an asset for use in HTML/CSS (e.g., "/themes/classic/tiles/brick.png").
    /// </summary>
    /// <param name="asset">The asset to retrieve.</param>
    /// <returns>URL path relative to wwwroot.</returns>
    string GetAssetPath(AssetName asset);

    /// <summary>
    /// Gets the MIME type for an asset based on file extension.
    /// </summary>
    /// <param name="asset">The asset to query.</param>
    /// <returns>MIME type string (e.g., "image/png", "image/svg+xml").</returns>
    string GetMimeType(AssetName asset);

    /// <summary>
    /// Gets all available theme names (internal identifiers).
    /// </summary>
    IReadOnlyList<string> AvailableThemes { get; }

    /// <summary>
    /// Gets metadata for a specific theme (display name, description, preview path).
    /// </summary>
    /// <param name="themeName">Theme name (case-insensitive).</param>
    ThemeMetadata GetThemeMetadata(string themeName);

    /// <summary>
    /// Gets metadata for the current theme.
    /// </summary>
    ThemeMetadata GetCurrentThemeMetadata();

    /// <summary>
    /// Sets the current theme.
    /// </summary>
    /// <param name="themeName">Theme name (case-insensitive).</param>
    void SetTheme(string themeName);

    /// <summary>
    /// Event raised when theme changes.
    /// </summary>
    event Action<string> ThemeChanged;
}
```

**Separation of Concerns:**

| Responsibility | Owner |
|----------------|-------|
| Theme selection, inheritance, fallback | `AssetService` |
| Asset name → file path mapping | `AssetService` |
| Theme metadata (JSON) loading | `AssetService` |
| SVG pattern generation | `BoardSvgGenerator` / `TileSvgRenderer` |
| CSS background/brush syntax | Blazor components |
| XAML ImageBrush syntax | Desktop app converters |

The consumer knows its output format; the service knows where files live.

### AssetService Implementation

Uses a two-level lookup: theme overrides → base fallback. No recursion, no file system probing at runtime.
All mappings loaded from JSON at startup.

**Data Model:**

```csharp
/// <summary>
/// Default implementation of IAssetService with two-level lookup.
/// Base theme contains ALL assets. Other themes only contain overrides.
/// All mappings are loaded from theme.json files at startup.
/// </summary>
public class AssetService : IAssetService
{
    /// <summary>
    /// Complete asset set from "base" theme. Every AssetName has an entry.
    /// </summary>
    private readonly Dictionary<AssetName, string> _baseAssets;

    /// <summary>
    /// Sparse override dictionaries for each theme. Only contains assets declared in theme.json.
    /// </summary>
    private readonly Dictionary<string, Dictionary<AssetName, string>> _themeOverrides;

    /// <summary>
    /// Theme metadata (display name, description, preview) for each theme.
    /// </summary>
    private readonly Dictionary<string, ThemeMetadata> _themeMetadata;

    private string _currentTheme = "classic";

    public string CurrentTheme => _currentTheme;
    public IReadOnlyList<string> AvailableThemes => _themeMetadata.Keys.Where(k => k != "base").ToList();
    public event Action<string>? ThemeChanged;

    public AssetService(IWebHostEnvironment env)
    {
        (_baseAssets, _themeOverrides, _themeMetadata) = LoadThemes(env.WebRootPath);
    }

    public string GetAssetPath(AssetName asset)
    {
        // Check theme overrides first (sparse - may not contain this asset)
        if (_themeOverrides.TryGetValue(_currentTheme, out var overrides)
            && overrides.TryGetValue(asset, out var path))
        {
            return path;
        }

        // Fall back to base (always complete)
        return _baseAssets[asset];
    }

    public ThemeMetadata GetThemeMetadata(string themeName)
    {
        return _themeMetadata.GetValueOrDefault(themeName.ToLowerInvariant())
            ?? throw new ArgumentException($"Unknown theme: {themeName}");
    }

    public ThemeMetadata GetCurrentThemeMetadata() => GetThemeMetadata(_currentTheme);

    public string GetMimeType(AssetName asset)
    {
        var path = GetAssetPath(asset);
        return path switch
        {
            _ when path.EndsWith(".svg") => "image/svg+xml",
            _ when path.EndsWith(".png") => "image/png",
            _ when path.EndsWith(".jpg") or path.EndsWith(".jpeg") => "image/jpeg",
            _ when path.EndsWith(".webp") => "image/webp",
            _ when path.EndsWith(".ttf") => "font/ttf",
            _ => "application/octet-stream"
        };
    }

    public void SetTheme(string themeName)
    {
        var normalized = themeName.ToLowerInvariant();
        if (_themeMetadata.ContainsKey(normalized) && normalized != "base")
        {
            _currentTheme = normalized;
            ThemeChanged?.Invoke(_currentTheme);
        }
    }

    private static (Dictionary<AssetName, string>, Dictionary<string, Dictionary<AssetName, string>>,
                    Dictionary<string, ThemeMetadata>) LoadThemes(string webRootPath)
    {
        var baseAssets = new Dictionary<AssetName, string>();
        var themeOverrides = new Dictionary<string, Dictionary<AssetName, string>>();
        var themeMetadata = new Dictionary<string, ThemeMetadata>();

        var themesDir = Path.Combine(webRootPath, "themes");

        // Load all theme.json files
        foreach (var themeDir in Directory.GetDirectories(themesDir))
        {
            var themeName = Path.GetFileName(themeDir).ToLowerInvariant();
            var jsonPath = Path.Combine(themeDir, "theme.json");

            if (!File.Exists(jsonPath)) continue;

            var json = File.ReadAllText(jsonPath);
            var definition = JsonSerializer.Deserialize<ThemeDefinition>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            // Store metadata
            themeMetadata[themeName] = new ThemeMetadata
            {
                Name = definition.Name,
                DisplayName = definition.DisplayName,
                Description = definition.Description,
                Preview = definition.Preview
            };

            // Parse asset mappings
            var assets = new Dictionary<AssetName, string>();
            foreach (var (key, value) in definition.Assets)
            {
                if (Enum.TryParse<AssetName>(key, ignoreCase: true, out var assetName))
                {
                    assets[assetName] = value;
                }
            }

            if (themeName == "base")
            {
                baseAssets = assets;
            }
            else
            {
                themeOverrides[themeName] = assets;
            }
        }

        // Verify base theme is complete
        foreach (AssetName asset in Enum.GetValues<AssetName>())
        {
            if (!baseAssets.ContainsKey(asset))
                throw new InvalidOperationException($"Base theme missing required asset: {asset}");
        }

        return (baseAssets, themeOverrides, themeMetadata);
    }
}
```

**ThemeMetadata Record:**

```csharp
/// <summary>
/// Theme metadata extracted from theme.json for UI display.
/// </summary>
public record ThemeMetadata
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public string Preview { get; init; } = "preview.png";
}
```

**Lookup Performance:**

| Operation | Complexity |
|-----------|------------|
| GetAssetPath (theme has override) | O(1) - two hash lookups |
| GetAssetPath (fallback to base) | O(1) - two hash lookups |
| SetTheme | O(1) - one hash lookup |
| Memory | O(base assets) + O(sum of overrides) |
| Startup | O(themes × assets) - one-time JSON parsing |

### Theme Definition

Each theme is defined by a `theme.json` file containing metadata AND asset mappings. The JSON is the
single source of truth - "any problem in computer science can be solved by adding a layer of indirection."

**Key Design Points:**

1. **Explicit mappings**: All asset paths are declared in JSON, not discovered by scanning
2. **Complete decoupling**: Logical `AssetName` has no relationship to physical file path or name
3. **Base is complete**: Base theme must map every `AssetName` to a path
4. **Themes are sparse**: Other themes only list overrides, falling back to base for unlisted assets
5. **Cross-theme references**: A theme can point to files in any location, including other themes

**ThemeDefinition Model:**

```csharp
/// <summary>
/// Represents a theme's metadata and asset mappings.
/// Loaded from theme.json in each theme directory.
/// </summary>
public record ThemeDefinition
{
    /// <summary>
    /// Internal theme identifier (lowercase, no spaces). E.g., "base", "classic", "startrek".
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Human-readable display name. E.g., "Classic", "Star Trek".
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Optional description shown in theme picker tooltip.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Path to preview thumbnail relative to wwwroot. E.g., "/themes/classic/preview.png".
    /// </summary>
    public string Preview { get; init; } = "preview.png";

    /// <summary>
    /// Asset mappings. Key is AssetName enum string, value is path relative to wwwroot.
    /// Base theme must have ALL assets. Other themes only include overrides.
    /// </summary>
    public Dictionary<string, string> Assets { get; init; } = new();
}
```

**Example theme.json (base) - COMPLETE:**

```json
{
  "name": "base",
  "displayName": "Base",
  "description": "Complete asset set - all other themes inherit from this",
  "preview": "/themes/base/preview.png",
  "assets": {
    "TileBrick": "/themes/base/tiles/brick.png",
    "TileWheat": "/themes/base/tiles/wheat.png",
    "TileWood": "/themes/base/tiles/wood.png",
    "TileOre": "/themes/base/tiles/ore.png",
    "TileSheep": "/themes/base/tiles/sheep.png",
    "TileDesert": "/themes/base/tiles/desert.png",
    "TileGoldMine": "/themes/base/tiles/goldmine.png",
    "TileSea": "/themes/base/tiles/sea.jpg",
    "TileInvasion": "/themes/base/tiles/invasion.png",

    "HarborBrick": "/themes/base/harbors/brick.png",
    "HarborOre": "/themes/base/harbors/ore.png",
    "HarborSheep": "/themes/base/harbors/sheep.png",
    "HarborWheat": "/themes/base/harbors/wheat.png",
    "HarborWood": "/themes/base/harbors/wood.png",
    "HarborThreeForOne": "/themes/base/harbors/threeforone.png",

    "CardBrick": "/themes/base/resources/brick.png",
    "CardWheat": "/themes/base/resources/wheat.png",
    "...": "... (all AssetName values mapped)"
  }
}
```

**Example theme.json (classic) - SPARSE:**

```json
{
  "name": "classic",
  "displayName": "Classic",
  "description": "The original Catan look and feel",
  "preview": "/themes/classic/preview.png",
  "assets": {
  }
}
```

Classic has no overrides - it uses base for everything. It exists as a named theme users can select.

**Example theme.json (startrek) - SPARSE with overrides:**

```json
{
  "name": "startrek",
  "displayName": "Star Trek",
  "description": "Boldly go where no settler has gone before",
  "preview": "/themes/startrek/preview.png",
  "assets": {
    "TileBrick": "/themes/startrek/tiles/dilithium.png",
    "TileWheat": "/themes/startrek/tiles/tritanium.svg",
    "TileWood": "/themes/startrek/tiles/duranium.png",
    "TileOre": "/themes/startrek/tiles/latinum.png",
    "TileSheep": "/themes/startrek/tiles/biomatter.png",
    "TileDesert": "/themes/startrek/tiles/nebula.svg",
    "BackgroundWater": "/themes/startrek/backgrounds/space.jpg"
  }
}
```

Star Trek overrides 7 assets. Everything else (harbors, stats, cards, etc.) falls back to base.

**Flexibility Examples:**

A theme can reference assets from anywhere:

```json
{
  "name": "remix",
  "displayName": "Remix",
  "description": "Mix and match from different themes",
  "assets": {
    "TileBrick": "/themes/startrek/tiles/dilithium.png",
    "TileWheat": "/themes/base/tiles/wheat.png",
    "TileWood": "/shared/special/custom-wood.svg",
    "BackgroundWater": "https://cdn.example.com/water.jpg"
  }
}
```

### Directory Structure

Suggested wwwroot organization. Since JSON contains explicit paths, files can be organized any way
that makes sense - the structure below is a recommendation, not a requirement.

```text
WebUI/wwwroot/
├── themes/
│   ├── base/                       # Base theme - JSON maps ALL AssetName values
│   │   ├── theme.json              # Complete asset mappings (see example above)
│   │   ├── preview.png             # Theme thumbnail
│   │   ├── tiles/
│   │   │   ├── brick.png
│   │   │   ├── wheat.png
│   │   │   ├── wood.png
│   │   │   ├── ore.png
│   │   │   ├── sheep.png
│   │   │   ├── desert.png
│   │   │   ├── goldmine.png
│   │   │   ├── sea.jpg
│   │   │   └── invasion.png
│   │   ├── harbors/
│   │   │   ├── brick.png
│   │   │   ├── ore.png
│   │   │   ├── sheep.png
│   │   │   ├── wheat.png
│   │   │   ├── wood.png
│   │   │   └── threeforone.png
│   │   ├── resources/              # Resource cards
│   │   │   ├── brick.png
│   │   │   ├── wheat.png
│   │   │   └── ... (all card types)
│   │   ├── stats/                  # Player stat icons
│   │   │   ├── score.svg
│   │   │   ├── roads.svg
│   │   │   └── ... (all stat types)
│   │   ├── buildings/              # Board pieces
│   │   │   ├── city.svg
│   │   │   ├── settlement.svg
│   │   │   ├── road.svg
│   │   │   └── ship.svg
│   │   ├── backgrounds/
│   │   │   ├── water.jpg
│   │   │   └── border.jpg
│   │   └── fonts/
│   │       └── catan.ttf
│   │
│   ├── classic/                    # Classic theme - sparse JSON (may be empty assets:{})
│   │   ├── theme.json              # Metadata only, no overrides
│   │   └── preview.png
│   │
│   ├── startrek/                   # Star Trek theme - sparse JSON with overrides
│   │   ├── theme.json              # Only lists assets that differ from base
│   │   ├── preview.png
│   │   └── tiles/                  # Override files (any name - JSON has the mapping)
│   │       ├── dilithium.png
│   │       ├── tritanium.svg
│   │       ├── duranium.png
│   │       ├── latinum.png
│   │       ├── biomatter.png
│   │       └── nebula.svg
│   │
│   └── minimal/                    # Minimal theme
│       ├── theme.json
│       ├── preview.png
│       └── tiles/
│           └── *.svg               # Vector alternatives
│
├── shared/                         # Assets NOT part of theme system
│   ├── players/                    # Player avatars (user-specific, not themed)
│   │   ├── adrian.jpg
│   │   ├── chris.jpg
│   │   └── ...
│   └── ui/                         # UI chrome (buttons, etc. - not themed)
│       └── ...
│
├── css/
│   └── app.css
└── index.html
```

**Key Points:**

1. **JSON is the source of truth** - file organization is flexible
2. **Base theme must be complete** - JSON must map every `AssetName` to a path
3. **Other themes are sparse** - JSON only lists overrides
4. **Files can be anywhere** - JSON paths can point to `/themes/base/...`, `/shared/...`, or even CDN URLs
5. **No naming conventions required** - Star Trek can use `dilithium.png` for brick since JSON maps it

### Usage Examples

**In Blazor Component (consumer owns the HTML/CSS syntax):**

```csharp
@inject IAssetService Assets

<img src="@Assets.GetAssetPath(AssetName.CardBrick)" alt="Brick" />

<div style="background-image: url('@Assets.GetAssetPath(AssetName.BackgroundWater)')"></div>
```

**In BoardSvgGenerator (consumer owns the SVG syntax):**

```csharp
public class BoardSvgGenerator
{
    private readonly IAssetService _assets;

    public BoardSvgGenerator(IAssetService assets)
    {
        _assets = assets;
    }

    /// <summary>
    /// Generates SVG pattern definition for a tile type.
    /// The SVG syntax is owned by this renderer, not the AssetService.
    /// </summary>
    private string CreateTilePattern(AssetName asset, string patternId)
    {
        var path = _assets.GetAssetPath(asset);
        return $"""
            <pattern id="{patternId}" patternUnits="objectBoundingBox" width="1" height="1">
              <image href="{path}" preserveAspectRatio="xMidYMid slice" width="100%" height="100%"/>
            </pattern>
            """;
    }

    private string GenerateTilePatterns()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<defs>");
        sb.AppendLine(CreateTilePattern(AssetName.TileBrick, "tile-brick"));
        sb.AppendLine(CreateTilePattern(AssetName.TileWheat, "tile-wheat"));
        // ... etc
        sb.AppendLine("</defs>");
        return sb.ToString();
    }
}
```

### Theme Picker UI

The theme picker should be accessible from the game UI, likely in a settings/options area or as a toolbar button.

**ThemePicker.razor:**

```razor
@inject IAssetService Assets
@implements IDisposable

<div class="theme-picker">
    <button class="theme-picker-button" @onclick="ToggleDropdown"
            title="@Assets.GetCurrentThemeMetadata().Description">
        <span class="theme-icon">&#xE771;</span> <!-- Segoe MDL2 Color icon -->
        <span class="theme-name">@Assets.GetCurrentThemeMetadata().DisplayName</span>
    </button>

    @if (_isOpen)
    {
        <div class="theme-dropdown">
            @foreach (var themeName in Assets.AvailableThemes)
            {
                var metadata = Assets.GetThemeMetadata(themeName);
                <button class="theme-option @(themeName == Assets.CurrentTheme ? "selected" : "")"
                        @onclick="() => SelectTheme(themeName)"
                        title="@metadata.Description">
                    <img src="/themes/@themeName/@metadata.Preview" alt="@metadata.DisplayName"
                         class="theme-preview" />
                    <span>@metadata.DisplayName</span>
                </button>
            }
        </div>
    }
</div>

@code {
    private bool _isOpen = false;

    protected override void OnInitialized()
    {
        Assets.ThemeChanged += OnThemeChanged;
    }

    private void ToggleDropdown() => _isOpen = !_isOpen;

    private void SelectTheme(string theme)
    {
        Assets.SetTheme(theme);
        _isOpen = false;
    }

    private void OnThemeChanged(string newTheme)
    {
        StateHasChanged();
    }

    public void Dispose()
    {
        Assets.ThemeChanged -= OnThemeChanged;
    }
}
```

All display names, descriptions, and preview paths come from `theme.json` - no hardcoded strings.

**ThemePicker.razor.css:**

```css
.theme-picker {
    position: relative;
    display: inline-block;
}

.theme-picker-button {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 8px 12px;
    background: var(--game-bg-panel);
    border: 1px solid var(--border-color);
    border-radius: 4px;
    color: var(--text-primary);
    cursor: pointer;
}

.theme-picker-button:hover {
    background: var(--accent-hover);
}

.theme-icon {
    font-family: "Segoe MDL2 Assets";
    font-size: 16px;
}

.theme-dropdown {
    position: absolute;
    top: 100%;
    left: 0;
    margin-top: 4px;
    background: var(--game-bg-panel);
    border: 1px solid var(--border-color);
    border-radius: 4px;
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
    z-index: 100;
    min-width: 160px;
}

.theme-option {
    display: flex;
    align-items: center;
    gap: 8px;
    width: 100%;
    padding: 8px 12px;
    background: transparent;
    border: none;
    color: var(--text-primary);
    cursor: pointer;
    text-align: left;
}

.theme-option:hover {
    background: var(--accent-hover);
}

.theme-option.selected {
    background: var(--accent-primary);
}

.theme-preview {
    width: 32px;
    height: 32px;
    border-radius: 4px;
    object-fit: cover;
}
```

**Placement Options:**

1. **Game Page Toolbar**: Add to existing toolbar/header area on Game.razor
2. **Settings Panel**: If a settings/options panel exists, add there
3. **Left Panel**: Add below or above existing controls in the left panel

**Integration in Game.razor:**

```razor
<div class="game-toolbar">
    <!-- Other toolbar items -->
    <ThemePicker />
</div>
```

**Theme Preview Images:**

Each theme should include a `preview.png` (e.g., 64x64 or 128x128) showing a representative sample:
- Classic: A wheat or brick tile
- StarTrek: A dilithium crystal or starship
- Minimal: Abstract colored hexagon

```text
themes/
├── classic/
│   ├── preview.png      # 64x64 preview thumbnail
│   └── ...
├── startrek/
│   ├── preview.png
│   └── ...
```

### ASP.NET Core Best Practices Applied

1. **Dependency Injection**: `IAssetService` registered as a scoped service
2. **Configuration-Driven**: Themes defined in JSON, loaded at startup
3. **Immutable Theme Data**: Theme definitions are read-only after load
4. **Event-Based Updates**: `ThemeChanged` event for reactive UI updates
5. **Fallback Chain**: Theme → BaseTheme → Classic (always present)

### Migration Plan

**Phase 1: Create Directory Structure**

1. Create `wwwroot/themes/base/` directory tree with subdirectories
2. Move existing assets to base theme:
   - `images/tiles/*` → `themes/base/tiles/`
   - `images/harbors/*` → `themes/base/harbors/`
   - `images/resources/*` → `themes/base/resources/`
   - `images/svg/*` → `themes/base/stats/` and `themes/base/buildings/` (split by purpose)
   - `images/textures/*` → `themes/base/backgrounds/`
   - `fonts/*` → `themes/base/fonts/`
3. Create `themes/base/theme.json` with COMPLETE asset mappings (every `AssetName` → path)
4. Create `themes/classic/theme.json` with metadata only (empty `assets: {}`)
5. Create `themes/classic/preview.png`
6. Move `images/players/*` → `shared/players/`
7. Remove orphaned files: `images/cherry.jpg`, `images/maple.jpg`
8. Delete old `images/` directory structure after migration verified

**Phase 2: Implement AssetService**

1. Create `AssetName` enum in `Shared/` project (or `GameService/`)
2. Create `ThemeDefinition` record (for JSON deserialization)
3. Create `ThemeMetadata` record (for UI display)
4. Create `IAssetService` interface with metadata methods
5. Implement `AssetService`:
   - Constructor loads all `theme.json` files
   - Parses asset mappings into `_baseAssets` (from base) and `_themeOverrides` (from others)
   - Validates base theme has all `AssetName` values
6. Register as singleton in DI container (theme data is immutable after load)

**Phase 3: Update Consumers**

1. Update `BoardSvgGenerator` to inject `IAssetService`:
   - Replace hardcoded paths with `_assets.GetAssetPath(AssetName.TileBrick)`
   - SVG pattern generation stays in BoardSvgGenerator (separation of concerns)
2. Update Blazor components:
   - `ResourceCard.razor` - use `Assets.GetAssetPath(AssetName.CardBrick)`
   - `PlayerTile.razor` - use `Assets.GetAssetPath(AssetName.StatScore)` etc.
3. Update any CSS that references image paths (may need CSS variables set from AssetService)

**Phase 4: Add Theme Picker UI**

1. Create `ThemePicker.razor` component
2. Add `ThemePicker.razor.css` styles
3. Add ThemePicker to Game.razor toolbar or settings area
4. Wire up `ThemeChanged` event to trigger re-render of themed components

**Phase 5: Create Additional Themes**

1. Create `themes/startrek/` as proof of concept:
   - Add `theme.json` with metadata and sparse overrides
   - Add `preview.png`
   - Add override image files (any names - JSON maps them)
2. Document theme creation process in this design doc
3. Consider a "minimal" theme with SVG geometric shapes instead of textures

## Desktop App Resource Architecture

### Resource Dictionary Structure

The Desktop app uses XAML ResourceDictionaries loaded in `App.xaml`:

```
DesktopApp/
├── Themes/
│   ├── ImageResources.xaml      # Primary image definitions (40+ ImageBrush)
│   ├── StyleResourceDictionary.xaml  # UI element styles
│   ├── Generic.xaml             # Control templates
│   └── ConverterDictionary.xaml # Value converters
├── Assets/
│   ├── Tiles/                   # Hex tile images
│   ├── Harbors/                 # Harbor images
│   ├── ResourceCards/           # Card images
│   ├── Fonts/                   # Custom Catan font
│   └── DefaultPlayers/          # Player profile images
```

**Load Order (App.xaml):**
1. XamlControlsResources (WinUI3 system styles)
2. ConverterDictionary.xaml (value converters - must load first)
3. Generic.xaml (control templates)
4. StyleResourceDictionary.xaml (UI element styles)
5. ImageResources.xaml (tile/card/harbor images)

### ImageResources.xaml Pattern

Each image is defined as a static `ImageBrush` resource with a key matching the enum value:

```xml
<ImageBrush x:Key="ResourceTileType.Brick"
            ImageSource="../Assets/Tiles/brick.png"
            Stretch="UniformToFill"/>
<ImageBrush x:Key="ResourceTileType.Wheat"
            ImageSource="../Assets/Tiles/wheat.png"
            Stretch="UniformToFill"/>
```

**Usage in XAML controls:**
```xml
<Polygon Fill="{StaticResource ResourceTileType.Brick}"/>
```

### Tile Images (Assets/Tiles/)

| Resource Type | File | Size | Notes |
|--------------|------|------|-------|
| Brick | brick.png | 487KB | Red/brown clay |
| Wheat | wheat.png | 633KB | Yellow grain field |
| Wood | wood.png | 531KB | Green forest |
| Ore | ore.png | 487KB | Gray mountains |
| Sheep | sheep.png | 439KB | Light green pasture |
| Desert | desert.png | 2.3MB | Tan desert |
| Gold Mine | goldMine.png | 5.6MB | Gold/orange mine |
| Sea | back.jpg | 18KB | Blue water |
| Back | back.jpg | 18KB | Tile back (same as sea) |

**Total tile images: ~10MB**

### Harbor Images (Assets/Harbors/)

| Harbor Type | File | Size |
|------------|------|------|
| 2:1 Brick | 2 for 1 brick.png | 72KB |
| 2:1 Ore | 2 for 1 ore.png | 72KB |
| 2:1 Sheep | 2 for 1 sheep.png | 57KB |
| 2:1 Wheat | 2 for 1 wheat.png | 56KB |
| 2:1 Wood | 2 for 1 wood.png | 58KB |
| 3:1 Generic | 3 for 1.png | 55KB |

**Total harbor images: ~370KB**

### Resource Card Images (Assets/ResourceCards/)

19 card types totaling ~97MB including:
- Base resources (brick, wheat, wood, ore, sheep)
- Expansion resources (cloth, paper, coin, gold mine)
- Development cards (trade, politics, science, victory point)
- Special cards (back, robber, invasion)

### Background Images

| Image | File | Size | Usage |
|-------|------|------|-------|
| Water | water.png | 32MB | Board background, water tiles |
| Cherry | cherry.jpg | 2.0MB | Tile border overlay |
| Maple | maple.jpg | 63KB | Alternative background |

### Custom Font (Assets/Fonts/)

| Font | File | Usage |
|------|------|-------|
| Catan | Catan.ttf | Symbol glyphs for game elements (icons, not numbers) |

**Font registration in App.xaml:**
```xml
<FontFamily x:Key="CatanFont">ms-appx:///../Assets/Fonts/Catan.ttf#Catan</FontFamily>
```

**Glyph Definitions (from `DesktopApp/Layout/CatanFont.cs`):**

| Symbol | Unicode | Constant | Usage |
|--------|---------|----------|-------|
| City | \uE900 | `CatanFont.City` | City building icon |
| Settlement | \uE926 | `CatanFont.Settlement` | Settlement building icon |
| Road | \uE909 | `CatanFont.Road` | Road icon |
| Soldier | \uE90E | `CatanFont.Soldier` | Soldier/knight icon |
| Score/Laurel | \uE907 | `CatanFont.Score` | Victory point display |
| Pirate | \uE90C | `CatanFont.Pirate` | Robber/resources lost |
| Target | \uE916 | `CatanFont.Target` | Times targeted |
| Sum | \uE910 | `CatanFont.Sum` | Total resources |
| Longest Road | \uE915 | `CatanFont.LongestRoad` | Achievement badge |
| Good Roll | \uE914 | `CatanFont.GoodRoll` | Favorable dice result |
| Bad Roll | \uE913 | `CatanFont.BadRoll` | Unfavorable dice result |
| Star | \uE911 | `CatanFont.Star` | Buildable indicator |
| Knight | \uE930 | `CatanFont.Knight` | Knight card |
| Ship | \uE90D | `CatanFont.Ship` | Naval unit |
| Metro | \uE90F | `CatanFont.Metro` | Metropolis |

**Primary Usage - PlayerCtrl.xaml:**

The main consumer is the player statistics panel which displays counts with icons:
```xml
<TextBlock FontFamily="{StaticResource CatanFont}" FontSize="28"
           Text="{x:Bind Glyph, Mode=OneWay}"/>
```

Each player stat (roads played, cities built, score, etc.) uses a glyph from CatanFont paired with a count number.

### Number Tokens (Roll Numbers on Tiles)

The number tokens displayed on resource tiles use a **different font system** than the Catan font glyphs.

**Implementation (from `DesktopApp/Tiles/CatanNumber.xaml`):**

```xml
<!-- Number display uses Segoe UI, NOT Catan font -->
<TextBlock FontFamily="Segoe UI" FontWeight="Bold" Text="{x:Bind Number}"/>

<!-- Probability pips use Segoe Fluent Icons -->
<TextBlock FontFamily="Segoe Fluent Icons" Text="{x:Bind Stars}"/>
```

**Probability Pips (Stars):**

The pips below each number indicate roll probability - more pips = more likely to be rolled:

| Number | Pips | Probability | Color |
|--------|------|-------------|-------|
| 2, 12 | 1 | Lowest | White |
| 3, 11 | 2 | Low | White |
| 4, 10 | 3 | Medium | White |
| 5, 9 | 4 | High | White |
| 6, 8 | 5 | Highest | **Red** |
| 7 | 0 | (Robber) | N/A |

**Visual Design:**
- Background: Blue ellipse (`#FF2F6999`, 75% opacity)
- Number: Bold, centered above pips
- Pips: Row of star characters (`\uE00A`) below number
- Red highlighting: Numbers 6 and 8 use red text/pips to indicate they're most likely

**WebUI SVG Rendering:**

For the SVG board, number tokens will be rendered as:
```xml
<g transform="translate(cx, cy)">
  <!-- Blue circle background -->
  <circle r="32" fill="#2F6999" opacity="0.75" stroke="white" stroke-width="0.5"/>

  <!-- Number -->
  <text y="-5" text-anchor="middle" font-family="sans-serif" font-weight="bold"
        font-size="24" fill="red">8</text>

  <!-- Probability pips -->
  <text y="12" text-anchor="middle" font-size="10" fill="red">•••••</text>
</g>
```

### How Images Are Used

1. **Data-Driven Rendering**: Controls use converters to map enum values to ImageBrush resources
2. **Converter Integration**:
   - `ResourceTypeToImageBrush` - Maps ResourceType enum to tile images
   - Similar converters for HarborType and ResourceCard types
3. **Tile Display Layers**:
   - Outer hex: Border brush (cherry.jpg pattern)
   - Inner hex: Resource tile image
   - Number display: Catan.ttf font
4. **Harbor Display**:
   - Positioned on tile vertices
   - Small resource card preview (67×100px)
   - Rotation based on harbor direction
5. **Resource Cards**:
   - Player hand display
   - Trade dialogs
   - Resource distribution
6. **Stretch Mode**: All tiles use `UniformToFill` to maintain aspect ratio

### Enum to Image Mappings

**ResourceType → Tile Image:**
```csharp
ResourceType.Brick    → "../Assets/Tiles/brick.png"
ResourceType.Wheat    → "../Assets/Tiles/wheat.png"
ResourceType.Wood     → "../Assets/Tiles/wood.png"
ResourceType.Ore      → "../Assets/Tiles/ore.png"
ResourceType.Sheep    → "../Assets/Tiles/sheep.png"
ResourceType.Desert   → "../Assets/Tiles/desert.png"
ResourceType.GoldMine → "../Assets/Tiles/goldMine.png"
ResourceType.Sea      → "../Assets/Tiles/back.jpg"
```

**HarborType → Harbor Image:**
```csharp
HarborType.Brick       → "../Assets/Harbors/2 for 1 brick.png"
HarborType.Ore         → "../Assets/Harbors/2 for 1 ore.png"
HarborType.Sheep       → "../Assets/Harbors/2 for 1 sheep.png"
HarborType.Wheat       → "../Assets/Harbors/2 for 1 wheat.png"
HarborType.Wood        → "../Assets/Harbors/2 for 1 wood.png"
HarborType.ThreeForOne → "../Assets/Harbors/3 for 1.png"
```

**ResourceCard → Card Image:**
```csharp
// Base resources
ResourceCard.Brick    → "../Assets/ResourceCards/brick.png"
ResourceCard.Wheat    → "../Assets/ResourceCards/wheat.png"
ResourceCard.Wood     → "../Assets/ResourceCards/wood.png"
ResourceCard.Ore      → "../Assets/ResourceCards/ore.png"
ResourceCard.Sheep    → "../Assets/ResourceCards/sheep.png"
ResourceCard.GoldMine → "../Assets/ResourceCards/goldMine.png"

// Expansion commodities
ResourceCard.Cloth    → "../Assets/ResourceCards/cloth.png"
ResourceCard.Paper    → "../Assets/ResourceCards/paper.png"
ResourceCard.Coin     → "../Assets/ResourceCards/coin.png"

// Development cards
ResourceCard.Trade        → "../Assets/ResourceCards/trade.png"
ResourceCard.Politics     → "../Assets/ResourceCards/politics.png"
ResourceCard.Science      → "../Assets/ResourceCards/science.png"
ResourceCard.VictoryPoint → "../Assets/ResourceCards/victorypoint.png"

// Special
ResourceCard.Back    → "../Assets/ResourceCards/back.png"
ResourceCard.Robber  → "../Assets/ResourceCards/robber.png"
```

## WebUI Asset Strategy

### Design Goals

1. **Reuse hi-res images** - Same visual quality as Desktop app
2. **Efficient caching** - Download once, reuse everywhere
3. **Fast initial load** - Progressive loading for large assets
4. **SVG pattern rendering** - Define patterns once, reference multiple times

### Architecture

**Note:** This section describes the legacy layout before the theme system migration. The current architecture uses the theme system described above with all assets under `/themes/base/`.

```
WebUI/
├── wwwroot/
│   ├── themes/
│   │   └── base/            # All themed assets organized by category
│   │       ├── tiles/       # Tile texture images (brick.png, wheat.png, etc.)
│   │       ├── harbors/     # Harbor images (brick.png, ore.png, etc.)
│   │       ├── resources/   # Resource card images
│   │       ├── stats/       # Player stat icons (SVGs)
│   │       ├── buildings/   # Building SVGs (settlement.svg, city.svg)
│   │       ├── backgrounds/ # Background textures (water.jpg, maple.jpg)
│   │       ├── fonts/       # Custom fonts (catan.ttf)
│   │       └── theme.json   # Theme asset mappings
│   └── css/
│       └── app.css          # @font-face for Catan font
```

### Custom Font in WebUI

Register the Catan font in CSS:

```css
@font-face {
    font-family: 'Catan';
    src: url('/themes/base/fonts/catan.ttf') format('truetype');
    font-weight: normal;
    font-style: normal;
}
```

**Usage in SVG:**
```xml
<text font-family="Catan" font-size="24">8</text>
```

**Usage for player stats, cities, settlements:**
- SVG text elements with `font-family="Catan"`
- CSS classes for HTML elements: `.catan-font { font-family: 'Catan', sans-serif; }`

### Static File Caching

Configure aggressive caching for immutable assets in `Program.cs`:

```csharp
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Cache images for 1 year (immutable content)
        if (ctx.File.Name.EndsWith(".png") || ctx.File.Name.EndsWith(".jpg"))
        {
            ctx.Context.Response.Headers.Append(
                "Cache-Control", "public, max-age=31536000, immutable");
        }
    }
});
```

**Cache behavior:**
- First visit: Browser downloads all tile images (~10MB)
- Subsequent visits: Browser uses cached versions (0 network requests)
- Cache invalidation: Change filename or add version query string

### SVG Pattern Rendering

The BoardSvgGenerator creates SVG with `<defs>` containing pattern definitions for each tile type. Each pattern is defined once and referenced by all matching hexes.

**Pattern Definition:**
```xml
<svg>
  <defs>
    <pattern id="tile-brick" patternUnits="objectBoundingBox" width="1" height="1">
      <image href="/themes/base/tiles/brick.png"
             preserveAspectRatio="xMidYMid slice"
             width="100" height="87"/>
    </pattern>
    <pattern id="tile-wheat" ...>
      <image href="/themes/base/tiles/wheat.png" .../>
    </pattern>
    <!-- ... other tile patterns -->
  </defs>

  <!-- Each hex references a pattern by ID -->
  <path d="M..." fill="url(#tile-brick)"/>
  <path d="M..." fill="url(#tile-wheat)"/>
</svg>
```

**Benefits:**
- Pattern defined once in `<defs>`, used N times
- Browser caches image files independently
- Same image data not duplicated in SVG
- Clean separation of tile texture from hex geometry

### Image Sizing for SVG Patterns

For flat-top hexes with size 50px (radius):
- Hex width: 100px (2 × size)
- Hex height: ~87px (√3 × size)

Pattern image dimensions should match or exceed hex dimensions for crisp rendering:
- Minimum: 100×87px
- Recommended: 200×174px (2x for retina displays)
- Current hi-res: Much larger (will be downscaled by browser)

### Resource Type to Pattern ID Mapping

**Tile Patterns:**
```csharp
private string GetTilePatternId(ResourceType resourceType)
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
```

**Harbor Patterns:**
```csharp
private string GetHarborPatternId(HarborType harborType)
{
    return harborType switch
    {
        HarborType.Brick => "harbor-brick",
        HarborType.Ore => "harbor-ore",
        HarborType.Sheep => "harbor-sheep",
        HarborType.Wheat => "harbor-wheat",
        HarborType.Wood => "harbor-wood",
        HarborType.ThreeForOne => "harbor-3for1",
        _ => "harbor-default"
    };
}
```

### Harbor Rendering

Harbors are positioned at tile vertices with rotation based on direction:

```xml
<defs>
  <pattern id="harbor-brick" patternUnits="objectBoundingBox" width="1" height="1">
    <image href="/themes/base/harbors/brick.png" width="40" height="60"/>
  </pattern>
</defs>

<!-- Harbor positioned at vertex with rotation -->
<g transform="translate(x, y) rotate(angle)">
  <rect width="40" height="60" fill="url(#harbor-brick)"/>
</g>
```

**Harbor positioning:**
- Calculate vertex position from adjacent tile coordinates
- Rotation angle based on harbor direction (0°, 60°, 120°, etc.)
- Size: 40×60px (scaled from Desktop's 67×100px)

## Implementation Plan

### Phase 1: Copy Assets to WebUI (COMPLETED)

Assets have been migrated to the theme system under `WebUI/wwwroot/themes/base/`:

1. ✅ Created directory structure under `themes/base/`:
   - `themes/base/tiles/` - Hex tile textures
   - `themes/base/harbors/` - Harbor images
   - `themes/base/resources/` - Resource card images
   - `themes/base/stats/` - Player stat icons (SVG)
   - `themes/base/buildings/` - Building SVGs
   - `themes/base/backgrounds/` - Background textures
   - `themes/base/fonts/` - Custom fonts
2. ✅ Copied and organized all assets from Desktop app
3. ✅ Created `theme.json` with asset mappings
4. ✅ Removed legacy `/images/` and `/fonts/` directories

### Phase 2: Update BoardSvgGenerator

1. Add tile pattern definitions to `<defs>` section
2. Map ResourceType enum to pattern IDs
3. Use `fill="url(#pattern-id)"` instead of solid colors
4. Calculate proper pattern dimensions for hex size

### Phase 2b: Add Harbor Rendering

1. Add harbor pattern definitions to `<defs>`
2. Map HarborType enum to harbor pattern IDs
3. Calculate harbor positions at tile vertices
4. Apply rotation based on harbor direction
5. Render harbors as positioned/rotated rectangles with pattern fill

### Phase 3: Configure Caching

1. Add static file middleware with cache headers
2. Set 1-year cache for image files
3. Add `immutable` directive for browser optimization

### Phase 4: Progressive Enhancement

1. Start with colored fills (current implementation)
2. Add pattern definitions for textured tiles
3. Add harbor rendering with images
4. Add resource card display

## Performance Considerations

### Initial Load

| Asset Category | Size | Strategy |
|----------------|------|----------|
| Tile images | ~10MB | Lazy load after critical path |
| Harbor images | ~370KB | Load with tiles |
| Card images | ~97MB | Load on demand when needed |

### Network Optimization

1. **HTTP/2 multiplexing**: Multiple images download in parallel
2. **Browser caching**: Images cached after first download
3. **SVG streaming**: SVG renders progressively as images load

### Memory Usage

- SVG patterns reference external images (not embedded)
- Browser manages image memory independently
- Patterns share same image data across all instances

## Comparison: Desktop vs WebUI

| Aspect | Desktop (XAML) | WebUI (SVG) |
|--------|----------------|-------------|
| Image storage | Embedded resources | Static files in wwwroot |
| Image reference | `{StaticResource key}` | `url(#pattern-id)` |
| Caching | In-process memory | Browser HTTP cache |
| Reuse mechanism | ResourceDictionary | SVG `<defs>` patterns |
| Stretch mode | `UniformToFill` | `preserveAspectRatio="xMidYMid slice"` |

## Future Enhancements

### Image Optimization

Consider creating optimized versions for web:
- WebP format (30% smaller than PNG)
- Multiple resolutions for different device densities
- Compressed versions for slower connections

### CDN Deployment

For production, consider serving images from CDN:
- Global edge caching
- Automatic format negotiation (WebP, AVIF)
- Bandwidth cost reduction

### Sprite Sheet Alternative

For maximum efficiency, could combine all tiles into single sprite sheet:
- One HTTP request for all tiles
- Use SVG `viewBox` to select tile region
- Trade-off: More complex pattern definitions

## File Reference Summary

### Desktop App Files

- `DesktopApp/Themes/ImageResources.xaml` - Image brush definitions
- `DesktopApp/Assets/Tiles/*.png` - Tile texture images
- `DesktopApp/Assets/Harbors/*.png` - Harbor images
- `DesktopApp/Assets/ResourceCards/*.png` - Card images

### WebUI Files

- `WebUI/wwwroot/themes/base/` - All themed assets (tiles, harbors, resources, stats, buildings, backgrounds, fonts)
- `WebUI/wwwroot/themes/base/theme.json` - Asset mappings for base theme
- `WebUI/Services/ClientAssetService.cs` - Theme-aware asset path resolution
- `WebUI/Services/Rendering/BoardSvgGenerator.cs` - SVG pattern generation using IAssetService

## References

- [MDN: SVG Pattern Element](https://developer.mozilla.org/en-US/docs/Web/SVG/Element/pattern)
- [Static Files in ASP.NET Core](https://docs.microsoft.com/aspnet/core/fundamentals/static-files)
- [HTTP Caching](https://developer.mozilla.org/en-US/docs/Web/HTTP/Caching)
- [WebUI-Design.md](./WebUI-Design.md) - Overall WebUI architecture

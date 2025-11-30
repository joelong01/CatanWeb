using System.Net.Http.Json;
using System.Text.Json;
using Catan3.WebUI.Models;

namespace Catan3.WebUI.Services
{
    /// <summary>
    /// Client-side implementation of IAssetService for Blazor WebAssembly.
    /// Loads theme configurations via HTTP from the server.
    /// </summary>
    public class ClientAssetService : IAssetService
    {
        private readonly HttpClient _httpClient;
        private Dictionary<AssetName, string> _baseAssets = new();
        private Dictionary<string, Dictionary<AssetName, string>> _themeOverrides = new();
        private Dictionary<string, ThemeMetadata> _themeMetadata = new();
        private string _currentTheme = "classic";
        private bool _isInitialized = false;

        public string CurrentTheme => _currentTheme;
        public IReadOnlyList<string> AvailableThemes => _themeMetadata.Keys.Where(k => k != "base").ToList();
        public event Action<string>? ThemeChanged;

        public ClientAssetService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Initialize the asset service by loading theme configurations.
        /// Should be called early in app startup.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            try
            {
                // Load base theme
                await LoadThemeAsync("base");

                // Load classic theme
                await LoadThemeAsync("classic");

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize ClientAssetService: {ex.Message}");
                // Continue with empty theme data - will use fallback paths
            }
        }

        private async Task LoadThemeAsync(string themeName)
        {
            try
            {
                var jsonPath = $"/themes/{themeName}/theme.json";
                var definition = await _httpClient.GetFromJsonAsync<ThemeDefinition>(jsonPath,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (definition == null) return;

                // Store metadata
                _themeMetadata[themeName] = new ThemeMetadata
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
                    _baseAssets = assets;
                }
                else
                {
                    _themeOverrides[themeName] = assets;
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Failed to load theme {themeName}: {ex.Message}");
            }
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
            if (_baseAssets.TryGetValue(asset, out var basePath))
            {
                return basePath;
            }

            // If still not found, return legacy path for backwards compatibility
            return GetLegacyFallbackPath(asset);
        }

        /// <summary>
        /// Returns legacy image paths for backwards compatibility when theme is not loaded.
        /// </summary>
        private static string GetLegacyFallbackPath(AssetName asset)
        {
            return asset switch
            {
                AssetName.TileBrick => "/images/tiles/brick.png",
                AssetName.TileWheat => "/images/tiles/wheat.png",
                AssetName.TileWood => "/images/tiles/wood.png",
                AssetName.TileOre => "/images/tiles/ore.png",
                AssetName.TileSheep => "/images/tiles/sheep.png",
                AssetName.TileDesert => "/images/tiles/desert.png",
                AssetName.TileGoldMine => "/images/tiles/goldMine.png",
                AssetName.TileSea => "/images/tiles/back.jpg",
                AssetName.TileInvasion => "/images/tiles/invasion.png",

                AssetName.HarborBrick => "/images/harbors/2 for 1 brick.png",
                AssetName.HarborOre => "/images/harbors/2 for 1 ore.png",
                AssetName.HarborSheep => "/images/harbors/2 for 1 sheep.png",
                AssetName.HarborWheat => "/images/harbors/2 for 1 wheat.png",
                AssetName.HarborWood => "/images/harbors/2 for 1 wood.png",
                AssetName.HarborThreeForOne => "/images/harbors/3 for 1.png",

                AssetName.BackgroundWater => "/images/tiles/back.jpg",
                AssetName.BackgroundBorderFill => "/images/maple.jpg",
                AssetName.BackgroundBorderStroke => "/images/cherry.jpg",

                _ => $"/images/missing-{asset}.png"
            };
        }

        public ThemeMetadata GetThemeMetadata(string themeName)
        {
            var normalized = themeName.ToLowerInvariant();
            if (_themeMetadata.TryGetValue(normalized, out var metadata))
            {
                return metadata;
            }

            // Return a default metadata if theme not found
            return new ThemeMetadata
            {
                Name = themeName,
                DisplayName = themeName,
                Description = null,
                Preview = "/images/tiles/wheat.png"
            };
        }

        public ThemeMetadata GetCurrentThemeMetadata() => GetThemeMetadata(_currentTheme);

        public string GetMimeType(AssetName asset)
        {
            var path = GetAssetPath(asset);
            return path switch
            {
                _ when path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) => "image/svg+xml",
                _ when path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) => "image/png",
                _ when path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) => "image/jpeg",
                _ when path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) => "image/jpeg",
                _ when path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) => "image/webp",
                _ when path.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) => "font/ttf",
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
    }
}

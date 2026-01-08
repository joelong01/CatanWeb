using Catan3.Shared.Models;
using Microsoft.JSInterop;

namespace Catan3.WebUI.Services;

/// <summary>
/// Client-side service that holds HouseRules in memory and syncs with localStorage.
/// This is the single source of truth for client-side house rules settings.
/// Each client can have their own settings independent of the GameModel.
/// </summary>
public class ClientHouseRulesService
{
    private readonly IJSRuntime _jsRuntime;
    private HouseRules _houseRules = new();
    private bool _initialized;

    private const string LocalStorageKey = "HouseRules";

    public ClientHouseRulesService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// The current house rules settings.
    /// </summary>
    public HouseRules HouseRules => _houseRules;

    /// <summary>
    /// Event raised when house rules change.
    /// </summary>
    public event Action? HouseRulesChanged;

    /// <summary>
    /// Initializes the service by loading settings from localStorage.
    /// Safe to call multiple times - only loads once.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", LocalStorageKey);
            if (!string.IsNullOrEmpty(json))
            {
                var loaded = System.Text.Json.JsonSerializer.Deserialize<HouseRules>(json);
                if (loaded != null)
                {
                    _houseRules = loaded;
                }
            }
            else
            {
                // No saved settings - use defaults with GriefDodgy OFF
                _houseRules = new HouseRules { GriefDodgy = false };
            }
        }
        catch
        {
            // On error, use defaults with GriefDodgy OFF
            _houseRules = new HouseRules { GriefDodgy = false };
        }

        _initialized = true;
    }

    /// <summary>
    /// Updates the house rules and persists to localStorage.
    /// </summary>
    public async Task UpdateAsync(HouseRules houseRules)
    {
        _houseRules = houseRules;
        await SaveAsync();
        HouseRulesChanged?.Invoke();
    }

    /// <summary>
    /// Updates a single property and persists to localStorage.
    /// </summary>
    public async Task UpdateAsync(Action<HouseRules> update)
    {
        update(_houseRules);
        await SaveAsync();
        HouseRulesChanged?.Invoke();
    }

    private async Task SaveAsync()
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(_houseRules);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", LocalStorageKey, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
}

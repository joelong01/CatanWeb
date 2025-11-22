using System.Net.Http.Json;

namespace Catan3.WebUI.Services;

/// <summary>
/// REST client proxy for game commands
/// </summary>
public class GameCommandProxy
{
    private readonly HttpClient _httpClient;
    private readonly GameServiceConfig _config;

    public GameCommandProxy(HttpClient httpClient, GameServiceConfig config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    /// <summary>
    /// Shuffle the board tiles
    /// </summary>
    public async Task<CommandResult> ShuffleAsync(string gameId, string playerId)
    {
        var request = new CommandRequest { PlayerId = playerId };
        return await PostCommandAsync($"/api/game/{gameId}/shuffle", request);
    }

    /// <summary>
    /// Advance to next game state
    /// </summary>
    public async Task<CommandResult> NextAsync(string gameId, string playerId)
    {
        var request = new CommandRequest { PlayerId = playerId };
        return await PostCommandAsync($"/api/game/{gameId}/next", request);
    }

    /// <summary>
    /// Load a game from database into memory
    /// </summary>
    public async Task<CommandResult> LoadGameAsync(string gameId)
    {
        return await PostCommandAsync($"/api/game/{gameId}/load", new CommandRequest());
    }

    private async Task<CommandResult> PostCommandAsync(string endpoint, CommandRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_config.BaseUrl}{endpoint}", request);

            var result = await response.Content.ReadFromJsonAsync<CommandResponse>();

            if (result == null)
            {
                return new CommandResult
                {
                    Success = false,
                    Error = "Invalid response from server",
                    StatusCode = (int)response.StatusCode
                };
            }

            return new CommandResult
            {
                Success = result.Success,
                Message = result.Message,
                Error = result.Error,
                ErrorCode = result.ErrorCode,
                GameId = result.GameId,
                StatusCode = (int)response.StatusCode
            };
        }
        catch (Exception ex)
        {
            return new CommandResult
            {
                Success = false,
                Error = $"Request failed: {ex.Message}",
                StatusCode = 0
            };
        }
    }
}

public class CommandRequest
{
    public string PlayerId { get; set; } = string.Empty;
}

public class CommandResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public string? ErrorCode { get; set; }
    public string? GameId { get; set; }
}

public class CommandResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public string? ErrorCode { get; set; }
    public string? GameId { get; set; }
    public int StatusCode { get; set; }
}

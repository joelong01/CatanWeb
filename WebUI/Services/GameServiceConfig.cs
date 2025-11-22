namespace Catan3.WebUI.Services;

/// <summary>
/// Configuration for GameService connection
/// </summary>
public class GameServiceConfig
{
    public string BaseUrl { get; set; } = "http://localhost:8080";

    public string HubUrl => $"{BaseUrl}/gameHub";
}

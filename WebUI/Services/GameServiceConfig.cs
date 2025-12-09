using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace Catan3.WebUI.Services;

/// <summary>
/// Configuration for GameService connection.
/// Dynamically determines the GameService URL based on the browser's hostname:
/// - Azure (*.azurewebsites.net): Uses {basename}-api.azurewebsites.net
/// - Local development: Uses same host with port 8080
/// </summary>
public class GameServiceConfig
{
    private readonly string _baseUrl;

    public GameServiceConfig(IWebAssemblyHostEnvironment hostEnvironment)
    {
        var baseUri = new Uri(hostEnvironment.BaseAddress);

        if (baseUri.Host.EndsWith(".azurewebsites.net"))
        {
            // Azure deployment: UI is at {basename}.azurewebsites.net
            // GameService is at {basename}-api.azurewebsites.net
            var hostParts = baseUri.Host.Split('.');
            var baseName = hostParts[0]; // e.g., "catan" from "catan.azurewebsites.net"
            _baseUrl = $"https://{baseName}-api.azurewebsites.net";
        }
        else
        {
            // Local development: GameService on same host, port 8080
            _baseUrl = $"http://{baseUri.Host}:8080";
        }
    }

    public string BaseUrl => _baseUrl;

    public string HubUrl => $"{BaseUrl}/gameHub";
}

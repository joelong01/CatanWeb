/// <summary>
/// Minimal ASP.NET Core server to host the Blazor WebAssembly application.
/// This serves the static files from the WebUI project and handles SPA routing.
/// Used for Azure App Service deployment where a server is required.
/// For local development, you can still use 'dotnet run --project WebUI' with DevServer.
/// </summary>
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// Serve static files from wwwroot (includes Blazor WASM files)
app.UseStaticFiles();

// Enable Blazor framework files
app.UseBlazorFrameworkFiles();

// SPA fallback - serve index.html for any unknown routes (client-side routing)
app.MapFallbackToFile("index.html");

app.Run();

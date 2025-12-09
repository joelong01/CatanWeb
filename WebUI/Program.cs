using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Blazored.LocalStorage;
using Catan3.WebUI;
using Catan3.WebUI.Models;
using Catan3.WebUI.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddSingleton(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register GameService configuration and connection service
builder.Services.AddSingleton(sp => new GameServiceConfig(builder.HostEnvironment));
builder.Services.AddSingleton<GameConnectionService>();

// Register GameStateService as singleton for thick client state management
builder.Services.AddSingleton<GameStateService>();

// Register theme-aware asset service for resolving asset paths
builder.Services.AddSingleton<ClientAssetService>();
builder.Services.AddSingleton<IAssetService>(sp => sp.GetRequiredService<ClientAssetService>());

var host = builder.Build();

// Initialize asset service (load theme.json files)
// Note: localStorage is set later via SetLocalStorage since ISyncLocalStorageService
// requires JS interop which may not be ready during startup
var assetService = host.Services.GetRequiredService<ClientAssetService>();
await assetService.InitializeAsync();

await host.RunAsync();

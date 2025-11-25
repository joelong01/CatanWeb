using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Catan3.WebUI;
using Catan3.WebUI.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register GameService configuration and proxy
builder.Services.AddSingleton<GameServiceConfig>();
builder.Services.AddScoped<GameCommandProxy>();

// Register GameStateService as singleton for thick client state management
builder.Services.AddSingleton<GameStateService>();

await builder.Build().RunAsync();

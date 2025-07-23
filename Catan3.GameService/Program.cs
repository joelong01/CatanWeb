using Catan3.GameService.Controllers;
using Catan3.GameService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add CORS for local development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080); // ? allows access from LAN
});

// Configure Discovery Service
builder.Services.Configure<DiscoveryServiceOptions>(options =>
{
    options.BroadcastPort = 8765;
    options.BroadcastInterval = 5000; // 5 seconds
    options.Enabled = true;
});

// Configure Game API options
builder.Services.Configure<GameApiOptions>(options =>
{
    // Default to 15 minutes for production
    options.HangingGetTimeout = TimeSpan.FromMinutes(15);
    
    // Override with shorter timeout for testing if in Development environment
    if (builder.Environment.IsDevelopment())
    {
        // You can also configure this via appsettings.json or environment variables
        var testTimeoutSeconds = builder.Configuration.GetValue<int?>("GameApi:HangingGetTimeoutSeconds");
        if (testTimeoutSeconds.HasValue)
        {
            options.HangingGetTimeout = TimeSpan.FromSeconds(testTimeoutSeconds.Value);
        }
    }
});

// Register services
builder.Services.AddSingleton<GameStateMachine>(provider => 
{
    // For now, create with null services - will be updated when we integrate fully
    return new GameStateMachine(null, "temp_save.json");
});

builder.Services.AddSingleton<IDiscoveryService, UdpDiscoveryService>();
builder.Services.AddHostedService(provider => (UdpDiscoveryService)provider.GetRequiredService<IDiscoveryService>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Enable CORS
app.UseCors("AllowLocalhost");

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

// Serve static files (including our companion.html)
app.UseStaticFiles();

// Map companion route to serve the HTML file
app.MapGet("/companion", async (HttpContext context) =>
{
    var filePath = Path.Combine(app.Environment.WebRootPath, "companion.html");
    if (File.Exists(filePath))
    {
        var content = await File.ReadAllTextAsync(filePath);
        context.Response.ContentType = "text/html";
        await context.Response.WriteAsync(content);
    }
    else
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync("Companion interface not found");
    }
});

app.MapStaticAssets();

// Map API controllers
app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Start discovery service and announce availability
var discoveryService = app.Services.GetRequiredService<IDiscoveryService>();

Console.WriteLine("=================================");
Console.WriteLine("?? Catan3 Game Service Starting");
Console.WriteLine("=================================");
Console.WriteLine();
Console.WriteLine("?? Network Discovery:");
Console.WriteLine("  • UDP Broadcast Port: 8765");
Console.WriteLine("  • Broadcasting every 5 seconds");
Console.WriteLine();
Console.WriteLine("?? Web API Endpoints:");
Console.WriteLine("  • Game Actions: /api/game/action");
Console.WriteLine("  • Game State: /api/gamestate/{gameId}");
Console.WriteLine("  • Players: /api/players/{gameId}");
Console.WriteLine("  • Real-time Updates: /api/gamestate/{gameId}/listen");
Console.WriteLine();
Console.WriteLine("?? Mobile Companion:");
Console.WriteLine("  • Interface URL: /companion");
Console.WriteLine("  • Mobile devices can connect via browser");
Console.WriteLine("  • Real-time updates via hanging GET");
Console.WriteLine();
Console.WriteLine("?? Service Status:");
Console.WriteLine("  • Game State Machine: Ready");
Console.WriteLine("  • REST API: Available");
Console.WriteLine("  • Web Companion: Available");
Console.WriteLine("  • UDP Discovery: Broadcasting");
Console.WriteLine();
Console.WriteLine("Ready for connections! ??");
Console.WriteLine("=================================");

// Update discovery with initial game info
discoveryService.UpdateGameInfo("default", "WaitingForNewGame", 0, "");

app.Run();

// Make the implicit Program class public so it can be referenced in tests
public partial class Program { }

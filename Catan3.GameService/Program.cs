using Catan3.GameService.Controllers;
using Catan3.GameService.Services;
using System.Net;
using System.Net.Sockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Set console encoding to UTF-8 to support emoji characters
Console.OutputEncoding = Encoding.UTF8;

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

// Configure to listen on all interfaces on port 8080
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080); // Allows access from LAN
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

// Register persistence service for save/load functionality
builder.Services.AddSingleton<IPersistanceService, GameServicePersistanceService>();

// Register client notification service for real-time updates
builder.Services.AddSingleton<IClientNotification, ClientNotificationService>();

// Register GameStateMachineService as Singleton to ensure shared state across all requests
// This ensures that games created in one request can be retrieved in subsequent requests
builder.Services.AddSingleton<GameStateMachineService>();

builder.Services.AddSingleton<IDiscoveryService, UdpDiscoveryService>();
builder.Services.AddHostedService(provider => (UdpDiscoveryService)provider.GetRequiredService<IDiscoveryService>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    // Only use HTTPS redirection in production when HTTPS is properly configured
    app.UseHttpsRedirection();
}
else
{
    // In development, we're using HTTP only on port 8080 for local network access
    // Skip HTTPS redirection to avoid the "Failed to determine https port" warning
    app.UseDeveloperExceptionPage();
}

// Enable CORS
app.UseCors("AllowLocalhost");

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
        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(content, Encoding.UTF8);
    }
    else
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync("Companion interface not found");
    }
});

// Map demo routes for UI preview
app.MapGet("/demo", async (HttpContext context) =>
{
    var filePath = Path.Combine(app.Environment.WebRootPath, "demo.html");
    if (File.Exists(filePath))
    {
        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(content, Encoding.UTF8);
    }
    else
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync("Demo interface not found");
    }
});

// Map companion with demo state parameter - Fix CSS loading by using absolute paths
app.MapGet("/companion/demo/{state}", async (HttpContext context) =>
{
    var state = context.Request.RouteValues["state"]?.ToString() ?? "";
    var filePath = Path.Combine(app.Environment.WebRootPath, "companion.html");
    if (File.Exists(filePath))
    {
        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        
        // Fix CSS and JS paths to be absolute
        content = content.Replace("href=\"companion.css\"", "href=\"/companion.css\"");
        content = content.Replace("src=\"companion.js\"", "src=\"/companion.js\"");
        
        // Add demo mode script injection
        var demoScript = $@"
    <script>
        window.DEMO_MODE = true;
        window.DEMO_STATE = '{state}';
    </script>
</head>";
        content = content.Replace("</head>", demoScript);
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(content, Encoding.UTF8);
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

// Get local IP address for network access
string GetLocalIPAddress()
{
    try
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
        socket.Connect("8.8.8.8", 65530);
        if (socket.LocalEndPoint is IPEndPoint endPoint)
        {
            return endPoint.Address.ToString();
        }
    }
    catch
    {
        // Fallback methods
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                {
                    return ip.ToString();
                }
            }
        }
        catch { }
    }
    
    return "localhost";
}

// Start discovery service and announce availability
var discoveryService = app.Services.GetRequiredService<IDiscoveryService>();

// Get local IP for display
var localIP = GetLocalIPAddress();
var port = 8080;

Console.WriteLine("=========================================");
Console.WriteLine("?? Catan3 Game Service Starting");
Console.WriteLine("=========================================");
Console.WriteLine();
Console.WriteLine("?? MOBILE COMPANION URLS:");
Console.WriteLine($"  • Local:   http://localhost:{port}/companion");
Console.WriteLine($"  • Network: http://{localIP}:{port}/companion");
Console.WriteLine($"  • Game List: http://localhost:{port}/companion (shows available games)");
Console.WriteLine($"  • Direct Game: http://localhost:{port}/companion?gameId={{gameId}}");
Console.WriteLine();
Console.WriteLine("?? UI DEMO/PREVIEW URLS:");
Console.WriteLine($"  • Demo Hub:       http://localhost:{port}/demo");
Console.WriteLine($"  • PickingBoard:   http://localhost:{port}/companion/demo/PickingBoard");
Console.WriteLine($"  • Allocation:     http://localhost:{port}/companion/demo/AllocateResourceForward");
Console.WriteLine($"  • Supplemental:   http://localhost:{port}/companion/demo/PickSupplementalPlayers");
Console.WriteLine($"  • Roll Dice:      http://localhost:{port}/companion/demo/WaitingForRoll");
Console.WriteLine($"  • Purchase:       http://localhost:{port}/companion/demo/WaitingForNext");
Console.WriteLine();
Console.WriteLine("?? API BASE URLS:");
Console.WriteLine($"  • Local:   http://localhost:{port}");
Console.WriteLine($"  • Network: http://{localIP}:{port}");
Console.WriteLine();
Console.WriteLine("?? API Endpoints:");
Console.WriteLine("  • Game Actions: /api/game/action");
Console.WriteLine("  • Game State: /api/gamestate/{gameId}");
Console.WriteLine("  • Real-time Updates: /api/gamestate/{gameId}/listen");
Console.WriteLine("  • Create Game: /api/game/new");
Console.WriteLine("  • Load Game: /api/game/load");
Console.WriteLine("  • Save Game: /api/game/persist");
Console.WriteLine("  • Available Games: /api/companion/games");
Console.WriteLine();
Console.WriteLine("?? Network Discovery:");
Console.WriteLine("  • UDP Broadcast Port: 8765");
Console.WriteLine("  • Broadcasting every 5 seconds");
Console.WriteLine("  • Auto-discovery for mobile devices");
Console.WriteLine();
if (app.Environment.IsDevelopment())
{
    Console.WriteLine("?? Development Mode:");
    Console.WriteLine("  • HTTP only (no HTTPS)");
    Console.WriteLine("  • CORS enabled for all origins");
    Console.WriteLine("  • Hot reload enabled");
    Console.WriteLine();
}
Console.WriteLine("? Service Status:");
Console.WriteLine("  • Game State Machine: Ready");
Console.WriteLine("  • REST API: Available");
Console.WriteLine("  • Web Companion: Available");
Console.WriteLine("  • UDP Discovery: Broadcasting");
Console.WriteLine();
Console.WriteLine("?? To connect from mobile device:");
Console.WriteLine($"   1. Ensure phone is on same WiFi network");
Console.WriteLine($"   2. Open browser and go to: http://{localIP}:{port}/companion");
Console.WriteLine($"   3. Select your player from the dropdown");
Console.WriteLine($"   4. Start playing!");
Console.WriteLine();
Console.WriteLine("?? To preview UI states:");
Console.WriteLine($"   • Open browser: http://localhost:{port}/demo");
Console.WriteLine($"   • Click on any state to see the UI");
Console.WriteLine();
Console.WriteLine("? Ready for connections! ?");
Console.WriteLine("=========================================");

// Update discovery with initial game info
discoveryService.UpdateGameInfo("default", "WaitingForNewGame", 0, "");

app.Run($"http://0.0.0.0:{port}");

// Make the implicit Program class public so it can be referenced in tests
public partial class Program { }

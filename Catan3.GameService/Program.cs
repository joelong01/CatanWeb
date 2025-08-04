using Catan3.GameService.Controllers;
using Catan3.GameService.Services;
using Catan3.GameService.Hubs;
using Catan3.Shared.Utility;
using System.Net;
using System.Net.Sockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Set console encoding to UTF-8 to support emoji characters
Console.OutputEncoding = Encoding.UTF8;

// Configure logging based on environment
if (builder.Environment.EnvironmentName == "Testing")
{
    // Suppress all logging during tests
    builder.Logging.ClearProviders();
    builder.Logging.SetMinimumLevel(LogLevel.Error);
}

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        JsonHelper.ConfigureOptions(options.JsonSerializerOptions);
    });

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

// Add SignalR for real-time communication
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        JsonHelper.ConfigureOptions(options.PayloadSerializerOptions);
    });

// Configure to listen on all interfaces on port 8080
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080); // Allows access from LAN
});

// Configure Game API options - simplified since we no longer use hanging GET
builder.Services.Configure<GameApiOptions>(options =>
{
    // No hanging GET timeout needed - SignalR handles all real-time updates
});

// Register persistence service for save/load functionality
builder.Services.AddSingleton<IPersistanceService, GameServicePersistanceService>();

// Register SignalR-based client notification service for real-time updates
builder.Services.AddSingleton<SignalRNotificationService>();
builder.Services.AddSingleton<IClientNotification>(provider => provider.GetRequiredService<SignalRNotificationService>());

// Register async command processor for fire-and-forget command execution
builder.Services.AddSingleton<AsyncCommandProcessor>();

// Register GameStateMachineService as Singleton to ensure shared state across all requests
// This ensures that games created in one request can be retrieved in subsequent requests
builder.Services.AddSingleton<GameStateMachineService>();

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
}

app.UseStaticFiles();

app.UseCors("AllowLocalhost");

app.UseRouting();

app.UseAuthorization();

// Companion interface serving
app.MapGet("/companion", async (HttpContext context, string? gameId = null) =>
{
    var filePath = Path.Combine(app.Environment.WebRootPath, "companion.html");
    if (File.Exists(filePath))
    {
        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        
        // Fix CSS and JS paths to be absolute
        content = content.Replace("href=\"companion.css\"", "href=\"/companion.css\"");
        content = content.Replace("src=\"companion.js\"", "src=\"/companion.js\"");
        
        // Inject gameId if provided
        if (!string.IsNullOrEmpty(gameId))
        {
            var gameIdScript = $@"
    <script>
        window.INITIAL_GAME_ID = '{gameId}';
    </script>
</head>";
            content = content.Replace("</head>", gameIdScript);
        }
        
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(content, Encoding.UTF8);
    }
    else
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync("Companion interface not found");
    }
});

// Demo interface serving for different game states
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

// Map SignalR GameHub
app.MapHub<GameHub>("/gameHub");

// Map API controllers
app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Suppress startup banner during tests
if (builder.Environment.EnvironmentName != "Testing")
{
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

    // Get local IP for display
    var localIP = GetLocalIPAddress();
    var port = 8080;

    Console.WriteLine("=========================================");
    Console.WriteLine("?? Catan3 Game Service Starting - Pure SignalR Architecture");
    Console.WriteLine("=========================================");
    Console.WriteLine();
    Console.WriteLine("?? MOBILE COMPANION URLS:");
    Console.WriteLine($"  ? Local:   http://localhost:{port}/companion");
    Console.WriteLine($"  ? Network: http://{localIP}:{port}/companion");
    Console.WriteLine($"  ? Game Discovery: http://localhost:{port}/api/companion/games");
    Console.WriteLine($"  ? Direct Game: http://localhost:{port}/companion?gameId={{gameId}}");
    Console.WriteLine();
    Console.WriteLine("?? UI DEMO/PREVIEW URLS:");
    Console.WriteLine($"  ? Demo Hub:       http://localhost:{port}/demo");
    Console.WriteLine($"  ? Demo WaitingForRoll: http://localhost:{port}/companion/demo/WaitingForRoll");
    Console.WriteLine($"  ? Demo WaitingForNext: http://localhost:{port}/companion/demo/WaitingForNext");
    Console.WriteLine($"  ? Demo PickingBoard:   http://localhost:{port}/companion/demo/PickingBoard");
    Console.WriteLine();
    Console.WriteLine("? PURE SIGNALR ARCHITECTURE:");
    Console.WriteLine($"  ? SignalR Hub: ws://localhost:{port}/gameHub");
    Console.WriteLine($"  ? Real-time MVVM messages - same as Desktop app");
    Console.WriteLine($"  ? Instant bi-directional communication");
    Console.WriteLine($"  ? Browser-based game discovery (no UDP)");
    Console.WriteLine();
    Console.WriteLine("?? GAME MANAGEMENT:");
    Console.WriteLine($"  ? New Game API: POST http://localhost:{port}/api/game/new");
    Console.WriteLine($"  ? Game List API: GET http://localhost:{port}/api/companion/games");
    Console.WriteLine($"  ? All Game Actions: SignalR Messages via /gameHub");
    Console.WriteLine();
    Console.WriteLine("? Service ready! Use Ctrl+C to stop.");
    Console.WriteLine("=========================================");
}

app.Run();

// Make the implicit Program class public so it can be referenced in tests
public partial class Program { }

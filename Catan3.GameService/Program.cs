using Catan3.GameService.Controllers;
using Catan3.GameService.Services;
using Catan3.GameService.Data;
using Catan3.Shared.Interfaces;
using Catan3.GameService.Hubs;
using Catan3.Shared.Utility;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Sockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Set console encoding to UTF-8 to support emoji characters
Console.OutputEncoding = Encoding.UTF8;

// Configure logging based on environment
// Allow verbose logging when explicitly requested via environment variable or command line
var suppressTestLogging = builder.Environment.EnvironmentName == "Testing"
    && Environment.GetEnvironmentVariable("CATAN_TEST_VERBOSE") != "true"
    && !args.Contains("--verbose");

if (suppressTestLogging)
{
    // Suppress all logging during tests (unless verbose mode requested)
    builder.Logging.ClearProviders();
    builder.Logging.SetMinimumLevel(LogLevel.Error);
}

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        JsonHelper.ConfigureOptions(options.JsonSerializerOptions);
    });

// Note: Previously had complex model validation configuration, but now we handle
// complex GameModel objects as JSON strings to avoid ASP.NET validation limits

// Add CORS for WebUI access (local and network)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebUI", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add SignalR for real-time communication
builder.Services.AddSignalR(options =>
    {
        // Increase message size limit for large GameModel objects (default is 32KB)
        options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
    })
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

// Register persistence services
builder.Services.AddScoped<IGamePersistence, GamePersistenceService>();
builder.Services.AddSingleton<IPersistenceService, NullPersistenceService>();

// Register SignalR-based client notification service for real-time updates
builder.Services.AddSingleton<SignalRNotificationService>();
builder.Services.AddSingleton<IClientNotification>(provider => provider.GetRequiredService<SignalRNotificationService>());

// Register async command processor for fire-and-forget command execution
builder.Services.AddSingleton<AsyncCommandProcessor>();

// Database provider detection (zero-config: SQLite locally, SQL Server on Azure)
var dbDetector = new DatabaseProviderDetector(builder.Configuration);
builder.Services.AddSingleton(dbDetector);

// Register DbContext with appropriate provider
builder.Services.AddDbContext<CatanDbContext>((serviceProvider, options) =>
{
    var detector = serviceProvider.GetRequiredService<DatabaseProviderDetector>();

    if (detector.UseSqlServer)
    {
        options.UseSqlServer(detector.ConnectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        });
    }
    else
    {
        options.UseSqlite(detector.ConnectionString);
    }
});

// Data directory for SQLite
var dataDir = dbDetector.DataDirectory;

var app = builder.Build();

// Ensure Data directory exists
Directory.CreateDirectory(dataDir);

// Find default data path using detector
var defaultDataPath = dbDetector.GetDefaultDataPath();

// Always auto-seed on startup if database is empty (idempotent operation)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<CatanDbContext>();
    var gamePersistence = scope.ServiceProvider.GetRequiredService<IGamePersistence>();
    await DatabaseSeeder.SeedAsync(context, defaultDataPath, gamePersistence, dbDetector.UseSqlServer);
}

// Handle --seed-database command (exit after seeding for explicit seed-only mode)
if (args.Contains("--seed-database"))
{
    return;
}

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

// Configure static files with caching for images
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Cache images for 7 days (they rarely change)
        if (ctx.File.Name.EndsWith(".png") ||
            ctx.File.Name.EndsWith(".jpg") ||
            ctx.File.Name.EndsWith(".jpeg") ||
            ctx.File.Name.EndsWith(".ttf") ||
            ctx.File.Name.EndsWith(".woff") ||
            ctx.File.Name.EndsWith(".woff2"))
        {
            ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=604800");
        }
    }
});

app.UseCors("AllowWebUI");

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

// Health check endpoint for service readiness
app.MapGet("/health", (DatabaseProviderDetector detector) => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    version = new
    {
        commit = Environment.GetEnvironmentVariable("DEPLOY_COMMIT") ?? "local",
        buildTime = Environment.GetEnvironmentVariable("DEPLOY_BUILD_TIME") ?? "unknown",
        environment = app.Environment.EnvironmentName
    },
    database = new
    {
        provider = detector.ProviderName,
        isAzure = detector.IsAzure
    }
}));

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
    var webUIPort = 5296;

    Console.WriteLine("=========================================");
    Console.WriteLine("🎲 Catan3 Game Service");
    Console.WriteLine("=========================================");
    Console.WriteLine();
    Console.WriteLine("🌐 OPEN IN BROWSER:");
    Console.WriteLine($"  → Local:   http://localhost:{webUIPort}");
    Console.WriteLine($"  → Network: http://{localIP}:{webUIPort}");
    Console.WriteLine();
    Console.WriteLine("✅ Service ready! Use Ctrl+C to stop.");
    Console.WriteLine("=========================================");
}

app.Run();

// Make the implicit Program class public so it can be referenced in tests
public partial class Program { }

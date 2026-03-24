using Catan3.GameService.Controllers;
using Catan3.GameService.Services;
using Catan3.GameService.Data;
using Catan3.Shared.Interfaces;
using Catan3.GameService.Hubs;
using Catan3.Shared.Utility;
using System.Net;
using System.Net.Sockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Startup logging - capture early to diagnose Azure crashes
Console.WriteLine($"[STARTUP] GameService starting at {DateTime.UtcNow:O}");
Console.WriteLine($"[STARTUP] Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine($"[STARTUP] ContentRootPath: {builder.Environment.ContentRootPath}");
Console.WriteLine($"[STARTUP] Args: {string.Join(", ", args)}");

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

// Add OpenAPI/Swagger for API documentation and client generation (NSwag)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
// DatabaseBackedPersistenceService wraps IGamePersistence for Log<T> compatibility
// This enables GameStateMachine.LogGameModel() -> Log.SaveAsync() -> database save
builder.Services.AddSingleton<IPersistenceService, DatabaseBackedPersistenceService>();
Console.WriteLine("[STARTUP] Using DatabaseBackedPersistenceService for game persistence");

// Register HttpClient for Azure Resource Graph queries
builder.Services.AddHttpClient();

// Register Azure SQL diagnostic service for connection troubleshooting
// AzureSqlDiagnosticService removed — CosmosDB doesn't need SQL-specific diagnostics

// Register SignalR-based client notification service for real-time updates
builder.Services.AddSingleton<SignalRNotificationService>();
builder.Services.AddSingleton<IClientNotification>(provider => provider.GetRequiredService<SignalRNotificationService>());

// Register async command processor for fire-and-forget command execution
builder.Services.AddSingleton<AsyncCommandProcessor>();

// Register recording service for test recording/replay
builder.Services.AddSingleton<RecordingService>();

// Register game template service for template CRUD operations
builder.Services.AddScoped<GameTemplateService>();

// Register background database seeding (runs after Kestrel starts listening,
// preventing Azure warmup probe timeouts on cold DB connections)
builder.Services.AddHostedService<DatabaseSeedingService>();

// Register CosmosDB as the database provider
Console.WriteLine("[STARTUP] Registering CosmosDB (ICatanDb)...");
builder.Services.AddSingleton<Microsoft.Azure.Cosmos.CosmosClient>(
    _ => Catan3.GameService.Abstractions.CosmosClientFactory.Create(builder.Configuration));
builder.Services.AddScoped<Catan3.GameService.Abstractions.ICatanDb>(sp =>
{
    var client = sp.GetRequiredService<Microsoft.Azure.Cosmos.CosmosClient>();
    return new Catan3.GameService.Abstractions.CosmosCatanDb(client, "catan");
});
Console.WriteLine("[STARTUP] CosmosDB registered");

Console.WriteLine("[STARTUP] Building application...");
var app = builder.Build();
Console.WriteLine("[STARTUP] Application built successfully");

// Default data path for seeding
var defaultDataPath = Path.Combine(AppContext.BaseDirectory, "Default Data");
Console.WriteLine($"[STARTUP] Default data path: {defaultDataPath}");

// Handle --check-database command (CosmosDB health check, JSON output, then exit)
if (args.Contains("--check-database"))
{
    var result = new Dictionary<string, object?>
    {
        ["healthy"] = false,
        ["provider"] = "CosmosDB",
        ["playerCount"] = 0,
        ["gameCount"] = 0,
    };

    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Catan3.GameService.Abstractions.ICatanDb>();
        await db.InitializeAsync();
        var players = await db.LoadPlayersAsync();
        var gameCount = await db.CountGamesAsync();
        result["playerCount"] = players.Count;
        result["gameCount"] = gameCount;
        result["healthy"] = true;
    }
    catch (Exception ex)
    {
        result["error"] = ex.Message;
    }

    Console.Write(System.Text.Json.JsonSerializer.Serialize(result));
    return;
}

// Handle --seed-database command (synchronous initialization, then exit)
if (args.Contains("--seed-database"))
{
    var seedLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");
    seedLogger.LogInformation("[STARTUP] --seed-database flag detected, initializing CosmosDB...");
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Catan3.GameService.Abstractions.ICatanDb>();
        await db.InitializeAsync();
        await DatabaseSeeder.UpsertSystemTemplatesAsync(db, seedLogger);
        seedLogger.LogInformation("[STARTUP] Database initialization completed, exiting");
    }
    catch (Exception ex)
    {
        seedLogger.LogError(ex, "[STARTUP] Database initialization failed");
    }
    return;
}

// Normal startup: database seeding runs in background via DatabaseSeedingService
// (registered as IHostedService above) so Kestrel starts listening immediately
Console.WriteLine("[STARTUP] Database seeding will run in background after server starts");

Console.WriteLine("[STARTUP] Configuring HTTP request pipeline...");
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

    // Enable Swagger UI for API exploration and NSwag type generation
    app.UseSwagger();
    app.UseSwaggerUI();
}

// CORS must come before static files so cross-origin fetches (e.g. React on :3000
// fetching streamdeck-latest.json from :8080) receive Access-Control-Allow-Origin.
app.UseCors("AllowWebUI");

// Configure static files with caching for images and custom MIME types
var contentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".streamDeckPlugin"] = "application/octet-stream";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypeProvider,
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
        // Force download for plugin files
        if (ctx.File.Name.EndsWith(".streamDeckPlugin"))
        {
            ctx.Context.Response.Headers.Append("Content-Disposition",
                $"attachment; filename=\"{ctx.File.Name}\"");
        }
    }
});

app.UseRouting();

app.UseAuthorization();

// MapStaticAssets requires a manifest file generated during build.
// Skip it when running under OpenAPI tools (NSwag, Swashbuckle CLI) since it's not needed for API discovery.
// These tools run the app to extract metadata but don't have the static assets manifest.
var isOpenApiGeneration = Environment.GetCommandLineArgs().Any(arg =>
    arg.Contains("NSwag", StringComparison.OrdinalIgnoreCase) ||
    arg.Contains("swagger", StringComparison.OrdinalIgnoreCase));
if (!isOpenApiGeneration)
{
    app.MapStaticAssets();
}

// Health check endpoint for service readiness
app.MapGet("/health", async (Catan3.GameService.Abstractions.ICatanDb db) =>
{
    // Basic health info
    var response = new Dictionary<string, object>
    {
        ["status"] = "healthy",
        ["timestamp"] = DateTime.UtcNow,
        ["version"] = new
        {
            commit = Environment.GetEnvironmentVariable("DEPLOY_COMMIT") ?? "local",
            buildTime = Environment.GetEnvironmentVariable("DEPLOY_BUILD_TIME") ?? "unknown",
            environment = app.Environment.EnvironmentName
        },
        ["database"] = new { provider = "CosmosDB" }
    };

    try
    {
        var players = await db.LoadPlayersAsync();
        var gameCount = await db.CountGamesAsync();
        response["databaseDiagnostics"] = new
        {
            connected = true,
            checkedAt = DateTime.UtcNow,
            playerCount = players.Count,
            gameCount,
        };
    }
    catch (Exception ex)
    {
        response["status"] = "degraded";
        response["databaseDiagnostics"] = new
        {
            connected = false,
            checkedAt = DateTime.UtcNow,
            error = ex.Message,
        };
    }

    return Results.Ok(response);
});

// Map SignalR GameHub
app.MapHub<GameHub>("/gameHub");

// Map API controllers
app.MapControllers();
Console.WriteLine("[STARTUP] Controllers mapped");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
Console.WriteLine("[STARTUP] Default route mapped");

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

Console.WriteLine("[STARTUP] Calling app.Run()...");
app.Run();
Console.WriteLine("[STARTUP] app.Run() exited");

// Make the implicit Program class public so it can be referenced in tests
public partial class Program { }

/// <summary>
/// Simple cache for health check results to avoid expensive Azure Resource Graph queries.
/// Results are cached for 10 minutes.
/// </summary>
internal static class HealthCheckCache
{
    private static object? _cachedResult;
    private static DateTime _lastCheck = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);
    private static readonly object _lock = new();

    public static bool ShouldRefresh()
    {
        lock (_lock)
        {
            return DateTime.UtcNow - _lastCheck > CacheDuration;
        }
    }

    public static bool HasCachedResult()
    {
        lock (_lock)
        {
            return _cachedResult != null;
        }
    }

    public static object? GetCachedResult()
    {
        lock (_lock)
        {
            return _cachedResult;
        }
    }

    public static void Update(object result)
    {
        lock (_lock)
        {
            _cachedResult = result;
            _lastCheck = DateTime.UtcNow;
        }
    }
}

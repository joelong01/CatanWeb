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
builder.Services.AddSingleton<AzureSqlDiagnosticService>();

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

// Database provider detection (zero-config: SQLite locally, SQL Server on Azure)
Console.WriteLine("[STARTUP] Creating DatabaseProviderDetector...");
var dbDetector = new DatabaseProviderDetector(builder.Configuration);
Console.WriteLine($"[STARTUP] Database provider: {dbDetector.ProviderName}, IsAzure: {dbDetector.IsAzure}");
Console.WriteLine($"[STARTUP] Connection string (masked): {MaskConnectionString(dbDetector.ConnectionString)}");
builder.Services.AddSingleton(dbDetector);

static string MaskConnectionString(string? cs)
{
    if (string.IsNullOrEmpty(cs)) return "(empty)";
    // Mask password in connection string
    return System.Text.RegularExpressions.Regex.Replace(cs, @"(Password|Pwd)=[^;]*", "$1=***", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
}

// Register DbContext with appropriate provider
// Use AddDbContextPool for SQL Server to enable DbContext pooling (reuses DbContext instances)
// This works alongside ADO.NET connection pooling for optimal performance
if (dbDetector.UseSqlServer)
{
    Console.WriteLine("[STARTUP] Registering DbContext with pooling (SQL Server)...");
    builder.Services.AddDbContextPool<CatanDbContext>(options =>
    {
        options.UseSqlServer(dbDetector.ConnectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        });
    }, poolSize: 32); // Match connection pool size
}
else
{
    Console.WriteLine("[STARTUP] Registering DbContext (SQLite)...");
    // SQLite doesn't benefit from DbContext pooling (file-based, no connection overhead)
    builder.Services.AddDbContext<CatanDbContext>(options =>
    {
        options.UseSqlite(dbDetector.ConnectionString);
    });
}
Console.WriteLine("[STARTUP] DbContext registered");

// Data directory for SQLite
var dataDir = dbDetector.DataDirectory;
Console.WriteLine($"[STARTUP] Data directory: {dataDir}");

Console.WriteLine("[STARTUP] Building application...");
var app = builder.Build();
Console.WriteLine("[STARTUP] Application built successfully");

// Ensure Data directory exists
Console.WriteLine($"[STARTUP] Creating data directory: {dataDir}");
try
{
    Directory.CreateDirectory(dataDir);
    Console.WriteLine("[STARTUP] Data directory created/verified");
}
catch (Exception ex)
{
    Console.WriteLine($"[STARTUP] WARNING: Failed to create data directory: {ex.Message}");
}

// Find default data path using detector
var defaultDataPath = dbDetector.GetDefaultDataPath();
Console.WriteLine($"[STARTUP] Default data path: {defaultDataPath}");

// Handle --check-database command (schema + data verification, JSON output, then exit)
if (args.Contains("--check-database"))
{
    var result = new Dictionary<string, object?>
    {
        ["healthy"] = false,
        ["databaseExists"] = false,
        ["schemaValid"] = false,
        ["hasPlayers"] = false,
        ["hasGames"] = false,
        ["hasTemplates"] = false,
        ["playerCount"] = 0,
        ["gameCount"] = 0,
        ["templateCount"] = 0,
        ["missingTables"] = Array.Empty<string>(),
        ["extraTables"] = Array.Empty<string>(),
        ["action"] = "install"
    };

    try
    {
        // Check if database file exists (SQLite only)
        if (!dbDetector.UseSqlServer)
        {
            var dbPath = dbDetector.ConnectionString
                .Replace("Data Source=", "", StringComparison.OrdinalIgnoreCase)
                .Trim();
            result["databaseExists"] = File.Exists(dbPath);
            if (!(bool)result["databaseExists"])
            {
                result["action"] = "create";
                Console.Write(System.Text.Json.JsonSerializer.Serialize(result));
                return;
            }
        }
        else
        {
            result["databaseExists"] = true; // SQL Server existence checked differently
        }

        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CatanDbContext>();

        // Get expected tables from EF Core model
        var expectedTables = context.Model.GetEntityTypes()
            .Select(e => e.GetTableName()!)
            .Where(t => t != null)
            .OrderBy(t => t)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Get actual tables from database
        var actualTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var conn = context.Database.GetDbConnection();
        await conn.OpenAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = dbDetector.UseSqlServer
                ? "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'"
                : "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name != '__EFMigrationsHistory'";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                actualTables.Add(reader.GetString(0));
            }
        }
        finally
        {
            await conn.CloseAsync();
        }

        var missing = expectedTables.Except(actualTables, StringComparer.OrdinalIgnoreCase).OrderBy(t => t).ToArray();
        var extra = actualTables.Except(expectedTables, StringComparer.OrdinalIgnoreCase).OrderBy(t => t).ToArray();

        result["missingTables"] = missing;
        result["extraTables"] = extra;
        result["schemaValid"] = missing.Length == 0;

        // Check data if schema is valid
        if (missing.Length == 0)
        {
            try
            {
                var playerCount = await context.Players.CountAsync();
                var gameCount = await context.GameSaveMetadata.CountAsync();
                var templateCount = await context.GameTemplates.CountAsync();

                result["playerCount"] = playerCount;
                result["gameCount"] = gameCount;
                result["templateCount"] = templateCount;
                result["hasPlayers"] = playerCount > 0;
                result["hasGames"] = gameCount > 0;
                result["hasTemplates"] = templateCount > 0;

                // Determine action needed
                if (playerCount == 0 || templateCount == 0)
                {
                    result["action"] = "install"; // needs seeding
                }
                else
                {
                    result["action"] = null; // everything looks good
                    result["healthy"] = true;
                }
            }
            catch
            {
                // Tables exist but queries fail -- schema mismatch
                result["schemaValid"] = false;
                result["action"] = "install";
            }
        }
        else
        {
            result["action"] = "install"; // missing tables
        }
    }
    catch (Exception ex)
    {
        result["action"] = "install";
        result["error"] = ex.Message;
    }

    Console.Write(System.Text.Json.JsonSerializer.Serialize(result));
    return;
}

// Handle --seed-database command (synchronous seeding, then exit)
if (args.Contains("--seed-database"))
{
    var seedLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");
    seedLogger.LogInformation("[STARTUP] --seed-database flag detected, seeding synchronously...");
    try
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CatanDbContext>();
        var gamePersistence = scope.ServiceProvider.GetRequiredService<IGamePersistence>();
        await DatabaseSeeder.SeedAsync(context, defaultDataPath, gamePersistence, dbDetector.UseSqlServer, seedLogger);
        seedLogger.LogInformation("[STARTUP] Database seeding completed, exiting");
    }
    catch (Exception ex)
    {
        seedLogger.LogError(ex, "[STARTUP] Database seeding failed");
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
// Always checks database connectivity, but caches expensive Resource Graph diagnostics for 10 minutes
app.MapGet("/health", async (
    DatabaseProviderDetector detector,
    AzureSqlDiagnosticService sqlDiagnostics,
    CatanDbContext dbContext,
    bool? checkDatabase) =>
{
    // Basic health info (always returned)
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
        ["database"] = new
        {
            provider = detector.ProviderName,
            isAzure = detector.IsAzure
        }
    };

    if (detector.IsAzure)
    {
        // Always try to connect to database (cheap operation)
        var canConnect = false;
        Exception? dbException = null;

        try
        {
            await dbContext.Database.CanConnectAsync();
            canConnect = true;
        }
        catch (Exception ex)
        {
            dbException = ex;
        }

        // Determine if we need full Resource Graph diagnostics
        var forceCheck = checkDatabase == true;
        var cachedResult = HealthCheckCache.GetCachedResult();
        var cachedWasConnected = cachedResult != null &&
            (cachedResult as dynamic)?.connected == true;

        // Run full diagnostics if:
        // 1. Forced via ?checkDatabase=true
        // 2. Cache expired
        // 3. Connection status changed (was connected, now failing - or vice versa)
        var statusChanged = cachedResult != null && canConnect != cachedWasConnected;
        var shouldRunFullDiagnostics = forceCheck || HealthCheckCache.ShouldRefresh() || statusChanged;

        if (canConnect)
        {
            // Database is accessible - run full troubleshoot to verify schema
            if (shouldRunFullDiagnostics || cachedResult == null)
            {
                try
                {
                    var troubleshootResult = await sqlDiagnostics.TroubleshootAsync();
                    var diagnosticResult = new
                    {
                        connected = troubleshootResult.ConnectionSuccessful,
                        checkedAt = DateTime.UtcNow,
                        status = troubleshootResult.SchemaMissing ? "schema-missing" : "Online",
                        schemaMissing = troubleshootResult.SchemaMissing,
                        checks = troubleshootResult.Checks,
                        issues = troubleshootResult.Issues,
                        cannotFix = troubleshootResult.CannotFix,
                        issue = troubleshootResult.SchemaMissing ? "Missing required database tables" : (string?)null,
                        recommendation = troubleshootResult.SchemaMissing
                            ? "Run './catan.ps1 azure database install' to create missing tables"
                            : (string?)null
                    };
                    HealthCheckCache.Update(diagnosticResult);
                    response["databaseDiagnostics"] = diagnosticResult;
                }
                catch (Exception ex)
                {
                    // Fallback if troubleshoot fails
                    var diagnosticResult = new
                    {
                        connected = true,
                        checkedAt = DateTime.UtcNow,
                        status = "Online",
                        issue = (string?)null,
                        recommendation = (string?)null,
                        troubleshootError = ex.Message
                    };
                    HealthCheckCache.Update(diagnosticResult);
                    response["databaseDiagnostics"] = diagnosticResult;
                }
            }
            else
            {
                // Use cached result
                response["databaseDiagnostics"] = cachedResult ?? new
                {
                    connected = true,
                    checkedAt = DateTime.UtcNow,
                    status = "Online",
                    issue = (string?)null,
                    recommendation = (string?)null
                };
            }
        }
        else
        {
            // Database connection failed
            response["status"] = "degraded";

            if (shouldRunFullDiagnostics)
            {
                try
                {
                    // Run expensive Resource Graph diagnostics
                    var diagnosis = await sqlDiagnostics.DiagnoseAsync(dbException);
                    var diagnosticResult = new
                    {
                        connected = false,
                        checkedAt = DateTime.UtcNow,
                        status = diagnosis.AzureStatus?.DatabaseStatus ?? "Unknown",
                        publicNetworkAccess = diagnosis.AzureStatus?.PublicNetworkAccess,
                        issue = diagnosis.Issue.ToString(),
                        recommendation = diagnosis.Recommendation,
                        error = diagnosis.ConnectionError
                    };
                    HealthCheckCache.Update(diagnosticResult);
                    response["databaseDiagnostics"] = diagnosticResult;
                }
                catch (Exception ex)
                {
                    response["databaseDiagnostics"] = new
                    {
                        connected = false,
                        checkedAt = DateTime.UtcNow,
                        error = $"Diagnostic check failed: {ex.Message}"
                    };
                }
            }
            else if (cachedResult != null)
            {
                // Use cached diagnostics but update connection status
                response["databaseDiagnostics"] = new
                {
                    connected = false,
                    checkedAt = DateTime.UtcNow,
                    cachedDiagnosticsFrom = (cachedResult as dynamic)?.checkedAt,
                    status = (cachedResult as dynamic)?.status ?? "Unknown",
                    publicNetworkAccess = (cachedResult as dynamic)?.publicNetworkAccess,
                    issue = (cachedResult as dynamic)?.issue,
                    recommendation = (cachedResult as dynamic)?.recommendation,
                    error = dbException?.Message
                };
                response["databaseDiagnosticsCached"] = true;
            }
            else
            {
                // No cache, can't run diagnostics - just report the error
                response["databaseDiagnostics"] = new
                {
                    connected = false,
                    checkedAt = DateTime.UtcNow,
                    error = dbException?.Message,
                    recommendation = "Run with ?checkDatabase=true for full diagnostics"
                };
            }
        }
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

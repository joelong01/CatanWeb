using Catan3.GameService.Data;

namespace Catan3.GameService.Services;

/// <summary>
/// Runs database seeding as a background service so Kestrel starts listening
/// immediately. This prevents Azure App Service warmup probe timeouts when
/// the database connection is slow (e.g., cold Azure SQL + Managed Identity).
/// </summary>
public class DatabaseSeedingService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly DatabaseProviderDetector _dbDetector;
    private readonly ILogger<DatabaseSeedingService> _logger;

    public DatabaseSeedingService(IServiceProvider services, DatabaseProviderDetector dbDetector, ILogger<DatabaseSeedingService> logger)
    {
        _services = services;
        _dbDetector = dbDetector;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield immediately so Kestrel can start listening before we do any work.
        // Without this, everything before the first 'await' runs synchronously and
        // blocks the host startup pipeline (service resolution, DB connections, etc.).
        await Task.Yield();

        var defaultDataPath = _dbDetector.GetDefaultDataPath();
        _logger.LogInformation("[SEEDER-BG] Starting background database seeding (defaultDataPath: {DefaultDataPath})", defaultDataPath);

        try
        {
            using var scope = _services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CatanDbContext>();
            var gamePersistence = scope.ServiceProvider.GetRequiredService<IGamePersistence>();

            await DatabaseSeeder.SeedAsync(context, defaultDataPath, gamePersistence, _dbDetector.UseSqlServer, _logger);
            _logger.LogInformation("[SEEDER-BG] Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SEEDER-BG] Database seeding failed. The service is running, but database operations may fail until connection is restored.");
        }
    }
}

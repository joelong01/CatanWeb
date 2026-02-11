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

    public DatabaseSeedingService(IServiceProvider services, DatabaseProviderDetector dbDetector)
    {
        _services = services;
        _dbDetector = dbDetector;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield immediately so Kestrel can start listening before we do any work.
        // Without this, everything before the first 'await' runs synchronously and
        // blocks the host startup pipeline (service resolution, DB connections, etc.).
        await Task.Yield();

        var defaultDataPath = _dbDetector.GetDefaultDataPath();
        Console.WriteLine($"[SEEDER-BG] Starting background database seeding (defaultDataPath: {defaultDataPath})");

        try
        {
            using var scope = _services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CatanDbContext>();
            var gamePersistence = scope.ServiceProvider.GetRequiredService<IGamePersistence>();

            await DatabaseSeeder.SeedAsync(context, defaultDataPath, gamePersistence, _dbDetector.UseSqlServer);
            Console.WriteLine("[SEEDER-BG] Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SEEDER-BG] WARNING: Database seeding failed: {ex.Message}");
            Console.WriteLine($"[SEEDER-BG] Exception type: {ex.GetType().FullName}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[SEEDER-BG] Inner exception: {ex.InnerException.Message}");
            }
            Console.WriteLine("[SEEDER-BG] The service is running, but database operations may fail until connection is restored.");
        }
    }
}

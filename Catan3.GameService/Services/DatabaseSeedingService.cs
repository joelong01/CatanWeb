using Catan3.GameService.Abstractions;
using Catan3.GameService.Data;

namespace Catan3.GameService.Services;

/// <summary>
/// Runs database initialization and seeding as a background service so Kestrel starts
/// listening immediately. For CosmosDB: calls InitializeAsync() to create containers,
/// then seeds system templates. For SQLite: runs EF Core migrations and seeds default data.
/// </summary>
public class DatabaseSeedingService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DatabaseSeedingService> _logger;

    public DatabaseSeedingService(IServiceProvider services, ILogger<DatabaseSeedingService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield immediately so Kestrel can start listening before we do any work.
        await Task.Yield();

        _logger.LogInformation("[SEEDER-BG] Starting background database initialization");

        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ICatanDb>();

            // Upsert system templates on every startup to stay in sync with code changes.
            // Database and containers must already exist (created by catan.ps1 database install).
            await DatabaseSeeder.UpsertSystemTemplatesAsync(db, _logger);
            _logger.LogInformation("[SEEDER-BG] System templates upserted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SEEDER-BG] Failed to upsert templates. Is the database installed? Run: ./catan.ps1 database install");
        }
    }
}

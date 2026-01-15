using System.Text.RegularExpressions;

namespace Catan3.GameService.Data;

/// <summary>
/// Detects whether to use SQLite (local) or SQL Server (Azure).
/// Zero-config: localhost always uses SQLite, Azure uses SQL Server.
/// </summary>
public class DatabaseProviderDetector
{
    private readonly IConfiguration _configuration;
    private readonly bool _useSqlServer;
    private readonly string _connectionString = null!; // Set in constructor via DetermineConnectionString()
    private readonly string _dataDirectory = null!;    // Set in constructor via DetermineDataDirectory()

    public DatabaseProviderDetector(IConfiguration configuration)
    {
        _configuration = configuration;

        Console.WriteLine("[DB-DETECT] DatabaseProviderDetector constructor starting");
        Console.WriteLine($"[DB-DETECT] WEBSITE_SITE_NAME: {configuration["WEBSITE_SITE_NAME"] ?? "(not set)"}");
        Console.WriteLine($"[DB-DETECT] DATABASE_PROVIDER: {configuration["DATABASE_PROVIDER"] ?? "(not set)"}");

        // Determine provider and cache the decision
        _useSqlServer = DetermineUseSqlServer();
        Console.WriteLine($"[DB-DETECT] UseSqlServer: {_useSqlServer}");

        _connectionString = DetermineConnectionString();
        Console.WriteLine($"[DB-DETECT] ConnectionString determined (length: {_connectionString?.Length ?? 0})");

        _dataDirectory = DetermineDataDirectory();
        Console.WriteLine($"[DB-DETECT] DataDirectory: {_dataDirectory}");
    }

    /// <summary>
    /// True if running in Azure App Service
    /// </summary>
    public bool IsAzure => !string.IsNullOrEmpty(_configuration["WEBSITE_SITE_NAME"]);

    /// <summary>
    /// True if SQL Server should be used (Azure deployment or explicit override)
    /// </summary>
    public bool UseSqlServer => _useSqlServer;

    /// <summary>
    /// Get the appropriate connection string
    /// </summary>
    public string ConnectionString => _connectionString;

    /// <summary>
    /// Get the data directory path (for SQLite file and temp files)
    /// </summary>
    public string DataDirectory => _dataDirectory;

    /// <summary>
    /// Get the database provider name for logging/diagnostics
    /// </summary>
    public string ProviderName => UseSqlServer ? "SqlServer" : "SQLite";

    private bool DetermineUseSqlServer()
    {
        // Explicit override via environment variable or config
        var databaseProvider = _configuration["DATABASE_PROVIDER"];
        if (!string.IsNullOrEmpty(databaseProvider))
        {
            return databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase);
        }

        // Azure App Service detected - use SQL Server
        if (IsAzure)
        {
            return true;
        }

        // Check if AzureSql connection string is configured (for local testing with SQL Server)
        var azureSqlConnection = _configuration.GetConnectionString("AzureSql");
        if (!string.IsNullOrEmpty(azureSqlConnection))
        {
            // Only use if explicitly requested via DATABASE_PROVIDER
            // Having the connection string alone doesn't switch providers
            return false;
        }

        // Default: SQLite for local development
        return false;
    }

    private string DetermineConnectionString()
    {
        if (UseSqlServer)
        {
            var connectionString = _configuration.GetConnectionString("AzureSql");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    "AzureSql connection string is required when using SQL Server. " +
                    "Set ConnectionStrings:AzureSql in configuration or App Service settings.");
            }
            return connectionString;
        }

        // SQLite - use configured path or default
        var configuredConnection = _configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(configuredConnection))
        {
            return configuredConnection;
        }

        // Default SQLite path - handle both development and published scenarios
        var dataDir = GetDefaultDataDirectory();
        var dbPath = Path.Combine(dataDir, "catan.db");
        return $"Data Source={dbPath}";
    }

    private string DetermineDataDirectory()
    {
        if (UseSqlServer)
        {
            // SQL Server doesn't need local data directory for database
            // but we still need it for temporary files
            return Path.Combine(AppContext.BaseDirectory, "Data");
        }

        // SQLite - extract directory from connection string
        var match = Regex.Match(ConnectionString, @"Data Source=(.+)");
        if (match.Success)
        {
            var dbPath = match.Groups[1].Value;
            var directory = Path.GetDirectoryName(Path.GetFullPath(dbPath));
            return directory ?? GetDefaultDataDirectory();
        }

        return GetDefaultDataDirectory();
    }

    private static string GetDefaultDataDirectory()
    {
        // Check if running in published mode (Data folder exists next to executable)
        var publishedDataDir = Path.Combine(AppContext.BaseDirectory, "Data");
        if (Directory.Exists(publishedDataDir))
        {
            return publishedDataDir;
        }

        // Development mode: navigate up from bin/Debug/net9.0 to project's Data folder
        var devDataDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data");
        return Path.GetFullPath(devDataDir);
    }

    /// <summary>
    /// Get the path to the Default Data folder (for seeding)
    /// </summary>
    public string GetDefaultDataPath()
    {
        // Check published location first
        var publishedPath = Path.Combine(AppContext.BaseDirectory, "Default Data");
        if (Directory.Exists(publishedPath))
        {
            return publishedPath;
        }

        // Fallback for development: navigate up from bin/Debug/net9.0 to project root
        var projectRoot = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..");
        var devPath = Path.Combine(projectRoot, "Catan3.GameService", "Default Data");
        return Path.GetFullPath(devPath);
    }
}

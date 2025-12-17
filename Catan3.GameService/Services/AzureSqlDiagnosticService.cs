using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Sql;
using Azure.ResourceManager.Sql.Models;
using Microsoft.Data.SqlClient;

namespace Catan3.GameService.Services;

/// <summary>
/// Provides diagnostics for Azure SQL connectivity issues.
/// Can detect common problems like:
/// - Database paused (auto-pause)
/// - Public network access disabled
/// - Firewall rules blocking access
/// - Managed identity not configured
///
/// Uses Azure Resource Graph to find resources by FQDN from the connection string,
/// avoiding the need to iterate through all subscriptions.
/// </summary>
public class AzureSqlDiagnosticService
{
    private readonly ILogger<AzureSqlDiagnosticService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    // Cache the resource ID once we find it
    private string? _cachedDatabaseResourceId;
    private string? _cachedServerResourceId;

    public AzureSqlDiagnosticService(
        ILogger<AzureSqlDiagnosticService> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Diagnoses Azure SQL connectivity issues and returns actionable information.
    /// </summary>
    public async Task<SqlDiagnosticResult> DiagnoseAsync(Exception? connectionException = null)
    {
        var result = new SqlDiagnosticResult
        {
            Timestamp = DateTime.UtcNow,
            IsAzureSql = IsRunningOnAzure()
        };

        // Parse connection string to get server and database names
        var connectionString = GetConnectionString();
        if (string.IsNullOrEmpty(connectionString))
        {
            result.Error = "No connection string configured";
            result.Recommendation = "Configure AzureSql connection string in App Service";
            return result;
        }

        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            result.ServerName = builder.DataSource;
            result.DatabaseName = builder.InitialCatalog;
        }
        catch (Exception ex)
        {
            result.Error = $"Invalid connection string: {ex.Message}";
            return result;
        }

        // Analyze the connection exception if provided
        if (connectionException != null)
        {
            result.ConnectionError = connectionException.Message;
            AnalyzeException(connectionException, result);
        }

        // If running on Azure with managed identity, try to get resource status
        if (result.IsAzureSql && !string.IsNullOrEmpty(result.ServerName))
        {
            await TryGetAzureResourceStatusAsync(result);
        }

        return result;
    }

    /// <summary>
    /// Uses Azure Resource Graph to find the SQL database resource ID by FQDN.
    /// This is much faster than iterating through subscriptions.
    /// </summary>
    private async Task<(string? serverId, string? databaseId)> FindResourceIdsByFqdnAsync(string serverFqdn, string databaseName)
    {
        // Return cached values if available
        if (_cachedServerResourceId != null && _cachedDatabaseResourceId != null)
            return (_cachedServerResourceId, _cachedDatabaseResourceId);

        try
        {
            var credential = new DefaultAzureCredential();
            var token = await credential.GetTokenAsync(new TokenRequestContext(["https://management.azure.com/.default"]));

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

            // Extract server name from FQDN (e.g., "sql-catan" from "sql-catan.database.windows.net")
            var serverName = ExtractServerName(serverFqdn);

            // Use Resource Graph to find the SQL server by FQDN
            var query = new
            {
                query = $@"
                    resources
                    | where type == 'microsoft.sql/servers'
                    | where properties.fullyQualifiedDomainName =~ '{serverFqdn}'
                       or name =~ '{serverName}'
                    | project id, name, resourceGroup, subscriptionId
                    | limit 1"
            };

            var content = new StringContent(JsonSerializer.Serialize(query), System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://management.azure.com/providers/Microsoft.ResourceGraph/resources?api-version=2022-10-01", content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Resource Graph query failed: {Status}", response.StatusCode);
                return (null, null);
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var data = doc.RootElement.GetProperty("data");
            if (data.GetArrayLength() == 0)
            {
                _logger.LogDebug("No SQL server found matching FQDN: {Fqdn}", serverFqdn);
                return (null, null);
            }

            var serverResource = data[0];
            var serverId = serverResource.GetProperty("id").GetString();
            var databaseId = $"{serverId}/databases/{databaseName}";

            // Cache the results
            _cachedServerResourceId = serverId;
            _cachedDatabaseResourceId = databaseId;

            _logger.LogDebug("Found SQL Server: {ServerId}", serverId);
            return (serverId, databaseId);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Failed to find resource via Resource Graph: {Error}", ex.Message);
            return (null, null);
        }
    }

    /// <summary>
    /// Attempts to fix common Azure SQL connectivity issues.
    /// Returns a result indicating what was checked, fixed, and what remains broken.
    /// </summary>
    public async Task<TroubleshootResult> TroubleshootAsync()
    {
        var result = new TroubleshootResult
        {
            Timestamp = DateTime.UtcNow,
            IsAzure = IsRunningOnAzure()
        };

        if (!result.IsAzure)
        {
            result.Message = "Not running on Azure - no troubleshooting needed";
            return result;
        }

        var connectionString = GetConnectionString();
        if (string.IsNullOrEmpty(connectionString))
        {
            result.Issues.Add("No connection string configured");
            result.Message = "Cannot troubleshoot without a connection string";
            return result;
        }

        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var serverFqdn = builder.DataSource?.Replace("tcp:", "").Split(',')[0];
            var databaseName = builder.InitialCatalog;
            result.ServerFqdn = serverFqdn;
            result.DatabaseName = databaseName;

            if (string.IsNullOrEmpty(serverFqdn) || string.IsNullOrEmpty(databaseName))
            {
                result.Issues.Add("Invalid connection string - missing server or database");
                return result;
            }

            var (serverId, databaseId) = await FindResourceIdsByFqdnAsync(serverFqdn, databaseName);
            if (string.IsNullOrEmpty(serverId))
            {
                result.Issues.Add("Could not find SQL Server via Resource Graph - check permissions");
                return result;
            }

            var credential = new DefaultAzureCredential();
            var armClient = new ArmClient(credential);

            // Check and fix server settings
            var serverResource = armClient.GetSqlServerResource(new ResourceIdentifier(serverId));
            var server = await serverResource.GetAsync();

            // Check Public Network Access
            var publicNetworkAccess = server.Value.Data.PublicNetworkAccess?.ToString();
            result.Checks.Add($"Public Network Access: {publicNetworkAccess}");

            if (publicNetworkAccess?.Equals("Disabled", StringComparison.OrdinalIgnoreCase) == true)
            {
                result.Issues.Add("Public Network Access is disabled");
                try
                {
                    _logger.LogInformation("Enabling public network access on SQL Server...");
                    var patch = new SqlServerPatch { PublicNetworkAccess = ServerNetworkAccessFlag.Enabled };
                    await serverResource.UpdateAsync(Azure.WaitUntil.Completed, patch);
                    result.Fixed.Add("Enabled Public Network Access");
                    _logger.LogInformation("Public network access enabled successfully");
                }
                catch (Exception ex)
                {
                    result.CannotFix.Add($"Failed to enable Public Network Access: {ex.Message}");
                    _logger.LogWarning(ex, "Failed to enable public network access");
                }
            }

            // Check firewall rule
            var firewallRules = serverResource.GetSqlFirewallRules();
            var hasAllowAzureServices = false;
            await foreach (var rule in firewallRules.GetAllAsync())
            {
                if (rule.Data.StartIPAddress == "0.0.0.0" && rule.Data.EndIPAddress == "0.0.0.0")
                {
                    hasAllowAzureServices = true;
                    result.Checks.Add($"Firewall rule '{rule.Data.Name}': AllowAzureServices (0.0.0.0-0.0.0.0)");
                    break;
                }
            }

            if (!hasAllowAzureServices)
            {
                result.Issues.Add("AllowAzureServices firewall rule is missing");
                try
                {
                    _logger.LogInformation("Creating AllowAzureServices firewall rule...");
                    var ruleData = new SqlFirewallRuleData { StartIPAddress = "0.0.0.0", EndIPAddress = "0.0.0.0" };
                    await firewallRules.CreateOrUpdateAsync(Azure.WaitUntil.Completed, "AllowAzureServices", ruleData);
                    result.Fixed.Add("Created AllowAzureServices firewall rule");
                    _logger.LogInformation("Firewall rule created successfully");
                }
                catch (Exception ex)
                {
                    result.CannotFix.Add($"Failed to create firewall rule: {ex.Message}");
                    _logger.LogWarning(ex, "Failed to create firewall rule");
                }
            }
            else
            {
                result.Checks.Add("AllowAzureServices firewall rule: OK");
            }

            // Check database status
            if (!string.IsNullOrEmpty(databaseId))
            {
                var dbResource = armClient.GetSqlDatabaseResource(new ResourceIdentifier(databaseId));
                var db = await dbResource.GetAsync();
                var dbStatus = db.Value.Data.Status?.ToString();
                result.Checks.Add($"Database status: {dbStatus}");

                if (dbStatus?.Equals("Paused", StringComparison.OrdinalIgnoreCase) == true)
                {
                    result.Issues.Add("Database is paused (auto-pause)");
                    result.CannotFix.Add("Database will auto-resume on first connection (30-60 second delay)");
                }
            }

            // Test connection after fixes
            result.Checks.Add("Testing database connection...");
            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                result.Checks.Add("Database connection: SUCCESS");
                result.ConnectionSuccessful = true;
            }
            catch (Exception ex)
            {
                result.Checks.Add($"Database connection: FAILED - {ex.Message}");
                result.ConnectionSuccessful = false;
                if (!result.Issues.Any())
                {
                    result.Issues.Add($"Connection failed: {ex.Message}");
                }
            }

            // Summary
            if (result.Fixed.Any() && result.ConnectionSuccessful)
            {
                result.Message = $"Fixed {result.Fixed.Count} issue(s) - connection now working";
            }
            else if (result.Fixed.Any())
            {
                result.Message = $"Fixed {result.Fixed.Count} issue(s) but connection still failing";
            }
            else if (result.ConnectionSuccessful)
            {
                result.Message = "No issues found - connection working";
            }
            else if (result.CannotFix.Any())
            {
                result.Message = $"Found issues that cannot be auto-fixed: {string.Join(", ", result.CannotFix)}";
            }
            else
            {
                result.Message = "Troubleshooting complete but connection still failing";
            }
        }
        catch (Exception ex)
        {
            result.Issues.Add($"Troubleshooting failed: {ex.Message}");
            result.Message = $"Error during troubleshooting: {ex.Message}";
            _logger.LogError(ex, "Troubleshooting failed");
        }

        return result;
    }

    /// <summary>
    /// Gets the database status directly using its resource ID.
    /// </summary>
    public async Task<(bool IsPaused, string? Status, string? PublicNetworkAccess)> CheckDatabaseStatusAsync()
    {
        if (!IsRunningOnAzure())
            return (false, null, null);

        try
        {
            var connectionString = GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
                return (false, null, null);

            var builder = new SqlConnectionStringBuilder(connectionString);
            var serverFqdn = builder.DataSource?.Replace("tcp:", "").Split(',')[0];
            var databaseName = builder.InitialCatalog;

            if (string.IsNullOrEmpty(serverFqdn) || string.IsNullOrEmpty(databaseName))
                return (false, null, null);

            var (serverId, databaseId) = await FindResourceIdsByFqdnAsync(serverFqdn, databaseName);
            if (string.IsNullOrEmpty(serverId) || string.IsNullOrEmpty(databaseId))
                return (false, "ResourceNotFound", null);

            // Get database and server status using ARM
            var credential = new DefaultAzureCredential();
            var armClient = new ArmClient(credential);

            var serverResource = armClient.GetSqlServerResource(new ResourceIdentifier(serverId));
            var server = await serverResource.GetAsync();
            var publicNetworkAccess = server.Value.Data.PublicNetworkAccess?.ToString();

            var dbResource = armClient.GetSqlDatabaseResource(new ResourceIdentifier(databaseId));
            var db = await dbResource.GetAsync();
            var status = db.Value.Data.Status?.ToString();
            var isPaused = status?.Equals("Paused", StringComparison.OrdinalIgnoreCase) == true;

            _logger.LogDebug("Database {Database} status: {Status}, PublicNetworkAccess: {PNA}",
                databaseName, status, publicNetworkAccess);

            return (isPaused, status, publicNetworkAccess);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check database status via Azure Resource Manager");
            return (false, null, null);
        }
    }

    private void AnalyzeException(Exception ex, SqlDiagnosticResult result)
    {
        var message = ex.Message.ToLowerInvariant();
        var fullMessage = ex.ToString().ToLowerInvariant();

        if (message.Contains("deny public network access"))
        {
            result.Issue = SqlDiagnosticIssue.PublicNetworkAccessDenied;
            result.Recommendation = "Enable public network access on the SQL Server in Azure Portal, or configure Private Endpoint";
            result.CanAutoFix = false;
        }
        else if (message.Contains("login failed") && message.Contains("managed identity"))
        {
            result.Issue = SqlDiagnosticIssue.ManagedIdentityNotConfigured;
            result.Recommendation = "Run 'catan.ps1 azure database deploy' to grant managed identity access";
            result.CanAutoFix = false;
        }
        else if (message.Contains("firewall") || message.Contains("ip address") && message.Contains("not allowed"))
        {
            result.Issue = SqlDiagnosticIssue.FirewallBlocking;
            result.Recommendation = "Add AllowAzureServices firewall rule (0.0.0.0-0.0.0.0) to the SQL Server";
            result.CanAutoFix = false;
        }
        else if (message.Contains("timeout") || message.Contains("timed out"))
        {
            result.Issue = SqlDiagnosticIssue.ConnectionTimeout;
            result.Recommendation = "Database may be resuming from auto-pause. Wait 30-60 seconds and retry.";
            result.CanAutoFix = true;
        }
        else if (fullMessage.Contains("serverless") && fullMessage.Contains("paused"))
        {
            result.Issue = SqlDiagnosticIssue.DatabasePaused;
            result.Recommendation = "Database is paused due to inactivity. It will auto-resume on the next connection attempt (may take 30-60 seconds).";
            result.CanAutoFix = true;
        }
        else if (message.Contains("network") || message.Contains("connection"))
        {
            result.Issue = SqlDiagnosticIssue.NetworkConnectivity;
            result.Recommendation = "Check network connectivity. Verify SQL Server FQDN is correct and App Service has outbound access.";
            result.CanAutoFix = false;
        }
        else
        {
            result.Issue = SqlDiagnosticIssue.Unknown;
            result.Recommendation = "Check Azure Portal for SQL Server and database status. Review application logs for details.";
        }
    }

    private async Task TryGetAzureResourceStatusAsync(SqlDiagnosticResult result)
    {
        try
        {
            var serverFqdn = result.ServerName?.Replace("tcp:", "").Split(',')[0];
            if (string.IsNullOrEmpty(serverFqdn) || string.IsNullOrEmpty(result.DatabaseName))
                return;

            var (serverId, databaseId) = await FindResourceIdsByFqdnAsync(serverFqdn, result.DatabaseName);
            if (string.IsNullOrEmpty(serverId) || string.IsNullOrEmpty(databaseId))
            {
                _logger.LogDebug("Could not find SQL resources via Resource Graph");
                return;
            }

            var credential = new DefaultAzureCredential();
            var armClient = new ArmClient(credential);

            // Get server status
            var serverResource = armClient.GetSqlServerResource(new ResourceIdentifier(serverId));
            var server = await serverResource.GetAsync();

            result.AzureStatus = new AzureSqlStatus
            {
                ServerName = server.Value.Data.Name,
                ServerState = server.Value.Data.State,
                PublicNetworkAccess = server.Value.Data.PublicNetworkAccess?.ToString() ?? "Unknown"
            };

            // Check public network access
            if (server.Value.Data.PublicNetworkAccess?.ToString()?.Equals("Disabled", StringComparison.OrdinalIgnoreCase) == true)
            {
                result.Issue = SqlDiagnosticIssue.PublicNetworkAccessDenied;
                result.Recommendation = "Public network access is disabled. Enable it in Azure Portal or use 'catan.ps1 azure database deploy'.";
                result.CanAutoFix = false;
            }

            // Get database status
            var dbResource = armClient.GetSqlDatabaseResource(new ResourceIdentifier(databaseId));
            var db = await dbResource.GetAsync();
            result.AzureStatus.DatabaseStatus = db.Value.Data.Status?.ToString() ?? "Unknown";

            if (db.Value.Data.Status?.ToString()?.Equals("Paused", StringComparison.OrdinalIgnoreCase) == true)
            {
                result.Issue = SqlDiagnosticIssue.DatabasePaused;
                result.Recommendation = "Database is paused due to inactivity. First connection will resume it (30-60 second delay).";
                result.CanAutoFix = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Could not get Azure resource status: {Error}", ex.Message);
            // This is optional diagnostics - don't fail if we can't get it
        }
    }

    private string? GetConnectionString()
    {
        // Check for AzureSql connection string (configured in App Service)
        var connStr = _configuration.GetConnectionString("AzureSql");
        if (!string.IsNullOrEmpty(connStr))
            return connStr;

        // Fall back to environment variable
        return Environment.GetEnvironmentVariable("SQLAZURECONNSTR_AzureSql");
    }

    private bool IsRunningOnAzure()
    {
        // Check common Azure App Service environment variables
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")) ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT"));
    }

    private static string? ExtractServerName(string? dataSource)
    {
        if (string.IsNullOrEmpty(dataSource))
            return null;

        // Handle "tcp:servername.database.windows.net,1433" format
        var name = dataSource
            .Replace("tcp:", "", StringComparison.OrdinalIgnoreCase)
            .Split(',')[0]
            .Split('.')[0];

        return name;
    }
}

/// <summary>
/// Result of SQL diagnostic check.
/// </summary>
public class SqlDiagnosticResult
{
    public DateTime Timestamp { get; set; }
    public bool IsAzureSql { get; set; }
    public string? ServerName { get; set; }
    public string? DatabaseName { get; set; }
    public string? ConnectionError { get; set; }
    public string? Error { get; set; }
    public SqlDiagnosticIssue Issue { get; set; } = SqlDiagnosticIssue.None;
    public string? Recommendation { get; set; }
    public bool CanAutoFix { get; set; }
    public AzureSqlStatus? AzureStatus { get; set; }
}

/// <summary>
/// Status information from Azure Resource Manager.
/// </summary>
public class AzureSqlStatus
{
    public string? ServerName { get; set; }
    public string? ServerState { get; set; }
    public string? PublicNetworkAccess { get; set; }
    public string? DatabaseStatus { get; set; }
}

/// <summary>
/// Common Azure SQL connectivity issues.
/// </summary>
public enum SqlDiagnosticIssue
{
    None,
    Unknown,
    PublicNetworkAccessDenied,
    FirewallBlocking,
    ManagedIdentityNotConfigured,
    DatabasePaused,
    ConnectionTimeout,
    NetworkConnectivity
}

/// <summary>
/// Result of troubleshooting attempt.
/// </summary>
public class TroubleshootResult
{
    public DateTime Timestamp { get; set; }
    public bool IsAzure { get; set; }
    public string? ServerFqdn { get; set; }
    public string? DatabaseName { get; set; }
    public string? Message { get; set; }
    public bool ConnectionSuccessful { get; set; }
    public List<string> Checks { get; set; } = new();
    public List<string> Issues { get; set; } = new();
    public List<string> Fixed { get; set; } = new();
    public List<string> CannotFix { get; set; } = new();
}

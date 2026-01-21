# Azure SQL Serverless Alternative Analysis

**Document Version:** 1.0  
**Last Updated:** 2024-12-08  
**Status:** Alternative Proposal

## Overview

This document analyzes using Azure SQL Serverless as an alternative to the CosmosDB + DAL approach, potentially eliminating the need for a complex data access layer while maintaining SQLite compatibility for local development.

## Azure SQL Serverless Benefits

### 🎯 **Eliminate DAL Complexity**

- **Same SQL Engine** - SQLite and SQL Server both use SQL, easier migration
- **Entity Framework Works** - No need to rewrite data access code
- **Familiar Tooling** - SQL Server Management Studio, existing knowledge
- **Direct Schema Migration** - Can migrate SQLite schema with minimal changes

### 💰 **Cost Efficiency**

- **Pay Per Use** - Only charged for actual compute time (per second billing)
- **Auto-Pause** - Database pauses after inactivity (1-hour minimum)
- **Serverless Scaling** - Automatically scales from 0.5 to 40 vCores
- **Storage Costs** - Only pay for data storage when paused (~$5-15/month for typical usage)

### 🔧 **Zero Configuration Benefits**

- **Connection String Switch** - Only need to change connection string for Azure
- **Same EF Code** - `CatanDbContext` works unchanged
- **Schema Compatibility** - Most SQLite schemas work on SQL Server
- **Backup/Recovery** - Built-in point-in-time restore

## Comparison Analysis

| Factor | CosmosDB + DAL | Azure SQL Serverless |
|--------|----------------|---------------------|
| **Code Changes** | Major - Full DAL implementation | Minimal - Connection string + minor schema tweaks |
| **Development Complexity** | High - Two different data models | Low - Same EF model everywhere |
| **Local Development** | SQLite (fast) | SQLite (fast) |
| **Cloud Deployment** | CosmosDB (NoSQL) | Azure SQL (SQL) |
| **Query Capabilities** | Limited SQL, custom filtering | Full SQL Server features |
| **Cost (Low Usage)** | ~$1-5/month (serverless) | ~$5-15/month (auto-pause) |
| **Cost (High Usage)** | Scales with RU usage | Scales with vCore usage |
| **Backup/Recovery** | Manual export/import | Automatic backups, point-in-time restore |
| **Performance** | Excellent for document queries | Excellent for relational queries |
| **Learning Curve** | High - New patterns | Low - Familiar SQL patterns |

## Simplified Architecture with Azure SQL Serverless

### Configuration Detection (Simplified)

```csharp
public class ConnectionStringProvider
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public string GetConnectionString()
    {
        // Zero config: localhost uses SQLite, Azure uses SQL Server
        var isAzure = !string.IsNullOrEmpty(_configuration["WEBSITE_SITE_NAME"]);
        
        if (isAzure)
        {
            return _configuration.GetConnectionString("AzureSqlConnection") 
                ?? throw new InvalidOperationException("AzureSqlConnection required for Azure deployment");
        }
        else
        {
            return _configuration.GetConnectionString("DefaultConnection") 
                ?? "Data Source=Data/catan.db";
        }
    }

    public bool IsAzureSql()
    {
        return !string.IsNullOrEmpty(_configuration["WEBSITE_SITE_NAME"]);
    }
}
```

### Simplified Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

var connectionProvider = new ConnectionStringProvider(builder.Configuration, builder.Environment);
var connectionString = connectionProvider.GetConnectionString();

// Same DbContext registration for both SQLite and SQL Server!
builder.Services.AddDbContext<CatanDbContext>(options =>
{
    if (connectionProvider.IsAzureSql())
    {
        options.UseSqlServer(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

// No need for IDataRepository - use CatanDbContext directly
builder.Services.AddScoped<IGamePersistence, DatabasePersistenceService>();
```

### Schema Compatibility

Most SQLite schema translates directly:

```sql
-- SQLite (current)
CREATE TABLE Players (
    Id TEXT PRIMARY KEY,
    Data TEXT NOT NULL
);

-- Azure SQL (minimal changes)  
CREATE TABLE Players (
    Id NVARCHAR(255) PRIMARY KEY,
    Data NVARCHAR(MAX) NOT NULL
);
```

### Migration Strategy (Simplified)

1. **Phase 1: Add SQL Server Support**
   - Update `CatanDbContext` to support both providers
   - Add connection string detection logic
   - Test locally with SQL Server LocalDB

2. **Phase 2: Deploy to Azure**
   - Provision Azure SQL Serverless database
   - Run EF migrations to create schema
   - Deploy app with Azure SQL connection string

3. **Phase 3: Data Migration (Optional)**
   - Export SQLite data
   - Import to Azure SQL using standard tools
   - No custom migration code needed

## Game Filtering with SQL Server

The game filtering becomes much simpler with full SQL support:

```csharp
public async Task<List<GameSaveMetadataEntity>> GetGamesByStateAsync(
    GameStateFilter filter, 
    string? startedBy = null)
{
    var query = _context.GameSaveMetadata.Include(g => g.GameData).AsQueryable();

    // Simple LINQ expressions work perfectly
    if (filter.ExcludeStates?.Any() == true)
    {
        query = query.Where(g => !filter.ExcludeStates.Contains(g.GameState));
    }

    if (filter.IncludeStates?.Any() == true)  
    {
        query = query.Where(g => filter.IncludeStates.Contains(g.GameState));
    }

    if (filter.ActiveGamesOnly)
    {
        query = query.Where(g => g.GameState != "GameOver");
    }

    if (filter.SavedAfter.HasValue)
    {
        query = query.Where(g => g.SavedAt >= filter.SavedAfter.Value);
    }

    if (!string.IsNullOrEmpty(startedBy) && startedBy != "*")
    {
        query = query.Where(g => g.StartedBy == startedBy);
    }

    if (filter.MaxResults.HasValue)
    {
        query = query.Take(filter.MaxResults.Value);
    }

    if (filter.Skip > 0)
    {
        query = query.Skip(filter.Skip);
    }

    return await query.OrderByDescending(g => g.SavedAt).ToListAsync();
}
```

## Potential Drawbacks

### Azure SQL Serverless Limitations

- **Minimum Cost** - Even when paused, storage costs ~$5-15/month
- **Cold Start** - 1-2 second delay when resuming from pause
- **Connection Limits** - Fewer concurrent connections than CosmosDB
- **Regional Availability** - Not available in all Azure regions

### SQLite vs SQL Server Differences

- **Data Types** - Some minor mapping differences (TEXT vs NVARCHAR)
- **Functions** - Some SQLite-specific functions not available
- **Constraints** - SQL Server has stricter constraint checking
- **Case Sensitivity** - SQL Server is case-insensitive by default

## Recommendation

**Azure SQL Serverless is likely the better choice** for this project because:

1. **🚀 Dramatically Simpler** - No DAL needed, same EF code works everywhere
2. **💰 Cost Effective** - Similar cost to CosmosDB for low usage scenarios  
3. **🔧 Zero Learning Curve** - Team already knows SQL Server
4. **🛠️ Better Tooling** - SSMS, familiar debugging, query optimization tools
5. **📊 Rich Querying** - Full SQL capabilities vs limited CosmosDB SQL
6. **🔄 Easy Migration** - Can migrate existing SQLite data with standard tools

## Updated Implementation Plan

### Immediate Steps

1. **Update CatanDbContext** to support SQL Server provider
2. **Add connection detection logic**
3. **Test locally** with SQL Server LocalDB
4. **Create Azure SQL Serverless** database
5. **Deploy and test** end-to-end

### Configuration Example

```json
// Local (appsettings.json) - No config needed!
{
  // SQLite used automatically on localhost
}

// Azure (App Service Configuration)
{
  "ConnectionStrings": {
    "AzureSqlConnection": "Server=tcp:catan-sql.database.windows.net,1433;Database=catan;Authentication=Active Directory Managed Identity;Encrypt=True;"
  }
}
```

This approach delivers the same functionality with 90% less code complexity!

---

## Implementation Plan

**Status:** APPROVED
**Target:** Replace SQLite-on-Azure with Azure SQL Serverless

### Plan Overview

This plan migrates the Catan3 application from SQLite (which doesn't work correctly with
multiple Azure App Service instances) to Azure SQL Serverless while maintaining SQLite for
local development.

### Goals

1. **Multi-instance support** - All App Service instances share same database
2. **Zero-config local dev** - SQLite works automatically on localhost
3. **Cost effective** - Serverless tier auto-pauses when idle
4. **Minimal code changes** - Same EF Core model, different provider

### Components Affected

| Component | Changes Required |
|-----------|------------------|
| **Catan3.GameService** | Add SQL Server provider, connection detection |
| **catan-azure.ps1** | Provision SQL Server, configure connection string |
| **webui.ps1** | Update database commands for dual-mode |
| **WebUI** | None - uses GameService API |
| **Database** | Schema compatible, seeding updates |

---

## Phase 1: GameService Database Abstraction

### Step 1.1: Add SQL Server NuGet Package

**File:** `Catan3.GameService/Catan3.GameService.csproj`

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.0" />
```

### Step 1.2: Create Database Provider Detection

**File:** `Catan3.GameService/Data/DatabaseProviderDetector.cs` (NEW)

```csharp
namespace Catan3.GameService.Data;

/// <summary>
/// Detects whether to use SQLite (local) or SQL Server (Azure).
/// Zero-config: localhost always uses SQLite, Azure uses SQL Server.
/// </summary>
public class DatabaseProviderDetector
{
    private readonly IConfiguration _configuration;

    public DatabaseProviderDetector(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// True if running in Azure App Service
    /// </summary>
    public bool IsAzure => !string.IsNullOrEmpty(_configuration["WEBSITE_SITE_NAME"]);

    /// <summary>
    /// True if SQL Server should be used (Azure deployment)
    /// </summary>
    public bool UseSqlServer => IsAzure ||
        _configuration["DATABASE_PROVIDER"]?.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Get the appropriate connection string
    /// </summary>
    public string GetConnectionString()
    {
        if (UseSqlServer)
        {
            return _configuration.GetConnectionString("AzureSql")
                ?? throw new InvalidOperationException(
                    "AzureSql connection string required for SQL Server mode");
        }

        // SQLite - use configured path or default
        return _configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=Data/catan.db";
    }

    /// <summary>
    /// Get the data directory path
    /// </summary>
    public string GetDataDirectory()
    {
        if (UseSqlServer)
        {
            // SQL Server doesn't need local data directory for database
            // but we still need it for temporary files
            return Path.Combine(AppContext.BaseDirectory, "Data");
        }

        // SQLite - extract directory from connection string
        var connString = GetConnectionString();
        var match = System.Text.RegularExpressions.Regex.Match(
            connString, @"Data Source=(.+)");
        if (match.Success)
        {
            return Path.GetDirectoryName(Path.GetFullPath(match.Groups[1].Value))
                ?? "Data";
        }
        return "Data";
    }
}
```

### Step 1.3: Update Program.cs for Dual Provider

**File:** `Catan3.GameService/Program.cs`

Update the DbContext registration:

```csharp
// Database provider detection (zero-config)
var dbDetector = new DatabaseProviderDetector(builder.Configuration);
builder.Services.AddSingleton(dbDetector);

// Register DbContext with appropriate provider
builder.Services.AddDbContext<CatanDbContext>((serviceProvider, options) =>
{
    var detector = serviceProvider.GetRequiredService<DatabaseProviderDetector>();

    if (detector.UseSqlServer)
    {
        options.UseSqlServer(detector.GetConnectionString(), sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        });
    }
    else
    {
        options.UseSqlite(detector.GetConnectionString());
    }
});
```

### Step 1.4: Update CatanDbContext for SQL Server Compatibility

**File:** `Catan3.GameService/Data/CatanDbContext.cs`

Add SQL Server-specific configuration in `OnModelCreating`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // Players table
    modelBuilder.Entity<PlayerEntity>(entity =>
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).HasMaxLength(255);
        entity.Property(e => e.Data).IsRequired();
        // SQL Server uses NVARCHAR(MAX) for large text, SQLite uses TEXT
    });

    // Images table
    modelBuilder.Entity<ImageEntity>(entity =>
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).HasMaxLength(255);
        entity.Property(e => e.ContentType).HasMaxLength(100);
        // Binary data works the same in both providers
    });

    // Game metadata table
    modelBuilder.Entity<GameSaveMetadataEntity>(entity =>
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.GameId).HasMaxLength(255);
        entity.Property(e => e.GameName).HasMaxLength(255);
        entity.Property(e => e.GameState).HasMaxLength(50);
        entity.Property(e => e.StartedBy).HasMaxLength(255);
        entity.Property(e => e.GameType).HasMaxLength(50);
        entity.Property(e => e.PlayerNames).HasMaxLength(500);

        entity.HasIndex(e => e.GameId).IsUnique();
        entity.HasIndex(e => e.GameState);
        entity.HasIndex(e => e.SavedAt);
    });

    // Game data table (blob storage)
    modelBuilder.Entity<GameSaveDataEntity>(entity =>
    {
        entity.HasKey(e => e.Id);
        // CompressedData is VARBINARY(MAX) in SQL Server
    });
}
```

### Step 1.5: Update DatabaseSeeder for SQL Server

**File:** `Catan3.GameService/Data/DatabaseSeeder.cs`

Update to handle both providers:

```csharp
public static async Task SeedAsync(
    CatanDbContext context,
    string defaultDataPath,
    IGamePersistence? gamePersistence = null,
    bool useSqlServer = false)
{
    if (useSqlServer)
    {
        // SQL Server: Use migrations or EnsureCreated
        // Migrations preferred for production
        await context.Database.MigrateAsync();
    }
    else
    {
        // SQLite: EnsureCreated is fine for development
        await context.Database.EnsureCreatedAsync();
    }

    // Rest of seeding logic unchanged...
}
```

### Step 1.6: Add Health Check for Database Provider

**File:** `Catan3.GameService/Controllers/GameApiController.cs`

Update health endpoint to show provider:

```csharp
[HttpGet("/health")]
public IActionResult GetHealth()
{
    var detector = HttpContext.RequestServices.GetService<DatabaseProviderDetector>();
    var provider = detector?.UseSqlServer == true ? "SqlServer" : "SQLite";

    return Ok(new
    {
        status = "healthy",
        timestamp = DateTime.UtcNow,
        database = new
        {
            provider = provider,
            isAzure = detector?.IsAzure ?? false
        }
    });
}
```

---

## Phase 2: Azure Infrastructure Scripts

### Step 2.1: Add SQL Server Provisioning to catan-azure.ps1

**File:** `.scripts/catan-azure.ps1`

Add new noun `sql` with verbs: `install`, `doctor`, `deploy`, `clean`

```powershell
function Install-SqlServer {
    <#
    .SYNOPSIS
        Provisions Azure SQL Server and Serverless database
    #>
    [CmdletBinding()]
    param()

    $sqlServerName = "sql-$BaseName"
    $databaseName = "catan"

    Write-Log -Level "INFO" -Message "Creating SQL Server: $sqlServerName"

    # Create SQL Server with managed identity authentication
    Invoke-AzCommand @"
sql server create `
    --name $sqlServerName `
    --resource-group $ResourceGroup `
    --location $Location `
    --enable-ad-only-auth `
    --external-admin-principal-type User `
    --external-admin-name "$((az account show --query user.name -o tsv))" `
    --external-admin-sid "$((az ad signed-in-user show --query id -o tsv))"
"@

    # Allow Azure services to access
    Invoke-AzCommand @"
sql server firewall-rule create `
    --server $sqlServerName `
    --resource-group $ResourceGroup `
    --name AllowAzureServices `
    --start-ip-address 0.0.0.0 `
    --end-ip-address 0.0.0.0
"@

    # Create serverless database
    Write-Log -Level "INFO" -Message "Creating Serverless database: $databaseName"
    Invoke-AzCommand @"
sql db create `
    --server $sqlServerName `
    --resource-group $ResourceGroup `
    --name $databaseName `
    --compute-model Serverless `
    --edition GeneralPurpose `
    --family Gen5 `
    --min-capacity 0.5 `
    --capacity 2 `
    --auto-pause-delay 60 `
    --backup-storage-redundancy Local
"@

    # Grant GameService managed identity access
    $gameServicePrincipalId = Invoke-AzCommand @"
webapp identity show `
    --name $GameServiceName `
    --resource-group $ResourceGroup `
    --query principalId -o tsv
"@ -FailOnError $false

    if ($gameServicePrincipalId) {
        Write-Log -Level "INFO" -Message "Granting database access to GameService managed identity"

        # This requires running a SQL command - output instructions
        Write-Log -Level "WARN" -Message @"
Run this SQL command in the database to grant access:
CREATE USER [$GameServiceName] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [$GameServiceName];
ALTER ROLE db_datawriter ADD MEMBER [$GameServiceName];
ALTER ROLE db_ddladmin ADD MEMBER [$GameServiceName];
"@
    }

    # Configure connection string in App Service
    $connectionString = "Server=tcp:$sqlServerName.database.windows.net,1433;Database=$databaseName;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;"

    Write-Log -Level "INFO" -Message "Configuring connection string in App Service"
    Invoke-AzCommand @"
webapp config connection-string set `
    --name $GameServiceName `
    --resource-group $ResourceGroup `
    --connection-string-type SQLAzure `
    --settings AzureSql="$connectionString"
"@

    Write-Log -Level "SUCCESS" -Message "SQL Server provisioned: $sqlServerName.database.windows.net"
}

function Get-SqlServerHealth {
    <#
    .SYNOPSIS
        Checks SQL Server and database health
    #>
    [CmdletBinding()]
    param()

    $sqlServerName = "sql-$BaseName"
    $databaseName = "catan"

    # Check server exists
    $server = Invoke-AzCommand @"
sql server show `
    --name $sqlServerName `
    --resource-group $ResourceGroup `
    --query name -o tsv
"@ -FailOnError $false

    if (-not $server) {
        Write-Log -Level "ERROR" -Message "SQL Server not found: $sqlServerName"
        return $false
    }

    Write-Log -Level "SUCCESS" -Message "SQL Server exists: $sqlServerName"

    # Check database exists and status
    $dbStatus = Invoke-AzCommand @"
sql db show `
    --server $sqlServerName `
    --resource-group $ResourceGroup `
    --name $databaseName `
    --query status -o tsv
"@ -FailOnError $false

    if (-not $dbStatus) {
        Write-Log -Level "ERROR" -Message "Database not found: $databaseName"
        return $false
    }

    Write-Log -Level "SUCCESS" -Message "Database status: $dbStatus"

    # Check connection string configured
    $connStrings = Invoke-AzCommand @"
webapp config connection-string list `
    --name $GameServiceName `
    --resource-group $ResourceGroup `
    --query "[?name=='AzureSql'].value" -o tsv
"@ -FailOnError $false

    if ($connStrings) {
        Write-Log -Level "SUCCESS" -Message "Connection string configured in App Service"
    } else {
        Write-Log -Level "WARN" -Message "Connection string not configured in App Service"
    }

    return $true
}

function Remove-SqlServer {
    <#
    .SYNOPSIS
        Removes SQL Server and database
    #>
    [CmdletBinding()]
    param()

    $sqlServerName = "sql-$BaseName"

    Write-Log -Level "WARN" -Message "Deleting SQL Server: $sqlServerName (includes all databases)"

    Invoke-AzCommand @"
sql server delete `
    --name $sqlServerName `
    --resource-group $ResourceGroup `
    --yes
"@ -FailOnError $false

    Write-Log -Level "SUCCESS" -Message "SQL Server deleted"
}
```

### Step 2.2: Update Deploy Function

Update `Deploy-GameService` to run migrations on deploy:

```powershell
function Deploy-GameService {
    # ... existing publish logic ...

    # After deploy, trigger database migration via health endpoint
    Write-Log -Level "INFO" -Message "Verifying database migration..."

    $healthUrl = "https://$GameServiceName.azurewebsites.net/api/database/migrate"
    $response = Invoke-RestMethod -Uri $healthUrl -Method POST -ErrorAction SilentlyContinue

    if ($response.success) {
        Write-Log -Level "SUCCESS" -Message "Database migration complete"
    }
}
```

### Step 2.3: Update Noun ValidateSet

Add `sql` to the noun validation:

```powershell
[ValidateSet("ui", "database", "game-service", "sql", "help")]
[string]$Noun
```

---

## Phase 3: WebUI.ps1 Updates

### Step 3.1: Add SQL Mode Detection

**File:** `webui.ps1`

Update database commands to detect and handle SQL Server mode:

```powershell
function Get-DatabaseMode {
    # Check if SQL Server mode is explicitly set
    if ($env:DATABASE_PROVIDER -eq "SqlServer") {
        return "SqlServer"
    }

    # Check if Azure SQL connection string exists
    $configFile = Join-Path $ProjectRoot "Catan3.GameService" "appsettings.json"
    if (Test-Path $configFile) {
        $config = Get-Content $configFile | ConvertFrom-Json
        if ($config.ConnectionStrings.AzureSql) {
            return "SqlServer"
        }
    }

    return "SQLite"
}

# Update database doctor command
"doctor" {
    $mode = Get-DatabaseMode
    Write-Host "Database Mode: $mode" -ForegroundColor Cyan

    if ($mode -eq "SqlServer") {
        Write-Host "SQL Server mode - checking via API..." -ForegroundColor Yellow
        # Call health endpoint
    } else {
        # Existing SQLite checks
    }
}
```

---

## Phase 4: EF Core Migrations

### Step 4.1: Create Initial Migration for SQL Server

Run from project root:

```bash
# Add migration for SQL Server
cd Catan3.GameService
dotnet ef migrations add InitialSqlServer --context CatanDbContext -- --DATABASE_PROVIDER=SqlServer

# Generate SQL script for review
dotnet ef migrations script --context CatanDbContext --output ../migrations/initial-sqlserver.sql -- --DATABASE_PROVIDER=SqlServer
```

### Step 4.2: Add Migration Endpoint

**File:** `Catan3.GameService/Controllers/GameApiController.cs`

```csharp
/// <summary>
/// Applies pending migrations. Called automatically on deploy.
/// Only works when deployed to Azure (safety measure).
/// </summary>
[HttpPost("database/migrate")]
public async Task<IActionResult> MigrateDatabase()
{
    var detector = HttpContext.RequestServices.GetRequiredService<DatabaseProviderDetector>();

    if (!detector.IsAzure)
    {
        return BadRequest(new { error = "Migration endpoint only available in Azure" });
    }

    try
    {
        await _dbContext.Database.MigrateAsync();

        // Seed if empty
        if (!await _dbContext.Players.AnyAsync())
        {
            var defaultDataPath = Path.Combine(AppContext.BaseDirectory, "Default Data");
            await DatabaseSeeder.SeedAsync(_dbContext, defaultDataPath, null, useSqlServer: true);
        }

        return Ok(new {
            success = true,
            message = "Migration complete",
            playerCount = await _dbContext.Players.CountAsync(),
            gameCount = await _dbContext.GameSaveMetadata.CountAsync()
        });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { error = ex.Message });
    }
}
```

---

## Phase 5: Testing Plan

### Step 5.1: Local Testing with SQLite (Unchanged)

```bash
# Should work exactly as before
./webui.ps1 run
./webui.ps1 database doctor
```

### Step 5.2: Local Testing with SQL Server (Optional)

```bash
# Use LocalDB for local SQL Server testing
$env:DATABASE_PROVIDER = "SqlServer"
$env:ConnectionStrings__AzureSql = "Server=(localdb)\mssqllocaldb;Database=CatanTest;Trusted_Connection=True;"
./webui.ps1 run
```

### Step 5.3: Azure Testing

```bash
# Install SQL infrastructure
./webui.ps1 azure install   # Includes SQL Server now

# Deploy
./webui.ps1 azure deploy

# Verify
./webui.ps1 azure doctor    # Shows SQL Server status

# Check health endpoint
curl https://catan-api.azurewebsites.net/health
# Returns: { "database": { "provider": "SqlServer", "isAzure": true } }
```

---

## Phase 6: Rollback Plan

If issues arise, rollback is straightforward:

1. **Revert code changes** - Remove SQL Server provider code
2. **Restore SQLite** - App falls back to SQLite automatically
3. **Single instance** - Scale App Service to 1 instance as workaround

The SQLite code path remains fully functional throughout migration.

---

## Implementation Checklist

### GameService Changes

- [ ] Add `Microsoft.EntityFrameworkCore.SqlServer` package
- [ ] Create `DatabaseProviderDetector.cs`
- [ ] Update `Program.cs` with dual-provider registration
- [ ] Update `CatanDbContext.OnModelCreating()` with column sizes
- [ ] Update `DatabaseSeeder.cs` for SQL Server
- [ ] Add `/api/database/migrate` endpoint
- [ ] Update `/health` endpoint to show provider
- [ ] Create EF Core migration for SQL Server
- [ ] Test locally with SQLite (no regression)
- [ ] Test locally with LocalDB (optional)

### Script Changes

- [ ] Add `sql` noun to `catan-azure.ps1`
- [ ] Implement `Install-SqlServer` function
- [ ] Implement `Get-SqlServerHealth` function
- [ ] Implement `Remove-SqlServer` function
- [ ] Update `Deploy-GameService` to trigger migration
- [ ] Update `webui.ps1` database commands for dual-mode
- [ ] Update `./webui.ps1 azure install` to include SQL

### Azure Infrastructure

- [ ] Provision Azure SQL Server (managed identity auth)
- [ ] Create serverless database
- [ ] Configure firewall rules
- [ ] Grant GameService managed identity db access
- [ ] Configure connection string in App Service
- [ ] Run initial migration
- [ ] Seed database with default data
- [ ] Verify multi-instance behavior

### Documentation

- [ ] Update `.design/azure.md` with SQL Server details
- [ ] Update `CLAUDE.md` if needed
- [ ] Archive `azure-cosmos-dal.md` as superseded

---

## Cost Estimate

| Resource | Configuration | Estimated Cost |
|----------|---------------|----------------|
| SQL Server | Serverless Gen5, 0.5-2 vCores | ~$5-15/month (auto-pause) |
| Storage | 5 GB included | $0 |
| Backup | Local redundancy | Included |

**Total:** ~$5-15/month for typical hobby usage (mostly paused)

---

## Conclusion

Azure SQL Serverless eliminates the need for a complex DAL while providing:

- **Same familiar Entity Framework patterns**
- **Zero configuration for localhost development**
- **Automatic scaling and cost optimization**
- **Rich SQL querying capabilities**
- **Better tooling and debugging experience**

The CosmosDB + DAL approach was over-engineered for this use case. Azure SQL Serverless provides the perfect balance of simplicity, cost-effectiveness, and functionality.

<#
.SYNOPSIS
    Azure resource management for Catan3 application.
.DESCRIPTION
    Manages Azure resources for the Catan3 game service, including App Services
    and Azure SQL Serverless. Supports install, deploy, doctor, and clean operations.
.PARAMETER Noun
    The resource to operate on: ui, database, game-service
.PARAMETER Verb
    The operation to perform: install, deploy, doctor, clean
.PARAMETER Yes
    Skip confirmation prompts for destructive operations
.PARAMETER Json
    Output doctor results as JSON
.PARAMETER HashTable
    Output doctor results as PowerShell hashtable
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG)
.PARAMETER Help
    Display help information
.EXAMPLE
    ./catan-azure.ps1 game-service install
    Creates the GameService App Service and related resources
.EXAMPLE
    ./catan-azure.ps1 database deploy -TraceLevel DEBUG
    Configures SQL Server connection string with verbose output
.EXAMPLE
    ./catan-azure.ps1 ui doctor -Json
    Checks UI health and outputs JSON
#>

param(
    [Parameter(Position = 0)]
    [ValidateSet("ui", "database", "game-service", "github", "help", "doctor", "install", "deploy", "clean")]
    [string]$Noun,

    [Parameter(Position = 1)]
    [ValidateSet("install", "deploy", "deploy-staging", "deploy-staging-access", "doctor", "clean", "fix", "swap", "verify")]
    [string]$Verb,

    [Parameter()]
    [switch]$Yes,

    [Parameter()]
    [switch]$Json,

    [Parameter()]
    [switch]$HashTable,

    [Parameter()]
    [switch]$Perf,

    [Parameter()]
    [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
    [string]$TraceLevel = "INFO",

    [Parameter()]
    [switch]$Help,

    [Parameter()]
    [switch]$Force,

    [Parameter()]
    [switch]$NoBuild,

    [Parameter()]
    [string]$Slot,

    [Parameter()]
    [string]$GameServiceUrl,

    [Parameter()]
    [switch]$Staging
)

$ErrorActionPreference = "Stop"

# Import utility module for logging
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module "$ScriptDir/utility-scripts.psm1" -Force
Set-ModuleTraceLevel -TraceLevel $TraceLevel

# Set default TraceLevel for all Write-Log calls in this script
$PSDefaultParameterValues = @{
    'Write-Log:TraceLevel' = $TraceLevel
}

# Paths - script is in .scripts/, project root is parent
$ProjectRoot = Split-Path -Parent $ScriptDir
$AzureConfigDir = Join-Path $ProjectRoot ".azure"
$AzureConfigFile = Join-Path $AzureConfigDir "catan-azure.json"

# Detect invocation context for command hints in doctor output
# If called via catan.ps1, use "./catan.ps1 azure <noun> <verb>"
# If called directly, use ".scripts/catan-azure.ps1 <noun> <verb>"
$callerStack = Get-PSCallStack
$calledViaCatan = $callerStack | Where-Object { $_.ScriptName -like "*catan.ps1" -and $_.ScriptName -notlike "*catan-azure.ps1" }
if ($calledViaCatan) {
    $script:CmdHintPrefix = "./catan.ps1 azure"
} else {
    $script:CmdHintPrefix = ".scripts/catan-azure.ps1"
}

Write-Log -Level "DEBUG" -Message "Script directory: $ScriptDir"
Write-Log -Level "DEBUG" -Message "Project root: $ProjectRoot"
Write-Log -Level "DEBUG" -Message "Config file: $AzureConfigFile"

# Default Azure configuration
$DefaultConfig = @{
    baseName         = ""
    resourceGroup    = ""
    location         = "westus2"
    storageAccount   = ""
    storageContainer = "data"
    gameService      = @{
        appServicePlan = ""
        appName        = ""
        url            = ""
    }
    ui               = @{
        appName = ""
        url     = ""
    }
    appInsights      = @{
        name = ""
    }
    sqlServer        = @{
        serverName   = ""
        databaseName = "catan"
        fqdn         = ""
    }
}

# Invoke-AzCommand is provided by utility-scripts.psm1 (no local copy)

#region Configuration Functions
# Get-AzureConfig, Save-AzureConfig, Test-AzureLogin, Register-AzureProvider,
# Install-AzureResourceGroup, Remove-AzureResourceGroup, Install-AzureAppServicePlan,
# Install-AzureAppInsights, Get-GitCommitHash, Deploy-KuduZip, Test-DeploymentNeeded
# — all provided by utility-scripts.psm1

# Thin wrapper: returns config as hashtable (callers use hashtable syntax throughout)
# Falls back to $DefaultConfig when no config file exists yet (first install)
function Get-LocalConfig {
    $config = Get-AzureConfig -ProjectRoot $ProjectRoot -AsHashtable -AllowMissing
    if ($config) { return $config }
    return $DefaultConfig.Clone()
}

function Save-LocalConfig {
    param([hashtable]$Config)
    Save-AzureConfig -ProjectRoot $ProjectRoot -Config $Config
}

<#
.SYNOPSIS
    Checks if a web app name is available in Azure.
.DESCRIPTION
    Uses the Azure REST API to check if a given app name can be used
    for a new Azure Web App (*.azurewebsites.net).
.PARAMETER Name
    The web app name to check (without the .azurewebsites.net suffix)
.OUTPUTS
    Boolean - $true if available, $false if taken
#>
function Test-WebAppNameAvailable {
    param([string]$Name)

    $sub = Invoke-AzCommand "account show --query id -o tsv"
    $body = @{
        name = $Name
        type = "Microsoft.Web/sites"
    } | ConvertTo-Json

    $result = Invoke-AzCommand "rest --method post --url `"https://management.azure.com/subscriptions/$sub/providers/Microsoft.Web/checknameavailability?api-version=2023-12-01`" --body '$body'" -FailOnError $false -JsonOutput

    return $result.nameAvailable
}

<#
.SYNOPSIS
    Finds an available base name for Azure resources.
.DESCRIPTION
    Tries the preferred name first, then fallback names, checking availability.
    Returns an existing resource group's base name if found, or the first
    available name for new resources.
.PARAMETER PreferredName
    The preferred base name to try first (default: "catan")
.OUTPUTS
    String - The available base name to use
#>
function Get-AvailableBaseName {
    param([string]$PreferredName = "catan")

    # Build list of candidates - preferred names plus several random options
    $candidates = @($PreferredName, "catangame")
    for ($i = 0; $i -lt 5; $i++) {
        $candidates += "catan$(Get-Random -Maximum 9999)"
    }

    foreach ($name in $candidates) {
        # Check if resource group exists (indicates name is in use by us)
        $rgName = "rg-$name"
        $existing = Invoke-AzCommand "group show --name $rgName" -FailOnError $false -JsonOutput
        if ($existing) {
            Write-Log -Level "INFO" -Message "Found existing resources with base name: $name"
            return $name
        }

        # Check if both app names are available (ui and api)
        $uiAvailable = Test-WebAppNameAvailable -Name $name
        $apiAvailable = Test-WebAppNameAvailable -Name "$name-api"

        if ($uiAvailable -and $apiAvailable) {
            Write-Log -Level "INFO" -Message "Base name available: $name"
            return $name
        }

        Write-Log -Level "WARN" -Message "Name '$name' is not available, trying next..."
    }

    throw "Could not find an available base name for Azure resources after trying $($candidates.Count) options"
}

<#
.SYNOPSIS
    Creates a full configuration from a base name.
.DESCRIPTION
    Delegates to the module's Get-AzureResourceNames for naming conventions,
    then populates the hashtable config format used by this script.
.PARAMETER BaseName
    The base name to derive all resource names from
.OUTPUTS
    Hashtable containing complete Azure resource configuration
#>
function Initialize-ConfigFromBaseName {
    param([string]$BaseName)

    $az = Get-AzureResourceNames -ProjectRoot $ProjectRoot

    # Start from DefaultConfig to ensure all nested hashtables exist,
    # then overlay with values from the config file and derived names
    $config = $DefaultConfig.Clone()
    $fileConfig = Get-LocalConfig
    # Preserve non-derived fields from the file (e.g., auth)
    foreach ($key in $fileConfig.Keys) {
        $config[$key] = $fileConfig[$key]
    }

    $config.baseName = $BaseName
    $config.resourceGroup = $az.ResourceGroup
    $config.location = $az.Location
    $config.storageAccount = $az.StorageAccount
    $config.gameService = @{
        appServicePlan = $az.GameServicePlan
        appName        = $az.GameServiceAppName
        url            = $az.GameServiceUrl
    }
    $config.ui = @{
        appName = $az.UiAppName
        url     = $az.UiUrl
    }
    $config.appInsights = @{
        name = $az.AppInsights
    }

    return $config
}

#endregion

#region Azure Auth Functions

# Test-AzureLogin provided by utility-scripts.psm1

# Register-AzureProvider, Install-AzureResourceGroup, Remove-AzureResourceGroup
# provided by utility-scripts.psm1

#endregion

#region Application Insights Functions

# Install-AzureAppInsights provided by utility-scripts.psm1

#endregion

#region Database Functions (Azure SQL Serverless)

<#
.SYNOPSIS
    Creates Azure SQL Server and Serverless database.
.DESCRIPTION
    Creates an Azure SQL Server with Azure AD-only authentication,
    creates a serverless database, and configures firewall rules.
    Uses managed identity for GameService access.
.PARAMETER Config
    Azure configuration hashtable
.OUTPUTS
    Boolean - $true on success
#>
function Install-Database {
    param([hashtable]$Config)

    $rgName = $Config.resourceGroup
    $location = $Config.location
    $sqlServerName = $Config.sqlServer.serverName
    $databaseName = $Config.sqlServer.databaseName

    # Ensure resource group exists
    Install-AzureResourceGroup -ResourceGroup $Config.resourceGroup -Location $Config.location | Out-Null

    # Ensure Microsoft.Sql provider is registered
    Register-AzureProvider -Namespace "Microsoft.Sql"

    Write-Log -Level "INFO" -Message "Checking SQL Server: $sqlServerName"

    $existing = Invoke-AzCommand "sql server show --name $sqlServerName --resource-group $rgName" -FailOnError $false -JsonOutput
    if (-not $existing) {
        Write-Log -Level "INFO" -Message "Creating SQL Server: $sqlServerName"

        # Get current user info for admin
        $userEmail = Invoke-AzCommand "account show --query user.name -o tsv"
        $userId = Invoke-AzCommand "ad signed-in-user show --query id -o tsv"

        # Create SQL Server with Azure AD-only authentication
        Invoke-AzCommand "sql server create --name $sqlServerName --resource-group $rgName --location $location --enable-ad-only-auth --external-admin-principal-type User --external-admin-name `"$userEmail`" --external-admin-sid $userId" -SuppressOutput

        Write-Log -Level "INFO" -Message "SQL Server created: $sqlServerName"
    }
    else {
        Write-Log -Level "INFO" -Message "SQL Server exists: $sqlServerName"
    }

    # Enable public network access (required for Azure App Service to connect without VNet/Private Endpoint)
    Write-Log -Level "INFO" -Message "Checking public network access..."
    $serverInfo = Invoke-AzCommand "sql server show --name $sqlServerName --resource-group $rgName --query publicNetworkAccess -o tsv" -FailOnError $false
    if ($serverInfo -ne "Enabled") {
        Write-Log -Level "INFO" -Message "Enabling public network access for SQL Server..."
        Invoke-AzCommand "sql server update --name $sqlServerName --resource-group $rgName --enable-public-network true" -SuppressOutput
        Write-Log -Level "INFO" -Message "Public network access enabled"
    }
    else {
        Write-Log -Level "INFO" -Message "Public network access already enabled"
    }

    # Configure firewall to allow Azure services
    Write-Log -Level "INFO" -Message "Configuring firewall rules..."
    $fwExists = Invoke-AzCommand "sql server firewall-rule show --server $sqlServerName --resource-group $rgName --name AllowAzureServices" -FailOnError $false -JsonOutput
    if (-not $fwExists) {
        Invoke-AzCommand "sql server firewall-rule create --server $sqlServerName --resource-group $rgName --name AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0" -SuppressOutput
        Write-Log -Level "INFO" -Message "Firewall rule created: AllowAzureServices"
    }
    else {
        Write-Log -Level "INFO" -Message "Firewall rule AllowAzureServices already exists"
    }

    # Check if database exists
    Write-Log -Level "INFO" -Message "Checking database: $databaseName"
    $dbExists = Invoke-AzCommand "sql db show --server $sqlServerName --resource-group $rgName --name $databaseName" -FailOnError $false -JsonOutput
    if (-not $dbExists) {
        Write-Log -Level "INFO" -Message "Creating Serverless database: $databaseName"

        # Create serverless database with 12-hour auto-pause delay (720 minutes)
        # Short delays (60 min) cause 30-60 sec cold starts too often during normal use
        # 12 hours means it only pauses overnight, avoiding bad UX during the day
        Invoke-AzCommand "sql db create --server $sqlServerName --resource-group $rgName --name $databaseName --compute-model Serverless --edition GeneralPurpose --family Gen5 --min-capacity 0.5 --capacity 2 --auto-pause-delay 720 --backup-storage-redundancy Local" -SuppressOutput

        Write-Log -Level "INFO" -Message "Database created: $databaseName (Serverless, auto-pause after 12 hours)"
    }
    else {
        Write-Log -Level "INFO" -Message "Database exists: $databaseName"
    }

    Write-Log -Level "INFO" -Message "SQL Server ready: $($Config.sqlServer.fqdn)"

    # Grant SQL Server Contributor role to GameService managed identity
    # This allows the Troubleshoot feature to enable public network access and manage firewall rules
    $appName = $Config.gameService.appName
    $principalId = Invoke-AzCommand "webapp identity show --name $appName --resource-group $rgName --query principalId -o tsv" -FailOnError $false

    if ($principalId) {
        $subscriptionId = Invoke-AzCommand "account show --query id -o tsv"
        if (-not $subscriptionId) {
            throw "Failed to get subscription ID"
        }
        $sqlServerScope = "/subscriptions/$subscriptionId/resourceGroups/$rgName/providers/Microsoft.Sql/servers/$sqlServerName"

        Write-Log -Level "INFO" -Message "Granting SQL Server Contributor role to GameService managed identity..."
        $existingRole = Invoke-AzCommand "role assignment list --assignee $principalId --role 'SQL Server Contributor' --scope $sqlServerScope --query [0].id -o tsv" -FailOnError $false
        if (-not $existingRole) {
            Invoke-AzCommand "role assignment create --assignee-object-id $principalId --assignee-principal-type ServicePrincipal --role 'SQL Server Contributor' --scope $sqlServerScope" -SuppressOutput
            Write-Log -Level "INFO" -Message "SQL Server Contributor role granted"
        }
        else {
            Write-Log -Level "DEBUG" -Message "SQL Server Contributor role already assigned"
        }
    }
    else {
        Write-Log -Level "WARN" -Message "GameService managed identity not found - run 'game-service install' first for full Troubleshoot support"
    }

    return $true
}

<#
.SYNOPSIS
    Configures GameService to use Azure SQL Server.
.DESCRIPTION
    Creates connection string and configures it in the GameService App Service.
    Grants the GameService managed identity access to the database using Invoke-SqlCmd
    with an Azure AD access token from the current CLI login.
.PARAMETER Config
    Azure configuration hashtable
.OUTPUTS
    Boolean - $true on success
#>
function Deploy-Database {
    param(
        [hashtable]$Config,
        [bool]$Force = $false
    )

    $rgName = $Config.resourceGroup
    $sqlServerName = $Config.sqlServer.serverName
    $databaseName = $Config.sqlServer.databaseName
    $fqdn = $Config.sqlServer.fqdn
    $appName = $Config.gameService.appName

    # Run doctor to see what needs to be done
    $doctor = Get-DatabaseDoctor -Config $Config

    # If already fully connected and not forced, nothing to do
    if ($doctor.checks.gameServiceConnected -and -not $Force) {
        Write-Log -Level "INFO" -Message "Database already configured and connected - skipping"
        return $true
    }

    # Step 0: Ensure network access is configured (may have been disabled by policy or manual change)
    if (-not $doctor.checks.publicNetworkAccess) {
        Write-Log -Level "INFO" -Message "Enabling public network access for SQL Server..."
        Invoke-AzCommand "sql server update --name $sqlServerName --resource-group $rgName --enable-public-network true" -SuppressOutput
        Write-Log -Level "INFO" -Message "Public network access enabled"
    }

    if (-not $doctor.checks.firewallRule) {
        Write-Log -Level "INFO" -Message "Creating firewall rule: AllowAzureServices..."
        Invoke-AzCommand "sql server firewall-rule create --server $sqlServerName --resource-group $rgName --name AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0" -SuppressOutput
        Write-Log -Level "INFO" -Message "Firewall rule created"
    }

    # Step 1: Configure connection string (if not already configured or forced)
    if (-not $doctor.checks.connectionString -or $Force) {
        # Connection string with connection pooling settings:
        # - Pooling=True (default, explicit for clarity)
        # - Min Pool Size=1 (keep at least 1 connection warm)
        # - Max Pool Size=30 (reasonable for single-instance App Service)
        # - Connection Timeout=30 (wait up to 30s for connection from pool)
        $connectionString = "Server=tcp:$fqdn,1433;Database=$databaseName;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Pooling=True;Min Pool Size=1;Max Pool Size=30;"

        Write-Log -Level "INFO" -Message "Configuring SQL connection string in App Service..."
        Invoke-AzCommand "webapp config connection-string set --name $appName --resource-group $rgName --connection-string-type SQLAzure --settings AzureSql=`"$connectionString`"" -SuppressOutput
        Write-Log -Level "INFO" -Message "Connection string configured (with connection pooling)"
    }
    else {
        Write-Log -Level "INFO" -Message "Connection string already configured - skipping"
    }

    # Step 2: Grant managed identity permissions (if not already connected, meaning permissions might be missing)
    # We can only verify permissions by successful connection, so if not connected we try to grant
    if (-not $doctor.checks.gameServiceConnected -or $Force) {
        # Get GameService managed identity principal ID
        $principalId = Invoke-AzCommand "webapp identity show --name $appName --resource-group $rgName --query principalId -o tsv" -FailOnError $false

        if (-not $principalId) {
            Write-Log -Level "WARN" -Message "GameService managed identity not found. Run 'game-service install' first."
            return $true
        }

        # Grant managed identity access to the database using Invoke-SqlCmd
        Write-Log -Level "INFO" -Message "Granting database access to managed identity: $appName"

        # Install SqlServer module if not available
        if (-not (Get-Module -ListAvailable -Name SqlServer)) {
            Write-Log -Level "INFO" -Message "Installing SqlServer PowerShell module..."
            Install-Module -Name SqlServer -Scope CurrentUser -Force -AllowClobber
        }

        # Add firewall rule for current IP to execute SQL commands
        Write-Log -Level "INFO" -Message "Adding temporary firewall rule for deployment..."
        $myIp = (Invoke-WebRequest -Uri "https://api.ipify.org" -UseBasicParsing -TimeoutSec 10).Content.Trim()
        $fwRuleName = "DeployScript-$([guid]::NewGuid().ToString().Substring(0,8))"
        Invoke-AzCommand "sql server firewall-rule create --server $sqlServerName --resource-group $rgName --name $fwRuleName --start-ip-address $myIp --end-ip-address $myIp" -SuppressOutput

        try {
            # Get access token for Azure SQL using Azure CLI
            Write-Log -Level "DEBUG" -Message "Acquiring Azure AD access token for SQL..."
            $tokenJson = Invoke-AzCommand "account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv"
            $accessToken = $tokenJson.Trim()

            # SQL to create user and grant permissions (idempotent)
            $sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '$appName')
BEGIN
    CREATE USER [$appName] FROM EXTERNAL PROVIDER;
END

IF NOT EXISTS (SELECT 1 FROM sys.database_role_members rm
               JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
               JOIN sys.database_principals m ON rm.member_principal_id = m.principal_id
               WHERE r.name = 'db_datareader' AND m.name = '$appName')
BEGIN
    ALTER ROLE db_datareader ADD MEMBER [$appName];
END

IF NOT EXISTS (SELECT 1 FROM sys.database_role_members rm
               JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
               JOIN sys.database_principals m ON rm.member_principal_id = m.principal_id
               WHERE r.name = 'db_datawriter' AND m.name = '$appName')
BEGIN
    ALTER ROLE db_datawriter ADD MEMBER [$appName];
END

IF NOT EXISTS (SELECT 1 FROM sys.database_role_members rm
               JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
               JOIN sys.database_principals m ON rm.member_principal_id = m.principal_id
               WHERE r.name = 'db_ddladmin' AND m.name = '$appName')
BEGIN
    ALTER ROLE db_ddladmin ADD MEMBER [$appName];
END
"@

            Write-Log -Level "DEBUG" -Message "Executing SQL to grant permissions..."
            Import-Module SqlServer -ErrorAction Stop
            Invoke-Sqlcmd -ServerInstance $fqdn -Database $databaseName -AccessToken $accessToken -Query $sql -ErrorAction Stop

            Write-Log -Level "INFO" -Message "Database permissions granted to: $appName"
        }
        catch {
            Write-Log -Level "ERROR" -Message "Failed to grant database permissions: $($_.Exception.Message)"
            Write-Log -Level "WARN" -Message ""
            Write-Log -Level "WARN" -Message "Manual alternative - run this SQL as server admin:"
            Write-Log -Level "WARN" -Message "  CREATE USER [$appName] FROM EXTERNAL PROVIDER;"
            Write-Log -Level "WARN" -Message "  ALTER ROLE db_datareader ADD MEMBER [$appName];"
            Write-Log -Level "WARN" -Message "  ALTER ROLE db_datawriter ADD MEMBER [$appName];"
            Write-Log -Level "WARN" -Message "  ALTER ROLE db_ddladmin ADD MEMBER [$appName];"
        }
        finally {
            # Clean up temporary firewall rule
            Write-Log -Level "DEBUG" -Message "Removing temporary firewall rule..."
            Invoke-AzCommand "sql server firewall-rule delete --server $sqlServerName --resource-group $rgName --name $fwRuleName" -FailOnError $false -SuppressOutput
        }
    }
    else {
        Write-Log -Level "INFO" -Message "Database permissions already configured - skipping"
    }

    return $true
}

<#
.SYNOPSIS
    Grants database access to the GameService staging slot's managed identity.
.DESCRIPTION
    Deployment slots have their own managed identity with a different principal ID.
    This function grants db_datareader, db_datawriter, and db_ddladmin roles to the
    staging slot identity. Idempotent -- safe to run multiple times.
.PARAMETER Config
    Azure configuration hashtable
.OUTPUTS
    Boolean indicating success
#>
function Grant-StagingDatabaseAccess {
    param(
        [hashtable]$Config
    )

    $rgName = $Config.resourceGroup
    $sqlServerName = $Config.sqlServer.serverName
    $databaseName = $Config.sqlServer.databaseName
    $fqdn = $Config.sqlServer.fqdn
    $appName = $Config.gameService.appName

    # Ensure staging slot exists and has managed identity
    Write-Log -Level "INFO" -Message "Ensuring GameService staging slot has managed identity..."
    $identity = Invoke-AzCommand "webapp identity show --name $appName --resource-group $rgName --slot staging --query principalId -o tsv" -FailOnError $false
    if (-not $identity) {
        Write-Log -Level "INFO" -Message "Assigning managed identity to staging slot..."
        Invoke-AzCommand "webapp identity assign --name $appName --resource-group $rgName --slot staging" -SuppressOutput
    }

    # Install SqlServer module if not available
    if (-not (Get-Module -ListAvailable -Name SqlServer)) {
        Write-Log -Level "INFO" -Message "Installing SqlServer PowerShell module..."
        Install-Module -Name SqlServer -Scope CurrentUser -Force -AllowClobber
    }

    # Add firewall rule for current IP
    Write-Log -Level "INFO" -Message "Adding temporary firewall rule for staging DB access..."
    $myIp = (Invoke-WebRequest -Uri "https://api.ipify.org" -UseBasicParsing -TimeoutSec 10).Content.Trim()
    $fwRuleName = "StagingAccess-$([guid]::NewGuid().ToString().Substring(0,8))"
    Invoke-AzCommand "sql server firewall-rule create --server $sqlServerName --resource-group $rgName --name $fwRuleName --start-ip-address $myIp --end-ip-address $myIp" -SuppressOutput

    try {
        # Get access token
        Write-Log -Level "DEBUG" -Message "Acquiring Azure AD access token for SQL..."
        $accessToken = (Invoke-AzCommand "account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv").Trim()

        # The staging slot identity name is 'appName/slots/staging'
        $slotIdentityName = "$appName/slots/staging"

        $sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '$slotIdentityName')
BEGIN
    CREATE USER [$slotIdentityName] FROM EXTERNAL PROVIDER;
END

IF NOT EXISTS (SELECT 1 FROM sys.database_role_members rm
               JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
               JOIN sys.database_principals m ON rm.member_principal_id = m.principal_id
               WHERE r.name = 'db_datareader' AND m.name = '$slotIdentityName')
BEGIN
    ALTER ROLE db_datareader ADD MEMBER [$slotIdentityName];
END

IF NOT EXISTS (SELECT 1 FROM sys.database_role_members rm
               JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
               JOIN sys.database_principals m ON rm.member_principal_id = m.principal_id
               WHERE r.name = 'db_datawriter' AND m.name = '$slotIdentityName')
BEGIN
    ALTER ROLE db_datawriter ADD MEMBER [$slotIdentityName];
END

IF NOT EXISTS (SELECT 1 FROM sys.database_role_members rm
               JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
               JOIN sys.database_principals m ON rm.member_principal_id = m.principal_id
               WHERE r.name = 'db_ddladmin' AND m.name = '$slotIdentityName')
BEGIN
    ALTER ROLE db_ddladmin ADD MEMBER [$slotIdentityName];
END
"@

        Write-Log -Level "INFO" -Message "Granting database access to staging identity: $slotIdentityName"
        Import-Module SqlServer -ErrorAction Stop
        Invoke-Sqlcmd -ServerInstance $fqdn -Database $databaseName -AccessToken $accessToken -Query $sql -ErrorAction Stop

        Write-Log -Level "INFO" -Message "Database permissions granted to staging slot"
    }
    catch {
        Write-Log -Level "ERROR" -Message "Failed to grant staging DB access: $($_.Exception.Message)"
        Write-Log -Level "WARN" -Message ""
        Write-Log -Level "WARN" -Message "Manual alternative - run this SQL as server admin:"
        Write-Log -Level "WARN" -Message "  CREATE USER [$slotIdentityName] FROM EXTERNAL PROVIDER;"
        Write-Log -Level "WARN" -Message "  ALTER ROLE db_datareader ADD MEMBER [$slotIdentityName];"
        Write-Log -Level "WARN" -Message "  ALTER ROLE db_datawriter ADD MEMBER [$slotIdentityName];"
        Write-Log -Level "WARN" -Message "  ALTER ROLE db_ddladmin ADD MEMBER [$slotIdentityName];"
        # Don't fail the whole deploy for this -- the service may still work with inherited permissions
    }
    finally {
        Write-Log -Level "DEBUG" -Message "Removing temporary firewall rule..."
        Invoke-AzCommand "sql server firewall-rule delete --server $sqlServerName --resource-group $rgName --name $fwRuleName" -FailOnError $false -SuppressOutput
    }

    return $true
}

<#
.SYNOPSIS
    Fixes common Azure SQL connectivity issues.
.DESCRIPTION
    Checks and fixes:
    - Public Network Access (enables if disabled)
    - AllowAzureServices firewall rule (creates if missing)
    This is useful when Azure Policy or automation has reverted settings.
.PARAMETER Config
    Azure configuration hashtable
.OUTPUTS
    Boolean indicating success
#>
function Fix-Database {
    param(
        [hashtable]$Config
    )

    $rgName = $Config.resourceGroup
    $sqlServerName = $Config.sqlServer.serverName
    $fqdn = $Config.sqlServer.fqdn

    Write-Log -Level "INFO" -Message "Checking Azure SQL settings..."

    # Check if SQL Server exists
    $serverExists = Invoke-AzCommand "sql server show --name $sqlServerName --resource-group $rgName" -FailOnError $false -SuppressOutput
    if (-not $serverExists) {
        Write-Log -Level "ERROR" -Message "SQL Server '$sqlServerName' not found in resource group '$rgName'"
        return $false
    }

    $fixedCount = 0

    # Check and fix Public Network Access
    $publicAccess = Invoke-AzCommand "sql server show --name $sqlServerName --resource-group $rgName --query publicNetworkAccess -o tsv" -FailOnError $false
    if ($publicAccess -ne "Enabled") {
        Write-Log -Level "WARN" -Message "Public Network Access is disabled - fixing..."
        Invoke-AzCommand "sql server update --name $sqlServerName --resource-group $rgName --enable-public-network true" -SuppressOutput
        Write-Log -Level "INFO" -Message "Public Network Access enabled"
        $fixedCount++
    }
    else {
        Write-Log -Level "INFO" -Message "Public Network Access: OK"
    }

    # Check and fix AllowAzureServices firewall rule
    $fwRule = Invoke-AzCommand "sql server firewall-rule show --server $sqlServerName --resource-group $rgName --name AllowAzureServices" -FailOnError $false -JsonOutput
    if (-not $fwRule) {
        Write-Log -Level "WARN" -Message "AllowAzureServices firewall rule is missing - creating..."
        Invoke-AzCommand "sql server firewall-rule create --server $sqlServerName --resource-group $rgName --name AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0" -SuppressOutput
        Write-Log -Level "INFO" -Message "AllowAzureServices firewall rule created"
        $fixedCount++
    }
    else {
        Write-Log -Level "INFO" -Message "AllowAzureServices firewall rule: OK"
    }

    # Check and fix schema (missing tables)
    Write-Log -Level "INFO" -Message "Checking database schema..."
    $schemaCheck = Test-DatabaseSchema -Config $Config -TraceLevel "INFO"

    if ($schemaCheck.checked) {
        if ($schemaCheck.schemaValid) {
            Write-Log -Level "INFO" -Message "Database schema: OK (all $($schemaCheck.existingTables.Count) tables exist)"
        }
        else {
            Write-Log -Level "WARN" -Message "Missing tables: $($schemaCheck.missingTables -join ', ')"
            Write-Log -Level "INFO" -Message "Creating missing tables..."

            $repairResult = Repair-DatabaseSchema -Config $Config -MissingTables $schemaCheck.missingTables -TraceLevel "INFO"

            if ($repairResult.success) {
                Write-Log -Level "INFO" -Message "Created tables: $($repairResult.tablesCreated -join ', ')"
                $fixedCount += $repairResult.tablesCreated.Count
            }
            else {
                Write-Log -Level "ERROR" -Message "Failed to create some tables"
                foreach ($err in $repairResult.errors) {
                    Write-Log -Level "ERROR" -Message "  $err"
                }
                return $false
            }
        }
    }
    else {
        Write-Log -Level "WARN" -Message "Could not check schema: $($schemaCheck.error)"
    }

    # Summary
    if ($fixedCount -gt 0) {
        Write-Log -Level "INFO" -Message "Fixed $fixedCount issue(s)"
    }
    else {
        Write-Log -Level "INFO" -Message "No issues found - all settings are correct"
    }

    return $true
}

<#
.SYNOPSIS
    Directly checks Azure SQL database schema for required tables.
.DESCRIPTION
    Connects to Azure SQL using Azure AD authentication and queries
    INFORMATION_SCHEMA.TABLES to verify all required tables exist.
    This does NOT rely on the GameService health endpoint.
.PARAMETER Config
    Azure configuration hashtable
.PARAMETER TraceLevel
    Logging level
.OUTPUTS
    Hashtable with schemaValid, missingTables, existingTables, error
#>
function Test-DatabaseSchema {
    param(
        [hashtable]$Config,
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    $fqdn = $Config.sqlServer.fqdn
    $databaseName = $Config.sqlServer.databaseName
    $sqlServerName = $Config.sqlServer.serverName
    $rgName = $Config.resourceGroup

    $result = @{
        schemaValid = $false
        missingTables = @()
        existingTables = @()
        error = $null
        checked = $false
    }

    # Required tables for the application
    $requiredTables = @("Players", "Images", "GameSaveMetadata", "GameSaveData", "CompletedGames", "Recordings")

    try {
        Write-Log -Level "DEBUG" -Message "Checking database schema directly via Azure SQL..." -TraceLevel $TraceLevel

        # Install SqlServer module if not available
        if (-not (Get-Module -ListAvailable -Name SqlServer)) {
            Write-Log -Level "DEBUG" -Message "SqlServer module not found, skipping direct schema check" -TraceLevel $TraceLevel
            $result.error = "SqlServer PowerShell module not installed"
            return $result
        }

        # Ensure public network access is enabled before creating firewall rules
        $publicAccess = Invoke-AzCommand "sql server show --name $sqlServerName --resource-group $rgName --query publicNetworkAccess -o tsv" -FailOnError $false
        if ($publicAccess -ne "Enabled") {
            Write-Log -Level "INFO" -Message "Enabling public network access for SQL Server..." -TraceLevel $TraceLevel
            Invoke-AzCommand "sql server update --name $sqlServerName --resource-group $rgName --enable-public-network true" -SuppressOutput
            Write-Log -Level "INFO" -Message "Public network access enabled" -TraceLevel $TraceLevel
        }

        # Add temporary firewall rule for current IP
        $myIp = (Invoke-WebRequest -Uri "https://api.ipify.org" -UseBasicParsing -TimeoutSec 10).Content.Trim()
        $fwRuleName = "SchemaCheck-$([guid]::NewGuid().ToString().Substring(0,8))"
        Write-Log -Level "DEBUG" -Message "Adding temporary firewall rule for $myIp..." -TraceLevel $TraceLevel
        Invoke-AzCommand "sql server firewall-rule create --server $sqlServerName --resource-group $rgName --name $fwRuleName --start-ip-address $myIp --end-ip-address $myIp" -SuppressOutput

        try {
            # Get access token for Azure SQL
            Write-Log -Level "DEBUG" -Message "Acquiring Azure AD access token..." -TraceLevel $TraceLevel
            $accessToken = (Invoke-AzCommand "account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv").Trim()

            # Query for existing tables
            $sql = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'"

            Write-Log -Level "DEBUG" -Message "Querying database tables..." -TraceLevel $TraceLevel
            Import-Module SqlServer -ErrorAction Stop
            $tables = Invoke-Sqlcmd -ServerInstance $fqdn -Database $databaseName -AccessToken $accessToken -Query $sql -ErrorAction Stop

            $existingTableNames = @($tables | ForEach-Object { $_.TABLE_NAME })
            $result.existingTables = $existingTableNames
            $result.checked = $true

            # Check which required tables are missing
            foreach ($table in $requiredTables) {
                if ($existingTableNames -contains $table) {
                    Write-Log -Level "DEBUG" -Message "Table '$table': EXISTS" -TraceLevel $TraceLevel
                }
                else {
                    Write-Log -Level "DEBUG" -Message "Table '$table': MISSING" -TraceLevel $TraceLevel
                    $result.missingTables += $table
                }
            }

            $result.schemaValid = ($result.missingTables.Count -eq 0)

            if ($result.schemaValid) {
                Write-Log -Level "DEBUG" -Message "Schema check: All $($requiredTables.Count) required tables exist" -TraceLevel $TraceLevel
            }
            else {
                Write-Log -Level "DEBUG" -Message "Schema check: Missing $($result.missingTables.Count) table(s): $($result.missingTables -join ', ')" -TraceLevel $TraceLevel
            }
        }
        finally {
            # Clean up temporary firewall rule
            Write-Log -Level "DEBUG" -Message "Removing temporary firewall rule..." -TraceLevel $TraceLevel
            Invoke-AzCommand "sql server firewall-rule delete --server $sqlServerName --resource-group $rgName --name $fwRuleName" -FailOnError $false -SuppressOutput
        }
    }
    catch {
        $result.error = $_.Exception.Message
        Write-Log -Level "DEBUG" -Message "Schema check failed: $($_.Exception.Message)" -TraceLevel $TraceLevel
    }

    return $result
}

<#
.SYNOPSIS
    Creates missing database tables directly in Azure SQL.
.DESCRIPTION
    Connects to Azure SQL using Azure AD authentication and creates
    any missing required tables. This does NOT rely on the GameService.
.PARAMETER Config
    Azure configuration hashtable
.PARAMETER MissingTables
    Array of table names to create (from Test-DatabaseSchema result)
.PARAMETER TraceLevel
    Logging level
.OUTPUTS
    Hashtable with success, tablesCreated, errors
#>
function Repair-DatabaseSchema {
    param(
        [hashtable]$Config,
        [string[]]$MissingTables,
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "INFO"
    )

    $fqdn = $Config.sqlServer.fqdn
    $databaseName = $Config.sqlServer.databaseName
    $sqlServerName = $Config.sqlServer.serverName
    $rgName = $Config.resourceGroup

    $result = @{
        success = $false
        tablesCreated = @()
        errors = @()
    }

    if (-not $MissingTables -or $MissingTables.Count -eq 0) {
        $result.success = $true
        return $result
    }

    # SQL CREATE TABLE statements for each table
    $tableDefinitions = @{
        "Players" = @"
CREATE TABLE [Players] (
    [Id] NVARCHAR(255) NOT NULL,
    [Data] NVARCHAR(MAX) NOT NULL,
    CONSTRAINT [PK_Players] PRIMARY KEY ([Id])
)
"@
        "Images" = @"
CREATE TABLE [Images] (
    [Id] NVARCHAR(255) NOT NULL,
    [ContentType] NVARCHAR(100) NOT NULL,
    [Data] VARBINARY(MAX) NOT NULL,
    CONSTRAINT [PK_Images] PRIMARY KEY ([Id])
)
"@
        "GameSaveData" = @"
CREATE TABLE [GameSaveData] (
    [Id] INT NOT NULL IDENTITY(1,1),
    [CompressedData] VARBINARY(MAX) NOT NULL,
    [Size] INT NOT NULL,
    CONSTRAINT [PK_GameSaveData] PRIMARY KEY ([Id])
)
"@
        "GameSaveMetadata" = @"
CREATE TABLE [GameSaveMetadata] (
    [Id] INT NOT NULL IDENTITY(1,1),
    [GameId] NVARCHAR(255) NULL,
    [StartedBy] NVARCHAR(255) NULL,
    [SavedAt] DATETIME2 NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [GameState] NVARCHAR(50) NULL,
    [GameType] NVARCHAR(50) NULL,
    [PlayerCount] INT NOT NULL,
    [PlayerNames] NVARCHAR(500) NULL,
    [TurnCount] INT NOT NULL,
    [GameName] NVARCHAR(255) NULL,
    [GameDataId] INT NOT NULL,
    CONSTRAINT [PK_GameSaveMetadata] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_GameSaveMetadata_GameSaveData_GameDataId] FOREIGN KEY ([GameDataId]) REFERENCES [GameSaveData] ([Id]) ON DELETE CASCADE
);
CREATE UNIQUE INDEX [IX_GameSaveMetadata_GameId] ON [GameSaveMetadata] ([GameId]);
CREATE INDEX [IX_GameSaveMetadata_StartedBy] ON [GameSaveMetadata] ([StartedBy]);
CREATE INDEX [IX_GameSaveMetadata_GameState] ON [GameSaveMetadata] ([GameState]);
CREATE INDEX [IX_GameSaveMetadata_SavedAt] ON [GameSaveMetadata] ([SavedAt])
"@
        "CompletedGames" = @"
CREATE TABLE [CompletedGames] (
    [Id] INT NOT NULL IDENTITY(1,1),
    [GameId] NVARCHAR(255) NOT NULL,
    [GameName] NVARCHAR(255) NOT NULL,
    [WinnerId] NVARCHAR(255) NOT NULL,
    [WinnerName] NVARCHAR(255) NOT NULL,
    [CompletedAt] DATETIME2 NOT NULL,
    [StartedAt] DATETIME2 NOT NULL,
    [PlayerCount] INT NOT NULL,
    [PlayerNames] NVARCHAR(500) NOT NULL,
    [TurnCount] INT NOT NULL,
    [CompressedData] VARBINARY(MAX) NOT NULL,
    [Size] INT NOT NULL,
    CONSTRAINT [PK_CompletedGames] PRIMARY KEY ([Id])
);
CREATE INDEX [IX_CompletedGames_GameId] ON [CompletedGames] ([GameId]);
CREATE INDEX [IX_CompletedGames_WinnerId] ON [CompletedGames] ([WinnerId]);
CREATE INDEX [IX_CompletedGames_CompletedAt] ON [CompletedGames] ([CompletedAt])
"@
        "Recordings" = @"
CREATE TABLE [Recordings] (
    [Id] NVARCHAR(255) NOT NULL,
    [Name] NVARCHAR(255) NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [GameType] NVARCHAR(50) NOT NULL,
    [PlayerCount] INT NOT NULL,
    [PlayerIds] NVARCHAR(500) NOT NULL,
    [ActionCount] INT NOT NULL,
    [Data] NVARCHAR(MAX) NOT NULL,
    CONSTRAINT [PK_Recordings] PRIMARY KEY ([Id])
);
CREATE INDEX [IX_Recordings_Name] ON [Recordings] ([Name]);
CREATE INDEX [IX_Recordings_CreatedAt] ON [Recordings] ([CreatedAt])
"@
    }

    try {
        Write-Log -Level "INFO" -Message "Creating $($MissingTables.Count) missing table(s) in Azure SQL..." -TraceLevel $TraceLevel

        # Install SqlServer module if not available
        if (-not (Get-Module -ListAvailable -Name SqlServer)) {
            Write-Log -Level "INFO" -Message "Installing SqlServer PowerShell module..." -TraceLevel $TraceLevel
            Install-Module -Name SqlServer -Scope CurrentUser -Force -AllowClobber
        }

        # Ensure public network access is enabled before creating firewall rules
        $publicAccess = Invoke-AzCommand "sql server show --name $sqlServerName --resource-group $rgName --query publicNetworkAccess -o tsv" -FailOnError $false
        if ($publicAccess -ne "Enabled") {
            Write-Log -Level "INFO" -Message "Enabling public network access for SQL Server..." -TraceLevel $TraceLevel
            Invoke-AzCommand "sql server update --name $sqlServerName --resource-group $rgName --enable-public-network true" -SuppressOutput
            Write-Log -Level "INFO" -Message "Public network access enabled" -TraceLevel $TraceLevel
        }

        # Add temporary firewall rule for current IP
        $myIp = (Invoke-WebRequest -Uri "https://api.ipify.org" -UseBasicParsing -TimeoutSec 10).Content.Trim()
        $fwRuleName = "SchemaMigrate-$([guid]::NewGuid().ToString().Substring(0,8))"
        Write-Log -Level "DEBUG" -Message "Adding temporary firewall rule for $myIp..." -TraceLevel $TraceLevel
        Invoke-AzCommand "sql server firewall-rule create --server $sqlServerName --resource-group $rgName --name $fwRuleName --start-ip-address $myIp --end-ip-address $myIp" -SuppressOutput

        try {
            # Get access token for Azure SQL
            Write-Log -Level "DEBUG" -Message "Acquiring Azure AD access token..." -TraceLevel $TraceLevel
            $accessToken = (Invoke-AzCommand "account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv").Trim()

            Import-Module SqlServer -ErrorAction Stop

            # Create each missing table
            foreach ($tableName in $MissingTables) {
                if (-not $tableDefinitions.ContainsKey($tableName)) {
                    $result.errors += "Unknown table: $tableName"
                    Write-Log -Level "WARN" -Message "Unknown table '$tableName' - skipping" -TraceLevel $TraceLevel
                    continue
                }

                $sql = $tableDefinitions[$tableName]
                Write-Log -Level "INFO" -Message "Creating table '$tableName'..." -TraceLevel $TraceLevel

                try {
                    # Split on semicolons to handle CREATE TABLE + CREATE INDEX
                    $statements = $sql -split ';' | Where-Object { $_.Trim() -ne '' }
                    foreach ($statement in $statements) {
                        Invoke-Sqlcmd -ServerInstance $fqdn -Database $databaseName -AccessToken $accessToken -Query $statement.Trim() -ErrorAction Stop
                    }
                    $result.tablesCreated += $tableName
                    Write-Log -Level "INFO" -Message "Created table '$tableName'" -TraceLevel $TraceLevel
                }
                catch {
                    $result.errors += "Failed to create '$tableName': $($_.Exception.Message)"
                    Write-Log -Level "ERROR" -Message "Failed to create table '$tableName': $($_.Exception.Message)" -TraceLevel $TraceLevel
                }
            }

            $result.success = ($result.errors.Count -eq 0)

            # Seed default recordings if Recordings table was just created
            if ($result.tablesCreated -contains "Recordings") {
                Write-Log -Level "INFO" -Message "Seeding default recordings..." -TraceLevel $TraceLevel
                $scriptDir = Split-Path -Parent $PSScriptRoot
                $recordingsPath = Join-Path $scriptDir "Catan3.GameService" "Default Data" "Recordings"

                if (Test-Path $recordingsPath) {
                    $recordingFiles = Get-ChildItem -Path $recordingsPath -Filter "*.json"
                    foreach ($file in $recordingFiles) {
                        try {
                            $recording = Get-Content $file.FullName -Raw | ConvertFrom-Json

                            # Escape single quotes in the data
                            $escapedData = $recording.data -replace "'", "''"
                            $escapedName = $recording.name -replace "'", "''"
                            $escapedPlayerIds = $recording.playerIds -replace "'", "''"
                            $escapedGameType = $recording.gameType -replace "'", "''"

                            $insertSql = @"
INSERT INTO [Recordings] ([Id], [Name], [CreatedAt], [GameType], [PlayerCount], [PlayerIds], [ActionCount], [Data])
VALUES ('$($recording.id)', '$escapedName', '$($recording.createdAt.ToString("yyyy-MM-ddTHH:mm:ss.fff"))', '$escapedGameType', $($recording.playerCount), '$escapedPlayerIds', $($recording.actionCount), '$escapedData')
"@
                            Invoke-Sqlcmd -ServerInstance $fqdn -Database $databaseName -AccessToken $accessToken -Query $insertSql -ErrorAction Stop
                            Write-Log -Level "INFO" -Message "Seeded recording: $($recording.name)" -TraceLevel $TraceLevel
                        }
                        catch {
                            Write-Log -Level "WARN" -Message "Failed to seed recording $($file.Name): $($_.Exception.Message)" -TraceLevel $TraceLevel
                        }
                    }
                }
                else {
                    Write-Log -Level "DEBUG" -Message "No recordings folder found at $recordingsPath" -TraceLevel $TraceLevel
                }
            }
        }
        finally {
            # Clean up temporary firewall rule
            Write-Log -Level "DEBUG" -Message "Removing temporary firewall rule..." -TraceLevel $TraceLevel
            Invoke-AzCommand "sql server firewall-rule delete --server $sqlServerName --resource-group $rgName --name $fwRuleName" -FailOnError $false -SuppressOutput
        }
    }
    catch {
        $result.errors += $_.Exception.Message
        Write-Log -Level "ERROR" -Message "Schema repair failed: $($_.Exception.Message)" -TraceLevel $TraceLevel
    }

    return $result
}

<#
.SYNOPSIS
    Checks health of the Azure SQL Server and database.
.DESCRIPTION
    Verifies SQL Server exists, database is online, and checks GameService
    health endpoint for database connectivity.
.PARAMETER Config
    Azure configuration hashtable
.OUTPUTS
    Hashtable with status, healthy flag, and diagnostic details
#>
function Get-DatabaseDoctor {
    param(
        [hashtable]$Config,
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    $rgName = $Config.resourceGroup
    $sqlServerName = $Config.sqlServer.serverName
    $databaseName = $Config.sqlServer.databaseName
    $gameServiceUrl = $Config.gameService.url
    $gameServiceAppName = $Config.gameService.appName
    $fqdn = $Config.sqlServer.fqdn

    Write-Log -Level "DEBUG" -Message "Get-DatabaseDoctor started" -TraceLevel $TraceLevel

    $result = @{
        resource     = "database"
        name         = "$sqlServerName/$databaseName"
        serverName   = $sqlServerName
        databaseName = $databaseName
        fqdn         = $fqdn
        status       = "unknown"
        healthy      = $false
        dbStatus     = "unknown"
        timestamp    = (Get-Date -Format "o")
        # Detailed checks for each install/deploy step
        checks       = @{
            resourceGroup        = $false
            sqlServer            = $false
            publicNetworkAccess  = $false
            firewallRule         = $false
            database             = $false
            connectionString     = $false
            connectionPooling    = $false
            managedIdentityUser  = $false
            gameServiceConnected = $false
            schemaValid          = $false
        }
        # What actions are needed
        needsInstall = $false
        needsDeploy  = $false
        needsFix     = $false  # For network settings that can be fixed without full install
    }

    try {
        # Check resource group exists
        Write-Log -Level "DEBUG" -Message "Checking resource group: $rgName" -TraceLevel $TraceLevel
        $rg = Invoke-AzCommand "group show --name $rgName" -FailOnError $false -JsonOutput
        $result.checks.resourceGroup = ($null -ne $rg)

        # Check SQL Server exists
        Write-Log -Level "DEBUG" -Message "Checking SQL server: $sqlServerName" -TraceLevel $TraceLevel
        $server = Invoke-AzCommand "sql server show --name $sqlServerName --resource-group $rgName" -FailOnError $false -JsonOutput
        if (-not $server) {
            $result.status = "server-not-found"
            $result.needsInstall = $true
            Write-Log -Level "DEBUG" -Message "SQL server not found, needsInstall=true" -TraceLevel $TraceLevel
            return $result
        }
        $result.checks.sqlServer = $true

        # Check public network access (required for App Service to connect without VNet)
        Write-Log -Level "DEBUG" -Message "Checking public network access" -TraceLevel $TraceLevel
        $publicAccess = Invoke-AzCommand "sql server show --name $sqlServerName --resource-group $rgName --query publicNetworkAccess -o tsv" -FailOnError $false
        $result.checks.publicNetworkAccess = ($publicAccess -eq "Enabled")
        if (-not $result.checks.publicNetworkAccess) {
            $result.needsFix = $true  # Can be fixed without full install
        }

        # Check firewall rule exists
        Write-Log -Level "DEBUG" -Message "Checking firewall rule" -TraceLevel $TraceLevel
        $fwRule = Invoke-AzCommand "sql server firewall-rule show --server $sqlServerName --resource-group $rgName --name AllowAzureServices" -FailOnError $false -JsonOutput
        $result.checks.firewallRule = ($null -ne $fwRule)
        if (-not $result.checks.firewallRule) {
            $result.needsFix = $true  # Can be fixed without full install
        }

        # Check database exists and status
        Write-Log -Level "DEBUG" -Message "Checking database: $databaseName" -TraceLevel $TraceLevel
        $db = Invoke-AzCommand "sql db show --server $sqlServerName --resource-group $rgName --name $databaseName" -FailOnError $false -JsonOutput
        if (-not $db) {
            $result.status = "database-not-found"
            $result.needsInstall = $true
            Write-Log -Level "DEBUG" -Message "Database not found, needsInstall=true" -TraceLevel $TraceLevel
            return $result
        }
        $result.checks.database = $true
        $result.dbStatus = $db.status

        # Check if database is online (may be paused)
        if ($db.status -eq "Online") {
            $result.status = "online"
        }
        elseif ($db.status -eq "Paused") {
            $result.status = "paused"
            $result.note = "Database is paused (auto-pause). Will resume on first connection."
        }
        else {
            $result.status = $db.status.ToLower()
        }

        # Check connection string from Azure (not from GameService)
        # CLI returns an array of objects with name/value/type, not a keyed object
        Write-Log -Level "DEBUG" -Message "Checking connection string configuration" -TraceLevel $TraceLevel
        $connStrings = Invoke-AzCommand "webapp config connection-string list --name $gameServiceAppName --resource-group $rgName" -FailOnError $false -JsonOutput
        $azureSqlConn = if ($connStrings) { $connStrings | Where-Object { $_.name -eq 'AzureSql' } | Select-Object -First 1 } else { $null }
        $result.checks.connectionString = ($null -ne $azureSqlConn)

        # Check if connection pooling is configured in connection string
        if ($azureSqlConn) {
            $connStr = $azureSqlConn.value
            $result.checks.connectionPooling = ($connStr -match "Pooling=True" -or $connStr -match "Min Pool Size")
        }

        # Check if managed identity user exists by testing GameService health endpoint
        # This is a quick check - if GameService responds with database connected, the MI user works
        Write-Log -Level "DEBUG" -Message "Checking GameService database connection" -TraceLevel $TraceLevel
        try {
            $health = Invoke-RestMethod -Uri "$gameServiceUrl/health?checkDatabase=true" -TimeoutSec 60
            if ($health.databaseDiagnostics -and $health.databaseDiagnostics.connected -eq $true) {
                $result.checks.gameServiceConnected = $true
                $result.checks.managedIdentityUser = $true
                # A successful connection proves the DB is online — the Azure control-plane
                # status (queried earlier) may still show "Paused" due to lag or because the
                # health check itself woke the database.
                if ($result.dbStatus -eq "Paused") {
                    $result.dbStatus = "Online"
                    $result.note = $null
                }
            }
            else {
                $result.checks.gameServiceConnected = $false
            }
        }
        catch {
            $result.checks.gameServiceConnected = $false
            Write-Log -Level "DEBUG" -Message "GameService health check failed: $_" -TraceLevel $TraceLevel
        }

        # Check schema DIRECTLY from Azure SQL (not via GameService)
        # This is the authoritative check - doesn't depend on deployed GameService code
        Write-Log -Level "DEBUG" -Message "Checking database schema directly" -TraceLevel $TraceLevel
        $schemaCheck = Test-DatabaseSchema -Config $Config -TraceLevel $TraceLevel

        if ($schemaCheck.checked) {
            $result.checks.schemaValid = $schemaCheck.schemaValid
            if (-not $schemaCheck.schemaValid) {
                $result.status = "schema-missing"
                $result.healthy = $false
                $result.needsDeploy = $true
                $result.missingTables = $schemaCheck.missingTables
                $result.note = "Missing tables: $($schemaCheck.missingTables -join ', '). Run './catan.ps1 azure deploy' to fix."
            }
        }
        elseif ($schemaCheck.error) {
            # Schema check failed - might be because database is paused
            Write-Log -Level "DEBUG" -Message "Schema check failed: $($schemaCheck.error)" -TraceLevel $TraceLevel
            if ($db.status -eq "Paused") {
                $result.note = "Database is paused - schema check skipped. Will resume on first connection."
                # Assume schema is OK if database is paused (can't check)
                $result.checks.schemaValid = $true
            }
            else {
                $result.checks.schemaValid = $false
                $result.schemaCheckError = $schemaCheck.error
            }
        }

        # Determine overall health
        if ($result.checks.schemaValid -and $result.checks.gameServiceConnected) {
            $result.healthy = $true
            $result.status = "connected"
            $result.needsDeploy = $false
        }
        elseif ($result.checks.sqlServer -and $result.checks.database) {
            # Infrastructure exists
            if (-not $result.checks.schemaValid -and $schemaCheck.checked) {
                # Schema is invalid - needs migration
                $result.healthy = $false
                $result.needsDeploy = $true
            }
            elseif (-not $result.checks.gameServiceConnected) {
                # GameService can't connect - needs deploy
                $result.healthy = ($db.status -eq "Online" -or $db.status -eq "Paused")
                $result.needsDeploy = $true
                if (-not $result.note) {
                    $result.note = "GameService cannot connect to database"
                }
            }
            else {
                $result.healthy = $true
            }
        }

        Write-Log -Level "DEBUG" -Message "Database doctor complete: healthy=$($result.healthy), needsDeploy=$($result.needsDeploy)" -TraceLevel $TraceLevel
    }
    catch {
        $result.status = "error"
        $result.error = $_.Exception.Message
        Write-Log -Level "DEBUG" -Message "Database doctor error: $($_.Exception.Message)" -TraceLevel $TraceLevel
    }

    return $result
}

<#
.SYNOPSIS
    Deletes the Azure SQL Server and database.
.DESCRIPTION
    Removes the SQL Server and all databases. This is destructive!
.PARAMETER Config
    Azure configuration hashtable
.OUTPUTS
    Boolean - $true on success
#>
function Clean-Database {
    param([hashtable]$Config)

    $rgName = $Config.resourceGroup
    $sqlServerName = $Config.sqlServer.serverName

    $existing = Invoke-AzCommand "sql server show --name $sqlServerName --resource-group $rgName" -FailOnError $false -JsonOutput
    if ($existing) {
        Write-Log -Level "WARN" -Message "Deleting SQL Server: $sqlServerName (includes all databases)"
        Invoke-AzCommand "sql server delete --name $sqlServerName --resource-group $rgName --yes" -SuppressOutput
        Write-Log -Level "INFO" -Message "SQL Server deleted"
    }
    else {
        Write-Log -Level "INFO" -Message "SQL Server does not exist: $sqlServerName"
    }

    return $true
}

#endregion

#region App Service Functions

# Install-AzureAppServicePlan provided by utility-scripts.psm1

<#
.SYNOPSIS
    Creates and configures the GameService Azure Web App.
.DESCRIPTION
    Creates the .NET 9.0 web app and enables managed identity.
    Database connection is configured separately via 'database deploy'.
.PARAMETER Config
    Azure configuration hashtable
.OUTPUTS
    Boolean - $true on success
#>
function Install-GameService {
    param([hashtable]$Config)

    $rgName = $Config.resourceGroup
    $planName = $Config.gameService.appServicePlan
    $appName = $Config.gameService.appName

    # Ensure resource group and plan exist
    Install-AzureResourceGroup -ResourceGroup $Config.resourceGroup -Location $Config.location | Out-Null
    Install-AzureAppServicePlan -ResourceGroup $Config.resourceGroup -PlanName $Config.gameService.appServicePlan -Location $Config.location | Out-Null

    Write-Log -Level "INFO" -Message "Checking GameService App: $appName"

    $existing = Invoke-AzCommand "webapp show --name $appName --resource-group $rgName" -FailOnError $false -JsonOutput
    if (-not $existing) {
        Write-Log -Level "INFO" -Message "Creating GameService App: $appName"
        Invoke-AzCommand "webapp create --name $appName --resource-group $rgName --plan $planName --runtime DOTNETCORE:9.0" -SuppressOutput
        Write-Log -Level "INFO" -Message "GameService App created: $appName"
    }
    else {
        Write-Log -Level "INFO" -Message "GameService App exists: $appName"
    }

    # Enable Always On to prevent cold starts (10-20 sec delay after idle)
    # This keeps the app warm by pinging it periodically
    Write-Log -Level "INFO" -Message "Enabling Always On for $appName..."
    Invoke-AzCommand "webapp config set --name $appName --resource-group $rgName --always-on true" -SuppressOutput

    # Increase container startup timeout from default 230s to 600s
    # The GameService does background DB seeding on first start which can be slow
    # on cold Azure SQL connections with Managed Identity
    Write-Log -Level "INFO" -Message "Setting container startup timeout to 600s..."
    Invoke-AzCommand "webapp config appsettings set --name $appName --resource-group $rgName --settings WEBSITES_CONTAINER_START_TIME_LIMIT=600" -SuppressOutput

    # Application Insights — skipped (not currently used; az monitor app-insights can hang)
    # To re-enable: uncomment and ensure Invoke-AzCommand timeout handles slow responses
    # $appInsightsConnectionString = Install-AppInsights -Config $Config
    # if ($appInsightsConnectionString) {
    #     Invoke-AzCommand "webapp config appsettings set --name $appName --resource-group $rgName --settings APPLICATIONINSIGHTS_CONNECTION_STRING=`"$appInsightsConnectionString`"" -SuppressOutput
    # }

    # Enable managed identity
    Write-Log -Level "INFO" -Message "Enabling managed identity for $appName..."
    $identity = Invoke-AzCommand "webapp identity assign --name $appName --resource-group $rgName" -FailOnError $false -JsonOutput
    $principalId = $identity.principalId
    if (-not $principalId) {
        # Identity may already exist, retrieve it
        $principalId = Invoke-AzCommand "webapp identity show --name $appName --resource-group $rgName --query principalId -o tsv"
    }

    if (-not $principalId) {
        throw "Failed to enable managed identity for $appName"
    }
    Write-Log -Level "DEBUG" -Message "Principal ID: $principalId"

    # Grant Reader role on resource group for Azure Resource Graph queries
    # This allows the Troubleshoot feature to find and inspect SQL Server resources
    $subscriptionId = Invoke-AzCommand "account show --query id -o tsv"
    if (-not $subscriptionId) {
        throw "Failed to get subscription ID"
    }
    $rgScope = "/subscriptions/$subscriptionId/resourceGroups/$rgName"

    Write-Log -Level "INFO" -Message "Granting Reader role on resource group to managed identity..."
    # Use --assignee-object-id + --assignee-principal-type to avoid Azure AD graph lookup
    # (newly created managed identities may not be replicated to graph yet)
    $existingRole = Invoke-AzCommand "role assignment list --assignee $principalId --role Reader --scope $rgScope --query [0].id -o tsv" -FailOnError $false
    if (-not $existingRole) {
        Invoke-AzCommand "role assignment create --assignee-object-id $principalId --assignee-principal-type ServicePrincipal --role Reader --scope $rgScope" -SuppressOutput
        Write-Log -Level "INFO" -Message "Reader role granted on resource group"
    }
    else {
        Write-Log -Level "DEBUG" -Message "Reader role already assigned on resource group"
    }

    Write-Log -Level "INFO" -Message "GameService App ready: $appName"
    return $true
}

<#
.SYNOPSIS
    Creates and configures the WebUI Azure Web App.
.DESCRIPTION
    Creates the web app for the React UI (Next.js),
    enables managed identity, and configures GameService URL.
.PARAMETER Config
    Azure configuration hashtable
.OUTPUTS
    Boolean - $true on success
#>
<#
.SYNOPSIS
    Ensures a `staging` deployment slot exists for the given app, creating it
    idempotently if missing.
.DESCRIPTION
    Looks up the app's App Service Plan via the app itself, so callers pass
    only the app + resource group. Deployment slots require Standard (S1) or
    higher; if the plan is below that tier, the plan is upgraded to S1 before
    the slot is created. Safe to call repeatedly.

    Step 1 of the cicd-robustness plan extracts this from Install-UI so the
    React CI deploy path can self-heal a missing slot instead of hard-failing
    (the literal cause of run 26066071823 / issue #175).
.PARAMETER AppName
    The App Service name (e.g. the UI or GameService app).
.PARAMETER RgName
    The resource group containing the app.
.OUTPUTS
    Boolean — $true if the slot is present (existing or freshly created).
#>
function Install-StagingSlot {
    param(
        [Parameter(Mandatory = $true)][string]$AppName,
        [Parameter(Mandatory = $true)][string]$RgName
    )

    # Resolve plan name from the app so callers don't need to know it
    $planId = Invoke-AzCommand "webapp show --name $AppName --resource-group $RgName --query appServicePlanId -o tsv" -FailOnError $false
    if (-not $planId) {
        Write-Log -Level "ERROR" -Message "App '$AppName' not found in resource group '$RgName' — cannot ensure staging slot"
        return $false
    }
    $planName = Split-Path $planId -Leaf

    Write-Log -Level "INFO" -Message "Checking staging slot for $AppName..."
    $stagingSlot = Invoke-AzCommand "webapp deployment slot list --name $AppName --resource-group $RgName --query `"[?name=='staging']`"" -FailOnError $false -JsonOutput
    if (-not $stagingSlot -or $stagingSlot.Count -eq 0) {
        # Deployment slots require Standard (S1) tier or higher — upgrade if needed
        $planSku = Invoke-AzCommand "appservice plan show --name $planName --resource-group $RgName --query sku.tier -o tsv" -FailOnError $false
        if ($planSku -and $planSku -notin @('Standard', 'Premium', 'PremiumV2', 'PremiumV3', 'Isolated', 'IsolatedV2')) {
            Write-Log -Level "INFO" -Message "App Service Plan '$planName' is '$planSku' tier — upgrading to Standard (S1) for deployment slot support..."
            Invoke-AzCommand "appservice plan update --name $planName --resource-group $RgName --sku S1" -SuppressOutput
            Write-Log -Level "INFO" -Message "Plan upgraded to S1"
        }
        Write-Log -Level "INFO" -Message "Creating staging slot for $AppName..."
        Invoke-AzCommand "webapp deployment slot create --name $AppName --resource-group $RgName --slot staging" -SuppressOutput
        Write-Log -Level "INFO" -Message "Staging slot created for $AppName"
    }
    else {
        Write-Log -Level "INFO" -Message "Staging slot exists for $AppName"
    }
    return $true
}

function Install-UI {
    param([hashtable]$Config)

    $rgName = $Config.resourceGroup
    $planName = $Config.gameService.appServicePlan
    $appName = $Config.ui.appName

    # Ensure resource group and plan exist
    Install-AzureResourceGroup -ResourceGroup $Config.resourceGroup -Location $Config.location | Out-Null
    Install-AzureAppServicePlan -ResourceGroup $Config.resourceGroup -PlanName $Config.gameService.appServicePlan -Location $Config.location | Out-Null

    Write-Log -Level "INFO" -Message "Checking UI App: $appName"

    $existing = Invoke-AzCommand "webapp show --name $appName --resource-group $rgName" -FailOnError $false -JsonOutput
    if (-not $existing) {
        Write-Log -Level "INFO" -Message "Creating UI App: $appName"
        Invoke-AzCommand "webapp create --name $appName --resource-group $rgName --plan $planName --runtime DOTNETCORE:9.0" -SuppressOutput
        Write-Log -Level "INFO" -Message "UI App created: $appName"
    }
    else {
        Write-Log -Level "INFO" -Message "UI App exists: $appName"
    }

    # Enable Always On to prevent cold starts (10-20 sec delay after idle)
    Write-Log -Level "INFO" -Message "Enabling Always On for $appName..."
    Invoke-AzCommand "webapp config set --name $appName --resource-group $rgName --always-on true" -SuppressOutput

    # Application Insights — skipped (not currently used; az monitor app-insights can hang)
    # $appInsightsConnectionString = Install-AppInsights -Config $Config
    # if ($appInsightsConnectionString) {
    #     Invoke-AzCommand "webapp config appsettings set --name $appName --resource-group $rgName --settings APPLICATIONINSIGHTS_CONNECTION_STRING=`"$appInsightsConnectionString`"" -SuppressOutput
    # }

    # Enable managed identity (for future extensibility)
    Write-Log -Level "INFO" -Message "Enabling managed identity for $appName..."
    Invoke-AzCommand "webapp identity assign --name $appName --resource-group $rgName" -FailOnError $false -SuppressOutput

    # Configure app settings
    Write-Log -Level "INFO" -Message "Configuring app settings..."
    Invoke-AzCommand "webapp config appsettings set --name $appName --resource-group $rgName --settings GAMESERVICE_URL=$($Config.gameService.url)" -SuppressOutput

    # Ensure staging deployment slot exists (idempotent; upgrades plan to S1 if needed)
    if (-not (Install-StagingSlot -AppName $appName -RgName $rgName)) {
        return $false
    }

    # Configure staging slot for Node.js with anti-Oryx settings (idempotent)
    # This prevents Kudu from running Oryx builds on pre-built standalone Next.js deployments
    Write-Log -Level "INFO" -Message "Configuring staging slot for Node.js..."
    Invoke-AzCommand "webapp config set --name $appName --resource-group $rgName --slot staging --linux-fx-version `"NODE|22-lts`" --startup-file `"node server.js`"" -SuppressOutput
    Invoke-AzCommand "webapp config appsettings set --name $appName --resource-group $rgName --slot staging --settings WEBSITE_NODE_DEFAULT_VERSION=~22 SCM_DO_BUILD_DURING_DEPLOYMENT=false ENABLE_ORYX_BUILD=false WEBSITES_CONTAINER_START_TIME_LIMIT=600" -SuppressOutput

    return $true
}

<#
.SYNOPSIS
    Idempotently creates Azure AD app registration and federated credentials for GitHub Actions OIDC.
.DESCRIPTION
    Creates the app registration, service principal, federated credentials for main and staging
    branches, assigns Contributor role on the resource group, and sets GitHub secrets.
    Safe to run repeatedly — checks each resource before creating.
.PARAMETER Config
    Azure configuration hashtable
.OUTPUTS
    Boolean - $true on success
#>
function Install-GitHubOidc {
    param([hashtable]$Config)

    $rgName = $Config.resourceGroup
    $baseName = $Config.baseName
    $appDisplayName = "github-actions-$baseName-deploy"

    # Detect GitHub repo
    Write-Log -Level "INFO" -Message "Detecting GitHub repository..."
    $ghRepo = $null
    try {
        $ghRepo = & gh repo view --json nameWithOwner -q .nameWithOwner 2>$null
    } catch {}
    if (-not $ghRepo) {
        Write-Log -Level "ERROR" -Message "Could not detect GitHub repository. Ensure 'gh' CLI is installed and authenticated."
        return $false
    }
    Write-Log -Level "INFO" -Message "GitHub repo: $ghRepo"

    # Get subscription and tenant IDs
    $accountInfo = Invoke-AzCommand "account show --query `"{subscriptionId:id, tenantId:tenantId}`"" -JsonOutput
    if (-not $accountInfo) {
        Write-Log -Level "ERROR" -Message "Could not get Azure account info"
        return $false
    }
    $subscriptionId = $accountInfo.subscriptionId
    $tenantId = $accountInfo.tenantId
    Write-Log -Level "INFO" -Message "Subscription: $subscriptionId"
    Write-Log -Level "INFO" -Message "Tenant: $tenantId"

    # Check if app registration exists
    Write-Log -Level "INFO" -Message "Checking app registration: $appDisplayName"
    $existingApps = Invoke-AzCommand "ad app list --display-name `"$appDisplayName`" --query `"[?displayName=='$appDisplayName']`"" -FailOnError $false -JsonOutput
    if ($existingApps -and $existingApps.Count -gt 0) {
        $appObjectId = $existingApps[0].id
        $appId = $existingApps[0].appId
        Write-Log -Level "INFO" -Message "App registration exists: $appDisplayName (appId: $appId)"
    }
    else {
        Write-Log -Level "INFO" -Message "Creating app registration: $appDisplayName"
        $newApp = Invoke-AzCommand "ad app create --display-name `"$appDisplayName`"" -JsonOutput
        $appObjectId = $newApp.id
        $appId = $newApp.appId
        Write-Log -Level "INFO" -Message "App registration created: $appDisplayName (appId: $appId)"
    }

    # Ensure service principal exists
    Write-Log -Level "INFO" -Message "Checking service principal..."
    $sp = Invoke-AzCommand "ad sp show --id $appId" -FailOnError $false -JsonOutput
    if (-not $sp) {
        Write-Log -Level "INFO" -Message "Creating service principal..."
        $sp = Invoke-AzCommand "ad sp create --id $appId" -JsonOutput
        Write-Log -Level "INFO" -Message "Service principal created"
    }
    else {
        Write-Log -Level "INFO" -Message "Service principal exists"
    }
    $spObjectId = $sp.id

    # Ensure federated credentials for main and staging branches
    $branches = @("main", "staging")
    $existingCreds = Invoke-AzCommand "ad app federated-credential list --id $appObjectId" -FailOnError $false -JsonOutput
    if (-not $existingCreds) { $existingCreds = @() }

    foreach ($branch in $branches) {
        $credName = "github-actions-$branch"
        $subject = "repo:${ghRepo}:ref:refs/heads/$branch"

        $existing = $existingCreds | Where-Object { $_.name -eq $credName }
        if ($existing) {
            Write-Log -Level "INFO" -Message "Federated credential exists: $credName"
        }
        else {
            Write-Log -Level "INFO" -Message "Creating federated credential: $credName (subject: $subject)"
            $credParams = @{
                name      = $credName
                issuer    = "https://token.actions.githubusercontent.com"
                subject   = $subject
                audiences = @("api://AzureADTokenExchange")
            } | ConvertTo-Json -Compress
            # Write to temp file because az CLI doesn't accept inline JSON well on all platforms
            $tempFile = [System.IO.Path]::GetTempFileName()
            $credParams | Set-Content -Path $tempFile -Encoding UTF8
            try {
                Invoke-AzCommand "ad app federated-credential create --id $appObjectId --parameters @$tempFile" -SuppressOutput
                Write-Log -Level "INFO" -Message "Federated credential created: $credName"
            }
            finally {
                Remove-Item $tempFile -ErrorAction SilentlyContinue
            }
        }
    }

    # Assign Contributor role on resource group (if not already assigned)
    Write-Log -Level "INFO" -Message "Checking Contributor role on $rgName..."
    $roleAssignments = Invoke-AzCommand "role assignment list --assignee $spObjectId --role Contributor --scope /subscriptions/$subscriptionId/resourceGroups/$rgName" -FailOnError $false -JsonOutput
    if ($roleAssignments -and $roleAssignments.Count -gt 0) {
        Write-Log -Level "INFO" -Message "Contributor role already assigned"
    }
    else {
        Write-Log -Level "INFO" -Message "Assigning Contributor role on $rgName..."
        Invoke-AzCommand "role assignment create --assignee-object-id $spObjectId --assignee-principal-type ServicePrincipal --role Contributor --scope /subscriptions/$subscriptionId/resourceGroups/$rgName" -SuppressOutput
        Write-Log -Level "INFO" -Message "Contributor role assigned"
    }

    # Set GitHub secrets
    Write-Log -Level "INFO" -Message "Setting GitHub secrets..."
    $secretErrors = 0
    foreach ($pair in @(
        @{ name = "AZURE_CLIENT_ID"; value = $appId },
        @{ name = "AZURE_TENANT_ID"; value = $tenantId },
        @{ name = "AZURE_SUBSCRIPTION_ID"; value = $subscriptionId }
    )) {
        try {
            $result = & gh secret set $pair.name --body $pair.value 2>&1
            if ($LASTEXITCODE -ne 0) {
                Write-Log -Level "WARN" -Message "Failed to set secret $($pair.name): $result"
                $secretErrors++
            }
            else {
                Write-Log -Level "INFO" -Message "Secret set: $($pair.name)"
            }
        }
        catch {
            Write-Log -Level "WARN" -Message "Failed to set secret $($pair.name): $_"
            $secretErrors++
        }
    }

    if ($secretErrors -gt 0) {
        Write-Log -Level "WARN" -Message "$secretErrors secret(s) failed to set. Ensure 'gh' CLI has repo admin access."
    }

    Write-Log -Level "INFO" -Message "GitHub OIDC setup complete"
    Write-Log -Level "INFO" -Message "  App: $appDisplayName"
    Write-Log -Level "INFO" -Message "  Client ID: $appId"
    Write-Log -Level "INFO" -Message "  Branches: $($branches -join ', ')"
    Write-Log -Level "INFO" -Message "  Role: Contributor on $rgName"
    return $true
}

<#
.SYNOPSIS
    Checks the health of GitHub Actions OIDC configuration.
.DESCRIPTION
    Verifies app registration, service principal, federated credentials, role assignments,
    and GitHub secrets exist and are correctly configured.
.PARAMETER Config
    Azure configuration hashtable
.PARAMETER TraceLevel
    Output detail level
.OUTPUTS
    Hashtable with doctor result (resource, name, healthy, status, checks)
#>
function Get-GitHubDoctor {
    param(
        [hashtable]$Config,
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    $baseName = $Config.baseName
    $rgName = $Config.resourceGroup
    $appDisplayName = "github-actions-$baseName-deploy"

    Write-Log -Level "DEBUG" -Message "Get-GitHubDoctor started" -TraceLevel $TraceLevel

    $result = @{
        resource  = "github"
        name      = $appDisplayName
        status    = "unknown"
        healthy   = $false
        timestamp = (Get-Date -Format "o")
        checks    = @{
            appRegistration    = $false
            servicePrincipal   = $false
            federatedMain      = $false
            federatedStaging   = $false
            contributorRole    = $false
        }
    }

    # Check app registration
    $existingApps = Invoke-AzCommand "ad app list --display-name `"$appDisplayName`" --query `"[?displayName=='$appDisplayName']`"" -FailOnError $false -JsonOutput
    if ($existingApps -and $existingApps.Count -gt 0) {
        $result.checks.appRegistration = $true
        $appObjectId = $existingApps[0].id
        $appId = $existingApps[0].appId

        # Check service principal
        $sp = Invoke-AzCommand "ad sp show --id $appId" -FailOnError $false -JsonOutput
        if ($sp) {
            $result.checks.servicePrincipal = $true
            $spObjectId = $sp.id

            # Check Contributor role
            $accountInfo = Invoke-AzCommand "account show --query id -o tsv" -FailOnError $false
            if ($accountInfo) {
                $roleAssignments = Invoke-AzCommand "role assignment list --assignee $spObjectId --role Contributor --scope /subscriptions/$accountInfo/resourceGroups/$rgName" -FailOnError $false -JsonOutput
                if ($roleAssignments -and $roleAssignments.Count -gt 0) {
                    $result.checks.contributorRole = $true
                }
            }
        }

        # Check federated credentials
        $creds = Invoke-AzCommand "ad app federated-credential list --id $appObjectId" -FailOnError $false -JsonOutput
        if ($creds) {
            foreach ($cred in $creds) {
                if ($cred.name -eq "github-actions-main") { $result.checks.federatedMain = $true }
                if ($cred.name -eq "github-actions-staging") { $result.checks.federatedStaging = $true }
            }
        }
    }

    # Determine overall health
    $allChecks = $result.checks.Values | Where-Object { $_ -eq $false }
    if ($allChecks.Count -eq 0) {
        $result.healthy = $true
        $result.status = "healthy"
    }
    else {
        $result.status = "needs-install"
    }

    return $result
}

# Get-GitCommitHash, Deploy-KuduZip, Test-DeploymentNeeded
# provided by utility-scripts.psm1

<#
.SYNOPSIS
    Builds and deploys the GameService to Azure.
.DESCRIPTION
    Publishes the Catan3.GameService project, creates a zip package,
    and deploys it to the Azure Web App using zip deployment.
    Skips deployment if no changes detected (use -Force to override).
.PARAMETER Config
    Azure configuration hashtable
.PARAMETER Force
    Force deployment even if no changes detected
.OUTPUTS
    Boolean - $true on success
#>
function Deploy-GameService {
    param(
        [hashtable]$Config,
        [bool]$Force = $false,
        [bool]$NoBuild = $false,
        [string]$Slot = $null
    )

    $rgName = $Config.resourceGroup
    $appName = $Config.gameService.appName
    $projectPath = Join-Path $ProjectRoot "Catan3.GameService"
    $publishPath = Join-Path $ProjectRoot ".publish/gameservice"
    $zipPath = Join-Path $ProjectRoot ".publish/gameservice.zip"
    $slotArgs = if ($Slot) { " --slot $Slot" } else { "" }
    $slotLabel = if ($Slot) { " (slot: $Slot)" } else { "" }

    # Ensure staging slot exists when deploying to a slot
    # See .design/staging-slot-config.md for the full list of required settings
    if ($Slot) {
        $existingSlots = Invoke-AzCommand "webapp deployment slot list --name $appName --resource-group $rgName --query `"[].name`" -o tsv" -FailOnError $false
        if ($existingSlots -notcontains $Slot) {
            Write-Log -Level "INFO" -Message "Creating deployment slot '$Slot' on $appName..."
            Invoke-AzCommand "webapp deployment slot create --name $appName --resource-group $rgName --slot $Slot" -SuppressOutput
            Invoke-AzCommand "webapp identity assign --name $appName --resource-group $rgName --slot $Slot" -SuppressOutput
        }

        # Always ensure required settings are present (idempotent)
        # Copy Cosmos settings from production so the staging slot can connect to the same DB
        Write-Log -Level "INFO" -Message "Configuring slot '$Slot' settings..."
        $prodSettings = Invoke-AzCommand "webapp config appsettings list --name $appName --resource-group $rgName" -Check -JsonOutput
        $cosmosEndpoint = ($prodSettings | Where-Object { $_.name -eq 'COSMOS_ENDPOINT' } | Select-Object -First 1).value
        $cosmosDatabase = ($prodSettings | Where-Object { $_.name -eq 'COSMOS_DATABASE' } | Select-Object -First 1).value
        Invoke-AzCommand "webapp config appsettings set --name $appName --resource-group $rgName --slot $Slot --settings DATABASE_MODE=azure COSMOS_ENDPOINT=$cosmosEndpoint COSMOS_DATABASE=$cosmosDatabase WEBSITES_CONTAINER_START_TIME_LIMIT=600 SCM_DO_BUILD_DURING_DEPLOYMENT=false" -SuppressOutput
    }

    # Check if deployment is needed
    if (-not (Test-DeploymentNeeded -AppName $appName -ResourceGroup $rgName -Force $Force -Slot $Slot)) {
        return $true
    }

    Write-Log -Level "INFO" -Message "Publishing GameService$slotLabel..."
    $publishArgs = @($projectPath, "-c", "Release", "-o", $publishPath, "--nologo", "-v", "q")
    if ($NoBuild) { $publishArgs += "--no-build" }
    dotnet publish @publishArgs

    Write-Log -Level "INFO" -Message "Creating deployment package..."
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path "$publishPath/*" -DestinationPath $zipPath

    $zipSize = (Get-Item $zipPath).Length / 1MB
    Write-Log -Level "INFO" -Message "Deploying to Azure$slotLabel ($([math]::Round($zipSize, 1)) MB)..."

    # Enable logging to diagnose startup failures
    Write-Log -Level "DEBUG" -Message "Enabling App Service logging..."
    Invoke-AzCommand "webapp log config --name $appName --resource-group $rgName$slotArgs --docker-container-logging filesystem --detailed-error-messages true --web-server-logging filesystem" -SuppressOutput

    # Deploy via Kudu ZIP Deploy API (truly async, unlike az webapp deploy --async true)
    if (-not (Deploy-KuduZip -AppName $appName -ResourceGroup $rgName -ZipPath $zipPath -Slot $Slot)) {
        return $false
    }

    # Restart the app after deploy to ensure the new code is loaded
    # Kudu async zipdeploy may not trigger an automatic restart
    Write-Log -Level "INFO" -Message "Restarting app to load new deployment$slotLabel..."
    Invoke-AzCommand "webapp restart --name $appName --resource-group $rgName$slotArgs" -SuppressOutput

    # Store the deployed commit hash and build timestamp
    $commitHash = Get-GitCommitHash
    $buildTime = (Get-Date -Format "o")  # ISO 8601 format
    Invoke-AzCommand "webapp config appsettings set --name $appName --resource-group $rgName$slotArgs --settings DEPLOY_COMMIT=$commitHash DEPLOY_BUILD_TIME=`"$buildTime`"" -SuppressOutput

    $url = if ($Slot) { "https://$appName-$Slot.azurewebsites.net" } else { $Config.gameService.url }
    Write-Log -Level "INFO" -Message "GameService deployed: $url"
    return $true
}

<#
.SYNOPSIS
    Builds and deploys the WebUI to Azure.
.DESCRIPTION
    Publishes the Blazor WebAssembly project, creates a zip package,
    and deploys it to the Azure Web App using zip deployment.
    Skips deployment if no changes detected (use -Force to override).
.PARAMETER Config
    Azure configuration hashtable
.PARAMETER Force
    Force deployment even if no changes detected
.OUTPUTS
    Boolean - $true on success
#>
function Deploy-UI {
    param(
        [hashtable]$Config,
        [bool]$Force = $false,
        [bool]$NoBuild = $false
    )

    $rgName = $Config.resourceGroup
    $appName = $Config.ui.appName
    $reactDir = Join-Path $ProjectRoot "react-ui"
    $deployDir = Join-Path $ProjectRoot ".publish/react-production"
    $zipPath = Join-Path $ProjectRoot ".publish/react-ui-production.zip"
    $gameServiceUrl = $Config.gameService.url

    # Check if deployment is needed
    if (-not (Test-DeploymentNeeded -AppName $appName -ResourceGroup $rgName -Force $Force)) {
        return $true
    }

    # Ensure production slot is configured for Node.js (idempotent)
    Write-Log -Level "INFO" -Message "Ensuring production is configured for Node.js..."
    Invoke-AzCommand "webapp config set --name $appName --resource-group $rgName --linux-fx-version `"NODE|22-lts`" --startup-file `"node server.js`"" -SuppressOutput
    Invoke-AzCommand "webapp config appsettings set --name $appName --resource-group $rgName --settings WEBSITE_NODE_DEFAULT_VERSION=~22 SCM_DO_BUILD_DURING_DEPLOYMENT=false ENABLE_ORYX_BUILD=false WEBSITES_CONTAINER_START_TIME_LIMIT=600" -SuppressOutput

    # Install dependencies — remove node_modules first to break file locks
    # (VS Code / Claude Code may hold native .node binaries open)
    Write-Log -Level "INFO" -Message "Installing React UI dependencies..."
    $nodeModulesPath = Join-Path $reactDir "node_modules"
    if (Test-Path $nodeModulesPath) {
        Write-Log -Level "DEBUG" -Message "Removing node_modules to break file locks..."
        Remove-Item $nodeModulesPath -Recurse -Force -ErrorAction SilentlyContinue
        if (Test-Path $nodeModulesPath) {
            Write-Log -Level "WARN" -Message "Could not fully remove node_modules — a process may hold a lock."
            Write-Log -Level "INFO" -Message "  Try closing VS Code or other editors, then retry."
        }
    }
    Push-Location $reactDir
    try {
        $npmOutput = npm ci 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Log -Level "ERROR" -Message "npm ci failed:"
            $npmOutput | ForEach-Object { Write-Log -Level "ERROR" -Message "  $_" }
            return $false
        }

        # Build Next.js standalone with production GameService URL
        Write-Log -Level "INFO" -Message "Building React UI (standalone)..."
        $env:NEXT_PUBLIC_GAME_SERVICE_URL = $gameServiceUrl
        $buildOutput = npm run build 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Log -Level "ERROR" -Message "Next.js build failed:"
            $buildOutput | ForEach-Object { Write-Log -Level "ERROR" -Message "  $_" }
            return $false
        }
        $buildOutput | ForEach-Object { Write-Log -Level "DEBUG" -Message $_ }
    }
    finally {
        Pop-Location
    }

    # Assemble deployment package
    Write-Log -Level "INFO" -Message "Assembling deployment package..."
    if (Test-Path $deployDir) { Remove-Item $deployDir -Recurse -Force }
    New-Item -ItemType Directory -Path $deployDir -Force | Out-Null

    $standalonePath = Join-Path $reactDir ".next/standalone/react-ui"
    if (-not (Test-Path $standalonePath)) {
        $standalonePath = Join-Path $reactDir ".next/standalone"
    }
    Copy-Item -Path "$standalonePath/*" -Destination $deployDir -Recurse -Force
    Copy-Item -Path (Join-Path $reactDir ".next/static") -Destination (Join-Path $deployDir ".next/static") -Recurse -Force
    Copy-Item -Path (Join-Path $reactDir "public") -Destination (Join-Path $deployDir "public") -Recurse -Force

    # Create zip
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    $items = Get-ChildItem -Path $deployDir -Force | Where-Object { $_.Name -ne '.' -and $_.Name -ne '..' }
    Compress-Archive -Path $items.FullName -DestinationPath $zipPath

    $zipSize = (Get-Item $zipPath).Length / 1MB
    Write-Log -Level "INFO" -Message "Deploying React UI to production ($([math]::Round($zipSize, 1)) MB)..."

    # Set the GameService URL
    Invoke-AzCommand "webapp config appsettings set --name $appName --resource-group $rgName --settings NEXT_PUBLIC_GAME_SERVICE_URL=$gameServiceUrl" -SuppressOutput

    # Deploy via Kudu ZIP Deploy API
    if (-not (Deploy-KuduZip -AppName $appName -ResourceGroup $rgName -ZipPath $zipPath)) {
        return $false
    }

    # Restart to load new deployment
    Write-Log -Level "INFO" -Message "Restarting app to load new deployment..."
    Invoke-AzCommand "webapp restart --name $appName --resource-group $rgName" -SuppressOutput

    # Store deployed commit
    $commitHash = Get-GitCommitHash
    $buildTime = (Get-Date -Format "o")
    Invoke-AzCommand "webapp config appsettings set --name $appName --resource-group $rgName --settings DEPLOY_COMMIT=$commitHash DEPLOY_BUILD_TIME=`"$buildTime`"" -SuppressOutput

    Write-Log -Level "INFO" -Message "UI deployed: $($Config.ui.url)"
    return $true
}

<#
.SYNOPSIS
    Builds and deploys the React UI to the Azure staging slot.
.DESCRIPTION
    Builds the Next.js standalone app, packages it, and deploys to the
    staging deployment slot. Use swap-slots to promote to production.
.PARAMETER Config
    Azure configuration hashtable
.PARAMETER Force
    Force deployment even if no changes detected
.OUTPUTS
    Boolean - $true on success
#>
function Deploy-ReactStaging {
    param(
        [hashtable]$Config,
        [bool]$Force = $false,
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR",
        [string]$GameServiceUrl = $null
    )

    $rgName = $Config.resourceGroup
    $appName = $Config.ui.appName
    $reactDir = Join-Path $ProjectRoot "react-ui"
    $deployDir = Join-Path $ProjectRoot ".publish/react-staging"
    $zipPath = Join-Path $ProjectRoot ".publish/react-ui.zip"
    # Default to the production GameService URL: GameService has no staging slot yet,
    # so the staging React slot must talk to the production GameService for now.
    # (Step 6 of cicd-robustness will add a GS staging slot + pair the staging React
    # against it so we verify the new-React ↔ new-GS combination that ships.)
    $gameServiceUrl = if ($GameServiceUrl) { $GameServiceUrl } else { "https://$($Config.gameService.appName).azurewebsites.net" }

    # Ensure staging slot exists (idempotent — creates with plan-SKU upgrade if needed).
    # This replaces the prior hard-fail that broke run 26066071823 / issue #175.
    if (-not (Install-StagingSlot -AppName $appName -RgName $rgName)) {
        Write-Log -Level "ERROR" -Message "Failed to ensure staging slot for $appName"
        return $false
    }

    # Ensure staging slot is configured for Node.js (idempotent)
    # After a production slot swap, the staging slot inherits the production runtime (DOTNETCORE:9.0)
    # which cannot run the Next.js standalone server. Reset to NODE|22-lts on every deploy.
    Write-Log -Level "INFO" -Message "Ensuring staging slot is configured for Node.js..."
    Invoke-AzCommand "webapp config set --name $appName --resource-group $rgName --slot staging --linux-fx-version `"NODE|22-lts`" --startup-file `"node server.js`"" -SuppressOutput
    Invoke-AzCommand "webapp config appsettings set --name $appName --resource-group $rgName --slot staging --settings WEBSITE_NODE_DEFAULT_VERSION=~22 SCM_DO_BUILD_DURING_DEPLOYMENT=false ENABLE_ORYX_BUILD=false WEBSITES_CONTAINER_START_TIME_LIMIT=600" -SuppressOutput

    # Skip if already current (unless -Force)
    $currentCommit = Get-GitCommitHash
    if (-not $Force) {
        $stagingSettings = Invoke-AzCommand "webapp config appsettings list --name $appName --resource-group $rgName --slot staging" -FailOnError $false -JsonOutput
        $deployedCommit = if ($stagingSettings) {
            ($stagingSettings | Where-Object { $_.name -eq 'DEPLOY_COMMIT' } | Select-Object -First 1).value
        }
        if ($deployedCommit -and $deployedCommit -eq $currentCommit) {
            Write-Log -Level "INFO" -Message "Staging already at commit $currentCommit — skipping (use -Force to override)"
            return $true
        }
    }

    # Install dependencies
    Write-Log -Level "INFO" -Message "Installing React UI dependencies..."
    Push-Location $reactDir
    try {
        $npmOutput = npm ci 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Log -Level "ERROR" -Message "npm ci failed:"
            $npmOutput | ForEach-Object { Write-Log -Level "ERROR" -Message "  $_" }
            return $false
        }
        Write-Log -Level "DEBUG" -Message "npm ci completed" -TraceLevel $TraceLevel

        # Build Next.js standalone
        # Note: next.config.ts sets images.unoptimized=true, so sharp is not needed
        # This avoids cross-platform native binary issues (macOS build → Linux deploy)
        Write-Log -Level "INFO" -Message "Building React UI (standalone)..."
        $env:NEXT_PUBLIC_GAME_SERVICE_URL = $gameServiceUrl
        $buildOutput = npm run build 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Log -Level "ERROR" -Message "Next.js build failed:"
            $buildOutput | ForEach-Object { Write-Log -Level "ERROR" -Message "  $_" }
            return $false
        }
        $buildOutput | ForEach-Object { Write-Log -Level "DEBUG" -Message $_ -TraceLevel $TraceLevel }
    }
    finally {
        Pop-Location
    }

    # Assemble deployment package (mirrors GitHub Action)
    Write-Log -Level "INFO" -Message "Assembling deployment package..."
    if (Test-Path $deployDir) { Remove-Item $deployDir -Recurse -Force }
    New-Item -ItemType Directory -Path $deployDir -Force | Out-Null

    $standalonePath = Join-Path $reactDir ".next/standalone/react-ui"
    if (-not (Test-Path $standalonePath)) {
        # Fallback: standalone may be at .next/standalone directly
        $standalonePath = Join-Path $reactDir ".next/standalone"
    }
    Copy-Item -Path "$standalonePath/*" -Destination $deployDir -Recurse -Force
    Copy-Item -Path (Join-Path $reactDir ".next/static") -Destination (Join-Path $deployDir ".next/static") -Recurse -Force
    Copy-Item -Path (Join-Path $reactDir "public") -Destination (Join-Path $deployDir "public") -Recurse -Force

    # Create zip (-Force includes hidden dirs like .next)
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    $items = Get-ChildItem -Path $deployDir -Force | Where-Object { $_.Name -ne '.' -and $_.Name -ne '..' }
    Compress-Archive -Path $items.FullName -DestinationPath $zipPath

    $zipSize = (Get-Item $zipPath).Length / 1MB
    Write-Log -Level "INFO" -Message "Deploying React UI to staging ($([math]::Round($zipSize, 1)) MB)..."

    # Set the GameService URL for this deploy (slot config is set at install time by Install-UI)
    Invoke-AzCommand "webapp config appsettings set --name $appName --resource-group $rgName --slot staging --settings NEXT_PUBLIC_GAME_SERVICE_URL=$gameServiceUrl" -SuppressOutput

    # Deploy via Kudu ZIP Deploy API (truly async, unlike az webapp deploy --async true)
    if (-not (Deploy-KuduZip -AppName $appName -ResourceGroup $rgName -ZipPath $zipPath -Slot "staging")) {
        return $false
    }

    # Restart the app after deploy to ensure the new code is loaded
    Write-Log -Level "INFO" -Message "Restarting staging slot to load new deployment..."
    Invoke-AzCommand "webapp restart --name $appName --resource-group $rgName --slot staging" -SuppressOutput

    # Store deployed commit in staging slot settings
    $commitHash = Get-GitCommitHash
    $buildTime = (Get-Date -Format "o")
    Invoke-AzCommand "webapp config appsettings set --name $appName --resource-group $rgName --slot staging --settings DEPLOY_COMMIT=$commitHash DEPLOY_BUILD_TIME=`"$buildTime`"" -SuppressOutput

    Write-Log -Level "INFO" -Message "React UI deployed to staging: https://$appName-staging.azurewebsites.net"
    return $true
}

<#
.SYNOPSIS
    Swaps the React UI staging slot into production.
.DESCRIPTION
    Performs a plain Azure slot swap (staging → production) using
    config-derived resource names so the workflow carries no hardcoded
    names (Finding I of the cicd-robustness review).

    Step 2 of the cicd-robustness plan keeps the operation simple; Step 4
    will replace this with the two-phase `--action preview|swap|reset`
    primitive that gives state-checked retry/rollback at the Azure layer.
.PARAMETER Config
    Azure configuration hashtable.
.OUTPUTS
    Boolean — $true on success.
#>
function Invoke-UISwap {
    param([hashtable]$Config)

    $rgName = $Config.resourceGroup
    $appName = $Config.ui.appName

    Write-Log -Level "INFO" -Message "Swapping staging -> production for $appName..."
    Invoke-AzCommand "webapp deployment slot swap --name $appName --resource-group $rgName --slot staging --target-slot production" -SuppressOutput
    Write-Log -Level "INFO" -Message "Swap complete: https://$appName.azurewebsites.net"
    return $true
}

<#
.SYNOPSIS
    Verifies a UI slot is serving HTTP 200 within a bounded retry window.
.DESCRIPTION
    Config-derived URL so the workflow carries no hardcoded hostnames
    (Finding I). HTTP 200 is the bar for Step 2; Step 3/6 of the
    cicd-robustness plan will introduce versioned readiness (expected
    commit/releaseId + API smoke test) and pairing checks.
.PARAMETER Config
    Azure configuration hashtable.
.PARAMETER Slot
    Which slot to verify: 'staging' or 'production'. Defaults to
    'production'.
.OUTPUTS
    Boolean — $true if the slot returned HTTP 200 within the retry window.
#>
function Invoke-UIVerify {
    param(
        [hashtable]$Config,
        [ValidateSet("staging", "production")]
        [string]$Slot = "production"
    )

    $appName = $Config.ui.appName
    $url = if ($Slot -eq "staging") {
        "https://$appName-staging.azurewebsites.net"
    }
    else {
        "https://$appName.azurewebsites.net"
    }

    Write-Log -Level "INFO" -Message "Verifying $Slot at $url..."
    for ($i = 1; $i -le 30; $i++) {
        try {
            $resp = Invoke-WebRequest -Uri $url -TimeoutSec 10 -UseBasicParsing -ErrorAction Stop
            if ($resp.StatusCode -eq 200) {
                Write-Log -Level "INFO" -Message "$Slot OK after $($i * 10)s (HTTP 200)"
                return $true
            }
            Write-Log -Level "DEBUG" -Message "  HTTP $($resp.StatusCode) ($($i * 10)s)..."
        }
        catch {
            Write-Log -Level "DEBUG" -Message "  not responding ($($i * 10)s)..."
        }
        Start-Sleep -Seconds 10
    }
    Write-Log -Level "ERROR" -Message "$Slot did not respond with HTTP 200 within 5 minutes ($url)"
    return $false
}

<#
.SYNOPSIS
    Checks health of the deployed GameService.
.DESCRIPTION
    Verifies the web app exists, checks its state, and calls the /health
    endpoint to confirm the service is responding correctly.
.PARAMETER Config
    Azure configuration hashtable
.OUTPUTS
    Hashtable with status, healthy flag, and diagnostic details
#>
function Get-GameServiceDoctor {
    param(
        [hashtable]$Config,
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR",
        [switch]$Staging
    )

    $appName = $Config.gameService.appName
    $planName = $Config.gameService.appServicePlan
    $url = $Config.gameService.url
    $rgName = $Config.resourceGroup

    # Determine target based on -Staging flag
    $slotArg = if ($Staging) { " --slot staging" } else { "" }
    $targetUrl = if ($Staging) { "https://$appName-staging.azurewebsites.net" } else { $url }
    $targetLabel = if ($Staging) { "staging" } else { "production" }

    Write-Log -Level "DEBUG" -Message "Get-GameServiceDoctor started (target: $targetLabel)" -TraceLevel $TraceLevel

    $result = @{
        resource    = "game-service"
        name        = if ($Staging) { "$appName (staging)" } else { $appName }
        url         = $targetUrl
        status      = "unknown"
        healthy     = $false
        healthCheck = "unknown"
        timestamp   = (Get-Date -Format "o")
        # Detailed checks for each install/deploy step
        checks      = @{
            resourceGroup    = $false
            appServicePlan   = $false
            planSkuOk        = $false
            webApp           = $false
            managedIdentity  = $false
            alwaysOn         = $false
            startupTimeout   = $false
            codeDeployed     = $false
            healthEndpoint   = $false
        }
        # Git commit and build time tracking
        currentCommit     = $null
        deployedCommit    = $null
        deployedBuildTime = $null
        # What actions are needed
        needsInstall   = $false
        needsDeploy    = $false
        # Current SKU for display
        currentSku     = $null
    }

    try {
        # Check resource group exists
        Write-Log -Level "DEBUG" -Message "Checking resource group: $rgName" -TraceLevel $TraceLevel
        $rg = Invoke-AzCommand "group show --name $rgName" -FailOnError $false -JsonOutput
        $result.checks.resourceGroup = ($null -ne $rg)

        # Check App Service Plan exists and has correct SKU
        Write-Log -Level "DEBUG" -Message "Checking app service plan: $planName" -TraceLevel $TraceLevel
        $plan = Invoke-AzCommand "appservice plan show --name $planName --resource-group $rgName" -FailOnError $false -JsonOutput
        $result.checks.appServicePlan = ($null -ne $plan)
        if ($plan) {
            $result.currentSku = $plan.sku.name
            # F1 (Free) and D1 (Shared) don't support Always On - need B1 or higher
            # Note: This is a performance limitation, not a missing infrastructure issue
            # The app works on F1, it just has cold start delays
            $result.checks.planSkuOk = ($plan.sku.name -notin @("F1", "D1"))
            if (-not $result.checks.planSkuOk) {
                # Don't set needsInstall - F1 works, just with performance limitations
                if (-not $result.performanceWarnings) { $result.performanceWarnings = @() }
                $result.performanceWarnings += "App Service Plan SKU is $($plan.sku.name) - upgrade to B1 or higher for Always On support"
            }
        }

        # Check web app exists
        Write-Log -Level "DEBUG" -Message "Checking web app: $appName" -TraceLevel $TraceLevel
        $app = Invoke-AzCommand "webapp show --name $appName --resource-group $rgName" -FailOnError $false -JsonOutput
        if (-not $app) {
            $result.status = "not-found"
            $result.needsInstall = $true
            Write-Log -Level "DEBUG" -Message "Web app not found, needsInstall=true" -TraceLevel $TraceLevel
            return $result
        }
        $result.checks.webApp = $true
        $result.status = $app.state.ToLower()

        # Check managed identity
        Write-Log -Level "DEBUG" -Message "Checking managed identity" -TraceLevel $TraceLevel
        $identity = Invoke-AzCommand "webapp identity show --name $appName --resource-group $rgName$slotArg --query principalId -o tsv" -FailOnError $false
        $result.checks.managedIdentity = (-not [string]::IsNullOrWhiteSpace($identity))

        # Check Always On setting (critical for performance - prevents cold starts)
        Write-Log -Level "DEBUG" -Message "Checking Always On setting" -TraceLevel $TraceLevel
        $alwaysOn = Invoke-AzCommand "webapp config show --name $appName --resource-group $rgName$slotArg --query alwaysOn -o tsv" -FailOnError $false
        $result.checks.alwaysOn = ($alwaysOn -eq "true")
        if (-not $result.checks.alwaysOn) {
            $result.performanceWarnings = @("Always On is disabled - app will have cold start delays")
        }

        # Check container startup timeout (default 230s is too short for cold DB connections)
        Write-Log -Level "DEBUG" -Message "Checking container startup timeout" -TraceLevel $TraceLevel
        $appSettings = Invoke-AzCommand "webapp config appsettings list --name $appName --resource-group $rgName$slotArg" -FailOnError $false -JsonOutput
        $timeoutSetting = $appSettings | Where-Object { $_.name -eq 'WEBSITES_CONTAINER_START_TIME_LIMIT' } | Select-Object -First 1
        $timeoutValue = if ($timeoutSetting) { [int]$timeoutSetting.value } else { 230 }
        $result.checks.startupTimeout = ($timeoutValue -ge 600)
        if (-not $result.checks.startupTimeout) {
            if (-not $result.performanceWarnings) { $result.performanceWarnings = @() }
            $result.performanceWarnings += "Container startup timeout is ${timeoutValue}s (need 600s) — run: $($script:CmdHintPrefix) game-service install"
        }

        # Get current git commit
        $result.currentCommit = Get-GitCommitHash
        Write-Log -Level "DEBUG" -Message "Current git commit: $($result.currentCommit)" -TraceLevel $TraceLevel

        # Check health endpoint first - this is the definitive test of whether code is deployed
        # The health endpoint returns the deployed commit and build time directly
        # Note: F1 (Free) tier apps can take 30-60+ seconds to cold start, so we retry
        Write-Log -Level "DEBUG" -Message "Checking health endpoint: $targetUrl/health" -TraceLevel $TraceLevel
        $health = $null
        $maxRetries = 2
        $timeouts = @(15, 60)  # First try 15s, retry with 60s for cold start

        for ($retry = 0; $retry -lt $maxRetries; $retry++) {
            try {
                $timeout = $timeouts[$retry]
                if ($retry -gt 0) {
                    Write-Log -Level "INFO" -Message "Health check retry $retry (cold start likely, waiting up to ${timeout}s)..." -TraceLevel $TraceLevel
                }
                $health = Invoke-RestMethod -Uri "$targetUrl/health" -TimeoutSec $timeout
                break  # Success, exit retry loop
            }
            catch {
                Write-Log -Level "DEBUG" -Message "Health check attempt $($retry + 1) failed: $_" -TraceLevel $TraceLevel
                if ($retry -eq $maxRetries - 1) {
                    # Final attempt failed
                    $result.healthCheck = "unreachable"
                    $result.checks.healthEndpoint = $false
                }
            }
        }

        if ($health) {
            $result.healthCheck = $health.status
            $result.checks.healthEndpoint = ($health.status -eq "healthy")
            # Get deployed version info from health endpoint
            if ($health.version) {
                if ($health.version.commit) {
                    $result.deployedCommit = $health.version.commit
                    Write-Log -Level "DEBUG" -Message "Deployed commit from health: $($result.deployedCommit)" -TraceLevel $TraceLevel
                }
                if ($health.version.buildTime) {
                    $result.deployedBuildTime = $health.version.buildTime
                    Write-Log -Level "DEBUG" -Message "Deployed build time: $($result.deployedBuildTime)" -TraceLevel $TraceLevel
                }
            }
        }

        # Code deployed = health endpoint responds OR DEPLOY_COMMIT is set in app settings
        # (app may still be starting after a fresh deploy — don't confuse cold start with "not deployed")
        if (-not $result.checks.healthEndpoint) {
            $deployCommit = $appSettings | Where-Object { $_.name -eq 'DEPLOY_COMMIT' } | Select-Object -First 1
            if ($deployCommit -and -not [string]::IsNullOrWhiteSpace($deployCommit.value)) {
                $result.checks.codeDeployed = $true
                $result.deployedCommit = $deployCommit.value
                Write-Log -Level "DEBUG" -Message "Health endpoint down but DEPLOY_COMMIT=$($deployCommit.value) — code is deployed (likely cold start)"
                $deployBuildTime = $appSettings | Where-Object { $_.name -eq 'DEPLOY_BUILD_TIME' } | Select-Object -First 1
                if ($deployBuildTime) { $result.deployedBuildTime = $deployBuildTime.value }
            } else {
                $result.checks.codeDeployed = $false
            }
        } else {
            $result.checks.codeDeployed = $true
        }

        # Check if deploy is needed:
        # - No code deployed at all = needs deploy
        # - Health endpoint works but no version info = old code, needs deploy
        # - Commit mismatch = needs deploy
        if (-not $result.checks.codeDeployed) {
            $result.needsDeploy = $true
            $result.deployReason = "No code deployed"
        }
        elseif (-not $result.checks.healthEndpoint -and $result.checks.codeDeployed) {
            # Code is deployed but health endpoint not responding — cold start, not a deploy issue
            $result.coldStart = $true
        }
        elseif ([string]::IsNullOrWhiteSpace($result.deployedCommit) -or $result.deployedCommit -eq "local") {
            # Health endpoint works but no version info in response = old code deployed
            $result.needsDeploy = $true
            $result.deployReason = "Deployed code missing version info"
        }
        elseif ([string]::IsNullOrWhiteSpace($result.deployedBuildTime) -or $result.deployedBuildTime -eq "unknown") {
            # Working but no build time tracking - needs deploy to enable tracking
            $result.needsDeploy = $true
            $result.deployReason = "Deployed code missing build time tracking"
        }
        elseif ($result.currentCommit -ne $result.deployedCommit) {
            # Commits differ — only flag NEEDS DEPLOY if backend files actually changed.
            # Commits that only modify scripts/docs/workflows don't require a redeploy.
            $changedFiles = @(git -C $ProjectRoot diff --name-only "$($result.deployedCommit)..$($result.currentCommit)" 2>$null)
            $deployableChanges = @($changedFiles | Where-Object { $_ -match '^(Catan3\.GameService|Catan3\.Shared)/' })
            if ($deployableChanges.Count -gt 0) {
                $result.needsDeploy = $true
                $result.deployReason = "Git commit mismatch"
            }
        }
        # Note: If commits match but code is uncommitted, -Force flag can be used to redeploy

        # Healthy if endpoint responds OR code is deployed but cold-starting
        $result.healthy = $result.checks.healthEndpoint -or ($result.checks.codeDeployed -and $result.coldStart)

        Write-Log -Level "DEBUG" -Message "GameService doctor complete: healthy=$($result.healthy), needsDeploy=$($result.needsDeploy)" -TraceLevel $TraceLevel
    }
    catch {
        $result.status = "error"
        $result.error = $_.Exception.Message
        Write-Log -Level "DEBUG" -Message "GameService doctor error: $($_.Exception.Message)" -TraceLevel $TraceLevel
    }

    return $result
}

<#
.SYNOPSIS
    Checks health of the deployed WebUI.
.DESCRIPTION
    Verifies the web app exists, checks its state, and confirms the UI
    responds with HTTP 200.
.PARAMETER Config
    Azure configuration hashtable
.OUTPUTS
    Hashtable with status, healthy flag, and diagnostic details
#>
function Get-UIDoctor {
    param(
        [hashtable]$Config,
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR",
        [switch]$Staging
    )

    $appName = $Config.ui.appName
    $planName = $Config.gameService.appServicePlan  # UI shares the same plan
    $url = $Config.ui.url
    $rgName = $Config.resourceGroup
    $gameServiceUrl = $Config.gameService.url

    # Determine target based on -Staging flag
    $slot = if ($Staging) { "staging" } else { "" }
    $targetUrl = if ($Staging) { "https://$appName-staging.azurewebsites.net" } else { $url }
    $targetLabel = if ($Staging) { "staging" } else { "production" }
    $gameServiceSettingName = if ($Staging) { 'NEXT_PUBLIC_GAME_SERVICE_URL' } else { 'GAMESERVICE_URL' }

    Write-Log -Level "DEBUG" -Message "Get-UIDoctor started (target: $targetLabel)" -TraceLevel $TraceLevel

    $result = @{
        resource  = "ui"
        name      = if ($Staging) { "$appName (staging)" } else { $appName }
        url       = $targetUrl
        status    = "unknown"
        healthy   = $false
        timestamp = (Get-Date -Format "o")
        # Detailed checks for each install/deploy step
        checks    = @{
            resourceGroup          = $false
            appServicePlan         = $false
            webApp                 = $false
            managedIdentity        = $false
            gameServiceUrl         = $false
            codeDeployed           = $false
            siteResponding         = $false
        }
        # Git commit tracking
        currentCommit         = $null
        deployedCommit        = $null
        # What actions are needed
        needsInstall          = $false
        needsDeploy           = $false
    }

    try {
        # Check resource group exists
        Write-Log -Level "STATUS" -Message "Checking resource group..."
        Write-Log -Level "DEBUG" -Message "Checking resource group: $rgName" -TraceLevel $TraceLevel
        $rg = Invoke-AzCommand "group show --name $rgName" -FailOnError $false -JsonOutput
        $result.checks.resourceGroup = ($null -ne $rg)

        # Check App Service Plan exists
        Write-Log -Level "STATUS" -Message "Checking app service plan..."
        Write-Log -Level "DEBUG" -Message "Checking app service plan: $planName" -TraceLevel $TraceLevel
        $plan = Invoke-AzCommand "appservice plan show --name $planName --resource-group $rgName" -FailOnError $false -JsonOutput
        $result.checks.appServicePlan = ($null -ne $plan)

        # Check web app exists
        Write-Log -Level "STATUS" -Message "Checking web app..."
        Write-Log -Level "DEBUG" -Message "Checking web app: $appName" -TraceLevel $TraceLevel
        $app = Invoke-AzCommand "webapp show --name $appName --resource-group $rgName" -FailOnError $false -JsonOutput
        if (-not $app) {
            $result.status = "not-found"
            $result.needsInstall = $true
            Write-Log -Level "DEBUG" -Message "Web app not found, needsInstall=true" -TraceLevel $TraceLevel
            Complete-StatusMessage
            return $result
        }
        $result.checks.webApp = $true
        $result.status = $app.state.ToLower()

        # --- Parallel batch 1: config checks (all independent once web app exists) ---
        Write-Log -Level "STATUS" -Message "Checking $targetLabel configuration..."

        # Run independent az calls in parallel using background jobs
        $jobs = @()
        $jobs += Start-Job -ScriptBlock {
            $a = @("webapp", "config", "show", "--name", $using:appName, "--resource-group", $using:rgName, "--query", "linuxFxVersion", "-o", "tsv")
            if ($using:slot) { $a += "--slot", $using:slot }
            & az @a 2>$null
        }
        $jobs += Start-Job -ScriptBlock {
            $a = @("webapp", "identity", "show", "--name", $using:appName, "--resource-group", $using:rgName, "--query", "principalId", "-o", "tsv")
            if ($using:slot) { $a += "--slot", $using:slot }
            & az @a 2>$null
        }
        $jobs += Start-Job -ScriptBlock {
            $a = @("webapp", "config", "appsettings", "list", "--name", $using:appName, "--resource-group", $using:rgName)
            if ($using:slot) { $a += "--slot", $using:slot }
            & az @a 2>$null
        }

        # Wait for all jobs (with STATUS updates)
        $allDone = $false
        while (-not $allDone) {
            $allDone = ($jobs | Where-Object { $_.State -eq 'Running' }).Count -eq 0
            if (-not $allDone) { Start-Sleep -Milliseconds 500 }
        }

        # Collect results (any job may return $null for staging slots without config)
        $runtimeRaw = Receive-Job $jobs[0]
        $runtime = if ($runtimeRaw) { "$runtimeRaw".Trim() } else { "" }
        Write-Log -Level "DEBUG" -Message "$targetLabel runtime: $runtime" -TraceLevel $TraceLevel
        $result.prodRuntime = $runtime

        $identityRaw = Receive-Job $jobs[1]
        $identity = if ($identityRaw) { "$identityRaw".Trim() } else { "" }
        $result.checks.managedIdentity = (-not [string]::IsNullOrWhiteSpace($identity))

        $appSettingsRaw = Receive-Job $jobs[2]
        $appSettingsJson = if ($appSettingsRaw) { ($appSettingsRaw) -join "`n" } else { "" }
        $appSettings = if ($appSettingsJson) { $appSettingsJson | ConvertFrom-Json -ErrorAction SilentlyContinue } else { $null }
        $configuredUrl = if ($appSettings) { ($appSettings | Where-Object { $_.name -eq $gameServiceSettingName } | Select-Object -First 1).value } else { $null }
        $result.checks.gameServiceUrl = ($configuredUrl -eq $gameServiceUrl)
        $result.deployedCommit = if ($appSettings) { ($appSettings | Where-Object { $_.name -eq 'DEPLOY_COMMIT' } | Select-Object -First 1).value } else { $null }
        Write-Log -Level "DEBUG" -Message "Deployed commit: $($result.deployedCommit)" -TraceLevel $TraceLevel

        $jobs | Remove-Job -Force

        # Get current git commit
        $result.currentCommit = Get-GitCommitHash
        Write-Log -Level "DEBUG" -Message "Current git commit: $($result.currentCommit)" -TraceLevel $TraceLevel

        # Check if code has been deployed
        $result.checks.codeDeployed = (-not [string]::IsNullOrWhiteSpace($result.deployedCommit))

        # Check if deploy is needed (commit mismatch)
        if (-not $result.checks.codeDeployed) {
            $result.needsDeploy = $true
        }
        elseif ($result.currentCommit -ne $result.deployedCommit) {
            # Commits differ — only flag NEEDS DEPLOY if frontend files actually changed.
            $changedFiles = @(git -C $ProjectRoot diff --name-only "$($result.deployedCommit)..$($result.currentCommit)" 2>$null)
            $deployableChanges = @($changedFiles | Where-Object { $_ -match '^react-ui/' })
            if ($deployableChanges.Count -gt 0) {
                $result.needsDeploy = $true
            }
        }

        # --- HTTP health check ---
        Write-Log -Level "STATUS" -Message "Checking $targetLabel site health..."

        $httpJob = Start-Job -ScriptBlock {
            try {
                $response = Invoke-WebRequest -Uri $using:targetUrl -TimeoutSec 15 -UseBasicParsing -ErrorAction Stop
                return @{ ok = $true; status = $response.StatusCode }
            } catch {
                return @{ ok = $false; error = $_.Exception.Message }
            }
        }

        $httpJob | Wait-Job | Out-Null

        # Collect HTTP result
        $httpResult = Receive-Job $httpJob
        if ($httpResult.ok) {
            $result.checks.siteResponding = $true
            $result.checks.codeDeployed = $true
            $result.checks.gameServiceUrl = $true
        } else {
            $result.checks.siteResponding = $false
            Write-Log -Level "DEBUG" -Message "$targetLabel site not responding: $($httpResult.error)" -TraceLevel $TraceLevel
            # Only set needsDeploy if code isn't deployed or commit doesn't match
            # A timeout with matching commit means cold start, not missing deploy
            if (-not $result.checks.codeDeployed) {
                $result.needsDeploy = $true
            }
        }

        $httpJob | Remove-Job -Force

        # Determine overall health
        # Site responding is the best signal, but code deployed + commit match
        # with no response is a cold start, not an unhealthy deployment
        if ($result.checks.siteResponding) {
            $result.healthy = $true
        }
        elseif ($result.checks.codeDeployed -and -not $result.needsDeploy) {
            $result.healthy = $true
            $result.coldStart = $true
        }

        Complete-StatusMessage
        Write-Log -Level "DEBUG" -Message "UI doctor complete: healthy=$($result.healthy), needsDeploy=$($result.needsDeploy)" -TraceLevel $TraceLevel
    }
    catch {
        Complete-StatusMessage
        $result.status = "error"
        $result.error = $_.Exception.Message
        Write-Log -Level "DEBUG" -Message "UI doctor error: $($_.Exception.Message)" -TraceLevel $TraceLevel
    }

    return $result
}

#endregion

#region Clean Functions

<#
.SYNOPSIS
    Deletes the GameService Azure Web App.
.DESCRIPTION
    Removes the GameService web app from Azure if it exists.
.PARAMETER Config
    Azure configuration hashtable
.OUTPUTS
    Boolean - $true on success
#>
function Clean-GameService {
    param([hashtable]$Config)

    $rgName = $Config.resourceGroup
    $appName = $Config.gameService.appName

    $existing = Invoke-AzCommand "webapp show --name $appName --resource-group $rgName" -FailOnError $false -JsonOutput
    if ($existing) {
        Write-Log -Level "INFO" -Message "Deleting GameService App: $appName"
        Invoke-AzCommand "webapp delete --name $appName --resource-group $rgName" -SuppressOutput
        Write-Log -Level "INFO" -Message "GameService App deleted"
    }
    else {
        Write-Log -Level "INFO" -Message "GameService App does not exist"
    }

    return $true
}

<#
.SYNOPSIS
    Deletes the WebUI Azure Web App.
.DESCRIPTION
    Removes the WebUI web app from Azure if it exists.
.PARAMETER Config
    Azure configuration hashtable
.OUTPUTS
    Boolean - $true on success
#>
function Clean-UI {
    param([hashtable]$Config)

    $rgName = $Config.resourceGroup
    $appName = $Config.ui.appName

    $existing = Invoke-AzCommand "webapp show --name $appName --resource-group $rgName" -FailOnError $false -JsonOutput
    if ($existing) {
        Write-Log -Level "INFO" -Message "Deleting UI App: $appName"
        Invoke-AzCommand "webapp delete --name $appName --resource-group $rgName" -SuppressOutput
        Write-Log -Level "INFO" -Message "UI App deleted"
    }
    else {
        Write-Log -Level "INFO" -Message "UI App does not exist"
    }

    return $true
}

#endregion

#region Output Functions

<#
.SYNOPSIS
    Displays doctor result in a formatted table.
.DESCRIPTION
    Takes a doctor result hashtable and displays it as a formatted table
    showing each check, its status, and recommended action.
.PARAMETER Result
    The doctor result hashtable
.PARAMETER Config
    Azure configuration hashtable (optional, for URL display)
#>
function Show-DoctorResult {
    param(
        [hashtable]$Result,
        [hashtable]$Config
    )

    # Column widths
    $col1 = 25  # Check name
    $col2 = 12  # Status

    # Header
    Write-Log -Level INFO -Message "" -NoLabel
    Write-Log -Level INFO -Message "$($Result.resource) ($($Result.name))" -NoLabel -ForegroundColor Cyan
    Write-Log -Level INFO -Message ("-" * 60) -NoLabel
    Write-Log -Level INFO -Message ("Check".PadRight($col1) + "Status".PadRight($col2) + "Details") -NoLabel -ForegroundColor Gray

    # Helper to show a check row
    function Show-CheckRow {
        param([string]$Name, [bool]$Status, [string]$Details = "")
        $statusText = if ($Status) { "OK" } else { "MISSING" }
        $statusColor = if ($Status) { "Green" } else { "Red" }

        # Build the full row as one string to avoid -NoNewline issues
        $row = ("  " + $Name).PadRight($col1) + $statusText.PadRight($col2)
        if ($Details) { $row += $Details }
        Write-Log -Level INFO -Message $row -NoLabel -ForegroundColor $statusColor
    }

    # Show checks based on resource type
    if ($Result.checks) {
        foreach ($key in $Result.checks.Keys | Sort-Object) {
            $displayName = switch ($key) {
                "resourceGroup" { "Resource Group" }
                "appServicePlan" { "App Service Plan" }
                "webApp" { "Web App" }
                "managedIdentity" { "Managed Identity" }
                "alwaysOn" { "Always On" }
                "startupTimeout" { "Startup Timeout" }
                "planSkuOk" { "Plan SKU" }
                "codeDeployed" { "Code Deployed" }
                "healthEndpoint" { "Health Endpoint" }
                "sqlServer" { "SQL Server" }
                "publicNetworkAccess" { "Public Network Access" }
                "firewallRule" { "Firewall Rule" }
                "database" { "Database" }
                "connectionString" { "Connection String" }
                "connectionPooling" { "Connection Pooling" }
                "managedIdentityUser" { "DB User (MI)" }
                "gameServiceConnected" { "GameService Connected" }
                "gameServiceUrl" { "GameService URL" }
                "siteResponding" { "Site Responding" }
                "appRegistration" { "App Registration" }
                "servicePrincipal" { "Service Principal" }
                "federatedMain" { "Federated (main)" }
                "federatedStaging" { "Federated (staging)" }
                "contributorRole" { "Contributor Role" }
                default { $key }
            }

            $action = ""
            if (-not $Result.checks[$key]) {
                $noun = $Result.resource  # e.g. "ui", "game-service", "database"
                $action = if ($key -eq "siteResponding") {
                    if ($Result.checks.codeDeployed -and -not $Result.needsDeploy) { "cold start — browse URL to wake" } else { "$script:CmdHintPrefix $noun deploy" }
                } elseif ($key -in @("codeDeployed", "healthEndpoint", "connectionString", "connectionPooling", "managedIdentityUser", "gameServiceConnected")) {
                    "$script:CmdHintPrefix $noun deploy"
                } elseif ($key -eq "planSkuOk") {
                    "current: $($Result.currentSku), need: B1+"
                } elseif ($key -eq "alwaysOn") {
                    "(requires B1+ SKU)"
                } elseif ($key -eq "startupTimeout") {
                    "$script:CmdHintPrefix $noun install"
                } elseif ($key -in @("publicNetworkAccess", "firewallRule")) {
                    "$script:CmdHintPrefix $noun fix"
                } elseif ($key -in @("appRegistration", "servicePrincipal", "federatedMain", "federatedStaging", "contributorRole")) {
                    "$script:CmdHintPrefix github install"
                } else {
                    "$script:CmdHintPrefix $noun install"
                }
            }
            Show-CheckRow -Name $displayName -Status $Result.checks[$key] -Details $action
        }
    }

    # Show runtime info if available
    if ($Result.prodRuntime) {
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level INFO -Message ("  Runtime").PadRight($col1) -NoLabel -NoNewline
        $rtLabel = if ($Result.prodRuntime -like "DOTNETCORE*") { ".NET ($($Result.prodRuntime))" }
                   elseif ($Result.prodRuntime -like "NODE*") { "React/Next.js ($($Result.prodRuntime))" }
                   else { $Result.prodRuntime }
        $rtColor = if ($Result.checks.siteResponding) { "Green" } else { "Yellow" }
        Write-Log -Level INFO -Message $rtLabel -ForegroundColor $rtColor -NoLabel
    }

    # Show git commit info if available
    if ($Result.currentCommit -or $Result.deployedCommit) {
        Write-Log -Level INFO -Message "" -NoLabel
        if ($Result.currentCommit -eq $Result.deployedCommit -and $Result.deployedCommit) {
            $commitRow = ("  Git Commit").PadRight($col1) + "MATCH".PadRight($col2) + $Result.currentCommit
            Write-Log -Level INFO -Message $commitRow -NoLabel -ForegroundColor Green
        } elseif ($Result.deployedCommit -and $Result.deployedCommit -ne "local") {
            if ($Result.needsDeploy) {
                $commitRow = ("  Git Commit").PadRight($col1) + "MISMATCH".PadRight($col2) + "deployed: $($Result.deployedCommit) -> current: $($Result.currentCommit)"
                Write-Log -Level INFO -Message $commitRow -NoLabel -ForegroundColor Yellow
            } else {
                $commitRow = ("  Git Commit").PadRight($col1) + "OK".PadRight($col2) + "deployed: $($Result.deployedCommit) (no deployable changes)"
                Write-Log -Level INFO -Message $commitRow -NoLabel -ForegroundColor Green
            }
        } else {
            $commitRow = ("  Git Commit").PadRight($col1) + "NONE".PadRight($col2) + "not yet deployed"
            Write-Log -Level INFO -Message $commitRow -NoLabel -ForegroundColor Yellow
        }
    }

    # Show build time if available
    if ($Result.deployedBuildTime -and $Result.deployedBuildTime -ne "unknown") {
        Write-Log -Level INFO -Message ("  Build Time").PadRight($col1) -NoLabel -NoNewline
        Write-Log -Level INFO -Message "DEPLOYED".PadRight($col2) -NoLabel -NoNewline -ForegroundColor Green
        Write-Log -Level INFO -Message "$($Result.deployedBuildTime)" -NoLabel
    }

    # Show database status if available
    if ($Result.dbStatus) {
        Write-Log -Level INFO -Message ("  Database Status").PadRight($col1) -NoLabel -NoNewline
        $dbColor = switch ($Result.dbStatus) {
            "Online" { "Green" }
            "Paused" { "Yellow" }
            default { "Red" }
        }
        Write-Log -Level INFO -Message $Result.dbStatus -ForegroundColor $dbColor -NoLabel
    }

    # Show diagnostic issue from health endpoint if available
    if ($Result.diagnosticIssue) {
        Write-Log -Level INFO -Message ("  Diagnostic Issue").PadRight($col1) -NoLabel -NoNewline
        $issueColor = switch ($Result.diagnosticIssue) {
            "None" { "Green" }
            "DatabasePaused" { "Yellow" }
            "ConnectionTimeout" { "Yellow" }
            default { "Red" }
        }
        Write-Log -Level INFO -Message $Result.diagnosticIssue -ForegroundColor $issueColor -NoLabel
    }

    # Show Azure database status from diagnostics if different from local check
    if ($Result.azureDatabaseStatus -and $Result.azureDatabaseStatus -ne $Result.dbStatus) {
        Write-Log -Level INFO -Message ("  Azure DB Status").PadRight($col1) -NoLabel -NoNewline
        $azureDbColor = switch ($Result.azureDatabaseStatus) {
            "Online" { "Green" }
            "Paused" { "Yellow" }
            default { "Red" }
        }
        Write-Log -Level INFO -Message $Result.azureDatabaseStatus -ForegroundColor $azureDbColor -NoLabel
    }

    # Summary line
    Write-Log -Level INFO -Message "" -NoLabel
    if ($Result.needsInstall) {
        Write-Log -Level INFO -Message "Status: NEEDS INSTALL" -NoLabel -ForegroundColor Red
        Write-Log -Level INFO -Message "  Recommended: $script:CmdHintPrefix $($Result.resource) install" -NoLabel -ForegroundColor Cyan
    } elseif ($Result.needsFix) {
        Write-Log -Level INFO -Message "Status: NEEDS FIX" -NoLabel -ForegroundColor Yellow
        Write-Log -Level INFO -Message "  Recommended: $script:CmdHintPrefix $($Result.resource) fix" -NoLabel -ForegroundColor Cyan
    } elseif ($Result.needsDeploy) {
        Write-Log -Level INFO -Message "Status: NEEDS DEPLOY" -NoLabel -ForegroundColor Yellow
        if ($Result.deployReason) {
            Write-Log -Level INFO -Message "  Reason: $($Result.deployReason)" -NoLabel
        }
        Write-Log -Level INFO -Message "  Recommended: $script:CmdHintPrefix $($Result.resource) deploy" -NoLabel -ForegroundColor Cyan
    } elseif ($Result.healthy -and $Result.coldStart) {
        Write-Log -Level INFO -Message "Status: HEALTHY (site not responding — likely cold start, browse URL to wake)" -NoLabel -ForegroundColor Yellow
    } elseif ($Result.healthy) {
        Write-Log -Level INFO -Message "Status: HEALTHY" -NoLabel -ForegroundColor Green
    } else {
        Write-Log -Level INFO -Message "Status: UNKNOWN" -NoLabel -ForegroundColor Red
    }

    # Show staging deploy status separately
    if ($Result.needsStagingDeploy) {
        Write-Log -Level INFO -Message "Staging: NEEDS DEPLOY" -NoLabel -ForegroundColor Yellow
        Write-Log -Level INFO -Message "  Recommended: $script:CmdHintPrefix ui deploy-staging" -NoLabel -ForegroundColor Cyan
    }

    # Show performance warnings if any
    if ($Result.performanceWarnings) {
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level INFO -Message "Performance Warnings:" -NoLabel -ForegroundColor Yellow
        foreach ($warning in $Result.performanceWarnings) {
            Write-Log -Level INFO -Message "  ⚠️  $warning" -NoLabel -ForegroundColor Yellow
        }
    }

    # Show note if any
    if ($Result.note) {
        Write-Log -Level INFO -Message "  Note: $($Result.note)" -NoLabel -ForegroundColor Yellow
    }

    # Show error if any
    if ($Result.error) {
        Write-Log -Level INFO -Message "  Error: $($Result.error)" -NoLabel -ForegroundColor Red
    }
}

<#
.SYNOPSIS
    Runs a performance test against the GameService API.
.DESCRIPTION
    Makes multiple HTTP requests to test cold start and warm response times.
.PARAMETER Config
    Azure configuration hashtable
.OUTPUTS
    Performance test results with timing information
#>
function Test-GameServicePerformance {
    param(
        [hashtable]$Config
    )

    $url = $Config.gameService.url
    $testCount = 5
    $times = @()

    Write-Log -Level INFO -Message "" -NoLabel
    Write-Log -Level INFO -Message "Performance Test: $url" -NoLabel -ForegroundColor Cyan
    Write-Log -Level INFO -Message "=" * 50 -NoLabel
    Write-Log -Level INFO -Message "" -NoLabel
    Write-Log -Level INFO -Message "Running $testCount sequential requests to /api/players..." -NoLabel
    Write-Log -Level INFO -Message "" -NoLabel

    for ($i = 1; $i -le $testCount; $i++) {
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            $response = Invoke-RestMethod -Uri "$url/api/players" -TimeoutSec 60 -ErrorAction Stop
            $stopwatch.Stop()
            $elapsed = $stopwatch.Elapsed.TotalSeconds
            $times += $elapsed

            $color = if ($elapsed -lt 1) { "Green" } elseif ($elapsed -lt 3) { "Yellow" } else { "Red" }
            $status = if ($elapsed -lt 1) { "FAST" } elseif ($elapsed -lt 3) { "SLOW" } else { "VERY SLOW" }

            Write-Log -Level INFO -Message ("  Request {0}: {1,6:N2}s  " -f $i, $elapsed) -NoLabel -NoNewline
            Write-Log -Level INFO -Message $status -ForegroundColor $color -NoLabel
        }
        catch {
            $stopwatch.Stop()
            Write-Log -Level ERROR -Message ("  Request {0}: FAILED - {1}" -f $i, $_.Exception.Message)
        }

        # Small delay between requests
        Start-Sleep -Milliseconds 200
    }

    Write-Log -Level INFO -Message "" -NoLabel
    Write-Log -Level INFO -Message "Summary:" -NoLabel -ForegroundColor Cyan
    Write-Log -Level INFO -Message "-" * 30 -NoLabel

    if ($times.Count -gt 0) {
        $min = ($times | Measure-Object -Minimum).Minimum
        $max = ($times | Measure-Object -Maximum).Maximum
        $avg = ($times | Measure-Object -Average).Average
        $first = $times[0]
        $warmAvg = if ($times.Count -gt 1) { ($times[1..($times.Count-1)] | Measure-Object -Average).Average } else { $first }

        Write-Log -Level INFO -Message ("  First request (cold):  {0,6:N2}s" -f $first) -NoLabel
        Write-Log -Level INFO -Message ("  Warm average:          {0,6:N2}s" -f $warmAvg) -NoLabel
        Write-Log -Level INFO -Message ("  Min / Max:             {0,6:N2}s / {1:N2}s" -f $min, $max) -NoLabel
        Write-Log -Level INFO -Message "" -NoLabel

        # Performance assessment
        if ($first -gt 10) {
            Write-Log -Level WARN -Message "⚠️  Cold start is very slow (>10s). Check:"
            Write-Log -Level INFO -Message "   - App Service Plan SKU (needs B1+ for Always On)" -NoLabel
            Write-Log -Level INFO -Message "   - Always On setting (prevents cold starts)" -NoLabel
            Write-Log -Level INFO -Message "   - Azure SQL auto-pause (may need to wake up)" -NoLabel
        }
        elseif ($first -gt 5) {
            Write-Log -Level WARN -Message "⚠️  Cold start is slow (>5s). Consider:"
            Write-Log -Level INFO -Message "   - Enabling Always On if not already enabled" -NoLabel
        }
        else {
            Write-Log -Level INFO -Message "✅ Cold start is acceptable (<5s)" -NoLabel -ForegroundColor Green
        }

        if ($warmAvg -gt 2) {
            Write-Log -Level WARN -Message "⚠️  Warm requests are slow (>2s avg). Check:"
            Write-Log -Level INFO -Message "   - Connection pooling in connection string" -NoLabel
            Write-Log -Level INFO -Message "   - Azure SQL tier and capacity" -NoLabel
        }
        elseif ($warmAvg -gt 1) {
            Write-Log -Level WARN -Message "⚠️  Warm requests are a bit slow (>1s avg)"
        }
        else {
            Write-Log -Level INFO -Message "✅ Warm requests are good (<1s avg)" -NoLabel -ForegroundColor Green
        }

        # Check for high variance (indicates connection issues)
        if ($times.Count -gt 2) {
            $variance = $max - $min
            if ($variance -gt 5) {
                Write-Log -Level WARN -Message ("⚠️  High variance detected ({0:N2}s). May indicate:" -f $variance)
                Write-Log -Level INFO -Message "   - Connection pool exhaustion" -NoLabel
                Write-Log -Level INFO -Message "   - Network instability" -NoLabel
                Write-Log -Level INFO -Message "   - Token refresh issues (Managed Identity)" -NoLabel
            }
        }
    }
    else {
        Write-Log -Level ERROR -Message "  No successful requests - check service health"
    }

    Write-Log -Level INFO -Message "" -NoLabel
}

#endregion

#region Help

<#
.SYNOPSIS
    Displays help text for the catan-azure.ps1 script.
.DESCRIPTION
    Shows available commands, nouns, verbs, and usage examples.
#>
function Show-Help {
    $help = @"
Catan Azure Management Script
=============================

Manages Azure resources for the Catan3 application.

Usage:
    ./catan-azure.ps1 <verb>              Run verb on ALL resources
    ./catan-azure.ps1 <noun> <verb>       Run verb on specific resource

Nouns:
    ui              React UI (Next.js) application
    database        Azure SQL Serverless database
    game-service    GameService ASP.NET Core API
    github          GitHub Actions OIDC authentication

Verbs:
    install         Create Azure resources (idempotent)
    deploy          Deploy code/data to Azure
    doctor          Check health and status
    fix             Fix common connectivity issues (database only)
    clean           Delete Azure resources

Options:
    -Yes            Skip confirmation prompts
    -Force          Force deploy even if no changes detected
    -Json           Output doctor results as JSON
    -HashTable      Output doctor results as PowerShell hashtable
    -Staging        Check staging environment health (doctor only)
    -Perf           Run performance test (game-service doctor only)
    -TraceLevel     Output verbosity (ERROR, WARN, INFO, DEBUG)

Examples:
    ./catan-azure.ps1 doctor                   Check health of ALL resources
    ./catan-azure.ps1 doctor -Staging           Check staging environment health
    ./catan-azure.ps1 doctor -Perf             Check all health + run perf test
    ./catan-azure.ps1 install                  Create ALL Azure resources
    ./catan-azure.ps1 deploy                   Deploy ALL code/data
    ./catan-azure.ps1 game-service install     Create GameService resources only
    ./catan-azure.ps1 database deploy          Configure SQL connection string
    ./catan-azure.ps1 database fix             Fix SQL network access and firewall
    ./catan-azure.ps1 ui doctor -Json          Check UI health (JSON output)
    ./catan-azure.ps1 github install            Setup GitHub Actions OIDC
    ./catan-azure.ps1 game-service clean       Delete GameService only
"@
    Write-Log -Level INFO -Message $help -NoLabel
}

#endregion

#region Main

# Handle help
if ($Help -or $Noun -eq "help" -or (-not $Noun -and -not $Verb)) {
    Show-Help
    exit 0
}

# Validate parameters
# Allow verb-only calls (e.g., "./catan-azure.ps1 doctor" runs on all resources)
if (-not $Verb -and $Noun -notin @("doctor", "install", "deploy", "clean")) {
    Write-Log -Level "ERROR" -Message "Verb required. Use: install, deploy, doctor, clean"
    exit 1
}

# Check Azure login
if (-not (Test-AzureLogin)) {
    exit 1
}

# Load or initialize config
$config = Get-LocalConfig

# For install, ensure we have a base name
if ($Verb -eq "install") {
    if (-not $config.baseName) {
        $baseName = Get-AvailableBaseName
        $config = Initialize-ConfigFromBaseName -BaseName $baseName
        # Only persist baseName (+ auth if present) — all other names are derived
        $persistConfig = @{ baseName = $baseName }
        if ($config.auth) { $persistConfig.auth = $config.auth }
        Save-LocalConfig -Config $persistConfig
    }
    else {
        # baseName exists — derive all resource names (convention over configuration)
        $config = Initialize-ConfigFromBaseName -BaseName $config.baseName
        Write-Log -Level "INFO" -Message "Using existing base name: $($config.baseName)"
    }
}
elseif (-not $config.baseName) {
    Write-Log -Level "ERROR" -Message "No Azure configuration found. Run 'install' first."
    exit 1
}
else {
    # Non-install verbs: still need derived names populated
    $config = Initialize-ConfigFromBaseName -BaseName $config.baseName
}

# Execute operation
# Skip header for doctor with -Json or -HashTable output (clean API mode)
if ($Verb -ne "doctor" -or (-not $Json -and -not $HashTable)) {
    Write-Log -Level "HEADER" -Message "Catan Azure: $Noun $Verb"
    Write-Log -Level "HEADER" -Message ("=" * 40)
}

$success = $false

# Handle verb-only calls (e.g., "./catan-azure.ps1 doctor" runs doctor on all resources)
if ($Noun -in @("doctor", "install", "deploy", "clean") -and -not $Verb) {
    # Noun is actually a verb - run against all resources
    $actualVerb = $Noun

    switch ($actualVerb) {
        "doctor" {
            $envLabel = if ($Staging) { "staging" } else { "production" }

            # Run doctor on all resources
            $allHealthy = $true

            # GameService
            Write-Log -Level "HEADER" -Message "Catan Azure: game-service doctor ($envLabel)"
            Write-Log -Level "HEADER" -Message ("=" * 40)
            $gsResult = Get-GameServiceDoctor -Config $config -TraceLevel $TraceLevel -Staging:$Staging
            if ($Json) {
                # For JSON, collect all results
            } else {
                Show-DoctorResult -Result $gsResult -Config $config
                if ($Perf -and $gsResult.checks.healthEndpoint) {
                    Test-GameServicePerformance -Config $config
                }
            }
            if (-not $gsResult.healthy) { $allHealthy = $false }

            # Database (shared between production and staging)
            Write-Log -Level "HEADER" -Message "Catan Azure: database doctor"
            Write-Log -Level "HEADER" -Message ("=" * 40)
            $dbResult = Get-DatabaseDoctor -Config $config -TraceLevel $TraceLevel
            if (-not $Json) {
                Show-DoctorResult -Result $dbResult -Config $config
            }
            if (-not $dbResult.healthy) { $allHealthy = $false }

            # UI
            Write-Log -Level "HEADER" -Message "Catan Azure: ui doctor ($envLabel)"
            Write-Log -Level "HEADER" -Message ("=" * 40)
            $uiResult = Get-UIDoctor -Config $config -TraceLevel $TraceLevel -Staging:$Staging
            if (-not $Json) {
                Show-DoctorResult -Result $uiResult -Config $config
            }
            if (-not $uiResult.healthy) { $allHealthy = $false }

            # GitHub OIDC
            Write-Log -Level "HEADER" -Message "Catan Azure: github doctor"
            Write-Log -Level "HEADER" -Message ("=" * 40)
            $ghResult = Get-GitHubDoctor -Config $config -TraceLevel $TraceLevel
            if (-not $Json) {
                Show-DoctorResult -Result $ghResult -Config $config
            }
            if (-not $ghResult.healthy) { $allHealthy = $false }

            # Output JSON if requested
            if ($Json) {
                $allResults = @{
                    gameService = $gsResult
                    database = $dbResult
                    ui = $uiResult
                    github = $ghResult
                }
                Write-Output ($allResults | ConvertTo-Json -Depth 10)
            }

            # Show service URLs
            $baseName = $config.baseName
            Write-Log -Level INFO -Message "" -NoLabel
            Write-Log -Level INFO -Message "Service URLs ($envLabel):" -NoLabel -ForegroundColor Cyan
            if ($Staging) {
                Write-Log -Level INFO -Message "  WebUI:       https://$baseName-staging.azurewebsites.net" -NoLabel
                Write-Log -Level INFO -Message "  GameService: https://$baseName-api-staging.azurewebsites.net" -NoLabel
            } else {
                Write-Log -Level INFO -Message "  WebUI:       $($config.ui.url)" -NoLabel
                Write-Log -Level INFO -Message "  GameService: $($config.gameService.url)" -NoLabel
            }

            $success = $allHealthy
        }
        "install" {
            # Install all resources in order
            Write-Log -Level "HEADER" -Message "Catan Azure: install all"
            Write-Log -Level "HEADER" -Message ("=" * 40)
            $success = (Install-GameService -Config $config) -and
                       (Install-Database -Config $config) -and
                       (Install-UI -Config $config) -and
                       (Install-GitHubOidc -Config $config)
        }
        "deploy" {
            # Deploy all resources
            Write-Log -Level "HEADER" -Message "Catan Azure: deploy all"
            Write-Log -Level "HEADER" -Message ("=" * 40)
            $success = (Deploy-GameService -Config $config -Force $Force -NoBuild $NoBuild) -and
                       (Deploy-Database -Config $config -Force $Force) -and
                       (Deploy-UI -Config $config -Force $Force -NoBuild $NoBuild) -and
                       (Install-GitHubOidc -Config $config)
        }
        "clean" {
            $confirm = Get-UserConfirmation -Question "Delete ALL Azure resources?" -TraceLevel $TraceLevel -Yes:$Yes
            if ($confirm -ne 'Yes') {
                Write-Log -Level "INFO" -Message "Cancelled"
                exit 0
            }
            Write-Log -Level "HEADER" -Message "Catan Azure: clean all"
            Write-Log -Level "HEADER" -Message ("=" * 40)
            $success = (Clean-UI -Config $config) -and
                       (Clean-GameService -Config $config) -and
                       (Clean-Database -Config $config)
        }
    }

    if ($success) { exit 0 } else { exit 1 }
}

switch ($Noun) {
    "game-service" {
        switch ($Verb) {
            "install" { $success = Install-GameService -Config $config }
            "deploy" { $success = Deploy-GameService -Config $config -Force $Force -NoBuild $NoBuild -Slot $Slot }
            "doctor" {
                $result = Get-GameServiceDoctor -Config $config -TraceLevel $TraceLevel -Staging:$Staging
                if ($Json) {
                    Write-Output ($result | ConvertTo-Json -Depth 10)
                } elseif ($HashTable) {
                    Write-Output $result
                } else {
                    Show-DoctorResult -Result $result -Config $config
                    # Run performance test if -Perf flag is set
                    if ($Perf -and $result.checks.healthEndpoint) {
                        Test-GameServicePerformance -Config $config
                    }
                }
                $success = $result.healthy
            }
            "clean" {
                $confirm = Get-UserConfirmation -Question "Delete GameService?" -TraceLevel $TraceLevel -Yes:$Yes
                if ($confirm -ne 'Yes') {
                    Write-Log -Level "INFO" -Message "Cancelled"
                    exit 0
                }
                $success = Clean-GameService -Config $config
            }
        }
    }
    "database" {
        switch ($Verb) {
            "install" { $success = Install-Database -Config $config }
            "deploy" { $success = Deploy-Database -Config $config -Force $Force }
            "fix" { $success = Fix-Database -Config $config }
            "doctor" {
                $result = Get-DatabaseDoctor -Config $config -TraceLevel $TraceLevel
                if ($Json) {
                    Write-Output ($result | ConvertTo-Json -Depth 10)
                } elseif ($HashTable) {
                    Write-Output $result
                } else {
                    Show-DoctorResult -Result $result -Config $config
                }
                $success = $result.healthy
            }
            "clean" {
                $confirm = Get-UserConfirmation -Question "Delete SQL Server and database?" -TraceLevel $TraceLevel -Yes:$Yes
                if ($confirm -ne 'Yes') {
                    Write-Log -Level "INFO" -Message "Cancelled"
                    exit 0
                }
                $success = Clean-Database -Config $config
            }
            "deploy-staging-access" { $success = Grant-StagingDatabaseAccess -Config $config }
        }
    }
    "ui" {
        switch ($Verb) {
            "install" { $success = Install-UI -Config $config }
            "deploy" { $success = Deploy-UI -Config $config -Force $Force -NoBuild $NoBuild }
            "deploy-staging" { $success = Deploy-ReactStaging -Config $config -Force $Force -TraceLevel $TraceLevel -GameServiceUrl $GameServiceUrl }
            "swap" { $success = Invoke-UISwap -Config $config }
            "verify" {
                $verifySlot = if ($Slot) { $Slot } else { "production" }
                $success = Invoke-UIVerify -Config $config -Slot $verifySlot
            }
            "doctor" {
                $result = Get-UIDoctor -Config $config -TraceLevel $TraceLevel -Staging:$Staging
                if ($Json) {
                    Write-Output ($result | ConvertTo-Json -Depth 10)
                } elseif ($HashTable) {
                    Write-Output $result
                } else {
                    Show-DoctorResult -Result $result -Config $config
                }
                $success = $result.healthy
            }
            "clean" {
                $confirm = Get-UserConfirmation -Question "Delete UI?" -TraceLevel $TraceLevel -Yes:$Yes
                if ($confirm -ne 'Yes') {
                    Write-Log -Level "INFO" -Message "Cancelled"
                    exit 0
                }
                $success = Clean-UI -Config $config
            }
        }
    }
    "github" {
        switch ($Verb) {
            "install" { $success = Install-GitHubOidc -Config $config }
            "doctor" {
                $result = Get-GitHubDoctor -Config $config -TraceLevel $TraceLevel
                if ($Json) {
                    Write-Output ($result | ConvertTo-Json -Depth 10)
                } elseif ($HashTable) {
                    Write-Output $result
                } else {
                    Show-DoctorResult -Result $result -Config $config
                }
                $success = $result.healthy
            }
            default {
                Write-Log -Level "ERROR" -Message "Unknown verb '$Verb' for github. Use: install, doctor"
                exit 1
            }
        }
    }
}

if ($success) {
    exit 0
}
else {
    exit 1
}

#endregion

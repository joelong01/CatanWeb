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
    [ValidateSet("ui", "database", "game-service", "help", "doctor", "install", "deploy", "clean")]
    [string]$Noun,

    [Parameter(Position = 1)]
    [ValidateSet("install", "deploy", "doctor", "clean")]
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
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Import utility module for logging
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module "$ScriptDir/utility-scripts.psm1" -Force

# Set default TraceLevel for all Write-Log calls in this script
$PSDefaultParameterValues = @{
    'Write-Log:TraceLevel' = $TraceLevel
}

# Paths - script is in .scripts/, project root is parent
$ProjectRoot = Split-Path -Parent $ScriptDir
$AzureConfigDir = Join-Path $ProjectRoot ".azure"
$AzureConfigFile = Join-Path $AzureConfigDir "catan-azure.json"

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

#region Azure CLI Wrapper

<#
.SYNOPSIS
    Executes an Azure CLI command with logging and error handling.
.DESCRIPTION
    Wraps az CLI calls to provide consistent logging, error handling, and debugging.
    Logs the command being executed, captures output, and optionally fails on errors.
.PARAMETER Command
    The az CLI command arguments (without 'az' prefix)
.PARAMETER FailOnError
    If true (default), throws an error when az returns non-zero exit code
.PARAMETER SuppressOutput
    If true, doesn't return the command output (for commands where we don't need results)
.PARAMETER JsonOutput
    If true, parses the output as JSON and returns as object
.EXAMPLE
    Invoke-AzCommand "account show --query name -o tsv"
.EXAMPLE
    Invoke-AzCommand "group create --name rg-test --location westus2" -JsonOutput
#>
function Invoke-AzCommand {
    param(
        [Parameter(Mandatory, Position = 0)]
        [string]$Command,

        [bool]$FailOnError = $true,
        [switch]$SuppressOutput,
        [switch]$JsonOutput
    )

    Write-Log -Level "DEBUG" -Message "az $Command"

    # Execute and capture both stdout and stderr
    $output = $null
    $errorOutput = $null
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

    try {
        # Use Invoke-Expression to run the command
        $fullCommand = "az $Command 2>&1"
        $result = Invoke-Expression $fullCommand
        $exitCode = $LASTEXITCODE
        $stopwatch.Stop()

        # Separate stdout from stderr (error records)
        $output = $result | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] }
        $errorOutput = $result | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] }

        # Format duration for logging
        $elapsed = $stopwatch.Elapsed
        $durationStr = if ($elapsed.TotalMinutes -ge 1) {
            "{0:N1} min" -f $elapsed.TotalMinutes
        } elseif ($elapsed.TotalSeconds -ge 1) {
            "{0:N1}s" -f $elapsed.TotalSeconds
        } else {
            "{0:N0}ms" -f $elapsed.TotalMilliseconds
        }

        # Log duration on separate line - INFO for slow commands (>10s), DEBUG otherwise
        $durationLevel = if ($elapsed.TotalSeconds -ge 10) { "INFO" } else { "DEBUG" }
        Write-Log -Level $durationLevel -Message "  completed in $durationStr"

        if ($exitCode -ne 0) {
            $errorMsg = if ($errorOutput) { $errorOutput -join "`n" } else { "Command failed with exit code $exitCode" }
            Write-Log -Level "DEBUG" -Message "az exit code: $exitCode"

            if ($FailOnError) {
                Write-Log -Level "ERROR" -Message "az $Command"
                Write-Log -Level "ERROR" -Message $errorMsg
                throw "Azure CLI command failed: $errorMsg"
            }
            else {
                Write-Log -Level "DEBUG" -Message "Command failed (non-fatal): $errorMsg"
                return $null
            }
        }

        if ($SuppressOutput) {
            return $true
        }

        if ($JsonOutput -and $output) {
            return $output | ConvertFrom-Json
        }

        return $output
    }
    catch {
        $stopwatch.Stop()
        if ($FailOnError) {
            throw
        }
        return $null
    }
}

#endregion

#region Configuration Functions

<#
.SYNOPSIS
    Loads Azure configuration from the config file.
.DESCRIPTION
    Reads the catan-azure.json config file and returns it as a hashtable.
    If the file doesn't exist, returns a clone of the default configuration.
.OUTPUTS
    Hashtable containing Azure resource configuration (baseName, resourceGroup, etc.)
#>
function Get-AzureConfig {
    if (Test-Path $AzureConfigFile) {
        return Get-Content $AzureConfigFile -Raw | ConvertFrom-Json -AsHashtable
    }
    return $DefaultConfig.Clone()
}

<#
.SYNOPSIS
    Saves Azure configuration to the config file.
.DESCRIPTION
    Writes the configuration hashtable to catan-azure.json.
    Creates the .azure directory if it doesn't exist.
.PARAMETER Config
    Hashtable containing Azure resource configuration
#>
function Save-AzureConfig {
    param([hashtable]$Config)

    if (-not (Test-Path $AzureConfigDir)) {
        New-Item -ItemType Directory -Path $AzureConfigDir -Force | Out-Null
    }

    $Config | ConvertTo-Json -Depth 10 | Set-Content $AzureConfigFile
    Write-Log -Level "INFO" -Message "Configuration saved to $AzureConfigFile"
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
    Takes a base name and generates all derived resource names following
    Azure naming conventions (rg-*, st*, asp-*, ai-*, etc.)
.PARAMETER BaseName
    The base name to derive all resource names from
.OUTPUTS
    Hashtable containing complete Azure resource configuration
#>
function Initialize-ConfigFromBaseName {
    param([string]$BaseName)

    $config = Get-AzureConfig
    $config.baseName = $BaseName
    $config.resourceGroup = "rg-$BaseName"
    $config.storageAccount = "st$($BaseName -replace '-', '')"
    $config.gameService.appServicePlan = "asp-$BaseName"
    $config.gameService.appName = "$BaseName-api"
    $config.gameService.url = "https://$BaseName-api.azurewebsites.net"
    $config.ui.appName = $BaseName
    $config.ui.url = "https://$BaseName.azurewebsites.net"
    $config.appInsights.name = "ai-$BaseName"
    $config.sqlServer.serverName = "sql-$BaseName"
    $config.sqlServer.databaseName = "catan"
    $config.sqlServer.fqdn = "sql-$BaseName.database.windows.net"

    return $config
}

#endregion

#region Azure Auth Functions

<#
.SYNOPSIS
    Verifies Azure CLI login status.
.DESCRIPTION
    Checks if the user is logged into Azure CLI and displays account info.
    Returns false with guidance if not logged in.
.OUTPUTS
    Boolean - $true if logged in, $false otherwise
#>
function Test-AzureLogin {
    $account = Invoke-AzCommand "account show" -FailOnError $false -JsonOutput
    if (-not $account) {
        Write-Log -Level "ERROR" -Message "Not logged into Azure"
        Write-Log -Level "INFO" -Message "Please run: az login"
        return $false
    }
    Write-Log -Level "INFO" -Message "Logged in as: $($account.user.name)"
    Write-Log -Level "INFO" -Message "Subscription: $($account.name)"
    return $true
}

<#
.SYNOPSIS
    Registers an Azure resource provider.
.DESCRIPTION
    Checks if a provider is registered and registers it if needed.
    Waits up to 2 minutes for registration to complete.
.PARAMETER Namespace
    The provider namespace (e.g., "Microsoft.Storage", "Microsoft.Web")
.OUTPUTS
    Boolean - $true if registered successfully
#>
function Register-AzureProvider {
    param([string]$Namespace)

    $state = Invoke-AzCommand "provider show --namespace $Namespace --query registrationState -o tsv" -FailOnError $false
    if ($state -eq "Registered") {
        Write-Log -Level "DEBUG" -Message "Provider $Namespace already registered"
        return $true
    }

    Write-Log -Level "INFO" -Message "Registering provider: $Namespace"
    Invoke-AzCommand "provider register --namespace $Namespace" -SuppressOutput

    # Wait for registration to complete (up to 2 minutes)
    $maxWait = 120
    $waited = 0
    while ($waited -lt $maxWait) {
        Start-Sleep -Seconds 5
        $waited += 5
        $state = Invoke-AzCommand "provider show --namespace $Namespace --query registrationState -o tsv" -FailOnError $false
        if ($state -eq "Registered") {
            Write-Log -Level "INFO" -Message "Provider $Namespace registered"
            return $true
        }
        Write-Log -Level "DEBUG" -Message "Waiting for $Namespace registration... ($waited s)"
    }

    throw "Provider $Namespace registration timed out after $maxWait seconds"
}

#endregion

#region Resource Group Functions

<#
.SYNOPSIS
    Creates or verifies the Azure resource group.
.DESCRIPTION
    Checks if the resource group exists and creates it if not.
.PARAMETER Config
    Azure configuration hashtable containing resourceGroup and location
.OUTPUTS
    Boolean - $true on success
#>
function Install-ResourceGroup {
    param([hashtable]$Config)

    $rgName = $Config.resourceGroup
    $location = $Config.location

    Write-Log -Level "INFO" -Message "Checking resource group: $rgName"

    $existing = Invoke-AzCommand "group show --name $rgName" -FailOnError $false -JsonOutput
    if ($existing) {
        Write-Log -Level "INFO" -Message "Resource group exists: $rgName"
        return $true
    }

    Write-Log -Level "INFO" -Message "Creating resource group: $rgName in $location"
    Invoke-AzCommand "group create --name $rgName --location $location" -SuppressOutput
    Write-Log -Level "INFO" -Message "Resource group created: $rgName"
    return $true
}

<#
.SYNOPSIS
    Deletes the Azure resource group and all contained resources.
.DESCRIPTION
    Initiates async deletion of the resource group. This deletes ALL resources
    within the group including storage, web apps, and databases.
.PARAMETER Config
    Azure configuration hashtable containing resourceGroup
.OUTPUTS
    Boolean - $true if deletion started or group doesn't exist
#>
function Remove-ResourceGroup {
    param([hashtable]$Config)

    $rgName = $Config.resourceGroup

    $existing = Invoke-AzCommand "group show --name $rgName" -FailOnError $false -JsonOutput
    if (-not $existing) {
        Write-Log -Level "INFO" -Message "Resource group does not exist: $rgName"
        return $true
    }

    Write-Log -Level "WARN" -Message "Deleting resource group: $rgName (this deletes ALL resources)"
    Invoke-AzCommand "group delete --name $rgName --yes --no-wait" -SuppressOutput
    Write-Log -Level "INFO" -Message "Resource group deletion started: $rgName"
    return $true
}

#endregion

#region Application Insights Functions

<#
.SYNOPSIS
    Creates Application Insights resource.
.DESCRIPTION
    Creates an Application Insights resource for monitoring and telemetry.
    Returns the connection string for use by web apps.
.PARAMETER Config
    Azure configuration hashtable
.OUTPUTS
    String - Application Insights connection string
#>
function Install-AppInsights {
    param([hashtable]$Config)

    $rgName = $Config.resourceGroup
    $location = $Config.location
    $appInsightsName = $Config.appInsights.name

    # Ensure resource group exists
    Install-ResourceGroup -Config $Config | Out-Null

    Write-Log -Level "INFO" -Message "Checking Application Insights: $appInsightsName"

    $existing = Invoke-AzCommand "monitor app-insights component show --app $appInsightsName --resource-group $rgName" -FailOnError $false -JsonOutput
    if (-not $existing) {
        Write-Log -Level "INFO" -Message "Creating Application Insights: $appInsightsName"
        Invoke-AzCommand "monitor app-insights component create --app $appInsightsName --resource-group $rgName --location $location --kind web --application-type web" -SuppressOutput
        Write-Log -Level "INFO" -Message "Application Insights created: $appInsightsName"
    }
    else {
        Write-Log -Level "INFO" -Message "Application Insights exists: $appInsightsName"
    }

    # Get the connection string
    $connectionString = Invoke-AzCommand "monitor app-insights component show --app $appInsightsName --resource-group $rgName --query connectionString -o tsv"

    return $connectionString
}

<#
.SYNOPSIS
    Gets Application Insights connection string.
.PARAMETER Config
    Azure configuration hashtable
.OUTPUTS
    String - Connection string or $null if not found
#>
function Get-AppInsightsConnectionString {
    param([hashtable]$Config)

    $rgName = $Config.resourceGroup
    $appInsightsName = $Config.appInsights.name

    $connectionString = Invoke-AzCommand "monitor app-insights component show --app $appInsightsName --resource-group $rgName --query connectionString -o tsv" -FailOnError $false
    return $connectionString
}

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
    Install-ResourceGroup -Config $Config | Out-Null

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
        }
        # What actions are needed
        needsInstall = $false
        needsDeploy  = $false
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
            $result.needsInstall = $true
        }

        # Check firewall rule exists
        Write-Log -Level "DEBUG" -Message "Checking firewall rule" -TraceLevel $TraceLevel
        $fwRule = Invoke-AzCommand "sql server firewall-rule show --server $sqlServerName --resource-group $rgName --name AllowAzureServices" -FailOnError $false -JsonOutput
        $result.checks.firewallRule = ($null -ne $fwRule)
        if (-not $result.checks.firewallRule) {
            $result.needsInstall = $true
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

        # Check GameService database connection using health endpoint with forced database check
        Write-Log -Level "DEBUG" -Message "Checking GameService database connection" -TraceLevel $TraceLevel
        try {
            # Use checkDatabase=true to force fresh database diagnostics
            $health = Invoke-RestMethod -Uri "$gameServiceUrl/health?checkDatabase=true" -TimeoutSec 60
            if ($health.database.provider -eq "SqlServer") {
                # Check if database diagnostics show connection success
                if ($health.databaseDiagnostics -and $health.databaseDiagnostics.connected -eq $true) {
                    $result.status = "connected"
                    $result.checks.gameServiceConnected = $true
                    $result.checks.managedIdentityUser = $true  # If connected, user must exist
                    $result.checks.connectionString = $true     # If connected, connection string must be configured
                    $result.checks.connectionPooling = $true    # Assume pooling is configured if connected
                    $result.healthy = $true
                }
                elseif ($health.databaseDiagnostics) {
                    # Database diagnostics available but not connected
                    $result.checks.gameServiceConnected = $false
                    $result.databaseDiagnostics = $health.databaseDiagnostics

                    # Extract diagnostic info
                    $diag = $health.databaseDiagnostics
                    if ($diag.issue) {
                        $result.diagnosticIssue = $diag.issue
                    }
                    if ($diag.recommendation) {
                        $result.note = $diag.recommendation
                    }
                    if ($diag.status) {
                        $result.azureDatabaseStatus = $diag.status
                    }
                    if ($diag.publicNetworkAccess) {
                        $result.checks.publicNetworkAccess = ($diag.publicNetworkAccess -eq "Enabled")
                    }

                    # Mark as needing deploy if there's a fixable issue
                    $result.needsDeploy = $true
                }
                else {
                    # No diagnostics available but service is up
                    $result.checks.gameServiceConnected = $false
                    $result.needsDeploy = $true
                }
            }
        }
        catch {
            $result.checks.gameServiceConnected = $false
            Write-Log -Level "DEBUG" -Message "GameService health check failed: $_" -TraceLevel $TraceLevel

            # Only check connection string from Azure if GameService not responding
            $connStrings = Invoke-AzCommand "webapp config connection-string list --name $gameServiceAppName --resource-group $rgName" -FailOnError $false -JsonOutput
            $result.checks.connectionString = ($connStrings -and $connStrings.AzureSql)

            # Check if connection pooling is configured in connection string
            if ($connStrings -and $connStrings.AzureSql) {
                $connStr = $connStrings.AzureSql.value
                $result.checks.connectionPooling = ($connStr -match "Pooling=True" -or $connStr -match "Min Pool Size")
                if (-not $result.checks.connectionPooling) {
                    $result.needsDeploy = $true
                }
            }

            if (-not $result.note) {
                $result.note = "GameService not responding - may need deploy or database may be resuming from pause"
            }
            $result.needsDeploy = $true
        }

        # Healthy if GameService can connect to database
        if ($result.checks.gameServiceConnected) {
            $result.healthy = $true
            $result.needsDeploy = $false
        }
        elseif ($result.checks.sqlServer -and $result.checks.database) {
            # Infrastructure exists but connection not working
            $result.healthy = ($db.status -eq "Online" -or $db.status -eq "Paused")
            $result.needsDeploy = $true
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

<#
.SYNOPSIS
    Creates or verifies the Azure App Service Plan.
.DESCRIPTION
    Creates a Linux App Service Plan (B1 SKU) if it doesn't exist.
    This plan hosts both GameService and WebUI apps.
.PARAMETER Config
    Azure configuration hashtable
.OUTPUTS
    Boolean - $true on success
#>
function Install-AppServicePlan {
    param([hashtable]$Config)

    $rgName = $Config.resourceGroup
    $planName = $Config.gameService.appServicePlan
    $location = $Config.location

    # Ensure Microsoft.Web provider is registered
    Register-AzureProvider -Namespace "Microsoft.Web"

    Write-Log -Level "INFO" -Message "Checking App Service Plan: $planName"

    $existing = Invoke-AzCommand "appservice plan show --name $planName --resource-group $rgName" -FailOnError $false -JsonOutput
    if (-not $existing) {
        # IMPORTANT: --number-of-workers 1 is required because GameStateMachineRegistry uses
        # an in-memory dictionary. Multiple instances would have separate dictionaries and
        # players on different instances couldn't see the same game state.
        Write-Log -Level "INFO" -Message "Creating App Service Plan: $planName (B1, single instance)"
        Invoke-AzCommand "appservice plan create --name $planName --resource-group $rgName --location $location --sku B1 --is-linux --number-of-workers 1" -SuppressOutput
        Write-Log -Level "INFO" -Message "App Service Plan created: $planName"
    }
    else {
        # Check if SKU needs upgrade (F1/D1 don't support Always On)
        $currentSku = $existing.sku.name
        if ($currentSku -in @("F1", "D1")) {
            Write-Log -Level "INFO" -Message "Upgrading App Service Plan from $currentSku to B1 (required for Always On)"
            Invoke-AzCommand "appservice plan update --name $planName --resource-group $rgName --sku B1" -SuppressOutput
            Write-Log -Level "INFO" -Message "App Service Plan upgraded to B1"
        }
        else {
            Write-Log -Level "INFO" -Message "App Service Plan exists: $planName (SKU: $currentSku)"
        }
    }

    return $true
}

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
    Install-ResourceGroup -Config $Config | Out-Null
    Install-AppServicePlan -Config $Config | Out-Null

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

    # Install and connect Application Insights
    $appInsightsConnectionString = Install-AppInsights -Config $Config
    if ($appInsightsConnectionString) {
        Write-Log -Level "INFO" -Message "Connecting Application Insights to $appName..."
        Invoke-AzCommand "webapp config appsettings set --name $appName --resource-group $rgName --settings APPLICATIONINSIGHTS_CONNECTION_STRING=`"$appInsightsConnectionString`"" -SuppressOutput
    }

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

    Write-Log -Level "INFO" -Message "GameService App ready: $appName"
    return $true
}

<#
.SYNOPSIS
    Creates and configures the WebUI Azure Web App.
.DESCRIPTION
    Creates the .NET 9.0 web app for the Blazor WebAssembly frontend,
    enables managed identity, and configures GameService URL.
.PARAMETER Config
    Azure configuration hashtable
.OUTPUTS
    Boolean - $true on success
#>
function Install-UI {
    param([hashtable]$Config)

    $rgName = $Config.resourceGroup
    $planName = $Config.gameService.appServicePlan
    $appName = $Config.ui.appName

    # Ensure resource group and plan exist
    Install-ResourceGroup -Config $Config | Out-Null
    Install-AppServicePlan -Config $Config | Out-Null

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

    # Install and connect Application Insights
    $appInsightsConnectionString = Install-AppInsights -Config $Config
    if ($appInsightsConnectionString) {
        Write-Log -Level "INFO" -Message "Connecting Application Insights to $appName..."
        Invoke-AzCommand "webapp config appsettings set --name $appName --resource-group $rgName --settings APPLICATIONINSIGHTS_CONNECTION_STRING=`"$appInsightsConnectionString`"" -SuppressOutput
    }

    # Enable managed identity (for future extensibility)
    Write-Log -Level "INFO" -Message "Enabling managed identity for $appName..."
    Invoke-AzCommand "webapp identity assign --name $appName --resource-group $rgName" -FailOnError $false -SuppressOutput

    # Configure app settings
    Write-Log -Level "INFO" -Message "Configuring app settings..."
    Invoke-AzCommand "webapp config appsettings set --name $appName --resource-group $rgName --settings GAMESERVICE_URL=$($Config.gameService.url)" -SuppressOutput

    return $true
}

<#
.SYNOPSIS
    Gets the current git commit hash for change detection.
.DESCRIPTION
    Returns the short git commit hash of HEAD for tracking deployments.
.OUTPUTS
    String - The short commit hash (7 chars)
#>
function Get-GitCommitHash {
    try {
        $hash = git -C $ProjectRoot rev-parse --short HEAD 2>$null
        return $hash.Trim()
    }
    catch {
        return "unknown"
    }
}

<#
.SYNOPSIS
    Checks if deployment is needed by comparing git commit hashes.
.DESCRIPTION
    Compares the current git commit hash with the deployed version stored
    in Azure app settings. Returns true if deployment is needed.
.PARAMETER AppName
    The Azure web app name
.PARAMETER ResourceGroup
    The Azure resource group name
.PARAMETER Force
    If true, always returns true (skip check)
.OUTPUTS
    Boolean - $true if deployment is needed, $false if up-to-date
#>
function Test-DeploymentNeeded {
    param(
        [string]$AppName,
        [string]$ResourceGroup,
        [bool]$Force
    )

    if ($Force) {
        Write-Log -Level "INFO" -Message "Force deploy requested"
        return $true
    }

    $currentHash = Get-GitCommitHash
    Write-Log -Level "DEBUG" -Message "Current git commit: $currentHash"

    # Get deployed version from app settings
    $deployedHash = Invoke-AzCommand "webapp config appsettings list --name $AppName --resource-group $ResourceGroup --query `"[?name=='DEPLOY_COMMIT'].value | [0]`" -o tsv" -FailOnError $false

    if (-not $deployedHash) {
        Write-Log -Level "INFO" -Message "No previous deployment found"
        return $true
    }

    Write-Log -Level "DEBUG" -Message "Deployed git commit: $deployedHash"

    if ($currentHash -eq $deployedHash) {
        Write-Log -Level "INFO" -Message "Already deployed (commit $currentHash). Use -Force to redeploy."
        return $false
    }

    Write-Log -Level "INFO" -Message "Changes detected: $deployedHash -> $currentHash"
    return $true
}

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
        [bool]$Force = $false
    )

    $rgName = $Config.resourceGroup
    $appName = $Config.gameService.appName
    $projectPath = Join-Path $ProjectRoot "Catan3.GameService"
    $publishPath = Join-Path $ProjectRoot ".publish/gameservice"
    $zipPath = Join-Path $ProjectRoot ".publish/gameservice.zip"

    # Check if deployment is needed
    if (-not (Test-DeploymentNeeded -AppName $appName -ResourceGroup $rgName -Force $Force)) {
        return $true
    }

    Write-Log -Level "INFO" -Message "Building GameService..."
    dotnet publish $projectPath -c Release -o $publishPath --nologo -v q

    Write-Log -Level "INFO" -Message "Creating deployment package..."
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path "$publishPath/*" -DestinationPath $zipPath

    $zipSize = (Get-Item $zipPath).Length / 1MB
    Write-Log -Level "INFO" -Message "Deploying to Azure ($([math]::Round($zipSize, 1)) MB)..."

    # Enable logging to diagnose startup failures
    Write-Log -Level "DEBUG" -Message "Enabling App Service logging..."
    Invoke-AzCommand "webapp log config --name $appName --resource-group $rgName --docker-container-logging filesystem --detailed-error-messages true --web-server-logging filesystem" -SuppressOutput

    # Use --async true to avoid infinite polling bug in az cli 2.61+
    # See: https://github.com/Azure/azure-cli/issues/29003
    Invoke-AzCommand "webapp deploy --name $appName --resource-group $rgName --src-path `"$zipPath`" --type zip --async true" -SuppressOutput

    # Store the deployed commit hash and build timestamp
    $commitHash = Get-GitCommitHash
    $buildTime = (Get-Date -Format "o")  # ISO 8601 format
    Invoke-AzCommand "webapp config appsettings set --name $appName --resource-group $rgName --settings DEPLOY_COMMIT=$commitHash DEPLOY_BUILD_TIME=`"$buildTime`"" -SuppressOutput

    Write-Log -Level "INFO" -Message "GameService deployed: $($Config.gameService.url)"
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
        [bool]$Force = $false
    )

    $rgName = $Config.resourceGroup
    $appName = $Config.ui.appName
    # Deploy WebUI.Server (hosts the Blazor WASM client) instead of standalone WebUI
    $projectPath = Join-Path $ProjectRoot "WebUI.Server"
    $publishPath = Join-Path $ProjectRoot ".publish/webui"
    $zipPath = Join-Path $ProjectRoot ".publish/webui.zip"

    # Check if deployment is needed
    if (-not (Test-DeploymentNeeded -AppName $appName -ResourceGroup $rgName -Force $Force)) {
        return $true
    }

    Write-Log -Level "INFO" -Message "Building WebUI.Server..."
    dotnet publish $projectPath -c Release -o $publishPath --nologo -v q

    # Remove BlazorDebugProxy (saves ~11 MB, not needed in production)
    $debugProxyPath = Join-Path $publishPath "BlazorDebugProxy"
    if (Test-Path $debugProxyPath) {
        Remove-Item $debugProxyPath -Recurse -Force
        Write-Log -Level "DEBUG" -Message "Removed BlazorDebugProxy folder"
    }

    Write-Log -Level "INFO" -Message "Creating deployment package..."
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path "$publishPath/*" -DestinationPath $zipPath

    $zipSize = (Get-Item $zipPath).Length / 1MB
    Write-Log -Level "INFO" -Message "Deploying to Azure ($([math]::Round($zipSize, 1)) MB)..."

    # Use --async true to avoid infinite polling bug in az cli 2.61+
    Invoke-AzCommand "webapp deploy --name $appName --resource-group $rgName --src-path `"$zipPath`" --type zip --async true" -SuppressOutput

    # Store the deployed commit hash and build timestamp
    $commitHash = Get-GitCommitHash
    $buildTime = (Get-Date -Format "o")  # ISO 8601 format
    Invoke-AzCommand "webapp config appsettings set --name $appName --resource-group $rgName --settings DEPLOY_COMMIT=$commitHash DEPLOY_BUILD_TIME=`"$buildTime`"" -SuppressOutput

    Write-Log -Level "INFO" -Message "UI deployed: $($Config.ui.url)"
    return $true
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
        [string]$TraceLevel = "ERROR"
    )

    $appName = $Config.gameService.appName
    $planName = $Config.gameService.appServicePlan
    $url = $Config.gameService.url
    $rgName = $Config.resourceGroup

    Write-Log -Level "DEBUG" -Message "Get-GameServiceDoctor started" -TraceLevel $TraceLevel

    $result = @{
        resource    = "game-service"
        name        = $appName
        url         = $url
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
            $result.checks.planSkuOk = ($plan.sku.name -notin @("F1", "D1"))
            if (-not $result.checks.planSkuOk) {
                $result.needsInstall = $true
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
        $identity = Invoke-AzCommand "webapp identity show --name $appName --resource-group $rgName --query principalId -o tsv" -FailOnError $false
        $result.checks.managedIdentity = (-not [string]::IsNullOrWhiteSpace($identity))

        # Check Always On setting (critical for performance - prevents cold starts)
        Write-Log -Level "DEBUG" -Message "Checking Always On setting" -TraceLevel $TraceLevel
        $alwaysOn = Invoke-AzCommand "webapp config show --name $appName --resource-group $rgName --query alwaysOn -o tsv" -FailOnError $false
        $result.checks.alwaysOn = ($alwaysOn -eq "true")
        if (-not $result.checks.alwaysOn) {
            $result.performanceWarnings = @("Always On is disabled - app will have cold start delays")
        }

        # Get current git commit
        $result.currentCommit = Get-GitCommitHash
        Write-Log -Level "DEBUG" -Message "Current git commit: $($result.currentCommit)" -TraceLevel $TraceLevel

        # Check health endpoint first - this is the definitive test of whether code is deployed
        # The health endpoint returns the deployed commit and build time directly
        Write-Log -Level "DEBUG" -Message "Checking health endpoint: $url/health" -TraceLevel $TraceLevel
        try {
            $health = Invoke-RestMethod -Uri "$url/health" -TimeoutSec 10
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
        catch {
            $result.healthCheck = "unreachable"
            $result.checks.healthEndpoint = $false
            Write-Log -Level "DEBUG" -Message "Health endpoint unreachable: $_" -TraceLevel $TraceLevel
        }

        # Code is deployed if health endpoint responds (regardless of commit tracking)
        $result.checks.codeDeployed = $result.checks.healthEndpoint

        # Check if deploy is needed:
        # - Health endpoint doesn't work = needs deploy
        # - Health endpoint works but no version info = old code, needs deploy
        # - No build time tracking = needs deploy (to enable tracking)
        # - Commit mismatch = needs deploy (code changed, even if uncommitted)
        if (-not $result.checks.healthEndpoint) {
            $result.needsDeploy = $true
            $result.deployReason = "Health endpoint not responding"
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
            # Commit changed - definitely needs deploy
            $result.needsDeploy = $true
            $result.deployReason = "Git commit mismatch"
        }
        # Note: If commits match but code is uncommitted, -Force flag can be used to redeploy

        # Healthy if endpoint responds
        $result.healthy = $result.checks.healthEndpoint

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
        [string]$TraceLevel = "ERROR"
    )

    $appName = $Config.ui.appName
    $planName = $Config.gameService.appServicePlan  # UI shares the same plan
    $url = $Config.ui.url
    $rgName = $Config.resourceGroup
    $gameServiceUrl = $Config.gameService.url

    Write-Log -Level "DEBUG" -Message "Get-UIDoctor started" -TraceLevel $TraceLevel

    $result = @{
        resource  = "ui"
        name      = $appName
        url       = $url
        status    = "unknown"
        healthy   = $false
        timestamp = (Get-Date -Format "o")
        # Detailed checks for each install/deploy step
        checks    = @{
            resourceGroup     = $false
            appServicePlan    = $false
            webApp            = $false
            managedIdentity   = $false
            gameServiceUrl    = $false
            codeDeployed      = $false
            siteResponding    = $false
        }
        # Git commit tracking
        currentCommit  = $null
        deployedCommit = $null
        # What actions are needed
        needsInstall   = $false
        needsDeploy    = $false
    }

    try {
        # Check resource group exists
        Write-Log -Level "DEBUG" -Message "Checking resource group: $rgName" -TraceLevel $TraceLevel
        $rg = Invoke-AzCommand "group show --name $rgName" -FailOnError $false -JsonOutput
        $result.checks.resourceGroup = ($null -ne $rg)

        # Check App Service Plan exists
        Write-Log -Level "DEBUG" -Message "Checking app service plan: $planName" -TraceLevel $TraceLevel
        $plan = Invoke-AzCommand "appservice plan show --name $planName --resource-group $rgName" -FailOnError $false -JsonOutput
        $result.checks.appServicePlan = ($null -ne $plan)

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
        $identity = Invoke-AzCommand "webapp identity show --name $appName --resource-group $rgName --query principalId -o tsv" -FailOnError $false
        $result.checks.managedIdentity = (-not [string]::IsNullOrWhiteSpace($identity))

        # Check GameService URL configured in app settings
        Write-Log -Level "DEBUG" -Message "Checking GameService URL config" -TraceLevel $TraceLevel
        $configuredUrl = Invoke-AzCommand "webapp config appsettings list --name $appName --resource-group $rgName --query `"[?name=='GAMESERVICE_URL'].value | [0]`" -o tsv" -FailOnError $false
        $result.checks.gameServiceUrl = ($configuredUrl -eq $gameServiceUrl)

        # Get current git commit
        $result.currentCommit = Get-GitCommitHash
        Write-Log -Level "DEBUG" -Message "Current git commit: $($result.currentCommit)" -TraceLevel $TraceLevel

        # Get deployed commit from app settings
        $result.deployedCommit = Invoke-AzCommand "webapp config appsettings list --name $appName --resource-group $rgName --query `"[?name=='DEPLOY_COMMIT'].value | [0]`" -o tsv" -FailOnError $false
        Write-Log -Level "DEBUG" -Message "Deployed commit: $($result.deployedCommit)" -TraceLevel $TraceLevel

        # Check if code has been deployed
        $result.checks.codeDeployed = (-not [string]::IsNullOrWhiteSpace($result.deployedCommit))

        # Check if deploy is needed (commit mismatch)
        if (-not $result.checks.codeDeployed) {
            $result.needsDeploy = $true
        }
        elseif ($result.currentCommit -ne $result.deployedCommit) {
            $result.needsDeploy = $true
        }

        # Check if UI responds
        Write-Log -Level "DEBUG" -Message "Checking site response: $url" -TraceLevel $TraceLevel
        try {
            $response = Invoke-WebRequest -Uri $url -TimeoutSec 10 -UseBasicParsing
            $result.checks.siteResponding = ($response.StatusCode -eq 200)
            # If site responds, code must be deployed and GameService URL must be configured
            if ($result.checks.siteResponding) {
                $result.checks.codeDeployed = $true
                $result.checks.gameServiceUrl = $true  # If UI works, URL must be configured
            }
        }
        catch {
            $result.checks.siteResponding = $false
            Write-Log -Level "DEBUG" -Message "Site not responding: $_" -TraceLevel $TraceLevel
            # If app exists but not responding, needs deploy
            if (-not $result.needsDeploy) {
                $result.needsDeploy = $true
            }
        }

        # Determine overall health - site responding is the definitive test
        if ($result.checks.siteResponding) {
            $result.healthy = $true
        }

        Write-Log -Level "DEBUG" -Message "UI doctor complete: healthy=$($result.healthy), needsDeploy=$($result.needsDeploy)" -TraceLevel $TraceLevel
    }
    catch {
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
    Write-Host ""
    Write-Host "$($Result.resource) ($($Result.name))" -ForegroundColor Cyan
    Write-Host ("-" * 60)
    Write-Host ("Check".PadRight($col1) + "Status".PadRight($col2) + "Details") -ForegroundColor Yellow

    # Helper to show a check row
    function Show-CheckRow {
        param([string]$Name, [bool]$Status, [string]$Details = "")
        $statusText = if ($Status) { "OK" } else { "MISSING" }
        $statusColor = if ($Status) { "Green" } else { "Red" }

        Write-Host -NoNewline ("  " + $Name).PadRight($col1)
        Write-Host -NoNewline $statusText.PadRight($col2) -ForegroundColor $statusColor
        if ($Details) {
            Write-Host $Details -ForegroundColor Gray
        } else {
            Write-Host ""
        }
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
                default { $key }
            }

            $action = ""
            if (-not $Result.checks[$key]) {
                $action = if ($key -in @("codeDeployed", "healthEndpoint", "connectionString", "connectionPooling", "managedIdentityUser", "gameServiceConnected", "siteResponding")) {
                    "run: deploy"
                } elseif ($key -eq "planSkuOk") {
                    "current: $($Result.currentSku), need: B1+"
                } else {
                    "run: install"
                }
            }
            Show-CheckRow -Name $displayName -Status $Result.checks[$key] -Details $action
        }
    }

    # Show git commit info if available
    if ($Result.currentCommit -or $Result.deployedCommit) {
        Write-Host ""
        Write-Host -NoNewline ("  Git Commit").PadRight($col1)
        if ($Result.currentCommit -eq $Result.deployedCommit -and $Result.deployedCommit) {
            Write-Host -NoNewline "MATCH".PadRight($col2) -ForegroundColor Green
            Write-Host "$($Result.currentCommit)" -ForegroundColor Gray
        } elseif ($Result.deployedCommit -and $Result.deployedCommit -ne "local") {
            Write-Host -NoNewline "MISMATCH".PadRight($col2) -ForegroundColor Yellow
            Write-Host "deployed: $($Result.deployedCommit) -> current: $($Result.currentCommit)" -ForegroundColor Gray
        } else {
            Write-Host -NoNewline "NONE".PadRight($col2) -ForegroundColor Yellow
            Write-Host "not yet deployed" -ForegroundColor Gray
        }
    }

    # Show build time if available
    if ($Result.deployedBuildTime -and $Result.deployedBuildTime -ne "unknown") {
        Write-Host -NoNewline ("  Build Time").PadRight($col1)
        Write-Host -NoNewline "DEPLOYED".PadRight($col2) -ForegroundColor Green
        Write-Host "$($Result.deployedBuildTime)" -ForegroundColor Gray
    }

    # Show database status if available
    if ($Result.dbStatus) {
        Write-Host -NoNewline ("  Database Status").PadRight($col1)
        $dbColor = switch ($Result.dbStatus) {
            "Online" { "Green" }
            "Paused" { "Yellow" }
            default { "Red" }
        }
        Write-Host $Result.dbStatus -ForegroundColor $dbColor
    }

    # Show diagnostic issue from health endpoint if available
    if ($Result.diagnosticIssue) {
        Write-Host -NoNewline ("  Diagnostic Issue").PadRight($col1)
        $issueColor = switch ($Result.diagnosticIssue) {
            "None" { "Green" }
            "DatabasePaused" { "Yellow" }
            "ConnectionTimeout" { "Yellow" }
            default { "Red" }
        }
        Write-Host $Result.diagnosticIssue -ForegroundColor $issueColor
    }

    # Show Azure database status from diagnostics if different from local check
    if ($Result.azureDatabaseStatus -and $Result.azureDatabaseStatus -ne $Result.dbStatus) {
        Write-Host -NoNewline ("  Azure DB Status").PadRight($col1)
        $azureDbColor = switch ($Result.azureDatabaseStatus) {
            "Online" { "Green" }
            "Paused" { "Yellow" }
            default { "Red" }
        }
        Write-Host $Result.azureDatabaseStatus -ForegroundColor $azureDbColor
    }

    # Summary line
    Write-Host ""
    Write-Host -NoNewline "Status: "
    if ($Result.needsInstall) {
        Write-Host "NEEDS INSTALL" -ForegroundColor Red
        Write-Host "  Recommended: " -NoNewline -ForegroundColor Gray
        Write-Host "./catan-azure.ps1 $($Result.resource) install" -ForegroundColor Cyan
    } elseif ($Result.needsDeploy) {
        Write-Host "NEEDS DEPLOY" -ForegroundColor Yellow
        if ($Result.deployReason) {
            Write-Host "  Reason: $($Result.deployReason)" -ForegroundColor Gray
        }
        Write-Host "  Recommended: " -NoNewline -ForegroundColor Gray
        Write-Host "./catan-azure.ps1 $($Result.resource) deploy" -ForegroundColor Cyan
    } elseif ($Result.healthy) {
        Write-Host "HEALTHY" -ForegroundColor Green
    } else {
        Write-Host "UNKNOWN" -ForegroundColor Red
    }

    # Show performance warnings if any
    if ($Result.performanceWarnings) {
        Write-Host ""
        Write-Host "Performance Warnings:" -ForegroundColor Yellow
        foreach ($warning in $Result.performanceWarnings) {
            Write-Host "  ⚠️  $warning" -ForegroundColor Yellow
        }
    }

    # Show note if any
    if ($Result.note) {
        Write-Host "  Note: $($Result.note)" -ForegroundColor Yellow
    }

    # Show error if any
    if ($Result.error) {
        Write-Host "  Error: $($Result.error)" -ForegroundColor Red
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

    Write-Host ""
    Write-Host "Performance Test: $url" -ForegroundColor Cyan
    Write-Host "=" * 50
    Write-Host ""
    Write-Host "Running $testCount sequential requests to /api/players..."
    Write-Host ""

    for ($i = 1; $i -le $testCount; $i++) {
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            $response = Invoke-RestMethod -Uri "$url/api/players" -TimeoutSec 60 -ErrorAction Stop
            $stopwatch.Stop()
            $elapsed = $stopwatch.Elapsed.TotalSeconds
            $times += $elapsed

            $color = if ($elapsed -lt 1) { "Green" } elseif ($elapsed -lt 3) { "Yellow" } else { "Red" }
            $status = if ($elapsed -lt 1) { "FAST" } elseif ($elapsed -lt 3) { "SLOW" } else { "VERY SLOW" }

            Write-Host ("  Request {0}: {1,6:N2}s  " -f $i, $elapsed) -NoNewline
            Write-Host $status -ForegroundColor $color
        }
        catch {
            $stopwatch.Stop()
            Write-Host ("  Request {0}: FAILED - {1}" -f $i, $_.Exception.Message) -ForegroundColor Red
        }

        # Small delay between requests
        Start-Sleep -Milliseconds 200
    }

    Write-Host ""
    Write-Host "Summary:" -ForegroundColor Cyan
    Write-Host "-" * 30

    if ($times.Count -gt 0) {
        $min = ($times | Measure-Object -Minimum).Minimum
        $max = ($times | Measure-Object -Maximum).Maximum
        $avg = ($times | Measure-Object -Average).Average
        $first = $times[0]
        $warmAvg = if ($times.Count -gt 1) { ($times[1..($times.Count-1)] | Measure-Object -Average).Average } else { $first }

        Write-Host ("  First request (cold):  {0,6:N2}s" -f $first)
        Write-Host ("  Warm average:          {0,6:N2}s" -f $warmAvg)
        Write-Host ("  Min / Max:             {0,6:N2}s / {1:N2}s" -f $min, $max)
        Write-Host ""

        # Performance assessment
        if ($first -gt 10) {
            Write-Host "⚠️  Cold start is very slow (>10s). Check:" -ForegroundColor Yellow
            Write-Host "   - App Service Plan SKU (needs B1+ for Always On)" -ForegroundColor Gray
            Write-Host "   - Always On setting (prevents cold starts)" -ForegroundColor Gray
            Write-Host "   - Azure SQL auto-pause (may need to wake up)" -ForegroundColor Gray
        }
        elseif ($first -gt 5) {
            Write-Host "⚠️  Cold start is slow (>5s). Consider:" -ForegroundColor Yellow
            Write-Host "   - Enabling Always On if not already enabled" -ForegroundColor Gray
        }
        else {
            Write-Host "✅ Cold start is acceptable (<5s)" -ForegroundColor Green
        }

        if ($warmAvg -gt 2) {
            Write-Host "⚠️  Warm requests are slow (>2s avg). Check:" -ForegroundColor Yellow
            Write-Host "   - Connection pooling in connection string" -ForegroundColor Gray
            Write-Host "   - Azure SQL tier and capacity" -ForegroundColor Gray
        }
        elseif ($warmAvg -gt 1) {
            Write-Host "⚠️  Warm requests are a bit slow (>1s avg)" -ForegroundColor Yellow
        }
        else {
            Write-Host "✅ Warm requests are good (<1s avg)" -ForegroundColor Green
        }

        # Check for high variance (indicates connection issues)
        if ($times.Count -gt 2) {
            $variance = $max - $min
            if ($variance -gt 5) {
                Write-Host "⚠️  High variance detected ({0:N2}s). May indicate:" -f $variance -ForegroundColor Yellow
                Write-Host "   - Connection pool exhaustion" -ForegroundColor Gray
                Write-Host "   - Network instability" -ForegroundColor Gray
                Write-Host "   - Token refresh issues (Managed Identity)" -ForegroundColor Gray
            }
        }
    }
    else {
        Write-Host "  No successful requests - check service health" -ForegroundColor Red
    }

    Write-Host ""
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
    ui              WebUI Blazor application
    database        Azure SQL Serverless database
    game-service    GameService ASP.NET Core API

Verbs:
    install         Create Azure resources (idempotent)
    deploy          Deploy code/data to Azure
    doctor          Check health and status
    clean           Delete Azure resources

Options:
    -Yes            Skip confirmation prompts
    -Force          Force deploy even if no changes detected
    -Json           Output doctor results as JSON
    -HashTable      Output doctor results as PowerShell hashtable
    -Perf           Run performance test (game-service doctor only)
    -TraceLevel     Output verbosity (ERROR, WARN, INFO, DEBUG)

Examples:
    ./catan-azure.ps1 doctor                   Check health of ALL resources
    ./catan-azure.ps1 doctor -Perf             Check all health + run perf test
    ./catan-azure.ps1 install                  Create ALL Azure resources
    ./catan-azure.ps1 deploy                   Deploy ALL code/data
    ./catan-azure.ps1 game-service install     Create GameService resources only
    ./catan-azure.ps1 database deploy          Configure SQL connection string
    ./catan-azure.ps1 ui doctor -Json          Check UI health (JSON output)
    ./catan-azure.ps1 game-service clean       Delete GameService only
"@
    Write-Host $help
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
$config = Get-AzureConfig

# For install, ensure we have a base name
if ($Verb -eq "install") {
    if (-not $config.baseName) {
        $baseName = Get-AvailableBaseName
        $config = Initialize-ConfigFromBaseName -BaseName $baseName
        Save-AzureConfig -Config $config
    }
    else {
        Write-Log -Level "INFO" -Message "Using existing base name: $($config.baseName)"
    }
}
elseif (-not $config.baseName) {
    Write-Log -Level "ERROR" -Message "No Azure configuration found. Run 'install' first."
    exit 1
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
            # Run doctor on all resources
            $allHealthy = $true

            # GameService
            Write-Log -Level "HEADER" -Message "Catan Azure: game-service doctor"
            Write-Log -Level "HEADER" -Message ("=" * 40)
            $gsResult = Get-GameServiceDoctor -Config $config -TraceLevel $TraceLevel
            if ($Json) {
                # For JSON, collect all results
            } else {
                Show-DoctorResult -Result $gsResult -Config $config
                if ($Perf -and $gsResult.checks.healthEndpoint) {
                    Test-GameServicePerformance -Config $config
                }
            }
            if (-not $gsResult.healthy) { $allHealthy = $false }

            # Database
            Write-Log -Level "HEADER" -Message "Catan Azure: database doctor"
            Write-Log -Level "HEADER" -Message ("=" * 40)
            $dbResult = Get-DatabaseDoctor -Config $config -TraceLevel $TraceLevel
            if (-not $Json) {
                Show-DoctorResult -Result $dbResult -Config $config
            }
            if (-not $dbResult.healthy) { $allHealthy = $false }

            # UI
            Write-Log -Level "HEADER" -Message "Catan Azure: ui doctor"
            Write-Log -Level "HEADER" -Message ("=" * 40)
            $uiResult = Get-UIDoctor -Config $config -TraceLevel $TraceLevel
            if (-not $Json) {
                Show-DoctorResult -Result $uiResult -Config $config
            }
            if (-not $uiResult.healthy) { $allHealthy = $false }

            # Output JSON if requested
            if ($Json) {
                $allResults = @{
                    gameService = $gsResult
                    database = $dbResult
                    ui = $uiResult
                }
                Write-Output ($allResults | ConvertTo-Json -Depth 10)
            }

            # Show service URLs
            Write-Host ""
            Write-Host "Service URLs:" -ForegroundColor Cyan
            Write-Host "  WebUI:       $($config.ui.url)"
            Write-Host "  GameService: $($config.gameService.url)"

            $success = $allHealthy
        }
        "install" {
            # Install all resources in order
            Write-Log -Level "HEADER" -Message "Catan Azure: install all"
            Write-Log -Level "HEADER" -Message ("=" * 40)
            $success = (Install-GameService -Config $config) -and
                       (Install-Database -Config $config) -and
                       (Install-UI -Config $config)
        }
        "deploy" {
            # Deploy all resources
            Write-Log -Level "HEADER" -Message "Catan Azure: deploy all"
            Write-Log -Level "HEADER" -Message ("=" * 40)
            $success = (Deploy-GameService -Config $config -Force $Force) -and
                       (Deploy-Database -Config $config -Force $Force) -and
                       (Deploy-UI -Config $config -Force $Force)
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
            "deploy" { $success = Deploy-GameService -Config $config -Force $Force }
            "doctor" {
                $result = Get-GameServiceDoctor -Config $config -TraceLevel $TraceLevel
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
        }
    }
    "ui" {
        switch ($Verb) {
            "install" { $success = Install-UI -Config $config }
            "deploy" { $success = Deploy-UI -Config $config -Force $Force }
            "doctor" {
                $result = Get-UIDoctor -Config $config -TraceLevel $TraceLevel
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
}

if ($success) {
    exit 0
}
else {
    exit 1
}

#endregion

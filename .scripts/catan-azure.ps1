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
    [ValidateSet("ui", "database", "game-service", "help")]
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
    [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
    [string]$TraceLevel = "INFO",

    [Parameter()]
    [switch]$Help
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

    # Configure firewall to allow Azure services
    Write-Log -Level "INFO" -Message "Configuring firewall rules..."
    $fwExists = Invoke-AzCommand "sql server firewall-rule show --server $sqlServerName --resource-group $rgName --name AllowAzureServices" -FailOnError $false -JsonOutput
    if (-not $fwExists) {
        Invoke-AzCommand "sql server firewall-rule create --server $sqlServerName --resource-group $rgName --name AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0" -SuppressOutput
        Write-Log -Level "INFO" -Message "Firewall rule created: AllowAzureServices"
    }

    # Check if database exists
    Write-Log -Level "INFO" -Message "Checking database: $databaseName"
    $dbExists = Invoke-AzCommand "sql db show --server $sqlServerName --resource-group $rgName --name $databaseName" -FailOnError $false -JsonOutput
    if (-not $dbExists) {
        Write-Log -Level "INFO" -Message "Creating Serverless database: $databaseName"

        # Create serverless database with auto-pause
        Invoke-AzCommand "sql db create --server $sqlServerName --resource-group $rgName --name $databaseName --compute-model Serverless --edition GeneralPurpose --family Gen5 --min-capacity 0.5 --capacity 2 --auto-pause-delay 60 --backup-storage-redundancy Local" -SuppressOutput

        Write-Log -Level "INFO" -Message "Database created: $databaseName (Serverless, auto-pause after 60 min)"
    }
    else {
        Write-Log -Level "INFO" -Message "Database exists: $databaseName"
    }

    Write-Log -Level "SUCCESS" -Message "SQL Server ready: $($Config.sqlServer.fqdn)"
    return $true
}

<#
.SYNOPSIS
    Configures GameService to use Azure SQL Server.
.DESCRIPTION
    Creates connection string and configures it in the GameService App Service.
    Also grants the GameService managed identity access to the database.
.PARAMETER Config
    Azure configuration hashtable
.OUTPUTS
    Boolean - $true on success
#>
function Deploy-Database {
    param([hashtable]$Config)

    $rgName = $Config.resourceGroup
    $databaseName = $Config.sqlServer.databaseName
    $fqdn = $Config.sqlServer.fqdn
    $appName = $Config.gameService.appName

    # Build connection string with managed identity auth
    $connectionString = "Server=tcp:$fqdn,1433;Database=$databaseName;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

    Write-Log -Level "INFO" -Message "Configuring SQL connection string in App Service..."
    Invoke-AzCommand "webapp config connection-string set --name $appName --resource-group $rgName --connection-string-type SQLAzure --settings AzureSql=`"$connectionString`"" -SuppressOutput

    Write-Log -Level "INFO" -Message "Connection string configured"

    # Get GameService managed identity principal ID
    $principalId = Invoke-AzCommand "webapp identity show --name $appName --resource-group $rgName --query principalId -o tsv" -FailOnError $false

    if ($principalId) {
        Write-Log -Level "WARN" -Message "To grant database access, run this SQL command as server admin:"
        Write-Log -Level "WARN" -Message "  CREATE USER [$appName] FROM EXTERNAL PROVIDER;"
        Write-Log -Level "WARN" -Message "  ALTER ROLE db_datareader ADD MEMBER [$appName];"
        Write-Log -Level "WARN" -Message "  ALTER ROLE db_datawriter ADD MEMBER [$appName];"
        Write-Log -Level "WARN" -Message "  ALTER ROLE db_ddladmin ADD MEMBER [$appName];"
        Write-Log -Level "INFO" -Message ""
        Write-Log -Level "INFO" -Message "Or connect via Azure Portal > SQL Database > Query Editor"
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
    param([hashtable]$Config)

    $rgName = $Config.resourceGroup
    $sqlServerName = $Config.sqlServer.serverName
    $databaseName = $Config.sqlServer.databaseName
    $gameServiceUrl = $Config.gameService.url

    $result = @{
        resource     = "database"
        serverName   = $sqlServerName
        databaseName = $databaseName
        fqdn         = $Config.sqlServer.fqdn
        status       = "unknown"
        healthy      = $false
        dbStatus     = "unknown"
        timestamp    = (Get-Date -Format "o")
    }

    try {
        # Check SQL Server exists
        $server = Invoke-AzCommand "sql server show --name $sqlServerName --resource-group $rgName" -FailOnError $false -JsonOutput
        if (-not $server) {
            $result.status = "server-not-found"
            return $result
        }

        $result.status = "server-exists"

        # Check database status
        $db = Invoke-AzCommand "sql db show --server $sqlServerName --resource-group $rgName --name $databaseName" -FailOnError $false -JsonOutput
        if (-not $db) {
            $result.status = "database-not-found"
            return $result
        }

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

        # Check GameService health endpoint for database connectivity
        try {
            $health = Invoke-RestMethod -Uri "$gameServiceUrl/health" -TimeoutSec 30
            if ($health.database.provider -eq "SqlServer") {
                $result.healthy = $true
                $result.status = "connected"
            }
        }
        catch {
            $result.note = "GameService not responding - may need deploy or database may be resuming from pause"
        }
    }
    catch {
        $result.status = "error"
        $result.error = $_.Exception.Message
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
        Write-Log -Level "INFO" -Message "Creating App Service Plan: $planName (B1)"
        Invoke-AzCommand "appservice plan create --name $planName --resource-group $rgName --location $location --sku B1 --is-linux" -SuppressOutput
        Write-Log -Level "INFO" -Message "App Service Plan created: $planName"
    }
    else {
        Write-Log -Level "INFO" -Message "App Service Plan exists: $planName"
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

    Write-Log -Level "SUCCESS" -Message "GameService App ready: $appName"
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
    Builds and deploys the GameService to Azure.
.DESCRIPTION
    Publishes the Catan3.GameService project, creates a zip package,
    and deploys it to the Azure Web App using zip deployment.
.PARAMETER Config
    Azure configuration hashtable
.OUTPUTS
    Boolean - $true on success
#>
function Deploy-GameService {
    param([hashtable]$Config)

    $rgName = $Config.resourceGroup
    $appName = $Config.gameService.appName
    $projectPath = Join-Path $ProjectRoot "Catan3.GameService"
    $publishPath = Join-Path $ProjectRoot ".publish/gameservice"
    $zipPath = Join-Path $ProjectRoot ".publish/gameservice.zip"

    Write-Log -Level "INFO" -Message "Building GameService..."
    dotnet publish $projectPath -c Release -o $publishPath --nologo -v q

    Write-Log -Level "INFO" -Message "Creating deployment package..."
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path "$publishPath/*" -DestinationPath $zipPath

    Write-Log -Level "INFO" -Message "Deploying to Azure..."
    Invoke-AzCommand "webapp deploy --name $appName --resource-group $rgName --src-path `"$zipPath`" --type zip" -SuppressOutput

    Write-Log -Level "INFO" -Message "GameService deployed: $($Config.gameService.url)"
    return $true
}

<#
.SYNOPSIS
    Builds and deploys the WebUI to Azure.
.DESCRIPTION
    Publishes the Blazor WebAssembly project, creates a zip package,
    and deploys it to the Azure Web App using zip deployment.
.PARAMETER Config
    Azure configuration hashtable
.OUTPUTS
    Boolean - $true on success
#>
function Deploy-UI {
    param([hashtable]$Config)

    $rgName = $Config.resourceGroup
    $appName = $Config.ui.appName
    # Deploy WebUI.Server (hosts the Blazor WASM client) instead of standalone WebUI
    $projectPath = Join-Path $ProjectRoot "WebUI.Server"
    $publishPath = Join-Path $ProjectRoot ".publish/webui"
    $zipPath = Join-Path $ProjectRoot ".publish/webui.zip"

    Write-Log -Level "INFO" -Message "Building WebUI.Server..."
    dotnet publish $projectPath -c Release -o $publishPath --nologo -v q

    Write-Log -Level "INFO" -Message "Creating deployment package..."
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path "$publishPath/*" -DestinationPath $zipPath

    Write-Log -Level "INFO" -Message "Deploying to Azure..."
    Invoke-AzCommand "webapp deploy --name $appName --resource-group $rgName --src-path `"$zipPath`" --type zip" -SuppressOutput

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
    param([hashtable]$Config)

    $appName = $Config.gameService.appName
    $url = $Config.gameService.url

    $result = @{
        resource    = "game-service"
        name        = $appName
        url         = $url
        status      = "unknown"
        healthy     = $false
        healthCheck = "unknown"
        timestamp   = (Get-Date -Format "o")
    }

    try {
        $rgName = $Config.resourceGroup
        $app = Invoke-AzCommand "webapp show --name $appName --resource-group $rgName" -FailOnError $false -JsonOutput
        if (-not $app) {
            $result.status = "not-found"
            return $result
        }

        $result.status = $app.state.ToLower()

        # Check health endpoint
        try {
            $health = Invoke-RestMethod -Uri "$url/health" -TimeoutSec 10
            $result.healthCheck = $health.status
            $result.healthy = ($health.status -eq "healthy")
        }
        catch {
            $result.healthCheck = "unreachable"
        }
    }
    catch {
        $result.status = "error"
        $result.error = $_.Exception.Message
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
    param([hashtable]$Config)

    $appName = $Config.ui.appName
    $url = $Config.ui.url

    $result = @{
        resource  = "ui"
        name      = $appName
        url       = $url
        status    = "unknown"
        healthy   = $false
        timestamp = (Get-Date -Format "o")
    }

    try {
        $rgName = $Config.resourceGroup
        $app = Invoke-AzCommand "webapp show --name $appName --resource-group $rgName" -FailOnError $false -JsonOutput
        if (-not $app) {
            $result.status = "not-found"
            return $result
        }

        $result.status = $app.state.ToLower()

        # Check if UI responds
        try {
            $response = Invoke-WebRequest -Uri $url -TimeoutSec 10 -UseBasicParsing
            $result.healthy = ($response.StatusCode -eq 200)
        }
        catch {
            $result.healthy = $false
        }
    }
    catch {
        $result.status = "error"
        $result.error = $_.Exception.Message
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
    Formats and outputs doctor check results.
.DESCRIPTION
    Outputs health check results in human-readable, JSON, or hashtable format.
    Includes service URLs when config is provided.
.PARAMETER Result
    Hashtable containing health check results
.PARAMETER Config
    Optional Azure configuration to display service URLs
.PARAMETER Json
    Output as JSON format
.PARAMETER HashTable
    Output as PowerShell hashtable
#>
function Output-DoctorResult {
    param(
        [hashtable]$Result,
        [hashtable]$Config,
        [switch]$Json,
        [switch]$HashTable
    )

    if ($Json) {
        $Result | ConvertTo-Json -Depth 10
        return
    }

    if ($HashTable) {
        $Result
        return
    }

    # Human-readable output
    Write-Log -Level "HEADER" -Message "$($Result.resource) Health Check"
    Write-Log -Level "HEADER" -Message ("=" * 40)
    Write-Log -Level "INFO" -Message "Resource: $($Result.name)"
    Write-Log -Level "INFO" -Message "Status: $($Result.status)"

    if ($Result.url) {
        Write-Log -Level "INFO" -Message "URL: $($Result.url)"
    }

    if ($Result.healthCheck) {
        Write-Log -Level "INFO" -Message "Health: $($Result.healthCheck)"
    }

    if ($Result.blobSize) {
        $sizeKb = [math]::Round($Result.blobSize / 1024, 1)
        Write-Log -Level "INFO" -Message "Database Size: ${sizeKb} KB"
    }

    if ($Result.playerCount) {
        Write-Log -Level "INFO" -Message "Players: $($Result.playerCount)"
    }

    if ($Result.gameCount) {
        Write-Log -Level "INFO" -Message "Games: $($Result.gameCount)"
    }

    if ($Result.note) {
        Write-Log -Level "WARN" -Message $Result.note
    }

    if ($Result.healthy) {
        Write-Log -Level "INFO" -Message "HEALTHY"
    }
    else {
        Write-Log -Level "ERROR" -Message "UNHEALTHY"
        if ($Result.error) {
            Write-Log -Level "ERROR" -Message $Result.error
        }
    }

    # Output service URLs if config provided
    if ($Config) {
        Write-Log -Level "INFO" -Message ""
        Write-Log -Level "INFO" -Message "Service URLs:"
        Write-Log -Level "INFO" -Message "  WebUI:       $($Config.ui.url)"
        Write-Log -Level "INFO" -Message "  GameService: $($Config.gameService.url)"
    }
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
    ./catan-azure.ps1 <noun> <verb> [options]

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
    -Json           Output doctor results as JSON
    -HashTable      Output doctor results as PowerShell hashtable

Examples:
    ./catan-azure.ps1 game-service install     Create GameService resources
    ./catan-azure.ps1 database deploy          Configure SQL connection string
    ./catan-azure.ps1 ui doctor -Json          Check UI health (JSON output)
    ./catan-azure.ps1 game-service clean       Delete GameService

Coordinated operations (via webui.ps1):
    ./webui.ps1 azure install                  Install all resources
    ./webui.ps1 azure deploy                   Deploy everything
    ./webui.ps1 azure doctor                   Check all health
    ./webui.ps1 azure clean                    Delete everything
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
if (-not $Verb) {
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
Write-Log -Level "HEADER" -Message "Catan Azure: $Noun $Verb"
Write-Log -Level "HEADER" -Message ("=" * 40)

$success = $false

switch ($Noun) {
    "game-service" {
        switch ($Verb) {
            "install" { $success = Install-GameService -Config $config }
            "deploy" { $success = Deploy-GameService -Config $config }
            "doctor" {
                $result = Get-GameServiceDoctor -Config $config
                Output-DoctorResult -Result $result -Config $config -Json:$Json -HashTable:$HashTable
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
            "deploy" { $success = Deploy-Database -Config $config }
            "doctor" {
                $result = Get-DatabaseDoctor -Config $config
                Output-DoctorResult -Result $result -Config $config -Json:$Json -HashTable:$HashTable
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
            "deploy" { $success = Deploy-UI -Config $config }
            "doctor" {
                $result = Get-UIDoctor -Config $config
                Output-DoctorResult -Result $result -Config $config -Json:$Json -HashTable:$HashTable
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

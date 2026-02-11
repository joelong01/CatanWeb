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
    [ValidateSet("install", "deploy", "deploy-staging", "doctor", "clean", "fix")]
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
    [switch]$NoBuild
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
            Invoke-AzCommand "role assignment create --assignee $principalId --role 'SQL Server Contributor' --scope $sqlServerScope" -SuppressOutput
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

    # Grant Reader role on resource group for Azure Resource Graph queries
    # This allows the Troubleshoot feature to find and inspect SQL Server resources
    $subscriptionId = Invoke-AzCommand "account show --query id -o tsv"
    if (-not $subscriptionId) {
        throw "Failed to get subscription ID"
    }
    $rgScope = "/subscriptions/$subscriptionId/resourceGroups/$rgName"

    Write-Log -Level "INFO" -Message "Granting Reader role on resource group to managed identity..."
    $existingRole = Invoke-AzCommand "role assignment list --assignee $principalId --role Reader --scope $rgScope --query [0].id -o tsv" -FailOnError $false
    if (-not $existingRole) {
        Invoke-AzCommand "role assignment create --assignee $principalId --role Reader --scope $rgScope" -SuppressOutput
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

    # Create staging deployment slot (used by GitHub Actions and swap-slots)
    # Requires Standard (S1) tier or higher — upgrade from Basic if needed
    Write-Log -Level "INFO" -Message "Checking staging slot..."
    $stagingSlot = Invoke-AzCommand "webapp deployment slot list --name $appName --resource-group $rgName --query `"[?name=='staging']`"" -FailOnError $false -JsonOutput
    if (-not $stagingSlot -or $stagingSlot.Count -eq 0) {
        # Check if plan SKU supports slots (Standard S1+ required)
        $planSku = Invoke-AzCommand "appservice plan show --name $planName --resource-group $rgName --query sku.tier -o tsv" -FailOnError $false
        if ($planSku -and $planSku -notin @('Standard', 'Premium', 'PremiumV2', 'PremiumV3', 'Isolated', 'IsolatedV2')) {
            Write-Log -Level "INFO" -Message "App Service Plan is '$planSku' tier — upgrading to Standard (S1) for deployment slot support..."
            Invoke-AzCommand "appservice plan update --name $planName --resource-group $rgName --sku S1" -SuppressOutput
            Write-Log -Level "INFO" -Message "Plan upgraded to S1"
        }
        Write-Log -Level "INFO" -Message "Creating staging slot for $appName..."
        Invoke-AzCommand "webapp deployment slot create --name $appName --resource-group $rgName --slot staging" -SuppressOutput
        Write-Log -Level "INFO" -Message "Staging slot created"
    }
    else {
        Write-Log -Level "INFO" -Message "Staging slot exists"
    }

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
        [bool]$Force = $false,
        [bool]$NoBuild = $false
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

    Write-Log -Level "INFO" -Message "Publishing GameService..."
    $publishArgs = @($projectPath, "-c", "Release", "-o", $publishPath, "--nologo", "-v", "q")
    if ($NoBuild) { $publishArgs += "--no-build" }
    dotnet publish @publishArgs

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
        [bool]$Force = $false,
        [bool]$NoBuild = $false
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

    Write-Log -Level "INFO" -Message "Publishing WebUI.Server..."
    $publishArgs = @($projectPath, "-c", "Release", "-o", $publishPath, "--nologo", "-v", "q")
    if ($NoBuild) { $publishArgs += "--no-build" }
    dotnet publish @publishArgs

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
        [string]$TraceLevel = "ERROR"
    )

    $rgName = $Config.resourceGroup
    $appName = $Config.ui.appName
    $reactDir = Join-Path $ProjectRoot "react-ui"
    $deployDir = Join-Path $ProjectRoot ".publish/react-staging"
    $zipPath = Join-Path $ProjectRoot ".publish/react-ui.zip"
    $gameServiceUrl = $Config.gameService.url

    # Check staging slot exists
    $stagingSlot = Invoke-AzCommand "webapp deployment slot list --name $appName --resource-group $rgName --query `"[?name=='staging']`"" -FailOnError $false -JsonOutput
    if (-not $stagingSlot -or $stagingSlot.Count -eq 0) {
        Write-Log -Level "ERROR" -Message "Staging slot does not exist. Run './catan.ps1 azure install' first."
        return $false
    }

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

    # Create zip
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path "$deployDir/*" -DestinationPath $zipPath

    $zipSize = (Get-Item $zipPath).Length / 1MB
    Write-Log -Level "INFO" -Message "Deploying React UI to staging ($([math]::Round($zipSize, 1)) MB)..."

    # Configure staging slot for Node.js (production slot uses .NET for Blazor)
    Write-Log -Level "INFO" -Message "Configuring staging slot for Node.js..."
    Invoke-AzCommand "webapp config set --name $appName --resource-group $rgName --slot staging --linux-fx-version `"NODE|22-lts`" --startup-file `"node server.js`"" -SuppressOutput
    Invoke-AzCommand "webapp config appsettings set --name $appName --resource-group $rgName --slot staging --settings WEBSITE_NODE_DEFAULT_VERSION=~22 NEXT_PUBLIC_GAME_SERVICE_URL=$gameServiceUrl" -SuppressOutput

    # Deploy zip using Invoke-BackgroundInstaller to isolate az CLI progress bar output
    # (az webapp deploy writes ANSI progress directly to terminal, bypassing PowerShell redirection)
    # Set AZURE_CORE_ONLY_SHOW_ERRORS to suppress the progress bar that writes directly to /dev/tty
    $prevOnlyShowErrors = $env:AZURE_CORE_ONLY_SHOW_ERRORS
    $env:AZURE_CORE_ONLY_SHOW_ERRORS = "true"
    $deployArgs = @("webapp", "deploy", "--name", $appName, "--resource-group", $rgName, "--slot", "staging", "--src-path", $zipPath, "--type", "zip", "--async", "true")
    $deployResult = Invoke-BackgroundInstaller -FilePath "az" -ArgumentList $deployArgs -OperationName "Staging zip deploy" -ProgressInterval 2 -TraceLevel $TraceLevel
    $env:AZURE_CORE_ONLY_SHOW_ERRORS = $prevOnlyShowErrors
    if (-not $deployResult.Success) {
        Write-Log -Level "ERROR" -Message "az webapp deploy failed (exit $($deployResult.ExitCode))"
        return $false
    }

    # Store deployed commit in staging slot settings
    $commitHash = Get-GitCommitHash
    $buildTime = (Get-Date -Format "o")
    Invoke-AzCommand "webapp config appsettings set --name $appName --resource-group $rgName --slot staging --settings DEPLOY_COMMIT=$commitHash DEPLOY_BUILD_TIME=`"$buildTime`"" -SuppressOutput

    Write-Log -Level "INFO" -Message "React UI deployed to staging: https://$appName-staging.azurewebsites.net"
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
        # Note: F1 (Free) tier apps can take 30-60+ seconds to cold start, so we retry
        Write-Log -Level "DEBUG" -Message "Checking health endpoint: $url/health" -TraceLevel $TraceLevel
        $health = $null
        $maxRetries = 2
        $timeouts = @(15, 60)  # First try 15s, retry with 60s for cold start

        for ($retry = 0; $retry -lt $maxRetries; $retry++) {
            try {
                $timeout = $timeouts[$retry]
                if ($retry -gt 0) {
                    Write-Log -Level "INFO" -Message "Health check retry $retry (cold start likely, waiting up to ${timeout}s)..." -TraceLevel $TraceLevel
                }
                $health = Invoke-RestMethod -Uri "$url/health" -TimeoutSec $timeout
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
            resourceGroup          = $false
            appServicePlan         = $false
            webApp                 = $false
            managedIdentity        = $false
            gameServiceUrl         = $false
            codeDeployed           = $false
            siteResponding         = $false
            stagingSlot            = $false
            stagingRuntime         = $false
            stagingStartup         = $false
            stagingCodeDeployed    = $false
            stagingResponding      = $false
        }
        # Git commit tracking
        currentCommit         = $null
        deployedCommit        = $null
        stagingDeployedCommit = $null
        # What actions are needed
        needsInstall          = $false
        needsDeploy           = $false
        needsStagingDeploy    = $false
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

        # --- Parallel batch 1: production config checks (all independent once web app exists) ---
        Write-Log -Level "STATUS" -Message "Checking production configuration..."

        # Run independent az calls in parallel using background jobs
        $jobs = @()
        $jobs += Start-Job -ScriptBlock { az webapp config show --name $using:appName --resource-group $using:rgName --query linuxFxVersion -o tsv 2>$null }
        $jobs += Start-Job -ScriptBlock { az webapp identity show --name $using:appName --resource-group $using:rgName --query principalId -o tsv 2>$null }
        $jobs += Start-Job -ScriptBlock { az webapp config appsettings list --name $using:appName --resource-group $using:rgName 2>$null }
        $jobs += Start-Job -ScriptBlock { az webapp deployment slot list --name $using:appName --resource-group $using:rgName 2>$null }

        # Wait for all jobs (with STATUS updates)
        $allDone = $false
        while (-not $allDone) {
            $allDone = ($jobs | Where-Object { $_.State -eq 'Running' }).Count -eq 0
            if (-not $allDone) { Start-Sleep -Milliseconds 500 }
        }

        # Collect results
        $prodRuntime = (Receive-Job $jobs[0]).Trim()
        Write-Log -Level "DEBUG" -Message "Production runtime: $prodRuntime" -TraceLevel $TraceLevel
        $result.prodRuntime = $prodRuntime

        $identity = (Receive-Job $jobs[1]).Trim()
        $result.checks.managedIdentity = (-not [string]::IsNullOrWhiteSpace($identity))

        $appSettingsJson = (Receive-Job $jobs[2]) -join "`n"
        $appSettings = if ($appSettingsJson) { $appSettingsJson | ConvertFrom-Json -ErrorAction SilentlyContinue } else { $null }
        $configuredUrl = if ($appSettings) { ($appSettings | Where-Object { $_.name -eq 'GAMESERVICE_URL' } | Select-Object -First 1).value } else { $null }
        $result.checks.gameServiceUrl = ($configuredUrl -eq $gameServiceUrl)
        $result.deployedCommit = if ($appSettings) { ($appSettings | Where-Object { $_.name -eq 'DEPLOY_COMMIT' } | Select-Object -First 1).value } else { $null }
        Write-Log -Level "DEBUG" -Message "Deployed commit: $($result.deployedCommit)" -TraceLevel $TraceLevel

        $slotsJson = (Receive-Job $jobs[3]) -join "`n"
        $slots = if ($slotsJson) { $slotsJson | ConvertFrom-Json -ErrorAction SilentlyContinue } else { $null }
        $stagingSlotData = if ($slots) { $slots | Where-Object { $_.name -eq 'staging' } } else { $null }

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
            $result.needsDeploy = $true
        }

        # --- Parallel batch 2: HTTP health checks + staging config (all independent) ---
        Write-Log -Level "STATUS" -Message "Checking site health..."

        # Start HTTP checks as jobs (these are the slow ones: 10-15s timeout each)
        $httpJobs = @()
        $httpJobs += Start-Job -ScriptBlock {
            try {
                $response = Invoke-WebRequest -Uri $using:url -TimeoutSec 10 -UseBasicParsing -ErrorAction Stop
                return @{ ok = $true; status = $response.StatusCode }
            } catch {
                return @{ ok = $false; error = $_.Exception.Message }
            }
        }

        $stagingUrl = "https://$appName-staging.azurewebsites.net"
        $hasStagingSlot = ($null -ne $stagingSlotData)
        $result.checks.stagingSlot = $hasStagingSlot

        if ($hasStagingSlot) {
            # Start staging HTTP check in parallel
            $httpJobs += Start-Job -ScriptBlock {
                try {
                    $response = Invoke-WebRequest -Uri $using:stagingUrl -TimeoutSec 15 -UseBasicParsing -ErrorAction Stop
                    return @{ ok = $true; status = $response.StatusCode }
                } catch {
                    return @{ ok = $false; error = $_.Exception.Message }
                }
            }

            # Start staging config checks in parallel
            $stagingJobs = @()
            $stagingJobs += Start-Job -ScriptBlock { az webapp config show --name $using:appName --resource-group $using:rgName --slot staging 2>$null }
            $stagingJobs += Start-Job -ScriptBlock { az webapp config appsettings list --name $using:appName --resource-group $using:rgName --slot staging 2>$null }
        }

        # Wait for all jobs
        $allJobs = $httpJobs + $(if ($hasStagingSlot) { $stagingJobs } else { @() })
        $allDone = $false
        $lastStatusTime = Get-Date
        while (-not $allDone) {
            $running = ($allJobs | Where-Object { $_.State -eq 'Running' }).Count
            $allDone = $running -eq 0
            if (-not $allDone) {
                $elapsed = [math]::Round(((Get-Date) - $lastStatusTime).TotalSeconds)
                if ($elapsed -ge 2) {
                    Write-Log -Level "STATUS" -Message "Checking site health... ($running checks remaining)"
                    $lastStatusTime = Get-Date
                }
                Start-Sleep -Milliseconds 500
            }
        }

        # Collect production HTTP result
        $prodHttp = Receive-Job $httpJobs[0]
        if ($prodHttp.ok) {
            $result.checks.siteResponding = $true
            $result.checks.codeDeployed = $true
            $result.checks.gameServiceUrl = $true
        } else {
            $result.checks.siteResponding = $false
            Write-Log -Level "DEBUG" -Message "Site not responding: $($prodHttp.error)" -TraceLevel $TraceLevel
            # Only set needsDeploy if code isn't deployed or commit doesn't match
            # A timeout with matching commit means cold start, not missing deploy
            if (-not $result.checks.codeDeployed) {
                $result.needsDeploy = $true
            }
        }

        if ($hasStagingSlot) {
            # Collect staging HTTP result
            $stagingHttp = Receive-Job $httpJobs[1]
            $result.checks.stagingResponding = $stagingHttp.ok
            if ($stagingHttp.ok) {
                Write-Log -Level "DEBUG" -Message "Staging responding: HTTP $($stagingHttp.status)" -TraceLevel $TraceLevel
                $result.checks.stagingCodeDeployed = $true
            } else {
                Write-Log -Level "DEBUG" -Message "Staging not responding: $($stagingHttp.error)" -TraceLevel $TraceLevel
            }

            # Collect staging config results
            $stagingConfigJson = (Receive-Job $stagingJobs[0]) -join "`n"
            $stagingConfig = if ($stagingConfigJson) { $stagingConfigJson | ConvertFrom-Json -ErrorAction SilentlyContinue } else { $null }
            $stagingRuntime = if ($stagingConfig) { $stagingConfig.linuxFxVersion } else { $null }
            $stagingStartup = if ($stagingConfig) { $stagingConfig.appCommandLine } else { $null }
            Write-Log -Level "DEBUG" -Message "Staging runtime: $stagingRuntime" -TraceLevel $TraceLevel
            Write-Log -Level "DEBUG" -Message "Staging startup: $stagingStartup" -TraceLevel $TraceLevel
            $result.checks.stagingRuntime = ($stagingRuntime -like "NODE*")
            $result.stagingRuntime = $stagingRuntime
            $result.checks.stagingStartup = ($stagingStartup -eq "node server.js")
            $result.stagingStartup = $stagingStartup

            $stagingSettingsJson = (Receive-Job $stagingJobs[1]) -join "`n"
            $stagingSettings = if ($stagingSettingsJson) { $stagingSettingsJson | ConvertFrom-Json -ErrorAction SilentlyContinue } else { $null }
            $result.stagingDeployedCommit = if ($stagingSettings) { ($stagingSettings | Where-Object { $_.name -eq 'DEPLOY_COMMIT' } | Select-Object -First 1).value } else { $null }
            Write-Log -Level "DEBUG" -Message "Staging deployed commit: $($result.stagingDeployedCommit)" -TraceLevel $TraceLevel
            $result.checks.stagingCodeDeployed = $result.checks.stagingCodeDeployed -or (-not [string]::IsNullOrWhiteSpace($result.stagingDeployedCommit))

            # Determine if staging needs deploy (config or code issues, NOT just HTTP timeout)
            # A non-responding staging slot with correct config/code is a cold start, not a deploy issue
            if (-not $result.checks.stagingRuntime -or -not $result.checks.stagingStartup -or -not $result.checks.stagingCodeDeployed) {
                $result.needsStagingDeploy = $true
            }

            $stagingJobs | Remove-Job -Force
        }
        else {
            Write-Log -Level "DEBUG" -Message "Staging slot missing, needsInstall=true" -TraceLevel $TraceLevel
            $result.needsInstall = $true
        }

        $httpJobs | Remove-Job -Force

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
                "stagingSlot" { "Staging Slot" }
                "stagingRuntime" { "Staging Runtime" }
                "stagingStartup" { "Staging Startup Cmd" }
                "stagingCodeDeployed" { "Staging Code Deployed" }
                "stagingResponding" { "Staging Responding" }
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
                } elseif ($key -in @("publicNetworkAccess", "firewallRule")) {
                    "$script:CmdHintPrefix $noun fix"
                } elseif ($key -eq "stagingSlot") {
                    "$script:CmdHintPrefix $noun install"
                } elseif ($key -eq "stagingRuntime") {
                    $rt = if ($Result.stagingRuntime) { $Result.stagingRuntime } else { "unknown" }
                    "have: $rt, need: NODE|22-lts — $script:CmdHintPrefix ui deploy-staging"
                } elseif ($key -eq "stagingStartup") {
                    $sc = if ($Result.stagingStartup) { $Result.stagingStartup } else { "none" }
                    "have: $sc, need: node server.js — $script:CmdHintPrefix ui deploy-staging"
                } elseif ($key -eq "stagingResponding") {
                    if ($Result.checks.stagingCodeDeployed -and -not $Result.needsStagingDeploy) { "cold start — browse URL to wake" } else { "$script:CmdHintPrefix ui deploy-staging" }
                } elseif ($key -eq "stagingCodeDeployed") {
                    "$script:CmdHintPrefix ui deploy-staging"
                } else {
                    "$script:CmdHintPrefix $noun install"
                }
            }
            Show-CheckRow -Name $displayName -Status $Result.checks[$key] -Details $action
        }
    }

    # Show slot configuration summary (what's serving production vs staging)
    if ($Result.prodRuntime -or $Result.stagingRuntime) {
        Write-Host ""
        Write-Host "  Slot Configuration:" -ForegroundColor Yellow

        function Get-RuntimeLabel {
            param([string]$Runtime)
            if ([string]::IsNullOrWhiteSpace($Runtime)) { return "unknown" }
            if ($Runtime -like "DOTNETCORE*") { return "Blazor ($Runtime)" }
            if ($Runtime -like "NODE*") { return "React/Next.js ($Runtime)" }
            return $Runtime
        }

        $prodLabel = Get-RuntimeLabel $Result.prodRuntime
        $stagingLabel = if ($Result.checks.stagingSlot) { Get-RuntimeLabel $Result.stagingRuntime } else { "no slot" }

        Write-Host -NoNewline ("    Production").PadRight($col1)
        $prodColor = if ($Result.checks.siteResponding) { "Green" } else { "Red" }
        Write-Host $prodLabel -ForegroundColor $prodColor

        Write-Host -NoNewline ("    Staging").PadRight($col1)
        $stagingColor = if ($Result.checks.stagingResponding) { "Green" } elseif ($Result.checks.stagingSlot) { "Yellow" } else { "Red" }
        Write-Host $stagingLabel -ForegroundColor $stagingColor
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

    # Show staging commit info if available
    if ($Result.stagingDeployedCommit) {
        Write-Host -NoNewline ("  Staging Commit").PadRight($col1)
        if ($Result.currentCommit -eq $Result.stagingDeployedCommit) {
            Write-Host -NoNewline "MATCH".PadRight($col2) -ForegroundColor Green
            Write-Host "$($Result.stagingDeployedCommit)" -ForegroundColor Gray
        } else {
            Write-Host -NoNewline "MISMATCH".PadRight($col2) -ForegroundColor Yellow
            Write-Host "deployed: $($Result.stagingDeployedCommit) -> current: $($Result.currentCommit)" -ForegroundColor Gray
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
        Write-Host "$script:CmdHintPrefix $($Result.resource) install" -ForegroundColor Cyan
    } elseif ($Result.needsFix) {
        Write-Host "NEEDS FIX" -ForegroundColor Yellow
        Write-Host "  Recommended: " -NoNewline -ForegroundColor Gray
        Write-Host "$script:CmdHintPrefix $($Result.resource) fix" -ForegroundColor Cyan
    } elseif ($Result.needsDeploy) {
        Write-Host "NEEDS DEPLOY" -ForegroundColor Yellow
        if ($Result.deployReason) {
            Write-Host "  Reason: $($Result.deployReason)" -ForegroundColor Gray
        }
        Write-Host "  Recommended: " -NoNewline -ForegroundColor Gray
        Write-Host "$script:CmdHintPrefix $($Result.resource) deploy" -ForegroundColor Cyan
    } elseif ($Result.healthy -and $Result.coldStart) {
        Write-Host "HEALTHY " -ForegroundColor Green -NoNewline
        Write-Host "(site not responding — likely cold start, browse URL to wake)" -ForegroundColor Yellow
    } elseif ($Result.healthy) {
        Write-Host "HEALTHY" -ForegroundColor Green
    } else {
        Write-Host "UNKNOWN" -ForegroundColor Red
    }

    # Show staging deploy status separately (staging issues don't block production)
    if ($Result.needsStagingDeploy) {
        Write-Host -NoNewline "Staging: "
        Write-Host "NEEDS DEPLOY" -ForegroundColor Yellow
        Write-Host "  Recommended: " -NoNewline -ForegroundColor Gray
        Write-Host "$script:CmdHintPrefix ui deploy-staging" -ForegroundColor Cyan
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
    fix             Fix common connectivity issues (database only)
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
    ./catan-azure.ps1 database fix             Fix SQL network access and firewall
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
            $success = (Deploy-GameService -Config $config -Force $Force -NoBuild $NoBuild) -and
                       (Deploy-Database -Config $config -Force $Force) -and
                       (Deploy-UI -Config $config -Force $Force -NoBuild $NoBuild)
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
            "deploy" { $success = Deploy-GameService -Config $config -Force $Force -NoBuild $NoBuild }
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
        }
    }
    "ui" {
        switch ($Verb) {
            "install" { $success = Install-UI -Config $config }
            "deploy" { $success = Deploy-UI -Config $config -Force $Force -NoBuild $NoBuild }
            "deploy-staging" { $success = Deploy-ReactStaging -Config $config -Force $Force -TraceLevel $TraceLevel }
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

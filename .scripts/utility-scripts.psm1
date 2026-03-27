<#
.SYNOPSIS
utility functions for PowerShell scripts.
.DESCRIPTION
Contains common utility functions used across multiple PowerShell scripts,
including logging, user confirmation, process management, and elevation handling.
#>

# Script-level variable for minimum PowerShell version
$script:MinRequiredPowershellVersion = "7.0.0"

# Add this at the top of the module
$script:ModuleRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

<#
.SYNOPSIS
    Writes a log message with the specified level and trace level.
.DESCRIPTION
    Writes a formatted log message if the trace level permits.
.PARAMETER Level
    The message level (HEADER, ERROR, WARN, INFO, DEBUG, SILENT)
.PARAMETER Message
    The message to write
.PARAMETER TraceLevel
    The current trace level (ERROR, WARN, INFO, DEBUG)
#>

# Module-level variable to track if last output was STATUS
$script:LastOutputWasStatus = $false

<#
.SYNOPSIS
Short description

.DESCRIPTION
Long description

.PARAMETER Level
Parameter description

.PARAMETER Message
Parameter description

.PARAMETER TraceLevel
Parameter description

.PARAMETER Silent
Parameter description

.PARAMETER NoLabel
Parameter description

.EXAMPLE
An example

.NOTES
General notes
#>
function Write-Log {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("HEADER", "ERROR", "WARN", "INFO", "DEBUG", "STATUS")]
        [string]$Level,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Message,

        [Parameter(Mandatory = $true)]
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel,

        [Parameter(Mandatory = $false)]
        [switch]$Silent,

        [Parameter(Mandatory = $false)]
        [switch]$NoLabel,

        [Parameter(Mandatory = $false)]
        [switch]$NoNewline,

        [Parameter(Mandatory = $false)]
        [string]$ForegroundColor
    )

    # If Silent is set, only show DEBUG and STATUS messages
    if ($Silent -and $Level -ne "DEBUG" -and $Level -ne "STATUS") {
        return
    }

    # Define level weights for comparison
    $weights = @{
        "DEBUG" = 4
        "INFO" = 3
        "WARN" = 2
        "ERROR" = 1
    }

    # Define colors for each level
    $defaultColors = @{
        "HEADER" = "Blue"
        "ERROR" = "Red"
        "WARN" = "Yellow"
        "INFO" = "Green"
        "DEBUG" = "Gray"
        "STATUS" = "Cyan"
    }

    $traceLevelWeight = $weights[$TraceLevel]
    $messageLevelWeight = if ($Level -eq "HEADER" -or $Level -eq "STATUS") { 0 } else { $weights[$Level] }

    if ($Level -eq "HEADER" -or $Level -eq "STATUS" -or $messageLevelWeight -le $traceLevelWeight) {
        $indent = if ($Level -ne "HEADER") { "     " } else { "" }
        # Use caller-specified color, or fall back to level default
        $color = if ($ForegroundColor) { $ForegroundColor } else { $defaultColors[$Level] }

        # Add timestamp for all messages except STATUS
        if ($Level -eq "STATUS") {
            # STATUS messages overwrite the current line using PowerShell cursor positioning
            $logMessage = "$indent[$Level] $Message"
            # Track that we're outputting a STATUS message
            $script:LastOutputWasStatus = $true
        } elseif ($Level -eq "DEBUG") {
            # DEBUG uses wall-clock time with milliseconds for precise timing
            $currentTime = Get-Date -Format "HH:mm:ss.fff"
            $logMessage = if ($NoLabel) { "$indent$Message" } else { "$indent[$Level $currentTime] $Message" }
        } else {
            # All other levels (HEADER, ERROR, WARN, INFO) use HH:mm:ss format
            $currentTime = Get-Date -Format "HH:mm:ss"
            $logMessage = if ($NoLabel) { "$indent$Message" } else { "$indent[$Level $currentTime] $Message" }
        }

        if ($Level -eq "STATUS") {
            # For STATUS messages, clear the current line and rewrite
            try {
                # Use carriage return approach for better compatibility
                Write-Host "`r$(" " * 120)`r$logMessage" -ForegroundColor $color -NoNewline
                [Console]::Out.Flush()
            } catch {
                # Fallback for environments where formatting fails
                Write-Host "`r$logMessage" -ForegroundColor $color -NoNewline
                [Console]::Out.Flush()
            }
        } else {
            # If last output was STATUS and this isn't STATUS, clear the STATUS line first
            if ($script:LastOutputWasStatus) {
                try {
                    # Clear the STATUS line using carriage return
                    Write-Host "`r$(" " * 120)`r" -NoNewline
                } catch {
                    # Fallback if clearing fails
                    Write-Host ""
                }
                $script:LastOutputWasStatus = $false
            }

            # For all other message types, use regular output
            if ($NoNewline) {
                Write-Host $logMessage -ForegroundColor $color -NoNewline
            } else {
                Write-Host $logMessage -ForegroundColor $color
            }
        }
    }
}

<#
.SYNOPSIS
    Completes STATUS message display by clearing the line and moving to a new line.
.DESCRIPTION
    This function should be called after STATUS messages to ensure proper line handling
    and prevent interference with subsequent INFO/ERROR messages.
#>
function Complete-StatusMessage {
    # Clear the STATUS line properly by using empty STATUS message
    # This leaves cursor at start of blank line without ugly spacing
    Write-Log -Level "STATUS" -Message "" -TraceLevel "DEBUG"
}

<#
.SYNOPSIS
    Refreshes environment variables to pick up newly installed tools and configurations.
.DESCRIPTION
    Updates the current PowerShell session with the latest environment variables from
    both Machine and User scopes. This is essential after installations that modify
    PATH, RUST_HOME, CARGO_HOME, and other environment variables.
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG)
.EXAMPLE
    Update-EnvironmentVariables -TraceLevel "DEBUG"
    Refreshes all environment variables with debug logging
#>
function Update-EnvironmentVariables {
    param(
        [Parameter()]
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "INFO"
    )

    Write-Log -Level "DEBUG" -Message "Refreshing environment variables..." -TraceLevel $TraceLevel

    # Define important environment variables to refresh
    $envVarsToRefresh = @(
        "PATH",
        "NODE_PATH",
        "npm_config_prefix"
    )

    foreach ($envVar in $envVarsToRefresh) {
        # Get values from both Machine and User scopes
        $machineValue = [System.Environment]::GetEnvironmentVariable($envVar, "Machine")
        $userValue = [System.Environment]::GetEnvironmentVariable($envVar, "User")

        # Handle PATH specially (needs to be combined and de-duplicated)
        if ($envVar -eq "PATH") {
            # Start with machine PATH
            $pathEntries = @()
            if ($machineValue) {
                $pathEntries += $machineValue -split ';' | Where-Object { $_ }
            }

            # Add user PATH entries
            if ($userValue) {
                $userEntries = $userValue -split ';' | Where-Object { $_ }
                foreach ($entry in $userEntries) {
                    if ($pathEntries -notcontains $entry) {
                        $pathEntries += $entry
                    }
                }
            }

            # On Windows, ensure critical directories are present
            if ($IsWindows) {
                # Critical directories that should always be in PATH
                $criticalPaths = @(
                    "${env:LOCALAPPDATA}\Microsoft\WindowsApps",  # For winget MSIX app aliases
                    "${env:ProgramFiles}\nodejs",                  # For MSI-style Node.js installations
                    "${env:APPDATA}\npm"                          # For global npm packages
                )

                foreach ($criticalPath in $criticalPaths) {
                    if ((Test-Path $criticalPath) -and ($pathEntries -notcontains $criticalPath)) {
                        Write-Log -Level "DEBUG" -Message "Adding missing critical PATH entry: $criticalPath" -TraceLevel $TraceLevel
                        $pathEntries += $criticalPath
                    }
                }
            }

            # Join and set the combined PATH
            $combinedValue = $pathEntries -join ';'
            if ($combinedValue) {
                $env:PATH = $combinedValue
                Write-Log -Level "DEBUG" -Message "Updated PATH ($($pathEntries.Count) entries, $($combinedValue.Length) chars)" -TraceLevel $TraceLevel
            }
        } else {
            # For non-PATH variables, user scope takes precedence
            $finalValue = if ($userValue) { $userValue } elseif ($machineValue) { $machineValue } else { $null }

            if ($finalValue) {
                [System.Environment]::SetEnvironmentVariable($envVar, $finalValue, "Process")
                Write-Log -Level "DEBUG" -Message "Updated $envVar = $finalValue" -TraceLevel $TraceLevel
            }
        }
    }

    Write-Log -Level "DEBUG" -Message "Environment variables refreshed successfully" -TraceLevel $TraceLevel
}

<#
.SYNOPSIS
    Updates the current PowerShell session PATH to include newly installed Node.js.
.DESCRIPTION
    Specifically updates the current session's PATH environment variable to include
    Node.js installations from user-scope winget packages. This solves the issue where
    Node.js is installed but not accessible in the current shell session.
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG)
.EXAMPLE
    Update-CurrentSessionPath -TraceLevel "DEBUG"
    Updates current session PATH with debug logging
#>
function Update-CurrentSessionPath {
    param(
        [Parameter()]
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "INFO"
    )

    Write-Log -Level "DEBUG" -Message "Updating current session PATH..." -TraceLevel $TraceLevel

    if ($IsWindows) {
        # Find user-scope winget Node.js installations
        $wingetPaths = @()

        # Check for both regular and LTS Node.js packages
        $packagePatterns = @(
            "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\OpenJS.NodeJS_*\node-v*",
            "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\OpenJS.NodeJS.LTS_*\node-v*"
        )

        foreach ($pattern in $packagePatterns) {
            $nodeDirs = Get-ChildItem -Path $pattern -Directory -ErrorAction SilentlyContinue
            foreach ($nodeDir in $nodeDirs) {
                if (Test-Path "$($nodeDir.FullName)\node.exe") {
                    $wingetPaths += $nodeDir.FullName
                    Write-Log -Level "DEBUG" -Message "Found Node.js at: $($nodeDir.FullName)" -TraceLevel $TraceLevel
                }
            }
        }

        # Add winget paths to current session PATH (most recent version first)
        if ($wingetPaths.Count -gt 0) {
            # Sort by version number to get the newest first
            # Extract version numbers and sort properly (25.0.0 should come before 22.20.0)
            $sortedPaths = $wingetPaths | Sort-Object {
                # Extract version from path like "node-v25.0.0-win-x64" or "node-v22.20.0-win-x64"
                if ($_ -match 'node-v(\d+)\.(\d+)\.(\d+)') {
                    # Create sortable version number: major * 10000 + minor * 100 + patch
                    [int]$matches[1] * 10000 + [int]$matches[2] * 100 + [int]$matches[3]
                } else {
                    0
                }
            } -Descending

            foreach ($wingetPath in $sortedPaths) {
                if ($env:PATH -notlike "*$wingetPath*") {
                    # Add to beginning of PATH so it takes precedence
                    $env:PATH = "$wingetPath;$env:PATH"
                    Write-Log -Level "DEBUG" -Message "Added Node.js to current session PATH: $wingetPath" -TraceLevel $TraceLevel
                }
            }
        }

        # Also ensure npm global directory is in PATH
        $npmGlobalPath = "$env:APPDATA\npm"
        if ($env:PATH -notlike "*$npmGlobalPath*") {
            $env:PATH = "$npmGlobalPath;$env:PATH"
            Write-Log -Level "DEBUG" -Message "Added npm global to current session PATH: $npmGlobalPath" -TraceLevel $TraceLevel
        }
    }

    Write-Log -Level "DEBUG" -Message "Current session PATH updated successfully" -TraceLevel $TraceLevel
}

<#
.SYNOPSIS
    Gets user confirmation for an action.
.PARAMETER Question
    The question to ask the user
.PARAMETER ShowAll
    If set, includes "Yes to All" and "No to All" options
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG)
.PARAMETER Yes
    Automatically answers yes to prompts
.RETURNS
    [string] One of: 'Yes', 'No', 'YesToAll', 'NoToAll'
.EXAMPLE
    Get-UserConfirmation -Question "Continue with installation?"
#>
<#
.SYNOPSIS
Short description

.DESCRIPTION
Long description

.PARAMETER Question
Parameter description

.PARAMETER ShowAll
Parameter description

.PARAMETER TraceLevel
Parameter description

.PARAMETER Yes
Parameter description

.EXAMPLE
Get-UserConfirmation -Question "Continue with installation?" -TraceLevel INFO
Prompts user with Yes/No options with INFO level output.

.NOTES
Provides interactive confirmation with configurable output levels and options.
#>

<#
.SYNOPSIS
    Prompts the user for confirmation with customizable options and output control.
.DESCRIPTION
    Displays a question to the user and provides Yes/No options, with optional additional choices.
    Supports configurable trace levels and silent operation.
.PARAMETER Question
    The question to ask the user.
.PARAMETER ShowAll
    Show additional options beyond Yes/No.
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG).
.PARAMETER Silent
    Suppresses non-essential output.
.EXAMPLE
    Get-UserConfirmation -Question "Continue with installation?" -TraceLevel INFO
    Prompts user with Yes/No options with INFO level output.
#>
function Get-UserConfirmation {
    param (
        [Parameter(Mandatory = $true)]
        [string]$Question,

        [Parameter()]
        [switch]$ShowAll,

        [Parameter()]
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR",

        [Parameter()]
        [switch]$Yes
    )

    # If Yes is true, return 'Yes' without prompting
    if ($Yes) {
        return 'Yes'
    }

    if ($ShowAll) {
        $choices = @(
            [System.Management.Automation.Host.ChoiceDescription]::new("&Yes", "Proceed with this action.")
            [System.Management.Automation.Host.ChoiceDescription]::new("&No", "Skip this action.")
            [System.Management.Automation.Host.ChoiceDescription]::new("Yes to &All", "Yes to all remaining actions.")
            [System.Management.Automation.Host.ChoiceDescription]::new("No to A&ll", "No to all remaining actions.")
        )
    } else {
        $choices = @(
            [System.Management.Automation.Host.ChoiceDescription]::new("&Yes", "Proceed with this action.")
            [System.Management.Automation.Host.ChoiceDescription]::new("&No", "Skip this action.")
        )
    }

    $result = $host.UI.PromptForChoice("", $Question, $choices, 1)  # Default to No (1)

    if ($ShowAll) {
        switch ($result) {
            0 { return 'Yes' }
            1 { return 'No' }
            2 { return 'YesToAll' }
            3 { return 'NoToAll' }
        }
    } else {
        switch ($result) {
            0 { return 'Yes' }
            1 { return 'No' }
        }
    }
}

<#
.SYNOPSIS
    Elevates the current script with admin privileges.
.DESCRIPTION
    Restarts the calling script with administrator privileges, preserving all parameters.
    Captures and displays output from the elevated process.
.PARAMETER BoundParameters
    The bound parameters from the calling script
#>
function Invoke-ElevatedInstance {
    param (
        [Parameter(Mandatory = $true)]
        [hashtable]$BoundParameters,

        [Parameter()]
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    try {
        $isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
        if ($isAdmin) {
            return
        }



        Write-Log -Level "WARN" -Message "This script requires Administrator privileges" -TraceLevel $TraceLevel
        if (Get-UserConfirmation -Question "Run as Administrator?" -TraceLevel $TraceLevel) {
            # Get the calling script path
            $callStack = Get-PSCallStack
            $scriptPath = $callStack[1].ScriptName

            # Get all command line arguments
            $cmdArgs = [Environment]::GetCommandLineArgs()

            # Find the script name in the arguments and take everything after it
            $scriptNameIndex = $cmdArgs.IndexOf($scriptPath)
            if ($scriptNameIndex -ge 0) {
                $argList = $cmdArgs[($scriptNameIndex + 1)..($cmdArgs.Length - 1)] -join ' '
            } else {
                # Fallback if we can't find the script name
                $argList = $cmdArgs[1..($cmdArgs.Length - 1)] -join ' '
            }

            Write-Log -Level "INFO" -Message "Script path: $scriptPath" -TraceLevel $TraceLevel
            Write-Log -Level "INFO" -Message "Arguments: $argList" -TraceLevel $TraceLevel

            $scriptContent = @"
            try {
                Write-Host "Executing: $scriptPath $argList"
                & "$scriptPath" $argList
                `$exitCode = `$LASTEXITCODE
            }
            finally {
                Write-Host "`nPress any key to continue..."
                `$null = `$Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
                exit `$exitCode
            }
"@
            Write-Log -Level "INFO" -Message "Temp script content:`n$scriptContent" -TraceLevel $TraceLevel

            # Create a temporary script that runs our script and then waits
            $tempScript = [System.IO.Path]::GetTempFileName()
            Rename-Item -Path $tempScript -NewName "$tempScript.ps1"
            $tempScript = "$tempScript.ps1"

            $scriptContent | Out-File -FilePath $tempScript

            # Start new elevated PowerShell process
            $psi = New-Object System.Diagnostics.ProcessStartInfo
            $psi.FileName = "pwsh.exe"
            $psi.Arguments = "-NoProfile -ExecutionPolicy RemoteSigned -File `"$tempScript`""
            $psi.Verb = "RunAs"
            $psi.UseShellExecute = $true
            $psi.WindowStyle = 'Normal'

            $proc = [System.Diagnostics.Process]::Start($psi)
            $proc.WaitForExit()

            # Clean up temp script
            if (Test-Path $tempScript) {
                Remove-Item $tempScript -Force
            }

            exit $proc.ExitCode
        } else {
            Write-Log -Level "ERROR" -Message "Script requires Administrator privileges to continue" -TraceLevel $TraceLevel
            exit 1
        }
    } catch {
        Write-Log -Level "ERROR" -Message "Failed to restart as Administrator: $_" -TraceLevel $TraceLevel
        exit 1
    }
}



<#
.SYNOPSIS
    Runs a script with elevated privileges.
.DESCRIPTION
    Creates and executes an elevated PowerShell instance to run the specified script
    with the given parameters and switches.
.PARAMETER ScriptPath
    Full path to the script to run
.PARAMETER Parameters
    Hashtable of parameter name/value pairs to pass to the script
.PARAMETER Switches
    Array of switch names to enable
.EXAMPLE
    Run-Elevated -ScriptPath ".\script.ps1" -Parameters @{Name="value"} -Switches @("Force","Verbose")
#>
function Invoke-Elevated {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ScriptPath,

        [Parameter()]
        [hashtable]$Parameters = @{},

        [Parameter()]
        [string[]]$Switches = @(),

        [Parameter()]
        [switch]$Wait = $false,

        [Parameter()]
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    try {
        $isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
        if ($isAdmin) {
            return
        }

        # Check if Silent is specified in parameters (not currently used but may be needed for future functionality)

        Write-Log -Level "WARN" -Message "Operation requires elevation. Restarting as administrator..." -TraceLevel $TraceLevel

        # Skip prompt if Yes is in Parameters or Switches
        $autoYes = $Parameters.ContainsKey('Yes') -and $Parameters['Yes'] -eq $true
        if (-not $autoYes) {
            $autoYes = $Switches -contains 'Yes'
        }

        if ($autoYes -or (Get-UserConfirmation -Question "Run as Administrator?" -TraceLevel $TraceLevel)) {
            # Build argument list - module import is handled in the ProcessStartInfo arguments

            $psi = New-Object System.Diagnostics.ProcessStartInfo
            $psi.FileName = "pwsh.exe"
            $psi.Arguments = "-NoProfile -ExecutionPolicy RemoteSigned -Command `"& { Import-Module '$PSScriptRoot\utility-scripts.psd1' -Force; Set-Location '$PWD'; & '$ScriptPath' $argString }`""
            $psi.Verb = "RunAs"
            $psi.UseShellExecute = $true
            $psi.WindowStyle = 'Normal'

            Write-Log -Level "INFO" -Message "Starting elevated process..." -TraceLevel $TraceLevel

            $proc = [System.Diagnostics.Process]::Start($psi)
            if ($Wait) {
                $proc.WaitForExit()

                # Only display Write-Log entries from the elevated process
                if (Test-Path $script:ElevatedLogFile) {
                    Get-Content $script:ElevatedLogFile | ForEach-Object {
                        Write-Host $_
                    }
                    Remove-Item $script:ElevatedLogFile -Force
                }

                return ($proc.ExitCode -eq 0)
            }
            return $true
        } else {
            Write-Log -Level "ERROR" -Message "Operation cancelled: elevation required" -TraceLevel $TraceLevel
            return $false
        }
    } catch {
        Write-Log -Level "ERROR" -Message "Failed to run elevated: $_" -TraceLevel $TraceLevel
        return $false
    }
}

<#
.SYNOPSIS
    Pauses execution and waits for user input.

.DESCRIPTION
    Displays a "Press any key to continue..." message and waits for the user to press any key
    before continuing execution. Useful for keeping windows open or adding user interaction points.

.PARAMETER Message
    Optional custom message to display. If not provided, uses default message.

.PARAMETER Level
    Log level for the message (INFO, WARN, or ERROR). Defaults to INFO.

.EXAMPLE
    WaitForUser
    # Shows "Press any key to continue..." and waits

.EXAMPLE
    WaitForUser -Message "Press any key when the installer completes..." -Level "WARN"
    # Shows custom warning message and waits
#>
function WaitForUser {
    param(
        [Parameter()]
        [string]$Message = "Press any key to continue...",

        [Parameter()]
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    Write-Log -Level "INFO" -Message $Message -TraceLevel $TraceLevel
    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
}

<#
.SYNOPSIS
    Stops specified running processes.
.DESCRIPTION
    Attempts to gracefully stop running processes by name. Useful for ensuring
    applications are closed before modifying their files or configurations.
.PARAMETER ProcessNames
    Array of process names to stop (without .exe extension)
.PARAMETER TraceLevel
    Level of logging detail (ERROR, WARN, INFO, DEBUG)
.EXAMPLE
    Stop-RunningProcesses -ProcessNames @("notepad", "calc") -TraceLevel INFO
#>
function Stop-RunningProcesses {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$ProcessNames,

        [Parameter()]
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    foreach ($procName in $ProcessNames) {
        $processes = Get-Process -Name $procName -ErrorAction SilentlyContinue
        if ($processes) {
            Write-Log -Level "INFO" -Message "Stopping process: $procName" -TraceLevel $TraceLevel
            $processes | Stop-Process -Force -ErrorAction SilentlyContinue
        }
    }
}

<#
.SYNOPSIS
    Runs an installer process in the background with progress monitoring and filtered output.
.DESCRIPTION
    Executes an installer with clean output suppression while providing periodic status updates.
    Monitors the process and provides progress feedback without overwhelming the user with verbose output.
.PARAMETER FilePath
    Path to the installer executable
.PARAMETER ArgumentList
    Arguments to pass to the installer
.PARAMETER OperationName
    Name of the operation for progress messages (e.g., "Rust installation", "VS Build Tools installation")
.PARAMETER ProgressInterval
    Interval in seconds between progress updates (default: 30)
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG)
.OUTPUTS
    [bool] True if installation succeeded, False otherwise
.EXAMPLE
    Invoke-BackgroundInstaller -FilePath "rustup-init.exe" -ArgumentList "-y" -OperationName "Rust installation" -TraceLevel "INFO"
#>
function Invoke-BackgroundInstaller {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter()]
        [string[]]$ArgumentList = @(),

        [Parameter(Mandatory = $true)]
        [string]$OperationName,

        [Parameter()]
        [int]$ProgressInterval = 1,

        [Parameter()]
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR",

        [Parameter()]
        [switch]$ShowLastLogLine,
        [Parameter()]
        [switch]$IsUninstallOperation
    )

    try {
        Write-Log -Level "INFO" -Message "Starting $OperationName..." -TraceLevel $TraceLevel

        # Create temp files for output capture to suppress verbose output (cross-platform)
        $tempDir = if ($IsWindows) { $env:TEMP } else { "/tmp" }
        $pathSeparator = if ($IsWindows) { "\" } else { "/" }
        $stdoutFile = "${tempDir}${pathSeparator}installer-stdout-$(Get-Random).log"
        $stderrFile = "${tempDir}${pathSeparator}installer-stderr-$(Get-Random).log"

        # Log the exact command being executed for debugging
        $argumentString = if ($ArgumentList) { ($ArgumentList -join " ") } else { "(no arguments)" }
        Write-Log -Level "DEBUG" -Message "Executing command: $FilePath $argumentString" -TraceLevel $TraceLevel

        # Start the installer process with output redirection
        $process = Start-Process -FilePath $FilePath -ArgumentList $ArgumentList -PassThru -NoNewWindow -RedirectStandardOutput $stdoutFile -RedirectStandardError $stderrFile

        # Monitor the process with periodic status updates
        $startTime = Get-Date
        $lastUpdate = $startTime

        # Show immediate status for all operations
        Write-Log -Level "STATUS" -Message "$OperationName in progress... (0s elapsed)" -TraceLevel $TraceLevel
        # Don't update lastUpdate here - we want the next update to happen based on ProgressInterval

        while (-not $process.HasExited) {
            $currentTime = Get-Date
            $elapsedSeconds = [math]::Round(($currentTime - $startTime).TotalSeconds)

            # Show status update at the specified progress interval
            if (($currentTime - $lastUpdate).TotalSeconds -ge $ProgressInterval) {
                # Format time appropriately
                if ($elapsedSeconds -lt 120) {
                    $timeDisplay = "${elapsedSeconds}s"
                } else {
                    $minutes = [math]::Round($elapsedSeconds / 60.0, 1)
                    $timeDisplay = "${minutes}m"
                }

                Write-Log -Level "STATUS" -Message "$OperationName in progress... ($timeDisplay elapsed)" -TraceLevel $TraceLevel
                $lastUpdate = $currentTime
            }

            Start-Sleep -Seconds 1
        }

        # Clear any remaining STATUS message and move to new line
        Complete-StatusMessage

        # Wait for process to fully complete and get exit code
        $process.WaitForExit()
        $exitCode = $process.ExitCode

        # Check for meaningful errors if installation failed
        if ($exitCode -ne 0 -and $exitCode -ne 3010) {
            Write-Log -Level "DEBUG" -Message "Checking installation logs for errors..." -TraceLevel $TraceLevel

            # Look for meaningful error messages in both stdout and stderr
            $allContent = @()
            if (Test-Path $stdoutFile) {
                $allContent += Get-Content $stdoutFile -ErrorAction SilentlyContinue
            }
            if (Test-Path $stderrFile) {
                $allContent += Get-Content $stderrFile -ErrorAction SilentlyContinue
            }

            # Show relevant error messages
            $meaningfulErrors = $allContent | Where-Object {
                $_ -match "(Error|Failed|Exception)" -and
                $_ -notmatch "(Unterminated string|base64|channelItems|Telemetry)"
            }

            # For uninstall operations, filter out "already removed" errors (these are actually success)
            if ($IsUninstallOperation) {
                $meaningfulErrors = $meaningfulErrors | Where-Object {
                    $_ -notmatch "(No such keg|not found|not installed|already removed|not available)"
                }
            }

            if ($meaningfulErrors) {
                Write-Log -Level "ERROR" -Message "$OperationName errors found:" -TraceLevel $TraceLevel
                foreach ($errorMessage in $meaningfulErrors | Select-Object -First 3) {
                    $trimmedMessage = $errorMessage.Trim()
                    if (-not [string]::IsNullOrEmpty($trimmedMessage)) {
                        Write-Log -Level "ERROR" -Message "  $trimmedMessage" -TraceLevel $TraceLevel
                    }
                }
            }
        }

        # Clean up temp files
        Remove-Item $stdoutFile -ErrorAction SilentlyContinue
        Remove-Item $stderrFile -ErrorAction SilentlyContinue

        # Return result object with exit code - let caller interpret what it means
        return @{
            Success = ($exitCode -eq 0 -or $exitCode -eq 3010)
            ExitCode = $exitCode
            OperationName = $OperationName
        }
    } catch {
        Write-Log -Level "ERROR" -Message "Failed to run $OperationName : $_" -TraceLevel $TraceLevel
        return @{
            Success = $false
            ExitCode = -1
            OperationName = $OperationName
        }
    }
}

<#
.SYNOPSIS
    Gets detailed information about the current PowerShell version.
.DESCRIPTION
    Returns detailed information about the current PowerShell version, including
    edition, version number, and platform details.
.PARAMETER ShowDetails
    If set, displays additional details about the PowerShell environment.
.EXAMPLE
    Get-PowerShellVersion
    # Shows basic version info
.EXAMPLE
    Get-PowerShellVersion -ShowDetails
    # Shows detailed version info
#>
function Get-PowerShellVersion {
    param(
        [Parameter()]
        [switch]$ShowDetails,

        [Parameter()]
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    $version = $PSVersionTable.PSVersion
    $edition = $PSVersionTable.PSEdition
    $platform = $PSVersionTable.Platform

    Write-Log -Level "INFO" -Message "PowerShell Version: $version" -TraceLevel $TraceLevel
    Write-Log -Level "INFO" -Message "Edition: $edition" -TraceLevel $TraceLevel
    Write-Log -Level "INFO" -Message "Platform: $platform" -TraceLevel $TraceLevel

    if ($ShowDetails) {
        Write-Log -Level "INFO" -Message "OS: $($PSVersionTable.OS)" -TraceLevel $TraceLevel
        Write-Log -Level "INFO" -Message ".NET Version: $($PSVersionTable.CLRVersion)" -TraceLevel $TraceLevel
    }

    return $PSVersionTable
}

<#
.SYNOPSIS
    Loads the Azure configuration from .azure/catan-azure.json.
.DESCRIPTION
    Reads and parses the Azure configuration file from the project root.
    Exits with error if the file is not found.
.PARAMETER ProjectRoot
    The root directory of the project (where .azure/ lives).
#>
function Get-AzureConfig {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectRoot
    )

    $configFile = Join-Path $ProjectRoot ".azure/catan-azure.json"
    if (-not (Test-Path $configFile)) {
        Write-Host "Azure configuration not found at $configFile" -ForegroundColor Red
        Write-Host "Run './catan.ps1 azure install' first." -ForegroundColor Yellow
        exit 1
    }

    return Get-Content $configFile -Raw | ConvertFrom-Json
}

<#
.SYNOPSIS
    Resolves the Azure GameService URL from config, deriving from baseName if needed.
.DESCRIPTION
    Returns gameService.url if present in the config. Otherwise derives the URL
    from baseName using the convention: https://{baseName}-api.azurewebsites.net
.PARAMETER AzureConfig
    The parsed Azure configuration object from Get-AzureConfig.
#>
function Get-AzureGameServiceUrl {
    param(
        [Parameter(Mandatory = $true)]
        $AzureConfig
    )

    $url = $AzureConfig.gameService.url
    if (-not $url -and $AzureConfig.baseName) {
        $url = "https://$($AzureConfig.baseName)-api.azurewebsites.net"
    }
    if (-not $url) {
        Write-Host "Cannot determine Azure GameService URL. Set gameService.url in .azure/catan-azure.json or ensure baseName is set." -ForegroundColor Red
        exit 1
    }
    return $url
}


# Export all functions
Export-ModuleMember -Function @(
    'Write-Log',
    'Complete-StatusMessage',
    'Update-EnvironmentVariables',
    'Update-CurrentSessionPath',
    'Get-UserConfirmation',
    'Invoke-ElevatedInstance',
    'Invoke-Elevated',
    'WaitForUser',
    'Stop-RunningProcesses',
    'Get-PowerShellVersion',
    'Invoke-BackgroundInstaller',
    'Get-AzureConfig',
    'Get-AzureGameServiceUrl'
)

#!/usr/bin/env pwsh
param(
    [Parameter(Position=0)]
    [string]$Arg0,
    [switch]$Clean,
    [switch]$NoBuild,
    [switch]$NoTest,
    [switch]$Release,
    [switch]$Help,
    [ValidateSet("x64", "x86", "ARM64")]
    [string]$Platform = "x64"
)

# Platform detection (use built-in automatic variables in PS Core, fallback for PS 5.1)
if (-not (Test-Path variable:IsMacOS)) { $script:IsMacOS = $false }
if (-not (Test-Path variable:IsLinux)) { $script:IsLinux = $false }
if (-not (Test-Path variable:IsWindows)) { $script:IsWindows = $true }

# Function to check and install .NET SDK if needed
function Initialize-DotNetSdk {
    # Read required version from global.json
    $globalJsonPath = Join-Path $PSScriptRoot "global.json"
    if (-not (Test-Path $globalJsonPath)) {
        Write-Output "⚠️  No global.json found, skipping SDK version check"
        return $true
    }

    $globalJson = Get-Content $globalJsonPath | ConvertFrom-Json
    $requiredVersion = $globalJson.sdk.version
    $majorVersion = $requiredVersion.Split('.')[0]

    # Check if dotnet command exists
    $dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnetCmd) {
        Write-Output "⚠️  dotnet command not found"
        return Install-DotNetSdk -MajorVersion $majorVersion
    }

    # Check if required SDK is installed
    $installedSdks = & dotnet --list-sdks 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Output "⚠️  dotnet --list-sdks failed"
        return Install-DotNetSdk -MajorVersion $majorVersion
    }

    $hasRequiredSdk = $installedSdks | Where-Object { $_ -match "^$majorVersion\." }

    if ($hasRequiredSdk) {
        Write-Output "✅ .NET $majorVersion SDK found"
        return $true
    }

    Write-Output "⚠️  .NET $majorVersion SDK not found"
    return Install-DotNetSdk -MajorVersion $majorVersion
}

function Install-DotNetSdk {
    param([string]$MajorVersion)

    if ($IsWindows) {
        Write-Output "❌ .NET $MajorVersion SDK is required but not installed"
        Write-Output "💡 Download from: https://dotnet.microsoft.com/download/dotnet/$MajorVersion.0"
        return $false
    }

    if ($IsMacOS) {
        # macOS: Use Homebrew (installs to standard PATH location)
        $brewCmd = Get-Command brew -ErrorAction SilentlyContinue
        if (-not $brewCmd) {
            Write-Output "❌ Homebrew not found. Install from https://brew.sh"
            Write-Output "💡 Or manually install .NET from: https://dotnet.microsoft.com/download/dotnet/$MajorVersion.0"
            return $false
        }

        Write-Output "📦 Installing .NET $MajorVersion SDK via Homebrew..."
        & brew install "dotnet-sdk@$MajorVersion"

        if ($LASTEXITCODE -eq 0) {
            Write-Output "✅ .NET $MajorVersion SDK installed via Homebrew"
            # Homebrew may require linking
            & brew link --overwrite "dotnet-sdk@$MajorVersion" 2>$null
            return $true
        } else {
            Write-Output "❌ Homebrew installation failed"
            return $false
        }
    } else {
        # Linux: Use dotnet-install script
        Write-Output "📦 Installing .NET $MajorVersion SDK via dotnet-install script..."
        $installDir = "$HOME/.dotnet"
        $installScript = "/tmp/dotnet-install.sh"

        try {
            Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.sh" -OutFile $installScript -ErrorAction Stop
            & chmod +x $installScript
            & bash $installScript --channel "$MajorVersion.0" --install-dir $installDir

            if ($LASTEXITCODE -eq 0) {
                $env:DOTNET_ROOT = $installDir
                $env:PATH = "${installDir}:$env:PATH"
                Write-Output "✅ .NET SDK installed to $installDir"
                Write-Output "💡 Add to ~/.bashrc: export PATH=`$HOME/.dotnet:`$PATH"
                return $true
            }
        } catch {
            Write-Output "❌ Failed: $($_.Exception.Message)"
        }
        Write-Output "💡 Install manually from: https://dotnet.microsoft.com/download/dotnet/$MajorVersion.0"
        return $false
    }
}

# Custom error handling for parameter validation
trap {
    if ($_.Exception.Message -like "*cannot validate argument*" -and $_.Exception.Message -like "*--*") {
        Write-Error "❌ Invalid parameter format detected!"
        Show-Help
        Close-Log
        exit 1
    }
    Write-Error "❌ Parameter error: $($_.Exception.Message)"
    Show-Help
    Close-Log
    exit 1
}

# Function to show help
function Show-Help {
    Write-Output @"
Catan Build Script

USAGE:
    .\build.ps1 [OPTIONS]

OPTIONS:
    -Clean          Clean the project before building
    -NoBuild        Skip the build step, only publish
    -NoTest         Skip running tests
    -Release        Build in Release configuration (default: Debug)
    -Verbose        Enable verbose output (built-in PowerShell)
    -Help           Show this help message

EXAMPLES:
    .\build.ps1                           # Build everything (default)
    .\build.ps1 -Clean -Release           # Clean release build
    .\build.ps1 -NoTest                   # Build without tests

DEFAULTS:
    By default, this script builds Shared, GameService, and CLI projects, then runs tests.
    UI tests are not included (Desktop app has been deprecated).

NOTE: Use PowerShell syntax (-Parameter) not bash syntax (--parameter)

COMMON ERRORS:
    --help     →  -Help
    --clean    →  -Clean
    --release  →  -Release
    --no-test  →  -NoTest
    --no-build →  -NoBuild
"@
}

# Helper: log commands in a readable, copy-pastable way
function Write-Command([string]$command, [object[]]$cmdArgs) {
    $fmt = $cmdArgs | ForEach-Object {
        if ($_ -is [string] -and $_ -match '\s') { '"' + $_ + '"' } else { $_ }
    }
    Write-Output ("» {0} {1}" -f $command, ($fmt -join ' '))
}

# Summarize one or more TRX files with failed tests
function Get-TrxSummary {
    param(
        [Parameter(Mandatory=$true)][string[]]$Paths
    )
    $totalFailed = 0

    foreach ($path in $Paths) {
        if (-not (Test-Path $path)) { continue }
        try {
            [xml]$doc = Get-Content -Path $path -Raw -ErrorAction Stop
        } catch {
            Write-Output "⚠️  Could not read TRX: $path - $($_.Exception.Message)"
            continue
        }

        $counters = $doc.TestRun.ResultSummary.Counters
        $failed = [int]($counters.failed)
        $passed = [int]($counters.passed)
        $total  = [int]($counters.total)
        $skipped = [int]($counters.total) - [int]($counters.executed)

        # Try to get assembly name from first UnitTest storage
        $storage = $doc.TestRun.TestDefinitions.UnitTest | Select-Object -First 1 | ForEach-Object { $_.storage }
        $assembly = if ($storage) { Split-Path -Leaf $storage } else { Split-Path -Leaf $path }

        $failedResults = @($doc.TestRun.Results.UnitTestResult | Where-Object { $_.outcome -eq 'Failed' })
        $totalFailed += $failedResults.Count

        Write-Output "\n🧾 TRX Summary: $assembly"
        Write-Output "   Total: $total | Passed: $passed | Failed: $failed | Skipped: $skipped"
        Write-Output "   File: $path"

        if ($failedResults.Count -gt 0) {
            # Build map from testId to UnitTest for full names
            $unitMap = @{}
            foreach ($u in $doc.TestRun.TestDefinitions.UnitTest) { $unitMap[$u.id] = $u }

            foreach ($r in $failedResults) {
                $name = $r.testName
                if ($r.testId -and $unitMap.ContainsKey($r.testId)) {
                    $u = $unitMap[$r.testId]
                    $class = $u.TestMethod.className
                    $method = $u.TestMethod.name
                    if ($class -and $method) { $name = "$class.$method" }
                }
                $msg = $r.Output.ErrorInfo.Message
                if ($msg) { $msg = $msg -replace "\r?\n", ' ' }
                $msg = ($msg | Select-Object -First 1)
                Write-Output ("   ❌ {0}" -f $name)
                if ($msg) { Write-Output ("      ↳ {0}" -f ($msg.Substring(0, [Math]::Min(200, $msg.Length)))) }
            }
        } else {
            Write-Output "   ✅ No failures"
        }
    }

    return $totalFailed
}

# Logging helpers
function Close-Log {
    if ($script:TranscriptStarted) {
        try { Stop-Transcript | Out-Null } catch { $null = $_ }
        $script:TranscriptStarted = $false
    }
}

# Initialize transcript logging to build.log unless running under tee wrapper
$script:TranscriptStarted = $false
$script:LogPath = $null
if (-not $env:BUILD_TEE) {
    $script:LogPath = Join-Path $PSScriptRoot "build.log"
    try {
        if (Test-Path $script:LogPath) { Remove-Item -Path $script:LogPath -Force -ErrorAction SilentlyContinue }
        Start-Transcript -Path $script:LogPath -Force | Out-Null
        $script:TranscriptStarted = $true
    } catch {
        Write-Output "⚠️  Unable to start logging to ${script:LogPath}: $($_.Exception.Message)"
    }
}

# (tee wrapper handles logging of external commands; invoke them directly)

# Handle bash-style --help (captured via Arg0)
if ($Arg0) {
    if ($Arg0 -eq "--help" -or $Arg0 -eq "-h" -or $Arg0 -eq "help") {
        Show-Help
        Close-Log
        exit 0
    }
    if ($Arg0 -like "--*") {
        Write-Error "❌ Invalid parameter format detected!"
        Show-Help
        Close-Log
        exit 1
    }
}

# Show help if requested
if ($Help) {
    Show-Help
    Close-Log
    exit 0
}

# Configuration
$Configuration = if ($Release) { "Release" } else { "Debug" }
$VerbosityLevel = if ($VerbosePreference -eq 'Continue') { "normal" } else { "minimal" }

# Project list
$CrossPlatformProjects = @(
    "Catan3.Shared/Catan3.Shared.csproj",
    "Catan3.GameService/Catan3.GameService.csproj",
    "Catan3.CLI/Catan3.CLI.csproj"
)

Write-Output "🏗️  Catan Build Script"
Write-Output "Configuration: $Configuration | Platform: $(if ($IsMacOS) { 'macOS' } elseif ($IsLinux) { 'Linux' } else { $Platform })"

# Check Visual Studio environment for symbol generation (Windows only)
if ($IsWindows) {
    $systemVCToolsDir = [System.Environment]::GetEnvironmentVariable("VCToolsInstallDir", "Machine")
    if ($systemVCToolsDir) {
        Write-Output "🔧 Visual Studio environment found for symbol generation"
        Write-Output "   VCToolsInstallDir: $systemVCToolsDir"

        # Ensure the current session has the environment variable
        if (-not $env:VCToolsInstallDir) {
            $env:VCToolsInstallDir = $systemVCToolsDir
            Write-Output "   ↳ Applied to current session"
        }
    } else {
        Write-Output "⚠️  VCToolsInstallDir system environment variable not set"
        $vsInstallPath = "C:\Apps\VS2025"
        if (Test-Path $vsInstallPath) {
            $vcToolsPath = Get-ChildItem -Path "$vsInstallPath\VC\Tools\MSVC" -Directory | Sort-Object Name -Descending | Select-Object -First 1
            if ($vcToolsPath) {
                Write-Output "💡 To fix symbol generation warnings, run this command as Administrator:"
                Write-Output "   [System.Environment]::SetEnvironmentVariable('VCToolsInstallDir', '$($vcToolsPath.FullName)\', 'Machine')"
                Write-Output "   Then restart your terminal/IDE for the change to take effect."
                Write-Output ""
                Write-Output "🔧 Using temporary environment setup for this build..."
                $env:VCToolsInstallDir = $vcToolsPath.FullName + "\"
                $env:VCINSTALLDIR = "$vsInstallPath\VC\"
            } else {
                Write-Output "❌ Visual Studio installation not found at $vsInstallPath"
                Write-Output "💡 Symbols package generation may fail due to missing mspdbcmf.exe"
            }
        } else {
            Write-Output "❌ Visual Studio installation not found at $vsInstallPath"
            Write-Output "💡 Install Visual Studio with C++ tools or set VCToolsInstallDir environment variable"
            Write-Output "   Example: [System.Environment]::SetEnvironmentVariable('VCToolsInstallDir', 'C:\\Program Files\\Microsoft Visual Studio\\2022\\Community\\VC\\Tools\\MSVC\\[version]\\', 'Machine')"
        }
    }
}

try {
    # Ensure .NET SDK is installed before any operations
    if (-not (Initialize-DotNetSdk)) {
        throw ".NET SDK installation failed or is not available"
    }

    # Clean if requested
    if ($Clean) {
        Write-Output "🧹 Cleaning project..."
        foreach ($proj in $CrossPlatformProjects) {
            $projPath = Join-Path $PSScriptRoot $proj
            if (Test-Path $projPath) {
                $cleanArgs = @($projPath, "-c", $Configuration, "--verbosity", $VerbosityLevel)
                Write-Command "dotnet clean" $cleanArgs
                dotnet clean @cleanArgs
            }
        }
    }

    # Build step
    if (!$NoBuild) {
        Write-Output "🔨 Building project..."

        foreach ($proj in $CrossPlatformProjects) {
            $projPath = Join-Path $PSScriptRoot $proj
            if (Test-Path $projPath) {
                $buildArgs = @($projPath, "-c", $Configuration, "--verbosity", $VerbosityLevel)
                Write-Command "dotnet build" $buildArgs
                dotnet build @buildArgs
                if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code: $LASTEXITCODE" }
            }
        }
        Write-Output "✅ Build completed successfully"

        # Run tests if not skipped
        if (!$NoTest) {
            Write-Output "🧪 Running tests..."
            # Prepare clean test results directory
            $ArtifactsDir = Join-Path $PSScriptRoot "artifacts"
            $TestResultsDir = Join-Path $ArtifactsDir "test-results"
            if (Test-Path $TestResultsDir) {
                try { Remove-Item -Path $TestResultsDir -Recurse -Force -ErrorAction Stop } catch { $null = $_ }
            }
            New-Item -ItemType Directory -Path $TestResultsDir -Force -ErrorAction SilentlyContinue | Out-Null

            # Discover test projects in Tests/ directory at project root
            $projectRoot = Split-Path $PSScriptRoot -Parent
            $testsDir = Join-Path $projectRoot "Tests"
            $testDirs = Get-ChildItem -Path $testsDir -Directory -ErrorAction SilentlyContinue |
                        Where-Object { $_.Name -ne "Data" }  # Exclude test data directory

            # Exclude Desktop tests (Desktop app has been deprecated)
            $testDirs = $testDirs | Where-Object { $_.Name -ne "Desktop" }

            $testProjects = @()
            foreach ($td in $testDirs) {
                $candidates = Get-ChildItem -Path $td.FullName -Filter *.csproj -File -ErrorAction SilentlyContinue
                if (-not $candidates) { continue }
                $preferred = $candidates | Where-Object { $_.BaseName -eq $td.Name }
                if (-not $preferred) { $preferred = $candidates | Select-Object -First 1 }
                $testProjects += $preferred.FullName
            }

            if (-not $testProjects -or $testProjects.Count -eq 0) {
                Write-Output "ℹ️  No test projects found. Skipping tests."
                $testExit = 0
            } else {
                $testExit = 0
                foreach ($proj in $testProjects) {
                    $testArgs = @(
                        $proj,
                        "-c", $Configuration,
                        "--verbosity", $VerbosityLevel,
                        "--logger", "trx",
                        "--logger", "console;verbosity=normal",
                        "--results-directory", $TestResultsDir
                    )
                    Write-Command "dotnet test" $testArgs
                    dotnet test @testArgs
                    if ($LASTEXITCODE -ne 0) { $testExit = $LASTEXITCODE }
                }
            }

            # Collect and summarize only new TRX files
            $trxFiles = Get-ChildItem -Path $TestResultsDir -Filter *.trx -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -ExpandProperty FullName
            $totalFailed = 0
            if ($trxFiles) {
                Write-Output ("📄 Test results saved to: {0}" -f ($trxFiles -join "; "))
                $totalFailed = Get-TrxSummary -Paths $trxFiles
            }

            if ($testExit -ne 0 -or ($totalFailed -gt 0)) {
                if ($testExit -ne 0) { Write-Error "dotnet test exited with code $testExit" }
                if ($totalFailed -gt 0) { Write-Error "$totalFailed test(s) failed per TRX summary" }
                throw "Tests failed. Skipping publish."
            }
            Write-Output "✅ All tests passed"
        }
    }

    Write-Output "`n🎉 Build process completed successfully!"
    Write-Output "ℹ️  Projects built: Shared, GameService, CLI"

} catch {
    Write-Error "❌ Build process failed: $($_.Exception.Message)"
    Close-Log
    exit 1
}

# Ensure transcript is stopped on successful completion
Close-Log

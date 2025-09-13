#!/usr/bin/env pwsh
param(
    [Parameter(Position=0)]
    [string]$Arg0,
    [switch]$Clean,
    [switch]$NoBuild,
    [switch]$NoTest,
    [switch]$SkipUiTests,
    [switch]$NoUiTests,
    [switch]$IncludeUiTests,
    [switch]$Release,
    [switch]$NoRegister,
    [switch]$NoFontRegister,
    [switch]$Unregister,
    [switch]$Help,
    [ValidateSet("x64", "x86", "ARM64")]
    [string]$Platform = "x64"
)

# Custom error handling for parameter validation
trap {
    if ($_.Exception.Message -like "*cannot validate argument*" -and $_.Exception.Message -like "*--*") {
        Write-Error "❌ Invalid parameter format detected!"
        Show-Help
        Stop-Log
        exit 1
    }
    Write-Error "❌ Parameter error: $($_.Exception.Message)"
    Show-Help
    Stop-Log
    exit 1
}

# Function to show help
function Show-Help {
    Write-Output @"
Catan Desktop App Build Script

USAGE:
    .\build.ps1 [OPTIONS]

OPTIONS:
    -Clean          Clean the project before building
    -NoBuild        Skip the build step, only publish
    -NoTest         Skip running tests
    -SkipUiTests    Skip UI/E2E test projects (e.g., Tests.DesktopApp.UI) - DEPRECATED, use -NoUiTests
    -NoUiTests      Skip UI/E2E test projects (alias for -SkipUiTests) - DEPRECATED, UI tests now skipped by default
    -IncludeUiTests Include UI/E2E test projects (requires recorded test files)
    -Release        Build in Release configuration (default: Debug)
    -NoRegister     Do not register the app after publish (default is to register)
    -NoFontRegister Do not register the Catan font (default is to register)
    -Unregister     Unregister the app from the system and exit
    -Platform       Target platform: x64, x86, ARM64 (default: x64)
    -Verbose        Enable verbose output (built-in PowerShell)
    -Help           Show this help message

EXAMPLES:
    .\build.ps1                           # Build, test, publish, and register (default)
    .\build.ps1 -Clean -Release           # Clean release build (registers by default)
    .\build.ps1 -NoTest -NoRegister       # Build and publish without tests and skip registration
    .\build.ps1 -IncludeUiTests           # Build, run ALL tests including UI tests, publish, register
    .\build.ps1 -SkipUiTests              # DEPRECATED - Build, run unit/integration tests only
    .\build.ps1 -NoUiTests                # DEPRECATED - Same as -SkipUiTests
    .\build.ps1 -NoFontRegister           # Build normally but skip font registration
    .\build.ps1 -Unregister               # Unregister the app and exit (implies -NoRegister)
    .\build.ps1 -Platform ARM64 -Release  # Build for ARM64 in Release mode

DEFAULTS:
    By default, this script will build, test (excluding UI tests), publish, register the app, and register
    the Catan font so you can find it in the Start menu and launch/debug it immediately. 
    UI tests are skipped by default as they require recorded test files - use -IncludeUiTests to run them.
    Use -NoRegister to skip app registration or -NoFontRegister to skip font registration.

NOTE: Use PowerShell syntax (-Parameter) not bash syntax (--parameter)

COMMON ERRORS:
    --help     →  -Help
    --clean    →  -Clean
    --release  →  -Release
    --no-test  →  -NoTest
    --no-build →  -NoBuild
    --no-register → -NoRegister
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
function Stop-Log {
    if ($script:TranscriptStarted) {
        try { Stop-Transcript | Out-Null } catch { }
        $script:TranscriptStarted = $false
    }
}

# Font registration helper
function Register-Font {
    param(
        [Parameter(Mandatory=$true)][string]$FontPath
    )
    
    if (-not (Test-Path $FontPath)) {
        Write-Output "⚠️  Font file not found: $FontPath"
        return $false
    }
    
    try {
        Write-Output "🎨 Registering Catan font..."
        
        # Load the font file
        $fontFile = Get-Item $FontPath
        
        # Add to Windows font registry using Win32 API
        Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class FontRegistration {
    [DllImport("gdi32.dll", EntryPoint="AddFontResourceW", SetLastError=true)]
    public static extern int AddFontResource([MarshalAs(UnmanagedType.LPWStr)]string lpFileName);
    
    [DllImport("user32.dll", SetLastError=true)]
    public static extern int SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    
    public const int HWND_BROADCAST = 0xFFFF;
    public const uint WM_FONTCHANGE = 0x001D;
}
"@
        
        # Register the font
        $result = [FontRegistration]::AddFontResource($FontPath)
        
        if ($result -gt 0) {
            # Notify all windows that fonts have changed
            [FontRegistration]::SendMessage([IntPtr]0xFFFF, 0x001D, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
            
            Write-Output "✅ Font registered successfully: $($fontFile.Name)"
            Write-Output "   Font families added: $result"
            return $true
        } else {
            Write-Output "❌ Font registration failed for: $($fontFile.Name)"
            return $false
        }
    } catch {
        Write-Output "❌ Error registering font: $($_.Exception.Message)"
        return $false
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
        Stop-Log
        exit 0
    }
    if ($Arg0 -like "--*") {
        Write-Error "❌ Invalid parameter format detected!"
        Show-Help
        Stop-Log
        exit 1
    }
}

# Show help if requested
if ($Help) {
    Show-Help
    Stop-Log
    exit 0
}

# Configuration
$NoRegister = $NoRegister -or $Unregister
$Configuration = if ($Release) { "Release" } else { "Debug" }
$ProjectPath = "Catan.sln"
$RuntimeId = "win-$Platform"
$OutputPath = "DesktopApp\bin\$Platform\$Configuration\net9.0-windows10.0.22621.0\$RuntimeId"
$VerbosityLevel = if ($VerbosePreference -eq 'Continue') { "normal" } else { "minimal" }
$PackageId = "606d7833-a1be-4389-aa5f-fe8dd1dd1da3"

Write-Output "🏗️  Catan Desktop App Build Script"
Write-Output "Configuration: $Configuration | Platform: $Platform | Runtime: $RuntimeId"

# Check Visual Studio environment for symbol generation
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

try {
    # Unregister app if requested (and exit immediately)
    if ($Unregister) {
        Write-Output "🗑️  Unregistering app..."
        $existingApp = Get-AppxPackage | Where-Object {$_.PackageFullName -like "*$PackageId*"}
        if ($existingApp) {
            Write-Output "Found $($existingApp.Count) installed instance(s) to remove"
            $existingApp | ForEach-Object {
                Write-Command "Remove-AppxPackage" @($_.PackageFullName)
            }
            $existingApp | Remove-AppxPackage
            Write-Output "✅ App unregistered successfully"
        } else {
            Write-Output "ℹ️  App not currently registered"
        }
        Stop-Log
        exit 0
    }

    # Clean if requested
    if ($Clean) {
    Write-Output "🧹 Cleaning project..."
        $cleanArgs = @(
            $ProjectPath,
            "-c", $Configuration,
            "-p:Platform=$Platform",
            "--verbosity", $VerbosityLevel
        )
    Write-Command "dotnet clean" $cleanArgs
    dotnet clean @cleanArgs
    if ($LASTEXITCODE -ne 0) { throw "Clean failed with exit code: $LASTEXITCODE" }
    }

    # Build step
    if (!$NoBuild) {
    Write-Output "🔨 Building project..."
        $buildArgs = @(
            $ProjectPath,
            "-c", $Configuration,
            "-p:Platform=$Platform",
            "-p:GenerateAppxPackageOnBuild=true",
            "--verbosity", $VerbosityLevel
        )
    Write-Command "dotnet build" $buildArgs
    dotnet build @buildArgs
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code: $LASTEXITCODE" }
    Write-Output "✅ Build completed successfully"

    # Register the Catan font for UI consistency (unless skipped)
    if (-not $NoFontRegister) {
        $fontPath = Join-Path $PSScriptRoot "DesktopApp\Assets\Fonts\Catan.ttf"
        Register-Font -FontPath $fontPath
    } else {
        Write-Output "⏭️  Font registration skipped (flag: -NoFontRegister)"
    }

        # Run tests if not skipped
        if (!$NoTest) {
            Write-Output "🧪 Running tests..."
            # Prepare clean test results directory
            $ArtifactsDir = Join-Path $PSScriptRoot "artifacts"
            $TestResultsDir = Join-Path $ArtifactsDir "test-results"
            if (Test-Path $TestResultsDir) {
                try { Remove-Item -Path $TestResultsDir -Recurse -Force -ErrorAction Stop } catch { }
            }
            New-Item -ItemType Directory -Path $TestResultsDir -Force -ErrorAction SilentlyContinue | Out-Null

            # Discover test projects at repo root matching 'Tests.*' (top-level only)
            $testDirs = Get-ChildItem -Path $PSScriptRoot -Directory -Filter 'Tests.*' -ErrorAction SilentlyContinue
            
            # Skip UI tests by default unless -IncludeUiTests is explicitly specified
            if (-not $IncludeUiTests) {
                $testDirs = $testDirs | Where-Object { $_.Name -notlike 'Tests.DesktopApp.UI*' }
                if ($SkipUiTests -or $NoUiTests) {
                    $flagUsed = if ($NoUiTests) { "-NoUiTests" } else { "-SkipUiTests" }
                    Write-Output "⏭️  Skipping UI test projects (flag: $flagUsed) - DEPRECATED: UI tests now skipped by default"
                } else {
                    Write-Output "⏭️  Skipping UI test projects (default behavior - use -IncludeUiTests to run them)"
                }
            } else {
                Write-Output "🎭 Including UI test projects (flag: -IncludeUiTests)"
            }

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

    # MSIX Package Installation step
    Write-Output "📦 Installing MSIX application..."
    
    # Find the generated MSIX package
    $appPackagesPath = Join-Path (Split-Path $OutputPath) "AppPackages"
    $packageDirs = Get-ChildItem -Path $appPackagesPath -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "*Debug_Test" }
    
    if ($packageDirs.Count -eq 0) {
        throw "No MSIX package directory found in $appPackagesPath"
    }
    
    $packageDir = $packageDirs[0].FullName
    $addAppScript = Join-Path $packageDir "Add-AppDevPackage.ps1"
    $msixFile = Get-ChildItem -Path $packageDir -Filter "*.msix" | Select-Object -First 1
    
    if (-not (Test-Path $addAppScript)) {
        throw "Add-AppDevPackage.ps1 not found in $packageDir"
    }
    
    if (-not $msixFile) {
        throw "No MSIX file found in $packageDir"
    }
    
    # Remove any existing version first
    $existingApp = Get-AppxPackage | Where-Object { $_.Name -eq $PackageId }
    if ($existingApp) {
        Write-Output "🔄 Unregistering previous version..."
        $existingApp | ForEach-Object { 
            Write-Command "Remove-AppxPackage" @($_.PackageFullName)
            Remove-AppxPackage -Package $_.PackageFullName -ErrorAction SilentlyContinue
        }
    }
    
    Write-Output "📦 Installing MSIX package: $($msixFile.Name)"
    Write-Command "Add-AppxPackage" @("-Path", $msixFile.FullName)
    
    try {
        # Try direct installation first
        Add-AppxPackage -Path $msixFile.FullName -ErrorAction Stop
        Write-Output "✅ MSIX package installed successfully"
    } catch {
        Write-Output "⚠️  Direct installation failed: $($_.Exception.Message)"
        Write-Output "🔄 Trying developer package script..."
        
        # Fallback to the developer script
        $originalLocation = Get-Location
        try {
            Set-Location $packageDir
            # Run with -Force to skip interactive prompts
            $process = Start-Process -FilePath "powershell.exe" -ArgumentList @("-ExecutionPolicy", "Bypass", "-File", "Add-AppDevPackage.ps1", "-Force") -Wait -PassThru -WindowStyle Hidden
            if ($process.ExitCode -ne 0) {
                throw "Developer package script failed with exit code: $($process.ExitCode)"
            }
            Write-Output "✅ MSIX package installed via developer script"
        } finally {
            Set-Location $originalLocation
        }
    }

    # Verify MSIX package
    Write-Output "🔍 Verifying MSIX package..."
    $appPackagesPath = Join-Path (Split-Path $OutputPath) "AppPackages"
    $packageDirs = Get-ChildItem -Path $appPackagesPath -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "*Debug_Test" }
    
    if ($packageDirs.Count -gt 0) {
        $packageDir = $packageDirs[0].FullName
        $msixFiles = Get-ChildItem -Path $packageDir -Filter "*.msix"
        
        if ($msixFiles.Count -gt 0) {
            $msixFile = $msixFiles[0]
            
            Write-Output "`n📋 MSIX Package Information:"
            Write-Output "┌─────────────────────────────────────────────────────────────────┐"
            Write-Output "│ Property              │ Value                                   │"
            Write-Output "├─────────────────────────────────────────────────────────────────┤"
            Write-Output "│ Package Path          │ $($msixFile.FullName.Substring(0, [Math]::Min(39, $msixFile.FullName.Length)).PadRight(39)) │"
            Write-Output "│ Package Size          │ $([Math]::Round($msixFile.Length / 1MB, 2).ToString().PadLeft(6)) MB".PadRight(39) + " │"
            Write-Output "│ Last Modified         │ $($msixFile.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss').PadRight(39)) │"
            Write-Output "│ Configuration         │ $($Configuration.PadRight(39)) │"
            Write-Output "│ Target Platform       │ $($Platform.PadRight(39)) │"
            Write-Output "│ Runtime Identifier    │ $($RuntimeId.PadRight(39)) │"
            Write-Output "└─────────────────────────────────────────────────────────────────┘"
        } else {
            Write-Output "⚠️  Warning: No MSIX package found in $packageDir"
        }
    } else {
        Write-Output "⚠️  Warning: No MSIX package directory found"
    }

    # Register app by default unless -NoRegister specified
    if (-not $NoRegister) {
        Write-Output "`n📱 Verifying app registration..."
        
        # Verify app installation
        Write-Output "🔍 Checking app installation..."
        $installedApp = Get-AppxPackage | Where-Object { $_.Name -eq $PackageId }
        
        if ($installedApp) {
            Write-Output "📋 Installed App Info:"
            Write-Output "   Name: $($installedApp.Name)"
            Write-Output "   Version: $($installedApp.Version)"
            Write-Output "   Status: $($installedApp.Status)"
            Write-Output "   Install Location: $($installedApp.InstallLocation)"
            Write-Output "✅ App successfully installed and registered in Start menu"
            $installationSuccessful = $true
        } else {
            Write-Output "❌ App installation verification failed - app not found in installed packages"
            $installationSuccessful = $false
        }
    } else {
        $installationSuccessful = $false
    }

    Write-Output "`n🎉 Build process completed successfully!"
    
    # Find the MSIX package location for final message
    $appPackagesPath = Join-Path (Split-Path $OutputPath) "AppPackages"
    Write-Output "📦 MSIX package location: $appPackagesPath"
    
    if (-not $NoRegister) {
        if ($installationSuccessful) {
            Write-Output "🚀 App is now available in Start menu"
        } else {
            Write-Output "⚠️  App build succeeded but installation failed - check the logs above"
            Write-Output "💡 You can try manual installation from: $appPackagesPath"
        }
    } else {
        Write-Output "💡 Registration skipped (remove -NoRegister to install the app in Start menu)"
    }

} catch {
    Write-Error "❌ Build process failed: $($_.Exception.Message)"
    Stop-Log
    exit 1
}

# Ensure transcript is stopped on successful completion
Stop-Log

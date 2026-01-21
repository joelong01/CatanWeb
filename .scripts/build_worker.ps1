#!/usr/bin/env pwsh
param(
    [Parameter(Position=0)]
    [string]$Arg0,
    [switch]$Clean,
    [switch]$NoBuild,
    [switch]$NoTest,
    [switch]$IncludeUiTests,
    [switch]$Release,
    [switch]$NoRegister,
    [switch]$NoFontRegister,
    [switch]$NoDesktop,
    [switch]$Unregister,
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
    -NoDesktop      Skip building the Desktop app (faster builds for web development)
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
    .\build.ps1                           # Build everything (default)
    .\build.ps1 -NoDesktop                # Build without Desktop app (fast for web dev)
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
function Close-Log {
    if ($script:TranscriptStarted) {
        try { Stop-Transcript | Out-Null } catch { $null = $_ }
        $script:TranscriptStarted = $false
    }
}

# Certificate helper for MSIX signing (Windows only)
function Initialize-MsixCertificate {
    # Suppress plaintext SecureString warning - password is randomly generated for dev certificates
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingConvertToSecureStringWithPlainText', '')]
    param(
        [Parameter(Mandatory=$true)][string]$ProjectDir,
        [Parameter(Mandatory=$true)][string]$PfxFileName
    )

    $pfxPath = Join-Path $ProjectDir $PfxFileName
    $certSubject = "CN=CatanDesktopDev"

    # Check if pfx already exists and is valid
    if (Test-Path $pfxPath) {
        # Read password from csproj
        $csprojPath = Get-ChildItem -Path $ProjectDir -Filter "*.csproj" | Select-Object -First 1
        if ($csprojPath) {
            $content = Get-Content $csprojPath.FullName -Raw
            if ($content -match '<PackageCertificatePassword>([^<]+)</PackageCertificatePassword>') {
                $certPassword = $Matches[1]
                try {
                    # Load certificate to validate it works with stored password
                    $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($pfxPath, $certPassword, "Exportable,PersistKeySet")
                    if ($cert) {
                        Write-Output "✅ MSIX certificate found: $PfxFileName"
                        return $true
                    }
                } catch {
                    Write-Output "⚠️  Existing certificate invalid, recreating..."
                }
            }
        }
        # If we get here, PFX exists but is invalid or password missing - delete and recreate
        Remove-Item -Path $pfxPath -Force -ErrorAction SilentlyContinue
    }

    # Generate random 6-digit password for this certificate
    $certPassword = Get-Random -Minimum 100000 -Maximum 999999

    Write-Output "🔐 Creating self-signed certificate for MSIX signing..."

    try {
        # Create a self-signed certificate for code signing
        $cert = New-SelfSignedCertificate `
            -Type Custom `
            -Subject $certSubject `
            -KeyUsage DigitalSignature `
            -FriendlyName "Catan Desktop Development Certificate" `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}") `
            -NotAfter (Get-Date).AddYears(5)

        if (-not $cert) {
            throw "Failed to create certificate"
        }

        Write-Output "   Certificate created with thumbprint: $($cert.Thumbprint)"

        # Export to PFX with random password (empty strings not allowed in some PS versions)
        $securePassword = ConvertTo-SecureString -String $certPassword -Force -AsPlainText
        Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $securePassword | Out-Null

        Write-Output "   Exported to: $pfxPath"

        # Update the project file with the new thumbprint and password
        $csprojPath = Get-ChildItem -Path $ProjectDir -Filter "*.csproj" | Select-Object -First 1
        if ($csprojPath) {
            $content = Get-Content $csprojPath.FullName -Raw
            # Replace the thumbprint
            $content = $content -replace '<PackageCertificateThumbprint>[^<]+</PackageCertificateThumbprint>', "<PackageCertificateThumbprint>$($cert.Thumbprint)</PackageCertificateThumbprint>"
            # Replace or add the password
            if ($content -match '<PackageCertificatePassword>[^<]+</PackageCertificatePassword>') {
                $content = $content -replace '<PackageCertificatePassword>[^<]+</PackageCertificatePassword>', "<PackageCertificatePassword>$certPassword</PackageCertificatePassword>"
            } else {
                # Add password after thumbprint
                $content = $content -replace '(<PackageCertificateThumbprint>[^<]+</PackageCertificateThumbprint>)', "`$1`n        <PackageCertificatePassword>$certPassword</PackageCertificatePassword>"
            }
            Set-Content -Path $csprojPath.FullName -Value $content -NoNewline
            Write-Output "   Updated thumbprint and password in: $($csprojPath.Name)"
        }

        # Add to TrustedPeople store for local deployment using certutil (works in CI without UI)
        # Export to CER format first (public key only)
        $cerPath = [System.IO.Path]::ChangeExtension($pfxPath, ".cer")
        Export-Certificate -Cert $cert -FilePath $cerPath -Force | Out-Null

        # Use certutil to add to TrustedPeople store (no UI prompt)
        & certutil -user -addstore TrustedPeople $cerPath 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Output "   Added to TrustedPeople store via certutil"
        } else {
            # Fallback to .NET method
            $trustedPeopleStore = New-Object System.Security.Cryptography.X509Certificates.X509Store("TrustedPeople", "CurrentUser")
            $trustedPeopleStore.Open("ReadWrite")
            $trustedPeopleStore.Add($cert)
            $trustedPeopleStore.Close()
            Write-Output "   Added to TrustedPeople store via .NET"
        }

        # Clean up temp CER file
        Remove-Item -Path $cerPath -Force -ErrorAction SilentlyContinue

        Write-Output "✅ MSIX certificate created and configured"
        return $true

    } catch {
        Write-Output "⚠️  Certificate creation failed: $($_.Exception.Message)"
        Write-Output "   Build will continue but MSIX signing may show warnings"
        return $false
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
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public const int HWND_BROADCAST = 0xFFFF;
    public const uint WM_FONTCHANGE = 0x001D;
}
"@

        # Register the font
        $result = [FontRegistration]::AddFontResource($FontPath)

        if ($result -gt 0) {
            # Notify all windows that fonts have changed (PostMessage is async, won't block)
            [FontRegistration]::PostMessage([IntPtr]0xFFFF, 0x001D, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null

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
$NoRegister = $NoRegister -or $Unregister
$Configuration = if ($Release) { "Release" } else { "Debug" }
$ProjectPath = "Catan.sln"
$RuntimeId = "win-$Platform"
$OutputPath = "DesktopApp\bin\$Platform\$Configuration\net9.0-windows10.0.22621.0\$RuntimeId"
$VerbosityLevel = if ($VerbosePreference -eq 'Continue') { "normal" } else { "minimal" }
$PackageId = "606d7833-a1be-4389-aa5f-fe8dd1dd1da3"

# Cross-platform project list (excludes Windows-only DesktopApp)
$CrossPlatformProjects = @(
    "Catan3.Shared/Catan3.Shared.csproj",
    "Catan3.GameService/Catan3.GameService.csproj",
    "WebUI/Catan3.WebUI.csproj",
    "Catan3.CLI/Catan3.CLI.csproj"
)

if ($IsWindows) {
    Write-Output "🏗️  Catan Desktop App Build Script"
    Write-Output "Configuration: $Configuration | Platform: $Platform | Runtime: $RuntimeId"
} else {
    Write-Output "🏗️  Catan Build Script (Cross-Platform Mode)"
    Write-Output "Configuration: $Configuration | Platform: $(if ($IsMacOS) { 'macOS' } else { 'Linux' })"
    Write-Output "ℹ️  Desktop app build skipped (Windows only)"
}

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

    # Unregister app if requested (and exit immediately) - Windows only
    if ($Unregister) {
        if (-not $IsWindows) {
            Write-Output "ℹ️  App unregistration is only available on Windows"
            Close-Log
            exit 0
        }
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
        Close-Log
        exit 0
    }

    # Clean if requested
    if ($Clean) {
        Write-Output "🧹 Cleaning project..."
        if ($IsWindows) {
            $cleanArgs = @(
                $ProjectPath,
                "-c", $Configuration,
                "-p:Platform=$Platform",
                "--verbosity", $VerbosityLevel
            )
            Write-Command "dotnet clean" $cleanArgs
            dotnet clean @cleanArgs
            if ($LASTEXITCODE -ne 0) { throw "Clean failed with exit code: $LASTEXITCODE" }
        } else {
            # Clean individual cross-platform projects
            foreach ($proj in $CrossPlatformProjects) {
                $projPath = Join-Path $PSScriptRoot $proj
                if (Test-Path $projPath) {
                    $cleanArgs = @($projPath, "-c", $Configuration, "--verbosity", $VerbosityLevel)
                    Write-Command "dotnet clean" $cleanArgs
                    dotnet clean @cleanArgs
                }
            }
        }
    }

    # Ensure MSIX certificate exists (Windows only, and only if building Desktop app)
    if ($IsWindows -and -not $NoDesktop) {
        $desktopAppDir = Join-Path (Split-Path $PSScriptRoot -Parent) "DesktopApp"
        Initialize-MsixCertificate -ProjectDir $desktopAppDir -PfxFileName "Catan Desktop_TemporaryKey.pfx"
    }

    # Build step
    if (!$NoBuild) {
        Write-Output "🔨 Building project..."

        if ($IsWindows -and -not $NoDesktop) {
            # Full solution build including Desktop app
            $buildArgs = @(
                $ProjectPath,
                "-c", $Configuration,
                "-p:Platform=$Platform",
                "-p:GenerateAppxPackageOnBuild=true",
                "-p:SuppressNETCoreSdkPreviewMessage=true",
                "--verbosity", $VerbosityLevel
            )
            Write-Command "dotnet build" $buildArgs
            dotnet build @buildArgs
            if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code: $LASTEXITCODE" }
        } else {
            # Build individual cross-platform projects (skip Desktop app)
            if ($NoDesktop -and $IsWindows) {
                Write-Output "⏭️  Skipping Desktop app build (flag: -NoDesktop)"
            }
            foreach ($proj in $CrossPlatformProjects) {
                $projPath = Join-Path $PSScriptRoot $proj
                if (Test-Path $projPath) {
                    $buildArgs = @($projPath, "-c", $Configuration, "--verbosity", $VerbosityLevel)
                    Write-Command "dotnet build" $buildArgs
                    dotnet build @buildArgs
                    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code: $LASTEXITCODE" }
                }
            }
        }
        Write-Output "✅ Build completed successfully"

        # Register the Catan font for UI consistency (Windows only, unless skipped)
        if ($IsWindows -and -not $NoFontRegister -and -not $NoDesktop) {
            $projectRoot = Split-Path $PSScriptRoot -Parent
            $fontPath = Join-Path $projectRoot "DesktopApp\Assets\Fonts\Catan.ttf"
            Register-Font -FontPath $fontPath
        } elseif ($IsWindows -and $NoDesktop) {
            Write-Output "⏭️  Font registration skipped (Desktop app not built)"
        } elseif ($IsWindows) {
            Write-Output "⏭️  Font registration skipped (flag: -NoFontRegister)"
        }

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

            # Always exclude Desktop tests on non-Windows (can't build Windows-only projects)
            if (-not $IsWindows) {
                $testDirs = $testDirs | Where-Object { $_.Name -ne "Desktop" }
                Write-Output "⏭️  Skipping Desktop tests (not available on this platform)"
            }
            # On Windows, skip UI tests by default unless -IncludeUiTests is explicitly specified
            elseif (-not $IncludeUiTests) {
                $testDirs = $testDirs | Where-Object { $_.Name -ne "Desktop" }
                Write-Output "⏭️  Skipping Desktop UI tests (default behavior - use -IncludeUiTests to run them)"
            } else {
                Write-Output "🎭 Including Desktop UI tests (flag: -IncludeUiTests)"
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

    # MSIX Package Installation step (Windows only, skip if -NoDesktop)
    if ($IsWindows -and -not $NoDesktop) {
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
            $errorMsg = $_.Exception.Message
            Write-Output "⚠️  Direct installation failed: $errorMsg"

            # Check if this is a certificate trust issue
            if ($errorMsg -match "0x800B0109|root certificate|not trusted") {
                Write-Output ""
                Write-Output "💡 Certificate trust issue detected. For local development:"
                Write-Output "   1. Double-click the .cer file in the package folder"
                Write-Output "   2. Click 'Install Certificate' → 'Local Machine' → 'Trusted Root'"
                Write-Output "   Or run Add-AppDevPackage.ps1 manually which will prompt for trust"
                Write-Output ""
                Write-Output "   For CI: The MSIX package was built successfully and can be distributed."
                Write-Output "   Package location: $($msixFile.FullName)"
                # Don't fail the build for cert trust issues - the package is built
            } else {
                Write-Output "🔄 Trying developer package script..."

                # Fallback to the developer script
                $originalLocation = Get-Location
                try {
                    Set-Location $packageDir
                    # Run with -Force to skip interactive prompts
                    $process = Start-Process -FilePath "powershell.exe" -ArgumentList @("-ExecutionPolicy", "Bypass", "-File", "Add-AppDevPackage.ps1", "-Force") -Wait -PassThru -WindowStyle Hidden
                    if ($process.ExitCode -ne 0) {
                        Write-Output "⚠️  Developer package script failed with exit code: $($process.ExitCode)"
                    } else {
                        Write-Output "✅ MSIX package installed via developer script"
                    }
                } finally {
                    Set-Location $originalLocation
                }
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
                Write-Output "│ Package Size          │ $("$([Math]::Round($msixFile.Length / 1MB, 2).ToString().PadLeft(6)) MB".PadRight(39)) │"
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
    } elseif ($IsWindows -and $NoDesktop) {
        # Windows with -NoDesktop: skip MSIX, just report success
        Write-Output "`n🎉 Build process completed successfully!"
        Write-Output "ℹ️  Projects built: Shared, GameService, WebUI, CLI (Desktop skipped)"
    } else {
        # Non-Windows: just report success
        Write-Output "`n🎉 Build process completed successfully!"
        Write-Output "ℹ️  Cross-platform projects built: Shared, GameService, WebUI, CLI"
    }

} catch {
    Write-Error "❌ Build process failed: $($_.Exception.Message)"
    Close-Log
    exit 1
}

# Ensure transcript is stopped on successful completion
Close-Log

# Setup Node.js, npm, and Mermaid CLI for Catan3 Companion
# This script will install everything needed to generate diagram images

Write-Host "?? Setting up Node.js, npm, and Mermaid CLI for Catan3 Companion..." -ForegroundColor Cyan
Write-Host ""

# Function to check if a command exists
function Test-Command($cmdname) {
    return [bool](Get-Command -Name $cmdname -ErrorAction SilentlyContinue)
}

# Function to add to PATH if not already present
function Add-ToPath($Path) {
    $currentPath = [Environment]::GetEnvironmentVariable("PATH", "User")
    if ($currentPath -notlike "*$Path*") {
        $newPath = $currentPath + ";" + $Path
        [Environment]::SetEnvironmentVariable("PATH", $newPath, "User")
        Write-Host "? Added to PATH: $Path" -ForegroundColor Green
        return $true
    } else {
        Write-Host "??  Already in PATH: $Path" -ForegroundColor Yellow
        return $false
    }
}

# Function to refresh environment variables in current session
function Update-SessionEnvironment {
    $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH","User")
}

# Step 1: Check if Node.js is already installed
Write-Host "?? Checking for existing Node.js installation..." -ForegroundColor Cyan

if (Test-Command "node") {
    $nodeVersion = node --version 2>$null
    Write-Host "? Node.js is already installed: $nodeVersion" -ForegroundColor Green
} else {
    Write-Host "? Node.js not found. Installing..." -ForegroundColor Yellow
    
    # Check if we have winget (Windows Package Manager)
    if (Test-Command "winget") {
        Write-Host "?? Installing Node.js via winget..." -ForegroundColor Cyan
        try {
            winget install OpenJS.NodeJS --accept-package-agreements --accept-source-agreements
            Write-Host "? Node.js installed successfully via winget!" -ForegroundColor Green
        } catch {
            Write-Host "? Winget installation failed. Please install manually." -ForegroundColor Red
            Write-Host "   Visit: https://nodejs.org/en/download/" -ForegroundColor Yellow
            Write-Host "   Download and install the LTS version for Windows" -ForegroundColor Yellow
            exit 1
        }
    } elseif (Test-Command "choco") {
        Write-Host "?? Installing Node.js via Chocolatey..." -ForegroundColor Cyan
        try {
            choco install nodejs -y
            Write-Host "? Node.js installed successfully via Chocolatey!" -ForegroundColor Green
        } catch {
            Write-Host "? Chocolatey installation failed. Please install manually." -ForegroundColor Red
            Write-Host "   Visit: https://nodejs.org/en/download/" -ForegroundColor Yellow
            exit 1
        }
    } else {
        Write-Host "? No package manager found (winget/choco). Manual installation required:" -ForegroundColor Red
        Write-Host "   1. Visit: https://nodejs.org/en/download/" -ForegroundColor Yellow
        Write-Host "   2. Download the LTS version for Windows (includes npm)" -ForegroundColor Yellow
        Write-Host "   3. Run the installer with default settings" -ForegroundColor Yellow
        Write-Host "   4. Restart your terminal/PowerShell" -ForegroundColor Yellow
        Write-Host "   5. Run this script again" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "?? Alternative: Use Windows Package Manager (winget):" -ForegroundColor Cyan
        Write-Host "   winget install OpenJS.NodeJS" -ForegroundColor Gray
        exit 1
    }
    
    # Refresh environment to pick up new installation
    Update-SessionEnvironment
    Start-Sleep 2
}

# Step 2: Verify Node.js installation
Write-Host ""
Write-Host "?? Verifying Node.js installation..." -ForegroundColor Cyan
Update-SessionEnvironment

try {
    $nodeVersion = node --version 2>$null
    $npmVersion = npm --version 2>$null
    
    if ($nodeVersion -and $npmVersion) {
        Write-Host "? Node.js: $nodeVersion" -ForegroundColor Green
        Write-Host "? npm: $npmVersion" -ForegroundColor Green
    } else {
        Write-Host "? Node.js/npm verification failed. Please restart your terminal and try again." -ForegroundColor Red
        Write-Host "   If the problem persists, manually add Node.js to your PATH:" -ForegroundColor Yellow
        Write-Host "   - Default location: C:\Program Files\nodejs\" -ForegroundColor Gray
        exit 1
    }
} catch {
    Write-Host "? Node.js/npm verification failed. Please restart your terminal and try again." -ForegroundColor Red
    exit 1
}

# Step 3: Install Mermaid CLI
Write-Host ""
Write-Host "?? Installing Mermaid CLI..." -ForegroundColor Cyan

try {
    # Check if already installed
    $mmdcVersion = mmdc --version 2>$null
    if ($mmdcVersion) {
        Write-Host "? Mermaid CLI already installed: $mmdcVersion" -ForegroundColor Green
    } else {
        Write-Host "?? Installing @mermaid-js/mermaid-cli globally..." -ForegroundColor Cyan
        npm install -g @mermaid-js/mermaid-cli
        
        # Verify installation
        Start-Sleep 2
        Update-SessionEnvironment
        $mmdcVersion = mmdc --version 2>$null
        if ($mmdcVersion) {
            Write-Host "? Mermaid CLI installed successfully: $mmdcVersion" -ForegroundColor Green
        } else {
            Write-Host "??  Mermaid CLI installed but not immediately available. Try restarting your terminal." -ForegroundColor Yellow
        }
    }
} catch {
    Write-Host "? Failed to install Mermaid CLI. Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   Try manually: npm install -g @mermaid-js/mermaid-cli" -ForegroundColor Yellow
    exit 1
}

# Step 4: Test the complete setup
Write-Host ""
Write-Host "?? Testing complete setup..." -ForegroundColor Cyan

$testsPassed = 0
$totalTests = 3

# Test Node.js
try {
    $nodeVersion = node --version 2>$null
    if ($nodeVersion) {
        Write-Host "? Node.js test passed: $nodeVersion" -ForegroundColor Green
        $testsPassed++
    } else {
        Write-Host "? Node.js test failed" -ForegroundColor Red
    }
} catch {
    Write-Host "? Node.js test failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test npm
try {
    $npmVersion = npm --version 2>$null
    if ($npmVersion) {
        Write-Host "? npm test passed: $npmVersion" -ForegroundColor Green
        $testsPassed++
    } else {
        Write-Host "? npm test failed" -ForegroundColor Red
    }
} catch {
    Write-Host "? npm test failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test Mermaid CLI
try {
    $mmdcVersion = mmdc --version 2>$null
    if ($mmdcVersion) {
        Write-Host "? Mermaid CLI test passed: $mmdcVersion" -ForegroundColor Green
        $testsPassed++
    } else {
        Write-Host "? Mermaid CLI test failed" -ForegroundColor Red
    }
} catch {
    Write-Host "? Mermaid CLI test failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Summary
Write-Host ""
Write-Host "?? Setup Summary:" -ForegroundColor Cyan
Write-Host "   Tests passed: $testsPassed/$totalTests" -ForegroundColor $(if($testsPassed -eq $totalTests) { "Green" } else { "Yellow" })

if ($testsPassed -eq $totalTests) {
    Write-Host ""
    Write-Host "?? Setup completed successfully!" -ForegroundColor Green
    Write-Host "   You can now generate Mermaid diagrams!" -ForegroundColor Green
    Write-Host ""
    Write-Host "?? Next steps:" -ForegroundColor Cyan
    Write-Host "   1. Run: .\generate-diagrams.ps1" -ForegroundColor Gray
    Write-Host "   2. Or run: .\generate-diagrams.bat" -ForegroundColor Gray
    Write-Host ""
    Write-Host "?? Diagrams will be available at:" -ForegroundColor Cyan
    Write-Host "   http://localhost:8080/diagrams/picking-game-flow.svg" -ForegroundColor Gray
    Write-Host "   http://localhost:8080/diagrams/joining-game-flow.svg" -ForegroundColor Gray
    Write-Host "   http://localhost:8080/diagrams/updating-game-flow.svg" -ForegroundColor Gray
} else {
    Write-Host ""
    Write-Host "??  Setup incomplete. Some components failed to install." -ForegroundColor Yellow
    Write-Host "   Please restart your terminal and try again." -ForegroundColor Yellow
    Write-Host "   If problems persist, install Node.js manually from:" -ForegroundColor Yellow
    Write-Host "   https://nodejs.org/en/download/" -ForegroundColor Gray
}

Write-Host ""
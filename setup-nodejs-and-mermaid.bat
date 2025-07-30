@echo off
echo ?? Setting up Node.js, npm, and Mermaid CLI for Catan3 Companion...
echo.

REM Check if Node.js is installed
node --version >nul 2>&1
if errorlevel 1 (
    echo ? Node.js not found. Installing via winget...
    echo.
    
    REM Try to install with winget
    winget install OpenJS.NodeJS --accept-package-agreements --accept-source-agreements
    if errorlevel 1 (
        echo ? Winget installation failed.
        echo.
        echo ?? Please install Node.js manually:
        echo    1. Visit: https://nodejs.org/en/download/
        echo    2. Download the LTS version for Windows
        echo    3. Run the installer with default settings
        echo    4. Restart your terminal/command prompt
        echo    5. Run this script again
        echo.
        pause
        exit /b 1
    )
    
    echo ? Node.js installation completed!
    echo ?? Please restart your command prompt and run this script again.
    pause
    exit /b 0
) else (
    echo ? Node.js is already installed
    node --version
)

REM Check if npm is working
npm --version >nul 2>&1
if errorlevel 1 (
    echo ? npm not found. This usually means Node.js installation is incomplete.
    echo    Please reinstall Node.js from https://nodejs.org/
    pause
    exit /b 1
) else (
    echo ? npm is working
    npm --version
)

echo.
echo ?? Installing Mermaid CLI...

REM Check if Mermaid CLI is already installed
mmdc --version >nul 2>&1
if errorlevel 1 (
    echo ?? Installing @mermaid-js/mermaid-cli globally...
    npm install -g @mermaid-js/mermaid-cli
    
    if errorlevel 1 (
        echo ? Failed to install Mermaid CLI
        echo    Try running: npm install -g @mermaid-js/mermaid-cli
        pause
        exit /b 1
    )
    
    echo ? Mermaid CLI installed successfully!
) else (
    echo ? Mermaid CLI is already installed
    mmdc --version
)

echo.
echo ?? Testing complete setup...

REM Test all components
set /a tests_passed=0
set /a total_tests=3

node --version >nul 2>&1
if not errorlevel 1 (
    echo ? Node.js test passed
    set /a tests_passed+=1
) else (
    echo ? Node.js test failed
)

npm --version >nul 2>&1
if not errorlevel 1 (
    echo ? npm test passed
    set /a tests_passed+=1
) else (
    echo ? npm test failed
)

mmdc --version >nul 2>&1
if not errorlevel 1 (
    echo ? Mermaid CLI test passed
    set /a tests_passed+=1
) else (
    echo ? Mermaid CLI test failed
)

echo.
echo ?? Setup Summary:
echo    Tests passed: %tests_passed%/%total_tests%

if %tests_passed%==3 (
    echo.
    echo ?? Setup completed successfully!
    echo    You can now generate Mermaid diagrams!
    echo.
    echo ?? Next steps:
    echo    1. Run: generate-diagrams.bat
    echo    2. Or run: generate-diagrams.ps1
    echo.
    echo ?? Diagrams will be available at:
    echo    http://localhost:8080/diagrams/picking-game-flow.svg
    echo    http://localhost:8080/diagrams/joining-game-flow.svg
    echo    http://localhost:8080/diagrams/updating-game-flow.svg
) else (
    echo.
    echo ??  Setup incomplete. Some components failed to install.
    echo    Please restart your command prompt and try again.
    echo    If problems persist, install Node.js manually from:
    echo    https://nodejs.org/en/download/
)

echo.
pause
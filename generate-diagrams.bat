@echo off
echo ?? Generating Mermaid Diagrams for Catan3 Companion...
echo.

REM Check if mmdc is available
mmdc --version >nul 2>&1
if errorlevel 1 (
    echo ? Mermaid CLI not found. Please install it first:
    echo    npm install -g @mermaid-js/mermaid-cli
    echo    Then run this script again.
    pause
    exit /b 1
)

echo ? Mermaid CLI found

REM Create diagrams directory if it doesn't exist
if not exist "Catan3.GameService\wwwroot\diagrams" (
    mkdir "Catan3.GameService\wwwroot\diagrams"
    echo ?? Created diagrams directory
)

echo.
echo ?? Generating diagrams...

REM Generate each diagram
echo   Generating Picking Game Flow...
mmdc -i "Catan3.GameService\wwwroot\mermaid-source\picking-game-flow.mmd" -o "Catan3.GameService\wwwroot\diagrams\picking-game-flow.svg" -t neutral -b white --scale 2

echo   Generating Joining Game Flow...
mmdc -i "Catan3.GameService\wwwroot\mermaid-source\joining-game-flow.mmd" -o "Catan3.GameService\wwwroot\diagrams\joining-game-flow.svg" -t neutral -b white --scale 2

echo   Generating Updating Game Flow...
mmdc -i "Catan3.GameService\wwwroot\mermaid-source\updating-game-flow.mmd" -o "Catan3.GameService\wwwroot\diagrams\updating-game-flow.svg" -t neutral -b white --scale 2

echo.
echo ?? Diagram generation complete!
echo ?? Diagrams are available in: Catan3.GameService\wwwroot\diagrams\
echo.
echo ?? Web URLs (when game service is running):
echo    http://localhost:8080/diagrams/picking-game-flow.svg
echo    http://localhost:8080/diagrams/joining-game-flow.svg
echo    http://localhost:8080/diagrams/updating-game-flow.svg
echo.
pause
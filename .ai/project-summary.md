# Catan Project Summary

## Current State (2025-12-09)

The Catan3 project is a multi-platform Settlers of Catan game system with:

- **Desktop App** (WinUI3) - Working reference implementation with service mode capability
- **GameService** (ASP.NET Core) - SignalR + REST API backend with Azure deployment support
- **WebUI** (Blazor WASM) - **Thick client** with client-side SVG board rendering
- **WebUI.Server** - Server-side Blazor host for Azure App Service deployment
- **Shared Library** - Common models and game logic
- **Test Suite** - Unified test infrastructure with modern ReplayTest approach

## Latest Session (December 9, 2025 - Azure Doctor & Theme Optimization)

### Azure Doctor Function Refactoring

Refactored doctor functions in `catan-azure.ps1` following reference script patterns:

- **Doctor functions** (`Get-GameServiceDoctor`, `Get-DatabaseDoctor`, `Get-UIDoctor`):
  - Accept `TraceLevel` parameter, only emit DEBUG messages
  - Return clean hashtables with `checks`, `needsInstall`, `needsDeploy`, `healthy`, `deployReason`
- **`Show-DoctorResult`** function for formatted table display
- Main loop handles `-Json`, `-HashTable`, or formatted table output

### Health Endpoint Version Tracking

Added version info to `/health` endpoint:

```json
{
  "version": {
    "commit": "abc123...",
    "buildTime": "2025-12-09T10:30:00Z",
    "environment": "Production"
  }
}
```

- Deploy sets `DEPLOY_COMMIT` and `DEPLOY_BUILD_TIME` app settings
- Doctor reads version from health endpoint (more reliable than Azure settings)
- Shows "NEEDS DEPLOY" with reason when code is outdated

### Theme Image Optimization

Replaced large base theme images with optimized versions:

- **Before**: 118MB (resources: 59MB, tiles: 56MB)
- **After**: 6.8MB (resources: 1.6MB, tiles: 2.3MB)
- Removed separate `web` theme - base theme now uses optimized images
- Deployment package significantly reduced

### Layout CSS Fixes (In Progress)

Working on responsive scaling for 4K TVs and small screens:

- Fixed MainLayout: `height: 100vh/100dvh` instead of `min-height`
- Added `min-height: 0` to `main` for flex shrinking
- Reverted BoardContainer to fixed dimensions (1050x950px) - absolute positioning broke road/building alignment
- Removed `aspect-ratio` from SVGs (conflicted with preserveAspectRatio)

## Previous Session (December 9, 2025 - Azure SQL Serverless)

### Azure SQL Serverless Implementation

- **Zero-Config Database Switching**: `DatabaseProviderDetector` auto-detects SQLite vs SQL Server
- **Managed Identity Auth**: Passwordless authentication via DefaultAzureCredential
- **WebUI.Server Project**: Server-side Blazor host for Azure deployment
- **Connection String Format**: `Server=tcp:server.database.windows.net;Database=CatanGame;Authentication=Active Directory Default`

### Azure Deployment Infrastructure

- **`catan-azure.ps1`**: Comprehensive Azure management script
- **Commands**: `install`, `deploy`, `doctor`, `clean`, `destroy` for each component
- **Components**: `game-service`, `database`, `ui`
- **Resource Naming**: `{baseName}-api`, `{baseName}-db`, `{baseName}-ui`

## Previous Session (December 8, 2025 - CSS Scaling System)

### ViewportScaler Implementation

JavaScript-based uniform scaling matching XAML Viewbox behavior:

- **Reference dimensions**: 1920x1080 (landscape), 1080x1920 (portrait)
- **Orientation detection**: Aspect ratio < 4:3 = portrait
- **Scale calculation**: `Math.min(scaleX, scaleY)` for uniform fit
- **CSS variables**: `--viewport-scale`, `--base-width`, `--base-height`

### Environment Indicator

CSS pseudo-element shows environment and orientation:
- `data-env="local"` or `data-env="web"`
- `data-layout-mode="landscape"` or `data-layout-mode="portrait"`
- Version string: `CSS 2025-12-09 v11 LOCAL landscape`

## Important Files

### Azure Infrastructure

- `.scripts/catan-azure.ps1` - Azure deployment and management
- `.azure/catan-azure.json` - Azure configuration (gitignored)
- `webui.ps1` - Development workflow with Azure commands

### Layout & Scaling

- `WebUI/wwwroot/js/viewportScaler.js` - JavaScript uniform scaling
- `WebUI/Layout/MainLayout.razor.css` - Page layout chain
- `WebUI/Pages/Game.razor.css` - Game page grid layout
- `WebUI/Components/Board/BoardContainer.razor.css` - Board container sizing

### Database

- `Catan3.Shared/Data/DatabaseProviderDetector.cs` - Auto-detects SQLite vs SQL Server
- `Catan3.GameService/Program.cs` - Database configuration and health endpoint

## Development Workflow

### Local Development

```bash
# Start services with hot reload
./webui.ps1 run

# Stop services
./webui.ps1 stop

# Clean build artifacts
./webui.ps1 clean

# Database operations
./webui.ps1 database doctor
./webui.ps1 database install
```

### Azure Deployment

```bash
# Full deploy (checks doctor first)
./webui.ps1 azure deploy

# Check health
./webui.ps1 azure doctor

# Individual components
./webui.ps1 azure game-service deploy
./webui.ps1 azure database deploy
./webui.ps1 azure ui deploy
```

### Service URLs

- **Local GameService**: http://localhost:8080
- **Local WebUI**: http://localhost:5296
- **Azure GameService**: https://{baseName}-api.azurewebsites.net
- **Azure WebUI**: https://{baseName}-ui.azurewebsites.net

## Current Issues

### Layout Not Scaling to Full Height

The game layout is not consuming full viewport height on widescreen monitors:

- CSS chain fixed: `.page` → `main` → `.content` → `.game-viewport` → `.game-container`
- ViewportScaler uses `Math.min(scaleX, scaleY)` for uniform scaling
- May need further testing to verify height propagation

### GameServiceProxy Uses SignalR Instead of HTTP POST (Latent Bug)

**Architecture principle**: HTTP for commands IN, SignalR for updates OUT.

**Current state**: `GameServiceProxy` sends all game commands via SignalR `InvokeAsync()`:

- `ExecuteMoveRobber`, `ExecuteRoll`, `ExecutePurchase`, `ExecuteRoadPurchase`, etc.

**Correct pattern**: Commands should POST to `/api/game/action` with:

```json
{
  "gameId": "...",
  "playerId": "...",
  "messageType": "MoveRobberMessage",
  "messageData": { "coordinates": {...}, "targetPlayerId": "..." }
}
```

**Impact**: The `AsyncCommandProcessor` already supports all message types correctly. Only the client-side `GameServiceProxy` needs to change from SignalR invoke to HTTP POST.

**Files to modify**:

- `Catan3.Shared/Services/GameServiceProxy.cs` - Change all `_connection.InvokeAsync()` calls to HTTP POST
- `Catan3.GameService/Hubs/GameHub.cs` - Remove redundant `Execute*` methods (or deprecate)

## Next Session Priorities

1. **Test Layout**: Verify v11 CSS fixes height issue on widescreen/4K
2. **Test Multi-Device**: iOS, Android, portrait/landscape modes
3. **Build and Test**: Run full build with tests
4. **Deploy to Azure**: Test version tracking with new health endpoint

## Rules & Patterns

- **Fixed Coordinate System**: 1920x1080 internal coordinates scaled uniformly
- **CSS Variables for Theming**: All colors via CSS custom properties
- **Doctor-Based Deployment**: Check health before deploying, skip completed steps
- **Version Tracking**: Health endpoint returns commit hash and build time
- **Theme Images**: Use optimized images in base theme (no separate web theme)
- **Build Command**: `./build.ps1 -NoTest` for quick builds

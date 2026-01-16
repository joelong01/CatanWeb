# WebUI Design Document

## Overview

The WebUI project is a Blazor WebAssembly client for the Catan game, providing a cross-platform web-based alternative to the WinUI3 Desktop app. Both clients share the same backend (GameService) and leverage the Catan3.Shared library for models, game logic, and utilities.

## Architecture Decision

### Why Blazor WebAssembly?

- **C# Everywhere**: Directly reference Catan3.Shared - no need to rewrite models or coordinate math
- **Shared Library Reuse**: HexCoordinates, all models, extensions, and validation logic work identically
- **SignalR Integration**: Same Microsoft.AspNetCore.SignalR.Client package as Desktop app
- **Cross-Platform**: Works on any device with a modern browser (Windows, Mac, Linux, iOS, Android)

### Alternative Considered

React/TypeScript would require:

- Auto-generating TypeScript types from C# models
- Rewriting hex coordinate math (~100 lines)
- Different SignalR client library

Blazor eliminates this translation layer entirely.

## Client Architecture: Thick Client with State Service

### Design Decision: Client-Side Rendering

**WebUI uses a "thick client" architecture** where the client handles all rendering and UI state management:

```
Server (GameService)
    ↓ SignalR
Sends: GameModel only (game state)
    ↓
GameStateService (Blazor singleton)
    ├── Holds: GameModel (authoritative game state)
    ├── Holds: PlayerData[] (for colors, images, names)
    ├── Holds: UI state (shownStars, selected tiles, etc.)
    ├── Renders: SVG client-side using shared C# code
    └── Triggers: Component re-renders via OnStateChanged event
    ↓
Components (Game.razor, BoardMeasurement.razor, etc.)
    ├── Subscribe to GameStateService.OnStateChanged
    ├── Call service methods to update state
    └── Service handles rendering and notifies all subscribers
```

### Benefits of Thick Client Architecture

✅ **Instant UI feedback** - No server round-trip for UI-only changes (slider, highlighting)
✅ **Animations trivial** - CSS transitions between DOM states
✅ **Code reuse** - Same C# rendering code (BoardSvgGenerator, TileSvgRenderer) runs in browser via Blazor WASM
✅ **Scales better** - Server only handles game logic, rendering distributed to clients
✅ **Modern device power** - Phones and PCs have plenty of compute capacity
✅ **Offline potential** - PWA capability with cached state
✅ **Simplified server** - GameService only sends GameModel updates, no SVG generation

### Server Responsibilities (GameService)

- **Game logic** - GameStateMachine processes actions, validates moves
- **State updates** - Sends GameModel via SignalR when state changes
- **Player data** - Provides PlayerData via REST API for profile selection
- **Asset serving** - Serves static images, SVG files (settlement.svg, city.svg, etc.)

**What server does NOT do:**

- ❌ Generate SVG
- ❌ Manage UI state (shownStars, highlighting, etc.)
- ❌ Handle animations

### Client Responsibilities (WebUI)

- **State management** - GameStateService holds GameModel and PlayerData
- **SVG rendering** - Client-side SVG generation using shared C# renderers
- **UI state** - Track slider positions, selections, hover states
- **Animations** - CSS transitions and animations
- **User input** - Send game actions to server via SignalR

### GameStateService (Singleton)

**Purpose**: Central state manager that holds game state and coordinates rendering across all components.

```csharp
public class GameStateService
{
    // Game state
    private GameModel _gameModel;
    private Dictionary<string, PlayerData> _playerData;

    // UI state (not in GameModel)
    public int ShownStars { get; private set; }

    // Rendering components (client-side)
    private readonly BoardSvgGenerator _boardGenerator;
    private readonly TileSvgRenderer _tileRenderer;
    private readonly BuildingSvgRenderer _buildingRenderer;
    private readonly RoadSvgRenderer _roadRenderer;
    private readonly HarborSvgRenderer _harborRenderer;

    // State change notifications
    public event Action? OnStateChanged;

    // Called by SignalR when server sends update
    public void UpdateGameState(GameModel gameModel)
    {
        _gameModel = gameModel;
        NotifyStateChanged();
    }

    // Client-side rendering
    public string GenerateBoardSvg()
    {
        return _boardGenerator.GenerateBoardSvg(_gameModel, _playerData, ShownStars);
    }

    public string GeneratePlayerStatsSvg(string playerId)
    {
        return _playerStatsRenderer.Render(_gameModel, _playerData[playerId]);
    }

    // UI actions (client-only, no server call)
    public void SetShownStars(int stars)
    {
        ShownStars = stars;
        NotifyStateChanged(); // All subscribed components re-render
    }

    private void NotifyStateChanged() => OnStateChanged?.Invoke();
}
```

### Component Pattern

All components that display game state subscribe to `GameStateService`:

```csharp
@inject GameStateService GameState
@implements IDisposable

<div class="game-board">
    @((MarkupString)GameState.GenerateBoardSvg())
</div>

@code {
    protected override void OnInitialized()
    {
        GameState.OnStateChanged += HandleStateChanged;
    }

    private void HandleStateChanged()
    {
        StateHasChanged(); // Re-render this component
    }

    public void Dispose()
    {
        GameState.OnStateChanged -= HandleStateChanged;
    }
}
```

### Animation Strategy

**Client-side CSS animations triggered by state changes:**

```css
.tile {
    opacity: 1;
    transition: opacity 0.3s ease;
}

.tile.dimmed {
    opacity: 0.5;
}

@keyframes flip-card {
    0% { transform: rotateY(0deg); }
    50% { transform: rotateY(90deg); }
    100% { transform: rotateY(180deg); }
}

.resource-card.flipping {
    animation: flip-card 0.5s ease;
}
```

**Flow:**

1. Server sends updated GameModel via SignalR
2. GameStateService.UpdateGameState() called
3. Components detect state change and re-render
4. DOM diff shows what changed (e.g., tile now has "dimmed" class)
5. Browser CSS handles animation automatically

### SignalR Integration

**Server → Client (GameModel updates):**

```csharp
// GameService sends only GameModel
await Clients.Group(gameId).SendAsync("GameStateUpdated", gameModel);
```

**Client receives and updates state:**

```csharp
public class GameHubService
{
    private readonly GameStateService _gameState;

    public async Task ConnectAsync(string gameId)
    {
        _connection.On<GameModel>("GameStateUpdated", gameModel =>
        {
            _gameState.UpdateGameState(gameModel);
        });

        await _connection.StartAsync();
    }
}
```

**Client → Server (User actions):**

```csharp
// User clicks to place settlement
await _hubConnection.InvokeAsync("PlaceSettlement", buildingCoords);
// Server processes, updates GameModel, broadcasts to all clients
```

### Why This Works Better Than Thin Client

**Thin client approach we considered:**

- Server generates SVG for every update
- Client just displays SVG
- Animations require complex coordination

**Problems with thin client:**

- Network latency for every UI change
- Complex animation coordination
- Server does rendering work for all clients
- No instant feedback

**Thick client advantages:**

- UI changes instant (no network)
- Animations are just CSS
- Server scales better (less work per client)
- Modern web app pattern (React, Vue, Angular all work this way)
- Blazor WASM advantage: can run same C# rendering code client-side

## Project Structure

```
Catan/
├── Catan3.Shared/           # Shared models, logic, utilities (REUSE)
├── Catan3.GameService/      # SignalR + REST backend
│   └── Services/            # Game state management only (NO rendering)
├── DesktopApp/              # WinUI3 client (REFERENCE)
├── WebUI/                   # Blazor WebAssembly client (NEW)
│   ├── Components/          # Reusable Blazor components
│   │   ├── Board/           # Board measurement, game board display
│   │   │   └── BoardMeasurement.razor
│   │   ├── Resources/       # Resource cards, star counters
│   │   │   ├── ResourceCard.razor
│   │   │   └── StarCounter.razor
│   │   └── Shared/          # Common UI components
│   │       └── IconButton.razor
│   ├── Pages/               # Routable pages
│   │   ├── Home.razor
│   │   ├── NewGame.razor
│   │   ├── LoadGame.razor
│   │   └── Game.razor
│   ├── Services/            # Client-side services (THICK CLIENT)
│   │   ├── GameStateService.cs      # State manager (singleton)
│   │   ├── GameHubService.cs        # SignalR connection
│   │   └── Rendering/               # SVG generation (client-side)
│   │       ├── BoardSvgConstants.cs # Rendering constants
│   │       ├── BoardSvgGenerator.cs # Main board compositor
│   │       ├── TileSvgRenderer.cs   # Tile rendering
│   │       ├── BuildingSvgRenderer.cs # Building rendering
│   │       ├── RoadSvgRenderer.cs   # Road rendering
│   │       ├── HarborSvgRenderer.cs # Harbor rendering
│   │       └── PlayerStatsSvgRenderer.cs # Player stats
│   ├── Layout/              # App layout
│   │   ├── MainLayout.razor
│   │   └── NavMenu.razor
│   ├── wwwroot/             # Static assets
│   │   ├── css/            # Stylesheets
│   │   ├── images/         # Game assets (tiles, resources, SVG files)
│   │   └── index.html      # App entry point
│   └── Program.cs           # Service registration, DI setup
└── Tests/
    └── WebUI/              # WebUI tests (bUnit, Playwright)
```

## What's Already Done

1. **Project Created**: `dotnet new blazorwasm -o WebUI -n Catan3.WebUI`
2. **Shared Reference Added**: `dotnet add reference ../Catan3.Shared/Catan3.Shared.csproj`
3. **SignalR Package Added**: `Microsoft.AspNetCore.SignalR.Client`
4. **Added to Solution**: Project included in Catan.sln

## Data Architecture

### Model vs ViewData Separation

The codebase distinguishes between game state and view/display concerns:

| Layer | Location | Naming | Purpose | Example |
|-------|----------|--------|---------|---------|
| **Models** | `Catan3.Shared/Models/` | `*Model` | Game state & logic | `PlayerModel` (scores, resources) |
| **ViewData** | `Catan3.Shared/ViewData/` | `*Data` | UI/display concerns | `PlayerData` (colors, images) |
| **ViewModels** | `DesktopApp/` or `WebUI/` | `*ViewModel` | Platform-specific UI | Desktop's `PlayerViewModel` (BitmapImage) |

**Why this separation?**

- `PlayerModel` contains game state (scores, resources, entitlements)
- `PlayerData` contains profile/display data (colors, images) needed by UI
- GameService API returns `PlayerData` for player selection
- WebUI uses `PlayerData` directly
- Desktop wraps it with platform-specific rendering (BitmapImage, Brush)

**Future CosmosDB migration:**

- `PlayerData` will be stored in CosmosDB as player profiles
- `PlayerModel` remains in-memory during gameplay
- Clear separation makes migration straightforward

### Database Architecture

The GameService uses SQLite for local development with a document-oriented model that mirrors the future CosmosDB schema.

**Database Location:** `Catan3.GameService/Data/catan.db`

**Tables:**

| Table | Schema | Description |
|-------|--------|-------------|
| **Players** | `Id (PK), Data (JSON)` | Player profiles stored as JSON documents |
| **Images** | `Id (PK), ContentType, Data (blob)` | Player images stored as binary blobs |
| **GameSaves** | `GameId (PK), CompressedData (blob), SavedAt (datetime), GameName (string)` | Saved game state as compressed .catan format |

**Storage Migration:**

All storage operations must use the database instead of the filesystem:

- **Game saves**: Previously saved to `.catan` files on disk, now stored in `GameSaves` table
- **Auto-save**: Game state persisted to database after each action
- **Load game**: Retrieve compressed data from `GameSaves` table
- **Benefits**:
  - Works in cloud/containerized environments
  - No filesystem permissions needed
  - Easy backup/migration
  - Consistent with CosmosDB migration path

**Document Model:**

- `PlayerEntity.Data` contains serialized `PlayerData` JSON
- This mirrors CosmosDB's document storage pattern
- Easy migration: replace SQLite provider with CosmosDB provider

**API Endpoints:**

| Endpoint | Description |
|----------|-------------|
| `GET /api/players` | Returns all players from database |
| `GET /api/images/{id}` | Serves image binary from database |

**Seeding:**

```powershell
cd Catan3.GameService
dotnet run -- --seed-database
```

This loads player data and images from `DesktopApp/Assets/DefaultPlayers/` into the database.

**Database Files:**

- `CatanDbContext.cs` - EF Core DbContext with Players and Images
- `DatabaseSeeder.cs` - Seeds database with player data and images

## What Catan3.Shared Provides

### Models (Direct Reuse - Game State)

- `GameModel` - Complete game state
- `TileModel` - Hex tile data
- `PlayerModel` - Player game state (scores, resources)
- `BuildingModel`, `RoadModel` - Game pieces
- `HarborModel` - Trading ports
- All enums: `GameState`, `ResourceType`, `Direction`, etc.

### ViewData (Direct Reuse - UI Concerns)

- `PlayerData` - Player profile (Id, Name, Colors, ImageUri)

### Utilities (Direct Reuse)

- `HexCoordinates` - Cube coordinate system with:
  - `ToPixelCenter(size, offsetX, offsetY)` - Convert hex to SVG position
  - `FromPixel(x, y, size, offsetX, offsetY)` - Hit testing
  - `Distance()`, `IsAdjacent()`, `GetAllNeighbors()`
  - Direction properties: `North`, `South`, etc.

### Extensions (Direct Reuse)

- `TileModelExtensions` - `TileFromCoords()`, `AdjacentTiles()`, etc.
- `GameModelExtensions` - `ValidateGame()`, etc.

### Message Types (Direct Reuse)

- All SignalR message types for communication with GameService

## Implementation Steps

### Phase 1: Core Infrastructure

1. **Create GameHubService** (`Services/GameHubService.cs`)
   - SignalR connection management
   - Message sending/receiving
   - Connection state handling
   - Reference: `DesktopApp/Services/GameServiceProxy.cs`

2. **Add Shared Namespace Imports** (`_Imports.razor`)

   ```razor
   @using Catan3.Shared.Models
   @using Catan3.Shared.Utility
   @using Catan3.Shared.Extensions
   ```

3. **Configure Services** (`Program.cs`)
   - Register GameHubService
   - Configure base URL for GameService

### Phase 2: Basic Rendering

4. **Create HexTile Component** (`Components/Board/HexTile.razor`)
   - SVG polygon for flat-top hexagon
   - Resource type coloring
   - Number token display
   - Click/hover events

   ```razor
   @using Catan3.Shared.Utility

   <g transform="translate(@X, @Y)">
       <polygon points="@HexPoints" fill="@FillColor" stroke="black" />
       <text>@Number</text>
   </g>

   @code {
       [Parameter] public HexCoordinates Coords { get; set; }
       [Parameter] public double Size { get; set; } = 50;

       private string HexPoints => CalculateFlatTopHexPoints(Size);
       private (double X, double Y) Position => Coords.ToPixelCenter(Size, OffsetX, OffsetY);
   }
   ```

5. **Create GameBoard Component** (`Components/Board/GameBoard.razor`)
   - SVG container
   - Render all tiles from GameModel
   - Handle board interactions

   ```razor
   <svg width="@Width" height="@Height" viewBox="@ViewBox">
       @foreach (var tile in GameModel.Tiles)
       {
           <HexTile Coords="@tile.TileKey"
                    Resource="@tile.ResourceTileType"
                    Number="@tile.Number" />
       }
   </svg>
   ```

6. **Create Game Page** (`Pages/Game.razor`)
   - Main game view
   - GameBoard component
   - Player panels
   - Action buttons

### Phase 3: Player & Game State

7. **Create PlayerCard Component** (`Components/Player/PlayerCard.razor`)
   - Player name and color
   - Resource counts
   - Score display
   - Active player indicator

8. **Create PlayerHand Component** (`Components/Player/PlayerHand.razor`)
   - Resource cards display
   - Development cards

9. **Implement Game State Display**
   - Turn indicator
   - Action prompts
   - Dice display

### Phase 4: Interactions

10. **Implement Drag-and-Drop for Tile Swapping**
    - Use HTML5 drag events or pointer events
    - Leverage `HexCoordinates.FromPixel()` for hit testing
    - Reference: `DesktopApp/Game/GameFactory/GameBoardCtrl.xaml.cs`

11. **Implement Building Placement**
    - Click handlers on valid positions
    - Visual feedback for buildable locations

12. **Implement Road Placement**
    - Edge detection
    - Valid road highlighting

### Phase 5: Game Flow

13. **Create NewGame Page** (`Pages/NewGame.razor`)
    - Player selection
    - Game type selection
    - Board shuffling

14. **Implement Dice Rolling**
    - Roll animation
    - Resource distribution display

15. **Implement Trading**
    - Trade dialog
    - Bank/harbor trade support

### Phase 6: Polish

16. **Add Animations**
    - CSS transitions for smooth updates
    - Dice roll animation
    - Resource distribution effects

17. **Responsive Design**
    - Mobile-friendly layout
    - Touch support
    - Viewport scaling

18. **Add Sound Effects** (optional)
    - Dice roll
    - Building placement
    - Resource collection

## Component Mapping: Desktop → WebUI

| Desktop (XAML) | WebUI (Blazor) | Notes |
|----------------|----------------|-------|
| `TileCtrl.xaml` | `HexTile.razor` | SVG polygon instead of XAML Path |
| `GameBoardCtrl.xaml` | `GameBoard.razor` | SVG container, same hit testing logic |
| `PlayerCtrl.xaml` | `PlayerCard.razor` | HTML/CSS layout |
| `RoadCtrl.xaml` | `Road.razor` | SVG line or path |
| `BuildingCtrl.xaml` | `Building.razor` | SVG shapes |
| `HarborCtrl.xaml` | `Harbor.razor` | SVG with rotation |
| `GameViewModel` | Game page state | Can potentially reuse with adaptation |

## SVG Hex Rendering

### Flat-Top Hexagon Points

For a flat-top hexagon centered at (0, 0) with size `s`:

```csharp
// 6 vertices, starting from right vertex, counter-clockwise
var points = new[]
{
    (s, 0),                           // Right
    (s/2, s * Math.Sqrt(3)/2),        // Bottom-right
    (-s/2, s * Math.Sqrt(3)/2),       // Bottom-left
    (-s, 0),                          // Left
    (-s/2, -s * Math.Sqrt(3)/2),      // Top-left
    (s/2, -s * Math.Sqrt(3)/2)        // Top-right
};
```

### Positioning Hexes

Use `HexCoordinates.ToPixelCenter()` directly:

```csharp
var center = tile.TileKey.ToPixelCenter(hexSize, boardOffsetX, boardOffsetY);
// Use center.X and center.Y for SVG transform
```

## SignalR Integration

### Connection Setup

```csharp
public class GameHubService
{
    private HubConnection _connection;

    public async Task ConnectAsync(string serviceUrl)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl($"{serviceUrl}/gameHub")
            .WithAutomaticReconnect()
            .Build();

        // Register handlers
        _connection.On<GameModel>("GameStateUpdated", OnGameStateUpdated);

        await _connection.StartAsync();
    }
}
```

### Message Handlers

Mirror the Desktop app's service mode handlers. The message types from Catan3.Shared work identically.

## Development Workflow

### WebUI Development Script (webui.ps1)

The `webui.ps1` PowerShell script provides a convenient way to build, run, and manage the WebUI development environment.

**Commands:**

| Command | Description |
|---------|-------------|
| `./webui.ps1 build` | Build all projects (calls `./build.ps1 -NoTest`) |
| `./webui.ps1 run` | Initialize database, start GameService and WebUI, launch browser |
| `./webui.ps1 debug` | Instructions for VS Code debugging |
| `./webui.ps1 clean` | Delete database and clean all build artifacts |

**First-time setup:**

```powershell
./webui.ps1 run
```

This will:

1. Check if the database exists at `Catan3.GameService/Data/catan.db`
2. If not, run `dotnet run -- --seed-database` to create and seed it
3. Start GameService on port 8080
4. Start WebUI on port 5296
5. Launch browser to `http://localhost:5296/newgame`

**Clean rebuild:**

```powershell
./webui.ps1 clean
./webui.ps1 run
```

### VS Code Launch Configurations

The project includes pre-configured VS Code launch options. Press `F5` or use the Run and Debug panel:

| Configuration | Description |
|---------------|-------------|
| **Debug GameService** | Runs GameService on `http://localhost:5024` with debugger |
| **Debug WebUI** | Runs WebUI on `http://localhost:5296` with debugger |
| **Debug WebUI (with GameService)** | Starts GameService first, then WebUI with debugger |
| **WebUI + GameService** | Compound launch - debugs both simultaneously |

**Recommended for development**: Use **"WebUI + GameService"** compound launch to debug both projects together.

### Running from Command Line

**WebUI only:**

```bash
cd WebUI
dotnet run
```

Opens at `http://localhost:5296`

**GameService only:**

```bash
cd Catan3.GameService
dotnet run
```

Runs at `http://localhost:5024`

**Both together:**

```bash
# Terminal 1
cd Catan3.GameService && dotnet run

# Terminal 2
cd WebUI && dotnet run
```

### Hot Reload

Blazor supports hot reload for rapid development:

```bash
cd WebUI
dotnet watch run
```

### Service URLs

| Service | URL |
|---------|-----|
| GameService | `http://localhost:5024` |
| GameService SignalR Hub | `http://localhost:5024/gameHub` |
| WebUI | `http://localhost:5296` |

## Testing Strategy

**Principle**: Every component and service must have corresponding tests written alongside the implementation. No PR should add functionality without test coverage.

### Test Project Structure

```
Tests/
├── WebUI/                           # NEW - WebUI test project
│   ├── Tests.WebUI.csproj
│   ├── Components/                  # Component unit tests
│   │   ├── HexTileTests.cs
│   │   ├── GameBoardTests.cs
│   │   └── PlayerCardTests.cs
│   ├── Services/                    # Service tests
│   │   └── GameHubServiceTests.cs
│   ├── Integration/                 # SignalR integration tests
│   │   └── GameFlowTests.cs
│   └── E2E/                         # End-to-end tests
│       └── PlaywrightTests.cs
```

### Test Frameworks

| Type | Framework | Purpose |
|------|-----------|---------|
| Unit | **bUnit** | Blazor component testing in isolation |
| Unit | **xUnit** | General .NET unit testing |
| Mock | **Moq** | Mock services and dependencies |
| Integration | **TestServer** | In-memory ASP.NET Core hosting |
| E2E | **Playwright** | Browser automation for full user flows |

### Creating the Test Project

```bash
# Create the test project
dotnet new xunit -o Tests/WebUI -n Tests.WebUI
cd Tests/WebUI

# Add required packages
dotnet add package bunit
dotnet add package Moq
dotnet add package Microsoft.Playwright

# Add project references
dotnet add reference ../../WebUI/Catan3.WebUI.csproj
dotnet add reference ../../Catan3.Shared/Catan3.Shared.csproj

# Add to solution
cd ../..
dotnet sln add Tests/WebUI/Tests.WebUI.csproj
```

### Unit Testing Components with bUnit

**Example: HexTile Component Test**

```csharp
using Bunit;
using Xunit;
using Catan3.WebUI.Components.Board;
using Catan3.Shared.Utility;
using Catan3.Shared.Models;

public class HexTileTests : TestContext
{
    [Fact]
    public void HexTile_RendersCorrectResource()
    {
        // Arrange
        var coords = new HexCoordinates(0, 0, 0);

        // Act
        var cut = RenderComponent<HexTile>(parameters => parameters
            .Add(p => p.Coords, coords)
            .Add(p => p.Resource, ResourceType.Wheat)
            .Add(p => p.Number, 8));

        // Assert
        cut.Find("polygon").GetAttribute("fill").Should().Contain("wheat");
        cut.Find("text").TextContent.Should().Be("8");
    }

    [Fact]
    public void HexTile_PositionsCorrectly()
    {
        // Arrange
        var coords = new HexCoordinates(1, -1, 0); // NorthEast of center

        // Act
        var cut = RenderComponent<HexTile>(parameters => parameters
            .Add(p => p.Coords, coords)
            .Add(p => p.Size, 50));

        // Assert - verify transform uses ToPixelCenter calculation
        var transform = cut.Find("g").GetAttribute("transform");
        var expectedCenter = coords.ToPixelCenter(50, 0, 0);
        transform.Should().Contain($"translate({expectedCenter.X}");
    }

    [Fact]
    public void HexTile_FiresClickEvent()
    {
        // Arrange
        var coords = new HexCoordinates(0, 0, 0);
        HexCoordinates? clickedCoords = null;

        // Act
        var cut = RenderComponent<HexTile>(parameters => parameters
            .Add(p => p.Coords, coords)
            .Add(p => p.OnClick, c => clickedCoords = c));

        cut.Find("polygon").Click();

        // Assert
        clickedCoords.Should().Be(coords);
    }
}
```

**Example: GameBoard Component Test**

```csharp
public class GameBoardTests : TestContext
{
    [Fact]
    public void GameBoard_RendersAllTiles()
    {
        // Arrange
        var gameModel = CreateTestGameModel(tileCount: 19);

        // Act
        var cut = RenderComponent<GameBoard>(parameters => parameters
            .Add(p => p.GameModel, gameModel));

        // Assert
        cut.FindAll("polygon").Count.Should().Be(19);
    }

    [Fact]
    public void GameBoard_HitTestReturnsCorrectTile()
    {
        // Arrange
        var gameModel = CreateTestGameModel();
        var cut = RenderComponent<GameBoard>(parameters => parameters
            .Add(p => p.GameModel, gameModel));

        // Act - simulate click at center hex position
        var centerCoords = new HexCoordinates(0, 0, 0);
        var pixelPos = centerCoords.ToPixelCenter(50, 100, 100);

        // Use HexCoordinates.FromPixel (same as production code)
        var hitCoords = HexCoordinates.FromPixel(pixelPos.X, pixelPos.Y, 50, 100, 100);

        // Assert
        hitCoords.Should().Be(centerCoords);
    }

    private GameModel CreateTestGameModel(int tileCount = 19)
    {
        // Helper to create test game models
        // Reuse test data patterns from Tests/GameService
    }
}
```

### Testing Services

**Example: GameHubService Test**

```csharp
public class GameHubServiceTests
{
    [Fact]
    public async Task ConnectAsync_EstablishesConnection()
    {
        // Arrange
        var mockConnection = new Mock<HubConnection>();
        var service = new GameHubService(mockConnection.Object);

        // Act
        await service.ConnectAsync();

        // Assert
        mockConnection.Verify(c => c.StartAsync(default), Times.Once);
    }

    [Fact]
    public async Task SendNewGame_InvokesHubMethod()
    {
        // Arrange
        var mockConnection = CreateMockHubConnection();
        var service = new GameHubService(mockConnection.Object);
        var players = new List<string> { "Player1", "Player2" };

        // Act
        await service.SendNewGameAsync(GameType.Regular, players);

        // Assert
        mockConnection.Verify(c => c.InvokeAsync(
            "NewGame",
            It.IsAny<object[]>(),
            default), Times.Once);
    }
}
```

### Integration Testing with GameService

**Example: Full Game Flow Test**

```csharp
public class GameFlowIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public GameFlowIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task NewGame_ReceivesGameState()
    {
        // Arrange
        var connection = await CreateSignalRConnection();
        GameModel? receivedGame = null;

        connection.On<GameModel>("GameStateUpdated", game =>
        {
            receivedGame = game;
        });

        // Act
        await connection.InvokeAsync("NewGame", GameType.Regular,
            new List<string> { "Alice", "Bob", "Charlie" });

        // Assert
        await WaitForCondition(() => receivedGame != null);
        receivedGame!.Players.Should().HaveCount(3);
        receivedGame.Tiles.Should().HaveCount(19);
        receivedGame.GameState.Should().Be(GameState.WaitingForStart);
    }

    [Fact]
    public async Task TileSwap_UpdatesGameState()
    {
        // Similar pattern - test real SignalR communication
    }
}
```

### End-to-End Testing with Playwright

**Example: Complete User Flow**

```csharp
public class PlaywrightE2ETests : IAsyncLifetime
{
    private IPlaywright _playwright;
    private IBrowser _browser;
    private IPage _page;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync();
        _page = await _browser.NewPageAsync();
    }

    [Fact]
    public async Task User_CanStartNewGame()
    {
        // Navigate to app
        await _page.GotoAsync("http://localhost:5000");

        // Click new game button
        await _page.ClickAsync("[data-testid='new-game-button']");

        // Select players
        await _page.ClickAsync("[data-testid='player-Alice']");
        await _page.ClickAsync("[data-testid='player-Bob']");

        // Start game
        await _page.ClickAsync("[data-testid='start-game-button']");

        // Verify game board appears
        await _page.WaitForSelectorAsync("[data-testid='game-board']");
        var tiles = await _page.QuerySelectorAllAsync("[data-testid='hex-tile']");
        tiles.Should().HaveCount(19);
    }

    [Fact]
    public async Task User_CanDragAndDropTiles()
    {
        // Setup game in PickingBoard state
        await SetupGameInPickingBoardState();

        // Perform drag and drop
        var sourceTile = await _page.QuerySelectorAsync("[data-testid='tile-0-0-0']");
        var destTile = await _page.QuerySelectorAsync("[data-testid='tile-1-0--1']");

        await sourceTile.DragToAsync(destTile);

        // Verify tiles swapped
        // ...
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }
}
```

### Test Data Management

Reuse existing test infrastructure from `Tests/Data`:

```csharp
// Load test scenarios
var testData = TestDataLoader.LoadTestFile("regular_game.catan_test");

// Use in tests
var gameModel = testData.InitialGameModel;
```

### Development Workflow with Tests

**For each implementation phase:**

1. **Write tests first** (TDD) or **immediately after** component creation
2. **Run tests locally** before committing:

   ```bash
   dotnet test Tests/WebUI
   ```

3. **CI runs all tests** on PR
4. **Coverage requirements**: Aim for >80% on new code

### Test Categories

Use traits to categorize tests for selective running:

```csharp
[Fact]
[Trait("Category", "Unit")]
public void HexTile_RendersCorrectly() { }

[Fact]
[Trait("Category", "Integration")]
public async Task SignalR_ConnectsSuccessfully() { }

[Fact]
[Trait("Category", "E2E")]
public async Task User_CompletesFullGame() { }
```

Run specific categories:

```bash
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
```

### CI/CD Integration

Add to build pipeline (e.g., GitHub Actions):

```yaml
- name: Test WebUI
  run: |
    dotnet test Tests/WebUI --configuration Release --logger trx

- name: E2E Tests
  run: |
    # Start services
    dotnet run --project Catan3.GameService &
    dotnet run --project WebUI &

    # Wait for services
    sleep 10

    # Run Playwright tests
    dotnet test Tests/WebUI --filter "Category=E2E"
```

### Testing Checklist for Each Component

Before marking a component complete:

- [ ] Unit tests for all public methods/properties
- [ ] Tests for edge cases (null, empty, invalid input)
- [ ] Tests for user interactions (click, drag, hover)
- [ ] Integration test if component uses services
- [ ] Test data-testid attributes added for E2E tests
- [ ] Tests pass locally
- [ ] Code coverage meets threshold

## Configuration

### appsettings.json

```json
{
  "GameService": {
    "Url": "https://localhost:5001"
  }
}
```

### Environment-Specific

- Development: localhost GameService
- Production: Deployed GameService URL

## Performance Considerations

- **SVG vs Canvas**: SVG scales perfectly and is easier for Blazor integration
- **Virtualization**: Not needed for Catan board size (~19-37 tiles)
- **State Management**: Keep game state in a single service, components subscribe to changes

## Future Enhancements

- **PWA Support**: Offline capability with service workers
- **WebRTC**: Direct peer-to-peer for reduced latency
- **Mobile App**: Wrap in MAUI Blazor Hybrid for app store distribution

## References

- [Blazor Documentation](https://docs.microsoft.com/aspnet/core/blazor/)
- [SignalR Client Documentation](https://docs.microsoft.com/aspnet/core/signalr/dotnet-client)
- [SVG Specification](https://developer.mozilla.org/en-US/docs/Web/SVG)
- [Red Blob Games Hexagons](https://www.redblobgames.com/grids/hexagons/)
- [Coordinate-Design.md](./Coordinate-Design.md) - Hex coordinate system details

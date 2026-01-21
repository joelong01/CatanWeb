# Session Summary - 2025-11-23

## Work Completed

### Harbor Rendering

- Implemented harbor rendering in BoardSvgGenerator.cs
- Fixed water triangle shape (base at hex edge vertices, apex at harbor center)
- Fixed viewBox bounds to include harbor positions
- Copied harbor images to GameService/wwwroot/images/harbors/

### Player Color Gradients

- Updated PlayerData model with PrimaryBackgroundColor, SecondaryBackgroundColor,
  ForegroundColor (replacing single BackgroundColor)
- Updated DatabaseSeeder with proper gradient colors for all default players
- Updated NewGame.razor to use CSS linear-gradient for player cards
- Updated Game.razor to use current player's gradient on left panel controls

### Database Design

- Created comprehensive database-design.md documenting all tables, use cases,
  and data management APIs
- Documents Players, Images, GameSaves tables with indices
- Includes future tables (GameInvitations, GameStats)
- Clarifies game stats (in GameModel) vs lifetime stats (future, in PlayerData)

### Bug Fixes

- Fixed critical player ID mismatch: NewGame was sending player Names instead
  of IDs (e.g., "Adrian" instead of "adrian-001")
- Added auto-seeding of database on GameService startup (idempotent)
- Added debug info panel showing PlayerProfiles loading status

### Developer Experience

- Added help command to webui.ps1 showing all available commands
- Improved Stop-Services function to wait for ports to be released
- Added player click test functionality to verify gradient colors

## Work in Progress

### Gradient Colors Not Displaying

- The left panel controls should show current player's gradient colors
- PlayerProfiles dictionary is loading correctly (verified via debug panel)
- Player IDs now match between GameModel and PlayerProfiles
- Need to verify colors render after the ID mismatch fix

## Decisions Made

- **Separation of concerns**: GameModel contains game state only, PlayerData
  contains profile/display data. They join via PlayerId.
- **Lazy loading**: PlayerProfiles loaded on-demand when GameModel arrives,
  handles F5 refresh and load game scenarios
- **Gradient colors**: Two background colors for gradient effect, matching
  Desktop app's PlayerColorViewModel pattern
- **Auto-seeding**: Database seeds automatically on startup if empty,
  eliminating need for manual seed commands

## Blockers & Issues

### Hot Reload Not Working

- Blazor WASM hot reload is broken - requires stop/clean/run cycle
- Very disruptive to development workflow
- Should investigate proper hot reload configuration

### webui.ps1 stop reliability

- Sometimes processes aren't fully killed
- Added delays and verification, but may need more robust solution

## Next Session Priority

1. **Verify gradient colors work** after the ID mismatch fix
2. **Fix hot reload** for Blazor WASM if possible
3. **Add player avatars** to the Game page player list
4. **Implement remaining Game page controls** (roll entry, purchase, etc.)

## Important Context

### Player ID Format

- Database stores IDs like "joe-001", "adrian-001"
- Display names extracted from ID: "joe-001" -> "Joe"
- Game must use full IDs, not just names

### Color Fields

- PrimaryBackgroundColor: Main gradient color
- SecondaryBackgroundColor: Darker gradient color
- ForegroundColor: Text color

### Data Flow

1. NewGame loads PlayerData from API
2. Selected player IDs sent to create game
3. Game page receives GameModel with player IDs
4. Game page loads PlayerProfiles, joins by ID
5. Controls render with current player's colors

## Environment Notes

- Database auto-seeds on first run
- No manual seed command needed
- webui.ps1 handles all service lifecycle

## Quick Start for Next Session

1. Pull latest changes: `git pull`
2. Start services: `./webui.ps1 run`
3. If issues: `./webui.ps1 stop && ./webui.ps1 clean && ./webui.ps1 run`
4. Current focus: WebUI/Pages/Game.razor
5. Continue with: Verify gradient colors display correctly

## Commands to Know

- Start dev: `./webui.ps1 run`
- Stop services: `./webui.ps1 stop`
- Rebuild WebUI: `./webui.ps1 update`
- Full clean: `./webui.ps1 clean`
- Show help: `./webui.ps1 help`
- Build all: `./build.ps1 -NoTest`

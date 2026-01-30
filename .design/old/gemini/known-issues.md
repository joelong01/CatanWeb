# Known Issues & Observations

**Status:** As-Built Audit Findings
**Date:** 2026-01-30

## 1. Code-Design Divergences

- **Winner Dialog vs Overlay**: Code references both `WinnerDialog` and `WinnerCelebration`. `GamePage` conditionally renders one or the other based on state, suggesting a migration or dual-mode UI is in progress.
- **Victory Points Overlay**: Comment says "BEING REPLACED" in `page.tsx` analysis, but code still imports and uses it.
- **REST vs SignalR**: The design intentionally deviates from the Blazor implementation. Blazor uses SignalR for *everything*. React uses REST for *commands* to ensure idempotency and ordering, while using SignalR only for *updates*. This is a robust architectural choice but differs from legacy docs.

## 2. Incomplete / TODO Items

- **GameState Enumeration**: `GamePage.tsx` contains a explicit Todo: `TODO: Auto-generate this from C# during build phase` regarding `GAME_STATE_MESSAGES`. Currently manually synced.
- **Cities & Knights Support**: The `GameState` enum contains many C&K states (`HandlePirates`, `MustMoveMerchant`, `PoliticsUpgrade`) but the React UI `GamePage` and `GAME_STATE_MESSAGES` mapping seems to treat them generically or minimally. Support may be partial in React.
- **Profile Images**: `PlayerProfile` interface supports images, but no upload/management UI was observed in the immediate file scan. Likely uses placeholders or static assets.

## 3. Potential Bugs / Risks

- **Sync Issues**: `GameStateMachine.cs` header warns: *"Sync Critical: Keep this class behaviorally in sync with `Catan3.GameService/Controllers/GameStateMachine.cs`."* This implies there might be *two* state machines (one shared, one in service)? **Correction**: The file read was `Catan3.Shared/GameLogic/GameStateMachine.cs`. The comment might be a legacy warning about a previous duplication, or referring to the CLI vs Service.
- **Error Handling**: `read_file` on `GameState.cs` failed initially. Typically this implies file system case sensitivity or path issues, though Windows is case-insensitive.
- **Hardcoded Strings**: Message types in `MessageObjects.cs` use `ToString()` overrides but the serialization relies on class names. Renaming classes would break the API contract.

## 4. UI/UX Gaps (vs Desktop)

- **Window Management**: The React `FloatingPanel` system mimics the Desktop windows, but lacks true OS-level window management (multi-monitor support).
- **Animations**: Desktop App likely has richer XAML animations for dice rolls/card deals. React implementation relies on CSS/Framer Motion but may essentially just "snap" state updates.

```

# Testing Strategy As-Built

**Status:** As-Built
**Source:** `.design/test-plan.md` & `catan.ps1`

## 1. Methodology

The project prioritizes **Regression Testing via Replay** over granular unit tests for game logic.
Since `GameStateMachine` is complex and deterministic, replaying a known sequence of moves and asserting the resulting `GameHash` is the most effective validation.

## 2. Test Pyramid

### A. Unit Tests (`Tests/Shared`)
*   **Focus**: `HexCoordinates` math, stateless utility helpers.
*   **Tools**: xUnit.

### B. Integration / Replay Tests (`Tests/GameService`)
*   **Focus**: Full game simulation.
*   **Mechanism**: Loads a JSON recording (`.catan_test`), replays 100+ actions, ensures the final state matches.
*   **Coverage**: "Full Simulated Game" recording covers ~90% of rule logic.

### C. UI Tests (Manual + Smoke)
*   **Focus**: Rendering, Network reconnection.
*   **Tools**: `catan.ps1 run` -> Manual verification. No automated browser tests (Selenium/Playwright) strictly enforced in CI yet.

## 3. CI/CD Pipeline

The `catan.ps1 test` command orchestrates the suite:

1.  **Build**: Compiles solution.
2.  **Unit**: Runs xUnit tests.
3.  **Replay**: Executes the Replay runner against standard recordings.
4.  **Database**: Verifies schema integrity via EF Core tools.

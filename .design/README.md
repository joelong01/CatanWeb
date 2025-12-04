# Catan Design Documentation

This directory contains the authoritative "as built" design documentation for the Catan project.
Each file reflects the current implementation in the repository as of December 3, 2025.

## Directory Structure

- `projects/` – Summaries for each solution project (DesktopApp, GameService, Shared, WebUI, CLI).
- `systems/` – Cross-cutting systems such as the game state machine, save/load pipeline, rendering flow, and real-time messaging.
- `gameplay/` – Per-game-state behavior and lifecycle notes.
- `ui/` – Component-level design notes for the WebUI and Desktop experiences.

## Source Mapping

Each document references its originating design file in `design_docs/` when applicable. Legacy docs remain untouched for historical context.

## Maintenance Notes

- Update the relevant document whenever implementation changes introduce new behavior or retire existing features.
- Use TODO sections to track gaps between intended and implemented behavior.
- Keep line lengths under 150 characters to satisfy markdown linting rules.

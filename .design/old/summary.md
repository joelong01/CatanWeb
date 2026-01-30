# Catan Project Design Summary

**Last Updated:** January 22, 2026
**Status:** Active Development

## Project Overview

The Catan project is a multi-client implementation of the Settlers of Catan board game. It features a centralized authoritative game server (`Catan3.GameService`) and multiple clients:

- **Blazor WebAssembly (`WebUI`)**: The current primary web interface.
- **WinUI 3 Desktop (`DesktopApp`)**: A native Windows implementation.
- **React + Next.js (`react-ui`)**: A new modern web client currently in development (porting from Blazor).

The architecture relies on a shared core logic library (`Catan3.Shared`) ensuring rule consistency across all platforms.

## Design Status Matrix

| ID | Design System | Status | Implementation Notes |
|----|--------------|--------|----------------------|
| **CORE** | **Game Engine** | ✅ Complete | `Catan3.Shared` handles authoritative state machine. |
| **API** | **Game Service** | ✅ Complete | ASP.NET Core with SignalR/REST hybrid. See `systems/game-service-api.md`. |
| **DATA** | **Persistence** | ✅ Complete | SQLite local storage with generic `IGamePersistence`. Azure ready. |
| **UI** | **Board Rendering** | ✅ Complete | SVG-based rendering in WebUI/React, XAML-based in Desktop. |
| **UI** | **Responsive/Mobile** | ✅ Complete | Media queries `(pointer: coarse)` and scaling logic implemented. |
| **FEAT** | **Balanced Board** | ✅ Complete | Algorithm to prevent clumping/imballance. See `balance-design.md`. |
| **FEAT** | **Grief/Dodgy** | ⚠️ Partial | "Dodgy" player logic and CSS animations exist. `GriefCelebration` pending full React port. |
| **INFRA** | **Azure Deployment** | 🚧 Planned | Designs exist (`azure*.md`), but local dev uses SQLite. |
| **PORT** | **React Migration** | 🔄 In Progress | `react-ui` scaffolding exists. Detailed plan in `ui/react/typescript-porting-design.md`. |

## Key Architecture Concepts

### 1. State Management

The `GameStateMachine` in `Catan3.Shared` is the single source of truth. State updates are pushed via SignalR to clients, which update their MVVM/React stores.

- **Design:** `systems/mvvm-messaging.md`, `ui/react/typescript-porting-design.md`
- **Impl:** `GameHub`, `GameModel`, `GameStateMachine`

### 2. Board Rendering

Clients render the board vectorially (SVG/XAML) based on the `GameModel`. No raster rendering on server.

- **Design:** `systems/board-rendering.md`
- **Impl:** `BoardSvgGenerator.cs` (Blazor), `BoardContainer.tsx` (React)

### 3. Data Access

Abstracted via `IGamePersistence` to support seamless switching between SQLite (Local) and CosmosDB/SQL (Azure).

- **Design:** `systems/save-load.md`
- **Impl:** `GamePersistenceService.cs`, `CatanDbContext`

## Current Initiatives

### TypeScript/React Port

A major effort is underway to port the Blazor `WebUI` to a modern **React 19 + Next.js 15** stack.

- **Goal:** Parity with Blazor client + improved maintainability.
- **Progress:** Project structure defined, basic components scaffolded.
- **Ref:** `ui/react/typescript-porting-design.md` , `ui/react/ts-port-impl-plan.md`

### Mobile & Touch Optimization

Continuous layout adjustments for mobile devices (iPad/Phone) using CSS media queries.

- **Ref:** `ui/react/responsive-design.md`

## Design Document Hierarchy

The `.design` directory is organized as follows:

- **`projects/`**: Specifics for CLI, Desktop, GameService, Shared, WebUI.
- **`systems/`**: Cross-cutting concerns (DB, API, Rendering, Messaging).
- **`ui/`**: Visual design, UX patterns, and specific component designs.
- **`reviews/`**: AI architecture reviews and feedback.

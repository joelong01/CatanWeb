# Gemini As-Built Documentation

This directory contains the output of the full system audit performed by Gemini on Jan 30, 2026.

## Core Documentation

- **[Message & State Flow](message-flow.md)**  
  The definitive guide to the `GameStateMachine`, `GameState` enum, and how messages flow between React, REST, and SignalR.

- **[React Architecture](react-architecture.md)**  
  Documentation of the `react-ui` folder detailed structure, technology stack, and component hierarchy.

- **[API Reference](api-reference.md)**  
  Complete list of REST endpoints (`/api/game/...`) and SignalR Hub methods.

- **[Game Rules Summary](game-rules-summary.md)**  
  A concise summary of the realized game rules (Hybrid model).

- **[Known Issues](known-issues.md)**  
  Discrepancies and TODOs identified during the audit.

## Systems & Infrastructure

- **[Database Schema](systems/database-schema.md)**  
  Detailed breakdown of `CatanDbContext`, entities, and the Hybrid Document-Relational pattern.

- **[Game Service Internals](systems/game-service-internals.md)**  
  Architecture of the ASP.NET Core backend, AsyncCommandProcessor, and persistence strategy.

- **[Azure Infrastructure](systems/azure-infrastructure.md)**  
  Hosting model, deployment configuration (App Service + Azure SQL), and singleton constraints.

- **[Coordinates & Rendering](systems/coordinates-and-rendering.md)**  
  Explanation of the Cubic Coordinate system and the layered SVG/HTML rendering engine.

- **[Cli Tooling](systems/cli-tooling.md)**
  Documentation of the `./catan.ps1` script and its capabilities. 

- **[Testing Strategy](systems/testing.md)**  
  Validation approach using Replay Tests and Unit Tests.

- **[Recording & Stats](systems/recording-and-stats.md)**  
  Deep dive into the recording infrastructure for tests and lifetime player statistics.

- **[CSS & Theming](systems/css-theming.md)**  
  Design tokens, color systems, and responsiveness.

- **[Game Flow & Diagrams](systems/game-flow.md)**
  Visual and logical flow of game states and initialization.

## Features

- **[Variants & House Rules](features/variants-and-house-rules.md)**  
  Documentation of the "Grief Dodgy" mode, Board Balancing, and 5-6 Player Supplemental rules.


## Operational Guides

- **[Troubleshooting Guide](troubleshooting.md)**
  Solutions for SSL errors, port conflicts, and database locks.


## Operational Guides

- **[Troubleshooting Guide](troubleshooting.md)**
  Solutions for SSL errors, port conflicts, and database locks.


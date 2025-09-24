# Moorestech Project Overview

## Project Purpose
Moorestech is an industrial/factory automation game written in Unity with both server and client components. It's a large-scale project with complex crafting, energy systems, and machinery mechanics.

## Tech Stack
- **Platform**: Unity (C#)
- **Architecture**: Client-Server architecture (both in Unity)
- **Testing**: Unity Test Framework (TDD for server-side)
- **Dependencies**: 
  - UniTask (async operations)
  - UniRx (reactive programming)
  - Addressables (asset management)
  - MCP (Model Context Protocol) for Unity integration
  - NuGetForUnity for package management

## Project Structure
- `/moorestech_server/` - Server-side Unity project
- `/moorestech_client/` - Client-side Unity project
- `/docs/` - Documentation files
- `/testServers/` - Test server configurations
- `/VanillaSchema/` - Game schema definitions (subtree)
- `/tools/` - Development tools
- `/.serena/` - Serena MCP server memory

## Key Systems
- Block system (machines, conveyors, etc.)
- Energy system (electric poles, generators, consumers)
- Crafting system (recipes, craft trees)
- Inventory management
- Train/rail system
- Gear power transmission system
- Fluid handling system
- Challenge/progression system
- Save/load system (JSON-based)

## Development Workflow
- Server-side uses TDD (Test-Driven Development)
- Client-side focuses on UI and rendering
- Both projects share the same Unity project structure
- Uses MCP tools for compilation and testing
- Git branching with feature branches
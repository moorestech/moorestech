# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Mooresmaster is a Roslyn-based C# Source Generator that generates type-safe C# data classes and loader code from YAML/JSON schema files. It targets game developers and data-driven applications who need compile-time type safety for data definitions.

## Build Commands

```bash
# Build generator (release)
dotnet build mooresmaster.Generator/ -c release

# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~ArrayIndexTest"

# Pack NuGet package
dotnet pack mooresmaster.Generator/
```

## Project Structure

- **mooresmaster.Generator/** - Main source generator (netstandard2.0, published to NuGet)
- **mooresmaster.Tests/** - XUnit test suite (net8.0)
- **mooresmaster.SandBox/** - Sample application demonstrating usage
- **memory-bank/** - AI assistant context documents (Japanese)

## Architecture

### Processing Pipeline

```
YAML schema files (.yml)
    ↓ YamlParser
JSON tokens
    ↓ JsonSchemaParser
Schema objects (ISchema)
    ↓ SemanticsGenerator
Semantic model
    ↓ NameResolver
Resolved definitions
    ↓ CodeGenerator + LoaderGenerator
Generated C# code (.g.cs)
```

### Key Components in mooresmaster.Generator/

| Directory | Purpose |
|-----------|---------|
| `Yaml/` | YAML to JSON conversion |
| `Json/` | JSON tokenizer and parser |
| `JsonSchema/` | Schema parsing, ISchema types |
| `Semantic/` | Semantic analysis |
| `Analyze/` | Validation (naming, interface scopes) |
| `NameResolve/` | Name collision resolution |
| `Definitions/` | Type definition generation |
| `CodeGenerate/` | C# class code generation |
| `LoaderGenerate/` | Data loader code generation |
| `YamlDotNet/` | Embedded YAML library (avoid external dependencies) |

### Entry Point

`MooresmasterSourceGenerator.cs` implements `IIncrementalGenerator` and orchestrates the entire pipeline.

## Schema Format

Schemas are YAML files defining data structures. Key features:

- **Types**: string, integer, number, boolean, uuid, vector2/3/4, vector2Int/3Int, object, array
- **`defineInterface`**: Creates polymorphic interfaces for type switching
- **`switch`**: Path-based conditional type selection
- **`index`**: Array indexing attribute for generated lookup methods

## Testing

Tests are organized by feature in `mooresmaster.Tests/`:
- `ArrayIndexTests/` - Array indexing functionality
- `DefineInterfaceTests/` - Interface polymorphism
- `OptionalTests/` - Optional field handling
- `SwitchPathTests/` - Switch/case path handling
- `AnalyzerTests/` - Schema validation

Each test directory contains corresponding YAML schema fixtures.

## Generated Output Files

- `mooresmaster.loader.g.cs` - Main loader functions
- `mooresmaster.loader.BuiltinLoader.g.cs` - Built-in type loaders
- `mooresmaster.loader.exception.g.cs` - Custom exceptions

## Git Subtree

The `mooresmaster.SandBox/schema/` directory is a git subtree from VanillaSchema:

```bash
# Pull updates from VanillaSchema
git subtree pull --prefix=mooresmaster.SandBox/schema schema main

# Push changes to VanillaSchema
git subtree push --prefix=mooresmaster.SandBox/schema schema main
```

## Technical Notes

- YamlDotNet is embedded in the source to avoid dependency conflicts with consuming projects
- Generator targets netstandard2.0 for broad compatibility
- Errors are reported as compiler diagnostics via Roslyn's diagnostic API

# Moorestech Agent Guide

## Core Workflow
- Follow direct system, developer, and user instructions in that order of priority.
- Optimise for readable, maintainable C# with small focused methods; use `#region` for local helper functions when methods grow large.
- Assume engine-provided dependencies are non-null unless interacting with optional or external data.
- Never create `.meta` files, and avoid unnecessary `try-catch` blocks.
- Reference existing documentation (`docs/ServerGuide.md`, `docs/ClientGuide.md`, `docs/ProtocolImplementationGuide.md`) when modifying related systems.

### Build & Validation Expectations
- Whenever you modify any C# code (client or server), run the relevant `dotnet build` command before committing to catch missing `using` directives or other compiler errors. For example:
  - `dotnet build moorestech_server/Assets/Scripts/Moorestech.Server.csproj`
  - `dotnet build moorestech_client/Assets/Scripts/Moorestech.Client.csproj`
- Treat build failures as blocking; resolve the error locally before requesting review or opening a PR.
- Surface the exact command output in the final summary when a build fails due to environment limitations.

## Testing Expectations
- Unity edit-mode tests cannot be executed inside this container. Document skipped runs with a warning emoji and the command name.

-## Train System Notes
- Train system implementation and testing guidance now lives in `docs/train/TrainSystemNotes.md`. Review that document alongside the `docs/train/README.md` index before updating rail logic or specs.

## Pull Requests
- Summarise behavioural changes clearly and list any skipped tests.
- Do not open a PR without first committing the relevant changes.

**End of Guide**
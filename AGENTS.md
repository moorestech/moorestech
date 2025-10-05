# Moorestech Agent Guide

## Core Workflow
- Follow direct system, developer, and user instructions in that order of priority.
- Optimise for readable, maintainable C# with small focused methods; use `#region` for local helper functions when methods grow large.
- Assume engine-provided dependencies are non-null unless interacting with optional or external data.
- Never create `.meta` files, and avoid unnecessary `try-catch` blocks.
- Reference existing documentation (`docs/ServerGuide.md`, `docs/ClientGuide.md`, `docs/ProtocolImplementationGuide.md`) when modifying related systems.

## Testing Expectations
- Unity edit-mode tests cannot be executed inside this container. Document skipped runs with a warning emoji and the command name.

## Train System Notes
- Use `TrainTestHelper.CreateEnvironment()` to bootstrap deterministic train tests; it wires world blocks, rail graph, and dependency injection.
- Prefer helper utilities such as `TrainTestHelper.PlaceRail(...)` and block IDs from `Tests.Module.TestMod.ForUnitTestModBlockId` to build rails and stations.
- When verifying docking behaviour, rely on docking handle references (e.g., `DockingHandle`, `TrainDockingHandle`) rather than cached `TrainUnit` state.
- Advance simulations with fixed time steps via helper update methods and guard loops with explicit failure conditions.
- Reset singleton datastores (e.g., `RailGraphDatastore.ResetInstance()`) when recreating environments across save/load scenarios.
- Remember that every rail segment is represented by a pair of directional nodes (`front` and `back`). These names indicate forward and reverse traversal helpers, not literal sides of a block.
- Do not rename existing scripts or methods that use `front`/`back` terminology; instead, document behaviour inline when touching the code.
- Pathing or validation logic that touches both directions should access each node explicitly (`rail.front`, `rail.back`) so the intended flow remains obvious to future readers.


## Pull Requests
- Summarise behavioural changes clearly and list any skipped tests.
- Do not open a PR without first committing the relevant changes.

**End of Guide**
# Research & Design Decisions: Gear Overload Destruction

---
**Purpose**: Capture discovery findings, architectural investigations, and rationale that inform the technical design.

**Usage**:
- Log research activities and outcomes during the discovery phase.
- Document design decision trade-offs that are too detailed for `design.md`.
- Provide references and evidence for future audits or reuse.
---

## Summary
- **Feature**: `gear-overload-destruction`
- **Discovery Scope**: Complex Integration (Modifies Core Gear Logic)
- **Key Findings**:
  - `GearEnergyTransformer` is the base class for all gear-related blocks.
  - `SimpleGearService` manages the state, but `GearEnergyTransformer` holds the `CurrentRpm` and `CurrentTorque` properties.
  - `VanillaIBlockTemplates` constructs templates, and templates construct blocks. Dependency Injection of `IBlockRemover` needs to flow through: `DI Container` -> `VanillaIBlockTemplates` -> `BlockTemplate` -> `BlockComponent` (`GearEnergyTransformer`).
  - `blocks.yaml` needs schema extension, which is handled by `SourceGenerator`.

## Research Log

### Existing Code Structure
- **Context**: Understanding where to implement the destruction logic.
- **Sources Consulted**: `GearEnergyTransformer.cs`, `VanillaGearTemplate.cs`, `VanillaIBlockTemplates.cs`, `WorldBlockDatastore.cs`.
- **Findings**:
  - `GearEnergyTransformer` is in `Game.Block.Blocks.Gear`.
  - It implements `IGearEnergyTransformer`.
  - It currently does not have an update loop (`Update()`).
  - `VanillaIBlockTemplates` is manually constructing templates in its constructor.
  - `WorldBlockDatastore` has a `RemoveBlock` method (via `IWorldBlockDatastore` which I assumed exists, need to verify exact method name). *Correction*: `WorldBlockDatastore` implements `IWorldBlockDatastore`. I need to verify the remove method on `IWorldBlockDatastore`.

### Destruction Mechanism
- **Context**: How to remove a block.
- **Sources Consulted**: `WorldBlockDatastore.cs` (partial read).
- **Findings**:
  - Need to confirm the exact method signature for removing a block in `IWorldBlockDatastore`.
  - `IBlockRemover` should wrap this to provide a cleaner interface and handle the "Reason" (Broken vs Manual).

### Parameter Loading
- **Context**: How to get overload parameters.
- **Sources Consulted**: `GearBlockParam` (inferred from `VanillaGearTemplate.cs`).
- **Findings**:
  - Parameters are cast from `BlockMasterElement.BlockParam`.
  - New parameters (`OverloadMaxRpm`, etc.) will be available in the generated `BlockParam` classes after `blocks.yaml` update.

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| **Local Update Loop** | Add `IUpdatableBlockComponent` to `GearEnergyTransformer` | Simple, self-contained logic per block. | Performance overhead if thousands of gears exist. | Chosen approach per requirements. |
| **Central Manager** | A system iterating over all gears to check overload | Better performance potential. | More complex state management. | |

## Design Decisions

### Decision: Destruction Logic Location
- **Context**: Where to calculate probability and trigger destruction.
- **Alternatives Considered**:
  1. `GearNetworkDatastore`: Centralized but might violate "block self-management" principle.
  2. `GearEnergyTransformer`: Decentralized, aligns with "GearEnergyTransformer monitors itself" requirement.
- **Selected Approach**: `GearEnergyTransformer`.
- **Rationale**: Explicitly requested in requirements. Encapsulates logic within the block component.
- **Trade-offs**: Potential CPU cost for many gears, but `destructionCheckInterval` mitigates this.

### Decision: IBlockRemover Injection
- **Context**: `GearEnergyTransformer` needs to call `WorldBlockDatastore.Remove`.
- **Selected Approach**: Inject `IBlockRemover` (wrapper around `WorldBlockDatastore`) into `VanillaIBlockTemplates`, then pass down to `GearEnergyTransformer`.
- **Rationale**: Follows DI pattern, decoupling `GearEnergyTransformer` from direct `WorldBlockDatastore` dependency and allowing mock implementation for testing.

## Risks & Mitigations
- **Risk**: Performance drop due to many updatable blocks.
  - **Mitigation**: Use `destructionCheckInterval` to stagger checks.
- **Risk**: `WorldBlockDatastore.Remove` might be heavy or unsafe during iteration.
  - **Mitigation**: Ensure removal is safe (e.g., schedule for next frame or use safe collection modification). `IBlockRemover` implementation must handle this.

## References
- `moorestech_server/Assets/Scripts/Game.Block/Blocks/Gear/GearEnergyTransformerComponent.cs`
- `moorestech_server/Assets/Scripts/Game.Block/Factory/VanillaIBlockTemplates.cs`



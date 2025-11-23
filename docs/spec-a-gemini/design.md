# Technical Design Document: Gear Overload Destruction

---
**Purpose**: Provide sufficient detail to ensure implementation consistency across different implementers, preventing interpretation drift.

**Approach**:
- Include essential sections that directly inform implementation decisions
- Omit optional sections unless critical to preventing implementation errors
- Match detail level to feature complexity
- Use diagrams and tables over lengthy prose

**Warning**: Approaching 1000 lines indicates excessive feature complexity that may require design simplification.
---

## Overview
**Purpose**: This feature introduces a mechanic where gear system blocks (gears, shafts) break when subjected to excessive RPM or Torque.
**Users**: Players designing factory systems will need to manage load to prevent equipment failure.
**Impact**: Adds a failure state to the gear network, requiring more strategic planning and maintenance.

### Goals
- Implement `IBlockRemover` to handle block destruction with specific reasons (`Broken`, `ManualRemove`).
- Extend `blocks.yaml` schema to support overload parameters (`maxRpm`, `maxTorque`, destruction probability).
- Implement destruction logic within `GearEnergyTransformer` that monitors RPM/Torque and destroys the block probabilistically upon overload.
- Inject dependencies properly through the `VanillaIBlockTemplates` chain.

### Non-Goals
- Visual effects for destruction (client-side only, outside server scope for now, though state change is propagated).
- Repair mechanics (out of scope, only destruction).

## Architecture

### Existing Architecture Analysis
- **Master Data**: `blocks.yaml` defines block properties. This will be extended.
- **Block System**: Blocks are composed of components. `GearEnergyTransformer` is a component logic base.
- **Factory**: `VanillaIBlockTemplates` creates block instances.
- **DataStore**: `WorldBlockDatastore` manages block existence.

### Architecture Pattern & Boundary Map

```mermaid
graph TB
    subgraph "Core.Master"
        Schema[blocks.yaml] --> SourceGen[SourceGenerator]
        SourceGen --> BlockParam[BlockParam Class]
    end

    subgraph "Game.Block"
        BlockFactory[BlockFactory] --> Template[VanillaIBlockTemplates]
        Template --> GearTemplate[VanillaGearTemplate]
        GearTemplate --> GearComp[GearEnergyTransformer]
        
        RemoverInterface[IBlockRemover] <|-- RemoverImpl[BlockRemover]
        RemoverImpl --> Datastore[WorldBlockDatastore]
        
        GearComp --> RemoverInterface
        GearComp --> BlockParam
    end

    subgraph "DI Container"
        Boot[MoorestechServerDIContainerGenerator] --> RemoverImpl
        Boot --> Template
    end
```

**Architecture Integration**:
- **Pattern**: Dependency Injection. `IBlockRemover` is injected down the factory chain.
- **Boundaries**: `GearEnergyTransformer` logic remains self-contained but now depends on `IBlockRemover`.
- **Steering Compliance**: Follows the existing Component/Entity pattern.

### Technology Stack

| Layer | Choice / Version | Role in Feature | Notes |
|-------|------------------|-----------------|-------|
| Backend | C# (.NET) | Core Logic | Strict typing required. |
| Data | YAML / SourceGen | Configuration | Schema definition. |
| Runtime | Unity / Server | Execution Env | |

## System Flows

### Destruction Logic Flow

```mermaid
sequenceDiagram
    participant Loop as Game Loop
    participant Gear as GearEnergyTransformer
    participant Remover as IBlockRemover
    participant Store as WorldBlockDatastore

    Loop->>Gear: Update() (Interval Check)
    Gear->>Gear: Check RPM & Torque
    alt Overload Detected
        Gear->>Gear: Calculate Probability
        Gear->>Gear: Roll Dice
        alt Destroyed
            Gear->>Remover: Remove(this, Reason.Broken)
            Remover->>Store: RemoveBlock(id)
        end
    end
```

## Requirements Traceability

| Requirement | Summary | Components | Interfaces | Flows |
|-------------|---------|------------|------------|-------|
| 1.1, 1.2 | `IBlockRemover` & Enum | `IBlockRemover`, `BlockRemoveReason` | `Remove(IBlock, BlockRemoveReason)` | |
| 1.3, 1.4 | Remover Impl & DI | `BlockRemover`, `MoorestechServerDIContainerGenerator` | | |
| 2.1, 2.2 | Schema Extension | `blocks.yaml`, `BlockParam` | | |
| 3.1, 3.2 | Monitor Logic | `GearEnergyTransformer` | `OnUpdate` (internal) | Destruction Logic Flow |
| 3.3, 3.4 | Probability & Destruction | `GearEnergyTransformer` | | |
| 4.1, 4.2 | Template DI | `VanillaIBlockTemplates` | Constructor | |
| 4.3 | Component Usage | `GearEnergyTransformer` | | |

## Components and Interfaces

### Component Summary
| Component | Domain/Layer | Intent | Req Coverage | Key Dependencies (P0/P1) | Contracts |
|-----------|--------------|--------|--------------|--------------------------|-----------|
| `IBlockRemover` | Game.Block | Abstract block removal | 1.1 - 1.4 | `IWorldBlockDatastore` (P0) | Service |
| `GearEnergyTransformer` | Game.Block | Core gear logic & destruction | 3.1 - 3.4, 4.3 | `IBlockRemover` (P0), `BlockParam` (P0) | |
| `VanillaIBlockTemplates` | Game.Block | Block factory configuration | 4.1, 4.2 | `IBlockRemover` (P0) | |
| `blocks.yaml` | Core.Master | Schema definition | 2.1, 2.2 | | |

### Game.Block

#### IBlockRemover / BlockRemover

| Field | Detail |
|-------|--------|
| Intent | Handles block removal with a reason context. |
| Requirements | 1.1, 1.2, 1.3, 1.4 |

**Responsibilities & Constraints**
- Wraps `WorldBlockDatastore.Remove`.
- Logging or event firing based on `BlockRemoveReason`.

**Dependencies**
- Outbound: `IWorldBlockDatastore` (P0)

**Contracts**: Service [x]

##### Service Interface
```csharp
public enum BlockRemoveReason
{
    Broken,
    ManualRemove
}

public interface IBlockRemover
{
    void Remove(BlockInstanceId blockInstanceId, BlockRemoveReason reason);
}
```

#### GearEnergyTransformer

| Field | Detail |
|-------|--------|
| Intent | Monitors overload and triggers destruction. |
| Requirements | 3.1, 3.2, 3.3, 3.4 |

**Responsibilities & Constraints**
- Periodically checks `CurrentRpm` and `CurrentTorque`.
- Calculates destruction probability.
- Calls `IBlockRemover` on failure.

**Dependencies**
- Inbound: `IBlockRemover` (injected via constructor).
- Data: `OverloadMaxRpm`, `OverloadMaxTorque`, etc. from `BlockParam`.

**Implementation Notes**
- Must implement `IUpdatableBlockComponent` (or similar mechanism) to run periodic checks.
- `baseDestructionProbability` is the base chance per check interval.
- Logic:
    - `rpmRatio = CurrentRpm / MaxRpm` (if > 1)
    - `torqueRatio = CurrentTorque / MaxTorque` (if > 1)
    - `totalRatio = rpmRatio * torqueRatio`
    - `chance = BaseChance * totalRatio`

#### VanillaIBlockTemplates

| Field | Detail |
|-------|--------|
| Intent | Factory class that injects dependencies into templates. |
| Requirements | 4.1, 4.2 |

**Responsibilities & Constraints**
- Accepts `IBlockRemover` in constructor.
- Passes it to `VanillaGearTemplate`, `VanillaShaftTemplate`, etc.

## Data Models

### Schema Extension (blocks.yaml)

**Structure Definition**:
New fields added to gear-related block definitions:

```yaml
# Example fragment
gear:
  overloadMaxRpm: float
  overloadMaxTorque: float
  destructionCheckInterval: float # Seconds
  baseDestructionProbability: float # 0.0 - 1.0
```

## Error Handling

### Error Strategy
- **Safe Removal**: If `IBlockRemover` fails (e.g., block already removed), catch and log warning, do not crash server.
- **Missing Params**: If `overloadMaxRpm` is 0 or missing, assume no destruction (infinite durability).

## Testing Strategy

- **Unit Tests**:
  - `GearEnergyTransformerTest`: Mock `IBlockRemover`, simulate high RPM/Torque, verify `Remove` is called (use statistical test or deterministic seed if possible, or just check logic path).
  - `BlockRemoverTest`: Verify it calls `WorldBlockDatastore`.
- **Integration Tests**:
  - Create a small network, apply power > limit, wait, verify block disappears.



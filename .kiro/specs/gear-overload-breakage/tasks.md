# Implementation Plan

## Phase 1: Infrastructure & Schema (Sequential)
- [x] 1.1 Define `IBlockRemover` and `BlockRemoveReason` Enum
  - Create interface and enum in `Game.Block.Interface`
  - _Requirements: 1.1, 1.2_

- [x] 1.2 Implement `BlockRemover`
  - Implement `BlockRemover` class wrapping `IWorldBlockDatastore.RemoveBlock`
  - Register to DI container in `MoorestechServerDIContainerGenerator`
  - _Requirements: 1.3, 1.4_

- [x] 1.3 Extend `blocks.yaml` Schema
  - Add `overloadMaxRpm`, `overloadMaxTorque`, `destructionCheckInterval`, `baseDestructionProbability` to gear block definitions
  - Verify `SourceGenerator` output (run build/generation command)
  - _Requirements: 2.1, 2.2_

## Phase 2: Core Logic Implementation (Parallel)
- [x] 2.1 Update `GearEnergyTransformer` Logic (P)
  - Inject `IBlockRemover` via constructor
  - Implement `IUpdatableBlockComponent` (or verify existing update mechanism)
  - Add `Update()` logic to check overload and calculate destruction probability
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 4.3_

- [x] 2.2 Update Templates and DI Chain (P)
  - Update `VanillaIBlockTemplates` constructor to accept `IBlockRemover`
  - Pass `IBlockRemover` to `VanillaGearTemplate`, `VanillaShaftTemplate`, etc.
  - Update `VanillaGearTemplate` etc. to pass `IBlockRemover` to `GearEnergyTransformer`
  - _Requirements: 4.1, 4.2_

## Phase 3: Testing & Validation (Sequential)
- [x] 3.1 Unit Tests for Destruction Logic
  - Create `GearEnergyTransformerTest`
  - Mock `IBlockRemover` and verify `Remove` call on overload
  - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 3.2 Integration Verification
  - Build and run server
  - Verify blocks break under load as expected in a test scenario


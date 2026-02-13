---
name: train-rail-save-load
description: Implement and validate save/load for train and rail systems based on the current rail-segment-centric architecture. Use when changing train snapshots, rail segment persistence, world load order, docking restoration, or train/rail save data schema and tests.
---

# Train/Rail Save Load

## Overview

Use this skill for developers implementing train/rail save-load changes under the current architecture.

## Current Architecture (Must Preserve)

- Rail connectivity is persisted as `railSegments` and restored by `RailGraphSaveLoadService`.
- Train state is persisted as `trainUnits` and restored by `TrainSaveLoadService`.
- Rail block load and rail segment restore are separate steps.
- Station/Cargo block templates in load path restore/register rail components, but segment connectivity restoration is delegated to rail segment restore.

## Save Data Model

- World payload includes both:
  - `trainUnits` (`TrainUnitSaveData`)
  - `railSegments` (`RailSegmentSaveData`)
- `RailSegmentSaveData` stores:
  - `A` / `B` (`ConnectionDestination`)
  - `Length`
  - `RailTypeGuid`
  - `IsDrawable`
- `ConnectionDestination` is the serializable 1:1 identifier for a concrete `IRailNode` (`blockPosition`, `componentIndex`, `isFront`).
- `TrainUnitSaveData` stores:
  - `railPositionSaveData`
  - runtime flags/speeds
  - cars
  - diagram

## Load Order Contract (Must Preserve)

In `WorldLoaderFromJson.Load(...)`, keep this order:
1. Load world blocks.
2. Restore rail segments via `RailGraphSaveLoadService.RestoreRailSegments(...)`.
3. Load common world/player/entity settings.
4. Restore train units via `TrainSaveLoadService.RestoreTrainStates(...)`.
5. Rebuild docking links via `TrainDockingStateRestorer.RestoreDockingState()`.

Reason:
- Train rail position restore resolves `ConnectionDestination` through rail graph provider.
- This resolution must remain 1:1 with restored `IRailNode` instances; if ambiguity appears, treat it as data/restore-order inconsistency.
- If rail segments are not restored before train restore, route/node resolution may fail or degrade.

## Rail Segment Restore Rules

- Skip null segment entries.
- For drawable segments, validate/repair saved length against expected Bezier-based length.
- Restore through datastore API (`TryRestoreRailSegment`) using `ConnectionDestination` endpoints.
- Do not re-introduce legacy "block-level connection restore" behavior.

## Train Restore Rules

- `TrainSaveLoadService.RestoreTrainStates(...)` resets existing registered trains before restore.
- `TrainUnit.RestoreFromSaveData(...)` resolves rail position from saved snapshot and reconstructs cars/diagram.
- Docking state is re-established after train restoration by dedicated restorer; do not inline this into per-train restore path.

## Implementation Checklist

1. When adding train/rail save fields, update both serializer and restore path.
2. Keep `railSegments` and `trainUnits` compatible with current load order.
3. Ensure `ConnectionDestination`-based node resolution remains valid.
4. Keep segment-type and drawable flags preserved across save/load.
5. Add regression tests for both:
   - rail graph consistency after restore
   - train state consistency after restore

## Validation Checklist

1. Save/load roundtrip preserves rail connectivity and lengths.
2. Train position/speed/auto-run/diagram are stable across reload.
3. Missing or invalid references fail safely without corrupting global train state.
4. Docking links are rebuilt correctly after restore.

## Key Files

- `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/AssembleSaveJsonText.cs`
- `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldLoaderFromJson.cs`
- `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfoV1.cs`
- `moorestech_server/Assets/Scripts/Game.Train/SaveLoad/RailGraphSaveLoadService.cs`
- `moorestech_server/Assets/Scripts/Game.Train/SaveLoad/RailGraphSaveData.cs`
- `moorestech_server/Assets/Scripts/Game.Train/SaveLoad/TrainSaveLoadService.cs`
- `moorestech_server/Assets/Scripts/Game.Train/Unit/TrainDockingStateRestorer.cs`
- `moorestech_server/Assets/Scripts/Game.Train/RailPositions/RailPositionFactory.cs`
- `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaTrainStationTemplate.cs`
- `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaTrainCargoTemplate.cs`

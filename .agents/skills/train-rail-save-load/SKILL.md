---
name: train-rail-save-load
description: Implement and validate save/load for train and rail systems under the current rail-segment-centric architecture and TrainUnit snapshot-sync model. Use when changing train save data, rail persistence, load order, docking restoration, or persistence tests.
---

# Train/Rail Save Load

## Overview
Use this skill for train/rail persistence changes under the current runtime architecture.

## Reuse-First Rule
- Before adding save/load helpers, search existing train/rail persistence code first.
- Prefer existing `Game.Train` save/load components over parallel implementations.
- If duplication is unavoidable, record `WHY_NEW_IMPLEMENTATION` in code/PR notes.
- Recommended pre-check:
  - `rg --line-number "SaveLoad|Restore|ConnectionDestination|RailPosition|TrainUnitSaveData" moorestech_server/Assets/Scripts`

## Current Architecture (Must Preserve)
- Rail connectivity persists as `railSegments` and restores via `RailGraphSaveLoadService`.
- Train state persists as `trainUnits` and restores via `TrainSaveLoadService`.
- Rail block placement/load and segment connectivity restoration are separate steps.

## Snapshot Boundary Contract (Must Preserve)
- Runtime train sync uses:
  - per-unit snapshot event (`va:event:trainUnitSnapshot`)
  - full snapshot API (`va:getTrainUnitSnapshots`)
- If TrainUnit/TrainCar state shape changes, update save/load and snapshot serialization together.
- Do not persist transport-only runtime values (sequence IDs, future buffers, queue internals).

## Save Data Model
- `railSegments` (`RailSegmentSaveData`) contains:
  - `A` / `B` (`ConnectionDestination`)
  - `Length`
  - `RailTypeGuid`
  - `IsDrawable`
- `trainUnits` (`TrainUnitSaveData`) contains rail position snapshot + train state + cars + diagram.
- `TrainCarSaveData` includes `TrainCarMasterId`, `IsFacingForward`, and stateful car data.

## Load Order Contract (Must Preserve)
1. Load world blocks.
2. Restore rail segments.
3. Restore common world/player/entity state.
4. Restore train units.
5. Restore docking links.

Reason: train rail-position restoration resolves `ConnectionDestination` via rail graph provider; restore order must keep this mapping valid.

## Rail Segment Restore Rules
- Skip null segment entries safely.
- For drawable segments, keep current length validation/correction behavior.
- Restore through datastore APIs (`TryRestoreRailSegment`), not ad-hoc graph mutation.

## Train Restore Rules
- Reset previously registered runtime train state before restore.
- Resolve rail position through provider/factory from saved snapshot data.
- Restore TrainUnit and TrainCar as one consistent unit.
- Rebuild docking state after unit restore pass.
- Fail safely per unit on invalid references; avoid corrupting global restore state.

## Implementation Checklist
1. Update serializer and restore path together for new fields.
2. Update snapshot serialization/apply when state affects runtime sync.
3. Keep `railSegments` and `trainUnits` compatible with load order.
4. Keep `ConnectionDestination` resolution contract valid.
5. Add regression tests for rail reconstruction and train restore consistency.
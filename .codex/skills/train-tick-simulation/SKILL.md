---
name: train-tick-simulation
description: Define and enforce deterministic train tick behavior between server and client under unified TickUnifiedId ordering, including hash gating, diff-bundle simulation triggers, and coexistence with per-unit TrainUnit snapshot events. Use when implementing or reviewing train tick order, hash checks, or tick-aligned train/rail protocol payloads.
---

# Train Tick Simulation

## Overview

Use this skill to keep server/client simulation aligned by unified chronological ordering and the current snapshot-sync policy.

## Reuse-First Rule

- Before adding tick/event helper logic, search existing train implementations first.
- Prefer shared `Game.Train` logic over per-handler duplicate implementations.
- If duplication is unavoidable, document `WHY_NEW_IMPLEMENTATION` in code/PR notes.
- Recommended pre-check:
  - `rg --line-number "TickUnifiedId|TickSequenceId|Overlap|CreateIndex|HasOverlap" moorestech_client/Assets/Scripts moorestech_server/Assets/Scripts`

## Target Tick Flow (Current)

Server order:
1. Build and emit hash state for tick `n`.
2. Increment server tick to `n+1` and reset per-tick sequence counter.
3. Run per-train simulation (`TrainUnit.Update()`).
4. Emit tick-diff trigger for tick `n+1` (even when per-train diffs are empty).
5. `TrainUnitTickDiffBundleEventPacket` bundles hash(`n`) + diff(`n+1`) and broadcasts one event.

Server structural-train updates:
- TrainUnit/TrainCar composition changes are sent separately as `va:event:trainUnitSnapshot` per-unit upsert/delete events.
- These events share the same unified ordering keys (`ServerTick`, `TickSequenceId`).

Client order:
1. Flush queued events in strict `TickUnifiedId` order (`id + 1`).
2. Apply tick-diff bundle event at its buffered id:
   - apply per-train input diffs
   - run local train simulation (`unit.Update()`)
3. Run hash gate on the requested id and decide whether tick can advance.
4. If no hash exists at current id but future hash exists, allow tick advance to avoid deadlock on missing intermediate ids.

## Unified Ordering Contract

- `TickUnifiedId = ((ulong)ServerTick << 32) | TickSequenceId`.
- All train/rail events must carry `ServerTick` and `TickSequenceId`.
- The client must enqueue network events; do not apply immediately in handlers.
- `TickSequenceId` must be allocated only by `TrainUpdateService.NextTickSequenceId()`.

## TickDiffBundle Contract

- Event tag: `va:event:trainUnitTickDiffBundle`.
- Bundle payload includes:
  - `ServerTick`
  - `HashTickSequenceId`
  - `DiffTickSequenceId`
  - `UnitsHash`
  - `RailGraphHash`
  - `Diffs[]`
- `Diffs[]` item includes:
  - `TrainInstanceId`
  - `MasconLevelDiff`
  - `IsNowDockingSpeedZero`
  - `ApproachingNodeIdDiff`
- The server emits bundle events each tick as simulation trigger; `Diffs[]` may be empty.
- Hash may be dummy (`uint.MaxValue`) on non-broadcast hash ticks; client treats dummy as pass.
- `Diffs[]` is not a TrainCar composition transport. TrainUnit/TrainCar structure sync remains snapshot-based.

## Snapshot Event Coexistence Contract

- Event tag: `va:event:trainUnitSnapshot`.
- Payload is one TrainUnit upsert or deletion tombstone.
- Use this for place/attach/remove/split/merge/delete effects.
- Do not add new train-car-only incremental events unless architecture policy is explicitly changed.

## Workflow

1. Verify server order: `hash -> tick increment -> simulation -> diff bundle`.
2. Ensure every added train/rail event payload has `ServerTick` and `TickSequenceId`.
3. Ensure client handlers only enqueue to `TrainUnitFutureMessageBuffer`.
4. Verify `TickSequenceId` continuity from server emission points.
5. Add tests for:
   - unified id monotonic progression
   - hash gate progression
   - empty-diff bundle still triggers simulation
   - per-unit snapshot events coexisting correctly with tick-diff bundle ordering

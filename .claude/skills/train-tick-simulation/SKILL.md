---
name: train-tick-simulation
description: Define and enforce deterministic train tick-phase behavior between server and client, including hash gating and mascon diff handling. Use when implementing or reviewing train tick order, pre-sim/post-sim event phases, or tick-aligned protocol/event payloads.
---

# Train Tick Simulation

## Overview

Use this skill to keep server/client simulation aligned by unified chronological ordering.

## Target Tick Flow (Current)

Server order:
1. Emit hash event first.
2. Increment server tick.
3. Run per-train simulation.
4. Emit tick-bound diff bundle event (even when per-train diffs are empty).

Client order:
1. Flush queued events in strict `TickUnifiedId` order (`id + 1`).
2. Apply tick-diff bundle event for the target tick:
   - apply per-train input diffs
   - run local train simulation (`unit.Update()`)
3. Run hash gate on the same unified id and decide whether tick can advance.

## Unified Ordering Contract

- `TickUnifiedId = ((ulong)ServerTick << 32) | TickSequenceId`.
- All train/rail events must carry `ServerTick` and `TickSequenceId`.
- The client must enqueue network events; do not apply immediately in handlers.

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

## Workflow

1. Verify server order: `hash -> tick increment -> simulation -> diff bundle`.
2. Ensure every added train/rail event payload has `ServerTick` and `TickSequenceId`.
3. Ensure client handlers only enqueue to `TrainUnitFutureMessageBuffer`.
4. Verify `TickSequenceId` continuity from server emission points.
5. Add tests for:
   - unified id monotonic progression
   - hash gate progression
   - empty-diff bundle still triggers simulation

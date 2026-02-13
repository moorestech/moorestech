---
name: train-rail-event-implementation
description: Implement new train/rail network events with correct tick semantics and simulation phase placement. Use when adding or modifying server/client event packets, MessagePack payloads, or network handlers for train and rail systems, especially when deciding whether an event must include tick and whether it belongs to pre-sim or post-sim.
---

# Train/Rail Event Implementation

## Overview

Use this skill when creating new train/rail events so that server and client apply data at the correct tick and phase.

## Core Rules

1. Include tick in almost all new train/rail events.
2. Default to `post-sim`.
3. Use `pre-sim` only when the event data must affect simulation results of the same tick.

## Current Baseline (Must Preserve)

- Tick rate is fixed at `20Hz` (`GameUpdater.TicksPerSecond = 20`, `SecondsPerTick = 1d / 20`).
- Server update order in `TrainUpdateService.UpdateTrains()` is:
  1. hash notify (`_onHashEvent.OnNext(_executedTick)`)
  2. `_executedTick++`
  3. `trainUnit.Update()` for all trains
  4. `NotifyPreSimulationDiff(_executedTick)`
- Hash broadcast interval is effectively every tick under current constants.

## Tick Rule

- Tick is required when the event affects train/rail simulation state, cache consistency, hash validation timing, or snapshot ordering.
- Use `ServerTick` consistently as the tick field name in MessagePack payloads.

## Phase Decision Rule

Choose `pre-sim` only if all of the following are true:
- The value is directly used by `SimulateUpdate()` in the same tick.
- The value is computed as simulation input in or around server train update flow.
- Applying after simulation would cause deterministic mismatch for that tick.

Otherwise choose `post-sim`.

Current practical rule in this project:
- New train/rail events are generally `post-sim`.
- `pre-sim` is a special case (for example: `va:event:trainUnitPreSimulationDiff`).

Reason:
- Most events are produced from processing that happens after train simulation in the frame/update pipeline.
- These events should not retroactively affect the already-executed simulation step of the same tick.

## Existing Phase Examples

- `pre-sim`: `va:event:trainUnitPreSimulationDiff`
  - Payload: `TrainUnitPreSimulationDiffMessagePack { ServerTick, Diffs[] }`
  - `Diffs[]`: `TrainId`, `MasconLevelDiff`, `IsNowDockingSpeedZero`, `ApproachingNodeIdDiff`
  - Queued with `EnqueuePre(...)`
  - Applied by `FlushPreBySimulatedTick()` before `SimulateUpdate()`
- `post-sim`: `va:event:trainUnitCreated`
  - Payload includes `ServerTick`
  - Queued with `EnqueuePost(...)`
  - Applied by `FlushPostBySimulatedTick()` after `SimulateUpdate()`

## Client Tick Apply Order (Must Preserve)

At each simulated tick in `TrainUnitClientSimulator`:
1. `AdvanceTick()`
2. `FlushPreBySimulatedTick()`
3. Optional simulation skip consume
4. `SimulateUpdate()`
5. `FlushPostBySimulatedTick()`
6. hash gate check via `CanAdvanceTick(currentTick)`

## Hash Mismatch Resync (Must Preserve)

- On mismatch, request train snapshots and apply baseline tick.
- Keep queue cleanup semantics:
  - `SetSnapshotBaselineTick(serverTick)`
  - `DiscardUpToTick(serverTick)`
  - `RecordSnapshotAppliedTick(serverTick)`

## Determinism and Randomness

- Train motion step is deterministic (`TrainDistanceSimulator.Step` + `Math.Truncate` distance conversion).
- Current train route selection in simulation path does not depend on random branching.
- `System.Random` usage for train car instance id generation is out-of-band from tick simulation behavior.

## Implementation Checklist

1. Define or update MessagePack payload with tick field.
2. Add or update server event packet and event tag.
3. On client, choose queue API:
   - `EnqueuePre(serverTick, ...)`
   - `EnqueuePost(serverTick, ...)`
4. Ensure handler does not apply immediately; enqueue for tick-based flush.
5. Verify hash mismatch and snapshot resync behavior is preserved.

## Validation Checklist

1. Same-tick ordering is correct (pre -> simulate -> post).
2. Event does not apply to past ticks.
3. Snapshot baseline handling does not double-apply event data.
4. Hash verification path remains stable.

## Key Files

- `moorestech_server/Assets/Scripts/Game.Train/Unit/TrainUpdateService.cs`
- `moorestech_server/Assets/Scripts/Server.Event/EventReceive/TrainUnitPreSimulationDiffEventPacket.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/TrainUnitFutureMessageBuffer.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Unit/TrainUnitClientSimulator.cs`

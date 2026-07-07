---
name: train-rail-event-implementation
description: Implement and modify train/rail event tags, payload schemas, server emit paths, and client handlers under unified TickUnifiedId ordering and per-TrainUnit snapshot boundaries. Use when adding or changing `va:event:*` behavior.
---

# Train/Rail Event Implementation (Unified Tick + Snapshot Policy)

## Overview
Use this skill when changing train/rail event contracts and handler wiring under unified ordering.

## Reuse-First Rule
- Before adding new event-side helpers, search existing train/rail event implementations first.
- Prefer reusing domain logic in `Game.Train` over re-implementing logic in handlers.
- If duplication is unavoidable, record `WHY_NEW_IMPLEMENTATION` in code/PR notes.
- Recommended pre-check:
  - `rg --line-number "TickUnifiedId|NextTickSequenceId|TrainUnitSnapshot|TickDiffBundle" moorestech_client/Assets/Scripts moorestech_server/Assets/Scripts`

## Core Concepts

### 1) Unified ordering key
- `TickUnifiedId = ((ulong)ServerTick << 32) | TickSequenceId`.
- `TickSequenceId` is server-allocated and per-tick monotonic.

### 2) Unified event queue
- Client handlers enqueue events into `TrainUnitFutureMessageBuffer`.
- Buffered events apply by unified id order.

### 3) Snapshot-first train sync
- Structural TrainUnit/TrainCar changes use `va:event:trainUnitSnapshot` (upsert/delete).
- Full-unit resync uses `va:getTrainUnitSnapshots`.

### 4) TickDiffBundle role
- `va:event:trainUnitTickDiffBundle` transports hash + per-tick diffs and drives simulation timing.
- `Diffs[]` can be empty and still acts as simulation trigger.

## Emission Patterns

### A. Tick simulation path
1. Server emits hash state.
2. Server advances tick, resets sequence, runs simulation.
3. Server emits pre-sim diff signal.
4. Tick diff bundle packet broadcasts ordered hash/diff payload.

### B. Train structure change path
1. Domain/protocol mutates train state.
2. Server emits per-unit snapshot notify (`NotifySnapshot`/`NotifyDeleted`).
3. Snapshot packet broadcasts `va:event:trainUnitSnapshot`.

### C. Rail graph change path
- Rail node/connection packets allocate `TickSequenceId` and broadcast ordered diff events.

## Implementation Rules

### Server-side
1. Allocate sequence IDs only via `TrainUpdateService.NextTickSequenceId()`.
2. Every train/rail event carries `ServerTick` and sequence ID.
3. Train composition changes should prefer snapshot notify path over new ad-hoc car events.

### Client-side
1. Handlers enqueue events, do not apply immediately.
2. Snapshot handlers apply upsert/delete at buffered tick and reconcile train/car visuals.
3. TickDiffBundle handler preserves current semantics: pre-sim diff apply + local simulation update.

## Implementation Checklist
1. Choose correct path (tick bundle, snapshot, rail diff).
2. Keep ordering fields in payload and on apply path.
3. Keep stale-event protection for destructive operations.
4. If state shape changes, update packet mapping + client apply + tests.
5. Verify unified-id ordering behavior with relevant tests.
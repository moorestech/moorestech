---
name: train-rail-event-implementation
description: Implement and modify train/rail network events with unified chronological ordering and current snapshot-sync policy. Use when adding or changing server/client event packets, MessagePack payloads, or handlers for train and rail systems, including TrainUnit/TrainCar updates that must follow per-unit snapshot notification.
---

# Train/Rail Event Implementation (Unified Tick + Snapshot Policy)

## Overview

The current architecture uses one unified chronological stream keyed by `TickUnifiedId`:

- Tick simulation trigger: `va:event:trainUnitTickDiffBundle`
- TrainUnit/TrainCar structure sync: `va:event:trainUnitSnapshot` (single unit upsert/delete)
- Rail graph diffs: node/connection created/removed events

Do not introduce new TrainCar-only event packets for normal feature work. Train composition changes are synchronized by per-unit snapshot events.

## Reuse-First Rule

- Before creating new event-side helper logic, search existing train/rail implementations first.
- Prefer reusing `Game.Train` domain logic rather than re-implementing similar logic in event handlers.
- If a new duplicate helper is unavoidable, document `WHY_NEW_IMPLEMENTATION` in code/PR notes.
- Recommended pre-check:
  - `rg --line-number "TickUnifiedId|NextTickSequenceId|Overlap|CreateIndex|HasOverlap" moorestech_client/Assets/Scripts moorestech_server/Assets/Scripts`

## Core Concepts

### 1. TickUnifiedId
- A 64-bit value: `((ulong)ServerTick << 32) | TickSequenceId`.
- `ServerTick`: The cumulative game tick count (20Hz).
- `TickSequenceId`: A monotonic, continuous sequence number within a tick, allocated via `_trainUpdateService.NextTickSequenceId()`.

### 2. Unified Event Queue
- All events are enqueued into `TrainUnitFutureMessageBuffer` via `EnqueueEvent`.
- Events are wrapped in `ITrainTickBufferedEvent` which has a single `Apply()` method.
- The client simulator flushes in order and advances tick through hash gating when no next event is present.

### 3. Snapshot-First Train Sync
- Server-side TrainUnit/TrainCar structural changes (place/attach/remove/split/merge/delete) are synchronized through `ITrainUnitSnapshotNotifyEvent`.
- `TrainUnitSnapshotEventPacket` converts notify data into `TrainUnitSnapshotEventMessagePack` and broadcasts `va:event:trainUnitSnapshot`.
- The payload always represents one TrainUnit (`IsDeleted` tombstone or full bundle snapshot).
- Full all-unit snapshots are fetched via `GetTrainUnitSnapshotsProtocol` for handshake/resync.

### 4. TickDiffBundle Role
- `va:event:trainUnitTickDiffBundle` carries hash and per-tick diffs.
- Client apply behavior:
  - apply per-train pre-sim diffs (`MasconLevelDiff`, docking zero flag, approaching node diff)
  - run `Update()` for all client train units
- `Diffs[]` can be empty; event still acts as simulation trigger.

## Emission Patterns (Current)

### A. Tick simulation path
1. `TrainUpdateService` emits hash state for tick `n`.
2. Service increments tick to `n+1`, resets per-tick sequence counter, runs server simulation.
3. Service emits pre-simulation diff event for tick `n+1`.
4. `TrainUnitTickDiffBundleEventPacket` combines hash(`n`) + diff(`n+1`) and broadcasts bundle.

### B. Train structure change path
1. Domain/protocol mutates trains (e.g., place/attach/remove car).
2. Call `NotifySnapshot(train)` or `NotifyDeleted(trainId)` on `ITrainUnitSnapshotNotifyEvent`.
3. `TrainUnitSnapshotEventPacket` allocates `TickSequenceId` and broadcasts one-unit snapshot event.

### C. Rail graph change path
- Rail node/connection packets allocate `TickSequenceId` and broadcast diff events.

## Implementation Rules

### Server-side
1. Allocate sequence IDs only through `_trainUpdateService.NextTickSequenceId()`.
2. Every train/rail broadcast event must carry `ServerTick` and `TickSequenceId`.
3. For TrainUnit/TrainCar structural sync, prefer `NotifySnapshot`/`NotifyDeleted` instead of defining new event tags.
4. When adding new sync fields to TrainUnit/TrainCar state, update snapshot factory and message packs, not only diff bundle.

### Client-side
1. Enqueue all event payloads to `TrainUnitFutureMessageBuffer`; do not apply immediately in handlers.
2. Snapshot event handlers must apply upsert/delete at buffered tick and reconcile train-car visuals.
3. TickDiffBundle handlers must keep current semantics (pre-sim diff apply + full unit update).

## Existing Event Types (Reference)

- `va:event:trainUnitTickDiffBundle`
  - Purpose: hash gate input + simulation trigger.
  - Apply: per-unit diff apply, then `Update()` all local units.
- `va:event:trainUnitSnapshot`
  - Purpose: TrainUnit/TrainCar structural synchronization.
  - Apply: per-unit upsert/delete in cache + TrainCar object reconciliation.

## Implementation Checklist

1. Decide path:
   - TrainUnit/TrainCar structure change -> snapshot notify path.
   - Tick inputs/hash/simulation trigger -> tick diff bundle path.
   - Rail topology diff -> rail node/connection path.
2. Ensure payload has `ServerTick` + `TickSequenceId`.
3. Ensure client handler only enqueues buffered events.
4. If TrainUnit/TrainCar data shape changed:
   - update `TrainUnitSnapshotFactory`
   - update `TrainUnitSnapshotMessagePack` model mapping
   - update client cache/snapshot application and hash consistency checks
5. Verify ordering with `TickUnifiedId`.

## Key Files

- `moorestech_server/Assets/Scripts/Game.Train/Unit/TrainUpdateService.cs`
- `moorestech_server/Assets/Scripts/Game.Train/Event/TrainUnitSnapshotNotifyEvent.cs`
- `moorestech_server/Assets/Scripts/Server.Event/EventReceive/TrainUnitTickDiffBundleEventPacket.cs`
- `moorestech_server/Assets/Scripts/Server.Event/EventReceive/TrainUnitSnapshotEventPacket.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/TrainUnitFutureMessageBuffer.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Unit/TrainUnitClientSimulator.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/TrainUnitTickDiffBundleEventNetworkHandler.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/TrainUnitSnapshotEventNetworkHandler.cs`

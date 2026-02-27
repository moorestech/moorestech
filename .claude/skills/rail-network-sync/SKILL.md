---
name: rail-network-sync
description: Implement and review rail/train network synchronization flows across client and server while preserving unified TickUnifiedId ordering and per-TrainUnit snapshot boundaries. Use when changing rail/train sync behavior, event apply order, or recovery-related synchronization paths.
---

# Rail Network Sync

## Overview
Use this skill to keep server/client train and rail synchronization aligned under unified ordering.

## Reuse-First Rule
- Before adding new sync helpers, search existing train/rail implementations first.
- Prefer shared logic in `Game.Train` over client-only/server-only duplicate implementations.
- If duplication is unavoidable, record a short `WHY_NEW_IMPLEMENTATION` reason in code/PR notes.
- Recommended pre-check:
  - `rg --line-number "TickUnifiedId|TickSequenceId|Snapshot|FutureMessageBuffer|RailGraph" moorestech_client/Assets/Scripts moorestech_server/Assets/Scripts`

## Architecture Baseline
- Unified ordering key: `TickUnifiedId = ((ulong)ServerTick << 32) | TickSequenceId`.
- Server sequence allocation is per tick (`TrainUpdateService.NextTickSequenceId()`), reset when tick advances.
- Tick simulation trigger event remains `va:event:trainUnitTickDiffBundle` and is emitted even when per-unit diffs are empty.
- Structural train sync boundary remains per-TrainUnit snapshot event (`va:event:trainUnitSnapshot`, upsert/delete).
- Rail node/connection diffs are buffered and applied under the same unified ordering discipline.

## Responsibilities of This Skill
1. Verify end-to-end sync flow: server emit -> client enqueue -> tick-aligned apply.
2. Preserve ordering consistency across tick-diff bundle, TrainUnit snapshot events, and rail graph events.
3. Validate stale-event protection in apply paths (for example endpoint guid/node checks on destructive rail operations).
4. Confirm buffering and dummy-hash interactions do not regress ordering behavior.

## Not Responsible For
- MessagePack schema/tag design (`train-rail-event-implementation`).
- Tick/hash algorithm semantics (`train-tick-simulation`).
- Save/load schema and restore order (`train-rail-save-load`).

## Canonical Flow Patterns

### Train tick: hash + diff + sim
Server:
1. Emit hash state for current tick.
2. Emit tick diff bundle with sequence IDs (`HashTickSequenceId`, `DiffTickSequenceId`) and `ServerTick`.

Client:
1. Expand bundle into buffered hash + buffered diff apply events.
2. Apply pre-sim diffs and run local simulation when unified id becomes flushable.

### TrainUnit structural sync
Server:
1. Raise per-unit snapshot notify (`NotifySnapshot` / `NotifyDeleted`).
2. Broadcast `va:event:trainUnitSnapshot` with ordering keys.

Client:
1. Buffer snapshot event.
2. Apply upsert/delete on target unified id and reconcile cache/visual state.

### Rail node/connection sync
Server:
- Broadcast node/connection create/remove events with `ServerTick` + `TickSequenceId`.

Client:
- Buffer then apply on target unified id.
- Keep endpoint validation guards for destructive operations.

## Workflow
1. Identify which sync flow changed (tick bundle, snapshot, rail diff, or combination).
2. Trace full path across server emit and client apply.
3. Verify ordering keys and buffer semantics for all affected events.
4. Verify stale payload guards and deletion/upsert behavior.
5. Add/update integration tests at changed boundaries.
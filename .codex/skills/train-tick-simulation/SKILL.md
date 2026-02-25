---
name: train-tick-simulation
description: Define and enforce deterministic train tick behavior under unified TickUnifiedId ordering, hash gating, diff-bundle simulation triggers, and coexistence with per-unit TrainUnit snapshot events.
---

# Train Tick Simulation

## Overview
Use this skill to keep server/client train simulation aligned under unified chronological ordering.

## Reuse-First Rule
- Before adding tick/event helper logic, search existing train implementations first.
- Prefer shared `Game.Train` logic over duplicate per-handler implementations.
- If duplication is unavoidable, record `WHY_NEW_IMPLEMENTATION` in code/PR notes.
- Recommended pre-check:
  - `rg --line-number "TickUnifiedId|TickSequenceId|TrainUpdateService|HashVerifier|TickDiffBundle" moorestech_client/Assets/Scripts moorestech_server/Assets/Scripts`

## Target Tick Flow (Current)
Server order:
1. Build and emit hash state for tick `n`.
2. Increment tick to `n+1` and reset per-tick sequence counter.
3. Run server train simulation (`TrainUnit.Update()`).
4. Emit pre-simulation diff trigger for tick `n+1` (even when diffs are empty).
5. Broadcast `trainUnitTickDiffBundle` carrying hash(`n`) + diff(`n+1`).

Client order:
1. Flush buffered events by increasing unified id.
2. Apply diff bundle event at its buffered id.
3. Evaluate hash gate for next unified id.
4. Advance tick when gate permits; allow deadlock-avoidance behavior when only future hashes exist.

## Unified Ordering Contract
- `TickUnifiedId = ((ulong)ServerTick << 32) | TickSequenceId`.
- Train/rail events must carry `ServerTick` and sequence ID.
- Client network handlers enqueue events; no immediate side-effect apply in handlers.
- Sequence IDs are allocated only via `TrainUpdateService.NextTickSequenceId()`.

## TickDiffBundle Contract
- Event tag: `va:event:trainUnitTickDiffBundle`.
- Bundle includes hash + diff payload with independent sequence IDs.
- `Diffs[]` may be empty and still acts as simulation trigger.
- Dummy hash (`uint.MaxValue`) must be treated as pass sentinel under current gate behavior.

## Snapshot Event Coexistence Contract
- Structural TrainUnit/TrainCar sync remains per-unit snapshot event (`va:event:trainUnitSnapshot`).
- Tick diff bundle does not replace snapshot-based structural synchronization.
- Hash mismatch recovery still uses snapshot retrieval path.

## Workflow
1. Verify server order: hash -> tick increment/reset -> sim -> diff trigger.
2. Verify all affected payloads keep ordering fields.
3. Verify client handlers enqueue, then buffered apply preserves unified ordering.
4. Verify gate behavior for dummy hash, stale hash discard, and future-hash progression.
5. Add/update tests for ordering, gate, empty-diff trigger, and snapshot coexistence.
---
name: train-system
description: >
  Train/rail system reference covering network sync flows, event implementation, tick simulation,
  save/load, core invariants, and test prioritization under unified TickUnifiedId ordering and
  per-TrainUnit snapshot synchronization. Use when changing train/rail sync behavior, `va:event:*`
  contracts, tick/hash progression, persistence, rail topology semantics, or planning train regression tests.
---

# Train System

Single reference for all train/rail work. Read the shared contracts first, then the section matching your change.

各セクションは元の6スキルを集約した要約。詳細な元ドキュメントは `references/` に完全な形で保存してある:
- 1. Network Sync Flow → [references/network-sync.md](references/network-sync.md)
- 2. Event Implementation → [references/event-implementation.md](references/event-implementation.md)
- 3. Tick Simulation → [references/tick-simulation.md](references/tick-simulation.md)
- 4. Save / Load → [references/save-load.md](references/save-load.md)
- 5. Core Invariants → [references/system-notes.md](references/system-notes.md)
- 6. Test Prioritization → [references/test-priorities.md](references/test-priorities.md)

## Shared Contracts (apply to every section)

### Reuse-First Rule
- Before adding helpers, search existing train/rail code; prefer shared `Game.Train` logic over duplicates.
- If duplication is unavoidable, record `WHY_NEW_IMPLEMENTATION` in code/PR notes.
- Pre-check: `rg --line-number "TickUnifiedId|TickSequenceId|RailPosition|Snapshot|TickDiffBundle|SaveLoad" moorestech_client/Assets/Scripts moorestech_server/Assets/Scripts`

### Unified Ordering Contract
- `TickUnifiedId = ((ulong)ServerTick << 32) | TickSequenceId`.
- Sequence IDs are allocated only via `TrainUpdateService.NextTickSequenceId()`, server-managed, reset per tick.
- Every train/rail event carries `ServerTick` + sequence ID.
- Client network handlers enqueue into `TrainUnitFutureMessageBuffer`; no immediate side-effect apply in handlers. Buffered events apply in unified-id order.

### Snapshot-First Train Sync
- Structural TrainUnit/TrainCar changes flow through per-unit snapshot event `va:event:trainUnitSnapshot` (upsert/delete); full resync via `va:getTrainUnitSnapshots` (canonical recovery path on hash mismatch).
- Tick simulation trigger remains `va:event:trainUnitTickDiffBundle`; it does not replace snapshot-based structural sync.

## 1. Network Sync Flow (end-to-end ownership)

Verify: server emit → client enqueue → tick-aligned apply, for all three flows.

- **Tick path**: server emits hash state, then tick diff bundle (`HashTickSequenceId`, `DiffTickSequenceId`, `ServerTick`). Client expands the bundle into buffered hash + diff apply events; applies pre-sim diffs and runs local simulation when the unified id becomes flushable.
- **TrainUnit structural path**: server raises `NotifySnapshot`/`NotifyDeleted` → broadcasts `va:event:trainUnitSnapshot`. Client buffers, applies upsert/delete at target unified id, reconciles cache/visual state.
- **Rail graph path**: node/connection create/remove events broadcast with ordering keys; client buffers then applies. Keep endpoint validation guards (guid/node checks) on destructive operations against stale payloads.

Checklist: trace the full path, verify ordering keys and buffer semantics on all affected events, verify stale guards and upsert/delete behavior, add integration tests at changed boundaries.

## 2. Event Implementation (tags, payloads, handlers)

- Choose the correct path: tick bundle / snapshot / rail diff. Prefer the snapshot notify path over new ad-hoc car events for train composition changes.
- `va:event:trainUnitTickDiffBundle`: hash + per-tick diffs with independent sequence IDs; `Diffs[]` may be empty and still triggers simulation.
- Snapshot handlers apply upsert/delete at buffered tick and reconcile train/car visuals.
- If state shape changes, update packet mapping + client apply + tests together.

## 3. Tick Simulation (progression, hash gate)

Server order (must preserve):
1. Build and emit hash state for tick `n`.
2. Increment tick to `n+1`, reset per-tick sequence counter.
3. Run server train simulation (`TrainUnit.Update()`).
4. Emit pre-simulation diff trigger for tick `n+1` (even when diffs are empty).
5. Broadcast `trainUnitTickDiffBundle` carrying hash(`n`) + diff(`n+1`).

Client order (must preserve):
1. Flush buffered events by increasing unified id.
2. Apply diff bundle at its buffered id.
3. Evaluate hash gate for next unified id.
4. Advance tick when gate permits; allow deadlock-avoidance when only future hashes exist.

Gate rules: dummy hash `uint.MaxValue` is a pass sentinel; stale hashes are discarded; hash-mismatch recovery uses the snapshot retrieval path.

## 4. Save / Load

Architecture (must preserve): rail connectivity persists as `railSegments` (restored via `RailGraphSaveLoadService`); train state persists as `trainUnits` (restored via `TrainSaveLoadService`); block placement and segment connectivity restoration are separate steps.

Load order (must preserve — rail-position restoration resolves `ConnectionDestination` via the rail graph provider):
1. World blocks → 2. rail segments → 3. common world/player/entity state → 4. train units → 5. docking links.

Data model:
- `RailSegmentSaveData`: `A`/`B` (`ConnectionDestination`), `Length`, `RailTypeGuid`, `IsDrawable`.
- `TrainUnitSaveData`: rail position snapshot + train state + cars + diagram. `TrainCarSaveData` includes `TrainCarMasterId`, `IsFacingForward`, stateful car data.

Rules:
- Skip null segment entries; keep drawable-segment length validation; restore via `TryRestoreRailSegment`, not ad-hoc graph mutation.
- Reset previously registered runtime train state before restore; restore TrainUnit + TrainCar as one unit; rebuild docking after the unit pass; fail safely per unit on invalid references.
- Do not persist transport-only runtime values (sequence IDs, future buffers, queue internals).
- If TrainUnit/TrainCar state shape changes, update save/load and snapshot serialization together.

## 5. Core Invariants (topology, direction, docking)

- `TrainUnit` is the composition and synchronization boundary. Reverse via `TrainUnit.Reverse()`, never by mutating only `RailPosition`.
- Deterministic distance handling: integer-based persisted distances.
- Rail components have directional endpoints (`FrontNode`/`BackNode`) as opposite pairs — directional graph semantics, not naming. No ad-hoc symmetric edge duplication.
- RailPosition node ordering is semantic; validate adjacency direction-aware before constructing paths; verify forward and reverse reachability.
- Docking handles are the source of truth for docking state; restore docking links after train registration.
- Backward-facing cars keep weight but produce no traction; preserve `IsFacingForward` through save/load and snapshot paths.
- Deletion must clear both cache state and visual/car representation on apply.

## 6. Test Prioritization

Priority order for regression coverage after a change:
1. **Tick ordering + buffering**: unified-id monotonicity / no rollback; stale drop + exact flush in future buffer.
2. **Tick sim + hash gate**: empty-diff bundle still triggers simulation; gate progression, stale-hash discard, dummy-hash (`uint.MaxValue`) consistency.
3. **Snapshot sync**: per-unit upsert/delete regression; deletion cleanup across cache/view.
4. **Long-run movement/topology/persistence**: reverse + traction consistency; RailPosition traversal edges; docking concurrency; save/load long-run stability.

For networked behaviors assert server emit + buffered apply + resulting state; prefer deterministic scenarios. Add at least one snapshot-sync regression test for TrainUnit/TrainCar structural changes.

## Workflow

1. Classify the change: sync flow / event contract / tick-gate / persistence / topology invariant.
2. State the applicable invariants from the sections above before editing; encode them as tests/assertions.
3. Implement, keeping ordering fields and stale guards intact.
4. Re-run relevant deterministic train tests and add coverage per section 6.

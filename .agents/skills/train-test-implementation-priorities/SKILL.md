---
name: train-test-implementation-priorities
description: Plan and prioritize train test implementation by risk and coverage gaps under unified tick ordering and per-unit snapshot synchronization architecture. Use when selecting train regression tests after changes to ordering, hash gates, snapshot sync, topology, or persistence behavior.
---

# Train Test Implementation Priorities

## Overview
Use this skill to prioritize high-risk coverage gaps instead of adding tests randomly.

## Reuse-First Rule
- Before creating test-only helpers, search existing train test utilities first.
- Prefer reusable helpers from production/test infrastructure over duplicated logic.
- If duplication is unavoidable, record `WHY_NEW_IMPLEMENTATION` in test comments/PR notes.
- Recommended pre-check:
  - `rg --line-number "TrainTestHelper|RailPosition|TickUnifiedId|Snapshot|Dock" moorestech_client/Assets/Scripts moorestech_server/Assets/Scripts`

## Priority Order

Priority 1:
- Tick ordering + client buffering regressions:
  - unified id monotonicity / no rollback
  - stale drop + exact flush behavior in future buffer

Priority 2:
- Tick simulation and hash gate behavior:
  - empty-diff tick bundle still triggers simulation
  - hash gate progression and stale-hash discard behavior
  - dummy-hash handling consistency (`uint.MaxValue`)

Priority 3:
- Snapshot-sync and structural train changes:
  - per-unit snapshot upsert/delete regression coverage
  - deletion cleanup behavior across cache/view state

Priority 4:
- Long-run movement/topology/persistence:
  - reverse + traction consistency
  - RailPosition traversal edge cases
  - docking concurrency and save/load long-run stability

## Coverage Expectations
- Balance unit-level, integration-level, and scenario-level tests.
- For networked behaviors, assert server emit + buffered apply + resulting state.
- Prefer deterministic scenarios over random-only checks.

## Workflow
1. Classify change type (ordering, hash/gate, snapshot, topology, persistence).
2. Select uncovered highest-priority scenarios first.
3. Define explicit failure signals for each test.
4. Add at least one snapshot-sync regression test for TrainUnit/TrainCar structural changes.
5. Implement tests using reusable utilities when possible.

---
name: train-test-implementation-priorities
description: Plan and prioritize train integration test implementation by risk and current coverage gaps under the current unified tick + per-unit snapshot sync architecture. Use when selecting next train tests, assessing coverage quality, identifying high-risk scenarios, or choosing regression investment after train/rail code changes.
---

# Train Test Implementation Priorities

## Overview

Use this skill to avoid random test additions and focus on highest-risk gaps first.

## Reuse-First Rule

- Before adding new test-only helper implementations, search existing train helpers first.
- Prefer reusing production helpers from `Game.Train` where valid, instead of cloning logic into tests.
- If a duplicate helper is unavoidable, document `WHY_NEW_IMPLEMENTATION` in test comments/PR notes.
- Recommended pre-check:
  - `rg --line-number "Overlap|CreateIndex|HasOverlap|TrainTestHelper|RailPosition" moorestech_client/Assets/Scripts moorestech_server/Assets/Scripts`

## Priority Order

Priority 1:
- Strengthen TrainUnit/TrainCar snapshot-sync regression coverage:
  - place/attach/remove operations emit expected per-unit snapshot updates
  - deletion tombstone path removes client cache + TrainCar visuals
  - changed train composition does not require per-car event packets
- Add deterministic checks around `TrainUnitSnapshotEventPacket` + `TrainUnitSnapshotEventNetworkHandler` apply timing.

Priority 2:
- Strengthen tick/hash behavior:
  - `TrainUnitTickDiffBundle` empty diff still triggers simulation
  - hash gate progression and stale-hash discard behavior
  - mismatch recovery (`GetTrainUnitSnapshots`, and rail+train combined resync path)

Priority 3:
- Strengthen persistence and topology long-run checks:
  - save/load with moving/docking/auto-run states
  - rail segment restore consistency with train restore order
  - runtime edge cases (reverse during operation, large topology variants)

## Coverage Expectations

- Keep a balanced layer mix:
  - unit-level algorithm checks
  - integration-level state consistency checks
  - scenario-level long-run behavior checks
- Include both server and client-side assertions for networked behaviors (event payload + buffered apply + cache/view effects).
- Prefer deterministic scenarios over random-only validation for regression stability.

## Workflow

1. Classify the request into implementation, refactor, or coverage review.
2. Select scenarios from highest uncovered priority first.
3. Define concrete failure signals for each new test.
4. For TrainUnit/TrainCar changes, always include at least one snapshot-sync regression test.
5. Implement tests with reusable train test utilities whenever possible.

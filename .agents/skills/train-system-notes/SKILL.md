---
name: train-system-notes
description: Apply core train invariants for rail graph topology, front/back node semantics, rail position ordering, docking references, reverse behavior, deterministic distance handling, and TrainUnit snapshot boundaries. Use when implementing or debugging train movement/topology consistency and directional rail semantics.
---

# Train System Notes

## Overview
Use this skill as a guardrail for train fundamentals and deterministic behavior.

## Reuse-First Rule
- Before adding rail/train algorithm helpers, search existing implementations first.
- Prefer canonical `Game.Train` logic over client-local duplication.
- If duplication is unavoidable, record `WHY_NEW_IMPLEMENTATION` in code/PR notes.
- Recommended pre-check:
  - `rg --line-number "RailPosition|OppositeNode|TrainUnit\.Reverse|Dock|Overlap" moorestech_client/Assets/Scripts moorestech_server/Assets/Scripts`

## Core Invariants
- Treat `TrainUnit` as composition and synchronization boundary.
- Reverse train direction with `TrainUnit.Reverse()`, not by mutating only `RailPosition`.
- Keep deterministic distance handling (integer-based persisted distances).
- Treat docking handles as source of truth for docking state.

## Train Sync Invariants
- Structural TrainUnit/TrainCar changes should flow through per-unit snapshot notify path.
- Deletion should clear both cache state and visual/car representation on apply.
- Full snapshot API remains canonical recovery path on mismatch/resync.

## Rail Graph Model Invariants
- Each rail component has directional endpoints (`FrontNode`/`BackNode`) as opposite pairs.
- Front/back semantics are directional graph semantics, not cosmetic naming.
- Avoid ad-hoc symmetric edge duplication that breaks opposite-node meaning.

## Rail Position Rules
- Node ordering is semantic and must match traversal assumptions.
- Validate adjacency in direction-aware way before constructing paths.
- Add assertions/tests that catch invalid order early.
- Verify forward and reverse reachability in deterministic scenarios.

## Station and Docking Notes
- Keep station/cargo node pairing consistent with directional routing semantics.
- Avoid duplicate manual station edge wiring in tests.
- Restore docking links after train registration in save/load flow.

## TrainCar Direction and Traction
- Backward-facing cars keep weight but do not produce traction.
- Preserve orientation via `IsFacingForward` through save/load and snapshot paths.
- If schema migration is needed, implement explicit migration logic.

## Workflow
1. Extract applicable invariants before editing.
2. Encode invariants as tests/assertions.
3. Implement change.
4. Re-run relevant deterministic train tests.

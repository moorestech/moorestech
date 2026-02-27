---
name: train-doc-index
description: Classify train/rail requests and route to the correct specialized skill quickly. Use when a request touches train networking, tick simulation ordering, snapshot-sync policy, save/load verification, rail graph behavior, or test prioritization.
---

# Train Doc Index

## Overview
Use this skill as the entry point to pick the right train workflow and avoid mixing unrelated concerns.

## Reuse-First Rule
- Before introducing new helper logic, search existing train/rail code first.
- Prefer established `Game.Train` logic over ad-hoc duplicate implementations.
- If duplication is unavoidable, record `WHY_NEW_IMPLEMENTATION` in code/PR notes.
- Recommended pre-check:
  - `rg --line-number "TickUnifiedId|TickSequenceId|RailPosition|Snapshot|SaveLoad" moorestech_client/Assets/Scripts moorestech_server/Assets/Scripts`

## Current Operation (Must Assume)
- Train structural sync is snapshot-first:
  - per-unit event: `va:event:trainUnitSnapshot`
  - full resync API: `va:getTrainUnitSnapshots`
- Tick simulation trigger remains `va:event:trainUnitTickDiffBundle`.
- Ordering key remains `TickUnifiedId = ((ulong)ServerTick << 32) | TickSequenceId`.
- `TickSequenceId` is server-managed and reset per tick.

## Routing Rules
- Use `rail-network-sync` for end-to-end train/rail synchronization flow ownership.
- Use `train-rail-event-implementation` for event tags, payload schema, and client/server handler changes.
- Use `train-tick-simulation` for tick/hash progression and gate/order behavior.
- Use `train-rail-save-load` for persistence schema, load order, restore correctness.
- Use `train-system-notes` for rail topology, front/back semantics, RailPosition invariants.
- Use `train-test-implementation-priorities` for risk-based test planning.

## Workflow
1. Classify the request into one primary category.
2. Pick one primary skill and at most one secondary skill.
3. State explicit invariants before edits.
4. For TrainUnit/TrainCar mutations, confirm snapshot-first policy before proposing packet changes.
5. Implement and verify against chosen invariants.
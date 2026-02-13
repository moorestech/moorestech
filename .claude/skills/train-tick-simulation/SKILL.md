---
name: train-tick-simulation
description: Define and enforce deterministic train tick-phase behavior between server and client, including hash gating and mascon diff handling. Use when implementing or reviewing train tick order, pre-sim/post-sim event phases, or tick-aligned protocol/event payloads.
---

# Train Tick Simulation

## Overview

Use this skill to keep server/client simulation aligned by tick and phase.

## Target Tick Flow

Server order:
1. Emit hash event first.
2. Increment server tick.
3. Run per-train simulation.
4. Emit tick-bound diffs only for changed trains.

Client order:
1. Apply pre-sim events for the tick.
2. Pass hash gate for that tick.
3. Simulate local train state.
4. Apply post-sim events for the tick.

## Mascon Diff Event Contract

- Event payload contains:
  - `Tick`
  - `Changes[]` with (`TrainId`, `MasconLevel`)
- Emit exactly one batch per tick when there is at least one change.
- Emit no event when there are no changes.
- Treat mascon diff as pre-sim input.

## Workflow

1. Classify incoming behavior as pre-sim or post-sim.
2. Verify server phase order and client phase order both match this skill.
3. Ensure every added train event/protocol includes tick.
4. Add tests for:
   - hash gate progression
   - pre-sim-before-sim enforcement
   - no-change suppression of mascon diff emission

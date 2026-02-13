---
name: train-system-notes
description: Apply core train system invariants for rail graph topology, front/back node semantics, rail position ordering, docking references, train reverse behavior, and deterministic distance handling. Use when implementing or debugging fundamental train movement and topology logic.
---

# Train System Notes

## Overview

Use this skill as a guardrail for train fundamentals and deterministic behavior.

## Core Invariants

- Build train tests with deterministic setup (`TrainTestHelper.CreateEnvironment`) and reset singleton state between scenarios.
- Trust docking handles (`ITrainDockHandle`) as source of truth for docking state, not local cache alone.
- Reverse train direction with `TrainUnit.Reverse()`. Do not reverse only `RailPosition`, because car orientation and traction consistency break.
- Keep rail-node distance as fixed integer values. Avoid float persistence for node distance because save/load reproducibility degrades.

## Test Setup Rules

- Build deterministic test worlds via `TrainTestHelper.CreateEnvironment()`.
- Place rails/stations with test helpers and `ForUnitTestModBlockId` identifiers.
- Advance simulation by explicit tick loops and keep loop guards to prevent infinite test hangs.
- Before cross-scenario save/load checks, clear singleton state (`RailGraphDatastore` and related global stores) to avoid leaked state.

## Debug Visualization

- For editor/dev diagnostics, snapshot current graph structure via `IRailGraphDatastore.CaptureSnapshot(...)`.
- Serialize snapshots and inspect node/edge payloads (`nodes` / `edges`) when validating topology mutations.

## Docking and Handle Rules

- Validate docking behavior through docking handles (`ITrainDockHandle`) rather than only local train caches.
- Keep docking/tick behavior consistent with the event phase rules in `train-rail-event-implementation`.

## TrainCar Direction and Traction

- Backward-facing cars keep weight but do not generate traction.
- Persist direction through `TrainCarSaveData.IsFacingForward`.
- Treat missing legacy direction value as default forward (`true`) on load.

## Rail Graph Model

- Each `RailComponent` owns `FrontNode` and `BackNode` as opposite pairs.
- Treat `FrontNode` and `BackNode` as paired directional endpoints (`OppositeNode`) inside one component.
- A simple rail block commonly has 2 rail components, so minimum topology is 4 directional nodes.
- The graph is directional at node level.
- Connection APIs use the selected source node (`FrontNode` or `BackNode`) as directed-edge start.
- A connection like `front(A) -> front(B)` implies reverse-path mapping through opposite nodes (`back(B) -> back(A)`), not a duplicated manual bidirectional edge.
- Avoid forcing symmetric edge wiring by manual duplicate connect calls; it breaks front/back correspondence and station path semantics.
- Disconnection/distance helpers are also front/back-sensitive; wrong side selection leaves stale reverse edges.

## Station and Loop Cautions

- Station/cargo components auto-generate entry/exit directed edges on placement.
- Station-like components should be modeled as `front/back x entry/exit` node pairs.
- Standard station-side pairing is directional: front side handles `Entry -> Exit`, back side handles reverse route pairing.
- Do not manually duplicate station entry/exit edge wiring in tests; it can double-count distances.
- Loop routes require explicit return connection wiring; otherwise forward/backward paths become disconnected.
- Diagram entries for station operations should use station exit-side nodes consistently.

## Rail Position Rules

- `RailPosition` ordering is not arbitrary: lower index means closer to train-front side in this model.
- `RailPosition` node list ordering must satisfy distance traversal expectations in `RailNodeCalculate`.
- `RailNodeCalculate.CalculateTotalDistance` evaluates adjacency as `railNodes[i + 1].GetDistanceToNode(railNodes[i])`.
- Because of this, a naively "travel-order" list can be reversed for distance calculation and return `-1`.
- Validate adjacency explicitly before building position paths, especially for round-trip and loop scenarios.
- Validate all adjacent pairs with `next.GetDistanceToNode(current) >= 0` before creating `RailPosition`.
- Add assertions that catch incorrect node order early for both forward and return direction checks.
- For runtime movement expectations, also verify forward reachability separately with `current.GetDistanceToNode(next) > 0`.
- For docking-oriented initial placement, keep the departure station exit-side node first and paired entry-side node next.
- Verify segment reachability with direction-aware checks such as `next.GetDistanceToNode(current)` where needed.

## Terminology

- `Front` / `Back`: directional graph semantics, not merely world-geometry front/back.
- `OppositeNode`: paired reverse-direction node inside the same rail component.
- `ConnectionDestination`: serializable endpoint identity (`blockPosition`, `componentIndex`, `isFront`) that maps 1:1 to a concrete `IRailNode` in the restored rail graph.
- Treat `ConnectionDestination` as the canonical node key in save/load boundaries. Avoid ad-hoc node identity reconstruction outside this mapping contract.

## Workflow

1. Extract applicable invariants from this skill before coding.
2. Encode those invariants as tests or assertions.
3. Implement the change.
4. Re-run related train tests and verify determinism.

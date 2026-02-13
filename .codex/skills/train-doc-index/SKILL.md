---
name: train-doc-index
description: Classify train-related requests and route execution to the correct train workflow immediately. Use when a request concerns train networking, tick-phase simulation, train save/load verification, rail graph behavior, or train test prioritization and you need to choose the right implementation/testing strategy.
---

# Train Doc Index

## Overview

Use this skill as an entry point to pick the right train workflow and avoid mixing unrelated concerns.

## Routing Rules

- Use `rail-network-sync` when changing protocol/event behavior for rail or train state synchronization.
- Use `train-tick-simulation` when changing tick progression, hash gating, or pre-sim/post-sim application order.
- Use `train-rail-save-load` when changing save data, load restoration, docking restoration, or persistence tests.
- Use `train-test-implementation-priorities` when deciding what train tests to implement next.
- Use `train-system-notes` when changing rail graph semantics, front/back handling, rail position ordering, or deterministic distance behavior.

## Workflow

1. Classify the request into one primary category.
2. Pick one primary skill and at most one secondary skill.
3. State explicit invariants before editing code.
4. Implement and verify against those invariants.

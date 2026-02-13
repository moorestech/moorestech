---
name: train-test-implementation-priorities
description: Plan and prioritize train integration test implementation by risk and current coverage gaps. Use when selecting next train tests, assessing coverage quality, identifying high-risk multi-train scenarios, or choosing regression investment after train-related code changes.
---

# Train Test Implementation Priorities

## Overview

Use this skill to avoid random test additions and focus on highest-risk gaps first.

## Priority Order

Priority 1:
- Strengthen multi-train operational scenarios (exchange, wait/evade, resume, route sharing) with deterministic assertions.
- Focus on deadlock or starvation risk and long-run behavioral correctness.

Priority 2:
- Strengthen realistic save/load scenarios (moving, slowing, docking, waiting states).
- Strengthen fault-injection cases (broken references, missing nodes, docking target loss).

Priority 3:
- Expand edge-case coverage for runtime operations (reverse while active, add/remove cars during operation, large topology variants).

## Coverage Expectations

- Keep a balanced layer mix:
  - unit-level algorithm checks
  - integration-level state consistency checks
  - scenario-level long-run behavior checks
- Prefer deterministic scenarios over random-only validation for regression stability.

## Workflow

1. Classify the request into implementation, refactor, or coverage review.
2. Select scenarios from highest uncovered priority first.
3. Define concrete failure signals for each new test.
4. Implement tests with reusable train test utilities whenever possible.

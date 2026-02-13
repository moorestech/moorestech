---
name: rail-network-sync
description: Implement and review rail/train network synchronization flows across client and server, including snapshots, event diffs, and hash-based recovery. Use when changing rail node/connection events, block place/remove networking, train create/remove sync, or related protocol handlers.
---

# Rail Network Sync

## Overview

Use this skill to keep server and client train/rail state transitions aligned.

## Sync Contracts

- Initial train bootstrap relies on train/rail snapshots and local appliers.
- Train bootstrap is snapshot-based (`GetTrainUnitSnapshots`); train entities are not sourced from `RequestWorldData`.
- Event stream applies state diffs for rail nodes/connections and block updates.
- Hash-state events drive mismatch detection and snapshot re-fetch only when needed.
- Train deletion is corrected via hash mismatch + snapshot reconciliation path.
- Event polling may deliver multiple ticks in one response; apply by tick semantics, not arrival order.

## Operation Flows

Bridge placement flow:
1. Client sends place request with rail component state.
2. Server places block and registers rail node(s).
3. Server broadcasts block-place + rail-node-created events.
4. Client applies block and rail cache updates.

Station placement flow:
1. Client sends normal block place request.
2. Server creates station components and auto-connections.
3. Server broadcasts block-place + node/connection-created events.
4. Client applies world state, graph cache, and station references.

Manual rail connect flow:
1. Client resolves node ids and sends connect request.
2. Server validates and connects nodes.
3. Server broadcasts connection-created event.
4. Client upserts connection cache.

Rail deletion flow:
1. Client sends block remove request.
2. Server removes block and related rail nodes/connections.
3. Server broadcasts remove-block + node/connection-removed events.
4. Client removes world object and graph cache entries.

Rail disconnect flow:
1. Client sends disconnect request.
2. Server validates edge removal safety and disconnects.
3. Server broadcasts connection-removed event.
4. Client removes connection cache.

Train placement flow:
1. Client sends place-train request with rail position data.
2. Server validates, creates train unit, registers update loop.
3. Server broadcasts train-created event with snapshot payload.
4. Client upserts train cache and entity state.

Train removal flow:
1. Client sends remove-train request.
2. Server removes cars and may destroy/unregister train unit.
3. Hash-state mismatch detects divergence.
4. Client reconciles with snapshot and removes stale train entities.

## Workflow

1. Identify the exact operation flow affected by the change.
2. Trace `client request -> server mutation -> broadcast -> client apply`.
3. Preserve hash verification and snapshot recovery semantics.
4. Keep event/protocol tag compatibility across both sides.
5. Add or update tests for changed flow boundaries.

---
name: train-rail-event-implementation
description: Implement new train/rail network events with unified chronological ordering. Use when adding or modifying server/client event packets, MessagePack payloads, or network handlers for train and rail systems, ensuring correct TickUnifiedId assignment and sequence continuity.
---

# Train/Rail Event Implementation (Unified Chronological Ordering)

## Overview

In the current architecture, the distinction between `pre-sim` and `post-sim` queues has been **unified** into a single chronological event stream. All events (including the simulation trigger itself) are ordered by a 64-bit `TickUnifiedId`.

## Core Concepts

### 1. TickUnifiedId
- A 64-bit value: `((ulong)ServerTick << 32) | TickSequenceId`.
- `ServerTick`: The cumulative game tick count (20Hz).
- `TickSequenceId`: A monotonic, continuous sequence number within a tick, allocated via `_trainUpdateService.NextTickSequenceId()`.

### 2. Unified Event Queue
- All events are enqueued into `TrainUnitFutureMessageBuffer` via `EnqueueEvent`.
- Events are wrapped in `ITrainTickBufferedEvent` which has a single `Apply()` method.
- The client-side simulator advances through these events by incrementing the `TickUnifiedId` by 1.

### 3. Simulation as an Event
- The train simulation (`Update()`) is now just another event in the stream.
- It is typically triggered by the `va:event:trainUnitTickDiffBundle` event.
- This bundle contains:
    - **Hash** for tick $n-1$ (used for validation).
    - **Diffs** for tick $n$ (input changes like Mascon).
    - **Simulation Trigger** for tick $n$.

## Execution Order

The client-side `TrainUnitClientSimulator.Tick()` follows this logic:
1.  **Flush Events**: It attempts to flush events with ID `current_id + 1` in a loop.
    *   If an event exists for that ID, its `Apply()` method is called.
    *   This might apply diffs, run simulation, or create/destroy train units.
2.  **Hash Gate**: If no more events are available for the next ID, it checks if it can advance to the next tick's base ID (`(tick + 1) << 32`).
3.  **Advance Tick**: If allowed, it moves to the next tick and starts the loop again.

## Implementation Rules

### Server-Side (Emitting Events)
1.  **Allocate Sequence ID**: Always use `_trainUpdateService.NextTickSequenceId()` for any event that needs chronological ordering.
2.  **Consistency**: Ensure that all events that affect the simulation or are affected by it are sent with the current `ServerTick` and a valid `TickSequenceId`.
3.  **Continuity**: Because the client expects sequential IDs (`id + 1`), you must ensure that events are emitted in a way that doesn't create gaps if the client expects to process them all. (Note: The current system relies on `NextTickSequenceId()` being called for all broadcast events).

### Client-Side (Receiving Events)
1.  **Enqueue Chronologically**: Use `_futureMessageBuffer.EnqueueEvent(serverTick, tickSequenceId, CreateBufferedEvent())`.
2.  **No Immediate Side Effects**: Never apply changes directly in the network handler. Always wrap them in a `BufferedEvent` and enqueue them.
3.  **Simulation Triggering**: If a new event type requires its own simulation step, it should be included in its `Apply()` logic or handled via the existing `TickDiffBundle` if it's just an input diff.

## Existing Event Examples

- `va:event:trainUnitTickDiffBundle`
    - Apply Logic: Updates `MasconLevel` etc. for affected trains, then calls `unit.Update()` for **all** trains.
- `va:event:trainUnitCreated`
    - Apply Logic: Adds a new train to the `TrainUnitClientCache` and updates views.

## Implementation Checklist

1.  **MessagePack**: Update or create a payload with `uint ServerTick` and `uint TickSequenceId`.
2.  **Server Packet**:
    *   Subscribe to the relevant event stream in `TrainUpdateService`.
    *   In the handler, call `_trainUpdateService.NextTickSequenceId()`.
    *   Serialize and broadcast using `_eventProtocolProvider.AddBroadcastEvent`.
3.  **Client Handler**:
    *   Deserialize the payload.
    *   Define a local function/region for the `Apply` logic.
    *   Call `_futureMessageBuffer.EnqueueEvent(...)` with a `TrainTickBufferedEvent`.
4.  **Verification**:
    *   Ensure the event happens at the correct logical time relative to the simulation.
    *   Check that `NextTickSequenceId()` is called exactly once per emitted payload.

## Key Files

- `moorestech_server/Assets/Scripts/Game.Train/Unit/TrainUpdateService.cs`
- `moorestech_server/Assets/Scripts/Server.Event/EventReceive/TrainUnitTickDiffBundleEventPacket.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/TrainUnitFutureMessageBuffer.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Unit/TrainUnitClientSimulator.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/TrainUnitTickDiffBundleEventNetworkHandler.cs`

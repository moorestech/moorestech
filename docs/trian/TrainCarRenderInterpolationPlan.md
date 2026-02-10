# TrainCar Render Interpolation - Implementation Edit Guide

## Goal
- Keep train simulation deterministic at 10Hz.
- Render TrainCar smoothly every frame by interpolating between two confirmed simulation ticks.
- Do not change server protocol or tick logic.

## Why this is the correct root fix
- The visible stutter comes from discrete pose updates every 100ms.
- Even if server/client simulation are identical, direct pose apply at 10Hz will always look stepped.
- The correct solution is a render layer between simulation ticks.

## Edit Scope (exact files)
1. Add new file:
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Unit/ITrainRenderInterpolationSource.cs`

2. Edit existing files:
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Unit/TrainUnitClientSimulator.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Unit/ClientTrainUnit.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/View/TrainCarEntityPoseUpdater.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Entity/Factory/TrainCarObjectFactory.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/View/Object/TrainCarObjectDatastore.cs`
- `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`

## File-by-file edit details

### 1) `ITrainRenderInterpolationSource.cs` (new)
Purpose:
- Expose render alpha (`0..1`) from the simulation clock to render systems.

Add interface:
```csharp
namespace Client.Game.InGame.Train.Unit
{
    public interface ITrainRenderInterpolationSource
    {
        float GetRenderAlpha();
    }
}
```

### 2) `TrainUnitClientSimulator.cs`
Purpose:
- Be the single source of interpolation alpha.
- Mark tick boundaries for per-train render state.

Edits:
- Implement `ITrainRenderInterpolationSource`.
- Add `public float GetRenderAlpha()`:
  - Return `Mathf.Clamp01((float)(_accumulatedSeconds / TickSeconds))`.
- In `SimulateOneTick()` wrap each unit update with boundary hooks:
  - `unit.BeginRenderInterpolationTick();`
  - `unit.Update();`
  - `unit.EndRenderInterpolationTick();`

Notes:
- Do not change existing tick gate / hash verification flow.

### 3) `ClientTrainUnit.cs`
Purpose:
- Keep two immutable rail positions for rendering.
- Reset interpolation state safely when snapshot overwrites local state.

Add fields:
```csharp
private RailPosition _renderFromRailPosition;
private RailPosition _renderToRailPosition;
```

Add methods:
- `public void BeginRenderInterpolationTick()`
  - `_renderFromRailPosition = _renderToRailPosition?.DeepCopy();`
- `public void EndRenderInterpolationTick()`
  - `_renderToRailPosition = RailPosition?.DeepCopy();`
  - If `_renderFromRailPosition == null`, set `_renderFromRailPosition = _renderToRailPosition?.DeepCopy();`
- `public bool TryGetRenderRailPositions(out RailPosition from, out RailPosition to)`
  - Return false if either side is null.

Edit `SnapshotUpdate(...)`:
- After `RailPosition = RailPositionFactory.Restore(...)`, call interpolation reset:
  - `_renderToRailPosition = RailPosition?.DeepCopy();`
  - `_renderFromRailPosition = _renderToRailPosition?.DeepCopy();`

Notes:
- This removes the need to edit `TrainUnitSnapshotApplier.cs` directly.
- Snapshot reapply naturally collapses interpolation (`from == to`) and avoids warp lerp.

### 4) `TrainCarEntityPoseUpdater.cs`
Purpose:
- Compute per-frame interpolated TrainCar pose from two rail positions.

Edits:
- Add dependency field:
  - `private ITrainRenderInterpolationSource _interpolationSource;`
- Change `SetDependencies(...)` signature:
  - `SetDependencies(TrainCarEntityObject trainCarEntity, TrainUnitClientCache trainCache, ITrainRenderInterpolationSource interpolationSource)`

Replace pose resolve flow:
- Existing flow uses one `unit.RailPosition`.
- New flow:
  1. Resolve car snapshot + offsets as now.
  2. Get `from/to` rail positions from `unit.TryGetRenderRailPositions(...)`.
  3. Compute front/rear poses on both sides via `TrainCarPoseCalculator.TryGetPose(...)`.
  4. `Vector3.Lerp` front and rear by `alpha = _interpolationSource.GetRenderAlpha()`.
  5. Rebuild center and forward from interpolated front/rear.
  6. Apply existing `BuildRotation(...)` and model center offset.

Do not edit:
- `TrainCarPoseCalculator` logic itself (reuse as is).

### 5) `TrainCarObjectFactory.cs`
Purpose:
- Pass interpolation source into each pose updater.

Edits:
- Add field:
  - `private readonly ITrainRenderInterpolationSource _interpolationSource;`
- Change constructor:
  - `TrainCarObjectFactory(TrainUnitClientCache trainCache, ITrainRenderInterpolationSource interpolationSource)`
- In `CreateTrainEntity(...)`:
  - `poseUpdater.SetDependencies(trainEntityObject, _trainCache, _interpolationSource);`

### 6) `TrainCarObjectDatastore.cs`
Purpose:
- Wire factory with new dependency.

Edits:
- Change `[Inject] Construct(...)` signature:
  - `Construct(TrainUnitClientCache trainUnitClientCache, ITrainRenderInterpolationSource interpolationSource)`
- Factory creation:
  - `_carObjectFactory = new TrainCarObjectFactory(trainUnitClientCache, interpolationSource);`

### 7) `MainGameStarter.cs`
Purpose:
- Register simulator as both tick entry and interpolation source.

Edit registration line:
- from:
  - `builder.Register<TrainUnitClientSimulator>(Lifetime.Singleton).As<ITickable>();`
- to:
  - `builder.Register<TrainUnitClientSimulator>(Lifetime.Singleton).As<ITickable>().As<ITrainRenderInterpolationSource>();`

## Implementation order (recommended)
1. Add interface file.
2. Update simulator (`GetRenderAlpha`, boundary hooks).
3. Update `ClientTrainUnit` render state methods.
4. Update pose updater to consume `from/to + alpha`.
5. Wire DI path (`TrainCarObjectFactory` -> `TrainCarObjectDatastore` -> `MainGameStarter`).
6. Compile and run train scene.

## Definition of done
- No compile errors in client assembly.
- `TrainCarEntityPoseUpdater` no longer depends on only one confirmed pose.
- In play mode, TrainCar movement appears smooth at frame rate while server tick remains 10Hz.
- After snapshot resync, no long lerp jump occurs (first frame after resync uses `from == to`).

## Out of scope for this task
- Prediction/extrapolation beyond confirmed ticks.
- Protocol changes.
- Rail graph interpolation redesign.

## Existing Train Extension (Add Car to Existing Train)

### Current state (what exists now)
- Client train-car placement is implemented only as new-train creation:
  - `TrainCarPlaceSystem` calls `VanillaApiWithResponse.PlaceTrainOnRail(...)`
  - File: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainCar/TrainCarPlaceSystem.cs`
  - File: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs`
- Server `PlaceTrainCarOnRailProtocol` always creates a new `TrainUnit` with one `TrainCar`.
  - File: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceTrainCarOnRailProtocol.cs`
- Server domain already has car attach APIs:
  - `TrainUnit.AttachCarToHead(...)`
  - `TrainUnit.AttachCarToRear(...)`
  - File: `moorestech_server/Assets/Scripts/Game.Train/Unit/TrainUnit.cs`
- There are unit tests for attach behavior at domain level:
  - `TrainUnitAddCarTest`
  - File: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainUnitAddCarTest.cs`
- Real-time network diff currently has train creation event only (`va:event:trainUnitCreated`).
  - File: `moorestech_server/Assets/Scripts/Server.Event/EventReceive/TrainUnitCreatedEventPacket.cs`
  - File: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/TrainUnitCreatedEventNetworkHandler.cs`
- For non-created train state changes, clients mostly recover by hash mismatch + snapshot resync.
  - File: `moorestech_server/Assets/Scripts/Server.Event/EventReceive/TrainUnitHashStateEventPacket.cs`
  - File: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/View/TrainUnitHashVerifier.cs`

### Current state gap (what is missing)
- No client operation exists to "attach new car to existing train".
- No protocol exists to request attach-by-target-train.
- No immediate broadcast event exists for "existing train updated" (car count / rail position changed).
- If attach is implemented server-side without update event, other clients will lag until hash resync.

### Required domain knowledge before implementation
- `AttachCarToHead/Rear` requires strict rail-position overlap conditions:
  - `AttachCarToRear`: added-car `RailPosition` head must overlap train rear.
  - `AttachCarToHead`: added-car `RailPosition` rear must overlap train head.
- The attach methods currently throw `InvalidOperationException` when overlap is invalid.
  - Protocol layer should pre-validate and avoid triggering exceptions in normal flow.
- Existing remove protocol resolves target train by `TrainCarId`.
  - Same targeting pattern can be reused for attach (stable and already used).
  - File: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveTrainCarProtocol.cs`

### Recommended request model (clean and minimal)
- Add a new protocol instead of overloading `va:placeTrainCar`.
- Suggested tag:
  - `va:attachTrainCar`
- Suggested request fields:
  - `RailPositionSnapshotMessagePack RailPosition`
  - `int HotBarSlot`
  - `int PlayerId`
  - `Guid TargetTrainCarId` (used to resolve target `TrainUnit`)
  - `AttachSide Side` (`Head` / `Rear`)
- Suggested response fields:
  - `bool Success`
  - `AttachTrainCarFailureType FailureType`

### Recommended server flow for attach request
1. Validate request payload and inventory item.
2. Resolve train-car master from held item (`ItemId` -> train-car master).
3. Restore and validate rail snapshot with expected length (reuse current place validation style).
4. Resolve target train by `TargetTrainCarId` from registered trains.
5. Validate attach preconditions (without throw):
  - overlap condition for selected side
  - optional operation policy: reject while moving, etc. (decide explicitly)
6. Create new `TrainCar`.
7. Execute attach (`AttachCarToHead` or `AttachCarToRear`).
8. Consume inventory item only after successful attach.
9. Emit train-update diff event (recommended) for all clients.

### Recommended client operation flow
1. Detect placement preview as usual (existing rail snapshot generation can be reused first).
2. Select existing train target by click:
  - Use `TrainCarEntityChildrenObject` hit to get `TrainCarId`.
  - File: `moorestech_client/Assets/Scripts/Client.Game/InGame/Entity/Object/TrainCarEntityChildrenObject.cs`
3. Decide side (`Head` / `Rear`) in UI/operation rule.
4. Send new attach request via `VanillaApiWithResponse`.
5. Handle failure reason for UX feedback.

### Synchronization impact (important)
- Current network path has only `TrainUnitCreated` immediate event.
- Attach modifies an existing train, so immediate update path must be added:
  - Either:
    - add `TrainUnitUpdatedEventPacket` with snapshot bundle, or
    - return snapshot in protocol response and also broadcast equivalent event for other players.
- Relying only on hash resync is technically workable but causes delayed visual/state update.

### Concrete file touch map for attach feature

#### Client
- `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs`
  - add `AttachTrainCar(...)` request method.
- `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs`
  - optional send-only variant if needed.
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainCar/TrainCarPlaceSystem.cs`
  - branch between new-train place and attach-to-existing-train.
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainCar/TrainCarPlacementDetector.cs`
  - extend hit model to carry attach target + side decision input.
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/*`
  - add handler if a new "train updated" event is introduced.

#### Server
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/AttachTrainCarProtocol.cs` (new recommended)
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`
  - register new protocol tag.
- `moorestech_server/Assets/Scripts/Server.Event/EventReceive/*`
  - add/update event packet for existing-train update (recommended).
- `moorestech_server/Assets/Scripts/Server.Util/MessagePack/*`
  - add request/response/event payload classes if needed.

### Tests to add before merge

#### Server packet tests
- Attach to rear succeeds with valid overlap.
- Attach to head succeeds with valid overlap.
- Invalid target train car id fails.
- Invalid rail snapshot fails.
- Not enough item count fails.
- (If policy added) moving-train attach rejected.

#### Sync tests
- Attach operation updates all clients without waiting for hash resync.
- Client cache and train-car objects reflect new car count/order after attach.

### Non-negotiable design decisions to lock early
- Target selection key: `TargetTrainCarId` (recommended) or `TrainId`.
- Side selection source: explicit user input vs auto side by proximity.
- Runtime policy: whether attach is allowed while train is moving/docked/autorun.
- Update transport: dedicated update event vs hash-only delayed correction.

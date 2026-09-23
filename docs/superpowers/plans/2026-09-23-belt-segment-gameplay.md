# Belt segment gameplay integration Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モードは同スキルの規模ゲートに従う。既に承認された本体checkoutを使い、3つの実装タスクを逐次実行する。全体goalは録画した実ゲームとPRまで継続する。

**Goal:** 新規ワールドで機械、segment、差分同期、GPUの箱表示、保存・復元が一緒に動く試作を完成させる。

**Architecture:** Game.Blockのworld ownerが確定した接続から共通Coreを構築し、MasterTickUpdaterが逐次駆動する。機械の既存同期挿入を記録した完全tickを送り、clientが同じCoreとGPUを進める。変更時はCPU/GPUを全置換し、新形式のセル状態を保存する。

**Tech Stack:** Unity 6000.3.8f1、C#、UniRx、MessagePack、Newtonsoft JSON、URP、ComputeShader、既存Game.BeltSegment。

## Requirements

| ID | Requirement and acceptance |
|---|---|
| R1 | Dropbox `E:/Dropbox/seg/mock/8/Core/README.md` とADR0069を正本とし、既存接続判定の実際のConnectedTargetsから最大segmentを作る。直線・曲がり・坂・合流・分岐をworld fixtureで検証する。 |
| R2 | D14/D21: 全game beltは16 units/tick（調整用agent既定値、1.25 cells/s）で逐次実行。無動力でも搬送、負荷は占有によらず一定。供給、空/走行/buffer各状態で比較する。 |
| R3 | D9: 実機械の搬入搬出はsegmentに対して行い、成功時のみ所有権移動。source chest→belt→destination chestで個数とidentityを追う。 |
| R4 | D11/D12: 最小座標で切った単純輪は1本のNormal self-link。前tickの実際の空きだけを参照し、満杯輪は停止。保存復元・配置順違いで同じ切れ目になる。 |
| R5 | D13: 全再構築はrunning itemを新経路に載せ直し、必要な後続だけ同じセル内で後方へずらす。生存セルのidentity/個数/順序を保持。撤去セルは既存refund、消えたjunction bufferはCoreの破棄仕様。 |
| R6 | D17: v3新形式を保存・復元し、moving item、junction RR/buffer、輪の切れ目を保持。旧形式は明示的に拒否し既存ファイルを変えない。 |
| R7 | tickとseqの意味を維持する。tickは物理更新、seqは同tick内順序でtickごとにreset。初期snapshot+event+client購読を実装し、重複/順序逆転/欠落/古い応答/空graphを検証する。trainの実装・番号は変更しない。 |
| R8 | CPU/GPUは同じ確定frameを1回適用。初回・世代変更は両方全再構築。server/client item identity・距離・RR・buffer、CPU/GPU kind・距離・entryを照合。 |
| R9 | D19/D20: 20Hzの確定位置を画像付きcubeでGPU instancing。走行列だけ表示し、reference同様bufferは内部状態。補間/個別modelは次回。実camera録画で曲がり/坂/複数種類/merge entryを確認。 |
| R10 | D7/D16/D18: 旧item beltと専用test、filter splitterを廃止。水平4方向と坂をUI/preview/server/blueprintで一致させ、他blockの向きを変えない。 |
| R11 | 全量転送は初回/再構築/復旧のみ。通常tickに全itemのCPU→GPU位置upload/readbackをしない。既存Core benchmarkに加えreplay/packingの実行コマンド・alloc/bytesを残す。 |
| R12 | unity-playmode-recorded-playtestで実UI設置、実チェスト搬送、輪、撤去・再構築、新規save/reloadを通す。計算testだけで完成扱いしない。 |

## Global Constraints

- 正本repoは `C:/Users/5080/Documents/GitHub/moorestech`。外部scratchからこのplanを `docs/superpowers/plans/2026-09-23-belt-segment-gameplay.md` に保存するのはGPU review owner解放後。masterの確認済みrevisionは `3fecf603f17897c96a34bd3a84863c220c993b64`、着手前再確認。
- D14–D22の原文は `C:/Users/5080/Documents/ChatGPT/segment-normal-diagnostics/user-steering-d14-d15.md` とADR0069。通常の型/メソッド選択はagent判断。重大な挙動/範囲/工数/手戻り分岐は実装前にユーザーへ質問する。
- AGENTS.md: 200行/file、10 code files/directory、partial/Func禁止、Actionへの置換禁止、UniRx通知、Unity YAML/meta手書き禁止、Library削除禁止。C#変更はcompile。境界以外のtry-catch禁止。
- 無関係dirtyを維持: client `.uloop/project-runner-pin.json`、client `Client.Localization/_CompileRequester.cs`、client `ProjectSettings/ShaderGraphSettings.asset`、server `Game.Block.Interface/Component/ConnectOverride.meta`。server `Core.Master/_CompileRequester.cs` の既存dirtyはdummyText nonceのみと確認済み。Task3のschema変更直前に差分を再確認して記録し、edit-schemaの必須trigger変更を意図してcommitする。それ以外の編集があれば分離して保持する。Task1/2では従来どおりこのmarkerも維持する。
- 全般的な差分通知refactor、旧save移行、実ゲーム並列化、補間、custom modelは今回のタスクに足さない。Coreのparallel/explicit speed APIを削除しない。
- 既存Core/GPUの計算はtest済み。今回のmetadata追加がその計算を変えないことを同じsuiteで確認する。
- 各taskは下記Interfacesを守り、publicは外部assemblyの利用者がいるものだけ。テスト観測は既存friend assemblyまたは `#if UNITY_EDITOR`、公開debug APIは追加しない。

## 配置と前例

| Component / mechanism | Assembly and precedent |
|---|---|
| `BeltWorldDatastore`、topology、機械adapter、cell save | Game.Block。`Blocks/Fluid/FluidNetworkDatastore.cs`、`FluidPipeComponent.cs`、`FluidPipeSaveComponent.cs`。登録購読+中央tick駆動。 |
| `IBeltWorldLookup` / `IBeltWorldMutation` | Game.Block内、前者はsnapshot protocol/event、後者はcomponent/template/中央tickで使用。変更側をstatic公開しない。 |
| `IBlockOutputAvailability` | Game.Block.Interface。実出力所有者が実装する読取専用component役割。Core.Inventoryへbelt判定を入れない。 |
| route/clock/snapshot/frame値、state hash | Game.BeltSegment。Unity/Block/inventory非依存。serverとclientが同じCoreの境界値を共有。 |
| JSON cell保存とV2→V3境界 | domain JSONはGame.Block、version/chainはGame.SaveLoad。MessagePackを保存に転用しない。 |
| belt DTO / event / request | Server.Util/MessagePack/BeltSegment、Server.Event/EventReceive、Server.Protocol/PacketResponse。`TrainUnitTickDiffBundleEventPacket` とsnapshot protocolの3点セットに従う。Server.Util→Game.Block参照を作らない。 |
| client subscription / recovery / draw | Client.Game/InGame/BeltSegment、Client.Networkのrequest facade、Client.Starter DI。trainのmodel/network配置に従う。 |
| GPU visibility | GPU replayと同じownerでtopology資源を置換。既存entity表示のskit非表示加入を維持する。毎frameのdrawは表示駆動、進行はaccepted tickのみ。 |

データフロー: world更新→connector dictionaries→world graph→Core tick→既存machine Updateの同期挿入→確定frame→packet→client CPU/GPU→表示。新componentはworldへの書き手、client viewは読者。旧beltごとのUpdateを中央tickへ統合するのはユーザー要求segment化のため。機械Updateの第二のpull実装を作らない。

| Existing operation | After integration | Evidence/owner |
|---|---|---|
| 通常/drag設置、回転、Q/E高さ、坂 | 存続 | 同じplace system、D18によりbeltの純上下向きだけ削除 |
| blueprint/copy/replace/remove | 存続 | server共通orientation判定、同じrefund protocol |
| 機械inventory/clean-room集計/train platform搬出 | 存続 | 各ownerのUpdateとInsertItemを保持 |
| filter splitter設定/設置 | 廃止 | D16、master参照も除去 |
| 歯車接続/既存overload機構 | 存続 | transportの速度/占有負荷だけD14変更 |
| 開始スキット中world非表示、通常HUD/camera | 存続 | draw visibilityを既存skit control群へ登録 |
| 新規save/reload | 存続 | v3、旧形式拒否はD17 |

## Cross-task value contracts

Pure values are placed under `Game.BeltSegment/World/` (no Unity/UniRx dependency). Constructors own detached arrays and validate external shape in the wire decoder. These signatures are implementation contracts, not claims that the types already exist.

```csharp
public readonly struct BeltStreamPosition
{
    public readonly ulong Tick;
    public readonly uint Sequence;
    public BeltStreamPosition(ulong tick, uint sequence) { Tick = tick; Sequence = sequence; }
}
public readonly struct BeltRouteCell
{
    public readonly BeltCell Cell;
    public readonly BeltEntryDirection Entry;
    public readonly int CenterHeightTwice;
    public readonly int InputHeightTwice;
    public BeltRouteCell(BeltCell cell, BeltEntryDirection entry,
        int centerHeightTwice, int inputHeightTwice)
    { Cell = cell; Entry = entry; CenterHeightTwice = centerHeightTwice; InputHeightTwice = inputHeightTwice; }
}
public sealed class BeltRoute
{
    public readonly BeltRouteCell[] Cells;
    // Four upstream route cells indexed by BeltDirection; wiring validates used entries.
    public readonly BeltRouteCell[] EntryCells;
    public BeltRoute(BeltRouteCell[] cells, BeltRouteCell[] entryCells)
    { Cells = cells; EntryCells = entryCells; }
}
public sealed class BeltWorldSnapshot
{
    public readonly BeltStreamPosition Position;
    public readonly ulong Generation;
    public readonly BeltReplaySnapshot Simulation;
    public readonly BeltRoute[] Routes;
    public BeltWorldSnapshot(BeltStreamPosition position, ulong generation,
        BeltReplaySnapshot simulation, BeltRoute[] routes)
    { Position = position; Generation = generation; Simulation = simulation; Routes = routes; }
}
public sealed class BeltWorldFrame
{
    public readonly BeltStreamPosition Previous;
    public readonly BeltStreamPosition Position;
    public readonly ulong Generation;
    public readonly uint PreviousHash;
    public readonly BeltReplayTick Replay;
    public BeltWorldFrame(BeltStreamPosition previous, BeltStreamPosition position,
        ulong generation, uint previousHash, BeltReplayTick replay)
    { Previous = previous; Position = position; Generation = generation; PreviousHash = previousHash; Replay = replay; }
}
```

`BeltCell` axes remain Core X/Y horizontal, Z elevation. Unity maps `(X,Z,Y)`. Segment IDs index the route/simulation arrays for one generation only. All generated route entry arrays have exactly4 entries; decoder rejects missing entries, invalid direction or route-length/capacity mismatch, duplicate IDs, invalid distance/spacing, and nonpositive insertion length. Unused entry cells never determine connectivity: actual inputs/links do.

Route geometry separates logical ownership from surface height. `CenterHeightTwice` is absolute2*y for flat,2*y+1 for Up/Down. `InputHeightTwice` is2*y for flat/Up,2*y+2 for Down, from the same slope rule used by BeltConnectionGeometry. An external machine entry uses its actual connected port height and adjacent horizontal cell, not the machine origin. At rendering progress0/0.5/1 use upstream center/shared port boundary/current center respectively, so a slope does not start rising on the preceding flat cell. Geometry tests fix these three positions for both slopes; logical ItemPosition and save progress stay unchanged.

Tick/order example: end tick40 has `(40,1), generation7`. A world change at tick40 end makes dirty. Before tick41 physical work, rebuild emits a replacement `(40,2), generation8`, then hash the replaced state. Tick41 frame has Previous `(40,2)`, Position `(41,1)`, generation8 and hash of that exact prior state. No rebuild: Previous `(40,1)`. A recovery snapshot copies the current completed position without consuming a sequence. Sequence resets on physical tick advance. Requests execute on the existing tick-end packet FIFO (ReceiveQueueProcessor/TickEndPacketQueue); if topology is dirty, they return the coherent completed graph and its old routes/generation, and the next tick's replacement advances it. A read-only request must not rebuild the graph midway through the world's mutation queue. Save captures current surviving cells separately and does not advance physics. After world load, use the existing post-load initialization point to rebuild restored graph and establish `(loadedTick,0)` before ticks/requests; empty initial world is valid. Tests cover two requests at the same position, dirty placement then request then next-tick rebuild, loaded global tick beyond uint.MaxValue, and sequence reset.

## Task 1: Connect the real world, machines, rebuilding and new saves

**Files — create (server `moorestech_server/Assets/Scripts/` prefix):**
- `Game.Block/Blocks/BeltConveyor/World/BeltWorldDatastore.cs`: registration, boundary, graph/payload ownership.
- `Game.Block/Blocks/BeltConveyor/World/IBeltWorldLookup.cs`, `IBeltWorldMutation.cs`: split role contracts.
- `Game.Block/Blocks/BeltConveyor/World/BeltWorldTickUpdater.cs`: readiness, graph.Tick(false), completed event.
- `Game.Block/Blocks/BeltConveyor/World/BeltTopologyBuilder.cs`, `BeltTopologyRoutes.cs`: canonical connector graph and maximal paths.
- `Game.Block/Blocks/BeltConveyor/World/BeltWorldRebuild.cs`, `BeltWorldItems.cs`: remap/RR/payload and cell capture.
- `Game.Block/Blocks/BeltConveyor/Ports/BeltMachineSource.cs`, `BeltMachineReceiver.cs`, `BeltMachinePortTable.cs`: fixed per-edge adapters/context keys.
- `Game.Block/Blocks/BeltConveyor/Components/SegmentBeltComponent.cs`, `SegmentBeltSaveComponent.cs`: IBlockInventory and per-cell persisted state.
- `Game.Block/Blocks/BeltConveyor/Save/BeltCellSaveState.cs`, `BeltSavedItem.cs`: JSON primitives, Item master GUID, transport GUID, progress/entry, retained RR and optional buffer. ItemInstanceIdは既存IItemStack契約どおりruntime identityで、transport Guidとは別。
- `Game.Block.Interface/Component/Inventory/IBlockOutputAvailability.cs`: `IBlockComponent` role with `bool HasOutputItem()` and no mutation. Existing Component root already has19 code files; use the Inventory subdirectory.
- `Game.BeltSegment/World/BeltStreamPosition.cs`, `BeltRoute.cs`, `BeltWorldSnapshot.cs`, `BeltWorldFrame.cs`.
- `Game.BeltSegment/Replay/BeltStateHash.cs`: deterministic integer/Guid hashing helpers called by Core owners without snapshot allocations.
- `Game.SaveLoad/Migration/Steps/SaveMigrationStepV2ToV3.cs`.
- Tests under `Tests/CombinedTest/Game/BeltSegmentWorld/`: `BeltWorldTopologyTest.cs`, `BeltWorldMachineTest.cs`, `BeltWorldRebuildTest.cs`, `BeltWorldSaveTest.cs`, `BeltWorldTickTest.cs` and shared `BeltWorldFixture.cs`.

**Files — modify:**
- `moorestech_server/Assets/Scripts/Game.BeltSegment/Items/BeltItem.cs`, `Simulation/BeltConveyorSegment.cs`, `Simulation/BeltItemQueue.cs`, `Simulation/BeltBuffer.cs`, `Replay/BeltSimulationGraph.cs`, `Replay/BeltReplaySimulation.cs` (entry metadata/hash ownership).
- `moorestech_server/Assets/Scripts/Game.Block/Game.Block.asmdef`, `Game.Block.Interface/Game.Block.Interface.asmdef` only as actual types require; no cyclic references.
- `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/Transport/VanillaBeltConveyorTemplate.cs`, `VanillaGearBeltConveyorTemplate.cs`, `Factory/VanillaIBlockTemplates.cs`.
- `moorestech_server/Assets/Scripts/Game.Block/Component/ConnectOverride/BeltConnectionOverride.cs` and existing connection tests using the old component.
- `moorestech_server/Assets/Scripts/Game.Block/Blocks/BeltConveyor/GearBeltConveyorComponent.cs`: remove per-belt Update/occupancy dependency; retain constant gear load.
- Real output owners: `Game.Block/Blocks/Chest/VanillaChestComponent.cs`, `Blocks/Machine/Inventory/VanillaMachineBlockInventoryComponent.cs`, `Blocks/Miner/VanillaMinerProcessorComponent.cs`, `Blocks/MapObjectMiner/VanillaGearMapObjectMinerProcessorComponent.cs`, `Blocks/CleanRoom/CleanRoomItemHatchComponent.cs`, `Blocks/TrainRail/ContainerComponents/TrainPlatformItemContainerComponent.cs`.
- `moorestech_server/Assets/Scripts/Server.Boot/MasterTickUpdater.cs`, `MoorestechServerDIContainerGenerator.cs`; `Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfo.cs`; affected save-chain construction tests.
- Read-only timing precedents: `moorestech_server/Assets/Scripts/Server.Boot/Loop/PacketProcessing/ReceiveQueueProcessor.cs`, `TickEndPacketQueue.cs`; no new protocol thread/queue is needed.
- Legacy symbol callers needed for compiling this cutover are updated/deleted in this task. Actual client entity emission is removed in Task2; legacy classes cannot drive transport meanwhile.

**Interfaces / Produces:**

```csharp
// Game.Block owns Unity/inventory-specific roles; no client references these roles.
public interface IBeltWorldLookup
{
    BeltWorldSnapshot CaptureSnapshot();
    IObservable<BeltWorldSnapshot> OnRebuilt { get; }
    IObservable<BeltWorldFrame> OnFrame { get; }
}
public interface IBeltWorldMutation
{
    void Register(SegmentBeltComponent component);
    void Unregister(SegmentBeltComponent component);
    void BeginTick(ulong tick);
    void CompleteTick();
    IItemStack Insert(SegmentBeltComponent target, IItemStack stack, InsertItemContext context);
    IItemStack GetCellItem(SegmentBeltComponent target);
    void SetCellItem(SegmentBeltComponent target, IItemStack stack);
    BeltCellSaveState CaptureCell(SegmentBeltComponent target);
}
public interface IBlockOutputAvailability : IBlockComponent
{
    bool HasOutputItem();
}
// Existing classes; new methods compute directly over their owned queues.
// BeltSimulationGraph and BeltReplaySimulation: public uint ComputeStateHash();
// BeltItem adds public BeltDirection AcceptedInput; set only on successful TryReceive.
```

- [ ] **Step 1: Implement canonical topology and detached boundary values.** Map actual connector edges including machine edges; sort cells lexicographically X/Z/Y in Unity (X/Y/Z Core), directions in Core enum order within each array, connector identity as final tie-breaker. Preserve Graph attachment groups Links → Outputs → Inputs; machine and internal ports are not interleaved. Never sort by allocation/registration order. A regular cell with >=2 incoming connections is a one-cell Merge; a cell with >=2 actual outgoing connections terminates its upstream maximal Branch path (D22); 2→1 removes its junction buffer, preserves running items under D13 and retains cell RR. Trace non-junction cells from boundary to next junction/machine/dead-end; remaining cycles use minimal cell as head and one Normal self-link. Validate impossible multi-output Merge shapes rather than silently dropping edges. Runtime IDs are rebuilt every generation; cell identity uses BlockInstanceId plus position, so replacement clears RR. No per-cell independent transport Update remains.

```csharp
// Core receive change, immediately before queue.EnqueueTail:
var accepted = item;
accepted.AcceptedInput = inputDirection;
queue.EnqueueTail(offer - length, accepted);
// Public hash methods delegate to owners; hash includes kind/speed/RR, ordered GUID/kind/entry/distance,
// buffer presence/value and ordered links/ports. Do not hash caller-owned ItemPosition references.
```

- [ ] **Step 2: Bind actual machine inventory ownership and central tick.** Discover all inventory-output owners by searching calls to ConnectingInventoryListPriorityInsertItemService; readiness must query actual output slots, never machine input/module slots. Implement IBlockOutputAvailability on those components and verify no connected source lacks the role. Source adapters freeze this bool before graph.Tick; receiver passes count-one registered payload to actual target InsertItem once and records only successful consumption. The old machine loop still writes the returned remainder; new component accepts one unit per successful Graph.TryInsert. Create unit Guid only after an offer is available, preserve original ItemInstanceId/metadata separately in the world registry. Remove registry entry only on successful external output, refunded cell removal or defined buffer loss. Real source can connect to several inputs: key by source block and both connectors, not block alone.

```csharp
// MasterTickUpdater after train and before its sorted ordinary block loop:
_beltWorldMutation.BeginTick(GameUpdater.CurrentTick);
foreach (var blockData in _tickOrderedBlocks) blockData.Block.TickUpdate();
_beltWorldMutation.CompleteTick();
// BeginTick: rebuild boundary if dirty; save prior position/hash; freeze sources; graph.Tick(false).
// CompleteTick: seal arrays of ReadyInputs/SuccessfulOutputs/ordered Insertions and emit frame once.
// Insert: resolve bound context, offer=graph.GetInputOffer(inputId); length=Math.Min(16, offer);
// reject unchanged stack when length<=0; on TryInsert success record insertion and return SubItem(1).
```

Insertion outside the machine-update window (explicit SetItem/editor/external inventory operations) is a boundary state mutation: invalidate capture, enqueue a full replacement before the next physical tick. Never hide it from client replay. Normal gameplay sources use the recorded window. `GetSlotSize()` is1 and `GetItem(0)` returns only this cell's running payload for refund; buffer is not another inventory slot. `SetItem` must remove/replace only that cell's running item and mark boundary dirty, not create a second transport owner. `InsertionCheck` is nonmutating and cannot claim context-specific merge reservation; definitive InsertItem returns the actual remainder.

- [ ] **Step 3: Reproject and save once per mutation epoch.** Decode each running item's owning cell from route length and distance. Capture cell map once, retain cell RR across temporary Normal role, retain buffer only when the same block still owns a junction buffer. Store loaded states on components until complete world reconstruction. For each new route order items from output to input, compute desired distance; set `distance=max(desired, priorDistance+256)` and assert it stays in that item's cell interval. Failure is visible, not deletion/clamping into another cell. Full capture is shared across all per-block GetSaveState calls until physical/insertion/topology mutation.

```csharp
int cellIndex = route.Cells.Length - 1 - distance / 256;
int progress = 256 - distance % 256;
// New route with the same owned cell:
int desired = (newRoute.Cells.Length - 1 - newCellIndex) * 256 + (256 - progress);
int placed = Math.Max(desired, previousDistance + 256);
// interval: base <= placed < base+256; first item uses desired without previous-distance term.
```

JSON is detached: transport Guid, item master Guid, progress, entry and RR. Existing ItemStackSaveJsonObject stores item kind/count, and IItemStack explicitly marks ItemInstanceId runtime-only and metadata unsupported; do not add a new item metadata persistence subsystem here. During live transport keep the count-one stack from `stack.SubItem(stack.Count - 1)` so its supported payload behavior follows existing inventory operations. The separate transport Guid survives saves. Route cut is derivable from saved block positions with the exact comparator; test equality after load. v3 chain registers V2→V3 returning `SaveMigrationStepResult.Failed` with a new-world prototype explanation. Existing v1→v2 tests remain valid individually; full v1/v2→current refusal is intentional. New saves have required new component keys. Save after a tick-end removal must exclude removed/refunded cells even if next physics rebuild has not run.

- [ ] **Step 4: Focused verification and commit.** Compile; run new world tests and existing Core+connection tests once. Test source/destination count, three merge inputs with only lower priority ready, two ports on one source, blocked receiver, no energy, constant load empty/full/buffer, loop partial/full, D13 256-vs32 regression, block replacement, disconnected belts, zero belts, immediate save after removal, immutable save capture and v3 reload. Repeat only changed/failing scope. Commit task paths including Unity-generated metadata with `Task 1:` prefix.

## Task 2: Deliver complete ticks and replace the client simulation

**Files — create:**
- `moorestech_server/Assets/Scripts/Server.Util/MessagePack/BeltSegment/BeltWorldSnapshotMessagePack.cs`, `BeltWorldFrameMessagePack.cs`, `BeltReplayStateMessagePack.cs`, `BeltReplayTickMessagePack.cs`, `BeltRouteMessagePack.cs`, `BeltWireCodec.cs`.
- `moorestech_server/Assets/Scripts/Server.Event/EventReceive/BeltWorldEventPacket.cs`.
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GetBeltWorldProtocol.cs` (only IPacketResponse here; DTOs elsewhere).
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BeltSegment/Network/BeltWorldEventHandler.cs`, `BeltWorldRecovery.cs`.
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BeltSegment/Model/ClientBeltWorld.cs`, `BeltFrameBuffer.cs`, `BeltStreamStatus.cs`.
- tests `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BeltWorldProtocolTest.cs`; `moorestech_client/Assets/Scripts/Client.Tests/BeltSegment/Network/BeltStreamOrderTest.cs`, `BeltSnapshotRaceTest.cs`, `BeltWireRoundTripTest.cs`.

**Files — modify:**
- `moorestech_server/Assets/Scripts/Server.Util/Server.Util.asmdef`, `Server.Protocol/PacketResponseCreator.cs`, `Server.Boot/MoorestechServerDIContainerGenerator.cs`.
- `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs`, `Client.Network/Client.Network.asmdef`, `Client.Starter/Registration/MainGameModelRegistration.cs`, `MainGameInteractionRegistration.cs`.
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RequestWorldDataProtocol.cs`, `PacketResponse/Util/CollectBeltConveyorItems.cs` and its other callers: stop old belt entity production.
- GPU metadata: `moorestech_client/Assets/Scripts/Client.Game/InGame/BeltSegment/Gpu/GpuBeltData.cs`, `GpuBeltInitialState.cs`, `GpuBeltTickUpload.cs`, `GpuBeltBuffers.cs`; `moorestech_client/Assets/Resources/BeltSegment/BeltGpuData.hlsl`, `BeltGpuQueue.hlsl`, `BeltGpuPorts.hlsl`, `BeltGpuNormal.hlsl`, `BeltGpuExternal.hlsl`; GPU tests/readback helpers.

**Interfaces — consumes:** Task1 pure BeltWorldSnapshot/BeltWorldFrame, lookup events and Core.ComputeStateHash; existing GpuBeltSimulation(snapshot, shader), ApplyTick(BeltReplayTick), Dispose().

**Interfaces — produces:**

```csharp
// Client.Network API method (DTO envelope stays inside API):
UniTask<BeltWorldSnapshot> GetBeltWorld(CancellationToken cancellationToken);
// Client.Game, internal service; constructor receives compute shader and normal DI dependencies.
// void ReceiveSnapshot(BeltWorldSnapshot snapshot);
// void ReceiveFrame(BeltWorldFrame frame);
// IObservable<BeltWorldSnapshot> OnRebuilt;
// IObservable<Unit> OnAdvanced;
// BeltStreamStatus Status (WaitingSnapshot, Running, Recovering).
// GpuBeltSimulation Simulation; BeltRoute[] Routes; BeltStreamPosition Position; ulong Generation.
```

- [ ] **Step 1: Encode detached state and register event+initial response.** Follow creating-server-protocol skill: Request/Response envelope keys start2, event payload keys start0, serializer constructor marked Obsolete, typed ItemId and enums on wire. Codec validates entire payload before constructing CPU/GPU state. Require bounded nonnegative counts, valid IDs/kinds/directions, spacing, unique per-generation external IDs and array lengths; no silent defaults for required fields. Convert Core's int kind at the codec boundary to existing ItemId; master Guid travels in JSON. Snapshot positions are value copies; never serialize live ItemPosition references. Events use `va:event:beltWorldSnapshot` and `va:event:beltWorldFrame`; request tag `va:getBeltWorld`. Event packet subscribes once through IBootInitializable.Load and broadcasts through existing provider. Request reads current completed lookup snapshot on the server's existing packet boundary.

- [ ] **Step 2: Subscribe before initial request and reconcile ordered frames.** Buffer by `(tick,seq)` while WaitingSnapshot/Recovering; impose bounded frame count (256) and request fresh full state if exceeded, recording recovery reason. Only one request in flight. Response generation/position older than accepted state is discarded. Apply a snapshot as one CPU+GPU replacement after both new objects construct; then dispose replaced GPU resources and drain frames whose Previous exactly equals accepted Position. Duplicate covered frames are discarded. Same-generation future frames queue; generation mismatch waits for its replacement; missing chain/hash mismatch triggers one recovery. New valid snapshot clears recovery and resumes, including zero-segment states. Failed/invalid response leaves an explicit recovery error. PacketExchangeManager supplies timeout/completion/cancellation but no generic retry facility; BeltWorldRecovery owns one cancelable delayed retry operation using that API. Release request ownership on all completion paths; no network/train refactor or permanently latched request flag.

The handler implements `IInitialEventApplyWaitTarget` and registers the same instance as entrypoint/self/wait role. Join `MainGameInitializationFinalizer`'s existing startup wait; settle only after coherent CPU/GPU initialization, including an empty graph. A valid event snapshot can satisfy startup while an older request is pending; its later response must not roll state back. Terminal initial decode/apply failure and lifecycle cancellation settle the waiter visibly without throwing through `VanillaApiEvent.InitializeDispatch`'s synchronous event replay. Transient timeout can retry; later recovery never resets the initial completion source. Use the actual `PacketExchangeManager` timeout (10 seconds), local bounded-backoff delay and lifecycle cancellation, and report the chosen tuning. Accepting an event snapshot does not itself free an outstanding request slot; an old completion cannot clear a newer operation's gate.

An exception during CPU-then-GPU apply can leave one object partially advanced. Enter Recovering and suppress OnAdvanced; the Task3 renderer must not draw that partial state. Construct both replacement objects before committing ownership and dispose uncommitted GPU resources on failure. Validate external frame shape before mutation.

```csharp
// The acceptance operation has one owner, ClientBeltWorld:
if (frame.PreviousHash != cpu.ComputeStateHash()) { RequestRecovery(); return; }
cpu.ApplyTick(frame.Replay, false);
gpu.ApplyTick(frame.Replay);
position = frame.Position;
advanced.OnNext(Unit.Default);
// Validation/apply exception is isolated at the untrusted network boundary, logged, and forces recovery.
// Do not publish advanced or draw a partially applied tick after failure.
```

- [ ] **Step 3: Propagate accepted input metadata through GPU transfers.** GPU running item buffer becomes packed kind+direction (`int2`,8 bytes, Marshal.SizeOf matching HLSL). Successful target Receive assigns its input direction. Hidden buffer/staged transfers may keep kind-only because the next target Receive assigns direction before drawing; initial running snapshot must preserve its accepted direction. External event resolves direction from its wired input, so no redundant event payload field or extra per-phase UAV is needed. Existing26 GPU cases still assert kind/distance; add merge selected-direction and restored-snapshot parity. GPU does not need Guid/payload registry.

- [ ] **Step 4: Validate race/recovery and commit.** Tests roundtrip initial+100 ticks, reload tick>uint.MaxValue, seq reset, duplicate/gap/reorder, replacement before/after request, stale response, replacement to empty graph, first belt after empty, consecutive topology generations, invalid buffer/IDs, hash mismatch recovering to real later snapshot and cancellation. Startup tests cover empty-state completion, malformed initial payload failure without aborting other event dispatch, timeout retry/single-flight cancellation, and a newer event winning over the pending initial response. Assert client/server Core state at same tick and CPU/GPU selected direction, plus suppression of partially applied state until recovery. Compile, focused Core/GPU/network suites, commit `Task 2:`.

## Task 3: Draw the live graph, finish the cutover and record gameplay

**Files — create:**
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BeltSegment/Rendering/BeltItemRenderer.cs`, `BeltDrawBuffers.cs`, `BeltDrawLayout.cs`, `BeltItemMaterials.cs`, `BeltDrawDispatch.cs`.
- `moorestech_client/Assets/Resources/BeltSegment/Rendering/BeltItemDraw.compute`, `BeltDrawGeometry.hlsl`, `BeltItemInstanced.shader`.
- `moorestech_client/Assets/Scripts/Client.Tests/BeltSegment/Rendering/BeltDrawPositionTest.cs`, `BeltDrawResourceTest.cs`.
- `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/belt-segment-gameplay.cs`, `belt-segment-reload.cs`.
- `docs/belt-segment-gameplay-validation.md`: actual command/results, artifacts, measured limits and next-iteration decisions.

**Files — modify/delete:**
- client `Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Parts/BeltConveyorInputControl.cs`; authoritative `Server.Protocol/PacketResponse/PlaceBlockProtocol.cs` and blueprint common place-validation path; placement family orientation tests.
- client `Client.Game/InGame/BlockSystem/StateProcessor/GearBeltConveyorStateChangeProcessor.cs`: inspected current class has no animation Update; do not invent an RPM-animation change here. Verify actual belt materials during playtest, preserve other gear processors.
- client `Client.Starter/Registration/MainGameModelRegistration.cs`, `MainGameInteractionRegistration.cs`, `Client.Game/Skit/SkitWorldObjectControlGroup.cs` registrations as needed for existing visibility role.
- Delete old belt movement/inserter/item entity classes and dedicated tests after full symbol sweep: server `Game.Block/Blocks/BeltConveyor/VanillaBeltConveyorComponent.cs`, `VanillaBeltConveyorInventoryItem.cs`, `VanillaBeltConveyorBlockInventoryInserter.cs`, `IItemCollectableBeltConveyor.cs`; `Game.Entity.Interface/EntityInstance/BeltConveyorItemEntity.cs`; client `Client.Game/InGame/Entity/Factory/BeltConveyorItemEntityObjectFactory.cs`, `Object/BeltConveyorItemEntityObject.cs`, `Object/CustomModelBeltConveyorItemEntityObject.cs`, `Object/Util/BeltConveyorItemPositionCalculator.cs`. Remove corresponding factory/DI cases. Keep path geometry assets used by belt models unless verified unused.
- Filter removal: server `Game.Block/Blocks/FilterSplitter/VanillaFilterSplitterComponent.cs`, `FilterSplitterMode.cs`, `FilterSplitterBlueprintSettingsJsonObject.cs`; `Factory/BlockTemplate/Transport/VanillaFilterSplitterTemplate.cs`; `Server.Protocol/PacketResponse/FilterSplitterStateProtocol.cs`; client `Client.WebUiHost/Game/Actions/FilterSplitterActions.cs`; all symbol-bound web/blueprint/settings registrations. Dedicated FilterSplitter tests removed; new Core Branch stays.
- `VanillaSchema/blocks.yml` のFilterSplitter enum entryとblockParam case、server `Core.Master/_CompileRequester.cs`、Tests.Moduleのtest master JSONを含む全symbol/GUID呼び出し。edit-schemaスキルとyaml_spec.mdに従い生成型は直接編集しない。schemaファイル自体の追加・削除はないためcsc.rspは変更不要。
- `moorestech_web/webui/src/` のfilter専用view/logic/registryと共有wire schema/types、対応するC#/TS wire fixture/tests。一般のinventory契約を維持する。
- `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json`, `buildMenu.json`, `research.json`, `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`: remove only FilterSplitter GUID `019e34c4-224f-777d-8312-2e94e92912ca` definition/reference. Push a separate branch and Draft PR, then update `.moorestech-external-revisions.json` to that pushed commit.
- `tools/BeltSegment/Benchmark/` existing benchmark project: add an explicitly labeled replay/packing scenario or separate mode using actual implementation; do not call CPU dispatch duration GPU execution time.
- `.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh`, `preflight.sh` and focused platform helper/tests if needed: accept native Windows absolute result paths and verify port availability using an available Windows/native mechanism. Missing `lsof` must not be interpreted as a free port. Keep the established preflight/boot/ready/scenario/result/recording/stop flow.

**Interfaces — consumes:** Task2 ClientBeltWorld accepted tick/rebuild events, GpuBeltSimulation.Buffers, pure routes. Item images from `ClientContext.ItemImageContainer.GetItemView(id).ItemTexture`. Renderer uses existing skit visibility interface; no new gameplay controls.

ItemImageContainer.GetItemView logs and returns null for a missing loaded view; EmptyItemId is invalid for a running item and rejected by wire validation. BeltItemMaterials checks the actual view/texture once at kind registration. Missing external texture uses one visible magenta cube material and emits a kind-specific diagnostic, preserving count and transport instead of throwing or hiding the item; normal pinned master playtest must have zero such diagnostics. Add a missing-image fixture for this boundary behavior. A valid texture creates the normal image material. Register item kinds densely from the loaded item master, so sparse numeric ItemId values cannot dictate buffer allocation; GPU grouping indexes this fixed mapping.

- [ ] **Step 1: Build GPU geometry and kind grouping.** Upload route cells/entry cells on rebuild. For each segment perform linear parallel prefix scan of gaps, producing each running item once; queue ring index must use actual head/capacity. Distances map via the Task1 cellIndex/progress formula; start is previous route cell, or route.EntryCells[item.AcceptedInput] for first cell/merge. Centerline slope interpolation uses cell heights. Initial cube edge is0.3 world units and center offset `(0.5,0.48,0.5)` from block origin in Unity, matching the reference's vertical offset and this game's corner-origin blocks. This visual tuning belongs in one draw-owner constant; verify actual slope/turn model alignment in recorded frames and correct that constant/geometry if it intersects the belt. Internal buffer emits zero instances. Group instances by item kind with GPU count→prefix→scatter and indirect arguments; shared capacity O(totalitems+kinds), no kind×capacity buffers. Kernels chunk dispatch above65535 groups and derive group sizes from shader. Rebuild releases all old buffers/materials/shader instances; initial zero graph uses zero draw counts.

```hlsl
// Geometry independent of drawing material; authoritative distance is integer.
uint cellIndex = routeLength - 1 - distance / 256;
float progress = (256 - distance % 256) / 256.0;
float3 point = progress < 0.5
    ? lerp(entryCenter, sharedPortBoundary, progress * 2)
    : lerp(sharedPortBoundary, currentCenter, progress * 2 - 1);
// GPU queue prefix computes distance for every item once, not one head scan per item.
```

- [ ] **Step 2: Issue URP instanced cube draws.** Reuse one cube mesh with UVs, one instanced material per actual item kind using the master image. Vertex shader reads compact world instance position via SV_InstanceID plus kind offset; indirect counts stay on GPU. Draw from ordinary render/update entry point with world bounds and active skit visibility. Recompute instance positions only after accepted tick/rebuild; draw current positions each frame. No synchronous GetData in production. Add assembly/test-only readback for fixed geometry assertions.

- [ ] **Step 3: Complete all old paths and master removal.** Resolve symbol callers by `rg` before deleting. Remove dedicated old movement/save/entity/filter tests; retain/refit generic family/connection/gear overload tests where their behavior still exists. Remove Shift vertical belt behavior and reject vertical belt placement through common server path used by blueprint; horizontal rotation and slopes still work. Parse all changed master JSON, assert four FilterSplitter GUID references become zero and unrelated research unlocks remain. Create/push/attach external Draft PR, update pin. No hand editing Unity metadata/YAML.

- [ ] **Step 4: Compile, draw tests and measured workload.** Test straight/corner/up/down, two merge input sides after snapshot restore, ring wrap,129th segment, two item kinds, buffer hidden, empty→nonempty→empty replacement and resource lifetime. Run focused draw/protocol tests while iterating. After all schema/code/JSON changes, run the full client-project test suite once as required by edit-schema, including imported server tests; record actual failures and scope. Run affected WebUI contract tests and the WebUI production build using the repo-native pnpm. Do not repeat already passed unchanged suites without a new concern. Benchmark warmup+fixed 10,000 ticks, report item/segment count, input/output event count, serialized bytes, CPU allocations and CPU replay/upload preparation timing separately; performance numbers are local observations, not guarantees. Reuse actual `GpuBeltTickUpload`/ABI sources for packing measurement when linking them into the existing .NET benchmark. Distinguish GPU upload bytes (event count × ABI stride) from actual MessagePack wire bytes; measure the real serializer in Unity if linking it would require fake master types. Do not substitute a synthetic wire format.

- [ ] **Step 5: Run unity-playmode-recorded-playtest and inspect artifacts.** Follow the skill, read-only scout `C:/Users/5080/Documents/ChatGPT/segment-normal-diagnostics/gameplay-playtest-scout.md`, and actual runner API. Use isolated new fixed world, ordinary camera/HUD, `SetupDebugEnvironment`, `SkipOpeningSkit`, UI placement; source conveyor chest `(1,32,0)` North→unpowered gear belt `(2,32,4)`→Up `(2,32,5)`→elevated straight `(2,33,6)`→Down `(2,32,7)`→east turns z8→north `(5,32,8)`→destination conveyor chest `(4,32,9)` North. Assert every actual connector, seed only source chest, wait for delivery. Add scout's two-source merge/branch and four-cell loop; populate then modify via real placement/removal protocol, assert refund accounting and accepted generation. Save through live SaveAndWaitWrittenAsync owner, stop/restart same fixed-world directory, compare saved identities/progress/RR/cut before reseeding, continue delivery. Record and visually inspect screenshots/video for slope/turn boxes, item textures, missing/duplicate entities, skit visibility and the current generic inventory/HUD. Retain a verified real-screen inventory/HUD image for the final Web-related PR screenshot requirement. Failure gets a concrete fix and rerun of affected scenario, not an optimistic pass.

The second scenario starts after ready.marker, by which time physics may have advanced. Exact moving progress is verified at the production load boundary by Task1's disk-roundtrip tests; do not compare later live progress to saved progress as though no tick elapsed. In the recorded reload verify topology/cut, conservation across machine inventories/running items/buffers, exact state of the full stopped loop, and resumed delivery. Use an existing load-boundary observation for exact moving-state comparison if available; otherwise distinguish those evidence scopes in the validation document without adding a production pause feature. Before execution, apply the Windows path/port corrections verified in `C:/Users/5080/Documents/ChatGPT/segment-normal-diagnostics/gameplay-integration-followups.md` and resolve Bash/Python/uloop paths explicitly.

```csharp
// Existing playtest DSL operations, actual route and inventory components:
await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
await p.SkipOpeningSkit();
await p.PrepareBlockForUiPlacement("木のコンベアチェスト", 2);
// Use existing UI operations for each route cell; assertions inspect actual ConnectedTargets.
// Source seeding is preparation; observed transport must execute real chest.Update/InsertItem.
```

- [ ] **Step 6: Commit code/scenarios and evidence.** `Task 3:` commit, no unsaved task edits. Validation document separates proven game behavior from future work, lists exact recordings/results and adjustable speed. Preserve old user worlds untouched.

## Task 4: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること

- [ ] Run automatically on the complete integration diff, not just last task; report expected/planned/recovered/missing coverage truthfully. Fix Critical/Important through appropriate agents. If fixes touch behavior/conditions/evaluation timing, rerun this plan's affected unityプレイ録画テスト on final binary. Do not repeat unchanged verification without a concrete concern.

## Task 5: セッション終了可能状態にすること

- [ ] Use pr-create, resolve actual master conflicts through the skill, compile after code conflict resolution, push and create Draft PR. Attach every PR including master-data PR with attach_artifact. Record complete task commits, validated heads and remaining limitations. Do not merge or deploy. Goal completion requires actual server/client/GPU/game/save path, not isolated computation suites.

## 判断記録（ADR）

- [ADR0069](../../adr/0069-belt-segment-simulation.md) and explicit D7/D9/D11–D22 govern behavior. D10 is superseded by D14; old implementation is not authority for occupancy-dependent load.
- Agent implementation choices: fixed game speed16, constant gear request rate1, per-cell JSON capture, typed belt stream preserving per-tick seq, pure shared boundary values, one integrated plan with3 implementation tasks. Do not label these user choices.
- Rationale for one plan: isolated Core/replay/GPU stages already exist; remaining tasks jointly produce the user's requested playable prototype. Splitting into more standalone computational PRs would not satisfy D15's stated priority.
- Retain original machine mutation ownership; add only output-readiness role, because generic inventory slots include machine input/module items and cannot truthfully reserve Merge input.
- Last accepted input direction is physical metadata required to reconstruct Merge entry after snapshot; RR alone cannot identify the winner. Existing ItemPosition remains caller-owned.
- New-format save refusal follows explicit D17, overriding general legacy migration scope for this prototype. It does not authorize deleting old files.
- GPU buffer invisibility matches supplied MyBeltConvSegment BuildPositions, which emits running queue count only. No new rule about inventory loss is introduced by display.
- Full game recorded validation is required because server and compute tests do not establish real rendering/placement/save integration. Review/PR tasks remain in this session under the explicit ongoing completion request.
- Self-review caught an incorrect globally monotonic seq assumption and corrected it to per-tick reset; removed an invented gear-animation Update change after inspecting the current class; limited item persistence to the actual existing item-save contract; split slope surface geometry from logical ownership to avoid raising items on an adjacent flat belt.
- Structure review found1 strong lookup-boundary trigger and1 weak shared-contract placement trigger. Agent resolved the lookup with one diagnostic fallback at the external image boundary. Keep pure snapshot/route/frame contracts in existing shared Game.BeltSegment with its graph/replay contracts; both CPU implementations already consume that assembly, so creating another Interface assembly adds no present separation benefit. These ordinary implementation decisions do not alter D14–D22 or claim user approval.
- Content self-review coverage: R1–R6 Task1; R7–R8 Task2; R9–R12 Task3, with Task1/2 focused tests feeding Task3's integration evidence. Request timing was corrected to the actual tick-end FIFO precedent: dirty read requests return a coherent prior graph; only the next boundary rebuild advances topology. This avoids read APIs mutating state and preserves existing placement/network timing.
- user-simulator review completed with new Critical0/Warning0/user-only choices0. It checked actual rulings and private corpus plus a bounded scout for collection operations; IItemCollectableBeltConveyor is entity collection for rendering, not an independently established hand-pick action. Fable unavailable: disclosed gpt-6-astra high fallback. No Critical meant no refuter invocation. User outcome score remains unconfirmed, not a claimed prediction hit.

- D22（2026-09-24）: 「出口が2本以上のときだけ分岐segmentにする（READMEの接続数に合わせる・推奨）」。Branchは実outdegree>=2。2→1のbuffer消失と走行列/RR保持をWorld regressionで固定する。

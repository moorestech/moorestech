# Electric/Gear Tick Boundary and Save Consistency Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 電力・歯車トポロジをdirty時だけtick先頭で線形再構築し、全クライアント操作と過負荷破断をtick末尾で確定した後、同じ世界時点を安全に保存する。

**Architecture:** 電力・歯車の変更履歴キューは廃止し、稼働中コンポーネントの登録表を正として全連結成分を`O(V+E)`で別マップへ構築して差し替える。受信パケットは全接続共通キューでtick末尾開始時に固定し、過負荷破断、固定パケット、Modのtick末尾処理、保存の順に実行する。保存要求は世代番号で集約し、ゲーム更新スレッド上でクリーンルーム形状を確定してからJSON化・アトミック書き込みする。

**Tech Stack:** Unity 6、C#、NUnit、UniRx、Microsoft.Extensions.DependencyInjection、MessagePack、Newtonsoft.Json、uloop

## Global Constraints

- 対象ブランチは`feature/ElectricTickUnification3`とし、全作業を同ブランチへコミットする。
- `master`のレシピ選択処理をすべて残し、給電メソッドだけ`SupplyExternalPower(float power)`へ解決する。
- 電力→歯車変換機の複数出力モードを維持する。
- 電力→歯車変換機は現在のバッテリー残容量だけを要求する。
- 歯車→電力変換機の発電0時表示値は今回の仕様・テスト対象に含めない。
- 電力・歯車の派生ネットワークを保存せず、明示的な電線・チェーン接続は各ブロック状態として保存する。
- ブロック種別と接続材料は`BlockGuid`・`ItemGuid`でJSON保存し、ロード時に`MasterHolder`で実行時IDへ解決する。`BlockId`・`ItemId`・`FluidId`を新しい永続値として保存しない。
- 出力モード配列、バッテリー容量、最大発電量などのマスタ定義値は保存せず、選択状態とバッテリー残量だけを保存する。
- Modの`OnLoad`前後に専用再構築や追加コールバックを設けない。ロード後最初の通常tick先頭で再構築する。
- テストだけが読む本番メソッド、プロパティ、フィールドを残さない。内部観測は`Tests/Util`のリフレクション補助へ置く。
- C#変更後は`uloop compile --project-path ./moorestech_client`を実行する。
- 新規・変更後の各C#ファイルは200行以下、`partial`・デフォルト引数・新規`try-catch`・C#標準`event/Action`通知を使用しない。
- 主要処理には日本語1行・英語1行の対コメントを約3〜10行ごとに置く。

---

## File Structure

### Production ownership

| File / type | Responsibility |
|---|---|
| `Game.EnergySystem/ElectricWire/ElectricWireNetworkDatastore.cs` | 稼働中コネクタ登録、electric dirty、完成済みマップの差し替え |
| `Game.EnergySystem/ElectricWire/ElectricWireTopologyMap.cs` | 全コネクタから電線連結成分を一度だけ構築 |
| `Game.Gear/Common/GearNetworkDatastore.cs` | 稼働中gear登録、gear dirty、再計算集合と完成済みマップの差し替え |
| `Game.Gear/Topology/GearNetworkTopologyMap.cs` | 全gearから歯車連結成分を一度だけ構築 |
| `Game.Gear/Topology/GearNetworkTopologyBuildResult.cs` | 新マップと再計算・継続tick・回転探索状態を交換前に一体完成 |
| `Server.Boot/Loop/PacketProcessing/TickEndPacketQueue.cs` | 全接続共通FIFO、tick境界での固定、保留tailの先頭戻し |
| `Server.Boot/Loop/PacketProcessing/ITickEndPacketEntry.cs` | 完了・保留を区別する接続固有パケット処理契約 |
| `Server.Boot/Loop/PacketProcessing/WorldMutationTickEndUpdater.cs` | キュー固定→過負荷予約破断→固定パケット実行の所有 |
| `Server.Protocol/TickEndPacketProcessResult.cs` | Protocol層からBoot層へ逆参照を作らず完了・保留を共有する結果型 |
| `Game.SaveLoad/WorldSaveCoordinator.cs` | 保存要求世代の集約、tick最終位相での単一保存実行 |
| `Game.SaveLoad.Interface/IWorldSaveRequest.cs` | 自動保存・手動保存が利用する要求専用契約 |
| `Game.CleanRoom/CleanRoomDetectionService.cs` | 通常budget処理と保存前全dirty batch処理で同じcarry-over経路を共有 |
| `Game.CleanRoom/CleanRoomDirtyBatchProcessor.cs` | dirty batch検出・旧新room照合・状態carry-overを担当して200行以下を維持 |
| `Server.Boot/DependencyInjection/*.cs` | 既存308行のDI生成を登録・materialize・tick配線に分割 |

### Test ownership

| File / type | Responsibility |
|---|---|
| `Tests/Util/EnergySystem/ElectricNetworkReflectionTestUtil.cs` | electric内部マップ・役割集合・非公開segment操作のテスト観測 |
| `Tests/UnitTest/Game/ElectricWireNetworkDatastoreFlushTest.cs` | dirty集約と一回再構築、安定tick no-op |
| `Tests/UnitTest/Server/TickEndPacketQueueTest.cs` | 全体FIFO、固定境界、切断skip、dirty保留 |
| `Tests/UnitTest/Game/SaveLoad/WorldSaveCoordinatorTest.cs` | 保存要求の集約・失敗維持・書き込み中要求維持 |
| `Tests/CombinedTest/Core/CleanRoom/CleanRoomPendingSaveTest.cs` | 部屋分割直後の保存で形状と汚染量が同時点になること |
| `Tests/CombinedTest/Server/PacketTest/TickEndWorldMutationTest.cs` | 実プロトコルの競合操作、過負荷優先、問い合わせ保留 |

## 配置と前例

| # | 項目 | 配置先・機構 | 前例との突合 | Verdict |
|---|---|---|---|---|
| 1 | electric dirty/map | `Game.EnergySystem` | 現在の`ElectricWireNetworkDatastore`と`ElectricTickUpdater`が同ドメインを所有 | ok |
| 2 | gear dirty/map | `Game.Gear` | 現在の`GearNetworkDatastore`と`GearTickUpdater`が同ドメインを所有 | ok |
| 3 | tick位相 | `Core.Update.GameUpdater`の汎用リスト | 既存`AdditionalUpdates`・`TickEndUpdates`と同じ機構。ドメイン処理は置かない | ok |
| 4 | 共通受信FIFO | `Server.Boot/Loop/PacketProcessing` | 既存`ReceiveQueueProcessor`・`SendQueueProcessor`の通信スレッド境界 | ok |
| 5 | dirty問い合わせgate | `Server.Protocol.PacketResponseCreator` | 同型が既にtag解析とresponse生成を所有し、Game.Gear/EnergySystem参照も既存asmdefにある | ok |
| 6 | 保存要求契約 | `Game.SaveLoad.Interface` | 既存`IWorldSaveDataSaver`と同じ公開契約層 | ok |
| 7 | 保存調整 | `Game.SaveLoad` | `AssembleSaveJsonText`・`WorldSaverForJson`と同じ永続化実装層 | ok |
| 8 | cleanroom全dirty確定 | `Game.CleanRoom` | 形状検出とcarry-overは既存`CleanRoomDetectionService`が所有 | ok |
| 9 | DI分割 | `Server.Boot/DependencyInjection` | 現在の`MoorestechServerDIContainerGenerator`の責務を同assembly内で分割 | ok |
| 10 | 内部観測 | `Tests/Util` + reflection | 既存`GearNetworkDatastoreReflectionTestUtil` | ok |

新しいイベント通知・asmdef参照・マスターデータ・セーブスキーマは追加しない。

---

### Task 1: Merge master and resolve the recipe-selection conflict

**Files:**
- Merge: all files changed by `master`
- Resolve: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs`
- Verify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaElectricMachineComponent.cs`
- Verify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaGearMachineComponent.cs`

**Interfaces:**
- Preserves: `IMachineRecipeSelectorComponent`, `SelectedRecipeGuid`, `SetSelectedRecipe`, `ClearSelectedRecipe`, selected-recipe save/load
- Produces: `public void SupplyExternalPower(float power)` used by electric and gear machine wrappers

- [ ] **Step 1: Confirm the virtual merge has one content conflict**

Run:

```powershell
git merge-tree --write-tree master HEAD
```

Expected: only `VanillaMachineProcessorComponent.cs` is reported as `CONFLICT (content)`.

- [ ] **Step 2: Merge master without committing the unresolved tree**

Run:

```powershell
git merge --no-commit master
```

Expected: merge stops at the processor conflict and all other files are auto-merged.

- [ ] **Step 3: Resolve the processor from master and apply the feature method name/body**

Keep master’s constructors, selected recipe state/detail/save fields, and these methods verbatim:

```csharp
public Guid SelectedRecipeGuid => _context.SelectedRecipe?.MachineRecipeGuid ?? Guid.Empty;
public MachineRecipeSelectionResult SetSelectedRecipe(MachineRecipeMasterElement recipe, IOpenableInventory refundOverflowInventory)
public MachineRecipeSelectionResult ClearSelectedRecipe(IOpenableInventory refundOverflowInventory)
private MachineRecipeSelectionResult ChangeSelection(MachineRecipeMasterElement recipe, IOpenableInventory refundOverflowInventory)
```

Replace only master’s `SupplyPower` declaration with:

```csharp
public void SupplyExternalPower(float power)
{
    BlockException.CheckDestroy(this);

    // 複数の電力セグメントから供給され得るため加算する
    // Accumulate power because multiple electric segments may supply this machine
    _context.SuppliedPower += power;
    if (CurrentState == ProcessState.Idle) _changeState.OnNext(Unit.Default);
}
```

- [ ] **Step 4: Verify conflict markers and both callers**

Run:

```powershell
rg -n "<<<<<<<|=======|>>>>>>>|SupplyPower\(" moorestech_server/Assets/Scripts --glob '*.cs'
rg -n "SupplyExternalPower\(" moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine --glob '*.cs'
```

Expected: no conflict marker or old `SupplyPower` call; both machine wrappers call `SupplyExternalPower`.

- [ ] **Step 5: Compile and run recipe-selection regressions**

Run:

```powershell
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelectionTest|MachineRecipeChangeRefundTest|MachineRecipeSelectionProtocolTest|MachineRecipeSelectionSaveLoadTest|BlueprintMachineRecipeSelectionTest|GearMachineIoTest|IdlePowerRateTest"
```

Expected: compile succeeds and all selected tests pass.

- [ ] **Step 6: Commit the merge**

```powershell
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs
git status --short
git commit -m "merge: masterを電力tick統合ブランチへ反映"
```

The merge already stages its non-conflicting paths. Confirm the status contains no user-owned untracked `.agents` or `.codex` paths in the index before committing.

---

### Task 2: Split bootstrap registration into focused files

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Boot/DependencyInjection/ServerGameplayServiceCollectionBuilder.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Boot/DependencyInjection/ServerEntryPointMaterializer.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Boot/DependencyInjection/MoorestechServerTickRegistration.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`
- Test: existing DI bootstrap tests selected by the command below

**Interfaces:**
- Produces: `ServerGameplayServiceCollectionBuilder.Build(MoorestechServerDIContainerOptions, ModsResource, MasterJsonFileContainer, ServiceProvider, ItemStackLevelDataStore) : ServiceCollection`
- Produces: `ServerEntryPointMaterializer.Materialize(ServiceProvider provider)`
- Produces: `MoorestechServerTickRegistration.Register(ServiceProvider provider)`
- Preserves: `Create(MoorestechServerDIContainerOptions options) : (PacketResponseCreator, ServiceProvider)`

- [ ] **Step 1: Record the current bootstrap behavior**

Run:

```powershell
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SaveJsonFileTest|ElectricTickUnificationTest|InitialHandshakeProtocolTest"
```

Expected: selected baseline tests pass before the mechanical split.

- [ ] **Step 2: Extract gameplay service registration**

Move the gameplay registrations into the following complete method, retaining this exact order:

```csharp
internal static class ServerGameplayServiceCollectionBuilder
{
    public static ServiceCollection Build(
        MoorestechServerDIContainerOptions options,
        ModsResource modResource,
        MasterJsonFileContainer masterJsonFileContainer,
        ServiceProvider initializerProvider,
        ItemStackLevelDataStore itemStackLevelDataStore)
    {
        var services = new ServiceCollection();
        services.AddSingleton<EventProtocolProvider, EventProtocolProvider>();
        services.AddSingleton<IWorldSettingsDatastore, WorldSettingsDatastore>();
        services.AddSingleton<IPlayerInventorySlotLevelDataStore, PlayerInventorySlotLevelDataStore>();
        services.AddSingleton<IPlayerInventoryDataStore, PlayerInventoryDataStore>();
        services.AddSingleton<IInventorySubscriptionStore, InventorySubscriptionStore>();
        services.AddSingleton<OpenableInventoryResolver>();
        services.AddSingleton<IElectricWireNetworkDatastore, ElectricWireNetworkDatastore>();
        services.AddSingleton<MaxElectricPoleMachineConnectionRange>();
        services.AddSingleton<IEntitiesDatastore, EntitiesDatastore>();
        services.AddSingleton<IEntityFactory, EntityFactory>();

        var railGraphDatastore = initializerProvider.GetService<RailGraphDatastore>();
        var trainUnitDatastore = initializerProvider.GetService<TrainUnitDatastore>();
        services.AddSingleton(initializerProvider.GetService<GearNetworkDatastore>());
        services.AddSingleton(initializerProvider.GetService<CleanRoomDatastore>());
        services.AddSingleton(railGraphDatastore);
        services.AddSingleton<IRailGraphDatastore>(railGraphDatastore);
        services.AddSingleton<IRailGraphProvider>(railGraphDatastore);
        services.AddSingleton(trainUnitDatastore);
        services.AddSingleton<ITrainUnitMutationDatastore>(trainUnitDatastore);
        services.AddSingleton<ITrainUnitLookupDatastore>(trainUnitDatastore);
        services.AddSingleton<RailConnectionCommandHandler>();
        services.AddSingleton(initializerProvider.GetService<TrainDiagramManager>());
        services.AddSingleton(initializerProvider.GetService<TrainRailPositionManager>());
        services.AddSingleton<IRailGraphNodeRemovalListener>(initializerProvider.GetService<TrainDiagramManager>());
        services.AddSingleton<IRailGraphNodeRemovalListener>(initializerProvider.GetService<TrainRailPositionManager>());

        services.AddSingleton<IGameUnlockStateDataController, GameUnlockStateDataController>();
        services.AddSingleton<CraftTreeManager>();
        services.AddSingleton<IGameActionExecutor, GameActionExecutor>();
        services.AddSingleton(itemStackLevelDataStore);
        services.AddSingleton<IItemStackLevelLookup>(itemStackLevelDataStore);
        services.AddSingleton<IItemStackLevelUnlocker>(itemStackLevelDataStore);
        services.AddSingleton<IResearchDataStore, ResearchDataStore>();
        services.AddSingleton<IBlueprintDatastore, BlueprintDatastore>();
        services.AddSingleton<ResearchEvent>();
        services.AddSingleton(initializerProvider.GetService<MapInfoJson>());
        services.AddSingleton(masterJsonFileContainer);
        services.AddSingleton<ChallengeDatastore>();
        services.AddSingleton<ChallengeEvent>();
        services.AddSingleton<TrainSaveLoadService>();
        services.AddSingleton<RailGraphSaveLoadService>();
        services.AddSingleton<TrainDockingStateRestorer>();
        services.AddSingleton<ITrainUpdateEvent, TrainUpdateEvent>();
        services.AddSingleton<ITrainUnitSnapshotNotifyEvent, TrainUnitSnapshotNotifyEvent>();
        services.AddSingleton<TrainCarRidingInputBuffer>();
        services.AddSingleton<TrainCarRidingManualCommandResolver>();
        services.AddSingleton<TrainUpdateService>();

        services.AddSingleton<ElectricTickUpdater>();
        services.AddSingleton<GearTickUpdater>();
        services.AddSingleton<IBlockRemovalReservationService, BlockRemovalReservationService>();
        services.AddSingleton<IPlayerConnectionChecker, PlayerConnectionRegistry>();
        services.AddSingleton<RidableResolver>();
        services.AddSingleton<IPlayerRidingDatastore, PlayerRidingDatastore>();
        services.AddSingleton<RemovedRidableRidingHandler>();

        services.AddSingleton(modResource);
        services.AddSingleton<IWorldSaveDataSaver, WorldSaverForJson>();
        services.AddSingleton<IWorldSaveDataLoader, WorldLoaderFromJson>();
        services.AddSingleton(options.saveJsonFilePath);
        services.AddSingleton<IMainInventoryUpdateEvent, MainInventoryUpdateEvent>();
        services.AddSingleton<IGrabInventoryUpdateEvent, GrabInventoryUpdateEvent>();
        services.AddSingleton<CraftEvent>();

        services.AddSingleton<ChangeBlockStateEventPacket>();
        services.AddSingleton<MainInventoryUpdateEventPacket>();
        services.AddSingleton<UnifiedInventoryEventPacket>();
        services.AddSingleton<GrabInventoryUpdateEventPacket>();
        services.AddSingleton<PlaceBlockEventPacket>();
        services.AddSingleton<RemoveBlockToSetEventPacket>();
        services.AddSingleton<CompletedChallengeEventPacket>();
        services.AddSingleton<ResearchCompleteEventPacket>();
        services.AddSingleton<ItemStackLevelUnlockEventPacket>();
        services.AddSingleton<MapObjectUpdateEventPacket>();
        services.AddSingleton<UnlockedEventPacket>();
        services.AddSingleton<RailNodeCreatedEventPacket>();
        services.AddSingleton<RailConnectionCreatedEventPacket>();
        services.AddSingleton<TrainUnitTickDiffBundleEventPacket>();
        services.AddSingleton<TrainUnitSnapshotEventPacket>();
        services.AddSingleton<RailNodeRemovedEventPacket>();
        services.AddSingleton<RailConnectionRemovedEventPacket>();
        services.AddSingleton<RidingStateEventPacket>();
        services.AddSingleton<AssembleSaveJsonText>();
        return services;
    }
}
```

- [ ] **Step 3: Extract eager entry-point materialization**

Create:

```csharp
internal static class ServerEntryPointMaterializer
{
    public static void Materialize(ServiceProvider provider)
    {
        provider.GetService<MainInventoryUpdateEventPacket>();
        provider.GetService<UnifiedInventoryEventPacket>();
        provider.GetService<GrabInventoryUpdateEventPacket>();
        provider.GetService<PlaceBlockEventPacket>();
        provider.GetService<RemoveBlockToSetEventPacket>();
        provider.GetService<CompletedChallengeEventPacket>();
        provider.GetService<GearNetworkDatastore>();
        provider.GetService<CleanRoomDatastore>();
        provider.GetService<RailGraphDatastore>();
        provider.GetService<TrainDiagramManager>();
        provider.GetService<TrainRailPositionManager>();
        provider.GetService<ChangeBlockStateEventPacket>();
        provider.GetService<MapObjectUpdateEventPacket>();
        provider.GetService<UnlockedEventPacket>();
        provider.GetService<ResearchCompleteEventPacket>();
        provider.GetService<ItemStackLevelUnlockEventPacket>();
        provider.GetService<RailNodeCreatedEventPacket>();
        provider.GetService<RailConnectionCreatedEventPacket>();
        provider.GetService<TrainUnitTickDiffBundleEventPacket>();
        provider.GetService<TrainUnitSnapshotEventPacket>();
        provider.GetService<RailNodeRemovedEventPacket>();
        provider.GetService<RailConnectionRemovedEventPacket>();
        provider.GetService<RidingStateEventPacket>();
        provider.GetService<RemovedRidableRidingHandler>();
    }
}
```

Keep `serverContext.SetMainServiceProvider(provider)` in the generator after materialization.

- [ ] **Step 4: Extract tick registration**

Create the initial production registration:

```csharp
internal static class MoorestechServerTickRegistration
{
    public static void Register(ServiceProvider provider)
    {
        var electricWireNetworkDatastore =
            provider.GetRequiredService<IElectricWireNetworkDatastore>();
        var gearNetworkDatastore =
            provider.GetRequiredService<GearNetworkDatastore>();
        var blockRemovalReservationService =
            provider.GetRequiredService<IBlockRemovalReservationService>();

        GameUpdater.AdditionalUpdates.Add(electricWireNetworkDatastore.FlushPendingCommands);
        GameUpdater.AdditionalUpdates.Add(gearNetworkDatastore.FlushPendingMutations);
        GameUpdater.AdditionalUpdates.Add(provider.GetRequiredService<ElectricTickUpdater>().Update);
        GameUpdater.AdditionalUpdates.Add(provider.GetRequiredService<GearTickUpdater>().Update);
        GameUpdater.TickEndUpdates.Add(() =>
        {
            blockRemovalReservationService.ApplyReservedRemovals();
            electricWireNetworkDatastore.FlushPendingCommands();
            gearNetworkDatastore.FlushPendingMutations();
        });
    }
}
```

This task is mechanical and preserves the current duplicate tick-head/tick-end flushes. Task 3 replaces the four tick-head delegates with rebuild/rebuild/calculate/calculate, and Task 5 replaces the tick-end closure with the shared packet coordinator before Task 6 adds the final save phase.

- [ ] **Step 5: Reduce the generator to orchestration**

The remaining main flow is:

```csharp
var services = ServerGameplayServiceCollectionBuilder.Build(
    options, modResource, masterJsonFileContainer, initializerProvider, itemStackLevelDataStore);
var serviceProvider = services.BuildServiceProvider();
var packetResponse = new PacketResponseCreator(serviceProvider);
MoorestechServerTickRegistration.Register(serviceProvider);
ServerEntryPointMaterializer.Materialize(serviceProvider);
serverContext.SetMainServiceProvider(serviceProvider);
MessagePackInitializer.Initialize();
return (packetResponse, serviceProvider);
```

- [ ] **Step 6: Check file sizes, compile, and rerun the baseline**

Run:

```powershell
Get-ChildItem moorestech_server/Assets/Scripts/Server.Boot -Recurse -Filter *.cs | ForEach-Object { if ((Get-Content $_.FullName).Count -gt 200) { $_.FullName } }
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SaveJsonFileTest|ElectricTickUnificationTest|InitialHandshakeProtocolTest"
```

Expected: no changed bootstrap file exceeds 200 lines; compile and tests pass.

- [ ] **Step 7: Commit**

```powershell
git add moorestech_server/Assets/Scripts/Server.Boot
git commit -m "refactor: サーバー起動登録を責務別に分割"
```

---

### Task 3: Replace electric and gear mutation replay with dirty full rebuilds

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.EnergySystem/ElectricWire/IElectricWireNetworkDatastore.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.EnergySystem/ElectricWire/ElectricWireNetworkDatastore.cs`
- Replace: `moorestech_server/Assets/Scripts/Game.EnergySystem/ElectricWire/ElectricWireTopologyMap.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.EnergySystem/ElectricWire/ElectricWireSegmentSplitService.cs`
- Delete: `moorestech_server/Assets/Scripts/Game.EnergySystem/ElectricWire/ElectricWireTopologyCommand.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.EnergySystem/ElectricTickUpdater.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.EnergySystem/EnergySegment.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Gear/Common/GearNetworkDatastore.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Gear/Common/GearTickUpdater.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Gear/Common/GearNetwork.cs`
- Replace: `moorestech_server/Assets/Scripts/Game.Gear/Topology/GearNetworkTopologyMap.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Gear/Topology/GearNetworkTopologyBuildResult.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Gear/Topology/GearConnectedComponentFinder.cs`
- Delete: `moorestech_server/Assets/Scripts/Game.Gear/Topology/GearTopologyMutation.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Gear/Tick/GearRuntimeStateStore.cs`
- Modify electric callers under `Game.Block/Blocks/ElectricWire` and `Server.Protocol/PacketResponse/Util/ElectricWire`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/DependencyInjection/MoorestechServerTickRegistration.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/Util/ElectricNetworkReflectionTestUtil.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/ElectricTickUnificationTest.cs`
- Modify: electric tests listed in Step 7

**Interfaces:**
- Produces `IElectricWireNetworkDatastore`:

```csharp
bool IsTopologyDirty { get; }
void AddConnector(IElectricWireConnector connector);
void RemoveConnector(IElectricWireConnector connector);
void MarkTopologyDirty();
void RebuildIfDirty();
bool TryGetEnergySegment(BlockInstanceId blockInstanceId, out EnergySegment segment);
IReadOnlyList<EnergySegment> GetSegments();
```

- Produces `GearNetworkDatastore.IsTopologyDirty` and `GearNetworkDatastore.RebuildIfDirty()`.
- Removes: `SegmentCount`, `FlushPendingCommands`, `RebuildAround`, `FlushPendingMutations`, mutation structs, public electric role dictionaries.
- Produces this exact `AdditionalUpdates` order: electric rebuild, gear rebuild, electric settlement, gear calculation.

- [ ] **Step 1: Rewrite failing electric dirty-boundary tests first**

In `ElectricWireNetworkDatastoreFlushTest`, assert these outcomes:

```csharp
datastore.AddConnector(connector);
Assert.IsFalse(datastore.TryGetEnergySegment(connector.BlockInstanceId, out _));
datastore.RebuildIfDirty();
Assert.IsTrue(datastore.TryGetEnergySegment(connector.BlockInstanceId, out _));

var appliedMap = ElectricNetworkReflectionTestUtil.GetTopologyMap(datastore);
datastore.RebuildIfDirty();
Assert.AreSame(appliedMap, ElectricNetworkReflectionTestUtil.GetTopologyMap(datastore));
```

Also enqueue several add/remove/edge changes before one rebuild and assert only the final graph exists. Count adjacency enumeration for electric and gear and prove one rebuild visits each registered component once. Add a registration-order regression that inspects `GameUpdater.AdditionalUpdates` from the test side and proves both `RebuildIfDirty` delegates precede both calculation delegates; do not add a production order counter or diagnostic property.

- [ ] **Step 2: Run the new test and observe the API failure**

Run:

```powershell
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWireNetworkDatastoreFlushTest"
```

Expected: compile/test fails because `RebuildIfDirty` and the reflection helper do not exist yet.

- [ ] **Step 3: Implement electric registration and map swap**

Use this datastore core:

```csharp
private readonly Dictionary<BlockInstanceId, IElectricWireConnector> _registeredConnectors = new();
private ElectricWireTopologyMap _topologyMap = ElectricWireTopologyMap.CreateEmpty();
private bool _isTopologyDirty = true;

public bool IsTopologyDirty => _isTopologyDirty;

public void AddConnector(IElectricWireConnector connector)
{
    _registeredConnectors[connector.BlockInstanceId] = connector;
    _isTopologyDirty = true;
}

public void RemoveConnector(IElectricWireConnector connector)
{
    _registeredConnectors.Remove(connector.BlockInstanceId);
    _isTopologyDirty = true;
}

public void MarkTopologyDirty() => _isTopologyDirty = true;

public void RebuildIfDirty()
{
    if (!_isTopologyDirty) return;
    var rebuilt = ElectricWireTopologyMap.Build(_registeredConnectors.Values);
    var previous = _topologyMap;
    _topologyMap = rebuilt;
    previous.Destroy();
    _isTopologyDirty = false;
}
```

`Build` must create the ID lookup in one `V` pass, call connected-component BFS once, enumerate every connector adjacency at most once, create one `EnergySegment` per component, register roles, and fill the ID map. It must not sort, replay Add/Remove operations, or rescan all vertices for each component. Instrument fake adjacency enumerators in the test and assert enumeration counts are bounded by `V + E`, independent of the number of queued mutations.

- [ ] **Step 4: Update all electric topology mutation callers**

Replace every call to `RebuildAround` found in the listed electric connector and protocol utility files with:

```csharp
ServerContext.GetService<IElectricWireNetworkDatastore>().MarkTopologyDirty();
```

Constructor/destruction continue to use `AddConnector`/`RemoveConnector`. `ElectricTickUpdater.Update()` performs settlement only; it must not own topology rebuilding. Remove every direct electric rebuild registration from tick end and the duplicate pre-updater registration.

- [ ] **Step 5: Implement gear registration and full connected-component build**

Use the same invariant:

```csharp
private readonly Dictionary<BlockInstanceId, IGearEnergyTransformer> _registeredGears = new();
private bool _isTopologyDirty = true;
public bool IsTopologyDirty => _isTopologyDirty;

public static void AddGear(IGearEnergyTransformer gear)
{
    _instance._registeredGears[gear.BlockInstanceId] = gear;
    _instance._isTopologyDirty = true;
}

public static void RemoveGear(IGearEnergyTransformer gear)
{
    _instance._registeredGears.Remove(gear.BlockInstanceId);
    _instance._isTopologyDirty = true;
}
```

`RebuildIfDirty()` must not clear or mutate any applied object while building. Construct a `GearNetworkTopologyBuildResult` containing the new topology map, runtime state, recalculation set, and continuous-tick set entirely in local/new objects. Fresh networks do not inherit the old rotation-search cache; their first normal `RunTick` calculates it. Build with one `GearConnectedComponentFinder` pass and bounded adjacency enumeration, then swap every applied reference together, destroy the detached old state, and finally clear dirty. `GearTickUpdater.Update()` performs gear calculation only; it must not own topology rebuilding.

Update `MoorestechServerTickRegistration` to register these four delegates in this exact order:

```csharp
GameUpdater.AdditionalUpdates.Add(
    provider.GetRequiredService<IElectricWireNetworkDatastore>().RebuildIfDirty);
GameUpdater.AdditionalUpdates.Add(
    provider.GetRequiredService<GearNetworkDatastore>().RebuildIfDirty);
GameUpdater.AdditionalUpdates.Add(
    provider.GetRequiredService<ElectricTickUpdater>().Update);
GameUpdater.AdditionalUpdates.Add(
    provider.GetRequiredService<GearTickUpdater>().Update);
```

This keeps both topology maps based on the same confirmed block boundary before either network performs supply/demand calculation.

- [ ] **Step 6: Remove test-only production observation APIs**

Make electric segment constructor/mutators/role collections non-public where only the energy assembly needs them. Remove `SegmentCount`. Make gear runtime lists/store/collection methods internal when all runtime callers share `Game.Gear`.

Create test reflection helpers with this shape:

```csharp
public static object GetTopologyMap(IElectricWireNetworkDatastore datastore);
public static int GetSegmentCount(IElectricWireNetworkDatastore datastore);
public static IReadOnlyDictionary<BlockInstanceId, IElectricConsumer> GetConsumers(EnergySegment segment);
public static IReadOnlyDictionary<BlockInstanceId, IElectricGenerator> GetGenerators(EnergySegment segment);
public static EnergySegment CreateSegment();
public static void AddGenerator(EnergySegment segment, IElectricGenerator generator);
public static ElectricNetworkStatistics SettleTick(EnergySegment segment);
```

Every helper must use `BindingFlags.NonPublic | BindingFlags.Instance`; do not add a production getter or diagnostic counter.

- [ ] **Step 7: Convert affected tests to reflection and final-state assertions**

Update these exact files:

```text
Tests/UnitTest/Core/Block/ElectricSegmentTest.cs
Tests/UnitTest/Game/ElectricWireNetworkDatastoreTest.cs
Tests/UnitTest/Game/ElectricWireNetworkDatastoreFlushTest.cs
Tests/CombinedTest/Core/ElectricPumpTest.cs
Tests/CombinedTest/Core/MinerMiningTest.cs
Tests/CombinedTest/Core/PumpFluidVeinTest.cs
Tests/CombinedTest/Game/ConnectElectricSegmentTest.cs
Tests/CombinedTest/Game/DisconnectElectricSegmentTest.cs
Tests/CombinedTest/Game/DisconnectMachineFromElectricSegmentTest.cs
Tests/CombinedTest/Game/ElectricWireSaveLoadTest.cs
Tests/CombinedTest/Game/Energy/MachineMultiSegmentPowerSupplyTest.cs
Tests/CombinedTest/Game/Energy/RemoveGeneratorFromMultiSegmentTest.cs
Tests/CombinedTest/Server/PacketTest/ElectricWireAutoConnectPlaceTest.cs
Tests/CombinedTest/Server/PacketTest/ElectricWireConnectionEditProtocolTest.cs
```

Delete the old union-by-size segment identity assertion: full rebuild intentionally replaces the whole derived map. Assert only final membership and connectivity.

- [ ] **Step 8: Run topology tests**

Run:

```powershell
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWireNetworkDatastoreTest|ElectricWireNetworkDatastoreFlushTest|ElectricSegmentTest|ConnectElectricSegmentTest|DisconnectElectricSegmentTest|DisconnectMachineFromElectricSegmentTest|ElectricWireSaveLoadTest|GearNetworkTest|ConnectorShapeConnectionTest|ElectricTickUnificationTest"
```

Expected: compile succeeds; dirty changes are invisible until the next tick head; stable ticks retain the same map.

- [ ] **Step 9: Commit**

```powershell
git add moorestech_server/Assets/Scripts/Game.EnergySystem moorestech_server/Assets/Scripts/Game.Gear moorestech_server/Assets/Scripts/Game.Block moorestech_server/Assets/Scripts/Server.Protocol moorestech_server/Assets/Scripts/Server.Boot moorestech_server/Assets/Scripts/Tests
git commit -m "refactor: 電力と歯車網をdirty全体再構築へ統合"
```

---

### Task 4: Fix remaining-capacity demand and tick-end overload expectations

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/ElectricToGear/ElectricToGearGeneratorComponent.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/ElectricToGearGeneratorTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/GearOverloadBreakageTest.cs`
- Verify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/GearBeltConveyorOverloadBreakageTest.cs`

**Interfaces:**
- Preserves: output modes and `SetSelectedMode(int)`
- Changes: `RequestEnergy` reports `max(0, BatteryCapacity - batteryRemaining)`
- Preserves: overload detection reserves removal; actual world deletion occurs in tick end

- [ ] **Step 1: Change tests to the approved request behavior**

Add assertions:

```csharp
c.OnElectricTickPostProcess(HalfRateStats(mode0.RequiredPower));
Assert.AreEqual((float)mode0.RequiredPower * 0.5f, c.RequestEnergy.AsPrimitive(), 0.001f);
c.OnElectricTickPostProcess(FullRateStats(c.RequestEnergy.AsPrimitive()));
Assert.AreEqual(0f, c.RequestEnergy.AsPrimitive(), 0.001f);
```

After switching from a half-charged mode0 battery to mode1, expect `mode1.RequiredPower - mode0.RequiredPower * 0.5`, not the full mode1 capacity.

- [ ] **Step 2: Verify the old test fails**

Run:

```powershell
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricToGearGeneratorTest"
```

Expected: remaining-capacity assertion fails against the current constant full-capacity request.

- [ ] **Step 3: Implement the request**

```csharp
public ElectricPower RequestEnergy =>
    new(Mathf.Max(0f, BatteryCapacity - _batteryRemaining));
```

Keep `_lastChargedPower` bounded by the same remaining capacity and retain every output mode.

- [ ] **Step 4: Correct the immediate-overload test**

After direct `TickOverloadCheck()` use:

```csharp
Assert.IsTrue(world.Exists(fullPosition));
GameUpdater.UpdateOneTick();
Assert.IsFalse(world.Exists(fullPosition));
```

The first assertion proves detection only reserved the removal; the second proves tick-end application.

- [ ] **Step 5: Compile, run, and commit**

```powershell
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricToGearGeneratorTest|ElectricToGearOutputModeProtocolTest|GearOverloadBreakageTest|GearBeltConveyorOverloadBreakageTest"
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/ElectricToGear moorestech_server/Assets/Scripts/Tests/CombinedTest/Core
git commit -m "fix: 変換機の残容量要求と過負荷境界を修正"
```

---

### Task 5: Process all client packets through one fixed tick-end FIFO

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Boot/Loop/PacketProcessing/ITickEndPacketEntry.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Boot/Loop/PacketProcessing/TickEndPacketQueue.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Boot/Loop/PacketProcessing/WorldMutationTickEndUpdater.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/TickEndPacketProcessResult.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Loop/PacketProcessing/ReceiveQueueProcessor.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Loop/ServerListenAcceptor.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/ServerInstanceManager.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Update/GameUpdater.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/DependencyInjection/ServerGameplayServiceCollectionBuilder.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/DependencyInjection/MoorestechServerTickRegistration.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/UnitTest/Server/TickEndPacketQueueTest.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/TickEndWorldMutationTest.cs`

**Interfaces:**

```csharp
public enum TickEndPacketProcessResult
{
    Completed,
    Deferred
}
```

Place this enum in the `Server.Protocol` namespace and file. `Server.Boot` already references `Server.Protocol`; placing the type under Boot would force the protocol assembly to reference Boot and create an assembly cycle.

```csharp
public interface ITickEndPacketEntry
{
    bool IsActive { get; }
    TickEndPacketProcessResult Process();
}
```

- `TickEndPacketQueue.Enqueue(ITickEndPacketEntry entry) : void`
- `TickEndPacketQueue.FreezeCurrentPackets() : void`
- `TickEndPacketQueue.ProcessFrozenPackets() : void`
- `WorldMutationTickEndUpdater.Update() : void`
- `PacketResponseCreator.GetTickEndPacketResponse(byte[] payload, PacketResponseContext context, out List<byte[]> responses) : TickEndPacketProcessResult`

- [ ] **Step 1: Write failing pure queue tests**

Cover:

```csharp
queue.Enqueue(new FakeEntry("A1", log));
queue.Enqueue(new FakeEntry("B1", log));
queue.Enqueue(new FakeEntry("A2", log));
queue.FreezeCurrentPackets();
queue.ProcessFrozenPackets();
CollectionAssert.AreEqual(new[] { "A1", "B1", "A2" }, log);
```

Also verify an entry enqueued after freeze waits for the next freeze and inactive entries are skipped. A `Deferred` entry plus its unprocessed frozen tail must be restored ahead of packets that arrived later.

- [ ] **Step 2: Run tests and observe missing types**

```powershell
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TickEndPacketQueueTest"
```

Expected: compile fails because the queue contract and implementation do not exist.

- [ ] **Step 3: Implement lock-linearized order and O(1) freeze**

The queue must assign and enqueue under the same lock:

```csharp
lock (_gate)
{
    _nextSequence++;
    _receiving.Enqueue(new SequencedPacket(_nextSequence, entry));
}
```

`FreezeCurrentPackets` swaps `_receiving` with an empty queue under that lock. `ProcessFrozenPackets` updates a private last-consumed sequence invariant, skips inactive entries, and handles the two results explicitly. `Completed` and inactive skip advance the consumed sequence. `Deferred` does not advance it, prepends the same-sequence current entry and frozen tail before the receiving queue under the same lock, and stops this batch. Do not use separate `Interlocked.Increment` and `ConcurrentQueue.Enqueue` operations.

- [ ] **Step 4: Convert the per-connection processor into an adapter**

Remove its `LateUpdateObservable` subscription and local `ConcurrentQueue`. `EnqueuePacket` creates a private runtime `ITickEndPacketEntry` carrying payload/context/sender and puts it in the shared queue. The connection-active flag crosses the socket and game threads, so read and write it with `Volatile.Read`/`Volatile.Write`. `Dispose` only marks the connection inactive and does not dispose the shared queue.

Its processing method must call:

```csharp
var processResult = _packetResponseCreator.GetTickEndPacketResponse(
    packet, _packetResponseContext, out var results);
if (processResult != TickEndPacketProcessResult.Completed) return processResult;
foreach (var result in results) EnqueueResponse(result);
return TickEndPacketProcessResult.Completed;
```

- [ ] **Step 5: Gate stale electric/gear network queries inside the existing deserialize path**

Refactor the existing `GetPacketResponse` and new `GetTickEndPacketResponse` through one core with one existing catch boundary. That boundary must include base deserialization, tag lookup, protocol execution, `SequenceId` assignment, response type conversion, and MessagePack serialization. Before calling the protocol, return `Deferred` only when:

```csharp
request.Tag == GetElectricNetworkInfoProtocol.ProtocolTag &&
_electricWireNetworkDatastore.IsTopologyDirty
```

or:

```csharp
request.Tag == GetGearNetworkInfoProtocol.ProtocolTag &&
_gearNetworkDatastore.IsTopologyDirty
```

Return `Completed` for a valid protocol response, including an expected protocol-level rejection represented by an empty response. Preserve the existing exception behavior: the shared handler logs the exception, returns an empty response list, and treats the packet as consumed. `Deferred` is reserved only for a valid network-info request whose network is dirty. Preserve the existing direct `GetPacketResponse` API by returning the core response list while its callers ignore the richer result.

- [ ] **Step 6: Establish tick-end ordering and a final phase**

Add `GameUpdater.FinalTickEndUpdates` and execute it after all `TickEndUpdates` but before `LateUpdate`. Include it in reset and editor frame stepping.

Implement:

```csharp
public void Update()
{
    _packetQueue.FreezeCurrentPackets();
    _blockRemovalReservationService.ApplyReservedRemovals();
    _packetQueue.ProcessFrozenPackets();
}
```

Register only this updater in `TickEndUpdates`; do not separately register reserved removals.

- [ ] **Step 7: Share one queue across every accepted connection**

Register `TickEndPacketQueue` and `WorldMutationTickEndUpdater` as singletons. Resolve the queue in `ServerInstanceManager`, pass it through `ServerListenAcceptor.StartServer`, and construct each `ReceiveQueueProcessor` with that same instance. Keep the two-element result tuple of DI `Create()` unchanged.

- [ ] **Step 8: Add real ordering regressions**

In test-side `ITickEndPacketEntry` adapters, call the real `GetTickEndPacketResponse` without sockets. Verify:

- Two same-coordinate place requests in enqueue order yield one placement and one inventory charge.
- Two placements at different coordinates competing for one remaining inventory item yield one placement and one charge.
- Two multi-cell placements with different origins but overlapping footprints yield one placement and one charge.
- `remove→place` and `place→remove` match enqueue order.
- Two manual removes of the same block return its material exactly once.
- An overload reservation is applied before a queued manual remove, so broken removal gives no manual refund.
- A network-info request following a topology mutation remains queued until a later tick whose topology is clean.
- A disconnected entry never performs its world mutation.
- [ ] **Step 9: Compile, run, and commit**

```powershell
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TickEndPacketQueueTest|TickEndWorldMutationTest|PlaceBlockProtocolTest|RemoveBlockProtocolTest|RemoveBlockRefundTest|ElectricWireConnectionEditProtocolTest|GetElectricNetworkInfoProtocolTest|GetGearNetworkInfoProtocolTest"
git add moorestech_server/Assets/Scripts/Core.Update moorestech_server/Assets/Scripts/Server.Boot moorestech_server/Assets/Scripts/Server.Protocol moorestech_server/Assets/Scripts/Tests
git commit -m "feat: クライアント操作を共通tick末尾FIFOへ統合"
```

---

### Task 6: Coordinate save requests at the final tick boundary

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad.Interface/IWorldSaveRequest.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/WorldSaveCoordinator.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/SaveProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Loop/AutoSaveSystem.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/ServerInstanceManager.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/DependencyInjection/ServerGameplayServiceCollectionBuilder.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/DependencyInjection/MoorestechServerTickRegistration.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDetectionService.cs`
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDirtyBatchProcessor.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/AssembleSaveJsonText.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/WorldSaveCoordinatorTest.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/CleanRoomPendingSaveTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/TickEndWorldMutationTest.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/TickEndSaveConsistencyTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveJsonFileTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/ElectricToGearGeneratorTest.cs`

**Interfaces:**

```csharp
public interface IWorldSaveRequest
{
    void RequestSave();
}

public sealed class WorldSaveCoordinator : IWorldSaveRequest
{
    public void RequestSave();
    public void SaveIfRequested();
}
```

```csharp
public void CleanRoomDetectionService.ProcessAllDirtySeeds();
public void CleanRoomDatastore.ApplyPendingStructureChangesForSave();
```

- [ ] **Step 1: Write coordinator generation tests**

With a test fake implementing the existing `IWorldSaveDataSaver`, cover:

```csharp
coordinator.RequestSave();
coordinator.RequestSave();
coordinator.SaveIfRequested();
coordinator.SaveIfRequested();
Assert.AreEqual(1, saver.SaveCount);
```

Have the fake request another save inside its first `Save()` and assert the second `SaveIfRequested()` saves again. Have a fake throw on its first `Save()` and assert a later call retries without adding a new request.

- [ ] **Step 2: Run and observe the missing coordinator**

```powershell
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WorldSaveCoordinatorTest"
```

Expected: compile fails because coordinator and request interface do not exist.

- [ ] **Step 3: Implement request generations**

Use `Interlocked`/`Volatile` generation state:

```csharp
public void RequestSave()
{
    Interlocked.Increment(ref _requestedGeneration);
}

public void SaveIfRequested()
{
    var targetGeneration = Volatile.Read(ref _requestedGeneration);
    if (targetGeneration == Volatile.Read(ref _completedGeneration)) return;
    _worldSaveDataSaver.Save();
    Volatile.Write(ref _completedGeneration, targetGeneration);
}
```

The completed generation changes only after success, so an exception retains the request and a request arriving during `Save()` remains newer.

- [ ] **Step 4: Convert manual and automatic saving to requests**

`SaveProtocol` resolves `IWorldSaveRequest` and only calls `RequestSave()`. `AutoSaveSystem.AutoSave` accepts the same interface and calls it after each delay. `ServerInstanceManager` passes the request service into the background task; the background task never reads a datastore or file.

- [ ] **Step 5: Register save after all tick-end handlers**

Register one `WorldSaveCoordinator`, alias it to `IWorldSaveRequest`, and add it to the final tick-end phase:

```csharp
var saveCoordinator = provider.GetRequiredService<WorldSaveCoordinator>();
GameUpdater.FinalTickEndUpdates.Add(saveCoordinator.SaveIfRequested);
```

Thus a save request produced by the fixed packet batch is handled in the same tick after all ordinary tick-end handlers, including Mod handlers added during `OnLoad`. Expected protocol rejection is a completed packet with an empty response and does not create a special save-retry state. A deferred read-only topology query remains queued but does not prevent saving already-confirmed world changes.

Create `TickEndSaveConsistencyTest` using the real shared queue, real place/remove/save protocols, and a temporary save path. Cover three independent fresh-DI cases:

1. Enqueue place then save; after one tick, load the written JSON into a fresh world and assert the block exists and the player inventory contains exactly the charged remainder.
2. Enqueue manual remove then save; after one tick, load the JSON and assert the block is absent and exactly one removal refund exists.
3. Reserve an overload break, enqueue manual remove then save, and run one tick; load the JSON and assert the block is absent and no manual-removal refund was duplicated.

These tests verify block and inventory data come from the same final tick boundary, not merely that a saver method was called.

- [ ] **Step 6: Share normal and save-only cleanroom structural processing**

The existing `CleanRoomDetectionService` is already 198 lines. Extract its batch flood-fill, affected-old-room selection, identity matching, and carry-over commit into the production-used `CleanRoomDirtyBatchProcessor`. Pass the live room list, pending batch queue, world, budget, and next room ID explicitly; do not duplicate the algorithm or add diagnostic accessors. Keep both changed files below 200 lines.

`CleanRoomDetectionService.ProcessDirtySeeds()` delegates with a `drainAll` flag. The normal method retains its cell budget. The save method calls the same processor with full drain; build boundary/occupied cell sets once and drain every batch in one invocation.

```csharp
public void ProcessDirtySeeds() => ProcessDirtySeeds(false);
public void ProcessAllDirtySeeds() => ProcessDirtySeeds(true);
```

The while condition is:

```csharp
while (_pendingBatches.Count > 0 &&
       (drainAll || processedBatchCount == 0 || visitedTotal < _dirtyCellBudgetPerTick))
```

Do not call `UpdatePurity`, machine effects, or `RebuildAll` from this path.

- [ ] **Step 7: Drain cleanroom changes before any save snapshot**

At the first line of `AssembleSaveJsonText.AssembleSaveJson()` call:

```csharp
_cleanRoomDatastore.ApplyPendingStructureChangesForSave();
```

Only then obtain block, inventory, and cleanroom JSON. This protects every real assembler call, not only the coordinator call site.

- [ ] **Step 8: Prove split-room save consistency without a purity tick**

The combined test must:

1. Build a sealed `3x1x1` interior and run normal ticks until it is one room.
2. Set its impurity to exactly `90.0`.
3. Place the center wall and immediately call `AssembleSaveJson()` without `GameUpdater.UpdateOneTick()`.
4. Load JSON into a fresh DI.
5. Assert two rooms exist and each holds `30.0 ± 0.001` impurity.

Exact `30.0` proves the carry-over path ran and no extra purity integration tick occurred.

- [ ] **Step 9: Update file-save tests to request then tick**

Replace direct test calls to `IWorldSaveDataSaver.Save()` in `SaveJsonFileTest` and `ElectricToGearGeneratorTest` with:

```csharp
serviceProvider.GetRequiredService<IWorldSaveRequest>().RequestSave();
GameUpdater.UpdateOneTick();
```

- [ ] **Step 10: Compile, run, and commit**

```powershell
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WorldSaveCoordinatorTest|TickEndWorldMutationTest|TickEndSaveConsistencyTest|CleanRoomIncrementalDetectionTest|CleanRoomPendingSaveTest|CleanRoomSaveLoadTest|SaveJsonFileTest|ElectricToGearGeneratorTest|SaveProtocol"
git add moorestech_server/Assets/Scripts/Game.SaveLoad.Interface moorestech_server/Assets/Scripts/Game.SaveLoad moorestech_server/Assets/Scripts/Game.CleanRoom moorestech_server/Assets/Scripts/Server.Boot moorestech_server/Assets/Scripts/Server.Protocol moorestech_server/Assets/Scripts/Tests
git commit -m "feat: tick末尾の整合した世界保存を実装"
```

---

### Task 7: Verify load reconstruction and remove remaining test-only electric/gear API

**Files:**
- Audit and restrict: `moorestech_server/Assets/Scripts/Game.EnergySystem/EnergySegment.cs`
- Audit and restrict: `moorestech_server/Assets/Scripts/Game.EnergySystem/ElectricWire/IElectricWireNetworkDatastore.cs`
- Audit and restrict: `moorestech_server/Assets/Scripts/Game.EnergySystem/ElectricWire/ElectricWireNetworkDatastore.cs`
- Audit and restrict: `moorestech_server/Assets/Scripts/Game.Gear/Common/GearNetwork.cs`
- Audit and restrict: `moorestech_server/Assets/Scripts/Game.Gear/Common/GearNetworkDatastore.cs`
- Audit and restrict: `moorestech_server/Assets/Scripts/Game.Gear/Tick/GearRuntimeStateStore.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/Util/ElectricNetworkReflectionTestUtil.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/Util/GearNetworkDatastoreReflectionTestUtil.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/ElectricWireSaveLoadTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/GearChainPoleSaveLoadTest.cs`

**Interfaces:**
- Load behavior: constructors/`OnPostBlockLoad` mark both registries dirty; no topology JSON and no Mod callback
- Test behavior: reflection helpers observe private fields; no production observation members

- [ ] **Step 1: Add first-tick reconstruction assertions**

For the explicit wire save:

```csharp
loader.Load(json);
Assert.IsTrue(electricNetworkDatastore.IsTopologyDirty);
Assert.IsFalse(electricNetworkDatastore.TryGetEnergySegment(firstConnectorId, out _));
GameUpdater.UpdateOneTick();
Assert.IsFalse(electricNetworkDatastore.IsTopologyDirty);
Assert.IsTrue(electricNetworkDatastore.TryGetEnergySegment(firstConnectorId, out var firstSegment));
Assert.IsTrue(electricNetworkDatastore.TryGetEnergySegment(secondConnectorId, out var secondSegment));
Assert.AreSame(firstSegment, secondSegment);
```

For the explicit chain save loaded into its own fresh DI world:

```csharp
loader.Load(json);
Assert.IsTrue(gearNetworkDatastore.IsTopologyDirty);
Assert.IsFalse(GearNetworkDatastore.TryGetGearNetwork(firstGearId, out _));
GameUpdater.UpdateOneTick();
Assert.IsFalse(gearNetworkDatastore.IsTopologyDirty);
Assert.IsTrue(GearNetworkDatastore.TryGetGearNetwork(firstGearId, out var firstGearNetwork));
Assert.IsTrue(GearNetworkDatastore.TryGetGearNetwork(secondGearId, out var secondGearNetwork));
Assert.AreSame(firstGearNetwork, secondGearNetwork);
```

Use the runtime `IsTopologyDirty` property only for the protocol gate; observe all other internals through reflection.

- [ ] **Step 2: Run save/load topology tests**

```powershell
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWireSaveLoadTest|GearMachineSaveLoadTest|GearChainPoleSaveLoadTest|ElectricToGearGeneratorTest"
```

Expected: loaded explicit edges rebuild on the first normal tick with no serialized derived topology.

- [ ] **Step 3: Audit branch production symbols against non-test call sites**

Run:

```powershell
git diff --unified=0 master...HEAD -- 'moorestech_server/Assets/Scripts/**/*.cs' | rg '^\+.*\b(public|internal|protected|private)\b'
rg -n "SegmentCount|Consumers =>|Generators =>|EnergyTransformers =>|RebuildCount|FlushPendingCommands|FlushPendingMutations|TopologyCommand|TopologyMutation" moorestech_server/Assets/Scripts --glob '*.cs'
```

Audit every electric/gear symbol added anywhere on this branch, including private fields and methods, plus every pre-existing public/internal observation member touched by these tests. For each, record a non-`Tests` runtime read/call site or a concrete framework/serializer entry point. A symbol with only test readers must be deleted, not merely made private; tests must inspect the actual runtime-owned field or invoke the actual runtime method through `Tests/Util` reflection. Restrict visibility only when a runtime caller still exists. Do not remove protocol/state/save contracts used by clients or serializers.

- [ ] **Step 4: Verify no topology save fields or Mod rebuild hooks were introduced**

Run:

```powershell
rg -n "ElectricSegment|GearNetwork|Topology" moorestech_server/Assets/Scripts/Game.SaveLoad --glob '*.cs'
rg -n "OnLoad.*Rebuild|Rebuild.*OnLoad|AfterTopology" moorestech_server/Assets/Scripts --glob '*.cs'
```

Expected: no derived electric/gear topology in save DTOs and no new Mod callback.

- [ ] **Step 5: Recheck persistence identifiers, master-derived values, and format**

Inspect these exact save paths:

```powershell
rg -n "BlockGuid|ItemGuid|GetItemGuid|GetItemId|JsonProperty|BatteryRemaining|SelectedIndex|BatteryCapacity|OutputModes|MaxGeneratedPower" moorestech_server/Assets/Scripts/Game.World moorestech_server/Assets/Scripts/Game.Block/Blocks/ElectricWire moorestech_server/Assets/Scripts/Game.Block/Blocks/GearChainPole moorestech_server/Assets/Scripts/Game.Block/Blocks/ElectricToGear moorestech_server/Assets/Scripts/Game.Block/Blocks/GearToElectric --glob '*.cs'
rg -n "ItemGuid|FluidGuid|GetItemId|GetFluidId|Capacity|MaxStack|Weight|FilterItemGuids" moorestech_server/Assets/Scripts/Core.Item.Interface/IItemStackJsonObject.cs moorestech_server/Assets/Scripts/Game.Fluid/FluidContainerSaveJsonObject.cs moorestech_server/Assets/Scripts/Game.Block/Blocks/FilterSplitter/VanillaFilterSplitterComponent.cs
```

Required findings:

- `WorldBlockDatastore.GetSaveJsonObject()` writes `BlockGuid`, while `LoadBlockDataList()` resolves it with `MasterHolder.BlockMaster.GetBlockId`.
- `ElectricWireSaveDataJsonObject` and `GearChainPoleSaveDataJsonObject` write connection material `ItemGuid`; component load calls `MasterHolder.ItemMaster.GetItemId`.
- `ElectricToGearGeneratorSaveJsonObject` writes only `SelectedIndex` and runtime `BatteryRemaining`; capacity and output-mode values are read from the block master.
- `GearToElectricGeneratorSaveJsonObject` writes only runtime `BatteryRemaining`; `MaxGeneratedPower` remains master-derived.
- Save objects use Newtonsoft JSON. MessagePack usage remains limited to client `BlockStateDetail` and protocol payloads.
- `ItemStackSaveJsonObject`, `FluidContainerSaveJsonObject`, and FilterSplitter remain the reference pattern: GUIDs plus runtime amounts/content are saved, while capacity, slot count, stack limit, and weight remain master-derived.
- No new global `ItemId`, `FluidId`, or `BlockId` persistence depends on master load order or `SortPriority`.

- [ ] **Step 6: Compile, run focused regressions, and commit audit fixes**

```powershell
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricTickUnificationTest|ElectricWireSaveLoadTest|GearNetworkTest|GearMachineSaveLoadTest|GetElectricNetworkInfoProtocolTest|GetGearNetworkInfoProtocolTest"
git add moorestech_server/Assets/Scripts
git commit -m "test: ロード再構築と内部観測境界を検証"
```

---

### Task 8: Bug-hunt QA and final branch commit

**Files:**
- Review: all changes in `master...HEAD`
- Modify: only defects found by the checks below

**Interfaces:**
- Produces: merge-ready `feature/ElectricTickUnification3`

- [ ] **Step 1: Check repository hygiene and file constraints**

```powershell
git diff --check master...HEAD
git status --short
Get-ChildItem moorestech_server/Assets/Scripts -Recurse -Filter *.cs | ForEach-Object { if ((Get-Content $_.FullName).Count -gt 200 -and (git diff --name-only master...HEAD) -contains $_.FullName.Substring((Get-Location).Path.Length + 1).Replace('\','/')) { $_.FullName } }
rg -n "\bpartial\b|<<<<<<<|=======|>>>>>>>" $(git diff --name-only master...HEAD -- '*.cs')
```

Expected: no whitespace/conflict issue, no newly introduced partial, and every newly created or materially rewritten C# file is at most 200 lines. Preserve the user-owned untracked `.agents/...` and `.codex/config.toml` files.

- [ ] **Step 2: Run the full focused subsystem suite**

```powershell
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Electric|Gear|CleanRoom|SaveJsonFile|WorldSaveCoordinator|TickEndPacket|MachineRecipeSelection|MachineRecipeChangeRefund|BlueprintMachineRecipeSelection"
```

Expected: every selected test passes.

- [ ] **Step 3: Compile again after tests and inspect errors**

```powershell
uloop compile --project-path ./moorestech_client
uloop get-logs --project-path ./moorestech_client --log-type Error
```

Expected: compile succeeds; no new relevant Unity error remains.

- [ ] **Step 4: Re-run merge conflict prediction**

```powershell
git merge-tree --write-tree master HEAD
```

Expected: no content conflict because the branch already contains master and the resolved processor.

- [ ] **Step 5: Inspect final diff and commit any QA fixes**

```powershell
git diff --stat master...HEAD
git diff --check
git status --short
git add moorestech_server/Assets/Scripts docs/superpowers
git commit -m "fix: 電力歯車tick統合のQA指摘を修正"
```

If no QA fix exists, do not create an empty commit. End with a clean tracked worktree and report all commit hashes and test results.

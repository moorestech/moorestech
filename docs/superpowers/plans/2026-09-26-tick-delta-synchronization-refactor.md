# Tick差分通知の共通化 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** train/railの既存tick・seq仕様を保ちながら、差分通知の機械的処理を別domainからも利用できる単独PRにする。

**Architecture:** セッションuint tickの時計をMasterTickUpdaterで明示的に進め、streamごとのseqを別instanceで管理する。clientはstreamごとの状態・順序buffer・進行計算を共通部へ抽出し、train contextがそれらを所有する。train固有のhash・欠落時判断・snapshot・resyncはtrain側に残す。

**Tech Stack:** Unity/C#、UniRx、UniTask、MessagePack、Microsoft DI、VContainer、NUnit、uloop。

**Baseline:** `origin/master` = `9cec17ddcb88f2ae798352ddf876370876589ef0`。worktree `C:/Users/5080/moorestech-worktrees/tick-delta-refactor`、branch `codex/tick-delta-refactor`。

## Requirements

- R1: ユーザー「差分通知の部分だけ」「masterブランチからリファクタリングPR作成」。「まずはこれだけ単体PR」を満たし、gear/belt本体・GPU・搬送・schema・save変更を含めない。受入: PR差分の範囲確認。
- R2: 既存ユーザー「tickとseq idの仕様はこのまま」。受入: uint session tick、tickごとseq0、最初の発行seq1、合成key、hash(n-1)+diff(n)、空diffトリガ、train/rail共通streamを回帰テストする。
- R3: ユーザー「鉄道べったりだったのを今後ギアやベルコンの差分通知に拡張」。受入: common部にTrain/Rail型・payload・通信tagがなく、2つの非train fixtureで同tick同seqを独立に適用できる。
- R4: stream Aの採番、適用、snapshot watermark、gate停止がstream Bへ作用しない。受入: server2streamとclient2contextのテストで相互不干渉を確認する。
- R5: 既存train startup/recoveryを維持。受入: rail→train初期push、購読前buffer/replay、view生成後の初期完了、失敗のfault伝搬、resync要求に対応するsnapshot適用通知・watermark・cache/view差し替え、古いevent破棄を検証する。ackと通常tick前進だけでは再同期成功としない。
- R6: 永続累積GameUpdater.CurrentTickとwire session tickの非同一を維持する。受入: save tickを復元しても新sessionのwire tickは0起点、既存train save/loadと乗車入力の回帰が通る。
- R7: ユーザー「過剰なawaitとかあったでしょ？」を実態に基づき整理する。受入: SendTrainResyncの中継state machineを除去、差分Applyは同期のまま、真の通信待ち・初期完了待ち・main-thread dispatchを維持する。
- R8: ユーザー「あなたはオーケストレーションに徹して。レビューはまた別にsol起動してやって」。受入: 実装担当と別のgpt-6-sol/highが全branchをレビューし、必要な修正と検証後にPRを作成する。

## Global Constraints

- [ADR 0071](../../adr/0071-tick-synchronization-stream-boundaries.md)を全タスクに適用。agent前提をユーザー裁定へ昇格しない。
- C#変更後は `uloop compile --project-path ./moorestech_client`。1コードファイル200行以下、新規ディレクトリ内コード10ファイル以下、partial/Func禁止。新しい汎用domain registryは作らない。
- meta/Unity YAMLは手書き禁止。Unity担当だけがEditorでmetaを生成する。Library削除禁止。原本 `C:/Users/5080/Documents/GitHub/moorestech` はread-only。
- MessagePackのfield/key/tag、保存形式、hash間引き、dummy、force-slip、catch-up係数・上限、main-thread/同期replay順序を変えない。
- compile/test失敗を成功扱いしない。domain reloadエラー時は45秒後に再試行する。既知環境失敗は生出力を外部記録へ残す。
- 実装担当は始めにpwd/AGENTS.mdとtrain-systemを読む。レビューはユーザー指定 `model: gpt-6-sol`, `reasoning_effort: high` を明示する。
- 本計画は3実装タスク＋2終了タスク。C#約47〜57ファイル、論理追加/変更1050〜1650行、削除/移動600〜900行の概算。Task 3の保存乗車・実snapshot適用fixture強化を含む。実測diffで更新する。

## 配置と前例

| 項目 | 配置先 | 機構・現実の受益者・前例 |
|---|---|---|
| ServerTickClock / TickSequenceState / TickUnifiedIdUtility | Core.Update/TickSynchronization | ドメイン非依存のuint時計と順序値。既存TrainUpdateService/TrainUnitTickStateから抽出。server7packetとclient bufferが使う |
| TrainTickSequenceSource | Game.Train/Unit/TickSynchronization | train/rail streamの採番所有者。各packetからsimulation service依存を外し、別streamに同じsingletonが誤注入されない型付きcomposition境界 |
| ClientTickState / TickEventBuffer / ClientTickAdvanceController | Client.Game/Common/TickSynchronization | 既存TrainUnitTickState/FutureMessageBuffer/ClientSimulatorの機械的部分。ドメイン型を含めない |
| ITickBufferedEvent / TickBufferedEvent / ITickAdvanceGate | 同上 | 既存同期Apply契約・既存callback実装・既存gate契約の移動/改名。将来目的だけのinterfaceを新設しない |
| TrainTickContext / TrainUnitHashBuffer | Client.Game/InGame/Train/Network/TickSynchronization | train state/events/driver/hash bufferの所有と接続。現在のnetwork/applier/simulator/debugが使用する |
| TrainUnitHashVerifier / snapshot / DTO / view | 現在のtrain/通信配置 | train hashとresync、rail依存、view構築をcommonへ入れない |

呼出し鎖: `GameUpdater → MasterTickUpdater入口で旧tick保持・ServerTickClock.AdvanceTick → 電力/gear/fluid → train旧tick hash → train sequence.BeginTick → train simulation+diff → EventProtocolProvider → PacketExchangeManager main-thread dispatch → Train handlers → train context.Events → common driver → train gate → train visual update`。

同じ時計を共有することは同じseqを共有することではない。将来domainは別TickSequenceStateと別client contextを持ち、MasterTickUpdaterの同じ明示境界からtickを受ける。train/rail内だけは従来のseq範囲を維持する。現PRは将来domainの登録・自動列挙機構を作らない。

## 操作の死活表

| 現在の操作・経路 | 計画後 | 根拠 |
|---|---|---|
| train走行・手動乗車入力・自動運転 | 維持 | 旧tick hash→入力集計→simulation→diffの順を固定 |
| train/car生成・追加・削除 | 維持 | 既存構造snapshot eventを同じstreamへenqueue |
| rail接続・撤去・GUID不一致拒否 | 維持 | handlerのpayload適用bodyを変更しない |
| 初期接続・保存された乗車状態の復帰 | 維持 | rail→train snapshot、車両view完成、初期待機後にplayer開始 |
| hash不一致からの復旧 | 維持 | train verifierの要求・現役所有・snapshot完了解除を維持 |
| train debug表示 | 維持 | 同じtrain contextのtick/seqを表示 |
| 通常save/load・累積tick復元 | 維持 | saveデータ・loaderを変更せず、wire clockを別instanceで開始 |

### Task 1: serverの時計とstream採番をsimulationから分離する

**Files:**
- Create: `moorestech_server/Assets/Scripts/Core.Update/TickSynchronization/ServerTickClock.cs`
- Create: `moorestech_server/Assets/Scripts/Core.Update/TickSynchronization/TickSequenceState.cs`
- Create: `moorestech_server/Assets/Scripts/Core.Update/TickSynchronization/TickUnifiedIdUtility.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Train/Unit/TickSynchronization/TrainTickSequenceSource.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Train/Unit/TickSynchronization/TrainTickDiffData.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Train/Unit/TickSynchronization/TrainHashStateEventData.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Train/Unit/TrainUpdateService.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MasterTickUpdater.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Event/EventReceive/TrainUnitTickDiffBundleEventPacket.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Event/EventReceive/TrainUnitSnapshotEventPacket.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Event/EventReceive/TrainFullSnapshotEventPacket.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Event/EventReceive/RailNodeCreatedEventPacket.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Event/EventReceive/RailNodeRemovedEventPacket.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Event/EventReceive/RailConnectionCreatedEventPacket.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Event/EventReceive/RailConnectionRemovedEventPacket.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/TrainCarRidingInputProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Util/MessagePack/TrainUnitTickDiffBundleMessagePack.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Util/MessagePack/TrainUnitTickDiffMessagePack.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/Util/TrainTestHelper.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainDiagramSaveLoadTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainBidirectionalCargoLoopSaveDataTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainHugeAutoRunSaveLoadConsistencyTest.cs`（実class名: `TrainHugeAutoRunTrainSaveLoadConsistencyTest`）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/TickSynchronization/TickSequenceStateTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/TrainTickSequencePacketTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/TrainFullSnapshotEventPacketTest.cs`

**Interfaces:**
- Produces: `ServerTickClock.Tick : uint { get; private set; }`, `void ServerTickClock.AdvanceTick()`.
- Produces: `TickSequenceState.Tick : uint { get; private set; }`, `TickSequenceState.SequenceId : uint { get; private set; }`, `void BeginTick(uint tick)`, `uint NextSequenceId()`.
- Produces: `ulong TickUnifiedIdUtility.CreateTickUnifiedId(uint tick, uint tickSequenceId)`.
- Produces: `TrainTickSequenceSource.Sequence : readonly TickSequenceState` (実stream所有者、staticではない).
- Produces: `void TrainUpdateService.PublishCurrentTickHash(uint tick)`, `void TrainUpdateService.UpdateTrains(uint tick)`.
- Consumes: 現在のTrainUpdateService.OnHashEvent/OnPreSimulationDiffEvent。payloadは同じfield構成の外部readonly structへ移動するだけ。

- [ ] **Step 1: 独立streamとpacket順序のテストを追加する。**

```csharp
[Test]
public void Streams_ResetIndependentlyAtTheSharedTick()
{
    var clock = new ServerTickClock();
    var first = new TickSequenceState();
    var second = new TickSequenceState();
    clock.AdvanceTick();
    first.BeginTick(clock.Tick);
    second.BeginTick(clock.Tick);
    Assert.AreEqual(1u, first.NextSequenceId());
    Assert.AreEqual(2u, first.NextSequenceId());
    Assert.AreEqual(1u, second.NextSequenceId());
    var watermark = first.SequenceId;
    Assert.AreEqual(2u, watermark);
    Assert.AreEqual(1u, second.SequenceId);
    clock.AdvanceTick();
    first.BeginTick(clock.Tick);
    second.BeginTick(clock.Tick);
    Assert.AreEqual(0u, first.SequenceId);
    Assert.AreEqual(0u, second.SequenceId);
    Assert.AreEqual(2u, first.Tick);
}
```

Packet fixtureは既存CapturedEventSinkとDIを使用する。handshake後 `GameUpdater.UpdateOneTick()` を2回呼び、bundle1はServerTick=1/HashTickSequenceId=1/DiffTickSequenceId=1、bundle2はServerTick=2/HashTickSequenceId=2/DiffTickSequenceId=1を検証する（何も生成しない空world）。間に対象playerだけへのfull snapshot pushを入れても同値であることを確認する。GameUpdater.RestoreCurrentTick(5000)後、新DIのwire clock=0、1update後wire=1/累積=5001であることを確認し、finallyでテスト前の累積値へ戻す。

- [ ] **Step 2: 小さいcommon stateを実装し、MasterTickUpdaterから明示駆動する。**

```csharp
public sealed class ServerTickClock
{
    public uint Tick { get; private set; }
    public void AdvanceTick() => Tick++;
}
public sealed class TickSequenceState
{
    public uint Tick { get; private set; }
    public uint SequenceId { get; private set; }
    public void BeginTick(uint tick)
    {
        Tick = tick;
        SequenceId = 0;
    }
    public uint NextSequenceId() => ++SequenceId;
}
public static class TickUnifiedIdUtility
{
    public static ulong CreateTickUnifiedId(uint tick, uint tickSequenceId)
        => ((ulong)tick << 32) | tickSequenceId;
}
public sealed class TrainTickSequenceSource
{
    public readonly TickSequenceState Sequence = new();
}
```

各型はFilesの別ファイルへ置く。common3型は `Core.Update.TickSynchronization`、train ownerと2payloadは `Game.Train.Unit` namespace。sourceのreadonly fieldはDI上のstream識別境界であり、新domainはこのtrain ownerを再利用しない。

D2/Aユーザー裁定に従い、MasterTickUpdater.Updateの入口で旧tickを保持して時計を進める。gear/fluid/blockの前後関係を動かさず、既存train境界で旧tick hashと新tickのstream開始を行う。

```csharp
var previousTick = _serverTickClock.Tick;
_serverTickClock.AdvanceTick();
var currentTick = _serverTickClock.Tick;
// Existing topology, electric, gear and fluid updates remain here.
_trainUpdateService.PublishCurrentTickHash(previousTick);
_trainTickSequenceSource.Sequence.BeginTick(currentTick);
_trainUpdateService.UpdateTrains(currentTick);
```

TrainUpdateServiceのBuildHashStateEventDataをPublishCurrentTickHashのlocal functionへ移し、OnHashEvent.OnNextまでをこのメソッドで実行。UpdateTrains(uint tick)は旧 `_executedTick` を引数tickに置換し、入力集計・simulate・NotifyPreSimulationDiffだけを既存順で実行する。内部2structを各新ファイルへ移し、nested type参照を新型へ直すことで200行以下へ収める。debugの挙動は変更しない。

```csharp
services.AddSingleton<ServerTickClock>();
services.AddSingleton<TrainTickSequenceSource>();
services.AddSingleton<TrainUpdateService>();
```

全packetのtick/seq依存は `_trainTickSequenceSource.Sequence.Tick` / `.NextSequenceId()` / `.SequenceId` に置換する。TickDiffBundleEventPacketだけはhash/diff購読のためTrainUpdateServiceも保持する。TrainCarRidingInputProtocolはServerTickClock.Tickを入力受信tickへ記録する。同protocolを手動newするPacketResponseCreatorも、serviceProviderからServerTickClockを取得してconstructorへ渡すよう追従する。旧GetCurrentTick/NextTickSequenceId/GetCurrentTickSequenceId/ResetTickをTrainUpdateServiceから削除する。

テストのResetTick呼出しは、新DI生成直後または破棄対象環境のcleanupにしかないため削除する。実行中環境を巻き戻すtest-only public APIを新設しない。既存SaveLoad fixtureが同じDIを再利用する場合はfixtureを新DIに切り替えるがsave/loader本体は触らない。

- [ ] **Step 3: compileとserver回帰を実行する。**

Run: `uloop compile --project-path ./moorestech_client`

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TickSequenceStateTest|TrainTickSequencePacketTest|TrainFullSnapshotEventPacketTest|TrainResyncProtocolTest|TrainCarRidingInputBufferTest|TrainCarRidingManualCommandResolverTest|TrainDiagramSaveLoadTest|TrainBidirectionalCargoLoopSaveDataTest|TrainHugeAutoRunTrainSaveLoadConsistencyTest" --timeout-seconds 1500`

Expected: compile ErrorCount=0、対象全件PASS。packet tags/keysとsave差分ゼロ。

- [ ] **Step 4: Task 1対象とEditor生成metaをコミットする。**

Commit: `refactor: separate server tick clock and train stream sequence`

### Task 2: clientの順序処理と進行計算をstream単位で抽出する

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/Common/TickSynchronization/ClientTickState.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/Common/TickSynchronization/TickEventBuffer.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/Common/TickSynchronization/ITickBufferedEvent.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/Common/TickSynchronization/TickBufferedEvent.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/Common/TickSynchronization/ITickAdvanceGate.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/Common/TickSynchronization/ClientTickAdvanceController.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/TickSynchronization/TrainTickContext.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/TickSynchronization/TrainUnitHashBuffer.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Unit/TrainUnitTickState.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Unit/ITrainUnitHashTickGate.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/TrainUnitFutureMessageBuffer.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/ITrainTickBufferedEvent.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/TrainTickBufferedEvent.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Unit/TrainUnitClientSimulator.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/View/TrainUnitHashVerifier.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/View/TrainUnitSnapshotApplier.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/RailGraphSnapshotApplier.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/TrainFullSnapshotEventNetworkHandler.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/TrainUnitSnapshotEventNetworkHandler.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/TrainUnitTickDiffBundleEventNetworkHandler.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/RailGraphCacheNetworkHandler.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/RailGraphConnectionNetworkHandler.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Debug/TrainUnitDebugOverlayPresenter.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Debug/TrainUnitDebugStatusFormatter.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/TrainUnitTickStateTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/TrainUnitFutureMessageBufferTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/TickSynchronization/ClientTickStreamIsolationTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/TickSynchronization/ClientTickAdvanceControllerTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/TickSynchronization/TrainTickHashGateTest.cs`

**Interfaces:**
- Consumes: Task 1の `TickUnifiedIdUtility.CreateTickUnifiedId(uint,uint)`。
- Produces: internal `ClientTickState`。旧TrainUnitTickStateのmethod名/戻り値/挙動を維持する。
- Produces: internal `ITickBufferedEvent { void Apply(); }`, `TickBufferedEvent.Create(Action applyAction) : ITickBufferedEvent`。既存の同期callback契約の移動。
- Produces: internal `TickEventBuffer(ClientTickState state)`, `void EnqueueEvent(uint tick,uint sequence,ITickBufferedEvent bufferedEvent)`, `bool TryFlushEvent(ulong id)`, `bool TryFlushEvent(uint tick,uint sequence)`, `void DiscardEventsAtOrBelow(ulong watermark)`。
- Produces: internal `ITickAdvanceGate { bool CanAdvanceTick(ulong currentTickUnifiedId); }`。
- Produces: internal `ClientTickAdvanceController(ClientTickState state,TickEventBuffer events)`, `double Advance(float deltaTime,ITickAdvanceGate gate)`。
- Produces: public `TrainTickContext()`、internal readonly fields `State : ClientTickState`, `Events : TickEventBuffer`, `AdvanceController : ClientTickAdvanceController`, `Hashes : TrainUnitHashBuffer`。
- Produces: internal `TrainUnitHashBuffer(ClientTickState state)`。旧FutureMessageBufferのhash tuple/API・DummyHash・first-hash logをそのまま移動する。

- [ ] **Step 1: common適用の非train fixtureと既存gateケースをテストする。**

```csharp
[Test]
public void EqualIdsInSeparateStreams_DoNotCollideOrPurgeEachOther()
{
    var firstState = new ClientTickState();
    var secondState = new ClientTickState();
    var first = new TickEventBuffer(firstState);
    var second = new TickEventBuffer(secondState);
    var firstEvent = new CountingTickEvent();
    var secondEvent = new CountingTickEvent();
    first.EnqueueEvent(1, 1, firstEvent);
    second.EnqueueEvent(1, 1, secondEvent);
    first.DiscardEventsAtOrBelow(TickUnifiedIdUtility.CreateTickUnifiedId(1, 1));
    Assert.IsFalse(first.TryFlushEvent(1, 1));
    Assert.IsTrue(second.TryFlushEvent(1, 1));
    Assert.AreEqual(0, firstEvent.Count);
    Assert.AreEqual(1, secondEvent.Count);
    Assert.AreEqual(0ul, firstState.GetAppliedTickUnifiedId());
}
private sealed class CountingTickEvent : ITickBufferedEvent
{
    public int Count { get; private set; }
    public void Apply() => Count++;
}
```

既存4bufferテストと4stateテストは新型へ移す。追加fixtureは2つのdriverへ別gate（片方常時false、片方true）を渡し、0.05fを100frame入力して停止側が0、進行側が正のtickになることを確認する。同seqの別payloadが未適用時に置換されること、逆順到着をexact-key順にflushすること、適用済み以下は再適用されないことを確認する。gate fixtureはdummy、hash一致、不一致、future hashのみ、hash無し、resync待機をそれぞれ検証し、期待する警告はLogAssert.Expectで明示する。

- [ ] **Step 2: commonを切り出してtrain contextへ配線する。**

旧TrainUnitTickState bodyは名前/namespace/utility参照のみ変更。旧FutureMessageBufferをevent部分とtrain hash部分へ機械的に分割する。hashのTryDequeueは既存どおり非消費readであることを保つ。共通bufferへhash tupleやDummyHashを入れない。

```csharp
public sealed class TrainTickContext
{
    internal readonly ClientTickState State;
    internal readonly TickEventBuffer Events;
    internal readonly ClientTickAdvanceController AdvanceController;
    internal readonly TrainUnitHashBuffer Hashes;
    public TrainTickContext()
    {
        State = new ClientTickState();
        Events = new TickEventBuffer(State);
        AdvanceController = new ClientTickAdvanceController(State, Events);
        Hashes = new TrainUnitHashBuffer(State);
    }
}
```

ClientTickAdvanceControllerは旧TrainUnitClientSimulatorの推定tickフィールド・定数・Tick本文を移す。変更は `Time.deltaTime` を引数deltaTime、`_hashTickGate` を引数gate、末尾visual updateを `return _estimatedClientTick` にするだけ。_localcnt・減衰0.9991・warmup20・0.5f・min1e-5・catch-up係数1e-4/1e-3・4tick上限・gate停止時tick+1の補正を保持する。不要なコメントアウト行だけ除く。

```csharp
public void Tick()
{
    var renderTick = _context.AdvanceController.Advance(Time.deltaTime, _hashVerifier);
    _visualUpdateSystem.UpdateAll(renderTick, _context.State.GetTick());
}
```

TrainUnitClientSimulatorは `(TrainTickContext context, TrainUnitHashVerifier hashVerifier, TrainUnitVisualUpdateSystem visualUpdateSystem)` を受け取る。TrainUnitHashVerifierはITickAdvanceGateを実装し、context.State/context.Hashesを利用する。gateのboolは既存の進行可否契約で、新しい意思決定結果を呼出し側に組み立てさせる変更ではない。

全network/applier/debugの旧state/buffer注入をTrainTickContextへ置換し、event操作はcontext.Events、hash操作はcontext.Hashes、位置操作はcontext.Stateへ一意に寄せる。TrainUnitSnapshotApplier/RailGraphSnapshotApplier/TrainFullSnapshotEventNetworkHandlerの適用bodyと完了順を変更しない。debug formatterがinternal型をpublic引数に出さないようFormatはTrainTickContextを受け取る。

```csharp
builder.Register<TrainTickContext>(Lifetime.Singleton);
builder.Register<TrainUnitClientSimulator>(Lifetime.Singleton).AsSelf().As<ITickable>();
builder.Register<TrainUnitHashVerifier>(Lifetime.Singleton).AsSelf().As<IDisposable>();
```

旧state/buffer/gate interfaceのunkeyed DI登録を削除する。将来streamは独自のdomain contextでcommon instanceを所有し、TrainTickContextを注入しない。

```csharp
public UniTask<TrainResyncProtocol.ResponseMessagePack> SendTrainResync(bool includeRailGraph, CancellationToken ct)
{
    var request = new TrainResyncProtocol.RequestMessagePack(includeRailGraph);
    return _packetExchangeManager.GetPacketResponse<TrainResyncProtocol.ResponseMessagePack>(request, ct);
}
```

これは中継state machineの除去だけである。TrainUnitHashVerifierのack await、初期source、PacketExchangeManagerのmain-thread Yield、view animation待機は維持する。

- [ ] **Step 3: compileとclient回帰を実行する。**

Run: `uloop compile --project-path ./moorestech_client`

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainUnitTickStateTest|TrainUnitFutureMessageBufferTest|ClientTickStreamIsolationTest|ClientTickAdvanceControllerTest|TrainTickHashGateTest|TrainFullSnapshotFailurePropagationTest|InitialEventApplyWaiterTest|PlayerRuntimeStartGateTest|PlayerRideFollowTest"`

Expected: compile ErrorCount=0、対象全件PASS。commonディレクトリへの `rg -n "Game.Train|InGame.Train|RailGraph|UnitsHash|MessagePack"` は0件。旧型参照は削除ファイル以外0件。

- [ ] **Step 4: Task 2対象とEditor生成metaをコミットする。**

Commit: `refactor: extract client tick stream ordering and progression`

### Task 3: snapshot・差分・実起動の受入を固定する

**Files:**
- Test: `moorestech_client/Assets/Scripts/Client.Tests/TickSynchronization/TrainTickSynchronizationIntegrationTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/TickSynchronization/TrainSnapshotStartupGateTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Train/TrainTickSynchronizationPlayTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Train/SavedRidingWorldFixture.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Train/TrainSnapshotApplyObservation.cs`
- Modify: `docs/train/TrainRailClientServerEventFlow.md`
- Modify: `docs/superpowers/plans/2026-09-26-tick-delta-synchronization-refactor.md`

**Interfaces:**
- Consumes: Task 1 server clock/sequence、Task 2 TrainTickContext、既存TrainFullSnapshotEventNetworkHandler/TrainUnitTickDiffBundleEventNetworkHandler、InitialEventApplyWaiter、PlayerObjectController。
- Produces: テストのみ。debug/test専用public APIを製品に追加しない。
- Produces: test-only `SavedRidingWorldFixture`。一時server data/world directory、保存したplayerId/TrainUnitInstanceId/TrainCarInstanceId/seatIndexを保持し、既存serializerで作ったsaveを実起動へ渡す。
- Produces: test-only `TrainSnapshotApplyObservation`。既存raw event購読とOnFullSnapshotApplied購読を束ね、要求直前から1組のrail/train payload・適用watermark・適用直後のstate/hash/cache/view参照を記録する。製品へrequest IDや観測APIを追加しない。

- [ ] **Step 1: 実payloadの適用と初期待機を検証するテストを追加する。**

既存CapturedEventSinkで得たfull snapshot/bundleのMessagePack payloadを実handlerへ渡す。private受信methodへのreflectionは既存TrainFullSnapshotFailurePropagationTestと同じtest-only手法を使用する。空worldは実cacheと空rail/storeを用意し、nullを利用した成功経路の省略をしない。

```csharp
[Test]
public void TrainInitialApply_RemainsPendingBeforePayload()
{
    var handler = new TrainFullSnapshotEventNetworkHandler(null, null, null);
    var pending = handler.WaitForInitialApplyAsync().Preserve();
    Assert.AreEqual(UniTaskStatus.Pending, pending.Status);
}
```

成功fixtureはrail snapshot→train snapshotを実applierへ通してからSucceededとなること、空diff bundleを受けた後のcommon driverが1tick進み再flushで二重進行しないことを確認する。upsert/deleteはTrainUnitSnapshotEventNetworkHandlerを通しcacheとviewの削除まで検証する。snapshot watermark以下のeventが破棄され、より新しいeventが残ることも実handlerで確認する。

初期乗車fixtureでは `IPlayerRidingDatastore.LoadSaveData` に対象車両/座席の保存状態を入れたserverから実 `InitialHandshakeProtocol` 応答とrail/train初期payloadを取得する。応答のRidingStateType=Restored、RidingTargetの車両ID、RidingSeatIndexを確認し、`InitialHandshakeResponse` へ変換する。clientは実TrainFullSnapshotEventNetworkHandler/applier、TrainCarObjectDatastore、InitialEventApplyWaiterを使う。train payloadを保留している間は待機Pending、player runtime未開始・ThirdPersonController無効をassertする。rail→trainを適用して実車両viewを生成した後、既存 `MainGameStarter.RestoreLoginState` → `MainGameContainerActivation.RestoreLoginState` → `InitialRideTrainCarRequest` → `TrainHUDScreenState` → `RidingPlayerState` → `TrainCarRideFollowTargetResolver` を通す。SetRideFollowTargetやPlayerStateController.SetStateをテストから直接呼んで代替しない。

成功条件はUIStateControl.CurrentState=TrainHUDScreen、PlayerStateController.CurrentState=Riding、TrainHUDScreenState.IsRiding=true、handshakeの車両IDのentityが実datastoreに存在、指定seat markerが解決できること。初期化後のLateUpdateでplayer poseがそのseat transformへ一致し、乗車中のThirdPersonControllerは無効である。実車両のseat markerをテスト中だけ移動/回転して次のLateUpdateを通し、playerも同じ差分へ追従することを確認してmarkerを復元する。手動追従だけの既存PlayerRideFollowTest、runtime gate単独のPlayerRuntimeStartGateTest、serverのInitialHandshakeProtocolTest/PlayerRidingDatastoreTestは補助回帰として実行する。

復旧fixtureは一致/dummy/不一致/future-only/emptyを固定する。snapshot完了がackより先の場合、旧ack失敗が次resync gateを解除しない場合、snapshot失敗が待機faultとなる場合をそれぞれ確認する。これは既存要求所有の検証であり、新retry仕様を追加しない。

- [ ] **Step 2: EditModeInPlayingTestで実起動・tick前進・resyncを検証する。**

`editmode-in-playing-test`を使用し、EnterPlayModeUtil → UnityTest直下のEnterPlayMode → LoadMainGame → 検証 → ExitPlayMode → SessionState復旧という既存順で実装する。namespaceは `Client.Tests.EditModeInPlayingTest`、CI categoryは既存boot同様 `CiShardClientPlay3`。このplanでは入力や画面レイアウトを変更しないため、録画付き通しプレイよりstate検証が直接的な軽量代替を採用する。

実起動には空worldでなく、接続済みrail・停止中の座席付き車両1台・保存乗車状態を持つfixtureを使う。既存EditMode server dataのtrainCarsは空で、server unit-testの座席車両はaddressablePathが空なので、そのまま成功fixtureにしない。`SavedRidingWorldFixture` は一時コピーしたtest server dataだけへ必要なrail定義と車両定義を追加し、車両のaddressablePathは登録済み `Vanilla/Train/Locomotive`、座席は実Prefabに存在するindex0を使う。共有test masterやPrefab/YAMLを編集しない。既存TrainTestHelper/RidingTestHelperのrail・車両構築手順と `IPlayerRidingDatastore.TryRide` を用い、`AssembleSaveJsonText.AssembleSaveJson` で生成したJSONを `WorldDataDirectory.SaveJsonFilePath` に書く。WorldProvisionerで用意したworldに対してDIを作り、この保存fixture用DIはゲームserverを起動する前に終了する。車両IDとseatIndexをfixture側に記録して期待値とする。

このworld directoryを `LoadMainGame(serverDirectory, worldDirectory)` へ渡し、実loader→handshake→MainGameInitializationFinalizer→RestoreLoginStateを通す。GameInitialized時に解決した実InitialHandshakeResponseのRidingTarget/SeatIndexが保存fixtureと等しいこと、初期snapshot完了、対象車両view存在、上記HUD/PlayerState/seat poseをassertする。LoadMainGameは初期化完了を待たず固定1秒で戻るため、呼出前からGameInitializedEventを購読し、その通知を180秒上限で待つ。初期source待機と車両存在を満たす前にplayerが始動しないことは、Step 1の遅延payload fixtureと起動中の観測で確認する。

続いて同じ停止中車両のあるworldで、再同期要求直前に観測を開始する。raw rail/train full snapshotの購読とOnFullSnapshotAppliedの購読はSendTrainResyncより前に登録する。初期snapshotは既に完了済みで、fixtureは自動resyncがない正常hash状態から要求を1回だけ送り、観測したfull snapshotはrail1件/train1件/applied1件であることを確認する。別要求の通知が混ざった場合は件数で失敗させる。

```csharp
async UniTask VerifyLiveTickSync(TrainUnitInstanceId trainId, TrainCarInstanceId carId)
{
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var context = resolver.Resolve<TrainTickContext>();
    var handler = resolver.Resolve<TrainFullSnapshotEventNetworkHandler>();
    var trainCache = resolver.Resolve<TrainUnitClientCache>();
    var railCache = resolver.Resolve<RailGraphClientCache>();
    var views = resolver.Resolve<TrainCarObjectDatastore>();
    var beforeUnit = trainCache.Units[trainId];
    var railNodeId = railCache.Nodes.First(node => node != null).NodeId;
    var beforeRailNode = railCache.Nodes[railNodeId];
    Assert.IsTrue(views.TryGetEntity(carId, out var beforeView));
    using var observation = new TrainSnapshotApplyObservation(
        ClientContext.VanillaApi.Event, handler, context, trainCache, railCache, views, trainId, carId, railNodeId);
    var ackTask = ClientContext.VanillaApi.Response.SendTrainResync(true, CancellationToken.None).Preserve();
    for (var frame = 0; frame < 300 && !observation.HasCompletePair; frame++)
        await UniTask.Yield();
    Assert.IsTrue(observation.HasCompletePair, "resync snapshot payload/applyが揃わなかった");
    Assert.AreEqual(1, observation.RailCount);
    Assert.AreEqual(1, observation.TrainCount);
    Assert.AreEqual(1, observation.AppliedCount);
    Assert.Less(observation.RailReceivedOrder, observation.TrainAppliedOrder);
    Assert.AreEqual(observation.TrainWatermark, observation.AppliedWatermark);
    Assert.AreEqual(observation.RailWatermark, observation.TrainWatermark);
    Assert.AreEqual(observation.AppliedWatermark, observation.StateAtApply);
    Assert.AreEqual(observation.TrainPayloadHash, observation.TrainHashAtApply);
    Assert.AreEqual(observation.RailPayloadHash, observation.RailHashAtApply);
    Assert.AreNotSame(beforeUnit, observation.TrainUnitAtApply);
    Assert.AreNotSame(beforeRailNode, observation.RailNodeAtApply);
    Assert.AreNotSame(beforeView, observation.CarViewAtApply);
    Assert.AreEqual(carId, observation.CarViewAtApply.TrainCarInstanceId);
    Assert.IsNotNull(await ackTask);
    var appliedTick = (uint)(observation.AppliedWatermark >> 32);
    for (var frame = 0; frame < 300 && context.State.GetTick() <= appliedTick; frame++)
        await UniTask.Yield();
    Assert.Greater(context.State.GetTick(), appliedTick);
}
```

`TrainSnapshotApplyObservation` のHasCompletePairは3種の観測が各1件揃った条件。raw rail payloadからGraphTick/GraphTickSequenceId/GraphHash、raw train payloadからServerTick/WatermarkTickSequenceId/UnitsHashを記録し、watermarkはTickUnifiedIdUtilityで作る。OnFullSnapshotAppliedのcallback内でStateAtApply、両ComputeCurrentHash、対象train unit/node/car view参照を即時に保存する。raw train購読と製品handlerの購読順には依存せず、照合は両方の記録が揃ってから行う。fixtureは停止中の車両を使い、callback外の後続tickでhashを取り直さない。snapshotを捨ててackと通常diffだけ届く実装はHasCompletePair、OnNextだけ通知して適用しない実装は3つの参照更新assert、rail適用を落とした実装はrail参照/hash/orderのいずれかで失敗する。

観測購読はtest helperのDisposeで解除する。frame/時間上限で未完了なら受信件数・watermark・stateをassertに含める。PlayerStartsOnBuiltTerrainTestに合わせ、LogAssert.ignoreFailingMessagesはEnterPlayMode直後と終了の既知framework期間だけに限定し、起動・snapshot適用中のErrorを検出する。

- [ ] **Step 3: 対象全体のcompile・回帰・実起動を実行し結果を記録する。**

Run: `uloop compile --project-path ./moorestech_client`

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TickSequenceStateTest|TrainTickSequencePacketTest|TrainFullSnapshotEventPacketTest|TrainResyncProtocolTest|TrainUnitTickStateTest|TrainUnitFutureMessageBufferTest|ClientTickStreamIsolationTest|ClientTickAdvanceControllerTest|TrainTickHashGateTest|TrainTickSynchronizationIntegrationTest|TrainSnapshotStartupGateTest|TrainFullSnapshotFailurePropagationTest|InitialEventApplyWaiterTest|InitialHandshakeProtocolTest|PlayerRuntimeStartGateTest|PlayerRideFollowTest|PlayerRidingDatastoreTest|TrainCarRidingInputBufferTest|TrainCarRidingManualCommandResolverTest|TrainDiagramSaveLoadTest|TrainBidirectionalCargoLoopSaveDataTest|TrainHugeAutoRunTrainSaveLoadConsistencyTest" --timeout-seconds 1500`

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type class --filter-value "Client.Tests.EditModeInPlayingTest.TrainTickSynchronizationPlayTest" --timeout-seconds 1500`

Run: `uloop get-logs --project-path ./moorestech_client --log-type Error`

Expected: compile ErrorCount=0、対象全件PASS、保存乗車handshakeから実HUD/PlayerState/座席追従への復帰、再同期のrail→train payloadと適用watermark・両hash・cache/view差し替え、適用後のtick前進、今回変更によるErrorなし。実行環境不成立は未検証として報告する。性能benchは新設せず、速度向上値をPRへ書かない。

- [ ] **Step 4: 現実の新所有者をtrain通信フロー文書へ反映し、検証結果と実測diff規模をplanへ追記してコミットする。**

Commit: `test: cover tick stream isolation and train synchronization lifecycle`

### Task 4: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること

**Files:** Review all branch changes. 記録は製品repo外のmoorestech_logs/harnessへ置き、同repo READMEを先に読む。

**Interfaces:** Consumes: 実装済みbranch、ADR0071、全テスト出力。Produces: 独立review結果と指摘解消記録。

- [ ] rootが実装担当と異なるfresh `gpt-6-sol / high` を明示して `moores-code-review` を委譲する。ユーザー意図、seq domain分離、時計境界、stale/gap/dummy、snapshot/ack競合、保存・初期乗車、公開範囲を確認する。
- [ ] 実装担当が採用指摘を修正し、影響テストを実行する。判定経路・条件式・評価時点に触れた場合はTask 3のEditModeInPlayingTestも修正後のbinaryで再実行する。
- [ ] 必要な再レビューを別sol/highへ依頼し、未解消criticalがないことを確認する。全変更をコミットする。

### Task 5: セッション終了可能状態にすること

**Files:** PR descriptionとbranch。Products: master向けPR URL。

**Interfaces:** Consumes: review通過branch/検証証跡。Produces: push済みcommitと作成PR。

- [ ] `pr-create` スキルでPRを作成する担当をrootが起動する。masterとのconflictがあれば実装担当がmasterをmergeして解消、compile・必要な回帰後にpushする。
- [ ] 全作業がcommit/push済み、PRがmaster宛て、公開diffにbelt/GPU実装が混入していないことを確認する。
- [ ] PRをattach_artifactでtaskへ添付する。検証できなかった点はPRと最終報告へ正確に残す。作成だけでCIやmerge可能状態を推測せず、実際の状態を確認する。

## 判断記録（ADR）

| 決定 | 出所 | 対象 |
|---|---|---|
| 差分通知基盤だけのmaster向け独立PR | ユーザー裁定 2026-09-26「差分通知の部分だけ」「まずはこれだけ単体PR」 | R1、全タスク |
| tickとseq仕様の維持 | 既存ユーザー裁定「tickとseq idの仕様はこのまま」 | R2、ADR0071、Task 1/2 |
| 共通時計とstreamごとのseq、client context所有 | agent前提: 現実のtrain/rail streamとAGENTS.md依存方向 | Core.Update、train domain owner、client common/context |
| train hash・dummy・force-slip・snapshot/resyncはdomain側 | agent前提: 既存gateの実装責務、belt prototypeとは回復契約が異なる | Task 2 |
| 共通driverは明示駆動、既存event適用の委譲 | agent前提: MasterTickUpdaterとTrainUnitClientSimulator自身を前例にする | Task 1/2、配置表 |
| 初期snapshotの非同期待機を維持 | agent前提: 初期乗車の車両準備と現実の外部到着境界 | Task 2/3 |
| snapshot失敗はfault＋LogError、rethrowしない | 既存ユーザー決定 `.decisions/2026-08-03-Train適用失敗のrethrowは削除しADRを正とする.md` | Task 2/3 |
| SendTrainResync中継awaitだけを除去 | agent前提: 同期Applyはmasterで既に同期void、要求UniTaskは必要 | Task 2 |
| 実装3タスク・review/PR各1タスク | agent前提: server境界とclient境界は別々に回帰可能、最後に実起動を確認 | 全タスク |
| unity-playmode-recorded-playtestの軽量代替にEditModeInPlayingTest | agent前提: 表示/操作設計変更ではなく同じframe内の同期・起動順をassertするため | Task 3/4 |
| 独立事前質問の残問0 | rootが別sol/highで最新masterを独立調査、2026-09-26 | 計画前監査。agent前提をユーザー裁定として消し込んでいない |
| plan独立reviewのImportant2件を受入へ反映 | agent前提: 2026-09-26 gpt-6-sol/highのreview。再同期適用証拠と保存乗車の統合経路をTask 3へ具体化。仕様変更・ユーザー裁定ではない | Task 3のみ。Task 1/2のAPI・owner・初期化順は不変 |
| user-simulator review: 確信ある逸脱/要裁定0件 | rootが別gpt-6-sol/high（ユーザー指定モデル）へ委譲し2026-09-26完了。過去裁定コーパスの実体を参照できず、今回依頼とADRが主根拠という限界あり | 本planのユーザー意図review |
| shadow: 採点対象0件 | 新refactor依頼後のユーザー質問0件のためsample0。旧belt設計回答を本sessionのsampleへ流用しない | shadow対象なし |

## Self-review / 実行結果

- 内容の自己確認: R1〜R8はTask 1〜5へ対応。common配置はドメイン型を含まず、現在の同役割実装から抽出する。future-only/empty/dummy/mismatch/initial-pendingを区別し、空streamでも停止と再開の条件をテストする。
- 構造の自己確認: 配置表の全項目は既存の駆動元・DI所有・型依存と突合済み。新たな共通状態をstaticへ置かず、train context/sourceがstream識別の所有を担う。nested TrainTickDiffDataの利用先を全検索し、bundleと個別diff DTOの両方をTask 1へ列挙した。placeholder検索とgit diff --checkは問題なし。
- snapshot失敗・外部通信失敗の寿命は既存handler/verifierに残す。新しい永続封鎖stateを追加しない。既存stale snapshot完了通知やresync失敗policyを今回修正する場合はplanを更新して独立reviewへ戻す。
- 独立plan reviewはCritical0/Important2、両ImportantをTask 3へ反映した。型閉包の弱い発火は純粋なTickUnifiedIdUtilityのCore.Update配置1件で、reviewerは配置妥当と判定。Task 1/2の設計変更はない。
- user-simulator reviewは逸脱/要裁定0、shadowはsample0で対象なし。compile/test・PRは未実行。担当者が証跡を追記する。

### 実装・検証の実測記録（2026-09-26）

- Task 1 commit `ea1462933`、Task 2 commit `5c93d9d`。Task 1のプロトコル利用箇所に合わせ `Server.Protocol.asmdef` へ `Core.Update` 参照も追加した。Task 3は製品挙動を変更せず、実payload・保存乗車起動・snapshot再同期の受入を追加した。
- Task 3の実配置は計画の5ファイルに `TickSynchronization/TrainSnapshotClientFixture.cs`（実cache/applier/view構成の共用）と `TickSynchronization/TrainResyncRequestRaceTest.cs`（実socketでack順を制御）を加えた7 C#ファイル、841行。遅延乗車fixtureは実PrefabのAwakeが必要なため、指定パスの `TrainSnapshotStartupGateTest` を `Client.Tests.EditModeInPlayingTest` namespaceのPlayMode遷移テストとして実行する。
- baseline `9cec17ddcb88f2ae798352ddf876370876589ef0` からのC#差分はGitのrename検出込みで63ファイル、追加1950行・削除554行。保存乗車・実payload・race観測fixtureを含む実測であり、文書とmetaは別計上。性能benchは新設していない。
- 最終compileは `ErrorCount=0`、`WarningCount=105`、新規Task 3コード由来のwarningなし。復元した既存の自動compile markerにより広い再コンパイルが走り、既存generator等のwarningを含む。loaded Client.Tests MVID `7d33a154-d279-4656-b3b4-cb3e21695e21` と新fixture/methodをEditor reflectionで確認した。
- 影響対象の全回帰は **83/83 PASS、fail/skip 0**（10:41:18〜10:41:30 UTC）。Step 3のfilter全体と `TrainResyncRequestRaceTest` を実行し、server/client stream分離・hash gate・payload適用・startup gate・snapshot failure・handshake・乗車入力・指定save/load回帰を含む。
- 実起動は **1/1 PASS**（10:42:37〜10:42:57 UTC）。一時server data/worldへ実serializerで保存した乗車状態からloader→handshake→finalizer→実HUD/Riding/seat追従を復元した。再同期要求前にgateが停止していないことを確認し、raw rail/train各1件・適用通知1件、rail→train順、watermarkと適用state、両hash、train/rail/viewの参照差替え、適用後のtick前進を検証した。
- PlayMode遷移でuloop JSONは `UNITY_DISCONNECTED_AFTER_ACCEPT` を返すため、このworktreeのEditor logが出力先として示した実case別 `TestResults.xml` を保存して結果を判定した。0件や旧binaryの結果を成功として扱っていない。
- 最終Error取得は **3件**。未変更のCEF package validatorによるmacOS用metaパスのDirectoryNotFoundExceptionがEnterPlayMode直後、未変更のUserPacketHandlerによる切断LogErrorとSocketExceptionがExitPlayMode後に各1件。実起動・snapshot適用の検証区間は `LogAssert.ignoreFailingMessages=false` でPASS。Error0とは報告しない。
- 起動準備時に判明したWebUI Node不足は環境担当が指定Node v20.18.1/pnpm9.15.0とfrozen-lockfile依存を配置して解消。環境ソース・lockfile・CEF・Library・pinの変更はコミットへ含めない。保存fixtureが破棄済みDIをstaticへ残していた問題はfixture所有の前値復元で解消し、製品bootは変更していない。
- 生出力: `C:/Users/5080/Documents/ChatGPT/tick-delta-refactor-20260926/task-3-compile-final.json`、`task-3-loaded-types-final.json`、`task-3-affected-final.xml`、`task-3-play-final.xml`、`task-3-play-final-editor.log`、`task-3-errors-final.json`。初回失敗を含む各iterationのJSON/XMLも同directoryへ保存した。


### 最終review修正の検証（2026-09-26、C9/C10裁定待ち時点）

- C7のfixture/helperを呼出し元のlocal functionへ移し、両Play testのbodyをiteratorへ展開した。Unityのdomain reloadで捕捉オブジェクトが失われないよう、local functionを含むbodyのスコープはEnterPlayMode後に開始する。C13のsnapshot待機とtick再開は各15秒の名前付きTimeSpanとStopwatchで期限を判定する。C14のfixture構築失敗はfinallyでproviderと一時rootを回収し、Play退出・SessionState・ignore flagはUnityTearDownで復元する。
- C8の追加受入はGeneratorのIBootInitializable.Loadで登録された実購読を維持し、乗車中のbranch選択入力によるtrain diff、node作成2件、connection作成/削除、node削除の計6件を同tickで発行する。全seqの連番・一意性と、逆順配送→VanillaApiEvent→実handler→buffer→各keyのrail/train cache変化・最終graph hashを確認した。既存TrainTestHelper.CreateEnvironmentの起動後Resetは4notifierを再生成するため使わず、GeneratorからTrainTestEnvironmentへ直接接続している。
- namespace移動と単一統合ID版TryFlushEventへの追従を含め、テスト変更は既存8 C#ファイル。最終影響回帰は **84/84 PASS、fail/skip 0**（12:14:22〜12:14:33 UTC）、実保存乗車起動・再同期は **1/1 PASS**（12:16:15〜12:16:34 UTC）。初回83/84のiterator捕捉失敗を修正後、両iteratorを現行assemblyで再実行した。
- Client.Tests MVID `e3f3f59e-e3a9-4129-8fdb-4682a90f00a9`、Client.Game MVID `2f4dc774-a469-4c7a-b312-818057cf8fbf`、Game.Train MVID `4b0fc77a-0763-49f3-9c56-b7eb628c608b`。新case・移動後namespace・TryAcceptReceivedTickUnifiedId・単一ID overloadをロード済みreflectionで確認。最初の変更compileは0 errors / 77 warnings、最後の変更なしcompileは0 errors / 0 warnings。既存警告の消滅という意味ではない。
- 最終live Errorは **3件**。独立Editor-log segmentの314行が未変更CEF validatorのmacOS metaパス例外（Play遷移直後）、4690/4707行が未変更UserPacketHandlerの終了時切断LogError/SocketException。assert区間は329〜4440行でignore=false、初期`4_1`と要求後`202_1`のsnapshot適用を含み、同期Errorなし。失敗したstartup初回もWebUiHost停止・backup scene復元・ignore=falseへの復帰と後続ケース継続を確認した。
- 生証拠は `C:/Users/5080/Documents/ChatGPT/tick-delta-refactor-20260926/final-fix-*` のcompile/loaded/affected-final/play-final XMLとEditor-log/errors-final JSON。外部の構築IOExceptionプローブは結果表示時にテスト外LogAssert getterが失敗したため、成功証拠に数えない。共用source無変更、read-lockはusingで解除、通常live Error件数とは分離した。C9/C10の挙動・期待値はこの修正では変更していない。


### 再reviewの機械修正と最終検証（round 2、2026-09-26）

- 製品commit `5dd44c4e310c328cf6dc655ce9c01c3e424d0d98` の `Game.Train.Unit.TickSynchronization` への3型移動へ、全test参照を検索しserverの `TrainTickSequencePacketTest` / `TrainFullSnapshotEventPacketTest` を追従した。規約guard指定のtestコメント2箇所を変更し、順序・失敗時cleanupの根拠は保持した。
- `TrainSnapshotStartupGateTest` のouter tryをfixture・GameObject・購読・static置換より前へ移動し、初期化途中のnullを許容して生成済みresourceを回収する。HUD終了時もfinallyからstaticを復元する。EnterPlayMode後にcaptureを生成するblockとUnityTearDownは維持した。追加失敗注入は行わず、この最終編集の成功経路を以下で検証した。
- 編集と整形の確定後に一巡実行。compile **0 errors / 77 warnings**、影響回帰 **84/84 PASS**（12:36:55〜12:37:06 UTC）、実保存乗車起動・再同期 **1/1 PASS**（12:37:51〜12:38:11 UTC）、fail/skip 0。Client.Tests MVID `5c5701d6-4cc7-495a-ac43-6496ac03c33a`、Client.Game `7f7ce535-242c-4d19-8c3c-4fb2917f01f0`、Game.Train `b0e5b94b-5f6b-43e0-9fe7-ddd5d6d6f253`。検証前後のreflectionで移動3型・旧namespace不在・単一ID flush・C8 caseを確認した。
- 最終liveのConsole Errorは **3件**。`final-fix-r2-play-editor.log` の313行がCEF遷移時例外、4710/4727行が終了時の既存socket切断2件。assert区間328〜4439行はignore=falseで、snapshot `4_1` と `203_1` の適用を含む。同期Errorなし、終了時ignore=false（4808行）、EditorPlaying=false / BootstrapDisabled=falseを確認。4524行のWebSocket受信終了診断もteardown開始後で、Console Error件数とは区別した。
- 生証拠は外部 `C:/Users/5080/Documents/ChatGPT/tick-delta-refactor-20260926/final-fix-r2-{compile.json,loaded-before.json,loaded-after.json,affected.xml,play.xml,play-editor.log,errors.json}`。CLI遷移切断応答も保存し、完了した実XMLを判定根拠とした。C9/C10は未回答のまま、今回も動作・期待値を変更していない。

### D2/A ユーザー裁定の適用（2026-09-26）

出所: ユーザー「全体更新の開始時に時計を進める」「これ採用します」。既存PRの追加裁定として以下を実施する。D1の初期snapshot＋差分・hash不一致時終了方針は確認中で、今回client/resyncを変更しない。

- [x] MasterTickUpdater入口でpreviousTickを保持し、時計を一度だけcurrentTickへ進める。hash(previousTick)はgear/fluid後の既存位置、BeginTick(currentTick)はtrain更新境界に保つ。
- [x] 実GearTickUpdaterの登録済み過負荷チェック経路から時計を観測し、gear→旧tick hash→新tick diffの順序・stream境界を検証する。既存空diff・hash cadence・初期0tick・packet/save回帰も実行する。
- [x] compile、ロード済みMVIDと新case実行、実XMLとErrorログを記録してscoped commitする。
- [ ] 既存CIの保存乗車テストで報告されたtree prefab起動ログを別コミットで調査・対処し、変更後DLLで保存乗車起動・snapshot再同期の実起動テストを再実行する（controller追加指示）。

D2サーバー検証: compile 0 errors / 46 warnings（既存source）、影響範囲37/37 PASS、fail/skip 0（2026-09-26 13:27:17〜13:27:25 UTC）。実gear経路から新tickを観測する新caseと、旧tick hash→新tick diff・空diff・初期0tick・乗車入力・保存復元・gear通知/過負荷・rail/gear/train replayを含む。Server.Boot MVID `aeda6519-63c1-4f9e-bc4f-97dc9b85089c`、Server.Tests `306fa55d-ce3e-4c7a-88f1-1019f3b41eba` を検証前後で確認。Errorログ0件。証跡は外部 `clock-phase-{compile.json,loaded-before.json,loaded-after.json,affected.xml,test-editor.log,errors-after.json}`。

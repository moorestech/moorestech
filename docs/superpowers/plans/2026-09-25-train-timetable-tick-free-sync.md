# 時刻表の tick 非同期化と停車駅の端指定 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** PR #1415 の時刻表・自動運転フラグを tick 同期の列車スナップショットから外して専用イベント＋問い合わせで UI へ配り、停車駅の端（先端側／後端側）をプロトコルで指定できるようにする。

**Architecture:** サーバーは `TrainTimetableSnapshot`（自動運転フラグ・現在の停車駅・停車駅の列〈座標＋端〉）を列車から作り、`ITrainTimetableNotifyEvent` 経由で tick 番号なしのブロードキャスト `va:event:trainTimetable` に流す。初期データは `va:getTrainTimetable`。クライアントは `ClientTrainTimetableDatastore` に保持し、Web UI の時刻表タブはそこだけを読む。`TrainSimulationSnapshot`・`ClientTrainUnit` から時刻表関連を完全に取り除く。

**Tech Stack:** Unity C#（サーバー/クライアント）、MessagePack、UniRx、VContainer、React + zod（webui）

## Requirements

- R1: `TrainSimulationSnapshot`／`TrainSimulationSnapshotMessagePack`／`ClientTrainUnit` に時刻表・自動運転フラグの項目が無い。受入: 型から `IsAutoRun`/`TimetableCurrentIndex`/`TimetableStops` が消え、コンパイルが通る。
- R2: 時刻表の現在地が進んでも、自動運転が切り替わっても、時刻表を編集しても `ITrainUnitSnapshotNotifyEvent` は発火しない（列車の走行同期が時刻表の都合で送られない）。受入: サーバーテストで「到着→次の駅へ進む」「自動運転OFF」「置換」「SetAutoRun」の各ケースで snapshot 通知 0 回。
- R3: 上記の各変化で `va:event:trainTimetable` が1回ずつブロードキャストされ、tick 番号を持たない。受入: 通知回数テスト（1回・次tickで重複なし）＋イベントペイロードの往復テスト。
- R4: `va:getTrainTimetable` が指定列車の時刻表（自動運転フラグ・現在の停車駅・停車駅の座標と端）を返し、列車が無ければ `Found=false` を返してサーバーログに理由を出す。受入: パケットテスト。
- R5: `va:trainScheduleEdit` の置換は停車駅ごとに座標と端（Front/Back）を受け、端に対応する Exit ノードを登録する。受入: Front 指定で Front側 Exit、Back 指定で Back側 Exit が登録されるテスト。駅でない・端不正は理由付きで拒否。
- R6: Front側 Exit を目的地にした列車も到着・ドッキングする（両端が駅ノードとして認識される）。受入: 既存 `TrainAutoRunTestScenario`（StationExitFront を使う）で到着・停車が通るテストを Front／Back 両方で。
- R7: 時刻表タブは開くと `va:getTrainTimetable` で取得し、開いている間は `va:event:trainTimetable` で自動運転表示・現在駅ハイライト・停車駅が更新される。受入: クライアントのデータストア通知テスト＋DtoBuilderテスト。
- R8: Web UI は新しく追加する停車駅を常に Back で送り、既存の停車駅は届いた端のまま送り返す。UI に端（向き）の選択は作らない。受入: vitest（追加は back・既存 front は front のまま payload に載る）。
- R9: セーブ形式は変えない。受入: `TrainDiagramSaveDataConverter` と `WorldSaveAllInfo.CurrentVersion` に差分なし。
- やらないこと: UI での向き選択、出発条件の追加、到達不能理由の UI 表示、D1〜D8（[[2026-09-24-時刻表UIの最終レビューでは既存Trainコードへの改修を持ち込まない]]）。

## Global Constraints

- 1ファイル200行未満。新規ファイルを置くディレクトリは10ファイル以下（超えるならサブディレクトリ）。`Server.Event/EventReceive/` と `Client.Game/InGame/Train/Network/` は既に10超なので新規ファイルはサブディレクトリ `Train/`・`Timetable/` へ置く。
- partial・`Func<>`・デフォルト引数・単純 getter/setter プロパティ禁止（MessagePack の `[Key]` プロパティは既存前例どおり可）。
- イベントは UniRx（`Subject<T>`／`IObservable<T>`）。
- 主要処理に日本語1行＋英語1行のコメント。
- fail-closed の拒否は理由をログへ出す（無音禁止）。
- .meta は手で作らない。.cs 変更後は `uloop compile --project-path ./moorestech_client`。
- テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<regex>"`（EditMode 既定）。
- 作業場所: `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/train-timetable-fix`（ブランチ `feature/train-timetable-ui`）。区切りごとにコミットする。

## File Structure

| ファイル | 責務 | 既存部品との関係 |
|---|---|---|
| `moorestech_server/Assets/Scripts/Game.Train/Unit/Timetable/TrainTimetableSnapshot.cs`（新） | `TrainTimetableStop`（座標＋端）と `TrainTimetableSnapshot` と、列車から作る `TrainTimetableSnapshotFactory` | 新規（`TrainUnitSnapshotFactory` の停車駅抽出ロジックをここへ移す） |
| `moorestech_server/Assets/Scripts/Game.Train/Event/ITrainTimetableNotifyEvent.cs`・`TrainTimetableNotifyEvent.cs`（新） | 時刻表変化の通知窓口 | `ITrainUnitSnapshotNotifyEvent` と同型 |
| `moorestech_server/Assets/Scripts/Game.Block/Blocks/TrainRail/TrainTimetableStationNodeResolver.cs`（改） | 駅ブロック＋端 → Exit ノード | 既存を拡張 |
| `moorestech_server/Assets/Scripts/Game.Train/Unit/TrainUpdateService.cs`（改） | tick 後の時刻表通知を timetable 通知へ付け替え | 既存 |
| `moorestech_server/Assets/Scripts/Server.Util/MessagePack/Train/TrainTimetableStopMessagePack.cs`（改）・`TrainTimetableMessagePack.cs`（新） | 通信形 | 既存 stop を端付きに |
| `moorestech_server/Assets/Scripts/Server.Event/EventReceive/Train/TrainTimetableEventPacket.cs`（新） | `va:event:trainTimetable` 配信 | `ItemStackLevelUnlockEventPacket` と同型 |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/TrainSchedule/GetTrainTimetableProtocol.cs`（新） | `va:getTrainTimetable` | `GetGameUnlockStateProtocol` と同役割 |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/TrainSchedule/TrainScheduleEditProtocol.cs`（改） | 停車駅＝座標＋端、通知先を timetable へ | 既存 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Timetable/ClientTrainTimetableDatastore.cs`・`TrainTimetableEventHandler.cs`（新） | クライアント保持と購読 | `ClientGameUnlockStateDatastore`＋`ItemStackLevelEventHandler` と同型 |
| `moorestech_client/Assets/Scripts/Client.Network/API/ResponseApi/TrainResponseApi.cs`（改） | `GetTrainTimetable` 送信口 | 既存 |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/...`（改） | DTO をデータストアから作る・開いたら取得・停車駅に端 | 既存 |
| `moorestech_web/webui/src/...`（改） | 停車駅に `side`、追加は back 固定 | 既存 |

### 共有状態の保持者と最新化（Self-Review 7）

| 事実 | 保持者 | 書き換え操作 → 最新化経路 |
|---|---|---|
| 列車の時刻表・自動運転・現在駅 | サーバー `TrainUnit`（正）／クライアント `ClientTrainTimetableDatastore`／webui の `timetable` prop | 置換・SetAutoRun（プロトコル）→ 同プロトコル内で `NotifyTimetableChanged` → イベント → Datastore.Apply → `OnTimetableUpdated` → BlockInventoryTopic 再配信（R3・R7）。到着で前進・自動OFF（tick 内）→ `TrainUpdateService` が通知（R3）。タブを開いた時 → `va:getTrainTimetable` の応答を Datastore.Apply（R7） |
| 駅名 | 駅ブロック状態 | 既存どおりブロック状態イベント（変更なし） |

## Tasks

### Task 1: サーバーの時刻表スナップショットと通知窓口、端付きノード解決

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Train/Unit/Timetable/TrainTimetableSnapshot.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Train/Event/ITrainTimetableNotifyEvent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Train/Event/TrainTimetableNotifyEvent.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs:231`（`ITrainUnitSnapshotNotifyEvent` 登録の直後に `services.AddSingleton<ITrainTimetableNotifyEvent, TrainTimetableNotifyEvent>();`）
- Modify: `moorestech_server/Assets/Scripts/Game.Train/Unit/TrainUpdateService.cs`（`NotifyTimetableAdvanced` の通知先）
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/TrainRail/TrainTimetableStationNodeResolver.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/TrainSchedule/TrainScheduleEditProtocol.cs`（この Task では `TryResolve(block, StationNodeSide.Back, out node)` に呼び替えるだけ）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainTimetable/TrainTimetableNotifyTest.cs`（`TrainTimetableSnapshotNotifyTest.cs` を git mv して書き換え）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainTimetable/TrainTimetableSnapshotFactoryTest.cs`（新）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainTimetable/TrainTimetableStationNodeResolverTest.cs`

**Interfaces:**
- Produces:
  - `Game.Train.Unit.TrainTimetableStop`（readonly struct: `Vector3Int StationPosition`, `StationNodeSide Side`）
  - `Game.Train.Unit.TrainTimetableSnapshot`（readonly struct: `TrainUnitInstanceId TrainUnitInstanceId`, `bool IsAutoRun`, `int CurrentIndex`, `IReadOnlyList<TrainTimetableStop> Stops`）
  - `Game.Train.Unit.TrainTimetableSnapshotFactory.Create(TrainUnit train) : TrainTimetableSnapshot`
  - `Game.Train.Event.ITrainTimetableNotifyEvent { IObservable<TrainUnit> OnTimetableChanged; void NotifyTimetableChanged(TrainUnit trainUnit); }`
  - `TrainTimetableStationNodeResolver.TryResolve(IBlock block, StationNodeSide side, out IRailNode node) : bool`

- [ ] **Step 1: 失敗するテストを書く**

`TrainTimetableSnapshotFactoryTest.cs`:

```csharp
using System.Collections.Generic;
using Game.Train.RailGraph;
using Game.Train.Unit;
using NUnit.Framework;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game.TrainTimetable
{
    public class TrainTimetableSnapshotFactoryTest
    {
        [Test]
        public void CarriesAutoRunCursorAndStopSide()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            scenario.Train.trainDiagram.ReplaceEntries(new List<IRailNode> { scenario.StationExitFront });

            var snapshot = TrainTimetableSnapshotFactory.Create(scenario.Train);

            Assert.AreEqual(scenario.Train.TrainUnitInstanceId, snapshot.TrainUnitInstanceId);
            Assert.IsTrue(snapshot.IsAutoRun);
            Assert.AreEqual(0, snapshot.CurrentIndex);
            Assert.AreEqual(1, snapshot.Stops.Count);
            Assert.AreEqual(Vector3Int.zero, snapshot.Stops[0].StationPosition);
            Assert.AreEqual(StationNodeSide.Front, snapshot.Stops[0].Side);
        }

        [Test]
        public void EmptyTimetableHasNoCursor()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            scenario.Train.trainDiagram.ReplaceEntries(new List<IRailNode>());

            var snapshot = TrainTimetableSnapshotFactory.Create(scenario.Train);

            Assert.AreEqual(-1, snapshot.CurrentIndex);
            Assert.IsEmpty(snapshot.Stops);
        }

        [Test]
        public void NonStationEntryDoesNotHighlightAnotherStation()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            scenario.Train.trainDiagram.MoveToNextEntry();

            var snapshot = TrainTimetableSnapshotFactory.Create(scenario.Train);

            Assert.AreEqual(-1, snapshot.CurrentIndex);
            Assert.AreEqual(1, snapshot.Stops.Count);
        }
    }
}
```

`TrainTimetableStationNodeResolverTest.cs` の `ResolvesBackExitNodeOfTrainStation` を次の2テストに置き換え、他2テストは `TryResolve(block, StationNodeSide.Back, out var node)` に呼び替える:

```csharp
        [TestCase(StationNodeSide.Front)]
        [TestCase(StationNodeSide.Back)]
        public void ResolvesExitNodeOfRequestedSide(StationNodeSide side)
        {
            var env = TrainTestHelper.CreateEnvironment();
            var (block, _) = TrainTestHelper.PlaceBlockWithRailComponents(
                env, ForUnitTestModBlockId.TestTrainStation, Vector3Int.zero, BlockDirection.North);

            Assert.IsTrue(TrainTimetableStationNodeResolver.TryResolve(block, side, out var node));
            Assert.AreEqual(side, node.StationRef.NodeSide);
            Assert.AreEqual(StationNodeRole.Exit, node.StationRef.NodeRole);
            Assert.AreSame(block, node.StationRef.StationBlock);
        }
```

`TrainTimetableNotifyTest.cs`（旧 `TrainTimetableSnapshotNotifyTest.cs` を `git mv` し、クラス名も変える）: 4テストとも購読先を `ITrainTimetableNotifyEvent.OnTimetableChanged` に替え（`data.TrainUnitInstanceId` → `train.TrainUnitInstanceId == trainUnit.TrainUnitInstanceId`、`data.TrainUnit.IsAutoRun` → `trainUnit.IsAutoRun`、`IsDeleted` 判定は削除）、さらに各テストで同時に `ITrainUnitSnapshotNotifyEvent.OnTrainUnitSnapshotNotified` も購読して **対象列車の snapshot 通知が0回** であることを assert する（R2）。例（`SnapshotIsNotifiedOnceAfterCurrentEntryAdvances` を `TimetableIsNotifiedOnceAfterCurrentEntryAdvances` に改名）:

```csharp
            var timetableNotify = ServerContext.GetService<ITrainTimetableNotifyEvent>();
            var snapshotNotify = ServerContext.GetService<ITrainUnitSnapshotNotifyEvent>();
            var snapshotCount = 0;
            using var snapshotSubscription = snapshotNotify.OnTrainUnitSnapshotNotified.Subscribe(data =>
            {
                if (data.TrainUnitInstanceId == train.TrainUnitInstanceId) snapshotCount++;
            });
            using var subscription = timetableNotify.OnTimetableChanged.Subscribe(trainUnit =>
            {
                if (trainUnit.TrainUnitInstanceId != train.TrainUnitInstanceId) return;
                notificationCount++;
                notificationTick = updateService.GetCurrentTick();
            });
            // ...既存のループと assert...
            Assert.AreEqual(0, snapshotCount, "時刻表の前進で列車の走行同期を送らない");
```

- [ ] **Step 2: 失敗を確認する** — Run: `uloop compile --project-path ./moorestech_client` → Expected: `TrainTimetableSnapshotFactory`・`ITrainTimetableNotifyEvent`・`TryResolve(IBlock, StationNodeSide, ...)` 未定義のコンパイルエラー。

- [ ] **Step 3: 実装する**

`TrainTimetableSnapshot.cs`:

```csharp
using System.Collections.Generic;
using Game.Train.RailGraph;
using UnityEngine;

namespace Game.Train.Unit
{
    // 停車駅の駅ブロック原点と入線する端
    // A stop's station block origin and the side the train arrives at
    public readonly struct TrainTimetableStop
    {
        public TrainTimetableStop(Vector3Int stationPosition, StationNodeSide side)
        {
            StationPosition = stationPosition;
            Side = side;
        }

        public Vector3Int StationPosition { get; }
        public StationNodeSide Side { get; }
    }

    // UIへ渡す列車1編成の時刻表と自動運転状態（tick同期しない）
    // One train's timetable and auto-run state for the UI (not tick-synchronized)
    public readonly struct TrainTimetableSnapshot
    {
        public TrainTimetableSnapshot(TrainUnitInstanceId trainUnitInstanceId, bool isAutoRun, int currentIndex, IReadOnlyList<TrainTimetableStop> stops)
        {
            TrainUnitInstanceId = trainUnitInstanceId;
            IsAutoRun = isAutoRun;
            CurrentIndex = currentIndex;
            Stops = stops;
        }

        public TrainUnitInstanceId TrainUnitInstanceId { get; }
        public bool IsAutoRun { get; }
        public int CurrentIndex { get; }
        public IReadOnlyList<TrainTimetableStop> Stops { get; }
    }

    public static class TrainTimetableSnapshotFactory
    {
        public static TrainTimetableSnapshot Create(TrainUnit train)
        {
            // 駅参照を持つ停車駅だけを座標と端で送る
            // Send only stops with a station reference, as position and side
            var entries = train.trainDiagram.Entries;
            var stops = new List<TrainTimetableStop>(entries.Count);
            var currentStopIndex = -1;
            for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                var station = entries[entryIndex].Node.StationRef;
                if (station == null || !station.HasStation) continue;
                if (entryIndex == train.trainDiagram.CurrentIndex) currentStopIndex = stops.Count;
                stops.Add(new TrainTimetableStop(station.StationPosition, station.NodeSide));
            }
            return new TrainTimetableSnapshot(train.TrainUnitInstanceId, train.IsAutoRun, currentStopIndex, stops);
        }
    }
}
```

`ITrainTimetableNotifyEvent.cs`:

```csharp
using System;
using Game.Train.Unit;

namespace Game.Train.Event
{
    // 時刻表・自動運転状態の変化をUI向けに知らせる窓口（tick同期しない）
    // Gateway notifying timetable and auto-run changes to the UI (not tick-synchronized)
    public interface ITrainTimetableNotifyEvent
    {
        IObservable<TrainUnit> OnTimetableChanged { get; }
        void NotifyTimetableChanged(TrainUnit trainUnit);
    }
}
```

`TrainTimetableNotifyEvent.cs`:

```csharp
using System;
using Game.Train.Unit;
using UniRx;

namespace Game.Train.Event
{
    public sealed class TrainTimetableNotifyEvent : ITrainTimetableNotifyEvent
    {
        private readonly Subject<TrainUnit> _subject = new();
        public IObservable<TrainUnit> OnTimetableChanged => _subject;

        public void NotifyTimetableChanged(TrainUnit trainUnit)
        {
            // 未登録の列車は配信先が無いので理由を残して捨てる
            // Drop unregistered trains with a logged reason since no client can address them
            if (trainUnit.TrainUnitInstanceId == TrainUnitInstanceId.Empty)
            {
                UnityEngine.Debug.LogWarning("[TrainTimetableNotify] ignored: train has no instance id");
                return;
            }
            _subject.OnNext(trainUnit);
        }
    }
}
```

`TrainUpdateService.NotifyTimetableAdvanced` の本体を次に置き換える（`ITrainUnitSnapshotNotifyEvent` の取得と `NotifySnapshot` 呼び出しを消す）:

```csharp
            void NotifyTimetableAdvanced()
            {
                var notify = ServerContext.GetService<ITrainTimetableNotifyEvent>();
                foreach (var trainUnit in _trainUnitLookupDatastore.GetRegisteredTrains())
                {
                    var entryChanged = trainUnit.trainDiagram.ConsumeCurrentEntryChanged();
                    var autoRunChanged = trainUnit.ConsumeAutoRunChanged();
                    if (!entryChanged && !autoRunChanged) continue;
                    notify.NotifyTimetableChanged(trainUnit);
                }
            }
```

コメントも「時刻表の現在地が進んだ列車をシミュレーション後に同期する」→「時刻表・自動運転が変わった列車をUIへ知らせる（走行同期とは別経路）」/ "Notify the UI of trains whose timetable or auto-run changed (separate from motion sync)" に直す。

`TrainTimetableStationNodeResolver.TryResolve` を端付きにする:

```csharp
        public static bool TryResolve(IBlock block, StationNodeSide side, out IRailNode node)
        {
            node = null;
            if (block == null) return false;
            if (block.BlockMasterElement.BlockType != BlockTypeConst.TrainStation) return false;

            // 駅のレールから指定した端のExitノードを探す
            // Find the exit node on the requested side among the station rails
            foreach (var rail in block.GetComponents<RailComponent>())
            {
                if (IsExitOnSide(rail.BackNode)) { node = rail.BackNode; return true; }
                if (IsExitOnSide(rail.FrontNode)) { node = rail.FrontNode; return true; }
            }

            return false;

            #region Internal

            bool IsExitOnSide(RailNode candidate)
            {
                if (candidate == null || candidate.StationRef == null) return false;
                return candidate.StationRef.NodeSide == side && candidate.StationRef.NodeRole == StationNodeRole.Exit;
            }

            #endregion
        }
```

`TrainScheduleEditProtocol` の呼び出しは `TryResolve(block, StationNodeSide.Back, out var node)`（`using Game.Train.RailGraph;` は既存）。

- [ ] **Step 4: 通ることを確認する** — Run: `uloop compile --project-path ./moorestech_client` → ErrorCount 0。Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainTimetable|TrainSchedule|TrainAutoRun"` → 全PASS（旧 `TrainTimetableSnapshotTest` もこの時点ではまだ通る）。

- [ ] **Step 5: コミット** — `git add -A moorestech_server && git commit -m "feat(train): 時刻表の通知をtick同期のsnapshotから専用窓口へ分け、駅の両端を解決できるようにする"`

### Task 2: サーバーの時刻表イベント・問い合わせ・端付き置換プロトコル

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Util/MessagePack/Train/TrainTimetableStopMessagePack.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Util/MessagePack/Train/TrainTimetableMessagePack.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Event/EventReceive/Train/TrainTimetableEventPacket.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`（`services.AddSingleton<RidingStateEventPacket>();` の後に `services.AddSingleton<TrainTimetableEventPacket>();`。IBootInitializable への転送登録は同ファイル331行付近の既存機構が拾うか確認し、拾わない場合は同じ形で追加）
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/TrainSchedule/GetTrainTimetableProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs:72`（`GetTrainTimetableProtocol` 登録）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/TrainSchedule/TrainScheduleEditProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/TrainSchedule/TrainScheduleEditTypes.cs`（`InvalidStationSide` を追加）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/TrainSchedule/TrainScheduleEditProtocolTest.cs`・`TrainScheduleMalformedRequestTest.cs`・`TrainScheduleProtocolTestEnvironment.cs`（改）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/TrainSchedule/GetTrainTimetableProtocolTest.cs`（新）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/TrainSchedule/TrainTimetableEventPacketTest.cs`（新）

**Interfaces:**
- Consumes: Task 1 の `TrainTimetableSnapshot`・`TrainTimetableSnapshotFactory.Create`・`ITrainTimetableNotifyEvent`・`TryResolve(block, side, out node)`
- Produces:
  - `Server.Util.MessagePack.TrainTimetableStopMessagePack`（`[Key(0)] Vector3IntMessagePack StationPosition`, `[Key(1)] StationNodeSide Side`, ctor `(TrainTimetableStop)`, ctor `(Vector3Int, StationNodeSide)`, `ToModel()`）
  - `Server.Util.MessagePack.TrainTimetableMessagePack`（`[Key(0)] TrainUnitInstanceId`, `[Key(1)] bool IsAutoRun`, `[Key(2)] int CurrentIndex`, `[Key(3)] List<TrainTimetableStopMessagePack> Stops`, ctor `(TrainTimetableSnapshot)`）
  - `TrainTimetableEventPacket.EventTag = "va:event:trainTimetable"`（ペイロード＝`TrainTimetableMessagePack`）
  - `GetTrainTimetableProtocol.ProtocolTag = "va:getTrainTimetable"`、`GetTrainTimetableRequest(TrainUnitInstanceId)`、`GetTrainTimetableResponse { bool Found; TrainTimetableMessagePack Timetable; }`
  - `TrainScheduleEditRequest.CreateReplaceTimetableRequest(TrainUnitInstanceId, IReadOnlyList<TrainTimetableStop> stops)`（座標だけの旧シグネチャは削除）。リクエストの `[Key(4)]` は `List<TrainTimetableStopMessagePack> Stops`

- [ ] **Step 1: 失敗するテストを書く**

`TrainScheduleProtocolTestEnvironment` の `Notifications` を `ITrainTimetableNotifyEvent TimetableNotifications` と `ITrainUnitSnapshotNotifyEvent SnapshotNotifications` の2つにする。既存の `CreateReplaceTimetableRequest(id, positions)` 呼び出しはすべて `positions.Select(p => new TrainTimetableStop(p, StationNodeSide.Back)).ToArray()` を渡す形にする。既存の「自動運転OFFを snapshot で観測する」テスト（106行付近）は `TimetableNotifications.OnTimetableChanged` の購読へ替え、同時に `SnapshotNotifications` の対象列車通知が0回であることを assert（R2）。次を追加:

```csharp
        [TestCase(StationNodeSide.Front)]
        [TestCase(StationNodeSide.Back)]
        public void ReplaceRegistersExitNodeOfRequestedSide(StationNodeSide side)
        {
            var fixture = new TrainScheduleProtocolTestEnvironment();
            var station = fixture.PlaceStation(new Vector3Int(0, 0, 40));
            var response = fixture.Send(Request.CreateReplaceTimetableRequest(fixture.Train.TrainUnitInstanceId,
                new[] { new TrainTimetableStop(station.BlockPositionInfo.OriginalPos, side) }));

            Assert.IsTrue(response.Success);
            var node = fixture.Train.trainDiagram.Entries[0].Node;
            Assert.AreEqual(side, node.StationRef.NodeSide);
            Assert.AreEqual(StationNodeRole.Exit, node.StationRef.NodeRole);
        }
```

`TrainScheduleMalformedRequestTest` に「`Stops` に `Side = (StationNodeSide)99` を入れたら `InvalidStationSide` で拒否」を追加（リクエストの `Stops` を直接書き換えて送る）。

`GetTrainTimetableProtocolTest.cs`:

```csharp
using System.Linq;
using Game.Train.RailGraph;
using Game.Train.Unit;
using MessagePack;
using NUnit.Framework;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class GetTrainTimetableProtocolTest
    {
        [Test]
        public void ReturnsTimetableWithSides()
        {
            var fixture = new TrainScheduleProtocolTestEnvironment();
            var station = fixture.PlaceStation(new Vector3Int(0, 0, 40));
            var position = station.BlockPositionInfo.OriginalPos;
            Assert.IsTrue(fixture.Send(TrainScheduleEditProtocol.TrainScheduleEditRequest.CreateReplaceTimetableRequest(
                fixture.Train.TrainUnitInstanceId, new[] { new TrainTimetableStop(position, StationNodeSide.Front) })).Success);

            var response = Get(fixture, fixture.Train.TrainUnitInstanceId);

            Assert.IsTrue(response.Found);
            Assert.AreEqual(fixture.Train.IsAutoRun, response.Timetable.IsAutoRun);
            Assert.AreEqual(0, response.Timetable.CurrentIndex);
            Assert.AreEqual(position, response.Timetable.Stops.Single().StationPosition.Vector3Int);
            Assert.AreEqual(StationNodeSide.Front, response.Timetable.Stops.Single().Side);
        }

        [Test]
        public void UnknownTrainReturnsNotFound()
        {
            var fixture = new TrainScheduleProtocolTestEnvironment();
            var response = Get(fixture, TrainUnitInstanceId.Create());
            Assert.IsFalse(response.Found);
            Assert.IsNull(response.Timetable);
        }

        private static GetTrainTimetableProtocol.GetTrainTimetableResponse Get(TrainScheduleProtocolTestEnvironment fixture, TrainUnitInstanceId id)
        {
            var payload = MessagePackSerializer.Serialize(new GetTrainTimetableProtocol.GetTrainTimetableRequest(id));
            var responses = fixture.Environment.PacketResponseCreator.GetPacketResponse(payload, new PacketResponseContext(null));
            return MessagePackSerializer.Deserialize<GetTrainTimetableProtocol.GetTrainTimetableResponse>(responses[0]);
        }
    }
}
```

（`TrainUnitInstanceId.Create()` が無ければ既存テストで未登録IDを作っている方法を Grep して合わせる。）

`TrainTimetableEventPacketTest.cs`: 置換を送った後、`EventProtocolProvider` から `TrainTimetableEventPacket.EventTag` のブロードキャストを1件取り出し `TrainTimetableMessagePack` へデシリアライズして列車ID・停車駅の端を照合する。取り出し方は既存 `Tests/CombinedTest/Server/PacketTest/Event/` 配下の他イベントテスト（例: `UnlockedEventPacket` を検証しているテスト）を Grep してその手順を呼ぶ。加えて `va:event:trainUnitSnapshot` のブロードキャストがこの置換で増えないことを assert（R2）。

- [ ] **Step 2: 失敗を確認** — `uloop compile` → 未定義エラー。

- [ ] **Step 3: 実装する**

`TrainTimetableStopMessagePack.cs`（置き換え）:

```csharp
using System;
using Game.Train.RailGraph;
using Game.Train.Unit;
using MessagePack;
using UnityEngine;

namespace Server.Util.MessagePack
{
    [MessagePackObject]
    public class TrainTimetableStopMessagePack
    {
        [Key(0)] public Vector3IntMessagePack StationPosition { get; set; }
        [Key(1)] public StationNodeSide Side { get; set; }

        [Obsolete("Reserved for MessagePack serialization.")]
        public TrainTimetableStopMessagePack() { }

        public TrainTimetableStopMessagePack(TrainTimetableStop stop) : this(stop.StationPosition, stop.Side) { }

        public TrainTimetableStopMessagePack(Vector3Int stationPosition, StationNodeSide side)
        {
            StationPosition = new Vector3IntMessagePack(stationPosition);
            Side = side;
        }

        public TrainTimetableStop ToModel()
        {
            return new TrainTimetableStop(StationPosition.Vector3Int, Side);
        }
    }
}
```

`TrainTimetableMessagePack.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Train.Unit;
using MessagePack;

namespace Server.Util.MessagePack
{
    // UI向け時刻表の通信形。tick番号は持たない
    // Wire form of the UI-facing timetable; carries no tick number
    [MessagePackObject]
    public class TrainTimetableMessagePack
    {
        [Key(0)] public TrainUnitInstanceId TrainUnitInstanceId { get; set; }
        [Key(1)] public bool IsAutoRun { get; set; }
        [Key(2)] public int CurrentIndex { get; set; }
        [Key(3)] public List<TrainTimetableStopMessagePack> Stops { get; set; }

        [Obsolete("Reserved for MessagePack serialization.")]
        public TrainTimetableMessagePack() { }

        public TrainTimetableMessagePack(TrainTimetableSnapshot snapshot)
        {
            TrainUnitInstanceId = snapshot.TrainUnitInstanceId;
            IsAutoRun = snapshot.IsAutoRun;
            CurrentIndex = snapshot.CurrentIndex;
            Stops = snapshot.Stops.Select(stop => new TrainTimetableStopMessagePack(stop)).ToList();
        }
    }
}
```

`TrainTimetableEventPacket.cs`:

```csharp
using Game.Train.Event;
using Game.Train.Unit;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    // 時刻表・自動運転の変化をtick番号なしで全員へ配る
    // Broadcast timetable and auto-run changes to everyone without a tick number
    public class TrainTimetableEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:trainTimetable";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly ITrainTimetableNotifyEvent _timetableNotifyEvent;

        public TrainTimetableEventPacket(EventProtocolProvider eventProtocolProvider, ITrainTimetableNotifyEvent timetableNotifyEvent)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _timetableNotifyEvent = timetableNotifyEvent;
        }

        public void Load()
        {
            _timetableNotifyEvent.OnTimetableChanged.Subscribe(trainUnit =>
            {
                var message = new TrainTimetableMessagePack(TrainTimetableSnapshotFactory.Create(trainUnit));
                _eventProtocolProvider.AddBroadcastEvent(EventTag, MessagePackSerializer.Serialize(message));
            });
        }
    }
}
```

`GetTrainTimetableProtocol.cs`（`ProtocolMessagePackBase`・`Tag` の書き方は同ディレクトリの `TrainScheduleEditProtocol` を踏襲）:

```csharp
using System;
using Game.Train.Unit;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    // 時刻表タブを開いたときの初期データを返す
    // Return the initial data when the timetable tab opens
    public class GetTrainTimetableProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getTrainTimetable";
        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;

        public GetTrainTimetableProtocol(ServiceProvider serviceProvider)
        {
            _trainUnitLookupDatastore = serviceProvider.GetService<ITrainUnitLookupDatastore>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var request = MessagePackSerializer.Deserialize<GetTrainTimetableRequest>(payload);
            if (!_trainUnitLookupDatastore.TryGetTrainUnit(request.TrainUnitInstanceId, out var train))
            {
                Debug.LogWarning($"[GetTrainTimetable] train not found: {request.TrainUnitInstanceId}");
                return new GetTrainTimetableResponse(false, null);
            }
            return new GetTrainTimetableResponse(true, new TrainTimetableMessagePack(TrainTimetableSnapshotFactory.Create(train)));
        }

        [MessagePackObject]
        public class GetTrainTimetableRequest : ProtocolMessagePackBase
        {
            [Key(2)] public TrainUnitInstanceId TrainUnitInstanceId { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public GetTrainTimetableRequest() { Tag = ProtocolTag; }

            public GetTrainTimetableRequest(TrainUnitInstanceId trainUnitInstanceId)
            {
                Tag = ProtocolTag;
                TrainUnitInstanceId = trainUnitInstanceId;
            }
        }

        [MessagePackObject]
        public class GetTrainTimetableResponse : ProtocolMessagePackBase
        {
            [Key(2)] public bool Found { get; set; }
            [Key(3)] public TrainTimetableMessagePack Timetable { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public GetTrainTimetableResponse() { Tag = ProtocolTag; }

            public GetTrainTimetableResponse(bool found, TrainTimetableMessagePack timetable)
            {
                Tag = ProtocolTag;
                Found = found;
                Timetable = timetable;
            }
        }
    }
}
```

`TrainScheduleEditProtocol` の変更:
- フィールド `ITrainUnitSnapshotNotifyEvent _snapshotNotifyEvent` → `ITrainTimetableNotifyEvent _timetableNotifyEvent`（`GetService<ITrainTimetableNotifyEvent>()`）。`ReplaceTimetable`／`SetAutoRun` の末尾の `_snapshotNotifyEvent.NotifySnapshot(trainUnit)` を `_timetableNotifyEvent.NotifyTimetableChanged(trainUnit)` に替える（直前の `Consume*` 2行は残す＝同tickの重複通知防止）。
- リクエスト `[Key(4)]` を `List<TrainTimetableStopMessagePack> Stops` に、private ctor・`CreateReplaceTimetableRequest(TrainUnitInstanceId, IReadOnlyList<TrainTimetableStop> stops)`・`CreateSetAutoRunRequest`（空リスト）を合わせる。
- `ReplaceTimetable` の検証ループ: `Stops == null || Stops.Contains(null)` → `InvalidRequest`。各 stop で `stop.StationPosition == null` → `InvalidRequest`。`!Enum.IsDefined(typeof(StationNodeSide), stop.Side)` → `Reject(..., InvalidStationSide, $"pos=... side={(int)stop.Side}")`。ノード解決は `TryResolve(block, stop.Side, out var node)`。

- [ ] **Step 4: 通ることを確認** — `uloop compile` ErrorCount 0 → `uloop run-tests ... --filter-value "TrainSchedule|GetTrainTimetable|TrainTimetable"` 全PASS。

- [ ] **Step 5: コミット** — `git commit -m "feat(protocol): va:event:trainTimetable と va:getTrainTimetable を追加し、停車駅を端付きで受ける"`

### Task 3: クライアントの時刻表データストアと Web UI ホストの付け替え

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Timetable/ClientTrainTimetableDatastore.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Timetable/TrainTimetableEventHandler.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs:80`付近（`builder.Register<ClientTrainTimetableDatastore>(Lifetime.Singleton); builder.RegisterEntryPoint<TrainTimetableEventHandler>();`）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/ResponseApi/TrainResponseApi.cs`（`GetTrainTimetable` 追加）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/Train/TrainTimetableDtos.cs`（停車駅 DTO に `Side`）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/Train/TrainTimetableDtoBuilder.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/TrainInventoryDtoFactory.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockInventoryTopic.cs`
- Rename+Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Train/TrainStationPositionParser.cs` → `TrainTimetableStopParser.cs`（座標に加え `side`）。`TrainStationActions.cs` が座標パーサーを使っていれば座標部分は `TrainStationPositionParser` として残し、stop 用は別メソッドにする
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Train/TrainTimetableActions.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUiHost/Train/TrainTimetableDatastoreNotificationTest.cs`（`TrainTimetableSnapshotNotificationTest.cs` を git mv して書き換え）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUiHost/Train/TrainTimetableDtoBuilderTest.cs`・`TrainStationPositionParserTest.cs`（改）

**Interfaces:**
- Consumes: Task 2 の `TrainTimetableMessagePack`・`TrainTimetableEventPacket.EventTag`・`GetTrainTimetableProtocol`
- Produces:
  - `Client.Game.InGame.Train.Timetable.ClientTrainTimetableDatastore { void Apply(TrainTimetableMessagePack message); bool TryGet(TrainUnitInstanceId id, out TrainTimetableMessagePack message); IObservable<TrainUnitInstanceId> OnTimetableUpdated; }`
  - `TrainResponseApi.GetTrainTimetable(this VanillaApiWithResponse api, TrainUnitInstanceId id, CancellationToken ct) : UniTask<GetTrainTimetableProtocol.GetTrainTimetableResponse>`
  - webui 向け停車駅 DTO: `{ position:{x,y,z}, name, side: "front"|"back" }`。駅一覧（`stations`）は side なし
  - アクション `train_timetable.replace` の payload: `{ stops: { x, y, z, side: "front"|"back" }[] }`

- [ ] **Step 1: 失敗するテストを書く**

`TrainTimetableDatastoreNotificationTest.cs`（git mv 後に中身を置き換え）:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.Train.Timetable;
using Game.Train.RailGraph;
using Game.Train.Unit;
using NUnit.Framework;
using Server.Util.MessagePack;
using UniRx;
using UnityEngine;

namespace Client.Tests.WebUiHost.Train
{
    public class TrainTimetableDatastoreNotificationTest
    {
        [Test]
        public void ApplyStoresTimetableAndNotifiesTrainId()
        {
            var datastore = new ClientTrainTimetableDatastore();
            var id = TrainUnitInstanceId.Create();
            var notified = new List<TrainUnitInstanceId>();
            using var subscription = datastore.OnTimetableUpdated.Subscribe(notified.Add);

            datastore.Apply(Message(id, true, 0, new TrainTimetableStop(new Vector3Int(1, 2, 3), StationNodeSide.Front)));

            Assert.That(notified, Is.EqualTo(new[] { id }));
            Assert.That(datastore.TryGet(id, out var stored), Is.True);
            Assert.That(stored.IsAutoRun, Is.True);
            Assert.That(stored.Stops[0].Side, Is.EqualTo(StationNodeSide.Front));
        }

        [Test]
        public void LaterApplyReplacesEarlierState()
        {
            var datastore = new ClientTrainTimetableDatastore();
            var id = TrainUnitInstanceId.Create();
            datastore.Apply(Message(id, true, 0));
            datastore.Apply(Message(id, false, -1));

            Assert.That(datastore.TryGet(id, out var stored), Is.True);
            Assert.That(stored.IsAutoRun, Is.False);
            Assert.That(stored.CurrentIndex, Is.EqualTo(-1));
        }

        private static TrainTimetableMessagePack Message(TrainUnitInstanceId id, bool autoRun, int index, params TrainTimetableStop[] stops)
        {
            return new TrainTimetableMessagePack(new TrainTimetableSnapshot(id, autoRun, index, stops));
        }
    }
}
```

`TrainTimetableDtoBuilderTest.cs`: 既存テストの入力を `ClientTrainUnit` の snapshot から `ClientTrainTimetableDatastore.Apply` に置き換え、停車駅 DTO の `Side` が `"front"`/`"back"` になること、データストアに未着なら `Timetable` が null（＝webui は既存の「時刻表欠落時の文言」を出す）であることを assert する。パーサーテストに `side` の正常2件（"front","back"）と不正（欠落・"up"・数値）を追加。

- [ ] **Step 2: 失敗を確認** — `uloop compile` → 未定義エラー。

- [ ] **Step 3: 実装する**

`ClientTrainTimetableDatastore.cs`:

```csharp
using System;
using System.Collections.Generic;
using Game.Train.Unit;
using Server.Util.MessagePack;
using UniRx;

namespace Client.Game.InGame.Train.Timetable
{
    // サーバーから届いた列車ごとの時刻表をUI用に保持する（走行計算は参照しない）
    // Hold per-train timetables from the server for the UI; motion simulation never reads this
    public class ClientTrainTimetableDatastore
    {
        private readonly Dictionary<TrainUnitInstanceId, TrainTimetableMessagePack> _timetables = new();
        private readonly Subject<TrainUnitInstanceId> _onTimetableUpdated = new();
        public IObservable<TrainUnitInstanceId> OnTimetableUpdated => _onTimetableUpdated;

        public void Apply(TrainTimetableMessagePack message)
        {
            _timetables[message.TrainUnitInstanceId] = message;
            _onTimetableUpdated.OnNext(message.TrainUnitInstanceId);
        }

        public bool TryGet(TrainUnitInstanceId trainUnitInstanceId, out TrainTimetableMessagePack message)
        {
            return _timetables.TryGetValue(trainUnitInstanceId, out message);
        }
    }
}
```

`TrainTimetableEventHandler.cs`（`ItemStackLevelEventHandler` と同じ形の `IInitializable`）:

```csharp
using Client.Game.InGame.Context;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Timetable
{
    // 時刻表イベントを購読しデータストアへ反映する（tickバッファを通さない）
    // Subscribe to timetable events and apply them to the datastore, bypassing the tick buffer
    public class TrainTimetableEventHandler : IInitializable
    {
        private readonly ClientTrainTimetableDatastore _datastore;

        public TrainTimetableEventHandler(ClientTrainTimetableDatastore datastore)
        {
            _datastore = datastore;
        }

        public void Initialize()
        {
            ClientContext.VanillaApi.Event.SubscribeEventResponse(TrainTimetableEventPacket.EventTag, payload =>
            {
                _datastore.Apply(MessagePackSerializer.Deserialize<TrainTimetableMessagePack>(payload));
            });
        }
    }
}
```

`TrainResponseApi` に追加:

```csharp
        // 時刻表タブを開いたときに現在の時刻表を取り寄せる
        // Fetch the current timetable when the timetable tab opens
        public static async UniTask<GetTrainTimetableProtocol.GetTrainTimetableResponse> GetTrainTimetable(
            this VanillaApiWithResponse api, TrainUnitInstanceId trainUnitInstanceId, CancellationToken ct)
        {
            var request = new GetTrainTimetableProtocol.GetTrainTimetableRequest(trainUnitInstanceId);
            return await api.PacketExchange.GetPacketResponse<GetTrainTimetableProtocol.GetTrainTimetableResponse>(request, ct);
        }
```

`TrainTimetableDtos.cs`: `TrainTimetableStopDto : TrainTimetableStationDto` を作らず、`TrainTimetableStopDto { TrainStationPositionDto Position; string Name; string Side; }` を新設し `TrainTimetableDto.Stops` の型を `List<TrainTimetableStopDto>` にする。

`TrainTimetableDtoBuilder.Build(long trainCarInstanceId, TrainUnitClientCache cache, ClientTrainTimetableDatastore timetables, BlockGameObjectDataStore blocks)`: 列車IDは従来どおり `cache.TryGetCarSnapshot` で引く。`timetables.TryGet(unit.TrainUnitInstanceId, out var timetable)` が false なら `Debug.Log("[TrainTimetableDto] timetable not received yet: ...")` を出して null を返す（取得は BlockInventoryTopic が起こす）。停車駅は `timetable.Stops` から `Side = stop.Side == StationNodeSide.Front ? "front" : "back"`、`IsAutoRun`・`CurrentIndex` も `timetable` から。`TrainInventoryDtoFactory.Create` に `ClientTrainTimetableDatastore` 引数を足して渡す（呼び出し側 `BlockInventoryTopic` も更新）。

`BlockInventoryTopic`:
- コンストラクタで `ClientTrainTimetableDatastore` を `ClientDIContext.DIContainer.DIContainerResolver.Resolve<ClientTrainTimetableDatastore>()` で取得。`_trainSnapshotSubscription`（`OnSnapshotApplied` 購読）を `_trainTimetableSubscription = _timetables.OnTimetableUpdated.Where(_ => _subInventoryState.CurrentSubInventorySource is TrainSubInventorySource).Subscribe(_ => SchedulePublish());` に置き換える。
- `BuildJson` の列車分岐で、開いている列車の `TrainUnitInstanceId` が前回取得した列車と違えば取得を起こす:

```csharp
            if (_subInventoryState.CurrentSubInventorySource is TrainSubInventorySource trainSource)
            {
                TrackBlock(null);
                RequestTimetableWhenTrainChanged(trainSource);
                return WebUiJson.Serialize(TrainInventoryDtoFactory.Create(trainSource, sub, _trainUnitClientCache, _timetables, ClientDIContext.BlockGameObjectDataStore));
            }
```

`RequestTimetableWhenTrainChanged` は private メソッド: `_trainUnitClientCache.TryGetCarSnapshot` で列車IDを引き、`_requestedTimetableTrainId` と同じなら何もしない。違えば記録して `FetchTimetableAsync(id).Forget()`。`FetchTimetableAsync` は `ClientContext.VanillaApi.Response.GetTrainTimetable(id, CancellationToken.None)` を待ち、`Found` なら `_timetables.Apply(response.Timetable)`、そうでなければ `Debug.LogWarning` で理由を出す。列車以外の分岐・閉じた分岐では `_requestedTimetableTrainId = TrainUnitInstanceId.Empty` に戻す（次に開いたとき再取得するため）。ファイルが200行を超えるなら、この取得ロジックを `Client.WebUiHost/Game/Topics/Inventory/TrainTimetableFetcher.cs`（`void RequestWhenTrainChanged(TrainSubInventorySource)`, `void Reset()`）へ切り出す。

`TrainTimetableActions`: replace は `payload["stops"]` の JArray を `TrainTimetableStopParser.TryParse(token, out TrainTimetableStop stop)` で読む（`side` は `"front"`→Front、`"back"`→Back、それ以外は失敗）。失敗は `Reject("invalid_stop")`。`CreateReplaceTimetableRequest(trainUnitId, stops)` を送る。`TryResolveOpenTrain` は変更なし。

- [ ] **Step 4: 通ることを確認** — `uloop compile` ErrorCount 0 → `uloop run-tests ... --filter-value "WebUiHost\\.Train|TrainTimetable|WebUi"` 全PASS。

- [ ] **Step 5: コミット** — `git commit -m "feat(client): 時刻表をtick非依存のデータストアで保持し、時刻表タブは開いたとき取得＋イベント購読で更新する"`

### Task 4: 列車スナップショットから時刻表を取り除く

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Train/Unit/TrainSnapshots.cs`（`TrainTimetableStopSnapshot` 型と `TrainSimulationSnapshot` の3項目・ctor引数を削除、PR で足した `using UnityEngine;` も不要なら削除）
- Modify: `moorestech_server/Assets/Scripts/Game.Train/Unit/TrainUnitSnapshotFactory.cs`（停車駅抽出を削除し master と同じ形へ）
- Modify: `moorestech_server/Assets/Scripts/Server.Util/MessagePack/TrainUnitSnapshotMessagePack.cs`（`[Key(6..8)]` と関連処理を削除）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Unit/ClientTrainUnit.cs`（`IsAutoRun`・`TimetableCurrentIndex`・`TimetableStops`・`_timetableStops` と `SnapshotUpdate`／`CreateSimulationSnapshot` 内の該当行を削除）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Unit/TrainUnitClientCache.cs`（PR で足した `OnSnapshotApplied`・`_onSnapshotApplied`・その `OnNext` 呼び出し・`using System;`/`using UniRx;` を削除）
- Delete: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainTimetable/TrainTimetableSnapshotTest.cs`（と .meta）
- `TrainSimulationSnapshot` の ctor を呼んでいる箇所をすべて Grep で直す（テスト含む）

**Interfaces:**
- Consumes: Task 3 までで時刻表の読み手がデータストアへ移っていること
- Produces: master と同じ `TrainSimulationSnapshot(id, speed, distance, mascon, manualBranchSelectionIndex, cars)`

- [ ] **Step 1: 失敗するテストを書く** — `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainTimetable/TrainTimetableNotifyTest.cs` に、tick 同期の通信形に時刻表が載らないことを固定するテストを足す:

```csharp
        [Test]
        public void SimulationSnapshotWireFormHasNoTimetableFields()
        {
            var keys = typeof(TrainSimulationSnapshotMessagePack).GetProperties()
                .Select(property => property.Name).ToArray();
            CollectionAssert.DoesNotContain(keys, "IsAutoRun");
            CollectionAssert.DoesNotContain(keys, "TimetableCurrentIndex");
            CollectionAssert.DoesNotContain(keys, "TimetableStops");
        }
```

- [ ] **Step 2: 失敗を確認** — `uloop run-tests ... --filter-value "SimulationSnapshotWireFormHasNoTimetableFields"` → FAIL。
- [ ] **Step 3: 実装する** — 上記 Files の削除を行う。`git diff origin/master -- <TrainSnapshots.cs TrainUnitSnapshotFactory.cs TrainUnitSnapshotMessagePack.cs>` が空（または空行差のみ）になるまで戻す。
- [ ] **Step 4: 通ることを確認** — `uloop compile` ErrorCount 0 → `uloop run-tests ... --filter-value "Train|WebUi|PacketTest"` 全PASS。
- [ ] **Step 5: コミット** — `git commit -m "refactor(train): 列車のtick同期スナップショットから時刻表と自動運転フラグを除く"`

### Task 5: Web UI の停車駅に端を持たせ、追加は Back で固定する

**Files:**
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/inventory.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/payloadTypes.ts`（`TrainTimetableStop` 型を export）
- Modify: `moorestech_web/webui/src/bridge/transport/actionContract.ts:77`
- Modify: `moorestech_web/webui/src/features/blockInventory/train/timetableEditLogic.ts`
- Modify: `moorestech_web/webui/src/features/blockInventory/train/TrainTimetableSection.tsx`・`TrainTimetableStopList.tsx`（型の付け替え）
- Modify: `moorestech_web/webui/e2e/mock-host/fixtures/trainTimetableFixtures.ts`（stops に `side: "back"`）
- Test: `moorestech_web/webui/src/features/blockInventory/train/timetableEditLogic.test.ts`・`TrainTimetableSection.test.ts`

**Interfaces:**
- Consumes: Task 3 の DTO（`stops[].side`）と payload `{ stops: {x,y,z,side}[] }`

- [ ] **Step 1: 失敗するテストを書く** — `timetableEditLogic.test.ts` に追加:

```ts
import { describe, expect, it } from "vitest";
import { addStop, toReplacePayload } from "./timetableEditLogic";

describe("stop side", () => {
  it("adds new stops on the fixed UI side (back) and keeps existing sides", () => {
    const existing = { position: { x: 1, y: 0, z: 2 }, name: "A", side: "front" as const };
    const station = { position: { x: 5, y: 0, z: 6 }, name: "B" };
    const draft = addStop({ stops: [existing] }, station);
    expect(toReplacePayload(draft)).toEqual({
      stops: [
        { x: 1, y: 0, z: 2, side: "front" },
        { x: 5, y: 0, z: 6, side: "back" },
      ],
    });
  });
});
```

- [ ] **Step 2: 失敗を確認** — Run: `cd moorestech_web/webui && pnpm vitest run src/features/blockInventory/train` → FAIL。

- [ ] **Step 3: 実装する**

`inventory.ts`:

```ts
export const TrainTimetableStopSideSchema = z.enum(["front", "back"]);
export const TrainTimetableStopSchema = TrainTimetableStationSchema.extend({ side: TrainTimetableStopSideSchema });
export const TrainTimetableDataSchema = z.object({
  trainUnitId: z.string(),
  isAutoRun: z.boolean(),
  currentIndex: z.number().int(),
  stops: z.array(TrainTimetableStopSchema),
  stations: z.array(TrainTimetableStationSchema),
});
```

`payloadTypes.ts` に `TrainTimetableStopSchema` を import し `export type TrainTimetableStop = z.infer<typeof TrainTimetableStopSchema>;`。

`actionContract.ts:77`: `"train_timetable.replace": { stops: { x: number; y: number; z: number; side: "front" | "back" }[] };`

`timetableEditLogic.ts`:
- `export type TimetableDraft = { stops: TrainTimetableStop[] };`
- `// UIは入線方向を固定し、新しい停車駅は常に後端側へ着ける（裁定 2026-09-25）` / `// The UI fixes the arrival direction; new stops always use the back side (ruling 2026-09-25)` を付けて `export const UI_FIXED_STOP_SIDE = "back" as const;`
- `addStop(draft, station: TrainTimetableStation)` は `{ ...station, side: UI_FIXED_STOP_SIDE }` を積む。
- `removeStop`／`moveStop`／`canEnableAutoRun`／`sameStopOrder` の型を `TrainTimetableStop` に。`sameStopOrder` は位置に加えて `side` も一致を見る。
- `stopKey(stop)` は不要（行 key は既存どおり）。
- `toReplacePayload` は `{ stops: draft.stops.map((s) => ({ x: s.position.x, y: s.position.y, z: s.position.z, side: s.side })) }`。

`TrainTimetableSection.tsx`・`TrainTimetableStopList.tsx` は型名の付け替えだけ（端は表示しない）。fixtures の stops に `side: "back"`。

- [ ] **Step 4: 通ることを確認** — `pnpm vitest run` 全PASS、`pnpm tsc --noEmit`・`pnpm lint`・`pnpm build` OK。
- [ ] **Step 5: コミット** — `git commit -m "feat(webui): 停車駅に端を持たせ、UIからの追加は後端側で固定する"`

### Task 6: 録画プレイテストシナリオと docs の整合

**Files:**
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/train/train-timetable-via-ui.cs`（サーバー側 `train.IsAutoRun` を見ているだけなら変更不要。クライアントの `ClientTrainUnit.IsAutoRun` 等を見ていたら `ClientTrainTimetableDatastore` へ付け替え）
- Modify: `.agents/skills/creating-server-protocol/SKILL.md`（本PRで足した記述が「snapshot に同乗」を前例として勧めていないか確認し、勧めていれば「UI専用の状態は tick 同期経路に乗せず専用イベントで」へ直す）

- [ ] **Step 1:** `git grep -n "TimetableStops\|TimetableCurrentIndex\|OnSnapshotApplied\|snapshot.*同乗" -- .agents docs moorestech_*` で残骸が 0 件（ADR の改訂前記述・plan 2026-09-24 を除く）であることを確認し、見つかったものを直す。
- [ ] **Step 2:** コミット `git commit -m "docs: 時刻表のtick非同期化にシナリオとスキル記述を合わせる"`

### Task 7: 全ブランチレビュー（必須・省略不可）

- [ ] **Step 1:** 必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。スキルは moores-code-review。
- [ ] **Step 2:** 指摘の反映がソースに触れたら、`uloop compile` と Task 1〜5 のテスト（`Train|WebUi|PacketTest`）と webui の vitest を再実行する。
- [ ] **Step 3:** 残課題（未検証事項）は1件ずつ `bd create` で起票し、PR 本文・進捗記録には issue 番号を列挙する。録画プレイテスト（moorestech-lc9ty.1 で既知の停止）は再実施を試み、止まった場合はその事実と issue 番号を書く。
- [ ] **Step 4:** テストログは期待語ではなく警告語で引く: `uloop get-logs --project-path ./moorestech_client --log-type Warning` と `--log-type Error` で `mismatch`・`rejected`・`ignored`・`not found`・`Hash` を検索し、テスト区間でハッシュ不一致（`TrainUnitHashVerifier`）が出ていないことを確認する。
- [ ] **Step 5:** push し、PR #1415 本文の「Summary」「実装体制」「残課題」を更新する（tick 非同期化・端指定・裁定更新を追記）。

## 判断記録（ADR）

- 設計: `docs/adr/0067-train-timetable-ui-and-per-train-auto-run.md` の「2026-09-25 改訂」節。`.decisions/2026-09-25-時刻表と自動運転フラグはtick同期しない.md`、`.decisions/2026-09-25-停車駅の端はプロトコルで指定できUIが入線方向を固定する.md`、`.decisions/2026-09-24-時刻表UIの最終レビューでは既存Trainコードへの改修を持ち込まない.md`（2026-09-25 例外追記）。
- 同一tick内の重複通知を防ぐ「変化フラグ＋Consume」機構（`ConsumeCurrentEntryChanged`／`ConsumeAutoRunChanged`）は PR 初版のものを残し、通知先だけ timetable 窓口へ替える。出所: agent判断（既存 Train への改修を裁定の例外範囲に絞る）。
- 初期データは「タブを開いたときの列車単位の問い合わせ」とし、接続時の全列車一括取得はしない。出所: agent判断（ユーザー裁定 A は「開いたときの問い合わせ」を明示。一括取得は不要な状態保持になる）。
- 停車駅 DTO（`stops`）だけに `side` を持たせ、駅一覧（`stations`）には持たせない。出所: agent判断（UI は端を選ばせない裁定のため、選択肢側に端は不要）。
- 新ファイル配置: `Server.Event/EventReceive/Train/`・`Client.Game/InGame/Train/Timetable/` のサブディレクトリ。出所: agent判断（既存ディレクトリが10ファイル超のため規約どおり）。
- 実装はユーザーの「その方向でPR上で直して」によりこのセッションで続けて行う。出所: ユーザー裁定 2026-09-25 原文「その方向でPR上で直して、裁定も更新して」。

# 列車の時刻表UIと列車単位の自動運転ON/OFF Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 列車の車両インベントリ画面に「時刻表」タブを設け、駅の順序リストの丸ごと置換と列車単位の自動運転ON/OFFをWeb UIから行えるようにし、駅ブロックのUIで駅名を付けられるようにする。デバッグ用の全列車一括自動運転は撤去する。

**Architecture:** サーバーは既存 `TrainDiagram` に「丸ごと置換」1操作を足し、置換・自動運転切替は1プロトコル `va:trainScheduleEdit`（Operation enum）で受ける。時刻表と自動運転フラグは既存の列車 snapshot（`TrainSimulationSnapshot`）に同乗させて配信し、駅名はブロック状態（`IBlockStateObservable`）で配信する。クライアントは既存の `TrainUnitClientCache` → `BlockInventoryTopic` → Web UI の経路に時刻表DTOを足し、Web UI は `PanelTabs`（新様式）で「インベントリ / 時刻表」を切り替える。出発条件は固定1秒待機のまま変えない。

**Tech Stack:** Unity C#（moorestech_server / moorestech_client）・MessagePack・UniRx・NUnit・React/TypeScript（moorestech_web/webui、Mantine は白リスト部品のみ）・vitest・プレイテストDSL（unity-playmode-recorded-playtest）。bd: `moorestech-lc9ty`。設計: `docs/adr/0067-train-timetable-ui-and-per-train-auto-run.md`、`.decisions/2026-09-24-*.md`、`CONTEXT.md`「列車の運行」。

## Requirements

- R1: `TrainDiagram.ReplaceEntries(駅ノード列)` で時刻表を丸ごと置き換えられる。各エントリの出発条件は固定 `WaitForTicks = GameUpdater.TicksPerSecond`（20tick）。受入: 置換後 `Entries.Count == 入力数`、各エントリ `GetWaitForTicksInitialTicks() == 20`、`CurrentIndex == 0`（入力が空なら `-1`）。
- R2: 置換後の現在地は常に先頭、自動運転フラグは置換前のまま。受入: 自動運転ONの列車に置換しても `IsAutoRun == true` のまま `GetCurrentNode() == 先頭ノード`。
- R3: 空の時刻表で自動運転ONを送るとサーバーは受理し既存検証で即OFFになる（サーバーは拒否しない）。受入: `SetAutoRun(true)` 後の `Update()` 1回で `IsAutoRun == false`。
- R4: 時刻表に載せられるのは `BlockTypeConst.TrainStation` ブロックのみで、登録ノードは Back側 Exit。受入: 貨物プラットフォームの座標を含む置換は `NotTrainStation` で全体拒否、駅の無い座標は `StationBlockNotFound` で全体拒否、拒否理由は `Debug.LogWarning` に出る。
- R5: 線路が未接続の駅も登録できる（経路成立を検証しない）。受入: 未接続の2駅で置換が `Success == true`。
- R6: `va:trainScheduleEdit` は Operation enum（`ReplaceTimetable` / `SetAutoRun`）を持つ1プロトコルで、Request は static factory でしか作れない。受入: `PacketTest` で両 Operation が通る。
- R7: `va:setTrainStationName` で駅名を設定できる。空白のみの名前は `EmptyName` で拒否しログに出る。受入: 成功時 `TrainStationComponent.StationName` が更新され `OnChangeBlockState` が発火する。
- R8: 列車 snapshot に `IsAutoRun`・`TimetableCurrentIndex`・`TimetableStops[{StationPosition}]` が同乗し、ハッシュには含まれない。受入: `TrainUnitSnapshotHashCalculator.Compute` が時刻表の有無で同じ値、MessagePack 往復で3項目が保存される。
- R9: 時刻表置換・自動運転切替の適用直後と、到着で次エントリへ進んだ tick の後に、該当列車の snapshot が通知される。受入: `ITrainUnitSnapshotNotifyEvent.OnTrainUnitSnapshotNotified` を購読するテストで通知回数を確認。
- R10: デバッグ経路を撤去する（`TurnOnorOffTrainAutoRun`・`ResetAndNotifyNodeAddition`・`SendCommandProtocol` の `trainAutoRun`・`DebugSheet` の Train auto run トグル・`TrainCarEntityObject` のコマンド送信）。受入: `grep -rn "trainAutoRun\|TrainAutoRunKey\|ResetAndNotifyNodeAddition" moorestech_server/Assets/Scripts moorestech_client/Assets/Scripts` が0件。
- R11: 列車の車両インベントリ画面（source="train"）に「インベントリ / 時刻表」タブがある。時刻表タブは自動運転トグル・停車駅リスト（↑↓×）・駅一覧（追加）・「適用」を持ち、適用で1回だけ置換を送り、適用前に閉じたらローカル編集は破棄される。受入: vitest でローカル編集ロジック、プレイテストで UI クリック経路。
- R12: 時刻表が空のとき UI の自動運転ONは押せない（disabled）。受入: vitest で `stops.length === 0` → disabled。
- R13: 現在向かっている駅の行がハイライト（`data-current="true"`）される。受入: vitest。
- R14: 駅ブロック（TrainStation）のUIに駅名の入力欄と「決定」があり、`va:setTrainStationName` を送る。貨物・液体プラットフォームには出ない。受入: `trainStation` DTO が TrainStation にだけ付く。
- R15: 時刻表タブの駅表示は「駅名 (x, y, z)」、駅名が空なら「駅 (x, y, z)」。受入: vitest。
- R16: 同時編集は後勝ち（バージョン照合なし）。受入: 置換が連続2回来たら後の内容になる（`PacketTest`）。
- R17: ランタイムプレイテスト（録画）で、駅2基とレール・機関車を置き、UI経由で時刻表に2駅を追加→適用→自動運転ON→列車が走り出す→自動運転OFF→止まる、を確認する。合否は期待行の存在だけでなく、区間中の `Hash mismatch detected` 警告0件・`[TrainScheduleEdit]` `[SetTrainStationName]` の拒否ログ0件で判定する。
- やらないこと: 出発条件の種類・条件UIの追加／駅名による行き先指定（同名駅グループ）／train limit・割り込み／時刻表のバージョン照合／経路成立の事前検証／到達不能の理由表示／セーブ形式の変更／時刻表タブからの駅名変更／プレイテストでのキーボード入力による駅名入力（CEF へキー入力は転送されない: `CefInputForwarder` は修飾キーしか合成しない）。

## Global Constraints

- 作業場所: `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/train-timetable-ui`（ブランチ `feature/train-timetable-ui`、Unity Editor 起動済み）。最初に `pwd` で確認する。メインワークツリーでは作業しない。
- `.cs` を変更したら必ず `uloop compile --project-path ./moorestech_client`。「Unity is reloading」は45秒待って再試行。テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。
- `.meta` は手で作らない（Unity生成のコミットは可）。`partial`・`Func<>`・デフォルト引数・try-catch（外部境界以外）は禁止。1ファイル200行以下、1ディレクトリ10ファイル以下。
- コメントは「// 日本語」→「// English」の2行セット、各1行。
- イベントは UniRx（`Subject<T>` を private 保持し `IObservable<T>` で公開）。
- 単純 getter/setter は禁止（`{ get; private set; }` ＋ `SetHoge` は可）。
- fail-closed で何もしない経路は理由をログへ出す。
- 新プロトコルは creating-server-protocol スキルの型（Key(2)〜、`[Obsolete]` 引数なしctor、`PacketResponseCreator` 登録、`VanillaApiWithResponse` に1プロトコル=1メソッド）。
- Web UI は webui-design スキルの白リストだけを使う（Mantine `Button`/`SegmentedControl`/`TextInput` 等は禁止。`blockInventoryDesign.test.ts` が機械検査する）。文言は `Localization/localization.csv` に追加して `pnpm gen:i18n` で `localizationKeys.ts` を再生成する（`localizationKeysFreshness.test.ts` が突き合わせる）。Web UI の変更後は `cd moorestech_web/webui && pnpm build` で `dist` を更新しないとプレイテストに反映されない。
- `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs` と `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` の dirty は Unity 由来。戻さない・コミットもしない。
- 各タスク末尾で `git add <touched files>` → `git commit`。コミットメッセージ末尾に `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`。

---

## File Structure（配置と前例）

| # | 項目 | 配置先（asmdef） | 機構・前例 |
|---|---|---|---|
| 1 | `TrainDiagram.ReplaceEntries` / `ConsumeCurrentEntryChanged`（既存クラスへ追加） | `moorestech_server/Assets/Scripts/Game.Train/Diagram/TrainDiagram.cs`（Game.Train） | 既存 `ResetAndNotifyNodeAddition` の「全削除→追加」を正式化。294行→分割: 保存/復元を `TrainDiagramSaveDataConverter.cs` へ切り出し |
| 2 | `TrainTimetableStationNodeResolver`（新規 static） | `Game.Train/Diagram/TrainTimetableStationNodeResolver.cs` | `TrainUpdateService.IsDebugAutoRunStationNode` の規則（TrainStation・Back側Exit）を移設 |
| 3 | `TrainStationComponent.SetStationName` ＋ `IBlockStateObservable` | `moorestech_server/Assets/Scripts/Game.Block/Blocks/TrainRail/TrainStationComponent.cs` | 同ディレクトリ `TrainPlatformTransferComponent.SetMode`＋`OnChangeBlockState` と同型 |
| 4 | `TrainStationNameStateDetail`（新規） | `Game.Block/Blocks/TrainRail/TrainStationNameStateDetail.cs` | `TrainPlatformTransferStateDetail` と同型（Key + MessagePack） |
| 5 | `TrainTimetableStopSnapshot`／`TrainSimulationSnapshot` 拡張 | `Game.Train/Unit/TrainSnapshots.cs`、`Game.Train/Unit/TrainUnitSnapshotFactory.cs` | 既存 snapshot に項目追加。ハッシュ（`TrainUnitSnapshotHashCalculator.MixBundle`）は明示列挙なので不変 |
| 6 | `TrainTimetableStopMessagePack`／`TrainSimulationSnapshotMessagePack` 拡張 | `moorestech_server/Assets/Scripts/Server.Util/MessagePack/TrainUnitSnapshotMessagePack.cs`（Server.Util）→ 200行超のため `TrainTimetableStopMessagePack.cs` を分離 | 既存 Key(0..5) の続きに Key(6..8) |
| 7 | `TrainUpdateService`: デバッグ経路削除・到着後の snapshot 通知 | `Game.Train/Unit/TrainUpdateService.cs` | `UpdateTrains` の「snapshot,生成イベント系はこれ以降」コメント位置に置く。`ITrainUnitSnapshotNotifyEvent` は `ServerContext.GetService` で取得（`TrainPlatformItemContainerComponent:161` 前例） |
| 8 | `TrainScheduleEditProtocol`（新規） | `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/TrainScheduleEditProtocol.cs`（Server.Protocol） | 1プロトコル＝1ドメイン・Operation enum・static factory（`FilterSplitterStateProtocol`／`RailConnectionEditRequest`） |
| 9 | `SetTrainStationNameProtocol`（新規） | `Server.Protocol/PacketResponse/SetTrainStationNameProtocol.cs` | `SetTrainPlatformTransferModeProtocol` と同型 |
| 10 | `PacketResponseCreator` 登録2行 | `Server.Protocol/PacketResponseCreator.cs:70` 付近 | 既存登録と同形 |
| 11 | `SendCommandProtocol` の `trainAutoRun` 削除 | `Server.Protocol/PacketResponse/CommandProtocol.cs` | — |
| 12 | `ClientTrainUnit` に時刻表・自動運転を保持、`TrainUnitClientCache.OnSnapshotApplied` | `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Unit/`（Client.Game） | 既存 `SnapshotUpdate` で写す。UniRx `Subject<TrainUnitInstanceId>` |
| 13 | `VanillaApiWithResponse.SendTrainScheduleEdit` / `SetTrainStationName` | `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs` | `SetTrainPlatformTransferMode` の隣 |
| 14 | `TrainTimetableDtos.cs`／`TrainTimetableDtoBuilder.cs`（新規） | `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/`（Client.WebUiHost） | `TrainPlatformDetailDtoBuilder` と同型。`BlockInventoryDto` に `Timetable`・`TrainStation` を追加 |
| 15 | `TrainInventoryDtoFactory.Create` 拡張、`BlockInventoryTopic` の列車 snapshot 購読 | `Client.WebUiHost/Game/Topics/` | `OnSubInventoryUpdated` 購読と同じ `SchedulePublish` 経路 |
| 16 | `TrainTimetableActions.cs`／`TrainStationActions.cs`（新規 IActionHandler） | `Client.WebUiHost/Game/Actions/` | `TrainPlatformSetTransferModeActionHandler` と同型。`WebUiGameBinder.Bind` に `hub.RegisterAction` |
| 17 | デバッグトグル削除 | `Client.Common/DebugConst.cs`、`Client.DebugSystem/DebugSheet/DebugSheetController.cs:82`、`Client.Game/InGame/Train/View/Object/Core/TrainCarEntityObject.cs:30-31,157,170-201` | — |
| 18 | Web UI 契約: `schemas/inventory.ts`・`payloadTypes.ts`・`actionContract.ts` | `moorestech_web/webui/src/bridge/` | 既存 `TrainPlatformDataSchema`／`train_platform.set_transfer_mode` の隣 |
| 19 | `PanelTabs`（新規 shared/ui、白リスト追記） | `moorestech_web/webui/src/shared/ui/PanelTabs/{index.tsx,style.module.css,index.test.ts}`、`.agents/skills/webui-design/SKILL.md` §8.22 | `ModeSwitch` の面・トークンを流用（新様式はユーザー裁定済み） |
| 20 | `features/blockInventory/train/`: `TrainInventoryBody.tsx`・`TrainTimetableSection.tsx`・`TrainTimetableStopList.tsx`・`timetableEditLogic.ts`(+test)・`style.module.css` | `moorestech_web/webui/src/features/blockInventory/train/` | `TrainPlatformInventory`＋`TrainPlatformSection` と同型。1ディレクトリ10ファイル以下 |
| 21 | `TrainStationNameSection.tsx`（新規） | `features/blockInventory/details/` | `BuildMenuSearchInput` の素 `<input>` 様式（§8.9） |
| 22 | i18n キー追加 | `Localization/localization.csv`、`moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（生成） | `pnpm gen:i18n` |
| 23 | mock-host fixture | `moorestech_web/webui/e2e/mock-host/fixtures.ts` | `trainCargo` の隣に `trainWithTimetable` |
| 24 | プレイテストシナリオ（新規） | `.agents/skills/unity-playmode-recorded-playtest/scenarios/train/train-timetable-via-ui.cs` | `train-run-hash-check.cs`（機関車設置・燃料・走行監視）＋ `ClickWebUi` |

### データフロー地図

```
[時刻表タブ] --action train_timetable.replace/set_auto_run--> [Client.WebUiHost Action] --VanillaApi--> [va:trainScheduleEdit]
   --> TrainDiagram.ReplaceEntries / TrainUnit.TurnOn(Off)AutoRun --> ITrainUnitSnapshotNotifyEvent.NotifySnapshot
   --> va:event:trainUnitSnapshot --> TrainUnitClientCache.Upsert --> OnSnapshotApplied --> BlockInventoryTopic.SchedulePublish
   --> block_inventory.current(timetable) --> [時刻表タブ 再描画]
[駅名欄] --action train_station.set_name--> [Action] --> [va:setTrainStationName] --> TrainStationComponent.SetStationName
   --> OnChangeBlockState --> ChangeBlockStateEvent --> BlockGameObject._blockStateMessagePack --> BlockInventoryTopic(既存購読) --> [駅名欄]
```
新規コンポーネントはすべて既存の書き手／読み手の位置に入る。交差点（bool 戻り・直接セッター・並行経路）は作らない。

### 共有されるサーバー状態の持ち主と最新化

| 事実 | 保持者 | 書き換え操作 | 最新化経路 |
|---|---|---|---|
| 時刻表・自動運転 | `TrainUnit`（正）→ `ClientTrainUnit`（写し） | `va:trainScheduleEdit`、到着で次エントリ | Task 4/5 の `NotifySnapshot` → Task 8 の `OnSnapshotApplied` → Task 9 の `SchedulePublish`（R9・R11） |
| 駅名 | `TrainStationComponent`（正）→ `BlockGameObject._blockStateMessagePack`（写し） | `va:setTrainStationName` | `OnChangeBlockState` → 既存 `ChangeBlockStateEventPacket` → `BlockInventoryTopic.TrackBlock` の既存購読で再配信（Task 3/9）。時刻表タブの駅一覧は開いた時点の名前を出す（同時に開く画面は1つなので鮮度は開くたびに保証される） |

### 機能パリティ（死活表）

| 操作 | 計画後 | 根拠 |
|---|---|---|
| デバッグシート「Train auto run」で全列車ON/OFF | 廃止（裁定） | ADR 0067「デバッグ経路は撤去する」 |
| F で車両インベントリを開く / E で乗車 | 生きる | `TrainCarInteractActions` は触らない |
| 貨物プラットフォームの積込/卸し切替 | 生きる | `TrainPlatformSection` は触らず、`TrainPlatformInventory` に駅名欄を足すだけ |
| プレイテスト `train-run-hash-check.cs` の `train.trainDiagram.AddEntry` 直呼び | 生きる | `AddEntry` は残す |

---

### Task 1: TrainDiagram に丸ごと置換と現在地変化フラグを足し、デバッグ用一括投入を消す

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Train/Diagram/TrainDiagram.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Train/Diagram/TrainDiagramSaveDataConverter.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Train/Diagram/TrainDiagramManager.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainDiagramReplaceEntriesTest.cs`

**Interfaces:**
- Produces: `public void ReplaceEntries(IReadOnlyList<IRailNode> stationNodes)`（全削除→各ノードを `WaitForTicks = GameUpdater.TicksPerSecond` で追加、`_currentIndex` は空なら -1、それ以外 0）
- Produces: `public bool ConsumeCurrentEntryChanged()`（`MoveToNextEntry`・`HandleNodeRemoval`・`ReplaceEntries` で立つフラグを読んで下ろす）
- Produces: `public static class TrainDiagramSaveDataConverter { public static TrainDiagramSaveData Create(TrainDiagram diagram); public static void Restore(TrainDiagram diagram, TrainDiagramSaveData saveData, IRailGraphProvider railGraphProvider); }`（既存の `CreateTrainDiagramSaveData`／`RestoreState` の中身を移す。`TrainDiagram` 側は薄い委譲を残す）

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System.Collections.Generic;
using Core.Update;
using Game.Train.Diagram;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using NUnit.Framework;
using Tests.Util;

namespace Tests.UnitTest.Game
{
    public class TrainDiagramReplaceEntriesTest
    {
        [Test]
        public void ReplaceEntriesResetsToHeadAndKeepsAutoRun()
        {
            using var scenario = TrainAutoRunTestScenario.CreateRunningScenario();
            var train = scenario.Train;
            var diagram = train.trainDiagram;
            Assert.IsTrue(train.IsAutoRun);
            Assert.Greater(diagram.Entries.Count, 1);

            // 先頭以外の駅から始まる新しい順序へ置き換える
            // Replace with a new order that starts from a different node
            var newNodes = new List<IRailNode> { diagram.Entries[1].Node, diagram.Entries[0].Node };
            diagram.ReplaceEntries(newNodes);

            Assert.AreEqual(2, diagram.Entries.Count);
            Assert.AreEqual(0, diagram.CurrentIndex, "置換後の現在地は常に先頭");
            Assert.AreSame(newNodes[0], diagram.GetCurrentNode());
            Assert.IsTrue(train.IsAutoRun, "置換で自動運転フラグは変わらない");
            Assert.AreEqual(GameUpdater.TicksPerSecond, diagram.Entries[0].GetWaitForTicksInitialTicks(), "固定1秒待機");
            Assert.AreEqual(GameUpdater.TicksPerSecond, diagram.Entries[1].GetWaitForTicksInitialTicks());
            Assert.IsTrue(diagram.ConsumeCurrentEntryChanged(), "置換は現在地変化として1回だけ観測できる");
            Assert.IsFalse(diagram.ConsumeCurrentEntryChanged());
        }

        [Test]
        public void ReplaceEntriesWithEmptyListTurnsOffAutoRunOnNextUpdate()
        {
            using var scenario = TrainAutoRunTestScenario.CreateRunningScenario();
            var train = scenario.Train;

            train.trainDiagram.ReplaceEntries(new List<IRailNode>());

            Assert.AreEqual(-1, train.trainDiagram.CurrentIndex);
            Assert.IsNull(train.trainDiagram.GetCurrentNode());
            train.Update();
            Assert.IsFalse(train.IsAutoRun, "目的地が無くなれば既存検証で自動運転は解除される");
        }

        [Test]
        public void MoveToNextEntryRaisesCurrentEntryChanged()
        {
            using var scenario = TrainAutoRunTestScenario.CreateRunningScenario();
            var diagram = scenario.Train.trainDiagram;
            Assert.IsFalse(diagram.ConsumeCurrentEntryChanged(), "シナリオ構築後はフラグが下りている");

            diagram.MoveToNextEntry();

            Assert.IsTrue(diagram.ConsumeCurrentEntryChanged());
        }

        [Test]
        public void SaveDataRoundTripKeepsEntriesAfterReplace()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            var diagram = scenario.Train.trainDiagram;
            diagram.ReplaceEntries(new List<IRailNode> { scenario.StationExitFront });

            var saveData = diagram.CreateTrainDiagramSaveData();

            Assert.AreEqual(1, saveData.Entries.Count);
            Assert.AreEqual(0, saveData.CurrentIndex);
            Assert.AreEqual(GameUpdater.TicksPerSecond, saveData.Entries[0].WaitForTicksInitial);
        }
    }
}
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `ReplaceEntries` / `ConsumeCurrentEntryChanged` が無いのでコンパイルエラー（CS1061）

- [ ] **Step 3: `TrainDiagramSaveDataConverter.cs` を新設し、保存・復元を移す**

```csharp
using System.Collections.Generic;
using System.Linq;
using Game.Train.RailGraph;
using Game.Train.Unit;

namespace Game.Train.Diagram
{
    // TrainDiagram とセーブデータの相互変換
    // Converts between TrainDiagram and its save data
    public static class TrainDiagramSaveDataConverter
    {
        public static TrainDiagramSaveData Create(TrainDiagram diagram)
        {
            var entries = new List<TrainDiagramEntrySaveData>();
            foreach (var entry in diagram.Entries)
            {
                entries.Add(new TrainDiagramEntrySaveData
                {
                    EntryId = entry.entryId,
                    Node = entry.Node.ConnectionDestination,
                    DepartureConditions = entry.DepartureConditionTypes?.ToList() ?? new List<TrainDiagram.DepartureConditionType>(),
                    WaitForTicksInitial = entry.GetWaitForTicksInitialTicks(),
                    WaitForTicksRemaining = entry.GetWaitForTicksRemainingTicks()
                });
            }
            return new TrainDiagramSaveData { CurrentIndex = diagram.CurrentIndex, Entries = entries };
        }

        // ノードを解決できないエントリは読み飛ばす（既存挙動）
        // Entries whose node cannot be resolved are skipped (existing behavior)
        public static List<TrainDiagramEntry> RestoreEntries(TrainDiagramSaveData saveData, IRailGraphProvider railGraphProvider)
        {
            var entries = new List<TrainDiagramEntry>();
            if (saveData?.Entries == null) return entries;
            foreach (var entryData in saveData.Entries)
            {
                if (entryData == null) continue;
                var node = railGraphProvider.ResolveRailNode(entryData.Node);
                if (node == null) continue;
                entries.Add(TrainDiagramEntry.CreateFromSaveData(
                    node, entryData.EntryId, entryData.DepartureConditions,
                    entryData.WaitForTicksInitial, entryData.WaitForTicksRemaining));
            }
            return entries;
        }
    }
}
```

- [ ] **Step 4: `TrainDiagram.cs` を書き換える**

`RestoreState` の本体を `TrainDiagramSaveDataConverter.RestoreEntries` 呼び出し＋インデックス補正に、`CreateTrainDiagramSaveData` を `TrainDiagramSaveDataConverter.Create(this)` に置き換える。`AddEntry(IRailNode node, DepartureConditionType departureConditionType, int waitTicks = 0)` のデフォルト引数を外す（3引数呼び出しは `ResetAndNotifyNodeAddition` だけで、Step 5 で消えるので他の呼び出し側修正は無い。`grep -rn "AddEntry(" moorestech_server/Assets/Scripts | grep -v TrainDiagram.cs` で2引数以下だけであることを確認する）。次のメンバーを追加する:

```csharp
        private bool _isCurrentEntryChanged;

        // 時刻表を丸ごと置き換える。各駅は固定1秒待機で発車し、現在地は常に先頭へ戻る
        // Replace the whole timetable; every stop departs after a fixed 1s wait and the cursor returns to the head
        public void ReplaceEntries(IReadOnlyList<IRailNode> stationNodes)
        {
            _entries.Clear();
            _currentIndex = -1;
            foreach (var node in stationNodes)
            {
                AddEntry(node, DepartureConditionType.WaitForTicks, GameUpdater.TicksPerSecond);
            }
            _isCurrentEntryChanged = true;
        }

        // 現在地が変わったかを読んで下ろす（snapshot通知の判断用）
        // Read and clear whether the current entry changed (used to decide snapshot notification)
        public bool ConsumeCurrentEntryChanged()
        {
            var changed = _isCurrentEntryChanged;
            _isCurrentEntryChanged = false;
            return changed;
        }
```

`MoveToNextEntry()` の末尾（`_currentIndex = (_currentIndex + 1) % _entries.Count;` の後）と `HandleNodeRemoval` の末尾（`if (isCurrentEntryRemoved && _entries.Count > 0)` の直前）に `_isCurrentEntryChanged = true;` を足す。`using Core.Update;` を追加する。

- [ ] **Step 5: `TrainDiagramManager.ResetAndNotifyNodeAddition` を削除する**

`TrainDiagramManager.cs` から `ResetAndNotifyNodeAddition` メソッドと `using Core.Update;` を消す。この時点で `TrainUpdateService.AutoDiagramNodeAdditionExample` がコンパイルエラーになるので、Task 7 を先に済ませてもよいが、本タスク内では `TrainUpdateService.cs` の `_diagramManager.ResetAndNotifyNodeAddition(stationNodes);` の1行だけを一時的に `// Task 7 で撤去` として削除して通す。

- [ ] **Step 6: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → Expected: エラー0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainDiagramReplaceEntriesTest|TrainDiagramAutoRunOperationsTest|TrainDiagramUpdateTest|TrainDiagramSaveLoadTest"` → Expected: 全PASS

- [ ] **Step 7: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.Train/Diagram/ moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainDiagramReplaceEntriesTest.cs moorestech_server/Assets/Scripts/Game.Train/Unit/TrainUpdateService.cs
git commit -m "feat(train): TrainDiagram に丸ごと置換と現在地変化フラグを追加しデバッグ用一括投入を撤去"
```

---

### Task 2: 駅ブロック→時刻表ノードの解決関数

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Train/Diagram/TrainTimetableStationNodeResolver.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainTimetableStationNodeResolverTest.cs`

**Interfaces:**
- Produces: `public static class TrainTimetableStationNodeResolver { public static bool TryResolve(IBlock block, out IRailNode node); }` — `block.BlockMasterElement.BlockType == BlockTypeConst.TrainStation` かつその `RailComponent` 群の中で `StationRef.NodeSide == Back && NodeRole == Exit` のノードを返す。それ以外は false（呼び出し側が理由をログする）。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using Game.Block.Interface;
using Game.Train.Diagram;
using Game.Train.RailGraph;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    public class TrainTimetableStationNodeResolverTest
    {
        [Test]
        public void ResolvesBackExitNodeOfTrainStation()
        {
            var env = TrainTestHelper.CreateEnvironment();
            var (block, components) = TrainTestHelper.PlaceBlockWithRailComponents(env, ForUnitTestModBlockId.TestTrainStation, Vector3Int.zero, BlockDirection.North);

            var resolved = TrainTimetableStationNodeResolver.TryResolve(block, out var node);

            Assert.IsTrue(resolved);
            Assert.AreEqual(StationNodeSide.Back, node.StationRef.NodeSide);
            Assert.AreEqual(StationNodeRole.Exit, node.StationRef.NodeRole);
            Assert.AreSame(block, node.StationRef.StationBlock);
        }

        [Test]
        public void RejectsItemPlatform()
        {
            var env = TrainTestHelper.CreateEnvironment();
            var block = TrainTestHelper.PlaceBlock(env, ForUnitTestModBlockId.TestTrainItemPlatform, Vector3Int.zero, BlockDirection.North);

            Assert.IsFalse(TrainTimetableStationNodeResolver.TryResolve(block, out var node));
            Assert.IsNull(node);
        }
    }
}
```

- [ ] **Step 2: 実行して失敗を確認** — `uloop compile` で CS0103（型が無い）

- [ ] **Step 3: 実装**

```csharp
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Train.RailGraph;

namespace Game.Train.Diagram
{
    // 駅ブロックから時刻表に登録するノード（Back側Exit）を引く
    // Resolve the node registered in a timetable (back-side exit) from a station block
    public static class TrainTimetableStationNodeResolver
    {
        public static bool TryResolve(IBlock block, out IRailNode node)
        {
            node = null;
            if (block == null) return false;
            if (block.BlockMasterElement.BlockType != BlockTypeConst.TrainStation) return false;

            // 駅の2本のRailComponentからBack側のExitノードを探す
            // Search the station's two rail components for the back-side exit node
            foreach (var rail in block.GetComponents<RailComponent>())
            {
                if (IsBackExit(rail.BackNode)) { node = rail.BackNode; return true; }
                if (IsBackExit(rail.FrontNode)) { node = rail.FrontNode; return true; }
            }
            return false;

            #region Internal

            bool IsBackExit(RailNode candidate)
            {
                if (candidate == null || candidate.StationRef == null) return false;
                return candidate.StationRef.NodeSide == StationNodeSide.Back && candidate.StationRef.NodeRole == StationNodeRole.Exit;
            }

            #endregion
        }
    }
}
```

`BlockTypeConst` の名前空間は `grep -rn "class BlockTypeConst" moorestech_server/Assets/Scripts` で確認して using を合わせる（`TrainUpdateService.cs` が既に参照している）。

- [ ] **Step 4: コンパイル・テスト** — `--filter-value "TrainTimetableStationNodeResolverTest"` → PASS
- [ ] **Step 5: コミット** — `git commit -m "feat(train): 駅ブロックから時刻表ノードを解決する関数を追加"`

---

### Task 3: 駅名の設定と配信（サーバー）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/TrainRail/TrainStationComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/TrainRail/TrainStationNameStateDetail.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/Train/VanillaTrainStationTemplate.cs:39`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainStationNameTest.cs`

**Interfaces:**
- Produces: `TrainStationComponent : IBlockSaveState, IBlockStateObservable` に `public string StationName { get; private set; }`（`{ get; private set; }`＋`SetHoge` は規約で許容）、`public void SetStationName(string stationName)`、`public IObservable<Unit> OnChangeBlockState`、`public BlockStateDetail[] GetBlockStateDetails()`
- Produces: `TrainStationNameStateDetail { public const string BlockStateDetailKey = "TrainStationName"; [Key(0)] public string StationName; public static BlockStateDetail CreateState(string) }`
- 新設駅の初期名は空文字（UI側が「駅 (x,y,z)」で補う）。既存セーブの "test" はそのまま残る。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using MessagePack;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UniRx;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    public class TrainStationNameTest
    {
        [Test]
        public void SetStationNameNotifiesAndExposesStateDetail()
        {
            var env = TrainTestHelper.CreateEnvironment();
            var block = TrainTestHelper.PlaceBlock(env, ForUnitTestModBlockId.TestTrainStation, Vector3Int.zero, BlockDirection.North);
            var station = block.GetComponent<TrainStationComponent>();
            Assert.AreEqual(string.Empty, station.StationName, "新設駅の初期名は空");

            var notified = 0;
            station.OnChangeBlockState.Subscribe(_ => notified++);
            station.SetStationName("北駅");

            Assert.AreEqual("北駅", station.StationName);
            Assert.AreEqual(1, notified);
            var detail = station.GetBlockStateDetails()[0];
            Assert.AreEqual(TrainStationNameStateDetail.BlockStateDetailKey, detail.Key);
            var decoded = MessagePackSerializer.Deserialize<TrainStationNameStateDetail>(detail.Value);
            Assert.AreEqual("北駅", decoded.StationName);
        }
    }
}
```

- [ ] **Step 2: 実行して失敗を確認** — CS1061（`SetStationName` 無し）

- [ ] **Step 3: `TrainStationNameStateDetail.cs` を作る**

```csharp
using System;
using Game.Block.Interface.Component;
using MessagePack;

namespace Game.Block.Blocks.TrainRail
{
    [MessagePackObject]
    public class TrainStationNameStateDetail
    {
        public const string BlockStateDetailKey = "TrainStationName";

        [Key(0)] public string StationName { get; set; }

        public TrainStationNameStateDetail(string stationName)
        {
            StationName = stationName;
        }

        public static BlockStateDetail CreateState(string stationName)
        {
            var detail = new TrainStationNameStateDetail(stationName);
            return new BlockStateDetail(BlockStateDetailKey, MessagePackSerializer.Serialize(detail));
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public TrainStationNameStateDetail() { }
    }
}
```

- [ ] **Step 4: `TrainStationComponent.cs` を書き換える**

```csharp
using System;
using System.Collections.Generic;
using Game.Block.Interface.Component;
using UniRx;

namespace Game.Block.Blocks.TrainRail
{
    public class TrainStationComponent : IBlockSaveState, IBlockStateObservable
    {
        public string StationName { get; private set; }
        public string SaveKey { get; } = typeof(TrainStationComponent).FullName;
        public bool IsDestroy { get; private set; }

        // 駅名変化をクライアントへ流すためのSubject
        // Subject that pushes station-name changes toward clients
        private readonly Subject<Unit> _onChangeBlockState = new();
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;

        public TrainStationComponent(string stationName)
        {
            StationName = stationName;
        }

        public TrainStationComponent(Dictionary<string, object> componentStates) : this(string.Empty)
        {
            if (!BlockComponentStateReader.TryRead<TrainStationComponentSaveData>(componentStates, SaveKey, out var saveData)) return;
            StationName = saveData.stationName;
        }

        public void SetStationName(string stationName)
        {
            StationName = stationName;
            _onChangeBlockState.OnNext(Unit.Default);
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            return new[] { TrainStationNameStateDetail.CreateState(StationName) };
        }

        public object GetSaveState()
        {
            return new TrainStationComponentSaveData(StationName);
        }

        public void Destroy()
        {
            IsDestroy = true;
        }

        [Serializable]
        public class TrainStationComponentSaveData
        {
            public string stationName;

            public TrainStationComponentSaveData(string stationName)
            {
                this.stationName = stationName;
            }
        }
    }
}
```

`VanillaTrainStationTemplate.cs:39` の `new TrainStationComponent("test")` を `new TrainStationComponent(string.Empty)` に変える。`Game.Block.asmdef` に UniRx 参照があることを `grep -n UniRx moorestech_server/Assets/Scripts/Game.Block/Game.Block.asmdef` で確認する（`TrainPlatformTransferComponent` が使っているので既にある）。

- [ ] **Step 5: 既存テストで "test" を前提にしている箇所を直す** — `grep -rn '"test"' moorestech_server/Assets/Scripts/Tests | grep -i station` で当たった行を空文字前提に修正。
- [ ] **Step 6: コンパイル・テスト** — `--filter-value "TrainStationNameTest|TrainStationDocking"` → PASS
- [ ] **Step 7: コミット** — `git commit -m "feat(train): 駅名の設定とブロック状態配信を追加"`

---

### Task 4: 列車 snapshot に時刻表と自動運転フラグを同乗させる

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Train/Unit/TrainSnapshots.cs`（`TrainSimulationSnapshot` 拡張・`TrainTimetableStopSnapshot` 追加）
- Modify: `moorestech_server/Assets/Scripts/Game.Train/Unit/TrainUnitSnapshotFactory.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Util/MessagePack/TrainTimetableStopMessagePack.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Util/MessagePack/TrainUnitSnapshotMessagePack.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Unit/ClientTrainUnit.cs`（コンストラクタ引数追加に追従）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainTimetableSnapshotTest.cs`

**Interfaces:**
- Produces: `public readonly struct TrainTimetableStopSnapshot { public Vector3Int StationPosition { get; } }`
- Produces: `TrainSimulationSnapshot` コンストラクタを `(trainUnitInstanceId, currentSpeed, accumulatedDistance, masconLevel, manualBranchSelectionIndex, cars, bool isAutoRun, int timetableCurrentIndex, IReadOnlyList<TrainTimetableStopSnapshot> timetableStops)` に拡張し、`IsAutoRun`・`TimetableCurrentIndex`・`TimetableStops` プロパティを持つ
- Produces: `TrainSimulationSnapshotMessagePack` に `[Key(6)] bool IsAutoRun`、`[Key(7)] int TimetableCurrentIndex`、`[Key(8)] List<TrainTimetableStopMessagePack> TimetableStops`
- Produces: `TrainTimetableStopMessagePack { [Key(0)] Vector3IntMessagePack StationPosition }`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System.Collections.Generic;
using Game.Train.RailGraph;
using Game.Train.Unit;
using MessagePack;
using NUnit.Framework;
using Server.Util.MessagePack;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    public class TrainTimetableSnapshotTest
    {
        [Test]
        public void SnapshotCarriesTimetableAndAutoRunWithoutChangingHash()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            var train = scenario.Train;
            var before = TrainUnitSnapshotHashCalculator.Compute(new[] { TrainUnitSnapshotFactory.CreateSnapshot(train) });

            train.trainDiagram.ReplaceEntries(new List<IRailNode> { scenario.StationExitFront });
            var bundle = TrainUnitSnapshotFactory.CreateSnapshot(train);

            Assert.IsTrue(bundle.Simulation.IsAutoRun);
            Assert.AreEqual(0, bundle.Simulation.TimetableCurrentIndex);
            Assert.AreEqual(1, bundle.Simulation.TimetableStops.Count);
            Assert.AreEqual(Vector3Int.zero, bundle.Simulation.TimetableStops[0].StationPosition, "駅ブロックの原点座標");
            var after = TrainUnitSnapshotHashCalculator.Compute(new[] { bundle });
            Assert.AreEqual(before, after, "時刻表はハッシュに含めない");
        }

        [Test]
        public void MessagePackRoundTripKeepsTimetable()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            scenario.Train.trainDiagram.ReplaceEntries(new List<IRailNode> { scenario.StationExitFront });
            var bundle = TrainUnitSnapshotFactory.CreateSnapshot(scenario.Train);

            var bytes = MessagePackSerializer.Serialize(new TrainUnitSnapshotBundleMessagePack(bundle));
            var restored = MessagePackSerializer.Deserialize<TrainUnitSnapshotBundleMessagePack>(bytes).ToModel();

            Assert.AreEqual(bundle.Simulation.IsAutoRun, restored.Simulation.IsAutoRun);
            Assert.AreEqual(bundle.Simulation.TimetableCurrentIndex, restored.Simulation.TimetableCurrentIndex);
            Assert.AreEqual(1, restored.Simulation.TimetableStops.Count);
            Assert.AreEqual(Vector3Int.zero, restored.Simulation.TimetableStops[0].StationPosition);
        }
    }
}
```

- [ ] **Step 2: 実行して失敗を確認** — CS1061（`IsAutoRun` 無し）

- [ ] **Step 3: `TrainSnapshots.cs` に追加**

`TrainSimulationSnapshot` の直前に追加:

```csharp
    // 時刻表の1停車駅（駅ブロックの原点座標）
    // One timetable stop (origin position of the station block)
    public readonly struct TrainTimetableStopSnapshot
    {
        public TrainTimetableStopSnapshot(Vector3Int stationPosition)
        {
            StationPosition = stationPosition;
        }

        public Vector3Int StationPosition { get; }
    }
```

`TrainSimulationSnapshot` のコンストラクタ末尾に `bool isAutoRun, int timetableCurrentIndex, IReadOnlyList<TrainTimetableStopSnapshot> timetableStops` を足し、`IsAutoRun`・`TimetableCurrentIndex`・`TimetableStops` を `{ get; }` で公開する。`using UnityEngine;` を確認する。

- [ ] **Step 4: `TrainUnitSnapshotFactory.BuildSimulationSnapshot` を拡張**

```csharp
            var stops = new List<TrainTimetableStopSnapshot>(train.trainDiagram.Entries.Count);
            foreach (var entry in train.trainDiagram.Entries)
            {
                // 駅参照が無いノード（テスト用の裸ノード等）は座標を持たないので飛ばす
                // Nodes without a station reference (bare test nodes) have no position, so skip them
                if (entry.Node.StationRef == null || !entry.Node.StationRef.HasStation) continue;
                stops.Add(new TrainTimetableStopSnapshot(entry.Node.StationRef.StationPosition));
            }

            return new TrainSimulationSnapshot(
                train.TrainUnitInstanceId,
                train.CurrentSpeed,
                train.AccumulatedDistance,
                train.masconLevel,
                train.GetManualBranchSelectionIndex(),
                carSnapshots,
                train.IsAutoRun,
                train.trainDiagram.CurrentIndex,
                stops);
```

- [ ] **Step 5: MessagePack を拡張**

`TrainTimetableStopMessagePack.cs`（新規、Server.Util）:

```csharp
using System;
using Game.Train.Unit;
using MessagePack;

namespace Server.Util.MessagePack
{
    [MessagePackObject]
    public class TrainTimetableStopMessagePack
    {
        [Key(0)] public Vector3IntMessagePack StationPosition { get; set; }

        [Obsolete("Reserved for MessagePack serialization.")]
        public TrainTimetableStopMessagePack() { }

        public TrainTimetableStopMessagePack(TrainTimetableStopSnapshot stop)
        {
            StationPosition = new Vector3IntMessagePack(stop.StationPosition);
        }

        public TrainTimetableStopSnapshot ToModel()
        {
            return new TrainTimetableStopSnapshot(StationPosition.Vector3Int);
        }
    }
}
```

`TrainSimulationSnapshotMessagePack` に `[Key(6)] public bool IsAutoRun`、`[Key(7)] public int TimetableCurrentIndex`、`[Key(8)] public List<TrainTimetableStopMessagePack> TimetableStops` を足し、コンストラクタで `snapshot.TimetableStops?.Select(s => new TrainTimetableStopMessagePack(s)).ToList() ?? new()` を詰め、`ToModel()` の `new TrainSimulationSnapshot(...)` 末尾に `IsAutoRun, TimetableCurrentIndex, TimetableStops?.Select(s => s.ToModel()).ToArray() ?? Array.Empty<TrainTimetableStopSnapshot>()` を渡す。ファイルが200行を超えるなら `TrainCarSnapshotMessagePack` を `TrainCarSnapshotMessagePack.cs` へ切り出す。

- [ ] **Step 6: クライアントの追従（コンパイルを通すための最小）**

`ClientTrainUnit.cs`: `public bool IsAutoRun { get; private set; }`、`public int TimetableCurrentIndex { get; private set; }`、`public IReadOnlyList<TrainTimetableStopSnapshot> TimetableStops => _timetableStops ?? Array.Empty<TrainTimetableStopSnapshot>();` を追加。`SnapshotUpdate` で3項目を写す。`TryCreateSnapshotBundle` 内 `CreateSimulationSnapshot()` の `new TrainSimulationSnapshot(...)` 末尾に `IsAutoRun, TimetableCurrentIndex, TimetableStops` を渡す。

- [ ] **Step 7: コンパイル・テスト** — `--filter-value "TrainTimetableSnapshotTest|TrainUnitSnapshot|TrainFullSnapshot|TrainHugeAutoRunSaveLoadConsistencyTest"` → PASS
- [ ] **Step 8: コミット** — `git commit -m "feat(train): 列車snapshotに時刻表と自動運転フラグを同乗させる"`

---

### Task 5: 到着で次エントリへ進んだ tick の後に snapshot を通知する

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Train/Unit/TrainUpdateService.cs`（`UpdateTrains` の `NotifyPreSimulationDiff(_executedTick);` の直後）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainTimetableSnapshotNotifyTest.cs`

**Interfaces:**
- Consumes: Task 1 の `TrainDiagram.ConsumeCurrentEntryChanged()`、既存 `ITrainUnitSnapshotNotifyEvent.NotifySnapshot(TrainUnit)`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System.Collections.Generic;
using Game.Context;
using Game.Train.Event;
using Game.Train.RailGraph;
using NUnit.Framework;
using Tests.Util;
using UniRx;

namespace Tests.UnitTest.Game
{
    public class TrainTimetableSnapshotNotifyTest
    {
        [Test]
        public void SnapshotIsNotifiedWhenCurrentEntryAdvances()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            var train = scenario.Train;
            var updateService = scenario.Environment.GetTrainUpdateService();
            var notify = ServerContext.GetService<ITrainUnitSnapshotNotifyEvent>();
            var count = 0;
            notify.OnTrainUnitSnapshotNotified.Subscribe(data => { if (data.TrainUnitInstanceId == train.TrainUnitInstanceId && !data.IsDeleted) count++; });

            // 待機400tickのドッキング中。発車で次エントリへ進むまで回す
            // Docked with a 400-tick wait; run until departure advances the entry
            for (var i = 0; i < 450 && count == 0; i++) updateService.UpdateTrains();

            Assert.AreEqual(1, count, "現在地が進んだtickの後に1回だけ通知される");
        }
    }
}
```

`TrainAutoRunTestScenario` に `Environment` プロパティが無ければ `public TrainTestEnvironment Environment { get; }` を足す（コンストラクタで受けている `environment` を公開するだけ）。`TrainUnitSnapshotNotifyEventData` のプロパティ名は `Game.Train/Event/TrainUnitSnapshotNotifyEventData.cs` を読んで合わせる。

- [ ] **Step 2: 実行して失敗を確認** — FAIL（count == 0）

- [ ] **Step 3: 実装**

`UpdateTrains` の `NotifyPreSimulationDiff(_executedTick);` の直後に追加:

```csharp
            // 到着で時刻表の現在地が進んだ列車は、シミュレーション後にsnapshotを流してUIへ反映する
            // Trains whose timetable cursor advanced on arrival push a snapshot after simulation so the UI follows
            NotifyTimetableAdvanced();
```

`#region Internal` 内に追加:

```csharp
            void NotifyTimetableAdvanced()
            {
                var notify = ServerContext.GetService<ITrainUnitSnapshotNotifyEvent>();
                foreach (var trainUnit in _trainUnitLookupDatastore.GetRegisteredTrains())
                {
                    if (!trainUnit.trainDiagram.ConsumeCurrentEntryChanged()) continue;
                    notify.NotifySnapshot(trainUnit);
                }
            }
```

`using Game.Context; using Game.Train.Event;` を足す。`ServerContext.GetService` がテスト環境で解決できることは `TrainPlatformItemContainerComponent` の既存テスト（`TrainStationDockingItemTransferTest`）が同じ呼び方で通っていることで担保される。

- [ ] **Step 4: コンパイル・テスト** — `--filter-value "TrainTimetableSnapshotNotifyTest|TrainDiagramAutoRunOperationsTest|TrainSingleTwoStationIntegrationTest"` → PASS
- [ ] **Step 5: コミット** — `git commit -m "feat(train): 時刻表の現在地が進んだ後にsnapshotを通知する"`

---

### Task 6: `va:trainScheduleEdit` プロトコル

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/TrainScheduleEditProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`（`SetTrainPlatformTransferModeProtocol` の登録行の次）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs`（`SetTrainPlatformTransferMode` の次）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/TrainScheduleEditProtocolTest.cs`

**Interfaces:**
- Produces: `TrainScheduleEditProtocol.ProtocolTag = "va:trainScheduleEdit"`
- Produces: `public enum TrainScheduleEditOperation { ReplaceTimetable, SetAutoRun }`
- Produces: `public enum TrainScheduleEditFailureReason { None, TrainNotFound, StationBlockNotFound, NotTrainStation }`
- Produces: `TrainScheduleEditRequest`（Key(2) `TrainUnitInstanceId TrainUnitInstanceId`、Key(3) `TrainScheduleEditOperation Operation`、Key(4) `List<Vector3IntMessagePack> StationPositions`、Key(5) `bool AutoRunEnabled`）と `static CreateReplaceTimetableRequest(TrainUnitInstanceId, IReadOnlyList<Vector3Int>)`／`static CreateSetAutoRunRequest(TrainUnitInstanceId, bool)`
- Produces: `TrainScheduleEditResponse`（Key(2) `bool Success`、Key(3) `TrainScheduleEditFailureReason FailureReason`、Key(4) `TrainScheduleEditOperation Operation`）
- Produces: `VanillaApiWithResponse.SendTrainScheduleEdit(TrainScheduleEditProtocol.TrainScheduleEditRequest request, CancellationToken ct)`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Train.Diagram;
using Game.Train.Unit;
using MessagePack;
using NUnit.Framework;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class TrainScheduleEditProtocolTest
    {
        [Test]
        public void ReplaceTimetableRegistersStationsAndResetsToHead()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            var env = scenario.Environment;
            var stationA = TrainTestHelper.PlaceBlock(env, ForUnitTestModBlockId.TestTrainStation, new Vector3Int(100, 0, 0), BlockDirection.North);
            var stationB = TrainTestHelper.PlaceBlock(env, ForUnitTestModBlockId.TestTrainStation, new Vector3Int(200, 0, 0), BlockDirection.North);

            var response = Send(env, TrainScheduleEditProtocol.TrainScheduleEditRequest.CreateReplaceTimetableRequest(
                scenario.Train.TrainUnitInstanceId, new[] { stationA.BlockPositionInfo.OriginalPos, stationB.BlockPositionInfo.OriginalPos }));

            Assert.IsTrue(response.Success);
            Assert.AreEqual(TrainScheduleEditFailureReason.None, response.FailureReason);
            var entries = scenario.Train.trainDiagram.Entries;
            Assert.AreEqual(2, entries.Count);
            Assert.AreSame(stationA, entries[0].Node.StationRef.StationBlock);
            Assert.AreSame(stationB, entries[1].Node.StationRef.StationBlock);
            Assert.AreEqual(0, scenario.Train.trainDiagram.CurrentIndex);
        }

        [Test]
        public void ReplaceTimetableRejectsPlatformAndKeepsOldTimetable()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            var env = scenario.Environment;
            var platform = TrainTestHelper.PlaceBlock(env, ForUnitTestModBlockId.TestTrainItemPlatform, new Vector3Int(100, 0, 0), BlockDirection.North);
            var before = scenario.Train.trainDiagram.Entries.Count;

            var response = Send(env, TrainScheduleEditProtocol.TrainScheduleEditRequest.CreateReplaceTimetableRequest(
                scenario.Train.TrainUnitInstanceId, new[] { platform.BlockPositionInfo.OriginalPos }));

            Assert.IsFalse(response.Success);
            Assert.AreEqual(TrainScheduleEditFailureReason.NotTrainStation, response.FailureReason);
            Assert.AreEqual(before, scenario.Train.trainDiagram.Entries.Count, "拒否時は時刻表を変えない");
        }

        [Test]
        public void ReplaceTimetableRejectsMissingBlock()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            var response = Send(scenario.Environment, TrainScheduleEditProtocol.TrainScheduleEditRequest.CreateReplaceTimetableRequest(
                scenario.Train.TrainUnitInstanceId, new[] { new Vector3Int(999, 0, 999) }));

            Assert.IsFalse(response.Success);
            Assert.AreEqual(TrainScheduleEditFailureReason.StationBlockNotFound, response.FailureReason);
        }

        [Test]
        public void SecondReplaceWins()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            var env = scenario.Environment;
            var stationA = TrainTestHelper.PlaceBlock(env, ForUnitTestModBlockId.TestTrainStation, new Vector3Int(100, 0, 0), BlockDirection.North);
            var stationB = TrainTestHelper.PlaceBlock(env, ForUnitTestModBlockId.TestTrainStation, new Vector3Int(200, 0, 0), BlockDirection.North);

            Send(env, TrainScheduleEditProtocol.TrainScheduleEditRequest.CreateReplaceTimetableRequest(scenario.Train.TrainUnitInstanceId, new[] { stationA.BlockPositionInfo.OriginalPos }));
            Send(env, TrainScheduleEditProtocol.TrainScheduleEditRequest.CreateReplaceTimetableRequest(scenario.Train.TrainUnitInstanceId, new[] { stationB.BlockPositionInfo.OriginalPos }));

            Assert.AreEqual(1, scenario.Train.trainDiagram.Entries.Count);
            Assert.AreSame(stationB, scenario.Train.trainDiagram.Entries[0].Node.StationRef.StationBlock);
        }

        [Test]
        public void SetAutoRunTogglesTrain()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            var env = scenario.Environment;
            Assert.IsTrue(scenario.Train.IsAutoRun);

            var off = Send(env, TrainScheduleEditProtocol.TrainScheduleEditRequest.CreateSetAutoRunRequest(scenario.Train.TrainUnitInstanceId, false));
            Assert.IsTrue(off.Success);
            Assert.IsFalse(scenario.Train.IsAutoRun);

            var on = Send(env, TrainScheduleEditProtocol.TrainScheduleEditRequest.CreateSetAutoRunRequest(scenario.Train.TrainUnitInstanceId, true));
            Assert.IsTrue(on.Success);
            Assert.IsTrue(scenario.Train.IsAutoRun);
        }

        [Test]
        public void SetAutoRunOnEmptyTimetableIsAcceptedAndTurnsOffOnUpdate()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            scenario.Train.trainDiagram.ReplaceEntries(new List<Game.Train.RailGraph.IRailNode>());

            var response = Send(scenario.Environment, TrainScheduleEditProtocol.TrainScheduleEditRequest.CreateSetAutoRunRequest(scenario.Train.TrainUnitInstanceId, true));

            Assert.IsTrue(response.Success, "サーバーは拒否しない（裁定）");
            scenario.Train.Update();
            Assert.IsFalse(scenario.Train.IsAutoRun);
        }

        [Test]
        public void UnknownTrainIsRejected()
        {
            using var scenario = TrainAutoRunTestScenario.CreateDockedScenario();
            var response = Send(scenario.Environment, TrainScheduleEditProtocol.TrainScheduleEditRequest.CreateSetAutoRunRequest(new TrainUnitInstanceId(System.Guid.NewGuid()), true));

            Assert.IsFalse(response.Success);
            Assert.AreEqual(TrainScheduleEditFailureReason.TrainNotFound, response.FailureReason);
        }

        private static TrainScheduleEditProtocol.TrainScheduleEditResponse Send(TrainTestEnvironment env, TrainScheduleEditProtocol.TrainScheduleEditRequest request)
        {
            var payload = MessagePackSerializer.Serialize(request);
            var responseBytes = env.PacketResponseCreator.GetPacketResponse(payload, new PacketResponseContext(null));
            Assert.AreEqual(1, responseBytes.Count);
            return MessagePackSerializer.Deserialize<TrainScheduleEditProtocol.TrainScheduleEditResponse>(responseBytes[0]);
        }
    }
}
```

`Send` は `SetTrainPlatformTransferModeProtocolTest.SendRequest` と同形（`environment.PacketResponseCreator.GetPacketResponse(payload, new PacketResponseContext(null))`）。`TrainUnitInstanceId` のコンストラクタは UnitGenerator 生成（`new TrainUnitInstanceId(Guid)`）。

- [ ] **Step 2: 実行して失敗を確認** — CS0246（型が無い）

- [ ] **Step 3: プロトコルを実装**

```csharp
using System;
using System.Collections.Generic;
using Game.Context;
using Game.Train.Diagram;
using Game.Train.Event;
using Game.Train.RailGraph;
using Game.Train.Unit;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public enum TrainScheduleEditOperation
    {
        ReplaceTimetable,
        SetAutoRun,
    }

    public enum TrainScheduleEditFailureReason
    {
        None,
        TrainNotFound,
        StationBlockNotFound,
        NotTrainStation,
    }

    // 列車1編成の運行設定（時刻表の丸ごと置換・自動運転ON/OFF）を受ける
    // Accepts per-train operation settings (whole-timetable replacement and auto-run toggle)
    public class TrainScheduleEditProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:trainScheduleEdit";

        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;
        private readonly ITrainUnitSnapshotNotifyEvent _snapshotNotifyEvent;

        public TrainScheduleEditProtocol(ServiceProvider serviceProvider)
        {
            _trainUnitLookupDatastore = serviceProvider.GetService<ITrainUnitLookupDatastore>();
            _snapshotNotifyEvent = serviceProvider.GetService<ITrainUnitSnapshotNotifyEvent>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var request = MessagePackSerializer.Deserialize<TrainScheduleEditRequest>(payload);
            if (!_trainUnitLookupDatastore.TryGetTrainUnit(request.TrainUnitInstanceId, out var train))
            {
                return Reject(request, TrainScheduleEditFailureReason.TrainNotFound, $"train={request.TrainUnitInstanceId}");
            }

            switch (request.Operation)
            {
                case TrainScheduleEditOperation.ReplaceTimetable:
                    return ReplaceTimetable(request, train);
                case TrainScheduleEditOperation.SetAutoRun:
                    return SetAutoRun(request, train);
                default:
                    throw new ArgumentOutOfRangeException();
            }

            #region Internal

            ProtocolMessagePackBase ReplaceTimetable(TrainScheduleEditRequest data, TrainUnit trainUnit)
            {
                // 全駅を先に解決し、1つでも駅でなければ全体を拒否する
                // Resolve every station first; reject the whole request if any entry is not a station
                var nodes = new List<IRailNode>(data.StationPositions.Count);
                foreach (var position in data.StationPositions)
                {
                    var block = ServerContext.WorldBlockDatastore.GetBlock(position.Vector3Int);
                    if (block == null) return Reject(data, TrainScheduleEditFailureReason.StationBlockNotFound, $"pos={position.Vector3Int}");
                    if (!TrainTimetableStationNodeResolver.TryResolve(block, out var node)) return Reject(data, TrainScheduleEditFailureReason.NotTrainStation, $"pos={position.Vector3Int} type={block.BlockMasterElement.BlockType}");
                    nodes.Add(node);
                }

                trainUnit.trainDiagram.ReplaceEntries(nodes);
                trainUnit.trainDiagram.ConsumeCurrentEntryChanged();
                _snapshotNotifyEvent.NotifySnapshot(trainUnit);
                return new TrainScheduleEditResponse(true, TrainScheduleEditFailureReason.None, data.Operation);
            }

            ProtocolMessagePackBase SetAutoRun(TrainScheduleEditRequest data, TrainUnit trainUnit)
            {
                if (data.AutoRunEnabled) trainUnit.TurnOnAutoRun();
                else trainUnit.TurnOffAutoRun();
                _snapshotNotifyEvent.NotifySnapshot(trainUnit);
                return new TrainScheduleEditResponse(true, TrainScheduleEditFailureReason.None, data.Operation);
            }

            ProtocolMessagePackBase Reject(TrainScheduleEditRequest data, TrainScheduleEditFailureReason reason, string detail)
            {
                // 拒否は無音にせず理由をログへ残す
                // Never reject silently; leave the reason in the log
                Debug.LogWarning($"[TrainScheduleEdit] rejected op={data.Operation} reason={reason} {detail}");
                return new TrainScheduleEditResponse(false, reason, data.Operation);
            }

            #endregion
        }

        #region MessagePack

        [MessagePackObject]
        public class TrainScheduleEditRequest : ProtocolMessagePackBase
        {
            [Key(2)] public TrainUnitInstanceId TrainUnitInstanceId { get; set; }
            [Key(3)] public TrainScheduleEditOperation Operation { get; set; }
            [Key(4)] public List<Vector3IntMessagePack> StationPositions { get; set; }
            [Key(5)] public bool AutoRunEnabled { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public TrainScheduleEditRequest() { Tag = ProtocolTag; }

            // Operationごとに必要な項目が違うので生成はstatic factoryに限る
            // Construction goes through static factories because each operation needs different fields
            private TrainScheduleEditRequest(TrainUnitInstanceId trainUnitInstanceId, TrainScheduleEditOperation operation, List<Vector3IntMessagePack> stationPositions, bool autoRunEnabled)
            {
                Tag = ProtocolTag;
                TrainUnitInstanceId = trainUnitInstanceId;
                Operation = operation;
                StationPositions = stationPositions;
                AutoRunEnabled = autoRunEnabled;
            }

            public static TrainScheduleEditRequest CreateReplaceTimetableRequest(TrainUnitInstanceId trainUnitInstanceId, IReadOnlyList<Vector3Int> stationPositions)
            {
                var positions = new List<Vector3IntMessagePack>(stationPositions.Count);
                foreach (var position in stationPositions) positions.Add(new Vector3IntMessagePack(position));
                return new TrainScheduleEditRequest(trainUnitInstanceId, TrainScheduleEditOperation.ReplaceTimetable, positions, false);
            }

            public static TrainScheduleEditRequest CreateSetAutoRunRequest(TrainUnitInstanceId trainUnitInstanceId, bool autoRunEnabled)
            {
                return new TrainScheduleEditRequest(trainUnitInstanceId, TrainScheduleEditOperation.SetAutoRun, new List<Vector3IntMessagePack>(), autoRunEnabled);
            }
        }

        [MessagePackObject]
        public class TrainScheduleEditResponse : ProtocolMessagePackBase
        {
            [Key(2)] public bool Success { get; set; }
            [Key(3)] public TrainScheduleEditFailureReason FailureReason { get; set; }
            [Key(4)] public TrainScheduleEditOperation Operation { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public TrainScheduleEditResponse() { Tag = ProtocolTag; }

            public TrainScheduleEditResponse(bool success, TrainScheduleEditFailureReason failureReason, TrainScheduleEditOperation operation)
            {
                Tag = ProtocolTag;
                Success = success;
                FailureReason = failureReason;
                Operation = operation;
            }
        }

        #endregion
    }
}
```

`Vector3IntMessagePack(Vector3Int)` コンストラクタと `.Vector3Int` プロパティは既存（`SetTrainPlatformTransferModeProtocol` が使用）。200行を超える場合は enum 2つを `TrainScheduleEditTypes.cs` へ分ける。

- [ ] **Step 4: 登録とクライアントAPI**

`PacketResponseCreator.cs` の `SetTrainPlatformTransferModeProtocol` 登録行の次に:
```csharp
            _packetResponseDictionary.Add(TrainScheduleEditProtocol.ProtocolTag, new TrainScheduleEditProtocol(serviceProvider));
```

`VanillaApiWithResponse.cs` の `SetTrainPlatformTransferMode` の次に:
```csharp
        // 列車の時刻表置換・自動運転切替（Requestは呼び出し側がstatic factoryで組む）
        // Train timetable replacement / auto-run toggle (callers build the request via static factories)
        public async UniTask<TrainScheduleEditProtocol.TrainScheduleEditResponse> SendTrainScheduleEdit(
            TrainScheduleEditProtocol.TrainScheduleEditRequest request, CancellationToken ct)
        {
            return await _packetExchangeManager.GetPacketResponse<TrainScheduleEditProtocol.TrainScheduleEditResponse>(request, ct);
        }
```

- [ ] **Step 5: コンパイル・テスト** — `--filter-value "TrainScheduleEditProtocolTest"` → 7件 PASS
- [ ] **Step 6: コミット** — `git commit -m "feat(protocol): va:trainScheduleEdit（時刻表置換・自動運転切替）を追加"`

---

### Task 7: `va:setTrainStationName` プロトコル

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/SetTrainStationNameProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/SetTrainStationNameProtocolTest.cs`

**Interfaces:**
- Produces: `SetTrainStationNameProtocol.ProtocolTag = "va:setTrainStationName"`、`enum SetTrainStationNameFailureReason { None, BlockNotFound, NotTrainStation, EmptyName }`
- Produces: `SetTrainStationNameRequest(Vector3Int position, string stationName)`（Key(2) Position, Key(3) StationName）、`SetTrainStationNameResponse`（Key(2) Success, Key(3) AppliedName, Key(4) FailureReason）
- Produces: `VanillaApiWithResponse.SetTrainStationName(Vector3Int position, string stationName, CancellationToken ct)`
- 名前は `Trim()` して適用。空なら `EmptyName`。長さ上限は設けない。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using MessagePack;
using NUnit.Framework;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class SetTrainStationNameProtocolTest
    {
        [Test]
        public void SetsTrimmedName()
        {
            var env = TrainTestHelper.CreateEnvironment();
            var block = TrainTestHelper.PlaceBlock(env, ForUnitTestModBlockId.TestTrainStation, Vector3Int.zero, BlockDirection.North);

            var response = Send(env, Vector3Int.zero, "  北駅 ");

            Assert.IsTrue(response.Success);
            Assert.AreEqual("北駅", response.AppliedName);
            Assert.AreEqual("北駅", block.GetComponent<TrainStationComponent>().StationName);
        }

        [Test]
        public void RejectsEmptyName()
        {
            var env = TrainTestHelper.CreateEnvironment();
            TrainTestHelper.PlaceBlock(env, ForUnitTestModBlockId.TestTrainStation, Vector3Int.zero, BlockDirection.North);

            var response = Send(env, Vector3Int.zero, "   ");

            Assert.IsFalse(response.Success);
            Assert.AreEqual(SetTrainStationNameProtocol.SetTrainStationNameFailureReason.EmptyName, response.FailureReason);
        }

        [Test]
        public void RejectsPlatformAndMissingBlock()
        {
            var env = TrainTestHelper.CreateEnvironment();
            TrainTestHelper.PlaceBlock(env, ForUnitTestModBlockId.TestTrainItemPlatform, Vector3Int.zero, BlockDirection.North);

            Assert.AreEqual(SetTrainStationNameProtocol.SetTrainStationNameFailureReason.NotTrainStation, Send(env, Vector3Int.zero, "x").FailureReason);
            Assert.AreEqual(SetTrainStationNameProtocol.SetTrainStationNameFailureReason.BlockNotFound, Send(env, new Vector3Int(50, 0, 50), "x").FailureReason);
        }

        private static SetTrainStationNameProtocol.SetTrainStationNameResponse Send(TrainTestEnvironment env, Vector3Int position, string name)
        {
            var request = new SetTrainStationNameProtocol.SetTrainStationNameRequest(position, name);
            var payload = MessagePackSerializer.Serialize(request);
            var responseBytes = env.PacketResponseCreator.GetPacketResponse(payload, new PacketResponseContext(null));
            Assert.AreEqual(1, responseBytes.Count);
            return MessagePackSerializer.Deserialize<SetTrainStationNameProtocol.SetTrainStationNameResponse>(responseBytes[0]);
        }
    }
}
```

- [ ] **Step 2: 実行して失敗を確認** — CS0246

- [ ] **Step 3: 実装**（`SetTrainPlatformTransferModeProtocol.cs` を雛形に）

```csharp
using System;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface.Extension;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class SetTrainStationNameProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:setTrainStationName";

        public SetTrainStationNameProtocol(ServiceProvider serviceProvider)
        {
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var request = MessagePackSerializer.Deserialize<SetTrainStationNameRequest>(payload);
            return Apply(request);

            #region Internal

            ProtocolMessagePackBase Apply(SetTrainStationNameRequest data)
            {
                var name = (data.StationName ?? string.Empty).Trim();
                if (name.Length == 0) return Reject(data, SetTrainStationNameFailureReason.EmptyName);

                var block = ServerContext.WorldBlockDatastore.GetBlock(data.Position.Vector3Int);
                if (block == null) return Reject(data, SetTrainStationNameFailureReason.BlockNotFound);
                if (!block.TryGetComponent<TrainStationComponent>(out var station)) return Reject(data, SetTrainStationNameFailureReason.NotTrainStation);

                station.SetStationName(name);
                return new SetTrainStationNameResponse(true, name, SetTrainStationNameFailureReason.None);
            }

            ProtocolMessagePackBase Reject(SetTrainStationNameRequest data, SetTrainStationNameFailureReason reason)
            {
                // 拒否理由をログへ残す（無音の縮退禁止）
                // Log the rejection reason (no silent fallback)
                Debug.LogWarning($"[SetTrainStationName] rejected reason={reason} pos={data.Position.Vector3Int}");
                return new SetTrainStationNameResponse(false, string.Empty, reason);
            }

            #endregion
        }

        public enum SetTrainStationNameFailureReason
        {
            None,
            BlockNotFound,
            NotTrainStation,
            EmptyName,
        }

        #region MessagePack

        [MessagePackObject]
        public class SetTrainStationNameRequest : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack Position { get; set; }
            [Key(3)] public string StationName { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public SetTrainStationNameRequest() { }

            public SetTrainStationNameRequest(Vector3Int position, string stationName)
            {
                Tag = ProtocolTag;
                Position = new Vector3IntMessagePack(position);
                StationName = stationName;
            }
        }

        [MessagePackObject]
        public class SetTrainStationNameResponse : ProtocolMessagePackBase
        {
            [Key(2)] public bool Success { get; set; }
            [Key(3)] public string AppliedName { get; set; }
            [Key(4)] public SetTrainStationNameFailureReason FailureReason { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public SetTrainStationNameResponse() { }

            public SetTrainStationNameResponse(bool success, string appliedName, SetTrainStationNameFailureReason failureReason)
            {
                Tag = ProtocolTag;
                Success = success;
                AppliedName = appliedName;
                FailureReason = failureReason;
            }
        }

        #endregion
    }
}
```

貨物プラットフォームは `TrainStationComponent` を持たない（`VanillaTrainItemPlatformTemplate` を `grep -n TrainStationComponent` で確認。もし持っていれば `block.BlockMasterElement.BlockType != BlockTypeConst.TrainStation` の判定を先に置く）。

- [ ] **Step 4: 登録とクライアントAPI**

`PacketResponseCreator.cs`: `_packetResponseDictionary.Add(SetTrainStationNameProtocol.ProtocolTag, new SetTrainStationNameProtocol(serviceProvider));`

`VanillaApiWithResponse.cs`:
```csharp
        public async UniTask<SetTrainStationNameProtocol.SetTrainStationNameResponse> SetTrainStationName(
            Vector3Int position, string stationName, CancellationToken ct)
        {
            var request = new SetTrainStationNameProtocol.SetTrainStationNameRequest(position, stationName);
            return await _packetExchangeManager.GetPacketResponse<SetTrainStationNameProtocol.SetTrainStationNameResponse>(request, ct);
        }
```

- [ ] **Step 5: コンパイル・テスト** — `--filter-value "SetTrainStationNameProtocolTest"` → PASS
- [ ] **Step 6: コミット** — `git commit -m "feat(protocol): va:setTrainStationName を追加"`

---

### Task 8: デバッグ経路の撤去（サーバー・クライアント）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Train/Unit/TrainUpdateService.cs`（`_trainAutoRunDebugEnabled`・`IsTrainAutoRunDebugEnabled`・`TurnOnorOffTrainAutoRun` と内部関数を削除。`TrainAutoRunOnArgument` 等の参照も消す）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/CommandProtocol.cs`（`TrainAutoRunCommand`/`TrainAutoRunOnArgument`/`TrainAutoRunOffArgument` 定数と `else if (command[0] == TrainAutoRunCommand)` 分岐を削除。`_trainUpdateService` が他で未使用なら field と `GetService` も削除）
- Modify: `moorestech_client/Assets/Scripts/Client.Common/DebugConst.cs:18-19`（`TrainAutoRunLabel`/`TrainAutoRunKey` 削除）
- Modify: `moorestech_client/Assets/Scripts/Client.DebugSystem/DebugSheet/DebugSheetController.cs:82`（1行削除）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/View/Object/Core/TrainCarEntityObject.cs`（`_isDebugAutoRunInitialized`・`_debugAutoRun`・157行の `UpdateDebugAutoRunCommand();`・`UpdateDebugAutoRunCommand`・`SendTrainAutoRunChanged` を削除。不要になった `using Server.Protocol.PacketResponse;`・`using Client.DebugSystem...` を消す）

- [ ] **Step 1: 削除する**（上記のとおり）
- [ ] **Step 2: 残存参照の確認**

Run: `grep -rn "trainAutoRun\|TrainAutoRunKey\|TrainAutoRunLabel\|ResetAndNotifyNodeAddition\|IsTrainAutoRunDebugEnabled\|TurnOnorOffTrainAutoRun" moorestech_server/Assets/Scripts moorestech_client/Assets/Scripts`
Expected: 0件（`.agents/skills/unity-playmode-recorded-playtest/scenarios/` は `addFuelToAllTrainCarsCommand` しか使っていないことも `grep -rn "trainAutoRun" .agents/skills` で確認）

- [ ] **Step 3: コンパイル** — エラー0。`--filter-value "SendCommandProtocol|CommandProtocol"` の既存テストがあれば PASS
- [ ] **Step 4: コミット** — `git commit -m "chore(train): デバッグ用の全列車自動運転トグルを撤去"`

---

### Task 9: クライアントキャッシュの通知と WebUiHost の DTO・トピック・アクション

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Unit/TrainUnitClientCache.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/TrainTimetableDtos.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/TrainTimetableDtoBuilder.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockInventoryDtos.cs`（`public TrainTimetableDto Timetable; public TrainStationDetailDto TrainStation;` を追加）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/TrainPlatformDetailDtoBuilder.cs`（TrainStation のとき `dto.TrainStation = new TrainStationDetailDto { Name = ... }`）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/TrainInventoryDtoFactory.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockInventoryTopic.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/TrainTimetableActions.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/TrainStationActions.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:186` 付近（`hub.RegisterAction` 3行）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUiHost/TrainTimetableDtoBuilderTest.cs`（`Client.Tests` に WebUiHost の EditMode テスト置き場が無ければ `grep -rln "TrainPlatformDetailDtoBuilder\|BlockInventoryDto" moorestech_client/Assets/Scripts/Client.Tests` で前例の場所に合わせる。無ければ `Client.Tests/WebUiHost/` を作る）

**Interfaces:**
- Produces: `TrainUnitClientCache.OnSnapshotApplied : IObservable<TrainUnitInstanceId>`（`Upsert` と `OverrideAll` の末尾で `OnNext`）
- Produces（wire, camelCase）: `timetable: { trainUnitId: string, isAutoRun: bool, currentIndex: int, stops: [{ position:{x,y,z}, name }], stations: [{ position:{x,y,z}, name }] }`、`trainStation: { name }`
- Produces: `TrainTimetableDtoBuilder.Build(long trainCarInstanceId, TrainUnitClientCache cache, BlockGameObjectDataStore blocks)` → `TrainTimetableDto`（列車が見つからなければ null）
- Produces: アクション `train_timetable.replace` {stations:[{x,y,z}]}、`train_timetable.set_auto_run` {enabled}、`train_station.set_name` {name}

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System.Collections.Generic;
using Client.WebUiHost.Game.Topics.BlockDetail;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUiHost
{
    public class TrainTimetableDtoBuilderTest
    {
        [Test]
        public void StationNameFallsBackToEmptyStringNotNull()
        {
            var dto = TrainTimetableDtoBuilder.CreateStationDto(new Vector3Int(1, 2, 3), null);
            Assert.AreEqual(string.Empty, dto.Name);
            Assert.AreEqual(1, dto.Position.X);
            Assert.AreEqual(2, dto.Position.Y);
            Assert.AreEqual(3, dto.Position.Z);
        }

        [Test]
        public void StationsAreSortedByPositionForStableUi()
        {
            var stations = new List<TrainTimetableStationDto>
            {
                TrainTimetableDtoBuilder.CreateStationDto(new Vector3Int(5, 0, 0), "b"),
                TrainTimetableDtoBuilder.CreateStationDto(new Vector3Int(1, 0, 9), "a"),
                TrainTimetableDtoBuilder.CreateStationDto(new Vector3Int(1, 0, 2), "c"),
            };
            TrainTimetableDtoBuilder.SortStations(stations);
            Assert.AreEqual("c", stations[0].Name);
            Assert.AreEqual("a", stations[1].Name);
            Assert.AreEqual("b", stations[2].Name);
        }
    }
}
```

- [ ] **Step 2: 実行して失敗を確認** — CS0246

- [ ] **Step 3: `TrainUnitClientCache` に通知を足す**

```csharp
        // snapshot適用を表示側（Web UIトピック等）へ知らせる
        // Notify the presentation side (Web UI topics etc.) that a snapshot was applied
        private readonly Subject<TrainUnitInstanceId> _onSnapshotApplied = new();
        public IObservable<TrainUnitInstanceId> OnSnapshotApplied => _onSnapshotApplied;
```
`Upsert` の return 直前で `_onSnapshotApplied.OnNext(trainUnitInstanceId);`、`OverrideAll` の末尾で各 unit について `OnNext`。`using System; using UniRx;` を足す。

- [ ] **Step 4: DTO とビルダー**

`TrainTimetableDtos.cs`:
```csharp
using System.Collections.Generic;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    public class TrainStationPositionDto { public int X; public int Y; public int Z; }

    public class TrainTimetableStationDto
    {
        public TrainStationPositionDto Position;
        public string Name;
    }

    // 列車1編成の時刻表と自動運転状態、選べる駅の一覧
    // One train's timetable, auto-run state, and the selectable station list
    public class TrainTimetableDto
    {
        public string TrainUnitId;
        public bool IsAutoRun;
        public int CurrentIndex;
        public List<TrainTimetableStationDto> Stops;
        public List<TrainTimetableStationDto> Stations;
    }

    public class TrainStationDetailDto { public string Name; }
}
```

`TrainTimetableDtoBuilder.cs`:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Train.Unit;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Train.Unit;
using UnityEngine;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    public static class TrainTimetableDtoBuilder
    {
        public static TrainTimetableDto Build(long trainCarInstanceId, TrainUnitClientCache cache, BlockGameObjectDataStore blocks)
        {
            if (!cache.TryGetCarSnapshot(new TrainCarInstanceId(trainCarInstanceId), out var unit, out _, out _, out _)) return null;

            // ワールド上の駅ブロックを列挙し、駅名は受信済みブロック状態から引く
            // Enumerate station blocks in the world; names come from the received block state
            var stationNames = new Dictionary<Vector3Int, string>();
            var stations = new List<TrainTimetableStationDto>();
            foreach (var block in blocks.BlockGameObjectDictionary.Values)
            {
                if (block.BlockMasterElement.BlockType != BlockTypeConst.TrainStation) continue;
                var name = block.GetStateDetail<TrainStationNameStateDetail>(TrainStationNameStateDetail.BlockStateDetailKey)?.StationName;
                var position = block.BlockPosInfo.OriginalPos;
                stationNames[position] = name ?? string.Empty;
                stations.Add(CreateStationDto(position, name));
            }
            SortStations(stations);

            var stops = new List<TrainTimetableStationDto>(unit.TimetableStops.Count);
            foreach (var stop in unit.TimetableStops)
            {
                stationNames.TryGetValue(stop.StationPosition, out var name);
                stops.Add(CreateStationDto(stop.StationPosition, name));
            }

            return new TrainTimetableDto
            {
                TrainUnitId = unit.TrainUnitInstanceId.ToString(),
                IsAutoRun = unit.IsAutoRun,
                CurrentIndex = unit.TimetableCurrentIndex,
                Stops = stops,
                Stations = stations,
            };
        }

        public static TrainTimetableStationDto CreateStationDto(Vector3Int position, string name)
        {
            return new TrainTimetableStationDto
            {
                Position = new TrainStationPositionDto { X = position.x, Y = position.y, Z = position.z },
                Name = name ?? string.Empty,
            };
        }

        // 座標順で並べ、開くたびに順序が変わらないようにする
        // Sort by position so the list order is stable across opens
        public static void SortStations(List<TrainTimetableStationDto> stations)
        {
            stations.Sort((a, b) =>
            {
                var byX = a.Position.X.CompareTo(b.Position.X);
                if (byX != 0) return byX;
                var byZ = a.Position.Z.CompareTo(b.Position.Z);
                return byZ != 0 ? byZ : a.Position.Y.CompareTo(b.Position.Y);
            });
        }
    }
}
```

`BlockGameObjectDataStore` の `BlockGameObjectDictionary` は `Client.Game/InGame/Block/BlockGameObjectDataStore.cs:18` に公開済み。`TrainCarInstanceId` は UnitOf(long)。`TryGetCarSnapshot` のシグネチャは `TrainUnitClientCache.cs:120`。

`TrainPlatformDetailDtoBuilder.Apply` に追加（`dto.TrainPlatform = ...` の後）:
```csharp
            // 駅だけが名前欄を持つ
            // Only the station carries the name field
            if (param is TrainStationBlockParam)
            {
                var nameState = block.GetStateDetail<TrainStationNameStateDetail>(TrainStationNameStateDetail.BlockStateDetailKey);
                dto.TrainStation = new TrainStationDetailDto { Name = nameState?.StationName ?? string.Empty };
            }
```

- [ ] **Step 5: `TrainInventoryDtoFactory` と `BlockInventoryTopic`**

`TrainInventoryDtoFactory.Create(TrainSubInventorySource source, SubInventoryModel inventory, TrainUnitClientCache cache, BlockGameObjectDataStore blocks)` に引数を2つ足し、`dto.Timetable = TrainTimetableDtoBuilder.Build(source.TrainCarInstanceId, cache, blocks);` を入れる。

`BlockInventoryTopic`: コンストラクタで `var cache = ClientDIContext.DIContainer.DIContainerResolver.Resolve<TrainUnitClientCache>();` を取り field に保持し、
```csharp
            // 列車を開いている間は snapshot 適用のたびに時刻表を再配信する
            // While a train is open, republish the timetable whenever a snapshot is applied
            _trainSnapshotSubscription = cache.OnSnapshotApplied
                .Where(_ => _subInventoryState.CurrentSubInventorySource is TrainSubInventorySource)
                .Subscribe(_ => SchedulePublish());
```
`Dispose` で解除。`BuildJson` の列車分岐を `TrainInventoryDtoFactory.Create(trainSource, sub, _trainUnitClientCache, ClientDIContext.BlockGameObjectDataStore)` に変える。ファイルが200行を超えるなら `BlockInventoryTopic` の `TrackBlock` 部分を `BlockInventoryTrackedBlock.cs` へ切り出す。

- [ ] **Step 6: アクション**

`TrainTimetableActions.cs`（2ハンドラ）:
```csharp
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Cysharp.Threading.Tasks;
using Game.Train.Unit;
using Newtonsoft.Json.Linq;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.WebUiHost.Game.Actions
{
    // 開いている列車の時刻表を丸ごと置き換える
    // Replace the whole timetable of the open train
    public class TrainTimetableReplaceActionHandler : IActionHandler
    {
        public string ActionType => "train_timetable.replace";
        private readonly SubInventoryState _subInventoryState;
        private readonly TrainUnitClientCache _cache;

        public TrainTimetableReplaceActionHandler(SubInventoryState subInventoryState, TrainUnitClientCache cache)
        {
            _subInventoryState = subInventoryState;
            _cache = cache;
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload?["stations"] is not JArray stations) return ActionResult.Fail("invalid_payload");
            if (!TrainTimetableActionSupport.TryResolveOpenTrain(_subInventoryState, _cache, out var trainUnitId)) return ActionResult.Fail("train_not_open");

            var positions = new List<Vector3Int>(stations.Count);
            foreach (var token in stations)
            {
                if (token is not JObject o) return ActionResult.Fail("invalid_station");
                positions.Add(new Vector3Int((int)o["x"], (int)o["y"], (int)o["z"]));
            }

            var request = TrainScheduleEditProtocol.TrainScheduleEditRequest.CreateReplaceTimetableRequest(trainUnitId, positions);
            var response = await ClientContext.VanillaApi.Response.SendTrainScheduleEdit(request, CancellationToken.None);
            if (response == null || !response.Success) return ActionResult.Fail($"replace_failed:{response?.FailureReason}");
            return ActionResult.Success();
        }
    }

    // 開いている列車の自動運転をON/OFFする
    // Toggle auto-run for the open train
    public class TrainTimetableSetAutoRunActionHandler : IActionHandler
    {
        public string ActionType => "train_timetable.set_auto_run";
        private readonly SubInventoryState _subInventoryState;
        private readonly TrainUnitClientCache _cache;

        public TrainTimetableSetAutoRunActionHandler(SubInventoryState subInventoryState, TrainUnitClientCache cache)
        {
            _subInventoryState = subInventoryState;
            _cache = cache;
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload?["enabled"] is not JValue { Type: JTokenType.Boolean } enabled) return ActionResult.Fail("invalid_payload");
            if (!TrainTimetableActionSupport.TryResolveOpenTrain(_subInventoryState, _cache, out var trainUnitId)) return ActionResult.Fail("train_not_open");

            var request = TrainScheduleEditProtocol.TrainScheduleEditRequest.CreateSetAutoRunRequest(trainUnitId, (bool)enabled);
            var response = await ClientContext.VanillaApi.Response.SendTrainScheduleEdit(request, CancellationToken.None);
            if (response == null || !response.Success) return ActionResult.Fail($"set_auto_run_failed:{response?.FailureReason}");
            return ActionResult.Success();
        }
    }

    // 開いている車両インベントリから所属列車IDを引く
    // Resolve the owning train id from the open car inventory
    public static class TrainTimetableActionSupport
    {
        public static bool TryResolveOpenTrain(SubInventoryState state, TrainUnitClientCache cache, out TrainUnitInstanceId trainUnitId)
        {
            trainUnitId = default;
            if (state.CurrentSubInventorySource is not TrainSubInventorySource source) return false;
            if (!cache.TryGetCarSnapshot(new TrainCarInstanceId(source.TrainCarInstanceId), out var unit, out _, out _, out _)) return false;
            trainUnitId = unit.TrainUnitInstanceId;
            return true;
        }
    }
}
```

`TrainStationActions.cs`:
```csharp
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    // 開いている駅ブロックの駅名を設定する
    // Set the name of the open station block
    public class TrainStationSetNameActionHandler : IActionHandler
    {
        public string ActionType => "train_station.set_name";
        private readonly SubInventoryState _subInventoryState;

        public TrainStationSetNameActionHandler(SubInventoryState subInventoryState)
        {
            _subInventoryState = subInventoryState;
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload?["name"] is not JValue { Type: JTokenType.String } name) return ActionResult.Fail("invalid_payload");
            if (_subInventoryState.CurrentSubInventorySource is not BlockSubInventorySource source) return ActionResult.Fail("block_not_open");
            if (source.BlockTypeName != "TrainStation") return ActionResult.Fail("invalid_block_type");

            var response = await ClientContext.VanillaApi.Response.SetTrainStationName(source.BlockPosition, (string)name, CancellationToken.None);
            if (response == null || !response.Success) return ActionResult.Fail($"set_name_failed:{response?.FailureReason}");
            return ActionResult.Success();
        }
    }
}
```

`WebUiGameBinder.Bind` の `TrainPlatformSetTransferModeActionHandler` 登録の次に:
```csharp
            var trainUnitClientCache = resolver.Resolve<TrainUnitClientCache>();
            hub.RegisterAction(new TrainTimetableReplaceActionHandler(subInventoryState, trainUnitClientCache));
            hub.RegisterAction(new TrainTimetableSetAutoRunActionHandler(subInventoryState, trainUnitClientCache));
            hub.RegisterAction(new TrainStationSetNameActionHandler(subInventoryState));
```

`Client.WebUiHost.asmdef` が `Client.Game`・`Game.Block`・`Game.Train`・`Core.Master` を参照していることを `grep -n '"' moorestech_client/Assets/Scripts/Client.WebUiHost/Client.WebUiHost.asmdef` で確認し、不足があれば追加する。

- [ ] **Step 7: コンパイル・テスト** — `uloop compile` エラー0、`--filter-value "TrainTimetableDtoBuilderTest"` PASS
- [ ] **Step 8: コミット** — `git commit -m "feat(webui-host): 時刻表DTOの配信と時刻表・駅名アクションを追加"`

---

### Task 10: Web UI 契約・i18n・PanelTabs（新様式）

**Files:**
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/inventory.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/payloadTypes.ts`
- Modify: `moorestech_web/webui/src/bridge/transport/actionContract.ts`
- Modify: `Localization/localization.csv`（末尾に追加）＋ `pnpm gen:i18n`
- Create: `moorestech_web/webui/src/shared/ui/PanelTabs/index.tsx`、`style.module.css`、`index.test.ts`
- Modify: `moorestech_web/webui/src/shared/ui/index.ts`
- Modify: `.agents/skills/webui-design/SKILL.md`（§8.22 追加）
- Modify: `moorestech_web/webui/e2e/mock-host/fixtures.ts`

**Interfaces:**
- Produces（TS）: `TrainStationPositionSchema`、`TrainTimetableStationSchema`、`TrainTimetableDataSchema`、`TrainStationDetailSchema`；`TrainInventoryOpen.timetable?`、`BlockInventoryOpen.trainStation?`；型 `TrainTimetableData`、`TrainTimetableStation`
- Produces（actions）: `"train_timetable.replace": { stations: { x: number; y: number; z: number }[] }`、`"train_timetable.set_auto_run": { enabled: boolean }`、`"train_station.set_name": { name: string }`
- Produces: `PanelTabs({ value, tabs: { value, label, testId? }[], onChange, testId })` — `data-selected` を持つ `button` の横並び。`ModeSwitch` と同じトークン面
- Produces（i18n キー、`L.ui.blockInventory.*`）: `trainTabInventory`、`trainTabTimetable`、`timetableAutoRun`、`timetableAutoRunOn`、`timetableAutoRunOff`、`timetableStops`、`timetableStopsEmpty`、`timetableStations`、`timetableStationsEmpty`、`timetableAdd`、`timetableApply`、`timetableMoveUp`、`timetableMoveDown`、`timetableRemove`、`timetableStationLabel`（"{name} ({x}, {y}, {z})"）、`timetableUnnamedStation`（"駅"）、`stationNameLabel`、`stationNamePlaceholder`、`stationNameApply`

- [ ] **Step 1: 失敗するテスト（PanelTabs）**

```ts
// PanelTabsの選択状態とクリック契約を検証する
// Verifies PanelTabs selection state and click contract
import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import PanelTabs from "./index";

describe("PanelTabs", () => {
  it("選択中タブをdata-selectedで公開しクリックで値を返す", () => {
    const onChange = vi.fn();
    const renderer = create(createElement(PanelTabs, {
      value: "timetable",
      tabs: [
        { value: "inventory", label: "inv", testId: "tab-inventory" },
        { value: "timetable", label: "tt", testId: "tab-timetable" },
      ],
      onChange,
      testId: "tabs",
    }));
    const buttons = renderer.root.findAllByType("button");
    expect(buttons[0].props["data-selected"]).toBeUndefined();
    expect(buttons[1].props["data-selected"]).toBe("true");
    act(() => buttons[0].props.onClick());
    expect(onChange).toHaveBeenCalledWith("inventory");
  });
});
```

- [ ] **Step 2: 実行して失敗** — `cd moorestech_web/webui && pnpm vitest run src/shared/ui/PanelTabs` → モジュール無しで FAIL

- [ ] **Step 3: PanelTabs 実装**

`index.tsx`:
```tsx
// パネル内のビュー切替タブ。ModeSwitchと同じ面・トークンで、選択中はdata-selectedで示す
// In-panel view tabs; same face/tokens as ModeSwitch, selected tab exposed via data-selected
import type { ReactNode } from "react";
import styles from "./style.module.css";

export type PanelTab = { value: string; label: ReactNode; testId?: string };

type Props = {
  value: string;
  tabs: PanelTab[];
  onChange: (value: string) => void;
  testId?: string;
};

export default function PanelTabs({ value, tabs, onChange, testId }: Props) {
  return (
    <div className={styles.root} role="tablist" data-testid={testId}>
      {tabs.map((tab) => (
        <button
          key={tab.value}
          className={styles.tab}
          type="button"
          role="tab"
          aria-selected={tab.value === value}
          data-selected={tab.value === value ? "true" : undefined}
          data-testid={tab.testId}
          onClick={() => onChange(tab.value)}
        >
          {tab.label}
        </button>
      ))}
    </div>
  );
}
```

`style.module.css`:
```css
/* タブ列。ModeSwitchの溝面を横並びにし、選択タブだけ明るい寒色面にする */
/* Tab row: ModeSwitch track faces side by side, only the selected tab gets the brighter cool face */
.root {
  display: flex;
  flex-direction: row;
  gap: var(--mode-switch-gap);
  margin-bottom: var(--mode-switch-padding-block);
}

.tab {
  flex: 1 1 auto;
  padding: var(--mode-switch-padding-block) var(--mode-switch-padding-inline);
  color: var(--text-muted);
  font: inherit;
  cursor: pointer;
  border: 0;
  border-radius: var(--bevel-1);
  background: var(--gauge-track);
  box-shadow: 0 0 0 var(--bevel-1) var(--bevel-c1);
}

.tab[data-selected="true"] {
  color: var(--text-high-contrast);
  background: color-mix(in srgb, var(--gauge-track), var(--bevel-c2) var(--mode-switch-selected-mix));
  box-shadow: 0 0 0 var(--bevel-1) var(--bevel-c2);
}

.tab:focus-visible {
  outline: var(--bevel-1) solid var(--text-high-contrast);
  outline-offset: var(--bevel-1);
}
```

`shared/ui/index.ts` に `export { default as PanelTabs, type PanelTab } from "./PanelTabs";` を追加。

- [ ] **Step 4: webui-design §8.22 を追記**（§8.21 の後、§9 の前）

```markdown
## 8.22 パネル内タブ（`shared/ui/PanelTabs`）

- 1つのパネルに同じ対象の別ビュー（例: 列車の「インベントリ / 時刻表」）を持たせるときだけ使う。別対象・別画面をタブで束ねない。
- 見た目は ModeSwitch と同族（`--gauge-track` の溝面、選択中は `--bevel-c2` の明るい寒色面と `--text-high-contrast`）。新しい色相・下線・アニメーションは足さない。
- タブ列はパネル本文の最上段に置き、`--mode-switch-gap` で並べる。高さはパネルに比例させない。
- `data-selected="true"` と `role="tab"` を公開する。testid は `<feature>-tab-<name>`。
- 出所: ユーザー裁定 2026-09-24「タブを新設する」（`.decisions/2026-09-24-列車インベントリに時刻表タブを新設する.md`）。
```

- [ ] **Step 5: 契約**

`schemas/inventory.ts` の `TrainInventoryOpenSchema` の前に:
```ts
export const TrainStationPositionSchema = z.object({ x: z.number().int(), y: z.number().int(), z: z.number().int() });
export const TrainTimetableStationSchema = z.object({ position: TrainStationPositionSchema, name: z.string() });
export const TrainTimetableDataSchema = z.object({
  trainUnitId: z.string(),
  isAutoRun: z.boolean(),
  currentIndex: z.number().int(),
  stops: z.array(TrainTimetableStationSchema),
  stations: z.array(TrainTimetableStationSchema),
});
export const TrainStationDetailSchema = z.object({ name: z.string() });
```
`BlockInventoryOpenSchema` に `trainStation: TrainStationDetailSchema.optional(),`、`TrainInventoryOpenSchema` に `timetable: TrainTimetableDataSchema.optional(),` を追加。`payloadTypes.ts` に `export type TrainTimetableData = z.infer<typeof TrainTimetableDataSchema>; export type TrainTimetableStation = z.infer<typeof TrainTimetableStationSchema>;` と import を追加。

`actionContract.ts` の `ActionPayloads` に3行、`as const satisfies` の配列に3要素（`"train_platform.set_transfer_mode"` の次）:
```ts
  "train_timetable.replace": { stations: { x: number; y: number; z: number }[] };
  "train_timetable.set_auto_run": { enabled: boolean };
  "train_station.set_name": { name: string };
```

- [ ] **Step 6: i18n**

`Localization/localization.csv` 末尾に追加（列: key,Source,english,japanese,german）:
```csv
ui.blockInventory.trainTabInventory,Inventory,Inventory,インベントリ,Inventar
ui.blockInventory.trainTabTimetable,Timetable,Timetable,時刻表,Fahrplan
ui.blockInventory.timetableAutoRun,Auto run,Auto run,自動運転,Automatik
ui.blockInventory.timetableAutoRunOn,ON,ON,ON,AN
ui.blockInventory.timetableAutoRunOff,OFF,OFF,OFF,AUS
ui.blockInventory.timetableStops,Stops,Stops,停車駅,Haltestellen
ui.blockInventory.timetableStopsEmpty,No stops. Add a station below.,No stops. Add a station below.,停車駅がありません。下の駅一覧から追加してください。,Keine Halte. Unten eine Station hinzufügen.
ui.blockInventory.timetableStations,Stations,Stations,駅一覧,Stationen
ui.blockInventory.timetableStationsEmpty,No stations in the world.,No stations in the world.,ワールドに駅がありません。,Keine Stationen in der Welt.
ui.blockInventory.timetableAdd,Add,Add,追加,Hinzufügen
ui.blockInventory.timetableApply,Apply,Apply,適用,Anwenden
ui.blockInventory.timetableMoveUp,Move up,Move up,上へ,Nach oben
ui.blockInventory.timetableMoveDown,Move down,Move down,下へ,Nach unten
ui.blockInventory.timetableRemove,Remove,Remove,削除,Entfernen
ui.blockInventory.timetableStationLabel,"{name} ({x}, {y}, {z})","{name} ({x}, {y}, {z})","{name} ({x}, {y}, {z})","{name} ({x}, {y}, {z})"
ui.blockInventory.timetableUnnamedStation,Station,Station,駅,Station
ui.blockInventory.stationNameLabel,Station name,Station name,駅名,Stationsname
ui.blockInventory.stationNamePlaceholder,Enter a station name,Enter a station name,駅名を入力,Stationsname eingeben
ui.blockInventory.stationNameApply,Set,Set,決定,Übernehmen
```
`cd moorestech_web/webui && pnpm gen:i18n` を実行し `localizationKeys.ts` を再生成。Unity側は `Client.Localization/_CompileRequester.cs` が CSV 変更で再生成される（メモリ: 触っていないキーの CS0117 が出たら force-recompile）。

- [ ] **Step 7: mock fixture**

`fixtures.ts` の `trainCargo` の次に:
```ts
export const trainWithTimetable = {
  open: true,
  source: "train",
  blockType: "Train",
  identifier: "train:103",
  itemSlots: Array.from({ length: 10 }, empty),
  fluidSlots: [],
  timetable: {
    trainUnitId: "unit-1",
    isAutoRun: false,
    currentIndex: 0,
    stops: [{ position: { x: 12, y: 0, z: 40 }, name: "北駅" }],
    stations: [
      { position: { x: -8, y: 0, z: 3 }, name: "" },
      { position: { x: 12, y: 0, z: 40 }, name: "北駅" },
    ],
  },
} satisfies BlockInventoryWireData;
```

- [ ] **Step 8: テスト** — `pnpm vitest run src/shared/ui/PanelTabs src/shared/i18n` → PASS（freshness 含む）、`pnpm tsc -b` → エラー0
- [ ] **Step 9: コミット** — `git commit -m "feat(webui): 時刻表の契約・i18n・PanelTabs様式を追加"`

---

### Task 11: 時刻表タブと駅名欄の React 実装

**Files:**
- Create: `moorestech_web/webui/src/features/blockInventory/train/timetableEditLogic.ts`
- Create: `moorestech_web/webui/src/features/blockInventory/train/timetableEditLogic.test.ts`
- Create: `moorestech_web/webui/src/features/blockInventory/train/TrainInventoryBody.tsx`
- Create: `moorestech_web/webui/src/features/blockInventory/train/TrainTimetableSection.tsx`
- Create: `moorestech_web/webui/src/features/blockInventory/train/TrainTimetableStopList.tsx`
- Create: `moorestech_web/webui/src/features/blockInventory/train/style.module.css`
- Create: `moorestech_web/webui/src/features/blockInventory/details/TrainStationNameSection.tsx`
- Modify: `moorestech_web/webui/src/features/blockInventory/views/TrainPlatformInventory.tsx`（`<TrainStationNameSection data={data} />` を先頭に）
- Modify: `moorestech_web/webui/src/features/blockInventory/BlockInventoryPanel.tsx`（train 分岐を `<TrainInventoryBody data={data} />` に置換）
- Modify: `moorestech_web/webui/src/features/blockInventory/blockInventoryDesign.test.ts`（sources に4ファイルを追加）

**Interfaces:**
- Produces: `timetableEditLogic.ts`
  - `type StationKey = string`（`"x,y,z"`）、`stationKey(position)`
  - `type TimetableDraft = { stops: TrainTimetableStation[] }`
  - `addStop(draft, station)`、`removeStop(draft, index)`、`moveStop(draft, index, delta)`（範囲外は同じ draft を返す）
  - `canEnableAutoRun(stops)`（`stops.length > 0`）
  - `stationLabel(t, station)`（名前が空なら `timetableUnnamedStation`）
  - `toReplacePayload(draft)` → `{ stations: [{x,y,z}] }`
- testid: `train-tabs`、`train-tab-inventory`、`train-tab-timetable`、`train-timetable-auto-run`（ModeSwitch）、`train-timetable-auto-run-on`／`-off`（option）、`train-timetable-stops`、`train-timetable-stop-<i>`（`data-current`）、`train-timetable-stop-<i>-up`／`-down`／`-remove`、`train-timetable-stations`、`train-timetable-station-<x>_<y>_<z>-add`、`train-timetable-apply`、`train-station-name-input`、`train-station-name-apply`

- [ ] **Step 1: 失敗するテスト（ロジック）**

```ts
import { describe, expect, it } from "vitest";
import { addStop, canEnableAutoRun, moveStop, removeStop, stationKey, toReplacePayload } from "./timetableEditLogic";

const a = { position: { x: 1, y: 0, z: 1 }, name: "A" };
const b = { position: { x: 2, y: 0, z: 2 }, name: "" };

describe("timetableEditLogic", () => {
  it("追加・削除・上下移動は新しいdraftを返し元を変えない", () => {
    const d0 = { stops: [] };
    const d1 = addStop(d0, a);
    const d2 = addStop(d1, b);
    expect(d0.stops).toHaveLength(0);
    expect(d2.stops.map((s) => stationKey(s.position))).toEqual(["1,0,1", "2,0,2"]);
    expect(moveStop(d2, 1, -1).stops[0]).toBe(b);
    expect(moveStop(d2, 0, -1)).toBe(d2);
    expect(moveStop(d2, 1, 1)).toBe(d2);
    expect(removeStop(d2, 0).stops).toEqual([b]);
  });

  it("同じ駅を2回入れられる（循環で2度停まる時刻表を許す）", () => {
    const d = addStop(addStop({ stops: [] }, a), a);
    expect(d.stops).toHaveLength(2);
  });

  it("停車駅が無いと自動運転ONにできない", () => {
    expect(canEnableAutoRun([])).toBe(false);
    expect(canEnableAutoRun([a])).toBe(true);
  });

  it("適用ペイロードは座標だけを送る", () => {
    expect(toReplacePayload({ stops: [a, b] })).toEqual({ stations: [{ x: 1, y: 0, z: 1 }, { x: 2, y: 0, z: 2 }] });
  });
});
```

- [ ] **Step 2: 実行して失敗** — `pnpm vitest run src/features/blockInventory/train` → FAIL

- [ ] **Step 3: ロジック実装**

```ts
// 時刻表タブのローカル編集。サーバーへは「適用」で丸ごと送る
// Local editing for the timetable tab; the whole list is sent on "Apply"
import type { TrainTimetableStation } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";

export type TimetableDraft = { stops: TrainTimetableStation[] };
export type StationPosition = TrainTimetableStation["position"];

export function stationKey(position: StationPosition): string {
  return `${position.x},${position.y},${position.z}`;
}

export function addStop(draft: TimetableDraft, station: TrainTimetableStation): TimetableDraft {
  return { stops: [...draft.stops, station] };
}

export function removeStop(draft: TimetableDraft, index: number): TimetableDraft {
  if (index < 0 || index >= draft.stops.length) return draft;
  return { stops: draft.stops.filter((_, i) => i !== index) };
}

export function moveStop(draft: TimetableDraft, index: number, delta: -1 | 1): TimetableDraft {
  const target = index + delta;
  if (index < 0 || index >= draft.stops.length || target < 0 || target >= draft.stops.length) return draft;
  const stops = [...draft.stops];
  [stops[index], stops[target]] = [stops[target], stops[index]];
  return { stops };
}

// 空の時刻表ではONにしない（サーバーは受理して即OFFに戻すため、UI側で塞ぐ裁定）
// Never enable on an empty timetable (the server accepts then immediately turns it off, so the UI blocks it)
export function canEnableAutoRun(stops: readonly TrainTimetableStation[]): boolean {
  return stops.length > 0;
}

export type Translator = ReturnType<typeof useI18n>["t"];

export function stationLabel(t: Translator, station: TrainTimetableStation): string {
  const name = station.name.length > 0 ? station.name : t(L.ui.blockInventory.timetableUnnamedStation);
  return t(L.ui.blockInventory.timetableStationLabel, { name, x: station.position.x, y: station.position.y, z: station.position.z });
}

export function toReplacePayload(draft: TimetableDraft): { stations: StationPosition[] } {
  return { stations: draft.stops.map((s) => ({ x: s.position.x, y: s.position.y, z: s.position.z })) };
}
```

`t` は `i18nStore.ts:169` の `useI18n()` が返す `createTranslator` の関数（第2引数に補間値）。型は `ReturnType<typeof useI18n>["t"]` で受ける。

- [ ] **Step 4: コンポーネント**

`TrainInventoryBody.tsx`:
```tsx
// 列車の車両インベントリ本文。インベントリ / 時刻表をPanelTabsで切り替える（§8.22）
// Train car inventory body; PanelTabs switches between inventory and timetable (§8.22)
import { useState } from "react";
import type { BlockInventoryData } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { PanelTabs } from "@/shared/ui";
import BlockItemGrid from "../BlockItemGrid";
import TrainTimetableSection from "./TrainTimetableSection";

type TrainData = Extract<BlockInventoryData, { source: "train" }>;
type Tab = "inventory" | "timetable";

export default function TrainInventoryBody({ data }: { data: TrainData }) {
  const { t } = useI18n();
  const [tab, setTab] = useState<Tab>("inventory");
  const tabs = [
    { value: "inventory", label: t(L.ui.blockInventory.trainTabInventory), testId: "train-tab-inventory" },
    { value: "timetable", label: t(L.ui.blockInventory.trainTabTimetable), testId: "train-tab-timetable" },
  ];
  return (
    <>
      <PanelTabs value={tab} tabs={tabs} onChange={(v) => setTab(v as Tab)} testId="train-tabs" />
      {tab === "inventory" && <BlockItemGrid itemSlots={data.itemSlots} testId="train-inventory-slots" />}
      {tab === "timetable" && data.timetable && <TrainTimetableSection key={data.timetable.trainUnitId} timetable={data.timetable} />}
    </>
  );
}
```

`TrainTimetableSection.tsx`:
```tsx
// 時刻表タブ。ローカルdraftを編集し「適用」で丸ごと送る。自動運転は即送信
// Timetable tab: edit a local draft and send the whole list on Apply; auto-run is sent immediately
import { useState } from "react";
import { Stack, Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import type { TrainTimetableData } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { ModeSwitch, PanelActionButton } from "@/shared/ui";
import TrainTimetableStopList from "./TrainTimetableStopList";
import { addStop, canEnableAutoRun, moveStop, removeStop, stationKey, stationLabel, toReplacePayload, type TimetableDraft } from "./timetableEditLogic";
import styles from "./style.module.css";

export default function TrainTimetableSection({ timetable }: { timetable: TrainTimetableData }) {
  const { t } = useI18n();
  // 適用前に閉じたら破棄される（コンポーネントのアンマウントで消える）
  // Discarded when closed before Apply (state dies with the component)
  const [draft, setDraft] = useState<TimetableDraft>({ stops: timetable.stops });
  const autoRunOptions = [
    { value: "on", label: t(L.ui.blockInventory.timetableAutoRunOn), testId: "train-timetable-auto-run-on", disabled: !canEnableAutoRun(timetable.stops) },
    { value: "off", label: t(L.ui.blockInventory.timetableAutoRunOff), testId: "train-timetable-auto-run-off" },
  ];
  return (
    <Stack gap="xs" data-testid="train-timetable-section">
      <div className={styles.row}>
        <Text size="sm">{t(L.ui.blockInventory.timetableAutoRun)}</Text>
        <ModeSwitch
          testId="train-timetable-auto-run"
          value={timetable.isAutoRun ? "on" : "off"}
          options={autoRunOptions}
          onChange={(v) => { void dispatchAction("train_timetable.set_auto_run", { enabled: v === "on" }); }}
        />
      </div>
      <Text size="sm">{t(L.ui.blockInventory.timetableStops)}</Text>
      <TrainTimetableStopList
        stops={draft.stops}
        currentIndex={timetable.currentIndex}
        onMove={(i, d) => setDraft((prev) => moveStop(prev, i, d))}
        onRemove={(i) => setDraft((prev) => removeStop(prev, i))}
      />
      <Text size="sm">{t(L.ui.blockInventory.timetableStations)}</Text>
      <div className={styles.list} data-testid="train-timetable-stations">
        {timetable.stations.length === 0 && <Text size="sm">{t(L.ui.blockInventory.timetableStationsEmpty)}</Text>}
        {timetable.stations.map((station) => (
          <div className={styles.row} key={stationKey(station.position)}>
            <span>{stationLabel(t, station)}</span>
            <PanelActionButton
              testId={`train-timetable-station-${stationKey(station.position).replace(/,/g, "_")}-add`}
              onClick={() => setDraft((prev) => addStop(prev, station))}
            >
              {t(L.ui.blockInventory.timetableAdd)}
            </PanelActionButton>
          </div>
        ))}
      </div>
      <PanelActionButton testId="train-timetable-apply" onClick={() => { void dispatchAction("train_timetable.replace", toReplacePayload(draft)); }}>
        {t(L.ui.blockInventory.timetableApply)}
      </PanelActionButton>
    </Stack>
  );
}
```

`TrainTimetableStopList.tsx`:
```tsx
// 停車駅リスト。現在向かっている行をdata-currentで示し、↑↓×で並べ替え・削除する
// Stop list; the row being headed to carries data-current, with up/down/remove controls
import { Text } from "@mantine/core";
import type { TrainTimetableStation } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { PanelActionButton } from "@/shared/ui";
import { stationLabel } from "./timetableEditLogic";
import styles from "./style.module.css";

type Props = {
  stops: TrainTimetableStation[];
  currentIndex: number;
  onMove: (index: number, delta: -1 | 1) => void;
  onRemove: (index: number) => void;
};

export default function TrainTimetableStopList({ stops, currentIndex, onMove, onRemove }: Props) {
  const { t } = useI18n();
  if (stops.length === 0) return <Text size="sm" data-testid="train-timetable-stops">{t(L.ui.blockInventory.timetableStopsEmpty)}</Text>;
  return (
    <ol className={styles.list} data-testid="train-timetable-stops">
      {stops.map((stop, i) => (
        <li className={styles.row} key={`${i}-${stop.position.x}-${stop.position.z}`} data-testid={`train-timetable-stop-${i}`} data-current={i === currentIndex ? "true" : undefined}>
          <span>{`${i + 1}. ${stationLabel(t, stop)}`}</span>
          <span className={styles.controls}>
            <PanelActionButton testId={`train-timetable-stop-${i}-up`} onClick={() => onMove(i, -1)}>{t(L.ui.blockInventory.timetableMoveUp)}</PanelActionButton>
            <PanelActionButton testId={`train-timetable-stop-${i}-down`} onClick={() => onMove(i, 1)}>{t(L.ui.blockInventory.timetableMoveDown)}</PanelActionButton>
            <PanelActionButton testId={`train-timetable-stop-${i}-remove`} onClick={() => onRemove(i)}>{t(L.ui.blockInventory.timetableRemove)}</PanelActionButton>
          </span>
        </li>
      ))}
    </ol>
  );
}
```

ボタン文言は ESLint ルール `no-jsx-visible-literal` が JSX の可視リテラル（「↑」等の記号も含む）を禁じるため、すべて i18n キー経由にする。行番号の `${i + 1}.` はテンプレートリテラル（式）なので対象外。

`style.module.css`:
```css
/* 行は左に名前・右に操作。寸法は固定長トークンだけを使う */
/* Rows put the label left and controls right; sizes use fixed-length tokens only */
.list {
  display: flex;
  flex-direction: column;
  gap: var(--mode-switch-gap);
  margin: 0;
  padding: 0;
  list-style: none;
}

.row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--mode-switch-padding-inline);
}

.row[data-current="true"] {
  color: var(--text-high-contrast);
}

.controls {
  display: flex;
  gap: var(--mode-switch-gap);
}

.nameInput {
  background: var(--gauge-track);
  border: var(--bevel-1) solid var(--bevel-c1);
  color: var(--text-default);
  padding: var(--mode-switch-padding-block) var(--mode-switch-padding-inline);
  font-family: inherit;
}

.nameInput::placeholder {
  color: var(--text-muted);
}

.nameInput:focus-visible {
  outline: var(--bevel-1) solid var(--text-high-contrast);
  outline-offset: var(--bevel-1);
}
```

`details/TrainStationNameSection.tsx`:
```tsx
// 駅ブロックの名前欄（§8.9の素input）。「決定」で送信し、表示の正本はブロック状態
// Station name field (bare input per §8.9); sent on "Set", block state remains the display source of truth
import { useState } from "react";
import { Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import type { BlockInventoryOpen } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { PanelActionButton } from "@/shared/ui";
import styles from "../train/style.module.css";

export default function TrainStationNameSection({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  const detail = data.trainStation;
  const [name, setName] = useState(detail?.name ?? "");
  if (!detail) return null;
  return (
    <div className={styles.row} data-testid="train-station-name-section">
      <Text size="sm">{t(L.ui.blockInventory.stationNameLabel)}</Text>
      <input
        className={styles.nameInput}
        type="text"
        value={name}
        placeholder={t(L.ui.blockInventory.stationNamePlaceholder)}
        onChange={(e) => setName(e.currentTarget.value)}
        data-testid="train-station-name-input"
      />
      <PanelActionButton testId="train-station-name-apply" onClick={() => { void dispatchAction("train_station.set_name", { name }); }}>
        {t(L.ui.blockInventory.stationNameApply)}
      </PanelActionButton>
    </div>
  );
}
```

`TrainPlatformInventory.tsx` の `<Stack>` 先頭に `<TrainStationNameSection data={data} />` を足す（`trainStation` が無い PF では null を返す）。

`BlockInventoryPanel.tsx`: `{data.source === "train" && !trainError && <BlockItemGrid .../>}` を `{data.source === "train" && !trainError && <TrainInventoryBody key={data.identifier} data={data} />}` に置き換え、import を足す。

`blockInventoryDesign.test.ts` の `sources` に `trainInventoryBody: read("./train/TrainInventoryBody.tsx")`、`trainTimetable: read("./train/TrainTimetableSection.tsx")`、`trainTimetableStops: read("./train/TrainTimetableStopList.tsx")`、`trainStationName: read("./details/TrainStationNameSection.tsx")` を追加し、`styles` に `panelTabs: read("../../shared/ui/PanelTabs/style.module.css")`、`trainTimetable: read("./train/style.module.css")` を追加。

- [ ] **Step 5: テスト・型・ビルド**

Run: `cd moorestech_web/webui && pnpm vitest run src/features/blockInventory src/shared` → PASS（design whitelist・noJsxVisibleLiteral・i18n freshness を含む）
Run: `pnpm tsc -b && pnpm build` → 成功（`dist` 更新）

- [ ] **Step 6: mock-host で目視QA（webui-design §0.2）** — `MOCK_PORT` と `MOORESTECH_VITE_PORT` をセッション固有に振り、`/__block` で `trainWithTimetable` を流して時刻表タブ・停車駅ハイライト・駅一覧の追加・適用を Playwright スクショで確認する（`e2e/capture-*.ts` の前例に倣い `e2e/capture-train-timetable.ts` を作ってよい）。スクショを `docs/superpowers/plans/` には置かず PR 説明に添付する。
- [ ] **Step 7: コミット** — `git commit -m "feat(webui): 列車インベントリに時刻表タブと駅名欄を追加"`

---

### Task 12: ランタイムプレイテスト（録画）

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/train/train-timetable-via-ui.cs`

**Interfaces:**
- Consumes: Task 11 の testid、Task 6/7 のプロトコル、`train-run-hash-check.cs` の駅・機関車設置手順

- [ ] **Step 1: シナリオを書く**

```csharp
// 時刻表UI経路の通し検証: 駅2基・レール・機関車を置き、Fで車両を開き時刻表タブから2駅を追加→適用→自動運転ON→走行→OFF→停止。
// 駅名はサーバー直で付け、時刻表タブの表示に名前が出ることを確認する（CEFへキー入力は転送されないため入力欄の打鍵はしない）。
// Timetable UI end-to-end: place two stations, rails and a locomotive, open the car with F, add both stations from the
// timetable tab, apply, turn auto-run on, watch it move, turn it off and watch it stop. Station names are set server-side
// and verified in the tab (keystrokes are not forwarded to CEF, so the name input is not typed).
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Operations;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.UnlockState;
using UnityEngine;
using TrainCarEntityObject = Client.Game.InGame.Train.View.Object.Core.TrainCarEntityObject;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("train-timetable-via-ui", options, async p =>
{
    var mismatchCount = 0;
    var rejectCount = 0;
    Application.logMessageReceived += CountWarnings;
    try
    {
        await p.SetupFlatGround();
        p.WarpPlayer(new Vector3(16f, 33.5f, 10f));
        await p.SkipOpeningSkit();

        // ===== 駅2基とレール =====
        // ===== Two stations and rails =====
        p.Note("駅2基を直設置し、A出口→B入口、B出口→A入口を結線する");
        var stationAPos = new Vector3Int(10, 32, 4);
        var stationBPos = new Vector3Int(10, 32, 64);
        p.PlaceBlockDirect("蒸気機関車駅", stationAPos, BlockDirection.North);
        p.PlaceBlockDirect("蒸気機関車駅", stationBPos, BlockDirection.North);
        await p.WaitBlockGameObject(stationAPos);
        await p.WaitBlockGameObject(stationBPos);
        var stationA = p.GetBlock(stationAPos);
        var stationB = p.GetBlock(stationBPos);
        var railsA = stationA.GetComponents<RailComponent>();
        var railsB = stationB.GetComponents<RailComponent>();
        p.Assert(railsA.Count == 2 && railsB.Count == 2, "駅は2本のRailComponentを持つ");
        // Exit(front)→相手Entry(front) を張ると逆方向 back 側も自動で張られる（docs/train/TrainSystemNotes.md）
        // Connecting exit(front)→other entry(front) also creates the reverse back-side edge (docs/train/TrainSystemNotes.md)
        RailComponent ExitOf(List<RailComponent> rails) => rails.First(r => r.FrontNode.StationRef.NodeRole == StationNodeRole.Exit);
        RailComponent EntryOf(List<RailComponent> rails) => rails.First(r => r.FrontNode.StationRef.NodeRole == StationNodeRole.Entry);
        ExitOf(railsA).FrontNode.ConnectNode(EntryOf(railsB).FrontNode);
        ExitOf(railsB).FrontNode.ConnectNode(EntryOf(railsA).FrontNode);
        stationA.GetComponent<TrainStationComponent>().SetStationName("北駅");
        stationB.GetComponent<TrainStationComponent>().SetStationName("南駅");

        // ===== 機関車 =====
        // ===== Locomotive =====
        p.Note("機関車をアンロックし駅Aの構内へ設置、燃料投入");
        var resolver = ClientDIContext.DIContainer.DIContainerResolver;
        var clientUnlockState = resolver.Resolve<IGameUnlockStateData>();
        var carMaster = MasterHolder.TrainUnitMaster.Train.TrainCars.First(c => 0 < c.TractionForce);
        p.ServerService<IGameUnlockStateDataController>().UnlockTrainCar(carMaster.TrainCarGuid);
        await p.Until(() => clientUnlockState.TrainCarUnlockStateInfos.TryGetValue(carMaster.TrainCarGuid, out var info) && info.IsUnlocked, 10f, "車両アンロック同期");
        foreach (var required in carMaster.RequiredItems) p.GiveItemDirect(MasterHolder.ItemMaster.GetItemMaster(required.ItemGuid).Name, required.Count);
        var trainLength = TrainLengthConverter.ToRailUnits(carMaster.Length);
        var exitA = ExitOf(railsA).FrontNode;
        var entryA = EntryOf(railsA).FrontNode;
        var railPosition = new RailPosition(new List<IRailNode> { exitA, entryA }, trainLength, 0);
        var placed = await ClientContext.VanillaApi.Response.PlaceTrainOnRail(railPosition, carMaster.TrainCarGuid, CancellationToken.None);
        p.Assert(placed != null && placed.Success, $"車両設置成功 (failure={placed?.FailureType})");
        await p.Until(() => SpawnedCar() != null, 15f, "車両entity出現");
        p.SendCommand("addFuelToAllTrainCarsCommand");
        await p.WaitSeconds(1f);
        var train = p.ServerService<ITrainUnitLookupDatastore>().GetRegisteredTrains().First();
        p.Assert(train.trainDiagram.Entries.Count == 0 && !train.IsAutoRun, "初期は時刻表が空で自動運転OFF");

        // ===== UI: 時刻表を設定 =====
        // ===== UI: configure the timetable =====
        p.Note("車両をFで開き時刻表タブへ");
        var car = SpawnedCar();
        p.WarpPlayer(car.transform.position + new Vector3(0f, 1.5f, -3f));
        await p.AimAt(car.transform.position);
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        await p.ClickWebUi("train-tab-timetable");
        await p.UntilWebUiElement("train-timetable-section", 10f);
        await PlaytestWebUiOps.WaitWebUiTextContains("train-timetable-stations", "北駅", 10f);
        await PlaytestWebUiOps.WaitWebUiTextContains("train-timetable-stations", "南駅", 10f);
        await p.Screenshot("01-timetable-tab");

        p.Note("北駅・南駅を追加して適用");
        await p.ClickWebUi($"train-timetable-station-{stationAPos.x}_{stationAPos.y}_{stationAPos.z}-add");
        await p.ClickWebUi($"train-timetable-station-{stationBPos.x}_{stationBPos.y}_{stationBPos.z}-add");
        await p.UntilWebUiElement("train-timetable-stop-1", 5f);
        await p.ClickWebUi("train-timetable-apply");
        await p.Until(() => train.trainDiagram.Entries.Count == 2, 10f, "サーバーの時刻表が2駅になる");
        p.Assert(ReferenceEquals(train.trainDiagram.Entries[0].Node.StationRef.StationBlock, stationA), "1駅目は北駅");
        p.Assert(ReferenceEquals(train.trainDiagram.Entries[1].Node.StationRef.StationBlock, stationB), "2駅目は南駅");
        await p.Screenshot("02-timetable-applied");

        // ===== UI: 自動運転ON → 走行 =====
        // ===== UI: auto-run on → moving =====
        p.Note("自動運転ONで出発を待つ");
        await p.ClickWebUi("train-timetable-auto-run-on");
        await p.Until(() => train.IsAutoRun, 10f, "サーバーの自動運転がON");
        await p.CloseWebUiPanel();
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
        var before = SpawnedCar().transform.position;
        await p.Until(() => 0.0 < train.CurrentSpeed, 15f, "列車が加速開始（1秒待機後に発車）");
        await p.WaitSeconds(3f);
        await p.Screenshot("03-running");
        var moved = (SpawnedCar().transform.position - before).magnitude;
        p.Assert(1f < moved, $"クライアント車両が動いた (moved={moved:F1})");

        // ===== UI: 自動運転OFF → 停止 =====
        // ===== UI: auto-run off → stopped =====
        p.Note("車両を開き直して自動運転OFF");
        car = SpawnedCar();
        p.WarpPlayer(car.transform.position + new Vector3(0f, 1.5f, -3f));
        await p.AimAt(car.transform.position);
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        await p.ClickWebUi("train-tab-timetable");
        await p.ClickWebUi("train-timetable-auto-run-off");
        await p.Until(() => !train.IsAutoRun, 10f, "サーバーの自動運転がOFF");
        await p.CloseWebUiPanel();
        await p.Until(() => train.CurrentSpeed <= 0.0, 30f, "惰性停止");
        await p.Screenshot("04-stopped");

        p.Assert(mismatchCount == 0, $"Hash mismatch警告0件 (count={mismatchCount})");
        p.Assert(rejectCount == 0, $"時刻表・駅名プロトコルの拒否ログ0件 (count={rejectCount})");
        await p.Screenshot("final");
    }
    finally
    {
        Application.logMessageReceived -= CountWarnings;
    }

    #region Internal

    void CountWarnings(string condition, string stackTrace, LogType type)
    {
        if (condition.Contains("Hash mismatch detected")) mismatchCount++;
        if (condition.Contains("[TrainScheduleEdit] rejected") || condition.Contains("[SetTrainStationName] rejected")) rejectCount++;
    }

    TrainCarEntityObject SpawnedCar() => UnityEngine.Object.FindObjectsByType<TrainCarEntityObject>(FindObjectsSortMode.None).FirstOrDefault();

    #endregion
});
```

`p.PlaceBlockDirect` は同期で `IBlock` を返す（`PlaytestDriver.cs:80`）。`p.AimAt(Vector3)` は世界座標（`PlaytestDriver.cs:141`）。`RailNode.ConnectNode(RailNode targetNode, int distance=-1)` の1引数呼びは `train-run-hash-check.cs` と同じ。駅の構内で機関車が入りきるか（駅長 vs 機関車長20）は `stationA.BlockPositionInfo.BlockSize.z` を `p.Note` で出し、入らなければ `railPosition` を駅Aの手前レール上（`train-run-hash-check.cs` と同じく橋脚を足す）に置く。

- [ ] **Step 2: Web UI をビルドして実行**

```bash
cd moorestech_web/webui && pnpm build && cd ../..
uloop control-play-mode --project-path ./moorestech_client --action stop
SKILL=.claude/skills/unity-playmode-recorded-playtest
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/train/train-timetable-via-ui.cs"
```
Expected: `result.json` の全 Assert が true、録画と `01`〜`04` のスクショが `moorestech_client/PlaytestResults/train-timetable-via-ui/` に出る。失敗したら `troubleshooting.md` の順で切り分ける（マスタピンの worktree 未作成→`git -C ../moorestech_master worktree add`）。

- [ ] **Step 3: 拒否ログの独立確認**

Run: `grep -E "rejected|mismatch|lost|refus|fall(ing)? back|orphan" moorestech_client/PlaytestResults/train-timetable-via-ui/*.log | head` → 0行（期待語ではなくシステムの警告語で引く）

- [ ] **Step 4: コミット** — `git add .agents/skills/unity-playmode-recorded-playtest/scenarios/train/train-timetable-via-ui.cs && git commit -m "test(playtest): 時刻表UI経路の通しシナリオを追加"`。録画・スクショはコミットしない（PR 説明に添付）。

---

### Task 13: 閉じタスク — 全ブランチレビュー・e2e 再実施・残課題の起票

- [ ] **Step 1: moores-code-review を全ブランチに対して実行する**（省略不可・自動実行）。指摘の反映がソース（判定経路・条件式・評価時点）に触れたら、Task 12 のシナリオを反映後のバイナリで再実施してから完了とする。
- [ ] **Step 2: 全テスト** — `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Train|SetTrainStationName"`、`cd moorestech_web/webui && pnpm vitest run && pnpm tsc -b` → すべて PASS
- [ ] **Step 3: 残課題の起票** — plan・e2e 記録・レビューで「未検証」「未確認」「残差」と書いたものを1件ずつ `bd create --parent moorestech-lc9ty` で起票し、結論に issue 番号を列挙する。少なくとも次を起票する:
  - 「転送完了」出発条件の新設（ADR 0067 で先送り）
  - プレイテストでのキーボード入力転送（`CefInputForwarder` が文字キーを転送しないため駅名入力の UI 経路が録画できない）
  - 時刻表の到達不能理由の UI 表示
- [ ] **Step 4: bd close** — `bd close moorestech-lc9ty --reason="PR #<番号>"`、pr-create スキルで PR を作成し、`moores-wt rm train-timetable-ui` で worktree と Editor を畳む。

---

## 判断記録（ADR）

- 設計ADR: `docs/adr/0067-train-timetable-ui-and-per-train-auto-run.md`（ユーザー裁定11件・agent前提7件）。裁定台帳: `.decisions/2026-09-24-*.md` 8本。用語: `CONTEXT.md`「列車の運行」。
- **到着後の snapshot 通知は `TrainUpdateService.UpdateTrains` の post-sim 位置で行う（TrainUnit.Update 内では行わない）。** 出所: agent前提（`UpdateTrains` の「snapshot,生成イベント系はこれ以降」コメント。`NotifySnapshot` は `ResetDiff` を呼ぶため tick 差分の集計前に呼ぶと差分を落とす）
- **時刻表の現在地変化は `TrainDiagram` のフラグ（`ConsumeCurrentEntryChanged`）で検知する。** 出所: agent前提（UniRx 購読でもよいが、消費者が `TrainUpdateService` 1つで tick 同期が要るため、tick 内で読んで下ろすフラグの方が発火時点が明確）
- **駅の初期名は空文字、UI は「駅 (x, y, z)」で補う。** 出所: agent前提（"test" 固定は表示に不適。既存セーブの "test" はそのまま）
- **駅一覧は座標順（X→Z→Y）で並べる。** 出所: agent前提（開くたびに順序が変わらないため）
- **同じ駅を時刻表に2回入れられる。** 出所: agent前提（循環で同駅に2度停まる運用を妨げない。サーバーも重複を禁じていない）
- **プレイテストでは駅名をサーバー直で付け、UI 入力欄の打鍵は検証しない。** 出所: agent前提（`CefInputForwarder` は修飾キーしか CEF へ合成せず文字入力が届かない。R14 は PacketTest と vitest で担保）
- **`TrainDiagram.cs` の保存/復元を `TrainDiagramSaveDataConverter` へ分離。** 出所: agent前提（200行規約。294行→分割）
- **Web UI の駅名欄は `PanelActionButton`「決定」で送信（onBlur 送信にしない）。** 出所: agent前提（意図しない送信を避ける。§8.9 の素 input 様式）

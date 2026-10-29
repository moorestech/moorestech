# UITK 画面状態の専用アセンブリ Client.Ui.State への分離 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 段階 0（PR #1559）で `Client.WebUiHost` から `Client.Game` へ移した画面状態 13 ファイルを、新設アセンブリ `Client.Ui.State` へ移し、`Client.Game` が画面状態を持たない形にする（挙動は変えない移動）。

**Architecture:** 参照の向きは `Client.Game` ← `Client.Ui.State` ← {`Client.WebUiHost`・`Client.Starter`・`Client.Tests`}（段階 1 で作る `Client.Ui` もここを参照する）。13 ファイルは `git mv` で `.meta` ごと移し（GUID を保つ）、名前空間を `Client.Game.InGame.*` から `Client.Ui.State.*` へ変え、参照側の `using` を張り替える。DI 登録（`MainGameUiStateRegistration`）の中身・登録順は変えない。移行計画 `docs/uitk-migration/plan.md` の「ViewModel は `Client.Game` に置く」を書き換える。

**Tech Stack:** Unity / C#、asmdef、VContainer、UniRx、NUnit（EditMode・EditModeInPlayingTest）、uloop

## Requirements

1. **R1 アセンブリ新設**: `moorestech_client/Assets/Scripts/Client.Ui.State/Client.Ui.State.asmdef` を作る。受け入れ: `Client.Game.asmdef` に `Client.Ui.State` への参照が無く、`Client.Ui.State.asmdef` は `Client.Game` を参照する。コンパイル Error 0
2. **R2 13 ファイルの移動**: 下の「移動表」の 13 ファイルを `.meta` ごと移し、名前空間を表のとおりにする。受け入れ: `git diff -M --name-status` で 13 件が R（rename）として出る。`grep -rn "Client.Game.InGame.EventMode\|Client.Game.InGame.UI.Modal\|Client.Game.InGame.UI.Research\|Client.Game.InGame.UI.BlockDetail" moorestech_client/Assets/Scripts` が 0 件
3. **R3 参照側の張り替え**: 移した型を使う `Client.Starter`・`Client.WebUiHost`・`Client.Tests` のファイル（「参照側の一覧」）の `using` を新しい名前空間へ替え、3 つの asmdef に `Client.Ui.State` を足す。受け入れ: コンパイル Error 0
4. **R4 空になったディレクトリの片付け**: 移動で空になる `Client.Game/InGame/EventMode`・`Client.Game/InGame/UI/BlockDetail`・`Client.Game/InGame/UI/Modal`・`Client.Game/InGame/UI/Research` をディレクトリの `.meta` ごと消す。受け入れ: 4 ディレクトリとその `.meta` が無い
5. **R5 uGUI 監査ゲートの追従**: `WebUiGateClassification` から空になった 3 ディレクトリのルールを消し、`Client.Ui.State` を走査対象と分類（`Infra`）に足す。受け入れ: `Client.Tests.WebUi.Gate.WebUiGateAuditTest` が全 PASS（腐敗ルール検出 `AllRulesMatchExistingFiles` を含む）
6. **R6 挙動不変**: 移動と `using`・asmdef・分類表の変更だけにする。メソッド本体・DI 登録・Web へ届く topic の中身は変えない。受け入れ: 「検証の対象テスト」が全 PASS（Web ホストを起動しない EditModeInPlayingTest `WebUiHostlessStateAvailabilityTest` を含む）
7. **R7 計画文書の改訂**: `docs/uitk-migration/plan.md` の §2.1「0-4 状態の移設」・§3.1・§3.2 を `Client.Ui.State` 前提へ書き換える。受け入れ: plan.md 内に「ViewModel・状態を `Client.Game` に置く」趣旨の記述が残らない
8. **R8 統合 PR への取り込み**: 作業ブランチを統合ブランチ `feature/uitk-stage0` へマージコミットで取り込み push し、PR #1559 の本文の「取り込み済み」表に 1 行足す。受け入れ: `gh pr view 1559` の head がマージコミットを指し、CI が全 pass

**やらないこと**
- 段階 0 より前から `Client.Game` にある画面寄りのクラス（`UIStateControl`・`BlueprintNameInputState`・`MouseCursorTooltipState`・`ClientLocalNotificationSource`・`ChallengeManager`・`ClientTrainTimetableDatastore` など）は動かさない。ゲームロジックから呼ばれており、外へ出すには依存の反転が要る別の仕事のため。bd `moorestech-albof.14` で追う。帰結: それまでは `Client.Game` に画面状態が残り続ける
- UITK 側 `Client.Ui` の asmdef は作らない（段階 1 の最初の PR で作る）
- クラス名・メソッドの中身・コメントの書き換えはしない（名前空間行と `using` 行だけ）

## Global Constraints

- 作業場所: worktree `/Users/sakastudio/hermes-agent/data/worktrees/moorestech/uitk-ui-state-assembly`、ブランチ `refactor/uitk-ui-state-assembly`（`origin/feature/uitk-stage0` の `ffab18e83a` から作成済み）
- `.meta` は手で作らない。移動は `git mv` で既存 `.meta` を一緒に動かす。新しい asmdef とディレクトリの `.meta` は Unity が作ったものをコミットする
- 1 ファイル 200 行未満、1 ディレクトリ 10 ファイルまで（`.meta` を除く）
- `partial`・`Func<>`・デフォルト引数は使わない
- Unity Editor はマシン全体で 2 本まで。起動前に `moores-wt status` で本数を見る。他セッションの Editor は止めない
- 名前空間はディレクトリと一致させる（`Client.Ui.State/<機能>/X.cs` → `namespace Client.Ui.State.<機能>`）
- 実行ホストは pi agent（gpt-6.1-sol）。pi 本体と moores-code-review の Codex runner は同じ Codex 週次枠を使う（2026-10-29 18:24 時点で 53%）。枠が尽きたら作業ブランチを push して止まる。Codex のクレジットは使わない（承諾なし）

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反1件・修正1件 / asmdef に Game.Block.Interface が無く CS0246 で落ちる点を修正。注目点（Step 4 の using の言い切り・Editor 停止・分類の説明文・Nuget 除去）も反映
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強1・弱0・第3バケツ0 / 裁定記録の「Client.Game は画面状態を知らない」と既存クラスの据え置きの食い違い（ユーザー裁定へ）

---

## 移動表

パスは `moorestech_client/Assets/Scripts/` からの相対。各 `.cs` の `.meta` も同じ先へ移す。

| # | 元 | 先 | 新しい名前空間 |
|---|---|---|---|
| 1 | `Client.Game/InGame/EventMode/EventLanguageGate.cs` | `Client.Ui.State/EventMode/EventLanguageGate.cs` | `Client.Ui.State.EventMode` |
| 2 | `Client.Game/InGame/Train/Timetable/OpenTrainTimetableTracker.cs` | `Client.Ui.State/Train/OpenTrainTimetableTracker.cs` | `Client.Ui.State.Train` |
| 3 | `Client.Game/InGame/Train/Timetable/OpenTrainUnitResolver.cs` | `Client.Ui.State/Train/OpenTrainUnitResolver.cs` | `Client.Ui.State.Train` |
| 4 | `Client.Game/InGame/Train/Timetable/TrainTimetableFetcher.cs` | `Client.Ui.State/Train/TrainTimetableFetcher.cs` | `Client.Ui.State.Train` |
| 5 | `Client.Game/InGame/UI/BlockDetail/BlockNetworkInfoCache.cs` | `Client.Ui.State/BlockDetail/BlockNetworkInfoCache.cs` | `Client.Ui.State.BlockDetail` |
| 6 | `Client.Game/InGame/UI/BlockDetail/OpenBlockDetailTracker.cs` | `Client.Ui.State/BlockDetail/OpenBlockDetailTracker.cs` | `Client.Ui.State.BlockDetail` |
| 7 | `Client.Game/InGame/UI/Blueprint/BlueprintNameInputModalBridge.cs` | `Client.Ui.State/Blueprint/BlueprintNameInputModalBridge.cs` | `Client.Ui.State.Blueprint` |
| 8 | `Client.Game/InGame/UI/Challenge/ChallengeProgressState.cs` | `Client.Ui.State/Challenge/ChallengeProgressState.cs` | `Client.Ui.State.Challenge` |
| 9 | `Client.Game/InGame/UI/Modal/ModalRequest.cs` | `Client.Ui.State/Modal/ModalRequest.cs` | `Client.Ui.State.Modal` |
| 10 | `Client.Game/InGame/UI/Modal/ModalRequestService.cs` | `Client.Ui.State/Modal/ModalRequestService.cs` | `Client.Ui.State.Modal` |
| 11 | `Client.Game/InGame/UI/Notification/NotificationFeed.cs` | `Client.Ui.State/Notification/NotificationFeed.cs` | `Client.Ui.State.Notification` |
| 12 | `Client.Game/InGame/UI/Notification/SequencedNotification.cs` | `Client.Ui.State/Notification/SequencedNotification.cs` | `Client.Ui.State.Notification` |
| 13 | `Client.Game/InGame/UI/Research/ResearchNodeStateStore.cs` | `Client.Ui.State/Research/ResearchNodeStateStore.cs` | `Client.Ui.State.Research` |

時刻表の 3 ファイルは、`Client.Game/InGame/Train/Timetable` に残る `ClientTrainTimetableDatastore`・`TrainTimetableEventHandler`（サーバー状態の同期でゲーム側の持ち物）と別の名前空間になる。移した側は `using Client.Game.InGame.Train.Timetable;` が要るなら足す。

## 参照側の一覧

移した型を使っていて `using` の張り替えが要るファイル（`moorestech_client/Assets/Scripts/` 相対）。古い名前空間の `using` は、そのファイルが同じ名前空間に残る型（`BlueprintNameInputState`・`ChallengeManager`・`ClientLocalNotificationSource`・`ClientTrainTimetableDatastore` 等）を使っていなければ消す。

- Client.Starter: `EventMode/EventModeStartGate.cs`、`Registration/MainGameUiStateRegistration.cs`
- Client.WebUiHost: `Game/Actions/EventMode/EventLanguageGateActions.cs`、`Game/Actions/FilterSplitterActions.cs`、`Game/Actions/ModalActions.cs`、`Game/Actions/ResearchActions.cs`、`Game/Actions/Train/TrainTimetableActions.cs`、`Game/Actions/Train/TrainTimetableOpenActionHandler.cs`、`Game/Actions/Train/TrainWebUiActionRegistration.cs`、`Game/EventMode/EventLanguageGateBinder.cs`、`Game/EventMode/EventLanguageGateTopic.cs`、`Game/Topics/BlockDetail/BlockDetailDtoBuilder.cs`、`Game/Topics/BlockDetail/Train/TrainTimetableDtoBuilder.cs`、`Game/Topics/BlockInventoryTopic.cs`、`Game/Topics/Challenge/ChallengeCurrentTopic.cs`、`Game/Topics/Challenge/ChallengeTreeTopic.cs`、`Game/Topics/Inventory/BlockInventoryDtoFactory.cs`、`Game/Topics/ModalTopic.cs`、`Game/Topics/Notification/NotificationTopic.cs`、`Game/Topics/Research/ResearchTopic.cs`、`Game/Topics/TrainInventoryDtoFactory.cs`、`Game/WebUiGameBinder.cs`
- Client.Tests: `EditModeInPlayingTest/Hostless/{BlockDetailStateProbe,ChallengeStateProbe,ModalStateProbe,NotificationStateProbe,ResearchStateProbe,TimetableStateProbe}.cs`、`EventMode/{EventLanguageGateTest,EventLanguageGateTopicTest,EventModeStartGateTest}.cs`、`UIState/Models/BlueprintNameInputModalBridgeTest.cs`、`WebUi/BlockDetail/{FluidBlockDetailDtoBuilderTest,GearToElectricDetailDtoBuilderTest}.cs`、`WebUi/Challenge/ChallengeProgressStateTest.cs`、`WebUi/WireContractNotificationTest.cs`、`WebUiHost/Train/TrainTimetableFetcherTest.cs`

触らない（古い名前空間の `using` は残る型のために使われている）: `Client.Starter/Initialization/MainGameInitializationFinalizer.cs`、`Client.Starter/MainGameStarter.cs`、`Client.Starter/Registration/MainGameContainerActivation.cs`、`Client.Starter/Registration/MainGameInteractionRegistration.cs`、`Client.Starter/Registration/MainGameModelRegistration.cs`、`Client.Tests/PlaceSystem/Blueprint/Copy/BlueprintCreateFailureNotifierTest.cs`、`Client.Tests/UIState/CameraPolicy/UiStateCameraPolicyServiceTest.cs`、`Client.Tests/UIState/Models/BlueprintNameInputStateTest.cs`、`Client.Tests/WebUiHost/Train/TrainTimetableDatastoreNotificationTest.cs`。コンパイルでこれらに型の未解決が出たら、そのファイルにも `using Client.Ui.State.<機能>;` を足す。

一覧は `ffab18e83a` 時点の grep から作った。着手時に次で漏れを確かめる:

```bash
cd moorestech_client/Assets/Scripts
grep -rlE "\b(EventLanguageGate|EventLanguageSelectionResult|OpenTrainTimetableTracker|OpenTrainUnitResolver|TrainTimetableFetcher|BlockNetworkInfoCache|OpenBlockDetailTracker|BlueprintNameInputModalBridge|ChallengeProgressState|ModalRequest|ModalRequestService|ModalVariant|ModalResult|NotificationFeed|SequencedNotification|ResearchNodeStateStore)\b" --include="*.cs" . | grep -v "^./Client.Ui.State/" | sort
```

## 検証の対象テスト

```
^Client\.Tests\.(EventMode|WebUi|WebUiHost|UIState|EditModeInPlayingTest\.Hostless)\.
```

`EditModeInPlayingTest.Hostless.WebUiHostlessStateAvailabilityTest` は PlayMode に入るのでドメインリロードが起きる。実行後に「Unity is reloading」が出たら 45 秒待ってから次のコマンドを打つ。

---

### Task 1: Client.Ui.State を新設し、画面状態 13 ファイルを移す

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Client.Ui.State.asmdef`
- Move: 移動表の 13 ファイル（各 `.meta` を含む）
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/EventMode.meta`、`moorestech_client/Assets/Scripts/Client.Game/InGame/UI/BlockDetail.meta`、`moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Modal.meta`、`moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Research.meta`（と空になったディレクトリ）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Client.Starter.asmdef`、`moorestech_client/Assets/Scripts/Client.WebUiHost/Client.WebUiHost.asmdef`、`moorestech_client/Assets/Scripts/Client.Tests/Tests.asmdef`
- Modify: 「参照側の一覧」の 37 ファイル（`using` 行だけ）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/Gate/WebUiGateClassification.cs`

**Interfaces:**
- Consumes: なし
- Produces: アセンブリ `Client.Ui.State`。公開型は移動前と同じ名前・同じシグネチャで、名前空間だけが移動表の「新しい名前空間」になる（例: `Client.Ui.State.Modal.ModalRequestService`、`Client.Ui.State.EventMode.EventLanguageGate`）

- [x] **Step 1: 位置と Editor の本数を確かめる**

```bash
pwd
git -C /Users/sakastudio/hermes-agent/data/worktrees/moorestech/uitk-ui-state-assembly log --oneline -1
moores-wt status
```
Expected: HEAD は `ffab18e83a` かその子孫（このplanのコミット）。このworktreeの Editor は動いていない（`editor=` が付いていない。動いたまま Step 3 の移動をすると Unity が `.meta` を作り直す恐れがある）。マシン全体の Editor は自分の分を足して 2 本以下

- [x] **Step 2: asmdef を作る**

`moorestech_client/Assets/Scripts/Client.Ui.State/Client.Ui.State.asmdef`:

```json
{
    "name": "Client.Ui.State",
    "rootNamespace": "",
    "references": [
        "Client.Game",
        "Client.Localization",
        "Client.Network",
        "Core.Master",
        "Game.Block.Interface",
        "Game.Research",
        "Game.Train",
        "Server.Event",
        "Server.Protocol",
        "Server.Util",
        "MessagePack",
        "UniRx",
        "UniTask",
        "VContainer"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

`Game.Block.Interface` は `TrainUnitInstanceId`（名前空間は `Game.Train.Unit` だが置き場はこのアセンブリ）・`BlockInstanceId`・`BlockPositionInfo` のために要る。コンパイルで `CS0012`（型が参照されていないアセンブリで定義されている）か `CS0246`（型が見つからない）が出たら、エラー文に出たアセンブリ名を `Client.Game.asmdef` の `references` から探して、この配列に足す。`Client.Game.asmdef` に無いものは足さない（足す必要が出たら移動ではなく設計の問題なので止めて報告する）

- [x] **Step 3: 13 ファイルを `.meta` ごと移す**

```bash
cd /Users/sakastudio/hermes-agent/data/worktrees/moorestech/uitk-ui-state-assembly/moorestech_client/Assets/Scripts
mkdir -p Client.Ui.State/{EventMode,Train,BlockDetail,Blueprint,Challenge,Modal,Notification,Research}
mvpair() { git mv "$1" "$2"; git mv "$1.meta" "$2.meta"; }
mvpair Client.Game/InGame/EventMode/EventLanguageGate.cs Client.Ui.State/EventMode/EventLanguageGate.cs
mvpair Client.Game/InGame/Train/Timetable/OpenTrainTimetableTracker.cs Client.Ui.State/Train/OpenTrainTimetableTracker.cs
mvpair Client.Game/InGame/Train/Timetable/OpenTrainUnitResolver.cs Client.Ui.State/Train/OpenTrainUnitResolver.cs
mvpair Client.Game/InGame/Train/Timetable/TrainTimetableFetcher.cs Client.Ui.State/Train/TrainTimetableFetcher.cs
mvpair Client.Game/InGame/UI/BlockDetail/BlockNetworkInfoCache.cs Client.Ui.State/BlockDetail/BlockNetworkInfoCache.cs
mvpair Client.Game/InGame/UI/BlockDetail/OpenBlockDetailTracker.cs Client.Ui.State/BlockDetail/OpenBlockDetailTracker.cs
mvpair Client.Game/InGame/UI/Blueprint/BlueprintNameInputModalBridge.cs Client.Ui.State/Blueprint/BlueprintNameInputModalBridge.cs
mvpair Client.Game/InGame/UI/Challenge/ChallengeProgressState.cs Client.Ui.State/Challenge/ChallengeProgressState.cs
mvpair Client.Game/InGame/UI/Modal/ModalRequest.cs Client.Ui.State/Modal/ModalRequest.cs
mvpair Client.Game/InGame/UI/Modal/ModalRequestService.cs Client.Ui.State/Modal/ModalRequestService.cs
mvpair Client.Game/InGame/UI/Notification/NotificationFeed.cs Client.Ui.State/Notification/NotificationFeed.cs
mvpair Client.Game/InGame/UI/Notification/SequencedNotification.cs Client.Ui.State/Notification/SequencedNotification.cs
mvpair Client.Game/InGame/UI/Research/ResearchNodeStateStore.cs Client.Ui.State/Research/ResearchNodeStateStore.cs
git rm -q Client.Game/InGame/EventMode.meta Client.Game/InGame/UI/BlockDetail.meta Client.Game/InGame/UI/Modal.meta Client.Game/InGame/UI/Research.meta
rmdir Client.Game/InGame/EventMode Client.Game/InGame/UI/BlockDetail Client.Game/InGame/UI/Modal Client.Game/InGame/UI/Research
```
Expected: `rmdir` が成功する（空でなければ移し漏れがあるので表と突き合わせる）

- [x] **Step 4: 移したファイルの名前空間を替える**

各ファイルの `namespace` 行を移動表の「新しい名前空間」へ替える。例 `Client.Ui.State/Modal/ModalRequestService.cs`:

```csharp
namespace Client.Ui.State.Modal
```

移したファイル同士の参照に合わせて `using` も替える:
- `Client.Ui.State/Blueprint/BlueprintNameInputModalBridge.cs`: `using Client.Game.InGame.UI.Modal;` → `using Client.Ui.State.Modal;`。`BlueprintNameInputState` を使っているので `using Client.Game.InGame.UI.Blueprint;` を足す
- `Client.Ui.State/Train/TrainTimetableFetcher.cs`・`OpenTrainTimetableTracker.cs`: 同じ名前空間に入るので互いの `using` は不要。`IClientTrainTimetableLookup`・`IClientTrainTimetableMutator`（`ClientTrainTimetableDatastore.cs` で定義、Client.Game に残る）を使うので `using Client.Game.InGame.Train.Timetable;` を足す
- `Client.Ui.State/Notification/NotificationFeed.cs`: Client.Game に残る `ClientLocalNotificationSource` を使うので `using Client.Game.InGame.UI.Notification;` を足す
- `Client.Ui.State/BlockDetail/*.cs`・`Client.Ui.State/Challenge/ChallengeProgressState.cs`・`Client.Ui.State/Research/ResearchNodeStateStore.cs`: 元の名前空間に暗黙で頼っていた型があれば（コンパイルで `CS0246` が出たら）、その型の名前空間の `using` を足す

- [x] **Step 5: 参照側の asmdef と `using` を張り替える**

3 つの asmdef（`Client.Starter.asmdef`・`Client.WebUiHost.asmdef`・`Tests.asmdef`）の `references` 配列の末尾に `"Client.Ui.State"` を足す。

「参照側の一覧」の各ファイルで、移した型の名前空間を新しいものへ替える。対応:

| 古い `using` | 新しい `using` |
|---|---|
| `Client.Game.InGame.EventMode` | `Client.Ui.State.EventMode` |
| `Client.Game.InGame.Train.Timetable`（移した 3 型を使う箇所） | `Client.Ui.State.Train` |
| `Client.Game.InGame.UI.BlockDetail` | `Client.Ui.State.BlockDetail` |
| `Client.Game.InGame.UI.Blueprint`（`BlueprintNameInputModalBridge` を使う箇所） | `Client.Ui.State.Blueprint` |
| `Client.Game.InGame.UI.Challenge`（`ChallengeProgressState` を使う箇所） | `Client.Ui.State.Challenge` |
| `Client.Game.InGame.UI.Modal` | `Client.Ui.State.Modal` |
| `Client.Game.InGame.UI.Notification`（`NotificationFeed`・`SequencedNotification` を使う箇所） | `Client.Ui.State.Notification` |
| `Client.Game.InGame.UI.Research` | `Client.Ui.State.Research` |

完全修飾名（`Client.Game.InGame.UI.Modal.ModalResult` のような書き方）が無いかも確かめる:

```bash
grep -rnE "Client\.Game\.InGame\.(EventMode|UI\.Modal|UI\.Research|UI\.BlockDetail)\b" moorestech_client/Assets/Scripts --include="*.cs"
```
Expected: 0 件

- [x] **Step 6: uGUI 監査ゲートの分類を追従させる**

`moorestech_client/Assets/Scripts/Client.Tests/WebUi/Gate/WebUiGateClassification.cs`:

`ScanRoots` の `"Client.DebugSystem",` の次に足す:

```csharp
            "Client.Ui.State",
```

`Rules` から次の 3 行を消す:

```csharp
            new Rule("Client.Game/InGame/UI/BlockDetail", Category.Infra, "開いているブロックの追跡とネットワーク情報"),
            new Rule("Client.Game/InGame/UI/Modal", Category.Infra, "モーダル要求の論理状態（表示は描画側が担当）"),
            new Rule("Client.Game/InGame/UI/Research", Category.Infra, "研究ノードの論理状態（表示は描画側が担当）"),
```

`Rules` の `new Rule("Client.Game/InGame/UI/Notification", ...)` の次に足す:

```csharp
            new Rule("Client.Ui.State", Category.Infra, "UITKとWebが共に読む画面状態（表示は描画側が担当）"),
```

`Rules` の Challenge の行は、残るのが `ChallengeManager` なので説明を替える:

```csharp
            new Rule("Client.Game/InGame/UI/Challenge", Category.Infra, "チャレンジ進行の管理（ChallengeManager）"),
```

`Client.Game/InGame/UI/Blueprint`・`Challenge`・`Notification` のルールは、残るファイル（`BlueprintNameInputState`・`ChallengeManager`・`ClientLocalNotificationSource` 等）があるので消さない。

- [x] **Step 7: Editor を起動してコンパイルする**

```bash
cd /Users/sakastudio/hermes-agent/data/worktrees/moorestech/uitk-ui-state-assembly
uloop launch --project-path ./moorestech_client
uloop compile --project-path ./moorestech_client
uloop get-logs --project-path ./moorestech_client --log-type Error
```
Expected: ErrorCount 0。Unity が `Client.Ui.State.asmdef.meta`・`Client.Ui.State.meta` と 8 つのサブディレクトリの `.meta` を作る

- [x] **Step 8: 対象テストを回す**

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.(EventMode|WebUi|WebUiHost|UIState|EditModeInPlayingTest\.Hostless)\." --timeout-seconds 1500
```
Expected: 全 PASS（`WebUiGateAuditTest` と `WebUiHostlessStateAvailabilityTest` を含む）。落ちたら、`ffab18e83a` を別の worktree で同じ regex を回して、移動前から落ちていたものか比べる（移動前から落ちるものは移動の不具合ではないので報告に書く）

- [x] **Step 9: 依存の向きを確かめる**

```bash
grep -c "Client.Ui.State" moorestech_client/Assets/Scripts/Client.Game/Client.Game.asmdef
git diff -M --name-status origin/feature/uitk-stage0 -- moorestech_client/Assets/Scripts | grep "^R" | grep "Client.Ui.State" | grep -v "\.meta" | wc -l
```
Expected: 1 行目 `0`、2 行目 `13`

- [x] **Step 10: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State moorestech_client/Assets/Scripts/Client.Ui.State.meta moorestech_client/Assets/Scripts/Client.Game moorestech_client/Assets/Scripts/Client.Starter moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Tests
git status --short
git commit -m "refactor(uitk): 画面状態を Client.Game から新設の Client.Ui.State へ移す"
```
Expected: `git status --short` に未追跡の `.meta` が残らない

### Task 2: 移行計画の置き場の記述を Client.Ui.State へ書き換える

**Files:**
- Modify: `docs/uitk-migration/plan.md`（§2.1「0-4 状態の移設」、§3.1、§3.2）

**Interfaces:**
- Consumes: Task 1 のアセンブリ名 `Client.Ui.State`
- Produces: なし

- [ ] **Step 1: §2.1「0-4 状態の移設」の 2 行を書き換える**

`WebUiModalService`（static をやめ、`Client.Game` のインターフェースにする） → `WebUiModalService`（static をやめ、`Client.Ui.State` の DI サービスにする）

`- 移す先は \`Client.Game\` の DI（MainGameStarter の LifetimeScope）。` で始まる行を次にする:

```markdown
- 移す先は `Client.Ui.State` アセンブリ（`Client.Game` を参照し、`Client.Game` からは参照されない）の型で、登録は MainGameStarter の LifetimeScope。今はこれらが `WebUiGameBinder.Bind()` の中でしか作られず、Hub が null だと丸ごと作られない（`Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:48-55`）。置き場の裁定は `.decisions/2026-10-29-UIの画面状態はClient.Gameに置かず専用アセンブリClient.Ui.Stateに置く.md`
```

- [ ] **Step 2: §3.1 の asmdef の行を書き換える**

`- **asmdef**: \`Client.Ui\` を新設する。` で始まる行を次にする:

```markdown
- **asmdef**: 描画の `Client.Ui` と画面状態の `Client.Ui.State`（段階 0 で新設済み）の 2 つにする。名前に技術名（Uitk）を入れない（Web という名前の改名を繰り返さないため）。参照の向きは `Client.Game` ← `Client.Ui.State` ← {`Client.Ui`・`Client.WebUiHost`}。`Client.Ui` は描画と操作だけを持ち、画面状態・ViewModel・UI コマンドは `Client.Ui.State` に置く。`Client.Ui.State` を `Client.Ui` に含めないのは、共存規則 R1 で `Client.Ui` が WebUiHost を触るため（WebUiHost も状態を読むので、同じアセンブリだと循環する）。`Client.Game` はどちらも参照せず、ゲーム側から UI へ頼む要求（モーダル等）はインターフェース越しにする。DI は既存の VContainer、イベントは UniRx
```

- [ ] **Step 3: §3.2 の 2 行を書き換える**

`- **正本には手を入れない**:` の行の先頭を `- **ゲームの正本には手を入れない**:` にし、末尾に「画面のための状態は正本ではないので `Client.Ui.State` に置く」を足す。

`- **topic を 2 つに分ける**:` の行の `①型付きのスナップショットと変化通知（\`IReadOnlyReactiveProperty<XxxViewModel>\`）を \`Client.Game\` に置く。` を `①型付きのスナップショットと変化通知（\`IReadOnlyReactiveProperty<XxxViewModel>\`）を \`Client.Ui.State\` に置く。` にする。

- [ ] **Step 4: 残りが無いことを確かめてコミットする**

```bash
grep -n "Client.Game" docs/uitk-migration/plan.md
```
Expected: 残る行は「ゲームの正本」「`Client.Game` は参照しない」「バグ報告から見るインターフェースは `Client.Game`」（§3.11。ゲーム側の持ち物なので対象外）など、状態の置き場を `Client.Game` と言っていないものだけ

```bash
git add docs/uitk-migration/plan.md
git commit -m "docs(uitk): 画面状態の置き場を Client.Ui.State に改める"
```

### Task 3: moores-code-review で全ブランチレビュー

必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。比較の基点は `origin/feature/uitk-stage0`（`ffab18e83a`）にする（master を基点にすると段階 0 全体が対象に入るため）。pi ホストでは codex-host の runner で回す。始める前に `~/.codex/sessions` の最新 rollout の `rate_limits.primary.used_percent` を見て、90% を超えていたら作業ブランチを push して止まり、報告する（クレジットは使わない）。レビュー指摘の反映がソースの判定経路・条件式に触れたら、Task 1 Step 8 のテストを反映後に回し直す。

- [ ] **Step 1: moores-code-review を基点 `origin/feature/uitk-stage0` で実行し、指摘を反映してコミットする**

### Task 4: 統合ブランチへ取り込み、PR #1559 をマージ可能な状態にする

セッション終了可能状態にすること。この作業は統合 PR #1559 の一部なので、新しい PR は作らない（`.decisions/2026-10-26-UITK段階0のPRは1本に統合する.md`）。

- [ ] **Step 1: 作業ブランチを push し、統合ブランチへマージコミットで取り込む**

先にこのworktreeの Editor を止める（`moores-wt status` で `editor=<pid>` を見て `kill <pid>`。動いたまま checkout すると再インポートが走る）。

```bash
cd /Users/sakastudio/hermes-agent/data/worktrees/moorestech/uitk-ui-state-assembly
git push -u origin refactor/uitk-ui-state-assembly
git fetch origin
git checkout -q --detach origin/feature/uitk-stage0
git merge --no-ff refactor/uitk-ui-state-assembly -m "Merge branch 'refactor/uitk-ui-state-assembly' into feature/uitk-stage0"
git push origin HEAD:feature/uitk-stage0
git checkout -q refactor/uitk-ui-state-assembly
```

`origin/feature/uitk-stage0` が `ffab18e83a` から進んでいて衝突したら、pr-create スキルの衝突解消手順に従って解き、Task 1 Step 7〜8 を回してから push する。master との衝突は `gh pr view 1559 --json mergeable,mergeStateStatus` で確かめ、`CONFLICTING` なら pr-create スキルの手順で master を統合ブランチへマージして解く

- [ ] **Step 2: PR #1559 の本文の「取り込み済み」表に 1 行足す**

`BODY=$(mktemp -t pr1559body); gh pr view 1559 --json body -q .body > "$BODY"` で本文を取り出し、表の `feature/uitk-s0-dsl` の行の次に次の行を足して `gh pr edit 1559 --body-file "$BODY"` で戻す:

```markdown
| `refactor/uitk-ui-state-assembly` | 画面状態 13 ファイルを `Client.Game` から新設の `Client.Ui.State` へ移す（挙動は変えない移動。2026-10-29 裁定 `.decisions/2026-10-29-UIの画面状態はClient.Gameに置かず専用アセンブリClient.Ui.Stateに置く.md`。計画 §3.1・§3.2 も改訂） |
```

- [ ] **Step 3: CI と mergeable を確かめる**

```bash
gh pr checks 1559 --watch --fail-fast
gh pr view 1559 --json mergeable,mergeStateStatus,headRefOid
```
Expected: 全 pass、`MERGEABLE` / `CLEAN`、headRefOid が Step 1 のマージコミット

- [ ] **Step 4: worktree を畳む**

```bash
git -C /Users/sakastudio/hermes-agent/data/worktrees/moorestech/uitk-ui-state-assembly status --short
moores-wt rm uitk-ui-state-assembly
```
Expected: 未コミット・未 push が無く、`rm` が成功する

## 判断記録（ADR）

- 置き場の裁定: [[2026-10-29-UIの画面状態はClient.Gameに置かず専用アセンブリClient.Ui.Stateに置く]]（`.decisions/`）。親の裁定は [[2026-10-18-CEFを撤去しUI-Toolkitへ全面移行する]]、PR の扱いは [[2026-10-26-UITK段階0のPRは1本に統合する]]
- P1（タスク分割）: asmdef 新設・移動・`using` 張り替え・分類表は途中でコンパイルが通らないので 1 タスクにまとめた。文書の改訂だけ Task 2 に分けた（コードを承認して文書を差し戻す、またはその逆に意味があるため）
- P2（名前空間）: 名前空間はディレクトリと一致させて `Client.Ui.State.<機能>` にする。時刻表は `Train/Timetable` ではなく `Train` 直下（`Client.Ui.State` の中で時刻表以外の列車の画面状態が来たら、そのときに分ける。今は 3 ファイルだけ）
- P3（構造の守り）: 「`Client.Game` が `Client.Ui.State` を参照しない」は、`Client.Ui.State` が `Client.Game` を参照するので循環参照として Unity が拒否する。新しいテストは足さない。「新しい画面状態を `Client.Game` に足さない」は計画 §3.1 の記述で始める（規約は一番軽い形から始め、破られたら強制を足す）
- P4（unityプレイ録画テスト）: 入れない。ランタイム挙動に触れないため（型の置き場と名前空間だけを変え、DI 登録・メソッド本体は変えない。画面に出るものは何も変わらない）。DI から状態が取れることは、Web ホストを起動しない EditModeInPlayingTest `WebUiHostlessStateAvailabilityTest` で確かめる
- P5（実行ホスト）: ユーザー指示「このままpi agentに依頼して」により pi agent（gpt-6.1-sol）で実行する。Codex 週次枠は 53%（10/29 18:24）で残っている
- P6（基点）: レビューと差分確認の基点は `origin/feature/uitk-stage0`。新しい PR は作らず、統合ブランチへマージコミットで取り込む
- P7（範囲の裁定・2026-10-29）: Phase 2.6 強1（範囲の食い違い）はユーザー裁定で決着。範囲はこの plan の 13 ファイルの移動だけ。PR #1559 が既存 `Client.Game` に入れた残りの差分（UniRx 化・NRE 修正・名前付き差し込み・旧 SkitUI 削除）は戻さない（差分を減らすための非本質的な迂回をしない）。出所はユーザー裁定（発言は `.decisions/2026-10-29-UIの画面状態はClient.Gameに置かず専用アセンブリClient.Ui.Stateに置く.md` に引用）
- P8（実行ホスト・2026-10-29）: ユーザー「いまぱっとできる？」の後「これ、このままpi agentに依頼して」。pi agent の新規セッションが Task 1 から実行する

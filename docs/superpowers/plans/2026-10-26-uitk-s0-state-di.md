# UITK 段階0 状態の移設（0-4・0-5・0-10） Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Web UI（CEF）ホストを起動しなくても、研究・チャレンジ・ブロック詳細・時刻表・モーダル・通知・出展モード言語ゲートの論理状態が `Client.Game` の DI から取れるようにし、`UIStateControl.OnStateChanged` を UniRx にし、Web ホストを起動しない Editor 限定スイッチを置く（挙動は変えない抽出 PR）。

**Architecture:** `Client.WebUiHost` の topic が抱えていた状態部分を `Client.Game` の純ロジッククラスへ移し、MainGameStarter の LifetimeScope に登録する（イベントを購読する状態は `IInitializable` の entry point にして Web の有無と無関係に起動時に作る）。WebUiHost 側の topic は「状態を購読して JSON にする」アダプタだけになり、`WebUiGameBinder.Bind()` は DI から状態を Resolve してアダプタを作るだけになる。帯域のための間引き（`IntervalFrame(4)`）とフレーム末の畳み込みはアダプタに残す。

**Tech Stack:** Unity / C#、VContainer（DI）、UniRx、UniTask、NUnit（EditMode・EditModeInPlayingTest）、uloop、unity-playmode-recorded-playtest

## Requirements

1. **0-5**: `UIStateControl.OnStateChanged` を `event Action<UIStateEnum>` から `IObservable<UIStateEnum>`（`Subject`）にし、購読側 8 か所（`BugReportUiStatePusher`・`ProgressRecorder`・`BlockInventoryTopic`・`TrainRidingTopic`・`UiStateTopic`・`ResearchTopic`・`BuildMenuTopic`・`OpenTrainTimetableTracker`）を全部 `Subscribe` に直す。受け入れ: `grep -rn "OnStateChanged +=\|OnStateChanged -=" moorestech_client/Assets/Scripts` が `UIStateControl` 関連で 0 件、コンパイル 0 エラー
2. **0-10**: `Client.Starter/Initialization/Boot/WebUiStartup.cs` の末尾に `#if UNITY_EDITOR` で囲った Editor 限定スイッチ `WebUiHostEditorSkip` を置く。印が立っていれば `WebUiHost.StartAsync` を呼ばずに `false` を返し、起動失敗と同じ `Hub == null` の経路で続ける。印は SessionState に置き、起動時に読んだ時点で消費する。受け入れ: EditModeInPlayingTest で `WebUiHost.Hub == null` のままメインゲームが初期化される。配布物には切替フラグが無い（`#if UNITY_EDITOR` の外から参照するとプレイヤービルドのコンパイルで落ちる）
3. **0-4 研究**: `ResearchTopic._nodeStates` と取り直しの駆動（研究画面への突入・研究画面表示中の所持変化）を `Client.Game` の `ResearchNodeStateStore` へ移す。受け入れ: Web ホスト無しで研究画面へ遷移すると `ResearchNodeStateStore.NodeStates` がサーバーから取り直される
4. **0-4 チャレンジ**: `ChallengeTopicState` の状態（カテゴリ全量と完了イベント）を `Client.Game` の `ChallengeProgressState` へ移し、`ChallengeTopicState` を消して `ChallengeTreeTopic`・`ChallengeCurrentTopic` がそれぞれ状態を購読する JSON アダプタになる。受け入れ: Web ホスト無しで `ChallengeProgressState.Categories` が handshake の全カテゴリを持つ
5. **0-4 ブロック詳細**: `BlockNetworkInfoCache` と、`BlockInventoryTopic.TrackBlock` の監視判断（開いているブロックの解決・ブロック状態イベントの購読・ネットワーク情報の取得開始）を `Client.Game` の `BlockNetworkInfoCache`・`OpenBlockDetailTracker` へ移す。Web 帯域のための再配信方針（連続値／ベルト／パイプ）はアダプタに残す。受け入れ: Web ホスト無しでブロックのサブインベントリを開くと、`OpenBlockDetailTracker.TrackedBlock` がそのブロックになり、`BlockNetworkInfoCache` が電力網かギア網の情報を取得する
6. **0-4 時刻表**: `OpenTrainTimetableTracker`・`TrainTimetableFetcher`（と両者が使う `OpenTrainUnitResolver`）を `Client.Game` へ移し DI に登録する。受け入れ: Web ホスト無しで `TrainTimetableFetcher` が取得要求を出し、存在しない列車を取得不可と判定する
7. **0-4 通知**: `NotificationTopic` の連番と移行告知（SaveMigration）の保持を `Client.Game` の `NotificationFeed` へ移す。受け入れ: Web ホスト無しでローカル通知が連番付きで `NotificationFeed.OnNotified` に流れる。既存の wire 契約テスト（`WireContractNotificationTest`）が通る
8. **0-4 モーダル**: `WebUiModalService` の static `Instance` をやめ、`Client.Game` の `ModalRequestService`（DI singleton）にする。受け入れ: Web ホスト無しで `ModalRequestService` に入力モーダルが保留され、`Respond` で解決する
9. **0-4 BP 名入力**: `BlueprintNameInputWebBridge`（状態→入力モーダル→状態への書き戻し）を `Client.Game` の `BlueprintNameInputModalBridge`（entry point）へ移す。受け入れ: Web ホスト無しで `BlueprintNameInputState.Open()` → モーダル保留 → `Respond("confirm", 名前)` → `OnConfirm` に名前が流れる
10. **0-4 出展モード言語ゲート**: `EventLanguageGate` を `Client.Game` へ移し、Web 以外の描く側（将来の UITK）から使えるようにする。ゲームの DI より前に要るので DI には載せず、作り手（`EventModeStartGate`）と渡し方は変えない（判断記録 D8）。受け入れ: `Client.Tests.EventMode` が全 PASS
11. **挙動不変**: 抽出 PR は「移動と配線」だけ。Web へ届く topic の中身・配信タイミング（フレーム末の畳み込み・4 フレーム間引き）・action の結果は変えない。受け入れ: 既存の C# テスト（`Client.Tests.WebUi`・`Client.Tests.WebUiHost`・`Client.Tests.EventMode`）と、移した topic に触る unityプレイ録画テスト 5 本がベースライン（Task 1）と同じ合否
12. **完了条件（計画 §2.1）**: Editor 限定スイッチで Web ホストを起動しない PlayMode でも、研究・チャレンジ・ブロック詳細・時刻表・モーダル・通知の状態が DI から取れる（EditModeInPlayingTest `WebUiHostlessStateAvailabilityTest`）。言語ゲートは DI より前に要るので対象外（D8）
13. **`BlockInventoryTopic` の 4tick／`IntervalFrame(4)` の食い違い**: 意図を判断記録に書き、挙動（`IntervalFrame(4)`）は変えない。コメントだけ実装に合わせる

**やらないこと（スコープ境界）**
- サーバー状態の同期（イベントパケット＋初期データ＋購読）の変更
- Web 側（`moorestech_web`）の変更。topic 名・JSON の形・action 名
- 遠隔実行（`Client.RemoteExec`・`WebUiEndpoints.cs` の remote-exec 分岐・`InitializeScenePipeline.cs` の `KestrelPort` 受け渡し。albof.2 の持ち物）
- ViewModel（不変の sealed class）への変換。UITK が使う段でその topic だけ作る（計画 §3.2「分けるのは、その段で使う topic だけ」）
- 上に挙げていない topic（インベントリ・ビルドメニュー・ホットバー・進捗バー・ツールチップ等）の移設
- `docs/uitk/`・CI の追従ゲート・`docs/webui/MIGRATION.md`（albof.4）、DSL（albof.6）、差し込み記法（albof.7）、OneJS／SkitUI 撤去（albof.5）
- Steam（playtest-staging を含む）・Discord など外へ出す操作

## Global Constraints

- AGENTS.md 全規約。特に: 1 ファイル 200 行未満／1 ディレクトリ 10 ファイルまで、partial 禁止、`Func<>` 禁止、try-catch 禁止（外部境界のみ）、イベント発火に `Action`/`event` を使わず UniRx、デフォルト引数禁止、初期化メソッドは `Initialize`、単純な getter/setter プロパティ禁止（`{ get; private set; }` は可）、主要処理に日本語→英語の 2 行コメント
- `.meta` を手で作らない。ファイル移動は `git mv <file>.cs` と `git mv <file>.cs.meta` を対で行い GUID を保つ。新規ファイルの `.meta` は Unity のコンパイル（`uloop compile`）で生成されたものをコミットする
- ゲーム寿命のオブジェクト（`Client.Game` へ移す状態クラス）には `IDisposable` を付けない（AGENTS.md「既知の制約」、moores-reviewer-digest #10）。WebUiHost 側のアダプタ（topic）は `WebSocketHub.ClearBindings` が Dispose するので従来どおり `IDisposable` のまま
- fail-closed で黙って捨てる経路を新設しない。移す既存の無音経路にはログを足すか `silent-ok:` を書く
- 移した状態クラスの DI 登録は `Client.Starter/Registration/MainGameUiStateRegistration.cs` に集める（前例: `MainGameModelRegistration` の「Web用の論理モデルを登録」節・`NetworkDisconnectState` の `RegisterEntryPoint<…>().AsSelf()`）
- コンパイル: `uloop compile --project-path ./moorestech_client`（ErrorCount 0）。Domain Reload 中エラーは 45 秒待って再試行
- テスト: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`
- **Unity Editor の予算**: 全セッション合計で同時 3 本まで。Editor を起動する前に `moores-wt status` で本数を確かめ、3 本以上動いていたら Editor の要らない作業を先に進める。Editor は `uloop launch` で起動する（`moores-wt new` の `--no-editor` で作成済みの worktree）
- Web UI を PlayMode で動かす前に `cd moorestech_web/webui && pnpm install --frozen-lockfile` を済ませる（`node_modules` を別 checkout から複製しない）
- 長い処理はバックグラウンドのポーリングで待たず、全部前景で実行する。区切り（タスク）ごとにコミットする
- 他レーンの持ち物（上の「やらないこと」）に触る必要が出たら、触らずに `bd note moorestech-albof.3 "…"` を残す
- 進捗は区切りごとに `bd note moorestech-albof.3 "…"`（1〜3 行）

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反5件・修正5件 / tracker の pipe 比較・再配信フラグ・アダプタからの Refresh をアダプタ側へ戻し、Challenge の共有 publisher を廃止、言語ゲートの DI 登録をやめた。死活表を追加
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み（機構変更後に変えた範囲を fresh-context で再検査済み。再検査で違反3・強2・弱1を追加し、パイプ基準の控え時点・撤去直後の閉判定・通知注記を修正）/ 強3・弱6・第3バケツ1 / 強2件は修正で解消（tracker の bool 撤去・通知のカテゴリ欠損をログ付きで落とす）、検査7（モーダルを interface にしない）は D2 のまま PR で見てもらう。弱は D13 に裁定

---

## 配置と前例

| # | 項目 | 配置先 | 機構 | 前例（役割で選んだもの） |
|---|---|---|---|---|
| 1 | `UIStateControl.OnStateChanged` | `Client.Game`（既存） | `Subject<UIStateEnum>` を `IObservable` で公開 | `NestedPauseSubStateController.OnStateChanged`（同じ「画面状態の遷移通知」の役割で UniRx） |
| 2 | `WebUiHostEditorSkip` | `Client.Starter/Initialization/Boot/WebUiStartup.cs` 末尾 `#if UNITY_EDITOR` | SessionState の印を起動時に消費 | `PlaytestStartGateBypass`（Editor の無人起動の印。SessionState・読んだら消費・EnteredEditMode で消す） |
| 3 | `ResearchNodeStateStore` | `Client.Game/InGame/UI/Research/` | `IInitializable` entry point、getter＋`IObservable<Unit>` | `NetworkDisconnectState`（Web 用論理モデル・イベント購読・`RegisterEntryPoint().AsSelf()`）、`ProgressBarState`（getter＋変化通知） |
| 4 | `ChallengeProgressState` | `Client.Game/InGame/UI/Challenge/` | 同上 | 同上 |
| 5 | `NotificationFeed`・`SequencedNotification` | `Client.Game/InGame/UI/Notification/` | 同上（通知は `Subject<SequencedNotification>`） | 同ディレクトリの `ClientLocalNotificationSource` |
| 6 | `ModalRequestService`・`ModalRequest`・`ModalVariant` | `Client.Game/InGame/UI/Modal/` | DI singleton、getter＋`IObservable<Unit>` | `PauseMenuStateService`（UI の論理状態サービス・DI singleton） |
| 7 | `BlueprintNameInputModalBridge` | `Client.Game/InGame/UI/Blueprint/` | `IInitializable` entry point | 同ディレクトリの `BlueprintNameInputCursorApplier`（同じ状態を購読する entry point） |
| 8 | `EventLanguageGate` | `Client.Game/InGame/EventMode/` | 型だけ移す。作り手は `EventModeStartGate`（DI より前）のまま、DI には載せない | 置換対象自身（`EventModeStartGate` が作り `EventLanguageGateBinder` へ渡す現行の駆動）。計画 §5「DI より前に出す全画面ゲート」 |
| 9 | `TrainTimetableFetcher`・`OpenTrainTimetableTracker`・`OpenTrainUnitResolver` | `Client.Game/InGame/Train/Timetable/` | fetcher は singleton、tracker は entry point | 駆動は置換対象自身（tracker は UI 状態遷移・列車キャッシュを購読）。置き場は時刻表ドメインの既存ディレクトリ（`ClientTrainTimetableDatastore` 等。役割は違うが、時刻表の型を UI 側と列車側に割らないため。D15） |
| 10 | `BlockNetworkInfoCache`・`OpenBlockDetailTracker` | `Client.Game/InGame/UI/BlockDetail/` | cache は singleton、tracker は entry point。tracker は読み口（`TrackedBlock`・変化通知）だけを出し、書き換えは自分の購読からだけ行う | `NetworkDisconnectState`、旧 `BlockInventoryTopic.TrackBlock`（置換対象自身の駆動＝UI 状態遷移とサブインベントリ更新の購読を引き継ぐ） |
| 11 | DI 登録 | `Client.Starter/Registration/MainGameUiStateRegistration.cs` | `MainGameStarter.StartGame` から呼ぶ | `MainGameModelRegistration`・`MainGameInteractionRegistration`。前例 `NetworkDisconnectState` が居る `MainGameModelRegistration` の「Web用の論理モデル」節へ足さず新ファイルにしたのは、9 種を足すと同ファイルが 200 行に迫り、`MainGameInteractionRegistration` は既に 215 行あるため |
| 12 | WebUiHost の topic | `Client.WebUiHost`（既存） | DI の状態を購読して JSON にするアダプタ。各 topic が自分で購読し自分で解除する。Web 帯域のための間引き・再配信方針はここに残す | `TutorialPresentationTopic`・`ProgressTopic`（`Client.Game` の状態を購読して配信） |

**データフロー（Phase 1.5）**: （サーバーイベント・UI 状態遷移・所持変化）→［`Client.Game` の状態クラス（書き手）］→（WebUiHost の topic＝読み手）→ JSON → Web。新設クラスはすべて「書き手」で、topic は「読み手」に縮む。読み手は状態を書き換える口を呼ばない（例外は Web の action が `ApplyNodeStates`・`Respond`・`ApplyFilterSplitterSnapshot` を呼ぶもので、移す前と同じ書き込み経路）。交差点（Web への直接セッター・bool の制御戻り）は足さない。

**死活表（Phase 2.5。移設で巻き添えになりうる操作）**

| 操作 | 移設後も生きるか | 根拠 |
|---|---|---|
| 研究画面（表示・研究完了ボタン） | 生きる | ストアの変化を topic が同期購読して全量配信。`research.complete` の action はストアへ書く（Task 5） |
| チャレンジ一覧・常駐 HUD | 生きる | 完了イベント 1 回で tree→current の順に配信（Subject は購読順に配るので binder の登録順で保つ。Task 6） |
| ブロック詳細（機械・電力・ギア・ベルト・パイプ・フィルタ分岐器） | 生きる | 追跡は tracker、再配信方針と間引きはアダプタ。Web 側の配信タイミングは不変（D6・D7。Task 10） |
| フィルタ分岐器の操作 | 生きる | action と tracker が同じ `BlockNetworkInfoCache` singleton を使う |
| 列車インベントリ・時刻表タブ | 生きる | fetcher は DI singleton で、topic と時刻表 action が同じインスタンスを使う（Task 9） |
| モーダル（確認・エラー） | 生きる | 応答 action と topic が同じ service を使う。Web ホスト停止時の cancel も残す（D16） |
| BP 名入力 | Web 有りは生きる | Web 無しで開くと保留のまま止まるのは移す前と同じ（D3） |
| 通知（獲得・拒否・移行告知） | 生きる | 連番を振らずに落とす条件は同じ。移行告知のスナップショットも返す（Task 7） |
| 出展モードの言語選択 | 生きる | ゲートの作り手と渡し方は変えない（D8） |
| UI 状態遷移を購読する 8 か所（ビルドメニュー・乗車・画面ルーティング等） | 生きる | Subject の同期 `OnNext` は `event Action` と同じ時点で発火（Task 2） |

---

## Task 1: ベースライン録画（変更前の合否を取る）

**Files:**
- Create（repo 外）: `../moorestech_logs/harness/albof3/baseline.md`（無ければディレクトリごと作る。`../moorestech_logs/README.md` を先に読む）

**Interfaces:**
- Consumes: なし
- Produces: ベースラインの合否表（Task 11 が同じシナリオを流して突き合わせる）

- [x] **Step 1: Editor の予算を確かめて起動する**

Run: `moores-wt status` → 動いている Editor が 3 本未満であることを確認。`cd moorestech_web/webui && pnpm install --frozen-lockfile && cd -`、`uloop launch --project-path ./moorestech_client`
Expected: Editor 起動。3 本以上なら Task 2 のコード作業（Editor 不要部分）を先に進め、空いてから戻る

- [x] **Step 2: unity-playmode-recorded-playtest スキルを読み、次の 5 本を変更前のコード（HEAD = `b40795d82b06e8ff39ca3b073ae57ff349e0d23a`（この実行の変更前コミット））で流す**

対象（移した topic に触るもの。壊れている bd `moorestech-wwxy` の 10 本は除外済み）:
1. `misc/ui-state-transition-matrix.cs`（UI 状態遷移・研究画面・チャレンジ一覧。0-5 と研究）
2. `misc/walk-while-menus-open.cs`（メニュー・機械 UI の開閉と到達範囲外での閉じ。0-5 とブロック詳細）
3. `building/ui-place-then-open-machine.cs`（ブロックインベントリの topic）
4. `train/train-timetable-via-ui.cs`（時刻表の取得・再取得。時刻表 fetcher/tracker）
5. `misc/vein-hand-mining-smoke.cs`（獲得通知。通知の連番）

Run: スキルの `scripts/run-scenario.sh` の手順どおり（第 3 引数のマスタ指定は `docs/development/worktree-and-review-pitfalls.md` に従う）
Expected: 各本の `result.json` の合否と動画パス

- [x] **Step 2b: 壊れていると記録された関連シナリオを 1 回だけ流す**

研究・チャレンジ・通知・BP を直接触る `research-node-state-label`・`server-notification-smoke`・`blueprint-copy-paste-via-ui`・`blueprint-right-click-delete-via-ui` を、同じ変更前コードで 1 回ずつ流す（bd `moorestech-wwxy` で壊れていると記録されている）。PASS したものは回帰対象に加え（Task 11 でも流す）、FAIL したものは result.json のパスと失敗理由 1 行を baseline.md に残して除外する。シナリオの修理は本 PR の範囲外

- [x] **Step 3: 合否表を書く**

`../moorestech_logs/harness/albof3/baseline.md` に「シナリオ／合否／result.json のパス／失敗ならその理由 1 行」を書く。変更前から失敗しているものは Task 11 で「変更前から失敗」として扱う（直さない。`bd note moorestech-albof.3` に 1 行残す）

- [x] **Step 4: 記録する**

`../moorestech_logs` の README の手順でコミットする（feature ブランチにはコミットしない）。`bd note moorestech-albof.3 "ベースライン録画5本: <合否の要約>"`

---

## Task 2: `UIStateControl.OnStateChanged` を UniRx にする（0-5）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/UIStateControl.cs:16,69`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/Capture/BugReportUiStatePusher.cs:22`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Playtest/Progress/ProgressRecorder.cs:63,112`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockInventoryTopic.cs:80,98`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/TrainRidingTopic.cs:32,45`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/UiStateTopic.cs:36,50`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Research/ResearchTopic.cs:39,56`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuTopic.cs:51,71`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Inventory/OpenTrainTimetableTracker.cs:33,49`

**Interfaces:**
- Consumes: なし
- Produces: `public IObservable<UIStateEnum> UIStateControl.OnStateChanged`（遷移のたびに新しい `CurrentState` を 1 回流す。購読時に現在値は流さない）。後続タスクの状態クラスはすべてこれを `Subscribe` する

- [x] **Step 1: `UIStateControl` を書き換える**

```csharp
// 16 行目の event を置き換える
private readonly Subject<UIStateEnum> _onStateChanged = new();
public IObservable<UIStateEnum> OnStateChanged => _onStateChanged;
```
```csharp
// 69 行目
_onStateChanged.OnNext(CurrentState);
```
`using UniRx;` は既にある。`event Action` が消えたら `using System;` は `IDisposable` のため残す。

- [x] **Step 2: Client.Game 側の購読 2 か所を直す**

`BugReportUiStatePusher.Initialize()`（ゲーム寿命。解除しない）:
```csharp
_sources.SetCurrentUiState(_uiStateControl.CurrentState);
_uiStateControl.OnStateChanged.Subscribe(_sources.SetCurrentUiState);
```
`ProgressRecorder`: 112 行目を既存の `CompositeDisposable` へ入れ、63 行目の `if (_started) _uiStateControl.OnStateChanged -= OnUiStateChanged;` を削除する:
```csharp
_uiStateControl.OnStateChanged.Subscribe(OnUiStateChanged).AddTo(_eventSubscriptions);
```
（`_eventSubscriptions` は 31 行目の `CompositeDisposable` で、119 行目などで `.AddTo(_eventSubscriptions)` に使われている。`Dispose()` が既に `_eventSubscriptions.Dispose()` する）

- [x] **Step 3: WebUiHost 側の購読 6 か所を同じ形に直す**

各クラスに `private readonly IDisposable _uiStateSubscription;` を足し、コンストラクタの `_uiStateControl.OnStateChanged += X;` を次へ、`Dispose()` の `-= X;` を `_uiStateSubscription.Dispose();` へ置き換える（`X` は各クラスの既存ハンドラ名: `BlockInventoryTopic`・`UiStateTopic`・`ResearchTopic`・`BuildMenuTopic` は `OnStateChanged`、`TrainRidingTopic`・`OpenTrainTimetableTracker` は `OnUiStateChanged`）:
```csharp
_uiStateSubscription = _uiStateControl.OnStateChanged.Subscribe(OnStateChanged);
```
`OpenTrainTimetableTracker` は既存の `_subscriptions`（`CompositeDisposable`）へ `.AddTo(_subscriptions)` で入れ、`Dispose()` の `-=` 行を消すだけでよい。各ファイルに `using UniRx;` が無ければ足す。

- [x] **Step 4: 取り残しを確かめる**

Run: `grep -rn "OnStateChanged +=\|OnStateChanged -=" moorestech_client/Assets/Scripts | grep -v "_subStateController\|onStateChanged ="`
Expected: 0 件（`MapObjectDistanceVisibilityController` の `_cullingGroup.onStateChanged = ` は別物）

- [x] **Step 5: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.(WebUi|WebUiHost|EventMode|BugReport)\."`
Expected: ErrorCount 0 / 全 PASS（失敗があれば、変更前から落ちているかを `git stash` ではなく一時 WIP コミットの前後で確かめて報告に書く）

- [x] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/UIStateControl.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/Capture/BugReportUiStatePusher.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Playtest/Progress/ProgressRecorder.cs moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics
git commit -m "refactor(ui-state): UIStateControl.OnStateChanged を UniRx の IObservable にし購読側を Subscribe へ直す"
```

---

## Task 3: Web ホストを起動しない Editor 限定スイッチと、Web 無し起動のテストの骨組み（0-10）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Initialization/Boot/WebUiStartup.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Hostless/WebUiHostlessStateAvailabilityTest.cs`

**Interfaces:**
- Consumes: なし
- Produces:
  - `public static class Client.Starter.Initialization.Boot.WebUiHostEditorSkip`（`#if UNITY_EDITOR` のみ存在）: `public static void Apply()`（次の 1 回の起動で Web ホストを起動しない印を立てる）、`internal static bool ConsumeSkipMark()`
  - テスト `WebUiHostlessStateAvailabilityTest` の `Body()` 内のローカル変数 `resolver`（`IObjectResolver`）。後続タスクはここへプローブの呼び出しを 1 行ずつ足す（待ちが要るプローブは `await XxxStateProbe.AssertAvailableAsync(resolver);`、要らないものは `XxxStateProbe.AssertAvailable(resolver);`）

- [x] **Step 1: `WebUiStartup` に分岐と末尾のスイッチを足す**

`StartAsync` の先頭（`try` の前）:
```csharp
#if UNITY_EDITOR
            // Editor限定スイッチの印があれば、起動失敗と同じHub=nullの経路で続ける
            // With the Editor-only switch marked, continue down the same hub-null path as a startup failure
            if (WebUiHostEditorSkip.ConsumeSkipMark())
            {
                Debug.LogWarning("[WebUiHost] Editor限定スイッチによりWebホストを起動しません（Hub=nullで続行）");
                return false;
            }
#endif
```
`WebUiStartup` クラスの閉じ括弧の後・namespace の閉じ括弧の前（ファイル末尾）:
```csharp
#if UNITY_EDITOR
    /// <summary>
    /// Editor限定: 次の1回の起動でWebホストを起動しない印。配布物には存在せず、非Editorコードから参照するとプレイヤービルドのコンパイルで落ちる
    /// Editor-only mark that skips the web host on the next boot; it does not exist in player builds, so a reference from non-editor code fails the player build
    /// 印はSessionStateに置き、起動時に読んだ時点で消費する（前例: PlaytestStartGateBypass）
    /// The mark lives in SessionState and is consumed when the boot reads it (precedent: PlaytestStartGateBypass)
    /// </summary>
    public static class WebUiHostEditorSkip
    {
        private const string SessionStateKey = "WebUiHostEditorSkip_NextBoot";

        // 未消費の印を次のPlayへ漏らさない
        // Never leak an unconsumed mark into the next play
        [UnityEditor.InitializeOnLoadMethod]
        private static void Initialize()
        {
            UnityEditor.EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        private static void HandlePlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.EnteredEditMode) UnityEditor.SessionState.EraseBool(SessionStateKey);
        }

        // 自動起動の入口（テストのPlayMode突入・今後のCEF無し起動ラダー）から呼ぶ
        // Called from unattended entry points (a test entering Play Mode, the future no-CEF boot ladder)
        public static void Apply()
        {
            UnityEditor.SessionState.SetBool(SessionStateKey, true);
        }

        internal static bool ConsumeSkipMark()
        {
            var marked = UnityEditor.SessionState.GetBool(SessionStateKey, false);
            UnityEditor.SessionState.EraseBool(SessionStateKey);
            return marked;
        }
    }
#endif
```

- [x] **Step 2: Web 無し起動のテストを書く**

`Client.Tests/EditModeInPlayingTest/Hostless/WebUiHostlessStateAvailabilityTest.cs`:
```csharp
using System.Collections;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
#if UNITY_EDITOR
using Client.Starter.Initialization.Boot;
using UnityEditor;
#endif
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Hostless
{
    /// <summary>
    /// Webホストを起動しないPlayModeでも、Webから移した論理状態がDIから取れることを確かめる（UITK段階0の完了条件）
    /// Verifies the logical states moved out of the web host are resolvable from DI in a Play Mode without the web host (UITK stage 0 exit criterion)
    /// </summary>
    [Category("CiShardClientPlay2")]
    public class WebUiHostlessStateAvailabilityTest
    {
        [UnityTest]
        public IEnumerator Webホストを起動しなくても移した状態がDIから取れる()
        {
            EnterPlayModeUtil();
#if UNITY_EDITOR
            WebUiHostEditorSkip.Apply();
#endif

            // yield return new EnterPlayMode は必ず[UnityTest]関数の直下で呼び出すこと
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function
            yield return new EnterPlayMode(expectDomainReload: true);

            // EnterPlayMode時のテストフレームワーク内部エラーでテストが失敗するのを防ぐ
            // Prevent test failure from test framework internal errors during EnterPlayMode
            LogAssert.ignoreFailingMessages = true;

            yield return Body().ToCoroutine();

            yield return new ExitPlayMode();

#if UNITY_EDITOR
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
#endif

            #region Internal

            async UniTask Body()
            {
                await LoadMainGame();

                // スイッチが効き、起動失敗と同じHub=nullの経路を通ったことを前提として固定する
                // Pin the precondition that the switch worked and the boot went down the hub-null path
                Assert.IsNull(Client.WebUiHost.Boot.WebUiHost.Hub, "Editor限定スイッチを付けたのにWebホストが起動した");
                var resolver = ClientDIContext.DIContainer.DIContainerResolver;
                Assert.IsNotNull(resolver);
            }

            #endregion
        }
    }
}
```

- [x] **Step 3: コンパイルし、テストを流す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.EditModeInPlayingTest.Hostless.WebUiHostlessStateAvailabilityTest" --timeout-seconds 900`
Expected: ErrorCount 0 / PASS。`uloop get-logs --project-path ./moorestech_client --log-type Warning` に `[WebUiHost] Editor限定スイッチによりWebホストを起動しません` と `[WebUiHost] Bind skipped: hub is null` が出ている

- [x] **Step 4: 印が漏れないことを確かめる**

Run: 同じ Editor で `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.EditModeInPlayingTest.BlockInventory.ElectricToGearOutputModeActionTest" --timeout-seconds 900`
Expected: PASS（このテストは hub を要する。印が次の起動へ漏れていたら hub が null で落ちる）

- [x] **Step 5: コミット（Unity が生成した `.meta` を含める）**

```bash
git add moorestech_client/Assets/Scripts/Client.Starter/Initialization/Boot/WebUiStartup.cs moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Hostless
git commit -m "feat(webui-host): Webホストを起動しないEditor限定スイッチとWeb無し起動のテストを足す"
```

---

## Task 4: モーダルと BP 名入力の状態を Client.Game の DI へ移す

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Modal/ModalRequestService.cs`（`git mv` で `Client.WebUiHost/Game/WebUiModalService.cs` から移し、改名して書き換える）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Modal/ModalRequest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Blueprint/BlueprintNameInputModalBridge.cs`（`git mv` で `Client.WebUiHost/Game/BlueprintNameInputWebBridge.cs` から）
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiStateRegistration.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs:108-109`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/ModalTopic.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/ModalActions.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebSocketHub.cs:115-117`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:70-76,147-148,152,157`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Hostless/ModalStateProbe.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Hostless/WebUiHostlessStateAvailabilityTest.cs`

**Interfaces:**
- Consumes: Task 3 の `resolver`
- Produces:
  - `Client.Game.InGame.UI.Modal.ModalRequestService`: `ModalRequest Pending { get; private set; }`、`IObservable<Unit> OnPendingChanged`、`UniTask<string> RequestModal(string title, string message, string buttonText, ModalVariant variant)`、`UniTask<(string result, string text)> RequestInputModal(string title, string message, string buttonText)`、`void CancelPendingRequest()`、`bool Respond(string id, string result, string text)`
  - `Client.Game.InGame.UI.Modal.ModalRequest`（フィールド `Id`・`Title`・`Message`・`ButtonText`・`Variant`・`RequiresInput`）、`enum ModalVariant { Confirm, Error }`
  - `Client.Starter.Registration.MainGameUiStateRegistration.Register(ContainerBuilder builder)`

- [x] **Step 1: ファイルを移す**

```bash
S=moorestech_client/Assets/Scripts
mkdir -p $S/Client.Game/InGame/UI/Modal
git mv $S/Client.WebUiHost/Game/WebUiModalService.cs $S/Client.Game/InGame/UI/Modal/ModalRequestService.cs
git mv $S/Client.WebUiHost/Game/WebUiModalService.cs.meta $S/Client.Game/InGame/UI/Modal/ModalRequestService.cs.meta
git mv $S/Client.WebUiHost/Game/BlueprintNameInputWebBridge.cs $S/Client.Game/InGame/UI/Blueprint/BlueprintNameInputModalBridge.cs
git mv $S/Client.WebUiHost/Game/BlueprintNameInputWebBridge.cs.meta $S/Client.Game/InGame/UI/Blueprint/BlueprintNameInputModalBridge.cs.meta
```
（新設ディレクトリ `Modal` 自体の `.meta` は Unity が生成する）

- [x] **Step 2: `ModalRequestService` を書き換える**

`WebUiModalService` から次を変える。中身のロジック（`RequestCore` の「最新要求を先に確定し、前の要求を cancel で解決する」順序、`CancelPendingRequest`、`Respond` の id 照合）は一字も変えない。
- namespace を `Client.Game.InGame.UI.Modal`、クラス名を `ModalRequestService` にする
- `public static WebUiModalService Instance` とコンストラクタの `Instance = this;`、`CancelPending()`（Instance を閉じる版）を削除する。コンストラクタは削除（既定コンストラクタ）
- `ModalRequest` と `ModalVariant` は `ModalRequest.cs` へ移す（200 行と 1 ファイル 1 責務）
- クラスコメントを次に置き換える:
```csharp
    /// <summary>
    /// 保留中のモーダル要求を1件持ち、描く側（Web・将来のUITK）からの応答で解決する。出す側（BP名入力など）はRequest系を呼ぶ
    /// Holds the single pending modal request and resolves it with the reply from the presenter (web, later UITK); producers such as the blueprint-name input call the Request methods
    /// </summary>
```

`ModalRequest.cs`:
```csharp
namespace Client.Game.InGame.UI.Modal
{
    /// <summary>
    /// 保留中モーダル要求の内容
    /// Contents of a pending modal request
    /// </summary>
    public class ModalRequest
    {
        public string Id;
        public string Title;
        public string Message;
        public string ButtonText;
        public ModalVariant Variant;
        public bool RequiresInput;
    }

    /// <summary>
    /// モーダルの表示種別。web の "confirm" | "error" に対応
    /// Modal display kind; maps to the web's "confirm" | "error"
    /// </summary>
    public enum ModalVariant
    {
        Confirm,
        Error,
    }
}
```

- [x] **Step 3: `BlueprintNameInputModalBridge` を entry point にする**

```csharp
using Client.Game.InGame.UI.Modal;
using Client.Localization;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using UniRx;
using VContainer.Unity;

namespace Client.Game.InGame.UI.Blueprint
{
    /// <summary>
    /// BP名入力状態の開閉を購読し、入力モーダルへ転送して応答を書き戻す。状態権威は BlueprintNameInputState のまま
    /// Bridges the blueprint-name state to the input modal and writes the reply back; BlueprintNameInputState stays the state authority
    /// </summary>
    public sealed class BlueprintNameInputModalBridge : IInitializable
    {
        private readonly BlueprintNameInputState _state;
        private readonly ModalRequestService _modalService;

        public BlueprintNameInputModalBridge(BlueprintNameInputState state, ModalRequestService modalService)
        {
            _state = state;
            _modalService = modalService;
        }

        public void Initialize()
        {
            _state.OnOpenChanged.Subscribe(OnOpenChanged);
        }

        // 以下 OnOpenChanged（#region Internal の RequestAndRespond を含む）は BlueprintNameInputWebBridge から一字も変えずに写す
        // OnOpenChanged below (including RequestAndRespond in #region Internal) is copied verbatim from BlueprintNameInputWebBridge
    }
}
```
`OnOpenChanged` 本体は移動元のまま（`IDisposable`・`Dispose()`・`_subscription` は削除。ゲーム寿命のため）。`Mooresmaster.Localization.Generated` が `Client.Game` から見えない場合は、`Client.Game` 内の既存利用（`Client.Game/InGame/UI/UIState/State/DebugBlockInfoState.cs`）と同じ using にそろえる。

- [x] **Step 4: DI 登録を新設する**

`Client.Starter/Registration/MainGameUiStateRegistration.cs`:
```csharp
using Client.Game.InGame.UI.Blueprint;
using Client.Game.InGame.UI.Modal;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration
{
    /// <summary>
    /// Web UIホストから移したUIの論理状態を登録する。Webの有無に関わらず起動時に作り、描く側（Web・UITK）は購読するだけ
    /// Registers the UI logical states moved out of the web UI host; they are built at startup regardless of the web, and presenters (web, UITK) only subscribe
    /// </summary>
    internal static class MainGameUiStateRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            // モーダルの保留要求と、BP名入力からモーダルへの橋渡し
            // The pending modal request and the bridge from the blueprint-name input to the modal
            builder.Register<ModalRequestService>(Lifetime.Singleton);
            builder.RegisterEntryPoint<BlueprintNameInputModalBridge>();
        }
    }
}
```
`MainGameStarter.StartGame` の `MainGameInteractionRegistration.Register(builder, initialHandshakeResponse);` の次の行に足す:
```csharp
            MainGameUiStateRegistration.Register(builder);
```

- [x] **Step 5: WebUiHost 側をアダプタにする**

`ModalTopic`: フィールドとコンストラクタ引数の型を `ModalRequestService` にし、`using Client.Game.InGame.UI.Modal;` を足す。`Dispose()` を次にする（Web ホストの停止で保留中の await を cancel で解決する従来の挙動を、`WebSocketHub.ClearBindings` の static 呼び出しからアダプタの Dispose へ移す）:
```csharp
        public void Dispose()
        {
            _disposed = true;
            _subscription.Dispose();

            // ホスト停止でバインドが外れたら、保留中モーダルのawaitをcancelで解決する
            // When the host stops and the binding is released, resolve the pending modal await with cancel
            _service.CancelPendingRequest();
        }
```
`ModalActions.cs` の `ModalRespondActionHandler`: 型を `ModalRequestService` に、`using Client.Game.InGame.UI.Modal;` を足す。
`WebSocketHub.ClearBindings()` の末尾 3 行（`// 保留中モーダルの await を…` の 2 行コメントと `WebUiModalService.Instance?.CancelPending();`）を削除する。

`WebUiGameBinder.Bind()`:
```csharp
            // モーダルの保留要求はClient.GameのDIが持つ（Webは配信と応答の受け口だけ）
            // The pending modal request lives in Client.Game's DI; the web only publishes it and receives the reply
            var modalService = resolver.Resolve<ModalRequestService>();
            // モーダルトピックを登録
            // Register the modal topic
            var modalTopic = new ModalTopic(hub, modalService);
            hub.RegisterTopic(ModalTopic.TopicName, modalTopic);
```
（70-76 行目の `new WebUiModalService()` とモーダルトピック登録を置き換える）。152 行目 `var blueprintNameInputState = resolver.Resolve<BlueprintNameInputState>();` と 157 行目 `new BlueprintNameInputWebBridge(blueprintNameInputState, modalService);` を削除し、147-148 行目のコメントを「ビルドメニュートピックを登録」だけにする（BP 名入力の橋渡しは DI の entry point に移った）。`ModalRespondActionHandler(modalService)` の行は変えない。不要になった `using Client.Game.InGame.UI.Blueprint;` が他で使われていなければ消す。

- [x] **Step 6: Web 無しのプローブを書いてテストへ足す**

`Client.Tests/EditModeInPlayingTest/Hostless/ModalStateProbe.cs`:

実測による補正（D21）: 下のサンプルの `nameInput.Open()` 単独呼び出しは実DIのコピー購読者が要求する選択局面を満たさない。実装ではテスト用ワールドでBPを解放し、実ブロック1個を置き、既存reflection helperで得たselectionの公開遷移を通して名前入力へ進む。確定後は生成GUID・選択リセット・thumbnailまで待ち、自分が生成したGUIDとブロックだけを片付ける。全Hostlessプローブと非同期cleanupの区間で想定外error/exceptionを検出する。サンプルよりこの補正を優先する。

```csharp
using Client.Game.InGame.UI.Blueprint;
using Client.Game.InGame.UI.Modal;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniRx;
using VContainer;

namespace Client.Tests.EditModeInPlayingTest.Hostless
{
    // BP名入力→入力モーダルの保留→応答→状態への書き戻しが、Webホスト無しでも DI の中だけで回ることを確かめる
    // Verifies blueprint-name input -> pending input modal -> reply -> write-back runs inside DI alone, without the web host
    internal static class ModalStateProbe
    {
        public static async UniTask AssertAvailableAsync(IObjectResolver resolver)
        {
            var modal = resolver.Resolve<ModalRequestService>();
            var nameInput = resolver.Resolve<BlueprintNameInputState>();
            string confirmedName = null;
            using var confirmSubscription = nameInput.OnConfirm.Subscribe(name => confirmedName = name);

            nameInput.Open();
            Assert.IsNotNull(modal.Pending, "BP名入力を開いても入力モーダルが保留されない");
            Assert.IsTrue(modal.Pending.RequiresInput);

            Assert.IsTrue(modal.Respond(modal.Pending.Id, "confirm", "hostless-bp"));
            for (var i = 0; i < 30 && confirmedName == null; i++) await UniTask.Yield();
            Assert.AreEqual("hostless-bp", confirmedName, "モーダルの確定がBP名入力へ書き戻されない");
            Assert.IsNull(modal.Pending);
        }
    }
}
```
`WebUiHostlessStateAvailabilityTest.Body()` の末尾（`Assert.IsNotNull(resolver);` の次）に足す:
```csharp
                await ModalStateProbe.AssertAvailableAsync(resolver);
```

- [x] **Step 7: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.(WebUi|WebUiHost)\.|Hostless\.WebUiHostlessStateAvailabilityTest" --timeout-seconds 1200`
Expected: ErrorCount 0 / 全 PASS（`WireContractTest` のモーダル fixture 3 件を含む）

- [x] **Step 8: コミット**

```bash
git add -A moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Modal moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Blueprint moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Starter moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Hostless
git commit -m "refactor(modal): モーダルの保留要求とBP名入力の橋渡しをClient.GameのDIへ移しstaticをやめる"
```

---

## Task 5: 研究ノード状態を Client.Game の DI へ移す

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Research/ResearchNodeStateStore.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Research/ResearchTopic.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Research/ResearchNodeDtoFactory.cs:16`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/ResearchActions.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:137-140,183`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiStateRegistration.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Hostless/ResearchStateProbe.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Hostless/WebUiHostlessStateAvailabilityTest.cs`

**Interfaces:**
- Consumes: Task 2 の `IObservable<UIStateEnum> UIStateControl.OnStateChanged`、Task 4 の `MainGameUiStateRegistration.Register`
- Produces: `Client.Game.InGame.UI.Research.ResearchNodeStateStore`: `IReadOnlyDictionary<Guid, ResearchNodeState> NodeStates { get; private set; }`（初期値は空の Dictionary）、`IObservable<Unit> OnNodeStatesChanged`、`void ApplyNodeStates(Dictionary<Guid, ResearchNodeState> nodeStates)`、`void Initialize()`。`ResearchNodeDtoFactory.Create(ResearchNodeMasterElement master, IReadOnlyDictionary<Guid, ResearchNodeState> states)`

- [x] **Step 1: `ResearchNodeStateStore` を書く**

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState;
using Cysharp.Threading.Tasks;
using Game.Research;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.UI.Research
{
    /// <summary>
    /// 研究ノードの状態（サーバーが正本）を持ち、研究画面への突入と表示中の所持変化で取り直す
    /// Holds the research node states (owned by the server) and refetches them on entering the research screen and on inventory moves while it is shown
    /// </summary>
    public sealed class ResearchNodeStateStore : IInitializable
    {
        private readonly UIStateControl _uiStateControl;
        private readonly LocalPlayerInventoryController _inventoryController;
        private readonly Subject<Unit> _onNodeStatesChanged = new();
        private CancellationTokenSource _cts;
        private bool _researchScreenActive;

        public IReadOnlyDictionary<Guid, ResearchNodeState> NodeStates { get; private set; } = new Dictionary<Guid, ResearchNodeState>();
        public IObservable<Unit> OnNodeStatesChanged => _onNodeStatesChanged;

        public ResearchNodeStateStore(UIStateControl uiStateControl, LocalPlayerInventoryController inventoryController)
        {
            _uiStateControl = uiStateControl;
            _inventoryController = inventoryController;
        }

        public void Initialize()
        {
            // state遷移を購読し、ResearchTree 突入で状態を取り直す
            // Subscribe to state transitions and refetch states on entering ResearchTree
            _uiStateControl.OnStateChanged.Subscribe(OnStateChanged);

            // 充足の正本はサーバーstateなので、所持が動いたら研究画面表示中は取り直す
            // Server state owns sufficiency, so refetch on inventory moves while the research screen is up
            _inventoryController.LocalPlayerInventory.OnItemChange.Subscribe(_ => RefreshWhileResearchScreenActive());
            _inventoryController.OnInventoryRefreshed.Subscribe(_ => RefreshWhileResearchScreenActive());
        }

        // research.complete 応答の最新全ノード状態を反映する
        // Apply the latest node states from a research.complete response
        public void ApplyNodeStates(Dictionary<Guid, ResearchNodeState> nodeStates)
        {
            NodeStates = nodeStates;
            _onNodeStatesChanged.OnNext(Unit.Default);
        }

        private void OnStateChanged(UIStateEnum state)
        {
            // ResearchTree 突入時のみサーバーから最新状態を取り直す（uGUI と同じ駆動）
            // Refetch server states only on entering ResearchTree (same trigger as uGUI)
            _researchScreenActive = state == UIStateEnum.ResearchTree;
            if (!_researchScreenActive) return;
            StartRefresh();
        }

        // 研究画面を見ていない間の所持変化は、次の突入時の取り直しで足りる
        // Inventory moves while the screen is closed are covered by the refetch on the next entry
        private void RefreshWhileResearchScreenActive()
        {
            if (!_researchScreenActive) return;
            StartRefresh();
        }

        private void StartRefresh()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            RefreshAsync(_cts.Token).Forget();
        }

        private async UniTaskVoid RefreshAsync(CancellationToken ct)
        {
            var states = await ClientContext.VanillaApi.Response.Progression.GetResearchNodeStates(ct);
            if (ct.IsCancellationRequested) return;

            // 応答なしは前回の状態を保ち、次の突入・所持変化で取り直す
            // On no response keep the previous states; the next entry or inventory move refetches
            if (states == null)
            {
                Debug.LogWarning("[ResearchNodeStateStore] GetResearchNodeStates returned no response; keeping the previous node states");
                return;
            }
            ApplyNodeStates(states);
        }
    }
}
```

- [x] **Step 2: `ResearchTopic` をアダプタにする**

```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Research;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Core.Master;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// research.tree トピック: 研究マスタ + ResearchNodeStateStore の状態を合成して push
    /// research.tree topic: merges the research master with ResearchNodeStateStore's states and pushes them
    /// </summary>
    public class ResearchTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "research.tree";

        private readonly WebSocketHub _hub;
        private readonly ResearchNodeStateStore _store;
        private readonly IDisposable _subscription;

        public ResearchTopic(WebSocketHub hub, ResearchNodeStateStore store)
        {
            _hub = hub;
            _store = store;

            // 状態の差し替えごとに全量を同期で配信する（移す前の ApplyNodeStates と同じタイミング）
            // Publish the full tree synchronously on every replacement, the same timing as the former ApplyNodeStates
            _subscription = _store.OnNodeStatesChanged.Subscribe(_ => _hub.Publish(TopicName, BuildJson()));
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }

        private string BuildJson()
        {
            var dto = new ResearchTreeDto { Nodes = new List<ResearchNodeDto>() };
            foreach (var master in MasterHolder.ResearchMaster.GetAllResearches())
            {
                dto.Nodes.Add(ResearchNodeDtoFactory.Create(master, _store.NodeStates));
            }
            return WebUiJson.Serialize(dto);
        }
    }
}
```
`ResearchNodeDtoFactory.Create` の第 2 引数の型を `IReadOnlyDictionary<Guid, ResearchNodeState>` にする（本体は `GetValueOrDefault` だけなのでそのまま通る）。
`ResearchCompleteActionHandler`: フィールドとコンストラクタ引数を `ResearchNodeStateStore _store` にし、`_researchTopic.ApplyNodeStates(...)` を `_store.ApplyNodeStates(response.NodeState.ToDictionary());` に、`using Client.WebUiHost.Game.Topics;` を `using Client.Game.InGame.UI.Research;` に替える。

- [x] **Step 3: 登録と binder**

`MainGameUiStateRegistration.Register` に足す（`using Client.Game.InGame.UI.Research;`）:
```csharp
            // 研究ノードの状態。UI状態遷移と所持変化を購読するため起動時に作る
            // Research node states; built at startup because they subscribe to UI-state transitions and inventory moves
            builder.RegisterEntryPoint<ResearchNodeStateStore>().AsSelf();
```
`WebUiGameBinder`:
```csharp
            // 研究ツリートピックを登録（状態はDIのストアが持つ）
            // Register the research-tree topic (the DI store owns the states)
            var researchNodeStateStore = resolver.Resolve<ResearchNodeStateStore>();
            hub.RegisterTopic(ResearchTopic.TopicName, new ResearchTopic(hub, researchNodeStateStore));
```
（137-140 行目を置き換え）。183 行目を `hub.RegisterAction(new ResearchCompleteActionHandler(researchNodeStateStore));` にする。

- [x] **Step 4: プローブ**

`Hostless/ResearchStateProbe.cs`:
```csharp
using Client.Game.InGame.UI.Research;
using Client.Game.InGame.UI.UIState;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using VContainer;

namespace Client.Tests.EditModeInPlayingTest.Hostless
{
    // 研究画面へ入ると、Webホスト無しでもストアがサーバーから状態を取り直すことを確かめる
    // Verifies the store refetches node states from the server on entering the research screen, without the web host
    internal static class ResearchStateProbe
    {
        public static async UniTask AssertAvailableAsync(IObjectResolver resolver)
        {
            var store = resolver.Resolve<ResearchNodeStateStore>();
            var uiStateControl = resolver.Resolve<UIStateControl>();
            var before = store.NodeStates;

            uiStateControl.RequestTransition(UIStateEnum.ResearchTree);
            for (var i = 0; i < 600 && ReferenceEquals(store.NodeStates, before); i++) await UniTask.Yield();
            Assert.IsFalse(ReferenceEquals(store.NodeStates, before), "研究画面へ入っても研究ノード状態を取り直さない");

            // 後続のプローブのためにゲーム画面へ戻す
            // Return to the game screen for the following probes
            uiStateControl.RequestTransition(UIStateEnum.GameScreen);
            for (var i = 0; i < 120 && uiStateControl.CurrentState != UIStateEnum.GameScreen; i++) await UniTask.Yield();
            Assert.AreEqual(UIStateEnum.GameScreen, uiStateControl.CurrentState);
        }
    }
}
```
テストの `Body()` 末尾に `await ResearchStateProbe.AssertAvailableAsync(resolver);` を足す。

- [x] **Step 5: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.WebUi\.Research|Client\.Tests\.WebUi\.WireContract|Hostless\.WebUiHostlessStateAvailabilityTest" --timeout-seconds 1200`
Expected: ErrorCount 0 / 全 PASS

- [x] **Step 6: コミット**

```bash
git add -A moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Research moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Starter/Registration moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Hostless
git commit -m "refactor(research): 研究ノード状態と取り直しの駆動をClient.GameのDIへ移しtopicをアダプタにする"
```

---

## Task 6: チャレンジの状態を Client.Game の DI へ移す

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Challenge/ChallengeProgressState.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Challenge/ChallengeTopicState.cs`（`git rm` で `.meta` も）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Challenge/ChallengeTreeTopic.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Challenge/ChallengeCurrentTopic.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Challenge/ChallengeDtoBuilder.cs:9,51`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:141-146`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiStateRegistration.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/Challenge/ChallengeProgressStateTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Hostless/ChallengeStateProbe.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Hostless/WebUiHostlessStateAvailabilityTest.cs`

**Interfaces:**
- Consumes: Task 4 の `MainGameUiStateRegistration.Register`
- Produces:
  - `Client.Game.InGame.UI.Challenge.ChallengeProgressState`: `IReadOnlyList<ChallengeCategoryResponse> Categories { get; private set; }`、`IObservable<Guid> OnChallengeCompleted`（`Categories` を差し替えた後に、完了したチャレンジの GUID を流す）、`void Initialize()`
  - `ChallengeTreeTopic(WebSocketHub hub, ChallengeProgressState state) : ITopicHandler, IDisposable`、`ChallengeCurrentTopic(WebSocketHub hub, ChallengeProgressState state) : ITopicHandler, IDisposable`
  - `ChallengeDtoBuilder.BuildTree(IReadOnlyList<ChallengeCategoryResponse>)`・`BuildCurrent(IReadOnlyList<ChallengeCategoryResponse>, string)`

- [x] **Step 1: `ChallengeProgressState` を書く**

```csharp
using System;
using System.Collections.Generic;
using Client.Network.API;
using Core.Master;
using MessagePack;
using Server.Event.EventReceive;
using UniRx;
using VContainer.Unity;

namespace Client.Game.InGame.UI.Challenge
{
    /// <summary>
    /// チャレンジの全カテゴリ（handshakeの初期値と完了イベントの全量）を持ち、完了を通知する
    /// Holds every challenge category (the handshake's initial value and each completion event's full state) and notifies completions
    /// </summary>
    public sealed class ChallengeProgressState : IInitializable
    {
        private readonly IVanillaApiEvent _vanillaApiEvent;
        private readonly Subject<Guid> _onChallengeCompleted = new();

        public IReadOnlyList<ChallengeCategoryResponse> Categories { get; private set; }
        public IObservable<Guid> OnChallengeCompleted => _onChallengeCompleted;

        public ChallengeProgressState(IVanillaApiEvent vanillaApiEvent, InitialHandshakeResponse initial)
        {
            _vanillaApiEvent = vanillaApiEvent;
            Categories = initial.Challenges;
        }

        public void Initialize()
        {
            // 既存の完了イベントを購読し、全量状態で置き換える
            // Subscribe to the existing completion event and replace the state with its full snapshot
            _vanillaApiEvent.SubscribeEventResponse(CompletedChallengeEventPacket.EventTag, OnCompletedChallenge);
        }

        private void OnCompletedChallenge(byte[] packet)
        {
            var message = MessagePackSerializer.Deserialize<CompletedChallengeEventMessagePack>(packet);
            var categories = new List<ChallengeCategoryResponse>();
            foreach (var category in message.ChallengeCategories)
            {
                var master = MasterHolder.ChallengeMaster.GetChallengeCategory(category.ChallengeCategoryGuid);
                var current = category.CurrentChallengeGuids.ConvertAll(MasterHolder.ChallengeMaster.GetChallenge);
                var completed = category.CompletedChallengeGuids.ConvertAll(MasterHolder.ChallengeMaster.GetChallenge);
                categories.Add(new ChallengeCategoryResponse(master, category.IsUnlocked, current, completed));
            }
            Categories = categories;
            _onChallengeCompleted.OnNext(message.CompletedChallengeGuid);
        }
    }
}
```
（`ChallengeTopicState.OnCompletedChallenge` の変換ロジックを一字も変えずに写したもの。購読元を `ClientContext.VanillaApi.Event` から、同じインスタンスである DI の `IVanillaApiEvent` へ替えた）

- [x] **Step 2: 2 つの topic がそれぞれ状態を購読するアダプタにし、`ChallengeTopicState` を消す**

```bash
S=moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Challenge
git rm $S/ChallengeTopicState.cs $S/ChallengeTopicState.cs.meta
```
`ChallengeTreeTopic.cs`:
```csharp
using System;
using Client.Game.InGame.UI.Challenge;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    public class ChallengeTreeTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "challenge.tree";
        private readonly WebSocketHub _hub;
        private readonly ChallengeProgressState _state;
        private readonly IDisposable _subscription;

        public ChallengeTreeTopic(WebSocketHub hub, ChallengeProgressState state)
        {
            _hub = hub;
            _state = state;
            // 完了のたびに全カテゴリのツリーを配信する
            // Publish the whole category tree on every completion
            _subscription = _state.OnChallengeCompleted.Subscribe(_ => _hub.Publish(TopicName, BuildJson()));
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }

        private string BuildJson()
        {
            return WebUiJson.Serialize(ChallengeDtoBuilder.BuildTree(_state.Categories));
        }
    }
}
```
`ChallengeCurrentTopic.cs`:
```csharp
using System;
using Client.Game.InGame.UI.Challenge;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    public class ChallengeCurrentTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "challenge.current";
        private readonly WebSocketHub _hub;
        private readonly ChallengeProgressState _state;
        private readonly IDisposable _subscription;

        public ChallengeCurrentTopic(WebSocketHub hub, ChallengeProgressState state)
        {
            _hub = hub;
            _state = state;
            // 完了GUIDはeventだけへ載せ、snapshotではnullに戻す
            // Include the completed GUID only in the event; snapshots return null
            _subscription = _state.OnChallengeCompleted.Subscribe(completedGuid => _hub.Publish(TopicName, BuildJson(completedGuid.ToString())));
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson(null));
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }

        private string BuildJson(string completedGuid)
        {
            return WebUiJson.Serialize(ChallengeDtoBuilder.BuildCurrent(_state.Categories, completedGuid));
        }
    }
}
```
`ChallengeDtoBuilder.BuildTree`・`BuildCurrent` の引数型を `IReadOnlyList<ChallengeCategoryResponse>` にする（本体は `foreach` だけ）。

- [x] **Step 3: 登録と binder**

`MainGameUiStateRegistration.Register` に足す（`using Client.Game.InGame.UI.Challenge;`）:
```csharp
            // チャレンジの全カテゴリ。完了イベントを購読するため起動時に作る
            // Every challenge category; built at startup because it subscribes to completion events
            builder.RegisterEntryPoint<ChallengeProgressState>().AsSelf();
```
`WebUiGameBinder`（141-146 行目）:
```csharp
            // チャレンジツリーと常駐HUDは同じ状態（DI）を購読する。Subjectは購読順に配るので、tree→currentの配信順はこの登録順で保つ
            // The challenge tree and persistent HUD subscribe to the same DI state; Subject delivers in subscription order, so this registration order keeps tree before current
            var challengeProgressState = resolver.Resolve<ChallengeProgressState>();
            hub.RegisterTopic(ChallengeTreeTopic.TopicName, new ChallengeTreeTopic(hub, challengeProgressState));
            hub.RegisterTopic(ChallengeCurrentTopic.TopicName, new ChallengeCurrentTopic(hub, challengeProgressState));
```
`var initialHandshake = resolver.Resolve<InitialHandshakeResponse>();` が他で使われていなければ消し、`using Client.Network.API;` も不要なら消す。

- [x] **Step 4: プローブ**

`Hostless/ChallengeStateProbe.cs`:
```csharp
using Client.Game.InGame.UI.Challenge;
using Client.Network.API;
using NUnit.Framework;
using VContainer;

namespace Client.Tests.EditModeInPlayingTest.Hostless
{
    // チャレンジの全カテゴリがWebホスト無しでもDIの状態に載っていることを確かめる
    // Verifies every challenge category is held by the DI state without the web host
    internal static class ChallengeStateProbe
    {
        public static void AssertAvailable(IObjectResolver resolver)
        {
            var state = resolver.Resolve<ChallengeProgressState>();
            var handshake = resolver.Resolve<InitialHandshakeResponse>();
            Assert.IsNotNull(state.Categories, "チャレンジの状態がDIに無い");
            Assert.AreEqual(handshake.Challenges.Count, state.Categories.Count);
        }
    }
}
```
テストの `Body()` 末尾に `ChallengeStateProbe.AssertAvailable(resolver);` を足す。

- [x] **Step 5: 完了イベントの経路を単体テストで固定する**

プローブは handshake の初期値しか見ないので、`Initialize()` で購読が張られ、完了イベントで `Categories` を差し替えてから GUID を流すことを EditMode で確かめる（移す前は無テストだった経路）。`Client.Tests/WebUi/Challenge/ChallengeProgressStateTest.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Challenge;
using Client.Network.API;
using Client.Tests.Inventory;
using MessagePack;
using NUnit.Framework;
using Server.Event.EventReceive;
using UniRx;
using UnityEngine;

namespace Client.Tests.WebUi.Challenge
{
    public class ChallengeProgressStateTest
    {
        [Test]
        public void 完了イベントでカテゴリを差し替えてから完了GUIDを流す()
        {
            var vanillaApiEvent = new CapturingVanillaApiEvent();
            var initialCategories = new List<ChallengeCategoryResponse>();
            var state = new ChallengeProgressState(vanillaApiEvent, CreateHandshake(initialCategories));
            state.Initialize();

            // 完了の通知時点でカテゴリが差し替わっていることを、購読の中で見る
            // Observe inside the subscription that categories were already replaced when the completion is notified
            var completedGuid = Guid.NewGuid();
            Guid? received = null;
            var replacedBeforeNotify = false;
            using var subscription = state.OnChallengeCompleted.Subscribe(guid =>
            {
                received = guid;
                replacedBeforeNotify = !ReferenceEquals(state.Categories, initialCategories);
            });

#pragma warning disable CS0618
            var message = new CompletedChallengeEventMessagePack
            {
                CompletedChallengeGuidStr = completedGuid.ToString(),
                NextChallengeGuidsStr = new List<string>(),
                PlayedSkitIds = new List<string>(),
                ChallengeCategories = new List<ChallengeCategoryMessagePack>(),
            };
#pragma warning restore CS0618
            vanillaApiEvent.Dispatch(CompletedChallengeEventPacket.EventTag, MessagePackSerializer.Serialize(message));

            Assert.AreEqual(completedGuid, received);
            Assert.IsTrue(replacedBeforeNotify, "完了GUIDを流す前にカテゴリが差し替わっていない");
            Assert.AreEqual(0, state.Categories.Count);
        }

        // handshakeのうちチャレンジ以外は読まないので、他は空で組む（前例 UIStateTestFixtureBase.CreateHandshakeResponse）
        // Only challenges are read from the handshake, so the rest stays empty (precedent: UIStateTestFixtureBase.CreateHandshakeResponse)
        private static InitialHandshakeResponse CreateHandshake(List<ChallengeCategoryResponse> challenges)
        {
#pragma warning disable CS0618
            var initialHandshake = new global::Server.Protocol.PacketResponse.InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack
            {
                Accepted = new global::Server.Protocol.PacketResponse.Handshake.HandshakeAcceptedMessagePack
                {
                    PlayerPos = new Vector3MessagePack(Vector3.zero),
                },
            };
#pragma warning restore CS0618
            return new InitialHandshakeResponse(initialHandshake, (null, null, null, challenges, null, null, null, null));
        }
    }
}
```
（`CompletedChallengeEventMessagePack` の `CompletedChallengeGuidStr` 等の setter・`Vector3MessagePack` の namespace は `moorestech_server/Assets/Scripts/Server.Event/EventReceive/CompletedChallengeEventPacket.cs` と `Client.Tests/UIState/UIStateTestFixtureBase.cs` の using に合わせる。`InitialHandshakeResponse` のコンストラクタが null の inventory 等で落ちる場合は、`UIStateTestFixtureBase.CreateHandshakeResponse` と同じく落ちない値を入れる）

- [x] **Step 6: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.WebUi\.|Hostless\.WebUiHostlessStateAvailabilityTest" --timeout-seconds 1200`
Expected: ErrorCount 0 / 全 PASS

- [x] **Step 7: コミット**

```bash
git add -A moorestech_client/Assets/Scripts/Client.Tests/WebUi/Challenge moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Challenge moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Starter/Registration moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Hostless
git commit -m "refactor(challenge): チャレンジの全カテゴリと完了通知をClient.GameのDIへ移し2つのtopicをアダプタにする"
```

---

## Task 7: 通知の連番と移行告知を Client.Game の DI へ移す

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Notification/NotificationFeed.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Notification/SequencedNotification.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Notification/NotificationTopic.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:115`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiStateRegistration.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractNotificationTest.cs:59-97`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Hostless/NotificationStateProbe.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Hostless/WebUiHostlessStateAvailabilityTest.cs`

**Interfaces:**
- Consumes: Task 4 の `MainGameUiStateRegistration.Register`
- Produces: `Client.Game.InGame.UI.Notification.NotificationFeed(IVanillaApiEvent vanillaApiEvent, ClientLocalNotificationSource localNotificationSource)`: `void Initialize()`、`IObservable<SequencedNotification> OnNotified`、`SequencedNotification SaveMigrationNotice { get; private set; }`。`SequencedNotification`: `public readonly long Seq; public readonly NotificationMessagePack Message;`。`NotificationTopic(WebSocketHub hub, NotificationFeed feed)`

- [x] **Step 1: `SequencedNotification` と `NotificationFeed` を書く**

```csharp
using Server.Event.Notification;

namespace Client.Game.InGame.UI.Notification
{
    /// <summary>
    /// 連番を振った通知。描く側は同じ連番の再表示を弾く
    /// A notification with its sequence number; presenters drop a repeat of the same number
    /// </summary>
    public sealed class SequencedNotification
    {
        public readonly long Seq;
        public readonly NotificationMessagePack Message;

        public SequencedNotification(long seq, NotificationMessagePack message)
        {
            Seq = seq;
            Message = message;
        }
    }
}
```
```csharp
using System;
using Client.Network.API;
using MessagePack;
using Server.Event.Notification;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.UI.Notification
{
    /// <summary>
    /// サーバー通知とクライアント側の拒否を1本にまとめて連番を振る。接続時1回きりの移行告知だけは初期データとして持ち続ける
    /// Merges server notifications and client-side refusals into one numbered feed; only the once-per-connect migration notice is kept as initial data
    /// </summary>
    public sealed class NotificationFeed : IInitializable
    {
        private readonly IVanillaApiEvent _vanillaApiEvent;
        private readonly ClientLocalNotificationSource _localNotificationSource;
        private readonly Subject<SequencedNotification> _onNotified = new();
        private long _seq;

        // 除去件数の告知はサーバー接続時に1回しか来ない。描く側が未購読の間に流すと失われるので初期データとして持ち続ける
        // The prune notice arrives only once per server connection; it would be lost while a presenter is unsubscribed, so it is kept as initial data
        public SequencedNotification SaveMigrationNotice { get; private set; }
        public IObservable<SequencedNotification> OnNotified => _onNotified;

        public NotificationFeed(IVanillaApiEvent vanillaApiEvent, ClientLocalNotificationSource localNotificationSource)
        {
            _vanillaApiEvent = vanillaApiEvent;
            _localNotificationSource = localNotificationSource;
        }

        public void Initialize()
        {
            _vanillaApiEvent.SubscribeEventResponse(NotificationService.EventTag, OnServerNotification);

            // クライアント側の拒否も同じ表示へ流す
            // Relay client-side refusals through the same display
            _localNotificationSource.OnNotification.Subscribe(Publish);
        }

        private void OnServerNotification(byte[] payload)
        {
            Publish(MessagePackSerializer.Deserialize<NotificationMessagePack>(payload));
        }

        private void Publish(NotificationMessagePack message)
        {
            // 未定義のカテゴリは連番を消費せず落とす。throwは購読パイプを貫き配信を止める
            // Undefined categories are dropped without consuming a number; a throw would pierce the subscription pipe and halt delivery
            if (!Enum.IsDefined(typeof(NotificationCategory), message.Category))
            {
                Debug.LogWarning($"[NotificationFeed] dropped notification with unknown category: {message.Category}");
                return;
            }

            _seq++;
            var notification = new SequencedNotification(_seq, message);

            // 再接続で届いた新しい告知は新しい連番で置き換え、再接続のたびに出し直す契約を保つ
            // A notice from a reconnect replaces the old one with a new number, keeping the re-show-per-reconnect contract
            if (message.Category == NotificationCategory.SaveMigration) SaveMigrationNotice = notification;
            _onNotified.OnNext(notification);
        }
    }
}
```
（連番の消費条件は移す前と同じ。移す前は `NotificationCategoryTable.TryGetWebName` が偽＝`Enum.IsDefined` が偽のときだけ連番を消費せず落としていた）

- [x] **Step 2: `NotificationTopic` をアダプタにする**

`NotificationDto` は同じファイルに残す。クラス本体を次に置き換える:
```csharp
    public sealed class NotificationTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "notification.events";

        private readonly WebSocketHub _hub;
        private readonly NotificationFeed _feed;
        private readonly IDisposable _subscription;

        public NotificationTopic(WebSocketHub hub, NotificationFeed feed)
        {
            _hub = hub;
            _feed = feed;
            _subscription = _feed.OnNotified.Subscribe(Publish);
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            // 通常の通知は揮発で再生しない。除去件数の告知だけを返し、同じseqの再表示はWeb側が弾く
            // Ordinary notifications are transient; only the prune notice is returned and the web drops a repeat of the same seq
            var notice = _feed.SaveMigrationNotice;
            if (notice == null || !TrySerialize(notice, out var json)) return UniTask.FromResult("{}");
            return UniTask.FromResult(json);
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }

        private void Publish(SequencedNotification notification)
        {
            if (TrySerialize(notification, out var json)) _hub.Publish(TopicName, json);
        }

        // Webのカテゴリ名が引けない通知は送らずエラーログに残す。feedが定義済みに絞っているので、ここで落ちるのは対応表の追加漏れだけ
        // A notification without a web category name is not sent and is logged as an error; the feed already narrowed to defined categories, so only a missing table entry lands here
        private static bool TrySerialize(SequencedNotification notification, out string json)
        {
            var message = notification.Message;
            if (!NotificationCategoryTable.TryGetWebName(message.Category, out var webCategory))
            {
                Debug.LogError($"[NotificationTopic] no web category name for {message.Category}; notification seq {notification.Seq} was not sent");
                json = null;
                return false;
            }
            json = WebUiJson.Serialize(ToDto(notification, webCategory));
            return true;
        }

        private static NotificationDto ToDto(SequencedNotification notification, string webCategory)
        {
            var message = notification.Message;
            return new NotificationDto
            {
                Seq = notification.Seq,
                Category = webCategory,
                MessageId = message.MessageId,
                MessageParams = message.MessageParams,
                ItemId = message.ItemId == ItemMaster.EmptyItemId ? null : (int?)message.ItemId.AsPrimitive(),
                // countは獲得通知だけが持つ。他カテゴリはキーごと省略しWeb側の判別unionを保つ
                // Only earned notifications carry a count; other categories omit the key entirely to keep the web's discriminated union honest
                Count = message.Category == NotificationCategory.ItemEarned ? message.Count : (int?)null,
            };
        }
    }
```
（`TryGetWebName` が偽になるのは未定義値のときだけで、feed が先に落とすので通常は到達しない。feed と表の判定がずれたときに null のカテゴリを黙って送らないための防御。表の追加漏れは移す前と同じく、default の無い switch 式の例外と CS8509 の警告で気づく）
using を `Client.Game.InGame.UI.Notification`・`Client.WebUiHost.Boot`・`Client.WebUiHost.Common`・`Core.Master`・`Cysharp.Threading.Tasks`・`Server.Event.Notification`・`System`・`UniRx`・`UnityEngine` に整理する（`MessagePack`・`Client.Network.API` は不要になる）。

- [x] **Step 3: 登録と binder**

`MainGameUiStateRegistration.Register` に足す（`using Client.Game.InGame.UI.Notification;`）:
```csharp
            // 通知の連番と移行告知。サーバー通知とローカル拒否を購読するため起動時に作る
            // Notification numbering and the migration notice; built at startup because they subscribe to server notifications and local refusals
            builder.RegisterEntryPoint<NotificationFeed>().AsSelf();
```
`WebUiGameBinder` 115 行目:
```csharp
            hub.RegisterTopic(NotificationTopic.TopicName, new NotificationTopic(hub, resolver.Resolve<NotificationFeed>()));
```

- [x] **Step 4: wire 契約テストの組み立てを直す**

`WireContractNotificationTest` に組み立ての補助を足し、`new NotificationTopic(hub, vanillaApiEvent, source)` の 3 か所をこれに置き換える（照合内容は変えない）:
```csharp
        // 本番と同じくfeedを初期化してからtopicを載せる
        // Initialize the feed before mounting the topic, as production does
        private static NotificationTopic CreateTopic(WebSocketHub hub, CapturingVanillaApiEvent vanillaApiEvent, ClientLocalNotificationSource source)
        {
            var feed = new NotificationFeed(vanillaApiEvent, source);
            feed.Initialize();
            return new NotificationTopic(hub, feed);
        }
```
`LocalUndoNotificationIsPublishedAndUnsubscribedOnDispose` の「Dispose 後は配信しない」の照合は、topic が feed の購読を外すことで従来どおり成り立つ。

- [x] **Step 5: プローブ**

`Hostless/NotificationStateProbe.cs`:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.UI.Notification;
using NUnit.Framework;
using Server.Event.Notification;
using UniRx;
using VContainer;

namespace Client.Tests.EditModeInPlayingTest.Hostless
{
    // ローカルの拒否通知がWebホスト無しでも連番付きでfeedに流れることを確かめる
    // Verifies a local refusal flows through the feed with a sequence number, without the web host
    internal static class NotificationStateProbe
    {
        public static void AssertAvailable(IObjectResolver resolver)
        {
            var feed = resolver.Resolve<NotificationFeed>();
            var received = new List<SequencedNotification>();
            using var subscription = feed.OnNotified.Subscribe(received.Add);

            var message = NotificationMessagePack.CreateOperationDenied("denied.undoRestoreSkipped", new[] { "2" });
            resolver.Resolve<ClientLocalNotificationSource>().Notify(message);

            Assert.AreEqual(1, received.Count, "ローカル通知がfeedに流れない");
            Assert.AreSame(message, received[0].Message);
            Assert.Greater(received[0].Seq, 0);
        }
    }
}
```
テストの `Body()` 末尾に `NotificationStateProbe.AssertAvailable(resolver);` を足す。

- [x] **Step 6: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.WebUi\.WireContractNotificationTest|Hostless\.WebUiHostlessStateAvailabilityTest" --timeout-seconds 1200`
Expected: ErrorCount 0 / 全 PASS

- [x] **Step 7: コミット**

```bash
git add -A moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Notification moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Starter/Registration moorestech_client/Assets/Scripts/Client.Tests
git commit -m "refactor(notification): 通知の連番と移行告知をClient.GameのNotificationFeedへ移しtopicをアダプタにする"
```

---

## Task 8: 出展モードの言語ゲートを Client.Game へ移す（DI には載せない）

**Files:**
- Modify（移動）: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/EventMode/EventLanguageGate.cs` → `moorestech_client/Assets/Scripts/Client.Game/InGame/EventMode/EventLanguageGate.cs`（`git mv` で `.meta` も）
- Modify（using のみ）: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/EventMode/EventLanguageGateBinder.cs`、`moorestech_client/Assets/Scripts/Client.WebUiHost/Game/EventMode/EventLanguageGateTopic.cs`、`moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/EventMode/EventLanguageGateActions.cs`、`moorestech_client/Assets/Scripts/Client.Starter/EventMode/EventModeStartGate.cs`
- Modify（using のみ）: `moorestech_client/Assets/Scripts/Client.Tests/EventMode/EventModeStartGateTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/EventMode/EventLanguageGateTopicTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/EventMode/EventLanguageGateTest.cs`

**Interfaces:**
- Consumes: なし
- Produces: `Client.Game.InGame.EventMode.EventLanguageGate`: `public EventLanguageGate(bool startsWaiting)`、`public bool IsWaitingSelection { get; private set; }`、`public IObservable<Unit> OnWaitingChanged`、`public UniTask WaitForSelectionAsync()`、`public EventLanguageSelectionResult TrySelectLanguage(string languageCode)`、`enum EventLanguageSelectionResult`（同じファイル）。作り手・寿命・渡し方（`EventModeStartGate` が作り、`EventLanguageGateBinder.Bind(hub, bool)` で Web の topic/action へ束ねる）は変えない

- [x] **Step 1: ゲートを移して公開範囲を広げる**

```bash
S=moorestech_client/Assets/Scripts
mkdir -p $S/Client.Game/InGame/EventMode
git mv $S/Client.WebUiHost/Game/EventMode/EventLanguageGate.cs $S/Client.Game/InGame/EventMode/EventLanguageGate.cs
git mv $S/Client.WebUiHost/Game/EventMode/EventLanguageGate.cs.meta $S/Client.Game/InGame/EventMode/EventLanguageGate.cs.meta
```
namespace を `Client.Game.InGame.EventMode` にし、`internal` の 4 メンバ（コンストラクタ・`IsWaitingSelection`・`OnWaitingChanged`・`TrySelectLanguage`）と `enum EventLanguageSelectionResult` を `public` にする（別アセンブリ `Client.WebUiHost` の topic・action が触るため）。2 行コメント「待機状態と選択の受付はこのアセンブリ内のtopic・actionだけが触る」と「開始側（Client.Starter）が待ち合わせる唯一の窓口なのでここだけpublicに残す」を次に置き換える:
```csharp
        // 待機状態と選択の受付は描く側（Webのtopic・action、将来のUITK）が触る。ゲームのDIより前に要るのでDIには載せず、開始側が作って描く側へ渡す
        // Presenters (the web topic/action, later UITK) touch the waiting state and selection intake; it is needed before the game DI, so the starter builds it and hands it to presenters instead of registering it in DI
```
ロジックは変えない。

- [x] **Step 2: 参照側の using を直す**

上の Files の 7 ファイルのうち `EventLanguageGate`・`EventLanguageSelectionResult` を参照するものに `using Client.Game.InGame.EventMode;` を足す（`Client.WebUiHost.Game.EventMode` の using は Binder・Topic を使うファイルでは残す）。コンパイルエラーで残りを拾う。

- [ ] **Step 3: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.EventMode\.|EditModeInPlayingTest\.EventLanguageGateCursorTest" --timeout-seconds 1500`
Expected: ErrorCount 0 / 全 PASS

- [x] **Step 4: コミット**

```bash
git add -A moorestech_client/Assets/Scripts/Client.Game/InGame/EventMode moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Starter moorestech_client/Assets/Scripts/Client.Tests
git commit -m "refactor(event-mode): 出展モードの言語ゲートをClient.Gameへ移しWeb以外の描く側から使えるようにする"
```

---

## Task 9: 時刻表の取得と開いている列車の追跡を Client.Game の DI へ移す

**Files:**
- Modify（移動）: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Inventory/TrainTimetableFetcher.cs`・`OpenTrainTimetableTracker.cs`、`moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/Train/OpenTrainUnitResolver.cs` → `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Timetable/`（`git mv` で `.meta` も）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockInventoryTopic.cs:58-73,95-105`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:84-85`
- Modify（using のみ）: `Client.WebUiHost/Game/Actions/Train/TrainWebUiActionRegistration.cs`・`TrainTimetableOpenActionHandler.cs`・`TrainTimetableActions.cs`、`Client.WebUiHost/Game/Topics/TrainInventoryDtoFactory.cs`、`Client.WebUiHost/Game/Topics/BlockDetail/Train/TrainTimetableDtoBuilder.cs`、`Client.Tests/WebUiHost/Train/TrainTimetableFetcherTest.cs`（すべて `moorestech_client/Assets/Scripts/` 配下）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiStateRegistration.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Hostless/TimetableStateProbe.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Hostless/WebUiHostlessStateAvailabilityTest.cs`

**Interfaces:**
- Consumes: Task 2 の `UIStateControl.OnStateChanged`、Task 4 の `MainGameUiStateRegistration.Register`
- Produces: namespace `Client.Game.InGame.Train.Timetable` の `TrainTimetableFetcher`（公開 API は移す前と同じ）、`OpenTrainTimetableTracker : IInitializable`（`IObservable<Unit> OnRepublishRequested`、`void Initialize()`。`IDisposable` は外す）、`OpenTrainUnitResolver`（静的、API 不変）。`BlockInventoryTopic` のコンストラクタは `OpenTrainTimetableTracker openTrainTimetableTracker` を受け取る（自分で作らない）

- [x] **Step 1: 3 ファイルを移す**

```bash
S=moorestech_client/Assets/Scripts
D=$S/Client.Game/InGame/Train/Timetable
for f in Topics/Inventory/TrainTimetableFetcher Topics/Inventory/OpenTrainTimetableTracker Topics/BlockDetail/Train/OpenTrainUnitResolver; do
  git mv $S/Client.WebUiHost/Game/$f.cs $D/$(basename $f).cs
  git mv $S/Client.WebUiHost/Game/$f.cs.meta $D/$(basename $f).cs.meta
done
```
3 ファイルの namespace を `Client.Game.InGame.Train.Timetable` にし、`using Client.WebUiHost.Game.Topics.BlockDetail;` を消す（同じ namespace になる）。

- [x] **Step 2: `OpenTrainTimetableTracker` を entry point にする**

コンストラクタは依存の保持だけにし、購読をすべて `Initialize()` へ移す。`IDisposable`・`Dispose()`・`_subscriptions` を外す（ゲーム寿命）。`timetables` はフィールド `_timetables` に保持する:
```csharp
    public sealed class OpenTrainTimetableTracker : IInitializable
    {
        private readonly SubInventoryState _subInventoryState;
        private readonly UIStateControl _uiStateControl;
        private readonly TrainUnitClientCache _trainUnitClientCache;
        private readonly IClientTrainTimetableLookup _timetables;
        private readonly TrainTimetableFetcher _fetcher;
        private readonly Subject<Unit> _onRepublishRequested = new();
        private TrainUnitInstanceId? _openTrainUnitInstanceId;
        public IObservable<Unit> OnRepublishRequested => _onRepublishRequested;

        public OpenTrainTimetableTracker(SubInventoryState subInventoryState, UIStateControl uiStateControl, TrainUnitClientCache trainUnitClientCache, IClientTrainTimetableLookup timetables, TrainTimetableFetcher fetcher)
        {
            _subInventoryState = subInventoryState;
            _uiStateControl = uiStateControl;
            _trainUnitClientCache = trainUnitClientCache;
            _timetables = timetables;
            _fetcher = fetcher;
        }

        public void Initialize()
        {
            // 開閉で取得状態を忘れ、開いている列車を解決し直す
            // Forget the fetch state on open/close (UI-state transitions) and re-resolve the open train
            _uiStateControl.OnStateChanged.Subscribe(_ => OnUiStateChanged());
            // 開いている列車の時刻表更新と取得失敗で再配信する
            // Republish on the open train's timetable updates and on fetch failures
            _timetables.OnTimetableUpdated
                .Where(id => _openTrainUnitInstanceId == id)
                .Subscribe(_ => _onRepublishRequested.OnNext(Unit.Default));
            _fetcher.OnFetchFailed.Subscribe(_onRepublishRequested.OnNext);
            // 所属列車が変わったら再配信・再取得する
            // Republish and refetch when coupling, splitting, or a late car snapshot changes the owning train
            _trainUnitClientCache.OnUnitApplied.Subscribe(_ => OnTrainUnitApplied());
            _trainUnitClientCache.OnUnitRemoved.Subscribe(_ => OnTrainUnitApplied());
        }
```
`OnUiStateChanged(UIStateEnum state)` は引数を使っていないので `OnUiStateChanged()` にする。`OnTrainUnitApplied`・`ResolveOpenTrainUnitInstanceId` は変えない。クラスは `IInitializable` のため `using VContainer.Unity;` を足す。

- [x] **Step 3: `BlockInventoryTopic` が tracker を受け取るようにする**

コンストラクタ引数の `TrainTimetableFetcher timetableFetcher` の後に `OpenTrainTimetableTracker openTrainTimetableTracker` を足し、73 行目の `new OpenTrainTimetableTracker(...)` を `_openTrainTimetableTracker = openTrainTimetableTracker;` に、`Dispose()` の `_openTrainTimetableTracker.Dispose();` を削除する（`_trainTimetableSubscription.Dispose();` は残す）。`using Client.Game.InGame.Train.Timetable;` は既にある。

- [x] **Step 4: 登録と binder**

`MainGameUiStateRegistration.Register` に足す（`using Client.Game.InGame.Train.Timetable;`）:
```csharp
            // 時刻表タブの取得と、開いている車両の所属列車の追跡
            // Timetable-tab fetching and tracking of the open car's owning train
            builder.Register<TrainTimetableFetcher>(Lifetime.Singleton);
            builder.RegisterEntryPoint<OpenTrainTimetableTracker>().AsSelf();
```
`WebUiGameBinder` 84 行目を `var timetableFetcher = resolver.Resolve<TrainTimetableFetcher>();` にし、85 行目の `new BlockInventoryTopic(...)` の `timetableFetcher,` の後に `resolver.Resolve<OpenTrainTimetableTracker>(),` を足す。`TrainWebUiActionRegistration.Register(hub, subInventoryState, trainUnitClientCache, timetableFetcher);` の行（188 行目）は変えない（Tier9 の挿入行と隣り合うため）。

- [x] **Step 5: using を直す**

移した 3 型を参照する WebUiHost のファイルと `TrainTimetableFetcherTest` に `using Client.Game.InGame.Train.Timetable;` を足し、`using Client.WebUiHost.Game.Topics.BlockDetail;` が `OpenTrainUnitResolver` のためだけにあったなら消す。コンパイルエラーで残りを拾う。

- [x] **Step 6: プローブ**

`Hostless/TimetableStateProbe.cs`:
```csharp
using Client.Game.InGame.Train.Timetable;
using Cysharp.Threading.Tasks;
using Game.Train.Unit;
using NUnit.Framework;
using VContainer;

namespace Client.Tests.EditModeInPlayingTest.Hostless
{
    // 時刻表の取得役がWebホスト無しでもサーバーへ要求を出し、存在しない列車を取得不可と判定することを確かめる
    // Verifies the timetable fetcher sends a request and marks a nonexistent train unavailable, without the web host
    internal static class TimetableStateProbe
    {
        public static async UniTask AssertAvailableAsync(IObjectResolver resolver)
        {
            Assert.IsNotNull(resolver.Resolve<OpenTrainTimetableTracker>());
            var fetcher = resolver.Resolve<TrainTimetableFetcher>();
            var unknownTrain = TrainUnitInstanceId.Create();

            fetcher.RequestForOpenedTab(unknownTrain);
            Assert.IsTrue(fetcher.IsFetchInFlight(unknownTrain), "時刻表の取得要求が出ない");

            // サーバーに無い列車は「列車なし」か「応答なし→1回再試行」のどちらでも取得不可に落ち着く
            // A train missing on the server settles as unavailable via either "no train" or "no response then one retry"
            for (var i = 0; i < 600 && !fetcher.IsUnavailable(unknownTrain); i++) await UniTask.Yield();
            Assert.IsTrue(fetcher.IsUnavailable(unknownTrain), "存在しない列車が取得不可にならない");
            fetcher.Close();
        }
    }
}
```
（`TrainUnitInstanceId` の namespace が `Game.Train.Unit` でなければ、`TrainTimetableFetcher.cs` の using にそろえる）
テストの `Body()` 末尾に `await TimetableStateProbe.AssertAvailableAsync(resolver);` を足す。

- [ ] **Step 7: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.WebUiHost\.|Client\.Tests\.WebUi\.|Hostless\.WebUiHostlessStateAvailabilityTest" --timeout-seconds 1500`
Expected: ErrorCount 0 / 全 PASS

- [ ] **Step 8: コミット**

```bash
git add -A moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Timetable moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Starter/Registration moorestech_client/Assets/Scripts/Client.Tests
git commit -m "refactor(train-timetable): 時刻表の取得役と開いている列車の追跡をClient.GameのDIへ移す"
```

---

## Task 10: ブロック詳細の監視判断とネットワーク情報を Client.Game の DI へ移す（Tier9 と重なるので最後）

**Files:**
- Modify（移動）: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockNetworkInfoCache.cs` → `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/BlockDetail/BlockNetworkInfoCache.cs`（`git mv` で `.meta` も）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/BlockDetail/OpenBlockDetailTracker.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockInventoryTopic.cs`（全面）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/FilterSplitterActions.cs`
- Modify（using のみ）: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockDetailDtoBuilder.cs`、`moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Inventory/BlockInventoryDtoFactory.cs`、`moorestech_client/Assets/Scripts/Client.Tests/WebUi/BlockDetail/GearToElectricDetailDtoBuilderTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/WebUi/BlockDetail/FluidBlockDetailDtoBuilderTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:81-86,184-185`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiStateRegistration.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Hostless/BlockDetailStateProbe.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Hostless/WebUiHostlessStateAvailabilityTest.cs`

**Interfaces:**
- Consumes: Task 2 の `UIStateControl.OnStateChanged`、Task 9 の `OpenTrainTimetableTracker`・`TrainTimetableFetcher`（`BlockInventoryTopic` のコンストラクタ引数）、Task 4 の `MainGameUiStateRegistration.Register`
- Produces:
  - `Client.Game.InGame.UI.BlockDetail.BlockNetworkInfoCache`: 公開 API は移す前と同じ。ただし `event Action OnUpdated` → `IObservable<Unit> OnUpdated`
  - `Client.Game.InGame.UI.BlockDetail.OpenBlockDetailTracker : IInitializable`: `BlockGameObject TrackedBlock { get; private set; }`（サブインベントリで開いているブロック。開いていなければ null）、`IObservable<BlockGameObject> OnTrackedBlockChanged`（追跡を張り替えた直後に新しい `TrackedBlock` を流す。外したときは null）、`IObservable<Unit> OnTrackedBlockStateChanged`（追跡中ブロックのブロック状態イベント）、`void Initialize()`。状態を書き換える口は public に出さない（読み手は読むだけ）
  - `BlockInventoryTopic(WebSocketHub hub, UIStateControl uiStateControl, SubInventoryState subInventoryState, OpenBlockDetailTracker tracker, BlockNetworkInfoCache networkCache, TrainUnitClientCache trainUnitClientCache, IClientTrainTimetableLookup timetables, TrainTimetableFetcher timetableFetcher, OpenTrainTimetableTracker openTrainTimetableTracker, BeltTransportClientCache beltCache, FluidNetworkClientCache fluidCache)`
  - `FilterSplitterSetModeActionHandler(SubInventoryState, BlockNetworkInfoCache)`、`FilterSplitterSetFilterItemActionHandler(SubInventoryState, LocalPlayerInventoryController, BlockNetworkInfoCache)`

**分け方（どちらに何が残るか）**: tracker（Client.Game）は「どのブロックを開いているか」と「そのブロックの何を監視するか」（ブロック状態イベントの購読、電力網・ギア網・フィルタ分岐器のネットワーク取得）だけを持つ。Web 帯域のための再配信方針（ギア連続値の 4 フレーム間引き、ベルト統計の窓での出し直し、パイプの中身を前回の配信と比べること）はアダプタ（`BlockInventoryTopic`）に残す。毎フレーム読む UITK はこれらを要らない（判断記録 D6・D7）。

- [ ] **Step 1: Tier9 の 2 本がマージ済みかを確かめ、済んでいれば先に取り込む**

Run: `git fetch origin && gh pr view 1551 --json state -q .state && gh pr view 1552 --json state -q .state`
Expected: どちらかが `MERGED` なら `git merge origin/master` → `uloop compile` → コミットしてから次へ。`OPEN` のままなら取り込まずに進む（衝突は閉じタスクの pr-create が解く。判断記録 D5）

- [ ] **Step 2: `BlockNetworkInfoCache` を移して UniRx にする**

```bash
S=moorestech_client/Assets/Scripts
mkdir -p $S/Client.Game/InGame/UI/BlockDetail
git mv $S/Client.WebUiHost/Game/Topics/BlockDetail/BlockNetworkInfoCache.cs $S/Client.Game/InGame/UI/BlockDetail/BlockNetworkInfoCache.cs
git mv $S/Client.WebUiHost/Game/Topics/BlockDetail/BlockNetworkInfoCache.cs.meta $S/Client.Game/InGame/UI/BlockDetail/BlockNetworkInfoCache.cs.meta
```
namespace を `Client.Game.InGame.UI.BlockDetail` にし、`public event Action OnUpdated;` を次に、`OnUpdated?.Invoke();` をすべて `_onUpdated.OnNext(Unit.Default);` にする（`using UniRx;` を足す）:
```csharp
        private readonly Subject<Unit> _onUpdated = new();
        public IObservable<Unit> OnUpdated => _onUpdated;
```
ポーリング・取得・ガードのロジックは変えない。

- [ ] **Step 3: `OpenBlockDetailTracker` を書く**

移す前の `BlockInventoryTopic.BuildJson` の「開いているブロックの解決」と、`TrackBlock` のうち監視の張り替え（ブロック状態イベントの購読・ネットワーク取得）をここへ移す。ブロック状態イベントの購読は `ClientContext.VanillaApi.Event` と同じインスタンスである DI の `IVanillaApiEvent`、ブロックの解決は `ClientDIContext.BlockGameObjectDataStore` と同じインスタンスである DI の `BlockGameObjectDataStore` で行う。
```csharp
using System;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Network.API;
using Mooresmaster.Model.BlocksModule;
using Server.Event.EventReceive;
using UniRx;
using VContainer.Unity;

namespace Client.Game.InGame.UI.BlockDetail
{
    /// <summary>
    /// サブインベントリで開いているブロックを追い、そのブロック状態イベントの購読とネットワーク情報の取得を張り替える
    /// Tracks the block open in the sub-inventory and re-wires its block-state event subscription and network fetches
    /// </summary>
    public sealed class OpenBlockDetailTracker : IInitializable
    {
        private readonly UIStateControl _uiStateControl;
        private readonly SubInventoryState _subInventoryState;
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly BlockNetworkInfoCache _networkCache;
        private readonly IVanillaApiEvent _vanillaApiEvent;
        private readonly Subject<BlockGameObject> _onTrackedBlockChanged = new();
        private readonly Subject<Unit> _onTrackedBlockStateChanged = new();
        private IDisposable _blockStateSubscription;

        public BlockGameObject TrackedBlock { get; private set; }
        public IObservable<BlockGameObject> OnTrackedBlockChanged => _onTrackedBlockChanged;
        public IObservable<Unit> OnTrackedBlockStateChanged => _onTrackedBlockStateChanged;

        public OpenBlockDetailTracker(UIStateControl uiStateControl, SubInventoryState subInventoryState, BlockGameObjectDataStore blockGameObjectDataStore, BlockNetworkInfoCache networkCache, IVanillaApiEvent vanillaApiEvent)
        {
            _uiStateControl = uiStateControl;
            _subInventoryState = subInventoryState;
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _networkCache = networkCache;
            _vanillaApiEvent = vanillaApiEvent;
        }

        public void Initialize()
        {
            // 開閉（UIステート遷移）とスロット更新のたびに、開いているブロックを解決し直す
            // Re-resolve the open block on every open/close (UI-state transition) and slot update
            _uiStateControl.OnStateChanged.Subscribe(_ => Refresh());
            _subInventoryState.OnSubInventoryUpdated.Subscribe(_ => Refresh());
        }

        // SubInventory 状態かつ発生元がブロックのときだけ追跡する（列車等の非ブロックは追跡しない）
        // Track only in the SubInventory state with a block source (non-block sources like trains are not tracked)
        private void Refresh()
        {
            var open = _uiStateControl.CurrentState == UIStateEnum.SubInventory && _subInventoryState.CurrentSubInventory != null;
            if (open && _subInventoryState.CurrentSubInventorySource is BlockSubInventorySource blockSource && _blockGameObjectDataStore.TryGetBlockGameObject(blockSource.BlockPosition, out var block))
            {
                Track(block);
                return;
            }
            Track(null);
        }

        // 追跡ブロックを切り替え、state イベント購読とネットワーク取得を張り替える
        // Switch the tracked block, re-wiring the state-event subscription and network fetches
        private void Track(BlockGameObject block)
        {
            if (ReferenceEquals(TrackedBlock, block)) return;
            // 前のブロックの state イベント購読を解除する
            // Unsubscribe the previous block's state event
            _blockStateSubscription?.Dispose();
            _blockStateSubscription = null;
            TrackedBlock = block;
            if (block == null)
            {
                _networkCache.Clear();
                _onTrackedBlockChanged.OnNext(null);
                return;
            }

            // uGUI BlockGameObject と同じ per-block イベントタグを直接購読して再配信トリガにする
            // Directly subscribe the same per-block event tag as uGUI BlockGameObject as the republish trigger
            var eventTag = ChangeBlockStateEventPacket.CreateSpecifiedBlockEventTag(block.BlockPosInfo);
            _blockStateSubscription = _vanillaApiEvent.SubscribeEventResponse(eventTag, _ => _onTrackedBlockStateChanged.OnNext(Unit.Default));
            // ネットワーク所属はマスタの接続paramが答える。blockTypeの列挙は新種別を取りこぼす
            // The master's connection params answer which networks a block joins; enumerating blockTypes drops new kinds
            var param = block.BlockMasterElement.BlockParam;
            var electric = param is IElectricWireConnectParam;
            var gear = param is IGearConnectors;
            var filterSplitter = block.BlockMasterElement.BlockType == "FilterSplitter";
            _networkCache.Track(block, electric, gear, filterSplitter);
            _onTrackedBlockChanged.OnNext(block);
        }
    }
}
```
（`IVanillaApiEvent.SubscribeEventResponse` の戻り値が `IDisposable` であることは、移す前の `_blockStateSubscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(...)` と同じ。型名・namespace が合わなければ移す前の `BlockInventoryTopic.cs` の using にそろえる）

- [ ] **Step 4: `BlockInventoryTopic` をアダプタにする**

フィールド・コンストラクタ・`Dispose`・`OnTrackedBlockChanged`・`CapturePipeBaseline`・`HasPipeContentsChanged`・`OnTrackedBlockStateChanged`・`BuildJson` を次にする。`TrackBlock`、`NetworkCache` プロパティ、`_networkCache = new()`、`_trackedBlock`、`_blockStateSubscription`、個別の `IDisposable` フィールド群は削除する。`SchedulePublish`（INFRA-7 のフレーム末の畳み込み）と `PublishContinuousSample` はそのまま残す。
```csharp
        public const string TopicName = "block_inventory.current";
        private readonly WebSocketHub _hub;
        private readonly UIStateControl _uiStateControl;
        private readonly SubInventoryState _subInventoryState;
        private readonly OpenBlockDetailTracker _tracker;
        private readonly BlockNetworkInfoCache _networkCache;
        private readonly TrainUnitClientCache _trainUnitClientCache;
        private readonly IClientTrainTimetableLookup _timetables;
        private readonly TrainTimetableFetcher _timetableFetcher;
        private readonly BeltTransportClientCache _beltCache;
        private readonly FluidNetworkClientCache _fluidCache;
        private readonly CompositeDisposable _subscriptions = new();
        // 再配信の方針はWeb帯域のためのものなのでアダプタが持つ。追跡ブロックが変わるたびに導き直す
        // Republish policy exists for web bandwidth, so the adapter owns it and re-derives it whenever the tracked block changes
        private bool _sampleContinuously;
        private bool _republishOnBeltWindow;
        // パイプの中身はブロック状態イベントで届かずクライアント側のコピーが毎tick進めるので、連続値と同じ間隔で見て、量か流体の種類が前回の配信から変わった回だけ出し直す
        // Pipe contents do not arrive as block state events but advance in the client-side copy every tick, so they are sampled at the continuous-sample interval and republished only when the amount or fluid id changed since the last publish
        // 比較の基準は、追跡が変わった後に最初にDTOを組む瞬間の値で控える（配信した中身と同じ瞬間にそろえる）
        // The comparison baseline is captured when the first DTO after a tracking change is built, so it matches the published contents
        private bool _pipeBaselinePending;
        private bool _republishOnFluidReplica;
        private long _publishedPipeAmount;
        private FluidId _publishedPipeFluidId;
        private bool _publishScheduled;
        private bool _continuousSamplePending;
        private bool _disposed;

        public BlockInventoryTopic(WebSocketHub hub, UIStateControl uiStateControl, SubInventoryState subInventoryState, OpenBlockDetailTracker tracker, BlockNetworkInfoCache networkCache, TrainUnitClientCache trainUnitClientCache, IClientTrainTimetableLookup timetables, TrainTimetableFetcher timetableFetcher, OpenTrainTimetableTracker openTrainTimetableTracker, BeltTransportClientCache beltCache, FluidNetworkClientCache fluidCache)
        {
            _hub = hub;
            _uiStateControl = uiStateControl;
            _subInventoryState = subInventoryState;
            _tracker = tracker;
            _networkCache = networkCache;
            _trainUnitClientCache = trainUnitClientCache;
            _timetables = timetables;
            _timetableFetcher = timetableFetcher;
            _beltCache = beltCache;
            _fluidCache = fluidCache;

            // 追跡ブロックの張り替えで再配信の方針を導き直す（購読前に既に追跡中なら今の分も）
            // Re-derive the republish policy when the tracked block changes (and for one already tracked before subscribing)
            OnTrackedBlockChanged(tracker.TrackedBlock);
            tracker.OnTrackedBlockChanged.Subscribe(OnTrackedBlockChanged).AddTo(_subscriptions);
            tracker.OnTrackedBlockStateChanged.Subscribe(_ => OnTrackedBlockStateChanged()).AddTo(_subscriptions);
            // 搬出統計の5tickブロックが完成するたびに、開いているベルトの詳細を出し直す
            // Republish the open belt's detail each time a 5-tick block of the exit statistics completes
            beltCache.ExitStatistics.OnWindowAdvanced.Subscribe(_ =>
            {
                if (_republishOnBeltWindow) SchedulePublish();
            }).AddTo(_subscriptions);
            // 時刻表更新・取得失敗・所属列車の変化で再配信する
            // Republish on the open train's timetable updates, fetch failures, and owning-train changes
            openTrainTimetableTracker.OnRepublishRequested.Subscribe(_ => SchedulePublish()).AddTo(_subscriptions);
            // 開閉（UIステート遷移）とスロット更新の両方を購読して push する
            // Subscribe to open/close (UI-state transitions) and slot updates, then push
            uiStateControl.OnStateChanged.Subscribe(_ => SchedulePublish()).AddTo(_subscriptions);
            subInventoryState.OnSubInventoryUpdated.Subscribe(_ => SchedulePublish()).AddTo(_subscriptions);
            // ネットワーク取得完了でも再配信する
            // Republish when a network fetch completes
            networkCache.OnUpdated.Subscribe(_ => SchedulePublish()).AddTo(_subscriptions);
            // ギア連続値とパイプの中身はWS帯域のため4フレーム間隔に間引いて配信する（tickでなくフレーム。判断記録 D7）
            // Thin gear continuous values and pipe contents to every four frames for WS bandwidth (frames, not ticks; decision D7)
            Observable.IntervalFrame(4)
                .Where(_ => _continuousSamplePending || HasPipeContentsChanged())
                .Subscribe(_ => PublishContinuousSample())
                .AddTo(_subscriptions);
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _disposed = true;
            _subscriptions.Dispose();
        }

        private void OnTrackedBlockChanged(BlockGameObject block)
        {
            // ギアの連続値の間引きとベルト統計窓での出し直しは、追跡ブロックの接続paramで決まる（ネットワーク取得の判断はtrackerが持つ）
            // Gear sampling and belt-window republish follow the tracked block's connection params (the network-fetch decision belongs to the tracker)
            var param = block == null ? null : block.BlockMasterElement.BlockParam;
            _sampleContinuously = param is IGearConnectors;
            _republishOnBeltWindow = param is IBeltSpeedParam;
            _republishOnFluidReplica = false;
            _pipeBaselinePending = block != null;
        }

        // 開いた時点でコピーに載っていないパイプは、載るまで中身を出さない(調べ直さない)。載っていれば今の中身を配信済みとして控え、最初の配信は通常の開く経路に任せる
        // A pipe not yet in the copy when opened shows no contents until reopened (no re-check); when present, the current contents count as published and the first publish goes through the normal open path
        private void CapturePipeBaseline(BlockGameObject block)
        {
            _pipeBaselinePending = false;
            _republishOnFluidReplica = _fluidCache.TryGetNodeIndex(block.BlockPosInfo.OriginalPos, out var pipeNode);
            if (!_republishOnFluidReplica) return;
            _publishedPipeAmount = _fluidCache.Replica.Network.NodeAmounts[pipeNode];
            _publishedPipeFluidId = _fluidCache.Replica.Network.NodeFluidIds[pipeNode];
        }

        // 開いているパイプの量・流体の種類をクライアント側のコピーから読み、前回の配信と違えば控えて true を返す(変わらない間は送らない)
        // Read the open pipe's amount and fluid id from the client-side copy; when they differ from the last publish, remember them and return true (nothing is sent while unchanged)
        private bool HasPipeContentsChanged()
        {
            if (!_republishOnFluidReplica || !_fluidCache.TryGetNodeIndex(_tracker.TrackedBlock.BlockPosInfo.OriginalPos, out var pipeNode)) return false;
            var network = _fluidCache.Replica.Network;
            var amount = network.NodeAmounts[pipeNode];
            var fluidId = network.NodeFluidIds[pipeNode];
            if (amount == _publishedPipeAmount && fluidId == _publishedPipeFluidId) return false;
            _publishedPipeAmount = amount;
            _publishedPipeFluidId = fluidId;
            return true;
        }

        private void OnTrackedBlockStateChanged()
        {
            if (_sampleContinuously) _continuousSamplePending = true;
            else SchedulePublish();
        }

        private string BuildJson()
        {
            // SubInventory 状態かつ発生元がブロックのときだけ開いている扱い（列車等の非ブロックは閉）
            // Open only in the SubInventory state with a block source (non-block sources like trains are closed)
            var open = _uiStateControl.CurrentState == UIStateEnum.SubInventory;
            var sub = _subInventoryState.CurrentSubInventory;
            if (!open || sub == null) return WebUiJson.Serialize(new BlockInventoryDto { Open = false });

            // 列車も既存の統一SubInventoryを同じwireへ写し、並行インベントリ経路を作らない
            // Map trains from the unified SubInventory onto the same wire instead of creating a parallel inventory path.
            if (_subInventoryState.CurrentSubInventorySource is TrainSubInventorySource trainSource)
            {
                return WebUiJson.Serialize(TrainInventoryDtoFactory.Create(trainSource, sub, _trainUnitClientCache, _timetables, _timetableFetcher, ClientDIContext.BlockGameObjectDataStore));
            }

            // 開いているブロックの解決はtrackerが遷移・スロット更新のたびに済ませている。ここは読むだけにする
            // The tracker already resolves the open block on every transition and slot update; only read it here
            var block = _tracker.TrackedBlock;
            if (block == null || _subInventoryState.CurrentSubInventorySource is not BlockSubInventorySource blockSource || !IsStillPlaced(blockSource, block))
            {
                return WebUiJson.Serialize(new BlockInventoryDto { Open = false });
            }
            if (_pipeBaselinePending) CapturePipeBaseline(block);
            var dto = BlockInventoryDtoFactory.Create(blockSource, sub, block, _networkCache, _beltCache, _fluidCache);
            return WebUiJson.Serialize(dto);

            #region Internal

            // 撤去されたフレームから閉じる遷移（次のUpdate）までの間は、移す前と同じく閉として出す（撤去済みのGameObjectを読まない）
            // Between a removal and the closing transition on the next Update, publish closed as before (never read a removed GameObject)
            bool IsStillPlaced(BlockSubInventorySource source, BlockGameObject tracked)
            {
                return ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(source.BlockPosition, out var placed) && ReferenceEquals(placed, tracked);
            }

            #endregion
        }
```
（移す前の `Dispose` が呼んでいた `TrackBlock(null)` は、追跡が DI 側に移りホスト停止と無関係になったので呼ばない。判断記録 D6）。不要になった using（`Server.Event.EventReceive`・`Client.WebUiHost.Game.Topics.BlockDetail` 等）を整理し、`Client.Game.InGame.UI.BlockDetail` を足す。`Core.Master`（`FluidId`）・`Mooresmaster.Model.BlocksModule`（`IGearConnectors`・`IBeltSpeedParam`）・`Client.Game.InGame.Block`（`BlockGameObject`）は残る。

- [ ] **Step 5: フィルタ分岐器の action をキャッシュ直結にする**

`FilterSplitterSetModeActionHandler`・`FilterSplitterSetFilterItemActionHandler` の `BlockInventoryTopic _blockInventoryTopic` を `BlockNetworkInfoCache _networkCache` にし、`_blockInventoryTopic.NetworkCache.ApplyFilterSplitterSnapshot(response);` を `_networkCache.ApplyFilterSplitterSnapshot(response);` にする。`using Client.WebUiHost.Game.Topics;` を `using Client.Game.InGame.UI.BlockDetail;` に替える。

- [ ] **Step 6: 登録と binder**

`MainGameUiStateRegistration.Register` に足す（`using Client.Game.InGame.UI.BlockDetail;`）:
```csharp
            // 開いているブロックのネットワーク情報と、その監視の張り替え
            // Network aggregates of the open block, and re-wiring of its monitoring
            builder.Register<BlockNetworkInfoCache>(Lifetime.Singleton);
            builder.RegisterEntryPoint<OpenBlockDetailTracker>().AsSelf();
```
`WebUiGameBinder` の `blockInventoryTopic` 生成（85 行目）を次の 2 行にする:
```csharp
            var blockNetworkInfoCache = resolver.Resolve<BlockNetworkInfoCache>();
            var blockInventoryTopic = new BlockInventoryTopic(hub, uiStateControl, subInventoryState, resolver.Resolve<OpenBlockDetailTracker>(), blockNetworkInfoCache, trainUnitClientCache, resolver.Resolve<IClientTrainTimetableLookup>(), timetableFetcher, resolver.Resolve<OpenTrainTimetableTracker>(), resolver.Resolve<BeltTransportClientCache>(), resolver.Resolve<FluidNetworkClientCache>());
```
184-185 行目の `blockInventoryTopic` を `blockNetworkInfoCache` に替える（2 行だけ。前後の行は触らない）。

- [ ] **Step 7: using を直す**

`BlockDetailDtoBuilder.cs`・`BlockInventoryDtoFactory.cs`・`GearToElectricDetailDtoBuilderTest.cs`・`FluidBlockDetailDtoBuilderTest.cs` に `using Client.Game.InGame.UI.BlockDetail;` を足す。コンパイルエラーで残りを拾う。

- [ ] **Step 8: プローブ**

`Hostless/BlockDetailStateProbe.cs`:
```csharp
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.BlockDetail;
using Client.Game.InGame.UI.UIState;
using Client.Tests.EditModeInPlayingTest.BlockInventory;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Hostless
{
    // ブロックのサブインベントリを開くと、Webホスト無しでもtrackerがそのブロックを追い、ネットワーク情報を取得することを確かめる
    // Verifies opening a block's sub-inventory makes the tracker follow it and fetch network info, without the web host
    internal static class BlockDetailStateProbe
    {
        private const string BlockName = "TestElectricToGearGeneratorUI";

        public static async UniTask AssertAvailableAsync(IObjectResolver resolver)
        {
            var tracker = resolver.Resolve<OpenBlockDetailTracker>();
            var networkCache = resolver.Resolve<BlockNetworkInfoCache>();
            var uiStateControl = resolver.Resolve<UIStateControl>();

            // 到達範囲外で閉じないよう自機の隣に置く
            // Place it next to the player so the out-of-reach check does not close it
            var pos = Vector3Int.FloorToInt(PlayerSystemContainer.Instance.PlayerObjectController.Position) + new Vector3Int(2, 0, 0);
            PlaceBlock(BlockName, pos, BlockDirection.North);
            var blockGameObject = await WaitBlockGameObjectSpawn(pos);

            // UIStateControlに文脈付き遷移の入口は無い。先に状態だけSubInventoryへ入れ（文脈なしのエラーログが1回出る）、実プレイと同じ文脈で開き直す
            // UIStateControl has no entry for a transition with context; enter SubInventory first (one no-context error log), then reopen with the real-play context
            LogAssert.Expect(LogType.Error, "SubInventoryState: サブインベントリソースが指定されていません");
            uiStateControl.RequestTransition(UIStateEnum.SubInventory);
            for (var i = 0; i < 120 && uiStateControl.CurrentState != UIStateEnum.SubInventory; i++) await UniTask.Yield();
            Assert.AreEqual(UIStateEnum.SubInventory, uiStateControl.CurrentState);
            var subInventoryState = await BlockSubInventoryOpener.Open(blockGameObject);

            for (var i = 0; i < 120 && !ReferenceEquals(tracker.TrackedBlock, blockGameObject); i++) await UniTask.Yield();
            Assert.AreSame(blockGameObject, tracker.TrackedBlock, "サブインベントリを開いてもブロックを追跡しない");
            for (var i = 0; i < 600 && networkCache.Electric == null && networkCache.GearNetwork == null; i++) await UniTask.Yield();
            Assert.IsTrue(networkCache.Electric != null || networkCache.GearNetwork != null, "開いたブロックのネットワーク情報を取得しない");

            // 閉じたら追跡とネットワーク情報を手放す
            // Closing releases the tracking and the network info
            BlockSubInventoryOpener.Close(subInventoryState);
            uiStateControl.RequestTransition(UIStateEnum.GameScreen);
            for (var i = 0; i < 120 && tracker.TrackedBlock != null; i++) await UniTask.Yield();
            Assert.IsNull(tracker.TrackedBlock);
        }
    }
}
```
（`BlockSubInventoryOpener.Open` が `SubInventoryState.OnEnter` を直接呼んでも `OnSubInventoryUpdated` が発火しない場合は、`TrackedBlock` が張り替わらずここで落ちる。そのときは `BlockSubInventoryOpener.Open` の中身と `SubInventoryState` のどの時点で `OnSubInventoryUpdated` が出るかを読み、tracker の購読元が実プレイの開く経路（F キー→`RequestTransition`＋文脈→`OnEnter`→読込完了）で必ず発火するものかを確かめてから、プローブ側の開き方を実プレイ側に寄せる。tracker の購読元を変えたら判断記録 D6 に追記する。置いた位置が地形と重なって置けなければ `+ new Vector3Int(0, 0, 2)` などへずらし、理由を報告に書く）
テストの `Body()` 末尾に `await BlockDetailStateProbe.AssertAvailableAsync(resolver);` を足す。

- [ ] **Step 9: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.WebUi\.|Client\.Tests\.WebUiHost\.|EditModeInPlayingTest\.BlockInventory\.|Hostless\.WebUiHostlessStateAvailabilityTest" --timeout-seconds 1500`
Expected: ErrorCount 0 / 全 PASS（`ElectricToGearOutputModeActionTest`・`MachineRecipeSelectRoundTripTest`・`MachineModuleSlotRoundTripTest` は hub 経由で `block_inventory` 系 action を実行する回帰）

- [ ] **Step 10: 200 行・10 ファイルを確かめる**

Run: `wc -l moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockInventoryTopic.cs moorestech_client/Assets/Scripts/Client.Game/InGame/UI/BlockDetail/*.cs moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs`、`ls moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Hostless/*.cs | wc -l`
Expected: 各 200 行未満、Hostless は 10 ファイル以下。`BlockInventoryTopic.cs` は移す前 229 行で、`TrackBlock` と解決部分が抜けて 200 行を下回る見込み。下回らなければ、`OnTrackedBlockChanged`・`HasPipeContentsChanged` の再配信方針を同じディレクトリの `BlockInventoryRepublishPolicy`（アダプタ専用の内部クラス）へ分ける。`WebUiGameBinder.cs` は移す前 197 行。超えたら Task 10 の中で、トピック登録を責務ごとの静的登録クラスへ分ける（前例 `C4WebUiRegistration`・`HotbarWebUiRegistration`）

- [ ] **Step 11: コミット**

```bash
git add -A moorestech_client/Assets/Scripts/Client.Game/InGame/UI/BlockDetail moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Starter/Registration moorestech_client/Assets/Scripts/Client.Tests
git commit -m "refactor(block-detail): 開いているブロックの追跡とネットワーク情報をClient.GameのDIへ移しtopicをアダプタにする"
```

---

## Task 11: 回帰の確認（unityプレイ録画テストと C# テスト一式）

**Files:**
- Modify（repo 外）: `../moorestech_logs/harness/albof3/baseline.md`（変更後の列を足す）

**Interfaces:**
- Consumes: Task 1 のベースライン合否表、Task 2〜10 の全変更
- Produces: 変更前後の合否の突き合わせ（PR 本文に貼る）

- [ ] **Step 1: C# テスト一式**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.(WebUi|WebUiHost|EventMode|BugReport|Playtest|Player|UIState)\.|EditModeInPlayingTest\.(BlockInventory|Boot|BugReport|Hostless)\.|EditModeInPlayingTest\.(EventLanguageGateCursorTest|LocalPlayEmbeddedServerBootTest|RemotePlayServerBootTest)" --timeout-seconds 1500`
Expected: 全 PASS。`uloop get-logs --project-path ./moorestech_client --log-type Error` が、意図した 1 件（`SubInventoryState: サブインベントリソースが指定されていません`、Hostless のプローブ）以外 0 件

- [ ] **Step 2: Task 1 と同じ 5 本を流す**

Task 1 Step 2 と同じ手順・同じ 5 本と、Task 1 Step 2b で PASS したシナリオ。
Expected: ベースラインで PASS だったものは全部 PASS。動画で、研究画面・チャレンジ一覧・機械 UI・時刻表タブ・獲得通知が変更前と同じに描かれている（各本 1 枚ずつスクショを抜いて `baseline.md` に並べる）

- [ ] **Step 3: 食い違いの扱い**

ベースラインと合否が違うものは、原因をログの行か動画のフレームで特定してから直す（推測で直さない）。直したらそのシナリオだけ流し直す。

- [ ] **Step 4: 記録**

`baseline.md` に変更後の列を足して `../moorestech_logs` へコミット。`bd note moorestech-albof.3 "回帰: C#一式PASS・録画5本 <合否>"`

---

## Task 12: 全ブランチレビュー

- [ ] **必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

Codex ホストでは moores-code-review の `references/codex-host.md` の runner で実行する。レビュー指摘の反映がソースの判定経路・条件式・その評価時点に触れたら、このタスクの中で Task 11 の録画 5 本と `WebUiHostlessStateAvailabilityTest` を反映後のバイナリで流し直してから完了とする。

---

## Task 13: PR を作ってセッションを閉じられる状態にする

- [ ] **セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える**

PR 本文に次の節を入れる:
- 「変更前後の合否」（Task 11 の表）
- 「ユーザーに見てほしい判断」: 判断記録のうち「agent 既定（ユーザー未裁定）」の行をすべて列挙する
- 「本PR外のリファクタ提案」（設計検査の第3バケツ）: `Client.WebUiHost/Game/Actions/ElectricToGearActions.cs:30`・`MachineRecipeSelectionActions.cs:53`・`Train/TrainStationActions.cs:29`・`Train/TrainPlatformActions.cs:31` が `CurrentSubInventorySource is not BlockSubInventorySource → "block_not_open"` で「今開いているブロック」を個別に解決している。新設の `OpenBlockDetailTracker.TrackedBlock` と同じ役割なので、そちらを読む形へ寄せられる（本 PR では触らない）
- Tier9 #1551・#1552 との重なり（判断記録 D5）と、どちらかが先にマージされたら当 PR 側で解く旨

PR 作成後、`bd note moorestech-albof.3 "PR #<番号> 作成"`。moorestech は PR 作成までで止め、マージしない。PR を出したらこの worktree は閉じない（Codex セッションは PR 作成で完了。worktree の片付けはユーザーかコントローラが `moores-wt rm` で行う）。

---

## 判断記録（ADR）

参照: `.decisions/2026-10-18-CEFを撤去しUI-Toolkitへ全面移行する.md`、`.decisions/2026-09-05-uGUIビューの論理状態はUnity側の純ロジッククラスへ抽出する.md`（ADR 0052 の抽出 PR）、`.decisions/2026-09-05-uGUI撤去は抽出PRと削除PRの2本に分ける.md`、計画 `docs/uitk-migration/plan.md` §2.1 0-4・0-5・0-10、§3.2、§3.3 R4、§4.4（改訂版は `origin/docs/uitk-web-continues`、PR #1555）

| # | 判断 | 理由 | 出所 |
|---|---|---|---|
| D0 | 計画 §0 の残り裁定 1〜3 は計画の推奨（1=A 束単位で暗転のまま作り所有表で切り替える、2=A 第一弾は命綱、3=(a) `HttpListener`／不可なら (b) 遠隔実行専用の最小 Kestrel、確認=UI 演出は実時間）を前提に進める。本 PR の範囲（状態の移設）はどの裁定にも依存しない | ユーザー指示「動く前提で並列で開始して」 | agent 既定（ユーザー未裁定） |
| D1 | 移した状態クラスは ViewModel（不変の sealed class・`IReadOnlyReactiveProperty<XxxViewModel>`）にせず、移す前の形（getter＋UniRx の変化通知）のまま `Client.Game` へ置く | 計画 §3.2「分けるのは、その段で使う topic だけ」。段階 0 は UITK を作らないので型付きの購読元は「型付き getter＋UniRx」で足り、ViewModel 化は各束の段で使う topic だけ行う。抽出 PR を「移動と配線」に限るため。前例 `ProgressBarState`・`NetworkDisconnectState` | agent 既定（ユーザー未裁定） |
| D2 | `WebUiModalService` は interface を作らず、実装ごと `Client.Game` の `ModalRequestService`（DI singleton）へ移し static `Instance` を消す。計画 §2.1 0-4 の字面「`Client.Game` のインターフェースにする」とは置くものが違う（Phase 2.6 検査7 で強として検出） | 計画の「インターフェースにする」は `Client.Game` から呼べるようにする手段だった（`Client.Game`→`Client.WebUiHost` の参照は張れない）。実装は Web に依存しない純状態なので、実装ごと移せば目的（Web 無しでもモーダル状態が DI から取れる・static をやめる）を満たす。実装が 1 つの interface は受益者が無い（moores-reviewer-digest #10）。前例 `PauseMenuStateService`。計画の字面に合わせたい場合は、PR レビューで interface を足すだけで済む | agent 既定（ユーザー未裁定）。PR 本文「ユーザーに見てほしい判断」の先頭に置く |
| D3 | `BlueprintNameInputWebBridge` は Web 依存部分が無い（モーダルサービスを呼ぶだけ）ので全体を `Client.Game` の entry point `BlueprintNameInputModalBridge` へ移す。Web 無しでは BP 名入力を開くと保留モーダルが解決されず状態は開いたまま（移す前も Web 無しでは開いたまま何も起きない）。ローカライズキー名 `WebModal*` は変えない（マスタの CSV に及ぶため） | 状態部分だけを切り出すと中身が残らない | agent 既定（ユーザー未裁定） |
| D4 | 状態クラスは `IInitializable` の entry point（`RegisterEntryPoint<…>().AsSelf()`）にし、購読をコンストラクタでなく `Initialize()` で張る。登録は新設 `MainGameUiStateRegistration` に集める | Web の有無と無関係に起動時に作らないと、遅延生成で起動直後のイベント（完了・通知）を取りこぼす。前例 `NetworkDisconnectState`。イベント配信の開始（`VanillaApiEvent.InitializeDispatch`）は `StartGame` と `WebUiGameBinder.Bind` の後なので、購読開始が Bind 時から DI 構築時へ早まっても受け取るイベントは同じ | agent 既定（ユーザー未裁定） |
| D5 | Tier9 の open PR #1551（`feat/tier9-compute-data-loop`）・#1552（`feat/tier9-fusion-elevator-endgame`）との衝突を最小にする切り方: 両 PR が触るのは `WebUiGameBinder.cs` の action 登録行への 1〜2 行の挿入（`ElectricToGearSetOutputModeActionHandler` の後と `MachineRecipeSelectActionHandler` の後）、`BlockInventoryTopic.TrackBlock` の 2 行コメント削除、`BlockDetailDtoBuilder.cs` への 2〜3 行の挿入。そこで (a) Tier9 と重なるブロック詳細の移設を最後の実装タスク（Task 10）に回し、着手時に Tier9 がマージ済みなら先に master を取り込む、(b) `WebUiGameBinder` の action 登録ブロックでは Tier9 の挿入位置に隣り合う行（`TrainWebUiActionRegistration.Register` の行など）を変えず、変えるのは `FilterSplitter` の 2 行と `ResearchComplete` の 1 行だけにする、(c) `BlockDetailDtoBuilder.cs` は using の 1 行だけ、(d) `TrackBlock` の 2 行コメント削除は移動先の `OpenBlockDetailTracker.Track` で衝突解消時に Tier9 側に合わせてよい（どちらでも挙動は同じ） | ユーザー指示「plan を書く前に両 PR の差分を見て、衝突を最小にする切り方を判断記録に書く」。差分は `git diff origin/master...origin/<branch>` で確認（2026-10-26、両 PR とも OPEN） | agent 既定（ユーザー未裁定） |
| D6 | ブロック詳細の分け方: `OpenBlockDetailTracker` は「どのブロックを開いているか」と「何を監視するか」（ブロック状態イベントの購読・電力網／ギア網／フィルタ分岐器のネットワーク取得）だけを持ち、UI 状態遷移とサブインベントリ更新を自分で購読して解決し直す。読み口は `TrackedBlock` と変化通知だけで、解決し直すメソッドは private（アダプタは読むだけ）。Web 帯域のための再配信方針（ギア連続値の間引き・ベルト統計窓での出し直し・パイプの中身を前回配信と比べること）はアダプタに残す。挙動の差: 追跡の開始・解除が移す前の「フレーム末の BuildJson」から「遷移・スロット更新の直後（同じフレーム）」へ早まる。`OnStateChanged` は新ステートの `OnEnter` 後に発火し、中身の読み込み完了で `OnSubInventoryUpdated` が発火するので、解決結果（読み込み中は追跡しない→読み込み後に追跡）は移す前と同じ。開いているブロックが撤去された場合は `SubInventoryState` が次の Update で閉じて遷移を起こし tracker が外れる。撤去したフレームから閉じるまでの間に配信があると古いブロックを読みかねないので、アダプタは配信の直前に「そのブロックがまだ置かれているか」だけを読み取り専用で確かめ、移す前と同じく閉として出す。パイプ比較の基準も、移す前と同じく配信する DTO を組む瞬間に控える（追跡が変わった時点で控えると、同じフレームで流体が届いたときに配信値と基準がずれるため）。ホスト停止時に移す前は `TrackBlock(null)` で追跡を外していたが、追跡は DI 側に移ったので外さない（ホスト停止はゲーム終了時だけ） | Web 無しで状態を取れることが完了条件。配置検査（Phase 1.5）で、アダプタから状態を書き換える経路と Web 専用の再配信方針を Client.Game に持たせる形を違反として直した。配信タイミング（フレーム末の畳み込み・4 フレーム間引き）はアダプタに残るので Web 側の挙動は不変 | agent 既定（ユーザー未裁定） |
| D7 | `BlockInventoryTopic` のコメント「4tick 間隔」と実装 `Observable.IntervalFrame(4)` の食い違い: 意図は「連続値（トルク・RPM）は変化時 publish だと毎フレーム出るので、固定間隔で間引いて WS 帯域とギア網走査を抑える」（`docs/webui/archive/cef-webui-migration-todo.md:173`「例: 4tick」、`docs/webui/plans/phase-b1-gear-blocks.md:25`、導入コミット `5f4ef4af40`）。「4tick」は間隔の例示で、単位の意図は無かった。実装（4 フレーム）を変えず、コメントを「4 フレーム間隔（WS 帯域の間引き）」に直す。間引きは計画 §3.2 どおりアダプタ側に残し、UITK では毎フレーム読む | 挙動は変えない指示。間引きは Web 帯域のためのもので状態ではない | agent 既定（ユーザー未裁定） |
| D8 | `EventLanguageGate` は型だけを `Client.Game/InGame/EventMode/` へ移し（メンバーを public に）、DI には載せない。作り手（`EventModeStartGate`、DI 構築より前）と、Web の topic/action への渡し方（`EventLanguageGateBinder.Bind(hub, bool)`）は変えない。計画 §2.1 0-4 は移す先を「Client.Game の DI」としているが、同じ計画 §5 は言語ゲートを「DI より前に出す全画面ゲート」とし、UITK 側の出し方を段階 5 で決めるとしている | ゲートは `StartGame` より前に待機が終わるので、DI から取れる時点では常に待機していない。DI 側に使い手がいない（配置検査 Phase 2 で違反として検出）。Web から外す目的（Web 以外の描く側が触れる型にする）は型の移動で満たす | agent 既定（ユーザー未裁定）。PR 本文「ユーザーに見てほしい判断」に置く |
| D9 | 0-10 のスイッチは SessionState の印を起動時に消費する形（`WebUiHostEditorSkip.Apply()`）。メニュー項目は作らない | 前例 `PlaytestStartGateBypass`（読んだら消費・EnteredEditMode で消す）。印が次の Play に漏れると hub 前提のテストが落ちるため消費型にする。人が手で切り替える入口は CEF 無し起動ラダー（§4.4）を DSL に載せる albof.6 で必要になったら足す（YAGNI） | agent 既定（ユーザー未裁定） |
| D10 | Web 無しのブロック詳細プローブは、`UIStateControl.RequestTransition(SubInventory)`（文脈なし。`SubInventoryState` がエラーログを 1 回出す）で状態を先に入れてから `BlockSubInventoryOpener.Open`（`OnEnter` に文脈を渡し、読み込み完了で `OnSubInventoryUpdated` が出る）で実プレイと同じ文脈で開き直す。エラーログは `LogAssert.Expect` で明示する | `UIStateControl` に文脈付き遷移の入口が無く、テスト専用 public を足すのは規約違反。入力注入で F キーを押す経路はテストの範囲を超える | agent 既定（ユーザー未裁定） |
| D11 | 回帰の録画は「移した topic に触る」5 本（`ui-state-transition-matrix`・`walk-while-menus-open`・`ui-place-then-open-machine`・`train-timetable-via-ui`・`vein-hand-mining-smoke`）を変更前後で突き合わせる。研究・チャレンジ・通知・BP を直接触る 4 本（`research-node-state-label`・`server-notification-smoke`・`blueprint-copy-paste-via-ui`・`blueprint-right-click-delete-via-ui`）は bd `moorestech-wwxy` で壊れていると記録されているので、変更前に 1 回だけ流し、PASS したものだけ回帰対象に加える（Task 1 Step 2b）。チャレンジ完了の経路は録画で踏まないので単体テスト（Task 6 Step 5）で押さえる | ランタイム挙動（UI）に触れるため録画は必須。一律に外さず実測で決める（user-simulator の予測: 画面に出る変更で録画を省くと問われる）。壊れたシナリオの修理は本 PR の範囲外 | agent 既定（ユーザー未裁定） |
| D12 | ランタイム挙動に触れる判定: 触れる（UI 状態遷移の通知機構・topic の配信経路・起動順）。unityプレイ録画テスト（Task 1・11）と EditModeInPlayingTest（Task 3〜10）の両方を入れる | moores-writing-plans の録画テスト規定 | 本 plan |
| D13 | 設計検査 Phase 2.6 の発火への裁定（ユーザー不在のため agent が決めた）。強: 5-A（tracker の null と bool の並立）→ bool を tracker から外したので解消。5-I（`TryGetWebName` の bool を捨てる）→ 捨てずに、名前が引けなければ送らずエラーログ（Task 7）。検査7（D2 と計画の字面の食い違い）→ D2 のまま、PR 本文で見てもらう。弱: `TryConsumePipeContentsChange` の 3 理由 → tracker から外しアダプタの移す前のコードのまま（解消）。ゲートの合成 bool → ゲート作成を変えないので解消。「開いているか」の判定がアダプタと tracker の 2 か所 → アダプタは列車の分岐と閉の判定にだけ使い、ブロックの解決は tracker だけが行う形にしたのでこのまま。`CancelPendingRequest` の呼び口 2 つ（BP 入力が閉じたとき／Web ホスト停止時）→ 所有者はサービス、呼ぶのは別の理由の 2 者で、移す前と同じ（移す前はホスト停止時に static 経由で呼んでいた）のでこのまま。終了経路の削除 3 か所 → いずれもゲーム寿命へ移した物の破棄で、AGENTS.md「ゲーム寿命の dispose は考慮不要」に当たるのでこのまま。Client.Game の具象型を WebUiHost が直接受け取る → 既存の WebUiHost は Client.Game の具象（`ProgressBarState` 等）を直接受け取っており、契約層は無いのでこのまま。再検査で追加: 5-E（tracker が撤去を購読しない）→ アダプタが配信直前に置かれているかを確かめる形で解消（D6）。検査7（D8 と計画 §2.1「移す先は DI」の食い違い）→ D8 のまま。上位計画 `docs/uitk-migration/plan.md` は本レーンの持ち物ではないので書き換えず、PR 本文で見てもらう。検査6（`param is IGearConnectors` と `TryGetNodeIndex` が tracker とアダプタの両方に出る）→ tracker はネットワーク取得の要否、アダプタは Web 帯域の間引きの要否で、問いが違うのでこのまま | AskUserQuestion 禁止の指示。各項目の理由は左に記載 | agent 既定（ユーザー未裁定） |
| D14 | `ResearchNodeStateStore` の初期値は移す前と同じく空（`InitialHandshakeResponse` の研究状態から初期化しない）。研究画面へ入るたびにサーバーから取り直す駆動も同じ | 抽出 PR は挙動を変えない。handshake から初期化すると、研究画面を開く前に Web へ送る `research.tree` の中身が変わる | agent 既定（ユーザー未裁定） |
| D15 | 時刻表の fetcher・tracker・`OpenTrainUnitResolver` は `Client.Game/InGame/Train/Timetable/`、ブロック詳細の tracker・cache は `Client.Game/InGame/UI/BlockDetail/` に置く。同じ「開いているサブインベントリの追跡」役なのに置き場が割れる | 時刻表の型（Datastore・EventHandler）は既に `Train/Timetable` にあり、時刻表の取得役をそこから離すと時刻表の型が UI 側と列車側に割れる。ブロック詳細には既存のドメインディレクトリが無いので UI 側に置く | agent 既定（ユーザー未裁定） |
| D16 | `ModalTopic.Dispose`（Web ホスト停止）で保留中のモーダルを cancel で解決する挙動は残す。UITK と併存すると、Web ホストの停止が UITK 側のモーダルも取り消す | 移す前と同じ挙動（`WebSocketHub.ClearBindings` が static 経由で呼んでいた）。ホスト停止はゲーム終了時だけなので実害は無い。UITK がモーダルを描く段で、保留の解決を描く側ごとに分けるかを決める | agent 既定（ユーザー未裁定） |
| D17 | ベースライン初回2本は入力環境障害のattemptとして保存し、録画前に現行録画skillのde-risk入力確認とEditor/GameViewフォーカス（今回背景動作は要件外）を行って無改変シナリオを再実行する | walk後のfocused=False・keyboard/mouse=Falseを観測し、focus後TrueとなりQueueStateEvent TabでPlayerInventoryへ実遷移を観測。UI回帰の証拠を得るため、対象UIへ届かない環境失敗を同合否の基準にしない | agent 既定（ユーザー未裁定） |
| D18 | 正規runnerのboot応答がalready playingとなった未投入シナリオは、正規preflight後にboot EDCを一度だけ実行し、今回のSessionDirectoryとready.markerを一致実証してから原文シナリオを一度だけ注入して回収する | 同一bootコードを再送するrunnerの経路でalready playingが反復した。新規sessionの実証により既存world混入を防ぎ、DSL/runnerやシナリオを変更せず必要な実測を得る | agent 既定（ユーザー未裁定） |
| D19 | 新設したUI純状態のディレクトリは既存WebUiGateClassification.RulesへCategory.Infraとして登録する | 既存Blueprint/BuildMenu/ProgressBarと同じ役割。新しい状態ファイルを正しく分類する付随変更であり、albof.4所有のG1 Web追従ゲート機構には触れない | agent 既定（ユーザー未裁定） |
| D20 | 現ホストで正規のUnity所有receiptが作れないため、長い検証前にEditor稼働時間を確かめ、既存MIN_AGE_MIN（現在30分）を跨ぐ場合はタスク区切りで自分のPIDだけを停止し、Editor予算と空き容量を再確認してuloop launchする | reaperログで旧PID59245の所有者未確認による停止を実測。常駐処理・wrapper・偽の所有者情報へ変更を加えず、検証中の停止を避ける | agent 既定（ユーザー未裁定） |

| D21 | HostlessのModalプローブはBP解放・実ブロック・公開selection遷移を前提にし、生成GUID・選択リセット・thumbnail完了と自分の生成物だけのcleanupまで待つ。起動後1フレームから全プローブとcleanup完了後1フレームまでLogAssertの失敗無視を解除し、標準runnerが想定外error/exceptionを検出する。全probe後は既存統合テストの前例に従い正規終了をawaitしFlushedをassertしてからExitPlayModeへ進む | 元サンプルは実DIのコピー購読者へSelectingStartのまま確定を流し、Task4/5/6のPassed XMLへBeginCreate例外を隠していた。独立再現とログ検出のみの赤実測で確認した。NoUnexpectedReceivedは通常Logも失敗にする実測があるため用いず、赤実測済みの標準runnerのerror/exception検出を使う。終了時エラーはprobe無し対照でも再現し、Task3比較はflush完了が未観測で原因未確定。既存PlaytestReportAndProgressTestの正規終了awaitをテストcleanupの既定とし実測で検証する。プロダクション終了処理を推測で変更しない | agent 既定（ユーザー未裁定） |

| D22 | BlockInventoryTopicのWeb専用再配信方針はTopics/Inventory/BlockInventoryRepublishPolicyへ分け、Topicは購読/JSON配信の窓口にする | Task10抽出後もTopicが208行となるため、空白圧縮せず連続値・ベルト・パイプ比較の役割で分割する。新規コード追加先10ファイル以下を優先し、既存多数のTopics直下ではなくInventory階層へ置く。状態追跡はClient.Game、Web帯域の方針はWebUiHostに保つ | agent 既定（ユーザー未裁定） |

| D23 | WebSocketHubの受信フレーム組立はinternal WebSocketReceiveLoopへ分け、Boot/Transportへ置く | 全ブランチ検査でTask4変更対象のHubだけ225行と判明。既存WebSocketConnection/MessageDispatcherと同じ役割分割にし、受信本文・公開API・接続所有・停止順序は保つ。空白圧縮やpartialを使わず200行未満と新設dir10以下を満たす | agent 既定（ユーザー未裁定） |

| D24 | Task8のcursor filterとTask11のBoot回帰filterは実namespaceに合わせ、EventLanguageGateCursorTest・LocalPlayEmbeddedServerBootTest・RemotePlayServerBootTestを明示対象にする | Bootはフォルダ名で3クラスのnamespaceには含まれず、元regexでは対象が0件だった。仕様の起動回帰を省略せず、実行XMLで各caseの包含を照合する | agent 既定（ユーザー未裁定） |

| D25 | Hostlessのブロック詳細probeは既存テスト前例の実足場を用意し、+2境界配置を余裕ある+1隣接配置へ補正する。空セルと既存InteractReachQueryのReachableを前提assertしてからtracker/network/閉後解除を検証する。配置した電気ブロックは既存統合テスト同様に隔離worldの正規shutdownへ寿命を預け、probe末尾でdatastore直操作による削除を追加しない | 無改変単独runで地面不足10件・out of range・Flushed未到達を再現し、正本InteractDistance2fに対し+2が境界と確認した。落下だけを原因と断定せず、物理や製品距離定数を変えず正規の操作前提を満たす。追加RemoveBlock cleanupだけを外した独立対照でdestroyed例外0/Flushed1を確認し、server専用Threadとの境界を無視する余分なcleanupを除く | agent 既定（ユーザー未裁定） |

| D26 | 言語選択待機前にUI/Playerの更新を所有クラスのAwakeで停止し、UIはInitializeの状態入場完了、Playerは既存StartPlayerRuntimeで開始する。PlayerObjectController同居interfaceは別ファイルへ移し200行未満を保ち、既存Player fixtureはinactive rootで参照配線後activateへ揃える | 現headと変更前baseの同一観測で、DI未完了なのに辞書/Inputがnullの実個体がenabled/active=Trueのまま更新し、3箇所各69件のNREを再現した。明白な既存未初期化NREを所有側の正規寿命へ揃えて直し、third-party本体改変やテストだけの無効化・null補完を避ける。cursorの期待外ログ検出を赤→緑で確認し、元の待機/cursor/初期化未完了assertを保つ。変更するPlayer fixture群とUIState回帰群を最終regexへ追加する | agent 既定（ユーザー未裁定） |

| D27 | BugReport公開タイムアウトfixtureは製品呼出し後に接続保留をassertし、同期で接続を受理する。非同期acceptの登録を必要としない方法へ替え、切断・子孫終了・資料復元等の元assertは保つ | 全体一式でfd1034/1026のIOSelector例外が再発し、独立の低FD→高FD対照で非同期登録だけが失敗、同期受理は成功した。製品のtimeoutや公開確認は変えず、fixtureの待機方式を補正する。FD高値の生成元と別caseのaccepted未完了の厳密原因は未確定として残す | agent 既定（ユーザー未裁定） |

| D28 | cursorの言語待機検証は元assertをすべて終えた後、正規終了をawaitしてFlushedを確認してからExitPlayModeへ進む | CEF正常の独立単独/最小列でも終了時socket例外を2回再現した。言語ゲート前に終了参加者が登録されるため、待機を人工通過せずD21と同じ保存/終了完了待ちを適用できる。例外免責や製品shutdownの推測変更を避ける | agent 既定（ユーザー未裁定） |

| D29 | 実再現したMachineModule/Recipe fixtureは元assert後に正規保存終了をawaitしFlushedを確認してからExitPlayModeへ進み、LoadMainGame後から終了完了まで期待外ログを検出する | 独立単独/最小列でreset→破棄済ServerStarter→FlushFailedを両classに再現。製品終了処理を変えずD21/D28同形のtest終了責務を満たす。未再現のBundle/Repeatは変更しない | agent 既定（ユーザー未裁定） |

| D30 | Task11で変更前にも実測したshader/Steam外部初期化の異常は、既存負債が残ることと全区間Error0未達をPR本文・報告へ明記し、`moorestech-albof.3.2`（shader）・`moorestech-albof.3.3`（Steam境界）へ追跡を分ける。ログ無視・Expect追加・素材やSteam設定の変更で合格にしない | fresh base `b40795d82b`/PID67122とfeature `a224297b0e`/PID8969でMetal再定義20headerが一致（pass詳細18/20一致、2件はFORWARD/OUTLINE差）。warm base0だけでは判断せずfresh対照を追加した。base Remote/PID8969とfeature `2c79fe0724`/PID90903は同じ閉宛先→MainMenu復帰でnative Steam client未発見とSteamManager.Awake124 Error1を記録。DI抽出以前の境界不良として証拠を残し、今回の修正範囲を保つ。Task11の実行完了と全区間ログ品質の合格は区別する | agent 既定（ユーザー未裁定） |

| D31 | 録画は正規preflight・同一boot API1回・今回のSessionDirectory/ready/live一致確認・変更前と同形の入力probe・無改変原文シナリオEDC1回をscratch helperで順に実行する。公式runner・DSL・シナリオ本体は変更せず、helperと全応答も証拠へ保存する | 現行録画skill `references/run-scenario.md` のready後Run前de-risk probeは必須だが、`run-scenario.sh`にはそのhookが無い。必須probeを省略せず正規各段階を実行するためのオーケストレーションで、D18のboot応答喪失救済とは理由が異なる。シナリオ原文SHA・新規session/ready時刻・live一致を照合し条件を保つ | agent 既定（ユーザー未裁定） |

| D32 | 実測済みElectricToGear・Bundle通常/欠損・Repeat fixtureへ実足場を共有し、正規LoadMainGame後の元assertから正規shutdown await/Flushedまで期待外ログを検出する。Hostlessの足場helperはUtil/Worldへ移し役割名へ改名する | 最新一式のscene境界でElectric/Bundle通常のreset→破棄済ServerStarter→FlushFailed、Bundle/Repeatの地面欠損を実測。製品/物理/閾値や例外免責を変えずtestの世界setupと終了責務を満たす。共有Hostlessも同じ最小列で再検証する | agent 既定（ユーザー未裁定） |

| D33 | 録画前のGameView入力環境は原値を記録してEditor経由で確認し、既存PlaytestUiOps同形のHierarchy→GameView遷移後に実入力成立を照合する。試した自分のPlayFocused設定は録画後にPlayUnfocusedへ明示復帰する | PlayFocused setterはその場で読めたが次bootでPlayUnfocusedへ戻り、設定変更の成功や根因解消は断定しない。既存focus遷移後にShift/focused/device成立を実測し原文を投入した。背景設定/製品/OS入力を変えず原値復帰を証拠へ残す | agent 既定（ユーザー未裁定） |

| D34 | 稼働中のWorldSnapshotRingを開始するBundle成功/Repeat fixtureは、小さな具体ServerTickEndTestStepを共有しawait RunOnServerAsyncでサーバーティック末尾から開始する | D32最小列でRepeat tick205の出自欠落Errorを実測し、独立単独では未再現。実競合原因は未確定だが稼働中server状態をmainから触る2callsiteを既存Belt fixtureの所有thread前例へ揃える。正規省略値null・元assert・製品リング・ログ検出は変えない | agent 既定（ユーザー未裁定） |

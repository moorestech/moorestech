# UITK W0-switch（全体スイッチ 1 つへの作り直し・albof.17）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 段階 1 で作った「画面ごとの所有表・Web への委譲・旧 R1 の Web 全面休止・Editor 限定の上書き」を外し、長命ブランチ `feature/uitk` の中を「全部 Web／全部 GameUi(UITK)」の Editor 限定スイッチ 1 つ（既定 Web）で切り替える形に作り直す。

**Architecture:** スイッチの値（`UiSystem { Web, GameUi }`）は起動で 1 回だけ `UiSystemBootSelection.ResolveAndApply()` が決め、`UiInputExclusivity.SetActiveUiSystem` へ押し込む。GameUi の起動は既存の Editor 経路（`WebUiStartup` の「Web ホストを起動しない」分岐＝0-10）に載り、CEF のブラウザも作らない。Client.Ui の登録の分岐は composition root（`MainGameUiRegistration`）の 1 か所だけで、Presenter・View・部品はスイッチを読まない。入力の裁き（`UiInputExclusivity`・`UiPointerHitTest`）と DSL のロケータは、スイッチの系だけを読む（両系の論理和にしない）。Web 側は旧 R1 の Web コミットを外して、取り込み済み master と同じ Web に戻す。

**Tech Stack:** Unity 6（UI Toolkit・InputSystem）、C#（VContainer・UniRx・UniTask）、NUnit（EditMode・EditModeInPlayingTest）、プレイテスト DSL（`Client.Playtest`）、Python 3（G1 台帳ゲート）、git・gh・bd

## Requirements

実行計画 `docs/uitk-migration/execution/execution-plan.md` §1 の W0-switch 行を 1 行 1 要件に割った。

1. 全体スイッチは Editor 限定・既定 Web の 1 つの enum（`UiSystem`）。値は起動ごとに 1 回だけ読み、PlayMode 中に変えても走っている起動には効かない。配布ビルドは常に Web。受け入れ: L0 `UiSystemSwitchTest`（2 件）が緑、`#if UNITY_EDITOR` の外から `UiSystemEditorSwitch` を参照する行が 0（`git grep -n UiSystemEditorSwitch -- 'moorestech_client/Assets/Scripts/Client.*/**/*.cs'` の結果が Task 2 の 2 ファイル・テスト 2 ファイルだけ）
2. GameUi の起動では Web ホストを起動せず（`WebUiHost.Hub == null`＝WS の接続 0）、CEF のブラウザの GameObject（RawImage と同じ）を非アクティブにする。受け入れ: L3 `GameUiSkeletonTest` で `Hub == null`・`WebUiHost.HostSkippedThisBoot == true`・診断記録 `HostStartup == SkippedByEditorSwitch`・有効な `WebUiCefNavigator` 0 個
3. Client.Ui の登録の分岐は `MainGameUiRegistration` の 1 か所。Web の起動では Client.Ui を 1 つも登録せず、`GameUiRoot` の全層が空。受け入れ: L3 `WebUiSystemSkeletonTest` でゲーム画面とポーズの両方で全層の `childCount == 0`、`git grep -n "UiSystem\." -- moorestech_client/Assets/Scripts/Client.Ui moorestech_client/Assets/Scripts/Client.Ui.State` が 0 件
4. 画面のルータは UIState と入れ子画面だけで画面を決める（`UiRoutingSnapshot` は `Screen` だけ）。`PauseMenuScreenPresenter.IsShownOn` は traits 表だけを見る（設計書 C10）。受け入れ: `git grep -n -E "MainPanelOwner|WebUiSuspended|UiSurface|UiOwnership" -- moorestech_client .agents` が 0 件
5. 所有表（`UiOwnershipTable`・`UiOwnershipEditorOverride`・`UiSurface`/`UiSurfaceOwner`）、`WebUiSuspension`、`GameUiRoot.SetCefVisible` と `cefRawImage`、`UiStateTopic` の休止配信、dispatcher の `web_ui_suspended` 拒否、`UiInputExclusivity` の休止（`SetWebUiSuspended`・`IsWebUiSuspended`）を消す。受け入れ: 要件 4 の grep に加え `git grep -n -E "SetCefVisible|SetWebUiSuspended|IsWebUiSuspended|web_ui_suspended|WebUiSuspension" -- moorestech_client .agents` が 0 件
6. 入力の裁きはスイッチの系だけを読む（R2）。Web の系では GameUi の Pick・入力欄フォーカスを読まず、GameUi の系では Web の pointerOverUi・入力欄フォーカスを読まない。受け入れ: L0 `UiInputExclusivityActiveSystemTest`（3 件。`UiInputExclusivityWebSuspensionTest` の書き換え）・`UiInputExclusivityGameUiTest`（4 件）・`UiInputExclusivityWebSilenceTest`（3 件）が緑
7. DSL のロケータはスイッチの系の 1 本だけで探す（R2・plan §4.4）。受け入れ: L0 `SwitchedUiElementLocatorTest`（10 件。`CompositeUiElementLocatorTest` の書き換え。選ばれた系は偽ロケータの `StartCount`・`FindCount` で確かめる）が緑、L3 `PauseMenuGameUiTest` で `UiInputExclusivity.ActiveUiSystem == GameUi`・ctor のログ `[PlaytestUi] locator reads only the GameUi UI`（`LogAssert.Expect`）・`PlaytestUiLocatorSession.Locator.Find("pause-menu-open-settings")` が Found
8. L3 `GameUiSkeletonTest`・`PauseMenuGameUiTest`・`GameUiInputTest`（2 件）をスイッチ GameUi で、`WebUiSystemSkeletonTest` をスイッチ Web で緑にする。`UiOwnershipTableTest`・`WebSocketSuspendedUiStateRequestTest` は削除
9. 旧 R1 の Web 側変更（`a97575e5b5`・`05b9bdefa8`・`1095cab8bf`・`ccb3d512f7`・`5421c7f9f0` の Web 部）を外し、`moorestech_web/` を取り込み済み master と同じにする（生成物 `src/shared/i18n/generated/localizationKeys.ts` だけは `3e946e4bd3` の C# 文言に連動するので残す）。受け入れ: `git diff --name-status $(git merge-base HEAD origin/master) HEAD -- moorestech_web` がその 1 行だけ
10. 台帳 `docs/uitk/TODO.md` に、外した Web コミットを `捨てた（理由）` で記帳（`a97575e5b5`・`1095cab8bf`・`ccb3d512f7` を `写した`→`捨てた`、撤去コミットを新規行）、「通信失敗のトースト」行を結論で書き換え、R4 手順を全体スイッチの手順に置き換える。受け入れ: ローカル G1 が「束 1（暗転・起点 979725bcd1）: 未記帳 0 件」「違反なし」rc=0、G1 単体テスト 5 本 OK
11. 録画: `uitk/stage1/uitk-lifeline`・`uitk/stage1/uitk-nested-pause-skit`・`uitk/stage1/uitk-nested-pause-train` をスイッチ GameUi で緑（ログに `[PlaytestUi] locator reads only the GameUi UI`）、`misc/pause-menu-hierarchy` をスイッチ Web で master と同じ合否。`uitk/uitk-pause-web-action-count` は役目（旧 R1 の実測）を終えたので削除
12. 回帰: `Client.Tests.Ui.*`・`Client.Tests.WebUi.*`・`Client.Tests.WebUiHost.*`・`Client.Tests.Playtest.*`・`Client.Tests.Input.*`・`Client.Tests.Player.TextInputMovementLockTest`・`Client.Tests.EditModeInPlayingTest.Hostless.*`・`Client.Tests.EditModeInPlayingTest.Ui.*` が緑
13. moores-code-review → pr-independent-review（設計判断になった指摘だけユーザー裁定 1 回）→ `base=feature/uitk` の PR を agent がマージコミットで取り込む。bd `moorestech-albof.17` を閉じる
- やらないこと: `UiInputExclusivity` の Web 側（`SetState`・無応答）の撤去（切替 PR の 5a）。`WebUiHostEditorSkip` の撤去（ホストレステストが使う。5a）。層 enum・`GameUiRoot.GetLayer(UiLayer, UiFamily)`・tss の束ね（F1）。master への PR。HUD など未移植画面の UITK 化（裁定「UITK の間、未実装の画面は使えない」）。prefab・シーンの編集（`MainGameUI.prefab` に残る `cefRawImage:` の行は Unity が次の保存で落とす。手で消さない）

## 操作の死活表（Phase 2.5）

所有表・旧 R1 の Web 休止・Web 起動での UITK ポーズを外すので、同じ機構にぶら下がる操作を並べる。「死ぬ」は 0 件。

| 操作 | Web 起動（既定・配布と同じ） | GameUi 起動（Editor のスイッチ） | 根拠 |
|---|---|---|---|
| Esc → ポーズ | 生きる（Web のポーズ。master と同じ） | 生きる（UITK のポーズ） | Web 側は Task 5 で master と同じへ戻す。GameUi 側は L3 `PauseMenuGameUiTest` |
| ポーズ → 設定・バグ報告 | 生きる（Web。master と同じ） | 生きる（UITK の設定・バグ報告） | 状態とコマンドは `MainGameUiStateRegistration` で両系に登録（W-5） |
| トースト | 生きる（Web） | 生きる（`ToastLayerPresenter`） | `GameUiToastFeed` は GameUi 分岐（W-5） |
| HUD など未移植画面 | 生きる（Web） | 裁定済み（使えない） | 裁定「UITK の間、未実装の画面は使えない」 |
| テキスト入力中のキー抑止・IME | 生きる（Web の入力欄フォーカス） | 生きる（GameUi の入力欄フォーカス・`ImeCompositionModeApplier`） | 片側読み（R6）。L0 `UiInputExclusivityActiveSystemTest`・L3 `GameUiInputTest` |
| ポインタ占有（建築・カメラのクリックを UI 上で止める） | 生きる（Web の pointerOverUi） | 生きる（GameUi の Pick） | `UiPointerHitTest` が `IsPointerOverActiveUi` を読む（Task 2 Step 2）。DSL の設置前の待ちは「本PR外のリファクタ提案」1 |
| DSL の testid 解決 | 生きる（Web ロケータ） | 生きる（GameUi ロケータ） | `SwitchedUiElementLocator`（Task 3）。録画 Task 6 |
| ホストレステスト（`WebUiHostEditorSkip` の印） | 生きる（Hub=null・CEF 非アクティブ） | 該当なし | 印は残す（W-3）。印の起動で CEF が非アクティブになるのは Hub=null で絵が出ない状態の後始末（W-4） |
| CEF の起動に失敗したときの Web 起動 | 生きる（master と同じ振る舞い） | 該当なし（CEF を作らない） | `Failed` の経路は変えない（裁定「CEF 由来の不具合は切替まで直さない」） |
| Web 無応答の検知 | 生きる（`WebUiSilenceWatcher`） | 該当なし（系の印で判定しない） | W-6・Task 2 Step 1 |

## Global Constraints

- 作業場所: 指揮が `moores-wt new uitk-lane/w0-switch --from origin/feature/uitk` で作った worktree（Editor 付き）。全コマンドはその worktree の root を cwd にして実行し、最初に `pwd` で確かめる。Editor を触る前に `moores-wt status` で自分の Editor（pid）を確かめ、他人の Editor を落とさない
- 前提: W0-merge（albof.20）が `feature/uitk` に入っている（束 1 が `暗転`）。入っていなければ始めない（G1 が旧 R1 の撤去を束 1 の未記帳として数える前提が崩れる）
- 裁定（取り込み後はこのブランチの `.decisions/`）: `2026-11-06-UITK長命ブランチの中は全体スイッチ1つで切り替え追従はブランチ側で記帳する.md`（全部 Web／全部 UITK の 1 つのデバッグスイッチ。UITK の間、未実装画面は使えない）、`2026-11-06-UITK移行は長命ブランチで作り一気に切り替える.md`、`2026-11-06-UITK一気切替の日にCEFとWebを同時に撤去する.md`、`2026-11-06-UITK切替までCEF由来の不具合は直さない.md`、`2026-11-06-CEF停止の記録はUITK長命ブランチから先にmasterへ切り出す.md`、`2026-10-18-CEFを撤去しUI-Toolkitへ全面移行する.md`
- 統合計画の正本: `docs/uitk-migration/plan.md` §2.2「段階 2 の前の作り直し」・§3.3 R1〜R4・§3.8・§4.4。設計書 `docs/uitk-migration/execution/component-architecture.md` §1.4 C10・§4.4・[A-16]
- 名前: スイッチの enum は `Client.Input.UiSystem { Web, GameUi }`（Client.Ui の名前に技術名 Uitk を入れない plan §3.1 に合わせ、段階 1 の `GameUi*` の語を使う）。Editor スイッチは `Client.Starter.Initialization.Boot.UiSystemEditorSwitch`、起動の解決は `UiSystemBootSelection`
- AGENTS.md: 1 ファイル 200 行未満・1 ディレクトリ 10 ファイルまで・partial／`Func<>` 禁止・イベントは UniRx・try-catch 禁止（try/finally は可）・主要セクションに日英 2 行コメント・`[SerializeField]` は `_` 無し・エディタ専用は `#if UNITY_EDITOR` でファイル末尾・デフォルト引数禁止・`.meta` を手で作らない（`git mv` での移動と削除は可。新規 .cs の .meta は Unity の compile で生成されたものをコミット）・Prefab/シーンを手で編集しない
- 行番号は `54c8a4c8c8` 時点。W0-merge の後で行がずれていたら、示した「旧テキスト」の文字列で場所を合わせる
- `.cs` を変えたタスクは必ず `uloop compile --project-path ./moorestech_client`（ErrorCount 0）。ドメインリロード中のエラーは 45 秒待って再試行
- バックグラウンドのポーリングで待たず、全部前景で実行する（録画は Bash の timeout 600000ms で前景）。タスクごとにコミットする。コミット末尾に `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`
- 使い捨てのファイル（開始 SHA・台帳スクリプト）は `${TMPDIR:-/tmp}/uitk-w0-switch/` に置きコミットしない
- 報告は `../moorestech_logs/harness/uitk/W0-switch/done.md`（完了条件ごとの証拠・未完了・要裁定）。`../moorestech_logs` に書く前に同 repo の `README.md` を読む。録画・Unity ログ・テスト結果 xml はそこへ置かず worktree の gitignore 下に置く

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反3件・修正3件 / CEF の生成可否を診断記録から WebUiHost の起動の状態 `HostSkippedThisBoot` へ移した（W-4）、テスト専用の `SwitchedUiElementLocator.ActiveSystem` とキャストを消して L3 をログと `ActiveUiSystem` で確かめる形にした、操作の死活表を足した（死ぬ操作 0）
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強5・弱4・第3バケツ1 / 強: 無応答の判定を系の印に寄せ probe の null の意味を 1 つに絞った（5-A）、系ごとの型分けは 5a で消える型になるので見送り W-15 に記録（5-B）、系の書き込み口を internal＋`InternalsVisibleTo("Client.Starter")` に閉じた（5-D・W-13）、印はデバッグスイッチでないと W-3 に明記（検査7）、`PlaytestUiOps.WaitPointerLeaveWebUi` は本PR外の提案へ（検査6）。弱: 比較の向きを `GameUi` 基準にそろえ、値の正本と読む 6 か所を Self-Review 6・W-14 に記録、W-1 に契約層に置かない理由を足し、飛ばした理由を分けない理由を W-3 に記録

---

## File Structure

パスは repo 相対。`S/` = `moorestech_client/Assets/Scripts/`（Files 節では略さず書く）。

| パス | 変更 | 責務 |
|---|---|---|
| `S/Client.Input/UiInputExclusivity.cs` | Modify（全置換） | `UiSystem` enum を同居（`InputSuppressionScope` の前例）。スイッチの系だけを読む入力の裁き。休止を消す |
| `S/Client.Game/InGame/Control/UiPointerHitTest.cs` | Modify | `IsPointerOverActiveUi` ∨ uGUI |
| `S/Client.Starter/Initialization/Boot/UiSystemBootSelection.cs` | Create | 起動で 1 回だけ系を決めて押し込む＋末尾に `#if UNITY_EDITOR` の `UiSystemEditorSwitch`（SessionState・メニュー） |
| `S/Client.Starter/Initialization/Boot/WebUiStartup.cs` | Modify | `StartAsync(exitToken, uiSystem)`。GameUi なら既存の Editor スキップ経路 |
| `S/Client.Starter/InitializeScenePipeline.cs` | Modify（:79-80） | 解決を `WebUiStartup` の直前に 1 回 |
| `S/Client.WebUiHost/Cef/WebUiCefNavigator.cs` | Modify（Awake） | Editor スキップの起動では CEF の GameObject を非アクティブ |
| `S/Client.WebUiHost/Boot/WebUiHost.cs` | Modify | 起動でホストを飛ばしたかの値 `HostSkippedThisBoot`（書き手は `WebUiStartup` だけ。診断記録とは別） |
| `S/Client.WebUiHost/AssemblyInfo.cs`・`S/Client.Input/AssemblyInfo.cs` | Modify | `InternalsVisibleTo("Client.Starter")`（起動の書き込み口を composition root に閉じる） |
| `S/Client.WebUiHost/Boot/WebSocketMessageDispatcher.cs` | Modify（:123-129 削除） | 休止中の `ui_state.request` 拒否を消す |
| `S/Client.WebUiHost/Game/Actions/UiStateActions.cs` | master 版へ戻す | `Type` 定数（拒否のためだけの追加）を消す |
| `S/Client.Ui/Foundation/Routing/`（`WebUiSuspension.cs` と `Routing.meta`） | Delete | 旧 R1 |
| `S/Client.Ui/Foundation/Root/GameUiRoot.cs` | Modify（全置換） | CEF の RawImage を持たない |
| `S/Client.Playtest/UiElements/CompositeUiElementLocator.cs` → `SwitchedUiElementLocator.cs` | `git mv`＋全置換 | 選ばれた系の 1 本だけへ委譲 |
| `S/Client.Playtest/UiElements/PlaytestUiLocatorSession.cs` | Modify（全置換） | Start のたびに起動の系でロケータを選ぶ |
| `S/Client.Ui.State/Routing/UiOwnershipTable.cs`・`UiSurface.cs` | Delete | 所有表 |
| `S/Client.Ui.State/Routing/UiRoutingState.cs` | Modify（全置換） | 画面だけを持つ |
| `S/Client.Ui/Screens/PauseMenu/PauseMenuScreenPresenter.cs` | Modify（:63-68） | traits だけ |
| `S/Client.Ui/Foundation/Input/ImeCompositionModeApplier.cs` | Modify（コメント） | GameUi の系だけで登録される前提の説明 |
| `S/Client.Starter/Registration/MainGameUiRegistration.cs` | Modify（全置換） | 分岐の 1 か所 |
| `S/Client.Starter/Registration/MainGameUiStateRegistration.cs` | Modify（全置換） | 両系が使う状態・コマンド |
| `S/Client.Starter/Registration/MainGameModelRegistration.cs` | Modify | `WebUiSilenceWatcher` の登録（W0-cef の master 側と同じ置き場） |
| `S/Client.Starter/MainGameStarter.cs` | Modify（:111） | 起動の系を登録口へ渡す |
| `S/Client.WebUiHost/Game/Topics/UiStateTopic.cs` | master 版へ戻す | 休止配信を消す |
| `S/Client.WebUiHost/Game/WebUiGameBinder.cs` | Modify（:96） | `UiStateTopic` の ctor |
| テスト（L0/L1） | 下表 | — |
| `moorestech_web/`（`localizationKeys.ts` を除く） | master 版へ戻す | 旧 R1 の Web 側 |
| `docs/uitk/TODO.md` | Modify | 追従記録・通信失敗の行・R4 手順 |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage1/uitk-lifeline.cs` | Modify（冒頭コメント） | 起動前の手順をスイッチへ |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-pause-web-action-count.cs` | Delete | 旧 R1 の実測 |

テスト:

| パス | 変更 | 中身 |
|---|---|---|
| `S/Client.Tests/Ui/Input/UiInputExclusivityGameUiTest.cs` | 全置換 | GameUi の系の 4 件 |
| `S/Client.Tests/Ui/Input/UiInputExclusivityWebSuspensionTest.cs` → `UiInputExclusivityActiveSystemTest.cs` | `git mv`＋全置換 | 片側読みの 3 件 |
| `S/Client.Tests/Ui/Input/UiInputExclusivityWebSilenceTest.cs` | Modify | 休止の 1 件を消し 3 件 |
| `S/Client.Tests/Ui/TestHost/UiTestHost.cs` | Modify | L1 を GameUi の系で回す |
| `S/Client.Tests/Ui/Routing/UiSystemSwitchTest.cs` | Create | スイッチと起動の解決 2 件 |
| `S/Client.Tests/Ui/Routing/UiOwnershipTableTest.cs` | Delete | — |
| `S/Client.Tests/WebUiHost/Transport/WebSocketSuspendedUiStateRequestTest.cs` | Delete | — |
| `S/Client.Tests/Ui/Dsl/CompositeUiElementLocatorTest.cs` → `SwitchedUiElementLocatorTest.cs` | `git mv`＋全置換 | 10 件 |
| `S/Client.Tests/Ui/Dsl/FakeUiElementLocator.cs` | Modify | `FindCount` |
| `S/Client.Tests/WebUi/UiStateSubStatePublishTest.cs`・`WireContractTest.cs`・`WireFixtures/ui_state.json` | master 版へ戻す | 休止の欄 |
| `S/Client.Tests/EditModeInPlayingTest/Ui/UiSystemPlayModeTestBase.cs` | Create | スイッチを選んで PlayMode に入る L3 の土台 |
| `S/Client.Tests/EditModeInPlayingTest/Ui/GameUiSkeletonTest.cs`・`PauseMenuGameUiTest.cs` | 全置換 | UITK 側 |
| `S/Client.Tests/EditModeInPlayingTest/Ui/WebUiSystemSkeletonTest.cs` | Create | Web 側で層が空 |
| `S/Client.Tests/EditModeInPlayingTest/Ui/GameUiInputTest.cs`・`GameUiTextFieldWiringChecks.cs` | Modify | UITK 側・Web の値を読まない |

`S/Client.Tests/EditModeInPlayingTest/Ui/` は 8 → 10 ファイル（上限ちょうど）。`S/Client.Tests/Ui/Routing/` は 2 → 2（1 削除・1 追加）。`S/Client.Starter/Initialization/Boot/` は 1 → 2。

---

### Task 1: 前提の確認とベースライン

**Files:**
- Create（コミットしない）: `${TMPDIR:-/tmp}/uitk-w0-switch/start.env`

**Interfaces:**
- Consumes: なし
- Produces: `start.env` の `START_SHA`（作り直し前の HEAD）と `MERGED_MASTER`（取り込み済み master）。Task 5・7 が読む

- [ ] **Step 1: 作業場所と前提を確かめる**

Run:
```bash
pwd
git status --short
git log -1 --format='%h %s'
grep -n '^| 1 | 基盤と命綱' docs/uitk/TODO.md
mkdir -p "${TMPDIR:-/tmp}/uitk-w0-switch"
{ echo "START_SHA=$(git rev-parse HEAD)"; echo "MERGED_MASTER=$(git merge-base HEAD origin/master)"; } > "${TMPDIR:-/tmp}/uitk-w0-switch/start.env"
cat "${TMPDIR:-/tmp}/uitk-w0-switch/start.env"
moores-wt status
```
Expected: `pwd` が `uitk-lane/w0-switch` の worktree、`git status --short` が空、束 1 の行が `| 1 | 基盤と命綱（ポーズ・設定・バグ報告・トースト） | 暗転 | 979725bcd1 | ...`。`暗転` でなければ止めて指揮へ報告（W0-merge 未了）。`moores-wt status` で自分の worktree に Editor の pid がある

- [ ] **Step 2: 作り直し前のコンパイルとテストの成否を記録する**

Run:
```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\." --timeout-seconds 1500
```
Expected: ErrorCount 0。テストの Passed/Failed 件数と失敗名を `done.md` の「ベースライン」に書く（ここでの失敗は作り直し前から在るもの。Task 7 で増減を比べる）。compile が通らなければ止めて指揮へ報告

---

### Task 2: 全体スイッチの値と入力の片側読み

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Input/UiInputExclusivity.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/UiPointerHitTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Initialization/Boot/UiSystemBootSelection.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Initialization/Boot/WebUiStartup.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs:79-80`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Cef/WebUiCefNavigator.cs`（Awake）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiHost.cs`（起動でホストを飛ばしたかの読み取り専用の値）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/AssemblyInfo.cs`・`moorestech_client/Assets/Scripts/Client.Input/AssemblyInfo.cs`（`InternalsVisibleTo("Client.Starter")`）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebSocketMessageDispatcher.cs:123-129`
- Modify（master 版へ）: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/UiStateActions.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Routing/`（`WebUiSuspension.cs`・`.meta`）と `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Routing.meta`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Root/GameUiRoot.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiRegistration.cs`（`WebUiSuspension` の 1 行と using だけ）
- Delete: `moorestech_client/Assets/Scripts/Client.Tests/WebUiHost/Transport/WebSocketSuspendedUiStateRequestTest.cs`（`.meta` も）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Input/UiInputExclusivityGameUiTest.cs`
- Rename+Modify: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Input/UiInputExclusivityWebSuspensionTest.cs` → `UiInputExclusivityActiveSystemTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Input/UiInputExclusivityWebSilenceTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Ui/TestHost/UiTestHost.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Routing/UiSystemSwitchTest.cs`

**Interfaces:**
- Consumes: なし
- Produces:
  - `public enum Client.Input.UiSystem { Web, GameUi }`
  - `UiInputExclusivity`: `public static UiSystem ActiveUiSystem { get; }`（静的プロパティ。既定 Web）／`internal static void SetActiveUiSystem(UiSystem system)`（呼べるのは `InternalsVisibleTo` の `Client.Starter`＝起動の composition root と `Client.Tests` だけ）／`public static bool IsPointerOverActiveUi(Vector2 screenPosition)`。`IsPointerOverWebUi`（Web の系のときだけ true になりうる）・`IsPointerOverGameUi(Vector2)`（GameUi の系のときだけ）・`SetState`・`SetGameUiTextInputFocused`・`SetGameUiPointerProbe`・`ConsumeEscapeThisFrame`・無応答の API は名前と引数を変えない。`SetWebUiSuspended`・`IsWebUiSuspended` は消える
  - `internal static UiSystem Client.Starter.Initialization.Boot.UiSystemBootSelection.ResolveAndApply()`
  - `#if UNITY_EDITOR public static class UiSystemEditorSwitch { public static UiSystem Current { get; } public static void Set(UiSystem system); }`
  - `internal static UniTask<bool> WebUiStartup.StartAsync(CancellationToken exitToken, UiSystem uiSystem)`
  - `WebUiHost`: `public static bool HostSkippedThisBoot { get; private set; }`／`internal static void SetHostSkippedThisBoot(bool skipped)`（書くのは `WebUiStartup` だけ。CEF の生成可否が読む）

- [ ] **Step 1: `UiInputExclusivity.cs` を全置換する**

```csharp
using System.Diagnostics;
using System.Threading;
using UniRx;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Client.Input
{
    [System.Flags]
    public enum InputSuppressionScope
    {
        Keyboard = 1,
    }

    // 全体スイッチで選ばれた描く系。長命ブランチの中だけの区別で、切替 PR で消す（plan §3.3 R1）
    // The drawing system chosen by the global switch; it exists only on the long-lived branch and goes away in the cutover PR (plan §3.3 R1)
    public enum UiSystem
    {
        Web,
        GameUi,
    }

    // 全体スイッチで選ばれた系の入力占有だけを読み、テキスト入力時のキー抑止とESCラッチを担う（plan §3.3 R2）
    // Reads input occupancy only from the system chosen by the global switch, suppresses keyboard input during text entry and latches ESC (plan §3.3 R2)
    public static class UiInputExclusivity
    {
        // Webはpongが15秒途絶えるとWSを閉じる（moorestech_web/webui/src/bridge/transport/webSocketClient.ts の 15000ms）。同じ値で無応答とみなす
        // The web closes the WS after 15s without a pong (15000ms in webSocketClient.ts); the same value marks the web as silent
        private const double WebSilenceThresholdSeconds = 15.0;

        // 現在値と変化通知を1本に畳む。購読が現在値を必ず連れてくるので取得と購読の間に落ちる窓が無い
        // Current value and change notification live in one stream, so nothing can slip between reading and subscribing
        private static readonly ReactiveProperty<bool> TextInputFocusedProperty = new(false);
        private static readonly object UiStateLock = new();
        private static volatile UiSystem _activeUiSystem = UiSystem.Web;
        private static volatile bool _pointerOverUi;
        private static volatile bool _webSilent;
        private static long _lastWebMessageTimestamp;
        private static bool _webTextInputFocused;
        private static volatile bool _gameUiTextInputFocused;
        private static IGameUiPointerProbe _gameUiPointerProbe;
        private static int _escapeConsumedFrame = -1;
        private static int _lastKeyboardProbeFrame = -1;

        // 選ばれていない系の値は読まない。無応答のWebは値を更新できないのでポインタ占有を主張させない
        // The unselected system is never read, and a silent web cannot update its values so it never claims the pointer
        public static bool IsPointerOverWebUi => _activeUiSystem == UiSystem.Web && !_webSilent && _pointerOverUi;
        public static UiSystem ActiveUiSystem => _activeUiSystem;
        public static bool IsTextInputFocused => TextInputFocusedProperty.Value;
        public static bool IsEscapeConsumedThisFrame => _escapeConsumedFrame == Time.frameCount;
        // 呼び出し元はWebSocketの受信スレッドなので購読側でメインスレッドへ移すこと
        // Callers run on the WebSocket thread, so subscribers must hop to the main thread
        public static IReadOnlyReactiveProperty<bool> TextInputFocused => TextInputFocusedProperty;

        // 起動で1回だけ composition root（Client.Starter）が押し込む。internal で他の層から書かせない
        // Pushed once per boot by the composition root (Client.Starter); internal so no other layer can write it
        internal static void SetActiveUiSystem(UiSystem system)
        {
            lock (UiStateLock)
            {
                if (_activeUiSystem == system) return;
                _activeUiSystem = system;
                RecomputeTextInputFocused();
            }
            Debug.Log($"[UiInput] active UI system={system}");
        }

        public static void SetState(bool pointerOverUi, bool textInputFocused)
        {
            // 同値落ちはReactivePropertyが担保し、受信ループと切断時リセットの並走はlockが直列化する
            // ReactiveProperty drops same-value writes while the lock serializes the receive loop against the disconnect reset
            lock (UiStateLock)
            {
                _pointerOverUi = pointerOverUi;
                _webTextInputFocused = textInputFocused;
                RecomputeTextInputFocused();
            }
        }

        // GameUiの入力欄のフォーカス。メインスレッドから呼ぶが、Webの書き込みと並走するのでlockの中で再計算する
        // GameUi text-field focus; called on the main thread but recomputed under the lock because web writes run concurrently
        public static void SetGameUiTextInputFocused(bool focused)
        {
            lock (UiStateLock)
            {
                _gameUiTextInputFocused = focused;
                RecomputeTextInputFocused();
            }
        }

        // null で解除。系の区別は _activeUiSystem が持ち、null は GameUi の系で DI が登録する前だけを表す
        // Pass null to unregister; the system lives in _activeUiSystem and null only means GameUi before DI registers the probe
        public static void SetGameUiPointerProbe(IGameUiPointerProbe probe)
        {
            _gameUiPointerProbe = probe;
        }

        // GameUi の系のときだけ Pick を見る（R2）
        // Look at the pick only in the GameUi system (R2)
        public static bool IsPointerOverGameUi(Vector2 screenPosition)
        {
            var probe = _gameUiPointerProbe;
            return _activeUiSystem == UiSystem.GameUi && probe != null && probe.IsPointerOverGameUi(screenPosition);
        }

        // 選ばれた系のポインタ占有。両系の論理和にしない（R2）
        // Pointer occupancy of the chosen system; never the OR of both systems (R2)
        public static bool IsPointerOverActiveUi(Vector2 screenPosition)
        {
            return _activeUiSystem == UiSystem.GameUi ? IsPointerOverGameUi(screenPosition) : IsPointerOverWebUi;
        }

        // このフレームのESCはUIが使った。InputKey/HybridInputは同フレームの押下を返さない
        // This frame's ESC was used by the UI; InputKey/HybridInput return no press for it within the same frame
        public static void ConsumeEscapeThisFrame()
        {
            if (IsEscapeConsumedThisFrame) return;
            _escapeConsumedFrame = Time.frameCount;
            Debug.Log($"[UiInput] escape consumed by game UI at frame {_escapeConsumedFrame}");
        }

        // WSスレッドから受信ごとに呼ぶ。時刻だけ書き、無応答の印は EvaluateWebSilence だけが書く（D-22）
        // Called on the WS thread per message; it writes only the time and only EvaluateWebSilence writes the silent mark (D-22)
        public static void RecordWebMessageReceived(long stopwatchTimestamp)
        {
            Interlocked.Exchange(ref _lastWebMessageTimestamp, stopwatchTimestamp);
        }

        // 一度も受信していなければ null（Web の系の起動直後。バグ報告の診断も読む）
        // Null when nothing has ever been received (right after a Web boot; the bug-report diagnostics read it too)
        public static double? SecondsSinceLastWebMessage(long nowStopwatchTimestamp)
        {
            var last = Interlocked.Read(ref _lastWebMessageTimestamp);
            if (last == 0) return null;
            return (nowStopwatchTimestamp - last) / (double)Stopwatch.Frequency;
        }

        // JSが止まりWSだけ残るとWebの値が古いまま残る（§3.11）。閾値超えでWeb側を読まない。GameUiの系では系の印で判定しない
        // A stopped JS with a live WS leaves stale web values (§3.11); past the threshold the web side is not read, and the GameUi system never judges silence
        public static void EvaluateWebSilence(long nowStopwatchTimestamp)
        {
            var seconds = SecondsSinceLastWebMessage(nowStopwatchTimestamp);
            var silent = _activeUiSystem == UiSystem.Web && seconds.HasValue && WebSilenceThresholdSeconds < seconds.Value;
            lock (UiStateLock)
            {
                if (_webSilent == silent) return;
                _webSilent = silent;
                RecomputeTextInputFocused();
            }

            if (silent) Debug.LogWarning($"[UiInput] web UI silent for {seconds.Value:F1}s; ignoring web input values until it answers again");
            else Debug.Log("[UiInput] web UI answered again; web input values are read again");
        }

        public static bool IsSuppressed(InputSuppressionScope scope)
        {
            return (scope & InputSuppressionScope.Keyboard) != 0 && TextInputFocusedProperty.Value;
        }

        public static void ProbeSuppressed(InputSuppressionScope scope)
        {
            // 入力時だけ記録しログ洪水を防ぐ
            // Log only on input to prevent log floods
            if ((scope & InputSuppressionScope.Keyboard) != 0 && TextInputFocusedProperty.Value && _lastKeyboardProbeFrame != Time.frameCount)
            {
                _lastKeyboardProbeFrame = Time.frameCount;
                Debug.Log($"[UiInputProbe] Suppressed keyboard input because a {_activeUiSystem} text field owns focus");
            }
        }

        // 呼び出し側はUiStateLockを保持していること。選ばれた系の値だけを読む（R2）
        // Callers must hold UiStateLock; only the chosen system's value is read (R2)
        private static void RecomputeTextInputFocused()
        {
            TextInputFocusedProperty.Value = _activeUiSystem == UiSystem.GameUi ? _gameUiTextInputFocused : !_webSilent && _webTextInputFocused;
        }

#if UNITY_EDITOR
        // EditModeではTime.frameCountが進まずラッチがテスト間で残るため、テストから解除する
        // In EditMode Time.frameCount does not advance so the latch would leak between tests; tests clear it here
        internal static void ClearEscapeLatchForTest()
        {
            _escapeConsumedFrame = -1;
        }

        // 受信時刻と無応答の印は静的に残るため、テスト間で「一度も受信していない」状態へ戻す
        // The receive time and silent mark are static, so tests return to the never-received state between cases
        internal static void ResetWebSilenceForTest()
        {
            Interlocked.Exchange(ref _lastWebMessageTimestamp, 0);
            lock (UiStateLock)
            {
                _webSilent = false;
                RecomputeTextInputFocused();
            }
        }
#endif
    }
}
```

`moorestech_client/Assets/Scripts/Client.Input/AssemblyInfo.cs` と `moorestech_client/Assets/Scripts/Client.WebUiHost/AssemblyInfo.cs` の `[assembly: InternalsVisibleTo("Client.Tests")]` の次の行に、それぞれ足す:
```csharp
[assembly: InternalsVisibleTo("Client.Starter")]
```
（`SetActiveUiSystem` と `WebUiHost.SetHostSkippedThisBoot` の書き手を起動の composition root に閉じる。判断記録 W-13）

- [ ] **Step 2: `UiPointerHitTest.cs` の判定を片側読みにする**

旧テキスト（:19-23）:
```csharp
            // GameUi(UITK)のPickはprobe未登録ならfalse、WebUiは休止中ならfalse
            // GameUi (UITK) pick is false without a probe, and the Web UI is false while suspended
            var isOverGameUi = UiInputExclusivity.IsPointerOverGameUi(HybridInput.GetMousePosition());

            return isOverGameUi || UiInputExclusivity.IsPointerOverWebUi || isOverUguiPanel;
```
新テキスト:
```csharp
            // 全体スイッチの系だけを読む（GameUi は Pick、Web は pointerOverUi。plan §3.3 R2）
            // Read only the global switch's system (GameUi uses the pick, Web uses pointerOverUi; plan §3.3 R2)
            var isOverActiveUi = UiInputExclusivity.IsPointerOverActiveUi(HybridInput.GetMousePosition());

            return isOverActiveUi || isOverUguiPanel;
```

- [ ] **Step 3: `UiSystemBootSelection.cs` を作る**

```csharp
using Client.Input;
using UnityEngine;

namespace Client.Starter.Initialization.Boot
{
    /// <summary>
    /// 描く系（Web か GameUi）を起動で1回だけ決め、入力の裁きへ押し込む（plan §3.3 R1・R4）
    /// Decides the drawing system (Web or GameUi) once per boot and pushes it into input arbitration (plan §3.3 R1, R4)
    /// </summary>
    internal static class UiSystemBootSelection
    {
        internal static UiSystem ResolveAndApply()
        {
#if UNITY_EDITOR
            var system = UiSystemEditorSwitch.Current;
#else
            // 配布ビルドにスイッチは無く、いつも Web で描く（R4）
            // Player builds have no switch and always draw with Web (R4)
            var system = UiSystem.Web;
#endif
            // どちらの系で起動したかは毎回ログに残す（Web の起動で Client.Ui を登録しない理由の手掛かり）
            // Always log which system this boot uses (the clue for why a Web boot registers nothing from Client.Ui)
            Debug.Log($"[UiSystem] this boot draws with {system}");
            UiInputExclusivity.SetActiveUiSystem(system);
            return system;
        }
    }
#if UNITY_EDITOR
    /// <summary>
    /// Editor 限定の全体スイッチ。次の起動から全部 Web か全部 GameUi(UITK) で描く。既定は Web（plan §3.3 R4）
    /// Editor-only global switch; from the next boot everything draws with Web or with GameUi (UITK); the default is Web (plan §3.3 R4)
    /// 値は SessionState に置くので PlayMode 入りのドメインリロードを越え、Editor を閉じると既定へ戻る。切替 PR（5a）でスイッチごと消す
    /// The value lives in SessionState, so it survives the play-mode domain reload and returns to the default when the Editor closes; the cutover PR (5a) deletes it
    /// </summary>
    public static class UiSystemEditorSwitch
    {
        private const string SessionStateKey = "UiSystemEditorSwitch_GameUi";
        private const string WebMenuPath = "moorestech/UI System/Web";
        private const string GameUiMenuPath = "moorestech/UI System/GameUi (UITK)";

        public static UiSystem Current => UnityEditor.SessionState.GetBool(SessionStateKey, false) ? UiSystem.GameUi : UiSystem.Web;

        // PlayMode 中に変えても走っている起動には効かない。起動ごとに1回だけ読むため
        // Changing it during play mode does not affect the running boot, because it is read once per boot
        public static void Set(UiSystem system)
        {
            UnityEditor.SessionState.SetBool(SessionStateKey, system == UiSystem.GameUi);
            Debug.Log($"[UiSystemEditorSwitch] the next boot draws with {system}");
        }

        [UnityEditor.MenuItem(WebMenuPath)]
        private static void SelectWeb()
        {
            Set(UiSystem.Web);
        }

        [UnityEditor.MenuItem(GameUiMenuPath)]
        private static void SelectGameUi()
        {
            Set(UiSystem.GameUi);
        }

        // メニューの印を今の値に合わせる
        // Keep the menu check marks in step with the current value
        [UnityEditor.MenuItem(WebMenuPath, true)]
        private static bool ValidateWeb()
        {
            UnityEditor.Menu.SetChecked(WebMenuPath, Current == UiSystem.Web);
            return true;
        }

        [UnityEditor.MenuItem(GameUiMenuPath, true)]
        private static bool ValidateGameUi()
        {
            UnityEditor.Menu.SetChecked(GameUiMenuPath, Current == UiSystem.GameUi);
            return true;
        }
    }
#endif
}
```

- [ ] **Step 4: `WebUiStartup.cs` に系を受け取らせ、GameUi の起動を既存の Editor スキップ経路に載せる**

旧テキスト（:1-23）:
```csharp
using System;
using System.Threading;
using Client.Game.InGame.BugReport.WebUiHealth;
using Client.WebUiHost.Health;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Starter.Initialization.Boot
{
    internal static class WebUiStartup
    {
        internal static async UniTask<bool> StartAsync(CancellationToken exitToken)
        {
#if UNITY_EDITOR
            // 印があれば起動失敗と同経路
            // With the mark, follow the startup-failure path
            if (WebUiHostEditorSkip.ConsumeSkipMark())
            {
                Debug.LogWarning("[WebUiHost] Editor限定スイッチによりWebホストを起動しません（Hub=nullで続行）");
                WebUiHealthRecorder.RecordHostStartup(WebUiHostStartupState.SkippedByEditorSwitch);
                return false;
            }
#endif
```
新テキスト:
```csharp
using System;
using System.Threading;
using Client.Game.InGame.BugReport.WebUiHealth;
using Client.Input;
using Client.WebUiHost.Health;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Starter.Initialization.Boot
{
    internal static class WebUiStartup
    {
        internal static async UniTask<bool> StartAsync(CancellationToken exitToken, UiSystem uiSystem)
        {
#if UNITY_EDITOR
            // 全体スイッチが GameUi の起動と印のある起動は、起動失敗と同経路で Web ホストを起動しない（plan §2.1 0-10・§3.3 R1）。印は毎回消費して次の起動へ漏らさない
            // A GameUi boot and a marked boot skip the web host along the startup-failure path (plan §2.1 0-10, §3.3 R1); the mark is always consumed so it never leaks into the next boot
            var skipMarked = WebUiHostEditorSkip.ConsumeSkipMark();
            var skipHost = uiSystem == UiSystem.GameUi || skipMarked;
            // 飛ばしたかは WebUiHost の起動の状態へ押し込む（CEF の生成可否が読む）。診断記録は別に残す
            // Push whether the host was skipped into WebUiHost's boot state (CEF creation reads it); the diagnostic record is kept separately
            Client.WebUiHost.Boot.WebUiHost.SetHostSkippedThisBoot(skipHost);
            if (skipHost)
            {
                Debug.LogWarning($"[WebUiHost] Editor限定スイッチによりWebホストを起動しません（uiSystem={uiSystem} skipMark={skipMarked}、Hub=nullで続行）");
                WebUiHealthRecorder.RecordHostStartup(WebUiHostStartupState.SkippedByEditorSwitch);
                return false;
            }
#endif
```
（配布ビルドでは `uiSystem` は常に Web で、引数は読まれない。C# は未使用引数を警告しない。配布ビルドでは `SetHostSkippedThisBoot` を呼ばず既定 false のまま）

`WebUiHost.cs`: `public static WebSocketHub Hub => _hub;` の次に足す（164 → 172 行）:
```csharp

        // 今回の起動で Web ホストを飛ばしたか。書くのは WebUiStartup だけで、CEF の生成可否が読む（診断記録 WebUiHealthRecorder とは別の起動の状態）
        // Whether this boot skipped the web host; only WebUiStartup writes it and CEF creation reads it (boot state, separate from the WebUiHealthRecorder diagnostics)
        public static bool HostSkippedThisBoot { get; private set; }

        internal static void SetHostSkippedThisBoot(bool skipped)
        {
            HostSkippedThisBoot = skipped;
        }
```

- [ ] **Step 5: `InitializeScenePipeline.cs` で起動の系を 1 回だけ決める**

旧テキスト（:79-80）:
```csharp
            var exitToken = Application.exitCancellationToken;
            await Initialization.Boot.WebUiStartup.StartAsync(exitToken);
```
新テキスト:
```csharp
            var exitToken = Application.exitCancellationToken;
            // 描く系を起動で1回だけ決める。GameUi の起動では Web ホストを起動しない（plan §3.3 R1・R4）
            // Decide the drawing system once per boot; a GameUi boot does not start the web host (plan §3.3 R1, R4)
            var uiSystem = Initialization.Boot.UiSystemBootSelection.ResolveAndApply();
            await Initialization.Boot.WebUiStartup.StartAsync(exitToken, uiSystem);
```
（189 → 192 行。`PlaytestBootUnityCallbackBindingTest` の「遠隔実行の有効化 → WebUi 起動」の順の検査はそのまま通る）

- [ ] **Step 6: `WebUiCefNavigator.cs` の Awake で、Editor スキップの起動では CEF を作らない**

旧テキスト:
```csharp
        private void Awake()
        {
#if UNITY_EDITOR_LINUX
```
新テキスト:
```csharp
        private void Awake()
        {
            // Editor スイッチで Web ホストを起動しなかった起動では CEF を作らず、絵（同じ GameObject の RawImage）も入力の受け手も残さない（plan §3.3 R1）
            // On a boot where the Editor switch skipped the web host, create no CEF and leave neither the picture (the RawImage on this GameObject) nor an input receiver (plan §3.3 R1)
            if (Boot.WebUiHost.HostSkippedThisBoot)
            {
                Debug.Log("[WebUiHost] the web host was skipped by the Editor switch; the CEF browser object stays inactive");
                gameObject.SetActive(false);
                return;
            }
#if UNITY_EDITOR_LINUX
```
using は足さない（`Client.WebUiHost.Cef` から同じ asmdef の `Client.WebUiHost.Boot.WebUiHost` を `Boot.` で引く）。診断記録 `WebUiHealthRecorder.HostStartup` は読まない（バグ報告用の記録で、制御の合図にしない。判断記録 W-4）。CEF のブラウザ生成は `CefUnityBrowserSample.Start` なので、同じ GameObject の Awake で非アクティブにすれば生成されない（同ファイルの Linux 分岐が `browser.enabled = false` で止めているのと同じ前提）

- [ ] **Step 7: dispatcher の休止拒否を消し、`UiStateActions.cs` を master 版へ戻す**

`WebSocketMessageDispatcher.cs` から次の 7 行（:123-129）と直後の空行を消す:
```csharp
            // 休止中（UITK が主パネルを出す間）の画面遷移要求は休止前に送られた残りなので、命綱のポーズを閉じさせない（R1）
            // A screen-transition request during suspension was sent before it, so it must not close the lifeline pause (R1)
            if (msg.Type == RequestUiStateActionHandler.Type && UiInputExclusivity.IsWebUiSuspended)
            {
                await RejectAsync("web_ui_suspended", "the web UI is suspended while the game UI owns the main panel");
                return;
            }
```
`UiInputExclusivity.RecordWebMessageReceived(...)`（CEF 停止の記録）と `UiInputExclusivity.SetState(...)` はそのまま残す。

Run:
```bash
source "${TMPDIR:-/tmp}/uitk-w0-switch/start.env"
git checkout "$MERGED_MASTER" -- moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/UiStateActions.cs
git diff "$MERGED_MASTER" -- moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/UiStateActions.cs | wc -l
```
Expected: `0`

- [ ] **Step 8: 旧 R1 の C# を消す（`WebUiSuspension`・`GameUiRoot` の CEF・休止拒否のテスト）**

Run:
```bash
git rm -r moorestech_client/Assets/Scripts/Client.Ui/Foundation/Routing moorestech_client/Assets/Scripts/Client.Ui/Foundation/Routing.meta
git rm moorestech_client/Assets/Scripts/Client.Tests/WebUiHost/Transport/WebSocketSuspendedUiStateRequestTest.cs moorestech_client/Assets/Scripts/Client.Tests/WebUiHost/Transport/WebSocketSuspendedUiStateRequestTest.cs.meta
```

`MainGameUiRegistration.cs` から `using Client.Ui.Foundation.Routing;` と `builder.RegisterEntryPoint<WebUiSuspension>();` の 2 行を消す（ファイル全体の作り直しは Task 4）。

`GameUiRoot.cs` を全置換する:
```csharp
using System;
using Client.Ui.Foundation.Assets;
using Client.Ui.Foundation.Layers;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Root
{
    /// <summary>
    /// UITK の UIDocument 1 枚を持ち、層スタックを組む（トークンはテーマ TSS から当たる）
    /// Owns the single UITK UIDocument, builds the layer stack (tokens come from the theme TSS)
    /// </summary>
    public class GameUiRoot : MonoBehaviour
    {
        private static GameUiRoot _instance;
        public static GameUiRoot Instance => _instance;

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private UiAssetCatalog assetCatalog;

        // シリアライズ値の公開に限った素通しの読み出し（D-24）
        // Pass-through accessor limited to exposing a serialized value (D-24)
        public UiAssetCatalog AssetCatalog => assetCatalog;

        private UiLayerStack _layers;

        private void Awake()
        {
            _instance = this;
            // 必須参照が欠けたら起動の段階で止める
            // Stop at startup when a required reference is missing
            if (uiDocument == null || assetCatalog == null) throw new InvalidOperationException("[GameUiRoot] required references are not assigned on MainGameUI.prefab");
            assetCatalog.ValidateAllAssigned();
            var root = uiDocument.rootVisualElement;
            _layers = UiLayerStack.Build(root);
        }

        // ルート共通の入力規則とポインタ判定を付ける先
        // Where the root-wide input rules and the pointer probe attach
        public VisualElement GetRootElement()
        {
            return uiDocument.rootVisualElement;
        }

        public VisualElement GetLayer(UiLayer layer)
        {
            return _layers.GetLayer(layer);
        }

        // ロケールなど、パネル全体に効くクラスをルートへ付け外しする
        // Toggles panel-wide classes such as the locale on the root
        public void SetRootClassEnabled(string className, bool enabled)
        {
            uiDocument.rootVisualElement.EnableInClassList(className, enabled);
        }
    }
}
```
（`MainGameUI.prefab` に残る `cefRawImage:` の行は使われない値として残り、Unity が次にその prefab を保存したときに落とす。手で編集しない）

- [ ] **Step 9: L0 のテストを書く・直す**

`UiInputExclusivityGameUiTest.cs` を全置換:
```csharp
using Client.Input;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Input
{
    // GameUi の系での文字入力・ESCラッチ・ポインタprobeの登録を確かめる。Web の値は読まない（plan §3.3 R2）
    // Verifies text input, the ESC latch and pointer-probe registration in the GameUi system; web values are not read (plan §3.3 R2)
    public class UiInputExclusivityGameUiTest
    {
        private class FixedProbe : IGameUiPointerProbe
        {
            private readonly bool _result;
            public FixedProbe(bool result) { _result = result; }
            public bool IsPointerOverGameUi(Vector2 screenPosition) => _result;
        }

        [SetUp]
        public void SetUp()
        {
            UiInputExclusivity.SetActiveUiSystem(UiSystem.GameUi);
        }

        [TearDown]
        public void TearDown()
        {
            UiInputExclusivity.SetState(false, false);
            UiInputExclusivity.SetGameUiTextInputFocused(false);
            UiInputExclusivity.SetGameUiPointerProbe(null);
            UiInputExclusivity.ClearEscapeLatchForTest();
            UiInputExclusivity.SetActiveUiSystem(UiSystem.Web);
        }

        [Test]
        public void GameUiFocusAloneIsTextInputFocused()
        {
            UiInputExclusivity.SetGameUiTextInputFocused(true);

            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);
            Assert.IsTrue(UiInputExclusivity.IsSuppressed(InputSuppressionScope.Keyboard));
            UiInputExclusivity.SetGameUiTextInputFocused(false);
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
        }

        [Test]
        public void WebFocusIsNotReadInGameUiSystem()
        {
            // Web が入力欄フォーカスを送っても GameUi の系では読まない
            // A web text-field focus is not read in the GameUi system
            UiInputExclusivity.SetState(false, true);
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
            Assert.IsFalse(UiInputExclusivity.IsSuppressed(InputSuppressionScope.Keyboard));

            // Web が false を送っても GameUi の欄のフォーカスは残る
            // Web sending false keeps the GameUi field's focus
            UiInputExclusivity.SetGameUiTextInputFocused(true);
            UiInputExclusivity.SetState(false, false);
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);
        }

        [Test]
        public void EscapeLatchIsFalseBeforeAndTrueAfterConsume()
        {
            Assert.IsFalse(UiInputExclusivity.IsEscapeConsumedThisFrame);
            UiInputExclusivity.ConsumeEscapeThisFrame();
            Assert.IsTrue(UiInputExclusivity.IsEscapeConsumedThisFrame);
        }

        [Test]
        public void PointerProbeRegistrationAndRelease()
        {
            Assert.IsFalse(UiInputExclusivity.IsPointerOverGameUi(Vector2.zero));

            UiInputExclusivity.SetGameUiPointerProbe(new FixedProbe(true));
            Assert.IsTrue(UiInputExclusivity.IsPointerOverGameUi(Vector2.zero));

            UiInputExclusivity.SetGameUiPointerProbe(new FixedProbe(false));
            Assert.IsFalse(UiInputExclusivity.IsPointerOverGameUi(Vector2.zero));

            UiInputExclusivity.SetGameUiPointerProbe(null);
            Assert.IsFalse(UiInputExclusivity.IsPointerOverGameUi(Vector2.zero));
        }
    }
}
```

`UiInputExclusivityWebSuspensionTest.cs` を改名して全置換:
```bash
git mv moorestech_client/Assets/Scripts/Client.Tests/Ui/Input/UiInputExclusivityWebSuspensionTest.cs moorestech_client/Assets/Scripts/Client.Tests/Ui/Input/UiInputExclusivityActiveSystemTest.cs
git mv moorestech_client/Assets/Scripts/Client.Tests/Ui/Input/UiInputExclusivityWebSuspensionTest.cs.meta moorestech_client/Assets/Scripts/Client.Tests/Ui/Input/UiInputExclusivityActiveSystemTest.cs.meta
```
```csharp
using System.Collections.Generic;
using Client.Input;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace Client.Tests.Ui.Input
{
    // 全体スイッチの系だけを読み、もう片方の値を論理和にしないことを固定する（plan §3.3 R2 の L0）
    // Pins that only the switch's system is read and the other side is never ORed in (L0 of plan §3.3 R2)
    public class UiInputExclusivityActiveSystemTest
    {
        private class AlwaysOverProbe : IGameUiPointerProbe
        {
            public bool IsPointerOverGameUi(Vector2 screenPosition) => true;
        }

        [TearDown]
        public void TearDown()
        {
            UiInputExclusivity.SetState(false, false);
            UiInputExclusivity.SetGameUiTextInputFocused(false);
            UiInputExclusivity.SetGameUiPointerProbe(null);
            UiInputExclusivity.SetActiveUiSystem(UiSystem.Web);
        }

        [Test]
        public void Web系ではWebの値だけを読みGameUiの値を読まない()
        {
            UiInputExclusivity.SetActiveUiSystem(UiSystem.Web);
            UiInputExclusivity.SetGameUiPointerProbe(new AlwaysOverProbe());
            UiInputExclusivity.SetGameUiTextInputFocused(true);

            // GameUi の占有だけがある間は何も占有していない
            // While only GameUi claims, nothing is occupied
            Assert.IsFalse(UiInputExclusivity.IsPointerOverGameUi(Vector2.zero));
            Assert.IsFalse(UiInputExclusivity.IsPointerOverActiveUi(Vector2.zero));
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);

            UiInputExclusivity.SetState(true, true);
            Assert.IsTrue(UiInputExclusivity.IsPointerOverActiveUi(Vector2.zero));
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);
            Assert.IsTrue(UiInputExclusivity.IsSuppressed(InputSuppressionScope.Keyboard));
        }

        [Test]
        public void GameUi系ではGameUiの値だけを読みWebの値を読まない()
        {
            UiInputExclusivity.SetActiveUiSystem(UiSystem.GameUi);
            UiInputExclusivity.SetState(true, true);

            // Web の占有だけがある間は何も占有していない
            // While only the web claims, nothing is occupied
            Assert.IsFalse(UiInputExclusivity.IsPointerOverWebUi);
            Assert.IsFalse(UiInputExclusivity.IsPointerOverActiveUi(Vector2.zero));
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);

            UiInputExclusivity.SetGameUiPointerProbe(new AlwaysOverProbe());
            UiInputExclusivity.SetGameUiTextInputFocused(true);
            Assert.IsTrue(UiInputExclusivity.IsPointerOverActiveUi(Vector2.zero));
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);
        }

        [Test]
        public void 系の切替で文字入力の占有を計算し直し購読者へ届ける()
        {
            UiInputExclusivity.SetActiveUiSystem(UiSystem.Web);
            UiInputExclusivity.SetState(false, true);
            var received = new List<bool>();
            using var subscription = UiInputExclusivity.TextInputFocused.Subscribe(received.Add);

            // Web の true → GameUi へ切替で false → GameUi の欄で true。Web へ戻しても Web の値も true なので届かない
            // Web true -> false on switching to GameUi -> true from a GameUi field; switching back to Web stays true so nothing new arrives
            UiInputExclusivity.SetActiveUiSystem(UiSystem.GameUi);
            UiInputExclusivity.SetGameUiTextInputFocused(true);
            UiInputExclusivity.SetActiveUiSystem(UiSystem.Web);

            CollectionAssert.AreEqual(new[] { true, false, true }, received);
        }
    }
}
```

`UiInputExclusivityWebSilenceTest.cs`: (a) `SetUp` の本文を次の 2 行にする:
```csharp
            UiInputExclusivity.SetActiveUiSystem(UiSystem.Web);
            UiInputExclusivity.ResetWebSilenceForTest();
```
(b) `TearDown` から `UiInputExclusivity.SetWebUiSuspended(false);` の 1 行を消す。(c) テスト `休止とは独立に無応答を判定する`（:69-85。直前の 2 行コメントを含む）を消す。残りは 3 件（`閾値を超えて受信が途絶えるとWebの値を無視し受信と評価で戻る`・`一度も受信していなければ無視しない`・`受信からの秒数をStopwatchの周波数で換算する`）。W0-cef が master に入れる `WebUiInputExclusivityWebSilenceTest` の 3 件と同じ中身になる（cef-record §5）

`UiTestHost.cs`: using に `using Client.Input;` を足し、`SetUpGameUiTheme` の後ろへ次を足す:
```csharp

        // UITK の部品と画面は GameUi の系で動く。入力の裁きを GameUi の系にして試し、終わったら既定の Web へ戻す（plan §3.3 R2）
        // UITK parts and screens run in the GameUi system; arbitrate input as GameUi during the test and restore the default Web afterwards (plan §3.3 R2)
        [SetUp]
        public void SelectGameUiSystemForUiTest()
        {
            UiInputExclusivity.SetActiveUiSystem(UiSystem.GameUi);
        }

        [TearDown]
        public void RestoreWebUiSystemAfterUiTest()
        {
            UiInputExclusivity.SetActiveUiSystem(UiSystem.Web);
        }
```
（派生の `ScreenTestHost` は `SetUpScreenHost`/`TearDownScreenHost` で名前が重ならない。派生の TearDown が先に走るので、`GameTextFieldTest` などが `SetGameUiTextInputFocused(false)` で後始末してから Web へ戻る）

`UiSystemSwitchTest.cs` を作る:
```csharp
using Client.Input;
using Client.Starter.Initialization.Boot;
using NUnit.Framework;

namespace Client.Tests.Ui.Routing
{
    // 全体スイッチの値が次の起動の解決に乗り、入力の裁きへ押し込まれる（plan §3.3 R1・R4）
    // The global switch's value reaches the next boot's resolution and is pushed into input arbitration (plan §3.3 R1, R4)
    public class UiSystemSwitchTest
    {
        [TearDown]
        public void TearDown()
        {
            UiSystemEditorSwitch.Set(UiSystem.Web);
            UiInputExclusivity.SetActiveUiSystem(UiSystem.Web);
        }

        [Test]
        public void GameUiへ切り替えた起動はGameUiで描き入力の裁きもGameUiになる()
        {
            UiSystemEditorSwitch.Set(UiSystem.GameUi);

            Assert.AreEqual(UiSystem.GameUi, UiSystemEditorSwitch.Current);
            Assert.AreEqual(UiSystem.GameUi, UiSystemBootSelection.ResolveAndApply());
            Assert.AreEqual(UiSystem.GameUi, UiInputExclusivity.ActiveUiSystem);
        }

        [Test]
        public void Webへ戻した起動はWebで描く()
        {
            UiSystemEditorSwitch.Set(UiSystem.GameUi);
            UiSystemEditorSwitch.Set(UiSystem.Web);

            Assert.AreEqual(UiSystem.Web, UiSystemEditorSwitch.Current);
            Assert.AreEqual(UiSystem.Web, UiSystemBootSelection.ResolveAndApply());
            Assert.AreEqual(UiSystem.Web, UiInputExclusivity.ActiveUiSystem);
        }
    }
}
```

- [ ] **Step 10: コンパイルし、L0/L1 を回す**

Run:
```bash
wc -l moorestech_client/Assets/Scripts/Client.Input/UiInputExclusivity.cs moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.(Input|Routing|Components|Screens)\." --timeout-seconds 1500
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.(Input\.|Player\.TextInputMovementLockTest|WebUiHost\.)" --timeout-seconds 1500
```
Expected: 2 ファイルとも 200 行未満。ErrorCount 0。`UiInputExclusivityGameUiTest` 4・`UiInputExclusivityActiveSystemTest` 3・`UiInputExclusivityWebSilenceTest` 3・`UiSystemSwitchTest` 2 を含めて全件 PASS（Task 1 Step 2 のベースラインで既に落ちていたものは除く）。新規 `.cs` の `.meta`（`UiSystemBootSelection.cs.meta`・`UiSystemSwitchTest.cs.meta`）が Unity により生成されている

- [ ] **Step 11: コミットする**

```bash
git add -A moorestech_client/Assets/Scripts
git commit -m "feat(uitk): 全体スイッチの値を起動で1回だけ決め、入力の裁きを片側読みにし、GameUi の起動では Web ホストと CEF を作らない（albof.17）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: DSL のロケータを片側読みへ

**Files:**
- Rename+Modify: `moorestech_client/Assets/Scripts/Client.Playtest/UiElements/CompositeUiElementLocator.cs` → `SwitchedUiElementLocator.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Playtest/UiElements/PlaytestUiLocatorSession.cs`
- Rename+Modify: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Dsl/CompositeUiElementLocatorTest.cs` → `SwitchedUiElementLocatorTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Dsl/FakeUiElementLocator.cs`

**Interfaces:**
- Consumes: `Client.Input.UiSystem`・`UiInputExclusivity.ActiveUiSystem`（Task 2）
- Produces: `internal sealed class SwitchedUiElementLocator : IUiElementLocator`（ctor `(UiSystem activeSystem, IUiElementLocator gameUi, IUiElementLocator web)`。選んだ系を外へ出すプロパティは持たない＝テスト専用 public を作らない）。`PlaytestUiLocatorSession.Locator` は `public static IUiElementLocator Locator { get; private set; }` になり、`Start()` のたびに起動の系で選び直す。`FakeUiElementLocator.FindCount`

- [ ] **Step 1: 改名して `SwitchedUiElementLocator.cs` を書く**

```bash
git mv moorestech_client/Assets/Scripts/Client.Playtest/UiElements/CompositeUiElementLocator.cs moorestech_client/Assets/Scripts/Client.Playtest/UiElements/SwitchedUiElementLocator.cs
git mv moorestech_client/Assets/Scripts/Client.Playtest/UiElements/CompositeUiElementLocator.cs.meta moorestech_client/Assets/Scripts/Client.Playtest/UiElements/SwitchedUiElementLocator.cs.meta
git mv moorestech_client/Assets/Scripts/Client.Tests/Ui/Dsl/CompositeUiElementLocatorTest.cs moorestech_client/Assets/Scripts/Client.Tests/Ui/Dsl/SwitchedUiElementLocatorTest.cs
git mv moorestech_client/Assets/Scripts/Client.Tests/Ui/Dsl/CompositeUiElementLocatorTest.cs.meta moorestech_client/Assets/Scripts/Client.Tests/Ui/Dsl/SwitchedUiElementLocatorTest.cs.meta
```
```csharp
using Client.Input;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.UiElements
{
    /// <summary>
    /// 全体スイッチで選ばれた系のロケータだけで探す。もう片方は照会もセッションも開かない（plan §3.3 R2・§4.4）
    /// Looks up only with the locator of the system chosen by the global switch; the other side is neither queried nor opened (plan §3.3 R2, §4.4)
    /// </summary>
    internal sealed class SwitchedUiElementLocator : IUiElementLocator
    {
        private readonly IUiElementLocator _active;

        public SwitchedUiElementLocator(UiSystem activeSystem, IUiElementLocator gameUi, IUiElementLocator web)
        {
            _active = activeSystem == UiSystem.GameUi ? gameUi : web;

            // シナリオのログから、どちらの系で探したかを辿れるようにする
            // Leave in the scenario log which system the lookups used
            Debug.Log($"[PlaytestUi] locator reads only the {activeSystem} UI");
        }

        public void StartSession()
        {
            _active.StartSession();
        }

        public void StopSession()
        {
            _active.StopSession();
        }

        public UniTask<UiElementSnapshot> Find(string testid, float remainingSeconds)
        {
            return _active.Find(testid, remainingSeconds);
        }

        public UniTask<UiSetTextResult> SetText(string testid, string text, float remainingSeconds)
        {
            return _active.SetText(testid, text, remainingSeconds);
        }
    }
}
```

- [ ] **Step 2: `PlaytestUiLocatorSession.cs` を全置換する**

```csharp
using System;
using Client.Input;
using Client.Playtest.UiElements.GameUi;
using Client.Playtest.WebUi;

namespace Client.Playtest.UiElements
{
    /// <summary>
    ///     ・シナリオ共通のロケータ保持
    ///     ・照会受け口と入力転送の開閉
    ///     ・Shared locator for scenarios
    ///     ・Query receiver and input forwarding lifecycle
    /// </summary>
    public static class PlaytestUiLocatorSession
    {
        // 両系の実装は1つずつ持ち回し、どちらを使うかだけを Start で決め直す
        // Keep one instance per renderer and only re-decide at Start which one to use
        private static readonly IUiElementLocator GameUiLocator = new GameUiElementLocator();
        private static readonly IUiElementLocator WebLocator = new WebUiElementLocator();
        private static int _generation;
        private static bool _active;

        // 全体スイッチの系のロケータ。起動で決まった系を Start のたびに読み直す（plan §3.3 R2）
        // The locator for the global switch's system; Start re-reads the system decided at boot (plan §3.3 R2)
        public static IUiElementLocator Locator { get; private set; } = new SwitchedUiElementLocator(UiSystem.Web, GameUiLocator, WebLocator);

        // 冪等。PlaytestRunnerとRun前のprobeの両方から呼ばれる
        // Idempotent; called from both PlaytestRunner and pre-Run probes
        public static void Start()
        {
            if (!_active)
            {
                _generation++;
                Locator = new SwitchedUiElementLocator(UiInputExclusivity.ActiveUiSystem, GameUiLocator, WebLocator);
            }
            _active = true;
            Locator.StartSession();
        }

        // 成否にかかわらず転送を止め、次回実行へ応答を持ち越さない
        // Stop forwarding and discard pending responses regardless of outcome
        public static void Stop()
        {
            _active = false;
            Locator.StopSession();
        }
        // 開始した実行だけに再送を許す
        // Permit retries only from the execution that started the operation
        internal static int CaptureGeneration()
        {
            RequireGeneration(_generation);
            return _generation;
        }

        internal static void RequireGeneration(int generation)
        {
            if (!_active || generation != _generation)
                throw new InvalidOperationException("UI operation belongs to a stopped session");
        }
    }
}
```
（静的フィールドは書いた順に初期化されるので、`Locator` の初期化子が使う 2 つのロケータは先に出来ている。`PlaytestRequestLifetimeTest.StopStartTerminatesOldTextRetryWithoutSecondDelivery` の Stop→Start は、同じ `WebLocator` の止めて開く順が前と同じなので変わらない）

- [ ] **Step 3: `FakeUiElementLocator.cs` に照会回数を足す**

`public int SetTextCount { get; private set; }` の次の行に足す:
```csharp
        public int FindCount { get; private set; }
```
`Find` の先頭（`LastFindBudgetSeconds = remainingSeconds;` の前）に足す:
```csharp
            FindCount++;
```

- [ ] **Step 4: `SwitchedUiElementLocatorTest.cs` を全置換する（10 件）**

```csharp
using Client.Input;
using Client.Playtest.UiElements;
using NUnit.Framework;

namespace Client.Tests.Ui.Dsl
{
    /// <summary>
    /// 全体スイッチの系のロケータだけで探し、もう片方には照会もセッションも開かないことを偽の 2 ロケータで固定する（plan §3.3 R2 の L0）
    /// Pins with two fake locators that only the switch's locator is used and the other is neither queried nor opened (L0 of plan §3.3 R2)
    /// </summary>
    public class SwitchedUiElementLocatorTest
    {
        private const string Testid = "dsl-target";

        [Test]
        public void GameUi系はGameUiの結果を返しWebに照会しない()
        {
            var gameUi = Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied);
            var web = Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied);
            var locator = new SwitchedUiElementLocator(UiSystem.GameUi, gameUi, web);

            Assert.AreSame(gameUi.LastFound, Find(locator));
            Assert.AreEqual(0, web.FindCount, "GameUi 系で Web に照会した");
        }

        [Test]
        public void GameUi系でGameUiに無ければWebへ回さずNotFound()
        {
            var web = Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied);
            var locator = new SwitchedUiElementLocator(UiSystem.GameUi, Fake(UiElementLookupStatus.NotFound, UiSetTextStatus.NotFound), web);

            Assert.AreEqual(UiElementLookupStatus.NotFound, Find(locator).Status);
            Assert.AreEqual(0, web.FindCount);
        }

        [Test]
        public void GameUi系の未準備はそのまま返す()
        {
            var web = Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied);
            var locator = new SwitchedUiElementLocator(UiSystem.GameUi, Fake(UiElementLookupStatus.RendererUnavailable, UiSetTextStatus.RendererUnavailable), web);

            Assert.AreEqual(UiElementLookupStatus.RendererUnavailable, Find(locator).Status);
            Assert.AreEqual(UiSetTextStatus.RendererUnavailable, SetText(locator).Status);
            Assert.AreEqual((0, 0), (web.FindCount, web.SetTextCount));
        }

        [Test]
        public void GameUi系の文字入力はGameUiだけに入れる()
        {
            var gameUi = Fake(UiElementLookupStatus.NotFound, UiSetTextStatus.NotFound);
            var web = Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied);
            var locator = new SwitchedUiElementLocator(UiSystem.GameUi, gameUi, web);

            Assert.AreEqual(UiSetTextStatus.NotFound, SetText(locator).Status);
            Assert.AreEqual((1, 0), (gameUi.SetTextCount, web.SetTextCount));
        }

        [Test]
        public void Web系はWebの結果を返しGameUiに照会しない()
        {
            var gameUi = Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied);
            var web = Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied);
            var locator = new SwitchedUiElementLocator(UiSystem.Web, gameUi, web);

            Assert.AreSame(web.LastFound, Find(locator));
            Assert.AreEqual(0, gameUi.FindCount, "Web 系で GameUi に照会した");
        }

        [Test]
        public void Web系でWebに無ければGameUiへ回さずNotFound()
        {
            var gameUi = Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied);
            var locator = new SwitchedUiElementLocator(UiSystem.Web, gameUi, Fake(UiElementLookupStatus.NotFound, UiSetTextStatus.NotFound));

            Assert.AreEqual(UiElementLookupStatus.NotFound, Find(locator).Status);
            Assert.AreEqual(0, gameUi.FindCount);
        }

        [Test]
        public void Web系の文字入力はWebだけに入れる()
        {
            var gameUi = Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied);
            var web = Fake(UiElementLookupStatus.Found, UiSetTextStatus.NotInteractable);
            var locator = new SwitchedUiElementLocator(UiSystem.Web, gameUi, web);

            Assert.AreEqual(UiSetTextStatus.NotInteractable, SetText(locator).Status);
            Assert.AreEqual((0, 1), (gameUi.SetTextCount, web.SetTextCount));
        }

        [Test]
        public void 選んだ系の重複はAmbiguousのまま返す()
        {
            var gameUiSide = new SwitchedUiElementLocator(UiSystem.GameUi, Fake(UiElementLookupStatus.Ambiguous, UiSetTextStatus.Ambiguous), Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied));
            var webSide = new SwitchedUiElementLocator(UiSystem.Web, Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied), Fake(UiElementLookupStatus.Ambiguous, UiSetTextStatus.Ambiguous));

            Assert.AreEqual(UiElementLookupStatus.Ambiguous, Find(gameUiSide).Status);
            Assert.AreEqual(UiSetTextStatus.Ambiguous, SetText(gameUiSide).Status);
            Assert.AreEqual(UiElementLookupStatus.Ambiguous, Find(webSide).Status);
        }

        [Test]
        public void 照会の残り時間をそのまま選んだ系へ渡す()
        {
            // 段階 1 の「GameUi で見つかった後の Web 照会を 0.5 秒で切る」は両系照会が無くなり要らない
            // Stage 1's 0.5s cut of the web duplicate check is gone with the two-sided lookup
            var gameUi = Fake(UiElementLookupStatus.Found, UiSetTextStatus.Applied);
            new SwitchedUiElementLocator(UiSystem.GameUi, gameUi, Fake(UiElementLookupStatus.NotFound, UiSetTextStatus.NotFound)).Find(Testid, 3f).GetAwaiter().GetResult();

            Assert.AreEqual(3f, gameUi.LastFindBudgetSeconds);
        }

        [Test]
        public void セッションは選んだ系だけを開閉する()
        {
            var gameUi = Fake(UiElementLookupStatus.NotFound, UiSetTextStatus.NotFound);
            var web = Fake(UiElementLookupStatus.NotFound, UiSetTextStatus.NotFound);
            var gameUiSide = new SwitchedUiElementLocator(UiSystem.GameUi, gameUi, web);
            gameUiSide.StartSession();
            gameUiSide.StopSession();
            Assert.AreEqual((1, 1, 0, 0), (gameUi.StartCount, gameUi.StopCount, web.StartCount, web.StopCount));

            var webSide = new SwitchedUiElementLocator(UiSystem.Web, gameUi, web);
            webSide.StartSession();
            webSide.StopSession();
            Assert.AreEqual((1, 1, 1, 1), (gameUi.StartCount, gameUi.StopCount, web.StartCount, web.StopCount));
        }

        private static FakeUiElementLocator Fake(UiElementLookupStatus findStatus, UiSetTextStatus setTextStatus)
        {
            return new FakeUiElementLocator(findStatus, setTextStatus);
        }

        // 偽は同期に完了するので、その場で結果を取り出せる
        // The fakes complete synchronously, so the result can be read immediately
        private static UiElementSnapshot Find(SwitchedUiElementLocator locator)
        {
            return locator.Find(Testid, 1f).GetAwaiter().GetResult();
        }

        private static UiSetTextResult SetText(SwitchedUiElementLocator locator)
        {
            return locator.SetText(Testid, "text", 1f).GetAwaiter().GetResult();
        }
    }
}
```

- [ ] **Step 5: コンパイルしテストを回す**

Run:
```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.(Ui\.Dsl|Playtest)\." --timeout-seconds 1500
git grep -n "CompositeUiElementLocator" -- moorestech_client .agents
```
Expected: ErrorCount 0。`SwitchedUiElementLocatorTest` 10 件と `Client.Tests.Playtest.*` が PASS。grep は 0 件

- [ ] **Step 6: コミットする**

```bash
git add -A moorestech_client/Assets/Scripts
git commit -m "feat(uitk): DSL のロケータを全体スイッチの系の1本だけで探す形にする（albof.17・R2）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: ルータを画面だけに縮め、分岐を composition root の 1 か所へ（L3 を UITK スイッチで）

**Files:**
- Delete: `moorestech_client/Assets/Scripts/Client.Ui.State/Routing/UiOwnershipTable.cs`・`UiSurface.cs`（`.meta` も）、`moorestech_client/Assets/Scripts/Client.Tests/Ui/Routing/UiOwnershipTableTest.cs`（`.meta` も）
- Modify: `moorestech_client/Assets/Scripts/Client.Ui.State/Routing/UiRoutingState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui/Screens/PauseMenu/PauseMenuScreenPresenter.cs:63-68`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/ImeCompositionModeApplier.cs:9-12,23-24`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiRegistration.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiStateRegistration.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs:111`
- Modify（master 版へ）: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/UiStateTopic.cs`・`moorestech_client/Assets/Scripts/Client.Tests/WebUi/UiStateSubStatePublishTest.cs`・`moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractTest.cs`・`moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/ui_state.json`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:96`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/UiSystemPlayModeTestBase.cs`・`WebUiSystemSkeletonTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/GameUiSkeletonTest.cs`・`PauseMenuGameUiTest.cs`（全置換）・`GameUiInputTest.cs`・`GameUiTextFieldWiringChecks.cs`

**Interfaces:**
- Consumes: `UiSystem`・`UiInputExclusivity.ActiveUiSystem`・`UiSystemEditorSwitch.Set`・`WebUiHost.HostSkippedThisBoot`（Task 2）、`PlaytestUiLocatorSession.Locator`（Task 3。`IUiElementLocator` のまま使い、具象型へキャストしない）
- Produces:
  - `UiRoutingState(UIStateControl uiStateControl, IReadOnlyList<INestedPauseScreenState> nestedPauseScreens)`、`IReadOnlyReactiveProperty<UiRoutingSnapshot> Current`、`UiRoutingSnapshot(UiScreen screen)` と `Screen` だけ（後続の画面レーンは `Current.Value.Screen` と `UiScreenTraitsTable` だけで出し分ける。設計書 §4.4）
  - `MainGameUiRegistration.Register(ContainerBuilder builder, GameUiRoot gameUiRoot, UiSystem uiSystem)`。後続の画面レーンは GameUi 分岐の下に 1 行ずつ足す（実行計画 E-1）
  - L3 の土台 `abstract class UiSystemPlayModeTestBase`（`protected static void SelectUiSystemBeforePlayMode(UiSystem system)`、`[TearDown] RestoreDefaultUiSystem()`）。後続の L3（設計書 §4.3「その段の画面が全体スイッチ UITK で開く・閉じる」）はこれを継承する

- [ ] **Step 1: 所有表を消し、ルータを画面だけに縮める**

Run:
```bash
git rm moorestech_client/Assets/Scripts/Client.Ui.State/Routing/UiOwnershipTable.cs moorestech_client/Assets/Scripts/Client.Ui.State/Routing/UiOwnershipTable.cs.meta moorestech_client/Assets/Scripts/Client.Ui.State/Routing/UiSurface.cs moorestech_client/Assets/Scripts/Client.Ui.State/Routing/UiSurface.cs.meta moorestech_client/Assets/Scripts/Client.Tests/Ui/Routing/UiOwnershipTableTest.cs moorestech_client/Assets/Scripts/Client.Tests/Ui/Routing/UiOwnershipTableTest.cs.meta
```

`UiRoutingState.cs` を全置換:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.NestedPause;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Routing
{
    /// <summary>
    /// 現在の画面を購読で保つ。どちらの系で描くかは持たない（全体スイッチは composition root だけが読む。設計書 §4.4）
    /// Keeps the current screen via subscriptions; it does not know which system draws (only the composition root reads the global switch; design §4.4)
    /// </summary>
    public sealed class UiRoutingState : IInitializable
    {
        public IReadOnlyReactiveProperty<UiRoutingSnapshot> Current => _current;

        private readonly UIStateControl _uiStateControl;
        private readonly IReadOnlyList<INestedPauseScreenState> _nestedPauseScreens;
        private readonly ReactiveProperty<UiRoutingSnapshot> _current;

        public UiRoutingState(UIStateControl uiStateControl, IReadOnlyList<INestedPauseScreenState> nestedPauseScreens)
        {
            _uiStateControl = uiStateControl;
            _nestedPauseScreens = nestedPauseScreens;
            _current = new ReactiveProperty<UiRoutingSnapshot>(Resolve());
        }

        public void Initialize()
        {
            // state 遷移と入れ子画面の表示変化で解決し直す。同値は ReactiveProperty が落とす
            // Re-resolve on state transitions and nested-screen presentation changes; ReactiveProperty drops equal values
            _uiStateControl.OnStateChanged.Subscribe(_ => Refresh());
            foreach (var nested in _nestedPauseScreens) nested.OnPresentationChanged.Subscribe(_ => Refresh());
            Refresh();
        }

        private void Refresh()
        {
            _current.Value = Resolve();
        }

        private UiRoutingSnapshot Resolve()
        {
            var subState = _uiStateControl.GetCurrentNestedPauseScreen()?.SubState;
            return new UiRoutingSnapshot(UiScreenRouting.Resolve(_uiStateControl.CurrentState, subState));
        }
    }

    /// <summary>
    /// 画面ルーティングの不変スナップショット
    /// Immutable snapshot of screen routing
    /// </summary>
    public sealed class UiRoutingSnapshot
    {
        public UiScreen Screen { get; }

        public UiRoutingSnapshot(UiScreen screen)
        {
            Screen = screen;
        }

        public override bool Equals(object obj)
        {
            return obj is UiRoutingSnapshot other && Screen == other.Screen;
        }

        public override int GetHashCode()
        {
            return (int)Screen;
        }

        public override string ToString()
        {
            return Screen.ToString();
        }
    }
}
```

- [ ] **Step 2: Presenter と IME の適用を直す**

`PauseMenuScreenPresenter.cs` 旧テキスト（:63-68）:
```csharp
        // ポーズを持つ画面の列挙は traits 表に任せ、主パネルが GameUi の持ち物のときだけ出す（D-20）
        // The traits table enumerates pause screens; show only when GameUi owns the main panel (D-20)
        private bool IsShownOn(UiRoutingSnapshot snapshot)
        {
            return UiScreenTraitsTable.Get(snapshot.Screen).PauseMenu && snapshot.MainPanelOwner == UiSurfaceOwner.GameUi;
        }
```
新テキスト:
```csharp
        // ポーズを持つ画面の列挙は traits 表に任せる。どちらの系で描くかは composition root が登録の有無で決める（設計書 C10・[A-16]）
        // The traits table enumerates pause screens; which system draws is decided by the composition root through registration (design C10, [A-16])
        private static bool IsShownOn(UiRoutingSnapshot snapshot)
        {
            return UiScreenTraitsTable.Get(snapshot.Screen).PauseMenu;
        }
```

`ImeCompositionModeApplier.cs` 旧テキスト（:9-12）:
```csharp
    /// <summary>
    /// IME モードを「どこかの入力欄にフォーカスがあれば On、無ければ Off」に 1 か所で決める
    /// Decides the IME mode in one place: On while any text field (GameUi or Web) has focus, Off otherwise
    /// </summary>
```
新テキスト:
```csharp
    /// <summary>
    /// IME モードを「GameUi の入力欄にフォーカスがあれば On、無ければ Off」に 1 か所で決める。全体スイッチの GameUi の起動でだけ登録される
    /// Decides the IME mode in one place: On while a GameUi text field has focus, Off otherwise; registered only on a GameUi boot of the global switch
    /// </summary>
```
旧テキスト（:23-24）:
```csharp
            // Web の値は WS スレッドから来るのでメインスレッドで適用する。TextField のフォーカスアウトで Auto に戻る処理より後に効く
            // Web values arrive on the WS thread, so apply on the main thread; this also lands after TextField's focus-out reset to Auto
```
新テキスト:
```csharp
            // メインスレッドで適用し、TextField のフォーカスアウトで Auto に戻る処理より後に効かせる
            // Apply on the main thread so it lands after TextField's focus-out reset to Auto
```

- [ ] **Step 3: composition root を分岐の 1 か所にする**

`MainGameUiRegistration.cs` を全置換:
```csharp
using Client.Input;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.PauseMenu;
using Client.Ui.Screens.Toast;
using Client.Ui.State.PauseMenu;
using Client.Ui.State.Toast;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration
{
    // Client.Ui の登録口。全体スイッチの分岐はここ 1 か所で、Presenter・View・部品はスイッチを読まない（plan §3.3 R1、設計書 §4.4・[A-16]）
    // Registration entry for Client.Ui; the global switch branches only here, and presenters, views and parts never read it (plan §3.3 R1, design §4.4, [A-16])
    // 後続レーンは画面の Presenter を GameUi 分岐の下に 1 行ずつ足す
    // Later lanes append their screen presenters below, inside the GameUi branch
    internal static class MainGameUiRegistration
    {
        public static void Register(ContainerBuilder builder, GameUiRoot gameUiRoot, UiSystem uiSystem)
        {
            // Web の系では Client.Ui を 1 つも登録せず、GameUiRoot の層は空のまま残る。起動の系は UiSystemBootSelection がログに出している
            // In the Web system nothing from Client.Ui is registered and the GameUiRoot layers stay empty; UiSystemBootSelection has logged the boot's system
            if (uiSystem != UiSystem.GameUi) return;

            builder.RegisterComponent(gameUiRoot);
            builder.RegisterEntryPoint<GameUiLocaleClassBinder>();
            builder.RegisterEntryPoint<GameUiInputInstaller>();
            builder.RegisterEntryPoint<ImeCompositionModeApplier>();
            builder.Register<GameUiToastFeed>(Lifetime.Singleton);
            builder.RegisterEntryPoint<ToastLayerPresenter>();

            // ポーズ画面。書きかけはページの作り直しを越えて残すので Presenter の外に置く
            // The pause screen; the draft lives outside the presenter so it survives page rebuilds
            builder.Register<BugReportDraft>(Lifetime.Singleton);
            builder.RegisterEntryPoint<PauseMenuScreenPresenter>();
        }
    }
}
```

`MainGameUiStateRegistration.cs`: using に `using Client.Ui.State.PauseMenu;` と `using Client.Ui.State.Settings;` を足し、旧テキスト（:49-52）:
```csharp
            // 画面ルータと所有表。Web ホストと描画側の両方が読む
            // Screen router and ownership table, read by both the web host and the renderer
            builder.Register<UiOwnershipTable>(Lifetime.Singleton);
            builder.RegisterEntryPoint<UiRoutingState>().AsSelf();
```
新テキスト:
```csharp
            // 画面ルータ。どちらの系でも同じ画面を引く（描く系の分岐は MainGameUiRegistration だけ）
            // Screen router; both systems resolve the same screen (the drawing-system branch lives only in MainGameUiRegistration)
            builder.RegisterEntryPoint<UiRoutingState>().AsSelf();

            // ポーズと設定の状態と型付きコマンド。Web のアダプタと GameUi の View が同じものを使うので両方の系で登録する
            // Pause and settings state and typed commands; Web adapters and GameUi views share them, so both systems register them
            builder.Register<PauseMenuCommands>(Lifetime.Singleton).As<IPauseMenuCommands>();
            builder.Register<SettingsCommands>(Lifetime.Singleton).As<ISettingsCommands>();
            builder.RegisterEntryPoint<PauseMenuModel>().AsSelf();
            builder.RegisterEntryPoint<SettingsModel>().AsSelf();
```

`MainGameModelRegistration.cs`: using に `using Client.WebUiHost.Boot;` を足し、`builder.RegisterEntryPoint<TrainTimetableEventHandler>();` の次に足す:
```csharp

            // Web の無応答の評価。W0-cef が master に置く場所と同じにし、取り込みで二重登録を作らない（cef-record §6）
            // Web silence evaluation; kept where W0-cef places it on master so a later merge does not register it twice (cef-record §6)
            builder.RegisterEntryPoint<WebUiSilenceWatcher>();
```
（141 → 145 行。GameUi の系では `EvaluateWebSilence` が系の印で無応答を判定しないので、Web から受信が無くても無応答にならない）

`MainGameStarter.cs`: using に `using Client.Input;` を足し、旧テキスト（:111）`MainGameUiRegistration.Register(builder, gameUiRoot);` を次へ:
```csharp
            MainGameUiRegistration.Register(builder, gameUiRoot, UiInputExclusivity.ActiveUiSystem);
```

- [ ] **Step 4: Web ホスト側の休止配信を master 版へ戻す**

Run:
```bash
source "${TMPDIR:-/tmp}/uitk-w0-switch/start.env"
git checkout "$MERGED_MASTER" -- \
  moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/UiStateTopic.cs \
  moorestech_client/Assets/Scripts/Client.Tests/WebUi/UiStateSubStatePublishTest.cs \
  moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractTest.cs \
  moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/ui_state.json
```
`WebUiGameBinder.cs` 旧テキスト（:96）:
```csharp
            var uiStateTopic = new UiStateTopic(hub, uiStateControl, uiStateDictionary, nestedPauseScreens, resolver.Resolve<Client.Ui.State.Routing.UiRoutingState>());
```
新テキスト:
```csharp
            var uiStateTopic = new UiStateTopic(hub, uiStateControl, uiStateDictionary, nestedPauseScreens);
```

- [ ] **Step 5: L3 の土台と Web 側の L3 を作る**

`UiSystemPlayModeTestBase.cs`:
```csharp
using Client.Input;
using Client.Starter.Initialization.Boot;
using NUnit.Framework;

namespace Client.Tests.EditModeInPlayingTest.Ui
{
    /// <summary>
    /// 全体スイッチを選んで PlayMode に入る L3 の土台。テストの後は成否によらず既定の Web へ戻す（plan §3.3 R4）
    /// Base for L3 tests that pick the global switch before entering play mode; afterwards it restores the default Web whatever the outcome (plan §3.3 R4)
    /// </summary>
    public abstract class UiSystemPlayModeTestBase
    {
        // EnterPlayMode より前に呼ぶ。値は SessionState にありドメインリロードを越えて次の起動に効く
        // Call before EnterPlayMode; the value lives in SessionState and reaches the next boot across the domain reload
        protected static void SelectUiSystemBeforePlayMode(UiSystem system)
        {
            UiSystemEditorSwitch.Set(system);
        }

        [TearDown]
        public void RestoreDefaultUiSystem()
        {
            UiSystemEditorSwitch.Set(UiSystem.Web);
        }
    }
}
```

`WebUiSystemSkeletonTest.cs`:
```csharp
using System;
using System.Collections;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Ui
{
    /// <summary>
    /// 全体スイッチの Web 側で、Client.Ui が何も載らず GameUiRoot の層が空のままかを実ゲームで確かめる（plan §2.2 完了条件）
    /// Verifies in the real game that on the Web side of the global switch nothing from Client.Ui is mounted and the GameUiRoot layers stay empty (plan §2.2)
    /// </summary>
    [Category("CiShardClientPlay1")]
    public class WebUiSystemSkeletonTest : UiSystemPlayModeTestBase
    {
        [UnityTest]
        public IEnumerator Web側ではGameUiRootの層が空のままでポーズもUITKに載らない()
        {
            SelectUiSystemBeforePlayMode(UiSystem.Web);
            EnterPlayModeUtil();

            // yield return new EnterPlayMode は必ず[UnityTest]関数の直下で呼び出すこと
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;

            yield return TestBody().ToCoroutine();

            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask TestBody()
            {
                await LoadMainGame();
                await UniTask.DelayFrame(5);
                var uiStateControl = ClientDIContext.DIContainer.DIContainerResolver.Resolve<UIStateControl>();
                var gameUiRoot = GameUiRoot.Instance;

                // Web 側の起動は入力の裁きも Web で、ゲーム画面で層が空
                // A Web-side boot arbitrates input as Web, and the layers are empty on the game screen
                Assert.AreEqual(UiSystem.Web, UiInputExclusivity.ActiveUiSystem, "起動の系が Web でない");
                AssertAllLayersEmpty(gameUiRoot, "ゲーム画面");

                // ポーズにしても UITK のポーズは載らない（Web のポーズが出る）
                // Pausing mounts no UITK pause (the Web pause shows instead)
                uiStateControl.RequestTransition(UIStateEnum.PauseMenu);
                await UniTask.DelayFrame(3);
                Assert.AreEqual(UIStateEnum.PauseMenu, uiStateControl.CurrentState, "ポーズへ遷移していない");
                AssertAllLayersEmpty(gameUiRoot, "ポーズ");
                uiStateControl.RequestTransition(UIStateEnum.GameScreen);
                await UniTask.DelayFrame(3);
            }

            #endregion
        }

        private static void AssertAllLayersEmpty(GameUiRoot gameUiRoot, string phase)
        {
            // Web 側では Client.Ui を登録しないので、どの層にも子が無い（plan §3.3 R1）
            // The Web side registers nothing from Client.Ui, so no layer has children (plan §3.3 R1)
            foreach (UiLayer layer in Enum.GetValues(typeof(UiLayer)))
            {
                var count = gameUiRoot.GetLayer(layer).childCount;
                Assert.AreEqual(0, count, $"{phase}: Web 側で層 {layer} に {count} 個の要素が載っている");
            }
        }
    }
}
```

- [ ] **Step 6: `GameUiSkeletonTest.cs` を UITK 側へ全置換する**

```csharp
using System;
using System.Collections;
using Client.Game.InGame.BugReport.WebUiHealth;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.State.Routing;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Cef;
using Client.WebUiHost.Health;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Ui
{
    /// <summary>
    /// 全体スイッチの UITK 側で、骨組み（層・Web ホスト無し・空パネルの入力素通し）が実ゲームで結線されているか確かめる
    /// Verifies on the UITK side of the global switch that the skeleton (layers, no web host, empty-panel input pass-through) is wired in the real game
    /// </summary>
    [Category("CiShardClientPlay1")]
    public class GameUiSkeletonTest : UiSystemPlayModeTestBase
    {
        [UnityTest]
        public IEnumerator UITK側の骨組みが層を持ちWebホスト無しで入力素通しが実ゲームで効く()
        {
            SelectUiSystemBeforePlayMode(UiSystem.GameUi);
            EnterPlayModeUtil();

            // yield return new EnterPlayMode は必ず[UnityTest]関数の直下で呼び出すこと
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;

            yield return TestBody().ToCoroutine();

            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask TestBody()
            {
                await LoadMainGame();
                await UniTask.DelayFrame(5);

                var resolver = ClientDIContext.DIContainer.DIContainerResolver;
                var routingState = resolver.Resolve<UiRoutingState>();
                var uiStateControl = resolver.Resolve<UIStateControl>();
                var gameUiRoot = GameUiRoot.Instance;

                AssertAllLayersExist(gameUiRoot);

                // UITK 側の起動では Web ホストも CEF も無い（WS の接続 0・CEF の RawImage 無し。plan §2.2 完了条件）
                // A UITK-side boot has neither a web host nor CEF (zero WS connections, no CEF RawImage; plan §2.2)
                Assert.AreEqual(UiSystem.GameUi, UiInputExclusivity.ActiveUiSystem, "起動の系が GameUi でない");
                Assert.IsNull(WebUiHost.Hub, "UITK 側で Web ホスト（WS の受け口）が起動している");
                Assert.IsTrue(WebUiHost.HostSkippedThisBoot, "Web ホストを飛ばした起動の状態が立っていない");
                Assert.AreEqual(WebUiHostStartupState.SkippedByEditorSwitch, WebUiHealthRecorder.HostStartup, "診断記録にスイッチで飛ばしたことが残っていない");
                Assert.AreEqual(0, UnityEngine.Object.FindObjectsByType<WebUiCefNavigator>(FindObjectsSortMode.None).Length, "CEF のブラウザ（RawImage と同じ GameObject）が有効のまま");

                // ポーズへ行き来しても、ルータは UIState だけで画面を決める
                // Going to the pause and back, the router decides the screen from UIState alone
                await TransitionTo(uiStateControl, UIStateEnum.PauseMenu);
                Assert.AreEqual(UiScreen.PauseMenu, routingState.Current.Value.Screen);
                await TransitionTo(uiStateControl, UIStateEnum.GameScreen);
                Assert.AreEqual(UiScreen.None, routingState.Current.Value.Screen);

                await AssertEmptyPanelDoesNotTakePointer(gameUiRoot);
            }

            #endregion
        }

        private static void AssertAllLayersExist(GameUiRoot gameUiRoot)
        {
            // 全層のコンテナが組まれている
            // Every layer has its container built
            foreach (UiLayer layer in Enum.GetValues(typeof(UiLayer)))
            {
                var container = gameUiRoot.GetLayer(layer);
                Assert.IsNotNull(container, $"層 {layer} のコンテナが無い");
                Assert.IsNotNull(container.panel, $"層 {layer} のコンテナがパネルに載っていない");
            }
        }

        private static async UniTask TransitionTo(UIStateControl uiStateControl, UIStateEnum state)
        {
            // 遷移要求は次の Update で消費される
            // The transition request is consumed in the next Update
            uiStateControl.RequestTransition(state);
            await UniTask.DelayFrame(3);
            Assert.AreEqual(state, uiStateControl.CurrentState, $"{state} へ遷移していない");
        }

        private static async UniTask AssertEmptyPanelDoesNotTakePointer(GameUiRoot gameUiRoot)
        {
            // 背面の Editor・CI の batchmode でも注入マウスが届く設定にする
            // Let injected mouse input reach a background Editor or CI batchmode
            using var backgroundInput = EditorBackgroundInputScope.Open();

            // カーソルを解放して画面中央（uGUI の要素が無い位置）へ置く
            // Release the cursor and place it at the screen center where no uGUI element sits
            InputManager.MouseCursorVisible(true);
            var center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            var mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            for (var i = 0; i < 5; i++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = center });
                InputSystem.Update();
                await UniTask.Yield();
            }

            var empty = await MeasurePointerAtCenter(gameUiRoot, center, "empty");
            Assert.IsNull(empty.picked, $"空の UITK パネルが {empty.picked?.name} を Pick した");
            Assert.IsFalse(empty.isOverGameObject, "空の UITK パネル越しに IsPointerOverGameObject が true");
            Assert.IsFalse(empty.isOverAnyUi, "空の UITK パネル越しに IsPointerOverAnyUi が true");

            // 対照: 拾える要素を置けば同じ判定が true になる（上の false が測定の空振りでない証拠。片側読みの GameUi 側の実配線）
            // Control: a pickable element flips the same checks to true (proof the false above is real, and the live wiring of the one-sided GameUi read)
            var blocker = new VisualElement { name = "skeleton-test-blocker", pickingMode = PickingMode.Position };
            blocker.style.position = Position.Absolute;
            blocker.style.left = 0;
            blocker.style.top = 0;
            blocker.style.right = 0;
            blocker.style.bottom = 0;
            gameUiRoot.GetLayer(UiLayer.Hud).Add(blocker);
            var covered = await MeasurePointerAtCenter(gameUiRoot, center, "covered");
            blocker.RemoveFromHierarchy();
            Assert.AreSame(blocker, covered.picked, "対照の要素が Pick されない");
            Assert.IsTrue(covered.isOverGameObject, "対照の要素の上で IsPointerOverGameObject が false（測定が効いていない）");
            Assert.IsTrue(covered.isOverAnyUi, "対照の要素の上で IsPointerOverAnyUi が false（GameUi の系の Pick が読まれていない）");
        }

        private static async UniTask<(VisualElement picked, bool isOverGameObject, bool isOverAnyUi)> MeasurePointerAtCenter(GameUiRoot gameUiRoot, Vector2 center, string label)
        {
            // EventSystem が新しい木で当たり直すまで数フレーム待つ
            // Wait a few frames so the EventSystem re-raycasts against the current tree
            await UniTask.DelayFrame(3);
            var panel = gameUiRoot.GetLayer(UiLayer.Backdrop).panel;
            var panelPosition = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(center.x, Screen.height - center.y));
            var picked = panel.Pick(panelPosition);
            var isOverGameObject = EventSystem.current.IsPointerOverGameObject();
            var isOverAnyUi = UiPointerHitTest.IsPointerOverAnyUi();
            Debug.Log($"[GameUiSkeletonTest] {label} center={center} panelPos={panelPosition} picked={(picked == null ? "null" : picked.name + "/" + picked.GetType().Name)} overGameObject={isOverGameObject} overAnyUi={isOverAnyUi} lock={UnityEngine.Cursor.lockState} system={UiInputExclusivity.ActiveUiSystem}");
            return (picked, isOverGameObject, isOverAnyUi);
        }
    }
}
```

- [ ] **Step 7: `PauseMenuGameUiTest.cs` を UITK 側へ全置換する**

```csharp
using System.Collections;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Input;
using Client.Playtest.Input;
using Client.Playtest.UiElements;
using Client.Playtest.UiElements.GameUi;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.State.Routing;
using Client.WebUiHost.Boot;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Ui
{
    /// <summary>
    /// 全体スイッチの UITK 側の実ゲームで、Esc から UITK のポーズが載り、設定へ行き来して閉じられ、Web の値と Web のロケータを読まないかを確かめる（R-7 の L3）
    /// Verifies on the UITK side that Esc mounts the UITK pause, settings round-trips and closes, and neither web values nor the web locator are read (L3 of R-7)
    /// </summary>
    [Category("CiShardClientPlay1")]
    public class PauseMenuGameUiTest : UiSystemPlayModeTestBase
    {
        [UnityTest]
        public IEnumerator UITK側でEscからポーズが出て設定へ行き来して閉じられWebの値を読まない()
        {
            SelectUiSystemBeforePlayMode(UiSystem.GameUi);
            EnterPlayModeUtil();

            // yield return new EnterPlayMode は必ず[UnityTest]関数の直下で呼び出すこと
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;

            yield return TestBody().ToCoroutine();

            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask TestBody()
            {
                await LoadMainGame();
                await UniTask.DelayFrame(5);
                var resolver = ClientDIContext.DIContainer.DIContainerResolver;
                var uiStateControl = resolver.Resolve<UIStateControl>();
                var routingState = resolver.Resolve<UiRoutingState>();
                var pauseMenuState = resolver.Resolve<PauseMenuStateService>();
                using var backgroundInput = EditorBackgroundInputScope.Open();

                // UITK 側では Web ホストが無く、入力の裁きは GameUi だけを読む
                // The UITK side has no web host, and input arbitration reads GameUi only
                Assert.AreEqual(UiSystem.GameUi, UiInputExclusivity.ActiveUiSystem, "起動の系が GameUi でない");
                Assert.IsNull(WebUiHost.Hub, "UITK 側で Web ホストが起動している");

                // ゲーム画面で Esc を押すと UITK のポーズが出る
                // Pressing Esc on the game screen shows the UITK pause
                await PressEscape();
                Assert.AreEqual(UIStateEnum.PauseMenu, uiStateControl.CurrentState, "Esc でポーズへ遷移しない");
                Assert.AreEqual(UiScreen.PauseMenu, routingState.Current.Value.Screen, "ルータの画面がポーズでない");
                Assert.IsNotNull(GameUiRoot.Instance.GetLayer(UiLayer.PauseMenu).Q("pause-menu"), "UiLayer.PauseMenu に pause-menu が無い");
                Assert.IsTrue(Find("pause-menu").Visible, "UITK のポーズが見えない");

                // Web 側の値が届いても文字入力にもポインタ占有にもならない（R2）
                // Web values never become text-input focus or pointer occupancy (R2)
                UiInputExclusivity.SetState(true, true);
                Assert.IsFalse(UiInputExclusivity.IsTextInputFocused, "UITK 側で Web の入力欄フォーカスが通った");
                Assert.IsFalse(UiInputExclusivity.IsPointerOverWebUi, "UITK 側で Web のポインタ占有が通った");
                UiInputExclusivity.SetState(false, false);

                // DSL のロケータは GameUi だけで探す（R2・plan §4.4）
                // The DSL locator looks up with GameUi only (R2, plan §4.4)
                await AssertDslLocatorReadsOnlyGameUi();

                // 設定ボタンをクリックすると設定のページになる
                // Clicking the settings button opens the settings page
                await ClickByTestid("pause-menu-open-settings");
                Assert.AreEqual(PauseMenuPage.Settings, pauseMenuState.CurrentPage.Value, "設定ボタンのクリックで設定のページへ行かない");
                Assert.IsTrue(Find("language-select").Visible, "設定のページに言語選択が見えない");

                // Esc 1 回目はトップへ戻り、2 回目でゲーム画面へ閉じる
                // The first Esc returns to the top and the second closes to the game screen
                await PressEscape();
                Assert.AreEqual(UIStateEnum.PauseMenu, uiStateControl.CurrentState, "設定のページの Esc でポーズが閉じた");
                Assert.AreEqual(PauseMenuPage.Top, pauseMenuState.CurrentPage.Value, "設定のページの Esc でトップへ戻らない");
                await PressEscape();
                Assert.AreEqual(UIStateEnum.GameScreen, uiStateControl.CurrentState, "トップの Esc でゲーム画面へ戻らない");
                var closedRoot = GameUiRoot.Instance.GetRootElement();
                Assert.AreEqual(UiElementLookupStatus.NotFound, GameUiElementQuery.Find(closedRoot, "pause-menu", new GameUiScreenMapper(closedRoot)).Status, "閉じた後も UITK のポーズが見えている");
                Assert.AreEqual(UiScreen.None, routingState.Current.Value.Screen, "閉じた後もルータの画面がポーズのまま");
            }

            #endregion
        }

        private static async UniTask AssertDslLocatorReadsOnlyGameUi()
        {
            // try/finally はセッションを必ず閉じるためで、例外は握らない
            // try/finally only guarantees the session closes; no exception is swallowed
            // 選んだ系は ctor のログで確かめる（ロケータの具象型へキャストしない）
            // The chosen system is confirmed by the ctor log (no cast to the concrete locator type)
            LogAssert.Expect(LogType.Log, "[PlaytestUi] locator reads only the GameUi UI");
            PlaytestUiLocatorSession.Start();
            try
            {
                Assert.AreEqual(UiSystem.GameUi, UiInputExclusivity.ActiveUiSystem, "起動の系が GameUi でない");
                var snapshot = await PlaytestUiLocatorSession.Locator.Find("pause-menu-open-settings", 5f);
                Assert.AreEqual(UiElementLookupStatus.Found, snapshot.Status, "DSL のロケータで設定ボタンが見つからない");
            }
            finally
            {
                PlaytestUiLocatorSession.Stop();
            }
        }

        private static async UniTask PressEscape()
        {
            await SemanticInput.TapKey(Key.Escape);
            await UniTask.DelayFrame(3);
        }

        private static UiElementSnapshot Find(string testid)
        {
            var root = GameUiRoot.Instance.GetRootElement();
            var snapshot = GameUiElementQuery.Find(root, testid, new GameUiScreenMapper(root));
            Debug.Log($"[PauseMenuGameUiTest] {testid} status={snapshot.Status} visible={snapshot.Visible} clickable={snapshot.Clickable} rect={snapshot.ScreenRect}");
            Assert.AreEqual(UiElementLookupStatus.Found, snapshot.Status, $"{testid} が 1 つだけ見つからない");
            return snapshot;
        }

        private static async UniTask ClickByTestid(string testid)
        {
            // ロケータのスクリーン矩形の中心へ動かし、数フレーム当たり直させてから押す
            // Move to the locator's screen-rect center, let it re-raycast for a few frames, then press
            var snapshot = Find(testid);
            Assert.IsTrue(snapshot.Clickable, $"{testid} がクリックできない");
            for (var i = 0; i < 5; i++)
            {
                SemanticInput.MouseMoveTo(snapshot.ScreenRect.center);
                await UniTask.Yield();
            }
            await SemanticInput.Click();
            await UniTask.DelayFrame(3);
        }
    }
}
```

- [ ] **Step 8: `GameUiInputTest.cs` と `GameUiTextFieldWiringChecks.cs` を UITK 側へ直す**

`GameUiInputTest.cs`: (a) `public class GameUiInputTest` を `public class GameUiInputTest : UiSystemPlayModeTestBase` にする。(b) 2 つのテストの `            EnterPlayModeUtil();`（2 か所。Edit の replace_all）を次へ:
```csharp
            // 全体スイッチを UITK にして起動する（plan §3.3 R4）
            // Boot with the global switch on UITK (plan §3.3 R4)
            SelectUiSystemBeforePlayMode(UiSystem.GameUi);
            EnterPlayModeUtil();
```
(c) `await GameUiTextFieldWiringChecks.WebTextInputTurnsImeOn();` を `await GameUiTextFieldWiringChecks.WebTextInputIsIgnoredInGameUiSystem();` へ。186 → 193 行。

`GameUiTextFieldWiringChecks.cs`: メソッド `WebTextInputTurnsImeOn`（:93-106）を次へ置き換える:
```csharp
        public static async UniTask WebTextInputIsIgnoredInGameUiSystem()
        {
            // (d) GameUi の系では Web の値（SetState）を読まず、文字入力にもポインタ占有にもならず IME も Off のまま（R2）
            // (d) In the GameUi system web values (SetState) are not read: no text-input focus, no pointer claim, and IME stays Off (R2)
            UiInputExclusivity.SetState(true, true);
            await UniTask.DelayFrame(ApplyWaitFrames);
            LogState("web values written in the GameUi system");
            var focusedWhileWebWritten = UiInputExclusivity.IsTextInputFocused;
            var pointerWhileWebWritten = UiInputExclusivity.IsPointerOverWebUi;
            var modeWhileWebWritten = UnityEngine.Input.imeCompositionMode;
            UiInputExclusivity.SetState(false, false);
            Assert.IsFalse(focusedWhileWebWritten, "GameUi の系で Web の入力欄フォーカスが文字入力になった");
            Assert.IsFalse(pointerWhileWebWritten, "GameUi の系で Web のポインタ占有が通った");
            Assert.AreEqual(IMECompositionMode.Off, modeWhileWebWritten, "GameUi の系で Web の入力欄フォーカスにより IME が Off でない");
        }
```

- [ ] **Step 9: コンパイルし、L0〜L3 を回す**

Run:
```bash
git grep -n -E "MainPanelOwner|WebUiSuspended|UiSurface|UiOwnership|SetCefVisible|SetWebUiSuspended|IsWebUiSuspended|web_ui_suspended|WebUiSuspension" -- moorestech_client
git grep -n "UiSystem\." -- moorestech_client/Assets/Scripts/Client.Ui moorestech_client/Assets/Scripts/Client.Ui.State
grep -c "Client.WebUiHost" moorestech_client/Assets/Scripts/Client.Ui/Client.Ui.asmdef moorestech_client/Assets/Scripts/Client.Ui.State/Client.Ui.State.asmdef
wc -l moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/*.cs moorestech_client/Assets/Scripts/Client.Starter/Registration/*.cs
ls moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/*.cs | wc -l
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.(Ui|WebUi|WebUiHost)\." --timeout-seconds 1500
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.EditModeInPlayingTest\.Ui\." --timeout-seconds 1500
```
Expected: 最初の 2 つの grep は 0 件。asmdef の grep は両方 `0`（UITK 側に WS の経路が無い証拠。Task 5 の台帳に書く）。全ファイル 200 行未満、`EditModeInPlayingTest/Ui/` の .cs は 10。ErrorCount 0。L0〜L2 全件 PASS（ベースラインからの減りなし）。L3 は `GameUiSkeletonTest` 1・`PauseMenuGameUiTest` 1・`GameUiInputTest` 2・`WebUiSystemSkeletonTest` 1 の 5 件 PASS。L3 の後にドメインリロードのエラーが出たら 45 秒待って再試行する。Editor が「L3 の後に SessionState のスイッチが GameUi のまま」になっていないことを `uloop execute-dynamic-code --project-path ./moorestech_client --code 'return Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Current.ToString();'` が `Web` を返すことで確かめる

- [ ] **Step 10: コミットする**

```bash
git add -A moorestech_client/Assets/Scripts
git commit -m "feat(uitk): 所有表とルータの休止判断を消し、Client.Ui の登録の分岐を composition root の1か所にする。L3 を全体スイッチの UITK 側と Web 側で書き直す（albof.17・C10・A-16）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Web 側を master と同じへ戻し、台帳へ記帳する

**Files:**
- Modify（master 版へ）: `moorestech_web/`（`moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts` を除く）
- Modify: `docs/uitk/TODO.md`
- Create（コミットしない）: `${TMPDIR:-/tmp}/uitk-w0-switch/ledger_w0_switch.py`

**Interfaces:**
- Consumes: `start.env` の `START_SHA`・`MERGED_MASTER`（Task 1）。Task 4 Step 9 の asmdef の grep 結果（`0`・`0`）
- Produces: 撤去コミット `REVERT_SHA`（10 桁）と台帳コミット。G1 の未記帳 0

- [ ] **Step 1: Web を取り込み済み master と同じに戻す**

Run:
```bash
source "${TMPDIR:-/tmp}/uitk-w0-switch/start.env"
git diff --name-status "$MERGED_MASTER" HEAD -- moorestech_web
git restore --source="$MERGED_MASTER" --staged --worktree -- moorestech_web
git restore --source=HEAD --staged --worktree -- moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts
git diff --cached --name-status "$MERGED_MASTER" -- moorestech_web
git status --short moorestech_web | head -30
```
Expected: 1 つ目の diff に旧 R1 の約 20 ファイル（`src/app/App.tsx`・`App.webUiSuspended.test.ts`・`bridge/contract/schemas/ui.ts`・`shared/uiState/inputExclusivity/*`・`uiScreenRouting.ts`・`e2e/mock-host/*` 等）と `localizationKeys.ts`。戻した後の `git diff --cached --name-status "$MERGED_MASTER"` は **`M	moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts` の 1 行だけ**。それ以外が出たら（取り込みで入った master の Web 変更が消えている、または旧 R1 以外の feature 側の Web 変更がある）コミットせず止め、`git restore --staged --worktree moorestech_web` で戻して指揮へ報告

- [ ] **Step 2: 撤去をコミットし、SHA を控える**

```bash
git commit -m "revert(uitk): 旧 R1 の Web 側変更（a97575e5b5・05b9bdefa8・1095cab8bf・ccb3d512f7・5421c7f9f0 の Web 部）を外し、取り込み済み master と同じ Web に戻す（albof.17・plan §2.2）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
echo "REVERT_SHA=$(git rev-parse --short=10 HEAD)" >> "${TMPDIR:-/tmp}/uitk-w0-switch/start.env"
tail -1 "${TMPDIR:-/tmp}/uitk-w0-switch/start.env"
```

- [ ] **Step 3: 台帳を書き換えるスクリプトを書いて回す**

`${TMPDIR:-/tmp}/uitk-w0-switch/ledger_w0_switch.py`:
```python
import sys
from pathlib import Path

# 台帳の 4 か所を決定的に書き換える使い捨てスクリプト。前提が崩れたら書き換えずに理由を出して止める
# Throwaway script that rewrites four places in the ledger deterministically; on a broken premise it stops with the reason and writes nothing
LEDGER = Path("docs/uitk/TODO.md")
DROPPED = {
    "a97575e5b5": "旧 R1 の Web 全面 unmount（UITK が主パネルを出す間 webUiSuspended で App を外す）。全体スイッチ（albof.17）で UITK 側は Web ホストを起動しなくなり休止の仕組みごと要らないので、W0-switch で外して master と同じ Web に戻した",
    "1095cab8bf": "旧 R1 の休止解除時の入力状態の送り直し。全体スイッチで休止が無くなり要らないので、W0-switch で外した",
    "ccb3d512f7": "旧 R1 の休止解除後に DOM 変化ごとにポインタを当たり判定し直す処理。全体スイッチで休止が無くなり要らないので、W0-switch で外した",
}
FAILURE_TOAST_ROW = (
    "| 通信失敗のトースト | 全体スイッチの UITK 側では Web ホストも WS も起動しないので、Web の通信失敗のトーストは出ない。"
    "UITK のコマンドは C# を直接呼び、拒否は GameUiToastFeed.ShowRejected が拒否理由ごとのローカライズ文言で出す。Web 側は master と同じ Web の toast がそのまま出す"
    " | W0-switch で確かめた: Client.Ui と Client.Ui.State の asmdef が Client.WebUiHost を参照しない（grep 0 件）、L3 PauseMenuGameUiTest と GameUiSkeletonTest で UITK 側の WebUiHost.Hub が null |"
)
SWITCH_RULE = (
    "- 全体スイッチ（計画 §3.3 R1〜R4）で描く系を選ぶ: PlayMode を止めた状態で Unity のメニュー `moorestech/UI System/GameUi (UITK)`（戻すときは `moorestech/UI System/Web`）を選ぶか、"
    "`uloop execute-dynamic-code --project-path ./moorestech_client --code 'Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi);'` を実行してから PlayMode に入る。"
    "値は起動ごとに 1 回だけ読まれ、Editor を閉じると既定の Web に戻る。GameUi の起動では Web ホストと CEF を起動せず、UITK にまだ無い画面は使えない（裁定）"
)


def fail(reason):
    print(f"ledger_w0_switch: {reason}", file=sys.stderr)
    sys.exit(1)


def main():
    if len(sys.argv) != 2 or len(sys.argv[1]) < 10:
        fail("撤去コミットの SHA（10 桁以上）を 1 つ渡す")
    revert_sha = sys.argv[1]
    lines = LEDGER.read_text(encoding="utf-8").split("\n")

    # 旧 R1 の 3 行を「写した」から「捨てた」へ
    # Turn the three old-R1 rows from "copied" into "dropped"
    for sha, reason in DROPPED.items():
        hits = [i for i, l in enumerate(lines) if l.startswith(f"| 1 | {sha} | ")]
        if len(hits) != 1:
            fail(f"追従記録の {sha} の行が 1 つでない（{len(hits)}）")
        lines[hits[0]] = f"| 1 | {sha} | 捨てた | {reason} |"

    # 撤去コミットを束 1 の最後の追従記録の後へ
    # Append the removal commit after bundle 1's last follow row
    follow = [i for i, l in enumerate(lines) if l.startswith("| 1 | ") and (" | 写した | " in l or " | 捨てた | " in l)]
    if not follow or any(l.startswith(f"| 1 | {revert_sha} ") for l in lines):
        fail("束 1 の追従記録が無いか、撤去コミットが既に記帳されている")
    lines.insert(follow[-1] + 1, f"| 1 | {revert_sha} | 捨てた | W0-switch（albof.17）の撤去コミット。旧 R1 の Web 側変更（a97575e5b5・05b9bdefa8・1095cab8bf・ccb3d512f7・5421c7f9f0）を外し、取り込み済み master と同じ Web に戻した（生成物 localizationKeys.ts は 3e946e4bd3 のまま）。UITK 側に写すものが無い |")

    # 通信失敗のトーストの行を結論で書き換える
    # Rewrite the failure-toast row with the conclusion
    toast = [i for i, l in enumerate(lines) if l.startswith("| 通信失敗のトースト |")]
    if len(toast) != 1:
        fail(f"「通信失敗のトースト」の行が 1 つでない（{len(toast)}）")
    lines[toast[0]] = FAILURE_TOAST_ROW

    # R4 手順（所有表の Editor 上書き）を全体スイッチの手順へ。無ければ「セルに」の前へ足す
    # Replace the R4 procedure (ownership override) with the switch procedure, or add it before the "cells" rule
    r4 = [i for i, l in enumerate(lines) if "UiOwnershipEditorOverride" in l]
    if len(r4) > 1:
        fail("UiOwnershipEditorOverride を含む行が 2 つ以上ある")
    if r4:
        lines[r4[0]] = SWITCH_RULE
    else:
        cells = [i for i, l in enumerate(lines) if l.startswith("- セルに `|` を書かない")]
        if len(cells) != 1:
            fail("「読み方と運用」の『セルに』の行が見つからない")
        lines.insert(cells[0], SWITCH_RULE)

    LEDGER.write_text("\n".join(lines), encoding="utf-8")
    print("ledger_w0_switch: OK")


main()
```
Run:
```bash
source "${TMPDIR:-/tmp}/uitk-w0-switch/start.env"
python3 "${TMPDIR:-/tmp}/uitk-w0-switch/ledger_w0_switch.py" "$REVERT_SHA"
grep -n -E "^\| 1 \| (a97575e5b5|05b9bdefa8|1095cab8bf|ccb3d512f7|5421c7f9f0|3e946e4bd3|$REVERT_SHA) " docs/uitk/TODO.md | cut -c1-48
grep -c "UiOwnershipEditorOverride" docs/uitk/TODO.md
grep -n "^| 通信失敗のトースト" docs/uitk/TODO.md | cut -c1-60
```
Expected: `ledger_w0_switch: OK`。束 1 の追従記録 7 行（`a97575e5b5 | 捨てた`・`05b9bdefa8 | 捨てた`・`1095cab8bf | 捨てた`・`ccb3d512f7 | 捨てた`・`3e946e4bd3 | 写した`・`5421c7f9f0 | 捨てた`・`<REVERT_SHA> | 捨てた`）。`UiOwnershipEditorOverride` は `0`。通信失敗の行が「全体スイッチの UITK 側では…」で始まる

- [ ] **Step 4: G1 とその単体テストを回す**

Run:
```bash
source "${TMPDIR:-/tmp}/uitk-w0-switch/start.env"
python3 .github/scripts/check-uitk-web-follow.py --base "$START_SHA" --head HEAD; echo "rc=$?"
python3 .github/scripts/check-uitk-web-follow.py --base 979725bcd1 --head HEAD; echo "rc=$?"
for f in .github/scripts/tests/test_uitk_web_follow_*.py; do
  n=$(basename "$f"); printf '%s: ' "$n"
  python3 -m unittest discover -s .github/scripts/tests -p "$n" 2>&1 | grep -E '^(Ran|OK|FAILED)' | tr '\n' ' '; echo
done
```
Expected: 2 回とも `G1: 束 1（暗転・起点 979725bcd1）: 未記帳 0 件` と `G1: 違反なし`、`rc=0`。単体テスト 5 本が全部 `OK`（件数は W0-merge の実測 11/18/10/27/4。台帳の行が増えても real_ledger の件数は変わらない）。未記帳が出たら、出たコミットを `git show --stat <sha> -- moorestech_web` で見て、旧 R1 の撤去なら Step 3 の `DROPPED` に足して回し直し、それ以外なら止めて指揮へ報告

- [ ] **Step 5: コミットする**

```bash
git add docs/uitk/TODO.md
git commit -m "docs(uitk): 旧 R1 の Web コミットと撤去コミットを捨てたで記帳し、通信失敗のトーストの行を結論で書き、R4 手順を全体スイッチの手順に置き換える（albof.17）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: unityプレイ録画テスト（UITK 側 3 本・Web 側 1 本）

**Files:**
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage1/uitk-lifeline.cs:3-4`
- Delete: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-pause-web-action-count.cs`

**Interfaces:**
- Consumes: `UiSystemEditorSwitch.Set`（Task 2）、Task 4 までの全変更
- Produces: 4 本の `result.json` の合否と録画のパス（`done.md`・Task 10 の報告ページ）

- [ ] **Step 1: シナリオの冒頭コメントを直し、役目を終えたシナリオを消す**

`uitk-lifeline.cs` 旧テキスト（:3-4）:
```csharp
// 実行前にPlayModeを止め、execute-dynamic-codeで WebUiHostEditorSkip.Apply() を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and set WebUiHostEditorSkip.Apply() via execute-dynamic-code before calling run-scenario.sh
```
新テキスト:
```csharp
// 実行前にPlayModeを止め、execute-dynamic-codeで UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ（全体スイッチの UITK 側。Web ホストを起動しない）
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh (UITK side of the global switch; no web host)
```
Run:
```bash
git rm .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-pause-web-action-count.cs
```

- [ ] **Step 2: スイッチを UITK にし、受け口を差し替えて UITK 側の 3 本を回す**

Run（各コマンドは前景。録画は Bash の timeout 600000ms）:
```bash
SKILL=.agents/skills/unity-playmode-recorded-playtest
uloop control-play-mode --project-path ./moorestech_client --action Stop
uloop execute-dynamic-code --project-path ./moorestech_client --code 'Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi); System.Environment.SetEnvironmentVariable("MOORESTECH_PLAYTEST_RECEIVER_BASE", "http://127.0.0.1:9"); return Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Current.ToString();'
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/uitk/stage1/uitk-lifeline.cs"
uloop get-logs --project-path ./moorestech_client --search-text "[PlaytestUi] locator reads only" --max-count 5
uloop get-logs --project-path ./moorestech_client --search-text "[UiSystem] this boot draws with" --max-count 5
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/uitk/stage1/uitk-nested-pause-skit.cs"
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/uitk/stage1/uitk-nested-pause-train.cs"
```
Expected: EDC が `GameUi` を返す。3 本の `result.json` が `Success`、`Asserts` が全部 `Passed`（`uitk-lifeline` の「Webホストは起動していない（hostless）」を含む）。ログに `[PlaytestUi] locator reads only the GameUi UI` と `[UiSystem] this boot draws with GameUi` がある（UITK のロケータで解決した証拠。plan §2.0-2）。`uitk-lifeline` は最後の「セーブして終了」で PlayMode が止まるので、save.json の更新時刻を同シナリオ冒頭コメントのとおり呼び出し側のシェルで確かめる。落ちたら、原因が UITK にまだ無い画面（裁定で使えない画面）に触る手順か、この作り直しの退行かを Unity ログと `Timeline` で切り分け、退行なら直して Task 2〜4 の該当テストを回し直す。未移植画面が原因ならシナリオを弱めず `done.md` の「要裁定」に書いて指揮へ報告

- [ ] **Step 3: スイッチを Web へ戻し、Web 側で `misc/pause-menu-hierarchy` を回す**

Run:
```bash
SKILL=.agents/skills/unity-playmode-recorded-playtest
uloop execute-dynamic-code --project-path ./moorestech_client --code 'Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.Web); System.Environment.SetEnvironmentVariable("MOORESTECH_PLAYTEST_RECEIVER_BASE", null); return Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Current.ToString();'
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/misc/pause-menu-hierarchy.cs"
uloop get-logs --project-path ./moorestech_client --search-text "[PlaytestUi] locator reads only" --max-count 5
```
Expected: EDC が `Web` を返す。`result.json` が `Success`（master の想定の合否）。ログに `[PlaytestUi] locator reads only the Web UI`。落ちたら、同じシナリオを master で回して合否を比べる: `moores-wt new tmp/w0-switch-master-baseline --from origin/master` で別 worktree を作り（Editor 枠が無ければ自分の Editor を止めてから）、同じ `run-scenario.sh` を回し、結果を `done.md` に並べてから `moores-wt rm tmp/w0-switch-master-baseline`。master でも落ちるなら「master と同じ合否」として合格、master で通るならこの作り直しの退行として直す

- [ ] **Step 4: コミットする**

```bash
git add -A .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk
git commit -m "test(uitk): 録画シナリオの起動手順を全体スイッチにし、旧 R1 の実測シナリオを消す（albof.17）

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
`done.md` に 4 本の `result.json` のパス・合否・録画のパス（`RecordingPath`）・ログの行を書く（録画そのものは `../moorestech_logs` に置かない）

---

### Task 7: 回帰の一括実行と報告

**Files:**
- Create: `../moorestech_logs/harness/uitk/W0-switch/done.md`（別 repo。このブランチのコミットには入れない）

**Interfaces:**
- Consumes: Task 1 のベースライン、Task 2〜6 の結果
- Produces: `done.md`（完了条件ごとの証拠・未完了・要裁定）

- [ ] **Step 1: 回帰を回す**

Run:
```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.(Ui|WebUi|WebUiHost|Playtest|Input)\." --timeout-seconds 1500
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.(Player\.TextInputMovementLockTest|EditModeInPlayingTest\.Hostless\.)" --timeout-seconds 1500
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.EditModeInPlayingTest\.Ui\." --timeout-seconds 1500
uloop get-logs --project-path ./moorestech_client --log-type Error --max-count 50
```
Expected: ErrorCount 0。全件 PASS。Task 1 のベースラインで落ちていたものは、減っていてよいが増えていないこと。`WebUiHostlessStateAvailabilityTest` は Web の系＋スキップの印で、CEF の GameObject が非アクティブになっても通る

- [ ] **Step 2: Requirements の受け入れを機械で確かめる**

Run:
```bash
source "${TMPDIR:-/tmp}/uitk-w0-switch/start.env"
git grep -n -E "MainPanelOwner|WebUiSuspended|UiSurface|UiOwnership|SetCefVisible|SetWebUiSuspended|IsWebUiSuspended|web_ui_suspended|WebUiSuspension|CompositeUiElementLocator" -- moorestech_client .agents
git grep -n "UiSystemEditorSwitch" -- 'moorestech_client/Assets/Scripts'
git diff --name-status "$MERGED_MASTER" HEAD -- moorestech_web
git diff --stat "$START_SHA" HEAD | tail -1
```
Expected: 1 つ目 0 件。2 つ目は `UiSystemBootSelection.cs`（定義と `#if UNITY_EDITOR` の中の参照）・`UiSystemSwitchTest.cs`・`UiSystemPlayModeTestBase.cs` だけ（`WebUiStartup.cs` は `UiSystem` 引数だけでスイッチを読まない）。3 つ目は `localizationKeys.ts` の 1 行だけ

- [ ] **Step 3: 報告を書く**

`../moorestech_logs/README.md` を読んでから、`../moorestech_logs/harness/uitk/W0-switch/done.md` に、Requirements 1〜13 の各行について証拠（コマンドと出力の要点・テスト件数・result.json のパス・ログの行）、Task 1 のベースラインとの差、未完了、要裁定を書く。`../moorestech_logs` 側のコミットはその repo の README の手順に従う

---

### Task 8: moores-code-review で全ブランチレビュー（必須）

**Files:**
- Modify: レビューの自動適用が触ったファイルだけ

**Interfaces:**
- Consumes: Task 2〜6 のコミット一式（`origin/feature/uitk...HEAD`）
- Produces: レビュー記録（`../moorestech_logs/harness/` 配下の `$RUNDIR`）

- [ ] **Step 1: 必ず moores-code-review スキルで全ブランチレビューを実行する（自動実行・ゴール文言による省略不可）**

対象は `origin/feature/uitk...HEAD`。Codex ホストでは `.agents/skills/moores-code-review/references/codex-host.md` の手順で `scripts/codex_workflow_runner/run.mjs` を使う。この計画の `## 判断記録（ADR）` の W-1〜W-12 と食い違う提案は採らず、`done.md` の「要裁定」に書く

- [ ] **Step 2: 自動適用があれば回し直してコミットする**

自動適用が `.cs` に触れたら Task 7 Step 1 の 4 コマンドを回す。判定経路・条件式（`UiInputExclusivity`・`UiPointerHitTest`・`WebUiStartup`・`WebUiCefNavigator`・`MainGameUiRegistration`・`SwitchedUiElementLocator`・`PlaytestUiLocatorSession`）に触れたら Task 6 Step 2・3 の録画 4 本も反映後のバイナリで回し直す。
```bash
git add -A moorestech_client .agents docs
git commit -m "fix(uitk): W0-switch の moores-code-review 反映

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
（自動適用が無ければコミットしない）

---

### Task 9: PR と独立レビュー（ユーザーの裁定 1 回）

**Files:**
- なし（PR・レビュー）

**Interfaces:**
- Consumes: Task 8 後の HEAD、Task 6 の録画
- Produces: `base=feature/uitk` の PR 番号、独立レビューの指摘と裁定

- [ ] **Step 1: pr-create スキルで `base=feature/uitk` の PR を作る**

push して PR を作る（`gh pr create --base feature/uitk`。master への PR は作らない）。PR 本文の先頭行は報告ページ `# https://review.moores.tech/pr/<番号>/report/` だけ。報告ページは `~/hermes-agent/data/services/pr-review/publish/README.md` の手順で公開し、manifest に録画 4 本（UITK 側 3・Web 側 1）と `result.json` の要約、`summary` に「全体スイッチ 1 つ（Editor 限定・既定 Web）への作り直し」「UITK 側は Web ホストと CEF を作らない」「入力の裁きと DSL のロケータは片側読み」「Web 側は master と同じ Web に戻した」を載せる。`publish_pr_report.py` が失敗したら報告 URL 無しで完了にしない

- [ ] **Step 2: pr-independent-review を回す**

計画 §3.13 の「全体スイッチの作り直しに独立レビューを付ける」に従い、この環境の pr-independent-review の運用（記録は `../moorestech_logs/harness/pr-independent-review/`）で PR を独立レビューにかける。指摘は別の subagent に再現させ、再現できたものだけ残す。機械的な修正は直して push し、Task 7 Step 1 を回す。設計判断になった指摘（Requirements・判断記録 W-1〜W-12・裁定のどれかと食い違うもの）だけを、判定（どれを選ぶか）を冒頭 1 行に置いて 1 回にまとめ、指揮経由でユーザーへ上げる（実行計画 §7 U-3）。設計判断が 0 件ならユーザーへは上げない

- [ ] **Step 3: 裁定を反映する**

ユーザーの裁定を `## 判断記録（ADR）` の出所「ユーザー裁定（pr-independent-review）」として `done.md` と PR 本文に写し、反映のコミットを push する。反映が判定経路に触れたら Task 6 の録画を回し直す

---

### Task 10: feature/uitk へマージコミットで取り込む（必須）

**Files:**
- なし（取り込み・後片付け）

**Interfaces:**
- Consumes: Task 9 後の PR
- Produces: `origin/feature/uitk` のマージコミット（F1 が `--from origin/feature/uitk` で起こす土台）

- [ ] **Step 1: feature/uitk の進みを取り込み、G1 と CI を見る**

Run:
```bash
git fetch origin
git merge --no-ff --no-edit origin/feature/uitk
git push
gh pr checks <PR番号>
```
Expected: 進んでいなければ `Already up to date.`。衝突したら `docs/uitk/TODO.md` の「## 追従記録」は両方の行を採る。`MainGameUiRegistration.cs`・`MainGameUiStateRegistration.cs`・`UiInputExclusivity.cs` の衝突は、このレーンの形（分岐 1 か所・片側読み）を保って相手の追加行を足す。`.cs` が入ったら `uloop compile` と Task 7 Step 1 を回し、判定経路に触れる衝突なら Task 6 の録画を回し直す。`UITK Web follow gate (G1)` と `UITK Web follow gate self-test` が pass、G1 のジョブログ（`gh run view <run-id> --log`）に `束 1（暗転・起点 979725bcd1）: 未記帳 0 件`。checks が終わっていなければ前景で `gh pr checks <PR番号> --watch`

- [ ] **Step 2: マージコミットで取り込む**

Run: `gh pr merge <PR番号> --merge`
Expected: マージされ、`git fetch origin && git log -1 --format='%H %P' origin/feature/uitk` の親が 2 つ（Squash・rebase 禁止）

- [ ] **Step 3: 後片付けと記録**

Run:
```bash
bd close moorestech-albof.17 --reason "W0-switch: PR #<番号> で全体スイッチ1つ（Editor限定・既定Web）へ作り直し、feature/uitk へ取り込んだ"
git status --short
git log origin/uitk-lane/w0-switch..HEAD
```
Expected: `git status --short` と `git log` が空。`done.md` に PR 番号・マージコミット・G1 のジョブ URL・独立レビューの裁定を足す。Editor のスイッチを `Web` に戻したことを Task 4 Step 9 の EDC で確かめてから、指揮へ「`moores-wt rm uitk-lane/w0-switch` 可」と報告する。セッション終了可能状態（全作業がコミット・push 済みで PR がマージ済み）を確認して終える

---

## Self-Review

1. Requirements coverage: R1 → Task 2 Step 3・5・9（`UiSystemSwitchTest`）・Task 7 Step 2。R2 → Task 2 Step 4・6・Task 4 Step 6。R3 → Task 4 Step 3・5・9。R4 → Task 4 Step 1・2・9。R5 → Task 2 Step 7・8・Task 4 Step 1・4・Task 7 Step 2。R6 → Task 2 Step 1・2・9。R7 → Task 3・Task 4 Step 7。R8 → Task 4 Step 5〜9・Task 2 Step 8・Task 4 Step 1。R9 → Task 5 Step 1〜2。R10 → Task 5 Step 3〜4。R11 → Task 6。R12 → Task 7。R13 → Task 8〜10。漏れなし
2. Placeholder scan: `<PR番号>`・`<番号>`・`<REVERT_SHA>` は実行時にしか決まらない値で、取り方を同じステップに書いた。コードを変えるステップは全部コードブロックか旧テキスト／新テキストの対で示した。「適切に」「TBD」は無い
3. Type consistency: `UiSystem`（Task 2 定義）を Task 3・4・6 で同名で使う。`UiInputExclusivity.ActiveUiSystem`・`SetActiveUiSystem`・`IsPointerOverActiveUi`（Task 2）を Task 3 の `PlaytestUiLocatorSession`・Task 4 の `MainGameStarter`・L3 で同名で使う。`UiSystemEditorSwitch.Set/Current`（Task 2）を Task 4 の土台・Task 6 の EDC・台帳の手順で同名。`SwitchedUiElementLocator` は選んだ系を外へ出さず、Task 4 Step 7 は `IUiElementLocator` のまま `PlaytestUiLocatorSession.Locator` を使う。`WebUiHost.HostSkippedThisBoot`・`SetHostSkippedThisBoot`（Task 2 Step 4）を Step 6 の `WebUiCefNavigator` と Task 4 Step 6 の L3 で同名で使う。`UiRoutingSnapshot(UiScreen)`（Task 4）の唯一の生成元は `UiRoutingState.Resolve`。`MainGameUiRegistration.Register(builder, gameUiRoot, uiSystem)` の 3 引数は Task 4 Step 3 の定義と `MainGameStarter` の呼び出しで一致。`WebUiStartup.StartAsync(exitToken, uiSystem)` は Task 2 Step 4 の定義と Step 5 の呼び出しで一致
4. 保留・縮退経路: (a) `MainGameUiRegistration` の Web 分岐の早期 return は失敗ではなく裁定どおりの既知の状態（設計書 §4.4「Presenter の未登録の画面は何も出ないだけで、ログは不要」）で、起動の系は `UiSystemBootSelection` が毎回 `[UiSystem] this boot draws with …` で出す。(b) `WebUiCefNavigator` の非アクティブ化は `WebUiHost.HostSkippedThisBoot` が立った起動だけで、`Debug.Log` を出す。解除の条件は「次の起動でスイッチを Web にする」で、SessionState は Editor 内に残り、Editor を閉じれば既定 Web に戻る（証拠の寿命 = Editor のセッション、封鎖の寿命 = 1 回の起動。起動のたびに読み直すので恒久封鎖にならない）。L3 が途中で落ちてもスイッチが GameUi のまま残らないよう `[TearDown]` で Web に戻し、Task 4 Step 9 の EDC で確かめる。(c) 最小構成: 起動前に一度もスイッチを触っていない Editor（SessionState に鍵が無い）→ `GetBool(key, false)` で Web（`UiSystemSwitchTest` の 2 件目が `Set(Web)` 経由で同値を見る）。GameUi の起動で受信が 1 度も無い → `EvaluateWebSilence` が系の印（`_activeUiSystem == Web` でなければ判定しない）で無応答にしない。Web の起動で受信が 1 度も無い → `SecondsSinceLastWebMessage` が null で無応答にならない（`UiInputExclusivityWebSilenceTest` の `一度も受信していなければ無視しない`）。probe の null は「GameUi の系で DI が登録する前」だけを表し、系の区別は印が持つ。(d) 「解かない」側（GameUi の系で Web の値を読まない）と、解く側（Web の系で Web の値が効く）を `UiInputExclusivityActiveSystemTest` の 2 件で対にした
5. 「決定的にする」を根拠にした選択: 無し。`SwitchedUiElementLocator` は同点選択をせず、スイッチの値で 1 本に決まる
6. 呼び出し側への責務漏出: `UiPointerHitTest` に残っていた「GameUi か Web か」の選択を `UiInputExclusivity.IsPointerOverActiveUi` へ入れた（呼び出し側に系の分岐が出ない）。スイッチを読むのは `UiSystemBootSelection`（解決）・`WebUiStartup`（ホストを起動するか）・`MainGameStarter`（登録口へ起動の系を渡す）・`MainGameUiRegistration`（Client.Ui を登録するか）・`UiInputExclusivity`（入力の裁き）・`PlaytestUiLocatorSession`（DSL）の 6 か所で、どれも plan §3.3 R1〜R2 が名指しした責務。`WebUiCefNavigator` はスイッチを読まず、派生した `WebUiHost.HostSkippedThisBoot` だけを読む。起動の値の正本は `UiInputExclusivity.ActiveUiSystem` 1 つで、`ResolveAndApply()` の戻り値はそれへ押し込んだ直後の同じ値（同じシーンの `WebUiStartup` へ渡すだけ）。別シーンの `MainGameStarter`・DSL は正本を読む（判断記録 W-14）。系の比較はすべて「`GameUi` と等しいときだけ GameUi、それ以外は既定の Web」の向きにそろえた。画面・部品は読まない（R3 の grep）

---

## 判断記録（ADR）

設計 ADR: 新規の `docs/adr/` は無い。従うのは Global Constraints の裁定、統合計画 §2.2・§3.3・§3.8・§4.4、設計書 §1.4 C10・§4.4・[A-16]、実行計画 §1 の W0-switch 行・§7 U-3。

| ID | 判断 | 出所 | 棄却した案と理由 |
|---|---|---|---|
| W-1 | スイッチの値は `Client.Input.UiSystem { Web, GameUi }` で、`UiInputExclusivity.cs` に同居させる（`InputSuppressionScope` の前例） | agent 既定 | 別ファイル: `Client.Input/` は .cs が 10 で上限ちょうど（stage1-asbuilt §6.1）／`Client.Starter` に置く: `UiPointerHitTest`（Client.Game）と `UiInputExclusivity`（Client.Input）が参照できない／契約層（`Client.Ui.State` 等）に置く: Client.Input・Client.Game から見えず、入力の裁きが参照できない。消費者（Starter・Playtest・Game）が全員参照できる最下層が Client.Input で、型は切替 PR（5a）で消える一時の印なので契約層を新設しない（レビュー 5-G への回答）／名前に Uitk: plan §3.1 が技術名を名前に入れないと決めている |
| W-2 | 値は起動で 1 回だけ `UiSystemBootSelection.ResolveAndApply()` が解決し、`UiInputExclusivity.SetActiveUiSystem` へ押し込む。Editor の保存先は SessionState（Editor を閉じると既定 Web） | agent 既定（plan §3.3 R4 の「Editor 限定・既定 Web」を具体化） | EditorPrefs: Editor を閉じても GameUi のまま残り、普段の起動が知らずに UITK になる（UITK では未移植画面が使えない）／PlayMode 中の切替: CEF の起動要否と揃わない（stage1-asbuilt §3.3 (c)）／毎フレーム Editor の値を読む: 起動とずれる |
| W-3 | GameUi の起動は既存の `WebUiStartup` の Editor スキップ経路（0-10、`SkippedByEditorSwitch`）に載せ、`WebUiHostEditorSkip`（印）は残す。印はホストレステストの自動起動の入口だけが立てる 1 回限りの仕掛けで、裁定「全部 Web／全部 UITK の 1 つのデバッグスイッチ」の言うデバッグスイッチではない（人が選ぶ描く系の切り替えは `UiSystemEditorSwitch` の 1 つだけ。印は描く系を変えず Web の系のまま Hub=null にする）。印の起動の「Web の系でホストも CEF も無い」状態は、ホストレステストが試す状態そのもの | 計画 §2.2「UITK 側は 0-10 の経路に載せ」・実行計画の W0-switch 行・レビュー検査7 への回答（agent 既定） | 印を消してスイッチへ一本化: ホストレステスト `WebUiHostlessStateAvailabilityTest` が Web の系の Hub=null を試す手段を失う（撤去は 5a）／印の起動では CEF を残す: Hub=null の起動で CEF が届かない URL を読み続けるだけで、試す状態は変わらず条件だけ増える／飛ばした理由（スイッチ・印）を起動の状態に分けて残す（レビュー 5-C）: 下流で理由を読み分ける消費者が無い。理由はログ（`uiSystem=… skipMark=…`）に残る |
| W-4 | CEF のブラウザの GameObject（RawImage・ブラウザ・ナビゲータが同居）は、`WebUiCefNavigator.Awake` で `WebUiHost.HostSkippedThisBoot` が立っているとき非アクティブにする。値は `WebUiStartup` がホストを飛ばすと決めた時点で `WebUiHost.SetHostSkippedThisBoot`（internal・`Client.Starter` だけが呼べる）へ押し込む。診断記録 `WebUiHealthRecorder.RecordHostStartup` は従来どおり診断だけを担う（書き手は同じ `WebUiStartup`） | agent 既定 | 診断記録 `WebUiHealthRecorder.HostStartup` を読む案: バグ報告用の記録で役割違い（前例は役割で選ぶ）。記録の種類や書く時機が報告の都合で変わると CEF の生成が黙って変わるので棄却／`GameUiRoot.SetCefVisible(false)` を残す: Foundation が CEF を知り続け、設計書 §1.3 の「SetCefVisible は albof.17 で消す」と食い違う／composition root で消す: DI の構築はシーンの Start より後で、CEF の生成（`CefUnityBrowserSample.Start`）に間に合わない／スイッチを直接読む: WebUiHost に系の分岐が増える。「ホストを飛ばした起動」という WebUiHost 自身の事実で決める方が責務に合い、起動失敗（Failed）の振る舞いは master のまま変えない（裁定「CEF 由来の不具合は切替まで直さない」） |
| W-5 | Client.Ui の登録は `MainGameUiRegistration` の GameUi 分岐の中だけ。両系が使う状態とコマンド（`PauseMenuCommands`・`SettingsCommands`・`PauseMenuModel`・`SettingsModel`・`UiRoutingState`）は `MainGameUiStateRegistration` で両系とも登録。`GameUiToastFeed`・`BugReportDraft` は GameUi だけが使うので GameUi 分岐 | 設計書 [A-16]・C10 | 状態も GameUi だけ: Web のアダプタ（`WebUiGameBinder`）が `IPauseMenuCommands`・`PauseMenuModel`・`ISettingsCommands` を解決できず Web 側が壊れる／Presenter ごとにスイッチを読む: 同種の分岐が散る（[A-16] の棄却理由） |
| W-6 | `WebUiSilenceWatcher` の登録は `MainGameModelRegistration` へ移し、両系で登録する | agent 既定（cef-record §2 の master 側の置き場に合わせる） | `MainGameUiRegistration` に残す: GameUi 分岐の中では Web の系で走らず、分岐の外に置くと「Client.Ui の登録口」に WebUiHost の型が残る（stage1-asbuilt §6.2）。W0-cef の取り込みで二重登録の解決（cef-record §6）も不要になる |
| W-7 | 入力の裁きは文字入力も含めて片側読み。L1 の土台 `UiTestHost` が各テストを GameUi の系で回し、後で Web へ戻す | plan §3.3 R2（「両系の値を論理和にしない」）・agent 既定（L1 の土台） | 文字入力だけ論理和のまま: 各系で片側しか書かないので実害は無いが、R2 の L0 で「片方だけを読む」を固定できない／L1 の各テストで個別に切り替える: 30 本超に同じ 2 行が散る |
| W-8 | DSL は `SwitchedUiElementLocator`（選んだ 1 本へ委譲）。系は `PlaytestUiLocatorSession.Start` のたびに `UiInputExclusivity.ActiveUiSystem`（起動で決まった値）から選ぶ。Web/GameUi の実装インスタンスは静的に 1 つずつ持ち回す | plan §4.4・agent 既定 | Editor スイッチの値を読む: 走っている起動と食い違いうる（PlayMode 中にメニューを触った場合）／静的 readonly のまま: 静的初期化が起動前に走ると既定 Web に固定される／毎回ロケータの実装を作り直す: Web ロケータの受け口の開閉と世代の管理（`PlaytestRequestLifetimeTest`）が変わる |
| W-9 | 旧 R1 の Web 側は、`a97575e5b5`・`1095cab8bf`・`ccb3d512f7`・`5421c7f9f0` に加えて `05b9bdefa8`（休止・カットシーン明けの stage 倍率の測り直し）も外し、`moorestech_web/` を取り込み済み master と同じにする。生成物 `localizationKeys.ts`（`3e946e4bd3`）だけ残す | plan §2.2・R3（「全体スイッチの Web 側を master と同じに」）・agent 既定（05b9bdefa8 の扱い） | 4 コミットだけ外す: `05b9bdefa8` は休止の再開に付随する変更で、休止が無くなると Web が master と食い違う理由だけが残る／`localizationKeys.ts` も戻す: C# の `localization.csv` に入った拒否理由の文言と生成物が合わなくなる（W0-merge の M-3） |
| W-10 | 台帳は使い捨てスクリプトで決定的に書き換え、撤去コミットの SHA を新しい追従記録の行にする。`a97575e5b5` 等 3 行は `写した` → `捨てた` | 実行計画の W0-switch 行（「外した Web コミットを捨てた（理由）で記帳」）・W0-merge の M-5 と同じ作法 | 3 行を `写した` のまま残す: 写し先（休止の仕組み）が消え、記帳が事実と食い違う／撤去コミットを記帳しない: G1 が束 1 の未記帳 1 件として数え、取り込みの規則（未記帳 0 を見てからマージ）を満たせない |
| W-11 | `uitk-pause-web-action-count` シナリオは消す。L3 `PauseMenuGameUiTest` の ui_state 配信の検査（休止 true/false）も消す | agent 既定 | 残す: 計る対象（CEF ありで UITK のポーズ中に Web が action を出さないか）が全体スイッチで存在しなくなる（tests-ladder の「役目を終える見込み」） |
| W-12 | unityプレイ録画テストのタスク（Task 6）を入れる。UITK 側 3 本・Web 側 1 本 | moores-writing-plans の必須（入力・UI・起動に触れる） | 入れない: ランタイム挙動（起動・入力の裁き・ポーズ）を変えるので不可 |
| W-13 | 描く系の書き込み口 `UiInputExclusivity.SetActiveUiSystem` と `WebUiHost.SetHostSkippedThisBoot` は internal にし、`InternalsVisibleTo("Client.Starter")`（起動の composition root）と既存の `Client.Tests` だけに開く。読み取りは public のまま同じ静的クラスに置く | agent 既定（レビュー 5-D への回答） | public のまま運用で守る: 起動の途中でどこからでも系を書き換えられ、R1「起動ごとに 1 回」を型で守れない／読み取り口と書き込み口を別の型へ分ける: `UiInputExclusivity` は静的 facade の前例（`SetState`・`SetGameUiTextInputFocused`）で、系の印だけ別型にすると同じ静的状態の lock が 2 つの型に割れる。書き手の制限は internal で足りる／一度きりの初期化: L0/L1 が系を切り替えて試すので不可 |
| W-14 | 起動の値の正本は `UiInputExclusivity.ActiveUiSystem` 1 つ。`ResolveAndApply()` の戻り値は押し込んだ直後の同じ値で、同じシーンの `WebUiStartup` へ渡すだけ。別シーンの `MainGameStarter` と DSL は正本を読む。系の比較は「`GameUi` と等しいときだけ GameUi」にそろえ、網羅 switch は入れない | agent 既定（レビュー 5-I・検査6 4行目への回答） | 戻り値を composition root から配る: `InitializeScenePipeline` と `MainGameStarter` はシーンが別で、間で値を運ぶ置き場が結局 Client.Input の静的値になる／全消費側を網羅 switch（未知値は例外）: 2 値の一時 enum（5a で消える）で未知値が生まれず、行数だけ増える（`UiInputExclusivity.cs` は 199 行）。比較の向きのそろえ（`MainGameUiRegistration` を `!= GameUi` へ）だけ入れた |
| W-15 | 入力の裁きの Web 専用の値（`_pointerOverUi`・`_webTextInputFocused`・`_webSilent`・`_lastWebMessageTimestamp`）と GameUi 専用の値（`_gameUiTextInputFocused`・`_gameUiPointerProbe`）は、系の印 `_activeUiSystem` と同じ静的クラスに置いたままにする。読む口はすべて系の印で片側に絞り（`IsPointerOverWebUi`・`IsPointerOverGameUi`・`RecomputeTextInputFocused`・`EvaluateWebSilence`）、L0 で固定する。`SwitchedUiElementLocator` も両系の実装を受け取り ctor で 1 本を選ぶ | agent 既定（レビュー 5-A・5-B への回答。5-A は無応答の判定を系の印に寄せた） | 系ごとの入力状態の型を作り起動で 1 つだけ持つ: Web 側の値は切替 PR（5a）で丸ごと消える（Requirements「やらないこと」）ので、消える型を今作るだけになる。Web の書き手（WS の受信スレッド）と GameUi の書き手の API は変えない約束（Task 2 Interfaces）で、型を分けても facade の中で印による振り分けが残る。`Client.Input/` は 10 ファイルの上限で、型を足すとサブディレクトリの新設まで要る／ロケータに選んだ 1 本だけを渡す: 選ぶ処理がセッション側へ移り、L0 で「選んだ系だけを照会・開閉する」を偽 2 本で固定できなくなる |

## 本PR外のリファクタ提案

1. `moorestech_client/Assets/Scripts/Client.Playtest/Operations/Ui/PlaytestUiOps.cs:131-146` の `WaitPointerLeaveWebUi` は、設置クリック前の「UI の上か」を Web の値（`IsPointerOverWebUi`）だけで待つ。本 PR の `UiInputExclusivity.IsPointerOverActiveUi` と同じ役割で、GameUi の系では常に即抜けになる。後続レーンで既存側を `IsPointerOverActiveUi(マウス位置)` へ委譲させる（GameUi の Pick は同期なので待ちは即抜けで済むが、判定の正本を 1 本にする）

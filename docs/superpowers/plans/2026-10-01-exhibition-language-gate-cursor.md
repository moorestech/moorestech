# 出展ビルドの言語選択でマウスカーソルが出ない不具合 修正 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 出展モードの言語選択ゲート待機中に、実マウスカーソルが見えて言語ボタンを押せるようにする。

**Architecture:** 原因は「MainGameシーンのロード直後に `GameStateController.Start()` がゲーム中状態へ入りカーソルを Locked+非表示にする一方、言語ゲート待機中はまだ UIState（GameScreenState 等）が存在せず誰もカーソルを戻さない」こと（コード読解による仮説。Task 1で実測確定）。ゲート開始側（`EventModeStartGate`）が待機の前後でカーソル表示を `InputManager.MouseCursorVisible` へプッシュする。待機解除後のロックは既存の `GameScreenState.OnEnter`→`EnterGameplay` が担う。

**Tech Stack:** Unity 6 / C# / NUnit(EditMode) / UniTask / UniRx / uloop

## 調査結果（根拠）

- `Client.MainMenu/*` と `Client.Starter/*` にメインメニュー上のカーソル制御は無い（`Cursor.*` / `MouseCursorVisible` を呼ぶのは `InputManager` とゲーム中UIState群のみ）。cef-unity 0.6.3 のコードにもカーソル操作は無い。
- 出展モード（`scripts/event/start-gamescom-loop.command` が `MOORESTECH_EVENT_MODE=1`）は `EventModeAutoStart` がメインメニューシーンから `LocalGameLauncher.StartLocalGame()` を即呼ぶため、メインメニューは一瞬しか出ず、来場者が見る「言語選択」は MainGame 内の WebUI 全画面ゲート（ADR 0040）。
- `MainGameInitializationFinalizer.FinalizeAsync` は先頭で `EventModeStartGate.WaitForLanguageSelectionAsync` を await し、`starter.StartGame`（DI構築・UIState生成）より前にいる。
- `GameSystem.prefab`（MainGame.unity内）の `GameStateController.Start()` → `ChangeState(InGame)` → `SetInGameState()` → `InputManager.MouseCursorVisible(false)`（`Client.Input/InputManager.cs:36`）で Locked+非表示。待機中これを解く経路は無い。
- 未確定: `GameStateController.Start()` とゲートのawait開始の前後関係（Task 1で測る）。また「メインメニュー本体（非出展）でも出ない」場合は別原因なので Task 1 で切り分ける。

## Requirements

1. 出展モード（`EventExhibitionSettings.IsEnabled`）の言語ゲート待機中、`Cursor.visible == true` かつ `Cursor.lockState == None`。受入: EditModeテストで待機開始後に両条件が成立。
2. 言語選択（待機解除）後は、既存どおり `GameScreenState.OnEnter` がカーソルをロックする。受入: 録画テストで選択後にロックへ戻る（既存の `EnterGameplay` を変更しない）。
3. 出展モードでない通常起動の挙動は変えない（`settings.IsEnabled == false` は早期returnのまま）。
4. 待機が `ct` キャンセルで抜けた場合に例外でカーソル状態を壊さない。
- やらないこと: メインメニューのUI変更、`GameStateController` の責務変更、WebUI側のCSS `cursor` 変更、ゲート画面のデザイン変更。

## Global Constraints

- 全コード200行未満。`partial` / `Func<>` / try-catch / 既定引数 禁止。.metaは手で作らない。
- コメントは日本語→英語の2行セット（各1行）。
- 時間計測にUnity実時間APIを使わない（本件は時間を扱わない）。
- 無音の縮退禁止: カーソルを出せない経路（`Mouse.current == null` 等）に入るなら理由ログを出す。
- コンパイル必須（.cs変更後 `uloop compile --project-path ./moorestech_client`）。作業は専用worktree `~/hermes-agent/data/worktrees/moorestech/exhibition-language-gate-cursor`（ブランチ `fix/exhibition-language-gate-cursor`）のEditorで行う。

## File Structure / 前例照合

| ファイル | 責務 | 既存部品 | 方針 |
|---|---|---|---|
| `moorestech_client/Assets/Scripts/Client.Starter/EventMode/EventModeStartGate.cs` | 待機前後のカーソル表示をプッシュ | `InputManager.MouseCursorVisible`（唯一のカーソル切替窓口） | 呼ぶ（新規実装しない） |
| `moorestech_client/Assets/Scripts/Client.Starter/Client.Starter.asmdef` | `Client.Input` 参照追加（未参照なら） | 他asmdefの前例 | 必要時のみ追記 |
| `moorestech_client/Assets/Scripts/Client.Tests/EventMode/EventModeStartGateCursorTest.cs`（新規） | 待機中カーソル表示の回帰テスト | `EventModeStartGateTest.cs` のフィクスチャ形 | 同形で新規 |

前例: ゲーム内でカーソルを出す箇所は全て `InputManager.MouseCursorVisible(true)` をUIStateの入口から呼ぶ（例 `PlayerInventoryState.cs:59`, `PauseMenuStateService.cs:47`）。ゲートは UIState ではないので、ゲート開始側が待機開始時に1回プッシュする（`Update()` での毎tick監視はしない）。

## 保留・縮退経路の表

| 状態 | 起きる時機 | 誰が起こす | ユーザーに見えるもの | 操作なしで解消 |
|---|---|---|---|---|
| 待機中（修正後） | MainGameロード完了〜言語ボタン押下 | 来場者の放置 | 言語選択画面＋表示されたカーソル | 押下で解除（永久待機はADR 0040の裁定どおり） |
| `hub == null`（画面を出せない） | WebUiHost不起動 | 環境異常 | 英語のまま開始（既存） | 自動。カーソル操作は不要なので触らない |
| `Mouse.current == null` | マウス未接続のブース機 | 環境 | カーソル出ない | 解消しない。`MouseCursorVisible` 内のwarpは既にnull時return。ログは Task 2 で追加 |

## Task 1: 原因の実測確定（コード変更なし・結果をplan末尾のメモに記す）

**Files:**
- Read: `moorestech_client/Assets/Scripts/Client.Game/Common/GameStateController.cs`
- Read: `moorestech_client/Assets/Scripts/Client.Starter/Initialization/MainGameInitializationFinalizer.cs`

**Interfaces:** Produces: 「`GameStateController.Start` がゲートawait開始より前/後か」「非出展のメインメニューで出ない場合の有無」の2点の実測結果。

- [ ] **Step 1:** worktreeのEditorを起動しcompile疎通を確認: `uloop launch <worktree>/moorestech_client` → `uloop compile --project-path ./moorestech_client`
- [ ] **Step 2:** `MOORESTECH_EVENT_MODE=1` で PlayMode（MainMenu起点）を起動し、ゲート待機中に `uloop execute-dynamic-code` で `UnityEngine.Cursor.visible` と `Cursor.lockState` を読む。期待: `false` / `Locked`（仮説どおり）。
- [ ] **Step 3:** 同条件で `GameStateController.Start` 直前・`EventModeStartGate.WaitForLanguageSelectionWithHubAsync` 冒頭に一時 `Debug.Log($"frame={Time.frameCount}")` を入れて前後関係を採り、ログを消す（コミットしない）。
- [ ] **Step 4:** `MOORESTECH_EVENT_MODE` 無しでメインメニューを出し、`Cursor.visible` が `true` であることを確認（メインメニュー本体が原因でないことの切り分け）。`false` なら本planの前提が崩れるのでユーザーへ報告して止まる。
- [ ] **Step 5:** 結果をplan末尾「実測メモ」へ追記しコミット。

## Task 2: ゲート待機中のカーソル表示（TDD）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/EventMode/EventModeStartGate.cs:36-47`（`AwaitSelectionThenArmAsync` 直前でプッシュ）
- Modify(必要時): `moorestech_client/Assets/Scripts/Client.Starter/Client.Starter.asmdef`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/EventMode/EventModeStartGateCursorTest.cs`

**Interfaces:**
- Consumes: `InputManager.MouseCursorVisible(bool)`（`Client.Input`）、`EventLanguageGate`、`EventModeStartGate.AwaitSelectionThenArmAsync(gate, idleTimeoutSeconds, armer, ct)`
- Produces: `internal static UniTask AwaitSelectionWithVisibleCursorAsync(EventLanguageGate gate, CancellationToken ct)`（カーソルを出して選択を待つだけ。武装は呼び出し側）

**実装方針（Task 1 の結果で分岐。agent判断）:**
- `Start` がゲート開始より**後**に走る場合: 待機の先頭で `await UniTask.NextFrame(ct)` を挟んでから `MouseCursorVisible(true)`（`Start` の後勝ちにする。前後関係を決める根拠をコメントに残す）。
- `Start` が**前**に走る場合: そのまま待機直前に `MouseCursorVisible(true)`。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System.Threading;
using Client.Localization;
using Client.Starter.EventMode;
using Client.WebUiHost.Game.EventMode;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.EventMode
{
    public class EventModeStartGateCursorTest
    {
        [SetUp]
        public void SetUp()
        {
            Localize.Initialize();
            // 本番で先にロックされている状態を再現する
            // Reproduce the production state where the cursor is already locked
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        [Test]
        public async Task 言語選択を待っている間はカーソルが表示される()
        {
            var gate = new EventLanguageGate(true);
            var wait = EventModeStartGate.AwaitSelectionWithVisibleCursorAsync(gate, CancellationToken.None).AsTask();
            await UniTask.NextFrame();

            Assert.IsTrue(Cursor.visible);
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState);

            gate.TrySelectLanguage("japanese");
            await wait;
        }
    }
}
```
（`async Task` のため先頭に `using System.Threading.Tasks;` も入れる。）

- [ ] **Step 2:** `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "EventModeStartGateCursorTest"` → 期待: コンパイルエラー（メソッド未定義）で失敗。
- [ ] **Step 3: 最小実装** — `EventModeStartGate.cs` に追加し、`WaitForLanguageSelectionWithHubAsync` の `await AwaitSelectionThenArmAsync(...)` の前に `await AwaitSelectionWithVisibleCursorAsync(gate, ct)` ではなく、`AwaitSelectionThenArmAsync` 内の最初の `await gate.WaitForSelectionAsync()` を差し替える形で呼ぶ（既存テスト `EventModeStartGateTest` の順序契約を保つため）:

```csharp
// 待機中は来場者が言語ボタンを押せるよう実カーソルを出す。ロックの再開は選択後のGameScreenState入場が担う
// Show the real cursor while waiting so visitors can press a language button; GameScreenState re-locks it after the choice
internal static async UniTask AwaitSelectionWithVisibleCursorAsync(EventLanguageGate gate, CancellationToken ct)
{
    InputManager.MouseCursorVisible(true);
    await gate.WaitForSelectionAsync().AttachExternalCancellation(ct);
}
```
`AwaitSelectionThenArmAsync` の1行目 `await gate.WaitForSelectionAsync().AttachExternalCancellation(ct);` を `await AwaitSelectionWithVisibleCursorAsync(gate, ct);` へ置換。asmdefに `Client.Input` が無ければ追加。
- [ ] **Step 4:** 同テスト＋`EventModeStartGateTest`＋`EventLanguageGate.*` を実行 → PASS。
- [ ] **Step 5:** `uloop compile --project-path ./moorestech_client`（エラー0）。
- [ ] **Step 6:** コミット `fix(event-mode): 言語選択ゲート待機中にマウスカーソルを表示する`

## Task 3: 実機相当の検証（unityプレイ録画テスト）

**Files:** `.agents/skills/unity-playmode-recorded-playtest` の既存手順に従い、既存の出展モード系シナリオがあればそれを流用（無ければ新設せず手動のuloop検証で代替し、その旨を記録）。

- [ ] **Step 1:** `MOORESTECH_EVENT_MODE=1` でPlayMode起動、ゲート待機中に `Cursor.visible==true / lockState==None` を `execute-dynamic-code` で確認。
- [ ] **Step 2:** 言語ボタンを入力注入で押し、ゲーム開始後に `Cursor.lockState==Locked`（通常プレイ復帰）を確認。
- [ ] **Step 3:** Editor.logで警告語（`refus` / `lost` / `fall` / `mismatch` / `orphan` / `Error`）がe2e区間でゼロであることを確認（期待語のgrepだけで合格にしない）。
- [ ] **Step 4:** 出展ビルド（`moorestech/Build/MacOsExhibitionBuild`）を1回作り、ループスクリプトで起動して実マウスで言語を選べることを確認（既存bd `moorestech-4ltg` の実機通しと兼ねられるなら記録をそこへ）。

## Task 4（最後・必須）: レビュー・PR

- [ ] **Step 1:** 全作業をコミット。
- [ ] **Step 2:** `moores-code-review` スキルで全ブランチレビューを実行（自動実行・省略不可）。指摘反映が判定経路に触れたら Task 3 を反映後バイナリで再実施。
- [ ] **Step 3:** 残課題（未検証・未確認）は1件ずつ `bd create` で起票し、記録に issue 番号を列挙する。
- [ ] **Step 4:** push → `pr-create` スキルでPR作成。PR後 `moores-wt rm exhibition-language-gate-cursor`。

## 判断記録（ADR）

- 本件は新規ADR不要の不具合修正。関連: `docs/adr/0040-event-mode-language-select-gate.md`、`.decisions/2026-08-28-出展モードの言語選択はロード完了後の全画面ゲートにする.md`（ゲートはロード後・永久待機の裁定を変えない）。
- 修正箇所を `GameStateController` でなくゲート開始側にした: agent判断。理由は、状態遷移の責務を変えず、出展ゲート固有の「UIStateが無い待機区間」をゲート自身が埋めるため。`GameStateController.Start` の `InGame` 遷移（プレイヤー有効化）を温存でき、前例（UI側が入口でカーソルを出す）にも沿う。
- 実装方針の分岐（`NextFrame` を挟むか）は Task 1 の実測で決める: agent判断。

## 実測メモ

（Task 1 完了時に追記）

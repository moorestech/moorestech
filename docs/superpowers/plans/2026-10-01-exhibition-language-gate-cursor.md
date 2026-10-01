# 出展モードの言語選択でマウスカーソルが出ない不具合 修正 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 出展モード（ループスクリプト起動）の言語選択ゲート待機中に、実マウスカーソルが見えて言語ボタンを押せるようにする。

**Architecture:** MainGameシーンの `GameStateController.Start()` がカーソルを Locked+非表示にするが、言語ゲート待機中は UIState が未生成で誰も戻さない。ゲート側（`EventModeStartGate`）が、`Start()` の実行を1フレーム待って追い越させてから `InputManager.MouseCursorVisible(true)` をプッシュする。選択後のロックは既存の `GameScreenState.OnEnter` が担う。

**Tech Stack:** Unity 6 / C# / NUnit / UniTask / uloop / cef-unity 0.6.4

## 症状と前提（確定済み）

- 報告: 出展者用ビルドを**ループスクリプト経由**（`scripts/event/start-gamescom-loop.command`、`MOORESTECH_EVENT_MODE=1`）で起動し、言語選択の画面でマウスカーソルが出ない（ユーザー回答 2026-10-01）。
- 出展モードではメインメニューは `EventModeAutoStart` が即座に `LocalGameLauncher.StartLocalGame()` で抜けるため、来場者が触る言語選択は MainGame 内の WebUI 全画面ゲート（ADR 0040）。メインメニューの `LanguageSetting` は対象外。

## 原因（コードで確定した部分と未実測の部分）

コードで確定:
1. `InitializeScenePipeline` は MainGame の `SceneManager.sceneLoaded` コールバック内で `MainGameInitializationFinalizer.RunAsync` を同期的に開始する（`Client.Starter/InitializeScenePipeline.cs:163-188`）。その先頭で `EventModeStartGate.WaitForLanguageSelectionAsync` を await する（`MainGameInitializationFinalizer.cs:50`）。UIState を作る `starter.StartGame` はこの後（同 `:54`）。
2. Unity の実行順では `sceneLoaded` は新シーンの Awake/OnEnable の後・**Start の前**に呼ばれる。よってゲートの待機開始は `GameStateController.Start()` より先で、その後 `Start()` → `ChangeState(InGame)` → `SetInGameState()` → `InputManager.MouseCursorVisible(false)` でロックされる（`Client.Game/Common/GameStateController.cs:28-56`、`Client.Input/InputManager.cs:36-43`）。
3. 待機中にカーソルを戻す経路は無い。`Client.MainMenu` / `Client.Starter` / cef-unity 0.6.4（hash `91a6078`）/ WebUI の CSS のいずれにもカーソル操作は無い。
4. ロックは2024-04から、ゲートは2026-08-28（`14c8aed6c`）から存在。仮説が正しければゲート導入時から壊れており、実機通し（bd `moorestech-4ltg`）が未了であることと整合する。

未実測（Task 1 で測る）:
- 出展モードPlayModeのゲート待機中に `Cursor.visible == false` / `Cursor.lockState == Locked` になっていること（＝赤の再現）。
- Editor で `Cursor.lockState` の書き込みがEditMode・PlayModeそれぞれで保持されるか（テスト手段の選択に使う。既存テストにカーソル状態を検証する前例は無い）。

## Requirements

1. 出展モードのゲート待機中、`GameStateController.Start()` 実行後も `Cursor.visible == true` かつ `Cursor.lockState == None`。受入: Task 2 の回帰テストが修正前に赤・修正後に緑。
2. 言語選択後は既存どおり `GameScreenState.OnEnter`→`EnterGameplay` がカーソルをロックする。受入: Task 3 で選択後に `Locked` を確認。`EnterGameplay` / `GameStateController` は変更しない。
3. 通常起動（`settings.IsEnabled == false`）とWebUiHost不在（`hub == null`）の挙動は変えない。カーソル操作はゲートを実際に出す経路でだけ行う。
4. 待機が `ct` キャンセルで抜けた場合、例外は既存どおり `OperationCanceledException` で上へ流れ、カーソル操作が追加の例外を出さない。
5. 既存テスト `EventModeStartGateTest`（選択→武装の順序契約）を壊さない。
- やらないこと: メインメニューUI・`LanguageSetting` の変更、`GameStateController` の責務変更、WebUI側の変更、ゲート画面デザインの変更、マウス未接続環境への対応（`MouseCursorVisible(true)` はマウスの有無に依存せず、未接続時は表示するカーソル自体が無い）。

## Global Constraints

- 全コード200行未満。`partial` / `Func<>` / try-catch / 既定引数 禁止。.metaは手で作らない。1ディレクトリ10ファイルまで（`Client.Tests/EventMode` は現在 .cs 6本）。
- コメントは日本語→英語の2行セット（各1行）。順序依存の待ち（NextFrame）には「なぜ1フレーム待つか」の根拠コメントを必ず書く。
- サーバーの時間規約は本件対象外（クライアントのフレーム待ちのみ）。
- .cs変更後は `uloop compile --project-path ./moorestech_client`。作業は worktree `~/hermes-agent/data/worktrees/moorestech/exhibition-language-gate-cursor`（ブランチ `fix/exhibition-language-gate-cursor`、土台 `b8d2177b5`）の自前Editorで行う。

## File Structure / 前例照合

| ファイル | 責務 | 既存部品 | 方針 |
|---|---|---|---|
| `moorestech_client/Assets/Scripts/Client.Starter/EventMode/EventModeStartGate.cs` | 待機開始時に1フレーム待ってカーソル表示をプッシュ | `InputManager.MouseCursorVisible`（唯一のカーソル切替窓口） | 呼ぶ |
| `moorestech_client/Assets/Scripts/Client.Starter/Client.Starter.asmdef` | `Client.Input` 参照を追加（現状未参照を確認済み） | 他asmdefの参照記法 | 追記 |
| 回帰テスト（置き場は Task 1 の結果で決定。下記） | 待機中カーソル表示の回帰 | EditMode: `EventModeStartGateTest.cs` の環境変数退避形／PlayMode: `Client.Tests/EditModeInPlayingTest/*` | 同形で新規 |

前例: ゲーム内でカーソルを出す箇所は全て入口で `InputManager.MouseCursorVisible(true)` を1回呼ぶ（`PlayerInventoryState.cs:59`、`PauseMenuStateService.cs:47`）。ゲートはUIStateではないので、ゲート自身が待機区間の入口で同じことをする。`Update()` での毎フレーム監視はしない。

## Task 1: 赤の再現とテスト手段の確定（本番コード変更なし）

**Files:** なし（結果は本plan末尾「実測メモ」へ追記）

- [ ] **Step 1:** worktreeのEditorを起動してcompileを通す: `moores-wt status` で自分のworktreeにEditorが無いことを確認 → `uloop launch <worktree>/moorestech_client` → `uloop compile --project-path ./moorestech_client`。
- [ ] **Step 2（テスト手段の判定）:** EditModeで `uloop execute-dynamic-code` から `Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;` を設定し、直後と1フレーム後に読み返す。両方とも保持されれば「EditMode可」、されなければ「EditMode不可」と記録。
- [ ] **Step 3（赤の再現）:** Editorプロセスに `MOORESTECH_EVENT_MODE=1` を渡して（`EventModeStartGateTest` と同じく `Environment.SetEnvironmentVariable` を execute-dynamic-code で設定してから）MainMenuシーンでPlayModeに入る。ゲート待機中に `Cursor.visible` / `Cursor.lockState` / `GameStateController.CurrentState` を読む。期待: `false` / `Locked` / `InGame`。違えば本planの原因が崩れるので、実装に進まずユーザーへ報告して止まる。
- [ ] **Step 4:** PlayModeで `Cursor.lockState` が Game ビュー非前面でも `Locked` を返すかを Step 3 の値で判定（`Locked` が読めれば「PlayMode可」）。
- [ ] **Step 5:** 結果を「実測メモ」に追記してコミット（`docs: 出展ゲートのカーソル実測結果`）。

**テスト手段の決定規則:** PlayMode可なら Task 2 は EditModeInPlayingTest で「出展モードでゲート待機に入ったらカーソルが見える」を検証する（`Start()` との順序まで含めて本物の故障を捕まえられるため第一候補）。PlayMode不可・EditMode可なら EditMode単体テスト（下記の代替）。両方不可ならテストを新設せず Task 3 の実機確認を唯一の検証とし、その理由をPR本文と bd に残す。

## Task 2: ゲート待機中のカーソル表示（TDD）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/EventMode/EventModeStartGate.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Client.Starter.asmdef`（`Client.Input` 追加）
- Create: 回帰テスト（Task 1 の決定規則に従う）

**Interfaces:**
- Consumes: `InputManager.MouseCursorVisible(bool)`、`EventLanguageGate.WaitForSelectionAsync()`
- Produces: `internal static async UniTask ShowCursorAfterSceneStartAsync(CancellationToken ct)`

- [ ] **Step 1: 失敗するテストを書く**
  - 第一候補（PlayMode可）: `Client.Tests/EditModeInPlayingTest/Boot/EventLanguageGateCursorTest.cs` を、同ディレクトリの `LocalPlayEmbeddedServerBootTest.cs` の起動形（`EnterPlayMode(expectDomainReload: true)` を `[UnityTest]` 直下で呼ぶ・`LoadMainGame` 相当）に倣って作る。`EnterPlayMode` 前に `MOORESTECH_EVENT_MODE=1` を設定する（環境変数はプロセス単位なのでドメインリロードを越える。`EventModeStartGateTest` の退避・復元形で後始末）。待機中の判定は「MainGameシーンがアクティブ」かつ「`GameInitializedEvent.OnGameInitialized` が未発火」の状態で数フレーム進めた時点とし、そこで `Assert.IsTrue(Cursor.visible)` と `Assert.AreEqual(CursorLockMode.None, Cursor.lockState)` を検証する。言語は選ばずに `ExitPlayMode` で抜ける（キャンセルは `InitializeScenePipeline` が失敗扱いせずログだけ出す既存経路）。ゲート個体を取り出すためのテスト専用publicは足さない（規約違反）。
  - 代替（EditMode可のみ）: `Client.Tests/EventMode/EventModeStartGateCursorTest.cs` で、`Cursor` を Locked にしておき `ShowCursorAfterSceneStartAsync` を呼び、await 後に表示を確認する。この形は `Start()` との順序を検証できないため、Task 3 Step 1 を必須の補完とする。
- [ ] **Step 2: 赤を確認する。** 本番コードには `ShowCursorAfterSceneStartAsync` の**空実装**（`await UniTask.NextFrame(ct);` だけ）を先に入れてcompileを通し、テストが**アサーションで**失敗することを確認する（コンパイルエラーでの失敗は赤と数えない）。
- [ ] **Step 3: 実装する。**

```csharp
// 待機開始はsceneLoaded内でGameStateController.Start()より先に走り、Startがカーソルをロックする。1フレーム待って後勝ちにする
// The wait starts inside sceneLoaded, before GameStateController.Start() locks the cursor, so wait one frame to win last
internal static async UniTask ShowCursorAfterSceneStartAsync(CancellationToken ct)
{
    await UniTask.NextFrame(ct);
    InputManager.MouseCursorVisible(true);
}
```

`WaitForLanguageSelectionWithHubAsync` の `await AwaitSelectionThenArmAsync(...)` の直前（`hub == null` の早期returnの後）で、待機と並行させる:

```csharp
// 来場者が言語ボタンを押せるよう待機中は実カーソルを出す。選択後のロックはGameScreenStateの入場が担う
// Show the real cursor while waiting so visitors can press a language button; GameScreenState re-locks it after the choice
ShowCursorAfterSceneStartAsync(ct).Forget();
```

`AwaitSelectionThenArmAsync` は変更しない（順序契約テストを保つ）。`Forget` にした理由: カーソル表示は待機の完了条件ではなく、選択が1フレーム以内に来ても待機を遅らせないため。キャンセル時は `NextFrame(ct)` が `OperationCanceledException` で抜けるだけで、`Forget` の既定ハンドラはキャンセルをエラー扱いしない（Requirement 4）。選択が NextFrame より先に完了した場合に、`GameScreenState` のロック後へ表示が後勝ちしないことを Step 4 で確認する（選択→`StartGame`→UIState生成は少なくとも `UniTask.Yield()` を挟むため1フレームを超える想定。崩れるなら `gate.IsWaitingSelection` を確認してから出す形へ変える）。
- [ ] **Step 4:** 新テスト＋ `EventModeStartGateTest` ＋ `EventLanguageGate` 系テストを実行（`uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "EventLanguageGate|EventModeStartGate"`、PlayMode系は `--test-mode PlayMode` が必要なら明示）→ PASS。
- [ ] **Step 5:** `uloop compile --project-path ./moorestech_client`（エラー0）、`uloop get-logs --log-type Error` で新規エラー無し。
- [ ] **Step 6:** コミット `fix(event-mode): 言語選択ゲート待機中にマウスカーソルを表示する`

## Task 3: 実機相当の検証

- [ ] **Step 1:** Editor PlayMode（出展モード）でゲート待機中に `Cursor.visible == true / lockState == None` を確認し、言語ボタンを WebUI 経由で押した後、ゲーム開始時に `Locked` へ戻ることを確認する（unityプレイ録画テストのDSLに出展ゲート操作があれば流用。無ければ execute-dynamic-code で `EventLanguageGate` に選択させて代替し、その旨を記録）。
- [ ] **Step 2:** Editor.logで警告語（`refus` / `lost` / `fall` / `mismatch` / `orphan` / `Error`）が検証区間でゼロであることを確認（期待語のgrepだけで合格にしない）。
- [ ] **Step 3:** 出展ビルド（`moorestech/Build/MacOsExhibitionBuild`）を作り、`start-gamescom-loop.command` で起動して、実マウスでカーソルが見え言語を選べること、選択後にカーソルがロックされることを確認する。Editorの結果はビルドのカーソル挙動の代わりにならないため省略しない。実機通しは bd `moorestech-4ltg` と兼ねてよい。

## Task 4（最後・必須）: レビュー・PR

- [ ] **Step 1:** 全作業をコミット。
- [ ] **Step 2:** `moores-code-review` で全ブランチレビュー（省略不可）。指摘反映がゲートの待機経路に触れたら Task 3 を再実施。
- [ ] **Step 3:** 残課題（Task 1 で「テスト不可」になった場合の検証の穴など）は `bd create` で起票し番号を列挙。
- [ ] **Step 4:** push → `pr-create` でPR作成 → `moores-wt rm exhibition-language-gate-cursor`。

## 判断記録

- 新規ADR不要の不具合修正。関連: `docs/adr/0040-event-mode-language-select-gate.md`、`.decisions/2026-08-28-出展モードの言語選択はロード完了後の全画面ゲートにする.md`（ロード後・永久待機の裁定は変えない）。
- 起動経路はループスクリプト経由: ユーザー回答（2026-10-01）。
- 修正位置をゲート側にした: agent判断。`GameStateController.Start()` の InGame 遷移（プレイヤー有効化を含む）を温存し、「UIStateが無い待機区間」を区間の持ち主であるゲートが埋めるため。
- 1フレーム待ちにした: agent判断。`sceneLoaded` が Start より前に呼ばれるというUnityの実行順から導いた（前planの「実測で分岐」はコードで決着済み）。
- 前plan（`2f950243b`）からの変更点: 起動経路を確定、順序をコードで確定してNextFrame一択に、EditModeでのカーソル検証が成立するかを Task 1 で先に判定、赤はアサーション失敗で確認、`Mouse.current == null` の縮退行を削除（表示側はマウス有無に依存しないため対象外）、土台を cef 0.6.4 の master へ更新。

## 実測メモ

Task 1 実測（2026-10-01・worktree の自前 Editor）:

- Step 2（EditMode）: execute-dynamic-code で `Cursor.lockState = Locked; Cursor.visible = false` を書き、同じ呼び出し内と次の呼び出し（後続のEditorフレーム）で読み返すと両方 `visible=False lock=Locked`。→ **EditMode可**。
- Step 3（赤の再現）: Editor プロセスへ `MOORESTECH_EVENT_MODE=1` と `MOORESTECH_EVENT_MODE_EDITOR=1`（Editor では opt-in 必須）を設定し MainMenu で Play。ゲート待機中（MainGame がアクティブ、`event_mode.language_gate` topic の snapshot が `{"waiting":true}`、WebUiHost.Hub 非null）に `Cursor.visible=False` / `lockState=Locked` / `GameStateController.CurrentState=InGame`。**赤を再現**。
- Step 4: PlayMode 中に `Locked` が読めた。→ **PlayMode可**。よって Task 2 は EditModeInPlayingTest を採る。
- 再現経路の注記（plan と違った点）:
  - 新規 worktree には同梱 Node（`moorestech_web/node`）と `webui/node_modules` が無く、WebUiHost が起動せず `hub == null` の縮退（英語で即開始・ゲート無し）になった。`moorestech_web/setup.sh` と `pnpm install --frozen-lockfile` で用意してから再現した。
  - Editor で MainMenu から Play すると `EventModeAutoStart` の `StartLocalGame` が `[PlaytestTitleGates] InitializeScenePipeline refused: the title gates never started` で断られ MainMenu へ戻った（タイトルの列はその後 `Passed` になる）。2回とも同じ。タイトル列通過後に execute-dynamic-code で `LocalGameLauncher.StartLocalGame()` を呼んで MainGame へ進めた。配布ビルドでも同じ断りが起きるかは Task 3 Step 3 で確認する。
  - ゲート待機中は `UIStateControl.Update`（UIStateControl.cs:54）と `ThirdPersonController`（:173/:309）が毎フレーム NullReferenceException を出していた（修正前から存在。本件の範囲外として記録のみ）。

Task 3 実測（2026-10-01・修正 `fae1980e9` 入り）:

- Step 1（Editor PlayMode・出展モード）: ゲート待機中（topic `{"waiting":true}`、MainGameアクティブ、`GameStateController.CurrentState=InGame`）に `Cursor.visible=True` / `lockState=None`。execute-dynamic-code で `event_mode.select_language` アクションを現行言語で実行 → 導入スキットへ（Skit中は既存どおり表示）→ `SkitPresentationStateStore.TrySkip` でスキットを飛ばすと `GameScreen` 入場で `visible=False` / `Locked`。録画DSLに出展ゲート操作は無いため execute-dynamic-code で代替した。
- Step 1 追加（選択が NextFrame より先に完了する競合）: 毎フレーム `PlayerLoopTiming.Initialization` で topic を監視し、待機が見えた最初のフレームで選択させた。選択フレーム（489）では `Locked`（NextFrame 未到達）、次フレーム（490）以降は Skit で表示、スキット後の `GameScreen` で `Locked` のまま。ロックは `RestoreLoginState`（初期snapshot・地形構築・スキットの後）で起こるため、表示のプッシュがロック後へ後勝ちする経路は観測されなかった。
- Step 2（ログ）: 検証区間の警告語ヒットは `InitializeScenePipeline refused: the title gates never started`（下記の既存事象）と `Script error: OnTerrainChanged`（既存）のみ。ゲート待機中は毎フレーム `UIStateControl.Update:54` / `ThirdPersonController:173,309` の NullReferenceException が出る（修正前の Task 1 でも同じ。本件の変更とは無関係）。
- Step 3（出展ビルド実機）: `PlayerBuildRequest.ForExhibition` で batchmode ビルド（Succeeded・15分）→ `MOORESTECH_EVENT_MODE=1 MOORESTECH_EVENT_LANGUAGE=german` で単発起動（ループスクリプトと同じ環境変数。無限ループは回さず）。
  - **ビルドでも `EventModeAutoStart` が `InitializeScenePipeline refused: the title gates never started` で断られ、MainMenu（ドイツ語UI・言語ドロップダウン付き）に戻った。** 出展モードの自動開始が機能していない別の不具合（本planの前提「EventModeAutoStart が即座に抜ける」と矛盾）。MainMenu 上ではカーソルは表示されていた。
  - MainMenu の「Lokal spielen」を cliclick で押して MainGame へ進めると、WebUI の言語選択ゲート（English/日本語/Deutsch/한국어）でカーソルが表示され、マウス移動に追従し English ボタンにホバーした（`screencapture -C` で確認）。English を押すとスキットが始まり、スキップ後のゲーム画面ではカーソルが非表示（ロック）になった。
  - 実マウスの物理操作ではなく cliclick の合成入力での確認。

出展モード自動開始の修正（2026-10-01・`4f3b25e20`）:

- 原因: `EventModeAutoStart`（AfterSceneLoad）がタイトル合成ルートの Start より先に `StartLocalGame` を呼び、`InitializeScenePipeline` が「確認が未開始」で断っていた（66877e531 で待ちが外れた）。smoke と同じく `PlaytestTitleGates` の通過を期限付き（60秒）で待ってからワールド削除と開始を行うよう直した。期限と待ち方は `PlaytestTitleGates.WaitUntilPassedWithinUnattendedDeadlineAsync` / `UnattendedPassTimeoutSeconds` に寄せ、smoke と共有した。起動言語の適用は今どおり同期で先に行う。
- 回帰テスト `EventModeAutoStartBootTest`（EditModeInPlayingTest）: `playModeStartScene` を MainMenu にして本物の起動フックで起動し、MainGame へ届くことを確かめる。開発機の `Saves/world_1` は同じ Saves 内の退避名へ動かして守り、Play 終了後に戻す。修正前は「returned to MainMenu (reloaded at 0.02s)」で赤、修正後は緑。
- 出展ビルド実機（Editor から `ForExhibition` で再ビルド）: `MOORESTECH_EVENT_MODE=1 MOORESTECH_EVENT_LANGUAGE=german` での単発起動と、同梱の `start-gamescom-loop.command` そのものでの起動の両方で、クリック無しで MainGame の言語選択ゲートへ到達し、カーソル表示を確認した。単発起動では English 選択 → スキット → スキップ後のゲーム画面でカーソル非表示（ロック）も確認した。Player.log に refused は 0 件。新規エラーは無く、既存の毎フレーム NRE（UIStateControl/ThirdPersonController）と CEF の署名検証ログだけだった。
- 起動言語 german は「player already chose a language」で拒否された（開発機に選択済み言語が残っているため。既存挙動）。

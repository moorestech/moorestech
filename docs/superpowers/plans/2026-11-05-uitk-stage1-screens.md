# UITK 段階1 screens レーン（命綱: ポーズ・設定・バグ報告・トースト・CEF 停止の記録と切替） Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** ポーズ一式（トップ・サブページ・切断表示・入れ子ポーズ）・設定 3 種・バグ報告フォーム・トースト層を UITK で作り、所有表でポーズ系を `GameUi` に切り替え、CEF 停止の生の値をバグ報告へ残し、CEF が無くても Esc からセーブ・設定・バグ報告ができる状態を `feature/uitk-s1-screens` に入れる。

**Architecture:** `PauseMenuState`・`PauseMenuStateService` は C# のまま使う。画面の状態（ViewModel と購読モデル）と型付きコマンドは `Client.Ui.State/PauseMenu`・`Client.Ui.State/Settings`・`Client.Ui.State/Toast` に置き、Web の action ハンドラは JSON を解いて同じ型付きコマンドを呼ぶアダプタへ薄くする。描画は `Client.Ui/Screens/{PauseMenu,Settings,BugReport,Toast}` の View（MonoBehaviour 非依存）と、`UiRoutingState` を購読して `UiLayer.PauseMenu` へ載せ降ろしする Presenter。USS は `UiAssetCatalog`（ScriptableObject）1 か所から起動時にロードし、欠けたら起動で止める。CEF 停止の記録は `Client.Game` のインターフェース越しに `Client.WebUiHost` が生の値 3 つを出し、manifest に載せる。

**Tech Stack:** Unity 6000.3.8f1 UI Toolkit、UI Test Framework 1.0.0（`UITestFixture`・`PanelSimulator`）、UniRx、VContainer、UniTask、NUnit、プレイテスト DSL（`Client.Playtest`）

## Requirements

設計の正本は `docs/uitk-migration/plan.md` §2.2（作業〈画面〉・完了条件・出荷）・§3.1〜3.4・§3.8・§3.9・§3.11・§4.2〜4.4・§4.6・§4.9、`.decisions/2026-10-26-UITK*.md`・`2026-10-27-入れ子ポーズ画面はINestedPauseScreenStateで束ねWeb契約は寛容にする.md`、骨組み・theme・input レーンの報告（`moorestech_logs/harness/sdd/feature-uitk-stage1/lane-*-done.md`）。grill は不要（段階計画とユーザー裁定で設計済み）。

- R-1 **型付きコマンドと JSON アダプタの分離（host §3）**: `pause_menu.save`・`pause_menu.save_and_quit`・`pause_menu.show_page`・`bug_report.submit`・`bug_report.resend_failed`・`bug_report.export_unsent`・`settings.applyGraphicsQualityPreset`・`settings.setGraphicsQuality`・`settings.setSprintInputMode`・`localization.setLocale` の判断（空本文・種別・画面名・不明値の拒否、送信後のトップ戻り）を `Client.Ui.State` の型付きコマンド `IPauseMenuCommands`・`ISettingsCommands` へ移す。Web のハンドラは JSON を型へ解き、解けなければ今の失敗コードで拒否し、解けたら型付きコマンドを呼ぶだけにする。受け入れ: 既存の `Client.Tests/WebUi/Settings/*`・`PauseMenuWireContractTest`・`WireContractTest` が緑のまま、型付きコマンドの L0 が緑
- R-2 **画面の状態の型付きスナップショット（§3.2）**: `PauseMenuViewModel`（ページ・切断・確保状態・配送状況・書き出し状況）と `SettingsViewModel`（画質の設定値・プリセット〈一致しなければ Custom〉・画質の復旧印・ダッシュ方式・ダッシュの復旧印・現在言語コード）を不変の sealed class にし、`IReadOnlyReactiveProperty<…>` で配る `PauseMenuModel`・`SettingsModel` を作る。`PauseMenuTopic` は `PauseMenuModel.Current` を購読して JSON を作る形へ寄せる（JSON の形は変えない）。受け入れ: L0 で源の変化が ViewModel に届く、wire の契約テストが緑
- R-3 **ポーズの View（§2.2 対象）**: `GamePanel` の中にトップ（題・切断時の赤字 1 行・セーブ・セーブして終了・設定・バグ報告の 4 ボタン）とサブページ（題・本文だけスクロール・戻る）を出す。ページは `PauseMenuViewModel.Page` だけで決める。ロケール印の要素 `pause-menu-locale-<code>` を持つ。testid（`name`）とアンカー（`TutorialAnchor.Mark`）は Web と同じ綴り（`pause-menu`・`pause-menu-save`・`pause-menu-save-and-quit`・`pause-menu-open-settings`・`pause-menu-open-bug-report`・`pause-menu-back`・`pause-menu-body`、アンカーは `AnchorIds` の pause 系）。受け入れ: L1（Web の `e2e/tests/system/pauseMenu.spec.ts` のテスト名を写した NUnit）
- R-4 **設定 3 種の View**: 言語（縦の ModeSwitch。`LanguageCatalog.Languages` の順。testid `language-select-option-<code>`）、ダッシュ方式（横の ModeSwitch。`sprint-mode-option-toggle/hold`。復旧印の 1 行）、画質（プリセット high/medium/low〈Custom のときは無選択＋`graphics-preset-custom` の 1 行〉・描画距離 300/1000/5000・影・解像度 100/75/50・SSAO・ポストプロセス・エフェクト・マップオブジェクト・地形の細かさ fine/coarse・海。testid は Web の `GraphicsQualitySettings.tsx` と同じ。復旧印 `graphics-recovered`）。受け入れ: L1（選ぶと型付きコマンドが記録される・現在値が選択表示になる）
- R-5 **バグ報告フォーム**: 種別（横の ModeSwitch `bug-report-kind-bug/feedback`）・本文（`GameTextField("bug-report-description", multiline: true)`）・状態の 1 行（`bug-report-status`。送信中＞確保状態の理由＞欠損一覧の順で 1 つだけ）・配送状況の 1 行（`bug-report-delivery`。Web の `describeDelivery`＋`describeFailedDelivery` と同じ文）・救出操作（`bug-report-resend-failed`・`bug-report-export-unsent`・`bug-report-export-status`）・送信（`bug-report-send`。本文が空白だけ・確保状態が ready でない・送信中は押せない。種別で文言が変わる）。下書き（本文と種別）は Presenter が持ち、ページを行き来しても残り、ポーズを開き直すと空に戻り、送信が受理されたら空と bug に戻る。送信中と確保状態 submitting の間は種別と本文を触れない。受け入れ: L0（文の組み立て）と L1（Web の `BugReportForm`・`BugReportRecoveryControls` の vitest と `feedbackReportLabels.spec.ts` のテスト名を写す）
- R-6 **トースト層（§2.2 対象）**: `UiLayer.Toast` に右下へ積むトースト（info と error の 2 種。3 秒で消える。`toast-host`）。出すのは (a) バグ報告の結果（欠損なしなら種別ごとの送信完了 info、欠損ありなら欠損一覧の error）と (b) 型付きコマンドの良性でない拒否（`UiCommandResult.Rejected` を受けた View が出す。文は `<コマンド名> failed: <コード>` を Web と同じ形で）。通信失敗のトーストは Web に残す。受け入れ: L1（積まれる・3 秒で消える〈パネルの時間を進める〉・種別のクラス）
- R-7 **載せ降ろしと切替（R1・R2）**: Presenter は `UiRoutingState.Current` を購読し、画面が `PauseMenu`・`TrainPause`・`SkitPause` のいずれかで `UiOwnershipTable` の `MainPanel` の所有者が `GameUi` のときポーズの View（起動時に 1 回だけ `UiLayer.PauseMenu` に載せたもの）を表示し、それ以外で隠す。所有表の 3 行を `GameUi` にする。台帳 `docs/uitk/TODO.md` の束 1 を `切替済み`、行 pauseMenu・settings・toast の所有者を `UITK` にする。受け入れ: L3（Esc で UITK のポーズが出て CEF の RawImage が非表示・閉じると戻る、入れ子 2 経路で同じ）、G1 が違反なし
- R-8 **CEF 停止の記録（§3.11）**: (1) manifest に生の値 3 つ `webUiHealth { hostStartup, cefProcess, secondsSinceLastWebMessage }` を入れる（`schemaVersion` を 6 へ）。`hostStartup` は `"started" | "failed" | "skippedByEditorSwitch" | "notAttempted"`、`cefProcess` は cef-unity の `CefRuntime` のイベントから記録した最後の状態 `"notObserved" | "running" | "lostRestarting" | "recoveryFailed"`、`secondsSinceLastWebMessage` は最後の WS 受信からの秒（一度も受信していなければ null）。(2) `WebUiStartup.cs` の起動失敗の LogWarning を LogError にする（Editor 限定スイッチによるスキップは LogWarning のまま）。(3) 最後の WS 受信から 15000ms（`webSocketClient.ts` の値）を超えたら `UiInputExclusivity` は Web 側の pointerOverUi・textInputFocused を無視する。受け入れ: L0（閾値の前後・受信で戻る・休止と独立）、manifest の L0、ラダーで書いた箱の manifest に 3 つが入っている
- R-9 **操作音**: 現行 Web のポーズ・設定・バグ報告は操作音を鳴らしておらず、UI 用の音の資産も無い（実測: `sound.play` の実装 0 件、`Assets` の音は破壊・設置の 5 種だけ）。新しい音は作らない。ボタンを鳴らす口は作らず判断記録に書く（D-9）
- R-10 **様式化（§0 様式化）**: ポーズのボタンは `PanelActionButton`（Web の `shared/ui` の PanelActionButton と同じ語彙: 面・ホバーで明るく。Web に縦帯は無いので付けない）、トーストは GamePanel の配色トークンの小窓（info＝`--color-accent` 系、error＝`--color-danger` 系の左帯）。色はトークンだけ（L2 規約が守る）。半透明トークンは実機で決め、`docs/uitk/translucent-tokens.md` の該当行の「決め方」を書き換える
- R-11 **撮影（§4.6）**: ポーズのトップとバグ報告を 1280×720 と 2560×1080 で撮る（ScreenCapture 系。透かし入り）。share に置き台帳と報告に URL を書く
- R-12 **ラダー 1 `uitk-lifeline`（§4.4）**: Web ホストを起動しない設定（`WebUiHostEditorSkip`）で PlayMode を始め、Esc → 設定で言語を日本語へ → バグ報告で種別バグ・日本語の本文を SetText → 送信（送信先は到達しない URL へ差し替え、本番の受け口へ送らない）→ トースト → セーブ → セーブして終了、を録画付きで通す。シナリオは `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage1/uitk-lifeline.cs`
- R-13 **入れ子ポーズ 2 経路の録画**: スキット中（skitPause）と列車乗車中（trainPause）で Esc → UITK のポーズ → 設定 → 戻る → Esc で閉じる、を録画する（`scenarios/uitk/stage1/uitk-nested-pause-skit.cs`・`uitk-nested-pause-train.cs`）
- R-14 **R1 の実測（骨組みの要裁定）**: CEF を起動した状態でポーズを開き、Web のポーズのボタンがあった位置をクリック・ESC・文字キーを注入して、Web が action を 0 件しか出さないことを数える（`WebSocketMessageDispatcher` の action 受信の数を Editor のログか一時の計数で）。0 件なら cef-unity は変えない。0 件でなければ cef-unity の所有者を確かめ、push できる fork ならフラグを 1 本足してピンを更新、できなければ要裁定
- R-15 **IME の確認（§4.9）**: バグ報告の本文（複数行）で、変換・確定・取消・変換中の ESC で画面が閉じない・確定後の ESC で値が残る・候補窓がキャレット位置・クリックで全選択しない、を Mac（この Mac mini。osascript の合成キー）と Windows（検証機 `ssh winbuild` の配布ビルド。`SendInput`）で確かめる。Windows では CEF がある状態の Web の入力欄（例: ビルドメニューの検索欄）の日本語入力も見る（input D-10）。Retina・全画面での候補窓の位置も見る。手順は bd `moorestech-albof.8` と S9 報告。1 本 1 日で打ち切り、結論を台帳へ
- R-16 **L1 の CI**: このレーンの L1 を含む client の EditMode shard を `gh workflow run run_test.yml --ref feature/uitk-s1-screens` で 1 回流し結果を報告に書く（S13 は input レーンで合格済み。このレーンの L1 が CI で通ることを確かめる）
- やらないこと: 旧 Web の `features/pauseMenu`・`features/settings` とテストの削除（切替の 1 周期後の別 PR）、通信失敗トーストの UITK 化、操作音の新設、UI のゲームパッド操作、moores-code-review・pr-create（D-1）、統合ブランチへの push

## Global Constraints

- AGENTS.md: 1 ファイル 200 行以下・1 ディレクトリ 10 ファイル以下、partial 禁止、`Func<>` 禁止、try-catch 原則禁止、イベントは UniRx（`Action` のイベントを新設しない）、日英 2 行コメント（各 1 行）、`[SerializeField]` は `_` 無し小文字キャメル、単純 getter/setter 禁止（`{ get; private set; }` は可）、デフォルト引数禁止、初期化は `Initialize`、エディタ専用コードは `#if UNITY_EDITOR` でファイル末尾、`.meta` を手で作らない、prefab・シーン・ScriptableObject は `uloop execute-dynamic-code` 経由でだけ変える
- 名前に技術名（Uitk）を入れない。UITK 側を指す語は `GameUi`
- 参照の向き `Client.Game` ← `Client.Ui.State` ← {`Client.Ui`・`Client.WebUiHost`}。`Client.Game` は `Client.Ui*`・`Client.WebUiHost` を参照しない
- 部品の作り方（theme・input レーン）: `GamePanel`・`GameTextField`・`LocalizedLabel`・`UiNumberFormat`・`UiMotionPlayer`・`TutorialAnchor.Mark`・`FakeModel`/`CommandRecorder` を使う。ボタン等は `focusable=false`。色は部品 USS で `--部品-名前: var(--token)` と受け直す（`:root` は子へ継承されない）。UXML の `text=` にリテラルを書かない（文言は `LocalizedLabel` と `LocalizationKeys`）。数値は `UiNumberFormat`
- フォント資産（`Assets/UI Toolkit/GameUi/Fonts/*.asset`）はテストや PlayMode で字形が足されて汚れる。コミット前に theme plan（`docs/superpowers/plans/2026-11-05-uitk-stage1-theme.md`）Task 3 Step 8 のコマンドで空に戻す
- fail-closed で拒否・無視する経路は理由をログへ出す。黙って続行する行には `silent-ok: <理由> / <誰がどこで気づくか>` を書く
- テストは `--filter-type regex` で絞る。`Client.Tests.Playtest` 系を選ぶ広い正規表現は禁止（本番送信テストを踏む）。録画シナリオでバグ報告を送るときは、PlayMode を起動する前に Editor プロセスの `MOORESTECH_PLAYTEST_RECEIVER_BASE` を到達しない URL（`http://127.0.0.1:9`）にする（受け口 URL は DI 登録時に 1 回だけ読まれるので、シナリオの中で立てても効かない。2026-11-06 実測で本番へ 1 件届いた）。実 outbox へ書くテスト（`BugReportBundleWriteTest` 等）を流した後は、開発者トークン経路の PlayMode 起動前に outbox の未送信を確かめる
- `.cs` を変えたら `uloop compile --project-path ./moorestech_client`
- EditModeInPlayingTest には `[Category("CiShardClientPlay1")]` を付ける
- バックグラウンドのポーリングで待たず、コマンドは前景で実行する。区切りごとにコミットし、`feature/uitk-s1-screens` を push する。統合ブランチへは push しない。PR は作らない
- Unity Editor はこの worktree の 1 本だけを使う。他の Editor を起動・停止しない
- `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` の未コミット変更（Editor の書き換え）はコミットしない
- 共有ファイル（層 enum・所有表・composition root・Tokens.uss・AnchorIds・DSL のロケータ本体）は拡張点の範囲でだけ足す

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反3件・修正3件 / ポーズは起動時 1 回マウントと表示切替へ・コマンド結果を理由 enum＋良性へ・死活表を追加
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強5・弱5・第3バケツ4 / 質問禁止のため本体が裁定し D-17〜D-25 に記録（結果型の判別・CEF 状態の enum・traits 表へ委譲・ADR 0069 改訂・無応答印の書き手 1 本・Crash の拒否は採用、Lookup/Mutation 分割は据え置き）

## 死活表（Phase 2.5。ポーズ系を GameUi に切り替え、ポーズ中は Web 全面オフ〈R1〉になることでぶら下がる操作）

| 操作 | 計画後 | 根拠 |
|---|---|---|
| Esc でポーズを開く・閉じる・子画面から戻る | 生きる | `PauseMenuStateService.HandleCloseKey` を変えない（D-4） |
| セーブ・セーブして終了・設定 3 種・バグ報告の送信・救出 | 生きる | 型付きコマンドへ移し UITK と Web の両方が呼ぶ（R-1） |
| スキット中・乗車中のポーズ | 生きる | traits 表の PauseMenu 列で表示（Task 6）、録画で確かめる（R-13） |
| ポーズ中の Web の HUD（ホットバー・装備 HUD・列車 HUD）とスキットの表示 | ポーズ中だけ消える | 裁定 `2026-10-26-UITKの主パネル表示中はWeb-UIを全面的にオフにする.md` と §2.2 出荷欄で決着済み |
| ポーズ中に届いたサーバー通知・チュートリアル枠・保留中のモーダル | 退化しうる（通知は Web が外れている間に流れると失われる可能性。推測） | 同じ R1 の裁定の帰結。Task 8 Step 3 で「ポーズ中に通知を 1 件流し、閉じた後に出るか」を実測し、失われるなら要裁定に書く（質問禁止のため実装は止めない） |
| 通信失敗のトースト | ポーズ中は出ない（Web が外れている） | §2.2「通信失敗のトーストは Web に残す」。UITK のコマンドは WS を通らないので通信失敗が起きない |

---

## File Structure

パスは `moorestech_client/Assets/Scripts/` 相対（docs・skills・USS 資産を除く）。

| ファイル | 責務 |
|---|---|
| `Client.Ui.State/Commands/UiCommandResult.cs` | 型付きコマンドの結果（Accepted／Rejected(code)）。Web の `ActionResult` へも写す |
| `Client.Ui.State/PauseMenu/IPauseMenuCommands.cs`・`PauseMenuCommands.cs` | ポーズとバグ報告の型付きコマンド（判断の本体） |
| `Client.Ui.State/PauseMenu/PauseMenuViewModel.cs`・`PauseMenuModel.cs` | ポーズの不変スナップショットと購読モデル |
| `Client.Ui.State/PauseMenu/BugReportFormText.cs` | 状態行・配送行・書き出し行を (キー, 引数) へ組む純関数 |
| `Client.Ui.State/Settings/ISettingsCommands.cs`・`SettingsCommands.cs`・`SettingsViewModel.cs`・`SettingsModel.cs` | 設定 3 種のコマンドとスナップショット |
| `Client.Ui.State/Toast/GameUiToast.cs`・`GameUiToastFeed.cs` | トーストの 1 件と、出す口（Subject） |
| `Client.WebUiHost/Game/Actions/PauseMenuActions.cs`・`BugReport/*.cs`・`Settings/*.cs`・`Localization/LocalizationActions.cs` | JSON→型付きコマンドのアダプタへ薄くする |
| `Client.WebUiHost/Game/Topics/C2/PauseMenuTopic.cs` | `PauseMenuModel` 購読へ寄せる |
| `Client.Ui/Foundation/Assets/UiAssetCatalog.cs`・`Assets/UI Toolkit/GameUi/GameUiAssetCatalog.asset` | USS の 1 か所のロード口（欠けたら起動で止める） |
| `Client.Ui/Components/PanelActionButton/*`・`Components/ModeSwitch/*`・`Components/Toast/*` | 部品（ボタン・択一・トーストの 1 件） |
| `Client.Ui/Screens/PauseMenu/*` | ポーズの View（パネル・トップ・サブページ）と Presenter（載せ降ろし） |
| `Client.Ui/Screens/Settings/*` | 言語・ダッシュ・画質の View |
| `Client.Ui/Screens/BugReport/*` | フォーム・下書き・救出操作の View |
| `Client.Ui/Screens/Toast/*` | トースト層の View と Presenter |
| `Client.Game/InGame/BugReport/WebUiHealth/IWebUiHealthProbe.cs`・`WebUiHealthReading.cs` | manifest へ載せる生の値の型と読み口 |
| `Client.WebUiHost/Health/WebUiHealthRecorder.cs`・`WebUiHealthProbe.cs` | 起動の成否・CEF のイベント・最後の受信を記録して答える |
| `Client.Input/UiInputExclusivity.cs` | Web の値の無応答時の無視（受信時刻・閾値・判定） |
| `Client.WebUiHost/Boot/WebUiSilenceWatcher.cs` | 1 秒ごとに無応答を判定させる（タイマー） |
| `Client.Ui.State/Routing/UiOwnershipTable.cs` | ポーズ系 3 行を GameUi |
| `Client.Starter/Registration/MainGameUiRegistration.cs` | 画面の登録 |
| `Client.Tests/Ui/Screens/**`・`Client.Tests/Ui/State/**`・`Client.Tests/EditModeInPlayingTest/Ui/PauseMenuGameUiTest.cs` | L0・L1・L3 |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/*.cs` | ラダーと入れ子ポーズの録画 |

---

### Task 1: 型付きコマンド・画面の状態・JSON アダプタ（R-1・R-2）

**Files:**
- Create: `Client.Ui.State/Commands/UiCommandResult.cs`、`Client.Ui.State/PauseMenu/{IPauseMenuCommands,PauseMenuCommands,PauseMenuViewModel,PauseMenuModel}.cs`、`Client.Ui.State/Settings/{ISettingsCommands,SettingsCommands,SettingsViewModel,SettingsModel}.cs`、`Client.Ui.State/Toast/{GameUiToast,GameUiToastFeed}.cs`
- Modify: `Client.Ui.State/Client.Ui.State.asmdef`（`Client.PlaytestReceiver` と `Client.Input` を参照に足す）、`docs/adr/0069-*.md`（送信後のトップ戻りの置き場を 1 行直す）、`Client.WebUiHost/Game/Actions/PauseMenuActions.cs`、`Client.WebUiHost/Game/Actions/BugReport/BugReportSubmitActionHandler.cs`・`BugReportRecoveryActions.cs`、`Client.WebUiHost/Game/Actions/Settings/GraphicsQualityActions.cs`・`SettingsActions.cs`、`Client.WebUiHost/Game/Actions/Localization/LocalizationActions.cs`、ハンドラを登録している箇所（`grep -rn "PauseMenuSaveActionHandler\|SettingsActions.Register\|LocalizationActions.Register" Client.WebUiHost Client.Starter`）、`Client.WebUiHost/Game/Topics/C2/PauseMenuTopic.cs`、DI 登録（`Client.Starter/Registration/MainGameUiRegistration.cs`）
- Test: `Client.Tests/Ui/State/PauseMenuCommandsTest.cs`、`SettingsCommandsTest.cs`、`PauseMenuModelTest.cs`、`SettingsModelTest.cs`

**Interfaces:**
- Produces（`namespace Client.Ui.State.Commands`）: `public abstract class UiCommandResult`、`public sealed class UiCommandAccepted : UiCommandResult { public static readonly UiCommandAccepted Instance; }`、`public sealed class UiCommandRejected : UiCommandResult { public UiCommandRejectReason Reason { get; } public string Detail { get; } }`（Detail は submitter の失敗コード等の補足。無ければ空文字）、`public enum UiCommandRejectReason { EmptyDescription, UnsubmittableReportKind, CaptureRefused, BundleWriteFailed, UnknownLanguage }`、`public static class UiCommandRejectReasonTraits { public static bool IsBenign(UiCommandRejectReason reason); public static string ToWireCode(UiCommandRejectReason reason); }`（§3.2「良性の拒否は理由の性質として enum に持つ」。今の拒否はどれも良性でない＝false。wire コードは今の Web の失敗コード `empty_description`・`invalid_kind`・`unknown_locale`。CaptureRefused・BundleWriteFailed はアダプタが Detail〈submitter の FailureCode〉をそのまま返す）
- Produces（`namespace Client.Ui.State.PauseMenu`）:
  - `public interface IPauseMenuCommands { void Save(); void SaveAndQuit(); void ShowPage(PauseMenuPage page); UniTask<UiCommandResult> SubmitBugReport(string description, PlaytestReportKind kind); void ResendFailedReports(); void ExportUnsentReports(); }`
  - `public sealed class PauseMenuCommands : IPauseMenuCommands`（ctor: `GameSaveRequester, SaveAndQuitPresenter, PauseMenuStateService, BugReportSubmitter, PlaytestOutboxRecovery`）。`SubmitBugReport` は本文を Trim し空なら LogWarning＋`EmptyDescription`、種別が Bug・Feedback 以外（Crash 等）なら LogWarning＋`UnsubmittableReportKind`（種別の網羅は `switch` で行い、未知の値は default で拒否）、submitter が拒否したら `CaptureRefused`／`BundleWriteFailed`（Detail に FailureCode）、受理なら `ShowPage(Top)` して `UiCommandAccepted.Instance`（`BugReportSubmitActionHandler` の今の判断をそのまま移す。ADR 0069 の「トップへ戻すのはハンドラ」を「型付きコマンド `PauseMenuCommands.SubmitBugReport`」へ 1 行直す）
  - `public sealed class PauseMenuViewModel { PauseMenuPage Page; bool Disconnected; BugReportCaptureStatus Capture; PlaytestDeliveryStatus Delivery; PlaytestExportStatus Export; }`（すべて `{ get; }`、ctor で受ける）
  - `public sealed class PauseMenuModel : IInitializable { IReadOnlyReactiveProperty<PauseMenuViewModel> Current; IObservable<Unit> OnPauseMenuOpened; }`（ctor: `NetworkDisconnectState, BugReportCaptureSession, PauseMenuStateService`。源は `PauseMenuTopic` の今の購読 5 本と同じ）
- Produces（`namespace Client.Ui.State.Settings`）:
  - `public interface ISettingsCommands { void ApplyGraphicsPreset(GraphicsQualityPreset preset); void SetGraphicsQuality(GraphicsQualitySettings settings); void SetSprintInputMode(SprintInputMode mode); UiCommandResult SetLanguage(string languageCode); }`（`SetLanguage` は `Localize.TrySetChosenLanguage` の偽を `UnknownLanguage` にする）
  - `public sealed class SettingsViewModel { GraphicsQualitySettings Graphics; GraphicsQualityPresetChoice Preset; bool GraphicsRecovered; SprintInputMode SprintMode; bool SprintRecovered; string LanguageCode; }`、`public enum GraphicsQualityPresetChoice { High, Medium, Low, Custom }`
  - `public sealed class SettingsModel : IInitializable { IReadOnlyReactiveProperty<SettingsViewModel> Current; }`（`GraphicsQualityPreference.OnSettingsChanged`・`SprintInputModePreference.OnSprintInputModeChanged`・`Localize.OnLanguageChanged` を購読）
- Produces（`namespace Client.Ui.State.Toast`）: `public enum GameUiToastVariant { Info, Error }`、`public sealed class GameUiToast { string Text; GameUiToastVariant Variant; }`、`public sealed class GameUiToastFeed { IObservable<GameUiToast> OnToast; void Show(string text, GameUiToastVariant variant); void ShowRejected(string commandName, UiCommandRejected rejected); }`（`ShowRejected` は `IsBenign` なら LogWarning だけ、そうでなければ `"<commandName> failed: <wire コードか Detail>"` の error を出し、同じ文を LogWarning にも出す）

- [ ] **Step 1: 型とコマンドを書く** — 上の Produces のとおり。`PauseMenuCommands` の各メソッドは今の Web ハンドラの本体を移す（`PauseMenuSaveActionHandler`→`Save`、`PauseMenuSaveAndQuitActionHandler`→`SaveAndQuit`、`BugReportSubmitActionHandler`→`SubmitBugReport`、`BugReport*RecoveryActions`→`ResendFailedReports`/`ExportUnsentReports`）。`SettingsCommands` は `GraphicsQualityPreference.ApplyPreset`・`SetSettings`・`SprintInputModePreference.SetMode`・`Localize.TrySetChosenLanguage`
- [ ] **Step 2: アダプタへ薄くする** — 各 Web ハンドラは ctor で `IPauseMenuCommands`／`ISettingsCommands` を受け、JSON を型へ解いて呼ぶだけにする。種別は `PlaytestReportKindText.TryParseSubmittableFromPauseMenu`、画面名は `PauseMenuPageContract.TryParse`、画質は `GraphicsQualityWire` のまま（解けなければ今の失敗コード）。`UiCommandResult` は `UiCommandAccepted` なら `ActionResult.Success()`、`UiCommandRejected` なら `ActionResult.Fail(<wire コードか Detail>)` で写す（今の失敗コードを保つ）。依存の無い static 登録（`SettingsActions.Register(hub)`・`LocalizationActions.Register(hub)`）は引数にコマンドを足して呼び出し側を直す（デフォルト引数禁止）
- [ ] **Step 3: モデルを書き、`PauseMenuTopic` を寄せる** — `PauseMenuTopic` は `PauseMenuModel.Current.Skip(1)` を購読して Publish し、`BuildJson` は `PauseMenuModel.Current.Value` から作る（JSON は不変）
- [ ] **Step 4: DI** — `MainGameUiRegistration.Register` に `builder.Register<PauseMenuCommands>(Lifetime.Singleton).As<IPauseMenuCommands>()`、`SettingsCommands`、`builder.RegisterEntryPoint<PauseMenuModel>().AsSelf()`、`SettingsModel` 同様、`builder.Register<GameUiToastFeed>(Lifetime.Singleton)` を足す。WebUiHost のハンドラの生成元が DI でなければ、生成元へ resolver から渡す（`PauseMenuTopic` の生成の前例に合わせる）
- [ ] **Step 5: L0 を書く** — `PauseMenuCommandsTest`: 空白だけの本文は `EmptyDescription` でトップへ戻らない／Crash 種別は `UnsubmittableReportKind`／`StepBackOnCloseKey` の前例と同じく `PauseMenuStateService` を本物で使い、`ShowPage` が `CurrentPage` を変える。submitter を偽にできない場合は空本文と ShowPage だけにし、送信の受理経路は Task 6 の L3 とラダーで確かめる（判断記録に残す）。`SettingsCommandsTest`: 不明言語コードは `UnknownLanguage`。`SettingsModelTest`: `SprintInputModePreference.SetMode` で `Current.Value.SprintMode` が変わる（TearDown で元に戻す）。`PauseMenuModelTest`: `ShowPage(Settings)` で `Current.Value.Page` が Settings
- [ ] **Step 6: compile とテスト** — `uloop compile --project-path ./moorestech_client`。`uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.Ui\.State\.|Client\.Tests\.WebUi\.Settings\.|PauseMenuWireContractTest|WireContractTest|BugReportSubmit"`
- [ ] **Step 7: コミットして push** — `Task 1: ポーズ・バグ報告・設定の判断を型付きコマンドへ移し Web のハンドラを JSON アダプタにする`

### Task 2: バグ報告の文の組み立て（R-5 の文）

**Files:**
- Create: `Client.Ui.State/PauseMenu/BugReportFormText.cs`、`Client.Ui.State/PauseMenu/UiText.cs`
- Test: `Client.Tests/Ui/State/BugReportFormTextTest.cs`

**Interfaces:**
- Consumes: Task 1 の `PauseMenuViewModel`
- Produces:
  - `public sealed class UiText { LocalizationKey Key; IReadOnlyList<LocalizationTextParam> Params; string Resolve(); }`（`Resolve` は `Localize.Get`/`GetFormatted`。文を連結する箇所は Resolve 後の文字列で連結する）
  - `public static class BugReportFormText`:
    - `UiText DescribeStatus(BugReportCaptureStatus capture, bool sending, PlaytestReportKind draftKind)` — 送信中は種別の sending、`noSession`→`Ui.BugReport.NoSession`、`capturing`→`CapturePending`、`submitting`→種別の sending、`ready`→null
    - `UiText DescribeMissing(BugReportCaptureStatus capture)` — 欠損 0 なら null、あれば `Ui.BugReport.Missing { items = ", " 連結 }`
    - `string DescribeDelivery(PlaytestDeliveryStatus delivery)` — Web の `describeDelivery()`＋`describeFailedDelivery()` を写した 1 行（`allSent`・在庫不明の 3 分岐・件数既知の 3 分岐・失敗件数の追記）
    - `string DescribeExport(PlaytestExportStatus export)` — Web の `describeExport()`（idle は null）
    - `bool ShowResend(PlaytestDeliveryStatus delivery)`・`bool ShowExport(PlaytestDeliveryStatus delivery)` — Web の `BugReportRecoveryControls` の判定
    - `LocalizationKey SendLabel(PlaytestReportKind kind)`・`LocalizationKey SentToast(PlaytestReportKind kind)` — 感想は `Ui.FeedbackReport.*`、バグは `Ui.BugReport.*`

- [ ] **Step 1: キーを確かめる** — 使うキーが元 CSV `Localization/localization.csv`（repo ルート）に実在するかを `git grep -n "ui.bugReport\|ui.feedbackReport\|ui.pauseMenu\|ui.settings\|ui.playtest" -- Localization/localization.csv` で確かめる。C# の名前は `ui.bugReport.noSession` → `LocalizationKeys.Ui.BugReport.NoSession`（段ごとに PascalCase）
- [ ] **Step 2: 書く** — 上の Produces のとおり、Web の `BugReportForm.tsx`・`BugReportRecoveryControls.tsx` の分岐をそのまま写す（ローカライズの引数名も Web の `{ count }`・`{ reason }`・`{ items }`・`{ path }` と同じ）
- [ ] **Step 3: L0 を書く** — Web の `moorestech_web/webui/src/features/pauseMenu/*.test.tsx` と `e2e/tests/system/pauseMenu.spec.ts` の配送・救出のテスト名を NUnit 名に写し（日本語のメソッド名可）、各分岐で (キー, 引数) を確かめる
- [ ] **Step 4: compile とテスト** — `--filter-value "BugReportFormTextTest"`
- [ ] **Step 5: コミットして push** — `Task 2: バグ報告の状態・配送・書き出しの文を Web と同じ分岐で組む純関数を足す`

### Task 3: 部品と USS のロード口（PanelActionButton・ModeSwitch・トーストの 1 件・UiAssetCatalog）

**Files:**
- Create: `Client.Ui/Foundation/Assets/UiAssetCatalog.cs`、`Client.Ui/Components/PanelActionButton/{PanelActionButton.cs,PanelActionButton.uss}`、`Client.Ui/Components/ModeSwitch/{ModeSwitch.cs,ModeSwitchOption.cs,ModeSwitch.uss}`、`Client.Ui/Components/Toast/{ToastItem.cs,Toast.uss}`、資産 `Assets/UI Toolkit/GameUi/GameUiAssetCatalog.asset`（uloop で作る）
- Modify: `Client.Ui/Foundation/Root/GameUiRoot.cs`（`[SerializeField] private UiAssetCatalog assetCatalog;` と `public UiAssetCatalog AssetCatalog => assetCatalog;`、Awake の必須検査へ足す）、`MainGameUI.prefab` の `GameUiRoot`（uloop で参照を入れる）
- Test: `Client.Tests/Ui/Components/PanelActionButtonTest.cs`、`ModeSwitchTest.cs`、`Client.Tests/Ui/Assets/UiAssetCatalogTest.cs`

**Interfaces:**
- Produces:
  - `public sealed class UiAssetCatalog : ScriptableObject`（`[CreateAssetMenu]` は付けない）。`[SerializeField] private StyleSheet gamePanel, panelActionButton, modeSwitch, toast;`（画面の `pauseMenu`・`settings`・`bugReport` は Task 4 が USS と一緒に足す） と同名の読み出し（`public StyleSheet GamePanel => gamePanel;` 等。素通しの読み出しは「単純 getter 禁止」の例外としてシリアライズ値の公開に限る＝前例は D-24）。`public void ValidateAllAssigned()` が欠けた名前を全部並べて `InvalidOperationException`
  - `public sealed class PanelActionButton : Button`（ctor `(string testid, LocalizationKey label, StyleSheet sheet)`、`focusable=false`、ラベルは `LocalizedLabel`、`clicked` に購読）
  - `public sealed class ModeSwitch<TValue> : VisualElement`（ctor `(string testid, ModeSwitchOrientation orientation, IReadOnlyList<ModeSwitchOption<TValue>> options, StyleSheet sheet)`。`IObservable<TValue> OnSelected`、`void SetSelected(TValue value)`（一致しなければ無選択）、`void SetSelectedNone()`、`void SetEnabledAll(bool enabled)`。選択は `--selected` クラス。各選択肢は `focusable=false` の Button で `name=option.Testid`）
  - `public sealed class ModeSwitchOption<TValue> { string Testid; TValue Value; LocalizationKey? LabelKey; string LiteralLabel; }`（ラベルはキーか、言語名・`300m` のような翻訳しない値の文字列のどちらか 1 つ。両方・どちらも無しは ctor で例外）
  - `public enum ModeSwitchOrientation { Horizontal, Vertical }`
  - `public sealed class ToastItem : VisualElement`（ctor `(GameUiToast toast, StyleSheet sheet)`。クラス `toast-item--info`／`toast-item--error`）

- [ ] **Step 1: 部品を書く** — USS の色は部品 USS の先頭で `--panel-action-button-face: var(--…)` のように受け直す（`Tokens.uss`・`ControlTokens.uss` から選ぶ。Web の `shared/ui/PanelActionButton` と `ModeSwitch` の CSS が使うトークンを写す）
- [ ] **Step 2: カタログ資産を作る** — `uloop execute-dynamic-code` で `ScriptableObject.CreateInstance<UiAssetCatalog>()` を `Assets/UI Toolkit/GameUi/GameUiAssetCatalog.asset` に保存し、`SerializedObject` で各 StyleSheet（この時点で存在するもの。画面の USS は Task 4・5 で足すたびに同じ方法で入れる）を入れ、`MainGameUI.prefab` の `GameUiRoot.assetCatalog` に入れて `PrefabUtility.SavePrefabAsset`。Awake は `assetCatalog == null` と `ValidateAllAssigned()` で止める
- [ ] **Step 3: L1・L2 を書く** — `PanelActionButtonTest`（クリックで 1 回・Space/Enter で押されない〈input の規則を付けたルートで〉・`focusable` false）、`ModeSwitchTest`（選ぶと OnSelected・`SetSelected` で `--selected` が 1 つ・無選択・無効で押しても流れない・縦横のクラス）、`UiAssetCatalogTest`（資産をロードして `ValidateAllAssigned` が通る＝L2。全フィールドの参照先が実在）
- [ ] **Step 4: compile とテスト** — `--filter-value "Client\.Tests\.Ui\."`
- [ ] **Step 5: コミットして push** — `Task 3: PanelActionButton・ModeSwitch・トーストの部品と USS のロード口 UiAssetCatalog を足す`

### Task 4: ポーズ・設定・バグ報告の View（R-3・R-4・R-5・R-10）

**Files:**
- Create: `Client.Ui/Screens/PauseMenu/{PauseMenuView.cs,PauseMenuTopPageView.cs,PauseMenuSubPageView.cs,PauseMenu.uss}`、`Client.Ui/Screens/Settings/{LanguageSelectView.cs,SprintModeSelectView.cs,GraphicsQualitySettingsView.cs,GraphicsQualityItemRows.cs,Settings.uss}`、`Client.Ui/Screens/BugReport/{BugReportFormView.cs,BugReportDraft.cs,BugReportRecoveryControlsView.cs,BugReport.uss}`
- Modify: `Client.Ui/Foundation/Assets/UiAssetCatalog.cs`（`pauseMenu`・`settings`・`bugReport` のフィールドと読み出し・`ValidateAllAssigned` への追加）、`GameUiAssetCatalog.asset`（uloop で 3 つの USS を入れる）
- Test: `Client.Tests/Ui/Screens/PauseMenu/{PauseMenuViewTest.cs,FakePauseMenuModel.cs,RecordingPauseMenuCommands.cs}`、`Client.Tests/Ui/Screens/Settings/{SettingsViewTest.cs,FakeSettingsModel.cs,RecordingSettingsCommands.cs}`、`Client.Tests/Ui/Screens/BugReport/BugReportFormViewTest.cs`

**Interfaces:**
- Consumes: Task 1 の ViewModel・コマンド・`GameUiToastFeed`、Task 2 の `BugReportFormText`、Task 3 の部品と `UiAssetCatalog`
- Produces:
  - `public sealed class PauseMenuView : VisualElement`（ctor `(IReadOnlyReactiveProperty<PauseMenuViewModel> model, IReadOnlyReactiveProperty<SettingsViewModel> settings, IPauseMenuCommands commands, ISettingsCommands settingsCommands, GameUiToastFeed toasts, BugReportDraft draft, UiAssetCatalog catalog)`。PauseMenuView 自身は層いっぱいの透明な置き場で、testid `name="pause-menu"` と `TutorialAnchor.Mark(…, AnchorIds.PauseMenu)` は中の見える枠（GamePanel）に付ける（Web も見える枠に testid。PauseMenuView 自身に同じ名前を付けない）（実際の定数名は `AnchorIds.cs` を見る）。購読はパネルに載っている間だけ張り（`AttachToPanelEvent`／`DetachFromPanelEvent`）、ページの変化でトップ／設定／バグ報告を作り直す）
  - `public sealed class BugReportDraft { string Description { get; private set; } PlaytestReportKind Kind { get; private set; } void SetDescription(string); void SetKind(PlaytestReportKind); void Reset(); }`（Reset は空・Bug）
  - `BugReportFormView` は送信時に `missing` と種別を押した時点で控え、`await commands.SubmitBugReport(...)` の結果が Accepted なら控えた欠損 0 で `SentToast(kind)` の info、あれば `Missing` の error を `toasts.Show`、`draft.Reset()`。Rejected なら `toasts.ShowRejected("bug_report.submit", outcome)` で下書きは残す。二度押しは送信中フラグで止める

- [ ] **Step 1: View を書く** — Web の `PauseMenuPanel.tsx`・`PauseMenuTopPage.tsx`・`PauseMenuSubPage.tsx`・`features/settings/*.tsx`・`BugReportForm.tsx`・`BugReportRecoveryControls.tsx` を写す。サブページの本文は `ScrollView`（`name="pause-menu-body"`、縦だけ）。切断の 1 行は `--color-danger` 系トークン。ロケール印は `LocalizedLabel` の言語変化で `pause-menu-locale-<code>` の要素の name を差し替える（DSL が `UntilUiElement("pause-menu-locale-japanese")` で待てる形）。設定の選択時は `settingsCommands` を呼び、`SetLanguage` が Rejected なら `ShowRejected("localization.setLocale", …)`。画質の各項目は `GraphicsQualityItemRows` に表で持ち、変更は「今の `SettingsViewModel.Graphics` の 1 項目だけ差し替えた `GraphicsQualitySettings`」を `SetGraphicsQuality` へ渡す
- [ ] **Step 2: USS とカタログ** — 画面 USS も色は受け直し。GamePanel の幅は Web の `style.module.css` の `.panel`／`.page` の寸法トークンを写す。カタログへ uloop で足す
- [ ] **Step 3: L1 を書く** — `FakePauseMenuModel : FakeModel<PauseMenuViewModel>`、`RecordingPauseMenuCommands : CommandRecorder<PauseMenuCommandRecord>, IPauseMenuCommands`（`SubmitBugReport` はテストが決めた結果を返す）、Settings も同様。Web の `e2e/tests/system/pauseMenu.spec.ts`・`pauseMenuSettingsLayout.spec.ts`・`feedbackReportLabels.spec.ts` と vitest のテスト名を写す。最低限: トップの 4 ボタンが各コマンドを 1 回出す／切断で赤字 1 行／ページ切替で戻るボタン・本文スクロール／設定で言語・ダッシュ・画質の選択がコマンドになる・Custom で無選択と 1 行／バグ報告で空本文は送信不可・ready 以外は送信不可・送信中は二度押し不可・受理でトースト info と下書き初期化・欠損ありで error・拒否で下書きが残る・感想を選ぶと送信ボタンの文言が感想・救出ボタンの出し分け。言語の文言はテスト内で `Localize` の現在言語に依存しないよう、キーと引数で比べるか英語に固定して TearDown で戻す
- [ ] **Step 4: compile とテスト** — `--filter-value "Client\.Tests\.Ui\.Screens\."`、加えて `Client\.Tests\.Ui\.Conventions\.`（L2 規約）
- [ ] **Step 5: フォント資産を空に戻し、コミットして push** — `Task 4: ポーズ・設定・バグ報告の View を GamePanel と部品で作る`

### Task 5: トースト層（R-6）

**Files:**
- Create: `Client.Ui/Screens/Toast/{ToastHostView.cs,ToastLayerPresenter.cs}`
- Modify: `MainGameUiRegistration.cs`
- Test: `Client.Tests/Ui/Screens/Toast/ToastHostViewTest.cs`

**Interfaces:**
- Consumes: Task 1 の `GameUiToastFeed`、Task 3 の `ToastItem`、`UiMotionPlayer`
- Produces:
  - `public sealed class ToastHostView : VisualElement`（`name="toast-host"`、右下に縦積み、`pickingMode=Ignore`。`void Push(GameUiToast toast)` で `ToastItem` を足し、`schedule.Execute(remove).StartingIn(3000)` で消す〈パネルの時間で進む。L1 で `simulate.FrameUpdate(ms)` で進められる〉）
  - `public sealed class ToastLayerPresenter : IInitializable, IDisposable`（`GameUiRoot.GetLayer(UiLayer.Toast)` に `ToastHostView` を常駐させ、`GameUiToastFeed.OnToast` を購読して `Push`）

- [ ] **Step 1: 書く** — 出入りは `UiMotionPlayer` の opacity tween（出現 150ms・消える 150ms。Web の Mantine Notification の既定に近い値）。消える前に `UiMotionHandle` の完了で Remove
- [ ] **Step 2: L1** — Web の `toastStore.test.ts`（「emitToast で追加され 3秒後に消える」「variant を保持する」）を写す
- [ ] **Step 3: 登録** — `builder.RegisterEntryPoint<ToastLayerPresenter>()`
- [ ] **Step 4: compile とテスト** — `--filter-value "Client\.Tests\.Ui\.Screens\.Toast\."`
- [ ] **Step 5: コミットして push** — `Task 5: トースト層を UITK に作りバグ報告の結果とコマンド拒否を出す`

### Task 6: 載せ降ろし・所有表の切替・結合確認（R-7・R-10・R-11）

**Files:**
- Create: `Client.Ui/Screens/PauseMenu/PauseMenuScreenPresenter.cs`、`Client.Tests/EditModeInPlayingTest/Ui/PauseMenuGameUiTest.cs`
- Modify: `Client.Ui.State/Routing/UiOwnershipTable.cs`（`PauseMenu`・`TrainPause`・`SkitPause` の `MainPanel` を `GameUi`）、`MainGameUiRegistration.cs`、`Client.Tests/Ui/Routing/UiOwnershipTableTest.cs`、`Client.Tests/EditModeInPlayingTest/Ui/GameUiSkeletonTest.cs`（切替で前提が変わる場合）、`docs/uitk/TODO.md`、`docs/uitk/translucent-tokens.md`
- Test: `UiOwnershipTableTest`、`PauseMenuGameUiTest`

**Interfaces:**
- Consumes: Task 4 の `PauseMenuView`・`BugReportDraft`、骨組みの `UiRoutingState`・`UiOwnershipTable`・`GameUiRoot`
- Produces: `public sealed class PauseMenuScreenPresenter : IInitializable, IDisposable`（§3.2「画面は起動時に 1 回だけマウントし、表示・非表示はルータが切り替える」。Initialize で `UiLayer.PauseMenu` に `PauseMenuView` を 1 回だけ Add し `display: none`。`UiRoutingState.Current` を購読し、`UiScreenTraitsTable.Get(snapshot.Screen).PauseMenu && ownershipTable.GetOwner(snapshot.Screen, UiSurface.MainPanel) == UiSurfaceOwner.GameUi` のとき表示（画面の列挙は traits 表 1 か所に任せ、ここで画面名を並べない）。`PauseMenuView` は `SetShown(bool)` を持ち、非表示の間はモデルの購読を止め、表示するときに最新値で 1 回描いてから購読を張る。`PauseMenuModel.OnPauseMenuOpened` で `BugReportDraft.Reset()`〈開くたびに空から〉。ページの中身（トップ／設定／バグ報告）はページの変化で作り直してよい）

- [ ] **Step 1: Presenter と登録** — `builder.Register<BugReportDraft>(Lifetime.Singleton)`、`builder.RegisterEntryPoint<PauseMenuScreenPresenter>()`
- [ ] **Step 2: 所有表を切り替える** — 3 行を `UiSurfaceOwner.GameUi`。`UiOwnershipTableTest` を直す。`GameUiSkeletonTest` が「全部 Web」を前提にしていたら、ポーズ系は GameUi の前提へ直す（Editor 上書きのテストはそのまま）
- [ ] **Step 3: L3 を書く** — `PauseMenuGameUiTest`（`[Category("CiShardClientPlay1")]`、`EditorBackgroundInputScope` で囲む。前例 `GameUiInputTest`）: ゲーム画面で Esc 注入 → `UIStateEnum.PauseMenu`、`GameUiRoot` の `UiLayer.PauseMenu` に `pause-menu` がある、CEF の RawImage が非表示（`GameUiRoot` の参照を反射せず、`UiRoutingState.Current.Value.WebUiSuspended` と `UiInputExclusivity.IsPointerOverWebUi` が false で見る）、`UiInputExclusivity.SetState(true, true)` を入れても `IsTextInputFocused` false、`pause-menu-open-settings` を DSL の UITK ロケータ（`GameUiElementQuery` の静的関数）で引いてクリック注入 → 設定のページ、Esc → トップ、Esc → ゲーム画面で `pause-menu` が無い・`WebUiSuspended` false、閉じた直後に `IsPointerOverWebUi` と Web 側の `IsTextInputFocused` が false（休止前に入れた true が残らない）
- [ ] **Step 4: 半透明トークンを実機で決める** — PlayMode でポーズを開き、`uloop screenshot` でなく ScreenCapture（theme レーンの撮影手順。`moorestech_logs/harness/sdd/feature-uitk-s1-theme/workspace/` の撮影コード）で 1280×720 と 2560×1080 を撮る。GamePanel の面の濃さが Web の参照キャプチャ（台帳 `## 参照キャプチャ` の 1-pause-top）と比べて読めるかを見て、変えたトークンと理由を `translucent-tokens.md` の「決め方」へ書く。撮影 2 枚は `share-put` して URL を台帳と報告へ
- [ ] **Step 5: 台帳** — `docs/uitk/TODO.md` の束 1 を `切替済み`、行 pauseMenu・settings・toast の所有者を `UITK`。`## agent 既定` に「通信失敗のトーストは Web に残る（toast 行の所有者は UITK だが、Web の toast は撤去まで通信失敗だけを出す）」を 1 行足す。`python3 .github/scripts/check-uitk-web-follow.py --base origin/master --head HEAD` が違反なし
- [ ] **Step 6: compile とテスト** — `--filter-value "Client\.Tests\.Ui\.|UiStateSubStatePublishTest|WireContractTest|PauseMenu"` と、L3 `--filter-value "PauseMenuGameUiTest|GameUiInputTest|GameUiSkeletonTest"`
- [ ] **Step 7: フォント資産を空に戻し、コミットして push** — `Task 6: ポーズ系の所有者を GameUi に切り替え UITK のポーズを載せ降ろしする`

### Task 7: CEF 停止の記録（R-8）

**Files:**
- Create: `Client.Game/InGame/BugReport/WebUiHealth/{IWebUiHealthProbe.cs,WebUiHealthReading.cs}`、`Client.WebUiHost/Health/{WebUiHealthRecorder.cs,WebUiHealthProbe.cs}`、`Client.WebUiHost/Boot/WebUiSilenceWatcher.cs`
- Modify: `Client.Game/InGame/BugReport/BugReportManifest.cs`（`SchemaVersion = 6`、`public WebUiHealthReading WebUiHealth;`）、`Client.Game/InGame/BugReport/BugReportBundleWriter.cs`（ctor に `IWebUiHealthProbe` を足し、`CreateHeader` の直後に `manifest.WebUiHealth = _webUiHealth.Read();`）、`BugReportBundleWriter` をテストで作っている 5 か所（`BugReportSubmitKindTest.cs`・`BugReportBundleWriterIdentityTest.cs` の 3 か所・`DeveloperReportRealReceiverCheck.cs`。偽の probe を渡す）、`Client.Starter/Registration/PlaytestRecordRegistration.cs`（probe の登録）、`Client.Starter/Initialization/Boot/WebUiStartup.cs`（成否を `WebUiHealthRecorder` へ記録・失敗の LogError）、`Client.WebUiHost/Boot/WebSocketMessageDispatcher.cs`（受信ごとに `UiInputExclusivity.RecordWebMessageReceived(Stopwatch.GetTimestamp())`）、`Client.Input/UiInputExclusivity.cs`、cef-unity のイベント購読の置き場（`Client.WebUiHost/Cef/WebUiCefNavigator.cs` の初期化時）
- Test: `Client.Tests/Ui/Input/UiInputExclusivityWebSilenceTest.cs`、`Client.Tests/BugReport/WebUiHealthManifestTest.cs`（置き場は既存の manifest テストの横）

**Interfaces:**
- Produces（`namespace Client.Game.InGame.BugReport.WebUiHealth`）:
  - `public interface IWebUiHealthProbe { WebUiHealthReading Read(); }`
  - `public enum WebUiHostStartupState { NotAttempted, Started, Failed, SkippedByEditorSwitch }`、`public enum CefProcessState { NotObserved, Running, LostRestarting, RecoveryFailed }`
  - `public sealed class WebUiHealthReading { public WebUiHostStartupState HostStartup; public CefProcessState CefProcess; public double? SecondsSinceLastWebMessage; }`（manifest の契約。enum は manifest へ書くときだけ camelCase の文字列にする〈フィールドに `[JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]`〉）
- Produces（`namespace Client.WebUiHost.Health`）:
  - `public static class WebUiHealthRecorder { void RecordHostStartup(WebUiHostStartupState state); void ObserveCefRuntime(); WebUiHostStartupState HostStartup; CefProcessState CefProcess; }`（未記録は `NotAttempted`／`NotObserved`。`ObserveCefRuntime` は `CefRuntime.ServerLost`・`ServerRecovered`・`ServerRecoveryFailed` を購読して状態を書き換え、初回呼び出しで `Running`。状態が変わるたびに Debug.Log（`recoveryFailed` は LogError）。状態は lock で守る〈CEF のイベントのスレッドは保証されないため〉）
  - `public sealed class WebUiHealthProbe : IWebUiHealthProbe`（`WebUiHealthRecorder` と `UiInputExclusivity.SecondsSinceLastWebMessage(Stopwatch.GetTimestamp())` から読む）
- Produces（`Client.Input.UiInputExclusivity` に足す）:
  - `public const double WebSilenceThresholdSeconds = 15.0;`（`webSocketClient.ts` の 15000ms と同じ。参照コメント付き）
  - `public static void RecordWebMessageReceived(long stopwatchTimestamp)`（WS スレッドから。受信時刻を `Interlocked.Exchange` で書くだけ。無応答の印 `_webSilent` は書かない＝印の書き手は `EvaluateWebSilence` 1 本。受信が戻ってから最大 1 秒で印が下りる）
  - `public static double? SecondsSinceLastWebMessage(long nowStopwatchTimestamp)`（一度も受信していなければ null）
  - `public static void EvaluateWebSilence(long nowStopwatchTimestamp)`（受信時刻と現在から `_webSilent` を lock の中で決め直し、論理和を再計算。切り替わったときだけ Log。閾値の 15000ms は `webSocketClient.ts` の pong 無応答で Web が WS を閉じる値で、閉じれば `WebSocketHub` の切断リセットが先に走る。この無視が効くのは JS が止まって WS だけ残る経路〈§3.11「今の問題」〉）
  - `IsPointerOverWebUi` と論理和の再計算は `!_webUiSuspended && !_webSilent` のときだけ Web 側を読む
  - `public sealed class WebUiSilenceWatcher : IInitializable, IDisposable`（`Observable.Interval(TimeSpan.FromSeconds(1))` で `EvaluateWebSilence(Stopwatch.GetTimestamp())`。無応答は「何も来ない」ことなので購読できる変化が無く、時計で見る＝判断記録 D-10）

- [ ] **Step 1: 無応答の無視を書く** — 上のとおり。受信が一度も無いときは無視しない（Web の値も false のまま）。受信が戻ったあと評価前は印が残ることもテストで固定する
- [ ] **Step 2: 生の値を書く** — `WebUiStartup.StartAsync` の各経路で `RecordHostStartup(SkippedByEditorSwitch／Started／Failed)`。失敗 2 か所の `Debug.LogWarning` を `Debug.LogError` にする（コメント「UIはWeb一本なので…」も「命綱は UITK にあるので続行し、失敗は LogError で開発者へ届ける」に直す）。`ObserveCefRuntime` はブラウザ生成の後に 1 回呼ぶ。manifest と writer を直し、writer の生成箇所を全部直す
- [ ] **Step 3: L0** — `UiInputExclusivityWebSilenceTest`: 受信 → 14 秒後に評価で Web の値が効く → 16 秒後に評価で効かない（pointer・text 両方）→ 受信で戻る／一度も受信していなければ無視しない／休止とは独立（休止中に受信が止まっても解除後は無応答なら効かない）。時刻は `Stopwatch.Frequency` から作った値を渡す。`WebUiHealthManifestTest`: 偽の probe の値が `ToJson()` に `webUiHealth.hostStartup: "skippedByEditorSwitch"` 等の綴りで出る・`schemaVersion` 6
- [ ] **Step 4: compile とテスト** — `--filter-value "UiInputExclusivity|WebUiHealth|BugReportManifest|BugReportBundle"`
- [ ] **Step 5: コミットして push** — `Task 7: CEF 停止の生の値 3 つを報告へ残し、Web の無応答時は入力排他の Web 側の値を無視する`

### Task 8: 完了条件の実測（R-12〜R-16）と締め

このタスクは本体（コントローラ）が自分で行う（Editor・検証機・録画を使い、判断が実測で分かれるため）。

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/{uitk-lifeline.cs,uitk-nested-pause-skit.cs,uitk-nested-pause-train.cs,uitk-pause-web-action-count.cs}`
- Modify: `docs/uitk/TODO.md`（`## 実測の結論`）、必要ならシナリオ実行の口（`run-scenario.sh` に Web ホストを起動しない指定が要れば足す）
- 報告: `moorestech_logs/harness/sdd/feature-uitk-stage1/lane-screens-done.md`

- [ ] **Step 1: ラダー `uitk-lifeline`** — PlayMode を止め、`uloop execute-dynamic-code` で `WebUiHostEditorSkip.Apply()` を立ててから run-scenario.sh（run-scenario.sh が停止の遷移で印を消すなら、Web ホストを起動しない指定を run-scenario.sh かシナリオの前処理へ足す）。起動前に Editor プロセスへ `Environment.SetEnvironmentVariable("MOORESTECH_PLAYTEST_RECEIVER_BASE", "http://127.0.0.1:9")`（シナリオは未設定なら送らずに止める）。手順は R-12。DSL の `ClickUi`/`SetText` が UITK ロケータで解決したことをログで確かめる。書かれた箱の `manifest.json` に `webUiHealth` が入り `hostStartup` が `skippedByEditorSwitch` であることを読む。`Record=true` で提出用を撮る
- [ ] **Step 2: 入れ子ポーズ 2 経路の録画** — 既存の `scenarios/misc/skit-pause-menu-esc.cs` と列車の乗車シナリオ（`scenarios/train/`）を土台に R-13
- [ ] **Step 3: R1 の実測（R-14）** — webui の依存を入れて（`pnpm install --frozen-lockfile`。worktree の gitignore 下）CEF を起動した状態で、ポーズ（UITK）中に Web のポーズのボタン位置（Web 版の座標は参照キャプチャか、所有者を Editor 上書きで Web に戻して測る）へクリック・Esc・文字キーを注入し、`[WebSocketHub]` の action 受信を数える。結果で cef-unity の扱いを決める（R-14）
- [ ] **Step 4: IME（R-15）** — Mac: この Mac mini の Editor でバグ報告の本文へ osascript の合成キー（入力ソースの切替は key code 104/102）。Windows: 検証機に配布ビルドを置く手順（`~/hermes-agent/data/HERMES-OPS.md`・`scripts/playtest/`・moores-playtest-release の Windows 検証の節）を読み、このブランチのビルドを Steam へ上げずに検証機で起動し、`SendInput` の合成キーで同じ確認＋CEF がある状態の Web の入力欄。1 日で打ち切り、できなかった段は理由と証拠付きで「不可」と書く
- [ ] **Step 5: CI（R-16）** — `gh workflow run run_test.yml --ref feature/uitk-s1-screens` を流し、終わるまで前景で `gh run watch` し結果を記録
- [ ] **Step 6: 台帳と報告** — `## 実測の結論` に R-14・R-15 と撮影の行を足す。`lane-screens-done.md` に作ったもの・最終コミット・compile・テスト（件数と filter）・実測・要裁定・未完了を書き、moorestech_logs の README に従ってコミット・push

## 判断記録（ADR）

- D-1 このレーンは moores-code-review と pr-create を行わない（段階 0 から続く裁定「各レーンは統合ブランチへ入れ、PR は段階で 1 本」`.decisions/2026-10-26-UITK段階0のPRは1本に統合する.md` の段階 1 への適用と、指揮セッションの依頼文）。最終レビューと PR は指揮セッションが統合後に行う。出所: ユーザー（指揮経由）
- D-2 AskUserQuestion は行わない（依頼文とユーザーの goal 指示）。人にしか決められないものは推奨案で進め、完了報告の「要裁定」に書く。user-simulator の要裁定も同じ扱い。出所: ユーザー
- D-3 配置: ViewModel・購読モデル・型付きコマンドは `Client.Ui.State`（§3.1「画面状態・ViewModel・UI コマンドは Client.Ui.State」）。WebUiHost のアダプタも同じコマンドを呼ぶので、`Client.Ui` に置くと WebUiHost から参照できない。前例: 段階 0 で `Client.Ui.State` へ移した `ModalRequestService`・`NotificationFeed`
- D-4 `PauseMenuStateService`・`PauseMenuState`・`NestedPauseSubStateController` は変えない（§2.2）。ESC はこれまでどおり C# の UIState が `HandleCloseKey` で処理し、入力欄の上の ESC は input レーンのラッチで 1 フレーム止まる
- D-5 testid は Web と同じ綴りにする（§3.12。既存の録画シナリオ〈`pause-menu-hierarchy.cs`・`feedback-report-labels.cs`・`localization-language-switch-via-ui.cs`〉が DSL の UITK ロケータでそのまま動くため）
- D-6 USS のロードは `UiAssetCatalog`（ScriptableObject・GameUiRoot が参照）1 か所（§3.1）。`Resources.Load` は欠けを起動で止められず文字列パスになるので採らない。カタログの読み出しの前例は D-24
- D-7 下書きは DI のシングルトン `BugReportDraft` が持ち、ポーズを開くたび（`OnPauseMenuOpened`）と受理後に空へ戻す。Web は「パネルが外れると消える」だったが、UITK はページの切替で View を作り直すので、View に持たせると設定へ行って戻ると消える
- D-8 トーストを出すのは View（コマンドの結果を受けた側）。型付きコマンドは結果を返すだけで、表示を知らない（Web の `dispatchAction` が拒否を見てトーストする形と同じ役割の分け方）
- D-9 操作音は作らない（R-9。現行 Web のポーズに音は無く、資産も無い）。plan.md §2.2 の「操作音はビューからサウンドのサービスを直接呼ぶ」は音を鳴らす画面の作り方の規則で、音の無い画面に音を足すのは見た目・手応えの新機能になる。要裁定に「ポーズのボタンに操作音を足すか（足すなら音の資産が要る）」を書く
- D-10 無応答の判定は 1 秒周期のタイマー（`Observable.Interval`）。「何も届かない」ことには購読できる変化が無いので、AGENTS.md「状態変化の検知は購読で」の例外。時計は `Stopwatch`（UI のための実時間。サーバーのゲームロジックではない＝plan.md §0 の確認「AGENTS.md の時間の規則はサーバーのゲームロジックの規則」）。WS スレッドからも読み書きするので `Time.realtimeSinceStartup` は使えない
- D-11 CEF のプロセスの生存は cef-unity の `CefRuntime.ServerLost`・`ServerRecovered`・`ServerRecoveryFailed` のイベントから記録した最後の状態にする。OS のプロセス一覧を名前で探す案は、プラットフォームごとにヘルパーの名前が違い、cef-unity の外の推測になるので採らない。manifest には判定でなく生の値を入れる（§3.11「原因を CEF と決めつけない」）
- D-12 manifest の `schemaVersion` を 6 に上げ、`webUiHealth` は常時非 null で出す（取れない値は各フィールドの `notAttempted`/`notObserved`/null で表し、欠損列には入れない＝値が「起動を試みていない」という事実そのものだから）。自動修正の読み手は 2026-10-31 の裁定で撤去済みで、対応する読み手は無い（ダッシュボードは未知のフィールドを無視する。推測。報告に書く）
- D-13 R1 は骨組みの Web App 全体 unmount を維持し、cef-unity の変更は実測（R-14）で決める。依頼文の手順どおり
- D-14 toast の台帳行の所有者は `UITK` にする（その束だけを持つ行は切替 PR で UITK にする台帳の規則）。通信失敗のトーストは撤去まで Web に残る（§2.2）ので、agent 既定に 1 行書く
- D-15 録画プレイテストは Task 8 のラダー 1・入れ子 2 経路・R1 実測で行う（ランタイム挙動に触れるため）。L3 は Task 6 の `PauseMenuGameUiTest`
- D-16 `PauseMenuCommands.SubmitBugReport` の受理経路の L0 は、`BugReportSubmitter` が sealed で偽にできないため置かない。受理・拒否の View 側は L1（記録係が結果を返す）、本物の経路はラダー 1 で確かめる
- D-17 トーストの様式（GamePanel 配色の小窓・info/error の左帯）は agent 既定（拒否権つき）。裁定 `2026-10-26-素の見た目の4画面はUITKへ移すときに様式化する.md` は 4 画面を同じ語彙で一度に（grill 1 回）としているが、このレーンは質問禁止で grill を回せず、4 画面のうちチャレンジ・列車 HUD は段階 2 以降。ポーズのボタンは既存の PanelActionButton の語彙。要裁定に「4 画面の語彙を決める grill の時期」と「今のトースト様式でよいか」を書く（user-simulator 予測）
- D-18 コマンドの結果は判別型 `UiCommandResult`（Accepted／Rejected＋理由 enum）にし、良性かどうかは理由の性質として持つ（§3.2。Phase 2.6 5-B と配置検査の違反 2）
- D-19 manifest の生の値は enum で持ち、文字列は manifest へ書くときだけ（Phase 2.6 5-I）
- D-20 ポーズの表示条件は `UiScreenTraitsTable` の PauseMenu 列＋所有表から導く。画面名を Presenter で並べない（Phase 2.6 検査 6・第3バケツ 1）。`WebUiSuspended` は段階 3 でモーダルも含む意味になるので条件に使わない（配置検査の注目点）
- D-21 ADR 0069 の「送信後にトップへ戻すのはハンドラ」を型付きコマンドへ 1 行直す（Phase 2.6 検査 7）
- D-22 無応答の印 `_webSilent` の書き手は `EvaluateWebSilence` 1 本（Phase 2.6 5-F）。受信の記録は時刻だけ
- D-23 `SubmitBugReport` は Bug・Feedback 以外を拒否する（Phase 2.6 5-I 弱。Crash は前回異常終了の箱で、ポーズからは出さない）
- D-24 据え置き: `BugReportDraft`・`WebUiHealthRecorder`・`UiInputExclusivity` の読み取りと変更の分割（Phase 2.6 5-D）はしない。`UiInputExclusivity` は既存の static の窓口の形（input レーン）で、ここで分けると窓口が 2 つになる。Draft は書き手が View と Presenter だけの小さな値。`!_webUiSuspended && !_webSilent` の 2 条件は別々の理由（R1 と §3.11）なので合成のままにする（5-C 弱）。UiAssetCatalog の素通しの読み出しは規約の例外とし、前例は `Client.Game/InGame/Control/InGameCameraController.cs` の `public Camera Camera => mainCamera;`（D-6 の前例の引き方を訂正）。View が購読してコマンドを呼ぶ形は input レーンの雛形（`FakeModel` の remarks・`CounterSampleViewTest`）に従う
- D-25 本 PR 外のリファクタ提案（第3バケツ。plan のタスクに足さず報告へ回す）: `EventLanguageGate.TrySelectLanguage` と `SetLanguage` の結果型の統合／`GraphicsQualityWire.PresetToWire` と `SettingsModel` のプリセット導出の重複／設定の 3 topic（`GraphicsQualityTopic`・`SprintInputModeTopic`・`LocalizationTopic`）を `SettingsModel` の購読へ寄せる
- D-26 user-simulator の review は実施した（予測の適用 1 件＝D-17、Warning 3 件を Task 3・Task 7 に反映）。要裁定（操作音・4 画面の grill の時期）は質問禁止のため完了報告へ回す
- D-27 ポーズの枠は R-3 どおり GamePanel の様式（タイトル行 19px の題・端のフェード）にし、Web のポーズ枠（角丸 12px・h1 34px の題）へは戻さない（Task 6 レビュー。§0 の様式化の対象はボタンだけで、枠の差は GamePanel 採用から来る）。ボタンの面・間隔・内側の余白は Web を写す。人の目で違和感があれば要裁定（完了報告へ）
- D-28 testid `pause-menu` とアンカーは PauseMenuView 自身でなく中の見える枠（GamePanel）に付ける（Task 4。translate % が前ページの高さで残り戻るボタンが画面外に出たため。Web も見える枠に testid）

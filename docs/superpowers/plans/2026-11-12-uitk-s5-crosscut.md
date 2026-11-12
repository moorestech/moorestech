# UITK S5-crosscut（横断層の残り: UI の例外の経路・作らない横断層の記帳・X-audit の設計判断 15 件・AC-5・束 5 の横断テストの写し）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Web の横断層のうち UITK に残っていた「UI の例外の受け皿（`AppErrorBoundary` の代わり）」を、例外をログ（`LogType.Exception`）とバグ報告の manifest（v7 `gameUiExceptions`）へ必ず出す経路として作り、出る場面の無い再接続オーバーレイ・辞書エラー・`ConnectingPlaceholder` は作らず disposition に理由を残し、X-audit が S5-crosscut へ割り振った設計判断（DC-8 B・DC-10・DC-11・DC-12・DC-16 C・DC-18・DC-19・DC-21・DC-23・DC-27・DC-28 B・DC-30・DC-31・DC-32・DC-34）と AC-5 を部品・Foundation の 1 か所へ寄せる形で当て、束 5 の横断テスト（`i18n.spec` 2・`allScreensI18n` 6・`uiState.spec` 5・`regression/inputLayer.spec` 4・`textSelection` 2）の意図を NUnit へ写して `feature/uitk` へマージする。

**Architecture:** 単位レーン（Mac の Editor 1 本）。新設は Foundation の小さな共有 5 本（`UiDisplay`・`UiScrollClipInset`・`GridMeshIndexWriter`／`Painter2DSquareCap`・`UssStyleResolutionGate`）と Client.Input の 1 組（`IUiKeyPressSource` と実装 `InputKeyPressSource`、キーごとの型 2 つ。矢印は A-162 の `InputSystemArrowKeyStream` を残して抑止だけ `InputSuppressionFilter` へ通す）とバグ報告の読み取り（判別 union の `GameUiExceptionReading`・ログを受けた時点でセッション通算で数える `GameUiExceptionTally`・詰める `BugReportManifestReadings`）で、残りは既存の直書き・重複をそこへ付け替える置換と、部品の ctor・USS のつまみの付け替え。Web ホスト（`Client.WebUiHost`）は State の表・Catalog・Commands を呼ぶ形へ付け替え、判定の正を State の 1 か所にする。各タスクは L0/L1 を先に赤で書き、置換後に緑、並べ比べ（1280×720・2560×1080）と録画（ラダー 1〜5 と横断の録画）で退行 0 を確かめる。

**Tech Stack:** Unity 6 / UI Toolkit（`Client.Ui`・`Client.Ui.State`・`Client.Input`）、VContainer、UniRx、NUnit（`ScreenTestHost` の panel simulate・`InputTestFixture`・EditModeInPlayingTest）、プレイテスト DSL（`.agents/skills/unity-playmode-recorded-playtest`）、Node（`tools/uitk/*.mjs`・`node --test`）、Python（`scripts/playtest/dashboard`）

設計の出所: 実行計画 `docs/uitk-migration/execution/execution-plan.md` の S5-crosscut 行と §8 E-17、設計書 `docs/uitk-migration/execution/component-architecture.md` の [A-13]（:479）・[A-38]（:504）・§3.2（:279-288）・§4.2（:335-）・§2.7 の OS カーソル行（:234）、統合計画 `plan.md` §2.6「最後の束の対象」、X-audit の `done.md`（`../moorestech_logs/harness/uitk/X-audit/done.md` の「## 設計判断」:118-250 と「## Warning 全件」:337-）、指揮の割り当て（brief `brief-s5-crosscut.md`）、裁定 `.decisions/2026-11-06-*` 7 本（UITK 6 本と CEF 停止。「UITK 切替まで CEF 由来の不具合は直さない」を含む）。ゴール原文:「WebUIと同じ見た目で、適切な設計、コンポーネント化されたUITKベースのUIを開発すること」。

## Requirements

レーン行（実行計画の原文）:

`| S5-crosscut | 横断層の残り | 5・W5 | S4-gamma | 要 | 単位 | UI の例外を LogError と manifest へ（`AppErrorBoundary` の代わり）、再接続オーバーレイ・辞書エラー・`ConnectingPlaceholder` は作らず disposition に理由（[A-13]・[A-38]。統合計画 §2.6 との差は E-17）。写す: `system/i18n.spec`(2)・`allScreensI18n`(6)・`uiState.spec`(5)・`regression/inputLayer.spec`(4。ホイールの 1 件は S4-inventory)・`textSelection`(2) の意図 |`

| # | 要件 | 受け入れ基準 |
|---|---|---|
| R-1 | UI の例外の経路（`AppErrorBoundary` の代わり） | (a) UITK のイベントコールバックと UniRx の購読の中で投げた例外が `LogType.Exception` でログに出て、`UnityLogRing` が拾うことを L1 `GameUiExceptionLoggingTest` が固定（2 件）。(b) バグ報告の manifest が `schemaVersion: 7` と `gameUiExceptions` を必ず持つ。形は判別 union で、`{"status":"recorded","count":n,"samples":[最大 5 件〈tick・message・frame〉]}` か `{"status":"unknown"}`（unknown は `count`・`samples` の欄を持たない）。件数は 2000 件で古い順に捨てるログ環（`UnityLogRing`）からでなく、ログを受けた時点で `GameUiExceptionTally` がセッション通算として数え、見本は先頭 5 件（環が溢れても序盤の例外が 0 件にならない）。確保時に数を取れなかった箱とクラッシュの箱は `BugReportManifestReadings.MarkGameUiExceptionsUnknown` 1 本で `unknown`＋`missing` の `gameUiExceptions` 行を組で書く（前例 `CrashBundleWriter`:57-58 の webUiHealth と同じ組。L0 `GameUiExceptionManifestTest` 6 件〈うち 1 件が「環が溢れた後でも序盤の例外が数に残る」〉。版を見る既存の `WebUiHealthManifestTest`・`CrashBundleWebUiHealthTest`・`RemoteExecBundleMarkTest` と録画シナリオ `playtest-receiver/developer-report-delivery.cs`・`developer-report-interrupt.cs` も 7 へ）。(c) ダッシュボードが `gameUiExceptions` を欄なし（v6 以前）・不明・読めない・記録ありで出し分ける（`python3 -m unittest scripts/playtest/tests/test_dashboard_game_ui_exceptions.py` 緑）。(d) フォールバック画面は作らない（D-1） |
| R-2 | 作らない横断層の記帳 | `docs/uitk/disposition.md` の `## 処遇` に、`e2e/tests/regression/connection.spec.ts` の 3 件（restoring・connecting placeholder・再接続バナー）と `allScreensI18n.test.ts` の辞書の HTTP 取得 3 件が `捨てる` で理由（[A-13]・[A-38]・E-17）付きで載り、`node tools/uitk/test-name-parity.mjs --bundle 5` がそれらを未対応に数えない。`AppErrorBoundary` のフォールバック画面を作らない理由は設計書 §7.1 の A-244（本文は D-1） |
| R-3 | 束 5 の横断テストの写し | `i18n.spec` 2・`allScreensI18n` 3（残る 3 は R-2 の捨てる）・`uiState.spec` 5・`inputLayer.spec` 4・`textSelection` 2 の計 16 件に `/// <web-test file="…">…</web-test>` が付いた NUnit が緑。`test-name-parity.mjs --bundle 5 --emit-missing` の出力にこの 5 ファイルの行が 0 |
| R-4 | DC-11 表示の出し入れ | `Foundation/Layout/UiDisplay.SetShown(VisualElement, bool)`（出すときは `StyleKeyword.Null`）が唯一の書き口、`UiDisplay.IsShownInline` が唯一の読み口。4 つの helper（`SpecialViewParts`:32・`SettingsSectionParts`:32・`BugReportRecoveryControlsView`:63・`SkitToolbar`:90）を消し、`UiGapContainer`・`UiLayerStack`・`CursorTooltipView` を含む直書き 74 か所（T2 の Files の全一覧。起草時の実測 76 行 44 ファイルから `CurrentChallengeHudView` の 2 行を除く）を置換する。検査 `git grep -nE "style\.display[[:space:]]*=[^=]" -- 'moorestech_client/Assets/Scripts/Client.Ui/*.cs'` の結果が `Foundation/Layout/UiDisplay.cs` の 1 行だけ（T22 Step 2 の取り込みまでは S5-challenge の持ち物 `Screens/Challenge/CurrentHud/CurrentChallengeHudView.cs`:38,43 の 2 行も残り、取り込みで置換して 1 行になる）。読み `style\.display[[:space:]]*==` は 0 行。L1 `UiDisplayTest` 3 件 |
| R-5 | DC-19 スクロールのクリップ逃げ | `Foundation/Layout/UiScrollClipInset`（`Apply` 4 辺・外と内を片側ずつ当てる `ApplyOuterMargin`／`ApplyInnerPadding`・左右だけの `ApplyHorizontal`）が唯一の式。4 実装（`RecipeEntryList`・`BuildMenuScrollClipInset`・`NodeGraphView`・`BlockInventoryNaturalScroll`）と `ResearchCalcKnobs.ApplyScrollBleed` が呼ぶ。`RecipeEntryList` は内側を部品の `RecipeListScrollView.SetClipPadding(float)` から当て、部品の内側の `contentContainer` を直に触らない。`ItemListView.ApplyClipClearance`（3 辺＋実測の基準値。Web の `ItemListPanel.module.css`:32-55 も別の式）は置き換えない（D-6）。L1 `UiScrollClipInsetTest` 3 件。並べ比べ（R-17）でスクロールの端が退行 0 |
| R-6 | DC-27 描画ヘルパ | `Foundation/Theme/Mesh/GridMeshIndexWriter.WriteQuads`・`Painter2DSquareCap.AddSegment`・`ShadowRingMeshWriter.RingCount`／`CenterMargin` が唯一。格子の index 5 か所・square cap 2 か所・影の定数 2 部品が呼ぶ。L1 `GridMeshIndexWriterTest` 2 件・`Painter2DSquareCapTest` 1 件。部品の L1 と並べ比べが退行 0 |
| R-7 | DC-28 B トークン待ちの宿主 | `Foundation/Theme/StyleResolution/UssStyleResolutionGate` が「解決待ち→解決の通知→配置後も未解決なら LogError」を持ち、`ToastHostView`・`NotificationHostView` が使う。読めないトークンの診断は両方が同じ文（`[<宿主>] … unreadable`）。L1 `UssStyleResolutionGateTest` 3 件、既存の Toast/Notification の L1 緑 |
| R-8 | DC-10 画面がキーを受ける口 | `InputKey` のキーを画面へ渡す口は `Client.Input/KeyPress/IUiKeyPressSource`（`IObservable<Unit> OnPressed`・`bool WasPressedThisFrame()`）と唯一の実装 `InputKeyPressSource`（抽象。`InputKey` を読むのはここだけ）で、DI の鍵はキーごとの型 `ISkitAdvanceKeyPressSource`／`ICloseUiKeyPressSource`（実装 `SkitAdvanceKeyPressSource`・`CloseUiKeyPressSource`）。`SkitEnterKeySource`・`IUiCloseKeyProbe`・`InputManagerCloseKeyProbe` を消し、`RecipeSelectionCloseReset` を `Client.Ui.State/Recipe/Screen/` へ移す（駆動は旧の pull のまま。D-8）。矢印は A-162 のとおり `Client.Input/ArrowKeys/InputSystemArrowKeyStream` が流し、UI の入力マップと `.inputactions` は変えない。stream が押下の縁を `InputSuppressionFilter.FilterKeyPress` に 1 回通し、`BeltThroughputSectionPresenter` の `UiInputExclusivity.IsTextInputFocused` を消す。入力欄のフォーカス中に 3 口とも流れないことを L0 で固定（`UiKeyPressSourceSuppressionTest` 3 件・`InputSystemArrowKeyStreamTest` に 1 件） |
| R-9 | DC-8 B 画面の表示判定 | 画面の開閉を読む State は `<機能>ScreenVisibility.IsShown(UiRoutingSnapshot …)` を通す（`BuildMenuScreenVisibility`・`RecipeScreenVisibility` を新設、`ResearchScreenVisibility`〈I-fix6 が作る〉・`BlockInventoryScreenVisibility` は既存）。`BuildMenuModel`・`BuildMenuCommands.IsBuildMenuOpen`・`ResearchNodeStateStore` が `UIStateControl.CurrentState` を読まない。`snapshot.Screen == UiScreen.<画面>` の直書き（`BuildMenuScreenPresenter`:54、研究の 3 か所は I-fix6 が付け替えていなければここで）も `<機能>ScreenVisibility` を呼ぶ（`git grep -n -e "Screen == UiScreen\.BuildMenu" -e "Screen == UiScreen\.ResearchTree" -- 'moorestech_client/Assets/Scripts/Client.Ui*/*.cs'` が `*ScreenVisibility.cs` だけ。起草時は 5 行）。`UiScreen` に無い状態（GameScreen・PlaceBlock 等）を読む箇所は理由の 1 行コメント付きで残す。既存の L0/L1 緑 |
| R-10 | DC-12 マスタに無い ItemId | `GameContentPresentation.TryItem(ItemId, out UiIcon)` を足し、`GameContentNotificationRowIcons` が `MasterHolder` を直に読まない。L1 1 件 |
| R-11 | DC-16 C ItemSlot の中身 | `ItemSlotContent` の優先（実物 > ゴースト、白面は実物だけ）を型コメントと部品カタログ（設計書の改訂案）に書き、`FluidSlotContent`（sealed 派生）との違いの理由を D-9 に残す。コードの型は変えない |
| R-12 | DC-18 指カーソル | `IconButton`・`PanelActionButton`・`ModeSwitch<TValue>` が ctor で `IUiOsCursor` を受け、自分（ModeSwitch は各選択肢）のホバーで `UiOsCursorHoverRequest(…, Pointer, Element)` を出す。全呼び出し側を更新。L1 `CommonButtonPointerCursorTest` 3 件（乗ると Pointer・外れると Default） |
| R-13 | DC-21 部品の内側の上書き | `PauseMenu.uss`:73・`SkitToolbar.uss`:35-49・`SkitChoices.uss`:36-40・`GamePanelSurface.uss`:83 の、他部品の内側クラス／`.unity-button` への上書きを、部品ルートのつまみ（`--panel-action-button-width`・`--icon-button-color`・`--game-panel-surface-face` ほか）へ置き換える。`GamePanelSurfaceKind.SkitChoice` を消す（Web の `GamePanelVariant` は `"default"｜"craft"｜"skit"｜"hud"` で、選択肢の板は GamePanel でなく `.choice::before`。`Skit` は `"skit"` と 1 対 1 なので残す。A-84 と設計書 §2.2・§2.7 の改訂案はメモ）。`UiStyleConventionTest` に「画面 USS が他部品の `__` 内側クラスを書かない」検査 1 件。並べ比べ 1-pause-top・2-skit-choices・2-skit-text が退行 0 |
| R-14 | DC-34 A 不足の旗 | `BuildMenuRequiredItem.Lacking` を消し、不足は `BuildMenuMaterialLine.IsInsufficient`（免除込み）だけ。`BuildMenuShortage` は `Held < Count` から決める。既存の L0 緑 |
| R-15 | DC-30 A トークン値の CI | `convert-web-tokens.mjs`・`convert-mantine-tokens.mjs` が `--check`（書かずに差があれば exit 1）を持ち、`run_test.yml` の webui-test が base（か head）が `feature/uitk` の PR でだけ両方を回す（G1 と同じ条件）。依存の無い `node --test tools/uitk/webTokens/tests/*.test.mjs`（2 件）は `uitk-web-follow-gate.yml` の自己テストの job に並べる。手元で両方 `--check` が exit 0 |
| R-16 | DC-31 A Presenter の結線 | L1 `PresenterMountTest`（本番の `UiLayerStack`・`GameUiRootTestFactory`・切り替えられる `UiRoutingState`）が `uiState.spec` の 5 件を写して緑。L3 `GameUiScreenMountTest` が実ゲームの起動後に、全画面の View が Presenter の決めた層（`GameUiRoot.GetLayer(UiLayer, UiFamily)`）に 1 つだけあり、Render が届いていること（ホットバーの枠の数が 0 でない・持ち物を開くと枠が 0 でない）を見る（DI 登録漏れ・層の取り違え・購読の消し忘れの網）。段ごとの L3 は各ラダーのレーンの物を数える（D-14） |
| R-17 | 比較撮影と独立評価 | 1-pause-top・2-skit-choices・2-skit-text・2-challenge-hud-multiple-long・3-build-menu・3-modal・4-item-list-large・4-research・4-block-chest・4-block-machine・1-toast・2-notifications・5-japanese-inventory・1-bug-report・2-world-pin-offscreen・5-tutorial-outline-label・2-train-hud・2-tooltip・2-key-control-hint・2-progress-labeled・3-hotbar-equipment-empty と recipe の `4-recipe-selected-craft`（`web-9ce5557bdb-s4-recipe`）を 1280×720 と 2560×1080 で撮り、参照と並べて opus の評価役 2 体が別々に判定。再現した差は直すか LX-205〜206 に記帳。`done.md`「差の表」に全行の処遇 |
| R-18 | DC-32 A Presenter の型 | ゲーム寿命の Presenter・Relay・Binder・Installer 17 本と `RecipeSelectionCloseReset` から例外なく `IDisposable` を外し、`Dispose` を呼ぶテストの 3 行を消す（D-16）。S5-gate の DI 前の土台 `GameUiPreDiFoundation` もこの 2 型の `Dispose` を呼ばない形で、どちらが先に入ってもコンパイルが通る（T0 Step 3 で照合）。設計書 §4.2 の型の改訂案をメモへ |
| R-19 | DC-23 A Web ホストの判定 | `UiStateActions.IsAllowed` を消して `UiStateTransitionRules.IsAllowed` を呼ぶ、`CraftActions`・`CraftingRecipesTopic`・`RecipeViewerItemListTopic`・列車の 7 組（s4-train 計画「## 本 PR 外のリファクタ提案」の 1〜8 行目）が State の Catalog・Commands・変換を呼ぶ。`Client.Tests/WebUi/` の既存テスト緑、Web 系の録画（R-21）が退行 0 |
| R-20 | AC-5・Warning | AC-5 の 48 件（除外 3 件は D-18）を単一参照のローカル関数へ移す。`BugReportFormView` の 3 件（:107・:173・:185）は I-fix6（DC-22 の持ち主）の取り込み後の実物で、移しても 200 行未満に収まるときだけ移し、収まらなければ新しい型を作らず落として `done.md` に理由を書く（D-24）。Warning の S5-crosscut 候補 4 行を拾う／見送るで 1 行ずつ処遇（c22-si×2・c46-cc は拾う、c63-cc は見送る） |
| R-21 | 録画 | 反映後のバイナリで、GameUi のラダー 1〜5（`uitk-lifeline`・`stage2/uitk-opening-hud`・`stage3/uitk-build-bp`・`stage4/uitk-production-loop`・`stage4/uitk-research-train`）と横断の録画 9 本（T20 Step 2）が `Success: true`。Web 系で `misc/ui-state-transition-matrix`・`misc/localization-language-switch-via-ui`・`train/fluid-inventory-via-interact`・`train/train-timetable-via-ui`・`train/train-station-default-name`・`playtest-receiver/developer-report-delivery`・`playtest-receiver/developer-report-interrupt`（manifest v7。`webUiHealth` が Web ホストの起動を見るので Web の系）が CEF の出る環境なら `Success: true`（出なければ RendererUnavailable の行を証拠に「未確認」） |
| R-22 | 指揮の割り当ての完了 | DC-8 B・DC-10 A・DC-11 A・DC-12 A・DC-16 C・DC-18 A・DC-19 A・DC-21 A（Timetable.uss の PanelActionButton 無効の上書きは I-fix5 のため除く）・DC-23 A・DC-27 A・DC-28 B・DC-30 A・DC-31 A・DC-32 A・DC-34 A・AC-5・Warning の候補が、`done.md`「割り当ての処遇」の表で全行「やった（Task n・証拠）／見送った（理由）」 |
| R-23 | 申し送りの処遇 | 「## 申し送りの処遇」の全行が `done.md` に写る |

### やらないこと

- 再接続オーバーレイ・辞書エラー・`ConnectingPlaceholder`・エラー境界のフォールバック画面（[A-13]・[A-38]・E-17・D-1）
- DC-37（S5-gate）、DC-15・DC-29・DC-33・DC-5 B（X-5）、DC-8 A・DC-16 A・DC-28 A・DC-9・DC-13・DC-14・DC-17・DC-20・DC-22・DC-24・DC-25・DC-26（I-fix6）
- `Timetable.uss` の PanelActionButton 無効時の上書きの除去（I-fix5）
- Web ホストの削除（X-5。このレーンは付け替えまで）
- `Screens/Challenge/`（S5-challenge）・言語ゲートと `Client.Ui.State/EventMode`・`Client.Starter/EventMode`（S5-gate）の新しいファイル。既存の `Screens/Challenge/CurrentHud/` は取り込み後に置換だけ当てる（D-20）
- 束 5 の暗転・突き合わせの締め・並べ比べの締め（S5-ladder）
- Codex（指揮の指示。§5・§3.3 の codex-audit は opus の subagent 2 体の独立評価で代える）

## Global Constraints

- AGENTS.md 全部（1 ファイル 200 行未満・1 ディレクトリ 10 ファイル・partial と `Func<>` の禁止・try-catch は外部境界だけ・UniRx・2 行コメント〈日本語 1 行→英語 1 行〉・`#region Internal` はメソッド内のローカル関数だけ・デフォルト引数禁止・単純な getter/setter プロパティ禁止〈`{ get; private set; }` は可〉・.meta を手で作らない・Prefab/シーンを手で編集しない・fail-closed の経路はログ）
- Mac mini で同時に動かす Unity Editor は全体で 1 本（このレーンの worktree の 1 本。起動前に `moores-wt status` で所有を見る）。subagent は Editor を起動しない。並列が要るときは Windows の `~/bin/winunity`（push 済みブランチ。結果は `~/.cache/winunity/runs/`）
- 実装は moores-subagent-driven-development で Claude Code の opus subagent に渡す。subagent への依頼文に「バックグラウンドのポーリングで待たず全部前景で実行する」「区切り（タスク）ごとにコミットする」を書く
- コンパイル: `uloop compile --project-path ./moorestech_client`（.cs を触ったタスクは毎回）。テスト: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。ドメインリロード中のエラーは 45 秒待って再試行
- 録画は PlayMode を止めた状態で `uloop execute-dynamic-code --project-path ./moorestech_client --code 'Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi);'`（Web は `UiSystem.Web`）を実行してから `.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh ./moorestech_client <scenario.cs>`。終了コード 0 成功・1 失敗・2 コンパイル不可。失敗したら次の前に `uloop control-play-mode --project-path ./moorestech_client --action Stop`。初回 "already playing" は 1 回だけ投入し直す
- 番号: 設計書の行は A-244〜A-248、見た目の例外は LX-205〜LX-206 だけ（[A-13]・[A-38] は参照のみ）。使わなかった番号は `done.md` に「未使用」と書く
- 共有区画は追記だけ: tss の族別区画・`MainGameUiRegistration` の画面登録区画・`localization.csv`・台帳 `docs/uitk/TODO.md` の「## 実測の結論」「## 見た目の例外」「## 追従記録」の末尾・`docs/uitk/disposition.md` の `## 処遇` の末尾。取り込みの衝突は両方の行を採る
- 比較撮影の出力は `moorestech_client/.uloop/outputs/compare/S5-crosscut/`。使い捨ての撮影スクリプトを書かない（部品・EditMode は `Client.Ui.Editor.Capture.UiRuntimePanelCapture.CaptureToPng(VisualElement, int, int, Color32, UiCaptureLocalization, string)`、画面・PlayMode は `GameViewResolution.Set`／`PlaytestGameViewOps.SetResolution(ct, w, h)`）。手順の正本は `.agents/skills/ref-uitk-design/references/compare-procedure.md`
- 参照キャプチャ: `../moorestech_logs/uitk/reference-captures/web-8a585f742a/`（1280×720）と `web-8a585f742a-2560x1080/`（recipe だけ `web-9ce5557bdb-s4-recipe`／`-2560x1080`）
- 録画 mp4・`PlaytestResults/`・TestResults xml・撮影 PNG は検証後に消す（報告ページへ載せる分は先に載せる）。テストで作ったワールドは `.agents/skills/unity-playmode-recorded-playtest/scripts/list-work-saves.sh` で探して消す。`.superpowers/` はコミットしない
- 新しい共有を見つけたら、このレーンの範囲（下の File Structure）に無いものは自分で作らず `done.md`「integrator への依頼」に書く

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反2件・修正2件 / `gameUiExceptions.status` に前例どおり欄の StringEnumConverter＋CamelCase 属性、`CraftActions` は switch を書かず `UiCommandRejectReasonTraits.ToWireCode` を呼ぶ。注目点 N1〜N9 は D-8（pull を保つ・キーごとの型の DI）・D-1（`GameUiExceptions/`）・D-7（`Theme/StyleResolution/`）・D-12（`SkitChoice` は Web に variant が無く削除）・D-23（CI の置き場）・D-24（`BugReportFormSender` を作らない）へ、死活表に 10 行
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強4・弱3・第3バケツ4 / 例外の読み取りを判別 union に畳み判定と「不明＋欠損」の口を 1 本ずつに、A-162 を保って矢印の抑止を stream 内の `InputSuppressionFilter` へ（A-246 で A-162 の一部を改める）、A-84 は A-247 で改める。弱 5-F5 は AGENTS.md の既知の制約（D-22）。第3バケツは B-1 を T2 へ取り込み、B-2〜B-4 は「## 本PR外のリファクタ提案」
- 独立評価・予測レビューの反映: 実施済み / 再現 27 件を反映・不再現 8 件 / UI の例外の件数をログ受信時のセッション通算へ（環の溢れで 0 件にしない）、manifest v7 は先取りし X-5a を v8 へ、T3 は `ItemListView` を外し `RecipeEntryList` に片側ずつの口、L3 は層と Render まで見る、S5-gate とは Dispose を呼ばない形で揃え、写し先・版の照合・パス・置き場の軽微を直した（処遇は判断記録の末尾）
- 評価反映後の再検査（変えた範囲・fresh-context）: 実施済み / 違反1件・修正1件・強1・弱3 / ADR 0057 の契約の段落と版の書き直しを T1 Step 4b に入れた（強 1 も同じ根）。弱 3（ゲーム寿命の静的イベント購読・RecipeEntryList の marginTop の書き手 2 つ・下流の X-5a の行）は前例どおり・D-6 の理由で保つ

## 死活表（Phase 2.5）

| 操作 | 計画後も生きるか | 根拠 |
|---|---|---|
| UI のコールバックで例外が出たときのゲーム続行 | 生きる | 例外の扱いは Unity のまま（ログに出て次のイベントは届く）。足すのは manifest の数え上げだけ（T1） |
| バグ報告の送信・書き出し・クラッシュ箱の回収 | 生きる | manifest に欄を 1 つ足すだけ。v6 以前の箱はダッシュボードが「欄なし」と出す（T1。`test_dashboard_game_ui_exceptions.py`） |
| スキットの Enter 送り・Esc での選択解除・ベルトの ←→ | 生きる（入力欄中の抑止が `InputSuppressionFilter` の 1 か所になる） | Enter と Esc は `InputKey` を、←→ は stream の中で `InputSuppressionFilter.FilterKeyPress` を通る（T6。L0 `UiKeyPressSourceSuppressionTest` 3 件・`InputSystemArrowKeyStreamTest` の抑止の 1 件・録画 `uitk-skit`・`uitk-recipe-craft`・`uitk-block-special-panels`） |
| Esc でレシピの選択が外れる（テキスト入力のフォーカス中・ESC ラッチがかかったフレームを含む） | 生きる（評価の時点は変わらない） | 駆動は旧と同じ pull（遷移の購読の中で `CloseUI.GetKeyDown` を読む）で、ESC ラッチと抑止は読んだ時点で評価される（D-8 の比較）。L0 `RecipeSelectionCloseResetTest` 既存 2 件＋`UiKeyPressSourceSuppressionTest` の CloseUI の件（`SetKeyDownForTest(true)` で押下・入力欄フォーカス中は false・`ConsumeEscapeThisFrame` 後は false）・録画 `uitk-recipe-craft` の「Esc で選択が外れる」段（T20 Step 2） |
| ←→ を他に使う操作（移動・カメラ） | 生きる | `.inputactions`・`InputManager` を触らない（A-162 を保つ）。stream は今までどおり InputSystem のイベントを読むだけ（T6） |
| トースト・通知が出る（トークンの解決前の預かり・読めないときの動き無しでの表示を含む） | 生きる | 預かりと流し直しは宿主に残り、門は解決と診断だけ（T5）。L1 `UssStyleResolutionGateTest` 3 件・既存の Toast/Notification の L1・並べ比べ 1-toast・2-notifications（T19）・録画 `uitk-notification-compare`（T20） |
| ビルドメニューの材料不足の表示（赤）と建てられるかの判定 | 生きる | 判定は `IsInsufficient`（免除込み）1 つで、`BuildMenuShortage` は `Held < Count`（T11）。L0 `BuildMenuEntryFactoryTest`:122 が Web の DTO の判定と並べて見る・並べ比べ 3-build-menu・録画 `uitk-build-menu` |
| 通知の行のアイコン（マスタに無い ItemId の代わりの絵とログ） | 生きる | 判定を `GameContentPresentation.TryItem` へ移すだけで代わりの組み立てとログは通知の口に残る（T8）。L1 1 件（無い ID で代わりの絵、ある ID で `Item(id)` と同じ絵） |
| × などの `IconButton` の色（通常・ホバー・無効）。ブロックのパネル・ビルドメニュー・スキットのツールバーの全呼び手 | 生きる | 色の規則をルートのつまみ `--icon-button-color` へ移す（T10 Step 3）。L1 `IconButton` の色の件（T10 Step 3 で足す）・並べ比べ 3-build-menu（`BuildMenuPanelSkeleton`:71 の ×）・4-block-chest（`BlockInventoryView`:55 の ×）・2-skit-text（ツールバー） |
| `GamePanelSurface` の hud・skit の面（チャレンジの HUD・会話窓） | 生きる | 面の規則をつまみの受け直しに変える（T10 Step 4）。L1 `GamePanelSurfaceTest` に種別ごとの面の値の件（T10 Step 4 で足す）・並べ比べ 2-challenge-hud-multiple-long・2-skit-text |
| 頂点カラーで描く装飾（ヴィネット・GamePanel の面・ダイアログの影・クラフトのボタン・レシピ行の枠・× の図形・スキットの送りの印・進捗の矢印の影） | 生きる | index の並び・square cap・影の定数を 1 か所へ移すだけで値は同じ（T4。並びの違う 2 か所は置換しない）。L1 `GridMeshIndexWriterTest`・`Painter2DSquareCapTest`・各部品の既存 L1・並べ比べ 3-modal（影）・recipe（ボタン・行の枠）・4-block-machine（進捗の矢印）・2-skit-text（送りの印）・4-block-chest（面・×） |
| Web 系: レシピ一覧と解放済みクラフトの表示・列車のホームの積み降ろしモード・駅名の変更・列車の液体インベントリ | 生きる | 判定を State へ付け替えるだけで wire は変えない（T15）。`Client.Tests/WebUi/` の対応テスト・録画 `train/fluid-inventory-via-interact`・`train/train-timetable-via-ui`・`train/train-station-default-name`（T20 Step 3。CEF が出ない環境なら「未確認」） |
| バグ報告フォームの送信（送信中の無効化・状態の表示・送信後のトースト） | 生きる | このレーンは送信の手順を動かさない（`BugReportFormSender` は作らない。D-24）。AC-5 で移すのは単一参照の helper だけ。L1 `BugReportForm` の既存テスト・録画 `playtest-receiver/developer-report-delivery`（T20 Step 3。Web の系）・並べ比べ 1-bug-report |
| 入力の共通規則・ドラッグ・押下ジェスチャ・OS カーソルのスタック・モーダル（AC-5 の移動先: `GameUiInputRules`・`UiDragDrop`・`UiPressGesture`・`UiOsCursorRequestStack`・`ModalView`） | 生きる | 挙動不変の移動（T17）。全 UI の L1（`^Client\.Tests\.Ui\.`）と WebUi の L0 で見る（T17 Step 5） |
| 各画面の出し入れ（display） | 生きる | 出すときはインラインを外して USS に戻す。USS が既定で `display: none` の要素は T2 Step 2 の照合で状態クラスへ替える |
| スクロールの端の見え方（5 画面） | 生きる | 式を 1 本にするだけ。並べ比べで確かめる（T3・T19） |
| ビルドメニューの開いた時の BP 取り直し・組み直し | 生きる | 開閉の読み先を `UiRoutingState` に替えるだけ（T7。`BuildMenuModelTest`・録画 `uitk-build-menu`） |
| 研究ノードの状態の購読 | 生きる | 同上（T7。`ResearchNodeStateStoreTest`・録画 `uitk-research-tree`） |
| 部品の指カーソル | 生きる（増える） | 画面が個別に足していた所（`SkitChoiceButton` ほか）は部品のものと重ならないよう T9 Step 4 で外す |
| ポーズメニュー・スキットの見た目 | 生きる | 上書きの経路だけ替える。並べ比べ 1-pause-top・2-skit-choices・2-skit-text で確かめる（T10・T19） |
| Web ホストの遷移の許可・クラフト・列車の時刻表（Web 系） | 生きる | 判定を State の 1 本へ付け替えるだけで wire の文字列は変えない（T15。`Client.Tests/WebUi/` の既存テスト・Web 系の録画） |
| Presenter の破棄（シーンを畳むとき） | 消える（意図） | ゲームはメインメニューへ戻らない設計で破棄は来ない（AGENTS.md 既知の制約）。L1 の後始末で呼んでいた 3 行は消す（どれも源をテストごとに作り直す。D-16） |

## 申し送りの処遇

| 出所 | 項目 | 処遇 |
|---|---|---|
| 指揮（`conductor.md`:186） | DC-23 A は 11-10 04:50 の「据え置き・X-5 で Web ホスト側を消す」を新しい決定で覆す | やる（T15・D-17） |
| X-audit `done.md` 割り振り | DC-8 B・DC-10・DC-11・DC-12・DC-16 B/C・DC-18・DC-19・DC-21・DC-23・DC-27・DC-28 B・DC-30・DC-31・DC-32・DC-34・AC-5（全対象） | やる（R-22 の表） |
| X-audit Warning | S5-crosscut 候補 4 行（c22-si 表示 helper・c22-si 空ラベル・c46-cc 研究の現在画面の読み・c63-cc readonly と `{ get; }`） | 拾う 3・見送る 1（T16・D-19） |
| s4-train 計画「## 本 PR 外のリファクタ提案」 | Web ホストと State の写しの 9 項目 | 1〜8 行目をやる（T15）。9 行目は記録だけの行（新設しなかった旨）なので処遇不要 |
| 他レーンの `done.md` | X-audit 以外に S5-crosscut 宛ての依頼 | 無し（起草時の grep。T0 Step 4 で最新を見直す） |

## File Structure

S = `moorestech_client/Assets/Scripts`。持ち主が他レーンのファイルは「取り込み後」と書いた。

| パス | 作る/直す | 役割 |
|---|---|---|
| `S/Client.Game/InGame/BugReport/GameUiExceptions/GameUiExceptionReading.cs` | 作る | manifest v7 `gameUiExceptions` の契約（判別 union: `GameUiExceptionsRecorded`／`GameUiExceptionsUnknown`・見本 `GameUiExceptionSample`）。置き場は読み取りごとのディレクトリ（前例 `BugReport/WebUiHealth/`） |
| `S/Client.Game/InGame/BugReport/GameUiExceptions/GameUiExceptionTally.cs` | 作る | ログを受けた時点で UI の例外をセッション通算で数える唯一の数え手（`Application.logMessageReceivedThreaded` を購読。名前付き定数 1 つ・`TryFindFirstUiFrame` 1 つ・見本は先頭 5 件）。2000 件の環 `UnityLogRing` からは数えない |
| `S/Client.Game/InGame/BugReport/Readings/BugReportManifestReadings.cs` | 作る | 確保データ → manifest の読み取り欄（webUiHealth を Writer から移し、gameUiExceptions を足す）と「不明と欠損を組で書く」唯一の口 `MarkGameUiExceptionsUnknown`。`BugReport/` 直下は .cs が既に 10 本なのでサブディレクトリ |
| `S/Client.Game/InGame/BugReport/BugReportManifest.cs`・`BugReportBundleWriter.cs`・`LastSession/CrashBundleWriter.cs` | 直す | v7・欄・Writer は 200 行未満に縮む |
| `S/Client.Game/InGame/BugReport/Capture/BugReportCaptureStatus.cs`・`BugReportCaptureSources.cs`・`BugReportCaptureSession.cs`・`S/Client.Starter/Registration/PlaytestRecordRegistration.cs`・`UncollectedBugReportCaptureSources.cs` | 直す | 確保の瞬間に数え手の値を写す（`IBugReportCaptureSources.GameUiExceptions()`・`BugReportCapturedData.GameUiExceptions`）と数え手の登録 |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/playtest-receiver/developer-report-delivery.cs`・`developer-report-interrupt.cs` | 直す | manifest の版の照合 6→7 |
| `scripts/playtest/dashboard/display_fields.py`・`collect_reports.py`・`static/js/views/report.js` | 直す | `gameUiExceptions` の表示 |
| `scripts/playtest/tests/test_dashboard_game_ui_exceptions.py` | 作る | 表示の 4 状態 |
| `S/Client.Tests/BugReport/Bundle/GameUiExceptionManifestTest.cs`・`WebUiHealthManifestTest.cs`・`CrashBundleWebUiHealthTest.cs`・`S/Client.Tests/RemoteExec/RemoteExecBundleMarkTest.cs`・`S/Client.Tests/BugReport/Capture/FakeBugReportCaptureSources.cs`・`S/Client.Tests/BugReport/Playtest/NullBugReportCaptureSources.cs` | 作る・直す | v7 の L0（後の 3 本は版の数だけ、偽物 2 本は口を 1 つ足すだけ） |
| `S/Client.Tests/Ui/Crosscut/` 一式（`GameUiExceptionLoggingTest`・`PresenterMountTest`・`Localization/`・`Input/`・`TextSelection/`） | 作る | 横断の L1。10 ファイルを超えたらサブディレクトリ |
| `S/Client.Tests/EditModeInPlayingTest/Ui/Crosscut/GameUiScreenMountTest.cs`・`AllScreensLanguageSwitchTest.cs` | 作る | L3（Stage<n> はラダーのレーンの持ち物なので使わない） |
| `S/Client.Ui/Foundation/Layout/UiDisplay.cs`・`UiScrollClipInset.cs` | 作る | DC-11・DC-19 |
| `S/Client.Ui/Foundation/Theme/Mesh/GridMeshIndexWriter.cs`・`Painter2DSquareCap.cs` | 作る | DC-27 |
| `S/Client.Ui/Foundation/Theme/ShadowRingMeshWriter.cs` | 直す | `RingCount`・`CenterMargin` |
| `S/Client.Ui/Foundation/Theme/StyleResolution/UssStyleResolutionGate.cs` | 作る | DC-28 B。`Foundation/Theme/` 直下は USS・txt を含めて 14 ファイルなのでサブディレクトリ（D-7 と同じ数え方） |
| `S/Client.Input/KeyPress/IUiKeyPressSource.cs`・`InputKeyPressSource.cs`・`UiKeyPressSources.cs` | 作る | DC-10（口 1・実装 1・キーごとの型 2） |
| `S/Client.Input/ArrowKeys/InputSystemArrowKeyStream.cs` | 直す | 押下の縁を `InputSuppressionFilter.FilterKeyPress` に通す（A-162 を保つ。`.inputactions`・`InputManager` は触らない） |
| `S/Client.Ui.State/Recipe/Screen/RecipeSelectionCloseReset.cs` | 移す | `Screens/Recipe/Input/` から |
| `S/Client.Ui.State/BuildMenu/BuildMenuScreenVisibility.cs`・`S/Client.Ui.State/Recipe/Screen/RecipeScreenVisibility.cs` | 作る | DC-8 B |
| `S/Client.Ui.State/Recipe/Commands/CraftRecipeUnlockCheck.cs` | 作る | DC-23 A。クラフトのレシピの解放の検査の唯一の判定（`RecipeCommands.Craft` と Web の `CraftActions` が呼ぶ） |
| `S/Client.Ui.State/Text/UiText.cs` | 直す | `UiText.Empty`（T16。I-fix6 が `Join` を足した後の実物） |
| `S/Client.Ui/Content/GameContentPresentation.cs` | 直す | `TryItem` |
| `S/Client.Ui/Components/Controls/{IconButton,PanelActionButton,ModeSwitch}/*.cs`・`.uss` | 直す | 指カーソル・つまみ |
| `S/Client.Ui/Components/Panels/GamePanelSurface/*` | 直す | `SkitChoice` を消し面のつまみをルートへ |
| `S/Client.Ui/Screens/**`（置換の対象は各タスクの Files） | 直す | 直書き・helper・ctor の更新 |
| `S/Client.Ui/Screens/BugReport/BugReportFormSender.cs` | 作らない | 送信の持ち主は X-audit DC-22 の論点で I-fix6 の持ち物（D-24） |
| `S/Client.WebUiHost/Game/**`（T15 の Files） | 直す | State の呼び出しへ付け替え |
| `tools/uitk/webTokens/writeOrCheck.mjs`・`tools/uitk/webTokens/tests/writeOrCheck.test.mjs` | 作る | DC-30 |
| `tools/uitk/convert-web-tokens.mjs`・`convert-mantine-tokens.mjs`・`.github/workflows/run_test.yml`（webui-test に `--check` の step）・`.github/workflows/uitk-web-follow-gate.yml`（自己テストの job に `node --test`） | 直す | `--check` と CI（D-23） |
| `docs/uitk/disposition.md`（`## 処遇` の末尾）・`docs/uitk/TODO.md`（「## 実測の結論」「## 見た目の例外」末尾） | 追記 | R-2・R-17 |
| `../moorestech_logs/harness/uitk/S5-crosscut/done.md` | 作る | 報告 |

---

### Task 0: 前提の照合と骨組み

依存: S4-gamma（実行計画のレーン行）。加えて、同じファイルを先に直すレーン I-fix5・I-fix6 のマージを待つ（D-2）。

**Files:** `../moorestech_logs/harness/uitk/S5-crosscut/done.md`（骨組み）

**Interfaces:** Consumes: `origin/feature/uitk`（S4-gamma・I-fix5・I-fix6 のマージ後）。Produces: 判断記録の「読み替え」行（実物とのずれ）

- [ ] **Step 1: 場所と土台**: `pwd` が自分の worktree。`git fetch origin && git log --oneline --merges origin/feature/uitk | head -20` で S4-gamma・I-fix5・I-fix6 の 3 本のマージを確かめる。無いものがあれば `cmux wait-for uitk-<レーン小文字>`（例 `uitk-s4-gamma`・`uitk-i-fix5`・`uitk-i-fix6`）で 1 本ずつ待ち、来たら `git merge origin/feature/uitk`。合図名が違えば `git log --merges --grep` で確かめて先へ進む
- [ ] **Step 2: Editor と疎通**: `moores-wt status` で自分の worktree の Editor が 1 本だけ（他の worktree の Editor が居れば起動を待つ）。`uloop compile --project-path ./moorestech_client` が通る
- [ ] **Step 3: 実物の照合**: 下の API を実物で開き、名前・引数・行番号を本計画と照合する。ずれは判断記録の末尾「読み替え」に `計画の名前 → 実物の名前（ファイル:行）` で 1 行ずつ書き、以降のタスクはその読み替えで進める
  - I-fix6: `ResearchScreenVisibility`（DC-8 A）と、それを `ResearchTreePresenter`:44・`ResearchTreeModel`:50-51・`ResearchSelectionStore`:29 が呼んでいるか（呼んでいなければ T7 Step 3 で付け替える。I-fix6 の計画 R-16 は 3 か所とも付け替える予定）・`ItemSlotMarks.None`（DC-16 A）・Toast の診断を写した `NotificationHostView`（DC-28 A）・`PanelActionButton`／`HudSectionHeading` の `UiText` 化（DC-17 A）・`BugReportFormText.DescribeDelivery`/`DescribeExport` の戻り型と `UiText.Join`（DC-22 A）・`IBlockInventoryPanelCommands.Close()` の戻り型（DC-13 A）
  - I-fix6 と同じファイルを触る順: `BugReportFormView.cs`・`BugReportFormText.cs`・`BugReportRecoveryControlsView.cs`・`UiText.cs`・`NotificationHostView.cs`・`PanelActionButton.cs`・研究の 3 か所は I-fix6 の取り込み後の実物にだけ当てる。`BugReportFormView.cs` は X-audit DC-22（バグ報告フォームの文字と送信の持ち主）の対象で I-fix6 が持つので、このレーンは送信の形を動かさない。`wc -l` で行数を控え、T17 の AC-5 の 3 件を移せるかを判断記録の「読み替え」に 1 行（D-24）
  - I-fix5: `UiCommandRejectReason.RecipeInvalid`/`RecipeLocked`（改名）・`Client.Ui/Content/MachineRecipeRowContentBuilder.cs`（移動）・`LocalizedLabel` の ctor・`DerivedTokens.uss` の分割先・`PauseMenuView.cs` の行
  - S4-gamma: `tools/uitk/list-recordings.sh`・`run-all-recordings.sh` の引数、ラダー 4・5 のシナリオのパス（`scenarios/uitk/stage4/uitk-production-loop.cs`・`uitk-research-train.cs`）、`UiSystemPlayModeTestBase` の置き場
  - S2-ladder: `scenarios/uitk/stage2/uitk-opening-hud.cs` の有無（無ければ T20 で D-21 の扱い）
  - S5-gate（T13 と同じ 2 型）: S5-gate が先に入っていれば `GameUiPreDiFoundation`（`Client.Starter/Registration/GameUi/Shared/`）が `GameUiSrgbCompositeInstaller`・`GameUiLocaleClassBinder` の `Dispose()` を呼んでいないこと（`git grep -n "Dispose" -- 'moorestech_client/Assets/Scripts/Client.Starter/Registration/GameUi/Shared/GameUiPreDiFoundation.cs'` が 2 型の呼び出しを含まない）。S5-gate の計画は呼ばない形（指揮の指示済み）なので、どちらが先でも T13 の後にコンパイルが通る。呼んでいれば T13 の前に S5-gate の持ち主へ `done.md`「integrator への依頼」で回し、取り込みでその 2 行を消す
  - S5-challenge（T14 の L3 の一覧）: `Screens/Challenge/Tree/ChallengeListView`（S5-challenge が作る）が在るか。在れば T14 Step 2 の一覧に入れる
  - I-fix5 の 10n（録画の置き場）: `scenarios/uitk/` 直下の録画が段ごとのサブディレクトリへ移ったか。移っていれば T20 の全パスを `find .agents/skills/unity-playmode-recorded-playtest/scenarios -name '<名>.cs'` の実物に読み替え、「読み替え」に 1 行ずつ書く
  - 入力（T6）: `.inputactions` の UI マップの `CloseUI` の binding が `<Keyboard>/escape` のままか・`InputSuppressionFilter` が internal のまま `Client.Input` にあるか・`InputSystemArrowKeyStream.PublishEdge` の行
- [ ] **Step 4: 他レーンの状況**: S5-challenge・S5-gate・S5-ladder のマージ状況と、`../moorestech_logs/harness/uitk/*/done.md` の S5-crosscut 宛ての依頼を `grep -rn "S5-crosscut" ../moorestech_logs/harness/uitk/*/done.md` で見直し、増えていれば「## 申し送りの処遇」に行を足す
- [ ] **Step 5: bd と骨組み**: `bd create "UITK S5-crosscut: 横断層の残り（例外の経路・DC 15 件・AC-5・束 5 の横断テスト）" --parent moorestech-albof` → `bd update <id> --claim`。`../moorestech_logs/README.md` を読んでから `harness/uitk/S5-crosscut/done.md` を「状態: 作業中」で作る（見出し: 証拠・PR・割り当ての処遇・差の表・未完了・要裁定・integrator への依頼）
- [ ] **Step 6: コミット**: moorestech_logs 側だけコミット・push（本 repo は変更なし）

---

### Task 1: UI の例外の経路（R-1・R-2）

**Files:**
- Create: `S/Client.Game/InGame/BugReport/GameUiExceptions/GameUiExceptionReading.cs`・`GameUiExceptions/GameUiExceptionTally.cs`・`S/Client.Game/InGame/BugReport/Readings/BugReportManifestReadings.cs`
- Modify: `S/Client.Game/InGame/BugReport/BugReportManifest.cs`（:42 版・:78 の下に欄）・`BugReportBundleWriter.cs`（:69-77 を移す）・`LastSession/CrashBundleWriter.cs`（:58 の下）
- Modify（確保の瞬間に数え手の値を写す。D-1）: `Capture/BugReportCaptureStatus.cs`（`BugReportCapturedData` の :117 `Logs` の下に `public GameUiExceptionReading GameUiExceptions = new GameUiExceptionsUnknown();`、`IBugReportCaptureSources` の :191 `Logs();` の下に `GameUiExceptionReading GameUiExceptions();` の 1 行ずつ。コメントは型の側に書き、196→198 行）・`Capture/BugReportCaptureSources.cs`（ctor に `GameUiExceptionTally`、口は `_tally.Snapshot()`）・`Capture/BugReportCaptureSession.cs`（:60 `data.Logs = …` の下に `data.GameUiExceptions = _sources.GameUiExceptions();`）・`S/Client.Starter/Registration/PlaytestRecordRegistration.cs`（:44 `UnityLogRing` の下に `builder.RegisterEntryPoint<GameUiExceptionTally>().AsSelf();`）・`S/Client.Starter/Registration/UncollectedBugReportCaptureSources.cs`（`new GameUiExceptionsUnknown()` を返す。前例は同ファイルの `WebUiHealth()` の「不明で返す」）
- Modify: `scripts/playtest/dashboard/display_fields.py`・`collect_reports.py`（:18・:77・:126）・`static/js/views/report.js`（:89・:113）
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/playtest-receiver/developer-report-delivery.cs`（:104 の `schemaVersion == 6`）・`developer-report-interrupt.cs`（:29 の `!= 6`）を 7 へ。どちらも `webUiHealth` が `started`・`observedAfterBrowserStart` であることも見るので、流すのは Web の系（T20 Step 3）
- Test: `S/Client.Tests/Ui/Crosscut/GameUiExceptionLoggingTest.cs`・`S/Client.Tests/BugReport/Bundle/GameUiExceptionManifestTest.cs`・`WebUiHealthManifestTest.cs`（:32 の 6→7）・`CrashBundleWebUiHealthTest.cs`（:25 の 6→7）・`S/Client.Tests/RemoteExec/RemoteExecBundleMarkTest.cs`（:70 の 6→7）・`S/Client.Tests/BugReport/Capture/FakeBugReportCaptureSources.cs`（:90 の下に `public GameUiExceptionReading GameUiExceptionsReading = new GameUiExceptionsRecorded(0, Array.Empty<GameUiExceptionSample>());` と口）・`S/Client.Tests/BugReport/Playtest/NullBugReportCaptureSources.cs`（:30 の下に `new GameUiExceptionsUnknown()` を返す口）・`scripts/playtest/tests/test_dashboard_game_ui_exceptions.py`
- Modify: `docs/uitk/disposition.md`（`## 処遇` の末尾に 6 行）
- Modify: `docs/adr/0057-bug-report-bundle-and-isolated-auto-fix.md`（:76 の `webUiHealth` の段落の後に `gameUiExceptions` の契約の段落・:76 末尾の「7 へ上げる」を「8 へ上げる」へ。Step 4b）

**Interfaces:** Consumes: `UnityLogEntry{Time,Tick,Type,Message,StackTrace}`（`BugReport/UnityLogRing.cs`:9-25）・前例の数え手 `UnityLogRing`（`IInitializable` で `Application.logMessageReceivedThreaded` を購読し、`Add(LogType, string, string)` が `GameUpdater.CurrentTick` を付けてロックの中で積む。:29-62）・`BugReportCapturedData.Logs`／`.WebUiHealth`（`Capture/BugReportCaptureStatus.cs`:117・:121）・`IBugReportCaptureSources`（同 :172-195）・`BugReportManifest.AddMissing(string, string)`（:106）・前例 `WebUiHealthReading` の欄ごとの `[JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]`（`WebUiHealth/WebUiHealthReading.cs`:33-37）。Produces: `abstract class GameUiExceptionReading { readonly GameUiExceptionStatus Status; }`・`GameUiExceptionsRecorded(int count, IReadOnlyList<GameUiExceptionSample> samples)`・`GameUiExceptionsUnknown()`・`GameUiExceptionTally : IInitializable`（`SampleLimit`・`Add(LogType type, string message, string stackTrace)`・`Snapshot() : GameUiExceptionsRecorded`・`static TryFindFirstUiFrame(string stackTrace, out string frame)`）・`IBugReportCaptureSources.GameUiExceptions() : GameUiExceptionReading`・`BugReportCapturedData.GameUiExceptions`・`BugReportManifestReadings.Apply(BugReportManifest, BugReportCapturedData)`・`BugReportManifestReadings.MarkGameUiExceptionsUnknown(BugReportManifest, string reason)`・manifest `gameUiExceptions`

- [ ] **Step 1: 例外がログに出ることを固定する L1（赤→緑の確認）**

```csharp
// S/Client.Tests/Ui/Crosscut/GameUiExceptionLoggingTest.cs
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Game.InGame.BugReport;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Crosscut
{
    // AppErrorBoundary の代わりの前提: UI の例外は握られずに LogType.Exception で出て、バグ報告のログ環が拾う
    // Premise standing in for AppErrorBoundary: UI exceptions are never swallowed, surface as LogType.Exception and land in the bug-report log ring
    public class GameUiExceptionLoggingTest : Client.Tests.Ui.Screens.ScreenTestHost
    {
        [Test]
        public void UIのクリックの中の例外はExceptionでログに出て環に入る()
        {
            var ring = new UnityLogRing();
            ring.Initialize();
            var button = Mount(new ThrowingButton());
            LogAssert.Expect(LogType.Exception, new Regex("GameUiExceptionProbe"));
            simulate.Click(button.worldBound.center);
            var entry = ring.Dump().Last(log => log.Type == LogType.Exception);
            StringAssert.Contains("GameUiExceptionProbe", entry.Message);
            StringAssert.Contains("Client.Tests.Ui.Crosscut", entry.StackTrace);
        }

        [Test]
        public void UniRxの購読の中の例外はExceptionでログに出る()
        {
            var ring = new UnityLogRing();
            ring.Initialize();
            var subject = new Subject<Unit>();
            subject.Subscribe(_ => throw new InvalidOperationException("GameUiExceptionProbe rx"));
            LogAssert.Expect(LogType.Exception, new Regex("GameUiExceptionProbe rx"));
            // UniRx は onError 未指定の例外を投げ直す。UI のイベント配送の中で投げ直されたものは UITK がログへ出す
            // UniRx rethrows when no onError is given; rethrown inside UI event dispatch, UITK logs it
            var button = Mount(new Button(() => subject.OnNext(Unit.Default)) { name = "rx-probe" });
            simulate.Click(button.worldBound.center);
            Assert.IsTrue(ring.Dump().Any(log => log.Type == LogType.Exception && log.Message.Contains("GameUiExceptionProbe rx")));
        }

        private sealed class ThrowingButton : Button
        {
            public ThrowingButton() : base(() => throw new InvalidOperationException("GameUiExceptionProbe click"))
            {
                name = "exception-probe";
                style.width = 40;
                style.height = 40;
            }
        }
    }
}
```

`UnityLogRing` が `Client.Tests` から見えない（internal）なら `Client.Game` の AssemblyInfo の `InternalsVisibleTo` を確かめる。`simulate.Click` から例外が外へ抜けて（UITK がログへ出さず）テストが落ちたら、それが実測の結論: 本番では PlayerLoop がログへ出すので経路は保たれるが、台帳「## 実測の結論」に「UITK の配送は例外を握らず投げ直す（EditMode の simulate で観測）」と書き、テストは `Assert.Throws` で配送が投げ直すことを固定する形へ替える。どちらでも「握り潰されない」ことが固定される。

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.Ui.Crosscut.GameUiExceptionLoggingTest"` → 2 件 PASS（または上の実測の形で 2 件 PASS）

- [ ] **Step 2: manifest v7 の L0 を先に書く（赤）**

```csharp
// S/Client.Tests/BugReport/Bundle/GameUiExceptionManifestTest.cs
using System.Linq;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.GameUiExceptions;
using Client.Game.InGame.BugReport.Readings;
using Client.Game.InGame.BugReport.WebUiHealth;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.BugReport.Bundle
{
    // manifest.gameUiExceptions の綴りと数え方を固定する。ダッシュボードはこの語で UI の故障を出す（A-244）
    // Pins manifest.gameUiExceptions' spelling and counting; the dashboard shows UI failures by these words (A-244)
    public class GameUiExceptionManifestTest
    {
        private const string UiStack = "Client.Ui.Screens.Probe.OnClick () (at Assets/Probe.cs:1)\nUnityEngine.UIElements.EventDispatcher.ProcessEvent ()";

        [Test]
        public void Client_Uiを含む例外だけを数え見本は5件まで()
        {
            var tally = new GameUiExceptionTally();
            for (var i = 0; i < 7; i++) tally.Add(LogType.Exception, $"boom {i}", UiStack);
            tally.Add(LogType.Exception, "world", "Client.Game.InGame.World.Tick ()");
            tally.Add(LogType.Error, "ui error", "Client.Ui.Screens.Probe.OnClick ()");
            var recorded = tally.Snapshot();
            Assert.AreEqual(GameUiExceptionStatus.Recorded, recorded.Status);
            Assert.AreEqual(7, recorded.Count);
            Assert.AreEqual(GameUiExceptionTally.SampleLimit, recorded.Samples.Count);
            Assert.AreEqual("boom 0", recorded.Samples[0].Message);
            Assert.AreEqual("Client.Ui.Screens.Probe.OnClick () (at Assets/Probe.cs:1)", recorded.Samples[0].Frame);
        }

        // 環（2000 件で古い順に捨てる）が溢れても、序盤の UI の例外は数と見本に残る（0 件と読ませない）
        // Even after the ring (dropping the oldest past 2000) overflows, an early UI exception stays in the count and samples (never reads as zero)
        [Test]
        public void 環が溢れた後でも序盤の例外が数に残る()
        {
            var ring = new UnityLogRing();
            var tally = new GameUiExceptionTally();
            Log(LogType.Exception, "early ui boom", UiStack);
            for (var i = 0; i < UnityLogRing.Capacity + 1; i++) Log(LogType.Log, $"noise {i}", "");
            Assert.IsFalse(ring.Dump().Any(entry => entry.Message == "early ui boom"), "前提: 環から序盤の例外が押し出されている");
            var recorded = tally.Snapshot();
            Assert.AreEqual(1, recorded.Count);
            Assert.AreEqual("early ui boom", recorded.Samples[0].Message);

            #region Internal

            // 本番と同じく、受けたログを環と数え手の両方へ渡す
            // As in production, every received log goes to both the ring and the tally
            void Log(LogType type, string message, string stackTrace)
            {
                ring.Add(type, message, stackTrace);
                tally.Add(type, message, stackTrace);
            }

            #endregion
        }

        [Test]
        public void UIのフレームが無いスタックは見つからないと返す()
        {
            Assert.IsFalse(GameUiExceptionTally.TryFindFirstUiFrame("Client.Game.InGame.World.Tick ()\nUnityEngine.PlayerLoop ()", out _));
            Assert.IsFalse(GameUiExceptionTally.TryFindFirstUiFrame("Client.Game.InGame.World:Apply (Client.Ui.State.Probe probe)", out _), "引数の型だけに UI が載る行は UI のフレームでない");
            Assert.IsTrue(GameUiExceptionTally.TryFindFirstUiFrame("UnityEngine.Foo ()\n  Client.Ui.State.Probe.Run () ", out var frame));
            Assert.AreEqual("Client.Ui.State.Probe.Run ()", frame);
        }

        [Test]
        public void 例外が無ければrecordedで0件()
        {
            var manifest = new BugReportManifest();
            BugReportManifestReadings.Apply(manifest, new BugReportCapturedData { GameUiExceptions = new GameUiExceptionTally().Snapshot(), WebUiHealth = new WebUiHealthReading() });
            var json = JObject.Parse(manifest.ToJson());
            Assert.AreEqual(7, (int)json["schemaVersion"]);
            Assert.AreEqual("recorded", (string)json["gameUiExceptions"]["status"]);
            Assert.AreEqual(0, (int)json["gameUiExceptions"]["count"]);
            Assert.AreEqual(0, ((JArray)json["gameUiExceptions"]["samples"]).Count);
            Assert.IsFalse(manifest.Missing.Any(item => item.Item == "gameUiExceptions"));
        }

        [Test]
        public void 確保時に数を取れなければunknownで件数の欄を持たず欠損に書く()
        {
            var manifest = new BugReportManifest();
            BugReportManifestReadings.Apply(manifest, new BugReportCapturedData { GameUiExceptions = new GameUiExceptionsUnknown(), WebUiHealth = new WebUiHealthReading() });
            var json = JObject.Parse(manifest.ToJson());
            Assert.AreEqual("unknown", (string)json["gameUiExceptions"]["status"]);
            Assert.IsNull(json["gameUiExceptions"]["count"], "unknown は件数の欄を持たない（0 件と読ませない）");
            Assert.IsNull(json["gameUiExceptions"]["samples"]);
            Assert.IsTrue(manifest.Missing.Any(item => item.Item == "gameUiExceptions"));
        }

        [Test]
        public void 新しいmanifestの既定はunknown()
        {
            var json = JObject.Parse(new BugReportManifest().ToJson());
            Assert.AreEqual("unknown", (string)json["gameUiExceptions"]["status"]);
        }
    }
}
```

Run: `uloop compile …` → コンパイルエラー（型が無い）を確かめる

- [ ] **Step 3: 読み取りを作る（契約・数え手・詰める口の 3 本）**

契約は判別 union にする。「数えた（recorded）」と「数えていない（unknown）」を型で分け、unknown は件数も見本も持たない（null と判別子で同じことを 2 通りに表さない）。JSON の判別子 `status` の綴りは、manifest の `Settings`（`BugReportManifest.cs`:20-25）が enum の変換器を持たないので、前例 `WebUiHealthReading`（:33-37）と同じく欄に属性を付けて契約にする。

```csharp
// S/Client.Game/InGame/BugReport/GameUiExceptions/GameUiExceptionReading.cs
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Client.Game.InGame.BugReport.GameUiExceptions
{
    public enum GameUiExceptionStatus
    {
        Recorded,
        Unknown,
    }

    /// <summary>
    /// manifest.gameUiExceptions の契約。数えた箱と数えていない箱を型で分け、数えていない箱は件数も見本も持たない（Web の AppErrorBoundary の代わり。A-244）
    /// The manifest.gameUiExceptions contract; counted and uncounted boxes are separate types and an uncounted box carries neither count nor samples (stands in for the Web AppErrorBoundary; A-244)
    /// </summary>
    public abstract class GameUiExceptionReading
    {
        // JSON の判別子。綴りは webUiHealth と同じく欄の属性で camelCase に固定する（manifest の Settings は enum の変換器を持たない）
        // The JSON discriminator; spelled camelCase by a per-field attribute as in webUiHealth (the manifest Settings carry no enum converter)
        [JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
        public readonly GameUiExceptionStatus Status;

        protected GameUiExceptionReading(GameUiExceptionStatus status)
        {
            Status = status;
        }
    }

    // セッション通算で数えた箱。0 件は「数えて無かった」という実値
    // A box counted over the whole session; zero is the real value "counted, none found"
    public sealed class GameUiExceptionsRecorded : GameUiExceptionReading
    {
        public readonly int Count;
        public readonly IReadOnlyList<GameUiExceptionSample> Samples;

        public GameUiExceptionsRecorded(int count, IReadOnlyList<GameUiExceptionSample> samples) : base(GameUiExceptionStatus.Recorded)
        {
            Count = count;
            Samples = samples;
        }
    }

    // 数えていない箱（確保時に数え手の値が無い・前回セッションのクラッシュ）。理由は manifest の missing が持つ
    // An uncounted box (no tally value at capture, previous-session crash); the reason lives in the manifest's missing list
    public sealed class GameUiExceptionsUnknown : GameUiExceptionReading
    {
        public GameUiExceptionsUnknown() : base(GameUiExceptionStatus.Unknown)
        {
        }
    }

    public sealed class GameUiExceptionSample
    {
        public readonly ulong Tick;
        public readonly string Message;
        public readonly string Frame;

        public GameUiExceptionSample(ulong tick, string message, string frame)
        {
            Tick = tick;
            Message = message;
            Frame = frame;
        }
    }
}
```

```csharp
// S/Client.Game/InGame/BugReport/GameUiExceptions/GameUiExceptionTally.cs
using System.Collections.Generic;
using Core.Update;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.BugReport.GameUiExceptions
{
    /// <summary>
    /// ログを受けた時点で UI（Client.Ui・Client.Ui.State）の例外をセッション通算で数える。2000 件で古い順に捨てる UnityLogRing から数えると、序盤の例外が押し出されて 0 件に見えるため（A-244）
    /// Counts UI (Client.Ui, Client.Ui.State) exceptions for the whole session as each log arrives; counting from UnityLogRing, which drops the oldest past 2000, would push early exceptions out and read as zero (A-244)
    /// </summary>
    public sealed class GameUiExceptionTally : IInitializable
    {
        public const int SampleLimit = 5;

        // スタックの行に載る UI の名前空間の頭（Client.Ui. は Client.Ui.State. も含む）
        // The UI namespace prefix found on stack lines (Client.Ui. also covers Client.Ui.State.)
        private const string UiNamespaceMarker = "Client.Ui.";

        // どのスレッドのログも受けるためロックで守る（前例 UnityLogRing）
        // Locked because logs arrive from any thread (precedent UnityLogRing)
        private readonly object _lock = new();
        private readonly List<GameUiExceptionSample> _samples = new(SampleLimit);
        private int _count;

        public void Initialize()
        {
            Application.logMessageReceivedThreaded += OnLogReceived;
        }

        public void Add(LogType type, string message, string stackTrace)
        {
            // 例外の種別で、スタックに UI のフレームが載るものだけ（UI の購読・コールバックが止まった印）
            // Only exceptions whose stack carries a UI frame (the sign that a UI subscription or callback broke)
            if (type != LogType.Exception || !TryFindFirstUiFrame(stackTrace, out var frame)) return;
            lock (_lock)
            {
                _count++;
                if (_samples.Count < SampleLimit) _samples.Add(new GameUiExceptionSample(GameUpdater.CurrentTick, message, frame));
            }
        }

        // 確保の瞬間の写し。件数はセッション通算、見本は先頭 5 件
        // The capture-moment copy; the count covers the whole session and the samples are the first five
        public GameUiExceptionsRecorded Snapshot()
        {
            lock (_lock)
            {
                return new GameUiExceptionsRecorded(_count, new List<GameUiExceptionSample>(_samples));
            }
        }

        // UI の例外かの唯一の判定。見つかれば最初の UI のフレームの行を返す。行頭（呼ばれたメソッドの型）だけを見て、引数の型に UI の型が載る行を数えない
        // The single test for a UI exception; when found, returns the first UI frame line. Only the line head (the called method's type) counts, so a line merely taking a UI type as an argument does not
        public static bool TryFindFirstUiFrame(string stackTrace, out string frame)
        {
            foreach (var line in stackTrace.Split('\n'))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith(UiNamespaceMarker)) continue;
                frame = trimmed;
                return true;
            }
            frame = null;
            return false;
        }

        private void OnLogReceived(string condition, string stackTrace, LogType type)
        {
            Add(type, condition, stackTrace);
        }
    }
}
```

```csharp
// S/Client.Game/InGame/BugReport/Readings/BugReportManifestReadings.cs
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.GameUiExceptions;
using Client.Game.InGame.BugReport.WebUiHealth;
using UnityEngine;

namespace Client.Game.InGame.BugReport.Readings
{
    /// <summary>
    /// 確保データから manifest の読み取り欄（webUiHealth・gameUiExceptions）を詰める。数えられなかった UI の例外は MarkGameUiExceptionsUnknown の 1 本で不明と欠損を組で書く
    /// Fills the manifest's reading fields (webUiHealth, gameUiExceptions) from captured data; uncountable UI exceptions go through MarkGameUiExceptionsUnknown alone, writing unknown and the gap together
    /// </summary>
    public static class BugReportManifestReadings
    {
        public static void Apply(BugReportManifest manifest, BugReportCapturedData data)
        {
            ApplyWebUiHealth();
            ApplyGameUiExceptions();

            #region Internal

            void ApplyWebUiHealth()
            {
                // 確保が箱を埋め損ねたら unknown を入れ、常時非 null の契約を守りつつ欠損と開発者ログへ残す
                // If the capture left the box empty, unknown is recorded so the always-non-null contract holds, with the gap in the manifest and the developer log
                if (data.WebUiHealth == null)
                {
                    Debug.LogError("[BugReport] 確保データにWebホスト・CEFの値が無い。webUiHealth は unknown で出す");
                    manifest.WebUiHealth = WebUiHealthReading.UnknownForPreviousSession();
                    manifest.AddMissing("webUiHealth", "確保データにWebホスト・CEFの値が無い");
                    return;
                }
                manifest.WebUiHealth = data.WebUiHealth;
            }

            void ApplyGameUiExceptions()
            {
                // 確保時に数え手の値を取れなかった箱（記録を集めない起動など）は数えていない。0 件と読ませない
                // A box whose capture got no tally value (such as a boot that collects no records) was not counted; never let it read as zero
                if (data.GameUiExceptions.Status == GameUiExceptionStatus.Unknown)
                {
                    MarkGameUiExceptionsUnknown(manifest, "確保時に UI の例外の数を取れなかった（記録を集めない起動）");
                    return;
                }
                manifest.GameUiExceptions = data.GameUiExceptions;
            }

            #endregion
        }

        // 数えていない箱の唯一の書き方。0 件と読ませないよう、不明の値と欠損の行を必ず組で書く（欠損は AddMissing が開発者ログにも出す）
        // The only way to write an uncounted box; unknown and the gap entry always go together so it never reads as zero (AddMissing also logs it for developers)
        public static void MarkGameUiExceptionsUnknown(BugReportManifest manifest, string reason)
        {
            manifest.GameUiExceptions = new GameUiExceptionsUnknown();
            manifest.AddMissing("gameUiExceptions", reason);
        }
    }
}
```

- [ ] **Step 4: manifest と 2 つの Writer**: `BugReportManifest.cs` の `SchemaVersion = 6` を `7` にし、その上の版の注記に `// v7: gameUiExceptions（UI の例外の数と見本。数えていない箱は status だけの unknown）を常時非nullで追加` と英訳の 1 行を足す。`WebUiHealth` の欄（:76）の下に次の 3 行を足す（`using Client.Game.InGame.BugReport.GameUiExceptions;` も）。判別子の綴りは型の側の属性が決める（`WebUiHealthReading` と同じく欄に属性を付ける形。manifest の `Settings` には足さない）。Newtonsoft は宣言型でなく実行時の型（`GameUiExceptionsRecorded`／`GameUiExceptionsUnknown`）の欄を書くので、unknown の箱には `count`・`samples` が出ない（Step 2 の 5 件目が見る）

```csharp
        // UI の例外の数と見本（A-244。Web の AppErrorBoundary の代わり）。v7 で追加、常に非 null。既定は数えていない箱
        // Count and samples of UI exceptions (A-244, standing in for the Web AppErrorBoundary); added in v7, never null, defaulting to an uncounted box
        public GameUiExceptionReading GameUiExceptions = new GameUiExceptionsUnknown();
```

`BugReportBundleWriter.cs` の :69-77（コメント 2 行と if/else）を `BugReportManifestReadings.Apply(manifest, data);` の 1 行へ置き換え、`using Client.Game.InGame.BugReport.Readings;` を足す（`WebUiHealthReading` の using が使われなくなれば消す。200 行→192 行前後）。`CrashBundleWriter.cs` の :58 の下に（`using Client.Game.InGame.BugReport.Readings;` も）:

```csharp
            // 落ちたセッションのログは前回の Player.log にしか無く、ここでは数えない。0 件と読ませないよう不明と欠損を組で書く
            // The crashed session's log lives only in the previous Player.log and is not counted here; write unknown and the gap together so it never reads as zero
            BugReportManifestReadings.MarkGameUiExceptionsUnknown(manifest, "前回セッションの UI の例外は数えていない");
```

確保の口（D-1。前例は同じ箱の `Logs`・`WebUiHealth` の写し方）: `IBugReportCaptureSources` の :191 `Logs();` の下に `GameUiExceptionReading GameUiExceptions();`、`BugReportCapturedData` の :117 `Logs` の下に `public GameUiExceptionReading GameUiExceptions = new GameUiExceptionsUnknown();` を 1 行ずつ足す（`BugReportCaptureStatus.cs` は 196→198 行。コメントは型の側に書いてここには足さない。`wc -l` が 200 を超えたら止めて `done.md`「要裁定」へ）。`BugReportCaptureSources` は ctor の最後に `GameUiExceptionTally tally` を受け、`public GameUiExceptionReading GameUiExceptions() { return _tally.Snapshot(); }`。`BugReportCaptureSession` の :60 の下に `data.GameUiExceptions = _sources.GameUiExceptions();`（`Logs` と同じ確保の瞬間に写す）。`PlaytestRecordRegistration` の :44 の下に `builder.RegisterEntryPoint<GameUiExceptionTally>().AsSelf();`（:42 のコメントの「ログリング・録画リング」に「UI の例外の数え手」を足す）。`UncollectedBugReportCaptureSources` は `WebUiHealth()` と同じ形で `new GameUiExceptionsUnknown()` を返す（この起動は数えない。確保の起点が無いので本来は呼ばれない）。テストの偽物 `FakeBugReportCaptureSources` は既定 `new GameUiExceptionsRecorded(0, Array.Empty<GameUiExceptionSample>())` の欄を返し、`NullBugReportCaptureSources` は `new GameUiExceptionsUnknown()` を返す。`BugReportDependencyCycleTest` の対応表は数え手が依存を持たないので変えない

版の照合: `WebUiHealthManifestTest.cs`:32・`CrashBundleWebUiHealthTest.cs`:25・`RemoteExecBundleMarkTest.cs`:70 の `Assert.AreEqual(6, …schemaVersion…)` と、録画シナリオ `playtest-receiver/developer-report-delivery.cs`:104 の `== 6`・`developer-report-interrupt.cs`:29 の `!= 6`（例外の文 `schemaVersion is not 6` も）を `7` に直す。`git grep -n -e "schemaVersion\"\]" -e "SchemaVersion" -- moorestech_client scripts .agents/skills/unity-playmode-recorded-playtest/scenarios` で 6 を見ている他の照合を探し、manifest の版を読むもの（`digest_schema.py` の `schemaVersion` は record.json の版で別物）だけ直す。2 本のシナリオは `webUiHealth.hostStartup == "started"`・`cefProcess == "observedAfterBrowserStart"` も見るので、GameUi の系（Web ホストは `SkippedByEditorSwitch`。`WebUiStartup.cs`:19-27）では通らない。流すのは T20 Step 3 の Web の系

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "GameUiExceptionManifestTest|WebUiHealthManifestTest|CrashBundleWebUiHealthTest|BugReportBundleWriterIdentityTest|RemoteExecBundleMarkTest|UnityLogRingTest|BugReportDependencyCycleTest|BugReportCapture"` → 全件 PASS

- [ ] **Step 4b: ADR 0057 の契約**: manifest の欄を足したレーンは同じ作業の中で ADR 0057 に契約の段落を足す（前例: `remoteExec` は :74、`webUiHealth` は :76 を `9d0c8e1cb4` で足した）。`docs/adr/0057-bug-report-bundle-and-isolated-auto-fix.md` の :76 の段落の後に 1 段落を足す: 「manifest の `gameUiExceptions` は常時存在し（SchemaVersion 7）、`{"status":"recorded","count":<整数>,"samples":[{tick, message, frame} を最大 5 件]}` か `{"status":"unknown"}` とする。unknown は count・samples の欄を持たず、`missing` の `gameUiExceptions` の行と組で書く。クラッシュの箱は常に unknown。件数は `GameUiExceptionTally` がログを受けた時点からのセッション通算で、Unity ログの環からは数えない（環が溢れても 0 件にしない）」。同じ :76 の末尾「そのとき SchemaVersion を 7 へ上げる」を「そのとき SchemaVersion を 8 へ上げる（7 は S5-crosscut の gameUiExceptions が使った）」へ直す。確かめ: `grep -n "gameUiExceptions\|SchemaVersion を 8" docs/adr/0057-bug-report-bundle-and-isolated-auto-fix.md` が 2 行以上
- [ ] **Step 5: ダッシュボード**: `display_fields.py` の末尾に `game_ui_exceptions(raw, source)` を足す（`web_ui_health` と同じ 4 状態。`"gameUiExceptions" not in raw` → `{"status": "absent"}`／dict でない・`status` が `recorded`・`unknown` 以外・`count` が int でない recorded → `warn` して `{"status": "unreadable"}`／`unknown` → `{"status": "unknown"}`／recorded → `{"status": "recorded", "count": n, "samples": [{"tick", "message", "frame"} …最大 5]}`）。`collect_reports.py` の :18 の import に足し、:77 の既定の行に `"gameUiExceptions": {"status": "unreadable"},`、:126 の下に `"gameUiExceptions": game_ui_exceptions(raw, source),`。`report.js` に `gameUiExceptionRows(value)` を `webUiHealthRows`（:89）の形で足し、:113 の並びに加える（欄なし＝「UI の例外: 記録なし（v6 以前）」・不明・読めない・`N 件`＋見本の `frame` を 1 行ずつ）
- [ ] **Step 6: ダッシュボードのテスト**: `scripts/playtest/tests/test_dashboard_game_ui_exceptions.py` を `test_dashboard.py`:93-110（`test_web_ui_health_status_…`）の土台（`add_report`・`reports_by_id`）を import して書く。4 件: 欄なし→absent、unknown→unknown、`{"status":"recorded","count":"x"}`→unreadable、`{"status":"recorded","count":2,"samples":[…]}`→recorded と count 2

Run: `python3 -m unittest discover -s scripts/playtest/tests -p 'test_dashboard*.py' -v` → 全件 OK

- [ ] **Step 7: disposition の 6 行**: `docs/uitk/disposition.md` の `## 処遇` の末尾に、列 `| 束 | Web のファイル | Web のテスト名 | 処遇 | 証跡のパスか理由 | PR |` で（「Web のファイル」は webui 相対をバッククォートで囲む。`tools/uitk/testNameParity/disposition.mjs`:27,34 が囲まない列を書式の誤りとして落とす。前例は同ファイル :86-92 の行）:
  - ``| 5 | `e2e/tests/regression/connection.spec.ts` | 切断後は全topic snapshotをrestoring中に復元し旧世代snapshotを破棄する | 捨てる | UITK には Web↔C# の WS が無く切断・復元の状態が起きない（設計書 A-38・実行計画 E-17） | <PR> |``
  - ``| 5 | `e2e/tests/regression/connection.spec.ts` | inventory未受信中はconnecting placeholderを表示する | 捨てる | UITK に「未受信」の状態が無い（設計書 A-13） | <PR> |``
  - ``| 5 | `e2e/tests/regression/connection.spec.ts` | WS切断時は再接続バナーをz-reconnect層に表示する | 捨てる | 同 A-38・E-17 | <PR> |``
  - ``| 5 | `src/shared/i18n/allScreensI18n.test.ts` | fetches fallback only once while also loading source for the fallback locale | 捨てる | UITK の辞書は起動時に同期で読み込み、HTTP 取得・取得の世代・topic 待ちが無い（設計書 A-38） | <PR> |``
  - ``| 5 | `src/shared/i18n/allScreensI18n.test.ts` | waits for the localization topic before the first dictionary fetch | 捨てる | 同上（A-38） | <PR> |``
  - ``| 5 | `src/shared/i18n/allScreensI18n.test.ts` | keeps the newest locale when StrictMode cleanup aborts an older dictionary generation | 捨てる | 同上（A-38） | <PR> |``
  - `connection.spec.ts` が束 5 でないとツールが言えば（Step 8 の出力で分かる）、その 3 行は束の列をツールの束に合わせる
- [ ] **Step 8: 突き合わせ**: `node tools/uitk/test-name-parity.mjs --bundle 5 --emit-missing > moorestech_client/.uloop/outputs/compare/S5-crosscut/parity-t1.txt; echo "exit=$?"`。出力に Step 7 の 6 件が無い（書式誤りの行が出たら直す）
- [ ] **Step 9: コミット**: `git add` した範囲で `git commit -m "feat(uitk): UI の例外を manifest v7 の gameUiExceptions へ数え、作らない横断層を disposition に記帳（S5-crosscut T1）"`

---

### Task 2: DC-11 表示の出し入れを `UiDisplay` 1 本へ（R-4）

**Files:**
- Create: `S/Client.Ui/Foundation/Layout/UiDisplay.cs`、Test: `S/Client.Tests/Ui/Layout/UiDisplayTest.cs`（置き場は既存の Layout の L1 `UiLayoutTest.cs`・`UiGroupWrapTest.cs` の横。`Client.Tests/Ui/Foundation/` は無く、`UiGapContainer` の L1 も無い）
- Modify（helper 4）: `Screens/BlockInventory/Special/SpecialViewParts.cs`:32・`Screens/Settings/SettingsSectionParts.cs`:32・`Screens/BugReport/BugReportRecoveryControlsView.cs`:63（I-fix6 の取り込み後の行）・`Screens/Skit/Parts/Toolbar/SkitToolbar.cs`:90（と各呼び手）
- Modify（Foundation。S 相対 `Client.Ui/`）: `Foundation/Layout/UiGapContainer.cs`:57（書き）・:102（読み）・`Foundation/Layers/UiLayerStack.cs`:70,77（`SetAllLayersVisible`。隠す前の `Blur` は残す。A-80 の持ち物だが、規則〈出すときは Null〉が同じなので `UiDisplay.SetShown` へ委ねる）
- Modify（Components）: `Components/Controls/RecipeActionButton/RecipeActionLabelShadow.cs`:63・`Components/Controls/GameTextField/ImeCompositionUnderline.cs`:35,60,82・`Components/Text/ShortcutHintLabel/ShortcutHintLabel.cs`:79（書き）・:89（読み）
- Modify（Screens。`Client.Ui/Screens/` 相対）: Research `ResearchTreeView`:90・`Parts/ResearchDetailActions`:51・`Parts/ResearchDetailPane`:97,109・`Parts/ResearchConsumeItemsSection`:41,52／CommonHud `CrosshairHudView`:36／CursorTooltip `CursorTooltipView`:34,49／Skit `Parts/Choices/SkitChoiceList`:31,38・`SkitTransitionView`:16,21・`Parts/Window/SkitDialogueWindow`:67,82・`Parts/Window/SkitBackgroundLine`:19,29／Tutorial `DragGuide/TutorialDragGuideView`:42,54・`WorldPin/WorldPinOverlayView`:40,54・`WorldPin/WorldPinEdgeArrowView`:46,51・`Overlay/TutorialOutlineView`:64,99・`Overlay/TutorialHighlightLabelView`:36,45・`KeyControlHint/KeyControlHintView`:33,44／TrainHud `TrainRidingHudView`:52／BlockInventory `BlockInventoryView`:66,71,114,127・`Train/TrainInventoryBodyView`:48,49・`Train/TrainInventoryContentsView`:30,31・`Train/Timetable/TrainTimetableAreaView`:65,66／Progress `ProgressHudView`:36／KeyHints `KeyHintHudView`:33,39／Notification `NotificationRowView`:56,60／Recipe `ItemListView`:88・`RecipeViewerView`:59,60,106／Shell `ScreenBackdropView`:23,35／Hotbar `HotbarHudView`:66／PauseMenu `PauseMenuTopPageView`:61・`PauseMenuView`:71,78／Inventory `Panel/InventoryView`:91・`Equipment/EquipmentHudView`:51／BuildMenu `Parts/BuildMenuPanelSkeleton`:81,82・`BuildMenuView`:58,95,103
- 数え: 起草時の実測は `git grep -nE "style\.display[[:space:]]*=[^=]" -- 'moorestech_client/Assets/Scripts/Client.Ui/*.cs'` で 76 行・44 ファイル。上の書き 74 行（helper 4＋Foundation 3＋Components 5＋Screens 62）と取り込み後の 2 行で全部。読み（`==`）は `UiGapContainer`:102・`ShortcutHintLabel`:89 の 2 行
- 取り込み後（D-20）: `Screens/Challenge/CurrentHud/CurrentChallengeHudView`:38,43 と、S5-challenge・S5-gate が新設した画面の直書き

**Interfaces:** Consumes: `UiGapContainer.SetItemShown`（`Foundation/Layout/UiGapContainer.cs`:53-59。既に `StyleKeyword.Null` の規則）。Produces: `UiDisplay.SetShown(VisualElement element, bool shown)`・`UiDisplay.IsShownInline(VisualElement element)`

- [ ] **Step 1: L1（赤）**

```csharp
// S/Client.Tests/Ui/Layout/UiDisplayTest.cs
using Client.Ui.Foundation.Layout;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Layout
{
    public class UiDisplayTest : Client.Tests.Ui.TestHost.UiTestHost
    {
        [Test]
        public void 隠すとインラインのnoneが付く()
        {
            var element = new VisualElement();
            UiDisplay.SetShown(element, false);
            Assert.AreEqual(DisplayStyle.None, element.style.display.value);
            Assert.AreEqual(StyleKeyword.Undefined, element.style.display.keyword);
        }

        [Test]
        public void 出すとインラインが外れUSSの値に戻る()
        {
            var element = new VisualElement();
            UiDisplay.SetShown(element, false);
            UiDisplay.SetShown(element, true);
            Assert.AreEqual(StyleKeyword.Null, element.style.display.keyword);
            Assert.IsTrue(UiDisplay.IsShownInline(element));
        }

        // 部品の USS が状態クラスで隠している要素は、画面が出しても隠れたまま（Flex を当てると状態クラスに勝ってしまうのが DC-11 の症状）
        // An element hidden by its component USS state class stays hidden when the screen shows it (inline Flex beating the class was the DC-11 symptom)
        [Test]
        public void 状態クラスのnoneは出しても負けない()
        {
            var sheet = UnityEngine.ScriptableObject.CreateInstance<StyleSheet>();
            Assert.IsTrue(UiTestStyleSheets.TryCompile(".probe--hidden { display: none; }", sheet));
            rootVisualElement.styleSheets.Add(sheet);
            var element = new VisualElement();
            element.AddToClassList("probe--hidden");
            rootVisualElement.Add(element);
            UiDisplay.SetShown(element, true);
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.None, element.resolvedStyle.display);
        }
    }
}
```

`UiTestHost` の名前空間と、テストで USS を文字列からコンパイルする既存の口（`UiTestStyleSheets.TryCompile` は仮の名前）を `git grep -n "StyleSheet>()\|ImportStyleSheet\|CompileStyleSheet" -- moorestech_client/Assets/Scripts/Client.Tests` で探して実物の名前に読み替える。口が無ければ 3 件目は `.uss` を `Client.Tests` の既存のテスト用 USS の横に置いて読み込む形（前例を grep で探す）にし、新しい口は作らない。

- [ ] **Step 2: USS の既定 none の照合**: 置換する要素ごとに、その要素のクラスが USS で既定 `display: none` になっていないかを照合する。`grep -rn -B3 "display: none" --include='*.uss' moorestech_client/Assets/Scripts/Client.Ui` の一覧（起草時 21 か所: `ResearchTree.uss`:55 `.research-tree__card-ring`・`UiModalHost.uss`:6・`MachineRecipeSelection.uss`:43・`GrabOverlay.uss`:6・`GamePanel.uss`:85,129 ほか部品内の状態クラス）と、上の置換対象の要素のクラスを突き合わせ、重なる要素は「USS の既定 none を外し、出し入れは `UiDisplay.SetShown` だけ」か「状態クラスの付け外し（`EnableInClassList`）」に揃える。どちらにしたかを判断記録 D-5 の下に 1 行ずつ書く
- [ ] **Step 3: 作る**

```csharp
// S/Client.Ui/Foundation/Layout/UiDisplay.cs
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Layout
{
    /// <summary>
    /// 要素の出し入れの唯一の口。隠すときはインラインの none、出すときはインラインを外して USS（状態クラスの none を含む）へ戻す（A-245）
    /// The single entry for showing and hiding; hiding sets inline none, showing removes the inline value so USS (including state-class none) applies again (A-245)
    /// </summary>
    public static class UiDisplay
    {
        public static void SetShown(VisualElement element, bool shown)
        {
            // 出すときに Flex を当てると部品の状態クラスの none に勝ってしまうので、Null で USS に任せる
            // Showing with inline Flex would beat a component's state-class none, so Null hands the value back to USS
            element.style.display = shown ? new StyleEnum<DisplayStyle>(StyleKeyword.Null) : new StyleEnum<DisplayStyle>(DisplayStyle.None);
        }

        // インラインで隠していないか（配置前でも読める。USS の結果は resolvedStyle で読む）
        // Whether no inline hide is set (readable before layout; read USS results through resolvedStyle)
        public static bool IsShownInline(VisualElement element)
        {
            return element.style.display.keyword == StyleKeyword.Null || element.style.display.value != DisplayStyle.None;
        }
    }
}
```

`UiGapContainer.SetItemShown`（:53-59）の本体は `UiDisplay.SetShown(item, shown)` を呼ぶ形へ、:102 の読みは `if (!UiDisplay.IsShownInline(item)) continue;` へ替える（規則が 1 か所になる）。`UiLayerStack.SetAllLayersVisible`（:64-78）は `if (!visible) _root.focusController.focusedElement?.Blur(); UiDisplay.SetShown(_root, visible);` の形にする（隠すときだけ先に外す順はそのまま）

- [ ] **Step 4: 置換**: 4 つの helper を消して呼び手を `UiDisplay.SetShown` へ。Files の直書き 74 か所（`style.display = x ? DisplayStyle.Flex : DisplayStyle.None` と、出す/隠すの 2 行に分かれたもの）を `UiDisplay.SetShown(<要素>, <真偽>)` へ。読む側（`ShortcutHintLabel`:89 など `style.display.value == DisplayStyle.Flex` を読むもの）は `UiDisplay.IsShownInline` へ。`SpecialViewParts.IsDisplayedInHierarchy` は読みの関数で別物なので残す
- [ ] **Step 5: 検査**: `git grep -nE "style\.display[[:space:]]*=[^=]" -- 'moorestech_client/Assets/Scripts/Client.Ui/*.cs'` の結果が `Foundation/Layout/UiDisplay.cs` の 1 行と、取り込み前の S5-challenge の持ち物 `Screens/Challenge/CurrentHud/CurrentChallengeHudView.cs`:38,43 だけ（R-4 と同じ基準。S5-challenge・S5-gate が先に入って増えた画面の行は D-20 で同じ置換を当てて消す。残したパスは `done.md` に書く）。`git grep -nE "style\.display[[:space:]]*==" -- 'moorestech_client/Assets/Scripts/Client.Ui/*.cs'` は 0 行（`\s` は git grep の ERE で効かず 0 行に見えるので使わない）。テストが `style.display.value == DisplayStyle.Flex` を見ている所は `git grep -n "DisplayStyle.Flex" -- moorestech_client/Assets/Scripts/Client.Tests` で探し、`resolvedStyle.display` か `UiDisplay.IsShownInline` に直す（`style.display.value` は Null のとき既定の Flex を返すので多くはそのまま通る）

Run: `uloop compile …` → `uloop run-tests … --filter-type regex --filter-value "^Client\.Tests\.Ui\."`（全 UI の L1。既定 600 秒で足りなければ `--timeout-seconds 1500`）→ 失敗 0

- [ ] **Step 6: コミット**: `feat(uitk): 表示の出し入れを UiDisplay.SetShown の 1 本へ寄せる（DC-11。S5-crosscut T2）`

---

### Task 3: DC-19 スクロールのクリップ逃げを `UiScrollClipInset` 1 本へ（R-5）

**Files:**
- Create: `S/Client.Ui/Foundation/Layout/UiScrollClipInset.cs`、Test: `S/Client.Tests/Ui/Layout/UiScrollClipInsetTest.cs`
- Modify: `Screens/Recipe/Parts/RecipeEntryList.cs`（:31,124-149。:142 の `_scroll.Q<GameScrollView>().contentContainer` の直書き）・`Components/Controls/RecipeListScrollView/RecipeListScrollView.cs`（:12・:27 のローカル変数 `scroll` をフィールド `_scroll` へ）・`Screens/BuildMenu/CategoryScroll/BuildMenuScrollClipInset.cs`（呼び手 `BuildMenuScrollBinding.cs`:23）・`Components/NodeGraph/NodeGraphView/NodeGraphView.cs`（:22,140-159）・`Screens/BlockInventory/BlockInventoryNaturalScroll.cs`（:14,30-41）・`Screens/Research/Parts/ResearchCalcKnobs.cs`（:32 `ApplyScrollBleed`。左右だけ）
- 置き換えない（D-6）: `Screens/Recipe/ItemListView.cs`:106-127 の `ApplyClipClearance`（左・上・下の 3 辺で右を持たず、margin に実測の基準値〈`--…-margin-left-base`・`-top-base`〉を足す。Web `features/recipe/panels/ItemListPanel.module.css`:32-55 も同じ 3 辺と基準値で、右は `margin-right: 4.435px` でつまみの位置を詰める。共有の式に入れると右の margin と padding が増え、縦のつまみが inset px 右へずれる）

**Interfaces:** Consumes: `GameScrollView`（`ctor(string testid)`・`contentContainer`・`OnContentLayoutChanged`）。Produces: `UiScrollClipInset.Apply(VisualElement outer, VisualElement inner, float inset)`（4 辺）・`UiScrollClipInset.ApplyOuterMargin(VisualElement outer, float inset)`・`UiScrollClipInset.ApplyInnerPadding(VisualElement inner, float inset)`（外と内が別の持ち主のとき）・`UiScrollClipInset.ApplyHorizontal(VisualElement outer, VisualElement inner, float inset)`（左右だけ）・`RecipeListScrollView.SetClipPadding(float inset)`

- [ ] **Step 1: 実装を読み、式を表にする**: 各実装の「外側に負の margin・内側に正の padding」の向き（4 辺か左右だけか）・元の margin を足すか・margin と padding を当てる要素が同じ持ち主か・つまみ名（USS の `--*-clip-inset`）を判断記録 D-6 に表で書く。起草時の実測（評価の指摘を実物で確かめた形）:

| 実装 | 外の margin | 内の padding | 共有の口 |
|---|---|---|---|
| `BuildMenuScrollClipInset`:28-38 | 4 辺 −inset（`scroll`） | 4 辺 inset（`scroll.contentContainer`） | `Apply`（ファイルを消し、呼び手から直に呼ぶ） |
| `NodeGraphView`:150-157 | 4 辺 −inset（自分） | 4 辺 inset（自分） | `Apply(this, this, inset)` |
| `BlockInventoryNaturalScroll`:33-40 | 4 辺 −bleed（`Root`） | 4 辺 bleed（`Root.contentContainer`） | `Apply` |
| `ResearchCalcKnobs.ApplyScrollBleed`:37-40 | 左右 −bleed | 左右 bleed | `ApplyHorizontal` |
| `RecipeEntryList`:137-147 | 4 辺 −inset（自分）、上だけ `gap − inset`（Web の Stack の margin-top gap と打ち消しの −inset の和） | 4 辺 inset（部品 `RecipeListScrollView` の内側の `GameScrollView.contentContainer`） | `ApplyOuterMargin(this, inset)` の後に上を `gap − inset` で書き直し、内は部品の `SetClipPadding(inset)`（部品の内側を直に触らない） |
| `ItemListView`:106-127 | 左 `leftBase − inset`・上 `topBase − inset`・下 −inset。右なし | 上・下・左 inset。右なし | 置き換えない（上の Files の理由。Web の CSS が別の式） |

表と実物が食い違えば（T0 Step 3 の取り込み後に行が動いた等）D-6 に読み替えを書く。共有の式は「外 margin = −inset、内 padding = inset」の 1 つで、外れる分は呼び手が式の後に自分の辺を書き直す形でだけ残す（足りない辺を打ち消す形は採らない。`ItemListView` を外す理由）
- [ ] **Step 2: L1（赤）**

```csharp
// S/Client.Tests/Ui/Layout/UiScrollClipInsetTest.cs
using Client.Ui.Foundation.Layout;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Layout
{
    // Web の margin: calc(-1 * clip-inset) と padding: clip-inset の写し（4 辺と左右だけの 2 形）
    // Port of the Web margin: calc(-1 * clip-inset) plus padding: clip-inset (all four sides, or left and right only)
    public class UiScrollClipInsetTest
    {
        [Test]
        public void 四辺に負のmarginと正のpaddingが付く()
        {
            var outer = new VisualElement();
            var inner = new VisualElement();
            UiScrollClipInset.Apply(outer, inner, 6f);
            Assert.AreEqual(-6f, outer.style.marginTop.value.value);
            Assert.AreEqual(-6f, outer.style.marginLeft.value.value);
            Assert.AreEqual(6f, inner.style.paddingBottom.value.value);
            Assert.AreEqual(6f, inner.style.paddingRight.value.value);
        }

        [Test]
        public void 左右だけは上下を触らない()
        {
            var outer = new VisualElement();
            var inner = new VisualElement();
            UiScrollClipInset.ApplyHorizontal(outer, inner, 4f);
            Assert.AreEqual(-4f, outer.style.marginLeft.value.value);
            Assert.AreEqual(StyleKeyword.Null, outer.style.marginTop.keyword);
            Assert.AreEqual(4f, inner.style.paddingRight.value.value);
            Assert.AreEqual(StyleKeyword.Null, inner.style.paddingTop.keyword);
        }

        // 外と内の持ち主が違う画面（RecipeEntryList と部品の内側）は片側ずつ当て、もう片方の要素を触らない
        // Screens whose outer and inner have different owners (RecipeEntryList and the component inside) apply one side at a time without touching the other element
        [Test]
        public void 外のmarginと内のpaddingは片側ずつ当てられる()
        {
            var outer = new VisualElement();
            var inner = new VisualElement();
            UiScrollClipInset.ApplyOuterMargin(outer, 5f);
            UiScrollClipInset.ApplyInnerPadding(inner, 5f);
            Assert.AreEqual(-5f, outer.style.marginRight.value.value);
            Assert.AreEqual(StyleKeyword.Null, outer.style.paddingTop.keyword);
            Assert.AreEqual(5f, inner.style.paddingLeft.value.value);
            Assert.AreEqual(StyleKeyword.Null, inner.style.marginTop.keyword);
        }
    }
}
```

- [ ] **Step 3: 作る**

```csharp
// S/Client.Ui/Foundation/Layout/UiScrollClipInset.cs
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Layout
{
    /// <summary>
    /// スクロールの切り抜きの外へ縁取り・影を逃がす唯一の式（Web の margin:-clip-inset と padding:clip-inset。A-245）
    /// The single formula that lets outlines and shadows escape the scroll clip (Web margin:-clip-inset plus padding:clip-inset; A-245)
    /// </summary>
    public static class UiScrollClipInset
    {
        public static void Apply(VisualElement outer, VisualElement inner, float inset)
        {
            ApplyOuterMargin(outer, inset);
            ApplyInnerPadding(inner, inset);
        }

        // 外の要素と内の要素の持ち主が違うとき用（内は部品が自分の口から当てる）
        // For when the outer and inner elements have different owners (the component applies the inner side through its own port)
        public static void ApplyOuterMargin(VisualElement outer, float inset)
        {
            outer.style.marginTop = -inset;
            outer.style.marginRight = -inset;
            outer.style.marginBottom = -inset;
            outer.style.marginLeft = -inset;
        }

        public static void ApplyInnerPadding(VisualElement inner, float inset)
        {
            inner.style.paddingTop = inset;
            inner.style.paddingRight = inset;
            inner.style.paddingBottom = inset;
            inner.style.paddingLeft = inset;
        }

        // 縦は Web が逃がさない画面（研究の計算の帯）用
        // For screens where the Web does not escape vertically (the research calculation band)
        public static void ApplyHorizontal(VisualElement outer, VisualElement inner, float inset)
        {
            outer.style.marginLeft = -inset;
            outer.style.marginRight = -inset;
            inner.style.paddingLeft = inset;
            inner.style.paddingRight = inset;
        }
    }
}
```

`RecipeListScrollView` は ctor のローカル変数 `scroll`（:27）を `private readonly GameScrollView _scroll;` のフィールドにし、`public void SetClipPadding(float inset) { UiScrollClipInset.ApplyInnerPadding(_scroll.contentContainer, inset); }` を足す。`RecipeEntryList.ApplyClipClearance` の :137-147 は次へ（`_scroll.Q<GameScrollView>()` で部品の内側を探す直書きが消える）:

```csharp
            // 外へ広げる式は共有のもの。上だけ見出しとの Stack gap sm を足す（Web は Stack の margin-top gap と打ち消しの -inset の和）
            // The outward formula is the shared one; only the top adds the Stack gap sm below the header (the Web sums the Stack margin-top gap and the cancelling -inset)
            UiScrollClipInset.ApplyOuterMargin(this, inset);
            style.marginTop = gap - inset;
            _scroll.SetClipPadding(inset);
```

`BuildMenuScrollClipInset`・`NodeGraphView`・`BlockInventoryNaturalScroll` は自前の 8 行を `UiScrollClipInset.Apply(…)` の 1 行へ、`ResearchCalcKnobs.ApplyScrollBleed` は `ApplyHorizontal` へ。`BuildMenuScrollClipInset` が `Apply` を呼ぶだけになったら、呼び手から直に呼んでファイルを消す。`ItemListView` は触らない（Step 1 の表）

Run: `uloop compile …` → `uloop run-tests … --filter-type regex --filter-value "UiScrollClipInsetTest|ItemList|RecipeEntryList|RecipeListScrollView|RecipeViewer|BuildMenuScroll|BuildMenuLayout|NodeGraph|BlockInventoryNaturalScroll|ResearchCalc"` → 失敗 0

- [ ] **Step 4: コミット**: `refactor(uitk): スクロールのクリップ逃げを UiScrollClipInset の 1 式へ（DC-19。S5-crosscut T3）`

---

### Task 4: DC-27 描画ヘルパ（R-6）

**Files:**
- Create: `S/Client.Ui/Foundation/Theme/Mesh/GridMeshIndexWriter.cs`・`Painter2DSquareCap.cs`、Test: `S/Client.Tests/Ui/Theme/Mesh/GridMeshIndexWriterTest.cs`・`Painter2DSquareCapTest.cs`（既存の Theme の L1 `Client.Tests/Ui/Theme/` の下に、本体と同じ `Mesh/`。`Client.Tests/Ui/Foundation/` は無い）
- Modify: `Screens/Shell/ScreenVignette.cs`（:85-97）・`Components/Panels/GamePanel/GamePanelFace.cs`（:101-116）・`Components/Panels/DialogFrame/DialogFrameShadow.cs`（:108-120）・`Components/Controls/RecipeActionButton/RecipeActionFaceMesh.cs`（:74-87）・`Components/Recipe/RecipeRow/RecipeRowFrame.cs`（:123-128）・`Components/Controls/IconButton/IconButtonCloseGlyph.cs`（:63 のローカル関数）・`SkitAdvanceMarker.cs`（`ExtendedEnd`）・`Foundation/Theme/ShadowRingMeshWriter.cs`・`Components/Recipe/RecipeRow/RecipeRowFrameShadowMesh.cs`（:21,29）・`Components/Gauges/ProgressArrowGlyph/ProgressArrowGlyphShadowMesh.cs`（:19,29）

**Interfaces:** Consumes: `MeshWriteData.SetNextIndex(ushort)`・`Painter2D.MoveTo/LineTo`。Produces: `GridMeshIndexWriter.WriteQuads(MeshWriteData mesh, int rows, int columns, int stride)`・`Painter2DSquareCap.AddSegment(Painter2D painter, Vector2 from, Vector2 to, float halfWidth)`・`Painter2DSquareCap.Extend(Vector2 end, Vector2 from, float length)`・`ShadowRingMeshWriter.RingCount`（8）・`ShadowRingMeshWriter.CenterMargin`（0.01f）

- [ ] **Step 1: 並びの照合**: 5 か所の index が「左上・右上・右下」「左上・右下・左下」（`GamePanelFace.cs`:106-115 の形）と同じかを読み、違う並び（`ItemHeaderRule`:67-72・`CrosshairShadowRing`:90-95 は起草時に別の並び）は置換しない。同じ並びのものだけを表にして D-7 に書く
- [ ] **Step 2: L1（赤）**: `GridMeshIndexWriterTest` — 2×3 の格子（stride 4）で 36 個の index の最初の 6 個が `0,1,5,0,5,4`、最後の 6 個が `6,7,11,6,11,10`（`MeshWriteData` は `MeshGenerationContext` の外で作れないので、`WriteQuads` の内部の並びを `GridMeshIndexWriter.QuadIndices(int row, int column, int stride)`〈`ushort[6]` を返す〉に出し、それを試す）。`Painter2DSquareCapTest` — `Extend(new Vector2(10,0), Vector2.zero, 2f)` が `(12,0)`
- [ ] **Step 3: 作る**

```csharp
// S/Client.Ui/Foundation/Theme/Mesh/GridMeshIndexWriter.cs
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Theme.Mesh
{
    /// <summary>
    /// 格子の頂点から四角を 2 枚の三角で書く唯一の並び（画面座標で時計回り。y は下向き）
    /// The single ordering that writes each grid quad as two triangles (clockwise in screen space, y down)
    /// </summary>
    public static class GridMeshIndexWriter
    {
        public static void WriteQuads(MeshWriteData mesh, int rows, int columns, int stride)
        {
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                foreach (var index in QuadIndices(row, column, stride)) mesh.SetNextIndex(index);
            }
        }

        public static ushort[] QuadIndices(int row, int column, int stride)
        {
            var topLeft = (ushort)(row * stride + column);
            var topRight = (ushort)(topLeft + 1);
            var bottomLeft = (ushort)(topLeft + stride);
            var bottomRight = (ushort)(bottomLeft + 1);
            return new[] { topLeft, topRight, bottomRight, topLeft, bottomRight, bottomLeft };
        }
    }
}
```

`WriteQuads` が毎フレーム配列を作るのが重ければ（描き直しの多い `ScreenVignette`）、`QuadIndices` を `SetNextIndex` 6 回に展開した私的な版を `WriteQuads` の中に置き、`QuadIndices` は同じ並びを返すテスト用ではなく `WriteQuads` が使う唯一の並びとして残す（テスト専用の public を作らない。`QuadIndices` を `WriteQuads` が呼ばない形にはしない）

```csharp
// S/Client.Ui/Foundation/Theme/Mesh/Painter2DSquareCap.cs
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Theme.Mesh
{
    /// <summary>
    /// Painter2D に無い square の線端を、端を線幅の半分だけ線の向きへ延ばして作る（SVG stroke-linecap: square の写し）
    /// Makes the square line cap Painter2D lacks by extending each end by half the stroke width along the line (port of SVG stroke-linecap: square)
    /// </summary>
    public static class Painter2DSquareCap
    {
        public static void AddSegment(Painter2D painter, Vector2 from, Vector2 to, float halfWidth)
        {
            painter.MoveTo(Extend(from, to, halfWidth));
            painter.LineTo(Extend(to, from, halfWidth));
        }

        // end を from から離れる向きへ length だけ延ばす
        // Extends end by length, away from from
        public static Vector2 Extend(Vector2 end, Vector2 from, float length)
        {
            return end + (end - from).normalized * length;
        }
    }
}
```

`ShadowRingMeshWriter` に `public const int RingCount = 8;` と `public const float CenterMargin = 0.01f;`（中心の縫い目を避ける余白）を足し、2 部品の私有の定数をそれへ（`RecipeRowFrameShadowMesh.RingCount` を外から読む所があれば `ShadowRingMeshWriter.RingCount` に読み替える）。5 か所の index・2 か所の square cap を置換する

Run: `uloop compile …` → `uloop run-tests … --filter-type regex --filter-value "GridMeshIndexWriterTest|Painter2DSquareCapTest|ScreenVignette|GamePanel|DialogFrame|RecipeAction|RecipeRow|IconButton|SkitAdvance|ProgressArrow"` → 失敗 0

- [ ] **Step 4: コミット**: `refactor(uitk): 格子の index・square cap・影の輪の定数を Foundation/Theme/Mesh へ（DC-27。S5-crosscut T4）`

---

### Task 5: DC-28 B トークン待ちの宿主を `UssStyleResolutionGate` へ（R-7）

**Files:**
- Create: `S/Client.Ui/Foundation/Theme/StyleResolution/UssStyleResolutionGate.cs`（`Foundation/Theme/` 直下は USS・txt を含め 14 ファイルなのでサブディレクトリ。D-7）、Test: `S/Client.Tests/Ui/Theme/StyleResolution/UssStyleResolutionGateTest.cs`（本体と同じサブディレクトリ。`Client.Tests/Ui/Theme/` 直下は既に 7 ファイル）
- Modify: `Screens/Toast/ToastHostView.cs`（:23-27,38-45,70-86）・`Screens/Notification/NotificationHostView.cs`（:22,33,44,61-71,142-168。I-fix6 の DC-28 A 反映後の実物）

**Interfaces:** Consumes: `UssCustomStyleReader.TryReadMilliseconds`／`TryReadPixels`（`Foundation/Theme/UssCustomStyleReader.cs`）。Produces: `UssStyleResolutionGate(VisualElement host, string ownerName)`・`bool IsResolved { get; private set; }`・`IObservable<ICustomStyle> OnResolved`・`void ReportHeldWhileUnresolved(string what)`・`void ReportUnreadable(string what)`

- [ ] **Step 1: L1（赤）**: 3 件 —（1）パネルに載せて USS を解決すると `OnResolved` が 1 回流れ `IsResolved` が真、（2）USS の無いまま配置まで進むと `LogAssert.Expect(LogType.Error, new Regex(@"\[ProbeHost\] custom style not resolved"))`、（3）解決前に `ReportHeldWhileUnresolved` を呼んでも配置前は何も出さず、配置後に呼ぶと LogError
- [ ] **Step 2: 作る**

```csharp
// S/Client.Ui/Foundation/Theme/StyleResolution/UssStyleResolutionGate.cs
using System;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Theme.StyleResolution
{
    /// <summary>
    /// USS のトークンを読む常駐の宿主の「解決待ち→解決の通知→配置後も未解決なら開発者へ」を持つ。預かる要求と読むトークンは宿主が持つ
    /// Holds "wait for resolution, announce it, tell developers if still unresolved after layout" for resident hosts reading USS tokens; held requests and token names stay with the host
    /// </summary>
    public sealed class UssStyleResolutionGate
    {
        private readonly string _ownerName;
        private readonly Subject<ICustomStyle> _onResolved = new();
        private bool _laidOutWithoutStyle;

        public bool IsResolved { get; private set; }
        public IObservable<ICustomStyle> OnResolved => _onResolved;

        public UssStyleResolutionGate(VisualElement host, string ownerName)
        {
            _ownerName = ownerName;
            host.RegisterCallback<CustomStyleResolvedEvent>(evt =>
            {
                IsResolved = true;
                _onResolved.OnNext(evt.customStyle);
            });
            host.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                // 配置まで進んでも解決が来ないのは tss の @import 漏れかクラスの付け忘れ
                // Layout without resolution means a missing tss @import or a missing class
                if (IsResolved || _laidOutWithoutStyle) return;
                _laidOutWithoutStyle = true;
                Debug.LogError($"[{_ownerName}] custom style not resolved after layout; is the host's USS imported by GameUiTheme.tss and its class present?");
            });
        }

        // 解決前に預かった要求があるとき宿主が呼ぶ。配置前の預かりは一時的なので黙る
        // Hosts call this when holding a request before resolution; holding before layout is transient, so it stays quiet
        public void ReportHeldWhileUnresolved(string what)
        {
            // silent-ok: 配置前の預かりは解決を待つ正常系 / 配置後も未解決なら GeometryChanged とここが LogError を出す
            if (!_laidOutWithoutStyle) return;
            Debug.LogError($"[{_ownerName}] {what} is held because the custom style never resolved");
        }

        // 解決したがトークンが読めず、動き無しで出すとき宿主が毎回呼ぶ
        // Hosts call this every time they show something without motion because tokens resolved but were unreadable
        public void ReportUnreadable(string what)
        {
            Debug.LogError($"[{_ownerName}] motion tokens are unreadable; showing without motion: {what}");
        }
    }
}
```

- [ ] **Step 3: 2 宿主を載せ替える**: `ToastHostView` は `_styleResolved` と `OnCustomStyleResolved` の登録をやめ、ctor で `_gate = new UssStyleResolutionGate(this, nameof(ToastHostView)); _gate.OnResolved.Subscribe(ReadDurations);`（`using Client.Ui.Foundation.Theme.StyleResolution;`）。`Push` の先頭は `if (!_gate.IsResolved) { _toastsWaitingForStyle.Add(toast); _gate.ReportHeldWhileUnresolved($"toast {toast.Text}"); return; }`、読めないときは `_gate.ReportUnreadable(toast.Text.ToString())`。`NotificationHostView` も同じ形（`StyleNotResolvedMessage`・`_laidOutWithoutStyle`・`OnGeometryChanged` を消し、`Render` の預かりで `ReportHeldWhileUnresolved("notification rows")`、尺 0 の時刻で出すとき `ReportUnreadable`）。両ファイルとも行数が減る
- [ ] **Step 4: テスト**: 既存の Toast・Notification の L1 で `[NotificationHostView] custom style not resolved` の文を `LogAssert.Expect` している所は、新しい文に合わせる（`git grep -n "custom style not resolved\|fade tokens are unreadable" -- moorestech_client/Assets/Scripts/Client.Tests`）

Run: `uloop compile …` → `uloop run-tests … --filter-type regex --filter-value "UssStyleResolutionGateTest|Toast|Notification"` → 失敗 0

- [ ] **Step 5: コミット**: `refactor(uitk): トーストと通知のトークン待ちを UssStyleResolutionGate へ（DC-28 B。S5-crosscut T5）`

---

### Task 6: DC-10 画面がキーを受ける口（R-8）

**Files:**
- Create: `S/Client.Input/KeyPress/IUiKeyPressSource.cs`（口と、DI の鍵になるキーごとの型 2 つ）・`InputKeyPressSource.cs`（唯一の実装。抽象）・`UiKeyPressSources.cs`（キーごとの実装 2 つ）
- Modify: `S/Client.Input/ArrowKeys/InputSystemArrowKeyStream.cs`（:36-48 の `PublishEdge`。押下の縁を `InputSuppressionFilter.FilterKeyPress` に通す）。`.inputactions`・`InputManager.cs`・`moorestechInputSettings.cs` は触らない（A-162 を保つ。D-8）
- Move: `S/Client.Ui/Screens/Recipe/Input/RecipeSelectionCloseReset.cs` → `S/Client.Ui.State/Recipe/Screen/RecipeSelectionCloseReset.cs`
- Delete: `S/Client.Ui/Screens/Skit/SkitEnterKeySource.cs`・`Screens/Recipe/Input/IUiCloseKeyProbe.cs`・`InputManagerCloseKeyProbe.cs`（`Screens/Recipe/Input/` が空になればディレクトリごと）と各 .meta。`InputSystemArrowKeyStream`・`IHorizontalArrowKeySource` は残す
- Modify: `Screens/Skit/SkitScreenPresenter.cs`（:28 の ctor・:57）・`Screens/BlockInventory/Special/Belt/BeltThroughputSectionPresenter.cs`（:60-63）・`S/Client.Starter/Registration/GameUi/Screens/RecipeGameUiRegistration.cs`（:25-28）・`Registration/GameUi/Hud/SkitGameUiRegistration.cs`（:22-23）。`BlockInventorySpecialGameUiRegistration.cs`:29 は変えない
- Test: `S/Client.Tests/Ui/Crosscut/Input/UiKeyPressSourceSuppressionTest.cs`（作る）・`S/Client.Tests/Input/ArrowKeys/InputSystemArrowKeyStreamTest.cs`（抑止の 1 件を足す）・`Client.Tests/Ui/Screens/Recipe/Input/RecipeSelectionCloseResetTest.cs`・`FakeCloseKeyProbe.cs`（`FakeCloseUiKeyPressSource` へ中身を替える。ファイル名は Unity の .meta を保つため据え置き、型名だけ替える）・`Client.Tests/Ui/Screens/Skit/SkitScreenPresenterTest.cs`（:104）・`Client.Tests/Ui/Screens/BlockInventory/Special/BeltThroughputSectionViewTest.cs`（:58-72。入力欄フォーカスの段は stream の L0 へ移ったので、偽の矢印での確かめから外す）

**Interfaces:** Consumes: `InputKey.OnGetKeyDown`・`InputKey.GetKeyDown`（`InputKey.cs`:24-33。どちらも `FilterPress` → `InputSuppressionFilter.FilterKeyPress`）・`InputKey.SetKeyDownForTest(bool)`（`#if UNITY_EDITOR` の internal。`Client.Input/AssemblyInfo.cs` が `Client.Tests` へ公開）・`InputSuppressionFilter.FilterKeyPress(bool pressed, InputSuppressionScope? scope, bool isEscapeKey)`（`InputSuppressionFilter.cs`:17-22。internal）・`UiInputExclusivity.SetState(bool, bool)`・`ConsumeEscapeThisFrame()`・`ClearEscapeLatchForTest()`。Produces: `IUiKeyPressSource { IObservable<Unit> OnPressed { get; } bool WasPressedThisFrame(); }`・`ISkitAdvanceKeyPressSource : IUiKeyPressSource`・`ICloseUiKeyPressSource : IUiKeyPressSource`・`abstract class InputKeyPressSource(InputKey key) : IUiKeyPressSource`・`SkitAdvanceKeyPressSource()`・`CloseUiKeyPressSource()`・`RecipeSelectionCloseReset(UiRoutingState routing, ICloseUiKeyPressSource closeKey, RecipeItemSelection selection)`・`SkitScreenPresenter` の ctor の最後に `ISkitAdvanceKeyPressSource skitAdvance`

- [ ] **Step 1: L0（赤）**: `UiKeyPressSourceSuppressionTest`（`InputTestFixture` を継ぐ。前例 `Client.Tests/Input/UiSkitAdvanceInputTest.cs`。`Setup`／`TearDown` で `TestReflection.ResetInputManagerCache()`、`TearDown` で `UiInputExclusivity.SetState(false, false)`・`UiInputExclusivity.ClearEscapeLatchForTest()`・`InputManager.UI.CloseUI.SetKeyDownForTest(false)`）3 件:

```csharp
// S/Client.Tests/Ui/Crosscut/Input/UiKeyPressSourceSuppressionTest.cs
using Client.Input;
using Client.Input.KeyPress;
using Client.Tests.Common;
using NUnit.Framework;
using UniRx;
using UnityEngine.InputSystem;

namespace Client.Tests.Ui.Crosscut.Input
{
    // 画面へキーを渡す口は抑止を InputKey の InputSuppressionFilter だけに任せる（A-246。Web inputLayer の入力欄の除外）
    // The key ports leave suppression to the InputKey's InputSuppressionFilter alone (A-246; the Web inputLayer text-field exclusion)
    public class UiKeyPressSourceSuppressionTest : InputTestFixture
    {
        public override void Setup()
        {
            base.Setup();
            TestReflection.ResetInputManagerCache();
        }

        public override void TearDown()
        {
            UiInputExclusivity.SetState(false, false);
            UiInputExclusivity.ClearEscapeLatchForTest();
            InputManager.UI.CloseUI.SetKeyDownForTest(false);
            TestReflection.ResetInputManagerCache();
            base.TearDown();
        }

        [Test]
        public void スキットの送りはEnterで流れ入力欄のフォーカス中は流れない()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var presses = 0;
            new SkitAdvanceKeyPressSource().OnPressed.Subscribe(_ => presses++);
            InputSystem.Update();
            Press(keyboard.enterKey);
            InputSystem.Update();
            Release(keyboard.enterKey);
            InputSystem.Update();
            UiInputExclusivity.SetState(false, true);
            Press(keyboard.enterKey);
            InputSystem.Update();
            Assert.AreEqual(1, presses);
        }

        // 旧の probe と同じく読んだ時点で抑止と ESC ラッチを評価する（D-8 の受け身の案）
        // As with the old probe, suppression and the ESC latch are evaluated at read time (D-8, the passive option)
        [Test]
        public void CloseUIはこのフレームの押下を読め入力欄のフォーカス中とESCを使ったフレームは偽()
        {
            // ESC ラッチは Keyboard.current の escapeKey が CloseUI の割り当てかで効く（InputKey.FilterPress）。無いと 3 つ目が素通りする
            // The ESC latch keys off whether Keyboard.current's escapeKey is bound to CloseUI (InputKey.FilterPress); without a keyboard the third assert passes through
            InputSystem.AddDevice<Keyboard>();
            var closeKey = new CloseUiKeyPressSource();
            InputManager.UI.CloseUI.SetKeyDownForTest(true);
            Assert.IsTrue(closeKey.WasPressedThisFrame());
            UiInputExclusivity.SetState(false, true);
            Assert.IsFalse(closeKey.WasPressedThisFrame());
            UiInputExclusivity.SetState(false, false);
            UiInputExclusivity.ConsumeEscapeThisFrame();
            Assert.IsFalse(closeKey.WasPressedThisFrame(), "CloseUI が ESC に割り当てられていればラッチで止まる");
        }

        [Test]
        public void 同じキーの口を2つ作っても抑止の判定はInputKeyの1か所()
        {
            var first = new SkitAdvanceKeyPressSource();
            var second = new SkitAdvanceKeyPressSource();
            InputManager.UI.SkitAdvance.SetKeyDownForTest(true);
            UiInputExclusivity.SetState(false, true);
            Assert.IsFalse(first.WasPressedThisFrame());
            Assert.IsFalse(second.WasPressedThisFrame());
            InputManager.UI.SkitAdvance.SetKeyDownForTest(false);
        }
    }
}
```

`CloseUiKeyPressSource` の 3 つ目の assert は、`CloseUI` の割り当てが `<Keyboard>/escape` であることと、`InputTestFixture` の中で `Keyboard.current` が在ること（`InputKey.FilterPress` の `isEscapeKey` は `Keyboard.current` が null だと偽。`InputKey.cs`:56-62。前例 `UiInputExclusivityEscapeLatchTest.cs`:29,45 も先にキーボードを足す）に頼る（binding は起草時に `.inputactions` の UI マップを読んで確かめた。T0 Step 3 で同じ値かを見る）。`InputSystemArrowKeyStreamTest` に 1 件足す:

```csharp
        [Test]
        public void 入力欄にフォーカスがある間は矢印を流さない()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var pressed = new List<HorizontalArrowKey>();
            new InputSystemArrowKeyStream().OnArrowPressed.Subscribe(pressed.Add);

            UiInputExclusivity.SetState(false, true);
            Press(keyboard.rightArrowKey);
            Release(keyboard.rightArrowKey);
            UiInputExclusivity.SetState(false, false);
            Press(keyboard.leftArrowKey);

            CollectionAssert.AreEqual(new[] { HorizontalArrowKey.Left }, pressed);
        }
```

（`using Client.Input;` を足し、`TearDown` を override して `UiInputExclusivity.SetState(false, false)` を呼ぶ）。Run: `uloop compile --project-path ./moorestech_client` → 型が無いエラーを確かめる

- [ ] **Step 2: 作る**

```csharp
// S/Client.Input/KeyPress/IUiKeyPressSource.cs
using System;
using UniRx;

namespace Client.Input.KeyPress
{
    /// <summary>
    /// 画面が InputKey のキーを受ける唯一の口。押下の瞬間（購読）と「このフレームに押されたか」（読み）の 2 つの読み方を持ち、入力欄のフォーカス中の抑止と ESC ラッチは InputKey の InputSuppressionFilter が持つ（A-246）
    /// The single port through which screens receive an InputKey; it offers the press moment (subscription) and "pressed this frame" (read), and suppression while a text field has focus plus the ESC latch live in the InputKey's InputSuppressionFilter (A-246)
    /// </summary>
    public interface IUiKeyPressSource
    {
        IObservable<Unit> OnPressed { get; }
        bool WasPressedThisFrame();
    }

    // DI の鍵になるキーごとの型（前例 IUiCloseKeyProbe・IHorizontalArrowKeySource。同じ口を受け手ごとに別のキーで渡すため）
    // Per-key types that serve as DI keys (precedent IUiCloseKeyProbe, IHorizontalArrowKeySource; each receiver gets the same port bound to a different key)
    public interface ISkitAdvanceKeyPressSource : IUiKeyPressSource
    {
    }

    public interface ICloseUiKeyPressSource : IUiKeyPressSource
    {
    }
}
```

```csharp
// S/Client.Input/KeyPress/InputKeyPressSource.cs
using System;
using UniRx;

namespace Client.Input.KeyPress
{
    /// <summary>
    /// IUiKeyPressSource の唯一の実装。InputKey の押下（抑止を通った後の OnGetKeyDown・GetKeyDown）をそのまま渡し、自分では判定しない
    /// The only IUiKeyPressSource implementation; it relays the InputKey press (OnGetKeyDown and GetKeyDown after suppression) and judges nothing itself
    /// </summary>
    public abstract class InputKeyPressSource : IUiKeyPressSource
    {
        private readonly InputKey _key;

        public IObservable<Unit> OnPressed { get; }

        protected InputKeyPressSource(InputKey key)
        {
            _key = key;
            OnPressed = Observable.FromEvent(handler => key.OnGetKeyDown += handler, handler => key.OnGetKeyDown -= handler);
        }

        public bool WasPressedThisFrame()
        {
            return _key.GetKeyDown;
        }
    }
}
```

```csharp
// S/Client.Input/KeyPress/UiKeyPressSources.cs
namespace Client.Input.KeyPress
{
    // スキットの送りの Enter（テンキーの Enter を含む。A-83）
    // Skit-advance Enter, including the keypad Enter (A-83)
    public sealed class SkitAdvanceKeyPressSource : InputKeyPressSource, ISkitAdvanceKeyPressSource
    {
        public SkitAdvanceKeyPressSource() : base(InputManager.UI.SkitAdvance)
        {
        }
    }

    // 画面を閉じる CloseUI（Esc）
    // CloseUI (Esc), which closes screens
    public sealed class CloseUiKeyPressSource : InputKeyPressSource, ICloseUiKeyPressSource
    {
        public CloseUiKeyPressSource() : base(InputManager.UI.CloseUI)
        {
        }
    }
}
```

`InputSystemArrowKeyStream.PublishEdge`（:42-48）の最後の行を次へ替える（押下の記憶は抑止中も更新し、流すかだけを抑止で決める）:

```csharp
                var pressed = 0.5f <= value;
                var edge = pressed && !wasPressed;
                wasPressed = pressed;
                // 入力欄のフォーカス中の抑止は InputKey と同じ判定へ通す（止めたときのログも InputSuppressionFilter が出す。A-246）
                // Text-field suppression goes through the same decision as InputKey (InputSuppressionFilter also logs when it blocks; A-246)
                if (edge && InputSuppressionFilter.FilterKeyPress(true, InputSuppressionScope.Keyboard, false)) _onArrowPressed.OnNext(arrow);
```

`BeltThroughputSectionPresenter.OnArrowPressed`（:58-65）の条件から `UiInputExclusivity.IsTextInputFocused ||` を消し、:60-62 のコメントを「ベルトが無い・表示されていないなら切り替えない（入力欄のフォーカス中は流れて来ない。Web の readActiveLayer）」と英訳・`silent-ok: ベルト以外のブロック・閉じた画面の矢印はこの区画の操作ではない / 切り替わらないなら L1 と録画（R-12）で気づく` に直す。`using Client.Input;` が使われなくなれば消す

`RecipeSelectionCloseReset` は State へ移し、駆動は旧のまま（遷移の購読の中で「このフレームに CloseUI が押されたか」を読む）。namespace を `Client.Ui.State.Recipe.Screen` に、ctor の第 2 引数を `ICloseUiKeyPressSource closeKey` に替え、`OnScreenChanged` の 1 行目を `if (!_closeKey.WasPressedThisFrame()) return; // silent-ok: Esc 以外の遷移（Tab 等）は選択を残す仕様 / RecipeSelectionCloseResetTest の CloseUIなし の件が見る` にする（`IDisposable` は T13 で外す）

`SkitScreenPresenter` は ctor の最後に `ISkitAdvanceKeyPressSource skitAdvance` を足して `_skitAdvance` に持ち、:57 を `_skitAdvance.OnPressed.Subscribe(_ => view.HandleEnterKey());` にする

- [ ] **Step 3: DI**: 前例のある形（キーごとの型を `Register<実装>(Lifetime.Singleton).As<型>()`。`RecipeGameUiRegistration.cs`:27 の `InputManagerCloseKeyProbe` と同じ）で書く。`RecipeGameUiRegistration` の :27 を `builder.Register<CloseUiKeyPressSource>(Lifetime.Singleton).As<ICloseUiKeyPressSource>();` に（:25-26 のコメントは「Esc の選択解除は UiRoutingState と CloseUI の口を受ける entry point（前例 ResearchSelectionStore）」と英訳へ）。`SkitGameUiRegistration` の :23 の前に `builder.Register<SkitAdvanceKeyPressSource>(Lifetime.Singleton).As<ISkitAdvanceKeyPressSource>();` とコメント 2 行「スキットの送りの Enter の口（抑止は InputKey が持つ）」。`using Client.Input.KeyPress;` を両方に足し、`using Client.Ui.Screens.Recipe.Input;` を消す
- [ ] **Step 4: テスト**: `FakeCloseKeyProbe.cs` の型を `public sealed class FakeCloseUiKeyPressSource : ICloseUiKeyPressSource`（`SetPressed(bool)`・`WasPressedThisFrame()` は今の形、`OnPressed` は `Observable.Never<Unit>()`）に替え、`RecipeSelectionCloseResetTest` の using と型名を直す。`SkitScreenPresenterTest`:104 の ctor に `new FakeSkitAdvanceKeyPressSource()`（同ファイルの private sealed class。`Subject<Unit>` を `OnPressed` に、`WasPressedThisFrame()` は false）を渡す。`BeltThroughputSectionViewTest` の `入力欄にフォーカスがある間とDetachの後は切り替えずDetachで既定へ戻る` から :63-66（フォーカスを立てて押す段）を消し、名前を `Detachの後は切り替えずDetachで既定へ戻る` にする（入力欄の抑止は `InputSystemArrowKeyStreamTest` の新しい件が見る）

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "UiKeyPressSourceSuppressionTest|InputSystemArrowKeyStreamTest|UiSkitAdvanceInputTest|RecipeSelectionCloseReset|Skit|BeltThroughput|InputSuppression"` → 失敗 0

- [ ] **Step 5: コミット**: `refactor(uitk): 画面のキーの口を IUiKeyPressSource 1 本へ寄せ、矢印の抑止を InputSuppressionFilter に通す（DC-10。S5-crosscut T6）`

---

### Task 7: DC-8 B 画面の表示判定（R-9。Warning c46-cc）

**Files:**
- Create: `S/Client.Ui.State/BuildMenu/BuildMenuScreenVisibility.cs`・`S/Client.Ui.State/Recipe/Screen/RecipeScreenVisibility.cs`
- Modify: `S/Client.Ui.State/BuildMenu/Source/BuildMenuModel.cs`（:47,67）・`BuildMenu/Commands/BuildMenuCommands.cs`（:71 `IsBuildMenuOpen`）・`Research/ResearchNodeStateStore.cs`（:21 フィールド・:31 ctor・:50-54 の `OnStateChanged`）・`Recipe/Screen/RecipeScreenModel.cs`（:105 `IsScreenShown`）・`S/Client.Ui/Screens/BuildMenu/BuildMenuScreenPresenter.cs`（:54 の `snapshot.Screen == UiScreen.BuildMenu`）と各 DI・テストの組み立て
- Modify（I-fix6 の結果次第。T0 Step 3 で見る）: `S/Client.Ui/Screens/Research/ResearchTreePresenter.cs`:44・`S/Client.Ui.State/Research/Tree/ResearchTreeModel.cs`:50-51・`Research/Tree/ResearchSelectionStore.cs`:29。I-fix6 の DC-8 A が `ResearchScreenVisibility.IsShown` へ付け替え済みなら触らない。残っていれば Step 3 で同じ形に付け替える
- 読むだけで残す（理由のコメントを 1 行）: `OpenBlockDetailTracker.cs`:56・`HotbarSelectRule.cs`:15・`HotbarCommands.cs`:30・`KeyHintHudModel.cs`:54-56・`ModeHudModel.cs`:52・`KeyControlHintModel.cs`:41・`TrainTimetableModel.cs`:50・`OpenTrainTimetableTracker.cs`:38,72

**Interfaces:** Consumes: `UiRoutingState.Current`（`IReadOnlyReactiveProperty<UiRoutingSnapshot>`）・`UiRoutingSnapshot.Screen`・`UiScreen`・前例 `BlockInventoryScreenVisibility.IsShown(UiRoutingSnapshot, BlockInventoryPanelState)`・I-fix6 の `ResearchScreenVisibility.IsShown(UiRoutingSnapshot)`。Produces: `BuildMenuScreenVisibility.IsShown(UiRoutingSnapshot snapshot)`・`RecipeScreenVisibility.IsShown(UiRoutingSnapshot snapshot)`

- [ ] **Step 1: 分類**: 上の UIStateEnum の読み 15 か所を「`UiScreen` で言える（画面の開閉）」「`UiScreen` に無い状態（GameScreen・PlaceBlock・DeleteBar 等）」に分け D-10 に表で書く。前者は `<機能>ScreenVisibility` へ、後者は `// UiScreen に無い状態（<状態名>）なので UIStateEnum を読む（A-248）` の 1 行と英訳を足して残す
- [ ] **Step 2: 作る**

```csharp
// S/Client.Ui.State/BuildMenu/BuildMenuScreenVisibility.cs
using Client.Ui.State.Routing;

namespace Client.Ui.State.BuildMenu
{
    /// <summary>
    /// ビルドメニューの画面を出すか（前例 BlockInventoryScreenVisibility。A-248）
    /// Whether to show the build-menu screen (precedent BlockInventoryScreenVisibility; A-248)
    /// </summary>
    public static class BuildMenuScreenVisibility
    {
        public static bool IsShown(UiRoutingSnapshot snapshot)
        {
            return snapshot.Screen == UiScreen.BuildMenu;
        }
    }
}
```

`RecipeScreenVisibility` は `RecipeScreenModel.IsScreenShown`（:105）の本体をそのまま移し、Model は呼ぶだけにする

- [ ] **Step 3: 付け替え**: `BuildMenuModel` は ctor の `UIStateControl` を `UiRoutingState` へ替え、:47 を `_routing.Current.Select(BuildMenuScreenVisibility.IsShown).DistinctUntilChanged().Where(shown => shown).Subscribe(_ => { … })`、:67 を `if (!BuildMenuScreenVisibility.IsShown(_routing.Current.Value)) return;`。`BuildMenuCommands.IsBuildMenuOpen` と `ResearchNodeStateStore` の 3 か所も同じ形。`UiRoutingState` は `UIStateControl.OnStateChanged` から写すので、開いた瞬間の順序（BP の取り直し→組み直し）は変わらない。`BuildMenuScreenPresenter`:54 の `.Select(snapshot => snapshot.Screen == UiScreen.BuildMenu)` を `.Select(BuildMenuScreenVisibility.IsShown)` に。研究の 3 か所が I-fix6 の後も直書きなら `ResearchScreenVisibility.IsShown` を呼ぶ形へ（`ResearchTreeModel`:50-51 は `var shown = ResearchScreenVisibility.IsShown(snapshot); var entering = !_screenShown && shown; _screenShown = shown;`）。テストは `UiRoutingStateTestFactory.Create(_host, state)`／`CreateSwitchable`（`Client.Tests/Ui/Screens/Inventory/Support/UiRoutingStateTestFactory.cs`:19,26）で組む。終わりに `git grep -n -e "Screen == UiScreen\.BuildMenu" -e "Screen == UiScreen\.ResearchTree" -- 'moorestech_client/Assets/Scripts/Client.Ui*/*.cs'` が `*ScreenVisibility.cs` だけ（R-9）
- [ ] **Step 4: 設計書の行**: 設計書 §1.1 に「画面の開閉は `UiRoutingState` と `<機能>ScreenVisibility.IsShown` で読む。`UiScreen` に無い状態だけ `UIStateEnum` を読む」を足す案をメモへ（A-248）
- [ ] **Step 4b: スキル本文**: `.agents/skills/ref-uitk-design/SKILL.md`:127 の「出し分けは `UiRoutingState.Current.Screen` と `UiScreenTraitsTable` だけで決める」を「出し分けは `UiRoutingState` と `UiScreenTraitsTable` で決め、画面の開閉を読む State は `<機能>ScreenVisibility.IsShown` を通す。`UiScreen` に無い状態だけ `UIStateEnum` を読み、その行に理由を 1 行書く（設計書 A-248）」へ直す（SKILL.md は設計書から再生成されない。部品カタログ `references/component-catalog.md` は設計書 §2 から再生成されるので手で直さない）。`git grep -n "UiRoutingState.Current.Screen" -- .agents/skills/ref-uitk-design` で他の行に同じ文が無いかを見る。コミットはこの Step の変更だけで `docs(skill): ref-uitk-design の出し分けの規則を A-248 に合わせる（S5-crosscut T7）`

Run: `uloop compile …` → `uloop run-tests … --filter-type regex --filter-value "BuildMenu|Research|Recipe|Routing"` → 失敗 0

- [ ] **Step 5: コミット**: `refactor(uitk): 画面の開閉の読みを UiRoutingState と ScreenVisibility へ揃える（DC-8 B。S5-crosscut T7）`

---

### Task 8: DC-12 と DC-16 C（R-10・R-11）

**Files:** Modify: `S/Client.Ui/Content/GameContentPresentation.cs`（:23 の下）・`Screens/Notification/GameContentNotificationRowIcons.cs`（:26）・`Components/Slots/ItemSlot/ItemSlotContent.cs`（型コメント）。Test: 既存の通知のアイコンの L1（`git grep -ln "GameContentNotificationRowIcons" -- moorestech_client/Assets/Scripts/Client.Tests`）に 1 件

**Interfaces:** Produces: `bool GameContentPresentation.TryItem(ItemId id, out UiIcon icon)`

- [ ] **Step 1: L1（赤）**: マスタに無い `ItemId(99999)` で `TryItem` が false・`icon` が null、ある ID で true と `Item(id)` と同じ絵
- [ ] **Step 2: 作る**

```csharp
        // マスタに無い ID なら false（未知の ID を受けうる口だけが使う。他は Item を使い、無ければ例外で開発者が気づく）
        // False for an id absent from the master (only for paths that may receive unknown ids; others use Item and throw so developers notice)
        public bool TryItem(ItemId id, out UiIcon icon)
        {
            icon = MasterHolder.ItemMaster.ExistItemId(id) ? Item(id) : null;
            return icon != null;
        }
```

`GameContentNotificationRowIcons.ItemIcon` の先頭を `if (_content.TryItem(itemId, out var icon)) return icon;` にし、`using Core.Master;` の `MasterHolder` 直読みを消す（`ItemId` の using は残す）

- [ ] **Step 3: DC-16 C**: `ItemSlotContent` の `<remarks>` に「優先: 実物 > ゴースト。白面は実物だけで決める（`ItemSlotPresentation.From`）。Web の props と同じ平らな形で、Fluid の sealed 派生とは揃えない（D-9）」を日英 1 行ずつ足す。部品カタログの改訂案はメモへ
- [ ] **Step 4**: `uloop compile …` → `uloop run-tests … --filter-type regex --filter-value "Notification|GameContent|ItemSlot"` → 失敗 0。コミット `feat(uitk): GameContentPresentation.TryItem と ItemSlotContent の優先の明記（DC-12・DC-16 C。S5-crosscut T8）`

---

### Task 9: DC-18 共通部品の指カーソル（R-12）

**Files:**
- Modify: `Components/Controls/IconButton/IconButton.cs`・`PanelActionButton/PanelActionButton.cs`・`ModeSwitch/ModeSwitch.cs`（:101 `CreateOptionButton`）
- Modify（呼び手）: IconButton — `Screens/BlockInventory/BlockInventoryView.cs`:55・`Screens/Skit/Parts/Toolbar/SkitToolbar.cs`:81・`Screens/BuildMenu/Parts/BuildMenuPanelSkeleton.cs`:71／PanelActionButton — `RocketLaunchPadTitleActionView`:55・`TrainStationNameRow`:34・`TrainTimetableStationListView`:65・`PauseMenuTopPageView`:48・`InventoryView`:48・`TrainTimetableStopListView`:105・`PauseMenuSubPageView`:37／ModeSwitch — `LanguageSelectView`:33・`SprintModeSelectView`:33・`TrainInventoryBodyView`:29・`BugReportFormView`:54・`GraphicsQualitySettingsView`:36・`GraphicsQualityRow`:50・`FilterDirectionView`:46・`ElectricToGearBodyView`:72・`TrainPlatformModeSection`:24・`TrainTimetableSectionView`:39・`BuildMenuCategorySidebar`:48／ギャラリー（`Client.Ui.Editor` の 7 か所）／テスト（IconButton 1・PanelActionButton 3）。起草後に増えた呼び手は `git grep -n "new IconButton(\|new PanelActionButton(\|new ModeSwitch<" -- moorestech_client/Assets/Scripts` で拾う
- Test: `S/Client.Tests/Ui/Components/Controls/PointerCursor/CommonButtonPointerCursorTest.cs`（`Controls/` 直下は .cs 9 本＋`ControlKnobOverrides.uss` で既に 10 ファイルなのでサブディレクトリ。既存の `ModeSwitch/`・`RecipeAction/` と同じ形）

**Interfaces:** Consumes: `IUiOsCursor.Request(UiOsCursorShape, UiOsCursorLayer)`・`UiOsCursorHoverRequest(VisualElement, IUiOsCursor, UiOsCursorShape, UiOsCursorLayer)`（`Foundation/Input/OsCursor/UiOsCursorHoverRequest.cs`）・テスト `UiOsCursorRequestStack(new FakeUiOsCursorApplier())`（`Client.Tests/Ui/TestDoubles/FakeUiOsCursorApplier.cs`）。Produces: `IconButton(string testid, VisualElement glyph, IUiOsCursor osCursor)`・`PanelActionButton(string testid, <I-fix6 後のラベルの型> label, IUiOsCursor osCursor)`・`ModeSwitch<TValue>(string, ModeSwitchOrientation, ModeSwitchRole, IReadOnlyList<ModeSwitchOption<TValue>>, IUiOsCursor osCursor)`

- [ ] **Step 1: L1（赤）**: 3 件 — 各部品を `UiOsCursorRequestStack(applier)` で作り、`simulate` でポインタを乗せると `applier` の最後の形が `Pointer`、外すと `Default`。ModeSwitch は 2 つ目の選択肢に乗せて `Pointer`。`FakeUiOsCursorApplier` の記録の名前は実物から読む
- [ ] **Step 2: 部品**: 3 部品が ctor の最後の引数で `IUiOsCursor` を受け、IconButton・PanelActionButton は `new UiOsCursorHoverRequest(this, osCursor, UiOsCursorShape.Pointer, UiOsCursorLayer.Element);`、ModeSwitch は `CreateOptionButton` で各 `button` に同じ 1 行（コメント「Web の cursor: pointer（IconButton/style.module.css:9 ほか）。部品が自分で出す（A-247）」）。Web の ModeSwitch の無効の選択肢は `cursor: default`（ModeSwitch/style.module.css:50-53）で、UITK の無効の要素はポインタの事象を受けないので同じになる。PanelActionButton・IconButton の無効は Web では指のまま（`:disabled` の除外が無い）。UITK で無効の要素に指が出ないなら、その差を D-11 に書き、並べ比べで見えない差（静止画で写らない）として LX-205 に記帳する
- [ ] **Step 3: 呼び手**: Presenter が持つ `IUiOsCursor`（`ResearchTreePresenter`・`SkitScreenPresenter` は既に ctor で受ける）を View へ渡す。持っていない Presenter は ctor に `IUiOsCursor osCursor` を足す（DI は登録済みの `IUiOsCursor` を解く）。View の ctor にも足す（デフォルト引数は使わない）。部品を ctor の中で作る View（`BugReportFormView`:54 の `ModeSwitch` など）は ctor の引数だけを足し、フィールドに持たない（`BugReportFormView` は起草時 194 行で、I-fix6 の取り込みも重なる）。終わりに触ったファイルを `wc -l` で全部見て 200 行未満（超えたら引数の受け渡しを見直し、`done.md`「要裁定」へ）
- [ ] **Step 4: 二重の外し**: 画面が同じ部品に個別に付けていた指（`git grep -n "new UiOsCursorHoverRequest" -- moorestech_client/Assets/Scripts/Client.Ui/Screens`。起草時は `SkitChoiceButton`:56・`SkitDialogueWindow`:49・`SelectedRecipeHeader`:48・`MachineRecipeSelectionRowView`:62 で、どれも 3 部品ではない）が 3 部品の上に乗っていないかを確かめ、乗っていれば画面側を消す
- [ ] **Step 5: 設計書**: §2.7 の OS カーソルの行（:234）の「画面が付ける」を「Web で `cursor: pointer` を持つ共通部品（IconButton・PanelActionButton・ModeSwitch）は部品が自分で出す。画面固有の要素だけ画面が付ける」へ改める案をメモへ（A-247）

Run: `uloop compile …` → `uloop run-tests … --filter-type regex --filter-value "CommonButtonPointerCursorTest|IconButton|PanelActionButton|ModeSwitch|OsCursor|PauseMenu|Settings|BuildMenu|BlockInventory|Skit|Train"` → 失敗 0

- [ ] **Step 6: コミット**: `feat(uitk): IconButton・PanelActionButton・ModeSwitch が自分で指カーソルを出す（DC-18。S5-crosscut T9）`

---

### Task 10: DC-21 部品の内側の上書きをつまみへ（R-13）

**Files:**
- Modify: `Screens/PauseMenu/PauseMenu.uss`（:71-75）・`Components/Controls/IconButton/IconButton.uss`・`Screens/Skit/Parts/Toolbar/SkitToolbar.uss`（:35-49）・`Screens/Skit/Parts/Choices/SkitChoices.uss`（:34-40）・`Screens/Skit/Parts/Choices/SkitChoiceButton.cs`・`Components/Panels/GamePanelSurface/GamePanelSurface.uss`（:66-90）・`GamePanelSurfaceKind.cs`・`GamePanelSurface.cs`
- Test: `S/Client.Tests/Ui/Conventions/UiStyleConventionTest.cs`（1 件足す）・`S/Client.Tests/Ui/Components/Controls/IconButtonTest.cs`（色の 1 件。Step 3）・`Client.Tests/Ui/Components/Panels/` の GamePanelSurface の L1・`Screens/Skit` の L1

**Interfaces:** Produces: USS つまみ `--icon-button-color`（IconButton のルート。Web は `color` を root に当てて `currentColor` で受ける）・`--game-panel-surface-face`／`--game-panel-surface-edge-fade`／`--game-panel-surface-top-fade`／`--game-panel-surface-bottom-fade`（GamePanelSurface のルート）。既存のつまみ `--panel-action-button-width`（`PanelActionButton.uss`:13 が `width: var(--panel-action-button-width)` で読む）

- [ ] **Step 1: 規約の検査（赤）**: `UiStyleConventionTest` に「`Screens/**/*.uss` の規則のセレクタが、`Components/**` の USS が定義する `__` 付きの内側クラス・`.unity-button` を含まない」を足す（部品 USS のクラス名の集合を `Components/**/*.uss` の `.name__part` から集め、画面 USS のセレクタと照合。前例は同ファイルの既存の検査の読み方）。起草時の違反は `PauseMenu.uss`:73・`SkitChoices.uss`:36-37 の 2 規則（`SkitToolbar.uss` は自分の `.skit-tool-glyph` に `--icon-button-glyph-color` を当てる形なので、検査は「部品のカスタムプロパティを部品の内側要素に当てる」も見る）。赤を確かめる
- [ ] **Step 2: ポーズメニュー**: `.pause-menu .panel-action-button.unity-button { width: auto; }` を `.pause-menu .panel-action-button { --panel-action-button-width: auto; }` に（Web `features/pauseMenu/style.module.css`:6 と同じつまみ）。USS の変数に `auto` を入れて `width: var(…)` が `auto` になるかは未確認なので、ポーズメニューの L1 で `resolvedStyle.width` が 77px でない（文字幅で決まる）ことを見る件を 1 件足し、通らなければ PanelActionButton.uss に Web の `--panel-action-button-width` の受け直し（`§3.2.7` の C# 導出）を足す
- [ ] **Step 3: IconButton の色**: `IconButton.uss` の色の規則をルートのつまみへ:

```css
/* 図形の色は置く側がルートの --icon-button-color で変える（Web は root の color を図形が currentColor で受ける） */
/* Callers recolour the glyph through the root's --icon-button-color (the Web glyph takes the root's color as currentColor) */
.icon-button.unity-button {
    --icon-button-color: var(--text-muted);
}

.icon-button.unity-button:hover:enabled {
    --icon-button-color: var(--text-high-contrast);
}

.icon-button .icon-button__glyph {
    --icon-button-glyph-color: var(--icon-button-color);
    --icon-button-glyph-stroke: var(--bevel-1);
}
```

`SkitToolbar.uss` の :35-49 の 3 規則は `.skit-toolbar .skit-toolbar__tool { --icon-button-color: var(--text-high-contrast); }`・`.skit-toolbar .skit-toolbar__tool--active { --icon-button-color: var(--select-cyan); }`・`.skit-toolbar .skit-toolbar__tool:disabled { --icon-button-color: var(--text-muted); }` へ（Web `SkitToolbar.module.css`:22 の `color`）。詳細度は IconButton の `:hover:enabled`（2 クラス＋2 擬似）が自動 ON（2 クラス）に勝ち、Web の `:not(:disabled):hover` と同じ。`.skit-tool-glyph` の寸法と影の規則（自分の要素）は残す。`IconButton` の L1（`Client.Tests/Ui/Components/Controls/` の IconButton のテスト）に 1 件「通常・ホバー・無効で図形の `--icon-button-glyph-color` の解決値が `--text-muted`・`--text-high-contrast`・`--text-muted`」を足す（変更前の値で先に緑を見てから USS を替え、緑のままを見る。全呼び手〈`BlockInventoryView`:55・`BuildMenuPanelSkeleton`:71・`SkitToolbar`:81〉の見た目は T19 の 3-build-menu・4-block-chest・2-skit-text で見る）

- [ ] **Step 4: スキットの選択肢（DC-21 案 A の規則で判定: Web の `GamePanelVariant` は `"default"`・`"craft"`・`"skit"`・`"hud"` で、`SkitChoiceList.module.css` の `.choice` は GamePanel を使わない素の button で板を `::before` で描く → `skit-choice` に当たる variant が無いので `SkitChoice` は画面側の USS へ移す＝削除）**: `GamePanelSurfaceKind.SkitChoice` と `GamePanelSurface.cs` の `KindClassName` の case と `GamePanelSurface.uss`:66-90（`--skit-choice` の 3 規則。:83 の `.unity-button:active` 起点を含む）を消す。`GamePanelSurface.cs` を読むと種別は面の子 1 つ（`GamePanelFace`）と本文の箱だけで、`Hud` の骨格に板に要らない子は無い（起草時に確かめた）。選択肢の板は `new GamePanelSurface($"skit-choice-{item.ChoiceId}-plate", GamePanelSurfaceKind.Hud)` に画面のクラス `skit-choice__plate` を付け、`SkitChoices.uss` に次を足す（詳細度は種別の `.game-panel-surface--hud`〈1 クラス〉に勝つよう 2 クラス）:

```css
/* 板は持ち主のボタンいっぱいに敷き、左右だけ水平フェード（Web .choice::before）。面の値は部品ルートのつまみで渡す */
/* The plate fills the owner button and fades only left and right (Web .choice::before); face values go through the component root knobs */
.skit-choices .skit-choice__plate {
    position: absolute;
    top: 0;
    right: 0;
    bottom: 0;
    left: 0;
    padding: 0;
    --game-panel-surface-face: var(--gauge-track);
    --game-panel-surface-edge-fade: var(--skit-choice-edge-fade);
    --game-panel-surface-top-fade: 0px;
    --game-panel-surface-bottom-fade: 0px;
}

/* 押している間は面をシアン寄りに（Web .choice:active::before） */
/* Push the face toward cyan while pressed (Web .choice:active::before) */
.skit-choices .skit-choice:active > .skit-choice__plate {
    --game-panel-surface-face: var(--skit-choice-active-face);
}
```

`GamePanelSurface.uss` の面の規則は、種別のルートが 4 つのつまみの既定を置き、面が受け直す形にする（`.game-panel-surface--hud { --game-panel-surface-face: var(--surface-navy); --game-panel-surface-edge-fade: var(--panel-edge-fade); --game-panel-surface-top-fade: var(--panel-edge-fade); --game-panel-surface-bottom-fade: var(--panel-edge-fade); }`・`--skit` も今の 4 値で同様、`.game-panel-surface > .game-panel-surface__face { --game-panel-face: var(--game-panel-surface-face); --game-panel-edge-fade: var(--game-panel-surface-edge-fade); --game-panel-top-fade: var(--game-panel-surface-top-fade); --game-panel-bottom-fade: var(--game-panel-surface-bottom-fade); }`）。`GamePanelSurfaceTest` に 1 件「Hud と Skit の面の `customStyle` の 4 値が種別の既定どおり」（変更前の値を先に読んで期待値にし、赤→緑でなく緑→緑で退行を見る）を足す。
  選択肢のボタンは `SkitChoiceButton` の ctor で `RemoveFromClassList(Button.ussClassName)` し、`SkitChoices.uss`:34-40 の `.unity-button` の打ち消しを消す。前例 0 件の手なので理由: Web の `.choice` は素の button（`background: none`・`border: 0`・`padding: 0`・押下の沈み無し）で、既定テーマ（Unity の既定ランタイムテーマと `Foundation/Theme/Theme.uss`:5-25）の `.unity-button` の面・枠・余白・ホバー・沈みを全部受けない。前例の形（部品の USS が `.x.unity-button` の 2 クラスで打ち消す。`IconButton.uss`:6-25・`RecipeActionButton.uss`:6-25）は Components の USS が持つ手で、画面の USS が `.unity-button` を書くのは本タスクが止める DC-21 の違反そのもの（Step 1 の検査が落とす）。`Button` の型は保つので `clicked`・`Clickable`・`focusable = false` の扱いは変わらない。板が `Hud` の骨格へ移ることで A-84 の棄却理由「板の面を Screens で描き直す: 面の格子の描き方が 2 本になる」には当たらない（描くのは今までどおり `GamePanelSurface` の `GamePanelFace` 1 本で、画面が渡すのはつまみの値だけ）
- [ ] **Step 5: 記帳**: `GamePanelSurfaceKind.Skit` は Web の `GamePanelVariant` の `"skit"`、`Hud` は `"hud"` と 1 対 1 なので残す旨を D-12 に書く（DC-21 A の「1 対 1 なら記帳」）。A-84 と設計書 §2.2 の `GamePanelSurface` 行の `--skit-choice`・§2.7 の「面は `GamePanelSurface` の種別 `skit-choice` `[A-84]`」を改める本文案はメモ（A-247 の行と設計書の改訂案）にある

Run: `uloop compile …` → `uloop run-tests … --filter-type regex --filter-value "UiStyleConventionTest|PauseMenu|Skit|GamePanelSurface|IconButton|BuildMenu|BlockInventory|Challenge|LookExceptionLedgerTest"` → 失敗 0

- [ ] **Step 6: コミット**: `refactor(uitk): 画面 USS の部品の内側への上書きをルートのつまみへ（DC-21。S5-crosscut T10）`

---

### Task 11: DC-34 A 不足の旗を 1 つに（R-14）

**Files:** Modify: `S/Client.Ui.State/BuildMenu/Logic/BuildMenuRequiredItem.cs`（`Lacking` と ctor の引数）・`Logic/BuildMenuShortage.cs`（:13）・`Source/BuildMenuMaterialRowsBuilder.cs`（:39）・テスト `BuildMenuEntryFactoryTest.cs`:122・`Client.Tests/Ui/State/BuildMenu/BuildMenuShortageTest.cs`:15-16（目標型の `new(guid, 4, 1, true)` の 4 引数。`new BuildMenuRequiredItem(` の grep に掛からない）・`Screens/BuildMenu/Support/BuildMenuReferenceDemo.cs`:83・`BuildMenuSamples.cs`:91。漏れは `git grep -n "BuildMenuRequiredItem" -- moorestech_client/Assets/Scripts` の全行で拾う

**Interfaces:** Produces: `BuildMenuRequiredItem(Guid itemGuid, int count, int held)`・`BuildMenuShortage.IsItemInsufficient(BuildMenuCatalogEntry entry, BuildMenuRequiredItem item)` は `!entry.PaymentWaived && item.Held < item.Count`

- [ ] **Step 1**: 既存の L0（`BuildMenuEntryFactoryTest`・`BuildMenuShortage` の L0）が緑なのを確かめる
- [ ] **Step 2**: `Lacking` を消し、`BuildMenuShortage` を `Held < Count` に。`BuildMenuMaterialRowsBuilder`:39 と `BuildMenuShortageTest.cs`:15-16 の第 4 引数を消す（後者の名前 `Lacking`・`Enough` の静的フィールドは `Held < Count` の値のまま残る）。`BuildMenuEntryFactoryTest`:122 の比較は DTO の `item.Lacking` と `line.Row.Item.Held < line.Row.Item.Count` を並べる形に（Web の DTO と同じ判定であることを見続ける）。`Client.WebUiHost` の DTO は State の型を使っていない（起草時の grep で 0 件）ので触らない
- [ ] **Step 3**: `uloop compile …` → `uloop run-tests … --filter-type regex --filter-value "BuildMenu"` → 失敗 0。コミット `refactor(uitk): ビルドメニューの不足の旗を IsInsufficient の 1 つへ（DC-34 A。S5-crosscut T11）`

---

### Task 12: DC-30 A トークンの値を CI で見る（R-15）

**Files:** Create: `tools/uitk/webTokens/writeOrCheck.mjs`・`tools/uitk/webTokens/tests/writeOrCheck.test.mjs`。Modify: `tools/uitk/convert-web-tokens.mjs`（:75-81 `writeOutput`）・`convert-mantine-tokens.mjs`（:44-46）・`.github/workflows/run_test.yml`（webui-test に step を 1 つ足す）・`.github/workflows/uitk-web-follow-gate.yml`（`uitk-web-follow-gate-selftest` の job に step を 1 つ足す）

**Interfaces:** Produces: `writeOrCheck(repositoryRoot, relativePath, text, checkMode)`（check のとき書かずに比べ、違えば `process.exitCode = 1`）・両変換器の `--check`

- [ ] **Step 1: テスト（赤）**

```js
// tools/uitk/webTokens/tests/writeOrCheck.test.mjs
import { test } from "node:test";
import assert from "node:assert/strict";
import { mkdtempSync, writeFileSync, readFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { writeOrCheck } from "../writeOrCheck.mjs";

test("check は同じ内容なら書かず exit 0 のまま", () => {
  const root = mkdtempSync(join(tmpdir(), "tokens-"));
  writeFileSync(join(root, "a.uss"), "same\n");
  process.exitCode = 0;
  writeOrCheck(root, "a.uss", "same\n", true);
  assert.equal(process.exitCode, 0);
});

test("check は違えば書かず exit 1", () => {
  const root = mkdtempSync(join(tmpdir(), "tokens-"));
  writeFileSync(join(root, "a.uss"), "old\n");
  process.exitCode = 0;
  writeOrCheck(root, "a.uss", "new\n", true);
  assert.equal(process.exitCode, 1);
  assert.equal(readFileSync(join(root, "a.uss"), "utf8"), "old\n");
  process.exitCode = 0;
});
```

- [ ] **Step 2: 作る**

```js
// tools/uitk/webTokens/writeOrCheck.mjs
// 生成物を書くか、--check のときは書かずに今のファイルと比べる（手で変えた・変換し忘れたトークンを CI で止める。DC-30）
// Write a generated file, or with --check compare it against the current file without writing (CI stops hand-edited or unconverted tokens; DC-30)
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";

export function writeOrCheck(repositoryRoot, relativePath, text, checkMode) {
  const absolutePath = join(repositoryRoot, relativePath);
  if (!checkMode) {
    writeFileSync(absolutePath, text);
    console.log(`wrote ${relativePath}`);
    return;
  }
  const current = existsSync(absolutePath) ? readFileSync(absolutePath, "utf8") : null;
  if (current === text) {
    console.log(`up to date ${relativePath}`);
    return;
  }
  console.error(`${relativePath} differs from the converter output; run the converter without --check and commit the result`);
  process.exitCode = 1;
}
```

両変換器の書き出し（`writeFileSync(join(repositoryRoot, …), …)` と `console.log("wrote …")`）を `writeOrCheck(repositoryRoot, <相対パス>, <本文>, process.argv.includes("--check"))` へ。200 行の検査（throw）は check でもそのまま走らせる

- [ ] **Step 3: 手元**: `node tools/uitk/convert-web-tokens.mjs --check; echo "exit=$?"` と `pnpm --dir moorestech_web/webui install --frozen-lockfile && node tools/uitk/convert-mantine-tokens.mjs --check; echo "exit=$?"` が両方 exit 0。1 でれば、差が手の変更か変換し忘れかを `git log -p` で調べ、変換器を回した結果をコミットして判断記録に 1 行（Web の値が正。I-fix6 の DC-20 が直したトークンと重なればその PR を書く）
- [ ] **Step 4: CI**: 置き場を 2 つに分ける（D-23）。依存の無い自己テストは前例（`uitk-web-follow-gate.yml`:52-53 の `node --test tools/uitk/testNameParity/tests/*.test.mjs`）に並べ、`uitk-web-follow-gate-selftest` の job の `Test the test-name parity tool` の下に足す:

```yaml
      # トークン変換器の書き出し・照合の自己テスト（依存なし）。master の PR でも走らせて破損を止める
      # Self-tests of the token converters' write-or-check (no dependencies); run on master PRs too so breakage is caught
      - name: Test the token converter write-or-check
        run: node --test tools/uitk/webTokens/tests/*.test.mjs
```

`--check` の 2 本は `convert-mantine-tokens.mjs` が webui の `node_modules`（Mantine の既定値）を読むので、`run_test.yml` の webui-test に step を足す。G1 と同じく「master の作業者に UITK の追従を求めない」ため、base（か head）が `feature/uitk` の PR でだけ走らせる（`uitk-web-follow-gate.yml`:20-22 の条件の形）。step を分けると前の step の `pnpm_binary` が消えるので同じ glob で解き直す:

```yaml
      # UITK のトークンが Web の CSS の変換と一致するか（DC-30）。G1 と同じく feature/uitk への PR と切替 PR だけで課す
      # Whether the UITK tokens match the conversion of the Web CSS (DC-30); like G1, enforced only on PRs into feature/uitk and the switch PR
      - name: Check UITK tokens against the Web CSS
        if: github.base_ref == 'feature/uitk' || github.head_ref == 'feature/uitk'
        shell: bash
        working-directory: moorestech_web
        run: |
          shopt -s nullglob
          candidates=(node/*/pnpm)
          pnpm_binary="$PWD/${candidates[0]}"
          "$pnpm_binary" --dir webui exec node ../../tools/uitk/convert-web-tokens.mjs --check
          "$pnpm_binary" --dir webui exec node ../../tools/uitk/convert-mantine-tokens.mjs --check
```

（`--dir webui exec` の cwd は `moorestech_web/webui` なので相対は `../../tools`。変換器はスクリプトの位置から repo の根を決めるので cwd に依らない。前の step の `pnpm install --frozen-lockfile` が入れた `node_modules` を使う）
- [ ] **Step 5**: `node --test tools/uitk/webTokens/tests/*.test.mjs` → 2 件 pass。コミット `ci(uitk): トークンの変換器に --check を足し webui-test で回す（DC-30 A。S5-crosscut T12）`

---

### Task 13: DC-32 A ゲーム寿命の型から IDisposable を外す（R-18）

**Files:** Modify: `GameUiInputInstaller`・`GameUiSrgbCompositeInstaller`・`GameUiLocaleClassBinder`（`Foundation/Text/GameUiLocaleClassBinder.cs`:13）・`CrosshairHudPresenter`・`TrainRidingHudPresenter`・`ProgressHudPresenter`・`KeyHintHudPresenter`・`CurrentChallengeHudPresenter`（取り込み後。D-20）・`NotificationLayerPresenter`・`ToastLayerPresenter`・`RecipeScreenPresenter`・`InventoryScreenPresenter`・`EquipmentHudPresenter`・`ScreenBackdropPresenter`・`PauseMenuScreenPresenter`・`BuildMenuScreenPresenter`・`NotificationFeedRelay`・`RecipeSelectionCloseReset`（T6 で移した先）

**Interfaces:** Produces: 各型の宣言が `: IInitializable`（または元の基底）だけ

- [ ] **Step 1: 一覧の照合**: `git grep -n "IDisposable" -- 'moorestech_client/Assets/Scripts/Client.Ui/**/*.cs' 'moorestech_client/Assets/Scripts/Client.Ui.State/**/*.cs'` を読み、ゲーム寿命（DI の Singleton・EntryPoint）か、短命（行・区画・ハンドル・購読の器）かを分ける。外すのは前者だけ。表を D-16 に書く
- [ ] **Step 2: Dispose を呼ぶテスト**: `NotificationLayeringTest.cs`:43（`_presenter?.Dispose()`）・`NotificationFeedRelayTest.cs`:42（`_relay.Dispose()`）・`RecipeSelectionCloseResetTest.cs`:40（`_reset.Dispose()`）の 3 行を消す（`TearDown` の残りの行は残す）。どれも源をテストごとに作り直すので、購読がテストの間で漏れない: `NotificationFeedRelay` は注入された `_feed.OnNotified`（`NotificationFeedRelay.cs`:30）を、`NotificationLayerPresenter` は `_model.Current`・`_stageScaleSource.Scale`（:46-47）を購読し、テストの `SetUp` が `NotificationFeed`・`NotificationModel`・`ReactiveProperty<float>` を毎回作る。`RecipeSelectionCloseReset` は `UiRoutingState` をテストごとに作り直す。静的な源（`Localize.OnLanguageChanged`）を購読する `GameUiLocaleClassBinder` と `GameUiSrgbCompositeInstaller` を作るテストは 0 件（起草後の `git grep -ln "new GameUiLocaleClassBinder\|new GameUiSrgbCompositeInstaller" -- moorestech_client/Assets/Scripts/Client.Tests`）。よって 17 本と `RecipeSelectionCloseReset` は例外なく外す（D-16）
- [ ] **Step 3: 外す**: 各型の `IDisposable` と `Dispose()` と、そのためだけのフィールド（`_subscription` の保持など）を消す。購読は `Subscribe(...)` の戻り値を捨てる形でよい（ゲーム寿命）。Step 2 の 0 件が取り込み後に変わっていない（静的な源を購読する型を EditMode の L1 が作っていない）ことを `git grep -n "Localize.OnLanguageChanged\|InputManager\.\|InputSystem.on"` と上の `new` の grep で確かめる。作るテストが増えていたら、IDisposable を残さず、そのテストが源の側を作り直す形へ直す
- [ ] **Step 4: 設計書**: §4.2 の型（:338-344 の `IInitializable, IDisposable` と `public void Dispose();`）を `IInitializable` だけにする改訂案をメモへ
- [ ] **Step 5**: `uloop compile …` → `uloop run-tests … --filter-type regex --filter-value "^Client\.Tests\.Ui\."`（`--timeout-seconds 1500`）→ 失敗 0。コミット `refactor(uitk): ゲーム寿命の Presenter・Relay・Binder・Installer から IDisposable を外す（DC-32 A。S5-crosscut T13）`

---

### Task 14: DC-31 A Presenter の結線と `uiState.spec`（R-16・R-3 の一部）

**Files:** Create: `S/Client.Tests/Ui/Crosscut/PresenterMountTest.cs`・`S/Client.Tests/EditModeInPlayingTest/Ui/Crosscut/GameUiScreenMountTest.cs`。Modify: `S/Client.Tests/Ui/Screens/BlockInventory/BlockInventoryPanelFlowTest.cs`:54（タグを 1 行足す）

**Interfaces:** Consumes: `GameUiRootTestFactory.Create(GameObject, VisualElement)`（`Client.Tests/Ui/Screens/Inventory/Support/GameUiRootTestFactory.cs`）・`UiRoutingStateTestFactory.CreateSwitchable(GameObject, UIStateEnum, IReadOnlyList<UIStateEnum>, out UIStateControl)`（同 `UiRoutingStateTestFactory.cs`:26）・`StageColumnsLayout`・`InventoryScreenPresenter(StageColumnsLayout, UiRoutingState, PlayerInventoryPanelModel, IPlayerSlotCommands, IInventorySortCommands, GameContentPresentation, UiCommandResultToaster)`・`RecipeScreenPresenter(StageColumnsLayout, UiRoutingState, RecipeScreenModel, GameContentPresentation, IRecipeCommands, UiCommandResultToaster, RecipeItemSelection …)`・`BlockInventoryScreenPresenter(StageColumnsLayout, UiRoutingState, BlockInventoryPanelLayoutModel, BlockInventoryPartRegistry, IBlockInventoryPanelCommands)`・`UiStateControlTransitionRequester.RequestTransition(UIStateEnum)`・`UiSystemPlayModeTestBase`（S4-gamma が作った L3 の基底。T0 で置き場を確かめる）

- [ ] **Step 1: PresenterMountTest（赤→緑）**: 組み立ては既存のテストの SetUp から写す（持ち物 `InventoryScreenVisibilityTest.cs`:32-41、レシピは `git grep -ln "new RecipeScreenPresenter(" -- moorestech_client/Assets/Scripts/Client.Tests` の SetUp、ブロックは `BlockInventoryPanelFlowTest` の SetUp）。新しい土台の型は作らず、写した組み立てはこのクラスの private メソッドにする（200 行を超えるなら `Crosscut/PresenterMount/` に土台 1 本と試験 1 本へ分ける）。試験 5 件（1 件ずつ `/// <web-test file="e2e/tests/uiState.spec.ts">…</web-test>`）:
  - `既定(PlayerInventory)でインベントリ画面が表示される` → 切り替えられる routing を PlayerInventory で作り、持ち物の View が出ている・レシピの View が出ている
  - `GameScreen でパネルが消え、PlayerInventory への event で再表示される` → control を GameScreen へ → 持ち物・レシピが隠れる → PlayerInventory へ → 再び出る
  - `SubInventory でインベントリ+ブロックパネルが出てレシピビューアは消える` → SubInventory（ブロックのパネルを開いた状態の源）→ 持ち物とブロックが出て、レシピが隠れる
  - `許可表で拒否されたui_state.requestはtransition_not_allowedを返し画面を維持する` → BuildMenu で `RequestTransition(UIStateEnum.PlayerInventory)` が false・`LogAssert.Expect(LogType.Warning, …)`（拒否のログ）・画面が BuildMenu のまま
  - 4 件目 `ブロックパネルの✕で ui_state.request(GameScreen) を送り画面が閉じる` は既存の `BlockInventoryPanelFlowTest`:54「閉じるを押すと遷移を頼み状態が閉になると消える」に `/// <web-test file="e2e/tests/uiState.spec.ts">ブロックパネルの✕で ui_state.request(GameScreen) を送り画面が閉じる</web-test>` を足す（同じ意図。新しい試験は作らない）
- [ ] **Step 2: L3 の網（赤→緑）**: `GameUiScreenMountTest`（`[UnityTest]`。editmode-in-playing-test スキルの形。`yield return new EnterPlayMode` は `[UnityTest]` の直下）: 全体スイッチ GameUi で起動し、DC-31 の症状（Presenter の購読を消す・載せる層を取り違えても緑のまま、本番でホットバーが空）を捕まえる 3 段を見る。足りない・違うものは全部並べて 1 回で落とす
  - (a) 一覧の作り方: Presenter が載せる最上位の View と層を `git grep -n "GetLayer(UiLayer\.\w*, UiFamily\.\w*)" -- 'moorestech_client/Assets/Scripts/Client.Ui/Screens/**/*Presenter.cs'`（起草時 20 行。例 `HotbarHudPresenter.cs`:38 の `GetLayer(UiLayer.Chrome, UiFamily.Viewport).Add(view)`）と各 Presenter の `new <View>(` から拾い、テストの中に `(View の型, UiLayer, UiFamily)` の表として書く（対象: `InventoryView`・`RecipeViewerView`・`BlockInventoryView`・`BuildMenuView`・`ResearchTreeView`・`PauseMenuView`・`HotbarHudView`・`EquipmentHudView`・`ProgressHudView`・`KeyHintHudView`・`CrosshairHudView`・`TrainRidingHudView`・`NotificationHostView`・`ToastHostView`・`CursorTooltipView`・`ScreenBackdropView`・`TutorialOutlineView`・`WorldPinOverlayView`・`KeyControlHintView`・`ModeHudView`・`GrabOverlayView`・`CurrentChallengeHudView`・スキットの View と、T14 の時点で在れば S5-challenge の `ChallengeListView`〈`Screens/Challenge/Tree/`。無ければ S5-challenge が自分の PR で足す。D-20〉。表の行数と grep の行数が合わなければ赤）
  - (b) 載り先の層: 各行で `GameUiRoot.Instance.GetLayer(<UiLayer>, <UiFamily>).Q<T>()` が 1 つ見つかり、ルート全体の `Query<T>().ToList().Count` が 1（別の層に載っていない・二重に載っていない）。`Q<T>()` をルートから探すだけの形は層の取り違えを見逃すので使わない
  - (c) Render が届いた形: 起動直後に描かれる HUD は、Model の購読から Render が届いた印を見る。必須はホットバー: `HotbarHudView` の `Q("hotbar-grid").childCount` が 0 でない（`HotbarHudView.Render` が割当の長さに枠を作る。`HotbarHudView.cs`:62-80）。開いて描かれる画面は 1 つ: `UiStateControlTransitionRequester.RequestTransition(UIStateEnum.PlayerInventory)` の後の数フレームで `InventoryView` の持ち物の枠（T0 Step 3 で実物のクラス名を引く）が 0 でない。他の HUD で起動直後に子を持つものは T0 Step 3 で実物を見て足し、足した行を判断記録の「読み替え」に書く
- [ ] **Step 3: 段ごとの L3**: 各段の L3（S2-ladder の `Stage2/`・S3 の L3・S4-gamma の `Stage4/`・S5-ladder の段 5）は持ち主のレーンの物を数え、無い段を `done.md`「integrator への依頼」に書く（D-14）
- [ ] **Step 4**: `uloop compile …` → `uloop run-tests … --filter-type regex --filter-value "PresenterMountTest|BlockInventoryPanelFlowTest|GameUiScreenMountTest"` → 失敗 0（L3 の後は 45 秒待ってから次）。コミット `test(uitk): Presenter の結線の L1 と全画面の載り先の L3 を足す（DC-31 A・uiState.spec。S5-crosscut T14）`

---

### Task 15: DC-23 A Web ホストの判定を State へ付け替え（R-19）

**Files（左を右の呼び出しへ付け替える）:**
- `S/Client.WebUiHost/Game/Actions/UiStateActions.cs`（:54 `IsAllowed`）→ `UiStateTransitionRules.IsAllowed`（`Client.Ui.State/Routing/UiStateTransitionRules.cs`:11）
- `Client.WebUiHost/Game/Actions/CraftActions.cs`（:26 `ResolveCraftRecipe`）→ 解放の検査を State の純関数 `CraftRecipeUnlockCheck.Check(IGameUnlockStateData, Guid) : UiCommandRejectReason?`（新設。`RecipeCommands.Craft`:29-30 もそれを呼ぶ）。wire は `invalid_recipe`・`recipe_locked` のまま。理由→wire の写像は既存の正本 `UiCommandRejectReasonTraits.ToWireCode`（`Client.Ui.State/Commands/UiCommandRejectReason.cs`:154-174）が持つので、`CraftActions` は switch を書かずそれを呼ぶ（I-fix5 の改名〈`RecipeInvalid`／`RecipeLocked`〉の追従も 1 か所で済む）
- `Client.WebUiHost/Game/Topics/CraftingRecipesTopic.cs`（:45-60）→ `CraftRecipeCatalog.BuildUnlocked`（`Client.Ui.State/Recipe/Catalog/CraftRecipeCatalog.cs`:15）の結果を DTO へ写すだけ
- `Client.WebUiHost/Game/Topics/RecipeViewerItemListTopic.cs`（:56-82）→ `RecipeViewerItemCatalog.BuildVisibleItems`
- 列車（s4-train 計画 :5587-5594 の 1〜8 行目）: `TrainTimetableDtoBuilder.Build`/`SortStations` → `TrainTimetableStationCatalog`・`TrainTimetableReadyDataFactory`／`TrainTimetableStopParser` → `TrainTimetableDeparture.ToServerStops`／`TrainTimetableActions`（置換・`AppliedIsAutoRun`・拒否の文字列）→ `TrainTimetableCommands`・`TrainCommandRejection`／`TrainPlatformDetailDtoBuilder`・`TrainStationDetailDtoBuilder` → `TrainPlatformDetailReader.Read`／`TrainPlatformActions.IsTrainPlatform` → `BlockComponentRegistry` の判定（`TrainBlockCommands.SetTransferMode` と同じ）／`ToWireMode`・`ParseMode` → `TrainPlatformTransferModeConversion.FromServer`・`ToServer`／`TrainStationActions` → `TrainBlockCommands.SetStationName` の解決・検査／`TrainInventoryDtoFactory` の液体 → `TrainCarFluidConversion`
- Create: `S/Client.Ui.State/Recipe/Commands/CraftRecipeUnlockCheck.cs`
- Test: `S/Client.Tests/WebUi/` の既存（`git grep -ln "UiStateActions\|CraftActions\|CraftingRecipesTopic\|RecipeViewerItemListTopic\|TrainTimetable\|TrainPlatform\|TrainStation\|TrainInventoryDto" -- moorestech_client/Assets/Scripts/Client.Tests/WebUi`）

**Interfaces:** Consumes: 上の State の型（`Client.WebUiHost.asmdef` は既に `Client.Ui.State` を参照）。Produces: `CraftRecipeUnlockCheck.Check(IGameUnlockStateData unlockState, Guid recipeGuid)`（解放表に無い→`RecipeInvalid`、未解放→`RecipeLocked`、解放済み→null。I-fix5 の改名後の名前）

- [ ] **Step 1: 対ごとの照合（赤の代わり）**: 各対で、Web 側の判定と State 側の判定が同じ入力で同じ結果を返すかを読む。違う所（例: Web は辞書に無いレシピを黙って飛ばし、State は `silent-ok` で飛ばす）は表にして D-17 に書き、Web の wire（文字列・DTO の形）を変えない向きに State を呼ぶ。変えざるをえない対は付け替えず、理由を D-17 と `done.md` に書く
- [ ] **Step 2: 付け替え**: 遷移とクラフトから。`UiStateActions.IsAllowed` を消して呼び手を `UiStateTransitionRules.IsAllowed` に。`CraftRecipeUnlockCheck` を作り、`RecipeCommands.Craft` と `CraftActions.ResolveCraftRecipe` が呼ぶ（`JValue` の型検査と `Guid.TryParse` は Web 側に残す）。

```csharp
// S/Client.Ui.State/Recipe/Commands/CraftRecipeUnlockCheck.cs
using System;
using Client.Ui.State.Commands;
using Game.UnlockState;

namespace Client.Ui.State.Recipe.Commands
{
    /// <summary>
    /// クラフトのレシピを送ってよいかの唯一の判定。解放表に無い→RecipeInvalid、未解放→RecipeLocked、送ってよい→null（UITK の RecipeCommands と Web の CraftActions が呼ぶ。DC-23 A）
    /// The single decision on whether a craft recipe may be sent: absent from the unlock table → RecipeInvalid, locked → RecipeLocked, sendable → null (called by the UITK RecipeCommands and the Web CraftActions; DC-23 A)
    /// </summary>
    public static class CraftRecipeUnlockCheck
    {
        public static UiCommandRejectReason? Check(IGameUnlockStateData unlockState, Guid recipeGuid)
        {
            if (!unlockState.CraftRecipeUnlockStateInfos.TryGetValue(recipeGuid, out var info)) return UiCommandRejectReason.RecipeInvalid;
            if (!info.IsUnlocked) return UiCommandRejectReason.RecipeLocked;
            return null;
        }
    }
}
```

`RecipeCommands.Craft` の :29-30 は `var rejection = CraftRecipeUnlockCheck.Check(_unlockState, recipeGuid); if (rejection.HasValue) return Reject(rejection.Value);` に。`CraftActions.ResolveCraftRecipe` の :37-38 は次へ（`using Client.Ui.State.Commands;`・`using Client.Ui.State.Recipe.Commands;` を足す）:

```csharp
            // 存在しないレシピと未解放レシピは State の判定で弾き、失敗コードは State の写像で綴る（サーバーはunlock検証を行わない）
            // Unknown and locked recipes are rejected by the State check and spelled by the State mapping (the server does not validate unlock state)
            var rejection = CraftRecipeUnlockCheck.Check(unlockStateData, recipeGuid);
            if (rejection.HasValue) return ActionResult.Fail(UiCommandRejectReasonTraits.ToWireCode(rejection.Value));
```

同じ関数の :32-33（`JValue` でない・`Guid.TryParse` が失敗）の `ActionResult.Fail("invalid_recipe")` も `ActionResult.Fail(UiCommandRejectReasonTraits.ToWireCode(UiCommandRejectReason.RecipeInvalid))` にする（同じ失敗コードを文字列と写像の 2 か所で綴らない。wire の文字列は `invalid_recipe` のまま）。Topic 2 本は Catalog を呼んで DTO へ写すだけにする。列車の 8 対を 1 対 1 コミットで進める
- [ ] **Step 3: テスト**: 対ごとに `uloop run-tests … --filter-type regex --filter-value "^Client\.Tests\.WebUi\.|UiStateTransitionRules|RecipeCommands|CraftRecipeCatalog|RecipeViewerItemCatalog|TrainTimetable|TrainPlatform|TrainBlockCommands"` → 失敗 0
- [ ] **Step 4: 判断記録**: D-17 に「conductor 11-10 04:50 の『据え置き・X-5 で Web ホスト側を消す』を、この決定（DC-23 A）で覆す。X-5 は付け替え済みの Web ホストを消すだけ」を書く
- [ ] **Step 5: コミット**: 対ごとに `refactor(webui-host): <対> の判定を State の <型> へ付け替える（DC-23 A。S5-crosscut T15）`

---

### Task 16: Warning の拾い（R-20 の一部。c22-si・c46-cc・c63-cc）

**Files:** Modify: `S/Client.Ui.State/Text/UiText.cs`（`Empty`）・`S/Client.Ui/Foundation/Text/LocalizedLabel.cs`（ctor `LocalizedLabel(UiText initial)`。I-fix5 後の実物）・`Screens/BlockInventory/Special/SpecialViewParts.cs`（:23 `SeedEmptyText` と 5 呼び手）・`Screens/BlockInventory/Fluid/FluidInventoryRow.cs`（:56）・`Screens/Skit/Parts/Window/SkitDialogueWindow.cs`（:97）・`Screens/Skit/Parts/SkitBackgroundLine.cs`（:18）

**Interfaces:** Produces: `UiText.Empty`（`FromVerbatim(string.Empty)` の静的な 1 つ）・`LocalizedLabel(UiText initial)`

- [ ] **Step 1**: c22-si（表示 helper）は T2 で拾った。c46-cc（研究の現在画面の読み）は T7 で拾った。c63-cc（readonly フィールド 31 ファイルと `{ get; }`）は見送る: 規約はどちらも許し、故障シナリオが無い。X-5 の規約の整理へ申し送る（D-19）
- [ ] **Step 2: 空ラベル（赤→緑）**: `LocalizedLabelTest` に「`new LocalizedLabel(UiText.Empty)` を載せても『文なし』の警告が出ず text が空」を足す。`UiText` に `public static readonly UiText Empty = FromVerbatim(string.Empty);`、`LocalizedLabel` に ctor を足し、4 か所と `SeedEmptyText` の 5 呼び手を置き換えて `SeedEmptyText` を消す
- [ ] **Step 3**: `uloop compile …` → `uloop run-tests … --filter-type regex --filter-value "LocalizedLabel|UiText|Special|Fluid|Skit"` → 失敗 0。コミット `refactor(uitk): 空の LocalizedLabel を ctor で作る（X-audit c22-si。S5-crosscut T16）`

---

### Task 17: AC-5 単一参照の helper をローカル関数へ（R-20）

**Files（48 件。S 相対。「→」は呼び手）:** BlockNetworkInfoCache.cs:172 FetchFilterSplitter→Track（既存の region へ）・SpecialStateDetailReader.cs:31→GearToElectric・HotbarSelection.cs:23 Of→FromOrigin・BugReportFormText.cs:101,112,123,133,143（123→112・133→101 は入れ子。I-fix6 の DC-22 A 後の実物）・ResearchTreeModel.cs:92 Rebuild→RebuildWhileShown・TrainPlatformEditCommands.cs:45,52・TrainStationNameEditState.cs:54→Initialize・TrainTimetableModel.cs:92→Build・TrainTimetableReadyDataFactory.cs:46→TryCreate・TrainTimetableEditCommands.cs:64→SetAutoRun・TrainTimetableEditSession.cs:78,99,108（3 段の入れ子）・TrainTimetableEditState.cs:53,65,77（3 段の入れ子）・ModeSwitch.cs:101→ctor（T9 の後）・ToastItem.cs:32→ctor・UiDragDrop.cs:83→TryFindTarget（外の TTarget を使う）・GameUiInputRules.cs:84→OnKeyDown・:97,:123→OnFocusIn・UiPressGesture.cs:113→ctor・UiOsCursorRequestStack.cs:44 FindTopShape→ApplyTopIfChanged・UiScreenPixelScope.cs:31→ctor・TrainTimetableStationListView.cs:51→Render・TrainTimetableStopListView.cs:60→Render・BugReportFormView.cs:107,173,185（I-fix6 の取り込み後に、移して 200 行未満に収まるときだけ。D-24）・BuildMenuEntrySlot.cs:52→ctor・CursorTooltipView.cs:63→OnAttached・HotbarHudView.cs:55→Render（既存の region へ）・ModalView.cs:65→ctor・:110→CreateInput（入れ子）・PauseMenuView.cs:160→SetShown・ItemListView.cs:106→ctor（T3 の置き換えから外した 3 辺の式のまま移す。D-6）・CraftHoldDriver.cs:71→Start・RecipeScreenPresenter.cs:55→Initialize・GraphicsQualitySettingsView.cs:68→ctor・LanguageSelectView.cs:51→ctor・ToastHostView.cs:89 ToneOf→Push（T5 の後）・WorldPinOverlayView.cs:112→ctor

除外（D-18）: `UiScreenTraits.cs`:49 BuildTraits（呼び手が静的フィールドの初期化子で、移すと静的 ctor が要る）・`UiOsCursorRequestStack.cs`:29 Withdraw（入れ子の型 `ShapeRequest.Dispose` から呼ぶ）・`HoverTooltipHost.cs`:143 OnTrackedDetached（`Track` の中で登録と解除に同じ method group を渡し、`Track` が複数回走る。ローカル関数が `this` 以外を捕まえると解除の等価が崩れる）・`BlockSlotRestrictionInput.cs`:91（AC-8）・イベントコールバックの約 45 件（DC-33。X-5）

**Interfaces:** なし（挙動不変）

- [ ] **Step 1: 照合**: 各行を実物で開き、呼び手が 1 つだけかを `git grep -n "<名前>"` で確かめる（同名の別クラスの私有メソッドは別物）。前のタスクで消えた・形が変わったもの（T3 の `ItemListView`・T5 の `ToastHostView`・T9 の `ModeSwitch`）と I-fix6 が直したもの（`BugReportFormText.cs`・`BugReportFormView.cs`）は今の形で判断する
- [ ] **Step 2: BugReportFormView の扱いを決める**: `BugReportFormView.cs` は X-audit DC-22（バグ報告フォームの文字と送信の持ち主）の対象で、I-fix6 が持つ（I-fix6 は案 A〈`DescribeDelivery`/`DescribeExport` を `UiText` で返す〉を採り、送信を View の外へ出す案 B は採っていない）。このレーンは送信の手順を View の外へ動かさず、新しい型（`BugReportFormSender` 等）も作らない。I-fix6 の取り込み後の実物で `wc -l` を取り、3 件（:107・:173・:185。行は取り込み後に `git grep -n` で引き直す）を呼び手の `#region Internal` へ移しても 200 行未満なら移す。超えるなら 3 件とも落とし、`done.md`「割り当ての処遇」の AC-5 の行に「BugReportFormView の 3 件は DC-22 の持ち主（I-fix6）の結論を動かさないため見送り。200 行の分割は DC-22 の案 B（送信を Presenter へ。前例 TrainPlatformBodyPresenter）と同じ決定になるので X-5 へ申し送る」と書く（D-24）
- [ ] **Step 3: 移す**: 1 ファイルずつ、helper を呼び手の `#region Internal` へ（無ければ作る。`#endregion` の下にコードを書かない）。入れ子の 4 組は内側から移す
- [ ] **Step 4: 行数**: `wc -l` で触ったファイルが全部 200 行未満（起草時の見込みで危ういのは `BlockNetworkInfoCache.cs` 187→189・`HoverTooltipHost.cs` は除外・`TrainTimetableEditSession.cs` 175→177）
- [ ] **Step 5**: `uloop compile …` → `uloop run-tests … --filter-type regex --filter-value "^Client\.Tests\.Ui\.|^Client\.Tests\.WebUi\."`（`--timeout-seconds 1500`）→ 失敗 0。コミットはディレクトリごと（`refactor(uitk): 単一参照の helper をローカル関数へ（AC-5 <範囲>。S5-crosscut T17）`）

---

### Task 18: 束 5 の横断テストの写し（R-3）

**Files:** Create: `S/Client.Tests/Ui/Crosscut/Localization/LanguageRoundTripTest.cs`・`S/Client.Tests/EditModeInPlayingTest/Ui/Crosscut/AllScreensLanguageSwitchTest.cs`・`S/Client.Tests/Ui/Crosscut/TextSelection/TextSelectionTest.cs`。Modify: `S/Client.Tests/Localization/Resolution/LocalizationDictionaryCoverageTest.cs`（1 件足す＋タグ）・`S/Client.Tests/Localization/Resolution/LocalizeTest.cs`（1 件足す＋タグ）・`S/Client.Tests/Ui/Input/GameUiInputRulesTest.cs`（タグ 2・試験 1）・L3 の Esc（下）

**Interfaces:** Consumes: `Localize.TrySetChosenLanguage(string)`（`Client.Localization/Localize.cs`:91）・`Localize.OnLanguageChanged`・`VanillaLocalizationTable.SourceTexts`・`LanguageCatalog.Languages`・`LanguageSelectView`（`ModeSwitch` の選択肢の testid）・`GameUiInputRules.Attach(VisualElement)`・`ITextSelection.isSelectable`（UITK の `Label.selection`・`TextField.textSelection`）

写す 16 件と置き場（1 件ずつ `/// <web-test file="<webui 相対>"><第 1 引数></web-test>`。describe は含めない）:

| Web のファイル | Web のテスト名 | 写し先 |
|---|---|---|
| `e2e/tests/system/i18n.spec.ts` | `localization.current切替でlocale別辞書を再取得し表示を更新する` | `LanguageRoundTripTest`: ポーズメニューの設定を載せ、`Localize.TrySetChosenLanguage("japanese")` で見出しの文が日本語の辞書の値に変わる（「再取得」は UITK では辞書の切替） |
| 同 | `ポーズメニューの言語選択で英語と日本語を往復する` | `LanguageRoundTripTest`: `LanguageSelectView` の日本語の選択肢を `simulate.Click` → 文が日本語、英語を押す → 英語に戻る（後始末で言語と PlayerPrefs を戻す。前例 `LocalizedLabelTest`:23-41） |
| `src/shared/i18n/allScreensI18n.test.ts` | `re-renders translated screen copy after localization.current changes with no legacy screen exemptions` | `AllScreensLanguageSwitchTest`（L3）: GameUi で起動し、英語の文のうち日本語と違うものの集合 E を辞書から作り、日本語へ切り替えた次のフレームに、ルート以下の全 `Label` の text が E に入っていない（除外の画面を作らない） |
| 同 | `marks a failed requested locale without replacing the previous ready dictionary` | UITK 側の言語の L0 `Client.Tests/Localization/Resolution/LocalizeTest.cs` に 1 件: `japanese` を選んだ後に存在しない言語コード（`klingon`）で `TrySetChosenLanguage` が false、`GetCurrentLanguageCode()` が `japanese` のまま、辞書の文も日本語のまま、`OnLanguageChanged` が流れない（拒否の LogWarning は `LogAssert.Expect`。前例 `LocalizeUnchosenLanguageTest.cs`:101-118 は未選択の状態からの拒否だけを見る）。Web ホストの契約テスト `Client.Tests/WebUi/Localization/LocalizationLanguageSelectionContractTest.cs` には置かない（X-5c が「5c-消す」で丸ごと消し、UITK 側でこの挙動を守る試験が一緒に消える） |
| 同 | `requires non-empty Source and every language cell for every generated key` | `LocalizationDictionaryCoverageTest.EveryVanillaKeyHasNonEmptyTextInEveryLanguage` にタグ、同ファイルに `EveryVanillaKeyHasNonEmptySourceText`（`VanillaLocalizationTable.SourceTexts` の値が全部非空）を足しタグを付ける。1 つの Web テストに 2 つの NUnit が付くので、ツールの数え方（Step 3）で 1 件として数えられるかを確かめ、数えないなら 2 つ目はタグ無し |
| `e2e/tests/uiState.spec.ts` | 5 件 | T14 で写した |
| `e2e/tests/regression/inputLayer.spec.ts` | `block inventory上のEscapeはGameScreen遷移を要求する` | L3（既存 `EditModeInPlayingTest/Ui/GameUiInputTest.cs` に 1 件足すか、`Crosscut/` に置く）: チェストを開き、Esc（`InputManager.UI.CloseUI.SetKeyDownForTest(true)` 前例 `UiInputExclusivityEscapeLatch` 系）で UIState が GameScreen |
| 同 | `Tabはブラウザのフォーカスを動かさない` | `GameUiInputRulesTest.NavigationMoveNextDoesNotMoveFocus`（:142）にタグ |
| 同 | `ボタンにフォーカスしてSpaceを押してもclickせずキーハンドラにも届かない` | `GameUiInputRulesTest.NavigationSubmitDoesNotClickFocusedButton`（:127）にタグ（ボタンがフォーカスを持てないことは `ButtonFocusIsBlurredWithWarning`:99 が見る） |
| 同 | `checkboxではSpaceでトグルせず、contenteditableには空白が入る` | `GameUiInputRulesTest` に 1 件: `Toggle` に submit を送っても値が変わらず、`TextField` に Space の文字入力を送ると空白が入る |
| `e2e/tests/system/textSelection.spec.ts` | `入力欄以外はテキスト選択できない` | `TextSelectionTest`: 持ち物・ビルドメニュー・ポーズメニューを載せ、全 `Label` の `selection.isSelectable` が false |
| 同 | `建設メニューの検索入力はテキスト選択できる` | `TextSelectionTest`: ビルドメニューの検索欄の `TextField` の `textSelection.isSelectable` が true |

`regression/inputLayer.spec.ts` の残る 1 件（ホイール）は既に `EquipmentWheelFlowTest.cs`:112 にある。

- [ ] **Step 1: 書く**: 上の表のとおり。新しいファイルは `Client.Tests/Ui/Crosscut/` の下（10 ファイルを超えるならサブディレクトリ）。L3 は 45 秒待ちの注意を守る
- [ ] **Step 2: 走らせる**: `uloop run-tests … --filter-type regex --filter-value "LanguageRoundTripTest|LocalizationDictionaryCoverageTest|LocalizeTest|GameUiInputRulesTest|TextSelectionTest"` → 失敗 0。L3 は `--filter-value "AllScreensLanguageSwitchTest|GameUiInputTest"`（別の回）→ 失敗 0
- [ ] **Step 3: 突き合わせ**: `node tools/uitk/test-name-parity.mjs --bundle 5 --emit-missing > moorestech_client/.uloop/outputs/compare/S5-crosscut/parity-t18.txt; echo "exit=$?"`。`grep -E "i18n.spec|allScreensI18n|uiState.spec|inputLayer.spec|textSelection.spec|connection.spec" parity-t18.txt` が 0 行。束 5 の他のファイル（チャレンジ・言語ゲート・チュートリアル等）の未対応は S5-challenge・S5-gate・S5-ladder の物なので exit 1 でもよい。件数（Web／写した／捨てた／未対応）を `done.md` に写す
- [ ] **Step 4: コミット**: `test(uitk): 束 5 の横断テスト（i18n・allScreensI18n・uiState・inputLayer・textSelection）を写す（S5-crosscut T18）`

---

### Task 19: 比較撮影と独立評価（R-17）

**Files:** 出力 `moorestech_client/.uloop/outputs/compare/S5-crosscut/`（コミットしない）・台帳 `docs/uitk/TODO.md`（「## 実測の結論」「## 見た目の例外」の末尾）

| 状態（参照の id） | 見るもの | 撮り方 |
|---|---|---|
| `1-pause-top` | ボタンの幅（T10）・指カーソルの副作用なし（T9） | 既存のポーズメニューの撮影（`git grep -ln "CaptureToPng" -- moorestech_client/Assets/Scripts` で持ち主のレーンが作った撮影の試験を探して回す） |
| `2-skit-choices`・`2-skit-text` | 選択肢の板・ツールの色（T10）・スキットの面（T10 Step 4）・送りの印（T4） | 既存のスキットの撮影（`uitk-skit-compare.cs` の録画の撮影段か EditMode の撮影） |
| `3-build-menu`・`4-item-list-large`・`4-research`・`4-block-chest`・recipe の `4-recipe-selected-craft`（`web-9ce5557bdb-s4-recipe`。3 枚のうちクラフトのボタン・レシピ行の枠・`RecipeEntryList` の端が同時に写るもの。`4-recipe-holding`・`4-recipe-selected-machine` は T4・T3 の変更が写らないので比べない） | スクロールの端（T3。`4-item-list-large` は `ItemListView` を置き換えないので退行 0 の確かめ）・表示（T2）・× の色（3-build-menu・4-block-chest。T10 Step 3）・GamePanel の面と ×（T4）・クラフトのボタンとレシピ行の枠（4-recipe-selected-craft。T4）・材料不足の赤（3-build-menu。T11） | 各画面の既存の撮影 |
| `2-challenge-hud-multiple-long` | `GamePanelSurface` の hud の面（T10 Step 4） | 既存のチャレンジ HUD の撮影（S5-challenge が先に入っていればその撮影） |
| `3-modal`・`4-block-machine` | ダイアログの影・進捗の矢印の影（T4） | 既存の撮影 |
| `1-toast`・`2-notifications` | 出入り（T5） | 既存の撮影（`uitk-notification-compare.cs`） |
| `5-japanese-inventory` | 日本語の折り返し（T16・T18） | 持ち物の撮影を日本語で |
| `1-bug-report` | フォームの分割（T17） | 既存の撮影 |
| `2-world-pin-offscreen`・`5-tutorial-outline-label`・`2-train-hud`・`2-tooltip`・`2-key-control-hint`・`2-progress-labeled`・`3-hotbar-equipment-empty` | T2 で「インラインの Flex」から「インラインを外して USS に任せる」へ変えた HUD・チュートリアルの要素（`WorldPinEdgeArrowView`・`TutorialHighlightLabelView`・`TrainRidingHudView`・`CursorTooltipView`・`KeyControlHintView`・`ProgressHudView`・`HotbarHudView`）が出ている。親の子孫セレクタが既定で `display: none` にしていた要素は、これまでインラインの Flex で勝っていて、T2 の後は出なくなりうる（T2 Step 2 の `grep -B3` では要素と結び付けにくい形） | 既存の撮影（`uitk-hud.cs`・`uitk-tutorial-cues.cs`・`uitk-mode-hud-and-tooltip.cs` の撮影段か EditMode の撮影。道具で撮れない状態は Step 1 の扱い） |

- [ ] **Step 1: 撮る**: 上の各状態を 1280×720 と 2560×1080 で撮る（既存の撮影の試験・録画の撮影段を回すだけ。使い捨ての撮影スクリプトを書かない）。道具で撮れない状態は理由を `done.md`「integrator への依頼」に書く
- [ ] **Step 2: 評価**: opus の subagent 2 体に、別々に「参照の PNG と UITK の PNG の組・読者は UITK と Web の見た目を揃える開発者・記帳済みの LX の一覧（台帳「## 見た目の例外」）」だけを渡し、差を「場所・差・画素か寸法の実測」で返させる（経緯は渡さない）
- [ ] **Step 3: 再現**: 別の subagent に、指摘ごとに画素・寸法を測って再現させ、再現したものだけ残す。このレーンの変更が原因なら直して撮り直す。このレーン以前からの差は持ち主（S5-ladder の締め）へ `done.md` で回す。直さない差は LX-205〜206 に記帳（台帳の行とコードの `/* look-exception: LX-n */`）
- [ ] **Step 4**: `done.md`「差の表」に全行の処遇。台帳「## 実測の結論」に T1 の UITK の例外の扱いの実測を 1 行（UITK 以外の配送元〈パケット受信・モデル更新〉の扱いは L1 で固定していないことも並べて書く。D-35）。コミット `docs(uitk): S5-crosscut の並べ比べの結論を台帳へ（S5-crosscut T19）`

---

### Task 20: 録画（R-21）

下のシナリオのパスは起草時の `scenarios/uitk/` 直下のもの。I-fix5 の 10n が段ごとのサブディレクトリへ移すので、T0 Step 3 の「読み替え」の実物のパスで流す（`run-scenario.sh` は見つからないパスを流せない）。

- [ ] **Step 1: GameUi のラダー**: 全体スイッチを GameUi にして、`uitk/uitk-lifeline.cs`・`uitk/stage2/uitk-opening-hud.cs`（無ければ D-21）・`uitk/stage3/uitk-build-bp.cs`・`uitk/stage4/uitk-production-loop.cs`・`uitk/stage4/uitk-research-train.cs` を `run-scenario.sh` で 1 本ずつ（S4-gamma の `tools/uitk/run-all-recordings.sh` があればそれで）。全部 `Success: true`
- [ ] **Step 2: 横断の GameUi**: 触った経路の録画 9 本 — `uitk/uitk-skit.cs`（Enter・T6）・`uitk/uitk-skit-choice-pause-cursor.cs`（指・T9）・`uitk/uitk-recipe-craft.cs`（Esc で選択が外れる・T6。段が無ければ足す）・`uitk/uitk-block-special-panels.cs`（ベルトの ←→・T6）・`uitk/uitk-build-menu.cs`（開いた時の組み直し・T7）・`uitk/uitk-research-tree.cs`（T7）・`uitk/uitk-notification-compare.cs`（T5）・`uitk/uitk-train-inventory.cs`（T2・T15 の State 側）・`uitk/uitk-tutorial-cues.cs`（チュートリアルの枠・札・ワールドピンの出し入れ・T2）。全部 `Success: true`。パスは I-fix5 の 10n（`scenarios/uitk/` 直下を段ごとのサブディレクトリへ移す）の後の実物に T0 Step 3 で読み替えたもの。`playtest-receiver/developer-report-delivery.cs`・`developer-report-interrupt.cs` は `webUiHealth.hostStartup == "started"` を見るので、GameUi の系（Web ホストは `SkippedByEditorSwitch`）では通らない。Step 3 で流す（manifest v7 の GameUi の系の形は T1 の L0 が見る）
- [ ] **Step 3: Web 系**: 全体スイッチを Web にして `misc/ui-state-transition-matrix.cs`・`misc/localization-language-switch-via-ui.cs`・`train/fluid-inventory-via-interact.cs`・`train/train-timetable-via-ui.cs`・`train/train-station-default-name.cs`（T15 の付け替え）と `playtest-receiver/developer-report-delivery.cs`・`developer-report-interrupt.cs`（manifest v7 の版の照合・T1）。T15 のうちホームの積み降ろしモードとレシピ一覧の topic は録画が無いので `Client.Tests/WebUi/` の L0 を根拠にする。CEF が出ない環境なら Editor ログの RendererUnavailable の行を証拠に「未確認」（裁定「UITK 切替まで CEF 由来の不具合は直さない」）
- [ ] **Step 4**: 失敗は原因を直して流し直す。録画は報告ページに載せる分だけ残し、他は消す。`list-work-saves.sh` でワールドを消す

---

### Task 21: moores-code-review で全ブランチレビュー（必須）

必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。base は `origin/feature/uitk`。設計判断は agent 既定で推奨案を採り、判断記録に「agent 既定（moores-code-review Dn）」と書く（ゴール・裁定から外れるものだけ `done.md`「要裁定」）。自動適用・反映がソースの判定経路・条件式・その評価時点に触れたら、Task 20 の録画を反映後のバイナリで流し直してから完了とする（実行計画 §4.3 の 1）。

---

### Task 22: feature/uitk への PR・取り込み・マージ（必須）

セッション終了可能状態にすること: pr-createスキルでPRを作成し、コンフリクトがあれば解消・コンパイル確認のうえpushする。このレーンは実行計画 §4.3 の形（base=feature/uitk、agent がマージ）。

- [ ] **Step 1: PR**: pr-create で `base=feature/uitk`。報告ページ `https://review.moores.tech/pr/<番号>/report/` に並べ比べ（参照・UITK の組）と録画を、`Mac mini の環境ローカル規約（CLAUDE.local の「PR の成果物の載せ先」）が指す publish の README` の manifest の書式で載せ、本文の先頭行をその URL の h1（`# <URL>`）にする。公開に失敗したら報告 URL 無しで完了にしない
- [ ] **Step 2: 取り込み**: `git merge origin/feature/uitk`。共有区画の衝突は両方の行を採る。S5-challenge・S5-gate が先に入っていれば、その画面の直書き（T2）・`IDisposable`（T13）・指カーソルの呼び手（T9）・`Screens/Challenge/CurrentHud/` に同じ置換を当てる（D-20）。`uloop compile`・T2 Step 5 と T18 Step 3 の検査・`uloop run-tests … --filter-type regex --filter-value "^Client\.Tests\.Ui\."`。判定経路に触れた衝突なら Task 20 の該当録画を再走。push
- [ ] **Step 3: G1 とマージ**: PR の `UITK Web follow gate (G1)` が pass・未記帳 0 を見て `gh pr merge <番号> --merge`（Squash 禁止）
- [ ] **Step 4: 片付け**: `moores-wt status` で自分の Editor だと確かめて閉じる。録画・`PlaytestResults/`・TestResults・撮影 PNG を消す。bd を close。worktree の `moores-wt rm` は指揮が行う
- [ ] **Step 5: 報告と合図**: `done.md` の 1 行目を「状態: 完了」（止まったら「状態: 停止（理由）」）にし、証拠・PR URL・割り当ての処遇（R-22 の表）・差の表・未完了・要裁定・integrator への依頼（A-244〜A-248 の本文案・設計書 §1.1／§2.7／§4.2 の改訂案・未使用の番号・段ごとの L3 の欠け）を書いて `../moorestech_logs` でコミット・push。最後に `cmux wait-for -S uitk-s5-crosscut` を 1 回

## Self-Review

設計検査（配置・型閉包）の反映後にやり直した。

1. **Requirements coverage**: R-1→T1、R-2→T1 Step 7-8、R-3→T14・T18、R-4→T2（取り込み後の残り 2 行は T22 Step 2）、R-5→T3、R-6→T4、R-7→T5、R-8→T6、R-9→T7（研究の 3 か所は I-fix6 の結果次第で T7 Step 3）、R-10・R-11→T8、R-12→T9、R-13→T10、R-14→T11、R-15→T12、R-16→T14、R-17→T19、R-18→T13、R-19→T15、R-20→T16・T17（`BugReportFormView` の 3 件は D-24 の条件付き）、R-21→T20（反映後の再走は T21）、R-22→T22 Step 5、R-23→T0 Step 4・T22 Step 5。死活表の 22 行は各行の根拠がどれかのタスクの Step（L1・録画・並べ比べの状態）に載っている（足した検証: T6 Step 1 の CloseUI と矢印の抑止の L0、T10 Step 3 の IconButton の色の L1、T10 Step 4 の面の値の L1、T19 の 2-challenge-hud-multiple-long・3-modal・4-block-machine、T20 Step 3 の列車の Web 録画 2 本）
2. **Placeholder scan**: 実物の名前が起草時に確かめられないもの（`UiTestStyleSheets.TryCompile`・`FakeUiOsCursorApplier` の記録名・`RecipeListScrollView` の内側のフィールド（起草時はローカル変数なのでフィールドを足す）・I-fix6 の型と行）は、探す grep と読み替えの書き先（判断記録の末尾）を書いた。R-4・T2 Step 5 の検査は git grep の ERE で効く `[[:space:]]` に直し、起草時に 76 行を数えて対象を全部 T2 の Files に書いた（以前の `\s` の形は 0 行を返し検査が空振りしていた）。「適切に」「必要なら」で閉じたステップは無い
3. **Type consistency（Produces／Consumes の閉包）**: T1 `GameUiExceptionReading`（抽象・`readonly Status`）／`GameUiExceptionsRecorded(int, IReadOnlyList<GameUiExceptionSample>)`／`GameUiExceptionsUnknown()`／`GameUiExceptionTally.Add`・`Snapshot`・`TryFindFirstUiFrame`・`SampleLimit`／`IBugReportCaptureSources.GameUiExceptions()`・`BugReportCapturedData.GameUiExceptions`／`BugReportManifestReadings.Apply`・`MarkGameUiExceptionsUnknown` は Step 2 のテスト・Step 3 の本体・Step 4 の Writer と確保の口・Interfaces で同じ名前。T6 `IUiKeyPressSource`（`OnPressed`・`WasPressedThisFrame`）・`ISkitAdvanceKeyPressSource`／`ICloseUiKeyPressSource`・`InputKeyPressSource`（抽象）・`SkitAdvanceKeyPressSource()`／`CloseUiKeyPressSource()`・`RecipeSelectionCloseReset(UiRoutingState, ICloseUiKeyPressSource, RecipeItemSelection)` は Step 1 のテスト・Step 2・Step 3 の DI・Step 4 の偽物で一致し、T13（`RecipeSelectionCloseReset` の IDisposable）・T18（Esc の L3 は `CloseUI.SetKeyDownForTest`）が同じ形を前提にする。T5 `UssStyleResolutionGate`（namespace `Client.Ui.Foundation.Theme.StyleResolution`）は T5 Step 3 と T17 の `ToastHostView` で一致。T15 `CraftRecipeUnlockCheck.Check(IGameUnlockStateData, Guid) : UiCommandRejectReason?` は File Structure・T15 の Files・本体・2 つの呼び手で一致し、wire は既存の `UiCommandRejectReasonTraits.ToWireCode` だけが綴る。`UiDisplay.SetShown`/`IsShownInline`（T2→T16 以降）、`UiScrollClipInset.Apply`/`ApplyOuterMargin`/`ApplyInnerPadding`/`ApplyHorizontal`・`RecipeListScrollView.SetClipPadding`（T3）、`GridMeshIndexWriter.WriteQuads`/`QuadIndices`・`Painter2DSquareCap.AddSegment`/`Extend`（T4）は変更なし。既存 API（`UnityLogEntry` の 5 引数 ctor・`InputKey.OnGetKeyDown`/`GetKeyDown`/`SetKeyDownForTest`・`InputSuppressionFilter.FilterKeyPress`〈internal、同じ asmdef から呼ぶ〉・`UiInputExclusivity.ConsumeEscapeThisFrame`/`ClearEscapeLatchForTest`・`UiOsCursorHoverRequest` の 4 引数・`GameUiRootTestFactory.Create`・`UiRoutingStateTestFactory.CreateSwitchable`・`Localize.TrySetChosenLanguage`・`WebUiHealthReading`:33-37 の属性）は実物の file:line で照合した。File Structure に無い新しいファイルは無い（`CraftRecipeUnlockCheck.cs`・`UiText.cs` を載せ、`BugReportFormSender.cs` は「作らない」の行）
4. **保留・縮退経路**: (a) `UssStyleResolutionGate` の預かりは「解決が来る」で解け、来ないまま配置へ進めば LogError（最小構成: 1 件の Push・0 件の Render。配置前は黙り、配置後は必ず出る。T5 Step 1 の 3 件目が両方を見る）。(b) `gameUiExceptions` の unknown は確保時に数え手の値を取れなかったとき（記録を集めない起動）とクラッシュの箱だけで、どちらも `MarkGameUiExceptionsUnknown` を通るので `missing` に必ず行が立ち、件数の欄そのものが無い（T1 Step 2 の 5 件目）。件数はログの環（2000 件）でなく受けた時点の通算なので、環が溢れても 0 件に縮まない（T1 Step 2 の 2 件目）。クラッシュ箱は恒久に unknown で、それを 0 件と読ませない表示（ダッシュボードの unknown）を対で試す。(c) 矢印は入力欄のフォーカス中に `InputSuppressionFilter` が止め、止めたことを `[UiInputProbe]` のログで出す（無音の縮退にならない）。(d) `RecipeSelectionCloseReset` は旧と同じ pull なので、押下と遷移の順序による取りこぼしは新しく生まれない（D-8）
5. **「決定的にする」**: 選択の規則は無い（`gameUiExceptions` の見本は時系列の先頭 5 件で、どれを選んでも件数は同じ）
6. **呼び出し側への責務漏出**: `GameContentPresentation.TryItem` は判定（bool）と値を返し、未知の ID の代替の組み立て（`UiIcon(null, idText, FallbackItemName)`）は通知の口に残る。代替を全画面で共有する DC-12 B を採らなかったため（D-15）。`UssStyleResolutionGate` は判定（`IsResolved`）を返し、預かりと流し直しは宿主に残る。宿主ごとに預かるもの（トースト列・最新の 1 つの VM）が違うため（D-13）。`CraftRecipeUnlockCheck` は理由を返し、wire の綴りは呼び手が既存の `ToWireCode` で引く（Web の `JValue` の検査と `Guid.TryParse` は Web の境界の責務なので Web 側に残す）。`IUiKeyPressSource` と矢印の stream の受け手は抑止を判定しない（`BeltThroughputSectionPresenter` に残るのはベルトが有るか・表示中かだけ）

## 判断記録（ADR）

従う裁定: `.decisions/2026-11-06-*`（UITK 6 本と CEF 停止）。依頼文の指示: Codex を使わない・Mac の Editor 1 本（2 本目以降は Windows）・問いは agent 既定・worktree の片付けは指揮。ランタイムの UI・入力・バグ報告に触れるので unityプレイ録画テスト（T20）を含む。

- D-1（agent 既定。設計書 §7.1 に A-244 を足す。契約の形は指揮の agent 既定（PLAN-W5 設計検査））: `AppErrorBoundary` の代わりは「例外を握らずログ（`LogType.Exception`）へ出し、バグ報告の manifest v7 `gameUiExceptions` で数える」。フォールバック画面と再読み込みのボタンは作らない。棄却: Web と同じフォールバック画面 — ゲームはメインメニューへ戻らない設計で `location.reload` に当たる戻り先が無く、画面を出しても押せる操作が無い。棄却: UI 全体の例外ハンドラ（try-catch で包む）— AGENTS.md の try-catch 禁止の外部境界に当たらず、握ると壊れた購読が黙って残る。契約は判別 union（`GameUiExceptionsRecorded(count, samples)`／`GameUiExceptionsUnknown`）で、判別子 `status` の綴りは前例 `WebUiHealthReading`:33-37 と同じく欄の `StringEnumConverter`＋`CamelCaseNamingStrategy` の属性で決める。棄却: 1 つの型に `Status` と `int? Count` を持つ — 「数えていない」を null と判別子の 2 通りで表し、片方だけ見た読み手が食い違う。UI の例外かの判定は `GameUiExceptionTally` の名前付き定数 `UiNamespaceMarker` と `TryFindFirstUiFrame` の 1 本（見つからないを空文字で表さない）、数えていない箱の書き方は `BugReportManifestReadings.MarkGameUiExceptionsUnknown` の 1 本（不明と欠損を組で書く。バグ報告の箱で確保時に数を取れなかったときとクラッシュの箱が同じ口を通る。前例 `CrashBundleWriter`:57-58 の webUiHealth も不明と `missing` の組）。置き場は前例 `BugReport/WebUiHealth/` に倣い読み取りごとの `BugReport/GameUiExceptions/`、詰める口は `BugReport/` 直下が .cs 10 本なので `BugReport/Readings/`。件数の数え方（指揮の agent 既定（PLAN-W5 評価））: 件数はログを受けた時点で `GameUiExceptionTally`（`IInitializable` で `Application.logMessageReceivedThreaded` を購読。前例 `UnityLogRing` と同じ受け方）がセッション通算として数え、見本は先頭 5 件、確保の瞬間に `IBugReportCaptureSources.GameUiExceptions()` で写す。棄却: 確保した `UnityLogRing.Dump()` から数える — 環は Log・Warning も含めて 2000 件で古い順に捨てる（`UnityLogRing.cs`:31,41-49）ので、序盤の UI の例外が押し出されると `{"status":"recorded","count":0}` になり「0 件」と黙って読める。棄却: 環が溢れた事実を契約に足す — 窓の中の数は報告ごとに意味が変わり、ダッシュボードで比べられない。同じ manifest に兄弟 `webUiHealth`（平らな sealed class。不在は enum の Unknown と `double?` の null）と形の違う判別 union が並ぶ。揃えないのは、webUiHealth の欄は「値ごとに取れない」が独立に起きるのに対し、gameUiExceptions は「数えたか」の 1 点で件数と見本の有無が決まるため（平らにすると `int? Count` の null と判別子で同じことを 2 通りに表す）。版（指揮の agent 既定（PLAN-W5 評価））: manifest の v7 はこのレーンが先に使う（`gameUiExceptions` を足した版。`webUiHealth` は持つ）。X-5a の計画は v7 を「`webUiHealth` を外した版」と定めているので、X-5a は 8 を使う（`done.md`「integrator への依頼」と notes に書く。ADR 0057:76 の「7 へ上げる」も X-5a が 8 と書き直す）
- D-2（agent 既定）: レーン行の依存は S4-gamma だけだが、同じファイルを先に直す I-fix5・I-fix6 のマージを T0 で待つ。棄却: 並走して取り込みで解く — DC-8 A・DC-16 A・DC-28 A・DC-17 A・DC-22 A の上にこのレーンの B/C 側が乗るので、先に入らないと同じ行を 2 回書き換える
- D-3（agent 既定）: 再接続オーバーレイ・辞書エラー・`ConnectingPlaceholder` は作らず、Web の 6 件を disposition の `捨てる` にする（[A-13]・[A-38]・E-17）
- D-4（agent 既定）: 横断の L1 の置き場は `Client.Tests/Ui/Crosscut/`、L3 は `EditModeInPlayingTest/Ui/Crosscut/`。棄却: `Stage5/` — S5-ladder の持ち物と重なる
- D-5（agent 既定。設計書 §7.1 に A-245 を足す。本文はメモ）: 表示の出し入れは `UiDisplay.SetShown`（出すときは Null）の 1 本。USS が既定で none の要素の扱いは T2 Step 2 の表をここへ足す。棄却: Flex 明示に揃え `SetItemShown` だけ例外（DC-11 B）— 状態クラスの none に勝つ症状が残る
- D-6（agent 既定。A-245 に含める）: スクロールのクリップ逃げは `UiScrollClipInset` の 1 式（外 margin = −inset・内 padding = inset。4 辺の `Apply`、外と内の持ち主が違うときの `ApplyOuterMargin`／`ApplyInnerPadding`、左右だけの `ApplyHorizontal`）。T3 Step 1 の表をここへ足す。棄却: USS の共通トークンだけ揃える（DC-19 B）— 実装が残る。形の合わない 2 つの扱い（評価の指摘を実物で再現。`ItemListView.cs`:106-127・`RecipeEntryList.cs`:124-149・Web `ItemListPanel.module.css`:32-55）: `ItemListView` は置き換えない — 左・上・下の 3 辺で右を持たず、margin に実測の基準値を足す Web の別の式（右は `margin-right: 4.435px` でつまみを詰める）で、共有の式に入れると右に −inset の margin と inset の padding が増えて縦のつまみが inset px ずれる。足りない辺を打ち消す口を足す案は棄却（式の後で 2 辺を消すのは「1 つの式」にならない）。`RecipeEntryList` は形に合う口を足して置き換える — margin は自分（4 辺。上だけ Web の Stack の gap を足す）、padding は部品の内側で持ち主が違うので、`ApplyOuterMargin` の後に上を書き直し、内側は部品の `RecipeListScrollView.SetClipPadding`（`ApplyInnerPadding`）が当てる。棄却: `RecipeListScrollView.SetClipInset` で部品に margin も当てる — margin が部品（子）に付き、今の `RecipeEntryList`（親）の margin と並びが変わる
- D-7（agent 既定。gate の置き場は指揮の agent 既定（PLAN-W5 設計検査））: 格子の index と square cap と影の輪の定数は `Foundation/Theme/Mesh/`。Theme 直下は USS・txt を含めて 14 ファイル（.cs 4 本）で、USS もディレクトリの 10 ファイルに数えるので、`UssStyleResolutionGate` も直下に足さず `Foundation/Theme/StyleResolution/` に置く。並びの違う 2 か所は置換しない（T4 Step 1）
- D-8（agent 既定。設計書 §7.1 に A-246 を足す。A-162 を保つこと・駆動の比較・DI の形は指揮の agent 既定（PLAN-W5 設計検査））: 画面が `InputKey` のキーを受ける口は `IUiKeyPressSource`（`OnPressed`・`WasPressedThisFrame()`）1 本と唯一の実装 `InputKeyPressSource` で、抑止と ESC ラッチは `InputKey` の `InputSuppressionFilter` だけが持つ。棄却: 3 通りを ref-uitk-design に書いて残す（DC-10 B）— 抑止の持ち方が 2 通り残る。棄却: 4 つの口を持つ集約型を 1 つ注入 — 使わない口まで見える。
  - 矢印（A-162 を保つ）: ←/→ は `InputSystemArrowKeyStream` が流し、UI の入力マップへ足さない。DC-10 の目的（抑止の持ち方を 1 か所に）は、stream が押下の縁を `InputSuppressionFilter.FilterKeyPress(true, InputSuppressionScope.Keyboard, false)` に 1 回通す形で満たす。`InputSuppressionScope.Keyboard` の抑止は `UiInputExclusivity.IsSuppressed` が `TextInputFocused` の値をそのまま返す（`UiInputExclusivity.cs` の `IsSuppressed`）ので、`BeltThroughputSectionPresenter` が読んでいた `IsTextInputFocused` と同じ値で、押下の配送と同じ呼び出しの中で読む点も同じ。よって受け手の `IsTextInputFocused` は外せる（表示中かの確かめは受け手に残る）。A-162 の棄却理由への答え: (1)「生成物の入力設定（共有の資産）を書き換える」— `.inputactions`・`moorestechInputSettings.cs`・`InputManager` を触らないので当たらない。(2)「←/→ は Player/Move と同じキーで UI マップの抑止の意味が合わない」— UI マップに入れず、通すのは `InputSuppressionScope.Keyboard`（入力欄のフォーカス中に止める）だけで、これは Web が window keydown を入力欄で絞っていたのと同じ意味。A-162 の帰結「抑止の判定が 2 か所に割れ、`InputSuppressionScope` に抑止が足されても ←/→ には効かない」は、この形で閉じる（A-246 で A-162 の「受け手が入力欄のフォーカスを確かめる」の部分だけを改める）
  - `RecipeSelectionCloseReset` の駆動: 受け身の案（旧の pull を保つ。遷移の購読の中で `ICloseUiKeyPressSource.WasPressedThisFrame()`＝`CloseUI.GetKeyDown` を読む）を採る。push の案（`OnPressed` で `Time.frameCount` を覚え、遷移のときに同じフレームかを比べる）と 3 点で比べた: (a) ESC ラッチの評価時点 — 受け身は読んだ時点（遷移の時点）で `IsEscapeConsumedThisFrame` を見る。push は `InputAction.started` の時点（InputSystem の更新）で見るので、同じフレームの後で UI が ESC を使った（`ConsumeEscapeThisFrame`）ときも押下として残り、旧と違う結果になる。(b) `SetKeyDownForTest` の効き方 — `GetKeyDown` にだけ効き `OnGetKeyDown` は流れない（`InputKey.cs`:25,64-75）。受け身なら L3 の Esc の写し（T18）と L0 が `SetKeyDownForTest(true)` で駆動でき、push だと InputSystem へ実キーを注入する形に替える必要がある。(c) 押下と遷移の順序 — 受け身は同じフレームで遷移の購読が走った時点の `GetKeyDown` を読むので順序に依らない（旧と同じ）。push は押下の配送が遷移より前でないと取りこぼす（L1 で固定できず録画頼み）。3 点とも受け身が旧と同じで、push を選ぶ理由が無い
  - DI の形: `WithParameter` は repo に前例 0 件なので使わない。前例のある「キーごとの型を `Register<実装>(Lifetime.Singleton).As<型>()`」（`RecipeGameUiRegistration.cs`:27 の `IUiCloseKeyProbe`・`BlockInventorySpecialGameUiRegistration.cs`:29 の `IHorizontalArrowKeySource`）で書く。キーごとの型 `ISkitAdvanceKeyPressSource`・`ICloseUiKeyPressSource` は `IUiKeyPressSource` を継ぐだけの DI の鍵で、実装 `SkitAdvanceKeyPressSource`・`CloseUiKeyPressSource` は `InputKeyPressSource` の ctor にキーを渡すだけ（`InputKey` を読む・判定する実装は `InputKeyPressSource` 1 つ）。棄却: 受け手をファクトリ登録（前例 `FoundationGameUiRegistration.cs`:24）— `SkitScreenPresenter` の 7 引数を全部 `Resolve` で並べることになり、entry point のファクトリ登録は前例が無い
  - 起草時の照合: `.inputactions` の UI マップの `CloseUI` は `<Keyboard>/escape`（T6 Step 1 の 2 件目の 3 つ目の assert が頼る）
- D-9（agent 既定）: DC-16 は C を採る。`ItemSlotContent` は Web の props（`itemId`・`count?`・`ghost?`・`catalog`・`shortage`）と同じ平らな形で、`ItemSlotPresentation.From` が Web の式の写し。I-fix6 の A で組み立てが `SlotItemContent.From` を通るようになり症状は消える。B（sealed 派生）は Web の `count` の 3 状態と catalog の組み合わせを派生へ割ると型が Web の props から離れ、呼び出し 60 余を書き換えても見た目と挙動は変わらない
- D-10（agent 既定。設計書 §7.1 に A-248 を足す。§1.1 の改訂案はメモ）: 画面の開閉は `UiRoutingState` と `<機能>ScreenVisibility.IsShown`。`UiScreen` に無い状態だけ `UIStateEnum` を読み、その行に理由を 1 行書く。T7 Step 1 の表をここへ足す。棄却: `UiScreenTraits` に画面ごとの真偽を足す（DC-8 C）— Routing に機能の語彙が増える
- D-11（agent 既定。設計書 §7.1 に A-247 を足す）: Web で `cursor: pointer` を持つ共通部品は部品が指を出す（§2.7 の「画面が付ける」を覆す。規則の改訂はこのレーンの計画どおりで、設計書への反映は指揮が docs PR で入れる。指揮の agent 既定（PLAN-W5 設計検査））。無効のボタンで指が出ない差が残れば LX-205
- D-12（agent 既定。A-247 に含める。`SkitChoice` の判定と A-84 の改訂は指揮の agent 既定（PLAN-W5 設計検査））: 画面 USS は部品の内側クラス・`.unity-button` を書かず、部品ルートのつまみだけを上書きする。内側の要素が customStyle で読む値は、部品 USS がルートのつまみから受け直す（§3.2 の 7 と同じ形）。種別は X-audit DC-21 案 A の規則（Web shared/ui の variant と 1 対 1 なら記帳、無ければ画面側の USS へ移す）で判定した: Web の `GamePanelVariant` は `"default"`・`"craft"`・`"skit"`・`"hud"`（`shared/ui/GamePanel/index.tsx`:11）で、`Skit`・`Hud` は 1 対 1 なので残して記帳する。選択肢の板は Web では GamePanel でなく `SkitChoiceList.module.css` の `.choice::before` なので `SkitChoice` に当たる variant が無く、種別を消して画面の USS（`SkitChoices.uss` の `.skit-choice__plate`）へ移す。これは A-84 の「選択肢の板は `GamePanelSurface` の種別 `SkitChoice` で描く」と設計書 §2.2 の `GamePanelSurface` 行の `--skit-choice`・§2.7 の「面は `GamePanelSurface` の種別 `skit-choice` `[A-84]`」を改める（本文案はメモ。A-84 の棄却理由「板の面を Screens で描き直すと面の格子の描き方が 2 本になる」には当たらない: 描くのは `Hud` の骨格の `GamePanelFace` 1 本のまま）。`SkitChoiceButton` の `RemoveFromClassList(Button.ussClassName)` は前例 0 件の手で、理由は T10 Step 4（Web の `.choice` は素の button で既定テーマの `.unity-button` の規則を全部受けない。前例の 2 クラスの打ち消しは Components の USS の手で、画面 USS で書くと DC-21 の違反になる）。新しいつまみ `--icon-button-color`・`--game-panel-surface-*` は Web に同名の変数が無い（Web は `color` と `::before` の CSS）ので、部品名で始まる名前にした
- D-13（agent 既定）: DC-28 は B。預かりと流し直しは宿主に残し、門は解決と診断だけを持つ（Self-Review 6）
- D-14（agent 既定）: DC-31 は A。段ごとの L3 は各ラダーのレーン（S2-ladder・S3-ladder・S4-gamma・S5-ladder）が作った物を数え、このレーンは全画面の載り先の L3 を 1 本足す。棄却: 段ごとの L3 をこのレーンで作る — `Stage<n>/` は持ち主がいる
- D-15（agent 既定）: DC-12 は A（`TryItem`）。棄却: 代替を `Item()` 自体に入れる（B）— 未知の ID で全画面が黙って代わりの絵を出し、開発者が気づく経路（例外）が消える
- D-16（agent 既定。設計書 §4.2 の改訂案はメモ）: ゲーム寿命の型は `IInitializable` だけ（終わらせ方の所有者は D-22）。17 本と `RecipeSelectionCloseReset` は例外なく `IDisposable` を外し、`Dispose` を呼ぶテストの 3 行は消す（T13 Step 2。どのテストも源をテストごとに作り直す）。棄却: テストの後始末だけが使う型に IDisposable を残す分岐 — 実物で当たる型が 0 件で（評価の指摘を再現）、残すと AGENTS.md「デバッグ/テスト専用 public をプロダクションに残さない」に反する形を選ぶ余地だけが残る。`GameUiSrgbCompositeInstaller`・`GameUiLocaleClassBinder` から Dispose を外すのは S5-gate の DI 前の土台と衝突しない: S5-gate の DI 前の土台 `GameUiPreDiFoundation` もこの 2 型の `Dispose` を呼ばない（S5-gate の計画どおり。指揮の agent 既定（PLAN-W5 評価））。T13 Step 1 の表をここへ足す
- D-17（agent 既定）: DC-23 は A。conductor 11-10 04:50 の「据え置き・X-5 で Web ホスト側を消す」をこの決定で覆す。X-5 は付け替え済みの Web ホストを消すだけ。wire（文字列・DTO）は変えない。理由から wire の失敗コードへの写像は既存の正本 `UiCommandRejectReasonTraits.ToWireCode` だけが持ち、Web ホストは switch を書かない（同じ写像が 2 か所になると I-fix5 の改名の追従も 2 か所に要る。指揮の agent 既定（PLAN-W5 設計検査））。T15 Step 1 の対ごとの表をここへ足す。棄却: 予測レビューが出した B（T15 を落とし X-5c で Web ホストごと消す）・C（新しい型の要らない 2 つだけ付け替える）— 指揮の X-audit 決定（11-12。DC-23 は A）
- D-18（agent 既定）: AC-5 は 48 件。除外 3 件（`UiScreenTraits.BuildTraits`・`UiOsCursorRequestStack.Withdraw`・`HoverTooltipHost.OnTrackedDetached`）は T17 の理由。AC-8 とイベントコールバック約 45 件（DC-33・X-5）は触らない
- D-19（agent 既定）: Warning の候補 4 行 — c22-si 表示 helper（拾う・T2）、c22-si 空ラベル（拾う・T16）、c46-cc（拾う・T7）、c63-cc（見送る: 規約はどちらも許し故障シナリオが無い。X-5 の規約の整理へ）
- D-20（agent 既定）: S5-challenge・S5-gate の持つファイルは、相手が先に入っていれば当日の実物へ、後なら取り込み（T22 Step 2）で同じ置換を当てる。相手の計画が先に `UiDisplay`・`IUiKeyPressSource`・3 部品の新しい ctor を使えるよう、A-245〜A-248 の本文を指揮に先に渡す。
  - S5-gate の DI 前の土台も Dispose を持たない（S5-gate の計画どおり）: `GameUiPreDiFoundation` は `GameUiSrgbCompositeInstaller`・`GameUiLocaleClassBinder` の `Dispose` を呼ばず、T13 がこの 2 型から `IDisposable` を外すのと衝突しない。どちらが先にマージしてもコンパイルが通ることを T0 Step 3 で照合する（指揮の agent 既定（PLAN-W5 評価））
  - L3 `GameUiScreenMountTest` の一覧（T14 Step 2）: S5-challenge の `ChallengeListView` は、T14 の時点で在れば一覧へ入れ、無ければ S5-challenge が自分の PR で一覧に足す（S5-challenge の計画に指示済み）。S5-ladder D-6 が段 5 の載り先をこの L3 に頼るので、どちらの経路でも一覧から落ちない（指揮の agent 既定（PLAN-W5 評価））
- D-21（agent 既定）: `stage2/uitk-opening-hud.cs` が最終 HEAD に無ければ、ラダー 2 を除いて録画を締め、`done.md`「要裁定」に 1 行（S3-ladder D-3 と同じ扱い）
- D-22（指揮の agent 既定（PLAN-W5 設計検査））: ゲーム寿命の型の終わらせ方（`IDisposable` を外した後の所有者）は考えない。根拠は AGENTS.md の既知の制約（ゲームは起動後メインメニューへ戻らず、ゲーム寿命オブジェクトの dispose 漏れは考慮不要）。死活表の「Presenter の破棄」行と D-16 の扱いのまま
- D-23（指揮の agent 既定（PLAN-W5 設計検査））: DC-30 の CI は 2 か所に分ける。依存の無い `node --test tools/uitk/webTokens/tests/*.test.mjs` は前例（`uitk-web-follow-gate.yml`:52-53 の testNameParity の自己テスト）どおり `uitk-web-follow-gate-selftest` の job に並べ、master の PR でも走らせる（道具の破損を master で止める）。`--check` の 2 本は `convert-mantine-tokens.mjs` が webui の `node_modules` を読むので `run_test.yml` の webui-test に置き、G1 と同じ `if: github.base_ref == 'feature/uitk' || github.head_ref == 'feature/uitk'`（同 yml :20-22）を付ける。理由も G1 と同じで、master で Web の CSS を変える作業者に UITK のトークンの追従を求めない（切替の PR で一度に揃う）
- D-24（指揮の agent 既定（PLAN-W5 設計検査））: `BugReportFormSender` は作らない。X-audit DC-22（バグ報告フォームの文字と送信の持ち主）は I-fix6 の持ち物で、I-fix6 は案 A（文字を `UiText` で返す）を採り、送信を View の外へ出す案 B を採っていない。行数のために送信の手順を別の型へ出すのは案 B と同じ決定を別レーンで下すことになり、前例（Presenter へ出す `TrainPlatformBodyPresenter`）とも形が違う。AC-5 の `BugReportFormView` の 3 件は I-fix6 の取り込み後の実物で 200 行未満に収まるときだけ移し、収まらなければ落として X-5 へ申し送る（T17 Step 2）。同じファイル（`BugReportFormView.cs`・`BugReportFormText.cs`・`BugReportRecoveryControlsView.cs`）は I-fix6 の取り込み後にだけ触る（T0 Step 3）
- D-25（指揮の agent 既定（PLAN-W5 設計検査））: DC-11 案 A の「直書きを置換」の範囲に `Foundation/Layers/UiLayerStack.cs`:70,77（A-80 の層の全表示切替）と `Screens/CursorTooltip/CursorTooltipView.cs`:34,49 を入れる。どちらも「出すときはインラインを外す／Flex を当てる」の同じ規則の別実装で、残すと R-4 の検査が通らない。`UiLayerStack` の「隠す前に入力欄のフォーカスを外す」は層の責務なので `UiDisplay` へ移さず呼び手に残す

- D-26（指揮の agent 既定（PLAN-W5 評価））: T13 で `GameUiSrgbCompositeInstaller`・`GameUiLocaleClassBinder` から `IDisposable` を外すのは保つ。S5-gate の DI 前の土台 `GameUiPreDiFoundation` も 2 型の `Dispose` を呼ばない（S5-gate の計画どおり。D-20）。どちらが先でもコンパイルが通ることを T0 Step 3 の照合項目にした
- D-27（指揮の agent 既定（PLAN-W5 評価））: `gameUiExceptions` の件数はログを受けた時点でセッション通算として数え、見本は先頭 5 件のまま（`GameUiExceptionTally`。D-1）。L0 に「環が溢れた後でも序盤の例外が数に残る」1 件（T1 Step 2 の 2 件目）
- D-28（指揮の agent 既定（PLAN-W5 評価））: Warning のうち D-16 の当たらない分岐は消して 17 本とも外す形に確定（D-16）、`SKILL.md`:127 が A-248 と食い違う件はスキル本文を直すステップを足す（T7 Step 4b。部品カタログは設計書から再生成されるので触らない）。クラッシュの箱の `missing` は前例を実物で確かめた結果、前例も不明と `missing` の組なので形を変えない（「評価の指摘の処遇」）
- D-29（指揮の agent 既定（PLAN-W5 評価））: manifest の v7 はこのレーンが先に使う。X-5a の計画の manifest の版を 8 へ上げることを integrator へ依頼する（D-1・notes）。ADR 0057 の契約の段落と「8 へ上げる」の書き直しは前例（`9d0c8e1cb4`）どおりこのレーンの T1 Step 4b で直す（X-5a に回さない。再検査 V-1）。録画シナリオの版の照合 2 本を T1 の grep の範囲と Files へ入れた
- D-30（指揮の agent 既定（PLAN-W5 評価））: T18 の「存在しない言語コードは拒んで前の言語のまま」の写し先は、X-5c で消える Web ホストの契約テストでなく UITK 側の `Client.Tests/Localization/Resolution/LocalizeTest.cs`
- D-31（指揮の agent 既定（PLAN-W5 評価））: T14 の `GameUiScreenMountTest` は型の有無だけでなく、載り先の層（`UiLayer`・`UiFamily`）と Render が届いた形（ホットバーの枠の数が 0 でない・持ち物を開くと枠が 0 でない）まで見る。S5-challenge の `ChallengeListView` は T14 の時点で在れば一覧へ入れ、無ければ S5-challenge が足す（D-20）
- D-32（指揮の agent 既定（PLAN-W5 評価））: T3 の共有の式に合わない 2 つは実物で確かめて、`ItemListView` は置き換えから外し、`RecipeEntryList` は形に合う口（`ApplyOuterMargin`／`ApplyInnerPadding`・`RecipeListScrollView.SetClipPadding`）を足して置き換える（理由は D-6）
- D-33（指揮の agent 既定（PLAN-W5 評価））: DC-23 は指揮の 11-12 の X-audit 決定どおり A のまま。予測レビューの B・C は D-17 に「棄却: 指揮の X-audit 決定」と 1 行だけ書いた
- D-34（指揮の agent 既定（PLAN-W5 評価））: 軽い指摘（disposition のバッククォート・`CrashBundleWebUiHealthTest`:25・`BuildMenuShortageTest`:15-16・1 ディレクトリ 10 ファイル・I-fix5 の 10n の録画パスの移動・recipe の参照キャプチャ 3 枚のどれと比べるか・200 行の確認・パスのずれ）は再現できたものを直した（「評価の指摘の処遇」）
- D-35（agent 既定）: `TryFindFirstUiFrame` は行頭（呼ばれたメソッドの型）が `Client.Ui.` の行だけを UI のフレームにする。棄却: 行のどこかに `Client.Ui.` を含めば数える — Unity のスタックの行は `Type:Method (args)` の形で、UI の型を引数に取るだけの UI でないフレームまで数える（設計検査の再検査の備考）。2 件目の L1（UniRx の購読の中の例外）は 1 件目と同じ UITK の配送から投げる形で、配送元が UITK でない経路（パケット受信・モデル更新から UI の購読が走る）の例外がログへ出るかは呼び手（PlayerLoop・UniTask の未観測例外の口）に依る。このレーンの L1 では固定せず、T19 Step 4 で台帳「## 実測の結論」へ T1 の実測と並べて「UITK 以外の配送元は未固定」と 1 行書く

読み替え（T0 Step 3 で足す）:

評価の指摘の処遇（独立評価 2 体・予測レビュー・設計検査の再検査。再現したものは計画へ反映済み、ここには再現しなかったものと、反映の場所だけを 1 行ずつ）:

- 反映（再現あり）: S5-gate の `GameUiPreDiFoundation` との Dispose の衝突（評価 1 重・評価 2 中）→ D-16・D-20・D-26・T0 Step 3／T6 の CloseUI の L0 にキーボードが無く ESC ラッチが効かない（`InputKey.cs`:56-62）→ T6 Step 1／`ItemListView`・`RecipeEntryList` が共有の式に合わない（`ItemListView.cs`:106-127・`RecipeEntryList.cs`:124-149・Web `ItemListPanel.module.css`:32-55）→ T3・D-6・D-32／DC-31 の L3 が層も Render も見ない → T14 Step 2・R-16・D-31／`ChallengeListView` が L3 の一覧に無い → D-20・T0 Step 3・T14／manifest v7 が X-5a と衝突（X-5a 計画 :5,27,123）→ D-1・D-29・notes／T18 の写し先が X-5c で消える（X-5c 計画 :201）→ T18・D-30／`gameUiExceptions` の件数が 2000 件の環から数えられ溢れると 0 件（`UnityLogRing.cs`:31,41-49）→ T1・D-1・D-27／D-16 の当たらない分岐（テストはどれも源を作り直し、静的な源の型を作るテストは 0 件）→ T13 Step 2・D-16／`SKILL.md`:127 が A-248 と食い違う → T7 Step 4b／録画シナリオ 2 本の `schemaVersion == 6` と、GameUi の系で `webUiHealth.hostStartup` が `skippedByEditorSwitch` になり delivery が通らない（`WebUiStartup.cs`:19-27）→ T1 Step 4・T20 Step 2-3・R-21
- 反映（軽・再現あり）: disposition の列のバッククォートと allScreensI18n のパス（`disposition.mjs`:27,34）→ T1 Step 7／`CrashBundleWebUiHealthTest.cs`:25 の版 → T1 Files・Step 4／`RemoteExecBundleMarkTest` の版の行は :70 → T1／`BuildMenuShortageTest.cs`:15-16 の 4 引数 → T11／`Client.Tests/Ui/Components/Controls/` が既に 10 ファイル → T9 の置き場を `PointerCursor/` へ／I-fix5 の 10n の録画の置き場の移動（I-fix5 計画 :213）→ T0 Step 3・T20／recipe の参照 3 枚のどれと比べるか → T19 は `4-recipe-selected-craft`／T2 が出し方を変える HUD・チュートリアルの 7 状態と `uitk-tutorial-cues.cs` → T19・T20 Step 2・R-17／`BugReportFormView` の 200 行（今 194 行。推測の指摘）→ T9 Step 3 は ctor の引数だけにして `wc -l` を見る／パスのずれ（`ResearchCalcKnobs.cs` は `Screens/Research/Parts/`、`ResearchNodeStateStore` は :21,31,50-54、`Client.Tests/Ui/Foundation/` と `UiGapContainer` の L1 は無い）→ T2〜T5・T7／`RecipeListScrollView` の内側の `GameScrollView` がローカル変数 → T3 でフィールドにする／再検査 N1（同じ manifest に形の違う 2 つの読み取り）→ D-1 に理由／N4（パース失敗の `"invalid_recipe"` が文字列のまま）→ T15 Step 2／備考の `IconButtonTest` が T10 の Files に無い → T10／備考の `TryFindFirstUiFrame` の引数の型の誤検出 → T1 Step 3・D-35／予測レビュー W の 2 件目の L1 が UITK の配送経路だけを見る → D-35（L1 は変えず台帳に範囲を書く）
- 再現しなかった: クラッシュの箱で `missing` を足すのは前例と形が違う（予測レビュー W）／前例 `CrashBundleWriter.cs`:57-58 は `WebUiHealthReading.UnknownForPreviousSession()` の直後に `AddMissing("webUiHealth", …)` を書き、`CrashBundleWebUiHealthTest.cs` も `missing` の webUiHealth の行を見ている。計画の「不明と `missing` の組」は前例と同じ形なので変えない
- 再現しなかった: 再検査の注目点 N2（`Readings/` の名前）・N3（空のキーごとの型の DI の鍵は前例 0 件）・N5（`RemoveFromClassList(Button.ussClassName)` は前例 0 件）・N6（`Hud` 種別の受け手が 2 つ）・N7（A-162・A-84 の改訂を agent 既定で入れる）・N8（`UiLayerStack` が `UiDisplay` に委ねる）／どれも違反ではなく故障シナリオを組めない（N3・N5 は計画が新しい形だと明記済み、N6 は画面 USS が 4 値を全部上書きする前提を T10 Step 4 の L1 が見る、N7 は notes の A 行の改め案で指揮が設計書へ入れる）
- 再現しなかった: 再検査の備考「新しい CI の step が `pnpm_binary` の有無を確かめない」／前の step が通っていれば実害は無く、故障シナリオを組めない

## 設計書・実行計画へ反映する行

- 設計書 §7.1: A-244（D-1）・A-245（D-5・D-6・D-25）・A-246（D-8。A-162 の「受け手が入力欄のフォーカスを確かめる」の部分を改める）・A-247（D-11・D-12。A-84 の「選択肢の板は `GamePanelSurface` の種別 `SkitChoice` で描く」の部分を改める）・A-248（D-10・D-16 の型）。本文の案は scratchpad の `notes-s5-crosscut.md`
- 設計書 §7.1 の既存行の改め: A-162（D-8）・A-84（D-12）の改め方の本文案は同メモ
- 設計書 §1.1（D-10）・§2.2 の `GamePanelSurface` 行（D-12。`--skit-choice` を消す）・§2.7 の OS カーソルの行（D-11）と「1 feature だけが使うもの」の選択肢の板の文（D-12）・§3.2 の 2（D-12）・§4.2 の型（D-16）・部品カタログの ItemSlot の行（D-9）の改訂案は同メモ
- 実行計画 §6.1 の表に S5-crosscut の行（同メモ）

## 本PR外のリファクタ提案

計画のタスクにはしない（設計検査 Phase 2.6 の第3バケツ。B-1 は T2 に取り込んだので載せない）。

| # | 既存側のファイル | 新設側 | 同じ役割と判断した根拠 |
|---|---|---|---|
| B-2 | `S/Client.Ui/Screens/Recipe/Parts/ItemHeaderRule.cs`:62-72・`S/Client.Ui/Screens/CommonHud/CrosshairShadowRing.cs`:84-95 | `GridMeshIndexWriter.WriteQuads`／`QuadIndices`（T4） | どれも格子・輪の四角を三角 2 枚で書く。頂点の並べ方だけが違うので T4 では置換しない（並べ方を引数にした 1 本にするか別のまま置くかを決める提案） |
| B-3 | `S/Client.Ui.State/Notification/Text/LocalizeNotificationTextLookup.cs`:27（`ItemDisplayName` が `MasterHolder.ItemMaster.ExistItemId` で判定し代わりの名前と LogError） | `GameContentPresentation.TryItem`（T8） | 通知の名前の側とアイコンの側が「マスタに無い ItemId」を別々に判定している（State は `Client.Ui` の型を参照できないので、1 本にするなら置き場も一緒に決める） |
| B-4 | `S/Client.Ui.State/BlockInventory/Machine/Selection/MachineRecipeSelectCommand.cs`:37-38・`S/Client.WebUiHost/Game/Actions/MachineRecipeSelectionActions.cs`:44 | `CraftRecipeUnlockCheck.Check`（T15） | 機械のレシピで「解放表に無い→Invalid・未解放→Locked」の同じ 2 段の検査を State と Web ホストが別々に書いている（クラフトと機械で 1 本にするか、種類ごとに 1 本ずつにするか） |

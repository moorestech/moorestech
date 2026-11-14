# UITK S3-ladder（段階 3 の締め: ラダー `uitk-build-bp`・パネル越しの D&D・検索欄の入力の受入・wwxy の 4 本・申し送り・束 3 の突き合わせ・並べ比べの独立評価）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** S3 の 2 レーン（S3-buildmenu #1645・S3-hotbar-modal #1647）が `feature/uitk` に入った後、全体スイッチ UITK（GameUi）で「B → 検索 → エントリをホットバーへドラッグ → 選んで設置 → BP をコピーして名前を付ける」をラダー `uitk-build-bp` として固定し、ビルドメニューのパネル越しの D&D と検索欄の入力の受入を L1 で押さえ、wwxy の束 3 の 4 本と申し送りの録画 3 本を直し、束 3 を暗転にして突き合わせ差分 0・G1 未記帳 0・並べ比べの独立評価を残して `feature/uitk` へ取り込む。

**Architecture:** integrator のレーン（`int`。Mac の Editor 1 本）。新しく書くのは主に「検査する側」（L1 テスト 3 本・録画シナリオ 2 本・参照の撮り直し）で、本番コードは検査で実測した不具合（電柱のホイールの換算など）と並べ比べで再現した差だけを直す。L1 は既存の土台 `BuildMenuScreenTestHost`・`UiLayerStack`（本番の層と族）・`UIStateTestFixtureBase`＋`InputKey.SetKeyDownForTest`（前例 `UiInputExclusivityEscapeLatchTest`）に乗る。録画は前例 `uitk-hotbar-and-bp-modal.cs` の手順を写し、新しいシナリオは `scenarios/uitk/stage3/` に置く（前例 S4-gamma D-24 の `stage4/`）。並べ比べは前例 `uitk-notification-compare.cs` の 2 本立て（色・寸法は EditMode の `UiRuntimePanelCapture` に Web の fixture 相当の状態と参照の地の色、PlayMode は崩れの確認だけ）。

**Tech Stack:** Unity 6 / UI Toolkit（`Client.Ui`・`Client.Ui.State`）、NUnit（`ScreenTestHost` の panel simulate・`InputTestFixture`）、プレイテスト DSL（`Client.Playtest`・`.agents/skills/unity-playmode-recorded-playtest`）、Node（`tools/uitk/test-name-parity.mjs`・`moorestech_web/webui/e2e/uitk-reference/capture.ts`）、Python（`.github/scripts/check-uitk-web-follow.py`）、bd。

設計の出所: 実行計画 `docs/uitk-migration/execution/execution-plan.md` §1.4 の S3-ladder 行（完了条件の原文:「ラダー `uitk-build-bp` を作り 1〜3 が緑。ビルドメニュー → ホットバーの D&D（L1。`AssignHotbar(1,id)` が 1 回だけ、`SelectBuildMenu` は 0 回）。wwxy の BP 2 本・電線 1 本・視点切替 1 本を直す。検索欄の入力中にゲームのキーが動かない・ESC はフォーカスを外すだけ（L1）。束 3 の突き合わせ差分 0・G1 0・codex-audit」）・§4.3・§5・§6.2、統合計画 `docs/uitk-migration/plan.md` §1（段階 3）・§2.0・§4.4（ラダー 3）、設計書 `docs/uitk-migration/execution/component-architecture.md` §3.3・§3.4、`.agents/skills/ref-uitk-design/references/compare-procedure.md`、台帳 `docs/uitk/TODO.md`「## 壊れた録画の割り振り」。申し送り: `../moorestech_logs/harness/uitk/S3-buildmenu/done.md`・`S3-hotbar-modal/done.md`。

## Requirements

| # | 要件 | 受け入れ基準 |
|---|---|---|
| R-1 | パネル越しの D&D（L1） | `BuildMenuToHotbarDragTest` が、`UiLayerStack.Build(rootVisualElement)` の本番の層と族（メニューは `UiLayer.Screen`/`UiFamily.Stage`、ホットバーは `UiLayer.Chrome`/`UiFamily.Viewport`）に本物の `BuildMenuView` と `HotbarHudView` を載せ、エントリ `build-menu-entry-block-<WoodChest>` を 5px を超えて動かして枠 1 に落とすと、ホットバーの記録が `assign:1:<WoodChest>` の 1 件だけ・ビルドメニューの `Selected`（＝`SelectBuildMenu`）0 件を PASS。対照として閾値未満で離すと `Selected` 1 件・ホットバーの記録 0 件も PASS |
| R-2 | 検索欄の入力の受入（L1・画面側） | `BuildMenuSearchInputGuardTest` が、`GameUiInputRules` を付けたビルドメニューで検索欄にフォーカスすると `IsTextInputFocused`・`IsSuppressed(InputSuppressionScope.Keyboard)` が真、ESC の KeyDown でフォーカスが外れ `IsEscapeConsumedThisFrame` が真・画面は出たまま・検索語は残る、を PASS |
| R-3 | 検索欄の入力の受入（L1・UIState 側） | `BuildMenuStateTextInputGuardTest` が、系を GameUi にした上で、入力中の旗が立っている間の B で `GetNextUpdate()` が null、旗が無い B で GameScreen、ESC（`CloseUI.SetKeyDownForTest(true)`）はラッチ無しで GameScreen・`ConsumeEscapeThisFrame()` の後は null、を PASS（対照と本番の条件を同じテストで並べる） |
| R-4 | ラダー 3 `uitk-build-bp` | `scenarios/uitk/stage3/uitk-build-bp.cs` が全体スイッチ GameUi・Web ホスト無しで、B → 検索欄をクリックして W 0.6 秒で動かない → ESC でフォーカスだけ外れメニューは開いたまま → 同じ W で動く（対照）→ 検索語を `SetText` → 石窯のエントリを枠 1 へドラッグ（前は空・後は石窯の Guid・他の枠は不変・UIState は BuildMenu のまま）→ B で閉じる → ワープ → 数字キー 2 で設置 → BP コピーで範囲 → 名前を `SetText` → 確定 → BP 登録、を `Success: true`・ErrorLogs 0・Note に系=GameUi |
| R-5 | ラダー 1〜3 が UITK で緑 | 最終 HEAD の GameUi で `uitk-lifeline`・`uitk-build-bp` が `Success: true`。`uitk-opening-hud`（S2-ladder の持ち物）は最終 HEAD にあれば流して `Success: true`。無ければ `done.md`「要裁定」に「ラダー 2 が未作成（S2-ladder 未着手）のまま S3-ladder を 1・3 で締めてよいか」を書き、マージは進める（D-3） |
| R-6 | wwxy の BP 2 本 | `building/blueprint-copy-paste-via-ui.cs`（S3-hotbar-modal が書き直し済み）と `building/blueprint-right-click-delete-via-ui.cs`（このレーンで書き直す）が GameUi と Web で `Success: true`（Web は CEF が出る環境のときだけ。出なければ Editor ログの RendererUnavailable を証拠に「未確認」） |
| R-7 | wwxy の電線 1 本 | `connect/electric-wire-connect-via-ui.cs` を削除。後継 `connect/electric-wire-tool-extend-chain-via-ui.cs` が GameUi で `Success: true`。旧だけが見ていた検証（結線前は別セグメント・セグメント統合・電力範囲ボックスの表示）を `done.md`「integrator への依頼」に 1 行（後継へ移すかは X-prerun 側の判断） |
| R-8 | wwxy の視点切替 1 本 | `misc/fps-tps-view-toggle-via-ui.cs` を `CrosshairVisibility.IsVisible()`（両系）と UITK の `crosshair` 要素の照会（GameUi だけ。Web の Crosshair には testid が無い）へ移植し、GameUi・Web で `Success: true` |
| R-9 | 申し送りの録画 3 本 | `building/blueprint-place-like-block.cs`・`connect/electric-wire-tool-pole-cycle-rotate-via-ui.cs`・`connect/electric-wire-tool-pole-ghost-visibility-check.cs` が GameUi で `Success: true`。束 4 の持ち物（装備のホイール）が原因の Assert は持ち主へ bd で回し、`done.md`「未完了」へ証拠付きで書く |
| R-10 | 束 3 の暗転と G1 | 台帳の束 3 が `暗転`・起点 `0e81bbe3c3`、`check-uitk-web-follow.py --base origin/master --head HEAD` で「束 3: 未記帳 0 件」。取り込み後も 0 件。PR の G1 job が pass |
| R-11 | 束 3 の突き合わせ差分 0 | `node tools/uitk/test-name-parity.mjs --bundle 3` が exit 0（起草時: Web 99 / 写した 99 / 未対応 0）。`docs/uitk/disposition.md`「## 突き合わせの記録」に 1 行 |
| R-12 | 参照の撮り直し | origin/master の worktree の webui で束 3 の参照を 1280×720 と 2560×1080 で撮り直し（`capture.ts`）、台帳「## 参照キャプチャ」の撮影元を書き換える。S3-hotbar-modal の `web-8a585f742a-1920x1080` も台帳に書く |
| R-13 | 並べ比べの独立評価 | 束 3 の 3 状態（`3-build-menu`・`3-hotbar-equipment-empty`・`3-modal`）を、組 1＝EditMode の `UiRuntimePanelCapture`（Web fixture 相当の状態・参照の地の 1 色・1280×720 と 2560×1080）、組 2＝PlayMode（崩れの確認だけ）で撮り、R-12 の参照と並べて opus の評価役 2 体に別々に判定させる。指摘は画素・寸法の実測で再現したものだけ残し、直すか `LX-170〜179` に記帳。`done.md`「差の表」に全行の処遇 |
| R-14 | 申し送りの処遇 | 「## 申し送りの処遇」の表の全行に「やった（Task n・証拠）／回した（bd id）／やらない（理由）」が付き、`done.md` に写す |
| R-15 | 台帳の割り振りの更新 | 「## 壊れた録画の割り振り」の S3-ladder の 4 行の方針の列に結果を書く（`X-prerun` の語を書かない。S4-gamma の一覧作りが行全体を grep するため） |
| R-16 | 締め | moores-code-review → pr-create（base=feature/uitk、報告ページ）→ origin/feature/uitk 取り込み → compile・テスト → G1 緑 → `gh pr merge --merge` → Editor を閉じる → `done.md`・合図 |

### やらないこと
- ラダー 2 `uitk-opening-hud` と wwxy の束 2 の 4 本（S2-ladder の持ち物）
- 装備のホイール（`EquipmentWheelInput`・`EquipmentWheelGate`。束 4・S4-inventory の持ち物）の修正。原因の証拠を取って bd で回す
- 画面・部品の新しい機能。設計書・実行計画の本文の改訂（`done.md`「integrator への依頼」に案を書く。台帳の自分の行は書く）
- 既存の `scenarios/uitk/` 直下 26 本・`Client.Tests/UIState/` 直下 11 本の分割（新しいファイルは足さない。既存の分割は I-fix の範囲として依頼に書く）
- 過去の計画書・研究文書に残る旧シナリオ名の書き換え
- Codex（ユーザー指示「一旦 Codex セッションは使わない」）。§5 の codex-audit は opus の subagent 2 体で代える

## Global Constraints

- AGENTS.md 全部（1 ファイル 200 行未満・1 ディレクトリ 10 ファイル・partial と `Func<>` の禁止・try-catch は外部境界だけ・UniRx・2 行コメント〈日本語 1 行→英語 1 行〉・`#region Internal` はメソッド内のローカル関数だけ・デフォルト引数禁止・.meta を手で作らない・Prefab/シーンを手で編集しない）
- Mac mini で同時に動かす Unity Editor は全体で 1 本（このレーンの worktree の 1 本。本体が起動済み）。subagent は Editor を起動しない。並列が要るときは Windows の `~/bin/winunity`（push 済みブランチ）
- 録画シナリオは PlayMode を止めた状態で `uloop execute-dynamic-code --project-path ./moorestech_client --code 'Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi);'`（Web で流すときは `UiSystem.Web`）を実行してから `run-scenario.sh` を呼ぶ。`uitk-*` は冒頭で `p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, …)`。`run-scenario.sh` が初回 "already playing" で終わったら 1 回だけ投入し直す（bd moorestech-trfka）。Editor が背面だと Keyboard/Mouse が無効になるので、シナリオは前例どおり `IgnoreFocus` と `EnableDevice` を入れる
- 録画 mp4・`PlaytestResults/`・TestResults xml・撮影 PNG は検証後に消す（報告ページへ載せる分は先に載せる）。`.superpowers/` はコミットしない
- 番号: 設計書の行は A-210〜A-219、見た目の例外は LX-170〜LX-179 だけ
- 共有ファイル（`UiCommandRejectReason.cs`・`localization.csv`・`Registration/GameUi`・`MainGameUiRegistration`・tss・asmdef・台帳）は自分の行だけ足す。取り込みの衝突は両方の行を採る（S2-skit が並走中）。`UiInputExclusivity.cs`（198 行）・`DerivedTokens.uss`（195 行）へ足すなら先に分ける
- 区切り（タスク）ごとにコミットする。subagent はバックグラウンドのポーリングで待たず前景で実行する

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反6件・修正6件 / L1 の置き場を `Screens/BuildMenu/`・`UIState/BuildMenu/` へ、新シナリオを `scenarios/uitk/stage3/` へ、電柱のホイールは読み方を変えず換算だけ直す、装備のホイールは束 4 へ回す、死活表に 2 行
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱0・第3バケツ0 / 発火なし（本番の新しい型が無い）

## 死活表（Phase 2.5）

| 操作 | 計画後も生きるか | 根拠 |
|---|---|---|
| ビルドメニューの検索・選択・D&D・BP 削除（GameUi/Web） | 生きる | 本番コードは触らない（L1 と録画を足すだけ）。R-1〜R-4・R-6 で確かめる |
| ビルドメニューの B・ESC・Tab・右短押しで閉じる・選んで建てる | 生きる | `BuildMenuState` は触らない見込み（`HybridInput.GetKeyDown` は既に抑止とラッチを通る。HybridInput.cs:48-55）。R-3 が落ちて直すことになったら `RightShortPressTransitionTest` と R-3 で確かめる |
| ホットバーの数字キー・クリック・ドラッグ | 生きる | 触らない。R-4 で数字キーとドラッグを通す（ホットバーにホイールの読み手は無い） |
| 電柱の種類のホイール切替（マウス） | 生きる（直る） | Task 7 で換算だけ直す（`ManualUpdate` から読む駆動は変えない） |
| 電柱の種類のホイール切替（トラックパッドの細かい量） | 生きる（感度が変わる） | `ScrollStepAccumulator` の蓄積は残すが、`/100f` を外すと 1 段に要る量が 100 分の 1 になる。Uniform の設定では 1 ノッチ＝1.0 で、1 ノッチ＝1 段に戻る。実測して判断記録 D-6 に書く |
| 装備のホイール選択 | 生きる | 触らない（束 4 へ回す） |
| 視点切替（FPS/TPS）とクロスヘア | 生きる | シナリオだけ移植 |
| Web 側の録画 | 生きる | 同じシナリオを Web でも流し、段階の前と同じ合否を見る |

## 申し送りの処遇

| 出所 | 項目 | 処遇 |
|---|---|---|
| S3-hotbar-modal 部分移植の残り・integrator への依頼 | hotbar.spec のビルドメニューのパネル越しの D&D の通し | やる（Task 3・Task 5） |
| S3-buildmenu integrator への依頼 | `building/blueprint-place-like-block` の通しの再走 | やる（Task 7） |
| S3-hotbar-modal 未確認 | 同シナリオの準備の電線「電柱→石窯 ConnectionLimit」 | やる（Task 7。燃料機械は電線口 0 が仕様〈`WirelessFuelMachineWireTest`「電柱から燃料機械へ手動でつなげない」〉なので、シナリオを電気機械へ替える） |
| S3-buildmenu 証拠（レーン外の FAIL） | pole-cycle-rotate（ホイールの `/100f`・bd moorestech-9a7t9・削除済みラベル） | やる（Task 7。装備の対照は束 4 へ回す） |
| 同上 | pole-ghost-visibility（削除済みラベルの古い Assert） | やる（Task 7） |
| S3-buildmenu integrator への依頼・D-59 | 1280×720 の参照の撮り直し | やる（Task 8） |
| S3-hotbar-modal integrator への依頼 | 1920×1080 の参照 `web-8a585f742a-1920x1080` を台帳へ | やる（Task 8） |
| S3-buildmenu integrator への依頼 | 設計書 A-57 に D2（D-63）の例外を追記 | 回す（設計書の改訂。`done.md`「integrator への依頼」に案を転記） |
| S3-buildmenu integrator への依頼 | `UiRuntimePanelCapture` に幕と地の画像を入れる口（D-61） | Task 9 で要れば D-61 の回避（`UiLayerStack` の Backdrop へ地を載せる）を使い、口の追加は I-fix へ回す |
| S3-buildmenu integrator への依頼 | GamePanel の題の影（LX-72・albof.39） | Task 9 の並べ比べで再現したら処遇。再現しなければ bd albof.39 のまま回す |
| S3-buildmenu integrator への依頼 | drift.md の所持/必要ラベルの引き方の訂正 | やらない（drift.md は消えた worktree の `.superpowers/` 内で repo に無い。正しい引き方は done.md に既に書かれている） |
| S3-buildmenu 未確認 | bug-pass の Warning W2（検索で可視カテゴリが変わったときの tween）・W3（スペーサ未反映の間は位置を保存しない） | 回す（bd を `--parent moorestech-albof.57` で 1 件ずつ立てる。挙動の変更で grill が要る） |
| S3-buildmenu 未確認 | `BuildMenuModel` 自体のテストが無い・車両の 2 つ目の絵が白い | 回す（bd。白い絵は Task 9 の組 2 で見えたら note） |
| S3-hotbar-modal integrator への依頼 | `filter: opacity` の倍率 1.5 のぼけ・LX-95 への展開・css-to-uss.md の opacity の行・A-108 の改め | 回す（`done.md` へ転記。LX-76 の 2560×1080 は Task 9 の組 1 で撮るので観測を書き添える） |
| S3-hotbar-modal integrator への依頼 | `DerivedTokens.uss` 195 行の分け方 | Task 9 で USS を足すときだけ先に分ける。足さなければ回す |
| S3-hotbar-modal integrator への依頼 | フォーカスを持ったまま入力欄入りの View を外すと旗が残る（モーダル以外） | やる（調べるだけ。Task 4 の L1 で、ビルドメニューを検索欄にフォーカスしたまま閉じたとき旗が落ちるかを 1 件確かめる。落ちなければ bd） |
| S3-hotbar-modal integrator への依頼 | S4-train `uitk-train-inventory.cs` 手順 3 を Assert に戻す再走 | 回す（S4-gamma の全録画で拾う。`done.md` に転記） |
| S3-hotbar-modal 未確認 | S4-train の「Esc の 1 回目でフォーカスが外れない」 | やる（R-4 のラダーの ESC が検索欄で 1 回で外れることで観測。結果を書く） |

## File Structure

| ファイル | 種類 | 責務 |
|---|---|---|
| `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BuildMenu/BuildMenuToHotbarDragTest.cs` | Create | R-1。ドラッグの源と土台がビルドメニュー側にあるのでここ（前例: 装備 HUD とホットバーを並べる `Screens/Inventory/EquipmentWheelPassthroughTest.cs` は主役の画面に置く）。直下 .cs 4→6 本 |
| `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BuildMenu/BuildMenuSearchInputGuardTest.cs` | Create | R-2 |
| `moorestech_client/Assets/Scripts/Client.Tests/UIState/BuildMenu/BuildMenuStateTextInputGuardTest.cs` | Create | R-3。新しいサブディレクトリ（前例 `UIState/PauseMenu/`） |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage3/uitk-build-bp.cs` | Create | R-4。ラダー 3 |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage3/uitk-stage3-compare.cs` | Create | R-13 の組 2（PlayMode） |
| （組 1 の撮影） | 新しいファイルは作らない | `uloop execute-dynamic-code` から既存の `UiRuntimePanelCapture.CaptureToPng` と既存の見本（`BuildMenuReferenceDemo`・`HotbarViewModelSamples.WebFixture()`・モーダルのギャラリー見本）を呼ぶ（compare-procedure.md §3。撮影のための新しいコードを書かない） |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/blueprint-right-click-delete-via-ui.cs` | Modify（書き直し） | R-6 |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/connect/electric-wire-connect-via-ui.cs` | Delete | R-7 |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/fps-tps-view-toggle-via-ui.cs` | Modify（移植） | R-8 |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/blueprint-place-like-block.cs`・`connect/electric-wire-tool-pole-cycle-rotate-via-ui.cs`・`connect/electric-wire-tool-pole-ghost-visibility-check.cs` | Modify | R-9 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/Parts/ElectricWirePoleSelection.cs` | Modify（Task 7 で実測して換算が誤りと確かめたとき） | R-9。ホイールの換算 |
| `docs/uitk/TODO.md` | Modify | 束 3 の行（済）・「## 追従記録」（済）・「## 参照キャプチャ」・「## 壊れた録画の割り振り」の S3-ladder の 4 行・「## 見た目の例外」（LX-170〜 を使うときだけ） |
| `docs/uitk/disposition.md` | Modify | 「## 突き合わせの記録」に 1 行 |
| `../moorestech_logs/harness/uitk/S3-ladder/done.md` | Create（repo 外） | 報告 |
| `../moorestech_logs/uitk/reference-captures/web-<sha>/`・`web-<sha>-2560x1080/` | Create（repo 外） | R-12 |

## Tasks

### Task 1: 前提の確認・Editor の起動・記録の準備（済）

- [x] **Step 1**: worktree・土台 `origin/feature/uitk` 7e85205095・S3 の 2 本のマージ・`uitk-lifeline.cs` あり・`uitk-opening-hud.cs` 無しを確認
- [x] **Step 2**: `moores-wt status` で Mac の Editor 0 本を確かめ、本体がこの worktree の Editor を `uloop launch` で起動し compile 0 エラー
- [x] **Step 3**: bd `moorestech-albof.57`（claim 済み）・`done.md` の骨組み

---

### Task 2: 束 3 の暗転と G1（済）

- [x] **Step 1〜4**: 起点 `0e81bbe3c3`（S3-buildmenu の分岐点 533bba0b05 と origin/master の merge-base）で暗転（d3c8f58543）、未記帳 18 件を記帳（45bf61188c）。G1 ローカル「束 3: 未記帳 0 件」。取り込みのたびに Task 12 で見直す

---

### Task 3: パネル越しの D&D の L1（R-1）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BuildMenu/BuildMenuToHotbarDragTest.cs`

**Interfaces:**
- Consumes: `BuildMenuScreenTestHost`（`View`・`Commands.Selected`・`Toaster`・`Entry(string kind, Guid targetId)`・`OpenMenu`・`Settle`）、`BuildMenuSamples.Create()`・`BuildMenuSamples.WoodChest`、`HotbarHudView(IHotbarCommands, PlacementTargetIcons, toaster)`、`HotbarTestPresentation.Create()`、`HotbarViewModelSamples.WebFixture()`、`RecordingHotbarCommands.Calls`、`UiLayerStack.Build(VisualElement)`・`GetLayer(UiLayer, UiFamily)`
- Produces: なし

- [ ] **Step 1: テストを書く**

土台 `BuildMenuScreenTestHost` は SetUp で `View` を root へ `Mount` する。本番の層を通すため、このテストの SetUp で `UiLayerStack.Build(rootVisualElement)` を組み、`View` を `GetLayer(UiLayer.Screen, UiFamily.Stage)` へ、ホットバーを `GetLayer(UiLayer.Chrome, UiFamily.Viewport)` へ載せる（本番の載せ先: `BuildMenuScreenPresenter.cs:53`・`HotbarHudPresenter.cs:38`）。

```csharp
using Client.Tests.Ui.Screens.BuildMenu.Support;
using Client.Tests.Ui.Screens.Hotbar;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Layers;
using Client.Ui.Screens.Hotbar;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BuildMenu
{
    // hotbar.spec「ビルドメニューエントリを空き枠へドラッグするとhotbar.assign{slot,id}を送る」のパネル越しの通し（S3-hotbar-modal D-16 の残り）
    // Panel-crossing run of the hotbar.spec build-menu-drag case (the remainder of S3-hotbar-modal D-16)
    /// <summary>
    /// ビルドメニューのエントリを本番の層越しにホットバーへ落とす（L1。段階 3 の締め）
    /// Drops a build-menu entry onto the hotbar across the production layers (L1, stage 3 closing)
    /// </summary>
    public class BuildMenuToHotbarDragTest : BuildMenuScreenTestHost
    {
        private RecordingHotbarCommands _hotbarCommands;
        private HotbarHudView _hotbar;

        [SetUp]
        public void PlaceOnProductionLayers()
        {
            // 本番と同じ層と族へメニューとホットバーを載せる
            // Put the menu and the hotbar on the same layers and families as production
            var layers = UiLayerStack.Build(rootVisualElement);
            View.RemoveFromHierarchy();
            layers.GetLayer(UiLayer.Screen, UiFamily.Stage).Add(View);
            _hotbarCommands = new RecordingHotbarCommands();
            _hotbar = new HotbarHudView(_hotbarCommands, HotbarTestPresentation.Create(), Toaster);
            layers.GetLayer(UiLayer.Chrome, UiFamily.Viewport).Add(_hotbar);
            _hotbar.Render(HotbarViewModelSamples.WebFixture());
            OpenMenu(BuildMenuSamples.Create());
        }

        [Test]
        public void エントリを枠1へドラッグすると割当が1回だけ出て選択は出ない()
        {
            var from = Entry("block", BuildMenuSamples.WoodChest).worldBound.center;
            var to = Slot(1).worldBound.center;
            Assert.AreEqual(Slot(1), FirstSlotAncestorOrSelf(panel.Pick(to)), "枠 1 の中心がメニューに覆われていない");

            // 5px の閾値を越えてから枠へ運ぶ
            // Cross the 5px threshold, then carry it to the slot
            simulate.MouseDown(from, MouseButton.LeftMouse);
            simulate.MouseMove(from + new Vector2(8f, -8f));
            simulate.MouseMove(Vector2.Lerp(from, to, 0.5f));
            simulate.MouseMove(to);
            simulate.MouseUp(to, MouseButton.LeftMouse);
            Settle();

            CollectionAssert.AreEqual(new[] { $"assign:1:{BuildMenuSamples.WoodChest:D}" }, _hotbarCommands.Calls);
            CollectionAssert.IsEmpty(Commands.Selected);
        }

        [Test]
        public void 閾値未満で離すとタップの選択だけが出る()
        {
            var from = Entry("block", BuildMenuSamples.WoodChest).worldBound.center;
            simulate.MouseDown(from, MouseButton.LeftMouse);
            simulate.MouseMove(from + new Vector2(3f, 0f));
            simulate.MouseUp(from + new Vector2(3f, 0f), MouseButton.LeftMouse);
            Settle();

            CollectionAssert.AreEqual(new[] { BuildMenuSamples.WoodChest }, Commands.Selected);
            CollectionAssert.IsEmpty(_hotbarCommands.Calls);
        }

        private SlotFrame Slot(int index)
        {
            return Find<SlotFrame>(_hotbar, $"hotbar-slot-{index}");
        }

        private static SlotFrame FirstSlotAncestorOrSelf(VisualElement picked)
        {
            for (var e = picked; e != null; e = e.parent)
                if (e is SlotFrame frame) return frame;
            return null;
        }
    }
}
```
`<web-test>` は付けない（同じ Web テストの写しは `HotbarHudDropTest` が持つ。parity の重複を避ける）。`Find`・`Toaster` の土台での名前、`UiLayer`・`UiFamily` の名前空間は実物に合わせる。

- [ ] **Step 2: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.Ui.Screens.BuildMenu.BuildMenuToHotbarDragTest"`
Expected: ErrorCount 0 / 2 PASS。落ちたら、組み方（層・座標）の誤りか本番の不具合（落とし先が他の層の上で拾えない等）かを `panel.Pick` の結果で切り分ける。本番の不具合なら直して判断記録へ。

- [ ] **Step 3: parity とコミット**

```bash
node tools/uitk/test-name-parity.mjs --bundle 3; echo "exit=$?"
git add moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BuildMenu/BuildMenuToHotbarDragTest.cs*
git commit -m "test(uitk): ビルドメニューからホットバーへの本番の層越しの D&D を L1 で固定する（S3-ladder）"
```
Expected: exit 0。.meta は Unity が作ったものだけ。

---

### Task 4: 検索欄の入力の受入の L1（R-2・R-3）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BuildMenu/BuildMenuSearchInputGuardTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/UIState/BuildMenu/BuildMenuStateTextInputGuardTest.cs`

**Interfaces:**
- Consumes: `GameUiInputRules.Attach(VisualElement)`、`UiInputExclusivity.IsTextInputFocused`・`IsSuppressed(InputSuppressionScope)`・`IsEscapeConsumedThisFrame`・`ConsumeEscapeThisFrame()`・`SetGameUiTextInputFocused(bool)`・`SetActiveUiSystem(UiSystem)`・`ClearEscapeLatchForTest()`、`InputManager.UI.CloseUI.SetKeyDownForTest(bool)`・`TestReflection.ResetInputManagerCache()`（前例 `Client.Tests/Ui/Input/UiInputExclusivityEscapeLatchTest.cs:13-23,56`）、`BuildMenuState(BuildMenuSelection, UiStateCameraPolicyService, RightShortPressInputService)`・`GetNextUpdate()`・`UITransitContext.NextStateEnum`、`UIStateTestFixtureBase`（`KeyboardDevice`・`Press`・`Release`・`CreateCameraPolicy`）
- Produces: なし

- [ ] **Step 1: 画面側のテストを書く**

`UiTestHost` は SetUp で系を GameUi にし TearDown で Web に戻す（UiTestHost.cs:43,49）ので系の設定は要らない。画面側は ESC で閉じる口を持たない（閉じるのは × だけ。BuildMenuView.cs:51）ので、`CloseCount` は Assert しない（閉じる判定は UIState 側の R-3）。

```csharp
using Client.Input;
using Client.Tests.Ui.Screens.BuildMenu.Support;
using Client.Ui.Foundation.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BuildMenu
{
    /// <summary>
    /// 検索欄の入力中はゲームのキーを止め、ESC はフォーカスを外すだけ（L1・画面側）
    /// While typing in search the game keys stop, and ESC only drops focus (L1, screen side)
    /// </summary>
    public class BuildMenuSearchInputGuardTest : BuildMenuScreenTestHost
    {
        [SetUp]
        public void AttachRulesAndOpen()
        {
            // 規則は画面の要素に付け、基底の Clear で画面ごと捨てる（前例 GameUiInputRulesTest）
            // Attach the rules to the screen element so the base Clear discards them (precedent GameUiInputRulesTest)
            GameUiInputRules.Attach(View);
            OpenMenu(BuildMenuSamples.Create());
        }

        [TearDown]
        public void ResetExclusivity()
        {
            UiInputExclusivity.SetGameUiTextInputFocused(false);
            UiInputExclusivity.ClearEscapeLatchForTest();
        }

        [Test]
        public void 検索欄にフォーカスするとキーボードのゲーム入力が止まる()
        {
            SearchField().Focus();
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);
            Assert.IsTrue(UiInputExclusivity.IsSuppressed(InputSuppressionScope.Keyboard));
        }

        [Test]
        public void 検索欄のESCはフォーカスを外すだけで画面と検索語は残る()
        {
            var field = SearchField();
            field.Focus();
            simulate.TypingText("chest");
            simulate.KeyDown(KeyCode.Escape);

            Assert.IsTrue(UiInputExclusivity.IsEscapeConsumedThisFrame);
            Assert.IsNull(panel.focusController.focusedElement);
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
            Assert.AreEqual(DisplayStyle.Flex, View.resolvedStyle.display);
            Assert.AreEqual("chest", field.value);
        }

        [Test]
        public void 検索欄にフォーカスしたまま画面を閉じると入力中の旗が落ちる()
        {
            // 申し送り: 入力欄入りの View をフォーカスごと外すと旗が残る問題がモーダル以外にもあるか
            // Hand-off: does hiding a view with a focused field leave the flag set outside the modal too
            SearchField().Focus();
            View.SetShown(false);
            Settle();
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
        }

        private TextField SearchField()
        {
            return Find<TextField>(View, "build-menu-search");
        }
    }
}
```
実装時の確認: `simulate.TypingText`・`KeyDown` の使い方は `GameUiInputRulesTest.cs:65-83` と同じ。画面が出ているかの読み方は `BuildMenuViewTest` の既存の判定に合わせる。3 件目が赤なら申し送りの不具合の実測なので、テストは消して bd（`--parent moorestech-albof.57`、題「ビルドメニューを検索欄にフォーカスしたまま閉じると入力中の旗が残る」・赤の Assert の出力を note）を立て、`done.md`「integrator への依頼」に書く（直す場所〈部品か宿主か〉は申し送りどおり integrator が決める）。

- [ ] **Step 2: UIState 側のテストを書く**

```csharp
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Input;
using Client.Tests.UIState.Fakes;
using NUnit.Framework;

namespace Client.Tests.UIState.BuildMenu
{
    /// <summary>
    /// GameUi の入力中・ESC 消費のフレームはビルドメニューの状態が閉じない（L1・UIState 側）
    /// The build-menu state does not close while GameUi text is focused or ESC was consumed (L1, state side)
    /// </summary>
    public class BuildMenuStateTextInputGuardTest : UIStateTestFixtureBase
    {
        private BuildMenuState _state;

        public override void Setup()
        {
            base.Setup();
            // 系の既定は Web なので GameUi にし、ESC の押下はテストから差し込む（前例 UiInputExclusivityEscapeLatchTest）
            // The default system is Web, so switch to GameUi; ESC presses are injected (precedent UiInputExclusivityEscapeLatchTest)
            UiInputExclusivity.SetActiveUiSystem(UiSystem.GameUi);
            TestReflection.ResetInputManagerCache();
            _state = new BuildMenuState(new BuildMenuSelection(), CreateCameraPolicy(new FakePlayerCameraInteractionApplier()), new RightShortPressInputService(new RightShortPressInput()));
            _state.OnEnter(new UITransitContext(UIStateEnum.GameScreen, null));
        }

        public override void TearDown()
        {
            InputManager.UI.CloseUI.SetKeyDownForTest(false);
            UiInputExclusivity.SetGameUiTextInputFocused(false);
            UiInputExclusivity.ClearEscapeLatchForTest();
            UiInputExclusivity.SetActiveUiSystem(UiSystem.Web);
            TestReflection.ResetInputManagerCache();
            base.TearDown();
        }

        [Test]
        public void Bは入力中だけ閉じない()
        {
            UiInputExclusivity.SetGameUiTextInputFocused(true);
            Press(KeyboardDevice.bKey);
            Assert.IsNull(_state.GetNextUpdate(), "入力中の B では閉じない");

            UiInputExclusivity.SetGameUiTextInputFocused(false);
            Release(KeyboardDevice.bKey);
            Press(KeyboardDevice.bKey);
            Assert.AreEqual(UIStateEnum.GameScreen, _state.GetNextUpdate()?.NextStateEnum, "入力中でない B では閉じる");
        }

        [Test]
        public void ESCは入力欄が消費したフレームだけ閉じない()
        {
            InputManager.UI.CloseUI.SetKeyDownForTest(true);
            Assert.AreEqual(UIStateEnum.GameScreen, _state.GetNextUpdate()?.NextStateEnum, "ラッチの無い ESC では閉じる");

            UiInputExclusivity.ConsumeEscapeThisFrame();
            Assert.IsNull(_state.GetNextUpdate(), "入力欄が消費した ESC では閉じない");
        }
    }
}
```
実装時の確認（実物の名前に合わせる）: `BuildMenuState` の組み立ては `RightShortPressTransitionTest.cs:81-86` の実コードを写す（using の名前空間も同じ）。`TestReflection` の名前空間と `CloseUI` の型（`InputKey`）は `UiInputExclusivityEscapeLatchTest.cs` を見る。`SetActiveUiSystem` の名前は `UiInputExclusivityGameUiTest.cs:18-32` を見る。`Press`/`Release` の後に InputSystem の更新が要るなら前例 `PlayerViewModeInputTest.cs:32` と同じ形にする。系を GameUi にしたうえで `Bは入力中だけ閉じない` の 1 つ目が赤なら本番の不具合なので、`BuildMenuState` の B の読み方を直して判断記録へ（死活表の行の確認も行う）。

- [ ] **Step 3: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BuildMenuSearchInputGuardTest|BuildMenuStateTextInputGuardTest|RightShortPressTransitionTest|UiInputExclusivity"`
Expected: ErrorCount 0 / 全 PASS（既存の `UiInputExclusivity*` のテストが、このテストの系の切替を持ち越して落ちないこと）

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BuildMenu/BuildMenuSearchInputGuardTest.cs* moorestech_client/Assets/Scripts/Client.Tests/UIState/BuildMenu*
git commit -m "test(uitk): ビルドメニューの検索欄の入力中のキー止めと ESC の外しを画面と UIState の L1 で固定する（S3-ladder）"
```

---

### Task 5: ラダー 3 `uitk-build-bp`（R-4）

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage3/uitk-build-bp.cs`

**Interfaces:**
- Consumes: `PlaytestRunner.Run`・`p.SetupDebugEnvironment`・`p.SkipOpeningSkitIfPlaying`・`p.ExitToGameScreen`・`p.PressKey`・`p.WaitUiState`・`p.ClickUi`・`p.SetText(testid, text)`・`p.Hotbar.UnlockBlueprint()`・`p.WarpPlayer`・`p.AimAt`・`p.ClickPlace`・`p.UntilUiElement`・`p.WaitForBlueprint`・`p.WaitBlockGameObject`・`p.Screenshot`・`p.PlayerPosition`・`p.CurrentUiState`、`PlaytestUiElementOps.FindUi`、`SemanticInput.*`、`PlaytestBuildMenuOps.BuildMenuBlockTestId(string)`、`BlueprintCopyPasteScenarioChecks.SelectEntry(categoryTestId, entryTestId)`（自分で B を押す）、`ClientHotbarDatastore.Assignments`
- Produces: `scenarios/uitk/stage3/uitk-build-bp.cs`（ラダー 3。S4-gamma・X-prerun が毎回流す。S4-gamma 計画の `scenarios/uitk/stage3/uitk-build-bp.cs` の読み替えを `done.md` の依頼に書く）

- [ ] **Step 1: シナリオを書く**

前例 `scenarios/uitk/stage3/uitk-hotbar-and-bp-modal.cs` の冒頭（系の主張・入力の準備）と `CenterOf`・`DragAsync`・`HoldKeyAsync` を写す。1 本 200 行未満。

```csharp
// ラダー 3: B → 検索 → エントリをホットバーへドラッグ → 選んで設置 → BP をコピーして名前を付ける（GameUi。S3-ladder）
// Ladder 3: B -> search -> drag an entry onto the hotbar -> select and place -> copy a blueprint and name it (GameUi, S3-ladder)
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.Hotbar;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 300f };
return PlaytestRunner.Run("uitk-build-bp", options, async p =>
{
    // 1. 系の主張と入力の準備（前例 uitk-hotbar-and-bp-modal）
    // 1. Assert the UI system and prepare input (precedent uitk-hotbar-and-bp-modal)
    p.Note($"全体スイッチ 系={UiInputExclusivity.ActiveUiSystem}");
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）");
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var hotbar = resolver.Resolve<ClientHotbarDatastore>();
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();

    // 2. B で開き、検索欄の入力中は W で動かず、ESC はフォーカスだけ外し、外した後の W では動く
    // 2. Open with B; W does not move while search is focused, ESC only drops focus, then W moves
    await p.PressKey(Key.B);
    await p.WaitUiState(UIStateEnum.BuildMenu, 10f);
    await p.ClickUi("build-menu-search");
    await p.Until(() => UiInputExclusivity.IsTextInputFocused, 3f, "検索欄のクリックで入力中の旗が立つ");
    var before = p.PlayerPosition;
    await HoldKeyAsync(Key.W, "入力中に W を 0.6 秒押す");
    p.Assert(Vector3.Distance(before, p.PlayerPosition) < 0.05f, $"検索欄の入力中は W で動かない {before} → {p.PlayerPosition}");
    await p.PressKey(Key.Escape);
    await p.WaitSeconds(0.3f);
    p.Assert(!UiInputExclusivity.IsTextInputFocused, "ESC 1 回で検索欄のフォーカスが外れる");
    p.Assert(p.CurrentUiState == UIStateEnum.BuildMenu, $"ESC でメニューは閉じない 実際:{p.CurrentUiState}");
    var afterBlur = p.PlayerPosition;
    await HoldKeyAsync(Key.W, "フォーカスを外した後に W を 0.6 秒押す（対照）");
    p.Assert(Vector3.Distance(afterBlur, p.PlayerPosition) > 0.05f, $"フォーカスを外した後の W では動く（入力が届いている対照） {afterBlur} → {p.PlayerPosition}");

    // 3. 検索語を入れ、石窯のエントリを枠 1 へドラッグ（枠 1 だけが石窯になり、建築モードにはならない）
    // 3. Type the query and drag the stone oven entry to slot 1 (only slot 1 becomes the oven; no build mode)
    await p.SetText("build-menu-search", "石窯");
    var entryTestId = PlaytestBuildMenuOps.BuildMenuBlockTestId("石窯");
    await p.UntilUiElement(entryTestId, 5f);
    var ovenId = Guid.Parse(entryTestId.Substring(entryTestId.Length - 36));
    var assignedBefore = hotbar.Assignments.ToArray();
    p.Assert(assignedBefore[1] == Guid.Empty, "ドラッグの前は枠 1 が空き");
    await p.Screenshot("01-search");
    await DragAsync(await CenterOf(entryTestId), await CenterOf("hotbar-slot-1"));
    await p.Until(() => hotbar.Assignments[1] == ovenId, 5f, "枠 1 に石窯が割り当たる");
    p.Assert(Enumerable.Range(0, assignedBefore.Length).Where(i => i != 1).All(i => hotbar.Assignments[i] == assignedBefore[i]), "枠 1 以外の割当は変わらない");
    p.Assert(p.CurrentUiState == UIStateEnum.BuildMenu, $"ドラッグでは選択（建築モード）にならない 実際:{p.CurrentUiState}");
    await p.Screenshot("02-dragged");

    // 4. 閉じて範囲の外へ退き、数字キー 2 で枠 1 を選んで石窯を建てる
    // 4. Close, step out of the range, pick slot 1 with digit 2 and build the stone oven
    await p.PressKey(Key.B);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    p.WarpPlayer(new Vector3(6f, 33.5f, -5f));
    await p.PressKey(Key.Digit2);
    await p.WaitUiState(UIStateEnum.PlaceBlock, 10f);
    await p.AimAt(new Vector3(2.5f, 32f, 2.5f));
    await p.ClickPlace();
    await p.WaitBlockGameObject(new Vector3Int(2, 32, 2));
    await p.Screenshot("03-placed");
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 5. BP コピーを選び、範囲を打って名前を付ける（名前は SetText）
    // 5. Pick blueprint copy, mark the range and name it (the name via SetText)
    await p.Hotbar.UnlockBlueprint();
    var checks = new BlueprintCopyPasteScenarioChecks(p, resolver.Resolve<MouseCursorTooltipState>());
    await checks.SelectEntry("build-menu-category-d1000000-0000-4000-8000-000000000009", "build-menu-entry-blueprintCopy-88dd687d-aceb-4aeb-94e8-44be1e1f5a0d");
    await p.AimAt(new Vector3(0.5f, 32f, 0.5f));
    await p.ClickPlace();
    await p.AimAt(new Vector3(4.5f, 32f, 4.5f));
    await p.ClickPlace();
    await p.UntilUiElement("modal-input", 10f);
    await p.SetText("modal-input", "ラダーBP");
    await p.ClickUi("modal-button");
    await p.WaitForBlueprint("ラダーBP", true, 15f, "BP『ラダーBP』が登録される");
    await p.Screenshot("04-blueprint");

    #region Internal

    async UniTask<Vector2> CenterOf(string testid) => (await PlaytestUiElementOps.FindUi(testid)).ScreenRect.center;

    async UniTask DragAsync(Vector2 from, Vector2 to)
    {
        p.Note($"ドラッグ {from} → {to}");
        SemanticInput.MouseMoveTo(from);
        await p.WaitSeconds(0.3f);
        SemanticInput.MouseButtonDown(0);
        await UniTask.DelayFrame(2);
        await SemanticInput.MouseGlideTo(to, 0.4f);
        SemanticInput.MouseButtonUp(0);
        await p.WaitSeconds(0.3f);
    }

    async UniTask HoldKeyAsync(Key key, string label)
    {
        p.Note(label);
        SemanticInput.KeyDown(key);
        await p.WaitSeconds(0.6f);
        SemanticInput.KeyUp(key);
    }

    #endregion
});
```
実装時の確認（実物で読む）: `BuildMenuBlockTestId` の返り値の末尾が Guid か（違えば前例の石窯の Id の引き方に替える）。B で開いた直後はカーソル解放なので Alt は要らない（`uitk-build-menu.cs` の操作で確かめる。要るなら前例の `HoldAltAsync` を足す）。`Assignments` の型に合わせて `ToArray()` の書き方を合わせる。

- [ ] **Step 2: GameUi で流す**

```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
uloop execute-dynamic-code --project-path ./moorestech_client --code 'Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi);'
.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage3/uitk-build-bp.cs
```
Expected: `result.json` `Success: true`・ErrorLogs 0・Note「全体スイッチ 系=GameUi」。落ちた Assert はシナリオの誤りか本番の不具合かを result.json とスクショで切り分け、本番の不具合はこのレーンで直して判断記録へ。

- [ ] **Step 3: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage3/
git commit -m "test(uitk): ラダー 3 uitk-build-bp を足す（S3-ladder）"
```
`done.md`「証拠」に PlaytestResults のディレクトリ名・PASS 数を書く。mp4 は Task 12 の報告ページ用に 1 本だけ残し、他は消す。

---

### Task 6: wwxy の束 3 の 4 本（R-6・R-7・R-8・R-15）

**Files:**
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/blueprint-right-click-delete-via-ui.cs`
- Delete: `.agents/skills/unity-playmode-recorded-playtest/scenarios/connect/electric-wire-connect-via-ui.cs`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/fps-tps-view-toggle-via-ui.cs`
- Modify: `docs/uitk/TODO.md`（「## 壊れた録画の割り振り」の S3-ladder の 4 行）

**Interfaces:**
- Consumes: Task 5 と同じ DSL、`ClientBlueprintLibrary.Blueprints`、`CrosshairVisibility.IsVisible()`、エントリ名 `build-menu-entry-blueprint-<guid>`（`BuildMenuEntrySlot.cs:54`）、UITK の `crosshair`（`CrosshairHudView.cs:19`）、`GameUiRoot.Instance.GetRootElement()`

- [ ] **Step 1: BP の右クリック削除を書き直す**

旧版（143 行）の検証の意図（ビルドメニューに出た BP をエントリの右クリックで消す・サーバーの BP が消える・メニューから消える）を残し、uGUI 型の参照を全部外す。系は主張せず `p.Note` に出す（Web でも流すため）。流れ: `uitk-hotbar-and-bp-modal.cs` の手順 6 と同じく木のチェストを置き、ワープしてから BP コピーで BP『delbp』を作る（名前は `p.SetText("modal-input", "delbp")`）→ ライブラリから `delbp` の Guid を引く → B → BP のカテゴリを開く（`BlueprintCopyPasteScenarioChecks` のカテゴリの開き方を写す）→ `build-menu-entry-blueprint-<Guid>` の中心へ `SemanticInput.MouseMoveTo` → 右クリック（`SemanticInput` に右ボタンの口があればそれ、無ければ `MouseButtonDown(1)`/`MouseButtonUp(1)`）→ 削除に確認の UI がある作りなら確定（`BuildMenuCommands.DeleteBlueprint` の流れを読んで決める）→ `p.WaitForBlueprint("delbp", false, 15f, "BP『delbp』がサーバーから消える")` → メニューからも消えることを同期の照会で待つ（GameUi は `p.Until(() => GameUiRoot.Instance.GetRootElement().Q(entryTestId) == null, 5f, …)`、Web は `PlaytestUiElementOps.FindUi` を await するループ）。`p.Until` のラムダの中で await しない（`Until(Func<bool>, …)`。PlaytestDriver.cs:177）。冒頭の ⚠ コメントを新しい 2 行の説明に置き換える。

- [ ] **Step 2: 電線の旧シナリオを消す**

```bash
git rm .agents/skills/unity-playmode-recorded-playtest/scenarios/connect/electric-wire-connect-via-ui.cs
git grep -n "electric-wire-connect-via-ui" -- . ':!*.meta' ':!docs/superpowers/plans' ':!docs/uitk-migration/execution/research'
```
Expected: 台帳の 1 行の他に参照が無い（過去の計画書・研究文書は書き換えない）。旧だけが見ていた検証（結線前は別セグメント・セグメント統合・電力範囲ボックスの表示）を `done.md`「integrator への依頼」に 1 行書く。

- [ ] **Step 3: 視点切替を移植する**

旧版の `CrosshairView` の参照を、両系で `resolver.Resolve<CrosshairVisibility>().IsVisible()` に、GameUi では加えて `PlaytestUiElementOps.FindUi("crosshair")` の `IsFound`（UITK の `FindUi` は見えている要素だけを数える。GameUiElementQuery.cs:15）に置き換える。Web の Crosshair には testid が無い（webui/src/features/commonHud/CommonHud.tsx:8）ので Web は `CrosshairVisibility` だけで判定し、その旨を `p.Note` に出す。FPS でクロスヘアが見え TPS で消えること・プレイヤーのレンダラの表示切替の Assert は旧版のまま。冒頭の ⚠ コメントを置き換える。

- [ ] **Step 4: GameUi と Web で流す**

```bash
SC=.agents/skills/unity-playmode-recorded-playtest/scenarios
run() { .agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh $SC/$1.cs; }
sys() { uloop control-play-mode --project-path ./moorestech_client --action Stop; uloop execute-dynamic-code --project-path ./moorestech_client --code "Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.$1);"; }
sys GameUi
for s in building/blueprint-copy-paste-via-ui building/blueprint-right-click-delete-via-ui misc/fps-tps-view-toggle-via-ui connect/electric-wire-tool-extend-chain-via-ui; do run $s; done
sys Web
for s in building/blueprint-copy-paste-via-ui building/blueprint-right-click-delete-via-ui misc/fps-tps-view-toggle-via-ui; do run $s; done
sys GameUi
```
Expected: GameUi で 4 本・Web で 3 本 `Success: true`。Web で CEF が出ない（RendererUnavailable・`build-menu-panel` 待ちのタイムアウト）なら、その Editor ログの行を `done.md`「未確認」に書いて Web 側は未確認にする。

- [ ] **Step 5: 台帳の 4 行を書き換える**

「## 壊れた録画の割り振り」の S3-ladder の 4 行の「方針」の列を結果に書き換える（例: `書き直した（S3-hotbar-modal 57a78b071e。S3-ladder で GameUi・Web とも緑を確認）`・`書き直した（S3-ladder。GameUi・Web とも緑）`・`削除した（S3-ladder。後継 extend-chain は GameUi で緑）`・`移植した（S3-ladder。CrosshairVisibility と crosshair の照会）`）。Web が未確認なら「Web は未確認（Editor の CEF が出ない）」。`X-prerun` の語を書かない。

- [ ] **Step 6: コミットする**

```bash
git add -A .agents/skills/unity-playmode-recorded-playtest/scenarios docs/uitk/TODO.md
git commit -m "test(uitk): wwxy の束 3 の 4 本を直す（BP 右クリック削除の書き直し・旧電線の削除・視点切替の移植）（S3-ladder）"
```

---

### Task 7: 申し送りの録画 3 本（R-9）

**Files:**
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/blueprint-place-like-block.cs`・`connect/electric-wire-tool-pole-cycle-rotate-via-ui.cs`・`connect/electric-wire-tool-pole-ghost-visibility-check.cs`
- Modify（換算が誤りと実測したとき）: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/Parts/ElectricWirePoleSelection.cs`
- Test（同上）: `ScrollStepAccumulator`・`ElectricWirePoleSelection` の既存テストのあるディレクトリ（`git grep -l ScrollStepAccumulator -- moorestech_client/Assets/Scripts/Client.Tests` で引く）

**Interfaces:**
- Consumes: Task 5 と同じ DSL。InputSystem の設定 `ScrollDeltaBehavior.UniformAcrossAllPlatforms`（`Client.Tests/Input/Pointer/ScrollDeltaBehaviorSettingTest.cs`。1 ノッチ＝1.0）、`SemanticInput.MouseWheel(1f)`（scroll.y=1 を入れる。SemanticInput.cs:158-163）

- [ ] **Step 1: 3 本を GameUi で流して落ち方を取る**

```bash
for s in building/blueprint-place-like-block connect/electric-wire-tool-pole-cycle-rotate-via-ui connect/electric-wire-tool-pole-ghost-visibility-check; do
  .agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh .agents/skills/unity-playmode-recorded-playtest/scenarios/$s.cs
done
```
Expected: 落ちた Assert と `result.json` を `done.md`「証拠」に書く。

- [ ] **Step 2: place-like-block の準備を仕様に合わせる**

「電柱→石窯を結線 理由:ConnectionLimit」は仕様どおり（燃料機械は電線口 0。`WirelessFuelMachineWireTest`「電柱から燃料機械へ手動でつなげない」が ConnectionLimit を Assert。シナリオはこの仕様より前に書かれた）。判定コードは直さない。シナリオの石窯（`oven`）を、ピン先マスタで電線がつながる電気機械（ピン先 `moorestech_master` の `blocks.json` から、電力を消費し解放済みにできる小さな機械を選ぶ。選んだ名前と理由を `done.md` に）へ替え、それに合わせて外接箱 `size = (6,5,5)` と「電線 2 本を保存」の Assert・その機械の位置と Unlock を直す。準備が通った後、モーダルの工程まで GameUi で `Success: true`。

- [ ] **Step 3: 電柱のホイールの換算を実測して直す（bd moorestech-9a7t9）**

`ElectricWirePoleSelection.ReadScroll` の中で、受け取った `Mouse.current.scroll.ReadValue().y` と `ScrollStepAccumulator` に渡した値を一時的に `Debug.Log` し（実測の後に消す）、シナリオの `SemanticInput.MouseWheel(1f)` 1 回で何が届くかを録る（`uloop execute-dynamic-code` で後から読むのは不可。スクロールは 1 フレームで 0 に戻る）。実マウスの 1 ノッチは測れなければ「未実測」と書く。
- 1 ノッチ＝1.0 が届き `/100f` で 0.01 段になっているなら、`/100f` を外して 1 ノッチ＝1 段に戻す。駆動（`ManualUpdate` から毎フレーム読む形）は変えず、`InputSystemPointerWheelStream` の購読にも替えない（D-6）。`ScrollStepAccumulator` に 1.0 を入れると 1 段進む L1 を既存のテストの置き場に 1 件足す
- トラックパッドの細かい量で 1 段に要る量が変わること（死活表）を D-6 に書く
- 直したら bd 9a7t9 に実測値と直した行を note して close

- [ ] **Step 4: 装備のホイールの対照（束 4）を調べて回す**

pole-cycle-rotate の対照「ゲーム画面のホイールで装備選択が変わる（2 ノッチ）」（79〜88 行。`LocalPlayerEquipment.SelectedIndex`）の読み手は `Client.Ui/Screens/Inventory/Wheel/EquipmentWheelInput.cs`（抑止は `EquipmentWheelGate`。設計書 A-145）で束 4 の持ち物。製品のコードは直さない。`EquipmentWheelInput`・`EquipmentWheelRouter`・`EquipmentWheelGate` の分岐のどれで止まったか（装備の枠数・UI の上の判定・Editor の更新の除外）を一時のログで取り、原因がシナリオ側（準備・ポインタの位置）ならシナリオを直す。製品側なら bd（`--parent moorestech-albof.57`。S4-inventory の系譜であることと証拠を本文に）を立て、シナリオの対照の Assert はそのまま残して `done.md`「未完了」に書く。一時のログは消す。

- [ ] **Step 5: 削除済みの電柱名ラベルを読む Assert を直す**

ラベルは 437dee26f3 で削除され、名前はカーソルのツールチップへ移った（`git show 437dee26f3 --stat` で確かめる）。pole-cycle-rotate の 71〜73・123〜124 行と ghost-visibility の 51〜53・74〜78 行の `ElectricWirePoleNameLabel` の参照を、後継（`MouseCursorTooltipState` に選択中の電柱名が出るか）を読む Assert に置き換える。後継で読めなければ名前の Assert を外して `SelectedPoleName()`・ゴーストの表示の Assert だけ残す（冒頭のコメントに理由を 2 行）。

- [ ] **Step 6: 流し直し・コンパイル・テスト・コミット**

本番コードを触ったら `uloop compile` と `uloop run-tests --filter-type regex --filter-value "ElectricWire|ScrollStep|Wheel"`。3 本を GameUi で流し直して `Success: true`（装備の対照だけが束 4 の原因で残るなら、その 1 件だけ Step 4 の証拠付きで未完了）。
```bash
git add -A .agents/skills/unity-playmode-recorded-playtest/scenarios <本番とテストのファイル>
git commit -m "fix(uitk): 申し送りの録画 3 本を直す（結線の前提・電柱のホイールの換算・削除済みラベルの Assert）（S3-ladder）"
```
（本番の修正とシナリオの修正は別コミットにしてよい）

---

### Task 8: 参照の撮り直しと束 3 の突き合わせの記録（R-11・R-12）

**Files:**
- Modify: `docs/uitk/TODO.md`（「## 参照キャプチャ」）
- Modify: `docs/uitk/disposition.md`
- Create（repo 外）: `../moorestech_logs/uitk/reference-captures/web-<sha>/`・`web-<sha>-2560x1080/`

- [ ] **Step 1: master の worktree で参照を撮る（Editor 不要）**

compare-procedure.md §2 の手順。worktree は `moores-wt new uitk-ref/s3-ladder --no-editor --from origin/master` で作る（置き場は環境ローカル規約の worktree の場所）。
```bash
cd <その worktree>/moorestech_web/webui && pnpm install && pnpm build
SHA=$(git rev-parse --short=10 HEAD)
UITK_REFERENCE_OUT=<moorestech_logs の絶対パス>/uitk/reference-captures/web-$SHA UITK_REFERENCE_PORT=<空きポート> node --import tsx e2e/uitk-reference/capture.ts
```
2560×1080 は既存の `web-0e81bbe3c3-2560x1080` を撮ったときの指定（`capture.ts`・`referenceShots.ts` の解像度の引数か環境変数を読んで決める）で `web-$SHA-2560x1080` に撮る。出た `3-build-menu.png`・`3-hotbar-equipment-empty.png`・`3-modal.png` が af5fcc9cb3 の後の Web（マスの絵が枠の中央）であることを目で見る。撮り終えたら `moores-wt rm uitk-ref/s3-ladder`。

- [ ] **Step 2: 台帳の参照を書き換える**

「## 参照キャプチャ」に、束 3 の最新の参照として撮影元 `$SHA`・置き場 2 つ（1280×720・2560×1080）の 1 行を足し、S3-hotbar-modal の `web-8a585f742a-1920x1080`（origin/master 8a585f742a・91 件）も 1 行足す。

- [ ] **Step 3: 突き合わせを記録する**

Run: `node tools/uitk/test-name-parity.mjs --bundle 3; echo "exit=$?"`
Expected: `束 3: Web 99 件 / 写した 99 / 処遇 0 / 写す予定 0 / 未対応 0`・exit 0（件数が増えていれば、増えた未対応をこのレーンで写す）。`docs/uitk/disposition.md`「## 突き合わせの記録」に `| 2026-11-12 | 3 | <HEAD 10 桁> | 99 | 99 | 0 | 0 | 0 |`（実測の件数）を足す。

- [ ] **Step 4: コミットする**

```bash
git add docs/uitk/TODO.md docs/uitk/disposition.md
git commit -m "docs(uitk): 束 3 の参照を撮り直して台帳に書き、突き合わせを記録する（差分 0）（S3-ladder）"
```
参照の PNG は `../moorestech_logs` でコミット・push する（`../moorestech_logs/README.md` を先に読む）。

---

### Task 9: 並べ比べと独立評価（R-13）

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage3/uitk-stage3-compare.cs`
- Modify（差を直すとき）: 束 3 の USS（`moorestech_client/Assets/Scripts/Client.Ui/Screens/BuildMenu/**`・`Client.Ui/Screens/Hotbar/**`・`Client.Ui/Screens/Modal/**` の .uss）
- Modify（例外にするとき）: `docs/uitk/TODO.md`「## 見た目の例外」（LX-170〜179）

**Interfaces:**
- Consumes: `Client.Ui.Editor.Capture.UiRuntimePanelCapture.CaptureToPng(VisualElement, int, int, Color32, UiCaptureLocalization, string)`（compare-procedure.md §3）、見本 `BuildMenuReferenceDemo.Create()`（`Client.Tests/Ui/Screens/BuildMenu/Support/`）・`HotbarViewModelSamples.WebFixture()`・モーダルのギャラリー見本（`Client.Ui.Editor.Gallery.Entries` の下を `grep -rl Modal` で引く）、`PlaytestGameViewOps.SetResolution(CancellationToken, int, int)`、Task 8 の参照

- [ ] **Step 1: 組 1（色・寸法）を EditMode で撮る**

PlayMode を止め、`uloop execute-dynamic-code` から `UiRuntimePanelCapture.CaptureToPng` を呼んで、3 状態を 1280×720 と 2560×1080 で撮る（compare-procedure.md §3 の例の形。撮影のための新しいコードを repo に足さない）。状態は Web の fixture に合わせる: ビルドメニューは S3-buildmenu が使った `BuildMenuReferenceDemo`、ホットバーは `HotbarViewModelSamples.WebFixture()`（枠 4 未解決・枠 7 割当）、モーダルは空欄・確定ボタン無効。地の色は各参照 PNG の該当画面の外側の画素を読んだ 1 色（S3-hotbar-modal は (217,114,52)）。幕や地の画像が要る状態は S3-buildmenu D-61 の回避（`UiLayerStack` の Backdrop へ `ScreenBackdropView` を載せる）を使う。ファイル名は参照の撮影 id と同じにし、`moorestech_client/.uloop/outputs/compare/S3-ladder/` に置く。

- [ ] **Step 2: 組 2（崩れの確認）を PlayMode で撮る**

`uitk-stage3-compare.cs`（200 行未満）: 系を GameUi で主張 → `PlaytestGameViewOps.SetResolution(<p のキャンセルトークン>, 1280, 720)` → ホットバーは `p.Hotbar.AssignHotbar(0, "木のチェスト")` だけ → `3-hotbar-equipment-empty` → B でビルドメニュー（最初のカテゴリ・ホバー無し）→ `3-build-menu` → 閉じてワープし BP コピーの範囲を打ってモーダル → `3-modal` → 同じ 3 枚を 2560×1080 で。GameUi で流して PNG 6 枚。

- [ ] **Step 3: 評価役 2 体に別々に判定させる（本体が派遣する）**

作業用に参照・組 1・組 2 を並べた簡易 HTML を scratchpad に作り（評価役の閲覧用。PR の報告ページは Task 12 で載せ先の生成器の書式にする）、opus の subagent を 2 体、同じ依頼文で別々に起こす（互いの結果を見せない）。渡すもの: ゴールの原文「WebUIと同じ見た目で、適切な設計、コンポーネント化されたUITKベースのUIを開発すること」、画像のローカルパス（参照 6・組 1 の 6・組 2 の 6）と撮影元 sha、`docs/uitk/TODO.md`「## 見た目の例外」、設計書 §3.3・§3.4、compare-procedure.md §5。依頼: 「色・寸法・配置・装飾の有無・文字の大きさと段組の差は参照と組 1 で判定する。組 2 は崩れ（はみ出し・重なり・欠け）の有無だけを見る。アイテムの並び・名前・地の違いは差に数えない。既存の LX に当たる差はその ID を書く。各差に座標か画素の値を添える」。報告は scratchpad のファイルへ、返答は 3 行。

- [ ] **Step 4: 再現して処遇を決める**

2 体の指摘を合わせ、本体（か別の subagent）が PNG の画素・寸法を測って再現できたものだけ残す。残った差ごとに、USS で直せるものは直して撮り直し、直すのに大きな対応が要り一目で分からない差だけ LX-170〜179 に記帳（`/* look-exception: LX-n */` の印・`LookExceptionLedgerTest` を通す）。LX-72（題の影）・LX-76（2560×1080 のぼけ）に当たる差は既存の ID で処遇し、観測を `done.md` に書く。全行を `done.md`「差の表」に「観点・判定・対応」で書く（S3-buildmenu の差の表の書式）。

- [ ] **Step 5: テストとコミット**

USS を触ったら `uloop compile` と `uloop run-tests --filter-type regex --filter-value "^Client\.Tests\.Ui\.(Screens\.(BuildMenu|Hotbar|Modal)|Conventions|Theme)\."`。
```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage3/uitk-stage3-compare.cs <直した USS> docs/uitk/TODO.md
git commit -m "test(uitk): 束 3 の並べ比べの撮影を足し、独立評価で再現した差を処遇する（S3-ladder）"
```
撮影の PNG は報告ページへ載せた後に worktree から消す。

---

### Task 10: 最終の HEAD でラダー 1〜3 と L1 を通す（R-5）

- [ ] **Step 1: テスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\." --timeout-seconds 1500` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.(UIState|Input)\."`
Expected: 全 PASS（件数を `done.md` へ）。

- [ ] **Step 2: ラダー**

GameUi で `uitk/stage1/uitk-lifeline.cs`・`uitk/stage3/uitk-build-bp.cs`（`uitk/uitk-opening-hud.cs` があればそれも）を流す。Expected: 全部 `Success: true`・ErrorLogs 0。ラダー 2 が無ければ R-5 のとおり「要裁定」へ。

- [ ] **Step 3: コミットは無し**（記録のみ）

---

### Task 11: moores-code-review で全ブランチレビュー（必須）

必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。base は `origin/feature/uitk`。設計判断は agent 既定で推奨案を採り、判断記録に「agent 既定（moores-code-review Dn）」と書く（ゴール・裁定から外れるものだけ `done.md`「要裁定」）。自動適用・反映の後は、Task 5・6・7 の録画を反映後のバイナリで流し直してから完了とする（実行計画 §4.3 の 1）。

---

### Task 12: feature/uitk への PR・取り込み・マージ（必須）

セッション終了可能状態にすること: pr-createスキルでPRを作成し、コンフリクトがあれば解消・コンパイル確認のうえpushする。このレーンは実行計画 §4.3 の形（base=feature/uitk、agent がマージ）。

- [ ] **Step 1: PR**: pr-create で `base=feature/uitk`。報告ページ `https://review.moores.tech/pr/<番号>/report/` に並べ比べ（参照・組 1・組 2）とラダーの録画を、載せ先の生成器の書式（`~/hermes-agent/data/services/pr-review/publish/README.md` の manifest の group／caption）で載せ、本文の先頭行をその URL の h1 にする
- [ ] **Step 2: 取り込み**: `git merge origin/feature/uitk`。共有区画の衝突は両方の行を採る。`uloop compile`・Task 10 Step 1 のテスト・G1 ローカル（束 3 未記帳 0）・判定経路に触れた衝突ならラダーの再走。push
- [ ] **Step 3: G1 とマージ**: PR の `UITK Web follow gate (G1)` が pass を見て `gh pr merge <番号> --merge`（Squash 禁止）
- [ ] **Step 4: 片付け**: `moores-wt status` で自分の Editor だと確かめて Editor を閉じる。録画・PlaytestResults・TestResults・撮影 PNG を消す。テストで作ったワールドを `list-work-saves.sh` で探して消す。bd を close。worktree の `moores-wt rm` は指揮が行う（依頼文の指示。D-8）
- [ ] **Step 5: 報告と合図**: `done.md` の 1 行目を「状態: 完了」（止まったら「状態: 停止（理由）」）にし、完了条件ごとの証拠・PR URL・未完了・要裁定・integrator への依頼（申し送りの処遇の「回す」行・A-210〜 の案・S4-gamma のラダー 3 のパスの読み替え・旧電線シナリオだけが見ていた検証）を書いて `../moorestech_logs` でコミット・push。最後に `cmux wait-for -S uitk-s3-ladder` を 1 回

## Self-Review

- Requirements の各行のタスク: R-1→T3、R-2・R-3→T4、R-4→T5、R-5→T10、R-6〜R-8・R-15→T6、R-9→T7、R-10→T2（済）・T12、R-11・R-12→T8、R-13→T9、R-14→各タスクと T12、R-16→T11・T12
- 保留の経路: R-5 のラダー 2 は「無ければ要裁定」で無音にならない。R-6 の Web 側の未確認は CEF 不在の証拠付き。束 4 の原因は bd へ証拠付き
- 対照: R-3 は対照（旗・ラッチ無しで閉じる）と本番の条件（閉じない）を同じテストで並べる。R-4 の W は抑止の後に対照（動く）を置く
- 型の一致: `RecordingHotbarCommands.Calls`・`RecordingBuildMenuCommands.Selected`・`BuildMenuSamples.WoodChest`・`Entry(kind, id)`・`UITransitContext.NextStateEnum`・`UiLayerStack.Build`/`GetLayer` は実物と一致（起草時と評価役が読んだ）

## 判断記録（ADR）

従う裁定: `.decisions/2026-11-06-*`（UITK 6 本と CEF 停止）。依頼文の指示: Codex を使わない・Mac Editor 1 本・計画から実装まで同じセッション・問いは agent 既定・worktree の片付けは指揮。

- D-1（agent 既定）: §5 の独立評価は opus の subagent 2 体（Codex の代わり）。計画は設計検査 1 体・独立評価 2 体・user-simulator（Fable）に通し、指摘を全部反映した（独立評価の High 6 件・Medium 10 件、設計検査の違反 6 件、user-simulator の適用推奨 2 件）。問いは agent 既定へ畳む（依頼文の指示）
- D-2（agent 既定）: 束 3 の暗転の起点は S3-buildmenu の分岐点で取り込み済みの master `0e81bbe3c3`。L0-s3 は純ロジックで束の画面を作っていない
- D-3（agent 既定・user-simulator の予測と同じ）: ラダー 2 `uitk-opening-hud` はこのレーンでは作らない（S2-ladder の持ち物・ファイル所有を重ねない）。最終 HEAD に無ければ完了条件「1〜3 が緑」を 1・3 で締めてよいかを `done.md`「要裁定」に書き、マージは進める（裁定「レーンは agent が feature/uitk へマージ」と、S4-gamma が S2-ladder を待つ DAG で欠けが後段で必ず検出されるため）
- D-4（agent 既定・設計検査 V-1〜V-3）: L1 は主役の画面 `Screens/BuildMenu/` へ（前例 `Screens/Inventory/EquipmentWheelPassthroughTest.cs`）、UIState の L1 は `UIState/BuildMenu/`（前例 `UIState/PauseMenu/`）、新シナリオは `scenarios/uitk/stage3/`（前例 S4-gamma D-24）。既存の 10 超のディレクトリへは新しいファイルを足さない
- D-5（agent 既定）: 検索欄の受入は画面側（`GameUiInputRules`・旗・ESC の消費）と UIState 側（旗と消費で閉じない）の 2 本の L1 に分ける。閉じる判定は UIState にあり、画面側だけでは受入を言えない。ESC は EditMode で InputSystem の押下が `CloseUI` に届かないので `SetKeyDownForTest` で差し込む（前例 `UiInputExclusivityEscapeLatchTest`）
- D-6（agent 既定・設計検査 V-4）: 電柱のホイールは `ManualUpdate` から読む駆動を変えず、Uniform の設定（1 ノッチ＝1.0）に合わせて `/100f` を外す。`WebWheelScale`（Client.Ui の 60px 換算）は UI の Web 互換の換算で、Client.Game の電柱の選択には持ち込まない。トラックパッドの感度は実測を書く。Task 7 の実測: `SemanticInput.MouseWheel(1f)` 1 回で `ReadScroll` に raw=1・渡す値 0.01（非 0 は 1 フレーム）が届き、`/100f` を外して 1 ノッチ＝1 段に戻した（b6d4312b0c）。トラックパッドの細かい量は `ScrollStepAccumulator` に蓄積され続けるが、1 段に要る量は従来の 100 分の 1（合計 1.0）になる。実マウスの 1 ノッチとトラックパッドの実量は未実測
- D-7（agent 既定・設計検査 V-5）: 装備のホイール（束 4）は直さず bd で回す（同じ integrator の前例 S4-gamma の「不具合は持ち主へ回す」）
- D-8（agent 既定）: §4.3 の 5（`moores-wt rm`）は依頼文の「worktree の片付けは指揮が行う」に従い、このレーンは Editor を閉じるまで
- D-9（agent 既定・評価役 H1/H2）: 並べ比べは 720p の古い参照を使わず撮り直す。色・寸法は EditMode の組 1（参照の地の 1 色・Web fixture 相当）で判定し、PlayMode の組 2 は崩れの確認だけ（前例 `uitk-notification-compare.cs` の 2 本立て・設計書 §3.3）
- D-10（agent 既定・評価役 L4）: place-like-block の ConnectionLimit は仕様（燃料機械は電線口 0）で、シナリオを電気機械へ替える
- D-11（agent 既定・Task 7 実測）: pole-cycle-rotate の装備のホイールの対照は、ピン先マスタの装備枠 1（moorestech_master 6e6046b で 3→1）で切替先が無く成り立たない（製品の不具合ではないので bd は立てない）。対照を「電柱ツールがホイールを占有する／抜けると外れる」に置き換え、装備の切替は枠 2 以上のときだけ評価し、枠 1 では Note に「未検査」と出す
- D-12（agent 既定・moores-code-review D1 案 A）: 電柱の種類のホイールは 1 フレームに進む段数を ±1 に Clamp（トラックパッドの慣性の大きな量で多段に回るのを抑える。慣性が何フレームも続く尾では段が進みうる・1 フレーム 2 ノッチ以上の速回しは超過分を捨てる）
- D-13（agent 既定・Task 9 実測）: 並べ比べで見つかった台帳に無い差 2 件は LX-170（2560×1080 のモーダルの 1px の線のにじみ。箱の丸め）・LX-171（和文の題の太さ。A-36 の改めを integrator へ依頼する暫定記帳）
- unityプレイ録画テスト: 含む（Task 5・6・7・9・10）

## 設計書・実行計画へ反映する行（integrator への依頼の案。このレーンは書かない）

- A-210: 段階 3 の横断の L1（パネル越しの D&D は `Screens/BuildMenu/BuildMenuToHotbarDragTest`・検索欄の受入は `Screens/BuildMenu/BuildMenuSearchInputGuardTest` と `UIState/BuildMenu/BuildMenuStateTextInputGuardTest`）
- A-211: ラダー 3 `uitk-build-bp` の置き場 `scenarios/uitk/stage3/` と中身。S4-gamma 計画の `scenarios/uitk/stage3/uitk-build-bp.cs` を読み替える
- A-212: 電柱のホイールの換算（Task 7 の結果で書く）
- 実行計画 §1.4 の S3-ladder 行の「codex-audit」を「独立評価（opus 2 体）」に読み替えた記録

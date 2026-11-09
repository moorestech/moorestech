# UITK S4-gamma（段階 4 の締め: 同時に出る組の L3・ラダー 4/5・全録画の 2 回目・アンカー 9 件・CPU 時間・束 4 の突き合わせ）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** S4 の 7 レーン（alpha・inventory・machine・train・research・recipe・special）が `feature/uitk` に入った後、全体スイッチ UITK で「持ち物・研究・機械が同時に出る組」が実ゲームで正しい列に 1 つずつ出て覆い合わないことを L3 で固定し、ラダー `uitk-production-loop`・`uitk-research-train` を作ってラダー 1〜4 を緑にし、全録画（全本の 2 回目）を UITK 側と Web 側で流して分類し、マスタのアンカー 9 件の実ゲームでの解決・`UIElementsUpdatePanels`＋`UIElementsRepaintPanels` の CPU 時間・束 4 の突き合わせ差分 0・G1 未記帳 0・波の並べ比べの codex-audit を台帳と `done.md` に残して `feature/uitk` へ取り込む。

**Architecture:** integrator のレーン（`int`。Editor 要）。新しく書くのは「検査する側」だけで、画面・部品・Foundation・State のコードは書かない。L3 は `Client.Tests/EditModeInPlayingTest/Ui/Stage4/` に、面の見え方を読む純粋な関数 `Stage4ScreenSetProbe` と期待の表 `Stage4ExpectedSurfaces` を置き、`Stage4ScreenSetGameUiTest` が本番の遷移（`UIStateControl.RequestTransition`・F キーの注入）で画面を巡って照合する。ラダー・アンカー・CPU・撮影は DSL のシナリオ（`scenarios/uitk/`）で、列車線の準備だけ `Client.Playtest/Operations/Ladder/PlaytestTrainLine` に切り出す（シナリオを 200 行未満に保つため）。全録画は `tools/uitk/run-all-recordings.sh` が 1 本ずつ `run-scenario.sh` を回して TSV に残し、分類と持ち主への回送は人手（このレーンの subagent）が `done.md` に書く。不具合は直さず、持ち主のレーン（取り込み済みなら I-fix）へ bd と依頼文で回して待つ。

**Tech Stack:** Unity 6 / UI Toolkit（`Client.Ui`・`Client.Ui.State`）、NUnit（EditModeInPlayingTest）、プレイテスト DSL（`Client.Playtest`・`.agents/skills/unity-playmode-recorded-playtest`）、`Unity.Profiling.ProfilerRecorder`、Node（`tools/uitk/test-name-parity.mjs`）、Python（`.github/scripts/check-uitk-web-follow.py`）、bash、codex-audit（gpt-6-astra）、bd。

設計の出所: 実行計画 `docs/uitk-migration/execution/execution-plan.md` §1.4 の S4-gamma 行（完了条件の原文:「同時に出る組（playerInventory・subInventory・researchTree）の L3、ラダー `uitk-production-loop`・`uitk-research-train` を作りラダー 1〜4 緑、UITK 側で全録画（全本の 2 回目）、マスタのアンカー 9 件が実ゲームで解決、`UIElementsUpdatePanels`＋`RepaintPanels` を台帳に 1 回記録、束 4 の突き合わせ差分 0・G1 0・codex-audit」、依存「S4-* 全部」）、統合計画 `docs/uitk-migration/plan.md` §2.0・§2.5・§4.4・§4.5・§4.7、設計書 `docs/uitk-migration/execution/component-architecture.md` §3.3・§3.4。

## Requirements

| # | 要件 | 受け入れ基準 |
|---|---|---|
| R-1 | 同時に出る組の L3 | `Stage4ScreenSetGameUiTest` が全体スイッチ UITK の実ゲームで GameScreen → PlayerInventory → GameScreen → ResearchTree → GameScreen → SubInventory（`釜`を F で開く）→ GameScreen → PlayerInventory を巡り、各画面で「出るべき面だけが見え（表の通り）・各面が期待の列の子孫にあり・同名の面が 1 つだけで・面の中心を `panel.Pick` すると自分か子孫が拾われる」こと、ResearchTree で `research-tree` の左端が `inventory-panel` の右端以上で、左余白を畳むクラス `stage-columns--left-padding-collapsed` が付くこと、巡る間の LogError と例外が 0 件であることを PASS |
| R-2 | ラダー 4 `uitk-production-loop` を作る | 全体スイッチ UITK・Web ホスト無しで、Tab → アイテム一覧で木の板を選ぶ → クラフトを長押しして木の板が 2 枚以上増える → 石窯を置き F で開く → 原木を掴んで入力枠へ置く・Shift＋クリックで青銅鉱石の粉を入れる・右ドラッグで手持ちの空き枠へ配る → 開いたまま青銅インゴットが出力枠に現れる（機械 UI を開いたまま中身が更新される録画。§2.5）→ Shift で取り出す → 整理、が `result.json` `Success: true`。録画 mp4 がある |
| R-3 | ラダー 5 `uitk-research-train` を作る | 全体スイッチ UITK で、研究素材を渡す → 研究ツリーを開き `原始研究1` のノード → 研究ボタンで完了（サーバーの完了を待つ）→ 駅 2 基の線を準備 → 駅を F で開き駅名を日本語で `SetText`・確定し、サーバーの駅名が変わる → 機関車を開き時刻表に 2 駅を足し（サーバー 2 件）・自動運転を入れて走り出す、が `Success: true` |
| R-4 | ラダー 1〜4 が UITK で緑 | `uitk-lifeline`・`uitk-opening-hud`・`uitk-build-bp`・`uitk-production-loop` の 4 本が同じ HEAD の全体スイッチ UITK で `Success: true`。`uitk-research-train` も緑（§2.5 の「ラダー 2 本」） |
| R-5 | 全録画の 2 回目 | `scenarios/` の全本を UITK 側で流し、1 本 1 行の結果（exit・秒・run dir）と分類（PASS／束 5 未移植／wwxy／前提つき／Web 専用／不具合）を `done.md` と台帳「## 実測の結論」に残す。不具合 0 件（見つかったものは持ち主の修正が入って再走で PASS）。Web 側も全本を流し、段階の前と同じ合否（S4 前のコミットとの差分 0。落ちたものは既存か退行かを分けて記録） |
| R-6 | マスタのアンカー 9 件が実ゲームで解決 | `uitk-master-anchors` がピン先マスタの `uiHighLight` を集めて 9 件であることを確かめ、各アンカーをその画面で `TutorialAnchorRegistry.Resolve` が `Unique` で引け、`AddOutlineHighlight` の枠（`tutorial-outline`）がアンカーの中心から 5px 以内に出て、`CompleteTutorial` で消えることを PASS。`recipe.item-<itemId>` も 1 件見る |
| R-7 | CPU 時間を台帳に 1 回記録 | `uitk-stage4-panel-cost` が GameScreen・PlayerInventory・ResearchTree の各 240 フレームで `UIElementsUpdatePanels`＋`UIElementsRepaintPanels` の平均・p95・最大を Note に出し、台帳「## 実測の結論」へ 1 行（HEAD・Editor/プレイヤーの別・解像度・値・目安 1ms 以下か）を書く。1ms を超えたら要裁定ではなく「未確認」と bd に記録（目安であって合否ではない。plan §2.5） |
| R-8 | 束 4 の突き合わせ差分 0 | `node tools/uitk/test-name-parity.mjs --bundle 4` が終了コード 0。持ち主の無い `e2e/mock-host/detailActions.test.ts` 3 件に処遇の行を足し、`docs/uitk/disposition.md`「## 突き合わせの記録」に 1 行足す |
| R-9 | G1 0 | 束 4 の状態が `暗転`・起点あり、`check-uitk-web-follow.py --base origin/master --head HEAD` の出力の「束 4（暗転・起点 …）: 未記帳 0 件」。PR の `UITK Web follow gate (G1)` が `pass` |
| R-10 | 波の並べ比べと codex-audit | S4 の各レーンが報告ページに出した Web／UITK の組と、gamma が撮る合成の組（同時に出る 3 画面 × 1280×720・2560×1080）を codex-audit（画像付き）にかけ、指摘を台帳の LX 行と照合する。再現できた指摘だけを持ち主（I-fix）へ回し、記帳が要るものは LX-141〜145 の文面を `done.md` に書いて渡す |
| R-11 | 回ってきた件を拾う | 下の「束ねる完了条件と回ってきた件」の表の各行に、処理（済み／I-fix へ回した bd／据え置きと帰結）を `done.md`「## 回ってきた件」で 1 行ずつ付ける。C4-e の残り（D-36 など）は Task 5 の観察で再判定する |
| R-12 | 設計書・実行計画へ反映する行 | 末尾の「## 設計書・実行計画へ反映する行」（A-175〜A-179、§1.4 の改め案、§6.1 の行）を指揮が写せる形で置く |
| R-13 | 最終の対 | moores-code-review → pr-create（`base=feature/uitk`、報告ページ）→ `origin/feature/uitk` の取り込みと compile・テスト → G1 緑で `gh pr merge --merge` |

### 束ねる完了条件と回ってきた件

各レーンの計画（`docs/superpowers/plans/2026-11-10-uitk-s4-<lane>.md`。alpha・inventory・machine・train は PLAN-W4a の worktree で起草、recipe・research・special はこの worktree）から写した。train の計画は起草中（172 行時点）で、「当該計画のマージ後に照合」とした行は Task 1 Step 3 でマージ済みの `done.md` から埋める。

| レーン | 完了条件（計画から） | gamma へ回した件・gamma が拾う件 | gamma での扱い |
|---|---|---|---|
| S4-alpha | ブロック UI の汎用族の UITK 化（`block-inventory`・`block-inventory-panel`・`block-inventory-close`・`<testId>-slot-<index>`）、SubInventory で `StageColumn.Viewer` 列、SlotCommands（`Client.Ui.State/SlotCommands/`） | integrator への依頼 (a) `Registration/GameUi/` のサブディレクトリ化、(b) `GamePanel` の題の右の逃げ 99px、(c) `SlotCommands` を設計書 §2 の State の表へ（A-142 の下書き） | (a) Task 1 Step 4 で本数を数える。10 を超えていれば bd を立て I-fix へ回す（gamma は他レーンの登録ファイルを動かさない。D-4）。(b) Task 13 の比べ方で special のロケットの差として照合し、未処理なら LX の文面を書く。(c) 設計書の行の有無だけ確かめる |
| S4-inventory | InventoryPanel・EquipmentPanel（`inventory-panel`・`inventory-sort`・`main-slot-<i>`・`equipment-slots`）、アンカーの印付け | 依頼 `TutorialAnchor.Unmark`（D-8）・`GameUiPointerProbe.PickAt`（D-11） | Foundation の口なので gamma は作らない。Task 11 で状態を確かめ、未処理なら bd を I-fix へ（無くても動く形で書かれているので段階 4 の完了は妨げない。帰結: 画面側の写しが 2 か所残る） |
| S4-machine | 機械の詳細・清浄室・流体枠・レシピ選択（`MachineRecipeSelectionRow` 等） | 依頼: `Client.Ui.State.asmdef` の参照、撮影表への 15 枚の追加（E-22 の自撮り） | asmdef は取り込み済みかを見るだけ。撮影表への追加は integrator の共有区画なので Task 11 で `docs/uitk/TODO.md`「## 参照キャプチャ」に自撮りの置き場を 1 行で記す |
| S4-train | 駅名（`train-station-name-input`・`-apply`）・時刻表（`train-tab-timetable`・`train-timetable-station-{x}_{y}_{z}-add`・`train-timetable-stop-{i}`・`train-timetable-auto-run-on`）。適用ボタンを消して即送信 | 当該計画のマージ後に照合（起草中） | Task 1 Step 3 で `done.md` の「gamma へ」「integrator への依頼」を読み、この表に足す |
| S4-research | 研究ツリー（`research-tree`・`research-panel`・`research-viewport`・`research-node-<guid>`・`research-button-<guid>`・`research-detail-pane`）、録画 `uitk-research-tree`・`tier9-5` | L3 は「S4-ladder」＝gamma。D-9 `NodeGraphView` の `MarkClipContainer` は integrator への依頼。tier9-5 が Web 前提の操作で落ちたら既存シナリオの横断は gamma。詳細ペインの自撮りの参照の台帳記載 | L3 は Task 3。`MarkClipContainer` は Task 11 で状態を確かめ未処理なら bd。tier9-5 は Task 9 の全録画で分類（不具合なら research か I-fix へ）。参照の記載は Task 11 |
| S4-recipe | ビューア・アイテム一覧（`recipe-viewer`・`item-list-panel`・`item-list-scroll`・`item-list-cell-<id>`・`craft-button-<guid>`・`craft-recipe-box-<guid>`・`recipe-entry-list`）、長押しクラフト、アンカー `recipe.craft-button`・`recipe.item-<itemId>` | D-20: ビューア＋機械 UI＋インベントリの L3 は gamma。D-9 `GameScrollView` のつまみ 5 本を設計書へ。自撮りの参照の台帳記載。部分移植の残り（`MachineRecipeEntry` の alt・`slotTooltip`） | L3 は Task 3（同時には出ないので、画面を行き来して残骸が出ないことで見る。D-3）。つまみは末尾の「設計書へ反映する行」に 1 行。参照は Task 11。部分移植の残りは Task 10 の parity で処遇の表にあるかを見る |
| S4-special | 特殊族（電柱・歯車変換・フィルター分配・ロケット・宇宙エレベーター・ベルト） | D-19: L3 は gamma。D-23（alpha の「出し入れをやり直す口」）・D-26（パネルの InstanceId）は integrator への依頼。部分移植の残り（filterSplitter の 3 件目など） | 特殊族は同時に出る組の L3 の対象外（SubInventory の 1 例として `釜` を見る。D-3）。D-23・D-26 は Task 11 で状態を確かめ未処理なら bd。部分移植の残りは Task 10 |
| C4-e（申し送り） | 掴み・分割ドラッグの State | D-5（Web ホストと State が持ち物の規則を二重に持つ。撤去まで据え置き）、D-8 `IPlayerSlotDragCommands`、D-29（LateTick で確定）、D-36（`BeginSplitDrag` の先行確定で計画の snapshot がずれうる。alpha の着地で表に出る）、D-37（マウスのリセットで session を捨てる）、bug-pass の Warning (a)〜(e)、`DiscardStaleSession`、outbox の箱、albof.40、掴み絵の撮影 id | D-36・Warning (a)(b)(e) は Task 5 の右ドラッグ配りと連続の分割で観察し、`done.md`「## 回ってきた件」に「再現した／しなかった（証拠）」で書く。再現したら bd を I-fix へ。D-5 は据え置き（帰結: 撤去 5c まで規則が 2 か所。X レーンで Web 側ごと消える）。albof.40・撮影 id は状態を確かめるだけ |
| conductor.md | Editor は同時 2 本、winunity が使える、CI `client-remainder` の `UnityCursorTooltipScreenTest` 1 件は既存の失敗 | — | Global Constraints に写した |

### マスタのアンカー 9 件（ピン先 `moorestech_master` e0c913033c の `challenges.json` の `uiHighLight`。実物で確かめた）

| # | チャレンジ（GUID の先頭） | 名前 | anchorId | 開く画面 |
|---|---|---|---|---|
| 1 | fb529cac | 原木を3個入手する | `challenge.current-hud` | GameScreen |
| 2 | 90a98c1f | 木の板を3枚作る | `recipe.craft-button` | PlayerInventory（木の板を選んだ後） |
| 3 | 7b9ddaf3 | 原始研究1 | `research.node-858bcb10-b8ba-478e-9bc5-473ca61281a2` | ResearchTree |
| 4 | 2eab252f | 原始研究2 | `research.node-b47c5e3c-1b58-42c5-a477-d485d2eae747` | ResearchTree |
| 5 | 2c65c32d | 原始研究3 | `research.node-bc5e7786-6759-4271-8095-836703b54490` | ResearchTree |
| 6 | 69fb3e56 | 原始研究4 | `research.node-0d76f2e5-be1c-4ad4-b460-97a8aad0495f` | ResearchTree |
| 7 | 76d33968 | 原始研究5 | `research.node-48f75a7e-36f3-4845-a0bc-f8de8b3d7baf` | ResearchTree |
| 8 | 5b4319eb | 原始研究6 | `research.node-3bca3b97-14d7-4cc1-a661-2266670bb6cb` | ResearchTree |
| 9 | d086eb33 | 木材の組み立て | `research.node-50372888-cd15-47c6-9986-adf50548d156` | ResearchTree |

チャレンジ 2 は同じチャレンジに `itemViewHighLight`（41e0a6e4）も持つが、これは `uiHighLight` ではないので 9 件に入れない。ピンが変わっていたら Task 7 のシナリオがマスタから集め直すので、表は Task 7 Step 3 の Note と照らして `done.md` に写す。

### 束 4 の突き合わせ（2026-11-10 起草時に `node tools/uitk/test-name-parity.mjs --bundle 4` を流した実測）

Web 457 件／写した 189／処遇 9／写す予定 1（albof.29、itemSlot のツールチップ、S4-inventory）／未対応 258。未対応はすべて S4 の各レーンの計画の持ち分で、レーンのマージで消える見込み。持ち主の無いものは 1 ファイルだけ。

| 持ち主 | 未対応のファイル（件数） |
|---|---|
| S4-alpha | `blockInventory.spec` 3・`blockInventoryGestures` 5・`blockRegistryCoverage` 1・`regression/sectionStack` 1・`SectionStackView.test` 8・`blockInventoryDesign` 16・`BlockInventoryPanel.localization` 2・`LackHighlightText` 2 |
| S4-inventory | `inventory.spec` 4・`equipment.spec` 11・`InventoryPanel/index.test` 2・`EquipmentPanel/index.test` 14 |
| S4-machine | `blockDetails` 8・`machineGestures` 3・`machineRecipe` 10・`machineRequiredCount` 4・`cleanRoom/*` 8・`fluidSlot` 5・`MachineSection` 9・`SelectedRecipeHeader` 2・`ModuleSlotStackLimitText` 2・`CleanRoomSection` 4・`CleanRoomYield` 3・`MachineInputStall` 3・`MachineRecipeSelectionRow` 5 |
| S4-special | `electricPole` 1・`electricToGear` 1・`filterSplitter` 3・`spaceElevator/*` 8・`GearToElectric` 7＋i18n 1・`BeltThroughput` 3・`Gear` 3・`RocketLaunchPad` 6・`SpaceElevator*` 5 |
| S4-train | `trainPlatform` 2・`train.spec` 1・`train/timetable` 2・`TrainInventoryBody` 1・`TrainTimetableImmediate` 8・`TrainTimetableSection` 5・`TrainPlatformInventory` 1 |
| S4-recipe | `recipe.spec` 9・`itemListScroll` 6・`recipeListScroll` 2・`slotTooltip` 2・`useDragScroll` 2・`CraftRecipeEntry` 3・`MachineRecipeEntry.fluids` 1・`MachineRecipeEntry.localization` 1・`RecipeContent` 2 |
| S4-research | `research.spec` 12・`researchMachineUpgradeUnlock` 1・`researchViewport` 5・`tmp-icon-click` 1（research が「捨てる」）・`ResearchDetailPane` 11・`ResearchNodeCard` 2・`ResearchTreePanel` 2 |
| **持ち主が無い（gamma）** | `e2e/mock-host/detailActions.test.ts` 3 件 → Task 10 で処遇「捨てる」（mock-host の詳細操作の結線は Web の検証基盤で、UITK の写し先が無い。理由を行に書く） |

### やらないこと

- 画面・部品・Foundation・State・トークン USS・登録ファイルの中身を書き換えない。不具合と不足は持ち主か I-fix へ回す（Task 9・11 の手順）
- `.cs` を書くのは新規 6 ファイル（L3 の 3 本・`PlaytestTrainLine`・`PlaytestPanelCostRecorder`・`PlaytestAnchorChecks`）だけ。既存の `.cs` は変えない
- 壊れている 10 本（bd moorestech-wwxy。束 4 の `misc/research-node-state-label.cs`・`tutorial-research-renumber.cs` は X-prerun）を直さない。全録画では「wwxy」に分類する
- ラダー 6 `uitk-challenge-gate` と束 5 の画面（チャレンジ一覧・言語ゲート）を作らない
- 束 4 を `切替済み` にしない（切替 PR で全束一度）
- 録画・Unity のログ・テスト結果 xml を `moorestech_logs` に置かない（worktree の gitignore 下に置き、`done.md` に要約だけ）

## Global Constraints

- 土台: `origin/feature/uitk`（起草時 0a94c9b96b）。S4 の 7 レーン・S2-tutorial（PR #1627）・S2-ladder・S3-ladder が入っていることが着手の前提（Task 1 Step 1）。worktree は `moores-wt new uitk-lane/s4-gamma --from origin/feature/uitk`（環境ローカル規約）
- Editor: Mac mini で同時 2 本まで（conductor.md）。自分の worktree の Editor だけを使い、起動・停止前に `moores-wt status` で所有者を確かめる。Web 側の全録画は Windows の `~/bin/winunity`（compile・test・play を ssh で回す）でも流してよい
- 全体スイッチ: PlayMode を止めてから `uloop execute-dynamic-code --project-path ./moorestech_client --code 'Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi);'`（Web 側は `UiSystem.Web`）。値は起動ごとに 1 回読まれ、Editor を閉じると Web に戻る
- 受け口: 録画の前に Editor プロセスへ `MOORESTECH_PLAYTEST_RECEIVER_BASE=http://127.0.0.1:9` を立てる（`uitk-lifeline` の冒頭の検査がこれを要求する。本番の受け口へ送らない）
- シナリオの起動: `.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh <project> <scenario.cs> [master-dir]`。終了コード 0 成功／1 失敗／2 コンパイル不可。出力に `run dir: <path>`。失敗すると PlayMode が残るので、次の起動の前に `uloop control-play-mode --project-path ./moorestech_client --action Stop`
- テスト: `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "<完全名>"`。L3 は PlayMode 遷移でドメインリロードを起こすので、直後の「Unity is reloading」は 45 秒待って再試行
- AGENTS.md の規約: 1 ファイル 200 行未満、1 ディレクトリ 10 ファイルまで、partial 禁止、`Func<>` を自分の型の引数・フィールドに使わない（DSL の既存 API `p.Until(Func<bool>, …)` へラムダを渡すのは既存の呼び出しで、新しい型は作らない）、イベントは UniRx、try-catch 禁止（`finally` で購読を外すのは可）、日英 2 行コメント、デフォルト引数禁止、初期化は `Initialize`、fail-closed はログへ、単純な getter/setter プロパティ禁止
- テスト名は日本語の文。L3 は `[Category("CiShardClientPlay1")]`、`UiSystemPlayModeTestBase` を継承し `GameUiSkeletonTest` と同じ遷移（`SelectUiSystemBeforePlayMode(UiSystem.GameUi)` → `EnterPlayModeUtil()` → `yield return new EnterPlayMode(expectDomainReload: true)` → `LogAssert.ignoreFailingMessages = true` → 本体 → `ExitPlayMode` → `SessionState.SetBool("DebugObjectsBootstrap_Disabled", false)`）
- 他レーンのファイルを書かない。gamma が書く共有区画は integrator の持ち分のうち `docs/uitk/TODO.md`（束の表の束 4 の行・「## 追従記録」の束 4 の行・「## 実測の結論」・「## 参照キャプチャ」の追記・「## 見た目の例外」の LX-141〜145 を書くのは I-fix）と `docs/uitk/disposition.md`（処遇の表の行と「## 突き合わせの記録」）だけ
- 番号: 設計書 §7.1 は A-175〜A-179、台帳は LX-141〜LX-145 だけを使う
- 報告: `../moorestech_logs/harness/uitk/S4-gamma/done.md`（1 行目は「状態: 進行中／停止／完了」）。`moorestech_logs` で作業する前に同 repo の `README.md` を読む
- CI の `Unity Test - client-remainder` で落ちる `UnityCursorTooltipScreenTest` 1 件は既存の失敗。マージの条件は G1 緑と、それ以外の必須ジョブの緑
- 新しい共有の申告: 検査用の口（`PlaytestTrainLine`・`PlaytestPanelCostRecorder`・`PlaytestAnchorChecks`）は DSL の Operations の新しいサブディレクトリ `Ladder/` に置き、既存の Operations のファイルは変えない。既存の `PlaytestDriver` に足したくなったら足さずに `done.md`「integrator への依頼」へ書く（`PlaytestDriver` は 199 行。A-111 の前例）

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 未実施
- Phase 2.6（型閉包・重複・ADR矛盾）: 未実施

---

## 死活表（Phase 2.5）

gamma は検査する側のファイルだけを足し、本番のコードに触れない。触る機構にぶら下がる操作は次の通りで、どれも死なない。

| 操作 | 計画後も生きるか | 根拠 |
|---|---|---|
| 全体スイッチ Web での全画面 | 生きる | 本番コードを変えない。Web 側の全録画（Task 9 Step 4）が段階の前と同じ合否を確かめる |
| 全体スイッチ UITK での全画面 | 生きる | 同上。L3・ラダー・全録画で確かめる側 |
| 既存の録画シナリオ 114 本 | 生きる | 既存のシナリオを書き換えない（新規 5 本だけ）。DSL の既存 Operations も変えない |
| `docs/uitk/TODO.md` を読む G1 | 生きる | 束 4 の行の状態・起点と追従記録の行を足すだけ。書式は「## 読み方と運用」に従い、`check-uitk-web-follow.py` をローカルで回して確かめる（Task 2） |
| `test-name-parity.mjs` を読む L2 | 生きる | 処遇の表に行を足すだけ。終了コード 2（書式の誤り）でないことを Task 10 で確かめる |

## File Structure

| パス | 新規/変更 | 責務 |
|---|---|---|
| `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/Stage4/Stage4ScreenSetGameUiTest.cs` | 新規 | L3。本番の遷移で 4 画面を巡り、各画面で `Stage4ScreenSetProbe` の照合を呼ぶ。Error ログを数える |
| `…/Ui/Stage4/Stage4ScreenSetProbe.cs` | 新規 | 根の木から面を名前で引き、見えているか・どの列の子孫か・同名の数・中心の Pick を返す純粋な関数 |
| `…/Ui/Stage4/Stage4ExpectedSurfaces.cs` | 新規 | 画面ごとに見えるべき面と、面ごとの列の名前（Web の `App.tsx` と `UiScreenTraitsTable` から写した表） |
| `moorestech_client/Assets/Scripts/Client.Playtest/Operations/Ladder/PlaytestTrainLine.cs` | 新規 | 駅 2 基・結線・機関車の解放と設置・燃料・列車の取得（既存 `train-timetable-via-ui.cs` の準備の写し。駅名は付けない） |
| `…/Operations/Ladder/PlaytestPanelCostRecorder.cs` | 新規 | `ProfilerRecorder` で UI のマーカーを N フレーム読み、平均・p95・最大を返す |
| `…/Operations/Ladder/PlaytestAnchorChecks.cs` | 新規 | マスタの `uiHighLight` を集め、アンカーの解決と枠の位置を確かめる |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-production-loop.cs` | 新規 | ラダー 4 |
| `…/scenarios/uitk/uitk-research-train.cs` | 新規 | ラダー 5 |
| `…/scenarios/uitk/uitk-master-anchors.cs` | 新規 | アンカー 9 件 |
| `…/scenarios/uitk/uitk-stage4-panel-cost.cs` | 新規 | CPU 時間（録画しない） |
| `…/scenarios/uitk/uitk-stage4-wave-shots.cs` | 新規 | 同時に出る 3 画面の撮影（録画しない。1280×720 と 2560×1080） |
| `tools/uitk/run-all-recordings.sh` | 新規 | 一覧のシナリオを順に `run-scenario.sh` で流し、1 本 1 行の TSV を書く |
| `tools/uitk/list-recordings.sh` | 新規 | 全本の一覧を作る（`scenarios/` の `.cs` から wwxy の 10 本を除く） |
| `docs/uitk/TODO.md` | 変更（共有区画） | 束 4 の行（暗転・起点）、「## 追従記録」の束 4 の行、「## 実測の結論」の 3 行（CPU・全録画 UITK・全録画 Web）、「## 参照キャプチャ」の自撮りの置き場 |
| `docs/uitk/disposition.md` | 変更（共有区画） | `detailActions.test.ts` 3 件の処遇の行、「## 突き合わせの記録」の 1 行 |

ディレクトリのファイル数: `Ui/Stage4/` 3、`Operations/Ladder/` 3、`scenarios/uitk/` は起草時 12 本＋S2〜S4 の各レーンが足す分＋この 5 本（シナリオは C# のアセンブリに入らないファイルで、10 ファイルの規則は `.cs` のコードのディレクトリに当てる前例どおり既存の 12 本が既に超えている。D-8）。`tools/uitk/` は Task 1 Step 4 で数え、10 を超えるなら `tools/uitk/recordings/` へ 2 本を置く。

**他レーンとの所有の境界**: L3 は他レーンの View・USS を読むだけ（名前とクラス）。シナリオは他レーンの testid を `ClickUi`・`UntilUiElement` で使うだけ。名前が計画と違っていたら Task 1 Step 2 の照合表で読み替え、gamma のファイルの側を直す（他レーンへ名前の変更を求めない。D-5）。

---

## Tasks

### Task 1: 前提の確認・照合表・記録の準備

**Files:**
- Create（repo 外）: `../moorestech_logs/harness/uitk/S4-gamma/done.md`

**Interfaces:**
- Consumes: `origin/feature/uitk` の HEAD
- Produces: `done.md` の骨組み、照合表（計画の名前 → 実物の名前）、`$MACHINE_BLOCK_PLAYTEST`（ピン先マスタの石窯の名前。既定 `石窯`）、bd の issue `<bd>`

- [ ] **Step 1: 前提のレーンが入っているかを確かめる**

Run:
```bash
pwd
git fetch origin
git log --oneline --merges origin/feature/uitk | grep -E "s4-(alpha|inventory|machine|train|research|recipe|special)|s2-tutorial|s2-ladder|s3-ladder" 
ls .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/ | grep -E "uitk-(lifeline|opening-hud|build-bp)\.cs"
test -f moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs && grep -c "S4-" moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs
```
Expected: マージコミットが 10 本（S4 の 7・S2-tutorial・S2-ladder・S3-ladder）、ラダーのシナリオ 3 本がある、`TutorialAnchorCoveragePending.cs` に `S4-` の持ち主の行が 0（S4 の各レーンが自分の行を消している）。どれかが欠けていたら `done.md` の 1 行目を「状態: 停止」にし、欠けたものを書いて指揮へ返す（S2-ladder・S3-ladder は実行計画の DAG で S4-gamma の依存に無いが、ラダー 1〜4 緑に要る。末尾の §1.4 の改め案）。Pending の行が残っていたら、その持ち主のレーンへの依頼として `done.md`「integrator への依頼」に書き、Task 7 はその行のアンカーを「持ち主の未処理」として扱う

- [ ] **Step 2: 計画の名前と実物の名前を照合する**

Run:
```bash
for n in inventory-panel inventory-sort equipment-slots recipe-viewer item-list-panel item-list-scroll craft-button- craft-recipe-box- research-tree research-panel research-viewport research-node- research-button- research-detail-pane block-inventory block-inventory-close train-station-name-input train-station-name-apply train-tab-timetable train-timetable-auto-run-on stage-column-inv stage-column-viewer stage-column-items stage-columns--left-padding-collapsed screen-backdrop tutorial-outline challenge-hud; do
  printf "%s\t%s\n" "$n" "$(git grep -l -- "\"$n" -- 'moorestech_client/Assets/Scripts/Client.Ui*' | head -2 | tr '\n' ' ')"
done
grep -rn "item-list-cell-\|main-slot-\|-slot-\"" moorestech_client/Assets/Scripts/Client.Ui/Screens | head
```
Expected: 各名前が 1 か所以上で見つかる。見つからない名前は実物の名前を探し、`done.md`「## 照合表」に「計画の名前 → 実物の名前（ファイル:行）」で書き、Task 3〜8 のコード片を実物の名前で写す（gamma のファイルの側を直す。D-5）。S4-train が適用ボタンを消していれば `train-station-name-apply` は無く、駅名は `SetText` の後に Enter か欄の外のクリックで送る形になる（S4-train の `done.md` の操作の記述に合わせて Task 6 の該当行を読み替える）

- [ ] **Step 3: 各レーンの `done.md` から回ってきた件を集める**

Run:
```bash
for l in S4-alpha S4-inventory S4-machine S4-train S4-research S4-recipe S4-special C4-e; do
  f=../moorestech_logs/harness/uitk/$l/done.md
  echo "=== $l $(head -1 $f 2>/dev/null)"
  grep -n -A12 "## integrator への依頼\|## 部分移植の残り\|gamma\|S4-ladder\|## 未確認" $f 2>/dev/null | head -60
done
```
Expected: 各レーンが「状態: 完了」。「束ねる完了条件と回ってきた件」の表に無い件があれば `done.md`「## 回ってきた件」に足す（S4-train の行はここで埋める）

- [ ] **Step 4: 数と前提の値を確かめる**

Run:
```bash
ls moorestech_client/Assets/Scripts/Client.Starter/Registration/GameUi/*.cs | wc -l
ls tools/uitk/ | wc -l
ls moorestech_client/Assets/Scripts/Client.Playtest/Operations/
grep -n "\"石窯\"\|\"蒸気機関車駅\"" -r ../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json | head -3
grep -n "ProfilerRecorder\|UIElementsUpdatePanels\|RepaintPanels" -r docs/uitk-migration/poc | head -5
```
Expected: 登録ファイルが 10 以下（超えていれば bd を立てて I-fix へ回す。gamma は動かさない。D-4）。`tools/uitk/` が 8 以下なら Task 9 の 2 本をそのまま置き、9 以上なら `tools/uitk/recordings/` に置いて以後のパスを読み替える。`Operations/` に `Ladder/` が無いこと。マスタに `石窯` と `蒸気機関車駅` がある（名前が違えば照合表へ）

- [ ] **Step 5: bd と done.md の骨組みを作る**

Run:
```bash
bd create "UITK S4-gamma: 段階 4 の締め（L3・ラダー 4/5・全録画・アンカー 9 件・CPU・束 4 parity・codex-audit）" --parent moorestech-albof -t task
bd update <bd> --claim
mkdir -p ../moorestech_logs/harness/uitk/S4-gamma
```
`done.md` を次の骨組みで作る:
```markdown
状態: 進行中
# S4-gamma
- bd: <bd>
- 土台: origin/feature/uitk <HEAD 10 桁>
## 照合表
## 回ってきた件
## 証拠
## 全録画の結果
## 差の表
## 要裁定
## integrator への依頼
## 未確認
```

- [ ] **Step 6: コミットは無し**（記録は repo 外）

---

### Task 2: 束 4 の暗転と G1

**Files:**
- Modify: `docs/uitk/TODO.md`（束の表の束 4 の行・「## 追従記録」の束 4 の行）

**Interfaces:**
- Consumes: Task 1 の HEAD
- Produces: 束 4 の `暗転`・起点 `<起点 10 桁>`。G1 の出力「束 4（暗転・起点 …）: 未記帳 0 件」

- [ ] **Step 1: 束 4 の状態を読む**

Run: `grep -n "^| 4 |" docs/uitk/TODO.md | head -1`
Expected: S4-alpha が着手時に `暗転` と起点を書いていればその行を使い、Step 2 を飛ばす。`Web` のままなら Step 2 へ

- [ ] **Step 2: 起点を引いて暗転にする**

Run:
```bash
ALPHA_MERGE=$(git log --merges --format=%H origin/feature/uitk --grep "s4-alpha" | tail -1)
git merge-base "$ALPHA_MERGE^1" origin/master | cut -c1-10
```
Expected: 10 桁のコミット。束の表の束 4 の行を `| 4 | 持ち物の束（…） | 暗転 | <起点> | \`uitk-production-loop\`・\`uitk-research-train\` |` に書き換える（中身の列はそのまま）

- [ ] **Step 3: G1 をローカルで回して未記帳を記帳する**

Run: `python3 .github/scripts/check-uitk-web-follow.py --base origin/master --head HEAD`
Expected: 「束 4（暗転・起点 <起点>）: 未記帳 k 件」。k が 1 以上なら、出たコミットごとに `git show --stat <sha> -- moorestech_web/webui` を読み、UITK 側に同じ変更があれば `写した`（写し先のファイル）、Web の検証基盤や Web 専用の結線なら `捨てた`（理由）で「## 追従記録」に `| 4 | <sha 10 桁> | 写した | <写し先> |` の形で足す。UITK 側に写すべき変更がまだ無ければ、それは追従漏れの不具合なので持ち主の画面のレーン（取り込み済みなら I-fix）へ bd と依頼文で回し、記帳せずに待つ（追従は未移植の実装より先。台帳の運用）。足したら同じコマンドで「未記帳 0 件」を見る

- [ ] **Step 4: コミットする**

```bash
git add docs/uitk/TODO.md
git commit -m "docs(uitk): 束 4 を暗転にして追従を記帳する（S4-gamma）" -- docs/uitk/TODO.md
```

---

### Task 3: 同時に出る組の L3（`Ui/Stage4/`）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/Stage4/Stage4ExpectedSurfaces.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/Stage4/Stage4ScreenSetProbe.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/Stage4/Stage4ScreenSetGameUiTest.cs`

**Interfaces:**
- Consumes: `GameUiRoot.Instance.GetRootElement()`、`UIStateControl.RequestTransition(UIStateEnum)`・`CurrentState`、`InteractKeyBlockOpener.PlaceNearPlayerAndOpen(string)`・`CloseToGameScreen()`、`UiSystemPlayModeTestBase.SelectUiSystemBeforePlayMode(UiSystem)`、`EditModeInPlayingTestUtil.LoadMainGame()`・`EnterPlayModeUtil()`
- Produces: `Stage4ExpectedSurfaces.Expected(UIStateEnum)`（`IReadOnlyList<Stage4Surface>`）・`Stage4ExpectedSurfaces.All`、`Stage4Surface(string Name, string ColumnName)`、`Stage4ScreenSetProbe.IsVisible(VisualElement)`・`ColumnNameOf(VisualElement)`・`CountByName(VisualElement root, string name)`・`IsPickedAtCenter(VisualElement)`（Task 12 の撮影が名前の表を使う）

期待の表は Web の `App.tsx`（InventoryPanel は `screenAllowsGrab`、RecipeViewer と ItemListPanel は `playerInventory`、ResearchTreePanel は `researchTree`、BlockInventoryPanel は SubInventory、Equipment は `alwaysOnHud`）と `UiScreenTraitsTable`（PlayerInventory・SubInventory は grab・backdrop・alwaysOnHud、ResearchTree は grab・backdrop で alwaysOnHud なし、GameScreen は alwaysOnHud）から写す。列の名前の空文字は「どの列の子孫でもない」（研究エリアは `OverlayPanel`/`Stage` 層、装備は `Chrome`/`Viewport` 層、幕は幕の層）。

- [ ] **Step 1: 期待の表を書く**

```csharp
using System.Collections.Generic;
using Client.Game.InGame.UI.UIState;

namespace Client.Tests.EditModeInPlayingTest.Ui.Stage4
{
    // 面の名前と、その面が載るべき列の名前（列の外なら空）
    // A surface name and the stage column it must live in (empty when outside every column)
    public readonly struct Stage4Surface
    {
        public readonly string Name;
        public readonly string ColumnName;

        public Stage4Surface(string name, string columnName)
        {
            Name = name;
            ColumnName = columnName;
        }
    }

    // 画面ごとに見えるべき面の表（Web の App.tsx と UiScreenTraitsTable の写し）
    // Which surfaces each screen shows (copied from the web App.tsx and UiScreenTraitsTable)
    public static class Stage4ExpectedSurfaces
    {
        public static readonly Stage4Surface Inventory = new("inventory-panel", "stage-column-inv");
        public static readonly Stage4Surface RecipeViewer = new("recipe-viewer", "stage-column-viewer");
        public static readonly Stage4Surface ItemList = new("item-list-panel", "stage-column-items");
        public static readonly Stage4Surface BlockInventory = new("block-inventory", "stage-column-viewer");
        public static readonly Stage4Surface ResearchTree = new("research-tree", "");
        public static readonly Stage4Surface Equipment = new("equipment-slots", "");
        public static readonly Stage4Surface Backdrop = new("screen-backdrop", "");

        public static readonly IReadOnlyList<Stage4Surface> All = new[] { Inventory, RecipeViewer, ItemList, BlockInventory, ResearchTree, Equipment, Backdrop };

        public static IReadOnlyList<Stage4Surface> Expected(UIStateEnum state)
        {
            // 同時に出る組だけを持つ。他の画面は呼ばない
            // Holds only the co-shown sets; other screens are not asked
            return state switch
            {
                UIStateEnum.PlayerInventory => new[] { Inventory, RecipeViewer, ItemList, Equipment, Backdrop },
                UIStateEnum.SubInventory => new[] { Inventory, BlockInventory, Equipment, Backdrop },
                UIStateEnum.ResearchTree => new[] { Inventory, ResearchTree, Backdrop },
                UIStateEnum.GameScreen => new[] { Equipment },
                _ => throw new System.ArgumentOutOfRangeException(nameof(state), state, "S4 の同時に出る組の表に無い画面"),
            };
        }
    }
}
```
（`throw` はテストの表の外を呼んだプログラムの誤りで、外部境界の try-catch ではない）

- [ ] **Step 2: 面を読む関数を書く**

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.EditModeInPlayingTest.Ui.Stage4
{
    // 根の木から面の見え方と置き場を読む（状態を持たない）
    // Reads how and where a surface shows from the root tree (stateless)
    public static class Stage4ScreenSetProbe
    {
        private const string ColumnNamePrefix = "stage-column-";

        public static int CountByName(VisualElement root, string name)
        {
            return root.Query(name: name).ToList().Count;
        }

        public static bool IsVisible(VisualElement element)
        {
            // 自分と祖先のどれかが消えていれば見えない
            // Hidden when the element or any ancestor is hidden
            for (var current = element; current != null; current = current.parent)
            {
                var style = current.resolvedStyle;
                if (style.display == DisplayStyle.None || style.visibility == Visibility.Hidden || style.opacity <= 0f) return false;
            }
            var bound = element.worldBound;
            return 0f < bound.width && 0f < bound.height;
        }

        public static string ColumnNameOf(VisualElement element)
        {
            // 祖先の列の名前を返し、列の外なら空を返す
            // Return the ancestor column name, or empty outside every column
            for (var current = element.parent; current != null; current = current.parent)
            {
                if (current.name != null && current.name.StartsWith(ColumnNamePrefix)) return current.name;
            }
            return "";
        }

        public static bool IsPickedAtCenter(VisualElement element)
        {
            // 中心で拾われた要素が自分か子孫なら覆われていない
            // Not covered when the element picked at the center is itself or a descendant
            var picked = element.panel.Pick(element.worldBound.center);
            for (var current = picked; current != null; current = current.parent)
            {
                if (current == element) return true;
            }
            return false;
        }

        public static string DescribePicked(VisualElement element)
        {
            var picked = element.panel.Pick(element.worldBound.center);
            return picked == null ? "（何も拾わない）" : $"{picked.name}（{string.Join(" ", picked.GetClasses())}）";
        }
    }
}
```
`IsPickedAtCenter` は `pickingMode = Ignore` の面（`screen-backdrop`）には使わない（中心で下の面が拾われるのが正しい）。テストは幕を除いて呼ぶ。

- [ ] **Step 3: L3 を書く**

```csharp
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Tests.EditModeInPlayingTest.BlockInventory.Scene;
using Client.Ui.Foundation.Root;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Ui.Stage4
{
    /// <summary>
    /// 段階 4 の同時に出る組（持ち物・研究・機械）を実ゲームで巡る
    /// Walks the stage-4 co-shown sets (inventory, research, machine) in the real game
    /// </summary>
    [Category("CiShardClientPlay1")]
    public class Stage4ScreenSetGameUiTest : UiSystemPlayModeTestBase
    {
        private const string MachineBlockName = "釜";
        private const string LeftPaddingCollapsedClass = "stage-columns--left-padding-collapsed";

        [UnityTest]
        public IEnumerator 持ち物と研究と機械の画面を行き来しても各面が自分の列に1つだけ見え覆われない()
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
                var uiStateControl = ClientDIContext.DIContainer.DIContainerResolver.Resolve<UIStateControl>();
                var root = GameUiRoot.Instance.GetRootElement();
                var errors = new List<string>();
                Application.logMessageReceived += CollectErrors;

                // 持ち物 → 研究 → 機械 → 持ち物の順に本番の遷移で巡る
                // Walk inventory -> research -> machine -> inventory through production transitions
                await TransitionTo(uiStateControl, UIStateEnum.PlayerInventory);
                AssertScreenSet(root, UIStateEnum.PlayerInventory);
                await TransitionTo(uiStateControl, UIStateEnum.GameScreen);
                AssertScreenSet(root, UIStateEnum.GameScreen);
                await TransitionTo(uiStateControl, UIStateEnum.ResearchTree);
                AssertScreenSet(root, UIStateEnum.ResearchTree);
                AssertResearchTreeRightOfInventory(root);
                await TransitionTo(uiStateControl, UIStateEnum.GameScreen);

                // 機械は F キーの注入で開き、閉じた後の持ち物に残骸が無いことを見る
                // Open the machine by injected F and check the inventory has no leftovers after closing
                await InteractKeyBlockOpener.PlaceNearPlayerAndOpen(MachineBlockName);
                await UniTask.DelayFrame(3);
                AssertScreenSet(root, UIStateEnum.SubInventory);
                await InteractKeyBlockOpener.CloseToGameScreen();
                await TransitionTo(uiStateControl, UIStateEnum.PlayerInventory);
                AssertScreenSet(root, UIStateEnum.PlayerInventory);

                Application.logMessageReceived -= CollectErrors;
                Assert.IsEmpty(errors, $"巡る間に Error・例外のログが出た:\n{string.Join("\n", errors)}");

                void CollectErrors(string condition, string stackTrace, LogType type)
                {
                    if (type == LogType.Error || type == LogType.Exception) errors.Add($"[{type}] {condition}");
                }
            }

            #endregion
        }

        private static void AssertScreenSet(VisualElement root, UIStateEnum state)
        {
            var expected = Stage4ExpectedSurfaces.Expected(state);
            foreach (var surface in Stage4ExpectedSurfaces.All)
            {
                var shouldShow = expected.Contains(surface);
                var count = Stage4ScreenSetProbe.CountByName(root, surface.Name);

                // 出ない面は木に無いか見えない。出る面は 1 つだけ見える
                // Hidden surfaces are absent or invisible; shown surfaces are visible exactly once
                if (!shouldShow)
                {
                    var element = root.Q(surface.Name);
                    Assert.IsTrue(element == null || !Stage4ScreenSetProbe.IsVisible(element), $"{state}: {surface.Name} が見えている");
                    continue;
                }
                Assert.AreEqual(1, count, $"{state}: {surface.Name} が {count} 個ある");
                var shown = root.Q(surface.Name);
                Assert.IsTrue(Stage4ScreenSetProbe.IsVisible(shown), $"{state}: {surface.Name} が見えない");
                Assert.AreEqual(surface.ColumnName, Stage4ScreenSetProbe.ColumnNameOf(shown), $"{state}: {surface.Name} の列が違う");

                // 幕は入力を素通しするので Pick の照合から外す
                // The backdrop lets input through, so skip it in the pick check
                if (surface.Name == Stage4ExpectedSurfaces.Backdrop.Name) continue;
                Assert.IsTrue(Stage4ScreenSetProbe.IsPickedAtCenter(shown), $"{state}: {surface.Name} の中心で {Stage4ScreenSetProbe.DescribePicked(shown)} が拾われた（覆われている）");
            }
        }

        private static void AssertResearchTreeRightOfInventory(VisualElement root)
        {
            // 研究エリアは持ち物の右に置かれ、左余白が畳まれる
            // The research area sits right of the inventory and the left padding collapses
            var inventory = root.Q(Stage4ExpectedSurfaces.Inventory.Name).worldBound;
            var research = root.Q(Stage4ExpectedSurfaces.ResearchTree.Name).worldBound;
            Assert.GreaterOrEqual(research.xMin, inventory.xMax - 0.5f, $"研究エリアの左端 {research.xMin} が持ち物の右端 {inventory.xMax} より左");
            Assert.IsNotNull(root.Q(className: LeftPaddingCollapsedClass), "ResearchTree で左余白を畳むクラスが付いていない");
        }

        private static async UniTask TransitionTo(UIStateControl uiStateControl, UIStateEnum state)
        {
            // 遷移要求は次の Update で消費される
            // The transition request is consumed in the next Update
            uiStateControl.RequestTransition(state);
            await UniTask.DelayFrame(3);
            Assert.AreEqual(state, uiStateControl.CurrentState, $"{state} へ遷移していない");
        }
    }
}
```
（研究エリアの左端は Web の `left = --inventory-panel-width + --stage-column-gap` で、`0.5f` は小数の丸めの余裕。研究の計画 R-5）

- [ ] **Step 4: コンパイルしてテストを回す**

Run:
```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.EditModeInPlayingTest.Ui.Stage4.Stage4ScreenSetGameUiTest" --timeout-seconds 900
```
Expected: `ErrorCount: 0`、1 件 PASS。落ちたら、メッセージの面・画面・拾われた要素で原因を分ける: (1) 照合表の名前の違い → Task 1 Step 2 の表で gamma の表を直す、(2) 面が重なる・列が違う・残骸が残る・Error ログ → 持ち主のレーン（取り込み済みなので I-fix）への不具合。bd を `--parent <bd>` で立て、再現の手順（このテストの名前・落ちた行・メッセージ）を書いて指揮へ依頼し、`done.md`「## 差の表」に書いて修正が入るまで待つ（テストを緩めない）。入ったら `git merge --no-ff origin/feature/uitk` で取り込み、同じコマンドで再走

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/Stage4
git commit -m "test(uitk): 段階 4 の同時に出る組を実ゲームで巡る L3 を足す（S4-gamma）" -- moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/Stage4
```
（`.meta` は Unity が作ったものをそのまま含める）

---
### Task 4: 検査用の DSL の口（`Client.Playtest/Operations/Ladder/`）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Playtest/Operations/Ladder/PlaytestTrainLine.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Playtest/Operations/Ladder/PlaytestPanelCostRecorder.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Playtest/Operations/Ladder/PlaytestAnchorChecks.cs`

**Interfaces:**
- Consumes: `PlaytestDriver`（`SetupFlatGround`・`WarpPlayer`・`PlaceBlockDirect`・`WaitBlockGameObject`・`GetBlock`・`RunOnServer`・`Until`・`Assert`・`GiveItemDirect`・`SendCommand`・`WaitSeconds`・`AimAt`）、`RailComponent`・`TrainStationComponent`・`StationNodeRole`、`ClientContext.VanillaApi.Response.PlaceTrainOnRail`、`ITrainUnitLookupDatastore`、`MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements`、`TutorialsElement.TutorialTypeConst.uiHighLight`・`UiHighLightTutorialParam.HighLightAnchorId`、`TutorialPresentationStateStore.Instance.AddOutlineHighlight(string, Guid)`・`ITutorialView.CompleteTutorial()`、`TutorialAnchorRegistry.Resolve(VisualElement, string)`、`GameUiRoot.Instance.GetRootElement()`、`ProfilerRecorderHandle.GetAvailable`・`ProfilerRecorder.StartNew`
- Produces:
  - `PlaytestTrainLineSetup`（`IBlock StationA`・`IBlock StationB`・`Vector3Int StationAPosition`・`StationBPosition`・`TrainUnit Train`）、`PlaytestTrainLine.Build(PlaytestDriver p, Vector3Int stationAPosition, Vector3Int stationBPosition)` → `UniTask<PlaytestTrainLineSetup>`、`PlaytestTrainLine.AimAtCar(PlaytestDriver p)`、`PlaytestTrainLine.AimAtStation(PlaytestDriver p, Vector3Int stationPosition)`
  - `PlaytestPanelCost`（`int Frames`・`double MeanMs`・`P95Ms`・`MaxMs`・`string Markers`）、`PlaytestPanelCostRecorder.FindPanelMarkers()` → `IReadOnlyList<ProfilerRecorderDescription>`、`PlaytestPanelCostRecorder.Measure(IReadOnlyList<ProfilerRecorderDescription> markers, int frames)` → `UniTask<PlaytestPanelCost>`
  - `PlaytestMasterAnchor`（`string AnchorId`・`Guid TutorialGuid`・`Guid ChallengeGuid`・`string ChallengeTitle`）、`PlaytestAnchorChecks.CollectUiHighlightAnchors()` → `IReadOnlyList<PlaytestMasterAnchor>`、`PlaytestAnchorChecks.CheckOutlineOnAnchor(PlaytestDriver p, PlaytestMasterAnchor anchor)` → `UniTask`（照合は `p.Assert` で記録する）

- [ ] **Step 1: 列車線の準備を書く**（既存 `scenarios/train/train-timetable-via-ui.cs` の準備の写し。既存シナリオは変えない）

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.InGame.Context;
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
using VContainer;
using TrainCarEntityObject = Client.Game.InGame.Train.View.Object.Core.TrainCarEntityObject;

namespace Client.Playtest.Operations.Ladder
{
    // 駅 2 基と機関車を置いた列車線の準備結果
    // The prepared line of two stations and a locomotive
    public sealed class PlaytestTrainLineSetup
    {
        public readonly IBlock StationA;
        public readonly IBlock StationB;
        public readonly Vector3Int StationAPosition;
        public readonly Vector3Int StationBPosition;
        public readonly TrainUnit Train;

        public PlaytestTrainLineSetup(IBlock stationA, IBlock stationB, Vector3Int stationAPosition, Vector3Int stationBPosition, TrainUnit train)
        {
            StationA = stationA;
            StationB = stationB;
            StationAPosition = stationAPosition;
            StationBPosition = stationBPosition;
            Train = train;
        }
    }

    // ラダーの列車の前段（駅の結線・機関車の設置・燃料）をサーバー直で組む。駅名は UI で付けるので付けない
    // Builds the ladder's train prelude server-side; station names are left for the UI
    public static class PlaytestTrainLine
    {
        private const string StationBlockName = "蒸気機関車駅";

        public static async UniTask<PlaytestTrainLineSetup> Build(PlaytestDriver p, Vector3Int stationAPosition, Vector3Int stationBPosition)
        {
            // 駅 2 基を置き、A 出口→B 入口・B 出口→A 入口を結線する
            // Place two stations and connect A exit to B entry and B exit to A entry
            await p.PlaceBlockDirect(StationBlockName, stationAPosition, BlockDirection.North);
            await p.PlaceBlockDirect(StationBlockName, stationBPosition, BlockDirection.North);
            await p.WaitBlockGameObject(stationAPosition);
            await p.WaitBlockGameObject(stationBPosition);
            var stationA = p.GetBlock(stationAPosition);
            var stationB = p.GetBlock(stationBPosition);
            var railsA = stationA.GetComponents<RailComponent>();
            var railsB = stationB.GetComponents<RailComponent>();
            p.Assert(railsA.Count == 2 && railsB.Count == 2, "駅は2本のRailComponentを持つ");
            await p.RunOnServer("駅2基を結線する", () =>
            {
                ExitOf(railsA).FrontNode.ConnectNode(EntryOf(railsB).FrontNode);
                ExitOf(railsB).FrontNode.ConnectNode(EntryOf(railsA).FrontNode);
            });

            // 機関車を解放して駅 A の構内へ置き、燃料を入れる
            // Unlock a locomotive, place it inside station A and fuel it
            var clientUnlockState = ClientDIContext.DIContainer.DIContainerResolver.Resolve<IGameUnlockStateData>();
            var carMaster = MasterHolder.TrainUnitMaster.Train.TrainCars.First(c => 0 < c.TractionForce);
            await p.RunOnServer("機関車を解放する", () => ServerContext.GetService<IGameUnlockStateDataController>().UnlockTrainCar(carMaster.TrainCarGuid));
            await p.Until(() => clientUnlockState.TrainCarUnlockStateInfos.TryGetValue(carMaster.TrainCarGuid, out var info) && info.IsUnlocked, 10f, "車両アンロック同期");
            foreach (var required in carMaster.RequiredItems) await p.GiveItemDirect(MasterHolder.ItemMaster.GetItemMaster(required.ItemGuid).Name, required.Count);
            var railPosition = new RailPosition(new List<IRailNode> { ExitOf(railsA).FrontNode, EntryOf(railsA).FrontNode }, TrainLengthConverter.ToRailUnits(carMaster.Length), 0);
            var placed = await ClientContext.VanillaApi.Response.PlaceTrainOnRail(railPosition, carMaster.TrainCarGuid, CancellationToken.None);
            p.Assert(placed != null && placed.Success, $"車両設置成功 (failure={placed?.FailureType})");
            await p.Until(() => SpawnedCar() != null, 15f, "車両entity出現");
            p.SendCommand("addFuelToAllTrainCarsCommand");
            await p.WaitSeconds(1f);

            TrainUnit train = null;
            await p.RunOnServer("検証対象の列車を取得する", () => train = ServerContext.GetService<ITrainUnitLookupDatastore>().GetRegisteredTrains().First());
            return new PlaytestTrainLineSetup(stationA, stationB, stationAPosition, stationBPosition, train);

            #region Internal

            RailComponent ExitOf(List<RailComponent> rails) => rails.First(r => r.FrontNode.StationRef.NodeRole == StationNodeRole.Exit);
            RailComponent EntryOf(List<RailComponent> rails) => rails.First(r => r.FrontNode.StationRef.NodeRole == StationNodeRole.Entry);

            #endregion
        }

        public static async UniTask AimAtCar(PlaytestDriver p)
        {
            // 駅の外の後端（南）側から F の届く 2m 以内で車両の上寄りを狙う
            // Aim high on the car from outside the station's rear (south) end within the 2m F reach
            Collider rear = null;
            foreach (var collider in SpawnedCar().GetComponentsInChildren<Collider>(true))
            {
                if (rear == null || collider.bounds.min.z < rear.bounds.min.z) rear = collider;
            }
            var bounds = rear.bounds;
            p.WarpPlayer(new Vector3(bounds.center.x, 33.5f, bounds.min.z - 1.2f));
            await p.WaitSeconds(1f);
            await p.AimAt(new Vector3(bounds.center.x, bounds.max.y - 0.3f, bounds.min.z + 0.3f));
            await p.WaitSeconds(0.5f);
        }

        public static async UniTask AimAtStation(PlaytestDriver p, Vector3Int stationPosition)
        {
            // 駅の外の南側から駅の南端の上寄りを狙う（構内に車両が無い駅で使う）
            // Aim high on the station's south end from outside (use on a station without a car)
            var bounds = await StationBounds(p, stationPosition);
            p.WarpPlayer(new Vector3(bounds.center.x, 33.5f, bounds.min.z - 1.2f));
            await p.WaitSeconds(1f);
            await p.AimAt(new Vector3(bounds.center.x, bounds.max.y - 0.3f, bounds.min.z + 0.3f));
            await p.WaitSeconds(0.5f);
        }

        private static async UniTask<Bounds> StationBounds(PlaytestDriver p, Vector3Int stationPosition)
        {
            var blockObject = await p.WaitBlockGameObject(stationPosition);
            var colliders = blockObject.GetComponentsInChildren<Collider>(true);
            var bounds = colliders[0].bounds;
            foreach (var collider in colliders) bounds.Encapsulate(collider.bounds);
            return bounds;
        }

        private static TrainCarEntityObject SpawnedCar() => Object.FindObjectsByType<TrainCarEntityObject>(FindObjectsSortMode.None).FirstOrDefault();
    }
}
```
200 行未満を Step 4 の `wc -l` で確かめる。

- [ ] **Step 2: CPU 時間の読み取りを書く**

```csharp
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;

namespace Client.Playtest.Operations.Ladder
{
    // UI のパネル更新と描画のマーカーを N フレーム読んだ結果（ms）
    // The UI panel update and repaint markers read over N frames (ms)
    public readonly struct PlaytestPanelCost
    {
        public readonly int Frames;
        public readonly double MeanMs;
        public readonly double P95Ms;
        public readonly double MaxMs;
        public readonly string Markers;

        public PlaytestPanelCost(int frames, double meanMs, double p95Ms, double maxMs, string markers)
        {
            Frames = frames;
            MeanMs = meanMs;
            P95Ms = p95Ms;
            MaxMs = maxMs;
            Markers = markers;
        }
    }

    // ProfilerRecorder で UITK のパネル更新と描画の時間を測る（PoC と同じ測り方）
    // Measures UITK panel update and repaint time with ProfilerRecorder (as the PoC did)
    public static class PlaytestPanelCostRecorder
    {
        private static readonly string[] MarkerKeywords = { "UpdatePanels", "RepaintPanels" };

        public static IReadOnlyList<ProfilerRecorderDescription> FindPanelMarkers()
        {
            // 使えるマーカーから UIElements のパネル更新と描画を名前で拾う
            // Pick UIElements panel update and repaint by name from the available markers
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            return handles
                .Select(ProfilerRecorderHandle.GetDescription)
                .Where(d => d.Name.StartsWith("UIElements") && MarkerKeywords.Any(k => d.Name.Contains(k)))
                .ToList();
        }

        public static IReadOnlyList<string> ListUiElementsMarkerNames()
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            return handles.Select(h => ProfilerRecorderHandle.GetDescription(h).Name).Where(n => n.Contains("UIElements")).OrderBy(n => n).ToList();
        }

        public static async UniTask<PlaytestPanelCost> Measure(IReadOnlyList<ProfilerRecorderDescription> markers, int frames)
        {
            // フレームごとにマーカーの和を取り、前のフレームの値を読む
            // Sum the markers per frame, reading the previous frame's value
            var recorders = markers.Select(d => ProfilerRecorder.StartNew(d.Category, d.Name, 1)).ToList();
            var samplesMs = new List<double>(frames);
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
            for (var i = 0; i < frames; i++)
            {
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
                samplesMs.Add(recorders.Sum(r => r.LastValue) / 1_000_000.0);
            }
            foreach (var recorder in recorders) recorder.Dispose();

            var sorted = samplesMs.OrderBy(v => v).ToList();
            var p95 = sorted[(int)System.Math.Ceiling(sorted.Count * 0.95) - 1];
            return new PlaytestPanelCost(frames, samplesMs.Average(), p95, sorted[^1], string.Join("+", markers.Select(d => d.Name)));
        }
    }
}
```
マーカーの名前は Editor の版で変わりうるので決め打ちしない。0 本なら Task 8 のシナリオが `ListUiElementsMarkerNames()` を Note に出して失敗する（気づく経路はシナリオの `Success: false` と Note）。

- [ ] **Step 3: マスタのアンカーの照合を書く**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Tutorial;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Root;
using Core.Master;
using Cysharp.Threading.Tasks;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Playtest.Operations.Ladder
{
    // マスタのチュートリアルが指すアンカー 1 件
    // One anchor a master tutorial points at
    public readonly struct PlaytestMasterAnchor
    {
        public readonly string AnchorId;
        public readonly Guid TutorialGuid;
        public readonly Guid ChallengeGuid;
        public readonly string ChallengeTitle;

        public PlaytestMasterAnchor(string anchorId, Guid tutorialGuid, Guid challengeGuid, string challengeTitle)
        {
            AnchorId = anchorId;
            TutorialGuid = tutorialGuid;
            ChallengeGuid = challengeGuid;
            ChallengeTitle = challengeTitle;
        }
    }

    // マスタの uiHighLight を集め、実ゲームの木でアンカーと枠を照合する
    // Collects master uiHighLight entries and checks the anchor and frame in the live tree
    public static class PlaytestAnchorChecks
    {
        private const string OutlineName = "tutorial-outline";
        private const float CenterTolerancePx = 5f;

        public static IReadOnlyList<PlaytestMasterAnchor> CollectUiHighlightAnchors()
        {
            var anchors = new List<PlaytestMasterAnchor>();
            foreach (var category in MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements)
            foreach (var challenge in category.Challenges)
            foreach (var tutorial in challenge.Tutorials)
            {
                if (tutorial.TutorialType != TutorialsElement.TutorialTypeConst.uiHighLight) continue;
                var param = (UiHighLightTutorialParam)tutorial.TutorialParam;
                anchors.Add(new PlaytestMasterAnchor(param.HighLightAnchorId, tutorial.TutorialGuid, challenge.ChallengeGuid, challenge.Title));
            }
            return anchors;
        }

        public static async UniTask CheckOutlineOnAnchor(PlaytestDriver p, PlaytestMasterAnchor anchor)
        {
            // 今の画面の木でアンカーが 1 つに解決する
            // The anchor resolves to exactly one element in the current tree
            var root = GameUiRoot.Instance.GetRootElement();
            var resolution = TutorialAnchorRegistry.Resolve(root, anchor.AnchorId);
            var unique = resolution as TutorialAnchorResolution.Unique;
            p.Assert(unique != null, $"{anchor.ChallengeTitle}: {anchor.AnchorId} が 1 つに解決する（{resolution.GetType().Name}）");
            if (unique == null) return;

            // 本番と同じ入口で枠を出し、画面内のアンカーなら中心が合うことを見る
            // Show the frame through the production entry and, for on-screen anchors, check the centers match
            var view = TutorialPresentationStateStore.Instance.AddOutlineHighlight(anchor.AnchorId, anchor.TutorialGuid);
            await p.Until(() => root.Q(OutlineName) != null, 5f, $"{anchor.AnchorId} の枠が出る");
            await UniTask.DelayFrame(2);
            var outline = root.Q(OutlineName);
            var anchorBound = unique.Element.worldBound;
            var onScreen = root.worldBound.Overlaps(anchorBound);
            if (onScreen)
            {
                var distance = Vector2.Distance(outline.worldBound.center, anchorBound.center);
                p.Assert(distance <= CenterTolerancePx, $"{anchor.AnchorId} の枠の中心がアンカーの中心から {distance:F1}px（{CenterTolerancePx}px 以内）");
            }
            else
            {
                p.Note($"{anchor.AnchorId} は画面外（{anchorBound}）。枠の位置は見ない");
            }
            await p.Screenshot($"anchor-{anchor.AnchorId}");

            // 完了で枠が消える
            // Completing removes the frame
            view.CompleteTutorial();
            await p.Until(() => root.Q(OutlineName) == null, 5f, $"{anchor.AnchorId} の枠が消える");
        }
    }
}
```
枠の要素名 `tutorial-outline` は S2-tutorial の実物で Task 1 Step 2 の照合表から読み替える（枠が名前でなくクラスなら `root.Q(className: …)`）。`TutorialPresentationStateStore` の名前空間は `Client.Game.InGame.Tutorial`（起草時の実物）。

- [ ] **Step 4: コンパイルして行数を見る**

Run:
```bash
uloop compile --project-path ./moorestech_client
wc -l moorestech_client/Assets/Scripts/Client.Playtest/Operations/Ladder/*.cs
```
Expected: `ErrorCount: 0`、3 本とも 200 行未満。`Func<>`・`partial`・`try` が無いことを `grep -n "Func<\|partial \|try" moorestech_client/Assets/Scripts/Client.Playtest/Operations/Ladder/*.cs` が 0 件で見る

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Playtest/Operations/Ladder
git commit -m "feat(playtest): ラダー用の列車線・CPU 時間・アンカー照合の口を足す（S4-gamma）" -- moorestech_client/Assets/Scripts/Client.Playtest/Operations/Ladder
```

---

### Task 5: ラダー 4 `uitk-production-loop`

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-production-loop.cs`

**Interfaces:**
- Consumes: Task 1 の照合表の名前（`item-list-cell-<id>`・`craft-button-<guid>`・`machine-input-slots`・`machine-output-slots`・`main-slot-<i>`・`inventory-sort`・`block-inventory-close`）、`PlaytestUiElementOps.FindUi(string)`（`ScreenRect`）、`SemanticInput`、`ILocalPlayerInventory`、`PlaytestItemOps.CountItemClientSide(ItemId)`、`IOpenableBlockInventoryComponent.InventoryItems`
- Produces: シナリオ `uitk-production-loop`（Task 12 の通しが使う）

マスタの値（e0c913033c）: 木の板 ← 原木×1（0.75s、craftRecipeGuid `37623c6d-e1cf-4985-abfb-8239e6e33981`）。石窯（`26096ad0-d355-4e2c-9e45-a5c5e91856e0`、ElectricMachine、requiredPower 0、入力 2・出力 1）のレシピ 青銅鉱石の粉 1＋原木 1 → 青銅インゴット 1（3s）。

- [ ] **Step 1: シナリオを書く**

```csharp
// ラダー4 uitk-production-loop: Tab→長押しクラフト→石窯に掴み・Shift・右ドラッグで入れる→開いたまま出来上がりを見る→取り出す→整理（UITKだけ）
// Ladder 4 uitk-production-loop: Tab -> hold-craft -> load a stone kiln by grab, Shift and right-drag -> watch output while open -> take out -> sort (UITK only)
// 実行前にPlayModeを止め、execute-dynamic-codeで UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.WebUiHost.Boot;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("uitk-production-loop", options, async p =>
{
    // 背景Editorでも注入入力が届くよう焦点無視へ切り替える
    // Ignore focus so injected input reaches a background Editor
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);

    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    p.Assert(WebUiHost.Hub == null, "Webホストは起動していない（UITK側）");

    var items = MasterHolder.ItemMaster;
    ItemId Id(string name) => items.GetItemAllIds().First(id => items.GetItemMaster(id).Name == name);
    var log = Id("原木");
    var plank = Id("木の板");
    var bronzeDust = Id("青銅鉱石の粉");
    var ingot = Id("青銅インゴット");
    await p.GiveItem("原木", 12);
    await p.GiveItem("青銅鉱石の粉", 4);

    // Tab で持ち物を開き、木の板を選んで長押しでクラフトする
    // Open the inventory with Tab, select planks and hold to craft
    p.Note("Tab→木の板→長押しクラフト");
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
    await PlaytestUiElementOps.ScrollDownUntilUiElementIsClickable($"item-list-cell-{plank.AsPrimitive()}", "item-list-scroll", 10f);
    await p.ClickUi($"item-list-cell-{plank.AsPrimitive()}");
    const string craftButton = "craft-button-37623c6d-e1cf-4985-abfb-8239e6e33981";
    await p.UntilUiElement(craftButton, 10f);
    var planksBefore = PlaytestItemOps.CountItemClientSide(plank);
    await HoldOn(craftButton, 2.5f);
    await p.Until(() => planksBefore + 2 <= PlaytestItemOps.CountItemClientSide(plank), 10f, "長押しで木の板が2枚以上増える");
    await p.Screenshot("01-hold-craft");
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 石窯を置いて F で開く
    // Place a stone kiln and open it with F
    var kilnPos = new Vector3Int(10, 32, 10);
    await p.SetupFlatGround();
    await p.PlaceBlockDirect("石窯", kilnPos, BlockDirection.North);
    var kilnObject = await p.WaitBlockGameObject(kilnPos);
    var kilnBounds = kilnObject.GetComponentsInChildren<Collider>(true)[0].bounds;
    p.WarpPlayer(new Vector3(kilnBounds.center.x, 33.5f, kilnBounds.min.z - 1.5f));
    await p.WaitSeconds(1f);
    await p.AimAt(kilnBounds.center);
    await p.PressInteract();
    await p.WaitUiState(UIStateEnum.SubInventory, 10f);
    await p.UntilUiElement("machine-input-slots", 10f);
    await p.Screenshot("02-kiln-open");

    // 原木を掴んで入力枠へ置き、Shift＋クリックで粉を入れる
    // Grab logs into an input slot, then Shift-click the dust in
    p.Note("掴んで置く・Shiftで入れる");
    await p.ClickUi(MainSlotOf(log));
    await p.ClickUi("machine-input-slots-slot-0");
    SemanticInput.KeyDown(Key.LeftShift);
    await p.ClickUi(MainSlotOf(bronzeDust));
    SemanticInput.KeyUp(Key.LeftShift);
    await p.Until(() => KilnHas(log) && KilnHas(bronzeDust), 10f, "石窯の入力に原木と粉が入る（サーバー）");

    // 残りの原木を掴み右ドラッグで空き 3 枠へ配る
    // Grab the remaining logs and right-drag them across three empty slots
    p.Note("右ドラッグで配る");
    await p.ClickUi(MainSlotOf(log));
    var empties = EmptyMainSlots(3);
    await DragWithButton(1, empties);
    p.Assert(empties.All(i => Local()[i].Id == log), "右ドラッグで空き3枠に原木が配られる");
    await p.Screenshot("03-right-drag");
    await p.ClickUi(MainSlotOf(log));

    // 開いたまま出来上がりが出力枠に出るのを見る（機械 UI を開いたまま中身が更新される録画）
    // Watch the ingot appear in the output while the UI stays open
    await p.Until(() => KilnHas(ingot), 20f, "石窯で青銅インゴットができる（サーバー）");
    await PlaytestUiElementOps.WaitUiTextContains("machine-output-slots", "1", 10f);
    await p.Screenshot("04-output-while-open");

    // Shift で取り出し、閉じて整理する
    // Shift-take the output, close and sort
    var ingotsBefore = PlaytestItemOps.CountItemClientSide(ingot);
    SemanticInput.KeyDown(Key.LeftShift);
    await p.ClickUi("machine-output-slots-slot-0");
    SemanticInput.KeyUp(Key.LeftShift);
    await p.Until(() => ingotsBefore < PlaytestItemOps.CountItemClientSide(ingot), 10f, "Shiftで青銅インゴットを取り出す");
    await p.ClickUi("block-inventory-close");
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
    await p.ClickUi("inventory-sort");
    await p.Until(() => Local()[0].Id != ItemMaster.EmptyItemId, 10f, "整理で先頭の枠が埋まる");
    await p.Screenshot("05-sorted");

    #region Internal

    ILocalPlayerInventory Local() => ClientDIContext.DIContainer.DIContainerResolver.Resolve<ILocalPlayerInventory>();
    string MainSlotOf(ItemId id) => $"main-slot-{Enumerable.Range(0, Local().MainSlotCount).First(i => Local()[i].Id == id)}";
    int[] EmptyMainSlots(int count) => Enumerable.Range(0, Local().MainSlotCount).Where(i => Local()[i].Id == ItemMaster.EmptyItemId).Take(count).ToArray();
    bool KilnHas(ItemId id) => p.GetBlock(kilnPos).GetComponent<IOpenableBlockInventoryComponent>().InventoryItems.Any(s => s.Id == id && 0 < s.Count);

    async UniTask HoldOn(string testid, float seconds)
    {
        var center = (await PlaytestUiElementOps.FindUi(testid)).ScreenRect.center;
        SemanticInput.MouseMoveTo(center);
        SemanticInput.MouseButtonDown(0);
        await p.WaitSeconds(seconds);
        SemanticInput.MouseButtonUp(0);
    }

    async UniTask DragWithButton(int button, int[] slots)
    {
        var first = (await PlaytestUiElementOps.FindUi($"main-slot-{slots[0]}")).ScreenRect.center;
        SemanticInput.MouseMoveTo(first);
        SemanticInput.MouseButtonDown(button);
        foreach (var slot in slots.Skip(1)) await SemanticInput.MouseGlideTo((await PlaytestUiElementOps.FindUi($"main-slot-{slot}")).ScreenRect.center, 0.3f);
        SemanticInput.MouseButtonUp(button);
        await p.WaitSeconds(0.5f);
    }

    #endregion
});
```
`KilnHas` はサーバーのブロックを読む（`p.GetBlock` は既存のシナリオがサーバーの値の読みに使う形）。tick と競合した読みで例外が出たら bd moorestech-xyuaf.2.10 の既知の問題として `p.RunOnServer` で包む形へ直す。`ItemId.AsPrimitive()`（PlaytestItemOps と同じ）・`ItemMaster.EmptyItemId` の綴りは Task 1 Step 2 で `item-list-cell-` の作り方と一緒に確かめる。`machine-input-slots-slot-0`・`machine-output-slots-slot-0` は照合表の実物の名前へ読み替える

- [ ] **Step 2: 系を UITK にして流す**

Run:
```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
uloop execute-dynamic-code --project-path ./moorestech_client --code 'System.Environment.SetEnvironmentVariable("MOORESTECH_PLAYTEST_RECEIVER_BASE","http://127.0.0.1:9"); Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi);'
.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh ./moorestech_client .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-production-loop.cs
```
Expected: 終了コード 0、`run dir` の `result.json` が `Success: true`、`recording.mp4` がある。`wc -l` で 200 行未満

- [ ] **Step 3: 掴みの申し送りを観察する（C4-e D-36・Warning (a)(b)(e)）**

録画の 03 の前後を見て、右ドラッグ配りの後の掴みの数（クライアントの掴みとサーバーの掴みが同じか）を `done.md`「## 回ってきた件」の D-36 の行に書く。続けて同じシナリオをもう 1 回流し、`uloop get-logs --project-path ./moorestech_client --log-type Warning` に `[SplitDrag]`・`stale`・`DiscardStaleSession` の行が出たかを数えて同じ行に書く（再現したら bd を `--parent <bd>` で立て、手順と行を添えて I-fix へ回す。gamma は直さない）

- [ ] **Step 4: 失敗したら持ち主へ回す**

`result.json` の `Assertions` の落ちた行と `uloop get-logs --log-type Error` を読み、(1) 名前の違い → 照合表を直してシナリオを直す、(2) UI の不具合（クリックが届かない・数が合わない・出力が描き直されない）→ 持ち主（I-fix）への bd と依頼文、`done.md`「## 差の表」に記録して待つ。入ったら取り込んで Step 2 を流し直す

- [ ] **Step 5: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-production-loop.cs
git commit -m "test(uitk): ラダー 4 uitk-production-loop を足す（S4-gamma）" -- .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-production-loop.cs
```

---

### Task 6: ラダー 5 `uitk-research-train`

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-research-train.cs`

**Interfaces:**
- Consumes: Task 4 の `PlaytestTrainLine.Build`・`AimAtCar`・`AimAtStation`・`PlaytestTrainLineSetup`、`PlaytestResearchSnapshots.WaitForResearch`、`ReadTrainState`・`WaitForTrainState`、照合表の名前（`research-node-<guid>`・`research-button-<guid>`・`train-station-name-input`・`train-station-name-apply`・`train-tab-timetable`・`train-timetable-station-{x}_{y}_{z}-add`・`train-timetable-stop-{i}`・`train-timetable-auto-run-on`）
- Produces: シナリオ `uitk-research-train`

原始研究 1（`858bcb10-b8ba-478e-9bc5-473ca61281a2`）の消費は 木の板 3・木の棒 3・砕いた石材 2、前提なし（e0c913033c）。

- [ ] **Step 1: シナリオを書く**

```csharp
// ラダー5 uitk-research-train: 研究を1つ完了→駅名を日本語で付ける（SetText）→時刻表を組んで自動運転を入れる（UITKだけ）
// Ladder 5 uitk-research-train: finish one research -> name a station in Japanese (SetText) -> build a timetable and turn auto-run on (UITK only)
// 実行前にPlayModeを止め、execute-dynamic-codeで UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ladder;
using Client.Playtest.Operations.ServerSnapshots;
using Client.WebUiHost.Boot;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.TrainRail;
using UnityEngine;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("uitk-research-train", options, async p =>
{
    // 背景Editorでも注入入力が届くよう焦点無視へ切り替える
    // Ignore focus so injected input reaches a background Editor
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);

    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    p.Assert(WebUiHost.Hub == null, "Webホストは起動していない（UITK側）");

    // 研究素材を渡し、研究ツリーで原始研究1を完了する
    // Give research materials and finish primitive research 1 in the tree
    var research1 = Guid.Parse("858bcb10-b8ba-478e-9bc5-473ca61281a2");
    await p.GiveItem("木の板", 3);
    await p.GiveItem("木の棒", 3);
    await p.GiveItem("砕いた石材", 2);
    p.Note("研究ツリー→原始研究1→研究する");
    await p.PressKey(Key.R);
    await p.WaitUiState(UIStateEnum.ResearchTree, 10f);
    await p.ClickUi($"research-node-{research1}");
    await p.ClickUi($"research-button-{research1}");
    await p.WaitForResearch(new[] { research1 }, Array.Empty<Guid>(), 15f, "原始研究1が完了する（サーバー）");
    await p.Screenshot("01-research-done");
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 駅 2 基と機関車の線を準備する（サーバー直。駅名は付けない）
    // Prepare two stations and a locomotive server-side (no names yet)
    await p.SetupFlatGround();
    var line = await PlaytestTrainLine.Build(p, new Vector3Int(10, 32, 4), new Vector3Int(10, 32, 64));

    // 駅 B を開き、駅名を日本語で入れて確定する
    // Open station B, type a Japanese name and confirm
    p.Note("駅名を日本語で付ける");
    await PlaytestTrainLine.AimAtStation(p, line.StationBPosition);
    await p.PressInteract();
    await p.WaitUiState(UIStateEnum.SubInventory, 10f);
    await p.SetText("train-station-name-input", "南駅");
    await p.ClickUi("train-station-name-apply");
    string serverName = null;
    await p.Until(() =>
    {
        serverName = line.StationB.GetComponent<TrainStationComponent>().StationName;
        return serverName == "南駅";
    }, 10f, "サーバーの駅名が南駅になる");
    await p.Screenshot("02-station-named");
    await p.ClickUi("block-inventory-close");
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 機関車を開き、時刻表に 2 駅を足して自動運転を入れる
    // Open the locomotive, add both stations to the timetable and turn auto-run on
    p.Note("時刻表を組んで自動運転");
    await PlaytestTrainLine.AimAtCar(p);
    await p.PressInteract();
    await p.WaitUiState(UIStateEnum.SubInventory, 10f);
    await p.ClickUi("train-tab-timetable");
    var a = line.StationAPosition;
    var b = line.StationBPosition;
    await p.ClickUi($"train-timetable-station-{a.x}_{a.y}_{a.z}-add");
    await p.ClickUi($"train-timetable-station-{b.x}_{b.y}_{b.z}-add");
    await p.UntilUiElement("train-timetable-stop-1", 5f);
    await p.WaitForTrainState(line.Train, 2, null, null, 10f, "サーバーの時刻表が2駅になる");
    await p.ClickUi("train-timetable-auto-run-on");
    await p.WaitForTrainState(line.Train, null, true, null, 10f, "サーバーの自動運転がON");
    await p.Screenshot("03-timetable");
    await p.ClickUi("block-inventory-close");
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.WaitForTrainState(line.Train, null, null, true, 15f, "列車が走り出す");
    await p.WaitSeconds(2f);
    await p.Screenshot("04-running");
});
```
`TrainStationComponent.StationName` の綴り、研究ツリーを開くキー（`R`）、駅名の確定の操作（S4-train が適用ボタンを消していれば `train-station-name-apply` の行を、S4-train の `done.md` に書かれた確定の操作〈Enter なら `await p.PressKey(Key.Enter);`〉に置き換える）、時刻表の追加が即送信か（適用ボタンが残っていれば `train-timetable-apply` を押す行を足す）は Task 1 Step 2・3 の照合表で決める。サーバーの駅名の読みで tick と競合したら `p.RunOnServer` で包む

- [ ] **Step 2: 系を UITK にして流す**

Run:
```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
uloop execute-dynamic-code --project-path ./moorestech_client --code 'System.Environment.SetEnvironmentVariable("MOORESTECH_PLAYTEST_RECEIVER_BASE","http://127.0.0.1:9"); Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi);'
.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh ./moorestech_client .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-research-train.cs
```
Expected: 終了コード 0、`Success: true`、`recording.mp4` がある。落ちたら Task 5 Step 4 と同じ手順で分けて回す

- [ ] **Step 3: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-research-train.cs
git commit -m "test(uitk): ラダー 5 uitk-research-train を足す（S4-gamma）" -- .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-research-train.cs
```

---

### Task 7: マスタのアンカー 9 件 `uitk-master-anchors`

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-master-anchors.cs`

**Interfaces:**
- Consumes: Task 4 の `PlaytestAnchorChecks.CollectUiHighlightAnchors()`・`CheckOutlineOnAnchor(PlaytestDriver, PlaytestMasterAnchor)`、`AnchorIds`（`ChallengeCurrentHud`・`RecipeCraftButton`・`ResearchNodePrefix`・`RecipeItem(int)` の綴りは Task 1 Step 2 で実物に合わせる）
- Produces: シナリオ `uitk-master-anchors`。`done.md`「## 証拠」のアンカー 9 件の表

- [ ] **Step 1: シナリオを書く**

```csharp
// マスタのアンカー: ピン先マスタの uiHighLight 9 件を、その画面を開いて実ゲームの木で解決し枠を出す（UITK だけ）
// Master anchors: resolve the 9 uiHighLight anchors of the pinned master in the live tree of each screen and show the frame (UITK only)
using System.Linq;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ladder;
using Core.Master;
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("uitk-master-anchors", options, async p =>
{
    // 背景Editorでも注入入力が届くよう焦点無視へ切り替える
    // Ignore focus so injected input reaches a background Editor
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);

    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();

    // マスタから集めた件数と中身を記録する
    // Record the count and contents collected from the master
    var anchors = PlaytestAnchorChecks.CollectUiHighlightAnchors();
    foreach (var a in anchors) p.Note($"{a.ChallengeGuid} {a.ChallengeTitle} → {a.AnchorId}");
    p.Assert(anchors.Count == 9, $"ピン先マスタの uiHighLight は 9 件（{anchors.Count}）");

    // ゲーム画面: チャレンジ HUD
    // Game screen: challenge HUD
    foreach (var a in anchors.Where(a => a.AnchorId == "challenge.current-hud")) await PlaytestAnchorChecks.CheckOutlineOnAnchor(p, a);

    // 持ち物: 木の板を選んでクラフトボタンとアイテムのセル
    // Inventory: select planks, then the craft button and the item cell
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
    var plank = MasterHolder.ItemMaster.GetItemAllIds().First(id => MasterHolder.ItemMaster.GetItemMaster(id).Name == "木の板");
    await PlaytestUiElementOps.ScrollDownUntilUiElementIsClickable($"item-list-cell-{plank.AsPrimitive()}", "item-list-scroll", 10f);
    await p.ClickUi($"item-list-cell-{plank.AsPrimitive()}");
    foreach (var a in anchors.Where(a => a.AnchorId == "recipe.craft-button")) await PlaytestAnchorChecks.CheckOutlineOnAnchor(p, a);
    var itemAnchor = anchors.First(a => a.AnchorId == "recipe.craft-button");
    await PlaytestAnchorChecks.CheckOutlineOnAnchor(p, new PlaytestMasterAnchor($"recipe.item-{plank.AsPrimitive()}", itemAnchor.TutorialGuid, itemAnchor.ChallengeGuid, "アイテムのセル（マスタ外の確認）"));
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 研究ツリー: 研究ノード 7 件
    // Research tree: the seven research nodes
    await p.PressKey(Key.R);
    await p.WaitUiState(UIStateEnum.ResearchTree, 10f);
    await p.WaitSeconds(1f);
    foreach (var a in anchors.Where(a => a.AnchorId.StartsWith("research.node-"))) await PlaytestAnchorChecks.CheckOutlineOnAnchor(p, a);

    // 9 件のどれも画面の割り振りから漏れていない
    // None of the 9 anchors is left out of the screen routing
    var routed = anchors.Count(a => a.AnchorId == "challenge.current-hud" || a.AnchorId == "recipe.craft-button" || a.AnchorId.StartsWith("research.node-"));
    p.Assert(routed == anchors.Count, $"全アンカーを画面に割り振った（{routed}/{anchors.Count}）");
});
```
アンカーの文字列は `AnchorIds` の定数と同じ（`challenge.current-hud`・`recipe.craft-button`・`research.node-`。`AnchorIds.cs` で実物を確かめる）。マスタ外の `recipe.item-<itemId>` は `AnchorIds.RecipeItem(int)` の書式で、タイトルにマスタ外と明記して 9 件と混ぜない

- [ ] **Step 2: 流す**

Run: Task 5 Step 2 の 3 行の最後のパスを `uitk-master-anchors.cs` にして流す
Expected: `Success: true`。Note に 9 行（上の「マスタのアンカー 9 件」の表と同じ）。アンカーが `Missing`・`Duplicate` なら持ち主（`TutorialAnchorCoveragePending` の旧表の持ち主。取り込み済みなら I-fix）へ bd と依頼文で回して待つ。枠の中心が 5px を超えてずれたら S2-tutorial の枠の位置の不具合として同じく回す

- [ ] **Step 3: 表を done.md に写してコミットする**

Note の 9 行を `done.md`「## 証拠」に「# / チャレンジ / anchorId / 解決 / 枠の中心のずれ px / スクショ」の表で写す。続けて:
```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-master-anchors.cs
git commit -m "test(uitk): マスタのアンカー 9 件を実ゲームで解決するシナリオを足す（S4-gamma）" -- .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-master-anchors.cs
```

---

### Task 8: CPU 時間 `uitk-stage4-panel-cost` と台帳の 1 行

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-stage4-panel-cost.cs`
- Modify: `docs/uitk/TODO.md`（「## 実測の結論」に 1 行）

**Interfaces:**
- Consumes: Task 4 の `PlaytestPanelCostRecorder.FindPanelMarkers()`・`ListUiElementsMarkerNames()`・`Measure(IReadOnlyList<ProfilerRecorderDescription>, int)`・`PlaytestPanelCost`、`PlaytestGameViewOps.SetResolution(CancellationToken, int, int)`
- Produces: 台帳の CPU 時間の行

- [ ] **Step 1: シナリオを書く**

```csharp
// 段階4のCPU時間: GameScreen・PlayerInventory・ResearchTree を各240フレーム、UIElements のパネル更新と描画の和を測る（録画しない）
// Stage-4 CPU time: sum UIElements panel update and repaint over 240 frames each on GameScreen, PlayerInventory and ResearchTree (no recording)
using System.Threading;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations.Ladder;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = false };
return PlaytestRunner.Run("uitk-stage4-panel-cost", options, async p =>
{
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);

    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    await PlaytestGameViewOps.SetResolution(CancellationToken.None, 1920, 1080);

    // マーカーが見つからなければ名前の一覧を残して止める
    // Leave the marker name list and stop when no marker is found
    var markers = PlaytestPanelCostRecorder.FindPanelMarkers();
    if (markers.Count == 0) p.Note($"UIElements のマーカー一覧: {string.Join(", ", PlaytestPanelCostRecorder.ListUiElementsMarkerNames())}");
    p.Assert(0 < markers.Count, $"パネル更新と描画のマーカーが見つかる（{markers.Count} 本）");
    if (markers.Count == 0) return;

    // 3 画面で測り、Note に 1 行ずつ出す
    // Measure on three screens and note one line each
    await MeasureOn("GameScreen", null);
    await MeasureOn("PlayerInventory", Key.Tab);
    await MeasureOn("ResearchTree", Key.R);

    #region Internal

    async UniTask MeasureOn(string label, Key? openKey)
    {
        if (openKey.HasValue)
        {
            await p.PressKey(openKey.Value);
            await p.WaitUiState(label == "ResearchTree" ? UIStateEnum.ResearchTree : UIStateEnum.PlayerInventory, 10f);
        }
        await p.WaitSeconds(1f);
        var cost = await PlaytestPanelCostRecorder.Measure(markers, 240);
        p.Note($"panel-cost {label} {Screen.width}x{Screen.height} frames={cost.Frames} mean={cost.MeanMs:F3}ms p95={cost.P95Ms:F3}ms max={cost.MaxMs:F3}ms markers={cost.Markers}");
        if (openKey.HasValue)
        {
            await p.PressKey(Key.Escape);
            await p.WaitUiState(UIStateEnum.GameScreen, 10f);
        }
    }

    #endregion
});
```
（`PlaytestGameViewOps` の名前空間は `Client.Playtest.Operations.Ui`。Task 1 Step 2 の grep で実物に合わせる）

- [ ] **Step 2: 流して台帳に書く**

Run: Task 5 Step 2 の 3 行の最後のパスを `uitk-stage4-panel-cost.cs` にして流し、`grep "panel-cost" <run dir>/result.json`
Expected: `Success: true`、3 行。台帳「## 実測の結論」の末尾に次の 1 行を足す:
```markdown
- 段階 4 の UI の CPU 時間（S4-gamma、<HEAD 10 桁>、Editor の PlayMode・1920×1080・全体スイッチ UITK、ProfilerRecorder で <markers> の和を各 240 フレーム）: GameScreen 平均 <x>ms／p95 <y>ms、PlayerInventory 平均 <x>ms／p95 <y>ms、ResearchTree 平均 <x>ms／p95 <y>ms。目安 1ms 以下（PoC 0.27ms）に <収まった／超えた: 超えたのは …>。プレイヤービルドでは測っていない
```
1ms を超えた画面があれば bd を `--parent <bd>` で立て（性能の調査。持ち主はその画面のレーン → I-fix）、`done.md`「## 未確認」に書く（合否ではない。plan §2.5 の目安）

- [ ] **Step 3: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-stage4-panel-cost.cs docs/uitk/TODO.md
git commit -m "docs(uitk): 段階 4 の UI の CPU 時間を測って台帳に記録する（S4-gamma）" -- .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-stage4-panel-cost.cs docs/uitk/TODO.md
```

---

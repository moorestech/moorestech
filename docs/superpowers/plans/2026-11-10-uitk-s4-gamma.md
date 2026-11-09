# UITK S4-gamma（段階 4 の締め: 同時に出る組の L3・ラダー 4/5・全録画の 2 回目・アンカー 9 件・CPU 時間・束 4 の突き合わせ）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** S4 の 7 レーン（alpha・inventory・machine・train・research・recipe・special）が `feature/uitk` に入った後、全体スイッチ UITK で「持ち物・研究・機械が同時に出る組」が実ゲームで正しい列に 1 つずつ出て覆い合わないことを L3 で固定し、ラダー `uitk-production-loop`・`uitk-research-train` を作ってラダー 1〜5 を緑にし、全録画（全本の 2 回目）を UITK 側と Web 側で流して分類し、マスタのアンカー 9 件の実ゲームでの解決・`UIElementsUpdatePanels`＋`UIElementsRepaintPanels` の CPU 時間・束 4 の突き合わせ差分 0・G1 未記帳 0・波の並べ比べの codex-audit を台帳と `done.md` に残して `feature/uitk` へ取り込む。

**Architecture:** integrator のレーン（`int`。Editor 要）。新しく書くのは「検査する側」だけで、画面・部品・Foundation・State のコードは書かない。L3 は `Client.Tests/EditModeInPlayingTest/Ui/Stage4/` に、面の見え方を読む純粋な関数 `Stage4ScreenSetProbe` と期待の表 `Stage4ExpectedSurfaces` を置き、`Stage4ScreenSetGameUiTest` が本番の遷移（`UIStateControl.RequestTransition`・F キーの注入）で画面を巡って照合する。ラダー・アンカー・CPU・撮影は DSL のシナリオ（`scenarios/uitk/`）で、列車線の準備だけ `Client.Playtest/Operations/Ladder/PlaytestTrainLine` に切り出す（シナリオを 200 行未満に保つため）。全録画は `tools/uitk/run-all-recordings.sh` が 1 本ずつ `run-scenario.sh` を回して TSV に残し、分類と持ち主への回送は人手（このレーンの subagent）が `done.md` に書く。不具合は直さず、持ち主のレーン（取り込み済みなら I-fix）へ bd と依頼文で回して待つ。

**Tech Stack:** Unity 6 / UI Toolkit（`Client.Ui`・`Client.Ui.State`）、NUnit（EditModeInPlayingTest）、プレイテスト DSL（`Client.Playtest`・`.agents/skills/unity-playmode-recorded-playtest`）、`Unity.Profiling.ProfilerRecorder`、Node（`tools/uitk/test-name-parity.mjs`）、Python（`.github/scripts/check-uitk-web-follow.py`）、bash、codex-audit（gpt-6-astra）、bd。

設計の出所: 実行計画 `docs/uitk-migration/execution/execution-plan.md` §1.4 の S4-gamma 行（完了条件の原文:「同時に出る組（playerInventory・subInventory・researchTree）の L3、ラダー `uitk-production-loop`・`uitk-research-train` を作りラダー 1〜5 緑（4 が production-loop・5 が research-train）、UITK 側で全録画（全本の 2 回目）、マスタのアンカー 9 件が実ゲームで解決、`UIElementsUpdatePanels`＋`RepaintPanels` を台帳に 1 回記録、束 4 の突き合わせ差分 0・G1 0・codex-audit」、依存「S4-* 全部」）、統合計画 `docs/uitk-migration/plan.md` §2.0・§2.5・§4.4・§4.5・§4.7、設計書 `docs/uitk-migration/execution/component-architecture.md` §3.3・§3.4。

## Requirements

| # | 要件 | 受け入れ基準 |
|---|---|---|
| R-1 | 同時に出る組の L3 | `Stage4ScreenSetGameUiTest` が全体スイッチ UITK の実ゲームで GameScreen → PlayerInventory → GameScreen → ResearchTree → GameScreen → SubInventory（`釜`を F で開く）→ GameScreen → PlayerInventory を巡り、各画面で「出るべき面だけが見え（表の通り）・各面が期待の列の子孫にあり・同名の面が 1 つだけで・面の中心を `panel.Pick` すると自分か子孫が拾われる」こと、ResearchTree で `research-tree` の左端が `inventory-panel` の右端以上で、左余白を畳むクラス `stage-columns--left-padding-collapsed` が付くこと、巡る間の LogError と例外が 0 件であることを PASS |
| R-2 | ラダー 4 `uitk-production-loop` を作る | 全体スイッチ UITK・Web ホスト無しで、Tab → アイテム一覧で木の板を選ぶ → クラフトを長押しして木の板が 2 枚以上増える → 青銅インゴットのレシピをサーバー直で解放し、石窯を置き F で開いてレシピを選ぶ → Shift＋クリックで青銅鉱石の粉を入れる・原木を右クリックで半分掴んで入力枠へ置く・右ドラッグで手持ちの空き枠へ配る → 開いたまま青銅インゴットが出力枠に現れる（機械 UI を開いたまま中身が更新される録画。§2.5）→ Shift で取り出す → 整理、が `result.json` `Success: true`。録画 mp4 がある |
| R-3 | ラダー 5 `uitk-research-train` を作る | 全体スイッチ UITK で、研究素材を渡す → 研究ツリーを開き `原始研究1` のノード → 研究ボタンで完了（サーバーの完了を待つ）→ 駅 2 基の線を準備 → 駅を F で開き駅名を日本語で `SetText`・確定し、サーバーの駅名が変わる → 機関車を開き時刻表に 2 駅を足し（サーバー 2 件）・自動運転を入れて走り出す、が `Success: true` |
| R-4 | ラダー 1〜5 が UITK で緑 | `uitk-lifeline`・`uitk-opening-hud`・`uitk-build-bp`・`uitk-production-loop`・`uitk-research-train` の 5 本が同じ HEAD の全体スイッチ UITK で `Success: true`（4 と 5 が §2.5 の「ラダー 2 本」） |
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

各レーンの計画（`docs/superpowers/plans/2026-11-10-uitk-s4-<lane>.md`。alpha・inventory・machine・train は PLAN-W4a の worktree〈de6f077a83 以降〉で起草、recipe・research・special はこの worktree）から写した。どれもマージ前の計画なので、Task 1 Step 3 でマージ済みの `done.md` と照らして差を足す。

| レーン | 完了条件（計画から） | gamma へ回した件・gamma が拾う件 | gamma での扱い |
|---|---|---|---|
| S4-alpha | ブロック UI の汎用族の UITK 化（`block-inventory`・`block-inventory-panel`・`block-inventory-close`・`<testId>-slot-<index>`）、SubInventory で `StageColumn.Viewer` 列、SlotCommands（`Client.Ui.State/SlotCommands/`） | integrator への依頼 (a) `Registration/GameUi/` のサブディレクトリ化、(b) `GamePanel` の題の右の逃げ 99px、(c) `SlotCommands` を設計書 §2 の State の表へ（A-142 の下書き） | (a) Task 1 Step 4 で直下と `Screens/` の本数を数え、alpha の `BlockInventoryGameUiRegistration.cs` 以外の S4 の画面の登録が `Screens/` にあるかを見る（E-1）。外れていれば bd を立て I-fix へ回す（gamma は他レーンの登録ファイルを動かさない。D-4）。(b) Task 13 の比べ方で special のロケットの差として照合し、未処理なら LX の文面を書く。(c) 設計書の行の有無だけ確かめる |
| S4-inventory | InventoryPanel・EquipmentPanel（`inventory-panel`・`inventory-sort`・`main-slot-<i>`・`equipment-slots`）、アンカーの印付け | 依頼 `TutorialAnchor.Unmark`（D-8）・`GameUiPointerProbe.PickAt`（D-11） | Foundation の口なので gamma は作らない。Task 11 で状態を確かめ、未処理なら bd を I-fix へ（無くても動く形で書かれているので段階 4 の完了は妨げない。帰結: 画面側の写しが 2 か所残る） |
| S4-machine | 機械の詳細・清浄室・流体枠・レシピ選択（`MachineRecipeSelectionRow` 等） | 依頼: `Client.Ui.State.asmdef` の参照、撮影表への 15 枚の追加（E-22 の自撮り） | asmdef は取り込み済みかを見るだけ。撮影表への追加は integrator の共有区画なので Task 11 で `docs/uitk/TODO.md`「## 参照キャプチャ」に自撮りの置き場を 1 行で記す |
| S4-train | 列車の本文（`train-tab-inventory`・`train-tab-timetable`）・時刻表の即時編集（適用ボタンを消し、保留中は `train-timetable-section--busy`）・駅名（`train-station-name-input`＋`train-station-name-apply`）・プラットフォーム。録画 `train-timetable-via-ui`・`train-station-default-name` を直す | integrator への依頼 (a) `PanelActionButton` の無効の見た目（画面のつまみで上書き中。D-11）、(b) 自撮りの参照（駅名の行・Loading/Unavailable・保留中・液体貨車）の撮影表への追加、(c) `Registration/GameUi/` の再編先 | (a) Task 11 で状態を確かめ、未処理なら bd を I-fix へ。(b) Task 11 Step 2 の参照の記載に含める。(c) S4-alpha の (a) と同じ扱い。ラダー 5 は即時送信の形で書く（Task 6） |
| S4-research | 研究ツリー（`research-tree`・`research-panel`・`research-viewport`・`research-node-<guid>`・`research-button-<guid>`・`research-detail-pane`）、録画 `uitk-research-tree`・`tier9-5` | L3 は「S4-ladder」＝gamma。D-9 `NodeGraphView` の `MarkClipContainer` は integrator への依頼（E-1・A-65 で子を切る容器へ印を付けるのは画面のレーンの義務）。tier9-5 が Web 前提の操作で落ちたら既存シナリオの横断は gamma。詳細ペインの自撮りの参照の台帳記載 | L3 は Task 3。`MarkClipContainer` は義務（E-1・A-65）なので、Task 11 Step 1 で研究の視口に印が無ければ bd を I-fix へ回し、入るまで「状態: 停止」で待つ（研究ノードのアンカーの画面外・切り抜きの判定の前提。D-19）。tier9-5 は Task 9 の全録画で分類（不具合なら research か I-fix へ）。参照の記載は Task 11 |
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
| S4-alpha（分け合うファイルあり） | `blockInventory.spec` 3・`blockInventoryGestures` 5・`blockRegistryCoverage` 1・`BlockInventoryPanel.localization` 2・`LackHighlightText` 2、分け合う `regression/sectionStack` 1（machine 1）・`SectionStackView.test` 8（machine 7・alpha 1）・`blockInventoryDesign` 16（machine 10・train 2・alpha 4） |
| S4-inventory | `inventory.spec` 4・`equipment.spec` 11・`InventoryPanel/index.test` 2・`EquipmentPanel/index.test` 14 |
| S4-machine | `blockDetails` 8（special と分け合う）・`machineGestures` 3・`machineRecipe` 10・`machineRequiredCount` 4・`cleanRoom/*` 8・`fluidSlot` 5・`MachineSection` 9・`SelectedRecipeHeader` 2・`ModuleSlotStackLimitText` 2・`CleanRoomSection` 4・`CleanRoomYield` 3・`MachineInputStall` 3・`MachineRecipeSelectionRow` 5 |
| S4-special | `electricPole` 1・`electricToGear` 1・`filterSplitter` 3・`spaceElevator/*` 8・`GearToElectric` 7＋i18n 1・`BeltThroughput` 3・`Gear` 3・`RocketLaunchPad` 6・`SpaceElevator*` 5 |
| S4-train | `trainPlatform` 2・`train.spec` 1・`train/timetable` 2・`TrainInventoryBody` 1・`TrainTimetableImmediate` 8・`TrainTimetableSection` 5・`TrainPlatformInventory` 1 |
| S4-recipe | `recipe.spec` 9・`itemListScroll` 6・`recipeListScroll` 2・`slotTooltip` 2・`useDragScroll` 2・`CraftRecipeEntry` 3・`MachineRecipeEntry.fluids` 1・`MachineRecipeEntry.localization` 1・`RecipeContent` 2 |
| S4-research | `research.spec` 12・`researchMachineUpgradeUnlock` 1・`researchViewport` 5・`tmp-icon-click` 1（research が「捨てる」）・`ResearchDetailPane` 11・`ResearchNodeCard` 2・`ResearchTreePanel` 2 |
| **持ち主が無い（gamma）** | `e2e/mock-host/detailActions.test.ts` 3 件 → Task 10 で処遇「捨てる」（mock-host の詳細操作の結線は Web の検証基盤で、UITK の写し先が無い。理由を行に書く） |

### やらないこと

- 画面・部品・Foundation・State・トークン USS・登録ファイルの中身を書き換えない。不具合と不足は持ち主か I-fix へ回す（Task 9・11 の手順）
- `.cs` を書くのは新規 7 ファイル（L3 の 3 本・`PlaytestTrainLine`・`PlaytestPanelCostRecorder`・`PlaytestAnchorChecks`・`PlaytestTrainStationSnapshots`）だけ。既存の `.cs` は変えない
- 壊れている 10 本（bd moorestech-wwxy。束 4 の `misc/research-node-state-label.cs`・`tutorial-research-renumber.cs` は X-prerun）を直さない。全録画では「wwxy」に分類する
- ラダー 6 `uitk-challenge-gate` と束 5 の画面（チャレンジ一覧・言語ゲート）を作らない
- 束 4 を `切替済み` にしない（切替 PR で全束一度）
- 録画・Unity のログ・テスト結果 xml を `moorestech_logs` に置かない（worktree の gitignore 下に置き、`done.md` に要約だけ）

## Global Constraints

- 土台: `origin/feature/uitk`（起草時 635d55f8e4。S2-tutorial #1627 は入っている）。ラダーの番号はこの計画では plan §4.4 の表の順（1 lifeline・2 opening-hud・3 build-bp・4 production-loop・5 research-train・6 challenge-gate）で呼ぶ。plan §2.6 は challenge-gate を「ラダー 5」と呼ぶので、食い違うときはシナリオ名が正。S4 の 7 レーン・S2-tutorial（PR #1627）・S2-ladder・S3-ladder が入っていることが着手の前提（Task 1 Step 1）。worktree は `moores-wt new uitk-lane/s4-gamma --from origin/feature/uitk`（環境ローカル規約）
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
- 新しい共有の申告: 検査用の口（`PlaytestTrainLine`・`PlaytestPanelCostRecorder`・`PlaytestAnchorChecks`）のうち、列車線と CPU 時間は DSL の Operations の新しいサブディレクトリ `Ladder/`（段の締めのラダーの準備と計測。名前空間は `ServerSnapshots/`・`ScenarioChecks/` と同じ平らな `Client.Playtest.Operations`）、アンカーの照合はシナリオ用の照合の前例 `ScenarioChecks/`（`PlaytestPlacementGuideChecks`）に置き、既存の Operations のファイルは変えない。サーバーの値は `ServerSnapshots/` の待ち（既存の `WaitForItemCount` 等と、足す `PlaytestTrainStationSnapshots`）で読み、メインスレッドから直に読まない。`RunOnServer` は書き込みにだけ使う。既存の `PlaytestDriver` に足したくなったら足さずに `done.md`「integrator への依頼」へ書く（`PlaytestDriver` は 199 行。A-111 の前例）

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反5件・修正5件 / サーバーの値の直読み・品目と見え方の自前実装・アンカー照合の置き場・死活表の読み手 2 つを直した
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強4・弱3・第3バケツ0 / Key? の不在を判別子に使う形を直し、ADR との食い違い 3 件（LX の追記者・並べ比べの判定者・MarkClipContainer の義務）を D-17〜D-19 で裁き、弱 3 件は D-20 でこのまま。シミュレーター反映後に変えた範囲を fresh-context で再検査（強1・弱4）し、駅名の待ちを ServerSnapshots の前例の形へ、枠の測り方を自分の入れ物と切り抜き後の見える範囲へ、§3.3 の改め案を足して全件直した。独立評価 2 体（opus 7 件・codex 4 件）の指摘を直した範囲を fresh-context で再検査（Phase 1〜2.6 の発火 0、故障シナリオ強1・弱4: 比較元の引き方・分岐点の引き方・枠の session の入れ替わり・D-23 の前提・消える worktree への grep）し全件直した
- 2 周目の独立評価の指摘（opus N1〜N4・codex 1〜2）と指揮の追加（登録ファイルの `Screens/`）を直した範囲を fresh-context で再検査: 6 件は実コードと照らして解消を確認、新規 1 件（参照の撮り直しで `capture.ts` の viewport が 1280×720 固定・node_modules 無し）を pnpm install/build・UITK_REFERENCE_OUT・使い捨て worktree 内だけの viewport 書き換えで直した
- 3 周目の独立評価の指摘（codex: `resolvedStyle.overflow` が `IResolvedStyle` に無くコンパイルできない／opus: alpha の直下の 1 本の除外・`manifest.json` の viewport・`moores-wt rm` の名前と `--prune-branch`）を直した: 自前のクリップ判定を消して本番の `TutorialAncestorClipWalker.ClipOf`（public static）へ寄せ、`Client.Playtest.asmdef` に `Client.Ui.State` を足す（実物で公開範囲・asmdef の参照・循環の無さを確かめた）

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
| `tools/uitk/testNameParity/ledgerBundles.mjs`（台帳の束の表・`## 行` を読む） | 生きる | Task 2 で束 4 の状態の列だけを `暗転` にし、行の形は変えない。Task 10・Task 15 Step 2 で parity を流して読めることを見る |
| `LookExceptionLedgerTest`（台帳の `## 見た目の例外` を読む） | 生きる | gamma はその節を書かない（LX の行と印は I-fix が置く）。Task 12 の取り込み後のテストで巻き込まれていないことを見る |

## File Structure

| パス | 新規/変更 | 責務 |
|---|---|---|
| `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/Stage4/Stage4ScreenSetGameUiTest.cs` | 新規 | L3。本番の遷移で 4 画面を巡り、各画面で `Stage4ScreenSetProbe` の照合を呼ぶ。Error ログを数える |
| `…/Ui/Stage4/Stage4ScreenSetProbe.cs` | 新規 | 根の木から面を名前で引き、見えているか・どの列の子孫か・同名の数・中心の Pick を返す純粋な関数 |
| `…/Ui/Stage4/Stage4ExpectedSurfaces.cs` | 新規 | 画面ごとに見えるべき面と、面ごとの列の名前（Web の `App.tsx` と `UiScreenTraitsTable` から写した表） |
| `moorestech_client/Assets/Scripts/Client.Playtest/Operations/Ladder/PlaytestTrainLine.cs` | 新規 | 駅 2 基・結線・機関車の解放と設置・燃料・列車の取得（既存 `train-timetable-via-ui.cs` の準備の写し。駅名は付けない） |
| `…/Operations/Ladder/PlaytestPanelCostRecorder.cs` | 新規 | `ProfilerRecorder` で UI のマーカーを N フレーム読み、平均・p95・最大を返す |
| `…/Operations/ScenarioChecks/PlaytestAnchorChecks.cs` | 新規 | マスタの `uiHighLight` を集め、アンカーの解決と枠の位置を確かめる |
| `moorestech_client/Assets/Scripts/Client.Playtest/Client.Playtest.asmdef` | 変更 | references に `Client.Ui.State` を足す（アンカーの見える範囲を本番の `TutorialAncestorClipWalker.ClipOf` で求め、その戻り値の型 `TutorialClipRect` が `Client.Ui.State` にあるため。Task 4 Step 3 の注記） |
| `…/Operations/ServerSnapshots/PlaytestTrainStationSnapshots.cs` | 新規 | 駅名をサーバーの tick で読み、待つ（前例 `PlaytestTrainSnapshots`。起草時 8 本 → 9 本） |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage4/uitk-production-loop.cs` | 新規 | ラダー 4 |
| `…/scenarios/uitk/stage4/uitk-research-train.cs` | 新規 | ラダー 5 |
| `…/scenarios/uitk/stage4/uitk-master-anchors.cs` | 新規 | アンカー 9 件 |
| `…/scenarios/uitk/stage4/uitk-stage4-panel-cost.cs` | 新規 | CPU 時間（録画しない） |
| `…/scenarios/uitk/stage4/uitk-stage4-wave-shots.cs` | 新規 | 同時に出る 3 画面の撮影（録画しない。1280×720 と 2560×1080） |
| `tools/uitk/run-all-recordings.sh` | 新規 | 一覧のシナリオを順に `run-scenario.sh` で流し、1 本 1 行の TSV を書く |
| `tools/uitk/list-recordings.sh` | 新規 | 系ごとの全本の一覧を作る（wwxy のうち X-prerun が直す行を除き、Web では UITK 専用の `scenarios/uitk/` も除く。外した名前は標準エラーへ） |
| `docs/uitk/TODO.md` | 変更（共有区画） | 束 4 の行（暗転・起点）、「## 追従記録」の束 4 の行、「## 実測の結論」の 3 行（CPU・全録画 UITK・全録画 Web）、「## 参照キャプチャ」の自撮りの置き場 |
| `docs/uitk/disposition.md` | 変更（共有区画） | `detailActions.test.ts` 3 件の処遇の行、「## 突き合わせの記録」の 1 行 |

ディレクトリのファイル数: `Ui/Stage4/` 3、`Operations/Ladder/` 2、`Operations/ScenarioChecks/` 1→2、`Operations/ServerSnapshots/` 8→9、`scenarios/uitk/stage4/` 5（gamma の新規シナリオは全部ここ。`scenarios/uitk/` 直下は起草時 10 本で、そこへは足さない。D-24）。`tools/uitk/` は Task 1 Step 4 で数え、10 を超えるなら `tools/uitk/recordings/` へ 2 本を置く。

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
Expected: マージコミットが 10 本（S4 の 7・S2-tutorial・S2-ladder・S3-ladder）、ラダーのシナリオ 3 本がある、`TutorialAnchorCoveragePending.cs` に `S4-` の持ち主の行が 0（S4 の各レーンが自分の行を消している）。どれかが欠けていたら `done.md` の 1 行目を「状態: 停止」にし、欠けたものを書いて指揮へ返す（S2-ladder・S3-ladder は実行計画の DAG で S4-gamma の依存に無いが、ラダー 1〜5 緑に要る。末尾の §1.4 の改め案）。Pending の行が残っていたら、その持ち主のレーンへの依頼として `done.md`「integrator への依頼」に書き、Task 7 はその行のアンカーを「持ち主の未処理」として扱う

- [ ] **Step 2: 計画の名前と実物の名前を照合する**

Run:
```bash
for n in inventory-panel inventory-sort equipment-slots recipe-viewer item-list-panel item-list-scroll craft-button- craft-recipe-box- research-tree research-panel research-viewport research-node- research-button- research-detail-pane block-inventory block-inventory-close train-station-name-input train-station-name-apply train-tab-timetable train-timetable-auto-run-on stage-column-inv stage-column-viewer stage-column-items stage-columns--left-padding-collapsed screen-backdrop tutorial-outline challenge-hud; do
  printf "%s\t%s\n" "$n" "$(git grep -l -- "\"$n" -- 'moorestech_client/Assets/Scripts/Client.Ui*' | head -2 | tr '\n' ' ')"
done
grep -rn "item-list-cell-\|main-slot-\|-slot-\"" moorestech_client/Assets/Scripts/Client.Ui/Screens | head
```
Expected: 各名前が 1 か所以上で見つかる。見つからない名前は実物の名前を探し、`done.md`「## 照合表」に「計画の名前 → 実物の名前（ファイル:行）」で書き、Task 3〜8 のコード片を実物の名前で写す（gamma のファイルの側を直す。D-5）。時刻表の適用ボタン `train-timetable-apply` は S4-train が消している（起草版 R-9。在れば Task 6 の即時送信の待ちは不要になるので S4-train の `done.md` と照らす）

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
ls moorestech_client/Assets/Scripts/Client.Starter/Registration/GameUi/Screens/*.cs | wc -l
ls tools/uitk/ | wc -l
ls moorestech_client/Assets/Scripts/Client.Playtest/Operations/
grep -n "\"石窯\"\|\"蒸気機関車駅\"" -r ../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json | head -3
grep -n "ProfilerRecorder\|UIElementsUpdatePanels\|RepaintPanels" -r docs/uitk-migration/poc | head -5
git grep -n "MarkClipContainer" -- moorestech_client/Assets/Scripts/Client.Ui | head -5
```
Expected: W4 以降の画面の登録ファイルは `Registration/GameUi/Screens/` に置く決まり（実行計画 E-1。S4-alpha で直下が 10 本になったため）。直下は S4-alpha の `BlockInventoryGameUiRegistration.cs` を含めて 10 本まで（E-1 が認めた 1 本）、`Screens/` も 10 本以下で、それ以外の S4 のレーンが足した画面の登録ファイルが `Screens/` にあること。alpha の 1 本の他に S4 の画面の登録ファイルが直下に増えている、またはどちらかが 10 を超えていれば bd を立てて I-fix へ回す（gamma は動かさない。D-4）。`tools/uitk/` が 8 以下なら Task 9 の 2 本をそのまま置き、9 以上なら `tools/uitk/recordings/` に置いて以後のパスを読み替える。`Operations/` に `Ladder/` が無いこと。マスタに `石窯` と `蒸気機関車駅` がある（名前が違えば照合表へ）。`MarkClipContainer` の印が研究の視口（`NodeGraphView` か `research-viewport`）に付いている。無ければ bd を I-fix へ回し、Task 7（アンカー）の前で「状態: 停止」にして待つ（E-1・A-65 の義務。D-19）

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

起点は「束のレーンを始めたとき取り込んであった最新の master」（台帳の運用）。S4 は 2 列（alpha 列と research 列）が並走したので、7 レーンのうち最も早く分岐したレーンの分岐点で取り込み済みだった master を起点にする（D-22）。指揮が W4 の頭に起点を `conductor.md` へ記録していればそれを使い、無ければ次で引く:

Run:
```bash
grep -n "束 4.*起点\|暗転の起点" ../moorestech_logs/harness/uitk/conductor.md | tail -3
for lane in alpha inventory machine train research recipe special; do
  m=$(git log --first-parent --merges --format=%H origin/feature/uitk --grep "from moorestech/uitk-lane/s4-$lane" | tail -1)
  first=$(git rev-list --reverse --first-parent "$m^2" --not "$m^1" | head -1)
  fork=$(git rev-parse "$first^")
  printf "%s\t%s\t%s\n" "$lane" "$(git merge-base "$fork" origin/master)" "$(git log -1 --format=%ci "$fork")"
done | tee moorestech_client/Temp/s4-gamma-forks.tsv
git merge-base --octopus $(cut -f2 moorestech_client/Temp/s4-gamma-forks.tsv) | cut -c1-10
```
Expected: 7 行と、それらの master 側の点の共通の祖先（最も古い点）の 10 桁。`for` の 2 列目が全部同じ直線上にあれば最も古いものと一致する。どちらを使ったか（指揮の記録か計算か）と 7 行を `done.md`「## 証拠」に書く。束の表の束 4 の行を `| 4 | 持ち物の束（…） | 暗転 | <起点> | \`uitk-production-loop\`・\`uitk-research-train\` |` に書き換える（中身の列はそのまま）

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
- Produces: `Stage4ExpectedSurfaces.Expected(UIStateEnum)`（`IReadOnlyList<Stage4Surface>`）・`Stage4ExpectedSurfaces.All`、`Stage4Surface.InColumn(string name, string columnName)`・`Stage4Surface.OutsideColumns(string name)`（`Name`・`ExpectsColumn`・`ColumnName`）、`Stage4ScreenSetProbe.IsVisible(VisualElement)`・`VisibleByName(VisualElement root, string name)`（`List<VisualElement>`）・`TryGetColumnName(VisualElement, out string)`・`CountByName(VisualElement root, string name)`・`IsPickedAtCenter(VisualElement)`

期待の表は Web の `App.tsx`（InventoryPanel は `screenAllowsGrab`、RecipeViewer と ItemListPanel は `playerInventory`、ResearchTreePanel は `researchTree`、BlockInventoryPanel は SubInventory、Equipment は `alwaysOnHud`）と `UiScreenTraitsTable`（PlayerInventory・SubInventory は grab・backdrop・alwaysOnHud、ResearchTree は grab・backdrop で alwaysOnHud なし、GameScreen は alwaysOnHud）から写す。列の名前の空文字は「どの列の子孫でもない」（研究エリアは `OverlayPanel`/`Stage` 層、装備は `Chrome`/`Viewport` 層、幕は幕の層）。

- [ ] **Step 1: 期待の表を書く**

```csharp
using System.Collections.Generic;
using Client.Game.InGame.UI.UIState;

namespace Client.Tests.EditModeInPlayingTest.Ui.Stage4
{
    // 面の名前と、その面が載るべき列（列の外に出る面は ExpectsColumn が偽。空文字で表さず書き忘れと区別する）
    // A surface name and its stage column (ExpectsColumn is false outside every column; no empty string, so a missing name is distinguishable)
    public readonly struct Stage4Surface
    {
        public readonly string Name;
        public readonly bool ExpectsColumn;
        public readonly string ColumnName;

        private Stage4Surface(string name, bool expectsColumn, string columnName)
        {
            Name = name;
            ExpectsColumn = expectsColumn;
            ColumnName = columnName;
        }

        public static Stage4Surface InColumn(string name, string columnName)
        {
            return new Stage4Surface(name, true, columnName);
        }

        // 列の外の面。ColumnName は読まない（ExpectsColumn が偽のとき AssertScreenSet は列が無いことだけを見る）
        // A surface outside every column; ColumnName is never read (AssertScreenSet only checks that no column holds it)
        public static Stage4Surface OutsideColumns(string name)
        {
            return new Stage4Surface(name, false, null);
        }
    }

    // 画面ごとに見えるべき面の表（Web の App.tsx と UiScreenTraitsTable の写し）
    // Which surfaces each screen shows (copied from the web App.tsx and UiScreenTraitsTable)
    public static class Stage4ExpectedSurfaces
    {
        public static readonly Stage4Surface Inventory = Stage4Surface.InColumn("inventory-panel", "stage-column-inv");
        public static readonly Stage4Surface RecipeViewer = Stage4Surface.InColumn("recipe-viewer", "stage-column-viewer");
        public static readonly Stage4Surface ItemList = Stage4Surface.InColumn("item-list-panel", "stage-column-items");
        public static readonly Stage4Surface BlockInventory = Stage4Surface.InColumn("block-inventory", "stage-column-viewer");
        public static readonly Stage4Surface ResearchTree = Stage4Surface.OutsideColumns("research-tree");
        public static readonly Stage4Surface Equipment = Stage4Surface.OutsideColumns("equipment-slots");
        public static readonly Stage4Surface Backdrop = Stage4Surface.OutsideColumns("screen-backdrop");

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
using System.Collections.Generic;
using Client.Playtest.UiElements.GameUi;
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

        public static List<VisualElement> VisibleByName(VisualElement root, string name)
        {
            // 同名の全件から見えるものだけを残す（DSL のロケータと同じ全件の照会）
            // Keep only the visible ones among every same-named element (same full query as the DSL locator)
            var all = root.Query(name: name).ToList();
            all.RemoveAll(element => !IsVisible(element));
            return all;
        }

        // 見えるかの判定は DSL のロケータと同じ 1 か所を使う（InternalsVisibleTo("Client.Tests")）
        // Use the DSL locator's single visibility rule (InternalsVisibleTo("Client.Tests"))
        public static bool IsVisible(VisualElement element)
        {
            return GameUiElementQuery.IsVisible(element);
        }

        public static bool TryGetColumnName(VisualElement element, out string columnName)
        {
            // 祖先の列を探し、列の外なら偽を返す（空文字を「列の外」の合図に使わない）
            // Find the ancestor column; return false outside every column (no empty string as the outside signal)
            for (var current = element.parent; current != null; current = current.parent)
            {
                if (current.name == null || !current.name.StartsWith(ColumnNamePrefix)) continue;
                columnName = current.name;
                return true;
            }
            columnName = null;
            return false;
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
using Client.Tests.EditModeInPlayingTest.BlockInventory;
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
                await WaitScreenSettled(root, UIStateEnum.PlayerInventory);
                AssertScreenSet(root, UIStateEnum.PlayerInventory);
                await TransitionTo(uiStateControl, UIStateEnum.GameScreen);
                await WaitScreenSettled(root, UIStateEnum.GameScreen);
                AssertScreenSet(root, UIStateEnum.GameScreen);
                await TransitionTo(uiStateControl, UIStateEnum.ResearchTree);
                await WaitScreenSettled(root, UIStateEnum.ResearchTree);
                AssertScreenSet(root, UIStateEnum.ResearchTree);
                AssertResearchTreeRightOfInventory(root);
                await TransitionTo(uiStateControl, UIStateEnum.GameScreen);

                // 機械は F キーの注入で開き、閉じた後の持ち物に残骸が無いことを見る
                // Open the machine by injected F and check the inventory has no leftovers after closing
                await InteractKeyBlockOpener.PlaceNearPlayerAndOpen(MachineBlockName);
                await WaitScreenSettled(root, UIStateEnum.SubInventory);
                AssertScreenSet(root, UIStateEnum.SubInventory);
                await InteractKeyBlockOpener.CloseToGameScreen();
                await TransitionTo(uiStateControl, UIStateEnum.PlayerInventory);
                await WaitScreenSettled(root, UIStateEnum.PlayerInventory);
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

        // 開閉の動きが落ち着くまで待つ上限のフレーム数（動きは数十フレームで終わるので余裕を持たせた値）
        // Frame cap for waiting until open/close animations settle (they finish in tens of frames, so this leaves headroom)
        private const int MaxSettleFrames = 120;

        private static async UniTask WaitScreenSettled(VisualElement root, UIStateEnum state)
        {
            // 開閉の動きの途中は不透明度が 0 と 1 の間なので、出る面が見え出ない面が消えるまで待つ
            // Mid open/close animations sit between 0 and 1 opacity, so wait until shown surfaces appear and hidden ones vanish
            var expected = Stage4ExpectedSurfaces.Expected(state);
            for (var i = 0; i < MaxSettleFrames; i++)
            {
                var settled = Stage4ExpectedSurfaces.All.All(s => Stage4ScreenSetProbe.VisibleByName(root, s.Name).Count == (expected.Contains(s) ? 1 : 0));
                if (settled) return;
                await UniTask.Yield();
            }
            // silent-ok: MaxSettleFrames で落ち着かなくても続ける / 直後の AssertScreenSet がどの面かを名前付きで落とす
        }

        private static void AssertScreenSet(VisualElement root, UIStateEnum state)
        {
            var expected = Stage4ExpectedSurfaces.Expected(state);
            foreach (var surface in Stage4ExpectedSurfaces.All)
            {
                var shouldShow = expected.Contains(surface);
                var visible = Stage4ScreenSetProbe.VisibleByName(root, surface.Name);

                // 同名の要素を全件見て、出ない面は可視が 0 件、出る面は可視がちょうど 1 件（先頭だけを見ると残骸を見逃す）
                // Check every same-named element: hidden surfaces have zero visible, shown ones exactly one (the first match alone misses leftovers)
                if (!shouldShow)
                {
                    Assert.AreEqual(0, visible.Count, $"{state}: {surface.Name} が {visible.Count} 個見えている");
                    continue;
                }
                Assert.AreEqual(1, visible.Count, $"{state}: 見える {surface.Name} が {visible.Count} 個（全件 {Stage4ScreenSetProbe.CountByName(root, surface.Name)}）");
                var shown = visible[0];
                AssertColumn(state, surface, shown);

                // 幕は入力を素通しするので Pick の照合から外す
                // The backdrop lets input through, so skip it in the pick check
                if (surface.Name == Stage4ExpectedSurfaces.Backdrop.Name) continue;
                Assert.IsTrue(Stage4ScreenSetProbe.IsPickedAtCenter(shown), $"{state}: {surface.Name} の中心で {Stage4ScreenSetProbe.DescribePicked(shown)} が拾われた（覆われている）");
            }
        }

        // 列に載るべき面はその列の子孫に、列の外の面はどの列の子孫でもないことを見る
        // A column surface must sit under its column; an outside surface must sit under no column
        private static void AssertColumn(UIStateEnum state, Stage4Surface surface, VisualElement shown)
        {
            var inColumn = Stage4ScreenSetProbe.TryGetColumnName(shown, out var columnName);
            if (!surface.ExpectsColumn)
            {
                Assert.IsFalse(inColumn, $"{state}: {surface.Name} は列の外のはずが {columnName} にある");
                return;
            }
            Assert.IsTrue(inColumn, $"{state}: {surface.Name} がどの列にも無い（期待 {surface.ColumnName}）");
            Assert.AreEqual(surface.ColumnName, columnName, $"{state}: {surface.Name} の列が違う");
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
- Create: `moorestech_client/Assets/Scripts/Client.Playtest/Operations/ScenarioChecks/PlaytestAnchorChecks.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Playtest/Operations/ServerSnapshots/PlaytestTrainStationSnapshots.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Playtest/Client.Playtest.asmdef`（references に `"Client.Ui.State"` を 1 行足す）

**Interfaces:**
- Consumes: `PlaytestDriver`（`SetupFlatGround`・`WarpPlayer`・`PlaceBlockDirect`・`WaitBlockGameObject`・`GetBlock`・`RunOnServer`・`Until`・`Assert`・`GiveItemDirect`・`SendCommand`・`WaitSeconds`・`AimAt`）、`RailComponent`・`TrainStationComponent`・`StationNodeRole`、`ClientContext.VanillaApi.Response.PlaceTrainOnRail`、`ITrainUnitLookupDatastore`、`MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements`、`TutorialsElement.TutorialTypeConst.uiHighLight`・`UiHighLightTutorialParam.HighLightAnchorId`、`TutorialPresentationStateStore.Instance.AddOutlineHighlight(string, Guid)`・`ITutorialView.CompleteTutorial()`、`TutorialAnchorRegistry.Resolve(VisualElement, string)`、`TutorialAncestorClipWalker.ClipOf(VisualElement, VisualElement)` → `TutorialClipRect`（`ContainsRect`・`Left`/`Top`/`Right`/`Bottom`）、`GameUiRoot.Instance.GetRootElement()`、`ProfilerRecorderHandle.GetAvailable`・`ProfilerRecorder.StartNew`
- Produces:
  - `PlaytestTrainLineSetup`（`IBlock StationA`・`IBlock StationB`・`Vector3Int StationAPosition`・`StationBPosition`・`TrainUnit Train`）、`PlaytestTrainLine.Build(PlaytestDriver p, Vector3Int stationAPosition, Vector3Int stationBPosition)` → `UniTask<PlaytestTrainLineSetup>`、`PlaytestTrainLine.AimAtCar(PlaytestDriver p)`、`PlaytestTrainLine.AimAtStation(PlaytestDriver p, Vector3Int stationPosition)`
  - `PlaytestTrainStationSnapshots.ReadStationName(this PlaytestDriver driver, IBlock station)` → `UniTask<string>`、`PlaytestTrainStationSnapshots.WaitForStationName(this PlaytestDriver driver, IBlock station, string expected, float timeoutSeconds, string label)`
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

namespace Client.Playtest.Operations
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

駅名の待ちはサーバーの値の待ちの前例 `ServerSnapshots/PlaytestTrainSnapshots.cs`（`PlaytestServerThreadAction.RunAsync` で読み、`BeginWait`／`CheckTimeout` で待つ）と同じ形で `ServerSnapshots/PlaytestTrainStationSnapshots.cs` に置く（`RunOnServer` は書き込み用で、呼ぶたびに録画のオーバーレイへ行を出すので待ちに使わない）:

```csharp
using Client.Playtest.Core;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;

namespace Client.Playtest.Operations
{
    public static class PlaytestTrainStationSnapshots
    {
        // 駅名をサーバーの tick で読む
        // Read the station name on the server tick
        public static async UniTask<string> ReadStationName(this PlaytestDriver driver, IBlock station)
        {
            var name = "";
            await PlaytestServerThreadAction.RunAsync(driver.CancellationToken, "駅名を複製する", () => name = station.GetComponent<TrainStationComponent>().StationName);
            return name;
        }

        public static async UniTask WaitForStationName(this PlaytestDriver driver, IBlock station, string expected, float timeoutSeconds, string label)
        {
            var wait = driver.BeginWait(timeoutSeconds, label);
            while (true)
            {
                var name = await driver.ReadStationName(station);
                if (name == expected) { wait.Complete(); return; }
                wait.CheckTimeout($"expected={expected}; actual={name}");
                await UniTask.Yield();
            }
        }
    }
}
```

- [ ] **Step 2: CPU 時間の読み取りを書く**

```csharp
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;

namespace Client.Playtest.Operations
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
using Client.Ui.Screens.Tutorial.Anchor;
using Client.Ui.State.Tutorial.Logic;
using Core.Master;
using Cysharp.Threading.Tasks;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Playtest.Operations
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
        private const string OutlineRingClass = "tutorial-outline__ring";
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

            // 本番と同じ入口で枠を出す。本番のチャレンジが既に枠を出しうるので、足す前の枠を控えて増えた 1 件だけを自分の枠とする
            // Show the frame via the production entry; production may already show frames, so snapshot first and take only the added one
            var before = new HashSet<VisualElement>(root.Query(OutlineName).ToList());
            var view = TutorialPresentationStateStore.Instance.AddOutlineHighlight(anchor.AnchorId, anchor.TutorialGuid);
            VisualElement own = null;
            await p.Until(() => (own = root.Query(OutlineName).ToList().FirstOrDefault(e => !before.Contains(e))) != null, 5f, $"{anchor.AnchorId} の枠が 1 つ増える");
            await UniTask.DelayFrame(2);
            var ring = own.Q(className: OutlineRingClass);

            // 見える範囲は本番の枠と同じ判定（TutorialAncestorClipWalker）で根の座標に取り、アンカーが収まるときだけ中心を比べる
            // Take the visible region with the production walker in root space and compare centers only when the anchor fits in it
            var anchorBound = root.WorldToLocal(unique.Element.worldBound);
            var visible = TutorialAncestorClipWalker.ClipOf(unique.Element, root);
            var fullyVisible = visible.Expand(0.5f).ContainsRect(new TutorialClipRect(anchorBound.xMin, anchorBound.yMin, anchorBound.xMax, anchorBound.yMax));
            if (fullyVisible)
            {
                var distance = Vector2.Distance(root.WorldToLocal(ring.worldBound).center, anchorBound.center);
                p.Assert(distance <= CenterTolerancePx, $"{anchor.AnchorId} の枠の中心がアンカーの中心から {distance:F1}px（{CenterTolerancePx}px 以内）");
            }
            else
            {
                p.Note($"{anchor.AnchorId} は画面外か切り抜きの容器で一部が隠れている（{anchorBound} のうち見える範囲 {visible.Left:F0},{visible.Top:F0}-{visible.Right:F0},{visible.Bottom:F0}）。枠の位置は見ない");
            }
            await p.Screenshot($"anchor-{anchor.AnchorId}");

            // 完了で自分の枠だけが木から外れ、控えた本番の枠は残る
            // Completing detaches only our frame; the snapshotted production frames stay
            view.CompleteTutorial();
            await p.Until(() => own.panel == null, 5f, $"{anchor.AnchorId} の自分の枠が消える");
            p.Assert(before.All(e => e.panel != null), $"{anchor.AnchorId} の完了で本番の枠を消していない");
        }
    }
}
```
見える範囲は自前で求めない（USS の `overflow` は `IResolvedStyle` に無く C# から読めない。設計書 component-architecture.md の MarkClipContainer の項）。本番の枠と同じ `TutorialAncestorClipWalker.ClipOf(VisualElement, VisualElement)`（public static。ScrollView の viewport・`MarkClipContainer` の印・インラインの `overflow` で判定）を使うので、`Client.Playtest.asmdef` の references に `Client.Ui.State`（`TutorialClipRect` の在りか）を足す。`tutorial-outline` は大きさの無い absolute の入れ物で（`TutorialOverlay.uss`・`TutorialOverlayView.cs`）、箱の位置と大きさは子の `tutorial-outline__ring`（クラス）が持つ（`TutorialOutlineView.cs`。S2-tutorial のテストも子で測る）。出現・消滅は入れ物の有無で、中心は ring の `worldBound` で見る。`TutorialPresentationStateStore` の名前空間は `Client.Game.InGame.Tutorial`（起草時の実物）。

- [ ] **Step 4: コンパイルして行数を見る**

Run:
```bash
uloop compile --project-path ./moorestech_client
wc -l moorestech_client/Assets/Scripts/Client.Playtest/Operations/Ladder/*.cs moorestech_client/Assets/Scripts/Client.Playtest/Operations/ScenarioChecks/PlaytestAnchorChecks.cs
```
Expected: `ErrorCount: 0`、3 本とも 200 行未満。`Func<>`・`partial`・`try` が無いことを `grep -n "Func<\|partial \|try" moorestech_client/Assets/Scripts/Client.Playtest/Operations/Ladder/*.cs moorestech_client/Assets/Scripts/Client.Playtest/Operations/ScenarioChecks/PlaytestAnchorChecks.cs` が 0 件で見る

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Playtest/Client.Playtest.asmdef moorestech_client/Assets/Scripts/Client.Playtest/Operations/Ladder moorestech_client/Assets/Scripts/Client.Playtest/Operations/ScenarioChecks/PlaytestAnchorChecks.cs moorestech_client/Assets/Scripts/Client.Playtest/Operations/ServerSnapshots/PlaytestTrainStationSnapshots.cs
git commit -m "feat(playtest): ラダー用の列車線・駅名の待ち・CPU 時間・アンカー照合の口を足す（S4-gamma）" -- moorestech_client/Assets/Scripts/Client.Playtest/Client.Playtest.asmdef moorestech_client/Assets/Scripts/Client.Playtest/Operations/Ladder moorestech_client/Assets/Scripts/Client.Playtest/Operations/ScenarioChecks moorestech_client/Assets/Scripts/Client.Playtest/Operations/ServerSnapshots
```

---

### Task 5: ラダー 4 `uitk-production-loop`

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage4/uitk-production-loop.cs`

**Interfaces:**
- Consumes: Task 1 の照合表の名前（`item-list-cell-<id>`・`craft-button-<guid>`・`machine-recipe-<guid>`・`machine-input-slots`・`machine-input-slot-<i>`・`machine-output-slots`・`machine-output-slot-<i>`・`main-slot-<i>`・`inventory-sort`・`block-inventory-close`）、`PlaytestUiElementOps.FindUi(string)`（`ScreenRect`）、`SemanticInput`、`ILocalPlayerInventory`、`PlaytestItemOps.CountItemClientSide(ItemId)`・`ResolveItemId(string)`、`PlaytestBlockSnapshots.WaitForItemCount`、`IGameUnlockStateDataController.UnlockMachineRecipe(Guid)`・`IGameUnlockStateData.MachineRecipeUnlockStateInfos`
- Produces: シナリオ `uitk-production-loop`（Task 12 の通しが使う）

マスタの値（e0c913033c）: 木の板 ← 原木×1（0.75s、craftRecipeGuid `37623c6d-e1cf-4985-abfb-8239e6e33981`）。石窯（`26096ad0-d355-4e2c-9e45-a5c5e91856e0`、ElectricMachine、requiredPower 0、入力 2・出力 1）のレシピ `af78bd9a-39c9-440a-9b43-61dca495f8fb` 青銅鉱石の粉 1＋原木 1 → 青銅インゴット 1（3s。`initialUnlocked: false`、原始研究 2 で解放）。

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
using Game.Block.Interface.Extension;
using Game.Context;
using Game.UnlockState;
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

    var log = PlaytestItemOps.ResolveItemId("原木");
    var plank = PlaytestItemOps.ResolveItemId("木の板");
    var bronzeDust = PlaytestItemOps.ResolveItemId("青銅鉱石の粉");
    var ingot = PlaytestItemOps.ResolveItemId("青銅インゴット");
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

    // 青銅インゴットのレシピをサーバー直で解放する（未解放・未選択の機械は入力を受けない。研究の流れはラダー 5 が持つ）
    // Unlock the bronze ingot recipe server-side (a locked or unselected machine takes no input; ladder 5 owns the research flow)
    var bronzeRecipe = Guid.Parse("af78bd9a-39c9-440a-9b43-61dca495f8fb");
    var clientUnlock = ClientDIContext.DIContainer.DIContainerResolver.Resolve<IGameUnlockStateData>();
    await p.RunOnServer("青銅インゴットのレシピを解放する", () => ServerContext.GetService<IGameUnlockStateDataController>().UnlockMachineRecipe(bronzeRecipe));
    await p.Until(() => clientUnlock.MachineRecipeUnlockStateInfos.TryGetValue(bronzeRecipe, out var info) && info.IsUnlocked, 10f, "レシピの解放がクライアントへ届く");

    // 石窯を置いて F で開き、レシピを選ぶ
    // Place a stone kiln, open it with F and select the recipe
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
    await p.Screenshot("02-kiln-recipe-select");
    await p.ClickUi($"machine-recipe-{bronzeRecipe}");
    await p.UntilUiElement("machine-input-slots", 10f);
    await p.Screenshot("03-kiln-open");

    // Shift＋クリックで粉を入れ、原木は右クリックで半分掴んで素材順の 2 枠目へ置く（入力枠はレシピの素材順に固定）
    // Shift-click the dust in, then right-click to grab half the logs and drop them in the second slot (inputs follow recipe order)
    p.Note("Shiftで入れる・半分掴んで置く");
    SemanticInput.KeyDown(Key.LeftShift);
    await p.ClickUi(MainSlotOf(bronzeDust));
    SemanticInput.KeyUp(Key.LeftShift);
    await RightClickOn(MainSlotOf(log));
    await p.ClickUi("machine-input-slot-1");
    var kilnInventory = p.GetBlock(kilnPos).GetComponent<IOpenableBlockInventoryComponent>();
    await p.WaitForItemCount(kilnInventory, log, 1, null, 10f, "石窯の入力に原木が入る（サーバー）");
    await p.WaitForItemCount(kilnInventory, bronzeDust, 1, null, 10f, "石窯の入力に粉が入る（サーバー）");

    // 残りの原木を掴み右ドラッグで空き 3 枠へ配る
    // Grab the remaining logs and right-drag them across three empty slots
    p.Note("右ドラッグで配る");
    p.Assert(Enumerable.Range(0, Local().MainSlotCount).Any(i => Local()[i].Id == log), "半分掴みの後も手持ちに原木が残る");
    await p.ClickUi(MainSlotOf(log));
    var empties = EmptyMainSlots(3);
    await DragWithButton(1, empties);
    p.Assert(empties.All(i => Local()[i].Id == log), "右ドラッグで空き3枠に原木が配られる");
    await p.Screenshot("04-right-drag");
    await p.ClickUi(MainSlotOf(log));

    // 開いたまま出来上がりが出力枠に出るのを見る（機械 UI を開いたまま中身が更新される録画）
    // Watch the ingot appear in the output while the UI stays open
    await p.WaitForItemCount(kilnInventory, ingot, 1, null, 20f, "石窯で青銅インゴットができる（サーバー）");
    await PlaytestUiElementOps.WaitUiTextContains("machine-output-slots", "1", 10f);
    await p.Screenshot("05-output-while-open");

    // Shift で取り出し、閉じて整理する
    // Shift-take the output, close and sort
    var ingotsBefore = PlaytestItemOps.CountItemClientSide(ingot);
    SemanticInput.KeyDown(Key.LeftShift);
    await p.ClickUi("machine-output-slot-0");
    SemanticInput.KeyUp(Key.LeftShift);
    await p.Until(() => ingotsBefore < PlaytestItemOps.CountItemClientSide(ingot), 10f, "Shiftで青銅インゴットを取り出す");
    await p.ClickUi("block-inventory-close");
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
    await p.ClickUi("inventory-sort");
    await p.Until(() => Local()[0].Id != ItemMaster.EmptyItemId, 10f, "整理で先頭の枠が埋まる");
    await p.Screenshot("06-sorted");

    #region Internal

    ILocalPlayerInventory Local() => ClientDIContext.DIContainer.DIContainerResolver.Resolve<ILocalPlayerInventory>();
    string MainSlotOf(ItemId id) => $"main-slot-{Enumerable.Range(0, Local().MainSlotCount).First(i => Local()[i].Id == id)}";
    int[] EmptyMainSlots(int count) => Enumerable.Range(0, Local().MainSlotCount).Where(i => Local()[i].Id == ItemMaster.EmptyItemId).Take(count).ToArray();

    async UniTask HoldOn(string testid, float seconds)
    {
        var center = (await PlaytestUiElementOps.FindUi(testid)).ScreenRect.center;
        SemanticInput.MouseMoveTo(center);
        SemanticInput.MouseButtonDown(0);
        await p.WaitSeconds(seconds);
        SemanticInput.MouseButtonUp(0);
    }

    async UniTask RightClickOn(string testid)
    {
        SemanticInput.MouseMoveTo((await PlaytestUiElementOps.FindUi(testid)).ScreenRect.center);
        SemanticInput.MouseButtonDown(1);
        await p.WaitSeconds(0.1f);
        SemanticInput.MouseButtonUp(1);
        await p.WaitSeconds(0.3f);
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
石窯の中身は既存の `PlaytestBlockSnapshots.WaitForItemCount`（サーバーのスレッドで複製して読む）で待つ。`ItemId.AsPrimitive()`（PlaytestItemOps と同じ）・`ItemMaster.EmptyItemId` の綴りは Task 1 Step 2 で `item-list-cell-` の作り方と一緒に確かめる。入力・出力の枠の名前 `machine-input-slot-<i>`・`machine-output-slot-<i>` とレシピ選択の行 `machine-recipe-<guid>` は S4-machine の起草版（R-5・R-14）の名前で、照合表の実物へ読み替える。石窯のレシピ（e0c913033c）は 6 本とも `initialUnlocked: false`、青銅インゴット `af78bd9a` の `inputItems` は [青銅鉱石の粉, 原木] の順。未選択の機械は入力を受けず（`.decisions/2026-08-30-レシピ未選択の機械は入力を一切受け入れない.md`）、入力枠はレシピの素材順に固定される（`.decisions/2026-08-30-機械入力スロットはサーバー側でもレシピ素材順に固定する.md`）ので、解放 → 選択 → 粉を 1 枠目・原木を 2 枠目の順にする（D-21）。左クリックの掴みは全量なので、原木は右クリックの半分掴みで置き、右ドラッグ配りの分を手持ちに残す

- [ ] **Step 2: 系を UITK にして流す**

Run:
```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
uloop execute-dynamic-code --project-path ./moorestech_client --code 'System.Environment.SetEnvironmentVariable("MOORESTECH_PLAYTEST_RECEIVER_BASE","http://127.0.0.1:9"); Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi);'
.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh ./moorestech_client .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage4/uitk-production-loop.cs
```
Expected: 終了コード 0、`run dir` の `result.json` が `Success: true`、`recording.mp4` がある。`wc -l` で 200 行未満

- [ ] **Step 3: 掴みの申し送りを観察する（C4-e D-36・Warning (a)(b)(e)）**

録画の 03 の前後を見て、右ドラッグ配りの後の掴みの数（クライアントの掴みとサーバーの掴みが同じか）を `done.md`「## 回ってきた件」の D-36 の行に書く。続けて同じシナリオをもう 1 回流し、`uloop get-logs --project-path ./moorestech_client --log-type Warning` に `[SplitDrag]`・`stale`・`DiscardStaleSession` の行が出たかを数えて同じ行に書く（再現したら bd を `--parent <bd>` で立て、手順と行を添えて I-fix へ回す。gamma は直さない）

- [ ] **Step 4: 失敗したら持ち主へ回す**

`result.json` の `Assertions` の落ちた行と `uloop get-logs --log-type Error` を読み、(1) 名前の違い → 照合表を直してシナリオを直す、(2) UI の不具合（クリックが届かない・数が合わない・出力が描き直されない）→ 持ち主（I-fix）への bd と依頼文、`done.md`「## 差の表」に記録して待つ。入ったら取り込んで Step 2 を流し直す

- [ ] **Step 5: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage4/uitk-production-loop.cs
git commit -m "test(uitk): ラダー 4 uitk-production-loop を足す（S4-gamma）" -- .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage4/uitk-production-loop.cs
```

---

### Task 6: ラダー 5 `uitk-research-train`

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage4/uitk-research-train.cs`

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
using Client.WebUiHost.Boot;
using Cysharp.Threading.Tasks;
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
    await p.WaitForStationName(line.StationB, "南駅", 10f, "サーバーの駅名が南駅になる");
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
    await p.WaitForTrainState(line.Train, 1, null, null, 10f, "サーバーの時刻表が1駅になる");
    await WaitTimetableIdle();
    await p.ClickUi($"train-timetable-station-{b.x}_{b.y}_{b.z}-add");
    await p.WaitForTrainState(line.Train, 2, null, null, 10f, "サーバーの時刻表が2駅になる");
    await p.UntilUiElement("train-timetable-stop-1", 5f);
    await WaitTimetableIdle();
    await p.ClickUi("train-timetable-auto-run-on");
    await p.WaitForTrainState(line.Train, null, true, null, 10f, "サーバーの自動運転がON");
    await p.Screenshot("03-timetable");
    await p.ClickUi("block-inventory-close");
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.WaitForTrainState(line.Train, null, null, true, 15f, "列車が走り出す");
    await p.WaitSeconds(2f);
    await p.Screenshot("04-running");

    #region Internal

    // 即時送信の保留（--busy）が解けるまで待つ。保留中の追加は捨てられる（S4-train R-9）
    // Wait until the immediate-send hold (--busy) clears; additions during the hold are dropped (S4-train R-9)
    async UniTask WaitTimetableIdle()
    {
        for (var i = 0; i < 50; i++)
        {
            var section = await PlaytestUiElementOps.FindUi("train-timetable-section");
            if (section.IsFound && !section.HasStateFlag("train-timetable-section--busy")) return;
            await p.WaitSeconds(0.2f);
        }
        p.Assert(false, "時刻表の保留が10秒で解けない");
    }

    #endregion
});
```
S4-train の計画（PLAN-W4a の起草版）では、駅名は `GameTextField(train-station-name-input)`＋`PanelActionButton(train-station-name-apply)` で送り（R-12）、時刻表は適用ボタンを消して追加ごとに即送信し、保留中は `train-timetable-section--busy` で次の追加を捨てる（R-9）。上のコードはその形。`TrainStationComponent.StationName`（起草時の実物）、研究ツリーを開くキー（`R`）、`HasStateFlag` がクラスの印を状態として返すかは Task 1 Step 2・3 の照合表で確かめる。サーバーの駅名は `PlaytestTrainStationSnapshots.WaitForStationName`（サーバーの tick で複製して読む）で待つ

- [ ] **Step 2: 系を UITK にして流す**

Run:
```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
uloop execute-dynamic-code --project-path ./moorestech_client --code 'System.Environment.SetEnvironmentVariable("MOORESTECH_PLAYTEST_RECEIVER_BASE","http://127.0.0.1:9"); Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi);'
.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh ./moorestech_client .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage4/uitk-research-train.cs
```
Expected: 終了コード 0、`Success: true`、`recording.mp4` がある。落ちたら Task 5 Step 4 と同じ手順で分けて回す

- [ ] **Step 3: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage4/uitk-research-train.cs
git commit -m "test(uitk): ラダー 5 uitk-research-train を足す（S4-gamma）" -- .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage4/uitk-research-train.cs
```

---

### Task 7: マスタのアンカー 9 件 `uitk-master-anchors`

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage4/uitk-master-anchors.cs`

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
    var plank = PlaytestItemOps.ResolveItemId("木の板");
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
Expected: `Success: true`。Note に 9 行（上の「マスタのアンカー 9 件」の表と同じ）。落ちたら先に gamma の照合の誤りでないかを見る（自分の枠を取り違えていないか〈増えた 1 件の特定〉・照合の最中に本番のチャレンジが進んで枠の session が入れ替わっていないか〈`AddOutlineHighlight` は直近の `BeginSession` の session に入る。Note のチャレンジの進行と照らす〉・アンカーの画面の開き方・名前の照合）。gamma の誤りなら gamma のファイルを直して流し直す。アンカーが `Missing`・`Duplicate` なら持ち主（`TutorialAnchorCoveragePending` の旧表の持ち主。取り込み済みなら I-fix）へ bd と依頼文で回して待つ。枠の中心が 5px を超えてずれたら S2-tutorial の枠の位置の不具合として同じく回す

- [ ] **Step 3: 表を done.md に写してコミットする**

Note の 9 行を `done.md`「## 証拠」に「# / チャレンジ / anchorId / 解決 / 枠の中心のずれ px / スクショ」の表で写す。続けて:
```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage4/uitk-master-anchors.cs
git commit -m "test(uitk): マスタのアンカー 9 件を実ゲームで解決するシナリオを足す（S4-gamma）" -- .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage4/uitk-master-anchors.cs
```

---

### Task 8: CPU 時間 `uitk-stage4-panel-cost` と台帳の 1 行

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage4/uitk-stage4-panel-cost.cs`
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
using Client.Playtest.Operations;
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
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1920, 1080);

    // マーカーが見つからなければ名前の一覧を残して止める
    // Leave the marker name list and stop when no marker is found
    var markers = PlaytestPanelCostRecorder.FindPanelMarkers();
    if (markers.Count == 0) p.Note($"UIElements のマーカー一覧: {string.Join(", ", PlaytestPanelCostRecorder.ListUiElementsMarkerNames())}");
    p.Assert(0 < markers.Count, $"パネル更新と描画のマーカーが見つかる（{markers.Count} 本）");
    if (markers.Count == 0)
    {
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);
        return;
    }

    // 3 画面で測り、Note に 1 行ずつ出す
    // Measure on three screens and note one line each
    await MeasureCurrent(UIStateEnum.GameScreen);
    await MeasureOpened(UIStateEnum.PlayerInventory, Key.Tab);
    await MeasureOpened(UIStateEnum.ResearchTree, Key.R);

    // Game View の解像度は PlayMode の後も残るので 1280x720 へ戻す
    // The Game View resolution outlives PlayMode, so restore 1280x720
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);

    #region Internal

    async UniTask MeasureOpened(UIStateEnum state, Key openKey)
    {
        // キーで開いて測り、Esc でゲーム画面へ戻る
        // Open with the key, measure, then return to the game screen with Esc
        await p.PressKey(openKey);
        await p.WaitUiState(state, 10f);
        await MeasureCurrent(state);
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }

    async UniTask MeasureCurrent(UIStateEnum state)
    {
        await p.WaitSeconds(1f);
        var cost = await PlaytestPanelCostRecorder.Measure(markers, 240);
        p.Note($"panel-cost {state} {Screen.width}x{Screen.height} frames={cost.Frames} mean={cost.MeanMs:F3}ms p95={cost.P95Ms:F3}ms max={cost.MaxMs:F3}ms markers={cost.Markers}");
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
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage4/uitk-stage4-panel-cost.cs docs/uitk/TODO.md
git commit -m "docs(uitk): 段階 4 の UI の CPU 時間を測って台帳に記録する（S4-gamma）" -- .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage4/uitk-stage4-panel-cost.cs docs/uitk/TODO.md
```

---
### Task 9: 全録画の 2 回目（UITK 側と Web 側）

**Files:**
- Create: `tools/uitk/list-recordings.sh`
- Create: `tools/uitk/run-all-recordings.sh`
- Modify: `docs/uitk/TODO.md`（「## 実測の結論」に 2 行）

**Interfaces:**
- Consumes: `run-scenario.sh`、台帳「## 壊れた録画の割り振り（bd moorestech-wwxy）」の表
- Produces: `tools/uitk/list-recordings.sh <GameUi|Web>`（標準出力に 1 行 1 本のシナリオのパス、標準エラーに外した名前と理由）、`tools/uitk/run-all-recordings.sh <GameUi|Web> <list.txt> <out.tsv> <開始行> <本数>`（TSV に `scenario system exit seconds run_dir` を追記）。結果の TSV は worktree の gitignore 下 `moorestech_client/Temp/uitk-recordings/` に置く

- [ ] **Step 1: 一覧を作るスクリプトを書く**

```bash
#!/usr/bin/env bash
# 系ごとの全録画の一覧を作る。wwxy の表のうち X-prerun が直す行と消えた行だけを外し、Web では UITK 専用（scenarios/uitk/）も外す
# Lists recordings per system; drops only wwxy rows owned by X-prerun (or deleted files), and UITK-only scenarios (scenarios/uitk/) for Web
set -eu
if [ $# -ne 1 ]; then
  echo "usage: $0 <GameUi|Web>" >&2
  exit 2
fi
system=$1
case "$system" in GameUi|Web) ;; *) echo "system は GameUi か Web" >&2; exit 2;; esac
root=.agents/skills/unity-playmode-recorded-playtest/scenarios
ledger=docs/uitk/TODO.md

# wwxy の節の表の行から、直すレーンが X-prerun の .cs だけを抜く（S2/S3-ladder が直した行は流す）
# From the wwxy table rows, take only .cs files whose fixing lane is X-prerun (rows fixed by S2/S3-ladder still run)
section=$(awk '/^## 壊れた録画の割り振り/{f=1;next} /^## /{f=0} f' "$ledger")
if [ -z "$section" ]; then
  echo "台帳に wwxy の節が見つからない（$ledger）" >&2
  exit 2
fi
excluded=$(printf '%s\n' "$section" | grep '^| `' | grep 'X-prerun' | grep -o '`[^`]*\.cs`' | tr -d '`' || true)

find "$root" -name '*.cs' | sort | while read -r path; do
  rel=${path#"$root"/}
  name=$(basename "$rel")
  if [ -n "$excluded" ] && printf '%s\n' "$excluded" | grep -qx -e "$rel" -e "$name"; then
    echo "除外(wwxy・X-prerun): $rel" >&2
    continue
  fi
  case "$rel" in uitk/*)
    if [ "$system" = Web ]; then echo "除外(UITK 専用): $rel" >&2; continue; fi
  esac
  echo "$path"
done
```

- [ ] **Step 2: 順に流すスクリプトを書く**

```bash
#!/usr/bin/env bash
# 一覧のシナリオを開始行から N 本だけ順に流し、1本1行で TSV に追記する（Bash の前景の上限に収めるため小分けにする）
# Runs N listed scenarios from a start line and appends one TSV row each (chunked to fit a foreground shell limit)
set -u
if [ $# -ne 5 ]; then
  echo "usage: $0 <GameUi|Web> <list.txt> <out.tsv> <start-line> <count>" >&2
  exit 2
fi
system=$1; list=$2; out=$3; start=$4; count=$5
case "$system" in GameUi|Web) ;; *) echo "system は GameUi か Web" >&2; exit 2;; esac
project=./moorestech_client
runner=.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh
switch_code="System.Environment.SetEnvironmentVariable(\"MOORESTECH_PLAYTEST_RECEIVER_BASE\",\"http://127.0.0.1:9\"); Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.$system);"

mkdir -p "$(dirname "$out")"
[ -f "$out" ] || printf "scenario\tsystem\texit\tseconds\trun_dir\n" > "$out"

tail -n +"$start" "$list" | head -n "$count" | while IFS= read -r scenario; do
  [ -z "$scenario" ] && continue

  # 前の失敗が残した PlayMode を止め、系と受け口を立て直す
  # Stop any PlayMode a previous failure left and reset the system and receiver
  # silent-ok: 止める PlayMode が無いときの失敗は正常 / 系の切替の失敗は下の switch-failed 行で TSV に残る
  uloop control-play-mode --project-path "$project" --action Stop >/dev/null 2>&1 || true
  if ! uloop execute-dynamic-code --project-path "$project" --code "$switch_code" >/dev/null; then
    printf "%s\t%s\t%s\t%s\t%s\n" "$scenario" "$system" "switch-failed" 0 "-" >> "$out"
    continue
  fi

  # 1 本流して終了コード・秒・run dir を残す
  # Run one scenario and record its exit code, seconds and run dir
  began=$(date +%s)
  log=$(mktemp)
  "$runner" "$project" "$scenario" > "$log" 2>&1
  code=$?
  dir=$(grep -o 'run dir: .*' "$log" | tail -1 | sed 's/^run dir: //')
  printf "%s\t%s\t%s\t%s\t%s\n" "$scenario" "$system" "$code" "$(( $(date +%s) - began ))" "${dir:--}" >> "$out"
  rm -f "$log"
done
```
`chmod +x tools/uitk/list-recordings.sh tools/uitk/run-all-recordings.sh`。Task 1 Step 4 で `tools/uitk/` が 9 以上だったら `tools/uitk/recordings/` に置き、以後のパスを読み替える

- [ ] **Step 3: UITK 側の全本を流す**

Run（前景で。1 回の呼び出しが 600 秒に収まるよう 3 本ずつ。1 本で 600 秒を超えたシナリオは次から 1 本ずつ）:
```bash
mkdir -p moorestech_client/Temp/uitk-recordings
tools/uitk/list-recordings.sh GameUi > moorestech_client/Temp/uitk-recordings/list.txt 2> moorestech_client/Temp/uitk-recordings/list-excluded.txt
wc -l moorestech_client/Temp/uitk-recordings/list.txt moorestech_client/Temp/uitk-recordings/list-excluded.txt
tools/uitk/run-all-recordings.sh GameUi moorestech_client/Temp/uitk-recordings/list.txt moorestech_client/Temp/uitk-recordings/gameui.tsv 1 3
tools/uitk/run-all-recordings.sh GameUi moorestech_client/Temp/uitk-recordings/list.txt moorestech_client/Temp/uitk-recordings/gameui.tsv 4 3
```
（開始行を 3 ずつ進めて一覧の最後まで。`Temp/` は Unity の gitignore 下）
Expected: TSV の行数が一覧の本数と同じ。終了コード 0 以外の行ごとに `result.json` と `uloop get-logs --log-type Error` の該当部分を読み、次の 6 つに分ける:
- `PASS`（0）
- `束 5 未移植`（チャレンジ一覧・言語ゲート・Web の横断層に触れて止まる。plan §2.0 の段階 4 の終わりでは対象外の束）
- `wwxy`（X-prerun が直す行は一覧から外れ `list-excluded.txt` に名前が残る。S2/S3-ladder が直した行は流れるので、落ちたら `不具合` として扱う）
- `前提つき`（`playtest-receiver`・IME の `.sh` など、run-scenario.sh 単体では前提が立たないもの。そのシナリオのヘッダの手順で 1 回流し直して PASS／不具合に振り直す）
- `Web 専用`（CEF・Web の DOM そのものを見るもの。例 `cef-webui-showcase`）
- `不具合`（UITK 側の画面・操作の誤り）

`不具合` は 1 本ずつ bd を `--parent <bd>` で立て、持ち主（その画面のレーン。取り込み済みなら I-fix）への依頼文（シナリオのパス・落ちた assert・Error の行・run dir の録画）を指揮へ返し、`done.md` の 1 行目を「状態: 停止（全録画の不具合 n 件の修正待ち）」にして待つ。修正が入ったら `git merge --no-ff origin/feature/uitk` で取り込み、`不具合` の行だけを同じスクリプトで流し直す（一覧を `grep` で作り直す）。不具合が 0 になったら次へ

- [ ] **Step 4: Web 側の全本を流し、段階の前と比べる**

Run:
```bash
tools/uitk/list-recordings.sh Web > moorestech_client/Temp/uitk-recordings/list-web.txt 2> moorestech_client/Temp/uitk-recordings/list-web-excluded.txt
```
のあと、Step 3 と同じ小分けで `Web` を `moorestech_client/Temp/uitk-recordings/web.tsv` へ流す（`scenarios/uitk/` は UITK 専用で外れる。D-23。Windows で流すなら `~/bin/winunity` の play で同じ一覧を回し、TSV を同じ形で作る）
Expected: 終了コード 0 以外の行を、S4 前の feature/uitk（最初に入った S4 のマージの 1 番目の親: `git rev-parse "$(git log --first-parent --merges --reverse --format=%H origin/feature/uitk --grep 'from moorestech/uitk-lane/s4-' | head -1)^1"`。レーン側の取り込みのマージに当たらないよう `--first-parent` と PR のマージの題で絞る）の使い捨て worktree（`moores-wt new uitk-tmp/s4-gamma-webbase --from <その SHA>`。Editor が 2 本を超えるなら winunity）で同じシナリオだけ Web で流し直し、`既存`（S4 前も落ちる）か `退行`（S4 前は通る）に分ける。S4 前のコミットに無いシナリオ（S4 のレーンが足したもの）は `比較不能（新設）`、あるが中身が変わったもの（`git diff --quiet <比較元> HEAD -- <シナリオ>` が差ありを返す。S4 のレーンがロケータ等を書き換えた）は `比較不能（変更）` とし、その画面の持ち主の録画（PR の報告ページ）で見たことを書く。`退行` は Step 3 の `不具合` と同じ手順で回して待つ。使い捨て worktree は流し終えたら `moores-wt rm uitk-tmp/s4-gamma-webbase --prune-branch` で畳む

- [ ] **Step 5: 台帳と done.md に書く**

`done.md`「## 全録画の結果」に 2 つの TSV を「シナリオ / UITK の分類 / Web の分類 / 秒」の表で写す（run dir は書かない）。台帳「## 実測の結論」の末尾に 2 行:
```markdown
- 全録画の 2 回目・UITK 側（S4-gamma、<HEAD 10 桁>、全体スイッチ GameUi）: <n> 本中 PASS <a>・束 5 未移植 <b>・前提つき（流し直して PASS）<c>・Web 専用 <d>・不具合 0（途中で見つけた <k> 件は bd <ids> で直して再走 PASS）。X-prerun が直す wwxy の <w> 本は対象外（名前は list-excluded.txt）
- 全録画の 2 回目・Web 側（S4-gamma、<HEAD 10 桁>、`scenarios/uitk/` の UITK 専用は除く）: <n> 本中 PASS <a>・既存の失敗 <e>（S4 前 <SHA 10 桁> でも落ちる: <シナリオ名>）・比較不能（新設）<f>・比較不能（変更）<g>・退行 0
```
`Saves/` に作ったワールドを `list-work-saves.sh` で探して消す

- [ ] **Step 6: コミットする**

```bash
git add tools/uitk/list-recordings.sh tools/uitk/run-all-recordings.sh docs/uitk/TODO.md
git commit -m "test(uitk): 全録画の 2 回目を UITK と Web で流し台帳に記録する（S4-gamma）" -- tools/uitk/list-recordings.sh tools/uitk/run-all-recordings.sh docs/uitk/TODO.md
```

---

### Task 10: 束 4 の突き合わせ差分 0

**Files:**
- Modify: `docs/uitk/disposition.md`（処遇の表に 3 行・「## 突き合わせの記録」に 1 行）

**Interfaces:**
- Consumes: `node tools/uitk/test-name-parity.mjs --bundle 4 [--emit-missing]`（終了コード 0 差分 0／1 差分あり／2 書式の誤り）
- Produces: 束 4 の差分 0

- [ ] **Step 1: 今の差分を出す**

Run: `node tools/uitk/test-name-parity.mjs --bundle 4 --emit-missing`
Expected: 未対応は `e2e/mock-host/detailActions.test.ts` の 3 件だけ。ほかのファイルが残っていたら、残ったテスト名ごとに持ち主を決める（`grep -rn "<テスト名>" docs/superpowers/plans`（S4 の 7 計画はマージ済みでこの repo にある） で各レーンの計画の写す表・`<web-test>` を引く。ファイル単位の表は分け合うファイルがあるので使わない）。持ち主のレーンの写し漏れなので、bd を `--parent <bd>` で立てて I-fix へ回し（gamma は他レーンのテストを書かない）、修正の取り込み後にもう一度流す

- [ ] **Step 2: 持ち主の無い 3 件に処遇を書く**

`docs/uitk/disposition.md`「## 処遇」の表の束 4 の行の並びに 3 行足す（PR 列は Task 15 Step 1 で PR 番号に書き換える）:
```markdown
| 4 | `e2e/mock-host/detailActions.test.ts` | setはrecipeGuidを選択状態へ反映する | 捨てる | mock-host（Web の検証基盤の偽ホスト）が機械のレシピ選択の detail action を受けて自分の状態を書き換える契約。UITK は mock-host を持たず、レシピ選択は S4-machine の State がサーバーへ型付きのコマンドで送る（偽ホストの写し先が無い） | #<PR番号> |
| 4 | `e2e/mock-host/detailActions.test.ts` | recipeGuidなしsetとclearは空GUIDへ戻す | 捨てる | 同上（偽ホストの状態の契約） | #<PR番号> |
| 4 | `e2e/mock-host/detailActions.test.ts` | 対象machineがないblockと未知operationを拒否する | 捨てる | 同上。UITK の操作は型で操作の種類を閉じ、未知の operation の文字列が届く経路が無い | #<PR番号> |
```
（テスト名はファイルの `it("…")` の文字列をそのまま。`--emit-missing` の出力と 1 字ずつ照らす）

- [ ] **Step 3: 差分 0 を見て記録する**

Run: `node tools/uitk/test-name-parity.mjs --bundle 4; echo "exit=$?"`
Expected: `exit=0`。出力の件数を「## 突き合わせの記録」に `| <今日> | 4 | <HEAD 10 桁> | <Web> | <写した> | <処遇> | <写す予定> | 0 |` で足す。写す予定（albof.29）が残っていても未対応 0 なら差分 0 の扱い（写す予定は差分として数える書式なので、1 以上なら終了コード 1 になる。そのときは albof.29 の状態を見て、S4-inventory が閉じていなければ I-fix へ回して待つ）

- [ ] **Step 4: コミットする**

```bash
git add docs/uitk/disposition.md
git commit -m "docs(uitk): 束 4 の突き合わせを差分 0 にする（S4-gamma）" -- docs/uitk/disposition.md
```

---

### Task 11: 回ってきた件の集約と参照の記載

**Files:**
- Modify: `docs/uitk/TODO.md`（「## 参照キャプチャ」に自撮りの置き場の追記）
- Modify（repo 外）: `done.md`

**Interfaces:**
- Consumes: Task 1 Step 3 で集めた件、各レーンの `done.md`
- Produces: `done.md`「## 回ってきた件」の全行に処理が付く

- [ ] **Step 1: 依頼の状態を確かめる**

Run:
```bash
git grep -n "Unmark" -- moorestech_client/Assets/Scripts/Client.Ui/Foundation/Anchor | head -3
git grep -n "PickAt" -- moorestech_client/Assets/Scripts/Client.Ui/Foundation | head -3
git grep -n "MarkClipContainer" -- moorestech_client/Assets/Scripts/Client.Ui | head -3
git grep -n "panel-action-button-width" -- "moorestech_client/Assets/UI Toolkit" moorestech_client/Assets/Scripts/Client.Ui/Components | head -3
ls moorestech_client/Assets/Scripts/Client.Starter/Registration/GameUi/*.cs | wc -l
ls moorestech_client/Assets/Scripts/Client.Starter/Registration/GameUi/Screens/*.cs | wc -l
grep -n "A-142\|SlotCommands" docs/uitk-migration/execution/component-architecture.md | head -3
```
Expected: 各件が入っているかが分かる。「束ねる完了条件と回ってきた件」の表の行ごとに、`done.md`「## 回ってきた件」へ「済み（ファイル:行）」か「未処理 → bd <id>（I-fix へ。帰結: …）」を書く。未処理のものは段階 4 の完了条件に入らない（ラダー・L3・録画が緑なら完了）ので、gamma は止まらず bd を立てるだけにする（D-6）。ただし `MarkClipContainer` は Task 1 Step 4 で見た義務で、ここでは入っていることを確かめ直すだけ（D-19）

- [ ] **Step 2: 自撮りの参照の置き場を台帳に記す**

S4-machine（15 枚）・S4-recipe・S4-research（詳細ペイン）の `done.md` の自撮りの置き場（報告ページの実体 `~/hermes-agent/data/services/pr-review/reports/pr-<N>/` か `moorestech_logs` のパス）を読み、台帳「## 参照キャプチャ」の末尾に 1 行:
```markdown
- 段階 4 の自撮りの参照（E-22。1280×720 の参照に無い状態を各レーンが PlayMode で撮った）: S4-machine <置き場>（`4-block-machineShortInput`・`4-block-machineFilledOutput`・清浄室 13）、S4-recipe <置き場>、S4-research <置き場>。撮影表 `referenceShots.ts` には足していない（Web の撮影の基盤は束 5 の持ち物）
```

- [ ] **Step 3: C4-e の残りを再判定する**

Task 5 Step 3 の観察を元に、C4-e の D-36・D-37・Warning (a)(b)(e)・`DiscardStaleSession` の行を「再現した（bd）／しなかった（観察の条件）」で埋める。D-5 は「据え置き（帰結: 撤去 5c まで Web ホストと State が持ち物の規則を 2 か所で持つ。X レーンで Web 側ごと消える）」と書く。albof.40・掴み絵の撮影 id は `bd show` の状態を写す

- [ ] **Step 4: コミットする**

```bash
git add docs/uitk/TODO.md
git commit -m "docs(uitk): 段階 4 の自撮りの参照の置き場を台帳に記す（S4-gamma）" -- docs/uitk/TODO.md
```

---

### Task 12: 最終の HEAD でラダー 1〜5 と L3 を通す

**Files:**
- なし（結果は `done.md`）

**Interfaces:**
- Consumes: Task 3・5・6 の成果、`uitk-lifeline`・`uitk-opening-hud`・`uitk-build-bp`
- Produces: 同じ HEAD でラダー 5 本が緑の証拠

- [ ] **Step 1: 取り込んでコンパイルする**

Run:
```bash
git fetch origin
git merge --no-ff --no-edit origin/feature/uitk
uloop compile --project-path ./moorestech_client
```
Expected: `ErrorCount: 0`

- [ ] **Step 2: ラダー 5 本を順に流す**

Run:
```bash
printf '%s\n' .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-lifeline.cs .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-opening-hud.cs .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-build-bp.cs .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage4/uitk-production-loop.cs .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage4/uitk-research-train.cs > moorestech_client/Temp/uitk-recordings/ladders.txt
tools/uitk/run-all-recordings.sh GameUi moorestech_client/Temp/uitk-recordings/ladders.txt moorestech_client/Temp/uitk-recordings/ladders.tsv 1 2
tools/uitk/run-all-recordings.sh GameUi moorestech_client/Temp/uitk-recordings/ladders.txt moorestech_client/Temp/uitk-recordings/ladders.tsv 3 2
tools/uitk/run-all-recordings.sh GameUi moorestech_client/Temp/uitk-recordings/ladders.txt moorestech_client/Temp/uitk-recordings/ladders.tsv 5 1
```
Expected: 5 行とも exit 0。`uitk-lifeline` はセーブして終了で PlayMode が止まるので、そのヘッダの「呼び出し側のシェルで save.json の更新を確かめる」を同じ手順で見る。落ちたら Task 5 Step 4 の手順

- [ ] **Step 3: L3 を回す**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.EditModeInPlayingTest\.Ui\.(Stage4\.|GameUiSkeletonTest)" --timeout-seconds 1200`
Expected: PASS。`done.md`「## 証拠」に HEAD・5 本の run dir の名前・L3 の件数を書く

- [ ] **Step 4: コミットは無し**（取り込みのマージコミットは Step 1 で積まれる）

---

### Task 13: 波の並べ比べと codex-audit

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage4/uitk-stage4-wave-shots.cs`

**Interfaces:**
- Consumes: `PlaytestGameViewOps.SetResolution(CancellationToken, int, int)`、`p.Screenshot(string)`、照合表の名前
- Produces: 同時に出る 3 画面 × 2 解像度の Web／UITK の組、codex-audit の結果（`done.md`「## 差の表」）

- [ ] **Step 1: 撮影のシナリオを書く**（系に依らない testid だけを使い、Web と UITK で同じ流れを撮る）

```csharp
// 段階4の波の撮影: 持ち物・機械・研究の同時に出る組を 1280x720 と 2560x1080 で撮る（録画しない。系は呼び出し側が選ぶ）
// Stage-4 wave shots: capture the co-shown inventory, machine and research sets at 1280x720 and 2560x1080 (no recording; caller picks the system)
using System.Threading;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = false };
return PlaytestRunner.Run("uitk-stage4-wave-shots", options, async p =>
{
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);

    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();

    // 同じ持ち物と石窯を用意する
    // Prepare the same inventory and stone kiln
    await p.GiveItem("原木", 12);
    await p.GiveItem("青銅鉱石の粉", 4);
    await p.SetupFlatGround();
    var kilnPos = new Vector3Int(10, 32, 10);
    await p.PlaceBlockDirect("石窯", kilnPos, BlockDirection.North);
    var kilnObject = await p.WaitBlockGameObject(kilnPos);
    var kilnBounds = kilnObject.GetComponentsInChildren<Collider>(true)[0].bounds;

    // 2 つの解像度で 3 画面を撮る
    // Capture three screens at two resolutions
    await ShootAll(1280, 720);
    await ShootAll(2560, 1080);

    // Game View の解像度は PlayMode の後も残るので、後に流すシナリオのために 1280x720 へ戻す（前例 uitk-gameview-resolution-switch）
    // The Game View resolution outlives PlayMode, so restore 1280x720 for later scenarios (as uitk-gameview-resolution-switch does)
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);

    #region Internal

    async UniTask ShootAll(int width, int height)
    {
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, width, height);
        await p.WaitSeconds(1f);
        var suffix = $"{width}x{height}";

        await p.PressKey(Key.Tab);
        await p.WaitUiState(UIStateEnum.PlayerInventory, 10f);
        await p.WaitSeconds(1f);
        await p.Screenshot($"4-wave-playerInventory-{suffix}");
        await p.PressKey(Key.Tab);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);

        p.WarpPlayer(new Vector3(kilnBounds.center.x, 33.5f, kilnBounds.min.z - 1.5f));
        await p.WaitSeconds(1f);
        await p.AimAt(kilnBounds.center);
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        await p.WaitSeconds(1f);
        await p.Screenshot($"4-wave-subInventory-kiln-{suffix}");
        await p.ClickUi("block-inventory-close");
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);

        await p.PressKey(Key.R);
        await p.WaitUiState(UIStateEnum.ResearchTree, 10f);
        await p.WaitSeconds(1f);
        await p.Screenshot($"4-wave-researchTree-{suffix}");
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }

    #endregion
});
```

- [ ] **Step 2: 両方の系で撮る**

Run: `printf '%s\n' .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage4/uitk-stage4-wave-shots.cs > moorestech_client/Temp/uitk-recordings/wave.txt` のあと、`tools/uitk/run-all-recordings.sh GameUi … wave.tsv 1 1` と `tools/uitk/run-all-recordings.sh Web … wave.tsv 1 1`
Expected: 2 行とも exit 0。各 run dir に 6 枚。`moorestech_client/Temp/uitk-recordings/compare/{web,uitk}/` へ名前を揃えて写す

- [ ] **Step 2b: 正本の参照キャプチャを並べる**

Web の見た目の正本は参照キャプチャ（設計書 §3.3）で、段の頭に integrator が撮り直す（実行計画「各段の頭」）。使う参照は次の順で決める:
1. `ls ../moorestech_logs/uitk/reference-captures/` と台帳「## 参照キャプチャ」から、段 4 の頭に撮り直した `web-<sha>/`（720p）と `web-<sha>-2560x1080/` を引く（無ければ最新）
2. `git merge-base --is-ancestor <Task 2 の束 4 の起点> <参照の sha>` が両方の解像度で真であることを見る（偽なら、その参照は束 4 の Web を全部は含まない）
3. 偽なら、gamma（integrator）が起点以後の `origin/master` の使い捨て worktree（`moores-wt new uitk-tmp/s4-gamma-ref --from origin/master --no-editor`）で撮り直す。新しい worktree には node_modules が無いので、先に `cd moorestech_web/webui && pnpm install && pnpm build` を流す。720p は `UITK_REFERENCE_OUT=<空のディレクトリ> UITK_REFERENCE_PORT=<空きポート> node --import tsx e2e/uitk-reference/capture.ts`（ref-uitk-design `compare-procedure.md` の流し方）で撮る。`capture.ts` の画面の大きさは `shotRunner.ts:11` の `referenceViewport`（1280×720）に固定で外から変える口が無いので、2560×1080 はこの使い捨て worktree の中でだけ `referenceViewport` を `{ width: 2560, height: 1080 }` に書き換えて別の空のディレクトリへ撮り、コミットしない（口を足すのは束 5 の Web の撮影基盤の持ち物）。撮れた `manifest.json` の `viewport`（`captureIndex.ts` が書く。`index.md` には無い）が 1280x720・2560x1080 になっていることを見てから `moorestech_logs/uitk/reference-captures/web-<sha>/`・`web-<sha>-2560x1080/` に置き、台帳「## 参照キャプチャ」の撮影元を書き換える（台帳の運用）。撮り方（書き換えた行を含む）を `done.md` に書く。撮り終えたら worktree を `moores-wt rm uitk-tmp/s4-gamma-ref --force --prune-branch` で畳む（書き換えは捨てる）

撮影 id `4-player-inventory`・`4-research`・`4-block-machine-recipe-selection` を取り、`compare/` に「参照／Web 実ゲーム（CEF）／UITK 実ゲーム」の 3 列で並べる。使った参照の sha を `done.md` に書く（D-25）。

参照は mock-host の高密度の fixture（例 `4-block-machine-recipe-selection` は `referenceShots.ts` の `machineRecipesOverflow` で大量のレシピを入れ `machine-selected-recipe` を押した状態）で、実ゲームの撮影（石窯を開くだけ）とデータの状態が違う。組ごとに状態の差（行数・スクロールの有無・選択の有無・中身）を `compare/states.md` に先に書き出し、参照との比較は状態に依らない配置・色の系統・装飾・文字の大きさ・余白だけにする。CEF と UITK の実ゲームの組は同じ状態なので、そこでは状態に依るものも含めて全部比べる

- [ ] **Step 3: 各レーンの組を集める**

各 S4 レーンの PR の報告ページの実体 `~/hermes-agent/data/services/pr-review/reports/pr-<N>/`（PR 番号は各 `done.md`「## 証拠」）の manifest から `group` ごとの Web／UITK の組のパスを集め、`compare/lanes.md` に「group / Web / UITK / 解像度」で並べる（コピーしない。パスだけ）

- [ ] **Step 4: codex-audit にかける**

codex-audit スキル（既定 gpt-6-astra）に、Step 2b の 3 列の組と Step 3 の組の画像を渡し、次を頼む:「段階 4（持ち物・掴み・レシピ・ブロック UI 全族・研究）の参照キャプチャ（mock-host で撮った意図した見た目の正本）・Web の実ゲーム（CEF）・UITK の実ゲームを並べた。(1) 参照と UITK の差は、添付の状態の差（compare/states.md）に依らない配置・色の系統・装飾・文字の大きさ・余白だけを挙げて。(2) CEF と UITK の差（同じ実ゲームの状態）を全部挙げて。(3) 分け方: CEF と UITK が揃って参照と違うものは『参照の版の差か状態の差』、CEF だけが参照とも UITK とも違うものは『CEF 由来の候補』（同じ状態の CEF と UITK の組で裏取りできたものだけを CEF 由来とする）、UITK だけが違うものは『UITK の差』。配置・色の系統・装飾の有無・文字の大きさ・欠けている要素・重なり・はみ出しの差を、組ごとに『差／重さ（一目で分かるか）／どちらが正しいか』で挙げて。台帳の見た目の例外 LX-106〜LX-140 の文面（添付）に当たる差は『記帳済み LX-n』と書いて除いて。合否の目安は 720p で 2px」。台帳「## 見た目の例外」の LX-106〜140 の行を本文に添える

- [ ] **Step 5: 指摘を再現してから回す**

指摘ごとに、別の subagent（fresh-context）に該当の組の画像と指摘文だけを渡し「この差は画像で確かめられるか。px で測って」と再現させ、再現できたものだけを `done.md`「## 差の表」に「画面 / 差 / 測った値 / CEF 由来か / 持ち主 / 処理の案」で残し、指揮へ判定を求める（設計書 §3.3: 並べ比べの判定は指揮。gamma は codex-audit を回して再現と案までを持つ。D-18）。指揮の判定ごとに、(1) 直す → 持ち主（I-fix）への bd と依頼文、(2) 例外にする（「同じにするための対応が大きいか、ブラウザの癖の再現が要り、かつ差が一目で分からない」ものだけ。台帳の運用）→ gamma の範囲の LX-141〜145 から番号を取り、文面（ID・画面・差・同じにしない理由・推奨案・撮影・出所）を `done.md` に書いて I-fix へ渡す。台帳の行とコード側の印 `/* look-exception: LX-n */` は、`LookExceptionLedgerTest` が両方の一致を同じ HEAD で見るので、差のあるファイルを直す I-fix の PR で一緒に入れる（D-17）。再現しなかったものは「再現せず」と 1 行で残す。直すものが残る間は「状態: 停止」で待ち、入ったら Step 2 を流し直して該当の組だけ再判定する

- [ ] **Step 6: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage4/uitk-stage4-wave-shots.cs
git commit -m "test(uitk): 段階 4 の波の撮影シナリオを足す（S4-gamma）" -- .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage4/uitk-stage4-wave-shots.cs
```

---

### Task 14: moores-code-review で全ブランチレビュー（必須）

**Files:**
- Modify: レビューの自動適用が触ったファイルだけ

**Interfaces:**
- Consumes: Task 1〜13 のコミット一式（`origin/feature/uitk...HEAD`）
- Produces: レビュー記録 `../moorestech_logs/harness/` 配下（`$RUNDIR`）

- [ ] **Step 1: 必ず moores-code-review スキルで全ブランチレビューを実行する（自動実行・ゴール文言による省略不可）**

対象は `origin/feature/uitk...HEAD`。レビューが gamma の範囲の外の直し（画面・部品・Foundation・State・登録ファイルを書き換える・他レーンのテストを足す・L3 やシナリオの照合を緩める・束 4 を切替済みにする）を提案したら採らず、`done.md`「## 要裁定」か「integrator への依頼」に書く

- [ ] **Step 2: 自動適用があれば検査と録画を回し直してコミットする**

自動適用が L3・`Operations/Ladder/`・`PlaytestAnchorChecks`・シナリオ・スクリプトに触れたら、反映後のバイナリで Task 12 Step 2〜3 を再走する（ユニットテストの通過は代替にならない）。続けて:
```bash
git add moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/Stage4 moorestech_client/Assets/Scripts/Client.Playtest/Operations/Ladder moorestech_client/Assets/Scripts/Client.Playtest/Operations/ScenarioChecks .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk tools/uitk docs/uitk
git commit -m "fix(uitk): S4-gamma の moores-code-review 反映

<実行ホストの指示する帰属行>" -- moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/Stage4 moorestech_client/Assets/Scripts/Client.Playtest/Operations/Ladder moorestech_client/Assets/Scripts/Client.Playtest/Operations/ScenarioChecks .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk tools/uitk docs/uitk
```
（自動適用が無ければコミットしない）

---

### Task 15: feature/uitk への PR・取り込み・マージ・片付け（必須）

**Files:**
- Modify: `docs/uitk/disposition.md`（Task 10 の 3 行の `#<PR番号>`）

**Interfaces:**
- Consumes: Task 14 後の HEAD
- Produces: `origin/feature/uitk` のマージコミット。以後 S5-crosscut・S5 の画面が段階 4 の上で始められる

- [ ] **Step 1: pr-create スキルで `base=feature/uitk` の PR を作る**

push して PR を作る（`gh pr create --base feature/uitk`）。作ったら Task 10 の 3 行の `#<PR番号>` を実際の番号に書き換えてコミット・push する。PR 本文の先頭行は報告ページ `# https://review.moores.tech/pr/<番号>/report/` だけ。報告ページは `~/hermes-agent/data/services/pr-review/publish/README.md` の手順で、manifest は次の形（`file` は絶対パス。並べ比べは Web を先に同じ `group` へ置く）:
```json
{
  "head": "<HEAD 10 桁>",
  "summary": [
    "段階 4 の締め: 持ち物・研究・機械の同時に出る組を実ゲームで巡る L3 を足し、各面が自分の列に 1 つだけ見えて覆われないことを固定した",
    "ラダー 4 uitk-production-loop・5 uitk-research-train を作り、ラダー 1〜5 を同じ HEAD の全体スイッチ UITK で緑にした",
    "全録画の 2 回目を UITK と Web で流し（不具合 0・退行 0）、マスタのアンカー 9 件・CPU 時間・束 4 の突き合わせ差分 0・G1 未記帳 0 を台帳に残した"
  ],
  "media": [
    {"file": "<uitk-production-loop の recording.mp4>", "caption": "ラダー 4 uitk-production-loop（UITK）", "group": "ラダー"},
    {"file": "<uitk-research-train の recording.mp4>", "caption": "ラダー 5 uitk-research-train（UITK）", "group": "ラダー"},
    {"file": "<uitk-master-anchors の recording.mp4>", "caption": "マスタのアンカー 9 件（UITK）", "group": "アンカー"},
    {"file": "<compare/web/4-wave-playerInventory-1280x720.png>", "caption": "Web playerInventory 1280×720", "group": "4-wave-playerInventory"},
    {"file": "<compare/uitk/4-wave-playerInventory-1280x720.png>", "caption": "UITK playerInventory 1280×720", "group": "4-wave-playerInventory"},
    {"file": "<compare/web/4-wave-playerInventory-2560x1080.png>", "caption": "Web playerInventory 2560×1080", "group": "4-wave-playerInventory"},
    {"file": "<compare/uitk/4-wave-playerInventory-2560x1080.png>", "caption": "UITK playerInventory 2560×1080", "group": "4-wave-playerInventory"},
    {"file": "<compare/web/4-wave-subInventory-kiln-1280x720.png>", "caption": "Web 石窯 1280×720", "group": "4-wave-subInventory"},
    {"file": "<compare/uitk/4-wave-subInventory-kiln-1280x720.png>", "caption": "UITK 石窯 1280×720", "group": "4-wave-subInventory"},
    {"file": "<compare/web/4-wave-subInventory-kiln-2560x1080.png>", "caption": "Web 石窯 2560×1080", "group": "4-wave-subInventory"},
    {"file": "<compare/uitk/4-wave-subInventory-kiln-2560x1080.png>", "caption": "UITK 石窯 2560×1080", "group": "4-wave-subInventory"},
    {"file": "<compare/web/4-wave-researchTree-1280x720.png>", "caption": "Web 研究 1280×720", "group": "4-wave-researchTree"},
    {"file": "<compare/uitk/4-wave-researchTree-1280x720.png>", "caption": "UITK 研究 1280×720", "group": "4-wave-researchTree"},
    {"file": "<compare/web/4-wave-researchTree-2560x1080.png>", "caption": "Web 研究 2560×1080", "group": "4-wave-researchTree"},
    {"file": "<compare/uitk/4-wave-researchTree-2560x1080.png>", "caption": "UITK 研究 2560×1080", "group": "4-wave-researchTree"}
  ],
  "verification": [
    "Unityコンパイル: エラー0",
    "L3 Stage4ScreenSetGameUiTest: PASS",
    "ラダー 1〜5（lifeline・opening-hud・build-bp・production-loop・research-train）: 同じ HEAD の全体スイッチ UITK で PASS",
    "全録画 2 回目: UITK <n> 本（不具合 0）・Web <n> 本（退行 0）",
    "マスタのアンカー 9 件: 全件 Unique・枠の中心のずれ <最大>px",
    "CPU: UIElementsUpdatePanels＋RepaintPanels 平均 <x>ms（目安 1ms）",
    "test-name-parity 束 4: 差分 0、G1 束 4 未記帳 0、codex-audit: 再現した差 <k> 件（処理は done.md）"
  ],
  "notes": ["分類の表・差の表・回ってきた件は moorestech_logs/harness/uitk/S4-gamma/done.md"]
}
```
`python3 ~/hermes-agent/data/services/pr-review/publish/publish_pr_report.py <PR番号> <manifest.json>` が失敗したら、報告 URL 無しで完了にしない

- [ ] **Step 2: feature/uitk の進みを取り込み、G1 緑を見る**

Run:
```bash
git fetch origin
git merge --no-ff --no-edit origin/feature/uitk
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.EditModeInPlayingTest\.Ui\.Stage4\." --timeout-seconds 900
python3 .github/scripts/check-uitk-web-follow.py --base origin/master --head HEAD
node tools/uitk/test-name-parity.mjs --bundle 4
git push
gh pr checks <PR番号>
```
Expected: `ErrorCount: 0`・PASS、「束 4（暗転・起点 …）: 未記帳 0 件」、parity 終了コード 0。共有区画（`docs/uitk/TODO.md` の節の末尾・`docs/uitk/disposition.md` の表）の衝突は両方の行を採る。取り込みが画面・部品・入力に触れていれば Task 12 Step 2 を再走する。`gh pr checks` で `UITK Web follow gate (G1)` と `UITK Web follow gate self-test` が `pass`（終わっていなければ前景で `gh pr checks <PR番号> --watch`）。`Unity Test - client-remainder` の `UnityCursorTooltipScreenTest` 1 件の失敗は既存

- [ ] **Step 3: マージコミットで取り込む**

Run: `gh pr merge <PR番号> --merge`
Expected: マージされ、`git fetch origin && git log -1 --format='%H %P' origin/feature/uitk` の親が 2 つ（Squash しない）

- [ ] **Step 4: bd を閉じ、報告を締め、worktree を畳む**

Run:
```bash
bd close <bd> --reason "PR #<番号> を feature/uitk へマージ（段階 4 の締め: L3・ラダー 4/5・全録画 2 回目・アンカー 9 件・CPU・束 4 parity 0・G1 0）"
git status --short
git log origin/uitk-lane/s4-gamma..HEAD
```
Expected: closed、`git status --short` と `git log` が空。`done.md` の 1 行目を「状態: 完了」にし、「## 証拠」に PR 番号・マージコミット・G1 のジョブ URL・報告ページの URL を足す。テストで `Saves/` に作ったワールドが残っていないことを確かめてから:
```bash
cd <メインのクローンの絶対パス>
moores-wt rm uitk-lane/s4-gamma --prune-branch
moores-wt status
```
Expected: worktree と Editor が畳まれる（未コミット・未 push で拒否されたら残りを片付けてからもう一度。`--force` は使わない）

---

## Self-Review

1. **Requirements coverage**: R-1 → Task 3（`Stage4ScreenSetGameUiTest`）・Task 12 Step 3、R-2 → Task 5、R-3 → Task 4 Step 1・Task 6、R-4 → Task 12 Step 2、R-5 → Task 9、R-6 → Task 4 Step 3・Task 7、R-7 → Task 4 Step 2・Task 8、R-8 → Task 10、R-9 → Task 2・Task 15 Step 2、R-10 → Task 13、R-11 → Task 1 Step 3・Task 5 Step 3・Task 11、R-12 → 末尾の節、R-13 → Task 14・15。§1.4 の句: 同時に出る組の L3（Task 3）・ラダー 2 本（Task 5・6）・ラダー 1〜5 緑（Task 12）・全録画の 2 回目（Task 9）・アンカー 9 件（Task 7）・CPU の台帳 1 回（Task 8）・束 4 差分 0（Task 10）・G1 0（Task 2・15）・codex-audit（Task 13）。plan §2.5 の「機械 UI を開いたまま中身が更新される録画」は Task 5 の 04、「UITK の掴みのコードが Web に依存しない」は Task 5 が Web ホスト無し（`WebUiHost.Hub == null`）で掴み・Shift・右ドラッグを通すことで見る。§6.2 の必須: 使う部品と API（Global Constraints・各 Task の Interfaces）、つまみ（gamma は部品のつまみを使わない。recipe D-9 の 5 本は末尾の設計書の行）、写すテストと `<web-test>`（gamma が写すテストは無い。持ち主の無い 3 件は処遇「捨てる」。D-7）、720p と 2560×1080（Task 13）、録画（Task 5・6・7・9）、登録の 1 行（gamma は登録しない。D-1）、新しい共有の申告（Global Constraints）、File Structure（他レーンと重ねない）。漏れなし
2. **Placeholder scan**: `<bd>`・`<PR番号>`・`<番号>`・`<HEAD 10 桁>`・`<起点>`・`<SHA 10 桁>`・`<n>`・`<x>`・`<y>`・`<置き場>`・manifest の `<… のパス>` は実行時にしか決まらず、取り方を同じタスクに書いた。他レーンの名前（testid・USS のクラス・スロットの名前）は計画から写した想定で、Task 1 Step 2 の照合表で実物に読み替える手順を書いた。「TBD」「適切に」は無い
3. **Type consistency**: `Stage4Surface.InColumn(string, string)`・`Stage4Surface.OutsideColumns(string)`・`Stage4ExpectedSurfaces.Expected(UIStateEnum)`・`All`・`Inventory`/`RecipeViewer`/`ItemList`/`BlockInventory`/`ResearchTree`/`Equipment`/`Backdrop`、`Stage4ScreenSetProbe.CountByName`・`VisibleByName`・`IsVisible`・`TryGetColumnName`・`IsPickedAtCenter`・`DescribePicked`、`PlaytestTrainLineSetup`（`StationA`・`StationB`・`StationAPosition`・`StationBPosition`・`Train`）・`PlaytestTrainLine.Build`・`AimAtCar`・`AimAtStation`、`PlaytestTrainStationSnapshots.ReadStationName`・`WaitForStationName`、`PlaytestPanelCost`（`Frames`・`MeanMs`・`P95Ms`・`MaxMs`・`Markers`）・`PlaytestPanelCostRecorder.FindPanelMarkers`・`ListUiElementsMarkerNames`・`Measure`、`PlaytestMasterAnchor`（`AnchorId`・`TutorialGuid`・`ChallengeGuid`・`ChallengeTitle`）・`PlaytestAnchorChecks.CollectUiHighlightAnchors`・`CheckOutlineOnAnchor` は定義（Task 3・4）と使用（Task 5〜8・13）で同じ綴り。スクリプトの引数 `run-all-recordings.sh <GameUi|Web> <list> <out> <start> <count>`・`list-recordings.sh <GameUi|Web>` は Task 9・12・13 で同じ。全シナリオの `using` に、拡張メソッド（`PressInteract`・`WaitForItemCount` 等）と口の名前空間 `Client.Playtest.Operations` がある
4. **保留・縮退経路**: (a) 前提のレーンが欠ける → Task 1 Step 1 で「状態: 停止」と指揮への返却（解消の主体は指揮。最小構成: S2-ladder・S3-ladder が DAG に無く着手時に未マージ → 改め案で辺を足す）。(b) 不具合・退行・写し漏れ → bd と依頼文で I-fix へ、「状態: 停止」で待ち、取り込み後に該当分だけ再走（解消の主体は I-fix。gamma は直さないので、待つ間に gamma が自分で解消できる経路は無いことを明記した）。(c) マーカー 0 本 → シナリオが一覧を Note に出して `Success: false`（気づく経路は result.json）。(d) 依頼の未処理（Foundation の口・登録の再編）→ 完了条件に入れず bd だけ（D-6。帰結を `done.md` に書く）。`MarkClipContainer` だけは義務なので止まって待つ（D-19）。(e) 系の切替の失敗 → TSV に `switch-failed` 行（スクリプトの `silent-ok` は止める PlayMode が無いときの `Stop` の失敗だけ）。(f) 1ms 超 → 合否にせず bd と「未確認」（plan §2.5 の目安）
5. **「決定的にする」を根拠にした選択**: 無し（全録画の順はファイル名順だが、各本の前で PlayMode を止めて系を立て直すので順に依らない）
6. **呼び出し側への責務漏出**: アンカーの集め方と照合は `PlaytestAnchorChecks` の 1 か所、列車線の準備は `PlaytestTrainLine` の 1 か所、CPU の測り方は `PlaytestPanelCostRecorder` の 1 か所、面の読み方は `Stage4ScreenSetProbe` の 1 か所。石窯を狙って開く 4 行は Task 5 と Task 13 の 2 か所に現れる（D-9 で据え置きの理由を書いた）

---

## 判断記録（ADR）

従う裁定・設計: 実行計画 `docs/uitk-migration/execution/execution-plan.md`（§1.4 S4-gamma 行・§3 の integrator の持ち物・§4.3・§5・§6.2・E-1・E-22）、統合計画 `docs/uitk-migration/plan.md`（§2.0・§2.5・§4.4・§4.5・§4.7）、設計書 `docs/uitk-migration/execution/component-architecture.md`（§3.3・§3.4、[A-74]・[A-92]・[A-111]）、台帳 `docs/uitk/TODO.md`「## 読み方と運用」、`docs/uitk/disposition.md`「## 書式」。

| ID | 判断 | 根拠 | 却下した案 |
|---|---|---|---|
| D-1 | gamma は本番のコード（画面・部品・Foundation・State・登録）を 1 行も書かず、検査する側のファイルだけを足す。登録の 1 行は無い | 段の締めの役目は「段が揃ったことの証拠」で、直すのは持ち主（取り込み後は I-fix）。gamma が直すと他レーンの所有と重なり、レビューの責任が割れる（実行計画 §8 の所有） | gamma が見つけた不具合をその場で直す: 所有が重なり、持ち主のテストが付かないまま直る |
| D-2 | L3 は `Ui/Stage4/` の新しいサブディレクトリに 3 ファイルで置く | `EditModeInPlayingTest/Ui/` は既に 10 ファイル（`.cs`）。面の読み方（Probe）と期待の表を分けると、Task 13 の撮影や後の段の締めが表を読める | `Ui/` 直下に 1 ファイル: 11 本目になる。1 ファイルに表と読み方を入れる: 200 行を超える見込み |
| D-3 | 「ビューア＋機械 UI＋インベントリ」の L3 は同時表示でなく行き来で見る（PlayerInventory → … → SubInventory〈`釜`〉→ … → PlayerInventory）。特殊族は対象にしない | Web の `App.tsx` でビューアは playerInventory のときだけ出て、機械 UI（SubInventory）と同時には出ない（S4-recipe の重なりの表もそう書く）。壊れ方は「前の画面の面が残る・重なる」なので、行き来の後に残骸が無いことを見る。特殊族は同じ `block-inventory` の器で列の扱いが同じ | 3 つを同時に出す: 本番で起きない状態を作ることになる。特殊族を全部巡る: 族ごとの見た目は各レーンの L1 と録画が見ている |
| D-4 | 登録ディレクトリ `Registration/GameUi/`（直下と、W4 以降の画面を置く `Screens/`。E-1）のどちらかが 10 を超えるか、S4-alpha の `BlockInventoryGameUiRegistration.cs`（E-1 が直下に認めた 1 本）の他に S4 の画面の登録が直下にあっても gamma は動かさず、bd を I-fix へ回す | 中身は各レーンの登録ファイルで、動かすと他レーンのファイルを書く。alpha が依頼済みで、W4 の取り込みの前に integrator が入れる段取り | gamma が再編する: 所有が重なる。完了条件にする: 段階 4 の完了条件（plan §2.5）に無い |
| D-5 | 他レーンの名前が計画と違えば gamma のファイルの側を直し、相手に変更を求めない | 名前は画面の持ち主が Web の `data-testid` に合わせて決めたもので、gamma の検査はそれを読む側 | 計画の名前へ合わせるよう依頼する: マージ済みの画面を名前のためだけに直すことになる |
| D-6 | 回ってきた依頼のうち Foundation の口（`TutorialAnchor.Unmark`・`GameUiPointerProbe.PickAt`・`GamePanel` の題の逃げ）と D-23・D-26 は、未処理でも段階 4 の完了を止めず bd を I-fix へ回すだけにする | 各レーンが「無くても動く形」で書いたと計画に書いている。gamma の完了条件（§1.4）に入っていない。帰結: 画面側の写しが残る（`done.md` に件ごとに書く） | 全部入るまで止める: 段階 5 の着手が理由無く遅れる |
| D-7 | 持ち主の無い `e2e/mock-host/detailActions.test.ts` 3 件は処遇「捨てる」 | mock-host（偽ホスト）が detail action を受けて自分の状態を書き換える契約で、UITK に偽ホストが無い。機械のレシピ選択の振る舞いは S4-machine の State のテストが持つ | 写す予定: 写し先が無いので永遠に閉じない |
| D-8 | （撤回。D-24 へ置き換え）新しいシナリオ 5 本は `scenarios/uitk/` に置く | — | — |
| D-9 | 石窯を置いて狙って開く 4 行（Task 5・13）は共通化しない | シナリオ間の共有は DSL の口になり、2 か所のために `Ladder/` に 4 本目を足すのは YAGNI。違いが出たら（狙いの位置）片方だけ直せる | `PlaytestKilnOps` を足す: 2 か所のための口 |
| D-10 | 全録画は小分けの前景の呼び出し（3 本ずつ）で流し、バックグラウンドで待たない | 依頼の規則（前景で実行）と Bash の前景の上限 600 秒。小分けでも TSV に追記するので途中から再開できる | 1 回で全本: 前景の上限を超える。バックグラウンドで流してポーリング: 規則に反する |
| D-11 | Web 側の失敗は S4 前のコミットで流し直して「既存」と「退行」に分ける | plan §2.0 の「Web 側は段階の前と同じ合否」は差分で見るもので、S4 前から落ちるものを gamma の不具合にしない | Web 側の失敗を全部不具合にする: 段階と無関係の既存の失敗を抱える |
| D-12 | CPU 時間は Editor の PlayMode・1920×1080 で測り、1ms 超は合否にしない | plan §2.5 は「台帳に 1 回記録」「目安 1ms」で、PoC も Editor で ProfilerRecorder を読んだ。プレイヤービルドでの計測は段階 4 の完了条件に無い | プレイヤービルドで測る: ビルドが要り段の締めの範囲を超える。1ms を合否にする: plan の目安を基準に変えることになる |
| D-13 | マスタのアンカーはシナリオがマスタから集め、件数 9 を assert する | ピンが動いたときに表の写し間違いで漏れないように、正本（マスタ）から集めて件数で囲う。件数が変われば落ちて気づく | アンカーの文字列を 9 個書く: ピンの変更で黙って古くなる |
| D-14 | `recipe.item-<itemId>` はマスタ外の 1 件として 9 件と分けて見る | 統合計画 §4.5 の追加の ID で、マスタは今使っていない。`AnchorIds.RecipeItem(int)` の書式が解決することだけを見る | 見ない: 追加の ID の解決が誰にも確かめられない |
| D-15 | ラダーとアンカー・撮影は録画の DSL、同時に出る組は L3 で見る | 同時に出る組は「木の構造（列・数・Pick）」の照合で、L3 が速く CI でも回る。ラダーは実操作の流れで、録画が要る（unityプレイ録画テスト） | 全部を録画で: CI で回らない。全部を L3 で: 掴み・長押し・F の流れの録画が残らない |
| D-16 | 列車線の準備と CPU 時間の計測は `Operations/Ladder/`（新しい役割のサブディレクトリ。名前空間は平らな `Client.Playtest.Operations`）、アンカーの照合は前例の `ScenarioChecks/`、サーバーの値は `ServerSnapshots/` の待ち（既存の `WaitForItemCount`、足す `PlaytestTrainStationSnapshots.WaitForStationName`）で読み、`RunOnServer` は書き込みにだけ使う。品目は `PlaytestItemOps.ResolveItemId`、見えるかは `GameUiElementQuery.IsVisible` を使う。出所: 設計検査（Phase 1〜2.5） | 既存のサブディレクトリは役割で分かれ（`ServerSnapshots`・`ScenarioChecks`・`Ui`）、準備と計測はどれにも当たらない。`Operations/` 直下は既に 10 本。サーバーの値をメインスレッドから読むと tick と競合する（bd moorestech-xyuaf.2.10） | 全部 `Ladder/` に置く: アンカーの照合は `ScenarioChecks` と同じ役割。`Operations/` 直下に置く: 11 本目になる。品目と見え方を自前で書く: 既存の 1 か所と判定が割れる |
| D-17 | LX の番号は gamma の範囲（LX-141〜145）から取るが、台帳の行は gamma でなく差のあるファイルを直す I-fix がコードの印と同じ PR で入れる。出所: 設計検査 Phase 2.6（検査7。設計書 §3.4「台帳の末尾へレーンが追記」との食い違い） | `LookExceptionLedgerTest` は「印の ID が台帳にある」「台帳の ID が印のどこかにある」を同じ HEAD で照合するので、行だけ先に入れると gamma の PR が赤になる。gamma は差のあるファイル（他レーンの画面）を書かない（D-1）。§3.4 の「レーン」を「印を置くレーン」と読む改め案を末尾の設計書の行に足す | gamma が台帳の行だけ入れる: 照合テストが落ちる。gamma が印も置く: 他レーンのファイルを書く |
| D-18 | codex-audit を回し、指摘を再現して処理の案を付けるまでが gamma、直すか例外にするかの判定は指揮。出所: 設計検査 Phase 2.6（検査7。設計書 §3.3「指揮（判定）」と実行計画 §5「段の締めで codex-audit」の食い違い） | 両方を満たす読み方: 回すのは段の締め（§5・§1.4）、判定は指揮（§3.3）。gamma は int のレーンで指揮と同じ場にいるので判定待ちは短い | gamma が判定まで持つ: §3.3 と食い違う |
| D-19 | `MarkClipContainer`（研究の視口に子を切る容器の印）は D-6 から外し、無ければ I-fix へ回して入るまで止まる。出所: 設計検査 Phase 2.6（検査7。E-1・A-65 の義務） | 印が無いと、研究ノードのアンカーが視口で切られて見えなくても枠が出る（チュートリアルの枠の前提を欠く）。R-6 の判定の前提なので完了条件に入る | 未処理でも進める: A-65 の義務を段の締めで見逃す |
| D-20 | 型の弱い指摘（検査5 の弱 3 件）はこのままにする。(a) 面の列を `StageColumn` でなく名前の文字列で持つのは、L3 が木を名前で読む側で、enum から名前への写しは `StageColumnsLayout` の中にしか無いから（写しを作ると 2 か所になる）。空文字は「列の外」の 1 値だけで、`Stage4ExpectedSurfaces` の表の中に閉じる。(b) マーカー名・アンカー名の接頭辞の一致は、正本（Editor の版・マスタ）が文字列で、決め打ちしない方が版の違いで黙らない（D-13・A-178）。(c) `PlaceTrainOnRail` の応答の `?.` は Assert の文面の中だけで、null も失敗も同じ「設置失敗」で止まる。出所: 設計検査 Phase 2.6（弱）→ agent 既定 | — | 列を enum にする: 名前の写しが L3 に増える |
| D-21 | ラダー 4 は青銅インゴットのレシピをサーバー直（`UnlockMachineRecipe`）で解放し、UI でレシピを選んでから入れる。出所: シミュレーター予測（ユーザー裁定 `.decisions/2026-08-30-レシピ未選択の機械は入力を一切受け入れない.md`・`機械入力スロットはサーバー側でもレシピ素材順に固定する.md` との照合）→ agent 既定 | 石窯のレシピは全部未解放で、未選択の機械は入力を受けない。plan §4.4 のラダー 4 の内容に研究は無く、研究の流れはラダー 5 が持つ。選択の操作は S4-machine の UI を踏むのでラダーに含める | 研究で解放する: ラダー 5 と流れが重なり、ラダー 4 が研究ツリーに依存する。初めから解放済みのマスタを使う: ピン先マスタと別のデータになる |
| D-22 | 束 4 の暗転の起点は、S4 の 7 レーンのうち最も早く分岐したレーンの分岐点で取り込み済みだった master（指揮の記録があればそれ）。出所: 独立評価（opus 3） | 台帳の運用は「束のレーンを始めたときの最新の master」。S4 は alpha 列と research 列が並走し、どのレーンも暗転を書いていないので、最初に始めたレーンの時点が起点。後の点を取ると、その間に入った Web 変更が G1 の追従から漏れる | alpha のマージの直前の master: research 列が先に始めた分の Web 変更が漏れる |
| D-23 | 全録画は系ごとに一覧を分ける。`scenarios/uitk/` 配下は UITK のレーンが全体スイッチ GameUi で流す前提で作ったもの（多くは `WebUiHost.Hub == null` を要求し、要求しないものも UITK のロケータの名前で書かれている）として Web の一覧から外し（外した本数と名前は `list-web-excluded.txt` から `done.md` へ）、Web の退行の比較は S4 前のコミットにもあるシナリオだけで行う（無いものは「比較不能（新設）」）。wwxy の表から外すのは直すレーンが X-prerun の行と、ファイルが消えた行だけ。出所: 独立評価（codex 2・opus 5） | UITK 専用を Web で流すと正常な Web 起動を失敗にする。比較元に無いシナリオでは既存か退行かを分けられない。S2-ladder・S3-ladder が直した 8 本は表に残っても流すべき | 全本を両方の系で流す: 偽の失敗が出る。表の全行を外す: 直った 8 本が黙って抜ける |
| D-24 | gamma の新規シナリオ 5 本は `scenarios/uitk/stage4/` にまとめる。出所: 独立評価（codex 4） | AGENTS.md の 10 ファイル上限に、アセンブリに入らないことを理由にした例外は無い。`scenarios/uitk/` は起草時 10 本で、そこへ足すと超える。他レーンのファイルは動かさない | `scenarios/uitk/` 直下: 上限を超える（旧 D-8 は撤回） |
| D-25 | 並べ比べの組は「参照キャプチャ（mock-host の正本。束 4 の起点を含む段 4 の頭の撮り直し、無ければ gamma が撮り直す）／Web の実ゲーム（CEF）／UITK の実ゲーム」の 3 列にする。参照とは状態に依らない配置・装飾だけを比べ、状態に依る差は同じ状態の CEF と UITK の組で比べる。「CEF 由来」はその組で裏取りできた差に限る。出所: 独立評価（opus 4・2 周目 opus N1・codex 2） | 設計書 §3.3 で Web の見た目の正本は参照キャプチャ。CEF 由来の不具合は UITK 切替まで直さない裁定があり、CEF だけと比べると崩れを写したまま差 0 になる | CEF の実ゲームだけと比べる: 正本と結び付かない |

ランタイムの挙動（UI・入力）を確かめるので、unityプレイ録画テスト（Task 5・6・7・9・12）と EditModeInPlayingTest（Task 3）を入れた。

## 未確認

- S4-train の駅名の確定（`train-station-name-apply`）と時刻表の即時送信・保留の印（`train-timetable-section--busy`）は起草版の計画から写した。マージ後の実物で Task 1 Step 2 の照合をする
- S2-ladder・S3-ladder の計画は未起草（§1.4 で ○）。ラダー 2・3 のシナリオ名（`uitk-opening-hud`・`uitk-build-bp`）は plan §4.4 の名前で、実物は Task 1 Step 1 で確かめる
- `UIElementsUpdatePanels`・`UIElementsRepaintPanels` の実際のマーカー名（Editor の版で `UIElements.UpdatePanels` のように変わりうる）。Task 4 は名前に「UIElements」と「UpdatePanels／RepaintPanels」を含むものを拾い、0 本なら一覧を出して止まる
- 石窯の入力・出力の枠の名前（`machine-input-slots` の子のスロットの名前）。S4-machine のマージ後に照合
- 研究ツリーを開くキーが `R` か（`uitk-research-tree` の録画の操作を Task 1 Step 3 で読む）

## 本PR外のリファクタ提案

- `scenarios/train/train-timetable-via-ui.cs` の準備（駅 2 基・結線・機関車）は `PlaytestTrainLine.Build` と同じ役割。既存シナリオの持ち主（S4-train）が `PlaytestTrainLine` を使う形へ寄せられる（同役割の判断: 同じ駅の位置・同じ結線・同じ機関車の選び方）
- 各レーンの録画シナリオの冒頭 6 行（焦点無視・デバイスの有効化）は全シナリオに同じ形で並ぶ。DSL の `PlaytestRunOptions` に入力の準備を持たせる形が考えられる（`uitk-lifeline.cs` と本計画の 5 本が同じ 6 行）

---

## 設計書・実行計画へ反映する行

指揮が設計書・実行計画へ写す。gamma は直接編集しない。

### 設計書 §7.1（A-175〜A-179）

| ID | 判断 | 却下した案と理由 |
|---|---|---|
| A-175 | 段の締めの「同時に出る組」の L3 は、面の名前と載る列の名前の表（Web の `App.tsx` と `UiScreenTraitsTable` から写す）を持ち、本番の遷移で画面を巡って「出るべき面だけが見え・期待の列の子孫にあり・同名が 1 つで・中心の `panel.Pick` が自分か子孫を拾う」を照合する。同時に出ない組（ビューアと機械 UI）は行き来の後の残骸で見る（S4-gamma） | 画面ごとの L1 に任せる: L1 は 1 画面のパネルで、列の取り合い・重なり・前の画面の残骸が見えない。同時に出ない組を同時に出す: 本番で起きない状態を作る |
| A-176 | マスタのアンカーの確かめは、シナリオがピン先マスタの `uiHighLight` を集めて件数を assert し、画面ごとに `TutorialAnchorRegistry.Resolve` が `Unique` で、`AddOutlineHighlight` の枠の中心がアンカーの中心から 5px 以内（画面内のとき）で、`CompleteTutorial` で消えることを見る（S4-gamma） | アンカーの文字列をテストに並べる: ピンの変更で黙って古くなる。Resolve だけを見る: 枠が別の位置に出るずれを見逃す |
| A-177 | 全録画（段階 1・4 の終わり・切替 PR）の結果は 1 本 1 行の TSV（scenario・system・exit・seconds・run_dir）に残し、PASS／束 5 未移植（段階 4 のとき）／wwxy／前提つき／Web 専用／不具合 の 6 つに分ける。Web 側の失敗は段階の前のコミットで流し直して既存と退行に分ける。不具合と退行は持ち主へ回して 0 にしてから段を締める（S4-gamma） | 失敗を全部不具合にする: 段と無関係の既存の失敗を抱える。分類を書かない: 次の全録画で同じ切り分けをやり直す |
| A-178 | UI の CPU 時間は `ProfilerRecorder` で名前に `UIElements` と `UpdatePanels`／`RepaintPanels` を含むマーカーを拾って和を取り、Editor の PlayMode・1920×1080 で画面ごとに 240 フレームの平均・p95・最大を台帳に書く。1ms は目安で合否にしない（S4-gamma） | マーカー名を決め打ちする: Editor の版で名前が変わると 0 を測って気づかない。合否にする: plan §2.5 は目安 |
| A-179 | 段の締めの並べ比べは、各レーンの報告ページの Web／UITK の組と、締めのレーンが同じ実ゲームの状態を両方の系で撮った組（同時に出る画面 × 1280×720・2560×1080）を codex-audit にかけ、記帳済みの LX を除いて差を出させ、別の subagent が画像で再現した差だけを持ち主へ回す。直すか例外にするかは指揮が判定し、例外にする差は締めのレーンが範囲の番号で LX の文面を書き、台帳の行とコードの印は差のあるファイルを直すレーン（I-fix）が同じ PR で置く（S4-gamma） | mock-host の参照だけと比べる: 実ゲームの同時表示の重なりが見えない。指摘をそのまま回す: 再現しない指摘で持ち主の手が止まる |

§3.4 の「`LX-n` の番号は…台帳「## 見た目の例外」の末尾へレーンが追記する」に「（段の締めが範囲を持つ例外は、印を置くレーン〈I-fix〉が台帳の行と印を同じ PR で入れる。`LookExceptionLedgerTest` が同じ HEAD で両方を照合するため）」を足す案（D-17）。§3.3 の「指揮は波ごとに codex-audit にかける」を「段の締めのレーンが波ごとに codex-audit を回して再現と処理の案を出し、指揮が判定する」へ改める案（D-18。実行計画 §5 の「段の締め（S4-gamma）で codex-audit にかける」と揃える）。§3.3（比べ方）に A-179 を 1 行で足す案: 「段の締めは、同じ実ゲームの状態を両方の系で撮る撮影シナリオ（`scenarios/uitk/uitk-stage<N>-wave-shots.cs`）を持つ」。

S4-recipe の D-9（`GameScrollView` のつまみ 5 本）は S4-recipe の計画の「設計書へ」の行が正本で、gamma は写すだけ（指揮が S4-recipe の割り当て行と一緒に入れる）。

### §2 のカタログ差分

部品の追加・変更は無い（gamma は部品を作らない）。DSL の口 `Client.Playtest/Operations/Ladder/`（`PlaytestTrainLine`・`PlaytestPanelCostRecorder`）と `Operations/ScenarioChecks/PlaytestAnchorChecks` を、§2 の検査の基盤の表があればそこへ 1 行で足す案:「`Operations/Ladder/`: 段の締めのラダーの準備と UI の CPU の計測。`ScenarioChecks/PlaytestAnchorChecks`: マスタのアンカーの照合（S4-gamma）」。

### 実行計画 §1.4 の S4-gamma 行の改め案

依存の列を「S4-* 全部・S2-ladder・S3-ladder（ラダー 1〜5 緑に `uitk-opening-hud`・`uitk-build-bp` が要る）・S2-tutorial（アンカーの枠）」に改め、DAG に `S2L --> G`・`S3L --> G` の辺を足す。完了条件の列は「ラダー 1〜5 緑（4 が production-loop・5 が research-train）」（R-4・Task 12。実行計画 §1.4 へ反映済み）とし、末尾に「（全録画の 2 回目は UITK と Web の両方。Web 側は S4 前のコミットと比べて退行 0）」を足す（plan §2.0-2 の写し）。

### 統合計画 §4.4 のラダー 4 の文言の改め案

「Tab → 長押しクラフト → 石窯に燃料と素材を入れる（掴み・Shift・右ドラッグ）→ 取り出す → 整理」を「Tab → 長押しクラフト → 石窯のレシピを選び素材を入れる（掴み・Shift・右ドラッグ）→ 開いたまま出来上がりを見る → 取り出す → 整理」へ（D-21。ピン先マスタの石窯は `ElectricMachine`・`requiredPower 0` で燃料の枠が無く、未選択の機械は入力を受けない）。

### 実行計画 §6.1 の割り当て行

| レーン | 設計書 §7.1 | 台帳 `LX-n` |
|---|---|---|
| S4-gamma（`P/2026-11-10-uitk-s4-gamma.md`） | A-175〜A-179 | LX-141〜145（段階 4 の波の並べ比べで再現した差のうち例外にするものの予約。文面は gamma が書き、行と印は I-fix が置く） |

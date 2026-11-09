# UITK S4-train（段階 4: 列車族のインベントリ）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Web 版の `features/blockInventory/train/*`（`TrainInventoryBody`・`TrainTimetableSection`・`StopList`・`useImmediateTimetable`）と `views/TrainPlatformInventory`（`TrainStationNameSection`・`TrainPlatformSection`）を、S4-alpha の外殻・拡張口と S4-machine の液体の中身表示（`FluidInventoryContents`）・液体の Model（`IBlockFluidSlotsModel`）を使って、Web と同じ見た目・同じ操作の UITK 画面にする。列車（source=train）の本文は貨車スロット・液体貨車・時刻表タブ（即時送信の編集・自動運転の切替）、ブロックの本文は TrainStation（駅名＋スロット＋積込/卸し）・TrainItemPlatform（スロット＋積込/卸し）・TrainFluidPlatform（液体＋積込/卸し）。L1・並べ比べ（720p・2560×1080）・録画（新設 `uitk/uitk-train-inventory`。古い適用ボタン前提で Web 系でも壊れている `train/train-timetable-via-ui` は両系で通る形に直す）まで済ませて `feature/uitk` へ取り込む。

**Architecture:** 画面レーン（単位エージェント・Editor 要）。設計書 §4.2「Model の購読は Presenter、View は Render だけ」と A-8「View が型付きコマンドを直接呼ぶ（Presenter は中継しない）」に従う。State（`Client.Ui.State/Train/` の新しい 6 ディレクトリ）に源の Model と、編集の器・描画値の Model・コマンドを DI の単一インスタンスで置く。源の Model は源を購読して `IReadOnlyReactiveProperty<…>` を配る: (1) `TrainTimetableModel`（`TrainUnitClientCache`・`IClientTrainTimetableLookup`・`TrainTimetableFetcher`・`BlockGameObjectDataStore`・`OpenTrainTimetableTracker` から、Web ホスト `TrainTimetableDtoBuilder` と同じ規則で Loading / Unavailable / Ready を組む。同じ状態は流し直さない）、(2) `TrainInventoryBodyModel`（スロットは alpha の `IBlockInventoryPanelSource`、液体は `SubInventoryState`）、(3) `TrainPlatformBodyModel`（スロットはパネルの源、駅名・積込/卸しは追跡ブロックの StateDetail を純関数 `TrainPlatformDetailReader` で、液体は S4-machine の `IBlockFluidSlotsModel`）。編集は前例 `PauseMenuStateService`＋`PauseMenuModel`＋`IPauseMenuCommands` と同じく、器（State）・描画値の Model（`Current` だけ）・コマンド（View が直接呼び `UiCommandResult` を返す。A-8）に分ける（D-33）: 時刻表は器 `TrainTimetableEditState`（列車ごとの `TrainTimetableEditSession`〈`useImmediateTimetable` の写し。保留・受理・公開待ち 5000ms・拒否の巻き戻し〉を持つ）・`TrainTimetableEditModel`・`TrainTimetableEditCommands`（`Add`・`Move`・`Remove`・`SetAutoRun`）、駅名は器 `TrainStationNameEditState`（下書き）・`TrainPlatformEditModel`・`TrainPlatformEditCommands`（入力・決定・移送モード）。器を Model とコマンドで共有するのは、View が直接呼ぶコマンドと Presenter が購読する描画値が同じセッション・下書きを見るため。サーバーへの送信は `TimetableEdit/Send/` の `ITrainTimetableCommands` と `ITrainBlockCommands`。拒否は理由ごとの `UiCommandRejectReason`（「開いていない」は S4-machine と共有の `BlockNotOpen`。どれも非良性でトーストする。D-31・D-32）。View 側（`Client.Ui/Screens/BlockInventory/Train/`）は alpha の拡張口に差す工場 2 本（列車の本文・`BlockInventoryBodyKind.TrainPlatform` の本文）が「Presenter（Attach で Model の `Current` を購読して `View.Render` へ流す・Detach で切る）＋ View（押下でコマンドを直接呼ぶ）」の組を作る。パネルごとの局所状態（列車の本文のタブと取得の契機）は区画の Presenter が持ち、別のパネルで作り直す（S4-machine の `MachineSelectionModeState` と同じ。D-33）。見た目は Web と同名のトークン（`--mode-switch-gap`・`--mode-switch-padding-inline`・`--mode-switch-padding-block`・`--bevel-1`・`--bevel-c1`・`--text-high-contrast`・`--text-muted`）と `UiTypographyClass` だけを使い、ローカルなつまみは持たない。

**Tech Stack:** Unity 6000.3.8f1 UI Toolkit、UniRx、UniTask、VContainer 1.10、NUnit＋`com.unity.ui.test-framework`、uloop CLI、プレイテスト DSL（`run-scenario.sh`）、node（test-name-parity）、pnpm（gen:i18n）、gh CLI、bd（Beads）

- **パスの略記**: `S/` = `moorestech_client/Assets/Scripts/`、`W/` = `moorestech_web/webui/`、`LOGS` = `../moorestech_logs`（このリポジトリの兄弟の記録 repo）、`SKILL` = `.agents/skills/unity-playmode-recorded-playtest`。Files 節とコマンドはリポジトリ相対の完全なパスで書く
- **正本**: 裁定 `.decisions/2026-11-06-*`（「段階2以降の画面もWeb版と同じ見た目にする」「長命ブランチの中は全体スイッチ1つ」「UITK切替までCEF由来の不具合は直さない」）、設計書 `docs/uitk-migration/execution/component-architecture.md`（§1・§2・§3・§4.1〜§4.3・§5.1・§7.1）、実行計画 `docs/uitk-migration/execution/execution-plan.md`（§1.4 の S4-train 行・§4.3・§5・§6.2・E-1・E-16・E-18〜E-23）、S4-alpha の計画 `docs/superpowers/plans/2026-11-10-uitk-s4-alpha.md`（改訂版 85f905aa3d）「## 後続レーンへの Produces 一覧」、S4-machine の計画 `docs/superpowers/plans/2026-11-10-uitk-s4-machine.md`（09b9a73226）の Task 3・Task 6 の Produces、本体の追加決定（2026-11-10。液体の中身表示は S4-machine が `Screens/BlockInventory/Fluid/` に作り S4-train は使うだけ・Model/Presenter/View の分け方）。食い違ったら 裁定 → 統合計画 → 設計書 → 実行計画 → この計画 の順で上が勝つ

## Requirements

設計の出所: 実行計画 §1.4 の S4-train 行（`TrainInventoryBody`・時刻表・駅名・プラットフォームの本文。`train.spec`・`timetable.spec`・`TrainTimetableImmediate.test`・`TrainTimetableSection.test`・`TrainInventoryBody.test`・`trainPlatform.spec`）、本体の決定 4 件（液体の中身は S4-machine・写すのは実物・古い録画は直す・Web ホストの導出の作り直しは重複として記録〈C4-e の agent 既定と同じ据え置き〉）、alpha の改訂（パネルは識別子だけ・各レーンが Model を持つ・工場は Presenter＋View の組）、レビュー r1 の反映（D-6・D-8・D-12・D-13・D-16〜D-24）、レビュー r2 の反映（D-2・D-20・D-21・D-22 を直し、D-25〜D-30 を足した）。

| ID | 要件 | 受け入れ基準 |
|---|---|---|
| R-1 | 列車の本文の器: `UiStack` に `ModeSwitch<TrainInventoryTab>(train-tabs, Horizontal, Tablist)`（`train-tab-inventory` 持ち物 / `train-tab-timetable` 時刻表）と `GameScrollView(train-body-scroll)`。間は `--mode-switch-gap`。開いた直後は持ち物タブ。同じ車両のあいだ（画面を隠して戻しても）タブは保たれ、別のパネルでは持ち物タブから（タブは列車の本文の Presenter の局所状態 `TrainTimetableFetchTrigger`。`IsSamePanel` が偽で作り直す。D-33）。タブの押下は View の `OnTabSelected` で Presenter へ。スクロール器の高さは alpha の大型パネル（`.block-inventory--large .block-inventory__panel` の高さと `block-inventory__body` の `flex-grow:1; min-height:0`）が決め、列車の `.train-body`・`.train-body__scroll` はその鎖を `flex-grow:1; min-height:0` でつなぐだけ（Web `BlockInventoryPanel.tsx` の「大型面は本文側が器を持つ」と `panelBodyScrollArea` の `.scroll`。D-37） | `TrainInventoryBodyTest`「asks C# to fetch only when the timetable tab is selected」・「隠して戻した同じ車両はタブを保ち別のパネルでは持ち物タブから」が緑。Task 10 の自撮り「本文の高さ」で、停車駅の多い時刻表で本文がパネルの高さに収まりスクロールする（D-37） |
| R-2 | 持ち物タブ: 器 `train-inventory-contents` の中に、液体があれば `FluidInventoryContents(train-inventory-fluids)`（S4-machine）、無ければ `BlockItemGrid(train-inventory-slots)`（alpha。押下は `IBlockSlotCommands`）。スロットは alpha のパネルの源（エラー無しの `OpenTrainInventoryPanel.ItemSlots`）から読む。液体貨車は名前と `量 / 容量`、再取得した量へ描き直す | `TrainInventoryBodyTest`「液体貨車は名前と量容量を表示し、再取得した量へ更新する」・`TrainInventoryFlowTest`「貨車スロットとコンテナ不在エラーを表示する」が緑 |
| R-3 | 時刻表タブ: Loading は `train-timetable-loading`（`ui.blockInventory.timetableLoading`）、Unavailable は `train-timetable-unavailable`（`timetableUnavailable`）、Ready は `TrainTimetableSection`。Ready の区画は持ち物タブのあいだも作ったまま隠す（`display:none`。Web は hidden で保つ）。区画は `TrainUnitInstanceId` ごとに作り直す（Web の key） | `TrainInventoryBodyTest`「shows loading and unavailable as distinct states without warning」が緑 |
| R-4 | 取得の依頼: 時刻表タブへ切り替えた瞬間と、時刻表タブのあいだに Loading が届くたびに列車の本文の Presenter が `ITrainTimetableCommands.RequestOpen()` を呼ぶ（判定は純粋な `TrainTimetableFetchTrigger`。D-33）。取得中の表示は Fetcher の開始の合図（`OnFetchStarted`）を `TrainTimetableModel` が購読して組み直す（D-20。Web ホストは購読しない。本番の RequestOpen は `OpenTrainUnitResolver` で列車を引き `TrainTimetableFetcher.RequestForOpenedTab`、引けなければ `MarkTabOpenedWithoutTrain`。Web ホスト `TrainTimetableOpenActionHandler` と同じ）。Ready でもタブ選択では依頼する | `TrainInventoryBodyTest` の fetch 3 件と「使えないの間でもタブを選ぶと取得を1回頼む」・`TrainTimetableFetchTriggerTest`・Hostless の `TimetableStateProbe`（開始の通知が 1 回）が緑。録画 `train/train-timetable-via-ui` で `[TrainTimetableFetcher] requesting timetable` が開いた直後 0・タブ選択で 1 |
| R-5 | 時刻表の状態: Web ホスト `TrainTimetableDtoBuilder.Build` と同じ優先（所有列車が引けない→Unavailable〈警告〉・`IsUnavailable`→Unavailable・`IsFetchInFlight`→Loading・キャッシュ無し→Loading・Ready）。駅一覧は `GetBlocksByBlockType(TrainStation)` の位置と `TrainStationNameStateDetail.StationName`（無ければ空）を x→z→y で整列。停車駅の名前は駅一覧から引き、引けない位置の集合が前回と変わったときだけ警告。未知の Side があれば Error ログで Unavailable。同じ状態（Ready は中身）は流し直さない（D-6） | `TrainTimetableReadyDataFactoryTest`・`TrainTimetableStationCatalogTest` が緑 |
| R-6 | 時刻表の区画: `UiStack(Xs)`（`train-timetable-section`。保留中は `train-timetable-section--busy`）。自動運転の行（`ui.blockInventory.timetableAutoRun` ＋ `ModeSwitch<bool>(train-timetable-auto-run, Horizontal, Group)` の `-on` / `-off`）。on は保留中かサーバーの停車駅が 0 件で無効、off は保留中で無効。押すと楽観更新なしで View が `ITrainTimetableEditCommands.SetAutoRun` を直接呼び即送信。停車駅が 0 件なら `timetableAutoRunUnavailable` を出す。Mantine `Text size="sm"` の 9 か所は `UiTypographyClass.TextSm`、停車駅・駅一覧の行ラベルは Mantine の body 16px（`--mantine-font-size-md`）（D-22） | `TrainTimetableSectionViewTest`「blocks auto-run until an added stop is authoritative and sends toggles immediately」が緑 |
| R-7 | 停車駅: `GamePanel(train-timetable-stops-panel, Craft)` の題 `timetableStops`＋`FadeRule`。0 件は文言 `train-timetable-stops`（`timetableStopsEmpty`）、それ以外は `train-timetable-stops` の列に行 `train-timetable-stop-{i}`（現在位置は `train-timetable-stop--current`＝`--text-high-contrast`。表示中の並びがサーバーの並びと同じときだけ。現在位置は `int?`）。行のラベルは `timetableStopLabel`（`{index}. {station}`）。操作 `-up`（先頭で無効）・`-down`（末尾で無効）・`-remove`（保留中は全部無効）。押下は View が `ITrainTimetableEditCommands.Move`・`Remove` を直接呼ぶ | `TrainTimetableSectionViewTest`「disables moves past either end and follows remote changes when idle」が緑 |
| R-8 | 駅一覧: `GamePanel(train-timetable-stations-panel, Craft)` の題 `timetableStations`＋`FadeRule`。0 件は `timetableStationsEmpty`。行は駅のラベル（`TimetableEditLogic.StationLabel`。名前が空なら `timetableUnnamedStation`）と `PanelActionButton(train-timetable-station-{x}_{y}_{z}-add, timetableAdd)`（保留中は無効。押下は `ITrainTimetableEditCommands.Add` を直接呼ぶ）。行の右の子と左の間は画面のクラス `train-timetable__row-end` の `--mode-switch-padding-inline`。行の上の区切りは `--bevel-1 solid --bevel-c1`、無効な操作の文字は `--text-muted` | `TrainTimetableFlowTest` 2 件が緑。並べ比べ R-15 |
| R-9 | 即時編集（`useImmediateTimetable` の写し）: 送信中・保留中の編集は警告して捨てる。並びが同じ編集は何もしない。編集は表示を即時に替え（保留）、受理されたら同じ並びの公開が届くまで保留を保つ（公開が先に届いていれば受理で解く）。受理後 5000ms 公開が無ければ警告して解く。拒否は警告・トースト・最新のサーバーの並びへ戻す。閉じた後に戻った結果は捨てる（閉じたことは `TrainTimetableModel` が `UIStateControl.OnStateChanged` で使えないへ倒して伝える。D-29）。列車が替わるとセッションを作り直す（セッションは器 `TrainTimetableEditState` が持つ。D-33） | `TrainTimetableEditSessionTest` 6 件・`TrainTimetableEditModelTest`「送った後に閉じると…」が緑。録画 `uitk/uitk-train-inventory` 手順 6 で閉じた後に編集の Model が使えないになる |
| R-10 | 送信: 置換は `TrainScheduleEditRequest.CreateReplaceTimetableRequest(列車, 停車駅)`（出発条件は `WaitForTicks`・`GameUpdater.TicksPerSecond`。Web ホスト `TrainTimetableStopParser` と同じ）、自動運転は `CreateSetAutoRunRequest`（`AppliedIsAutoRun` が求めた値と違えば拒否）。列車・駅・PF が開いていなければ送らず拒否する。拒否は理由ごとの `UiCommandRejectReason`（R-13。Detail はサーバーの `FailureReason` を載せるときだけ。D-31）で返し、コマンド（`TrainTimetableEditCommands`・`TrainPlatformEditCommands`）が `UiCommandResultToaster.Report` へ渡す。どれも非良性で、Web と同じくトーストする（D-32） | `TrainTimetableDepartureTest` と `TrainTimetableFlowTest`「拒否された追加を巻き戻し、次の追加で復帰する」が緑 |
| R-11 | プラットフォームの本文（Kind `TrainPlatform`）: `UiStack(Sm)`（`train-platform-view`）に、描画値は `ClosedTrainPlatformBody`／`OpenTrainPlatformBody`（部分ごとの有無は S4-machine の `SectionContent<T>`。D-19・D-34）で、View は「あれば出す」の 1 規則。駅なら駅名の行、移送の状態がある品物 PF・駅ならパネルの源のスロットで `BlockItemGrid(train-platform-item-slots)`（空でも実枠を出す）、液体があれば `FluidInventoryContents(train-platform-fluid-slots)`（液体 PF は `IBlockFluidSlotsModel`。D-17）、移送の状態があれば `ModeSwitch<TrainPlatformTransferMode>(train-platform-mode, Horizontal, Group)` の `-load`（`trainPlatformLoad` 積込）/ `-unload`（`trainPlatformUnload` 卸し）。押すと楽観更新なしで View が `ITrainPlatformEditCommands.SelectTransferMode` を直接呼んで送る。描画値は追跡ブロックの状態変化 1 回につき 1 回だけ組む（D-35） | `TrainPlatformBodyTest` 3 件・`TrainPlatformDetailReaderTest` が緑 |
| R-12 | 駅名: `GameTextField(train-station-name-input, false, GaugeInset)`（プレースホルダ `stationNamePlaceholder`）と `PanelActionButton(train-station-name-apply, stationNameApply)`。手を付けていない入力は同期した名前に従う。送るのは決定を押したときだけ（入力のたびには送らない）。拒否は同期済みの最新の名前へ戻す（送った回の下書きが今も有効なときだけ。閉じる・開き直すと下書きは作り直される。D-25）。下書きは器 `TrainStationNameEditState` が持ち（描画値は `TrainPlatformEditModel`。D-33）、View が `ITrainPlatformEditCommands.EditStationName`・`ApplyStationName` を直接呼ぶ。入力欄は伸ばさない。プラットフォームでは行を出さない | `TrainPlatformBodyTest`「sends station names only through Set and hides the field for platforms」・`TrainStationNameDraftTest`・`TrainPlatformEditModelTest` が緑。録画 `uitk/uitk-train-inventory` 手順 2 |
| R-13 | 新しい断りの理由（理由ごとに 1 値・どれも非良性。D-31・D-32）: S4-machine と共有の `BlockNotOpen`（wire `block_not_open`。時刻表の `train_not_open` もこれ。先にマージした方が足し、後の方は使うだけ）、`TrainTimetableReplaceFailed`（`replace_failed`）・`TrainAutoRunFailed`（`set_auto_run_failed`）・`TrainAutoRunNotApplied`（`set_auto_run_not_applied`）・`TrainInvalidBlockType`（`invalid_block_type`）・`TrainStationNameFailed`（`set_name_failed`）・`TrainPlatformModeFailed`（`mode_request_failed`）。文言キーは値ごとに `ui.commandRejected.<値>`。`localization.csv` に 7 行（共有の 1 行が既にあれば 6 行）を足し `pnpm --dir moorestech_web/webui run gen:i18n` で再生成、`docs/uitk/TODO.md`「追従記録」に 1 行 | 既存の `PauseMenuCommandsTest`（`UiCommandRejectReason` の全列挙を回す良性の配列とトースト用の翻訳文の 2 件。配列もテスト名も変えない）が緑（D-32） |
| R-14 | 写すテスト（実物で数え直した S4-train 分 20 件＋alpha が train に割り振った共有 2 件。下の「写すテスト」表）。`node tools/uitk/test-name-parity.mjs --bundle 4` で S4-train のファイルの未対応 0。`<web-test>` で写した件は `docs/uitk/disposition.md` に書かない | parity の出力に S4-train のファイルの未対応が無い |
| R-15 | 並べ比べ: 参照 `4-block-train`・`4-block-trainTimetable`・`4-block-train-timetable-tab`・`4-block-trainPlatform`・`4-block-trainFluidPlatform` を 1280×720・2560×1080 で撮り差の表。参照の無い場面（駅名の行・Loading/Unavailable・保留中・液体貨車）は E-22 の自撮りで足す | 差の表の全行が「同じ／直した／LX-121〜LX-125／他レーン持ち」のどれか |
| R-16 | 登録: `S/Client.Starter/Registration/GameUi/Screens/BlockInventoryTrainGameUiRegistration.cs`（E-1 ad92a44777。無ければ作る）（Model・コマンド・タイマー・工場 2 本）を `MainGameUiRegistration` の画面登録区画から 1 行で呼ぶ。`GameUiTheme.tss` の `Screens` 区画に `@import` 2 行 | 全体スイッチ UITK で起動して `uloop get-logs --log-type Error` が 0 件。L2（`UiThemeImportBundleTest`・`UiStyleConventionTest`・`UiLayerDependencyDirectionTest`・`LookExceptionLedgerTest`）が緑 |
| R-17 | 録画: 新設 `uitk/uitk-train-inventory`（駅名の入力→決定→クライアントの駅名を待つ・フォーカス中の Esc/E の閉じ・積込/卸し・時刻表の追加と自動運転）、`train/train-timetable-via-ui`（Web 系でも壊れている適用ボタンの押下を消し、待ちをサーバーとクライアントの時刻表の駅数だけにして両系で通す）、`train/fluid-inventory-via-interact`・`uitk/uitk-lifeline` を全体スイッチ UITK で回し、Web 系でも `train/train-timetable-via-ui`・`train/train-station-default-name`（触らない）を回す（D-13） | 6 本の `result.json` が PASS、mp4、`done.md` に各回の `UiInputExclusivity.ActiveUiSystem` の値 |
| R-18 | 終わりは moores-code-review の全ブランチレビュー → pr-create（base `feature/uitk`・報告ページ）→ `origin/feature/uitk` の取り込み・compile・テスト → G1 緑 → `gh pr merge --merge` → bd を閉じる | `git log origin/feature/uitk` にこのレーンのマージコミット。`LOGS/harness/uitk/S4-train/done.md` の 1 行目が「状態: 完了」 |

**やらないこと（スコープ境界）**

- 外殻（題・閉じる・エラー行 `train-inventory-error`・大型の寸法・自然高のスクロール器）と拡張口そのもの（S4-alpha）
- 液体の中身表示 `FluidInventoryContents`・`FluidSlotRow`・ブロックの液体の読み手（S4-machine が `Screens/BlockInventory/Fluid/` と State に作る。S4-train は使うだけ）
- 乗車 HUD（S2-hud 済み。`train.spec.ts` の「乗車HUD…」は写さない）
- 時刻表の編集ロジック `TimetableEditLogic`（L0-s4b 済み）・`OpenTrainTimetableTracker`・`OpenTrainUnitResolver`（既存。変えない）。`TrainTimetableFetcher` には取得の開始の合図 `OnFetchStarted` を足し、Fetcher 内部の取得ラッチを State 内の別クラス `TrainTimetableFetchLatch` へ出す分割をする（公開の口・既存の通知の意味・振る舞いは変えない。D-20）
- 部品（`ModeSwitch`・`PanelActionButton`・`GameTextField`・`GameScrollView`・`GamePanel`・`FadeRule`）と Foundation は変えない。要るものは `done.md`「integrator への依頼」へ
- Web 側（`moorestech_web/`）と Web の橋（`Client.WebUiHost/**`）は変えない。例外は `localization.csv` の行を足したときの生成物の再生成だけ（E-1）。Web ホストにしかない導出を State へ 1 本にまとめる作り直しも本 PR ではしない（C4-e の agent 既定と同じ据え置き。D-2・「## 本 PR 外のリファクタ提案」）。State の既存 `TrainTimetableFetcher` には取得の開始の合図を足し、200 行を守るためFetcher 内部の取得ラッチを State 内の別クラス `TrainTimetableFetchLatch` へ出す分割をする。Fetcher の公開の口・既存の通知の意味と、Tracker・Web ホストの購読は変えない（E-1 の State の口への追記。D-20）
- 録画 `train/train-station-default-name`（Web 系の既定名の初期同期の主張のまま。D-13）
- 出発条件の UI（Web にも無い。固定の `WaitForTicks` 1 秒）

## 死活表

触る機構: `MainGameUiRegistration`（GameUi の系だけで登録。Web の系は登録の頭で return）・既存の `TrainTimetableFetcher`（取得の開始の合図 `OnFetchStarted` を足し、取得ラッチを別クラスへ出す。購読は `TrainTimetableModel` だけ。D-20）・`Client.Ui.State.asmdef`・`UiCommandRejectReason`（Settings・PauseMenu・分割ドラッグ・持ち物の移動・alpha のブロックの枠も使う）・`localization.csv`（Web の `localizationKeys.ts` も再生成）・`GameUiTheme.tss`・既存の `OpenTrainTimetableTracker`／`OpenTrainUnitResolver`／`SubInventoryState`／`OpenBlockDetailTracker`／`UIStateControl`（呼ぶ・読むだけ）・alpha の `IBlockInventoryPanelSource` と S4-machine の `IBlockFluidSlotsModel`（購読するだけ）・録画シナリオ `train/train-timetable-via-ui`（直す）と `uitk/uitk-train-inventory`（新設）。`Client.WebUiHost/**` は変えない（D-2）

| 操作 | 計画後 | 根拠 |
|---|---|---|
| Web 系: 列車インベントリの開閉・貨車の枠・時刻表タブ・即時編集・自動運転 | 生きる（不変） | Web の系では Client.Ui を登録しない。`Client.WebUiHost/**` も Tracker も変えない。Fetcher には UITK だけが購読する開始の合図を足し、取得ラッチの分割は振る舞いを変えない（既存の `TrainTimetableFetcherTest`・Hostless のプローブ・Task 11 Step 3 の Web 系の録画で確かめる） |
| Web 系: 駅名の入力・決定、PF の積込/卸し | 生きる | 同上 |
| Web 系: 新しい文言キー | 生きる | キーが 1 つ増えるだけで、Web では使わない |
| UITK 系: 貨車を開いて本文を出す（持ち物タブ・液体貨車） | 新しく使える | Task 5・7・8 |
| UITK 系: 貨車の枠の左／右／ダブル／Shift | 新しく使える | alpha の `BlockItemGrid` → `IBlockSlotCommands`（`TryReadBlockSlots` はエラー無しの列車で真。alpha D-37） |
| UITK 系: 時刻表タブ・追加／上下／削除・自動運転 | 新しく使える | Task 4・6・7、録画 `uitk/uitk-train-inventory` 手順 5・6 |
| UITK 系: 駅名の入力・決定、PF の積込/卸し | 新しく使える | Task 5・7、録画 `uitk/uitk-train-inventory` 手順 2・4 |
| UITK 系: 液体 PF の液体 | 新しく使える | 追跡ブロックの `FluidMachineInventoryStateDetail` を S4-machine の `IBlockFluidSlotsModel` が読む（D-17）。Task 1 Step 2 で空でないことを確かめる |
| UITK 系: テキスト欄にフォーカスがあるときの E・ESC での閉じる | 要確認 | C2-b の `UiInputExclusivity` の範囲。入力欄が閉じるキーを食えば閉じられなくなる（退化）。録画 `uitk/uitk-train-inventory` 手順 3（Task 7 Step 5・Task 11 Step 2）で確かめ、食うなら C2-b の持ち主へ上げる |
| 乗車 HUD（S2-hud） | 生きる | `TrainRidingHudModel` を触らない。Fetcher・Tracker の購読が増えるだけ |
| 既存の拒否理由（Settings・PauseMenu・分割ドラッグ・ブロックの枠） | 生きる | enum と switch の末尾に足すだけ（E-1）。足す 7 値（S4-machine と共有の `BlockNotOpen` を含む）はどれも非良性なので `PauseMenuCommandsTest` の benign 配列も名前も変えない（D-31・D-32） |
| 録画 `train/train-timetable-via-ui`（Web 系。X-prerun で回す） | 今は壊れている → 直る | 押していた `train-timetable-apply` は Web にも無い（`TrainTimetableImmediate.test.ts:38`）。押下を消し、待ちをサーバーとクライアントの時刻表の駅数だけにする（系ごとの保留の印は読まない）。Task 11 Step 1・Step 3 で Web 系でも回す |
| 録画 `train/train-timetable-via-ui`（UITK 系） | 新しく通る | 同上 |
| 録画 `train/train-station-default-name` | 生きる | 触らない（D-13）。UITK の駅名の入力・決定は新設の `uitk/uitk-train-inventory` が持つ。Task 11 Step 3 で Web 系でも回す |
| 録画 `uitk/uitk-train-inventory` | 新設（UITK 系だけ） | Task 11 Step 2 |
| 録画 `train/fluid-inventory-via-interact`・`uitk/uitk-lifeline` | 生きる | 書き換えない。UITK 系で回すだけ |
| ホットバー・HUD・他の常駐表示 | 生きる | 触らない |

## Global Constraints

- **作業場所**: `moores-wt new uitk-lane/s4-train --from origin/feature/uitk`（Editor 要）。最初に `pwd` で worktree にいることを確かめる。Unity は自分の worktree の Editor だけを使い、起動・停止の前に `moores-wt status` で所有を確かめる。Mac mini の Editor は同時 2 本まで。Mac が逼迫したら Windows の `~/bin/winunity`（compile/test/play を ssh で回す）も使える
- **依存**: S4-alpha（拡張口・`BlockItemGrid`・`IBlockSlotCommands`・パネルの状態）と **C4-a**（`FluidSlot`・`FluidSlotRow`・`FluidAmountSlot`。`FluidInventoryContents` の行の中身で、L1 の `TrainTestText.HasFluidIcon` が読むアイコンのクラス名はこの実装でしか決まらない。実行計画 §1.4 の S4-train 行の依存）と **S4-machine**（`FluidInventoryContents`・ブロックの液体の読み手）が `origin/feature/uitk` に入っていること（Task 1 Step 1 で確かめ、無ければ待つ）。ほかに C3-a（`GamePanel`・`FadeRule`・`PanelActionButton`）・C3-b（`GameScrollView`）・C4-c 系の `ModeSwitch`・`GameTextField`・L0-s4b（`TimetableEditLogic`・`BlockComponentRegistry`）。すべてマージ済み
- **登録の置き場**: W4 以降の画面レーンの登録ファイルは `S/Client.Starter/Registration/GameUi/Screens/` に置く（E-1 ad92a44777。`GameUi/` 直下は S4-alpha で 10 本になり上限に達する）。最初に要したレーンがディレクトリを作るので、Task 1 Step 3 で有無を確かめ、無ければこのレーンが作る
- **規約**: 1 ファイル 200 行以下・1 ディレクトリ 10 ファイル以下（`.meta` を除く）・partial 禁止・`Func<>` 禁止・イベントは UniRx（`Action` 禁止）・try-catch 禁止（このレーンに外部境界は無い。ネットワークの失敗は API が null を返す）・日英 2 行コメント・デフォルト引数禁止・単純 getter/setter 禁止（`{ get; private set; }` と式本体の計算プロパティは可）・初期化は `Initialize`・null チェックは外部データ（サーバー応答・StateDetail）だけ・fail-closed の経路は開発者ログ・意図して黙る行は `silent-ok: <理由> / <誰が気づくか>`・`#region Internal` はメソッド内のローカル関数だけ・`.meta` を手で作らない・Prefab/シーンを手で編集しない・`Update()` で同値判定しない（購読）
- **Model / Presenter / View（設計書 §4.2・A-8）**: Model（State）だけが源を購読し `IReadOnlyReactiveProperty<…>` を配る。判断・セッションの寿命・下書きは State の器が持ち、描画値の Model とコマンドが共有する（D-33）。Presenter（Screens）は `Attach` で Model の `Current` を購読して `View.Render` へ流し、`Detach` で全部切る（コマンドの中継はしない。パネルごとの局所状態〈タブと取得の契機〉だけは列車の本文の Presenter が持つ。D-33）。非表示の間は描かない: 持ち物タブの中身も時刻表タブの中身も表示中だけ購読する（設計書 §4.2 の SetShown の注記「出し入れだけ。描かないのは Presenter が表示中だけ購読して守る」。D-27）。View は Model を購読しない。押下は View が自分の子の部品の `OnClicked`・`OnOptionSelected`・`OnValueChanged` から State のコマンド（`ITrainTimetableEditCommands`・`ITrainPlatformEditCommands`）を直接呼ぶ。タブの押下だけは `TrainInventoryBodyView.OnTabSelected` で Presenter の局所状態へ流す。言語の変化は `LocalizedLabel` が自分で描き直し、解決済みの文字列を埋めた所だけ Presenter が描き直す（D-10）
- **State は `Client.WebUiHost` を参照しない**。Web ホストにしかない導出（駅一覧の整列・名前の補完・状態の優先・未知の Side・出発条件・`AppliedIsAutoRun` の照合・プラットフォーム/駅の詳細）は State に同じ規則でもう 1 本持つ（`moorestech_logs/harness/uitk/C4-e/done.md` 要裁定 1 と同じ agent 既定の据え置き。指揮の裁定ではない。負債が残る〈CEF 撤去の日に `Client.WebUiHost` 側を消す〉。D-2）。Web ホストが既に呼んでいる State の口（`TrainTimetableFetcher`・`OpenTrainUnitResolver`）は UITK も使い、Fetcher には取得の開始の合図を足し、Fetcher 内部の取得ラッチを State 内の別クラス `TrainTimetableFetchLatch` へ出す分割をする（公開の口・既存の通知の意味は変えない。D-20）。重複の一覧は「## 本 PR 外のリファクタ提案」
- **asmdef**: `S/Client.Ui.State/Client.Ui.State.asmdef` に要るのは `Game.Block`（`TrainStationNameStateDetail`・`TrainPlatformTransferStateDetail`・`TrainPlatformTransferComponent`）と `Core.Update`（`GameUpdater.TicksPerSecond`）。Task 2 Step 3 で grep し、無いものだけ末尾に足す（S4-machine の後なら `Core.Update` だけの見込み）。取り込みで同じ参照が衝突したら 1 行に畳む（E-1）。`Client.Ui` には足さない（View は State の `TrainPlatformTransferMode` を使う。D-8）
- **USS**: 画面 USS の先頭に出所の 2 行（日本語 1 行 `/* web: features/blockInventory/train/style.module.css …（日本語の要約） */` → 英語 1 行 `/* web: … (English) */`。1 行に日英を混ぜない）。行の中の間は画面のクラス（`train-timetable__row-end`・`train-station-name__row-end`）を右の子に付けて 1 本の規則で取る（D-22）。色はトークン経由だけ。部品のクラスを画面 USS の選択子の主語にしない。部品のつまみの上書きは画面のルートのクラスの下で（このレーンでは `--panel-action-button-text` の無効時の上書き 1 件。D-11）。`:root` のトークンを C# で読むときは読む要素のクラスで `--<部品>-<名>: var(--token)` と受け直す（`UssCustomStyleReader` の remarks。このレーンでは読まない）
- **計画の Produces と実物の照合**: alpha（改訂版 85f905aa3d）と S4-machine（09b9a73226）の型は計画から写した。両レーンの実装で綴りが変わっていれば Task 1 Step 2 で合わせる
- **使う部品と公開 API**（Components・Foundation・既存 State は `origin/feature/uitk` の実物、alpha と S4-machine はそれぞれの計画の Produces から写した）

```csharp
// ── alpha の口（改訂版 85f905aa3d の「後続レーンへの Produces 一覧」から写した）
namespace Client.Ui.Screens.BlockInventory.Registry
public interface ITrainInventoryBodyFactory { ITrainInventoryPart Create(); }                              // 登録は 1 本だけ
public interface ITrainInventoryPart { VisualElement Root { get; } void Attach(OpenTrainInventoryPanel panel); void Detach(); }   // Presenter（Root が View）
public interface IBlockInventoryPart { VisualElement Root { get; } void Attach(OpenBlockInventoryPanel panel); void Detach(); }
public interface IBlockInventoryBodyFactory { BlockInventoryBodyKind Kind { get; } IBlockInventoryPart Create(BlockInventoryBodyMount mount); }
public sealed class BlockInventoryBodyMount { public BlockInventoryBodyMount(bool fillsPanelHeight); public bool FillsPanelHeight { get; private set; } }   // テストは new BlockInventoryBodyMount(false)
namespace Client.Ui.State.BlockInventory.Panel
public sealed class OpenTrainInventoryPanel : BlockInventoryPanelState { public long TrainCarInstanceId { get; } public IReadOnlyList<SlotItem> ItemSlots { get; } public BlockInventoryPanelError Error { get; } }
public sealed class OpenBlockInventoryPanel : BlockInventoryPanelState { public string BlockType { get; } public Guid BlockGuid { get; } public BlockId BlockId { get; } public BlockInstanceId BlockInstanceId { get; } public Vector3Int Position { get; } public IReadOnlyList<SlotItem> ItemSlots { get; } public BlockInventoryPanelError Error { get; } public BlockInventoryComponents Components { get; } }
// 呼ばれ方: 同じパネル（列車は車両・エラー、ブロックは位置・Guid・エラー）かつ同じ大きさのあいだは何も呼ばれない。変わると Detach → Create → Attach。画面が隠れると Detach
// 列車の本文の工場が未登録なら alpha は空の部分を返す（null ではない。alpha D-35）。IBlockInventoryOperations.TryReadBlockSlots はエラー無しのブロック／列車のときだけ真（alpha D-37）
// テスト: Client.Tests.Ui.State.BlockInventory.PanelModel.BlockInventoryTestPanels.Train(error) / OfType(blockType, ...)・FakePanelSource(ReactiveProperty<BlockInventoryPanelState>)・BlockInventoryTestShell.Create(ScreenTestHost host, ReactiveProperty<BlockInventoryPanelState> state, params object[] plugged)
namespace Client.Ui.State.BlockInventory.Panel
public interface IBlockInventoryPanelSource { IReadOnlyReactiveProperty<BlockInventoryPanelState> Current { get; } }   // 共有サービス。スロットの更新でも配り直す
public abstract class BlockInventoryPanelState { public static readonly BlockInventoryPanelState Closed; public abstract bool IsSamePanel(BlockInventoryPanelState other); }
public enum BlockInventoryPanelError { None, OpenFailed, ContainerMissing, TrainCarMissing }   // エラー無しは == None
namespace Client.Ui.Screens.BlockInventory.Parts
public sealed class BlockItemGridFactory { public BlockItemGrid Create(string testId); }               // 共有サービス（MainGameUiRegistration）。枠 <testId>-slot-<i>。押下は IBlockSlotCommands へ配線済み
public sealed class BlockItemGrid : VisualElement { public void Render(IReadOnlyList<SlotItem> itemSlots, IReadOnlyList<int> slotIndices); }
namespace Client.Ui.State.BlockInventory.Logic.Common
public static class BlockDetailMath { public static IReadOnlyList<int> AllSlotIndices(int count); }

// ── S4-machine（計画 2f48c85880 の Task 3・Task 6 の Produces から写した）
namespace Client.Ui.Screens.BlockInventory.Fluid
public sealed class FluidInventoryContents : VisualElement { public FluidInventoryContents(string testid, GameContentPresentation content); public void Render(IReadOnlyList<BlockFluidSlotState> fluids); }  // Web FluidInventoryContents。中に UiStack(Xs) を 1 つ持つ。0 件で子なし。行 <testid>-slot-<i>、液体名と「量 / 容量」の文は行の兄弟（name = testid の器から読む）
namespace Client.Ui.State.BlockInventory.Fluid
public sealed class BlockFluidSlotsViewModel { IReadOnlyList<BlockFluidSlotState> Slots; bool HasProgress; float Progress; }
public interface IBlockFluidSlotsModel { IReadOnlyReactiveProperty<BlockFluidSlotsViewModel> Current { get; } }   // 追跡ブロックの液体（タンクの入力→出力・パイプの複製）。閉・列車〈source=train〉では空。液体 PF は追跡ブロックの FluidMachineInventoryStateDetail で入る（D-17）
public static class BlockFluidSlotConversion { static IReadOnlyList<BlockFluidSlotState> FromTanks(IReadOnlyList<FluidMessagePack> inputTanks, IReadOnlyList<FluidMessagePack> outputTanks); }
// テスト: FakeBlockFluidSlotsModel（Client.Tests の State/BlockInventory/Family/FakeFamilyModels.cs）

// ── Components（実物）
namespace Client.Ui.Components.Controls
public sealed class ModeSwitch<TValue> : VisualElement
{
    public ModeSwitch(string testid, ModeSwitchOrientation orientation, ModeSwitchRole role, IReadOnlyList<ModeSwitchOption<TValue>> options);
    public IObservable<TValue> OnOptionSelected { get; }        // 押しても自分では選ばない。無効な選択肢は流さない
    public void SetSelected(TValue value); public void SetSelectedNone(); public void SetEnabledAll(bool enabled); public void SetOptionEnabled(TValue value, bool enabled);
}
public sealed class ModeSwitchOption<TValue> { public ModeSwitchOption(string testid, TValue value, UiText label); }
public enum ModeSwitchOrientation { Horizontal, Vertical }
public enum ModeSwitchRole { Group, Tablist }
public sealed class PanelActionButton : Button { public PanelActionButton(string testid, LocalizationKey label); public IObservable<Unit> OnClicked { get; } }  // 文字色はつまみ --panel-action-button-text
public sealed class GameTextField : VisualElement
{
    public GameTextField(string testid, bool multiline, GameTextFieldLook look);   // GameTextFieldLook { GaugeInset, MantineInput }
    public TextField Field { get; }                                                // Field.name == testid
    public IObservable<string> OnValueChanged { get; }                             // IME の組成中も流れる
    public void SetValueWithoutNotify(string value); public void SetPlaceholder(UiText placeholder);
}
public sealed class GameScrollView : ScrollView { public GameScrollView(string testid); }
namespace Client.Ui.Components.Panels
public sealed class GamePanel : VisualElement { public GamePanel(string testid, GamePanelFrame frame); public void SetTitle(UiText title); public override VisualElement contentContainer { get; } }  // GamePanelFrame.Craft
namespace Client.Ui.Components.Decor
public sealed class FadeRule : VisualElement { public FadeRule(); }

// ── Foundation（実物）
public sealed class UiStack : UiGapContainer { public UiStack(UiGap gap); }  public sealed class UiGroup : UiGapContainer { public UiGroup(UiGap gap); }
// UiGapContainer: AddItem(VisualElement)・SetItemShown(VisualElement, bool)・ClearItems()
public class LocalizedLabel : Label { public LocalizedLabel(); public void SetKey(LocalizationKey key); public void SetText(UiText text); }
public static class UiTypographyClass { public const string TextXs, TextSm, TextMuted, TextDefault, TextHighContrast; }
public sealed class UiText { public UiText(LocalizationKey key); public UiText(LocalizationKey key, IReadOnlyList<LocalizationTextParam> textParams); public static UiText FromVerbatim(string text); }

// ── State（実物）
namespace Client.Ui.State.Train
public sealed class TrainTimetableFetcher { void RequestForOpenedTab(TrainUnitInstanceId id); void MarkTabOpenedWithoutTrain(); bool IsUnavailable(TrainUnitInstanceId id); bool IsFetchInFlight(TrainUnitInstanceId id); }
public sealed class OpenTrainTimetableTracker : IInitializable { IObservable<Unit> OnRepublishRequested { get; } }
public static class OpenTrainUnitResolver { bool TryResolveOpenTrain(SubInventoryState state, TrainUnitClientCache cache, out TrainUnitInstanceId id); bool TryResolveOwningTrain(long trainCarInstanceId, TrainUnitClientCache cache, out TrainUnitInstanceId id); }
namespace Client.Ui.State.Train.Logic   // L0-s4b
public readonly struct TrainTimetableStation { Vector3Int Position; string Name; }   public readonly struct TimetableEditStop { Vector3Int Position; string Name; StationNodeSide Side; }
public readonly struct TimetableReplaceStop { Vector3Int Position; StationNodeSide Side; }   public enum StopMoveDirection { Up = -1, Down = 1 }
public static class TimetableEditLogic { StationKey(Vector3Int); AddStop(stops, station); RemoveStop(stops, index); MoveStop(stops, index, direction); CanEnableAutoRun(stops); StationLabel(station, unnamedStationName) → UiText; ToReplacePayload(stops) → List<TimetableReplaceStop>; SameStopOrder(left, right); }
namespace Client.Ui.State.Commands
public sealed class UiCommandAccepted : UiCommandResult { public static readonly UiCommandAccepted Instance; }
public sealed class UiCommandRejected : UiCommandResult { public UiCommandRejected(UiCommandRejectReason reason, string detail); }
namespace Client.Ui.State.Toast
public sealed class UiCommandResultToaster { public void Report(string commandName, UiCommandResult result); }   // DI 登録済み（MainGameUiRegistration）
namespace Client.Ui.State.BlockInventory.Logic.Common
public abstract class BlockFluidSlotState { public static BlockFluidSlotState Empty(double capacity); public static BlockFluidSlotState Filled(FluidId fluidId, double amount, double capacity); }

// ── 源（実物）
SubInventoryState { SubInventoryModel CurrentSubInventory; ISubInventorySource CurrentSubInventorySource; IObservable<Unit> OnSubInventoryUpdated; }
SubInventoryModel { List<IItemStack> SubInventory; List<FluidMessagePack> Fluids; }    FluidMessagePack { int FluidId; double Amount; double MaxCapacity; }
TrainSubInventorySource { long TrainCarInstanceId; }    BlockSubInventorySource { Vector3Int BlockPosition; bool TryGetBlockIdentity(out Guid guid, out string blockTypeName); }
OpenBlockDetailTracker { BlockGameObject TrackedBlock; IObservable<BlockGameObject> OnTrackedBlockChanged; IObservable<Unit> OnTrackedBlockStateChanged; }
BlockGameObject { BlockMasterElement BlockMasterElement; BlockPosInfo BlockPosInfo /* OriginalPos */; T GetStateDetail<T>(string key); }
BlockGameObjectDataStore { IReadOnlyList<BlockGameObject> GetBlocksByBlockType(string blockType); }   // 確かめた（BlockGameObjectDataStore.cs:158）
TrainUnitClientCache;  IClientTrainTimetableLookup { IObservable<TrainUnitInstanceId> OnTimetableUpdated; bool TryGet(TrainUnitInstanceId id, out TrainTimetableSnapshot snapshot); }   // OnTimetableUpdated の型は確かめた（ClientTrainTimetableDatastore.cs:12）
Localize { static IObservable<Unit> OnLanguageChanged; static string Get(LocalizationKey key); }   // 確かめた（Localize.cs:22）
BlockMasterElement.BlockParam : IBlockParam   // Mooresmaster.Model.BlocksModule
TrainTimetableSnapshot { TrainUnitInstanceId TrainUnitInstanceId; bool IsAutoRun; int CurrentIndex; IReadOnlyList<TrainTimetableStop> Stops; }   TrainTimetableStop { Vector3Int StationPosition; StationNodeSide Side; }
ClientContext.VanillaApi.Response.Train { SendTrainScheduleEdit(TrainScheduleEditProtocol.TrainScheduleEditRequest, CancellationToken) → { Success; FailureReason; AppliedIsAutoRun }; SetTrainStationName(Vector3Int, string, CancellationToken) → { Success; AppliedName; FailureReason }; SetTrainPlatformTransferMode(Vector3Int, TrainPlatformTransferComponent.TransferMode, CancellationToken) → { Success; AppliedMode; FailureReason } }
TrainStationNameStateDetail { TrainStationNameStateDetail(string stationName); const string BlockStateDetailKey; string StationName; }   TrainPlatformTransferStateDetail { TrainPlatformTransferStateDetail(TrainPlatformTransferComponent.TransferMode mode); const string BlockStateDetailKey; TransferMode Mode; }   // Game.Block.Blocks.TrainRail。ctor は確かめた
TrainItemPlatformBlockParam / TrainStationBlockParam { int ItemSlotCount; }   TrainFluidPlatformBlockParam   // Mooresmaster.Model.BlocksModule
// ── テストの土台（実物）
ScreenTestHost { protected static T Find<T>(VisualElement root, string testid); protected bool IsShown(VisualElement element) /* 要素自身の resolvedStyle.display だけを見る */; protected static bool IsSelected(VisualElement root, string optionTestid); }
UiStyleConventionRules { static List<UiStyleViolation> CheckUss(string ussText, bool allowColorLiterals); }   UiStyleViolation { UiStyleRule Rule; }   // Client.Tests.Ui.Conventions。UiStyleRule.ColorLiteral
// Web ホストの拒否文字列（照合済み）: 時刻表 TrainTimetableActions.cs（train_not_open・replace_failed:<理由>・set_auto_run_failed:<理由>・set_auto_run_not_applied:<値>）、駅 TrainStationActions.cs（block_not_open・invalid_block_type・set_name_failed:<理由>）、PF TrainPlatformActions.cs（block_not_open・invalid_block_type・mode_request_failed〈接尾辞なし〉）
```

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み（レビュー r1）/ 違反 6 件・修正 6 件 / 死活表が無い（V-1 → 「## 死活表」を足した）・スロット列を alpha のパネルの源と別に作る（V-2 → D-16）・asmdef の重ね足し（V-3 → Task 2 Step 3 を grep して無いものだけに。E-1 の 1 行に畳む決まり）・液体 PF の液体の源（V-4 → D-17。S4-machine の R-3 も直っている）・blockType の自前の列挙（V-5 → D-21）・録画の Web 系の退化（V-6 → D-13。UITK の録画を `uitk/` に新設し、時刻表の録画は両系で通る形に直した）。注目点は Presenter の判断と寿命（→ D-12・D-22 の A-8 の形）、Reader の純関数化（→ D-21）、`Refresh` の欠け（→ D-20）、E-1 の列挙（本体が fce54ea0b5 で足した）、受け入れ基準のテスト名（→ D-24）を直した
- Phase 2.6（型閉包・重複・ADR 矛盾）: 実施済み（レビュー r1）/ 強 7・弱 9・第 3 バケツ 8 / 強: 編集セッションの null＋bool（5-A1 → D-18）・`-1` の番兵（5-B1 → D-19）・PF の描画値の判別子と片側の値の同居（5-B3・5-C2 → D-19）・Presenter の中継と ADR A-8 の矛盾（7-1 → 設計書 A-8 に合わせて State に編集の Model を置いた。D-12）は直した。D-2 の根拠と ADR の矛盾（7-1）は C4-e の要裁定 1 と同じ据え置きにし、誤った根拠「E-1 に反する」を書き直した（D-2。呼び名は r2 で agent 既定に直した）。`Localize.Get` の素通し（5-I6）は再現せず直していない。弱: bool の合成（5-C1）・blockType の部分分岐（5-I3）・判定と表示の別入力（6-1）は D-19・D-21 の組み替えで解消し、移送モードの写しの 2 か所（6-4）は `TrainPlatformTransferModeConversion` 1 本にした（D-8）。空名（5-B2）・失敗理由の畳み（5-C3）・`response?.FailureReason`（5-C4）・理由の enum と Detail（5-I2）は Web ホストと同じ形なので見送り、応答の判定の繰り返し（6-4）は D-2 の据え置きの負債として残す。第 3 バケツ 8 件は「## 本 PR 外のリファクタ提案」へ
- 配置検査 r2（差分検査）: 実施済み / 違反 2 件・修正 2 件 / View が列車の型 `TrainUnitInstanceId`（`Game.Block.Interface`。`Client.Ui.asmdef` に参照が無くコンパイルできない）を持つ（V-r2-1 → State の `ReadyTrainTimetableArea.IsSameTrain` で比べる。D-28）・D-21 の「駅の判定は 1 か所」が実物（Reader と `TrainBlockCommands.SetStationName` の 2 か所）と違う（V-r2-2 → D-21 の記述を実物に合わせた）。注目点の D-20 の手呼び（N-1）は Fetcher の開始の通知を購読する形に直した（D-20）
- Phase 2.6 r2（差分検査）: 実施済み / 強 4・弱 11・第 3 バケツ新規 1 / 強: View が列車の型を持つ（7-1 → D-28）・`_session` と `_server` の組（5-A1 → セッションが `Server` を持つ。D-30）・D-2 の根拠の呼び名（「本体の裁定」→ C4-e の `done.md` 要裁定 1 と同じ agent 既定。D-2）は直した。`Localize.Get` の素通し（5-I6）は r1 と同じく再現せず直していない。弱: `SameItems` の自前の比べ（N-7 → `SequenceEqual`。D-30）と D-20 の手呼び（N-1 → 購読。D-20）は直した。編集 Model の口が void（N-2）・液体の有無の出し分け（N-3）・Commands が `SubInventoryState` を読む（N-4）・フォーカス中の E/Esc（N-5。未確認に既載）・実時間の時間切れ（N-6。A-156 に既載）・`ReceivePanel` の 2 か所ほか検査 6 の 4 件は、故障シナリオが無いか r1・D-2 の据え置きの範囲なので見送った。第 3 バケツ新規: 移送モードの写しが Web ホストと 2 系統 → 「## 本 PR 外のリファクタ提案」に足した
- 配置検査 r3（差分検査）: 実施済み / 違反 1 件・修正 1 件 / 既存の `OnFetchFailed` を「開始と失敗」に広げて Tracker 経由で Web ホストの配信を毎回 Ready→Loading→Ready にしていた（V-r3-1 → 開始の合図 `OnFetchStarted` を別に足し `TrainTimetableModel` だけが購読、Tracker と Web は不変、Fetcher の 200 行は取得ラッチの分割で守る。D-20）。codex の指摘（持ち物タブも非表示の間に描いていた）も直した（D-27 を両タブに広げ L0 を 1 件）
- Phase 2.6 r3（差分検査）: 実施済み / 強 1・弱 4 / 強: E-1 は「足す」だけを許すのに既存の合図を変えていた（7-1 → V-r3-1 と同じ直しで解消。D-20）。弱: Global Constraints の「そのまま使う」（7-2 → 「Fetcher には開始の合図だけ足す」に）・Web ホストのコメントが古くなる（7-3 → Tracker を変えないので解消）・`TrainBlockNotOpen` と `MachineRecipeBlockNotOpen` の重複（N-r3-1 → D-26 に 1 行、integrator への依頼 (e)）・D-29 の却下理由の比べ方の片寄り（N-r3-2 → 閉じの入力が選んだ案でも 2 系統なことを書いた）。opus の軽微 4 件（良性の配列の食い違いと Task 2 のコミット漏れ・対象外の節の「変えない」・csv の行数・液体の写しの枠内の数字と名前の消去）も直した
- 配置検査 r4（差分検査）: 実施済み / 違反 0 件
- moores-code-review（計画 W4a の全体レビュー）: 反映済み / 拒否を理由ごとの値にし Detail はサーバーの失敗理由だけ（D-9 を覆し D-31）・拒否はすべて非良性、「開いていない」は S4-machine と共有の `BlockNotOpen`（D-26 を覆し D-32）・編集を器・描画値の Model・コマンドに分け、タブを Presenter の局所状態へ（D-12 を覆し D-33）・PF の本文の有無を `SectionContent<T>` へ（D-19 を覆し D-34）・PF の本文の 2 回組み直し（D-35）・公開待ちの段階と時間切れの順（D-36）・本文のスクロール器の高さの持ち主（D-37）・`LocalizationKey` を使うコードの `using`・`TrainUnitInstanceId` の固定 Guid と grep のパス・`UiIcon.Label`・`UiSystem.Web`・水の Guid の前例・依存に C4-a・`Train(error)` の戻り型の照合・録画の Error 0 件の数え方・`silent-ok` の漏れ 3 か所・A-159 の取り下げ
- Phase 2.6 r4（差分検査）: 実施済み / 強 0・弱 2・修正 2 / 弱: 「合図だけを足す」が取得ラッチの分割を含まない書き方（7-2 → Global Constraints・やらないこと・対象外の節を `TrainTimetableFetchLatch` への分割込みの書き方に直した）・Task 3 の Interfaces に `TrainTimetableFetchLatch` のシグネチャが無い（5-I-1 → 立てる・倒す・読む口を Produces に足した）。あわせて File Structure の「switch に 1 行ずつ」を「2 行ずつ」に揃えた

## 写すテスト

実物（`W/`）で数え直した S4-train の持ち分は 20 件（本体の言う 21 件との差は起草メモ）。`<web-test>` の名前は Web の題そのまま（describe は付けない・it.each は展開しない）。

| Web のファイル | 件 | UITK の写し先（`S/Client.Tests/Ui/`） |
|---|---|---|
| `e2e/tests/train.spec.ts` | 1:「貨車スロットとコンテナ不在エラーを表示する」（「乗車HUD…」は S2-hud の `TrainRidingHudViewTest` で済み） | `Screens/BlockInventory/Train/TrainInventoryFlowTest` |
| `e2e/tests/train/timetable.spec.ts` | 2:「駅の追加・上下移動・削除を即時反映し、再読込でも保持する」「拒否された追加を巻き戻し、次の追加で復帰する」 | `Screens/BlockInventory/Train/TrainTimetableFlowTest` |
| `src/features/blockInventory/train/TrainTimetableImmediate.test.ts` | 8: 1〜6 件目（immediately sends each add, move and remove without an Apply button／waits for publication after acceptance so the next edit cannot use stale stops／waits for acceptance even when the publication arrives first／reverts rejected edits to the newest authority and allows another edit／releases a missing publication after the existing transport timeout／does not defer an edit or refresh a different train after closing） | `State/Train/TimetableEdit/TrainTimetableEditSessionTest` |
| 〃 | 7〜8 件目（blocks auto-run until an added stop is authoritative and sends toggles immediately／disables moves past either end and follows remote changes when idle） | `Screens/BlockInventory/Train/Timetable/TrainTimetableSectionViewTest` |
| `src/features/blockInventory/train/TrainTimetableSection.test.ts` | 5: 1〜4 件目（asks C# to fetch only when the timetable tab is selected／asks C# to fetch on tab select even when the timetable is already ready／re-asks C# when a reset fetch arrives as loading while the timetable tab stays selected／shows loading and unavailable as distinct states without warning） | `Screens/BlockInventory/Train/TrainInventoryBodyTest` |
| 〃 | 5 件目（sends station names only through Set and hides the field for platforms） | `Screens/BlockInventory/Train/TrainPlatformBodyTest` |
| `src/features/blockInventory/train/TrainInventoryBody.test.tsx` | 1:「液体貨車は名前と量容量を表示し、再取得した量へ更新する」 | `Screens/BlockInventory/Train/TrainInventoryBodyTest` |
| `e2e/tests/block/trainPlatform.spec.ts` | 2:「PFスロットを表示し、積込から卸しへ目標モードActionを送る」「液体PFは液体名・アイコン・量容量と卸しモードを表示する」 | `Screens/BlockInventory/Train/TrainPlatformBodyTest` |
| `src/features/blockInventory/views/__tests__/TrainPlatformInventory.test.tsx` | 1:「PF自身の充填スロット・常時テキストを更新し、空でも実スロットを残す」 | `Screens/BlockInventory/Train/TrainPlatformBodyTest` |
| `src/features/blockInventory/blockInventoryDesign.test.ts`（alpha が train に割り振った共有） | 「ゲージとモード切替を用途別に共通部品へ置換する」（列車のモード切替の分）・「%sから禁止Mantine部品とテーマ色を除去する」（列車の USS の分） | `Screens/BlockInventory/Train/TrainDesignTest`（alpha の `BlockInventoryDesignTest` には足さず、同じタグで自分のファイルに写す） |

## File Structure

すべて `moorestech_client/Assets/Scripts/` の下（`S/`）。新しいディレクトリはどれも 10 ファイル以下・各ファイル 200 行以下。

**State**（`S/Client.Ui.State/Train/`。既存 5 本＋`Logic/` に、1 本と 6 ディレクトリ〈`TimetableEdit/Send/` を含む〉を足す）

| ファイル | 責務 |
|---|---|
| `Train/TrainCommandRejection.cs` | 列車系コマンドの拒否を理由ごとの `UiCommandRejectReason`（Detail はサーバーの失敗理由だけ）で作り、Web ホストと同じ書式で Warning ログ（D-31） |
| `Train/Timetable/TrainTimetableViewState.cs` | Loading / Unavailable / Ready の型と「同じ状態か」の判定 |
| `Train/Timetable/TrainTimetableReadyData.cs` | 用意済みの時刻表（列車・自動運転・現在位置・停車駅・駅一覧）と中身の等しさ |
| `Train/Timetable/TrainTimetableStationCatalog.cs` | 駅一覧を x→z→y で整列（Web ホスト `TrainTimetableDtoBuilder.SortStations` と同じ規則。D-2） |
| `Train/Timetable/TrainTimetableReadyDataFactory.cs` | スナップショット＋駅一覧 → Ready（名前の補完・欠けの警告・未知の Side） |
| `Train/Timetable/ITrainTimetableSource.cs` | 時刻表の状態の口（`Current`。D-20） |
| `Train/Timetable/TrainTimetableModel.cs` | 本番の源。状態の優先を組み、変化の契機を購読。駅ブロックから駅一覧を読む |
| `Train/TimetableEdit/Send/ITrainTimetableCommands.cs` | サーバーへ送る口: 取得の依頼・置換・自動運転 |
| `Train/TimetableEdit/Send/TrainTimetableCommands.cs` | 本番。`TrainTimetableFetcher`・`OpenTrainUnitResolver` を呼び、`TrainScheduleEditRequest` を送る |
| `Train/TimetableEdit/Send/TrainTimetableDeparture.cs` | 送信形 → サーバーの停車駅（固定の出発条件） |
| `Train/TimetableEdit/ITrainTimetableWaitTimer.cs` | 公開待ちの時間切れの口 |
| `Train/TimetableEdit/UniRxTrainTimetableWaitTimer.cs` | 本番。`Observable.Timer` |
| `Train/TimetableEdit/TrainTimetableEditSession.cs` | 即時編集（`useImmediateTimetable` の写し。段階は private な判別 union） |
| `Train/TimetableEdit/TrainTimetableSectionViewModel.cs` | 区画の描画値（現在位置 `int?`・自動運転の可否・中身の等しさ） |
| `Train/TimetableEdit/TrainTimetableAreaViewModel.cs` | 時刻表タブの中身（Loading / Unavailable / 列車ごとの Ready） |
| `Train/TimetableEdit/TrainTimetableEditState.cs` | 編集の器。時刻表の状態を購読し、列車ごとのセッションを持つ（Model とコマンドが共有。D-33） |
| `Train/TimetableEdit/ITrainTimetableEditModel.cs` | 時刻表タブの描画値（`Current` だけ。Presenter が購読） |
| `Train/TimetableEdit/TrainTimetableEditModel.cs` | 本番。器の変化を購読して描画値を組む |
| `Train/TimetableEdit/ITrainTimetableEditCommands.cs` | 追加・移動・削除・自動運転（View が直接呼ぶ。A-8。`UiCommandResult` を返す） |
| `Train/TimetableEdit/TrainTimetableEditCommands.cs` | 本番。開いているセッションへ渡すか自動運転を送り、開いていなければ `BlockNotOpen` をトースト |
| `Train/Inventory/TrainInventoryTab.cs` | タブの列挙 |
| `Train/Inventory/TrainTimetableFetchTrigger.cs` | タブの局所状態と、タブ選択・Loading の到着 → 取得の依頼の判定（列車の本文の Presenter が持つ。D-33） |
| `Train/Inventory/TrainInventoryBodyViewModel.cs` | 貨車の中身（スロット・液体） |
| `Train/Inventory/ITrainInventoryBodySource.cs` | 貨車の中身の口 |
| `Train/Inventory/TrainInventoryBodyModel.cs` | 本番。スロットは alpha のパネルの源、液体は `SubInventoryState` |
| `Train/Inventory/TrainCarFluidConversion.cs` | 貨車の `FluidMessagePack` → `BlockFluidSlotState`（S4-machine の `BlockFluidSlotConversion.FromTanks` を入力側だけで呼ぶ） |
| `Train/Platform/TrainPlatformTransferMode.cs` | State 側の積込/卸し（View が `Game.Block` を参照しないため） |
| `Train/Platform/TrainPlatformTransferModeConversion.cs` | サーバーの移送モードとの写し（読み手と送り手が共有） |
| `Train/Platform/TrainStationNamePart.cs` | 駅だけが持つ部分（同期した駅名） |
| `Train/Platform/TrainPlatformBodyState.cs` | 閉 / 開（部分ごとの有無は `SectionContent<T>`。D-34）の union |
| `Train/Platform/ITrainPlatformBodySource.cs` | プラットフォームの中身の口 |
| `Train/Platform/TrainPlatformBodyModel.cs` | 本番。パネルの源と S4-machine の `IBlockFluidSlotsModel` を購読し、追跡ブロックを読んで 1 回だけ組む（D-35） |
| `Train/Platform/TrainPlatformDetailReader.cs` | StateDetail・BlockParam → 描画値の純関数（Web ホスト `TrainStationDetailDtoBuilder`・`TrainPlatformDetailDtoBuilder` と同じ規則。D-2） |
| `Train/Platform/Edit/ITrainBlockCommands.cs` | 駅名・モードの送信 |
| `Train/Platform/Edit/TrainBlockCommands.cs` | 本番 |
| `Train/Platform/Edit/TrainStationNameDraft.cs` | 駅名の下書き（同期・編集・拒否の巻き戻し） |
| `Train/Platform/Edit/TrainStationNameEditState.cs` | 駅名の下書きの器。パネルごとに下書きを作り直し、拒否は送った回の下書きが今のものなときだけ戻す（Model とコマンドが共有。D-33） |
| `Train/Platform/Edit/ITrainPlatformEditModel.cs` | 駅名の入力欄の描画値（Presenter が購読） |
| `Train/Platform/Edit/TrainPlatformEditModel.cs` | 本番。器の入力を配る |
| `Train/Platform/Edit/ITrainPlatformEditCommands.cs` | 駅名の入力・決定・モードの選択（View が直接呼ぶ。A-8） |
| `Train/Platform/Edit/TrainPlatformEditCommands.cs` | 本番。送ってトーストし、駅名の拒否は器へ戻させる |

共有ファイルへの追記（E-1）: `Commands/UiCommandRejectReason.cs`（列挙と switch 3 本に 7 行ずつ〈S4-machine と共有の `BlockNotOpen`〈先にマージしたレーンが足し、後のレーンは使うだけ〉と列車の 6 値。D-31・D-32〉、末尾に追記）、`Client.Ui.State.asmdef`（無い参照だけ。S4-machine の後なら `Core.Update` だけの見込み）。

**View**（`S/Client.Ui/Screens/BlockInventory/Train/`）

| ファイル | 責務 |
|---|---|
| `Train/TrainInventoryBodyFactory.cs` | `ITrainInventoryBodyFactory` の実装 |
| `Train/TrainInventoryBodyPresenter.cs` | タブの局所状態（`TrainTimetableFetchTrigger`。別のパネルで作り直す）を持ち、表示中のタブの Model（中身か時刻表）だけを購読して View へ流す（D-33） |
| `Train/TrainInventoryBodyView.cs` | タブ＋スクロール器＋中身＋時刻表の器。タブの押下は `OnTabSelected` で Presenter へ |
| `Train/TrainInventoryContentsView.cs` | スロットか液体か（`train-inventory-contents`） |
| `Train/TrainPlatformBodyFactory.cs` | `IBlockInventoryBodyFactory`（Kind `TrainPlatform`） |
| `Train/TrainPlatformBodyPresenter.cs` | 本文の状態と駅名の入力を購読して View へ流す |
| `Train/TrainPlatformBodyView.cs` | 駅名の行・スロット・液体・モード（あれば出す） |
| `Train/Train.uss` | 本文の間隔・駅名の行 |
| `Train/Timetable/TrainTimetableAreaView.cs` | Loading / Unavailable / 区画の出し分け（列車ごとに区画を作り直す） |
| `Train/Timetable/TrainTimetableSectionView.cs` | 自動運転の行と 2 つの枠。押下は `ITrainTimetableEditCommands` を直接呼ぶ |
| `Train/Timetable/TrainTimetableStopListView.cs` | 停車駅の列 |
| `Train/Timetable/TrainTimetableStationListView.cs` | 駅一覧の列 |
| `Train/Timetable/TrainTimetableHeading.cs` | 枠の見出し（器に余白、ラベルに文字の型） |
| `Train/Timetable/Timetable.uss` | `style.module.css` の時刻表の部分の写し |
| `Train/Parts/TrainStationNameRow.cs` | 駅名の入力と決定 |
| `Train/Parts/TrainPlatformModeSection.cs` | 積込/卸しの切替 |

**登録**: `S/Client.Starter/Registration/GameUi/Screens/BlockInventoryTrainGameUiRegistration.cs`（E-1。ディレクトリが無ければ作る）、`MainGameUiRegistration.cs` の画面登録区画に 1 行、`moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss` の `/* == section: Screens == */` に 2 行。

**テスト**（`S/Client.Tests/Ui/`）

| ファイル | 中身 |
|---|---|
| `State/Train/Timetable/TrainTimetableStationCatalogTest.cs` | 整列 |
| `State/Train/Timetable/TrainTimetableReadyDataFactoryTest.cs` | 名前の補完・警告の重複抑止・未知の Side・同じ中身を流し直さない |
| `State/Train/TimetableEdit/TrainTimetableEditSessionTest.cs` | `TrainTimetableImmediate.test.ts` 1〜6 |
| `State/Train/TimetableEdit/TrainTimetableDepartureTest.cs` | 出発条件 |
| `State/Train/TimetableEdit/TrainTimetableEditModelTest.cs` | 送った後に閉じる・列車替わりでセッションを作り直す・時刻表が開いていない操作は拒否して送らない |
| `State/Train/Inventory/TrainTimetableFetchTriggerTest.cs` | 依頼の判定 |
| `State/Train/Platform/TrainStationNameDraftTest.cs` | 下書き |
| `State/Train/Platform/TrainPlatformDetailReaderTest.cs` | 駅・品物 PF・液体 PF・状態未着の 4 行 |
| `State/Train/Platform/TrainPlatformEditModelTest.cs` | 閉じて同じ駅を開き直した後の拒否は新しい入力を消さない |
| `State/Train/Fakes/FakeTrainTimetableCommands.cs` | 完了を手で返す偽コマンド |
| `State/Train/Fakes/FakeTrainBlockCommands.cs` | 〃 |
| `State/Train/Fakes/ManualTrainTimetableWaitTimer.cs` | 手で発火する時間切れ |
| `State/Train/Fakes/FakeTrainSources.cs` | 3 つの源の偽物 |
| `State/Train/Fakes/TrainTestData.cs` | 駅・停車駅（`TimetableEditLogic` で組む）・Ready の組み立て（列車は固定 Guid） |
| `State/Train/Fakes/TrainTestPlatforms.cs` | 駅・品物 PF・液体 PF の本文の描画値（`SectionContent<T>`）の組み立て |
| `Screens/BlockInventory/Train/TrainInventoryBodyTest.cs` | タブと取得 4 件＋液体貨車 1 件＋非表示のタブを描かない 1 件（D-27）＋タブの局所状態 2 件（使えないの間の依頼・同じ車両で保ち別のパネルで戻す。D-33） |
| `Screens/BlockInventory/Train/TrainInventoryFlowTest.cs` | alpha の外殻ごと（`train.spec`） |
| `Screens/BlockInventory/Train/TrainTimetableFlowTest.cs` | `timetable.spec` 2 件 |
| `Screens/BlockInventory/Train/TrainPlatformBodyTest.cs` | `trainPlatform.spec` 2・PF 1・駅名 1 |
| `Screens/BlockInventory/Train/TrainDesignTest.cs` | 共有 2 件（USS の色・共通部品だけ） |
| `Screens/BlockInventory/Train/TrainTestBodies.cs` | 偽の源・偽の送信の上に本物の編集の器・Model・コマンドを組んで工場を作る |
| `Screens/BlockInventory/Train/TrainTestText.cs` | 木の文字・液体名・液体のアイコンの有無を読む補助 |
| `Screens/BlockInventory/Train/Timetable/TrainTimetableSectionViewTest.cs` | `TrainTimetableImmediate.test.ts` 7〜8 |

**録画**: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-train-inventory.cs`（新設）、`scenarios/train/train-timetable-via-ui.cs`（両系で通る形に直す）。

## Tasks

### Task 1: 前提の確認と記録の準備

**Files:**
- 記録: `LOGS/harness/uitk/S4-train/done.md`（新規。1 行目「状態: 作業中」）

- [ ] **Step 1: 依存が入っているか確かめる**

```bash
pwd
git fetch origin
git log origin/feature/uitk --oneline | grep -iE "s4-alpha|c4-a|s4-machine"
ls moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Registry moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Fluid moorestech_client/Assets/Scripts/Client.Ui/Components/Slots/Fluid
grep -rn 'AddToClassList("fluid-slot__icon")\|game-icon--image' moorestech_client/Assets/Scripts/Client.Ui/Components/Slots
```
Expected: alpha・C4-a・S4-machine のマージコミットがあり、`Registry/` に `ITrainInventoryBodyFactory`、`Components/Slots/Fluid/` に `FluidSlot`・`FluidSlotRow`・`FluidAmountSlot`、`Fluid/` に `FluidInventoryContents` がある。液体の枠のアイコンの要素のクラス（C4-a 計画では `fluid-slot__icon`）と絵が出ている印（`game-icon--image`）が grep で見つかる。違えば Task 7 の `TrainTestText.HasFluidIcon` の綴りを実物に合わせ `done.md`「名前の合わせ」に書く。無ければ `cmux wait-for uitk-s4-alpha` / `cmux wait-for uitk-c4-a` / `cmux wait-for uitk-s4-machine` を待つ（このレーンの実装はその後）

- [ ] **Step 2: 計画から写した名前を実物に合わせる**

Global Constraints のコードブロックの alpha・S4-machine の型（`ITrainInventoryPart`・`IBlockInventoryPart`・`BlockInventoryBodyMount`・`OpenTrainInventoryPanel`・`FluidInventoryContents(testid, content)`・`IBlockFluidSlotsModel`・`BlockFluidSlotConversion.FromTanks`）と、テストの土台 `BlockInventoryTestPanels.Train(error)` の戻り型（本計画の L1 は `ITrainInventoryPart.Attach(OpenTrainInventoryPanel)` へ無キャストで渡す。alpha 計画は戻り型を明示しておらず、同系の `FromTrain` は `BlockInventoryPanelState` を返す。基底型なら L1 の呼び所に `(OpenTrainInventoryPanel)` を足す。`OfType` の呼び所は既にキャストしている）を実物と突き合わせ、違うものを表にして `done.md`「名前の合わせ」に書く。あわせて、液体 PF を追跡しているあいだ `IBlockFluidSlotsModel.Current.Value.Slots` が空でないことを PlayMode の `uloop execute-dynamic-code` で確かめる（D-17。空なら S4-machine の持ち主へ上げ、液体 PF の行が空になることを `done.md`「未確認」に書く）。同じく、駅の追跡ブロックの状態変化（積込/卸しの切り替え）と追跡替えのたびに `IBlockFluidSlotsModel.Current` が参照の違う値で配り直されることを、購読の回数を数えて確かめる（D-35。`BlockFluidSlotsModel` の `Rebuild` が毎回 `new BlockFluidSlotsViewModel` するかを読むだけでもよい。同じ値を畳んで流さない作りなら Task 5 の `TrainPlatformBodyModel` を注のとおり追跡の通知の購読へ戻す）。以降のタスクのコードはその表で綴りだけ置き換える。形が違う（工場が Presenter を返さない・液体の Model が列車で空でない等）なら、置き換えの方針を D-n として判断記録に足す

- [ ] **Step 3: 実物の綴りを確かめる**

```bash
cd moorestech_client/Assets/Scripts
grep -rn "public TrainTimetableSnapshot(" ../../../moorestech_server/Assets/Scripts/Game.Train | head
grep -n "UnitOf\|struct TrainUnitInstanceId" ../../../moorestech_server/Assets/Scripts/Game.Block.Interface/Train/TrainUnitInstanceId.cs
grep -rn "EmptyFluidId" ../../../moorestech_server/Assets/Scripts/Core.Master | head -2
ls Client.Starter/Registration/GameUi Client.Starter/Registration/GameUi/Screens
```
Expected: `TrainTimetableSnapshot` のコンストラクタ（テストで組めるか）、`TrainUnitInstanceId` が `[UnitOf(typeof(Guid), …)] readonly partial struct`（ctor は Guid 1 引数。`TrainTestData` は固定 Guid で組む）、`FluidId` の作り方と空の液体の比べ方、`GameUi/Screens/` の有無（無ければ Task 8 で作る）。下のコードと違うところは綴りだけ合わせ、`done.md`「名前の合わせ」に足す

- [ ] **Step 4: done.md を作る**

`mkdir -p ../moorestech_logs/harness/uitk/S4-train` の上で `done.md` を次の骨組みで作る（`LOGS` は別 repo。このレーンのブランチではコミットしない）:

```markdown
状態: 作業中
# S4-train
## 名前の合わせ
## 証拠
## 差の表
## 要裁定
## integrator への依頼
- PanelActionButton に無効時の見た目が無い。列車の時刻表は画面のルートで `--panel-action-button-text` を `--text-muted` に上書きして Web に合わせた（D-11）。部品側に無効の状態を持たせるなら置き換える
- 共有の新部品は作っていない（`TrainStationNameRow`・`TrainPlatformModeSection` は画面だけの部品）
## 未確認
```

---

### Task 2: 断りの理由・文言・asmdef

**Files:**
- Modify: `S/Client.Ui.State/Commands/UiCommandRejectReason.cs`
- Modify: `S/Client.Ui.State/Client.Ui.State.asmdef`
- Modify: `Localization/localization.csv`（`ui.commandRejected.*` の末尾に 6 行。共有の `blockNotOpen` が無ければ 7 行）
- 変えない（回すだけ）: `S/Client.Tests/Ui/State/PauseMenuCommandsTest.cs`。足す理由は全部非良性なので、良性の配列にもテスト名（今の `良性の拒否理由は言語の選択済みと持ち物の移動と分割の取りこぼしだけ`。`PauseMenuCommandsTest.cs:76`）にも触らない。全列挙を回す 2 件〈良性の配列との一致・翻訳文〉が新しい値も見る（D-32。alpha D-56 は改名を取り下げたので、名前は今の名前のまま）
- 再生成: `pnpm --dir moorestech_web/webui run gen:i18n` の生成物
- Modify: `docs/uitk/TODO.md`「追従記録」に 1 行
- Create: `S/Client.Ui.State/Train/TrainCommandRejection.cs`

**Interfaces:**
- Produces: `UiCommandRejectReason.BlockNotOpen`（S4-machine と共有。先にマージしたレーンが足し、後のレーンは使うだけ）・`TrainTimetableReplaceFailed`・`TrainAutoRunFailed`・`TrainAutoRunNotApplied`・`TrainInvalidBlockType`・`TrainStationNameFailed`・`TrainPlatformModeFailed`（7 つとも非良性）、`TrainCommandRejection.Reject(string logTag, UiCommandRejectReason reason, string serverFailureReason)`

| 値 | 起きる所（Web ホストの理由文字列） | wire（`ToWireCode`） | 文言キー |
|---|---|---|---|
| `BlockNotOpen`（共有） | 時刻表の `train_not_open`・駅/PF の `block_not_open`・時刻表が開いていない編集（S4-machine のレシピ選択も同じ） | `block_not_open` | `ui.commandRejected.blockNotOpen` |
| `TrainTimetableReplaceFailed` | `replace_failed:<理由>` | `replace_failed` | `ui.commandRejected.trainTimetableReplaceFailed` |
| `TrainAutoRunFailed` | `set_auto_run_failed:<理由>` | `set_auto_run_failed` | `ui.commandRejected.trainAutoRunFailed` |
| `TrainAutoRunNotApplied` | `set_auto_run_not_applied:<値>` | `set_auto_run_not_applied` | `ui.commandRejected.trainAutoRunNotApplied` |
| `TrainInvalidBlockType` | 駅/PF の `invalid_block_type` | `invalid_block_type` | `ui.commandRejected.trainInvalidBlockType` |
| `TrainStationNameFailed` | `set_name_failed:<理由>` | `set_name_failed` | `ui.commandRejected.trainStationNameFailed` |
| `TrainPlatformModeFailed` | `mode_request_failed` | `mode_request_failed` | `ui.commandRejected.trainPlatformModeFailed` |

`Detail` は既定 `""` で、サーバーの `FailureReason` を載せるときだけ入れる（前例 `PauseMenuCommands.SubmitBugReport` が submitter の失敗コードを Detail に入れるのと同じ使い方。`ToWireCode()` は Detail があればそれを返す）。時刻表の `train_not_open` は wire が `block_not_open` になる（Web ホストの綴りと違うのはログの文字列だけで、Web も UITK もトーストする。D-32）

- [ ] **Step 1: 列挙と switch に足す**

`grep -n "BlockNotOpen" moorestech_client/Assets/Scripts/Client.Ui.State/Commands/UiCommandRejectReason.cs` で共有の `BlockNotOpen` の有無を確かめる（S4-machine が先にマージしていれば有る。有れば使うだけで足さない。無ければこのレーンが足し、S4-machine は使うだけ）。`UiCommandRejectReason` の列挙の末尾に、無ければ `BlockNotOpen,`、続けて `TrainTimetableReplaceFailed,`・`TrainAutoRunFailed,`・`TrainAutoRunNotApplied,`・`TrainInvalidBlockType,`・`TrainStationNameFailed,`・`TrainPlatformModeFailed,`（E-1: 列挙と switch はレーンが末尾に追記し、全列挙を見る `PauseMenuCommandsTest` を同じ PR で緑にする）。3 本の switch の末尾に同じ順で:
- `IsBenign`: 7 つとも `=> false,`（Web は `dispatchAction` で拒否をトーストし、`moorestech_web/webui/src/bridge/transport/actions.ts` の `BENIGN_ERRORS` に `train_timetable.*`・`train_station.set_name`・`train_platform.set_transfer_mode` の項目は無い。`IsBenign` のコメントの契約〈Web の BENIGN_ERRORS にあるものだけ良性〉と alpha D-56 に揃える。D-32）
- `ToLocalizationKey`: `UiCommandRejectReason.BlockNotOpen => LocalizationKeys.Ui.CommandRejected.BlockNotOpen,` ほか表の文言キー
- `ToWireCode`: 表の wire（`"block_not_open"`・`"replace_failed"`・`"set_auto_run_failed"`・`"set_auto_run_not_applied"`・`"invalid_block_type"`・`"set_name_failed"`・`"mode_request_failed"`）

`PauseMenuCommandsTest` は変えない（良性の値を足さないので、良性の配列と全列挙の一致はそのまま緑。取り込みで S4-machine が良性の値を足していたら、その PR の配列と名前に従う）

- [ ] **Step 2: 文言を足して再生成する**

`Localization/localization.csv` の `ui.commandRejected.*` の最後の行の下に（共有の `blockNotOpen` が既にあれば 1 行目は足さない。1 行目は S4-machine 計画 Task の csv と一字一句同じにしてあり、取り込みで両側にあれば 1 行に畳む。D-32）:

```csv
ui.commandRejected.blockNotOpen,The block is not open,The block is not open,ブロックが開いていません,Der Block ist nicht geöffnet,블록이 열려 있지 않습니다
ui.commandRejected.trainTimetableReplaceFailed,The timetable change was rejected,The timetable change was rejected,時刻表の変更が受け付けられませんでした,Die Fahrplanänderung wurde abgelehnt,시간표 변경이 거부되었습니다
ui.commandRejected.trainAutoRunFailed,Could not switch auto-run,Could not switch auto-run,自動運転を切り替えられませんでした,Automatikbetrieb konnte nicht umgeschaltet werden,자동 운전을 전환하지 못했습니다
ui.commandRejected.trainAutoRunNotApplied,Auto-run was not changed,Auto-run was not changed,自動運転は切り替わりませんでした,Der Automatikbetrieb wurde nicht geändert,자동 운전이 바뀌지 않았습니다
ui.commandRejected.trainInvalidBlockType,This block cannot take this setting,This block cannot take this setting,このブロックにはこの設定ができません,Dieser Block unterstützt diese Einstellung nicht,이 블록에는 이 설정을 할 수 없습니다
ui.commandRejected.trainStationNameFailed,The station name was rejected,The station name was rejected,駅名が受け付けられませんでした,Der Bahnhofsname wurde abgelehnt,역 이름이 거부되었습니다
ui.commandRejected.trainPlatformModeFailed,Could not switch loading and unloading,Could not switch loading and unloading,積込と卸しを切り替えられませんでした,Be- und Entladen konnte nicht umgeschaltet werden,적재와 하역을 전환하지 못했습니다
```

```bash
pnpm --dir moorestech_web/webui run gen:i18n
```
Expected: `LocalizationKeys.Ui.CommandRejected.BlockNotOpen`・`TrainTimetableReplaceFailed` ほか 7 つが引ける。Unity が csv の変化を拾わなければ前例どおり `_CompileRequester.cs` を触ってから compile。`docs/uitk/TODO.md`「追従記録」に `| 4 | <このコミット> | 写した | localization.csv ui.commandRejected.blockNotOpen・trainTimetableReplaceFailed・trainAutoRunFailed・trainAutoRunNotApplied・trainInvalidBlockType・trainStationNameFailed・trainPlatformModeFailed（S4-train。UITK の拒否理由用。Web は使わない。blockNotOpen は S4-machine と共有） |`

- [ ] **Step 3: asmdef に参照を足す**

`grep -n '"Game.Block"\|"Core.Update"' moorestech_client/Assets/Scripts/Client.Ui.State/Client.Ui.State.asmdef` で確かめ、無いものだけを `references` の末尾に足す（S4-machine の後なら `Core.Update` だけの見込み。書式は同ファイルの既存の `Game.Train` 等の行に合わせる。GUID 参照なら各 asmdef の `.meta` の guid を読んで書く）。取り込みで同じ参照が衝突したら 1 行に畳む（E-1）

- [ ] **Step 4: 拒否の組み立て**

```csharp
using Client.Ui.State.Commands;
using UnityEngine;

namespace Client.Ui.State.Train
{
    /// <summary>
    /// 列車系コマンドの拒否を理由ごとの値で作り、Web ホストと同じ書式で開発者ログへ出す
    /// Builds train command refusals with per-reason values and logs them for developers in the Web host's format
    /// </summary>
    public static class TrainCommandRejection
    {
        public static UiCommandRejected Reject(string logTag, UiCommandRejectReason reason, string serverFailureReason)
        {
            // Detail はサーバーの失敗理由だけ。応答が無い（切断・時間切れ）・理由が空なら空文字（前例 PauseMenuCommands.SubmitBugReport）
            // Detail carries only the server failure reason; empty when there is no response or no reason (precedent PauseMenuCommands.SubmitBugReport)
            var detail = serverFailureReason ?? string.Empty;
            var wire = UiCommandRejectReasonTraits.ToWireCode(reason);

            // Web ホストの TrainTimetableActionSupport.Reject と同じ「理由:失敗理由」の書式
            // Same "reason:failure" format as the Web host's TrainTimetableActionSupport.Reject
            Debug.LogWarning(detail.Length == 0 ? $"[{logTag}] rejected: {wire}" : $"[{logTag}] rejected: {wire}:{detail}");
            return new UiCommandRejected(reason, detail);
        }
    }
}
```
（`serverFailureReason` の null はネットワークの応答〈外部データ〉が無いときだけ。呼び手はサーバーの理由が無い拒否に `string.Empty` を渡す）

- [ ] **Step 5: compile とコミット**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.Ui.State.PauseMenuCommandsTest"`
Expected: `ErrorCount: 0`、`PauseMenuCommandsTest` が全件 PASS（良性の配列と全列挙の一致・翻訳文）

```bash
git add Localization/localization.csv moorestech_web/webui moorestech_client/Assets/Scripts/Client.Ui.State docs/uitk/TODO.md
git commit -m "feat(uitk): 列車の操作の拒否理由を理由ごとに足し、文言・asmdef 参照を足す（S4-train）

<実行ホストの指示する帰属行>"
```

---

### Task 3: 時刻表の状態（`Client.Ui.State/Train/Timetable/`）

**Files:**
- Create: `S/Client.Ui.State/Train/Timetable/` の 6 本
- Create: `S/Client.Tests/Ui/State/Train/Timetable/TrainTimetableStationCatalogTest.cs`・`TrainTimetableReadyDataFactoryTest.cs`、`S/Client.Tests/Ui/State/Train/Fakes/TrainTestData.cs`
- Modify（E-1: マージ済みの State の口への追記）: `S/Client.Ui.State/Train/TrainTimetableFetcher.cs`（取得の開始の合図。D-20）
- Create: `S/Client.Ui.State/Train/TrainTimetableFetchLatch.cs`（Fetcher から出す取得ラッチ。振る舞いは変えない）
- Modify: `S/Client.Tests/EditModeInPlayingTest/Hostless/TimetableStateProbe.cs`

**Interfaces:**
- Consumes: `TrainTimetableFetcher`・`OpenTrainTimetableTracker`・`OpenTrainUnitResolver`（既存。Web ホストも使っている State の口をそのまま使う）、`TrainTimetableStation`・`TimetableEditStop`・`TimetableEditLogic`（L0-s4b）
- Produces:
```csharp
namespace Client.Ui.State.Train.Timetable
public abstract class TrainTimetableViewState { static Loading; static Unavailable; static Ready(TrainTimetableReadyData); static bool IsSameState(TrainTimetableViewState current, TrainTimetableViewState next); }
public sealed class ReadyTrainTimetableViewState : TrainTimetableViewState { public TrainTimetableReadyData Data { get; } }
public sealed class TrainTimetableReadyData { TrainUnitInstanceId; IsAutoRun; CurrentIndex; Stops; Stations; bool HasSameContent(TrainTimetableReadyData other); }
public interface ITrainTimetableSource { IReadOnlyReactiveProperty<TrainTimetableViewState> Current { get; } }
public sealed class TrainTimetableModel : ITrainTimetableSource, IInitializable   // ctor に UIStateControl（閉じたことを Web ホストと同じ契機で拾う）
// 既存への追記（E-1: マージ済みの State の口へ合図を足す）: TrainTimetableFetcher.OnFetchStarted : IObservable<UniRx.Unit>（取得中を立てた直後に流す）。購読は TrainTimetableModel だけ。既存の OnFetchFailed と Tracker は変えない
namespace Client.Ui.State.Train
internal sealed class TrainTimetableFetchLatch   // Fetcher 内部の取得ラッチ（新設。Fetcher だけが持つ。振る舞いは分割前と同じ）
{
    public TrainUnitInstanceId? RequestedTrainUnitInstanceId { get; private set; }
    public bool RetriedForRequestedTrain { get; private set; }
    public bool IsSameRequest(TrainUnitInstanceId trainUnitInstanceId);            // 同じ列車の重複要求を畳む判定
    public void StartRequest(TrainUnitInstanceId trainUnitInstanceId);            // 立てる: 要求中の列車・取得中、使えない・再試行済みを倒す
    public void MarkRetried();                                                    // 再試行を使った
    public void ReleaseInFlight(TrainUnitInstanceId deliveredTrainUnitInstanceId); // 倒す: 届いた列車が要求中なら取得中を倒す
    public void ReleaseUnavailable(TrainUnitInstanceId deliveredTrainUnitInstanceId);
    public void MarkUnavailable(TrainUnitInstanceId trainUnitInstanceId);          // 倒す: 取得中を倒し、使えないを立てる
    public void Clear();                                                          // Close で全部倒す
    public bool IsUnavailable(TrainUnitInstanceId trainUnitInstanceId);           // 読む（Fetcher の同名の口が素通しで呼ぶ）
    public bool IsFetchInFlight(TrainUnitInstanceId trainUnitInstanceId);
    public bool HasFetchInFlight { get; }                                         // 打ち切りのログと届いた時の判定用
}
```

- [ ] **Step 1: 失敗するテストを書く**

`TrainTestData.cs`:

```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.Timetable;
using Game.Train.Unit;
using UnityEngine;

namespace Client.Tests.Ui.State.Train.Fakes
{
    /// <summary>
    /// Web のモック trainWithTimetable と同じ駅を持ち、停車駅は L0-s4b の編集ロジックで組む（Side を手で書かない）
    /// Holds the same stations as the Web mock trainWithTimetable and builds stops with the L0-s4b edit logic (never hand-written sides)
    /// </summary>
    public static class TrainTestData
    {
        // TrainUnitInstanceId は Guid の UnitOf 型。テストでは固定の Guid で組む
        // TrainUnitInstanceId is a Guid UnitOf type; tests build it from fixed Guids
        public static readonly TrainUnitInstanceId Unit1 = new TrainUnitInstanceId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        public static readonly TrainUnitInstanceId Unit2 = new TrainUnitInstanceId(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        public static readonly TrainTimetableStation Unnamed = new(new Vector3Int(-8, 0, 3), string.Empty);
        public static readonly TrainTimetableStation North = new(new Vector3Int(12, 0, 40), "北駅");
        public static readonly IReadOnlyList<TimetableEditStop> NoStops = new List<TimetableEditStop>();

        // 追加した駅の並び。Side は AddStop が決める（Back 固定）
        // Order after adding a station; AddStop decides the side (always Back)
        public static IReadOnlyList<TimetableEditStop> Added(IReadOnlyList<TimetableEditStop> stops, TrainTimetableStation station)
        {
            return TimetableEditLogic.AddStop(stops, station);
        }

        public static TrainTimetableReadyData Ready(TrainUnitInstanceId unit, bool isAutoRun, IReadOnlyList<TimetableEditStop> stops)
        {
            return new TrainTimetableReadyData(unit, isAutoRun, 0, stops, new List<TrainTimetableStation> { Unnamed, North });
        }

        public static TrainTimetableReadyData NorthOnly(TrainUnitInstanceId unit)
        {
            return Ready(unit, false, Added(NoStops, North));
        }
    }
}
```
（`TrainUnitInstanceId` は `moorestech_server/Assets/Scripts/Game.Block.Interface/Train/TrainUnitInstanceId.cs` の `[UnitOf(typeof(Guid))] readonly partial struct`〈namespace `Game.Train.Unit`〉で、ctor は Guid 1 引数。前例 `Client.Tests/PlaceSystem/TrainCostIntegration/TrainCarPlacementEntryCostTest.cs:77` の `new TrainUnitInstanceId(Guid…)`。綴りは Task 1 Step 3 で確かめる）

`TrainTimetableStationCatalogTest.cs`:

```csharp
using System.Collections.Generic;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.Timetable;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.State.Train.Timetable
{
    public class TrainTimetableStationCatalogTest
    {
        [Test]
        public void 駅はxzyの順で並ぶ()
        {
            // Web ホストの SortStations と同じ優先順
            // Same priority as the Web host's SortStations
            var sorted = TrainTimetableStationCatalog.Sorted(new List<TrainTimetableStation>
            {
                new(new Vector3Int(1, 5, 0), "b"),
                new(new Vector3Int(0, 9, 9), "a"),
                new(new Vector3Int(1, 0, 0), "c"),
                new(new Vector3Int(1, 9, -1), "d"),
            });
            CollectionAssert.AreEqual(new[] { "a", "d", "c", "b" }, sorted.ConvertAll(s => s.Name));
        }
    }
}
```

`TrainTimetableReadyDataFactoryTest.cs`:

```csharp
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.Timetable;
using Game.Train.Diagram;
using Game.Train.RailGraph;
using Game.Train.Unit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.Train.Timetable
{
    public class TrainTimetableReadyDataFactoryTest
    {
        private static readonly List<TrainTimetableStation> Stations = new() { TrainTestData.Unnamed, TrainTestData.North };

        [Test]
        public void 停車駅の名前は駅一覧から引き引けない位置は空名で警告は集合が変わったときだけ()
        {
            var factory = new TrainTimetableReadyDataFactory();
            var snapshot = Snapshot(StopAt(TrainTestData.North.Position), StopAt(new Vector3Int(99, 0, 0)));

            // 初回は欠けの集合が変わるので 1 回警告する
            // The first build changes the missing set, so it warns once
            LogAssert.Expect(LogType.Warning, new Regex(@"\[TrainTimetableModel\] station blocks not found.*99"));
            Assert.IsTrue(factory.TryCreate(snapshot, Stations, out var data));
            Assert.AreEqual("北駅", data.Stops[0].Name);
            Assert.AreEqual(string.Empty, data.Stops[1].Name);

            // 同じ集合ではもう警告しない
            // The same set does not warn again
            Assert.IsTrue(factory.TryCreate(snapshot, Stations, out _));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 未知のSideがあれば作らずErrorログ()
        {
            var factory = new TrainTimetableReadyDataFactory();
            var snapshot = Snapshot(new TrainTimetableStop(TrainTestData.North.Position, (StationNodeSide)99, TrainDiagram.DepartureConditionType.WaitForTicks, 20));
            LogAssert.Expect(LogType.Error, new Regex(@"\[TrainTimetableModel\] discarding timetable with unknown side"));
            Assert.IsFalse(factory.TryCreate(snapshot, Stations, out _));
        }

        [Test]
        public void 中身が同じ用意済みは流し直さず読込中と使えないは同じ値で流さない()
        {
            // 貨車の中身の更新だけでは時刻表の行を作り直さない（R-5）
            // A car contents update alone does not rebuild the timetable rows (R-5)
            var first = TrainTimetableViewState.Ready(TrainTestData.NorthOnly(TrainTestData.Unit1));
            var same = TrainTimetableViewState.Ready(TrainTestData.NorthOnly(TrainTestData.Unit1));
            var moved = TrainTimetableViewState.Ready(TrainTestData.Ready(TrainTestData.Unit1, true, TrainTestData.Added(TrainTestData.NoStops, TrainTestData.North)));
            Assert.IsTrue(TrainTimetableViewState.IsSameState(first, same));
            Assert.IsFalse(TrainTimetableViewState.IsSameState(first, moved));
            Assert.IsTrue(TrainTimetableViewState.IsSameState(TrainTimetableViewState.Loading, TrainTimetableViewState.Loading));
            Assert.IsFalse(TrainTimetableViewState.IsSameState(first, TrainTimetableViewState.Loading));
        }

        private static TrainTimetableStop StopAt(Vector3Int position)
        {
            return new TrainTimetableStop(position, StationNodeSide.Back, TrainDiagram.DepartureConditionType.WaitForTicks, 20);
        }

        private static TrainTimetableSnapshot Snapshot(params TrainTimetableStop[] stops)
        {
            return new TrainTimetableSnapshot(TrainTestData.Unit1, false, 0, stops);
        }
    }
}
```
（`TrainTimetableSnapshot` のコンストラクタの引数順は Task 1 Step 3 の実物に合わせる）

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainTimetableStationCatalogTest|TrainTimetableReadyDataFactoryTest"`
Expected: コンパイルエラー（型が無い）

- [ ] **Step 2: 型を書く**

`TrainTimetableViewState.cs`:

```csharp
namespace Client.Ui.State.Train.Timetable
{
    /// <summary>
    /// 時刻表タブに出す状態。読込中・使えない・用意済みの 3 つ
    /// State shown in the timetable tab: loading, unavailable, or ready
    /// </summary>
    public abstract class TrainTimetableViewState
    {
        public static readonly TrainTimetableViewState Loading = new LoadingTrainTimetableViewState();
        public static readonly TrainTimetableViewState Unavailable = new UnavailableTrainTimetableViewState();

        public static TrainTimetableViewState Ready(TrainTimetableReadyData data)
        {
            return new ReadyTrainTimetableViewState(data);
        }

        // 同じ状態なら流し直さない。用意済みは中身で比べ、読込中・使えないは同じインスタンス
        // Same states are not republished: ready compares contents, loading/unavailable are single instances
        public static bool IsSameState(TrainTimetableViewState current, TrainTimetableViewState next)
        {
            if (current is ReadyTrainTimetableViewState currentReady && next is ReadyTrainTimetableViewState nextReady) return currentReady.Data.HasSameContent(nextReady.Data);
            return ReferenceEquals(current, next);
        }
    }

    public sealed class LoadingTrainTimetableViewState : TrainTimetableViewState
    {
        internal LoadingTrainTimetableViewState() { }
    }

    public sealed class UnavailableTrainTimetableViewState : TrainTimetableViewState
    {
        internal UnavailableTrainTimetableViewState() { }
    }

    public sealed class ReadyTrainTimetableViewState : TrainTimetableViewState
    {
        public TrainTimetableReadyData Data { get; }

        internal ReadyTrainTimetableViewState(TrainTimetableReadyData data)
        {
            Data = data;
        }
    }
}
```

`TrainTimetableReadyData.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Client.Ui.State.Train.Logic;
using Game.Train.Unit;

namespace Client.Ui.State.Train.Timetable
{
    /// <summary>
    /// サーバーから届いた時刻表と、追加先の駅一覧
    /// The timetable published by the server and the stations it can add
    /// </summary>
    public sealed class TrainTimetableReadyData
    {
        public TrainUnitInstanceId TrainUnitInstanceId { get; }
        public bool IsAutoRun { get; }
        public int CurrentIndex { get; }
        public IReadOnlyList<TimetableEditStop> Stops { get; }
        public IReadOnlyList<TrainTimetableStation> Stations { get; }

        public TrainTimetableReadyData(TrainUnitInstanceId trainUnitInstanceId, bool isAutoRun, int currentIndex, IReadOnlyList<TimetableEditStop> stops, IReadOnlyList<TrainTimetableStation> stations)
        {
            TrainUnitInstanceId = trainUnitInstanceId;
            IsAutoRun = isAutoRun;
            CurrentIndex = currentIndex;
            Stops = stops;
            Stations = stations;
        }

        // 停車駅は座標・端・名前、駅一覧は座標・名前で比べる（値の構造体の既定の Equals。前例 CursorTooltipViewModel）
        // Stops compare position, side and name; stations compare position and name (struct default Equals; precedent CursorTooltipViewModel)
        public bool HasSameContent(TrainTimetableReadyData other)
        {
            return TrainUnitInstanceId.Equals(other.TrainUnitInstanceId) && IsAutoRun == other.IsAutoRun && CurrentIndex == other.CurrentIndex
                   && Stops.SequenceEqual(other.Stops) && Stations.SequenceEqual(other.Stations);
        }
    }
}
```

`TrainTimetableStationCatalog.cs`:

```csharp
using System.Collections.Generic;
using Client.Ui.State.Train.Logic;

namespace Client.Ui.State.Train.Timetable
{
    /// <summary>
    /// 駅一覧を x → z → y で並べる（Web ホスト TrainTimetableDtoBuilder.SortStations と同じ規則。D-2）
    /// Orders stations by x, then z, then y (same rule as the Web host TrainTimetableDtoBuilder.SortStations; D-2)
    /// </summary>
    public static class TrainTimetableStationCatalog
    {
        public static List<TrainTimetableStation> Sorted(IEnumerable<TrainTimetableStation> stations)
        {
            var sorted = new List<TrainTimetableStation>(stations);
            sorted.Sort((a, b) =>
            {
                var byX = a.Position.x.CompareTo(b.Position.x);
                if (byX != 0) return byX;
                var byZ = a.Position.z.CompareTo(b.Position.z);
                return byZ != 0 ? byZ : a.Position.y.CompareTo(b.Position.y);
            });
            return sorted;
        }
    }
}
```

`TrainTimetableReadyDataFactory.cs`:

```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.Train.Logic;
using Game.Train.RailGraph;
using Game.Train.Unit;
using UnityEngine;

namespace Client.Ui.State.Train.Timetable
{
    /// <summary>
    /// 時刻表のスナップショットと駅一覧から用意済みの時刻表を組む（Web ホスト CreateFromTimetable と同じ規則。D-2）
    /// Builds the ready timetable from a snapshot and the station list (same rule as the Web host CreateFromTimetable; D-2)
    /// </summary>
    public sealed class TrainTimetableReadyDataFactory
    {
        private readonly HashSet<Vector3Int> _lastWarnedMissingStationPositions = new();

        public bool TryCreate(TrainTimetableSnapshot timetable, IReadOnlyList<TrainTimetableStation> stations, out TrainTimetableReadyData data)
        {
            data = null;
            var stationNames = new Dictionary<Vector3Int, string>();
            foreach (var station in stations) stationNames[station.Position] = station.Name;

            // 停車駅ごとに駅名を当て、未知の端は表示ごと捨てて Unavailable にする
            // Name each stop; an unknown side discards the whole timetable as unavailable
            var stops = new List<TimetableEditStop>(timetable.Stops.Count);
            var missingStationPositions = new HashSet<Vector3Int>();
            foreach (var stop in timetable.Stops)
            {
                if (!Enum.IsDefined(typeof(StationNodeSide), stop.Side))
                {
                    Debug.LogError($"[TrainTimetableModel] discarding timetable with unknown side: {timetable.TrainUnitInstanceId}");
                    return false;
                }
                if (!stationNames.TryGetValue(stop.StationPosition, out var name)) missingStationPositions.Add(stop.StationPosition);
                stops.Add(new TimetableEditStop(stop.StationPosition, name ?? string.Empty, stop.Side));
            }
            WarnMissingStationsWhenChanged(timetable.TrainUnitInstanceId, missingStationPositions);

            data = new TrainTimetableReadyData(timetable.TrainUnitInstanceId, timetable.IsAutoRun, timetable.CurrentIndex, stops, stations);
            return true;
        }

        // 欠けの集合が前回と同じなら黙る。公開のたびに同じ警告を出さないため（Web ホストと同じ）
        // Stay quiet when the missing set is unchanged so each publish does not repeat the warning (same as the Web host)
        private void WarnMissingStationsWhenChanged(TrainUnitInstanceId trainUnitInstanceId, HashSet<Vector3Int> missingStationPositions)
        {
            if (_lastWarnedMissingStationPositions.SetEquals(missingStationPositions)) return;
            _lastWarnedMissingStationPositions.Clear();
            _lastWarnedMissingStationPositions.UnionWith(missingStationPositions);
            if (missingStationPositions.Count == 0) return;
            Debug.LogWarning($"[TrainTimetableModel] station blocks not found for stops at {string.Join(", ", missingStationPositions)}; emitting empty names: {trainUnitInstanceId}");
        }
    }
}
```

`ITrainTimetableSource.cs`:

```csharp
using UniRx;

namespace Client.Ui.State.Train.Timetable
{
    /// <summary>
    /// 開いている列車の時刻表の状態。列車が開いていなければ Unavailable
    /// Timetable state of the open train; unavailable when no train is open
    /// </summary>
    public interface ITrainTimetableSource
    {
        IReadOnlyReactiveProperty<TrainTimetableViewState> Current { get; }
    }
}
```

`TrainTimetableModel.cs`:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Train.Timetable;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Ui.State.Train.Logic;
using Game.Block.Blocks.TrainRail;
using UniRx;
using UnityEngine;
using VContainer.Unity;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Client.Ui.State.Train.Timetable
{
    /// <summary>
    /// 開いている貨車の時刻表を Web ホスト TrainTimetableDtoBuilder.Build と同じ優先で組む（D-2）
    /// Builds the open car's timetable with the same priority as the Web host TrainTimetableDtoBuilder.Build (D-2)
    /// </summary>
    public sealed class TrainTimetableModel : ITrainTimetableSource, IInitializable
    {
        private readonly SubInventoryState _subInventoryState;
        private readonly UIStateControl _uiStateControl;
        private readonly TrainUnitClientCache _cache;
        private readonly IClientTrainTimetableLookup _timetables;
        private readonly TrainTimetableFetcher _fetcher;
        private readonly BlockGameObjectDataStore _blocks;
        private readonly OpenTrainTimetableTracker _tracker;
        private readonly TrainTimetableReadyDataFactory _readyDataFactory = new();
        private readonly ReactiveProperty<TrainTimetableViewState> _current = new(TrainTimetableViewState.Unavailable);

        public IReadOnlyReactiveProperty<TrainTimetableViewState> Current => _current;

        public TrainTimetableModel(SubInventoryState subInventoryState, UIStateControl uiStateControl, TrainUnitClientCache cache, IClientTrainTimetableLookup timetables, TrainTimetableFetcher fetcher, BlockGameObjectDataStore blocks, OpenTrainTimetableTracker tracker)
        {
            _subInventoryState = subInventoryState;
            _uiStateControl = uiStateControl;
            _cache = cache;
            _timetables = timetables;
            _fetcher = fetcher;
            _blocks = blocks;
            _tracker = tracker;
        }

        public void Initialize()
        {
            // Web ホスト BlockInventoryTopic と同じ契機: 画面の開閉（閉じると使えないへ倒れ、編集は閉じる）・中身の更新・時刻表の到着
            // Same triggers as the Web host BlockInventoryTopic: screen open/close (closing falls to unavailable and closes the edit), contents updates, timetable arrival
            _uiStateControl.OnStateChanged.Subscribe(_ => Refresh());
            _subInventoryState.OnSubInventoryUpdated.Subscribe(_ => Refresh());
            _timetables.OnTimetableUpdated.Subscribe(_ => Refresh());
            _tracker.OnRepublishRequested.Subscribe(_ => Refresh());
            // 取得の開始は UITK だけが拾い、依頼の直後に読込中を出す（Web ホストの配信は変えない。D-20）
            // Only UITK picks up fetch start to show Loading right after the request (the Web host publishing is unchanged; D-20)
            _fetcher.OnFetchStarted.Subscribe(_ => Refresh());
            Refresh();
        }

        private void Refresh()
        {
            var next = _subInventoryState.CurrentSubInventorySource is TrainSubInventorySource source
                ? Build(source.TrainCarInstanceId)
                : TrainTimetableViewState.Unavailable;
            // silent-ok: 中身が同じ状態は配り直さない / 表示が変わらないので見ている人に差は無く、行の作り直しも起きない（R-5）
            if (TrainTimetableViewState.IsSameState(_current.Value, next)) return;
            _current.Value = next;
        }

        private TrainTimetableViewState Build(long trainCarInstanceId)
        {
            // 優先: 列車が引けない → 使えない → 取得中 → キャッシュ無し → 用意済み
            // Priority: no owning train, unavailable, fetch in flight, no cache, then ready
            if (!OpenTrainUnitResolver.TryResolveOwningTrain(trainCarInstanceId, _cache, out var trainUnitInstanceId))
            {
                Debug.LogWarning($"[TrainTimetableModel] Missing car snapshot: {trainCarInstanceId}");
                return TrainTimetableViewState.Unavailable;
            }
            if (_fetcher.IsUnavailable(trainUnitInstanceId)) return TrainTimetableViewState.Unavailable;
            if (_fetcher.IsFetchInFlight(trainUnitInstanceId)) return TrainTimetableViewState.Loading;
            if (!_timetables.TryGet(trainUnitInstanceId, out var timetable)) return TrainTimetableViewState.Loading;
            return _readyDataFactory.TryCreate(timetable, ReadStations(), out var data)
                ? TrainTimetableViewState.Ready(data)
                : TrainTimetableViewState.Unavailable;
        }

        // 駅ブロックの位置と駅名（サーバーの StateDetail。届く前は空名で並べる。Web ホストと同じ）
        // Station block positions and names (a server StateDetail; empty until it arrives, same as the Web host)
        private List<TrainTimetableStation> ReadStations()
        {
            var stations = new List<TrainTimetableStation>();
            foreach (var block in _blocks.GetBlocksByBlockType(BlockTypeConst.TrainStation))
            {
                var name = block.GetStateDetail<TrainStationNameStateDetail>(TrainStationNameStateDetail.BlockStateDetailKey)?.StationName;
                // silent-ok: 駅名の StateDetail が届く前は空名で並べ、無名の駅の文言で出す（Web ホストと同じ）/ 届かないままなら駅一覧が無名のままで、録画 train/train-timetable-via-ui の「1駅目は北駅」の主張が落ちて開発者が気づく
                stations.Add(new TrainTimetableStation(block.BlockPosInfo.OriginalPos, name ?? string.Empty));
            }
            return TrainTimetableStationCatalog.Sorted(stations);
        }
    }
}
```

- [ ] **Step 3: 取得の開始の合図を足す（既存の Fetcher・プローブへの追記）**

`S/Client.Ui.State/Train/TrainTimetableFetcher.cs` は今 198 行で、合図を足すと 200 行を超える。先に取得ラッチを `S/Client.Ui.State/Train/TrainTimetableFetchLatch.cs`（新設。`_requestedTrainUnitInstanceId`・`_unavailableTrainUnitInstanceId`・`_isFetchInFlight`・`_retriedForRequestedTrain` と、それを読む `IsUnavailable`・`IsFetchInFlight`、`Close` の中のラッチの初期化）へ出し、Fetcher はラッチを 1 つ持って委ねる（公開の口と振る舞いは変えない。Fetcher の `IsUnavailable`・`IsFetchInFlight` はラッチへの素通しの式本体にする）。そのうえで:
- `private readonly Subject<UniRx.Unit> _onFetchStarted = new();` と `public IObservable<UniRx.Unit> OnFetchStarted => _onFetchStarted;`（直前に「取得中を立てた直後に流す。購読は UITK の TrainTimetableModel だけ」の日英 2 行）
- `RequestForOpenedTab` で取得中を立てた直後に `_onFetchStarted.OnNext(UniRx.Unit.Default);`（購読側が組み直すと読込中になる）
- `OnFetchFailed`・`OpenTrainTimetableTracker`・`Client.WebUiHost/**` は変えない（Web の配信の契機は今のまま）

`S/Client.Tests/EditModeInPlayingTest/Hostless/TimetableStateProbe.cs`: `fetcher.RequestForOpenedTab(unknownTrain);` の前で `var starts = 0; using var counting = fetcher.OnFetchStarted.Subscribe(_ => starts++);` を取り、直後に `Assert.AreEqual(1, starts, "取得の開始が通知されない");`（本番の Fetcher は送信境界に触れるので L0 では試せない。前例のプローブの中で確かめる）

- [ ] **Step 4: テストを回す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainTimetableStationCatalogTest|TrainTimetableReadyDataFactoryTest|TrainTimetableFetcherTest|WebUiHostlessStateAvailabilityTest"`
Expected: `ErrorCount: 0`、全件 PASS（State 4 件・既存の Fetcher 3 件〈ラッチの分割で振る舞いが変わらない〉・Hostless のプローブ）。`wc -l` で Fetcher とラッチがどちらも 200 行以下。Hostless は PlayMode へ遷移するので、終わったらドメインリロードの待ちを入れる

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Train moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Train moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Hostless/TimetableStateProbe.cs
git commit -m "feat(uitk): 時刻表の状態の Model を足し、取得の開始の合図を足す（S4-train）

<実行ホストの指示する帰属行>"
```

---

### Task 4: 即時編集・コマンド・時刻表の編集 Model（`Client.Ui.State/Train/TimetableEdit/`）

**Files:**
- Create: `S/Client.Ui.State/Train/TimetableEdit/` の 10 本（編集の器・描画値の Model・編集のコマンド・セッション・時間切れ・描画値 2）と `TimetableEdit/Send/` の 3 本（サーバーへ送る `ITrainTimetableCommands`・`TrainTimetableCommands`・`TrainTimetableDeparture`。namespace `Client.Ui.State.Train.TimetableEdit.Send`。編集のコマンドを分けて 13 本になり 1 ディレクトリ 10 本を超えるので、送信の 3 本を下へ出す。D-33）
- Create: `S/Client.Tests/Ui/State/Train/TimetableEdit/TrainTimetableEditSessionTest.cs`・`TrainTimetableDepartureTest.cs`・`TrainTimetableEditModelTest.cs`、`S/Client.Tests/Ui/State/Train/Fakes/FakeTrainTimetableCommands.cs`・`ManualTrainTimetableWaitTimer.cs`・`FakeTrainSources.cs`

**Interfaces:**
- Consumes: Task 3 の `ITrainTimetableSource`・`TrainTimetableViewState`・`TrainTimetableReadyData`、`TimetableEditLogic`（L0-s4b）、`UiCommandResultToaster`、`TrainCommandRejection`（Task 2）、`TrainScheduleEditProtocol.TrainScheduleEditRequest`、`TrainTimetableFetcher`・`OpenTrainUnitResolver`（既存。Web ホストの `TrainTimetableOpenActionHandler` が呼ぶのと同じ口）
- Produces:
```csharp
namespace Client.Ui.State.Train.TimetableEdit
namespace Client.Ui.State.Train.TimetableEdit.Send
public interface ITrainTimetableCommands { void RequestOpen(); UniTask<UiCommandResult> Replace(IReadOnlyList<TimetableReplaceStop> stops); UniTask<UiCommandResult> SetAutoRun(bool enabled); }   // サーバーへ送る口（本番 TrainTimetableCommands）
namespace Client.Ui.State.Train.TimetableEdit
public interface ITrainTimetableWaitTimer { IObservable<Unit> After(TimeSpan delay); }
public sealed class TrainTimetableEditSession   // 状態は private な判別 union（Idle・Sending・AwaitingPublication）
{
    public const string ReplaceCommandName = "train_timetable.replace";
    public static readonly TimeSpan PublicationTimeout;   // 5000ms（Web DEFAULT_ACTION_TIMEOUT_MS）
    public TrainTimetableEditSession(ITrainTimetableCommands commands, ITrainTimetableWaitTimer timer, UiCommandResultToaster toaster, TrainTimetableReadyData server);
    public TrainTimetableReadyData Server { get; }  public IReadOnlyList<TimetableEditStop> DisplayedStops { get; }  public bool IsPending { get; }  public IObservable<Unit> OnDisplayChanged { get; }
    public void ReceiveServer(TrainTimetableReadyData server); public void Replace(IReadOnlyList<TimetableEditStop> nextStops); public void Close();
}
public sealed class TrainTimetableSectionViewModel { Stops; int? CurrentIndex; Stations; IsPending; IsAutoRun; CanEnableAutoRun; static Build(server, displayedStops, isPending); bool HasSameContent(other); }
public abstract class TrainTimetableAreaViewModel { static Loading; static Unavailable; static Ready(TrainUnitInstanceId, TrainTimetableSectionViewModel); static bool IsSameArea(current, next); }
public sealed class ReadyTrainTimetableArea : TrainTimetableAreaViewModel { TrainUnitInstanceId TrainUnitInstanceId; TrainTimetableSectionViewModel Section; bool IsSameTrain(ReadyTrainTimetableArea other); }   // View は列車の型を持たずこれで比べる
public sealed class TrainTimetableEditState : IInitializable   // 編集セッションの器（Model と Commands が共有。前例 PauseMenuStateService。D-33）
{
    public TrainTimetableEditState(ITrainTimetableSource source, ITrainTimetableCommands commands, ITrainTimetableWaitTimer timer, UiCommandResultToaster toaster);
    public bool IsLoading { get; }  public IObservable<Unit> OnChanged { get; }  public bool TryGetSession(out TrainTimetableEditSession session);
}
public interface ITrainTimetableEditModel { IReadOnlyReactiveProperty<TrainTimetableAreaViewModel> Current { get; } }   // 描画値だけ（Presenter が購読）
public sealed class TrainTimetableEditModel : ITrainTimetableEditModel, IInitializable   // ctor (TrainTimetableEditState state)
public interface ITrainTimetableEditCommands   // View が直接呼ぶ（設計書 A-8）。結果のトーストはコマンド側が済ませる
{
    UiCommandResult Add(TrainTimetableStation station); UiCommandResult Move(int index, StopMoveDirection direction); UiCommandResult Remove(int index); UiCommandResult SetAutoRun(bool enabled);
}
public sealed class TrainTimetableEditCommands : ITrainTimetableEditCommands   // ctor (TrainTimetableEditState state, ITrainTimetableCommands commands, UiCommandResultToaster toaster)
```

- [ ] **Step 1: 偽物と失敗するテストを書く**

`FakeTrainTimetableCommands.cs`:

```csharp
using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.TimetableEdit.Send;
using Cysharp.Threading.Tasks;

namespace Client.Tests.Ui.State.Train.Fakes
{
    /// <summary>
    /// 送られた置換・自動運転を記録し、結果はテストが手で返す
    /// Records sent replaces and auto-run toggles; tests resolve the results by hand
    /// </summary>
    public sealed class FakeTrainTimetableCommands : ITrainTimetableCommands
    {
        public int OpenRequestCount { get; private set; }
        public List<IReadOnlyList<TimetableReplaceStop>> SentReplaces { get; } = new();
        public List<bool> SentAutoRuns { get; } = new();
        private readonly Queue<UniTaskCompletionSource<UiCommandResult>> _pending = new();

        public void RequestOpen()
        {
            OpenRequestCount++;
        }

        public UniTask<UiCommandResult> Replace(IReadOnlyList<TimetableReplaceStop> stops)
        {
            SentReplaces.Add(stops);
            return Enqueue();
        }

        public UniTask<UiCommandResult> SetAutoRun(bool enabled)
        {
            SentAutoRuns.Add(enabled);
            return Enqueue();
        }

        public void Accept()
        {
            _pending.Dequeue().TrySetResult(UiCommandAccepted.Instance);
        }

        // 理由ごとの拒否。Detail はサーバーの FailureReason の写し（無ければ空文字。D-31）
        // Per-reason refusal; Detail mirrors the server FailureReason (empty when absent; D-31)
        public void Reject(UiCommandRejectReason reason, string serverFailureReason)
        {
            _pending.Dequeue().TrySetResult(new UiCommandRejected(reason, serverFailureReason));
        }

        private UniTask<UiCommandResult> Enqueue()
        {
            var source = new UniTaskCompletionSource<UiCommandResult>();
            _pending.Enqueue(source);
            return source.Task;
        }
    }
}
```

`ManualTrainTimetableWaitTimer.cs`:

```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.Train.TimetableEdit;
using UniRx;

namespace Client.Tests.Ui.State.Train.Fakes
{
    /// <summary>
    /// 時間切れをテストが手で起こす
    /// Lets tests fire the timeout by hand
    /// </summary>
    public sealed class ManualTrainTimetableWaitTimer : ITrainTimetableWaitTimer
    {
        public List<TimeSpan> RequestedDelays { get; } = new();
        private readonly Subject<Unit> _elapsed = new();

        public IObservable<Unit> After(TimeSpan delay)
        {
            RequestedDelays.Add(delay);
            return _elapsed.Take(1);
        }

        public void Elapse()
        {
            _elapsed.OnNext(Unit.Default);
        }
    }
}
```

`FakeTrainSources.cs`（源の偽物。ここでは時刻表の口だけを持ち、Task 5 Step 1 で貨車・プラットフォームの口を足す）:

```csharp
using Client.Ui.State.Train.Timetable;
using UniRx;

namespace Client.Tests.Ui.State.Train.Fakes
{
    /// <summary>
    /// 源の偽物。値はテストが Set で押し込む
    /// Fake sources; tests push values with Set
    /// </summary>
    public sealed class FakeTrainSources : ITrainTimetableSource
    {
        private readonly ReactiveProperty<TrainTimetableViewState> _timetable = new(TrainTimetableViewState.Loading);

        IReadOnlyReactiveProperty<TrainTimetableViewState> ITrainTimetableSource.Current => _timetable;

        public void SetTimetable(TrainTimetableViewState state)
        {
            _timetable.Value = state;
        }
    }
}
```

`TrainTimetableEditSessionTest.cs`（ScreenTestHost は使わない。トースト係は `GameUiToastFeed` から組む。期待の並びは `TrainTestData.Added`・`TimetableEditLogic` で組み、Side を手で書かない）:

```csharp
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.State.Commands;
using Client.Ui.State.Toast;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.TimetableEdit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.Ui.State.Train.Fakes.TrainTestData;

namespace Client.Tests.Ui.State.Train.TimetableEdit
{
    public class TrainTimetableEditSessionTest
    {
        private FakeTrainTimetableCommands _commands;
        private ManualTrainTimetableWaitTimer _timer;
        private TrainTimetableEditSession _session;
        private IReadOnlyList<TimetableEditStop> _server;

        [SetUp]
        public void SetUp()
        {
            _commands = new FakeTrainTimetableCommands();
            _timer = new ManualTrainTimetableWaitTimer();
            _server = Added(NoStops, North);
            _session = new TrainTimetableEditSession(_commands, _timer, new UiCommandResultToaster(new GameUiToastFeed()), Ready(Unit1, false, _server));
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableImmediate.test.ts">immediately sends each add, move and remove without an Apply button</web-test>
        [Test]
        public void 追加移動削除を適用ボタンなしで即送る()
        {
            // 追加は即送信され、表示も即替わる
            // An add is sent at once and shows at once
            var added = Added(_server, Unnamed);
            _session.Replace(added);
            Assert.AreEqual(1, _commands.SentReplaces.Count);
            Assert.AreEqual(2, _session.DisplayedStops.Count);
            Assert.IsTrue(_session.IsPending);

            // 受理と公開で保留が解け、続けて移動・削除も 1 回ずつ送る
            // Acceptance plus publication releases it; move and remove then send once each
            _commands.Accept();
            _session.ReceiveServer(Ready(Unit1, false, added));
            Assert.IsFalse(_session.IsPending);
            var moved = TimetableEditLogic.MoveStop(added, 1, StopMoveDirection.Up);
            _session.Replace(moved);
            _commands.Accept();
            _session.ReceiveServer(Ready(Unit1, false, moved));
            _session.Replace(TimetableEditLogic.RemoveStop(moved, 0));
            Assert.AreEqual(3, _commands.SentReplaces.Count);
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableImmediate.test.ts">waits for publication after acceptance so the next edit cannot use stale stops</web-test>
        [Test]
        public void 受理後も公開が届くまで次の編集を受けない()
        {
            var added = Added(_server, Unnamed);
            _session.Replace(added);
            _commands.Accept();
            Assert.IsTrue(_session.IsPending);
            Assert.AreEqual(new[] { TimeSpan.FromMilliseconds(5000) }, _timer.RequestedDelays);

            // 保留中の編集は警告して捨てる
            // An edit while pending is dropped with a warning
            LogAssert.Expect(LogType.Warning, new Regex(@"\[TrainTimetableEdit\] ignored"));
            _session.Replace(TimetableEditLogic.RemoveStop(added, 0));
            Assert.AreEqual(1, _commands.SentReplaces.Count);
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableImmediate.test.ts">waits for acceptance even when the publication arrives first</web-test>
        [Test]
        public void 公開が先に届いても受理まで保留する()
        {
            var added = Added(_server, Unnamed);
            _session.Replace(added);
            _session.ReceiveServer(Ready(Unit1, false, added));
            Assert.IsTrue(_session.IsPending);

            // 受理の時点でサーバーが同じ並びなら時間切れを待たずに解く
            // On acceptance with a matching server order, release without waiting for the timeout
            _commands.Accept();
            Assert.IsFalse(_session.IsPending);
            Assert.IsEmpty(_timer.RequestedDelays);
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableImmediate.test.ts">reverts rejected edits to the newest authority and allows another edit</web-test>
        [Test]
        public void 拒否は最新のサーバーの並びへ戻し次の編集を受ける()
        {
            _session.Replace(Added(_server, Unnamed));
            var remote = Added(NoStops, Unnamed);
            _session.ReceiveServer(Ready(Unit1, false, remote));

            // 拒否で保留を解き、表示は届いた最新の並び
            // Rejection releases the pending edit and shows the newest published order
            // トースト係のログは Detail（サーバーの FailureReason）を出す（UiCommandRejected.ToWireCode）
            // The toaster log prints the Detail (the server FailureReason; UiCommandRejected.ToWireCode)
            LogAssert.Expect(LogType.Warning, new Regex(@"\[UiCommandResultToaster\] train_timetable.replace failed: busy"));
            _commands.Reject(UiCommandRejectReason.TrainTimetableReplaceFailed, "busy");
            Assert.IsFalse(_session.IsPending);
            CollectionAssert.AreEqual(remote, _session.DisplayedStops);
            _session.Replace(TimetableEditLogic.RemoveStop(remote, 0));
            Assert.AreEqual(2, _commands.SentReplaces.Count);
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableImmediate.test.ts">releases a missing publication after the existing transport timeout</web-test>
        [Test]
        public void 公開が来なければ時間切れで解く()
        {
            _session.Replace(Added(_server, Unnamed));
            _commands.Accept();
            LogAssert.Expect(LogType.Warning, new Regex(@"\[TrainTimetableEdit\] publication did not arrive"));
            _timer.Elapse();
            Assert.IsFalse(_session.IsPending);
            Assert.AreEqual(1, _session.DisplayedStops.Count);
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableImmediate.test.ts">does not defer an edit or refresh a different train after closing</web-test>
        [Test]
        public void 閉じた後に戻った結果は表示を動かさない()
        {
            _session.Replace(Added(_server, Unnamed));
            var displayedAtClose = _session.DisplayedStops;
            _session.Close();

            // 閉じた後の受理は捨て、時間切れも仕掛けない
            // Acceptance after close is discarded and no timeout is armed
            LogAssert.Expect(LogType.Warning, new Regex(@"\[TrainTimetableEdit\] result after close discarded"));
            _commands.Accept();
            Assert.AreSame(displayedAtClose, _session.DisplayedStops);
            Assert.IsEmpty(_timer.RequestedDelays);
        }
    }
}
```

`TrainTimetableDepartureTest.cs`:

```csharp
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.TimetableEdit.Send;
using Core.Update;
using Game.Train.Diagram;
using Game.Train.RailGraph;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.State.Train.TimetableEdit
{
    public class TrainTimetableDepartureTest
    {
        [Test]
        public void 送る停車駅の出発条件は1秒待ち固定()
        {
            // Web ホスト TrainTimetableStopParser と同じ固定の出発条件
            // Same fixed departure condition as the Web host TrainTimetableStopParser
            var stops = TrainTimetableDeparture.ToServerStops(new[] { new TimetableReplaceStop(new Vector3Int(1, 2, 3), StationNodeSide.Front) });
            Assert.AreEqual(new Vector3Int(1, 2, 3), stops[0].StationPosition);
            Assert.AreEqual(StationNodeSide.Front, stops[0].Side);
            Assert.AreEqual(TrainDiagram.DepartureConditionType.WaitForTicks, stops[0].DepartureConditionType);
            Assert.AreEqual(GameUpdater.TicksPerSecond, stops[0].WaitTicks);
        }
    }
}
```
（ここの `Front` はサーバーへ送る形の写しの確認で、`AddStop` の並びとは比べない）

`TrainTimetableEditModelTest.cs`（器・描画値の Model・編集のコマンドを本番どおり組み、源とサーバーへ送る口だけ偽物にする。D-33）:

```csharp
using System.Text.RegularExpressions;
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.State.Commands;
using Client.Ui.State.Toast;
using Client.Ui.State.Train.Timetable;
using Client.Ui.State.Train.TimetableEdit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.Ui.State.Train.Fakes.TrainTestData;

namespace Client.Tests.Ui.State.Train.TimetableEdit
{
    public class TrainTimetableEditModelTest
    {
        private FakeTrainSources _sources;
        private FakeTrainTimetableCommands _send;
        private ManualTrainTimetableWaitTimer _timer;
        private TrainTimetableEditModel _model;
        private TrainTimetableEditCommands _edit;

        [SetUp]
        public void SetUp()
        {
            _sources = new FakeTrainSources();
            _send = new FakeTrainTimetableCommands();
            _timer = new ManualTrainTimetableWaitTimer();
            var toaster = new UiCommandResultToaster(new GameUiToastFeed());
            var state = new TrainTimetableEditState(_sources, _send, _timer, toaster);
            _model = new TrainTimetableEditModel(state);
            _edit = new TrainTimetableEditCommands(state, _send, toaster);
            state.Initialize();
            _model.Initialize();
        }

        [Test]
        public void 送った後に閉じると受理も拒否もトーストと時間切れを出さない()
        {
            // 閉じると源は使えないへ倒れる（本番は TrainTimetableModel が UIStateControl.OnStateChanged で組み直す。録画手順 6 で確かめる）
            // Closing turns the source unavailable (in production TrainTimetableModel rebuilds on UIStateControl.OnStateChanged; recording step 6 checks it)
            _sources.SetTimetable(TrainTimetableViewState.Ready(NorthOnly(Unit1)));
            Assert.IsInstanceOf<UiCommandAccepted>(_edit.Add(Unnamed));
            _sources.SetTimetable(TrainTimetableViewState.Unavailable);
            LogAssert.Expect(LogType.Warning, new Regex(@"\[TrainTimetableEdit\] result after close discarded"));
            _send.Accept();
            Assert.IsEmpty(_timer.RequestedDelays);

            // 開き直して送り、また閉じてから拒否が戻っても、トーストの警告は出ない
            // Reopen, send, close again; a late refusal raises no toaster warning either
            _sources.SetTimetable(TrainTimetableViewState.Ready(NorthOnly(Unit1)));
            _edit.Add(Unnamed);
            _sources.SetTimetable(TrainTimetableViewState.Unavailable);
            LogAssert.Expect(LogType.Warning, new Regex(@"\[TrainTimetableEdit\] result after close discarded"));
            _send.Reject(UiCommandRejectReason.TrainTimetableReplaceFailed, "late");
            LogAssert.NoUnexpectedReceived();
            Assert.IsInstanceOf<UnavailableTrainTimetableArea>(_model.Current.Value);
        }

        [Test]
        public void 列車が替わると保留を捨てて新しい列車の並びを出す()
        {
            // 列車 1 で追加して保留中のまま列車 2 が届く（Web の key=trainUnitId と同じく持ち越さない）
            // An add on train 1 is still pending when train 2 arrives (nothing carries over, like the Web key=trainUnitId)
            _sources.SetTimetable(TrainTimetableViewState.Ready(NorthOnly(Unit1)));
            _edit.Add(Unnamed);
            Assert.IsTrue(Section().IsPending);
            _sources.SetTimetable(TrainTimetableViewState.Ready(Ready(Unit2, false, NoStops)));
            Assert.IsFalse(Section().IsPending);
            Assert.AreEqual(0, Section().Stops.Count);

            // 列車 1 の結果が後から戻っても列車 2 の表示は動かない
            // A late result for train 1 does not move train 2's display
            LogAssert.Expect(LogType.Warning, new Regex(@"\[TrainTimetableEdit\] result after close discarded"));
            _send.Accept();
            Assert.AreEqual(0, Section().Stops.Count);
        }

        [Test]
        public void 時刻表が開いていない操作は拒否して送らない()
        {
            // 開いていないブロックへの操作と同じ理由で拒否し、Web と同じくトーストする（非良性。D-32）
            // Refused with the same reason as an operation on a block that is not open, and toasted like the Web (not benign; D-32)
            _sources.SetTimetable(TrainTimetableViewState.Unavailable);
            LogAssert.Expect(LogType.Warning, new Regex(@"\[TrainTimetableEdit\] rejected: block_not_open"));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[UiCommandResultToaster\] train_timetable\.replace failed: block_not_open"));
            var result = _edit.Add(North);
            Assert.AreEqual(UiCommandRejectReason.BlockNotOpen, ((UiCommandRejected)result).Reason);
            Assert.IsEmpty(_send.SentReplaces);
        }

        private TrainTimetableSectionViewModel Section()
        {
            return ((ReadyTrainTimetableArea)_model.Current.Value).Section;
        }
    }
}
```

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainTimetableEditSessionTest|TrainTimetableDepartureTest|TrainTimetableEditModelTest"`
Expected: コンパイルエラー（型が無い）

- [ ] **Step 2: 口とタイマーと出発条件を書く**

`Send/ITrainTimetableCommands.cs`:

```csharp
using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Train.Logic;
using Cysharp.Threading.Tasks;

namespace Client.Ui.State.Train.TimetableEdit.Send
{
    /// <summary>
    /// 開いている列車の時刻表への操作。列車はその時点で開いている貨車から引く（Web と同じ）
    /// Operations on the open train's timetable; the train resolves from the car open at that moment (same as Web)
    /// </summary>
    public interface ITrainTimetableCommands
    {
        void RequestOpen();
        UniTask<UiCommandResult> Replace(IReadOnlyList<TimetableReplaceStop> stops);
        UniTask<UiCommandResult> SetAutoRun(bool enabled);
    }
}
```

`ITrainTimetableWaitTimer.cs`:

```csharp
using System;
using UniRx;

namespace Client.Ui.State.Train.TimetableEdit
{
    /// <summary>
    /// 受理後の公開待ちの時間切れ。テストで手で起こせるよう口にする
    /// Timeout for awaiting publication after acceptance; an interface so tests can fire it by hand
    /// </summary>
    public interface ITrainTimetableWaitTimer
    {
        IObservable<Unit> After(TimeSpan delay);
    }
}
```

`UniRxTrainTimetableWaitTimer.cs`:

```csharp
using System;
using UniRx;

namespace Client.Ui.State.Train.TimetableEdit
{
    /// <summary>
    /// 実時間の待ち。UI の通信待ちでゲームロジックの経過ではないので実時間でよい（Web の通信の時間切れと同じ）
    /// Real-time wait; this is a UI transport wait, not game logic time (same as the Web transport timeout)
    /// </summary>
    public sealed class UniRxTrainTimetableWaitTimer : ITrainTimetableWaitTimer
    {
        public IObservable<Unit> After(TimeSpan delay)
        {
            return Observable.Timer(delay, Scheduler.MainThreadIgnoreTimeScale).AsUnitObservable();
        }
    }
}
```

`Send/TrainTimetableDeparture.cs`:

```csharp
using System.Collections.Generic;
using Client.Ui.State.Train.Logic;
using Core.Update;
using Game.Train.Diagram;
using Game.Train.Unit;

namespace Client.Ui.State.Train.TimetableEdit.Send
{
    /// <summary>
    /// UI が送る停車駅に固定の出発条件を付ける（Web ホスト TrainTimetableStopParser と同じ規則。D-2）
    /// Attaches the fixed departure condition to stops sent from the UI (same rule as the Web host TrainTimetableStopParser; D-2)
    /// </summary>
    public static class TrainTimetableDeparture
    {
        private const TrainDiagram.DepartureConditionType UiFixedDepartureCondition = TrainDiagram.DepartureConditionType.WaitForTicks;

        public static List<TrainTimetableStop> ToServerStops(IReadOnlyList<TimetableReplaceStop> stops)
        {
            var serverStops = new List<TrainTimetableStop>(stops.Count);
            foreach (var stop in stops) serverStops.Add(new TrainTimetableStop(stop.Position, stop.Side, UiFixedDepartureCondition, GameUpdater.TicksPerSecond));
            return serverStops;
        }
    }
}
```

- [ ] **Step 3: 本番のコマンドを書く**

`Send/TrainTimetableCommands.cs`（拒否は理由ごとの `UiCommandRejectReason`。wire は Web ホスト `TrainTimetableActions.cs` の 33・41・49・71・77・81・92 行の接頭辞と同じ綴り〈照合済み〉、`Detail` はサーバーの `FailureReason` だけ。列車が開いていないのは共有の `BlockNotOpen`。D-31・D-32）:

```csharp
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState.State;
using Client.Ui.State.Commands;
using Client.Ui.State.Train.Logic;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Ui.State.Train.TimetableEdit.Send
{
    /// <summary>
    /// 時刻表の取得依頼・置換・自動運転をサーバーへ送る（Web ホスト TrainTimetableActions と同じ規則。D-2）
    /// Sends timetable fetch requests, replaces and auto-run toggles to the server (same rules as the Web host TrainTimetableActions; D-2)
    /// </summary>
    public sealed class TrainTimetableCommands : ITrainTimetableCommands
    {
        private const string LogTag = "TrainTimetableCommands";
        private readonly SubInventoryState _subInventoryState;
        private readonly TrainUnitClientCache _cache;
        private readonly TrainTimetableFetcher _fetcher;

        public TrainTimetableCommands(SubInventoryState subInventoryState, TrainUnitClientCache cache, TrainTimetableFetcher fetcher)
        {
            _subInventoryState = subInventoryState;
            _cache = cache;
            _fetcher = fetcher;
        }

        public void RequestOpen()
        {
            // 列車が引けなければ「列車なしで開いた」と記録する（Web ホスト TrainTimetableOpenActionHandler と同じ State の口）
            // Without a resolvable train, record the tab as opened without one (the same State calls as the Web host TrainTimetableOpenActionHandler)
            if (OpenTrainUnitResolver.TryResolveOpenTrain(_subInventoryState, _cache, out var trainUnitId)) _fetcher.RequestForOpenedTab(trainUnitId);
            else _fetcher.MarkTabOpenedWithoutTrain();
        }

        public async UniTask<UiCommandResult> Replace(IReadOnlyList<TimetableReplaceStop> stops)
        {
            if (!OpenTrainUnitResolver.TryResolveOpenTrain(_subInventoryState, _cache, out var trainUnitId)) return TrainCommandRejection.Reject(LogTag, UiCommandRejectReason.BlockNotOpen, string.Empty);

            // 応答はネットワーク由来なので null（切断・時間切れ）を拒否として扱う
            // The response comes from the network, so null (disconnect or timeout) counts as a refusal
            var request = TrainScheduleEditProtocol.TrainScheduleEditRequest.CreateReplaceTimetableRequest(trainUnitId, TrainTimetableDeparture.ToServerStops(stops));
            var response = await ClientContext.VanillaApi.Response.Train.SendTrainScheduleEdit(request, CancellationToken.None);
            if (response == null || !response.Success) return TrainCommandRejection.Reject(LogTag, UiCommandRejectReason.TrainTimetableReplaceFailed, response?.FailureReason);
            return UiCommandAccepted.Instance;
        }

        public async UniTask<UiCommandResult> SetAutoRun(bool enabled)
        {
            if (!OpenTrainUnitResolver.TryResolveOpenTrain(_subInventoryState, _cache, out var trainUnitId)) return TrainCommandRejection.Reject(LogTag, UiCommandRejectReason.BlockNotOpen, string.Empty);

            // 受理されても求めた値で適用されていなければ拒否（Web ホストと同じ照合）
            // Even when accepted, refuse if the applied value differs from the request (same check as the Web host)
            var request = TrainScheduleEditProtocol.TrainScheduleEditRequest.CreateSetAutoRunRequest(trainUnitId, enabled);
            var response = await ClientContext.VanillaApi.Response.Train.SendTrainScheduleEdit(request, CancellationToken.None);
            if (response == null || !response.Success) return TrainCommandRejection.Reject(LogTag, UiCommandRejectReason.TrainAutoRunFailed, response?.FailureReason);
            if (response.AppliedIsAutoRun == enabled) return UiCommandAccepted.Instance;

            // 適用された値はサーバーの失敗理由ではないので Detail に入れず、ログにだけ残す（Web ホストの set_auto_run_not_applied:<値> と同じ情報）
            // The applied value is not a server failure reason, so it stays out of Detail and goes to the log only (same information as the Web host's set_auto_run_not_applied:<value>)
            Debug.LogWarning($"[{LogTag}] auto-run applied as {response.AppliedIsAutoRun}, requested {enabled}");
            return TrainCommandRejection.Reject(LogTag, UiCommandRejectReason.TrainAutoRunNotApplied, string.Empty);
        }
    }
}
```

- [ ] **Step 4: 編集セッションを書く**

`TrainTimetableEditSession.cs`（保留は private な判別 union 1 本で持ち、null と bool の組で表さない。D-18）:

```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Toast;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.Timetable;
using Client.Ui.State.Train.TimetableEdit.Send;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Client.Ui.State.Train.TimetableEdit
{
    /// <summary>
    /// 時刻表の即時編集。編集を即送り、受理と同じ並びの公開が揃うまで表示を保留する（Web useImmediateTimetable の写し）
    /// Immediate timetable editing: sends each edit at once and holds the display until acceptance and a matching publication (copy of Web useImmediateTimetable)
    /// </summary>
    public sealed class TrainTimetableEditSession
    {
        public const string ReplaceCommandName = "train_timetable.replace";
        public static readonly TimeSpan PublicationTimeout = TimeSpan.FromMilliseconds(5000);

        private readonly ITrainTimetableCommands _commands;
        private readonly ITrainTimetableWaitTimer _timer;
        private readonly UiCommandResultToaster _toaster;
        private readonly Subject<Unit> _onDisplayChanged = new();
        private EditPhase _phase = IdlePhase.Instance;
        private bool _closed;

        // サーバーの最新の時刻表（列車の識別・並び・駅一覧）。セッションと寿命が同じなので一緒に持つ
        // The server's latest timetable (train, order, stations); it shares the session's lifetime so the session holds it
        public TrainTimetableReadyData Server { get; private set; }
        public IReadOnlyList<TimetableEditStop> DisplayedStops { get; private set; }
        public bool IsPending { get; private set; }
        public IObservable<Unit> OnDisplayChanged => _onDisplayChanged;

        public TrainTimetableEditSession(ITrainTimetableCommands commands, ITrainTimetableWaitTimer timer, UiCommandResultToaster toaster, TrainTimetableReadyData server)
        {
            _commands = commands;
            _timer = timer;
            _toaster = toaster;
            Server = server;
            DisplayedStops = server.Stops;
        }

        public void ReceiveServer(TrainTimetableReadyData server)
        {
            // 受理済みの保留は同じ並びの公開が届いた時点で解く
            // An accepted pending edit is released once a matching publication arrives
            Server = server;
            if (_phase is AwaitingPublicationPhase awaiting && TimetableEditLogic.SameStopOrder(awaiting.Stops, server.Stops)) BackToIdle();
            Publish();
        }

        public void Replace(IReadOnlyList<TimetableEditStop> nextStops)
        {
            // 前の編集が終わるまでは古い並びから組んだ編集を受けない
            // Until the previous edit settles, refuse edits built from a stale order
            if (_phase is not IdlePhase)
            {
                Debug.LogWarning("[TrainTimetableEdit] ignored: previous edit is still pending");
                return;
            }
            // silent-ok: 並びが変わらない編集は送る意味が無い / 表示も変わらないので操作した人が見て分かる（Web と同じ）
            if (TimetableEditLogic.SameStopOrder(nextStops, Server.Stops)) return;

            _phase = new SendingPhase(nextStops);
            Publish();
            SendAsync(nextStops).Forget();
        }

        public void Close()
        {
            _closed = true;
            BackToIdle();
        }

        private async UniTaskVoid SendAsync(IReadOnlyList<TimetableEditStop> stops)
        {
            var result = await _commands.Replace(TimetableEditLogic.ToReplacePayload(stops));

            // 閉じた後の結果は別の列車の表示を動かさないよう捨てる
            // Results after close are discarded so they cannot touch another train's display
            if (_closed)
            {
                Debug.LogWarning($"[TrainTimetableEdit] result after close discarded: {result}");
                return;
            }
            if (result is UiCommandRejected) _toaster.Report(ReplaceCommandName, result);

            // 拒否か、公開が先に届いていれば解く。まだなら時間切れを仕掛けて公開を待つ
            // Release on refusal or when the publication already arrived; otherwise arm the timeout and wait
            if (result is UiCommandRejected || TimetableEditLogic.SameStopOrder(stops, Server.Stops)) BackToIdle();
            else ArmAwaitingPublication(stops);
            Publish();
        }

        private void ArmAwaitingPublication(IReadOnlyList<TimetableEditStop> stops)
        {
            // 段階を先に入れてから時間切れを仕掛ける。タイマーが同期で発火しても BackToIdle がこの段階を見て Idle へ戻せる（D-36）
            // Enter the phase before arming the timeout so a synchronously firing timer still finds this phase and returns to Idle (D-36)
            var awaiting = new AwaitingPublicationPhase(stops);
            _phase = awaiting;
            awaiting.Arm(_timer.After(PublicationTimeout).Subscribe(_ => ReleaseMissingPublication()));
        }

        private void ReleaseMissingPublication()
        {
            Debug.LogWarning($"[TrainTimetableEdit] publication did not arrive within {PublicationTimeout.TotalMilliseconds}ms; showing the server order");
            BackToIdle();
            Publish();
        }

        private void BackToIdle()
        {
            if (_phase is AwaitingPublicationPhase awaiting) awaiting.Disarm();
            _phase = IdlePhase.Instance;
        }

        private void Publish()
        {
            DisplayedStops = _phase switch
            {
                SendingPhase sending => sending.Stops,
                AwaitingPublicationPhase awaiting => awaiting.Stops,
                _ => Server.Stops,
            };
            IsPending = _phase is not IdlePhase;
            _onDisplayChanged.OnNext(Unit.Default);
        }

        // 編集の段階: 待ちなし・送信中・受理後の公開待ち
        // Edit phases: idle, sending, and awaiting publication after acceptance
        private abstract class EditPhase { }

        private sealed class IdlePhase : EditPhase
        {
            public static readonly IdlePhase Instance = new();
        }

        private sealed class SendingPhase : EditPhase
        {
            public IReadOnlyList<TimetableEditStop> Stops { get; }

            public SendingPhase(IReadOnlyList<TimetableEditStop> stops)
            {
                Stops = stops;
            }
        }

        private sealed class AwaitingPublicationPhase : EditPhase
        {
            public IReadOnlyList<TimetableEditStop> Stops { get; }
            private IDisposable _timeout = Disposable.Empty;

            public AwaitingPublicationPhase(IReadOnlyList<TimetableEditStop> stops)
            {
                Stops = stops;
            }

            // 段階に入った後で時間切れの購読を受け取る（同期発火で既に Idle なら、受け取る購読は発火済みで捨ててよい）
            // Receives the timeout subscription after the phase is entered (if a synchronous fire already went Idle, the finished subscription is harmless)
            public void Arm(IDisposable timeout)
            {
                _timeout = timeout;
            }

            public void Disarm()
            {
                _timeout.Dispose();
            }
        }
    }
}
```

- [ ] **Step 5: 区画とタブの中身の描画値を書く**

`TrainTimetableSectionViewModel.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.Timetable;

namespace Client.Ui.State.Train.TimetableEdit
{
    /// <summary>
    /// 時刻表の区画に描く値。現在位置は表示中の並びがサーバーと同じときだけ当てる（Web StopList と同じ）
    /// Values drawn by the timetable section; the current row applies only when the displayed order matches the server (same as Web StopList)
    /// </summary>
    public sealed class TrainTimetableSectionViewModel
    {
        public IReadOnlyList<TimetableEditStop> Stops { get; }
        public int? CurrentIndex { get; }
        public IReadOnlyList<TrainTimetableStation> Stations { get; }
        public bool IsPending { get; }
        public bool IsAutoRun { get; }
        public bool CanEnableAutoRun { get; }

        private TrainTimetableSectionViewModel(IReadOnlyList<TimetableEditStop> stops, int? currentIndex, IReadOnlyList<TrainTimetableStation> stations, bool isPending, bool isAutoRun, bool canEnableAutoRun)
        {
            Stops = stops;
            CurrentIndex = currentIndex;
            Stations = stations;
            IsPending = isPending;
            IsAutoRun = isAutoRun;
            CanEnableAutoRun = canEnableAutoRun;
        }

        public static TrainTimetableSectionViewModel Build(TrainTimetableReadyData server, IReadOnlyList<TimetableEditStop> displayedStops, bool isPending)
        {
            // 自動運転の可否は楽観表示でなくサーバーの停車駅で決める（追加が確定するまで ON にできない）
            // Auto-run availability follows the server stops, not the optimistic list, so ON waits until an add is authoritative
            int? currentIndex = TimetableEditLogic.SameStopOrder(displayedStops, server.Stops) ? server.CurrentIndex : null;
            return new TrainTimetableSectionViewModel(displayedStops, currentIndex, server.Stations, isPending, server.IsAutoRun, TimetableEditLogic.CanEnableAutoRun(server.Stops));
        }

        // 同じ中身なら区画を描き直さない（貨車の中身の更新だけで行を作り直さない。R-5）
        // Same contents skip the redraw so a car contents update alone never rebuilds rows (R-5)
        public bool HasSameContent(TrainTimetableSectionViewModel other)
        {
            return CurrentIndex == other.CurrentIndex && IsPending == other.IsPending && IsAutoRun == other.IsAutoRun && CanEnableAutoRun == other.CanEnableAutoRun
                   && Stops.SequenceEqual(other.Stops) && Stations.SequenceEqual(other.Stations);
        }
    }
}
```

`TrainTimetableAreaViewModel.cs`:

```csharp
using Game.Train.Unit;

namespace Client.Ui.State.Train.TimetableEdit
{
    /// <summary>
    /// 時刻表タブの中身。読込中・使えない・列車ごとの区画の 3 つ
    /// Content of the timetable tab: loading, unavailable, or a per-train section
    /// </summary>
    public abstract class TrainTimetableAreaViewModel
    {
        public static readonly TrainTimetableAreaViewModel Loading = new LoadingTrainTimetableArea();
        public static readonly TrainTimetableAreaViewModel Unavailable = new UnavailableTrainTimetableArea();

        public static TrainTimetableAreaViewModel Ready(TrainUnitInstanceId trainUnitInstanceId, TrainTimetableSectionViewModel section)
        {
            return new ReadyTrainTimetableArea(trainUnitInstanceId, section);
        }

        public static bool IsSameArea(TrainTimetableAreaViewModel current, TrainTimetableAreaViewModel next)
        {
            if (current is ReadyTrainTimetableArea currentReady && next is ReadyTrainTimetableArea nextReady)
            {
                return currentReady.IsSameTrain(nextReady) && currentReady.Section.HasSameContent(nextReady.Section);
            }
            return ReferenceEquals(current, next);
        }
    }

    public sealed class LoadingTrainTimetableArea : TrainTimetableAreaViewModel
    {
        internal LoadingTrainTimetableArea() { }
    }

    public sealed class UnavailableTrainTimetableArea : TrainTimetableAreaViewModel
    {
        internal UnavailableTrainTimetableArea() { }
    }

    public sealed class ReadyTrainTimetableArea : TrainTimetableAreaViewModel
    {
        public TrainUnitInstanceId TrainUnitInstanceId { get; }
        public TrainTimetableSectionViewModel Section { get; }

        internal ReadyTrainTimetableArea(TrainUnitInstanceId trainUnitInstanceId, TrainTimetableSectionViewModel section)
        {
            TrainUnitInstanceId = trainUnitInstanceId;
            Section = section;
        }

        // 同じ列車か。View は列車の型（Game.Block.Interface）を参照できないのでこれで区画の作り直しを決める
        // Whether it is the same train; the View cannot reference the train type (Game.Block.Interface), so it decides section rebuilds with this
        public bool IsSameTrain(ReadyTrainTimetableArea other)
        {
            return TrainUnitInstanceId.Equals(other.TrainUnitInstanceId);
        }
    }
}
```

- [ ] **Step 6: 編集の器・描画値の Model・編集のコマンドを書く**

描画値（`Current`）と操作を同じ口に載せず、前例 `PauseMenuStateService`＋`PauseMenuModel`＋`IPauseMenuCommands`（alpha・inventory・S4-machine も同形）と同じく 3 つに分ける。列車ごとの編集セッションは「操作が書き換え、描画値が読む」共有の状態なので、Model と Commands のどちらにも置かず器 `TrainTimetableEditState` に置く（D-33）。

`TrainTimetableEditState.cs`:

```csharp
using System;
using Client.Ui.State.Toast;
using Client.Ui.State.Train.Timetable;
using Client.Ui.State.Train.TimetableEdit.Send;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Train.TimetableEdit
{
    /// <summary>
    /// 列車ごとの編集セッションの器。描画値の Model が読み、編集のコマンドが書き換える（前例 PauseMenuStateService）
    /// Holder of the per-train edit session; the draw-value model reads it and the edit commands mutate it (precedent PauseMenuStateService)
    /// </summary>
    public sealed class TrainTimetableEditState : IInitializable
    {
        private readonly ITrainTimetableSource _source;
        private readonly ITrainTimetableCommands _commands;
        private readonly ITrainTimetableWaitTimer _timer;
        private readonly UiCommandResultToaster _toaster;
        private readonly Subject<Unit> _onChanged = new();
        private readonly SerialDisposable _sessionSubscription = new();
        private TrainTimetableEditSession _session;

        // セッションが無いあいだ読込中か（偽なら使えない）。源の最新の状態から決める
        // Whether it is loading while no session exists (false means unavailable), from the source's latest state
        public bool IsLoading { get; private set; }

        // セッションの開閉・表示の変化・源の状態の変化のたびに流す
        // Fires on session open/close, display changes and source state changes
        public IObservable<Unit> OnChanged => _onChanged;

        public TrainTimetableEditState(ITrainTimetableSource source, ITrainTimetableCommands commands, ITrainTimetableWaitTimer timer, UiCommandResultToaster toaster)
        {
            _source = source;
            _commands = commands;
            _timer = timer;
            _toaster = toaster;
        }

        public void Initialize()
        {
            _source.Current.Subscribe(Receive);
        }

        // 開いている列車のセッション。時刻表が開いていなければ偽
        // The open train's session; false when no timetable is open
        public bool TryGetSession(out TrainTimetableEditSession session)
        {
            session = _session;
            return _session != null;
        }

        private void Receive(TrainTimetableViewState state)
        {
            if (state is ReadyTrainTimetableViewState ready)
            {
                ReceiveReady(ready.Data);
                return;
            }
            CloseSession();
            IsLoading = state is LoadingTrainTimetableViewState;
            _onChanged.OnNext(Unit.Default);
        }

        private void ReceiveReady(TrainTimetableReadyData data)
        {
            // 列車が替わったら編集を作り直し（Web の key=trainUnitId）、同じ列車ならサーバーの並びを編集へ渡す（表示の変化はセッションの通知で流れる）
            // A different train rebuilds the session (Web key=trainUnitId); the same train hands the server order to the session (its notification carries the change)
            if (_session == null || !_session.Server.TrainUnitInstanceId.Equals(data.TrainUnitInstanceId))
            {
                OpenSession(data);
                return;
            }
            _session.ReceiveServer(data);
        }

        private void OpenSession(TrainTimetableReadyData data)
        {
            CloseSession();
            _session = new TrainTimetableEditSession(_commands, _timer, _toaster, data);
            _sessionSubscription.Disposable = _session.OnDisplayChanged.Subscribe(_ => _onChanged.OnNext(Unit.Default));
            _onChanged.OnNext(Unit.Default);
        }

        private void CloseSession()
        {
            _sessionSubscription.Disposable = null;
            _session?.Close();
            _session = null;
        }
    }
}
```
（`_session` の null は「開いていない」を表す内部の寿命の分岐で、外に出す口は `TryGetSession` だけ。サーバーの時刻表はセッションが持つので、組で立てる・倒すフィールドは無い〈レビュー r2 の 5-A1〉）

`ITrainTimetableEditModel.cs`:

```csharp
using UniRx;

namespace Client.Ui.State.Train.TimetableEdit
{
    /// <summary>
    /// 時刻表タブの描画値。Presenter が購読して View へ流す（操作は ITrainTimetableEditCommands）
    /// Draw values of the timetable tab; the Presenter subscribes them into the View (operations live in ITrainTimetableEditCommands)
    /// </summary>
    public interface ITrainTimetableEditModel
    {
        IReadOnlyReactiveProperty<TrainTimetableAreaViewModel> Current { get; }
    }
}
```

`TrainTimetableEditModel.cs`:

```csharp
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Train.TimetableEdit
{
    /// <summary>
    /// 編集の器を購読し、時刻表タブの描画値（読込中・使えない・列車ごとの区画）を配る
    /// Subscribes to the edit holder and publishes the timetable tab's draw values (loading, unavailable or a per-train section)
    /// </summary>
    public sealed class TrainTimetableEditModel : ITrainTimetableEditModel, IInitializable
    {
        private readonly TrainTimetableEditState _state;
        private readonly ReactiveProperty<TrainTimetableAreaViewModel> _current = new(TrainTimetableAreaViewModel.Unavailable);

        public IReadOnlyReactiveProperty<TrainTimetableAreaViewModel> Current => _current;

        public TrainTimetableEditModel(TrainTimetableEditState state)
        {
            _state = state;
        }

        public void Initialize()
        {
            // 器の Initialize が先でも後でも、購読の後に 1 回組めば最新を配れる
            // Whether the holder initializes first or later, building once after subscribing publishes the latest
            _state.OnChanged.Subscribe(_ => Rebuild());
            Rebuild();
        }

        private void Rebuild()
        {
            var next = Build();
            // silent-ok: 同じ中身は配り直さない / 表示が変わらないので見ている人に差は無い（R-5）
            if (TrainTimetableAreaViewModel.IsSameArea(_current.Value, next)) return;
            _current.Value = next;
        }

        private TrainTimetableAreaViewModel Build()
        {
            if (!_state.TryGetSession(out var session)) return _state.IsLoading ? TrainTimetableAreaViewModel.Loading : TrainTimetableAreaViewModel.Unavailable;
            var section = TrainTimetableSectionViewModel.Build(session.Server, session.DisplayedStops, session.IsPending);
            return TrainTimetableAreaViewModel.Ready(session.Server.TrainUnitInstanceId, section);
        }
    }
}
```

`ITrainTimetableEditCommands.cs`:

```csharp
using Client.Ui.State.Commands;
using Client.Ui.State.Train.Logic;

namespace Client.Ui.State.Train.TimetableEdit
{
    /// <summary>
    /// 時刻表の編集の操作。View が直接呼ぶ（設計書 A-8。Presenter は中継しない）。結果のトーストはこの口の実装が済ませる
    /// Timetable edit operations; the View calls them directly (design doc A-8; no Presenter relay) and the implementation reports results to the toaster
    /// </summary>
    public interface ITrainTimetableEditCommands
    {
        UiCommandResult Add(TrainTimetableStation station);
        UiCommandResult Move(int index, StopMoveDirection direction);
        UiCommandResult Remove(int index);
        UiCommandResult SetAutoRun(bool enabled);
    }
}
```

`TrainTimetableEditCommands.cs`:

```csharp
using Client.Ui.State.Commands;
using Client.Ui.State.Toast;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.TimetableEdit.Send;
using Cysharp.Threading.Tasks;

namespace Client.Ui.State.Train.TimetableEdit
{
    /// <summary>
    /// 時刻表の編集の操作。開いているセッションへ渡すか自動運転を送り、開いていなければ拒否してトーストへ
    /// Timetable edit operations: hand edits to the open session or send auto-run, and refuse with a toast when none is open
    /// </summary>
    public sealed class TrainTimetableEditCommands : ITrainTimetableEditCommands
    {
        public const string SetAutoRunCommandName = "train_timetable.set_auto_run";
        private const string LogTag = "TrainTimetableEdit";
        private readonly TrainTimetableEditState _state;
        private readonly ITrainTimetableCommands _commands;
        private readonly UiCommandResultToaster _toaster;

        public TrainTimetableEditCommands(TrainTimetableEditState state, ITrainTimetableCommands commands, UiCommandResultToaster toaster)
        {
            _state = state;
            _commands = commands;
            _toaster = toaster;
        }

        // 受理は「編集をセッションへ渡した」の意味。保留中で捨てた編集・並びの同じ編集もセッションがログつきで扱う（Web も知らせない）。サーバーの結果はセッションがトーストへ渡す
        // Accepted means "handed to the session"; edits dropped while pending or with the same order are logged by the session (Web does not notify either), and the session reports the server result
        public UiCommandResult Add(TrainTimetableStation station)
        {
            if (!_state.TryGetSession(out var session)) return NotOpen(TrainTimetableEditSession.ReplaceCommandName);
            session.Replace(TimetableEditLogic.AddStop(session.DisplayedStops, station));
            return UiCommandAccepted.Instance;
        }

        public UiCommandResult Move(int index, StopMoveDirection direction)
        {
            if (!_state.TryGetSession(out var session)) return NotOpen(TrainTimetableEditSession.ReplaceCommandName);
            session.Replace(TimetableEditLogic.MoveStop(session.DisplayedStops, index, direction));
            return UiCommandAccepted.Instance;
        }

        public UiCommandResult Remove(int index)
        {
            if (!_state.TryGetSession(out var session)) return NotOpen(TrainTimetableEditSession.ReplaceCommandName);
            session.Replace(TimetableEditLogic.RemoveStop(session.DisplayedStops, index));
            return UiCommandAccepted.Instance;
        }

        // 自動運転は楽観表示しない。受理は「送った」の意味で、結果は公開で反映され、拒否だけトーストへ
        // Auto-run is never shown optimistically; accepted means "sent", the publication reflects the result and only refusals toast
        public UiCommandResult SetAutoRun(bool enabled)
        {
            if (!_state.TryGetSession(out _)) return NotOpen(SetAutoRunCommandName);
            SendAutoRunAsync(enabled).Forget();
            return UiCommandAccepted.Instance;
        }

        // 時刻表が開いていない操作は送らず、Web ホストが train_not_open を返してトーストするのと同じく拒否をトーストへ（D-32）
        // An operation without an open timetable is not sent and its refusal is toasted, as the Web host's train_not_open is (D-32)
        private UiCommandResult NotOpen(string commandName)
        {
            var rejected = TrainCommandRejection.Reject(LogTag, UiCommandRejectReason.BlockNotOpen, string.Empty);
            _toaster.Report(commandName, rejected);
            return rejected;
        }

        private async UniTaskVoid SendAutoRunAsync(bool enabled)
        {
            var result = await _commands.SetAutoRun(enabled);
            _toaster.Report(SetAutoRunCommandName, result);
        }
    }
}
```
（結果のトーストを View でなくコマンドが済ませるのは、置換の結果がセッション〈State〉の中でしか分からず〈拒否の巻き戻しと同じ所で報告する〉、自動運転・開いていない拒否も同じ 1 か所に揃えるため。S4-machine D-33〈View がトースト係へ渡す〉との違いは D-33 に書いた）

- [ ] **Step 7: テストを回してコミット**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainTimetableEditSessionTest|TrainTimetableDepartureTest|TrainTimetableEditModelTest"`
Expected: `ErrorCount: 0`、10 件 PASS。`wc -l` で `TimetableEdit/` と `TimetableEdit/Send/` がどちらも 10 本以下・各 200 行以下。`UniTaskCompletionSource.TrySetResult` の継続が同期で走らずに `Accept()` 直後の主張が落ちるなら、テスト側で `await UniTask.Yield()` を挟む `[UnityTest]` に替える（本体のコードは替えない）

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Train/TimetableEdit moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Train
git commit -m "feat(uitk): 時刻表の即時編集・送信・編集の器と Model とコマンドを足す（S4-train）

<実行ホストの指示する帰属行>"
```

---

### Task 5: 貨車とプラットフォームの Model（`Client.Ui.State/Train/Inventory/`・`Platform/`・`Platform/Edit/`）

**Files:**
- Create: `S/Client.Ui.State/Train/Inventory/` の 6 本、`S/Client.Ui.State/Train/Platform/` の 7 本、`S/Client.Ui.State/Train/Platform/Edit/` の 8 本
- Create: `S/Client.Tests/Ui/State/Train/Inventory/TrainTimetableFetchTriggerTest.cs`、`S/Client.Tests/Ui/State/Train/Platform/TrainStationNameDraftTest.cs`・`TrainPlatformDetailReaderTest.cs`・`TrainPlatformEditModelTest.cs`、`S/Client.Tests/Ui/State/Train/Fakes/FakeTrainBlockCommands.cs`・`TrainTestPlatforms.cs`（タブの局所状態は区画の Presenter が持つので、その L1 は Task 7 の `TrainInventoryBodyTest` に置く。D-33）
- Modify: `S/Client.Tests/Ui/State/Train/Fakes/FakeTrainSources.cs`（貨車・プラットフォームの口を足す）

**Interfaces:**
- Consumes: alpha の `IBlockInventoryPanelSource`・`OpenTrainInventoryPanel`・`OpenBlockInventoryPanel`（`ItemSlots`・`Error`・`Components.Body`・`BlockType`）・`BlockInventoryPanelState.IsSamePanel`、L0-s4b の `BlockComponentRegistry`・`BlockInventoryBodyKind.TrainPlatform`・`BlockFluidSlotState`、S4-machine の `IBlockFluidSlotsModel.Current`（`BlockFluidSlotsViewModel.Slots`。液体 PF は追跡ブロックの `FluidMachineInventoryStateDetail` から入る。D-17。追跡替え・状態変化のたびに配り直す前提は Task 1 Step 2 で確かめる。D-35）・`SectionContent<T>`（`Client.Ui.State.BlockInventory.Common`）と `BlockFluidSlotConversion.FromTanks`、`OpenBlockDetailTracker`、`SubInventoryState`、Task 3 の `ITrainTimetableSource`
- Produces:
```csharp
namespace Client.Ui.State.Train.Inventory
public enum TrainInventoryTab { Inventory, Timetable }
public sealed class TrainTimetableFetchTrigger { public TrainInventoryTab Tab { get; } public bool SelectTab(TrainInventoryTab tab); public bool ReceiveState(TrainTimetableViewState state); }   // タブの局所状態と取得の契機。列車の本文の Presenter が 1 つ持ち、別のパネルで作り直す（D-33）
public sealed class TrainInventoryBodyViewModel { public static readonly TrainInventoryBodyViewModel Closed; public IReadOnlyList<SlotItem> ItemSlots { get; } public IReadOnlyList<BlockFluidSlotState> Fluids { get; } }
public interface ITrainInventoryBodySource { IReadOnlyReactiveProperty<TrainInventoryBodyViewModel> Current { get; } }
namespace Client.Ui.State.Train.Platform
public enum TrainPlatformTransferMode { LoadToTrain, UnloadToPlatform }
public static class TrainPlatformTransferModeConversion { FromServer(TrainPlatformTransferComponent.TransferMode); ToServer(TrainPlatformTransferMode); }
public abstract class TrainPlatformBodyState { public static readonly TrainPlatformBodyState Closed; }
public sealed class OpenTrainPlatformBody : TrainPlatformBodyState { SectionContent<TrainStationNamePart> StationName /* 駅だけ Present */; SectionContent<IReadOnlyList<SlotItem>> ItemSlots /* 移送の状態がある品物 PF・駅だけ Present */; IReadOnlyList<BlockFluidSlotState> Fluids; SectionContent<TrainPlatformTransferMode> Mode /* 移送の状態があるときだけ Present */; }   // D-19・D-34
public sealed class TrainStationNamePart { public string SyncedName { get; } }
public interface ITrainPlatformBodySource { IReadOnlyReactiveProperty<TrainPlatformBodyState> Current { get; } }
public static class TrainPlatformDetailReader { public static OpenTrainPlatformBody Read(string blockType, IBlockParam param, TrainStationNameStateDetail stationName, TrainPlatformTransferStateDetail transfer, IReadOnlyList<SlotItem> itemSlots, IReadOnlyList<BlockFluidSlotState> fluids); }
namespace Client.Ui.State.Train.Platform.Edit
public interface ITrainBlockCommands { UniTask<UiCommandResult> SetStationName(string name); UniTask<UiCommandResult> SetTransferMode(TrainPlatformTransferMode mode); }
public sealed class TrainStationNameDraft { string Input; void Sync(string syncedName); void Edit(string input); void Reject(); }
public sealed class TrainStationNameEditState : IInitializable { IReadOnlyReactiveProperty<string> Input; TrainStationNameDraft Draft; void Edit(string input); void RevertIfCurrent(TrainStationNameDraft draftAtSend); }   // 駅名の下書きの器（Model と Commands が共有。D-33）
public interface ITrainPlatformEditModel { IReadOnlyReactiveProperty<string> StationNameInput { get; } }   // 描画値だけ（Presenter が購読）
public interface ITrainPlatformEditCommands { void EditStationName(string input); UiCommandResult ApplyStationName(); UiCommandResult SelectTransferMode(TrainPlatformTransferMode mode); }   // View が直接呼ぶ（A-8）。サーバーの結果のトーストは実装が済ませる
```

- [ ] **Step 1: 偽物と失敗するテストを書く**

`FakeTrainSources.cs` を 3 つの源の偽物にする（Task 4 の時刻表の口に貨車・プラットフォームの口を足す）:

```csharp
using Client.Ui.State.Train.Inventory;
using Client.Ui.State.Train.Platform;
using Client.Ui.State.Train.Timetable;
using UniRx;

namespace Client.Tests.Ui.State.Train.Fakes
{
    /// <summary>
    /// 3 つの源の偽物。値はテストが Set で押し込む
    /// Fakes of the three sources; tests push values with Set
    /// </summary>
    public sealed class FakeTrainSources : ITrainTimetableSource, ITrainInventoryBodySource, ITrainPlatformBodySource
    {
        private readonly ReactiveProperty<TrainTimetableViewState> _timetable = new(TrainTimetableViewState.Loading);
        private readonly ReactiveProperty<TrainInventoryBodyViewModel> _body = new(TrainInventoryBodyViewModel.Closed);
        private readonly ReactiveProperty<TrainPlatformBodyState> _platform = new(TrainPlatformBodyState.Closed);

        IReadOnlyReactiveProperty<TrainTimetableViewState> ITrainTimetableSource.Current => _timetable;
        IReadOnlyReactiveProperty<TrainInventoryBodyViewModel> ITrainInventoryBodySource.Current => _body;
        IReadOnlyReactiveProperty<TrainPlatformBodyState> ITrainPlatformBodySource.Current => _platform;

        public void SetTimetable(TrainTimetableViewState state)
        {
            _timetable.Value = state;
        }

        public void SetBody(TrainInventoryBodyViewModel body)
        {
            _body.Value = body;
        }

        public void SetPlatform(TrainPlatformBodyState platform)
        {
            _platform.Value = platform;
        }
    }
}
```

`FakeTrainBlockCommands.cs`:

```csharp
using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Train.Platform;
using Client.Ui.State.Train.Platform.Edit;
using Cysharp.Threading.Tasks;

namespace Client.Tests.Ui.State.Train.Fakes
{
    /// <summary>
    /// 駅名・モードの送信を記録し、結果はテストが手で返す
    /// Records station name and mode sends; tests resolve the results by hand
    /// </summary>
    public sealed class FakeTrainBlockCommands : ITrainBlockCommands
    {
        public List<string> SentNames { get; } = new();
        public List<TrainPlatformTransferMode> SentModes { get; } = new();
        private readonly Queue<UniTaskCompletionSource<UiCommandResult>> _pending = new();

        public UniTask<UiCommandResult> SetStationName(string name)
        {
            SentNames.Add(name);
            return Enqueue();
        }

        public UniTask<UiCommandResult> SetTransferMode(TrainPlatformTransferMode mode)
        {
            SentModes.Add(mode);
            return Enqueue();
        }

        public void Accept()
        {
            _pending.Dequeue().TrySetResult(UiCommandAccepted.Instance);
        }

        // 理由ごとの拒否。Detail はサーバーの FailureReason の写し（無ければ空文字。D-31）
        // Per-reason refusal; Detail mirrors the server FailureReason (empty when absent; D-31)
        public void Reject(UiCommandRejectReason reason, string serverFailureReason)
        {
            _pending.Dequeue().TrySetResult(new UiCommandRejected(reason, serverFailureReason));
        }

        private UniTask<UiCommandResult> Enqueue()
        {
            var source = new UniTaskCompletionSource<UiCommandResult>();
            _pending.Enqueue(source);
            return source.Task;
        }
    }
}
```

`TrainTimetableFetchTriggerTest.cs`:

```csharp
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.State.Train.Inventory;
using Client.Ui.State.Train.Timetable;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Train.Inventory
{
    public class TrainTimetableFetchTriggerTest
    {
        [Test]
        public void 時刻表タブへ移った瞬間と時刻表タブで読込中が届いたときだけ依頼する()
        {
            var trigger = new TrainTimetableFetchTrigger();

            // 持ち物タブの読込中では依頼しない
            // Loading on the inventory tab does not ask
            Assert.IsFalse(trigger.ReceiveState(TrainTimetableViewState.Loading));
            Assert.IsTrue(trigger.SelectTab(TrainInventoryTab.Timetable));
            Assert.IsFalse(trigger.SelectTab(TrainInventoryTab.Timetable));

            // 時刻表タブでは読込中が届くたびに依頼し、用意済みでは依頼しない
            // On the timetable tab every loading arrival asks, ready does not
            Assert.IsTrue(trigger.ReceiveState(TrainTimetableViewState.Loading));
            Assert.IsFalse(trigger.ReceiveState(TrainTimetableViewState.Ready(TrainTestData.NorthOnly(TrainTestData.Unit1))));
            Assert.IsFalse(trigger.SelectTab(TrainInventoryTab.Inventory));
        }
    }
}
```

`TrainStationNameDraftTest.cs`:

```csharp
using Client.Ui.State.Train.Platform.Edit;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Train.Platform
{
    public class TrainStationNameDraftTest
    {
        [Test]
        public void 手を付けていない入力は同期した名前に従い拒否で同期済みへ戻る()
        {
            var draft = new TrainStationNameDraft();
            draft.Sync("北駅");
            Assert.AreEqual("北駅", draft.Input);

            // 編集中は同期で上書きしない
            // While edited, a sync does not overwrite
            draft.Edit("南駅");
            draft.Sync("東駅");
            Assert.AreEqual("南駅", draft.Input);

            // 拒否は最新の同期済みへ戻し、以後は同期に従う
            // Rejection reverts to the latest synced name and follows syncs again
            draft.Reject();
            Assert.AreEqual("東駅", draft.Input);
            draft.Sync("西駅");
            Assert.AreEqual("西駅", draft.Input);

            // 決定した名前が同期されれば、その後の同期にも従う
            // Once the applied name syncs back, later syncs are followed too
            draft.Edit("中央駅");
            draft.Sync("中央駅");
            draft.Sync("中央駅2");
            Assert.AreEqual("中央駅2", draft.Input);
        }
    }
}
```

`TrainPlatformEditModelTest.cs`（送った回の下書きだけを巻き戻す。D-25。器・Model・コマンドを本番どおり組む。D-33）:

```csharp
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Commands;
using Client.Ui.State.Items;
using Client.Ui.State.Toast;
using Client.Ui.State.Train.Platform;
using Client.Ui.State.Train.Platform.Edit;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Client.Tests.Ui.State.Train.Platform
{
    public class TrainPlatformEditModelTest
    {
        [Test]
        public void 閉じて同じ駅を開き直した後に戻った拒否は新しい入力を消さない()
        {
            var panels = new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.OfType(BlockTypeConst.TrainStation)));
            var sources = new FakeTrainSources();
            sources.SetPlatform(TrainTestPlatforms.StationBody("北駅", new List<SlotItem>(), new List<BlockFluidSlotState>(), TrainPlatformTransferMode.LoadToTrain));
            var commands = new FakeTrainBlockCommands();
            var state = new TrainStationNameEditState(panels, sources);
            var model = new TrainPlatformEditModel(state);
            var edit = new TrainPlatformEditCommands(state, commands, new UiCommandResultToaster(new GameUiToastFeed()));
            state.Initialize();

            // 駅で送ってから閉じ、同じ駅を開き直して打ち直す（パネルの識別は同じ駅なので IsSamePanel では見分けられない）
            // Send, close, reopen the same station and type again (IsSamePanel cannot tell it apart: same station)
            edit.EditStationName("南駅");
            Assert.IsInstanceOf<UiCommandAccepted>(edit.ApplyStationName());
            panels.State.Value = BlockInventoryPanelState.Closed;
            panels.State.Value = BlockInventoryTestPanels.OfType(BlockTypeConst.TrainStation);
            edit.EditStationName("東駅");

            // 前の回の拒否はトーストとログだけで、新しい入力は打った値のまま
            // The earlier refusal only toasts and logs; the new input keeps the typed value
            LogAssert.Expect(LogType.Warning, new Regex(@"train_station\.set_name failed"));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[TrainStationName\] set_name refused after the draft was replaced"));
            commands.Reject(UiCommandRejectReason.TrainStationNameFailed, "busy");
            Assert.AreEqual("東駅", model.StationNameInput.Value);
        }
    }
}
```

`TrainTestPlatforms.cs`（駅・PF の本文の描画値を組む補助。部分の有無を `SectionContent<T>` で書く手間をテストから外す）:

```csharp
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.Items;
using Client.Ui.State.Train.Platform;

namespace Client.Tests.Ui.State.Train.Fakes
{
    /// <summary>
    /// 駅・品物 PF・液体 PF の開いた本文を、Reader と同じ部分の有無で組む
    /// Builds open station, item platform and fluid platform bodies with the same part presence as the Reader
    /// </summary>
    public static class TrainTestPlatforms
    {
        public static OpenTrainPlatformBody StationBody(string syncedName, IReadOnlyList<SlotItem> slots, IReadOnlyList<BlockFluidSlotState> fluids, TrainPlatformTransferMode mode)
        {
            return new OpenTrainPlatformBody(SectionContent<TrainStationNamePart>.Present(new TrainStationNamePart(syncedName)), SectionContent<IReadOnlyList<SlotItem>>.Present(slots), fluids, SectionContent<TrainPlatformTransferMode>.Present(mode));
        }

        public static OpenTrainPlatformBody ItemPlatformBody(IReadOnlyList<SlotItem> slots, IReadOnlyList<BlockFluidSlotState> fluids, TrainPlatformTransferMode mode)
        {
            return new OpenTrainPlatformBody(SectionContent<TrainStationNamePart>.Absent, SectionContent<IReadOnlyList<SlotItem>>.Present(slots), fluids, SectionContent<TrainPlatformTransferMode>.Present(mode));
        }

        public static OpenTrainPlatformBody FluidPlatformBody(IReadOnlyList<BlockFluidSlotState> fluids, TrainPlatformTransferMode mode)
        {
            return new OpenTrainPlatformBody(SectionContent<TrainStationNamePart>.Absent, SectionContent<IReadOnlyList<SlotItem>>.Absent, fluids, SectionContent<TrainPlatformTransferMode>.Present(mode));
        }
    }
}
```

`TrainPlatformDetailReaderTest.cs`（駅・品物 PF・液体 PF・状態未着の 4 行。Web ホストの `TrainStationDetailDtoBuilder`・`TrainPlatformDetailDtoBuilder` と同じ規則）:

```csharp
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.Items;
using Client.Ui.State.Train.Platform;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using NUnit.Framework;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Client.Tests.Ui.State.Train.Platform
{
    public class TrainPlatformDetailReaderTest
    {
        private static readonly IReadOnlyList<SlotItem> Slots = new[] { new SlotItem(new ItemId(3), 12) };
        private static readonly IReadOnlyList<BlockFluidSlotState> NoFluids = new List<BlockFluidSlotState>();
        private static readonly TrainPlatformTransferStateDetail Unload = new(TrainPlatformTransferComponent.TransferMode.UnloadToPlatform);

        [Test]
        public void 駅は名前と枠とモードを持つ()
        {
            var body = TrainPlatformDetailReader.Read(BlockTypeConst.TrainStation, TestBlockParams.Station(), new TrainStationNameStateDetail("北駅"), Unload, Slots, NoFluids);
            Assert.AreEqual("北駅", body.StationName.Value.SyncedName);
            Assert.AreSame(Slots, body.ItemSlots.Value);
            Assert.AreEqual(TrainPlatformTransferMode.UnloadToPlatform, body.Mode.Value);
        }

        [Test]
        public void 品物PFは名前を持たず枠とモードを持つ()
        {
            var body = TrainPlatformDetailReader.Read(BlockTypeConst.TrainItemPlatform, TestBlockParams.ItemPlatform(), null, Unload, Slots, NoFluids);
            Assert.IsFalse(body.StationName.IsPresent);
            Assert.AreSame(Slots, body.ItemSlots.Value);
        }

        [Test]
        public void 液体PFは枠を持たず液体とモードを持つ()
        {
            var fluids = new[] { BlockFluidSlotState.Empty(1000) };
            var body = TrainPlatformDetailReader.Read(BlockTypeConst.TrainFluidPlatform, TestBlockParams.FluidPlatform(), null, Unload, Slots, fluids);
            Assert.IsFalse(body.ItemSlots.IsPresent);
            Assert.AreSame(fluids, body.Fluids);
            Assert.AreEqual(TrainPlatformTransferMode.UnloadToPlatform, body.Mode.Value);
        }

        [Test]
        public void 移送の状態が届く前は枠もモードも出さず駅名は空()
        {
            // Web ホストは状態が無ければ詳細ごと出さない。駅名の StateDetail が無ければ空名
            // The Web host omits the detail without a state; a missing name StateDetail gives an empty name
            var body = TrainPlatformDetailReader.Read(BlockTypeConst.TrainStation, TestBlockParams.Station(), null, null, Slots, NoFluids);
            Assert.AreEqual(string.Empty, body.StationName.Value.SyncedName);
            Assert.IsFalse(body.ItemSlots.IsPresent);
            Assert.IsFalse(body.Mode.IsPresent);
        }
    }
}
```
（`TestBlockParams` は各 `BlockParam` をテスト用 mod のブロックマスタから引く小さな補助を同じファイルの下に置く。`MasterHolder.BlockMaster` から `BlockType` で最初の 1 つを取り `BlockParam` を返す。`TrainStationNameStateDetail(string)`・`TrainPlatformTransferStateDetail(TransferMode)` の ctor は実物で確かめた）

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainTimetableFetchTriggerTest|TrainStationNameDraftTest|TrainPlatformDetailReaderTest|TrainPlatformEditModelTest"`
Expected: コンパイルエラー（型が無い）

- [ ] **Step 2: 貨車側の値と源を書く**

`TrainInventoryTab.cs`:

```csharp
namespace Client.Ui.State.Train.Inventory
{
    /// <summary>
    /// 列車の本文のタブ
    /// Tabs of the train body
    /// </summary>
    public enum TrainInventoryTab
    {
        Inventory,
        Timetable,
    }
}
```

`TrainTimetableFetchTrigger.cs`:

```csharp
using Client.Ui.State.Train.Timetable;

namespace Client.Ui.State.Train.Inventory
{
    /// <summary>
    /// 時刻表の取得を頼む契機を決める（Web TrainInventoryBody の effect の写し。重複の依頼は Fetcher が畳む）
    /// Decides when to ask for a timetable fetch (copy of the Web TrainInventoryBody effect; the Fetcher folds duplicates)
    /// </summary>
    public sealed class TrainTimetableFetchTrigger
    {
        public TrainInventoryTab Tab { get; private set; } = TrainInventoryTab.Inventory;

        // 時刻表タブへ移った瞬間だけ真。用意済みでも頼み直す
        // True only on the switch into the timetable tab, even when already ready
        public bool SelectTab(TrainInventoryTab tab)
        {
            var enteredTimetable = tab == TrainInventoryTab.Timetable && Tab != TrainInventoryTab.Timetable;
            Tab = tab;
            return enteredTimetable;
        }

        // 時刻表タブのあいだに読込中（取得のやり直し）が届いたら頼み直す
        // While on the timetable tab, a loading state (a reset fetch) asks again
        public bool ReceiveState(TrainTimetableViewState state)
        {
            return Tab == TrainInventoryTab.Timetable && state is LoadingTrainTimetableViewState;
        }
    }
}
```

`TrainInventoryBodyViewModel.cs`:

```csharp
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.Items;

namespace Client.Ui.State.Train.Inventory
{
    /// <summary>
    /// 開いている貨車の中身。液体貨車は Fluids、それ以外は ItemSlots を描く
    /// Contents of the open car; fluid cars draw Fluids, others draw ItemSlots
    /// </summary>
    public sealed class TrainInventoryBodyViewModel
    {
        public static readonly TrainInventoryBodyViewModel Closed = new(new List<SlotItem>(), new List<BlockFluidSlotState>());

        public IReadOnlyList<SlotItem> ItemSlots { get; }
        public IReadOnlyList<BlockFluidSlotState> Fluids { get; }

        public TrainInventoryBodyViewModel(IReadOnlyList<SlotItem> itemSlots, IReadOnlyList<BlockFluidSlotState> fluids)
        {
            ItemSlots = itemSlots;
            Fluids = fluids;
        }
    }
}
```
（列車の識別は alpha のパネルの状態〈`OpenTrainInventoryPanel.TrainCarInstanceId`〉が持つので、ここには置かない。D-19）

`ITrainInventoryBodySource.cs`:

```csharp
using UniRx;

namespace Client.Ui.State.Train.Inventory
{
    /// <summary>
    /// 開いている貨車の中身の口
    /// Source of the open car's contents
    /// </summary>
    public interface ITrainInventoryBodySource
    {
        IReadOnlyReactiveProperty<TrainInventoryBodyViewModel> Current { get; }
    }
}
```

`TrainCarFluidConversion.cs`:

```csharp
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Fluid;
using Client.Ui.State.BlockInventory.Logic.Common;
using Game.Block.Interface.State;

namespace Client.Ui.State.Train.Inventory
{
    /// <summary>
    /// 貨車の液体を描画値へ写す。S4-machine のタンクの写しを入力側だけで使い、写しを 2 か所に持たない
    /// Maps car fluids to draw values by reusing the S4-machine tank conversion with inputs only, so the mapping lives in one place
    /// </summary>
    public static class TrainCarFluidConversion
    {
        public static IReadOnlyList<BlockFluidSlotState> ToFluids(IReadOnlyList<FluidMessagePack> fluids)
        {
            return BlockFluidSlotConversion.FromTanks(fluids, new List<FluidMessagePack>());
        }
    }
}
```

`TrainInventoryBodyModel.cs`（スロットは alpha のパネルの状態から読み、`IItemStack → SlotItem` の写しを持たない。D-16）:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.UI.UIState.State;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Panel;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Train.Inventory
{
    /// <summary>
    /// 開いている貨車の中身を配る。スロットはパネルの状態、液体はサブインベントリから
    /// Publishes the open car's contents: slots from the panel state, fluids from the sub-inventory
    /// </summary>
    public sealed class TrainInventoryBodyModel : ITrainInventoryBodySource, IInitializable
    {
        private readonly IBlockInventoryPanelSource _panels;
        private readonly SubInventoryState _subInventoryState;
        private readonly ReactiveProperty<TrainInventoryBodyViewModel> _current = new(TrainInventoryBodyViewModel.Closed);

        public IReadOnlyReactiveProperty<TrainInventoryBodyViewModel> Current => _current;

        public TrainInventoryBodyModel(IBlockInventoryPanelSource panels, SubInventoryState subInventoryState)
        {
            _panels = panels;
            _subInventoryState = subInventoryState;
        }

        public void Initialize()
        {
            _panels.Current.Subscribe(_ => Rebuild());
            _subInventoryState.OnSubInventoryUpdated.Subscribe(_ => Rebuild());
            Rebuild();
        }

        private void Rebuild()
        {
            // エラー無しの列車のパネルのあいだだけ中身を配る（エラー行は alpha が出す）
            // Contents publish only while an error-free train panel is open (alpha shows the error row)
            if (_panels.Current.Value is not OpenTrainInventoryPanel { Error: BlockInventoryPanelError.None } open)
            {
                _current.Value = TrainInventoryBodyViewModel.Closed;
                return;
            }
            var fluids = _subInventoryState.CurrentSubInventory?.Fluids;
            // silent-ok: 列車を開いた直後は液体の応答がまだ無く、液体なしで描いて届いた OnSubInventoryUpdated で描き直す / 応答が来ないままなら液体貨車の行が出ず、録画 train/fluid-inventory-via-interact の液体の主張が落ちて開発者が気づく
            _current.Value = new TrainInventoryBodyViewModel(open.ItemSlots, fluids == null ? new List<BlockFluidSlotState>() : TrainCarFluidConversion.ToFluids(fluids));
        }
    }
}
```
（`CurrentSubInventory` はサーバーの応答から組まれる外部データなので null を見る。パネルが列車を開いた直後に液体の応答がまだ無い一瞬は、液体なしで描き、届いた `OnSubInventoryUpdated` で描き直す）

タブと取得の依頼の判定（`TrainTimetableFetchTrigger`）はパネルごとの局所状態なので State に Model を置かず、Task 7 の `TrainInventoryBodyPresenter` が 1 つ持って別のパネルで作り直す（D-33）。

- [ ] **Step 3: プラットフォーム側の値と源を書く**

`TrainPlatformTransferMode.cs`:

```csharp
namespace Client.Ui.State.Train.Platform
{
    /// <summary>
    /// 積込（PF → 列車）と卸し（列車 → PF）。View が Game.Block を参照しないよう State で持つ（D-8）
    /// Load (platform to train) and unload (train to platform); held in State so the View never references Game.Block (D-8)
    /// </summary>
    public enum TrainPlatformTransferMode
    {
        LoadToTrain,
        UnloadToPlatform,
    }
}
```

`TrainPlatformTransferModeConversion.cs`:

```csharp
using Game.Block.Blocks.TrainRail;

namespace Client.Ui.State.Train.Platform
{
    /// <summary>
    /// サーバーの移送モードとの写しを 1 か所に置く（読み手と送り手が共有する）
    /// Keeps the mapping to the server transfer mode in one place, shared by the reader and the sender
    /// </summary>
    public static class TrainPlatformTransferModeConversion
    {
        public static TrainPlatformTransferMode FromServer(TrainPlatformTransferComponent.TransferMode mode)
        {
            return mode == TrainPlatformTransferComponent.TransferMode.UnloadToPlatform ? TrainPlatformTransferMode.UnloadToPlatform : TrainPlatformTransferMode.LoadToTrain;
        }

        public static TrainPlatformTransferComponent.TransferMode ToServer(TrainPlatformTransferMode mode)
        {
            return mode == TrainPlatformTransferMode.UnloadToPlatform ? TrainPlatformTransferComponent.TransferMode.UnloadToPlatform : TrainPlatformTransferComponent.TransferMode.LoadToTrain;
        }
    }
}
```

`TrainStationNamePart.cs`:

```csharp
namespace Client.Ui.State.Train.Platform
{
    /// <summary>
    /// 駅だけが持つ部分。サーバーと同期した駅名（届く前は空）
    /// Part only a station has: the station name synced from the server (empty until it arrives)
    /// </summary>
    public sealed class TrainStationNamePart
    {
        public string SyncedName { get; }

        public TrainStationNamePart(string syncedName)
        {
            SyncedName = syncedName;
        }
    }
}
```

`TrainPlatformBodyState.cs`（判別子と片側だけで意味のある値を同居させず、部分ごとの有無を S4-machine の `SectionContent<T>`〈`Client.Ui.State.BlockInventory.Common`〉で表す。null で「無し」を表さない。Web の `trainStation?`・`trainPlatform?` の省略可能な詳細と同じ意味。D-19・D-34）:

```csharp
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.Items;

namespace Client.Ui.State.Train.Platform
{
    /// <summary>
    /// 駅・プラットフォームの本文の状態。閉じているか、部分ごとの有無を持つ開いた本文
    /// State of the station and platform body: closed, or open with each part present or absent
    /// </summary>
    public abstract class TrainPlatformBodyState
    {
        public static readonly TrainPlatformBodyState Closed = new ClosedTrainPlatformBody();
    }

    public sealed class ClosedTrainPlatformBody : TrainPlatformBodyState
    {
        internal ClosedTrainPlatformBody() { }
    }

    public sealed class OpenTrainPlatformBody : TrainPlatformBodyState
    {
        // 駅だけが持つ。他は Absent
        // Present on stations only; Absent otherwise
        public SectionContent<TrainStationNamePart> StationName { get; }
        // 移送の状態がある品物 PF・駅だけが持つ。他は Absent
        // Present only on item platforms and stations with a transfer state; Absent otherwise
        public SectionContent<IReadOnlyList<SlotItem>> ItemSlots { get; }
        public IReadOnlyList<BlockFluidSlotState> Fluids { get; }
        // 移送の状態があるときだけ持つ
        // Present only when a transfer state exists
        public SectionContent<TrainPlatformTransferMode> Mode { get; }

        public OpenTrainPlatformBody(SectionContent<TrainStationNamePart> stationName, SectionContent<IReadOnlyList<SlotItem>> itemSlots, IReadOnlyList<BlockFluidSlotState> fluids, SectionContent<TrainPlatformTransferMode> mode)
        {
            StationName = stationName;
            ItemSlots = itemSlots;
            Fluids = fluids;
            Mode = mode;
        }
    }
}
```

`ITrainPlatformBodySource.cs`:

```csharp
using UniRx;

namespace Client.Ui.State.Train.Platform
{
    /// <summary>
    /// 開いている駅・プラットフォームの描画値の口
    /// Source of the open station or platform's draw values
    /// </summary>
    public interface ITrainPlatformBodySource
    {
        IReadOnlyReactiveProperty<TrainPlatformBodyState> Current { get; }
    }
}
```

`TrainPlatformDetailReader.cs`（`BlockGameObject` を取らない純関数。値の取り出しは Model の 1 か所。D-21）:

```csharp
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.Items;
using Game.Block.Blocks.TrainRail;
using Mooresmaster.Model.BlocksModule;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Client.Ui.State.Train.Platform
{
    /// <summary>
    /// 駅・プラットフォームの描画値を組む（Web ホスト TrainStationDetailDtoBuilder・TrainPlatformDetailDtoBuilder と同じ規則。D-2）
    /// Builds station and platform draw values (same rules as the Web host TrainStationDetailDtoBuilder and TrainPlatformDetailDtoBuilder; D-2)
    /// </summary>
    public static class TrainPlatformDetailReader
    {
        public static OpenTrainPlatformBody Read(string blockType, IBlockParam param, TrainStationNameStateDetail stationName, TrainPlatformTransferStateDetail transfer, IReadOnlyList<SlotItem> itemSlots, IReadOnlyList<BlockFluidSlotState> fluids)
        {
            // 駅名・移送モードはサーバーの StateDetail。届く前は名前は空・モードの切替は出さない（Web ホストと同じ）
            // Name and mode are server StateDetails; before they arrive the name is empty and no mode switch shows (same as the Web host)
            // silent-ok: 駅名の StateDetail が届く前は空名の欄を出す（Web ホストと同じ）/ 届かないままなら欄が空のままで、録画 uitk/uitk-train-inventory 手順 2 の駅名の待ちが落ちて開発者が気づく
            var namePart = blockType == BlockTypeConst.TrainStation
                ? SectionContent<TrainStationNamePart>.Present(new TrainStationNamePart(stationName?.StationName ?? string.Empty))
                : SectionContent<TrainStationNamePart>.Absent;
            var mode = transfer == null ? SectionContent<TrainPlatformTransferMode>.Absent : SectionContent<TrainPlatformTransferMode>.Present(TrainPlatformTransferModeConversion.FromServer(transfer.Mode));

            // スロットは移送の状態があり、かつ品物の PF・駅のときだけ（Web ホストは状態が無ければ詳細ごと出さない）
            // Item slots only with a transfer state on an item platform or station (the Web host omits the detail without a state)
            var hasItemSlots = transfer != null && param is TrainItemPlatformBlockParam or TrainStationBlockParam;
            var slotsPart = hasItemSlots ? SectionContent<IReadOnlyList<SlotItem>>.Present(itemSlots) : SectionContent<IReadOnlyList<SlotItem>>.Absent;
            return new OpenTrainPlatformBody(namePart, slotsPart, fluids, mode);
        }
    }
}
```
（`ItemSlotCount` は Web と同じく描画には使わない: 実枠はパネルの `ItemSlots` の数で出る）

`TrainPlatformBodyModel.cs`:

```csharp
using Client.Game.InGame.UI.UIState.State;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Fluid;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.State.BlockInventory.Panel;
using Game.Block.Blocks.TrainRail;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Train.Platform
{
    /// <summary>
    /// 開いている駅・プラットフォームの描画値を、パネルの状態・追跡ブロック・液体の Model から配る
    /// Publishes the open station or platform's draw values from the panel state, the tracked block and the fluid model
    /// </summary>
    public sealed class TrainPlatformBodyModel : ITrainPlatformBodySource, IInitializable
    {
        private readonly IBlockInventoryPanelSource _panels;
        private readonly OpenBlockDetailTracker _tracker;
        private readonly IBlockFluidSlotsModel _fluidSlots;
        private readonly ReactiveProperty<TrainPlatformBodyState> _current = new(TrainPlatformBodyState.Closed);

        public IReadOnlyReactiveProperty<TrainPlatformBodyState> Current => _current;

        public TrainPlatformBodyModel(IBlockInventoryPanelSource panels, OpenBlockDetailTracker tracker, IBlockFluidSlotsModel fluidSlots)
        {
            _panels = panels;
            _tracker = tracker;
            _fluidSlots = fluidSlots;
        }

        public void Initialize()
        {
            // 追跡替え・状態変化は液体の Model が組み直して必ず配り直すので、その 1 本に乗って 1 回だけ組む（D-35）
            // The fluid model rebuilds and republishes on every tracked-block change and state change, so ride that one stream and build once (D-35)
            _panels.Current.Subscribe(_ => Rebuild());
            _fluidSlots.Current.Subscribe(_ => Rebuild());
        }

        private void Rebuild()
        {
            // 本文の種類はパネルの登録表（BlockComponentRegistry）で決まり、ここで blockType を並べない
            // The body kind comes from the panel registry (BlockComponentRegistry); no blockType list here
            if (_panels.Current.Value is not OpenBlockInventoryPanel { Error: BlockInventoryPanelError.None } open || open.Components.Body != BlockInventoryBodyKind.TrainPlatform)
            {
                _current.Value = TrainPlatformBodyState.Closed;
                return;
            }
            var block = _tracker.TrackedBlock;
            // silent-ok: 追跡の替わりがパネルより後に届く一瞬だけ閉じた値 / 直後の追跡替えで液体の Model が配り直すので組み直し（D-35）、出ないままなら L1・録画で本文が空のまま見える
            if (block == null)
            {
                _current.Value = TrainPlatformBodyState.Closed;
                return;
            }

            // StateDetail はサーバー由来の外部データなので無いことがある（Reader が扱う）
            // StateDetails come from the server and may be missing; the Reader handles that
            var stationName = block.GetStateDetail<TrainStationNameStateDetail>(TrainStationNameStateDetail.BlockStateDetailKey);
            var transfer = block.GetStateDetail<TrainPlatformTransferStateDetail>(TrainPlatformTransferStateDetail.BlockStateDetailKey);
            _current.Value = TrainPlatformDetailReader.Read(open.BlockType, block.BlockMasterElement.BlockParam, stationName, transfer, open.ItemSlots, _fluidSlots.Current.Value.Slots);
        }
    }
}
```
（`OpenBlockDetailTracker` の 2 つの通知を自分でも購読すると、1 回の状態変化で「追跡の通知 → 液体の配り直し」の 2 回組み直し、1 回目は古い液体の列で組む。液体の Model〈S4-machine の `BlockFluidSlotsModel`〉は同じ 2 つの通知で毎回新しい `BlockFluidSlotsViewModel` を作って配り直すので、その 1 本だけに乗れば新しい追跡ブロックと液体で 1 回だけ組む。`ReactiveProperty` は購読した瞬間に今の値を流すので初回の `Rebuild()` も要らない。前提〈状態変化のたびに参照の違う値を配る〉は Task 1 Step 2 で確かめ、同じ値を畳んで流さない作りなら `_tracker` の 2 つの通知の購読へ戻し、`_fluidSlots` は読むだけにする。D-35）

- [ ] **Step 4: プラットフォームの編集側を書く（`Platform/Edit/`）**

`ITrainBlockCommands.cs`:

```csharp
using Client.Ui.State.Commands;
using Client.Ui.State.Train.Platform;
using Cysharp.Threading.Tasks;

namespace Client.Ui.State.Train.Platform.Edit
{
    /// <summary>
    /// 開いている駅・プラットフォームへの操作。対象はその時点で開いているブロック（Web と同じ）
    /// Operations on the open station or platform; the target is the block open at that moment (same as Web)
    /// </summary>
    public interface ITrainBlockCommands
    {
        UniTask<UiCommandResult> SetStationName(string name);
        UniTask<UiCommandResult> SetTransferMode(TrainPlatformTransferMode mode);
    }
}
```

`TrainBlockCommands.cs`（拒否は理由ごとの `UiCommandRejectReason`。wire は Web ホスト `TrainStationActions.cs:28-35,43`・`TrainPlatformActions.cs:30-32,41` の接頭辞と同じ綴り〈照合済み〉。モードの失敗は `mode_request_failed`〈Web ホストも理由の接尾辞なしなので Detail も空〉。D-31）:

```csharp
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.State.Commands;
using Cysharp.Threading.Tasks;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Client.Ui.State.Train.Platform.Edit
{
    /// <summary>
    /// 駅名・移送モードをサーバーへ送る（Web ホスト TrainStationActions・TrainPlatformActions と同じ規則。D-2）
    /// Sends station names and transfer modes to the server (same rules as the Web host TrainStationActions and TrainPlatformActions; D-2)
    /// </summary>
    public sealed class TrainBlockCommands : ITrainBlockCommands
    {
        private const string StationLogTag = "TrainStationAction";
        private const string PlatformLogTag = "TrainPlatformAction";
        private readonly SubInventoryState _subInventoryState;

        public TrainBlockCommands(SubInventoryState subInventoryState)
        {
            _subInventoryState = subInventoryState;
        }

        public async UniTask<UiCommandResult> SetStationName(string name)
        {
            if (!TryResolveOpenBlock(out var source, out var blockType)) return TrainCommandRejection.Reject(StationLogTag, UiCommandRejectReason.BlockNotOpen, string.Empty);
            if (blockType != BlockTypeConst.TrainStation) return TrainCommandRejection.Reject(StationLogTag, UiCommandRejectReason.TrainInvalidBlockType, string.Empty);

            // 応答はネットワーク由来なので null を拒否として扱う
            // The response comes from the network, so null counts as a refusal
            var response = await ClientContext.VanillaApi.Response.Train.SetTrainStationName(source.BlockPosition, name, CancellationToken.None);
            if (response == null || !response.Success) return TrainCommandRejection.Reject(StationLogTag, UiCommandRejectReason.TrainStationNameFailed, response?.FailureReason);
            return UiCommandAccepted.Instance;
        }

        public async UniTask<UiCommandResult> SetTransferMode(TrainPlatformTransferMode mode)
        {
            // 本文の種類はパネルと同じ登録表で判定する（blockType を並べない）
            // The body kind is judged by the same registry as the panel (no blockType list)
            if (!TryResolveOpenBlock(out var source, out var blockType)) return TrainCommandRejection.Reject(PlatformLogTag, UiCommandRejectReason.BlockNotOpen, string.Empty);
            if (BlockComponentRegistry.Resolve(blockType).Body != BlockInventoryBodyKind.TrainPlatform) return TrainCommandRejection.Reject(PlatformLogTag, UiCommandRejectReason.TrainInvalidBlockType, string.Empty);

            var response = await ClientContext.VanillaApi.Response.Train.SetTrainPlatformTransferMode(source.BlockPosition, TrainPlatformTransferModeConversion.ToServer(mode), CancellationToken.None);
            if (response == null || !response.Success) return TrainCommandRejection.Reject(PlatformLogTag, UiCommandRejectReason.TrainPlatformModeFailed, string.Empty);
            return UiCommandAccepted.Instance;
        }

        private bool TryResolveOpenBlock(out BlockSubInventorySource source, out string blockType)
        {
            blockType = null;
            source = _subInventoryState.CurrentSubInventorySource as BlockSubInventorySource;
            return source != null && source.TryGetBlockIdentity(out _, out blockType);
        }
    }
}
```

`TrainStationNameDraft.cs`:

```csharp
namespace Client.Ui.State.Train.Platform.Edit
{
    /// <summary>
    /// 駅名の下書き。同期済みの名前と同じ入力（未編集）だけが同期に従い、拒否で最新の同期済みへ戻す（Web TrainStationNameSection の写し）
    /// Station name draft: only an input equal to the synced name (unedited) follows syncs; rejection reverts to the latest synced name (copy of Web TrainStationNameSection)
    /// </summary>
    public sealed class TrainStationNameDraft
    {
        private string _synced = string.Empty;

        public string Input { get; private set; } = string.Empty;

        public void Sync(string syncedName)
        {
            // Web と同じく「前の同期値と同じ入力」を未編集とみなす
            // Same as Web: an input equal to the previous synced value counts as unedited
            if (Input == _synced) Input = syncedName;
            _synced = syncedName;
        }

        public void Edit(string input)
        {
            Input = input;
        }

        // 非受理・到達不能なら入力を捨て、待ちのあいだに届いた最新の同期値へ戻す
        // On refusal or no reply, drop the input and return to the latest synced name that arrived meanwhile
        public void Reject()
        {
            Input = _synced;
        }
    }
}
```

`TrainStationNameEditState.cs`（駅名の下書きの器。描画値の Model と View が呼ぶコマンドの両方が読み書きするので、どちらにも寄せず DI の単一インスタンスで共有する。View がコマンドを直接呼び〈A-8〉、遅れて戻る拒否が送った回の下書きを見分ける〈D-25〉には、下書きが Presenter より長く生きる必要がある。D-33）:

```csharp
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Commands;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Ui.State.Train.Platform.Edit
{
    /// <summary>
    /// 駅名の下書きの器。パネルごとに下書きを作り直し、同期した名前を下書きへ流す
    /// Holder of the station name draft; rebuilds the draft per panel and feeds synced names into it
    /// </summary>
    public sealed class TrainStationNameEditState : IInitializable
    {
        private readonly IBlockInventoryPanelSource _panels;
        private readonly ITrainPlatformBodySource _bodies;
        private readonly ReactiveProperty<string> _input = new(string.Empty);
        private BlockInventoryPanelState _panel = BlockInventoryPanelState.Closed;

        public IReadOnlyReactiveProperty<string> Input => _input;
        public TrainStationNameDraft Draft { get; private set; } = new();

        public TrainStationNameEditState(IBlockInventoryPanelSource panels, ITrainPlatformBodySource bodies)
        {
            _panels = panels;
            _bodies = bodies;
        }

        public void Initialize()
        {
            _panels.Current.Subscribe(ReceivePanel);
            _bodies.Current.Subscribe(_ => SyncDraft());
        }

        public void Edit(string input)
        {
            Draft.Edit(input);
            _input.Value = Draft.Input;
        }

        // 下書きは閉じる・別のパネルのたびに作り直すので、開閉の世代も兼ねる。送った回の下書きが今のものでなければ戻さない
        // Drafts are rebuilt on every close or panel change, so they double as the open generation; revert only the draft that sent
        public void RevertIfCurrent(TrainStationNameDraft draftAtSend)
        {
            if (!ReferenceEquals(Draft, draftAtSend))
            {
                Debug.LogWarning("[TrainStationName] set_name refused after the draft was replaced; nothing to revert");
                return;
            }
            Draft.Reject();
            _input.Value = Draft.Input;
        }

        private void ReceivePanel(BlockInventoryPanelState panel)
        {
            // silent-ok: 同じパネルの再配信では下書きを保つ / 打ちかけの入力を消さないため。別のパネルは下で作り直す
            if (panel.IsSamePanel(_panel)) return;
            _panel = panel;
            Draft = new TrainStationNameDraft();
            SyncDraft();
        }

        private void SyncDraft()
        {
            if (_bodies.Current.Value is OpenTrainPlatformBody { StationName: { IsPresent: true } stationName }) Draft.Sync(stationName.Value.SyncedName);
            _input.Value = Draft.Input;
        }
    }
}
```

`ITrainPlatformEditModel.cs`:

```csharp
using UniRx;

namespace Client.Ui.State.Train.Platform.Edit
{
    /// <summary>
    /// 駅名の入力欄の描画値。Presenter が購読する
    /// Draw value of the station name field; the Presenter subscribes to it
    /// </summary>
    public interface ITrainPlatformEditModel
    {
        IReadOnlyReactiveProperty<string> StationNameInput { get; }
    }
}
```

`TrainPlatformEditModel.cs`:

```csharp
using UniRx;

namespace Client.Ui.State.Train.Platform.Edit
{
    /// <summary>
    /// 駅名の下書きの器から入力欄の描画値を配る
    /// Publishes the station name field's draw value from the draft holder
    /// </summary>
    public sealed class TrainPlatformEditModel : ITrainPlatformEditModel
    {
        private readonly TrainStationNameEditState _state;

        public IReadOnlyReactiveProperty<string> StationNameInput => _state.Input;

        public TrainPlatformEditModel(TrainStationNameEditState state)
        {
            _state = state;
        }
    }
}
```

`ITrainPlatformEditCommands.cs`:

```csharp
using Client.Ui.State.Commands;

namespace Client.Ui.State.Train.Platform.Edit
{
    /// <summary>
    /// 駅名の入力と決定・移送モードの切替。View が直接呼ぶ（設計書 A-8）。サーバーの結果は実装がトーストへ渡す
    /// Station name input and apply, and transfer mode selection; the View calls them directly (design doc A-8) and the implementation toasts server results
    /// </summary>
    public interface ITrainPlatformEditCommands
    {
        void EditStationName(string input);
        UiCommandResult ApplyStationName();
        UiCommandResult SelectTransferMode(TrainPlatformTransferMode mode);
    }
}
```

`TrainPlatformEditCommands.cs`:

```csharp
using Client.Ui.State.Commands;
using Client.Ui.State.Toast;
using Cysharp.Threading.Tasks;

namespace Client.Ui.State.Train.Platform.Edit
{
    /// <summary>
    /// 駅名と移送モードを送る。結果はトーストへ渡し、駅名の拒否は送った回の下書きだけを戻す
    /// Sends station names and transfer modes; results go to the toaster and a station name refusal reverts only the draft that sent
    /// </summary>
    public sealed class TrainPlatformEditCommands : ITrainPlatformEditCommands
    {
        public const string SetNameCommandName = "train_station.set_name";
        public const string SetModeCommandName = "train_platform.set_transfer_mode";
        private readonly TrainStationNameEditState _state;
        private readonly ITrainBlockCommands _commands;
        private readonly UiCommandResultToaster _toaster;

        public TrainPlatformEditCommands(TrainStationNameEditState state, ITrainBlockCommands commands, UiCommandResultToaster toaster)
        {
            _state = state;
            _commands = commands;
            _toaster = toaster;
        }

        public void EditStationName(string input)
        {
            _state.Edit(input);
        }

        // 受理は「送った」の意味。サーバーの結果は届いてからトーストへ渡す
        // Accepted means "sent"; the server result is reported to the toaster when it arrives
        public UiCommandResult ApplyStationName()
        {
            ApplyAsync(_state.Draft, _state.Draft.Input).Forget();
            return UiCommandAccepted.Instance;
        }

        // 選択は替えず目標のモードを送るだけ。結果は公開で反映され、拒否だけトーストへ
        // The selection is not changed; only the target mode is sent and the publication reflects it, with refusals toasted
        public UiCommandResult SelectTransferMode(TrainPlatformTransferMode mode)
        {
            SendModeAsync(mode).Forget();
            return UiCommandAccepted.Instance;
        }

        private async UniTaskVoid ApplyAsync(TrainStationNameDraft draftAtSend, string name)
        {
            var result = await _commands.SetStationName(name);
            _toaster.Report(SetNameCommandName, result);
            if (result is UiCommandRejected) _state.RevertIfCurrent(draftAtSend);
        }

        private async UniTaskVoid SendModeAsync(TrainPlatformTransferMode mode)
        {
            var result = await _commands.SetTransferMode(mode);
            _toaster.Report(SetModeCommandName, result);
        }
    }
}
```
（駅が開いていないときの拒否は `TrainBlockCommands` が `BlockNotOpen` で返し、上の `Report` がトーストする〈D-32〉ので、ここでは開閉を見ない。View の押下は戻り値を使わない〈結果のトーストは実装が済ませる。D-33〉）

- [ ] **Step 5: テストを回してコミット**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainTimetableFetchTriggerTest|TrainStationNameDraftTest|TrainPlatformDetailReaderTest|TrainPlatformEditModelTest"`
Expected: `ErrorCount: 0`、7 件 PASS

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Train moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Train
git commit -m "feat(uitk): 貨車とプラットフォームの Model・駅名の編集の器と Model とコマンドを足す（S4-train）

<実行ホストの指示する帰属行>"
```

---

### Task 6: 時刻表の View（`Screens/BlockInventory/Train/Timetable/`）

**Files:**
- Create: `S/Client.Ui/Screens/BlockInventory/Train/Timetable/` の 6 本（`.cs` 5・`Timetable.uss`）

**Interfaces:**
- Consumes: Task 4 の `ITrainTimetableEditCommands`・`TrainTimetableAreaViewModel`・`TrainTimetableSectionViewModel`、`GamePanel・`FadeRule`・`PanelActionButton`・`ModeSwitch<bool>`・`LocalizedLabel`・`UiStack`・`UiTypographyClass`
- Produces: `TrainTimetableAreaView(ITrainTimetableEditCommands commands)`（`Render(TrainTimetableAreaViewModel model, string unnamedStationName)`）、`TrainTimetableSectionView.BusyClass = "train-timetable-section--busy"`、画面のクラス `train-timetable__row-end`（行の右の子）

View は押下で `ITrainTimetableEditCommands` の型付きの操作を直接呼ぶ（設計書 A-8。描画値の `ITrainTimetableEditModel` とは別の口。D-33）。結果のトーストはコマンド側が済ませるので View は戻り値を読まない。描画は Presenter（Task 7 の `TrainInventoryBodyPresenter`）が `Render` へ流す。L1 は Task 7 の本文ごと組んで試す（`TrainTimetableSectionViewTest` は Task 7 Step 4）。

- [ ] **Step 1: 2 つの列を書く**

`TrainTimetableStopListView.cs`:

```csharp
using Client.Localization;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Decor;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.TimetableEdit;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train.Timetable
{
    /// <summary>
    /// 停車駅の枠（Web TrainTimetableStopList の写し）。押下は編集のコマンドを直接呼ぶ（A-8）
    /// Stop list frame (copy of Web TrainTimetableStopList); presses call the edit commands directly (A-8)
    /// </summary>
    public sealed class TrainTimetableStopListView : VisualElement
    {
        private const string ListTestId = "train-timetable-stops";
        private readonly ITrainTimetableEditCommands _commands;
        private readonly GamePanel _panel;
        private readonly LocalizedLabel _empty;
        private readonly VisualElement _list;

        public TrainTimetableStopListView(ITrainTimetableEditCommands commands)
        {
            _commands = commands;

            // Craft 枠に見出し（内側余白は器、文字の型はラベル）・飾り線・列（空なら文言）
            // A craft frame with the heading (padding on the wrapper, type on the label), the fade rule and the list or empty text
            _panel = new GamePanel("train-timetable-stops-panel", GamePanelFrame.Craft);
            _empty = new LocalizedLabel { name = ListTestId };
            _empty.SetKey(LocalizationKeys.Ui.BlockInventory.TimetableStopsEmpty);
            _empty.AddToClassList(UiTypographyClass.TextSm);
            _list = new VisualElement { name = ListTestId };
            _list.AddToClassList("train-timetable__list");
            _panel.Add(TrainTimetableHeading.Create(LocalizationKeys.Ui.BlockInventory.TimetableStops));
            _panel.Add(new FadeRule());
            Add(_panel);
        }

        public void Render(TrainTimetableSectionViewModel model, string unnamedStationName)
        {
            // 空の文言と列は同じ testid なので、片方だけを木に置く
            // The empty text and the list share a testid, so only one stays in the tree
            _empty.RemoveFromHierarchy();
            _list.RemoveFromHierarchy();
            if (model.Stops.Count == 0)
            {
                _panel.Add(_empty);
                return;
            }
            _list.Clear();
            for (var i = 0; i < model.Stops.Count; i++) _list.Add(CreateRow(model, i, unnamedStationName));
            _panel.Add(_list);
        }

        private VisualElement CreateRow(TrainTimetableSectionViewModel model, int index, string unnamedStationName)
        {
            var stop = model.Stops[index];
            var row = new VisualElement { name = $"train-timetable-stop-{index}" };
            row.AddToClassList("train-timetable__row");
            row.EnableInClassList("train-timetable__row--following", 0 < index);
            row.EnableInClassList("train-timetable-stop--current", model.CurrentIndex == index);

            // ラベルは「番号. 駅名(座標)」。駅名の空は言語で変わるので Presenter が言語の変化で描き直す（D-10）
            // The label is "index. station(coords)"; the Presenter re-renders on language change since the unnamed name localizes (D-10)
            var station = TimetableEditLogic.StationLabel(new TrainTimetableStation(stop.Position, stop.Name), unnamedStationName).Resolve();
            var label = new LocalizedLabel { name = $"train-timetable-stop-{index}-label" };
            label.SetText(new UiText(LocalizationKeys.Ui.BlockInventory.TimetableStopLabel, new[]
            {
                new LocalizationTextParam("index", UiNumberFormat.Integer(index + 1)),
                new LocalizationTextParam("station", station),
            }));
            label.AddToClassList("train-timetable__label");

            // 押下は行の番号つきで編集のコマンドへ。行を作り直すと古いボタンごと購読も捨てられる
            // Presses go to the edit commands with the row index; rebuilding rows discards old buttons with their subscriptions
            var up = Control($"train-timetable-stop-{index}-up", LocalizationKeys.Ui.BlockInventory.TimetableMoveUp, model.IsPending || index == 0);
            var down = Control($"train-timetable-stop-{index}-down", LocalizationKeys.Ui.BlockInventory.TimetableMoveDown, model.IsPending || index == model.Stops.Count - 1);
            var remove = Control($"train-timetable-stop-{index}-remove", LocalizationKeys.Ui.BlockInventory.TimetableRemove, model.IsPending);
            up.OnClicked.Subscribe(_ => _commands.Move(index, StopMoveDirection.Up));
            down.OnClicked.Subscribe(_ => _commands.Move(index, StopMoveDirection.Down));
            remove.OnClicked.Subscribe(_ => _commands.Remove(index));

            // 2 個目以降の操作に間のクラス（UITK の USS に兄弟の結合子が無いので C# で付ける。前例 ModeSwitch の --following）
            // Gap class on the second and later controls (UITK USS has no sibling combinator; precedent ModeSwitch --following)
            down.AddToClassList("train-timetable__control--following");
            remove.AddToClassList("train-timetable__control--following");
            var controls = new VisualElement();
            controls.AddToClassList("train-timetable__controls");
            controls.AddToClassList("train-timetable__row-end");
            controls.Add(up);
            controls.Add(down);
            controls.Add(remove);
            row.Add(label);
            row.Add(controls);
            return row;
        }

        private static PanelActionButton Control(string testid, LocalizationKey label, bool disabled)
        {
            var button = new PanelActionButton(testid, label);
            button.AddToClassList("train-timetable__control");
            button.SetEnabled(!disabled);
            return button;
        }
    }
}
```
（`UiNumberFormat` の名前空間は `TimetableEditLogic` と同じ）

`TrainTimetableStationListView.cs`:

```csharp
using Client.Localization;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Decor;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.TimetableEdit;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train.Timetable
{
    /// <summary>
    /// 追加先の駅一覧の枠（Web TrainTimetableSection の駅一覧の写し）。追加は編集のコマンドを直接呼ぶ（A-8）
    /// Frame listing the stations that can be added (copy of the Web TrainTimetableSection station list); adds call the edit commands directly (A-8)
    /// </summary>
    public sealed class TrainTimetableStationListView : VisualElement
    {
        private readonly ITrainTimetableEditCommands _commands;
        private readonly LocalizedLabel _empty;
        private readonly VisualElement _list;

        public TrainTimetableStationListView(ITrainTimetableEditCommands commands)
        {
            _commands = commands;
            var panel = new GamePanel("train-timetable-stations-panel", GamePanelFrame.Craft);
            _list = new VisualElement { name = "train-timetable-stations" };
            _list.AddToClassList("train-timetable__list");
            _empty = new LocalizedLabel();
            _empty.SetKey(LocalizationKeys.Ui.BlockInventory.TimetableStationsEmpty);
            _empty.AddToClassList(UiTypographyClass.TextSm);
            panel.Add(TrainTimetableHeading.Create(LocalizationKeys.Ui.BlockInventory.TimetableStations));
            panel.Add(new FadeRule());
            panel.Add(_list);
            Add(panel);
        }

        public void Render(TrainTimetableSectionViewModel model, string unnamedStationName)
        {
            // 0 件は列の中に文言を置く（Web と同じく testid は列に付く）
            // With no stations the text sits inside the list (the testid stays on the list, as in Web)
            _list.Clear();
            if (model.Stations.Count == 0)
            {
                _list.Add(_empty);
                return;
            }
            for (var i = 0; i < model.Stations.Count; i++) _list.Add(CreateRow(model.Stations[i], 0 < i, model.IsPending, unnamedStationName));
        }

        private VisualElement CreateRow(TrainTimetableStation station, bool following, bool disabled, string unnamedStationName)
        {
            // testid は座標を _ でつなぐ（負号はそのまま。例 -8_0_3）
            // The testid joins coordinates with _ and keeps minus signs (e.g. -8_0_3)
            var key = $"{station.Position.x}_{station.Position.y}_{station.Position.z}";
            var row = new VisualElement();
            row.AddToClassList("train-timetable__row");
            row.EnableInClassList("train-timetable__row--following", following);
            var label = new LocalizedLabel();
            label.SetText(TimetableEditLogic.StationLabel(station, unnamedStationName));
            label.AddToClassList("train-timetable__label");
            var add = new PanelActionButton($"train-timetable-station-{key}-add", LocalizationKeys.Ui.BlockInventory.TimetableAdd);
            add.AddToClassList("train-timetable__control");
            add.AddToClassList("train-timetable__row-end");
            add.SetEnabled(!disabled);
            add.OnClicked.Subscribe(_ => _commands.Add(station));
            row.Add(label);
            row.Add(add);
            return row;
        }
    }
}
```
（`StationLabel` の名前の引数は言語ごとに `Localize.Get(timetableUnnamedStation)` を Presenter が渡す。`StationLabel` の UiText 自体は `LocalizedLabel` が言語の変化で描き直す）

`TrainTimetableHeading.cs`:

```csharp
using Client.Localization;
using Client.Ui.Foundation.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train.Timetable
{
    /// <summary>
    /// 枠の見出し。内側余白は器に、文字の型（sm・高コントラスト）はラベルに付ける（同じ要素に付けると padding が取り合う）
    /// Frame heading: padding on the wrapper and the type (sm, high contrast) on the label, since both on one element fight over padding
    /// </summary>
    public static class TrainTimetableHeading
    {
        public static VisualElement Create(LocalizationKey key)
        {
            var wrapper = new VisualElement();
            wrapper.AddToClassList("train-timetable__section-heading");
            var label = new LocalizedLabel();
            label.SetKey(key);
            label.AddToClassList(UiTypographyClass.TextSm);
            label.AddToClassList(UiTypographyClass.TextHighContrast);
            wrapper.Add(label);
            return wrapper;
        }
    }
}
```

- [ ] **Step 2: 区画と器を書く**

`TrainTimetableSectionView.cs`:

```csharp
using Client.Localization;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using Client.Ui.State.Train.TimetableEdit;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train.Timetable
{
    /// <summary>
    /// 時刻表の区画（Web TrainTimetableSection の写し）。自動運転の行・停車駅・駅一覧
    /// Timetable section (copy of Web TrainTimetableSection): auto-run row, stops and stations
    /// </summary>
    public sealed class TrainTimetableSectionView : VisualElement
    {
        public const string BusyClass = "train-timetable-section--busy";
        private readonly UiStack _stack = new(UiGap.Xs);
        private readonly ModeSwitch<bool> _autoRun;
        private readonly LocalizedLabel _autoRunUnavailable;
        private readonly TrainTimetableStopListView _stops;
        private readonly TrainTimetableStationListView _stations;

        public TrainTimetableSectionView(ITrainTimetableEditCommands commands)
        {
            name = "train-timetable-section";
            AddToClassList("train-timetable");

            // 自動運転の行: 左に見出し・右に ON/OFF。押下は編集のコマンドを直接呼ぶ（A-8）
            // Auto-run row: heading left, ON/OFF right; presses call the edit commands directly (A-8)
            var autoRunRow = new VisualElement();
            autoRunRow.AddToClassList("train-timetable__row");
            var autoRunLabel = new LocalizedLabel();
            autoRunLabel.SetKey(LocalizationKeys.Ui.BlockInventory.TimetableAutoRun);
            autoRunLabel.AddToClassList(UiTypographyClass.TextSm);
            _autoRun = new ModeSwitch<bool>("train-timetable-auto-run", ModeSwitchOrientation.Horizontal, ModeSwitchRole.Group, new[]
            {
                new ModeSwitchOption<bool>("train-timetable-auto-run-on", true, new UiText(LocalizationKeys.Ui.BlockInventory.TimetableAutoRunOn)),
                new ModeSwitchOption<bool>("train-timetable-auto-run-off", false, new UiText(LocalizationKeys.Ui.BlockInventory.TimetableAutoRunOff)),
            });
            _autoRun.AddToClassList("train-timetable__row-end");
            _autoRun.OnOptionSelected.Subscribe(enabled => commands.SetAutoRun(enabled));
            autoRunRow.Add(autoRunLabel);
            autoRunRow.Add(_autoRun);
            _autoRunUnavailable = new LocalizedLabel { name = "train-timetable-auto-run-unavailable" };
            _autoRunUnavailable.SetKey(LocalizationKeys.Ui.BlockInventory.TimetableAutoRunUnavailable);
            _autoRunUnavailable.AddToClassList(UiTypographyClass.TextSm);

            _stops = new TrainTimetableStopListView(commands);
            _stations = new TrainTimetableStationListView(commands);
            _stack.AddItem(autoRunRow);
            _stack.AddItem(_autoRunUnavailable);
            _stack.AddItem(_stops);
            _stack.AddItem(_stations);
            Add(_stack);
        }

        public void Render(TrainTimetableSectionViewModel model, string unnamedStationName)
        {
            // 保留中は全操作を塞ぎ、ON はサーバーの停車駅が 0 件のあいだも塞ぐ
            // While pending every control is blocked; ON also stays blocked while the server has no stops
            EnableInClassList(BusyClass, model.IsPending);
            _autoRun.SetSelected(model.IsAutoRun);
            _autoRun.SetOptionEnabled(true, !model.IsPending && model.CanEnableAutoRun);
            _autoRun.SetOptionEnabled(false, !model.IsPending);
            _stack.SetItemShown(_autoRunUnavailable, !model.CanEnableAutoRun);
            _stops.Render(model, unnamedStationName);
            _stations.Render(model, unnamedStationName);
        }
    }
}
```

`TrainTimetableAreaView.cs`:

```csharp
using Client.Localization;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Train.TimetableEdit;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train.Timetable
{
    /// <summary>
    /// 時刻表タブの中身。読込中・使えない・区画のどれか 1 つを出す
    /// Content of the timetable tab: shows exactly one of loading, unavailable, or the section
    /// </summary>
    public sealed class TrainTimetableAreaView : VisualElement
    {
        private readonly ITrainTimetableEditCommands _commands;
        private readonly LocalizedLabel _loading;
        private readonly LocalizedLabel _unavailable;
        private TrainTimetableSectionView _section;
        private ReadyTrainTimetableArea _sectionArea;

        public TrainTimetableAreaView(ITrainTimetableEditCommands commands)
        {
            name = "train-timetable-area";
            _commands = commands;
            _loading = Message("train-timetable-loading", LocalizationKeys.Ui.BlockInventory.TimetableLoading);
            _unavailable = Message("train-timetable-unavailable", LocalizationKeys.Ui.BlockInventory.TimetableUnavailable);
            Add(_loading);
            Add(_unavailable);
            Show(_loading);
        }

        public void Render(TrainTimetableAreaViewModel model, string unnamedStationName)
        {
            if (model is not ReadyTrainTimetableArea ready)
            {
                RemoveSection();
                Show(model is LoadingTrainTimetableArea ? _loading : _unavailable);
                return;
            }

            // 列車が替わるたびに新しい区画を作る（Web の key=trainUnitId と同じく中の状態を持ち越さない）
            // A new section per train, carrying no inner state over (same as the Web key=trainUnitId)
            if (_section == null || !_sectionArea.IsSameTrain(ready))
            {
                RemoveSection();
                _section = new TrainTimetableSectionView(_commands);
                Add(_section);
            }
            _sectionArea = ready;
            Show(null);
            _section.Render(ready.Section, unnamedStationName);
        }

        private void Show(VisualElement message)
        {
            _loading.style.display = message == _loading ? DisplayStyle.Flex : DisplayStyle.None;
            _unavailable.style.display = message == _unavailable ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RemoveSection()
        {
            _section?.RemoveFromHierarchy();
            _section = null;
        }

        private static LocalizedLabel Message(string testid, LocalizationKey key)
        {
            var label = new LocalizedLabel { name = testid };
            label.SetKey(key);
            label.AddToClassList(UiTypographyClass.TextSm);
            return label;
        }
    }
}
```
（`_section` の null は「区画をまだ作っていない」を表す View 内の寿命で、外部データの null チェックではない。列車の比べは State の `ReadyTrainTimetableArea.IsSameTrain` で、View は `Game.Block.Interface` の型を持たない〈`Client.Ui.asmdef` に参照が無い。D-8〉）

`Timetable.uss`:

```css
/* web: features/blockInventory/train/style.module.css の行・操作・一覧・見出し（ローカルのつまみ無し） */
/* web: rows, controls, lists and headings from the Web train style module; no local knobs */
.train-timetable__list {
    flex-direction: column;
}

.train-timetable__row {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
}

.train-timetable__row > .train-timetable__row-end {
    margin-left: var(--mode-switch-padding-inline);
}

.train-timetable__label {
    flex-shrink: 1;
    min-width: 0;
    margin: 0;
    padding: 0;
    font-size: var(--mantine-font-size-md);
    white-space: normal;
}

.train-timetable-stop--current {
    color: var(--text-high-contrast);
}

.train-timetable__controls {
    flex-direction: row;
    flex-shrink: 0;
}

.train-timetable__controls > .train-timetable__control--following {
    margin-left: var(--mode-switch-gap);
}

.train-timetable__list > .train-timetable__row {
    padding: var(--mode-switch-padding-block) var(--mode-switch-padding-inline);
}

.train-timetable__list > .train-timetable__row--following {
    border-top-width: var(--bevel-1);
    border-top-color: var(--bevel-c1);
}

.train-timetable__section-heading {
    padding: 0 var(--mode-switch-padding-inline) var(--mode-switch-padding-block);
}

/* 無効な操作の文字は --text-muted（Web .list button:disabled）。部品のつまみを画面のルートで上書き（D-11） */
/* Disabled control text uses --text-muted (Web .list button:disabled) via the part knob at the screen root (D-11) */
.train-timetable .train-timetable__control:disabled {
    --panel-action-button-text: var(--text-muted);
    opacity: 1;
}
```
（行の右の子の間は `train-timetable__row-end` 1 本で取り、部品のクラスを主語にしない。停車駅の操作・駅一覧の `+`・自動運転の `ModeSwitch` の 3 つに C# で付ける。UITK の USS は `gap` と兄弟の結合子（`+`）を持たないので、2 個目以降の操作と 2 行目以降の行には C# が `train-timetable__control--following`・`train-timetable__row--following` を付ける（前例 `ModeSwitch` の `mode-switch__option--following`）。行ラベルの字は Web の型の付かない `<span>` が継ぐ Mantine の body 16px（`docs/uitk/TODO.md` の実測）なので `--mantine-font-size-md`。`border-top-width` に `var()` が使えなければ同値のトークンの解決後の値を書かず、`UssCustomStyleReader` で読んで C# が `style.borderTopWidth` に入れる。どちらにしても見た目は Web と同じにする）

- [ ] **Step 3: compile してコミット**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `ErrorCount: 0`（L1 は Task 7 Step 4 で本文ごと回す。`Timetable.uss` の取り込みは Task 8）

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Train/Timetable
git commit -m "feat(uitk): 時刻表の区画の View を足す（S4-train）

<実行ホストの指示する帰属行>"
```

---

### Task 7: 列車とプラットフォームの本文（`Screens/BlockInventory/Train/`・`Parts/`）

**Files:**
- Create: `S/Client.Ui/Screens/BlockInventory/Train/` の 7 本＋`Train.uss`、`Train/Parts/` の 2 本
- Create: `S/Client.Tests/Ui/Screens/BlockInventory/Train/TrainTestBodies.cs`・`TrainTestText.cs`・`TrainInventoryBodyTest.cs`・`TrainPlatformBodyTest.cs`、`Train/Timetable/TrainTimetableSectionViewTest.cs`

**Interfaces:**
- Consumes: alpha の `ITrainInventoryBodyFactory`・`IBlockInventoryBodyFactory`・`BlockInventoryBodyMount(bool fillsPanelHeight)`・`BlockItemGridFactory`、S4-machine の `FluidInventoryContents(testid, GameContentPresentation)`、Task 4〜6 の型
- Produces: `TrainInventoryBodyFactory : ITrainInventoryBodyFactory`、`TrainPlatformBodyFactory : IBlockInventoryBodyFactory`（Kind `TrainPlatform`）

Presenter は Model の `Current` を購読して View の `Render` へ流し、State の寿命とコマンドの中継を持たない（設計書 §4.2・A-8）。押下は View が State のコマンドの口（`ITrainTimetableEditCommands`・`ITrainPlatformEditCommands`）を直接呼ぶ。パネルごとの局所状態（列車の本文のタブと取得の契機 `TrainTimetableFetchTrigger`）だけは区画の Presenter が持ち、`IsSamePanel` が偽のパネルで作り直す（S4-machine の `MachineSelectionModeState` と同じ置き場。D-33）。

- [ ] **Step 1: 画面だけの部品を書く**

`Parts/TrainStationNameRow.cs`:

```csharp
using Client.Localization;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using Client.Ui.State.Train.Platform.Edit;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train.Parts
{
    /// <summary>
    /// 駅名の行（Web TrainStationNameSection の写し）。入力と決定は編集のコマンドを直接呼ぶ（A-8）。送るのは決定だけ
    /// Station name row (copy of Web TrainStationNameSection); input and Set call the edit commands directly (A-8) and only Set sends
    /// </summary>
    public sealed class TrainStationNameRow : VisualElement
    {
        private readonly GameTextField _input;

        public TrainStationNameRow(ITrainPlatformEditCommands commands)
        {
            name = "train-station-name-section";
            AddToClassList("train-station-name");
            var label = new LocalizedLabel();
            label.SetKey(LocalizationKeys.Ui.BlockInventory.StationNameLabel);
            label.AddToClassList(UiTypographyClass.TextSm);

            // 入力欄は自然な幅（Web は伸ばさない）。間は右の 2 つの子に付ける画面のクラスで取る
            // The field keeps its natural width (Web does not stretch it); gaps come from a screen class on the two right children
            _input = new GameTextField("train-station-name-input", false, GameTextFieldLook.GaugeInset);
            _input.SetPlaceholder(new UiText(LocalizationKeys.Ui.BlockInventory.StationNamePlaceholder));
            _input.AddToClassList("train-station-name__row-end");
            _input.AddToClassList("train-station-name__input");
            _input.OnValueChanged.Subscribe(commands.EditStationName);
            var apply = new PanelActionButton("train-station-name-apply", LocalizationKeys.Ui.BlockInventory.StationNameApply);
            apply.AddToClassList("train-station-name__row-end");
            apply.OnClicked.Subscribe(_ => commands.ApplyStationName());
            Add(label);
            Add(_input);
            Add(apply);
        }

        public void Render(string input)
        {
            // 入力中（IME の組成を含む）の値と同じなら触らない
            // Leave the field alone when it already holds the value, including during IME composition
            if (_input.Field.value != input) _input.SetValueWithoutNotify(input);
        }
    }
}
```

`Parts/TrainPlatformModeSection.cs`:

```csharp
using Client.Localization;
using Client.Ui.Components.Controls;
using Client.Ui.State.Text;
using Client.Ui.State.Train.Platform;
using Client.Ui.State.Train.Platform.Edit;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train.Parts
{
    /// <summary>
    /// 積込/卸しの切替（Web TrainPlatformSection の写し）。押下は編集のコマンドを直接呼び、選択はサーバーの状態でだけ替わる
    /// Load/unload switch (copy of Web TrainPlatformSection); presses call the edit commands directly and the selection follows the server only
    /// </summary>
    public sealed class TrainPlatformModeSection : VisualElement
    {
        private readonly ModeSwitch<TrainPlatformTransferMode> _mode;

        public TrainPlatformModeSection(ITrainPlatformEditCommands commands)
        {
            // 選択肢の testid は Web に無いので -load / -unload を足す（D-7）
            // Web options carry no testids, so -load / -unload are added (D-7)
            name = "train-platform-section";
            _mode = new ModeSwitch<TrainPlatformTransferMode>("train-platform-mode", ModeSwitchOrientation.Horizontal, ModeSwitchRole.Group, new[]
            {
                new ModeSwitchOption<TrainPlatformTransferMode>("train-platform-mode-load", TrainPlatformTransferMode.LoadToTrain, new UiText(LocalizationKeys.Ui.BlockInventory.TrainPlatformLoad)),
                new ModeSwitchOption<TrainPlatformTransferMode>("train-platform-mode-unload", TrainPlatformTransferMode.UnloadToPlatform, new UiText(LocalizationKeys.Ui.BlockInventory.TrainPlatformUnload)),
            });
            _mode.OnOptionSelected.Subscribe(mode => commands.SelectTransferMode(mode));
            Add(_mode);
        }

        public void Render(TrainPlatformTransferMode mode)
        {
            _mode.SetSelected(mode);
        }
    }
}
```

- [ ] **Step 2: 列車の本文を書く**

`TrainInventoryContentsView.cs`:

```csharp
using Client.Ui.Screens.BlockInventory.Fluid;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.Train.Inventory;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train
{
    /// <summary>
    /// 持ち物タブ。液体貨車は液体の中身、それ以外は貨車スロット（Web TrainInventoryBody と同じ分岐）
    /// Inventory tab: fluid cars show fluid contents, others show car slots (same branch as Web TrainInventoryBody)
    /// </summary>
    public sealed class TrainInventoryContentsView : VisualElement
    {
        private readonly BlockItemGrid _grid;
        private readonly FluidInventoryContents _fluids;

        public TrainInventoryContentsView(BlockItemGrid grid, FluidInventoryContents fluids)
        {
            name = "train-inventory-contents";
            _grid = grid;
            _fluids = fluids;
            Add(_grid);
            Add(_fluids);
        }

        public void Render(TrainInventoryBodyViewModel model)
        {
            var hasFluids = 0 < model.Fluids.Count;
            _grid.style.display = hasFluids ? DisplayStyle.None : DisplayStyle.Flex;
            _fluids.style.display = hasFluids ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasFluids) _fluids.Render(model.Fluids);
            else _grid.Render(model.ItemSlots, BlockDetailMath.AllSlotIndices(model.ItemSlots.Count));
        }
    }
}
```

`TrainInventoryBodyView.cs`:

```csharp
using System;
using Client.Localization;
using Client.Ui.Components.Controls;
using Client.Ui.Screens.BlockInventory.Train.Timetable;
using Client.Ui.State.Text;
using Client.Ui.State.Train.Inventory;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train
{
    /// <summary>
    /// 列車の本文（Web TrainInventoryBody の写し）。タブとスクロール器の中に持ち物と時刻表。タブの押下は Presenter の局所状態へ流す（D-33）
    /// Train body (copy of Web TrainInventoryBody): tabs, then inventory and timetable inside the scroll view; tab presses flow to the Presenter's local state (D-33)
    /// </summary>
    public sealed class TrainInventoryBodyView : VisualElement
    {
        private readonly ModeSwitch<TrainInventoryTab> _tabs;

        public TrainInventoryContentsView Contents { get; }
        public TrainTimetableAreaView Timetable { get; }
        public IObservable<TrainInventoryTab> OnTabSelected => _tabs.OnOptionSelected;

        public TrainInventoryBodyView(TrainInventoryContentsView contents, TrainTimetableAreaView timetable)
        {
            name = "train-inventory-body";
            AddToClassList("train-body");
            Contents = contents;
            Timetable = timetable;
            _tabs = new ModeSwitch<TrainInventoryTab>("train-tabs", ModeSwitchOrientation.Horizontal, ModeSwitchRole.Tablist, new[]
            {
                new ModeSwitchOption<TrainInventoryTab>("train-tab-inventory", TrainInventoryTab.Inventory, new UiText(LocalizationKeys.Ui.BlockInventory.TrainTabInventory)),
                new ModeSwitchOption<TrainInventoryTab>("train-tab-timetable", TrainInventoryTab.Timetable, new UiText(LocalizationKeys.Ui.BlockInventory.TrainTabTimetable)),
            });

            // 時刻表は持ち物タブのあいだも作ったまま隠す（Web の hidden。D-5）
            // The timetable stays built but hidden on the inventory tab (Web hidden; D-5)
            var scroll = new GameScrollView("train-body-scroll");
            scroll.AddToClassList("train-body__scroll");
            scroll.Add(Contents);
            scroll.Add(Timetable);
            Add(_tabs);
            Add(scroll);
        }

        public void RenderTab(TrainInventoryTab tab)
        {
            _tabs.SetSelected(tab);
            Contents.style.display = tab == TrainInventoryTab.Inventory ? DisplayStyle.Flex : DisplayStyle.None;
            Timetable.style.display = tab == TrainInventoryTab.Timetable ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
```

`TrainInventoryBodyPresenter.cs`（タブと取得の契機はパネルごとの局所状態なので、State に Model を置かずこの Presenter が `TrainTimetableFetchTrigger` を 1 つ持つ。前例は S4-machine の `MachineSelectionModeState`。D-33）:

```csharp
using Mooresmaster.Localization.Generated;
using System;
using Client.Localization;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Train.Inventory;
using Client.Ui.State.Train.Timetable;
using Client.Ui.State.Train.TimetableEdit;
using Client.Ui.State.Train.TimetableEdit.Send;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train
{
    /// <summary>
    /// 列車の本文の Presenter。タブの局所状態と取得の契機を持ち、中身・時刻表の Model を購読して View へ流す
    /// Presenter of the train body: owns the local tab state and fetch trigger, and subscribes the contents and timetable models into the View
    /// </summary>
    public sealed class TrainInventoryBodyPresenter : ITrainInventoryPart
    {
        private readonly TrainInventoryBodyView _view;
        private readonly ITrainInventoryBodySource _body;
        private readonly ITrainTimetableEditModel _timetable;
        private readonly ITrainTimetableSource _timetableSource;
        private readonly ITrainTimetableCommands _timetableCommands;
        private readonly CompositeDisposable _subscriptions = new();
        private readonly SerialDisposable _shownTabSubscription = new();
        private TrainTimetableFetchTrigger _trigger = new();
        private BlockInventoryPanelState _panel = BlockInventoryPanelState.Closed;

        public VisualElement Root => _view;

        public TrainInventoryBodyPresenter(TrainInventoryBodyView view, ITrainInventoryBodySource body, ITrainTimetableEditModel timetable, ITrainTimetableSource timetableSource, ITrainTimetableCommands timetableCommands)
        {
            _view = view;
            _body = body;
            _timetable = timetable;
            _timetableSource = timetableSource;
            _timetableCommands = timetableCommands;
        }

        public void Attach(OpenTrainInventoryPanel panel)
        {
            // 別のパネルでは持ち物タブから（Web は本文の mount ごとに初期化）。隠して戻した同じ車両はタブを保つ
            // A different panel starts on the inventory tab (Web resets per body mount); the same car shown again keeps its tab
            if (!panel.IsSamePanel(_panel)) _trigger = new TrainTimetableFetchTrigger();
            _panel = panel;
            _view.OnTabSelected.Subscribe(SelectTab).AddTo(_subscriptions);
            _timetableSource.Current.Subscribe(state =>
            {
                if (_trigger.ReceiveState(state)) _timetableCommands.RequestOpen();
            }).AddTo(_subscriptions);
            RenderTab(_trigger.Tab);
        }

        public void Detach()
        {
            _subscriptions.Clear();
            _shownTabSubscription.Disposable = null;
        }

        // 取得中の表示は Fetcher の開始の合図で源が組み直して出す（D-20）。重複の依頼は Fetcher が畳む
        // Loading shows when the source rebuilds on the Fetcher's start notification (D-20); the Fetcher folds duplicates
        private void SelectTab(TrainInventoryTab tab)
        {
            var enteredTimetable = _trigger.SelectTab(tab);
            RenderTab(tab);
            if (enteredTimetable) _timetableCommands.RequestOpen();
        }

        // 表示中のタブの Model だけを購読する。非表示の間に描かないのは Presenter の購読で守る（設計書 §4.2 の SetShown の注記。購読の頭で最新値を 1 回描く）
        // Subscribe only the shown tab's model; the Presenter's subscription keeps hidden tabs from drawing (design doc §4.2 SetShown note; the latest value draws once on subscribe)
        private void RenderTab(TrainInventoryTab tab)
        {
            _view.RenderTab(tab);
            _shownTabSubscription.Disposable = tab == TrainInventoryTab.Timetable ? SubscribeTimetable() : SubscribeContents();
        }

        private IDisposable SubscribeContents()
        {
            return _body.Current.Subscribe(_view.Contents.Render);
        }

        private IDisposable SubscribeTimetable()
        {
            var subscriptions = new CompositeDisposable();
            _timetable.Current.Subscribe(_ => RenderTimetable()).AddTo(subscriptions);
            Localize.OnLanguageChanged.Subscribe(_ => RenderTimetable()).AddTo(subscriptions);
            return subscriptions;
        }

        // 無名の駅の名前は言語で変わるので、言語の変化でも描き直す（D-10）
        // The unnamed station name localizes, so language changes redraw too (D-10)
        private void RenderTimetable()
        {
            _view.Timetable.Render(_timetable.Current.Value, Localize.Get(LocalizationKeys.Ui.BlockInventory.TimetableUnnamedStation));
        }
    }
}
```
（alpha は同じパネル・同じ大きさのあいだ何も呼ばず、変わると旧部分の `Detach` → 新しく `Create` → `Attach`、画面が隠れると `Detach` する〈alpha 計画「後続レーンへの Produces 一覧」の呼ばれ方〉。別のパネルは新しい Presenter になるので契機も新しくなり、`IsSamePanel` の比べは同じ Presenter へ別のパネルが Attach されたときの守り。時刻表の源は購読した瞬間に今の値を流し、契機が時刻表タブで読込中なら頼む〈隠して戻したとき。Web の effect も mount で走る〉）

`TrainInventoryBodyFactory.cs`:

```csharp
using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.Fluid;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.Train.Timetable;
using Client.Ui.State.Train.Inventory;
using Client.Ui.State.Train.Timetable;
using Client.Ui.State.Train.TimetableEdit;
using Client.Ui.State.Train.TimetableEdit.Send;

namespace Client.Ui.Screens.BlockInventory.Train
{
    /// <summary>
    /// source=train の本文を作る（alpha の拡張口）。Presenter と View の組を返す
    /// Creates the source=train body for alpha's extension point, returning a Presenter and View pair
    /// </summary>
    public sealed class TrainInventoryBodyFactory : ITrainInventoryBodyFactory
    {
        private readonly ITrainInventoryBodySource _body;
        private readonly ITrainTimetableEditModel _timetable;
        private readonly ITrainTimetableEditCommands _timetableEdit;
        private readonly ITrainTimetableSource _timetableSource;
        private readonly ITrainTimetableCommands _timetableCommands;
        private readonly BlockItemGridFactory _gridFactory;
        private readonly GameContentPresentation _content;

        public TrainInventoryBodyFactory(ITrainInventoryBodySource body, ITrainTimetableEditModel timetable, ITrainTimetableEditCommands timetableEdit, ITrainTimetableSource timetableSource, ITrainTimetableCommands timetableCommands, BlockItemGridFactory gridFactory, GameContentPresentation content)
        {
            _body = body;
            _timetable = timetable;
            _timetableEdit = timetableEdit;
            _timetableSource = timetableSource;
            _timetableCommands = timetableCommands;
            _gridFactory = gridFactory;
            _content = content;
        }

        public ITrainInventoryPart Create()
        {
            var contents = new TrainInventoryContentsView(_gridFactory.Create("train-inventory-slots"), new FluidInventoryContents("train-inventory-fluids", _content));
            var view = new TrainInventoryBodyView(contents, new TrainTimetableAreaView(_timetableEdit));
            return new TrainInventoryBodyPresenter(view, _body, _timetable, _timetableSource, _timetableCommands);
        }
    }
}
```
（alpha D-35 により、この工場が登録されていないあいだ alpha のレジストリは null ではなく空の部分 `EmptyTrainInventoryPart` を返す。登録後はこの工場の部分が使われる）

- [ ] **Step 3: プラットフォームの本文を書く**

`TrainPlatformBodyView.cs`:

```csharp
using Client.Ui.Foundation.Layout;
using Client.Ui.Screens.BlockInventory.Fluid;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Train.Parts;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.Train.Platform;
using Client.Ui.State.Train.Platform.Edit;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train
{
    /// <summary>
    /// 駅・プラットフォームの本文（Web TrainPlatformInventory の写し）。駅名 → スロット → 液体 → モード。部分は「あれば出す」の 1 規則
    /// Station and platform body (copy of Web TrainPlatformInventory): name, slots, fluids, mode; each part follows "show when present"
    /// </summary>
    public sealed class TrainPlatformBodyView : VisualElement
    {
        private readonly UiStack _stack = new(UiGap.Sm);
        private readonly TrainStationNameRow _nameRow;
        private readonly BlockItemGrid _grid;
        private readonly FluidInventoryContents _fluids;
        private readonly TrainPlatformModeSection _mode;

        public TrainPlatformBodyView(BlockItemGrid grid, FluidInventoryContents fluids, ITrainPlatformEditCommands commands)
        {
            name = "train-platform-view";
            _nameRow = new TrainStationNameRow(commands);
            _grid = grid;
            _fluids = fluids;
            _mode = new TrainPlatformModeSection(commands);
            _stack.AddItem(_nameRow);
            _stack.AddItem(_grid);
            _stack.AddItem(_fluids);
            _stack.AddItem(_mode);
            Add(_stack);
        }

        public void Render(TrainPlatformBodyState state, string stationNameInput)
        {
            // 閉じた状態ではどの部分も出さない（エラー行は alpha の外殻が出す）
            // A closed state shows no part (alpha's shell shows the error row)
            if (state is not OpenTrainPlatformBody open)
            {
                _stack.SetItemShown(_nameRow, false);
                _stack.SetItemShown(_grid, false);
                _stack.SetItemShown(_fluids, false);
                _stack.SetItemShown(_mode, false);
                return;
            }

            // 出さない部分は Stack から隠し、間の余白も詰める
            // Hidden parts leave the stack so their gaps collapse too
            _stack.SetItemShown(_nameRow, open.StationName.IsPresent);
            _stack.SetItemShown(_grid, open.ItemSlots.IsPresent);
            _stack.SetItemShown(_fluids, 0 < open.Fluids.Count);
            _stack.SetItemShown(_mode, open.Mode.IsPresent);
            if (open.StationName.IsPresent) _nameRow.Render(stationNameInput);
            if (open.ItemSlots.IsPresent) _grid.Render(open.ItemSlots.Value, BlockDetailMath.AllSlotIndices(open.ItemSlots.Value.Count));
            _fluids.Render(open.Fluids);
            if (open.Mode.IsPresent) _mode.Render(open.Mode.Value);
        }
    }
}
```

`TrainPlatformBodyPresenter.cs`:

```csharp
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Train.Platform;
using Client.Ui.State.Train.Platform.Edit;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train
{
    /// <summary>
    /// 駅・プラットフォームの本文の Presenter。本文の状態と駅名の入力を購読して View へ流すだけ（下書き・送信は State。D-33）
    /// Presenter of the station and platform body: only subscribes the body state and the name input into the View (draft and sends stay in State; D-33)
    /// </summary>
    public sealed class TrainPlatformBodyPresenter : IBlockInventoryPart
    {
        private readonly TrainPlatformBodyView _view;
        private readonly ITrainPlatformBodySource _source;
        private readonly ITrainPlatformEditModel _edit;
        private readonly CompositeDisposable _subscriptions = new();

        public VisualElement Root => _view;

        public TrainPlatformBodyPresenter(TrainPlatformBodyView view, ITrainPlatformBodySource source, ITrainPlatformEditModel edit)
        {
            _view = view;
            _source = source;
            _edit = edit;
        }

        // ブロックの識別は Model がパネルの源から読むので、ここでは使わない
        // Models read the block identity from the panel source, so it is unused here
        public void Attach(OpenBlockInventoryPanel panel)
        {
            _source.Current.Subscribe(_ => Render()).AddTo(_subscriptions);
            _edit.StationNameInput.Subscribe(_ => Render()).AddTo(_subscriptions);
        }

        public void Detach()
        {
            _subscriptions.Clear();
        }

        private void Render()
        {
            _view.Render(_source.Current.Value, _edit.StationNameInput.Value);
        }
    }
}
```

`TrainPlatformBodyFactory.cs`:

```csharp
using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.Fluid;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.State.Train.Platform;
using Client.Ui.State.Train.Platform.Edit;

namespace Client.Ui.Screens.BlockInventory.Train
{
    /// <summary>
    /// TrainStation・TrainItemPlatform・TrainFluidPlatform の本文を作る（Kind TrainPlatform）
    /// Creates the body for TrainStation, TrainItemPlatform and TrainFluidPlatform (Kind TrainPlatform)
    /// </summary>
    public sealed class TrainPlatformBodyFactory : IBlockInventoryBodyFactory
    {
        private readonly ITrainPlatformBodySource _source;
        private readonly ITrainPlatformEditModel _edit;
        private readonly ITrainPlatformEditCommands _commands;
        private readonly BlockItemGridFactory _gridFactory;
        private readonly GameContentPresentation _content;

        public BlockInventoryBodyKind Kind => BlockInventoryBodyKind.TrainPlatform;

        public TrainPlatformBodyFactory(ITrainPlatformBodySource source, ITrainPlatformEditModel edit, ITrainPlatformEditCommands commands, BlockItemGridFactory gridFactory, GameContentPresentation content)
        {
            _source = source;
            _edit = edit;
            _commands = commands;
            _gridFactory = gridFactory;
            _content = content;
        }

        public IBlockInventoryPart Create(BlockInventoryBodyMount mount)
        {
            var view = new TrainPlatformBodyView(_gridFactory.Create("train-platform-item-slots"), new FluidInventoryContents("train-platform-fluid-slots", _content), _commands);
            return new TrainPlatformBodyPresenter(view, _source, _edit);
        }
    }
}
```

`Train.uss`:

```css
/* web: features/blockInventory/train/style.module.css .body と details/station/trainStationNameSection.module.css .row（ローカルのつまみ無し） */
/* web: the Web train .body and the station name .row; no local knobs */
.train-body {
    flex-direction: column;
    flex-grow: 1;
    min-height: 0;
}

.train-body__scroll {
    flex-grow: 1;
    min-height: 0;
    margin-top: var(--mode-switch-gap);
}

.train-station-name {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
}

.train-station-name > .train-station-name__row-end {
    margin-left: var(--mode-switch-padding-inline);
}
```
（入力欄は伸ばさず自然な幅のまま〈Web は `flex-grow` を当てていない〉。Task 10 で Web 側の自撮りの入力欄の幅を測り、UITK の自然な幅と違えば、同値の既存トークンか `Foundation/Theme/DerivedTokens.uss` の名札付きブロック〈根拠のコメントつき。E-1〉にトークンを足して `.train-station-name__input` の `width` に当てる。差が残ったときだけ LX にする）

- [ ] **Step 4: 失敗するテストを書く**

`TrainTestBodies.cs`:

```csharp
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.Train;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Toast;
using Client.Ui.State.Train.Platform.Edit;
using Client.Ui.State.Train.TimetableEdit;
using UniRx;

namespace Client.Tests.Ui.Screens.BlockInventory.Train
{
    /// <summary>
    /// 列車とプラットフォームの本文を、偽の源・偽の送信の上に本物の編集の器・Model・コマンドを本番どおり組んで作る（D-33）
    /// Builds the train and platform bodies with the real edit holders, models and commands over fake sources and fake senders, wired as in production (D-33)
    /// </summary>
    public sealed class TrainTestBodies
    {
        public FakeTrainSources Sources { get; } = new();
        public FakeTrainTimetableCommands TimetableCommands { get; } = new();
        public FakeTrainBlockCommands BlockCommands { get; } = new();
        public ReactiveProperty<BlockInventoryPanelState> Panel { get; }
        public TrainTimetableEditModel TimetableEdit { get; }
        public TrainTimetableEditCommands TimetableEditCommands { get; }
        public TrainPlatformEditModel PlatformEdit { get; }
        public TrainPlatformEditCommands PlatformEditCommands { get; }

        public TrainTestBodies(UiCommandResultToaster toaster, BlockInventoryPanelState panel)
        {
            // 本番と同じく器が源を一度だけ購読し、Model とコマンドは器を共有する
            // As in production the holders subscribe to their sources once, and models and commands share them
            Panel = new ReactiveProperty<BlockInventoryPanelState>(panel);
            var panels = new FakePanelSource(Panel);
            var timetableState = new TrainTimetableEditState(Sources, TimetableCommands, new ManualTrainTimetableWaitTimer(), toaster);
            TimetableEdit = new TrainTimetableEditModel(timetableState);
            TimetableEditCommands = new TrainTimetableEditCommands(timetableState, TimetableCommands, toaster);
            timetableState.Initialize();
            TimetableEdit.Initialize();
            var stationNameState = new TrainStationNameEditState(panels, Sources);
            PlatformEdit = new TrainPlatformEditModel(stationNameState);
            PlatformEditCommands = new TrainPlatformEditCommands(stationNameState, BlockCommands, toaster);
            stationNameState.Initialize();
        }

        public TrainInventoryBodyFactory TrainBodyFactory(BlockItemGridFactory grids, GameContentPresentation content)
        {
            return new TrainInventoryBodyFactory(Sources, TimetableEdit, TimetableEditCommands, Sources, TimetableCommands, grids, content);
        }

        public ITrainInventoryPart CreateTrainBody(BlockItemGridFactory grids, GameContentPresentation content)
        {
            return TrainBodyFactory(grids, content).Create();
        }

        public IBlockInventoryPart CreatePlatformBody(BlockItemGridFactory grids, GameContentPresentation content)
        {
            return new TrainPlatformBodyFactory(Sources, PlatformEdit, PlatformEditCommands, grids, content).Create(new BlockInventoryBodyMount(false));
        }
    }
}
```
（`BlockItemGridFactory`・`GameContentPresentation` のテストでの組み方は alpha の `BlockInventoryTestBodies` と S4-machine の `FluidSectionsTest` の前例に合わせる。`BlockInventoryTestParts`〈`Grids()` と `Content()` を返す小さな補助〉は alpha・S4-machine のテスト補助に同等のものがあればそれを使い、無ければこのファイルの下に置く）

`Timetable/TrainTimetableSectionViewTest.cs`（`TrainTimetableImmediate.test.ts` 7・8 件目。Web と同じ順と主張）:

```csharp
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Train.Timetable;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.Train.Fakes.TrainTestData;

namespace Client.Tests.Ui.Screens.BlockInventory.Train.Timetable
{
    public class TrainTimetableSectionViewTest : ScreenTestHost
    {
        private TrainTestBodies _bodies;
        private ITrainInventoryPart _body;

        [SetUp]
        public void OpenTimetableTab()
        {
            _bodies = new TrainTestBodies(Toaster, BlockInventoryTestPanels.Train(BlockInventoryPanelError.None));
            _body = _bodies.CreateTrainBody(BlockInventoryTestParts.Grids(), BlockInventoryTestParts.Content());
            Mount(_body.Root);
            _body.Attach(BlockInventoryTestPanels.Train(BlockInventoryPanelError.None));
            simulate.Click(Find<Button>(_body.Root, "train-tab-timetable"));
        }

        [TearDown]
        public void Close()
        {
            _body.Detach();
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableImmediate.test.ts">blocks auto-run until an added stop is authoritative and sends toggles immediately</web-test>
        [Test]
        public void 追加が確定するまで自動運転ONを塞ぎ切替は即送る()
        {
            // 停車駅 0 件では ON が無効で、理由の文が出る
            // With no stops, ON is disabled and the reason text shows
            Publish(Ready(Unit1, false, NoStops));
            Assert.IsFalse(Find<Button>(_body.Root, "train-timetable-auto-run-on").enabledSelf);
            Assert.IsTrue(IsShown(_body.Root.Q<Label>("train-timetable-auto-run-unavailable")));

            // 追加は楽観表示でも ON を開けない。受理と公開で確定してから開く
            // An optimistic add does not open ON; it opens once acceptance and the publication confirm it
            simulate.Click(Find<Button>(_body.Root, "train-timetable-station-12_0_40-add"));
            Assert.IsFalse(Find<Button>(_body.Root, "train-timetable-auto-run-on").enabledSelf);
            _bodies.TimetableCommands.Accept();
            Publish(NorthOnly(Unit1));
            Assert.IsTrue(Find<Button>(_body.Root, "train-timetable-auto-run-on").enabledSelf);

            // 押すと楽観更新なしで即送る
            // Pressing sends immediately without an optimistic update
            simulate.Click(Find<Button>(_body.Root, "train-timetable-auto-run-on"));
            CollectionAssert.AreEqual(new[] { true }, _bodies.TimetableCommands.SentAutoRuns);
            Assert.IsTrue(IsSelected(_body.Root, "train-timetable-auto-run-off"));

            // 自動運転 ON が公開された後は OFF を押すと false を送る
            // After auto-run ON is published, pressing OFF sends false
            _bodies.TimetableCommands.Accept();
            Publish(Ready(Unit1, true, Added(NoStops, North)));
            simulate.Click(Find<Button>(_body.Root, "train-timetable-auto-run-off"));
            CollectionAssert.AreEqual(new[] { true, false }, _bodies.TimetableCommands.SentAutoRuns);
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableImmediate.test.ts">disables moves past either end and follows remote changes when idle</web-test>
        [Test]
        public void 端を越える移動を塞ぎ待ちが無いときは外の変更に従う()
        {
            // 1 駅では上も下も無効
            // With one stop both up and down are disabled
            Publish(NorthOnly(Unit1));
            Assert.IsFalse(Find<Button>(_body.Root, "train-timetable-stop-0-up").enabledSelf);
            Assert.IsFalse(Find<Button>(_body.Root, "train-timetable-stop-0-down").enabledSelf);

            // 待ちが無ければ別の画面からの変更（2 駅）をそのまま映し、何も送らない
            // When idle, a change from elsewhere (two stops) shows as is and nothing is sent
            Publish(Ready(Unit1, false, Added(Added(NoStops, North), Unnamed)));
            Assert.IsNotNull(_body.Root.Q("train-timetable-stop-1"));
            Assert.IsTrue(Find<Button>(_body.Root, "train-timetable-stop-0-down").enabledSelf);
            Assert.IsFalse(Find<Button>(_body.Root, "train-timetable-stop-1-down").enabledSelf);
            Assert.AreEqual(0, _bodies.TimetableCommands.SentReplaces.Count);
            Assert.AreEqual(0, _bodies.TimetableCommands.SentAutoRuns.Count);
        }

        private void Publish(TrainTimetableReadyData data)
        {
            _bodies.Sources.SetTimetable(TrainTimetableViewState.Ready(data));
            simulate.FrameUpdate();
        }
    }
}
```
（`Find`・`IsShown`・`IsSelected(root, optionTestid)` は `ScreenTestHost` の実物〈`IsSelected` は 2 引数〉。`simulate.Click` は前例 `BugReportRecoveryControlsViewTest`）

`TrainTestText.cs`（木の文字を集める補助。前例 `ModeHudViewTest.cs:123`。`UiTextAssert` は `AreEqual` しか持たない）:

```csharp
using System;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Core.Master;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Train
{
    /// <summary>
    /// 本文のテストで文字と液体の表示を読む補助
    /// Helpers that read texts and fluid display in the body tests
    /// </summary>
    public static class TrainTestText
    {
        // テスト用 mod の水。液体の Guid の定数クラスは無いので前例（FluidMasterEndpointTest.TestWaterFluidGuid）と同じく直書きする
        // The test mod's water; there is no fluid Guid constants class, so it is written inline like the precedent FluidMasterEndpointTest.TestWaterFluidGuid
        public static readonly Guid WaterFluidGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        // 要素とその子孫の文字を空白でつなぐ（量・容量の文は枠の兄弟なので液体の器から、枠内の数字は枠の要素から読む）
        // Joins the texts of an element and its descendants (amount text is a sibling of the slot, so read from the fluid container)
        public static string TextsUnder(VisualElement root)
        {
            return string.Join(" ", root.Query<TextElement>().ToList().ConvertAll(e => e.text));
        }

        // 液体の表示名は本文と同じ引き方（GameContentPresentation の液体の UiIcon が持つ名前）
        // The fluid display name is read the same way as the body (the name carried by GameContentPresentation's fluid UiIcon)
        public static string FluidName(FluidId fluidId)
        {
            return BlockInventoryTestParts.Content().Fluid(fluidId).Label.Resolve();
        }

        // 枠にアイコンが出ているか（空の枠は GameIcon.ClearIcon で絵の状態クラスを外す。C4-a の FluidSlot.ShowEmpty）
        // Whether the slot shows an icon (an empty slot clears GameIcon's picture state class; C4-a FluidSlot.ShowEmpty)
        public static bool HasFluidIcon(VisualElement slotRow)
        {
            var icon = slotRow.Q(className: "fluid-slot__icon");
            return icon != null && icon.ClassListContains("game-icon--image");
        }
    }
}
```
（液体名は Foundation の既存 `UiIcon.Label`〈公開メンバは `Texture`・`IdText`・`Label` の 3 つ。`Client.Ui/Foundation/Icons/UiIcon.cs:14-16`、前例 `Client.Tests/Ui/Content/GameContentPresentationTest.cs:81` の `.Label.Resolve()`〉。アイコンの要素のクラス名〈`fluid-slot__icon`〉と「出ている」の印〈`game-icon--image`〉は C4-a 計画〈`FluidSlot` の `_icon.AddToClassList("fluid-slot__icon")`・空で `ClearIcon`〉から写した。C4-a の `FluidSlot`／`FluidSlotRow` の実物と、S4-machine の `FluidInventoryContents` が行の中に `FluidSlot` を持つことを Task 1 Step 2 で確かめ、違えば「名前の合わせ」で合わせる。テストの液体にテクスチャが無く `--image` が立たない場合は、絵の有無でなく `HoverTooltipMark` の印の有無で見る形に替え、`done.md`「名前の合わせ」に書く）

`TrainInventoryBodyTest.cs`（8 件。後ろの 3 件は Web の写しではなく、D-27 の主張と、State から Presenter へ移したタブの局所状態〈D-33〉の主張）:

```csharp
using System.Collections.Generic;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Train.Inventory;
using Client.Ui.State.Train.Timetable;
using Core.Master;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.Train.Fakes.TrainTestData;

namespace Client.Tests.Ui.Screens.BlockInventory.Train
{
    public class TrainInventoryBodyTest : ScreenTestHost
    {
        private TrainTestBodies _bodies;
        private ITrainInventoryPart _body;

        [SetUp]
        public void MountBody()
        {
            _bodies = new TrainTestBodies(Toaster, BlockInventoryTestPanels.Train(BlockInventoryPanelError.None));
            _body = _bodies.CreateTrainBody(BlockInventoryTestParts.Grids(), BlockInventoryTestParts.Content());
            Mount(_body.Root);
            _body.Attach(BlockInventoryTestPanels.Train(BlockInventoryPanelError.None));
            simulate.FrameUpdate();
        }

        [TearDown]
        public void DetachBody()
        {
            _body.Detach();
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableSection.test.ts">asks C# to fetch only when the timetable tab is selected</web-test>
        [Test]
        public void 時刻表タブを選んだときだけ取得を頼む()
        {
            // 持ち物タブのあいだは読込中が届いても頼まない
            // On the inventory tab a loading state does not ask
            _bodies.Sources.SetTimetable(TrainTimetableViewState.Loading);
            Assert.AreEqual(0, _bodies.TimetableCommands.OpenRequestCount);
            Assert.IsTrue(IsSelected(_body.Root, "train-tab-inventory"));

            // 持ち物の器は子ごと隠れる（IsShown は要素自身の display を見るので器で確かめる）
            // The inventory container hides with its children (IsShown reads the element's own display, so check the container)
            simulate.Click(Find<Button>(_body.Root, "train-tab-timetable"));
            simulate.FrameUpdate();
            Assert.AreEqual(1, _bodies.TimetableCommands.OpenRequestCount);
            Assert.IsTrue(IsShown(_body.Root.Q("train-timetable-loading")));
            Assert.IsFalse(IsShown(_body.Root.Q("train-inventory-contents")));

            // 持ち物タブへ戻しても頼まない
            // Going back to the inventory tab does not ask
            simulate.Click(Find<Button>(_body.Root, "train-tab-inventory"));
            Assert.AreEqual(1, _bodies.TimetableCommands.OpenRequestCount);
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableSection.test.ts">asks C# to fetch on tab select even when the timetable is already ready</web-test>
        [Test]
        public void 用意済みでもタブ選択では取得を頼む()
        {
            _bodies.Sources.SetTimetable(TrainTimetableViewState.Ready(NorthOnly(Unit1)));
            simulate.Click(Find<Button>(_body.Root, "train-tab-timetable"));
            simulate.FrameUpdate();
            Assert.AreEqual(1, _bodies.TimetableCommands.OpenRequestCount);
            Assert.IsTrue(IsShown(_body.Root.Q("train-timetable-section")));
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableSection.test.ts">re-asks C# when a reset fetch arrives as loading while the timetable tab stays selected</web-test>
        [Test]
        public void 時刻表タブのあいだに読込中へ戻れば頼み直す()
        {
            _bodies.Sources.SetTimetable(TrainTimetableViewState.Ready(NorthOnly(Unit1)));
            simulate.Click(Find<Button>(_body.Root, "train-tab-timetable"));
            _bodies.Sources.SetTimetable(TrainTimetableViewState.Loading);
            Assert.AreEqual(2, _bodies.TimetableCommands.OpenRequestCount);
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableSection.test.ts">shows loading and unavailable as distinct states without warning</web-test>
        [Test]
        public void 読込中と使えないを別の文で出し警告しない()
        {
            simulate.Click(Find<Button>(_body.Root, "train-tab-timetable"));
            _bodies.Sources.SetTimetable(TrainTimetableViewState.Loading);
            simulate.FrameUpdate();
            Assert.IsTrue(IsShown(_body.Root.Q("train-timetable-loading")));
            Assert.IsFalse(IsShown(_body.Root.Q("train-timetable-unavailable")));

            _bodies.Sources.SetTimetable(TrainTimetableViewState.Unavailable);
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(_body.Root.Q("train-timetable-loading")));
            Assert.IsTrue(IsShown(_body.Root.Q("train-timetable-unavailable")));
            LogAssert.NoUnexpectedReceived();
        }

        /// <web-test file="src/features/blockInventory/train/TrainInventoryBody.test.tsx">液体貨車は名前と量容量を表示し、再取得した量へ更新する</web-test>
        [Test]
        public void 液体貨車は名前と量容量を出し再取得で描き直す()
        {
            var water = MasterHolder.FluidMaster.GetFluidId(TrainTestText.WaterFluidGuid);
            _bodies.Sources.SetBody(new TrainInventoryBodyViewModel(new List<SlotItem>(), new[] { BlockFluidSlotState.Filled(water, 10, 1000) }));
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(_body.Root.Q("train-inventory-slots")));
            StringAssert.Contains(TrainTestText.FluidName(water), FluidTexts());
            StringAssert.Contains("10 / 1,000", FluidTexts());
            Assert.IsTrue(TrainTestText.HasFluidIcon(_body.Root.Q("train-inventory-fluids-slot-0")));

            // 再取得した量へ描き直す
            // Redraws with the refetched amount
            _bodies.Sources.SetBody(new TrainInventoryBodyViewModel(new List<SlotItem>(), new[] { BlockFluidSlotState.Filled(water, 90, 1000) }));
            simulate.FrameUpdate();
            StringAssert.Contains("90 / 1,000", FluidTexts());
            StringAssert.Contains("90", TrainTestText.TextsUnder(_body.Root.Q("train-inventory-fluids-slot-0")));

            // 空になると枠は残り、名前・量・アイコンは消える
            // When emptied the slot stays while name, amount and icon disappear
            _bodies.Sources.SetBody(new TrainInventoryBodyViewModel(new List<SlotItem>(), new[] { BlockFluidSlotState.Empty(1000) }));
            simulate.FrameUpdate();
            Assert.IsNotNull(_body.Root.Q("train-inventory-fluids-slot-0"));
            Assert.IsFalse(TrainTestText.HasFluidIcon(_body.Root.Q("train-inventory-fluids-slot-0")));
            StringAssert.DoesNotContain(" / ", FluidTexts());
            StringAssert.DoesNotContain(TrainTestText.FluidName(water), FluidTexts());
        }

        [Test]
        public void 時刻表タブの間の中身の更新は描かず持ち物へ戻すと最新を描く()
        {
            // 非表示の持ち物タブは購読しないので、時刻表タブの間の更新は木に出ない（D-27）
            // The hidden inventory tab is not subscribed, so updates during the timetable tab do not reach the tree (D-27)
            var water = MasterHolder.FluidMaster.GetFluidId(TrainTestText.WaterFluidGuid);
            simulate.Click(Find<Button>(_body.Root, "train-tab-timetable"));
            _bodies.Sources.SetBody(new TrainInventoryBodyViewModel(new List<SlotItem>(), new[] { BlockFluidSlotState.Filled(water, 10, 1000) }));
            simulate.FrameUpdate();
            Assert.IsNull(_body.Root.Q("train-inventory-fluids-slot-0"));

            // 戻すと購読の頭で最新値を 1 回描く
            // Switching back draws the latest value once on subscribe
            simulate.Click(Find<Button>(_body.Root, "train-tab-inventory"));
            simulate.FrameUpdate();
            Assert.IsNotNull(_body.Root.Q("train-inventory-fluids-slot-0"));
            StringAssert.Contains("10 / 1,000", FluidTexts());
        }

        [Test]
        public void 使えないの間でもタブを選ぶと取得を1回頼む()
        {
            // 取得中の表示は Fetcher の開始の合図を TrainTimetableModel が拾って出す（Hostless のプローブで確かめる）。ここは頼む回数だけ
            // Loading comes from TrainTimetableModel picking up the Fetcher start signal (checked in the Hostless probe); here only the request count
            _bodies.Sources.SetTimetable(TrainTimetableViewState.Unavailable);
            simulate.Click(Find<Button>(_body.Root, "train-tab-timetable"));
            Assert.AreEqual(1, _bodies.TimetableCommands.OpenRequestCount);
        }

        [Test]
        public void 隠して戻した同じ車両はタブを保ち別のパネルでは持ち物タブから()
        {
            // 画面が隠れると Detach、戻ると同じ Presenter へ Attach（alpha の呼ばれ方）。同じ車両ならタブを保つ
            // Hiding the screen detaches and showing it attaches the same Presenter again (alpha's call order); the same car keeps its tab
            simulate.Click(Find<Button>(_body.Root, "train-tab-timetable"));
            _body.Detach();
            _body.Attach(BlockInventoryTestPanels.Train(BlockInventoryPanelError.None));
            Assert.IsTrue(IsSelected(_body.Root, "train-tab-timetable"));

            // 識別の違うパネル（エラーが違う）が来たら持ち物タブから（Web は本文の mount ごとに初期化）
            // A panel with a different identity (different error) starts on the inventory tab (Web resets per body mount)
            _body.Detach();
            _body.Attach(BlockInventoryTestPanels.Train(BlockInventoryPanelError.TrainCarMissing));
            Assert.IsTrue(IsSelected(_body.Root, "train-tab-inventory"));
        }

        // 液体名と量・容量の文は枠の兄弟なので、FluidInventoryContents の器（testid）から読む
        // Name and amount texts are siblings of the slot, so read them from the FluidInventoryContents container (its testid)
        private string FluidTexts()
        {
            return TrainTestText.TextsUnder(_body.Root.Q("train-inventory-fluids"));
        }
    }
}
```
（格子 `train-inventory-slots` は液体の有無で自分の display を切り替えるので、最後の件はその要素で確かめてよい。水の `FluidId` は `TrainTestText.WaterFluidGuid`〈前例 `Client.Tests/WebUi/FluidMasterEndpointTest.cs:15` の Guid 直書き。`ForUnitTestFluidGuids` のような定数クラスは `Tests.Module/TestMod/` に無い〉から引く。`BlockFluidSlotState.Filled`・`Empty` の綴りは S4-machine の型に合わせる）

`TrainPlatformBodyTest.cs`（4 件）:

```csharp
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Commands;
using Client.Ui.State.Items;
using Client.Ui.State.Train.Platform;
using Core.Master;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Client.Tests.Ui.Screens.BlockInventory.Train
{
    public class TrainPlatformBodyTest : ScreenTestHost
    {
        private static readonly IReadOnlyList<BlockFluidSlotState> NoFluids = new List<BlockFluidSlotState>();
        private TrainTestBodies _bodies;
        private IBlockInventoryPart _body;

        [SetUp]
        public void MountBody()
        {
            var panel = BlockInventoryTestPanels.OfType(BlockTypeConst.TrainItemPlatform);
            _bodies = new TrainTestBodies(Toaster, panel);
            _body = _bodies.CreatePlatformBody(BlockInventoryTestParts.Grids(), BlockInventoryTestParts.Content());
            Mount(_body.Root);
            _body.Attach((OpenBlockInventoryPanel)panel);
        }

        [TearDown]
        public void DetachBody()
        {
            _body.Detach();
        }

        /// <web-test file="e2e/tests/block/trainPlatform.spec.ts">PFスロットを表示し、積込から卸しへ目標モードActionを送る</web-test>
        [Test]
        public void PFスロットを出し積込から卸しを送る()
        {
            // Web のモック blockTrainPlatform: 2 枠（12 個と空）・積込
            // Web mock blockTrainPlatform: two slots (12 and empty), load mode
            var slots = new[] { new SlotItem(new ItemId(3), 12), new SlotItem(ItemMaster.EmptyItemId, 0) };
            _bodies.Sources.SetPlatform(TrainTestPlatforms.ItemPlatformBody(slots, NoFluids, TrainPlatformTransferMode.LoadToTrain));
            simulate.FrameUpdate();
            Assert.IsNotNull(_body.Root.Q("train-platform-item-slots-slot-1"));
            Assert.IsTrue(IsSelected(_body.Root, "train-platform-mode-load"));

            // 押しても選択は替えず、目標のモードを送るだけ
            // Pressing does not change the selection; it only sends the target mode
            simulate.Click(Find<Button>(_body.Root, "train-platform-mode-unload"));
            CollectionAssert.AreEqual(new[] { TrainPlatformTransferMode.UnloadToPlatform }, _bodies.BlockCommands.SentModes);
            Assert.IsTrue(IsSelected(_body.Root, "train-platform-mode-load"));
        }

        /// <web-test file="e2e/tests/block/trainPlatform.spec.ts">液体PFは液体名・アイコン・量容量と卸しモードを表示する</web-test>
        [Test]
        public void 液体PFは液体と卸しモードを出す()
        {
            var water = MasterHolder.FluidMaster.GetFluidId(TrainTestText.WaterFluidGuid);
            _bodies.Sources.SetPlatform(TrainTestPlatforms.FluidPlatformBody(new[] { BlockFluidSlotState.Filled(water, 250, 1000) }, TrainPlatformTransferMode.UnloadToPlatform));
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(_body.Root.Q("train-platform-item-slots")));
            StringAssert.Contains(TrainTestText.FluidName(water), FluidTexts(_body));
            StringAssert.Contains("250 / 1,000", FluidTexts(_body));
            Assert.IsTrue(TrainTestText.HasFluidIcon(_body.Root.Q("train-platform-fluid-slots-slot-0")));
            Assert.IsTrue(IsSelected(_body.Root, "train-platform-mode-unload"));
        }

        /// <web-test file="src/features/blockInventory/views/__tests__/TrainPlatformInventory.test.tsx">PF自身の充填スロット・常時テキストを更新し、空でも実スロットを残す</web-test>
        [Test]
        public void 充填スロットを描き直し空でも実枠を残す()
        {
            // Web と同じく液体 PF の本文で、水 250 → 700 → 空
            // As in Web, a fluid platform body going water 250, then 700, then empty
            var bodies = new TrainTestBodies(Toaster, BlockInventoryTestPanels.OfType(BlockTypeConst.TrainFluidPlatform));
            var body = bodies.CreatePlatformBody(BlockInventoryTestParts.Grids(), BlockInventoryTestParts.Content());
            Mount(body.Root);
            body.Attach((OpenBlockInventoryPanel)BlockInventoryTestPanels.OfType(BlockTypeConst.TrainFluidPlatform));
            var water = MasterHolder.FluidMaster.GetFluidId(TrainTestText.WaterFluidGuid);
            bodies.Sources.SetPlatform(TrainTestPlatforms.FluidPlatformBody(new[] { BlockFluidSlotState.Filled(water, 250, 1000) }, TrainPlatformTransferMode.UnloadToPlatform));
            simulate.FrameUpdate();
            StringAssert.Contains(TrainTestText.FluidName(water), FluidTexts(body));
            StringAssert.Contains("250 / 1,000", FluidTexts(body));
            Assert.IsTrue(TrainTestText.HasFluidIcon(body.Root.Q("train-platform-fluid-slots-slot-0")));

            bodies.Sources.SetPlatform(TrainTestPlatforms.FluidPlatformBody(new[] { BlockFluidSlotState.Filled(water, 700, 1000) }, TrainPlatformTransferMode.UnloadToPlatform));
            simulate.FrameUpdate();
            StringAssert.Contains("700 / 1,000", FluidTexts(body));
            StringAssert.Contains("700", TrainTestText.TextsUnder(body.Root.Q("train-platform-fluid-slots-slot-0")));

            // 空でも枠は 1 つ残り、アイコンと文は消える
            // Even when empty one slot stays while the icon and texts disappear
            bodies.Sources.SetPlatform(TrainTestPlatforms.FluidPlatformBody(new[] { BlockFluidSlotState.Empty(1000) }, TrainPlatformTransferMode.UnloadToPlatform));
            simulate.FrameUpdate();
            Assert.IsNotNull(body.Root.Q("train-platform-fluid-slots-slot-0"));
            Assert.IsNull(body.Root.Q("train-platform-fluid-slots-slot-1"));
            Assert.IsFalse(TrainTestText.HasFluidIcon(body.Root.Q("train-platform-fluid-slots-slot-0")));
            StringAssert.DoesNotContain(" / ", FluidTexts(body));
            StringAssert.DoesNotContain(TrainTestText.FluidName(water), FluidTexts(body));
            body.Detach();
        }

        // 液体名と量・容量の文は枠の兄弟なので、FluidInventoryContents の器（testid）から読む
        // Name and amount texts are siblings of the slot, so read them from the FluidInventoryContents container (its testid)
        private static string FluidTexts(IBlockInventoryPart body)
        {
            return TrainTestText.TextsUnder(body.Root.Q("train-platform-fluid-slots"));
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableSection.test.ts">sends station names only through Set and hides the field for platforms</web-test>
        [Test]
        public void 駅名は決定でだけ送りPFでは欄を出さない()
        {
            // PF では駅名の行が無い
            // Platforms have no station name row
            _bodies.Sources.SetPlatform(TrainTestPlatforms.ItemPlatformBody(new List<SlotItem>(), NoFluids, TrainPlatformTransferMode.LoadToTrain));
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(_body.Root.Q("train-station-name-section")));

            // 駅では打つだけでは送らず、決定で 1 回送る。拒否は同期名へ戻す
            // On a station typing sends nothing, Set sends once, and a refusal reverts to the synced name
            _bodies.Sources.SetPlatform(TrainTestPlatforms.StationBody("北駅", new List<SlotItem>(), NoFluids, TrainPlatformTransferMode.LoadToTrain));
            simulate.FrameUpdate();
            var field = _body.Root.Q<TextField>("train-station-name-input");
            Assert.AreEqual("北駅", field.value);
            field.value = "南駅";
            Assert.IsEmpty(_bodies.BlockCommands.SentNames);
            simulate.Click(Find<Button>(_body.Root, "train-station-name-apply"));
            CollectionAssert.AreEqual(new[] { "南駅" }, _bodies.BlockCommands.SentNames);
            LogAssert.Expect(LogType.Warning, new Regex(@"\[UiCommandResultToaster\] train_station.set_name failed"));
            _bodies.BlockCommands.Reject(UiCommandRejectReason.TrainStationNameFailed, "duplicate");
            Assert.AreEqual("北駅", field.value);
        }
    }
}
```
（水は `TrainTestText.WaterFluidGuid`。`ItemMaster.EmptyItemId`・`ItemId` の綴りはテスト用 mod の前例に合わせる。`BlockInventoryTestPanels.OfType` が `OpenBlockInventoryPanel` を返す型ならキャストは要らない）

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.Ui\.Screens\.BlockInventory\.Train\."`
Expected: `ErrorCount: 0`、14 件 PASS（SectionView 2・Body 8〈タブの局所状態の 2 件を含む。D-33〉・Platform 4。ScrollView の自然高などの見た目はここでは主張しない）。`UniTaskCompletionSource` の継続が同期で走らず `Accept`/`Reject` 直後の主張が落ちるなら、その件だけ `[UnityTest]` にして `await UniTask.Yield()` を挟む

- [ ] **Step 5: テキスト欄にフォーカスがあるときの閉じを確かめる**

死活表の「要確認」行。`TrainPlatformBodyTest` と同じ組み立てで駅名の欄にフォーカスを置き、E と Esc を `simulate` で送って、alpha の外殻の閉じる経路（`UiInputExclusivity` の範囲。C2-b）が効くかを Task 11 の録画 `uitk/uitk-train-inventory` の中で確かめる（L1 の外殻には入力の振り分けが無いので録画で見る）。結果を `done.md`「証拠」に書き、効かなければ C2-b の持ち主へ `done.md`「integrator への依頼」で上げる（このレーンでは入力の振り分けを変えない）

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Train moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Train
git commit -m "feat(uitk): 列車とプラットフォームの本文を足す（S4-train）

<実行ホストの指示する帰属行>"
```

---

### Task 8: 登録・USS の取り込み・外殻ごとの L1

**Files:**
- Create: `S/Client.Starter/Registration/GameUi/Screens/BlockInventoryTrainGameUiRegistration.cs`（E-1 ad92a44777 の置き場。ディレクトリが無ければこのレーンが作る）
- Modify: `S/Client.Starter/Registration/MainGameUiRegistration.cs`（画面登録区画に 1 行）
- Modify: `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`（`Screens` 区画に 2 行）
- Create: `S/Client.Tests/Ui/Screens/BlockInventory/Train/TrainInventoryFlowTest.cs`・`TrainTimetableFlowTest.cs`・`TrainDesignTest.cs`

- [ ] **Step 1: 登録を書く**

```csharp
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.Train;
using Client.Ui.State.Train.Inventory;
using Client.Ui.State.Train.Platform;
using Client.Ui.State.Train.Platform.Edit;
using Client.Ui.State.Train.Timetable;
using Client.Ui.State.Train.TimetableEdit;
using Client.Ui.State.Train.TimetableEdit.Send;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi.Screens
{
    /// <summary>
    /// 列車族のブロックインベントリ（列車の本文・駅/PF の本文）の登録
    /// Registers the train family block inventory: the train body and the station/platform body
    /// </summary>
    public static class BlockInventoryTrainGameUiRegistration
    {
        public static void Register(IContainerBuilder builder)
        {
            // 源の Model（源の購読は起動時に 1 回）
            // Source models subscribe to their sources once at startup
            builder.RegisterEntryPoint<TrainTimetableModel>().As<ITrainTimetableSource>();
            builder.RegisterEntryPoint<TrainInventoryBodyModel>().As<ITrainInventoryBodySource>();
            builder.RegisterEntryPoint<TrainPlatformBodyModel>().As<ITrainPlatformBodySource>();

            // 編集の器（Model とコマンドが共有する。D-33）
            // Edit holders shared by the models and commands (D-33)
            builder.RegisterEntryPoint<TrainTimetableEditState>().AsSelf();
            builder.RegisterEntryPoint<TrainStationNameEditState>().AsSelf();

            // 描画値の Model（Presenter が購読）と、View が直接呼ぶコマンド（A-8）
            // Draw-value models the Presenters subscribe to, and commands the View calls directly (A-8)
            builder.RegisterEntryPoint<TrainTimetableEditModel>().As<ITrainTimetableEditModel>();
            builder.Register<TrainPlatformEditModel>(Lifetime.Singleton).As<ITrainPlatformEditModel>();
            builder.Register<TrainTimetableEditCommands>(Lifetime.Singleton).As<ITrainTimetableEditCommands>();
            builder.Register<TrainPlatformEditCommands>(Lifetime.Singleton).As<ITrainPlatformEditCommands>();

            // コマンドと時間切れ
            // Commands and the timeout
            builder.Register<TrainTimetableCommands>(Lifetime.Singleton).As<ITrainTimetableCommands>();
            builder.Register<TrainBlockCommands>(Lifetime.Singleton).As<ITrainBlockCommands>();
            builder.Register<UniRxTrainTimetableWaitTimer>(Lifetime.Singleton).As<ITrainTimetableWaitTimer>();

            // alpha の拡張口への差し込み
            // Plug into alpha's extension points
            builder.Register<TrainInventoryBodyFactory>(Lifetime.Singleton).As<ITrainInventoryBodyFactory>();
            builder.Register<TrainPlatformBodyFactory>(Lifetime.Singleton).As<IBlockInventoryBodyFactory>();
        }
    }
}
```
（名前空間はディレクトリに合わせて `Client.Starter.Registration.GameUi.Screens`。`MainGameUiRegistration.cs` に `using` を 1 行足す。`RegisterEntryPoint<T>().As<I>()`・`.AsSelf()` で `IInitializable` と口〈器は型そのもの〉の両方に登録されることを compile 後の起動ログで確かめる。前例 `OpenTrainTimetableTracker` は `.AsSelf()`。器は前例 `PauseMenuStateService` と同じく型そのもので解決し、Model とコマンドが同じ 1 つを受ける）

`MainGameUiRegistration.cs` の画面登録区画（他レーンの行の並びの末尾）に:

```csharp
            BlockInventoryTrainGameUiRegistration.Register(builder);
```

`GameUiTheme.tss` の `/* == section: Screens == */` の末尾に:

```css
@import url("project://database/Assets/Scripts/Client.Ui/Screens/BlockInventory/Train/Train.uss");
@import url("project://database/Assets/Scripts/Client.Ui/Screens/BlockInventory/Train/Timetable/Timetable.uss");
```
（書式は同区画の既存の `@import` 行に合わせる）

- [ ] **Step 2: 外殻ごとのテストを書く**

`TrainInventoryFlowTest.cs`（alpha の外殻 `BlockInventoryTestShell` に本文の工場を差して開く）:

```csharp
using System.Collections.Generic;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Train.Inventory;
using Core.Master;
using NUnit.Framework;

namespace Client.Tests.Ui.Screens.BlockInventory.Train
{
    public class TrainInventoryFlowTest : ScreenTestHost
    {
        /// <web-test file="e2e/tests/train.spec.ts">貨車スロットとコンテナ不在エラーを表示する</web-test>
        [Test]
        public void 貨車スロットとコンテナ不在エラーを出す()
        {
            // Web のモック trainCargo: 24・8 と空 7 枠
            // Web mock trainCargo: 24, 8 and seven empty slots
            var bodies = new TrainTestBodies(Toaster, BlockInventoryTestPanels.Train(BlockInventoryPanelError.None));
            var shell = BlockInventoryTestShell.Create(this, bodies.Panel, bodies.TrainBodyFactory(BlockInventoryTestParts.Grids(), BlockInventoryTestParts.Content()));
            bodies.Sources.SetBody(new TrainInventoryBodyViewModel(TrainCargoSlots(), new List<BlockFluidSlotState>()));
            simulate.FrameUpdate();
            StringAssert.Contains("24", TrainTestText.TextsUnder(shell.View.Q("train-inventory-slots-slot-0")));
            StringAssert.Contains("8", TrainTestText.TextsUnder(shell.View.Q("train-inventory-slots-slot-1")));
            Assert.IsNotNull(shell.View.Q("train-inventory-slots-slot-8"));

            // コンテナ不在では alpha のエラー行が出て、本文は作られない
            // With the container missing, alpha's error row shows and no body is built
            bodies.Panel.Value = BlockInventoryTestPanels.Train(BlockInventoryPanelError.ContainerMissing);
            simulate.FrameUpdate();
            Assert.IsTrue(IsShown(shell.View.Q("train-inventory-error")));
            Assert.IsNull(shell.View.Q("train-inventory-slots"));
        }

        private static SlotItem[] TrainCargoSlots()
        {
            var slots = new SlotItem[9];
            slots[0] = new SlotItem(new ItemId(1), 24);
            slots[1] = new SlotItem(new ItemId(2), 8);
            for (var i = 2; i < slots.Length; i++) slots[i] = new SlotItem(ItemMaster.EmptyItemId, 0);
            return slots;
        }
    }
}
```
（`BlockInventoryTestShell.Create(ScreenTestHost host, ReactiveProperty<BlockInventoryPanelState> state, params object[] plugged)` は alpha の Produces の形。返り値の `View` の名前は alpha の実物に合わせる）

`TrainTimetableFlowTest.cs`（`timetable.spec` 2 件。Web と同じ手順。公開はテストが `SetTimetable` で返す＝サーバーの代わり。期待の並びは `TimetableEditLogic` で組み、Side を手で書かない。各手順の後に保留が解けた〈`--busy` が無い〉ことを確かめる。Web の `aria-busy="false"` に当たる）:

```csharp
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.Train.Timetable;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Commands;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.Timetable;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.Train.Fakes.TrainTestData;

namespace Client.Tests.Ui.Screens.BlockInventory.Train
{
    public class TrainTimetableFlowTest : ScreenTestHost
    {
        private TrainTestBodies _bodies;
        private ITrainInventoryPart _body;

        [SetUp]
        public void OpenTimetableTab()
        {
            // Web のモック trainTimetable: 停車駅は北駅 1 つ、駅一覧は無名(-8,0,3)と北駅(12,0,40)
            // Web mock trainTimetable: one stop at North; stations are unnamed (-8,0,3) and North (12,0,40)
            _bodies = new TrainTestBodies(Toaster, BlockInventoryTestPanels.Train(BlockInventoryPanelError.None));
            _body = _bodies.CreateTrainBody(BlockInventoryTestParts.Grids(), BlockInventoryTestParts.Content());
            Mount(_body.Root);
            _body.Attach(BlockInventoryTestPanels.Train(BlockInventoryPanelError.None));
            _bodies.Sources.SetTimetable(TrainTimetableViewState.Ready(NorthOnly(Unit1)));
            Click("train-tab-timetable");
        }

        [TearDown]
        public void Close()
        {
            _body.Detach();
        }

        /// <web-test file="e2e/tests/train/timetable.spec.ts">駅の追加・上下移動・削除を即時反映し、再読込でも保持する</web-test>
        [Test]
        public void 追加移動削除を即時に映し開き直しても保つ()
        {
            Assert.IsNull(_body.Root.Q("train-timetable-apply"));

            // 追加 → 上 → 下 → 削除。どれも受理と公開で保留が解ける
            // Add, up, down, remove; each releases on acceptance and publication
            var added = Added(NorthOnly(Unit1).Stops, Unnamed);
            Edit("train-timetable-station--8_0_3-add", added);
            Assert.IsNotNull(_body.Root.Q("train-timetable-stop-1"));
            var up = TimetableEditLogic.MoveStop(added, 1, StopMoveDirection.Up);
            Edit("train-timetable-stop-1-up", up);
            StringAssert.Contains("(-8, 0, 3)", TrainTestText.TextsUnder(StopLabel(0)));
            var down = TimetableEditLogic.MoveStop(up, 0, StopMoveDirection.Down);
            Edit("train-timetable-stop-0-down", down);
            StringAssert.Contains("(12, 0, 40)", TrainTestText.TextsUnder(StopLabel(0)));
            var removed = TimetableEditLogic.RemoveStop(down, 0);
            Edit("train-timetable-stop-0-remove", removed);
            Assert.IsNull(_body.Root.Q("train-timetable-stop-1"));

            // タブの往復と開き直し（Web の再読込）のどちらでも反映済みの並びが残る
            // The applied order survives both a tab round trip and reopening (Web reload)
            Click("train-tab-inventory");
            Click("train-tab-timetable");
            StringAssert.Contains("(-8, 0, 3)", TrainTestText.TextsUnder(StopLabel(0)));
            Reopen();
            Assert.IsNull(_body.Root.Q("train-timetable-stop-1"));
            StringAssert.Contains("(-8, 0, 3)", TrainTestText.TextsUnder(StopLabel(0)));

            // 最後の 1 件を消すと 0 件になり、自動運転 ON が塞がる
            // Removing the last stop leaves none and blocks auto-run ON
            Edit("train-timetable-stop-0-remove", TimetableEditLogic.RemoveStop(removed, 0));
            Assert.IsNull(_body.Root.Q("train-timetable-stop-0"));
            Assert.IsFalse(Find<Button>(_body.Root, "train-timetable-auto-run-on").enabledSelf);
            Assert.AreEqual(5, _bodies.TimetableCommands.SentReplaces.Count);
        }

        /// <web-test file="e2e/tests/train/timetable.spec.ts">拒否された追加を巻き戻し、次の追加で復帰する</web-test>
        [Test]
        public void 拒否された追加を巻き戻し次の追加で戻る()
        {
            Click("train-timetable-station--8_0_3-add");
            LogAssert.Expect(LogType.Warning, new Regex(@"train_timetable\.replace failed: test"));
            _bodies.TimetableCommands.Reject(UiCommandRejectReason.TrainTimetableReplaceFailed, "test");
            simulate.FrameUpdate();
            AssertIdle();
            Assert.IsNull(_body.Root.Q("train-timetable-stop-1"));
            Assert.AreEqual(1, ShownToasts.Count);

            // 次の追加は通り、公開で 2 件になる
            // The next add goes through and the publication shows two stops
            Edit("train-timetable-station--8_0_3-add", Added(NorthOnly(Unit1).Stops, Unnamed));
            Assert.IsNotNull(_body.Root.Q("train-timetable-stop-1"));
            Assert.AreEqual(2, _bodies.TimetableCommands.SentReplaces.Count);
        }

        // 押す → 受理 → 公開 → 保留が解けたことを確かめる
        // Press, accept, publish, then confirm the pending state cleared
        private void Edit(string testid, IReadOnlyList<TimetableEditStop> published)
        {
            Click(testid);
            _bodies.TimetableCommands.Accept();
            _bodies.Sources.SetTimetable(TrainTimetableViewState.Ready(Ready(Unit1, false, published)));
            simulate.FrameUpdate();
            AssertIdle();
        }

        // 閉じて同じ貨車を開き直す。alpha と同じく旧部分を Detach し、工場から作り直して Attach するので、タブは持ち物から始まる
        // Close and reopen the same car; as alpha does, detach the old part and Attach a new one from the factory, so the tab starts on inventory
        private void Reopen()
        {
            _body.Detach();
            _body.Root.RemoveFromHierarchy();
            _bodies.Panel.Value = BlockInventoryPanelState.Closed;
            _bodies.Panel.Value = BlockInventoryTestPanels.Train(BlockInventoryPanelError.None);
            _body = _bodies.CreateTrainBody(BlockInventoryTestParts.Grids(), BlockInventoryTestParts.Content());
            Mount(_body.Root);
            _body.Attach(BlockInventoryTestPanels.Train(BlockInventoryPanelError.None));
            Assert.IsTrue(IsSelected(_body.Root, "train-tab-inventory"));
            Click("train-tab-timetable");
        }

        private void AssertIdle()
        {
            Assert.IsFalse(_body.Root.Q("train-timetable-section").ClassListContains(TrainTimetableSectionView.BusyClass));
        }

        private Label StopLabel(int index)
        {
            return _body.Root.Q<Label>($"train-timetable-stop-{index}-label");
        }

        private void Click(string testid)
        {
            simulate.Click(Find<Button>(_body.Root, testid));
            simulate.FrameUpdate();
        }
    }
}
```
（ラベルの座標の書式 `(-8, 0, 3)` は L0-s4b の `TimetableEditLogic.StationLabel` の実物に合わせる）

`TrainDesignTest.cs`（共有 2 件。前例は alpha の `BlockInventoryDesignTest` の同名の件。そこには足さず、列車の分を自分のファイルで主張する）:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using Client.Tests.Ui.Conventions;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.Components.Controls;
using Client.Ui.Screens.BlockInventory.Fluid;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Train.Inventory;
using Client.Ui.State.Train.Platform;
using Client.Ui.State.Train.Timetable;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.Train.Fakes.TrainTestData;

namespace Client.Tests.Ui.Screens.BlockInventory.Train
{
    public class TrainDesignTest : ScreenTestHost
    {
        private static readonly string[] TrainUssPaths =
        {
            "Assets/Scripts/Client.Ui/Screens/BlockInventory/Train/Train.uss",
            "Assets/Scripts/Client.Ui/Screens/BlockInventory/Train/Timetable/Timetable.uss",
        };
        private static readonly Type[] RawControlTypes = { typeof(Button), typeof(Toggle), typeof(RadioButton), typeof(Slider), typeof(ProgressBar), typeof(TextField) };

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">ゲージとモード切替を用途別に共通部品へ置換する</web-test>
        [Test]
        public void 列車のモード切替は共通部品ModeSwitchで出す()
        {
            // タブ・自動運転・積込/卸しの 3 つとも共通の ModeSwitch
            // Tabs, auto-run and load/unload all use the shared ModeSwitch
            var (train, platform) = MountAllParts();
            Assert.IsInstanceOf<ModeSwitch<TrainInventoryTab>>(train.Q("train-tabs"));
            Assert.IsInstanceOf<ModeSwitch<bool>>(train.Q("train-timetable-auto-run"));
            Assert.IsInstanceOf<ModeSwitch<TrainPlatformTransferMode>>(platform.Q("train-platform-mode"));
        }

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">%sから禁止Mantine部品とテーマ色を除去する</web-test>
        [Test]
        public void 列車の本文は共通部品とトークンの色だけで組む()
        {
            // (a) USS に色の直書きが無い。判定は規約検査 UiStyleConventionRules と同じもの
            // (a) No literal colors in the USS, judged by the same UiStyleConventionRules as the convention check
            foreach (var path in TrainUssPaths)
            {
                var violations = UiStyleConventionRules.CheckUss(File.ReadAllText(path), false);
                Assert.IsFalse(violations.Exists(v => v.Rule == UiStyleRule.ColorLiteral), $"{path} に色の直書きがある");
            }

            // (b) 列車・駅の本文の操作の要素はどれも共通部品で、素の Button・Toggle 等を画面が作らない
            // (b) Every control in the train and station bodies is a shared part; the screen builds no raw Button, Toggle, etc.
            var (train, platform) = MountAllParts();
            var sharedControls = new List<VisualElement>();
            CollectControls(train, sharedControls);
            CollectControls(platform, sharedControls);
            Assert.Less(0, sharedControls.Count);
        }

        // 列車の本文（時刻表タブを開いた区画つき）と、駅名・枠・液体・モードを全部出した駅の本文を組む
        // Builds the train body with the timetable section open and a station body showing name, slots, fluids and mode
        private (VisualElement train, VisualElement platform) MountAllParts()
        {
            var bodies = new TrainTestBodies(Toaster, BlockInventoryTestPanels.Train(BlockInventoryPanelError.None));
            var train = bodies.CreateTrainBody(BlockInventoryTestParts.Grids(), BlockInventoryTestParts.Content());
            Mount(train.Root);
            train.Attach(BlockInventoryTestPanels.Train(BlockInventoryPanelError.None));
            bodies.Sources.SetTimetable(TrainTimetableViewState.Ready(NorthOnly(Unit1)));
            simulate.Click(Find<Button>(train.Root, "train-tab-timetable"));

            var platform = bodies.CreatePlatformBody(BlockInventoryTestParts.Grids(), BlockInventoryTestParts.Content());
            Mount(platform.Root);
            platform.Attach((OpenBlockInventoryPanel)BlockInventoryTestPanels.OfType("TrainStation"));
            bodies.Sources.SetPlatform(TrainTestPlatforms.StationBody("北駅", new[] { new SlotItem(new Core.Master.ItemId(3), 12) }, new[] { BlockFluidSlotState.Empty(1000) }, TrainPlatformTransferMode.LoadToTrain));
            simulate.FrameUpdate();
            return (train.Root, platform.Root);
        }

        // 共通部品の中は部品の責任なので下りない。それ以外に素の操作の要素があれば落とす
        // Shared parts own their insides, so do not descend; any other raw control fails
        private static void CollectControls(VisualElement element, List<VisualElement> sharedControls)
        {
            if (IsSharedControl(element))
            {
                sharedControls.Add(element);
                return;
            }
            Assert.IsFalse(Array.IndexOf(RawControlTypes, element.GetType()) >= 0, $"素の {element.GetType().Name} '{element.name}' を画面が作っている");
            foreach (var child in element.Children()) CollectControls(child, sharedControls);
        }

        private static bool IsSharedControl(VisualElement element)
        {
            var type = element.GetType();
            return (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ModeSwitch<>))
                   || element is PanelActionButton || element is IconButton || element is GameTextField || element is BlockItemGrid || element is FluidInventoryContents;
        }
    }
}
```
（`Children()` は `contentContainer` の子を返すので、`GameScrollView` の Scroller や `GamePanel` の枠の内部には下りない。`UiStyleConventionRules.CheckUss(string ussText, bool allowColorLiterals)`・`UiStyleRule.ColorLiteral` は `Client.Tests/Ui/Conventions` の実物で確かめた。`violations` が `List<UiStyleViolation>` なので `Exists` を使う）

- [ ] **Step 3: compile・テスト・起動**

Run:
```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.Ui\.(State|Screens\.BlockInventory)\.Train\.|PauseMenuCommandsTest|UiThemeImportBundleTest|UiStyleConventionTest|UiLayerDependencyDirectionTest|LookExceptionLedgerTest|BlockInventory"
uloop get-logs --project-path ./moorestech_client --log-type Error
```
Expected: `ErrorCount: 0`、全件 PASS（State 21・Screens 19・`PauseMenuCommandsTest`〈`UiCommandRejectReason` の全列挙を回す 2 件。足した 7 値はどれも非良性なので配列は変えていない。D-32〉・L2・alpha の `BlockInventory*`）。全体スイッチ UITK（`UiSystemEditorSwitch.Set(UiSystem.GameUi)`）で PlayMode を起動し、列車・駅を開いて Error ログ 0 件

- [ ] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Starter "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss" moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Train
git commit -m "feat(uitk): 列車族の本文を登録し外殻ごとの L1 を足す（S4-train）

<実行ホストの指示する帰属行>"
```

---

### Task 9: 名前の対応

**Files:** なし（`docs/uitk/disposition.md` には `<web-test>` で写した件を書かない。`compareBundles.mjs` が誤りにするため）

- [ ] **Step 1: 対応を確かめる**

Run: `node tools/uitk/test-name-parity.mjs --bundle 4`
Expected: S4-train のファイル（`train.spec.ts`〈乗車HUD の件は S2-hud で対応済み〉・`train/timetable.spec.ts`・`TrainTimetableImmediate.test.ts`・`TrainTimetableSection.test.ts`・`TrainInventoryBody.test.tsx`・`block/trainPlatform.spec.ts`・`views/__tests__/TrainPlatformInventory.test.tsx`）に未対応が無い。`blockInventoryDesign.test.ts` の 2 件は alpha と同じ名前のタグで対応済み（列車の分）。未対応が残れば `done.md`「証拠」に出力を貼り、写し漏れを足してから先へ進む

---

### Task 10: 並べ比べと見た目の例外

**Files:**
- Modify: `docs/uitk/TODO.md`（「## 見た目の例外」。使ったときだけ LX-121〜LX-125。「## 参照キャプチャ」に自撮りの行）
- Modify（要るときだけ）: `S/Client.Ui/Foundation/Theme/DerivedTokens.uss`（駅名の入力欄の幅のトークン）
- 記録: `LOGS/harness/uitk/S4-train/done.md`「差の表」

- [ ] **Step 1: 撮る**

参照（基準 `web-a9d5123537` は 1280×720、`web-0e81bbe3c3-2560x1080` は 2560×1080）と同じ場面を UITK で撮る。場面の組み方・撮り方・比較画像は ref-uitk-design スキルの「比べ方」節に従う。

| 参照 | 場面（Web のモック） |
|---|---|
| `4-block-train` | `trainCargo`（24・8・空 7 枠・持ち物タブ） |
| `4-block-trainTimetable` | `trainWithTimetable` の持ち物タブ（空 10 枠。参照は格子の上が切れている → 自撮りの参照と照らす） |
| `4-block-train-timetable-tab` | `trainWithTimetable` の時刻表タブ（停車駅 北駅・駅一覧 2 件・自動運転 OFF） |
| `4-block-trainPlatform` | `blockTrainPlatform`（2 枠・積込） |
| `4-block-trainFluidPlatform` | `blockTrainFluidPlatform`（水 250/1000・卸し） |

参照の無い場面は E-22 の自撮りで Web 側も撮る（使い捨ての master worktree の mock-host に一時の fixture を置く。コミットしない）: 駅の駅名の行（`TrainStation`。fixture が無い）・時刻表の Loading と Unavailable・保留中（`train-timetable-section--busy` で操作が `--text-muted`）・液体貨車。撮った参照は `docs/uitk/TODO.md`「## 参照キャプチャ」に行を足し、integrator へ撮影表への追加を依頼する（`done.md`「integrator への依頼」）

- [ ] **Step 2: 差の表を書く**

| 参照 | 見る点 | 予想 |
|---|---|---|
| 4-block-train | タブの位置・間（`--mode-switch-gap`）・格子の列数と枠 | 同じ |
| 4-block-trainTimetable | 持ち物タブの格子・スクロール器の上端 | 同じ（参照の切れは Web 側の撮影の問題。自撮りで確かめる） |
| 4-block-train-timetable-tab | 自動運転の行・Craft 枠 2 つ・見出しの色と余白・行の区切り線・`+` ボタンの位置・文字の行の高さと位置（`TextSm`）・行内の gap（長い駅名で右の子との間が詰まらない） | 同じ。見る点に「停車駅・駅一覧の行ラベルの字の大きさ（Web は body 16px）」も足す |
| 4-block-trainPlatform | スロット・ModeSwitch の位置 | 同じ |
| 4-block-trainFluidPlatform | 液体の行（S4-machine の部品）・ModeSwitch | 液体の行の差は S4-machine 持ち（他レーン持ち）。ModeSwitch は同じ |
| 自撮り: 駅名の行 | 入力欄の幅・高さ・決定ボタン・行内の gap | Web の入力欄の幅を測って UITK と比べる。違えば同値の既存トークンか `Foundation/Theme/DerivedTokens.uss` の名札付きブロック（根拠のコメントつき。E-1）にトークンを足して `.train-station-name__input` の `width` に当てる。差が残ったときだけ LX |
| 自撮り: 保留中 | 無効な操作の文字色 | 同じ（D-11 のつまみの上書き） |
| 自撮り: 本文の高さ | 停車駅を 15 件ほど足した時刻表タブで、本文がパネルの高さに収まり `train-body-scroll` がスクロールするか（0 に潰れない・全内容分に伸びない） | 同じ（alpha の大型パネルが高さを決め、列車の USS は鎖をつなぐだけ。D-37）。潰れる・伸びるなら alpha の大型パネルの高さの鎖を `uloop execute-dynamic-code` で `resolvedStyle.height` を辿って測り、切れている段を `done.md`「要裁定」に書いて alpha の持ち主へ上げる |

差は 1 件ずつ「同じ／直した／LX-n／他レーン持ち」で閉じる。LX は `docs/uitk/TODO.md`「## 見た目の例外」に LX-121 から順に使い、USS に `/* look-exception: LX-n */` を付ける

- [ ] **Step 3: コミット**

```bash
git add docs/uitk/TODO.md moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Train moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/DerivedTokens.uss
git commit -m "docs(uitk): 列車族の並べ比べの差を閉じる（S4-train）

<実行ホストの指示する帰属行>"
```
（差が無く何も変えていなければコミットしない）

---

### Task 11: 録画（UITK の録画を新設し、壊れた時刻表の録画を両系で通る形に直す）

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-train-inventory.cs`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/train/train-timetable-via-ui.cs`
- 触らない: `scenarios/train/train-station-default-name.cs`（Web 系の既定名の初期同期の主張のまま。D-13）

- [ ] **Step 1: `train-timetable-via-ui.cs` を Web と UITK の両方で通る形に直す**

今のシナリオは 2 駅を続けて追加してから `train-timetable-apply` を押す。適用ボタンは Web にも無い（`TrainTimetableImmediate.test.ts:38`）ので、Web 系でも今は壊れている。追加は即送信・保留中は次の追加を受けないので、次のとおり直す。待ちはサーバーとクライアントのデータだけで取り、系ごとに違う保留の印（UITK の `train-timetable-section--busy`・Web の `aria-busy`）は読まない（Web の DSL は `data-*` 属性しか返さない）:
1. 冒頭のコメント（日英）の「追加→適用」を「1 駅ずつ追加し、時刻表に載るのを待つ」に替える
2. 「北駅・南駅を追加して適用」の節を次に替える:

```csharp
        p.Note("北駅・南駅を1駅ずつ追加（即時送信）");
        await p.ClickUi($"train-timetable-station-{stationAPos.x}_{stationAPos.y}_{stationAPos.z}-add");
        await p.WaitForTrainState(train, 1, null, null, 10f, "サーバーの時刻表が1駅になる");
        await p.Until(() => ClientTimetableStopCount() == 1, 10f, "クライアントの時刻表が1駅になる（保留が解ける）");
        await p.ClickUi($"train-timetable-station-{stationBPos.x}_{stationBPos.y}_{stationBPos.z}-add");
        await p.WaitForTrainState(train, 2, null, null, 10f, "サーバーの時刻表が2駅になる");
        await p.Until(() => ClientTimetableStopCount() == 2, 10f, "クライアントの時刻表が2駅になる");
        await p.UntilUiElement("train-timetable-stop-1", 5f);
```
（`ClientTimetableStopCount()` はシナリオ内のローカル関数で、`IClientTrainTimetableLookup` を前例 `uitk/uitk-grab-overlay.cs` と同じく `ClientDIContainer` から引き、`TryGet(列車の TrainUnitInstanceId, out snapshot)` の `Stops.Count`〈無ければ 0〉を返す。クライアントの公開が届いた後は DSL のアクション間 0.5 秒の間に両系の UI が描き直されるので、次の追加は保留に当たらない）
3. スクショ名 `02-timetable-applied` を `02-timetable-added` に替える
4. 取得の回数の主張（開いた直後 0・タブ選択で 1）と拒否ログの数え方（`[TrainScheduleEdit] rejected`）はそのまま残し、UITK の拒否の Warning `[TrainTimetableCommands] rejected:` も同じ数え方に足す（両系で拒否 0 件を主張する）

- [ ] **Step 2: `uitk/uitk-train-inventory.cs` を書く**

前例 `scenarios/uitk/uitk-grab-overlay.cs` の形（`UiSystemEditorSwitch.Set(UiSystem.GameUi)`・`ClientDIContainer` から型を引く）と、設置は `train/train-timetable-via-ui.cs` の前半（駅 2 つ・レール・貨車 1 両）をそのまま使う。次を順に主張する:
1. 駅 A を `PressInteract` で開く → `WaitUiState(UIStateEnum.SubInventory)` → `train-platform-view`・`train-station-name-section`・`train-platform-mode` が出る（`Screenshot("01-station")`）
2. `PlaytestUiElementOps.SetText("train-station-name-input", "中央駅")` → 送らないこと（クライアントの駅名が既定名のまま）→ `ClickUi("train-station-name-apply")` → クライアントの駅名が「中央駅」になるのを待つ。駅名は `train-station-default-name.cs:88-91` と同じく `ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(stationAPos)` の `view.GetStateDetail<TrainStationNameStateDetail>(TrainStationNameStateDetail.BlockStateDetailKey)?.StationName` で読む（サーバーの `TrainStationComponent` には `GetStateDetail` が無い）→ `WaitUiTextContains("train-station-name-input", "中央駅")`
3. 死活表の要確認「テキスト欄にフォーカスがあるときの E・ESC での閉じる」: 入力欄にフォーカスを置いたまま Esc を InputSystem で注入し、`WaitUiState(UIStateEnum.GameScreen, 5f)` で閉じることを主張する。落ちたら `done.md`「証拠」に結果を書き、C2-b の持ち主へ「integrator への依頼」で上げ、この手順だけ `p.Note` の記録に替えて残りを回す（このレーンでは入力の振り分けを変えない）。閉じたら駅 A を開き直す。E も同じ形で 1 回確かめる
4. `ClickUi("train-platform-mode-unload")` → 押しただけでは選択が替わらず、クライアントの `TrainPlatformTransferStateDetail.Mode` が `UnloadToPlatform` になってから替わる（`Screenshot("02-station-unload")`）→ `block-inventory-close` → `WaitUiState(UIStateEnum.GameScreen)`
5. 貨車を `PressInteract` で開く → `train-inventory-body` が出て `train-tab-inventory` が選ばれている → `ClickUi("train-tab-timetable")` → `train-timetable-section` → 駅 A を追加 → `WaitForTrainState(train, 1, …)` とクライアントの時刻表 1 駅を待ち、`train-timetable-section` が `train-timetable-section--busy` を持たない（UITK の系だけの録画なので UITK の印を読む）
6. `ClickUi("train-timetable-auto-run-on")` → `WaitForTrainState(train, null, true, null, 10f, …)` → `train-timetable-auto-run-on` が選ばれる（`Screenshot("03-timetable-auto-run")`）→ 閉じる → `ClientDIContainer` から引いた `ITrainTimetableEditModel.Current.Value` が `UnavailableTrainTimetableArea` になるのを `p.Until` で待つ（本番の通知経路〈`UIStateControl.OnStateChanged` → `TrainTimetableModel` → 編集の Model〉で閉じたことが届く。D-29）
7. 全体で Error ログが 0 件: DSL に `AssertNoErrorLogs` は無く、`PlaytestRunner` は `result.ErrorLogs` に集めるだけで成否に効かない（`Client.Playtest/PlaytestRunner.cs:84`）。前例 `train/train-timetable-via-ui.cs:36` と同じく、シナリオの頭で `Application.logMessageReceived` に Error・Exception を数えるローカル関数を足し（`try`/`finally` で外す）、最後に `p.Assert(errorCount == 0, $"Error ログが 0 件 (count={errorCount})")` で主張する。あわせて Step 3 で `result.json` の `ErrorLogs` が空であることを読み、`done.md`「証拠」に書く

押下は DSL の `ClickUi`、文字は `PlaytestUiElementOps.SetText`、キーは InputSystem の注入だけを使い OS の入力は使わない

- [ ] **Step 3: 回す**

実行前に PlayMode を止め、`uloop execute-dynamic-code` で `UiSystemEditorSwitch.Set(UiSystem.GameUi)` を立ててから、unity-playmode-recorded-playtest スキルの `scripts/run-scenario.sh` で `uitk/uitk-train-inventory`・`train/train-timetable-via-ui`・`train/fluid-inventory-via-interact`・`uitk/uitk-lifeline` を回す。続けて全体スイッチを Web（`UiSystem.Web`。`Client.Input/UiInputExclusivity.cs` の列挙）に戻し、`train/train-timetable-via-ui` と `train/train-station-default-name` を回す（Web 系が直った／退化していないことの確かめ。死活表）
Expected: 6 本の `result.json` が PASS、mp4 がある。`done.md`「証拠」に result.json のパス・mp4・各回の `UiInputExclusivity.ActiveUiSystem` の値（GameUi 4 本・Web 2 本）、6 本の `result.json` の `ErrorLogs` が空であること（空でなければ行をそのまま貼り、原因を直すか「要裁定」へ）と、手順 3 のフォーカス中の閉じの結果を書く。生の録画は worktree の gitignore 下に置き、`LOGS` へはスクショ 3 枚までと報告だけ。終わりに `list-work-saves.sh` でテストのワールドを消す

- [ ] **Step 4: コミット**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-train-inventory.cs .agents/skills/unity-playmode-recorded-playtest/scenarios/train/train-timetable-via-ui.cs
git commit -m "test(uitk): 列車族の UITK 録画を足し、時刻表の録画を即時送信に直す（S4-train）

<実行ホストの指示する帰属行>"
```

---

### Task 12: 全ブランチのレビュー

- [ ] **Step 1: moores-code-review を全ブランチで回す**

moores-code-review スキルを、base `origin/feature/uitk` に対するこのブランチの全差分で起動する。スキルの手順どおり、機械的な修正は自動で当て（自動適用の後は Task 11 の録画を反映後のバイナリで撮り直す）、設計判断は D-n として判断記録へ足すか、目的・裁定から外れるものだけ `done.md`「要裁定」へ書く（人へ質問しない）

- [ ] **Step 2: 当てた修正の後に compile とテスト**

Run: `uloop compile --project-path ./moorestech_client` → Task 8 Step 3 と同じテストの絞り込み
Expected: `ErrorCount: 0`、全件 PASS

- [ ] **Step 3: コミット**

```bash
git add -A moorestech_client docs .agents/skills/unity-playmode-recorded-playtest/scenarios/train .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-train-inventory.cs
git commit -m "fix(uitk): 全ブランチのレビューを反映（S4-train）

<実行ホストの指示する帰属行>"
```

---

### Task 13: PR・取り込み・マージ・報告

- [ ] **Step 1: PR を作る**

pr-create スキルで base `feature/uitk` の PR を作る。本文の先頭行は報告ページ `# https://review.moores.tech/pr/<番号>/report/`（並べ比べの HTML と録画を載せる。素材と公開手順は環境ローカル規約の正本 README。公開に失敗したら報告 URL 無しで完了にしない）。本文の末尾は実行ホストの指示する帰属行

- [ ] **Step 2: `origin/feature/uitk` を取り込む**

```bash
git fetch origin
git merge origin/feature/uitk
```
共有区画（`MainGameUiRegistration` の画面登録区画・`GameUiTheme.tss` の `Screens` 区画・`docs/uitk/TODO.md`・`UiCommandRejectReason`〈alpha の `BlockSlotUnavailable` 等と並ぶ〉・`localization.csv`・`Client.Ui.State.asmdef` の references）の衝突は両側の行を残して解く。ただし `Client.Ui.State.asmdef` で同じ参照が両側にあれば 1 行に畳む（E-1）。`localization.csv` が衝突したら解いた後に `pnpm --dir moorestech_web/webui run gen:i18n` を回し直す。取り込み後に `uloop compile` と Task 8 Step 3 のテストを回し、緑を確かめて push

- [ ] **Step 3: G1 を待ってマージする**

`gh pr checks <番号>` が緑になったら `gh pr merge <番号> --merge`（squash しない）。`UnityCursorTooltipScreenTest` が CI だけで落ちるのは既知で無視してよい（他の赤は直して push し直す）

- [ ] **Step 4: 報告と後始末**

1. `bd close <S4-train の issue>`
2. `LOGS/harness/uitk/S4-train/done.md` の 1 行目を「状態: 完了」にし、PR 番号・マージコミット・証拠・差の表・integrator への依頼・未確認を埋める
3. `cmux wait-for -S uitk-s4-train`
4. `moores-wt rm s4-train`（Editor と worktree を畳む。未 push があれば拒否されるので push を確かめてから）

---

## Self-Review

- 写すテスト 20 件（＋共有 2 件）は Task 4（6）・Task 7（2＋5＋4）・Task 8（1＋2＋共有 2）に全部ある。数: 6＋2＋5＋4＋1＋2 = 20（`train.spec` 1・`timetable.spec` 2・`TrainTimetableImmediate` 8・`TrainTimetableSection` 5・`TrainInventoryBody` 1・`trainPlatform.spec` 2・`TrainPlatformInventory` 1）
- R-1〜R-18 は Task 1〜13 のどこかに受け入れ基準の主張がある。R-15 は Task 10、R-17 は Task 11、R-18 は Task 12〜13
- 型の綴り: `TrainTimetableViewState.IsSameState`・`TrainTimetableReadyData.HasSameContent`・`TrainTimetableFetcher.OnFetchStarted`・`TrainTimetableEditSession.Server / DisplayedStops / IsPending / OnDisplayChanged / ReceiveServer / Replace / Close`・`TrainTimetableSectionViewModel.Build / HasSameContent / CurrentIndex(int?)`・`TrainTimetableAreaViewModel.IsSameArea`・`ReadyTrainTimetableArea.IsSameTrain`・`TrainCommandRejection.Reject(logTag, reason, serverFailureReason)`・`UiCommandRejectReason.BlockNotOpen / TrainTimetableReplaceFailed / TrainAutoRunFailed / TrainAutoRunNotApplied / TrainInvalidBlockType / TrainStationNameFailed / TrainPlatformModeFailed`・`TrainTestText.TextsUnder / FluidName / HasFluidIcon`・`TrainTimetableEditState.IsLoading / OnChanged / TryGetSession`・`ITrainTimetableEditModel.Current`・`ITrainTimetableEditCommands.Add / Move / Remove / SetAutoRun`・`TrainTimetableFetchTrigger.Tab / SelectTab / ReceiveState`・`TrainInventoryBodyView.OnTabSelected`・`TrainStationNameEditState.Input / Draft / Edit / RevertIfCurrent`・`ITrainPlatformEditModel.StationNameInput`・`ITrainPlatformEditCommands.EditStationName / ApplyStationName / SelectTransferMode`・`OpenTrainPlatformBody.StationName / ItemSlots / Mode`（`SectionContent<T>`）・`Fluids`・`TrainTestPlatforms.StationBody / ItemPlatformBody / FluidPlatformBody`・`TrainPlatformDetailReader.Read(blockType, param, …)` は定義・テスト・View で一致
- ディレクトリの本数: State `Timetable/` 6・`TimetableEdit/` 10＋`TimetableEdit/Send/` 3・`Inventory/` 6・`Platform/` 7＋`Platform/Edit/` 8、View `Train/` 8・`Train/Timetable/` 6・`Train/Parts/` 2、テスト `State/Train/Fakes/` 6・`State/Train/Platform/` 3・`State/Train/Inventory/` 1・`Screens/BlockInventory/Train/` 7、登録 `Client.Starter/Registration/GameUi/Screens/` 1 本以上。どれも 10 以下
- 層: Presenter は Model の `Current` を購読して `Render` へ流す（表示中のタブだけ。D-27）。パネルごとの局所状態（タブと取得の契機）だけは列車の本文の Presenter が持つ（D-33）。View はサーバーの型を持たない（D-28）。押下は View が State のコマンドを直接呼ぶ（A-8）。判断・セッションの寿命・下書きは State の器が持ち、描画値の Model とコマンドが共有する（D-33）
- alpha・S4-machine の型は計画の Produces から写し、実物との差は Task 1 Step 2 で合わせる。合わせた結果の形の違いは D-n で足す
- 規約: `Action` のコールバックは使っていない（押下は `OnClicked.Subscribe` で State の口を呼ぶ）。`Func<>`・partial・try-catch・デフォルト引数は無い。黙る経路は `silent-ok` 8 か所（並びの同じ編集・同じ中身の再配信 2・同じパネルの再配信〈駅名の器〉・追跡が遅れた一瞬・貨車の液体の応答前・駅名の StateDetail の欠け 2〈Reader と時刻表の駅一覧〉）と、ログつきの捨て（保留中の編集・閉じた後の結果・下書きが作り直された後の駅名の拒否）。開いていない操作は送らずに非良性の `BlockNotOpen` でトーストする（D-32）

## 判断記録

| ID | 判断 | 理由 | 却下した案 |
|---|---|---|---|
| D-1 | State は `Train/` の下に役割で 6 ディレクトリ（`Timetable/`・`TimetableEdit/`・`TimetableEdit/Send/`〈サーバーへ送る 3 本。D-33 で編集のコマンドを分けて 10 本を超えたため〉・`Inventory/`・`Platform/`・`Platform/Edit/`）、View は `Train/`・`Train/Timetable/`・`Train/Parts/` | 1 ディレクトリ 10 本の上限と、Model / 編集 / 本文 / ブロック側で変わる理由が別 | `Train/` 直下に全部（38 本で上限超え） |
| D-2 | **Web ホストにしかない導出は State に同じ規則でもう 1 本持つ（負債が残る。CEF 撤去の日に `Client.WebUiHost` 側を消す）**: 駅一覧の組み立てと整列・停車駅の名前の補完と欠けの警告・状態の優先・未知の Side の捨て・出発条件・`AppliedIsAutoRun` の照合・拒否文字列・駅/PF の詳細・貨車の液体の変換。Web ホストが既に State の口を呼んでいる所（`TrainTimetableFetcher`・`OpenTrainUnitResolver`）は UITK もその口を使い、写さない。重複の一覧は「## 本 PR 外のリファクタ提案」。出所: レビュー r1（根拠の書き直し）・レビュー r2（根拠の呼び名を直した） | C4-e の `moorestech_logs/harness/uitk/C4-e/done.md` 要裁定 1（「D-5 据え置き（負債が残る）」）と同じ agent 既定（指揮・ユーザーの裁定ではない。指揮が done.md の要裁定に上げる）に揃える（S4-alpha・S4-machine D-3 も同じ）。State は `Client.WebUiHost` を参照できず（層の向き）、Web ホストは撤去で消えるので、撤去の日に片側を消せば 1 本に戻る | State に 1 本にして Web ホストから呼ぶ（設計書 A-84・A-21 の向き。C4-e の agent 既定と揃えて撤去まで据え置き、このレーンで Web ホストを書き換えると Web 系の検証が増える）。旧版の却下理由「E-1 に反する」は誤り（E-1 に Web ホストの変更禁止は無い）なので消した |
| D-3 | 公開待ちの時間切れは口 `ITrainTimetableWaitTimer` で注入し、本番は UniRx の実時間 5000ms | Web の `DEFAULT_ACTION_TIMEOUT_MS` と同じ値。UI の通信待ちでゲームロジックの経過ではないので、AGENTS.md の「ティックで測る」の対象外。テストで手で起こせる | `GameUpdater` のティックで測る（ゲームが止まっていると UI が永久に保留） |
| D-4 | 閉じた後に戻った結果は捨て、Warning ログを出す（`Close()` のフラグ） | Web の unmount 後の無視と同じ。別の列車を開いた後に古い結果で表示を動かさない | 結果を待ってから閉じる（閉じる操作が遅れる） |
| D-5 | Ready の区画は持ち物タブのあいだも作ったまま `display:none`。列車が替わると作り直す | Web は hidden で mount を保ち、key=trainUnitId で作り直す。保留中にタブを替えても編集が切れない | タブごとに作り直す（保留が消える） |
| D-6 | 同じ状態は流し直さない: Loading・Unavailable は単一のインスタンス、Ready は中身（`HasSameContent`）で比べる。区画の描画値も同じ（`IsSameArea`）。出所: レビュー r1（Ready の比較を足した） | 「読込中が届くたびの頼み直し」は Ready → Loading の遷移で起きる。同じ Loading の連続で頼み直すと依頼が続く。貨車の中身の更新（`OnSubInventoryUpdated`）だけで時刻表の行とボタンを作り直さない | Ready を毎回新しく流す（中身の更新のたびに行を作り直し、押下の最中に要素が替わる） |
| D-7 | 積込/卸しの選択肢に testid `train-platform-mode-load` / `-unload` を足す | Web の選択肢には testid が無く、UITK の DSL とテストは名前で押す。部品 `ModeSwitch` は選択肢の testid を必須にしている | 文言で押す（言語で壊れる） |
| D-8 | 積込/卸しは State 側の列挙 `TrainPlatformTransferMode` で持ち、サーバーの列挙との写しは `TrainPlatformTransferModeConversion` の 1 か所（読み手と送り手が共有）。出所: レビュー r1（写しを 1 本にした） | `Client.Ui` に `Game.Block` の参照を足さない（View がサーバーの型を知らない） | `Client.Ui.asmdef` に `Game.Block` を足す／写しを読み手と送り手に 2 本持つ |
| D-9 | **覆した（D-31）**。旧: 拒否の理由は新しい 1 つ `TrainEditRejected`（文言 1 つ）に寄せ、Web と同じ理由文字列は `Detail` に入れる | 旧の理由（理由ごとの文言は Web に無い）は、Detail を wire に流用して `ToWireCode` の「Detail は補足」の契約を曲げ、どの拒否も同じ文言になる点を見ていなかった | — |
| D-10 | 停車駅のラベルの中の駅名（名前が空なら `timetableUnnamedStation`）は `TrainInventoryBodyPresenter` が `Localize.OnLanguageChanged` で描き直す | `LocalizedLabel` は自分のキーは描き直すが、引数に埋めた解決済みの文字列は変わらない | 引数に UiText を入れ子で渡す（`LocalizationTextParam` が文字列だけ） |
| D-11 | 無効な操作の文字色は、画面のルートのクラスの下で部品のつまみ `--panel-action-button-text` を `--text-muted` に上書き（＋`opacity: 1`） | Web `.list button:disabled { color: var(--text-muted) }`。`PanelActionButton` に無効の見た目が無く、部品のクラスを画面 USS の主語にしない規約。既定テーマの無効の不透明度が乗ると Web と違うので打ち消す。integrator へ部品側の無効の状態を依頼 | 画面 USS で `.panel-action-button` を直接塗る（規約違反） |
| D-12 | **覆した（D-33）**。旧: タブと取得の依頼の判定は State の `TrainInventoryTabModel`（`TrainTimetableFetchTrigger` を持つ単一インスタンス）が持ち、View のタブの押下が `SelectTab` を直接呼ぶ。出所: レビュー r1 | タブはパネルごとの局所状態で、サーバーの状態でも画面をまたぐ状態でもない。State の単一インスタンスに置くと Model に操作の口が混ざり（描画値とコマンドの分離に反する）、パネルの源を別に購読して「別のパネル」を見分け直す配線が要った。純粋な判定 `TrainTimetableFetchTrigger` は残して L0 で試す | — |
| D-13 | UITK の操作の録画は `scenarios/uitk/uitk-train-inventory.cs` に新設する（alpha の `uitk-block-inventory`・S4-machine の `uitk-machine-ui` と同じ形）。`train/train-timetable-via-ui.cs` は Web 系でも今は壊れている（無い適用ボタンを押す）ので、その場で両系で通る形に直す（押下を消し、待ちはサーバーとクライアントの時刻表の駅数だけ）。`train/train-station-default-name.cs` は触らない。出所: レビュー r1 | Web 系で回す録画に UITK だけで成り立つ手順を足すと Web 系が退化する。時刻表の録画は直さないと Web 系の壊れが残り、両系で成り立つ待ち方がある。系ごとの保留の印（UITK `--busy`・Web `aria-busy`）は Web の DSL から読めない | 既存の 2 本をその場で UITK の操作へ延ばす（旧版。Web 系の `train-station-default-name` が退化する）／時刻表の録画も `uitk/` へ分ける（Web 系の壊れが残る） |
| D-14 | 自動運転と積込/卸しは楽観更新しない。押すと即送り、選択はサーバーの公開でだけ替わる | Web と同じ。`ModeSwitch` も押下で自分では選ばない | 押した瞬間に選択を替える |
| D-15 | 駅名の拒否は待ちのあいだに届いた最新の同期名へ戻す。「未編集」は前の同期値と同じ入力とみなす | Web `TrainStationNameSection`（`latestName` と `previousName` の比べ方）と同じ。拒否のまま dirty で残り再同期されないのを防ぐ | 編集フラグで持つ（打ち戻した入力が同期に従わない差が出る） |
| D-16 | 貨車・PF のスロットは alpha のパネルの源（`IBlockInventoryPanelSource.Current` の `OpenTrainInventoryPanel`／`OpenBlockInventoryPanel`〈エラー無し〉の `ItemSlots`）を Model が購読して読む。液体だけを `SubInventoryState`（貨車）・`IBlockFluidSlotsModel`（PF）から読む。本文の工場は「Presenter＋View」の組を返し、Model は DI の単一インスタンスで起動時に源を購読する。出所: レビュー r1（スロットの源を替えた） | パネルの源はスロットの更新でも配り直すので、`Current` を購読すれば更新が届く。`IItemStack → SlotItem` の写しを State にもう 1 本作らない（alpha のパネルと `LocalPlayerInventoryOperations` に既にある） | `SubInventoryState` からスロットを自前で写す（旧版。同じ役目の 3 本目）／パネルの `ItemSlots` を `Attach` の引数で一度だけ描く（更新が届かない） |
| D-17 | 液体 PF の液体は S4-machine の `IBlockFluidSlotsModel` を読む。出所: レビュー r1 | サーバー `TrainPlatformFluidContainerComponent.cs:172-173` が `FluidMachineInventoryStateDetail` を出し、S4-machine の `BlockFluidSlotsModel` は追跡ブロックのその StateDetail を読む（machine R-3）。`SubInventoryModel.SetFluids` を呼ぶのは `TrainSubInventorySource.cs:59` だけなので、液体 PF（ブロック）では `SubInventoryModel.Fluids` は空のまま。Web ホスト `BlockDetailDtoBuilder.cs:104-109` も同じ StateDetail から組む。Task 1 Step 2 で液体 PF を追跡しているあいだ `Slots` が空でないことを確かめる | `SubInventoryModel.Fluids` を読む（液体 PF の行が空になる） |
| D-18 | 編集セッションの段階は private な判別 union（`Idle`・`Sending(stops)`・`AwaitingPublication(stops, timeout)`）1 本で持つ。`_closed` は寿命なので別。表示は `DisplayedStops`・`IsPending` と変化の通知 `OnDisplayChanged` 1 本で出す。出所: レビュー r1 | null の `_pendingStops` と bool の `_accepted` の組では「受理済みなのに並びが無い」等のありえない組が書ける。通知を 1 本にすると、並びと保留の片方だけが替わった途中の値を配らない | null と bool の組（旧版） |
| D-19 | 描画値は有無を型で表す: 貨車の本文の値から `TrainCarInstanceId`（-1 で閉）を消し（識別はパネルの状態が持つ）、区画の現在位置は `int?`、PF の本文は `ClosedTrainPlatformBody`／`OpenTrainPlatformBody`。View は「あれば出す」の 1 規則で描く。PF の部分の有無は当初「無ければ null」だったが、**その部分は覆した（D-34。`SectionContent<T>` にした）**。出所: レビュー r1・moores-code-review | 数値の番兵と、判別 bool と片側だけで意味のある値の同居をやめる。Web の `trainStation?`・`trainPlatform?` の省略可能な詳細と同じ意味 | `-1`・`IsStation`＋`StationName ""`・`HasTransferMode`＋既定の `LoadToTrain`（旧版） |
| D-20 | 取得の依頼の直後の読込中は、既存 `TrainTimetableFetcher` に開始の合図 `OnFetchStarted`（取得中を立てた直後に流す）を足し、`TrainTimetableModel` だけが購読して組み直して出す。`ITrainTimetableSource` から `Refresh` を外し、取得を頼む側（D-33 で列車の本文の Presenter。当初は `TrainInventoryTabModel`）は頼むだけにする。既存の `OnFetchFailed`・`OpenTrainTimetableTracker`・Web ホストは変えない。Fetcher の 200 行は取得ラッチを `TrainTimetableFetchLatch` へ出して守る。開始の合図は Hostless の `TimetableStateProbe` で確かめる（本番の Fetcher は送信境界に触れるので L0 では試せない）。出所: レビュー r1・r2（手呼びから購読へ）・r3（既存の通知を広げず別に足す） | AGENTS.md「状態変化の検知は購読で」。取得中を立てる場所（Fetcher）が知らせれば、源の読み手が誰でも `Refresh` を呼べる口が要らない。E-1（ad92a44777）はマージ済みの State の口へ合図を「足す」ことを許す | 既存通知 `OnFetchFailed` の意味を「開始と失敗」に広げる（r2。Tracker 経由で Web ホストの配信が毎回 Ready→Loading→Ready になり、区画が作り直されて Web の挙動が変わる。既存の合図を変えるので E-1 の「足す」を越える）／頼む側が `ITrainTimetableSource.Refresh()` を手で呼ぶ（r1。源の読み手なら誰でも呼べる口になる）／`TrainTimetableCommands.RequestOpen` の中で呼ぶ |
| D-21 | 駅・PF の描画値の組み立て `TrainPlatformDetailReader.Read` は `BlockGameObject` を取らない純関数にし、StateDetail と BlockParam の取り出しは `TrainPlatformBodyModel` の 1 か所に置く。本文の種類は `BlockComponentRegistry`（Model はパネルの `Components.Body`、Commands は `Resolve(blockType).Body`）で判定し、駅かどうかは `BlockTypeConst.TrainStation` との比較で、Reader（表示の導出）と `TrainBlockCommands.SetStationName`（送信の門。Web ホスト `TrainStationActions` と同じ）の 2 か所（役割が違い、同じ定数との比較なので食い違わない。レビュー r2 で記述を実物に合わせた）。駅一覧の整列も `TrainTimetableStation` の列を受ける。出所: レビュー r1 | S4-machine の「状態詳細 → 値の写しは `BlockGameObject` を取らない純関数で L0 で試す」に揃える。3 つの blockType の列挙を自前で持たない（alpha の登録表と食い違わない） | `Read(BlockGameObject, …)` と `IsTrainPlatformBlock`（旧版） |
| D-22 | 見た目の直し: Mantine `Text size="sm"` の 9 か所（自動運転の見出し・自動運転不可の文・停車駅と駅一覧の見出し・空の文 2・読込中・使えない・駅名の見出し）に `UiTypographyClass.TextSm`。停車駅・駅一覧の行ラベル（Web は型の付かない `<span>` で Mantine の body 16px を継ぐ）は `--mantine-font-size-md`。見出しの内側余白は器（`train-timetable__section-heading`）に、文字の型はラベルに付ける。駅のラベル（Web は継承の `<span>`）は `margin: 0; padding: 0`。行の右の子（停車駅の操作・駅一覧の `+`・自動運転の `ModeSwitch`、駅名の入力欄と決定）に画面のクラス `…__row-end` を付けて間を 1 本の規則で取る。駅名の入力欄は伸ばさない。USS の出所のコメントは日本語 1 行 → 英語 1 行。UITK の USS に兄弟の結合子が無いので、2 個目以降の操作・2 行目以降の行は C# が `--following` のクラスを付ける（前例 `ModeSwitch`）。駅名の入力欄には幅の当て先 `train-station-name__input` を付ける。出所: レビュー r1・レビュー r2（数・行ラベルの字・結合子・入力欄のクラス） | 部品のクラスを画面 USS の主語にしない規約のまま、Web の `gap` を全行で抜けなく写す。Web の入力欄は `flex-grow` を持たない | 見出しの器とラベルに同じクラス（`padding: 0` と取り合う）／`train-timetable__controls` だけに間（自動運転の行・駅一覧の行で抜ける） |
| D-23 | 共有テスト「%sから禁止Mantine部品とテーマ色を除去する」は、(a) USS の色の直書きが無いことを規約検査と同じ `UiStyleConventionRules.CheckUss` の `ColorLiteral` で、(b) 列車・駅の本文の木の操作の要素がすべて共通部品（`ModeSwitch`・`PanelActionButton`・`IconButton`・`GameTextField`・`BlockItemGrid`・`FluidInventoryContents`）で素の `Button`・`Toggle`・`RadioButton`・`Slider`・`ProgressBar`・`TextField` を画面が作らないことを主張する。出所: レビュー r1 | Web の元テストはソースに禁止部品のタグとテーマ色が無いことの 2 つを見る。仮の正規表現は規約検査と判定がずれる | USS の色だけを見る（旧版。素の `Button` を使っても通る） |
| D-24 | R-13 の受け入れは既存の `PauseMenuCommandsTest`（`UiCommandRejectReason` の全列挙を回す良性の配列とトースト用の翻訳文の 2 件）にする。足す値はどれも非良性なので、良性の配列とテスト名（`PauseMenuCommandsTest.cs:76` の今の名前）は変えない（当初の「良性の `TrainBlockNotOpen` を配列に足しテスト名を直す」は D-32 で覆した）。出所: レビュー r1・r3・moores-code-review | 旧版が挙げた `UiCommandRejectReasonTraitsTest`・`LocalizationKeysTest` は実在しない | 新しいテストを作る（同じ全列挙が 2 本になる） |
| D-25 | 駅名の拒否で戻すのは、送った回の下書き（`ApplyStationName` で取った参照）が今の下書きと同じときだけ。下書きは閉じる・別のパネルのたびに作り直すので開閉の世代も兼ねる。出所: レビュー r2 | `IsSamePanel` は位置・ブロック・エラーだけを比べるので、送る → 閉じる → 同じ駅を開いて打ち直す、で旧要求の拒否が新しい入力を消していた。Web は送った回のコンポーネントの `setName` を呼ぶだけで、開き直した欄は触られない | パネルの識別で比べる（r1。同じ駅を開き直すと見分けられない）／開閉の世代の数を別に持つ（下書きの参照で足りる） |
| D-26 | **覆した（D-32）**。旧: 駅・PF の「開いていない」は良性の `TrainBlockNotOpen` で返しトーストに出さない。出所: レビュー r2 | 旧の理由「Web と同じく画面に出ない」は事実と逆だった。Web の `BENIGN_ERRORS`（`moorestech_web/webui/src/bridge/transport/actions.ts`）に `train_timetable.*`・`train_station.set_name`・`train_platform.set_transfer_mode` の項目は無く、Web はどの拒否もトーストする | — |
| D-27 | `TrainInventoryBodyPresenter` は表示中のタブの Model だけを購読する（持ち物タブなら中身、時刻表タブなら時刻表と言語の変化。タブの購読の中で張り替え、購読の頭で最新値を 1 回描く）。区画と中身の要素は非表示のあいだも作ったまま（D-5）。出所: ADR の更新（ad92a44777 の §4.2 SetShown の注記）・レビュー r3（持ち物タブにも広げた） | 設計書 §4.2「SetShown は出し入れだけ。非表示の間に描かないのは Presenter が表示中だけ Model を購読して守る」。中身の Model はスロットの更新のたびに配り直す（D-16）。Web も持ち物タブ以外では中身を mount しない | 常に購読して非表示の区画・中身も描く（注記に反し、時刻表タブの間の貨物の更新で隠れた枠を描き直す）／時刻表だけ表示中購読（r2。持ち物側が却下した形のまま） |
| D-28 | View は列車の型（`TrainUnitInstanceId`。`Game.Block.Interface`）を持たず、区画の作り直しは State の `ReadyTrainTimetableArea.IsSameTrain` で決める。出所: レビュー r2 | `Client.Ui.asmdef` は `Game.Block.Interface` を参照せず（asmdef の参照は推移しない）、D-8 も View にサーバーの型を持たせない | `Client.Ui.asmdef` に参照を足す（D-8 に反する） |
| D-29 | 閉じたことは `TrainTimetableModel` が `UIStateControl.OnStateChanged` を購読して組み直し、使えないへ倒して編集の器に伝える（器 `TrainTimetableEditState` はセッションを閉じ、遅れて戻った受理・拒否を捨てる。D-33 で編集の Model から器へ移した）。L0 は器の約束（源が使えない → 捨てる）を、本番の通知経路は録画 `uitk/uitk-train-inventory` 手順 6 で確かめる。出所: レビュー r2 | `SubInventoryState` は開閉で `OnSubInventoryUpdated` を出さず、Tracker の開閉の購読も再公開を出さないので、閉じても Ready のまま編集が続き、閉じた後に拒否のトーストと巻き戻し・受理の 5000ms の時間切れが走っていた。Web ホスト `BlockInventoryTopic` も `UIStateControl.OnStateChanged` で配り直す | 編集の Model がパネルの源を購読して閉じる（時刻表の源は Ready のまま残るので、閉じた後に同じ列車を開き直すと同じ中身の Ready が配り直されずセッションが開かない。源と編集の Model の閉じの入力がずれる窓ができる。なお選んだ案でも State の閉じの入力は 2 系統〈時刻表は `UIStateControl.OnStateChanged`、本文・PF・駅名の下書きは alpha のパネルの源の `Closed`〉で、どちらが先に倒れても源が使えなくなった時点で閉じるので食い違いは起きない）／`TrainTimetableModel` を L0 で組む（`UIStateControl` は MonoBehaviour で組めない） |
| D-30 | 編集セッションはサーバーの時刻表（`TrainTimetableReadyData`）を `Server` として持ち、編集の Model は `_session` 1 本にする。値の列の比べは `Enumerable.SequenceEqual`（自前の `SameItems` を消す）。出所: レビュー r2（5-A1・N-7） | 組で立てる・倒す 2 つのフィールド（`_session` と `_server`）を言葉の注で保証する形をやめる。`SequenceEqual` は既定の比較子（値の構造体の `Equals`）で同じ判定（前例 `CursorTooltipViewModel`） | 2 フィールドのまま注で保証する（r1）／`TrainTimetableValueEquality` を残す |
| D-31 | 列車の拒否は理由ごとの `UiCommandRejectReason` で返す（`TrainTimetableReplaceFailed`・`TrainAutoRunFailed`・`TrainAutoRunNotApplied`・`TrainInvalidBlockType`・`TrainStationNameFailed`・`TrainPlatformModeFailed`、と共有の `BlockNotOpen`）。wire は Web ホストの理由の頭（`replace_failed` 等）、文言キーは値ごとに `ui.commandRejected.<値>`。`Detail` は既定 `""` で、サーバーの `FailureReason` を載せるときだけ入れる。組み立ては `TrainCommandRejection.Reject(logTag, reason, serverFailureReason)` の 1 か所（D-9 を覆した）。出所: moores-code-review（設計判断 D1） | `ToWireCode` は「Detail があればそれ」を返すので、Detail に wire を詰めると Detail の意味（補足）が崩れる。前例 `PauseMenuCommands.SubmitBugReport` は失敗コードだけを Detail に入れる。理由ごとの文言があれば利用者に何が断られたかが伝わる | 1 つの `TrainEditRejected` に寄せ Detail に Web の理由文字列を入れる（D-9 の旧案。どれも同じ文言で、Detail の契約を曲げる） |
| D-32 | 足す拒否は全部非良性でトーストする。「開いていない」（駅・PF の `block_not_open`、時刻表の `train_not_open`、時刻表が開いていない編集）は S4-machine と共有の `BlockNotOpen` 1 値（wire `block_not_open`・文言キー `ui.commandRejected.blockNotOpen`。csv の行は S4-machine 計画と一字一句同じ）。S4-machine と S4-train の先にマージした方が列挙・3 つの switch・csv に足し、後の方は使うだけ（着手時に grep で確かめ、取り込みで両側にあれば 1 行に畳む）。`PauseMenuCommandsTest` の良性の配列と名前は変えない（D-26 を覆した）。出所: moores-code-review（設計判断 D2。S4-machine D-38 と同じ裁き） | Web の `BENIGN_ERRORS` に列車の操作の項目は無く、Web はどの拒否もトーストする。`IsBenign` の契約（Web の BENIGN_ERRORS にあるものだけ良性）と alpha D-56 にそろう。同じ意味・同じ wire の値を 2 レーンで 2 つ持つと取り込みで衝突する。時刻表の wire が Web ホストの `train_not_open` と違うのはログの文字列だけで、利用者に見える振る舞い（トースト）は Web と同じ | 良性の `TrainBlockNotOpen`（D-26 の旧案。Web と挙動が逆）／時刻表だけ `TrainNotOpen` を別に足す（同じ意味の値が増える） |
| D-33 | 編集は前例 `PauseMenuStateService`＋`PauseMenuModel`＋`IPauseMenuCommands` と同じく 3 つに分ける: 器（`TrainTimetableEditState`〈列車ごとのセッション〉・`TrainStationNameEditState`〈駅名の下書き〉。DI の単一インスタンス）・描画値の Model（`ITrainTimetableEditModel.Current`・`ITrainPlatformEditModel.StationNameInput`。Presenter が購読）・コマンド（`ITrainTimetableEditCommands`・`ITrainPlatformEditCommands`。View が直接呼び〈A-8〉、`UiCommandResult` を返す。サーバーの結果はコマンドがトーストへ渡すので View は戻り値を読まない）。器を共有するのは、View が直接呼ぶコマンドと Presenter が購読する描画値が同じセッション・下書きを見るためで、下書きは遅れて戻る拒否が送った回を見分けるまで Presenter より長く生きる（D-25）。タブはパネルごとの局所状態なので State に置かず、列車の本文の Presenter が `TrainTimetableFetchTrigger` を 1 つ持ち、`IsSamePanel` が偽のパネルで作り直す（前例 S4-machine の `MachineSelectionModeState`）。View はタブの押下を `OnTabSelected` で出す。送信の 3 本は `TimetableEdit/Send/` へ出す（10 本の上限）。D-12 を覆した。出所: moores-code-review（設計判断 D3） | 1 つの Model に描画値と操作を同居させると、偽の `ReactiveProperty` で Presenter＋View を試す L1 の形（`FakeModel<T>`）に合わず、操作の結果も返せない。前例の分け方に揃えると、Model は `Current` だけ・コマンドは結果を返す形になる。タブは alpha が別のパネルで本文を作り直すので Presenter の寿命で足り、State のパネルの購読が要らない | 編集の Model に操作も持たせる（旧版）／器を作らず Model とコマンドがそれぞれ源を購読する（同じセッションが 2 つでき、表示と送信がずれる）／タブも State の Model に残す（D-12 の旧案） |
| D-34 | `OpenTrainPlatformBody` の部分の有無（駅名・スロット・モード）は S4-machine の `SectionContent<T>`（`Client.Ui.State.BlockInventory.Common`。`IsPresent`・`Value`・`Absent`・`Present`）で配る。テストは `TrainTestPlatforms` の 3 つの組み立てで作る。D-19 の「無ければ null」を覆した。出所: moores-code-review（Fable） | 設計書 A-150 は「値を null 許容で配る」を Nullチェックの指針に反する案として却下し、同じ W4 の S4-machine は `SectionContent<T>` で有無を型にしている。2 計画で有無の表し方を揃える | null 許容のまま A-150 に例外を書く（同じ W4 で表し方が割れる） |
| D-35 | `TrainPlatformBodyModel` はパネルの源と `IBlockFluidSlotsModel.Current` だけを購読し、`OpenBlockDetailTracker` の 2 つの通知は購読しない（追跡ブロックは組むときに読む）。前提（液体の Model が追跡替え・状態変化のたびに参照の違う値で配り直す）は Task 1 Step 2 で確かめ、外れたら追跡の通知の購読へ戻す。出所: moores-code-review（efficiency） | 追跡の通知と液体の配り直しの両方を購読すると、状態変化 1 回で 2 回組み、1 回目は古い液体の列で組む。液体の Model は同じ 2 つの通知で組み直すので、その 1 本に乗れば新しい値で 1 回だけ組める | 3 本とも購読して同じ値は畳む（1 回目が古い液体で組まれる窓は残る）／`CombineLatest` で束ねる（入力が同じ通知から派生するので 2 回流れるのは変わらない。alpha D-55 と同じ形） |
| D-36 | 公開待ちの段階は、段階を `_phase` に入れてから時間切れを `Arm` で仕掛ける（`AwaitingPublicationPhase.Arm`・`Disarm`。`BackToIdle` が `Disarm` する）。出所: moores-code-review（Fable） | `new AwaitingPublicationPhase(stops, _timer.After(...).Subscribe(...))` は `Subscribe` が代入より先に評価され、同期で発火するタイマーだと Idle に戻した直後に発火済みの段階が入り、保留が解けない | タイマーは同期で発火しない前提を注で書く（偽物や将来の実装で崩れる） |
| D-37 | 列車の本文の `GameScrollView(train-body-scroll)` の高さは alpha の大型パネルが決める（`.block-inventory--large .block-inventory__panel` の高さと `block-inventory__body` の `flex-grow:1; min-height:0`）。列車の `.train-body`・`.train-body__scroll` は `flex-grow:1; min-height:0` でその鎖をつなぐだけで、自分で高さの上限を持たない。Task 10 の自撮り「本文の高さ」で確かめ、潰れる・伸びるなら鎖の切れた段を alpha の持ち主へ上げる。出所: moores-code-review（Fable） | Web も `BlockInventoryPanel.tsx` の「大型面は本文側が器を持つ」で、高さは外殻が決め、本文の `ScrollArea`（`panelBodyScrollArea` の `.scroll`）が残りを埋める。alpha Produces の「本文は自分でスクロール器を持たない」は自然高・区画の積み重ねの本文の話で、大型の本文は器を持つ（Web と同じ） | 列車の USS に高さの上限のトークンを足す（Web に無い値。外殻と二重に決める） |

## 設計書・実行計画へ反映する行

設計書 `docs/uitk-migration/execution/component-architecture.md` と実行計画へ本体が足す下書き（この計画では編集しない）。

| ID | 下書き |
|---|---|
| A-155 | 列車族の本文の置き場: State は `Client.Ui.State/Train/{Timetable,TimetableEdit,TimetableEdit/Send,Inventory,Platform,Platform/Edit}/`、View は `Client.Ui/Screens/BlockInventory/Train/{,Timetable/,Parts/}`。源の Model（`TrainTimetableModel`・`TrainInventoryBodyModel`・`TrainPlatformBodyModel`）と、編集の器（`TrainTimetableEditState`・`TrainStationNameEditState`）・描画値の Model（`TrainTimetableEditModel`・`TrainPlatformEditModel`。`Current` だけで Presenter が購読）・コマンド（`TrainTimetableEditCommands`・`TrainPlatformEditCommands`。View が直接呼び〈A-8〉`UiCommandResult` を返す）を DI の単一インスタンスで持つ（前例 `PauseMenuStateService`＋`PauseMenuModel`＋`IPauseMenuCommands`。器は同じセッション・下書きを Model とコマンドで共有するため）。タブはパネルごとの局所状態なので列車の本文の Presenter が持ち、別のパネルで作り直す。alpha の拡張口に差す工場 2 本が Presenter（購読して `Render` へ流す）＋View の組を作る。スロットは alpha のパネルの源から読む。登録は `BlockInventoryTrainGameUiRegistration`（S4-train 計画 D-1・D-16・D-33） |
| A-156 | 時刻表の即時編集 `TrainTimetableEditSession`（Web `useImmediateTimetable` の写し。段階は判別 union）: 編集は即送信・保留中の編集は捨てる・受理と同じ並びの公開で解く・受理後 5000ms 公開が無ければ解く（実時間。UI の通信待ちでティックの対象外）・拒否は最新のサーバーの並びへ戻す・閉じた後の結果は捨てる。閉じたことは `UIStateControl.OnStateChanged` で使えないへ倒して伝える。Web ホストにしかない導出は State に同じ規則でもう 1 本持ち（C4-e の agent 既定と同じ据え置き。負債が残る）、CEF 撤去の日に `Client.WebUiHost` 側を消す |
| A-157 | 列車の断りの理由は理由ごとの `UiCommandRejectReason`（`TrainTimetableReplaceFailed`・`TrainAutoRunFailed`・`TrainAutoRunNotApplied`・`TrainInvalidBlockType`・`TrainStationNameFailed`・`TrainPlatformModeFailed`。文言キーは `ui.commandRejected.<値>`）と、S4-machine と共有の `BlockNotOpen`（wire `block_not_open`・文言キー `ui.commandRejected.blockNotOpen`。先にマージしたレーンが足し、後のレーンは使うだけ）。どれも非良性で、Web と同じくトーストする（Web の `BENIGN_ERRORS` に列車の操作の項目は無い）。`Detail` は既定 `""` で、サーバーの `FailureReason` を載せるときだけ入れる（S4-train 計画 D-31・D-32） |
| A-158 | 駅名の欄は `GameTextField(GaugeInset)`＋`PanelActionButton`。送るのは決定の押下だけ、拒否は最新の同期名へ戻す。積込/卸しの選択肢の testid は `train-platform-mode-load` / `-unload`（Web に無い名前を UITK で足す） |
| 実行計画 §1.4 | S4-train 行（設計書の裁定ではなく実行計画の行の直しの下書き）: 写すテストの数を実物の 20 件（`train.spec` 1・`timetable.spec` 2・`TrainTimetableImmediate` 8・`TrainTimetableSection` 5・`TrainInventoryBody` 1・`trainPlatform.spec` 2・`TrainPlatformInventory` 1）＋共有 2 件に直し、依存に S4-machine（`FluidInventoryContents`・`IBlockFluidSlotsModel`）を足す。録画は `uitk/uitk-train-inventory`（新設）と `train/train-timetable-via-ui`（両系で通る形に直す） |

## 本 PR 外のリファクタ提案

Phase 2.6 の第 3 バケツ。このレーンのタスクにはしない。どれも D-2 の据え置き（C4-e の `done.md` 要裁定 1 と同じ agent 既定）の対象で、CEF 撤去の日に `Client.WebUiHost` 側を消せば 1 本に戻る。

- 既存 `Client.WebUiHost/Game/Topics/BlockDetail/Train/TrainTimetableDtoBuilder.cs`（`Build`・`SortStations`・`CreateFromTimetable`）と新設 `TrainTimetableStationCatalog`・`TrainTimetableReadyDataFactory`・`TrainTimetableModel.Build`: 駅ブロック → 駅一覧（`GetBlocksByBlockType(TrainStation)`＋駅名 StateDetail＋整列）と状態の優先が 2 本
- 既存 `Client.WebUiHost/Game/Actions/Train/TrainTimetableStopParser.cs` と新設 `TrainTimetableDeparture.ToServerStops`: 固定の出発条件を付ける写しが 2 本
- 既存 `Client.WebUiHost/Game/Actions/Train/TrainTimetableActions.cs`（`TrainTimetableActionSupport.Reject`）・`TrainTimetableOpenActionHandler.cs` と新設 `TrainTimetableCommands`・`TrainCommandRejection`: 置換・`AppliedIsAutoRun` の照合・拒否文字列の組み立てが 2 本（開いた列車の解決と取得の依頼は両方が既存の `OpenTrainUnitResolver`・`TrainTimetableFetcher` を呼ぶので 1 本）
- 既存 `Client.WebUiHost/Game/Topics/BlockDetail/TrainPlatformDetailDtoBuilder.cs`・`Train/TrainStationDetailDtoBuilder.cs` と新設 `TrainPlatformDetailReader.Read`: 駅名・モード・スロットの有無の導出が 2 本
- 既存 `Client.WebUiHost/Game/Actions/TrainPlatformActions.cs`（`IsTrainPlatform` の 3 つの blockType）と新設 `TrainBlockCommands.SetTransferMode`（`BlockComponentRegistry` で判定）: 種別の判定が 2 本。Web ホスト側も `BlockComponentRegistry` に寄せられる
- 既存 `Client.WebUiHost/Game/Topics/BlockDetail/TrainPlatformDetailDtoBuilder.cs`（`ToWireMode`）・`Client.WebUiHost/Game/Actions/TrainPlatformActions.cs`（`ParseMode`）と新設 `TrainPlatformTransferModeConversion`（`FromServer`・`ToServer`）: サーバーの移送モードの写しが 2 系統（レビュー r2 で追加）
- 既存 `Client.WebUiHost/Game/Actions/Train/TrainStationActions.cs` と新設 `TrainBlockCommands.SetStationName`: 開いているブロックの解決・種別検査・送信・拒否文字列が 2 本
- 既存 `Client.WebUiHost/Game/Topics/TrainInventoryDtoFactory.cs` と新設 `TrainCarFluidConversion`（スロットは alpha のパネルの源を使うので液体だけ）: 貨車の液体の写しが 2 本
- 既存 `Client.Ui.State/Inventory/Actions/LocalPlayerInventoryOperations.cs`（`ToSlotItem(IItemStack)`）: 旧版はこれと同じ `IItemStack → SlotItem` の写しを State にもう 1 本作る所だったが、D-16 でパネルの源を使うので新設しない（記録だけ）

## 起草メモ（本体向け）

- **数の食い違い（申告）**: 本体の指示は「実物の 21 件」だが、実物で数えると S4-train の持ち分は **20 件**。下調べの 21 件は `test-name-parity --bundle 4` の出力に S4-research の `ResearchDetailPane` の 1 件が混ざっていた。内訳は「写すテスト」表と、上の「設計書・実行計画へ反映する行」の実行計画 §1.4 の下書き。`train.spec` の「乗車HUD…」（S2-hud 済み）は除き、`TrainPlatformInventory.test.tsx` の 1 件は足した。ほかに alpha が train に割り振った `blockInventoryDesign.test.ts` の共有 2 件を自分のファイル `TrainDesignTest` に同じタグで写す（alpha の `BlockInventoryDesignTest` には足さない）
- **レビュー r1 の反映**: 再現した・一部の節を推奨どおりに直した（D-6・D-8・D-12・D-13・D-16 を直し、D-17〜D-24 を足した。どれも「出所: レビュー r1」）。§15 は A-8 に合わせて State に編集の Model 3 つを置き、Presenter を購読だけにした。§16 は推奨と違い、C4-e の要裁定 1 と同じく重複を据え置いて（r2 で「agent 既定」と呼び名を直した） D-2 の根拠を書き直し、重複を「## 本 PR 外のリファクタ提案」に並べた。§14 は Web 系の録画を退化させず、UITK の録画を `uitk/` に新設し、壊れていた `train-timetable-via-ui` は両系で通る形に直した。§20（`Localize.Get` の欠損）は再現しなかったので直していない。別に見つかった `IsSelected` の 1 引数の呼び方 5 か所は `IsSelected(root, optionTestid)` に直した
- **S4-machine の型**: 計画（2f48c85880）の Produces から写した: `FluidInventoryContents : VisualElement`（中に `UiStack(Xs)`。文は行の兄弟）・`FluidInventoryContents(testid, GameContentPresentation)`・`Render(IReadOnlyList<BlockFluidSlotState>)`、液体の Model `IBlockFluidSlotsModel`（液体 PF の液体はこれを購読。D-17。machine の R-3 も「液体プラットフォームもブロックで同じ経路、貨車は空」に直っている）、`BlockFluidSlotConversion.FromTanks`（貨車の液体の変換に入力側だけで使う）。貨車の液体は `IBlockFluidSlotsModel` の対象外なので `SubInventoryState` の `Fluids` を `TrainInventoryBodyModel` が読む
- **alpha の型**: 改訂版（85f905aa3d・44c416e9e4）の Produces から写した（`ITrainInventoryPart`・`IBlockInventoryPart`・`Attach`/`Detach`・`BlockInventoryBodyMount(bool)`・`IBlockInventoryPanelSource`）。スロットはパネルの源を Model が購読して読む（D-16）。`IBlockInventoryOperations.TryReadBlockSlots` はエラー無しのブロック／列車のときだけ真（alpha D-37）で、貨車の枠の押下もこの条件で通る。列車の本文の工場が未登録のあいだ alpha は空の部分を返す（alpha D-35）。`BlockInventoryPanelLayout` は 3 つの static でしか作れないが、このレーンは大型パネルの寄与を足さないので触らない。テストの外殻は `BlockInventoryTestShell.Create(this, state, plugged)`
- **共有ファイルへの追記（E-1。fce54ea0b5 の決まり）**: `Client.Ui.State.asmdef` は無い参照だけを足し、取り込みで同じ参照が衝突したら 1 行に畳む。`UiCommandRejectReason` は列挙と switch 3 本の末尾に 7 行ずつ足す（共有の `BlockNotOpen` は S4-machine が先にマージしていれば足さず使うだけ。取り込みで両側にあれば 1 行に畳む。D-32）。どれも非良性なので `PauseMenuCommandsTest` は変えずに緑のまま。`localization.csv` に 7 行（共有の 1 行が既にあれば 6 行。`gen:i18n` の再生成と追従記録）。`MainGameUiRegistration` に 1 行・`GameUiTheme.tss` に 2 行
- **integrator への依頼（Task 1 で done.md に書く）**: (a) `PanelActionButton` の無効の見た目（今は画面のつまみの上書きで Web に合わせた。D-11）、(b) 自撮りの参照（駅名の行・Loading/Unavailable・保留中・液体貨車）の撮影表への追加、(c) 登録ファイルは E-1（ad92a44777）の `Registration/GameUi/Screens/` に置く（先に作ったレーンが無ければこのレーンが作る）、(d) 駅名の入力欄の幅のトークンを足したときはその名札付きブロック（Task 10）、(e) 「開いていない」は S4-machine と共有の `BlockNotOpen` 1 値（D-32）。先にマージしたレーンが列挙・switch・csv に足し、後のレーンは使うだけ。取り込みで両側にあれば列挙と csv の行を 1 つに畳む。共有の新部品は作っていない
- **設計書との食い違い**: 設計書は `GameScrollView.ScrollTo` を書いているが実物に無い（`SetScrollOffset` 等だけ）。S4-train は使わないので影響は無い
- **レビュー r2 の反映**: 成り立った 17 件を各節の既定案どおりに直した（D-2・D-20・D-21・D-22 を直し、D-25〜D-30 を足した。どれも「出所: レビュー r2」）。成り立たなかった 5-I6 と、拾わない弱い 9 件は直していない。ADR の更新（ad92a44777）に合わせ、登録ファイルを `Registration/GameUi/Screens/` に置き、時刻表は表示中だけ購読する（D-27）。`block_not_open` は S4-machine と揃えて良性にした（D-26。moores-code-review で覆し D-32）
- **レビュー r3 の反映**: 成り立った 11 件を既定案どおりに直した（D-20・D-24・D-26・D-27・D-29 を書き直した）
- **moores-code-review の反映**: 設計判断 D1〜D3 と Fable・efficiency の指摘を直した（D-9・D-12・D-19・D-26 を覆し、D-31〜D-37 を足した。どれも「出所: moores-code-review」）。設計書 A-155・A-157 の直しの下書きは「設計書・実行計画へ反映する行」に置いた（設計書は本体が直す）
- **要裁定: なし**。D-2 の据え置きは C4-e の agent 既定に揃えたもので、指揮が done.md の要裁定に上げる
- **未確認（実装時に確かめる）**: `PanelActionButton` に既定テーマの無効の不透明度が乗るか（D-11 で `opacity: 1` を当てた）、テキスト欄にフォーカスがあるあいだ E や閉じるキーが入力欄へ食われず UI の閉じが効くか（死活表の要確認。Task 11 Step 2 手順 3）、alpha の大型パネルの中で本文が縦に伸びるか（`.train-body` の `flex-grow`）、駅名の入力欄の自然な幅（Task 10 で Web と測り比べる）、`border-top-width: var()` が UITK で効くか（Task 6 の注の代替あり）、`UiIcon` の名前の綴りと液体のアイコンの要素のクラス名（`TrainTestText`。Task 1 Step 2 で合わせる）、取得ラッチの分割で Fetcher の振る舞いが変わらないか（Task 3 Step 4・Task 11 Step 3）、`UniTaskCompletionSource` の継続が同期で走るか（Task 4・7 の注の代替あり）、`OpenTrainTimetableTracker.OnRepublishRequested` が駅名の変化を運ぶか、`TrainTimetableSnapshot`・`TrainUnitInstanceId` をテストで組めるか、`BlockInventoryTestPanels.Train(error)` が毎回同じ車両で組むか（`TrainInventoryBodyTest`「隠して戻した同じ車両はタブを保ち…」が同じ車両の Attach で組む）、液体の Model が状態変化のたびに参照の違う値で配り直すか（D-35。Task 1 Step 2）、大型パネルの中で列車の本文のスクロール器に高さが付くか（D-37。Task 10 の自撮り「本文の高さ」）

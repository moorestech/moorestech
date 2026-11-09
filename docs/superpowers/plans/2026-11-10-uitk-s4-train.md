# UITK S4-train（段階 4: 列車族のインベントリ）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Web 版の `features/blockInventory/train/*`（`TrainInventoryBody`・`TrainTimetableSection`・`StopList`・`useImmediateTimetable`）と `views/TrainPlatformInventory`（`TrainStationNameSection`・`TrainPlatformSection`）を、S4-alpha の外殻・拡張口と S4-machine の液体の中身表示（`FluidInventoryContents`）・液体の Model（`IBlockFluidSlotsModel`）を使って、Web と同じ見た目・同じ操作の UITK 画面にする。列車（source=train）の本文は貨車スロット・液体貨車・時刻表タブ（即時送信の編集・自動運転の切替）、ブロックの本文は TrainStation（駅名＋スロット＋積込/卸し）・TrainItemPlatform（スロット＋積込/卸し）・TrainFluidPlatform（液体＋積込/卸し）。L1・並べ比べ（720p・2560×1080）・録画（古い適用ボタン前提の `train/train-timetable-via-ui` を UITK の操作に直す）まで済ませて `feature/uitk` へ取り込む。

**Architecture:** 画面レーン（単位エージェント・Editor 要）。設計書 §4.2「Model の購読は Presenter、View は Render だけ」に従う。State（`Client.Ui.State/Train/` の新しい 4 ディレクトリ）に、源を購読して `IReadOnlyReactiveProperty<…ViewModel>` を配る Model を置く: (1) 時刻表の状態 `TrainTimetableModel`（`TrainUnitClientCache`・`IClientTrainTimetableLookup`・`TrainTimetableFetcher`・`BlockGameObjectDataStore`・`OpenTrainTimetableTracker` を購読し、Web ホスト `TrainTimetableDtoBuilder` と同じ規則で Loading / Unavailable / Ready〈停車駅・駅一覧〉を組む）、(2) 列車本文の中身 `TrainInventoryBodyModel`（`SubInventoryState` から貨車スロットと液体）、(3) プラットフォーム本文の中身 `TrainPlatformBodyModel`（`OpenBlockDetailTracker.TrackedBlock` の StateDetail から駅名・積込/卸し・スロット数、液体は S4-machine の `IBlockFluidSlotsModel`）。編集は `TrainTimetableEditSession`（`useImmediateTimetable` の写し。保留・受理・公開待ち 5000ms・拒否の巻き戻し）が `ITrainTimetableCommands`（本番はサーバーへ `TrainScheduleEditRequest` を送る）を呼ぶ。駅名の下書きは `TrainStationNameDraft`。View 側（`Client.Ui/Screens/BlockInventory/Train/`）は alpha の拡張口に差す工場 2 本（列車の本文・`BlockInventoryBodyKind.TrainPlatform` の本文）が「Presenter（Attach で Model と View の意図を購読し View.Render へ流す・Detach で切る）＋ View」の組を作る。View は自分で購読しない。見た目は Web と同名のトークン（`--mode-switch-gap`・`--mode-switch-padding-inline`・`--bevel-1`・`--bevel-c1`・`--text-high-contrast`・`--text-muted`）だけを使い、ローカルなつまみは持たない。

**Tech Stack:** Unity 6000.3.8f1 UI Toolkit、UniRx、UniTask、VContainer 1.10、NUnit＋`com.unity.ui.test-framework`、uloop CLI、プレイテスト DSL（`run-scenario.sh`）、node（test-name-parity）、pnpm（gen:i18n）、gh CLI、bd（Beads）

- **パスの略記**: `S/` = `moorestech_client/Assets/Scripts/`、`W/` = `moorestech_web/webui/`、`LOGS` = `../moorestech_logs`（このリポジトリの兄弟の記録 repo）、`SKILL` = `.agents/skills/unity-playmode-recorded-playtest`。Files 節とコマンドはリポジトリ相対の完全なパスで書く
- **正本**: 裁定 `.decisions/2026-11-06-*`（「段階2以降の画面もWeb版と同じ見た目にする」「長命ブランチの中は全体スイッチ1つ」「UITK切替までCEF由来の不具合は直さない」）、設計書 `docs/uitk-migration/execution/component-architecture.md`（§1・§2・§3・§4.1〜§4.3・§5.1・§7.1）、実行計画 `docs/uitk-migration/execution/execution-plan.md`（§1.4 の S4-train 行・§4.3・§5・§6.2・E-1・E-16・E-18〜E-23）、S4-alpha の計画 `docs/superpowers/plans/2026-11-10-uitk-s4-alpha.md`（改訂版 85f905aa3d）「## 後続レーンへの Produces 一覧」、S4-machine の計画 `docs/superpowers/plans/2026-11-10-uitk-s4-machine.md`（09b9a73226）の Task 3・Task 6 の Produces、本体の追加決定（2026-11-10。液体の中身表示は S4-machine が `Screens/BlockInventory/Fluid/` に作り S4-train は使うだけ・Model/Presenter/View の分け方）。食い違ったら 裁定 → 統合計画 → 設計書 → 実行計画 → この計画 の順で上が勝つ

## Requirements

設計の出所: 実行計画 §1.4 の S4-train 行（`TrainInventoryBody`・時刻表・駅名・プラットフォームの本文。`train.spec`・`timetable.spec`・`TrainTimetableImmediate.test`・`TrainTimetableSection.test`・`TrainInventoryBody.test`・`trainPlatform.spec`）、本体の決定 4 件（液体の中身は S4-machine・写すのは実物・古い録画は直す・Web ホストの導出の作り直しは重複として記録）、alpha の改訂（パネルは識別子だけ・各レーンが Model を持つ・工場は Presenter＋View の組）。

| ID | 要件 | 受け入れ基準 |
|---|---|---|
| R-1 | 列車の本文の器: `UiStack` に `ModeSwitch<TrainInventoryTab>(train-tabs, Horizontal, Tablist)`（`train-tab-inventory` 持ち物 / `train-tab-timetable` 時刻表）と `GameScrollView(train-body-scroll)`。間は `--mode-switch-gap`。開いた直後は持ち物タブ。同じ車両のあいだタブは保たれる | `TrainInventoryBodyTest`「asks C# to fetch only when the timetable tab is selected」が緑 |
| R-2 | 持ち物タブ: 液体があれば `FluidInventoryContents(train-inventory-fluids)`（S4-machine）、無ければ `BlockItemGrid(train-inventory-slots)`（alpha。押下は `IBlockSlotCommands`）。液体貨車は名前と `量 / 容量`、再取得した量へ描き直す | `TrainInventoryBodyTest`「液体貨車は名前と量容量を表示し、再取得した量へ更新する」・`TrainInventoryFlowTest`「貨車スロットとコンテナ不在エラーを表示する」が緑 |
| R-3 | 時刻表タブ: Loading は `train-timetable-loading`（`ui.blockInventory.timetableLoading`）、Unavailable は `train-timetable-unavailable`（`timetableUnavailable`）、Ready は `TrainTimetableSection`。Ready の区画は持ち物タブのあいだも作ったまま隠す（`display:none`。Web は hidden で保つ）。区画は `TrainUnitInstanceId` ごとに作り直す（Web の key） | `TrainInventoryBodyTest`「shows loading and unavailable as distinct states without warning」が緑 |
| R-4 | 取得の依頼: 時刻表タブへ切り替えた瞬間と、時刻表タブのあいだに Loading が届くたびに `ITrainTimetableCommands.RequestOpen()`（本番は `OpenTrainUnitResolver` で列車を引き `TrainTimetableFetcher.RequestForOpenedTab`、引けなければ `MarkTabOpenedWithoutTrain`。Web ホスト `TrainTimetableOpenActionHandler` と同じ）。Ready でもタブ選択では依頼する | `TrainInventoryBodyTest` の fetch 3 件が緑。録画で `[TrainTimetableFetcher] requesting timetable` が開いた直後 0・タブ選択で 1・開き直してタブ選択で 2 |
| R-5 | 時刻表の状態: Web ホスト `TrainTimetableDtoBuilder.Build` と同じ優先（所有列車が引けない→Unavailable〈警告〉・`IsUnavailable`→Unavailable・`IsFetchInFlight`→Loading・キャッシュ無し→Loading・Ready）。駅一覧は `GetBlocksByBlockType(TrainStation)` の位置と `TrainStationNameStateDetail.StationName`（無ければ空）を x→z→y で整列。停車駅の名前は駅一覧から引き、引けない位置の集合が前回と変わったときだけ警告。未知の Side があれば Error ログで Unavailable | `TrainTimetableReadyDataFactoryTest`・`TrainTimetableStationCatalogTest` が緑 |
| R-6 | 時刻表の区画: `UiStack(Xs)`（`train-timetable-section`。保留中は `train-timetable-section--busy`）。自動運転の行（`ui.blockInventory.timetableAutoRun` ＋ `ModeSwitch<bool>(train-timetable-auto-run, Horizontal, Group)` の `-on` / `-off`）。on は保留中かサーバーの停車駅が 0 件で無効、off は保留中で無効。押すと楽観更新なしで `SetAutoRun` を即送信。停車駅が 0 件なら `timetableAutoRunUnavailable` を出す | `TrainTimetableSectionViewTest`「blocks auto-run until an added stop is authoritative and sends toggles immediately」が緑 |
| R-7 | 停車駅: `GamePanel(train-timetable-stops-panel, Craft)` の題 `timetableStops`＋`FadeRule`。0 件は文言 `train-timetable-stops`（`timetableStopsEmpty`）、それ以外は `train-timetable-stops` の列に行 `train-timetable-stop-{i}`（現在位置は `train-timetable-stop--current`＝`--text-high-contrast`。表示中の並びがサーバーの並びと同じときだけ）。行のラベルは `timetableStopLabel`（`{index}. {station}`）。操作 `-up`（先頭で無効）・`-down`（末尾で無効）・`-remove`（保留中は全部無効） | `TrainTimetableSectionViewTest`「disables moves past either end and follows remote changes when idle」が緑 |
| R-8 | 駅一覧: `GamePanel(train-timetable-stations-panel, Craft)` の題 `timetableStations`＋`FadeRule`。0 件は `timetableStationsEmpty`。行は駅のラベル（`TimetableEditLogic.StationLabel`。名前が空なら `timetableUnnamedStation`）と `PanelActionButton(train-timetable-station-{x}_{y}_{z}-add, timetableAdd)`（保留中は無効）。行の上の区切りは `--bevel-1 solid --bevel-c1`、無効な操作の文字は `--text-muted` | `TrainTimetableFlowTest` 2 件が緑。並べ比べ R-15 |
| R-9 | 即時編集（`useImmediateTimetable` の写し）: 送信中・保留中の編集は警告して捨てる。並びが同じ編集は何もしない。編集は表示を即時に替え（保留）、受理されたら同じ並びの公開が届くまで保留を保つ（公開が先に届いていれば受理で解く）。受理後 5000ms 公開が無ければ警告して解く。拒否は警告・トースト・最新のサーバーの並びへ戻す。閉じた後に戻った結果は捨てる | `TrainTimetableEditSessionTest` 6 件が緑 |
| R-10 | 送信: 置換は `TrainScheduleEditRequest.CreateReplaceTimetableRequest(列車, 停車駅)`（出発条件は `WaitForTicks`・`GameUpdater.TicksPerSecond`。Web ホスト `TrainTimetableStopParser` と同じ）、自動運転は `CreateSetAutoRunRequest`（`AppliedIsAutoRun` が求めた値と違えば拒否）。列車が開いていなければ拒否。拒否は `UiCommandRejectReason.TrainEditRejected`（新設。詳細に Web と同じ理由文字列）で返し、呼び手が `UiCommandResultToaster.Report` へ渡す | `TrainTimetableCommandsTest`（純粋部分）と `TrainTimetableFlowTest`「拒否された追加を巻き戻し、次の追加で復帰する」が緑 |
| R-11 | プラットフォームの本文（Kind `TrainPlatform`）: `UiStack(Sm)`（`train-platform-view`）に、駅なら駅名の行、スロット数があれば `BlockItemGrid(train-platform-item-slots)`（空でも実枠を出す）、液体なら `FluidInventoryContents(train-platform-fluid-slots)`、移送の状態があれば `ModeSwitch<TrainPlatformTransferMode>(train-platform-mode, Horizontal, Group)` の `-load`（`trainPlatformLoad` 積込）/ `-unload`（`trainPlatformUnload` 卸し）。押すと楽観更新なしで `SetTransferMode` を送る | `TrainPlatformBodyTest` 3 件が緑 |
| R-12 | 駅名: `GameTextField(train-station-name-input, false, GaugeInset)`（プレースホルダ `stationNamePlaceholder`）と `PanelActionButton(train-station-name-apply, stationNameApply)`。手を付けていない入力は同期した名前に従う。送るのは決定を押したときだけ（入力のたびには送らない）。拒否は同期済みの最新の名前へ戻す。プラットフォームでは行を出さない | `TrainPlatformBodyTest`「sends station names only through Set and hides the field for platforms」が緑。録画 `train/train-station-default-name` の延長分 |
| R-13 | 新しい断りの理由: `UiCommandRejectReason.TrainEditRejected`（benign でない・wire code `train_edit_rejected`・文言キー `ui.commandRejected.trainEditRejected`）。`localization.csv` に行を足し `pnpm --dir moorestech_web/webui run gen:i18n` で再生成、`docs/uitk/TODO.md`「追従記録」に 1 行 | `UiCommandRejectReasonTraitsTest`（既存。全列挙を回す件）が緑。`LocalizationKeysTest` が緑 |
| R-14 | 写すテスト（実物で数え直した S4-train 分 20 件＋alpha が train に割り振った共有 2 件。下の「写すテスト」表）。`node tools/uitk/test-name-parity.mjs --bundle 4` で S4-train のファイルの未対応 0。`<web-test>` で写した件は `docs/uitk/disposition.md` に書かない | parity の出力に S4-train のファイルの未対応が無い |
| R-15 | 並べ比べ: 参照 `4-block-train`・`4-block-trainTimetable`・`4-block-train-timetable-tab`・`4-block-trainPlatform`・`4-block-trainFluidPlatform` を 1280×720・2560×1080 で撮り差の表。参照の無い場面（駅名の行・Loading/Unavailable・保留中・液体貨車）は E-22 の自撮りで足す | 差の表の全行が「同じ／直した／LX-121〜LX-125／他レーン持ち」のどれか |
| R-16 | 登録: `S/Client.Starter/Registration/GameUi/BlockInventoryTrainGameUiRegistration.cs`（Model・コマンド・タイマー・工場 2 本）を `MainGameUiRegistration` の画面登録区画から 1 行で呼ぶ。`GameUiTheme.tss` の `Screens` 区画に `@import` 2 行 | 全体スイッチ UITK で起動して `uloop get-logs --log-type Error` が 0 件。L2（`UiThemeImportBundleTest`・`UiStyleConventionTest`・`UiLayerDependencyDirectionTest`・`LookExceptionLedgerTest`）が緑 |
| R-17 | 録画: `train/train-timetable-via-ui`（適用ボタンを消し、追加の直後にサーバーの状態を待つ形へ直す）・`train/train-station-default-name`（入力→決定→サーバーの名前を待つを足す）・`train/fluid-inventory-via-interact`・`uitk/uitk-lifeline` を全体スイッチ UITK で回す | 4 本の `result.json` が PASS、mp4、`done.md` に `UiInputExclusivity.ActiveUiSystem` の値 |
| R-18 | 終わりは moores-code-review の全ブランチレビュー → pr-create（base `feature/uitk`・報告ページ）→ `origin/feature/uitk` の取り込み・compile・テスト → G1 緑 → `gh pr merge --merge` → bd を閉じる | `git log origin/feature/uitk` にこのレーンのマージコミット。`LOGS/harness/uitk/S4-train/done.md` の 1 行目が「状態: 完了」 |

**やらないこと（スコープ境界）**

- 外殻（題・閉じる・エラー行 `train-inventory-error`・大型の寸法・自然高のスクロール器）と拡張口そのもの（S4-alpha）
- 液体の中身表示 `FluidInventoryContents`・`FluidSlotRow`・ブロックの液体の読み手（S4-machine が `Screens/BlockInventory/Fluid/` と State に作る。S4-train は使うだけ）
- 乗車 HUD（S2-hud 済み。`train.spec.ts` の「乗車HUD…」は写さない）
- 時刻表の編集ロジック `TimetableEditLogic`（L0-s4b 済み）・`TrainTimetableFetcher`・`OpenTrainTimetableTracker`・`OpenTrainUnitResolver`（既存。変えない）
- 部品（`ModeSwitch`・`PanelActionButton`・`GameTextField`・`GameScrollView`・`GamePanel`・`FadeRule`）と Foundation は変えない。要るものは `done.md`「integrator への依頼」へ
- Web 側（`moorestech_web/`）と Web の橋（`Client.WebUiHost/**`）は変えない。例外は `localization.csv` の行を足したときの生成物の再生成だけ（E-1）
- 出発条件の UI（Web にも無い。固定の `WaitForTicks` 1 秒）

## Global Constraints

- **作業場所**: `moores-wt new uitk-lane/s4-train --from origin/feature/uitk`（Editor 要）。最初に `pwd` で worktree にいることを確かめる。Unity は自分の worktree の Editor だけを使い、起動・停止の前に `moores-wt status` で所有を確かめる。Mac mini の Editor は同時 2 本まで。Mac が逼迫したら Windows の `~/bin/winunity`（compile/test/play を ssh で回す）も使える
- **依存**: S4-alpha（拡張口・`BlockItemGrid`・`IBlockSlotCommands`・パネルの状態）と **S4-machine**（`FluidInventoryContents`・ブロックの液体の読み手）が `origin/feature/uitk` に入っていること（Task 1 Step 1 で確かめ、無ければ待つ）。ほかに C3-a（`GamePanel`・`FadeRule`・`PanelActionButton`）・C3-b（`GameScrollView`）・C4-c 系の `ModeSwitch`・`GameTextField`・L0-s4b（`TimetableEditLogic`・`BlockComponentRegistry`）。すべてマージ済み
- **登録の本数**: `S/Client.Starter/Registration/GameUi/` は alpha で 9 本、integrator のサブディレクトリ化の後に置く。Task 1 で再編後の置き場を確かめ、その時点の `GameUi/`（または再編先）に 1 本足す
- **規約**: 1 ファイル 200 行以下・1 ディレクトリ 10 ファイル以下（`.meta` を除く）・partial 禁止・`Func<>` 禁止・イベントは UniRx（`Action` 禁止）・try-catch 禁止（このレーンに外部境界は無い。ネットワークの失敗は API が null を返す）・日英 2 行コメント・デフォルト引数禁止・単純 getter/setter 禁止（`{ get; private set; }` と式本体の計算プロパティは可）・初期化は `Initialize`・null チェックは外部データ（サーバー応答・StateDetail）だけ・fail-closed の経路は開発者ログ・意図して黙る行は `silent-ok: <理由> / <誰が気づくか>`・`#region Internal` はメソッド内のローカル関数だけ・`.meta` を手で作らない・Prefab/シーンを手で編集しない・`Update()` で同値判定しない（購読）
- **Model / Presenter / View**: Model（State）だけが源を購読し `IReadOnlyReactiveProperty<…ViewModel>` を配る。Presenter（Screens）は `Attach` で Model と View の意図（`IObservable`）を購読し `View.Render(ViewModel)` へ流し、`Detach` で全部切る。View は購読しない（自分の子の部品の `OnClicked` 等を自分の意図の `Subject` へ流す配線だけは View が持つ。言語の変化は `LocalizedLabel` が自分で描き直す）
- **State は `Client.WebUiHost` を参照しない**。Web ホストにしかない導出（駅一覧の組み立て・整列・名前の補完・状態の優先・未知の Side・出発条件・`AppliedIsAutoRun` の照合・プラットフォーム/駅の詳細）は State に写し直す。重複は D-2 に記録
- **asmdef**: `S/Client.Ui.State/Client.Ui.State.asmdef` に `Game.Block`（`TrainStationNameStateDetail`・`TrainPlatformTransferStateDetail`・`TrainPlatformTransferComponent`）と `Core.Update`（`GameUpdater.TicksPerSecond`）を足す（共有ファイルへの追記）。`Client.Ui` には足さない（View は State の `TrainPlatformTransferMode` を使う。D-8）
- **USS**: 画面 USS の先頭に出所の 1 行（日英。`/* web: features/blockInventory/train/TrainTimetableSection.module.css …（日本語の要約） / (English) */`）。色はトークン経由だけ。部品のクラスを画面 USS の選択子の主語にしない。部品のつまみの上書きは画面のルートのクラスの下で（このレーンでは `--panel-action-button-text` の無効時の上書き 1 件。D-11）。`:root` のトークンを C# で読むときは読む要素のクラスで `--<部品>-<名>: var(--token)` と受け直す（`UssCustomStyleReader` の remarks。このレーンでは読まない）
- **計画の Produces と実物の照合**: alpha（改訂版 85f905aa3d）と S4-machine（09b9a73226）の型は計画から写した。両レーンの実装で綴りが変わっていれば Task 1 Step 2 で合わせる
- **使う部品と公開 API**（Components・Foundation・既存 State は `origin/feature/uitk` の実物、alpha と S4-machine はそれぞれの計画の Produces から写した）

```csharp
// ── alpha の口（改訂版 85f905aa3d の「後続レーンへの Produces 一覧」から写した）
namespace Client.Ui.Screens.BlockInventory.Registry
public interface ITrainInventoryBodyFactory { ITrainInventoryPart Create(); }                              // 登録は 1 本だけ
public interface ITrainInventoryPart { VisualElement Root { get; } void Attach(OpenTrainInventoryPanel panel); void Detach(); }   // Presenter（Root が View）
public interface IBlockInventoryPart { VisualElement Root { get; } void Attach(OpenBlockInventoryPanel panel); void Detach(); }
public interface IBlockInventoryBodyFactory { BlockInventoryBodyKind Kind { get; } IBlockInventoryPart Create(BlockInventoryBodyMount mount); }
public sealed class BlockInventoryBodyMount { public bool FillsPanelHeight { get; private set; } }
namespace Client.Ui.State.BlockInventory.Panel
public sealed class OpenTrainInventoryPanel : BlockInventoryPanelState { public long TrainCarInstanceId { get; } public IReadOnlyList<SlotItem> ItemSlots { get; } public BlockInventoryPanelError Error { get; } }
public sealed class OpenBlockInventoryPanel : BlockInventoryPanelState { public string BlockType { get; } public Guid BlockGuid { get; } public BlockId BlockId { get; } public BlockInstanceId BlockInstanceId { get; } public Vector3Int Position { get; } public IReadOnlyList<SlotItem> ItemSlots { get; } public BlockInventoryPanelError Error { get; } public BlockInventoryComponents Components { get; } }
// 呼ばれ方: 同じパネル（列車は車両・エラー、ブロックは位置・Guid・エラー）かつ同じ大きさのあいだは何も呼ばれない。変わると Detach → Create → Attach。画面が隠れると Detach
// テスト: Client.Tests.Ui.State.BlockInventory.PanelModel.BlockInventoryTestPanels.Train(error) / OfType(blockType, ...)・FakePanelSource・BlockInventoryTestShell.Create(state, params object[] plugged)
namespace Client.Ui.Screens.BlockInventory.Parts
public sealed class BlockItemGridFactory { public BlockItemGrid Create(string testId); }               // 共有サービス（MainGameUiRegistration）。枠 <testId>-slot-<i>。押下は IBlockSlotCommands へ配線済み
public sealed class BlockItemGrid : VisualElement { public void Render(IReadOnlyList<SlotItem> itemSlots, IReadOnlyList<int> slotIndices); }
namespace Client.Ui.State.BlockInventory.Logic.Common
public static class BlockDetailMath { public static IReadOnlyList<int> AllSlotIndices(int count); }

// ── S4-machine（計画 09b9a73226 の Task 3・Task 6 の Produces から写した）
namespace Client.Ui.Screens.BlockInventory.Fluid
public sealed class FluidInventoryContents : UiStack { public FluidInventoryContents(string testid, GameContentPresentation content); public void Render(IReadOnlyList<BlockFluidSlotState> fluids); }  // Web FluidInventoryContents。0 件で子なし。行 <testid>-slot-<i>
namespace Client.Ui.State.BlockInventory.Fluid
public sealed class BlockFluidSlotsViewModel { IReadOnlyList<BlockFluidSlotState> Slots; bool HasProgress; float Progress; }
public interface IBlockFluidSlotsModel { IReadOnlyReactiveProperty<BlockFluidSlotsViewModel> Current { get; } }   // 追跡ブロックの液体（タンクの入力→出力・パイプの複製）。閉・列車では空
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
namespace Client.Ui.State.Command
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
BlockGameObjectDataStore { IEnumerable<BlockGameObject> GetBlocksByBlockType(string blockType); }
TrainUnitClientCache;  IClientTrainTimetableLookup { IObservable<TrainUnitInstanceId> OnTimetableUpdated; bool TryGet(TrainUnitInstanceId id, out TrainTimetableSnapshot snapshot); }
TrainTimetableSnapshot { TrainUnitInstanceId TrainUnitInstanceId; bool IsAutoRun; int CurrentIndex; IReadOnlyList<TrainTimetableStop> Stops; }   TrainTimetableStop { Vector3Int StationPosition; StationNodeSide Side; }
ClientContext.VanillaApi.Response.Train { SendTrainScheduleEdit(TrainScheduleEditProtocol.TrainScheduleEditRequest, CancellationToken) → { Success; FailureReason; AppliedIsAutoRun }; SetTrainStationName(Vector3Int, string, CancellationToken) → { Success; AppliedName; FailureReason }; SetTrainPlatformTransferMode(Vector3Int, TrainPlatformTransferComponent.TransferMode, CancellationToken) → { Success; AppliedMode; FailureReason } }
TrainStationNameStateDetail { const string BlockStateDetailKey = "TrainStationName"; string StationName; }   TrainPlatformTransferStateDetail { const string BlockStateDetailKey = "TrainPlatformTransfer"; TransferMode Mode; }   // Game.Block.Blocks.TrainRail
TrainItemPlatformBlockParam / TrainStationBlockParam { int ItemSlotCount; }   TrainFluidPlatformBlockParam   // Mooresmaster.Model.BlocksModule
```
（`IClientTrainTimetableLookup.OnTimetableUpdated` の型、`BlockGameObjectDataStore.GetBlocksByBlockType` の戻りの型は Task 1 Step 3 で実物を確かめ、違えば下のコードの綴りだけ合わせる）

## 設計検査記録

- 配置検査: 未実施
- Phase 2.6: 未実施

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

**State**（`S/Client.Ui.State/Train/`。既存 5 本＋`Logic/` に、1 本と 4 ディレクトリを足す）

| ファイル | 責務 |
|---|---|
| `Train/TrainCommandRejection.cs` | 列車系コマンドの拒否を `UiCommandRejected(TrainEditRejected, 理由)` にし、Web ホストと同じ書式で Warning ログ |
| `Train/Timetable/TrainTimetableViewState.cs` | Loading / Unavailable / Ready の型 |
| `Train/Timetable/TrainTimetableReadyData.cs` | 用意済みの時刻表（列車・自動運転・現在位置・停車駅・駅一覧） |
| `Train/Timetable/TrainTimetableStationCatalog.cs` | 駅ブロックから駅一覧を組み x→z→y で整列（Web ホスト `TrainTimetableDtoBuilder` の写し） |
| `Train/Timetable/TrainTimetableReadyDataFactory.cs` | スナップショット＋駅一覧 → Ready（名前の補完・欠けの警告・未知の Side） |
| `Train/Timetable/ITrainTimetableSource.cs` | 時刻表の状態の口（テストは偽物） |
| `Train/Timetable/TrainTimetableModel.cs` | 本番の源。状態の優先を写し、変化の契機を購読 |
| `Train/TimetableEdit/ITrainTimetableCommands.cs` | 取得の依頼・置換・自動運転 |
| `Train/TimetableEdit/TrainTimetableCommands.cs` | 本番。`TrainScheduleEditRequest` を送る |
| `Train/TimetableEdit/TrainTimetableDeparture.cs` | 送信形 → サーバーの停車駅（固定の出発条件） |
| `Train/TimetableEdit/ITrainTimetableWaitTimer.cs` | 公開待ちの時間切れの口 |
| `Train/TimetableEdit/UniRxTrainTimetableWaitTimer.cs` | 本番。`Observable.Timer` |
| `Train/TimetableEdit/TrainTimetableEditSession.cs` | 即時編集（`useImmediateTimetable` の写し） |
| `Train/TimetableEdit/TrainTimetableSectionViewModel.cs` | 区画の描画値（現在位置を当てるか・自動運転の可否） |
| `Train/Inventory/TrainInventoryTab.cs` | タブの列挙 |
| `Train/Inventory/TrainTimetableFetchTrigger.cs` | タブ選択・Loading の到着 → 取得の依頼の判定 |
| `Train/Inventory/TrainInventoryBodyViewModel.cs` | 貨車の中身（スロット・液体） |
| `Train/Inventory/ITrainInventoryBodySource.cs` | 貨車の中身の口 |
| `Train/Inventory/TrainInventoryBodyModel.cs` | 本番。`SubInventoryState` を購読 |
| `Train/Inventory/TrainCarContentsConversion.cs` | `IItemStack` → `SlotItem`、貨車の `FluidMessagePack` → `BlockFluidSlotState`（S4-machine の `BlockFluidSlotConversion.FromTanks` を入力側だけで呼ぶ） |
| `Train/Platform/TrainPlatformTransferMode.cs` | State 側の積込/卸し（View が `Game.Block` を参照しないため） |
| `Train/Platform/TrainPlatformBodyViewModel.cs` | 駅名・スロット数・スロット・液体・モード |
| `Train/Platform/ITrainPlatformBodySource.cs` | プラットフォームの中身の口 |
| `Train/Platform/TrainPlatformBodyModel.cs` | 本番。追跡ブロック・`SubInventoryState`・S4-machine の `IBlockFluidSlotsModel` を購読 |
| `Train/Platform/TrainPlatformDetailReader.cs` | `BlockGameObject` → 描画値（Web ホスト `TrainPlatformDetailDtoBuilder`・`TrainStationDetailDtoBuilder` の写し） |
| `Train/Platform/ITrainBlockCommands.cs` | 駅名・モードの送信 |
| `Train/Platform/TrainBlockCommands.cs` | 本番 |
| `Train/Platform/TrainStationNameDraft.cs` | 駅名の下書き（同期・編集・拒否の巻き戻し） |

共有ファイルへの追記: `Commands/UiCommandRejectReason.cs`（列挙 1 つと switch 3 本に 1 行ずつ）、`Client.Ui.State.asmdef`（`Game.Block`・`Core.Update`）。

**View**（`S/Client.Ui/Screens/BlockInventory/Train/`）

| ファイル | 責務 |
|---|---|
| `Train/TrainInventoryBodyFactory.cs` | `ITrainInventoryBodyFactory` の実装 |
| `Train/TrainInventoryBodyPresenter.cs` | タブ・取得の依頼・中身と時刻表の配線 |
| `Train/TrainInventoryBodyView.cs` | タブ＋スクロール器＋中身＋時刻表の器 |
| `Train/TrainInventoryContentsView.cs` | スロットか液体か |
| `Train/TrainPlatformBodyFactory.cs` | `IBlockInventoryBodyFactory`（Kind `TrainPlatform`） |
| `Train/TrainPlatformBodyPresenter.cs` | 駅名の下書き・送信・モード送信 |
| `Train/TrainPlatformBodyView.cs` | 駅名の行・スロット・液体・モード |
| `Train/Train.uss` | 本文の間隔 |
| `Train/Timetable/TrainTimetableAreaView.cs` | Loading / Unavailable / 区画の出し分け |
| `Train/Timetable/TrainTimetablePresenter.cs` | 列車ごとの編集セッション・区画の意図 → コマンド |
| `Train/Timetable/TrainTimetableSectionView.cs` | 自動運転の行と 2 つの枠 |
| `Train/Timetable/TrainTimetableStopListView.cs` | 停車駅の列 |
| `Train/Timetable/TrainTimetableStationListView.cs` | 駅一覧の列 |
| `Train/Timetable/TimetableStopMoveRequest.cs` | 上下移動の意図の値 |
| `Train/Timetable/Timetable.uss` | `TrainTimetableSection.module.css` の写し |
| `Train/Parts/TrainStationNameRow.cs` | 駅名の入力と決定 |
| `Train/Parts/TrainPlatformModeSection.cs` | 積込/卸しの切替 |

**登録**: `S/Client.Starter/Registration/GameUi/BlockInventoryTrainGameUiRegistration.cs`（Task 1 で integrator の再編先を確かめる）、`MainGameUiRegistration.cs` の画面登録区画に 1 行、`moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss` の `/* == section: Screens == */` に 2 行。

**テスト**（`S/Client.Tests/Ui/`）

| ファイル | 中身 |
|---|---|
| `State/Train/Timetable/TrainTimetableStationCatalogTest.cs` | 整列 |
| `State/Train/Timetable/TrainTimetableReadyDataFactoryTest.cs` | 名前の補完・警告の重複抑止・未知の Side |
| `State/Train/TimetableEdit/TrainTimetableEditSessionTest.cs` | `TrainTimetableImmediate.test.ts` 1〜6 |
| `State/Train/TimetableEdit/TrainTimetableDepartureTest.cs` | 出発条件 |
| `State/Train/Inventory/TrainTimetableFetchTriggerTest.cs` | 依頼の判定 |
| `State/Train/Platform/TrainStationNameDraftTest.cs` | 下書き |
| `State/Train/Fakes/FakeTrainTimetableCommands.cs` | 完了を手で返す偽コマンド |
| `State/Train/Fakes/FakeTrainBlockCommands.cs` | 〃 |
| `State/Train/Fakes/ManualTrainTimetableWaitTimer.cs` | 手で発火する時間切れ |
| `State/Train/Fakes/FakeTrainSources.cs` | 3 つの源の偽物 |
| `State/Train/Fakes/TrainTestData.cs` | 駅・停車駅・Ready の組み立て |
| `Screens/BlockInventory/Train/TrainInventoryBodyTest.cs` | タブと取得 4 件＋液体貨車 1 件 |
| `Screens/BlockInventory/Train/TrainInventoryFlowTest.cs` | alpha の外殻ごと（`train.spec`） |
| `Screens/BlockInventory/Train/TrainTimetableFlowTest.cs` | `timetable.spec` 2 件 |
| `Screens/BlockInventory/Train/TrainPlatformBodyTest.cs` | `trainPlatform.spec` 2・PF 1・駅名 1 |
| `Screens/BlockInventory/Train/TrainDesignTest.cs` | 共有 2 件 |
| `Screens/BlockInventory/Train/TrainTestBodies.cs` | 工場を偽の源で組む |
| `Screens/BlockInventory/Train/Timetable/TrainTimetableSectionViewTest.cs` | `TrainTimetableImmediate.test.ts` 7〜8 |

**録画**: `.agents/skills/unity-playmode-recorded-playtest/scenarios/train/train-timetable-via-ui.cs`（直す）、`scenarios/train/train-station-default-name.cs`（延ばす）。

## Tasks

### Task 1: 前提の確認と記録の準備

**Files:**
- 記録: `LOGS/harness/uitk/S4-train/done.md`（新規。1 行目「状態: 作業中」）

- [ ] **Step 1: 依存が入っているか確かめる**

```bash
pwd
git fetch origin
git log origin/feature/uitk --oneline | grep -iE "s4-alpha|s4-machine"
ls moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Registry moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Fluid
```
Expected: alpha と S4-machine のマージコミットがあり、`Registry/` に `ITrainInventoryBodyFactory`、`Fluid/` に `FluidInventoryContents` がある。無ければ `cmux wait-for uitk-s4-alpha` / `cmux wait-for uitk-s4-machine` を待つ（このレーンの実装はその後）

- [ ] **Step 2: 計画から写した名前を実物に合わせる**

Global Constraints のコードブロックの alpha・S4-machine の型（`ITrainInventoryPart`・`IBlockInventoryPart`・`BlockInventoryBodyMount`・`OpenTrainInventoryPanel`・`FluidInventoryContents(testid, content)`・`IBlockFluidSlotsModel`・`BlockFluidSlotConversion.FromTanks`）を実物と突き合わせ、違うものを表にして `done.md`「名前の合わせ」に書く。以降のタスクのコードはその表で綴りだけ置き換える。形が違う（工場が Presenter を返さない・液体の Model が列車で空でない等）なら、置き換えの方針を D-n として判断記録に足す

- [ ] **Step 3: 実物の綴りを確かめる**

```bash
cd moorestech_client/Assets/Scripts
grep -rn "OnTimetableUpdated\|bool TryGet" Client.Game/InGame/Train | head
grep -rn "GetBlocksByBlockType" Client.Game | head -3
grep -rn "public TrainTimetableSnapshot(\|public TrainUnitInstanceId(" ../../../moorestech_server/Assets/Scripts/Game.Train | head
grep -rn "EmptyFluidId" ../../../moorestech_server/Assets/Scripts/Core.Master | head -2
ls Client.Starter/Registration/GameUi
```
Expected: 戻りの型（`IObservable<TrainUnitInstanceId>` か `IObservable<Unit>` か・`IEnumerable` か `List` か）、`TrainTimetableSnapshot` と `TrainUnitInstanceId` のコンストラクタ（テストで組めるか）、`FluidId` の作り方と空の液体の比べ方、登録ディレクトリの再編の有無。下のコードと違うところは綴りだけ合わせ、`done.md`「名前の合わせ」に足す

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
- Modify: `Localization/localization.csv`（`ui.commandRejected.*` の末尾に 1 行）
- 再生成: `pnpm --dir moorestech_web/webui run gen:i18n` の生成物
- Modify: `docs/uitk/TODO.md`「追従記録」に 1 行
- Create: `S/Client.Ui.State/Train/TrainCommandRejection.cs`

**Interfaces:**
- Produces: `UiCommandRejectReason.TrainEditRejected`、`TrainCommandRejection.Reject(string logTag, string reason)`

- [ ] **Step 1: 列挙と switch に 1 行ずつ足す**

`UiCommandRejectReason` の末尾に `TrainEditRejected,`。`IsBenign` に `UiCommandRejectReason.TrainEditRejected => false,`、`ToLocalizationKey` に `UiCommandRejectReason.TrainEditRejected => LocalizationKeys.Ui.CommandRejected.TrainEditRejected,`、`ToWireCode` に `UiCommandRejectReason.TrainEditRejected => "train_edit_rejected",`。`Detail` に Web と同じ理由文字列（`replace_failed:<理由>` 等）を入れるので、`UiCommandRejected.ToWireCode` の Detail 優先によりトースト係のログは Web と同じ文字列になる

- [ ] **Step 2: 文言を足して再生成する**

`Localization/localization.csv` の `ui.commandRejected.*` の最後の行の下に:

```csv
ui.commandRejected.trainEditRejected,The train setting change was rejected,The train setting change was rejected,列車の設定変更が受け付けられませんでした,Die Änderung der Zugeinstellung wurde abgelehnt,열차 설정 변경이 거부되었습니다
```

```bash
pnpm --dir moorestech_web/webui run gen:i18n
```
Expected: `LocalizationKeys.Ui.CommandRejected.TrainEditRejected` が引ける。Unity が csv の変化を拾わなければ前例どおり `_CompileRequester.cs` を触ってから compile。`docs/uitk/TODO.md`「追従記録」に `| 4 | <このコミット> | 写した | localization.csv ui.commandRejected.trainEditRejected（S4-train。UITK の拒否トースト用。Web は使わない） |`

- [ ] **Step 3: asmdef に参照を足す**

`Client.Ui.State.asmdef` の `references` に `Game.Block` と `Core.Update` を足す（書式は同ファイルの既存の `Game.Train` 等の行に合わせる。GUID 参照なら各 asmdef の `.meta` の guid を読んで書く）

- [ ] **Step 4: 拒否の組み立て**

```csharp
using Client.Ui.State.Commands;
using UnityEngine;

namespace Client.Ui.State.Train
{
    /// <summary>
    /// 列車系コマンドの拒否を Web ホストと同じ理由文字列で作り、開発者ログへ出す
    /// Builds train command refusals with the Web host's reason strings and logs them for developers
    /// </summary>
    public static class TrainCommandRejection
    {
        public static UiCommandRejected Reject(string logTag, string reason)
        {
            // Web ホストの TrainTimetableActionSupport.Reject と同じ書式
            // Same format as the Web host's TrainTimetableActionSupport.Reject
            Debug.LogWarning($"[{logTag}] rejected: {reason}");
            return new UiCommandRejected(UiCommandRejectReason.TrainEditRejected, reason);
        }
    }
}
```

- [ ] **Step 5: compile とコミット**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `ErrorCount: 0`

```bash
git add Localization/localization.csv moorestech_web/webui moorestech_client/Assets/Scripts/Client.Ui.State docs/uitk/TODO.md
git commit -m "feat(uitk): 列車の設定変更の拒否理由と文言・asmdef 参照を足す（S4-train）

<実行ホストの指示する帰属行>"
```

---

### Task 3: 時刻表の状態（`Client.Ui.State/Train/Timetable/`）

**Files:**
- Create: `S/Client.Ui.State/Train/Timetable/` の 6 本
- Create: `S/Client.Tests/Ui/State/Train/Timetable/TrainTimetableStationCatalogTest.cs`・`TrainTimetableReadyDataFactoryTest.cs`、`S/Client.Tests/Ui/State/Train/Fakes/TrainTestData.cs`

**Interfaces:**
- Consumes: `TrainTimetableFetcher`・`OpenTrainTimetableTracker`・`OpenTrainUnitResolver`（既存）、`TrainTimetableStation`・`TimetableEditStop`（L0-s4b）
- Produces: `TrainTimetableViewState`（`Loading`・`Unavailable`・`Ready(data)`）、`ReadyTrainTimetableViewState.Data`、`TrainTimetableReadyData`、`ITrainTimetableSource.Current`、`TrainTimetableModel`

- [ ] **Step 1: 失敗するテストを書く**

`TrainTestData.cs`:

```csharp
using System.Collections.Generic;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.Timetable;
using Game.Train.RailGraph;
using Game.Train.Unit;
using UnityEngine;

namespace Client.Tests.Ui.State.Train.Fakes
{
    /// <summary>
    /// Web のモック trainWithTimetable と同じ駅・停車駅を組む
    /// Builds the same stations and stops as the Web mock trainWithTimetable
    /// </summary>
    public static class TrainTestData
    {
        public static readonly TrainUnitInstanceId Unit1 = new(1);
        public static readonly TrainUnitInstanceId Unit2 = new(2);
        public static readonly TrainTimetableStation Unnamed = new(new Vector3Int(-8, 0, 3), string.Empty);
        public static readonly TrainTimetableStation North = new(new Vector3Int(12, 0, 40), "北駅");

        public static TimetableEditStop Stop(TrainTimetableStation station, StationNodeSide side)
        {
            return new TimetableEditStop(station.Position, station.Name, side);
        }

        public static TrainTimetableReadyData Ready(TrainUnitInstanceId unit, bool isAutoRun, int currentIndex, params TimetableEditStop[] stops)
        {
            return new TrainTimetableReadyData(unit, isAutoRun, currentIndex, stops, new List<TrainTimetableStation> { Unnamed, North });
        }

        public static TrainTimetableReadyData NorthOnly(TrainUnitInstanceId unit)
        {
            return Ready(unit, false, 0, Stop(North, StationNodeSide.Back));
        }
    }
}
```
（`TrainUnitInstanceId` の作り方は Task 1 Step 3 の綴りに合わせる）

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
            var stations = new List<TrainTimetableStation>
            {
                new(new Vector3Int(1, 5, 0), "b"),
                new(new Vector3Int(0, 9, 9), "a"),
                new(new Vector3Int(1, 0, 0), "c"),
                new(new Vector3Int(1, 9, -1), "d"),
            };
            TrainTimetableStationCatalog.Sort(stations);
            CollectionAssert.AreEqual(new[] { "a", "d", "c", "b" }, stations.ConvertAll(s => s.Name));
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
    }
}
```

`TrainTimetableStationCatalog.cs`:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Ui.State.Train.Logic;
using Game.Block.Blocks.TrainRail;

namespace Client.Ui.State.Train.Timetable
{
    /// <summary>
    /// 駅ブロックから時刻表の駅一覧を組む（Web ホスト TrainTimetableDtoBuilder の写し。D-2）
    /// Builds the timetable station list from station blocks (copy of the Web host TrainTimetableDtoBuilder; D-2)
    /// </summary>
    public static class TrainTimetableStationCatalog
    {
        public static List<TrainTimetableStation> Build(IEnumerable<BlockGameObject> stationBlocks)
        {
            var stations = new List<TrainTimetableStation>();
            foreach (var block in stationBlocks)
            {
                // 駅名はサーバーの StateDetail。届く前は空名で並べる（Web ホストと同じ）
                // The name is a server StateDetail; before it arrives the station lists with an empty name (same as the Web host)
                var name = block.GetStateDetail<TrainStationNameStateDetail>(TrainStationNameStateDetail.BlockStateDetailKey)?.StationName;
                stations.Add(new TrainTimetableStation(block.BlockPosInfo.OriginalPos, name ?? string.Empty));
            }
            Sort(stations);
            return stations;
        }

        // x → z → y の順。Web ホスト SortStations と同じ
        // Order by x, then z, then y; same as the Web host SortStations
        public static void Sort(List<TrainTimetableStation> stations)
        {
            stations.Sort((a, b) =>
            {
                var byX = a.Position.x.CompareTo(b.Position.x);
                if (byX != 0) return byX;
                var byZ = a.Position.z.CompareTo(b.Position.z);
                return byZ != 0 ? byZ : a.Position.y.CompareTo(b.Position.y);
            });
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
    /// 時刻表のスナップショットと駅一覧から用意済みの時刻表を組む（Web ホスト CreateFromTimetable の写し。D-2）
    /// Builds the ready timetable from a snapshot and the station list (copy of the Web host CreateFromTimetable; D-2)
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
using Client.Game.InGame.Block;
using Client.Game.InGame.Train.Timetable;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
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
        private readonly TrainUnitClientCache _cache;
        private readonly IClientTrainTimetableLookup _timetables;
        private readonly TrainTimetableFetcher _fetcher;
        private readonly BlockGameObjectDataStore _blocks;
        private readonly OpenTrainTimetableTracker _tracker;
        private readonly TrainTimetableReadyDataFactory _readyDataFactory = new();
        private readonly ReactiveProperty<TrainTimetableViewState> _current = new(TrainTimetableViewState.Unavailable);

        public IReadOnlyReactiveProperty<TrainTimetableViewState> Current => _current;

        public TrainTimetableModel(SubInventoryState subInventoryState, TrainUnitClientCache cache, IClientTrainTimetableLookup timetables, TrainTimetableFetcher fetcher, BlockGameObjectDataStore blocks, OpenTrainTimetableTracker tracker)
        {
            _subInventoryState = subInventoryState;
            _cache = cache;
            _timetables = timetables;
            _fetcher = fetcher;
            _blocks = blocks;
            _tracker = tracker;
        }

        public void Initialize()
        {
            // Web ホストの再公開の契機と同じ: 開閉・時刻表の到着・トラッカーの再公開要求
            // Same republish triggers as the Web host: open/close, timetable arrival, tracker republish requests
            _subInventoryState.OnSubInventoryUpdated.Subscribe(_ => Rebuild());
            _timetables.OnTimetableUpdated.Subscribe(_ => Rebuild());
            _tracker.OnRepublishRequested.Subscribe(_ => Rebuild());
            Rebuild();
        }

        private void Rebuild()
        {
            _current.Value = _subInventoryState.CurrentSubInventorySource is TrainSubInventorySource source
                ? Build(source.TrainCarInstanceId)
                : TrainTimetableViewState.Unavailable;
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

            var stations = TrainTimetableStationCatalog.Build(_blocks.GetBlocksByBlockType(BlockTypeConst.TrainStation));
            return _readyDataFactory.TryCreate(timetable, stations, out var data)
                ? TrainTimetableViewState.Ready(data)
                : TrainTimetableViewState.Unavailable;
        }
    }
}
```
（`Loading`・`Unavailable` は同じインスタンスなので `ReactiveProperty` は同じ状態の連続を流さない。Ready は毎回新しいので流れる。D-6）

- [ ] **Step 3: テストを回す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainTimetableStationCatalogTest|TrainTimetableReadyDataFactoryTest"`
Expected: `ErrorCount: 0`、3 件 PASS

- [ ] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Train/Timetable moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Train
git commit -m "feat(uitk): 時刻表の状態の Model を足す（S4-train）

<実行ホストの指示する帰属行>"
```

---

### Task 4: 即時編集とコマンド（`Client.Ui.State/Train/TimetableEdit/`）

**Files:**
- Create: `S/Client.Ui.State/Train/TimetableEdit/` の 7 本
- Create: `S/Client.Tests/Ui/State/Train/TimetableEdit/TrainTimetableEditSessionTest.cs`・`TrainTimetableDepartureTest.cs`、`S/Client.Tests/Ui/State/Train/Fakes/FakeTrainTimetableCommands.cs`・`ManualTrainTimetableWaitTimer.cs`

**Interfaces:**
- Consumes: `TimetableEditLogic`（L0-s4b）、`UiCommandResultToaster`、`TrainCommandRejection`（Task 2）、`TrainScheduleEditProtocol.TrainScheduleEditRequest`
- Produces:
```csharp
namespace Client.Ui.State.Train.TimetableEdit
public interface ITrainTimetableCommands { void RequestOpen(); UniTask<UiCommandResult> Replace(IReadOnlyList<TimetableReplaceStop> stops); UniTask<UiCommandResult> SetAutoRun(bool enabled); }
public interface ITrainTimetableWaitTimer { IObservable<Unit> After(TimeSpan delay); }
public sealed class TrainTimetableEditSession
{
    public const string ReplaceCommandName = "train_timetable.replace";
    public static readonly TimeSpan PublicationTimeout;   // 5000ms（Web DEFAULT_ACTION_TIMEOUT_MS）
    public TrainTimetableEditSession(ITrainTimetableCommands commands, ITrainTimetableWaitTimer timer, UiCommandResultToaster toaster, IReadOnlyList<TimetableEditStop> serverStops);
    public IReadOnlyReactiveProperty<IReadOnlyList<TimetableEditStop>> DisplayedStops { get; }
    public IReadOnlyReactiveProperty<bool> IsPending { get; }
    public void ReceiveServerStops(IReadOnlyList<TimetableEditStop> serverStops);
    public void Replace(IReadOnlyList<TimetableEditStop> nextStops);
    public void Close();
}
public sealed class TrainTimetableSectionViewModel { public static TrainTimetableSectionViewModel Build(TrainTimetableReadyData server, IReadOnlyList<TimetableEditStop> displayedStops, bool isPending); }
```

- [ ] **Step 1: 偽物と失敗するテストを書く**

`FakeTrainTimetableCommands.cs`:

```csharp
using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.TimetableEdit;
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

        public void Reject(string reason)
        {
            _pending.Dequeue().TrySetResult(new UiCommandRejected(UiCommandRejectReason.TrainEditRejected, reason));
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

`TrainTimetableEditSessionTest.cs`（ScreenTestHost は使わない。トースト係は `GameUiToastFeed` から組む）:

```csharp
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.State.Toast;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.TimetableEdit;
using Game.Train.RailGraph;
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
            _server = new[] { Stop(North, StationNodeSide.Back) };
            _session = new TrainTimetableEditSession(_commands, _timer, new UiCommandResultToaster(new GameUiToastFeed()), _server);
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableImmediate.test.ts">immediately sends each add, move and remove without an Apply button</web-test>
        [Test]
        public void 追加移動削除を適用ボタンなしで即送る()
        {
            // 追加は即送信され、表示も即替わる
            // An add is sent at once and shows at once
            var added = TimetableEditLogic.AddStop(_server, Unnamed);
            _session.Replace(added);
            Assert.AreEqual(1, _commands.SentReplaces.Count);
            Assert.AreEqual(2, _session.DisplayedStops.Value.Count);
            Assert.IsTrue(_session.IsPending.Value);

            // 受理と公開で保留が解け、続けて移動・削除も 1 回ずつ送る
            // Acceptance plus publication releases it; move and remove then send once each
            _commands.Accept();
            _session.ReceiveServerStops(added);
            Assert.IsFalse(_session.IsPending.Value);
            var moved = TimetableEditLogic.MoveStop(added, 1, StopMoveDirection.Up);
            _session.Replace(moved);
            _commands.Accept();
            _session.ReceiveServerStops(moved);
            _session.Replace(TimetableEditLogic.RemoveStop(moved, 0));
            Assert.AreEqual(3, _commands.SentReplaces.Count);
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableImmediate.test.ts">waits for publication after acceptance so the next edit cannot use stale stops</web-test>
        [Test]
        public void 受理後も公開が届くまで次の編集を受けない()
        {
            var added = TimetableEditLogic.AddStop(_server, Unnamed);
            _session.Replace(added);
            _commands.Accept();
            Assert.IsTrue(_session.IsPending.Value);
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
            var added = TimetableEditLogic.AddStop(_server, Unnamed);
            _session.Replace(added);
            _session.ReceiveServerStops(added);
            Assert.IsTrue(_session.IsPending.Value);

            // 受理の時点でサーバーが同じ並びなら時間切れを待たずに解く
            // On acceptance with a matching server order, release without waiting for the timeout
            _commands.Accept();
            Assert.IsFalse(_session.IsPending.Value);
            Assert.IsEmpty(_timer.RequestedDelays);
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableImmediate.test.ts">reverts rejected edits to the newest authority and allows another edit</web-test>
        [Test]
        public void 拒否は最新のサーバーの並びへ戻し次の編集を受ける()
        {
            _session.Replace(TimetableEditLogic.AddStop(_server, Unnamed));
            var remote = new[] { Stop(North, StationNodeSide.Front) };
            _session.ReceiveServerStops(remote);

            // 拒否で保留を解き、表示は届いた最新の並び
            // Rejection releases the pending edit and shows the newest published order
            LogAssert.Expect(LogType.Warning, new Regex(@"\[UiCommandResultToaster\] train_timetable.replace failed: replace_failed:busy"));
            _commands.Reject("replace_failed:busy");
            Assert.IsFalse(_session.IsPending.Value);
            Assert.AreEqual(StationNodeSide.Front, _session.DisplayedStops.Value[0].Side);
            _session.Replace(TimetableEditLogic.RemoveStop(remote, 0));
            Assert.AreEqual(2, _commands.SentReplaces.Count);
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableImmediate.test.ts">releases a missing publication after the existing transport timeout</web-test>
        [Test]
        public void 公開が来なければ時間切れで解く()
        {
            _session.Replace(TimetableEditLogic.AddStop(_server, Unnamed));
            _commands.Accept();
            LogAssert.Expect(LogType.Warning, new Regex(@"\[TrainTimetableEdit\] publication did not arrive"));
            _timer.Elapse();
            Assert.IsFalse(_session.IsPending.Value);
            Assert.AreEqual(1, _session.DisplayedStops.Value.Count);
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableImmediate.test.ts">does not defer an edit or refresh a different train after closing</web-test>
        [Test]
        public void 閉じた後に戻った結果は表示を動かさない()
        {
            _session.Replace(TimetableEditLogic.AddStop(_server, Unnamed));
            var displayedAtClose = _session.DisplayedStops.Value;
            _session.Close();

            // 閉じた後の受理は捨て、時間切れも仕掛けない
            // Acceptance after close is discarded and no timeout is armed
            LogAssert.Expect(LogType.Warning, new Regex(@"\[TrainTimetableEdit\] result after close discarded"));
            _commands.Accept();
            Assert.AreSame(displayedAtClose, _session.DisplayedStops.Value);
            Assert.IsEmpty(_timer.RequestedDelays);
        }
    }
}
```

`TrainTimetableDepartureTest.cs`:

```csharp
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.TimetableEdit;
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

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainTimetableEditSessionTest|TrainTimetableDepartureTest"`
Expected: コンパイルエラー（型が無い）

- [ ] **Step 2: 口とタイマーと出発条件を書く**

`ITrainTimetableCommands.cs`:

```csharp
using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Train.Logic;
using Cysharp.Threading.Tasks;

namespace Client.Ui.State.Train.TimetableEdit
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

`TrainTimetableDeparture.cs`:

```csharp
using System.Collections.Generic;
using Client.Ui.State.Train.Logic;
using Core.Update;
using Game.Train.Diagram;
using Game.Train.Unit;

namespace Client.Ui.State.Train.TimetableEdit
{
    /// <summary>
    /// UI が送る停車駅に固定の出発条件を付ける（Web ホスト TrainTimetableStopParser の写し。D-2）
    /// Attaches the fixed departure condition to stops sent from the UI (copy of the Web host TrainTimetableStopParser; D-2)
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

`TrainTimetableCommands.cs`:

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

namespace Client.Ui.State.Train.TimetableEdit
{
    /// <summary>
    /// 時刻表の取得依頼・置換・自動運転をサーバーへ送る（Web ホスト TrainTimetableActions の写し。D-2）
    /// Sends timetable fetch requests, replaces and auto-run toggles to the server (copy of the Web host TrainTimetableActions; D-2)
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
            // 列車が引けなければ「列車なしで開いた」と記録する（Web ホスト TrainTimetableOpenActionHandler と同じ）
            // Without a resolvable train, record the tab as opened without one (same as the Web host TrainTimetableOpenActionHandler)
            if (OpenTrainUnitResolver.TryResolveOpenTrain(_subInventoryState, _cache, out var trainUnitId)) _fetcher.RequestForOpenedTab(trainUnitId);
            else _fetcher.MarkTabOpenedWithoutTrain();
        }

        public async UniTask<UiCommandResult> Replace(IReadOnlyList<TimetableReplaceStop> stops)
        {
            if (!OpenTrainUnitResolver.TryResolveOpenTrain(_subInventoryState, _cache, out var trainUnitId)) return TrainCommandRejection.Reject(LogTag, "train_not_open");

            // 応答はネットワーク由来なので null（切断・時間切れ）を拒否として扱う
            // The response comes from the network, so null (disconnect or timeout) counts as a refusal
            var request = TrainScheduleEditProtocol.TrainScheduleEditRequest.CreateReplaceTimetableRequest(trainUnitId, TrainTimetableDeparture.ToServerStops(stops));
            var response = await ClientContext.VanillaApi.Response.Train.SendTrainScheduleEdit(request, CancellationToken.None);
            if (response == null || !response.Success) return TrainCommandRejection.Reject(LogTag, $"replace_failed:{response?.FailureReason}");
            return UiCommandAccepted.Instance;
        }

        public async UniTask<UiCommandResult> SetAutoRun(bool enabled)
        {
            if (!OpenTrainUnitResolver.TryResolveOpenTrain(_subInventoryState, _cache, out var trainUnitId)) return TrainCommandRejection.Reject(LogTag, "train_not_open");

            // 受理されても求めた値で適用されていなければ拒否（Web ホストと同じ照合）
            // Even when accepted, refuse if the applied value differs from the request (same check as the Web host)
            var request = TrainScheduleEditProtocol.TrainScheduleEditRequest.CreateSetAutoRunRequest(trainUnitId, enabled);
            var response = await ClientContext.VanillaApi.Response.Train.SendTrainScheduleEdit(request, CancellationToken.None);
            if (response == null || !response.Success) return TrainCommandRejection.Reject(LogTag, $"set_auto_run_failed:{response?.FailureReason}");
            if (response.AppliedIsAutoRun != enabled) return TrainCommandRejection.Reject(LogTag, $"set_auto_run_not_applied:{response.AppliedIsAutoRun}");
            return UiCommandAccepted.Instance;
        }
    }
}
```

- [ ] **Step 4: 編集セッションを書く**

`TrainTimetableEditSession.cs`:

```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Toast;
using Client.Ui.State.Train.Logic;
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
        private readonly ReactiveProperty<IReadOnlyList<TimetableEditStop>> _displayedStops;
        private readonly ReactiveProperty<bool> _isPending = new(false);
        private IReadOnlyList<TimetableEditStop> _serverStops;
        private IReadOnlyList<TimetableEditStop> _pendingStops;
        private bool _accepted;
        private bool _closed;
        private IDisposable _publicationTimeout;

        public IReadOnlyReactiveProperty<IReadOnlyList<TimetableEditStop>> DisplayedStops => _displayedStops;
        public IReadOnlyReactiveProperty<bool> IsPending => _isPending;

        public TrainTimetableEditSession(ITrainTimetableCommands commands, ITrainTimetableWaitTimer timer, UiCommandResultToaster toaster, IReadOnlyList<TimetableEditStop> serverStops)
        {
            _commands = commands;
            _timer = timer;
            _toaster = toaster;
            _serverStops = serverStops;
            _displayedStops = new ReactiveProperty<IReadOnlyList<TimetableEditStop>>(serverStops);
        }

        public void ReceiveServerStops(IReadOnlyList<TimetableEditStop> serverStops)
        {
            // 受理済みの保留は同じ並びの公開が届いた時点で解く
            // A pending edit that was accepted is released once a matching publication arrives
            _serverStops = serverStops;
            if (_pendingStops != null && _accepted && TimetableEditLogic.SameStopOrder(_pendingStops, serverStops)) ClearPending();
            Publish();
        }

        public void Replace(IReadOnlyList<TimetableEditStop> nextStops)
        {
            // 前の編集が終わるまでは古い並びから組んだ編集を受けない
            // Until the previous edit settles, refuse edits built from a stale order
            if (_pendingStops != null)
            {
                Debug.LogWarning("[TrainTimetableEdit] ignored: previous edit is still pending");
                return;
            }
            // silent-ok: 並びが変わらない編集は送る意味が無い / 表示も変わらないので操作した人が見て分かる（Web と同じ）
            if (TimetableEditLogic.SameStopOrder(nextStops, _serverStops)) return;

            _pendingStops = nextStops;
            _accepted = false;
            Publish();
            SendAsync(nextStops).Forget();
        }

        public void Close()
        {
            _closed = true;
            _publicationTimeout?.Dispose();
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
            if (result is UiCommandRejected)
            {
                _toaster.Report(ReplaceCommandName, result);
                ClearPending();
                Publish();
                return;
            }

            // 受理。公開が先に届いていれば解き、まだなら時間切れを仕掛けて待つ
            // Accepted: release if the publication already arrived, otherwise arm the timeout and wait
            if (TimetableEditLogic.SameStopOrder(stops, _serverStops))
            {
                ClearPending();
                Publish();
                return;
            }
            _accepted = true;
            _publicationTimeout = _timer.After(PublicationTimeout).Subscribe(_ => ReleaseMissingPublication());
        }

        private void ReleaseMissingPublication()
        {
            Debug.LogWarning($"[TrainTimetableEdit] publication did not arrive within {PublicationTimeout.TotalMilliseconds}ms; showing the server order");
            ClearPending();
            Publish();
        }

        private void ClearPending()
        {
            _pendingStops = null;
            _accepted = false;
            _publicationTimeout?.Dispose();
            _publicationTimeout = null;
        }

        private void Publish()
        {
            _displayedStops.Value = _pendingStops ?? _serverStops;
            _isPending.Value = _pendingStops != null;
        }
    }
}
```

- [ ] **Step 5: 区画の描画値を書く**

`TrainTimetableSectionViewModel.cs`:

```csharp
using System.Collections.Generic;
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
        public const int NoCurrentIndex = -1;

        public IReadOnlyList<TimetableEditStop> Stops { get; }
        public int CurrentIndex { get; }
        public IReadOnlyList<TrainTimetableStation> Stations { get; }
        public bool IsPending { get; }
        public bool IsAutoRun { get; }
        public bool CanEnableAutoRun { get; }

        private TrainTimetableSectionViewModel(IReadOnlyList<TimetableEditStop> stops, int currentIndex, IReadOnlyList<TrainTimetableStation> stations, bool isPending, bool isAutoRun, bool canEnableAutoRun)
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
            var currentIndex = TimetableEditLogic.SameStopOrder(displayedStops, server.Stops) ? server.CurrentIndex : NoCurrentIndex;
            return new TrainTimetableSectionViewModel(displayedStops, currentIndex, server.Stations, isPending, server.IsAutoRun, TimetableEditLogic.CanEnableAutoRun(server.Stops));
        }
    }
}
```

- [ ] **Step 6: テストを回してコミット**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainTimetableEditSessionTest|TrainTimetableDepartureTest"`
Expected: `ErrorCount: 0`、7 件 PASS。`UniTaskCompletionSource.TrySetResult` の継続が同期で走らずに `Accept()` 直後の主張が落ちるなら、テスト側で `await UniTask.Yield()` を挟む `[UnityTest]` に替える（本体のコードは替えない）

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Train/TimetableEdit moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Train
git commit -m "feat(uitk): 時刻表の即時編集とコマンドを足す（S4-train）

<実行ホストの指示する帰属行>"
```

---

### Task 5: 貨車とプラットフォームの Model（`Client.Ui.State/Train/Inventory/`・`Platform/`）

**Files:**
- Create: `S/Client.Ui.State/Train/Inventory/` の 6 本、`S/Client.Ui.State/Train/Platform/` の 8 本
- Create: `S/Client.Tests/Ui/State/Train/Inventory/TrainTimetableFetchTriggerTest.cs`、`S/Client.Tests/Ui/State/Train/Platform/TrainStationNameDraftTest.cs`、`S/Client.Tests/Ui/State/Train/Fakes/FakeTrainBlockCommands.cs`・`FakeTrainSources.cs`

**Interfaces:**
- Consumes: `SubInventoryState`・`OpenBlockDetailTracker`、`BlockFluidSlotState`（L0-s4b）、`IBlockFluidSlotsModel.Current`（`BlockFluidSlotsViewModel.Slots`）と `BlockFluidSlotConversion.FromTanks`（S4-machine の Task 3。`Client.Ui.State/BlockInventory/Fluid/`）
- Produces:
```csharp
namespace Client.Ui.State.Train.Inventory
public enum TrainInventoryTab { Inventory, Timetable }
public sealed class TrainTimetableFetchTrigger { public TrainInventoryTab Tab { get; } public bool SelectTab(TrainInventoryTab tab); public bool ReceiveState(TrainTimetableViewState state); }
public sealed class TrainInventoryBodyViewModel { public static readonly TrainInventoryBodyViewModel Closed; public long TrainCarInstanceId { get; } public IReadOnlyList<SlotItem> ItemSlots { get; } public IReadOnlyList<BlockFluidSlotState> Fluids { get; } }
public interface ITrainInventoryBodySource { IReadOnlyReactiveProperty<TrainInventoryBodyViewModel> Current { get; } }
namespace Client.Ui.State.Train.Platform
public enum TrainPlatformTransferMode { LoadToTrain, UnloadToPlatform }
public sealed class TrainPlatformBodyViewModel { Closed; IsStation; StationName; HasItemSlots; ItemSlots; Fluids; HasTransferMode; Mode; }
public interface ITrainPlatformBodySource { IReadOnlyReactiveProperty<TrainPlatformBodyViewModel> Current { get; } }
public interface ITrainBlockCommands { UniTask<UiCommandResult> SetStationName(string name); UniTask<UiCommandResult> SetTransferMode(TrainPlatformTransferMode mode); }
public sealed class TrainStationNameDraft { string Input; void Sync(string syncedName); void Edit(string input); void Reject(); }
```

- [ ] **Step 1: 失敗するテストを書く**

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
using Client.Ui.State.Train.Platform;
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

`FakeTrainBlockCommands.cs`:

```csharp
using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Train.Platform;
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

        public void Reject(string reason)
        {
            _pending.Dequeue().TrySetResult(new UiCommandRejected(UiCommandRejectReason.TrainEditRejected, reason));
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

`FakeTrainSources.cs`:

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
        private readonly ReactiveProperty<TrainPlatformBodyViewModel> _platform = new(TrainPlatformBodyViewModel.Closed);

        IReadOnlyReactiveProperty<TrainTimetableViewState> ITrainTimetableSource.Current => _timetable;
        IReadOnlyReactiveProperty<TrainInventoryBodyViewModel> ITrainInventoryBodySource.Current => _body;
        IReadOnlyReactiveProperty<TrainPlatformBodyViewModel> ITrainPlatformBodySource.Current => _platform;

        public void SetTimetable(TrainTimetableViewState state)
        {
            _timetable.Value = state;
        }

        public void SetBody(TrainInventoryBodyViewModel body)
        {
            _body.Value = body;
        }

        public void SetPlatform(TrainPlatformBodyViewModel platform)
        {
            _platform.Value = platform;
        }
    }
}
```

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainTimetableFetchTriggerTest|TrainStationNameDraftTest"`
Expected: コンパイルエラー（型が無い）

- [ ] **Step 2: 貨車側を書く**

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
    /// 時刻表の取得を C# 側へ頼む契機を決める（Web TrainInventoryBody の effect の写し）
    /// Decides when to ask for a timetable fetch (copy of the Web TrainInventoryBody effects)
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
        public static readonly TrainInventoryBodyViewModel Closed = new(-1, new List<SlotItem>(), new List<BlockFluidSlotState>());

        public long TrainCarInstanceId { get; }
        public IReadOnlyList<SlotItem> ItemSlots { get; }
        public IReadOnlyList<BlockFluidSlotState> Fluids { get; }

        public TrainInventoryBodyViewModel(long trainCarInstanceId, IReadOnlyList<SlotItem> itemSlots, IReadOnlyList<BlockFluidSlotState> fluids)
        {
            TrainCarInstanceId = trainCarInstanceId;
            ItemSlots = itemSlots;
            Fluids = fluids;
        }
    }
}
```
（`SlotItem` の名前空間は alpha の Produces の綴りに合わせる）

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

`TrainCarContentsConversion.cs`:

```csharp
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Fluid;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.Items;
using Core.Item.Interface;
using Game.Block.Interface.State;

namespace Client.Ui.State.Train.Inventory
{
    /// <summary>
    /// 貨車のサブインベントリを描画値へ写す（Web ホスト TrainInventoryDtoFactory の写し。D-2）
    /// Maps the car sub-inventory to draw values (copy of the Web host TrainInventoryDtoFactory; D-2)
    /// </summary>
    public static class TrainCarContentsConversion
    {
        public static IReadOnlyList<SlotItem> ToSlots(IReadOnlyList<IItemStack> stacks)
        {
            var slots = new List<SlotItem>(stacks.Count);
            foreach (var stack in stacks) slots.Add(new SlotItem(stack.Id, stack.Count));
            return slots;
        }

        // 貨車の液体は S4-machine のタンクの写しを入力側だけで使う（空の液体 ID は空きタンク。写しを 2 か所に持たない）
        // Car fluids reuse the S4-machine tank conversion with inputs only (empty id is an empty tank; no second copy of the mapping)
        public static IReadOnlyList<BlockFluidSlotState> ToFluids(IReadOnlyList<FluidMessagePack> fluids)
        {
            return BlockFluidSlotConversion.FromTanks(fluids, new List<FluidMessagePack>());
        }
    }
}
```


`TrainInventoryBodyModel.cs`:

```csharp
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Train.Inventory
{
    /// <summary>
    /// 開いている貨車の中身を SubInventoryState から配る
    /// Publishes the open car's contents from SubInventoryState
    /// </summary>
    public sealed class TrainInventoryBodyModel : ITrainInventoryBodySource, IInitializable
    {
        private readonly SubInventoryState _subInventoryState;
        private readonly ReactiveProperty<TrainInventoryBodyViewModel> _current = new(TrainInventoryBodyViewModel.Closed);

        public IReadOnlyReactiveProperty<TrainInventoryBodyViewModel> Current => _current;

        public TrainInventoryBodyModel(SubInventoryState subInventoryState)
        {
            _subInventoryState = subInventoryState;
        }

        public void Initialize()
        {
            _subInventoryState.OnSubInventoryUpdated.Subscribe(_ => Rebuild());
            Rebuild();
        }

        private void Rebuild()
        {
            // 列車以外が開いているあいだは閉じた値（本文は alpha が出さない）
            // While something other than a train is open, publish the closed value (alpha shows no body)
            var model = _subInventoryState.CurrentSubInventory;
            if (_subInventoryState.CurrentSubInventorySource is not TrainSubInventorySource source || model == null)
            {
                _current.Value = TrainInventoryBodyViewModel.Closed;
                return;
            }
            _current.Value = new TrainInventoryBodyViewModel(source.TrainCarInstanceId, TrainCarContentsConversion.ToSlots(model.SubInventory), TrainCarContentsConversion.ToFluids(model.Fluids));
        }
    }
}
```

- [ ] **Step 3: プラットフォーム側を書く**

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

`TrainPlatformBodyViewModel.cs`:

```csharp
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.Items;

namespace Client.Ui.State.Train.Platform
{
    /// <summary>
    /// 駅・プラットフォームの本文に描く値
    /// Values drawn by the station and platform body
    /// </summary>
    public sealed class TrainPlatformBodyViewModel
    {
        public static readonly TrainPlatformBodyViewModel Closed = new(false, string.Empty, false, new List<SlotItem>(), new List<BlockFluidSlotState>(), false, TrainPlatformTransferMode.LoadToTrain);

        public bool IsStation { get; }
        public string StationName { get; }
        public bool HasItemSlots { get; }
        public IReadOnlyList<SlotItem> ItemSlots { get; }
        public IReadOnlyList<BlockFluidSlotState> Fluids { get; }
        public bool HasTransferMode { get; }
        public TrainPlatformTransferMode Mode { get; }

        public TrainPlatformBodyViewModel(bool isStation, string stationName, bool hasItemSlots, IReadOnlyList<SlotItem> itemSlots, IReadOnlyList<BlockFluidSlotState> fluids, bool hasTransferMode, TrainPlatformTransferMode mode)
        {
            IsStation = isStation;
            StationName = stationName;
            HasItemSlots = hasItemSlots;
            ItemSlots = itemSlots;
            Fluids = fluids;
            HasTransferMode = hasTransferMode;
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
        IReadOnlyReactiveProperty<TrainPlatformBodyViewModel> Current { get; }
    }
}
```

`TrainPlatformDetailReader.cs`:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.Items;
using Game.Block.Blocks.TrainRail;
using Mooresmaster.Model.BlocksModule;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Client.Ui.State.Train.Platform
{
    /// <summary>
    /// 駅・プラットフォームのブロックから描画値を読む（Web ホスト TrainPlatformDetailDtoBuilder・TrainStationDetailDtoBuilder の写し。D-2）
    /// Reads draw values from a station or platform block (copy of the Web host TrainPlatformDetailDtoBuilder and TrainStationDetailDtoBuilder; D-2)
    /// </summary>
    public static class TrainPlatformDetailReader
    {
        public static bool IsTrainPlatformBlock(string blockType)
        {
            return blockType == BlockTypeConst.TrainStation || blockType == BlockTypeConst.TrainItemPlatform || blockType == BlockTypeConst.TrainFluidPlatform;
        }

        public static TrainPlatformBodyViewModel Read(BlockGameObject block, IReadOnlyList<SlotItem> itemSlots, IReadOnlyList<BlockFluidSlotState> fluids)
        {
            var master = block.BlockMasterElement;
            var isStation = master.BlockType == BlockTypeConst.TrainStation;

            // 駅名・移送モードはサーバーの StateDetail。届く前は名前は空・モードの切替は出さない（Web ホストと同じ）
            // Name and mode are server StateDetails; before they arrive the name is empty and no mode switch shows (same as the Web host)
            var stationName = isStation ? block.GetStateDetail<TrainStationNameStateDetail>(TrainStationNameStateDetail.BlockStateDetailKey)?.StationName ?? string.Empty : string.Empty;
            var transfer = block.GetStateDetail<TrainPlatformTransferStateDetail>(TrainPlatformTransferStateDetail.BlockStateDetailKey);
            // スロット数は移送の状態があり、かつ品物の PF・駅のときだけ持つ（Web ホストは状態が無ければ詳細ごと出さない）
            // Item slots exist only with a transfer state on an item platform or station (the Web host omits the detail without a state)
            var hasItemSlots = transfer != null && master.BlockParam is TrainItemPlatformBlockParam or TrainStationBlockParam;
            var mode = transfer != null && transfer.Mode == TrainPlatformTransferComponent.TransferMode.UnloadToPlatform
                ? TrainPlatformTransferMode.UnloadToPlatform
                : TrainPlatformTransferMode.LoadToTrain;

            // 液体の列は S4-machine の液体 Model が追跡ブロックから組んだもの（Web の fluidSlots と同じく全ブロック共通）
            // Fluids are the list the S4-machine fluid model built for the tracked block (shared by all blocks, like Web fluidSlots)
            return new TrainPlatformBodyViewModel(isStation, stationName, hasItemSlots, hasItemSlots ? itemSlots : new List<SlotItem>(), fluids, transfer != null, mode);
        }
    }
}
```
（`BlockTypeConst` の駅・PF の綴りは Task 1 Step 3 の grep で確かめる。`ItemSlotCount` は Web と同じく描画には使わない: 実枠はサブインベントリの数で出る）

`TrainPlatformBodyModel.cs`:

```csharp
using Client.Game.InGame.UI.UIState.State;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Fluid;
using Client.Ui.State.Train.Inventory;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Train.Platform
{
    /// <summary>
    /// 開いている駅・プラットフォームの描画値を、追跡ブロックとサブインベントリから配る
    /// Publishes the open station or platform's draw values from the tracked block and the sub-inventory
    /// </summary>
    public sealed class TrainPlatformBodyModel : ITrainPlatformBodySource, IInitializable
    {
        private readonly SubInventoryState _subInventoryState;
        private readonly OpenBlockDetailTracker _tracker;
        private readonly IBlockFluidSlotsModel _fluidSlots;
        private readonly ReactiveProperty<TrainPlatformBodyViewModel> _current = new(TrainPlatformBodyViewModel.Closed);

        public IReadOnlyReactiveProperty<TrainPlatformBodyViewModel> Current => _current;

        public TrainPlatformBodyModel(SubInventoryState subInventoryState, OpenBlockDetailTracker tracker, IBlockFluidSlotsModel fluidSlots)
        {
            _subInventoryState = subInventoryState;
            _tracker = tracker;
            _fluidSlots = fluidSlots;
        }

        public void Initialize()
        {
            // 追跡替え・追跡ブロックの状態変化・スロット更新で組み直す（alpha のパネルの契機と同じ）
            // Rebuild on tracked-block change, its state change and slot updates (same triggers as alpha's panel)
            _tracker.OnTrackedBlockChanged.Subscribe(_ => Rebuild());
            _tracker.OnTrackedBlockStateChanged.Subscribe(_ => Rebuild());
            _subInventoryState.OnSubInventoryUpdated.Subscribe(_ => Rebuild());
            _fluidSlots.Current.Subscribe(_ => Rebuild());
            Rebuild();
        }

        private void Rebuild()
        {
            var block = _tracker.TrackedBlock;
            var model = _subInventoryState.CurrentSubInventory;
            if (block == null || model == null || !TrainPlatformDetailReader.IsTrainPlatformBlock(block.BlockMasterElement.BlockType))
            {
                _current.Value = TrainPlatformBodyViewModel.Closed;
                return;
            }
            _current.Value = TrainPlatformDetailReader.Read(block, TrainCarContentsConversion.ToSlots(model.SubInventory), _fluidSlots.Current.Value.Slots);
        }
    }
}
```

`ITrainBlockCommands.cs`:

```csharp
using Client.Ui.State.Commands;
using Cysharp.Threading.Tasks;

namespace Client.Ui.State.Train.Platform
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

`TrainBlockCommands.cs`:

```csharp
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Ui.State.Commands;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.TrainRail;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Client.Ui.State.Train.Platform
{
    /// <summary>
    /// 駅名・移送モードをサーバーへ送る（Web ホスト TrainStationActions・TrainPlatformActions の写し。D-2）
    /// Sends station names and transfer modes to the server (copy of the Web host TrainStationActions and TrainPlatformActions; D-2)
    /// </summary>
    public sealed class TrainBlockCommands : ITrainBlockCommands
    {
        public const string SetNameCommandName = "train_station.set_name";
        public const string SetModeCommandName = "train_platform.set_transfer_mode";
        private readonly SubInventoryState _subInventoryState;

        public TrainBlockCommands(SubInventoryState subInventoryState)
        {
            _subInventoryState = subInventoryState;
        }

        public async UniTask<UiCommandResult> SetStationName(string name)
        {
            if (!TryResolveOpenBlock(out var source, out var blockType)) return TrainCommandRejection.Reject("TrainStationAction", "block_not_open");
            if (blockType != BlockTypeConst.TrainStation) return TrainCommandRejection.Reject("TrainStationAction", "invalid_block_type");

            // 応答はネットワーク由来なので null を拒否として扱う
            // The response comes from the network, so null counts as a refusal
            var response = await ClientContext.VanillaApi.Response.Train.SetTrainStationName(source.BlockPosition, name, CancellationToken.None);
            if (response == null || !response.Success) return TrainCommandRejection.Reject("TrainStationAction", $"set_name_failed:{response?.FailureReason}");
            return UiCommandAccepted.Instance;
        }

        public async UniTask<UiCommandResult> SetTransferMode(TrainPlatformTransferMode mode)
        {
            if (!TryResolveOpenBlock(out var source, out var blockType)) return TrainCommandRejection.Reject("TrainPlatformAction", "block_not_open");
            if (!TrainPlatformDetailReader.IsTrainPlatformBlock(blockType)) return TrainCommandRejection.Reject("TrainPlatformAction", "invalid_block_type");

            var serverMode = mode == TrainPlatformTransferMode.UnloadToPlatform ? TrainPlatformTransferComponent.TransferMode.UnloadToPlatform : TrainPlatformTransferComponent.TransferMode.LoadToTrain;
            var response = await ClientContext.VanillaApi.Response.Train.SetTrainPlatformTransferMode(source.BlockPosition, serverMode, CancellationToken.None);
            if (response == null || !response.Success) return TrainCommandRejection.Reject("TrainPlatformAction", $"set_mode_failed:{response?.FailureReason}");
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
（拒否の理由文字列は Web ホスト `TrainPlatformActions.cs` の実物の綴りに合わせる。Task 1 Step 3 で読む）

`TrainStationNameDraft.cs`:

```csharp
namespace Client.Ui.State.Train.Platform
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

- [ ] **Step 4: テストを回してコミット**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainTimetableFetchTriggerTest|TrainStationNameDraftTest"`
Expected: `ErrorCount: 0`、2 件 PASS

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Train moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Train
git commit -m "feat(uitk): 貨車とプラットフォームの Model を足す（S4-train）

<実行ホストの指示する帰属行>"
```

---

### Task 6: 時刻表の View と Presenter（`Screens/BlockInventory/Train/Timetable/`）

**Files:**
- Create: `S/Client.Ui/Screens/BlockInventory/Train/Timetable/` の 6 本（`.cs` 5・`Timetable.uss`）
- Create: `S/Client.Tests/Ui/Screens/BlockInventory/Train/Timetable/TrainTimetableSectionViewTest.cs`

**Interfaces:**
- Consumes: Task 3〜4 の型、`GamePanel`・`FadeRule`・`PanelActionButton`・`ModeSwitch<bool>`・`LocalizedLabel`・`UiStack`
- Produces: `TrainTimetableAreaView`（`ShowLoading`・`ShowUnavailable`・`ReplaceSection`）、`TrainTimetablePresenter`（`Render(TrainTimetableViewState)`・`RerenderLanguage()`・`Close()`）、`TrainTimetableSectionView.BusyClass = "train-timetable-section--busy"`

- [ ] **Step 1: 失敗するテストを書く**

`TrainTimetableSectionViewTest.cs`:

```csharp
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.Screens.BlockInventory.Train.Timetable;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.Timetable;
using Game.Train.RailGraph;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.Train.Fakes.TrainTestData;

namespace Client.Tests.Ui.Screens.BlockInventory.Train.Timetable
{
    public class TrainTimetableSectionViewTest : ScreenTestHost
    {
        private FakeTrainTimetableCommands _commands;
        private TrainTimetableAreaView _area;
        private TrainTimetablePresenter _presenter;

        [SetUp]
        public void MountArea()
        {
            _commands = new FakeTrainTimetableCommands();
            _area = Mount(new TrainTimetableAreaView());
            _presenter = new TrainTimetablePresenter(_area, _commands, new ManualTrainTimetableWaitTimer(), Toaster);
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableImmediate.test.ts">blocks auto-run until an added stop is authoritative and sends toggles immediately</web-test>
        [Test]
        public void 追加が確定するまで自動運転ONを塞ぎ切替は即送る()
        {
            // 停車駅 0 件では ON が無効で、理由の文が出る
            // With no stops, ON is disabled and the reason text shows
            _presenter.Render(TrainTimetableViewState.Ready(Ready(Unit1, false, 0)));
            simulate.FrameUpdate();
            Assert.IsFalse(Find<Button>(_area, "train-timetable-auto-run-on").enabledSelf);
            Assert.IsTrue(IsShown(_area.Q<Label>("train-timetable-auto-run-unavailable")));

            // 追加は楽観表示でも ON を開けない。公開で確定してから開く
            // An optimistic add does not open ON; it opens after the publication confirms it
            simulate.Click(Find<Button>(_area, $"train-timetable-station-12_0_40-add"));
            Assert.IsFalse(Find<Button>(_area, "train-timetable-auto-run-on").enabledSelf);
            _commands.Accept();
            _presenter.Render(TrainTimetableViewState.Ready(NorthOnly(Unit1)));
            simulate.FrameUpdate();
            Assert.IsTrue(Find<Button>(_area, "train-timetable-auto-run-on").enabledSelf);

            // 押すと楽観更新なしで即送る
            // Pressing sends immediately without an optimistic update
            simulate.Click(Find<Button>(_area, "train-timetable-auto-run-on"));
            CollectionAssert.AreEqual(new[] { true }, _commands.SentAutoRuns);
            Assert.IsTrue(IsSelected(Find<Button>(_area, "train-timetable-auto-run-off")));
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableImmediate.test.ts">disables moves past either end and follows remote changes when idle</web-test>
        [Test]
        public void 端を越える移動を塞ぎ待ちが無いときは外の変更に従う()
        {
            _presenter.Render(TrainTimetableViewState.Ready(Ready(Unit1, false, 0, Stop(Unnamed, StationNodeSide.Front), Stop(North, StationNodeSide.Back))));
            simulate.FrameUpdate();
            Assert.IsFalse(Find<Button>(_area, "train-timetable-stop-0-up").enabledSelf);
            Assert.IsTrue(Find<Button>(_area, "train-timetable-stop-0-down").enabledSelf);
            Assert.IsFalse(Find<Button>(_area, "train-timetable-stop-1-down").enabledSelf);
            Assert.IsTrue(_area.Q("train-timetable-stop-0").ClassListContains("train-timetable-stop--current"));

            // 待ちが無ければ別の画面からの変更をそのまま映す
            // When idle, a change from elsewhere shows as is
            _presenter.Render(TrainTimetableViewState.Ready(Ready(Unit1, false, 0, Stop(North, StationNodeSide.Back))));
            simulate.FrameUpdate();
            Assert.IsNull(_area.Q("train-timetable-stop-1"));
            UiTextAssert.Contains("1. 北駅", _area.Q<Label>("train-timetable-stop-0-label"));
        }
    }
}
```
（`Find`・`IsShown`・`IsSelected` は `ScreenTestHost`、`UiTextAssert` は `Client.Tests/Ui/TestDoubles` の実名に合わせる。`simulate.Click` は前例 `BugReportRecoveryControlsViewTest`）

Run: `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.Ui.Screens.BlockInventory.Train.Timetable.TrainTimetableSectionViewTest"`
Expected: コンパイルエラー

- [ ] **Step 2: 意図の値と 2 つの列を書く**

`TimetableStopMoveRequest.cs`:

```csharp
using Client.Ui.State.Train.Logic;

namespace Client.Ui.Screens.BlockInventory.Train.Timetable
{
    /// <summary>
    /// 停車駅の上下移動の意図
    /// Intent to move a stop up or down
    /// </summary>
    public readonly struct TimetableStopMoveRequest
    {
        public readonly int Index;
        public readonly StopMoveDirection Direction;

        public TimetableStopMoveRequest(int index, StopMoveDirection direction)
        {
            Index = index;
            Direction = direction;
        }
    }
}
```

`TrainTimetableStopListView.cs`:

```csharp
using System;
using Client.Localization;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Decor;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.TimetableEdit;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train.Timetable
{
    /// <summary>
    /// 停車駅の枠（Web TrainTimetableStopList の写し）。押下は自分の意図へ流すだけ
    /// Stop list frame (copy of Web TrainTimetableStopList); presses only flow into its own intents
    /// </summary>
    public sealed class TrainTimetableStopListView : VisualElement
    {
        private const string ListTestId = "train-timetable-stops";
        private readonly GamePanel _panel;
        private readonly LocalizedLabel _empty;
        private readonly VisualElement _list;
        private readonly Subject<TimetableStopMoveRequest> _onMoveRequested = new();
        private readonly Subject<int> _onRemoveRequested = new();

        public IObservable<TimetableStopMoveRequest> OnMoveRequested => _onMoveRequested;
        public IObservable<int> OnRemoveRequested => _onRemoveRequested;

        public TrainTimetableStopListView()
        {
            // Craft 枠に見出し・飾り線・列（空なら文言）
            // A craft frame holding the heading, the fade rule and the list (or the empty text)
            _panel = new GamePanel("train-timetable-stops-panel", GamePanelFrame.Craft);
            var heading = new LocalizedLabel();
            heading.SetKey(LocalizationKeys.Ui.BlockInventory.TimetableStops);
            heading.AddToClassList("train-timetable__section-heading");
            _empty = new LocalizedLabel { name = ListTestId };
            _empty.SetKey(LocalizationKeys.Ui.BlockInventory.TimetableStopsEmpty);
            _list = new VisualElement { name = ListTestId };
            _list.AddToClassList("train-timetable__list");
            _panel.Add(heading);
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
            row.EnableInClassList("train-timetable-stop--current", index == model.CurrentIndex);

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

            // 押下は行の番号つきの意図へ写して流す。行を作り直すと古いボタンごと購読も捨てられる
            // Presses map to intents carrying the row index; rebuilding rows discards old buttons with their subscriptions
            var up = Control($"train-timetable-stop-{index}-up", LocalizationKeys.Ui.BlockInventory.TimetableMoveUp, model.IsPending || index == 0);
            var down = Control($"train-timetable-stop-{index}-down", LocalizationKeys.Ui.BlockInventory.TimetableMoveDown, model.IsPending || index == model.Stops.Count - 1);
            var remove = Control($"train-timetable-stop-{index}-remove", LocalizationKeys.Ui.BlockInventory.TimetableRemove, model.IsPending);
            up.OnClicked.Select(_ => new TimetableStopMoveRequest(index, StopMoveDirection.Up)).Subscribe(x => _onMoveRequested.OnNext(x));
            down.OnClicked.Select(_ => new TimetableStopMoveRequest(index, StopMoveDirection.Down)).Subscribe(x => _onMoveRequested.OnNext(x));
            remove.OnClicked.Select(_ => index).Subscribe(x => _onRemoveRequested.OnNext(x));

            var controls = new VisualElement();
            controls.AddToClassList("train-timetable__controls");
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
（`Subscribe(subject)` にしないのは、ボタンの完了が意図の Subject を閉じないようにするため。`UiNumberFormat` の名前空間は `TimetableEditLogic` と同じ）

`TrainTimetableStationListView.cs`:

```csharp
using System;
using Client.Localization;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Decor;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.TimetableEdit;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train.Timetable
{
    /// <summary>
    /// 追加先の駅一覧の枠（Web TrainTimetableSection の駅一覧の写し）
    /// Frame listing the stations that can be added (copy of the Web TrainTimetableSection station list)
    /// </summary>
    public sealed class TrainTimetableStationListView : VisualElement
    {
        private readonly GamePanel _panel;
        private readonly LocalizedLabel _empty;
        private readonly VisualElement _list;
        private readonly Subject<TrainTimetableStation> _onAddRequested = new();

        public IObservable<TrainTimetableStation> OnAddRequested => _onAddRequested;

        public TrainTimetableStationListView()
        {
            _panel = new GamePanel("train-timetable-stations-panel", GamePanelFrame.Craft);
            var heading = new LocalizedLabel();
            heading.SetKey(LocalizationKeys.Ui.BlockInventory.TimetableStations);
            heading.AddToClassList("train-timetable__section-heading");
            _list = new VisualElement { name = "train-timetable-stations" };
            _list.AddToClassList("train-timetable__list");
            _empty = new LocalizedLabel();
            _empty.SetKey(LocalizationKeys.Ui.BlockInventory.TimetableStationsEmpty);
            _panel.Add(heading);
            _panel.Add(new FadeRule());
            _panel.Add(_list);
            Add(_panel);
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
            foreach (var station in model.Stations) _list.Add(CreateRow(station, model.IsPending, unnamedStationName));
        }

        private VisualElement CreateRow(TrainTimetableStation station, bool disabled, string unnamedStationName)
        {
            // testid は座標を _ でつなぐ（負号はそのまま。例 -8_0_3）
            // The testid joins coordinates with _ and keeps minus signs (e.g. -8_0_3)
            var key = $"{station.Position.x}_{station.Position.y}_{station.Position.z}";
            var row = new VisualElement();
            row.AddToClassList("train-timetable__row");
            var label = new LocalizedLabel();
            label.SetText(TimetableEditLogic.StationLabel(station, unnamedStationName));
            label.AddToClassList("train-timetable__label");
            var add = new PanelActionButton($"train-timetable-station-{key}-add", LocalizationKeys.Ui.BlockInventory.TimetableAdd);
            add.AddToClassList("train-timetable__control");
            add.SetEnabled(!disabled);
            add.OnClicked.Select(_ => station).Subscribe(x => _onAddRequested.OnNext(x));
            row.Add(label);
            row.Add(add);
            return row;
        }
    }
}
```
（`StationLabel` の名前の引数は言語ごとに `Localize.Get(timetableUnnamedStation)` を Presenter が渡す。`StationLabel` の UiText 自体は `LocalizedLabel` が言語の変化で描き直す）

- [ ] **Step 3: 区画と器を書く**

`TrainTimetableSectionView.cs`:

```csharp
using System;
using Client.Localization;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using Client.Ui.State.Train.TimetableEdit;
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

        public TrainTimetableStopListView Stops { get; } = new();
        public TrainTimetableStationListView Stations { get; } = new();
        public IObservable<bool> OnAutoRunSelected => _autoRun.OnOptionSelected;

        public TrainTimetableSectionView()
        {
            name = "train-timetable-section";
            AddToClassList("train-timetable");

            // 自動運転の行: 左に見出し・右に ON/OFF
            // Auto-run row: heading left, ON/OFF right
            var autoRunRow = new VisualElement();
            autoRunRow.AddToClassList("train-timetable__row");
            var autoRunLabel = new LocalizedLabel();
            autoRunLabel.SetKey(LocalizationKeys.Ui.BlockInventory.TimetableAutoRun);
            _autoRun = new ModeSwitch<bool>("train-timetable-auto-run", ModeSwitchOrientation.Horizontal, ModeSwitchRole.Group, new[]
            {
                new ModeSwitchOption<bool>("train-timetable-auto-run-on", true, new UiText(LocalizationKeys.Ui.BlockInventory.TimetableAutoRunOn)),
                new ModeSwitchOption<bool>("train-timetable-auto-run-off", false, new UiText(LocalizationKeys.Ui.BlockInventory.TimetableAutoRunOff)),
            });
            autoRunRow.Add(autoRunLabel);
            autoRunRow.Add(_autoRun);
            _autoRunUnavailable = new LocalizedLabel { name = "train-timetable-auto-run-unavailable" };
            _autoRunUnavailable.SetKey(LocalizationKeys.Ui.BlockInventory.TimetableAutoRunUnavailable);
            _autoRunUnavailable.AddToClassList(UiTypographyClass.TextSm);

            _stack.AddItem(autoRunRow);
            _stack.AddItem(_autoRunUnavailable);
            _stack.AddItem(Stops);
            _stack.AddItem(Stations);
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
            Stops.Render(model, unnamedStationName);
            Stations.Render(model, unnamedStationName);
        }
    }
}
```

`TrainTimetableAreaView.cs`:

```csharp
using Client.Localization;
using Client.Ui.Foundation.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train.Timetable
{
    /// <summary>
    /// 時刻表タブの中身。読込中・使えない・区画のどれか 1 つを出す
    /// Content of the timetable tab: shows exactly one of loading, unavailable, or the section
    /// </summary>
    public sealed class TrainTimetableAreaView : VisualElement
    {
        private readonly LocalizedLabel _loading;
        private readonly LocalizedLabel _unavailable;
        private TrainTimetableSectionView _section;

        public TrainTimetableAreaView()
        {
            name = "train-timetable-area";
            _loading = new LocalizedLabel { name = "train-timetable-loading" };
            _loading.SetKey(LocalizationKeys.Ui.BlockInventory.TimetableLoading);
            _unavailable = new LocalizedLabel { name = "train-timetable-unavailable" };
            _unavailable.SetKey(LocalizationKeys.Ui.BlockInventory.TimetableUnavailable);
            Add(_loading);
            Add(_unavailable);
            ShowLoading();
        }

        public void ShowLoading()
        {
            RemoveSection();
            _loading.style.display = DisplayStyle.Flex;
            _unavailable.style.display = DisplayStyle.None;
        }

        public void ShowUnavailable()
        {
            RemoveSection();
            _loading.style.display = DisplayStyle.None;
            _unavailable.style.display = DisplayStyle.Flex;
        }

        // 列車が替わるたびに新しい区画を作る（Web の key=trainUnitId と同じく中の状態を持ち越さない）
        // A new section per train, carrying no inner state over (same as the Web key=trainUnitId)
        public TrainTimetableSectionView ReplaceSection()
        {
            RemoveSection();
            _loading.style.display = DisplayStyle.None;
            _unavailable.style.display = DisplayStyle.None;
            _section = new TrainTimetableSectionView();
            Add(_section);
            return _section;
        }

        private void RemoveSection()
        {
            _section?.RemoveFromHierarchy();
            _section = null;
        }
    }
}
```

- [ ] **Step 4: Presenter を書く**

`TrainTimetablePresenter.cs`:

```csharp
using Client.Localization;
using Client.Ui.State.Toast;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.Timetable;
using Client.Ui.State.Train.TimetableEdit;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.Ui.Screens.BlockInventory.Train.Timetable
{
    /// <summary>
    /// 時刻表の状態を区画へ流し、区画の意図を列車ごとの編集セッションとコマンドへ渡す
    /// Feeds the timetable state into the section and routes section intents to the per-train edit session and commands
    /// </summary>
    public sealed class TrainTimetablePresenter
    {
        private const string SetAutoRunCommandName = "train_timetable.set_auto_run";
        private readonly TrainTimetableAreaView _view;
        private readonly ITrainTimetableCommands _commands;
        private readonly ITrainTimetableWaitTimer _timer;
        private readonly UiCommandResultToaster _toaster;
        private readonly CompositeDisposable _sectionSubscriptions = new();
        private TrainTimetableEditSession _session;
        private TrainTimetableSectionView _section;
        private TrainTimetableReadyData _server;

        public TrainTimetablePresenter(TrainTimetableAreaView view, ITrainTimetableCommands commands, ITrainTimetableWaitTimer timer, UiCommandResultToaster toaster)
        {
            _view = view;
            _commands = commands;
            _timer = timer;
            _toaster = toaster;
        }

        public void Render(TrainTimetableViewState state)
        {
            if (state is ReadyTrainTimetableViewState ready)
            {
                RenderReady(ready.Data);
                return;
            }
            CloseSection();
            if (state is LoadingTrainTimetableViewState) _view.ShowLoading();
            else _view.ShowUnavailable();
        }

        public void RerenderLanguage()
        {
            if (_section != null) RenderSection();
        }

        public void Close()
        {
            CloseSection();
        }

        private void RenderReady(TrainTimetableReadyData data)
        {
            // 列車が替わったら区画と編集を作り直し、同じ列車ならサーバーの並びを編集へ渡す
            // A different train rebuilds the section and session; the same train hands the server order to the session
            if (_session == null || !_server.TrainUnitInstanceId.Equals(data.TrainUnitInstanceId)) OpenSection(data);
            else _session.ReceiveServerStops(data.Stops);
            _server = data;
            RenderSection();
        }

        private void OpenSection(TrainTimetableReadyData data)
        {
            CloseSection();
            _server = data;
            _session = new TrainTimetableEditSession(_commands, _timer, _toaster, data.Stops);
            _section = _view.ReplaceSection();

            // 編集の表示と区画の意図を購読する（View は購読しない）
            // Subscribe to the session display and the section intents (the View never subscribes)
            _session.DisplayedStops.Skip(1).Subscribe(_ => RenderSection()).AddTo(_sectionSubscriptions);
            _session.IsPending.Skip(1).Subscribe(_ => RenderSection()).AddTo(_sectionSubscriptions);
            _section.Stations.OnAddRequested.Subscribe(station => _session.Replace(TimetableEditLogic.AddStop(_session.DisplayedStops.Value, station))).AddTo(_sectionSubscriptions);
            _section.Stops.OnMoveRequested.Subscribe(move => _session.Replace(TimetableEditLogic.MoveStop(_session.DisplayedStops.Value, move.Index, move.Direction))).AddTo(_sectionSubscriptions);
            _section.Stops.OnRemoveRequested.Subscribe(index => _session.Replace(TimetableEditLogic.RemoveStop(_session.DisplayedStops.Value, index))).AddTo(_sectionSubscriptions);
            _section.OnAutoRunSelected.Subscribe(enabled => SendAutoRun(enabled).Forget()).AddTo(_sectionSubscriptions);
        }

        private void RenderSection()
        {
            var model = TrainTimetableSectionViewModel.Build(_server, _session.DisplayedStops.Value, _session.IsPending.Value);
            _section.Render(model, Localize.Get(LocalizationKeys.Ui.BlockInventory.TimetableUnnamedStation));
        }

        private void CloseSection()
        {
            _sectionSubscriptions.Clear();
            _session?.Close();
            _session = null;
            _section = null;
        }

        // 自動運転は楽観表示しない。結果は公開で反映され、拒否だけトーストへ
        // Auto-run is never shown optimistically; the publication reflects the result and only refusals toast
        private async UniTaskVoid SendAutoRun(bool enabled)
        {
            var result = await _commands.SetAutoRun(enabled);
            _toaster.Report(SetAutoRunCommandName, result);
        }
    }
}
```

`Timetable.uss`:

```css
/* web: features/blockInventory/train/style.module.css の行・操作・一覧・見出し（ローカルのつまみ無し） / (English) rows, controls, lists and headings from the Web train style module; no local knobs */
.train-timetable__list {
    flex-direction: column;
}

.train-timetable__row {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
}

.train-timetable__row > .train-timetable__controls {
    margin-left: var(--mode-switch-padding-inline);
}

.train-timetable__label {
    flex-shrink: 1;
    min-width: 0;
    white-space: normal;
}

.train-timetable-stop--current {
    color: var(--text-high-contrast);
}

.train-timetable__controls {
    flex-direction: row;
    flex-shrink: 0;
}

.train-timetable__controls > .train-timetable__control + .train-timetable__control {
    margin-left: var(--mode-switch-gap);
}

.train-timetable__list > .train-timetable__row {
    padding: var(--mode-switch-padding-block) var(--mode-switch-padding-inline);
}

.train-timetable__list > .train-timetable__row + .train-timetable__row {
    border-top-width: var(--bevel-1);
    border-top-color: var(--bevel-c1);
}

.train-timetable__section-heading {
    padding: 0 var(--mode-switch-padding-inline) var(--mode-switch-padding-block);
    color: var(--text-high-contrast);
}

/* 無効な操作の文字は --text-muted（Web .list button:disabled）。部品のつまみを画面のルートで上書き（D-11） / Disabled control text uses --text-muted (Web .list button:disabled) via the part knob at the screen root (D-11) */
.train-timetable .train-timetable__control:disabled {
    --panel-action-button-text: var(--text-muted);
    opacity: 1;
}
```
（UITK の USS は `gap` と `+` 結合子を持たない。Task 6 Step 5 の compile で USS の警告が出たら、`+` の 2 か所を「先頭以外の子」に当たるクラスを C# が付ける形（`train-timetable__row--following`・`train-timetable__control--following`）に替える。`border-top-width` に `var()` が使えなければ同値のトークンの解決後の値を書かず、`UssCustomStyleReader` で読んで C# が `style.borderTopWidth` に入れる。どちらにしても見た目は Web と同じにする）

- [ ] **Step 5: テストを回してコミット**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.Ui.Screens.BlockInventory.Train.Timetable.TrainTimetableSectionViewTest"`
Expected: `ErrorCount: 0`、2 件 PASS（`Timetable.uss` の取り込みは Task 8。ここでは色・余白を主張しない）

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Train/Timetable moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Train
git commit -m "feat(uitk): 時刻表の区画の View と Presenter を足す（S4-train）

<実行ホストの指示する帰属行>"
```

---

### Task 7: 列車とプラットフォームの本文（`Screens/BlockInventory/Train/`・`Parts/`）

**Files:**
- Create: `S/Client.Ui/Screens/BlockInventory/Train/` の 7 本＋`Train.uss`、`Train/Parts/` の 2 本
- Create: `S/Client.Tests/Ui/Screens/BlockInventory/Train/TrainTestBodies.cs`・`TrainInventoryBodyTest.cs`・`TrainPlatformBodyTest.cs`

**Interfaces:**
- Consumes: alpha の `ITrainInventoryBodyFactory`・`IBlockInventoryBodyFactory`・`BlockItemGridFactory`、S4-machine の `FluidInventoryContents(testid, GameContentPresentation)`、Task 3〜6 の型
- Produces: `TrainInventoryBodyFactory : ITrainInventoryBodyFactory`、`TrainPlatformBodyFactory : IBlockInventoryBodyFactory`（Kind `TrainPlatform`）

- [ ] **Step 1: 画面だけの部品を書く**

`Parts/TrainStationNameRow.cs`:

```csharp
using System;
using Client.Localization;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train.Parts
{
    /// <summary>
    /// 駅名の行（Web TrainStationNameSection の写し）。送るのは決定の押下だけ
    /// Station name row (copy of Web TrainStationNameSection); only the Set press sends
    /// </summary>
    public sealed class TrainStationNameRow : VisualElement
    {
        private readonly GameTextField _input;
        private readonly PanelActionButton _apply;

        public IObservable<string> OnInputEdited => _input.OnValueChanged;
        public IObservable<Unit> OnApplyClicked => _apply.OnClicked;

        public TrainStationNameRow()
        {
            name = "train-station-name-section";
            AddToClassList("train-station-name");
            var label = new LocalizedLabel();
            label.SetKey(LocalizationKeys.Ui.BlockInventory.StationNameLabel);
            label.AddToClassList(UiTypographyClass.TextSm);
            _input = new GameTextField("train-station-name-input", false, GameTextFieldLook.GaugeInset);
            _input.SetPlaceholder(new UiText(LocalizationKeys.Ui.BlockInventory.StationNamePlaceholder));
            _input.AddToClassList("train-station-name__input");
            _apply = new PanelActionButton("train-station-name-apply", LocalizationKeys.Ui.BlockInventory.StationNameApply);
            Add(label);
            Add(_input);
            Add(_apply);
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
using System;
using Client.Localization;
using Client.Ui.Components.Controls;
using Client.Ui.State.Text;
using Client.Ui.State.Train.Platform;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train.Parts
{
    /// <summary>
    /// 積込/卸しの切替（Web TrainPlatformSection の写し）。選択はサーバーの状態でだけ替わる
    /// Load/unload switch (copy of Web TrainPlatformSection); the selection changes only with the server state
    /// </summary>
    public sealed class TrainPlatformModeSection : VisualElement
    {
        private readonly ModeSwitch<TrainPlatformTransferMode> _mode;

        public IObservable<TrainPlatformTransferMode> OnModeSelected => _mode.OnOptionSelected;

        public TrainPlatformModeSection()
        {
            // 選択肢の testid は Web に無いので -load / -unload を足す（D-7）
            // Web options carry no testids, so -load / -unload are added (D-7)
            name = "train-platform-section";
            _mode = new ModeSwitch<TrainPlatformTransferMode>("train-platform-mode", ModeSwitchOrientation.Horizontal, ModeSwitchRole.Group, new[]
            {
                new ModeSwitchOption<TrainPlatformTransferMode>("train-platform-mode-load", TrainPlatformTransferMode.LoadToTrain, new UiText(LocalizationKeys.Ui.BlockInventory.TrainPlatformLoad)),
                new ModeSwitchOption<TrainPlatformTransferMode>("train-platform-mode-unload", TrainPlatformTransferMode.UnloadToPlatform, new UiText(LocalizationKeys.Ui.BlockInventory.TrainPlatformUnload)),
            });
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
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train
{
    /// <summary>
    /// 列車の本文（Web TrainInventoryBody の写し）。タブとスクロール器の中に持ち物と時刻表
    /// Train body (copy of Web TrainInventoryBody): tabs, then inventory and timetable inside the scroll view
    /// </summary>
    public sealed class TrainInventoryBodyView : VisualElement
    {
        private readonly ModeSwitch<TrainInventoryTab> _tabs;

        public TrainInventoryContentsView Contents { get; }
        public TrainTimetableAreaView Timetable { get; } = new();
        public IObservable<TrainInventoryTab> OnTabSelected => _tabs.OnOptionSelected;

        public TrainInventoryBodyView(TrainInventoryContentsView contents)
        {
            name = "train-inventory-body";
            AddToClassList("train-body");
            Contents = contents;
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

`TrainInventoryBodyPresenter.cs`:

```csharp
using Client.Localization;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.Train.Timetable;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Train.Inventory;
using Client.Ui.State.Train.Timetable;
using Client.Ui.State.Train.TimetableEdit;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train
{
    /// <summary>
    /// 列車の本文の Presenter。タブ・取得の依頼・中身と時刻表を View へ流す
    /// Presenter of the train body: routes tabs, fetch requests, contents and the timetable to the View
    /// </summary>
    public sealed class TrainInventoryBodyPresenter : ITrainInventoryPart
    {
        private readonly TrainInventoryBodyView _view;
        private readonly ITrainInventoryBodySource _body;
        private readonly ITrainTimetableSource _timetableSource;
        private readonly ITrainTimetableCommands _commands;
        private readonly TrainTimetablePresenter _timetable;
        private readonly CompositeDisposable _subscriptions = new();
        private TrainTimetableFetchTrigger _fetchTrigger;

        public VisualElement Root => _view;

        public TrainInventoryBodyPresenter(TrainInventoryBodyView view, ITrainInventoryBodySource body, ITrainTimetableSource timetableSource, ITrainTimetableCommands commands, TrainTimetablePresenter timetable)
        {
            _view = view;
            _body = body;
            _timetableSource = timetableSource;
            _commands = commands;
            _timetable = timetable;
        }

        public void Attach(OpenTrainInventoryPanel panel)
        {
            // 開くたびに持ち物タブから始める（Web は本文の mount ごとに初期化）
            // Every open starts on the inventory tab (Web resets per body mount)
            _fetchTrigger = new TrainTimetableFetchTrigger();
            _view.RenderTab(_fetchTrigger.Tab);
            _view.OnTabSelected.Subscribe(SelectTab).AddTo(_subscriptions);
            _body.Current.Subscribe(_view.Contents.Render).AddTo(_subscriptions);
            _timetableSource.Current.Subscribe(ReceiveTimetable).AddTo(_subscriptions);
            Localize.OnLanguageChanged.Subscribe(_ => _timetable.RerenderLanguage()).AddTo(_subscriptions);
        }

        public void Detach()
        {
            _subscriptions.Clear();
            _timetable.Close();
        }

        private void SelectTab(TrainInventoryTab tab)
        {
            var enteredTimetable = _fetchTrigger.SelectTab(tab);
            _view.RenderTab(tab);
            if (enteredTimetable) _commands.RequestOpen();
        }

        private void ReceiveTimetable(TrainTimetableViewState state)
        {
            _timetable.Render(state);
            if (_fetchTrigger.ReceiveState(state)) _commands.RequestOpen();
        }
    }
}
```
（`Localize.OnLanguageChanged` の型は Task 1 Step 3 で確かめる。`IObservable` でなければ前例 `LocalizedLabel` の購読の書き方に合わせる）

`TrainInventoryBodyFactory.cs`:

```csharp
using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.Fluid;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.Train.Timetable;
using Client.Ui.State.Toast;
using Client.Ui.State.Train.Inventory;
using Client.Ui.State.Train.Timetable;
using Client.Ui.State.Train.TimetableEdit;

namespace Client.Ui.Screens.BlockInventory.Train
{
    /// <summary>
    /// source=train の本文を作る（alpha の拡張口）。Presenter と View の組を返す
    /// Creates the source=train body for alpha's extension point, returning a Presenter and View pair
    /// </summary>
    public sealed class TrainInventoryBodyFactory : ITrainInventoryBodyFactory
    {
        private readonly ITrainInventoryBodySource _body;
        private readonly ITrainTimetableSource _timetable;
        private readonly ITrainTimetableCommands _commands;
        private readonly ITrainTimetableWaitTimer _timer;
        private readonly UiCommandResultToaster _toaster;
        private readonly BlockItemGridFactory _gridFactory;
        private readonly GameContentPresentation _content;

        public TrainInventoryBodyFactory(ITrainInventoryBodySource body, ITrainTimetableSource timetable, ITrainTimetableCommands commands, ITrainTimetableWaitTimer timer, UiCommandResultToaster toaster, BlockItemGridFactory gridFactory, GameContentPresentation content)
        {
            _body = body;
            _timetable = timetable;
            _commands = commands;
            _timer = timer;
            _toaster = toaster;
            _gridFactory = gridFactory;
            _content = content;
        }

        public ITrainInventoryPart Create()
        {
            var contents = new TrainInventoryContentsView(_gridFactory.Create("train-inventory-slots"), new FluidInventoryContents("train-inventory-fluids", _content));
            var view = new TrainInventoryBodyView(contents);
            var timetable = new TrainTimetablePresenter(view.Timetable, _commands, _timer, _toaster);
            return new TrainInventoryBodyPresenter(view, _body, _timetable, _commands, timetable);
        }
    }
}
```

- [ ] **Step 3: プラットフォームの本文を書く**

`TrainPlatformBodyView.cs`:

```csharp
using System;
using Client.Ui.Foundation.Layout;
using Client.Ui.Screens.BlockInventory.Fluid;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Train.Parts;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.Train.Platform;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train
{
    /// <summary>
    /// 駅・プラットフォームの本文（Web TrainPlatformInventory の写し）。駅名 → スロット → 液体 → モード
    /// Station and platform body (copy of Web TrainPlatformInventory): name, slots, fluids, then mode
    /// </summary>
    public sealed class TrainPlatformBodyView : VisualElement
    {
        private readonly UiStack _stack = new(UiGap.Sm);
        private readonly BlockItemGrid _grid;
        private readonly FluidInventoryContents _fluids;
        private readonly TrainPlatformModeSection _mode = new();

        public TrainStationNameRow NameRow { get; } = new();
        public IObservable<TrainPlatformTransferMode> OnModeSelected => _mode.OnModeSelected;

        public TrainPlatformBodyView(BlockItemGrid grid, FluidInventoryContents fluids)
        {
            name = "train-platform-view";
            _grid = grid;
            _fluids = fluids;
            _stack.AddItem(NameRow);
            _stack.AddItem(_grid);
            _stack.AddItem(_fluids);
            _stack.AddItem(_mode);
            Add(_stack);
        }

        public void Render(TrainPlatformBodyViewModel model, string stationNameInput)
        {
            // 出さない区画は Stack から隠し、間の余白も詰める
            // Hidden parts leave the stack so their gaps collapse too
            _stack.SetItemShown(NameRow, model.IsStation);
            if (model.IsStation) NameRow.Render(stationNameInput);
            _stack.SetItemShown(_grid, model.HasItemSlots);
            if (model.HasItemSlots) _grid.Render(model.ItemSlots, BlockDetailMath.AllSlotIndices(model.ItemSlots.Count));
            _stack.SetItemShown(_fluids, 0 < model.Fluids.Count);
            _fluids.Render(model.Fluids);
            _stack.SetItemShown(_mode, model.HasTransferMode);
            _mode.Render(model.Mode);
        }
    }
}
```

`TrainPlatformBodyPresenter.cs`:

```csharp
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Commands;
using Client.Ui.State.Toast;
using Client.Ui.State.Train.Platform;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train
{
    /// <summary>
    /// 駅・プラットフォームの本文の Presenter。駅名の下書きと送信・モードの送信を受け持つ
    /// Presenter of the station and platform body: owns the name draft and sends names and modes
    /// </summary>
    public sealed class TrainPlatformBodyPresenter : IBlockInventoryPart
    {
        private readonly TrainPlatformBodyView _view;
        private readonly ITrainPlatformBodySource _source;
        private readonly ITrainBlockCommands _commands;
        private readonly UiCommandResultToaster _toaster;
        private readonly TrainStationNameDraft _draft = new();
        private readonly CompositeDisposable _subscriptions = new();
        private TrainPlatformBodyViewModel _latest = TrainPlatformBodyViewModel.Closed;
        private bool _attached;

        public VisualElement Root => _view;

        public TrainPlatformBodyPresenter(TrainPlatformBodyView view, ITrainPlatformBodySource source, ITrainBlockCommands commands, UiCommandResultToaster toaster)
        {
            _view = view;
            _source = source;
            _commands = commands;
            _toaster = toaster;
        }

        public void Attach(OpenBlockInventoryPanel panel)
        {
            _attached = true;
            _source.Current.Subscribe(Render).AddTo(_subscriptions);
            _view.NameRow.OnInputEdited.Subscribe(_draft.Edit).AddTo(_subscriptions);
            _view.NameRow.OnApplyClicked.Subscribe(_ => ApplyName().Forget()).AddTo(_subscriptions);
            _view.OnModeSelected.Subscribe(mode => SendMode(mode).Forget()).AddTo(_subscriptions);
        }

        public void Detach()
        {
            _attached = false;
            _subscriptions.Clear();
        }

        private void Render(TrainPlatformBodyViewModel model)
        {
            _latest = model;
            _draft.Sync(model.StationName);
            _view.Render(model, _draft.Input);
        }

        private async UniTaskVoid ApplyName()
        {
            var result = await _commands.SetStationName(_draft.Input);
            _toaster.Report(TrainBlockCommands.SetNameCommandName, result);
            if (result is not UiCommandRejected) return;

            // 閉じた後なら入力欄が無いので戻さない。開いていれば最新の同期名へ戻す
            // After close there is no field to revert; while open, return to the latest synced name
            if (!_attached)
            {
                Debug.LogWarning("[TrainStationName] set_name refused after close; nothing to revert");
                return;
            }
            _draft.Reject();
            _view.Render(_latest, _draft.Input);
        }

        private async UniTaskVoid SendMode(TrainPlatformTransferMode mode)
        {
            var result = await _commands.SetTransferMode(mode);
            _toaster.Report(TrainBlockCommands.SetModeCommandName, result);
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
using Client.Ui.State.Toast;
using Client.Ui.State.Train.Platform;

namespace Client.Ui.Screens.BlockInventory.Train
{
    /// <summary>
    /// TrainStation・TrainItemPlatform・TrainFluidPlatform の本文を作る（Kind TrainPlatform）
    /// Creates the body for TrainStation, TrainItemPlatform and TrainFluidPlatform (Kind TrainPlatform)
    /// </summary>
    public sealed class TrainPlatformBodyFactory : IBlockInventoryBodyFactory
    {
        private readonly ITrainPlatformBodySource _source;
        private readonly ITrainBlockCommands _commands;
        private readonly UiCommandResultToaster _toaster;
        private readonly BlockItemGridFactory _gridFactory;
        private readonly GameContentPresentation _content;

        public BlockInventoryBodyKind Kind => BlockInventoryBodyKind.TrainPlatform;

        public TrainPlatformBodyFactory(ITrainPlatformBodySource source, ITrainBlockCommands commands, UiCommandResultToaster toaster, BlockItemGridFactory gridFactory, GameContentPresentation content)
        {
            _source = source;
            _commands = commands;
            _toaster = toaster;
            _gridFactory = gridFactory;
            _content = content;
        }

        public IBlockInventoryPart Create(BlockInventoryBodyMount mount)
        {
            var view = new TrainPlatformBodyView(_gridFactory.Create("train-platform-item-slots"), new FluidInventoryContents("train-platform-fluid-slots", _content));
            return new TrainPlatformBodyPresenter(view, _source, _commands, _toaster);
        }
    }
}
```

`Train.uss`:

```css
/* web: features/blockInventory/train/style.module.css .body と details/station/trainStationNameSection.module.css .row（ローカルのつまみ無し） / (English) the Web train .body and station name .row; no local knobs */
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

.train-station-name > .train-station-name__input {
    flex-grow: 1;
    margin-left: var(--mode-switch-padding-inline);
    margin-right: var(--mode-switch-padding-inline);
}
```
（入力欄の自然な幅は Web の `textAreaField .field` に従い、参照キャプチャが無いので Task 10 の自撮りで確かめる。差が出たら LX に記帳する）

- [ ] **Step 4: 失敗するテストを書く**

`TrainTestBodies.cs`:

```csharp
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.Fluid;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.Train;
using Client.Ui.State.Toast;

namespace Client.Tests.Ui.Screens.BlockInventory.Train
{
    /// <summary>
    /// 列車とプラットフォームの本文を偽の源・偽のコマンドで組む
    /// Builds the train and platform bodies on fake sources and fake commands
    /// </summary>
    public sealed class TrainTestBodies
    {
        public FakeTrainSources Sources { get; } = new();
        public FakeTrainTimetableCommands TimetableCommands { get; } = new();
        public FakeTrainBlockCommands BlockCommands { get; } = new();
        public ManualTrainTimetableWaitTimer Timer { get; } = new();

        public TrainInventoryBodyFactory TrainBodyFactory(UiCommandResultToaster toaster, BlockItemGridFactory grids, GameContentPresentation content)
        {
            return new TrainInventoryBodyFactory(Sources, Sources, TimetableCommands, Timer, toaster, grids, content);
        }

        public ITrainInventoryPart CreateTrainBody(UiCommandResultToaster toaster, BlockItemGridFactory grids, GameContentPresentation content)
        {
            return TrainBodyFactory(toaster, grids, content).Create();
        }

        public IBlockInventoryPart CreatePlatformBody(UiCommandResultToaster toaster, BlockItemGridFactory grids, GameContentPresentation content)
        {
            return new TrainPlatformBodyFactory(Sources, BlockCommands, toaster, grids, content).Create(BlockInventoryTestBodies.NaturalMount());
        }
    }
}
```
（`BlockItemGridFactory`・`GameContentPresentation`・`BlockInventoryBodyMount` のテストでの組み方は alpha の `BlockInventoryTestBodies` と S4-machine の `FluidSectionsTest` の前例に合わせる〈`NaturalMount()` は alpha の補助の実名に置き換える〉。`BlockItemGridFactory` が `IBlockSlotCommands` を要るなら alpha の偽物を渡す）

`TrainInventoryBodyTest.cs`（5 件。骨組みと 2 件を全文で示し、残りは同じ形）:

```csharp
using System.Collections.Generic;
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Logic.Common;
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
        private ITrainInventoryPart _presenter;

        [SetUp]
        public void MountBody()
        {
            _bodies = new TrainTestBodies();
            _presenter = _bodies.CreateTrainBody(Toaster, BlockInventoryTestParts.Grids(), BlockInventoryTestParts.Content());
            Mount(_presenter.Root);
            _presenter.Attach(BlockInventoryTestPanels.Train(BlockInventoryPanelError.None));
            simulate.FrameUpdate();
        }

        [TearDown]
        public void DetachBody()
        {
            _presenter.Detach();
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableSection.test.ts">asks C# to fetch only when the timetable tab is selected</web-test>
        [Test]
        public void 時刻表タブを選んだときだけ取得を頼む()
        {
            // 持ち物タブのあいだは読込中が届いても頼まない
            // On the inventory tab a loading state does not ask
            _bodies.Sources.SetTimetable(TrainTimetableViewState.Loading);
            Assert.AreEqual(0, _bodies.TimetableCommands.OpenRequestCount);
            Assert.IsTrue(IsSelected(Find<Button>(_presenter.Root, "train-tab-inventory")));

            simulate.Click(Find<Button>(_presenter.Root, "train-tab-timetable"));
            Assert.AreEqual(1, _bodies.TimetableCommands.OpenRequestCount);
            Assert.IsTrue(IsShown(_presenter.Root.Q("train-timetable-loading")));
            Assert.IsFalse(IsShown(_presenter.Root.Q("train-inventory-slots")));
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableSection.test.ts">asks C# to fetch on tab select even when the timetable is already ready</web-test>
        [Test]
        public void 用意済みでもタブ選択では取得を頼む()
        {
            _bodies.Sources.SetTimetable(TrainTimetableViewState.Ready(NorthOnly(Unit1)));
            simulate.Click(Find<Button>(_presenter.Root, "train-tab-timetable"));
            Assert.AreEqual(1, _bodies.TimetableCommands.OpenRequestCount);
            Assert.IsTrue(IsShown(_presenter.Root.Q("train-timetable-section")));
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableSection.test.ts">re-asks C# when a reset fetch arrives as loading while the timetable tab stays selected</web-test>
        [Test]
        public void 時刻表タブのあいだに読込中へ戻れば頼み直す()
        {
            _bodies.Sources.SetTimetable(TrainTimetableViewState.Ready(NorthOnly(Unit1)));
            simulate.Click(Find<Button>(_presenter.Root, "train-tab-timetable"));
            _bodies.Sources.SetTimetable(TrainTimetableViewState.Loading);
            Assert.AreEqual(2, _bodies.TimetableCommands.OpenRequestCount);
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableSection.test.ts">shows loading and unavailable as distinct states without warning</web-test>
        [Test]
        public void 読込中と使えないを別の文で出し警告しない()
        {
            simulate.Click(Find<Button>(_presenter.Root, "train-tab-timetable"));
            _bodies.Sources.SetTimetable(TrainTimetableViewState.Loading);
            simulate.FrameUpdate();
            Assert.IsTrue(IsShown(_presenter.Root.Q("train-timetable-loading")));
            Assert.IsFalse(IsShown(_presenter.Root.Q("train-timetable-unavailable")));

            _bodies.Sources.SetTimetable(TrainTimetableViewState.Unavailable);
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(_presenter.Root.Q("train-timetable-loading")));
            Assert.IsTrue(IsShown(_presenter.Root.Q("train-timetable-unavailable")));
            LogAssert.NoUnexpectedReceived();
        }

        /// <web-test file="src/features/blockInventory/train/TrainInventoryBody.test.tsx">液体貨車は名前と量容量を表示し、再取得した量へ更新する</web-test>
        [Test]
        public void 液体貨車は名前と量容量を出し再取得で描き直す()
        {
            var water = MasterHolder.FluidMaster.GetFluidId(ForUnitTestFluidGuids.Water);
            _bodies.Sources.SetBody(new TrainInventoryBodyViewModel(7, new List<SlotItem>(), new[] { BlockFluidSlotState.Filled(water, 10, 1000) }));
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(_presenter.Root.Q("train-inventory-slots")));
            UiTextAssert.Contains("10 / 1,000", _presenter.Root.Q("train-inventory-fluids-slot-0"));

            _bodies.Sources.SetBody(new TrainInventoryBodyViewModel(7, new List<SlotItem>(), new[] { BlockFluidSlotState.Filled(water, 90, 1000) }));
            simulate.FrameUpdate();
            UiTextAssert.Contains("90 / 1,000", _presenter.Root.Q("train-inventory-fluids-slot-0"));
        }
    }
}
```
（`BlockInventoryTestParts`（`Grids()` と `Content()`〈テスト用の `GameContentPresentation`〉を返す小さな補助）は alpha・S4-machine のテスト補助に同等のものがあればそれを使い、無ければ `TrainTestBodies.cs` の中に置く。水の `FluidId` の引き方はテスト用 mod の前例に合わせる）

`TrainPlatformBodyTest.cs`（4 件）:

```csharp
using System.Collections.Generic;
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.Items;
using Client.Ui.State.Train.Platform;
using Core.Master;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Train
{
    public class TrainPlatformBodyTest : ScreenTestHost
    {
        private TrainTestBodies _bodies;
        private IBlockInventoryPart _presenter;

        [SetUp]
        public void MountBody()
        {
            _bodies = new TrainTestBodies();
            _presenter = _bodies.CreatePlatformBody(Toaster, BlockInventoryTestParts.Grids(), BlockInventoryTestParts.Content());
            Mount(_presenter.Root);
            _presenter.Attach(BlockInventoryTestPanels.OfType("TrainItemPlatform"));
        }

        [TearDown]
        public void DetachBody()
        {
            _presenter.Detach();
        }

        /// <web-test file="e2e/tests/block/trainPlatform.spec.ts">PFスロットを表示し、積込から卸しへ目標モードActionを送る</web-test>
        [Test]
        public void PFスロットを出し積込から卸しを送る()
        {
            // Web のモック blockTrainPlatform: 2 枠（12 個と空）・積込
            // Web mock blockTrainPlatform: two slots (12 and empty), load mode
            _bodies.Sources.SetPlatform(new TrainPlatformBodyViewModel(false, string.Empty, true, new[] { new SlotItem(new ItemId(3), 12), new SlotItem(ItemMaster.EmptyItemId, 0) }, new List<BlockFluidSlotState>(), true, TrainPlatformTransferMode.LoadToTrain));
            simulate.FrameUpdate();
            Assert.IsNotNull(_presenter.Root.Q("train-platform-item-slots-slot-1"));
            Assert.IsTrue(IsSelected(Find<Button>(_presenter.Root, "train-platform-mode-load")));

            // 押しても選択は替えず、目標のモードを送るだけ
            // Pressing does not change the selection; it only sends the target mode
            simulate.Click(Find<Button>(_presenter.Root, "train-platform-mode-unload"));
            CollectionAssert.AreEqual(new[] { TrainPlatformTransferMode.UnloadToPlatform }, _bodies.BlockCommands.SentModes);
            Assert.IsTrue(IsSelected(Find<Button>(_presenter.Root, "train-platform-mode-load")));
        }

        /// <web-test file="e2e/tests/block/trainPlatform.spec.ts">液体PFは液体名・アイコン・量容量と卸しモードを表示する</web-test>
        [Test]
        public void 液体PFは液体と卸しモードを出す()
        {
            var water = MasterHolder.FluidMaster.GetFluidId(ForUnitTestFluidGuids.Water);
            _bodies.Sources.SetPlatform(new TrainPlatformBodyViewModel(false, string.Empty, false, new List<SlotItem>(), new[] { BlockFluidSlotState.Filled(water, 250, 1000) }, true, TrainPlatformTransferMode.UnloadToPlatform));
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(_presenter.Root.Q("train-platform-item-slots")));
            UiTextAssert.Contains("250 / 1,000", _presenter.Root.Q("train-platform-fluid-slots-slot-0"));
            Assert.IsTrue(IsSelected(Find<Button>(_presenter.Root, "train-platform-mode-unload")));
        }

        /// <web-test file="src/features/blockInventory/views/__tests__/TrainPlatformInventory.test.tsx">PF自身の充填スロット・常時テキストを更新し、空でも実スロットを残す</web-test>
        [Test]
        public void 充填スロットを描き直し空でも実枠を残す()
        {
            _bodies.Sources.SetPlatform(new TrainPlatformBodyViewModel(false, string.Empty, true, new[] { new SlotItem(new ItemId(3), 12) }, new List<BlockFluidSlotState>(), true, TrainPlatformTransferMode.LoadToTrain));
            simulate.FrameUpdate();
            _bodies.Sources.SetPlatform(new TrainPlatformBodyViewModel(false, string.Empty, true, new[] { new SlotItem(ItemMaster.EmptyItemId, 0) }, new List<BlockFluidSlotState>(), true, TrainPlatformTransferMode.LoadToTrain));
            simulate.FrameUpdate();
            Assert.IsTrue(IsShown(_presenter.Root.Q("train-platform-item-slots-slot-0")));
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableSection.test.ts">sends station names only through Set and hides the field for platforms</web-test>
        [Test]
        public void 駅名は決定でだけ送りPFでは欄を出さない()
        {
            // PF では駅名の行が無い
            // Platforms have no station name row
            _bodies.Sources.SetPlatform(new TrainPlatformBodyViewModel(false, string.Empty, true, new List<SlotItem>(), new List<BlockFluidSlotState>(), true, TrainPlatformTransferMode.LoadToTrain));
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(_presenter.Root.Q("train-station-name-section")));

            // 駅では打つだけでは送らず、決定で 1 回送る。拒否は同期名へ戻す
            // On a station typing sends nothing, Set sends once, and a refusal reverts to the synced name
            _bodies.Sources.SetPlatform(new TrainPlatformBodyViewModel(true, "北駅", true, new List<SlotItem>(), new List<BlockFluidSlotState>(), true, TrainPlatformTransferMode.LoadToTrain));
            simulate.FrameUpdate();
            var field = _presenter.Root.Q<TextField>("train-station-name-input");
            field.value = "南駅";
            Assert.IsEmpty(_bodies.BlockCommands.SentNames);
            simulate.Click(Find<Button>(_presenter.Root, "train-station-name-apply"));
            CollectionAssert.AreEqual(new[] { "南駅" }, _bodies.BlockCommands.SentNames);
            _bodies.BlockCommands.Reject("set_name_failed:duplicate");
            Assert.AreEqual("北駅", field.value);
        }
    }
}
```
（`ForUnitTestFluidGuids`・`ItemMaster.EmptyItemId`・`ItemId` の綴りはテスト用 mod の前例に合わせる。最後の件の `Reject` で `[UiCommandResultToaster]` の Warning が出るので `LogAssert.Expect` を足す）

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.Ui\.Screens\.BlockInventory\.Train\."`
Expected: `ErrorCount: 0`、9 件 PASS（Task 6 の 2 件＋5＋4。ScrollView の自然高などの見た目はここでは主張しない）

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Train moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Train
git commit -m "feat(uitk): 列車とプラットフォームの本文を足す（S4-train）

<実行ホストの指示する帰属行>"
```

---

### Task 8: 登録・USS の取り込み・外殻ごとの L1

**Files:**
- Create: `S/Client.Starter/Registration/GameUi/BlockInventoryTrainGameUiRegistration.cs`（再編後の置き場）
- Modify: `S/Client.Starter/Registration/MainGameUiRegistration.cs`（画面登録区画に 1 行）
- Modify: `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`（`Screens` 区画に 2 行）
- Create: `S/Client.Tests/Ui/Screens/BlockInventory/Train/TrainInventoryFlowTest.cs`・`TrainTimetableFlowTest.cs`・`TrainDesignTest.cs`

- [ ] **Step 1: 登録を書く**

```csharp
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.Train;
using Client.Ui.State.Train.Inventory;
using Client.Ui.State.Train.Platform;
using Client.Ui.State.Train.Timetable;
using Client.Ui.State.Train.TimetableEdit;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi
{
    /// <summary>
    /// 列車族のブロックインベントリ（列車の本文・駅/PF の本文）の登録
    /// Registers the train family block inventory: the train body and the station/platform body
    /// </summary>
    public static class BlockInventoryTrainGameUiRegistration
    {
        public static void Register(IContainerBuilder builder)
        {
            // Model（源の購読は起動時に 1 回）
            // Models subscribe to their sources once at startup
            builder.RegisterEntryPoint<TrainTimetableModel>().As<ITrainTimetableSource>();
            builder.RegisterEntryPoint<TrainInventoryBodyModel>().As<ITrainInventoryBodySource>();
            builder.RegisterEntryPoint<TrainPlatformBodyModel>().As<ITrainPlatformBodySource>();

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
（名前空間は再編後の置き場に合わせる。`RegisterEntryPoint<T>().As<I>()` で `IInitializable` と口の両方に登録されることを compile 後の起動ログで確かめる。前例 `OpenTrainTimetableTracker` は `.AsSelf()`）

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
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Train.Inventory;
using Client.Ui.State.BlockInventory.Logic.Common;
using Core.Master;
using NUnit.Framework;
using UniRx;

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
            var bodies = new TrainTestBodies();
            var state = new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.Train(BlockInventoryPanelError.None));
            var shell = BlockInventoryTestShell.Create(state, bodies.TrainBodyFactory(Toaster, BlockInventoryTestParts.Grids(), BlockInventoryTestParts.Content()));
            Mount(shell.Root);
            bodies.Sources.SetBody(new TrainInventoryBodyViewModel(1, TrainCargoSlots(), new System.Collections.Generic.List<BlockFluidSlotState>()));
            simulate.FrameUpdate();
            UiTextAssert.Contains("24", shell.Root.Q("train-inventory-slots-slot-0"));
            UiTextAssert.Contains("8", shell.Root.Q("train-inventory-slots-slot-1"));
            Assert.IsNotNull(shell.Root.Q("train-inventory-slots-slot-8"));

            // コンテナ不在では alpha のエラー行が出て、本文は作られない
            // With the container missing, alpha's error row shows and no body is built
            state.Value = BlockInventoryTestPanels.Train(BlockInventoryPanelError.ContainerMissing);
            simulate.FrameUpdate();
            Assert.IsTrue(IsShown(shell.Root.Q("train-inventory-error")));
            Assert.IsNull(shell.Root.Q("train-inventory-slots"));
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
（`BlockInventoryTestShell.Create` の差し込み引数の型は alpha 改訂版に合わせる）

`TrainTimetableFlowTest.cs`（`timetable.spec` 2 件。本文ごと開き、時刻表タブで操作する。公開はテストが `SetTimetable` で返す＝サーバーの代わり）:

```csharp
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.Timetable;
using Game.Train.RailGraph;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.Train.Fakes.TrainTestData;

namespace Client.Tests.Ui.Screens.BlockInventory.Train
{
    public class TrainTimetableFlowTest : ScreenTestHost
    {
        private TrainTestBodies _bodies;
        private ITrainInventoryPart _presenter;

        [SetUp]
        public void OpenTimetableTab()
        {
            _bodies = new TrainTestBodies();
            _presenter = _bodies.CreateTrainBody(Toaster, BlockInventoryTestParts.Grids(), BlockInventoryTestParts.Content());
            Mount(_presenter.Root);
            _presenter.Attach(BlockInventoryTestPanels.Train(BlockInventoryPanelError.None));
            _bodies.Sources.SetTimetable(TrainTimetableViewState.Ready(NorthOnly(Unit1)));
            simulate.Click(Find<Button>(_presenter.Root, "train-tab-timetable"));
        }

        [TearDown]
        public void Close()
        {
            _presenter.Detach();
        }

        /// <web-test file="e2e/tests/train/timetable.spec.ts">駅の追加・上下移動・削除を即時反映し、再読込でも保持する</web-test>
        [Test]
        public void 追加移動削除を即時に映し開き直しても保つ()
        {
            // 追加 → 即 2 行・保留
            // Add: two rows at once, pending
            simulate.Click(Find<Button>(_presenter.Root, "train-timetable-station--8_0_3-add"));
            Assert.IsNotNull(_presenter.Root.Q("train-timetable-stop-1"));
            Assert.IsTrue(_presenter.Root.Q("train-timetable-section").ClassListContains("train-timetable-section--busy"));
            var added = new[] { Stop(North, StationNodeSide.Back), Stop(Unnamed, StationNodeSide.Front) };
            Publish(added);

            // 上へ移動 → 削除。どれも公開で確定する
            // Move up, then remove; each settles on publication
            simulate.Click(Find<Button>(_presenter.Root, "train-timetable-stop-1-up"));
            var moved = new[] { added[1], added[0] };
            Publish(moved);
            simulate.Click(Find<Button>(_presenter.Root, "train-timetable-stop-0-remove"));
            Publish(new[] { added[0] });
            Assert.AreEqual(3, _bodies.TimetableCommands.SentReplaces.Count);

            // 閉じて開き直すと、サーバーの並びがそのまま出る
            // Closing and reopening shows the server order as is
            _presenter.Detach();
            _presenter.Attach(BlockInventoryTestPanels.Train(BlockInventoryPanelError.None));
            simulate.Click(Find<Button>(_presenter.Root, "train-tab-timetable"));
            simulate.FrameUpdate();
            UiTextAssert.Contains("1. 北駅", _presenter.Root.Q<Label>("train-timetable-stop-0-label"));
            Assert.IsNull(_presenter.Root.Q("train-timetable-stop-1"));
        }

        /// <web-test file="e2e/tests/train/timetable.spec.ts">拒否された追加を巻き戻し、次の追加で復帰する</web-test>
        [Test]
        public void 拒否された追加を巻き戻し次の追加で戻る()
        {
            simulate.Click(Find<Button>(_presenter.Root, "train-timetable-station--8_0_3-add"));
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("train_timetable.replace failed"));
            _bodies.TimetableCommands.Reject("replace_failed:invalid_station");
            simulate.FrameUpdate();
            Assert.IsNull(_presenter.Root.Q("train-timetable-stop-1"));
            Assert.AreEqual(1, ShownToasts.Count);

            // 次の追加は通る
            // The next add goes through
            simulate.Click(Find<Button>(_presenter.Root, "train-timetable-station--8_0_3-add"));
            Assert.AreEqual(2, _bodies.TimetableCommands.SentReplaces.Count);
            Assert.IsNotNull(_presenter.Root.Q("train-timetable-stop-1"));
        }

        private void Publish(TimetableEditStop[] stops)
        {
            _bodies.TimetableCommands.Accept();
            _bodies.Sources.SetTimetable(TrainTimetableViewState.Ready(Ready(Unit1, false, 0, stops)));
            simulate.FrameUpdate();
        }
    }
}
```

`TrainDesignTest.cs`（共有 2 件。前例は alpha の `BlockInventoryDesignTest` の同名の件。そこには足さず、列車の分を自分のファイルで主張する）:

```csharp
using System.IO;
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.Components.Controls;
using Client.Ui.Screens.BlockInventory.Registry;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Train
{
    public class TrainDesignTest : ScreenTestHost
    {
        private static readonly string[] TrainUssPaths =
        {
            "Assets/Scripts/Client.Ui/Screens/BlockInventory/Train/Train.uss",
            "Assets/Scripts/Client.Ui/Screens/BlockInventory/Train/Timetable/Timetable.uss",
        };

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">ゲージとモード切替を用途別に共通部品へ置換する</web-test>
        [Test]
        public void 列車のモード切替は共通部品ModeSwitchで出す()
        {
            // タブ・自動運転・積込/卸しの 3 つとも共通の ModeSwitch
            // Tabs, auto-run and load/unload all use the shared ModeSwitch
            var bodies = new TrainTestBodies();
            var train = bodies.CreateTrainBody(Toaster, BlockInventoryTestParts.Grids(), BlockInventoryTestParts.Content());
            var platform = bodies.CreatePlatformBody(Toaster, BlockInventoryTestParts.Grids(), BlockInventoryTestParts.Content());
            Assert.IsInstanceOf<ModeSwitch<Client.Ui.State.Train.Inventory.TrainInventoryTab>>(train.Root.Q("train-tabs"));
            Assert.IsInstanceOf<ModeSwitch<Client.Ui.State.Train.Platform.TrainPlatformTransferMode>>(platform.Root.Q("train-platform-mode"));
            Assert.IsInstanceOf<ModeSwitch<bool>>(new Client.Ui.Screens.BlockInventory.Train.Timetable.TrainTimetableSectionView().Q("train-timetable-auto-run"));
        }

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">%sから禁止Mantine部品とテーマ色を除去する</web-test>
        [TestCaseSource(nameof(TrainUssPaths))]
        public void 列車のUSSは色をトークンだけで書く(string ussPath)
        {
            // 色の直書き（#・rgb・色名）が無いことを確かめる。前例は alpha の BlockInventoryDesignTest の同名の件
            // Asserts no literal colors (#, rgb, named); the precedent is the same-named case in alpha's BlockInventoryDesignTest
            var uss = File.ReadAllText(ussPath);
            StringAssert.DoesNotMatch(@"#[0-9a-fA-F]{3,8}\b|rgba?\(|:\s*(white|black|red|green|blue)\s*;", uss);
        }
    }
}
```
（色の直書きの判定は alpha の同名の件の実装をそのまま写す。上の正規表現は仮）

- [ ] **Step 3: compile・テスト・起動**

Run:
```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.Ui\.(State|Screens\.BlockInventory)\.Train\.|UiCommandRejectReason|UiThemeImportBundleTest|UiStyleConventionTest|UiLayerDependencyDirectionTest|LookExceptionLedgerTest|BlockInventory"
uloop get-logs --project-path ./moorestech_client --log-type Error
```
Expected: `ErrorCount: 0`、全件 PASS（State 12・Screens 14・L2・alpha の `BlockInventory*`）。全体スイッチ UITK（`UiSystemEditorSwitch.Set(UiSystem.GameUi)`）で PlayMode を起動し、列車・駅を開いて Error ログ 0 件

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
| 4-block-train-timetable-tab | 自動運転の行・Craft 枠 2 つ・見出しの色と余白・行の区切り線・`+` ボタンの位置 | 同じ。USS に `+` 結合子が無い分は Task 6 の注のクラスで同じにする |
| 4-block-trainPlatform | スロット・ModeSwitch の位置 | 同じ |
| 4-block-trainFluidPlatform | 液体の行（S4-machine の部品）・ModeSwitch | 液体の行の差は S4-machine 持ち（他レーン持ち）。ModeSwitch は同じ |
| 自撮り: 駅名の行 | 入力欄の幅・高さ・決定ボタン | 見込みの例外: 入力欄の自然な幅（Web は `textAreaField .field` の既定幅） |
| 自撮り: 保留中 | 無効な操作の文字色 | 同じ（D-11 のつまみの上書き） |

差は 1 件ずつ「同じ／直した／LX-n／他レーン持ち」で閉じる。LX は `docs/uitk/TODO.md`「## 見た目の例外」に LX-121 から順に使い、USS に `/* look-exception: LX-n */` を付ける

- [ ] **Step 3: コミット**

```bash
git add docs/uitk/TODO.md moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Train
git commit -m "docs(uitk): 列車族の並べ比べの差を閉じる（S4-train）

<実行ホストの指示する帰属行>"
```
（差が無く何も変えていなければコミットしない）

---

### Task 11: 録画（古い適用ボタン前提のシナリオを直す）

**Files:**
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/train/train-timetable-via-ui.cs`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/train/train-station-default-name.cs`

- [ ] **Step 1: `train-timetable-via-ui.cs` を即時送信に直す**

今のシナリオは 2 駅を続けて追加してから `train-timetable-apply` を押す（Web の旧 UI）。Web も UITK も適用ボタンは無く、追加は即送信・保留中は次の追加を受けない。次のとおり直す:
1. 冒頭のコメント（日英）の「追加→適用」を「1 駅ずつ追加し、サーバーの時刻表に載るのを待つ」に替える
2. 「北駅・南駅を追加して適用」の節を次に替える:

```csharp
        p.Note("北駅・南駅を1駅ずつ追加（即時送信）");
        await p.ClickUi($"train-timetable-station-{stationAPos.x}_{stationAPos.y}_{stationAPos.z}-add");
        await p.WaitForTrainState(train, 1, null, null, 10f, "サーバーの時刻表が1駅になる");
        await p.Until("時刻表の保留が解ける", () => !PlaytestUiElementOps.FindUi("train-timetable-section").HasStateFlag("train-timetable-section--busy"), 10f);
        await p.ClickUi($"train-timetable-station-{stationBPos.x}_{stationBPos.y}_{stationBPos.z}-add");
        await p.WaitForTrainState(train, 2, null, null, 10f, "サーバーの時刻表が2駅になる");
        await p.UntilUiElement("train-timetable-stop-1", 5f);
```
（`p.Until` と `FindUi(...).HasStateFlag` の実名は DSL の `PlaytestUiElementOps`・`UiElementSnapshot` に合わせる。`--` を含むクラスが状態フラグとして読める）
3. スクショ名 `02-timetable-applied` を `02-timetable-added` に替える
4. 取得の回数の主張（開いた直後 0・タブ選択で 1・開き直してタブ選択で 2）と拒否ログの数え方（`[TrainScheduleEdit] rejected`）はそのまま残す。UITK では拒否の Warning が `[TrainTimetableCommands] rejected:` で出るので、拒否 0 件の主張がこのタグも数えるよう足す

- [ ] **Step 2: `train-station-default-name.cs` に打鍵と決定を足す**

UITK では入力欄へ打鍵が届く（CEF の制約が無い）。既存の `UntilUiElement("train-station-name-input")` の後に:

```csharp
        p.Note("駅名を入力して決定");
        await PlaytestUiElementOps.SetText("train-station-name-input", "中央駅");
        await p.ClickUi("train-station-name-apply");
        var appliedName = string.Empty;
        await p.Until("サーバーの駅名が替わる", () =>
        {
            p.RunOnServerSync("駅名を読む", () => appliedName = station.GetStateDetail<TrainStationNameStateDetail>(TrainStationNameStateDetail.BlockStateDetailKey).StationName);
            return appliedName == "中央駅";
        }, 10f);
        await PlaytestUiElementOps.WaitUiTextContains("train-station-name-input", "中央駅", 5f);
        await p.Screenshot("02-station-renamed");
```
（サーバーの値を読む DSL の実名は既存シナリオの `RunOnServer` の使い方に合わせる。同期で読めなければ `await p.RunOnServer(...)` を待ちの外へ出し、0.5 秒ごとに読み直す形にする）

- [ ] **Step 3: 回す**

実行前に PlayMode を止め、`uloop execute-dynamic-code` で `UiSystemEditorSwitch.Set(UiSystem.GameUi)` を立ててから、unity-playmode-recorded-playtest スキルの `scripts/run-scenario.sh` で `train/train-timetable-via-ui`・`train/train-station-default-name`・`train/fluid-inventory-via-interact`・`uitk/uitk-lifeline` を回す
Expected: 4 本の `result.json` が PASS、mp4 がある。`done.md`「証拠」に result.json のパス・mp4・`UiInputExclusivity.ActiveUiSystem` の値（GameUi）を書く。生の録画は worktree の gitignore 下に置き、`LOGS` へはスクショ 3 枚までと報告だけ。終わりに `list-work-saves.sh` でテストのワールドを消す

- [ ] **Step 4: コミット**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/train
git commit -m "test(uitk): 時刻表の録画を即時送信に直し駅名の決定を足す（S4-train）

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
git add -A moorestech_client docs .agents/skills/unity-playmode-recorded-playtest/scenarios/train
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
共有区画（`MainGameUiRegistration` の画面登録区画・`GameUiTheme.tss` の `Screens` 区画・`docs/uitk/TODO.md`・`UiCommandRejectReason`〈alpha の `BlockSlotUnavailable` 等と並ぶ〉・`localization.csv`・`Client.Ui.State.asmdef` の references）の衝突は両側の行を残して解く。`localization.csv` が衝突したら解いた後に `pnpm --dir moorestech_web/webui run gen:i18n` を回し直す。取り込み後に `uloop compile` と Task 8 Step 3 のテストを回し、緑を確かめて push

- [ ] **Step 3: G1 を待ってマージする**

`gh pr checks <番号>` が緑になったら `gh pr merge <番号> --merge`（squash しない）。`UnityCursorTooltipScreenTest` が CI だけで落ちるのは既知で無視してよい（他の赤は直して push し直す）

- [ ] **Step 4: 報告と後始末**

1. `bd close <S4-train の issue>`
2. `LOGS/harness/uitk/S4-train/done.md` の 1 行目を「状態: 完了」にし、PR 番号・マージコミット・証拠・差の表・integrator への依頼・未確認を埋める
3. `cmux wait-for -S uitk-s4-train`
4. `moores-wt rm s4-train`（Editor と worktree を畳む。未 push があれば拒否されるので push を確かめてから）

---

## Self-Review

- 写すテスト 20 件（＋共有 2 件）は Task 4（6）・Task 6（2）・Task 7（9）・Task 8（3＋共有 2）に全部ある。数: 6＋2＋5＋4＋1＋2 = 20
- R-1〜R-18 は Task 1〜13 のどこかに受け入れ基準の主張がある。R-15 は Task 10、R-17 は Task 11、R-18 は Task 12〜13
- 型の綴り: `TrainTimetableViewState`・`ReadyTrainTimetableViewState.Data`・`TrainTimetableEditSession.DisplayedStops / IsPending / ReceiveServerStops / Replace / Close`・`TrainTimetableSectionViewModel.Build`・`TrainTimetableFetchTrigger.SelectTab / ReceiveState`・`TrainStationNameDraft.Sync / Edit / Reject` は定義とテスト・Presenter で一致
- ディレクトリの本数: State `Timetable/` 7・`TimetableEdit/` 7・`Inventory/` 6・`Platform/` 8、View `Train/` 8・`Train/Timetable/` 7・`Train/Parts/` 2、テスト `State/Train/Fakes/` 5。どれも 10 以下
- alpha・S4-machine の型は計画の Produces から写し、実物との差は Task 1 Step 2 で合わせる。合わせた結果の形の違いは D-n で足す
- 規約: `Action` のコールバックは使っていない（意図は `Subject` と `Select`）。`Func<>`・partial・try-catch・デフォルト引数は無い。黙る経路は `silent-ok` 1 か所（並びの同じ編集）と、ログつきの捨て（保留中の編集・閉じた後の結果・閉じた後の駅名の拒否）

## 判断記録

| ID | 判断 | 理由 | 却下した案 |
|---|---|---|---|
| D-1 | State は `Train/` の下に役割で 4 ディレクトリ（`Timetable/`・`TimetableEdit/`・`Inventory/`・`Platform/`）、View は `Train/`・`Train/Timetable/`・`Train/Parts/` | 1 ディレクトリ 10 本の上限と、Model / 編集 / 本文 / ブロック側で変わる理由が別 | `Train/` 直下に全部（27 本で上限超え） |
| D-2 | **Web ホストにしかない導出を State で作り直す（重複）**: 駅一覧の組み立て（`GetBlocksByBlockType(TrainStation)`＋駅名の StateDetail）・x→z→y の整列・停車駅の名前の補完と欠けの警告・状態の優先（所有列車・`IsUnavailable`・`IsFetchInFlight`・キャッシュ）・未知の Side の捨て・出発条件（`WaitForTicks`・`TicksPerSecond`）・`AppliedIsAutoRun` の照合・`train_not_open` 等の拒否文字列・駅/PF の詳細（モード・スロット数の有無）・貨車の液体の変換。写し元は `Client.WebUiHost` の `TrainTimetableDtoBuilder`・`TrainTimetableStopParser`・`TrainTimetableActions`・`TrainTimetableOpenActionHandler`・`TrainStationActions`・`TrainPlatformActions`・`TrainPlatformDetailDtoBuilder`・`TrainStationDetailDtoBuilder`・`TrainInventoryDtoFactory` | State は `Client.WebUiHost` を参照できない（層の向き）。CEF 撤去で Web ホストごと消えるので、片方を正にする共通化は撤去後に UITK 側だけが残る形で解消する | Web ホストの導出を共通の場所へ移して両方から呼ぶ（Web ホストを変えない縛り・E-1 に反する） |
| D-3 | 公開待ちの時間切れは口 `ITrainTimetableWaitTimer` で注入し、本番は UniRx の実時間 5000ms | Web の `DEFAULT_ACTION_TIMEOUT_MS` と同じ値。UI の通信待ちでゲームロジックの経過ではないので、AGENTS.md の「ティックで測る」の対象外。テストで手で起こせる | `GameUpdater` のティックで測る（ゲームが止まっていると UI が永久に保留） |
| D-4 | 閉じた後に戻った結果は捨て、Warning ログを出す（`Close()` のフラグ） | Web の unmount 後の無視と同じ。別の列車を開いた後に古い結果で表示を動かさない | 結果を待ってから閉じる（閉じる操作が遅れる） |
| D-5 | Ready の区画は持ち物タブのあいだも作ったまま `display:none`。列車が替わると作り直す | Web は hidden で mount を保ち、key=trainUnitId で作り直す。保留中にタブを替えても編集が切れない | タブごとに作り直す（保留が消える） |
| D-6 | Loading・Unavailable は単一のインスタンスで、同じ状態の連続は流さない | 時刻表タブのあいだの「読込中が届くたびの頼み直し」は Ready → Loading の遷移で起きる（Web の取得のやり直しの公開）。同じ Loading の連続で頼み直すと依頼が無限に続きうる | Loading も毎回新しく作る |
| D-7 | 積込/卸しの選択肢に testid `train-platform-mode-load` / `-unload` を足す | Web の選択肢には testid が無く、UITK の DSL とテストは名前で押す。部品 `ModeSwitch` は選択肢の testid を必須にしている | 文言で押す（言語で壊れる） |
| D-8 | 積込/卸しは State 側の列挙 `TrainPlatformTransferMode` で持ち、サーバーの列挙への写しは `TrainBlockCommands`・`TrainPlatformDetailReader` の 2 か所 | `Client.Ui` に `Game.Block` の参照を足さない（View がサーバーの型を知らない） | `Client.Ui.asmdef` に `Game.Block` を足す |
| D-9 | 拒否の理由は新しい 1 つ `TrainEditRejected`（文言 1 つ）に寄せ、Web と同じ理由文字列は `Detail` に入れる | Web は拒否の生のコードをトーストに出すが、UITK は利用者向けの文言に替える規約（`UiCommandRejectReasonTraits.ToLocalizationKey`）。理由ごとの文言は Web に無いので足さない。ログは Detail 優先で Web と同じ文字列 | 理由ごとに列挙と文言を足す（Web に無い文言が 6 つ増える） |
| D-10 | 停車駅のラベルの中の駅名（名前が空なら `timetableUnnamedStation`）は Presenter が `Localize.OnLanguageChanged` で描き直す | `LocalizedLabel` は自分のキーは描き直すが、引数に埋めた解決済みの文字列は変わらない | 引数に UiText を入れ子で渡す（`LocalizationTextParam` が文字列だけ） |
| D-11 | 無効な操作の文字色は、画面のルートのクラスの下で部品のつまみ `--panel-action-button-text` を `--text-muted` に上書き（＋`opacity: 1`） | Web `.list button:disabled { color: var(--text-muted) }`。`PanelActionButton` に無効の見た目が無く、部品のクラスを画面 USS の主語にしない規約。既定テーマの無効の不透明度が乗ると Web と違うので打ち消す。integrator へ部品側の無効の状態を依頼 | 画面 USS で `.panel-action-button` を直接塗る（規約違反） |
| D-12 | 取得の依頼の判定 `TrainTimetableFetchTrigger` は State に置き、Presenter が持つ | Web の effect の条件は純粋な判定で、L1 で View 無しに確かめられる | View が自分で依頼する（View は購読しない規約） |
| D-13 | 既存の録画 `train/train-timetable-via-ui.cs`・`train/train-station-default-name.cs` はその場で直す（新しいシナリオを作らない） | 適用ボタンは Web にも無く、シナリオは今の Web でも古い。同じ経路の検証を 2 本にしない | `uitk/` に別シナリオを新設（古い方が壊れたまま残る） |
| D-14 | 自動運転と積込/卸しは楽観更新しない。押すと即送り、選択はサーバーの公開でだけ替わる | Web と同じ。`ModeSwitch` も押下で自分では選ばない | 押した瞬間に選択を替える |
| D-15 | 駅名の拒否は待ちのあいだに届いた最新の同期名へ戻す。「未編集」は前の同期値と同じ入力とみなす | Web `TrainStationNameSection`（`latestName` と `previousName` の比べ方）と同じ。拒否のまま dirty で残り再同期されないのを防ぐ | 編集フラグで持つ（打ち戻した入力が同期に従わない差が出る） |
| D-16 | 本文の工場は「Presenter＋View」の組（`ITrainInventoryPart`・`IBlockInventoryPart`）を返し、Model は DI の単一インスタンスで起動時に源を購読する。貨車のスロットも `OpenTrainInventoryPanel.ItemSlots` ではなく自分の Model から読む | 設計書 §4.2「Model の購読は Presenter、View は Render だけ」と alpha の改訂（同じパネルのあいだ `Attach` は呼び直されない） | パネルの `ItemSlots` を `Attach` で描く（スロットの更新が届かない） |

## 設計書・実行計画へ反映する行

設計書 `docs/uitk-migration/execution/component-architecture.md` と実行計画へ本体が足す下書き（この計画では編集しない）。

| ID | 下書き |
|---|---|
| A-155 | 列車族の本文の置き場: State は `Client.Ui.State/Train/{Timetable,TimetableEdit,Inventory,Platform}/`、View は `Client.Ui/Screens/BlockInventory/Train/{,Timetable/,Parts/}`。Model（`TrainTimetableModel`・`TrainInventoryBodyModel`・`TrainPlatformBodyModel`）が源を購読し、alpha の拡張口に差す工場 2 本が Presenter＋View の組を作る。登録は `BlockInventoryTrainGameUiRegistration` |
| A-156 | 時刻表の即時編集 `TrainTimetableEditSession`（Web `useImmediateTimetable` の写し）: 編集は即送信・保留中の編集は捨てる・受理と同じ並びの公開で解く・受理後 5000ms 公開が無ければ解く（実時間。UI の通信待ちでティックの対象外）・拒否は最新のサーバーの並びへ戻す・閉じた後の結果は捨てる。Web ホストにしかない導出（駅一覧・整列・状態の優先・出発条件等）は State に重複して写す（CEF 撤去で解消） |
| A-157 | 断りの理由 `UiCommandRejectReason.TrainEditRejected`（非良性・wire `train_edit_rejected`・文言 `ui.commandRejected.trainEditRejected`）。Web と同じ理由文字列は `Detail` に入れる |
| A-158 | 駅名の欄は `GameTextField(GaugeInset)`＋`PanelActionButton`。送るのは決定の押下だけ、拒否は最新の同期名へ戻す。積込/卸しの選択肢の testid は `train-platform-mode-load` / `-unload`（Web に無い名前を UITK で足す） |
| A-159 | 実行計画 §1.4 の S4-train 行: 写すテストの数を実物の 20 件（`train.spec` 1・`timetable.spec` 2・`TrainTimetableImmediate` 8・`TrainTimetableSection` 5・`TrainInventoryBody` 1・`trainPlatform.spec` 2・`TrainPlatformInventory` 1）＋共有 2 件に直し、依存に S4-machine（`FluidInventoryContents`）を足す |

## 起草メモ（本体向け）

- **数の食い違い（申告）**: 本体の指示は「実物の 21 件」だが、実物で数えると S4-train の持ち分は **20 件**。下調べの 21 件は `test-name-parity --bundle 4` の出力に S4-research の `ResearchDetailPane` の 1 件が混ざっていた。内訳は「写すテスト」表と A-159。`train.spec` の「乗車HUD…」（S2-hud 済み）は除き、`TrainPlatformInventory.test.tsx` の 1 件は足した。ほかに alpha が train に割り振った `blockInventoryDesign.test.ts` の共有 2 件を自分のファイル `TrainDesignTest` に同じタグで写す（alpha の `BlockInventoryDesignTest` には足さない）
- **S4-machine の型**: 計画（09b9a73226）の Produces から写した: `FluidInventoryContents(testid, GameContentPresentation)`・`Render(IReadOnlyList<BlockFluidSlotState>)`、液体の Model `IBlockFluidSlotsModel`（プラットフォームの液体はこれを購読）、`BlockFluidSlotConversion.FromTanks`（貨車の液体の変換に入力側だけで使い、写しを 2 か所に持たない）。S4-machine の Produces は工場ではなくコンストラクタなので、S4-train の工場が `GameContentPresentation` を受けて `new` する。`IBlockFluidSlotsModel` は「列車では空」なので貨車の液体には使えず、`SubInventoryState` の `Fluids` を S4-train の `TrainInventoryBodyModel` が読む
- **alpha の型**: 改訂版（85f905aa3d）の Produces から写した（`ITrainInventoryPart`・`IBlockInventoryPart`・`Attach`/`Detach`・`BlockInventoryBodyMount`）。`OpenTrainInventoryPanel.ItemSlots` は残っているが、同じパネルのあいだ `Attach` は呼び直されないので、スロットの更新は S4-train の `TrainInventoryBodyModel` が `SubInventoryState` から配る（alpha のパネルの Model と同じ源を 2 か所で購読する。D-16）。`IUiStateTransitionRequester` は使わない（閉じるは alpha の外殻）
- **本体の決定 4 件の反映**: (1) 液体の中身は S4-machine のものを使うだけ・依存に S4-machine を足した（Global Constraints・A-159）。(2) 写すテストは上のとおり。(3) `train-timetable-via-ui.cs` は `train-timetable-apply` を押す旧 UI 前提だったので、即時送信に直すタスクを Task 11 に入れた（`train-station-default-name.cs` も UITK では打鍵できるので決定まで延ばした）。(4) Web ホストの導出の作り直しは D-2 に重複として書いた
- **共有ファイルへの追記**: `Client.Ui.State.asmdef` に `Game.Block`・`Core.Update`（駅名・移送の StateDetail と `TicksPerSecond`）。`UiCommandRejectReason` に 1 つ。`localization.csv` に 1 行（`gen:i18n` の再生成と追従記録）。`MainGameUiRegistration` に 1 行・`GameUiTheme.tss` に 2 行
- **integrator への依頼（Task 1 で done.md に書く）**: (a) `PanelActionButton` の無効の見た目（今は画面のつまみの上書きで Web に合わせた。D-11）、(b) 自撮りの参照（駅名の行・Loading/Unavailable・保留中・液体貨車）の撮影表への追加、(c) `Registration/GameUi/` の再編先（alpha が依頼済み。S4-train はその置き場に 1 本足す）。共有の新部品は作っていない
- **設計書との食い違い**: 設計書は `GameScrollView.ScrollTo` を書いているが実物に無い（`SetScrollOffset` 等だけ）。S4-train は使わないので影響は無い
- **要裁定: なし**。目的・裁定から外れる判断は無い
- **未確認（実装時に確かめる）**: `PanelActionButton` に既定テーマの無効の不透明度が乗るか（D-11 で `opacity: 1` を当てた）、テキスト欄にフォーカスがあるあいだ E や閉じるキーが入力欄へ食われず UI の閉じが効くか（C2-b の `UiInputExclusivity` の範囲）、alpha の大型パネルの中で本文が縦に伸びるか（`.train-body` の `flex-grow`）、駅名の入力欄の自然な幅（参照が無い）、USS の `+` 結合子と `border-top-width: var()` が UITK で効くか（Task 6 の注の代替あり）、`UniTaskCompletionSource` の継続が同期で走るか（Task 4 の注の代替あり）、`OpenTrainTimetableTracker.OnRepublishRequested` が取得の状態の変化（取得中 → 済み）と駅名の変化を全部運ぶか、`TrainTimetableSnapshot`・`TrainUnitInstanceId` をテストで組めるか
- 設計検査記録は未実施のまま

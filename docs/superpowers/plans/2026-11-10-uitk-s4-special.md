# UITK S4-special（段階 4 画面: ブロックインベントリの特殊族）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Web 版 `features/blockInventory` の特殊族（FilterSplitter・ElectricToGearGenerator・ElectricPole・SpaceElevatorBase・RocketLaunchPad〈題の合図ボタン付き〉の本文 5 つと、SectionStackView に差し込む区画 GearToElectric・BeltThroughput〈`BarSeries`〉・Gear・ElectricNetwork・GearNetwork の 5 つ）を、S4-alpha の拡張口（blockType ごとの本文・題の操作・SectionStackView の固定順の区画）へ「登録するだけ」で載る UITK の View にし、Web と同じ見た目（720p・2560×1080 の並べ比べ）・同じ操作（フィルタのモードと枠・出力モード・接続・建造/発射の合図・ベルトの窓の←/→）を、L0/L1・録画 `connect/rotary-generator-ui`・`tier9/tier9-4-rocket-launch`・`tier9/tier9-4b-rocket-cargo-shift-reject`・新規 `uitk/uitk-block-special-panels` まで確かめて `feature/uitk` へ取り込む。

**Architecture:** 画面レーン（単位エージェント・Editor 要）。外殻（パネル・題・閉じる・自然高のスクロール器）・`blockComponentRegistry` の解決・SectionStackView の並び・`BlockItemGrid`・`LackHighlightText` は S4-alpha が持ち、このレーンは自分のディレクトリ（`Screens/BlockInventory/Special/`・`Client.Ui.State/BlockInventory/Special/`）と自分の登録ファイル（`BlockInventorySpecialGameUiRegistration.cs`）からだけ差し込む。特殊族の詳細（state detail・電力網/歯車網の集約・フィルタの状態・ベルトの搬出統計）は、Web の橋の `BlockDetailDtoBuilder` 系と同じ源を `SpecialBlockDetailReader` が純関数で読み、`SpecialBlockDetailModel`（`OpenBlockDetailTracker`・`BlockNetworkInfoCache`・`BeltSegmentExitStatisticsTable` を購読し、変化ごとに同期で組み直す [A-57]）が不変の `SpecialBlockDetailViewModel` を `IReadOnlyReactiveProperty` で配る。差し込む部分は alpha の形（S4-alpha 計画 85f905aa3d の「差し込みの形」）どおり Presenter＋View で、Presenter（`SpecialDetailPresenter`・ベルトだけ `BeltThroughputSectionPresenter`）が `Attach(panel)` で alpha のパネルの源（同じパネルの枠）と自分の詳細 Model を購読して View の `Render` へ流し、`Detach()` で切る。View は購読しない。区画の Presenter は `IsApplicable`（`IReadOnlyReactiveProperty<bool>`）を詳細の有無で書き、alpha の `SectionStackBody` がそれを購読して `SetItemShown` で出し入れと先頭の間隔を組み直す（区画は自分の `display` を触らない。D-23）。開いたブロックの識別は alpha のパネル（`BlockInstanceId`・位置・blockType）の 1 本で、詳細の Model・操作・Shift の寄与がそれに揃える（D-26）。操作は `ISpecialBlockCommands`（`UniTask<UiCommandResult>`。前例 `PauseMenuCommands.SubmitBugReport`）を View が直に呼び [A-8]、拒否は `UiCommandResultToaster` へ渡す。表示は Web と同じく楽観更新しない（サーバーの後続の状態で変わる）。見た目は既存の部品（`ModeSwitch`・`ItemSlot`・`GaugeBar`・`BarSeries`・`PanelActionButton`・`UiStack`/`UiGroup`・`LocalizedLabel`）と alpha の `LackHighlightText`・`BlockItemGridGroup` だけで組み、新しい共有部品は作らない。

**Tech Stack:** Unity 6000.3.8f1 UI Toolkit（ランタイムパネル・USS・`AttachToPanelEvent`/`DetachFromPanelEvent`）、UniRx、UniTask、VContainer、Unity Input System（`InputSystem.onEvent`）、NUnit＋`com.unity.ui.test-framework`（`UITestFixture`・`PanelSimulator`）、uloop CLI、プレイテスト DSL（`run-scenario.sh`）、node（Web の参照キャプチャ・test-name-parity・`npm run gen:i18n`）、gh CLI、bd（Beads）

- **パスの略記**: `S/` = `moorestech_client/Assets/Scripts/`、`W/` = `moorestech_web/webui/`、`LOGS` = `../moorestech_logs`（repo の兄弟）、`SKILL` = `.agents/skills/unity-playmode-recorded-playtest`、`SP` = `S/Client.Ui/Screens/BlockInventory/Special`、`SS` = `S/Client.Ui.State/BlockInventory/Special`。Files 節と各コマンドはリポジトリ相対の完全なパスで書く
- **正本**: 裁定 `.decisions/2026-11-06-*`（とくに「段階2以降の画面もWeb版と同じ見た目にする」「長命ブランチの中は全体スイッチ1つ」「UITK切替までCEF由来の不具合は直さない」）、設計書 `docs/uitk-migration/execution/component-architecture.md`（§1.1・§2.4 ModeSwitch/PanelActionButton・§2.5 GaugeBar/BarSeries・§2.7 の「1 feature だけが使うもの」〈`LackHighlightText`・`BlockItemGrid`・`details/rows/*` は blockInventory 内で共有〉・§3.2〜3.4・§4・§5.1、[A-8]・[A-40]・[A-45]・[A-47]・[A-57]・[A-76]・[A-79]・[A-85]）、実行計画 `docs/uitk-migration/execution/execution-plan.md`（§1.4 の S4-special 行・§4.3・§5・§6.2・E-1・E-13・E-19・E-22）。食い違ったら 裁定 → 統合計画 → 設計書 → 実行計画 → この計画 の順で上が勝つ。この計画が設計書・完了条件と違う所は判断記録に書き、設計書に足す行の案は末尾「設計書・実行計画へ反映する行」に置く（指揮が設計書 §7.1 へ写す。A-160〜A-164）
- **レーンの起動は指揮が行う**（実行計画 §4.1。実装は moores-subagent-driven-development でこのホストの Claude Code の subagent が担う）
- **所有の境界**（指揮経由の PLAN-W4a との申し合わせ・2026-11-10。実行計画 E-23）: S4-alpha は `Screens/BlockInventory/` 直下の外殻・`Registry/`・`SectionStack/`（区画の順序 enum を含む）・`Parts/`（`LackHighlightText`・`BlockItemGrid`・rows）・`Client.Ui.State/BlockInventory/Panel/`・`BlockInventoryGameUiRegistration.cs`。S4-special は `Screens/BlockInventory/Special/`・`Client.Ui.State/BlockInventory/Special/`（`Logic/` は L0 済みで使うだけ）・`Client.Starter/Registration/GameUi/Screens/BlockInventorySpecialGameUiRegistration.cs`。S4-machine は `Screens/BlockInventory/Machine/`・`Context/`・`Family/`・`Fluid/`（採掘機・ポンプ・発電機と文脈の区画を含む）、S4-train は `Screens/BlockInventory/Train/`。S4-machine・S4-special・S4-train は自分のディレクトリと `Client.Starter/Registration/GameUi/Screens/` の自分の登録ファイル（W4 以降の画面の登録の置き場。実行計画 E-1 の追記 ad92a44777）から VContainer の複数登録で差し込むだけで、alpha のファイルを書かない。Shaft・Gear・GearBeltConveyor の本文は alpha の既定（SectionStackView）で、このレーンはそこへ区画を差し込むだけ

## Requirements

設計の出所: 実行計画 §1.4 の S4-special 行（完了条件の原文:「FilterSplitter・ElectricToGear・GearToElectric・ElectricPole・Gear・ベルトの流量（`BarSeries`）・ロケット・宇宙エレベーター。写す: `electricPole`(1)・`electricToGear`(1)・`filterSplitter`(3)・`spaceElevator/*`(8)・C（GearToElectric 7・BeltThroughput 3・RocketLaunchPad 5・SpaceElevator 5・Gear 3）。録画 `connect/rotary-generator-ui`・`tier9` のロケット 2 本」、依存 S4-alpha・C4-b）、§6.2（画面レーンの計画に必ず入れるもの）、E-22（C3-b の型〈`HoverTooltipMark`〉を使うので依存に C3-b を足す。C3-b は #1623 でマージ済み）。

| ID | 要件 | 受け入れ基準 |
|---|---|---|
| R-1 | 詳細の Model: 開いたブロックの state detail（`ElectricToGearGeneratorBlockStateDetail`・`GearToElectricGeneratorBlockStateDetail`・`GearStateDetail`・`RocketLaunchPadStateDetail`・`SpaceElevatorBaseStateDetail`）とマスタの param、`BlockNetworkInfoCache`（電力網・歯車網・フィルタ分配器）、`BeltTransportClientCache`（搬出統計）から、Web の橋の `BlockDetailDtoBuilder` 系と同じ値の `SpecialBlockDetailViewModel` を組む。追跡ブロックの変化・状態の変化・集約の更新・ベルトの窓の進みで同期に組み直す（間引かない [A-57]） | `SpecialStateDetailReaderTest`・`SpecialNetworkReaderTest`・`BeltThroughputDetailTest`（L0）が緑。録画（R-12）で値が更新される |
| R-2 | 区画 GearToElectric: 発電状態（`LackHighlightText`・不足トーン）にホバーで計測時期の説明（`HoverTooltipMark`）、供給電力 `current / max`（正の微小値は有効 2 桁。Web `formatPower`）。詳細が無ければ何も出さない | `GearToElectricSectionViewTest`（Web `GearToElectricSection.test.ts` 7 件・`.i18n.test.ts` 1 件）が緑 |
| R-3 | 区画 BeltThroughput: 選んだ窓（既定 180tick）の行・計測済みなら分間行・直近 9 秒の棒（`BarSeries`）。←/→ で窓を切り替える（入力欄にフォーカスがあるとき・パネルが出ていないときは切り替えない）。詳細が無ければ何も出さない | `BeltThroughputSectionViewTest`（Web 3 件）と `InputSystemArrowKeyStreamTest` が緑 |
| R-4 | 区画 Gear（消費側は基準 RPM 付き・発電側は現在値だけ）・ElectricNetwork（需要 0 は「需要なし」、他は集約の 1 行）・GearNetwork（集約の 1 行と止まっていれば停止理由を不足色で） | `GearSectionViewTest`（Web 3 件）・`NetworkSectionViewTest` が緑 |
| R-5 | 本文 ElectricPole（`electric-pole-view` に電力網の区画）・ElectricToGear（縦の `ModeSwitch` で出力モード、充足率の `GaugeBar`、消費電力。選ぶと `SetElectricToGearMode(i)`） | `ElectricBodyViewTest`（Web `electricPole.spec.ts` 1 件・`electricToGear.spec.ts` 1 件）が緑 |
| R-6 | 本文 FilterSplitter: 方向ごとに「出力 n」・横の `ModeSwitch`（既定/ホワイト/ブラック。`filter-mode-{d}`）・フィルタ枠（`filter-slot-{d}-{s}`。左押下は掴んでいる物を設定・掴みが空なら無操作、右押下は解除） | `FilterSplitterBodyViewTest`（Web `filterSplitter.spec.ts` 3 件。3 件目は alpha のパネルの自然高スクロールに載せて末尾の枠へ届く）が緑 |
| R-7 | 本文 RocketLaunchPad: 資材・積荷・出力の 3 群（alpha の `BlockItemGridGroup`。サーバーの絶対添字のまま）・状態文（待ち理由が優先）・進捗の `GaugeBar`、詳細未着は「状態の取得を待っています」。題の操作は合図待ちの段階だけ `PanelActionButton`（`rocket-launch-pad-build`/`-launch`）を出し `SendRocketCommand` | `RocketLaunchPadViewTest`（Web `RocketLaunchPadInventory.test.tsx` の写せる 5 件。本文と題の操作）が緑 |
| R-8 | 本文 SpaceElevatorBase: 入口・出口の 2 群（`space-elevator-grid` の中。絶対添字のまま・ホストの並べ替えをそのまま）・状態文（不足トーン）・プール量・接続済みなら昇降の `GaugeBar`、未接続なら接続ボタン（`readyToConnect` だけ押せる）・電力網の区画、詳細未着は取得待ち | `SpaceElevatorViewTest`（Web vitest 6 件・`spaceElevator.spec.ts` 6 件）が緑 |
| R-9 | Shift の入れ口: 発射台・宇宙エレベーターの入れ口の受け入れ（サーバーと同じ定義）を alpha の Shift の判断へ渡す（受け付けない物・詳細未着の Shift は送らずに通知） | `SpecialInletBindingContributorTest`（L0。Web `shiftRejection.spec.ts` 2 件を判断まで写す）が緑。送らない・通知は alpha の Shift の実行器と録画 `tier9-4b-rocket-cargo-shift-reject`（alpha の拡張口 (6) が無ければ Task 1 Step 2 の分岐 B） |
| R-10 | 操作: `ISpecialBlockCommands` の 5 つ（フィルタのモード・フィルタの枠・出力モード・接続・合図）は開いたブロックの種別を確かめ、サーバーへ送り、結果を `UiCommandResult` で返す。Web の BENIGN_ERRORS と同じもの（合図の重ね押し・接続済み・掴みが空のフィルタ設定）は良性、他はトースト | `SpecialBlockCommandsTest`（L0）が緑。`RocketLaunchPadViewTest` の「%sの成功後topicが遅れても二度目の拒否を誤報しない」が緑 |
| R-11 | 登録: `MainGameUiRegistration` の画面登録区画に 1 行（`BlockInventorySpecialGameUiRegistration.Register(builder);`）。`GameUiTheme.tss` の `Screens` 区画に `@import` 1 行。alpha の拡張口へ本文 5・題の操作 1・区画 5 を登録する | 全体スイッチ UITK で起動して `uloop get-logs --log-type Error` が 0 件。L2（`UiThemeImportBundleTest`・`UiStyleConventionTest`・`UiLayerDependencyDirectionTest`・`LookExceptionLedgerTest`）が緑 |
| R-12 | 録画（全体スイッチ UITK）: 既存の `connect/rotary-generator-ui`・`tier9/tier9-4-rocket-launch`・`tier9/tier9-4b-rocket-cargo-shift-reject` と、新規 `uitk/uitk-block-special-panels`（フィルタ分配器のモードと枠・回転生成機の出力モード・電柱・ベルトの窓の←/→・歯車の区画・宇宙エレベーターの表示）を通す | 4 本の `result.json` が `Success: true`、録画 mp4 があり、`done.md` に `系=GameUi` の Note がある |
| R-13 | 並べ比べ: 15 状態（電柱・回転生成機・フィルタ分配器・歯車発電機・歯車・ベルト・回転発電機 2・発射台・宇宙エレベーター 6）×2 解像度（1280×720・2560×1080）の 30 セルを、セルごとに Web の参照 id と UITK の撮影 id を決めて並べる（Task 10 の撮影表。参照に無いセルは自分で撮り、実行時に作れない状態は EditMode の本番 PanelSettings で撮る。D-34）。差の表を `done.md` に書く | 差の表が 30 行そろい、全行に「同じ／直した／LX-n／捨てる」のどれか（撮れていないセルが 1 つでもあれば未完了）。合否の目安は設計書 [A-14]（720p で 2px） |
| R-14 | 写した Web テストは `<web-test>` で示し、捨てる 1 件（TS の型検査）だけ処遇の表（`docs/uitk/disposition.md`）に行を足す | `node tools/uitk/test-name-parity.mjs --bundle 4 --emit-missing` でこのレーンの持ち分（File Structure の「写すテスト」の表）の未対応が 0、書式・整合の誤り 0 |
| R-15 | 終わりは moores-code-review の全ブランチレビュー → pr-create（base `feature/uitk`・報告ページ）→ `origin/feature/uitk` の取り込み・compile・テスト → G1 緑 → `gh pr merge --merge` → bd を閉じる → `moores-wt rm` | `git log origin/feature/uitk` にこのレーンのマージコミット。`done.md` の 1 行目が「状態: 完了」 |

**やらないこと（スコープ境界）**

- 外殻（`BlockInventoryPanel` 相当・題・閉じるボタン・エラー行・自然高のスクロール器・大型パネルの判定）、`Registry/` の解決、`SectionStack/` の並びと既定の区画（ItemGrid・FluidRow 等）、`Parts/`（`LackHighlightText`・`BlockItemGrid`・`BlockItemGridGroup`・rows）、ブロックのスロット操作（左右押下・ダブルクリック・Shift の実行器）は書かない（S4-alpha）。足りない口は `done.md` の「integrator への依頼」に書いて止めずに進める（Task 1 Step 2 の分岐）
- 機械・採掘機・ポンプ・発電機（`GeneratorSection`。ElectricGenerator・FuelGearGenerator・SimpleGearGenerator の `generator` データ）・清浄機・入力の詰まり・液体の区画は書かない（S4-machine）。列車族は書かない（S4-train）
- 部品（`Components/**`）・Foundation（`Foundation/**`）・トークン USS（`Tokens.uss`・`SlotTokens.uss`・`MantineTokens.uss`・`DerivedTokens.uss`）・`UiLayer`・`UiScreenTraits` は変えない
- Web 側（`moorestech_web/`）と Web の橋（`Client.WebUiHost/**`）は一切変えない。Web の橋の DTO 組み立てと UITK の読み手は CEF 停止の撤去まで 2 本並ぶ（D-2）
- L3（EditModeInPlayingTest）は持たない（段の締め S4-gamma）
- `blockInventoryDesign.test.ts`（S15）・`BlockInventoryPanel.localization.test.ts`（S2）・`blockComponentRegistry.test.ts`・`SectionStackView.test.tsx` は写さない（S4-alpha）

## Global Constraints

- **作業場所**: `moores-wt new uitk-lane/s4-special --from origin/feature/uitk`（Editor 要）。最初に `pwd` で worktree にいることを確かめる。Unity は自分の worktree の Editor だけを使い、起動・停止の前に `moores-wt status` で所有を確かめる。Mac の Editor が逼迫したら Windows の `~/bin/winunity`（compile/test/play を ssh で回す）を使ってよい
- **依存**: S4-alpha（外殻・拡張口・`Parts/`）・C4-b（`BarSeries`。#1624 でマージ済み）・C3-b（`HoverTooltipMark`。#1623 でマージ済み。E-22 により依存に足す）・L0-s4b（`Client.Ui.State/BlockInventory/Logic/**`。#1599 でマージ済み）・C4-e（`PlayerGrabModel`。#1626 でマージ済み）。Task 1 Step 1 で全部の実物を確かめ、S4-alpha が `origin/feature/uitk` に無ければ止めて指揮へ報告する
- **使う部品と公開 API**。C2-*・C3-*・C4-b・C4-e・L0-s4b・段階 1 の型は `origin/feature/uitk`（`0a94c9b96b`）の実物から写した。S4-alpha の型は S4-alpha 計画 85f905aa3d（`origin/uitk-lane/plan-w4a` の `docs/superpowers/plans/2026-11-10-uitk-s4-alpha.md` 末尾「## 後続レーンへの Produces 一覧」）から写した。再検査中で名前が変わりうるので「マージ後に実物と照合」（Task 1 Step 2 で grep し、名前が違えば以後を実物の名前で読み替え、`done.md` の「未確認」に「API 名の読み替え: <計画の名前> → <実物>」と書く。形〈引数・寿命〉が違えば、このレーンの Presenter〈`Special/Registration/`・`Special/Belt/BeltThroughputSectionPresenter.cs`〉と工場だけを直し、View 本体は変えない）

```csharp
// ===== 既存の部品（実物。S/Client.Ui/Components/**）=====
namespace Client.Ui.Components.Controls
{
    public enum ModeSwitchOrientation { Horizontal, Vertical }
    public enum ModeSwitchRole { Group, Tablist }
    public sealed class ModeSwitchOption<TValue> { public ModeSwitchOption(string testid, TValue value, UiText label); public string Testid { get; } public TValue Value { get; } public UiText Label { get; } }
    public sealed class ModeSwitch<TValue> : VisualElement
    {
        public ModeSwitch(string testid, ModeSwitchOrientation orientation, ModeSwitchRole role, IReadOnlyList<ModeSwitchOption<TValue>> options);
        public IObservable<TValue> OnOptionSelected { get; }
        public void SetSelected(TValue value); public void SetSelectedNone(); public void SetEnabledAll(bool enabled); public void SetOptionEnabled(TValue value, bool enabled);
    } // 選択中の選択肢は Button に .mode-switch__option--selected（ScreenTestHost.IsSelected が読む）
    public sealed class PanelActionButton : Button { public PanelActionButton(string testid, LocalizationKey label); public IObservable<Unit> OnClicked { get; } } // 押せない状態は SetEnabled(false)
}
namespace Client.Ui.Components.Gauges
{
    public sealed class GaugeBar : VisualElement { public GaugeBar(string testid); public void SetValue(float ratio01); } // 充填は子 .gauge-bar__fill の style.width（%）。高さは祖先の --gauge-bar-slot-size / 6
    public sealed class BarSeries : VisualElement { public BarSeries(string testid); public void SetValues(IReadOnlyList<float?> ratios01); } // 棒 .bar-series__bar・null は --empty・先頭は --leading
}
namespace Client.Ui.Components.Slots
{
    public sealed class ItemSlot : VisualElement { public ItemSlot(string frameTestid); public void SetContent(ItemSlotContent content); public IObservable<SlotGesture> OnSlotGestured { get; } }
    public sealed class ItemSlotContent { public ItemSlotContent(UiIcon icon, int? count, ItemSlotMarks marks, ItemSlotShortage shortage, ItemSlotGhost ghost, UiText tooltipOverride); } // icon null は空きマス、count null は絵だけ
    public readonly struct ItemSlotMarks { public ItemSlotMarks(bool selected, bool catalog, bool insufficient); }
    public readonly struct SlotGesture { public readonly SlotGestureKind Kind; public readonly bool Shift; }
    public enum SlotGestureKind { LeftDown, RightDown, LeftEnter, RightEnter, DoubleClick, HoverEnter, HoverLeave }
}
namespace Client.Ui.Foundation.Layout
{
    public enum UiGap { None, Px2, Px4, Xs, Sm, Md, Lg, Xl }
    public sealed class UiStack : UiGapContainer { public UiStack(UiGap gap); }   // 縦（Mantine Stack）
    public sealed class UiGroup : UiGapContainer { public UiGroup(UiGap gap); }   // 横（Mantine Group）
    public abstract class UiGapContainer : VisualElement { public void AddItem(VisualElement item); public void SetItemShown(VisualElement item, bool shown); public void ClearItems(); }
}
namespace Client.Ui.Foundation.Text
{
    public class LocalizedLabel : Label { public LocalizedLabel(); public void SetText(UiText text); }
    public static class UiTypographyClass { public const string TextXs = "text--xs"; public const string TextSm = "text--sm"; public const string TextMd = "text--md"; public const string TextDefault = "text--default"; public const string TextMuted = "text--muted"; public const string TextInsufficient = "text--insufficient"; public const string TextHighContrast = "text--high-contrast"; }
}
namespace Client.Ui.Foundation.Tooltip { public static class HoverTooltipMark { public static void Mark(VisualElement element, UiText text); public static void Unmark(VisualElement element); public static bool TryFindMarked(VisualElement from, out VisualElement marked, out UiText text); } }
namespace Client.Ui.Content { public sealed class GameContentPresentation { public UiIcon Item(ItemId id); public UiText ItemName(ItemId id); } }
namespace Client.Ui.State.Text { public sealed class UiText { public UiText(LocalizationKey key); public UiText(LocalizationKey key, LocalizationTextParam[] parameters); public static UiText FromVerbatim(string text); public string Resolve(); } }
namespace Client.Ui.State.Text { public static class UiNumberFormat { public static string Fixed(double value, int decimals); public static string Integer(long value); public static long RoundHalfUp(double value); } } // Fixed は JS toFixed 互換（L0-s4b）

// ===== L0-s4b（実物。S/Client.Ui.State/BlockInventory/Logic/**）=====
namespace Client.Ui.State.BlockInventory.Logic.Panel
{
    public enum BlockInventoryBodyKind { SectionStack, FilterSplitter, ElectricToGear, TrainPlatform, ElectricPole, SpaceElevator, RocketLaunchPad }
    public enum BlockInventoryTitleActionKind { None, RocketLaunchPadCommand }
    public static class BlockComponentRegistry { public static BlockInventoryComponents Resolve(string blockType); }
}
namespace Client.Ui.State.BlockInventory.Logic.Special
{
    public enum FilterSlotClickAction { Set, Clear, Noop }
    public enum FilterSplitterMode { Default, Whitelist, Blacklist }
    public static class FilterSplitterLogic { public static FilterSlotClickAction ClickAction(int grabCount, bool clear); public static LocalizationKey ModeKey(FilterSplitterMode mode); }
    public enum RocketCommand { Build, Launch }
    public readonly struct RocketCommandButton { public RocketCommand Command { get; } public LocalizationKey LabelKey { get; } }
    public static class RocketLaunchPadLogic { public static LocalizationKey StatusKey(RocketLaunchPadPhase phase, RocketLaunchPadWaitReason waitReason); public static bool TryGetCommandButton(RocketLaunchPadPhase phase, out RocketCommandButton button); }
    public readonly struct SpaceElevatorStatusDisplay { public LocalizationKey LabelKey { get; } public bool Insufficient { get; } }
    public static class SpaceElevatorLogic { public static SpaceElevatorStatusDisplay StatusDisplay(SpaceElevatorBaseStatus status); }
}
namespace Client.Ui.State.BlockInventory.Logic.Belt
{
    public enum BeltWindowStep { Previous = -1, Next = 1 }
    public abstract class BeltThroughputWindow { public int Ticks { get; } public double MaxCount { get; } public static BeltThroughputWindow Measured(int ticks, int exitCount, double maxCount); public static BeltThroughputWindow Measuring(int ticks, double maxCount); }
    public sealed class MeasuredBeltWindow : BeltThroughputWindow { public int ExitCount { get; } }
    public abstract class BeltThroughputRowDisplay { public UiText Text { get; } }   // MeasuringBeltRowDisplay・MeasuredBeltRowDisplay（.ItemsPerMinute）
    public static class BeltThroughputLogic
    {
        public const int DefaultBeltWindowTicks = 180; public const int RecentSecondBarCount = 9;
        public static bool TrySelectBeltWindow(IReadOnlyList<BeltThroughputWindow> windows, int ticks, out BeltThroughputWindow selected);
        public static int CycleBeltWindow(IReadOnlyList<BeltThroughputWindow> windows, int currentTicks, BeltWindowStep step);
        public static BeltThroughputRowDisplay RowDisplay(BeltThroughputWindow window);
        public static double?[] RecentSecondBars(IReadOnlyList<int> counts, double maxCountPerSecond, int barCount);
    }
}
namespace Client.Ui.State.BlockInventory.Logic.Detail
{
    public abstract class GearDetail { public double CurrentRpm { get; } public double CurrentTorque { get; } public static GearDetail Consumer(double currentRpm, double currentTorque, double baseRpm); public static GearDetail Generator(double currentRpm, double currentTorque); }
    public sealed class GearRowDisplay { public UiText Torque { get; } public UiText Rpm { get; } public static GearRowDisplay From(GearDetail gear); }
    public enum GearNetworkStopReasonKind { None, Rocked, OverRequirePower, NoGeneration, NoGenerator }
    public enum GearToElectricDisplayState { Generating, Idle, Stopped }
    public sealed class GearToElectricStateDisplay { public LocalizationKey LabelKey { get; } public bool Insufficient { get; } }
    public static class BlockDetailStateDisplay { public static bool TryGetStopReasonKey(GearNetworkStopReasonKind reason, out LocalizationKey key); public static GearToElectricStateDisplay GearToElectric(GearToElectricDisplayState state); }
}
namespace Client.Ui.State.BlockInventory.Logic.SlotRestriction { public readonly struct InletSlotBinding { public InletSlotBinding(int slot, IReadOnlyList<ItemId> acceptedItemIds); public int Slot { get; } public IReadOnlyList<ItemId> AcceptedItemIds { get; } } }

// ===== 段階 1〜C4-e の State（実物）=====
namespace Client.Ui.State.BlockDetail
{
    public sealed class OpenBlockDetailTracker : IInitializable { public BlockGameObject TrackedBlock { get; } public BlockSubInventorySource TrackedSource { get; } public IObservable<BlockGameObject> OnTrackedBlockChanged { get; } public IObservable<Unit> OnTrackedBlockStateChanged { get; } }
    public class BlockNetworkInfoCache
    {
        public GetElectricNetworkInfoProtocol.ElectricNetworkInfoSnapshot Electric { get; }    // 未取得は null（TotalGeneratePower・TotalRequiredPower・ConsumerCount・PowerRate）
        public GetGearNetworkInfoProtocol.GearNetworkInfoSnapshot GearNetwork { get; }         // 未取得は null（TotalRequiredGearPower・TotalGenerateGearPower・StopReason〈Game.Gear.Common.GearNetworkStopReason〉）
        public FilterSplitterStateProtocol.FilterSplitterStateResponse FilterSplitter { get; } // 未取得は null（Directions[].Mode〈Game.Block.Blocks.FilterSplitter.FilterSplitterMode〉・FilterItemIds）
        public IObservable<Unit> OnUpdated { get; }
        public void ApplyFilterSplitterSnapshot(FilterSplitterStateProtocol.FilterSplitterStateResponse response);
    } // 両系で MainGameUiStateRegistration が登録済み（Web の橋もこれを読む）
}
namespace Client.Ui.State.Inventory.Grab { public sealed class PlayerGrabModel : IInitializable { public IReadOnlyReactiveProperty<SlotItem> Current { get; } public PlayerGrabModel(IPlayerInventoryOperations operations); } }
namespace Client.Ui.State.Items { public readonly struct SlotItem { public readonly ItemId ItemId; public readonly int Count; public bool IsEmpty { get; } } }
namespace Client.Ui.State.Commands
{
    public abstract class UiCommandResult { }   // UiCommandAccepted.Instance・UiCommandRejected(UiCommandRejectReason reason, string detail)（.Reason・.Detail・.ToWireCode()。Detail が空でなければ Detail が wire）
    public enum UiCommandRejectReason { EmptyDescription, UnsubmittableReportKind, CaptureRefused, BundleWriteFailed, UnknownLanguage, LanguageAlreadySelected, ItemSourceSlotEmpty, ItemSourceCountShort, SplitGrabNotEmpty, SplitDragNoValidSlots, /* 共有区画: 末尾に足す */ }
    public static class UiCommandRejectReasonTraits { public static bool IsBenign(UiCommandRejectReason r); public static LocalizationKey ToLocalizationKey(UiCommandRejectReason r); public static string ToWireCode(UiCommandRejectReason r); }
}
namespace Client.Ui.State.Toast { public sealed class UiCommandResultToaster { public void Report(string commandName, UiCommandResult result); } } // 拒否は LogWarning「[UiCommandResultToaster] <名> failed: <wire>」、良性でなければトースト

// ===== Client.Game・Client.Network（実物。Web の橋 `S/Client.WebUiHost/Game/Topics/BlockDetail/**`・`Actions/**` が同じ源を読む）=====
// BlockGameObject.GetStateDetail<T>(string key)・.BlockMasterElement.BlockParam・.BlockPosInfo.OriginalPos（Client.Game.InGame.Block）
// BlockSubInventorySource.BlockPosition・TryGetBlockIdentity(out Guid blockGuid, out string blockTypeName, out BlockInstanceId instanceId)
// BeltTransportClientCache.TryGetObservedSegmentIndex(Vector3Int cell, out int segmentIndex)・.Replica.Segments[i].Speed・.ExitStatistics（BeltSegmentExitStatisticsTable: TryGetExitCount(segment, ticks, out exitCount)・ReadRecentExitCounts(segment, ringSlotsPerSecond, int[] buckets) → filled・OnWindowAdvanced）
// BeltSegmentExitWindows.Ticks・.RingBlockTicks（Client.Game.InGame.BeltTransport.Statistics）、BeltConstants.ItemWidth（Core.BeltTransport）、GameUpdater.TicksPerSecond
// ClientContext.VanillaApi.Response.Block: SendFilterSplitterStateRequest(request, ct)・SetElectricToGearOutputMode(pos, index, ct)・ConnectSpaceElevator(pos, instanceId, ct) → (response, PacketWaitCompletionReason)・SendRocketLaunchPadCommand(pos, operation, instanceId, ct) → (response, reason)
// FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetModeRequest(pos, dir, mode)・CreateSetFilterItemRequest(pos, dir, slot, itemId)
// RocketLaunchPadSlotLayout(RocketLaunchPadBlockParam)（Game.Block.Interface.Component.RocketLaunchPad）: MaterialSlots・PayloadSlots・OutputSlotStart・RequirementAt(slot).ItemId
// SpaceElevatorBaseInventoryComponent.SatelliteSlot・FirstOutputSlot・GetCarriedPayloadItemIds(param)（Game.Block.Blocks.SpaceElevator）
```

- **S4-alpha の API**（出所: S4-alpha 計画 85f905aa3d〈`origin/uitk-lane/plan-w4a` の `docs/superpowers/plans/2026-11-10-uitk-s4-alpha.md` 末尾「## 後続レーンへの Produces 一覧」の「差し込みの形」・(1)〜(3)・(6)・画面の部品・パネルの状態・遷移要求の口・登録・共有区画〉。再検査中で名前が変わりうる。マージ後に実物と Task 1 Step 2 で照合する）

```csharp
// ===== S4-alpha（S4-alpha 計画 85f905aa3d の Produces 一覧。再検査中で名前が変わりうる。マージ後に実物と照合）=====
namespace Client.Ui.State.BlockInventory.Panel
{
    // パネルの状態は識別子・エラー・枠だけ（BlockGameObject は載らない）。区画・本文の Model が開いたブロックを引く鍵は BlockInstanceId・Position・BlockGuid
    public interface IBlockInventoryPanelSource { IReadOnlyReactiveProperty<BlockInventoryPanelState> Current { get; } }   // 共有サービス（MainGameUiRegistration の共有サービスの並び）
    public abstract class BlockInventoryPanelState { public static readonly BlockInventoryPanelState Closed; public abstract bool IsSamePanel(BlockInventoryPanelState other); }   // ブロックは位置・Guid・エラーで同じパネル
    public sealed class OpenBlockInventoryPanel : BlockInventoryPanelState
    {
        public string BlockType { get; }  public Guid BlockGuid { get; }  public BlockId BlockId { get; }
        public BlockInstanceId BlockInstanceId { get; }  public Vector3Int Position { get; }
        public IReadOnlyList<SlotItem> ItemSlots { get; }  public BlockInventoryPanelError Error { get; }  public BlockInventoryComponents Components { get; }
    }
    public enum BlockInventoryPanelError { None, OpenFailed, ContainerMissing, TrainCarMissing }   // 「エラー無し」は == None で判定する（null と比べない）
    // 大型パネル判定の寄与（S4-machine。このレーンは寄与しない。特殊族は大型にならない）
    public interface IBlockInventoryLargePanelRule { bool IsLargePanel(OpenBlockInventoryPanel panel); IObservable<Unit> OnRuleInputChanged { get; } }
}
namespace Client.Ui.State.BlockInventory.SlotRestriction
{
    // 拡張口 (6): Shift 配分の宛先の束縛の寄与（ロケット・宇宙エレベーターの入れ口は S4-special）。束縛の詳細は寄与の実装が自分の State から読む（パネルの状態には載らない）
    public interface IBlockSlotRestrictionContributor { bool AppliesTo(OpenBlockInventoryPanel panel); OpenBlockSlotInput Contribute(OpenBlockInventoryPanel panel, OpenBlockSlotInput input); }
}
namespace Client.Ui.State.Routing
{
    public interface IUiStateTransitionRequester { bool RequestTransition(UIStateEnum requested); }   // 共有サービス。このレーンは使わない（閉じるは alpha の外殻）
}
namespace Client.Ui.Screens.BlockInventory.Registry
{
    // 差し込む部分は Presenter（この型）＋View（Root）。Attach で自分の Model を購読して View.Render へ流し、Detach で切る。View は購読しない
    public interface IBlockInventoryPart { VisualElement Root { get; } void Attach(OpenBlockInventoryPanel panel); void Detach(); }
    // 拡張口 (1): blockType ごとの本文（Kind は L0-s4b の BlockComponentRegistry が引く。二重登録は起動時に例外）
    public interface IBlockInventoryBodyFactory { BlockInventoryBodyKind Kind { get; } IBlockInventoryPart Create(BlockInventoryBodyMount mount); }
    public sealed class BlockInventoryBodyMount { public bool FillsPanelHeight { get; private set; } }
    // 拡張口 (2): 題の操作（ブロック・エラー無しのときだけ作られ、GamePanel の題の操作の器 block-inventory-title-action に入る）
    public interface IBlockInventoryTitleActionFactory { BlockInventoryTitleActionKind Kind { get; } IBlockInventoryPart Create(); }
}
namespace Client.Ui.Screens.BlockInventory.SectionStack
{
    // 拡張口 (3): 標準の本文の固定順の区画（列挙の順が描く順。1 Slot に 1 本）。区画の間は UiStack(UiGap.Sm)、区画は自分の外側の余白を持たない
    public enum SectionStackSlot { Context, ItemGrid, Machine, Miner, Pump, Generator, GearToElectric, BeltThroughput, Gear, ElectricNetwork, GearNetwork, FluidDetails, FluidRow }
    public interface IBlockInventorySection : IBlockInventoryPart
    {
        // Attach 後に有効。区画の Presenter が自分の Model の変化で書く（パネルの組み直しの外で変わってよい）。
        // SectionStackBody が購読して出し入れと UiStack の先頭の間隔を組み直す。区画は自分の display を触らない
        IReadOnlyReactiveProperty<bool> IsApplicable { get; }
    }
    public interface ISectionStackSectionFactory { SectionStackSlot Slot { get; } IBlockInventorySection Create(SectionStackSectionMount mount); }
    public sealed class SectionStackSectionMount { public SectionStackConfig Config { get; private set; } public bool FillsPanelHeight { get; private set; } }   // Config は判別 union（Client.Ui.State.BlockInventory.SectionStack）。このレーンは読まない
}
namespace Client.Ui.Screens.BlockInventory.Parts
{
    public enum LackHighlightTextSize { Xs, Sm }
    public sealed class LackHighlightText : LocalizedLabel { public LackHighlightText(string testid, LackHighlightTextSize size); public void Render(UiText text, bool insufficient); }
    public sealed class BlockItemGrid : VisualElement { public void Render(IReadOnlyList<SlotItem> itemSlots, IReadOnlyList<int> slotIndices); } // 枠 testid = "<testId>-slot-<index>"。押下は alpha の IBlockSlotCommands へ配線済み。Render は区画の Presenter から呼ばれる
    public sealed class BlockItemGridGroup : VisualElement { public BlockItemGridGroup(LocalizationKey label, BlockItemGrid grid); } // ラベル text--sm text--muted・間 4px
    public sealed class BlockItemGridFactory { public BlockItemGrid Create(string testId); } // 共有サービス（MainGameUiRegistration の共有サービスの並び）
}
// 呼ばれ方: 同じパネル（IsSamePanel）かつ同じ大きさのあいだは何も呼ばれない。変わると旧部分の Detach → 新しく Create → Attach。画面が隠れると Detach。
//   同じパネルの間の枠の変化は Presenter が IBlockInventoryPanelSource.Current を購読して受ける（前例 alpha の SectionStackItemGridSection: 別のパネルへ替わる途中の値は描かない）
// 登録: 各レーンの Client.Starter/Registration/GameUi/Screens/<レーン>GameUiRegistration.cs（E-1 の追記 ad92a44777）で口のインターフェイスとして登録するだけ（alpha のファイルは触らない）。alpha は IReadOnlyList<口> で受ける
// 複数レーンが ctor で受ける共有サービスは MainGameUiRegistration の共有サービスの並び（IBlockInventoryPanelSource・IUiStateTransitionRequester・BlockItemGridFactory 等）。このレーンの型はどれも自レーン専用なので自分の登録ファイルに置く
// 題の右の逃げ: Web は calc(var(--panel-action-button-width) + var(--mantine-spacing-sm))＝99px。alpha が integrator へ依頼済み。このレーンはロケットの並べ比べで +12px（--mantine-spacing-sm）の差が残っていないかを見て、未処理なら LX に記帳する（Task 10）
// L1 の土台（alpha）: Client.Tests.Ui.State.BlockInventory.PanelModel.BlockInventoryTestPanels（Chest()・ChestWithSlots(...)・OpenFailedChest()・Train(error)・OfType(blockType, params SlotItem[])）・FakePanelSource(ReactiveProperty<BlockInventoryPanelState>)（State で書き換え）、
//   Client.Tests/Ui/Screens/BlockInventory/BlockInventoryTestFixtures.cs の BlockInventoryTestBodies.SectionStack(FakePanelSource source, string blockType, params ISectionStackSectionFactory[] plugged)（.Root・.Attach(panel)）。手本は alpha の SectionStackViewTest「区画の出し入れはパネルの描き直し無しで変わり先頭の間隔が詰まる」
```

- **層の向き**（設計書 §1.1・`UiLayerDependencyRules`）: `Client.Ui.State/**` は Client.Game・Client.Network・Server.*・Game.* を参照してよい（asmdef。このレーンが `Game.Block`・`Game.Gear` を足す。D-8）。`Client.Ui/Screens/**` は State・Components・Foundation・Content を参照してよく、`Client.Game`・`Game.*` の型を受けない（ViewModel は値だけ運ぶ）。`Screens/BlockInventory/Special/**` は alpha の `Screens/BlockInventory/{Registry,SectionStack,Parts}` を参照してよい（同じ機能の中。設計書 §2.7「blockInventory 内で共有」）
- **AGENTS.md の規約**: 1 ファイル 200 行未満・1 ディレクトリ 10 ファイルまで（.meta は数えない）・`partial`/`Func<>` 禁止・イベントは UniRx・try-catch 禁止（このレーンに外部境界の catch は無い。通信の失敗は `PacketWaitCompletionReason` と応答の値で分岐する）・デフォルト引数禁止・単純な getter/setter プロパティ禁止（`{ get; private set; }` と式本体の読み取り専用は可）・主要処理に「// 日本語 → // English」の 2 行コメント（各 1 行）・メソッド内のローカル関数は `#region Internal`・初期化は `Initialize`・fail-closed の経路はログ（意図した無音は `silent-ok: <理由> / <誰がどこで気づくか>`）・`.meta` は手で作らない・Prefab/シーンを手で書き換えない・`Update()` で毎フレームの同値判定をしない
- **USS の規約**（ref-uitk-design・設計書 §3.2）: 画面 USS `SP/BlockInventorySpecial.uss` はルートのクラスごと（`.electric-pole-view`・`.electric-to-gear-view`・`.filter-splitter`・`.rocket-launch-pad-view`・`.space-elevator-view`・`.special-section`）で始まり、子は `__*`。部品のクラスを書かない（部品の見た目はつまみ）。色は `var()` だけ。`z-index`・構造擬似クラス・`gap`・合成の太字を使わない。USS の先頭に `/* web: <Web の CSS> */` を日英で書く。このレーンの見た目は Mantine の `Stack`/`Group`/`Text` だけなので、間隔は `UiStack`/`UiGroup` の `UiGap`、文字は `UiTypographyClass`、色は `--text-default`・`--text-muted`・`--text-high-contrast`・`--text-insufficient`（`text--*` のクラス）で当て、USS に書くのは Web の局所上書きがある所だけ（無ければ USS は空のルール 1 行にしない。Task 6 Step 3 で要るものだけ書く）
- **字間・色の罠（持ち越し）**: UITK の字間は字の大きさ/100 × 値。このレーンは字間を書かない（Web も `normal`）。部品ギャラリーは sRGB 合成なので、色の比較はランタイムの PanelSettings（Linear 合成）で撮る（Task 10）。暗い半透明の α は I-fix1 で指数 1.85 に選び直し済み、明るい色の α は未実測（このレーンは半透明を足さない）
- **テストの置き場と名前**: `S/Client.Tests/Ui/Screens/BlockInventory/Special/`（区画・本文の L1）・`S/Client.Tests/Ui/State/BlockInventory/Special/`（L0）・`S/Client.Tests/Input/ArrowKeys/`（矢印キー）、namespace はディレクトリに合わせる。テスト名は日本語の挙動文。Web から写したものは `[Test]` の直前に `/// <web-test file="<W/ からの相対パス>"><it/test の第 1 引数></web-test>`（describe は含めない。`it.each`・`for` の中の `test` は第 1 引数の書式文字列そのまま〈`${…}`・`%s` を展開しない〉。同じタグの NUnit が複数あってよい。実行計画 E-13・W0-tools 書式）
- **コマンド**: コンパイル `uloop compile --project-path ./moorestech_client`（期待: ErrorCount 0）。テスト `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。このレーンのテストフィルタ（以下 `$FILTER`）は `^Client\.Tests\.(Ui\.(Screens\.BlockInventory|State\.BlockInventory|State\.PauseMenuCommandsTest|Conventions|Theme|Layers|Tooltip)|Input\.ArrowKeys)\.`（`PauseMenuCommandsTest` は良性の一覧を固定するので名指し。alpha・S4-machine・S4-train の BlockInventory のテストも同じフィルタに入り、取り込み後の回帰を見る）。ドメインリロード中のエラーは 45 秒待って再試行
- **共有ファイルの所有**（実行計画 E-1）: このレーンが書く共有ファイルは、`S/Client.Starter/Registration/MainGameUiRegistration.cs` の画面登録区画の 1 行、`moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss` の `Screens` 区画の `@import` 1 行、`S/Client.Ui.State/Client.Ui.State.asmdef` の `references` の末尾 4 要素（`Game.Block`・`Game.Gear`・`Core.BeltTransport`・`Core.Update`。D-8）、`S/Client.Ui.State/Commands/UiCommandRejectReason.cs` の共有区画（enum と 3 つの switch の末尾に 4 値）と良性の一覧を固定する `S/Client.Tests/Ui/State/PauseMenuCommandsTest.cs` の「良性の拒否理由は…」1 件、`Localization/localization.csv` の `ui.commandRejected.*` の群の末尾 4 行と再生成した `W/src/shared/i18n/generated/localizationKeys.ts`（`npm run gen:i18n`。台帳「## 追従記録」へ 1 行）、`docs/uitk/TODO.md` の「## 実測の結論」「## 見た目の例外」（`LX-126`〜`LX-130`）の末尾、`docs/uitk/disposition.md` の「## 処遇」の末尾 1 行（D-13）。シナリオ `SKILL/scenarios/uitk/uitk-block-special-panels.cs` はこのレーンが持つ。既存の `connect/rotary-generator-ui.cs`・`tier9/tier9-4-rocket-launch.cs`・`tier9/tier9-4b-rocket-cargo-shift-reject.cs` は書き換えない（GameUi で通すだけ。落ちたら Task 11 の分岐）
- **画面専用の部品の置き場**（設計書 §5.1-2）: このレーンの View・Presenter はすべて `SP/` の下（族ごとのサブディレクトリ）。新しい汎用部品は作らない（D-18）。矢印キーの流れ `InputSystemArrowKeyStream` は `Client.Ui` が Input System を参照しないため `S/Client.Input/ArrowKeys/` に新規ファイルで置く（前例 C4-e の `Client.Input/Pointer/InputSystemPointerButtonStream.cs`・E-19 の「最初の使い手が小さな基盤を持つ」。D-7）
- **新しい共有部品・口を見つけたときの手順**: 作業中に「別の画面でも要る部品」「alpha の口に無い差し込み」を見つけたら作らない。`LOGS/harness/uitk/S4-special/done.md` の「## integrator への依頼」に「部品名／Web の正本のパス／使う画面／このレーンで作らない理由」を 1 行で書き、`bd note <bd> "<同じ 1 行>"` にも残して先へ進む。既知の依頼（Task 1 Step 3 で雛形に書く）: 無し（Task 1 Step 2 の照合で増えたらそこで書く）。ただし、マージ済みのレーンの State の口（例: `OpenBlockDetailTracker`・`BlockNetworkInfoCache` の合図・読み口）に合図や読み口が足りないときは、依頼にせずこのレーンが足してよい。そのときは本番の実装と記録係・偽物を同じ PR で全部直す（E-1 の追記 ad92a44777）。ホイール 1 ノッチの px（A-71）を `GameScrollView` の外で使うことになったら、Foundation の共有定数へ上げてよい（同。いまの計画には使い所が無い）
- **参照キャプチャ**: `LOGS/uitk/reference-captures/web-a9d5123537/`（1280×720）・`web-0e81bbe3c3-2560x1080/`（2560×1080）の `4-block-electricPole`・`4-block-electricToGear`・`4-block-filterSplitter`・`4-block-gearGenerator`（SimpleGearGenerator の fixture。このレーンの Gear と GearNetwork の 2 区画だけが出る）、2560×1080 だけにある `4-block-spaceElevator{,Climbing,Insufficient,Loading,OutputBlocked,Reordered}`。どのセルをどの参照・撮影で比べるかは Task 10 の撮影表（15 状態×2 解像度）が正本。参照に無いセルは Task 10 Step 1 で master の使い捨て worktree の mock-host に一時の fixture を足して撮る（E-22・D-15）
- **並べ比べの画像の原本**は worktree の gitignore 下 `moorestech_client/.uloop/outputs/compare/S4-special/` に置き、`moorestech_logs` へ入れない（スクショ 3 枚程度まで）。`moorestech_logs` へ書く前に `LOGS/README.md` を読む
- **報告**: `LOGS/harness/uitk/S4-special/done.md`（1 行目「状態: 完了」。途中で止まるときは「状態: 停止（理由）」）。節は「証拠」「差の表」「要裁定」「integrator への依頼」「未確認」「部分移植の残り」

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 注目点6件（矢印キーの前例・区画の display の書き手 2 人・開いたブロックの源 3 つ・依存の束・State からサーバー実装への依存・死活表の欠け）を D-7・D-23・D-25・D-26 と死活表の 2 行に反映。検査後の修正（D-27 の Details の列挙を internal にした所）は fresh-context の再検査を回していない
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強2・弱3・第3バケツ2 / 強 2 件（VM の任意の詳細・送信の結果）はシミュレーター予測でこのまま（D-28・D-29）、弱は D-30・D-31 とベルトの 2 度引きの修正、層の外の指摘（Screens へのサーバーの列挙）を D-27 で修正、第3バケツは「本PR外のリファクタ提案」へ
- 変更範囲の再検査（独立評価〈codex 3 件〉・S4-alpha 85f905aa3d・W4a の申し送り・E-1 の追記 ad92a44777 を反映した後。fresh-context 2 体）: 配置 違反0・注意4 / 型・整合 強0・弱5 / codex の 3 件（区画の display 直書き・2 解像度・件数）の解消を Web の実物の数え直し〈宣言 44・ケース 62→区画の id の面を足して 70〉で確認。注意は filterSplitter の写しを本文のホスト越しのパネル全体で見る形へ・`BodyMount` を実物の public ctor へ・統合 L1 の名前を中身に合わせ末尾の網の消失を足す・パネルと追跡ブロックの持続する食い違いを警告ログへ、弱は `regression/sectionStack` の特殊族の区画の id 8 ケース（W-1）・食い違いの数え直し・撮り方を A-164 へ（asmdef は A-160 へ畳む）・`SetShown` の注記・歯車ポンプの空の検査の削除と登録の using を反映。登録行は区画の末尾に直した。この修正後の再検査は回していない

---

## 死活表（Phase 2.5）

全体スイッチ UITK（`UiInputExclusivity.ActiveUiSystem == GameUi`）で起動したとき、Web の系で動いていた特殊族の振る舞いが生きるか。Web の橋（`BlockInventoryTopic`・`BlockDetailDtoBuilder` 系・`FilterSplitter*ActionHandler`・`ElectricToGearSetOutputModeActionHandler`・`SpaceElevatorConnectActionHandler`・`RocketLaunchPadCommandActionHandler`）は Web の系でだけ登録され、源（`OpenBlockDetailTracker`・`BlockNetworkInfoCache`・`BeltTransportClientCache`・`SubInventoryState`）は両系で登録済み。

| 振る舞い | GameUi で生きるか | 根拠 |
|---|---|---|
| フィルタ分配器の方向ごとのモード切り替え | 生きる | `FilterSplitterBodyView` の `ModeSwitch` → `ISpecialBlockCommands.SetFilterMode`（対象は alpha のパネルの位置・`BlockInstanceId`。D-26） → `SendFilterSplitterStateRequest` → 応答を `BlockNetworkInfoCache.ApplyFilterSplitterSnapshot` → `OnUpdated` → Model が組み直す（Task 4・7） |
| フィルタ枠の設定・解除 | 生きる | 左押下は掴んでいる物、右押下は空。掴みが空の左押下は送らない（Web の `noop`）（Task 4・7） |
| 回転生成機の出力モード選択 | 生きる | `ElectricToGearBodyView` → `SetElectricToGearMode` → `SetElectricToGearOutputMode`。表示は後続の state detail で変わる（Task 4・7） |
| 電柱を開いて電力網の集約を見る | 生きる | `BlockNetworkInfoCache` の電力網のポーリング（両系）→ `ElectricPoleBodyView`（Task 3・7） |
| 回転発電機の発電状態・供給電力 | 生きる | `GearToElectricGeneratorBlockStateDetail` → `GearToElectricSectionView`（Task 3・6） |
| ベルトの流量と←/→ の窓切り替え | 生きる | `BeltSegmentExitStatisticsTable.OnWindowAdvanced` の購読で組み直し、←/→ は `InputSystemArrowKeyStream`（Task 3・5・6） |
| 歯車の区画・歯車網の集約と停止理由 | 生きる | `GearStateDetail`・`BlockNetworkInfoCache.GearNetwork`（Task 3・6） |
| 発射台の建造・発射の合図 | 生きる | 題の操作 `RocketLaunchPadTitleActionView` → `SendRocketCommand`（Task 4・8）。題の右の余白は alpha |
| 発射台・宇宙エレベーターのスロット操作（左右押下・ダブルクリック・Shift） | alpha のマージ後に生きる | 枠は alpha の `BlockItemGridFactory.Create` の `BlockItemGrid`（`BlockItemGridGroup` で見出しを付ける）、身振りの実行器は alpha。このレーンは絶対添字を渡すだけ（Task 8） |
| 発射台・宇宙エレベーターへの Shift の入れ口の絞り | alpha のマージ後に生きる | `SpecialInletBindingContributor` が alpha の拡張口 (6) `IBlockSlotRestrictionContributor` で入れ口を寄与（Task 3・9）。口が実物に無ければ Task 1 Step 2 の分岐 B |
| 宇宙エレベーターの接続 | 生きる | `SpaceElevatorSectionView` → `ConnectSpaceElevator`（Task 4・8） |
| 機械・発電機など標準の本文の中の歯車・電力網・歯車網の区画（S4-machine の本文にも同じ区画が差し込まれる） | alpha のマージ後に生きる | 区画は alpha の拡張口 (3) の Slot に 1 本ずつ登録し、どの blockType の標準の本文にも同じ工場が差し込まれる。区画の Presenter が詳細の有無で `IsApplicable` を書き、alpha の `SectionStackBody` が `SetItemShown` で出し入れする（Task 6・9） |
| 網の集約・ベルトの統計がパネルの組み直しの外で届き、先頭の区画が後から出る・消える | 生きる（間隔も Web と同じ） | 区画は自分の `display` を触らず `IsApplicable` を書くだけで、親の `SetItemShown` が先頭の間隔を組み直す。統合 L1 `SpecialSectionStackSpacingTest` が後続の区画の余白まで見る（Task 9。D-23） |
| ベルトを開いて ←/→ を押すと窓の切り替えとプレイヤーの横移動が同時に起きる | Web と同じ（変えない） | ←/→ は Player/Move にも割り当て済み（`moorestechInputSettings.inputactions`）、`SubInventoryState.LocksPlayerMovement()` は false、Keyboard の抑止は入力欄のフォーカス中だけ（`UiInputExclusivity`）。Web の系も同じ割り当て・同じロックなので振る舞いは同じ（コード読み。D-32） |
| 合図の重ね押し・接続済みの再押下が誤報にならない | 生きる | 良性の拒否 `SpecialBlockCommandStale`（Task 4） |
| 特殊族の Web の CEF 側の表示 | GameUi では描かない（裁定どおり） | 全体スイッチ 1 つ（`.decisions/2026-11-06-長命ブランチの中は全体スイッチ1つ`）。Web の系は変えない |

---

## File Structure

### 所有するファイル（このレーンが作る）

State（`SS` = `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special`）:

| パス | 責務 |
|---|---|
| `SS/ISpecialBlockDetailModel.cs` | 詳細の Model の口（`IReadOnlyReactiveProperty<SpecialBlockDetailViewModel> Current`） |
| `SS/SpecialBlockDetailModel.cs` | 源の購読と同期の組み直し（`IInitializable`。alpha のパネルの `BlockInstanceId` と追跡ブロックが一致する間だけ読む。D-26） |
| `SS/Details/SpecialBlockDetailViewModel.cs` | 開いたブロックの特殊族の詳細（不在は null の参照ではなく `TryGet*` で読む） |
| `SS/Details/ElectricToGearDetail.cs` | 出力モードの一覧・選択・充足率・消費電力 |
| `SS/Details/GearToElectricDetail.cs` | 発電状態・供給電力・最大 |
| `SS/Details/BeltThroughputDetail.cs` | 窓の一覧・直近の秒ごとの搬出数・秒あたり最大 |
| `SS/Details/NetworkDetails.cs` | 電力網・歯車網の集約 |
| `SS/Details/FilterSplitterDetail.cs` | 方向ごとのモードとフィルタ枠 |
| `SS/Details/RocketLaunchPadDetail.cs` | 段階・待ち理由・進捗・資材/積荷の枠・出力の先頭 |
| `SS/Details/SpaceElevatorDetail.cs` | 入口・出口の枠・接続・プール・昇降・状態 |
| `SS/Text/JsNumberText.cs` | JS の `toFixed`/`toPrecision(2)`/`String(number)` の写し |
| `SS/Text/SpecialDetailTexts.cs` | 区画・本文の文（`UiText`）を組む純関数 |
| `SS/Source/SpecialBlockDetailReader.cs` | `BlockGameObject`・`BlockNetworkInfoCache`・`BeltTransportClientCache` から VM を組む（Web の `BlockDetailDtoBuilder` の特殊族の部分の写し） |
| `SS/Source/SpecialStateDetailReader.cs` | state detail とマスタの param から各 Detail を組む純関数 |
| `SS/Source/SpecialNetworkReader.cs` | 集約キャッシュから網と分配器の Detail を組む純関数 |
| `SS/Source/BeltThroughputReader.cs` | ベルトの複製と統計から `BeltThroughputDetail` を組む |
| `SS/Source/SpecialInletBindingContributor.cs` | 発射台・宇宙エレベーターの入れ口（alpha の Shift の口の実装。追跡ブロックをパネルの `BlockInstanceId` で照らして読む） |
| `SS/Commands/ISpecialBlockCommands.cs` | 操作の口 |
| `SS/Commands/SpecialBlockCommands.cs` | 開いたブロックの確かめ・掴みの読み・送信・結果の写し |
| `SS/Commands/IOpenSpecialBlock.cs` | 開いたブロックの位置・種別・インスタンス（`OpenSpecialBlock`）を読む口 |
| `SS/Commands/PanelOpenSpecialBlock.cs` | alpha のパネルの源（`IBlockInventoryPanelSource`）からの実装（D-26） |
| `SS/Commands/ISpecialBlockRequestSender.cs` | サーバーへの送信の口と結果 `SpecialBlockRequestOutcome` |
| `SS/Commands/VanillaSpecialBlockRequestSender.cs` | `ClientContext.VanillaApi` での実装（分配器の応答をキャッシュへ当てる） |
| `SS/Commands/SpecialBlockResponseCodes.cs` | 通信結果・サーバーの拒否理由 → Web と同じ失敗コード・良性の判定 |

Screens（`SP` = `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special`）:

| パス | 責務 |
|---|---|
| `SP/BlockInventorySpecial.uss` | 画面 USS（Web の局所上書きだけ） |
| `SP/Network/ElectricNetworkSectionView.cs` | 区画 ElectricNetwork（`electric-network-section`） |
| `SP/Network/GearNetworkSectionView.cs` | 区画 GearNetwork（`gear-network-section`） |
| `SP/Network/ElectricPoleBodyView.cs` | 本文 ElectricPole（`electric-pole-view`） |
| `SP/Energy/GearSectionView.cs` | 区画 Gear（`gear-section`） |
| `SP/Energy/GearToElectricSectionView.cs` | 区画 GearToElectric（`gear-to-electric-section`） |
| `SP/Energy/ElectricToGearBodyView.cs` | 本文 ElectricToGear（`electric-to-gear-view`） |
| `SP/Belt/BeltThroughputSectionView.cs` | 区画 BeltThroughput の View（`belt-throughput-section`。購読しない） |
| `SP/Belt/BeltThroughputSectionPresenter.cs` | 区画 BeltThroughput の Presenter（詳細と ←/→ を購読し、窓の選択と `IsApplicable` を持つ） |
| `SP/Belt/BeltWindowCursor.cs` | 選んだ窓の tick（既定 180・←/→ で巡回・Detach で既定へ） |
| `SP/FilterSplitter/FilterSplitterBodyView.cs` | 本文 FilterSplitter（`filter-splitter`） |
| `SP/FilterSplitter/FilterDirectionView.cs` | 方向 1 つ（`filter-direction-{d}`） |
| `SP/Space/RocketLaunchPadBodyView.cs` | 本文 RocketLaunchPad（`rocket-launch-pad-view`/`-loading`） |
| `SP/Space/RocketLaunchPadTitleActionView.cs` | 題の操作（`rocket-launch-pad-build`/`-launch`） |
| `SP/Space/SpaceElevatorBodyView.cs` | 本文 SpaceElevatorBase（`space-elevator-view`） |
| `SP/Space/SpaceElevatorSectionView.cs` | 宇宙エレベーターの状態区画（`space-elevator-section`/`-loading`） |
| `SP/SpecialViewParts.cs` | View が共通に使う小さな手続き（文字の行・子の表示の出し入れ〈`UiGapContainer` の項目と、SectionStack の項目としての区画の根には使わない。本文の唯一の子として包む区画にはよい〉・表示中の判定） |
| `SP/Registration/ISpecialDetailRenderer.cs` | View が実装する描画の口 `Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)` |
| `SP/Registration/SpecialDetailPresenter.cs` | alpha の本文・題の操作・区画の 3 つの口を 1 つで実装する Presenter（`Attach` でパネルの枠と詳細の Model を購読し View へ流す。`IsApplicable` を書く） |
| `SP/Registration/SpecialPresenters.cs` | Presenter の組み立て（`Of(view)`・`Belt(view)`。詳細の Model・パネルの源・矢印キーを持つ） |
| `SP/Registration/SpecialViewKit.cs` | View が受ける依存の束（Commands・表示素材・トースト・alpha の格子の工場。購読の源は入れない） |
| `SP/Registration/SpecialBodyFactories.cs` | 本文 5・題の操作 1 の工場（alpha の口の実装） |
| `SP/Registration/SpecialSectionFactories.cs` | 区画 5 の工場（alpha の口の実装） |

入力（E-19・D-7）: `moorestech_client/Assets/Scripts/Client.Input/ArrowKeys/InputSystemArrowKeyStream.cs`（←/→ の押下の縁を流す）と同じディレクトリの `IHorizontalArrowKeySource.cs`（口 `IObservable<HorizontalArrowKey> OnArrowPressed` と `enum HorizontalArrowKey { Left, Right }`。区画のテストは偽物に差し替える）

登録: `moorestech_client/Assets/Scripts/Client.Starter/Registration/GameUi/Screens/BlockInventorySpecialGameUiRegistration.cs`

テスト:

| パス | 層 | 内容 |
|---|---|---|
| `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/Special/JsNumberTextTest.cs` | L0 | JS の数値書式 |
| `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/Special/SpecialStateDetailReaderTest.cs` | L0 | state detail → Detail |
| `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/Special/SpecialNetworkReaderTest.cs` | L0 | 集約 → Detail・停止理由の写し |
| `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/Special/BeltThroughputDetailTest.cs` | L0 | 窓と棒 |
| `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/Special/SpecialBlockCommandsTest.cs` | L0 | 開いたブロックの確かめ・掴み・結果の写し |
| `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/Special/SpecialBlockResponseCodesTest.cs` | L0 | 失敗コード・良性 |
| `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/Special/SpecialInletBindingContributorTest.cs` | L0 | 入れ口・取得待ち（`shiftRejection.spec.ts` 2 件） |
| `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/Special/RecordingSpecialRequestSender.cs`・`FakeOpenSpecialBlock.cs` | 道具 | 送信の記録・開いたブロックの偽物 |
| `moorestech_client/Assets/Scripts/Client.Tests/Input/ArrowKeys/InputSystemArrowKeyStreamTest.cs` | L0 | ←/→ の縁 |
| `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Special/Support/SpecialScreenTestHost.cs` | 道具 | 偽の Model・記録する Commands・表示素材を組み、値の読み取り（ゲージ・棒）を持つ |
| `…/Special/Support/FakeSpecialBlockDetailModel.cs`・`RecordingSpecialBlockCommands.cs`・`RecordingBlockSlotCommands.cs`・`SpecialDetailSamples.cs` | 道具 | 偽の Model・記録する Commands・alpha の格子の押下の記録係・詳細の見本（Web の fixture の値） |
| `…/Special/GearToElectricSectionViewTest.cs` | L1 | Web 7＋i18n 1 |
| `…/Special/GearSectionViewTest.cs` | L1 | Web 3 |
| `…/Special/BeltThroughputSectionViewTest.cs` | L1 | Web 3＋入力中・外れた後 1（矢印は偽の `IHorizontalArrowKeySource` で押す。実キーの縁は `InputSystemArrowKeyStreamTest`） |
| `…/Special/NetworkSectionViewTest.cs` | L1 | 電力網・歯車網（Web の写しなし。区画の表示を固定）と Presenter の通知・Detach |
| `…/Special/ElectricBodyViewTest.cs` | L1 | `electricPole.spec.ts` 1・`electricToGear.spec.ts` 1 |
| `…/Special/FilterSplitterBodyViewTest.cs` | L1 | `filterSplitter.spec.ts` 3 |
| `…/Special/RocketLaunchPadViewTest.cs` | L1 | `RocketLaunchPadInventory.test.tsx` 5 |
| `…/Special/SpecialFactoriesTest.cs` | L1 | 工場の Kind・Slot と根の testid |
| `…/Special/SpaceElevatorViewTest.cs` | L1 | `SpaceElevatorInventory.test.tsx`（宣言 1・ケース 2）・`SpaceElevatorSection.test.tsx`（宣言 5・ケース 11）・`spaceElevator.spec.ts`（宣言 6・ケース 9） |
| `…/Special/SectionStack/SpecialSectionStackSpacingTest.cs` | L1（統合） | alpha の標準の本文に本物の区画 5 つを差し、先頭の区画が後着・消失したときの後続の余白（D-23） |
| `…/Special/SectionStack/SpecialBlockDetailsTest.cs` | L1（統合） | `blockDetails.spec.ts` の歯車とフィルタの面 4 名（ケース 5）・`regression/sectionStack.spec.ts` の特殊族の面 1 名（ケース 9） |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-block-special-panels.cs` | 録画 | 新規（R-12） |

（`…` = `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory`。ディレクトリのファイル数: `SS/Details` 8・`SS/Source` 5・`SS/Commands` 7・`SS/Text` 2・`SS` 直下 2、`SP/*` は各 2〜5、`Client.Tests/Ui/State/BlockInventory/Special` 9、`…/Special` 9＋`Support/` 5＋`SectionStack/` 2、`SP/Registration` 6・`SP/Belt` 3。どれも 10 以下）

### 共有ファイル（共有区画だけ書く）

| パス | 書く所 |
|---|---|
| `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiRegistration.cs` | 画面登録区画の末尾に `BlockInventorySpecialGameUiRegistration.Register(builder);` 1 行と、無ければ `using Client.Starter.Registration.GameUi.Screens;` 1 行 |
| `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss` | `Screens` 区画に `@import url("…/Screens/BlockInventory/Special/BlockInventorySpecial.uss");` 1 行 |
| `moorestech_client/Assets/Scripts/Client.Ui.State/Client.Ui.State.asmdef` | `references` の末尾に `Game.Block`・`Game.Gear`・`Core.BeltTransport`・`Core.Update`（D-8。他レーンが同じ名前を足していれば和を採る） |
| `moorestech_client/Assets/Scripts/Client.Ui.State/Commands/UiCommandRejectReason.cs` | enum と 3 つの switch の末尾に 4 値（D-6） |
| `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/PauseMenuCommandsTest.cs` | 「良性の拒否理由は…」の `benign` の配列に 2 値、テスト名を「…と特殊族の古い合図と掴みの空」まで伸ばす |
| `Localization/localization.csv` | `ui.commandRejected.*` の群の末尾 4 行 |
| `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts` | `npm run gen:i18n` の再生成だけ（手で書かない） |
| `docs/uitk/TODO.md` | 「## 実測の結論」「## 見た目の例外」（`LX-126`〜`LX-130`）の末尾 |
| `docs/uitk/disposition.md` | 「## 処遇」の末尾 1 行（D-13） |
| `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/BlockInventoryDesignTest.cs` | alpha の色の件の `TestCase` に `BlockInventorySpecial.uss` を 1 行（alpha の Produces 一覧の指示） |

### つまみ（Web と同じ名前で上書きするもの）

| つまみ | 当てる所 | 値 | Web の出所 |
|---|---|---|---|
| `--gauge-bar-slot-size` | なし（部品の既定のまま） | 既定 | Web の `GaugeBar` も `--slot-size` 既定 |
| `--panel-action-button-width` | なし（題の右の余白は alpha の外殻が持つ） | 既定 | `GamePanel` の `titleAction` |

このレーンが上書きするつまみは無い（Web の特殊族は部品の既定のまま使う）。並べ比べで差が出たときに足すなら、Web の CSS の同じ名前で `SP/BlockInventorySpecial.uss` のルートのクラスの下に書く。

### 写すテスト（束 4・このレーンの持ち分）

数え方: 「宣言」は `it`/`test`/`it.each` の呼び出しの数（`for` の中の `test` は 1 つ。`<web-test>` のタグと test-name-parity が照らす名前の数）、「ケース」は `it.each` の行と `for` の要素を展開した実行の数（NUnit の `[TestCase]` の数と揃える）。Web の実物（`W/` の下）を開いて数え直した（2026-11-10）。分け合うファイル（`blockDetails`・`regression/sectionStack`）は、このレーンが写す面のケースだけを数える。

| Web のファイル（`W/` からの相対） | 宣言 | ケース | UITK のテスト | 処遇 |
|---|---|---|---|---|
| `e2e/tests/block/electric/electricPole.spec.ts` | 1 | 1 | `ElectricBodyViewTest` | 写す |
| `e2e/tests/block/electric/electricToGear.spec.ts` | 1 | 1 | `ElectricBodyViewTest` | 写す |
| `e2e/tests/block/filterSplitter.spec.ts` | 3 | 3 | `FilterSplitterBodyViewTest` | 写す（3 件目は alpha の外殻のスクロールに載せる。外殻の土台が無ければ部分移植。D-24） |
| `e2e/tests/block/spaceElevator/spaceElevator.spec.ts` | 6 | 9（`for` の状態 3・出口 2 を展開） | `SpaceElevatorViewTest` | 写す（身振り→絶対添字まで。持ち物の移動の結果は alpha・録画） |
| `e2e/tests/block/spaceElevator/shiftRejection.spec.ts` | 2 | 2 | `SpecialInletBindingContributorTest` | 写す（判断まで。送らない・通知は alpha の Shift の実行器） |
| `e2e/tests/block/blockDetails.spec.ts`（S4-machine と分け合う。S4-machine 計画 44a0bb0334 の対応表で S4-special に回した面） | 4（`renders ${type} detail section`・「歯車機械は消費トルクと基準付きRPMを赤なしで出し、網の需給行を持つ」・「歯車発電機は発生トルクと現在RPMだけを出す」・「歯車ポンプは電力行を持たず歯車行を出す」） | 5（`renders ${type}` は gearMachine・filterSplitter の 2。他は各 1。歯車ポンプは歯車行の面だけ） | `SpecialBlockDetailsTest` | 写す（`renders ${type}`・歯車ポンプの 2 名は S4-machine も同じタグで自分の面を写す） |
| `e2e/tests/regression/sectionStack.spec.ts`（alpha の持ち主表で「machine・special（区画の id）＋special（FilterSplitter）」と割り振られた面） | 1（`${entry.type}は対応セクションだけを表示する`） | 9（filterSplitter 1 と、特殊族の区画の id〈`gear-section`・`gear-network-section`・`electric-network-section`〉が出る 8 種: gearMachine・gearMiner・gearPump・gearGenerator・machine・miner・generator・pump） | `SpecialBlockDetailsTest` | 写す（同じ 12 ケースのうち、項目グリッドの id は alpha、機械・採掘機・ポンプ・発電機の区画の id は S4-machine が同じタグで写す。chest・tank・generic は特殊族の区画が出ないので持ち分に無い） |
| `src/features/blockInventory/details/__tests__/GearToElectricSection.test.ts` | 7 | 13（`it.each` の 7 行を展開） | `GearToElectricSectionViewTest` | 写す |
| `src/features/blockInventory/details/__tests__/GearToElectricSection.i18n.test.ts` | 1 | 1 | `GearToElectricSectionViewTest` | 写す（D-14） |
| `src/features/blockInventory/details/__tests__/BeltThroughputSection.test.ts` | 3 | 3 | `BeltThroughputSectionViewTest` | 写す |
| `src/features/blockInventory/details/__tests__/GearSection.test.ts` | 3 | 3 | `GearSectionViewTest` | 写す |
| `src/features/blockInventory/views/__tests__/RocketLaunchPadInventory.test.tsx` | 6 | 7（`it.each` の 2 行を展開） | `RocketLaunchPadViewTest`（宣言 5・ケース 6） | 写す 6 ケース・捨てる 1（"accepts panel data and narrows inside the dedicated view"。TS の型検査。D-13） |
| `src/features/blockInventory/details/spaceElevator/__tests__/SpaceElevatorInventory.test.tsx` | 1 | 2（`it.each([false, true])`） | `SpaceElevatorViewTest` | 写す |
| `src/features/blockInventory/details/spaceElevator/__tests__/SpaceElevatorSection.test.tsx` | 5 | 11（`it.each` の 7 行を展開） | `SpaceElevatorViewTest` | 写す |

計 14 ファイル・宣言 44（写す 43・捨てる 1）・ケース 70（写す 69・捨てる 1）。実行計画 §1.4 の数（`spaceElevator/*`(8)・GearToElectric 7・RocketLaunchPad 5・SpaceElevator 5）は宣言で数えてあり、`it.each`・`for` の展開とは食い違わない。食い違いは 5 つ: RocketLaunchPad 5 は捨てる 1 を除いた宣言、SpaceElevator 5 は `SpaceElevatorSection` だけで `SpaceElevatorInventory` の 1 宣言を含まない、`GearToElectricSection.i18n` の 1 宣言が無い、他レーンの表から回ってきた `blockDetails` の 4 名と `regression/sectionStack` の 1 名が無い。§1.4 の行は宣言とケースの 2 つの数で書き直す（末尾「設計書・実行計画へ反映する行」）。完了の判定は、名前の照合（test-name-parity）を宣言で、実行（`uloop run-tests` の件数）をケースで見る（Task 12 Step 2・3）

---

## Tasks

### Task 1: 前提の確認・alpha の拡張口の照合・記録の準備

**Files:**
- Create: `../moorestech_logs/harness/uitk/S4-special/done.md`（雛形。moorestech_logs 側）

**Interfaces:**
- Consumes: `origin/feature/uitk`（S4-alpha・C4-b・C3-b・L0-s4b・C4-e マージ済み）、Global Constraints の「使う部品と公開 API」「S4-alpha の API」
- Produces: bd のレーン ID（以下 `<bd>`）、`done.md` の雛形、alpha の API の読み替え表（違ったときだけ）、分岐 A/B の結果（Shift の入れ口の口）

- [ ] **Step 1: 作業場所と既存の部品の実物を確かめる**

Run:
```bash
pwd
git log -1 --format='%H %s' origin/feature/uitk
S=moorestech_client/Assets/Scripts
grep -n "public ModeSwitch(\|public IObservable<TValue> OnOptionSelected\|public void SetSelected(" $S/Client.Ui/Components/Controls/ModeSwitch/ModeSwitch.cs
grep -n "public PanelActionButton(\|OnClicked" $S/Client.Ui/Components/Controls/PanelActionButton/PanelActionButton.cs
grep -n "public GaugeBar(\|public void SetValue(" $S/Client.Ui/Components/Gauges/GaugeBar/GaugeBar.cs
grep -n "public BarSeries(\|public void SetValues(" $S/Client.Ui/Components/Gauges/BarSeries/BarSeries.cs
grep -n "public ItemSlot(\|OnSlotGestured\|public void SetContent(" $S/Client.Ui/Components/Slots/ItemSlot/ItemSlot.cs
grep -rn "public static void Mark(\|public static bool TryFindMarked(" $S/Client.Ui/Foundation/Tooltip/
grep -n "public static BlockInventoryComponents Resolve" $S/Client.Ui.State/BlockInventory/Logic/Panel/BlockComponentRegistry.cs
grep -n "public static .*TryGetCommandButton\|public static LocalizationKey StatusKey" $S/Client.Ui.State/BlockInventory/Logic/Special/RocketLaunchPadLogic.cs
grep -n "public static .* RecentSecondBars\|public static int CycleBeltWindow\|public static bool TrySelectBeltWindow\|public static BeltThroughputRowDisplay RowDisplay" $S/Client.Ui.State/BlockInventory/Logic/Belt/BeltThroughputLogic.cs
grep -n "public IReadOnlyReactiveProperty<SlotItem> Current" $S/Client.Ui.State/Inventory/Grab/PlayerGrabModel.cs
grep -rn "BeltTransportClientCache" $S/Client.Starter | head -3
grep -n "section: Screens" "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss"
ls $S/Client.Starter/Registration/GameUi/Screens/*.cs | wc -l
```
Expected: worktree の絶対パス。部品の 5 行・`HoverTooltipMark` の 2 行・L0-s4b の 4 行・`PlayerGrabModel.Current` が Global Constraints のとおり出る。`BeltTransportClientCache` が両系の登録（`MainGameModelRegistration` か同等）に 1 行ある。`/* == section: Screens == */` が出る。
- どれかのファイルが無ければ、その依存が未取り込みなので止め、`done.md` を「状態: 停止（<依存> 未取り込み）」にして指揮へ報告する
- `BeltTransportClientCache` が GameUi の系で登録されていなければ止めて指揮へ（源は両系の登録の持ち物で、このレーンは書かない）
- 登録の置き場は `Registration/GameUi/Screens/`（W4 以降の画面の登録。E-1 の追記 ad92a44777）。10 ファイルの関門もこのディレクトリで数える。`Registration/GameUi/Screens/*.cs` が 10 本以上（alpha・他の W4 レーンの登録を含む）なら、このレーンの 1 本で 11 本以上になる。止めずに作業を進め、`done.md` の「integrator への依頼」に「`Registration/GameUi/Screens/` の分け方」を書く。再編が `origin/feature/uitk` に入り、10 本以下の置き場へ足せるまで Task 14 のマージをしない（AGENTS.md の 1 ディレクトリ 10 ファイル。D-20）。`Screens/` がまだ無ければこのレーンが作る（`.meta` は Unity が作る）。再編後は File Structure と Task 9 の登録のパスを読み替え、読み替えたことを `done.md` に書く

- [ ] **Step 2: S4-alpha の拡張口を実物と照合する**

Run:
```bash
S=moorestech_client/Assets/Scripts
ls $S/Client.Ui/Screens/BlockInventory $S/Client.Ui/Screens/BlockInventory/Registry $S/Client.Ui/Screens/BlockInventory/SectionStack $S/Client.Ui/Screens/BlockInventory/Parts $S/Client.Ui.State/BlockInventory/Panel $S/Client.Ui.State/BlockInventory/SlotRestriction
grep -rn "interface IBlockInventoryPart\|interface IBlockInventoryBodyFactory\|interface IBlockInventoryTitleActionFactory\|class BlockInventoryBodyMount" $S/Client.Ui/Screens/BlockInventory/Registry/
grep -rn "enum SectionStackSlot\|interface IBlockInventorySection\|interface ISectionStackSectionFactory\|class SectionStackSectionMount" $S/Client.Ui/Screens/BlockInventory/SectionStack/
grep -n "public .*{ get; }\|IsSamePanel" $S/Client.Ui.State/BlockInventory/Panel/BlockInventoryPanelState.cs
grep -rn "interface IBlockSlotRestrictionContributor\|WithInletBindings" $S/Client.Ui.State/BlockInventory/ | head
grep -rn "public LackHighlightText(\|public void Render(\|InsufficientClassName\|public BlockItemGridGroup(\|public BlockItemGrid Create(" $S/Client.Ui/Screens/BlockInventory/Parts/
grep -rn "IReadOnlyReactiveProperty<bool> IsApplicable\|void Attach(OpenBlockInventoryPanel" $S/Client.Ui/Screens/BlockInventory/
grep -rn "IsApplicable" $S/Client.Ui/Screens/BlockInventory/SectionStack/SectionStackBody.cs
grep -rn "interface IBlockInventoryPanelSource\|BlockInstanceId BlockInstanceId" $S/Client.Ui.State/BlockInventory/Panel/
ls $S/Client.Tests/Ui/Screens/BlockInventory/ $S/Client.Tests/Ui/State/BlockInventory/PanelModel 2>/dev/null
grep -n "BlockInventoryGameUiRegistration" $S/Client.Starter/Registration/MainGameUiRegistration.cs
```
Expected: Global Constraints の「S4-alpha の API」の口が出る。読み替えの規則:
- **名前だけ違う**: 以後の全タスクを実物の名前で読む。`done.md` の「未確認」に「API 名の読み替え: <計画の名前> → <実物>」を 1 行ずつ書く
- **形が違う**（例: `Attach` の引数が `OpenBlockInventoryPanel` でない、`Detach` が無い）: Presenter と工場（`SP/Registration/SpecialDetailPresenter.cs`・`SpecialPresenters.cs`・`SpecialBodyFactories.cs`・`SpecialSectionFactories.cs`・`SP/Belt/BeltThroughputSectionPresenter.cs`）だけを実物の形に合わせる（View と State は変えない。View は `ISpecialDetailRenderer` しか知らない）。合わせ方を `done.md` の「未確認」に書く
- **口そのものが無い**（拡張口 (1)〜(3) のどれか）: 止めて指揮へ（alpha の所有物で、このレーンは書かない）。`done.md` を「状態: 停止（S4-alpha の拡張口 <n> が無い）」にする
- **分岐 A（拡張口 (6) `IBlockSlotRestrictionContributor` がある。alpha の計画どおり）**: Task 3 の `SpecialInletBindingContributor` をその口に実装し、Task 9 で DI に登録する
- **分岐 B（拡張口 (6) が無い）**: Task 3 の `SpecialInletBindingContributor` から `: IBlockSlotRestrictionContributor` と `AppliesTo`・`Contribute` を外し、静的な `TryGetInletBindings`・`SpaceElevatorBindings` だけの `static class` にして、Task 9 では登録しない。`done.md` の「integrator への依頼」に「S4-alpha の Shift の束縛へ発射台・宇宙エレベーターの入れ口を寄与する口が無い。実装は `Client.Ui.State/BlockInventory/Special/Source/SpecialInletBindingContributor.cs` にある。それまで両ブロックの Shift は取得待ち（`BlockDetailLoading`）のまま」と書く。R-9 の受け入れ基準は L0（判断まで）で満たし、死活表の行を「alpha の口の追加後に生きる」と読む（D-17）
- **区画の出し入れの通知**: `IBlockInventorySection.IsApplicable` が `IReadOnlyReactiveProperty<bool>` で、`SectionStackBody.cs` がそれを購読して `SetItemShown` していること（上の 2 つの grep）。違えば（bool のメソッドのまま・購読していない）止めて指揮へ。`done.md` を「状態: 停止（S4-alpha の区画の出し入れの通知が無い）」にする。区画が自分の `display` を切り替える逃げ道は作らない（`UiGapContainer` の先頭の間隔がずれる。D-23）
- **パネルの識別**: `OpenBlockInventoryPanel` が `BlockInstanceId`・`Position` を持ち `Block`（`BlockGameObject`）を持たないこと。`BlockInstanceId` が無ければ止めて指揮へ（詳細の Model・操作・Shift の寄与が開いたブロックを照らす鍵。D-26）。あわせて、開いたまま同じ位置でブロックが置き換わったとき alpha のパネルが新しい `BlockInstanceId` で再発行するか（`IsSamePanel` が位置・Guid・エラーしか見ないなら再発行されない）を読み、再発行しないなら `done.md` の「integrator への依頼」に「置き換え時にパネルの `BlockInstanceId` を更新して再発行してほしい（しないと特殊族の詳細が取得待ちのまま。`SpecialBlockDetailModel` の警告で気づく）」と書く
- L1 の土台（`BlockInventoryTestPanels.OfType`・`BlockInventoryTestBodies`）の名前を控える。無ければ Task 7・Task 8 の「外殻の土台が無いとき」の分岐（部分移植）を使う

- [ ] **Step 3: bd を積み、done.md の雛形を置く**

Run:
```bash
bd create "UITK S4-special: ブロックインベントリの特殊族（分配器・回転生成機・回転発電機・電柱・歯車・ベルトの流量・ロケット・宇宙エレベーター）を UITK へ" --parent moorestech-albof -t task -p 2
bd update <bd> --claim
mkdir -p ../moorestech_logs/harness/uitk/S4-special
```
`done.md` に次を書く（1 行目は終わるまで「状態: 作業中」。`../moorestech_logs/README.md` を先に読む）:
```markdown
状態: 作業中

# S4-special 作業報告

## 証拠
## 差の表
## 要裁定
## integrator への依頼
## 未確認
## 部分移植の残り
```
Expected: bd の ID が出る。以後 `<bd>` をその ID で読む。Step 1・2 で書いた依頼・読み替えを `bd note <bd> "<同じ文>"` にも残す

---

### Task 2: 詳細の型と文（`Client.Ui.State/BlockInventory/Special/Details`・`Text`）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Ui.State/Client.Ui.State.asmdef`（`references` の末尾に 4 つ）
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special/Details/SpecialBlockDetailViewModel.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special/Details/ElectricToGearDetail.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special/Details/GearToElectricDetail.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special/Details/BeltThroughputDetail.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special/Details/NetworkDetails.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special/Details/FilterSplitterDetail.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special/Details/RocketLaunchPadDetail.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special/Details/SpaceElevatorDetail.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special/Text/JsNumberText.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special/Text/SpecialDetailTexts.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/Special/JsNumberTextTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/Special/BeltThroughputDetailTest.cs`

**Interfaces:**
- Consumes: L0-s4b の `GearDetail`・`GearToElectricDisplayState`・`GearNetworkStopReasonKind`・`BeltThroughputWindow`・`BeltThroughputLogic`・`FilterSplitterMode`（client）、サーバーの `RocketLaunchPadPhase`・`RocketLaunchPadWaitReason`・`SpaceElevatorBaseStatus`（`Game.Block.Interface.State`）、`UiNumberFormat`
- Produces（後段の全タスクが使う。名前と形はこのとおり）:
```csharp
namespace Client.Ui.State.BlockInventory.Special.Details
{
    public sealed class SpecialBlockDetailViewModel
    {
        public static readonly SpecialBlockDetailViewModel Empty;
        public SpecialBlockDetailViewModel(ElectricToGearDetail electricToGear, GearToElectricDetail gearToElectric, GearDetail gear, BeltThroughputDetail belt, ElectricNetworkDetail electricNetwork, GearNetworkDetail gearNetwork, FilterSplitterDetail filterSplitter, RocketLaunchPadDetail rocketLaunchPad, SpaceElevatorDetail spaceElevator); // 不在は null で渡す（読み手が TryGet* で分ける）
        public bool TryGetElectricToGear(out ElectricToGearDetail detail); // 同形: TryGetGearToElectric・TryGetGear(out GearDetail)・TryGetBelt・TryGetElectricNetwork・TryGetGearNetwork・TryGetFilterSplitter・TryGetRocketLaunchPad・TryGetSpaceElevator
    }
    public readonly struct ElectricToGearOutputMode { public ElectricToGearOutputMode(double rpm, double torque, double requiredPower); public double Rpm { get; } public double Torque { get; } public double RequiredPower { get; } }
    public sealed class ElectricToGearDetail { public ElectricToGearDetail(IReadOnlyList<ElectricToGearOutputMode> outputModes, int selectedIndex, double fulfillmentRate, double consumedElectricPower); /* 同名の get-only */ public bool HasSameModes(ElectricToGearDetail other); }
    public sealed class GearToElectricDetail { public GearToElectricDetail(GearToElectricDisplayState state, double suppliedPower, double maxGeneratedPower); /* 同名の get-only */ }
    public sealed class BeltThroughputDetail { public BeltThroughputDetail(IReadOnlyList<BeltThroughputWindow> windows, IReadOnlyList<int> recentSecondCounts, double maxCountPerSecond); public IReadOnlyList<BeltThroughputWindow> Windows { get; } public IReadOnlyList<float?> RecentSecondBars(); }
    public sealed class ElectricNetworkDetail { public ElectricNetworkDetail(double totalGeneratePower, double totalRequiredPower, int consumerCount, double powerRate); /* 同名の get-only */ }
    public sealed class GearNetworkDetail { public GearNetworkDetail(double totalGenerateGearPower, double totalRequiredGearPower, GearNetworkStopReasonKind stopReason); /* 同名の get-only */ }
    public readonly struct FilterSplitterDirectionDetail { public FilterSplitterDirectionDetail(FilterSplitterMode mode, IReadOnlyList<ItemId> filterItemIds); public FilterSplitterMode Mode { get; } public IReadOnlyList<ItemId> FilterItemIds { get; } }
    public sealed class FilterSplitterDetail { public FilterSplitterDetail(IReadOnlyList<FilterSplitterDirectionDetail> directions); public IReadOnlyList<FilterSplitterDirectionDetail> Directions { get; } }
    // サーバーの列挙（RocketLaunchPadPhase・SpaceElevatorBaseStatus 等）は internal にし、Screens へは文言キー・判定だけを出す（Client.Ui は Game.Block.Interface を参照しない。D-27）
    public sealed class RocketLaunchPadDetail { public RocketLaunchPadDetail(RocketLaunchPadPhase phase, RocketLaunchPadWaitReason waitReason, float progress, IReadOnlyList<int> materialSlots, IReadOnlyList<int> payloadSlots, int outputSlotStart); internal RocketLaunchPadPhase Phase { get; } internal RocketLaunchPadWaitReason WaitReason { get; } public float Progress { get; } public IReadOnlyList<int> MaterialSlots { get; } public IReadOnlyList<int> PayloadSlots { get; } public int OutputSlotStart { get; } public LocalizationKey StatusKey { get; } public bool TryGetCommandButton(out RocketCommandButton button); public IReadOnlyList<int> OutputSlots(int itemSlotCount); }
    public sealed class SpaceElevatorDetail { public SpaceElevatorDetail(IReadOnlyList<int> satelliteInputSlots, IReadOnlyList<int> outputSlots, bool connected, int cablePoolAmount, int cablesPerElevator, float climbProgress, SpaceElevatorBaseStatus status); /* 同名の get-only。ただし Status は internal */ public SpaceElevatorStatusDisplay StatusDisplay { get; } public bool CanConnect { get; } }
}
namespace Client.Ui.State.BlockInventory.Special.Text
{
    public static class JsNumberText { public static string Fixed0(double value); public static string Precision2(double value); public static string Number(double value); public static string PositivePower(double power); }
    public static class SpecialDetailTexts
    {
        public static UiText ElectricNetworkSummary(ElectricNetworkDetail network);
        public static UiText GearNetworkSummary(GearNetworkDetail network);
        public static UiText GearToElectricSuppliedPower(GearToElectricDetail detail);
        public static UiText ElectricToGearOutputMode(ElectricToGearOutputMode mode);
        public static UiText ElectricToGearConsumedPower(ElectricToGearDetail detail);
        public static UiText OutputNumber(int directionIndex);
        public static UiText SpaceElevatorPool(SpaceElevatorDetail detail);
    }
}
```

- [ ] **Step 1: asmdef に 4 つを足す**

`Client.Ui.State.asmdef` の `references` の `"VContainer"` の後に `"Game.Block", "Game.Gear", "Core.BeltTransport", "Core.Update"` を足す（Web の橋 `Client.WebUiHost.asmdef` が同じ源を読むために持つ参照と同じ名前。D-8）。他レーンが先に同じ名前を足していれば重ねない

- [ ] **Step 2: テストを書く**

`JsNumberTextTest.cs`:
```csharp
using Client.Ui.State.BlockInventory.Special.Text;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// JS の数値書式（toFixed(0)・toPrecision(2)・String(number)）を Web と同じ文字にする
    /// Formats numbers exactly like JS toFixed(0), toPrecision(2) and String(number)
    /// </summary>
    public class JsNumberTextTest
    {
        [TestCase(0d, "0")]
        [TestCase(0.1d, "0.10")]
        [TestCase(0.49d, "0.49")]
        [TestCase(0.5d, "1")]
        [TestCase(29.49999d, "29")]
        [TestCase(29.5d, "30")]
        [TestCase(1e-8d, "1.0e-8")]
        [TestCase(0.2d, "0.20")]
        [TestCase(100d, "100")]
        public void 正の微小値は有効2桁で残し他は整数に丸める(double power, string expected)
        {
            Assert.AreEqual(expected, JsNumberText.PositivePower(power));
        }

        [TestCase(0.000123d, "0.00012")]
        [TestCase(0.0000012d, "0.0000012")]
        [TestCase(1.5e-7d, "1.5e-7")]
        [TestCase(0.095d, "0.095")]
        [TestCase(150d, "1.5e+2")]
        public void 有効2桁はJSのtoPrecisionと同じ表記(double value, string expected)
        {
            Assert.AreEqual(expected, JsNumberText.Precision2(value));
        }

        [TestCase(10d, "10")]
        [TestCase(2.5d, "2.5")]
        [TestCase(0.1d, "0.1")]
        public void 素の数値はJSのStringと同じ表記(double value, string expected)
        {
            Assert.AreEqual(expected, JsNumberText.Number(value));
        }
    }
}
```

`BeltThroughputDetailTest.cs`:
```csharp
using Client.Ui.State.BlockInventory.Logic.Belt;
using Client.Ui.State.BlockInventory.Special.Details;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// ベルトの詳細が直近の秒ごとの搬出を 9 本の棒（足りない古い側は空）にする
    /// The belt detail turns recent per-second exits into nine bars, padding the older missing side with empties
    /// </summary>
    public class BeltThroughputDetailTest
    {
        [Test]
        public void 直近3秒しかなければ古い6本を空にして秒あたり最大で割る()
        {
            var detail = new BeltThroughputDetail(new[] { BeltThroughputWindow.Measured(180, 9, 9) }, new[] { 1, 0, 1 }, 1);
            CollectionAssert.AreEqual(new float?[] { null, null, null, null, null, null, 1f, 0f, 1f }, detail.RecentSecondBars());
        }
    }
}
```

- [ ] **Step 3: 実装を書く**

`Details/ElectricToGearDetail.cs`:
```csharp
using System.Collections.Generic;

namespace Client.Ui.State.BlockInventory.Special.Details
{
    // 回転生成機の出力モード 1 つ（マスタの順が添字の意味）
    // One ElectricToGear output mode; master order is the meaning of the index
    public readonly struct ElectricToGearOutputMode
    {
        public double Rpm { get; }
        public double Torque { get; }
        public double RequiredPower { get; }

        public ElectricToGearOutputMode(double rpm, double torque, double requiredPower)
        {
            Rpm = rpm;
            Torque = torque;
            RequiredPower = requiredPower;
        }
    }

    /// <summary>
    /// 回転生成機の詳細（Web ElectricToGearDetailDto と同じ値）
    /// ElectricToGear generator detail (same values as Web ElectricToGearDetailDto)
    /// </summary>
    public sealed class ElectricToGearDetail
    {
        public IReadOnlyList<ElectricToGearOutputMode> OutputModes { get; }
        public int SelectedIndex { get; }
        public double FulfillmentRate { get; }
        public double ConsumedElectricPower { get; }

        public ElectricToGearDetail(IReadOnlyList<ElectricToGearOutputMode> outputModes, int selectedIndex, double fulfillmentRate, double consumedElectricPower)
        {
            OutputModes = outputModes;
            SelectedIndex = selectedIndex;
            FulfillmentRate = fulfillmentRate;
            ConsumedElectricPower = consumedElectricPower;
        }

        // 選択肢の作り直しが要るか（モードの数と値が同じなら選択の差し替えだけで済む）
        // Whether the options must be rebuilt; same count and values only need a selection update
        public bool HasSameModes(ElectricToGearDetail other)
        {
            if (other.OutputModes.Count != OutputModes.Count) return false;
            for (var i = 0; i < OutputModes.Count; i++)
            {
                if (!OutputModes[i].Equals(other.OutputModes[i])) return false;
            }
            return true;
        }
    }
}
```

`Details/GearToElectricDetail.cs`:
```csharp
using Client.Ui.State.BlockInventory.Logic.Detail;

namespace Client.Ui.State.BlockInventory.Special.Details
{
    /// <summary>
    /// 回転発電機の詳細（発電状態・供給電力・マスタの最大）
    /// GearToElectric generator detail: generation state, supplied power and the master maximum
    /// </summary>
    public sealed class GearToElectricDetail
    {
        public GearToElectricDisplayState State { get; }
        public double SuppliedPower { get; }
        public double MaxGeneratedPower { get; }

        public GearToElectricDetail(GearToElectricDisplayState state, double suppliedPower, double maxGeneratedPower)
        {
            State = state;
            SuppliedPower = suppliedPower;
            MaxGeneratedPower = maxGeneratedPower;
        }
    }
}
```

`Details/BeltThroughputDetail.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Belt;

namespace Client.Ui.State.BlockInventory.Special.Details
{
    /// <summary>
    /// ベルトの搬出統計（窓ごとの数と直近の秒ごとの数。Web BeltThroughputDetailDto と同じ値）
    /// Belt exit statistics: per-window counts and recent per-second counts (same values as Web BeltThroughputDetailDto)
    /// </summary>
    public sealed class BeltThroughputDetail
    {
        public IReadOnlyList<BeltThroughputWindow> Windows { get; }
        public IReadOnlyList<int> RecentSecondCounts { get; }
        public double MaxCountPerSecond { get; }

        public BeltThroughputDetail(IReadOnlyList<BeltThroughputWindow> windows, IReadOnlyList<int> recentSecondCounts, double maxCountPerSecond)
        {
            Windows = windows;
            RecentSecondCounts = recentSecondCounts;
            MaxCountPerSecond = maxCountPerSecond;
        }

        // 棒の高さ（古い順。足りない古い側は null で空の棒）
        // Bar heights, oldest first; the missing older side is null for empty bars
        public IReadOnlyList<float?> RecentSecondBars()
        {
            var ratios = BeltThroughputLogic.RecentSecondBars(RecentSecondCounts, MaxCountPerSecond, BeltThroughputLogic.RecentSecondBarCount);
            var bars = new float?[ratios.Length];
            for (var i = 0; i < ratios.Length; i++) bars[i] = ratios[i].HasValue ? (float)ratios[i].Value : null;
            return bars;
        }
    }
}
```

`Details/NetworkDetails.cs`:
```csharp
using Client.Ui.State.BlockInventory.Logic.Detail;

namespace Client.Ui.State.BlockInventory.Special.Details
{
    // 電力網の集約（発電・需要・消費者数・供給率）
    // Electric network aggregate: generation, demand, consumer count and supply rate
    public sealed class ElectricNetworkDetail
    {
        public double TotalGeneratePower { get; }
        public double TotalRequiredPower { get; }
        public int ConsumerCount { get; }
        public double PowerRate { get; }

        public ElectricNetworkDetail(double totalGeneratePower, double totalRequiredPower, int consumerCount, double powerRate)
        {
            TotalGeneratePower = totalGeneratePower;
            TotalRequiredPower = totalRequiredPower;
            ConsumerCount = consumerCount;
            PowerRate = powerRate;
        }
    }

    // 歯車網の集約（供給・要求・停止理由）
    // Gear network aggregate: supply, requirement and stop reason
    public sealed class GearNetworkDetail
    {
        public double TotalGenerateGearPower { get; }
        public double TotalRequiredGearPower { get; }
        public GearNetworkStopReasonKind StopReason { get; }

        public GearNetworkDetail(double totalGenerateGearPower, double totalRequiredGearPower, GearNetworkStopReasonKind stopReason)
        {
            TotalGenerateGearPower = totalGenerateGearPower;
            TotalRequiredGearPower = totalRequiredGearPower;
            StopReason = stopReason;
        }
    }
}
```

`Details/FilterSplitterDetail.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Special;
using Core.Master;

namespace Client.Ui.State.BlockInventory.Special.Details
{
    // 方向 1 つのモードとフィルタ枠（空の枠は EmptyItemId）
    // One direction's mode and filter slots; an empty slot is EmptyItemId
    public readonly struct FilterSplitterDirectionDetail
    {
        public FilterSplitterMode Mode { get; }
        public IReadOnlyList<ItemId> FilterItemIds { get; }

        public FilterSplitterDirectionDetail(FilterSplitterMode mode, IReadOnlyList<ItemId> filterItemIds)
        {
            Mode = mode;
            FilterItemIds = filterItemIds;
        }
    }

    /// <summary>
    /// フィルタ分配器の方向ごとの設定（Web FilterSplitterDto と同じ並び）
    /// Filter splitter per-direction settings, in the same order as Web FilterSplitterDto
    /// </summary>
    public sealed class FilterSplitterDetail
    {
        public IReadOnlyList<FilterSplitterDirectionDetail> Directions { get; }

        public FilterSplitterDetail(IReadOnlyList<FilterSplitterDirectionDetail> directions)
        {
            Directions = directions;
        }
    }
}
```

`Details/RocketLaunchPadDetail.cs`:
```csharp
using System.Collections.Generic;
using Client.Localization;
using Client.Ui.State.BlockInventory.Logic.Special;
using Game.Block.Interface.State;

namespace Client.Ui.State.BlockInventory.Special.Details
{
    /// <summary>
    /// 発射台の詳細。資材・積荷の枠はサーバーの絶対添字、出力は先頭から在庫の末尾まで（Web RocketLaunchPadDetailDtoBuilder と同じ）
    /// Launch pad detail; material and payload slots are absolute server indices, outputs run from the start to the inventory end (same as the Web builder)
    /// </summary>
    public sealed class RocketLaunchPadDetail
    {
        internal RocketLaunchPadPhase Phase { get; }
        internal RocketLaunchPadWaitReason WaitReason { get; }
        public float Progress { get; }
        public IReadOnlyList<int> MaterialSlots { get; }
        public IReadOnlyList<int> PayloadSlots { get; }
        public int OutputSlotStart { get; }

        public RocketLaunchPadDetail(RocketLaunchPadPhase phase, RocketLaunchPadWaitReason waitReason, float progress, IReadOnlyList<int> materialSlots, IReadOnlyList<int> payloadSlots, int outputSlotStart)
        {
            Phase = phase;
            WaitReason = waitReason;
            Progress = progress;
            MaterialSlots = materialSlots;
            PayloadSlots = payloadSlots;
            OutputSlotStart = outputSlotStart;
        }

        // 状態文と題の操作は State で引き、Screens へサーバーの列挙を出さない（D-27）
        // Resolve the status text and title action in State so Screens never sees the server enums (D-27)
        public LocalizationKey StatusKey => RocketLaunchPadLogic.StatusKey(Phase, WaitReason);

        public bool TryGetCommandButton(out RocketCommandButton button)
        {
            return RocketLaunchPadLogic.TryGetCommandButton(Phase, out button);
        }

        // 出力の枠（在庫の数が先頭に届かなければ空）
        // Output slots; empty when the inventory does not reach the start
        public IReadOnlyList<int> OutputSlots(int itemSlotCount)
        {
            var slots = new List<int>();
            for (var slot = OutputSlotStart; slot < itemSlotCount; slot++) slots.Add(slot);
            return slots;
        }
    }
}
```

`Details/SpaceElevatorDetail.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Special;
using Game.Block.Interface.State;

namespace Client.Ui.State.BlockInventory.Special.Details
{
    /// <summary>
    /// 宇宙エレベーターの詳細。入口・出口は配られた順のままサーバーの絶対添字（Web SpaceElevatorDetailDto と同じ）
    /// Space elevator detail; inlet and outlets keep the published order as absolute server indices (same as Web SpaceElevatorDetailDto)
    /// </summary>
    public sealed class SpaceElevatorDetail
    {
        public IReadOnlyList<int> SatelliteInputSlots { get; }
        public IReadOnlyList<int> OutputSlots { get; }
        public bool Connected { get; }
        public int CablePoolAmount { get; }
        public int CablesPerElevator { get; }
        public float ClimbProgress { get; }
        internal SpaceElevatorBaseStatus Status { get; }

        // 状態の文言と不足色・接続できるかは State で決める（Screens へサーバーの列挙を出さない。D-27）
        // Status text, shortage tone and connectability are decided in State (no server enums reach Screens. D-27)
        public SpaceElevatorStatusDisplay StatusDisplay => SpaceElevatorLogic.StatusDisplay(Status);
        public bool CanConnect => Status == SpaceElevatorBaseStatus.ReadyToConnect;

        public SpaceElevatorDetail(IReadOnlyList<int> satelliteInputSlots, IReadOnlyList<int> outputSlots, bool connected, int cablePoolAmount, int cablesPerElevator, float climbProgress, SpaceElevatorBaseStatus status)
        {
            SatelliteInputSlots = satelliteInputSlots;
            OutputSlots = outputSlots;
            Connected = connected;
            CablePoolAmount = cablePoolAmount;
            CablesPerElevator = cablesPerElevator;
            ClimbProgress = climbProgress;
            Status = status;
        }
    }
}
```

`Details/SpecialBlockDetailViewModel.cs`:
```csharp
using Client.Ui.State.BlockInventory.Logic.Detail;

namespace Client.Ui.State.BlockInventory.Special.Details
{
    /// <summary>
    /// 開いたブロックの特殊族の詳細。どれも届くまで不在（Web の DTO のキー省略と同じ）。読み手は TryGet* で在否を分ける
    /// Special-family details of the open block; each is absent until it arrives (like the omitted Web DTO keys); readers split presence with TryGet*
    /// </summary>
    public sealed class SpecialBlockDetailViewModel
    {
        public static readonly SpecialBlockDetailViewModel Empty = new(null, null, null, null, null, null, null, null, null);

        private readonly ElectricToGearDetail _electricToGear;
        private readonly GearToElectricDetail _gearToElectric;
        private readonly GearDetail _gear;
        private readonly BeltThroughputDetail _belt;
        private readonly ElectricNetworkDetail _electricNetwork;
        private readonly GearNetworkDetail _gearNetwork;
        private readonly FilterSplitterDetail _filterSplitter;
        private readonly RocketLaunchPadDetail _rocketLaunchPad;
        private readonly SpaceElevatorDetail _spaceElevator;

        public SpecialBlockDetailViewModel(ElectricToGearDetail electricToGear, GearToElectricDetail gearToElectric, GearDetail gear, BeltThroughputDetail belt, ElectricNetworkDetail electricNetwork, GearNetworkDetail gearNetwork, FilterSplitterDetail filterSplitter, RocketLaunchPadDetail rocketLaunchPad, SpaceElevatorDetail spaceElevator)
        {
            _electricToGear = electricToGear;
            _gearToElectric = gearToElectric;
            _gear = gear;
            _belt = belt;
            _electricNetwork = electricNetwork;
            _gearNetwork = gearNetwork;
            _filterSplitter = filterSplitter;
            _rocketLaunchPad = rocketLaunchPad;
            _spaceElevator = spaceElevator;
        }

        public bool TryGetElectricToGear(out ElectricToGearDetail detail) => Present(_electricToGear, out detail);
        public bool TryGetGearToElectric(out GearToElectricDetail detail) => Present(_gearToElectric, out detail);
        public bool TryGetGear(out GearDetail detail) => Present(_gear, out detail);
        public bool TryGetBelt(out BeltThroughputDetail detail) => Present(_belt, out detail);
        public bool TryGetElectricNetwork(out ElectricNetworkDetail detail) => Present(_electricNetwork, out detail);
        public bool TryGetGearNetwork(out GearNetworkDetail detail) => Present(_gearNetwork, out detail);
        public bool TryGetFilterSplitter(out FilterSplitterDetail detail) => Present(_filterSplitter, out detail);
        public bool TryGetRocketLaunchPad(out RocketLaunchPadDetail detail) => Present(_rocketLaunchPad, out detail);
        public bool TryGetSpaceElevator(out SpaceElevatorDetail detail) => Present(_spaceElevator, out detail);

        private static bool Present<T>(T value, out T detail) where T : class
        {
            detail = value;
            return value != null;
        }
    }
}
```

`Text/JsNumberText.cs`:
```csharp
using System;
using System.Globalization;
using Client.Ui.State.Text;

namespace Client.Ui.State.BlockInventory.Special.Text
{
    /// <summary>
    /// Web が文に入れる数値の JS の書式（toFixed(0)・toPrecision(2)・String(number)）の写し
    /// Port of the JS number formats the Web puts into sentences: toFixed(0), toPrecision(2) and String(number)
    /// </summary>
    public static class JsNumberText
    {
        public static string Fixed0(double value)
        {
            return UiNumberFormat.Fixed(value, 0);
        }

        // 正の微小値は 0 に潰さず有効 2 桁で残す（Web GearToElectricSection の formatPower）
        // Keep a positive tiny value at two significant digits instead of 0 (Web GearToElectricSection formatPower)
        public static string PositivePower(double power)
        {
            var rounded = Fixed0(power);
            return 0 < power && rounded == "0" ? Precision2(power) : rounded;
        }

        // toPrecision(2): 指数が -6 より小さいか 2 以上なら指数表記（符号付き）、他は小数（JS の規則）
        // toPrecision(2): signed exponent notation when the exponent is below -6 or at least 2, plain decimals otherwise (JS rule)
        public static string Precision2(double value)
        {
            if (value == 0) return "0.0";
            var exponent = (int)Math.Floor(Math.Log10(Math.Abs(value)));
            var mantissa = Math.Round(value / Math.Pow(10, exponent), 1, MidpointRounding.AwayFromZero);
            if (10 <= Math.Abs(mantissa))
            {
                mantissa /= 10;
                exponent += 1;
            }
            if (exponent < -6 || 2 <= exponent) return $"{mantissa.ToString("0.0", CultureInfo.InvariantCulture)}e{(exponent < 0 ? "-" : "+")}{Math.Abs(exponent)}";

            // 小数は有効 2 桁ぶんの桁数で丸める
            // Plain decimals round to the digits that keep two significant figures
            var decimals = 1 - exponent;
            return Math.Round(value, decimals, MidpointRounding.AwayFromZero).ToString("F" + decimals, CultureInfo.InvariantCulture);
        }

        // String(number): 整数は小数点なし、他は最短の往復表記
        // String(number): integers without a decimal point, others in the shortest round-trip form
        public static string Number(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
```

`Text/SpecialDetailTexts.cs`:
```csharp
using Client.Localization;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.BlockInventory.Special.Text
{
    /// <summary>
    /// 特殊族の区画・本文の文。値の書式は Web の各 View と同じ（NetworkSections・GearToElectricSection・ElectricToGearInventory・FilterSplitterInventory・SpaceElevatorSection）
    /// Sentences of the special sections and bodies; value formats match each Web view
    /// </summary>
    public static class SpecialDetailTexts
    {
        public static UiText ElectricNetworkSummary(ElectricNetworkDetail network)
        {
            // 供給率は JS の Math.round と同じく .5 を上へ丸める
            // The supply rate rounds .5 up like JS Math.round
            var rate = UiNumberFormat.Integer(UiNumberFormat.RoundHalfUp(network.PowerRate * 100));
            return With(LocalizationKeys.Ui.BlockInventory.ElectricNetworkSummary,
                Param("generated", JsNumberText.Fixed0(network.TotalGeneratePower)),
                Param("required", JsNumberText.Fixed0(network.TotalRequiredPower)),
                Param("count", network.ConsumerCount.ToString()),
                Param("rate", rate));
        }

        public static UiText GearNetworkSummary(GearNetworkDetail network)
        {
            return With(LocalizationKeys.Ui.BlockInventory.GearNetworkSummary,
                Param("generated", JsNumberText.Fixed0(network.TotalGenerateGearPower)),
                Param("required", JsNumberText.Fixed0(network.TotalRequiredGearPower)));
        }

        public static UiText GearToElectricSuppliedPower(GearToElectricDetail detail)
        {
            return With(LocalizationKeys.Ui.BlockInventory.GearToElectricSuppliedPower,
                Param("current", JsNumberText.PositivePower(detail.SuppliedPower)),
                Param("max", JsNumberText.PositivePower(detail.MaxGeneratedPower)));
        }

        public static UiText ElectricToGearOutputMode(ElectricToGearOutputMode mode)
        {
            return With(LocalizationKeys.Ui.BlockInventory.ElectricToGearOutputMode,
                Param("rpm", JsNumberText.Number(mode.Rpm)),
                Param("torque", JsNumberText.Number(mode.Torque)),
                Param("power", JsNumberText.Number(mode.RequiredPower)));
        }

        public static UiText ElectricToGearConsumedPower(ElectricToGearDetail detail)
        {
            return With(LocalizationKeys.Ui.BlockInventory.ElectricToGearConsumedPower, Param("power", JsNumberText.Fixed0(detail.ConsumedElectricPower)));
        }

        // 方向は 1 始まりで見せる
        // Directions are shown starting at 1
        public static UiText OutputNumber(int directionIndex)
        {
            return With(LocalizationKeys.Ui.BlockInventory.OutputNumber, Param("index", (directionIndex + 1).ToString()));
        }

        public static UiText SpaceElevatorPool(SpaceElevatorDetail detail)
        {
            return With(LocalizationKeys.Ui.BlockInventory.SpaceElevatorPool, Param("pool", detail.CablePoolAmount.ToString()), Param("required", detail.CablesPerElevator.ToString()));
        }

        private static LocalizationTextParam Param(string name, string value)
        {
            return new LocalizationTextParam(name, value);
        }

        private static UiText With(LocalizationKey key, params LocalizationTextParam[] parameters)
        {
            return new UiText(key, parameters);
        }
    }
}
```

- [ ] **Step 4: コンパイルしテストを実行して通ることを確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.State\.BlockInventory\.Special\."`
Expected: ErrorCount 0 / PASS。`Precision2` の行が落ちたら、JS の `Number.prototype.toPrecision` の規則（指数 e が -6 より小さいか精度以上なら指数表記。指数の符号は常に付く）に合わせて実装を直す。テストの期待値は変えない（Web の vitest の行と Node の `(150).toPrecision(2)` 等で確かめた値）

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Client.Ui.State.asmdef moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/Special
git commit -m "feat(uitk): ブロックインベントリの特殊族の詳細の型と文を足す（S4-special）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```
（`.meta` は Unity が作ったものだけを足す。以後のタスクも同じ）

---

### Task 3: 読み手と詳細の Model（`Source/`・`SpecialBlockDetailModel`）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special/Source/SpecialStateDetailReader.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special/Source/SpecialNetworkReader.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special/Source/BeltThroughputReader.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special/Source/SpecialBlockDetailReader.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special/Source/SpecialInletBindingContributor.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special/ISpecialBlockDetailModel.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special/SpecialBlockDetailModel.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/Special/SpecialStateDetailReaderTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/Special/SpecialNetworkReaderTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/Special/SpecialInletBindingContributorTest.cs`

**Interfaces:**
- Consumes: Task 2 の Details 一式、`OpenBlockDetailTracker`・`BlockNetworkInfoCache`・`BeltTransportClientCache`（実物）、L0-s4b の `InletSlotBinding`・`BlockSlotRestrictionInput`・`BlockSlotRestrictionResolver`、S4-alpha の拡張口 (6) `IBlockSlotRestrictionContributor`・`OpenBlockInventoryPanel`（`BlockType`・`BlockInstanceId`・`Error`）・`IBlockInventoryPanelSource`・L0-s4b の `OpenBlockSlotInput.WithInletBindings`（Task 1 Step 2 で実物と照合）
- Produces:
```csharp
namespace Client.Ui.State.BlockInventory.Special
{
    public interface ISpecialBlockDetailModel { IReadOnlyReactiveProperty<SpecialBlockDetailViewModel> Current { get; } }
    public sealed class SpecialBlockDetailModel : ISpecialBlockDetailModel, IInitializable { public SpecialBlockDetailModel(IBlockInventoryPanelSource panels, OpenBlockDetailTracker tracker, BlockNetworkInfoCache networkCache, BeltTransportClientCache beltCache); }   // 識別は alpha のパネル、中身は追跡ブロック（BlockInstanceId が一致する間だけ。D-26）
}
namespace Client.Ui.State.BlockInventory.Special.Source
{
    public static class SpecialStateDetailReader
    {
        public static ElectricToGearDetail ElectricToGear(ElectricToGearGeneratorBlockStateDetail state, IReadOnlyList<ElectricToGearOutputMode> outputModes);
        public static GearToElectricDetail GearToElectric(GearToElectricGeneratorBlockStateDetail state, double maxGeneratedPower);
        public static GearToElectricDisplayState ToDisplayState(GearToElectricGenerationState state);
        public static GearDetail Generator(GearStateDetail gear);
        public static GearDetail Consumer(GearStateDetail gear, double baseRpm);
        public static RocketLaunchPadDetail RocketLaunchPad(RocketLaunchPadStateDetail state, IReadOnlyList<int> materialSlots, IReadOnlyList<int> payloadSlots, int outputSlotStart);
        public static SpaceElevatorDetail SpaceElevator(SpaceElevatorBaseStateDetail state, int satelliteSlot, int firstOutputSlot, int outputSlotCount, int cablesPerElevator);
    }
    public static class SpecialNetworkReader
    {
        public static ElectricNetworkDetail Electric(GetElectricNetworkInfoProtocol.ElectricNetworkInfoSnapshot snapshot);   // null → null
        public static GearNetworkDetail Gear(GetGearNetworkInfoProtocol.GearNetworkInfoSnapshot snapshot);                    // null → null
        public static FilterSplitterDetail FilterSplitter(FilterSplitterStateProtocol.FilterSplitterStateResponse response);  // null → null
        public static GearNetworkStopReasonKind ToStopReasonKind(GearNetworkStopReason reason);
        public static FilterSplitterMode ToClientMode(Game.Block.Blocks.FilterSplitter.FilterSplitterMode mode);
        public static Game.Block.Blocks.FilterSplitter.FilterSplitterMode ToServerMode(FilterSplitterMode mode);
    }
    public static class BeltThroughputReader { public static BeltThroughputDetail Read(BlockGameObject block, BeltTransportClientCache cache); } // ベルトでなければ null
    public static class SpecialBlockDetailReader { public static SpecialBlockDetailViewModel Read(BlockGameObject block, BlockNetworkInfoCache networkCache, BeltTransportClientCache beltCache); }
    public sealed class SpecialInletBindingContributor : IBlockSlotRestrictionContributor   // alpha の拡張口 (6)
    {
        public bool AppliesTo(OpenBlockInventoryPanel panel);                                 // blockType が発射台・宇宙エレベーター
        public OpenBlockSlotInput Contribute(OpenBlockInventoryPanel panel, OpenBlockSlotInput input); // 状態の未着は input のまま（L0-s4b が取得待ちへ倒す）
        public static bool TryGetInletBindings(BlockGameObject block, out IReadOnlyList<InletSlotBinding> bindings); // 入れ口固定でない・詳細未着は false
        public static IReadOnlyList<InletSlotBinding> SpaceElevatorBindings(int satelliteSlot, IReadOnlyList<ItemId> carriedPayloadItemIds);
    }
}
```

- [ ] **Step 1: テストを書く**

`SpecialStateDetailReaderTest.cs`:
```csharp
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Source;
using Game.Block.Blocks.ElectricToGear;
using Game.Block.Blocks.GearToElectric;
using Game.Block.Interface.State;
using Game.Gear.Common;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// state detail とマスタの値から Web の DTO と同じ値の詳細を組む
    /// Builds details with the same values as the Web DTOs from state details and master values
    /// </summary>
    public class SpecialStateDetailReaderTest
    {
        [Test]
        public void 回転生成機は選択と充足率と消費電力とマスタ順のモードを写す()
        {
            var state = new ElectricToGearGeneratorBlockStateDetail { SelectedIndex = 1, ElectricFulfillmentRate = 0.75f, ConsumedElectricPower = 10f };
            var modes = new[] { new ElectricToGearOutputMode(10, 10, 10), new ElectricToGearOutputMode(20, 20, 10) };

            var detail = SpecialStateDetailReader.ElectricToGear(state, modes);

            Assert.AreEqual(1, detail.SelectedIndex);
            Assert.AreEqual(0.75d, detail.FulfillmentRate, 1e-6);
            Assert.AreEqual(10d, detail.ConsumedElectricPower, 1e-6);
            CollectionAssert.AreEqual(modes, detail.OutputModes);
        }

        [TestCase(GearToElectricGenerationState.Generating, GearToElectricDisplayState.Generating)]
        [TestCase(GearToElectricGenerationState.Idle, GearToElectricDisplayState.Idle)]
        [TestCase(GearToElectricGenerationState.Stopped, GearToElectricDisplayState.Stopped)]
        public void 回転発電機の発電状態を表示の状態へ写す(GearToElectricGenerationState server, GearToElectricDisplayState expected)
        {
            var state = new GearToElectricGeneratorBlockStateDetail { GenerationState = server, GeneratedElectricPower = 72.4f };
            var detail = SpecialStateDetailReader.GearToElectric(state, 100);
            Assert.AreEqual(expected, detail.State);
            Assert.AreEqual(72.4d, detail.SuppliedPower, 1e-4);
            Assert.AreEqual(100d, detail.MaxGeneratedPower);
        }

        [Test]
        public void 歯車は役割ごとに消費側は基準RPM付き発電側は現在値だけを持つ()
        {
            var consumer = SpecialStateDetailReader.Consumer(new GearStateDetail(true, 5f, 2.4f, GearRole.Consumer), 10);
            var generator = SpecialStateDetailReader.Generator(new GearStateDetail(true, 20f, 5f, GearRole.Generator));
            Assert.AreEqual(GearRowDisplay.From(GearDetail.Consumer(5, 2.4f, 10)).Rpm.Resolve(), GearRowDisplay.From(consumer).Rpm.Resolve());
            Assert.AreEqual(GearRowDisplay.From(GearDetail.Generator(20, 5)).Torque.Resolve(), GearRowDisplay.From(generator).Torque.Resolve());
        }

        [Test]
        public void 発射台は資材と積荷の絶対添字と出力の先頭を持ち在庫の数まで出力を並べる()
        {
            var detail = SpecialStateDetailReader.RocketLaunchPad(new RocketLaunchPadStateDetail(RocketLaunchPadPhase.AwaitingBuildCommand, RocketLaunchPadWaitReason.None, 0.375f), new[] { 0, 1 }, new[] { 2 }, 3);
            CollectionAssert.AreEqual(new[] { 0, 1 }, detail.MaterialSlots);
            CollectionAssert.AreEqual(new[] { 2 }, detail.PayloadSlots);
            CollectionAssert.AreEqual(new[] { 3, 4 }, detail.OutputSlots(5));
            CollectionAssert.IsEmpty(detail.OutputSlots(3));
            Assert.AreEqual(0.375f, detail.Progress);
        }

        [Test]
        public void 宇宙エレベーターは入口1枠と出口の連番とプールと状態を持つ()
        {
            var detail = SpecialStateDetailReader.SpaceElevator(new SpaceElevatorBaseStateDetail(false, 250, 0f, SpaceElevatorBaseStatus.InsufficientCable), 0, 1, 2, 1000);
            CollectionAssert.AreEqual(new[] { 0 }, detail.SatelliteInputSlots);
            CollectionAssert.AreEqual(new[] { 1, 2 }, detail.OutputSlots);
            Assert.AreEqual(250, detail.CablePoolAmount);
            Assert.AreEqual(1000, detail.CablesPerElevator);
            Assert.AreEqual(SpaceElevatorBaseStatus.InsufficientCable, detail.Status);
            Assert.IsFalse(detail.Connected);
        }
    }
}
```

`SpecialNetworkReaderTest.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Source;
using Core.Master;
using Game.Gear.Common;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using ServerFilterSplitterMode = Game.Block.Blocks.FilterSplitter.FilterSplitterMode;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// 集約キャッシュの値を網と分配器の詳細へ写す（未取得は不在）
    /// Maps aggregate cache values to network and splitter details; unfetched values stay absent
    /// </summary>
    public class SpecialNetworkReaderTest
    {
        [Test]
        public void 未取得の集約は不在のまま()
        {
            Assert.IsNull(SpecialNetworkReader.Electric(null));
            Assert.IsNull(SpecialNetworkReader.Gear(null));
            Assert.IsNull(SpecialNetworkReader.FilterSplitter(null));
        }

        [Test]
        public void 電力網と歯車網の集約を写す()
        {
            var electric = SpecialNetworkReader.Electric(new GetElectricNetworkInfoProtocol.ElectricNetworkInfoSnapshot(240f, 180f, 1f, 3));
            Assert.AreEqual(240d, electric.TotalGeneratePower);
            Assert.AreEqual(180d, electric.TotalRequiredPower);
            Assert.AreEqual(3, electric.ConsumerCount);
            Assert.AreEqual(1d, electric.PowerRate);

            var gear = SpecialNetworkReader.Gear(new GetGearNetworkInfoProtocol.GearNetworkInfoSnapshot(30f, 20f, GearNetworkStopReason.OverRequirePower));
            Assert.AreEqual(20d, gear.TotalGenerateGearPower);
            Assert.AreEqual(30d, gear.TotalRequiredGearPower);
            Assert.AreEqual(GearNetworkStopReasonKind.OverRequirePower, gear.StopReason);
        }

        [Test]
        public void 停止理由はサーバーの全値を写す()
        {
            foreach (GearNetworkStopReason reason in Enum.GetValues(typeof(GearNetworkStopReason)))
            {
                Assert.AreEqual(reason.ToString(), SpecialNetworkReader.ToStopReasonKind(reason).ToString());
            }
        }

        [Test]
        public void 分配器のモードを両向きに写し方向とフィルタ枠を並びのまま写す()
        {
            foreach (ServerFilterSplitterMode mode in Enum.GetValues(typeof(ServerFilterSplitterMode)))
            {
                Assert.AreEqual(mode, SpecialNetworkReader.ToServerMode(SpecialNetworkReader.ToClientMode(mode)));
            }

            var response = new FilterSplitterStateProtocol.FilterSplitterStateResponse(true, FilterSplitterStateProtocol.FilterSplitterStateFailureReason.None, 2, 2, new List<FilterSplitterStateProtocol.DirectionStatePack>
            {
                new() { Mode = ServerFilterSplitterMode.Whitelist, FilterItemIds = new List<ItemId> { new(4), ItemMaster.EmptyItemId } },
                new() { Mode = ServerFilterSplitterMode.Blacklist, FilterItemIds = new List<ItemId> { new(7), new(8) } },
            });
            var detail = SpecialNetworkReader.FilterSplitter(response);
            Assert.AreEqual(2, detail.Directions.Count);
            Assert.AreEqual(FilterSplitterMode.Whitelist, detail.Directions[0].Mode);
            CollectionAssert.AreEqual(new[] { new ItemId(4), ItemMaster.EmptyItemId }, detail.Directions[0].FilterItemIds);
            Assert.AreEqual(FilterSplitterMode.Blacklist, detail.Directions[1].Mode);
        }
    }
}
```
（`ItemId` の生成の書き方〈`new ItemId(4)`〉が実物と違えば実物に合わせる。`ItemMaster.EmptyItemId` は Web の橋 `FilterSplitterActions` が使う実物）

`SpecialInletBindingContributorTest.cs`:
```csharp
using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.BlockInventory.Special.Source;
using Client.Ui.State.ItemMove;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// 宇宙エレベーターの入れ口が運べない物の Shift を候補 0 にし、詳細の未着は取得待ちにする（Shift の判断まで。送らない・通知は S4-alpha の Shift の実行器）
    /// The space elevator inlet leaves no candidate for an unsupported item on Shift and a pending detail resolves to loading (decision only; not sending and the notice belong to S4-alpha's Shift executor)
    /// </summary>
    public class SpecialInletBindingContributorTest
    {
        private static readonly ItemId Wood = new(1);
        private static readonly ItemId GpuSatellite = new(5);

        /// <web-test file="e2e/tests/block/spaceElevator/shiftRejection.spec.ts">受け付けないアイテムのShiftは送らずに拒否の通知を出す</web-test>
        [Test]
        public void 受け付けないアイテムのShiftは候補が無く入れ口の物だけが入口へ向かう()
        {
            var bindings = SpecialInletBindingContributor.SpaceElevatorBindings(0, new[] { GpuSatellite });
            var open = BlockSlotRestrictionInput.OpenBlock(BlockMasterElement.BlockTypeConst.SpaceElevatorBase, 3).WithInletBindings(bindings);

            var wood = BlockSlotRestrictionResolver.Resolve(open, Wood) as BoundBlockSlots;
            var satellite = BlockSlotRestrictionResolver.Resolve(open, GpuSatellite) as BoundBlockSlots;

            Assert.IsNotNull(wood);
            CollectionAssert.IsEmpty(wood.CandidateIndices);
            CollectionAssert.AreEqual(new[] { 0 }, satellite.CandidateIndices);
        }

        /// <web-test file="e2e/tests/block/spaceElevator/shiftRejection.spec.ts">詳細の取得待ちのShiftは送らずに取得待ちを通知する</web-test>
        [Test]
        public void 詳細の取得待ちのShiftは取得待ちになる()
        {
            var pending = BlockSlotRestrictionInput.OpenBlock(BlockMasterElement.BlockTypeConst.SpaceElevatorBase, 3);
            Assert.IsInstanceOf<BlockDetailLoading>(BlockSlotRestrictionResolver.Resolve(pending, Wood));
        }
    }
}
```

- [ ] **Step 2: 実装を書く**

`Source/SpecialStateDetailReader.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Special.Details;
using Game.Block.Blocks.ElectricToGear;
using Game.Block.Blocks.GearToElectric;
using Game.Block.Interface.State;
using Game.Gear.Common;

namespace Client.Ui.State.BlockInventory.Special.Source
{
    /// <summary>
    /// state detail とマスタの値から各詳細を組む（Web の ElectricToGear/GearToElectric/Gear/RocketLaunchPad/SpaceElevator の DtoBuilder の写し）
    /// Builds each detail from state details and master values (port of the Web DtoBuilders)
    /// </summary>
    public static class SpecialStateDetailReader
    {
        public static ElectricToGearDetail ElectricToGear(ElectricToGearGeneratorBlockStateDetail state, IReadOnlyList<ElectricToGearOutputMode> outputModes)
        {
            return new ElectricToGearDetail(outputModes, state.SelectedIndex, state.ElectricFulfillmentRate, state.ConsumedElectricPower);
        }

        public static GearToElectricDetail GearToElectric(GearToElectricGeneratorBlockStateDetail state, double maxGeneratedPower)
        {
            return new GearToElectricDetail(ToDisplayState(state.GenerationState), state.GeneratedElectricPower, maxGeneratedPower);
        }

        public static GearToElectricDisplayState ToDisplayState(GearToElectricGenerationState state)
        {
            return state switch
            {
                GearToElectricGenerationState.Generating => GearToElectricDisplayState.Generating,
                GearToElectricGenerationState.Idle => GearToElectricDisplayState.Idle,
                GearToElectricGenerationState.Stopped => GearToElectricDisplayState.Stopped,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, "未知の回転発電機の状態"),
            };
        }

        public static GearDetail Generator(GearStateDetail gear)
        {
            return GearDetail.Generator(gear.CurrentRpm, gear.CurrentTorque);
        }

        public static GearDetail Consumer(GearStateDetail gear, double baseRpm)
        {
            return GearDetail.Consumer(gear.CurrentRpm, gear.CurrentTorque, baseRpm);
        }

        public static RocketLaunchPadDetail RocketLaunchPad(RocketLaunchPadStateDetail state, IReadOnlyList<int> materialSlots, IReadOnlyList<int> payloadSlots, int outputSlotStart)
        {
            return new RocketLaunchPadDetail(state.Phase, state.WaitReason, state.Progress, materialSlots, payloadSlots, outputSlotStart);
        }

        // 入口は 1 枠、出口は先頭から連番（Web SpaceElevatorDetailDtoBuilder と同じ）
        // One inlet slot and consecutive outlets from the first output slot (same as the Web builder)
        public static SpaceElevatorDetail SpaceElevator(SpaceElevatorBaseStateDetail state, int satelliteSlot, int firstOutputSlot, int outputSlotCount, int cablesPerElevator)
        {
            var outputs = new List<int>(outputSlotCount);
            for (var i = 0; i < outputSlotCount; i++) outputs.Add(firstOutputSlot + i);
            return new SpaceElevatorDetail(new[] { satelliteSlot }, outputs, state.IsConnected, state.CablePoolAmount, cablesPerElevator, state.ClimbProgress, state.Status);
        }
    }
}
```
（`GearDetail.Consumer`/`Generator` の引数の型〈double〉は L0-s4b の実物。`GearStateDetail` の float は暗黙に広がる）

`Source/SpecialNetworkReader.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Details;
using Game.Gear.Common;
using Server.Protocol.PacketResponse;
using ServerFilterSplitterMode = Game.Block.Blocks.FilterSplitter.FilterSplitterMode;

namespace Client.Ui.State.BlockInventory.Special.Source
{
    /// <summary>
    /// 集約キャッシュ（電力網・歯車網・フィルタ分配器）を詳細へ写す。未取得（null）は不在のまま（Web ApplyNetworkCaches と同じ）
    /// Maps the aggregate caches to details; unfetched (null) stays absent (same as Web ApplyNetworkCaches)
    /// </summary>
    public static class SpecialNetworkReader
    {
        public static ElectricNetworkDetail Electric(GetElectricNetworkInfoProtocol.ElectricNetworkInfoSnapshot snapshot)
        {
            // silent-ok: 未取得はポーリングの到着待ちで、区画を出さないのが Web と同じ表示 / 届かないままなら録画（R-12）の電柱の段で気づく
            if (snapshot == null) return null;
            return new ElectricNetworkDetail(snapshot.TotalGeneratePower, snapshot.TotalRequiredPower, snapshot.ConsumerCount, snapshot.PowerRate);
        }

        public static GearNetworkDetail Gear(GetGearNetworkInfoProtocol.GearNetworkInfoSnapshot snapshot)
        {
            // silent-ok: 未取得はポーリングの到着待ち / 届かないままなら録画（R-12）の歯車の段で気づく
            if (snapshot == null) return null;
            return new GearNetworkDetail(snapshot.TotalGenerateGearPower, snapshot.TotalRequiredGearPower, ToStopReasonKind(snapshot.StopReason));
        }

        public static FilterSplitterDetail FilterSplitter(FilterSplitterStateProtocol.FilterSplitterStateResponse response)
        {
            // silent-ok: 未取得は分配器の初回取得の到着待ち / 届かないままなら録画（R-12）の分配器の段で気づく
            if (response == null) return null;
            var directions = new List<FilterSplitterDirectionDetail>(response.Directions.Count);
            foreach (var direction in response.Directions) directions.Add(new FilterSplitterDirectionDetail(ToClientMode(direction.Mode), direction.FilterItemIds));
            return new FilterSplitterDetail(directions);
        }

        public static GearNetworkStopReasonKind ToStopReasonKind(GearNetworkStopReason reason)
        {
            return reason switch
            {
                GearNetworkStopReason.None => GearNetworkStopReasonKind.None,
                GearNetworkStopReason.Rocked => GearNetworkStopReasonKind.Rocked,
                GearNetworkStopReason.OverRequirePower => GearNetworkStopReasonKind.OverRequirePower,
                GearNetworkStopReason.NoGeneration => GearNetworkStopReasonKind.NoGeneration,
                GearNetworkStopReason.NoGenerator => GearNetworkStopReasonKind.NoGenerator,
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "未知の歯車網の停止理由"),
            };
        }

        public static FilterSplitterMode ToClientMode(ServerFilterSplitterMode mode)
        {
            return mode switch
            {
                ServerFilterSplitterMode.Default => FilterSplitterMode.Default,
                ServerFilterSplitterMode.Whitelist => FilterSplitterMode.Whitelist,
                ServerFilterSplitterMode.Blacklist => FilterSplitterMode.Blacklist,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "未知のフィルタ分配器のモード"),
            };
        }

        public static ServerFilterSplitterMode ToServerMode(FilterSplitterMode mode)
        {
            return mode switch
            {
                FilterSplitterMode.Default => ServerFilterSplitterMode.Default,
                FilterSplitterMode.Whitelist => ServerFilterSplitterMode.Whitelist,
                FilterSplitterMode.Blacklist => ServerFilterSplitterMode.Blacklist,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "未知のフィルタ分配器のモード"),
            };
        }
    }
}
```

`Source/BeltThroughputReader.cs`:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.BeltTransport.Statistics;
using Client.Game.InGame.Block;
using Client.Ui.State.BlockInventory.Logic.Belt;
using Client.Ui.State.BlockInventory.Special.Details;
using Core.BeltTransport;
using Core.Update;
using Mooresmaster.Model.BlocksModule;

namespace Client.Ui.State.BlockInventory.Special.Source
{
    /// <summary>
    /// クライアントのベルトの複製が数えた搬出統計を読む（Web BeltThroughputDetailDtoBuilder の写し）
    /// Reads exit statistics counted by the client's belt replica (port of Web BeltThroughputDetailDtoBuilder)
    /// </summary>
    public static class BeltThroughputReader
    {
        private const int RingSlotsPerSecond = GameUpdater.TicksPerSecond / BeltSegmentExitWindows.RingBlockTicks;

        public static BeltThroughputDetail Read(BlockGameObject block, BeltTransportClientCache cache)
        {
            // ベルトでない・複製に観測区間が無いなら不在
            // Absent unless the block is a belt with an observed segment in the replica
            if (block.BlockMasterElement.BlockParam is not IBeltSpeedParam) return null;
            // silent-ok: ベルトの区間がまだ複製に載っていない間は区画を出さない（Web と同じ） / 載らないままなら録画（R-12）のベルトの段で気づく
            if (!cache.TryGetObservedSegmentIndex(block.BlockPosInfo.OriginalPos, out var segmentIndex)) return null;
            var speed = (double)cache.Replica.Segments[segmentIndex].Speed;

            // 窓ごとの数と最大（速さ×tick÷物の幅）
            // Per-window counts and maxima (speed x ticks / item width)
            var windows = new List<BeltThroughputWindow>(BeltSegmentExitWindows.Ticks.Length);
            foreach (var ticks in BeltSegmentExitWindows.Ticks)
            {
                var maxCount = speed * ticks / BeltConstants.ItemWidth;
                windows.Add(cache.ExitStatistics.TryGetExitCount(segmentIndex, ticks, out var exitCount) ? BeltThroughputWindow.Measured(ticks, exitCount, maxCount) : BeltThroughputWindow.Measuring(ticks, maxCount));
            }

            // 直近の秒ごとの数（埋まった分だけ）
            // Recent per-second counts, only as many as are filled
            var buckets = new int[BeltThroughputLogic.RecentSecondBarCount];
            var filled = cache.ExitStatistics.ReadRecentExitCounts(segmentIndex, RingSlotsPerSecond, buckets);
            var recent = new List<int>(filled);
            for (var i = 0; i < filled; i++) recent.Add(buckets[i]);
            return new BeltThroughputDetail(windows, recent, speed * GameUpdater.TicksPerSecond / BeltConstants.ItemWidth);
        }
    }
}
```

`Source/SpecialBlockDetailReader.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.Block;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Special.Details;
using Game.Block.Blocks.ElectricToGear;
using Game.Block.Blocks.GearToElectric;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Special.Source
{
    /// <summary>
    /// 開いたブロックの特殊族の詳細をまとめて読む（Web BlockDetailDtoBuilder の特殊族の部分と同じ源・同じ条件）
    /// Reads all special-family details of the open block (same sources and conditions as the special part of Web BlockDetailDtoBuilder)
    /// </summary>
    public static class SpecialBlockDetailReader
    {
        public static SpecialBlockDetailViewModel Read(BlockGameObject block, BlockNetworkInfoCache networkCache, BeltTransportClientCache beltCache)
        {
            var param = block.BlockMasterElement.BlockParam;
            return new SpecialBlockDetailViewModel(
                ReadElectricToGear(), ReadGearToElectric(), ReadGear(), BeltThroughputReader.Read(block, beltCache),
                SpecialNetworkReader.Electric(networkCache.Electric), SpecialNetworkReader.Gear(networkCache.GearNetwork), SpecialNetworkReader.FilterSplitter(networkCache.FilterSplitter),
                ReadRocketLaunchPad(), ReadSpaceElevator());

            #region Internal

            ElectricToGearDetail ReadElectricToGear()
            {
                if (param is not ElectricToGearGeneratorBlockParam electricParam) return null;
                var state = block.GetStateDetail<ElectricToGearGeneratorBlockStateDetail>(ElectricToGearGeneratorBlockStateDetail.BlockStateDetailKey);
                // silent-ok: 状態の初回到着待ち。本文は空のまま（Web と同じ） / 届かないままなら録画（R-12）の回転生成機の段で気づく
                if (state == null) return null;
                var modes = electricParam.OutputModes.Select(mode => new ElectricToGearOutputMode(mode.Rpm, mode.Torque, mode.RequiredPower)).ToList();
                return SpecialStateDetailReader.ElectricToGear(state, modes);
            }

            GearToElectricDetail ReadGearToElectric()
            {
                if (param is not GearToElectricGeneratorBlockParam generatorParam) return null;
                var state = block.GetStateDetail<GearToElectricGeneratorBlockStateDetail>(GearToElectricGeneratorBlockStateDetail.GearGeneratorBlockStateDetailKey);
                if (state != null) return SpecialStateDetailReader.GearToElectric(state, generatorParam.MaxGeneratedPower);

                // 未着は開発者へ知らせて区画を出さない（Web の橋と同じ警告）
                // Tell developers about the missing state and render no section (same warning as the Web bridge)
                Debug.LogWarning("[SpecialBlockDetailReader] GearToElectric の状態詳細が未着のため区画を保留します");
                return null;
            }

            GearDetail ReadGear()
            {
                var gear = block.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey);
                // silent-ok: 歯車でないブロックは歯車の状態を持たない / 歯車ブロックで出ないなら録画（R-12）の歯車の段で気づく
                if (gear == null) return null;
                if (gear.Role == GearRole.Generator) return SpecialStateDetailReader.Generator(gear);
                if (param is IGearConsumptionParam consumptionParam) return SpecialStateDetailReader.Consumer(gear, consumptionParam.GearConsumption.BaseRpm);

                // 消費側なのに消費の param が無いのはマスタの食い違い（Web の橋と同じエラー）
                // A consumer without a consumption param is a master mismatch (same error as the Web bridge)
                Debug.LogError($"[SpecialBlockDetailReader] Server reports GearRole.Consumer but block param {param?.GetType().Name} has no IGearConsumptionParam; gear rows are omitted");
                return null;
            }

            RocketLaunchPadDetail ReadRocketLaunchPad()
            {
                if (param is not RocketLaunchPadBlockParam rocketParam) return null;
                var state = block.GetStateDetail<RocketLaunchPadStateDetail>(RocketLaunchPadStateDetail.BlockStateDetailKey);
                // silent-ok: 状態の初回到着待ちは本文が「状態の取得を待っています」を出す / 録画（R-12）の発射台の段で気づく
                if (state == null) return null;
                var layout = new RocketLaunchPadSlotLayout(rocketParam);
                return SpecialStateDetailReader.RocketLaunchPad(state, layout.MaterialSlots.ToList(), layout.PayloadSlots.ToList(), layout.OutputSlotStart);
            }

            SpaceElevatorDetail ReadSpaceElevator()
            {
                if (param is not SpaceElevatorBaseBlockParam elevatorParam) return null;
                var state = block.GetStateDetail<SpaceElevatorBaseStateDetail>(SpaceElevatorBaseStateDetail.BlockStateDetailKey);
                // silent-ok: 状態の初回到着待ちは区画が取得待ちを出す / 録画（R-12）の宇宙エレベーターの段で気づく
                if (state == null) return null;
                return SpecialStateDetailReader.SpaceElevator(state, SpaceElevatorBaseInventoryComponent.SatelliteSlot, SpaceElevatorBaseInventoryComponent.FirstOutputSlot, elevatorParam.OutputSlotCount, elevatorParam.CablesPerElevator);
            }

            #endregion
        }
    }
}
```
（マスタの生成物の値の型〈`Rpm`・`MaxGeneratedPower`・`BaseRpm` の float/double、`CablesPerElevator` の int〉は Web の橋の同じ行がそのまま代入している型に合わせる。食い違えばキャストを足す）

`Source/SpecialInletBindingContributor.cs`:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.SlotRestriction;
using Core.Master;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;

namespace Client.Ui.State.BlockInventory.Special.Source
{
    /// <summary>
    /// 発射台・宇宙エレベーターの入れ口が受け付ける物を S4-alpha の Shift の束縛へ寄与する（Web の slotBindings と同じ。状態の未着は寄与せず取得待ち）
    /// Contributes what the launch pad and space elevator inlets accept to S4-alpha's Shift bindings (same as the Web slotBindings; no contribution while the state is missing, so it stays pending)
    /// </summary>
    public sealed class SpecialInletBindingContributor : IBlockSlotRestrictionContributor
    {
        private readonly OpenBlockDetailTracker _tracker;

        public SpecialInletBindingContributor(OpenBlockDetailTracker tracker)
        {
            _tracker = tracker;
        }

        public bool AppliesTo(OpenBlockInventoryPanel panel)
        {
            return panel.BlockType == BlockMasterElement.BlockTypeConst.RocketLaunchPad || panel.BlockType == BlockMasterElement.BlockTypeConst.SpaceElevatorBase;
        }

        public OpenBlockSlotInput Contribute(OpenBlockInventoryPanel panel, OpenBlockSlotInput input)
        {
            // パネルは識別子だけなので、追跡ブロックを BlockInstanceId で照らして中身を読む（D-26）
            // The panel only carries identifiers, so read contents from the tracked block matched by BlockInstanceId (D-26)
            var block = _tracker.TrackedBlock;
            // silent-ok: 追跡の差し替え途中・状態の未着は寄与しない（L0-s4b が入れ口固定のブロックを取得待ちへ倒す） / alpha の Shift が取得待ちの通知（状態の取得を待っています）を出して気づく
            if (block == null || !block.BlockInstanceId.Equals(panel.BlockInstanceId)) return input;
            return TryGetInletBindings(block, out var bindings) ? input.WithInletBindings(bindings) : input;
        }

        public static bool TryGetInletBindings(BlockGameObject block, out IReadOnlyList<InletSlotBinding> bindings)
        {
            bindings = null;
            var param = block.BlockMasterElement.BlockParam;

            // 発射台: 資材と積荷の枠ごとに要求の 1 品
            // Launch pad: one required item per material and payload slot
            if (param is RocketLaunchPadBlockParam rocketParam)
            {
                if (block.GetStateDetail<RocketLaunchPadStateDetail>(RocketLaunchPadStateDetail.BlockStateDetailKey) == null) return false;
                var layout = new RocketLaunchPadSlotLayout(rocketParam);
                var list = new List<InletSlotBinding>();
                foreach (var slot in layout.MaterialSlots) list.Add(new InletSlotBinding(slot, new[] { layout.RequirementAt(slot).ItemId }));
                foreach (var slot in layout.PayloadSlots) list.Add(new InletSlotBinding(slot, new[] { layout.RequirementAt(slot).ItemId }));
                bindings = list;
                return true;
            }

            // 宇宙エレベーター: 入口 1 枠に運べる積荷
            // Space elevator: the carried payloads on the single inlet slot
            if (param is SpaceElevatorBaseBlockParam elevatorParam)
            {
                if (block.GetStateDetail<SpaceElevatorBaseStateDetail>(SpaceElevatorBaseStateDetail.BlockStateDetailKey) == null) return false;
                bindings = SpaceElevatorBindings(SpaceElevatorBaseInventoryComponent.SatelliteSlot, SpaceElevatorBaseInventoryComponent.GetCarriedPayloadItemIds(elevatorParam));
                return true;
            }

            // AppliesTo が両ブロックに絞るのでここへは来ない。来たら blockType とマスタの食い違いなので開発者へ出す
            // AppliesTo narrows to the two blocks, so reaching here means blockType and master disagree; tell the developer
            UnityEngine.Debug.LogError($"[SpecialInletBindingContributor] blockType は入れ口固定だがマスタの param が {param.GetType().Name}: Shift は取得待ちのまま");
            return false;
        }

        public static IReadOnlyList<InletSlotBinding> SpaceElevatorBindings(int satelliteSlot, IReadOnlyList<ItemId> carriedPayloadItemIds)
        {
            return new[] { new InletSlotBinding(satelliteSlot, carriedPayloadItemIds) };
        }
    }
}
```
（`BlockTypeConst.RocketLaunchPad`・`SpaceElevatorBase` の定数名は生成物の実物に合わせる。`Client.Ui.State.asmdef` は既に `Client.Game` を参照している〈`OpenBlockDetailTracker.TrackedBlock` が `BlockGameObject`〉。L0 では追跡ブロック〈`BlockGameObject`〉を組めないので `Contribute` は呼ばず、判断は `SpaceElevatorBindings` と L0-s4b の `Resolve` で見る〈下のテスト〉。本番の `Contribute`〈追跡ブロックの照らし合わせを含む〉は録画〈R-12 の `tier9-4b` の Shift の段〉で見る）

`ISpecialBlockDetailModel.cs`:
```csharp
using Client.Ui.State.BlockInventory.Special.Details;
using UniRx;

namespace Client.Ui.State.BlockInventory.Special
{
    // 開いたブロックの特殊族の詳細を配る口（View とテストの偽物が使う）
    // Port that publishes the open block's special-family details (used by views and test fakes)
    public interface ISpecialBlockDetailModel
    {
        IReadOnlyReactiveProperty<SpecialBlockDetailViewModel> Current { get; }
    }
}
```

`SpecialBlockDetailModel.cs`:
```csharp
using Client.Game.InGame.BeltTransport;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Source;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// パネル・追跡ブロック・状態・集約・ベルトの窓の進みのたびに同期で詳細を組み直す（間引かない [A-57]。Web BlockInventoryTopic の再発行と同じ契機）
    /// Rebuilds the details synchronously on every panel, tracked block, state, aggregate and belt-window change (no thinning [A-57]; same triggers as Web BlockInventoryTopic)
    /// </summary>
    public sealed class SpecialBlockDetailModel : ISpecialBlockDetailModel, IInitializable
    {
        private readonly IBlockInventoryPanelSource _panels;
        private readonly OpenBlockDetailTracker _tracker;
        private readonly BlockNetworkInfoCache _networkCache;
        private readonly BeltTransportClientCache _beltCache;
        private readonly ReactiveProperty<SpecialBlockDetailViewModel> _current = new(SpecialBlockDetailViewModel.Empty);
        private string _lastReportedMismatch;

        public IReadOnlyReactiveProperty<SpecialBlockDetailViewModel> Current => _current;

        public SpecialBlockDetailModel(IBlockInventoryPanelSource panels, OpenBlockDetailTracker tracker, BlockNetworkInfoCache networkCache, BeltTransportClientCache beltCache)
        {
            _panels = panels;
            _tracker = tracker;
            _networkCache = networkCache;
            _beltCache = beltCache;
        }

        public void Initialize()
        {
            // 変化を起こす源を購読し、そのたびに組み直す
            // Subscribe to every source of change and rebuild each time
            _panels.Current.Subscribe(_ => Rebuild());
            _tracker.OnTrackedBlockChanged.Subscribe(_ => Rebuild());
            _tracker.OnTrackedBlockStateChanged.Subscribe(_ => Rebuild());
            _networkCache.OnUpdated.Subscribe(_ => Rebuild());
            _beltCache.ExitStatistics.OnWindowAdvanced.Subscribe(_ => Rebuild());
            Rebuild();
        }

        private void Rebuild()
        {
            // 開いたブロックの識別は alpha のパネル、中身は追跡ブロック。インスタンスが一致する間だけ読む（D-26）
            // Identity comes from alpha's panel and contents from the tracked block; read only while the instances match (D-26)
            var block = _tracker.TrackedBlock;
            var open = _panels.Current.Value as OpenBlockInventoryPanel;
            var opened = open is { Error: BlockInventoryPanelError.None } && block != null;
            var matches = opened && block.BlockInstanceId.Equals(open.BlockInstanceId);
            ReportMismatch(opened && !matches ? $"panel {open.BlockInstanceId} != tracked {block.BlockInstanceId}" : null);
            // silent-ok: 閉じている間・開閉と差し替えの途中（どちらかが null・エラー）は空にする（区画は出ず本文は取得待ち） / 両方そろって食い違う場合は ReportMismatch が警告ログに出す
            _current.Value = matches ? SpecialBlockDetailReader.Read(block, _networkCache, _beltCache) : SpecialBlockDetailViewModel.Empty;
        }

        private void ReportMismatch(string mismatch)
        {
            // 開いたパネルと追跡ブロックの食い違いは、組み合わせが変わったときだけ警告する（集約の更新ごとに出さない）
            // Warn only when the panel/tracked mismatch pair changes, not on every aggregate update
            if (mismatch != null && mismatch != _lastReportedMismatch) Debug.LogWarning($"[SpecialBlockDetailModel] {mismatch}");
            _lastReportedMismatch = mismatch;
        }
    }
}
```
（購読はゲーム寿命で、AGENTS.md の既知の制約〈メインメニューへ戻らない〉により破棄しない。前例 `OpenBlockDetailTracker.Initialize`）

- [ ] **Step 3: コンパイルしテストを実行して通ることを確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.State\.BlockInventory\.Special\."`
Expected: ErrorCount 0 / PASS

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/Special
git commit -m "feat(uitk): 特殊族の詳細を源から読み、変化のたびに組み直す Model を足す（S4-special）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 4: 操作（`ISpecialBlockCommands`）・拒否理由・文言

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special/Commands/ISpecialBlockCommands.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special/Commands/SpecialBlockCommands.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special/Commands/IOpenSpecialBlock.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special/Commands/PanelOpenSpecialBlock.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special/Commands/ISpecialBlockRequestSender.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special/Commands/VanillaSpecialBlockRequestSender.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special/Commands/SpecialBlockResponseCodes.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui.State/Commands/UiCommandRejectReason.cs`（共有区画: enum と 3 つの switch の末尾に 4 値）
- Modify: `Localization/localization.csv`（`ui.commandRejected.*` の群の末尾 4 行）
- Modify: `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（`gen:i18n` の再生成）・`moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs`（印。生成物）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/PauseMenuCommandsTest.cs`（良性の一覧）
- Modify: `docs/uitk/TODO.md`（「## 追従記録」の末尾 1 行）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/Special/SpecialBlockCommandsTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/Special/SpecialBlockResponseCodesTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/Special/RecordingSpecialRequestSender.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/Special/FakeOpenSpecialBlock.cs`

**Interfaces:**
- Consumes: Task 3 の `SpecialNetworkReader.ToServerMode`、L0-s4b の `FilterSplitterLogic.ClickAction`・`FilterSlotClickAction`・`RocketCommand`、`PlayerGrabModel.Current`、`BlockNetworkInfoCache.ApplyFilterSplitterSnapshot`、`ClientContext.VanillaApi.Response.Block` の 4 つ（Global Constraints）、S4-alpha の `IBlockInventoryPanelSource`（`OpenBlockInventoryPanel` の `Position`・`BlockType`・`BlockInstanceId`・`Error`。D-26）
- Produces:
```csharp
namespace Client.Ui.State.BlockInventory.Special.Commands
{
    public interface ISpecialBlockCommands
    {
        UniTask<UiCommandResult> SetFilterMode(int directionIndex, FilterSplitterMode mode);
        UniTask<UiCommandResult> SetFilterItem(int directionIndex, int slotIndex, bool clear);   // clear=false は掴んでいる物を設定、掴みが空なら良性の拒否
        UniTask<UiCommandResult> SetElectricToGearMode(int modeIndex);
        UniTask<UiCommandResult> ConnectSpaceElevator();
        UniTask<UiCommandResult> SendRocketCommand(RocketCommand command);
    }
    public readonly struct OpenSpecialBlock { public OpenSpecialBlock(Vector3Int position, string blockType, BlockInstanceId instanceId); public Vector3Int Position { get; } public string BlockType { get; } public BlockInstanceId InstanceId { get; } }
    public interface IOpenSpecialBlock { bool TryGet(out OpenSpecialBlock block); }
    public enum SpecialBlockRequestOutcomeKind { Accepted, Stale, Failed }
    public readonly struct SpecialBlockRequestOutcome { public static SpecialBlockRequestOutcome Accepted { get; } public static SpecialBlockRequestOutcome Stale(string wireCode); public static SpecialBlockRequestOutcome Failed(string wireCode); public SpecialBlockRequestOutcomeKind Kind { get; } public string WireCode { get; } }
    public interface ISpecialBlockRequestSender
    {
        UniTask<SpecialBlockRequestOutcome> SetFilterMode(Vector3Int position, int directionIndex, FilterSplitterMode mode);
        UniTask<SpecialBlockRequestOutcome> SetFilterItem(Vector3Int position, int directionIndex, int slotIndex, ItemId itemId);
        UniTask<SpecialBlockRequestOutcome> SetElectricToGearMode(Vector3Int position, int modeIndex);
        UniTask<SpecialBlockRequestOutcome> ConnectSpaceElevator(Vector3Int position, BlockInstanceId instanceId);
        UniTask<SpecialBlockRequestOutcome> SendRocketCommand(Vector3Int position, RocketCommand command, BlockInstanceId instanceId);
    }
    public static class SpecialBlockResponseCodes
    {
        public static SpecialBlockRequestOutcome FromWait(PacketWaitCompletionReason reason, string timeoutCode, string invalidResponseCode); // Received は Accepted を返さず呼び手が応答を読む
        public static SpecialBlockRequestOutcome FromConnectResult(SpaceElevatorConnectResult result);
        public static SpecialBlockRequestOutcome FromRocketFailure(RocketLaunchPadCommandProtocol.RocketLaunchPadCommandFailureReason reason);
    }
}
// UiCommandRejectReason（共有区画の末尾）: SpecialBlockNotOpen・SpecialBlockRequestFailed・SpecialBlockCommandStale・FilterSplitterGrabEmpty
```

- [ ] **Step 1: テストの道具とテストを書く**

`FakeOpenSpecialBlock.cs`:
```csharp
using Client.Ui.State.BlockInventory.Special.Commands;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    // 開いたブロックの偽物（null なら開いていない）
    // Fake open block; null means nothing is open
    public sealed class FakeOpenSpecialBlock : IOpenSpecialBlock
    {
        private OpenSpecialBlock? _block;

        public void SetOpen(OpenSpecialBlock? block)
        {
            _block = block;
        }

        public bool TryGet(out OpenSpecialBlock block)
        {
            block = _block.GetValueOrDefault();
            return _block.HasValue;
        }
    }
}
```

`RecordingSpecialRequestSender.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Commands;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// 送信を 1 行の文字で記録し、テストが決めた結果を返す（既定は受理）
    /// Records each send as one line and returns the outcome chosen by the test (accepted by default)
    /// </summary>
    public sealed class RecordingSpecialRequestSender : ISpecialBlockRequestSender
    {
        private SpecialBlockRequestOutcome _next = SpecialBlockRequestOutcome.Accepted;

        public List<string> Sent { get; } = new();

        public void SetNextOutcome(SpecialBlockRequestOutcome outcome)
        {
            _next = outcome;
        }

        public UniTask<SpecialBlockRequestOutcome> SetFilterMode(Vector3Int position, int directionIndex, FilterSplitterMode mode) => Record($"filter_mode {position} {directionIndex} {mode}");
        public UniTask<SpecialBlockRequestOutcome> SetFilterItem(Vector3Int position, int directionIndex, int slotIndex, ItemId itemId) => Record($"filter_item {position} {directionIndex} {slotIndex} {itemId.AsPrimitive()}");
        public UniTask<SpecialBlockRequestOutcome> SetElectricToGearMode(Vector3Int position, int modeIndex) => Record($"electric_to_gear {position} {modeIndex}");
        public UniTask<SpecialBlockRequestOutcome> ConnectSpaceElevator(Vector3Int position, BlockInstanceId instanceId) => Record($"connect {position} {instanceId.AsPrimitive()}");
        public UniTask<SpecialBlockRequestOutcome> SendRocketCommand(Vector3Int position, RocketCommand command, BlockInstanceId instanceId) => Record($"rocket {position} {command} {instanceId.AsPrimitive()}");

        private UniTask<SpecialBlockRequestOutcome> Record(string line)
        {
            Sent.Add(line);
            return UniTask.FromResult(_next);
        }
    }
}
```

`SpecialBlockCommandsTest.cs`:
```csharp
using Client.Tests.Ui.State.Inventory;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Commands;
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Grab;
using Client.Ui.State.Items;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// 特殊族の操作が開いたブロックの種別を確かめ、掴みを読み、結果を UiCommandResult に写す（Web の各 ActionHandler と同じ判断）
    /// Special-family commands check the open block type, read the grab and map outcomes to UiCommandResult (same decisions as the Web action handlers)
    /// </summary>
    public class SpecialBlockCommandsTest
    {
        private static readonly Vector3Int Position = new(3, 0, 4);
        private RecordingSpecialRequestSender _sender;
        private FakeOpenSpecialBlock _open;
        private RecordingPlayerInventoryOperations _operations;
        private SpecialBlockCommands _commands;

        [SetUp]
        public void SetUp()
        {
            _sender = new RecordingSpecialRequestSender();
            _open = new FakeOpenSpecialBlock();
            _operations = new RecordingPlayerInventoryOperations(36, 0);
            var grab = new PlayerGrabModel(_operations);
            grab.Initialize();
            _commands = new SpecialBlockCommands(_open, _sender, grab);
        }

        [Test]
        public void 開いていなければ送らずに開いていない拒否を返す()
        {
            var result = _commands.SetElectricToGearMode(0).GetAwaiter().GetResult() as UiCommandRejected;
            Assert.AreEqual(UiCommandRejectReason.SpecialBlockNotOpen, result.Reason);
            Assert.AreEqual("block_not_open", result.ToWireCode());
            CollectionAssert.IsEmpty(_sender.Sent);
        }

        [Test]
        public void 種別の違うブロックには送らない()
        {
            Open(BlockMasterElement.BlockTypeConst.FilterSplitter);
            var result = _commands.SendRocketCommand(RocketCommand.Build).GetAwaiter().GetResult() as UiCommandRejected;
            Assert.AreEqual(UiCommandRejectReason.SpecialBlockNotOpen, result.Reason);
            Assert.AreEqual("invalid_block_type", result.ToWireCode());
            CollectionAssert.IsEmpty(_sender.Sent);
        }

        [Test]
        public void フィルタ枠は掴んでいる物を設定し右押下は空にし掴みが空の左押下は送らない()
        {
            Open(BlockMasterElement.BlockTypeConst.FilterSplitter);
            _operations.SetGrab(new SlotItem(new ItemId(4), 3));
            Assert.AreSame(UiCommandAccepted.Instance, _commands.SetFilterItem(2, 1, false).GetAwaiter().GetResult());
            Assert.AreSame(UiCommandAccepted.Instance, _commands.SetFilterItem(2, 1, true).GetAwaiter().GetResult());
            CollectionAssert.AreEqual(new[] { $"filter_item {Position} 2 1 4", $"filter_item {Position} 2 1 {ItemMaster.EmptyItemId.AsPrimitive()}" }, _sender.Sent);

            _operations.SetGrab(new SlotItem(ItemMaster.EmptyItemId, 0));
            var noop = _commands.SetFilterItem(2, 1, false).GetAwaiter().GetResult() as UiCommandRejected;
            Assert.AreEqual(UiCommandRejectReason.FilterSplitterGrabEmpty, noop.Reason);
            Assert.IsTrue(UiCommandRejectReasonTraits.IsBenign(noop.Reason));
            Assert.AreEqual(2, _sender.Sent.Count);
        }

        [Test]
        public void モードと出力モードと接続と合図は位置とインスタンスを付けて送る()
        {
            Open(BlockMasterElement.BlockTypeConst.FilterSplitter);
            _commands.SetFilterMode(0, FilterSplitterMode.Blacklist).GetAwaiter().GetResult();
            Open(BlockMasterElement.BlockTypeConst.ElectricToGearGenerator);
            _commands.SetElectricToGearMode(1).GetAwaiter().GetResult();
            Open(BlockMasterElement.BlockTypeConst.SpaceElevatorBase);
            _commands.ConnectSpaceElevator().GetAwaiter().GetResult();
            Open(BlockMasterElement.BlockTypeConst.RocketLaunchPad);
            _commands.SendRocketCommand(RocketCommand.Launch).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { $"filter_mode {Position} 0 Blacklist", $"electric_to_gear {Position} 1", $"connect {Position} 77", $"rocket {Position} Launch 77" }, _sender.Sent);
        }

        [Test]
        public void 古い合図は良性で失敗は理由のコード付きの拒否()
        {
            Open(BlockMasterElement.BlockTypeConst.RocketLaunchPad);
            _sender.SetNextOutcome(SpecialBlockRequestOutcome.Stale("rocket_command_not_awaiting_build"));
            var stale = _commands.SendRocketCommand(RocketCommand.Build).GetAwaiter().GetResult() as UiCommandRejected;
            Assert.AreEqual(UiCommandRejectReason.SpecialBlockCommandStale, stale.Reason);
            Assert.IsTrue(UiCommandRejectReasonTraits.IsBenign(stale.Reason));
            Assert.AreEqual("rocket_command_not_awaiting_build", stale.ToWireCode());

            _sender.SetNextOutcome(SpecialBlockRequestOutcome.Failed("rocket_command_missing_materials"));
            var failed = _commands.SendRocketCommand(RocketCommand.Build).GetAwaiter().GetResult() as UiCommandRejected;
            Assert.AreEqual(UiCommandRejectReason.SpecialBlockRequestFailed, failed.Reason);
            Assert.IsFalse(UiCommandRejectReasonTraits.IsBenign(failed.Reason));
            Assert.AreEqual("rocket_command_missing_materials", failed.ToWireCode());
        }

        private void Open(string blockType)
        {
            _open.SetOpen(new OpenSpecialBlock(Position, blockType, new BlockInstanceId(77)));
        }
    }
}
```

`SpecialBlockResponseCodesTest.cs`:
```csharp
using System;
using Client.Network.API;
using Client.Ui.State.BlockInventory.Special.Commands;
using Game.Block.Interface.Component;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using FailureReason = Server.Protocol.PacketResponse.RocketLaunchPadCommandProtocol.RocketLaunchPadCommandFailureReason;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// 通信結果とサーバーの拒否理由を Web の橋と同じ失敗コードへ写し、Web の BENIGN_ERRORS と同じものだけを古い操作（良性）にする
    /// Maps transport results and server refusals to the Web bridge failure codes; only Web BENIGN_ERRORS become stale (benign)
    /// </summary>
    public class SpecialBlockResponseCodesTest
    {
        [Test]
        public void 届かなかった応答は時間切れと壊れた応答を分ける()
        {
            Assert.AreEqual("connect_timeout", SpecialBlockResponseCodes.FromWait(PacketWaitCompletionReason.Timeout, "connect_timeout", "connect_invalid_response").WireCode);
            Assert.AreEqual("connect_invalid_response", SpecialBlockResponseCodes.FromWait(PacketWaitCompletionReason.DeserializeFailed, "connect_timeout", "connect_invalid_response").WireCode);
            Assert.AreEqual(SpecialBlockRequestOutcomeKind.Failed, SpecialBlockResponseCodes.FromWait(PacketWaitCompletionReason.Timeout, "a", "b").Kind);
        }

        [TestCase(SpaceElevatorConnectResult.Connected, SpecialBlockRequestOutcomeKind.Accepted, "")]
        [TestCase(SpaceElevatorConnectResult.AlreadyConnected, SpecialBlockRequestOutcomeKind.Stale, "already_connected")]
        [TestCase(SpaceElevatorConnectResult.InsufficientCable, SpecialBlockRequestOutcomeKind.Failed, "insufficient_cable")]
        [TestCase(SpaceElevatorConnectResult.InstanceMismatch, SpecialBlockRequestOutcomeKind.Failed, "connect_instance_mismatch")]
        public void 接続の結果を写す(SpaceElevatorConnectResult result, SpecialBlockRequestOutcomeKind kind, string wire)
        {
            var outcome = SpecialBlockResponseCodes.FromConnectResult(result);
            Assert.AreEqual(kind, outcome.Kind);
            Assert.AreEqual(wire, outcome.WireCode);
        }

        [Test]
        public void 合図の拒否はどの理由もコードを持ち待ち以外の2つだけが古い操作()
        {
            foreach (FailureReason reason in Enum.GetValues(typeof(FailureReason)))
            {
                // None は成功の応答だけが持つ（Success=true）ので写さない
                // None only appears on a successful response (Success=true), so it is not mapped
                if (reason == FailureReason.None) continue;
                var outcome = SpecialBlockResponseCodes.FromRocketFailure(reason);
                StringAssert.StartsWith("rocket_command_", outcome.WireCode, reason.ToString());
                var stale = reason is FailureReason.NotAwaitingBuildCommand or FailureReason.NotAwaitingLaunchCommand;
                Assert.AreEqual(stale ? SpecialBlockRequestOutcomeKind.Stale : SpecialBlockRequestOutcomeKind.Failed, outcome.Kind, reason.ToString());
            }
        }
    }
}
```
（`RocketLaunchPadCommandFailureReason.None` は成功の応答だけが持つ値で、`FromRocketFailure` は `ArgumentOutOfRangeException` にする〈Web の橋も switch の既定で投げる〉。`BlockInstanceId` の生成〈`new BlockInstanceId(77)`〉と `AsPrimitive()` は UnitGenerator の実物）

`PauseMenuCommandsTest.cs` の「良性の拒否理由は言語の選択済みと持ち物の移動と分割の取りこぼしだけ」を、名前を「良性の拒否理由は言語の選択済みと持ち物の移動と分割の取りこぼしと特殊族の古い操作と空の掴みだけ」に変え、`benign` の配列の末尾に `UiCommandRejectReason.SpecialBlockCommandStale, UiCommandRejectReason.FilterSplitterGrabEmpty` を足す（他レーンが同じ配列に足していれば両方を残す）。コメントの 2 行も「…と、Web の BENIGN_ERRORS の発射台の待ち外れ・接続済みと、Web が送らない空の掴みのフィルタ設定は…」に合わせる

- [ ] **Step 2: 拒否理由と文言を足す**

`UiCommandRejectReason.cs` の enum の末尾（その時点の最後の値の次）に 4 値を足し、3 つの switch の末尾にも 1 行ずつ足す:
```csharp
        SpecialBlockNotOpen,
        SpecialBlockRequestFailed,
        SpecialBlockCommandStale,
        FilterSplitterGrabEmpty,
```
`IsBenign`:
```csharp
                UiCommandRejectReason.SpecialBlockNotOpen => false,
                UiCommandRejectReason.SpecialBlockRequestFailed => false,
                UiCommandRejectReason.SpecialBlockCommandStale => true,
                UiCommandRejectReason.FilterSplitterGrabEmpty => true,
```
`ToLocalizationKey`:
```csharp
                UiCommandRejectReason.SpecialBlockNotOpen => LocalizationKeys.Ui.CommandRejected.SpecialBlockNotOpen,
                UiCommandRejectReason.SpecialBlockRequestFailed => LocalizationKeys.Ui.CommandRejected.SpecialBlockRequestFailed,
                UiCommandRejectReason.SpecialBlockCommandStale => LocalizationKeys.Ui.CommandRejected.SpecialBlockCommandStale,
                UiCommandRejectReason.FilterSplitterGrabEmpty => LocalizationKeys.Ui.CommandRejected.FilterSplitterGrabEmpty,
```
`ToWireCode`（Detail が空でなければ `UiCommandRejected.ToWireCode` が Detail を使う。これは Detail が空のときの既定）:
```csharp
                UiCommandRejectReason.SpecialBlockNotOpen => "block_not_open",
                UiCommandRejectReason.SpecialBlockRequestFailed => "special_block_request_failed",
                UiCommandRejectReason.SpecialBlockCommandStale => "special_block_command_stale",
                UiCommandRejectReason.FilterSplitterGrabEmpty => "grab_empty",
```
`IsBenign` の上のコメント 2 行を「選択済み再選択と、Web の BENIGN_ERRORS にある持ち物の移動・分割・特殊族の古い操作の取りこぼしと、Web が送らない空の掴みのフィルタ設定は良性」（英語行も同じ意味）に直す。

`Localization/localization.csv` の `ui.commandRejected.*` の群の末尾（その時点の群の最後の行の次）に:
```csv
ui.commandRejected.specialBlockNotOpen,The block screen is not open,The block screen is not open,ブロックの画面が開いていません,Der Blockbildschirm ist nicht geöffnet,블록 화면이 열려 있지 않습니다
ui.commandRejected.specialBlockRequestFailed,The block did not accept the operation,The block did not accept the operation,ブロックが操作を受け付けませんでした,Der Block hat die Aktion nicht angenommen,블록이 조작을 받아들이지 않았습니다
ui.commandRejected.specialBlockCommandStale,The block has already changed,The block has already changed,ブロックの状態がすでに変わっています,Der Block hat sich bereits geändert,블록 상태가 이미 바뀌었습니다
ui.commandRejected.filterSplitterGrabEmpty,Hold an item to set the filter,Hold an item to set the filter,フィルタに設定するアイテムを持ってください,Halte einen Gegenstand um den Filter zu setzen,필터에 설정할 아이템을 들어 주세요
```
Run:
```bash
pnpm --dir moorestech_web/webui run gen:i18n
uloop compile --project-path ./moorestech_client
git diff --stat -- moorestech_web/webui/src/shared/i18n/generated/ moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs
```
Expected: `localizationKeys.ts` に 4 キーが増え（`contentKeys.ts` は変わらない）、`_CompileRequester.cs` の印が変わる（変わらなければ `uloop compile` をもう 1 回流してから見直す）。コンパイルエラーは Step 3 の実装が無い分だけ（`SpecialBlockCommands` 等）

- [ ] **Step 3: 実装を書く**

`Commands/ISpecialBlockCommands.cs`:
```csharp
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.Commands;
using Cysharp.Threading.Tasks;

namespace Client.Ui.State.BlockInventory.Special.Commands
{
    /// <summary>
    /// 特殊族の操作（Web の filter_splitter.*・electric_to_gear.set_output_mode・space_elevator.connect・rocket_launch_pad.command）。表示は楽観更新せずサーバーの後続の状態で変わる
    /// Special-family commands (the Web filter_splitter.*, electric_to_gear.set_output_mode, space_elevator.connect, rocket_launch_pad.command); the display is not updated optimistically
    /// </summary>
    public interface ISpecialBlockCommands
    {
        UniTask<UiCommandResult> SetFilterMode(int directionIndex, FilterSplitterMode mode);
        UniTask<UiCommandResult> SetFilterItem(int directionIndex, int slotIndex, bool clear);
        UniTask<UiCommandResult> SetElectricToGearMode(int modeIndex);
        UniTask<UiCommandResult> ConnectSpaceElevator();
        UniTask<UiCommandResult> SendRocketCommand(RocketCommand command);
    }
}
```

`Commands/IOpenSpecialBlock.cs`:
```csharp
using Game.Block.Interface;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Special.Commands
{
    // 開いたブロックの位置・種別・インスタンス
    // Position, type and instance of the open block
    public readonly struct OpenSpecialBlock
    {
        public Vector3Int Position { get; }
        public string BlockType { get; }
        public BlockInstanceId InstanceId { get; }

        public OpenSpecialBlock(Vector3Int position, string blockType, BlockInstanceId instanceId)
        {
            Position = position;
            BlockType = blockType;
            InstanceId = instanceId;
        }
    }

    // 開いたブロックを読む口（開いていない・識別が未取得なら false）
    // Port that reads the open block; false when nothing is open or its identity is not fetched yet
    public interface IOpenSpecialBlock
    {
        bool TryGet(out OpenSpecialBlock block);
    }
}
```

`Commands/PanelOpenSpecialBlock.cs`:
```csharp
using Client.Ui.State.BlockInventory.Panel;

namespace Client.Ui.State.BlockInventory.Special.Commands
{
    /// <summary>
    /// 開いたブロックの識別を S4-alpha のパネルの源から読む（詳細の Model・Shift の寄与と同じ識別。D-26）
    /// Reads the open block's identity from S4-alpha's panel source (the same identity as the detail model and the Shift contributor. D-26)
    /// </summary>
    public sealed class PanelOpenSpecialBlock : IOpenSpecialBlock
    {
        private readonly IBlockInventoryPanelSource _panels;

        public PanelOpenSpecialBlock(IBlockInventoryPanelSource panels)
        {
            _panels = panels;
        }

        public bool TryGet(out OpenSpecialBlock block)
        {
            block = default;
            if (_panels.Current.Value is not OpenBlockInventoryPanel { Error: BlockInventoryPanelError.None } open) return false;
            block = new OpenSpecialBlock(open.Position, open.BlockType, open.BlockInstanceId);
            return true;
        }
    }
}
```
（偽の経路〈閉じている・取得失敗のパネル〉は呼び手 `SpecialBlockCommands` が `SpecialBlockNotOpen` の拒否にし、`UiCommandResultToaster` が LogWarning とトーストを出す。ここではログを出さない）

`Commands/ISpecialBlockRequestSender.cs`:
```csharp
using Client.Ui.State.BlockInventory.Logic.Special;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Special.Commands
{
    public enum SpecialBlockRequestOutcomeKind
    {
        Accepted,
        Stale,
        Failed,
    }

    // 送信の結果。古い操作（良性）と失敗は Web の橋と同じ失敗コードを持つ
    // Send outcome; stale (benign) and failed outcomes carry the same failure code as the Web bridge
    public readonly struct SpecialBlockRequestOutcome
    {
        public SpecialBlockRequestOutcomeKind Kind { get; }
        public string WireCode { get; }

        private SpecialBlockRequestOutcome(SpecialBlockRequestOutcomeKind kind, string wireCode)
        {
            Kind = kind;
            WireCode = wireCode;
        }

        public static SpecialBlockRequestOutcome Accepted => new(SpecialBlockRequestOutcomeKind.Accepted, "");
        public static SpecialBlockRequestOutcome Stale(string wireCode) => new(SpecialBlockRequestOutcomeKind.Stale, wireCode);
        public static SpecialBlockRequestOutcome Failed(string wireCode) => new(SpecialBlockRequestOutcomeKind.Failed, wireCode);
    }

    // サーバーへの送信の口（テストは記録係に差し替える）
    // Port that sends to the server; tests swap in a recorder
    public interface ISpecialBlockRequestSender
    {
        UniTask<SpecialBlockRequestOutcome> SetFilterMode(Vector3Int position, int directionIndex, FilterSplitterMode mode);
        UniTask<SpecialBlockRequestOutcome> SetFilterItem(Vector3Int position, int directionIndex, int slotIndex, ItemId itemId);
        UniTask<SpecialBlockRequestOutcome> SetElectricToGearMode(Vector3Int position, int modeIndex);
        UniTask<SpecialBlockRequestOutcome> ConnectSpaceElevator(Vector3Int position, BlockInstanceId instanceId);
        UniTask<SpecialBlockRequestOutcome> SendRocketCommand(Vector3Int position, RocketCommand command, BlockInstanceId instanceId);
    }
}
```

`Commands/SpecialBlockResponseCodes.cs`:
```csharp
using System;
using Client.Network.API;
using Game.Block.Interface.Component;
using Server.Protocol.PacketResponse;
using FailureReason = Server.Protocol.PacketResponse.RocketLaunchPadCommandProtocol.RocketLaunchPadCommandFailureReason;

namespace Client.Ui.State.BlockInventory.Special.Commands
{
    /// <summary>
    /// 通信結果・サーバーの拒否理由を Web の橋（SpaceElevatorConnectActionHandler・RocketLaunchPadCommandActionHandler）と同じ失敗コードへ写す。Web の BENIGN_ERRORS のコードだけが古い操作
    /// Maps transport results and server refusals to the same failure codes as the Web bridge; only Web BENIGN_ERRORS codes are stale
    /// </summary>
    public static class SpecialBlockResponseCodes
    {
        public static SpecialBlockRequestOutcome FromWait(PacketWaitCompletionReason reason, string timeoutCode, string invalidResponseCode)
        {
            return reason switch
            {
                PacketWaitCompletionReason.Timeout => SpecialBlockRequestOutcome.Failed(timeoutCode),
                PacketWaitCompletionReason.DeserializeFailed => SpecialBlockRequestOutcome.Failed(invalidResponseCode),
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "受信済みは応答の中身で写す"),
            };
        }

        public static SpecialBlockRequestOutcome FromConnectResult(SpaceElevatorConnectResult result)
        {
            return result switch
            {
                SpaceElevatorConnectResult.Connected => SpecialBlockRequestOutcome.Accepted,
                SpaceElevatorConnectResult.AlreadyConnected => SpecialBlockRequestOutcome.Stale("already_connected"),
                SpaceElevatorConnectResult.InsufficientCable => SpecialBlockRequestOutcome.Failed("insufficient_cable"),
                SpaceElevatorConnectResult.NotSpaceElevatorBase => SpecialBlockRequestOutcome.Failed("not_space_elevator_base"),
                SpaceElevatorConnectResult.InvalidRequest => SpecialBlockRequestOutcome.Failed("connect_invalid_request"),
                SpaceElevatorConnectResult.BlockNotFound => SpecialBlockRequestOutcome.Failed("connect_block_not_found"),
                SpaceElevatorConnectResult.InstanceMismatch => SpecialBlockRequestOutcome.Failed("connect_instance_mismatch"),
                _ => throw new ArgumentOutOfRangeException(nameof(result), result, "未知の接続結果"),
            };
        }

        public static SpecialBlockRequestOutcome FromRocketFailure(FailureReason reason)
        {
            return reason switch
            {
                FailureReason.BlockNotFound => SpecialBlockRequestOutcome.Failed("rocket_command_block_not_found"),
                FailureReason.NotRocketLaunchPad => SpecialBlockRequestOutcome.Failed("rocket_command_not_launch_pad"),
                FailureReason.UnknownOperation => SpecialBlockRequestOutcome.Failed("rocket_command_unknown_operation"),
                FailureReason.InvalidRequest => SpecialBlockRequestOutcome.Failed("rocket_command_invalid_request"),
                FailureReason.NotAwaitingBuildCommand => SpecialBlockRequestOutcome.Stale("rocket_command_not_awaiting_build"),
                FailureReason.NotAwaitingLaunchCommand => SpecialBlockRequestOutcome.Stale("rocket_command_not_awaiting_launch"),
                FailureReason.MissingMaterials => SpecialBlockRequestOutcome.Failed("rocket_command_missing_materials"),
                FailureReason.MissingPayload => SpecialBlockRequestOutcome.Failed("rocket_command_missing_payload"),
                FailureReason.OutputFull => SpecialBlockRequestOutcome.Failed("rocket_command_output_full"),
                FailureReason.InstanceMismatch => SpecialBlockRequestOutcome.Failed("rocket_command_instance_mismatch"),
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "未知の合図の拒否理由"),
            };
        }
    }
}
```

`Commands/VanillaSpecialBlockRequestSender.cs`:
```csharp
using System.Threading;
using Client.Game.InGame.Context;
using Client.Network.API;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Source;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;
using Operation = Server.Protocol.PacketResponse.RocketLaunchPadCommandProtocol.RocketLaunchPadCommandOperation;

namespace Client.Ui.State.BlockInventory.Special.Commands
{
    /// <summary>
    /// ClientContext.VanillaApi で送り、応答を Web の橋と同じ失敗コードへ写す。分配器の応答は集約キャッシュへ当てる（Web FilterSplitter*ActionHandler と同じ）
    /// Sends through ClientContext.VanillaApi and maps responses to the Web bridge codes; splitter responses are applied to the aggregate cache (same as the Web handlers)
    /// </summary>
    public sealed class VanillaSpecialBlockRequestSender : ISpecialBlockRequestSender
    {
        private readonly BlockNetworkInfoCache _networkCache;

        public VanillaSpecialBlockRequestSender(BlockNetworkInfoCache networkCache)
        {
            _networkCache = networkCache;
        }

        public async UniTask<SpecialBlockRequestOutcome> SetFilterMode(Vector3Int position, int directionIndex, FilterSplitterMode mode)
        {
            var request = FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetModeRequest(position, directionIndex, SpecialNetworkReader.ToServerMode(mode));
            return ApplyFilterResponse(await ClientContext.VanillaApi.Response.Block.SendFilterSplitterStateRequest(request, CancellationToken.None));
        }

        public async UniTask<SpecialBlockRequestOutcome> SetFilterItem(Vector3Int position, int directionIndex, int slotIndex, ItemId itemId)
        {
            var request = FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetFilterItemRequest(position, directionIndex, slotIndex, itemId);
            return ApplyFilterResponse(await ClientContext.VanillaApi.Response.Block.SendFilterSplitterStateRequest(request, CancellationToken.None));
        }

        public async UniTask<SpecialBlockRequestOutcome> SetElectricToGearMode(Vector3Int position, int modeIndex)
        {
            var response = await ClientContext.VanillaApi.Response.Block.SetElectricToGearOutputMode(position, modeIndex, CancellationToken.None);
            return response != null && response.Success ? SpecialBlockRequestOutcome.Accepted : SpecialBlockRequestOutcome.Failed("mode_request_failed");
        }

        public async UniTask<SpecialBlockRequestOutcome> ConnectSpaceElevator(Vector3Int position, BlockInstanceId instanceId)
        {
            var (response, reason) = await ClientContext.VanillaApi.Response.Block.ConnectSpaceElevator(position, instanceId, CancellationToken.None);
            if (reason != PacketWaitCompletionReason.Received) return SpecialBlockResponseCodes.FromWait(reason, "connect_timeout", "connect_invalid_response");
            return response == null ? SpecialBlockRequestOutcome.Failed("connect_invalid_response") : SpecialBlockResponseCodes.FromConnectResult(response.Result);
        }

        public async UniTask<SpecialBlockRequestOutcome> SendRocketCommand(Vector3Int position, RocketCommand command, BlockInstanceId instanceId)
        {
            var operation = command == RocketCommand.Build ? Operation.Build : Operation.Launch;
            var (response, reason) = await ClientContext.VanillaApi.Response.Block.SendRocketLaunchPadCommand(position, operation, instanceId, CancellationToken.None);
            if (reason != PacketWaitCompletionReason.Received) return SpecialBlockResponseCodes.FromWait(reason, "rocket_command_timeout", "rocket_command_invalid_response");
            if (response == null) return SpecialBlockRequestOutcome.Failed("rocket_command_invalid_response");
            return response.Success ? SpecialBlockRequestOutcome.Accepted : SpecialBlockResponseCodes.FromRocketFailure(response.FailureReason);
        }

        private SpecialBlockRequestOutcome ApplyFilterResponse(FilterSplitterStateProtocol.FilterSplitterStateResponse response)
        {
            // 受理した応答は最新の状態として集約へ当て、Model の組み直しを起こす
            // Apply an accepted response to the aggregate as the latest state, which triggers the model rebuild
            if (response == null || !response.Success) return SpecialBlockRequestOutcome.Failed("filter_request_failed");
            _networkCache.ApplyFilterSplitterSnapshot(response);
            return SpecialBlockRequestOutcome.Accepted;
        }
    }
}
```
（`RocketCommand` が 2 値でなくなったら `Build ? … : Launch` の三項を switch に変える。`ClientContext` の namespace〈`Client.Game.InGame.Context`〉は Web の橋の `using` と同じ）

`Commands/SpecialBlockCommands.cs`:
```csharp
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Grab;
using Core.Master;
using Cysharp.Threading.Tasks;
using Mooresmaster.Model.BlocksModule;

namespace Client.Ui.State.BlockInventory.Special.Commands
{
    /// <summary>
    /// 特殊族の操作。開いたブロックの種別を確かめ、掴みを読み、送信の結果を UiCommandResult へ写す
    /// Special-family commands: checks the open block type, reads the grab and maps send outcomes to UiCommandResult
    /// </summary>
    public sealed class SpecialBlockCommands : ISpecialBlockCommands
    {
        private readonly IOpenSpecialBlock _openBlock;
        private readonly ISpecialBlockRequestSender _sender;
        private readonly PlayerGrabModel _grab;

        public SpecialBlockCommands(IOpenSpecialBlock openBlock, ISpecialBlockRequestSender sender, PlayerGrabModel grab)
        {
            _openBlock = openBlock;
            _sender = sender;
            _grab = grab;
        }

        public async UniTask<UiCommandResult> SetFilterMode(int directionIndex, FilterSplitterMode mode)
        {
            if (!TryOpen(BlockMasterElement.BlockTypeConst.FilterSplitter, out var block, out var rejected)) return rejected;
            return ToResult(await _sender.SetFilterMode(block.Position, directionIndex, mode));
        }

        public async UniTask<UiCommandResult> SetFilterItem(int directionIndex, int slotIndex, bool clear)
        {
            if (!TryOpen(BlockMasterElement.BlockTypeConst.FilterSplitter, out var block, out var rejected)) return rejected;

            // 掴みが空の左押下は送らない（Web filterSlotClickAction の noop・uGUI と同じ）
            // A left press with an empty grab sends nothing (Web filterSlotClickAction noop, same as uGUI)
            var grab = _grab.Current.Value;
            var action = FilterSplitterLogic.ClickAction(grab.Count, clear);
            if (action == FilterSlotClickAction.Noop) return new UiCommandRejected(UiCommandRejectReason.FilterSplitterGrabEmpty, "");
            var itemId = action == FilterSlotClickAction.Clear ? ItemMaster.EmptyItemId : grab.ItemId;
            return ToResult(await _sender.SetFilterItem(block.Position, directionIndex, slotIndex, itemId));
        }

        public async UniTask<UiCommandResult> SetElectricToGearMode(int modeIndex)
        {
            if (!TryOpen(BlockMasterElement.BlockTypeConst.ElectricToGearGenerator, out var block, out var rejected)) return rejected;
            return ToResult(await _sender.SetElectricToGearMode(block.Position, modeIndex));
        }

        public async UniTask<UiCommandResult> ConnectSpaceElevator()
        {
            if (!TryOpen(BlockMasterElement.BlockTypeConst.SpaceElevatorBase, out var block, out var rejected)) return rejected;
            return ToResult(await _sender.ConnectSpaceElevator(block.Position, block.InstanceId));
        }

        public async UniTask<UiCommandResult> SendRocketCommand(RocketCommand command)
        {
            if (!TryOpen(BlockMasterElement.BlockTypeConst.RocketLaunchPad, out var block, out var rejected)) return rejected;
            return ToResult(await _sender.SendRocketCommand(block.Position, command, block.InstanceId));
        }

        // 開いていない・種別が違うなら送らずに拒否する（Web の block_not_open・invalid_block_type）
        // Refuse without sending when nothing is open or the type differs (Web block_not_open, invalid_block_type)
        private bool TryOpen(string expectedBlockType, out OpenSpecialBlock block, out UiCommandResult rejected)
        {
            rejected = null;
            if (!_openBlock.TryGet(out block))
            {
                rejected = new UiCommandRejected(UiCommandRejectReason.SpecialBlockNotOpen, "block_not_open");
                return false;
            }
            if (block.BlockType == expectedBlockType) return true;
            rejected = new UiCommandRejected(UiCommandRejectReason.SpecialBlockNotOpen, "invalid_block_type");
            return false;
        }

        private static UiCommandResult ToResult(SpecialBlockRequestOutcome outcome)
        {
            return outcome.Kind switch
            {
                SpecialBlockRequestOutcomeKind.Accepted => UiCommandAccepted.Instance,
                SpecialBlockRequestOutcomeKind.Stale => new UiCommandRejected(UiCommandRejectReason.SpecialBlockCommandStale, outcome.WireCode),
                _ => new UiCommandRejected(UiCommandRejectReason.SpecialBlockRequestFailed, outcome.WireCode),
            };
        }
    }
}
```
（`BlockMasterElement.BlockTypeConst` の 4 定数〈`FilterSplitter`・`ElectricToGearGenerator`・`SpaceElevatorBase`・`RocketLaunchPad`〉は L0-s4b の `BlockComponentRegistry` が使う実物。名前が違えば実物に合わせる。拒否の理由は呼び手の View が `UiCommandResultToaster.Report` へ渡し、Toaster が LogWarning〈開発者〉と、良性でなければトースト〈利用者〉を出す）

- [ ] **Step 4: コンパイルしテストを実行して通ることを確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.State\.(BlockInventory\.Special\.|PauseMenuCommandsTest|UiCommandResultToasterTest)"`
Expected: ErrorCount 0 / PASS（`SpecialBlockCommandsTest` 5・`SpecialBlockResponseCodesTest` 3〈TestCase を含め 6〉・良性の一覧）。`localization.csv` のキー数を数えるテスト（在れば `Client.Tests.Localization`）も同じ回で回す

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State moorestech_client/Assets/Scripts/Client.Tests/Ui/State Localization/localization.csv moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs
git commit -m "feat(uitk): 特殊族の操作を UiCommandResult で返し、Web の良性の拒否を写す（S4-special）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```
台帳 `docs/uitk/TODO.md` の「## 追従記録」の末尾に `| 1 | <このコミットの 10 桁> | 写した | S4-special の特殊族の拒否理由の文言。Web 側は Localization/localization.csv への追加に連動した生成物 src/shared/i18n/generated/localizationKeys.ts の再生成だけで、写し先は同じコミットの C# UiCommandRejectReasonTraits と CSV |` を足し、`git add docs/uitk/TODO.md && git commit -m "docs(uitk): 拒否理由の文言の再生成を追従記録へ（S4-special）"`（末尾に帰属行）

---

### Task 5: ←/→ の押下の流れ（`Client.Input/ArrowKeys`）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Input/ArrowKeys/InputSystemArrowKeyStream.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Input/ArrowKeys/IHorizontalArrowKeySource.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Input/ArrowKeys/InputSystemArrowKeyStreamTest.cs`

**Interfaces:**
- Consumes: Unity Input System（`InputSystem.onEvent`・`Keyboard.current`）。前例 `moorestech_client/Assets/Scripts/Client.Input/Pointer/InputSystemPointerButtonStream.cs`（縁だけを流す・デバイスのリセットで記憶を下ろす・無ければ警告）
- Produces:
```csharp
namespace Client.Input.ArrowKeys
{
    public enum HorizontalArrowKey { Left, Right }
    public interface IHorizontalArrowKeySource { IObservable<HorizontalArrowKey> OnArrowPressed { get; } }
    public sealed class InputSystemArrowKeyStream : IHorizontalArrowKeySource { public InputSystemArrowKeyStream(); }
}
```
（namespace を `Client.Input.Keyboard` にしない。`UnityEngine.InputSystem.Keyboard` の型名とぶつかる。D-7）

- [ ] **Step 1: テストを書く**

`InputSystemArrowKeyStreamTest.cs`:
```csharp
using System.Collections.Generic;
using Client.Input.ArrowKeys;
using NUnit.Framework;
using UniRx;
using UnityEngine.InputSystem;

namespace Client.Tests.Input.ArrowKeys
{
    /// <summary>
    /// ←/→ を押した瞬間だけ 1 回流し、押しっぱなし・他のキー・離しでは流さない（Web の window keydown の代わり）
    /// Publishes once only at the press of ← or →; holding, other keys and releases publish nothing (stands in for Web window keydown)
    /// </summary>
    public class InputSystemArrowKeyStreamTest : InputTestFixture
    {
        [Test]
        public void 左右の矢印の押下だけを1回ずつ流す()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var pressed = new List<HorizontalArrowKey>();
            new InputSystemArrowKeyStream().OnArrowPressed.Subscribe(pressed.Add);

            Press(keyboard.rightArrowKey);
            Set(keyboard.aKey, 1f);
            Release(keyboard.rightArrowKey);
            Press(keyboard.leftArrowKey);
            Press(keyboard.upArrowKey);

            CollectionAssert.AreEqual(new[] { HorizontalArrowKey.Right, HorizontalArrowKey.Left }, pressed);
        }

        [Test]
        public void リセットの後に押し直すとまた流す()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var pressed = new List<HorizontalArrowKey>();
            new InputSystemArrowKeyStream().OnArrowPressed.Subscribe(pressed.Add);

            Press(keyboard.rightArrowKey);
            InputSystem.ResetDevice(keyboard);
            Press(keyboard.rightArrowKey);

            Assert.AreEqual(2, pressed.Count);
        }
    }
}
```

- [ ] **Step 2: 実装を書く**

`IHorizontalArrowKeySource.cs`:
```csharp
using System;

namespace Client.Input.ArrowKeys
{
    public enum HorizontalArrowKey
    {
        Left,
        Right,
    }

    // ←/→ の押下を流す口（画面のテストは偽物に差し替える）
    // Port that publishes ← / → presses; screen tests swap in a fake
    public interface IHorizontalArrowKeySource
    {
        IObservable<HorizontalArrowKey> OnArrowPressed { get; }
    }
}
```

`InputSystemArrowKeyStream.cs`:
```csharp
using System;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Client.Input.ArrowKeys
{
    /// <summary>
    /// キーボードの ← / → が押された瞬間に 1 回流す（UI のフォーカスに依らない。Web の window keydown の代わり）
    /// Publishes once at the moment ← or → is pressed, independent of UI focus (stands in for Web window keydown)
    /// </summary>
    public sealed class InputSystemArrowKeyStream : IHorizontalArrowKeySource
    {
        private readonly Subject<HorizontalArrowKey> _onArrowPressed = new();
        private bool _leftWasPressed;
        private bool _rightWasPressed;

        public IObservable<HorizontalArrowKey> OnArrowPressed => _onArrowPressed;

        public InputSystemArrowKeyStream()
        {
            // キーボード無しなら理由を警告
            // Warn why when there is no keyboard
            if (Keyboard.current == null) Debug.LogWarning("[InputSystemArrowKeyStream] Keyboard.current is null; no arrow press will be published until a keyboard sends events");
            InputSystem.onEvent += OnInputEvent;
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            // 状態イベントから左右の矢印の押下の縁だけを流す
            // Publish only the press edges of the left and right arrows from state events
            if (device != Keyboard.current) return;
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;
            PublishEdge(Keyboard.current.leftArrowKey, HorizontalArrowKey.Left, ref _leftWasPressed);
            PublishEdge(Keyboard.current.rightArrowKey, HorizontalArrowKey.Right, ref _rightWasPressed);

            #region Internal

            void PublishEdge(UnityEngine.InputSystem.Controls.KeyControl key, HorizontalArrowKey arrow, ref bool wasPressed)
            {
                // silent-ok: この状態イベントにキーの値が無い（部分の DeltaStateEvent）なら前の押下を保つ / 押下が流れないなら L0 と録画（R-12）のベルトの段で気づく
                if (!key.ReadValueFromEvent(eventPtr, out var value)) return;
                var pressed = 0.5f <= value;
                var edge = pressed && !wasPressed;
                wasPressed = pressed;
                if (edge) _onArrowPressed.OnNext(arrow);
            }

            #endregion
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            // フォーカス喪失等でキーの状態が消えたら押下の記憶を下ろす（前例 InputSystemPointerButtonStream）
            // Forget the presses when the key state is dropped on focus loss etc. (precedent InputSystemPointerButtonStream)
            if (device is not Keyboard) return;
            if (change is not (InputDeviceChange.SoftReset or InputDeviceChange.HardReset or InputDeviceChange.Removed or InputDeviceChange.Disabled)) return;
            _leftWasPressed = false;
            _rightWasPressed = false;
        }
    }
}
```
（ローカル関数は `ref` 引数を取れる。C# のローカル関数が外側の `eventPtr` を捕まえるのは許される。購読はゲーム寿命で外さない。前例と同じ）

- [ ] **Step 3: コンパイルしテストを実行して通ることを確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Input\.ArrowKeys\."`
Expected: ErrorCount 0 / PASS（2 件）

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Input/ArrowKeys moorestech_client/Assets/Scripts/Client.Tests/Input/ArrowKeys
git commit -m "feat(uitk): ←/→ の押下の縁を Input System から流す（S4-special。ベルトの窓の切り替え用）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 6: 区画 5 つ・Presenter・L1 の土台

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Registration/ISpecialDetailRenderer.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Registration/SpecialDetailPresenter.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Registration/SpecialPresenters.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Network/ElectricNetworkSectionView.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Network/GearNetworkSectionView.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Energy/GearSectionView.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Energy/GearToElectricSectionView.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Belt/BeltWindowCursor.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Belt/BeltThroughputSectionView.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Belt/BeltThroughputSectionPresenter.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/SpecialViewParts.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Special/Support/SpecialScreenTestHost.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Special/Support/FakeSpecialBlockDetailModel.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Special/Support/RecordingSpecialBlockCommands.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Special/Support/SpecialDetailSamples.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Special/GearToElectricSectionViewTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Special/GearSectionViewTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Special/BeltThroughputSectionViewTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Special/NetworkSectionViewTest.cs`

**Interfaces:**
- Consumes: Task 2 の Details・`SpecialDetailTexts`、Task 3 の `ISpecialBlockDetailModel`、Task 5 の `IHorizontalArrowKeySource`、L0-s4b の `GearRowDisplay`・`BlockDetailStateDisplay`・`BeltThroughputLogic`、部品 `BarSeries`・`HoverTooltipMark`・`UiStack`・`LocalizedLabel`・`UiTypographyClass`、S4-alpha の `LackHighlightText`・`LackHighlightTextSize`・`OpenBlockInventoryPanel`・`IBlockInventoryPanelSource`・`IBlockInventorySection`（`IBlockInventoryPart`。`Attach`/`Detach`・`IsApplicable`）と L1 の土台 `BlockInventoryTestPanels`・`FakePanelSource`（Global Constraints「S4-alpha の API」。Task 1 Step 2 の読み替え）
- Produces:
```csharp
namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    // View が実装する描画の口。View は購読しない（Presenter が流す）
    public interface ISpecialDetailRenderer
    {
        bool HasDetail(SpecialBlockDetailViewModel detail);   // 区画は自分の詳細の有無、本文・題の操作は常に true
        void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail);
    }
    // alpha の本文・題の操作・区画の 3 つの口に共通の Presenter（IBlockInventorySection は IBlockInventoryPart）
    public sealed class SpecialDetailPresenter : IBlockInventorySection
    {
        public SpecialDetailPresenter(VisualElement root, ISpecialDetailRenderer view, ISpecialBlockDetailModel model, IBlockInventoryPanelSource panels);
        public VisualElement Root { get; }
        public IReadOnlyReactiveProperty<bool> IsApplicable { get; }   // 詳細の有無。本文・題の操作では alpha が読まない
        public void Attach(OpenBlockInventoryPanel panel);              // 同じパネルの枠と詳細の Model を購読し View.Render へ流す
        public void Detach();                                           // 購読を切る
    }
    // Presenter の組み立て（購読の源を持つのはここだけ。View の依存の束 SpecialViewKit には入れない）
    public sealed class SpecialPresenters
    {
        public SpecialPresenters(ISpecialBlockDetailModel model, IBlockInventoryPanelSource panels, IHorizontalArrowKeySource arrowKeys);
        public SpecialDetailPresenter Of<TView>(TView view) where TView : VisualElement, ISpecialDetailRenderer;
        public BeltThroughputSectionPresenter Belt(BeltThroughputSectionView view);
    }
}
namespace Client.Ui.Screens.BlockInventory.Special.Belt
{
    public sealed class BeltThroughputSectionView : VisualElement { public void Render(BeltThroughputRowDisplay row, IReadOnlyList<float?> bars); }   // 購読しない
    public sealed class BeltThroughputSectionPresenter : IBlockInventorySection { public BeltThroughputSectionPresenter(BeltThroughputSectionView view, ISpecialBlockDetailModel model, IHorizontalArrowKeySource arrowKeys); }   // 窓の選択（BeltWindowCursor）を持ち、Detach で既定へ戻す
}
namespace Client.Ui.Screens.BlockInventory.Special
{
    public static class SpecialViewParts
    {
        public static LocalizedLabel Label(string testid, params string[] classes); // 文字の大きさ・色のクラスを付けた LocalizedLabel
        public static void SetShown(VisualElement element, bool shown);              // 子の display の出し入れ（UiGapContainer の項目と、SectionStack の項目としての区画の根には使わない。本文の唯一の子として包む区画にはよい）
        public static bool IsDisplayedInHierarchy(VisualElement element);           // パネルに載り、祖先まで display:none でない
    }
}
// 区画の View: ElectricNetworkSectionView()・GearNetworkSectionView()・GearSectionView()・GearToElectricSectionView()（VisualElement かつ ISpecialDetailRenderer）と BeltThroughputSectionView()。
// どれも自分の display を触らない。出し入れは Presenter の IsApplicable を alpha の SectionStackBody が SetItemShown で当てる（Web の return null。D-23）。
// 本文の中に区画の View を置くとき（電柱・宇宙エレベーターの電力網）は、本文が HasDetail で出し入れを決める（親が UiGapContainer なら SetItemShown）
```

- [ ] **Step 1: L1 の土台を書く**

`Support/FakeSpecialBlockDetailModel.cs`:
```csharp
using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.BlockInventory.Special;
using Client.Ui.State.BlockInventory.Special.Details;

namespace Client.Tests.Ui.Screens.BlockInventory.Special.Support
{
    // 特殊族の詳細の偽の Model（テストが Set で詳細を差し込む）
    // Fake special-detail model; the test pushes details with Set
    public sealed class FakeSpecialBlockDetailModel : FakeModel<SpecialBlockDetailViewModel>, ISpecialBlockDetailModel
    {
        public FakeSpecialBlockDetailModel() : base(SpecialBlockDetailViewModel.Empty)
        {
        }
    }
}
```

`Support/RecordingSpecialBlockCommands.cs`:
```csharp
using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Commands;
using Client.Ui.State.Commands;
using Cysharp.Threading.Tasks;

namespace Client.Tests.Ui.Screens.BlockInventory.Special.Support
{
    /// <summary>
    /// 特殊族の操作を Web の action と payload の形の 1 行で記録し、テストが決めた結果を返す（既定は受理）
    /// Records each special command as one line shaped like the Web action and payload, returning the outcome chosen by the test (accepted by default)
    /// </summary>
    public sealed class RecordingSpecialBlockCommands : CommandRecorder<string>, ISpecialBlockCommands
    {
        private UiCommandResult _next = UiCommandAccepted.Instance;

        public void SetNextResult(UiCommandResult result)
        {
            _next = result;
        }

        public UniTask<UiCommandResult> SetFilterMode(int directionIndex, FilterSplitterMode mode) => Reply($"filter_splitter.set_mode {directionIndex} {mode}");
        public UniTask<UiCommandResult> SetFilterItem(int directionIndex, int slotIndex, bool clear) => Reply($"filter_splitter.set_filter_item {directionIndex} {slotIndex} {(clear ? "clear" : "set")}");
        public UniTask<UiCommandResult> SetElectricToGearMode(int modeIndex) => Reply($"electric_to_gear.set_output_mode {modeIndex}");
        public UniTask<UiCommandResult> ConnectSpaceElevator() => Reply("space_elevator.connect");
        public UniTask<UiCommandResult> SendRocketCommand(RocketCommand command) => Reply($"rocket_launch_pad.command {command}");

        private UniTask<UiCommandResult> Reply(string line)
        {
            Record(line);
            return UniTask.FromResult(_next);
        }
    }
}
```

`Support/SpecialDetailSamples.cs`（Web の fixture〈`e2e/mock-host/fixtures/blocks/energyFixtures.ts`・`logisticsFixtures.ts`・`WireFixtures/block_inventory_rocket_launch_pad.json`〉の値）:
```csharp
using Client.Ui.State.BlockInventory.Logic.Belt;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Details;
using Core.Master;
using Game.Block.Interface.State;

namespace Client.Tests.Ui.Screens.BlockInventory.Special.Support
{
    /// <summary>
    /// Web の fixture と同じ値の詳細の見本。VM は Only* で 1 種類だけを持たせる
    /// Detail samples with the same values as the Web fixtures; Only* builds a VM carrying just one kind
    /// </summary>
    public static class SpecialDetailSamples
    {
        public static ElectricNetworkDetail PoleNetwork => new(240, 180, 3, 1);

        public static ElectricToGearDetail ElectricToGear => new(new[] { new ElectricToGearOutputMode(10, 10, 10), new ElectricToGearOutputMode(20, 20, 10) }, 1, 0.75, 10);

        public static FilterSplitterDetail FilterSplitter => new(new[]
        {
            new FilterSplitterDirectionDetail(FilterSplitterMode.Whitelist, new[] { new ItemId(4), ItemMaster.EmptyItemId }),
            new FilterSplitterDirectionDetail(FilterSplitterMode.Default, new[] { ItemMaster.EmptyItemId, ItemMaster.EmptyItemId }),
            new FilterSplitterDirectionDetail(FilterSplitterMode.Blacklist, new[] { new ItemId(7), new ItemId(8) }),
        });

        public static BeltThroughputDetail Belt => new(new[] { BeltThroughputWindow.Measured(90, 4, 4.5), BeltThroughputWindow.Measured(180, 9, 9), BeltThroughputWindow.Measuring(360, 18) }, new[] { 1, 0, 1 }, 1);

        public static RocketLaunchPadDetail Rocket(RocketLaunchPadPhase phase) => new(phase, RocketLaunchPadWaitReason.None, 0f, new[] { 0 }, new[] { 1 }, 2);

        public static SpaceElevatorDetail SpaceElevator(SpaceElevatorBaseStatus status, bool connected, int pool, float progress) => new(new[] { 0 }, new[] { 1, 2 }, connected, pool, 1000, progress, status);

        public static SpecialBlockDetailViewModel Only(ElectricToGearDetail detail) => new(detail, null, null, null, null, null, null, null, null);
        public static SpecialBlockDetailViewModel Only(GearToElectricDetail detail) => new(null, detail, null, null, null, null, null, null, null);
        public static SpecialBlockDetailViewModel Only(GearDetail detail) => new(null, null, detail, null, null, null, null, null, null);
        public static SpecialBlockDetailViewModel Only(BeltThroughputDetail detail) => new(null, null, null, detail, null, null, null, null, null);
        public static SpecialBlockDetailViewModel Only(ElectricNetworkDetail detail) => new(null, null, null, null, detail, null, null, null, null);
        public static SpecialBlockDetailViewModel Only(GearNetworkDetail detail) => new(null, null, null, null, null, detail, null, null, null);
        public static SpecialBlockDetailViewModel Only(FilterSplitterDetail detail) => new(null, null, null, null, null, null, detail, null, null);
        public static SpecialBlockDetailViewModel Only(RocketLaunchPadDetail detail) => new(null, null, null, null, null, null, null, detail, null);
        public static SpecialBlockDetailViewModel ElevatorWithNetwork(SpaceElevatorDetail detail) => new(null, null, null, null, new ElectricNetworkDetail(4000, 2000, 1, 1), null, null, null, detail);
    }
}
```

`Support/SpecialScreenTestHost.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Input.ArrowKeys;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Text;
using Mod.Loader;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special.Support
{
    /// <summary>
    /// 特殊族の L1 の土台。偽の Model・記録する操作・矢印キー・表示素材と、ゲージ・棒・文字の読み取りを持つ
    /// L1 base for the special family: fake model, recording commands, arrow keys and content, plus readers for gauges, bars and text
    /// </summary>
    public abstract class SpecialScreenTestHost : ScreenTestHost
    {
        protected static readonly IReadOnlyList<SlotItem> NoSlots = Array.Empty<SlotItem>();

        protected FakeSpecialBlockDetailModel Model { get; private set; }
        protected RecordingSpecialBlockCommands Commands { get; private set; }
        protected FakeArrowKeys ArrowKeys { get; private set; }
        protected FakePanelSource Panels { get; private set; }
        protected SpecialPresenters Presenters { get; private set; }

        [SetUp]
        public void SetUpSpecialHost()
        {
            Model = new FakeSpecialBlockDetailModel();
            Commands = new RecordingSpecialBlockCommands();
            ArrowKeys = new FakeArrowKeys();
            Panels = new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryPanelState.Closed));
            Presenters = new SpecialPresenters(Model, Panels, ArrowKeys);
        }

        // alpha の土台でパネルを開いた状態にして返す（Presenter の Attach に渡す）
        // Opens a panel with alpha's fixtures and returns it (passed to a presenter's Attach)
        protected OpenBlockInventoryPanel OpenPanel(string blockType, params SlotItem[] slots)
        {
            var panel = (OpenBlockInventoryPanel)BlockInventoryTestPanels.OfType(blockType, slots);
            Panels.State.Value = panel;
            return panel;
        }

        // アイテムの名前・絵が要るテストだけがテスト用 mod のマスタを読む（前例 GameContentPresentationTest）
        // Only tests that need item names and pictures load the test mod master (precedent GameContentPresentationTest)
        protected static GameContentPresentation LoadContent()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return new GameContentPresentation(new FakeContentTextureSource(null, null, null));
        }

        protected static string TextOf(VisualElement root, string testid)
        {
            return Find<Label>(root, testid).text;
        }

        protected static string Resolved(UiText text)
        {
            return text.Resolve();
        }

        // ゲージの充填の割合（.gauge-bar__fill の width の %）
        // Gauge fill ratio from the width percent of .gauge-bar__fill
        protected static float GaugeValue(VisualElement root, string testid)
        {
            return Find<VisualElement>(root, testid).Q(className: "gauge-bar__fill").style.width.value.value / 100f;
        }

        // 棒の値（空の棒は null、他は充填の高さの割合）
        // Bar values: null for an empty bar, otherwise the fill height ratio
        protected static float?[] BarValues(VisualElement root, string testid)
        {
            return Find<VisualElement>(root, testid).Query(className: "bar-series__bar").ToList()
                .Select(bar => bar.ClassListContains("bar-series__bar--empty") ? (float?)null : bar.Q(className: "bar-series__bar-fill").style.height.value.value / 100f)
                .ToArray();
        }

        protected sealed class FakeArrowKeys : IHorizontalArrowKeySource
        {
            private readonly Subject<HorizontalArrowKey> _pressed = new();

            public IObservable<HorizontalArrowKey> OnArrowPressed => _pressed;

            public void Press(HorizontalArrowKey key)
            {
                _pressed.OnNext(key);
            }
        }
    }
}
```
（`MoorestechServerDIContainerGenerator`・`TestModDirectory` の using は `GameContentPresentationTest.cs` の実物に合わせる。`FakePanelSource`・`BlockInventoryTestPanels` は alpha の L1 の土台〈`Client.Tests/Ui/State/BlockInventory/PanelModel/`〉で、Task 1 Step 2 で名前を照合する）

- [ ] **Step 2: L1 を書く**

`GearToElectricSectionViewTest.cs`:
```csharp
using Client.Localization;
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.Screens.BlockInventory.Special.Energy;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special
{
    /// <summary>
    /// 回転発電機の区画: 発電状態（不足トーン）と計測時期の説明、供給電力 current / max（Web GearToElectricSection の vitest）
    /// GearToElectric section: generation state with the shortage tone and timing note, supplied power current / max (Web GearToElectricSection vitest)
    /// </summary>
    public class GearToElectricSectionViewTest : SpecialScreenTestHost
    {
        /// <web-test file="src/features/blockInventory/details/__tests__/GearToElectricSection.test.ts">shows supplied power over the master maximum while generating</web-test>
        [Test]
        public void 発電中は最大に対する供給電力を不足トーン無しで出す()
        {
            var view = Render(GearToElectricDisplayState.Generating, 72.4, 100);
            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.BlockInventory.GearToElectricStateGenerating), TextOf(view, "gear-to-electric-state"));
            Assert.AreEqual(Supplied("72", "100"), TextOf(view, "gear-to-electric-supplied-power"));
            Assert.IsFalse(IsInsufficient(view));
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/GearToElectricSection.test.ts">shows idle without an insufficient tone</web-test>
        [Test]
        public void 待機は不足トーン無し()
        {
            var view = Render(GearToElectricDisplayState.Idle, 0, 100);
            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.BlockInventory.GearToElectricStateIdle), TextOf(view, "gear-to-electric-state"));
            Assert.IsFalse(IsInsufficient(view));
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/GearToElectricSection.test.ts">shows stopped with an insufficient tone</web-test>
        [Test]
        public void 停止は不足トーンで出す()
        {
            var view = Render(GearToElectricDisplayState.Stopped, 0, 100);
            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.BlockInventory.GearToElectricStateStopped), TextOf(view, "gear-to-electric-state"));
            Assert.IsTrue(IsInsufficient(view));
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/GearToElectricSection.test.ts">preserves positive supply %s as %s</web-test>
        [TestCase(0d, "0")]
        [TestCase(0.1d, "0.10")]
        [TestCase(0.49d, "0.49")]
        [TestCase(0.5d, "1")]
        [TestCase(29.49999d, "29")]
        [TestCase(29.5d, "30")]
        [TestCase(1e-8d, "1.0e-8")]
        public void 正の供給は0に潰さず桁を残す(double supplied, string expected)
        {
            Assert.AreEqual(Supplied(expected, "100"), TextOf(Render(GearToElectricDisplayState.Generating, supplied, 100), "gear-to-electric-supplied-power"));
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/GearToElectricSection.test.ts">preserves a positive master maximum below integer precision</web-test>
        [Test]
        public void 整数に満たない最大も桁を残す()
        {
            Assert.AreEqual(Supplied("0.10", "0.20"), TextOf(Render(GearToElectricDisplayState.Generating, 0.1, 0.2), "gear-to-electric-supplied-power"));
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/GearToElectricSection.test.ts">explains the measurement timing on the state row</web-test>
        [Test]
        public void 状態の行にホバーで計測時期の説明を持つ()
        {
            var view = Render(GearToElectricDisplayState.Generating, 10, 100);
            Assert.IsTrue(HoverTooltipMark.TryFindMarked(Find<VisualElement>(view, "gear-to-electric-state"), out _, out var text));
            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.BlockInventory.GearToElectricMeasurementTiming), text.Resolve());
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/GearToElectricSection.test.ts">renders nothing before detail arrives</web-test>
        [Test]
        public void 詳細の前は何も出さない()
        {
            // 出し入れは Presenter の IsApplicable が親へ知らせる（区画は自分の display を触らない。D-23）
            // Visibility is reported by the presenter's IsApplicable (the section never touches its own display. D-23)
            var presenter = Presenters.Of(Mount(new GearToElectricSectionView()));
            presenter.Attach(OpenPanel("GearToElectricGenerator"));
            Assert.IsFalse(presenter.IsApplicable.Value);
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/GearToElectricSection.i18n.test.ts">shows the specified supplied-power sentence after real i18n interpolation</web-test>
        [Test]
        public void 日本語の実際の文で供給電力と状態と説明を出す()
        {
            Localize.TrySetChosenLanguage("japanese");
            var view = Render(GearToElectricDisplayState.Generating, 72.4, 100);
            Assert.AreEqual("供給電力 72 / 最大 100", TextOf(view, "gear-to-electric-supplied-power"));
            Assert.AreEqual("発電中", TextOf(view, "gear-to-electric-state"));
            HoverTooltipMark.TryFindMarked(Find<VisualElement>(view, "gear-to-electric-state"), out _, out var tooltip);
            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.BlockInventory.GearToElectricMeasurementTiming), tooltip.Resolve());
        }

        private GearToElectricSectionView Render(GearToElectricDisplayState state, double supplied, double max)
        {
            var view = Mount(new GearToElectricSectionView());
            view.Render(NoSlots, SpecialDetailSamples.Only(new GearToElectricDetail(state, supplied, max)));
            return view;
        }

        private static string Supplied(string current, string max)
        {
            return new UiText(LocalizationKeys.Ui.BlockInventory.GearToElectricSuppliedPower, new[] { new LocalizationTextParam("current", current), new LocalizationTextParam("max", max) }).Resolve();
        }

        private static bool IsInsufficient(VisualElement view)
        {
            return Find<VisualElement>(view, "gear-to-electric-state").ClassListContains(LackHighlightText.InsufficientClassName);
        }
    }
}
```
（i18n の件の日本語の文は `Localization/localization.csv` の `gearToElectricSuppliedPower`・`gearToElectricStateGenerating` の japanese 列。Web の `.i18n.test.ts` の期待文と同じ。`ScreenTestHost` の TearDown が言語を戻す。不足のクラス名は alpha の `LackHighlightText.InsufficientClassName`）

`GearSectionViewTest.cs`:
```csharp
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.Screens.BlockInventory.Special.Energy;
using Client.Ui.State.BlockInventory.Logic.Detail;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special
{
    /// <summary>
    /// 歯車の区画: 消費側は基準 RPM 付き、発電側は現在値だけ。トルク 0 でも同じ 2 行（Web GearSection の vitest）
    /// Gear section: consumers show RPM with its base, generators only the current; zero torque keeps the same two rows (Web GearSection vitest)
    /// </summary>
    public class GearSectionViewTest : SpecialScreenTestHost
    {
        /// <web-test file="src/features/blockInventory/details/__tests__/GearSection.test.ts">renders consumed torque without a denominator and RPM with its base for consumers</web-test>
        [Test]
        public void 消費側は分母無しのトルクと基準付きのRPMを出す()
        {
            var gear = GearDetail.Consumer(5, 2.4, 10);
            var view = Render(gear);
            Assert.AreEqual(Resolved(GearRowDisplay.From(gear).Torque), TextOf(view, "gear-torque"));
            Assert.AreEqual(Resolved(GearRowDisplay.From(gear).Rpm), TextOf(view, "gear-rpm"));
            StringAssert.Contains("2.4", TextOf(view, "gear-torque"));
            StringAssert.Contains("10.0", TextOf(view, "gear-rpm"));
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/GearSection.test.ts">renders generated torque and current RPM only for generators</web-test>
        [Test]
        public void 発電側は発生トルクと現在のRPMだけを出す()
        {
            var gear = GearDetail.Generator(20, 5);
            var view = Render(gear);
            Assert.AreEqual(Resolved(GearRowDisplay.From(gear).Torque), TextOf(view, "gear-torque"));
            Assert.AreEqual(Resolved(GearRowDisplay.From(gear).Rpm), TextOf(view, "gear-rpm"));
            StringAssert.DoesNotContain("/", TextOf(view, "gear-rpm"));
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/GearSection.test.ts">keeps the same rows for a zero-torque transmission block</web-test>
        [Test]
        public void トルク0の伝達ブロックも同じ2行()
        {
            var view = Render(GearDetail.Consumer(5, 0, 5));
            CollectionAssert.AreEqual(new[] { "gear-section", "gear-torque", "gear-rpm" }, view.Query<VisualElement>().Where(e => !string.IsNullOrEmpty(e.name)).ToList().ConvertAll(e => e.name));
        }

        private GearSectionView Render(GearDetail gear)
        {
            var view = Mount(new GearSectionView());
            view.Render(NoSlots, SpecialDetailSamples.Only(gear));
            return view;
        }
    }
}
```

`BeltThroughputSectionViewTest.cs`:
```csharp
using Client.Input;
using Client.Input.ArrowKeys;
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.Screens.BlockInventory.Special.Belt;
using Client.Ui.State.BlockInventory.Logic.Belt;
using Client.Ui.State.BlockInventory.Special.Details;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special
{
    /// <summary>
    /// ベルトの流量: 既定 180tick の窓・分間・直近 9 秒の棒。←/→ で窓を巡回し端で止まる（Web BeltThroughputSection の vitest）。Presenter＋View で組む
    /// Belt throughput: default 180-tick window, per-minute row and nine recent bars; ←/→ cycle the window and stop at the ends (Web vitest), built as presenter plus view
    /// </summary>
    public class BeltThroughputSectionViewTest : SpecialScreenTestHost
    {
        /// <web-test file="src/features/blockInventory/details/__tests__/BeltThroughputSection.test.ts">renders nothing without belt data</web-test>
        [Test]
        public void ベルトの詳細が無ければ何も出さない()
        {
            var (_, presenter) = Attach(SpecialBlockDetailViewModel.Empty);
            Assert.IsFalse(presenter.IsApplicable.Value);
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/BeltThroughputSection.test.ts">shows the 180-tick window and nine per-second bars by default</web-test>
        [Test]
        public void 既定は180tickの窓と9本の棒()
        {
            var (view, presenter) = Attach(SpecialDetailSamples.Only(SpecialDetailSamples.Belt));
            Assert.IsTrue(presenter.IsApplicable.Value);
            var row = (MeasuredBeltRowDisplay)BeltThroughputLogic.RowDisplay(SpecialDetailSamples.Belt.Windows[1]);
            Assert.AreEqual(Resolved(row.Text), TextOf(view, "belt-throughput-window"));
            Assert.IsTrue(IsShown(Find<Label>(view, "belt-throughput-per-minute")));
            StringAssert.Contains("60.0", TextOf(view, "belt-throughput-per-minute"));
            CollectionAssert.AreEqual(new float?[] { null, null, null, null, null, null, 1f, 0f, 1f }, BarValues(view, "belt-throughput-bars"));
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/BeltThroughputSection.test.ts">switches windows with the arrow keys</web-test>
        [Test]
        public void 矢印キーで窓を切り替え端で止まる()
        {
            var (view, _) = Attach(SpecialDetailSamples.Only(SpecialDetailSamples.Belt));
            ArrowKeys.Press(HorizontalArrowKey.Right);
            Assert.AreEqual(Resolved(BeltThroughputLogic.RowDisplay(SpecialDetailSamples.Belt.Windows[2]).Text), TextOf(view, "belt-throughput-window"));
            Assert.IsFalse(IsShown(Find<Label>(view, "belt-throughput-per-minute")), "計測中は分間を出さない");
            Assert.AreEqual(9, BarValues(view, "belt-throughput-bars").Length);

            ArrowKeys.Press(HorizontalArrowKey.Left);
            ArrowKeys.Press(HorizontalArrowKey.Left);
            var first = Resolved(BeltThroughputLogic.RowDisplay(SpecialDetailSamples.Belt.Windows[0]).Text);
            Assert.AreEqual(first, TextOf(view, "belt-throughput-window"));
            StringAssert.Contains("89", first);
            ArrowKeys.Press(HorizontalArrowKey.Left);
            Assert.AreEqual(first, TextOf(view, "belt-throughput-window"));
        }

        [Test]
        public void 入力欄にフォーカスがある間とDetachの後は切り替えずDetachで既定へ戻る()
        {
            var (view, presenter) = Attach(SpecialDetailSamples.Only(SpecialDetailSamples.Belt));
            var before = TextOf(view, "belt-throughput-window");
            UiInputExclusivity.SetGameUiTextInputFocused(true);
            ArrowKeys.Press(HorizontalArrowKey.Right);
            UiInputExclusivity.SetGameUiTextInputFocused(false);
            Assert.AreEqual(before, TextOf(view, "belt-throughput-window"));

            ArrowKeys.Press(HorizontalArrowKey.Right);
            presenter.Detach();
            ArrowKeys.Press(HorizontalArrowKey.Right);
            presenter.Attach(OpenPanel("BeltConveyor"));
            Assert.AreEqual(before, TextOf(view, "belt-throughput-window"), "Detach の後は既定の窓へ戻る");
        }

        private (BeltThroughputSectionView view, BeltThroughputSectionPresenter presenter) Attach(SpecialBlockDetailViewModel detail)
        {
            var view = Mount(new BeltThroughputSectionView());
            var presenter = Presenters.Belt(view);
            Model.Set(detail);
            presenter.Attach(OpenPanel("BeltConveyor"));
            return (view, presenter);
        }
    }
}
```
（`UiInputExclusivity.SetGameUiTextInputFocused` は GameUi の系でだけ効く実物。テストの系が Web なら `UiInputExclusivity.IsTextInputFocused` が立たないので、C2-b の L1〈`GameTextField` のテスト〉が系を切り替える書き方に合わせる。合わせられなければこの件は `SpecialViewParts` を通さず `BeltThroughputSectionPresenter` の判定を `IsTextInputFocused` だけの小さな関数に分け、その関数を L0 で見る）

`NetworkSectionViewTest.cs`:
```csharp
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.Screens.BlockInventory.Special.Network;
using System.Collections.Generic;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Text;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special
{
    /// <summary>
    /// 電力網・歯車網の区画（Web NetworkSections）と、Presenter が Attach から Detach まで詳細とパネルの枠を購読し出し入れを知らせること
    /// Electric and gear network sections (Web NetworkSections) and the presenter subscribing to details and panel slots from Attach to Detach while reporting visibility
    /// </summary>
    public class NetworkSectionViewTest : SpecialScreenTestHost
    {
        [Test]
        public void 需要0は需要なしを出し他は集約の1行を出す()
        {
            var view = Mount(new ElectricNetworkSectionView());
            view.Render(NoSlots, SpecialDetailSamples.Only(new ElectricNetworkDetail(240, 0, 0, 1)));
            Assert.IsTrue(IsShown(Find<Label>(view, "electric-network-no-demand")));
            Assert.IsFalse(IsShown(Find<VisualElement>(view, "electric-network-summary")));

            view.Render(NoSlots, SpecialDetailSamples.Only(SpecialDetailSamples.PoleNetwork));
            Assert.IsFalse(IsShown(Find<Label>(view, "electric-network-no-demand")));
            Assert.AreEqual(Resolved(SpecialDetailTexts.ElectricNetworkSummary(SpecialDetailSamples.PoleNetwork)), TextOf(view, "electric-network-summary"));
        }

        [Test]
        public void 歯車網は集約と止まっていれば停止理由を出す()
        {
            var view = Mount(new GearNetworkSectionView());
            view.Render(NoSlots, SpecialDetailSamples.Only(new GearNetworkDetail(20, 30, GearNetworkStopReasonKind.OverRequirePower)));
            Assert.IsTrue(IsShown(Find<Label>(view, "gear-stop-reason")));
            Assert.IsTrue(Find<Label>(view, "gear-stop-reason").ClassListContains("text--insufficient"));

            view.Render(NoSlots, SpecialDetailSamples.Only(new GearNetworkDetail(20, 10, GearNetworkStopReasonKind.None)));
            Assert.IsFalse(IsShown(Find<Label>(view, "gear-stop-reason")));
        }

        [Test]
        public void Presenterは組み直しの外で届いた詳細で出し入れを知らせ自分のdisplayを触らずDetachの後は描かない()
        {
            var view = new ElectricNetworkSectionView();
            var presenter = Presenters.Of(view);
            Mount(presenter.Root);
            presenter.Attach(OpenPanel("ElectricPole"));
            Assert.IsFalse(presenter.IsApplicable.Value);

            Model.Set(SpecialDetailSamples.Only(SpecialDetailSamples.PoleNetwork));
            Assert.IsTrue(presenter.IsApplicable.Value, "網の集約が後から届いても出す側へ変わる");
            Assert.AreEqual(Resolved(SpecialDetailTexts.ElectricNetworkSummary(SpecialDetailSamples.PoleNetwork)), TextOf(view, "electric-network-summary"));

            Model.Set(SpecialBlockDetailViewModel.Empty);
            Assert.IsFalse(presenter.IsApplicable.Value, "集約が消えたら隠す側へ変わる");
            Assert.AreEqual(StyleKeyword.Null, view.style.display.keyword, "区画は自分の display を触らない（出し入れは親の SetItemShown）");

            presenter.Detach();
            Model.Set(SpecialDetailSamples.Only(SpecialDetailSamples.PoleNetwork));
            Assert.IsFalse(presenter.IsApplicable.Value, "Detach の後は購読しない");
        }

        [Test]
        public void Presenterは同じパネルの枠の変化を流し別のパネルの値は描かない()
        {
            var view = new RecordingRenderer();
            var presenter = Presenters.Of(view);
            Model.Set(SpecialDetailSamples.Only(SpecialDetailSamples.PoleNetwork));
            presenter.Attach(OpenPanel("ElectricPole"));
            Panels.State.Value = BlockInventoryTestPanels.OfType("ElectricPole", new SlotItem(new ItemId(1), 3));
            Assert.AreEqual(3, view.LastSlots[0].Count, "同じパネルの枠の更新が届く");

            Panels.State.Value = BlockInventoryTestPanels.OpenFailedChest();
            Assert.AreEqual(3, view.LastSlots[0].Count, "別のパネルへ替わる途中の値は描かない");
        }

        // 渡された枠を覚えるだけの View
        // A view that only remembers the slots it was given
        private sealed class RecordingRenderer : VisualElement, ISpecialDetailRenderer
        {
            public IReadOnlyList<SlotItem> LastSlots { get; private set; }
            public bool HasDetail(SpecialBlockDetailViewModel detail) => true;
            public void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail) => LastSlots = itemSlots;
        }
    }
}
```
（`OfType` が同じ位置・Guid の部屋を返すこと〈同じパネル扱い〉は alpha の土台の実物で確かめる。違えば同じ位置で枠だけ変えた `OpenBlockInventoryPanel` を internal ctor で組む。`using` に `System.Collections.Generic`・`Client.Tests.Ui.State.BlockInventory.PanelModel`・`Client.Ui.State.Items`・`Core.Master` を足す）

- [ ] **Step 3: 実装を書く**

`Registration/ISpecialDetailRenderer.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;

namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    // 特殊族の View が実装する描画の口（自分の詳細の有無と、スロットの中身・特殊族の詳細での描画）。View は購読せず Presenter から呼ばれる
    // Rendering port the special views implement: whether their detail exists, and drawing from slot contents and special details; views never subscribe and are called by the presenter
    public interface ISpecialDetailRenderer
    {
        bool HasDetail(SpecialBlockDetailViewModel detail);
        void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail);
    }
}
```

`Registration/SpecialDetailPresenter.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Special;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    /// <summary>
    /// S4-alpha の本文・題の操作・区画の口を 1 つで実装する Presenter。Attach から Detach まで、同じパネルの枠と特殊族の詳細の Model を購読して View へ流し、区画の出し入れを IsApplicable で知らせる
    /// Presenter implementing S4-alpha's body, title-action and section ports in one; from Attach to Detach it streams the same panel's slots and the special detail model into the view and reports section visibility through IsApplicable
    /// </summary>
    public sealed class SpecialDetailPresenter : IBlockInventorySection
    {
        private readonly ISpecialDetailRenderer _view;
        private readonly ISpecialBlockDetailModel _model;
        private readonly IBlockInventoryPanelSource _panels;
        private readonly ReactiveProperty<bool> _isApplicable = new(false);
        private readonly CompositeDisposable _subscriptions = new();

        public VisualElement Root { get; private set; }
        public IReadOnlyReactiveProperty<bool> IsApplicable => _isApplicable;   // 型を狭める公開（前例 alpha の SectionStackItemGridSection）

        public SpecialDetailPresenter(VisualElement root, ISpecialDetailRenderer view, ISpecialBlockDetailModel model, IBlockInventoryPanelSource panels)
        {
            Root = root;
            _view = view;
            _model = model;
            _panels = panels;
        }

        public void Attach(OpenBlockInventoryPanel panel)
        {
            // 同じパネルの枠の変化と、パネルの組み直しの外で届く詳細（網の集約・ベルトの統計）の両方で描き直す
            // Redraw on both slot changes of the same panel and details arriving outside panel rebuilds (network totals, belt stats)
            _panels.Current
                .Where(current => IsSame(current, panel))
                .Select(current => ((OpenBlockInventoryPanel)current).ItemSlots)
                .CombineLatest(_model.Current, (slots, detail) => (slots, detail))
                .Subscribe(pair => Draw(pair.slots, pair.detail))
                .AddTo(_subscriptions);

            #region Internal

            bool IsSame(BlockInventoryPanelState current, OpenBlockInventoryPanel attached)
            {
                // silent-ok: 別のパネルへ替わる途中の値は描かない / alpha の BodyHost が同じフレームで Detach して新しい部分を作る（前例 alpha の SectionStackItemGridSection）
                return current is OpenBlockInventoryPanel open && open.IsSamePanel(attached);
            }

            #endregion
        }

        public void Detach()
        {
            _subscriptions.Clear();
        }

        private void Draw(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)
        {
            // 出し入れは IsApplicable で親へ知らせ、出す時だけ描く（区画は自分の display を触らない。D-23）
            // Report visibility to the parent via IsApplicable and draw only when shown (sections never touch their own display. D-23)
            _isApplicable.Value = _view.HasDetail(detail);
            if (_isApplicable.Value) _view.Render(itemSlots, detail);
        }
    }
}
```
（本文・題の操作は `HasDetail` が常に真なので、詳細の未着でも `Render` が呼ばれ、View が取得待ちを描く。alpha は本文・題の操作の `IsApplicable` を読まない。`CombineLatest` の型は UniRx の実物に合わせる。同じパネルの間はパネルの源が枠の更新で値を出すので、枠だけの変化でも描き直る）

`Registration/SpecialPresenters.cs`:
```csharp
using Client.Input.ArrowKeys;
using Client.Ui.Screens.BlockInventory.Special.Belt;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Special;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    // 特殊族の Presenter の組み立て。購読の源（詳細の Model・パネル・矢印キー）を持つのはここだけで、View は受けない
    // Builds the special presenters; only this holds the subscription sources (detail model, panel, arrow keys), never the views
    public sealed class SpecialPresenters
    {
        private readonly ISpecialBlockDetailModel _model;
        private readonly IBlockInventoryPanelSource _panels;
        private readonly IHorizontalArrowKeySource _arrowKeys;

        public SpecialPresenters(ISpecialBlockDetailModel model, IBlockInventoryPanelSource panels, IHorizontalArrowKeySource arrowKeys)
        {
            _model = model;
            _panels = panels;
            _arrowKeys = arrowKeys;
        }

        public SpecialDetailPresenter Of<TView>(TView view) where TView : VisualElement, ISpecialDetailRenderer
        {
            return new SpecialDetailPresenter(view, view, _model, _panels);
        }

        public BeltThroughputSectionPresenter Belt(BeltThroughputSectionView view)
        {
            return new BeltThroughputSectionPresenter(view, _model, _arrowKeys);
        }
    }
}
```

`SpecialViewParts.cs`（`SP/` 直下）:
```csharp
using Client.Ui.Foundation.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special
{
    /// <summary>
    /// 特殊族の View が共通に使う小さな手続き（文字の行・表示の出し入れ・表示中の判定）
    /// Small helpers shared by the special views: text rows, display toggling and the displayed check
    /// </summary>
    public static class SpecialViewParts
    {
        public static LocalizedLabel Label(string testid, params string[] classes)
        {
            var label = new LocalizedLabel { name = testid };
            foreach (var className in classes) label.AddToClassList(className);
            return label;
        }

        // 子の出し入れ。UiGapContainer の項目（SetItemShown を使う）と SectionStack の項目としての区画の根には使わない。本文の唯一の子として包む区画にはよい
        // Toggles a child; not for UiGapContainer items (use SetItemShown) or section roots as SectionStack items; fine for a section wrapped as a body's only child
        public static void SetShown(VisualElement element, bool shown)
        {
            element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // パネルに載り、自分から根まで display:none が無い（Web の「層が blockInventory」の代わり）
        // Attached to a panel with no display:none from itself to the root (stands in for the Web "active layer is blockInventory")
        public static bool IsDisplayedInHierarchy(VisualElement element)
        {
            if (element.panel == null) return false;
            for (var current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None) return false;
            }
            return true;
        }
    }
}
```

`Network/ElectricNetworkSectionView.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Text;
using Client.Ui.State.Items;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Network
{
    /// <summary>
    /// 電力網の集約の区画。消費者 0 は「需要なし」、他は集約の 1 行（Web ElectricNetworkSection）
    /// Electric network aggregate section; zero consumers shows "no demand", otherwise one summary row (Web ElectricNetworkSection)
    /// </summary>
    public sealed class ElectricNetworkSectionView : VisualElement, ISpecialDetailRenderer
    {
        private readonly UiStack _stack = new(UiGap.Px2);
        private readonly LocalizedLabel _noDemand;
        private readonly LackHighlightText _summary;

        public ElectricNetworkSectionView()
        {
            name = "electric-network-section";
            AddToClassList("special-section");
            _noDemand = SpecialViewParts.Label("electric-network-no-demand", UiTypographyClass.TextXs, UiTypographyClass.TextMuted);
            _noDemand.SetText(new UiText(LocalizationKeys.Ui.BlockInventory.NoDemand));
            _summary = new LackHighlightText("electric-network-summary", LackHighlightTextSize.Xs);
            _stack.AddItem(_noDemand);
            _stack.AddItem(_summary);
            Add(_stack);
        }

        public bool HasDetail(SpecialBlockDetailViewModel detail) => detail.TryGetElectricNetwork(out _);

        public void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)
        {
            // silent-ok: 詳細が無ければ Presenter が IsApplicable を偽にして親が隠す（Render は呼ばれない。本文の中では本文が HasDetail で隠す） / 区画の L1「詳細の前は何も出さない」で気づく
            if (!detail.TryGetElectricNetwork(out var network)) return;

            var noDemand = network.ConsumerCount == 0;
            _stack.SetItemShown(_noDemand, noDemand);
            _stack.SetItemShown(_summary, !noDemand);
            if (!noDemand) _summary.Render(SpecialDetailTexts.ElectricNetworkSummary(network), false);
        }
    }
}
```

`Network/GearNetworkSectionView.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Text;
using Client.Ui.State.Items;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Network
{
    /// <summary>
    /// 歯車網の集約と停止理由の区画（Web GearNetworkSection）
    /// Gear network aggregate and stop reason section (Web GearNetworkSection)
    /// </summary>
    public sealed class GearNetworkSectionView : VisualElement, ISpecialDetailRenderer
    {
        private readonly UiStack _stack = new(UiGap.Px2);
        private readonly LackHighlightText _summary;
        private readonly LocalizedLabel _stopReason;

        public GearNetworkSectionView()
        {
            name = "gear-network-section";
            AddToClassList("special-section");
            _summary = new LackHighlightText("gear-network-summary", LackHighlightTextSize.Xs);
            _stopReason = SpecialViewParts.Label("gear-stop-reason", UiTypographyClass.TextXs, UiTypographyClass.TextInsufficient);
            _stack.AddItem(_summary);
            _stack.AddItem(_stopReason);
            Add(_stack);
        }

        public bool HasDetail(SpecialBlockDetailViewModel detail) => detail.TryGetGearNetwork(out _);

        public void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)
        {
            // silent-ok: 詳細が無ければ Presenter が IsApplicable を偽にして親が隠す（Render は呼ばれない。本文の中では本文が HasDetail で隠す） / 区画の L1「詳細の前は何も出さない」で気づく
            if (!detail.TryGetGearNetwork(out var network)) return;

            // 止まっていれば理由の文を不足色で足す
            // Add the reason sentence in the shortage color when stopped
            _summary.Render(SpecialDetailTexts.GearNetworkSummary(network), false);
            var stopped = BlockDetailStateDisplay.TryGetStopReasonKey(network.StopReason, out var reasonKey);
            _stack.SetItemShown(_stopReason, stopped);
            if (stopped) _stopReason.SetText(new UiText(reasonKey));
        }
    }
}
```

`Energy/GearSectionView.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Energy
{
    /// <summary>
    /// 歯車の区画（トルク・RPM の 2 行。Web GearSection）
    /// Gear section with torque and RPM rows (Web GearSection)
    /// </summary>
    public sealed class GearSectionView : VisualElement, ISpecialDetailRenderer
    {
        private readonly LocalizedLabel _torque = SpecialViewParts.Label("gear-torque", UiTypographyClass.TextSm, UiTypographyClass.TextDefault);
        private readonly LocalizedLabel _rpm = SpecialViewParts.Label("gear-rpm", UiTypographyClass.TextSm, UiTypographyClass.TextDefault);

        public GearSectionView()
        {
            name = "gear-section";
            AddToClassList("special-section");
            var stack = new UiStack(UiGap.Px2);
            stack.AddItem(_torque);
            stack.AddItem(_rpm);
            Add(stack);
        }

        public bool HasDetail(SpecialBlockDetailViewModel detail) => detail.TryGetGear(out _);

        public void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)
        {
            // silent-ok: 詳細が無ければ Presenter が IsApplicable を偽にして親が隠す（Render は呼ばれない。本文の中では本文が HasDetail で隠す） / 区画の L1「詳細の前は何も出さない」で気づく
            if (!detail.TryGetGear(out var gear)) return;
            var rows = GearRowDisplay.From(gear);
            _torque.SetText(rows.Torque);
            _rpm.SetText(rows.Rpm);
        }
    }
}
```
（テスト「トルク0の伝達ブロックも同じ2行」が名前の付いた要素を数えるので、`UiStack` には名前を付けない）

`Energy/GearToElectricSectionView.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Text;
using Client.Ui.State.Items;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Energy
{
    /// <summary>
    /// 回転発電機の区画。状態の行はホバーで計測時期を説明し、停止は不足トーン（Web GearToElectricSection）
    /// GearToElectric section; the state row explains measurement timing on hover and a stop uses the shortage tone (Web GearToElectricSection)
    /// </summary>
    public sealed class GearToElectricSectionView : VisualElement, ISpecialDetailRenderer
    {
        private readonly LackHighlightText _state = new("gear-to-electric-state", LackHighlightTextSize.Sm);
        private readonly LocalizedLabel _supplied = SpecialViewParts.Label("gear-to-electric-supplied-power", UiTypographyClass.TextSm, UiTypographyClass.TextDefault);

        public GearToElectricSectionView()
        {
            name = "gear-to-electric-section";
            AddToClassList("special-section");
            HoverTooltipMark.Mark(_state, new UiText(LocalizationKeys.Ui.BlockInventory.GearToElectricMeasurementTiming));
            var stack = new UiStack(UiGap.Px2);
            stack.AddItem(_state);
            stack.AddItem(_supplied);
            Add(stack);
        }

        public bool HasDetail(SpecialBlockDetailViewModel detail) => detail.TryGetGearToElectric(out _);

        public void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)
        {
            // silent-ok: 詳細が無ければ Presenter が IsApplicable を偽にして親が隠す（Render は呼ばれない。本文の中では本文が HasDetail で隠す） / 区画の L1「詳細の前は何も出さない」で気づく
            if (!detail.TryGetGearToElectric(out var generator)) return;

            // 状態の文と不足トーンは L0 の表で決める
            // The state sentence and shortage tone come from the L0 table
            var display = BlockDetailStateDisplay.GearToElectric(generator.State);
            _state.Render(new UiText(display.LabelKey), display.Insufficient);
            _supplied.SetText(SpecialDetailTexts.GearToElectricSuppliedPower(generator));
        }
    }
}
```

`Belt/BeltWindowCursor.cs`:
```csharp
using Client.Input.ArrowKeys;
using Client.Ui.State.BlockInventory.Logic.Belt;
using Client.Ui.State.BlockInventory.Special.Details;

namespace Client.Ui.Screens.BlockInventory.Special.Belt
{
    /// <summary>
    /// 選んだ窓の tick（既定 180。←/→ で巡回し端で止まる。Web の useState と cycleBeltWindow）
    /// Selected window ticks (default 180; ←/→ cycle and stop at the ends; Web useState and cycleBeltWindow)
    /// </summary>
    public sealed class BeltWindowCursor
    {
        public int SelectedTicks { get; private set; } = BeltThroughputLogic.DefaultBeltWindowTicks;

        public void Step(BeltThroughputDetail belt, HorizontalArrowKey key)
        {
            var step = key == HorizontalArrowKey.Left ? BeltWindowStep.Previous : BeltWindowStep.Next;
            SelectedTicks = BeltThroughputLogic.CycleBeltWindow(belt.Windows, SelectedTicks, step);
        }

        // 区画がパネルから外れたら既定へ戻す（Web のコンポーネントの破棄と同じ）
        // Return to the default when the section leaves the panel (same as the Web component unmount)
        public void Reset()
        {
            SelectedTicks = BeltThroughputLogic.DefaultBeltWindowTicks;
        }
    }
}
```

`Belt/BeltThroughputSectionView.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.Components.Gauges;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.State.BlockInventory.Logic.Belt;
using Client.Ui.State.Text;
using Client.Localization;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Belt
{
    /// <summary>
    /// ベルトの流量の区画の View。選んだ窓の行・計測済みなら分間・直近 9 秒の棒を描くだけで、購読も窓の選択も持たない（Web BeltThroughputSection の見た目）
    /// View of the belt throughput section; draws the selected window row, per-minute when measured and nine recent bars, holding no subscription or window choice (Web BeltThroughputSection look)
    /// </summary>
    public sealed class BeltThroughputSectionView : VisualElement
    {
        private readonly UiStack _stack = new(UiGap.Px2);
        private readonly LocalizedLabel _window = SpecialViewParts.Label("belt-throughput-window", UiTypographyClass.TextSm, UiTypographyClass.TextDefault);
        private readonly LocalizedLabel _perMinute = SpecialViewParts.Label("belt-throughput-per-minute", UiTypographyClass.TextSm, UiTypographyClass.TextDefault);
        private readonly BarSeries _bars = new("belt-throughput-bars");

        public BeltThroughputSectionView()
        {
            name = "belt-throughput-section";
            AddToClassList("special-section");
            _stack.AddItem(_window);
            _stack.AddItem(_perMinute);
            _stack.AddItem(_bars);
            Add(_stack);
        }

        public void Render(BeltThroughputRowDisplay row, IReadOnlyList<float?> bars)
        {
            // 計測済みの窓だけ分間の行を出す
            // Show the per-minute row only for a measured window
            _window.SetText(row.Text);
            var measured = row as MeasuredBeltRowDisplay;
            _stack.SetItemShown(_perMinute, measured != null);
            if (measured != null) _perMinute.SetText(new UiText(LocalizationKeys.Ui.BlockInventory.ItemsPerMinute, new[] { new LocalizationTextParam("itemsPerMinute", measured.ItemsPerMinute) }));
            _bars.SetValues(bars);
        }
    }
}
```

`Belt/BeltThroughputSectionPresenter.cs`:
```csharp
using Client.Input;
using Client.Input.ArrowKeys;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Logic.Belt;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Special;
using Client.Ui.State.BlockInventory.Special.Details;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Belt
{
    /// <summary>
    /// ベルトの流量の区画の Presenter。詳細と ←/→ を Attach から Detach まで購読し、窓の選択（既定 180・端で止まる）と出し入れ（IsApplicable）を持つ（Web BeltThroughputSection の useState と keydown）
    /// Presenter of the belt throughput section; subscribes to details and ←/→ from Attach to Detach, owning the window choice (default 180, stops at the ends) and visibility (Web useState and keydown)
    /// </summary>
    public sealed class BeltThroughputSectionPresenter : IBlockInventorySection
    {
        private readonly BeltThroughputSectionView _view;
        private readonly ISpecialBlockDetailModel _model;
        private readonly IHorizontalArrowKeySource _arrowKeys;
        private readonly BeltWindowCursor _cursor = new();
        private readonly ReactiveProperty<bool> _isApplicable = new(false);
        private readonly CompositeDisposable _subscriptions = new();
        private BeltThroughputDetail _belt;

        public VisualElement Root => _view;
        public IReadOnlyReactiveProperty<bool> IsApplicable => _isApplicable;

        public BeltThroughputSectionPresenter(BeltThroughputSectionView view, ISpecialBlockDetailModel model, IHorizontalArrowKeySource arrowKeys)
        {
            _view = view;
            _model = model;
            _arrowKeys = arrowKeys;
        }

        public void Attach(OpenBlockInventoryPanel panel)
        {
            // 詳細の変化と矢印の押下を Detach まで聞く
            // Listen to detail changes and arrow presses until Detach
            _model.Current.Subscribe(detail =>
            {
                _belt = detail.TryGetBelt(out var belt) ? belt : null;
                Draw();
            }).AddTo(_subscriptions);
            _arrowKeys.OnArrowPressed.Subscribe(OnArrowPressed).AddTo(_subscriptions);
        }

        public void Detach()
        {
            // 外れたら窓を既定へ戻す（Web のコンポーネントの破棄と同じ）
            // Reset the window when detached (same as the Web component unmount)
            _subscriptions.Clear();
            _cursor.Reset();
            _belt = null;
        }

        private void OnArrowPressed(HorizontalArrowKey key)
        {
            // 入力欄にフォーカスがある・ベルトが無い・表示されていないなら切り替えない（Web の isTextInputElement・readActiveLayer）
            // Do not switch while a text field has focus, without a belt or while hidden (Web isTextInputElement, readActiveLayer)
            // silent-ok: ベルト以外のブロック・閉じた画面・入力中の矢印はこの区画の操作ではない / 切り替わらないなら L1 と録画（R-12）で気づく
            if (_belt == null || UiInputExclusivity.IsTextInputFocused || !SpecialViewParts.IsDisplayedInHierarchy(_view)) return;
            _cursor.Step(_belt, key);
            Draw();
        }

        private void Draw()
        {
            // 窓が選べなければ出さない（Web の selected 無しの return null）。出し入れは親の SetItemShown（D-23）
            // Hide when no window can be selected (Web returns null without a selection); the parent's SetItemShown applies it (D-23)
            BeltThroughputWindow selected = null;
            _isApplicable.Value = _belt != null && BeltThroughputLogic.TrySelectBeltWindow(_belt.Windows, _cursor.SelectedTicks, out selected);
            if (_isApplicable.Value) _view.Render(BeltThroughputLogic.RowDisplay(selected), _belt.RecentSecondBars());
        }
    }
}
```
（`RecentSecondBars()` の戻りの型は Task 2 の `BeltThroughputDetail` に合わせ、`IReadOnlyList<float?>` へ渡せる形にする。`Root` は式本体の読み取り専用〈単純な getter の禁止に当たらない形は Global Constraints の規約どおり〉。`SP/Belt/` は 3 本）

- [ ] **Step 4: コンパイルしテストを実行して通ることを確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.Screens\.BlockInventory\.Special\."`
Expected: ErrorCount 0 / PASS（GearToElectric 14〈TestCase 込み。Web 13＋i18n 1〉・Gear 3・Belt 4・Network 4）。alpha の `LackHighlightText` の名前・クラスが想定と違えば Task 1 Step 2 の読み替えに従う

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Special
git commit -m "feat(uitk): 回転発電機・ベルトの流量・歯車・電力網・歯車網の区画と Presenter（S4-special）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 7: 本文 ElectricPole・ElectricToGear・FilterSplitter と依存の束

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Registration/SpecialViewKit.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Network/ElectricPoleBodyView.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Energy/ElectricToGearBodyView.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/FilterSplitter/FilterSplitterBodyView.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/FilterSplitter/FilterDirectionView.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Special/Support/SpecialScreenTestHost.cs`（`BlockSlots`・`Kit(GameContentPresentation)` を足す）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Special/Support/RecordingBlockSlotCommands.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Special/ElectricBodyViewTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Special/FilterSplitterBodyViewTest.cs`

**Interfaces:**
- Consumes: Task 4 の `ISpecialBlockCommands`（`SetFilterMode`・`SetFilterItem`・`SetElectricToGearMode`）、Task 6 の `ISpecialDetailRenderer`・`SpecialViewParts`・`ElectricNetworkSectionView`（本文の中では本文が `HasDetail` で出し入れする）・`SpecialScreenTestHost`・`SpecialDetailSamples`、部品 `ModeSwitch<T>`（`ModeSwitchOption<T>(testid, value, UiText)`・`OnOptionSelected`・`SetSelected`）・`GaugeBar(testid).SetValue(float)`・`ItemSlot(frameTestid)`（`OnSlotGestured`・`SetContent`）・`UiStack`・`UiGroup`、L0-s4b の `FilterSplitterLogic.ModeKey`、`UiCommandResultToaster.Report`、alpha の `BlockItemGridFactory`
- Produces:
```csharp
namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    public sealed class SpecialViewKit
    {
        public SpecialViewKit(ISpecialBlockCommands commands, GameContentPresentation presentation, UiCommandResultToaster toaster, BlockItemGridFactory grids);   // 購読の源（詳細の Model・パネル・矢印キー）は入れない。View は購読しない
        public ISpecialBlockCommands Commands { get; }
        public GameContentPresentation Presentation { get; }
        public BlockItemGridFactory Grids { get; }
        public void Run(string commandName, UniTask<UiCommandResult> command); // 結果をトーストの係へ（良性は警告ログだけ）
    }
}
// 本文: ElectricPoleBodyView()・ElectricToGearBodyView(SpecialViewKit kit)・FilterSplitterBodyView(SpecialViewKit kit)。どれも VisualElement かつ ISpecialDetailRenderer（HasDetail は常に true）
// 操作の名前（トーストと警告ログの名前。Web の action 名）: "electric_to_gear.set_output_mode"・"filter_splitter.set_mode"・"filter_splitter.set_filter_item"
```

- [ ] **Step 1: テストを書く**

`Support/RecordingBlockSlotCommands.cs`（alpha の `IBlockSlotCommands` の記録係。alpha の格子の押下が絶対添字で届くかを見る）:
```csharp
using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.Commands;
using Client.Ui.State.SlotCommands.Block;

namespace Client.Tests.Ui.Screens.BlockInventory.Special.Support
{
    // alpha の格子から届くブロックの枠の押下を 1 行ずつ記録する（結果は常に受理）
    // Records block-slot presses arriving from alpha's grid, one line each (always accepted)
    public sealed class RecordingBlockSlotCommands : CommandRecorder<string>, IBlockSlotCommands
    {
        public UiCommandResult LeftDown(int blockSlot, bool shift) => Reply($"LeftDown {blockSlot}{(shift ? " shift" : "")}");
        public UiCommandResult RightDown(int blockSlot) => Reply($"RightDown {blockSlot}");
        public UiCommandResult DoubleClick(int blockSlot) => Reply($"DoubleClick {blockSlot}");

        private UiCommandResult Reply(string line)
        {
            Record(line);
            return UiCommandAccepted.Instance;
        }
    }
}
```

`SpecialScreenTestHost.cs` に足す（`[SetUp]` で `BlockSlots = new RecordingBlockSlotCommands();` も足す）:
```csharp
        protected RecordingBlockSlotCommands BlockSlots { get; private set; }

        // 本文の L1 が使う依存の束（格子は alpha の本物の工場に記録係の押下先を渡す。トーストは ScreenTestHost の係）
        // Dependency bundle for body L1 tests (alpha's real grid factory with the recording press target; toasts from ScreenTestHost)
        protected SpecialViewKit Kit(GameContentPresentation presentation)
        {
            return new SpecialViewKit(Commands, presentation, Toaster, new BlockItemGridFactory(BlockSlots, presentation, Toaster));
        }
```
（`BlockItemGridFactory` の ctor〈`IBlockSlotCommands`・`GameContentPresentation`・`UiCommandResultToaster`〉は S4-alpha 計画 85f905aa3d の記述〈共有サービス。`MainGameUiRegistration` の共有サービスの並び〉。実物と違えば実物に合わせる。`Support/` は 5 本）

`ElectricBodyViewTest.cs`:
```csharp
using System.Linq;
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.Components.Controls.ModeSwitch;
using Client.Ui.Screens.BlockInventory.Special.Energy;
using Client.Ui.Screens.BlockInventory.Special.Network;
using Client.Ui.State.BlockInventory.Special.Text;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special
{
    /// <summary>
    /// 電柱の本文（電力網の集約）と回転生成機の本文（出力モード・充足率・消費電力・選択の送信）（Web の e2e を L1 で）
    /// Electric pole body (network aggregate) and electric-to-gear body (output modes, fulfillment, consumed power, selection) (Web e2e as L1)
    /// </summary>
    public class ElectricBodyViewTest : SpecialScreenTestHost
    {
        /// <web-test file="e2e/tests/block/electric/electricPole.spec.ts">電柱専用ビューに発電量・需要量・消費者数・供給率を表示する</web-test>
        [Test]
        public void 電柱の本文に発電量と需要量と消費者数と供給率を出す()
        {
            var view = Mount(new ElectricPoleBodyView());
            view.Render(NoSlots, SpecialDetailSamples.Only(SpecialDetailSamples.PoleNetwork));

            Assert.IsTrue(IsShown(Find<VisualElement>(view, "electric-pole-view")));
            var summary = TextOf(view, "electric-network-summary");
            foreach (var part in new[] { "240", "180", "3", "100%" }) StringAssert.Contains(part, summary);
        }

        /// <web-test file="e2e/tests/block/electric/electricToGear.spec.ts">出力モード行とStateDetail値を表示し、選択indexをaction送信する</web-test>
        [Test]
        public void 出力モードの行と状態の値を出し選んだ添字を送る()
        {
            var view = Mount(new ElectricToGearBodyView(Kit(null)));
            var detail = SpecialDetailSamples.ElectricToGear;
            view.Render(NoSlots, SpecialDetailSamples.Only(detail));

            Assert.IsTrue(IsShown(Find<VisualElement>(view, "electric-to-gear-view")));
            Assert.AreEqual(Resolved(SpecialDetailTexts.ElectricToGearOutputMode(detail.OutputModes[0])), Find<Button>(view, "electric-to-gear-mode-0").text);
            StringAssert.Contains("10 rpm / 10 trq / 10 W", Find<Button>(view, "electric-to-gear-mode-0").text);
            StringAssert.Contains("10 W", TextOf(view, "electric-to-gear-consumed-power"));
            Assert.IsTrue(IsSelected(view, "electric-to-gear-mode-1"));
            Assert.AreEqual(0.75f, GaugeValue(view, "electric-to-gear-fulfillment"), 0.001f);

            simulate.Click(Find<Button>(view, "electric-to-gear-mode-0"));
            CollectionAssert.AreEqual(new[] { "electric_to_gear.set_output_mode 0" }, Commands.Issued);
        }

        [Test]
        public void 同じモードの一覧の更新では切り替えを作り直さない()
        {
            var view = Mount(new ElectricToGearBodyView(Kit(null)));
            view.Render(NoSlots, SpecialDetailSamples.Only(SpecialDetailSamples.ElectricToGear));
            var before = view.Q<ModeSwitch<int>>();

            view.Render(NoSlots, SpecialDetailSamples.Only(new Client.Ui.State.BlockInventory.Special.Details.ElectricToGearDetail(SpecialDetailSamples.ElectricToGear.OutputModes, 0, 0.5, 20)));
            Assert.AreSame(before, view.Q<ModeSwitch<int>>());
            Assert.IsTrue(IsSelected(view, "electric-to-gear-mode-0"));
            Assert.AreEqual(0.5f, GaugeValue(view, "electric-to-gear-fulfillment"), 0.001f);
        }
    }
}
```
（Web の `electricToGearOutputMode` の文〈`{rpm} rpm / {torque} trq / {power} W`〉は `SpecialDetailTexts.ElectricToGearOutputMode` が組む。`ModeSwitchOption` の押し面が `Button` でなければ `Find<VisualElement>` と `simulate.Click` の実物の形に合わせる〈前例 `Client.Tests/Ui/Screens/Settings` の `simulate.Click(Find<Button>(view, "sprint-mode-option-hold"))`〉）

`FilterSplitterBodyViewTest.cs`:
```csharp
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.Screens.BlockInventory.Special.FilterSplitter;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special
{
    /// <summary>
    /// フィルタ分配器: 方向ごとのモードの明示の送信・枠の右押下の解除・短い窓で末尾の枠に届く（Web filterSplitter.spec.ts）
    /// Filter splitter: explicit mode per direction, right-down clears a slot, the last slot is reachable on a short window (Web filterSplitter.spec.ts)
    /// </summary>
    public class FilterSplitterBodyViewTest : SpecialScreenTestHost
    {
        /// <web-test file="e2e/tests/block/filterSplitter.spec.ts">mode switch sends explicitly selected mode</web-test>
        [Test]
        public void 択一でブラックリストを選ぶとそのモードを明示して送る()
        {
            var view = Render();
            Assert.IsTrue(IsSelected(view, "filter-mode-0-Whitelist"));
            simulate.Click(Find<Button>(view, "filter-mode-0-Blacklist"));
            CollectionAssert.AreEqual(new[] { "filter_splitter.set_mode 0 Blacklist" }, Commands.Issued);
        }

        /// <web-test file="e2e/tests/block/filterSplitter.spec.ts">right click on a filter slot sends clear</web-test>
        [Test]
        public void 枠の右押下は解除を送る()
        {
            var view = Render();
            simulate.Click(Find<VisualElement>(view, "filter-slot-0-0"), MouseButton.RightMouse);
            CollectionAssert.AreEqual(new[] { "filter_splitter.set_filter_item 0 0 clear" }, Commands.Issued);
        }

        /// <web-test file="e2e/tests/block/filterSplitter.spec.ts">last direction's filter slot is reachable by scrolling on a short window</web-test>
        [Test]
        public void 短い窓でも末尾の方向の枠へスクロールで届き解除を送る()
        {
            // alpha の外殻（自然高のスクロール器）に載せ、600px の高さで末尾の枠を見える所へ送る
            // Mount on alpha's shell (natural-height scroller) and bring the last slot into view at 600px height
            var panel = BlockInventoryTestBodies.ShellWithBody(new FilterSplitterBodyView(Kit(LoadContent())), new Vector2(1280, 600));
            Mount(panel.Root);
            ((FilterSplitterBodyView)panel.Body).Render(NoSlots, SpecialDetailSamples.Only(SpecialDetailSamples.FilterSplitter));
            simulate.FrameUpdate();

            var last = Find<VisualElement>(panel.Root, "filter-slot-2-1");
            panel.ScrollTo(last);
            simulate.FrameUpdate();
            Assert.IsTrue(panel.IsInScrollViewport(last), "スクロールの後に末尾の枠が見える");
            simulate.Click(last, MouseButton.RightMouse);
            Assert.AreEqual("filter_splitter.set_filter_item 2 1 clear", Commands.Issued[Commands.Issued.Count - 1]);
        }

        [Test]
        public void 左押下は設定を送り掴みが空なら操作の側で良性に断る()
        {
            var view = Render();
            Commands.SetNextResult(new Client.Ui.State.Commands.UiCommandRejected(Client.Ui.State.Commands.UiCommandRejectReason.FilterSplitterGrabEmpty, string.Empty));
            simulate.Click(Find<VisualElement>(view, "filter-slot-1-0"), MouseButton.LeftMouse);
            CollectionAssert.AreEqual(new[] { "filter_splitter.set_filter_item 1 0 set" }, Commands.Issued);
            CollectionAssert.IsEmpty(ShownToasts, "掴みが空は良性（Web の noop）");
        }

        private FilterSplitterBodyView Render()
        {
            var view = Mount(new FilterSplitterBodyView(Kit(LoadContent())));
            view.Render(NoSlots, SpecialDetailSamples.Only(SpecialDetailSamples.FilterSplitter));
            return view;
        }
    }
}
```
（3 件目の `BlockInventoryTestBodies.ShellWithBody`・`ScrollTo`・`IsInScrollViewport` は alpha の L1 の土台〈外殻と自然高のスクロール器を組む物〉の実物の名前に読み替える。alpha の土台に「本文を差し込んだ外殻」を組む手段が無ければ、この件は書かず（`[Ignore]` で残さない）、タグごと消して、`done.md` の「## 部分移植の残り」に「`filterSplitter.spec.ts` の 3 件目: 外殻のスクロールに載せる土台が無い。録画 R-12 の分配器の段で末尾の枠の解除を見る」と書く〈D-24〉。フィルタ枠の絵はマスタが要るので、このテストは `Kit(LoadContent())`〈テスト用 mod のマスタを読む。Task 6 Step 1〉で組む。`FilterDirectionView` は `ItemMaster.EmptyItemId` の枠だけを空きマスにし、他は `Presentation.Item(id)` を呼ぶ）

- [ ] **Step 2: 実装を書く**

`Registration/SpecialViewKit.cs`:
```csharp
using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.State.BlockInventory.Special.Commands;
using Client.Ui.State.Commands;
using Client.Ui.State.Toast;
using Cysharp.Threading.Tasks;

namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    /// <summary>
    /// 特殊族の View が受ける依存の束（操作・表示素材・alpha の格子の工場）と、操作の結果の届け先。購読の源は入れない（Presenter の SpecialPresenters が持つ）
    /// Dependency bundle the special views receive (commands, content, alpha's grid factory) and where command results go; no subscription sources (SpecialPresenters holds them)
    /// </summary>
    public sealed class SpecialViewKit
    {
        private readonly UiCommandResultToaster _toaster;

        public ISpecialBlockCommands Commands { get; }
        public GameContentPresentation Presentation { get; }
        public BlockItemGridFactory Grids { get; }

        public SpecialViewKit(ISpecialBlockCommands commands, GameContentPresentation presentation, UiCommandResultToaster toaster, BlockItemGridFactory grids)
        {
            Commands = commands;
            Presentation = presentation;
            _toaster = toaster;
            Grids = grids;
        }

        // 操作の結果を待ってトーストの係へ渡す（拒否は警告ログ、良性でなければトースト。楽観更新はしない。D-5）
        // Await the result and hand it to the toaster (rejects log a warning, non-benign ones toast; no optimistic update. D-5)
        public void Run(string commandName, UniTask<UiCommandResult> command)
        {
            command.ContinueWith(result => _toaster.Report(commandName, result)).Forget();
        }
    }
}
```

`Network/ElectricPoleBodyView.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Network
{
    /// <summary>
    /// 電柱の本文。電力網の区画だけを包む（Web ElectricPoleInventory）
    /// Electric pole body wrapping only the electric network section (Web ElectricPoleInventory)
    /// </summary>
    public sealed class ElectricPoleBodyView : VisualElement, ISpecialDetailRenderer
    {
        private readonly ElectricNetworkSectionView _network = new();

        public ElectricPoleBodyView()
        {
            name = "electric-pole-view";
            AddToClassList("electric-pole-view");
            Add(_network);
        }

        public bool HasDetail(SpecialBlockDetailViewModel detail) => true;

        public void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)
        {
            // 区画は自分の display を触らないので、包む本文が詳細の有無で出し入れする（Web の ElectricNetworkSection の return null）
            // Sections never touch their own display, so the wrapping body toggles it by detail presence (Web ElectricNetworkSection returns null)
            var present = _network.HasDetail(detail);
            SpecialViewParts.SetShown(_network, present);
            if (present) _network.Render(itemSlots, detail);
        }
    }
}
```

`Energy/ElectricToGearBodyView.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.Components.Controls.ModeSwitch;
using Client.Ui.Components.Gauges;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Text;
using Client.Ui.State.Items;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Energy
{
    /// <summary>
    /// 回転生成機の本文。縦の択一で出力モード、充足率のゲージ、消費電力（Web ElectricToGearInventory）
    /// Electric-to-gear body: vertical exclusive switch of output modes, fulfillment gauge, consumed power (Web ElectricToGearInventory)
    /// </summary>
    public sealed class ElectricToGearBodyView : VisualElement, ISpecialDetailRenderer
    {
        public const string CommandName = "electric_to_gear.set_output_mode";

        private readonly SpecialViewKit _kit;
        private readonly UiStack _stack = new(UiGap.Sm);
        private readonly VisualElement _modeHolder = new();
        private readonly GaugeBar _fulfillment = new("electric-to-gear-fulfillment");
        private readonly LocalizedLabel _consumed = SpecialViewParts.Label("electric-to-gear-consumed-power", UiTypographyClass.TextSm);
        private ModeSwitch<int> _modes;
        private ElectricToGearDetail _shownModes;

        public ElectricToGearBodyView(SpecialViewKit kit)
        {
            _kit = kit;
            name = "electric-to-gear-view";
            AddToClassList("electric-to-gear-view");
            var fulfillmentLabel = SpecialViewParts.Label(null, UiTypographyClass.TextSm);
            fulfillmentLabel.SetText(new UiText(LocalizationKeys.Ui.BlockInventory.ElectricToGearFulfillment));
            _stack.AddItem(_modeHolder);
            _stack.AddItem(fulfillmentLabel);
            _stack.AddItem(_fulfillment);
            _stack.AddItem(_consumed);
            Add(_stack);
        }

        public bool HasDetail(SpecialBlockDetailViewModel detail) => true;

        public void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)
        {
            // 詳細の未着は Web と同じく本文を空にする（Web は詳細無しで null）
            // Without detail the body stays empty, as the Web returns null
            var present = detail.TryGetElectricToGear(out var electricToGear);
            SpecialViewParts.SetShown(_stack, present);
            if (!present) return;

            // 択一はモードの一覧が変わったときだけ作り直す（毎回作るとフォーカスと押下が切れる。D-21）
            // Rebuild the switch only when the mode list changes (rebuilding every time drops focus and presses. D-21)
            if (_shownModes == null || !_shownModes.HasSameModes(electricToGear)) RebuildModes(electricToGear);
            _shownModes = electricToGear;
            _modes.SetSelected(electricToGear.SelectedIndex);
            _fulfillment.SetValue((float)electricToGear.FulfillmentRate);
            _consumed.SetText(SpecialDetailTexts.ElectricToGearConsumedPower(electricToGear));

            #region Internal

            void RebuildModes(ElectricToGearDetail source)
            {
                var options = new List<ModeSwitchOption<int>>();
                for (var i = 0; i < source.OutputModes.Count; i++) options.Add(new ModeSwitchOption<int>($"electric-to-gear-mode-{i}", i, SpecialDetailTexts.ElectricToGearOutputMode(source.OutputModes[i])));
                _modeHolder.Clear();
                _modes = new ModeSwitch<int>("electric-to-gear-modes", ModeSwitchOrientation.Vertical, ModeSwitchRole.Group, options);
                _modes.OnOptionSelected.Subscribe(index => _kit.Run(CommandName, _kit.Commands.SetElectricToGearMode(index)));
                _modeHolder.Add(_modes);
            }

            #endregion
        }
    }
}
```
（`SpecialViewParts.Label(null, …)` は name を付けない。Web の充足率の見出しは testid を持たない。Web の消費電力の `Text` は色の指定が無いので `text--sm` だけ）

`FilterSplitter/FilterDirectionView.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.Components.Controls.ModeSwitch;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Text;
using Client.Ui.State.Text;
using Core.Master;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.FilterSplitter
{
    /// <summary>
    /// フィルタ分配器の方向 1 つ: 「出力 n」・横の択一（既定/ホワイト/ブラック）・フィルタ枠（左押下で設定、右押下で解除）
    /// One filter splitter direction: "Output n", horizontal exclusive switch (default/white/black), filter slots (left-down sets, right-down clears)
    /// </summary>
    public sealed class FilterDirectionView : VisualElement
    {
        public const string SetModeCommandName = "filter_splitter.set_mode";
        public const string SetItemCommandName = "filter_splitter.set_filter_item";
        private static readonly FilterSplitterMode[] Modes = { FilterSplitterMode.Default, FilterSplitterMode.Whitelist, FilterSplitterMode.Blacklist };

        private readonly SpecialViewKit _kit;
        private readonly int _directionIndex;
        private readonly ModeSwitch<FilterSplitterMode> _mode;
        private readonly UiGroup _slots = new(UiGap.Px4);
        private readonly List<ItemSlot> _slotViews = new();

        public FilterDirectionView(SpecialViewKit kit, int directionIndex)
        {
            _kit = kit;
            _directionIndex = directionIndex;
            name = $"filter-direction-{directionIndex}";
            var stack = new UiStack(UiGap.Xs);
            var output = SpecialViewParts.Label(null, UiTypographyClass.TextSm, UiTypographyClass.TextDefault);
            output.SetText(SpecialDetailTexts.OutputNumber(directionIndex));

            // 択一の選択肢は Web の filterModes の順。押下は目標のモードを明示して送る
            // Options follow the Web filterModes order; a press sends the target mode explicitly
            var options = new List<ModeSwitchOption<FilterSplitterMode>>();
            foreach (var mode in Modes) options.Add(new ModeSwitchOption<FilterSplitterMode>($"filter-mode-{directionIndex}-{mode}", mode, new UiText(FilterSplitterLogic.ModeKey(mode))));
            _mode = new ModeSwitch<FilterSplitterMode>($"filter-mode-{directionIndex}", ModeSwitchOrientation.Horizontal, ModeSwitchRole.Group, options);
            _mode.OnOptionSelected.Subscribe(mode => _kit.Run(SetModeCommandName, _kit.Commands.SetFilterMode(_directionIndex, mode)));

            stack.AddItem(output);
            stack.AddItem(_mode);
            stack.AddItem(_slots);
            Add(stack);
        }

        public void Render(FilterSplitterDirectionDetail direction)
        {
            _mode.SetSelected(direction.Mode);
            EnsureSlotCount(direction.FilterItemIds.Count);
            for (var i = 0; i < _slotViews.Count; i++) _slotViews[i].SetContent(Content(direction.FilterItemIds[i]));

            #region Internal

            void EnsureSlotCount(int count)
            {
                // 枠の数はマスタで決まり開いている間は変わらない。違えば作り直す
                // The slot count comes from the master and stays while open; rebuild when it differs
                if (_slotViews.Count == count) return;
                _slots.ClearItems();
                _slotViews.Clear();
                for (var slotIndex = 0; slotIndex < count; slotIndex++)
                {
                    var slot = new ItemSlot($"filter-slot-{_directionIndex}-{slotIndex}");
                    var captured = slotIndex;
                    slot.OnSlotGestured.Subscribe(gesture => OnGesture(captured, gesture.Kind));
                    _slotViews.Add(slot);
                    _slots.AddItem(slot);
                }
            }

            ItemSlotContent Content(ItemId itemId)
            {
                var icon = itemId == ItemMaster.EmptyItemId ? null : _kit.Presentation.Item(itemId);
                return new ItemSlotContent(icon, null, new ItemSlotMarks(false, false, false), null, null, null);
            }

            #endregion
        }

        private void OnGesture(int slotIndex, SlotGestureKind kind)
        {
            // 左押下は設定、右押下は解除。掴みが空の設定は操作の側が良性に断る（Web の noop）
            // Left-down sets and right-down clears; an empty-grab set is benignly refused by the command (Web noop)
            // silent-ok: 押下以外の身振り（ホバー・なぞり・ダブルクリック）はフィルタ枠の操作ではない / Web の ItemSlot も onLeftDown・onRightDown だけを渡す
            if (kind != SlotGestureKind.LeftDown && kind != SlotGestureKind.RightDown) return;
            _kit.Run(SetItemCommandName, _kit.Commands.SetFilterItem(_directionIndex, slotIndex, kind == SlotGestureKind.RightDown));
        }
    }
}
```
（`ItemSlotContent` の count に null を渡すと数字を出さない〈Web の `ItemSlot` に count を渡さないのと同じ〉。ctor の引数の型が実物と違えば実物に合わせる。`SpecialViewParts.Label` の testid が null のとき name を付けない形は Task 6 の実装〈`name = testid`〉で null が入るだけなので同じ）

`FilterSplitter/FilterSplitterBodyView.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.Foundation.Layout;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.FilterSplitter
{
    /// <summary>
    /// フィルタ分配器の本文。方向を縦に積む（Web FilterSplitterInventory。詳細の未着は何も出さない）
    /// Filter splitter body stacking directions vertically (Web FilterSplitterInventory; nothing before the detail arrives)
    /// </summary>
    public sealed class FilterSplitterBodyView : VisualElement, ISpecialDetailRenderer
    {
        private readonly SpecialViewKit _kit;
        private readonly UiStack _stack = new(UiGap.Sm);
        private readonly List<FilterDirectionView> _directions = new();

        public FilterSplitterBodyView(SpecialViewKit kit)
        {
            _kit = kit;
            name = "filter-splitter";
            AddToClassList("filter-splitter");
            Add(_stack);
        }

        public bool HasDetail(SpecialBlockDetailViewModel detail) => true;

        public void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)
        {
            var present = detail.TryGetFilterSplitter(out var filter);
            SpecialViewParts.SetShown(_stack, present);
            if (!present) return;

            // 方向の数が変わったときだけ作り直す（開いている間はマスタで固定）
            // Rebuild only when the direction count changes (fixed by the master while open)
            if (_directions.Count != filter.Directions.Count)
            {
                _stack.ClearItems();
                _directions.Clear();
                for (var d = 0; d < filter.Directions.Count; d++)
                {
                    var direction = new FilterDirectionView(_kit, d);
                    _directions.Add(direction);
                    _stack.AddItem(direction);
                }
            }
            for (var d = 0; d < _directions.Count; d++) _directions[d].Render(filter.Directions[d]);
        }
    }
}
```

- [ ] **Step 3: コンパイルしテストを実行して通ることを確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.Screens\.BlockInventory\.Special\.(ElectricBodyViewTest|FilterSplitterBodyViewTest)\."`
Expected: ErrorCount 0 / PASS（Electric 3・FilterSplitter 4。3 件目を部分移植にしたときは 3）

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Special
git commit -m "feat(uitk): 電柱・回転生成機・フィルタ分配器の本文（S4-special）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 8: 発射台（本文・題の操作）と宇宙エレベーター（本文・状態区画）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Space/RocketLaunchPadBodyView.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Space/RocketLaunchPadTitleActionView.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Space/SpaceElevatorBodyView.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Space/SpaceElevatorSectionView.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Special/RocketLaunchPadViewTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Special/SpaceElevatorViewTest.cs`

**Interfaces:**
- Consumes: Task 2 の `RocketLaunchPadDetail`（`OutputSlots(itemSlotCount)`）・`SpaceElevatorDetail`・`SpecialDetailTexts.SpaceElevatorPool`、Task 4 の `SendRocketCommand`・`ConnectSpaceElevator`・良性の `SpecialBlockCommandStale`、Task 6 の `ElectricNetworkSectionView`・`SpecialViewParts`、Task 7 の `SpecialViewKit`・`RecordingBlockSlotCommands`・`Kit`、L0-s4b の `RocketLaunchPadLogic.StatusKey`・`TryGetCommandButton`・`SpaceElevatorLogic.StatusDisplay`、部品 `GaugeBar`・`PanelActionButton(testid, LocalizationKey)`（`OnClicked`・`SetEnabled`）、alpha の `BlockItemGridFactory.Create`・`BlockItemGrid.Render`・`BlockItemGridGroup(label, grid)`・`LackHighlightText`
- Produces:
```csharp
namespace Client.Ui.Screens.BlockInventory.Special.Space
{
    public sealed class RocketLaunchPadBodyView : VisualElement, ISpecialDetailRenderer { public RocketLaunchPadBodyView(SpecialViewKit kit); }
    public sealed class RocketLaunchPadTitleActionView : VisualElement, ISpecialDetailRenderer { public const string CommandName = "rocket_launch_pad.command"; public RocketLaunchPadTitleActionView(SpecialViewKit kit); }
    public sealed class SpaceElevatorBodyView : VisualElement, ISpecialDetailRenderer { public SpaceElevatorBodyView(SpecialViewKit kit); }
    public sealed class SpaceElevatorSectionView : VisualElement { public const string CommandName = "space_elevator.connect"; public SpaceElevatorSectionView(SpecialViewKit kit); public void Render(SpecialBlockDetailViewModel detail); }
}
```

- [ ] **Step 1: テストを書く**

`RocketLaunchPadViewTest.cs`:
```csharp
using System.Linq;
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.Screens.BlockInventory.Special.Space;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Commands;
using Client.Ui.State.Items;
using Client.Ui.State.Text;
using Core.Master;
using Game.Block.Interface.State;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special
{
    /// <summary>
    /// 発射台の本文と題の操作（Web RocketLaunchPadInventory.test.tsx の写せる 5 件。TS の型検査の 1 件は捨てる。D-13）
    /// Launch pad body and title action (the five portable cases of Web RocketLaunchPadInventory.test.tsx; the TS type-check case is discarded. D-13)
    /// </summary>
    public class RocketLaunchPadViewTest : SpecialScreenTestHost
    {
        /// <web-test file="src/features/blockInventory/views/__tests__/RocketLaunchPadInventory.test.tsx">a mismatched registry key shows pending detail without throwing</web-test>
        [Test]
        public void 発射台の詳細が無い種別では取得待ちを出し落ちない()
        {
            var view = Mount(new RocketLaunchPadBodyView(Kit(null)));
            view.Render(NoSlots, SpecialDetailSamples.Only(SpecialDetailSamples.PoleNetwork));
            Assert.IsTrue(IsShown(Find<Label>(view, "rocket-launch-pad-loading")));
            Assert.IsFalse(IsShown(Find<VisualElement>(view, "rocket-launch-pad-view")));
        }

        /// <web-test file="src/features/blockInventory/views/__tests__/RocketLaunchPadInventory.test.tsx">waits for initial detail and renders when the topic arrives</web-test>
        [Test]
        public void 詳細の未着は取得待ちで届いたら本文を描く()
        {
            var view = Mount(new RocketLaunchPadBodyView(Kit(null)));
            view.Render(NoSlots, SpecialBlockDetailViewModel.Empty);
            Assert.IsTrue(IsShown(Find<Label>(view, "rocket-launch-pad-loading")));

            view.Render(ThreeSlots(), SpecialDetailSamples.Only(SpecialDetailSamples.Rocket(RocketLaunchPadPhase.AwaitingBuildCommand)));
            Assert.IsFalse(IsShown(Find<Label>(view, "rocket-launch-pad-loading")));
            Assert.IsTrue(IsShown(Find<VisualElement>(view, "rocket-launch-pad-view")));
        }

        /// <web-test file="src/features/blockInventory/views/__tests__/RocketLaunchPadInventory.test.tsx">dispatches both commands from GamePanel.titleAction and hides them outside command waits</web-test>
        [Test]
        public void 題の操作は合図待ちの段階だけ出て押すと合図を送る()
        {
            var action = Mount(new RocketLaunchPadTitleActionView(Kit(null)));
            action.Render(NoSlots, SpecialDetailSamples.Only(SpecialDetailSamples.Rocket(RocketLaunchPadPhase.AwaitingBuildCommand)));
            simulate.Click(Find<Button>(action, "rocket-launch-pad-build"));
            action.Render(NoSlots, SpecialDetailSamples.Only(SpecialDetailSamples.Rocket(RocketLaunchPadPhase.AwaitingLaunchCommand)));
            Assert.IsFalse(IsShown(Find<Button>(action, "rocket-launch-pad-build")));
            simulate.Click(Find<Button>(action, "rocket-launch-pad-launch"));
            CollectionAssert.AreEqual(new[] { "rocket_launch_pad.command Build", "rocket_launch_pad.command Launch" }, Commands.Issued);

            action.Render(NoSlots, SpecialDetailSamples.Only(SpecialDetailSamples.Rocket(RocketLaunchPadPhase.Building)));
            Assert.IsFalse(IsShown(Find<Button>(action, "rocket-launch-pad-launch")));
            Assert.IsFalse(IsShown(action), "合図待ち以外は題の操作ごと出さない");
        }

        /// <web-test file="src/features/blockInventory/views/__tests__/RocketLaunchPadInventory.test.tsx">%sの成功後topicが遅れても二度目の拒否を誤報しない</web-test>
        [TestCase(RocketCommand.Build, RocketLaunchPadPhase.AwaitingBuildCommand, RocketLaunchPadPhase.Building, "rocket-launch-pad-build")]
        [TestCase(RocketCommand.Launch, RocketLaunchPadPhase.AwaitingLaunchCommand, RocketLaunchPadPhase.ReadyToLaunch, "rocket-launch-pad-launch")]
        public void 成功の後に段階の更新が遅れても二度目の古い合図の拒否をトーストしない(RocketCommand command, RocketLaunchPadPhase phase, RocketLaunchPadPhase nextPhase, string testid)
        {
            var action = Mount(new RocketLaunchPadTitleActionView(Kit(null)));
            action.Render(NoSlots, SpecialDetailSamples.Only(SpecialDetailSamples.Rocket(phase)));
            simulate.Click(Find<Button>(action, testid));
            Commands.SetNextResult(new UiCommandRejected(UiCommandRejectReason.SpecialBlockCommandStale, command == RocketCommand.Build ? "rocket_command_not_awaiting_build" : "rocket_command_not_awaiting_launch"));
            simulate.Click(Find<Button>(action, testid));

            Assert.AreEqual(2, Commands.Issued.Count);
            CollectionAssert.IsEmpty(ShownToasts);
            action.Render(NoSlots, SpecialDetailSamples.Only(SpecialDetailSamples.Rocket(nextPhase)));
            Assert.IsFalse(IsShown(Find<Button>(action, testid)));
        }

        /// <web-test file="src/features/blockInventory/views/__tests__/RocketLaunchPadInventory.test.tsx">renders DTO groups and preserves their absolute indices through real mouse gestures</web-test>
        [Test]
        public void 群を描き実際の押下でもサーバーの絶対添字を保つ()
        {
            var view = Mount(new RocketLaunchPadBodyView(Kit(LoadContent())));
            var slots = new[] { new SlotItem(new ItemId(3), 20), new SlotItem(new ItemId(4), 5), new SlotItem(new ItemId(5), 7), new SlotItem(new ItemId(1), 2) };
            var rocket = new RocketLaunchPadDetail(RocketLaunchPadPhase.AwaitingBuildCommand, RocketLaunchPadWaitReason.OutputFull, 0.375f, new[] { 2 }, new[] { 0 }, 3);
            view.Render(slots, SpecialDetailSamples.Only(rocket));

            Assert.AreEqual(0.375f, GaugeValue(view, "rocket-launch-pad-progress"), 0.001f);
            Assert.AreEqual(new UiText(RocketLaunchPadLogic.StatusKey(rocket.Phase, rocket.WaitReason)).Resolve(), TextOf(view, "rocket-launch-pad-status"));
            foreach (var (group, index) in new[] { ("material", 2), ("payload", 0), ("output", 3) })
            {
                var frame = Find<VisualElement>(view, $"rocket-launch-pad-{group}-slots-slot-{index}");
                simulate.Click(frame);
                simulate.Click(frame, MouseButton.RightMouse);
                simulate.DoubleClick(frame);
                CollectionAssert.AreEqual(new[] { $"LeftDown {index}", $"RightDown {index}", $"DoubleClick {index}" }, BlockSlots.Issued.Skip(BlockSlots.Issued.Count - 3).ToArray());
            }
        }

        private static SlotItem[] ThreeSlots()
        {
            return new[] { new SlotItem(ItemMaster.EmptyItemId, 0), new SlotItem(ItemMaster.EmptyItemId, 0), new SlotItem(ItemMaster.EmptyItemId, 0) };
        }
    }
}
```
（Web の 5 件目は押下の後の送信 payload〈`block_inventory.move_item` 等〉まで見る。押下 → 絶対添字は本件、添字 → 持ち物の移動は alpha の `BlockSlotCommandsTest` が見る。分けたことを `done.md` の「## 部分移植の残り」に書く。`RocketLaunchPadPhase`・`RocketLaunchPadWaitReason` の値の名前〈`AwaitingBuildCommand`・`Building`・`AwaitingLaunchCommand`・`ReadyToLaunch`・`OutputFull`〉は `Game.Block.Interface.State` の実物に合わせる。`simulate.DoubleClick` は前例 `SlotFrameGestureTest`）

`SpaceElevatorViewTest.cs`:
```csharp
using System.Linq;
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Special.Space;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;
using Client.Ui.State.Text;
using Core.Master;
using Game.Block.Interface.State;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special
{
    /// <summary>
    /// 宇宙エレベーターの本文（入口・出口の群・状態区画・電力網）（Web SpaceElevatorInventory / SpaceElevatorSection の vitest と spaceElevator.spec.ts）
    /// Space elevator body (inlet and outlet groups, state section, electric network) (Web SpaceElevatorInventory / SpaceElevatorSection vitests and spaceElevator.spec.ts)
    /// </summary>
    public class SpaceElevatorViewTest : SpecialScreenTestHost
    {
        /// <web-test file="src/features/blockInventory/details/spaceElevator/__tests__/SpaceElevatorInventory.test.tsx">空スロット=%sでも入口1枠と全出口を識別し元データを保持する</web-test>
        [TestCase(false)]
        [TestCase(true)]
        public void 空きでも入口1枠と全出口を元の添字で出す(bool empty)
        {
            var view = Render(Slots(empty), Unconnected(SpaceElevatorBaseStatus.ReadyToConnect));
            Assert.AreEqual(new[] { "space-elevator-input-grid-slot-0" }, FrameIds(view, "space-elevator-input-grid"));
            Assert.AreEqual(new[] { "space-elevator-output-grid-slot-1", "space-elevator-output-grid-slot-2" }, FrameIds(view, "space-elevator-output-grid"));
        }

        /// <web-test file="src/features/blockInventory/details/spaceElevator/__tests__/SpaceElevatorSection.test.tsx">ケーブル不足なら接続できず不足の理由を示す</web-test>
        [Test]
        public void ケーブル不足は接続できず不足の色で理由を出す()
        {
            var view = Render(Slots(true), SpecialDetailSamples.SpaceElevator(SpaceElevatorBaseStatus.InsufficientCable, false, 2000, 0f));
            Assert.IsFalse(Find<Button>(view, "space-elevator-connect").enabledSelf);
            Assert.IsTrue(Find<Label>(view, "space-elevator-status").ClassListContains(LackHighlightText.InsufficientClassName));
        }

        /// <web-test file="src/features/blockInventory/details/spaceElevator/__tests__/SpaceElevatorSection.test.tsx">接続可能なら空payloadのactionを送る</web-test>
        [Test]
        public void 接続可能なら接続を送る()
        {
            var view = Render(Slots(true), Unconnected(SpaceElevatorBaseStatus.ReadyToConnect));
            simulate.Click(Find<Button>(view, "space-elevator-connect"));
            CollectionAssert.AreEqual(new[] { "space_elevator.connect" }, Commands.Issued);
        }

        /// <web-test file="src/features/blockInventory/details/spaceElevator/__tests__/SpaceElevatorSection.test.tsx">接続済みなら進捗を描き接続ボタンを隠す</web-test>
        [Test]
        public void 接続済みは進捗を描き接続ボタンを隠す()
        {
            var view = Render(Slots(true), SpecialDetailSamples.SpaceElevator(SpaceElevatorBaseStatus.Climbing, true, 1000, 0.5f));
            Assert.AreEqual(0.5f, GaugeValue(view, "space-elevator-progress"), 0.001f);
            Assert.IsFalse(IsShown(Find<Button>(view, "space-elevator-connect")));
        }

        /// <web-test file="src/features/blockInventory/details/spaceElevator/__tests__/SpaceElevatorSection.test.tsx">%s の文言と不足色を表示する</web-test>
        [TestCase(SpaceElevatorBaseStatus.ReadyToConnect, false)]
        [TestCase(SpaceElevatorBaseStatus.InsufficientCable, true)]
        [TestCase(SpaceElevatorBaseStatus.WaitingSatellite, false)]
        [TestCase(SpaceElevatorBaseStatus.Climbing, false)]
        [TestCase(SpaceElevatorBaseStatus.LowPower, true)]
        [TestCase(SpaceElevatorBaseStatus.InletBlocked, true)]
        [TestCase(SpaceElevatorBaseStatus.OutputBlocked, true)]
        public void 状態ごとの文言と不足色を出す(SpaceElevatorBaseStatus status, bool insufficient)
        {
            Assert.AreEqual(insufficient, SpaceElevatorLogic.StatusDisplay(status).Insufficient);
            var view = Render(Slots(true), Unconnected(status));
            Assert.AreEqual(new UiText(SpaceElevatorLogic.StatusDisplay(status).LabelKey).Resolve(), TextOf(view, "space-elevator-status"));
            Assert.AreEqual(insufficient, Find<Label>(view, "space-elevator-status").ClassListContains(LackHighlightText.InsufficientClassName));
        }

        /// <web-test file="src/features/blockInventory/details/spaceElevator/__tests__/SpaceElevatorSection.test.tsx">詳細未着は取得待ちを表示し接続操作を作らない</web-test>
        [Test]
        public void 詳細の未着は取得待ちを出し接続ボタンを出さない()
        {
            var view = Mount(new SpaceElevatorBodyView(Kit(null)));
            view.Render(Slots(true), SpecialBlockDetailViewModel.Empty);
            Assert.AreEqual(new UiText(LocalizationKeys.Ui.BlockInventory.DetailLoading).Resolve(), TextOf(view, "space-elevator-loading"));
            Assert.IsFalse(IsShown(Find<Button>(view, "space-elevator-connect")));
        }

        /// <web-test file="e2e/tests/block/spaceElevator/spaceElevator.spec.ts">接続ボタンでactionを送り、接続後は進みを出す</web-test>
        [Test]
        public void 接続ボタンで送り接続後は進みを出す()
        {
            var view = Render(Slots(false), Unconnected(SpaceElevatorBaseStatus.ReadyToConnect));
            Assert.IsTrue(IsShown(Find<VisualElement>(view, "space-elevator-section")));
            StringAssert.Contains(new UiText(LocalizationKeys.Ui.BlockInventory.SpaceElevatorSatelliteInput).Resolve(), AllText(Find<VisualElement>(view, "space-elevator-grid")));
            StringAssert.Contains(new UiText(LocalizationKeys.Ui.BlockInventory.SpaceElevatorResultOutput).Resolve(), AllText(Find<VisualElement>(view, "space-elevator-grid")));
            StringAssert.Contains("4000", TextOf(view, "electric-network-summary"));
            StringAssert.Contains("2000", TextOf(view, "electric-network-summary"));
            StringAssert.Contains("1000", TextOf(view, "space-elevator-pool"));

            simulate.Click(Find<Button>(view, "space-elevator-connect"));
            CollectionAssert.AreEqual(new[] { "space_elevator.connect" }, Commands.Issued);
            view.Render(Slots(false), SpecialDetailSamples.ElevatorWithNetwork(SpecialDetailSamples.SpaceElevator(SpaceElevatorBaseStatus.Climbing, true, 1000, 0f)));
            Assert.IsTrue(IsShown(Find<VisualElement>(view, "space-elevator-progress")));
            Assert.IsFalse(IsShown(Find<Button>(view, "space-elevator-connect")));
        }

        /// <web-test file="e2e/tests/block/spaceElevator/spaceElevator.spec.ts">${fixture} の状態と進捗を描画する</web-test>
        [TestCase(SpaceElevatorBaseStatus.InsufficientCable, false, -1f)]
        [TestCase(SpaceElevatorBaseStatus.Climbing, true, 0.5f)]
        [TestCase(SpaceElevatorBaseStatus.OutputBlocked, true, 1f)]
        public void 不足と昇降と出口待ちの状態と進捗を描く(SpaceElevatorBaseStatus status, bool connected, float progress)
        {
            var view = Render(Slots(false), SpecialDetailSamples.SpaceElevator(status, connected, 1000, connected ? progress : 0f));
            Assert.IsTrue(IsShown(Find<VisualElement>(view, "space-elevator-view")));
            StringAssert.Contains("100%", TextOf(view, "electric-network-summary"));
            Assert.AreEqual(new UiText(SpaceElevatorLogic.StatusDisplay(status).LabelKey).Resolve(), TextOf(view, "space-elevator-status"));
            Assert.AreEqual(connected, IsShown(Find<VisualElement>(view, "space-elevator-progress")));
            Assert.AreEqual(!connected, IsShown(Find<Button>(view, "space-elevator-connect")));
            if (connected) Assert.AreEqual(progress, GaugeValue(view, "space-elevator-progress"), 0.001f);
            else Assert.IsFalse(Find<Button>(view, "space-elevator-connect").enabledSelf);
        }

        /// <web-test file="e2e/tests/block/spaceElevator/spaceElevator.spec.ts">入口への投入は元slot0を送り出口を変えない</web-test>
        [Test]
        public void 入口の押下は元の添字0で届く()
        {
            var view = Render(Slots(false), Unconnected(SpaceElevatorBaseStatus.ReadyToConnect));
            simulate.Click(Find<VisualElement>(view, "space-elevator-input-grid-slot-0"));
            CollectionAssert.AreEqual(new[] { "LeftDown 0" }, BlockSlots.Issued);
        }

        /// <web-test file="e2e/tests/block/spaceElevator/spaceElevator.spec.ts">出口${outputIndex + 1}の回収は元slot${outputIndex + 1}を送る</web-test>
        [TestCase(0)]
        [TestCase(1)]
        public void 出口の押下は元の添字で届く(int outputIndex)
        {
            var view = Render(Slots(false), Unconnected(SpaceElevatorBaseStatus.ReadyToConnect));
            simulate.Click(Find<VisualElement>(view, $"space-elevator-output-grid-slot-{outputIndex + 1}"));
            CollectionAssert.AreEqual(new[] { $"LeftDown {outputIndex + 1}" }, BlockSlots.Issued);
        }

        /// <web-test file="e2e/tests/block/spaceElevator/spaceElevator.spec.ts">詳細の取得待ちから接続可能へ遷移する</web-test>
        [Test]
        public void 取得待ちから接続可能へ移る()
        {
            var view = Mount(new SpaceElevatorBodyView(Kit(LoadContent())));
            view.Render(Slots(false), SpecialBlockDetailViewModel.Empty);
            Assert.IsTrue(IsShown(Find<Label>(view, "space-elevator-loading")));
            view.Render(Slots(false), Unconnected(SpaceElevatorBaseStatus.ReadyToConnect));
            Assert.IsFalse(IsShown(Find<Label>(view, "space-elevator-loading")));
            Assert.IsTrue(Find<Button>(view, "space-elevator-connect").enabledSelf);
        }

        /// <web-test file="e2e/tests/block/spaceElevator/spaceElevator.spec.ts">ホストが並べ替えた入口と出口の添字を描画と操作に使う</web-test>
        [Test]
        public void 並べ替えた入口と出口の添字を描画と押下に使う()
        {
            var reordered = new SpaceElevatorDetail(new[] { 2 }, new[] { 1, 0 }, false, 1000, 1000, 0f, SpaceElevatorBaseStatus.ReadyToConnect);
            var view = Render(Slots(false), SpecialDetailSamples.ElevatorWithNetwork(reordered));
            Assert.AreEqual(new[] { "space-elevator-input-grid-slot-2" }, FrameIds(view, "space-elevator-input-grid"));
            Assert.AreEqual(new[] { "space-elevator-output-grid-slot-1", "space-elevator-output-grid-slot-0" }, FrameIds(view, "space-elevator-output-grid"));
            simulate.Click(Find<VisualElement>(view, "space-elevator-output-grid-slot-0"));
            CollectionAssert.AreEqual(new[] { "LeftDown 0" }, BlockSlots.Issued);
        }

        private SpaceElevatorBodyView Render(SlotItem[] slots, SpaceElevatorDetail detail)
        {
            return Render(slots, SpecialDetailSamples.ElevatorWithNetwork(detail));
        }

        private SpaceElevatorBodyView Render(SlotItem[] slots, SpecialBlockDetailViewModel detail)
        {
            var view = Mount(new SpaceElevatorBodyView(Kit(LoadContent())));
            view.Render(slots, detail);
            return view;
        }

        private static SpaceElevatorDetail Unconnected(SpaceElevatorBaseStatus status)
        {
            return SpecialDetailSamples.SpaceElevator(status, false, 1000, 0f);
        }

        private static SlotItem[] Slots(bool empty)
        {
            return empty
                ? new[] { new SlotItem(ItemMaster.EmptyItemId, 0), new SlotItem(ItemMaster.EmptyItemId, 0), new SlotItem(ItemMaster.EmptyItemId, 0) }
                : new[] { new SlotItem(ItemMaster.EmptyItemId, 0), new SlotItem(new ItemId(1), 4), new SlotItem(new ItemId(2), 7) };
        }

        // 格子の中の枠の testid を並び順で
        // Frame testids inside a grid, in display order
        private static string[] FrameIds(VisualElement root, string gridTestid)
        {
            return Find<VisualElement>(root, gridTestid).Query<VisualElement>().Where(e => e.name != null && e.name.StartsWith(gridTestid + "-slot-")).ToList().Select(e => e.name).ToArray();
        }

        private static string AllText(VisualElement root)
        {
            return string.Join(" ", root.Query<Label>().ToList().Select(l => l.text));
        }
    }
}
```
（`SpaceElevatorBaseStatus` の値の名前は `Game.Block.Interface.State` の実物に合わせる。e2e の 3・4・6 件目は押下 → 絶対添字までを見る。持ち物の移動の結果・掴み絵は alpha の `BlockSlotCommandsTest` と録画 R-12 の宇宙エレベーターの段が見る〈`done.md` の「## 部分移植の残り」〉）

- [ ] **Step 2: 実装を書く**

`Space/RocketLaunchPadBodyView.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.Components.Gauges;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Space
{
    /// <summary>
    /// 発射台の本文: 資材・積荷の群、状態文、進捗、出力の群（サーバーの絶対添字のまま）。詳細の未着は取得待ち（Web RocketLaunchPadInventory）
    /// Launch pad body: material and payload groups, status, progress, output group (server absolute indices); pending detail shows loading (Web RocketLaunchPadInventory)
    /// </summary>
    public sealed class RocketLaunchPadBodyView : VisualElement, ISpecialDetailRenderer
    {
        private readonly LocalizedLabel _loading = SpecialViewParts.Label("rocket-launch-pad-loading", UiTypographyClass.TextSm, UiTypographyClass.TextMuted);
        private readonly UiStack _view = new(UiGap.Sm) { name = "rocket-launch-pad-view" };
        private readonly BlockItemGrid _material;
        private readonly BlockItemGrid _payload;
        private readonly BlockItemGrid _output;
        private readonly LocalizedLabel _status = SpecialViewParts.Label("rocket-launch-pad-status", UiTypographyClass.TextSm, "rocket-launch-pad-view__status");
        private readonly GaugeBar _progress = new("rocket-launch-pad-progress");

        public RocketLaunchPadBodyView(SpecialViewKit kit)
        {
            AddToClassList("rocket-launch-pad-view");
            _loading.SetText(new UiText(LocalizationKeys.Ui.BlockInventory.DetailLoading));
            _material = kit.Grids.Create("rocket-launch-pad-material-slots");
            _payload = kit.Grids.Create("rocket-launch-pad-payload-slots");
            _output = kit.Grids.Create("rocket-launch-pad-output-slots");

            // Web と同じ並び: 資材・積荷・状態・進捗・出力
            // Same order as the Web: materials, payloads, status, progress, outputs
            _view.AddItem(new BlockItemGridGroup(LocalizationKeys.Ui.BlockInventory.RocketMaterials, _material));
            _view.AddItem(new BlockItemGridGroup(LocalizationKeys.Ui.BlockInventory.RocketPayloads, _payload));
            _view.AddItem(_status);
            _view.AddItem(_progress);
            _view.AddItem(new BlockItemGridGroup(LocalizationKeys.Ui.BlockInventory.RocketOutputs, _output));
            Add(_loading);
            Add(_view);
        }

        public bool HasDetail(SpecialBlockDetailViewModel detail) => true;

        public void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)
        {
            var present = detail.TryGetRocketLaunchPad(out var rocket);
            SpecialViewParts.SetShown(_loading, !present);
            SpecialViewParts.SetShown(_view, present);
            if (!present) return;

            _material.Render(itemSlots, rocket.MaterialSlots);
            _payload.Render(itemSlots, rocket.PayloadSlots);
            _output.Render(itemSlots, rocket.OutputSlots(itemSlots.Count));
            _status.SetText(new UiText(rocket.StatusKey));
            _progress.SetValue(rocket.Progress);
        }
    }
}
```
（`RocketLaunchPadDetail` のサーバーの列挙は internal で、View は `StatusKey`・`TryGetCommandButton` だけを読む〈D-27〉。状態文の色 `--text-high-contrast` は `UiTypographyClass` に無いので、画面 USS の `.rocket-launch-pad-view__status`〈Task 9〉で当てる）

`Space/RocketLaunchPadTitleActionView.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.Components.Controls.PanelActionButton;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Space
{
    /// <summary>
    /// 発射台の題の操作。合図待ちの段階だけ建造/発射のボタンを 1 つ出す（Web RocketLaunchPadCommandAction）
    /// Launch pad title action; shows the single build or launch button only while waiting for a command (Web RocketLaunchPadCommandAction)
    /// </summary>
    public sealed class RocketLaunchPadTitleActionView : VisualElement, ISpecialDetailRenderer
    {
        public const string CommandName = "rocket_launch_pad.command";

        private readonly SpecialViewKit _kit;
        private readonly Dictionary<RocketCommand, PanelActionButton> _buttons = new();

        public RocketLaunchPadTitleActionView(SpecialViewKit kit)
        {
            _kit = kit;
            name = "rocket-launch-pad-title-action";
        }

        public bool HasDetail(SpecialBlockDetailViewModel detail) => true;

        public void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)
        {
            // 合図待ちでなければ題の操作ごと隠す（Web は null。手動の操作を持たない段階）。根は alpha の題の操作の器の唯一の子で UiGapContainer の項目ではないので自分で隠してよい（区画とは違う）
            // Hide the whole action outside command waits (Web returns null); the root is the only child of alpha's title-action holder, not a UiGapContainer item, so it may hide itself (unlike sections)
            var hasButton = detail.TryGetRocketLaunchPad(out var rocket) && rocket.TryGetCommandButton(out var button) ? Show(button) : HideAll();
            SpecialViewParts.SetShown(this, hasButton);

            #region Internal

            bool Show(RocketCommandButton shown)
            {
                if (!_buttons.ContainsKey(shown.Command)) _buttons[shown.Command] = CreateButton(shown);
                foreach (var pair in _buttons) SpecialViewParts.SetShown(pair.Value, pair.Key == shown.Command);
                return true;
            }

            bool HideAll()
            {
                foreach (var pair in _buttons) SpecialViewParts.SetShown(pair.Value, false);
                return false;
            }

            PanelActionButton CreateButton(RocketCommandButton source)
            {
                var created = new PanelActionButton($"rocket-launch-pad-{source.Command.ToString().ToLowerInvariant()}", source.LabelKey);
                created.OnClicked.Subscribe(_ => _kit.Run(CommandName, _kit.Commands.SendRocketCommand(source.Command)));
                Add(created);
                return created;
            }

            #endregion
        }
    }
}
```
（`TryGetCommandButton` の out 変数を三項の中で使う形が読みにくければ if 文 2 つに開いてよい。ボタンは段階が変わっても作り直さない〈押下の途中で消えない〉）

`Space/SpaceElevatorSectionView.cs`:
```csharp
using Client.Ui.Components.Controls.PanelActionButton;
using Client.Ui.Components.Gauges;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Text;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Space
{
    /// <summary>
    /// 宇宙エレベーターの状態区画: 状態文（不足トーン）・プール量・接続済みなら昇降の進み、未接続なら接続ボタン（接続可能だけ押せる）（Web SpaceElevatorSection）
    /// Space elevator state section: status with shortage tone, pool amount, climb progress when connected, else a connect button enabled only when ready (Web SpaceElevatorSection)
    /// </summary>
    public sealed class SpaceElevatorSectionView : VisualElement
    {
        public const string CommandName = "space_elevator.connect";

        private readonly LocalizedLabel _loading = SpecialViewParts.Label("space-elevator-loading", UiTypographyClass.TextSm, UiTypographyClass.TextMuted);
        private readonly UiStack _section = new(UiGap.Px4) { name = "space-elevator-section" };
        private readonly LackHighlightText _status = new("space-elevator-status", LackHighlightTextSize.Sm);
        private readonly LocalizedLabel _pool = SpecialViewParts.Label("space-elevator-pool", UiTypographyClass.TextSm, UiTypographyClass.TextDefault);
        private readonly GaugeBar _progress = new("space-elevator-progress");
        private readonly PanelActionButton _connect = new("space-elevator-connect", LocalizationKeys.Ui.BlockInventory.SpaceElevatorConnect);

        public SpaceElevatorSectionView(SpecialViewKit kit)
        {
            _loading.SetText(new UiText(LocalizationKeys.Ui.BlockInventory.DetailLoading));
            _connect.OnClicked.Subscribe(_ => kit.Run(CommandName, kit.Commands.ConnectSpaceElevator()));
            _section.AddItem(_status);
            _section.AddItem(_pool);
            _section.AddItem(_progress);
            _section.AddItem(_connect);
            Add(_loading);
            Add(_section);
        }

        public void Render(SpecialBlockDetailViewModel detail)
        {
            // 詳細の未着は取得待ちだけを出し、接続の操作を出さない
            // Pending detail shows only the loading line, without the connect operation
            var present = detail.TryGetSpaceElevator(out var elevator);
            SpecialViewParts.SetShown(_loading, !present);
            SpecialViewParts.SetShown(_section, present);
            if (!present) return;

            var display = elevator.StatusDisplay;
            _status.Render(new UiText(display.LabelKey), display.Insufficient);
            _pool.SetText(SpecialDetailTexts.SpaceElevatorPool(elevator));
            _section.SetItemShown(_progress, elevator.Connected);
            _section.SetItemShown(_connect, !elevator.Connected);
            _progress.SetValue(elevator.ClimbProgress);
            _connect.SetEnabled(elevator.CanConnect);
        }
    }
}
```

`Space/SpaceElevatorBodyView.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.Foundation.Layout;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Special.Network;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Space
{
    /// <summary>
    /// 宇宙エレベーターの本文: 入口・出口の群（サーバーの絶対添字・ホストの並びのまま）、状態区画、電力網の区画（Web SpaceElevatorInventory）
    /// Space elevator body: inlet and outlet groups (server absolute indices in host order), state section, electric network section (Web SpaceElevatorInventory)
    /// </summary>
    public sealed class SpaceElevatorBodyView : VisualElement, ISpecialDetailRenderer
    {
        private readonly UiStack _grid = new(UiGap.Sm) { name = "space-elevator-grid" };
        private readonly BlockItemGridGroup _inputGroup;
        private readonly BlockItemGridGroup _outputGroup;
        private readonly BlockItemGrid _input;
        private readonly BlockItemGrid _output;
        private readonly SpaceElevatorSectionView _section;
        private readonly ElectricNetworkSectionView _network = new();
        private readonly UiStack _stack = new(UiGap.Sm);

        public SpaceElevatorBodyView(SpecialViewKit kit)
        {
            name = "space-elevator-view";
            AddToClassList("space-elevator-view");
            _input = kit.Grids.Create("space-elevator-input-grid");
            _output = kit.Grids.Create("space-elevator-output-grid");
            _inputGroup = new BlockItemGridGroup(LocalizationKeys.Ui.BlockInventory.SpaceElevatorSatelliteInput, _input);
            _outputGroup = new BlockItemGridGroup(LocalizationKeys.Ui.BlockInventory.SpaceElevatorResultOutput, _output);
            _grid.AddItem(_inputGroup);
            _grid.AddItem(_outputGroup);
            _section = new SpaceElevatorSectionView(kit);

            _stack.AddItem(_grid);
            _stack.AddItem(_section);
            _stack.AddItem(_network);
            Add(_stack);
        }

        public bool HasDetail(SpecialBlockDetailViewModel detail) => true;

        public void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)
        {
            // 群は詳細が届いてから出す（Web は詳細無しで群を空にする）
            // Groups appear once the detail arrives (the Web leaves them empty without detail)
            var present = detail.TryGetSpaceElevator(out var elevator);
            _grid.SetItemShown(_inputGroup, present);
            _grid.SetItemShown(_outputGroup, present);
            if (present)
            {
                _input.Render(itemSlots, elevator.SatelliteInputSlots);
                _output.Render(itemSlots, elevator.OutputSlots);
            }
            _section.Render(detail);

            // 電力網の区画は Stack の項目なので SetItemShown で出し入れする（直に display を触ると先頭の間隔がずれる。D-23）
            // The network section is a Stack item, so toggle it with SetItemShown (touching display directly skews the leading gap. D-23)
            var hasNetwork = _network.HasDetail(detail);
            _stack.SetItemShown(_network, hasNetwork);
            if (hasNetwork) _network.Render(itemSlots, detail);
        }
    }
}
```
（`PanelActionButton` の namespace は実物〈`Client.Ui/Components/Controls/PanelActionButton/PanelActionButton.cs`〉に合わせる。`UiStack` の name を初期化子で付けるのは alpha の `SectionStackBody` と同じ書き方）

- [ ] **Step 3: コンパイルしテストを実行して通ることを確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.Screens\.BlockInventory\.Special\.(RocketLaunchPadViewTest|SpaceElevatorViewTest)\."`
Expected: ErrorCount 0 / PASS（Rocket 6〈TestCase 込み。Web 7 ケースのうち捨てる 1 を除く〉・SpaceElevator 22〈TestCase 込み。Inventory 2・Section 11・spec 9〉）

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Special
git commit -m "feat(uitk): 発射台の本文と題の操作・宇宙エレベーターの本文と状態区画（S4-special）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 9: 登録・工場・画面 USS・共有区画の 2 行

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Registration/SpecialBodyFactories.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Registration/SpecialSectionFactories.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/BlockInventorySpecial.uss`
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Registration/GameUi/Screens/BlockInventorySpecialGameUiRegistration.cs`（W4 以降の画面の登録の置き場。E-1 の追記 ad92a44777。再編後は再編先。D-20）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiRegistration.cs`（画面登録区画に 1 行）
- Modify: `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`（`Screens` 区画に `@import` 1 行）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/BlockInventoryDesignTest.cs`（alpha の色の件に `TestCase` を 1 行。alpha の Produces 一覧「登録・共有区画」の指示）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Special/SpecialFactoriesTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Special/SectionStack/SpecialSectionStackSpacingTest.cs`（統合 L1。D-23）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Special/SectionStack/SpecialBlockDetailsTest.cs`（`blockDetails`・`regression/sectionStack` の持ち分）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Special/Support/SpecialScreenTestHost.cs`（`SectionFactories()`）・`SpecialDetailSamples.cs`（歯車機械・歯車発電機・回転発電機の見本）

**Interfaces:**
- Consumes: Task 3〜8 の全型、alpha の `IBlockInventoryBodyFactory`・`IBlockInventoryTitleActionFactory`・`ISectionStackSectionFactory`・`IBlockSlotRestrictionContributor`・`BlockInventoryBodyMount`・`SectionStackSectionMount`・L1 の土台 `BlockInventoryTestBodies.SectionStack`、既存の DI 登録（`OpenBlockDetailTracker`・`BlockNetworkInfoCache`・`BeltTransportClientCache`・`PlayerGrabModel`・`GameContentPresentation`・`UiCommandResultToaster`）と alpha の共有サービス（`IBlockInventoryPanelSource`・`BlockItemGridFactory`。`MainGameUiRegistration` の共有サービスの並び）
- Produces:
```csharp
namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    // 本文: FilterSplitterBodyFactory・ElectricToGearBodyFactory・ElectricPoleBodyFactory・SpaceElevatorBodyFactory・RocketLaunchPadBodyFactory（: IBlockInventoryBodyFactory。ctor(SpecialViewKit kit, SpecialPresenters presenters)）
    // 題の操作: RocketLaunchPadTitleActionFactory（: IBlockInventoryTitleActionFactory。ctor(SpecialViewKit kit, SpecialPresenters presenters)）
    // 区画: GearToElectricSectionFactory・BeltThroughputSectionFactory・GearSectionFactory・ElectricNetworkSectionFactory・GearNetworkSectionFactory（: ISectionStackSectionFactory。ctor(SpecialPresenters presenters)）
}
namespace Client.Starter.Registration.GameUi.Screens { internal static class BlockInventorySpecialGameUiRegistration { public static void Register(ContainerBuilder builder); } }
```

- [ ] **Step 1: テストを書く**

`SpecialFactoriesTest.cs`:
```csharp
using System.Linq;
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Logic.Panel;
using NUnit.Framework;

namespace Client.Tests.Ui.Screens.BlockInventory.Special
{
    /// <summary>
    /// 工場の Kind・Slot が持ち分どおりで重ならず、作った部分の根が Web の testid を持つ
    /// Factory kinds and slots match this lane's share without overlap, and each created part's root carries the Web testid
    /// </summary>
    public class SpecialFactoriesTest : SpecialScreenTestHost
    {
        [Test]
        public void 本文5つと題の操作1つと区画5つを持ち分どおりに作る()
        {
            var kit = Kit(null);
            var bodies = new IBlockInventoryBodyFactory[] { new FilterSplitterBodyFactory(kit, Presenters), new ElectricToGearBodyFactory(kit, Presenters), new ElectricPoleBodyFactory(kit, Presenters), new SpaceElevatorBodyFactory(kit, Presenters), new RocketLaunchPadBodyFactory(kit, Presenters) };
            CollectionAssert.AreEquivalent(new[] { BlockInventoryBodyKind.FilterSplitter, BlockInventoryBodyKind.ElectricToGear, BlockInventoryBodyKind.ElectricPole, BlockInventoryBodyKind.SpaceElevator, BlockInventoryBodyKind.RocketLaunchPad }, bodies.Select(b => b.Kind));
            CollectionAssert.AreEquivalent(new[] { "filter-splitter", "electric-to-gear-view", "electric-pole-view", "space-elevator-view", null }, bodies.Select(b => b.Create(new BlockInventoryBodyMount(false)).Root.name));

            Assert.AreEqual(BlockInventoryTitleActionKind.RocketLaunchPadCommand, new RocketLaunchPadTitleActionFactory(kit, Presenters).Kind);

            var sections = SectionFactories();
            CollectionAssert.AreEquivalent(new[] { SectionStackSlot.GearToElectric, SectionStackSlot.BeltThroughput, SectionStackSlot.Gear, SectionStackSlot.ElectricNetwork, SectionStackSlot.GearNetwork }, sections.Select(s => s.Slot));
            CollectionAssert.AreEquivalent(new[] { "gear-to-electric-section", "belt-throughput-section", "gear-section", "electric-network-section", "gear-network-section" }, sections.Select(s => s.Create(new SectionStackSectionMount(null, false)).Root.name));
        }
    }
}
```
（`BlockInventoryBodyMount`・`SectionStackSectionMount` の ctor が internal なら alpha の土台の組み方に合わせる。発射台の本文は根に name を持たない〈`rocket-launch-pad-loading`/`-view` が子〉ので null）

`SectionStack/SpecialSectionStackSpacingTest.cs`（統合 L1。alpha の標準の本文〈`SectionStackBody`〉に本物の区画の工場 5 つを差す。手本は alpha の `SectionStackViewTest`「区画の出し入れはパネルの描き直し無しで変わり先頭の間隔が詰まる」。D-23）:
```csharp
using System.Linq;
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Special.Details;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special.SectionStack
{
    /// <summary>
    /// 先頭の区画がパネルの組み直しの外で後から出る・消えるとき、後続の区画の余白まで Web の Stack gap="sm" と同じに組み直される
    /// When the leading section appears or disappears outside panel rebuilds, the following sections' gaps are re-laid like the Web Stack gap="sm"
    /// </summary>
    public class SpecialSectionStackSpacingTest : SpecialScreenTestHost
    {
        private static readonly string[] SpecialSectionNames = { "gear-to-electric-section", "belt-throughput-section", "gear-section", "electric-network-section", "gear-network-section" };

        [Test]
        public void 先頭の区画が後から届くと後続の区画に余白が付き消えると詰まる()
        {
            // 歯車と歯車網だけの詳細で開く（回転発電機の状態は未着）
            // Open with only gear and gear-network details (the generator state has not arrived)
            var root = MountStack("GearToElectricGenerator", WithGearToElectric(null));
            AssertLeadingGap(root, "gear-section", "gear-network-section");

            // 回転発電機の状態が組み直しの外で届く → 先頭が入れ替わり、歯車の区画に余白が付く
            // The generator state arrives outside a rebuild; the leader changes and the gear section gains a gap
            Model.Set(WithGearToElectric(SpecialDetailSamples.GearToElectricGenerating));
            simulate.FrameUpdate();
            AssertLeadingGap(root, "gear-to-electric-section", "gear-section", "gear-network-section");

            // 先頭が消える → 歯車の区画が先頭に戻り、余白が 0 に詰まる
            // The leader disappears; the gear section leads again and its gap collapses to 0
            Model.Set(WithGearToElectric(null));
            simulate.FrameUpdate();
            AssertLeadingGap(root, "gear-section", "gear-network-section");
        }

        [Test]
        public void 先頭の歯車の区画が消えると歯車網が先頭で余白0になり網が消えても先頭は変わらない()
        {
            var root = MountStack("GearMachine", SpecialDetailSamples.GearMachine);
            AssertLeadingGap(root, "gear-section", "gear-network-section");

            // 先頭が消える → 歯車網が先頭になり余白 0
            // The leader vanishes; the gear network leads with a zero gap
            Model.Set(SpecialDetailSamples.Only(SpecialDetailSamples.GearMachineNetwork));
            simulate.FrameUpdate();
            AssertLeadingGap(root, "gear-network-section");

            // 網の集約が消える（末尾の消失）→ 歯車の区画だけが余白 0 で残る
            // The network aggregate vanishes (tail removal); only the gear section remains with a zero gap
            Model.Set(SpecialDetailSamples.Only(SpecialDetailSamples.GearMachineGear));
            simulate.FrameUpdate();
            AssertLeadingGap(root, "gear-section");
        }

        private VisualElement MountStack(string blockType, SpecialBlockDetailViewModel detail)
        {
            Model.Set(detail);
            var panel = OpenPanel(blockType);
            var body = BlockInventoryTestBodies.SectionStack(Panels, blockType, SectionFactories());
            Mount(body.Root);
            body.Attach(panel);
            simulate.FrameUpdate();
            return body.Root;
        }

        private static SpecialBlockDetailViewModel WithGearToElectric(GearToElectricDetail generator)
        {
            return new SpecialBlockDetailViewModel(null, generator, GearDetail.Generator(20, 5), null, null, new GearNetworkDetail(20, 30, GearNetworkStopReasonKind.None), null, null, null);
        }

        // 表示中の特殊族の区画が期待の順に並び、先頭だけ余白 0・他は正（UiGap.Sm の実値）
        // Shown special sections appear in the expected order; only the leader has a 0 gap, the others positive (the UiGap.Sm value)
        private static void AssertLeadingGap(VisualElement root, params string[] expected)
        {
            var shown = root.Children().Where(e => e.resolvedStyle.display == DisplayStyle.Flex).ToList();
            CollectionAssert.AreEqual(expected, shown.Select(e => e.name).Where(SpecialSectionNames.Contains).ToArray());
            Assert.AreEqual(0f, shown[0].resolvedStyle.marginTop, "表示中の先頭の区画は余白 0");
            foreach (var following in shown.Skip(1)) Assert.Less(0f, following.resolvedStyle.marginTop, $"{following.name} に区画の間の余白がある");
        }
    }
}
```
（`BlockInventoryTestBodies.SectionStack` の戻りは `var` で受け、根だけを返す〈`.Root`・`.Attach(panel)` は alpha の手本どおり〉。GearToElectricGenerator・GearMachine の標準の本文に alpha の ItemGrid 区画や S4-machine の区画が出る場合も、`shown[0]` は全区画の中の先頭なので、余白の確かめはそれを含めた並びで行い、特殊族の区画の順だけを `SpecialSectionNames` で絞って比べる。先頭が特殊族でない blockType では 1 件目の `AreEqual(0f, …)` が alpha 側の区画を見ることになるので、両件とも ItemGrid が出ない blockType〈`SectionStackConfigTable.Resolve` が `Hidden`〉であることを Task 1 Step 2 の照合のときに確かめる。出るなら `SpecialDetailSamples` の名前の区画だけで先頭を判定する形に直す）

`SectionStack/SpecialBlockDetailsTest.cs`（S4-machine と分け合う `blockDetails.spec.ts` の歯車とフィルタの面、alpha の持ち主表で回ってきた `regression/sectionStack.spec.ts` の filterSplitter）:
```csharp
using System.Linq;
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.State.BlockInventory.Special.Details;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special.SectionStack
{
    /// <summary>
    /// ブロック詳細の歯車とフィルタの面（Web blockDetails.spec・regression/sectionStack.spec。S4-machine が同じタグで機械・採掘機・ポンプ・発電機の面を写す）
    /// Gear and filter facets of the block details (Web blockDetails.spec, regression/sectionStack.spec; S4-machine ports the machine, miner, pump and generator facets under the same tags)
    /// </summary>
    public class SpecialBlockDetailsTest : SpecialScreenTestHost
    {
        private static readonly string[] SpecialSectionIds = { "gear-to-electric-section", "belt-throughput-section", "gear-section", "electric-network-section", "gear-network-section" };

        private static readonly string[] WebSectionIds =
        {
            "machine-section", "miner-section", "generator-section", "gear-section", "electric-network-section", "gear-network-section",
            "chest-grid", "machine-fluid-slots", "miner-output-grid", "gear-miner-output-grid", "generator-fuel-grid",
            "generic-block-grid", "generic-block-fluids", "filter-splitter", "pump-section", "pump-fluid-slots",
        };

        /// <web-test file="e2e/tests/block/blockDetails.spec.ts">renders ${type} detail section</web-test>
        [Test]
        public void 歯車機械の標準の本文に歯車の区画が出る()
        {
            var root = MountStack(BlockMasterElement.BlockTypeConst.GearMachine, SpecialDetailSamples.GearMachine);
            Assert.IsTrue(IsShown(Find<VisualElement>(root, "gear-section")));
        }

        /// <web-test file="e2e/tests/block/blockDetails.spec.ts">renders ${type} detail section</web-test>
        [Test]
        public void フィルタ分配器の専用の本文が出る()
        {
            var root = MountFilterSplitter();
            Assert.IsTrue(IsShown(Find<VisualElement>(root, "filter-splitter")));
        }

        /// <web-test file="e2e/tests/block/blockDetails.spec.ts">歯車機械は消費トルクと基準付きRPMを赤なしで出し、網の需給行を持つ</web-test>
        [Test]
        public void 歯車機械は消費トルクと基準付きRPMを不足色なしで出し歯車網の区画を持つ()
        {
            var root = MountStack(BlockMasterElement.BlockTypeConst.GearMachine, SpecialDetailSamples.GearMachine);
            var rows = GearRowDisplay.From(SpecialDetailSamples.GearMachineGear);
            Assert.AreEqual(Resolved(rows.Torque), TextOf(root, "gear-torque"));
            Assert.AreEqual(Resolved(rows.Rpm), TextOf(root, "gear-rpm"));
            StringAssert.Contains("3.0", TextOf(root, "gear-torque"));
            StringAssert.Contains("12.5 / 20.0", TextOf(root, "gear-rpm"));
            AssertNotInsufficient(root, "gear-torque", "gear-rpm");
            Assert.IsTrue(IsShown(Find<VisualElement>(root, "gear-network-section")));
        }

        /// <web-test file="e2e/tests/block/blockDetails.spec.ts">歯車発電機は発生トルクと現在RPMだけを出す</web-test>
        [Test]
        public void 歯車発電機は発生トルクと現在RPMだけを不足色なしで出す()
        {
            var root = MountStack(BlockMasterElement.BlockTypeConst.SimpleGearGenerator, SpecialDetailSamples.GearGenerator);
            StringAssert.Contains("5.0", TextOf(root, "gear-torque"));
            StringAssert.Contains("20.0", TextOf(root, "gear-rpm"));
            StringAssert.DoesNotContain("/", TextOf(root, "gear-rpm"));
            AssertNotInsufficient(root, "gear-torque", "gear-rpm");
        }

        /// <web-test file="e2e/tests/block/blockDetails.spec.ts">歯車ポンプは電力行を持たず歯車行を出す</web-test>
        [Test]
        public void 歯車ポンプは歯車の区画を出し電力網の区画を出さない()
        {
            var root = MountStack(BlockMasterElement.BlockTypeConst.GearPump, SpecialDetailSamples.GearMachine);
            Assert.IsTrue(IsShown(Find<VisualElement>(root, "gear-section")));
        }

        /// <web-test file="e2e/tests/regression/sectionStack.spec.ts">${entry.type}は対応セクションだけを表示する</web-test>
        [TestCase("GearMachine", true)]
        [TestCase("GearMiner", true)]
        [TestCase("GearPump", true)]
        [TestCase("SimpleGearGenerator", true)]
        [TestCase("ElectricMachine", false)]
        [TestCase("ElectricMiner", false)]
        [TestCase("ElectricGenerator", false)]
        [TestCase("ElectricPump", false)]
        public void 歯車駆動は歯車と歯車網の区画だけ電力駆動は電力網の区画だけを特殊族の区画として出す(string blockType, bool gearDriven)
        {
            // Web の cases の shown のうち特殊族の区画の id だけを見る（他の id は alpha・S4-machine が同じタグで見る）
            // Check only the special section ids among the Web cases' shown list (alpha and S4-machine check the rest under the same tag)
            var root = MountStack(blockType, gearDriven ? SpecialDetailSamples.GearMachine : SpecialDetailSamples.Only(SpecialDetailSamples.PoleNetwork));
            var expected = gearDriven ? new[] { "gear-section", "gear-network-section" } : new[] { "electric-network-section" };
            var shownSpecial = SpecialSectionIds.Where(id => root.Q(id) is { } section && IsShown(section)).ToArray();
            CollectionAssert.AreEqual(expected, shownSpecial);
        }

        /// <web-test file="e2e/tests/regression/sectionStack.spec.ts">${entry.type}は対応セクションだけを表示する</web-test>
        [Test]
        public void フィルタ分配器は専用の本文だけを出し他の区画を持たない()
        {
            // 種別の表で専用の本文に振られ、標準の本文の区画を一つも作らない
            // The registry routes it to the dedicated body, which creates none of the standard sections
            Assert.AreEqual(BlockInventoryBodyKind.FilterSplitter, BlockComponentRegistry.Resolve(BlockMasterElement.BlockTypeConst.FilterSplitter).Body);
            var root = MountFilterSplitter();
            Assert.IsTrue(IsShown(Find<VisualElement>(root, "filter-splitter")));
            CollectionAssert.IsEmpty(WebSectionIds.Where(id => id != "filter-splitter" && root.Q(id) != null).ToArray());
        }

        private VisualElement MountStack(string blockType, SpecialBlockDetailViewModel detail)
        {
            Model.Set(detail);
            var panel = OpenPanel(blockType);
            var body = BlockInventoryTestBodies.SectionStack(Panels, blockType, SectionFactories());
            Mount(body.Root);
            body.Attach(panel);
            simulate.FrameUpdate();
            return body.Root;
        }

        private VisualElement MountFilterSplitter()
        {
            // 本物の本文の工場 5 つと区画の工場 5 つを alpha の本文のホストに登録し、パネル全体の根を返す（混入を見られるように）
            // Register the real 5 body and 5 section factories on alpha's body host and return the whole panel root, so a stray section would show up
            Model.Set(SpecialDetailSamples.Only(SpecialDetailSamples.FilterSplitter));
            var kit = Kit(LoadContent());
            var bodies = new IBlockInventoryBodyFactory[] { new FilterSplitterBodyFactory(kit, Presenters), new ElectricToGearBodyFactory(kit, Presenters), new ElectricPoleBodyFactory(kit, Presenters), new SpaceElevatorBodyFactory(kit, Presenters), new RocketLaunchPadBodyFactory(kit, Presenters) };
            var host = BlockInventoryTestBodies.BodyHost(Panels, bodies, SectionFactories());
            Mount(host.Root);
            OpenPanel(BlockMasterElement.BlockTypeConst.FilterSplitter);
            simulate.FrameUpdate();
            return host.Root;
        }

        // Web は文字色 rgb(238,238,238)（--text-default）で赤なしを見る。L1 は不足のクラスが無いことで見て、色の実値は並べ比べ（Task 10）で見る
        // The Web checks the text color rgb(238,238,238); L1 checks the absence of the shortage class, leaving the actual color to the side-by-side (Task 10)
        private static void AssertNotInsufficient(VisualElement root, params string[] testids)
        {
            foreach (var testid in testids) Assert.IsFalse(Find<VisualElement>(root, testid).ClassListContains("text--insufficient"), $"{testid} は不足色でない");
        }
    }
}
```
（`BlockInventoryTestBodies.BodyHost` は alpha の L1 の土台で「本文の工場と区画の工場を登録した本文のホスト〈`BlockInventoryBodyHost`〉を組み、パネル源の開閉に従って本文を差し替える物」の実物の名前に読み替える。部分 1 つだけを根にすると標準の区画の混入が構造上起こらず空振りするので、本文だけで組む形には戻さない。土台に本文のホストを組む手段が無ければ、filterSplitter の 2 つのケース〈`blockDetails` と `regression/sectionStack`〉のうち混入を見る `regression/sectionStack` の 1 ケースは書かず、`done.md` の「## 部分移植の残り」に「`regression/sectionStack` の filterSplitter: 本文のホストの土台が無い。録画 R-12 の分配器の段で他の区画が出ないことを見る」と書く〈D-24 と同じ扱い〉。`BlockTypeConst.GearMachine`・`GearPump`・`SimpleGearGenerator` は生成物の実物の名前に合わせる〈マスタの `blockType` は `GearMachine`・`GearPump` を確かめ済み〉。歯車ポンプの「電力行を持たない」の面〈`pump-power-rate` が無い〉は S4-machine が同じタグで写す。ここで見るのは歯車行〈`gear-section`〉だけ。歯車ポンプに電力網の詳細が付かないこと〈Reader が種別ごとに網の詳細を付けるか〉は L1 の偽の Model では見られないので、録画 R-12 の歯車の段と Web の橋との値の一致〈D-2〉で見る。区画の TestCase〈上の 8 種〉も同じく「詳細の有無 → 出る区画」の写しを固定するもので、種別ごとの詳細の付与は見ない。blockType の文字列〈`ElectricMachine`・`ElectricMiner`・`ElectricGenerator`・`ElectricPump` 等〉は生成物の `BlockTypeConst` の実物の名前に合わせる）

`Support/SpecialDetailSamples.cs` に足す（Web の `e2e/mock-host/fixtures/blocks/` の gearMachine・gearGenerator と同じ値）:
```csharp
        public static GearDetail GearMachineGear => GearDetail.Consumer(12.5, 3, 20);
        public static GearNetworkDetail GearMachineNetwork => new(20, 30, GearNetworkStopReasonKind.None);
        public static SpecialBlockDetailViewModel GearMachine => new(null, null, GearMachineGear, null, null, GearMachineNetwork, null, null, null);
        public static SpecialBlockDetailViewModel GearGenerator => new(null, null, GearDetail.Generator(20, 5), null, null, new GearNetworkDetail(0, 5, GearNetworkStopReasonKind.None), null, null, null);
        public static GearToElectricDetail GearToElectricGenerating => new(GearToElectricDisplayState.Generating, 120, 200);
```
（値は Web の fixture〈`energyFixtures.ts`・`machineFixtures.ts` 等。gearMachine の「消費トルク 3.0」「RPM 12.5 / 20.0」、gearGenerator の「発生トルク 5.0」「RPM 20.0」〉を開いて合わせる。`GearToElectricDetail` の ctor の並びは Task 2 の実物に合わせる。`Support/` は 5 本のまま）

`Support/SpecialScreenTestHost.cs` に足す（統合 L1 用。`using Client.Ui.Screens.BlockInventory.SectionStack;`）:
```csharp
        // 本物の区画の工場 5 つ（alpha の標準の本文に差す統合 L1 用）
        // The five real section factories (for integration L1 plugged into alpha's standard body)
        protected ISectionStackSectionFactory[] SectionFactories()
        {
            return new ISectionStackSectionFactory[] { new GearToElectricSectionFactory(Presenters), new BeltThroughputSectionFactory(Presenters), new GearSectionFactory(Presenters), new ElectricNetworkSectionFactory(Presenters), new GearNetworkSectionFactory(Presenters) };
        }
```


- [ ] **Step 2: 実装を書く**

`Registration/SpecialBodyFactories.cs`:
```csharp
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.Special.Energy;
using Client.Ui.Screens.BlockInventory.Special.FilterSplitter;
using Client.Ui.Screens.BlockInventory.Special.Network;
using Client.Ui.Screens.BlockInventory.Special.Space;
using Client.Ui.State.BlockInventory.Logic.Panel;

namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    // S4-alpha の拡張口 (1)・(2) へ差し込む特殊族の本文 5 つと題の操作 1 つ
    // The five special bodies and one title action plugged into S4-alpha's extension ports (1) and (2)
    public sealed class FilterSplitterBodyFactory : IBlockInventoryBodyFactory
    {
        private readonly SpecialViewKit _kit;
        private readonly SpecialPresenters _presenters;
        public FilterSplitterBodyFactory(SpecialViewKit kit, SpecialPresenters presenters) { _kit = kit; _presenters = presenters; }
        public BlockInventoryBodyKind Kind => BlockInventoryBodyKind.FilterSplitter;
        public IBlockInventoryPart Create(BlockInventoryBodyMount mount) => _presenters.Of(new FilterSplitterBodyView(_kit));
    }

    public sealed class ElectricToGearBodyFactory : IBlockInventoryBodyFactory
    {
        private readonly SpecialViewKit _kit;
        private readonly SpecialPresenters _presenters;
        public ElectricToGearBodyFactory(SpecialViewKit kit, SpecialPresenters presenters) { _kit = kit; _presenters = presenters; }
        public BlockInventoryBodyKind Kind => BlockInventoryBodyKind.ElectricToGear;
        public IBlockInventoryPart Create(BlockInventoryBodyMount mount) => _presenters.Of(new ElectricToGearBodyView(_kit));
    }

    public sealed class ElectricPoleBodyFactory : IBlockInventoryBodyFactory
    {
        private readonly SpecialViewKit _kit;
        private readonly SpecialPresenters _presenters;
        public ElectricPoleBodyFactory(SpecialViewKit kit, SpecialPresenters presenters) { _kit = kit; _presenters = presenters; }
        public BlockInventoryBodyKind Kind => BlockInventoryBodyKind.ElectricPole;
        public IBlockInventoryPart Create(BlockInventoryBodyMount mount) => _presenters.Of(new ElectricPoleBodyView());
    }

    public sealed class SpaceElevatorBodyFactory : IBlockInventoryBodyFactory
    {
        private readonly SpecialViewKit _kit;
        private readonly SpecialPresenters _presenters;
        public SpaceElevatorBodyFactory(SpecialViewKit kit, SpecialPresenters presenters) { _kit = kit; _presenters = presenters; }
        public BlockInventoryBodyKind Kind => BlockInventoryBodyKind.SpaceElevator;
        public IBlockInventoryPart Create(BlockInventoryBodyMount mount) => _presenters.Of(new SpaceElevatorBodyView(_kit));
    }

    public sealed class RocketLaunchPadBodyFactory : IBlockInventoryBodyFactory
    {
        private readonly SpecialViewKit _kit;
        private readonly SpecialPresenters _presenters;
        public RocketLaunchPadBodyFactory(SpecialViewKit kit, SpecialPresenters presenters) { _kit = kit; _presenters = presenters; }
        public BlockInventoryBodyKind Kind => BlockInventoryBodyKind.RocketLaunchPad;
        public IBlockInventoryPart Create(BlockInventoryBodyMount mount) => _presenters.Of(new RocketLaunchPadBodyView(_kit));
    }

    public sealed class RocketLaunchPadTitleActionFactory : IBlockInventoryTitleActionFactory
    {
        private readonly SpecialViewKit _kit;
        private readonly SpecialPresenters _presenters;
        public RocketLaunchPadTitleActionFactory(SpecialViewKit kit, SpecialPresenters presenters) { _kit = kit; _presenters = presenters; }
        public BlockInventoryTitleActionKind Kind => BlockInventoryTitleActionKind.RocketLaunchPadCommand;
        public IBlockInventoryPart Create() => _presenters.Of(new RocketLaunchPadTitleActionView(_kit));
    }
}
```
（どの本文も `mount.FillsPanelHeight` を使わない。特殊族は大型パネルにならない〈大型の判定は S4-machine の寄与だけ〉）

`Registration/SpecialSectionFactories.cs`:
```csharp
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.Screens.BlockInventory.Special.Belt;
using Client.Ui.Screens.BlockInventory.Special.Energy;
using Client.Ui.Screens.BlockInventory.Special.Network;

namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    // S4-alpha の拡張口 (3) の特殊族の区画 5 つ（Slot は alpha の SectionStackSlot。並びは alpha の列挙の順）
    // The five special sections for S4-alpha's extension port (3) (slots from alpha's SectionStackSlot; order follows alpha's enum)
    public sealed class GearToElectricSectionFactory : ISectionStackSectionFactory
    {
        private readonly SpecialPresenters _presenters;
        public GearToElectricSectionFactory(SpecialPresenters presenters) { _presenters = presenters; }
        public SectionStackSlot Slot => SectionStackSlot.GearToElectric;
        public IBlockInventorySection Create(SectionStackSectionMount mount) => _presenters.Of(new GearToElectricSectionView());
    }

    public sealed class BeltThroughputSectionFactory : ISectionStackSectionFactory
    {
        private readonly SpecialPresenters _presenters;
        public BeltThroughputSectionFactory(SpecialPresenters presenters) { _presenters = presenters; }
        public SectionStackSlot Slot => SectionStackSlot.BeltThroughput;
        public IBlockInventorySection Create(SectionStackSectionMount mount) => _presenters.Belt(new BeltThroughputSectionView());
    }

    public sealed class GearSectionFactory : ISectionStackSectionFactory
    {
        private readonly SpecialPresenters _presenters;
        public GearSectionFactory(SpecialPresenters presenters) { _presenters = presenters; }
        public SectionStackSlot Slot => SectionStackSlot.Gear;
        public IBlockInventorySection Create(SectionStackSectionMount mount) => _presenters.Of(new GearSectionView());
    }

    public sealed class ElectricNetworkSectionFactory : ISectionStackSectionFactory
    {
        private readonly SpecialPresenters _presenters;
        public ElectricNetworkSectionFactory(SpecialPresenters presenters) { _presenters = presenters; }
        public SectionStackSlot Slot => SectionStackSlot.ElectricNetwork;
        public IBlockInventorySection Create(SectionStackSectionMount mount) => _presenters.Of(new ElectricNetworkSectionView());
    }

    public sealed class GearNetworkSectionFactory : ISectionStackSectionFactory
    {
        private readonly SpecialPresenters _presenters;
        public GearNetworkSectionFactory(SpecialPresenters presenters) { _presenters = presenters; }
        public SectionStackSlot Slot => SectionStackSlot.GearNetwork;
        public IBlockInventorySection Create(SectionStackSectionMount mount) => _presenters.Of(new GearNetworkSectionView());
    }
}
```

`BlockInventorySpecial.uss`:
```css
/* web: features/blockInventory/views/RocketLaunchPadInventory.tsx の状態文 Text c="var(--text-high-contrast)" を写す。他の特殊族は Mantine の Stack/Group/Text の既定だけで局所上書きが無い / ports the launch pad status Text c="var(--text-high-contrast)"; the other special views use only Mantine Stack/Group/Text defaults with no local overrides */
.rocket-launch-pad-view__status {
    color: var(--text-high-contrast);
}
```

`BlockInventorySpecialGameUiRegistration.cs`:
```csharp
using Client.Input.ArrowKeys;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.SlotRestriction;
using Client.Ui.State.BlockInventory.Special;
using Client.Ui.State.BlockInventory.Special.Commands;
using Client.Ui.State.BlockInventory.Special.Source;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi.Screens
{
    // ブロックインベントリの特殊族（GameUi）の登録。詳細の Model・操作・矢印キーと、S4-alpha の拡張口への差し込み
    // Registers the block-inventory special family for GameUi: detail model, commands, arrow keys and the plug-ins to S4-alpha's ports
    internal static class BlockInventorySpecialGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            // 詳細と操作（State）
            // Details and commands (State)
            builder.RegisterEntryPoint<SpecialBlockDetailModel>().As<ISpecialBlockDetailModel>();
            builder.Register<PanelOpenSpecialBlock>(Lifetime.Singleton).As<IOpenSpecialBlock>();
            builder.Register<VanillaSpecialBlockRequestSender>(Lifetime.Singleton).As<ISpecialBlockRequestSender>();
            builder.Register<SpecialBlockCommands>(Lifetime.Singleton).As<ISpecialBlockCommands>();
            builder.Register<SpecialInletBindingContributor>(Lifetime.Singleton).As<IBlockSlotRestrictionContributor>();

            // 矢印キー・View の依存の束・Presenter の組み立て（パネルの源と格子の工場は alpha の共有サービスを受ける）
            // Arrow keys, the view dependency bundle and presenter assembly (panel source and grid factory come from alpha's shared services)
            builder.Register<InputSystemArrowKeyStream>(Lifetime.Singleton).As<IHorizontalArrowKeySource>();
            builder.Register<SpecialViewKit>(Lifetime.Singleton);
            builder.Register<SpecialPresenters>(Lifetime.Singleton);

            // S4-alpha の拡張口 (1)・(2)・(3) への差し込み
            // Plug-ins to S4-alpha's extension ports (1), (2) and (3)
            builder.Register<FilterSplitterBodyFactory>(Lifetime.Singleton).As<IBlockInventoryBodyFactory>();
            builder.Register<ElectricToGearBodyFactory>(Lifetime.Singleton).As<IBlockInventoryBodyFactory>();
            builder.Register<ElectricPoleBodyFactory>(Lifetime.Singleton).As<IBlockInventoryBodyFactory>();
            builder.Register<SpaceElevatorBodyFactory>(Lifetime.Singleton).As<IBlockInventoryBodyFactory>();
            builder.Register<RocketLaunchPadBodyFactory>(Lifetime.Singleton).As<IBlockInventoryBodyFactory>();
            builder.Register<RocketLaunchPadTitleActionFactory>(Lifetime.Singleton).As<IBlockInventoryTitleActionFactory>();
            builder.Register<GearToElectricSectionFactory>(Lifetime.Singleton).As<ISectionStackSectionFactory>();
            builder.Register<BeltThroughputSectionFactory>(Lifetime.Singleton).As<ISectionStackSectionFactory>();
            builder.Register<GearSectionFactory>(Lifetime.Singleton).As<ISectionStackSectionFactory>();
            builder.Register<ElectricNetworkSectionFactory>(Lifetime.Singleton).As<ISectionStackSectionFactory>();
            builder.Register<GearNetworkSectionFactory>(Lifetime.Singleton).As<ISectionStackSectionFactory>();
        }
    }
}
```
（このレーンの型はどれも自レーン専用なので、alpha の Produces 一覧の「共有サービスの並び」〈`MainGameUiRegistration`〉には足さず自分の登録ファイルに置く。分岐 B〈Task 1 Step 2〉では `SpecialInletBindingContributor` の行を書かない。`SpecialInletBindingContributor` は State の型なので `Client.Starter.asmdef` の参照は既存の `Client.Ui.State` で足りる。`Client.Input` の参照が `Client.Starter.asmdef` に無ければ足す〈`GrabGameUiRegistration` が `Client.Input.Pointer` を使っているので有るはず〉）

`MainGameUiRegistration.cs` の画面登録区画の末尾に（特定の行の次とは決めない。E-1 の追記 ad92a44777）。`using Client.Starter.Registration.GameUi.Screens;` が無ければ足す:
```csharp
            BlockInventorySpecialGameUiRegistration.Register(builder);
```

`GameUiTheme.tss` の `Screens` 区画の末尾に:
```css
@import url("project://database/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/BlockInventorySpecial.uss");
```

`BlockInventoryDesignTest.cs` の「%sから禁止Mantine部品とテーマ色を除去する」の `TestCase` 群の末尾に（alpha の書き方に合わせる）:
```csharp
        [TestCase("Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/BlockInventorySpecial.uss")]
```

- [ ] **Step 3: コンパイルし、テストと起動のエラーを見る**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "$FILTER"`
Expected: ErrorCount 0 / PASS（`SpecialFactoriesTest` 1・`SpecialSectionStackSpacingTest` 2・`SpecialBlockDetailsTest` 14〈TestCase 込み〉、L2 の `UiThemeImportBundleTest`・`UiStyleConventionTest`・`UiLayerDependencyDirectionTest`・`LookExceptionLedgerTest` を含む）

Run（全体スイッチ UITK で PlayMode を 1 回起動して止める）: `uloop execute-dynamic-code --project-path ./moorestech_client --code "Client.Input.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi); return 0;"` → `uloop control-play-mode --project-path ./moorestech_client --action Play` → 30 秒後に `uloop get-logs --project-path ./moorestech_client --log-type Error` → `uloop control-play-mode --project-path ./moorestech_client --action Stop`
Expected: Error 0 件（VContainer の解決失敗・二重登録の例外が無い）。alpha の起動時の情報ログに「空きの Slot」として GearToElectric・BeltThroughput・Gear・ElectricNetwork・GearNetwork が並ばない（`uloop get-logs --log-type Log` で `SectionStackSlotRegistry` を grep）

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special moorestech_client/Assets/Scripts/Client.Starter/Registration moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss"
git commit -m "feat(uitk): 特殊族を S4-alpha の拡張口へ登録し画面 USS を取り込む（S4-special）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 10: 並べ比べ（状態×解像度の表・参照キャプチャと UITK の撮影）

**Files:**
- Modify: `docs/uitk/TODO.md`（「## 見た目の例外」に `LX-126`〜`LX-130` のうち使った番号だけ）
- 成果物（コミットしない）: `moorestech_client/.uloop/outputs/compare/S4-special/*.png`・`compare.html`

**Interfaces:**
- Consumes: Task 9 までの全部、Task 11 の新シナリオと既存の 3 本の撮影、参照 `LOGS/uitk/reference-captures/web-a9d5123537/`・`web-0e81bbe3c3-2560x1080/`、Web の mock-host（`W/e2e/mock-host/`）、`Client.Ui.Editor.Capture.UiRuntimePanelCapture`（EditMode で本番の PanelSettings の複製に描く。A-74）・`PlaytestGameViewOps.SetResolution`
- Produces: `done.md` の「## 差の表」（下の撮影表の全行に「同じ／直した／LX-n／捨てる」）・`compare.html`（Task 14 の報告ページへ載せる）

設計書 `component-architecture.md` の「比べ方」の表（UITK の画面は参照と同じ状態を 720p と 2560×1080 の 2 種撮る）に合わせ、比べる単位を「状態×解像度」の 1 セルにする。1 状態は 2 セル（720p・2560×1080）で、下の 15 状態 × 2 解像度 = **30 セルすべての比較を完了条件**にする（1 セルでも撮れていない・比べていないなら Task 10 は未完了。`done.md` の「## 差の表」は 30 行）。

撮影表（Web の参照 id は `<組>/<名前>.png`。`ref720` = `web-a9d5123537`、`ref2560` = `web-0e81bbe3c3-2560x1080`、`self` = Step 1 で自分で撮る `compare/S4-special/web-<名前>@<解像度>.png`。UITK の撮影 id は `<シナリオ>/<p.Screenshot の名前>@<解像度>`、`edit/<名前>@<解像度>` は Step 3 の EditMode の撮影）:

| # | 状態 | Web 参照 id（720p / 2560×1080） | UITK 撮影 id（720p / 2560×1080） | 見る所 |
|---|---|---|---|---|
| 1 | 電柱（`electricPole`） | `ref720/4-block-electricPole` / `ref2560/4-block-electricPole` | `uitk-block-special-panels/electric-pole@720` / `@2560x1080` | 電力網の 1 行の段組 |
| 2 | 回転生成機（`electricToGear`） | `ref720/4-block-electricToGear` / `ref2560/4-block-electricToGear` | `uitk-block-special-panels/electric-to-gear@720` / `@2560x1080` | 縦の `ModeSwitch`・充足率の `GaugeBar` |
| 3 | フィルタ分配器（`filterSplitter`） | `ref720/4-block-filterSplitter` / `ref2560/4-block-filterSplitter` | `uitk-block-special-panels/filter-splitter@720` / `@2560x1080` | 方向ごとの行・横の `ModeSwitch`・枠 |
| 4 | 歯車発電機（`gearGenerator`。Gear・GearNetwork の 2 区画） | `ref720/4-block-gearGenerator` / `ref2560/4-block-gearGenerator` | `uitk-block-special-panels/gear-generator@720` / `@2560x1080` | 区画の間隔（`Stack gap="sm"`）。Web の fixture は `SimpleGearGenerator` で、マスタに同じ種別が無いので実行時は `燃料式風車`（`FuelGearGenerator`）で撮り、Gear・GearNetwork の 2 区画の領域だけ比べる（燃料の区画は S4-machine の持ち物） |
| 5 | 歯車（Gear ブロック） | `self/gear@720` / `self/gear@2560x1080` | `uitk-block-special-panels/gear@720` / `@2560x1080` | トルク・RPM の 2 行 |
| 6 | ベルト（180tick の窓と棒） | `self/belt@720` / `self/belt@2560x1080` | `uitk-block-special-panels/belt-throughput@720` / `@2560x1080` | `BarSeries` の棒の幅・高さ |
| 7 | 回転発電機（発電中） | `self/gearToElectricGenerating@720` / `@2560x1080` | `rotary-generator-ui/rotary-generator-generating@720` / `@2560x1080` | 状態の行・供給電力 |
| 8 | 回転発電機（停止。不足トーン） | `self/gearToElectricStopped@720` / `@2560x1080` | `rotary-generator-ui/rotary-generator-stopped@720` / `@2560x1080` | 不足の色 |
| 9 | 発射台（建造の合図待ち。題のボタン） | `self/rocketAwaitingBuild@720` / `@2560x1080` | `tier9-4-rocket-launch/02-panel-build-button@720` / `@2560x1080` | 3 群・状態文・題の右の逃げ（Web 99px＝`--panel-action-button-width`＋`--mantine-spacing-sm`。UITK が 12px 足りなければ alpha の依頼が未処理 → LX） |
| 10 | 宇宙エレベーター（`readyToConnect`） | `self/spaceElevator@720` / `ref2560/4-block-spaceElevator` | `edit/space-elevator-ready@720` / `@2560x1080` | 接続ボタン・プール量 |
| 11 | 宇宙エレベーター（`insufficientCable`） | `self/spaceElevatorInsufficient@720` / `ref2560/4-block-spaceElevatorInsufficient` | `uitk-block-special-panels/space-elevator@720` / `@2560x1080` | 不足トーンの状態文 |
| 12 | 宇宙エレベーター（`climbing`） | `self/spaceElevatorClimbing@720` / `ref2560/4-block-spaceElevatorClimbing` | `edit/space-elevator-climbing@720` / `@2560x1080` | 昇降の `GaugeBar` |
| 13 | 宇宙エレベーター（取得待ち） | `self/spaceElevatorLoading@720` / `ref2560/4-block-spaceElevatorLoading` | `edit/space-elevator-loading@720` / `@2560x1080` | 取得待ちの文 |
| 14 | 宇宙エレベーター（`outputBlocked`） | `self/spaceElevatorOutputBlocked@720` / `ref2560/4-block-spaceElevatorOutputBlocked` | `edit/space-elevator-output-blocked@720` / `@2560x1080` | 出口待ちの不足トーン・満杯の昇降 |
| 15 | 宇宙エレベーター（並べ替えた添字） | `self/spaceElevatorReordered@720` / `ref2560/4-block-spaceElevatorReordered` | `edit/space-elevator-reordered@720` / `@2560x1080` | 入口 1 枠・出口 2 枠の並び |

（行 10・12〜15 は実行時に決まった状態を作れない〈接続の直前・昇降の途中の値・詳細の未着・出口の満杯・ホストの並べ替え〉ので、UITK は EditMode の `UiRuntimePanelCapture` で本番の PanelSettings に本文だけを描いて撮る。比べるのは Web の参照の本文の領域〈`space-elevator-view` の外接矩形で切り抜く〉で、外殻〈題・閉じる・枠〉は行 11 の実行時の撮影で見る。D-34）

- [ ] **Step 1: Web の参照に無いセルを撮る（D-15）**

master の使い捨て worktree を作り（Unity を起動しないので `git worktree add` でよい。置き場は環境ローカル規約の worktree の置き場）、`W/e2e/mock-host/fixtures/blocks/` に一時の fixture を足して、撮影表の `self/*` のセルを **720p と 2560×1080 の両方**で撮る（行 5〜9 は両解像度、行 10〜15 は 720p だけ。2560×1080 は参照の組にある）。撮り方は既存の参照キャプチャの手順（`LOGS/uitk/reference-captures/README.md`・`W/../e2e/uitk-reference/shotRunner.ts` の `prepareShot`）に従う。fixture はコミットせず、worktree ごと消す。撮った png は `moorestech_client/.uloop/outputs/compare/S4-special/web-<名前>@<解像度>.png` へ置く。撮影表へ足すことは integrator へ依頼する（`done.md` の「## integrator への依頼」に 1 行。完了は integrator の撮影を待たない。E-22）

- [ ] **Step 2: UITK を実行時に両解像度で撮る**

全体スイッチ UITK で、Task 11 の新シナリオ `uitk-block-special-panels`（`Capture` が 1 状態を 1280×720 と 2560×1080 で撮る）を流す（Task 11 を先に流してよい）。既存の `connect/rotary-generator-ui`・`tier9/tier9-4-rocket-launch` は書き換えずに 2 回流す: 1 回目はゲームビュー 1280×720、2 回目は PlayMode の前に `uloop execute-dynamic-code --project-path ./moorestech_client --code "Client.Ui.Editor.Capture.GameViewResolution.Set(2560, 1080); return 0;"` で 2560×1080 にしてから流し、終わったら同じコードで 1280×720 に戻す。撮った png を撮影表の UITK 撮影 id の名前で `compare/S4-special/uitk-<id>.png` に置く

- [ ] **Step 3: 実行時に作れない状態を EditMode で両解像度で撮る（D-34）**

PlayMode を止めた Editor で、行 10・12〜15 の状態ごとに `uloop execute-dynamic-code` で `SpaceElevatorBodyView` を組み、`SpecialDetailSamples` と同じ形で Web の fixture（`energyFixtures.ts` の `blockSpaceElevator*`）の値の詳細を `Render` してから、`UiRuntimePanelCapture.Render(view, 1280, 720, <Web の地の色>)` と `(…, 2560, 1080, …)` で撮って png に書く（`Texture2D.EncodeToPNG`。破棄は呼び手）。View の格子の工場は記録係の押下先で組む（押下は見ない）。`UiRuntimePanelCapture` は EditMode 専用で、倍率は複製の ScaleWithScreenSize が決める（実行時と同じ倍率になることは行 11 の実行時の撮影と見比べて確かめ、違えば差の表の根拠に書く）

- [ ] **Step 4: 差の表を書く（30 セル）**

`compare.html`（左 Web・右 UITK を 30 セル並べ、差のあるセルに印）を作り、`done.md` の「## 差の表」に 1 セル 1 行で「# ／状態／解像度／Web 参照 id／UITK 撮影 id／差／処遇（同じ／直した／LX-n／捨てる）／根拠」を書く。30 行そろわないうちは完了にしない（撮れないセルが出たら、撮れない理由と代わりの撮り方を `done.md` の「## 未確認」に書いて指揮へ報告し、そのセルを空のまま完了にしない）。合否の目安は 720p で 2px（[A-14]）、2560×1080 は倍率を掛けた値。直すのは自分の `SP/**` と `BlockInventorySpecial.uss` だけ。alpha の外殻・部品の差（題の右の逃げ・`BlockItemGridGroup` の間隔など）は直さず `done.md` の「## integrator への依頼」へ。題の右の逃げ（行 9）は 99px に 12px（`--mantine-spacing-sm`）足りないままなら、alpha の依頼が未処理として `docs/uitk/TODO.md` の「## 見た目の例外」に LX を 1 つ振る。直せない差は同じ節に `LX-126` から番号を振る（書式は既存の行に合わせる。D-11 の JS 数値の写しで桁が合わない所が出たらここ）

- [ ] **Step 5: コミットする**

```bash
git add docs/uitk/TODO.md moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special
git commit -m "fix(uitk): 特殊族の並べ比べの差を直し、直せない差を見た目の例外に記帳する（S4-special）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```
（直した所も記帳も無ければコミットしない。`done.md` の 30 行がすべて「同じ」と書く）

---

### Task 11: unityプレイ録画テスト（全体スイッチ UITK）

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-block-special-panels.cs`
- 成果物（コミットしない）: 各シナリオの `result.json`・mp4（worktree の gitignore 下）

**Interfaces:**
- Consumes: Task 9 までの全部、DSL（`PlaytestRunner.Run`・`SetupDebugEnvironment`・`PlaceBlockDirect`・`WaitBlockGameObject`・`AimAt`・`PressInteract`・`WaitUiState`・`UntilUiElement`・`ClickUi`・`PressKey`・`GiveItem`・`Screenshot`・`Note`・`Assert`・`Until`）
- Produces: 4 本の `result.json`（`Success: true`）と mp4、`done.md` の「## 証拠」に `系=GameUi` の Note の行

- [ ] **Step 1: 新シナリオを書く**

`uitk-block-special-panels.cs`（前例 `connect/rotary-generator-ui.cs` の開き方〈照準 → F → `SubInventory`〉と `uitk/uitk-grab-overlay.cs` の GameUi の確かめ方。ブロックの日本語名はマスタ〈`../moorestech_master/server_v8/mods/*/master/blocks.json` の `name`〉で確かめて定数を直す）:
```csharp
// ブロックインベントリの特殊族（フィルタ分配器・回転生成機・電柱・ベルト・歯車・宇宙エレベーター）を UITK（GameUi）で通す
// Runs the block-inventory special family (filter splitter, electric-to-gear, pole, belt, gear, space elevator) on UITK (GameUi)
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("uitk-block-special-panels", options, async p =>
{
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）");
    p.Note("系=GameUi");
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    InputSystem.AddDevice<Keyboard>();
    InputSystem.AddDevice<Mouse>();
    p.WarpPlayer(new Vector3(5.5f, 34f, 2.5f));

    // フィルタ分配器: モードを択一で切り替え、枠を設定・解除する（掴みが空の左押下は送らない）
    // Filter splitter: switch a mode exclusively, set and clear a slot (an empty-grab left press sends nothing)
    var splitter = await Open("フィルタ分配器", new Vector3Int(2, 32, 6), "filter-splitter");
    await p.ClickUi("filter-mode-0-Blacklist");
    await p.Until(() => Selected("filter-mode-0-Blacklist"), 10f, "方向0がブラックリストへ");
    await p.GiveItem("鉄インゴット", 1);
    await p.ClickUi("filter-slot-0-0");
    await Capture("filter-splitter");
    await Close();

    // 回転生成機: 出力モードを選ぶと状態の選択が変わる
    // Electric-to-gear: choosing an output mode changes the selected state
    await Open("回転生成機", new Vector3Int(4, 32, 6), "electric-to-gear-view");
    await p.ClickUi("electric-to-gear-mode-0");
    await p.Until(() => Selected("electric-to-gear-mode-0"), 10f, "出力モード0が選ばれた");
    await Capture("electric-to-gear");
    await Close();

    // 電柱: 電力網の集約の 1 行が出る
    // Electric pole: the network summary row appears
    await Open("電柱", new Vector3Int(6, 32, 6), "electric-pole-view");
    await p.UntilUiElement("electric-network-section", 15f);
    await Capture("electric-pole");
    await Close();

    // ベルト: ←/→ で窓が切り替わる（←/→ は Player/Move でもあり横に動くので押下は短く、離れてパネルが閉じないようにする。D-32）
    // Belt: ←/→ switch the window
    await Open("ベルトコンベア", new Vector3Int(8, 32, 6), "belt-throughput-section");
    var before = Text("belt-throughput-window");
    await p.PressKey(Key.RightArrow);
    await p.Until(() => Text("belt-throughput-window") != before, 5f, "→ で窓が進む");
    await p.PressKey(Key.LeftArrow);
    await p.Until(() => Text("belt-throughput-window") == before, 5f, "← で戻る");
    await Capture("belt-throughput");
    await Close();

    // 歯車: トルクと RPM の 2 行
    // Gear: torque and RPM rows
    await Open("鉄の歯車", new Vector3Int(10, 32, 6), "gear-section");
    await Capture("gear");
    await Close();

    // 歯車発電機: 歯車と歯車網の 2 区画（Web の参照 4-block-gearGenerator と比べる。燃料の区画は S4-machine）
    // Gear generator: the gear and gear-network sections (compared with the 4-block-gearGenerator reference; the fuel section is S4-machine's)
    await Open("燃料式風車", new Vector3Int(12, 32, 6), "gear-network-section");
    await Capture("gear-generator");
    await Close();

    // 宇宙エレベーター: 群・状態区画・電力網が出る（接続は tier9 の 2 本が見る）
    // Space elevator: groups, the state section and the network appear (connection is covered by the two tier9 runs)
    await Open("宇宙エレベーター基部", new Vector3Int(14, 32, 6), "space-elevator-view");
    await p.UntilUiElement("space-elevator-status", 15f);
    p.Note($"宇宙エレベーターの状態文={Text("space-elevator-status")}");
    await Capture("space-elevator");
    await Close();

    #region Internal

    async UniTask<BlockGameObject> Open(string blockName, Vector3Int position, string testid)
    {
        await p.PlaceBlockDirect(blockName, position, BlockDirection.North);
        var block = await p.WaitBlockGameObject(position);
        await p.AimAt(block.transform.position + new Vector3(0.5f, 0.5f, 0.5f));
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        await p.UntilUiElement(testid, 15f);
        return block;
    }

    async UniTask Capture(string name)
    {
        // 1 状態を 720p と 2560×1080 の 2 種で撮り、720p へ戻す（設計書「比べ方」。Task 10 の撮影表）
        // Capture one state at 720p and 2560x1080, then return to 720p (design doc comparison table; Task 10 shot table)
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);
        await p.Screenshot($"{name}@720");
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, 2560, 1080);
        await p.Screenshot($"{name}@2560x1080");
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);
    }

    async UniTask Close()
    {
        await p.ClickUi("block-inventory-close");
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }

    bool Selected(string testid) => PlaytestUiElements.HasClass(testid, "mode-switch__option--selected");
    string Text(string testid) => PlaytestUiElements.ReadText(testid);

    #endregion
});
```
（`PlaytestUiElements.HasClass`・`ReadText` は GameUi の要素を testid で読む DSL の実物の名前に合わせる〈無ければ `GameUiRoot.Instance.GetRootElement().Q<…>(testid)` で読む。前例 `uitk-grab-overlay.cs`〉。照準の位置は前例の `AimAtClickCollider` に合わせて直す。`PlaytestGameViewOps.SetResolution` は前例 `uitk/uitk-gameview-resolution-switch.cs` の書き方。宇宙エレベーターの状態文〈Note〉が「ケーブルが足りません」〈`insufficientCable`〉でなければ、Task 10 の行 11 を EditMode の撮影〈Step 3〉に回し、`done.md` に書く。`PlaceBlockDirect` がティックと競合して落ちる既知の不具合〈bd `moorestech-xyuaf.2.10`〉に当たったら、置いた後に `p.WaitSeconds(0.5f)` を挟み、それでも落ちれば `done.md` の「## 未確認」に書いて既存の 3 本で代える。ローカル関数の式本体は `Func` を使わない）

- [ ] **Step 2: 4 本を流す**

Run（各本の前に PlayMode を止め、`uloop execute-dynamic-code --project-path ./moorestech_client --code "Client.Input.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi); return 0;"` を流す）:
```bash
SK=.agents/skills/unity-playmode-recorded-playtest
$SK/scripts/run-scenario.sh $SK/scenarios/uitk/uitk-block-special-panels.cs
$SK/scripts/run-scenario.sh $SK/scenarios/connect/rotary-generator-ui.cs
$SK/scripts/run-scenario.sh $SK/scenarios/tier9/tier9-4-rocket-launch.cs
$SK/scripts/run-scenario.sh $SK/scenarios/tier9/tier9-4b-rocket-cargo-shift-reject.cs
```
Expected: 4 本とも `result.json` が `Success: true`、mp4 がある。既存の 3 本は書き換えない。既存の 3 本が GameUi で落ちたら、(a) 落ちた所がこのレーンの View・Model なら直して流し直す、(b) alpha の外殻・格子・Shift の実行器なら直さず `done.md` の「## integrator への依頼」に書き、Web の系（`UiSystem.Web`）でも同じ所で落ちるかを流して比べ、Web で通るなら UITK の欠落として「## 未確認」へ、両系で落ちるなら既存の不具合として bd に積む（`bd create … --parent <bd>`）

- [ ] **Step 3: 証拠を書いてコミットする**

`done.md` の「## 証拠」に 4 本の `result.json` の要約（Success・Note の `系=GameUi`・mp4 のパス）を書く。
```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-block-special-panels.cs moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special
git commit -m "test(uitk): 特殊族の録画シナリオを足し、既存の 3 本と GameUi で通す（S4-special）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 12: テスト名の対応・処遇の表・全体の回帰

**Files:**
- Modify: `docs/uitk/disposition.md`（「## 処遇」の末尾 1 行。D-13）
- Modify: `docs/uitk/TODO.md`（「## 実測の結論」の末尾。このレーンで実測したことがあれば）

**Interfaces:**
- Consumes: Task 2〜11 の全テスト、`tools/uitk/test-name-parity.mjs`
- Produces: 束 4 のこのレーンの持ち分の未対応 0

- [ ] **Step 1: 捨てる 1 件を処遇の表に書く**

`docs/uitk/disposition.md` の「## 処遇」の末尾に（列は既存の行に合わせる）:
```markdown
| `src/features/blockInventory/views/__tests__/RocketLaunchPadInventory.test.tsx` | accepts panel data and narrows inside the dedicated view | 捨てる | TS の型検査（`expectTypeOf`）。C# は型がコンパイルで閉じる | S4-special |
```

- [ ] **Step 2: テスト名の対応を確かめる**

Run: `node tools/uitk/test-name-parity.mjs --bundle 4 --emit-missing`
Expected: File Structure「写すテスト」の表の 14 ファイル・44 宣言の未対応が 0、書式・整合の誤り 0（alpha・machine・train の持ち分の未対応は数えない。分け合う `blockDetails`・`regression/sectionStack` は S4-machine・alpha のタグと同じ名前が並んでよい）

- [ ] **Step 3: 全体の回帰を流す**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "$FILTER"` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\." --timeout-seconds 1500`
Expected: PASS（`UnityCursorTooltipScreenTest` の既知の失敗は数えない。それ以外が落ちたらこのレーンの変更との関係を調べ、関係するなら直す）。件数を `done.md` の「## 証拠」に書く。Web の写しは 69 ケース（捨てる 1 を除く）が NUnit の実行件数（`[TestCase]` 込み）に含まれることを、`<web-test>` の付いたテストの実行件数で確かめる（末尾「設計書・実行計画へ反映する行」の §1.4 の宣言・ケースの根拠）

- [ ] **Step 4: コミットする**

```bash
git add docs/uitk/disposition.md docs/uitk/TODO.md
git commit -m "docs(uitk): 特殊族の捨てる 1 件を処遇の表に記す（S4-special）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 13: 全ブランチレビュー

- [ ] **Step 1: 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

base は `origin/feature/uitk`。記録は `../moorestech_logs/harness/` へ（featureブランチにコミットしない）。指摘の反映が判定経路・条件式・評価の時点（例: `SpecialDetailPresenter` の `IsApplicable` の書き込みと同じパネルの判定・`BeltThroughputSectionPresenter.OnArrowPressed` のガード・`SpecialBlockCommands` の種別の確かめ）に触れたら、Task 11 の 4 本を反映後のバイナリで流し直してから完了とする。反映はタスクごとにコミットする

---

### Task 14: PR・取り込み・G1・マージ・片付け

- [ ] **Step 1: セッション終了可能状態にすること: pr-create スキルで PR を作る**

base `feature/uitk`。PR 本文の先頭行は `# https://review.moores.tech/pr/<PR番号>/report/`（h1 で URL だけ）。報告ページには Task 10 の `compare.html` と Task 11 の 4 本の録画を載せる（素材・manifest・公開コマンドは環境ローカル規約〈CLAUDE.local の「PR の成果物の載せ先」〉が指す README。公開に失敗したら報告 URL 無しで完了にしない）。画像・動画の URL を本文に並べない

- [ ] **Step 2: `origin/feature/uitk` を取り込み、確かめて push する**

```bash
git fetch origin
git merge --no-ff origin/feature/uitk
```
衝突は共有区画（`MainGameUiRegistration.cs`・`GameUiTheme.tss`・`Client.Ui.State.asmdef`・`UiCommandRejectReason.cs`・`PauseMenuCommandsTest.cs`・`localization.csv`・`localizationKeys.ts`・`TODO.md`・`disposition.md`）で起きうる。両方の行を残す（enum の値は末尾に並べ、`localizationKeys.ts` は `pnpm --dir moorestech_web/webui run gen:i18n` で作り直す）。`Registration/GameUi/` の再編が入っていれば登録ファイルを再編先へ移し、File Structure と違う所を `done.md` に書く（D-20）。
Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "$FILTER"`
Expected: ErrorCount 0 / PASS。push する

- [ ] **Step 3: G1 が緑になるのを待つ**

Run: `gh pr checks <PR番号> --watch`
Expected: 全部緑。`UnityCursorTooltipScreenTest` の失敗は既知（CI で前からある）なので、それだけなら緑と読む。他が落ちたら直して push し直す

- [ ] **Step 4: マージし、片付ける**

`Registration/GameUi/Screens/*.cs` が 10 本を超えたままなら（再編が未着）マージせず、`done.md` を「状態: 停止（`Registration/GameUi/Screens/` の再編待ち）」にして指揮へ渡す（D-20）。超えていなければ:
```bash
gh pr merge <PR番号> --merge
bd close <bd> --reason "PR #<PR番号> を feature/uitk へマージ"
```
`done.md` の 1 行目を「状態: 完了」にする。テストで `Saves/` に作ったワールドを `list-work-saves.sh` で探して消す。`moores-wt rm <worktree 名>` で worktree と Editor を畳む（環境ローカル規約）

---

## Self-Review

1. **Requirements の網羅**: R-1 → Task 2・3（L0）＋Task 11。R-2・R-3・R-4 → Task 5・6。R-5・R-6 → Task 7。R-7・R-8 → Task 8。R-9 → Task 3（L0）・Task 9（登録）・Task 11（`tier9-4b`）。R-10 → Task 4・8。R-11 → Task 9。R-12 → Task 11。R-13 → Task 10。R-14 → Task 12。R-15 → Task 13・14。漏れなし
2. **プレースホルダ**: 「実物に合わせる」は、起草時に実物を確かめられなかった名前（alpha の未マージの型、`RocketLaunchPadPhase` の値の綴り、DSL の要素の読み取り）だけに付け、照合のコマンドを Task 1 Step 2 に置いた。TBD・「同様」は無い
3. **型の一致**: `ISpecialDetailRenderer`（`HasDetail`・`Render(IReadOnlyList<SlotItem>, SpecialBlockDetailViewModel)`）を Task 6〜9 の全 View が実装する。`SpecialDetailPresenter`・`BeltThroughputSectionPresenter`（`IBlockInventorySection`。`Attach`/`Detach`・`IsApplicable`）・`SpecialPresenters`・`SpecialViewKit.Run`・`RecordingSpecialBlockCommands` の 1 行の書式（`<action> <args>`）・`RecordingBlockSlotCommands` の書式（`LeftDown <slot>`）を Task 6〜9 で同じ綴りで使う。alpha の型は Global Constraints の「S4-alpha の API」（85f905aa3d）と同じ綴り
4. **保留・縮退の解消**: (a) 詳細の未着 → 取得待ち・区画を隠す（Presenter の `IsApplicable` が偽 → alpha の `SetItemShown`）。届けば Presenter の購読で描き直し出す側へ変わる（`Attach` から `Detach` まで購読。L1「Presenterは組み直しの外で…」と統合 L1 `SpecialSectionStackSpacingTest`）。要素 0（詳細が一度も来ない）でも取得待ちの文が出続けるので無音にならない。(b) Shift の入れ口の未着 → 寄与しない → L0-s4b が取得待ち → alpha の通知。詳細が届けばパネルの組み直し（追跡ブロックの状態の変化）で寄与が入る。(c) 掴みが空のフィルタ設定 → 良性の拒否（警告ログ）。(d) 合図の古い拒否 → 良性。段階が変われば題の操作が消える（L1 の 2 件）。(e) 区画の出し入れは親の `SetItemShown` が当てるので、組み直しの外で出し入れが変わっても先頭の間隔はその場で組み直る（D-23。ずれは残らない）。(f) 開いたブロックの識別と追跡ブロックが一致しない間（開閉・差し替えの途中）は詳細を空にし、Shift の寄与もしない → 本文は取得待ち・Shift は取得待ちの通知
5. **「決定的」を理由にした選択**: 無い。ベルトの窓の既定 180 と端で止まる巡回は Web の `cycleBeltWindow` の写し（L1 で固定）
6. **呼び出し側への責務漏出**: 掴みの中身を読んで設定/無操作を決めるのは `SpecialBlockCommands`（State）で、View は左右の押下を渡すだけ。結果のトーストは `SpecialViewKit.Run` の 1 箇所。開いたブロックの種別の確かめも Commands の中。View 側に同じ判定は無い

## 判断記録（ADR）

設計の出所: 設計書 `docs/uitk-migration/execution/component-architecture.md`（[A-8] View が操作を直接呼ぶ・[A-14] 並べ比べの目安・[A-45]〜[A-47]・[A-57] 変化ごとに同期で描き直す）、実行計画 `docs/uitk-migration/execution/execution-plan.md`（§1.4 S4-special 行・§6.2・E-1・E-13・E-19・E-22）、裁定 `.decisions/2026-11-06-*`（全体スイッチ 1 つ・Web 版と同じ見た目・CEF 由来の不具合は直さない）。以下は planning 中に生じた判断。

- **D-1 所有の境界**: 指揮の申し合わせ（PLAN-W4a）どおり。このレーンは `Screens/BlockInventory/Special/`・`Client.Ui.State/BlockInventory/Special/`（`Logic/` を除く）・`BlockInventorySpecialGameUiRegistration.cs` だけを書く。S4-machine の `GeneratorSection`（ElectricGenerator・FuelGearGenerator・SimpleGearGenerator）は持たない。`SimpleGearGenerator` の参照 `4-block-gearGenerator` でこのレーンが見るのは Gear・GearNetwork の 2 区画だけ
- **D-2 Web の橋と UITK の読み手の並走**: Web の `BlockDetailDtoBuilder` 系は変えず、UITK 用の読み手 `SpecialBlockDetailReader` を State に新設して同じ源を読む。CEF の撤去まで 2 本並ぶ（本PR外のリファクタ提案へ）。却下: Web の橋の DTO を UITK が読む（`Client.WebUiHost` への依存が層の向きに反する）
- **D-3 特殊族専用の詳細の Model**: alpha の `OpenBlockInventoryPanel` に特殊族の詳細を混ぜず、`SpecialBlockDetailModel` を別に持つ。alpha は「連続値は区画が自分で購読」と定めており（Produces 一覧 (3)）、網の集約・ベルトの統計はパネルの組み直しの外で届くため
- **D-4 Presenter 1 つ＋ベルトだけ専用**: `SpecialDetailPresenter` が alpha の本文・題の操作・区画の 3 つの口を 1 つで実装し、View は `ISpecialDetailRenderer` だけを知る（購読しない）。ベルトは窓の選択と ←/→ の購読という区画固有の状態を持つので `BeltThroughputSectionPresenter` を分ける。購読の源（詳細の Model・パネル・矢印キー）を持つのは `SpecialPresenters` だけ。alpha の口の形が変わったときの直し所を Presenter と工場に閉じる
- **D-5 操作は非同期・楽観更新なし**: 結果はサーバーの応答と後続の状態で表示が変わる（Web の dispatchAction と同じ）。`SpecialViewKit.Run` が結果をトーストの係へ渡す
- **D-6 拒否理由 4 つ**: `SpecialBlockNotOpen`・`SpecialBlockRequestFailed`（良性でない）・`SpecialBlockCommandStale`・`FilterSplitterGrabEmpty`（良性。Web の BENIGN_ERRORS と noop）。wire は Web と同じ文字列
- **D-7 矢印キーの流れを `Client.Input/ArrowKeys` に**: `Client.Ui` は Input System を直に読まない。前例 `InputSystemPointerButtonStream`（E-19）。namespace を `Client.Input.Keyboard` にしない（`UnityEngine.InputSystem.Keyboard` とぶつかる）。比べた案: `InputManager.UI` に `InputKey` を足して `moorestechInputSettings` の UI マップへ ←/→ を割り当てる案。却下の理由は、生成物の入力設定（共有の資産）を書き換えることになり、←/→ は Player/Move と同じキーで UI マップの抑止（`InputSuppressionScope`）の意味が合わないこと。Web は window の keydown をマップに依らず聞き、入力欄と層だけで絞っていたので、それと同じ判定（`UiInputExclusivity.IsTextInputFocused`・表示中か）を受け手に置く。前例 `InputSystemPointerButtonStream` は interface を持たず具象を ctor で注入しているが、ここは区画の L1 で偽物に差し替えるため `IHorizontalArrowKeySource` を切る。前例から借りるのは「縁を流す・リセットで記憶を下ろす」の形だけで、役割（UI のキー操作）の前例は UITK に無い新規パターンとして扱う
- **D-8 asmdef の参照 4 つ**: `Client.Ui.State` に `Game.Block`・`Game.Gear`・`Core.BeltTransport`・`Core.Update`（Web の橋 `Client.WebUiHost.asmdef` が同じ源を読むための参照と同じ）
- **D-9 フィルタの択一の選択肢の testid**: `filter-mode-{d}-{mode}`（Web は名前で押すが UITK の L1 は testid で押す）。掴みが空の左押下は View でなく Commands が良性に断る（D-6）
- **D-10 ベルトの窓の選択は Presenter が持つ**: Web の `useState` と同じく区画の寿命の状態（`BeltWindowCursor`）を `BeltThroughputSectionPresenter` が持ち、`Detach` で既定 180 へ戻る（Web の破棄と同じ。alpha は同じパネルの間は部分を作り直さないので、パネルを替えると既定へ戻る）
- **D-11 JS の数値書式の写し**: `toFixed(0)`・`toPrecision(2)`・`String(number)` を `JsNumberText` に写す（node で実測した境界値を L0 で固定）。桁が合わない所が出たら LX に記帳
- **D-12 発射台の枠の割り当て**: `RocketLaunchPadSlotLayout`（サーバーの定義）から資材・積荷・出力の先頭を引く。Web と同じ絶対添字のまま
- **D-13 捨てる 1 件**: `RocketLaunchPadInventory.test.tsx` の「accepts panel data and narrows inside the dedicated view」は TS の型検査なので捨てる（`disposition.md` に 1 行）
- **D-14 回転発電機の i18n の件を写す**: §1.4 に無いが同じ区画の Web テストなので持ち分に入れる
- **D-15 参照に無い状態は使い捨ての master の worktree で撮る**: Web の mock-host に一時の fixture を足して撮り、fixture はコミットしない（E-22）
- **D-16 録画**: 既存の 3 本（書き換えない）と新規 `uitk/uitk-block-special-panels` の 4 本。ランタイムの挙動（入力・UI）に触れるので unityプレイ録画テストを入れる
- **D-17 Shift の入れ口**: alpha の拡張口 (6) `IBlockSlotRestrictionContributor` に `SpecialInletBindingContributor` を差し込む（分岐 A）。口が実物に無ければ分岐 B（静的な判断だけ作り integrator へ依頼）。L0 は判断まで、送らない・通知は alpha と録画
- **D-18 新しい汎用部品を作らない**: 使う部品は既存の `ModeSwitch`・`GaugeBar`・`BarSeries`・`PanelActionButton`・`ItemSlot` と alpha の `Parts/`
- **D-19 L3 を持たない**: 段の締め（S4-gamma）が持つ
- **D-20 登録の置き場と数**: 登録ファイルは W4 以降の画面の置き場 `Registration/GameUi/Screens/` に置き（実行計画 E-1 の追記 ad92a44777）、10 ファイルの関門もそこで数える。W4 の画面レーン（alpha・inventory・machine・train・special・recipe 等）が先に入って 10 本を超えたら、作業は止めずに進め、再編が `origin/feature/uitk` に入るまでマージしない。名前空間はディレクトリに合わせ `Client.Starter.Registration.GameUi.Screens`（`MainGameUiRegistration.cs` の画面登録区画は他の W4 レーンの行と同じ書き方に合わせ、`using Client.Starter.Registration.GameUi.Screens;` が無ければ足す）
- **D-21 回転生成機の択一はモードの一覧が変わったときだけ作り直す**: 毎回作るとフォーカスと押下が切れる。`ElectricToGearDetail.HasSameModes` で比べる
- **D-22 合図の古い拒否は良性**: 成功の後に段階の更新が遅れた二度目の押下の拒否をトーストしない（Web の「%sの成功後topicが遅れても二度目の拒否を誤報しない」）
- **D-23 区画の出し入れは alpha の通知で解消**: 区画の Presenter は詳細の有無で `IsApplicable`（`IReadOnlyReactiveProperty<bool>`）を書くだけで、自分の `display` を触らない。alpha の `SectionStackBody` がそれを購読して `UiGapContainer.SetItemShown` を呼び、出し入れと先頭の間隔をその場で組み直す（S4-alpha 計画 85f905aa3d の拡張口 (3)）。網の集約・ベルトの統計のようにパネルの組み直しの外で先頭の区画が後から出る・消える場合も、後続の区画の余白まで Web の `Stack gap="sm"` と同じになることを統合 L1 `SpecialSectionStackSpacingTest`（alpha の標準の本文に本物の区画 5 つを差す）で確かめる。本文の中に区画の View を置く所（電柱・宇宙エレベーターの電力網）は、包む本文が `HasDetail` で出し入れする（Stack の項目なら `SetItemShown`）。出所: 独立評価（codex）の指摘と alpha の改訂。却下: 区画の View が自分の `display` を切り替える（`UiGapContainer` が先頭の間隔を組み直さず、次のパネルの組み直しまで Web と違う間隔が残る）・区画が親の `SetItemShown` を直に呼ぶ（alpha の内部の形に依る）
- **D-24 フィルタ分配器の 3 件目**: alpha の外殻の L1 の土台（本文を差し込んだ外殻と自然高のスクロール器）に載せて写す。土台が無ければ部分移植にし、録画の分配器の段で末尾の枠を見る
- **D-25 View の依存の束 `SpecialViewKit`**: 工場と View が同じ 4 つ（Commands・表示素材・トースト・格子の工場）を受けるので 1 つに束ね、結果をトーストへ渡す `Run` も 1 箇所に置く（同じ 3 行が View ごとに並ぶのを避ける）。購読の源（詳細の Model・パネル・矢印キー）は束に入れず `SpecialPresenters` が持つ（View が購読できる依存を持たない。D-33）。UITK に同じ形の前例は無い新規パターン。却下: 各工場が個別に ctor で受ける（工場 11 本の ctor と View の ctor の引数が全部同じになる）
- **D-26 開いたブロックの識別は alpha のパネルの 1 本**: alpha のパネルが `BlockInstanceId`・`Position`・blockType を持つ（85f905aa3d）ので、操作（`PanelOpenSpecialBlock`）・詳細の Model（`SpecialBlockDetailModel`）・Shift の寄与（`SpecialInletBindingContributor`）は、どれも「どのブロックが開いているか」を alpha のパネルから読む。パネルは `BlockGameObject` を持たないので、state detail・マスタの param は `OpenBlockDetailTracker.TrackedBlock` から読み、`BlockInstanceId` が一致する間だけ使う（開閉・差し替えの途中は空・寄与なし）。追跡ブロックは識別の源ではなく中身の読み口。却下: 操作の識別を `SubInventoryState` から読む（Web の ActionHandler と同じ源だが、パネルと別の源が並び、差し替えの途中で操作の対象と表示が食い違いうる）・パネルに `BlockGameObject` を戻すよう依頼する（alpha が識別子だけに絞った設計に反する）
- **D-27 Screens へサーバーの列挙を出さない**: `Client.Ui.asmdef` は `Game.Block.Interface` を参照しない（層の向き）。`RocketLaunchPadDetail`・`SpaceElevatorDetail` のサーバーの列挙は internal にし、文言キー・コマンドのボタン・接続できるかを State で引いて出す（Phase 2.6 の指摘で直した）
- **D-28 詳細の VM は 9 つの任意の詳細を並べる（判別子で分けない）**: 1 つのブロックが複数の詳細を同時に持つ（宇宙エレベーターは本体＋電力網、機械は歯車＋電力網＋歯車網＋ベルト）ので、`BlockInventoryBodyKind` 1 つで分ける型にはできない。不在は `TryGet*` だけで読み、null を外へ出さない（Phase 2.6 の 5-A。シミュレーター予測「このままでよい」）
- **D-29 送信の結果 `SpecialBlockRequestOutcome` は Kind＋WireCode のまま**: 結果は `SpecialBlockCommands` の 1 関数の中だけで `UiCommandResult` へ写され、Accepted の `WireCode` を読む所が無い（Phase 2.6 の 5-B。シミュレーター予測「このままでよい」。読む所が増えたら型を分ける）
- **D-30 失敗理由を畳む Try 3 つは畳んだまま**: `TryGetInletBindings`（固定でない・未着・param の食い違い）・`IOpenSpecialBlock.TryGet`（開いていない・識別の未着）・`SpecialBlockCommands.TryOpen`（開いていない・種別違い）。どれも分けた理由で呼び手の分岐が変わらず、種別違いと param の食い違いは別にログか wire で区別している（Phase 2.6 の 5-C・弱）
- **D-31 発射台・宇宙エレベーターの判定が Reader と入れ口の寄与に 2 回出る**: 入れ口は要求の品（マスタの `RequirementAt`）が要り、詳細の型は品を持たないので導出を 1 本にできない。重複は param の型の判定と状態の在否の 2 行に留める（Phase 2.6 の検査 6・弱）。ベルトの窓の 2 度引きはコードを 1 回に直した
- **D-32 ブロックインベントリ中の ←/→ の横移動は据え置く**: ←/→ は Player/Move と同じキーで、SubInventory は移動をロックしない（Web の系も同じ）。このレーンでは変えない。出所: シミュレーター予測（(a) Web と同じとして据え置く。根拠: 2026-11-06 裁定「Web と同じ振る舞い」「CEF 由来の不具合は直さない」）。移動を止めるなら `SubInventoryState.LocksPlayerMovement()` と `UIStateMovementLockDeclarationTest` の別件
- **D-33 差し込みの形は alpha の Presenter＋View に合わせる**: S4-alpha 計画 85f905aa3d の「差し込みの形」（Presenter が `Attach(panel)` で自レーンの Model を購読し View の `Render` へ流す・`Detach` で切る・View は購読しない・パネルは識別子だけ）に合わせ、前版の「適合の層が作ってから Detach まで購読する」形を改めた。同じパネルの間は alpha が何も呼ばないので、枠の変化も Presenter が `IBlockInventoryPanelSource` を購読して受ける（前例 alpha の `SectionStackItemGridSection`）。`SpecialViewKit` から購読の源（Model・矢印キー）を外した（View が購読できる依存を持たない）。出所: alpha の改訂
- **D-34 実行時に作れない状態は EditMode で本番の PanelSettings に描いて撮る**: 宇宙エレベーターの接続直前・昇降の途中・取得待ち・出口の満杯・並べ替えた添字は、録画で決まった値を作れない。`UiRuntimePanelCapture`（A-74。本番の PanelSettings の複製に描く）で本文を 720p・2560×1080 で撮り、Web の参照の本文の領域と比べる。外殻は実行時の撮影（行 11）で見る。これで撮影表の 30 セル全部が比べられる。設計書 §3.3 の「PlayMode・ScreenCapture で撮る」の例外になるので A-164 として反映する行に置く。却下: その状態を比べない（設計書の「同じ状態を 2 解像度で」を満たさない）・mock の状態を実行時に作るデバッグ口を足す（プロダクションにテスト専用の口を残す）。出所: 独立評価（codex）の指摘
- **D-35 写すテストは宣言とケースの 2 つで数える**: 名前の照合（`<web-test>`・test-name-parity）は宣言、実行の完了は `it.each`・`for` を展開したケース（`[TestCase]` と揃える）で見る。前版の「実行計画が it.each を数えていない」は誤りで、§1.4 は一貫して宣言で数えており、食い違いは捨てる 1 件の除外・`SpaceElevatorInventory` と i18n の欠け・他レーンから回ってきた `blockDetails`・`regression/sectionStack` の欠けだった。`regression/sectionStack` は alpha の持ち主表が区画の id を持ち主のレーンへ割り振っているので、特殊族の区画の id が出る 8 種も写す（名前の照合は他レーンの同じタグで通ってしまうので、ケースで数えないと穴が見えない）。出所: 独立評価（codex）の指摘
- **シミュレーター予測（review 実施済み）**: 適用推奨 1 件（死活表の ←/→ の行の前提の誤り）を適用、Warning 3 件（D-20 の条件文・D-7 の前例との形の違い・電柱は無印だけ openable）のうち前 2 件を適用、電柱は Task 11 が無印を使うので変更なし。裁定候補 1 件（←/→ の横移動）は予測 (a) で D-32 に記録（ゴール・裁定から外れないので要裁定 0）

## 本PR外のリファクタ提案

- Web の橋 `Client.WebUiHost/Game/Topics/BlockDetail/**` の特殊族の DTO 組み立てと、このレーンの `SpecialBlockDetailReader`・`SpecialStateDetailReader`・`SpecialNetworkReader`・`BeltThroughputReader` は同じ源から同じ値を組む（D-2）。CEF の撤去のときに Web の橋の側を消す
- Web の橋の `FilterSplitter*ActionHandler`・`ElectricToGearSetOutputModeActionHandler`・`SpaceElevatorConnectActionHandler`・`RocketLaunchPadCommandActionHandler` と `VanillaSpecialBlockRequestSender`・`SpecialBlockResponseCodes` は同じ送信と失敗コードの写しを持つ。同じく CEF の撤去で 1 本にする
- 既存 `Client.WebUiHost/Game/Topics/BlockInventoryTopic.cs`＋`Topics/Inventory/BlockInventoryRepublishPolicy.cs`／新設 `SpecialBlockDetailModel`: どちらも「追跡ブロック・状態・集約・ベルトの窓の進みで詳細を出し直す」契機の判定。既存はベルト以外の窓の進みを捨てる（`RepublishOnBeltWindow`）が、新設は窓の進みごとに組み直す。出し直しの方針を 1 本にする
- 既存 `Client.Input/Pointer/InputSystemPointerButtonStream.cs`／新設 `Client.Input/ArrowKeys/InputSystemArrowKeyStream.cs`: 「`ReadValueFromEvent`＋0.5 の閾値で押下の縁を判定し、リセットで記憶を下ろす」同じ判定。縁の判定を小さな共通の型へ寄せる

## 未確認

- S4-alpha の型の綴り（85f905aa3d は再検査中）。Task 1 Step 2 で照合する。`BlockInventoryBodyMount`・`SectionStackSectionMount` の ctor をテストから呼べるか、`OfType` が同じ位置・Guid のパネルを返すか
- 題の右の逃げ（+12px）を alpha の依頼が処理したか（Task 10 の行 9 で見る）
- `UiRuntimePanelCapture` の倍率が実行時の舞台の倍率と同じか（Task 10 Step 3）
- `RocketLaunchPadPhase`・`RocketLaunchPadWaitReason`・`SpaceElevatorBaseStatus` の値の綴り、`BlockTypeConst` の定数名、`ItemSlotContent` の count に null を渡せるか、`ModeSwitchOption` の押し面が `Button` か
- ベルトの区画がパネルから外れたとき窓が既定へ戻ること（L1 で見る。alpha が同じパネルの間に区画を作り直さない前提）
- 新シナリオのブロックの日本語名と照準の位置（Task 11 Step 1）
- `PlaceBlockDirect` のティックとの競合（bd `moorestech-xyuaf.2.10`）が新シナリオに当たるか

## 設計書・実行計画へ反映する行

設計書 `docs/uitk-migration/execution/component-architecture.md` の A 表へ:

| ID | 判断 |
|---|---|
| A-160 | ブロックインベントリの特殊族は専用の詳細の Model（`SpecialBlockDetailModel`。alpha のパネルの源・`OpenBlockDetailTracker`・`BlockNetworkInfoCache`・`BeltTransportClientCache` を購読し、開いたブロックの識別は alpha のパネルの `BlockInstanceId` に揃える）を持ち、Presenter 1 つ（`SpecialDetailPresenter`。ベルトだけ `BeltThroughputSectionPresenter`）が S4-alpha の本文・題の操作・区画の口を実装する。View は購読しない。区画は自分の `display` を触らず `IsApplicable` を書き、alpha の `SectionStackBody` が `SetItemShown` で出し入れと先頭の間隔を組み直す。`Client.Ui.State.asmdef` は Web の橋と同じ源を読むため `Game.Block`・`Game.Gear`・`Core.BeltTransport`・`Core.Update` を参照する |
| A-161 | 特殊族の操作の拒否理由は `SpecialBlockNotOpen`・`SpecialBlockRequestFailed`（良性でない）・`SpecialBlockCommandStale`・`FilterSplitterGrabEmpty`（良性）。wire は Web と同じ |
| A-162 | ←/→ の押下は `Client.Input/ArrowKeys/InputSystemArrowKeyStream`（`IHorizontalArrowKeySource`）が流し、受け手が入力欄のフォーカスと表示中かを確かめる |
| A-163 | 発射台・宇宙エレベーターの Shift の入れ口は S4-special が `IBlockSlotRestrictionContributor` で寄与する。束縛は追跡ブロックをパネルの `BlockInstanceId` で照らして読み、一致しない間は寄与しない（取得待ち） |
| A-164 | 画面レーンの並べ比べで、実行時に決まった値を作れない状態（接続の直前・途中の進み・詳細の未着・満杯・ホストの並べ替え）は、EditMode の `UiRuntimePanelCapture`（A-74）で本番の PanelSettings に本文だけを 720p・2560×1080 で描いて撮り、Web の参照の本文の領域と比べる。外殻は同じ画面の実行時の撮影（PlayMode・ScreenCapture）で見る。§3.3「比べ方」の「UITK の画面」の行の例外として足す（S4-special D-34） |

部品カタログの差: なし（新しい汎用部品を作らない。D-18）

実行計画 §1.4 の S4-special 行（PLAN-W4a の改め案〈876b3229e5〉の文面を土台に、件数を宣言・ケースの 2 つで書き、`blockDetails`・`regression/sectionStack` の持ち分・Inventory・i18n・新規の録画を足す）:

| レーン | 完了条件 |
|---|---|
| S4-special | FilterSplitter・ElectricToGear・GearToElectric・ElectricPole・Gear・ベルトの流量（`BarSeries`）・ロケット・宇宙エレベーター。写す（宣言/ケース）: `blockDetails` の歯車とフィルタの面（S4-machine 計画の対応表で S4-special に回したもの。歯車機械のトルク・RPM・網、歯車発電機、歯車ポンプの歯車行、`renders ${type}` の gearMachine・filterSplitter。4/5）・`regression/sectionStack` の filterSplitter と特殊族の区画の id（1/9）・`electricPole`(1/1)・`electricToGear`(1/1)・`filterSplitter`(3/3)・`spaceElevator/*`(8/11〈`spaceElevator` 6/9・`shiftRejection` 2/2〉)・C（GearToElectric 7/13＋i18n 1/1・BeltThroughput 3/3・RocketLaunchPad 6/7〈写す 5/6・捨てる 1/1〉・SpaceElevatorSection 5/11＋SpaceElevatorInventory 1/2・Gear 3/3）。計 14 ファイル・宣言 44（写す 43）・ケース 70（写す 69）。並べ比べは 15 状態×2 解像度の 30 セル。録画 `connect/rotary-generator-ui`・`tier9` のロケット 2 本・新規 `uitk/uitk-block-special-panels` |

実行計画 §6.1 へ:

| レーン（計画） | A 番号 | LX 番号 |
|---|---|---|
| S4-special（P/2026-11-10-uitk-s4-special.md） | A-160〜A-164 | LX-126〜LX-130 |

# UITK S4-special（段階 4 画面: ブロックインベントリの特殊族）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Web 版 `features/blockInventory` の特殊族（FilterSplitter・ElectricToGearGenerator・ElectricPole・SpaceElevatorBase・RocketLaunchPad〈題の合図ボタン付き〉の本文 5 つと、SectionStackView に差し込む区画 GearToElectric・BeltThroughput〈`BarSeries`〉・Gear・ElectricNetwork・GearNetwork の 5 つ）を、S4-alpha の拡張口（blockType ごとの本文・題の操作・SectionStackView の固定順の区画）へ「登録するだけ」で載る UITK の View にし、Web と同じ見た目（720p・2560×1080 の並べ比べ）・同じ操作（フィルタのモードと枠・出力モード・接続・建造/発射の合図・ベルトの窓の←/→）を、L0/L1・録画 `connect/rotary-generator-ui`・`tier9/tier9-4-rocket-launch`・`tier9/tier9-4b-rocket-cargo-shift-reject`・新規 `uitk/uitk-block-special-panels` まで確かめて `feature/uitk` へ取り込む。

**Architecture:** 画面レーン（単位エージェント・Editor 要）。外殻（パネル・題・閉じる・自然高のスクロール器）・`blockComponentRegistry` の解決・SectionStackView の並び・`BlockItemGrid`・`LackHighlightText` は S4-alpha が持ち、このレーンは自分のディレクトリ（`Screens/BlockInventory/Special/`・`Client.Ui.State/BlockInventory/Special/`）と自分の登録ファイル（`BlockInventorySpecialGameUiRegistration.cs`）からだけ差し込む。特殊族の詳細（state detail・電力網/歯車網の集約・フィルタの状態・ベルトの搬出統計）は、Web の橋の `BlockDetailDtoBuilder` 系と同じ源を `SpecialBlockDetailReader` が純関数で読み、`SpecialBlockDetailModel`（`OpenBlockDetailTracker`・`BlockNetworkInfoCache`・`BeltSegmentExitStatisticsTable` を購読し、変化ごとに同期で組み直す [A-57]）が不変の `SpecialBlockDetailViewModel` を `IReadOnlyReactiveProperty` で配る。各 View は alpha の `Render(BlockInventoryViewModel)`（スロットの中身）と自分の詳細 Model の両方を `SpecialDetailBinding`（要素がパネルに載っている間だけ購読。前例 `UiScreenPixelScope`・`LocalizedTextBinding`）で受けて描く。操作は `ISpecialBlockCommands`（`UniTask<UiCommandResult>`。前例 `PauseMenuCommands.SubmitBugReport`）を View が直に呼び [A-8]、拒否は `UiCommandResultToaster` へ渡す。表示は Web と同じく楽観更新しない（サーバーの後続の状態で変わる）。見た目は既存の部品（`ModeSwitch`・`ItemSlot`・`GaugeBar`・`BarSeries`・`PanelActionButton`・`UiStack`/`UiGroup`・`LocalizedLabel`）と alpha の `LackHighlightText`・`BlockItemGridGroup` だけで組み、新しい共有部品は作らない。

**Tech Stack:** Unity 6000.3.8f1 UI Toolkit（ランタイムパネル・USS・`AttachToPanelEvent`/`DetachFromPanelEvent`）、UniRx、UniTask、VContainer、Unity Input System（`InputSystem.onEvent`）、NUnit＋`com.unity.ui.test-framework`（`UITestFixture`・`PanelSimulator`）、uloop CLI、プレイテスト DSL（`run-scenario.sh`）、node（Web の参照キャプチャ・test-name-parity・`npm run gen:i18n`）、gh CLI、bd（Beads）

- **パスの略記**: `S/` = `moorestech_client/Assets/Scripts/`、`W/` = `moorestech_web/webui/`、`LOGS` = `../moorestech_logs`（repo の兄弟）、`SKILL` = `.agents/skills/unity-playmode-recorded-playtest`、`SP` = `S/Client.Ui/Screens/BlockInventory/Special`、`SS` = `S/Client.Ui.State/BlockInventory/Special`。Files 節と各コマンドはリポジトリ相対の完全なパスで書く
- **正本**: 裁定 `.decisions/2026-11-06-*`（とくに「段階2以降の画面もWeb版と同じ見た目にする」「長命ブランチの中は全体スイッチ1つ」「UITK切替までCEF由来の不具合は直さない」）、設計書 `docs/uitk-migration/execution/component-architecture.md`（§1.1・§2.4 ModeSwitch/PanelActionButton・§2.5 GaugeBar/BarSeries・§2.7 の「1 feature だけが使うもの」〈`LackHighlightText`・`BlockItemGrid`・`details/rows/*` は blockInventory 内で共有〉・§3.2〜3.4・§4・§5.1、[A-8]・[A-40]・[A-45]・[A-47]・[A-57]・[A-76]・[A-79]・[A-85]）、実行計画 `docs/uitk-migration/execution/execution-plan.md`（§1.4 の S4-special 行・§4.3・§5・§6.2・E-1・E-13・E-19・E-22）。食い違ったら 裁定 → 統合計画 → 設計書 → 実行計画 → この計画 の順で上が勝つ。この計画が設計書・完了条件と違う所は判断記録に書き、設計書に足す行の案は末尾「設計書・実行計画へ反映する行」に置く（指揮が設計書 §7.1 へ写す。A-160〜A-164）
- **レーンの起動は指揮が行う**（実行計画 §4.1。実装は moores-subagent-driven-development でこのホストの Claude Code の subagent が担う）
- **所有の境界**（指揮経由の PLAN-W4a との申し合わせ・2026-11-10）: S4-alpha は `Screens/BlockInventory/` 直下の外殻・`Registry/`・`SectionStack/`（区画の順序 enum を含む）・`Parts/`（`LackHighlightText`・`BlockItemGrid`・rows）・`Client.Ui.State/BlockInventory/Panel/`・`BlockInventoryGameUiRegistration.cs`。S4-special は `Screens/BlockInventory/Special/`・`Client.Ui.State/BlockInventory/Special/`（`Logic/` は L0 済みで使うだけ）・`Client.Starter/Registration/GameUi/BlockInventorySpecialGameUiRegistration.cs`。S4-machine は `Screens/BlockInventory/Machine/`（context〈cleanRoom・yield・入力の詰まり〉・Machine・Miner・Pump・Generator・液体）、S4-train は `Screens/BlockInventory/Train/`。Shaft・Gear・GearBeltConveyor の本文は alpha の既定（SectionStackView）で、このレーンはそこへ区画を差し込むだけ

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
| R-13 | 並べ比べ: 参照 `4-block-electricPole`・`4-block-electricToGear`・`4-block-filterSplitter`・`4-block-gearGenerator`（1280×720・2560×1080）・`4-block-spaceElevator`・`4-block-spaceElevatorClimbing`・`4-block-spaceElevatorInsufficient`・`4-block-spaceElevatorLoading`（2560×1080。720p は自分で撮る）と、参照に無い 3 状態（発射台・ベルト・回転発電機。両解像度を自分で撮る）を UITK と並べ、差の表を `done.md` に書く | 差の表の全行に「同じ／直した／LX-n／捨てる」のどれか。合否の目安は設計書 [A-14]（720p で 2px） |
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
- **使う部品と公開 API**。C2-*・C3-*・C4-b・C4-e・L0-s4b・段階 1 の型は `origin/feature/uitk`（`0a94c9b96b`）の実物から写した。S4-alpha は起草中（PLAN-W4a・PLAN-W4a の worktree の `docs/superpowers/plans/2026-11-10-uitk-s4-alpha.md`）で、この計画の起草の頭と終わりの時点で計画ファイルが無かったため、指揮から伝えられた所有の境界と拡張口 5 つ（本文・題の操作・区画の差し込み・列車の本文・大型パネルの寄与）から**想定 API** を書いた。「マージ後に実物と照合」（Task 1 Step 2 で grep し、名前・形が違えば以後を実物の名前で読み替え、`done.md` の「未確認」に「API 名の読み替え: <想定> → <実物>」と書く。形〈引数・寿命〉が違えば、このレーンの `Special/Registration/` の適合の層だけを直し、View 本体は変えない）

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

- **S4-alpha の API**（出所: S4-alpha 計画 14a85b146c〈`origin/uitk-lane/plan-w4a` の `docs/superpowers/plans/2026-11-10-uitk-s4-alpha.md` 末尾「## 後続レーンへの Produces 一覧」の (1)〜(3)・(6)・画面の部品・パネルの状態〉。レビュー中で名前が変わりうる。マージ後に実物と Task 1 Step 2 で照合する）

```csharp
// ===== S4-alpha（S4-alpha 計画 14a85b146c の Produces 一覧。レビュー中で名前が変わりうる。マージ後に実物と照合）=====
namespace Client.Ui.State.BlockInventory.Panel
{
    // 開いたブロック。このレーンは BlockType・ItemSlots・Block（区画が詳細を読む源。L0/L1 では null）を読む。同じ位置・同じブロックの間は部分を作り直さず Render を繰り返す
    public sealed class OpenBlockInventoryPanel : BlockInventoryPanelState { string BlockType; Guid BlockGuid; BlockId BlockId; Vector3Int Position; IReadOnlyList<SlotItem> ItemSlots; BlockInventoryPanelError Error; BlockInventoryComponents Components; BlockGameObject Block; bool IsSamePanel(OpenBlockInventoryPanel other); }
    // パネルの Model は遷移・スロット更新・追跡の変化・追跡ブロックの状態の変化で組み直す。連続値（網の集約・ベルトの統計）は各区画が自分で購読する
}
namespace Client.Ui.State.BlockInventory.SlotRestriction
{
    // 拡張口 (6): Shift 配分の宛先の束縛の寄与（ロケット・宇宙エレベーターの入れ口は S4-special）
    public interface IBlockSlotRestrictionContributor { bool AppliesTo(OpenBlockInventoryPanel panel); OpenBlockSlotInput Contribute(OpenBlockInventoryPanel panel, OpenBlockSlotInput input); }
}
namespace Client.Ui.Screens.BlockInventory.Registry
{
    // 差し込む部分。同じパネルの間は Render を繰り返し、外すとき Detach を 1 回呼ぶ
    public interface IBlockInventoryPart { VisualElement Root { get; } void Render(OpenBlockInventoryPanel panel); void Detach(); }
    // 拡張口 (1): blockType ごとの本文（Kind は L0-s4b の BlockComponentRegistry が引く。二重登録は起動時に例外）
    public interface IBlockInventoryBodyFactory { BlockInventoryBodyKind Kind { get; } IBlockInventoryPart Create(BlockInventoryBodyMount mount); }
    public sealed class BlockInventoryBodyMount { public bool FillsPanelHeight { get; } }
    // 拡張口 (2): 題の操作（ブロック・エラー無しのときだけ alpha が作る）
    public interface IBlockInventoryTitleActionFactory { BlockInventoryTitleActionKind Kind { get; } IBlockInventoryPart Create(); }
}
namespace Client.Ui.Screens.BlockInventory.SectionStack
{
    // 拡張口 (3): 標準の本文の固定順の区画（列挙の順が描く順。1 Slot に 1 本）
    public enum SectionStackSlot { Context, ItemGrid, Machine, Miner, Pump, Generator, GearToElectric, BeltThroughput, Gear, ElectricNetwork, GearNetwork, FluidDetails, FluidRow }
    public interface IBlockInventorySection : IBlockInventoryPart { bool IsApplicable(OpenBlockInventoryPanel panel); } // alpha は Render の度に IsApplicable で SetItemShown し、真なら Render
    public interface ISectionStackSectionFactory { SectionStackSlot Slot { get; } IBlockInventorySection Create(SectionStackSectionMount mount); }
    public sealed class SectionStackSectionMount { public SectionStackConfig Config { get; } public bool FillsPanelHeight { get; } }
}
namespace Client.Ui.Screens.BlockInventory.Parts
{
    public enum LackHighlightTextSize { Xs, Sm }
    public sealed class LackHighlightText : LocalizedLabel { public const string InsufficientClassName = "lack-highlight-text--insufficient"; public LackHighlightText(string testid, LackHighlightTextSize size); public void Render(UiText text, bool insufficient); }
    public sealed class BlockItemGrid : VisualElement { public void Render(IReadOnlyList<SlotItem> itemSlots, IReadOnlyList<int> slotIndices); } // 枠 testid = "<testId>-slot-<index>"。身振りは alpha の IBlockSlotCommands へ配線済み
    public sealed class BlockItemGridGroup : VisualElement { public BlockItemGridGroup(LocalizationKey label, BlockItemGrid grid); } // ラベル text--sm text--muted・間 4px
    public sealed class BlockItemGridFactory { public BlockItemGrid Create(string testId); } // DI 登録済み（BlockInventoryGameUiRegistration）
}
// パネルの源: Client.Ui.State.BlockInventory.Panel.IBlockInventoryPanelSource { IReadOnlyReactiveProperty<BlockInventoryPanelState> Current }（DI 登録済み）。区画の詳細の源は OpenBlockInventoryPanel.Block（L0/L1 では null。区画側の責務で落ちないこと）
// 呼ばれ方: パネルが変わる（種類・位置・エラーの有無・大きさ）と旧部分の Detach → 新しく Create → Render。同じパネルでは Render だけ
// L1 の土台（alpha）: BlockInventoryTestPanels（Chest()・ChestWithSlots(...)・OpenFailedChest()・Train(error)・OfType(blockType, ...)）・FakePanelSource、Client.Tests/Ui/Screens/BlockInventory/BlockInventoryTestFixtures.cs（BlockInventoryTestBodies）・Client.Tests/Ui/State/BlockInventory/PanelModel/BlockInventoryTestPanels.cs（OfType(blockType, params SlotItem[])。OpenBlockInventoryPanel の internal ctor を呼ぶ）
```

- **層の向き**（設計書 §1.1・`UiLayerDependencyRules`）: `Client.Ui.State/**` は Client.Game・Client.Network・Server.*・Game.* を参照してよい（asmdef。このレーンが `Game.Block`・`Game.Gear` を足す。D-8）。`Client.Ui/Screens/**` は State・Components・Foundation・Content を参照してよく、`Client.Game`・`Game.*` の型を受けない（ViewModel は値だけ運ぶ）。`Screens/BlockInventory/Special/**` は alpha の `Screens/BlockInventory/{Registry,SectionStack,Parts}` を参照してよい（同じ機能の中。設計書 §2.7「blockInventory 内で共有」）
- **AGENTS.md の規約**: 1 ファイル 200 行未満・1 ディレクトリ 10 ファイルまで（.meta は数えない）・`partial`/`Func<>` 禁止・イベントは UniRx・try-catch 禁止（このレーンに外部境界の catch は無い。通信の失敗は `PacketWaitCompletionReason` と応答の値で分岐する）・デフォルト引数禁止・単純な getter/setter プロパティ禁止（`{ get; private set; }` と式本体の読み取り専用は可）・主要処理に「// 日本語 → // English」の 2 行コメント（各 1 行）・メソッド内のローカル関数は `#region Internal`・初期化は `Initialize`・fail-closed の経路はログ（意図した無音は `silent-ok: <理由> / <誰がどこで気づくか>`）・`.meta` は手で作らない・Prefab/シーンを手で書き換えない・`Update()` で毎フレームの同値判定をしない
- **USS の規約**（ref-uitk-design・設計書 §3.2）: 画面 USS `SP/BlockInventorySpecial.uss` はルートのクラスごと（`.electric-pole-view`・`.electric-to-gear-view`・`.filter-splitter`・`.rocket-launch-pad-view`・`.space-elevator-view`・`.special-section`）で始まり、子は `__*`。部品のクラスを書かない（部品の見た目はつまみ）。色は `var()` だけ。`z-index`・構造擬似クラス・`gap`・合成の太字を使わない。USS の先頭に `/* web: <Web の CSS> */` を日英で書く。このレーンの見た目は Mantine の `Stack`/`Group`/`Text` だけなので、間隔は `UiStack`/`UiGroup` の `UiGap`、文字は `UiTypographyClass`、色は `--text-default`・`--text-muted`・`--text-high-contrast`・`--text-insufficient`（`text--*` のクラス）で当て、USS に書くのは Web の局所上書きがある所だけ（無ければ USS は空のルール 1 行にしない。Task 6 Step 3 で要るものだけ書く）
- **字間・色の罠（持ち越し）**: UITK の字間は字の大きさ/100 × 値。このレーンは字間を書かない（Web も `normal`）。部品ギャラリーは sRGB 合成なので、色の比較はランタイムの PanelSettings（Linear 合成）で撮る（Task 10）。暗い半透明の α は I-fix1 で指数 1.85 に選び直し済み、明るい色の α は未実測（このレーンは半透明を足さない）
- **テストの置き場と名前**: `S/Client.Tests/Ui/Screens/BlockInventory/Special/`（区画・本文の L1）・`S/Client.Tests/Ui/State/BlockInventory/Special/`（L0）・`S/Client.Tests/Input/ArrowKeys/`（矢印キー）、namespace はディレクトリに合わせる。テスト名は日本語の挙動文。Web から写したものは `[Test]` の直前に `/// <web-test file="<W/ からの相対パス>"><it/test の第 1 引数></web-test>`（describe は含めない。`it.each`・`for` の中の `test` は第 1 引数の書式文字列そのまま〈`${…}`・`%s` を展開しない〉。同じタグの NUnit が複数あってよい。実行計画 E-13・W0-tools 書式）
- **コマンド**: コンパイル `uloop compile --project-path ./moorestech_client`（期待: ErrorCount 0）。テスト `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。このレーンのテストフィルタ（以下 `$FILTER`）は `^Client\.Tests\.(Ui\.(Screens\.BlockInventory|State\.BlockInventory|State\.PauseMenuCommandsTest|Conventions|Theme|Layers|Tooltip)|Input\.ArrowKeys)\.`（`PauseMenuCommandsTest` は良性の一覧を固定するので名指し。alpha・S4-machine・S4-train の BlockInventory のテストも同じフィルタに入り、取り込み後の回帰を見る）。ドメインリロード中のエラーは 45 秒待って再試行
- **共有ファイルの所有**（実行計画 E-1）: このレーンが書く共有ファイルは、`S/Client.Starter/Registration/MainGameUiRegistration.cs` の画面登録区画の 1 行、`moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss` の `Screens` 区画の `@import` 1 行、`S/Client.Ui.State/Client.Ui.State.asmdef` の `references` の末尾 4 要素（`Game.Block`・`Game.Gear`・`Core.BeltTransport`・`Core.Update`。D-8）、`S/Client.Ui.State/Commands/UiCommandRejectReason.cs` の共有区画（enum と 3 つの switch の末尾に 4 値）と良性の一覧を固定する `S/Client.Tests/Ui/State/PauseMenuCommandsTest.cs` の「良性の拒否理由は…」1 件、`Localization/localization.csv` の `ui.commandRejected.*` の群の末尾 4 行と再生成した `W/src/shared/i18n/generated/localizationKeys.ts`（`npm run gen:i18n`。台帳「## 追従記録」へ 1 行）、`docs/uitk/TODO.md` の「## 実測の結論」「## 見た目の例外」（`LX-126`〜`LX-130`）の末尾、`docs/uitk/disposition.md` の「## 処遇」の末尾 1 行（D-13）。シナリオ `SKILL/scenarios/uitk/uitk-block-special-panels.cs` はこのレーンが持つ。既存の `connect/rotary-generator-ui.cs`・`tier9/tier9-4-rocket-launch.cs`・`tier9/tier9-4b-rocket-cargo-shift-reject.cs` は書き換えない（GameUi で通すだけ。落ちたら Task 11 の分岐）
- **画面専用の部品の置き場**（設計書 §5.1-2）: このレーンの View・適合の層はすべて `SP/` の下（族ごとのサブディレクトリ）。新しい汎用部品は作らない（D-18）。矢印キーの流れ `InputSystemArrowKeyStream` は `Client.Ui` が Input System を参照しないため `S/Client.Input/ArrowKeys/` に新規ファイルで置く（前例 C4-e の `Client.Input/Pointer/InputSystemPointerButtonStream.cs`・E-19 の「最初の使い手が小さな基盤を持つ」。D-7）
- **新しい共有部品・口を見つけたときの手順**: 作業中に「別の画面でも要る部品」「alpha の口に無い差し込み」を見つけたら作らない。`LOGS/harness/uitk/S4-special/done.md` の「## integrator への依頼」に「部品名／Web の正本のパス／使う画面／このレーンで作らない理由」を 1 行で書き、`bd note <bd> "<同じ 1 行>"` にも残して先へ進む。既知の依頼（Task 1 Step 3 で雛形に書く）: 無し（Task 1 Step 2 の照合で増えたらそこで書く）
- **参照キャプチャ**: `LOGS/uitk/reference-captures/web-a9d5123537/`（1280×720）・`web-0e81bbe3c3-2560x1080/`（2560×1080）の `4-block-electricPole.png`・`4-block-electricToGear.png`・`4-block-filterSplitter.png`・`4-block-gearGenerator.png`（SimpleGearGenerator の fixture。このレーンの Gear と GearNetwork の 2 区画だけが出る）、2560×1080 だけにある `4-block-spaceElevator{,Climbing,Insufficient,Loading,OutputBlocked,Reordered}.png`。参照に無い状態（720p の宇宙エレベーター 6 枚・発射台・ベルト・回転発電機）は Task 10 Step 1 で master の使い捨て worktree の mock-host に一時の fixture を足して撮る（E-22・D-15）
- **並べ比べの画像の原本**は worktree の gitignore 下 `moorestech_client/.uloop/outputs/compare/S4-special/` に置き、`moorestech_logs` へ入れない（スクショ 3 枚程度まで）。`moorestech_logs` へ書く前に `LOGS/README.md` を読む
- **報告**: `LOGS/harness/uitk/S4-special/done.md`（1 行目「状態: 完了」。途中で止まるときは「状態: 停止（理由）」）。節は「証拠」「差の表」「要裁定」「integrator への依頼」「未確認」「部分移植の残り」

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 未実施
- Phase 2.6（型閉包・重複・ADR矛盾）: 未実施

---

## 死活表（Phase 2.5）

全体スイッチ UITK（`UiInputExclusivity.ActiveUiSystem == GameUi`）で起動したとき、Web の系で動いていた特殊族の振る舞いが生きるか。Web の橋（`BlockInventoryTopic`・`BlockDetailDtoBuilder` 系・`FilterSplitter*ActionHandler`・`ElectricToGearSetOutputModeActionHandler`・`SpaceElevatorConnectActionHandler`・`RocketLaunchPadCommandActionHandler`）は Web の系でだけ登録され、源（`OpenBlockDetailTracker`・`BlockNetworkInfoCache`・`BeltTransportClientCache`・`SubInventoryState`）は両系で登録済み。

| 振る舞い | GameUi で生きるか | 根拠 |
|---|---|---|
| フィルタ分配器の方向ごとのモード切り替え | 生きる | `FilterSplitterBodyView` の `ModeSwitch` → `ISpecialBlockCommands.SetFilterMode` → `SendFilterSplitterStateRequest` → 応答を `BlockNetworkInfoCache.ApplyFilterSplitterSnapshot` → `OnUpdated` → Model が組み直す（Task 4・7） |
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
| 合図の重ね押し・接続済みの再押下が誤報にならない | 生きる | 良性の拒否 `SpecialBlockCommandStale`（Task 4） |
| 特殊族の Web の CEF 側の表示 | GameUi では描かない（裁定どおり） | 全体スイッチ 1 つ（`.decisions/2026-11-06-長命ブランチの中は全体スイッチ1つ`）。Web の系は変えない |

---

## File Structure

### 所有するファイル（このレーンが作る）

State（`SS` = `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special`）:

| パス | 責務 |
|---|---|
| `SS/ISpecialBlockDetailModel.cs` | 詳細の Model の口（`IReadOnlyReactiveProperty<SpecialBlockDetailViewModel> Current`） |
| `SS/SpecialBlockDetailModel.cs` | 源の購読と同期の組み直し（`IInitializable`） |
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
| `SS/Source/SpecialInletBindingContributor.cs` | 発射台・宇宙エレベーターの入れ口（alpha の Shift の口の実装） |
| `SS/Commands/ISpecialBlockCommands.cs` | 操作の口 |
| `SS/Commands/SpecialBlockCommands.cs` | 開いたブロックの確かめ・掴みの読み・送信・結果の写し |
| `SS/Commands/IOpenSpecialBlock.cs` | 開いたブロックの位置・種別・インスタンス（`OpenSpecialBlock`）を読む口 |
| `SS/Commands/SubInventoryOpenSpecialBlock.cs` | `SubInventoryState` からの実装 |
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
| `SP/Belt/BeltThroughputSectionView.cs` | 区画 BeltThroughput（`belt-throughput-section`） |
| `SP/Belt/BeltWindowCursor.cs` | 選んだ窓の tick（既定 180・←/→ で巡回・区画がパネルから外れたら既定へ） |
| `SP/FilterSplitter/FilterSplitterBodyView.cs` | 本文 FilterSplitter（`filter-splitter`） |
| `SP/FilterSplitter/FilterDirectionView.cs` | 方向 1 つ（`filter-direction-{d}`） |
| `SP/Space/RocketLaunchPadBodyView.cs` | 本文 RocketLaunchPad（`rocket-launch-pad-view`/`-loading`） |
| `SP/Space/RocketLaunchPadTitleActionView.cs` | 題の操作（`rocket-launch-pad-build`/`-launch`） |
| `SP/Space/SpaceElevatorBodyView.cs` | 本文 SpaceElevatorBase（`space-elevator-view`） |
| `SP/Space/SpaceElevatorSectionView.cs` | 宇宙エレベーターの状態区画（`space-elevator-section`/`-loading`） |
| `SP/SpecialViewParts.cs` | View が共通に使う小さな手続き（文字の行・表示の出し入れ・表示中の判定） |
| `SP/Registration/ISpecialDetailRenderer.cs` | View が実装する描画の口 `Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)` |
| `SP/Registration/SpecialDetailAdapter.cs` | alpha の本文・題の操作・区画の 3 つの口を 1 つで実装し、詳細の Model を要素の寿命で購読する |
| `SP/Registration/SpecialViewKit.cs` | View が受ける依存の束（Model・Commands・表示素材・トースト・矢印キー・alpha の格子の工場） |
| `SP/Registration/SpecialBodyFactories.cs` | 本文 5・題の操作 1 の工場（alpha の口の実装） |
| `SP/Registration/SpecialSectionFactories.cs` | 区画 5 の工場（alpha の口の実装） |

入力（E-19・D-7）: `moorestech_client/Assets/Scripts/Client.Input/ArrowKeys/InputSystemArrowKeyStream.cs`（←/→ の押下の縁を流す）と同じディレクトリの `IHorizontalArrowKeySource.cs`（口 `IObservable<HorizontalArrowKey> OnArrowPressed` と `enum HorizontalArrowKey { Left, Right }`。区画のテストは偽物に差し替える）

登録: `moorestech_client/Assets/Scripts/Client.Starter/Registration/GameUi/BlockInventorySpecialGameUiRegistration.cs`

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
| `…/Special/NetworkSectionViewTest.cs` | L1 | 電力網・歯車網（Web の写しなし。区画の表示を固定） |
| `…/Special/ElectricBodyViewTest.cs` | L1 | `electricPole.spec.ts` 1・`electricToGear.spec.ts` 1 |
| `…/Special/FilterSplitterBodyViewTest.cs` | L1 | `filterSplitter.spec.ts` 3 |
| `…/Special/RocketLaunchPadViewTest.cs` | L1 | `RocketLaunchPadInventory.test.tsx` 5 |
| `…/Special/SpecialFactoriesTest.cs` | L1 | 工場の Kind・Slot と根の testid |
| `…/Special/SpaceElevatorViewTest.cs` | L1 | `SpaceElevatorInventory.test.tsx` 1・`SpaceElevatorSection.test.tsx` 5・`spaceElevator.spec.ts` 6 |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-block-special-panels.cs` | 録画 | 新規（R-12） |

（`…` = `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory`。ディレクトリのファイル数: `SS/Details` 8・`SS/Source` 5・`SS/Commands` 7・`SS/Text` 2・`SS` 直下 2、`SP/*` は各 2〜5、`Client.Tests/Ui/State/BlockInventory/Special` 9、`…/Special` 9＋`Support/` 5、`SP/Registration` 6。どれも 10 以下）

### 共有ファイル（共有区画だけ書く）

| パス | 書く所 |
|---|---|
| `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiRegistration.cs` | 画面登録区画に `BlockInventorySpecialGameUiRegistration.Register(builder);` 1 行（alpha の行の後） |
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

| Web のファイル（`W/` からの相対） | 件数 | UITK のテスト | 処遇 |
|---|---|---|---|
| `e2e/tests/block/electric/electricPole.spec.ts` | 1 | `ElectricBodyViewTest` | 写す |
| `e2e/tests/block/electric/electricToGear.spec.ts` | 1 | `ElectricBodyViewTest` | 写す |
| `e2e/tests/block/filterSplitter.spec.ts` | 3 | `FilterSplitterBodyViewTest` | 写す（3 件目は alpha の外殻のスクロールに載せる。外殻の土台が無ければ部分移植） |
| `e2e/tests/block/spaceElevator/spaceElevator.spec.ts` | 6 | `SpaceElevatorViewTest` | 写す（身振り→絶対添字まで。持ち物の移動の結果は alpha・録画） |
| `e2e/tests/block/spaceElevator/shiftRejection.spec.ts` | 2 | `SpecialInletBindingContributorTest` | 写す（判断まで。送らない・通知は alpha の Shift の実行器） |
| `src/features/blockInventory/details/__tests__/GearToElectricSection.test.ts` | 7 | `GearToElectricSectionViewTest` | 写す |
| `src/features/blockInventory/details/__tests__/GearToElectricSection.i18n.test.ts` | 1 | `GearToElectricSectionViewTest` | 写す（D-14） |
| `src/features/blockInventory/details/__tests__/BeltThroughputSection.test.ts` | 3 | `BeltThroughputSectionViewTest` | 写す |
| `src/features/blockInventory/details/__tests__/GearSection.test.ts` | 3 | `GearSectionViewTest` | 写す |
| `src/features/blockInventory/views/__tests__/RocketLaunchPadInventory.test.tsx` | 6 | `RocketLaunchPadViewTest` 5 | 写す 5・捨てる 1（"accepts panel data and narrows inside the dedicated view"。TS の型検査。D-13） |
| `src/features/blockInventory/details/spaceElevator/__tests__/SpaceElevatorInventory.test.tsx` | 1 | `SpaceElevatorViewTest` | 写す |
| `src/features/blockInventory/details/spaceElevator/__tests__/SpaceElevatorSection.test.tsx` | 5 | `SpaceElevatorViewTest` | 写す |

計 39 件（写す 38・捨てる 1）。実行計画 §1.4 の件数（Rocket 5・SpaceElevator 5）は `it.each` の行を数えていない。実物の件数は Rocket 6・SpaceElevatorSection 5＋SpaceElevatorInventory 1（末尾「設計書・実行計画へ反映する行」で §1.4 の行を直す）

---

## Tasks

### Task 1: 前提の確認・alpha の拡張口の照合・記録の準備

**Files:**
- Create: `../moorestech_logs/harness/uitk/S4-special/done.md`（雛形。moorestech_logs 側）

**Interfaces:**
- Consumes: `origin/feature/uitk`（S4-alpha・C4-b・C3-b・L0-s4b・C4-e マージ済み）、Global Constraints の「使う部品と公開 API」「S4-alpha の想定 API」
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
ls $S/Client.Starter/Registration/GameUi/*.cs | wc -l
```
Expected: worktree の絶対パス。部品の 5 行・`HoverTooltipMark` の 2 行・L0-s4b の 4 行・`PlayerGrabModel.Current` が Global Constraints のとおり出る。`BeltTransportClientCache` が両系の登録（`MainGameModelRegistration` か同等）に 1 行ある。`/* == section: Screens == */` が出る。
- どれかのファイルが無ければ、その依存が未取り込みなので止め、`done.md` を「状態: 停止（<依存> 未取り込み）」にして指揮へ報告する
- `BeltTransportClientCache` が GameUi の系で登録されていなければ止めて指揮へ（源は両系の登録の持ち物で、このレーンは書かない）
- `Registration/GameUi/*.cs` が 10 本以上（alpha の登録を含む）なら、このレーンの 1 本で 11 本以上になる。止めずに作業を進め、`done.md` の「integrator への依頼」に「`Registration/GameUi/` の分け方」を書く。再編が `origin/feature/uitk` に入り、10 本以下の置き場へ足せるまで Task 14 のマージをしない（AGENTS.md の 1 ディレクトリ 10 ファイル。S3-hotbar-modal と同じ扱い。D-20）。再編後は File Structure と Task 9 の登録のパスを読み替え、読み替えたことを `done.md` に書く

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
grep -rn "OnApplicabilityChanged\|RequestRelayout\|ApplicabilityChanged" $S/Client.Ui/Screens/BlockInventory/SectionStack/
ls $S/Client.Tests/Ui/Screens/BlockInventory/ $S/Client.Tests/Ui/State/BlockInventory/PanelModel 2>/dev/null
grep -n "BlockInventoryGameUiRegistration" $S/Client.Starter/Registration/MainGameUiRegistration.cs
```
Expected: Global Constraints の「S4-alpha の API」の口が出る。読み替えの規則:
- **名前だけ違う**: 以後の全タスクを実物の名前で読む。`done.md` の「未確認」に「API 名の読み替え: <計画の名前> → <実物>」を 1 行ずつ書く
- **形が違う**（例: `Render` の引数が `OpenBlockInventoryPanel` でない、`Detach` が無い）: `SP/Registration/SpecialDetailAdapter.cs`・`SpecialBodyFactories.cs`・`SpecialSectionFactories.cs` だけを実物の形に合わせる（View と State は変えない。View は `ISpecialDetailRenderer` しか知らない）。合わせ方を `done.md` の「未確認」に書く
- **口そのものが無い**（拡張口 (1)〜(3) のどれか）: 止めて指揮へ（alpha の所有物で、このレーンは書かない）。`done.md` を「状態: 停止（S4-alpha の拡張口 <n> が無い）」にする
- **分岐 A（拡張口 (6) `IBlockSlotRestrictionContributor` がある。alpha の計画どおり）**: Task 3 の `SpecialInletBindingContributor` をその口に実装し、Task 9 で DI に登録する
- **分岐 B（拡張口 (6) が無い）**: Task 3 の `SpecialInletBindingContributor` から `: IBlockSlotRestrictionContributor` と `AppliesTo`・`Contribute` を外し、静的な `TryGetInletBindings`・`SpaceElevatorBindings` だけの `static class` にして、Task 9 では登録しない。`done.md` の「integrator への依頼」に「S4-alpha の Shift の束縛へ発射台・宇宙エレベーターの入れ口を寄与する口が無い。実装は `Client.Ui.State/BlockInventory/Special/Source/SpecialInletBindingContributor.cs` にある。それまで両ブロックの Shift は取得待ち（`BlockDetailLoading`）のまま」と書く。R-9 の受け入れ基準は L0（判断まで）で満たし、死活表の行を「alpha の口の追加後に生きる」と読む（D-17）
- **区画の出し入れの合図**: alpha の `SectionStackBody` は `IsApplicable` をパネルの `Render` のときにしか聞かない。網の集約・ベルトの統計はパネルの組み直しの外で届くので、このレーンの区画は届いた時に自分で `display` を切り替える（D-23）。alpha に区画から出し入れのやり直しを求める口（例 `IObservable<Unit> OnApplicabilityChanged`）が入っていれば、`SpecialDetailAdapter` の `Draw` で出し入れが変わったときにそれへ流す形に読み替え、自分で `display` を触る行を消す
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
    public sealed class RocketLaunchPadDetail { public RocketLaunchPadDetail(RocketLaunchPadPhase phase, RocketLaunchPadWaitReason waitReason, float progress, IReadOnlyList<int> materialSlots, IReadOnlyList<int> payloadSlots, int outputSlotStart); /* 同名の get-only */ public IReadOnlyList<int> OutputSlots(int itemSlotCount); }
    public sealed class SpaceElevatorDetail { public SpaceElevatorDetail(IReadOnlyList<int> satelliteInputSlots, IReadOnlyList<int> outputSlots, bool connected, int cablePoolAmount, int cablesPerElevator, float climbProgress, SpaceElevatorBaseStatus status); /* 同名の get-only */ }
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
using Game.Block.Interface.State;

namespace Client.Ui.State.BlockInventory.Special.Details
{
    /// <summary>
    /// 発射台の詳細。資材・積荷の枠はサーバーの絶対添字、出力は先頭から在庫の末尾まで（Web RocketLaunchPadDetailDtoBuilder と同じ）
    /// Launch pad detail; material and payload slots are absolute server indices, outputs run from the start to the inventory end (same as the Web builder)
    /// </summary>
    public sealed class RocketLaunchPadDetail
    {
        public RocketLaunchPadPhase Phase { get; }
        public RocketLaunchPadWaitReason WaitReason { get; }
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
        public SpaceElevatorBaseStatus Status { get; }

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
- Consumes: Task 2 の Details 一式、`OpenBlockDetailTracker`・`BlockNetworkInfoCache`・`BeltTransportClientCache`（実物）、L0-s4b の `InletSlotBinding`・`BlockSlotRestrictionInput`・`BlockSlotRestrictionResolver`、S4-alpha の拡張口 (6) `IBlockSlotRestrictionContributor`・`OpenBlockInventoryPanel`（`BlockType`・`Block`）・L0-s4b の `OpenBlockSlotInput.WithInletBindings`（Task 1 Step 2 で実物と照合）
- Produces:
```csharp
namespace Client.Ui.State.BlockInventory.Special
{
    public interface ISpecialBlockDetailModel { IReadOnlyReactiveProperty<SpecialBlockDetailViewModel> Current { get; } }
    public sealed class SpecialBlockDetailModel : ISpecialBlockDetailModel, IInitializable { public SpecialBlockDetailModel(OpenBlockDetailTracker tracker, BlockNetworkInfoCache networkCache, BeltTransportClientCache beltCache); }
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
        public bool AppliesTo(OpenBlockInventoryPanel panel)
        {
            return panel.BlockType == BlockMasterElement.BlockTypeConst.RocketLaunchPad || panel.BlockType == BlockMasterElement.BlockTypeConst.SpaceElevatorBase;
        }

        public OpenBlockSlotInput Contribute(OpenBlockInventoryPanel panel, OpenBlockSlotInput input)
        {
            // silent-ok: 状態の未着は寄与しない（L0-s4b が入れ口固定のブロックを取得待ちへ倒す） / alpha の Shift が取得待ちの通知（状態の取得を待っています）を出して気づく
            return TryGetInletBindings(panel.Block, out var bindings) ? input.WithInletBindings(bindings) : input;
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
（`BlockTypeConst.RocketLaunchPad`・`SpaceElevatorBase` の定数名は生成物の実物に合わせる。`Client.Ui.State.asmdef` は alpha が `Client.Game` を参照済み〈`OpenBlockInventoryPanel.Block` が `BlockGameObject`〉。L1/L0 では `panel.Block` が null なので、`Contribute` は L0 で呼ばず、判断は `SpaceElevatorBindings` と L0-s4b の `Resolve` で見る〈下のテスト〉。本番の `Contribute` は録画〈R-12 の Shift の段〉で見る）

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
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Source;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// 追跡ブロック・状態・集約・ベルトの窓の進みのたびに同期で詳細を組み直す（間引かない [A-57]。Web BlockInventoryTopic の再発行と同じ契機）
    /// Rebuilds the details synchronously on every tracked block, state, aggregate and belt-window change (no thinning [A-57]; same triggers as Web BlockInventoryTopic)
    /// </summary>
    public sealed class SpecialBlockDetailModel : ISpecialBlockDetailModel, IInitializable
    {
        private readonly OpenBlockDetailTracker _tracker;
        private readonly BlockNetworkInfoCache _networkCache;
        private readonly BeltTransportClientCache _beltCache;
        private readonly ReactiveProperty<SpecialBlockDetailViewModel> _current = new(SpecialBlockDetailViewModel.Empty);

        public IReadOnlyReactiveProperty<SpecialBlockDetailViewModel> Current => _current;

        public SpecialBlockDetailModel(OpenBlockDetailTracker tracker, BlockNetworkInfoCache networkCache, BeltTransportClientCache beltCache)
        {
            _tracker = tracker;
            _networkCache = networkCache;
            _beltCache = beltCache;
        }

        public void Initialize()
        {
            // 変化を起こす源を購読し、そのたびに組み直す
            // Subscribe to every source of change and rebuild each time
            _tracker.OnTrackedBlockChanged.Subscribe(_ => Rebuild());
            _tracker.OnTrackedBlockStateChanged.Subscribe(_ => Rebuild());
            _networkCache.OnUpdated.Subscribe(_ => Rebuild());
            _beltCache.ExitStatistics.OnWindowAdvanced.Subscribe(_ => Rebuild());
            Rebuild();
        }

        private void Rebuild()
        {
            // 閉じている間は空
            // Empty while nothing is open
            var block = _tracker.TrackedBlock;
            _current.Value = block == null ? SpecialBlockDetailViewModel.Empty : SpecialBlockDetailReader.Read(block, _networkCache, _beltCache);
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
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special/Commands/SubInventoryOpenSpecialBlock.cs`
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
- Consumes: Task 3 の `SpecialNetworkReader.ToServerMode`、L0-s4b の `FilterSplitterLogic.ClickAction`・`FilterSlotClickAction`・`RocketCommand`、`PlayerGrabModel.Current`、`BlockNetworkInfoCache.ApplyFilterSplitterSnapshot`、`ClientContext.VanillaApi.Response.Block` の 4 つ（Global Constraints）、`SubInventoryState.CurrentSubInventorySource`・`BlockSubInventorySource.TryGetBlockIdentity(out Guid, out string, out BlockInstanceId)`
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

`Commands/SubInventoryOpenSpecialBlock.cs`:
```csharp
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;

namespace Client.Ui.State.BlockInventory.Special.Commands
{
    /// <summary>
    /// 開いたサブインベントリのブロックの識別を読む（Web の各 ActionHandler と同じ入口）
    /// Reads the identity of the open sub-inventory block (same entry as the Web action handlers)
    /// </summary>
    public sealed class SubInventoryOpenSpecialBlock : IOpenSpecialBlock
    {
        private readonly SubInventoryState _subInventoryState;

        public SubInventoryOpenSpecialBlock(SubInventoryState subInventoryState)
        {
            _subInventoryState = subInventoryState;
        }

        public bool TryGet(out OpenSpecialBlock block)
        {
            block = default;
            if (_subInventoryState.CurrentSubInventorySource is not BlockSubInventorySource source) return false;
            if (!source.TryGetBlockIdentity(out _, out var blockType, out var instanceId)) return false;
            block = new OpenSpecialBlock(source.BlockPosition, blockType, instanceId);
            return true;
        }
    }
}
```
（偽の 2 経路は呼び手 `SpecialBlockCommands` が `SpecialBlockNotOpen` の拒否にし、`UiCommandResultToaster` が LogWarning とトーストを出す。ここではログを出さない）

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

### Task 6: 区画 5 つ・適合の層・L1 の土台

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Registration/ISpecialDetailRenderer.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Registration/SpecialDetailAdapter.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Network/ElectricNetworkSectionView.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Network/GearNetworkSectionView.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Energy/GearSectionView.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Energy/GearToElectricSectionView.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Belt/BeltWindowCursor.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Belt/BeltThroughputSectionView.cs`
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
- Consumes: Task 2 の Details・`SpecialDetailTexts`、Task 3 の `ISpecialBlockDetailModel`、Task 5 の `IHorizontalArrowKeySource`、L0-s4b の `GearRowDisplay`・`BlockDetailStateDisplay`・`BeltThroughputLogic`、部品 `BarSeries`・`HoverTooltipMark`・`UiStack`・`LocalizedLabel`・`UiTypographyClass`、S4-alpha の `LackHighlightText`・`LackHighlightTextSize`・`OpenBlockInventoryPanel`・`IBlockInventorySection`（`IBlockInventoryPart`）（Global Constraints「S4-alpha の API」。Task 1 Step 2 の読み替え）
- Produces:
```csharp
namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    public interface ISpecialDetailRenderer
    {
        bool HasDetail(SpecialBlockDetailViewModel detail);   // 区画は自分の詳細の有無、本文・題の操作は常に true
        void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail);
    }
    public sealed class SpecialDetailAdapter : IBlockInventorySection   // 本文・題の操作・区画の 3 つの口に共通（IBlockInventorySection は IBlockInventoryPart）
    {
        public SpecialDetailAdapter(VisualElement root, ISpecialDetailRenderer renderer, ISpecialBlockDetailModel model);
        public VisualElement Root { get; }
        public bool IsApplicable(OpenBlockInventoryPanel panel);
        public void Render(OpenBlockInventoryPanel panel);
        public void Detach();
    }
}
namespace Client.Ui.Screens.BlockInventory.Special
{
    public static class SpecialViewParts
    {
        public static LocalizedLabel Label(string testid, params string[] classes); // 文字の大きさ・色のクラスを付けた LocalizedLabel
        public static void SetShown(VisualElement element, bool shown);              // display の出し入れ
        public static bool IsDisplayedInHierarchy(VisualElement element);           // パネルに載り、祖先まで display:none でない
    }
}
// 区画: ElectricNetworkSectionView()・GearNetworkSectionView()・GearSectionView()・GearToElectricSectionView()・BeltThroughputSectionView(IHorizontalArrowKeySource arrowKeys)
// どれも VisualElement かつ ISpecialDetailRenderer。詳細が無ければ自分を display:none にする（Web の return null。パネルの組み直しの外で詳細が届いた時のため。D-23）
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
using Client.Ui.Content;
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

        [SetUp]
        public void SetUpSpecialHost()
        {
            Model = new FakeSpecialBlockDetailModel();
            Commands = new RecordingSpecialBlockCommands();
            ArrowKeys = new FakeArrowKeys();
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
（`MoorestechServerDIContainerGenerator`・`TestModDirectory` の using は `GameContentPresentationTest.cs` の実物に合わせる）

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
            var view = Mount(new GearToElectricSectionView());
            view.Render(NoSlots, SpecialBlockDetailViewModel.Empty);
            Assert.IsFalse(IsShown(view));
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
    /// ベルトの流量: 既定 180tick の窓・分間・直近 9 秒の棒。←/→ で窓を巡回し端で止まる（Web BeltThroughputSection の vitest）
    /// Belt throughput: default 180-tick window, per-minute row and nine recent bars; ←/→ cycle the window and stop at the ends (Web vitest)
    /// </summary>
    public class BeltThroughputSectionViewTest : SpecialScreenTestHost
    {
        /// <web-test file="src/features/blockInventory/details/__tests__/BeltThroughputSection.test.ts">renders nothing without belt data</web-test>
        [Test]
        public void ベルトの詳細が無ければ何も出さない()
        {
            var view = Mount(new BeltThroughputSectionView(ArrowKeys));
            view.Render(NoSlots, SpecialBlockDetailViewModel.Empty);
            Assert.IsFalse(IsShown(view));
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/BeltThroughputSection.test.ts">shows the 180-tick window and nine per-second bars by default</web-test>
        [Test]
        public void 既定は180tickの窓と9本の棒()
        {
            var view = Render();
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
            var view = Render();
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
        public void 入力欄にフォーカスがある間とパネルから外れた後は切り替えない()
        {
            var view = Render();
            var before = TextOf(view, "belt-throughput-window");
            UiInputExclusivity.SetGameUiTextInputFocused(true);
            ArrowKeys.Press(HorizontalArrowKey.Right);
            UiInputExclusivity.SetGameUiTextInputFocused(false);
            Assert.AreEqual(before, TextOf(view, "belt-throughput-window"));

            view.RemoveFromHierarchy();
            ArrowKeys.Press(HorizontalArrowKey.Right);
            Mount(view);
            view.Render(NoSlots, SpecialDetailSamples.Only(SpecialDetailSamples.Belt));
            Assert.AreEqual(before, TextOf(view, "belt-throughput-window"), "外れた後は既定の窓へ戻る");
        }

        private BeltThroughputSectionView Render()
        {
            var view = Mount(new BeltThroughputSectionView(ArrowKeys));
            view.Render(NoSlots, SpecialDetailSamples.Only(SpecialDetailSamples.Belt));
            return view;
        }
    }
}
```
（`UiInputExclusivity.SetGameUiTextInputFocused` は GameUi の系でだけ効く実物。テストの系が Web なら `UiInputExclusivity.IsTextInputFocused` が立たないので、C2-b の L1〈`GameTextField` のテスト〉が系を切り替える書き方に合わせる。合わせられなければこの件は `SpecialViewParts` を通さず `BeltThroughputSectionView` の判定を `IsTextInputFocused` だけの小さな関数に分け、その関数を L0 で見る）

`NetworkSectionViewTest.cs`:
```csharp
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.Screens.BlockInventory.Special.Network;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Text;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special
{
    /// <summary>
    /// 電力網・歯車網の区画（Web NetworkSections）と、適合の層が作ってから Detach まで詳細を購読すること
    /// Electric and gear network sections (Web NetworkSections) and the adapter subscribing to details from creation until Detach
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
        public void 適合の層はパネルの組み直しの外で届いた詳細でも描き直しDetachの後は描かない()
        {
            var view = new ElectricNetworkSectionView();
            var adapter = new SpecialDetailAdapter(view, view, Model);
            Mount(adapter.Root);
            Assert.IsFalse(adapter.IsApplicable(null));

            Model.Set(SpecialDetailSamples.Only(SpecialDetailSamples.PoleNetwork));
            Assert.IsTrue(adapter.IsApplicable(null));
            Assert.IsTrue(IsShown(view), "網の集約が後から届いても区画が出る");

            adapter.Detach();
            Model.Set(SpecialBlockDetailViewModel.Empty);
            Assert.AreEqual(Resolved(SpecialDetailTexts.ElectricNetworkSummary(SpecialDetailSamples.PoleNetwork)), TextOf(view, "electric-network-summary"), "Detach の後は購読しない");
        }
    }
}
```

- [ ] **Step 3: 実装を書く**

`Registration/ISpecialDetailRenderer.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;

namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    // 特殊族の View が実装する描画の口（自分の詳細の有無と、スロットの中身・特殊族の詳細での描画）
    // Rendering port the special views implement: whether their detail exists, and drawing from slot contents and special details
    public interface ISpecialDetailRenderer
    {
        bool HasDetail(SpecialBlockDetailViewModel detail);
        void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail);
    }
}
```

`Registration/SpecialDetailAdapter.cs`:
```csharp
using System;
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
    /// S4-alpha の本文・題の操作・区画の口を 1 つで実装し、alpha のスロットの中身と特殊族の詳細の Model を合わせて View へ渡す。Model は作ってから Detach まで購読する
    /// Implements S4-alpha's body, title-action and section ports in one, combining alpha's slot contents with the special detail model; the model is subscribed from creation until Detach
    /// </summary>
    public sealed class SpecialDetailAdapter : IBlockInventorySection
    {
        private readonly ISpecialDetailRenderer _renderer;
        private readonly ISpecialBlockDetailModel _model;
        private readonly IDisposable _subscription;
        private IReadOnlyList<SlotItem> _itemSlots = Array.Empty<SlotItem>();

        public VisualElement Root { get; }

        public SpecialDetailAdapter(VisualElement root, ISpecialDetailRenderer renderer, ISpecialBlockDetailModel model)
        {
            Root = root;
            _renderer = renderer;
            _model = model;

            // 網の集約・ベルトの統計はパネルの組み直しの外で届くので、詳細の変化でも描き直す（alpha の「連続値は区画が自分で購読」）
            // Network aggregates and belt stats arrive outside panel rebuilds, so redraw on detail changes too (alpha: sections subscribe their own continuous values)
            _subscription = _model.Current.Subscribe(Draw);
        }

        public bool IsApplicable(OpenBlockInventoryPanel panel)
        {
            return _renderer.HasDetail(_model.Current.Value);
        }

        public void Render(OpenBlockInventoryPanel panel)
        {
            _itemSlots = panel.ItemSlots;
            Draw(_model.Current.Value);
        }

        public void Detach()
        {
            _subscription.Dispose();
        }

        private void Draw(SpecialBlockDetailViewModel detail)
        {
            _renderer.Render(_itemSlots, detail);
        }
    }
}
```
（alpha の題の操作・本文の口は `IBlockInventoryPart` を返す。`IBlockInventorySection` はそれを継ぐので、同じ適合の層を 3 つの口に渡せる。本文・題の操作では `IsApplicable` を alpha が呼ばない）

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
            // 集約が無ければ区画ごと出さない
            // Hide the whole section without an aggregate
            var present = detail.TryGetElectricNetwork(out var network);
            SpecialViewParts.SetShown(this, present);
            if (!present) return;

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
            var present = detail.TryGetGearNetwork(out var network);
            SpecialViewParts.SetShown(this, present);
            if (!present) return;

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
            var present = detail.TryGetGear(out var gear);
            SpecialViewParts.SetShown(this, present);
            if (!present) return;
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
            var present = detail.TryGetGearToElectric(out var generator);
            SpecialViewParts.SetShown(this, present);
            if (!present) return;

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
using System;
using System.Collections.Generic;
using Client.Input;
using Client.Input.ArrowKeys;
using Client.Ui.Components.Gauges;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Logic.Belt;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using Client.Localization;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Belt
{
    /// <summary>
    /// ベルトの流量の区画。選んだ窓の行・計測済みなら分間・直近 9 秒の棒。←/→ で窓を切り替える（Web BeltThroughputSection）
    /// Belt throughput section: the selected window row, per-minute when measured, and nine recent bars; ←/→ switch the window (Web BeltThroughputSection)
    /// </summary>
    public sealed class BeltThroughputSectionView : VisualElement, ISpecialDetailRenderer
    {
        private readonly UiStack _stack = new(UiGap.Px2);
        private readonly LocalizedLabel _window = SpecialViewParts.Label("belt-throughput-window", UiTypographyClass.TextSm, UiTypographyClass.TextDefault);
        private readonly LocalizedLabel _perMinute = SpecialViewParts.Label("belt-throughput-per-minute", UiTypographyClass.TextSm, UiTypographyClass.TextDefault);
        private readonly BarSeries _bars = new("belt-throughput-bars");
        private readonly BeltWindowCursor _cursor = new();
        private BeltThroughputDetail _belt;
        private IDisposable _arrowSubscription = Disposable.Empty;

        public BeltThroughputSectionView(IHorizontalArrowKeySource arrowKeys)
        {
            name = "belt-throughput-section";
            AddToClassList("special-section");
            _stack.AddItem(_window);
            _stack.AddItem(_perMinute);
            _stack.AddItem(_bars);
            Add(_stack);

            // 矢印は載っている間だけ聞き、外れたら窓を既定へ戻す
            // Listen to arrows only while attached; reset the window when detached
            RegisterCallback<AttachToPanelEvent>(_ => _arrowSubscription = arrowKeys.OnArrowPressed.Subscribe(OnArrowPressed));
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                _arrowSubscription.Dispose();
                _cursor.Reset();
            });
        }

        public bool HasDetail(SpecialBlockDetailViewModel detail) => detail.TryGetBelt(out _);

        public void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)
        {
            _belt = detail.TryGetBelt(out var belt) ? belt : null;
            Draw();
        }

        private void OnArrowPressed(HorizontalArrowKey key)
        {
            // 入力欄にフォーカスがある・ベルトが無い・表示されていないなら切り替えない（Web の isTextInputElement・readActiveLayer）
            // Do not switch while a text field has focus, without a belt or while hidden (Web isTextInputElement, readActiveLayer)
            // silent-ok: ベルト以外のブロック・閉じた画面・入力中の矢印はこの区画の操作ではない / 切り替わらないなら L1 と録画（R-12）で気づく
            if (_belt == null || UiInputExclusivity.IsTextInputFocused || !SpecialViewParts.IsDisplayedInHierarchy(this)) return;
            _cursor.Step(_belt, key);
            Draw();
        }

        private void Draw()
        {
            // 窓が選べなければ区画ごと出さない（Web の selected 無しの return null）
            // Hide the whole section when no window can be selected (Web returns null without a selection)
            var shown = _belt != null && BeltThroughputLogic.TrySelectBeltWindow(_belt.Windows, _cursor.SelectedTicks, out _);
            SpecialViewParts.SetShown(this, shown);
            if (!shown) return;

            BeltThroughputLogic.TrySelectBeltWindow(_belt.Windows, _cursor.SelectedTicks, out var selected);
            var row = BeltThroughputLogic.RowDisplay(selected);
            _window.SetText(row.Text);
            var measured = row as MeasuredBeltRowDisplay;
            _stack.SetItemShown(_perMinute, measured != null);
            if (measured != null) _perMinute.SetText(new UiText(LocalizationKeys.Ui.BlockInventory.ItemsPerMinute, new[] { new LocalizationTextParam("itemsPerMinute", measured.ItemsPerMinute) }));
            _bars.SetValues(_belt.RecentSecondBars());
        }
    }
}
```
（`Draw` の 2 度目の `TrySelectBeltWindow` は 1 回にまとめてよい: `if (_belt == null || !BeltThroughputLogic.TrySelectBeltWindow(...out var selected)) { SetShown(false); return; }` の形に直して書く。上の形は読みやすさのための説明で、実装は 1 回）

- [ ] **Step 4: コンパイルしテストを実行して通ることを確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.Screens\.BlockInventory\.Special\."`
Expected: ErrorCount 0 / PASS（GearToElectric 13〈TestCase 込み〉・Gear 3・Belt 4・Network 3）。alpha の `LackHighlightText` の名前・クラスが想定と違えば Task 1 Step 2 の読み替えに従う

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Special
git commit -m "feat(uitk): 回転発電機・ベルトの流量・歯車・電力網・歯車網の区画と適合の層（S4-special）

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
- Consumes: Task 4 の `ISpecialBlockCommands`（`SetFilterMode`・`SetFilterItem`・`SetElectricToGearMode`）、Task 6 の `ISpecialDetailRenderer`・`SpecialViewParts`・`ElectricNetworkSectionView`・`SpecialScreenTestHost`・`SpecialDetailSamples`、部品 `ModeSwitch<T>`（`ModeSwitchOption<T>(testid, value, UiText)`・`OnOptionSelected`・`SetSelected`）・`GaugeBar(testid).SetValue(float)`・`ItemSlot(frameTestid)`（`OnSlotGestured`・`SetContent`）・`UiStack`・`UiGroup`、L0-s4b の `FilterSplitterLogic.ModeKey`、`UiCommandResultToaster.Report`、alpha の `BlockItemGridFactory`
- Produces:
```csharp
namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    public sealed class SpecialViewKit
    {
        public SpecialViewKit(ISpecialBlockDetailModel model, ISpecialBlockCommands commands, GameContentPresentation presentation, UiCommandResultToaster toaster, IHorizontalArrowKeySource arrowKeys, BlockItemGridFactory grids);
        public ISpecialBlockDetailModel Model { get; }
        public ISpecialBlockCommands Commands { get; }
        public GameContentPresentation Presentation { get; }
        public IHorizontalArrowKeySource ArrowKeys { get; }
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
            return new SpecialViewKit(Model, Commands, presentation, Toaster, ArrowKeys, new BlockItemGridFactory(BlockSlots, presentation, Toaster));
        }
```
（`BlockItemGridFactory` の ctor〈`IBlockSlotCommands`・`GameContentPresentation`・`UiCommandResultToaster`〉は S4-alpha 計画 14a85b146c の Task 6 の記述。実物と違えば実物に合わせる。`Support/` は 5 本）

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
using Client.Input.ArrowKeys;
using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.State.BlockInventory.Special;
using Client.Ui.State.BlockInventory.Special.Commands;
using Client.Ui.State.Commands;
using Client.Ui.State.Toast;
using Cysharp.Threading.Tasks;

namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    /// <summary>
    /// 特殊族の View が受ける依存の束（詳細の Model・操作・表示素材・矢印キー・alpha の格子の工場）と、操作の結果の届け先
    /// Dependency bundle the special views receive (detail model, commands, content, arrow keys, alpha's grid factory) and where command results go
    /// </summary>
    public sealed class SpecialViewKit
    {
        private readonly UiCommandResultToaster _toaster;

        public ISpecialBlockDetailModel Model { get; }
        public ISpecialBlockCommands Commands { get; }
        public GameContentPresentation Presentation { get; }
        public IHorizontalArrowKeySource ArrowKeys { get; }
        public BlockItemGridFactory Grids { get; }

        public SpecialViewKit(ISpecialBlockDetailModel model, ISpecialBlockCommands commands, GameContentPresentation presentation, UiCommandResultToaster toaster, IHorizontalArrowKeySource arrowKeys, BlockItemGridFactory grids)
        {
            Model = model;
            Commands = commands;
            Presentation = presentation;
            _toaster = toaster;
            ArrowKeys = arrowKeys;
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
            _network.Render(itemSlots, detail);
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
            _status.SetText(new UiText(RocketLaunchPadLogic.StatusKey(rocket.Phase, rocket.WaitReason)));
            _progress.SetValue(rocket.Progress);
        }
    }
}
```
（状態文の色 `--text-high-contrast` は `UiTypographyClass` に無いので、画面 USS の `.rocket-launch-pad-view__status`〈Task 9〉で当てる）

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
            // 合図待ちでなければ題の操作ごと隠す（Web は null。手動の操作を持たない段階）
            // Hide the whole action outside command waits (Web returns null; no manual operation in those phases)
            var hasButton = detail.TryGetRocketLaunchPad(out var rocket) && RocketLaunchPadLogic.TryGetCommandButton(rocket.Phase, out var button) ? Show(button) : HideAll();
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
using Game.Block.Interface.State;
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

            var display = SpaceElevatorLogic.StatusDisplay(elevator.Status);
            _status.Render(new UiText(display.LabelKey), display.Insufficient);
            _pool.SetText(SpecialDetailTexts.SpaceElevatorPool(elevator));
            _section.SetItemShown(_progress, elevator.Connected);
            _section.SetItemShown(_connect, !elevator.Connected);
            _progress.SetValue(elevator.ClimbProgress);
            _connect.SetEnabled(elevator.Status == SpaceElevatorBaseStatus.ReadyToConnect);
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

            var stack = new UiStack(UiGap.Sm);
            stack.AddItem(_grid);
            stack.AddItem(_section);
            stack.AddItem(_network);
            Add(stack);
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
            _network.Render(itemSlots, detail);
        }
    }
}
```
（`PanelActionButton` の namespace は実物〈`Client.Ui/Components/Controls/PanelActionButton/PanelActionButton.cs`〉に合わせる。`UiStack` の name を初期化子で付けるのは alpha の `SectionStackBody` と同じ書き方）

- [ ] **Step 3: コンパイルしテストを実行して通ることを確かめる**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.Screens\.BlockInventory\.Special\.(RocketLaunchPadViewTest|SpaceElevatorViewTest)\."`
Expected: ErrorCount 0 / PASS（Rocket 6〈TestCase 込み〉・SpaceElevator 21〈TestCase 込み〉）

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
- Modify: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/Registration/SpecialDetailAdapter.cs`（`Of<TView>` を足す）
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special/BlockInventorySpecial.uss`
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Registration/GameUi/BlockInventorySpecialGameUiRegistration.cs`（再編後は再編先。D-20）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiRegistration.cs`（画面登録区画に 1 行）
- Modify: `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`（`Screens` 区画に `@import` 1 行）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/BlockInventoryDesignTest.cs`（alpha の色の件に `TestCase` を 1 行。alpha の Produces 一覧「登録・共有区画」の指示）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Special/SpecialFactoriesTest.cs`

**Interfaces:**
- Consumes: Task 3〜8 の全型、alpha の `IBlockInventoryBodyFactory`・`IBlockInventoryTitleActionFactory`・`ISectionStackSectionFactory`・`IBlockSlotRestrictionContributor`・`BlockInventoryBodyMount`・`SectionStackSectionMount`、既存の DI 登録（`OpenBlockDetailTracker`・`BlockNetworkInfoCache`・`BeltTransportClientCache`・`SubInventoryState`・`PlayerGrabModel`・`GameContentPresentation`・`UiCommandResultToaster`・alpha の `BlockItemGridFactory`）
- Produces:
```csharp
namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    public sealed class SpecialDetailAdapter { public static SpecialDetailAdapter Of<TView>(TView view, ISpecialBlockDetailModel model) where TView : VisualElement, ISpecialDetailRenderer; }
    // 本文: FilterSplitterBodyFactory・ElectricToGearBodyFactory・ElectricPoleBodyFactory・SpaceElevatorBodyFactory・RocketLaunchPadBodyFactory（: IBlockInventoryBodyFactory。ctor(SpecialViewKit kit)）
    // 題の操作: RocketLaunchPadTitleActionFactory（: IBlockInventoryTitleActionFactory）
    // 区画: GearToElectricSectionFactory・BeltThroughputSectionFactory・GearSectionFactory・ElectricNetworkSectionFactory・GearNetworkSectionFactory（: ISectionStackSectionFactory）
}
namespace Client.Starter.Registration.GameUi { internal static class BlockInventorySpecialGameUiRegistration { public static void Register(ContainerBuilder builder); } }
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
            var bodies = new IBlockInventoryBodyFactory[] { new FilterSplitterBodyFactory(kit), new ElectricToGearBodyFactory(kit), new ElectricPoleBodyFactory(kit), new SpaceElevatorBodyFactory(kit), new RocketLaunchPadBodyFactory(kit) };
            CollectionAssert.AreEquivalent(new[] { BlockInventoryBodyKind.FilterSplitter, BlockInventoryBodyKind.ElectricToGear, BlockInventoryBodyKind.ElectricPole, BlockInventoryBodyKind.SpaceElevator, BlockInventoryBodyKind.RocketLaunchPad }, bodies.Select(b => b.Kind));
            CollectionAssert.AreEquivalent(new[] { "filter-splitter", "electric-to-gear-view", "electric-pole-view", "space-elevator-view", null }, bodies.Select(b => b.Create(new BlockInventoryBodyMount(false)).Root.name));

            Assert.AreEqual(BlockInventoryTitleActionKind.RocketLaunchPadCommand, new RocketLaunchPadTitleActionFactory(kit).Kind);

            var sections = new ISectionStackSectionFactory[] { new GearToElectricSectionFactory(kit), new BeltThroughputSectionFactory(kit), new GearSectionFactory(kit), new ElectricNetworkSectionFactory(kit), new GearNetworkSectionFactory(kit) };
            CollectionAssert.AreEquivalent(new[] { SectionStackSlot.GearToElectric, SectionStackSlot.BeltThroughput, SectionStackSlot.Gear, SectionStackSlot.ElectricNetwork, SectionStackSlot.GearNetwork }, sections.Select(s => s.Slot));
            CollectionAssert.AreEquivalent(new[] { "gear-to-electric-section", "belt-throughput-section", "gear-section", "electric-network-section", "gear-network-section" }, sections.Select(s => s.Create(new SectionStackSectionMount(null, false)).Root.name));
        }
    }
}
```
（`BlockInventoryBodyMount`・`SectionStackSectionMount` の ctor が internal なら alpha の土台の組み方に合わせる。発射台の本文は根に name を持たない〈`rocket-launch-pad-loading`/`-view` が子〉ので null）

- [ ] **Step 2: 実装を書く**

`SpecialDetailAdapter.cs` に足す:
```csharp
        // View 1 つを本文・区画・題の操作の口に包む
        // Wrap one view in the body, section or title-action port
        public static SpecialDetailAdapter Of<TView>(TView view, ISpecialBlockDetailModel model) where TView : VisualElement, ISpecialDetailRenderer
        {
            return new SpecialDetailAdapter(view, view, model);
        }
```

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
        public FilterSplitterBodyFactory(SpecialViewKit kit) { _kit = kit; }
        public BlockInventoryBodyKind Kind => BlockInventoryBodyKind.FilterSplitter;
        public IBlockInventoryPart Create(BlockInventoryBodyMount mount) => SpecialDetailAdapter.Of(new FilterSplitterBodyView(_kit), _kit.Model);
    }

    public sealed class ElectricToGearBodyFactory : IBlockInventoryBodyFactory
    {
        private readonly SpecialViewKit _kit;
        public ElectricToGearBodyFactory(SpecialViewKit kit) { _kit = kit; }
        public BlockInventoryBodyKind Kind => BlockInventoryBodyKind.ElectricToGear;
        public IBlockInventoryPart Create(BlockInventoryBodyMount mount) => SpecialDetailAdapter.Of(new ElectricToGearBodyView(_kit), _kit.Model);
    }

    public sealed class ElectricPoleBodyFactory : IBlockInventoryBodyFactory
    {
        private readonly SpecialViewKit _kit;
        public ElectricPoleBodyFactory(SpecialViewKit kit) { _kit = kit; }
        public BlockInventoryBodyKind Kind => BlockInventoryBodyKind.ElectricPole;
        public IBlockInventoryPart Create(BlockInventoryBodyMount mount) => SpecialDetailAdapter.Of(new ElectricPoleBodyView(), _kit.Model);
    }

    public sealed class SpaceElevatorBodyFactory : IBlockInventoryBodyFactory
    {
        private readonly SpecialViewKit _kit;
        public SpaceElevatorBodyFactory(SpecialViewKit kit) { _kit = kit; }
        public BlockInventoryBodyKind Kind => BlockInventoryBodyKind.SpaceElevator;
        public IBlockInventoryPart Create(BlockInventoryBodyMount mount) => SpecialDetailAdapter.Of(new SpaceElevatorBodyView(_kit), _kit.Model);
    }

    public sealed class RocketLaunchPadBodyFactory : IBlockInventoryBodyFactory
    {
        private readonly SpecialViewKit _kit;
        public RocketLaunchPadBodyFactory(SpecialViewKit kit) { _kit = kit; }
        public BlockInventoryBodyKind Kind => BlockInventoryBodyKind.RocketLaunchPad;
        public IBlockInventoryPart Create(BlockInventoryBodyMount mount) => SpecialDetailAdapter.Of(new RocketLaunchPadBodyView(_kit), _kit.Model);
    }

    public sealed class RocketLaunchPadTitleActionFactory : IBlockInventoryTitleActionFactory
    {
        private readonly SpecialViewKit _kit;
        public RocketLaunchPadTitleActionFactory(SpecialViewKit kit) { _kit = kit; }
        public BlockInventoryTitleActionKind Kind => BlockInventoryTitleActionKind.RocketLaunchPadCommand;
        public IBlockInventoryPart Create() => SpecialDetailAdapter.Of(new RocketLaunchPadTitleActionView(_kit), _kit.Model);
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
        private readonly SpecialViewKit _kit;
        public GearToElectricSectionFactory(SpecialViewKit kit) { _kit = kit; }
        public SectionStackSlot Slot => SectionStackSlot.GearToElectric;
        public IBlockInventorySection Create(SectionStackSectionMount mount) => SpecialDetailAdapter.Of(new GearToElectricSectionView(), _kit.Model);
    }

    public sealed class BeltThroughputSectionFactory : ISectionStackSectionFactory
    {
        private readonly SpecialViewKit _kit;
        public BeltThroughputSectionFactory(SpecialViewKit kit) { _kit = kit; }
        public SectionStackSlot Slot => SectionStackSlot.BeltThroughput;
        public IBlockInventorySection Create(SectionStackSectionMount mount) => SpecialDetailAdapter.Of(new BeltThroughputSectionView(_kit.ArrowKeys), _kit.Model);
    }

    public sealed class GearSectionFactory : ISectionStackSectionFactory
    {
        private readonly SpecialViewKit _kit;
        public GearSectionFactory(SpecialViewKit kit) { _kit = kit; }
        public SectionStackSlot Slot => SectionStackSlot.Gear;
        public IBlockInventorySection Create(SectionStackSectionMount mount) => SpecialDetailAdapter.Of(new GearSectionView(), _kit.Model);
    }

    public sealed class ElectricNetworkSectionFactory : ISectionStackSectionFactory
    {
        private readonly SpecialViewKit _kit;
        public ElectricNetworkSectionFactory(SpecialViewKit kit) { _kit = kit; }
        public SectionStackSlot Slot => SectionStackSlot.ElectricNetwork;
        public IBlockInventorySection Create(SectionStackSectionMount mount) => SpecialDetailAdapter.Of(new ElectricNetworkSectionView(), _kit.Model);
    }

    public sealed class GearNetworkSectionFactory : ISectionStackSectionFactory
    {
        private readonly SpecialViewKit _kit;
        public GearNetworkSectionFactory(SpecialViewKit kit) { _kit = kit; }
        public SectionStackSlot Slot => SectionStackSlot.GearNetwork;
        public IBlockInventorySection Create(SectionStackSectionMount mount) => SpecialDetailAdapter.Of(new GearNetworkSectionView(), _kit.Model);
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

namespace Client.Starter.Registration.GameUi
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
            builder.Register<SubInventoryOpenSpecialBlock>(Lifetime.Singleton).As<IOpenSpecialBlock>();
            builder.Register<VanillaSpecialBlockRequestSender>(Lifetime.Singleton).As<ISpecialBlockRequestSender>();
            builder.Register<SpecialBlockCommands>(Lifetime.Singleton).As<ISpecialBlockCommands>();
            builder.Register<SpecialInletBindingContributor>(Lifetime.Singleton).As<IBlockSlotRestrictionContributor>();

            // 矢印キーと View の依存の束
            // Arrow keys and the view dependency bundle
            builder.Register<InputSystemArrowKeyStream>(Lifetime.Singleton).As<IHorizontalArrowKeySource>();
            builder.Register<SpecialViewKit>(Lifetime.Singleton);

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
（分岐 B〈Task 1 Step 2〉では `SpecialInletBindingContributor` の行を書かない。`SpecialInletBindingContributor` は State の型なので `Client.Starter.asmdef` の参照は既存の `Client.Ui.State` で足りる。`Client.Input` の参照が `Client.Starter.asmdef` に無ければ足す〈`GrabGameUiRegistration` が `Client.Input.Pointer` を使っているので有るはず〉）

`MainGameUiRegistration.cs` の画面登録区画、alpha の `BlockInventoryGameUiRegistration.Register(builder);` の行の後に:
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
Expected: ErrorCount 0 / PASS（L2 の `UiThemeImportBundleTest`・`UiStyleConventionTest`・`UiLayerDependencyDirectionTest`・`LookExceptionLedgerTest` を含む）

Run（全体スイッチ UITK で PlayMode を 1 回起動して止める）: `uloop execute-dynamic-code --project-path ./moorestech_client --code "Client.Input.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi); return 0;"` → `uloop control-play-mode --project-path ./moorestech_client --action Play` → 30 秒後に `uloop get-logs --project-path ./moorestech_client --log-type Error` → `uloop control-play-mode --project-path ./moorestech_client --action Stop`
Expected: Error 0 件（VContainer の解決失敗・二重登録の例外が無い）。alpha の起動時の情報ログに「空きの Slot」として GearToElectric・BeltThroughput・Gear・ElectricNetwork・GearNetwork が並ばない（`uloop get-logs --log-type Log` で `SectionStackSlotRegistry` を grep）

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special moorestech_client/Assets/Scripts/Client.Starter/Registration moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss"
git commit -m "feat(uitk): 特殊族を S4-alpha の拡張口へ登録し画面 USS を取り込む（S4-special）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 10: 並べ比べ（参照キャプチャと UITK の撮影）

**Files:**
- Modify: `docs/uitk/TODO.md`（「## 見た目の例外」に `LX-126`〜`LX-130` のうち使った番号だけ）
- 成果物（コミットしない）: `moorestech_client/.uloop/outputs/compare/S4-special/*.png`・`compare.html`

**Interfaces:**
- Consumes: Task 9 までの全部、参照 `LOGS/uitk/reference-captures/web-a9d5123537/`・`web-0e81bbe3c3-2560x1080/`、Web の mock-host（`W/e2e/mock-host/`）
- Produces: `done.md` の「## 差の表」（全行に「同じ／直した／LX-n／捨てる」）・`compare.html`（Task 14 の報告ページへ載せる）

- [ ] **Step 1: 参照に無い状態を撮る（D-15）**

master の使い捨て worktree を作り（Unity を起動しないので `git worktree add` でよい。置き場は環境ローカル規約の worktree の置き場）、`W/e2e/mock-host/fixtures/blocks/` に一時の fixture を足して 1280×720 で撮る: 宇宙エレベーター 6 状態（`spaceElevator`・`Climbing`・`Insufficient`・`Loading`・`OutputBlocked`・`Reordered`）・発射台（`awaitingBuildCommand`。題のボタンが出る）・ベルト（180tick の窓と棒）・回転発電機（発電中）。撮り方は既存の参照キャプチャの手順（`LOGS/uitk/reference-captures/README.md`）に従う。fixture はコミットせず、worktree ごと消す。撮った png は `moorestech_client/.uloop/outputs/compare/S4-special/web-*.png` へ置く

- [ ] **Step 2: UITK を同じ状態で撮る**

全体スイッチ UITK で、Task 11 の新シナリオ `uitk-block-special-panels` の `p.Screenshot` を参照と同じ状態・同じ解像度（1280×720。`4-block-gearGenerator` は 2560×1080 も）で撮る（Task 11 を先に流してよい）。発射台・宇宙エレベーターは `tier9` の 2 本の撮影を使う。撮った png を `uitk-*.png` で同じ所へ置く

- [ ] **Step 3: 差の表を書く**

`compare.html`（Web と UITK を横に並べ、差の行に印）を作り、`done.md` の「## 差の表」に 1 状態 1 行で「状態／差／処遇（同じ／直した／LX-n／捨てる）／根拠」を書く。合否の目安は 720p で 2px（[A-14]）。直すのは自分の `SP/**` と `BlockInventorySpecial.uss` だけ。alpha の外殻・部品の差（題の右の逃げ・`BlockItemGridGroup` の間隔など）は直さず `done.md` の「## integrator への依頼」へ。直せない差は `docs/uitk/TODO.md` の「## 見た目の例外」に `LX-126` から番号を振る（書式は既存の行に合わせる。D-11 の JS 数値の写しで桁が合わない所が出たらここ）

- [ ] **Step 4: コミットする**

```bash
git add docs/uitk/TODO.md moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Special
git commit -m "fix(uitk): 特殊族の並べ比べの差を直し、直せない差を見た目の例外に記帳する（S4-special）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```
（直した所も記帳も無ければコミットしない。`done.md` に「差なし」と書く）

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
    await p.Screenshot("filter-splitter");
    await Close();

    // 回転生成機: 出力モードを選ぶと状態の選択が変わる
    // Electric-to-gear: choosing an output mode changes the selected state
    await Open("回転生成機", new Vector3Int(4, 32, 6), "electric-to-gear-view");
    await p.ClickUi("electric-to-gear-mode-0");
    await p.Until(() => Selected("electric-to-gear-mode-0"), 10f, "出力モード0が選ばれた");
    await p.Screenshot("electric-to-gear");
    await Close();

    // 電柱: 電力網の集約の 1 行が出る
    // Electric pole: the network summary row appears
    await Open("電柱", new Vector3Int(6, 32, 6), "electric-pole-view");
    await p.UntilUiElement("electric-network-section", 15f);
    await p.Screenshot("electric-pole");
    await Close();

    // ベルト: ←/→ で窓が切り替わる
    // Belt: ←/→ switch the window
    await Open("ベルトコンベア", new Vector3Int(8, 32, 6), "belt-throughput-section");
    var before = Text("belt-throughput-window");
    await p.PressKey(Key.RightArrow);
    await p.Until(() => Text("belt-throughput-window") != before, 5f, "→ で窓が進む");
    await p.PressKey(Key.LeftArrow);
    await p.Until(() => Text("belt-throughput-window") == before, 5f, "← で戻る");
    await p.Screenshot("belt-throughput");
    await Close();

    // 歯車: トルクと RPM の 2 行
    // Gear: torque and RPM rows
    await Open("鉄の歯車", new Vector3Int(10, 32, 6), "gear-section");
    await p.Screenshot("gear");
    await Close();

    // 宇宙エレベーター: 群・状態区画・電力網が出る（接続は tier9 の 2 本が見る）
    // Space elevator: groups, the state section and the network appear (connection is covered by the two tier9 runs)
    await Open("宇宙エレベーター基部", new Vector3Int(14, 32, 6), "space-elevator-view");
    await p.UntilUiElement("space-elevator-status", 15f);
    await p.Screenshot("space-elevator");
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
（`PlaytestUiElements.HasClass`・`ReadText` は GameUi の要素を testid で読む DSL の実物の名前に合わせる〈無ければ `GameUiRoot.Instance.GetRootElement().Q<…>(testid)` で読む。前例 `uitk-grab-overlay.cs`〉。照準の位置は前例の `AimAtClickCollider` に合わせて直す。`PlaceBlockDirect` がティックと競合して落ちる既知の不具合〈bd `moorestech-xyuaf.2.10`〉に当たったら、置いた後に `p.WaitSeconds(0.5f)` を挟み、それでも落ちれば `done.md` の「## 未確認」に書いて既存の 3 本で代える。ローカル関数の式本体は `Func` を使わない）

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
Expected: File Structure「写すテスト」の表の 12 ファイルの未対応が 0、書式・整合の誤り 0（alpha・machine・train の持ち分の未対応は数えない）

- [ ] **Step 3: 全体の回帰を流す**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "$FILTER"` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\." --timeout-seconds 1500`
Expected: PASS（`UnityCursorTooltipScreenTest` の既知の失敗は数えない。それ以外が落ちたらこのレーンの変更との関係を調べ、関係するなら直す）。件数を `done.md` の「## 証拠」に書く（末尾「設計書・実行計画へ反映する行」の §1.4 の実件数の根拠）

- [ ] **Step 4: コミットする**

```bash
git add docs/uitk/disposition.md docs/uitk/TODO.md
git commit -m "docs(uitk): 特殊族の捨てる 1 件を処遇の表に記す（S4-special）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 13: 全ブランチレビュー

- [ ] **Step 1: 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

base は `origin/feature/uitk`。記録は `../moorestech_logs/harness/` へ（featureブランチにコミットしない）。指摘の反映が判定経路・条件式・評価の時点（例: `SpecialDetailAdapter.IsApplicable`・`BeltThroughputSectionView.OnArrowPressed` のガード・`SpecialBlockCommands` の種別の確かめ）に触れたら、Task 11 の 4 本を反映後のバイナリで流し直してから完了とする。反映はタスクごとにコミットする

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

`Registration/GameUi/*.cs` が 10 本を超えたままなら（再編が未着）マージせず、`done.md` を「状態: 停止（`Registration/GameUi/` の再編待ち）」にして指揮へ渡す（D-20）。超えていなければ:
```bash
gh pr merge <PR番号> --merge
bd close <bd> --reason "PR #<PR番号> を feature/uitk へマージ"
```
`done.md` の 1 行目を「状態: 完了」にする。テストで `Saves/` に作ったワールドを `list-work-saves.sh` で探して消す。`moores-wt rm <worktree 名>` で worktree と Editor を畳む（環境ローカル規約）

---

## Self-Review

1. **Requirements の網羅**: R-1 → Task 2・3（L0）＋Task 11。R-2・R-3・R-4 → Task 5・6。R-5・R-6 → Task 7。R-7・R-8 → Task 8。R-9 → Task 3（L0）・Task 9（登録）・Task 11（`tier9-4b`）。R-10 → Task 4・8。R-11 → Task 9。R-12 → Task 11。R-13 → Task 10。R-14 → Task 12。R-15 → Task 13・14。漏れなし
2. **プレースホルダ**: 「実物に合わせる」は、起草時に実物を確かめられなかった名前（alpha の未マージの型、`RocketLaunchPadPhase` の値の綴り、DSL の要素の読み取り）だけに付け、照合のコマンドを Task 1 Step 2 に置いた。TBD・「同様」は無い
3. **型の一致**: `ISpecialDetailRenderer`（`HasDetail`・`Render(IReadOnlyList<SlotItem>, SpecialBlockDetailViewModel)`）を Task 6〜9 の全 View が実装する。`SpecialDetailAdapter`（`IBlockInventorySection`）・`SpecialViewKit.Run`・`RecordingSpecialBlockCommands` の 1 行の書式（`<action> <args>`）・`RecordingBlockSlotCommands` の書式（`LeftDown <slot>`）を Task 6〜9 で同じ綴りで使う。alpha の型は Global Constraints の「S4-alpha の API」（14a85b146c）と同じ綴り
4. **保留・縮退の解消**: (a) 詳細の未着 → 取得待ち・区画を隠す。届けば Model の購読で描き直す（`SpecialDetailAdapter` が作ってから Detach まで購読。L1「適合の層はパネルの組み直しの外で…」）。要素 0（詳細が一度も来ない）でも取得待ちの文が出続けるので無音にならない。(b) Shift の入れ口の未着 → 寄与しない → L0-s4b が取得待ち → alpha の通知。詳細が届けばパネルの組み直し（追跡ブロックの状態の変化）で寄与が入る。(c) 掴みが空のフィルタ設定 → 良性の拒否（警告ログ）。(d) 合図の古い拒否 → 良性。段階が変われば題の操作が消える（L1 の 2 件）。(e) 区画の出し入れのずれ（D-23）は次のパネルの組み直しで直る。届く契機が組み直しの外だけの区画（網の集約）でも、区画自身が `display` を切り替えるので見え方は正しく、ずれるのは間隔だけ
5. **「決定的」を理由にした選択**: 無い。ベルトの窓の既定 180 と端で止まる巡回は Web の `cycleBeltWindow` の写し（L1 で固定）
6. **呼び出し側への責務漏出**: 掴みの中身を読んで設定/無操作を決めるのは `SpecialBlockCommands`（State）で、View は左右の押下を渡すだけ。結果のトーストは `SpecialViewKit.Run` の 1 箇所。開いたブロックの種別の確かめも Commands の中。View 側に同じ判定は無い

## 判断記録（ADR）

設計の出所: 設計書 `docs/uitk/design.md`（[A-8] View が操作を直接呼ぶ・[A-14] 並べ比べの目安・[A-45]〜[A-47]・[A-57] 変化ごとに同期で描き直す）、実行計画 `docs/uitk/execution-plan.md`（§1.4 S4-special 行・§6.2・E-1・E-13・E-19・E-22）、裁定 `.decisions/2026-11-06-*`（全体スイッチ 1 つ・Web 版と同じ見た目・CEF 由来の不具合は直さない）。以下は planning 中に生じた判断。

- **D-1 所有の境界**: 指揮の申し合わせ（PLAN-W4a）どおり。このレーンは `Screens/BlockInventory/Special/`・`Client.Ui.State/BlockInventory/Special/`（`Logic/` を除く）・`BlockInventorySpecialGameUiRegistration.cs` だけを書く。S4-machine の `GeneratorSection`（ElectricGenerator・FuelGearGenerator・SimpleGearGenerator）は持たない。`SimpleGearGenerator` の参照 `4-block-gearGenerator` でこのレーンが見るのは Gear・GearNetwork の 2 区画だけ
- **D-2 Web の橋と UITK の読み手の並走**: Web の `BlockDetailDtoBuilder` 系は変えず、UITK 用の読み手 `SpecialBlockDetailReader` を State に新設して同じ源を読む。CEF の撤去まで 2 本並ぶ（本PR外のリファクタ提案へ）。却下: Web の橋の DTO を UITK が読む（`Client.WebUiHost` への依存が層の向きに反する）
- **D-3 特殊族専用の詳細の Model**: alpha の `OpenBlockInventoryPanel` に特殊族の詳細を混ぜず、`SpecialBlockDetailModel` を別に持つ。alpha は「連続値は区画が自分で購読」と定めており（Produces 一覧 (3)）、網の集約・ベルトの統計はパネルの組み直しの外で届くため
- **D-4 適合の層 1 つ**: `SpecialDetailAdapter` が alpha の本文・題の操作・区画の 3 つの口を 1 つで実装し、View は `ISpecialDetailRenderer` だけを知る。alpha の口の形が変わったときの直し所を `Registration/` に閉じる
- **D-5 操作は非同期・楽観更新なし**: 結果はサーバーの応答と後続の状態で表示が変わる（Web の dispatchAction と同じ）。`SpecialViewKit.Run` が結果をトーストの係へ渡す
- **D-6 拒否理由 4 つ**: `SpecialBlockNotOpen`・`SpecialBlockRequestFailed`（良性でない）・`SpecialBlockCommandStale`・`FilterSplitterGrabEmpty`（良性。Web の BENIGN_ERRORS と noop）。wire は Web と同じ文字列
- **D-7 矢印キーの流れを `Client.Input/ArrowKeys` に**: `Client.Ui` は Input System を直に読まない。前例 `InputSystemPointerButtonStream`（E-19）。namespace を `Client.Input.Keyboard` にしない（`UnityEngine.InputSystem.Keyboard` とぶつかる）
- **D-8 asmdef の参照 4 つ**: `Client.Ui.State` に `Game.Block`・`Game.Gear`・`Core.BeltTransport`・`Core.Update`（Web の橋 `Client.WebUiHost.asmdef` が同じ源を読むための参照と同じ）
- **D-9 フィルタの択一の選択肢の testid**: `filter-mode-{d}-{mode}`（Web は名前で押すが UITK の L1 は testid で押す）。掴みが空の左押下は View でなく Commands が良性に断る（D-6）
- **D-10 ベルトの窓の選択は View が持つ**: Web の `useState` と同じく区画の寿命の状態（`BeltWindowCursor`）。パネルから外れたら既定 180 へ戻る（Web の破棄と同じ）
- **D-11 JS の数値書式の写し**: `toFixed(0)`・`toPrecision(2)`・`String(number)` を `JsNumberText` に写す（node で実測した境界値を L0 で固定）。桁が合わない所が出たら LX に記帳
- **D-12 発射台の枠の割り当て**: `RocketLaunchPadSlotLayout`（サーバーの定義）から資材・積荷・出力の先頭を引く。Web と同じ絶対添字のまま
- **D-13 捨てる 1 件**: `RocketLaunchPadInventory.test.tsx` の「accepts panel data and narrows inside the dedicated view」は TS の型検査なので捨てる（`disposition.md` に 1 行）
- **D-14 回転発電機の i18n の件を写す**: §1.4 に無いが同じ区画の Web テストなので持ち分に入れる
- **D-15 参照に無い状態は使い捨ての master の worktree で撮る**: Web の mock-host に一時の fixture を足して撮り、fixture はコミットしない（E-22）
- **D-16 録画**: 既存の 3 本（書き換えない）と新規 `uitk/uitk-block-special-panels` の 4 本。ランタイムの挙動（入力・UI）に触れるので unityプレイ録画テストを入れる
- **D-17 Shift の入れ口**: alpha の拡張口 (6) `IBlockSlotRestrictionContributor` に `SpecialInletBindingContributor` を差し込む（分岐 A）。口が実物に無ければ分岐 B（静的な判断だけ作り integrator へ依頼）。L0 は判断まで、送らない・通知は alpha と録画
- **D-18 新しい汎用部品を作らない**: 使う部品は既存の `ModeSwitch`・`GaugeBar`・`BarSeries`・`PanelActionButton`・`ItemSlot` と alpha の `Parts/`
- **D-19 L3 を持たない**: 段の締め（S4-gamma）が持つ
- **D-20 登録の置き場の数**: `Registration/GameUi/` が 10 本を超える。作業は止めずに進め、再編が `origin/feature/uitk` に入るまでマージしない（alpha も integrator へ依頼済み）
- **D-21 回転生成機の択一はモードの一覧が変わったときだけ作り直す**: 毎回作るとフォーカスと押下が切れる。`ElectricToGearDetail.HasSameModes` で比べる
- **D-22 合図の古い拒否は良性**: 成功の後に段階の更新が遅れた二度目の押下の拒否をトーストしない（Web の「%sの成功後topicが遅れても二度目の拒否を誤報しない」）
- **D-23 区画の出し入れ**: alpha の `SectionStackBody` は `IsApplicable` をパネルの `Render` のときにしか聞かない。網の集約・ベルトの統計はその外で届くので、区画の View が自分の `display` を切り替える。代償は、組み直しの外で出し入れが変わったときに `UiStack` の先頭の間隔が次の組み直しまでずれうること。alpha に「出し入れのやり直しを求める口」を足してもらうのが本筋なので integrator への依頼にする（要裁定ではない: 目的・裁定から外れない）。却下: 区画が親の `UiGapContainer.SetItemShown` を直に呼ぶ（alpha の内部の形に依る）
- **D-24 フィルタ分配器の 3 件目**: alpha の外殻の L1 の土台（本文を差し込んだ外殻と自然高のスクロール器）に載せて写す。土台が無ければ部分移植にし、録画の分配器の段で末尾の枠を見る
- **シミュレーター予測**: user-simulator の review の予測と適用は「## 設計検査記録」の下の記録に書く（このレーンはユーザーへ質問しない。指揮の指示）

## 本PR外のリファクタ提案

- Web の橋 `Client.WebUiHost/Game/Topics/BlockDetail/**` の特殊族の DTO 組み立てと、このレーンの `SpecialBlockDetailReader`・`SpecialStateDetailReader`・`SpecialNetworkReader`・`BeltThroughputReader` は同じ源から同じ値を組む（D-2）。CEF の撤去のときに Web の橋の側を消す
- Web の橋の `FilterSplitter*ActionHandler`・`ElectricToGearSetOutputModeActionHandler`・`SpaceElevatorConnectActionHandler`・`RocketLaunchPadCommandActionHandler` と `VanillaSpecialBlockRequestSender`・`SpecialBlockResponseCodes` は同じ送信と失敗コードの写しを持つ。同じく CEF の撤去で 1 本にする
- alpha の `SectionStackBody` に区画から出し入れのやり直しを求める口（D-23）

## 未確認

- S4-alpha の型の綴り（14a85b146c はレビュー中）。Task 1 Step 2 で照合する
- `RocketLaunchPadPhase`・`RocketLaunchPadWaitReason`・`SpaceElevatorBaseStatus` の値の綴り、`BlockTypeConst` の定数名、`ItemSlotContent` の count に null を渡せるか、`ModeSwitchOption` の押し面が `Button` か
- ベルトの区画がパネルから外れたとき窓が既定へ戻ること（L1 で見る。alpha が同じパネルの間に区画を作り直さない前提）
- 新シナリオのブロックの日本語名と照準の位置（Task 11 Step 1）
- `PlaceBlockDirect` のティックとの競合（bd `moorestech-xyuaf.2.10`）が新シナリオに当たるか

## 設計書・実行計画へ反映する行

設計書 `docs/uitk/design.md` の A 表へ:

| ID | 判断 |
|---|---|
| A-160 | ブロックインベントリの特殊族は専用の詳細の Model（`SpecialBlockDetailModel`。`OpenBlockDetailTracker`・`BlockNetworkInfoCache`・`BeltTransportClientCache` を購読）を持ち、適合の層 1 つ（`SpecialDetailAdapter`）が S4-alpha の本文・題の操作・区画の口を実装する。区画はパネルの組み直しの外で届く詳細に合わせて自分の `display` を切り替える |
| A-161 | 特殊族の操作の拒否理由は `SpecialBlockNotOpen`・`SpecialBlockRequestFailed`（良性でない）・`SpecialBlockCommandStale`・`FilterSplitterGrabEmpty`（良性）。wire は Web と同じ |
| A-162 | ←/→ の押下は `Client.Input/ArrowKeys/InputSystemArrowKeyStream`（`IHorizontalArrowKeySource`）が流し、受け手が入力欄のフォーカスと表示中かを確かめる |
| A-163 | 発射台・宇宙エレベーターの Shift の入れ口は S4-special が `IBlockSlotRestrictionContributor` で寄与する。S4-alpha の `SectionStackBody` に、区画から出し入れのやり直しを求める口を足すことを integrator へ依頼する |
| A-164 | `Client.Ui.State.asmdef` に `Game.Block`・`Game.Gear`・`Core.BeltTransport`・`Core.Update` を参照させる（Web の橋と同じ源を読むため） |

部品カタログの差: なし（新しい汎用部品を作らない。D-18）

実行計画 §1.4 の S4-special 行（実件数に直す）:

| レーン | 完了条件 |
|---|---|
| S4-special | FilterSplitter・ElectricToGear・GearToElectric・ElectricPole・Gear・ベルトの流量（`BarSeries`）・ロケット・宇宙エレベーター。写す: `electricPole`(1)・`electricToGear`(1)・`filterSplitter`(3)・`spaceElevator/*`(8〈`spaceElevator` 6・`shiftRejection` 2〉)・C（GearToElectric 7＋i18n 1・BeltThroughput 3・RocketLaunchPad 6〈写す 5・捨てる 1〉・SpaceElevatorSection 5＋SpaceElevatorInventory 1・Gear 3）。計 39（写す 38・捨てる 1）。録画 `connect/rotary-generator-ui`・`tier9` のロケット 2 本・新規 `uitk/uitk-block-special-panels` |

実行計画 §6.1 へ:

| レーン（計画） | A 番号 | LX 番号 |
|---|---|---|
| S4-special（P/2026-11-10-uitk-s4-special.md） | A-160〜A-164 | LX-126〜LX-130 |

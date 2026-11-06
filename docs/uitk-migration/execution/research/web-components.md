# 調査: Web UI 部品・画面の棚卸し（web-components）

UITK の部品化の単位を決める材料。対象は worktree `uitk-execution-plan`（土台 origin/feature/uitk）の `moorestech_web/webui/src`。

- 基準パス: `W = moorestech_web/webui`。以下のパスは断りなければ `W/src/` 起点。
- feature/uitk と origin/master の webui 差分は 14 ファイル（`app/App.tsx`・`shared/uiState/*` の入力排他・`bridge/contract/schemas/ui.ts`・`localizationKeys.ts` 等）。`shared/ui/*` と `features/*`（keyHint のテスト 1 本を除く）は master と同一（`git diff --stat HEAD origin/master -- moorestech_web/webui/src` で実測）。よって本表は master の見た目の正本としてそのまま使える。
- 数値の出し方（実測）:
  - 行数 = `wc -l` 相当（改行数）。テスト（`*.test.*`）は別列。
  - vitest 件数 = 行頭の `it(` / `test(`（`.each` 含む）の数を正規表現で数えた近似。`it.each` は 1 件と数える。
  - playwright 件数 = `e2e/tests/**/*.spec.ts` の行頭 `test(` / `test.skip(` 等（`beforeEach` / `afterEach` / `describe` は除外）。ループ内の `test(` も 1 件と数える。
  - JSX 使用回数 = 非テスト `.tsx` 内の `<部品名` の出現数。

---

## 0. 結論（UITK 部品化の単位への示唆）

1. **共有部品は 24 個（`shared/ui/index.ts` の export）＋ CSS だけの共有 3 本＋非 UI 共有 9 モジュール。** 2 feature 以上で使うのは 15 個（ItemSlot・GamePanel・ModeSwitch・SlotGrid・FadeRule・PanelActionButton・IconButton・HoverTooltip・GaugeBar・FluidAmountSlot・ConnectingPlaceholder・PlacementTargetFace・ProgressArrowGlyph・RecipeListScrollArea・SlotFrame。§1 表）。残りは shared/ui 内部の下請け（GameIcon・FluidSlot・ItemIcon 等）か 1 feature 専用。
2. **スロット系は 1 族（SlotFrame を底に ItemSlot / BlockSlot / FluidAmountSlot / PlacementTargetFace が中身を載せる）で、中身の規則は `shared/ui/slotContent.module.css` 1 本が正本。** UITK でも「SlotFrame（枠・状態）＋中身 Element」の 2 層に分けるのが Web と同形（推測ではなく `composes` の実測から）。状態は data 属性 5 種（selected / filled / catalog / insufficient / ghost）。
3. **feature 間で事実上共有されているのに feature 内に埋まっている部品が 4 つある**（§4）: `features/recipe/views/RecipeRow`（recipe と blockInventory）、`features/recipe/views/LevelInheritanceBadge`（同）、`features/inventory/inventoryGridColumns`（inventory と blockInventory）、`features/hotbar/useHotbarDragSource`（hotbar と buildMenu）。加えて構造が重複している HUD 見出し（チャレンジ HUD と操作モード HUD のラベル＋FadeRule＋本文リスト）がある。UITK では Components 層へ上げる候補。
4. **局所上書き用の CSS 変数が部品の「つまみ」になっている**（`--slot-size`・`--icon-pad`・`--count-*`・`--shortage-count-*`・`--bevel-*`・`--sel-c*`・`--icon-button-size`・`--panel-action-button-width`・`--mode-switch-option-height`・`--gauge-fill`・`--recipe-*` の 45 箇所。§3.3）。UITK 側部品は同じつまみを USS カスタムプロパティか C# の Set メソッドで受けられる必要がある。
5. **Mantine 由来の見た目が残っている箇所**は Web 版の見た目そのものだが設計規約（ref-webui-design §9「Mantine 標準テーマ剥き出し禁止」）とは食い違う: `shared/ui/FluidSlot`（`--mantine-color-dark-*` 枠）・`GameIcon` のフォールバック文字色・`challenge`・`trainHud`・`treeView`・`ModalHost`（Mantine `Modal`/`Button`/`TextInput`）・`ToastHost`（Mantine `Notification`）。components.md §8.6 自身が「`ChallengePanel` / `ModalHost` の素の Mantine `Button` は未着手の負債、前例として引用しない」と書いている。UITK で「同じ見た目」に写すか、正規の語彙（PanelActionButton 等）へ寄せるかは判断が要る（推測: ゴールに照らすと後者を agent 既定にできるが、見た目が変わるので escalate 候補）。

---

## 1. 共有部品（`shared/ui/*`）

### 1.1 使用状況と props

| 部品 | 行数 tsx/css/test | props（要点） | JSX 使用（feature 別） | feature 数 |
|---|---|---|---|---|
| ItemSlot | 117/33/181 | `itemId` `count?` `tooltip?` `selected?` `catalog?` `insufficient?` `shortage?{ownedCount,requiredCount,lacking,tooltipKey}` `ghost?{itemId,count}` `onLeftDown(shift)` `onRightDown` `onRightEnter` `onLeftEnter` `onDoubleClick` `onHoverChange(bool)` `testId?`＋div属性 | blockInventory 7・research 5・recipe 5・inventory 3・buildMenu 1（計21） | 5 |
| GamePanel | 74/216+27/38 | `variant: default\|craft\|skit\|hud`、枠付き(default/craft)のみ `title?` `titleAction?` `gridArea?`、共通 `style?` `children`（union 型で面のみ variant に title を渡すとコンパイルエラー） | blockInventory 3・research 2・recipe 2・skit 1・modeHud 1・inventory 1・buildMenu 1・challenge 1（計12） | 8 |
| PanelActionButton | 23/32/— | `onClick` `children` `testId?`＋button属性＋`data-*` | blockInventory 7・pauseMenu 5・inventory 1（計13） | 3 |
| ModeSwitch | 62/61/124 | `value: T\|null` `options: {value,label,disabled?}[]` `onChange` `orientation?: horizontal\|vertical` `role?: group\|tablist` `disabled?` `testId?` | blockInventory 5・settings 1・pauseMenu 1・buildMenu 1（計8） | 4 |
| SlotGrid | 27/8/— | `children` `cols?`(既定9) `testId?` `style?`（`--slot-size`/`--slot-grid-gap` の局所上書き用） | blockInventory 4・buildMenu 2・recipe 1・inventory 1（計8） | 4 |
| FadeRule | 7/7/— | なし | buildMenu 3・blockInventory 2・skit 1・modeHud 1・challenge 1（計8） | 5 |
| HoverTooltip | 79/14/— | `label` `disabled?` `children: ReactElement`（Mantine `Tooltip` を包む。祖先スクロールで引っ込め、実移動で開き直す） | blockInventory 2・buildMenu 1＋shared/ui 内部 5（ItemSlot・BlockSlot・FluidSlot×2・FluidAmountSlot） | 2 |
| IconButton | 44/37/37 | `onClick` `ariaLabel` `testId?` `children?`（省略で×）＋button属性 | skit 4・blockInventory 1・buildMenu 1 | 3 |
| GaugeBar | 25/17/20 | `value`(0..1) `testId?` | blockInventory 4・progress 1 | 2 |
| FluidAmountSlot | 54/20/79 | `fluidGuid` `badge: AmountBadge` `testId?` | blockInventory 3・recipe 2 | 2 |
| SlotFrame | 55/82/42 | 状態 `selected?` `filled?` `catalog?` `insufficient?` `ghost?`＋ItemSlot と同じマウス系 callback | hotbar 1・buildMenu 1＋shared/ui 内部 3（ItemSlot・BlockSlot・FluidAmountSlot） | 2 |
| ConnectingPlaceholder | 12/—/— | Mantine `TextProps`（children 除く）。`t(ui.common.connecting)` を `Text size=sm c=dimmed` で出す | recipe 2・inventory 1 | 2 |
| PlacementTargetFace | 15/14/— | `iconUrl: string\|null\|undefined` `displayName` | hotbar 1・buildMenu 1 | 2 |
| ProgressArrowGlyph | 57/34/92 | `value: number\|null`（null=静止矢印・progressbar ロール無し） `testId` | blockInventory 1・recipe 1 | 2 |
| RecipeListScrollArea | 31/14/— | `leadingContent` `children` `scrollClassName?` `listClassName?` `listGap` `listTestId` | blockInventory 1・recipe 1 | 2 |
| FluidSlotRow | 30/—/— | `fluids: FluidSlotEntry[]` `progress?: number\|null` `testId` | blockInventory 3 | 1 |
| BarSeries | 23/28/23 | `values: (number\|null)[]` `testId?` | blockInventory 1（ベルト搬出量 §8.23） | 1 |
| ProgressArrowBar | 13/20/— | `value` | blockInventory 1＋FluidSlotRow 内部 1 | 1 |
| BlockSlot | 24/5/36 | `blockId` `name?` `testId?` | research 1 | 1 |
| BlockIcon | 25/—/— | （GameIcon ラッパ） | recipe 1＋BlockSlot 内部 | 1 |
| FluidIcon | 26/—/— | `fluidGuid` `fallback` `className?` | blockInventory 1＋FluidSlot×2・FluidAmountSlot 内部 | 1 |
| ItemIcon | 26/—/— | （GameIcon ラッパ） | notification 1＋ItemSlot 内部 | 1 |
| FluidSlot | 51/42/186 | `fluid: FluidSlotData` `ghost?{fluidGuid,amount}` | feature 直接使用なし（FluidSlotRow 経由のみ） | 0 |
| GameIcon | 73/19/— | `id` `src` `alt` `className?` `fallback: idText\|label\|none` `onRenderResult?` | feature 直接使用なし（Item/Block/FluidIcon の下請け） | 0 |
| useSlotMouse（hook） | 28/—/30 | `(onLeftDown, onRightDown, onRightEnter?, onLeftEnter?)` | SlotFrame 内部のみ | 0 |
| formatSlotAmount（関数） | 11/—/34 | `(n) => string` | blockInventory 1・research 1＋FluidSlot/FluidAmountSlot | 2 |

shared/ui 全体: 非テストコード 1044 行、テスト 16 ファイル・63 件。

### 1.2 見た目の仕様（CSS module と components.md の実測）

色・寸法はトークン名で示す。トークンの値は §3。

| 部品 | 寸法 | 色・面 | 状態 | 仕様の出典 |
|---|---|---|---|---|
| SlotFrame | `--slot-size`（既定 3rem）四方、角丸0 | 面 `rgb(50 52 67 / 50%)`、`box-shadow` 3重のベベル（`--bevel-1..3` 幅・`--bevel-c1..3` 色）＋内側 `--face-inset` | `data-filled`=白面 `rgb(254 254 254)`／`data-catalog`=灰面 `rgb(124 126 136)`／`data-insufficient`=opacity .4／`data-selected`=ベベル色を `--sel-c1..3`（既定シアン `--select-cyan` 系）へ。selected+filled は下端に暗帯／`data-ghost`=子の opacity `--slot-ghost-opacity`(.35) | `shared/ui/SlotFrame/style.module.css`、SKILL §4 |
| slotContent（ItemSlot/BlockSlot/FluidAmountSlot/PlacementTargetFace の中身） | アイコン 100%・padding `--icon-pad`(5px)／個数 右下 `bottom: --count-bottom(-3px)` `right: 2px`・`--count-font-size`(19px)・weight 500 | 文字 `--count-text`(#111)、縁 `iconTextOutlineLight/Dark`（`--icon-text-stroke-*`） | — | `shared/ui/slotContent.module.css`、ADR 0033 |
| ItemSlot 不足表示 | `shortageCount` 右下 `--shortage-count-right(-2px)`/`--shortage-count-bottom(-4px)`/`--shortage-count-font-size(11px)` | `--count-text`、`data-lack` で `--text-insufficient` | `shortage` 指定時は通常個数バッジを出さない | `shared/ui/ItemSlot/style.module.css`、SKILL §4 |
| FluidAmountSlot | SlotFrame 白面＋中身 | 量 `--fluid-amount-font-size`(10px) | — | §4 |
| FluidSlot | **3rem 固定（トークン外）** | 枠 `1px solid var(--mantine-color-dark-4)`、角丸 `--mantine-radius-sm`、面 `--mantine-color-dark-8`、背面フィル色＝液体マスタ色、量 12px bold 白 | ghost | `shared/ui/FluidSlot/style.module.css`（Mantine 変数直参照） |
| GamePanel default | padding `8px 10px 10px 28px`、ヘッダ高 19px・タイトル 19px（scale .947）・罫線 2px | `::before` に `--surface-navy` 縦グラデ＋左右 `--panel-edge-fade` マスク＋影、下部三角装飾 3 個（幅68px・高57px・`rgb(94 100 128/20%)`） | `title` 有りで上下 2 本罫線、`titleAction` は右端絶対配置 | `shared/ui/GamePanel/style.module.css`、SKILL §2 |
| GamePanel craft | padding `8px 15px 15px 10px` | 面 `rgb(7 9 18/80%)`＋内周線 3.132px/3.524px、右下グリップ `--craft-grip-*` | — | 同上 |
| GamePanel skit | 下端全幅・高 `--skit-window-height` | `--skit-window-face`＋上端 `--skit-window-top-fade` フェード | — | 同上、§8.12 |
| GamePanel hud | padding `--hud-panel-padding` | `--surface-navy`＋4辺 `--panel-edge-fade` フェード | — | `GamePanel/hudVariant.module.css`、SKILL §1 |
| ModeSwitch | gap `--mode-switch-gap`、padding `--mode-switch-padding-block/inline`、縦時 高さ `--mode-switch-option-height` | 面 `--gauge-track`＋`--bevel-c1` 輪郭、文字 `--text-muted` | selected=`--text-high-contrast`＋面に `--bevel-c2` を `--mode-switch-selected-mix`(30%) 混色／focus-visible outline／disabled・option-disabled／tablist で下余白 | `ModeSwitch/style.module.css`、§8.6・§8.22 |
| PanelActionButton | `--panel-action-button-width/height/font-size`（87px/28.609px/15px）、weight 500 | 面 `--gauge-track`、文字 `--text-high-contrast`＋0.3px 黒の擬似縁 | hover=面に `--bevel-c2` を 22% 混色／focus-visible | §8.6 |
| IconButton | `--icon-button-size`(slot/2)・`--icon-button-icon-size`(slot/3) | 面なし、線 `--bevel-1` 幅、`--text-muted` | hover/focus で `--text-high-contrast` | §8.6 |
| GaugeBar | 幅100%・高 `--slot-size/6` | 溝 `--gauge-track`＋`--bevel-c1` 内周、充填 `--gauge-fill` | — | §8.6（充填の逸脱は §8.13・§8.18 のみ） |
| BarSeries | 高 `--bar-series-height`・棒幅 `--bar-series-bar-width`・隙間 `--bar-series-gap` | GaugeBar と同じ器 | null=溝のみ（`data-empty`） | §8.6 |
| ProgressArrowGlyph | `--craft-arrow-width/height`（53.622×30.53px）、SVG path 固定 | 溝 `--gauge-track`、充填 `--color-content-primary`、輪郭 `--craft-arrow-outline` 2px、drop-shadow | value=null で充填層・ロール無し | §8.13 |
| ProgressArrowBar | `--progress-arrow-width/height`(4rem×.75rem) | GaugeBar と同じ器 | — | — |
| FadeRule | 幅100%・高2px | `rgb(128 132 159)` の 30→42→26% 横グラデ | — | SKILL §6 装飾語彙1 |
| HoverTooltip | `--tooltip-max-width`/padding/font-size に `--ui-scale` を掛ける | `--tooltip-face`・`--tooltip-border`、白文字、`pre-line` | 祖先スクロールで閉じる | §8 |
| SlotGrid | `--slot-grid-gap`(.425rem)・`--slot-grid-row-gap`、`width: fit-content` | — | — | SKILL §4 |
| RecipeListScrollArea | padding `--recipe-entry-bleed`＋右 `--recipe-list-scrollbar-reserve` | panelBodyScrollArea を継承 | — | §8.10 |
| GameIcon フォールバック | 10px | `--mantine-color-dark-2` | idText / label / none | SKILL §4 |
| PlacementTargetFace | ラベル 10px | `--text-default` | — | — |

### 1.3 CSS だけで共有されているもの（部品化されていない共有）

| ファイル | 中身 | 利用（`composes`） |
|---|---|---|
| `shared/ui/panelBodyScrollArea.module.css`(27) | Mantine ScrollArea を本文いっぱいに伸ばす flex＋スクロールバー（トラック `--gauge-track`・ノブ `--bevel-c2`） | research・blockInventory(×2)・blockInventory/train・pauseMenu・buildMenu・RecipeListScrollArea（6 feature） |
| `shared/ui/textAreaField.module.css`(21) | 入力欄の面（`--gauge-track`＋`--bevel-c1` 枠・placeholder `--text-muted`・focus-visible） | pauseMenu（バグ報告）・blockInventory/details/station（駅名） |
| `shared/ui/slotContent.module.css`(31) | 1 マスの中身（§1.2） | ItemSlot・BlockSlot・FluidAmountSlot・PlacementTargetFace |
| `features/recipe/views/RecipeActionButton.module.css`(10) | 主要アクションの青グラデボタン（`--recipe-action-background`） | recipe/RecipeBox のみ |

UITK への示唆（推測）: スクロール領域・テキスト入力欄はそれぞれ 1 部品（`GameScrollView` / 既存 `GameTextField`）にまとめれば足りる。

### 1.4 非 UI の共有モジュール（`shared/*`）

| モジュール | 行数 code/test(件) | 役割 | 利用者 |
|---|---|---|---|
| itemMove | 199 / 3ファイル・33件 | プレイヤー／ブロックスロットのクリック計画（純ロジック。`planPlayerLeftClick` 等）と `dispatchPlanned` | inventory(2)・blockInventory(1) |
| pointerGesture | 141 / 1・5件 | `usePressGesture`（5px 閾値のタップ/ドラッグ判定）・`dragThreshold` | hotbar・recipe(useDragScroll)・treeView |
| tooltip | 72 / 2・8件 | `CursorTooltip`（ホスト指示のカーソル追従説明。18px・padding 6/10・max 320、ADR 0019）・位置クランプ | app/App.tsx のみ |
| treeView | 460 / 5・29件 | ノードグラフのパン・ズーム・慣性・リサイズ追従・ノードジェスチャ（§8.5） | research・challenge |
| tutorialAnchor | 358 / 3・11件 | `tutorialAnchor()` 属性付与・登録表・祖先クリップ矩形・anchor id 表 | 13 feature＋bridge（最多の横断依存） |
| uiScale | 17 / 0 | `--ui-scale` の store | app・tutorial(2) |
| uiState | 383（master 側は入力排他の構成が異なる）/ 3・35件 | ActiveLayer 判定・画面ルーティング（`screenForUiState` 等）・ゲームレイヤー keydown/wheel・grab 可否・blocking スキット判定 | app・blockInventory(3)・inventory(2)・tutorial(2)・challenge・hotbar・keyHint・recipe |
| placementTarget | 45 / 1・9件 | 配置対象（ブロック/列車等）の表示名・アイコン解決 | buildMenu・hotbar・modeHud・research |
| i18n | 502 / 9・56件 | `useI18n`/`t()`・辞書 store・アイテム名解決・`LocalizedShortcutHint`・生成キー `L` | ほぼ全 feature（blockInventory は 43 ファイル） |

その他 `shared/` 直下: `clamp01.ts`・`ownedCounts.ts`・`materialTooltipText.ts`（ItemSlot の shortage ツールチップ文言）。

---

## 2. feature ごとの棚卸し

### 2.1 数値

| feature | code ts/tsx 行 | css 行 | 非テストファイル数 | vitest ファイル/件 | playwright spec/件 | 使う共有部品（JSX 回数） |
|---|---|---|---|---|---|---|
| blockInventory | 819/1528 | 208 | 58 | 35/165 | 19/65（`e2e/tests/block/**` 17本＋`regression/sectionStack`＋`train/timetable`） | ItemSlot7・PanelActionButton7・ModeSwitch5・GaugeBar4・SlotGrid4・GamePanel3・FluidAmountSlot3・FluidSlotRow3・FadeRule2・HoverTooltip2・IconButton・BarSeries・FluidIcon・ProgressArrowBar・ProgressArrowGlyph・RecipeListScrollArea |
| buildMenu | 455/394 | 189 | 13 | 7/53 | 3/19（`regression/buildMenu*`）＋`placementIconCentering`2 を hotbar と共有 | FadeRule3・SlotGrid2・GamePanel・ModeSwitch(縦)・ItemSlot・SlotFrame・HoverTooltip・IconButton・PlacementTargetFace |
| recipe | 276/465 | 403 | 17 | 9/33 | 4/19（`e2e/tests/recipe/**`） | ItemSlot5・GamePanel2・FluidAmountSlot2・ConnectingPlaceholder2・SlotGrid・ProgressArrowGlyph・RecipeListScrollArea・BlockIcon |
| research | 125/324 | 177 | 9 | 5/36 | 4/19（うち `tmp-icon-click.spec.ts` 1 件は一時名） | ItemSlot5・GamePanel2・BlockSlot（＋shared/treeView） |
| tutorial | 79/402 | 155 | 9 | 3/42 | 3/13（`system/tutorial`・`tutorialHighlightClip`・`worldPin`） | なし（shared/tutorialAnchor・uiScale・i18n の LocalizedShortcutHint） |
| pauseMenu | 129/308 | 59 | 8 | 8/37 | 3/7（`system/pauseMenu`・`pauseMenuSettingsLayout`・`feedbackReportLabels`） | PanelActionButton5・ModeSwitch（＋settings 3 部品・textAreaField・panelBodyScrollArea） |
| inventory | 189/251 | 33 | 9 | 6/42 | 3/20（`inventory/**`） | ItemSlot3・GamePanel・SlotGrid・PanelActionButton・ConnectingPlaceholder |
| notification | 227/91 | 89 | 4 | 5/42 | 4/6（`notification/**`） | ItemIcon |
| skit | 22/297 | 249 | 7 | 1/5 | 2/7（`skit`・`skitPauseInput`）＋`phaseC4`2 | IconButton4・GamePanel(skit)・FadeRule |
| settings | 3/189 | 0 | 4 | 3/17 | pauseMenu の spec に含まれる | ModeSwitch（3 画面） |
| eventLanguageGate | 126/90 | 12 | 4 | 2/9 | なし | なし（Mantine Overlay/Portal/Button） |
| challenge | 9/121 | 58 | 5 | 1/2 | 2/12（`challenge.spec`・`challenge/hudLayout`） | GamePanel(hud)・FadeRule（＋shared/treeView） |
| hotbar | 99/83 | 105 | 4 | 2/10 | 1/6＋`placementIconCentering` | SlotFrame・PlacementTargetFace（SlotGrid 対象外・SKILL §4） |
| modeHud | 2/98 | 83 | 3 | 2/11 | 1/6 | GamePanel(craft)・FadeRule |
| modal | 26/57 | 0 | 3 | 1/8 | 1/4 | なし（Mantine Modal/Button/Text/TextInput） |
| keyHint | 1/32 | 25 | 2 | 1/4 | なし | なし（`keyHintText` 共有クラス） |
| toast | 35/21 | 14 | 3 | 1/2 | なし（`regression/actionErrors` が間接） | なし（Mantine Notification） |
| commonHud | 1/9 | 14 | 2 | 1/3 | 1/6（`system/commonHud`） | なし（クロスヘア） |
| progress | 1/28 | 29 | 2 | 0/0 | 1/3 | GaugeBar（`--gauge-fill` を局所上書き） |
| trainHud | 1/30 | 11 | 2 | 0/0 | `train.spec` 2 のうち一部 | なし（Mantine Paper/Stack/Text/Title） |

横断 spec（特定 feature に属さない）: `system/i18n`2・`portalLayerScaling`4・`textSelection`2・`uiState`5・`regression/inputLayer`5・`connection`3・`actionErrors`2・`itemSlot`2・`reference/captureReadiness`5。app/ 4ファイル・15件、bridge/ 29ファイル・191件（vitest）。

### 2.2 feature 固有部品とロジックの内訳

分類: 【V】表示部品（tsx）／【P】純ロジック（bridge も hook も参照しない .ts）／【S】状態（store・hook）／【B】ブリッジ参照（bridge の型・topic・action を触る .ts）。分類は import の機械判定で、bridge 参照の多くは型参照のみ（推測）。

- **blockInventory**（`features/blockInventory/`）
  - 外殻【V】`BlockInventoryPanel.tsx`(104・Mantine ScrollArea)・`BlockItemGrid.tsx`(48・持ち物と同列数のグリッド)・`BlockInventoryKeyHandler.tsx`(23)。
  - 振り分け【B】`registry/blockComponentRegistry.ts`(43・blockType→ビュー)・`views/SectionStackView.tsx`(94・標準セクション合成)。
  - ビュー【V】`views/` 7 本（ElectricPole・ElectricToGear・FilterSplitter・RocketLaunchPad・SpaceElevator・TrainPlatform・SectionStack）。
  - 詳細セクション【V】`details/` 約 20 本（Machine・Miner・Generator・Gear・GearToElectric・Network・BeltThroughput・TrainPlatform・cleanRoom×2・machineInputStall・pump・rocket・spaceElevator・station 駅名入力）。
  - 機械 UI【V】`details/machine/`（MachineInventoryBody・SelectedRecipeHeader・ModuleSlotStackLimitText・recipeSelection の List/Row）。§8.7。
  - 行部品【V】`details/rows/`（MachineStateRow・PerMinuteRateRow・PowerRateText）・`details/LackHighlightText`・`details/blockInfoFetch/BlockInfoUnavailableLine`。
  - 列車【V】`train/`（TrainInventoryBody・TrainTimetableSection・TrainTimetableStopList）＋【S】`useImmediateTimetable.ts`。
  - ロジック【B】`detailLogic.ts`(118)・`beltThroughputLogic.ts`(69)・`machineSlotGhosts.ts`(91)・`machineRecipeSelectionLogic.ts`(61)・`cleanRoomLogic.ts`(74)・`cleanRoomYieldLogic.ts`(61)・`timetableEditLogic.ts`(61)・`rocketLaunchPadLogic.ts`・`spaceElevator*`・`filterSplitterLogic.ts`。【P】`blockInventoryKeydown.ts`・`registeredBlockTypes.ts`。【S】`useBlockSlotGestures.ts`(46)。
- **buildMenu**: 【V】`BuildMenuPanel`(120・3カラム)・`views/` 7 本（CategoryGrid・CategoryList・DetailSidebar・SearchInput(§8.9 素 input)・BuildMenuSlot・CategorySidebar(縦 ModeSwitch)）。【S】`hooks/useBuildMenuCategoryScroll.ts`(**305 行**、ジャンプ＋scroll-spy)。【P】`logic/buildMenuScrollSpy.ts`・`sessionState/buildMenuSessionState.ts`。【B】`buildMenuGrouping.ts`・`buildMenuShortage.ts`。§8.11。
- **recipe**: 【V】`panels/ItemListPanel`(83・6列)・`panels/RecipeViewer`(59)・`views/`（CraftRecipeEntry・MachineRecipeEntry・RecipeRow・RecipeContent・ItemHeader・LevelInheritanceBadge）。CSS が厚い（`RecipeBox.module.css` 212 行）。【P】`holdCraftLogic.ts`・`recipeSlotLayout.ts`。【S】`selectionStore.ts`・`useHoldCraft.ts`(rAF)・`useDragScroll.ts`・`RecipeSelectionKeyHandler.tsx`。【B】`craftLogic.ts`・`levelInheritanceBadge.ts`。§8.13・§8.17。
- **research**: 【V】`ResearchTreePanel`・`ResearchNodeCard`・`ResearchDetailPane`(84)・`unlock/`（UnlockSections(103)・UnlockCraftCapLabel・UnlockFluidLabel）。【B】`researchLogic.ts`(93)・`unlock/unlockEntries.ts`。グラフは shared/treeView。§8.5。
- **tutorial**: 【V】`overlay/TutorialOverlay.tsx`(193・ハイライト枠とクリップ)・`overlay/HighlightLabel`・`overlay/DragGuide`・`WorldPinOverlay`(83)・`KeyControlHintHud`。【P】`overlay/highlightGlowToken.ts`・`labelGapToken.ts`（CSS 変数を JS で読む）。【B】`tutorialElement.ts`。§8.8・§8.17(ドラッグガイド)・§8.19。
- **pauseMenu**: 【V】`PauseMenuPanel`・`PauseMenuTopPage`・`PauseMenuSubPage`・`BugReportForm`(155)・`BugReportRecoveryControls`。【S】`useBugReportDraft.ts`。settings の 3 画面を埋め込む。
- **settings**: 【V】`GraphicsQualitySettings`(115)・`LanguageSelect`・`SprintModeSelect`（いずれも ModeSwitch＋Mantine Title/Text）。
- **inventory**: 【V】`InventoryPanel`(75)・`EquipmentPanel`(127・常時 HUD)・`GrabOverlay`(49)。【B】`slotActions.ts`(95・3パネル共通スロット操作)・`blockSlotRestriction.ts`・`splitDrag.ts`。【P】`equipmentLogic.ts`・`inventoryGridColumns.ts`。
- **notification**: 【V】`NotificationHost`(91)。【P】`notificationMessages.ts`(145・messageId→文言)。【S】`notificationStore.ts`(80)。
- **skit**: 【V】`SkitPresentation`(129)・`SkitTransition`・`controls/SkitChoiceList`・`controls/SkitToolbar`・`icons.tsx`(73・インライン SVG 群)。【P】`interaction.ts`。CSS 249 行。§8.12。
- **eventLanguageGate**: 【V】`EventLanguageGate`(Mantine Overlay/Portal)・`EventLanguageGateBody`。【S】`useLanguageSelectionAnswer.ts`(125・状態機械)。§8.20/8.20a。注: components.md §8.20 が外殻として名指す `features/eventLanguageGate/FullScreenGate` は**このブランチにも origin/master にも存在しない**（`git grep FullScreenGate` は components.md のみヒット）。文書が実装に遅れている。
- **challenge**: 【V】`ChallengePanel`(Mantine Button/Group/Title)・`ChallengeNodeCard`(Mantine Paper)・`CurrentChallengeHud`。【B】`challengeLogic.ts`。§8.14。
- **hotbar**: 【V】`HotbarPanel/index.tsx`(83)。【P】`hotbarDnd.ts`。【S】`useHotbarDragSource.ts`。CSS で SlotFrame のベベル色を局所上書き（§3.3）。
- **modeHud**: 【V】`PlacementModeHud`・`DeleteModeWarningBands`（装飾語彙6 黄黒斜線帯）。§8.15。
- **modal**: 【V】`ModalHost`（Mantine Modal/Button/TextInput）。【B】`modalLogic.ts`。
- **keyHint**: 【V】`KeyHintHud`。**commonHud**: 【V】`CommonHud`(クロスヘア)。**progress**: 【V】`ProgressBar`(GaugeBar)。**trainHud**: 【V】`TrainRidingHud`(Mantine Paper)。**toast**: 【V】`ToastHost`(Mantine Notification)＋【S】`toastStore.ts`。

---

## 3. トークン（CSS 変数）の体系

### 3.1 置き場と件数（実測）

| ファイル | 行数 | 定義数（概算） | 中身 |
|---|---|---|---|
| `app/tokens.css` | 491 | 約 200 | 下表の系統すべて＋`@keyframes tutorial-attention-pulse-*`＋共有クラス `:where(.keyHintText)`・`.iconTextOutlineLight/Dark` |
| `app/slotTokens.css` | 26 | 9 | ベベル `--bevel-1..3`・`--bevel-c1..3`・`--face-inset`・`--slot-ghost-opacity` |
| `app/skitTokens.css` | 70 | 約 35 | `--skit-*`・`--z-stage-skit`・`--z-portal-skit-transition` |
| `app/index.css` | 50 | 1 | `@font-face`、ロケール別 `--font-ui` 切替、body の user-select |
| features/shared の CSS module 内 | — | 45 箇所 | 局所上書き（§3.3） |

app 3 ファイルの定義行合計 242。

### 3.2 系統

| 系統 | 代表トークン（値） | 備考 |
|---|---|---|
| 舞台・拡縮 | `--ui-stage-width/height`(1280/720)・`--ui-viewport-*`・`--ui-scale`（JS が書く） | stage 族／viewport 族（SKILL §1.5） |
| 面色 | `--surface-navy`(rgb(10 14 27/80%))・`--tooltip-face`・`--skit-window-face`・`--full-screen-gate-face`・`--research-node-face`/`--notification-face`(=surface-navy) | 新色ハードコード禁止（SKILL §5） |
| 文字色 | `--text-default`(#eee)・`--text-high-contrast`(#fff)・`--text-muted`(#a6a7ab)・`--text-insufficient`(#ff7878)・`--color-content-primary`(#f2f3f7)・`--count-text`(#111) | |
| アクセント | `--select-cyan`(rgb(0 221 255))・`--recipe-action-background`(青グラデ)・`--tutorial-attention-red`(#ff0000)・`--craft-arrow-outline` | 用途限定（SKILL §5） |
| ベベル・ゲージ | `--bevel-*`・`--gauge-track`(=surface-navy)・`--gauge-fill`(rgb(104 106 120))・`--gauge-outline-width`・`--bar-series-*` | |
| スロット寸法 | `--slot-size`(3rem)・`--hotbar-slot-size`(3.23rem)・`--equipment-slot-*`・`--inventory-slot-base-*`・`--inventory-columns`(10)・`--icon-text-stroke-*` | |
| 部品寸法 | `--mode-switch-*`・`--panel-action-button-*`・`--icon-button-*`・`--tooltip-*`・`--progress-arrow-*`・`--craft-arrow-*`・`--craft-grip-*`・`--panel-edge-fade`(12px)・`--hud-panel-padding`(14px) | |
| 画面別寸法 | `--build-menu-*`(10本)・`--recipe-*`(15本)・`--challenge-hud-*`(14本)・`--operation-hud-*`(10本)・`--notification-*`・`--world-pin-*`・`--tutorial-*`・`--key-hint-*`・`--bug-report-*`・`--pause-menu-max-height`・`--full-screen-gate-*`・`--delete-mode-warning-*`・`--menu-*-safe-area`・`--block-panel-*`・`--stage-*` | 1280 基準の固定長（%禁止。例外はヴィネット） |
| bleed（はみ出し余白） | `--item-list-count-bleed`・`--research-detail-*-bleed`・`--block-panel-body-bleed`・`--recipe-entry-bleed`・`--tutorial-anchor-padding`・`--tutorial-anchor-clip-inset` | §8.10 |
| z 層 | `--z-viewport-*`(0/1/2/40)・`--z-stage-*`(20/30/31/40/100/250)・`--z-portal-*`(10/200/210/300/900/2000/2500) | 数値直書き禁止 |
| 動き | `--tutorial-pulse-duration`(1200ms)・`--tutorial-pulse-strong/subtle`（keyframes 名）・`--notification-enter/exit-duration`・`--tutorial-drag-guide-duration` | |
| 字体 | `--font-ui`・`--label-face-*`・`--skit-*-font-size` | |

参照ゼロ（`var(` で参照されない）: `--z-portal-modal` のみ（`--world-pin-edge-margin`・`--tutorial-highlight-label-gap` は TS から `getComputedStyle` で読まれている。`WorldPinOverlay.tsx:76`・`overlay/labelGapToken.ts:9`）。

既存 UITK 側（`moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/`）には `Tokens.uss`・`SlotTokens.uss`・`SkitTokens.uss`・`ControlTokens.uss`・`Typography.uss`・`Theme.uss` がすでにある（中身の照合は本調査の範囲外）。

### 3.3 局所上書き（部品の「つまみ」として使われている変数）

| 上書きされる変数 | 上書き箇所 | 意味 |
|---|---|---|
| `--slot-size` | `hotbar/HotbarPanel/style.module.css:41`・`inventory/EquipmentPanel/style.module.css:20`・`inventory/InventoryPanel/GrabOverlay.module.css:4` | スロット寸法 |
| `--icon-pad` `--count-bottom` `--count-font-size` `--count-letter-spacing` | `recipe/views/RecipeBox.module.css:61-64,116-119`・`hotbar/HotbarPanel/style.module.css:46-47` | 中身の寸法 |
| `--shortage-count-right/bottom/font-size` | `blockInventory/details/machine/machineInventoryBody.module.css:16-18`・`recipe/views/RecipeBox.module.css:70-71` | 不足表示の位置 |
| `--bevel-*` `--bevel-inset` `--face-inset` `--sel-c1..3` `--selected-face-*` | `hotbar/HotbarPanel/style.module.css:42-64` | ホットバー専用の枠色・選択色 |
| `--recipe-row-padding-block` `--recipe-slot-size-max` | `blockInventory/details/machine/recipeSelection/machineRecipeSelectionList.module.css:25-26` | 機械レシピ行の詰め |
| `--gauge-fill` | `progress/style.module.css:16` | 採掘ゲージの白（§8.18） |
| `--icon-button-size` `--icon-button-icon-size` | `skit/controls/SkitToolbar.module.css:6-7` | スキットのツールボタン |
| `--panel-action-button-width: auto` | `pauseMenu/style.module.css:6` | 縦並び |
| `--mode-switch-option-height` | `buildMenu/style.module.css:48` | 縦サイドバー |
| `--key-hint-bottom-gap` `--drag-guide-scaled-size` | keyHint・tutorial | 局所定数 |

feature CSS の色ハードコード（`#hex`/`rgb(` を含む行。ref-webui-design §9 の禁止事項と食い違う箇所、行数）: RecipeBox 12・HotbarPanel 10・ItemHeader 3・SkitChoiceList 2・pauseMenu 2・research 1・notification 1・ItemListPanel 1。Mantine 変数直参照: `trainHud/style.module.css`・`challenge/style.module.css`・`shared/ui/FluidSlot`・`shared/ui/GameIcon`・`shared/treeView/TreeView.module.css`。

Mantine コンポーネントの import 回数（features＋shared＋app）: Text 42・Stack 33・Group 10・Title 9・ScrollArea 8・Button 8・Box 4・Portal 3・Paper 3・Overlay 2・Tooltip/TextInput/Notification/Modal/Loader 各1。UITK では Text/Title の文字様式（`size="sm" c="dimmed"` 等）を Typography のクラスへ、Stack/Group をレイアウトクラスへ写す必要がある（推測: 42 ファイル分の `Text` の size/c の組み合わせは本調査では集計していない）。

---

## 4. feature 内に埋まっている「事実上の共有部品」

### 4.1 他 feature から import されているもの（実測: `@/features/<他feature>/...` の import）

| 埋まっている場所 | 使う feature | 種別 | UITK での扱いの候補（推測） |
|---|---|---|---|
| `features/recipe/views/RecipeRow.tsx`(53) | recipe（CraftRecipeEntry・MachineRecipeEntry）・blockInventory（`details/machine/recipeSelection/MachineRecipeSelectionRow.tsx:6`） | 表示部品（3カラム grid・矢印列固定・素材/結果の折返し。§8.17） | Components 層の `RecipeRow` |
| `features/recipe/views/LevelInheritanceBadge.tsx`(19)＋`logic/levelInheritanceBadge.ts` | recipe・blockInventory（`MachineRecipeSelectionRow.tsx:7`） | 表示部品 | Components 層 |
| `features/recipe/logic/recipeSlotLayout.ts`(27) | RecipeRow 経由で両者 | 純ロジック | RecipeRow に同梱 |
| `features/inventory/inventoryGridColumns.ts`(5) | inventory・blockInventory（`BlockItemGrid.tsx:6`） | 定数 | トークン／定数 |
| `features/hotbar/useHotbarDragSource.ts`(40) | hotbar・buildMenu（`views/BuildMenuSlot.tsx:4`） | ジェスチャ（クロス要素 D&D。SKILL §4） | Foundation/Input 層 |
| `features/blockInventory/details/machine/machineSlotGhosts.ts`・`details/detailLogic.ts` | inventory（`blockSlotRestriction.ts:3-4`） | 純ロジック | 共有ロジック層 |
| `features/settings` の 3 部品 | pauseMenu（`PauseMenuPanel.tsx:2`） | 画面の埋め込み | 既に UITK Screens/Settings として独立 |
| `features/notification` の store | inventory（`slotActions.ts:10`） | サービス | ホスト |
| `features/toast` の `emitToast` | pauseMenu（`BugReportForm.tsx:6`） | サービス | 既存 UITK Toast |

### 4.2 import はされていないが構造が重複しているもの

| パターン | 箇所 | 根拠 |
|---|---|---|
| HUD 見出し（`--text-muted` の小ラベル → FadeRule → 本文リスト） | `challenge/CurrentChallengeHud.tsx:21-32`（GamePanel hud）・`modeHud/PlacementModeHud.tsx:20-29`（GamePanel craft） | CSS が同形で、トークン族が並行して二重化している（`--challenge-hud-label-font-size/letter-spacing/label-rule-gap/rule-list-gap` と `--operation-hud-*` が同名・同値 12px/1px/5px/8px。`app/tokens.css:165-191`） |
| 見出しラベル＋FadeRule の区切り | buildMenu のカテゴリ大見出し／サブカテゴリ見出し（§8.11）・blockInventory の区切り・skit | FadeRule の使用 8 回のうち大半がラベルと対 |
| 1 マス中身の寸法上書き | RecipeBox・HotbarPanel・機械本体 | §3.3 |

### 4.3 feature 内で複数ファイルから使われる部品（feature 内共有。UITK でも feature 内部品として切る単位）

| 部品 | 利用ファイル数 |
|---|---|
| `blockInventory/details/LackHighlightText` | 9（MachineSection・PowerRateText・Pump・CleanRoom・Network・SpaceElevator・GearToElectric・MachineStateRow・MachineInputStall） |
| `blockInventory/BlockItemGrid` | 6（MachineInventoryBody・TrainInventoryBody・SectionStackView・RocketLaunchPad・SpaceElevator・TrainPlatform） |
| `blockInventory/details/blockInfoFetch/BlockInfoUnavailableLine` | 3 |
| `blockInventory/details/rows/*`（MachineStateRow・PerMinuteRateRow・PowerRateText） | 各 2 |
| `blockInventory/useBlockSlotGestures` | 2 |
| `inventory/slotActions` | 2（InventoryPanel・EquipmentPanel） |
| `tutorial/tutorialElement` | 2 |
| `skit/icons.tsx` | 3（SkitPresentation・SkitToolbar・SkitChoiceList） |

---

## 5. 参考

- 見た目の正本: `W/docs/design/components.md`（402 行、§8〜§8.23。§8.17 が 2 回使われている）・`.agents/skills/ref-webui-design/SKILL.md`（267 行。§1 画面構成・§1.5 stage/viewport 族・§2 GamePanel・§4 スロット・§5 色・§6 装飾語彙 6 種・§7 文字・§9 禁止リスト）。
- 参照キャプチャ: `/Users/sakastudio/hermes-agent/data/repos/moorestech_logs/uitk/reference-captures/` には `web-869b2d4264` と `web-a9d5123537` の 2 ディレクトリ（中身は未確認）。撮影台本は `W/e2e/uitk-reference/referenceShots.ts`(122 行)。
- 既存 UITK 部品（feature/uitk）: `Client.Ui/Components/` に GamePanel（Face・DecoLine・BottomDeco）・ModeSwitch・PanelActionButton・TextField（IME）・Toast の 5 系統。Web の共有部品 24 個のうち UITK にあるのは GamePanel・ModeSwitch・PanelActionButton の 3 個（＋Web では Mantine の TextInput/Notification に当たる 2 個）。

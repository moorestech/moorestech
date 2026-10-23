# UI Toolkit 移行のための画面・部品の棚卸し

調査日 2026-10-22。読むだけの調査で、repo には何も書いていない。
パスは特に断りがなければ `moorestech_web/webui/src/` からの相対パス。`推測` と書いた所は実測や実装の確認をしていない。

---

## 0. 前提と出典

| 資料 | パス | ここで使った内容 |
| --- | --- | --- |
| 裁定 | `.decisions/2026-10-18-CEFを撤去しUI-Toolkitへ全面移行する.md` | 素の C#/UXML/USS で全面移行する。OneJS は使わない。移行の順番・出荷形・完了条件は grill と plan で決める |
| 議論の記録 | `docs/webui/ui-toolkit-migration-discussion-2026-10-18.md` | strangler 方式の案、vitest と Playwright を受入条件へ読み替える、第一弾はインベントリかホットバー |
| PoC の実測 | ブランチ `poc/uitk-hard-cases` のレポート（report/index.html） | 移行を止める障害は無い。自前で作る部品とその規模、USS で書けないこと、未検証の項目 |
| デザイン哲学 | `.agents/skills/ref-webui-design/SKILL.md`、`docs/design/components.md` | パネル面の variant、装飾語彙、画像アセットの禁止、スクロールバー §8.10、全画面ゲート §8.20 |
| uGUI の例外 | ADR 0052、`.decisions/2026-09-05-メインメニューとロード画面はuGUI現状維持（UI Toolkit 化は後日）.md` | メインメニューとロード画面は今回の範囲外 |

**行数の数え方**: `*.test.*`・`testSupport.ts`・`generated/` を除いた ts/tsx と CSS を数えた。

---

## 1. 全体像

### 1.1 規模（テストを除く）

| 領域 | ts/tsx 行 | CSS 行 | 合計 |
| --- | ---: | ---: | ---: |
| features（20機能） | 6,522 | 1,859 | 8,381 |
| shared（共通部品とロジック） | 3,082 | 848 | 3,930 |
| app（骨格とトークン） | 222 | 700 | 922 |
| bridge（WebSocket・topic/action・playtest 照会） | 2,203 | 0 | 2,203 |
| **計** | | | **約 15.4k** |

議論の記録にある「約 24.5k 行」はテストを含めた値と思われる（推測）。書き直す対象のプロダクションコードは約 15k 行。このうち bridge の 2.2k 行は、UITK にすると C# から直接購読する形に変わるので、大部分は消える見込み（推測）。
C# 側の `moorestech_client/Assets/Scripts/Client.WebUiHost/` は 145 ファイルあり、そのうち `Game/`（Topics・Actions の発行側）が 8,165 行。ここは「Web へ JSON で配る」層から「C# の VisualElement が購読する」層へ作り替えることになる（推測）。

### 1.2 アプリの骨格（`app/App.tsx`）

- **stage**: 1280×720 の論理 stage を `--ui-scale` で一様に拡縮する（`app/App.tsx`、`shared/uiScale`）。UITK では PanelSettings の Scale With Screen Size に置き換えられる見込み（推測）。
- **三つの族**（ADR 0013/0017）
  - stage 族: パネル類
  - viewport 族（`.viewportOverlay`）: 画面端に付く HUD。位置は実画面の端に置き、寸法だけ stage と同じ率で拡縮する
  - 背面 viewport 族: 通知。stage より奥に置き、`--ui-scale` を自分で掛ける
  - viewport 族は「位置は実画面、寸法は stage 拡縮」という二重の基準を持つ。UITK では、単一 PanelSettings の拡縮だけでは表せない可能性がある（推測）。
- **画面ルーティング**: C# の UIStateControl → topic `ui_state.current` → `screenForUiState`（`shared/uiState/uiScreenRouting.ts`）。`SCREEN_TRAITS` 表が各画面の性質（grab / pauseMenu / skitInput / backdrop / alwaysOnHud / trainHud）を持つ。画面は10種: none, playerInventory, subInventory, researchTree, buildMenu, challengeList, pauseMenu, trainHud, trainPause, skitPause。
- **同時に出る組み合わせ**（`app/App.tsx:95-107`）
  - `playerInventory` では InventoryPanel・RecipeViewer・ItemListPanel を同時に出す
  - `subInventory` では InventoryPanel と BlockInventoryPanel を同時に出す
  - `researchTree` も grab=true なので InventoryPanel が出る
  - GrabOverlay（掴んだアイテムの絵）は、これらの画面にまたがって1つだけある（`app/App.tsx:131`）
- **Portal 層**: ToastHost・SkitTransition・TutorialOverlay・WorldPinOverlay（`app/App.tsx` の `<Portal>`）。
- **CEF 固有の層**: 再接続オーバーレイ、辞書エラーのオーバーレイ、`AppErrorBoundary`。UITK では役目が無くなる見込み（推測）。
- **全体を隠す**: Ctrl+U（uiVisibility）とカットシーン中は、UI を丸ごと unmount する（`app/App.tsx:86`）。

### 1.3 重なり順（`app/tokens.css` の `--z-*`）

| 層 | 値 | UITK での置き換え（推測） |
| --- | ---: | --- |
| viewport-backdrop | 0 | ルート直下の兄弟順 |
| behind-stage（通知） | 1 | 同上 |
| stage | 2 | 同上 |
| stage-screen | 20 | stage 内の兄弟順 |
| overlay-panel / chrome | 30 / 31 | 同上 |
| grab / pause-menu | 40 | 同上 |
| portal-modal | 200 | モーダル層の要素（ルート直下で最後尾） |
| skit-transition | 210 | 同上 |
| stage-tooltip | 250 | ツールチップ層 |
| toast | 300 | トースト層 |
| tutorial | 900 | チュートリアル層 |
| reconnect | 2000 | 不要になる見込み |
| full-screen-gate | 2500 | 全画面ゲート層 |

USS には z-index が無い（PoC）。そのため、層ごとにルート直下の兄弟要素を固定順で置く「層マネージャ」を最初に作る必要がある。

### 1.4 データの流れ

- **topic と action**
  - topic は約30種（`bridge/transport/protocol.ts`）
  - action は `dispatchAction` / `dispatchActionOutcome` で送る。結果は accepted / rejected / unreachable の3値
  - 機能ごとの購読と送信は §3 の各節に書いた
- **アイコン**: HTTP URL から読む（itemIconUrl / blockIconUrl / fluidIconUrl）。UITK では Sprite・Texture2D を直接渡す形に変わる。
- **i18n 辞書**: HTTP fetch で取る（`shared/i18n/I18nProvider.tsx:93`。リトライと 409 staleRevision がある）。UITK では C# の `Client.Localization.Localize` を直接呼べる。
  - 3段のフォールバック（対象言語 → 英語 → Source → `[!key]`）は C# 側にも同じものがある（`moorestech_client/Assets/Scripts/Client.Localization/LocalizationTextResolver.cs`）。
  - **差分**: 文中の差し込み記法が違う。
    - Web は名前付きの `{count}` などを使う（`shared/i18n/i18nStore.ts:125`）
    - C# は位置指定の `{p0}` しか解釈しない（`LocalizationTextInterpolator.cs`）
    - 同じ `Localization/localization.csv` に両方が混在している（`{pN}` が190箇所、`{count}` が65箇所など）
    - したがって、C# 側に名前付きの差し込みを足すか、辞書を統一する必要がある
- **プレイテストの照会**: `bridge/playtest/domQueryResponder.ts:95` が `[data-testid=…]` で DOM を探す。プレイテスト DSL はこれに頼っているので、UITK では `name` かクラスで照会する代わりの仕組みが要る。

### 1.5 Mantine の使い方（薄い）

| 使っている部品 | 使っている場所 |
| --- | --- |
| Stack / Group / Text / Title / Box / Paper | blockInventory, challenge, eventLanguageGate, modal, pauseMenu, progress, recipe, settings, toast, trainHud, ui, app |
| ScrollArea | blockInventory, buildMenu, pauseMenu, recipe, ui(RecipeListScrollArea) |
| Button | challenge, eventLanguageGate, modal, pauseMenu, recipe, app |
| Modal / TextInput | modal |
| Notification | toast |
| Tooltip | ui(HoverTooltip) |
| Overlay / Portal / Loader | eventLanguageGate, app, tooltip |
| useViewportSize | tutorial(WorldPinOverlay) |

HTML5 の DnD は使っていない（`draggable={false}` だけ）。ドラッグはすべて pointer capture を使った自作。canvas も使っていない。SVG はインラインで5箇所にある（§5）。

---

## 2. 20機能の一覧

| 機能 | 何を表示・操作するか | 行数 ts / css（ファイル数） | vitest | 難易度 | 主な難所 |
| --- | --- | --- | ---: | :---: | --- |
| blockInventory | ブロックの中身・機械・発電・列車・時刻表 | 1,788 / 181（49） | 113 | **高** | 種類の多さ、時刻表、IME 入力、BarSeries、兄弟セレクタ |
| buildMenu | 建築物の一覧・検索・詳細 | 840 / 189（14） | 52 | **高** | スクロールスパイ、smooth scroll、ホットバーへの DnD、IME 検索 |
| recipe | アイテムのカタログとレシピ表示・長押しクラフト | 673 / 403（20） | 27 | **高** | コンテナクエリ、grid、擬似要素の装飾、ドラッグスクロール、長押し |
| tutorial | ハイライト枠・ドラッグ誘導・ワールドピン | 481 / 155（12） | 42 | **高** | アンカーの解決、祖先によるクリップ、脈動、SVG、viewport 座標 |
| inventory | プレイヤーのインベントリと装備 HUD | 396 / 33（9） | 34 | 中 | 掴んで追従、分割ドラッグ、ホイールでの装備選択 |
| hotbar | 9枠のホットバー | 182 / 105（5） | 10 | 中 | DnD の受け側（elementFromPoint）、マスク |
| research | 研究ツリーと詳細ペイン | 399 / 152（9） | 31 | 中 | TreeView、グラデのボタン |
| challenge | チャレンジツリーと目標 HUD | 119 / 58（6） | 0 | 中 | TreeView、Mantine の素の見た目 |
| skit | 会話窓・選択肢・ツールバー・暗転 | 315 / 247（10） | 5 | 中 | タイプライター、マスクフェード、SVG、擬似要素 |
| notification | 左端に積む獲得通知 | 312 / 89（5） | 38 | 中 | keyframes の入退場、集約したときの再生し直し |
| pauseMenu | ポーズ・設定・バグ報告 | 267 / 59（7） | 29 | 中 | textarea の IME、スクロール |
| modal | 確認と入力のモーダル | 83 / 0（3） | 8 | 中 | TextInput の IME、幕、フォーカス |
| modeHud | 配置 HUD と削除モードの帯 | 100 / 83（4） | 11 | 中 | 斜線帯（repeating-linear-gradient） |
| eventLanguageGate | 出展モードの言語選択ゲート | 216 / 12（5） | 9 | 低 | 全画面の不透明面、状態機械 |
| settings | 画質・言語・ダッシュ方式の切替 | 192 / 0（4） | 15 | 低 | ModeSwitch だけ |
| toast | 右下のトースト | 56 / 14（4） | 2 | 低 | 様式外の負債（moorestech-cgm） |
| progress | 採掘ゲージ | 29 / 29（3） | 0 | 低 | GaugeBar |
| keyHint | キーヒント | 33 / 25（3） | 4 | 低 | `<kbd>` の様式 |
| trainHud | 乗車中の分岐表示 | 31 / 11（3） | 0 | 低 | Mantine の素の見た目 |
| commonHud | クロスヘア | 10 / 14（3） | 3 | 低 | box-shadow |

難易度は次の観点で付けた。規模、§5 の CSS 難所の数、PoC で実証済みか（DnD・ツールチップ・グラフ・ListView・アニメ）、未検証領域（IME）に触れるか。

---

## 3. 機能ごとの詳細

### 3.1 inventory（中）

- **表示と操作**
  - 10列のメイングリッド
  - 左右クリック、Shift、ダブルクリック
  - 右ドラッグで配る、左ドラッグで分割（`splitDrag.ts`、window の mouseup）
  - 掴んだアイテムがマウスに追従する（`GrabOverlay.tsx`、window の mousemove）
  - 整理ボタン
  - 装備 HUD
    - ホイールで循環選択する（`EquipmentPanel/index.tsx`、`equipmentLogic.ts`）
    - pendingSelections を FIFO で持ち、revision で確認する
    - buildTool が占有している間はホイールを抑止する
- **主要ファイル**: `features/inventory/InventoryPanel/index.tsx`(73)、`GrabOverlay.tsx`(49)、`slotActions.ts`(101)、`splitDrag.ts`(13)、`EquipmentPanel/index.tsx`(127)、`equipmentLogic.ts`(30)
- **部品**: Mantine は無し。ItemSlot・SlotGrid・GamePanel・PanelActionButton・ConnectingPlaceholder を使う。
- **難所**: 掴んだ絵の追従（ルート直下の absolute 要素）、分割ドラッグの状態機械、ホイール入力の排他。
- **難易度は中**: PoC で DnD を約140行で実証済み。ロジックは `shared/itemMove`（純粋な関数）をそのまま C# へ移せる。
- **依存**
  - `features/blockInventory`（`slotActions.ts` が machineSlotGhosts / detailLogic を import している）、`shared/itemMove`、`ownedCounts`、`tutorialAnchor`、`ui`、`uiState`
  - topic: inventory, blockInventory, placementMode, uiState
  - action: inventory.select_equipment / sort / split_drag、itemMove 系（inventory.move_item / split / collect）
- **注意**
  - InventoryPanel は playerInventory（RecipeViewer・ItemListPanel と同時）と subInventory（BlockInventoryPanel と同時）の両方に出る（`app/App.tsx:95-107`）。
  - 掴んだアイテムはこれらのパネルをまたいで動く。したがって、inventory だけを先に UITK にすると、相手のパネル（CEF）との間で掴みが途切れる（推測。§7）。

### 3.2 hotbar（中）

- **表示と操作**
  - 9枠。中身は配置対象（ブロック・列車・接続ツール・BP）
  - タップで `hotbar.select`
  - D&D: `usePressGesture`（閾値5px）で始め、`document.elementFromPoint` と `data-hotbar-slot-index` で落ち先を判定する（`features/hotbar/useHotbarDragSource.ts:27`、`hotbarDnd.ts`）。assign / swap / clear の3通り
  - ビルドメニューの項目がドラッグ元になる
  - 数字キーは Unity 側で処理する
- **主要ファイル**: `HotbarPanel/index.tsx`(83)、`hotbarDnd.ts`(57)、`useHotbarDragSource.ts`(40)、CSS 105
- **部品**: Mantine は無し。SlotFrame・PlacementTargetFace を使う。CSS にはベベルの変数・選択時のシアン・番号タグ・mask がある。
- **難易度は中**: `elementFromPoint` は `panel.Pick()` に置き換えられる。PoC の罠「捕捉中も下の要素に PointerEnter が届く」に気をつける。
- **依存**
  - `placementTarget`、`pointerGesture`、`tutorialAnchor`、`ui`、`uiState`
  - topic: hotbar, uiState
  - action: hotbar.select
  - buildMenu から import される（DnD の受け側）

### 3.3 buildMenu（高）

- **表示と操作**: 3カラム。
  - 左: カテゴリのサイドバー（縦の ModeSwitch）
  - 中: 検索 `<input>` と、1本のスクロールに並ぶカテゴリ群（SlotGrid 8列）
  - 右: ホバーで固定表示される詳細サイドバー
  - その他
    - スクロールスパイ（`hooks/useBuildMenuCategoryScroll.ts`、305行）
    - ResizeObserver（:166）
    - smooth scrollTo によるジャンプ（:134, :301）と末尾のスペーサ
    - セッション内で scrollTop / 検索語 / ホバーを保存する
    - BP は右クリックで削除する
    - ホットバーへ D&D できる
    - 不足しているときは HoverTooltip を出す
- **主要ファイル**: `BuildMenuPanel`(120)、`hooks/useBuildMenuCategoryScroll.ts`(305)、`style.module.css`(189)
- **部品**: Mantine は ScrollArea。自前は GamePanel・ItemSlot・SlotFrame・SlotGrid・HoverTooltip・IconButton・FadeRule・ModeSwitch・PlacementTargetFace。
- **難所**
  - 検索欄の IME（未検証領域）
  - スクロール位置から見出しを判定する（ScrollView の scrollOffset と子の layout から計算する）
  - smooth scroll のアニメを自作する
  - CSS grid（8列）
  - スクロールバーの様式（§8.10）と、チュートリアルのために取る逃げの余白
- **難易度は高**: 規模に加えて、スクロール・DnD・IME の3つを同時に抱える。PoC は ListView を実証したが、スクロールスパイは未実証。
- **依存**
  - `features/hotbar`（DnD の受け側）、`materialTooltipText`、`placementTarget`、`tutorialAnchor`、`ui`
  - topic: buildMenu
  - action: build_menu.select, blueprint.delete, ui_state.request

### 3.4 blockInventory（高）

- **表示と操作**
  - blockType のレジストリで画面を振り分ける
    - FilterSplitter
    - Shaft / Gear / GearBeltConveyor → SectionStackView
    - ElectricToGearGenerator
    - TrainStation / ItemPlatform / FluidPlatform
    - ElectricPole
  - 汎用の SectionStackView（76行）は configByBlockType で決まる。対象はチェスト・機械・発電機・採掘機・ポンプ・パイプ
  - セクションの種類
    - Machine: レシピ選択モードとインベントリモードの切替、RecipeListScrollArea、ゴーストスロット、ProgressArrowGlyph、モジュールスロット、分間生産数
    - Miner、Pump
    - Generator（GaugeBar）
    - GearToElectric（HoverTooltip）
    - BeltThroughput（BarSeries。←/→ キーで窓を切り替える）
    - Gear、ElectricNetwork / GearNetwork、FluidInventoryContents
  - FilterSplitter は3モードの ModeSwitch とフィルタスロット。ElectricToGear は縦の ModeSwitch と GaugeBar
  - TrainStationNameSection: 素の `<input>` と決定ボタン
  - 列車インベントリ: ModeSwitch のタブ（インベントリ / 時刻表）と Mantine ScrollArea
  - 時刻表
    - 停車駅リストに ↑↓× の操作がある。駅を追加できる。自動運転の ModeSwitch がある
    - `useImmediateTimetable` で楽観更新する（タイムアウトは DEFAULT_ACTION_TIMEOUT_MS）
  - Esc で閉じる（BlockInventoryKeyHandler）。× の IconButton がある
- **主要ファイル**: `BlockInventoryPanel`(92)、`registry`、`SectionStackView`(76)、`details/*`、`train/*`。合計1,788行、49ファイル。
- **部品**: Mantine は Box / Group / ScrollArea / Stack / Text。自前は GamePanel・ItemSlot・SlotGrid・HoverTooltip・IconButton・PanelActionButton・FadeRule・GaugeBar・ProgressArrowGlyph・ProgressArrowBar・FluidSlotRow・FluidAmountSlot・FluidIcon・BarSeries・RecipeListScrollArea・ModeSwitch（5ファイル）。
- **難所**
  - `.row > span:first-child` と `.list > .row + .row`（`features/blockInventory/train/style.module.css:43, :54`）。USS に無いので C# でクラスを付ける
  - grid（machineInventoryBody）
  - BarSeries（Painter2D。PoC で 18系列×300点を1.5ms で描けた）
  - 駅名入力の IME
  - 楽観更新とタイムアウト
- **難易度は高**: 種類の数（セクションが11種以上）と規模が最大。個々の部品は中難度だが、共通部品（§6）が揃っていないと着手できない。
- **依存**
  - `features/recipe`（RecipeRow）、`clamp`、`itemMove`、`tutorialAnchor`、`ui`、`uiState`
  - topic: blockInventory, inventory, machineRecipes
  - action: machine_recipe.select, filter_splitter.set_mode / set_filter_item, electric_to_gear.set_output_mode, train_platform.set_transfer_mode, train_station.set_name, train_timetable.open / replace / set_auto_run, ui_state.request、itemMove 系（block_inventory.move_item / split / collect）

### 3.5 recipe（高）

- **表示と操作**
  - 右: 6列のアイテムカタログ（`ItemListPanel`、83行と CSS 85行）
    - ドラッグでスクロールし（`useDragScroll`）、タップで選択する
    - 制作可能数を表示する
  - 中央: レシピの単一リスト（`RecipeViewer`、`RecipeContent`）
    - クラフトは長押し（`logic/useHoldCraft.ts`。rAF で craftTime ごとに発火する。:66）。Enter でもできる
    - 進捗は矢印グリフで出す
    - 機械レシピはブロックアイコンを表示する
    - 素材をクリックするとそのアイテムへ移動する
    - ゲームレイヤーの Esc で選択を解除する
- **主要ファイル**: `RecipeBox.module.css`(212)、`CraftRecipeEntry`(84)、`ItemListPanel`(83)、`useHoldCraft.ts`(82)、`RecipeContent`(75)、`craftLogic`(55)、`RecipeRow`(53)、`MachineRecipeEntry`(49)、`useDragScroll`(30)、`recipeSlotLayout`(27)
- **部品**: Mantine は Box / Button / ScrollArea / Stack / Text。自前は GamePanel・ItemSlot・SlotGrid・ProgressArrowGlyph・BlockIcon・RecipeListScrollArea・ConnectingPlaceholder。
- **難所**
  - レシピ行の寸法をコンテナクエリ（`container-type: inline-size` と cqw）で決めている（`RecipeBox.module.css:49, :99`、`recipeSlotLayout`）。USS には無いので、GeometryChangedEvent を受けて C# で計算する
  - 3カラムの grid（:8, :17）
  - 四隅のブラケットを `::before` / `::after` と4枚のグラデで描いている（:110-131）
  - 外周リングの box-shadow（:33）、`filter: drop-shadow`（:118）、クラフトボタンの `:hover` グラデ（:177）
  - 見出しの罫線は段付きのグラデ（`ItemHeader.module.css:57`）
- **難易度は高**: 装飾の再現（Painter2D か子要素の追加）と、レイアウトの計算を C# へ移す作業が重なる。
- **依存**
  - `clamp`、`ownedCounts`、`pointerGesture`、`tutorialAnchor`（`recipe.craft-button` はマスタから参照されている）、`ui`、`uiState`
  - topic: craftRecipes, inventory, itemList, machineRecipes
  - action: craft.execute
  - blockInventory から import される（RecipeRow）

### 3.6 research（中）

- **表示と操作**
  - TreeView によるパン・ズームのノードグラフ。ノードは4状態（data-locked / 無印 / data-ready / data-completed。ADR 0014・0044）
  - 右上に浮く詳細ペイン（GamePanel craft）
    - 消費アイテムの不足表示（shortage）
    - 解放セクション（ブロック / 機械レシピ / アイテム / 報酬 / その他）
    - 研究ボタン（青グラデ）と、押せない理由
  - 初期フォーカスを当てる。ビューポートはセッション内で保存する（`docs/design/components.md` §8.5）
- **主要ファイル**: `ResearchTreePanel`(63)、`ResearchNodeCard`(42)、`ResearchDetailPane`(77)、`researchLogic`(77)、`UnlockSections`(96)、`style`(152)
- **部品**: Mantine は無し。GamePanel・ItemSlot・BlockSlot・FluidSlot・TreeView を使う。
- **難所**: TreeView（§4）、グラデのボタン、研究ノードのアンカー（`research.node-<guid>`。マスタの challenges.json から7箇所で参照されている）。
- **難易度は中**: PoC のレシピ図でパン・ズームと線の描画を実証済み。
- **依存**
  - `ownedCounts`、`placementTarget`、`treeView`、`tutorialAnchor`、`ui`
  - topic: researchTree, inventory
  - action: research.complete
  - 研究画面は全画面 UI の例外で、alwaysOnHud=false

### 3.7 challenge（中）

- **表示と操作**
  - カテゴリのボタン列（Mantine の Button filled / subtle。横にスクロールする）
  - TreeView のノード（Paper と scale 変換）
  - 常時表示の目標 HUD（`CurrentChallengeHud`、42行。GamePanel hud と FadeRule）。blocking のスキット中は隠す
- **主要ファイル**: `ChallengePanel`(37)、`ChallengeNodeCard`(38)、`CurrentChallengeHud`(42)、CSS 44
- **部品**: Mantine は Button / Group / Paper / Text / Title。CSS が `--mantine-color-dark-8` などを直接使っている（repo 全体で Mantine の CSS 変数を参照しているのは13箇所）。
- **難所**: TreeView、Mantine の素の見た目を様式化し直すかどうか（§9）。
- **難易度は中**: 規模は小さいが、TreeView に依存する。vitest は0件で、受入条件の資料は e2e だけ（challenge.spec 系）。
- **依存**
  - `treeView`、`tutorialAnchor`（`challenge.current-hud` はマスタから参照されている）、`ui`、`uiState`
  - topic: challengeTree, challengeCurrent

### 3.8 skit（中）

- **表示と操作**
  - blocking の会話窓（GamePanel skit）
    - 上端をマスクでフェードする（`shared/ui/GamePanel/style.module.css:71`）
    - タイプライター表示（`SkitPresentation.tsx:41` の setInterval）
    - クリックか Enter で全文表示、または次へ進む
    - 送り待ちのシェブロンを出す
  - 選択肢の板（`SkitChoiceList.module.css`）
    - `::before` の面に左右の α マスクを掛ける（:33-38）
    - `::after` に菱形を置き、`:hover` と `:focus-visible` で変わる（:44, :84）
  - ツールバー: 自動送り・スキップ・UI 非表示のアイコンボタン。SVG 6種（`icons.tsx`、73行）
  - UI 復帰ボタン、背景スキットの1行表示、暗転（全画面の黒。`SkitTransition`）
  - interactive=false のときは pointer-events を全部止める
- **主要ファイル**: `SkitPresentation`(125)、`SkitToolbar`(55)、`SkitChoiceList`(29)、`icons.tsx`(73)、CSS 95+97+55、`app/skitTokens.css`（38トークン）
- **部品**: Mantine は無し。GamePanel・IconButton・FadeRule を使う。
- **難所**: マスクフェード、擬似要素、SVG アイコン、タイプライター。
- **難易度は中**: 文字送りは schedule.Every で書ける。マスクと SVG は Painter2D か VectorImage で置き換える（§5）。
- **依存**
  - `i18n`、`ui`
  - topic: skitPresentation
  - action: skit.advance / select / set_auto / set_ui_hidden / skip
  - blocking 中は challenge の HUD を隠す（`useBlockingSkitActive`）

### 3.9 notification（中）

- **表示と操作**
  - 画面の左端・縦中央に column-reverse で積む
  - 入場と退場を `@keyframes` で動かす（`style.module.css:67, :79`。スライドとフェード）
  - 生存時間 7000ms から退場の遅延を逆算する
  - 獲得通知は集約する。集約したら `getAnimations()` でアニメを頭から回し直す（`NotificationHost.tsx:68`）
  - animationEnd で除去し、保険のタイマーも持つ
  - アイテムアイコンを出す。saveMigration の通知は重複を除く
- **主要ファイル**: `NotificationHost`(91)、`notificationMessages`(139)、`notificationStore`(80)、CSS 89
- **部品**: Mantine は無し。ItemIcon を使う。
- **難所**: keyframes の代わり（experimental.animation か自作の tween）、再生し直し、背面 viewport 族での拡縮。
- **難易度は中**: PoC でアニメの基礎を実証済み。罠は「height:auto は transition できない」と「高さ0の親の中で子の文字が潰れる」で、どちらもこの画面に効く。ADR 0016 が仕様の正本。
- **依存**
  - `i18n`、`ui`
  - topic: notification（event）

### 3.10 pauseMenu（中）

- **表示と操作**
  - トップ: セーブ / セーブして終了 / 設定 / バグ報告
  - サブページ: Mantine ScrollArea と戻る
  - バグ報告
    - 種別の ModeSwitch
    - `<textarea>`（IME が要る）
    - 送信ボタン、配送状況の1行
    - トースト（`BugReportForm.tsx:46-47`）
- **主要ファイル**: `BugReportForm`(143)、`PauseMenuPanel`(37)、`TopPage`(34)、`SubPage`(26)、`useBugReportDraft`(26)
- **部品**: Mantine は Button / ScrollArea / Stack / Text / Title。自前は PanelActionButton・ModeSwitch。
- **難所**: 複数行の TextField と IME、スクロールバーの様式。
- **難易度は中**
- **依存**
  - `features/settings`、`features/toast`、`tutorialAnchor`、`ui`
  - topic: pauseMenu
  - action: pause_menu.save / save_and_quit / show_page、bug_report.submit

### 3.11 modal（中）

- **表示と操作**: Mantine Modal（遷移なし）。入力モーダルでは TextInput を autoFocus にする。確認ボタン（error のときは赤）。`ui.modal.respond` を送る。
- **主要ファイル**: `ModalHost`(57)、`modalLogic`(25)
- **部品**: Mantine の Modal / TextInput / Button / Text。自前の部品は無い。
- **難所**: 背景の幕、フォーカスを中に閉じ込める、Esc、IME。
- **難易度は中**: 規模は小さい。ただしモーダル層そのものを作る必要があり、IME は未検証。
- **依存**: topic modal、action ui.modal.respond。

### 3.12 modeHud（中）

- **表示と操作**
  - 配置 HUD: GamePanel craft に高さと、置けない理由を出す（`PlacementModeHud`、68行）
  - 削除モード: 上下に黄黒の斜線帯（`DeleteModeWarningBands`、30行。`style.module.css:68` の repeating-linear-gradient）と DeleteBoxHeightHud
- **部品**: GamePanel・FadeRule・PlacementTargetFace（placementTarget 経由）。
- **難所**: 斜線帯は、繰り返しテクスチャか Painter2D で描く。画像アセット禁止の規則に触れる（§9）。
- **難易度は中**（帯だけが難所で、ほかは低）
- **依存**
  - `placementTarget`、`tutorialAnchor`、`ui`
  - topic: placementMode, deleteMode

### 3.13 eventLanguageGate（低）

- **表示と操作**
  - 出展モードで出す全画面の不透明ゲート（Mantine Overlay / Portal）
  - 言語ボタン（Button xl）
  - 応答の状態機械（`useLanguageSelectionAnswer`、125行。押せない状態、受理後10秒の閉じ待ち、閉じない / 切断 / 拒否の1行）
  - 辞書を通さない文言を使う（`DictionaryIndependentText`。components.md §8.20）
- **難易度は低**: 見た目が単純。状態機械はそのまま C# へ移せる。
- **依存**
  - `i18n`（辞書前の文言表 `preDictionaryText.ts`）
  - 表示は App が topic eventLanguageGate を直接購読して決める

### 3.14 settings（低）

- **表示と操作**: 画質プリセットと個別設定（`GraphicsQualitySettings`、115行）、言語（`LanguageSelect`、42行）、ダッシュ方式（`SprintModeSelect`、32行）。どれも ModeSwitch。
- **部品**: Mantine は Title / Text。自前は ModeSwitch。
- **難易度は低**
- **依存**
  - topic: graphicsQuality, localization, sprintInputMode
  - action: settings.applyGraphicsQualityPreset / setGraphicsQuality / setSprintInputMode、localization.setLocale
  - pauseMenu の中に出る

### 3.15 toast（低）

- **表示と操作**: Mantine Notification を右下に出し、3秒で消す（`ToastHost`、21行。`toastStore`、33行）。
- **呼び出し元**
  - bridge が action の失敗を伝える経路（`main.tsx:17` の `setToastSink(emitToast)`）
  - BugReportForm
- **難易度は低**: 様式外の負債（moorestech-cgm）を、移行のついでに様式化するかは要裁定（§9）。

### 3.16 progress（低）

- 採掘ゲージ（GaugeBar）とラベル（`ProgressBar`、28行）。ホットバーの床を基準に積む（`app/App.tsx` の viewport 族）。依存は topic progress、`tutorialAnchor`、`ui`。

### 3.17 keyHint（低）

- C# の `uiState.keyHints` を `<kbd>` で積む（`KeyHintHud`、32行）。keyHintText クラスは3画面が共有する1宣言（`app/tokens.css:442`）。依存は uiState。

### 3.18 trainHud（低）

- 乗車中の分岐の選択を表示する（`TrainRidingHud`、30行）。Mantine の Paper / Title / Text のまま、様式化されていない。依存は topic trainRiding、`tutorialAnchor`。

### 3.19 commonHud（低）

- 6px の点のクロスヘア（box-shadow）。依存は topic crosshair、`tutorialAnchor`。

### 3.20 tutorial（高）

- **表示と操作**
  - アンカーのハイライト枠（`TutorialOverlay`、193行）
    - border と box-shadow のグロー
    - clip-path inset で、祖先のクリップと同じ形に切る
    - 脈動（`app/tokens.css:430-440` の keyframes。scale 1.03 / 1.08）
    - ラベル（`HighlightLabel`）は上下を反転し、右端で押し戻す
  - D&D 誘導の矢印（`DragGuide`）
    - 2つのアンカーの間を、CSS 変数を使った keyframes でループする。filter drop-shadow を掛ける
    - `tutorial.anchor_ack` を送る
  - ワールドピン（`WorldPinOverlay`、83行）: 画面内ではピンの SVG、画面外では画面端に矢印をクランプする。useViewportSize を使う
  - キー操作のヒント（`KeyControlHintHud`、52行。LocalizedShortcutHint）
- **難所**
  - アンカーの解決（§4 tutorialAnchor）
  - 祖先によるクリップの交差計算
  - 脈動と誘導のループアニメ
  - SVG
  - viewport 座標と stage 拡縮の混在
- **難易度は高**: 13機能に置かれたアンカーへ横断的に依存する。strangler の途中では、CEF と UITK にまたがってハイライトを出す必要がある（§7）。
- **依存**
  - `tutorialAnchor`、`uiScale`、`uiState`
  - topic: tutorialPresentation, worldPins, uiState
  - action: tutorial.anchor_ack

---

## 4. shared/ の共通部品

| 部品 | 行数 ts / css（ファイル数） | vitest | 中身 | UITK での扱い（推測を含む） | 難易度 |
| --- | --- | ---: | --- | --- | :---: |
| ui | 1,037 / 805（51） | 59 | パネル枠・スロット・ボタン・ゲージ・アイコン（下表） | 部品ごとに VisualElement の派生と USS を作る。§6 の中核 | 中〜高 |
| i18n | 428 / 0（8） | 55 | 3段のフォールバック、`[!key]`、`{name}` の差し込み、HTTP fetch、`LocalizedShortcutHint`（`` マーカーに kbd を差し込む）、辞書前の文言表 | C# の `Localize` へ寄せる。名前付きの差し込みと kbd の差し込みを C# に足す（§1.4） | 中 |
| treeView | 408 / 25（10） | 27 | CSS transform の平行移動と拡縮、div を回転させた線、wheel ズーム、慣性（`usePanInertia.ts`、τ=325ms）、拡大率 0.4〜2.5 | transform と Painter2D の線で作る。PoC で実証済み | 中 |
| tutorialAnchor | 358 / 0（7） | 11 | `anchorRegistry.ts`（Mutation・Resize・IntersectionObserver と rAF。:11-13）、`ancestorClip.ts`（祖先の overflow の padding box を交差させる）、`anchorIds.ts`（静的 ID 19個と動的 prefix 6個）、`data-tutorial-anchor` の空白区切り | VisualElement の worldBound と、祖先の overflow:hidden をたどって作り直す。登録は AttachToPanel / DetachFromPanel で行う | 中〜高 |
| uiState | 304 / 0（8） | 26 | 画面ルーティング、activeLayer（modal > blockInventory > research > buildMenu > game）、入力の排他（`useWebInputExclusivity.ts`）、ゲームレイヤーのキーとホイール、grab の可否、blocking スキット | C# の UIStateControl に寄せる。pointerOverUi は panel.Pick で判定する | 中 |
| itemMove | 191 / 0（6） | 30 | 純粋なプランナ（planPlayer / BlockLeft / Right / DoubleClick、planDirectMoves） | そのまま C# へ移す。テストも移せる | 低 |
| pointerGesture | 141 / 0（2） | 5 | `usePressGesture`: pointer capture、5px の閾値、tap / drag / abort | CapturePointer で作る。DnD・パン・ドラッグスクロールで共通の状態機械にする | 低〜中 |
| tooltip | 76 / 18（4） | 7 | `CursorTooltip`（topic ui.tooltip を Portal に出し、画面端でクランプする。OFFSET 12） | ツールチップ層。PoC で約90行 | 低〜中 |
| placementTarget | 45 / 0（2） | 9 | 配置対象の表示名を解決する | C# のロジックへ | 低 |
| uiScale | 17 / 0（2） | 0 | zustand の store | PanelSettings に任せる | 低 |

### 4.1 `shared/ui` の内訳（主なもの）

| 部品 | 行数 | 使う機能 | 再現の難所 |
| --- | --- | --- | --- |
| GamePanel（variant: default / craft / skit / hud） | 74 + CSS 216 + hudVariant 27 | 8機能: blockInventory, buildMenu, challenge, inventory, modeHud, recipe, research, skit | 背景は `::before` のグラデ面。ほかに mask-image の合成（:36, hud:21）、外側の box-shadow（:33）、craft の inset box-shadow 2層（:85）、右下グリップ（clip-path :100）、下向き三角（clip-path :215）、タイトルの scale |
| ItemSlot | 114 | 5機能: blockInventory, buildMenu, inventory, recipe, research | HoverTooltip、ゴースト、catalog、shortage（所持 / 必要）、文字の縁取り |
| SlotFrame | 55 + CSS 82 | buildMenu, hotbar（ItemSlot 経由では全スロット） | 3層ベベルの inset box-shadow（:27）、data 属性で表す状態、選択時のシアン、下端のグラデトリム |
| useSlotMouse | 28 | ItemSlot 経由 | 左右の mousedown、buttons マスクの enter、contextmenu の抑止 |
| GameIcon / ItemIcon / BlockIcon / FluidIcon | 73 ほか | notification, recipe, blockInventory ほか | img の onError で代わりの絵に切り替えて固定する → Sprite を直接渡せば不要になる |
| HoverTooltip | 79 | blockInventory, buildMenu（ItemSlot 経由では5機能） | Mantine Tooltip。祖先がスクロールしたら引っ込める |
| ModeSwitch | 62 + CSS 61 | 4機能: blockInventory, buildMenu, pauseMenu, settings | グラデ、box-shadow、`:focus-visible` |
| IconButton | 44 | 3機能: blockInventory, buildMenu, skit | × の SVG、grid、`:hover` |
| PanelActionButton | 23 | 3機能: blockInventory, inventory, pauseMenu | `:hover` |
| FadeRule | 小 | 5機能: blockInventory, buildMenu, challenge, modeHud, skit | 両端がフェードする水平グラデ（`FadeRule/style.module.css:6`） |
| GaugeBar | 小 | blockInventory, progress | 溝と充填 |
| ProgressArrowGlyph / ProgressArrowBar | 57 ほか | blockInventory, recipe | SVG の clipPath で充填する |
| BarSeries | 小 | blockInventory | 系列の棒グラフ → Painter2D |
| FluidSlot / FluidAmountSlot / FluidSlotRow | 51 / 54 / 30 | research, blockInventory | 色の付いた背面フィル |
| SlotGrid | 27 | 4機能: blockInventory, buildMenu, inventory, recipe | CSS の grid repeat → flex-wrap と固定幅 |
| RecipeListScrollArea | 27 | blockInventory, recipe | Mantine ScrollArea と §8.10 の様式 |
| PlacementTargetFace / BlockSlot / ConnectingPlaceholder | 小 | buildMenu, hotbar / research / inventory, recipe | — |

---

## 5. 横断の難所: CSS の機能と USS の対応

| CSS の機能 | 使っている所（根拠） | USS での扱い |
| --- | --- | --- |
| linear / radial-gradient の面 | GamePanel、FadeRule、ModeSwitch、SlotFrame、research のボタン、recipe の craftButton（`RecipeBox.module.css:177`）、ItemHeader（:57）、SkitChoiceList、App のヴィネット（radial） | USS に無い。Painter2D（generateVisualContent）か 9-slice テクスチャで描く（PoC「グラデは Painter2D かテクスチャで代替」） |
| repeating-linear-gradient | modeHud の斜線帯（`features/modeHud/style.module.css:68`） | 繰り返しのテクスチャか Painter2D |
| mask-image | GamePanel（:36, :71, :206）、hudVariant（:21）、SkitChoiceList（:37）、hotbar | USS に無い。面を Painter2D で描き、頂点色の α でフェードを作る（推測） |
| clip-path | GamePanel（:100, :215 の三角）、tutorial overlay（inset）、tokens | USS に無い。三角は Painter2D、矩形のクリップは overflow:hidden の入れ子にする |
| box-shadow（外側、inset の多層） | GamePanel（:33, :85）、SlotFrame（:27）、RecipeBox（:33）、Crosshair、tutorial のグロー | USS に無い。border の重ねか Painter2D。外側のぼかし影は `filter: drop-shadow` で代わりになるか要確認（PoC で filter が使えるのは確認済み） |
| `::before` / `::after` | GamePanel、RecipeBox（:110-131）、ItemHeader（:60）、SkitChoiceList（:33, :44） | USS に擬似要素は無い。子の VisualElement を明示的に足す |
| `:hover` / `:active` / `:disabled` | SkitToolbar、SkitChoiceList、RecipeBox、IconButton、PanelActionButton | USS にある。低難度 |
| `:focus-visible` | ModeSwitch、SkitChoiceList（:85） | USS に無い見込み（推測）。`:focus` で代える |
| `:first-child`、隣接兄弟 `+` | `features/blockInventory/train/style.module.css:43, :54` | USS に無い（PoC）。C# でクラスを付ける |
| `display: grid` | App、blockInventory の machineInventoryBody、RecipeBox（:8, :17）、buildMenu、IconButton、SlotGrid | USS に無い（PoC）。flex-wrap と固定幅、または C# で計算する |
| `gap` | 多数 | USS に無い（PoC）。margin で代える |
| コンテナクエリ（cqw） | recipe（`RecipeBox.module.css:49, :99`、recipeSlotLayout） | USS に無い。GeometryChangedEvent を受けて C# で計算する |
| `z-index` | ほぼすべて | USS に無い（PoC）。兄弟順・BringToFront・層マネージャ（§1.3） |
| `position: fixed` | CursorTooltip、HoverTooltip、GrabOverlay | USS に無い。ルート直下の absolute 層に置く |
| `@keyframes` | 脈動（`app/tokens.css:430-440`）、tutorial overlay、notification（:67, :79） | USS に無い。experimental.animation か自作の tween（PoC 実証済み） |
| CSS の `transition` | **使っていない**（grep で0件。語として出るのは暗転のクラス名とトークン名だけ） | — |
| `filter: drop-shadow` | SkitToolbar、tutorial、worldPin、RecipeBox（:118）、ProgressArrowGlyph | PoC「filter は使える」 |
| `-webkit-text-stroke` | アイコン上の個数（iconTextOutline） | `-unity-text-outline`（PoC で可） |
| `calc()` と `var(--ui-scale)` | HoverTooltip、tooltip、notification | USS は var() だけで calc() は無い見込み（推測。PoC の報告に記載なし）。拡縮は PanelSettings に任せて calc を消す |
| インライン SVG | skit/icons.tsx（6種）、IconButton の ×、ProgressArrowGlyph（clipPath で充填）、DragGuide、WorldPinOverlay | VectorImage アセットか Painter2D。画像アセット禁止の規則に触れる（§9） |
| `pointer-events: none` | tooltip、skit の interactive=false | `pickingMode = Ignore` |
| 半透明の色 `rgb(… / α%)` | `app/tokens.css` に少なくとも11行 | Linear 色空間だと Web より薄く出る（PoC）。値の調整し直しが要る |
| 10〜13px の小さな文字 | 18宣言（10px×4、10.565px×1、11px×1、12px×8、13px×4）。例: `--inventory-count-base-font-size`、`--fluid-amount-font-size` | 11〜12px の和文は Chrome より細い（PoC）。画質の確認が要る |
| ScrollArea のスクロールバー | §8.10。ItemListPanel、RecipeViewer、buildMenu、TreeView にチュートリアルの逃げの余白がある | ScrollView の Scroller を USS で塗る（暗色テーマの USS に含める）。逃げの余白は padding と負のマージンで同じ形にする |
| smooth な `scrollTo` | `useBuildMenuCategoryScroll.ts:134, :301` | 自作の tween で scrollOffset を動かす |
| Resize / Mutation / IntersectionObserver | `anchorRegistry.ts:11-13`、`useBuildMenuCategoryScroll.ts:166` | GeometryChangedEvent、AttachToPanelEvent、必要ならポーリング |
| `document.elementFromPoint` | `useHotbarDragSource.ts:27` | `panel.Pick(position)` |
| `element.getAnimations()` | `NotificationHost.tsx:68` | 自作のアニメ基盤に「頭から回し直す」API を持たせる |
| rAF / setInterval | `useHoldCraft.ts:66`、`usePanInertia.ts:68`、`SkitPresentation.tsx:41` | `schedule.Execute().Every()`。UI の演出は実時間でよい。GameUpdater の規約はサーバーロジックだけが対象 |
| 文字の入力欄（IME） | BuildMenuSearchInput、TrainStationNameSection、ModalHost の TextInput、BugReportForm の textarea（4箇所） | TextField。**IME は PoC で未検証** |

`backdrop-filter` はどこにも使っていない。そのため、PoC が「素直にはできない」とした2点のうち1点は、この移行には影響しない。もう1点の `:nth-child` / `:last-child` に当たるのは blockInventory/train の2箇所だけ。

---

## 6. UI Toolkit 側に先に作るべき共通部品

「使う機能の数」は §3 の import 調べと topic 調べに基づく。

| # | 部品 | 中身 | 使う機能（数） | PoC の状況 | 置き時 |
| --- | --- | --- | --- | --- | --- |
| 1 | **デザイントークン USS** | `app/tokens.css`（192変数）、`skitTokens.css`（38）、`slotTokens.css`（8）を USS の変数へ移す。半透明の値は Linear 用に調整し直す | 全20 | 暗色テーマの USS（146行）を実証済み | 最初 |
| 2 | **フォント** | MooresUI JP（NotoSansJP-Medium）と MooresUI（NotoSans-Regular）の SDF FontAsset。グリフ単位のフォールバック。合成 bold は使わない | 全20 | `FontAsset.CreateFontAsset` を実証済み。小さい文字は細い | 最初 |
| 3 | **層マネージャと画面ルーティング** | ルート直下に固定順の層（§1.3）。`screenForUiState` と SCREEN_TRAITS を C# 化する。Ctrl+U とカットシーンでの全体非表示。stage 拡縮と viewport 族を分ける | 全20 | z-index の代替（兄弟順）を確認済み | 最初 |
| 4 | **i18n の結線** | C# の `Localize` を直接呼ぶ。名前付きの差し込み `{count}` を足す。kbd の差し込み（LocalizedShortcutHint）。辞書前の文言表。辞書の切替で再描画する | 16（modal, toast, commonHud, progress 以外） | 未実証 | 最初 |
| 5 | **入力の排他** | pointerOverUi（panel.Pick）、文字入力中のフォーカス、Tab と Space を封じる（`.decisions` 2026-10-01）、文字入力中の Esc で blur、activeLayer の優先順位 | 全体（uiState を直接使うのは7機能） | 未実証（ゲームパッドとフォーカス移動も未検証） | 最初 |
| 6 | **チュートリアルアンカーの登録表** | anchorId（静的19個と動的 prefix 6個）を VisualElement に登録する。worldBound と祖先クリップの交差を計算する。マスタの `highLightAnchorId`（`moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/challenges.json`: `research.node-*` が7、`recipe.craft-button`、`challenge.current-hud`）との契約を保つ | 13（blockInventory, buildMenu, challenge, commonHud, hotbar, inventory, modeHud, pauseMenu, progress, recipe, research, trainHud, tutorial） | 未実証 | 最初（どの画面にもアンカーがあるため） |
| 7 | **プレイテスト照会の代替** | `data-testid` を VisualElement の `name` に移し、domQueryResponder 相当を作る | e2e と DSL を持つ全機能 | 未実証 | 最初 |
| 8 | **GamePanel 枠**（4 variant） | Painter2D でグラデ面、端のフェード、inset 2層、右下グリップ、下向き三角、タイトル罫線を描く | 8（blockInventory, buildMenu, challenge, inventory, modeHud, recipe, research, skit） | Painter2D でのグラデは実証済み | 第一弾の画面と同時 |
| 9 | **スロット系**（SlotFrame、ItemSlot、アイコン、個数の縁取り、ゴースト、不足表示） | 3層ベベル、選択時のシアン、`-unity-text-outline`、Sprite を直接表示 | 7（blockInventory, buildMenu, inventory, recipe, research, hotbar, notification） | 縁取りは実証済み | 第一弾の画面と同時 |
| 10 | **SlotGrid**（grid の代わり） | flex-wrap と固定セル幅で N 列 | 4（blockInventory, buildMenu, inventory, recipe） | flex-wrap と margin を確認済み | 第一弾の画面と同時 |
| 11 | **ボタン類**（PanelActionButton、IconButton、主要アクションの青グラデ、確認ボタンの赤、Mantine Button の代わり） | `:hover` / `:active` / `:disabled`、SVG の × | 10（blockInventory, buildMenu, skit, inventory, pauseMenu, challenge, eventLanguageGate, modal, research, recipe） | — | 第一弾の画面と同時 |
| 12 | **ツールチップ層** | HoverTooltip（祖先がスクロールしたら引っ込める）と CursorTooltip（topic ui.tooltip、画面端でクランプ）。書式は ADR 0019（18px、padding 6/10、max-width 320） | 5（blockInventory, buildMenu, inventory, recipe, research）とアプリ全体の CursorTooltip | 約90行で実証済み | 第一弾の画面と同時 |
| 13 | **press ジェスチャと DnD 層** | 閾値付きの tap / drag / abort、CapturePointer、panel.Pick による落ち先の判定、掴んだアイテムの追従層（GrabOverlay）、分割ドラッグ | 7（inventory, blockInventory, hotbar, buildMenu, recipe, research, challenge） | 約140行で実証済み。PointerEnter の罠あり | 第一弾の画面と同時 |
| 14 | **スクロール領域** | ScrollView に §8.10 の様式（ネイビーのノブ、type=auto 相当、チュートリアルの逃げ、装飾が外へはみ出す分の余白）、smooth scroll、ドラッグスクロール | 5（blockInventory, buildMenu, pauseMenu, recipe, challenge の横スクロール） | ListView の1万行を実証済み | 2弾目以降 |
| 15 | **ModeSwitch** | 横と縦、tablist の役割、グラデ、フォーカス表現 | 4（blockInventory, buildMenu, pauseMenu, settings） | — | 2弾目以降 |
| 16 | **テキスト入力** | TextField の暗色の様式（§8.9 の検索欄）、複数行、IME、フォーカスで入力の排他を切り替える | 4（buildMenu, blockInventory, modal, pauseMenu） | **IME は未検証** | 早めに実機で確認（§9） |
| 17 | **Painter2D の図形部品** | GaugeBar、ProgressArrowGlyph（充填）、BarSeries、FadeRule、斜線帯、段付きの罫線、選択肢の板、四隅のブラケット | 図形ごとに: FadeRule 5、ゲージ・矢印・系列グラフ 3（blockInventory, progress, recipe）、斜線帯 1（modeHud）、選択肢の板 1（skit）、ブラケット 1（recipe） | グラフ 18×300 で1.5ms | 必要になった画面で順に |
| 18 | **アニメーション基盤** | tween（α・位置・拡縮）、keyframes 相当のループ（脈動、誘導）、頭から回し直す、完了イベント、ばね・指数減衰（慣性）、smooth scroll | 7（notification, tutorial, skit, buildMenu, research, challenge, recipe） | transition、experimental.animation、自作のばねを実証済み。罠が3つ | 通知かチュートリアルの前に |
| 19 | **TreeView** | パンとズーム（0.4〜2.5）、慣性 τ=325ms、Painter2D の線、ビューポートのセッション保存、初期フォーカス | 2（research, challenge） | パンとズームを実証済み | research の前に |
| 20 | **モーダル層と全画面ゲート** | 幕、フォーカスの閉じ込め、Esc、不透明な全画面の面 | 2（modal, eventLanguageGate） | 未実証 | 2弾目以降 |
| 21 | **トーストと通知の層** | トースト（右下、3秒）と、背面 viewport 族の通知の積み方 | 2（toast, notification）と、bridge の失敗通知の経路（全 action） | — | 2弾目以降 |
| 22 | **アイコン（SVG の代わり）** | skit の6種、×、ピン、矢印 → VectorImage か Painter2D。文中のアイコンは SpriteAsset | 4（skit, tutorial, blockInventory / buildMenu の IconButton） | 文中アイコンは内部 API のリフレクションで実証済み | 必要になった画面で |
| 23 | **純ロジックの C# 移植** | itemMove（2機能）、placementTarget（4: buildMenu, hotbar, modeHud, research）、ownedCounts（3: inventory, recipe, research）、craftLogic、researchLogic、equipmentLogic | 機能ごと | vitest をそのまま NUnit に移せる | 各画面と同時 |

---

## 7. 移行の順番への含意（推測を含む）

### 7.1 一緒に移さないと壊れる束（strangler の単位）

CEF は uGUI の描画面として合成されている（ADR 0052）。UITK の panel との前後は、panel 単位でしか決められない見込み（推測）。そのため、**次の束は同じ系統（両方 CEF か、両方 UITK）にそろっている必要がある**。

| 束 | 中身 | 理由（根拠） |
| --- | --- | --- |
| A: 掴みの束 | inventory、blockInventory、recipe（RecipeViewer と ItemListPanel） | GrabOverlay は playerInventory・subInventory・researchTree にまたがって1つだけある（`app/App.tsx:95-107, :131`）。inventory の `slotActions.ts` は blockInventory を import し、blockInventory は recipe の RecipeRow を import する |
| B: 建築の束 | buildMenu、hotbar | ビルドメニューからホットバーへ D&D する（`features/buildMenu` が `features/hotbar` を import している） |
| C: チュートリアル | tutorial と、アンカーを持つ13機能 | ハイライト枠は DOM の矩形から描いている。移した画面のアンカーを Web 側の overlay は見つけられない。どちらかの系統に「相手側のアンカー矩形」を渡す橋が要る（推測） |
| D: ポーズの束 | pauseMenu、settings、toast | pauseMenu が settings と toast を import している |

### 7.2 第一弾の候補の比較（推測）

- **hotbar 単独**
  - 小さい（287行）。常時出ている HUD なので、効果がよく見える
  - ただし束 B により、buildMenu からの D&D が途切れる
  - 先に「CEF から UITK へ落とす橋」を作るか、buildMenu も一緒に移す必要がある
- **inventory 単独**
  - 束 A のため単独では切り出せない
  - 掴んだアイテムを CEF 側（RecipeViewer・BlockInventory）と行き来させる手段が無いため
- **低難度の HUD 群**（commonHud, keyHint, progress, trainHud, modeHud）
  - 依存が少なく、共通部品の #1〜#3 と #6 の試運転に向いている
  - ただし progress・commonHud・trainHud・modeHud はアンカーを持つ。そのため、束 C の橋（アンカーの矩形を CEF の overlay へ渡すか、tutorial overlay を先に UITK へ移す）が先に要る
- **結論の候補**（推測）: 共通部品の #1〜#7 と「アンカー矩形の橋」を先に作る。そのうえで HUD 群 → 束 B → 束 A → 残り、の順に進める。議論の記録が挙げた「インベントリかホットバー」は、単独だと束を割ってしまう点を grill で詰める必要がある。

### 7.3 受入条件の資料（読み替える対象）

| 機能 | vitest | e2e の spec（主なもの。`moorestech_web/webui/e2e/tests/`） |
| --- | ---: | --- |
| blockInventory | 113 | block 系 約53ケース |
| buildMenu | 52 | `regression/buildMenu.spec.ts`、`regression/buildMenuLayout.spec.ts` |
| tutorial | 42 | `system/tutorial.spec.ts`、`system/worldPin.spec.ts` ほか14ファイルにアンカーの検査がある |
| notification | 38 | `notification/animation`・`itemEarned`・`layering`・`fixedWidth` |
| inventory | 34 | inventory 系 約21、`inventory/equipment.spec.ts` |
| research | 31 | research 系 約20（`research/researchViewport.spec.ts` を含む） |
| pauseMenu | 29 | `system/pauseMenu.spec.ts`、`system/feedbackReportLabels.spec.ts`、`system/pauseMenuSettingsLayout.spec.ts` |
| recipe | 27 | recipe 系 約21（`recipeListScroll`、`itemListScroll`） |
| settings | 15 | `system/i18n.spec.ts` ほか |
| modeHud | 11 | `modeHud/operation-mode-hud.spec.ts` 約7 |
| hotbar | 10 | `hotbar/hotbar.spec.ts` 約7 |
| eventLanguageGate | 9 | — |
| modal | 8 | modal 系 約5 |
| skit | 5 | `skit.spec.ts` 約6 |
| keyHint / commonHud / toast | 4 / 3 / 2 | `system/commonHud.spec.ts`、`regression/actionErrors.spec.ts` |
| challenge / progress / trainHud | 0 / 0 / 0 | `challenge.spec.ts` 約13、`progress.spec.ts` 約4、`train.spec.ts` 約6 |
| shared | i18n 55、ui 59、itemMove 30、treeView 27、uiState 26、tutorialAnchor 11、placementTarget 9、tooltip 7、pointerGesture 5 | `system/portalLayerScaling.spec.ts`、`regression/inputLayer.spec.ts`、`system/textSelection.spec.ts` |

---

## 8. 移行で消えるもの、移すもの

| 種別 | 対象 | 扱い（推測） |
| --- | --- | --- |
| 消える | bridge の WebSocket 輸送と再接続、`AppErrorBoundary`、辞書の HTTP fetch とリトライと 409、アイコンの URL と onError のラッチ、`--ui-scale` を手で掛けている所、zustand の store | C# の直接購読と PanelSettings で不要になる |
| そのまま C# へ移す | itemMove、craftLogic、researchLogic、equipmentLogic、modalLogic、placementTarget、`useLanguageSelectionAnswer` の状態機械、notification の集約、時刻表の楽観更新、スクロールスパイの計算 | 純粋な関数と状態機械。vitest のケースは NUnit に移せる |
| 作り直す | 見た目の部品（§6）、層、入力、アンカー、アニメ | — |
| 作り替える | `Client.WebUiHost/Game/`（8,165行）の Topics と Actions | JSON で配る形をやめ、ViewModel への購読にする。topic のデータモデルは正本として残すのが議論の記録の方針 |

---

## 9. 計画で裁定が要る論点

1. **画像アセット禁止の規則**（ref-webui-design）
   - UITK では、グラデ・マスク・斜線帯・SVG アイコンを Painter2D で描くかテクスチャを焼くかを選ぶ必要がある
   - テクスチャと VectorImage を許すかどうかで、§6 の #8・#17・#22 の工数が大きく変わる
2. **Linear 色空間での半透明**
   - tokens の半透明の値（少なくとも11行）は、そのままでは薄く出る
   - 「見た目を合わせる正本」をどこに置くか（Web の画面写真と比べるか、数値を変換するか）
3. **小さい文字の画質**
   - 10〜13px が18宣言ある。PoC では和文が細かった
   - 最小の字の大きさを見直すか、Advanced Text Generator（未検証）を試すか
4. **IME**
   - 4箇所の入力欄はすべて未検証。日本語 IME での変換・確定・Esc・フォーカス排他を、Windows と macOS の実機で手で確かめる工程を計画に入れる
5. **Mantine の素の見た目のまま残っている画面**
   - toast（負債 moorestech-cgm）、challenge（Mantine の CSS 変数13箇所）、trainHud
   - 移行のついでに様式化するか、見た目を同じにすることを優先するか
6. **strangler の束と橋**
   - §7.1 の束 A〜D を単位にするか、CEF と UITK の間に掴み・D&D・アンカー矩形の橋を作って画面単位で切るか
7. **viewport 族の拡縮**
   - 位置は実画面、寸法は stage 拡縮という二重の基準を、PanelSettings 1枚で表すか、panel を2枚に分けるか
8. **i18n の差し込み記法**
   - 同じ辞書に `{p0}` と `{count}` が混在している（§1.4）。C# 側に名前付きの差し込みを足すか、辞書を一方に統一するか
9. **プレイテスト DSL の照会**
   - `data-testid` を `name` に移す規約と、domQueryResponder の代わりを、第一弾より前に決める
   - 決めないと、録画テストで移行を検証できない
10. **CEF 固有の層の扱い**
    - 再接続オーバーレイ、辞書エラー、AppErrorBoundary は、UITK では不要になる見込み
    - 一方で「UI の例外を誰が気づけるか」という役割は残るので、代わりの気づく経路を決める

---
name: webui-design
description: |
  moorestech Web UI（moorestech_web/webui）のデザイン哲学。見た目・構造のホワイトリスト。
  Use when: 1.moorestech_web/webui配下のコードを読む・書く・レビューする時 2.新しいパネル・モーダル・HUD・コンポーネントを追加する時
  3.CSS・色・レイアウト・装飾を変更する時 4.Web UIのデザイン判断に迷った時
---

# moorestech Web UI デザイン哲学

このドキュメントは moorestech Web UI の見た目・構造の**ホワイトリスト**である。

**大原則: ここに書かれていない表現・コンポーネント・パターンは使わない、やらない。**
新しい表現が必要になったら、実装する前にこのドキュメントを更新して裁定を取る。
「とりあえず作って後で様式化」は禁止。様式が先、実装が後。

正本（リファレンス実装）はインベントリ画面（`InventoryPanel` + `RecipeViewer` + `ItemListPanel`）。
迷ったらインベントリ画面がどうしているかを見て、それに従う。

---

## 1. 画面構成

- **全画面UIは作らない。** すべてフローティングパネルまたはモーダル形式。
  - Web UIは3D世界の上に載る透明オーバーレイ（CEF）であり、世界が透けて見えることが前提。
  - 画面全体を不透明な面で塗り潰すレイアウトは、いかなる画面でも禁止。
- **背景ディムは App の screen backdrop 1枚だけが担う。** 各パネルが独自に画面を暗くしない。
- **重なり順は `index.css` の `--z-*` トークンのみで制御する。** 数値のz-index直書き禁止。
- 常時表示HUD（ホットバー・クロスヘア・キーヒント等）は例外的にパネル外だが、これも「浮いている」表現であること。面で塗らない。

## 2. パネル — GamePanel を使い回す

- **パネル背景はすべて `shared/ui/GamePanel`。** 新しいパネル背景を発明しない。
  - `variant="default"`: 縁を持たず世界背景へ溶ける半透明ネイビー面（インベントリパネルの背景）。側面・一覧系パネルの標準。
  - `variant="craft"`: 1px枠+内周線を持つ中央詳細用の細めバリアント。
- **上部2本線+タイトル（`title` 指定）は「一覧の置き場」に限る。**
  - 使う: インベントリ、クラフトレシピ一覧など、アイテムが並ぶ主要パネル。
  - 使わない: 詳細表示、小型フロート、モーダル、HUD。`title` を渡さなければ罫線は出ない。
- 新しい見た目が必要なら GamePanel に variant を追加し、本ドキュメントに追記してから使う。GamePanel の外で独自CSSのパネル面を作るのは禁止。
- **注: インベントリ画面の「整理」ボタンとpingボタンは仮実装であり、様式に含めない。** これらを前例として引用しない。

## 2.5 ブロックUIパネル

- **ブロックインベントリの外枠は `GamePanel variant="default"` + `title`=ブロック名。** スロットが並ぶ主要パネルを「一覧の置き場」として扱い、タイトル上下の2本罫線を許可する。
- App の stage グリッドにある `viewer` 領域へ置き、持ち物パネルの右隣で上端を揃える。機能側の固定配置・独自z-index・パネル面・下端フェードは禁止し、配置は stage、面表現は GamePanel が一元供給する。
- GamePanel の下向き三角と内容が重ならないよう、ブロックパネルだけ `--block-panel-bottom-safe-area` の下部安全帯を確保する。共通 GamePanel の余白は変更しない。
- 閉じる操作はパネル右上の `shared/ui/PanelCloseButton` を使う。面を持たない浮遊の×とし、Mantine CloseButton は使わない。
- **レシピ選択を持つ機械ブロックのみ大型レイアウト**: 研究パネル同様 `viewer-start / items-end` の2列を占有し、上端は持ち物パネルと揃え、下端はホットバー手前で止める。中身は `ModeSwitch` を横向きタブバーとした「インベントリ / レシピ選択」の2タブ切替（§8.7）。レシピ0件のブロックは従来の小型パネルのまま。

## 3. モーダル

- **モーダルの面もインベントリパネル系（GamePanelのトーン）を使う。** Mantine標準テーマ剥き出しの白/グレー面を出さない。
- モーダルは中央配置+backdropディム。backdropはモーダル専用の1枚のみ（screen backdropと二重にしない）。
- 確認・入力等の定型モーダルは `ModalHost`（`ui.modal` トピック駆動）を通す。機能側が勝手に独自モーダルをマウントしない。

## 4. スロットとグリッド

- **アイテム・ブロック・液体を1マスで表すものは `shared/ui` のコンポーネントのみ。**
  - `ItemSlot` / `BlockSlot` / `FluidSlot` / `FluidSlotRow` / 素枠は `SlotFrame`。
  - 並べるのは `SlotGrid`（既定9列）。独自の grid CSS でスロットを並べない。
- スロット寸法は `--slot-size`、間隔は `--slot-grid-gap` の局所上書きで調整する。コンポーネント内にpx直書きしない。
- スロットの状態表現は data属性（`data-selected` / `data-filled` / `data-catalog` / `data-insufficient`）に統一。新しい状態が要るなら data属性を追加する。
- マウス操作の契約は `useSlotMouse`（左押下・右押下・ドラッグ進入・ダブルクリック）。スロットに生の onClick を生やさない。

## 5. 色・トーン

- パレットはuGUI由来の**半透明ネイビー（#0a0e1b / #070912 系, α0.8）+ 寒色グレー**。
- 色は `index.css` の CSS変数（`--color-*` / `--text-*` / `--bevel-*` 等）から取る。機能側CSSへの新色ハードコード禁止。新色が必要ならトークン化してから使う。
- アクセントの青グラデ（`--recipe-action-background`）は**主要アクションボタン限定**。装飾や面には使わない。
- 面は必ず半透明。不透明100%の面は作らない（世界が透けるのが前提のため）。
- `index.css` の `--text-muted` は従属テキスト、`--text-insufficient` は不足/警告、`--gauge-track` はゲージの溝、`--gauge-fill` はゲージの充填に使う。
- 機能側への色ハードコードは引き続き禁止し、これらの色も必ずトークン経由で参照する。

## 6. 装飾

- **UI装飾の画像アセット化は禁止。** 枠・罫線・文字・グリップ等はCSS/DOM/インラインSVGで再現する。（例外はテスト用モックの世界背景のみ）
- 装飾語彙は既存の3つに限る:
  1. 両端フェードする水平罫線（タイトル上下の2本線）
  2. 下向き三角の底面テクスチャ（default パネル下部）
  3. 右下三角グリップ（craft パネル）
- 新しい装飾モチーフ（光彩、パーティクル、角丸カード、ドロップシャドウの多用等）を増やさない。
- 装飾アニメーションは基本入れない。トランジションを入れる場合もe2eが同期検証できること（モーダルは duration 0）。

## 7. 文字

- フォントは `--font-ui` のみ。個別 font-family 指定禁止。
- 実フォントは単一ウェイトのため**合成bold/italicは禁止**（`font-synthesis: none` を崩さない）。
- **表示文字列は必ず `t()` を通す。** JSXへの生リテラルは lint（no-jsx-visible-literal）で落ちる。
- キー操作ヒントは `<kbd>` + `t()` の既存様式（InventoryScreenChrome の keyHints）に従う。

## 8. 通知・情報表示

- 一時通知は `ToastHost`、カーソル追従の説明は `CursorTooltip` を使う。機能側で独自トースト・独自ツールチップを作らない。
- 接続前のプレースホルダは `ConnectingPlaceholder`。
- 進捗矢印は `ProgressArrow`。

## 8.5 グラフビュー（研究ツリー等のノードグラフ）

- グラフの置き場は `GamePanel variant="default"` + タイトル罫線。body内で `shared/treeView` のパン・ズームを使う。
- **研究ノードカード**: 「名前1行(ellipsis) + `ItemSlot`アイコン」の縦積みのみ。説明・消費・報酬・ボタンはカードに載せない。
  面は `--research-node-face`、枠は `--research-node-border`（index.cssのトークン）。
  状態はdata属性（`data-completed` / `data-researchable` / `data-locked` / `data-selected`）。
  lockedはopacity減衰、selectedは `--text-high-contrast` のoutlineで表す。新しい色相・光彩は使わない。
- **グラフ内詳細ペイン**: ノード選択で開く `GamePanel variant="craft"` のフロート。グラフパネル内の固定位置
  （パン・ズーム非追従）。内容は名前・説明・消費(`ItemSlot`+insufficient)・報酬/解放(`ItemSlot`)・
  主要アクションボタン（青グラデ）・閉じるボタン。オンオフ可能（同ノード再クリック/閉じるで消える）。

## 8.6 shared/ui の汎用表示部品

- **GaugeBar**: 読み取り専用の水平ゲージ。溝は `--gauge-track`（半透明ネイビー）と `--bevel-c1` の薄い内周輪郭、充填は `--gauge-fill`（寒色グレー）を使い、青グラデは禁止。`value`（0..1）を描くだけでドメイン語彙を持たない。
- **ModeSwitch**: `option.value` / `option.label` / `onChange` の汎用I/Fを持つ択一モード切替。選択中は `data-selected`（`--text-high-contrast` + 寒色面）、非選択は `--text-muted` とし、各選択肢は間隔を空けて独立したボタンとして示す。青グラデは禁止。
- **PanelCloseButton**: パネル右上の面を持たない×。インラインSVGまたはCSSで描画する。

## 8.7 機械レシピ選択タブ

- **MachineSection のタブとして置く。** 対象レシピが1件以上ある機械は `ModeSwitch`（横向き）で「インベントリ / レシピ選択」を切り替える。デフォルトはインベントリタブ。0件ならタブ自体を出さず従来表示のまま。
- インベントリタブは従来の機械表示（入出力/モジュールスロット・進捗矢印・流体行・分間生産数）。電力率テキストはタブの外の共通フッタとして両タブで常時表示する。
- レシピ選択タブは、解放済みレシピの代表出力アイテムを `shared/ui` の `ItemSlot` で `SlotGrid`（9列折返し）に列挙し、独自gridは作らない。
- 選択中は ItemSlot の `selected`（SlotFrame の `data-selected`）で示し、新しい色相・光彩は足さない。
- 左クリックで選択し、右クリックは選択中の場合だけ解除する。マウス契約は ItemSlot の `onLeftDown` / `onRightDown`（内部の `useSlotMouse`）に従う。
- 選択中レシピの詳細（材料 `ItemSlot` → 出力 `ItemSlot`・所要時間）をグリッド下に表示する。様式は RecipeViewer の `MachineRecipeView`（矢印テキスト＋ItemSlot列）に準拠し、新しい装飾は増やさない。

## 9. やらないことリスト（再掲・明示）

- 全画面UI・不透明な面での塗り潰し
- Mantine標準テーマ剥き出しの見た目
- UI装飾のための画像アセット追加
- GamePanel 以外のパネル背景 / shared/ui 以外のスロット表現
- 機能側CSSへの色・z-index・スロット寸法の直書き
- 新しい装飾モチーフ・装飾アニメーションの無断追加
- **このドキュメントに書かれていないパターンの使用**（必要なら先にここを更新する）

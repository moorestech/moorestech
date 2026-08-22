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

**大原則: フェード・余白などの視覚寸法は固定長トークンが既定。パネル寸法に比例する%指定は破綻源。**
%指定は基準サイズでは正しく見え、寸法違いのパネル（大型化・別画面流用）で初めて破綻する。
比例させたい明確な理由がある場合のみ%を使い、その理由をコメントで明記する。

正本（リファレンス実装）はインベントリ画面（`InventoryPanel` + `RecipeViewer` + `ItemListPanel`）。
迷ったらインベントリ画面がどうしているかを見て、それに従う。

---

## 0. 実装フロー（Web UIの作業はこれで回す）

Web UI は Unity を経由せず HMR で即反映でき、Playwright で画素・レイアウトを実測できる。
**この速さを使い切ることが前提であり、「Unityを立てて目視する」「コードを読んで推測する」で済ませてはいけない。**

### 0.1 worktree を切る

```bash
moores-wt new <branch> --no-editor
cd <worktree>/moorestech_web/webui && pnpm install
```

Web UI だけなら Unity Editor は不要（`--no-editor`）。`node_modules` は worktree に付いてこないので `pnpm install` する（storeが温まっていれば数秒）。

### 0.2 mock-host + vite dev を上げる（Unity不要・HMR有効）

```bash
MOCK_PORT=<port_a> MOORESTECH_E2E=true node --import tsx e2e/mock-host/server.ts
MOORESTECH_E2E=true MOORESTECH_BACKEND_PORT=<port_a> MOORESTECH_VITE_PORT=<port_b> pnpm dev
```

`vite.config.ts` が `/api` `/ws` `/__` を backend ポートへプロキシするので、mock-host が Unity サーバーの代わりになる。
画面状態は mock の制御エンドポイントで作る（`/__uistate` `/__block` `/__modal` `/__topic-control` 等・`e2e/support/mockControl.ts` が窓口）。

**ポートはセッション固有に振る。** Playwright既定の 5273 を使い回すと並列セッションで衝突し、無関係な spec が落ちて原因調査が空転する。

### 0.3 cloudflared quick tunnel で人間に見せる

```bash
cloudflared tunnel --url http://127.0.0.1:<port_b> --http-host-header 127.0.0.1:<port_b>
```

`--http-host-header` は必須。無いと vite の allowedHosts 検査が `*.trycloudflare.com` を弾き "Blocked request" になる（`vite.config.ts` は無変更で通せる）。
URLはDNS伝播に十数秒かかる。HMRが効くので、修正はそのURLへ即反映される＝ユーザーと同じ画面を見ながら詰められる。

### 0.4 直す前に、症状の出所を実測で特定する（最重要）

**見た目の症状は必ず数値の出所へ落としてから直す。** スクショだけを見て原因を決めない。

Playwright スクリプトで次を出力する:
- `getBoundingClientRect()` … 位置・寸法のズレ
- `scrollHeight` vs `clientHeight` / `scrollWidth` vs `clientWidth` … 溢れの有無と量
- `getComputedStyle()` … 実際に効いている値（トークンの解決結果）
- `dataset.state` / `display` … Mantineの内部状態（スクロールバー等）

原因候補が複数あるときは **ablation** で切る。要素を1つずつ `display:none` にする／変数を1つずつ変える／値を0.1px刻みでスイープして、症状が消える点を見つける。

> 実例（2026-08-22 CRAFT RECIPE一覧）: 「黒い枠線」は `type="always"` が描いた**つまみ幅0の水平スクロールバー**（`scrollWidth === clientWidth` で溢れゼロ）、「不要なスクロール」は個数バッジの5px はみ出し（`.count` を消すと `scrollHeight - clientHeight` が 5→0）だった。どちらも見ただけでは特定できず、実測とablationで初めて確定した。

### 0.5 確定したらテストと目視QA

`pnpm lint` / `pnpm test` / `pnpm test:e2e` を通し、§10 の目視QAチェック項目を実施する。
**挙動を固定していた既存 e2e があれば、裁定に合わせて反転させる**（古い assertion を残したまま実装だけ変えない）。

### 0.6 後片付け

tunnel・vite・mock-host を落とし、`moores-wt rm` で worktree を削除する。

---

## 1. 画面構成

- **全画面UIは作らない。** すべてフローティングパネルまたはモーダル形式。
  - Web UIは3D世界の上に載る透明オーバーレイ（CEF）であり、世界が透けて見えることが前提。
  - 画面全体を不透明な面で塗り潰すレイアウトは、いかなる画面でも禁止。
  - 例外（ADR 0014・ユーザー裁定 2026-08-19）: 研究ツリー画面のみ、半透明GamePanelが
    「持ち物パネルの右隣から画面右端まで・画面上端から下端まで」を占有してよい。面は従来どおり半透明で世界は透ける。
    持ち物パネルとは重ならない（重畳ではなく棲み分け）。研究画面では持ち物をstage左paddingごと画面左端へ寄せ、
    常時表示族のホットバー・装備HUDは描画しない。チャレンジHUD・キー操作ヒント・採掘進捗バーは
    このパネルより上の層（`.viewportOverlay` の `--z-stage-overlay-panel-chrome`）に残す。
- **背景ディムは App の screen backdrop 1枚だけが担う。** 各パネルが独自に画面を暗くしない。
- **常時の縁ヴィネットは App の実viewport全面が担う。** 1280基準stageへ置くと横長画面の途中で切れるため、stage背景へ戻さない。ヴィネットの楕円寸法・中心・停止位置だけは、縦横比が異なる実viewportの四辺へ同じ比率で沿わせる必要があるため、固定長原則の例外としてviewport比例の`%`トークンを使う。
- **重なり順は `index.css` の `--z-*` トークンのみで制御する。** 数値のz-index直書き禁止。
- 常時表示HUD（ホットバー・クロスヘア・キーヒント等）は例外的にパネル外で、原則として「浮いている」表現とし面で塗らない。
  - **唯一の例外は目標HUD（チャレンジHUD・§8.14）**。面が必要な場合も独自CSSで面を作らず、`GamePanel variant="hud"` から供給する（面色は `--surface-navy`・4辺フェードは `--panel-edge-fade` をパネル面と共有し、安全帯だけ `--hud-panel-padding` を持つ）。他のHUDへ面を広げるのは都度裁定。

## 1.5 stage族 と viewport族

- **全ての表示要素は stage族 か viewport族 のどちらかに属する。実装前にどちらかを宣言する**（ADR 0013）。
  - **stage族**: 1280×720基準の `.stage` 上で一様拡縮する。形はアスペクト比で変わらない。パネル・グリッド・詳細はこちら。
  - **viewport族**: 実画面の辺へ位置が追従し、内容寸法だけが stage 拡縮に従う。`App.module.css` の `.viewportOverlay` 配下へ置く。
- **常時表示HUD族（ホットバー・装備HUD・キーヒント・採掘プログレスバー・目標HUD・操作モードHUD）は viewport族。** stage絶対配置のまま `calc()` で補正しない（補正式がHUDの数だけ増殖して破綻する）。
- **`.viewportOverlay` は `pointer-events: none`。** 配下へ置く操作可能要素（ホットバーのスロット列・装備HUD）は `pointer-events: auto` を明示する。忘れると操作が死ぬ。
- **第三の所属として背面viewport族がある**（ADR 0017）。`.viewport` 直下・`.stage` の裏（`--z-viewport-behind-stage`）に置き、`--ui-scale` に追従しない。stage族でもviewport族でもない。現状の唯一の利用者は通知（§8）。
- 基準解像度1280×720では stage と viewport が一致するため、族の移動だけでは描画結果が変わらない。

## 2. パネル — GamePanel を使い回す

- **パネル背景はすべて `shared/ui/GamePanel`。** 新しいパネル背景を発明しない。
  - `variant="default"`: 縁を持たず世界背景へ溶ける半透明ネイビー面（インベントリパネルの背景）。側面・一覧系パネルの標準。
  - `variant="craft"`: 1px枠+内周線を持つ中央詳細用の細めバリアント。
  - `variant="hud"`: 面と4辺の境界フェードだけを持つ常時表示HUD用バリアント。タイトル罫線・下向き三角・右下グリップ・正本合わせの実測オフセットを持たない。余白は `--hud-panel-padding`（全辺、フェード幅を超える安全帯）。実装は `GamePanel/hudVariant.module.css` に分け、セレクタは `[data-variant="hud"].hud` と併記して `.panel` のpaddingへimport順に依存せず詳細度で勝たせる。
- **面の左右フェード幅は固定長トークン `--panel-edge-fade` のみ。** %指定はパネル幅でフェード幅が伸びて内容がフェード帯に載るため禁止。内容はGamePanelのpadding内に置く限り不透明領域内に収まることを保証する（はみ出し防止の唯一の機構）。
- **ただし共通GamePanelのpaddingは全辺でこの保証を満たしていない**（左28pxのみフェード幅12px超。右10px・上8pxはフェード幅未満）。正本合わせの持ち物パネルでは意図的な非対称なので共通paddingは変更せず、**内容量でサイズが決まるパネル（チェスト等）は不足する辺を安全帯トークンで補う**（前例: `--block-panel-right-safe-area` / `--block-panel-bottom-safe-area`）。内容の縁とフェード開始位置が近い辺は「面が内容の直後で途切れて見える」ため、余白は「フェード幅+視認できる余白」を確保する。
- **`titleAction?: ReactNode`（枠付きvariant限定）**: タイトル行の右端へ絶対配置する汎用スロット。パネル自身への副次アクション（§8.6 `PanelActionButton`）を置く場所で、GamePanel 側はドメイン語彙を持たない。本文先頭/末尾へ置くとグリッドが押されて正本合わせの実測値が崩れるため、行の右端絶対配置を守る。
- **面のみのvariant（`skit` / `hud`）は型でタイトル行を持てない。** Props は「枠付き（`default` / `craft`：`gridArea` / `title` / `titleAction` 可）」と「面のみ（`skit` / `hud`）」のunionで、面のみ側に `title` を渡すのはコンパイルエラーになる。
- **上部2本線+タイトル（`title` 指定）は「一覧の置き場」に限る。**
  - 使う: インベントリ、クラフトレシピ一覧など、アイテムが並ぶ主要パネル。
  - 使わない: 詳細表示、小型フロート、モーダル、HUD。`title` を渡さなければ罫線は出ない。
- 新しい見た目が必要なら GamePanel に variant を追加し、本ドキュメントに追記してから使う。GamePanel の外で独自CSSのパネル面を作るのは禁止。
- **注: 「整理」ボタンは §8.6 `PanelActionButton` として様式化済み**（stage右上への浮かせ置き＋色ハードコードの仮実装は撤去）。pingボタンは依然として仮実装であり、様式に含めず前例として引用しない。

## 2.5 ブロックUIパネル

- **ブロックインベントリの外枠は `GamePanel variant="default"` + `title`=ブロック名。** スロットが並ぶ主要パネルを「一覧の置き場」として扱い、タイトル上下の2本罫線を許可する。
- App の stage グリッドにある `viewer` 領域へ置き、持ち物パネルの右隣で上端を揃える。機能側の固定配置・独自z-index・パネル面・下端フェードは禁止し、配置は stage、面表現は GamePanel が一元供給する。
- GamePanel の下向き三角と内容が重ならないよう、ブロックパネルだけ `--block-panel-bottom-safe-area` の下部安全帯を確保する。共通 GamePanel の余白は変更しない。
- 内容量で幅が決まる小型ブロックパネル（チェスト等）は、GamePanel共通の右余白10pxがフェード帯に食われて面が途切れて見えるため、`--block-panel-right-safe-area`（左インデント28pxと対称）の右余白を追加する。大型機械パネルは固定幅・中央揃えのため対象外。
- 閉じる操作はパネル右上の `shared/ui/IconButton`（children省略で既定の×）を使う。面を持たない浮遊の×とし、Mantine CloseButton は使わない。
- **レシピ選択を持つ機械ブロックのみ大型レイアウト**: `viewer-start / items-end` の2列を占有し、上端は持ち物パネルと揃え、下端はホットバー手前で止める（研究パネルは持ち物の右隣から画面端までの別レイアウトのため前例には引かない）。中身は `ModeSwitch` を横向きタブバーとした「インベントリ / レシピ選択」の2タブ切替（§8.7）。レシピ0件のブロックは従来の小型パネルのまま。

## 3. モーダル

- **モーダルの面もインベントリパネル系（GamePanelのトーン）を使う。** Mantine標準テーマ剥き出しの白/グレー面を出さない。
- モーダルは中央配置+backdropディム。backdropはモーダル専用の1枚のみ（screen backdropと二重にしない）。
- 確認・入力等の定型モーダルは `ModalHost`（`ui.modal` トピック駆動）を通す。機能側が勝手に独自モーダルをマウントしない。

## 4. スロットとグリッド

- **アイテム・ブロック・液体を1マスで表すものは `shared/ui` のコンポーネントのみ。**
  - `ItemSlot` / `BlockSlot` / `FluidSlot` / `FluidSlotRow` / 素枠は `SlotFrame`。
  - 並べるのは `SlotGrid`（既定9列）。独自の grid CSS でスロットを並べない。
  - **ただしパネル内のスロット群に限る。常時表示HUD族（ホットバー・装備HUD）は `SlotGrid` の対象外**で、HUD自身の固定長トークンで組んだ1列のflexに並べる（前例: `HotbarPanel` / `EquipmentPanel`）。折返しの無い1列にグリッドの列数概念を持ち込まないため。
  - **もう1つの例外はレシピ行（§8.17）**。素材・結果のスロット寸法はコンテナクエリ（`container-type: inline-size` + `cqw`）から引くため `SlotGrid` の既定 `grid-template-columns` を必ず上書きすることになる。`--slot-size` を要素自身の `grid-template-columns` で使うと `cqw` が祖先コンテナへ解決して失敗する（実測でスロットが縮まず溢れた）ため、`RecipeRow` は独自gridを持つ。ユーザー裁定 2026-08-20。
- スロット寸法は `--slot-size`、間隔は `--slot-grid-gap` の局所上書きで調整する。コンポーネント内にpx直書きしない。
- スロットの状態表現は data属性（`data-selected` / `data-filled` / `data-catalog` / `data-insufficient`）に統一。新しい状態が要るなら data属性を追加する。
- マウス操作の契約は `useSlotMouse`（左押下・右押下・ドラッグ進入・ダブルクリック）。スロットに生の onClick を生やさない。
- **同一要素でクリックと複数要素をまたぐドラッグ&ドロップ（掴む→運ぶ→離した場所で判定）を両立させる場面は `useHotbarDragSource`（前例: `HotbarPanel`/`BuildMenuSlot`）。** `useSlotMouse` は単一要素内の左右押下・ドラッグ進入・ダブルクリックのみが対象で、クロス要素D&Dは対象外。5px未満の移動はタップ（クリック相当）、以上はドラッグへ確定し、`pointerdown` で `preventDefault()` して旧 `mousedown` 経由クリックとの二重発火を止める。ドロップ先は `document.elementFromPoint` + 対象要素の `data-*` 属性（例: `data-hotbar-slot-index`）で判定し、HTML5 DnD（`draggable`/`dragstart`等）は使わない。
- ホバープレビュー（ホバー中スロットの詳細を別領域へ出す）は SlotFrame/ItemSlot の `onHoverChange` を使う。機能側で生の onMouseEnter/Leave をスロットに生やさない。
- **用途の異なるスロット群を同一パネル内に並置する時は、ラベル（`--text-muted`）または `FadeRule` の区切りで必ず区別する。** 無札の並置は入出力と誤読されるため禁止（例: アップグレードスロット）。
- **左右のスロット数が非対称になり得る行の中央要素（進捗矢印等）は `1fr auto 1fr` グリッドで中央に固定する。** 行全体のflex中央寄せは個数差で中央要素がずれるため使わない。

## 5. 色・トーン

- パレットはuGUI由来の**半透明ネイビー（#0a0e1b / #070912 系, α0.8）+ 寒色グレー**。
- 色は `index.css` の CSS変数（`--color-*` / `--text-*` / `--bevel-*` 等）から取る。機能側CSSへの新色ハードコード禁止。新色が必要ならトークン化してから使う。
- アクセントの青グラデ（`--recipe-action-background`）は**主要アクションボタン限定**。装飾や面には使わない。
- 面は必ず半透明。不透明100%の面は作らない（世界が透けるのが前提のため）。
- `index.css` の `--text-muted` は従属テキスト、`--text-insufficient` は不足/警告、`--gauge-track` はゲージの溝、`--gauge-fill` はゲージの充填に使う。
- **選択・強調のシアンは `--select-cyan`（`rgb(0 221 255)`、uGUI `frame_select.png` / `nav_arrow.png` 由来）。** 用途はスロット選択枠とスキットの送り待ちマーカー・選択肢ホバー/押下・ツールボタンON状態の点灯・
  研究ノードカードの実行可能状態の枠色点灯（§8.5・ADR 0014）に限る。青グラデ（`--recipe-action-background`）とは別語彙であり、面の常時装飾には使わない。
- 機能側への色ハードコードは引き続き禁止し、これらの色も必ずトークン経由で参照する。

## 6. 装飾

- **UI装飾の画像アセット化は禁止。** 枠・罫線・文字・グリップ等はCSS/DOM/インラインSVGで再現する。（例外はテスト用モックの世界背景のみ）
- 装飾語彙は以下の6つに限る:
  1. 両端フェードする水平罫線（タイトル上下の2本線）
  2. 下向き三角の底面テクスチャ（default パネル下部）
  3. 右下三角グリップ（craft パネル）
  4. 両端の菱形マーカー（uGUI `btn__select_*.png` 由来。**スキット選択肢限定**・§8.12）
  5. シアンの下向きシェブロン=送り待ちマーカー（uGUI `nav_arrow.png` 由来。**スキット会話窓限定**・§8.12。光彩は付けない）
  6. 黄黒の斜線警告帯（uGUI `delete bar.png` 由来。**削除モードの画面上下端限定**・§8.15。画像は移植せずCSS反復グラデーションで再現する）
- 新しい装飾モチーフ（光彩、パーティクル、角丸カード、ドロップシャドウの多用等）を増やさない。
- 装飾アニメーションは基本入れない。トランジションを入れる場合もe2eが同期検証できること（モーダルは duration 0）。
  - **例外は通知の出入り（§8）だけ**。入場＝左から `--notification-shift` のスライド＋フェード、退場＝その逆再生で、色相・形・光彩は動かさない。
  - アニメーションを足す場合、テスト時に尺をゼロへ落とす抜け道は作らない（実挙動と乖離するため）。計算値の `animation-name` はCSS Modulesがハッシュ化するので、e2eでは部分一致で照合する。

## 7. 文字

- フォントは `--font-ui` のみ。個別 font-family 指定禁止。
- 実フォントは単一ウェイトのため**合成bold/italicは禁止**（`font-synthesis: none` を崩さない）。
- **表示文字列は必ず `t()` を通す。** JSXへの生リテラルは lint（no-jsx-visible-literal）で落ちる。
- キー操作ヒントは `<kbd>` + `t()` の既存様式（InventoryScreenChrome の keyHints）に従う。**文字様式は `app/tokens.css` の低詳細度クラス `:where(.keyHintText)` が唯一の正**で、使う側は `keyHintText` を併記し、機能側CSSには位置決め（position / gap / z-index）だけを残す。同じ文字様式の宣言ブロックを機能側へ複製しない。
- **テキスト選択は入力欄のみ**（§9・ADR 0021）。`app/index.css` の `body { user-select: none }` ＋ `input, textarea { user-select: text }` が唯一の正で、機能側CSSで `user-select` を書かない。

## 8. 通知・情報表示

- 一時通知は `ToastHost`（クライアントローカルの汎用トースト）または `NotificationHost`（`features/notification`。サーバー発のゲーム通知＝achievement/operationDenied、topic `notification.events`、左端縦中央・7秒・`ItemIcon`付き可）のどちらかを使う。カーソル追従の説明は `CursorTooltip`。機能側でこの2ホスト以外の独自トースト・独自ツールチップを作らない。
- **`CursorTooltip` の書式はWeb側トークンが唯一の正**（ADR 0019）: フォント18px・padding 6/10px・max-width 320px。ホストは辞書キーと位置パラメータだけを送り、寸法値（fontSize等）はwireに載せない。
- **NotificationHostは背面viewport族**（§1.5・`--z-viewport-behind-stage`）。stage族でもviewport族でもなく、`--ui-scale` に追従しない。
- **NotificationHostの見た目は研究ノードカード同族の枠付き浮遊行**: 面=`--notification-face`（半透明ネイビー）+ 枠=`--notification-border` 1px（直角・角丸/影なし）。最大幅は`--notification-max-width`（画面幅20%・ユーザー裁定の画面比例値）で超過分は折返す。文字色はトークンのみ: achievement=`--text-high-contrast`、operationDenied=`--text-insufficient`。カテゴリはdata属性（`data-category`）で表す。Mantine `Notification` コンポーネントは使わない。
- **NotificationHostの出入りは唯一の装飾アニメーション例外**（§6）。入場は `--notification-enter-duration`（160ms・ease-out）で左から `--notification-shift`（12px）のスライドイン＋フェードイン、退場は `--notification-exit-duration`（200ms・ease-in）でその逆再生。生存尺は store の `NOTIFICATION_DISPLAY_MS`（7000ms）が単一の正で、`NotificationHost` がインラインCSS変数 `--notification-lifetime` として渡し、CSSは退場遅延を `calc(生存尺 − 退場尺)` で逆算する。**退場のためにstoreへ状態（`exiting` 等）を持たせない。** 退場の `animation-fill-mode` は `forwards`（`both` にすると遅延中に前方適用されて入場が消える）。積み替えの移動は補間せず、同時表示数の上限も設けない。
- 接続前のプレースホルダは `ConnectingPlaceholder`。
- 進捗矢印は `ProgressArrowBar`（採掘機・流体行の帯状ゲージ）。クラフト画面と機械の加工行は §8.13 の矢印グリフゲージを使う。器が帯か矢印グリフかを名前で区別する。

## 8.5 グラフビュー（研究ツリー等のノードグラフ）

- グラフの置き場は `GamePanel variant="default"` + タイトル罫線。body内で `shared/treeView` のパン・ズームを使う。
- **研究ノードカード**: 「名前1行(ellipsis) + `ItemSlot`アイコン」の縦積みのみ。説明・消費・報酬・ボタンはカードに載せない。
  面は `--research-node-face`、枠は `--research-node-border`（tokens.cssのトークン）。
  状態はdata属性で4値を表す（ADR 0014）:
  `data-locked`（前提未達）=opacity減衰45% / 無印（前提充足・アイテム不足）=通常グレー枠 /
  `data-ready`（今すぐ研究できる）=`--select-cyan`の枠色 / `data-completed`=`--text-default`の白枠。
  アイテム充足の正本はサーバーstateであり、クライアントは所持数から再計算しない（インベントリ更新で
  ホストが research.tree を再publishするため、state自体がライブ追従する）。所持数は消費アイテムの
  不足強調・所持/必要バッジという表示にだけ使う。
  `data-selected` は従来どおり `--text-high-contrast` のoutline。新しい色相・光彩は使わない。
- **グラフ内詳細ペイン**: ノード選択で開く `GamePanel variant="craft"` のフロート。グラフパネル内の固定位置
  （パン・ズーム非追従）。
  内容は名前・説明・「必要アイテム」ラベル付き消費（`ItemSlot`+insufficient+研究専用ツールチップ`ui.research.consumeItemTooltip`
  ＝名前/所持数/必要数の3行のみ・この画面に無いクリック導線は案内しない。不足時は`CraftRecipeView`同型で数値も赤文字にする）・
  種類別ラベル付き解放セクション（「解放: ブロック」=`BlockSlot`（ホバーで名前が出る）、「解放: 機械レシピ」=
  レシピ単位でアイテム出力は`ItemSlot`・液体出力は量ラベル（研究feature内の`UnlockFluidLabel`。容量の概念が
  無いため`FluidSlot`の充填率表現は使わない）を連結表示（アイテムと液体は排他ではなく、混在レシピの液体も消さない）、
  「解放: アイテム」（`unlockItemRecipeView`由来。testId `research-unlock-items`）=`ItemSlot`、「報酬アイテム」=
  個数付き`ItemSlot`、「解放: その他」=connect tool/train car名のテキスト行）・主要アクションボタン（青グラデ）・
  閉じるボタン。ラベルは`--text-muted`、空の種類のセクションは出さない（§4の無札並置禁止に従う）。
  種類→表示はTS側`unlock/unlockEntries.ts`の判別unionとルックアップ表に集約し、種類追加時は表の欠損がコンパイルエラーになる。
  オンオフ可能（同ノード再クリック/閉じるで消える）。
- **ビューポートの保持と初期フォーカス**: パン・ズーム位置は `viewportKey` によるセッション内ストアで保持し、
  画面を閉じて開き直しても復元する（リロードで消える。永続化はしない）。保存が無い初回のみ、機能側が渡す
  `initialFocus`（研究では最初の `researchable` ノード）をビューポート中央に据える。保存済みが常に優先。
  `viewportKey` はマウント中不変が契約（切り替えは `key` 再マウントで行う）。同一キーの TreeView を
  同時にマウントするのは禁止（ストアが last-writer-wins で上書きされ同期しない）。
- **パンの慣性**: ドラッグを離した後は速度を指数減衰（時定数・発動/停止閾値は `shared/treeView/viewport/` の
  定数）で滑走させる。装飾アニメーションではなく操作の物理であり、CSS transition は使わない。
  pointerup だけが滑走を発動し、pointercancel・capture喪失は中断としてキャンセルする。
  e2e は滑走の静止を待ってから座標検証する（settle待ち）。慣性は treeView のパン専用で、
  ネイティブスクロールの一覧（ScrollArea系）へ独自のドラッグスクロール・慣性を足すのは別裁定。

## 8.6 shared/ui の汎用表示部品

- **GaugeBar**: 読み取り専用の水平ゲージ。溝は `--gauge-track`（半透明ネイビー）と `--bevel-c1` の薄い内周輪郭、充填は `--gauge-fill`（寒色グレー）を使い、青グラデは禁止。`value`（0..1）を描くだけでドメイン語彙を持たない。
  - **ゲージの溝は常に `--gauge-track`。** 帯でも矢印でも器の形が変わるだけで、溝のトーンは変えない。
  - **充填は `--gauge-fill` が既定。** 逸脱してよいのは「器そのものが既に確立した見た目を持ち、その見た目＝満了状態である」場合（前例: 矢印グリフゲージの `--color-content-primary`・§8.13）と、「パネル面を持たず世界の上へ直に載る常時表示HUDで、寒色グレーが背景に沈んで進捗が読めない」場合（前例: 採掘プログレスバー・§8.18）に限り、逸脱先は必ず既存トークンから取る。緑など新しい色相をゲージへ持ち込むのは、溝・充填のどちらでも禁止。
  - **逸脱は器ごとに局所化する。** `GaugeBar` 自体や `--gauge-fill` トークンの定義は変えず、利用側の器のCSSで `--gauge-fill` をローカル上書きする（共有部品にドメイン語彙を持ち込まないため）。
- **ModeSwitch**: `option.value` / `option.label` / `onChange` の汎用I/Fを持つ択一モード切替。選択中は `data-selected`（`--text-high-contrast` + 寒色面）、非選択は `--text-muted` とし、各選択肢は間隔を空けて独立したボタンとして示す。青グラデは禁止。
  - **縦利用（`orientation="vertical"`）はサイドバーナビとして使ってよい。** カテゴリ切替のような縦積み択一に、新規コンポーネントを作らずこれを転用する。
  - **`disabled?: boolean`**: root に `data-disabled` を付与し全ボタンを `disabled` にする汎用減衰。選択肢は `--text-muted` 系へさらに減衰しクリック不可（`pointer-events: none`）。判断（いつdisabledにするか）は利用側が持ち、ModeSwitch自体はドメイン語彙を持たない。
- **PanelActionButton**: パネルへ付随する副次アクションの押しボタン。面は検索入力（§8.9）同族の `--gauge-track`、文字は `--text-high-contrast`、hoverは色相を変えず面だけを明化、`:focus-visible` は ModeSwitch 踏襲。寸法は `--panel-action-button-*` 固定長トークン。主要アクションの青グラデ（`RecipeActionButton`・§5）へ寄せない。置き場は `GamePanel` の `titleAction`（前例: 持ち物パネルの「整理」）。`onClick` / `children` だけを受け、ドメイン語彙は持たない。
  - `PauseMenuPanel` / `ChallengePanel` / `ModalHost` には素の Mantine `Button` が残っている。同語彙へ寄せる候補だが未着手の負債であり、**前例として引用しない**。
- **IconButton**: 面を持たない浮遊アイコンボタン。`children` 省略時は既定の×（従来の PanelCloseButton）で、閉じる以外の用途は呼び出し側がインラインSVGを渡す。寸法は `--icon-button-size` / `--icon-button-icon-size` の局所上書きで変え、共有側にドメイン語彙は持たせない。
- **FadeRule**: 両端フェードする水平罫線（装飾語彙1）の単体部品。パネル内のセクション区切りに使う。GamePanel のタイトル罫線と同族の青灰グラデで、新しい色相は持たない。

## 8.7 機械レシピ選択タブ

- **MachineSection のタブとして置く。** 対象レシピが1件以上ある機械は `ModeSwitch`（横向き）で「レシピ選択 / インベントリ」を切り替える。初期タブはレシピ未選択ならレシピ選択、選択済みならインベントリ。開いた後の手動切替は強制しない。0件ならタブ自体を出さず従来表示のまま。
- **機械UIの中身は基本的に中央揃え。** 両タブとも詳細・グリッド・テキストを水平中央に揃える。稼働状態ラベル（待機中/稼働中/停止中。Halted のみ `--text-insufficient`、他は`--text-high-contrast`）はタブの外の共通フッタとして両タブで常時表示する。電力率テキストは稼働状態ラベルの隣に、**稼働状態が停止中(halted)でない場合だけ**併記する。停止中は要求電力を出さないため充足率が意味を持たず、ラベル「停止中」のみで状態を伝える。表示可否は要求電力の数値ではなく状態で決める（要求電力0で稼働する機械＝石窯・ボイラー等を停止中と同じ表示に潰さないため）。電力率の%は実効要求電力に対する充足率であり、100%未満は常に電力不足を意味する（ADR 0010）。
- インベントリタブは従来の機械表示（入出力/モジュールスロット・進捗矢印・流体行・分間生産数）に加え、レシピ選択中はその生産物（代表出力アイテム）を `ItemSlot` 1個（個数バッジ無し）で表示する。
  - **加工行は進捗矢印をパネル中央に固定**し、左右を等幅（1fr auto 1fr）にして入力は矢印へ右寄せ、出力は矢印から左寄せで対称に置く。
  - **モジュールスロットは加工行から1段下げ、`--text-muted` の「アップグレードスロット」ラベルを直上に付けて**用途を明示する。入出力と紛れる無札の並置は禁止。
- レシピ選択タブは上から「詳細プレビュー → `FadeRule` の区切り罫線 → レシピグリッド」の縦構成。
  - **詳細プレビュー**: ホバー中レシピを優先し、無ければ選択中レシピを表示。どちらも無ければ `--text-muted` の案内テキスト。内容は「材料 `ItemSlot` 列 → 矢印テキスト（直下に所要時間） → 出力 `ItemSlot` 列」で、`MachineRecipeSelectionTab` 自身の矢印テキスト様式（`ui.common.rightArrow`）に準拠。高さを固定しホバーで段落が跳ねないようにする。
  - **レシピグリッド**: 解放済みレシピの代表出力アイテムを `shared/ui` の `ItemSlot` で `SlotGrid`（9列折返し）に列挙し、独自gridは作らない。
- 選択中は ItemSlot の `selected`（SlotFrame の `data-selected`）で示し、新しい色相・光彩は足さない。
- 左クリックで選択し、**選択と同時にインベントリタブへ切り替える**。右クリックは選択中の場合だけ解除する。マウス契約は ItemSlot の `onLeftDown` / `onRightDown`、ホバーは `onHoverChange` に従う。

## 8.8 ワールドピンHUD（チュートリアルの位置誘導）

- **座標の正はUnity。** Unityがワールド座標を正規化ビューポート座標（0..1、左上原点）と画面中心からの方向ベクトルへ毎フレーム射影し、`tutorial.world_pins` トピックで配信する。Web側は受信値を描くだけで、3D射影・カメラ知識を一切持たない。
- 表示は常時表示HUD族（§1の例外）。パネル面を持たず「浮いている」表現とし、`pointer-events: none` で入力を素通しする。
- **画面内ピン**: 指定座標にインラインSVGの下向きマーカー + 直上のテキストラベル。ラベル面は `--world-pin-face`（半透明ネイビー族）、文字は `--text-high-contrast`。マーカー先端が指定座標に一致するよう配置する。
- **画面外矢印**: 方向ベクトルを画面端（マージン `--world-pin-edge-margin` の固定長）へクランプした位置に、方向へ回転したインラインSVGの軸付き塗りつぶし矢印を置く。`--text-high-contrast` の塗りと `--world-pin-face` の輪郭を使い、世界背景から分離する最小限の影を許可する。テキストラベルは付けない（uGUI版HudArrowと同じ責務分担）。
- 色相・光彩・アニメーションは追加しない。z層は `--z-world-pin` トークンのみで制御する。

## 8.9 検索入力

- Mantine `TextInput` は使わない。素の `<input>` に `--gauge-track` 同族の半透明面（GaugeBar の溝と同トーン）を背景として与える。
- プレースホルダは `--text-muted`。フォーカス表現は ModeSwitch の `:focus-visible`（`--text-high-contrast` の outline）を踏襲し、新しいフォーカス様式を増やさない。
- 幅・高さは固定長トークンで指定する（パネル幅比例の%指定は禁止・大原則参照）。

## 8.10 カスタムスクロールバー

- Mantine `ScrollArea` の `:global(.mantine-ScrollArea-*)` セレクタで上書きする（前例: `ItemListPanel.module.css`）。ScrollArea自体は使ってよいが、既定の白ノブ/透明トラックのまま出さない。
- トラックは `var(--gauge-track)`、ノブは `var(--bevel-c2)` を基調にしたネイビートーンへ統一する（ItemListPanelの白ノブ＋透明トラックは持ち物一覧固有の正本合わせ／裁定であり、他パネルではこのネイビートーンに従う）。
- ノブ寸法はコンテンツ量から自然算出させ、固定pxで決め打ちしない。
- **`type` は `auto` を既定とし、`always` を使わない。** `always` は水平バーも常時描画するため、横に溢れていない場面で**つまみ幅0の黒帯**が内容の直下に敷かれる（2026-08-22に CRAFT RECIPE 一覧で実害。ユーザー裁定 2026-08-17 で `ItemListPanel` を `auto` + トラック透明へ変更）。
- **ScrollArea に入れる中身は、外へはみ出す装飾の分だけ内側に余白を確保する。** 確保しないと数pxの偽の溢れが立ち、スクロール不要な件数でもスクロールバーが出る（そして装飾はクリップされて欠ける）。はみ出す装飾の例＝スロットの外側ベベルリング・個数バッジ・エントリ枠の四隅ブラケット。余白は固定長トークンで持つ。
  - 前例: `--recipe-entry-bleed`（レシピ単一リスト・四隅ブラケット+外周リング）、`--item-list-count-bleed`（アイテム一覧・個数バッジ）。
  - 上限高（`mah`）を持つ場合、その値は「N段+bleed」が丸ごと収まる高さである必要がある。段数だけ数えて bleed を忘れると境界の段数でだけバーが出る。

## 8.11 建設メニュー

- **stage水平中央の大型パネル**: stage絶対配置のバンド（ホットバー前例 `HotbarPanel` の
  `position:absolute; left:0; right:0` + flex中央）で、固定幅 `--build-menu-panel-width` のパネルを
  水平センターに置く。stageはレターボックスで常に画面中央にあるため全解像度で画面中央に一致する。
  縦は上端 `--menu-upper-safe-area`・高さ `--menu-content-height`（他メニューの上端揃えを維持）。
  持ち物画面の左詰めgrid（`inv/viewer/items`列）には参加しない（ADR-0007）。
- **3カラム構成**: 1枚のGamePanel内で「カテゴリ | 検索+グリッド | 詳細サイドバー」。
  詳細サイドバー幅は `--build-menu-detail-width`（固定長）。
- **縦ModeSwitchサイドバー**: カテゴリ切替は §8.6 の縦向き ModeSwitch を左サイドバーとして使う。
  幅は `--build-menu-sidebar-width`（固定長）。**各ボタンは `--build-menu-category-height` の固定高・
  上詰め**とし、パネル高さ・カテゴリ数に比例して伸縮させない（縦ModeSwitchの高さは
  `--mode-switch-option-height` 変数で利用側が注入する）。
  **カテゴリ名は全ロケールで1行に収まる長さを前提とし、折り返しは想定しない。**
  幅は日本語名でなく最長の英訳（実マスタv8の `Building Materials`）を基準に決める。
  収まらない名前が現れたら `--build-menu-sidebar-width` と `--build-menu-panel-width` をセットで見直す。
- **検索**: §8.9 の検索入力を中央カラム上部に置く。
- **sticky詳細サイドバー**: ホバー中エントリを表示し、カーソルが離れても直前エントリを表示し続ける。
  初回ホバー前のみ `--text-muted` の案内テキスト。内容は「アイコン → 名前 → `FadeRule` →
  必要素材ラベル（`--text-muted`）+ `ItemSlot` 群」の縦積み。説明文は出さない（マスタに存在しない）。
  閉じる✕がこの列の右上に重なるため、上端に `--build-menu-detail-top-safe-area` の安全帯を空ける（§2の安全帯前例と同族）。
- **サブカテゴリ見出し**: グリッド内のサブカテゴリ区切りは `--text-muted` のラベル + `FadeRule`
  （§8.6と同一部品）。無札の並置は禁止（§4のスロット群区別ルールに従う）。
- グリッド本体は `SlotGrid` を使い独自gridを作らない。端の安全余白は `--build-menu-edge-safe-area`。
  グリッド右端はオーバーレイ縦スクロールバー分の `--build-menu-grid-scrollbar-reserve` を予約し、
  列幅を削らずその分 `--build-menu-panel-width` を広げる。
- **セッション内状態保持**: 選択カテゴリ・検索文字列・スクロール位置・詳細sticky表示は
  セッション内ストア（§8.5のviewport保持と同族・リロードで消える・永続化なし）で保持し、
  閉じて開き直しても復元する。

## 8.12 スキット会話UI

- **見た目の正はUnityのスキットUI**（`SkitUI.uxml`/`SkitUI.uss` と `MainGameUI.prefab` の `BackgroundText`）。
  Web はそれをCSS/DOM/インラインSVGで再現する。PNGアセットの移植は §6 のとおり禁止。
- **配置は `.stage` 内の `.viewportOverlay` に置く。** UnityのPanelSettings同様に1280基準の固定長トークンを
  `.stage` の一様拡縮へ追従させつつ、overlayの論理外寸だけを実viewport相当へ広げる。これにより横長画面でも
  全幅の面と画面端HUDがstage幅で途中切れしない。Portal直下の固定pxや`position: fixed`は使わず、
  1920設計pxからstage pxへの換算は一律2/3、子要素は`position: absolute`で統一する。

### 通常スキット（blocking）

- **会話窓は `GamePanel variant="skit"`**。画面下部・**全幅ブリード**の帯（高さ `--skit-window-height`）で、
  面は `--skit-window-face`、**上端のみ** `--skit-window-top-fade`（固定長）で世界へ縦フェードする。
  左右・下端はフェードせず、タイトル罫線・下向き三角・右下グリップは持たない。角丸・外枠は付けない。
- 中身は縦に「話者名 → `FadeRule` → 本文 → 送り待ちマーカー」。話者名・本文とも `--text-high-contrast`。
  階層は合成boldでなく**フォントサイズ差**（話者名 `--skit-speaker-font-size` > 本文 `--skit-body-font-size`）で作る（§7）。
- 文字が上端フェード帯に載らないよう、窓の縁に `--skit-window-edge-safe-area` の安全余白を確保する（§2の安全帯前例と同族）。
- 区切り罫線は §6 装飾語彙1 そのもの。専用CSSを書かず `FadeRule` を使い、幅だけ `--skit-rule-inset` で絞る。
  上下余白は正本実測どおり**上詰まり・下空き**（`--skit-rule-margin-top` < `--skit-rule-margin-bottom`）。
- 送り待ちマーカーは本文右下のインラインSVG下向きシェブロン（§6 装飾語彙5）。色は `--select-cyan`。光彩・点滅は付けない。
  寸法・右マージンは正本 `nav_arrow.png` 実測の `--skit-advance-marker-size` / `--skit-advance-marker-right` で、
  本文の罫線インセット（`--skit-rule-inset`）とは別値。
- **選択肢は会話窓の上・右寄せで下から積み上げる。** 各行は固定寸法（`--skit-choice-width` × `--skit-choice-height`）の
  板で、面は `--gauge-track`、左右 `--skit-choice-edge-fade` の水平フェードマスク（原画の水平αランプ実測＝片側27%）。
  上下線（`--bevel-c1`・太さ `--skit-choice-rule-thickness`）と両端の `--bevel-c2` 菱形マーカー
  （§6 装飾語彙4・インラインSVG・`aria-hidden`・寸法 `--skit-choice-marker-size`＝板高の38%）は
  **板端でなく原画どおり内側**（左右 `--skit-choice-rule-inset` / 上下 `--skit-choice-rule-vertical-inset`）に置く。
  これらはフェード帯に載るため、面（`::before`）とは別要素（`::after`・SVG）として全不透明で描く。
  板と会話窓の間隔は `--skit-choices-window-gap`（正本 SelectButton の margin-bottom 実測）で、板同士の `--skit-choice-gap` とは別値。
  ホバーは線と菱形を `--select-cyan` へ、押下は面を `--gauge-track` と `--select-cyan` の混色
  （混色比 `--skit-choice-active-mix`、ModeSwitch の selected-mix 前例に倣う）へ切り替える。新しい色相は足さない。
  ラベルは板の中央、板に収まるよう本文より一段小さい `--skit-choice-font-size`。
  （Unity実機は板とラベルの位置が未整合の未完成状態のため、「固定寸法の板＋中央ラベル」という意図を正とする）
- **ツールボタン（Auto / Skip / UI非表示）は画面右上に横並びの、面を持たないアイコンボタン**とする
  （共通 `IconButton` に各アイコンを children で渡す。面・枠・focus表現は共通側、スキット固有の減衰と点灯だけを機能側が足す）。
  アイコンはインラインSVG、既定不透明度は `--skit-tool-icon-opacity`。正本アイコンは枠に対し**bbox比1.00**のため、
  `--icon-button-*` を `--skit-tool-button-size` と同寸へ局所上書きして枠いっぱいに描く（縮小率のマジックナンバーを置かない）。
  ただし図像はviewBox内余白の分だけ内側（実効約0.8）に留める。viewBoxを外接まで詰めると現行の
  `--skit-tool-gap` でSkip終端バーと隣接アイコンが接触するため、詰めるならギャップ再実測とセットで裁定する。
  Auto の on/off は同一SVGの `data-enabled` による色切替で表し、アイコン自体を差し替えない。
  テキストラベルのボタンにはしない。明るい世界背景でも線が消えないよう、既存の世界分離用暗色トークンによる
  最小限の固定長ドロップシャドウをアイコンへ付け、通常時も不透明で描く。
- **会話窓が非表示の演出中（`textAreaVisible=false`）もツールバーは右上に残す。**
  正本でも TextArea とツールは兄弟で、消えるのは TextArea だけであるため。
- Unity にある Log ボタンは本体機能が未配線（`SkitUITools.cs`）のため Web では出さない。
- **UI非表示からの復帰ボタンは Web 専用に置いてよい。** Unity は Escape キーで復帰するが、CEF は
  スキット中にキー入力主権を持たないため。面を持たない浮遊アイコンとし、ツールボタンと同じ右上に置く。

### 背景スキット（background）

- **面も枠も持たない。** 画面下部中央に「話者名 : 本文」の1行を中央揃えで置くだけ
  （正本 `BackgroundText` と同じ）。文字色は `--text-high-contrast`、サイズ（`--skit-background-font-size`）と
  下端距離（`--skit-background-bottom`）は固定長トークン。
- 会話ボックス・カード・角丸は作らない。
- **`pointer-events: none` を必ず維持する。** 背景スキットはゲームプレイ中に出るため、
  面が入力を捕まえると採掘・設置が死ぬ（`isPointerOverWebUi` の判定対象になるため）。

### トランジション（暗転）

- **§1「画面全体を不透明な面で塗り潰す禁止」の唯一の例外とする。** web モード中は Unity が
  自前のスキットUIを丸ごと無効化する（`SkitManager` の `skitUI.SetActive(!webUiMode)`）ため、
  Web が描かないと暗転演出が消えるため。
- 全画面の不透明黒（`--skit-transition-face`）・`pointer-events: none`・**会話窓より上**（正本 uxml でも Transition は Root の後）に置く。
  レターボックス帯も覆うため stage ではなく Portal に置き、z層は `--z-skit-transition`。
- フェード時間は契約に無いので即時切替とする（duration を契約へ足す場合は別裁定）。

### 共通

- 色・寸法・z層はすべて `index.css` のトークン経由。`--z-skit`（stage内の層序）と `--z-skit-transition` を定義し、
  フォールバック付きの未定義トークン参照（`var(--z-skit, 500)` 等）はしない。
- stage は独自スタッキングコンテキストのため、Portal側の `ModalHost` / `ToastHost`(`--z-toast`=300) は
  常に**会話窓**（stage内 `--z-skit`）より上に来る。スキット中にモーダルは出ない想定であり、これを許容する。
- ただし暗転（`--z-skit-transition`=210）はPortal直下のためモーダルより上・トーストより下に載る。
  モーダルの実効zはMantine既定の200で、`--z-modal` は定義のみの未配線トークンである点に注意する。
- **blockingスキット中はワールドピンHUD（§8.8）とチャレンジHUDを出さない。** Unityでもスキットは画面演出を
  専有するため。判断は各featureが `skit.presentation` の
  `mode` を購読して自前で行い、共有層やHUD基盤にスキット語彙を持ち込まない。

## 8.13 クラフト進捗矢印（矢印グリフ自体がゲージ）

- **矢印グリフゲージは共有部品 `ProgressArrowGlyph`（shared/ui）であり、クラフト画面の素材→結果矢印と機械の加工行（入力→出力間）が使う。** 既定寸法は部品自身の `.arrow` が `--craft-arrow-width`/`--craft-arrow-height` を直接参照して1箇所だけ持ち、呼び出し側は寸法用ラッパーを持たない。機械側だけ余白調整が要るときは `.arrow` の親要素へ最小限のCSSを足す（別名トークンの新設は禁止）。
- **クラフト画面（`CraftRecipeEntry`）の素材→結果の矢印は、矢印グリフそのものが進捗ゲージ**。矢印の下に独立した細いバーを敷くのは禁止（旧 `.craftArrowTrack` / `.craftArrowFill` の緑バーは廃止した）。器＝矢印であり、ゲージを別の要素として増やさない。
- **構造はインラインSVGの3層**（`ProgressArrowGlyph`）。同じ矢印 path を3回描く:
  1. 溝レイヤー: `--gauge-track` で塗った矢印全体
  2. 充填レイヤー: `--color-content-primary` で塗った矢印を `clipPath` の矩形で左から `value`（0..1）分だけ切り出す
  3. 輪郭レイヤー: 塗り無し・`--craft-arrow-outline` のストロークのみ。**最上層に置いて clip を通さない**（輪郭が充填境界で途切れると矢印の形が壊れるため）
- **溝は `--gauge-track`、充填は `--color-content-primary`（白）。** 充填が §8.6 既定の `--gauge-fill` でない理由は、uGUI正本の白矢印がクラフト完了状態の見た目そのものであり、`value=1` で正本と一致させる必要があるため（ユーザー裁定）。この逸脱は矢印グリフゲージ限りで、帯状ゲージへ白充填を広げない。輪郭色はトークン `--craft-arrow-outline`（従来の白矢印から引き継いだシアン）。
- **`value=1` の一致は「塗りの内部が一致」の意味。** 輪郭のアンチエイリアス画素だけは、旧実装が白を背景へ、本実装が白を溝へブレンドするため最大31/255（実測1221px）暗くなる。縁1pxに閉じた差なので視覚的には判別できない。画素完全一致を要求する検査を足さないこと。
- **`value=1` は基準状態であって、連続クラフト中には現れない。** `advanceHoldCraft` は完了フレームで `elapsed` を 0 へ戻すため、長押し中の進捗は `0→1未満` を周回して完了時に 0 へスナップする。`value=1` に到達するのは `craftTime<=0` の即時レシピのみ。完了の演出を足したくなったらここを読むこと（1フレームの満杯表示は §8.13 の transition 禁止と併せてほぼ視認できない）。
- **待機（`value=0`）では矢印が暗い溝＋シアン輪郭になる。** 形はシアン輪郭が担保する。待機時を明るく戻すために溝を明色化するのは禁止（進捗の読み取りが成立しなくなる）。
- **進捗という概念が無い箇所は `value={null}` を渡して静止表示にする。** `null` では `role="progressbar"`・`aria-value*`・充填層・clipPath を一切出さず、溝と輪郭の2層だけを描く。`value={0}`（進捗0の待機）で代用するのは禁止（支援技術に「0%で停止中のprogressbar」と読み上げられ、待機表現に手を入れると進捗概念の無い側へ自動で波及する）。
- **clipPath の id は `useId()` 由来で一意化する。** 同一ページに矢印が複数並ぶと固定 id は衝突して全部が同じ進捗になるため、固定文字列の id は禁止。
- `value` は `clamp01` を通す（NaN は 0）。クリップ矩形は矢印 path の水平範囲（viewBox 座標系）に合わせ、`value=0` で完全に空、`value=1` で完全に充填になること。
- 進捗はアニメーション（transition）を付けない。`useHoldCraft` の rAF が毎フレーム値を更新するため、補間は二重になる。

## 8.14 チャレンジHUD

- 常時表示HUD族の中で唯一**面を持つ**（§1の例外）。面は `GamePanel variant="hud"` が供給し、枠・角丸は持たない。位置決め（`.viewportOverlay` 左上・`--challenge-hud-*`）はHUD側CSSが持ち、面表現はHUD側に書かない。
- 面の外形は実viewport左上24pxに据え、文字は `--hud-panel-padding` の安全帯で内側へ寄せる（画面端から約44px）。面幅は `--challenge-hud-width`（560px。面のpadding 20px×2を含み、実効テキスト幅は520pxを保つ）固定で、目標文が短くても縮めない。
- 構成は「`--text-muted` の従属見出し → `FadeRule` → `--text-high-contrast` の目標一覧」だけとする。
- HUDの本文幅は長文の可読性を保つ固定長とし、`FadeRule`だけを本文幅の約3分の1へ短縮する。位置・本文幅・罫線幅・間隔・文字サイズ・文字影は `--challenge-hud-*` 固定長トークンで管理する。
- 複数目標は受信順で縦積みし、長文・長語を固定幅内で折り返す。
- アイコン、ゲージ、箇条書き装飾、光彩、アニメーションは追加しない。
- 文字影 `--challenge-hud-text-shadow` は面付き後も残し、通知同族の控えめな値（0.35px級）にする。可読性の主担当は面で、影は世界が透ける面上の補助。
- メニュー上端の安全帯 `--menu-upper-safe-area`（168px）は、目標3件までの面付きHUDが収まる高さとして決めている。HUDの寸法・目標行数の上限を変えるときはこのトークンを一緒に見直す。
- インベントリ・研究・建築・チャレンジ一覧・ポーズ等のメニュー中も表示を維持し、**HUD自身は画面状態を参照して位置・幅・間隔・文字サイズ・DOMを切り替えない**。全画面で同じ左上レイアウトを使い、`--menu-upper-safe-area` はその単一HUDが収まる高さを確保する。メニュー本体の高さは `--menu-content-height` を使う。
  研究画面はADR 0014の例外として安全帯を覆うパネルを持ち物の右側だけに敷き、チャレンジHUDはその上に残る。
  同画面では常時表示族（ホットバー・装備HUD）を描画しないため、パネル下端は下安全帯を超えて画面下端まで伸びる。
- `pointer-events: none` を維持し、blockingスキット中は表示しない。

## 8.15 操作モードHUD

- 配置モードの状態表示は、`.viewportOverlay` の実画面右上へ独立して固定し、`GamePanel variant="craft"` に収める。クラフトレシピ詳細と同じ半透明ネイビー面・1px枠・内周線・右下グリップをそのまま再利用する。
- 配置HUDは「`--text-muted` の従属見出し → `FadeRule` → `--text-high-contrast` の詳細一覧」とし、警告だけ `--text-insufficient` を使う。Mantineの `Paper` / `Stack` / `Title` / `Text` は使わない。
- 削除モードでは説明パネルを出さず、uGUI正本の `delete bar.png` と同じ黄黒斜線帯を実viewport上下端へ表示する。正本の1920設計・高さ60px・端中央配置で画面内に見える半幅を1280基準へ換算し、帯高は20pxとする。
- 削除警告帯はCSSの `repeating-linear-gradient` で再現し、色・帯高・斜線周期・角度は `--delete-mode-warning-*` 固定長トークンへ集約する。画像アセットはWebへ移植しない。
- `delete.hud` のチュートリアルアンカーはstage全面の親ではなく下側警告帯へ付け、吹き出しを画面内へ保つ。
- 位置・幅・間隔・文字サイズは `--operation-hud-*` 固定長トークンで管理する。
- 配置HUD・削除警告帯とも光彩、アニメーション、合成boldを追加せず、`pointer-events: none` でゲーム入力を素通しする。
- PlaceBlock / DeleteBar中もチャレンジHUDを表示する。チャレンジHUDは左上、配置HUDは右上、削除警告帯は上下端へ責務ごとに分離する。

## 8.16 装備HUD

- 常時表示HUD族として、面・枠・角丸を持たず、画面右端に下詰めで浮かせる。ホットバーと同じ床に揃え、列は上へ伸ばす。
- 枠数はマスタ可変（`inventory` トピックの `equipment` 長が正）のため、列の高さは内容に任せ、寸法だけを固定長で決める。
- 1枠は `shared/ui` の `ItemSlot`。枠数可変の縦1列はHUD族の配置であり `SlotGrid` の対象外とする（§4）。
- 位置・寸法・間隔は `--equipment-*` 固定長トークンで管理し、ホットバーと同族の寸法は `--hotbar-*` を参照して複製しない。
- 選択の表現は `ItemSlot` の `selected`（`data-selected`）だけとし、新しい色相・光彩・アニメーションは追加しない。
- 選択操作はホイール（素手=-1 を含む循環）。GameScreen中はカーソルロックでクリックできないため、クリック選択は画面表示中に限る。
- ホイールは共有フック `useGameLayerWheel` で受け、**具体側のハンドラ先頭で `isPointerOverWebUi` によりWeb UI上のホイールを捨てる**。一覧のスクロールと二重発火するため。共有フックにこの判断を持ち込まない。
- **常駐HUD族（ホットバー・装備HUD・チャレンジHUD・操作モードHUD等）はscreenレベルのメニューへ埋もれないよう `z-index: var(--z-overlay-panel)` を明示する。**

## 8.17 レシピビューア（単一リスト）

- **タブ・ページャは持たない。** 選択アイテムの全レシピを「クラフトレシピ優先→機械レシピ」の順で
  1本の縦スクロールリストに並べる。1エントリ=1レシピ（ADR 0011）。
- リスト上部は選択中アイテムの名前ヘッダー（名前+`FadeRule`同族の罫線）のみ。装飾タブ（ハンマーSVG）は廃止済みで復活させない。
- **レシピ行の骨格（3カラムgrid・矢印列・結果列の実測値ベース幾何）は表示専用の `views/RecipeRow` 1枚に集約する。**
  クラフト/機械の各エントリは素材・結果の中身と矢印の値だけを渡す。片側だけに骨格の変更を書き足すのは禁止
  （両者が単一リスト内で上下に並ぶため、縦位置・列幅のズレが直接見える）。
- **中央列は「所要秒数 → 矢印 → 操作」の縦積み**（ユーザー裁定 2026-08-20）。秒数は必ず矢印の真上、
  操作（クラフトボタン／機械表示）は必ず矢印の真下に置く。エントリ全幅へ広げる操作要素を作らない。
- **矢印は素材の点数によらず枠の中心に固定する。** 左右の列は `minmax(0, 1fr)` で等幅にすること
  （素の `1fr` はmin-content床で素材側だけ広がり、矢印が右へ逃げる）。中心を保つ幅は
  クラフトボタン幅 `--recipe-craft-button-width` とのトレードオフで、ボタンを広げると素材が縮む。
- **素材・結果は3点以上で折り返し、列数は2で固定して行を増やす**（ユーザー裁定 2026-08-20）。
  3-4点→横2縦2、5-6点→横2縦3。枠の高さは行数に追随して自動で伸びる。入力と出力で同じ規則を使う。
  **列を増やす向きに折り返さないこと**（列が増えるとスロットが縮み、個数テキストが実測6.2pxまで落ちて読めなくなる）。
- **スロット寸法は列数と列幅から算出する**（`min(--recipe-slot-size-max, (100cqw - 間隔) / 列数)`）。
  列数が2で固定されている限り上限に張り付くが、パネル幅が縮んだときの保険としてこの式を保つ。
  段階固定値は点数が想定を超えた瞬間に中央列へ食い込むため使わない。基準幅は `cqw`（列自身の幅）で取る
  ことし、`%` は不可（スロットの親が内容依存幅で循環し、実測で0.8pxまで潰れた）。
  **`--slot-size` をコンテナ自身の `grid-template-columns` で使ってはいけない**（`cqw` が祖先の
  コンテナを見にいき解決に失敗する。実測でスロットが縮まず溢れた）。列幅は `auto` にしてスロット実寸へ追従させる。
- 所持/必要テキストはスロットの外へ出さない。右へはみ出すと最終列で枠の実効幅を超え中央列へ食い込む。
- **クラフトレシピエントリ**は「素材`ItemSlot`列 → 中央列 → 結果`ItemSlot`」の1段構成。
  中央列の操作はクラフト実行ボタン（青グラデ `--recipe-action-background`）で、幅は矢印幅の1.5倍
  （`--recipe-craft-button-width`）固定。この値は矢印を中心に置いたまま素材2点をフルサイズで並べられる上限。ラベルは操作名のみで秒数を含めない（秒数は矢印上が唯一の出所）。
  素材の不足は `data-insufficient` 減光と所持/必要の赤字（`--text-insufficient` 系）で示す。
- **機械レシピエントリ**はクラフトエントリと同じレシピ行ベース（矢印は §8.13 の `value={null}` 静止表示）で、
  中央列の操作が「ブロックアイコン→ブロック名」の縦積みクリック不可表示（`--text-muted`）に置き換わる。
  アイコンは素材スロットと同寸（`--recipe-info-icon-size`）。素材は必要数のみ表示し、所持数チェックは付けない。
- **エントリの `data-testid` はレシピGUIDで一意化する**（`craft-recipe-entry-<recipeGuid>` / `machine-recipe-entry-<recipeGuid>`）。
  同一アイテムに同種レシピが何件並んでも指名できるようにするため、種別だけの固定testIdへ戻さない。
- リストのスクロールは Mantine `ScrollArea` + §8.10 のネイビースクロールバー。
  最大高・エントリ間隔は `--recipe-list-*` 固定長トークンで管理する。
- **アイテム一覧のクラフト可能数バッジは0のとき描画しない**（1以上のみ）。スロット面のグレー/白の
  塗り分け（`data-catalog`/`data-filled`）は維持する。
- **`ItemSlot` の個数バッジと素材の所持/必要テキストは黒**（明色面前提）。不足の赤字だけ例外。

## 8.18 採掘プログレスバー

- **採掘・MapObject破壊の進捗はホットバー直上の `GaugeBar` 1本だけで示す**（常時表示HUD族・viewport族）。面・枠は持たず、`visible` で出し入れする。
- **充填のみ `--color-content-primary`（白）へ逸脱する**（§8.6 の2つ目の逸脱事由）。パネル面が無くゲージが世界へ直に載るため、既定の `--gauge-fill`（寒色グレー）は明るい地形・岩肌の上で溝と見分けがつかない。溝は他ゲージと同じ `--gauge-track` のままにし、器のトーンは変えない。
- 実装は `features/progress/style.module.css` の `.wrapper` で `--gauge-fill` をローカル上書きする。`GaugeBar` 側・トークン定義側は触らない。
- 光彩・アニメーション・完了演出は付けない。

## 8.17 チュートリアルのドラッグガイド矢印

- **D&D操作の説明専用。** `tutorial.presentation` の `dragGuides`（from/to anchor）を受け、
  fromアンカー中心→toアンカー中心へカーソル型インラインSVGが移動をループするアニメーションを
  `TutorialOverlay` に描く。装飾ではなく操作説明であり、他用途への流用は禁止（ユーザー裁定 2026-08-18）。
- from/toの**両方**のアンカーが解決している間だけ表示する。片方でも未解決（対象UIが閉じている等）なら
  何も描かない。「対象UIを開くまでの誘導」はチャレンジsummary文言の責務。
- 図像は `--text-high-contrast` の塗り+世界分離用の最小限の固定長ドロップシャドウ（§8.12ツールボタンと同族）。
  新しい色相・光彩は使わない。寸法 `--tutorial-drag-guide-size`、周期 `--tutorial-drag-guide-duration` の
  固定長トークンで管理する。移動はCSS keyframesのtranslateで、ease-in-out・無限ループ・終端で不透明度を
  落として先頭へ戻る。
- `pointer-events: none` を維持し、z層は既存の tutorial overlay 内（新しい `--z-*` を増やさない）。
- e2e/スクリーンショット検証はアニメーション非同期のため座標一致を要求しない（表示有無のみ検証する）。
- **枠線ハイライトの文言ラベル**: `tutorial.presentation` の outline に `labelTutorialGuid` があるとき、`TutorialOverlay` が枠線の下辺外側・左揃えに `t(challengeTutorial.<guid>.text)` のラベルを描く（ユーザー裁定 2026-08-20）。面は `--world-pin-face`、文字は `--text-high-contrast`、間隔は `--tutorial-highlight-label-gap`、padding・文字サイズはワールドピンのラベルと共有する `--label-face-padding` / `--label-face-font-size`。枠線が非表示ならラベルも出さない。ラベル自身はclip-pathを持たないため、祖先クリップが枠線を1pxでも削る間もラベルは出さない。`t()` の解決結果が空（辞書未着など）のときもラベル面ごと出さない。吹き出し矢印・光彩・アニメーションは付けない。

## 8.19 キー操作ヒントHUD（チュートリアルの keyControl）

- `tutorial.presentation` の kind `keyControl`（tutorialGuid / keyName / uiState）を `KeyControlHintHud` が描く。表示は `ui_state.current` の `state` が `uiState` と一致する間だけで、blockingスキット中は出さない（ユーザー裁定 2026-08-20）。
- 配置は常時表示HUD族の `.viewportOverlay` 内・画面下中央で、ホットバーの床（`--hotbar-floor-offset`）から `--tutorial-key-hint-hotbar-gap` だけ上に置き、採掘ゲージと重ねない。複数は `--tutorial-key-hint-gap` で縦積み。床位置の計算式（`--hotbar-floor-offset` + 各HUD固有のgap）は採掘プログレスバー（§8.18）と共有する。
- 様式は §7 のキー操作ヒント（`<kbd>{keyName}</kbd>` + `t(challengeTutorial.<guid>.text)`）。実装は `LocalizedShortcutHint`（`shared/i18n`）を `layout="prefix"` で再利用する（kbdを常に先頭へ置く様式を型で表明し、`layout="inline"` の文言中マーカー差し込みと識別可能にする）。文字様式はInventoryScreenChrome/ResearchScreenChromeのkeyHintsと共有する `keyHintText` クラス（§7）、kbdとの間隔・縦積み間隔は `--tutorial-key-hint-*` 固定長トークン。面・枠・光彩・アニメーションは持たず `pointer-events: none`。

## 9. やらないことリスト（再掲・明示）

- 全画面UI・不透明な面での塗り潰し（唯一の例外は §8.12 のスキット暗転）
- Mantine標準テーマ剥き出しの見た目
- UI装飾のための画像アセット追加
- GamePanel 以外のパネル背景 / shared/ui 以外のスロット表現
- 機能側CSSへの色・z-index・スロット寸法の直書き
- 機能側CSSでの `user-select` 指定（グローバル1箇所＋入力欄の例外だけで表現する・§7）
- 新しい装飾モチーフ・装飾アニメーションの無断追加
- 面フェード・余白の%指定（固定長トークンを使う。理由なき%は破綻源）
- 用途の異なるスロット群の無札並置（ラベルか区切りで区別する）
- ゲージ本体とは別に進捗表示用のバーを併設すること（器そのものを充填する・§8.13）
- **このドキュメントに書かれていないパターンの使用**（必要なら先にここを更新する）

## 10. 実装後の目視QA（必須）

パネルの新設・寸法変更・レイアウト変更をしたら、コードレビューだけで終えず**mockホストのスクリーンショットで実画面を確認する**
（§0 の実装フローで上げた mock-host + vite dev を使う。単発なら `e2e/capture-eval.ts` の様式でも可。`/__block` `/__uistate` で対象画面を再現して撮影する）。

**目視は最終確認であって原因特定の手段ではない。** 症状を見つけたら §0.4 の実測・ablationへ戻る。

チェック項目:
1. **端**: 内容（タブバー・ボタン・グリッド）がパネル面のフェード帯に載って「はみ出て」見えないか。逆に、内容の直後で面が途切れて「切れて」見えないか（内容の縁〜フェード開始の余白が左右で対称か）。拡大クロップで**4辺すべて**確認する。内容量でサイズが決まるパネルは特に右端・下端が危ない（共通paddingがフェード幅未満の辺）
2. **中央と対称**: 中央揃え指定の要素が実際にパネル中心線上にあるか。左右の要素数が非対称なケースで確認する
3. **区別**: 無札のスロット群・用途が読めない要素が並んでいないか
4. **重なり**: 対象画面のuiStateを正しく設定したか（別パネルの透け重なりを問題と誤認しない・実際の重なりを見逃さない）

%指定や幅依存の値を触った場合は、基準幅（持ち物378px）・大型幅（機械759px）・研究パネル幅（867px＝1280-(378+35)）で確認する。
画面端HUD・全幅帯を触った場合は、1280×720に加えて2432×786等の高さ制約型横長viewportでも、左右端・右上アンカー・固定長の内容幅を確認する。

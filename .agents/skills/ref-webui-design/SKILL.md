---
name: ref-webui-design
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

JSが `getComputedStyle(...).getPropertyValue(...)` と `parseFloat` で読む寸法トークンは、単一のpx値に保つ。カスタムプロパティの `calc(...)` はこの読み方では数値に評価されず、NaNになる。式へ変更する前に読取側を検索し、必要なら単一px値のトークンへ分けてJS側で合成する。現行の `labelGapToken.ts` と `highlightGlowToken.ts` もこの制約を持つ。

原因候補が複数あるときは **ablation** で切る。要素を1つずつ `display:none` にする／変数を1つずつ変える／値を0.1px刻みでスイープして、症状が消える点を見つける。

> 実例（2026-08-22 CRAFT RECIPE一覧）: 「黒い枠線」は `type="always"` が描いた**つまみ幅0の水平スクロールバー**（`scrollWidth === clientWidth` で溢れゼロ）、「不要なスクロール」は個数バッジの5px はみ出し（`.count` を消すと `scrollHeight - clientHeight` が 5→0）だった。どちらも見ただけでは特定できず、実測とablationで初めて確定した。

### 0.4.5 実装前の前提審査（症状を直す前に器と箱・既存定数を審査する）

**既存の寸法定数を据え置く前に、責務と前例を1行で書く（実測の直後・plan 確定前に必ず）。**

- 実測は症状の値（溢れ量・色・はみ出し px）だけでなく**器の値**（パネル本文の高さ・親の rect）を同じ出力に並べ、症状箇所の箱（スクロール領域・クリップ矩形）の大きさが器と一致しているかを**先に**読む。箱が器より小さく一致していないなら、症状を追う前に「この箱の大きさは何が決めるべきか（器／中身／正本）」を plan の項目にする。症状は箱の大きさの帰結であることが多く、箱を直せば複数症状が同時に落ちる。
- コード内コメントや過去イテレーションで正当化された既存定数（`mah`・固定 px・`--*-max-height`・`minHeight`）は**審査対象**であって所与ではない。据え置くなら plan に「責務＝○○が決めるべき値だから据え置き」と1行書く。「総スクロール範囲が不変」「N段が収まる」「ノブ比が正本と一致」は中身の量・見た目の根拠であり、箱の大きさの責務の根拠にはならない。
- 同形の兄弟（同じ機構を使う他パネル・同じ DOM に刺さる Portal オーバーレイ）を `grep` で列挙し、一括適用するか、外すなら理由を plan に書く。**理由無しの「別issue」外出しは禁止**。前例（§8.10 の前例パネル等）と形が違うなら、違う理由を plan に書く。

> 実例（2026-08-22 CRAFT RECIPE 一覧）: 初回計測の同じ出力に `panel.height 452` と `viewport.height 46 / scrollHeight 51` が並んでいたのに溢れ 5px だけを読み、既存の `mah={381.2}` を「7段が収まり総スクロール範囲が不変だから据え置きで正しい」と通して、黒帯→偽の溢れ→領域の大きさ→ハイライトのラベルと症状ごとに4回パッチした。問うべきは「スクロール領域は器（パネル本文）が決める」の一問で、それで4症状は同時に落ちた。同形の中央レシピビューアを「別issue」に外して戻らなかった。

### 0.5 確定したらテストと目視QA

`pnpm lint` / `pnpm test` / `pnpm test:e2e` を通し、§10 の目視QAチェック項目を実施する。
**挙動を固定していた既存 e2e があれば、裁定に合わせて反転させる**（古い assertion を残したまま実装だけ変えない）。

- 英語の表示文言を期待するe2eでは、各テストで `setTopicScenario(page, "english")` によりロケールを明示する。mock-host の localization topic の既定値は `japanese`。文言不一致で落ちたら、ポートや接続を疑う前にロケールの設定と反映を確認する。
- ScrollAreaの「件数が増えても表示領域の高さが変わらない」を検証するときは、固定高パネルのboundingBoxではなく、実際のScrollArea視口の `clientHeight` を増加前後で比較する。併せて `scrollHeight - clientHeight` が非溢れから溢れへ変わることを確認する。前例は `e2e/support/layoutAssertions.ts` の `expectScrollsOnlyWhenOverflowing`。外枠の固定高だけを測って内部視口の潰れを見逃さない。
- Playwrightで遷移後の可視性・opacityを検証するときは、`toBeVisible`・`toHaveCSS`等の自動リトライmatcherを使う。`evaluate`で採った瞬間値への単発`toBe`は、アニメーション途中を拾って偽陰性になる。トークンに追従する寸法の関係を検証するときは、期待するpx値を複製せず、`getComputedStyle()`で解決済みの寸法・間隔を取得して実測矩形と比較する。固定値そのものが仕様の場合は、その値の検証を別に保つ。
- topic到着を契機に初期選択・中央寄せなどを一度だけ行うUIでは、完成した状態だけをfixtureにしない。サーバー状態が未着の初回payloadを先に配信し、その後に有効な状態を配信する順序を再現する。初回payloadの受信と必要データの準備完了を区別し、未着状態で一度きりの初期化を消費しないことを確認する。
- 受信スキーマにZodの`transform`を追加・変更した場合は、変換前のwire payloadを`deliverTopicPayload`へ渡し、ストアに変換後の値が入ることを入口テストで確認する。スキーマ単体の成功や、変換後の値をストアへ直接渡すテストだけでは、検証後に生payloadを格納する誤りを検出できない。

### 0.6 後片付け

起動時に tunnel・vite・mock-host のPIDまたは実行セッションIDを控える。終了時は自分が起動したプロセスであることを確認し、そのプロセスだけを停止する。サービス名による `pkill -f` は別セッションや本番の同名プロセスを巻き込むため使わない。検証をsubagentへ委譲する場合も、この停止対象の制約を渡す。停止後、`moores-wt rm` で worktree を削除する。

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
  - 例外（ADR 0040・出展モードの言語選択ゲート・§8.20a）: 出展モード（`MOORESTECH_EVENT_MODE=1`）の
    言語選択ゲートのみ、全画面を不透明黒（`--event-language-gate-face`）で塗り潰してよい。
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
- **第三の所属として背面viewport族がある**（ADR 0017）。`.viewport` 直下・`.stage` の裏（`--z-viewport-behind-stage`）に置く。stage族でもviewport族でもなく、`--ui-scale` は自前で掛ける（通知は掛けている・§8）。現状の唯一の利用者は通知（§8）。
- 基準解像度1280×720では stage と viewport が一致するため、族の移動だけでは描画結果が変わらない。
- `--ui-scale` を掛ける層を新設・移動するときは、Portal先も含め、配下が参照する寸法トークンの `vw` / `vh` を検索する。これらは実viewportですでに拡大した長さへさらにscaleが掛かるため、内容寸法にはstage座標の固定長を使う。1280×720だけでなく異なる解像度でも実寸を測る。現行の `tokens.css` の `--notification-width: 256px` が前例。

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
- **レシピ選択を持つ機械ブロックのみ大型レイアウト**: `viewer-start / items-end` の2列を占有し、上端は持ち物パネルと揃え、下端はホットバー手前で止める（研究パネルは持ち物の右隣から画面端までの別レイアウトのため前例には引かない）。中身はタブを持たず「レシピ選択モード / インベントリモード」の2画面をSatisfactory方式で往復する（§8.7）。レシピ0件のブロックは従来の小型パネルのまま。

## 3. モーダル

- **モーダルの面もインベントリパネル系（GamePanelのトーン）を使う。** Mantine標準テーマ剥き出しの白/グレー面を出さない。
- モーダルは中央配置+backdropディム。backdropはモーダル専用の1枚のみ（screen backdropと二重にしない）。
- 確認・入力等の定型モーダルは `ModalHost`（`ui.modal` トピック駆動）を通す。機能側が勝手に独自モーダルをマウントしない。

## 4. スロットとグリッド

- **アイテム・ブロック・液体を1マスで表すものは `shared/ui` のコンポーネントのみ。**
  - `ItemSlot` / `BlockSlot` / `FluidSlot` / `FluidAmountSlot` / `FluidSlotRow` / 素枠は `SlotFrame`。
  - 並べるのは `SlotGrid`（既定9列）。独自の grid CSS でスロットを並べない。
  - **ただしパネル内のスロット群に限る。常時表示HUD族（ホットバー・装備HUD）は `SlotGrid` の対象外**で、HUD自身の固定長トークンで組んだ1列のflexに並べる（前例: `HotbarPanel` / `EquipmentPanel`）。折返しの無い1列にグリッドの列数概念を持ち込まないため。
  - **もう1つの例外はレシピ行（§8.17）**。素材・結果のスロット寸法はコンテナクエリ（`container-type: inline-size` + `cqw`）から引くため `SlotGrid` の既定 `grid-template-columns` を必ず上書きすることになる。`--slot-size` を要素自身の `grid-template-columns` で使うと `cqw` が祖先コンテナへ解決して失敗する（実測でスロットが縮まず溢れた）ため、`RecipeRow` は独自gridを持つ。ユーザー裁定 2026-08-20。
  - **`FluidSlot` は「背面に amount/capacity の縦フィル ＋ 前面に液体アイコン ＋ 右下に量バッジ」の3層。** 背面フィルの色は`GET /api/master/fluids`で配信される液体マスタの色（fluidGuid解決）であり、クライアント側で導出しない。マスタ未取得中はフィルを描かない（フォールバック色でごまかさない）。アイコン取得に失敗した液体は背面フィルだけが残る（`FluidIcon` へ `fallback={{ kind: "none" }}` を明示する）。
  - **`FluidAmountSlot` は容量の無い液体量の1マス**（レシピ行・選択中レシピ表示）。`SlotFrame` の白面（`data-filled`）に液体アイコン＋右下にレシピ量バッジ（`formatSlotAmount`）で、背面フィルは持たない。背面フィルが無いぶんアイコン取得失敗を隠せないため、ここだけは `fallback={{ kind: "label", text: 辞書の液体名 }}` で液体名を残す（空の白面を「中身あり」と主張しないため。フォールバックの選択は `FluidIcon` の必須propで呼び出し側が明示する）。**`ItemSlot`/`BlockSlot` が使う `#id` テキストフォールバック（`kind: "idText"`）は液体では使わない** — 液体のidは36文字GUIDで、マスに収まらず隣接スロットへ溢れる（ユーザー裁定 2026-09-10）。液体名も長くなり得るため、`GameIcon` のフォールバックラベルは1行省略（`nowrap` + `text-overflow: ellipsis`）で必ず枠内に収める。辞書が未着で名前が空のときは `kind: "none"` として白面も出さない。タンク（amount/capacity）を表すときは `FluidSlot`、レシピ量を表すときは `FluidAmountSlot` と役割で使い分ける（ADR 0054、ユーザー裁定 2026-09-10）。
  - **1マスの中身（アイコン寸法・量バッジ）の規則は `shared/ui/slotContent.module.css` が正本**で、`ItemSlot`/`BlockSlot`/`FluidAmountSlot` は `composes` で引く（前例 `panelBodyScrollArea.module.css`）。他コンポーネントの私有CSS moduleから借用したり、同じ宣言を複製したりしない。液体量は桁が多いため `.amount` 側で寸法（`--fluid-amount-font-size`・右寄せ）だけを上書きする。
- スロット寸法は `--slot-size`、間隔は `--slot-grid-gap` の局所上書きで調整する。コンポーネント内にpx直書きしない。
- スロットの状態表現は data属性（`data-selected` / `data-filled` / `data-catalog` / `data-insufficient`）に統一。新しい状態が要るなら data属性を追加する。
- マウス操作の契約は `useSlotMouse`（左押下・右押下・ドラッグ進入・ダブルクリック）。スロットに生の onClick を生やさない。
- **同一要素でクリックと複数要素をまたぐドラッグ&ドロップ（掴む→運ぶ→離した場所で判定）を両立させる場面は `useHotbarDragSource`（前例: `HotbarPanel`/`BuildMenuSlot`）。** `useSlotMouse` は単一要素内の左右押下・ドラッグ進入・ダブルクリックのみが対象で、クロス要素D&Dは対象外。5px未満の移動はタップ（クリック相当）、以上はドラッグへ確定し、`pointerdown` で `preventDefault()` して旧 `mousedown` 経由クリックとの二重発火を止める。ドロップ先は `document.elementFromPoint` + 対象要素の `data-*` 属性（例: `data-hotbar-slot-index`）で判定し、HTML5 DnD（`draggable`/`dragstart`等）は使わない。
- ホバープレビュー（ホバー中スロットの詳細を別領域へ出す）は SlotFrame/ItemSlot の `onHoverChange` を使う。機能側で生の onMouseEnter/Leave をスロットに生やさない。
- **用途の異なるスロット群を同一パネル内に並置する時は、ラベル（`--text-muted`）または `FadeRule` の区切りで必ず区別する。** 無札の並置は入出力と誤読されるため禁止（例: アップグレードスロット）。
- **左右のスロット数が非対称になり得る行の中央要素（進捗矢印等）は `1fr auto 1fr` グリッドで中央に固定する。** 行全体のflex中央寄せは個数差で中央要素がずれるため使わない。
- **アイコンの上に重なるテキストは、文字色の反対色の縁で浮かせる**（ADR 0033）。黒文字には白縁、白文字には黒縁。
  太さは `--icon-text-stroke-width` の1本で全系統共通、色は `--icon-text-stroke-light` / `--icon-text-stroke-dark`。
  縁は `-webkit-text-stroke` + `paint-order: stroke fill` の真のストロークで描き、`text-shadow` による擬似縁・ぼかし影は使わない。
  適用は tokens.css の共有クラス `iconTextOutlineLight` / `iconTextOutlineDark` を TSX で合成して行い（前例 `keyHintText`）、
  featureのCSSは位置決めと文字色だけを持つ。現在の適用先は `ItemSlot .count` / `ItemSlot .shortageCount` /
  `FluidSlot .amount` / `FluidAmountSlot .amount` / `HotbarPanel .num` の5箇所。
- **素材の「所持/必要」は `ItemSlot` の `shortage`（`{ ownedCount, requiredCount, tooltipKey }`）だけが描く。**
  クラフト・研究・建設メニューの3系統はこの1箇所へ集約済みで、feature側に絶対配置のカウント要素とそのCSSを複製しない。
  赤字にするかは呼び出し側の `insufficient` が決め（免除等の合成は呼び出し側の責務）、位置だけ
  `--shortage-count-right` / `--shortage-count-bottom` で用途ごとに寄せる（レシピ行はスロット内へ収める・§8.17）。
  アイコンに重なっていない文字（通知・キーヒント・目標HUD・ボタンラベル）はこの様式の対象外で、従来の文字影のままにする。

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
- ハイライトのクリップは `shared/tutorialAnchor/ancestorClip.ts` の `clipPathInset` を前例にする。`inset(0px)` でもborder box外のグローは切れるため、クリップ不要な辺には実際のグロー幅に対応する負のinsetを残す。実測テストでcomputed `clip-path`を読む場合は、CSS短縮形の1〜4値を展開する（前例: `e2e/tests/system/tutorialHighlightClip.spec.ts`）。4値固定の正規表現で判定しない。
- 装飾アニメーションは基本入れない。トランジションを入れる場合もe2eが同期検証できること（モーダルは duration 0）。
  - **例外は通知の出入り（§8）と、チュートリアル誘導の脈動（§8.8/§8.17/§8.19・ADR 0039）**。通知は入場＝左から `--notification-shift` のスライド＋フェード、退場＝その逆再生で、色相・形・光彩は動かさない。
  - アニメーションを足す場合、テスト時に尺をゼロへ落とす抜け道は作らない（実挙動と乖離するため）。計算値の `animation-name` はCSS Modulesがハッシュ化するので、e2eでは部分一致で照合する。
  - **チュートリアル誘導の脈動は tokens.css が正本**: 実数値を焼いた `@keyframes tutorial-attention-pulse-strong`（1.08）/ `-subtle`（1.03）と、周期 `--tutorial-pulse-duration`（1200ms）を置く。利用側は `animation: var(--tutorial-pulse-strong|subtle) …` と名前トークン経由で参照する（**素名を直書きすると CSS Modules がハッシュ化してキーフレームに届かず、無言で脈動しない**）。振幅を `var()` で利用側から注入する形は採らない — 書き忘れた要素の既存 `transform` ごと無言で消え、合成スレッドにも載らないため（ADR 0039）。
  - 脈動する要素の矩形をe2eで実測するときは `e2e/support/pulseFreeze.ts` の `freezeAttentionPulse(page)` で位相を `scale(1)` に固定してから測る（尺は殺さない）。

## 7. 文字

- フォントは `--font-ui` のみ。個別 font-family 指定禁止。
- 実フォントは単一ウェイトのため**合成bold/italicは禁止**（`font-synthesis: none` を崩さない）。
- **表示文字列は必ず `t()` を通す。** JSXへの生リテラルは lint（no-jsx-visible-literal）で落ちる。
- キー操作ヒントは `<kbd>` + `t()` の既存様式（InventoryScreenChrome の keyHints）に従う。**文字様式は `app/tokens.css` の低詳細度クラス `:where(.keyHintText)` が唯一の正**で、使う側は `keyHintText` を併記し、機能側CSSには位置決め（position / gap / z-index）だけを残す。同じ文字様式の宣言ブロックを機能側へ複製しない。
- **テキスト選択は入力欄のみ**（§9・ADR 0021）。`app/index.css` の `body { user-select: none }` ＋ `input, textarea { user-select: text }` が唯一の正で、機能側CSSで `user-select` を書かない。

## 8. コンポーネント別仕様（移設）

通知・グラフビュー・shared/ui 部品・機械UI・ワールドピン・検索入力・スクロールバー・建設メニュー・スキット・進捗矢印・各HUD・レシピビューア・全画面ゲート等の個別仕様（§8〜§8.21）は `moorestech_web/webui/docs/design/components.md` にある（節番号は維持）。**既存コンポーネントを触る・同種を新設する前に該当節を読む。** ホワイトリストの大原則はその文書にも及ぶ。

---

## 9. やらないことリスト（再掲・明示）

- 全画面UI・不透明な面での塗り潰し（例外は §8.12 のスキット暗転・§8.20a の出展モード言語選択ゲートだけ。外殻は §8.20 の `FullScreenGate` を共有）
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

パネルの新設・寸法変更・レイアウト変更・行やスロットへの要素追加をしたら、コードレビューだけで終えず**mockホストのスクリーンショットで実画面を確認する**
（§0 の実装フローで上げた mock-host + vite dev を使う。単発なら `e2e/capture-eval.ts` の様式でも可。`/__block` `/__uistate` で対象画面を再現して撮影する）。

**撮影対象は変更の再現条件を含む画面を選ぶ。** 撮影スクリプトの既定（`capture-machine-qa.ts` の `gearMachine` 等）を使い回さず、
変更が触った要素（液体レシピ・複数出力・ゴースト等）を実際に持つ fixture／ブロックで撮る。実アセットで撮る（`MOCK_DEMO=1`。
非DEMOはアイコンが404→非描画になり、肥大も欠落も観測できない）。
（実例 2026-08-30: レシピ行に足した液体 `FluidIcon` の寸法無し素置きが、`gearMachine` 固定の撮影では画面に出ず、実機で500px原寸描画になって発覚）

**目視は最終確認であって原因特定の手段ではない。** 症状を見つけたら §0.4 の実測・ablationへ戻る。

チェック項目:
1. **端**: 内容（タブバー・ボタン・グリッド）がパネル面のフェード帯に載って「はみ出て」見えないか。逆に、内容の直後で面が途切れて「切れて」見えないか（内容の縁〜フェード開始の余白が左右で対称か）。拡大クロップで**4辺すべて**確認する。内容量でサイズが決まるパネルは特に右端・下端が危ない（共通paddingがフェード幅未満の辺）
2. **中央と対称**: 中央揃え指定の要素が実際にパネル中心線上にあるか。左右の要素数が非対称なケースで確認する
3. **区別**: 無札のスロット群・用途が読めない要素が並んでいないか
4. **重なり**: 対象画面のuiStateを正しく設定したか（別パネルの透け重なりを問題と誤認しない・実際の重なりを見逃さない）
5. **寸法**: 追加・変更した要素（アイコン・スロット・バッジ）が想定寸法で描かれ、隣の同種要素と同寸か。寸法を渡していない裸の `<img>` は実アセット原寸（500px）で描かれる

%指定や幅依存の値を触った場合は、基準幅（持ち物378px）・大型幅（機械759px）・研究パネル幅（867px＝1280-(378+35)）で確認する。
画面端HUD・全幅帯を触った場合は、1280×720に加えて2432×786等の高さ制約型横長viewportでも、左右端・右上アンカー・固定長の内容幅を確認する。

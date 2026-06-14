---
name: csharp-branch-review-cockpit
description: C# ブランチの変更をレビューするためのローカル Web cockpit(Vite+React)を生成する。file-tree サイドバー＋デフォルト折りたたみコードビュー(Prism vsDark＝difit と同一)＋依存インスペクタ(依存先/依存元チップ)＋Dep-Map(asmdef 列×ディレクトリ階層・A/M/D 色カード・ホバーで方向矢印付き1ホップとメソッド一覧が展開)を持つ。同梱テンプレと埋め込みデータで「今回の CleanRoom 版」を寸分違わず再現でき、extract.py で任意のブランチにも生成できる。Use When — 「レビュー cockpit を作って」「変更を折りたたみコードと依存マップで見たい」「difit 風のローカルレビュー画面を出して」「CleanRoom の cockpit を再現」と言われた場合。
---

## 概要

ローカルで動くレビュー特化ツールを生成する。3 要求を 3 ゾーンに割り当てた「Cockpit」と、依存関係を俯瞰する「Dep-Map」の 2 モードを持つ単一ページ Web アプリ。日本語解説は持たず、コードと構造そのものを見せる。

- **req1 変更ファイルの階層** → 左サイドバー(file-tree、A/M/D アイコン、+/- 行数、asmdef 色ドット、検索、reviewed チェック)
- **req2 各ファイルの依存** → 右インスペクタ(依存先↓/依存元↑チップ、クリックでジャンプ)＋ Dep-Map の方向矢印エッジ
- **req3 認知負荷の低減** → メソッド本体・#region をデフォルト折りたたみ(`{ Nline }` バッジ表示)、展開は任意

データ層は Python の `extract.py` が `git diff base...branch` と C# 構造を解析した `public/data.json`。フロントは完全にデータ駆動。

## 前提条件

- `node` / `npm`、`python3`（標準ライブラリのみ）。対象は C# の git リポジトリ。
- 検証に `playwright-cli`（任意）。
- **配置先は必ず作業対象リポジトリの外**（既定 `/tmp/review-cockpit-<topic>/`）。repo 内に作ると `node_modules`・生成物が `git status` を汚す。
- C# のコードハイライトは difit と完全同一構成（`prism-react-renderer` ＋ 動的ロードした `prismjs/components/prism-csharp` ＋ `vsDark` テーマ背景除去）。これは `templates/src/lib/prismSetup.ts` に内包済み。

## 手順

用途で 2 経路ある。**A は repo の現在状態に依存せず再現**（ファイルが移動・改名されても不変）、**B は任意ブランチから生成**。

### 経路 A — 今回の CleanRoom 版を寸分違わず再現（repo 非依存）

埋め込み済みの `references/cleanroom.data.json`（全 50 ファイルのテキスト・構造・依存を含む）を使う。対象 repo を読まないので、元 repo がリファクタされていても完全一致する。

```bash
DST=/tmp/review-cockpit-cleanroom
mkdir -p "$DST" && cp -R {skill}/templates/. "$DST"/
cp {skill}/assets/cleanroom.data.json "$DST"/public/data.json
cd "$DST" && npm ci && npm run dev      # http://localhost:5182/
```

`{skill}` はこのスキルの絶対パス。`npm ci` は同梱 `package-lock.json` で依存を正確に固定する。

### 経路 B — 任意の C# ブランチ用に生成

```bash
DST=/tmp/review-cockpit-<topic>
mkdir -p "$DST" && cp -R {skill}/templates/. "$DST"/
cd "$DST" && npm install
python3 scripts/extract.py --repo <repo-abs-path> --base <base> --branch <branch> --out public/data.json
# 変更 .cs を自動抽出(テスト除外)。テストも含めるなら --include-tests
# 明示ファイル一覧で固定するなら --files-from list.txt(1行1パス, repo相対)
npm run dev
```

asmdef は最寄りの `*.asmdef` から推定し、列順・色は `src/lib/asm.ts` の既知マップ（未知はパレット/末尾列にフォールバック）。**列順や色を厳密に制御したい asmdef は `asm.ts` の `ASM_COLOR` / `ASM_LAYER` に 1 行ずつ追記する**。

### 検証（playwright-cli、完了前に必須）

`playwright-cli open <url>` の後、最低限:
- コンソールエラーが favicon 404 のみであること
- Cockpit でファイル選択 → 折りたたみコード表示、`{ Nline }` バッジ、依存チップのジャンプ
- Dep-Map でノードにホバー → 方向矢印エッジ＋カード下方展開、下のカードが押し下げられスクロールバーが変動しない

## Gotchas

実装中に実際に踏んだ落とし穴。これらを外すと壊れる、または見た目が劣化する。

- **Prism C# 文法は `window.Prism = Prism`（prism-react-renderer の Prism）を設定してから `prismjs/components/prism-csharp` を動的 import する**。順序を誤ると csharp 文法が prism-react-renderer 側に登録されず色が付かない（`prismSetup.ts` がこの順序を担保）。
- **vsDark テーマは背景色を除去して使う**。コード面の背景は GitHub-dark 系で別管理。除去しないとテーマ背景がパネルと二重になる。
- **折りたたみの `{` 表示**: この repo は Allman（`{` が独立行）。折りたたみ時は独立 `{` 行を描画せず、signature の直後（`>` の隣）に `{ Nline }` バッジをインライン表示する。K&R は末尾 `{` を除去してからバッジを付ける。次の行に `⋯ Nline` を出す旧方式は不可（読みにくいと却下された）。class/struct/namespace の `{` は折りたたみ対象外なので残る（スコープを見せるため意図的）。
- **Dep-Map のエッジはアイドル時は非表示**。全 182 本を常時描くとハイラーボールになり却下された。ホバー中のノードの 1 ホップのみ描画する。
- **カード色は変更種別(A=緑/M=橙/D=赤)**。asmdef は列で表すので asmdef 色では塗らない。`A`/`M` の文字バッジも出さず、左ボーダー色だけで表す。
- **ハイライト色はエッジ色に一致**: 青エッジ(依存先/imports)で繋がるカードは青、橙エッジ(依存元/used by)は橙。
- **ホバー展開は下のカードを押し下げる**。展開で増える高さを同一列の下方カードへ加算し、`PAD_BOTTOM`（最大展開分）を常時確保してキャンバス高さを固定する。これをしないと最下部カードのホバーでスクロールバーが伸縮する。横にも `PAD_RIGHT` を確保。
- **ファイル名は折り返さない**。カード幅を最長ファイル名に合わせて確保（フォント 10.5px）。2 行折り返しは却下された。
- **Dep-Map の列内はフラットなディレクトリ表示ではなく、共通プレフィックスを畳んだ実ディレクトリ木をインデントで描く**。
- **asmdef 推定は最寄りの `.asmdef`**。パス部分一致のハードコードにしないこと。これによりファイルがサブディレクトリへ移動しても asmdef が変わらない（実際に CleanRoom 群は後日 `Blocks/CleanRoom/Boundary/` 等へ移動した）。
- **完全再現は repo に依存させない**。対象 repo のファイルは移動・改名・HEAD 前進で変わる。寸分違わぬ再現は埋め込み `cleanroom.data.json`（テキスト同梱）を正とする。経路 B は「その時点の repo 状態」を映すもので、過去の特定スナップショットの再現には使えない。
- **依存エッジはコメント除去後に宣言型名の参照で算出**。コメント内の型名で偽エッジを作らない（interface が `Datastore` をコメントで言及して誤検出した事例）。
- **`public/data.json` は生成物**。コミットしない（テンプレの `.gitkeep` のみ）。
- **C# パースはヒューリスティック**（brace マッチ＋`#region`）。式 body(`=> expr;`)は折りたたみ対象外。多くは Allman 単一行 signature 前提。
- **`extract.py` を npm の `predev` で自動実行しない**。引数必須化したため自動起動は失敗する。データ生成は明示ステップ。

## Available scripts

- `scripts/extract.py` — `git diff base...branch` ＋ C# 構造解析 → `data.json`。`python3 scripts/extract.py --help`。`--repo/--base/--branch/--out` 必須、`--files-from`/`--include-tests` 任意。

## Bundled templates / data

- `templates/` — アプリ一式（verbatim）。`src/`（App・modes・components・lib）、`styles.css`、`vite.config.ts`、`tsconfig.json`、`package.json`、`package-lock.json`、`index.html`、`scripts/extract.py`。コピーしてそのまま動く。
- `assets/cleanroom.data.json` — 経路 A 用の完全再現データ（50 ファイル分、512KB）。
- `assets/cleanroom.files.txt` — 再現対象ファイル一覧（参考。パスは生成時点のもので、現 repo では移動済み）。
- `references/architecture.md` — 各テンプレファイルの役割と、改造時に触る箇所。フロントを変更する前に読む。

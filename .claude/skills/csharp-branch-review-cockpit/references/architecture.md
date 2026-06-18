# テンプレ構成と改造ガイド

フロントは `public/data.json` を唯一の入力とするデータ駆動アプリ。改造時はまず該当ファイルを見る。

## データ層

- `scripts/extract.py` — `git diff base...branch` で変更 .cs/.ts/.tsx を集め(テスト・dotディレクトリ除外)、各ファイルを解析して `data.json` を出力。
  - `parse_csharp()` — usings / 宣言型 / `#region` / メソッドの fold 範囲(brace マッチ＋多行 signature 対応)。
  - `parse_ts()` — imports(相対/`@`/動的) / トップレベル宣言 / 関数・アロー・メソッドの fold 範囲(K&R brace、JSX 式の `{` は除外)。
  - 依存(言語別・グラフは言語内で閉じる) — C#=コメント除去後に「他ファイルの宣言型名」を参照していれば edge。TS=import 指定子を相対/`@/`→src で解決し変更集合内のファイルに当たれば edge。いずれも逆依存も算出。
  - `group_of()`/`ts_group()` — 列キー。C#=最寄りの `*.asmdef` 名、TS=`pkg/スライス`(pkg の `src` 直下を先頭2セグメントで畳む。例 `webui/bridge`・`webui/features/recipe`)。TS は1パッケージが縦長1列になるのを避けるため細分する。group に `/` を含むことを `asm.ts` が TS スライス判定に使う。`sourceRoot` — 対象ファイルの共通ディレクトリ(混載で共通が無ければ空=フルパス表示)。
- `src/lib/types.ts` — `data.json` の型。`DataSet { branch, base, sourceRoot, files[], asmdefs[] }`、`FileRec { path,name,asmdef,status,add,del,text,parsed,addedLines,depsOut,depsIn }`。`asmdef` フィールドは C#=asmdef / TS=package の汎用「列キー」。

## 状態・共通

- `src/lib/store.tsx` — Context。`mode`(cockpit/depmap)、`selected`、`expanded`(fold key 集合)、`reviewed`(localStorage)、`search`、各アクション。`jumpToFile` は選択＋cockpit 切替。
- `src/lib/rows.ts` — `buildRows(file, expanded)`。折りたたみ状態に応じた表示行を生成。collapsed メソッドは signature ＋ `{ Nline }` バッジ、Allman の独立 `{` 行は出さない。`methodKey`/`regionKey`。
- `src/lib/asm.ts` — group → 色(`asmColor`)・列順(`asmLayer`)。**既知 asmdef はここに固定。未知 C# は層 50。TS スライス("/"含む)は `tsLayer`(60+、基盤→app)＋`TS_PALETTE`。列順・色を厳密にしたい場合はここを調整**。
- `src/lib/status.ts` — A/M/D → 色(`statusColor`)。
- `src/lib/members.ts` — ファイルの宣言型・メソッド signature・#region を行順に並べる(Dep-Map のホバー展開で使用)。
- `src/lib/prismSetup.ts` — **difit と同一の C# ハイライト**。prism-react-renderer の Prism を `window.Prism` に登録 → `prism-csharp` 動的ロード → `vsDark`(背景除去)。順序厳守。

## Cockpit モード（3 ゾーン）

- `src/modes/Cockpit.tsx` — レイアウト(tree / center / inspector)。
- `src/components/FileTree.tsx` — 変更ファイル木。`data.sourceRoot` を剥がして表示。A/M アイコン、+/-、asmdef ドット、検索、collapse/expand all、reviewed。
- `src/components/FileHeader.tsx` — 中央上部のファイル見出し(status/asmdef/+-/fold 全展開/reviewed)。
- `src/components/CodeView.tsx` — `<Highlight>`(prism vsDark)で全行トークン化し、`buildRows` の可視行だけ描画。collapsed は `{ Nline }` バッジをインライン。
- `src/components/DepChips.tsx` — 依存インスペクタ。依存先↓/依存元↑チップ、クリックでジャンプ。

## Dep-Map モード

- `src/modes/DepMap.tsx` — asmdef を列(`asmLayer` 順)、列内を `data.sourceRoot` 基準のディレクトリ木でインデント配置。カード左ボーダー＝`statusColor`。ホバーで 1 ホップのみエッジ描画(青=依存先/橙=依存元)、中点に方向矢印、カードを下方展開(下カードは `activeExtra` 押し下げ)。`PAD_BOTTOM`/`PAD_RIGHT` でスクロール固定。
- 主要定数: `NODE_W`(カード幅・名前折返し回避)、`HEAD_H=33`(0.75x)、`INDENT`、`BODY_MAX`。

## スタイル

- `src/styles.css` — ダーク IDE テーマ。vsDark トークン色はインライン(getTokenProps)。`.foldbadge`/`.node`/`.dirhead`/`.lit-out`(青)/`.lit-in`(橙)/エッジ・矢印クラス等。

## よくある改造

- **対象プロジェクトの asmdef 色/列順** → `asm.ts`。
- **対応言語の追加(cs/ts/tsx 以外)** → `prismSetup.ts` の `langForPath`(拡張子→文法)と文法ロード、`extract.py` の `parse_*`/`is_excluded`/依存解決を拡張。
- **コードビューの折りたたみ規則** → `rows.ts` ＋ `CodeView.tsx`。
- **Dep-Map のレイアウト/エッジ** → `DepMap.tsx`。

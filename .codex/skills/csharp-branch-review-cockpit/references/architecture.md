# テンプレ構成と改造ガイド

フロントは `public/data.json` を唯一の入力とするデータ駆動アプリ。改造時はまず該当ファイルを見る。

## データ層

- `scripts/extract.py` — `git diff base...branch` で変更 .cs を集め、各ファイルを解析して `data.json` を出力。
  - `parse_csharp()` — usings / 宣言型 / `#region` / メソッドの fold 範囲(brace マッチ＋多行 signature 対応)。
  - 依存 — コメント除去後に「他ファイルの宣言型名」を参照していれば edge。逆依存も算出。
  - `asmdef_of()` — 最寄りの `*.asmdef` 名。`sourceRoot` — 対象ファイルの共通ディレクトリ。
- `src/lib/types.ts` — `data.json` の型。`DataSet { branch, base, sourceRoot, files[], asmdefs[] }`、`FileRec { path,name,asmdef,status,add,del,text,parsed,addedLines,depsOut,depsIn }`。

## 状態・共通

- `src/lib/store.tsx` — Context。`mode`(cockpit/depmap)、`selected`、`expanded`(fold key 集合)、`reviewed`(localStorage)、`search`、各アクション。`jumpToFile` は選択＋cockpit 切替。
- `src/lib/rows.ts` — `buildRows(file, expanded)`。折りたたみ状態に応じた表示行を生成。collapsed メソッドは signature ＋ `{ Nline }` バッジ、Allman の独立 `{` 行は出さない。`methodKey`/`regionKey`。
- `src/lib/asm.ts` — asmdef → 色(`asmColor`)・列順(`asmLayer`)。**既知 asmdef はここに固定。未知はパレット/層 50 にフォールバック。列順・色を厳密にしたい asmdef はここに追記**。
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
- **C# 以外の言語** → `prismSetup.ts` の文法ロードと `extract.py` のパースを差し替え(L3 方向、要追加実装)。
- **コードビューの折りたたみ規則** → `rows.ts` ＋ `CodeView.tsx`。
- **Dep-Map のレイアウト/エッジ** → `DepMap.tsx`。

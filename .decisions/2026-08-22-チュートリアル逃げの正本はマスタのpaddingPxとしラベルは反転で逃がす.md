# チュートリアル逃げの正本はマスタのpaddingPxとし、ラベルは反転で逃がす

2026-08-22 ユーザー裁定（moores-code-review の設計判断 D1〜D4）。

## 決定

1. **ラベルが下に収まらない時は枠線の上へ反転する**（D1）。
   `HighlightLabel` が自分の高さを実測し、`box.bottom + 高さ > clip.bottom` かつ上に収まるなら `box.top - 高さ` へ置く。
2. **§8.10 の逃げ規則を、規則を書いた当のPRの中で全スクローラへ適用する**（D2）。
   `RecipeViewer.module.css .recipeListScroll` と `buildMenu/style.module.css .scroll` に
   `ItemListPanel` と同形（viewport `padding: var(--tutorial-anchor-clip-inset)` ＋ ScrollArea側の同量負マージン）を当てた。
3. **逃げ量の正本はマスタの `paddingPx`**（D3）。
   `--tutorial-anchor-padding` の CSS 値は初回描画までの想定値にすぎず、`TutorialOverlay` が
   presentation の `paddingPx` 最大値を実行時に書き戻す。e2e はリテラルでなく
   「逃げ ≧ `--tutorial-anchor-padding` + `--tutorial-highlight-glow`」の関係式で検査する。
4. **ツールチップの復帰トリガは現状維持**（D4）。スクロールで潜り込んだ別セルのツールチップが
   自動で開く挙動は許容する。

## 棄却した案

- D1: ラベル矩形を可視判定に足す案（下端付近でラベルが出なくなる）／ラベルにも clip-path を当てる案（文言が半端に切れる）
- D2: ADR へ「未対応」と明記して実装は動かさない案（規則と実装の乖離が残る）／ビルドメニューだけ直す案
- D3: `paddingPx` を wire スキーマから落として Web 所有へ引き上げる案（`moorestech_master`・クライアントへ波及する repo 跨ぎ変更のため見送り）
- D4: `enter` で座標を比較する案（判定が2箇所に増える）／復帰を実移動のみに限る案

## 理由

ラベルは clip-path を持たないため、可視判定を緩めた分だけ「枠は見えるがラベルは容器の外」という位置が
成立していた。反転は「常に見え、容器の外へも出ない」を同時に満たす唯一の案。
逃げ量は実行時に自動追随させることで、マスタが変わっても無言で枠が削られる経路を塞ぐ。

## リンク

- [[2026-08-17-アイテム一覧のスクロールバーは正本より視認性を優先する]]
- docs/adr/0024-tutorial-highlight-ancestor-clip-mask.md
- レビュー記録: `../moorestech_logs/harness/moores-code-review/runs/2026-08-22-1945/`

# レシピ行の骨格は共有 RecipeRow へ集約する

決定: `CraftRecipeEntry` / `MachineRecipeEntry` が丸ごと複製しているレシピ行骨格（3カラムgrid・`translateY(-1.565749px)`・`margin-left:-5.2px` 等の実測値ベースの幾何）を `views/RecipeRow.tsx` の表示専用コンポーネントへ集約する。CSSは既存の `RecipeBox.module.css` / `craftArrow.module.css` を流用する。

棄却案:
- 現状維持 — レビューでも判定が割れた（centralizationはCritical、precedent-alignmentは「改善提案の域」、ai-recurringは棄権）
- `CraftRecipeEntry` に吸収して1コンポーネント化 — ファイル数は減るが長押しクラフト挙動と機械情報行が同居し、200行制約と「共有部品にドメイン語彙を持ち込まない」方針に不利

理由: 単一リストで両エントリが上下に並ぶため、片方だけ幾何を触ると縦位置・列幅のズレが直接ユーザーの目に映る。壊れやすい実測値の出所を1箇所に保つ。

リンク: [[2026-08-18-参照ゼロになった資産3件は本ブランチで削除する]]

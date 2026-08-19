# レシピエントリの testId はレシピGUIDで一意化する

決定: `CraftRecipeEntry` / `MachineRecipeEntry` に `testId` propを足し、親の `RecipeContent` が `craft-recipe-entry-<recipeGuid>` 形式を注入する。e2eは前方一致セレクタで束ねる。

棄却案:
- 現状維持（e2e側で `.first()`/`.nth()`）— 変更量は最小だが「どのレシピの行か」をテストから指名できない
- `data-recipe-guid` 属性を別に足す — 既存の testId 契約の書き味から外れる

理由: ADR 0011 のゴールが「1エントリ1レシピを何件でも並べる」である以上、識別子がレシピ粒度で取れないのは目的と噛み合わない。同一アイテムに2件目のクラフトレシピが入った瞬間、Playwright strict mode 違反で6箇所のe2eが同時に落ちる。一意性は親の責務、という本planのチュートリアルアンカー注入と同型。

リンク: [[2026-08-18-レシピ行の骨格は共有RecipeRowへ集約する]]

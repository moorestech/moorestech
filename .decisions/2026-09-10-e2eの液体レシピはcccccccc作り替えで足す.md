# e2eの液体レシピは cccccccc の作り替えで足す（レシピ件数を増やさない）

決定: mock-host の電気機械レシピ `cccccccc`（アイテムのみ入出力）を液体のみ入出力レシピへ作り替え、
電気機械のレシピ件数は3件のまま据え置く。plan の「4件目 `ffffffff` を追加・行数アサート3→4」は撤回する。

理由: レシピ選択リストの視口は3.88行分しか無い（clientHeight 321px / 行高 74.7px / rowGap 8px）。
4件目を足すと既定シナリオが既に溢れ、既存の番人 `expectScrollsOnlyWhenOverflowing`
（`e2e/support/layoutAssertions.ts`）の前提「既定では溢れていない」が破れる。
`cccccccc` は fixture 以外から参照ゼロ（grep 確認済み）で、作り替えの副作用は
アイテム2種入力の行バリエーションが1件減ることだけ。

棄却案:
- 視口高/行密度予算を広げて4行入れる — webui-design §8.10 のレイアウト予算に触れる別裁定が必要で、本 plan の範囲を超える
- 液体レシピを別 topic シナリオへ分離 — 既存 fixture は壊さないが e2e 側の準備コードが増える

リンク: `docs/adr/0054-recipe-row-fluid-slot-matches-item-slot.md` /
`docs/superpowers/plans/2026-09-10-machine-recipe-fluid-amount-slot.md` Task 4

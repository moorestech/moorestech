# レシピ行は独自gridを維持し SlotGrid の例外とする

2026-08-20 ユーザー裁定（moores-code-review の設計判断5）。

## 決定
`RecipeRow` の素材・結果スロットは共有基盤 `SlotGrid` に戻さず独自gridのままとし、
webui-design SKILL.md §4「独自の grid CSS でスロットを並べない」に例外を1行明記して文言矛盾を解消する。

## 棄却案
`SlotGrid` に `className` prop を追加して `container-type` と `--recipe-slot-*` を渡し、`RecipeRow` をそちらへ戻す案。

## 理由
`SlotGrid` の既定 `gridTemplateColumns: repeat(cols, var(--slot-size, 2rem))` は、`RecipeRow` が実測で踏んだ罠
（`--slot-size` を要素自身の `grid-template-columns` で使うと `cqw` が祖先コンテナへ解決して失敗し、スロットが縮まず溢れる）と同型。
戻しても必ず style 上書きが要るため、共有基盤に prop を1つ増やす対価に見合わない。

- [[2026-08-20-レシピの素材と結果は3点以上で2行へ折り返す]]

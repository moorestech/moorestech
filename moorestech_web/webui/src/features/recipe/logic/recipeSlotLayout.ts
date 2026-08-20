import type { CSSProperties } from "react";

// 折り返し後の列数は2で固定し、点数が増えたぶんは行が増える（3点=横2縦2、6点=横2縦3。ユーザー裁定 2026-08-20）。
// 列を増やす向きに折り返すとスロットが縮んで個数テキストが読めなくなる（実測6.2px）
// Wrapping keeps two columns and grows rows instead (3 items = 2x2, 6 items = 2x3; user ruling 2026-08-20).
// Wrapping the other way adds columns, which shrinks slots until the count text is unreadable (measured 6.2px)
export const MAX_SLOT_COLUMNS = 2;

// 点数から列数と、列幅に実際に収まるスロット寸法を引く。
// 基準幅はcqw（列そのものの幅）で取る。%はスロットの親が内容依存幅のため循環し、実測で0.8pxまで潰れた
// Derive the column count and the slot size that actually fits the column from the item count.
// The basis is cqw (the column's own width); % is circular because the slot's parent is content-sized, which
// measured out at 0.8px
export function recipeSlotLayout(count: number): CSSProperties {
  // 0点でも repeat(0, …) とゼロ除算calcを出さないよう1列を床にする
  // Floor at one column so a zero count cannot emit repeat(0, …) or a divide-by-zero calc
  const columns = Math.max(1, Math.min(count, MAX_SLOT_COLUMNS));
  // 列幅はautoでスロット実寸に追従させる。--slot-sizeをこの要素自身のgrid-template-columnsで使うと
  // cqwが祖先のコンテナを見にいって解決に失敗し、実測でスロットが縮まず溢れた
  // Columns are auto so they follow the slot's real size; using --slot-size in this element's own
  // grid-template-columns makes cqw resolve against an ancestor container instead, which measured out as
  // slots stuck at their cap and overflowing
  return {
    gridTemplateColumns: `repeat(${columns}, auto)`,
    "--slot-size": `min(var(--recipe-slot-size-max), calc((100cqw - ${columns - 1} * var(--recipe-slot-gap)) / ${columns}))`,
  } as CSSProperties;
}

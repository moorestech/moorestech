import type { CSSProperties, ReactNode } from "react";
import { Box } from "@mantine/core";
import { ProgressArrowGlyph } from "@/shared/ui";
import styles from "./RecipeBox.module.css";

// 3点以上は2行へ折り返す。1行のまま縮めると個数テキストが読めない寸法(実測6.2px)まで落ちるため
// Three or more wrap to two rows; keeping one row shrinks the count text to an unreadable size (measured 6.2px)
const WRAP_THRESHOLD = 3;
// 列が3本以上になって初めてスロットが上限を割り、個数テキストの連動縮小が要る
// Only at three or more columns does the slot drop below its cap, requiring the linked count-text shrink
const DENSE_COLUMN_COUNT = 3;

type Props = {
  testId: string;
  // 素材・結果はスロット単位の配列で受ける。点数が寸法算出の入力になるため単一ノードでは足りない
  // Materials and results arrive per slot; the count feeds the sizing, so a single node is not enough
  materials: ReactNode[];
  // 進捗が無ければnullで静止矢印
  // Pass null when there is no progress; the arrow renders static
  arrowValue: number | null;
  arrowTestId: string;
  // 矢印の真上に置く所要秒数
  // Duration text sitting directly above the arrow
  duration: ReactNode;
  // 矢印の真下に置く操作（クラフトボタン／機械表示）
  // The action placed directly below the arrow (craft button / machine display)
  action: ReactNode;
  result: ReactNode[];
};

// 点数から行数・列数と、列幅に実際に収まるスロット寸法を引く。
// 基準幅はcqw（列そのものの幅）で取る。%はスロットの親が内容依存幅のため循環し、実測で0.8pxまで潰れた
// Derive the row/column counts and the slot size that actually fits the column from the item count.
// The basis is cqw (the column's own width); % is circular because the slot's parent is content-sized, which
// measured out at 0.8px
function slotLayout(count: number) {
  const rows = count >= WRAP_THRESHOLD ? 2 : 1;
  const columns = Math.ceil(count / rows);
  // 列幅はautoでスロット実寸に追従させる。--slot-sizeをこの要素自身のgrid-template-columnsで使うと
  // cqwが祖先のコンテナを見にいって解決に失敗し、実測でスロットが縮まず36.8pxのまま溢れた
  // Columns are auto so they follow the slot's real size; using --slot-size in this element's own
  // grid-template-columns makes cqw resolve against an ancestor container instead, which measured out as
  // slots stuck at 36.8px and overflowing
  const style = {
    gridTemplateColumns: `repeat(${columns}, auto)`,
    "--slot-size": `min(var(--recipe-slot-size-max), calc((100cqw - ${columns - 1} * var(--recipe-slot-gap)) / ${columns}))`,
  } as CSSProperties;
  return { style, dense: columns >= DENSE_COLUMN_COUNT || undefined };
}

// 共通レシピ行骨格。幾何値をここに集約
// Shared recipe-row frame; keeps measured geometry in one place
export default function RecipeRow({ testId, materials, arrowValue, arrowTestId, duration, action, result }: Props) {
  const materialLayout = slotLayout(materials.length);
  const resultLayout = slotLayout(result.length);

  return (
    // 素材点数で矢印列がズレるためgridで3カラムの列位置を固定する
    // A grid pins the 3 columns; space-between let the arrow drift with material count
    <div className={styles.recipeBox} data-testid={testId}>
      {/* 3点以上は2行へ折り返し、収まらない分だけスロットを縮めて中心の矢印・ボタンへ食い込ませない */}
      {/* Three or more wrap to two rows; slots shrink only as far as needed so they never reach the centered arrow and button */}
      <div className={styles.recipeMaterials} style={materialLayout.style} data-dense={materialLayout.dense}>{materials}</div>
      {/* 中央列は秒数→矢印→操作の縦積み。旧機械レシピUIと同じ並び */}
      {/* The center column stacks duration, arrow, then action, matching the old machine-recipe UI */}
      <Box className={styles.recipeArrowCol}>
        <div className={styles.recipeDuration} data-testid={`${testId}-duration`}>{duration}</div>
        <ProgressArrowGlyph value={arrowValue} testId={arrowTestId} />
        <div className={styles.recipeActionSlot}>{action}</div>
      </Box>
      {/* 出力も素材と同じ折り返し規則で並べる（ユーザー裁定 2026-08-20） */}
      {/* Results follow the same wrapping rule as materials (user ruling 2026-08-20) */}
      <div className={styles.recipeResult} style={resultLayout.style} data-dense={resultLayout.dense}>{result}</div>
    </div>
  );
}

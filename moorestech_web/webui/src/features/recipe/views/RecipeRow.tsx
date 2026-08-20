import type { CSSProperties, ReactNode } from "react";
import { Box, Group } from "@mantine/core";
import { ProgressArrowGlyph } from "@/shared/ui";
import styles from "./RecipeBox.module.css";

// 個数テキストが縮んだスロットからはみ出し始める点数
// The count where the count text starts overflowing a shrunken slot
const DENSE_SLOT_COUNT = 3;

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

// 枠に実際に収まる寸法を点数から引く。上限を超えない範囲で目一杯使うため、点数が増えるほど自動で縮む。
// 基準幅はcqw（列そのものの幅）で取る。%はスロットの親が内容依存幅のため循環し、実測で0.8pxまで潰れた
// Derive the size that actually fits from the count; it fills up to the cap, so more slots shrink automatically.
// The basis is cqw (the column's own width); % is circular because the slot's parent is content-sized, which
// measured out at 0.8px
function slotSizing(count: number): CSSProperties {
  return { "--slot-size": `min(var(--recipe-slot-size-max), calc((100cqw - ${count - 1} * var(--recipe-slot-gap)) / ${count}))` } as CSSProperties;
}

// 共通レシピ行骨格。幾何値をここに集約
// Shared recipe-row frame; keeps measured geometry in one place
export default function RecipeRow({ testId, materials, arrowValue, arrowTestId, duration, action, result }: Props) {
  return (
    // 素材点数で矢印列がズレるためgridで3カラムの列位置を固定する
    // A grid pins the 3 columns; space-between let the arrow drift with material count
    <div className={styles.recipeBox} data-testid={testId}>
      {/* 点数が増えるほどスロットを縮め、中心の矢印・ボタンへ食い込ませない */}
      {/* Shrink the slots as the count grows so they never reach the centered arrow and button */}
      <Group gap={0} wrap="nowrap" className={styles.recipeMaterials} style={slotSizing(materials.length)} data-dense={materials.length >= DENSE_SLOT_COUNT || undefined}>{materials}</Group>
      {/* 中央列は秒数→矢印→操作の縦積み。旧機械レシピUIと同じ並び */}
      {/* The center column stacks duration, arrow, then action, matching the old machine-recipe UI */}
      <Box className={styles.recipeArrowCol}>
        <div className={styles.recipeDuration} data-testid={`${testId}-duration`}>{duration}</div>
        <ProgressArrowGlyph value={arrowValue} testId={arrowTestId} />
        <div className={styles.recipeActionSlot}>{action}</div>
      </Box>
      {/* 出力は複数になりうるため横並びのflexで受ける（縦積みは固定高の枠を突き破る） */}
      {/* Results can be plural, so lay them out horizontally; stacking would burst the fixed-height frame */}
      <Group gap={0} wrap="nowrap" className={styles.recipeResult} style={slotSizing(result.length)} data-dense={result.length >= DENSE_SLOT_COUNT || undefined}>{result}</Group>
    </div>
  );
}

import type { ReactNode } from "react";
import { Box, Group } from "@mantine/core";
import { ProgressArrowGlyph } from "@/shared/ui";
import styles from "./RecipeBox.module.css";

type Props = {
  testId: string;
  materials: ReactNode;
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
  result: ReactNode;
};

// 共通レシピ行骨格。幾何値をここに集約
// Shared recipe-row frame; keeps measured geometry in one place
export default function RecipeRow({ testId, materials, arrowValue, arrowTestId, duration, action, result }: Props) {
  return (
    // 素材点数で矢印列がズレるためgridで3カラムの列位置を固定する
    // A grid pins the 3 columns; space-between let the arrow drift with material count
    <div className={styles.recipeBox} data-testid={testId}>
      <Group gap="var(--recipe-slot-gap)" wrap="nowrap" className={styles.recipeMaterials}>{materials}</Group>
      {/* 中央列は秒数→矢印→操作の縦積み。旧機械レシピUIと同じ並び */}
      {/* The center column stacks duration, arrow, then action, matching the old machine-recipe UI */}
      <Box className={styles.recipeArrowCol}>
        <div className={styles.recipeDuration} data-testid={`${testId}-duration`}>{duration}</div>
        <ProgressArrowGlyph value={arrowValue} testId={arrowTestId} />
        <div className={styles.recipeActionSlot}>{action}</div>
      </Box>
      {/* 出力は複数になりうるため横並びのflexで受ける（縦積みは固定高の枠を突き破る） */}
      {/* Results can be plural, so lay them out horizontally; stacking would burst the fixed-height frame */}
      <Group gap="var(--recipe-slot-gap)" wrap="nowrap" className={styles.recipeResult}>{result}</Group>
    </div>
  );
}

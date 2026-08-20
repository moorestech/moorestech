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
  result: ReactNode;
};

// 共通レシピ行骨格。幾何値をここに集約
// Shared recipe-row frame; keeps measured geometry in one place
export default function RecipeRow({ testId, materials, arrowValue, arrowTestId, result }: Props) {
  return (
    // 素材点数で矢印列がズレるためgridで3カラムの列位置を固定する
    // A grid pins the 3 columns; space-between let the arrow drift with material count
    <div className={styles.recipeBox} data-testid={testId}>
      <Group gap={0} className={styles.recipeMaterials}>{materials}</Group>
      {/* 素材と完成品の間に進捗矢印を置く */}
      {/* Place the progress arrow between materials and result */}
      <Box className={styles.recipeArrowCol}>
        <ProgressArrowGlyph value={arrowValue} testId={arrowTestId} />
      </Box>
      {/* 出力は複数になりうるため横並びのflexで受ける（縦積みは固定高の枠を突き破る） */}
      {/* Results can be plural, so lay them out horizontally; stacking would burst the fixed-height frame */}
      <Group gap={0} wrap="nowrap" className={styles.recipeResult}>{result}</Group>
    </div>
  );
}

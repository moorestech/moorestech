import type { ReactNode } from "react";
import { Box } from "@mantine/core";
import { ProgressArrowGlyph } from "@/shared/ui";
import { recipeSlotLayout } from "../logic/recipeSlotLayout";
import styles from "./RecipeBox.module.css";

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
  // 矢印下の操作（クラフト/機械表示）
  // The action placed below the arrow (craft button / machine display)
  action: ReactNode;
  result: ReactNode[];
};

// 共通レシピ行骨格。幾何値をここに集約
// Shared recipe-row frame; keeps measured geometry in one place
export default function RecipeRow({ testId, materials, arrowValue, arrowTestId, duration, action, result }: Props) {
  const materialStyle = recipeSlotLayout(materials.length);
  const resultStyle = recipeSlotLayout(result.length);

  return (
    // 素材点数で矢印列がズレるためgridで3カラムの列位置を固定する
    // A grid pins the 3 columns; space-between let the arrow drift with material count
    <div className={styles.recipeBox} data-testid={testId}>
      {/* 3点以上は2列のまま行を増やす。列が増えないためスロットは常にフルサイズを保てる */}
      {/* Three or more keep two columns and add rows; with the column count fixed the slots stay full size */}
      <div className={styles.recipeMaterials} style={materialStyle}>{materials}</div>
      {/* 中央列は秒数→矢印→操作の縦積み。旧機械レシピUIと同じ並び */}
      {/* The center column stacks duration, arrow, then action, matching the old machine-recipe UI */}
      <Box className={styles.recipeArrowCol}>
        <div className={styles.recipeDuration} data-testid={`${testId}-duration`}>{duration}</div>
        <ProgressArrowGlyph value={arrowValue} testId={arrowTestId} />
        <div className={styles.recipeActionSlot}>{action}</div>
      </Box>
      {/* 出力も素材と同じ折り返し規則で並べる（ユーザー裁定 2026-08-20） */}
      {/* Results follow the same wrapping rule as materials (user ruling 2026-08-20) */}
      <div className={styles.recipeResult} style={resultStyle}>{result}</div>
    </div>
  );
}

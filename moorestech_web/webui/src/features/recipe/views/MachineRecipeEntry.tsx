import { Box, Stack, Text } from "@mantine/core";
import { ItemSlot, BlockIcon, FluidAmountSlot } from "@/shared/ui";
import type { MachineRecipe } from "@/bridge";
import RecipeRow from "./RecipeRow";
import LevelInheritanceBadge from "./LevelInheritanceBadge";
import styles from "./RecipeBox.module.css";
import { blockNameKey, L, useI18n } from "@/shared/i18n";

type Props = {
  recipe: MachineRecipe;
  onSelect: (itemId: number) => void;
  // testIdはレシピ単位で親が注入
  // Parent injects a per-recipe testId
  testId: string;
};

// 機械エントリ: 矢印はnullで静止
// 矢印の上に秒数、下にブロックアイコンとブロック名
// Machine entry: arrow stays static via null
// Duration above the arrow, block icon and name below it
export default function MachineRecipeEntry({ recipe, onSelect, testId }: Props) {
  const { t } = useI18n();
  const localizedBlockName = t(blockNameKey(recipe.blockGuid));

  return (
    <Box className={styles.recipeEntry} data-testid={testId}>
      <RecipeRow
        testId={`machine-recipe-box-${recipe.recipeGuid}`}
        arrowValue={null}
        arrowTestId={`machine-progress-arrow-${recipe.recipeGuid}`}
        duration={t(L.ui.recipe.duration, { seconds: recipe.time })}
        materials={[
          // 機械レシピは手クラフトしないため必要数のみ表示する（所持数チェックなし）
          // Machine recipes are not hand-crafted, so show required counts only (no owned-count check)
          ...recipe.inputItems.map((r, i) => (
            <ItemSlot key={`item-${i}`} itemId={r.itemId} count={r.count} onLeftDown={() => onSelect(r.itemId)} />
          )),
          // 液体はレシピ一覧の遷移先を持たないため量の表示だけ（前例 MachineRecipeSelectionRow）
          // Fluids have no recipe-list destination, so they only show the amount (precedent: MachineRecipeSelectionRow)
          ...recipe.inputFluids.map((fluid, i) => (
            <FluidAmountSlot key={`fluid-${i}`} fluidGuid={fluid.fluidGuid} badge={{ kind: "show", amount: fluid.amount }} testId={`${testId}-input-fluid-${i}`} />
          )),
        ]}
        actionMode="slot"
        action={(
          <Stack className={styles.machineInfo} gap={2} align="center">
            <BlockIcon blockId={recipe.blockId} alt={localizedBlockName} fallback={{ kind: "idText" }} className={styles.machineInfoIcon} />
            <Text className={styles.machineInfoText}>{localizedBlockName}</Text>
            <LevelInheritanceBadge inheritance={recipe.levelInheritance} testId={`machine-recipe-level-inheritance-${recipe.recipeGuid}`} />
          </Stack>
        )}
        result={[
          ...recipe.outputItems.map((r, i) => (
            <ItemSlot key={`item-${i}`} itemId={r.itemId} count={r.count} onLeftDown={() => onSelect(r.itemId)} />
          )),
          ...recipe.outputFluids.map((fluid, i) => (
            <FluidAmountSlot key={`fluid-${i}`} fluidGuid={fluid.fluidGuid} badge={{ kind: "show", amount: fluid.amount }} testId={`${testId}-output-fluid-${i}`} />
          )),
        ]}
      />
    </Box>
  );
}

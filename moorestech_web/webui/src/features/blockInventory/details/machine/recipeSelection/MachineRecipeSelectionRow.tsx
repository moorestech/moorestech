// レシピ選択行: 上辺にレシピ名、骨格は共有RecipeRow（中央列は秒数＋静止矢印のみ）
// Recipe selection row: recipe name on top, shared RecipeRow skeleton (center column = duration + static arrow only)
import { Box, Text } from "@mantine/core";
import { FluidIcon, ItemSlot } from "@/shared/ui";
import { L, fluidNameKey, useI18n, useItemNameResolver } from "@/shared/i18n";
import RecipeRow from "@/features/recipe/views/RecipeRow";
import type { MachineRecipeSelectionRowData } from "../machineRecipeSelectionLogic";
import styles from "./machineRecipeSelectionList.module.css";

type Props = { row: MachineRecipeSelectionRowData; onSelect: (recipeGuid: string) => void };

export default function MachineRecipeSelectionRow({ row, onSelect }: Props) {
  const { t } = useI18n();
  const resolveItemName = useItemNameResolver();
  const { recipe, subject } = row;
  // レシピ名は代表出力（アイテム優先、無ければ液体）の名前（D2）
  // The recipe name is the representative output's name (item first, fluid otherwise) (D2)
  const name = subject.kind === "item"
    ? (resolveItemName(subject.itemId) ?? t(L.ui.common.itemFallback, { itemId: subject.itemId }))
    : t(fluidNameKey(subject.fluidGuid));

  return (
    <Box
      className={styles.row}
      data-testid={`machine-recipe-${recipe.recipeGuid}`}
      data-selected={row.selected ? "true" : undefined}
      role="button"
      onClick={() => onSelect(recipe.recipeGuid)}
    >
      <Text className={styles.name} data-testid={`machine-recipe-${recipe.recipeGuid}-name`}>{name}</Text>
      <RecipeRow
        testId={`machine-recipe-${recipe.recipeGuid}-row`}
        arrowValue={null}
        arrowTestId={`machine-recipe-${recipe.recipeGuid}-arrow`}
        duration={t(L.ui.blockInventory.recipeDuration, { seconds: recipe.time })}
        materials={[
          ...recipe.inputItems.map((item, i) => <ItemSlot key={`item-${i}`} itemId={item.itemId} count={item.count} />),
          ...recipe.inputFluids.map((fluid, i) => <FluidIcon key={`fluid-${i}`} fluidGuid={fluid.fluidGuid} />),
        ]}
        action={null}
        result={[
          ...recipe.outputItems.map((item, i) => <ItemSlot key={`item-${i}`} itemId={item.itemId} count={item.count} />),
          ...recipe.outputFluids.map((fluid, i) => <FluidIcon key={`fluid-${i}`} fluidGuid={fluid.fluidGuid} />),
        ]}
      />
    </Box>
  );
}

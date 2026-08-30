// レシピ選択行: 上辺にレシピ名、骨格は共有RecipeRow（中央列は秒数＋静止矢印のみ）
// Recipe selection row: recipe name on top, shared RecipeRow skeleton (center column = duration + static arrow only)
import { Box, Text } from "@mantine/core";
import { ItemSlot } from "@/shared/ui";
import { L, useI18n, useItemNameResolver } from "@/shared/i18n";
import RecipeRow from "@/features/recipe/views/RecipeRow";
import type { MachineRecipeSelectionRowData } from "../machineRecipeSelectionLogic";
import styles from "./machineRecipeSelectionList.module.css";

type Props = { row: MachineRecipeSelectionRowData; onSelect: (recipeGuid: string) => void };

export default function MachineRecipeSelectionRow({ row, onSelect }: Props) {
  const { t } = useI18n();
  const resolveItemName = useItemNameResolver();
  const { recipe } = row;
  // レシピ名は代表出力（先頭の生産物）のアイテム名
  // The recipe name is the representative output's (first product's) item name
  const name = recipe.outputItems.length > 0 ? resolveItemName(recipe.outputItems[0].itemId) : "";

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
        materials={recipe.inputItems.map((item, i) => <ItemSlot key={i} itemId={item.itemId} count={item.count} />)}
        action={null}
        result={recipe.outputItems.map((item, i) => <ItemSlot key={i} itemId={item.itemId} count={item.count} />)}
      />
    </Box>
  );
}

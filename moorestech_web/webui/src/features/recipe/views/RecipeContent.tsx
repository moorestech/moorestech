import { useMemo } from "react";
import { ScrollArea, Stack, Text } from "@mantine/core";
import { buildOwnedCounts } from "@/shared/ownedCounts";
import styles from "../panels/RecipeViewer.module.css";
import type { CraftRecipesData, MachineRecipesData, PlayerInventoryData } from "@/bridge";
import { buildRecipeEntries } from "../logic/craftLogic";
import ItemHeader from "./ItemHeader";
import CraftRecipeEntry from "./CraftRecipeEntry";
import MachineRecipeEntry from "./MachineRecipeEntry";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import { L, useI18n, useItemNameResolver } from "@/shared/i18n";

type Props = {
  itemId: number;
  recipes: CraftRecipesData;
  machineRecipes: MachineRecipesData;
  inventory: PlayerInventoryData;
  onSelect: (itemId: number) => void;
};

// 選択アイテムのレシピ本体。全レシピをクラフト優先の単一リストで縦に並べる（ADR 0011）
// Recipe body for the selected item; every recipe stacks in one craft-first list (ADR 0011)
export default function RecipeContent({ itemId, recipes, machineRecipes, inventory, onSelect }: Props) {
  const { t } = useI18n();
  const resolveItemName = useItemNameResolver();
  // 導出は純関数＋useMemo。入力 topic が変わらない限り再計算しない
  // Derivations are pure functions + useMemo; no recompute unless the input topics change
  const entries = useMemo(() => buildRecipeEntries(recipes, machineRecipes, itemId), [recipes, machineRecipes, itemId]);
  // grabは所持数に含めない
  // The server's OneClickCraft only consults the main inventory, so grab is excluded from the tally
  const counts = useMemo(() => buildOwnedCounts(inventory.mainSlots), [inventory]);

  const itemName = resolveItemName(itemId) ?? t(L.ui.common.itemFallback, { itemId });
  // チュートリアルアンカーは重複禁止。並び順ではなくkind判別子から対象クラフトを特定する
  // The tutorial anchor must stay unique, so pick its craft entry by kind, never by list position
  const anchoredCraftGuid = entries.find((entry) => entry.kind === "craft")?.recipe.recipeGuid;

  if (entries.length === 0) {
    return (
      <Stack gap="sm">
        <ItemHeader name={itemName} />
        <Text size="sm" c="dimmed">{t(L.ui.recipe.noRecipes)}</Text>
      </Stack>
    );
  }

  return (
    <Stack className={styles.recipeContent} gap="sm">
      <ItemHeader name={itemName} />
      <ScrollArea.Autosize mah="var(--recipe-list-max-height)" type="auto" scrollbarSize="var(--recipe-list-scrollbar-reserve)" className={styles.recipeListScroll}>
        <Stack className={styles.recipeList} gap="var(--recipe-entry-gap)" data-testid="recipe-entry-list">
          {entries.map((entry) =>
            entry.kind === "craft" ? (
              <CraftRecipeEntry
                key={entry.recipe.recipeGuid}
                recipe={entry.recipe}
                counts={counts}
                onSelect={onSelect}
                testId={`craft-recipe-entry-${entry.recipe.recipeGuid}`}
                tutorialAnchorProps={entry.recipe.recipeGuid === anchoredCraftGuid ? tutorialAnchor(TutorialAnchorIds.recipeCraftButton) : undefined}
              />
            ) : (
              <MachineRecipeEntry
                key={entry.recipe.recipeGuid}
                recipe={entry.recipe}
                onSelect={onSelect}
                testId={`machine-recipe-entry-${entry.recipe.recipeGuid}`}
              />
            ),
          )}
        </Stack>
      </ScrollArea.Autosize>
    </Stack>
  );
}

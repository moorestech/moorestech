import { useMemo } from "react";
import { Stack, Text } from "@mantine/core";
import { buildOwnedCounts } from "@/shared/ownedCounts";
import { RecipeListScrollArea } from "@/shared/ui";
import styles from "../panels/RecipeViewer.module.css";
import type { CraftRecipesData, MachineRecipesData, PlayerInventoryData } from "@/bridge";
import { buildRecipeEntries } from "../logic/craftLogic";
import ItemHeader from "./ItemHeader";
import CraftRecipeEntry from "./CraftRecipeEntry";
import MachineRecipeEntry from "./MachineRecipeEntry";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import { L, useI18n, useItemDisplayName } from "@/shared/i18n";

type Props = {
  itemId: number;
  recipes: CraftRecipesData;
  machineRecipes: MachineRecipesData;
  inventory: PlayerInventoryData;
  onSelect: (itemId: number) => void;
};

// 全レシピをクラフト優先の単一リストで表示
// Shows every recipe in one craft-first list
export default function RecipeContent({ itemId, recipes, machineRecipes, inventory, onSelect }: Props) {
  const { t } = useI18n();
  const itemDisplayName = useItemDisplayName();
  // 導出は純関数＋useMemo。入力 topic が変わらない限り再計算しない
  // Derivations are pure functions + useMemo; no recompute unless the input topics change
  const entries = useMemo(() => buildRecipeEntries(recipes, machineRecipes, itemId), [recipes, machineRecipes, itemId]);
  // grabは所持数に含めない
  // The server's OneClickCraft only consults the main inventory, so grab is excluded from the tally
  const counts = useMemo(() => buildOwnedCounts(inventory.mainSlots), [inventory]);

  const itemName = itemDisplayName(itemId);
  // buildRecipeEntriesがクラフト優先で並べるため、先頭がクラフトならそれが代表
  // buildRecipeEntries sorts craft first, so the head entry is the representative craft when it is one
  const headEntry = entries[0];
  const anchoredCraftGuid = headEntry?.kind === "craft" ? headEntry.recipe.recipeGuid : undefined;

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
      <RecipeListScrollArea scrollClassName={styles.recipeListScroll} listClassName={styles.recipeList} listGap="var(--recipe-entry-gap)" listTestId="recipe-entry-list">
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
      </RecipeListScrollArea>
    </Stack>
  );
}

// 開いている機械に対応するレシピを選択行の表示データへ変換する
// Converts recipes for the open machine into selection-row display data
import type { MachineRecipe } from "@/bridge";

const emptyGuid = "00000000-0000-0000-0000-000000000000";

export type MachineRecipeSelectionRowData = {
  recipeGuid: string;
  iconItemId: number;
  iconCount: number;
  selected: boolean;
};

export function buildMachineRecipeSelectionRows(
  recipes: readonly MachineRecipe[],
  blockGuid: string,
  selectedRecipeGuid: string | null | undefined,
): MachineRecipeSelectionRowData[] {
  const hasSelection = !isEmptyGuid(selectedRecipeGuid);

  // blockGuid一致と代表アイコンの存在を同時に保証し、空スロットを作らない
  // Require both a matching blockGuid and a representative icon so no empty slot is created
  return recipes.flatMap((recipe) => {
    if (recipe.blockGuid !== blockGuid) return [];
    const icon = recipe.outputItems[0] ?? recipe.inputItems[0];
    if (icon === undefined) return [];
    return [{
      recipeGuid: recipe.recipeGuid,
      iconItemId: icon.itemId,
      iconCount: icon.count,
      selected: hasSelection && recipe.recipeGuid === selectedRecipeGuid,
    }];
  });
}

function isEmptyGuid(guid: string | null | undefined): boolean {
  return !guid || guid === emptyGuid;
}

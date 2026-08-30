// 開いている機械に対応するレシピを選択行データへ変換する
// Converts the open machine's recipes into selection-row data
import type { MachineRecipe } from "@/bridge";

const emptyGuid = "00000000-0000-0000-0000-000000000000";

export type MachineRecipeSelectionRowData = { recipe: MachineRecipe; selected: boolean };

export function buildMachineRecipeSelectionRows(
  recipes: readonly MachineRecipe[],
  blockGuid: string,
  selectedRecipeGuid: string,
): MachineRecipeSelectionRowData[] {
  const hasSelection = hasSelectedRecipe(selectedRecipeGuid);
  // blockGuid一致と代表出力（行名・ヘッダ両方の出所）の存在を同時に保証する
  // Require both a matching blockGuid and a representative output, which both the row name and header rely on
  return recipes
    .filter((recipe) => recipe.blockGuid === blockGuid && recipe.outputItems.length > 0)
    .map((recipe) => ({ recipe, selected: hasSelection && recipe.recipeGuid === selectedRecipeGuid }));
}

export function hasSelectedRecipe(selectedRecipeGuid: string): boolean {
  return selectedRecipeGuid !== emptyGuid;
}

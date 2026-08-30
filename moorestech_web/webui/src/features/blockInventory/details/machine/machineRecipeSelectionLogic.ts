// 開いている機械に対応するレシピを選択行データへ変換する
// Converts the open machine's recipes into selection-row data
import type { MachineRecipe } from "@/bridge";

const emptyGuid = "00000000-0000-0000-0000-000000000000";

// レシピの代表表示（行名・ヘッダアイコンの出所）。アイテム優先・無ければ液体（2026-08-30裁定D2、ADR 0042 R2/R3）
// The recipe's representative display (source of the row name / header icon). Item first, fluid otherwise (2026-08-30 ruling D2, ADR 0042 R2/R3)
export type RecipeDisplaySubject = { kind: "item"; itemId: number; count: number } | { kind: "fluid"; fluidGuid: string; amount: number };

export type MachineRecipeSelectionRowData = { recipe: MachineRecipe; subject: RecipeDisplaySubject; selected: boolean };

// 代表出力を解決する唯一の場所。出力アイテムの先頭優先、無ければ出力液体の先頭。両方無ければ表示不能
// The single place that resolves the representative output: first output item, else first output fluid; undefined when neither exists
export function resolveRecipeDisplaySubject(recipe: MachineRecipe): RecipeDisplaySubject | undefined {
  if (recipe.outputItems.length > 0) return { kind: "item", itemId: recipe.outputItems[0].itemId, count: recipe.outputItems[0].count };
  if (recipe.outputFluids.length > 0) return { kind: "fluid", fluidGuid: recipe.outputFluids[0].fluidGuid, amount: recipe.outputFluids[0].amount };
  return undefined;
}

export function buildMachineRecipeSelectionRows(
  recipes: readonly MachineRecipe[],
  blockGuid: string,
  selectedRecipeGuid: string,
): MachineRecipeSelectionRowData[] {
  const hasSelection = hasSelectedRecipe(selectedRecipeGuid);
  // blockGuid一致のレシピのうち、代表出力を持つものだけを行にする（液体のみ出力のボイラー等も含む）
  // Rows are recipes matching blockGuid that have a representative output (includes fluid-only outputs like boilers)
  const rows: MachineRecipeSelectionRowData[] = [];
  for (const recipe of recipes) {
    if (recipe.blockGuid !== blockGuid) continue;
    const subject = resolveRecipeDisplaySubject(recipe);
    if (subject === undefined) continue;
    rows.push({ recipe, subject, selected: hasSelection && recipe.recipeGuid === selectedRecipeGuid });
  }
  return rows;
}

export function hasSelectedRecipe(selectedRecipeGuid: string): boolean {
  return selectedRecipeGuid !== emptyGuid;
}

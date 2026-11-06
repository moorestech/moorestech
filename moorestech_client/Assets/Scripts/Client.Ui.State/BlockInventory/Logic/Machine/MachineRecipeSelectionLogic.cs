using System;
using System.Collections.Generic;

namespace Client.Ui.State.BlockInventory.Logic.Machine
{
    /// <summary>
    /// 開いている機械に対応するレシピを選択行へ変える（Web machineRecipeSelectionLogic.ts の写し。名前の解決は画面側）
    /// Turns the open machine's recipes into selection rows (port of Web machineRecipeSelectionLogic.ts; name resolution stays in the screen)
    /// </summary>
    public static class MachineRecipeSelectionLogic
    {
        public static List<MachineRecipeSelectionRow> BuildRows(IReadOnlyList<MachineRecipeSummary> recipes, Guid blockGuid, Guid selectedRecipeGuid)
        {
            var hasSelection = HasSelectedRecipe(selectedRecipeGuid);
            var rows = new List<MachineRecipeSelectionRow>();
            foreach (var recipe in recipes)
            {
                // 開いている機械のレシピで、代表出力を持つものだけを行にする（液体だけを出すボイラー等も含む）
                // Rows are the open machine's recipes that have a representative output (fluid-only boilers included)
                if (recipe.BlockGuid != blockGuid) continue;
                if (!TryResolveDisplaySubject(recipe, out var subject)) continue;
                rows.Add(new MachineRecipeSelectionRow(recipe, subject, hasSelection && recipe.RecipeGuid == selectedRecipeGuid));
            }
            return rows;

            #region Internal

            // 代表出力を決める唯一の場所。出力アイテムの先頭を優先し、無ければ出力液体の先頭。両方無ければ行にできない
            // The single place resolving the representative output: first item, else first fluid; neither means no row
            bool TryResolveDisplaySubject(MachineRecipeSummary recipe, out RecipeDisplaySubject subject)
            {
                if (0 < recipe.OutputItems.Count)
                {
                    subject = new ItemRecipeDisplaySubject(recipe.OutputItems[0].ItemId, recipe.OutputItems[0].Count);
                    return true;
                }
                if (0 < recipe.OutputFluids.Count)
                {
                    subject = new FluidRecipeDisplaySubject(recipe.OutputFluids[0].FluidId, recipe.OutputFluids[0].Amount);
                    return true;
                }
                subject = null;
                return false;
            }

            #endregion
        }

        public static bool HasSelectedRecipe(Guid selectedRecipeGuid)
        {
            return selectedRecipeGuid != Guid.Empty;
        }

        // 行名の「他 N 件」の N。生産物が複数あるレシピはアイコンと食い違わないよう残りの数を出す（0 以下なら出さない）
        // The N in the row name's "and N others"; multi-product recipes state the remainder so names match icons (hidden at 0 or less)
        public static int OtherOutputCount(MachineRecipeSummary recipe)
        {
            return recipe.OutputItems.Count + recipe.OutputFluids.Count - 1;
        }
    }
}

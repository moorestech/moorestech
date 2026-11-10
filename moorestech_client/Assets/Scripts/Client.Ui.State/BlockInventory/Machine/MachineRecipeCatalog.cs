using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.Recipe.Data;
using Client.Ui.State.Recipe.Source;

namespace Client.Ui.State.BlockInventory.Machine
{
    /// <summary>
    /// 解放済みレシピの要約・辞書と、ブロック×選択ごとの行の覚え。レシピ源が変わったときだけ組み、状態更新では組まない（D-39）
    /// Unlocked-recipe summaries and lookup plus the per block×selection row cache; built only when the recipe source changes, never on state updates (D-39)
    /// </summary>
    public sealed class MachineRecipeCatalog
    {
        public static readonly MachineRecipeCatalog Empty = Build(Array.Empty<MachineRecipeData>());

        private readonly IReadOnlyList<MachineRecipeSummary> _summaries;
        private readonly Dictionary<Guid, MachineRecipeData> _recipes;
        private bool _hasCachedRows;
        private Guid _cachedBlockGuid;
        private MachineRecipeSelection _cachedSelection;
        private IReadOnlyList<MachineRecipeSelectionRow> _cachedRows;

        private MachineRecipeCatalog(IReadOnlyList<MachineRecipeSummary> summaries, Dictionary<Guid, MachineRecipeData> recipes)
        {
            _summaries = summaries;
            _recipes = recipes;
        }

        public static MachineRecipeCatalog Build(IReadOnlyList<MachineRecipeData> unlockedRecipes)
        {
            var recipes = new Dictionary<Guid, MachineRecipeData>(unlockedRecipes.Count);
            foreach (var recipe in unlockedRecipes) recipes[recipe.RecipeGuid] = recipe;
            return new MachineRecipeCatalog(MachineRecipeSummaryConversion.ToSummaries(unlockedRecipes), recipes);
        }

        public IReadOnlyList<MachineRecipeSelectionRow> RowsFor(Guid blockGuid, MachineRecipeSelection selection)
        {
            // ブロックと選択が前回と同じなら前回の列を返す（加工中の状態更新で行を組み直さない）
            // Return the previous rows while the block and selection are unchanged (no rebuild on processing updates)
            if (_hasCachedRows && _cachedBlockGuid == blockGuid && _cachedSelection.IsSame(selection)) return _cachedRows;
            _cachedRows = selection.BuildRows(_summaries, blockGuid);
            _cachedBlockGuid = blockGuid;
            _cachedSelection = selection;
            _hasCachedRows = true;
            return _cachedRows;
        }

        public bool TryGetRecipe(Guid recipeGuid, out MachineRecipeData recipe)
        {
            return _recipes.TryGetValue(recipeGuid, out recipe);
        }
    }
}

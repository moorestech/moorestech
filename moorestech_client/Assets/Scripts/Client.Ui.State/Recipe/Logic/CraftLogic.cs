using System;
using System.Collections.Generic;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Data;
using Core.Master;

namespace Client.Ui.State.Recipe.Logic
{
    /// <summary>
    /// レシピ一覧の組み立てとクラフト可否・最大制作数（Web features/recipe/logic/craftLogic の写し）
    /// Builds the recipe list and judges craftability and maximum output (port of Web craftLogic)
    /// </summary>
    public static class CraftLogic
    {
        // 選んだアイテムを作るレシピを、クラフトを先頭・機械を後ろにそれぞれデータ順で並べる
        // Recipes producing the item: crafts first, then machines, each in data order
        public static IReadOnlyList<RecipeEntry> BuildRecipeEntries(IReadOnlyList<CraftRecipeData> craftRecipes, IReadOnlyList<MachineRecipeData> machineRecipes, ItemId itemId)
        {
            var entries = new List<RecipeEntry>();
            foreach (var recipe in craftRecipes)
            {
                if (recipe.ResultItemId == itemId) entries.Add(new CraftRecipeEntry(recipe));
            }
            foreach (var recipe in machineRecipes)
            {
                if (ProducesItem(recipe, itemId)) entries.Add(new MachineRecipeEntry(recipe));
            }
            return entries;

            #region Internal

            // 継承レシピは基準の生産物に加え、継承先の全レベルも生産物として扱う
            // An inheritance recipe also produces every level of its target besides the base output
            bool ProducesItem(MachineRecipeData recipe, ItemId targetItemId)
            {
                foreach (var output in recipe.OutputItems)
                {
                    if (output.ItemId == targetItemId) return true;
                }
                return recipe.LevelInheritance is LevelInheritanceMapping mapping && ContainsItem(mapping.TargetItemIds, targetItemId);
            }

            bool ContainsItem(IReadOnlyList<ItemId> itemIds, ItemId targetItemId)
            {
                foreach (var candidate in itemIds)
                {
                    if (candidate == targetItemId) return true;
                }
                return false;
            }

            #endregion
        }

        public static bool Craftable(CraftRecipeData recipe, IReadOnlyDictionary<ItemId, int> counts)
        {
            return OwnedItemCounts.HasEnoughItems(recipe.RequiredItems, counts);
        }

        // 完成品ごとの最大制作数。同じ完成品のレシピが複数あれば多い方を採る（uGUI ItemListView と同じ）
        // Maximum output per result item; the larger wins across recipes of the same result (same as uGUI ItemListView)
        public static IReadOnlyDictionary<ItemId, int> CraftableResultCounts(IReadOnlyList<CraftRecipeData> recipes, IReadOnlyDictionary<ItemId, int> counts)
        {
            var result = new Dictionary<ItemId, int>();
            foreach (var recipe in recipes)
            {
                var outputCount = MaxCraftTimes(recipe) * recipe.ResultCount;
                if (OwnedItemCounts.OwnedCountOf(result, recipe.ResultItemId) < outputCount) result[recipe.ResultItemId] = outputCount;
            }
            return result;

            #region Internal

            // 素材ごとの「所持数÷必要数」の最小。必要数0の素材は所持0なら0回・所持ありなら読み飛ばし、全素材が読み飛ばしなら0回（Web の NaN／MAX_SAFE_INTEGER 番兵と同じ帰結）
            // Minimum of owned/required; a zero-count material gives 0 if unowned else is skipped; all skipped yields 0 (same outcome as Web NaN/MAX_SAFE_INTEGER)
            int MaxCraftTimes(CraftRecipeData recipe)
            {
                var times = int.MaxValue;
                foreach (var required in recipe.RequiredItems)
                {
                    var owned = OwnedItemCounts.OwnedCountOf(counts, required.ItemId);
                    if (required.Count == 0)
                    {
                        if (owned == 0) return 0;
                        continue;
                    }
                    times = Math.Min(times, owned / required.Count);
                }
                return times == int.MaxValue ? 0 : times;
            }

            #endregion
        }
    }
}

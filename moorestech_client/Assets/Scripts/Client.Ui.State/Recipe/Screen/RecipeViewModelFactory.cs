using System;
using System.Collections.Generic;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Data;
using Client.Ui.State.Recipe.Logic;
using Core.Master;

namespace Client.Ui.State.Recipe.Screen
{
    /// <summary>
    /// データ・持ち数・選択から一覧とビューアの VM を組む純関数（Web RecipeContent・ItemListPanel の計算の写し）
    /// Pure builders of the list and viewer VMs from data, owned counts and selection (port of the Web RecipeContent and ItemListPanel math)
    /// </summary>
    public static class RecipeViewModelFactory
    {
        public static ItemListViewModel BuildItemList(IReadOnlyList<ItemId> items, IReadOnlyList<CraftRecipeData> craftRecipes, IReadOnlyDictionary<ItemId, int> owned)
        {
            // 結果アイテムごとのクラフト可能数をセルへ（無ければ 0。0 はバッジ無し）
            // Craftable count per result item into each cell (0 when absent; 0 shows no badge)
            var counts = CraftLogic.CraftableResultCounts(craftRecipes, owned);
            var cells = new ItemListCell[items.Count];
            for (var i = 0; i < cells.Length; i++) cells[i] = new ItemListCell(items[i], OwnedItemCounts.OwnedCountOf(counts, items[i]));
            return new ItemListViewModel(cells);
        }

        public static RecipeViewerViewModel BuildViewer(RecipeSelection selection, IReadOnlyList<CraftRecipeData> craftRecipes, IReadOnlyList<MachineRecipeData> machineRecipes, IReadOnlyDictionary<ItemId, int> owned)
        {
            if (selection is not ItemRecipeSelection item) return RecipeViewerNoSelection.Instance;

            // クラフト優先の単一列。アンカーは先頭がクラフトのときその 1 件だけ
            // Craft-first single list; the anchor goes only on the head when it is a craft
            var entries = CraftLogic.BuildRecipeEntries(craftRecipes, machineRecipes, item.ItemId);
            var result = new RecipeEntryViewModel[entries.Count];
            for (var i = 0; i < result.Length; i++) result[i] = ToViewModel(entries[i], i == 0);
            return new RecipeViewerItemSelected(item.ItemId, result);

            #region Internal

            RecipeEntryViewModel ToViewModel(RecipeEntry entry, bool isHead)
            {
                return entry switch
                {
                    CraftRecipeEntry craft => new CraftRecipeEntryViewModel(craft.Recipe, BuildMaterials(craft.Recipe), CraftLogic.Craftable(craft.Recipe, owned), isHead),
                    MachineRecipeEntry machine => new MachineRecipeEntryViewModel(machine.Recipe),
                    _ => throw new ArgumentOutOfRangeException(nameof(entry), entry.GetType().Name, "未知のレシピエントリ"),
                };
            }

            IReadOnlyList<RecipeMaterialViewModel> BuildMaterials(CraftRecipeData recipe)
            {
                // 素材ごとの所持・必要・不足（Web CraftRecipeEntry の lacking = owned < required）
                // Owned, required and lacking per material (Web CraftRecipeEntry lacking = owned < required)
                var materials = new RecipeMaterialViewModel[recipe.RequiredItems.Count];
                for (var i = 0; i < materials.Length; i++)
                {
                    var required = recipe.RequiredItems[i];
                    var ownedCount = OwnedItemCounts.OwnedCountOf(owned, required.ItemId);
                    materials[i] = new RecipeMaterialViewModel(required.ItemId, required.Count, ownedCount, ownedCount < required.Count);
                }
                return materials;
            }

            #endregion
        }
    }
}

using System.Collections.Generic;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Data;
using Core.Master;
using Game.UnlockState;

namespace Client.Ui.State.Recipe.Catalog
{
    /// <summary>
    /// 解放済みのクラフトレシピを State のデータへ写す（Web CraftingRecipesTopic の源）
    /// Copies unlocked craft recipes into State data (source of the Web CraftingRecipesTopic)
    /// </summary>
    public static class CraftRecipeCatalog
    {
        public static IReadOnlyList<CraftRecipeData> BuildUnlocked(IGameUnlockStateData unlockState)
        {
            var infos = unlockState.CraftRecipeUnlockStateInfos;
            var result = new List<CraftRecipeData>();
            foreach (var recipe in MasterHolder.CraftRecipeMaster.GetAllCraftRecipes())
            {
                // silent-ok: 解放表に無いレシピは Web の topic と同じくロック扱い / 一覧に出ないことを CraftRecipeCatalogTest と録画 uitk-recipe-craft が見る
                if (!infos.TryGetValue(recipe.CraftRecipeGuid, out var info) || !info.IsUnlocked) continue;

                // 素材と結果を ItemId へ引き直す
                // Re-resolve materials and the result to ItemIds
                var required = new ItemAmount[recipe.RequiredItems.Length];
                for (var i = 0; i < required.Length; i++) required[i] = new ItemAmount(MasterHolder.ItemMaster.GetItemId(recipe.RequiredItems[i].ItemGuid), recipe.RequiredItems[i].Count);
                result.Add(new CraftRecipeData(recipe.CraftRecipeGuid, MasterHolder.ItemMaster.GetItemId(recipe.CraftResultItemGuid), recipe.CraftResultCount, recipe.CraftTime, required));
            }
            return result;
        }
    }
}

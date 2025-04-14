using System;
using System.Collections.Generic;
using Mooresmaster.Loader.CraftRecipesModule;
using Mooresmaster.Model.CraftRecipesModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class CraftRecipeMaster
    {
        public readonly CraftRecipes CraftRecipes;
        
        public CraftRecipeMaster(JToken craftRecipeJToken)
        {
            CraftRecipes = CraftRecipesLoader.Load(craftRecipeJToken);
            ValidateCraftRecipe();
            
            #region Internal
            
            void ValidateCraftRecipe()
            {
                // チェックするアイテムのGUIDを取得
                var checkTargets = new List<Guid>();
                foreach (var craftRecipeMasterElement in CraftRecipes.Data)
                {
                    foreach (var requiredItem in craftRecipeMasterElement.RequiredItems)
                    {
                        checkTargets.Add(requiredItem.ItemGuid);
                    }
                    checkTargets.Add(craftRecipeMasterElement.CraftResultItemGuid);
                }
                
                // アイテムのGUIDが存在するかチェック
                var notExistItemGuids = new List<Guid>();
                foreach (var checkItem in checkTargets)
                {
                    try
                    {
                        MasterHolder.ItemMaster.GetItemId(checkItem);
                    }
                    catch (InvalidOperationException e)
                    {
                        notExistItemGuids.Add(checkItem);
                    }
                }
                
                // アイテムのGUIDが存在しない場合はエラーを出力
                if (notExistItemGuids.Count > 0)
                {
                    var errorMessage = "クラフトレシピに存在しないアイテムのGUIDが含まれています。\n";
                    foreach (var notExistItemGuid in notExistItemGuids)
                    {
                        errorMessage += $"{notExistItemGuid}\n";
                    }
                    throw new InvalidOperationException(errorMessage);
                }
            }
            
  #endregion
        }
        
        /// <summary>
        /// クラフトレシピIDからマスターデータを取得
        /// Gets the master data from the crafting recipe ID.
        /// </summary>
        public CraftRecipeMasterElement GetCraftRecipe(Guid guid)
        {
            return Array.Find(CraftRecipes.Data, x => x.CraftRecipeGuid == guid);
        }
        
        /// <summary>
        /// 指定したアイテムを作るためのクラフトレシピを取得します。
        /// Gets the crafting recipe for the specified item.
        /// </summary>
        public CraftRecipeMasterElement[] GetResultItemCraftRecipes(ItemId itemId)
        {
            var itemGuid = MasterHolder.ItemMaster.GetItemMaster(itemId).ItemGuid;
            return Array.FindAll(CraftRecipes.Data, x => x.CraftResultItemGuid == itemGuid);
        }
        
        /// <summary>
        /// すべてのクラフトレシピを取得します。
        /// Gets all crafting recipes.
        /// </summary>
        public CraftRecipeMasterElement[] GetAllCraftRecipes()
        {
            return CraftRecipes.Data;
        }
    }
}
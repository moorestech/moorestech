using System;
using System.Collections.Generic;
using Mooresmaster.Loader.CraftRecipesModule;
using Mooresmaster.Model.CraftRecipesModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class CraftRecipeMaster : IMasterValidator
    {
        public readonly CraftRecipes CraftRecipes;

        public CraftRecipeMaster(JToken craftRecipeJToken)
        {
            CraftRecipes = CraftRecipesLoader.Load(craftRecipeJToken);
        }

        public bool Validate(out string errorLogs)
        {
            errorLogs = "";
            errorLogs += ValidateCraftRecipe();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string ValidateCraftRecipe()
            {
                // チェックするアイテムのGUIDを取得
                // Get item GUIDs to check
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
                // Check if item GUIDs exist
                var logs = "";
                foreach (var checkItem in checkTargets)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(checkItem);
                    if (itemId == null)
                    {
                        logs += $"[CraftRecipeMaster] has invalid ItemGuid:{checkItem}\n";
                    }
                }

                return logs;
            }

            #endregion
        }

        public void Initialize()
        {
            // CraftRecipeMasterは追加の初期化処理がないため、空実装
            // CraftRecipeMaster has no additional initialization, so empty implementation
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
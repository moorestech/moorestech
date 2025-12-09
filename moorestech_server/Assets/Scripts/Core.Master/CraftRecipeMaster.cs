using System;
using Core.Master.Validator;
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
            return CraftRecipeMasterUtil.Validate(CraftRecipes, out errorLogs);
        }

        public void Initialize()
        {
            CraftRecipeMasterUtil.Initialize(CraftRecipes);
        }
        
        /// <summary>
        /// クラフトレシピIDからマスターデータを取得（見つからない場合は例外）
        /// Gets the master data from the crafting recipe ID (throws if not found).
        /// </summary>
        public CraftRecipeMasterElement GetCraftRecipe(Guid guid)
        {
            var result = GetCraftRecipeOrNull(guid);
            if (result == null)
            {
                throw new InvalidOperationException($"CraftRecipeElement not found. CraftRecipeGuid:{guid}");
            }
            return result;
        }

        /// <summary>
        /// クラフトレシピIDからマスターデータを取得（見つからない場合はnull）
        /// Gets the master data from the crafting recipe ID (returns null if not found).
        /// </summary>
        public CraftRecipeMasterElement GetCraftRecipeOrNull(Guid guid)
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
using System.Collections.Generic;
using System.Linq;
using industrialization.Core.Config.Recipe.Data;
using industrialization.Core.Item;

namespace industrialization.Core.Config.Recipe
{
    public static class MachineRecipeConfig
    {
        
        private static IMachineRecipeData[] _recipedatas;

        //IDからレシピデータを取得する
        public static IMachineRecipeData GetRecipeData(int id)
        {
            _recipedatas ??= MachineRecipeJsonLoad.LoadConfig();
            return _recipedatas[id];
        }

        private static Dictionary<string, IMachineRecipeData> _recipeDataCash;
        /// <summary>
        /// 設置物IDと現在の搬入スロットからレシピを検索し、取得する
        /// </summary>
        /// <param name="installationId">設置物ID</param>
        /// <param name="inputItem">搬入スロット</param>
        /// <returns>レシピデータ</returns>
        public static IMachineRecipeData GetRecipeData(int installationId, List<IItemStack> inputItem)
        {
            _recipedatas ??= MachineRecipeJsonLoad.LoadConfig();

            if (_recipeDataCash == null)
            {
                _recipeDataCash = new Dictionary<string, IMachineRecipeData>();
                _recipedatas.ToList().ForEach(recipe =>
                {
                    _recipeDataCash.Add(
                        GetKey(recipe.InstallationId,recipe.ItemInputs.ToList()),
                        recipe);
                });
            }

            var tmpInputItem = inputItem.Where(i => i.Id != NullItemStack.NullItemId).ToList();
            tmpInputItem.Sort((a, b) => a.Id - b.Id);
            var key = GetKey(installationId, tmpInputItem);
            if (_recipeDataCash.ContainsKey(key))
            {
                return _recipeDataCash[key];
            }
            else
            {
                return new NullMachineRecipeData();
            }
        }
        private static string GetKey(int installationId, List<IItemStack> itemId)
        {
            var items = "";
            itemId.Sort((a, b) => a.Id - b.Id);
            itemId.ForEach(i =>
            {
                items = items + "_" + i.Id;
            });
            return installationId + items;
        }
    }
}
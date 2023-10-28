using System.Collections.Generic;
using System.Linq;
using Core.ConfigJson;
using Core.Item;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.RecipeConfig;

namespace Game.Block.RecipeConfig
{
    public class MachineRecipeConfig : IMachineRecipeConfig
    {
        private readonly Dictionary<string, MachineRecipeData> _recipeDataCache;
        private readonly List<MachineRecipeData> _recipedatas;

        //IDからレシピデータを取得する
        public MachineRecipeConfig(IBlockConfig blockConfig, ItemStackFactory itemStackFactory, ConfigJsonList configJson)
        {
            _recipedatas = new MachineRecipeJsonLoad().LoadConfig(blockConfig, itemStackFactory, configJson.SortedMachineRecipeConfigJsonList);

            _recipeDataCache = new Dictionary<string, MachineRecipeData>();
            _recipedatas.ToList().ForEach(recipe =>
            {
                _recipeDataCache.Add(
                    GetRecipeDataCacheKey(recipe.BlockId, recipe.ItemInputs.ToList()),
                    recipe);
            });
        }


        public IReadOnlyList<MachineRecipeData> GetAllRecipeData()
        {
            return _recipedatas;
        }

        public MachineRecipeData GetEmptyRecipeData()
        {
            return MachineRecipeData.CreateEmptyRecipe();
        }

        public MachineRecipeData GetRecipeData(int id)
        {
            return id == -1 ? MachineRecipeData.CreateEmptyRecipe() : _recipedatas[id];
        }


        /// <summary>
        ///     設置物IDと現在の搬入スロットからレシピを検索し、取得する
        /// </summary>
        /// <param name="BlockId">設置物ID</param>
        /// <param name="inputItem">搬入スロット</param>
        /// <returns>レシピデータ</returns>
        public MachineRecipeData GetRecipeData(int BlockId, IReadOnlyList<IItemStack> inputItem)
        {
            var tmpInputItem = inputItem.Where(i => i.Count != 0).ToList();
            tmpInputItem.Sort((a, b) => a.Id - b.Id);
            var key = GetRecipeDataCacheKey(BlockId, tmpInputItem);
            if (_recipeDataCache.ContainsKey(key)) return _recipeDataCache[key];

            return MachineRecipeData.CreateEmptyRecipe();
        }

        private string GetRecipeDataCacheKey(int blockId, List<IItemStack> itemId)
        {
            var items = "";
            itemId.Sort((a, b) => a.Id - b.Id);
            itemId.ForEach(i => { items = items + "_" + i.Id; });
            return blockId + items;
        }
    }
}
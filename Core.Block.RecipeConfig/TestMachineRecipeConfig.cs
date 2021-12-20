using System.Collections.Generic;
using System.Linq;
using Core.Block.RecipeConfig.Data;
using Core.Item;
using Core.Item.Util;

namespace Core.Block.RecipeConfig
{
    public class TestMachineRecipeConfig : IMachineRecipeConfig
    {
        private readonly List<IMachineRecipeData> _recipedatas;
        
        private readonly Dictionary<string, IMachineRecipeData> _recipeDataCash;

        //IDからレシピデータを取得する
        public TestMachineRecipeConfig(ItemStackFactory itemStackFactory)
        {
            _recipedatas = new MachineRecipeJsonLoad().LoadConfig(itemStackFactory);
            
            _recipeDataCash = new Dictionary<string, IMachineRecipeData>();
            _recipedatas.ToList().ForEach(recipe =>
            {
                _recipeDataCash.Add(
                    GetKey(recipe.BlockId, recipe.ItemInputs.ToList()),
                    recipe);
            });
        }

        public IMachineRecipeData GetRecipeData(int id)
        {
            if (id == -1)
            {
                return new NullMachineRecipeData();
            }
            return _recipedatas[id];
        }

        public IMachineRecipeData GetNullRecipeData()
        {
            return new NullMachineRecipeData();
        }


        /// <summary>
        /// 設置物IDと現在の搬入スロットからレシピを検索し、取得する
        /// </summary>
        /// <param name="BlockId">設置物ID</param>
        /// <param name="inputItem">搬入スロット</param>
        /// <returns>レシピデータ</returns>
        public IMachineRecipeData GetRecipeData(int BlockId, List<IItemStack> inputItem)
        {
            var tmpInputItem = inputItem.Where(i => i.Count != 0).ToList();
            tmpInputItem.Sort((a, b) => a.Id - b.Id);
            var key = GetKey(BlockId, tmpInputItem);
            if (_recipeDataCash.ContainsKey(key))
            {
                return _recipeDataCash[key];
            }
            else
            {
                return new NullMachineRecipeData();
            }
        }

        private string GetKey(int blockId, List<IItemStack> itemId)
        {
            var items = "";
            itemId.Sort((a, b) => a.Id - b.Id);
            itemId.ForEach(i => { items = items + "_" + i.Id; });
            return blockId + items;
        }
    }
}
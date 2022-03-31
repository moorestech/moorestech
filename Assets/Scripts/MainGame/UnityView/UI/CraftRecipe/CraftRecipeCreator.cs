using System.Collections.Generic;
using System.Linq;
using Core.Item;
using MainGame.Basic;
using SinglePlay;

namespace MainGame.UnityView.UI.CraftRecipe
{
    public class CraftRecipeCreator
    {
        private readonly Dictionary<int, List<Recipe>> _itemIdToRecipe = new();

        public CraftRecipeCreator(ItemListViewer itemListViewer,SinglePlayInterface singlePlayInterface)
        {
            //レシピ表示用のDictionaryを構築する
            var craftRecipe = singlePlayInterface.CraftingConfig.GetCraftingConfigList();
            var machineRecipe = singlePlayInterface.MachineRecipeConfig.GetAllRecipeData();

            //手元クラフトの構築
            foreach (var recipe in craftRecipe)
            {
                if (!_itemIdToRecipe.TryGetValue(recipe.Result.Id,out var list))
                {
                    list = new();
                    _itemIdToRecipe[recipe.Result.Id] = list;
                }
                
                list.Add(new Recipe(recipe.Items,recipe.Result,RecipeType.Craft));
            }
            
            
            //機械レシピの構築
            foreach (var recipe in machineRecipe)
            {
                var resultItem = recipe.ItemOutputs.Select(o => o.OutputItem).ToList();
                foreach (var output in recipe.ItemOutputs)
                {
                    if (!_itemIdToRecipe.TryGetValue(output.OutputItem.Id,out var list))
                    {
                        list = new();
                        _itemIdToRecipe[output.OutputItem.Id] = list;
                    }
                    
                    list.Add(new Recipe(recipe.ItemInputs,resultItem,RecipeType.Machine));
                }
            }
            
            
            
            //イベントをサブスクライブ
            itemListViewer.OnItemListClick += OnItemListClick;
        }

        private void OnItemListClick(int itemId)
        {
            
        }
    }

    class Recipe
    {
        public readonly List<ItemStack> ItemStacks = new();
        public readonly List<ItemStack> ResultItem = new();
        public readonly RecipeType RecipeType;

        public Recipe(List<IItemStack> itemStacks, List<IItemStack> resultItem, RecipeType recipeType)
        {
            foreach (var item in itemStacks)
            {
                ItemStacks.Add(new ItemStack(item.Id,item.Count));
            }
            foreach (var item in resultItem)
            {
                ResultItem.Add(new ItemStack(item.Id,item.Count));
            }
            
            RecipeType = recipeType;
        }
        public Recipe(List<IItemStack> itemStacks, IItemStack resultItem, RecipeType recipeType)
        {
            foreach (var item in itemStacks)
            {
                ItemStacks.Add(new ItemStack(item.Id,item.Count));
            }
            ResultItem.Add(new ItemStack(resultItem.Id,resultItem.Count));
            
            RecipeType = recipeType;
        }
    }

    enum RecipeType
    {
        Craft,
        Machine
    }
}
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.UnlockState;
using Mooresmaster.Model.CraftRecipesModule;
using Mooresmaster.Model.MachineRecipesModule;

namespace Client.Game.InGame.UI.Inventory.RecipeViewer
{
    /// <summary>
    /// アイテムID → 全てのレシピ を管理するためのクラス 
    /// </summary>
    public class  ItemRecipeViewerDataContainer
    {
        private readonly Dictionary<ItemId, RecipeViewerItemRecipes> _recipeViewerElements = new();
        
        public RecipeViewerItemRecipes GetItem(ItemId itemId)
        {
            return _recipeViewerElements.GetValueOrDefault(itemId);
        }
        
        public ItemRecipeViewerDataContainer(IGameUnlockStateData gameUnlockStateData)
        {
            // そのアイテムを作成するための機械のレシピを取得
            // Get the recipe of the machine to create the item
            var machineRecipeDictionary = new Dictionary<ItemId, List<MachineRecipeMasterElement>>();
            foreach (var machineRecipeMaster in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
            {
                foreach (var outputItem in machineRecipeMaster.OutputItems)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(outputItem.ItemGuid);
                    if (!machineRecipeDictionary.ContainsKey(itemId))
                    {
                        machineRecipeDictionary.Add(itemId, new List<MachineRecipeMasterElement>());
                    }
                    
                    machineRecipeDictionary[itemId].Add(machineRecipeMaster);
                }
            }
            
            // そのアイテムを作成するためのクラフトレシピを取得
            // Get the craft recipe to create the item
            foreach (var itemId in MasterHolder.ItemMaster.GetItemAllIds())
            {
                var resultCraftRecipes = MasterHolder.CraftRecipeMaster.GetResultItemCraftRecipes(itemId).ToList();
                
                // そのアイテムを作成するための機械のレシピを機械ごとに作成
                // Create a machine recipe for each machine to create the item
                var resultMachineRecipes = new Dictionary<BlockId, List<MachineRecipeMasterElement>>();
                if (machineRecipeDictionary.TryGetValue(itemId, out var machineRecipesList))
                {
                    foreach (var machineRecipe in machineRecipesList)
                    {
                        var blockId = MasterHolder.BlockMaster.GetBlockId(machineRecipe.BlockGuid);
                        if (resultMachineRecipes.ContainsKey(blockId))
                        {
                            resultMachineRecipes[blockId].Add(machineRecipe);
                        }
                        else
                        {
                            resultMachineRecipes.Add(blockId, new List<MachineRecipeMasterElement> { machineRecipe });
                        }
                    }
                }
                
                _recipeViewerElements.Add(itemId, new RecipeViewerItemRecipes(resultCraftRecipes, resultMachineRecipes, itemId, gameUnlockStateData));
            }
            
            // レシピが存在しないアイテムを除外する
            // Exclude items with no recipes
            var removeList = new List<ItemId>();
            foreach (var kv in _recipeViewerElements)
            {
                var itemId = kv.Key;
                var recipe = kv.Value;
                
                if (recipe.AllCraftRecipes.Count == 0 && recipe.MachineRecipes.Count == 0)
                {
                    removeList.Add(itemId);
                }
            }
            foreach (var itemId in removeList)
            {
                _recipeViewerElements.Remove(itemId);
            }
        }
    }
    
    public class RecipeViewerItemRecipes
    {
        public readonly ItemId ResultItemId;
        
        //TODO 他のmodの他のレシピにも対応できるようの柔軟性をもたせた設計を考える
        public readonly Dictionary<BlockId, List<MachineRecipeMasterElement>> MachineRecipes;
        public readonly List<CraftRecipeMasterElement> AllCraftRecipes;
        
        private readonly IGameUnlockStateData _gameUnlockStateData;
        
        public RecipeViewerItemRecipes(List<CraftRecipeMasterElement> craftRecipes, Dictionary<BlockId, List<MachineRecipeMasterElement>> machineRecipes, ItemId resultItemId, IGameUnlockStateData gameUnlockStateData)
        {
            AllCraftRecipes = craftRecipes;
            MachineRecipes = machineRecipes;
            ResultItemId = resultItemId;
            _gameUnlockStateData = gameUnlockStateData;
        }
        
        /// <summary>
        /// アンロック済みのクラフトレシピを取得
        /// </summary>
        public List<CraftRecipeMasterElement> UnlockedCraftRecipes()
        {
            var infos = _gameUnlockStateData.CraftRecipeUnlockStateInfos;
            return AllCraftRecipes.Where(c => infos[c.CraftRecipeGuid].IsUnlocked).ToList();
        }
    }
}
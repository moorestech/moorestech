using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Mooresmaster.Model.CraftRecipesModule;
using Mooresmaster.Model.MachineRecipesModule;

namespace Client.Game.InGame.UI.Inventory.Sub
{
    // TODO クラフトレシピ改善時にこれを使う
    public class ItemRecipeViewerDataContainer
    {
        public readonly Dictionary<ItemId, RecipeViewerItemRecipes> CraftRecipeViewerElements = new();
        
        public ItemRecipeViewerDataContainer()
        {
            // そのアイテムを作成するための機械のレシピを取得
            // Get the recipe of the machine to create the item
            var machineRecipeDictionary = new Dictionary<ItemId, List<MachineRecipeMasterElement>>();
            foreach (var machineRecipeMaster in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
            {
                foreach (var inputItem in machineRecipeMaster.InputItems)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(inputItem.ItemGuid);
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
                
                CraftRecipeViewerElements.Add(itemId, new RecipeViewerItemRecipes(resultCraftRecipes, resultMachineRecipes));
            }
        }
    }
    
    public class RecipeViewerItemRecipes
    {
        //TODO 他のmodの他のレシピにも対応できるようの柔軟性をもたせた設計を考える
        public readonly List<CraftRecipeMasterElement> CraftRecipes;
        public readonly Dictionary<BlockId, List<MachineRecipeMasterElement>> MachineRecipes;
        
        public RecipeViewerItemRecipes(List<CraftRecipeMasterElement> craftRecipes, Dictionary<BlockId, List<MachineRecipeMasterElement>> machineRecipes)
        {
            CraftRecipes = craftRecipes;
            MachineRecipes = machineRecipes;
        }
    }
}
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
        public readonly Dictionary<ItemId,ItemRecipes> CraftRecipeViewerElements = new();
        
        public ItemRecipeViewerDataContainer()
        {
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
            
            foreach (var itemId in MasterHolder.ItemMaster.GetItemAllIds())
            {
                var craftRecipes = MasterHolder.CraftRecipeMaster.GetResultItemCraftRecipes(itemId).ToList();
                
                var machineRecipeMasterElements = new List<MachineRecipeMasterElement>();
                if (machineRecipeDictionary.TryGetValue(itemId, out List<MachineRecipeMasterElement> value))
                {
                    machineRecipeMasterElements.AddRange(value);
                }
                
                CraftRecipeViewerElements.Add(itemId, new ItemRecipes(craftRecipes, machineRecipeMasterElements));
            }
        }
    }
    
    public class ItemRecipes
    {
        public readonly List<CraftRecipeMasterElement> CraftRecipes;
        public readonly List<MachineRecipeMasterElement> MachineRecipes;
        
        public ItemRecipes(List<CraftRecipeMasterElement> craftRecipes, List<MachineRecipeMasterElement> machineRecipes)
        {
            CraftRecipes = craftRecipes;
            MachineRecipes = machineRecipes;
        }
        
        //TODO 他のmodの他のレシピにも対応できるようの柔軟性をもたせた設計を考える
    }
}
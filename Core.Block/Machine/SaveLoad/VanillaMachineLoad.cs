using System;
using Core.Block.Machine.Inventory;
using Core.Block.RecipeConfig;
using Core.Item;

namespace Core.Block.Machine.SaveLoad
{
    public class VanillaMachineLoad
    {
        
        private readonly VanillaMachineInputInventory _vanillaMachineInputInventory;
        private readonly VanillaMachineOutputInventory _vanillaMachineOutputInventory;
        private readonly ItemStackFactory _itemStackFactory;
        private readonly IMachineRecipeConfig _machineRecipeConfig;
        private readonly int _requestPower;

        public VanillaMachineLoad(
            VanillaMachineInputInventory vanillaMachineInputInventory, 
            VanillaMachineOutputInventory vanillaMachineOutputInventory, 
            ItemStackFactory itemStackFactory, 
            IMachineRecipeConfig machineRecipeConfig,
            int requestPower)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;
            _itemStackFactory = itemStackFactory;
            _machineRecipeConfig = machineRecipeConfig;
            _requestPower = requestPower;
        }

        public VanillaMachineRunProcess Load(string loadString)
        {
            var split = loadString.Split(',');
            
            int index = 1;
            int inventorySlot = 0;
            for (; split[index] != "outputSlot"; index+=2)
            {
                var id = int.Parse(split[index]);
                var count = int.Parse(split[index + 1]);
                _vanillaMachineInputInventory.SetItem(inventorySlot,_itemStackFactory.Create(id, count));
                inventorySlot++;
            }
            
            inventorySlot = 0;
            for (index++; split[index] != "state"; index+=2)
            {
                var id = int.Parse(split[index]);
                var count = int.Parse(split[index + 1]);
                _vanillaMachineOutputInventory.SetItem(inventorySlot,_itemStackFactory.Create(id, count));
                inventorySlot++;
            }
            
            index++;
            var state = (ProcessState) int.Parse(split[index]);
            index+=2;
            var remainingMillSecond = Double.Parse(split[index]);
            index+=2;
            int recipeId = int.Parse(split[index]);
            var processingRecipeData = _machineRecipeConfig.GetRecipeData(recipeId);

            return new VanillaMachineRunProcess(_vanillaMachineInputInventory, _vanillaMachineOutputInventory, state, remainingMillSecond, processingRecipeData,_requestPower);
        }
    }
}
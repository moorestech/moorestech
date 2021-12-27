using System;
using Core.Block.RecipeConfig;
using Core.Item;

namespace Core.Block.Machine
{
    public class NormalMachineLoad
    {
        
        private readonly NormalMachineInputInventory _normalMachineInputInventory;
        private readonly NormalMachineOutputInventory _normalMachineOutputInventory;
        private readonly ItemStackFactory _itemStackFactory;
        private readonly IMachineRecipeConfig _machineRecipeConfig;

        public NormalMachineLoad(NormalMachineInputInventory normalMachineInputInventory, NormalMachineOutputInventory normalMachineOutputInventory, ItemStackFactory itemStackFactory, IMachineRecipeConfig machineRecipeConfig)
        {
            _normalMachineInputInventory = normalMachineInputInventory;
            _normalMachineOutputInventory = normalMachineOutputInventory;
            _itemStackFactory = itemStackFactory;
            _machineRecipeConfig = machineRecipeConfig;
        }

        public NormalMachineRunProcess Load(string loadString)
        {
            var split = loadString.Split(',');
            int index = 1;
            int inventorySlot = 0;
            for (; split[index] != "outputSlot"; index+=2)
            {
                var id = int.Parse(split[index]);
                var count = int.Parse(split[index + 1]);
                _normalMachineInputInventory.SetItem(inventorySlot,_itemStackFactory.Create(id, count));
                inventorySlot++;
            }
            
            inventorySlot = 0;
            for (index++; split[index] != "state"; index+=2)
            {
                var id = int.Parse(split[index]);
                var count = int.Parse(split[index + 1]);
                _normalMachineOutputInventory.SetItem(inventorySlot,_itemStackFactory.Create(id, count));
                inventorySlot++;
            }
            index++;
            var state = (ProcessState) int.Parse(split[index]);
            index+=2;
            var remainingMillSecond = Double.Parse(split[index]);
            index+=2;
            int recipeId = int.Parse(split[index]);
            var processingRecipeData = _machineRecipeConfig.GetRecipeData(recipeId);

            return new NormalMachineRunProcess(_normalMachineInputInventory, _normalMachineOutputInventory, state, remainingMillSecond, processingRecipeData);
        }
    }
}
using Game.Block.Blocks.Machine.Inventory;
using Game.Context;

namespace Game.Block.Blocks.Machine.SaveLoad
{
    public class VanillaMachineLoad
    {
        private readonly int _requestPower;
        private readonly VanillaMachineInputInventory _vanillaMachineInputInventory;
        private readonly VanillaMachineOutputInventory _vanillaMachineOutputInventory;
        
        public VanillaMachineLoad(
            VanillaMachineInputInventory vanillaMachineInputInventory,
            VanillaMachineOutputInventory vanillaMachineOutputInventory,
            int requestPower)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;
            _requestPower = requestPower;
        }
        
        public VanillaMachineProcessorComponent LoadVanillaMachineRunProcess(string loadString)
        {
            var split = loadString.Split(',');
            
            var index = 1;
            var inventorySlot = 0;
            for (; split[index] != "outputSlot"; index += 2)
            {
                var id = int.Parse(split[index]);
                var count = int.Parse(split[index + 1]);
                var item = ServerContext.ItemStackFactory.Create(id, count);
                _vanillaMachineInputInventory.SetItem(inventorySlot, item);
                inventorySlot++;
            }
            
            inventorySlot = 0;
            for (index++; split[index] != "state"; index += 2)
            {
                var id = int.Parse(split[index]);
                var count = int.Parse(split[index + 1]);
                var item = ServerContext.ItemStackFactory.Create(id, count);
                _vanillaMachineOutputInventory.SetItem(inventorySlot, item);
                inventorySlot++;
            }
            
            index++;
            var state = (ProcessState)int.Parse(split[index]);
            index += 2;
            var remainingMillSecond = double.Parse(split[index]);
            index += 2;
            var recipeId = int.Parse(split[index]);
            var processingRecipeData = ServerContext.MachineRecipeConfig.GetRecipeData(recipeId);
            
            return new VanillaMachineProcessorComponent(_vanillaMachineInputInventory, _vanillaMachineOutputInventory, state,
                remainingMillSecond, processingRecipeData, _requestPower);
        }
    }
}
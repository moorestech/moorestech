using System.Text;
using Game.Block.Blocks.Machine.Inventory;

namespace Game.Block.Blocks.Machine.SaveLoad
{
    public class VanillaMachineSave
    {
        private readonly VanillaMachineInputInventory _vanillaMachineInputInventory;
        private readonly VanillaMachineOutputInventory _vanillaMachineOutputInventory;
        private readonly VanillaMachineRunProcess _vanillaMachineRunProcess;

        public VanillaMachineSave(
            VanillaMachineInputInventory vanillaMachineInputInventory,
            VanillaMachineOutputInventory vanillaMachineOutputInventory,
            VanillaMachineRunProcess vanillaMachineRunProcess)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;
            _vanillaMachineRunProcess = vanillaMachineRunProcess;
        }

        public string Save()
        {
            
            //inputSlot,item1 id,item1 count,item2 id,item2 count,outputSlot,item1 id,item1 count,item2 id,item2 count,state,0 or 1,remainingTime,500
            var saveState = new StringBuilder("inputSlot,");
            
            foreach (var item in _vanillaMachineInputInventory.InputSlot) saveState.Append(item.Id + "," + item.Count + ",");

            saveState.Append("outputSlot,");
            
            foreach (var item in _vanillaMachineOutputInventory.OutputSlot) saveState.Append(item.Id + "," + item.Count + ",");

            
            saveState.Append("state," + (int)_vanillaMachineRunProcess.CurrentState + ",");
            
            saveState.Append("remainingTime," + _vanillaMachineRunProcess.RemainingMillSecond + ",");
            //ID
            saveState.Append("recipeId," + _vanillaMachineRunProcess.RecipeDataId);

            return saveState.ToString();
        }
    }
}
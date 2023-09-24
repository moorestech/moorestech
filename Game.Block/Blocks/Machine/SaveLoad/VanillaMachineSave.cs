using System.Text;
using Core.Block.Blocks.Machine.Inventory;

namespace Core.Block.Blocks.Machine.SaveLoad
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
            //フォーマット
            //inputSlot,item1 id,item1 count,item2 id,item2 count,outputSlot,item1 id,item1 count,item2 id,item2 count,state,0 or 1,remainingTime,500
            StringBuilder saveState = new StringBuilder("inputSlot,");
            //インプットスロットを保存
            foreach (var item in _vanillaMachineInputInventory.InputSlot)
            {
                saveState.Append(item.Id + "," + item.Count + ",");
            }

            saveState.Append("outputSlot,");
            //アウトプットスロットを保存
            foreach (var item in _vanillaMachineOutputInventory.OutputSlot)
            {
                saveState.Append(item.Id + "," + item.Count + ",");
            }

            //状態を保存
            saveState.Append("state," + (int) _vanillaMachineRunProcess.CurrentState + ",");
            //現在の残り時間を保存
            saveState.Append("remainingTime," + _vanillaMachineRunProcess.RemainingMillSecond + ",");
            //レシピIDを保存
            saveState.Append("recipeId," + _vanillaMachineRunProcess.RecipeDataId);

            return saveState.ToString();
        }
    }
}
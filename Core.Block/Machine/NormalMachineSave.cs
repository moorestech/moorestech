using System;
using System.Text;
using Core.Item;

namespace Core.Block.Machine
{
    public class NormalMachineSaveLoad
    {
        private readonly NormalMachineInputInventory _normalMachineInputInventory;
        private readonly NormalMachineOutputInventory _normalMachineOutputInventory;
        private readonly NormalMachineRunProcess _normalMachineRunProcess;

        public NormalMachineSaveLoad(NormalMachineInputInventory normalMachineInputInventory, NormalMachineOutputInventory normalMachineOutputInventory, NormalMachineRunProcess normalMachineRunProcess, ItemStackFactory itemStackFactory)
        {
            _normalMachineInputInventory = normalMachineInputInventory;
            _normalMachineOutputInventory = normalMachineOutputInventory;
            _normalMachineRunProcess = normalMachineRunProcess;
        }

        public string Save()
        {
            //フォーマット
            //inputSlot,item1 id,item1 count,item2 id,item2 count,outputSlot,item1 id,item1 count,item2 id,item2 count,state,0 or 1,remainingTime,500
            StringBuilder saveState = new StringBuilder("inputSlot,");
            //インプットスロットを保存
            foreach (var item in _normalMachineInputInventory.InputSlot)
            {
                saveState.Append(item.Id + "," + item.Count + ",");
            }
            saveState.Append("outputSlot,");
            //アウトプットスロットを保存
            foreach (var item in _normalMachineOutputInventory.OutputSlot)
            {
                saveState.Append(item.Id + "," + item.Count + ",");
            }
            //状態を保存
            saveState.Append("state,"+(int)_normalMachineRunProcess.State + ",");
            //現在の残り時間を保存
            saveState.Append("remainingTime,"+_normalMachineRunProcess.RemainingMillSecond + ",");
            //レシピIDを保存
            saveState.Append("recipeId,"+_normalMachineRunProcess.RecipeDataId);
            
            return saveState.ToString();
        }
    }
}
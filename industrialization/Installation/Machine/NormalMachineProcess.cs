using System.Collections.Generic;
using industrialization.Config.Installation;
using industrialization.Item;
using industrialization.Util;

namespace industrialization.Installation.Machine
{
    public class NormalMachineProcess : IMachineComponent
    {
        private readonly List<IItemStack> _inputSlot;

        public NormalMachineProcess(int installtionId)
        {
            var data = InstallationConfig.GetInstallationsConfig(installtionId);
            _inputSlot = CreateEmptyItemStacksList.Create(data.InputSlot);
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            
            for (int i = 0; i < _inputSlot.Count; i++)
            {
                if (_inputSlot[i].CanAdd(itemStack))
                {
                    var r = _inputSlot[i].AddItem(itemStack);
                    _inputSlot[i] = r.MineItemStack;
                    //TODO プロセスをスタートさせる
                    return r.ReceiveItemStack;
                }
            }
            return itemStack;
        }
        
        
        
        //TODO プロセスをスタートする
        void StartProcess()
        {
            
            //プロセスをスタートできるか判定
            if (_machineRunProcess != null && _machineRunProcess.IsProcessing()) return;
            
            var recipe = MachineRecipeConfig.GetRecipeData(installationId, InputSlot.ToList());
            var tmp = recipe.RecipeConfirmation(InputSlot);
            if(!tmp) return;
            
            
            //TODO アウトプットスロットに空きがあるかチェック

            //スタートできるなら加工をスタートし、アイテムを減らす
            _machineRunProcess = new MachineRunProcess(OutputEvent,recipe);
            foreach (var item in recipe.ItemInputs)
            {
                for (int i = 0; i < _inputSlot.Count; i++)
                {
                    if (_inputSlot[i].Id == item.Id &&  item.Amount <=_inputSlot[i].Amount)
                    {
                        _inputSlot[i] = _inputSlot[i].SubItem(item.Amount);
                        break;
                    }
                }
            }
        }
    }
}
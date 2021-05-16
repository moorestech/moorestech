using System.Collections.Generic;
using industrialization.Config.Installation;
using industrialization.Item;
using industrialization.Util;

namespace industrialization.Installation.Machine
{
    public class NormalMachineInputInventory : IMachineComponent
    {
        private NormalMachineStartProcess _normalMachineStartProcess;
        private List<IItemStack> _inputSlot;

        public NormalMachineInputInventory(NormalMachineStartProcess normalMachineStartProcess,int installationId)
        {
            _normalMachineStartProcess = normalMachineStartProcess;
            var data = InstallationConfig.GetInstallationsConfig(installationId);
            _inputSlot = CreateEmptyItemStacksList.Create(data.InputSlot);
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            
            for (var i = 0; i < _inputSlot.Count; i++)
            {
                if (!_inputSlot[i].IsAllowedToAdd(itemStack)) continue;
                
                //インベントリにアイテムを入れる
                var r = _inputSlot[i].AddItem(itemStack);
                _inputSlot[i] = r.MineItemStack;
                
                //プロセスをスタートさせる
                _inputSlot = _normalMachineStartProcess.StartingProcess(_inputSlot);
                
                //とった結果のアイテムを返す
                return r.ReceiveItemStack;
            }
            return itemStack;
        }
    }
}
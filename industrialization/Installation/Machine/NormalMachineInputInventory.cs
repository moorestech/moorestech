using System.Collections.Generic;
using industrialization.Config.Installation;
using industrialization.Item;
using industrialization.Util;

namespace industrialization.Installation.Machine
{
    public class NormalMachineInputInventory : IMachineComponent
    {
        private NormalMachineProcess _normalMachineProcess;
        private List<IItemStack> _inputSlot;

        public NormalMachineInputInventory(NormalMachineProcess normalMachineProcess,int installationId)
        {
            _normalMachineProcess = normalMachineProcess;
            var data = InstallationConfig.GetInstallationsConfig(installationId);
            _inputSlot = CreateEmptyItemStacksList.Create(data.InputSlot);
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            
            for (var i = 0; i < _inputSlot.Count; i++)
            {
                if (!_inputSlot[i].CanAdd(itemStack)) continue;
                
                //インベントリにアイテムを入れる
                var r = _inputSlot[i].AddItem(itemStack);
                _inputSlot[i] = r.MineItemStack;
                
                //プロセスをスタートさせる
                _inputSlot = _normalMachineProcess.StartProcess(_inputSlot);
                
                //とった結果のアイテムを返す
                return r.ReceiveItemStack;
            }
            return itemStack;
        }
    }
}
using System.Collections.Generic;
using System.Linq;
using industrialization.Config.Installation;
using industrialization.Item;
using industrialization.Util;

namespace industrialization.Installation.Machine
{
    public class NormalMachineInputInventory
    {
        public readonly NormalMachineStartProcess NormalMachineStartProcess;
        private List<IItemStack> _inputSlot;

        public List<IItemStack> InputSlot
        {
            get
            {
                var a = _inputSlot.Where(i => i.Id != NullItemStack.NullItemId).ToList();
                a.Sort((a, b) => a.Id - b.Id);
                return a.ToList();
            }
        }

        public NormalMachineInputInventory(int installationId,NormalMachineStartProcess normalMachineStartProcess)
        {
            NormalMachineStartProcess = normalMachineStartProcess;
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
                _inputSlot = NormalMachineStartProcess.StartingProcess(_inputSlot);
                
                //とった結果のアイテムを返す
                return r.ReceiveItemStack;
            }
            return itemStack;
        }
    }
}
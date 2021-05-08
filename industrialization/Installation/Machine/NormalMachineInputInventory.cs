using System.Collections.Generic;
using industrialization.Config.Installation;
using industrialization.Item;
using industrialization.Util;

namespace industrialization.Installation.Machine
{
    public class NormalMachineInputInventory : IMachineComponent
    {
        private readonly List<IItemStack> _inputSlot;

        public NormalMachineInputInventory(int installtionId)
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
    }
}
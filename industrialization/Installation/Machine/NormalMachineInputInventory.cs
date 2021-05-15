using System.Collections.Generic;
using industrialization.Item;
using industrialization.Util;

namespace industrialization.Installation.Machine
{
    public class NormalMachineInputInventory : IMachineComponent
    {
        private NormalMachineProcess _normalMachineProcess;
        private readonly List<IItemStack> _inputSlot;

        public NormalMachineInputInventory(NormalMachineProcess normalMachineProcess)
        {
            _normalMachineProcess = normalMachineProcess;
            //TODO ここ取得できるようにする
            _inputSlot = CreateEmptyItemStacksList.Create(4);
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            
            for (var i = 0; i < _inputSlot.Count; i++)
            {
                if (!_inputSlot[i].CanAdd(itemStack)) continue;
                var r = _inputSlot[i].AddItem(itemStack);
                _inputSlot[i] = r.MineItemStack;
                //TODO プロセスをスタートさせる
                return r.ReceiveItemStack;
            }
            return itemStack;
        }
    }
}
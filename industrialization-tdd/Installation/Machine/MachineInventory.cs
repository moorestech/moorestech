using System.Linq;
using industrialization.Inventory;
using industrialization.Item;

namespace industrialization.Installation.Machine
{
    public class MachineInventory : IInstallationInventory
    {
        private MachineRunProcess machineRunProcess;
        private IItemStack[] InputSlot;
        private IItemStack[] OutpuutSlot;

        //TODO インプット、アウトプットスロットを取得し実装
        public MachineInventory(int inventorySlots)
        {
            machineRunProcess = new MachineRunProcess(OutputEvent);
            InputSlot = new ItemStack[1];
            OutpuutSlot = new ItemStack[1];
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            for (int i = 0; i < InputSlot.Length; i++)
            {
                if (InputSlot[i].ID == itemStack.ID)
                {
                    var r = InputSlot[i].addItem(itemStack);
                    InputSlot[i] = r.MineItemStack;
                    return r.ReceiveItemStack;
                }
            }
            return itemStack;
        }

        void OutputEvent(ItemStack output)
        {
            
        }

        public InventoryData GetInventory()
        {
            throw new System.NotImplementedException();
        }
    }
}
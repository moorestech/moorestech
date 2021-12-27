using Core.Block.BlockInventory;
using Core.Block.Machine.Inventory;
using Core.Item;

namespace Core.Block.Machine
{
    public class NormalMachineBlockInventory
    {
        private readonly NormalMachineInputInventory _normalMachineInputInventory;
        private readonly NormalMachineOutputInventory _normalMachineOutputInventory;

        public NormalMachineBlockInventory(NormalMachineInputInventory normalMachineInputInventory, NormalMachineOutputInventory normalMachineOutputInventory)
        {
            _normalMachineInputInventory = normalMachineInputInventory;
            _normalMachineOutputInventory = normalMachineOutputInventory;
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            //アイテムをインプットスロットに入れた後、プロセス開始できるなら開始
            var item = _normalMachineInputInventory.InsertItem(itemStack);
            return item;
        }

        public void AddConnector(IBlockInventory blockInventory)
        {
            _normalMachineOutputInventory.AddConnectInventory(blockInventory);
            
        }

        public void RemoveConnector(IBlockInventory blockInventory)
        {
            _normalMachineOutputInventory.RemoveConnectInventory(blockInventory);
        }
    }
}
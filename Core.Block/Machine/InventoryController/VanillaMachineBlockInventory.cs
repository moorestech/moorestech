using Core.Block.BlockInventory;
using Core.Block.Machine.Inventory;
using Core.Item;

namespace Core.Block.Machine.InventoryController
{
    public class VanillaMachineBlockInventory
    {
        private readonly VanillaMachineInputInventory _vanillaMachineInputInventory;
        private readonly VanillaMachineOutputInventory _vanillaMachineOutputInventory;

        public VanillaMachineBlockInventory(VanillaMachineInputInventory vanillaMachineInputInventory, VanillaMachineOutputInventory vanillaMachineOutputInventory)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            //アイテムをインプットスロットに入れた後、プロセス開始できるなら開始
            var item = _vanillaMachineInputInventory.InsertItem(itemStack);
            return item;
        }

        public void AddConnector(IBlockInventory blockInventory)
        {
            _vanillaMachineOutputInventory.AddConnectInventory(blockInventory);
            
        }

        public void RemoveConnector(IBlockInventory blockInventory)
        {
            _vanillaMachineOutputInventory.RemoveConnectInventory(blockInventory);
        }
    }
}
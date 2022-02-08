using Core.Item;

namespace Core.Block.BlockInventory
{
    public interface IBlockInventory
    {
        public IItemStack InsertItem(IItemStack itemStack);
        public void AddOutputConnector(IBlockInventory blockInventory);
        public void RemoveOutputConnector(IBlockInventory blockInventory);

        public int GetSlotSize();
    }
}
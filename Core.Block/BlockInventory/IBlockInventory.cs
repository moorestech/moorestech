using Core.Item;

namespace Core.Block.BlockInventory
{
    public interface IBlockInventory
    {
        public IItemStack InsertItem(IItemStack itemStack);
        public void AddConnector(IBlockInventory blockInventory);
        public void RemoveConnector(IBlockInventory blockInventory);
    }
}
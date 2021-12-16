using Core.Item;

namespace Core.Block.BlockInventory
{
    public interface IBlockInventory
    {
        public IItemStack InsertItem(IItemStack itemStack);
        public void ChangeConnector(IBlockInventory blockInventory);
    }
}
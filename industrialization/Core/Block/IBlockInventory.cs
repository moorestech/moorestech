using industrialization.Core.Item;

namespace industrialization.Core.Block
{
    public interface IBlockInventory
    {
        public IItemStack InsertItem(IItemStack itemStack);
        public void ChangeConnector(IBlockInventory blockInventory);
    }
}
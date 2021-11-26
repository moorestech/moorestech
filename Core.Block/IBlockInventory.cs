using Core.Item;

namespace Core.Block
{
    public interface IBlockInventory
    {
        public IItemStack InsertItem(IItemStack itemStack);
        public void ChangeConnector(IBlockInventory blockInventory);
        public IItemStack GetItem(int slot);
        public void SetItem(int slot, IItemStack itemStack);
    }
}
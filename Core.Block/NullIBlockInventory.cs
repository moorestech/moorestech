using Core.Item;

namespace Core.Block
{
    public class NullIBlockInventory : IBlockInventory
    {
        public IItemStack InsertItem(IItemStack itemStack)
        {
            return itemStack;
        }

        public void ChangeConnector(IBlockInventory blockInventory)
        {
            
        }

        public IItemStack GetItem(int slot)
        {
            return ItemStackFactory.CreatEmpty();
        }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            return itemStack;
        }
    }
}
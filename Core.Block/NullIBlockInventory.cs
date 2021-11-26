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
            throw new System.NotImplementedException();
        }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            throw new System.NotImplementedException();
        }
    }
}
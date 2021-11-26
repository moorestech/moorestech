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

        public IItemStack ReplaceItem(int index, IItemStack itemStack)
        {
            throw new System.NotImplementedException();
        }
    }
}
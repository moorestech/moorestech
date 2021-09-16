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
    }
}
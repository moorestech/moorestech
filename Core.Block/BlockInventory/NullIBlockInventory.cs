using Core.Item;

namespace Core.Block.BlockInventory
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
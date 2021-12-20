using Core.Item;

namespace Core.Block.BlockInventory
{
    public class NullIBlockInventory : IBlockInventory
    {
        public IItemStack InsertItem(IItemStack itemStack)
        {
            return itemStack;
        }

        public void AddConnector(IBlockInventory blockInventory) { }
        public void RemoveConnector(IBlockInventory blockInventory) { }
    }
}
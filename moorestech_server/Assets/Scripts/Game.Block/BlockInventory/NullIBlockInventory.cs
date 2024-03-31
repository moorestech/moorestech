using Server.Core.Item;

namespace Game.Block.BlockInventory
{
    public class NullIBlockInventory : IBlockInventory
    {
        private readonly ItemStackFactory _itemStackFactory;

        public NullIBlockInventory(ItemStackFactory itemStackFactory)
        {
            _itemStackFactory = itemStackFactory;
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            return itemStack;
        }

        public IItemStack GetItem(int slot)
        {
            return _itemStackFactory.CreatEmpty();
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
        }

        public int GetSlotSize()
        {
            return 0;
        }

        public void AddOutputConnector(IBlockInventory blockInventory)
        {
        }

        public void RemoveOutputConnector(IBlockInventory blockInventory)
        {
        }
    }
}
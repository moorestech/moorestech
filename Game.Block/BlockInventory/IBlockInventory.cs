using Core.Item;

namespace Game.Block.BlockInventory
{
    /// <summary>
    ///     
    ///     
    /// </summary>
    public interface IBlockInventory
    {
        public IItemStack InsertItem(IItemStack itemStack);
        public void AddOutputConnector(IBlockInventory blockInventory);
        public void RemoveOutputConnector(IBlockInventory blockInventory);

        public IItemStack GetItem(int slot);
        void SetItem(int slot, IItemStack itemStack);
        public int GetSlotSize();
    }
}
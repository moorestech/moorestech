using Core.Item.Interface;

namespace Game.Block.Blocks.Connector
{
    public interface IBlockInventoryInserter
    {
        public IItemStack InsertItem(IItemStack itemStack);
    }
}
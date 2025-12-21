using Core.Item.Interface;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.Connector
{
    public interface IBlockInventoryInserter
    {
        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context);
    }
}
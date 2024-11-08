using Core.Item.Interface;

namespace Game.Block.Blocks.BeltConveyor.Connector
{
    public interface IBeltConveyorConnector
    {
        public IItemStack InsertItem(IItemStack itemStack);
    }
}
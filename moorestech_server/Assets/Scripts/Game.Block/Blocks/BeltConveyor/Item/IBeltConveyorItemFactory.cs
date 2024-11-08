using Core.Item.Interface;
using Core.Master;

namespace Game.Block.Blocks.BeltConveyor
{
    public interface IBeltConveyorItemFactory
    {
        public IBeltConveyorInventoryItem CreateItem(ItemId itemId, ItemInstanceId itemInstanceId);
        
        public IBeltConveyorInventoryItem LoadItem(string jsonString);
    }
}
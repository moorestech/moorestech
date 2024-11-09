using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;

namespace Game.CraftChainer.BlockComponent
{
    public class ChainerTransporterItemFactory : IBeltConveyorItemFactory
    {
        public IBeltConveyorInventoryItem CreateItem(ItemId itemId, ItemInstanceId itemInstanceId)
        {
            throw new System.NotImplementedException();
        }
        public IBeltConveyorInventoryItem LoadItem(string jsonString)
        {
            throw new System.NotImplementedException();
        }
    }
}
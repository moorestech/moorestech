using Core.Block.BeltConveyor.Generally;
using Core.Block.BlockInventory;
using Core.Item;

namespace Core.Block.BeltConveyor.util
{
    public static class BeltConveyorFactory
    {
        public static GenericBeltConveyor Create(int blockId,int intId, IBlockInventory connect,ItemStackFactory itemStackFactory)
        {
            return new GenericBeltConveyor(blockId, intId, connect,itemStackFactory);
        } 
    }
}
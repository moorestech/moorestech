using Core.Block.BeltConveyor.Generally;

namespace Core.Block.BeltConveyor.util
{
    public static class BeltConveyorFactory
    {
        public static GenericBeltConveyor Create(int blockId,int intId, IBlockInventory connect)
        {
            return new GenericBeltConveyor(blockId, intId, connect);
        } 
    }
}
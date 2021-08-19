using System;
using industrialization.Core.Block.BeltConveyor.Generally;

namespace industrialization.Core.Block.BeltConveyor.util
{
    public static class BeltConveyorFactory
    {
        public static GenericBeltConveyor Create(int blockId,int intId, IBlockInventory connect)
        {
            return new(blockId, intId, connect);
        } 
    }
}
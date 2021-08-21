using System;
using industrialization.Core.Block.BeltConveyor.Generally;

namespace industrialization.Core.Block.BeltConveyor.util
{
    public static class BeltConveyorFactory
    {
        public static GenericBeltConveyor Create(uint blockId,uint intId, IBlockInventory connect)
        {
            return new GenericBeltConveyor(blockId, intId, connect);
        } 
    }
}
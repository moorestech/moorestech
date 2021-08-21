using System;

namespace industrialization.Core.Block.Machine.util
{
    public static class NormalMachineFactory
    {
        public static NormalMachine Create(uint BlockId,uint intID, IBlockInventory connect)
        {
            return new NormalMachine(BlockId, intID,
                new NormalMachineInputInventory(BlockId),
                new NormalMachineOutputInventory(BlockId,connect));
        }
    }
}
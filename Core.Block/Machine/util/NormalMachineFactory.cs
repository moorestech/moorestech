using Core.Block.Config;

namespace Core.Block.Machine.util
{
    public static class NormalMachineFactory
    {
        public static NormalMachine Create(int BlockId,int intID, IBlockInventory connect,IBlockConfig blockConfig)
        {
            return new NormalMachine(BlockId, intID,
                new NormalMachineInputInventory(BlockId,blockConfig),
                new NormalMachineOutputInventory(BlockId,connect,blockConfig));
        }
    }
}
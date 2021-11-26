namespace Core.Block.Machine.util
{
    public static class NormalMachineFactory
    {
        public static NormalMachine Create(int BlockId,int intID, IBlockInventory connect)
        {
            return new NormalMachine(BlockId, intID,
                new NormalMachineInputInventory(BlockId),
                new NormalMachineOutputInventory(BlockId,connect));
        }
    }
}
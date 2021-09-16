namespace Core.Block.Machine.util
{
    public static class NormalMachineFactory
    {
        public static GeneralMachine Create(int BlockId,int intID, IBlockInventory connect)
        {
            return new GeneralMachine(BlockId, intID,
                new NormalMachineInputInventory(BlockId),
                new NormalMachineOutputInventory(BlockId,connect));
        }
    }
}
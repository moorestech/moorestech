using Core.Block.Config;
using Core.Block.RecipeConfig;

namespace Core.Block.Machine.util
{
    public static class NormalMachineFactory
    {
        public static NormalMachine Create(int BlockId,int intID, IBlockInventory connect,IBlockConfig blockConfig,IMachineRecipeConfig machineRecipeConfig)
        {
            return new NormalMachine(BlockId, intID,
                new NormalMachineInputInventory(BlockId,blockConfig,machineRecipeConfig),
                new NormalMachineOutputInventory(BlockId,connect,blockConfig));
        }
    }
}
using Core.Block.Config;
using Core.Block.RecipeConfig;
using Core.Item;

namespace Core.Block.Machine.util
{
    public static class NormalMachineFactory
    {
        public static NormalMachine Create(int BlockId,int intID, IBlockInventory connect,IBlockConfig blockConfig,IMachineRecipeConfig machineRecipeConfig,ItemStackFactory itemStackFactory)
        {
            return new NormalMachine(BlockId, intID,
                new NormalMachineInputInventory(BlockId,blockConfig,machineRecipeConfig,itemStackFactory),
                new NormalMachineOutputInventory(BlockId,connect,blockConfig,itemStackFactory));
        }
    }
}
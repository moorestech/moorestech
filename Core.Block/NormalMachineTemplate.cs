using Core.Block.Config;
using Core.Block.Machine;
using Core.Block.RecipeConfig;
using Core.Item;
using Core.Item.Config;

namespace Core.Block
{
    public class NormalMachineTemplate : IBlockTemplate
    {
        private readonly IMachineRecipeConfig _machineRecipeConfig;
        private readonly ItemStackFactory _itemStackFactory;

        public NormalMachineTemplate(IMachineRecipeConfig machineRecipeConfig, ItemStackFactory itemStackFactory)
        {
            _machineRecipeConfig = machineRecipeConfig;
            _itemStackFactory = itemStackFactory;
        }

        public IBlock New(BlockConfigData param, int intId)
        {
            var machineParam = param.Param as MachineBlockConfigParam;
            
            return new NormalMachine(param.Id,intId ,
                new NormalMachineInputInventory(param.Id,machineParam.InputSlot,_machineRecipeConfig,_itemStackFactory),
                new NormalMachineOutputInventory(new NullIBlockInventory(),machineParam.OutputSlot,_itemStackFactory));
        }
    }
}
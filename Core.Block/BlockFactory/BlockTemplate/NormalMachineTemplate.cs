using Core.Block.BlockInventory;
using Core.Block.Config;
using Core.Block.Config.LoadConfig;
using Core.Block.Config.Param;
using Core.Block.Machine;
using Core.Block.RecipeConfig;
using Core.Item;

namespace Core.Block.BlockFactory.BlockTemplate
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

        public IBlock Load(BlockConfigData param, int intId, string state)
        {
            var machineParam = param.Param as MachineBlockConfigParam;
            return new NormalMachine(param.Id,intId ,state,
                _itemStackFactory,
                _machineRecipeConfig,
                new NormalMachineInputInventory(param.Id,machineParam.InputSlot,_machineRecipeConfig,_itemStackFactory),
                new NormalMachineOutputInventory(new NullIBlockInventory(),machineParam.OutputSlot,_itemStackFactory));
        }
    }
}
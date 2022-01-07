using Core.Block.BlockInventory;
using Core.Block.Blocks;
using Core.Block.Blocks.Machine;
using Core.Block.Blocks.Machine.Inventory;
using Core.Block.Blocks.Machine.InventoryController;
using Core.Block.Blocks.Machine.SaveLoad;
using Core.Block.Config.LoadConfig;
using Core.Block.Config.LoadConfig.Param;
using Core.Block.RecipeConfig;
using Core.Item;

namespace Core.Block.BlockFactory.BlockTemplate
{
    public class VanillaMachineTemplate : IBlockTemplate
    {
        private readonly IMachineRecipeConfig _machineRecipeConfig;
        private readonly ItemStackFactory _itemStackFactory;

        public VanillaMachineTemplate(IMachineRecipeConfig machineRecipeConfig, ItemStackFactory itemStackFactory)
        {
            _machineRecipeConfig = machineRecipeConfig;
            _itemStackFactory = itemStackFactory;
        }

        public IBlock New(BlockConfigData param, int intId)
        {
            var(input, output, machineParam) = GetData(param);

            var runProcess = new VanillaMachineRunProcess(input, output, _machineRecipeConfig.GetNullRecipeData(),
                machineParam.RequiredPower);

            return new VanillaMachine(param.BlockId, intId,
                new VanillaMachineBlockInventory(input, output),
                new VanillaMachineInventory(input, output),
                new VanillaMachineSave(input, output, runProcess),
                runProcess
            );
        }

        public IBlock Load(BlockConfigData param, int intId, string state)
        {
            var(input, output, machineParam) = GetData(param);

            var runProcess = new VanillaMachineLoad(input, output, _itemStackFactory, _machineRecipeConfig,
                machineParam.RequiredPower).Load(state);


            return new VanillaMachine(param.BlockId, intId,
                new VanillaMachineBlockInventory(input, output),
                new VanillaMachineInventory(input, output),
                new VanillaMachineSave(input, output, runProcess),
                runProcess
            );
        }

        private (VanillaMachineInputInventory, VanillaMachineOutputInventory,MachineBlockConfigParam) GetData(BlockConfigData param)
        {
            var machineParam = param.Param as MachineBlockConfigParam;
            
            var input = new VanillaMachineInputInventory(param.BlockId, machineParam.InputSlot, _machineRecipeConfig,
                _itemStackFactory);

            var output = new VanillaMachineOutputInventory( machineParam.OutputSlot,
                _itemStackFactory);
            
            return (input, output, machineParam);
        }
    }
}
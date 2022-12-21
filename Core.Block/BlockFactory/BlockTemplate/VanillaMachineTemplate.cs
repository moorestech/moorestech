using Core.Block.BlockInventory;
using Core.Block.Blocks;
using Core.Block.Blocks.Machine;
using Core.Block.Blocks.Machine.Inventory;
using Core.Block.Blocks.Machine.InventoryController;
using Core.Block.Blocks.Machine.SaveLoad;
using Core.Block.Config.LoadConfig;
using Core.Block.Config.LoadConfig.Param;
using Core.Block.Event;
using Core.Block.RecipeConfig;
using Core.Item;

namespace Core.Block.BlockFactory.BlockTemplate
{
    public class VanillaMachineTemplate : IBlockTemplate
    {
        private readonly IMachineRecipeConfig _machineRecipeConfig;
        private readonly ItemStackFactory _itemStackFactory;
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdateEvent;

        public VanillaMachineTemplate(IMachineRecipeConfig machineRecipeConfig, ItemStackFactory itemStackFactory, BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            _machineRecipeConfig = machineRecipeConfig;
            _itemStackFactory = itemStackFactory;
            _blockInventoryUpdateEvent = blockInventoryUpdateEvent;
        }

        public IBlock New(BlockConfigData param, int entityId, ulong blockHash)
        {
            var(input, output, machineParam) = GetData(param,entityId);

            var runProcess = new VanillaMachineRunProcess(input, output, _machineRecipeConfig.GetNullRecipeData(),
                machineParam.RequiredPower);

            return new VanillaMachine(param.BlockId, entityId,blockHash,
                new VanillaMachineBlockInventory(input, output),
                new VanillaMachineInventory(input, output),
                new VanillaMachineSave(input, output, runProcess),
                runProcess,
                _itemStackFactory
            );
        }

        public IBlock Load(BlockConfigData param, int entityId, ulong blockHash, string state)
        {
            var(input, output, machineParam) = GetData(param,entityId);

            var runProcess = new VanillaMachineLoad(input, output, _itemStackFactory, _machineRecipeConfig,
                machineParam.RequiredPower).Load(state);


            return new VanillaMachine(param.BlockId, entityId,blockHash,
                new VanillaMachineBlockInventory(input, output),
                new VanillaMachineInventory(input, output),
                new VanillaMachineSave(input, output, runProcess),
                runProcess,
                _itemStackFactory
            );
        }

        private (VanillaMachineInputInventory, VanillaMachineOutputInventory,MachineBlockConfigParam) GetData(BlockConfigData param,int entityId)
        {
            var machineParam = param.Param as MachineBlockConfigParam;
            
            var input = new VanillaMachineInputInventory(
                param.BlockId, machineParam.InputSlot, _machineRecipeConfig, _itemStackFactory, _blockInventoryUpdateEvent,entityId);

            var output = new VanillaMachineOutputInventory(
                machineParam.OutputSlot, _itemStackFactory, _blockInventoryUpdateEvent,entityId,machineParam.InputSlot);
            
            return (input, output, machineParam);
        }
    }
}
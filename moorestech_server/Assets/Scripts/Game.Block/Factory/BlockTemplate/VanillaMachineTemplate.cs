using Core.Item;
using Game.Block.Base;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.InventoryController;
using Game.Block.Blocks.Machine.SaveLoad;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.RecipeConfig;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaMachineTemplate : IBlockTemplate
    {
        public delegate VanillaMachineBase CreateMachine(
            (int blockId, int entityId, long blockHash, VanillaMachineBlockInventory vanillaMachineBlockInventory,
                VanillaMachineSave vanillaMachineSave, VanillaMachineRunProcess vanillaMachineRunProcess,
                ItemStackFactory itemStackFactory) data);

        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdateEvent;
        private readonly CreateMachine _createMachine;
        private readonly ItemStackFactory _itemStackFactory;


        private readonly IMachineRecipeConfig _machineRecipeConfig;


        public VanillaMachineTemplate(IMachineRecipeConfig machineRecipeConfig, ItemStackFactory itemStackFactory,
            BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent, CreateMachine createMachine)
        {
            _machineRecipeConfig = machineRecipeConfig;
            _itemStackFactory = itemStackFactory;
            _blockInventoryUpdateEvent = blockInventoryUpdateEvent;
            _createMachine = createMachine;
        }

        public IBlock New(BlockConfigData param, int entityId, long blockHash)
        {
            var (input, output, machineParam) = GetData(param, entityId);

            var runProcess = new VanillaMachineRunProcess(input, output, _machineRecipeConfig.GetEmptyRecipeData(),
                machineParam.RequiredPower);

            return _createMachine((param.BlockId, entityId, blockHash,
                    new VanillaMachineBlockInventory(input, output),
                    new VanillaMachineSave(input, output, runProcess),
                    runProcess,
                    _itemStackFactory
                ));
        }

        public IBlock Load(BlockConfigData param, int entityId, long blockHash, string state)
        {
            var (input, output, machineParam) = GetData(param, entityId);

            var runProcess = new VanillaMachineLoad(input, output, _itemStackFactory, _machineRecipeConfig,
                machineParam.RequiredPower).LoadVanillaMachineRunProcess(state);


            return _createMachine((param.BlockId, entityId, blockHash,
                    new VanillaMachineBlockInventory(input, output),
                    new VanillaMachineSave(input, output, runProcess),
                    runProcess,
                    _itemStackFactory
                ));
        }

        private (VanillaMachineInputInventory, VanillaMachineOutputInventory, MachineBlockConfigParam) GetData(
            BlockConfigData param, int entityId)
        {
            var machineParam = param.Param as MachineBlockConfigParam;

            var input = new VanillaMachineInputInventory(
                param.BlockId, machineParam.InputSlot, _machineRecipeConfig, _itemStackFactory,
                _blockInventoryUpdateEvent, entityId);

            var output = new VanillaMachineOutputInventory(
                machineParam.OutputSlot, _itemStackFactory, _blockInventoryUpdateEvent, entityId,
                machineParam.InputSlot);

            return (input, output, machineParam);
        }
    }
}
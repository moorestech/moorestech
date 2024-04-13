using Core.Item.Interface;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.InventoryController;
using Game.Block.Blocks.Machine.SaveLoad;
using Game.Block.Component;
using Game.Block.Component.IOConnector;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Context;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaMachineTemplate : IBlockTemplate
    {
        //TODO こういうダルいところ整理したい 全部コンポーネントにするのはアリ
        public delegate VanillaMachineBase CreateMachine(
            (int blockId, int entityId, long blockHash, VanillaMachineBlockInventory vanillaMachineBlockInventory,
                VanillaMachineSave vanillaMachineSave, VanillaMachineRunProcess vanillaMachineRunProcess,
                BlockPositionInfo blockPositionInfo, InventoryInputConnectorComponent inputConnectorComponent) data);

        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdateEvent;
        private readonly CreateMachine _createMachine;

        public VanillaMachineTemplate(BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent, CreateMachine createMachine)
        {
            _blockInventoryUpdateEvent = blockInventoryUpdateEvent;
            _createMachine = createMachine;
        }

        public IBlock New(BlockConfigData param, int entityId, long blockHash, BlockPositionInfo blockPositionInfo)
        {
            var inputConnectorComponent = CreateInputConnector(blockPositionInfo);
            var (input, output, machineParam) = GetDependencies(param, entityId, inputConnectorComponent);

            var emptyRecipe = ServerContext.MachineRecipeConfig.GetEmptyRecipeData();
            var runProcess = new VanillaMachineRunProcess(input, output, emptyRecipe, machineParam.RequiredPower);

            return _createMachine((param.BlockId, entityId, blockHash,
                    new VanillaMachineBlockInventory(input, output),
                    new VanillaMachineSave(input, output, runProcess),
                    runProcess,
                    blockPositionInfo,
                    inputConnectorComponent
                ));
        }

        public IBlock Load(BlockConfigData param, int entityId, long blockHash, string state, BlockPositionInfo blockPositionInfo)
        {
            var inputConnectorComponent = CreateInputConnector(blockPositionInfo);
            var (input, output, machineParam) = GetDependencies(param, entityId, inputConnectorComponent);

            var runProcess = new VanillaMachineLoad(input, output, machineParam.RequiredPower).LoadVanillaMachineRunProcess(state);

            return _createMachine((param.BlockId, entityId, blockHash,
                    new VanillaMachineBlockInventory(input, output),
                    new VanillaMachineSave(input, output, runProcess),
                    runProcess,
                    blockPositionInfo,
                    inputConnectorComponent
                ));
        }

        private (VanillaMachineInputInventory, VanillaMachineOutputInventory, MachineBlockConfigParam) GetDependencies(BlockConfigData param, int entityId, InventoryInputConnectorComponent inventoryInputConnectorComponent)
        {
            var machineParam = param.Param as MachineBlockConfigParam;

            var input = new VanillaMachineInputInventory(
                param.BlockId, machineParam.InputSlot,
                _blockInventoryUpdateEvent, entityId);

            var output = new VanillaMachineOutputInventory(
                machineParam.OutputSlot, ServerContext.ItemStackFactory, _blockInventoryUpdateEvent, entityId,
                machineParam.InputSlot, inventoryInputConnectorComponent);

            return (input, output, machineParam);
        }

        private InventoryInputConnectorComponent CreateInputConnector(BlockPositionInfo blockPositionInfo)
        {
            return new InventoryInputConnectorComponent(new IOConnectionSetting(
                new ConnectDirection[] { new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0) },
                new ConnectDirection[] { new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0) },
                new[] { VanillaBlockType.BeltConveyor }), blockPositionInfo);
        }
    }
}
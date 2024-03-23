using Core.Item;
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
using Game.Block.Interface.RecipeConfig;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaMachineTemplate : IBlockTemplate
    {
        //TODO こういうダルいところ整理したい 全部コンポーネントにするのはアリ
        public delegate VanillaMachineBase CreateMachine(
            (int blockId, int entityId, long blockHash, VanillaMachineBlockInventory vanillaMachineBlockInventory,
                VanillaMachineSave vanillaMachineSave, VanillaMachineRunProcess vanillaMachineRunProcess,
                ItemStackFactory itemStackFactory, BlockPositionInfo blockPositionInfo, InputConnectorComponent inputConnectorComponent) data);

        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdateEvent;
        private readonly ComponentFactory _componentFactory;
        private readonly CreateMachine _createMachine;
        private readonly ItemStackFactory _itemStackFactory;


        private readonly IMachineRecipeConfig _machineRecipeConfig;


        public VanillaMachineTemplate(IMachineRecipeConfig machineRecipeConfig, ItemStackFactory itemStackFactory,
            BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent, CreateMachine createMachine, ComponentFactory componentFactory)
        {
            _machineRecipeConfig = machineRecipeConfig;
            _itemStackFactory = itemStackFactory;
            _blockInventoryUpdateEvent = blockInventoryUpdateEvent;
            _createMachine = createMachine;
            _componentFactory = componentFactory;
        }

        public IBlock New(BlockConfigData param, int entityId, long blockHash, BlockPositionInfo blockPositionInfo)
        {
            var inputConnectorComponent = CreateInputConnector(blockPositionInfo);
            var (input, output, machineParam) = GetDependencies(param, entityId, inputConnectorComponent);

            var runProcess = new VanillaMachineRunProcess(input, output, _machineRecipeConfig.GetEmptyRecipeData(), machineParam.RequiredPower);

            return _createMachine((param.BlockId, entityId, blockHash,
                    new VanillaMachineBlockInventory(input, output),
                    new VanillaMachineSave(input, output, runProcess),
                    runProcess,
                    _itemStackFactory,
                    blockPositionInfo,
                    inputConnectorComponent
                ));
        }

        public IBlock Load(BlockConfigData param, int entityId, long blockHash, string state, BlockPositionInfo blockPositionInfo)
        {
            var inputConnectorComponent = CreateInputConnector(blockPositionInfo);
            var (input, output, machineParam) = GetDependencies(param, entityId, inputConnectorComponent);

            var runProcess = new VanillaMachineLoad(input, output, _itemStackFactory, _machineRecipeConfig, machineParam.RequiredPower).LoadVanillaMachineRunProcess(state);

            return _createMachine((param.BlockId, entityId, blockHash,
                    new VanillaMachineBlockInventory(input, output),
                    new VanillaMachineSave(input, output, runProcess),
                    runProcess,
                    _itemStackFactory,
                    blockPositionInfo,
                    inputConnectorComponent
                ));
        }

        private (VanillaMachineInputInventory, VanillaMachineOutputInventory, MachineBlockConfigParam) GetDependencies(BlockConfigData param, int entityId, InputConnectorComponent inputConnectorComponent)
        {
            var machineParam = param.Param as MachineBlockConfigParam;

            var input = new VanillaMachineInputInventory(
                param.BlockId, machineParam.InputSlot, _machineRecipeConfig, _itemStackFactory,
                _blockInventoryUpdateEvent, entityId);

            var output = new VanillaMachineOutputInventory(
                machineParam.OutputSlot, _itemStackFactory, _blockInventoryUpdateEvent, entityId,
                machineParam.InputSlot, inputConnectorComponent);

            return (input, output, machineParam);
        }

        private InputConnectorComponent CreateInputConnector(BlockPositionInfo blockPositionInfo)
        {
            return _componentFactory.CreateInputConnectorComponent(blockPositionInfo,
                new IOConnectionSetting(
                    new ConnectDirection[] { new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0) },
                    new ConnectDirection[] { new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0) },
                    new[] { VanillaBlockType.BeltConveyor }));
        }
    }
}
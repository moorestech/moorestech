using System.Collections.Generic;
using Core.Item.Interface;
using Game.Block.Blocks;
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
using Game.Block.Interface.Component;
using Game.Context;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaMachineTemplate : IBlockTemplate
    {
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdateEvent;

        public VanillaMachineTemplate(BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            _blockInventoryUpdateEvent = blockInventoryUpdateEvent;
        }

        public IBlock New(BlockConfigData param, int entityId, long blockHash, BlockPositionInfo blockPositionInfo)
        {
            var inputConnectorComponent = CreateInputConnector(blockPositionInfo);
            var (input, output, machineParam) = GetDependencies(param, entityId, inputConnectorComponent);

            var emptyRecipe = ServerContext.MachineRecipeConfig.GetEmptyRecipeData();
            var runProcess = new VanillaMachineRunProcess(input, output, emptyRecipe, machineParam.RequiredPower);

            var blockInventory = new VanillaMachineBlockInventory(input, output);
            var machineSave = new VanillaMachineSave(input, output, runProcess);
            var machineComponent = new VanillaElectricMachineComponent(entityId, blockInventory,machineSave,runProcess);

            var components = new List<IBlockComponent>()
            {
                machineComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(entityId, param.BlockId, components, blockPositionInfo);
        }

        public IBlock Load(BlockConfigData param, int entityId, long blockHash, string state, BlockPositionInfo blockPositionInfo)
        {
            var inputConnectorComponent = CreateInputConnector(blockPositionInfo);
            var (input, output, machineParam) = GetDependencies(param, entityId, inputConnectorComponent);

            var runProcess = new VanillaMachineLoad(input, output, machineParam.RequiredPower).LoadVanillaMachineRunProcess(state);

            var blockInventory = new VanillaMachineBlockInventory(input, output);
            var machineSave = new VanillaMachineSave(input, output, runProcess);
            var machineComponent = new VanillaElectricMachineComponent(entityId, blockInventory,machineSave,runProcess);

            var components = new List<IBlockComponent>()
            {
                machineComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(entityId, param.BlockId, components, blockPositionInfo);
        }

        private (VanillaMachineInputInventory, VanillaMachineOutputInventory, MachineBlockConfigParam) GetDependencies(BlockConfigData param, int entityId, BlockConnectorComponent<IBlockInventory> blockConnectorComponent)
        {
            var machineParam = param.Param as MachineBlockConfigParam;

            var input = new VanillaMachineInputInventory(
                param.BlockId, machineParam.InputSlot,
                _blockInventoryUpdateEvent, entityId);

            var output = new VanillaMachineOutputInventory(
                machineParam.OutputSlot, ServerContext.ItemStackFactory, _blockInventoryUpdateEvent, entityId,
                machineParam.InputSlot, blockConnectorComponent);

            return (input, output, machineParam);
        }

        private BlockConnectorComponent<IBlockInventory> CreateInputConnector(BlockPositionInfo blockPositionInfo)
        {
            return new BlockConnectorComponent<IBlockInventory>(new IOConnectionSetting(
                new ConnectDirection[] { new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0) },
                new ConnectDirection[] { new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0) },
                new[] { VanillaBlockType.BeltConveyor }), blockPositionInfo);
        }
    }
}
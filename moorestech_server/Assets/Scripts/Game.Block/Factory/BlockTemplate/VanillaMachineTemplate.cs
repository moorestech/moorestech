using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.SaveLoad;
using Game.Block.Component;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Event;
using Game.Block.Factory.Extension;
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
        
        public IBlock New(BlockConfigData config, int entityId, BlockPositionInfo blockPositionInfo)
        {
            var inputConnectorComponent = config.CreateInventoryConnector(blockPositionInfo);
            var (input, output, machineParam) = GetDependencies(config, entityId, inputConnectorComponent);
            
            var emptyRecipe = ServerContext.MachineRecipeConfig.GetEmptyRecipeData();
            var runProcess = new VanillaMachineRunProcess(input, output, emptyRecipe, machineParam.RequiredPower);
            
            var blockInventory = new VanillaMachineBlockInventory(input, output);
            var machineSave = new VanillaMachineSave(input, output, runProcess);
            var machineComponent = new VanillaElectricMachineComponent(entityId, blockInventory, machineSave, runProcess);
            
            var components = new List<IBlockComponent>
            {
                machineComponent,
                inputConnectorComponent
            };
            
            return new BlockSystem(entityId, config.BlockId, components, blockPositionInfo);
        }
        
        public IBlock Load(string state, BlockConfigData config, int entityId, BlockPositionInfo blockPositionInfo)
        {
            var inputConnectorComponent = config.CreateInventoryConnector(blockPositionInfo);
            var (input, output, machineParam) = GetDependencies(config, entityId, inputConnectorComponent);
            
            var runProcess = new VanillaMachineLoad(input, output, machineParam.RequiredPower).LoadVanillaMachineRunProcess(state);
            
            var blockInventory = new VanillaMachineBlockInventory(input, output);
            var machineSave = new VanillaMachineSave(input, output, runProcess);
            var machineComponent = new VanillaElectricMachineComponent(entityId, blockInventory, machineSave, runProcess);
            
            var components = new List<IBlockComponent>
            {
                machineComponent,
                inputConnectorComponent
            };
            
            return new BlockSystem(entityId, config.BlockId, components, blockPositionInfo);
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
    }
}
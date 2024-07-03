using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Component;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Event;
using Game.Block.Factory.Extension;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Component;
using Game.Context;
using Game.Gear.Common;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaGearMachineTemplate : IBlockTemplate
    {
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdateEvent;
        
        public VanillaGearMachineTemplate(BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            _blockInventoryUpdateEvent = blockInventoryUpdateEvent;
        }
        
        public IBlock New(BlockConfigData config, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            BlockConnectorComponent<IBlockInventory> inputConnectorComponent = config.CreateInventoryConnector(blockPositionInfo);
            var (input, output) = VanillaMachineTemplate.GetDependencies(config, blockInstanceId, inputConnectorComponent, _blockInventoryUpdateEvent);
            var machineParam = (GearMachineConfigParam)config.Param;
            
            var connectSetting = machineParam.GearConnectSettings;
            var gearConnector = new BlockConnectorComponent<IGearEnergyTransformer>(connectSetting, connectSetting, blockPositionInfo);
            
            var emptyRecipe = ServerContext.MachineRecipeConfig.GetEmptyRecipeData();
            var processor = new VanillaMachineProcessorComponent(input, output, emptyRecipe, machineParam.RequiredPower);
            
            var blockInventory = new VanillaMachineBlockInventoryComponent(input, output);
            var machineSave = new VanillaMachineSaveComponent(input, output, processor);
            
            var machineComponent = new VanillaGearMachineComponent(machineParam, processor, gearConnector, blockInstanceId);
            
            var components = new List<IBlockComponent>
            {
                blockInventory,
                machineSave,
                processor,
                machineComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, config.BlockId, components, blockPositionInfo);
        }
        
        public IBlock Load(string state, BlockConfigData config, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var inputConnectorComponent = config.CreateInventoryConnector(blockPositionInfo);
            var (input, output) = VanillaMachineTemplate.GetDependencies(config, blockInstanceId, inputConnectorComponent, _blockInventoryUpdateEvent);
            var machineParam = (GearMachineConfigParam)config.Param;
            
            var vanillaGearParam = config.Param as GearMachineConfigParam;
            var connectSetting = vanillaGearParam.GearConnectSettings;
            
            var gearConnector = new BlockConnectorComponent<IGearEnergyTransformer>(connectSetting, connectSetting, blockPositionInfo);
            
            var processor = VanillaMachineTemplate.LoadState(state, input, output, machineParam.RequiredPower);
            
            var blockInventory = new VanillaMachineBlockInventoryComponent(input, output);
            var machineSave = new VanillaMachineSaveComponent(input, output, processor);
            
            var machineComponent = new VanillaGearMachineComponent(vanillaGearParam, processor, gearConnector, blockInstanceId);
            
            var components = new List<IBlockComponent>
            {
                blockInventory,
                machineSave,
                processor,
                machineComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, config.BlockId, components, blockPositionInfo);
        }
    }
}
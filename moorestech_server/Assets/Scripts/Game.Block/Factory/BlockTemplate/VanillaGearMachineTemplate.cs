using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.EnergySystem;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaGearMachineTemplate : IBlockTemplate
    {
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdateEvent;
        
        public VanillaGearMachineTemplate(BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            _blockInventoryUpdateEvent = blockInventoryUpdateEvent;
        }
        
        public IBlock New(BlockElement blockElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var machineParam = blockElement.BlockParam as GearMachineBlockParam;
            var inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(machineParam.InventoryConnectors, blockPositionInfo);
            
            var inputSlot = machineParam.InputSlot;
            var outputSlot = machineParam.OutputSlot;
            var blockId = BlockMaster.GetItemId(blockElement.BlockId);
            var (input, output) = BlockTemplateUtil.GetMachineIOInventory(blockId, blockInstanceId, inputSlot, outputSlot, inputConnectorComponent, _blockInventoryUpdateEvent);
            
            var connectSetting = machineParam.GearConnects;
            var gearConnector = new BlockConnectorComponent<IGearEnergyTransformer>(connectSetting, connectSetting, blockPositionInfo);
            
            var emptyRecipe = ServerContext.MachineRecipeConfig.GetEmptyRecipeData();
            var requirePower = new ElectricPower(machineParam.RequireTorque * machineParam.RequiredRpm);
            var processor = new VanillaMachineProcessorComponent(input, output, emptyRecipe, requirePower);
            
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
            
            return new BlockSystem(blockInstanceId, blockElement.BlockId, components, blockPositionInfo);
        }
        
        public IBlock Load(string state, BlockElement blockElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var machineParam = blockElement.BlockParam as GearMachineBlockParam;
            var inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(machineParam.InventoryConnectors, blockPositionInfo);
            
            var inputSlot = machineParam.InputSlot;
            var outputSlot = machineParam.OutputSlot;
            var blockId = BlockMaster.GetItemId(blockElement.BlockId);
            var (input, output) = BlockTemplateUtil.GetMachineIOInventory(blockId, blockInstanceId, inputSlot, outputSlot, inputConnectorComponent, _blockInventoryUpdateEvent);
            
            var connectSetting = machineParam.GearConnects;
            var gearConnector = new BlockConnectorComponent<IGearEnergyTransformer>(connectSetting, connectSetting, blockPositionInfo);
            
            var requirePower = new ElectricPower(machineParam.RequireTorque * machineParam.RequiredRpm);
            
            // パラメーターをロード
            // Load the parameters
            var processor = BlockTemplateUtil.MachineLoadState(state, input, output, requirePower);
            
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
            
            return new BlockSystem(blockInstanceId, blockElement.BlockId, components, blockPositionInfo);
        }
    }
}
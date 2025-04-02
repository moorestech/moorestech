using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaMachineTemplate : IBlockTemplate
    {
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdateEvent;
        
        public VanillaMachineTemplate(BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            _blockInventoryUpdateEvent = blockInventoryUpdateEvent;
        }
        
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var machineParam = blockMasterElement.BlockParam as ElectricMachineBlockParam;
            
            BlockConnectorComponent<IBlockInventory> inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(machineParam.InventoryConnectors, blockPositionInfo);
            
            var blockId = MasterHolder.BlockMaster.GetBlockId(blockMasterElement.BlockGuid);
            var (input, output, fluidInput, fluidOutput) = BlockTemplateUtil.GetMachineIOInventory(blockId, blockInstanceId, machineParam, inputConnectorComponent, _blockInventoryUpdateEvent);
            
            var processor = new VanillaMachineProcessorComponent(input, output, fluidInput, fluidOutput, null, new ElectricPower(machineParam.RequiredPower));
            
            var blockInventory = new VanillaMachineBlockInventoryComponent(input, output);
            var machineSave = new VanillaMachineSaveComponent(input, output, processor);
            var machineComponent = new VanillaElectricMachineComponent(blockInstanceId, processor);
            
            var components = new List<IBlockComponent>
            {
                blockInventory,
                machineSave,
                processor,
                machineComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
        
        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var machineParam = blockMasterElement.BlockParam as ElectricMachineBlockParam;
            
            BlockConnectorComponent<IBlockInventory> inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(machineParam.InventoryConnectors, blockPositionInfo);
            var blockId = MasterHolder.BlockMaster.GetBlockId(blockMasterElement.BlockGuid);
            var (input, output, fluidInput, fluidOutput) = BlockTemplateUtil.GetMachineIOInventory(blockId, blockInstanceId, machineParam, inputConnectorComponent, _blockInventoryUpdateEvent);
            
            var processor = BlockTemplateUtil.MachineLoadState(componentStates, input, output, new ElectricPower(machineParam.RequiredPower));
            
            var blockInventory = new VanillaMachineBlockInventoryComponent(input, output);
            var machineSave = new VanillaMachineSaveComponent(input, output, processor);
            var machineComponent = new VanillaElectricMachineComponent(blockInstanceId, processor);
            
            var components = new List<IBlockComponent>
            {
                blockInventory,
                machineSave,
                processor,
                machineComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
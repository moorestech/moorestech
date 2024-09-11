using System.Collections.Generic;
using System.Linq;
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
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaMachineTemplate : IBlockTemplate
    {
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdateEvent;
        
        public VanillaMachineTemplate(BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            _blockInventoryUpdateEvent = blockInventoryUpdateEvent;
        }
        
        public IBlock New(BlockElement blockElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var machineParam = blockElement.BlockParam as ElectricMachineBlockParam;
            
            var inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(machineParam.InventoryConnectors, blockPositionInfo);
            
            var inputSlot = machineParam.InputItemSlotCount;
            var outputSlot = machineParam.OutputItemSlotCount;
            var blockId = MasterHolder.BlockMaster.GetBlockId(blockElement.BlockGuid);
            var (input, output) = BlockTemplateUtil.GetMachineIOInventory(blockId, blockInstanceId, inputSlot, outputSlot, inputConnectorComponent, _blockInventoryUpdateEvent);

            var processor = new VanillaMachineProcessorComponent(input, output, null, new ElectricPower(machineParam.RequiredPower));
            
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
            
            return new BlockSystem(blockInstanceId, blockElement.BlockGuid, components, blockPositionInfo);
        }
        
        public IBlock Load(string state, BlockElement blockElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var machineParam = blockElement.BlockParam as ElectricMachineBlockParam;
            
            var inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(machineParam.InventoryConnectors, blockPositionInfo);
            var inputSlot = machineParam.InputItemSlotCount;
            var outputSlot = machineParam.OutputItemSlotCount;
            var blockId = MasterHolder.BlockMaster.GetBlockId(blockElement.BlockGuid);
            var (input, output) = BlockTemplateUtil.GetMachineIOInventory(blockId, blockInstanceId, inputSlot, outputSlot, inputConnectorComponent, _blockInventoryUpdateEvent);
            
            var processor = BlockTemplateUtil.MachineLoadState(state, input, output, new ElectricPower(machineParam.RequiredPower));
            
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
            
            return new BlockSystem(blockInstanceId, blockElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
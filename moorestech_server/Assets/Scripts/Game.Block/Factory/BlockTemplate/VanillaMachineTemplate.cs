using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks;
using Game.Block.Blocks.Fluid;
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
            var (input, output) = BlockTemplateUtil.GetMachineIOInventory(blockId, blockInstanceId, machineParam, inputConnectorComponent, _blockInventoryUpdateEvent);
            
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
            
            // 流体接続のサポートを追加（流体インベントリコネクタが定義されている場合）
            if (machineParam.FluidInventoryConnectors != null && (machineParam.InputTankCount > 0 || machineParam.OutputTankCount > 0))
            {
                var fluidConnector = IFluidInventory.CreateFluidInventoryConnector(machineParam.FluidInventoryConnectors, blockPositionInfo);
                var fluidInventory = new VanillaMachineFluidInventoryComponent(
                    input,
                    output,
                    fluidConnector
                );
                
                components.Add(fluidConnector);
                components.Add(fluidInventory);
            }
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
        
        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var machineParam = blockMasterElement.BlockParam as ElectricMachineBlockParam;
            
            BlockConnectorComponent<IBlockInventory> inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(machineParam.InventoryConnectors, blockPositionInfo);
            var blockId = MasterHolder.BlockMaster.GetBlockId(blockMasterElement.BlockGuid);
            var (input, output) = BlockTemplateUtil.GetMachineIOInventory(blockId, blockInstanceId, machineParam, inputConnectorComponent, _blockInventoryUpdateEvent);
            
            var processor = BlockTemplateUtil.MachineLoadState(componentStates, input, output, new ElectricPower(machineParam.RequiredPower), blockMasterElement);
            
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
            
            // 流体接続のサポートを追加（流体インベントリコネクタが定義されている場合）
            if (machineParam.FluidInventoryConnectors != null && (machineParam.InputTankCount > 0 || machineParam.OutputTankCount > 0))
            {
                var fluidConnector = IFluidInventory.CreateFluidInventoryConnector(machineParam.FluidInventoryConnectors, blockPositionInfo);
                var fluidInventory = new VanillaMachineFluidInventoryComponent(
                    input,
                    output,
                    fluidConnector
                );
                
                components.Add(fluidConnector);
                components.Add(fluidInventory);
            }
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
        
    }
}
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks;
using Game.Block.Blocks.ElectricWire;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;
using Game.Block.Interface.Component.ConnectJudge;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaMachineTemplate : IBlockTemplate
    {
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdateEvent;
        
        public VanillaMachineTemplate(BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            _blockInventoryUpdateEvent = blockInventoryUpdateEvent;
        }
        
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            var machineParam = blockMasterElement.BlockParam as ElectricMachineBlockParam;
            
            BlockConnectorComponent<IBlockInventory, DefaultConnectJudge> inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(machineParam.InventoryConnectors, blockPositionInfo);
            
            var blockId = MasterHolder.BlockMaster.GetBlockId(blockMasterElement.BlockGuid);
            var (input, output, module) = BlockTemplateUtil.GetMachineIOInventory(blockId, blockInstanceId, machineParam, inputConnectorComponent, _blockInventoryUpdateEvent);

            var effectComponent = new MachineModuleEffectComponent(module);
            var processor = new VanillaMachineProcessorComponent(input, output, machineParam.RequiredPower, machineParam.IdlePowerRate, effectComponent);

            var blockInventory = new VanillaMachineBlockInventoryComponent(input, output, module);
            var machineSave = new VanillaMachineSaveComponent(input, output, module, processor);
            var machineComponent = new VanillaElectricMachineComponent(blockInstanceId, processor);
            // 機械はConsumer役をワイヤー端点に渡す
            // Machine passes the consumer role to the wire endpoint
            var wireConnector = new ElectricWireConnectorComponent(machineParam.MaxWireConnectionCount, machineParam.MaxWireLength, blockInstanceId, machineComponent, null, null, null);

            var components = new List<IBlockComponent>
            {
                blockInventory,
                machineSave,
                effectComponent,
                processor,
                machineComponent,
                inputConnectorComponent,
                wireConnector,
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
            
            BlockConnectorComponent<IBlockInventory, DefaultConnectJudge> inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(machineParam.InventoryConnectors, blockPositionInfo);
            var blockId = MasterHolder.BlockMaster.GetBlockId(blockMasterElement.BlockGuid);
            var (input, output, module) = BlockTemplateUtil.GetMachineIOInventory(blockId, blockInstanceId, machineParam, inputConnectorComponent, _blockInventoryUpdateEvent);

            var effectComponent = new MachineModuleEffectComponent(module);
            var processor = BlockTemplateUtil.MachineLoadState(componentStates, input, output, module, effectComponent, machineParam.RequiredPower, machineParam.IdlePowerRate, blockMasterElement);

            var blockInventory = new VanillaMachineBlockInventoryComponent(input, output, module);
            var machineSave = new VanillaMachineSaveComponent(input, output, module, processor);
            var machineComponent = new VanillaElectricMachineComponent(blockInstanceId, processor);
            // 機械はConsumer役をワイヤー端点に渡す
            // Machine passes the consumer role to the wire endpoint
            var wireConnector = new ElectricWireConnectorComponent(machineParam.MaxWireConnectionCount, machineParam.MaxWireLength, blockInstanceId, machineComponent, null, null, componentStates);

            var components = new List<IBlockComponent>
            {
                blockInventory,
                machineSave,
                effectComponent,
                processor,
                machineComponent,
                inputConnectorComponent,
                wireConnector,
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

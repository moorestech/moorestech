using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks;
using Game.Block.Blocks.CleanRoom.Machine;
using Game.Block.Blocks.ElectricWire;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaCleanRoomMachineTemplate : IBlockTemplate
    {
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdateEvent;

        public VanillaCleanRoomMachineTemplate(BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            _blockInventoryUpdateEvent = blockInventoryUpdateEvent;
        }

        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return Create(null, blockMasterElement, blockInstanceId, blockPositionInfo, _blockInventoryUpdateEvent);
        }

        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return Create(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo, _blockInventoryUpdateEvent);
        }

        private static IBlock Create(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            var machineParam = blockMasterElement.BlockParam as CleanRoomMachineBlockParam;
            var inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(machineParam.InventoryConnectors, blockPositionInfo);

            // 通常機械と同じ入出力・モジュール構成を作り、清浄室専用Processorへ渡す
            // Build the same IO/module inventories as normal machines and pass them to the clean-room processor
            var blockId = MasterHolder.BlockMaster.GetBlockId(blockMasterElement.BlockGuid);
            var (input, output, module) = BlockTemplateUtil.GetMachineIOInventory(blockId, blockInstanceId, machineParam, inputConnectorComponent, blockInventoryUpdateEvent);
            var effectComponent = new MachineModuleEffectComponent(module);
            var processor = new CleanRoomMachineProcessorComponent(componentStates, blockInstanceId, input, output, module, machineParam.RequiredPower, machineParam.IdlePowerRate, effectComponent);

            // CleanRoomMachineComponentは汚染判定と電力Consumerを兼ねる
            // CleanRoomMachineComponent serves both pollution reporting and the electric consumer role
            var blockInventory = new VanillaMachineBlockInventoryComponent(input, output, module);
            var machineComponent = new CleanRoomMachineComponent(blockInstanceId, processor);
            var wireConnector = new ElectricWireConnectorComponent(machineParam.MaxWireConnectionCount, machineParam.MaxWireLength, blockInstanceId, machineComponent, componentStates);

            var components = new List<IBlockComponent>
            {
                blockInventory,
                effectComponent,
                processor,
                machineComponent,
                inputConnectorComponent,
                wireConnector,
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}

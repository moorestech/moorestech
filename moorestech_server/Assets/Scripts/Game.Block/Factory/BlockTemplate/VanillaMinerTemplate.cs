using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks;
using Game.Block.Blocks.Miner;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaMinerTemplate : IBlockTemplate
    {
        private readonly BlockOpenableInventoryUpdateEvent _blockOpenableInventoryUpdateEvent;
        
        public VanillaMinerTemplate(BlockOpenableInventoryUpdateEvent blockOpenableInventoryUpdateEvent)
        {
            _blockOpenableInventoryUpdateEvent = blockOpenableInventoryUpdateEvent;
        }
        
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var (requestPower, outputSlot) = GetData(blockMasterElement);
            
            var minerParam = blockMasterElement.BlockParam as ElectricMinerBlockParam;
            var inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(minerParam.InventoryConnectors, blockPositionInfo);
            var blockId = MasterHolder.BlockMaster.GetBlockId(blockMasterElement.BlockGuid);
            
            var minerProcessorComponent = new VanillaMinerProcessorComponent(blockId, blockInstanceId, requestPower, outputSlot, _blockOpenableInventoryUpdateEvent, inputConnectorComponent, blockPositionInfo);
            var electricMinerComponent = new VanillaElectricMinerComponent(blockInstanceId, requestPower, minerProcessorComponent);
            var components = new List<IBlockComponent>
            {
                minerProcessorComponent,
                electricMinerComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
        
        public IBlock Load(string state, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var (requestPower, outputSlot) = GetData(blockMasterElement);
            
            var minerParam = blockMasterElement.BlockParam as ElectricMinerBlockParam;
            var inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(minerParam.InventoryConnectors, blockPositionInfo);
            var blockId = MasterHolder.BlockMaster.GetBlockId(blockMasterElement.BlockGuid);
            
            var minerProcessorComponent = new VanillaMinerProcessorComponent(state, blockId, blockInstanceId, requestPower, outputSlot, _blockOpenableInventoryUpdateEvent, inputConnectorComponent, blockPositionInfo);
            var electricMinerComponent = new VanillaElectricMinerComponent(blockInstanceId, requestPower, minerProcessorComponent);
            var components = new List<IBlockComponent>
            {
                minerProcessorComponent,
                electricMinerComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
        
        private (ElectricPower requestPower, int outputSlot) GetData(BlockMasterElement blockMasterElement)
        {
            var minerParam = blockMasterElement.BlockParam as ElectricMinerBlockParam;
            
            var requestPower = minerParam.RequiredPower;
            
            return (new ElectricPower(requestPower), minerParam.OutputItemSlotCount);
        }
    }
}
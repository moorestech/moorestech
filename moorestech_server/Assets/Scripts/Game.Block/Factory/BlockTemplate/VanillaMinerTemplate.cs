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
        
        public IBlock New(BlockElement blockElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var (requestPower, outputSlot) = GetData(blockElement);
            
            var minerParam = blockElement.BlockParam as ElectricMinerBlockParam;
            var inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(minerParam.InventoryConnectors, blockPositionInfo);
            var blockId = MasterHolder.BlockMaster.GetBlockId(blockElement.BlockGuid);
            
            var minerComponent = new VanillaElectricMinerComponent(blockId, blockInstanceId, requestPower, outputSlot, _blockOpenableInventoryUpdateEvent, inputConnectorComponent, blockPositionInfo);
            var components = new List<IBlockComponent>
            {
                minerComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockElement.BlockGuid, components, blockPositionInfo);
        }
        
        public IBlock Load(string state, BlockElement blockElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var (requestPower, outputSlot) = GetData(blockElement);
            
            var minerParam = blockElement.BlockParam as ElectricMinerBlockParam;
            var inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(minerParam.InventoryConnectors, blockPositionInfo);
            var blockId = MasterHolder.BlockMaster.GetBlockId(blockElement.BlockGuid);
            
            var minerComponent = new VanillaElectricMinerComponent(state, blockId, blockInstanceId, requestPower, outputSlot, _blockOpenableInventoryUpdateEvent, inputConnectorComponent, blockPositionInfo);
            var components = new List<IBlockComponent>
            {
                minerComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockElement.BlockGuid, components, blockPositionInfo);
        }
        
        private (ElectricPower requestPower, int outputSlot) GetData(BlockElement blockElement)
        {
            var minerParam = blockElement.BlockParam as ElectricMinerBlockParam;
            
            var requestPower = minerParam.RequiredPower;
            
            return (new ElectricPower(requestPower), minerParam.OutputItemSlotCount);
        }
    }
}
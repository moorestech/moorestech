using System.Collections.Generic;
using Core.Const;
using Game.Block.Blocks;
using Game.Block.Blocks.Miner;
using Game.Block.Component;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Event;
using Game.Block.Factory.Extension;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Component;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaMinerTemplate : IBlockTemplate
    {
        private readonly BlockOpenableInventoryUpdateEvent _blockOpenableInventoryUpdateEvent;
        
        public VanillaMinerTemplate(BlockOpenableInventoryUpdateEvent blockOpenableInventoryUpdateEvent)
        {
            _blockOpenableInventoryUpdateEvent = blockOpenableInventoryUpdateEvent;
        }
        
        public IBlock New(BlockConfigData config, EntityID entityId, BlockPositionInfo blockPositionInfo)
        {
            var (requestPower, outputSlot) = GetData(config);
            
            BlockConnectorComponent<IBlockInventory> inputConnectorComponent = config.CreateInventoryConnector(blockPositionInfo);
            var minerComponent = new VanillaElectricMinerComponent(config.BlockId, entityId, requestPower, outputSlot, _blockOpenableInventoryUpdateEvent, inputConnectorComponent, blockPositionInfo);
            var components = new List<IBlockComponent>
            {
                minerComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(entityId, config.BlockId, components, blockPositionInfo);
        }
        
        public IBlock Load(string state, BlockConfigData config, EntityID entityId, BlockPositionInfo blockPositionInfo)
        {
            var (requestPower, outputSlot) = GetData(config);
            
            BlockConnectorComponent<IBlockInventory> inputConnectorComponent = config.CreateInventoryConnector(blockPositionInfo);
            var minerComponent = new VanillaElectricMinerComponent(state, config.BlockId, entityId, requestPower, outputSlot, _blockOpenableInventoryUpdateEvent, inputConnectorComponent, blockPositionInfo);
            var components = new List<IBlockComponent>
            {
                minerComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(entityId, config.BlockId, components, blockPositionInfo);
        }
        
        private (int requestPower, int outputSlot) GetData(BlockConfigData param)
        {
            var minerParam = param.Param as MinerBlockConfigParam;
            
            var oreItem = ItemConst.EmptyItemId;
            var requestPower = minerParam.RequiredPower;
            var miningTime = int.MaxValue;
            
            return (requestPower, minerParam.OutputSlot);
        }
    }
}
using System.Collections.Generic;
using Core.Const;
using Game.Block.Blocks;
using Game.Block.Blocks.Miner;
using Game.Block.Component.IOConnector;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Event;
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

        public IBlock New(BlockConfigData param, int entityId, long blockHash, BlockPositionInfo blockPositionInfo)
        {
            var (requestPower, outputSlot) = GetData(param);

            var inputConnectorComponent = GetComponent(blockPositionInfo);
            var minerComponent = new VanillaElectricMinerComponent(param.BlockId,entityId, requestPower,outputSlot,_blockOpenableInventoryUpdateEvent, inputConnectorComponent, blockPositionInfo);
            var components = new List<IBlockComponent>
            {
                minerComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(entityId, param.BlockId, components, blockPositionInfo);
        }

        public IBlock Load(BlockConfigData param, int entityId, long blockHash, string state, BlockPositionInfo blockPositionInfo)
        {
            var (requestPower, outputSlot) = GetData(param);

            var inputConnectorComponent = GetComponent(blockPositionInfo);
            var minerComponent = new VanillaElectricMinerComponent(state, param.BlockId,entityId, requestPower,outputSlot,_blockOpenableInventoryUpdateEvent, inputConnectorComponent, blockPositionInfo);
            var components = new List<IBlockComponent>
            {
                minerComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(entityId, param.BlockId, components, blockPositionInfo);
        }

        private (int requestPower, int outputSlot) GetData(BlockConfigData param)
        {
            var minerParam = param.Param as MinerBlockConfigParam;

            var oreItem = ItemConst.EmptyItemId;
            var requestPower = minerParam.RequiredPower;
            var miningTime = int.MaxValue;

            return (requestPower, minerParam.OutputSlot);
        }

        BlockConnectorComponent<IBlockInventory> GetComponent(BlockPositionInfo blockPositionInfo)
        {
            return new BlockConnectorComponent<IBlockInventory>(
                new IOConnectionSetting(
                    new ConnectDirection[] { },
                    new ConnectDirection[] { new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0) },
                    new[] { VanillaBlockType.BeltConveyor }), blockPositionInfo);
        }
    }
}
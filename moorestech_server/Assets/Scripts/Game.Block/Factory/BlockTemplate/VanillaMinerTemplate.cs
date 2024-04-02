using Core.Const;
using Core.Item.Interface;
using Game.Block.Blocks.Miner;
using Game.Block.Component;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaMinerTemplate : IBlockTemplate
    {
        public delegate VanillaMinerBase LoadMiner(
            (string state, int blockId, int entityId, long blockHash, int requestPower, int outputSlotCount, BlockOpenableInventoryUpdateEvent openableInvEvent, BlockPositionInfo blockPositionInfo, ComponentFactory componentFactory) data);

        public delegate VanillaMinerBase NewMiner(
            (int blockId, int entityId, long blockHash, int requestPower, int outputSlotCount,  BlockOpenableInventoryUpdateEvent openableInvEvent, BlockPositionInfo blockPositionInfo, ComponentFactory componentFactory) data);

        private readonly BlockOpenableInventoryUpdateEvent _blockOpenableInventoryUpdateEvent;
        private readonly ComponentFactory _componentFactory;
        

        public readonly LoadMiner _loadMiner;
        public readonly NewMiner _newMiner;

        public VanillaMinerTemplate(BlockOpenableInventoryUpdateEvent blockOpenableInventoryUpdateEvent, NewMiner newMiner, LoadMiner loadMiner, ComponentFactory componentFactory)
        {
            _blockOpenableInventoryUpdateEvent = blockOpenableInventoryUpdateEvent;
            _newMiner = newMiner;
            _loadMiner = loadMiner;
            _componentFactory = componentFactory;
        }

        public IBlock New(BlockConfigData param, int entityId, long blockHash, BlockPositionInfo blockPositionInfo)
        {
            var (requestPower, outputSlot) = GetData(param, entityId);

            return _newMiner((param.BlockId, entityId, blockHash, requestPower, outputSlot, _blockOpenableInventoryUpdateEvent, blockPositionInfo, _componentFactory));
        }

        public IBlock Load(BlockConfigData param, int entityId, long blockHash, string state, BlockPositionInfo blockPositionInfo)
        {
            var (requestPower, outputSlot) = GetData(param, entityId);

            return _loadMiner((state, param.BlockId, entityId, blockHash, requestPower,
                outputSlot, _blockOpenableInventoryUpdateEvent, blockPositionInfo, _componentFactory));
        }

        private (int, int) GetData(BlockConfigData param, int entityId)
        {
            var minerParam = param.Param as MinerBlockConfigParam;

            var oreItem = ItemConst.EmptyItemId;
            var requestPower = minerParam.RequiredPower;
            var miningTime = int.MaxValue;

            return (requestPower, minerParam.OutputSlot);
        }
    }
}
using Core.Block.Blocks;
using Core.Block.Blocks.Miner;
using Core.Block.Config.LoadConfig;
using Core.Block.Config.LoadConfig.Param;
using Core.Block.Event;
using Core.Const;
using Core.Item;
using Core.Item.Util;
using Core.Ore;

namespace Core.Block.BlockFactory.BlockTemplate
{
    public class VanillaMinerTemplate : IBlockTemplate
    {
        private readonly ItemStackFactory _itemStackFactory;
        private readonly BlockOpenableInventoryUpdateEvent _blockOpenableInventoryUpdateEvent;

        public VanillaMinerTemplate(ItemStackFactory itemStackFactory, BlockOpenableInventoryUpdateEvent blockOpenableInventoryUpdateEvent)
        {
            _itemStackFactory = itemStackFactory;
            _blockOpenableInventoryUpdateEvent = blockOpenableInventoryUpdateEvent;
        }

        public IBlock New(BlockConfigData param, int entityId, ulong blockHash)
        {
            var (requestPower, outputSlot) = GetData(param, entityId);
            
            return new VanillaMiner(param.BlockId, entityId,blockHash, requestPower, outputSlot,
                _itemStackFactory,_blockOpenableInventoryUpdateEvent);
        }

        public IBlock Load(BlockConfigData param, int entityId, ulong blockHash, string state)
        {
            var (requestPower, outputSlot) = GetData(param, entityId);
            
            return new VanillaMiner(state, param.BlockId, entityId,blockHash, requestPower,
                outputSlot,
                _itemStackFactory,_blockOpenableInventoryUpdateEvent);
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
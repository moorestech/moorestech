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
        public delegate VanillaMinerBase NewMiner((int blockId, int entityId, ulong blockHash, int requestPower, int outputSlotCount, ItemStackFactory itemFactory, BlockOpenableInventoryUpdateEvent openableInvEvent) data);
        public delegate VanillaMinerBase LoadMiner((string state,int blockId, int entityId, ulong blockHash, int requestPower, int outputSlotCount, ItemStackFactory itemFactory, BlockOpenableInventoryUpdateEvent openableInvEvent) data);
        
        public readonly NewMiner _newMiner;
        public readonly LoadMiner _loadMiner;
        
        
        private readonly ItemStackFactory _itemStackFactory;
        private readonly BlockOpenableInventoryUpdateEvent _blockOpenableInventoryUpdateEvent;

        public VanillaMinerTemplate(ItemStackFactory itemStackFactory, BlockOpenableInventoryUpdateEvent blockOpenableInventoryUpdateEvent, NewMiner newMiner, LoadMiner loadMiner)
        {
            _itemStackFactory = itemStackFactory;
            _blockOpenableInventoryUpdateEvent = blockOpenableInventoryUpdateEvent;
            _newMiner = newMiner;
            _loadMiner = loadMiner;
        }

        public IBlock New(BlockConfigData param, int entityId, ulong blockHash)
        {
            var (requestPower, outputSlot) = GetData(param, entityId);
            
            return _newMiner((param.BlockId, entityId,blockHash, requestPower, outputSlot,
                _itemStackFactory,_blockOpenableInventoryUpdateEvent));
        }

        public IBlock Load(BlockConfigData param, int entityId, ulong blockHash, string state)
        {
            var (requestPower, outputSlot) = GetData(param, entityId);
            
            return _loadMiner((state, param.BlockId, entityId,blockHash, requestPower,
                outputSlot,
                _itemStackFactory,_blockOpenableInventoryUpdateEvent));
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
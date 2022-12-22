using Core.Block.Blocks;
using Core.Block.Blocks.Chest;
using Core.Block.Config.LoadConfig;
using Core.Block.Config.LoadConfig.Param;
using Core.Block.Event;
using Core.Item;

namespace Core.Block.BlockFactory.BlockTemplate
{
    public class VanillaChestTemplate : IBlockTemplate
    {
        private readonly ItemStackFactory _itemStackFactory;
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdateEvent;

        public VanillaChestTemplate(ItemStackFactory itemStackFactory,BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            _itemStackFactory = itemStackFactory;
            _blockInventoryUpdateEvent = blockInventoryUpdateEvent;

        }

        public IBlock New(BlockConfigData param, int entityId, ulong blockHash)
        {
            var chest = param.Param as ChestConfigParam;
            return new VanillaChest(param.BlockId, entityId,blockHash,chest.ChestItemNum,_itemStackFactory, _blockInventoryUpdateEvent);
        }

        public IBlock Load(BlockConfigData param, int entityId, ulong blockHash, string state)
        {
            var chest = param.Param as ChestConfigParam;
            return new VanillaChest(state,param.BlockId, entityId,blockHash,chest.ChestItemNum,_itemStackFactory, _blockInventoryUpdateEvent);
        }
    }
}
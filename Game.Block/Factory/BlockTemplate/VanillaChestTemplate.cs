using Core.Item;
using Game.Block.Blocks.Chest;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Factory.BlockTemplate
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
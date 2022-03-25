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
        private IBlockTemplate _blockTemplateImplementation;

        public VanillaChestTemplate(ItemStackFactory itemStackFactory,IBlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            _itemStackFactory = itemStackFactory;
            _blockInventoryUpdateEvent = blockInventoryUpdateEvent as BlockOpenableInventoryUpdateEvent;

        }

        public IBlock New(BlockConfigData param, int entityId)
        {
            var chest = param.Param as ChestConfigParam;
            return new VanillaChest(param.BlockId, entityId,chest.ChestItemNum,_itemStackFactory, _blockInventoryUpdateEvent);
        }

        public IBlock Load(BlockConfigData param, int entityId, string state)
        {
            var chest = param.Param as ChestConfigParam;
            return new VanillaChest(param.BlockId, entityId,chest.ChestItemNum,_itemStackFactory, _blockInventoryUpdateEvent);
        }
    }
}
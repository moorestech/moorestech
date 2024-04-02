using Game.Block.Blocks.Chest;
using Game.Block.Component;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaChestTemplate : IBlockTemplate
    {
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdateEvent;
        private readonly ComponentFactory _componentFactory;


        public VanillaChestTemplate(BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent, ComponentFactory componentFactory)
        {
            _blockInventoryUpdateEvent = blockInventoryUpdateEvent;
            _componentFactory = componentFactory;
        }

        public IBlock New(BlockConfigData param, int entityId, long blockHash, BlockPositionInfo blockPositionInfo)
        {
            var chest = param.Param as ChestConfigParam;
            return new VanillaChest(param.BlockId, entityId, blockHash, chest.ChestItemNum, _blockInventoryUpdateEvent, blockPositionInfo, _componentFactory);
        }

        public IBlock Load(BlockConfigData param, int entityId, long blockHash, string state, BlockPositionInfo blockPositionInfo)
        {
            var chest = param.Param as ChestConfigParam;
            return new VanillaChest(state, param.BlockId, entityId, blockHash, chest.ChestItemNum, _blockInventoryUpdateEvent, blockPositionInfo, _componentFactory);
        }
    }
}
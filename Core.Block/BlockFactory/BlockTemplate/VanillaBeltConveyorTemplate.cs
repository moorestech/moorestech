using Core.Block.Blocks;
using Core.Block.Blocks.BeltConveyor;
using Core.Block.Config.LoadConfig;
using Core.Block.Config.LoadConfig.Param;
using Core.Item;

namespace Core.Block.BlockFactory.BlockTemplate
{
    public class VanillaBeltConveyorTemplate : IBlockTemplate
    {
        private readonly ItemStackFactory _itemStackFactory;

        public VanillaBeltConveyorTemplate(ItemStackFactory itemStackFactory)
        {
            _itemStackFactory = itemStackFactory;
        }

        public IBlock New(BlockConfigData param, int entityId)
        {
            var beltConveyor = param.Param as BeltConveyorConfigParam;
            return new VanillaBeltConveyor(param.BlockId, entityId, _itemStackFactory, beltConveyor.BeltConveyorItemNum,
                beltConveyor.TimeOfItemEnterToExit);
        }

        public IBlock Load(BlockConfigData param, int entityId, string state)
        {
            var beltConveyor = param.Param as BeltConveyorConfigParam;
            return new VanillaBeltConveyor(param.BlockId, entityId, state, _itemStackFactory,
                beltConveyor.BeltConveyorItemNum, beltConveyor.TimeOfItemEnterToExit);
        }
    }
}
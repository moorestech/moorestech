using Core.Block.BeltConveyor;
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

        public IBlock New(BlockConfigData param, int intId)
        {
            var beltConveyor = param.Param as BeltConveyorConfigParam;
            return new VanillaBeltConveyor(param.BlockId,intId,_itemStackFactory,beltConveyor.BeltConveyorItemNum,beltConveyor.TimeOfItemEnterToExit);
        }

        public IBlock Load(BlockConfigData param, int intId, string state)
        {
            var beltConveyor = param.Param as BeltConveyorConfigParam;
            return new VanillaBeltConveyor(param.BlockId,intId,state,_itemStackFactory,beltConveyor.BeltConveyorItemNum,beltConveyor.TimeOfItemEnterToExit);
        }
    }
}
using Core.Item;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaBeltConveyorTemplate : IBlockTemplate
    {
        private readonly ItemStackFactory _itemStackFactory;

        public VanillaBeltConveyorTemplate(ItemStackFactory itemStackFactory)
        {
            _itemStackFactory = itemStackFactory;
        }

        public IBlock New(BlockConfigData param, int entityId, ulong blockHash)
        {
            var beltConveyor = param.Param as BeltConveyorConfigParam;
            return new VanillaBeltConveyor(param.BlockId, entityId, blockHash, _itemStackFactory, beltConveyor.BeltConveyorItemNum,
                beltConveyor.TimeOfItemEnterToExit);
        }

        public IBlock Load(BlockConfigData param, int entityId, ulong blockHash, string state)
        {
            var beltConveyor = param.Param as BeltConveyorConfigParam;
            return new VanillaBeltConveyor(param.BlockId, entityId, blockHash, state, _itemStackFactory,
                beltConveyor.BeltConveyorItemNum, beltConveyor.TimeOfItemEnterToExit);
        }
    }
}
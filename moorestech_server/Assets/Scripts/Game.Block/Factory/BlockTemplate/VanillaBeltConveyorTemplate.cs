using Core.Item;
using Game.Block.Interface;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.World.Interface.DataStore;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaBeltConveyorTemplate : IBlockTemplate
    {
        private readonly ItemStackFactory _itemStackFactory;

        public VanillaBeltConveyorTemplate(ItemStackFactory itemStackFactory)
        {
            _itemStackFactory = itemStackFactory;
        }

        public IBlock New(BlockConfigData param, int entityId, long blockHash,BlockPositionInfo blockPositionInfo)
        {
            var beltConveyor = param.Param as BeltConveyorConfigParam;
            return new VanillaBeltConveyor(param.BlockId, entityId, blockHash, _itemStackFactory,
                beltConveyor.BeltConveyorItemNum,
                beltConveyor.TimeOfItemEnterToExit,blockPositionInfo);
        }

        public IBlock Load(BlockConfigData param, int entityId, long blockHash, string state,BlockPositionInfo blockPositionInfo)
        {
            var beltConveyor = param.Param as BeltConveyorConfigParam;
            return new VanillaBeltConveyor(param.BlockId, entityId, blockHash, state, _itemStackFactory,
                beltConveyor.BeltConveyorItemNum, beltConveyor.TimeOfItemEnterToExit,blockPositionInfo);
        }
    }
}
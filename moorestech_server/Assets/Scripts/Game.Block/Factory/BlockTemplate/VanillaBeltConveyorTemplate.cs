using Server.Core.Item;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Component;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaBeltConveyorTemplate : IBlockTemplate
    {
        private readonly ComponentFactory _componentFactory;
        private readonly ItemStackFactory _itemStackFactory;

        public VanillaBeltConveyorTemplate(ItemStackFactory itemStackFactory, ComponentFactory componentFactory)
        {
            _itemStackFactory = itemStackFactory;
            _componentFactory = componentFactory;
        }

        public IBlock New(BlockConfigData param, int entityId, long blockHash, BlockPositionInfo blockPositionInfo)
        {
            var beltConveyor = param.Param as BeltConveyorConfigParam;
            return new VanillaBeltConveyor(param.BlockId, entityId, blockHash, _itemStackFactory, beltConveyor.BeltConveyorItemNum, beltConveyor.TimeOfItemEnterToExit, blockPositionInfo, _componentFactory);
        }

        public IBlock Load(BlockConfigData param, int entityId, long blockHash, string state, BlockPositionInfo blockPositionInfo)
        {
            var beltConveyor = param.Param as BeltConveyorConfigParam;
            return new VanillaBeltConveyor(param.BlockId, entityId, blockHash, state, _itemStackFactory, beltConveyor.BeltConveyorItemNum, beltConveyor.TimeOfItemEnterToExit, blockPositionInfo, _componentFactory);
        }
    }
}
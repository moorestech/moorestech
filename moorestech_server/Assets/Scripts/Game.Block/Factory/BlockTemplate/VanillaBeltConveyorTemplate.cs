using Core.Item.Interface;
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


        public VanillaBeltConveyorTemplate(IItemStackFactory itemStackFactory, ComponentFactory componentFactory)
        {
            _componentFactory = componentFactory;
        }

        public IBlock New(BlockConfigData param, int entityId, long blockHash, BlockPositionInfo blockPositionInfo)
        {
            var beltConveyor = param.Param as BeltConveyorConfigParam;
            return new VanillaBeltConveyor(param.BlockId, entityId, blockHash, beltConveyor.BeltConveyorItemNum, beltConveyor.TimeOfItemEnterToExit, blockPositionInfo, _componentFactory);
        }

        public IBlock Load(BlockConfigData param, int entityId, long blockHash, string state, BlockPositionInfo blockPositionInfo)
        {
            var beltConveyor = param.Param as BeltConveyorConfigParam;
            return new VanillaBeltConveyor(param.BlockId, entityId, blockHash, state, beltConveyor.BeltConveyorItemNum, beltConveyor.TimeOfItemEnterToExit, blockPositionInfo, _componentFactory);
        }
    }
}
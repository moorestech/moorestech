using Game.Block.Blocks.BeltConveyor;
using Game.Block.Component;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaBeltConveyorTemplate : IBlockTemplate
    {
        public IBlock New(BlockConfigData param, int entityId, long blockHash, BlockPositionInfo blockPositionInfo)
        {
            var beltConveyor = param.Param as BeltConveyorConfigParam;
            return new VanillaBeltConveyor(param.BlockId, entityId, blockHash, beltConveyor.BeltConveyorItemNum, beltConveyor.TimeOfItemEnterToExit, blockPositionInfo);
        }

        public IBlock Load(BlockConfigData param, int entityId, long blockHash, string state, BlockPositionInfo blockPositionInfo)
        {
            var beltConveyor = param.Param as BeltConveyorConfigParam;
            return new VanillaBeltConveyor(param.BlockId, entityId, blockHash, state, beltConveyor.BeltConveyorItemNum, beltConveyor.TimeOfItemEnterToExit, blockPositionInfo);
        }
    }
}
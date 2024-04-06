using Game.Block.Blocks.Chest;
using Game.Block.Component;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaChestTemplate : IBlockTemplate
    {
        public IBlock New(BlockConfigData param, int entityId, long blockHash, BlockPositionInfo blockPositionInfo)
        {
            var chest = param.Param as ChestConfigParam;
            return new VanillaChest(param.BlockId, entityId, blockHash, chest.ChestItemNum, blockPositionInfo);
        }

        public IBlock Load(BlockConfigData param, int entityId, long blockHash, string state, BlockPositionInfo blockPositionInfo)
        {
            var chest = param.Param as ChestConfigParam;
            return new VanillaChest(state, param.BlockId, entityId, blockHash, chest.ChestItemNum, blockPositionInfo);
        }
    }
}
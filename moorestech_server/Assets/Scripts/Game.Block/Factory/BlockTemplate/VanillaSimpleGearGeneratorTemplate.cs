using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaSimpleGearGeneratorTemplate : IBlockTemplate
    {
        public IBlock New(BlockConfigData config, int entityId, BlockPositionInfo blockPositionInfo)
        {
            throw new System.NotImplementedException();
        }
        public IBlock Load(string state, BlockConfigData config, int entityId, BlockPositionInfo blockPositionInfo)
        {
            throw new System.NotImplementedException();
        }
    }
}
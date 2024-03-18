using Game.Block.Base;
using Game.Block.Blocks.ElectricPole;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaElectricPoleTemplate : IBlockTemplate
    {
        public IBlock New(BlockConfigData param, int entityId, long blockHash)
        {
            return new VanillaElectricPole(param.BlockId, entityId, blockHash);
        }

        public IBlock Load(BlockConfigData param, int entityId, long blockHash, string state)
        {
            return new VanillaElectricPole(param.BlockId, entityId, blockHash);
        }
    }
}
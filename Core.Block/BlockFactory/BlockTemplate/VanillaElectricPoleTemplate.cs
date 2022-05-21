using Core.Block.Blocks;
using Core.Block.Blocks.ElectricPole;
using Core.Block.Config.LoadConfig;

namespace Core.Block.BlockFactory.BlockTemplate
{
    public class VanillaElectricPoleTemplate : IBlockTemplate
    {
        public IBlock New(BlockConfigData param, int entityId, ulong blockHash)
        {
            return new VanillaElectricPole(param.BlockId, entityId,blockHash);
        }

        public IBlock Load(BlockConfigData param, int entityId, ulong blockHash, string state)
        {
            return new VanillaElectricPole(param.BlockId, entityId,blockHash);
        }
    }
}
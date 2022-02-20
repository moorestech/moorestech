using Core.Block.Blocks;
using Core.Block.Blocks.ElectricPole;
using Core.Block.Config.LoadConfig;

namespace Core.Block.BlockFactory.BlockTemplate
{
    public class VanillaElectricPoleTemplate : IBlockTemplate
    {
        public IBlock New(BlockConfigData param, int entityId)
        {
            return new VanillaElectricPole(param.BlockId, entityId);
        }

        public IBlock Load(BlockConfigData param, int entityId, string state)
        {
            return new VanillaElectricPole(param.BlockId, entityId);
        }
    }
}
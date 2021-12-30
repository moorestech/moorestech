using Core.Block.Config.LoadConfig;
using Core.Block.Config.LoadConfig.Param;
using Core.Block.ElectricPole;
using Core.Block.Machine;

namespace Core.Block.BlockFactory.BlockTemplate
{
    public class VanillaElectricPoleTemplate : IBlockTemplate
    {
        public IBlock New(BlockConfigData param, int intId)
        {
            return new VanillaElectricPole(param.BlockId,intId);
        }

        public IBlock Load(BlockConfigData param, int intId, string state)
        {
            return new VanillaElectricPole(param.BlockId,intId);
        }
    }
}
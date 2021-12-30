using Core.Block.Config.LoadConfig;
using Core.Block.PowerGenerator;

namespace Core.Block.BlockFactory.BlockTemplate
{
    public class VanillaPowerGeneratorTemplate : IBlockTemplate
    {
        public IBlock New(BlockConfigData param, int intId)
        {
            return new VanillaPowerGenerator(param.BlockId,intId);
        }

        public IBlock Load(BlockConfigData param, int intId, string state)
        {
            return new VanillaPowerGenerator(param.BlockId,intId);
        }
    }
}
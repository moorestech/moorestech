using Game.Block.Blocks.ElectricPole;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaGearEnergyTransformerTemplate : IBlockTemplate
    {
        public IBlock New(BlockConfigData param, int entityId, ulong blockHash)
        {
            return new VanillaGearEnergyTransformer(param.BlockId, entityId,blockHash);
        }

        public IBlock Load(BlockConfigData param, int entityId, ulong blockHash, string state)
        {
            return new VanillaGearEnergyTransformer(param.BlockId, entityId,blockHash);
        }
    }
}
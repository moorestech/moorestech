using Game.Block.Interface;
using Game.Block.Blocks.ElectricPole;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.World.Interface.DataStore;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaGearEnergyTransformerTemplate : IBlockTemplate
    {
        public IBlock New(BlockConfigData param, int entityId, long blockHash,BlockPositionInfo blockPositionInfo)
        {
            return new VanillaGearEnergyTransformer(param.BlockId, entityId, blockHash, blockPositionInfo);
        }

        public IBlock Load(BlockConfigData param, int entityId, long blockHash, string state,BlockPositionInfo blockPositionInfo)
        {
            return new VanillaGearEnergyTransformer(param.BlockId, entityId, blockHash , blockPositionInfo);
        }
    }
}
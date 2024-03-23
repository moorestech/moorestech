using Core.EnergySystem.Gear;
using Game.Block.Interface;

namespace Game.Block.Blocks.ElectricPole
{
    public class VanillaGearEnergyTransformer : VanillaEnergyTransformerBase, IGearEnergyTransformer
    {
        public VanillaGearEnergyTransformer(int blockId, int entityId, long blockHash,BlockPositionInfo blockPositionInfo) : base(blockId, entityId, blockHash,blockPositionInfo)
        {
        }
    }
}
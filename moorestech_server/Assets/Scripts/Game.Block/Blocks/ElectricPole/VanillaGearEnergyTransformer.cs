using Core.EnergySystem.Gear;

namespace Game.Block.Blocks.ElectricPole
{
    public class VanillaGearEnergyTransformer : VanillaEnergyTransformerBase, IGearEnergyTransformer
    {
        public VanillaGearEnergyTransformer(int blockId, int entityId, ulong blockHash) : base(blockId, entityId, blockHash)
        {
        }
    }
}
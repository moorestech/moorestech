using Core.EnergySystem.Electric;

namespace Game.Block.Blocks.ElectricPole
{
    public class VanillaElectricPole : VanillaEnergyTransformerBase, IElectricPole
    {
        public VanillaElectricPole(int blockId, int entityId, long blockHash) : base(blockId, entityId, blockHash)
        {
        }
    }
}
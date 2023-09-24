using Core.EnergySystem.Electric;

namespace Core.Block.Blocks.ElectricPole
{
    public class VanillaElectricPole : VanillaEnergyTransformerBase, IElectricPole
    {
        public VanillaElectricPole(int blockId, int entityId, ulong blockHash) : base(blockId, entityId, blockHash)
        {
        }
    }
}
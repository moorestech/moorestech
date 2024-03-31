using Server.Core.EnergySystem.Electric;
using Game.Block.Interface;

namespace Game.Block.Blocks.ElectricPole
{
    public class VanillaElectricPole : VanillaEnergyTransformerBase, IElectricPole
    {
        public VanillaElectricPole(int blockId, int entityId, long blockHash, BlockPositionInfo blockPositionInfo) : base(blockId, entityId, blockHash, blockPositionInfo)
        {
        }
    }
}
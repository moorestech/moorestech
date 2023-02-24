using System;
using Core.Electric;

namespace Core.Block.Blocks.ElectricPole
{
    public class VanillaElectricPole : IElectricPole, IBlock
    {
        public int EntityId { get; }
        public int BlockId { get; }
        public ulong BlockHash { get; }
        public event Action<ChangedBlockState> OnBlockStateChange;

        public VanillaElectricPole(int blockId, int entityId, ulong blockHash)
        {
            BlockId = blockId;
            EntityId = entityId;
            BlockHash = blockHash;
        }
        public string GetSaveState()
        {
            return String.Empty;
        }
    }
}
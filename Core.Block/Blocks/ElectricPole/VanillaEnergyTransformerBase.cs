using System;
using Core.Block.Blocks.State;
using Core.EnergySystem;

namespace Core.Block.Blocks.ElectricPole
{
    public abstract class VanillaEnergyTransformerBase : IEnergyTransformer, IBlock
    {
        public int EntityId { get; }
        public int BlockId { get; }
        public ulong BlockHash { get; }
        public event Action<ChangedBlockState> OnBlockStateChange;

        protected VanillaEnergyTransformerBase(int blockId, int entityId, ulong blockHash)
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
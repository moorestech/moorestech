using System;
using Core.EnergySystem;
using Game.Block.Base;
using Game.Block.Interface;
using Game.Block.Interface.State;

namespace Game.Block.Blocks.ElectricPole
{
    public abstract class VanillaEnergyTransformerBase : IEnergyTransformer, IBlock
    {
        protected VanillaEnergyTransformerBase(int blockId, int entityId, long blockHash)
        {
            BlockId = blockId;
            EntityId = entityId;
            BlockHash = blockHash;
        }

        public int BlockId { get; }
        public long BlockHash { get; }
        public event Action<ChangedBlockState> OnBlockStateChange;

        public string GetSaveState()
        {
            return string.Empty;
        }

        public int EntityId { get; }
    }
}
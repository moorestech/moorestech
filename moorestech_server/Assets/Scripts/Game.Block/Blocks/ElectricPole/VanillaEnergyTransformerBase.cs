using System;
using Core.EnergySystem;
using Game.Block.Interface;
using Game.Block.Interface.State;
using UniRx;

namespace Game.Block.Blocks.ElectricPole
{
    public abstract class VanillaEnergyTransformerBase : IEnergyTransformer, IBlock
    {
        protected readonly Subject<ChangedBlockState> _onBlockStateChange = new();

        protected VanillaEnergyTransformerBase(int blockId, int entityId, long blockHash, BlockPositionInfo blockPositionInfo)
        {
            BlockId = blockId;
            EntityId = entityId;
            BlockHash = blockHash;
            BlockPositionInfo = blockPositionInfo;
        }
        public int BlockId { get; }
        public long BlockHash { get; }

        public IBlockComponentManager ComponentManager { get; } = new BlockComponentManager();
        public BlockPositionInfo BlockPositionInfo { get; }
        public IObservable<ChangedBlockState> BlockStateChange => _onBlockStateChange;

        public string GetSaveState()
        {
            return string.Empty;
        }

        public bool Equals(IBlock other)
        {
            if (other is null) return false;
            return EntityId == other.EntityId && BlockId == other.BlockId && BlockHash == other.BlockHash;
        }
        public int EntityId { get; }

        public override bool Equals(object obj)
        {
            return obj is IBlock other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(EntityId, BlockId, BlockHash);
        }
    }
}
using System;
using Game.Block.Interface;
using Game.Block.Interface.State;
using UniRx;

namespace Game.Block.Blocks
{
    public class VanillaBlock : IBlock
    {
        public VanillaBlock(int blockId, int entityId, long blockHash, BlockPositionInfo blockPositionInfo)
        {
            BlockId = blockId;
            EntityId = entityId;
            BlockHash = blockHash;
            BlockPositionInfo = blockPositionInfo;
        }
        public IBlockComponentManager ComponentManager { get; } = new BlockComponentManager();
        public BlockPositionInfo BlockPositionInfo { get; }
        public IObservable<ChangedBlockState> BlockStateChange { get; } = new Subject<ChangedBlockState>();
        public int EntityId { get; }
        public int BlockId { get; }
        public long BlockHash { get; }

        public string GetSaveState()
        {
            return string.Empty;
        }



        public bool Equals(IBlock other)
        {
            if (other is null) return false;
            return EntityId == other.EntityId && BlockId == other.BlockId && BlockHash == other.BlockHash;
        }

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
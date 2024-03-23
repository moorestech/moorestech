using System;
using Game.Block.Interface;
using Game.Block.Interface;
using Game.Block.Interface.State;
using UniRx;

namespace Game.Block.Blocks
{
    public class VanillaBlock : IBlock
    {
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

        public VanillaBlock(int blockId, int entityId, long blockHash, BlockPositionInfo blockPositionInfo)
        {
            BlockId = blockId;
            EntityId = entityId;
            BlockHash = blockHash;
            BlockPositionInfo = blockPositionInfo;
        }
    }
}
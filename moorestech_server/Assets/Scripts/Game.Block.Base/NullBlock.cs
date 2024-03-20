using System;
using Core.Const;
using Game.Block.Interface;
using Game.Block.Interface.State;
using UniRx;

namespace Game.Block.Base
{
    public class NullBlock : IBlock
    {
        public IObservable<ChangedBlockState> OnBlockStateChange => _onBlockStateChange;
        Subject<ChangedBlockState> _onBlockStateChange;
        public int EntityId => BlockConst.NullBlockEntityId;
        public int BlockId => BlockConst.EmptyBlockId;
        public long BlockHash => 0;
        public IBlockComponentManager ComponentManager { get; } = new BlockComponentManager();

        public string GetSaveState()
        {
            return string.Empty;
        }

    }
}
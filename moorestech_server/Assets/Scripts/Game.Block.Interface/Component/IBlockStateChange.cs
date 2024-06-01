using System;
using Game.Block.Interface.ComponentAttribute;
using Game.Block.Interface.State;

namespace Game.Block.Interface.Component
{
    [DisallowMultiple]
    public interface IBlockStateChange : IBlockComponent
    {
        public IObservable<BlockState> OnChangeBlockState { get; }
        public BlockState GetBlockState();
    }
}
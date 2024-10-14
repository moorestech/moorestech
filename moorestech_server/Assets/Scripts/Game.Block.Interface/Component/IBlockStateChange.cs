using System;
using Game.Block.Interface.ComponentAttribute;
using UniRx;

namespace Game.Block.Interface.Component
{
    [DisallowMultiple]
    public interface IBlockStateChange : IBlockComponent
    {
        public IObservable<Unit> OnChangeBlockState { get; }
        public BlockStateTypes GetBlockState();
    }
    
    public struct BlockStateTypes
    {
        public readonly string CurrentStateType;
        public readonly string PreviousStateType;
        
        public BlockStateTypes(string currentStateType, string previousStateType)
        {
            CurrentStateType = currentStateType;
            PreviousStateType = previousStateType;
        }
    }
}
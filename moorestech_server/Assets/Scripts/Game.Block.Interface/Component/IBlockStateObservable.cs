using System;
using UniRx;

namespace Game.Block.Interface.Component
{
    public interface IBlockStateObservable : IBlockStateDetail
    {
        public IObservable<Unit> OnChangeBlockState { get; }
    }
}